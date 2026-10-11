"""신사 돌 축대 LOD1 굽기(2026-10-04): 껍데기(줄눈 면 + 끝막음 + 갓돌)에 LOD0(돌·갓돌)를 구움 — 성 석축 LOD 와 같은 방식(impcx).
출력 out_shrine/: JPShrineWall.bin(HNMS: SHRINE_WALL_LOD0 · SHRINE_WALL_LOD1), P0_BC.png · P0_NOS.png, layout.json"""
import os, sys, json, time
os.environ.setdefault('IMPOSTOR_DATA', '/home/claude/jplod/data'); os.environ.setdefault('IMPOSTOR_TEX', '/home/claude/jplod/data/tex')
os.environ['IMP_PAGE'] = '2048'
for d in ['/home/claude/jplod', '/home/claude/impostor', '/home/claude/palace_simplify/hnm', '/home/claude/palace_simplify']:
    sys.path.append(d)
import numpy as np
import impcx as IC
from load_cx import load_complex
from common import Bld
from export import write_meshes
import pbsimp
from pbsimp import weld, compact
JT = '/home/claude/jptown/'
OUT = JT + 'out_shrine'; os.makedirs(OUT, exist_ok=True)
IC.DENS = dict(IC.DENS)
IC.DENS[('base', 'S')] = (56, 0.3, 0.8); IC.DENS[('base', 'T')] = (40, 0.3, 0.6); IC.DENS[('base', 'B')] = (8, 0.3, 0.6)


def main():
    t0 = time.time(); log = print
    _, mats = load_complex('JPCastle')
    mname = [m['name'] for m in mats]
    d = np.load(JT + 'shrine_wall_L0.npz', allow_pickle=True)
    pos, nrm, uv = d['pos'].astype(np.float64), d['nrm'], d['uv']
    subs = list(d['subs']); smats = list(d['mats'])
    # 굽기 장면(LOD0 전체)
    T = np.concatenate([np.asarray(s).reshape(-1, 3) for s in subs])
    TM = np.concatenate([np.full(len(s) // 3, mname.index(m)) for s, m in zip(subs, smats)])
    scene = dict(P=pos, N=nrm.astype(np.float64), U=uv.astype(np.float64), T=T, TM=TM, mats=mats)
    Bm = Bld(scene)
    # 껍데기 → 정점 합침
    shT = np.load(JT + 'shrine_wall_shellT.npy')
    V = shT.reshape(-1, 3); F = np.arange(len(V)).reshape(-1, 3)
    q = np.round(V / 1e-4).astype(np.int64); _, idx, inv = np.unique(q, axis=0, return_index=True, return_inverse=True)
    V = V[idx]; F = inv.ravel()[F]
    hulls = {'SHRINE_WALL': dict(V=V, F=F, part=np.full(len(F), 2, np.int8), info={}, src_tris=int(len(T)))}
    charts = IC.make_charts(hulls)
    place, n, s = IC.layout(charts, 1, log)
    meshes = IC.build_meshes(hulls, charts, place, 'JPShrineWall')
    L1 = meshes['SHRINE_WALL']['mesh']
    L0 = dict(pos=d['pos'], nrm=d['nrm'], uv=d['uv'], subs=[np.asarray(x, np.int32) for x in subs], mats=smats)
    # LOD0 정점 합치기(면마다 따로라 큼) — 같은 위치·법선·UV 만
    p, nn, uu, ss = weld(L0['pos'], L0['nrm'], L0['uv'], [x.astype(np.uint32) for x in L0['subs']], use_normals=True)
    p, nn, uu, ss = compact(p, nn, uu, ss)
    L0 = dict(pos=p, nrm=nn, uv=uu, subs=[np.asarray(x, np.int32) for x in ss], mats=smats)
    write_meshes(os.path.join(OUT, 'JPShrineWall.bin'), [('SHRINE_WALL_LOD0', L0), ('SHRINE_WALL_LOD1', L1)])
    st = []
    for pg in range(n):
        BC, NOS, stt = IC.bake_page(Bm, pg, meshes, charts, log)
        IC.save_png(BC, os.path.join(OUT, 'P%d_BC.png' % pg)); IC.save_png(NOS, os.path.join(OUT, 'P%d_NOS.png' % pg)); st.append(stt)
    json.dump(dict(pages=n, scale=s, lod0_tris=int(sum(len(x) for x in L0['subs']) // 3), lod0_verts=int(len(L0['pos'])),
                   lod1_tris=int(sum(len(x) for x in L1['subs']) // 3), lod1_mats=L1['mats'], lod0_mats=smats, bake=st), open(os.path.join(OUT, 'layout.json'), 'w'), indent=1)
    log('done %.0f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
