import numpy as np, json, time
from pbsimp import *
from rayeval import RMesh, compare, compare2
TEX = '/mnt/user-data/uploads/Haesin/Temp/PalaceSimplify/tex/'
matmap = json.load(open(TEX + 'Heungnyemun_matmap.json', encoding='utf-8'))
inst = {}
for line in open('data/Heungnyemun/instances.tsv', encoding='utf-8'):
    p = line.rstrip('\n').split('\t'); k = int(p[0])
    if k not in inst: inst[k] = p[18].split('|')
ms = read_bin('data/Heungnyemun/meshes.bin')
texcache = {}
def tex_for(mname):
    fn = matmap.get(mname)
    if not fn: return None
    if fn not in texcache: texcache[fn] = np.load(TEX + fn)
    return texcache[fn]
def rmesh_from(pos, uv, subs):
    tris = np.concatenate([s.reshape(-1, 3) for s in subs]) if subs else np.zeros((0, 3), np.int32)
    tmat = np.concatenate([np.full(len(s) // 3, k) for k, s in enumerate(subs)]) if subs else np.zeros(0, np.int32)
    return RMesh(pos, tris, uv, tmat)
names = ['SM_HNM_Windowframe_A', 'SM_HNM_Roof_B03', 'SM_HNM_Window_A', 'SM_HNM_Gongpo_J']
for i, d in enumerate(ms):
    if d['name'] not in names: continue
    texs = {k: tex_for(n) for k, n in enumerate(inst[i])}
    subs0 = [s.astype(np.int32) for s in d['subs']]
    RO = rmesh_from(d['pos'], d['uv'], subs0)
    flat = flat_fraction(d) >= 0.9
    pos, nrm, uv, S = weld(d['pos'], d['nrm'], d['uv'], [s.astype(np.uint32) for s in d['subs']], use_normals=not flat)
    for tol, wuv in ((0.002, 100), (0.004, 100), (0.008, 100), (0.015, 100), (0.008, 400)):
        t = time.time()
        res, err = simplify(pos, nrm, uv, S, tol, wuv, 0.0 if flat else 0.2)
        nt = sum(len(s) for s in res) // 3
        RD = rmesh_from(pos, uv, [s.astype(np.int32) for s in res])
        r = compare2(RO, RD, texs, tol)
        RD.free()
        print('%-22s tol %.3f w %3d tris %6d/%6d  miss %.4f depth %.4f color %.4f rays %d  t=%.1f' % (d['name'], tol, wuv, nt, len(RO.tris), r['miss'], r['depth'], r['color'], r['rays'], time.time() - t))
    r = compare(RO, RO, texs, 0.002); print('   self', r)
    RO.free()
