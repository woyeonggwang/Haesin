"""간의대 돌난간 판 그림 굽기용: 흥례문 데모 원본에서 영제교 서쪽 난간(하엽동자·돌란대·다리 몸체)을 모아 옆(서쪽)에서 평행 광선으로 찍음."""
import sys, os, json
for _d in (os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Impostor'), '/home/claude/impostor'):
    if os.path.isdir(_d): sys.path.insert(0, os.path.normpath(_d))
import numpy as np
import common
import load as L
from pbsimp import read_bin
from load_bld import read_mat_rows

def load_region(cx, box, names_ok=None):
    D = os.path.join(common.DATA, cx)
    inst = L.read_instances(os.path.join(D, 'instances.tsv'))
    meshes = read_bin(os.path.join(D, 'meshes.bin'))
    mrows = read_mat_rows(os.path.join(D, 'materials.tsv'))
    mm_path = os.path.join(common.TEXC, '%s_matmap.json' % cx)
    matmap = json.load(open(mm_path, encoding='utf-8')) if os.path.exists(mm_path) else {}
    P = []; N = []; U = []; T = []; TM = []; base = 0; mat_id = {}; mats = []; names = []
    def mid(n):
        if n not in mat_id:
            mat_id[n] = len(mats); r = mrows.get(n)
            col = [float(x) for x in r[4].split(',')] if r and r[4] else [1, 1, 1, 1]
            st = [float(x) for x in r[5].split(',')] if r and r[5] else [1, 1, 0, 0]
            f = matmap.get(n); npy = os.path.join(common.TEXC, f) if f else None
            if npy and not os.path.exists(npy): npy = None
            mats.append(dict(name=n, npy=npy, png=None, color=np.array(col, np.float32), st=np.array(st, np.float32),
                             clip=bool(r and r[6] not in ('', '0')), cutoff=float(r[7]) if r and r[7] else 0.5, smooth=float(r[8]) if r and r[8] else 0.3,
                             nrm=r[3] if r else ''))
        return mat_id[n]
    lo, hi = np.array(box[0]), np.array(box[1])
    for i in inst:
        d = meshes[i['k']]; nm = d['name']
        if names_ok and not any(k in nm for k in names_ok):
            continue
        M = i['M']; A = M[:3, :3]
        W = L.xform(M, d['pos'].astype(np.float64))
        c = (W.min(0) + W.max(0)) / 2
        if not ((c >= lo).all() and (c <= hi).all()):
            continue
        Nn = d['nrm'].astype(np.float64) @ np.linalg.inv(A); Nn /= np.maximum(np.linalg.norm(Nn, axis=1), 1e-12)[:, None]
        mir = np.linalg.det(A) < 0
        for s, idx in enumerate(d['subs']):
            t = np.asarray(idx).reshape(-1, 3).astype(np.int64)
            if len(t) == 0: continue
            if mir: t = t[:, [0, 2, 1]]
            T.append(t + base); TM.append(np.full(len(t), mid(i['mats'][s] if s < len(i['mats']) else ''), np.int32))
        P.append(W); N.append(Nn); U.append(d['uv'].astype(np.float64)); names.append(nm); base += len(W)
    return dict(P=np.concatenate(P), N=np.concatenate(N), U=np.concatenate(U), T=np.concatenate(T), TM=np.concatenate(TM), mats=mats, names=names)

if __name__ == '__main__':
    b = load_region('Heungnyemun', [(-12, -3, -50), (12, 4, -28)])
    from collections import Counter
    print(len(b['T']), 'tris', Counter(n.split('#')[0] for n in b['names']).most_common())
    print('bbox', b['P'].min(0).round(2), b['P'].max(0).round(2))
    for m in b['mats']: print(m['name'], m['npy'] and os.path.basename(m['npy']), m['st'], m['nrm'][-40:])
