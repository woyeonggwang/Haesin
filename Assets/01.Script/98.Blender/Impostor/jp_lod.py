"""일본 마을 집 7종 LOD 만들기(2026-10-04).
LOD1 = 원본에서 지붕 기와·초가(지붕 메시의 JP_Kawara·JP_Kawara_Ridge·JP_Thatched_Straw)를 빼고 나머지를 줄임(재질별, 3 cm, 작은 떨어진 조각 없앰·UV 이음매 넘어 접기 허용)
       + 기와 윗면을 따라 만든 판(위·아래 두 장, 0.2 m 격자, 기와 꼭대기 − 2 cm) — 판 그림은 LOD2 와 같은 페이지에 구움
LOD2 = 지붕 모양 상자(임포스터, hullgen) — 7종이 4096 페이지 하나를 같이 씀
입력: data/JPTown(PalaceBinExport: 프리팹을 x = 60·i 에 놓고 내보낸 것) + data/tex
출력 out/: JPTown_LOD.bin(HNMS: '<집>_LOD1'·'<집>_LOD2', 프리팹 로컬 좌표), P0_BC.png·P0_NOS.png, layout.json"""
import os, sys, json, time
# 입력: IMPOSTOR_DATA(JPTown 폴더의 부모), IMPOSTOR_TEX(tex/index.tsv), JPLOD_OUT(결과 폴더)
os.environ.setdefault('IMPOSTOR_DATA', '/home/claude/jplod/data'); os.environ.setdefault('IMPOSTOR_TEX', '/home/claude/jplod/data/tex')
os.environ['IMPOSTOR_OUT'] = '/home/claude/jplod'
_HERE = os.path.dirname(os.path.abspath(__file__))
for _d in (_HERE, os.path.join(_HERE, '..', 'Heungnyemun'), '/home/claude/impostor', '/home/claude/palace_simplify/hnm'):
    if os.path.isdir(_d): sys.path.append(os.path.normpath(_d))
import numpy as np
import impcx as IC
import common
from load_cx import load_complex, merge
from common import Bld
from export import write_meshes
from plate import build_sheets, fill_small_holes, triangulate
import pbsimp
from pbsimp import weld, simplify, compact
OUT = os.environ.get('JPLOD_OUT', '/home/claude/jplod/out')
IC.DENS = {('roof', 'T'): (64, 0.6, 1.2), ('roof', 'B'): (32, 0.5, 1.5), ('roof', 'S'): (48, 0.4, 1.0),
           ('body', 'S'): (40, 0.35, 1.2), ('body', 'T'): (16, 0.3, 0.6), ('body', 'B'): (8, 0.3, 0.6),
           ('base', 'S'): (32, 0.3, 0.8), ('base', 'T'): (24, 0.3, 0.5), ('base', 'B'): (4, 0.3, 0.5),
           ('pillar', 'S'): (32, 0.2, 0.4), ('pillar', 'T'): (8, 0.2, 0.4), ('pillar', 'B'): (4, 0.2, 0.4)}
IC.KEEP_TRIS = 100
CX = 'JPTown'
TILE = {'JP_Kawara', 'JP_Kawara_Ridge', 'JP_Thatched_Straw'}
SIMP_TOL = float(os.environ.get('SIMP_TOL', '0.03'))   # 35 m 에서 4K 화소 약 1.9 cm, 1080p 3.8 cm


def main():
    os.makedirs(OUT, exist_ok=True)
    logf = open(os.path.join(OUT, 'log.txt'), 'a', encoding='utf-8')
    def log(*a):
        s = ' '.join(str(x) for x in a); print(s, flush=True); logf.write(s + '\n'); logf.flush()
    t0 = time.time()
    objs0, mats = load_complex(CX)
    mname = [m['name'] for m in mats]
    tile_ids = [i for i, n in enumerate(mname) if n in TILE]
    houses = {}
    for nm in objs0:
        houses.setdefault(nm.split('|')[0], []).append(nm)
    objs = {}; offs = {}
    for h, names in houses.items():
        m = merge(objs0, names)
        objs[h] = dict(P=m['P'], N=m['N'], U=m['U'], T=m['T'], TM=m['TM'])
        offs[h] = float(np.round(np.median(m['P'][:, 0]) / 60.0) * 60.0)
    hulls, keep = IC.make_hulls(CX, objs, mats, None, log)
    # LOD1: 지붕 판(기와 윗면 − 2 cm, 위·아래 두 장)
    lod1_src = {}
    for h, names in houses.items():
        roof = [n for n in names if n.endswith('_Roof')][0]
        o = objs0[roof]
        sel = np.isin(o['TM'], tile_ids)
        Tt = o['T'][sel]
        used, inv = np.unique(Tt.ravel(), return_inverse=True)
        sheets, rm = build_sheets(o['P'][used].astype(np.float32), inv.reshape(-1, 3).astype(np.int32), cell=0.2, gap=0.6, log=log)
        VV = []; FF = []; b = 0
        for sh in sheets:
            fill_small_holes(sh)
            V, F = triangulate(sh, 0.02)
            if len(F) == 0:
                continue
            V = np.asarray(V, np.float32); F = np.asarray(F, np.uint32)
            nrm = np.zeros_like(V); uv = np.zeros((len(V), 2), np.float32)
            subs, err = simplify(V, nrm, uv, [F.reshape(-1)], 0.01, 0.0, 0.0, options=pbsimp.ErrorAbsolute | pbsimp.LockBorder)
            V, _, _, subs = compact(V, nrm, uv, subs)
            F = subs[0].reshape(-1, 3).astype(np.int64)
            VV.append(V.astype(np.float64)); FF.append(F + b); b += len(V)
        rm.free()
        V = np.concatenate(VV); F = np.concatenate(FF)
        # 아래 판 = 위 판을 1 cm 내리고 감김을 뒤집은 것(같은 자리에 겹치면 굽기 광선이 아무 쪽이나 맞음)
        V2 = np.concatenate([V, V - np.array([0, 0.01, 0])])
        F2 = np.concatenate([F, F[:, [0, 2, 1]] + len(V)])
        hulls[h + '_L1'] = dict(V=V2, F=F2, part=np.zeros(len(F2), np.int8), info={}, src_tris=0)
        log('%s roof sheets %d tris (x2)' % (h, len(F)))
        # 나머지 원본 줄이기(재질별)
        P = []; N = []; U = []; S = {}; base = 0
        for n in names:
            oo = objs0[n]
            s_ = ~np.isin(oo['TM'], tile_ids) if n == roof else np.ones(len(oo['T']), bool)
            for mi in np.unique(oo['TM'][s_]):
                t = oo['T'][s_ & (oo['TM'] == mi)]
                S.setdefault(int(mi), []).append(t + base)
            P.append(oo['P']); N.append(oo['N']); U.append(oo['U']); base += len(oo['P'])
        P = np.concatenate(P).astype(np.float32); N = np.concatenate(N).astype(np.float32); U = np.concatenate(U).astype(np.float32)
        mids = sorted(S)
        subs0 = [np.concatenate(S[k]).reshape(-1).astype(np.uint32) for k in mids]
        n0 = sum(len(s) for s in subs0) // 3
        p, nn, uu, subs = weld(P, N, U, subs0, use_normals=True)
        subs, err = simplify(p, nn, uu, subs, SIMP_TOL, 0.5, 0.3, options=pbsimp.ErrorAbsolute | pbsimp.Prune | (1 << 5))
        p, nn, uu, subs = compact(p, nn, uu, subs)
        lod1_src[h] = dict(pos=p, nrm=nn, uv=uu, subs=subs, mats=[mname[k] for k in mids])
        log('%s LOD1 rest %d -> %d tris' % (h, n0, sum(len(s) for s in subs) // 3))
    charts = IC.make_charts(hulls)
    place, n, s = IC.layout(charts, 1, log)
    meshes = IC.build_meshes(hulls, charts, place, CX)
    out = []; stats = {}
    for h in houses:
        off = np.array([offs[h], 0, 0], np.float32)
        a = lod1_src[h]; sm = meshes[h + '_L1']['mesh']
        nb = len(a['pos'])
        lod1 = dict(pos=np.concatenate([a['pos'], sm['pos']]) - off, nrm=np.concatenate([a['nrm'], sm['nrm']]),
                    uv=np.concatenate([a['uv'], sm['uv']]), subs=[np.asarray(x, np.int32) for x in a['subs']] + [np.asarray(x, np.int32) + nb for x in sm['subs']],
                    mats=a['mats'] + sm['mats'])
        l2 = meshes[h]['mesh']; lod2 = dict(l2, pos=l2['pos'] - off)
        out += [(h + '_LOD1', lod1), (h + '_LOD2', lod2)]
        stats[h] = dict(lod0=int(len(objs[h]['T'])), lod1=int(sum(len(x) for x in lod1['subs']) // 3), lod2=int(sum(len(x) for x in lod2['subs']) // 3),
                        lod1_mats=len(lod1['mats']))
    write_meshes(os.path.join(OUT, 'JPTown_LOD.bin'), out)
    json.dump(dict(pages=n, scale=s, offs=offs, stats=stats), open(os.path.join(OUT, 'layout.json'), 'w'), indent=1)
    log('stats', json.dumps(stats), 'scale %.3f' % s)
    np.savez(os.path.join(OUT, 'view.npz'), **{k + '|' + q: v[q] for k, v in out for q in ('pos',)}, **{k + '|tri': np.concatenate(v['subs']) for k, v in out})
    if os.environ.get('NOBAKE'):
        return
    m = merge(objs, list(objs.keys())); m['mats'] = mats
    Bm = Bld(m)
    for pg in range(n):
        BC, NOS, st = IC.bake_page(Bm, pg, meshes, charts, log)
        IC.save_png(BC, os.path.join(OUT, 'P%d_BC.png' % pg)); IC.save_png(NOS, os.path.join(OUT, 'P%d_NOS.png' % pg))
    log('done %.0f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
