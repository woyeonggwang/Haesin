"""일본성 LOD 만들기(2026-10-04). 부분: TENSHU(천수 바깥 + 천수 기단 Keep_F1) · YAGURA_NE · YAGURA_SW(망루) · WALL(석축·성벽 위 통로·흰 담).
실내(JC_Interior_*·JC_Keep_Interior_*)와 성문(West_Gate_*)은 메시를 안 바꿈(Unity 에서 거리로 끔).
TENSHU·YAGURA: LOD1 = 서까래·처마 받침(Exposed_Rafters·Roof_Brackets)과 기와(Tenshu_Charcoal_Verdigris_Kawara·Tenshu_Raised_Kawara_Ribs)를 빼고 나머지 줄임 + 기와 윗면 판(위·아래 두 장, 그림은 페이지에 구움)
                LOD2 = 지붕 모양 상자(임포스터)
WALL: 석축 돌(3,477 덩어리, 덩어리마다 32 삼각형이라 줄여지지 않음)은 뒤에 있는 280 삼각형 둘레 껍데기(Castle_Integrated_…_Wall_Ring)에
      돌 그림을 구워 대신함 — LOD1 = 껍데기(페이지) + 성벽 위 통로 바닥·흰 담 원본, LOD2 = 껍데기 + 흰 담 줄임(15 cm), 통로 바닥 뺌.
재질 대부분이 삼면 투영(월드 좌표) — 굽기 장면에서만 그 삼각형의 UV 를 면 방향 투영 × 배율로 바꿔 그림을 읽음(같은 셰이더 규칙: X 면 zy, Y 면 xz, Z 면 xy).
출력 out_castle/: JPCastle_LOD.bin(HNMS: '<부분>_LOD1'·'<부분>_LOD2', 프리팹 루트 좌표), P<k>_BC.png·P<k>_NOS.png, layout.json"""
import os, sys, json, time, re
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
OUT = os.environ.get('JPLOD_OUT', '/home/claude/jplod/out_castle')
CX = 'JPCastle'
IC.KEEP_TRIS = 100
IC.DENS = {('roof', 'T'): (48, 0.6, 1.2), ('roof', 'B'): (20, 0.5, 1.5), ('roof', 'S'): (40, 0.4, 1.0),
           ('body', 'S'): (36, 0.35, 1.2), ('body', 'T'): (16, 0.3, 0.6), ('body', 'B'): (8, 0.3, 0.6),
           ('base', 'S'): (24, 0.3, 0.8), ('base', 'T'): (16, 0.3, 0.5), ('base', 'B'): (4, 0.3, 0.5),
           ('pillar', 'S'): (24, 0.2, 0.4), ('pillar', 'T'): (8, 0.2, 0.4), ('pillar', 'B'): (4, 0.2, 0.4)}
# LOD1 이 시작되는 거리(천수 약 80 m, 망루 약 45 m)에서 4K 화소 약 4.3 / 2.4 cm
SHEET_TOL = {'TENSHU': 0.04, 'YAGURA_NE': 0.025, 'YAGURA_SW': 0.025}
REST_TOL = {'TENSHU': 0.05, 'YAGURA_NE': 0.03, 'YAGURA_SW': 0.03}
TILE = {'Tenshu_Charcoal_Verdigris_Kawara', 'Tenshu_Raised_Kawara_Ribs'}
TRIPLANAR = {'Ishigaki_': 0.25, 'Tenshu_': 0.5}     # 재질 이름 앞부분 → _TexWorldScale


def group_of(nm):
    n = nm.split('|')[-1]
    if n.startswith('JC_Interior_') or n.startswith('JC_Keep_Interior_'): return 'INTERIOR'
    if n.startswith('West_Gate_'): return 'GATE'
    if n.startswith('JC_Yagura_NE'): return 'YAGURA_NE'
    if n.startswith('JC_Yagura_SW'): return 'YAGURA_SW'
    if n.startswith('JC_Tenshu_') or n == 'JC_Keep_F1': return 'TENSHU'
    return 'WALL'


def triplanar_uv(objs, mats):
    """굽기 장면용: 삼면 투영 재질 삼각형을 정점 분리하고 UV = 면 방향 투영 × 배율"""
    sc = np.zeros(len(mats))
    for i, m in enumerate(mats):
        for k, v in TRIPLANAR.items():
            if m['name'].startswith(k): sc[i] = v
    out = {}
    for nm, o in objs.items():
        tp = sc[o['TM']] > 0
        if not tp.any():
            out[nm] = o; continue
        P, N, U, T, TM = o['P'], o['N'], o['U'], o['T'], o['TM']
        Tt = T[tp]; p = P[Tt].reshape(-1, 3); n = N[Tt].reshape(-1, 3)
        fn = np.cross(P[Tt[:, 1]] - P[Tt[:, 0]], P[Tt[:, 2]] - P[Tt[:, 0]]); ax = np.abs(fn).argmax(1).repeat(3)
        s = sc[TM[tp]].repeat(3)
        uv = np.where((ax == 0)[:, None], p[:, [2, 1]], np.where((ax == 1)[:, None], p[:, [0, 2]], p[:, [0, 1]])) * s[:, None]
        nb = len(P)
        out[nm] = dict(P=np.concatenate([P, p]), N=np.concatenate([N, n]), U=np.concatenate([U, uv]),
                       T=np.concatenate([T[~tp], nb + np.arange(len(p)).reshape(-1, 3)]), TM=np.concatenate([TM[~tp], TM[tp]]))
    return out


def merged(objs, names):
    m = merge(objs, names)
    return dict(P=m['P'], N=m['N'], U=m['U'], T=m['T'], TM=m['TM'])


def simp_group(objs0, names, mname, tol, prune, drop_mats=()):
    P = []; N = []; U = []; S = {}; base = 0
    for n in names:
        if re.search(r'Exposed_Rafters|Roof_Brackets', n) and drop_mats:
            continue        # 서까래·처마 받침은 기와 판 아랫장 그림에 구움
        oo = objs0[n]
        keep = ~np.isin(oo['TM'], [i for i, x in enumerate(mname) if x in drop_mats])
        for mi in np.unique(oo['TM'][keep]):
            S.setdefault(int(mi), []).append(oo['T'][keep & (oo['TM'] == mi)] + base)
        P.append(oo['P']); N.append(oo['N']); U.append(oo['U']); base += len(oo['P'])
    P = np.concatenate(P).astype(np.float32); N = np.concatenate(N).astype(np.float32); U = np.concatenate(U).astype(np.float32)
    mids = sorted(S)
    subs0 = [np.concatenate(S[k]).reshape(-1).astype(np.uint32) for k in mids]
    n0 = sum(len(s) for s in subs0) // 3
    p, nn, uu, subs = weld(P, N, U, subs0, use_normals=True)
    opt = pbsimp.ErrorAbsolute | (1 << 5) | (pbsimp.Prune if prune else 0)
    subs, err = simplify(p, nn, uu, subs, tol, 0.05, 0.3, options=opt)
    p, nn, uu, subs = compact(p, nn, uu, subs)
    keepk = [i for i, s in enumerate(subs) if len(s)]
    return dict(pos=p, nrm=nn, uv=uu, subs=[subs[i] for i in keepk], mats=[mname[mids[i]] for i in keepk]), n0


def main():
    os.makedirs(OUT, exist_ok=True)
    logf = open(os.path.join(OUT, 'log.txt'), 'a', encoding='utf-8')
    def log(*a):
        s = ' '.join(str(x) for x in a); print(s, flush=True); logf.write(s + '\n'); logf.flush()
    t0 = time.time()
    objs0, mats = load_complex(CX)
    mname = [m['name'] for m in mats]
    tile_ids = [i for i, n in enumerate(mname) if n in TILE]
    groups = {}
    for nm in objs0:
        groups.setdefault(group_of(nm), []).append(nm)
    log('groups', {g: (len(v), int(sum(len(objs0[n]['T']) for n in v))) for g, v in groups.items()})
    objsB = triplanar_uv(objs0, mats)             # 굽기용
    BLD = ['TENSHU', 'YAGURA_NE', 'YAGURA_SW']
    hobjs = {g: merged(objsB, groups[g]) for g in BLD}
    hulls, keep = IC.make_hulls(CX, hobjs, mats, None, log)
    lod1 = {}; stats = {}
    for g in BLD:
        # 기와 판
        TP = []; TT = []; b = 0
        for n in groups[g]:
            o = objs0[n]; s = np.isin(o['TM'], tile_ids)
            if not s.any(): continue
            Tt = o['T'][s]; used, inv = np.unique(Tt.ravel(), return_inverse=True)
            TP.append(o['P'][used]); TT.append(inv.reshape(-1, 3) + b); b += len(used)
        sheets, rm = build_sheets(np.concatenate(TP).astype(np.float32), np.concatenate(TT).astype(np.int32), cell=0.25, gap=0.6, log=log)
        VV = []; FF = []; b = 0
        for sh in sheets:
            fill_small_holes(sh)
            V, F = triangulate(sh, 0.02)
            if len(F) == 0: continue
            V = np.asarray(V, np.float32); F = np.asarray(F, np.uint32)
            z = np.zeros_like(V); uv = np.zeros((len(V), 2), np.float32)
            subs, err = simplify(V, z, uv, [F.reshape(-1)], SHEET_TOL[g], 0.0, 0.0, options=pbsimp.ErrorAbsolute | pbsimp.LockBorder)
            V, _, _, subs = compact(V, z, uv, subs)
            VV.append(V.astype(np.float64)); FF.append(subs[0].reshape(-1, 3).astype(np.int64) + b); b += len(V)
        rm.free()
        V = np.concatenate(VV); F = np.concatenate(FF)
        V2 = np.concatenate([V, V - np.array([0, 0.01, 0])]); F2 = np.concatenate([F, F[:, [0, 2, 1]] + len(V)])
        hulls[g + '_L1'] = dict(V=V2, F=F2, part=np.zeros(len(F2), np.int8), info={}, src_tris=0)
        rest, n0 = simp_group(objs0, groups[g], mname, REST_TOL[g], True, TILE)
        lod1[g] = rest
        log('%s sheets %d tris(x2), rest %d -> %d' % (g, len(F), n0, sum(len(s) for s in rest['subs']) // 3))
    ring = [n for n in groups['WALL'] if n.endswith('Wall_Ring')][0]
    ro = objs0[ring]
    hulls['WALL_SHELL'] = dict(V=ro['P'].astype(np.float64), F=ro['T'].astype(np.int64), part=np.full(len(ro['T']), 2, np.int8), info={}, src_tris=int(len(ro['T'])))
    deck = [n for n in groups['WALL'] if 'Perimeter_' in n]
    par = [n for n in deck if 'Parapet' in n]
    wall1, n1 = simp_group(objs0, deck, mname, 0.0, False)
    wall2, n2 = simp_group(objs0, par, mname, 0.15, True)
    log('WALL deck %d -> L1 %d, parapet %d -> L2 %d' % (n1, sum(len(s) for s in wall1['subs']) // 3, n2, sum(len(s) for s in wall2['subs']) // 3))
    charts = IC.make_charts(hulls)
    place, n, s = IC.layout(charts, int(os.environ.get('PAGES', '0')), log)
    meshes = IC.build_meshes(hulls, charts, place, CX)
    out = []
    for g in BLD:
        a = lod1[g]; sm = meshes[g + '_L1']['mesh']; nb = len(a['pos'])
        L1 = dict(pos=np.concatenate([a['pos'], sm['pos']]), nrm=np.concatenate([a['nrm'], sm['nrm']]), uv=np.concatenate([a['uv'], sm['uv']]),
                  subs=[np.asarray(x, np.int32) for x in a['subs']] + [np.asarray(x, np.int32) + nb for x in sm['subs']], mats=a['mats'] + sm['mats'])
        out += [(g + '_LOD1', L1), (g + '_LOD2', meshes[g]['mesh'])]
        stats[g] = dict(lod0=int(len(hobjs[g]['T'])), lod1=int(sum(len(x) for x in L1['subs']) // 3), lod2=int(sum(len(x) for x in meshes[g]['mesh']['subs']) // 3),
                        lod1_mats=L1['mats'], lod2_mats=meshes[g]['mesh']['mats'])
    sh = meshes['WALL_SHELL']['mesh']
    for nm, w in (('WALL_LOD1', wall1), ('WALL_LOD2', wall2)):
        nb = len(w['pos'])
        L = dict(pos=np.concatenate([w['pos'], sh['pos']]), nrm=np.concatenate([w['nrm'], sh['nrm']]), uv=np.concatenate([w['uv'], sh['uv']]),
                 subs=[np.asarray(x, np.int32) for x in w['subs']] + [np.asarray(x, np.int32) + nb for x in sh['subs']], mats=w['mats'] + sh['mats'])
        out.append((nm, L))
    stats['WALL'] = dict(lod0=int(sum(len(objs0[x]['T']) for x in groups['WALL'])), lod1=int(sum(len(x) for x in out[-2][1]['subs']) // 3),
                         lod2=int(sum(len(x) for x in out[-1][1]['subs']) // 3), lod1_mats=out[-2][1]['mats'], lod2_mats=out[-1][1]['mats'])
    stats['groups'] = {g: [x.split('|')[-1] for x in v] for g, v in groups.items()}
    write_meshes(os.path.join(OUT, 'JPCastle_LOD.bin'), out)
    json.dump(dict(pages=n, scale=s, stats=stats), open(os.path.join(OUT, 'layout.json'), 'w'), indent=1)
    log('stats', json.dumps({k: {q: v[q] for q in ('lod0', 'lod1', 'lod2')} for k, v in stats.items() if k != 'groups'}), 'pages', n, 'scale %.3f' % s)
    if os.environ.get('NOBAKE'):
        return
    keepnames = [x for x in objsB if group_of(x) not in ('INTERIOR',)]
    m = merge(objsB, keepnames); m['mats'] = mats
    Bm = Bld(m)
    for pg in range(n):
        BC, NOS, st = IC.bake_page(Bm, pg, meshes, charts, log)
        IC.save_png(BC, os.path.join(OUT, 'P%d_BC.png' % pg)); IC.save_png(NOS, os.path.join(OUT, 'P%d_NOS.png' % pg))
    log('done %.0f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
