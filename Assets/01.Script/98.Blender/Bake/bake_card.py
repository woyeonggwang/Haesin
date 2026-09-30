# Background Blender script: 창·문 메시(OBJ) -> 앞·뒤 두 장의 판에 창살+창호지를 굽는다 -> <key>_card.fbx + _card_BC/_N.png
# usage: blender -b --factory-startup -P bake_card.py -- <obj> <outdir> <key> <px_per_m> <max_px>
# 창살을 막대 메시로 두면 멀리서 지글거려서(모아레) 판 + 텍스처로 바꾼다. 빈 곳은 가운데에 둔 창호지 판이 채운다.
import bpy, bmesh, sys, os, math, time, json
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
OBJ, OUT, KEY = argv[0], argv[1], argv[2]
DENS = float(argv[3]) if len(argv) > 3 else 400.0
MAXPX = int(argv[4]) if len(argv) > 4 else 2048
os.makedirs(OUT, exist_ok=True)
LOG = os.path.join(OUT, KEY + "_card_log.txt")
T0 = time.time()
def log(*a):
    s = "[%6.1fs] " % (time.time() - T0) + " ".join(str(x) for x in a)
    print(s, flush=True)
    with open(LOG, "a", encoding="utf-8") as f: f.write(s + "\n")
open(LOG, "w").close()

try:
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for dt in ("OPTIX", "CUDA", "HIP", "ONEAPI", "METAL"):
        try:
            prefs.compute_device_type = dt; prefs.get_devices()
            devs = [d for d in prefs.devices if d.type == dt]
            if devs:
                for d in prefs.devices: d.use = (d.type == dt)
                break
        except Exception: pass
except Exception: pass
scn = bpy.context.scene
scn.render.engine = "CYCLES"
try: scn.cycles.device = "GPU"
except: pass

for _o in list(bpy.data.objects): bpy.data.objects.remove(_o, do_unlink=True)
bpy.ops.wm.obj_import(filepath=OBJ, forward_axis="NEGATIVE_Z", up_axis="Y")
high = [o for o in scn.objects if o.type == "MESH"]

mtl = {}; cur = None
with open(os.path.splitext(OBJ)[0] + ".mtl", encoding="utf-8") as f:
    for line in f:
        line = line.strip()
        if line.startswith("newmtl "): cur = line[7:].strip(); mtl[cur] = {}
        elif cur and line.startswith("map_Kd "): mtl[cur]["bc"] = line[7:].strip()
        elif cur and line.startswith("norm "): mtl[cur]["n"] = line[5:].strip()
        elif cur and line.startswith("Kd "): mtl[cur]["kd"] = [float(x) for x in line[3:].split()]
def getimg(p, noncolor):
    try:
        im = bpy.data.images.load(p, check_existing=True)
        if noncolor: im.colorspace_settings.name = "Non-Color"
        return im
    except Exception as e:
        log("img fail", p, e); return None
for m in bpy.data.materials:
    info = mtl.get(m.name)
    if info is None: continue
    m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    outn = nt.nodes.new("ShaderNodeOutputMaterial"); bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], outn.inputs[0])
    kd = info.get("kd", [1, 1, 1])
    im = getimg(info["bc"], False) if "bc" in info else None
    if im:
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = im
        if kd != [1, 1, 1]:
            mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"; mix.inputs[0].default_value = 1.0
            nt.links.new(t.outputs[0], mix.inputs[6]); mix.inputs[7].default_value = (kd[0], kd[1], kd[2], 1)
            nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
        else:
            nt.links.new(t.outputs[0], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (kd[0], kd[1], kd[2], 1)
    imn = getimg(info["n"], True) if "n" in info else None
    if imn:
        tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = imn
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(tn.outputs[0], nm.inputs["Color"]); nt.links.new(nm.outputs[0], bsdf.inputs["Normal"])

bpy.ops.object.select_all(action="DESELECT")
for o in high: o.select_set(True)
bpy.context.view_layer.objects.active = high[0]
if len(high) > 1: bpy.ops.object.join()
H = bpy.context.view_layer.objects.active
H.data.transform(H.matrix_world); H.matrix_world.identity()
vs = [v.co for v in H.data.vertices]
mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
size = mx - mn
ax = min(range(3), key=lambda i: size[i]); a1 = (ax + 1) % 3; a2 = (ax + 2) % 3
log("tris", len(H.data.polygons), "size", tuple(round(x, 3) for x in size), "axis", ax)

# 가운데 창호지 판(빈 곳을 채움) — 창호지 색은 한지 텍스처 평균(sRGB 0.86)
paper = bpy.data.materials.new("HANJI"); paper.use_nodes = True
pb = paper.node_tree.nodes.get("Principled BSDF"); pb.inputs["Base Color"].default_value = (0.70, 0.68, 0.66, 1)
def quad(name, plane, sign, u0, u1):
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UV")
    cs = []
    for (s1, s2) in ((0, 0), (1, 0), (1, 1), (0, 1)):
        p = Vector((0, 0, 0)); p[ax] = plane
        p[a1] = mn[a1] + size[a1] * s1; p[a2] = mn[a2] + size[a2] * s2
        cs.append((bm.verts.new(p), s1, s2))
    f = bm.faces.new([c[0] for c in cs])
    for lp, c in zip(f.loops, cs):
        lp[uvl].uv = (u0 + (u1 - u0) * c[1], c[2])
    bm.normal_update()
    if (f.normal[ax] > 0) != (sign > 0):
        f.normal_flip()
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return me
# 판: 앞면(+축, UV 왼쪽 절반) + 뒷면(−축, 오른쪽 절반)
eps = 0.002
bm = bmesh.new()
for part in (quad("f", mx[ax] + eps, 1, 0.0, 0.5), quad("b", mn[ax] - eps, -1, 0.5, 1.0)):
    bm.from_mesh(part); bpy.data.meshes.remove(part)
card_me = bpy.data.meshes.new(KEY + "_card"); bm.to_mesh(card_me); bm.free()
card = bpy.data.objects.new(KEY + "_card", card_me); scn.collection.objects.link(card)

def p2(x):
    s = 32
    while s < x and s < MAXPX: s *= 2
    return s
W = p2(2 * size[a1] * DENS); Hh = p2(size[a2] * DENS)
img_bc = bpy.data.images.new(KEY + "_card_BC", W, Hh, alpha=False)
img_n = bpy.data.images.new(KEY + "_card_N", W, Hh, alpha=False); img_n.colorspace_settings.name = "Non-Color"
mat = bpy.data.materials.new("MAT_" + KEY + "_card"); mat.use_nodes = True
tex = mat.node_tree.nodes.new("ShaderNodeTexImage"); mat.node_tree.nodes.active = tex
card_me.materials.append(mat)
bk = scn.render.bake
bk.use_selected_to_active = True
bk.cage_extrusion = 0.01
bk.max_ray_distance = size[ax] + 0.03
bk.margin = 4
scn.cycles.samples = 16
bpy.ops.object.select_all(action="DESELECT")
H.select_set(True); card.select_set(True); bpy.context.view_layer.objects.active = card
tex.image = img_bc
bk.use_pass_direct = False; bk.use_pass_indirect = False; bk.use_pass_color = True
bpy.ops.object.bake(type="DIFFUSE")
tex.image = img_n; bk.normal_space = "TANGENT"
bpy.ops.object.bake(type="NORMAL")
# 가림(맞았는지) 굽기: 부재 전체를 흰 발광으로 바꿔 EMIT → 1 = 부재, 0 = 빈 곳
img_cov = bpy.data.images.new(KEY + "_cov", W, Hh, alpha=False); img_cov.colorspace_settings.name = "Non-Color"
em = bpy.data.materials.new("EMITW"); em.use_nodes = True; ent = em.node_tree; ent.nodes.clear()
eo = ent.nodes.new("ShaderNodeOutputMaterial"); ee = ent.nodes.new("ShaderNodeEmission"); ee.inputs[1].default_value = 1.0
ent.links.new(ee.outputs[0], eo.inputs[0])
for i in range(len(H.material_slots)): H.material_slots[i].material = em
if len(H.material_slots) == 0: H.data.materials.append(em)
tex.image = img_cov
bpy.ops.object.bake(type="EMIT")

# 빈 곳 처리(면마다): 가장자리와 이어진 빈 곳 = 창 밖(투명), 창살 사이의 빈 곳 = 창호지(한지 색·평평한 노멀)
import numpy as np
from collections import deque
bc = np.array(img_bc.pixels[:], dtype=np.float32).reshape(Hh, W, 4)
nn = np.array(img_n.pixels[:], dtype=np.float32).reshape(Hh, W, 4)
cov = np.array(img_cov.pixels[:], dtype=np.float32).reshape(Hh, W, 4)[:, :, 0] > 0.5
PAPER = np.array([0.86, 0.85, 0.84], dtype=np.float32)
alpha = np.ones((Hh, W), dtype=np.float32)
cut = False
for c0, c1 in ((0, W // 2), (W // 2, W)):
    sub = ~cov[:, c0:c1]; h, w = sub.shape
    outside = np.zeros_like(sub)
    dq = deque()
    for y in range(h):
        for x in (0, w - 1):
            if sub[y, x] and not outside[y, x]: outside[y, x] = True; dq.append((y, x))
    for x in range(w):
        for y in (0, h - 1):
            if sub[y, x] and not outside[y, x]: outside[y, x] = True; dq.append((y, x))
    while dq:
        y, x = dq.popleft()
        for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= yy < h and 0 <= xx < w and sub[yy, xx] and not outside[yy, xx]:
                outside[yy, xx] = True; dq.append((yy, xx))
    holes = sub & ~outside
    reg = bc[:, c0:c1, :3]; reg[holes] = PAPER; reg[outside] = PAPER
    nreg = nn[:, c0:c1, :3]; nreg[holes] = (0.5, 0.5, 1.0); nreg[outside] = (0.5, 0.5, 1.0)
    # 판 크기에 비해 아주 작은 가장자리 틈은 막고, 큰 바깥(팔각창 모서리 등)만 투명하게
    if outside.sum() > 0.002 * h * w:
        alpha[:, c0:c1][outside] = 0.0; cut = True
bc[:, :, 3] = alpha
img_bc_a = bpy.data.images.new(KEY + "_card_BCA", W, Hh, alpha=True)
img_bc_a.pixels[:] = bc.ravel()
img_n.pixels[:] = nn.ravel()
img_bc = img_bc_a
img_bc.filepath_raw = os.path.join(OUT, KEY + "_card_BC.png"); img_bc.file_format = "PNG"; img_bc.save()
img_n.filepath_raw = os.path.join(OUT, KEY + "_card_N.png"); img_n.file_format = "PNG"; img_n.save()
bpy.ops.object.select_all(action="DESELECT"); card.select_set(True); bpy.context.view_layer.objects.active = card
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, KEY + "_card.fbx"), use_selection=True,
    axis_forward="-Z", axis_up="Y", bake_space_transform=True, apply_unit_scale=True,
    mesh_smooth_type="OFF", use_mesh_modifiers=False, add_leaf_bones=False, embed_textures=False, path_mode="STRIP")
info = {"key": KEY, "cutout": cut, "tris": len(H.data.polygons), "size": [round(x, 3) for x in size], "px": [W, Hh], "seconds": round(time.time() - T0, 1)}
with open(os.path.join(OUT, KEY + "_card_info.json"), "w") as f: json.dump(info, f)
log("DONE", info)
