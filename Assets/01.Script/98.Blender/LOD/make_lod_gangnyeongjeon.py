
import bpy, bmesh, sys, os, json
argv=sys.argv[sys.argv.index("--")+1:]
SRC, OUT = argv[0], argv[1]
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
def imp(p):
    before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=p)
    new=[o for o in bpy.data.objects if o not in before and o.type=="MESH"]
    for o in new:
        o.data.transform(o.matrix_world); o.matrix_world.identity(); o.parent=None
    return new
def bb(objs):
    xs=[];ys=[];zs=[]
    for o in objs:
        for v in o.data.vertices: xs.append(v.co.x); ys.append(v.co.y); zs.append(v.co.z)
    return [round(min(xs),2),round(max(xs),2),round(min(ys),2),round(max(ys),2),round(min(zs),2),round(max(zs),2)]
def export(objs,path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    bpy.ops.wm.obj_export(filepath=path, forward_axis="NEGATIVE_Z", up_axis="Y", export_selected_objects=True, export_materials=True, path_mode="ABSOLUTE", export_normals=True, export_uv=True, export_triangulated_mesh=True)
def decimate(o,ratio):
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active=o
    d=o.modifiers.new("d","DECIMATE"); d.decimate_type="COLLAPSE"; d.ratio=ratio; d.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=d.name)
info={}
env=imp(os.path.join(SRC,"GNJa_roofenv.fbx"))[0]
info["env_bb"]=bb([env])
bm=bmesh.new(); bm.from_mesh(env.data)
bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.normal.z<-0.2],context="FACES"); bm.to_mesh(env.data); bm.free()
top=sum(p.area for p in env.data.polygons)
info["top_tris"]=len(env.data.polygons)
flat=env; flat.name="GNJ_RoofFlat"
# LOD1 평면: 윗면만(구운 BC 텍스처 그대로 씀)
export([flat],os.path.join(OUT,"GNJ_LOD1_Roof.obj"))
# LOD2 판: 더 줄인 윗면(검은색)
p2=bpy.data.objects.new("GNJ_RoofPlate",flat.data.copy()); bpy.context.scene.collection.objects.link(p2)
t=max(150,int(top/6)); decimate(p2,min(1.0,t/max(1,len(p2.data.polygons))))
info["plate_tris"]=len(p2.data.polygons)
export([p2],os.path.join(OUT,"GNJ_LOD2_RoofPlate.obj"))
body=imp(os.path.join(SRC,"GNJa.fbx"))
info["body_names"]=[o.name for o in body]; info["body_bb"]=bb(body)
b2=[o for o in body if "roof" not in o.name.lower()]
for o in b2: decimate(o,0.12)
info["lod2_body_tris"]=sum(len(o.data.polygons) for o in b2)
export(b2,os.path.join(OUT,"GNJ_LOD2_Body.obj"))
json.dump(info,open(os.path.join(OUT,"lod_info.json"),"w"))
print("DONE",info)
