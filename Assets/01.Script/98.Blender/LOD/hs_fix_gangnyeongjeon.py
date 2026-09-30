
import bpy, bmesh, builtins, math, re, time
from mathutils import Vector
from mathutils.bvhtree import BVHTree
def is_tile(n):
    l=(n or "").lower(); return ("roof" in l or "giwa" in l) and "ornament" not in l and "rafter" not in l and "loof" not in l
def base(n): return re.sub(r"\.\d{3}$","",n)
def load_obj(code):
    V=[];VT=[];faces=[];mat=None
    with open(r"D:\UnityAssetCache\_edit\Gangnyeongjeon_%s.obj"%code,encoding="utf-8",errors="ignore") as fh:
        for line in fh:
            c=line[:2]
            if c=="v ": a=line.split(); V.append(Vector((float(a[1])-48.0,-float(a[3]),float(a[2]))))
            elif c=="vt": a=line.split(); VT.append((float(a[1]),float(a[2])))
            elif c=="us": mat=line.split()[1]
            elif c=="f ": faces.append((mat,[tuple(int(x) if x else 0 for x in p.split("/")) for p in line.split()[1:]]))
    D=dict(V=V,VT=VT,faces=faces,bvh=BVHTree.FromPolygons(V,[tuple(i[0]-1 for i in f) for m,f in faces]))
    D['otile']=[is_tile(m) for m,f in faces]
    xs=[v.x for v in V]; ys=[v.y for v in V]; zs=[v.z for v in V]
    D['bb']=(min(xs),max(xs),min(ys),max(ys),min(zs),max(zs))
    D['ctr']=Vector(((D['bb'][0]+D['bb'][1])/2,(D['bb'][2]+D['bb'][3])/2,(D['bb'][4]+D['bb'][5])/2))
    D['R']=Vector((D['bb'][1]-D['bb'][0],D['bb'][3]-D['bb'][2],D['bb'][5]-D['bb'][4])).length/2+1
    return D
def post_bvh(code):
    O=bpy.data.objects["GNJC_"+code]; me=O.data; M=O.matrix_world
    b=BVHTree.FromPolygons([M@v.co for v in me.vertices],[tuple(p.vertices) for p in me.polygons])
    pt=[is_tile(me.materials[p.material_index].name if me.materials[p.material_index] else "") for p in me.polygons]
    return b,pt
def judge(a,b,otile,ptile,restore,kill):
    if a[0] is None and b[0] is None: return
    if a[0] is not None and b[0] is None: restore.add(a[2]); return
    if a[0] is None: kill.add(b[2]); return
    at=otile[a[2]]; bt=ptile[b[2]]
    tol=0.05 if (at and not bt) else (0.3 if (at or bt) else 0.08)
    if b[3]>a[3]+tol: restore.add(a[2])
    elif b[3]<a[3]-tol: kill.add(b[2])
def dirs_all():
    ds=[Vector((0,0,-1))]
    for a in range(0,360,22):
        for el in (-60,-30,-10,0,12):
            r=math.radians(a); e=math.radians(el); ds.append(Vector((math.cos(r)*math.cos(e),math.sin(r)*math.cos(e),math.sin(e))))
    return ds
def scan_dirs(code, dirs, step):
    D=builtins.hs_orig[code]; ob=D['bvh']; otile=D['otile']; ctr=D['ctr']; R=D['R']
    bP,ptile=post_bvh(code); restore=set(); kill=set(); n=int(R/step)
    for d in dirs:
        up=Vector((0,0,1)) if abs(d.z)<0.9 else Vector((1,0,0))
        u=d.cross(up).normalized(); w=u.cross(d).normalized()
        for i in range(-n,n+1):
            for j in range(-n,n+1):
                o=ctr+u*(i*step)+w*(j*step)-d*(R+5)
                judge(ob.ray_cast(o,d,2*R+10),bP.ray_cast(o,d,2*R+10),otile,ptile,restore,kill)
    return restore,kill
def scan_top(code, step=0.1):
    D=builtins.hs_orig[code]; ob=D['bvh']; otile=D['otile']; x0,x1,y0,y1,z0,z1=D['bb']
    bP,ptile=post_bvh(code); restore=set(); kill=set(); d=Vector((0,0,-1)); zt=z1+2
    nx=int((x1-x0)/step)+1; ny=int((y1-y0)/step)+1
    for i in range(nx):
        x=x0+i*step
        for j in range(ny):
            o=Vector((x,y0+j*step,zt))
            judge(ob.ray_cast(o,d,zt+5),bP.ray_cast(o,d,zt+5),otile,ptile,restore,kill)
    return restore,kill
def apply_fix(code, restore, kill):
    O=bpy.data.objects["GNJC_"+code]; D=builtins.hs_orig[code]; oV=D['V']; oF=D['faces']; me=O.data; M=O.matrix_world
    bm=bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table(); uv=bm.loops.layers.uv.get("UVMap") or bm.loops.layers.uv.active
    bmesh.ops.delete(bm,geom=[bm.faces[i] for i in kill if i<len(bm.faces)],context='FACES')
    slot={}
    for k,m in enumerate(me.materials):
        if m: slot.setdefault(base(m.name),k)
    Mi=M.inverted(); miss=0
    for fi in restore:
        mname,f=oF[fi]; b0=base(mname)
        if b0 not in slot:
            mm=bpy.data.materials.get(mname) or bpy.data.materials.get(b0)
            if mm is None: miss+=1; continue
            me.materials.append(mm); slot[b0]=len(me.materials)-1
        try: nf=bm.faces.new([bm.verts.new(Mi@oV[q[0]-1]) for q in f])
        except ValueError: miss+=1; continue
        nf.material_index=slot[b0]; nf.smooth=True
        if uv:
            for lp,q in zip(nf.loops,f):
                if q[1]: lp[uv].uv=D['VT'][q[1]-1]
    bm.to_mesh(me); bm.free(); return miss
