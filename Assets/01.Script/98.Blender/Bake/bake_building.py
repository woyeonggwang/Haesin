import os
# Background Blender script: OBJ(high) -> proxy/decimate -> atlas UV -> bake BC+N -> FBX
# usage: blender -b --factory-startup -P bake_building.py -- <obj> <outdir> <name> <atlas> <target_tris>
import bpy, bmesh, sys, os, math, time, json
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
OBJ, OUT, NAME = argv[0], argv[1], argv[2]
ATLAS = int(argv[3]); TARGET = int(argv[4])
os.makedirs(OUT, exist_ok=True)
LOG = os.path.join(OUT, NAME + "_log.txt")
T0 = time.time()
def log(*a):
    s = "[%7.1fs] " % (time.time() - T0) + " ".join(str(x) for x in a)
    print(s, flush=True)
    with open(LOG, "a", encoding="utf-8") as f: f.write(s + "\n")

open(LOG, "w").close()
log("start", OBJ)

# ---------- GPU ----------
try:
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for dt in ("OPTIX", "CUDA", "HIP", "ONEAPI", "METAL"):
        try:
            prefs.compute_device_type = dt
            prefs.get_devices()
            devs = [d for d in prefs.devices if d.type == dt]
            if devs:
                for d in prefs.devices: d.use = (d.type == dt)
                log("GPU", dt, [d.name for d in devs]); break
        except Exception as e:
            pass
except Exception as e:
    log("gpu setup failed", e)

scn = bpy.context.scene
scn.render.engine = "CYCLES"
try: scn.cycles.device = "CPU" if os.environ.get("HAESIN_CPU") else "GPU"
except: pass
log("device", scn.cycles.device)

# ---------- import ----------
# factory-startup 장면의 기본 큐브·카메라·조명을 지운다(안 지우면 큐브가 같이 구워져 원점에 흰 상자가 생김)
for _o in list(bpy.data.objects): bpy.data.objects.remove(_o, do_unlink=True)
bpy.ops.wm.obj_import(filepath=OBJ, forward_axis="NEGATIVE_Z", up_axis="Y")
high = [o for o in scn.objects if o.type == "MESH"]
log("imported objects", len(high), "tris", sum(len(o.data.polygons) for o in high))

# rebuild materials from MTL (importer mangles absolute paths and ignores 'norm')
mtl = {}
cur = None
with open(os.path.splitext(OBJ)[0] + ".mtl", encoding="utf-8") as f:
    for line in f:
        line = line.strip()
        if line.startswith("newmtl "): cur = line[7:].strip(); mtl[cur] = {}
        elif cur and line.startswith("map_Kd "): mtl[cur]["bc"] = line[7:].strip()
        elif cur and line.startswith("norm "): mtl[cur]["n"] = line[5:].strip()
        elif cur and line.startswith("Kd "): mtl[cur]["kd"] = [float(x) for x in line[3:].split()]
imgcache = {}
def getimg(p, noncolor):
    if p in imgcache: return imgcache[p]
    try:
        im = bpy.data.images.load(p, check_existing=True)
        if noncolor: im.colorspace_settings.name = "Non-Color"
    except Exception as e:
        log("img fail", p, e); im = None
    imgcache[p] = im; return im
nfix = 0
for m in bpy.data.materials:
    info = mtl.get(m.name)
    if info is None: continue
    m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    outn = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], outn.inputs[0])
    kd = info.get("kd", [1, 1, 1])
    if "bc" in info and getimg(info["bc"], False):
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = getimg(info["bc"], False)
        if kd != [1, 1, 1]:
            mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
            mix.inputs[0].default_value = 1.0
            nt.links.new(t.outputs[0], mix.inputs[6]); mix.inputs[7].default_value = (kd[0], kd[1], kd[2], 1)
            nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
        else:
            nt.links.new(t.outputs[0], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (kd[0], kd[1], kd[2], 1)
    if "n" in info and getimg(info["n"], True):
        tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = getimg(info["n"], True)
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(tn.outputs[0], nm.inputs["Color"]); nt.links.new(nm.outputs[0], bsdf.inputs["Normal"])
    nfix += 1
log("materials rebuilt", nfix, "of", len(mtl), "images", len([i for i in imgcache.values() if i]))

hcol = bpy.data.collections.new("HIGH"); scn.collection.children.link(hcol)
for o in high:
    for c in o.users_collection: c.objects.unlink(o)
    hcol.objects.link(o)

# ---------- low build ----------
PROXY_KEYS = ("door", "window", "changho", "munsal", "gyeokja")
def is_proxy(name):
    n = name.lower()
    if "frame" in n: return False
    return any(k in n for k in PROXY_KEYS)

def obb_box(o):
    me = o.data
    pts = [o.matrix_world @ v.co for v in me.vertices]
    if len(pts) < 4: return None
    c = sum(pts, Vector()) / len(pts)
    # covariance
    cov = [[0.0]*3 for _ in range(3)]
    for p in pts:
        d = p - c
        for i in range(3):
            for j in range(3): cov[i][j] += d[i]*d[j]
    import numpy as np
    w, v = np.linalg.eigh(np.array(cov))
    axes = [Vector(v[:, i]) for i in range(3)]
    mins = [min((p - c).dot(a) for p in pts) for a in axes]
    maxs = [max((p - c).dot(a) for p in pts) for a in axes]
    bm = bmesh.new()
    vs = []
    for ix in (0, 1):
        for iy in (0, 1):
            for iz in (0, 1):
                p = c + axes[0]*(mins[0] if ix == 0 else maxs[0]) + axes[1]*(mins[1] if iy == 0 else maxs[1]) + axes[2]*(mins[2] if iz == 0 else maxs[2])
                vs.append(bm.verts.new(p))
    idx = lambda x, y, z: x*4 + y*2 + z
    faces = [(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)]
    for f in faces:
        try: bm.faces.new([vs[i] for i in f])
        except: pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(o.name + "_proxy")
    bm.to_mesh(mesh); bm.free()
    return mesh

lcol = bpy.data.collections.new("LOW"); scn.collection.children.link(lcol)

def category(name):
    n = name.lower()
    # 지붕 밑(서까래·개판)과 박공벽은 목부. 예전엔 roof0/roof1/roof_ 만 지붕으로 봐서 SM_SJJ_RoofA·SM_CCJ_RoofTileB 같은 기와가 목부로 구워졌음
    if any(k in n for k in ("rafter", "rooffloor", "roofinner", "roofwall")): return "wood"
    if "roof" in n or any(k in n for k in ("kiwa", "giwa", "roofplate", "yongmeoli", "chimi", "japsang", "ridge", "mangwa", "sumaksae")): return "roof"
    if ("door" in n or "window" in n or "changho" in n or "hyeonpan" in n or "hyunpan" in n or "signboard" in n or "pyeonaek" in n or "koreanpaper" in n) and "frame" not in n: return "win"
    if any(k in n for k in ("stereobate", "stair", "steppingstone", "outstone", "foundationstone", "soilfloor", "outfloor", "stela", "brick", "gidan", "stone", "road", "ground", "woldae")): return "stone"
    return "wood"
DENS = {"win": 140.0, "wood": 100.0, "roof": 56.0, "stone": 40.0}
CAT_RATIO_MUL = {"win": 1.0, "wood": 1.0, "roof": 0.8, "stone": 0.7}
CAT_MIN_RATIO = {"win": 0.0, "wood": 0.0, "roof": 0.2, "stone": 0.0}

cats = {}
for o in high: cats.setdefault(category(o.name), []).append(o)
total_nonproxy = sum(len(o.data.polygons) for o in high if not (is_proxy(o.name) and len(o.data.polygons) > 40))
ratio = min(1.0, max(0.02, (TARGET * 0.9) / max(1, total_nonproxy)))
log("categories", {k: len(v) for k, v in cats.items()}, "non-proxy tris", total_nonproxy, "ratio", round(ratio, 4))

low_by_cat = {}
nproxy = 0
for cat, objs in cats.items():
    lst = []
    for o in objs:
        if is_proxy(o.name) and len(o.data.polygons) > 40:
            me = obb_box(o)
            if me is None: continue
            lo = bpy.data.objects.new(o.name + "_L", me); lcol.objects.link(lo); lst.append(lo); nproxy += 1
            continue
        me = o.data.copy()
        lo = bpy.data.objects.new(o.name + "_L", me); lo.matrix_world = o.matrix_world.copy(); lcol.objects.link(lo)
        nt = len(me.polygons)
        if nt > 300:
            md = lo.modifiers.new("dec", "DECIMATE"); md.decimate_type = "COLLAPSE"
            md.ratio = min(1.0, max(ratio * CAT_RATIO_MUL[cat], CAT_MIN_RATIO[cat], 150.0 / nt))
            md.use_collapse_triangulate = True
        lst.append(lo)
    low_by_cat[cat] = lst
log("low built, proxies", nproxy)

bpy.ops.object.select_all(action="DESELECT")
for o in high: o.select_set(True)
bpy.context.view_layer.objects.active = high[0]
bpy.ops.object.join()
high_joined = bpy.context.view_layer.objects.active
high_joined.name = "HIGH_" + NAME
log("high joined", len(high_joined.data.polygons))

dg = bpy.context.evaluated_depsgraph_get()
lows = {}
for cat, lst in low_by_cat.items():
    bm = bmesh.new()
    for lo in lst:
        if lo.modifiers:
            ev = lo.evaluated_get(dg); me = bpy.data.meshes.new_from_object(ev)
        else:
            me = lo.data.copy()
        me.transform(lo.matrix_world); bm.from_mesh(me); bpy.data.meshes.remove(me)
    for lo in lst:
        m = lo.data; bpy.data.objects.remove(lo)
        if m.users == 0: bpy.data.meshes.remove(m)
    me = bpy.data.meshes.new(NAME + "_" + cat); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    ob = bpy.data.objects.new(NAME + "_" + cat, me); lcol.objects.link(ob)
    lows[cat] = ob
log("low tris", {c: len(o.data.polygons) for c, o in lows.items()})

import numpy as _np
bk = scn.render.bake
bk.use_selected_to_active = True
bk.cage_extrusion = 0.03
bk.max_ray_distance = 0.6
bk.margin = 8
scn.cycles.samples = 4
info = {"name": NAME, "high_tris": len(high_joined.data.polygons), "proxies": nproxy, "cats": {}}
for cat, ob in lows.items():
    me = ob.data
    area = sum(p.area for p in me.polygons)
    px = math.sqrt(area * 1.7) * DENS[cat]
    size = 512
    while size < px and size < ATLAS: size *= 2
    while me.uv_layers: me.uv_layers.remove(me.uv_layers[0])
    uv = me.uv_layers.new(name="Atlas"); me.uv_layers.active = uv
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    marg = 3.0 / size
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=marg, area_weight=0.0, scale_to_bounds=False)
    try: bpy.ops.uv.pack_islands(margin=marg, rotate=True)
    except Exception as e: log("pack failed", e)
    bpy.ops.object.mode_set(mode="OBJECT")
    img_bc = bpy.data.images.new(NAME + "_" + cat + "_BC", size, size, alpha=False)
    img_n = bpy.data.images.new(NAME + "_" + cat + "_N", size, size, alpha=False); img_n.colorspace_settings.name = "Non-Color"
    mat = bpy.data.materials.new("ATLAS_" + NAME + "_" + cat); mat.use_nodes = True
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage"); mat.node_tree.nodes.active = tex
    me.materials.clear(); me.materials.append(mat)
    bpy.ops.object.select_all(action="DESELECT")
    high_joined.select_set(True); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    # 지붕은 기와(위)와 서까래(아래)가 한 메시라 크게 줄이면 낮은 면이 두 층 사이로 들어간다 -> 케이지를 멀리서 쏜다
    bk.cage_extrusion = 0.45 if cat == "roof" else 0.03
    bk.max_ray_distance = 1.2 if cat == "roof" else 0.6
    tex.image = img_bc
    scn.cycles.samples = 16 if cat == "roof" else 4   # 기와 골이 계단·얼룩으로 구워지지 않게 지붕은 텍셀 안에서 여러 번 쏨
    bk.use_pass_direct = False; bk.use_pass_indirect = False; bk.use_pass_color = True
    bpy.ops.object.bake(type="DIFFUSE")
    tex.image = img_n; bk.normal_space = "TANGENT"
    bpy.ops.object.bake(type="NORMAL")
    img_bc.filepath_raw = os.path.join(OUT, NAME + "_" + cat + "_BC.png"); img_bc.file_format = "PNG"; img_bc.save()
    img_n.filepath_raw = os.path.join(OUT, NAME + "_" + cat + "_N.png"); img_n.file_format = "PNG"; img_n.save()
    info["cats"][cat] = {"tris": len(me.polygons), "area": round(area, 1), "atlas": size}
    log("baked", cat, info["cats"][cat])

bpy.ops.object.select_all(action="DESELECT")
for ob in lows.values(): ob.select_set(True)
bpy.context.view_layer.objects.active = list(lows.values())[0]
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True,
    axis_forward="-Z", axis_up="Y", bake_space_transform=True, apply_unit_scale=True,
    mesh_smooth_type="FACE", use_mesh_modifiers=False, add_leaf_bones=False, embed_textures=False, path_mode="STRIP")
info["low_tris"] = sum(v["tris"] for v in info["cats"].values()); info["seconds"] = round(time.time() - T0, 1)
with open(os.path.join(OUT, NAME + "_info.json"), "w") as f: json.dump(info, f)
log("DONE", info)
