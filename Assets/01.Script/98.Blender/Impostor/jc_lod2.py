"""일본성 천수·망루 LOD2 다시 만들기(2026-10-04): 예전 LOD2(지붕 모양 상자)는 천수 1층 처마·벽이 빠져 멀리서 1층이 비어 보였음
→ LOD1(기와 판 + 나머지 줄임)을 더 크게 줄인 것으로 바꿈. 그림 페이지는 LOD1 과 같은 것을 씀(새로 굽지 않음).
입력·출력: out_castle/JPCastle_LOD.bin 의 '<부분>_LOD1' → '<부분>_LOD2' 를 바꿔 JPCastle_LOD2.bin 으로."""
import sys, os, json
for d in ['/home/claude/impostor', '/home/claude/palace_simplify/hnm', '/home/claude/palace_simplify']: sys.path.append(d)
import numpy as np, pbsimp
from pbsimp import weld, simplify, compact
from export import read_meshes, write_meshes
SRC = sys.argv[1] if len(sys.argv) > 1 else 'out_castle/JPCastle_LOD.bin'
TOL = {'TENSHU': (0.25, 0.15), 'YAGURA_NE': (0.15, 0.10), 'YAGURA_SW': (0.15, 0.10)}   # (나머지, 판)
m = read_meshes(SRC); it = m.items() if isinstance(m, dict) else m
meshes = {x[0]: x[1] for x in it}
out = []
def part(d, idx, tol, opt):
    subs0 = [np.asarray(d['subs'][i], np.uint32) for i in idx]
    p, n, u, s = weld(np.asarray(d['pos'], np.float32), np.asarray(d['nrm'], np.float32), np.asarray(d['uv'], np.float32), subs0, use_normals=True)
    s, err = simplify(p, n, u, s, tol, 0.05, 0.3, options=opt)
    p, n, u, s = compact(p, n, u, s)
    k = [i for i, x in enumerate(s) if len(x)]
    return dict(pos=p, nrm=n, uv=u, subs=[s[i] for i in k], mats=[d['mats'][idx[i]] for i in k])
stats = {}
for nm in meshes:
    g = nm.rsplit('_LOD', 1)[0]
    if nm.endswith('_LOD2') and g in TOL:
        d = meshes[g + '_LOD1']
        sheet = [i for i, mm in enumerate(d['mats']) if mm.startswith('IMP_')]
        rest = [i for i in range(len(d['mats'])) if i not in sheet]
        a = part(d, rest, TOL[g][0], pbsimp.ErrorAbsolute | (1 << 5) | pbsimp.Prune)
        b = part(d, sheet, TOL[g][1], pbsimp.ErrorAbsolute | (1 << 5) | pbsimp.Prune)
        nb = len(a['pos'])
        L = dict(pos=np.concatenate([a['pos'], b['pos']]), nrm=np.concatenate([a['nrm'], b['nrm']]), uv=np.concatenate([a['uv'], b['uv']]),
                 subs=[np.asarray(x, np.int32) for x in a['subs']] + [np.asarray(x, np.int32) + nb for x in b['subs']], mats=a['mats'] + b['mats'])
        stats[nm] = dict(lod1=int(sum(len(x) for x in d['subs']) // 3), old=int(sum(len(x) for x in meshes[nm]['subs']) // 3), new=int(sum(len(x) for x in L['subs']) // 3), mats=L['mats'])
        out.append((nm, L))
print(json.dumps(stats, indent=1, ensure_ascii=False))
write_meshes('out_castle/JPCastle_LOD2.bin', out)
