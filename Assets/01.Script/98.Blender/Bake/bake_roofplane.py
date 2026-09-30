
# Background Blender: 기와(high) -> 윗면만 남긴 단순한 면 + 노멀맵만 굽기(색은 기와 텍스처 평균 한 색)
# usage: blender -b --factory-startup -P bake_roofplane.py -- <obj> <outdir> <name> <atlas> <voxel> <m2_per_tri>
import bpy, bmesh, sys, os, math, time, json
argv = sys.argv[sys.argv.index("--") + 1:]
OBJ, OUT, NAME = argv[0], argv[1], argv[2]
ATLAS = int(argv[3]); VOX = float(argv[4]); M2 = float(argv[5])
os.makedirs(OUT, exist_ok=True)
LOG = os.path.join(OUT, NAME + "_log.txt"); open(LOG, "w").close(); T0 = time.time()
def log(*a):
    s = "[%7.1fs] " % (time.time() - T0) + " ".join(str(x) for x in a)
    print(s, flush=True)
    with open(LOG, "a", encoding="utf-8") as f: f.write(s + "\n")
try:
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for dt in ("OPTIX", "CUDA", "HIP", "ONEAPI"):
        try:
            prefs.compute_device_type = dt; prefs.get_devices()
            if [d for d in prefs.devices if d.type == dt]:
                for d in prefs.devices: d.use = (d.type == dt)
                log("GPU", dt); break
        except Exception: pass
except Exception as e: log("gpu fail", e)
scn = bpy.context.scene; scn.render.engine = "CYCLES"
try: scn.cycles.device = "GPU"
except Exception: pass
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.wm.obj_import(filepath=OBJ, forward_axis="NEGATIVE_Z", up_axis="Y")
hs = [o for o in scn.objects if o.type == "MESH"]
bpy.ops.object.select_all(action="DESELECT")
for o in hs: o.select_set(True)
bpy.context.view_layer.objects.active = hs[0]
if len(hs) > 1: bpy.ops.object.join()
H = bpy.context.view_layer.objects.active
# 기와 평균 색(텍스처 한 장 평균, 선형)
col = [0.08, 0.09, 0.1]
try:
    for m in H.data.materials:
        for n in m.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image:
                px = n.image.pixels[:]; k = len(px) // 4; step = max(1, k // 200000)
                s = [0.0, 0.0, 0.0]; c = 0
                for i in range(0, k, step):
                    s[0] += px[i*4]; s[1] += px[i*4+1]; s[2] += px[i*4+2]; c += 1
                col = [x / c for x in s]; raise StopIteration
except StopIteration: pass
except Exception as e: log("avg fail", e)
log("high", len(H.data.polygons), "avg color", [round(x, 4) for x in col])
me = H.data.copy(); me.transform(H.matrix_world)
P = bpy.data.objects.new(NAME, me); scn.collection.objects.link(P)
while P.data.materials: P.data.materials.pop()
bpy.ops.object.select_all(action="DESELECT"); P.select_set(True); bpy.context.view_layer.objects.active = P
so = P.modifiers.new("solid", "SOLIDIFY"); so.thickness = VOX * 1.3; so.offset = 0.0
bpy.ops.object.modifier_apply(modifier=so.name)
rm = P.modifiers.new("remesh", "REMESH"); rm.mode = "VOXEL"; rm.voxel_size = VOX; rm.adaptivity = 0.0
bpy.ops.object.modifier_apply(modifier=rm.name)
sm = P.modifiers.new("smooth", "SMOOTH"); sm.factor = 0.7; sm.iterations = 12
bpy.ops.object.modifier_apply(modifier=sm.name)
# 윗면만 남김(아래를 보는 면 삭제) — 기와 겉면 한 장
bm = bmesh.new(); bm.from_mesh(P.data)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.z < -0.2], context="FACES")
bm.to_mesh(P.data); bm.free()
area = sum(p.area for p in P.data.polygons)
target = int(max(200, area / M2))
n0 = len(P.data.polygons)
if n0 > target:
    dm = P.modifiers.new("dec", "DECIMATE"); dm.decimate_type = "COLLAPSE"; dm.ratio = max(0.001, target / n0); dm.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=dm.name)
for p in P.data.polygons: p.use_smooth = True
log("plane tris", len(P.data.polygons), "area", round(area, 1), "target", target)
# UV: 위에서 평면 투사, 8 m 칸마다 섬
bm = bmesh.new(); bm.from_mesh(P.data); uvl = bm.loops.layers.uv.verify()
xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]; x0, y0 = min(xs), min(ys)
keys = {}
for f in bm.faces:
    c = f.calc_center_median(); k = (int((c.x - x0) // 8.0), int((c.y - y0) // 8.0))
    idx = keys.setdefault(k, len(keys))
    for lp in f.loops: lp[uvl].uv = ((lp.vert.co.x - x0) + idx * 1000.0, lp.vert.co.y - y0)
bm.to_mesh(P.data); bm.free()
size = ATLAS
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.uv.select_all(action="SELECT")
try: bpy.ops.uv.pack_islands(margin=3.0 / size, rotate=True)
except Exception as e: log("pack fail", e)
bpy.ops.object.mode_set(mode="OBJECT")
img_n = bpy.data.images.new(NAME + "_N", size, size, alpha=False); img_n.colorspace_settings.name = "Non-Color"
mat = bpy.data.materials.new("MAT_" + NAME); mat.use_nodes = True
tex = mat.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = img_n; mat.node_tree.nodes.active = tex
P.data.materials.append(mat)
bk = scn.render.bake; bk.use_selected_to_active = True
bk.cage_extrusion = VOX + 0.25; bk.max_ray_distance = VOX * 2 + 0.6; bk.margin = 8; bk.normal_space = "TANGENT"
scn.cycles.samples = 16
bpy.ops.object.select_all(action="DESELECT"); H.select_set(True); P.select_set(True); bpy.context.view_layer.objects.active = P
bpy.ops.object.bake(type="NORMAL")
img_n.filepath_raw = os.path.join(OUT, NAME + "_N.png"); img_n.file_format = "PNG"; img_n.save()
bpy.ops.object.select_all(action="DESELECT"); P.select_set(True); bpy.context.view_layer.objects.active = P
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, axis_forward="-Z", axis_up="Y",
    bake_space_transform=True, apply_unit_scale=True, mesh_smooth_type="OFF", use_mesh_modifiers=False, add_leaf_bones=False, embed_textures=False, path_mode="STRIP")
info = {"name": NAME, "tris": len(P.data.polygons), "area": round(area, 1), "atlas": size, "color": col, "seconds": round(time.time() - T0, 1)}
with open(os.path.join(OUT, NAME + "_info.json"), "w") as f: json.dump(info, f)
log("DONE", info)
