# Background Blender script: OBJ(high) roof parts -> smooth envelope -> bake BC+N -> <name>_roofenv.fbx
# usage: blender -b --factory-startup -P bake_roofenv.py -- <obj> <outdir> <name> <atlas_max> <voxel>
# 기와 골을 메시로 남기면 멀리서 줄무늬(모아레)가 생겨서, 지붕을 매끈한 껍데기로 만들고 기와는 텍스처·노멀맵으로만 남긴다.
import bpy, bmesh, sys, os, math, time, json

argv = sys.argv[sys.argv.index("--") + 1:]
OBJ, OUT, NAME = argv[0], argv[1], argv[2]
ATLAS = int(argv[3]); VOX = float(argv[4]) if len(argv) > 4 else 0.3
os.makedirs(OUT, exist_ok=True)
LOG = os.path.join(OUT, NAME + "_roofenv_log.txt")
T0 = time.time()
def log(*a):
    s = "[%7.1fs] " % (time.time() - T0) + " ".join(str(x) for x in a)
    print(s, flush=True)
    with open(LOG, "a", encoding="utf-8") as f: f.write(s + "\n")
open(LOG, "w").close()
log("start", OBJ, "voxel", VOX)

try:
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for dt in ("OPTIX", "CUDA", "HIP", "ONEAPI", "METAL"):
        try:
            prefs.compute_device_type = dt; prefs.get_devices()
            devs = [d for d in prefs.devices if d.type == dt]
            if devs:
                for d in prefs.devices: d.use = (d.type == dt)
                log("GPU", dt); break
        except Exception: pass
except Exception as e:
    log("gpu setup failed", e)
scn = bpy.context.scene
scn.render.engine = "CYCLES"
try: scn.cycles.device = "GPU"
except: pass

for _o in list(bpy.data.objects): bpy.data.objects.remove(_o, do_unlink=True)
bpy.ops.wm.obj_import(filepath=OBJ, forward_axis="NEGATIVE_Z", up_axis="Y")
allobj = [o for o in scn.objects if o.type == "MESH"]

def category(name):
    n = name.lower()
    if any(k in n for k in ("rafter", "rooffloor", "roofinner", "roofwall", "bridge")): return "other"   # bridge: "ridge" 가 들어 있어 영제교가 지붕으로 잡혔음
    if "roof" in n or any(k in n for k in ("kiwa", "giwa", "roofplate", "yongmeoli", "chimi", "japsang", "ridge", "mangwa", "sumaksae")): return "roof"
    return "other"
roof = [o for o in allobj if category(o.name) == "roof"]
log("objects", len(allobj), "roof", len(roof), "roof tris", sum(len(o.data.polygons) for o in roof))
if not roof:
    with open(os.path.join(OUT, NAME + "_roofenv_info.json"), "w") as f: json.dump({"name": NAME, "roof": 0}, f)
    log("DONE no roof"); sys.exit(0)
for o in allobj:
    if o not in roof: bpy.data.objects.remove(o, do_unlink=True)

# MTL 로 재질 다시 만들기(bake_building.py 와 같음)
mtl = {}; cur = None
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
used = set()
for o in roof:
    for s in o.material_slots:
        if s.material: used.add(s.material.name)
for m in bpy.data.materials:
    if m.name not in used: continue
    info = mtl.get(m.name)
    if info is None: continue
    m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    outn = nt.nodes.new("ShaderNodeOutputMaterial"); bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], outn.inputs[0])
    kd = info.get("kd", [1, 1, 1])
    if "bc" in info and getimg(info["bc"], False):
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = getimg(info["bc"], False)
        if kd != [1, 1, 1]:
            mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"; mix.inputs[0].default_value = 1.0
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

bpy.ops.object.select_all(action="DESELECT")
for o in roof: o.select_set(True)
bpy.context.view_layer.objects.active = roof[0]
bpy.ops.object.join()
H = bpy.context.view_layer.objects.active; H.name = "HIGH_" + NAME
log("high joined", len(H.data.polygons))

# ---------- 껍데기: 복셀 리메시 → 다듬기 → 줄이기 ----------
me = H.data.copy()
# bake_building.py 처럼 월드 변환을 정점에 굽고 오브젝트는 원점·무회전(OBJ 가져오기의 축 회전이 오브젝트에 남아 있음)
me.transform(H.matrix_world)
env = bpy.data.objects.new(NAME + "_roofenv", me); scn.collection.objects.link(env)
while env.data.materials: env.data.materials.pop()
bpy.ops.object.select_all(action="DESELECT"); env.select_set(True); bpy.context.view_layer.objects.active = env
# 한 겹짜리 얇은 기와 메시(경회루·근정전 등)는 복셀보다 얇아 리메시에 구멍이 나므로 먼저 두께를 준다
so = env.modifiers.new("solid", "SOLIDIFY"); so.thickness = VOX * 1.3; so.offset = 0.0; so.use_even_offset = False
bpy.ops.object.modifier_apply(modifier=so.name)
rm = env.modifiers.new("remesh", "REMESH"); rm.mode = "VOXEL"; rm.voxel_size = VOX; rm.adaptivity = 0.0
bpy.ops.object.modifier_apply(modifier=rm.name)
log("remeshed", len(env.data.polygons))
sm = env.modifiers.new("smooth", "SMOOTH"); sm.factor = 0.7; sm.iterations = 12
bpy.ops.object.modifier_apply(modifier=sm.name)
area = sum(p.area for p in env.data.polygons)
target = int(min(250000, max(3000, area / 0.25)))
nt0 = len(env.data.polygons) * 2
if nt0 > target:
    dm = env.modifiers.new("dec", "DECIMATE"); dm.decimate_type = "COLLAPSE"; dm.ratio = max(0.01, target / nt0); dm.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=dm.name)
for p in env.data.polygons: p.use_smooth = True
log("env tris", len(env.data.polygons), "area", round(area, 1), "target", target)

# ---------- UV·굽기 ----------
DENS = 56.0
px = math.sqrt(area * 0.5 * 1.7) * DENS      # 껍데기는 위·아래 두 겹이라 넓이 절반 기준
size = 512
while size < px and size < ATLAS: size *= 2
uv = env.data.uv_layers.new(name="Atlas"); env.data.uv_layers.active = uv
# UV: 위를 보는 면은 위에서, 아래를 보는 면은 아래에서 평면 투사(두 덩어리). smart_project 는 리메시 면의 법선이 들쭉날쭉해
# 섬이 수천 조각으로 갈라져(경회루) 밉맵에서 얼룩이 생겼음
marg = 3.0 / size
bm = bmesh.new(); bm.from_mesh(env.data); uvl = bm.loops.layers.uv.verify()
xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]
x0, y0 = min(xs), min(ys); spanx = max(xs) - x0 + 1.0
# 행각처럼 ㅁ자 지붕은 통째로 투사하면 가운데가 비어 텍스처가 낭비되므로 8 m 칸마다 따로 떼어(섬) 빽빽하게 채운다
CELL = 8.0
keys = {}
for f in bm.faces:
    top = f.normal.z >= 0
    c = f.calc_center_median()
    k = (int((c.x - x0) // CELL), int((c.y - y0) // CELL), top)
    idx = keys.setdefault(k, len(keys))
    for lp in f.loops:
        co = lp.vert.co
        lp[uvl].uv = ((co.x - x0) + idx * 1000.0, co.y - y0)
bm.to_mesh(env.data); bm.free()
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.select_all(action="SELECT")
try: bpy.ops.uv.pack_islands(margin=marg, rotate=True)
except Exception as e: log("pack failed", e)
bpy.ops.object.mode_set(mode="OBJECT")
img_bc = bpy.data.images.new(NAME + "_roofenv_BC", size, size, alpha=False)
img_n = bpy.data.images.new(NAME + "_roofenv_N", size, size, alpha=False); img_n.colorspace_settings.name = "Non-Color"
mat = bpy.data.materials.new("MAT_" + NAME + "_roofenv"); mat.use_nodes = True
tex = mat.node_tree.nodes.new("ShaderNodeTexImage"); mat.node_tree.nodes.active = tex
env.data.materials.append(mat)
bk = scn.render.bake
bk.use_selected_to_active = True
bk.cage_extrusion = VOX + 0.25
bk.max_ray_distance = VOX * 2 + 0.6
bk.margin = 8
scn.cycles.samples = 16          # 텍셀 안에서 여러 번 쏴서 기와 골이 계단·얼룩으로 구워지지 않게
bpy.ops.object.select_all(action="DESELECT")
H.select_set(True); env.select_set(True); bpy.context.view_layer.objects.active = env
tex.image = img_bc
bk.use_pass_direct = False; bk.use_pass_indirect = False; bk.use_pass_color = True
bpy.ops.object.bake(type="DIFFUSE")
tex.image = img_n; bk.normal_space = "TANGENT"
bpy.ops.object.bake(type="NORMAL")
# 품질 검사(BVH): ① 껍데기 면에서 안쪽으로 쏜 레이가 기와에 닿는 비율 ② 기와 면에서 앞·뒤로 쏜 레이가 껍데기에 닿는 비율.
# 얇은 한 겹 기와 메시(경회루 SM_KHL_Giwa 등)는 복셀 리메시가 군데군데 비어 껍데기에 구멍이 났음 → 둘 중 하나라도 90 % 밑이면 bad(기존 지붕을 씀)
import random
from mathutils.bvhtree import BVHTree
random.seed(1)
Mw = H.matrix_world
hv = [Mw @ v.co for v in H.data.vertices]; hp = [tuple(p.vertices) for p in H.data.polygons]
bvhH = BVHTree.FromPolygons(hv, hp)
ev = [v.co.copy() for v in env.data.vertices]; ep = [tuple(p.vertices) for p in env.data.polygons]
bvhE = BVHTree.FromPolygons(ev, ep)
def area_sample(polys, n):
    ar = [p.area for p in polys]; tot = sum(ar)
    return random.choices(range(len(polys)), weights=ar, k=n) if tot > 0 else []
hit1 = 0; ids = area_sample(env.data.polygons, 3000)
for i in ids:
    p = env.data.polygons[i]; c = p.center; nrm = p.normal
    o = c + nrm * (VOX + 0.25)
    if bvhH.ray_cast(o, -nrm, VOX * 2 + 0.6)[0] is not None: hit1 += 1
r1 = hit1 / max(1, len(ids))
hit2 = 0; ids2 = area_sample(H.data.polygons, 3000); N3 = Mw.to_3x3()
for i in ids2:
    p = H.data.polygons[i]; c = Mw @ p.center; nrm = (N3 @ p.normal).normalized()
    if bvhE.ray_cast(c, nrm, 1.0)[0] is not None or bvhE.ray_cast(c, -nrm, 1.0)[0] is not None: hit2 += 1
r2 = hit2 / max(1, len(ids2))
miss_ratio = 1.0 - min(r1, r2); black_all = 0.0
log("coverage env->high", round(r1, 3), "high->env", round(r2, 3))
img_bc.filepath_raw = os.path.join(OUT, NAME + "_roofenv_BC.png"); img_bc.file_format = "PNG"; img_bc.save()
img_n.filepath_raw = os.path.join(OUT, NAME + "_roofenv_N.png"); img_n.file_format = "PNG"; img_n.save()
log("baked", size)

bpy.ops.object.select_all(action="DESELECT"); env.select_set(True); bpy.context.view_layer.objects.active = env
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + "_roofenv.fbx"), use_selection=True,
    axis_forward="-Z", axis_up="Y", bake_space_transform=True, apply_unit_scale=True,
    mesh_smooth_type="OFF", use_mesh_modifiers=False, add_leaf_bones=False, embed_textures=False, path_mode="STRIP")
# ---------- 먼 거리용 평면 지붕: 껍데기의 윗면만 남겨 크게 줄임(같은 UV·BC 를 씀, 노멀맵 없음) ----------
flat = bpy.data.objects.new(NAME + "_roofflat", env.data.copy()); scn.collection.objects.link(flat)
bm = bmesh.new(); bm.from_mesh(flat.data)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.z < -0.2], context="FACES")
bm.to_mesh(flat.data); bm.free()
top_area = sum(p.area for p in flat.data.polygons)
ftarget = int(max(150, top_area / 6.0))
if len(flat.data.polygons) > ftarget:
    bpy.ops.object.select_all(action="DESELECT"); flat.select_set(True); bpy.context.view_layer.objects.active = flat
    dm = flat.modifiers.new("dec", "DECIMATE"); dm.decimate_type = "COLLAPSE"; dm.ratio = max(0.002, ftarget / max(1, len(flat.data.polygons))); dm.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=dm.name)
for p in flat.data.polygons: p.use_smooth = True
bpy.ops.object.select_all(action="DESELECT"); flat.select_set(True); bpy.context.view_layer.objects.active = flat
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + "_roofflat.fbx"), use_selection=True,
    axis_forward="-Z", axis_up="Y", bake_space_transform=True, apply_unit_scale=True,
    mesh_smooth_type="OFF", use_mesh_modifiers=False, add_leaf_bones=False, embed_textures=False, path_mode="STRIP")
log("flat tris", len(flat.data.polygons), "top area", round(top_area, 1))
info = {"name": NAME, "roof": 1, "flat_tris": len(flat.data.polygons), "cov_env": round(r1, 3), "cov_high": round(r2, 3), "bad": min(r1, r2) < 0.9, "high_tris": len(H.data.polygons), "env_tris": len(env.data.polygons), "atlas": size, "seconds": round(time.time() - T0, 1)}
with open(os.path.join(OUT, NAME + "_roofenv_info.json"), "w") as f: json.dump(info, f)
log("DONE", info)
