import sys, json, numpy as np, math, time
from pbsimp import read_bin, flat_fraction, weld, simplify, compact
from rayeval import RMesh, compare2
cx = sys.argv[1]
TEX = '/mnt/user-data/uploads/Haesin/Temp/PalaceSimplify/tex/'
matmap = json.load(open(TEX + '%s_matmap.json' % cx, encoding='utf-8'))
inst_mats = {}; cnt = {}
for line in open('data/%s/instances.tsv' % cx, encoding='utf-8'):
    p = line.rstrip('\n').split('\t'); k = int(p[0]); inst_mats.setdefault(k, p[18].split('|')); cnt[k] = cnt.get(k, 0) + 1
ms = read_bin('data/%s/meshes.bin' % cx)
tc = {}
def tex_for(n):
    fn = matmap.get(n)
    if not fn: return None
    if fn not in tc: tc[fn] = np.load(TEX + fn)
    return tc[fn]
def rm(pos, uv, subs, vn):
    tris = np.concatenate([s.reshape(-1, 3) for s in subs]).astype(np.int32)
    tmat = np.concatenate([np.full(len(s) // 3, k) for k, s in enumerate(subs)])
    return RMesh(pos, tris, uv, tmat, vn)
order = sorted([k for k in range(len(ms)) if k in cnt], key=lambda k: -cnt[k] * sum(len(s) for s in ms[k]['subs']))
NC = math.cos(math.radians(12))
for k in order[:int(sys.argv[2]) if len(sys.argv) > 2 else 10]:
    d = ms[k]; subs0 = [s.astype(np.uint32) for s in d['subs']]
    ff = flat_fraction(d)
    if ff >= 0.9: continue
    RO = rm(d['pos'], d['uv'], [s.astype(np.int32) for s in subs0], d['nrm'])
    texs = {i: tex_for(n) for i, n in enumerate(inst_mats[k])}
    pos, nrm, uv, S = weld(d['pos'], d['nrm'], d['uv'], subs0, use_normals=True)
    line = '%-30s inst %3d tris %6d |' % (d['name'][:30], cnt[k], sum(len(s) for s in subs0) // 3)
    for wn in (0.2, 0.05, 0.02):
        for tol in (0.004, 0.008):
            res, err = simplify(pos, nrm, uv, S, tol, 100.0, wn)
            nt = sum(len(s) for s in res) // 3
            RD = rm(pos, uv, [s.astype(np.int32) for s in res], nrm)
            r = compare2(RO, RD, texs, tol, nrm_cos=NC); RD.free()
            e = r['miss'] + r['depth'] + r['color'] + r['shade']
            line += ' w%.2f/%dmm %6d %s%.4f(sh%.4f)' % (wn, int(tol*1000), nt, 'OK' if e <= 0.002 else 'NG', e, r['shade'])
    RO.free()
    print(line, flush=True)
