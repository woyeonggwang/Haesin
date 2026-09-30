
import bpy, bmesh, sys, os, json
argv=sys.argv[sys.argv.index("--")+1:]
SRC, OUT, PFX = argv[0], argv[1], argv[2]; CH=argv[3].split(",")
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
def imp(p):
    before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=p)
    new=[o for o in bpy.data.objects if o not in before and o.type=="MESH"]
    for o in new:
        o.data.transform(o.matrix_world); o.matrix_world.identity(); o.parent=None
    return new
def export(objs,path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    bpy.ops.wm.obj_export(filepath=path, forward_axis="NEGATIVE_Z", up_axis="Y", export_selected_objects=True, export_materials=True, path_mode="ABSOLUTE", export_normals=True, export_uv=True, export_triangulated_mesh=True)
def decimate(o,ratio):
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active=o
    d=o.modifiers.new("d","DECIMATE"); d.decimate_type="COLLAPSE"; d.ratio=ratio; d.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=d.name)
info={}; plates=[]; bodies=[]
for ch in CH:
    envp=os.path.join(SRC,ch+"_roofenv.fbx")
    if os.path.exists(envp):
        env=imp(envp)[0]
        bm=bmesh.new(); bm.from_mesh(env.data)
        bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.normal.z<-0.2],context="FACES"); bm.to_mesh(env.data); bm.free()
        top=sum(p.area for p in env.data.polygons); t=max(100,int(top/6))
        decimate(env,min(1.0,t/max(1,len(env.data.polygons)))); plates.append(env)
    for o in imp(os.path.join(SRC,ch+".fbx")):
        if "roof" in o.name.lower(): bpy.data.objects.remove(o, do_unlink=True); continue
        decimate(o,0.04); bodies.append(o)
info["plate_tris"]=sum(len(o.data.polygons) for o in plates); info["body_tris"]=sum(len(o.data.polygons) for o in bodies)
export(plates,os.path.join(OUT,PFX+"_LOD2_RoofPlate.obj")); export(bodies,os.path.join(OUT,PFX+"_LOD2_Body.obj"))
print("DONE",info)
