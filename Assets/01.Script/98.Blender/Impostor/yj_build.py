"""육조거리 관아 묶음 — 궁 단지(사정전·강녕전·교태전) 단순화판을 관아 하나로 쓰기.

길에 가까운 앞쪽(정면 행각 + 문 + 양옆 행각 앞 20 m) = 원래 메시 그대로(가까이서 깨진 곳 없게, LOD0),
나머지(안쪽 전각·뒤 행각) = 지붕 모양 상자(임포스터), 앞쪽도 멀리서는 임포스터(LOD1).
현판(재질 이름에 Stela)은 모두 뺌(사용자 지시 2026-09-30). 땅판(Soilfloor·SoilFloor·OutFloor)은 빼고 터레인을 씀.

python3 yj_build.py <형식 A|B|C> [front|bake|all]
  front: out/YJ_<형식>/YJ_<형식>_Front.bin (HNMS, 메시 'YJ_<형식>_Front' 하나, 재질 = 원래 재질 이름) + front.json(기준점·크기·삼각형)
  bake : impcx 와 같은 방식으로 임포스터(YJ_<형식>_Imp.bin·_Keep.bin·P<k>_BC/NOS.png·layout.json)
좌표 = 원래 단지 프리팹 루트 좌표(정면 = -z). 기준점(pivot) = 정면 가운데 x, 땅 높이, 정면 z(처마 끝).
"""
import os, sys, json
import numpy as np
import common
from load_cx import load_complex

# 형식: 원래 단지, 앞쪽 상자(x0, x1, z0, z1 — 삼각형 무게중심), 통째로 앞쪽인 오브젝트(문), 땅 높이(단지 로컬 y)
# 앞쪽 = (z < zmid) 또는 (z < zside 이고 (x < xL 또는 x > xR)) — 경계 평면에서 삼각형을 잘라 나눔(비죽 나온 긴 삼각형 없게)
TYPES = {
    'A': dict(src='Sajeongjeon', ground=0.55, zmid=15.6, zside=18.6, xL=-41.6, xR=42.9, gate=['SJC_SJM', 'SJM_RoofBacking']),
    'B': dict(src='Gangnyeongjeon', ground=0.55, zmid=-28.2, zside=-20.8, xL=-42.4, xR=43.3, gate=['GNJC_HOM', 'HOM_RoofBacking']),
    'C': dict(src='Gyotaejeon', ground=1.73, zmid=89.6, zside=97.4, xL=-17.0, xR=22.0, gate=['GTJ_YEM']),
}
DROP_MAT = ('Stela', 'Soilfloor', 'SoilFloor', 'OutFloor')


def is_backing(nm):
    return nm.endswith('RoofBacking')


def is_doorcard(nm, o):
    """문짝 판(4~12 삼각형짜리 오브젝트) — 임포스터에서는 굽기 장면에만"""
    return len(o['T']) <= 12


def clip_plane(o, axis, val):
    """삼각형을 평면 (좌표[axis] = val) 로 잘라 (아래쪽 < val, 위쪽 >= val) 두 오브젝트로. 잘린 곳은 정점을 새로 만듦(위치·법선·UV 보간)."""
    P, N, U, T, TM = o['P'], o['N'], o['U'], o['T'], o['TM']
    d = P[:, axis] - val
    dt = d[T]
    below = (dt < 0).all(1); above = (dt >= 0).all(1); cross = ~(below | above)
    outs = []
    for side in (0, 1):
        sel = below if side == 0 else above
        outs.append(dict(T=[T[sel]], TM=[TM[sel]]))
    NP = [P]; NN = [N]; NU = [U]; nv = len(P)
    ci = np.nonzero(cross)[0]
    for k in ci:
        tri = T[k]; m = TM[k]
        poly = [(int(i), d[i]) for i in tri]
        for side in (0, 1):
            ring = []
            for a in range(3):
                i0, d0 = poly[a]; i1, d1 = poly[(a + 1) % 3]
                in0 = (d0 < 0) if side == 0 else (d0 >= 0)
                in1 = (d1 < 0) if side == 0 else (d1 >= 0)
                if in0:
                    ring.append(('v', i0))
                if in0 != in1:
                    tt = d0 / (d0 - d1)
                    ring.append(('n', i0, i1, tt))
            ids = []
            for r in ring:
                if r[0] == 'v':
                    ids.append(r[1])
                else:
                    _, i0, i1, tt = r
                    NP.append((P[i0] * (1 - tt) + P[i1] * tt)[None]); nn = N[i0] * (1 - tt) + N[i1] * tt
                    NN.append((nn / max(np.linalg.norm(nn), 1e-12))[None]); NU.append((U[i0] * (1 - tt) + U[i1] * tt)[None])
                    ids.append(nv); nv += 1
            for q in range(1, len(ids) - 1):
                outs[side]['T'].append(np.array([[ids[0], ids[q], ids[q + 1]]])); outs[side]['TM'].append(np.array([m]))
    P2 = np.concatenate(NP); N2 = np.concatenate(NN); U2 = np.concatenate(NU)
    res = []
    for side in (0, 1):
        Tt = np.concatenate(outs[side]['T']).astype(np.int64) if outs[side]['T'] else np.zeros((0, 3), np.int64)
        Tm = np.concatenate(outs[side]['TM']).astype(np.int32) if outs[side]['TM'] else np.zeros(0, np.int32)
        res.append(dict(P=P2, N=N2, U=U2, T=Tt.reshape(-1, 3), TM=Tm))
    return res


def cat(a, b):
    """같은 정점 배열을 쓰는 두 조각을 합침"""
    if len(a['P']) != len(b['P']):
        base = len(a['P'])
        return dict(P=np.concatenate([a['P'], b['P']]), N=np.concatenate([a['N'], b['N']]), U=np.concatenate([a['U'], b['U']]),
                    T=np.concatenate([a['T'], b['T'] + base]), TM=np.concatenate([a['TM'], b['TM']]))
    return dict(a, T=np.concatenate([a['T'], b['T']]), TM=np.concatenate([a['TM'], b['TM']]))


def split_front(o, T_):
    lo, hi = clip_plane(o, 2, T_['zside'])            # hi: 뒤
    f1, mid = clip_plane(lo, 2, T_['zmid'])           # f1: 앞(가운데 포함)
    left, rest1 = clip_plane(mid, 0, T_['xL'])       # left: 왼쪽 옆 행각
    rest2, right = clip_plane(rest1, 0, T_['xR'])    # right: 오른쪽 옆 행각
    front = cat(cat(f1, left), right)
    back = cat(hi, rest2)
    return front, back


def split_type(t):
    T_ = TYPES[t]
    objs, mats = load_complex(T_['src'])
    dropm = np.array([any(k in m['name'] for k in DROP_MAT) for m in mats])
    front = {}; bake = {}
    stats = dict(dropped=0)
    for nm, o in objs.items():
        if nm.startswith('SM_GNJ_SoilFloor'):
            continue
        keep = ~dropm[o['TM']]
        stats['dropped'] += int((~keep).sum())
        if not keep.any():
            continue
        o = dict(o, T=o['T'][keep], TM=o['TM'][keep])
        if nm in T_['gate']:
            front[nm] = o
            continue
        f, b = split_front(o, T_)
        if len(f['T']):
            front[nm] = f
        if len(b['T']):
            bake[nm] = b
    return objs, mats, front, bake, stats


def merge_subset(parts, mats):
    """{이름: dict(P, N, U, T, TM)} → 메시 하나(재질마다 서브메시, 쓰는 정점만)"""
    P = []; N = []; U = []; T = []; TM = []; base = 0
    for nm, o in parts.items():
        used, inv = np.unique(o['T'].ravel(), return_inverse=True)
        P.append(o['P'][used]); N.append(o['N'][used]); U.append(o['U'][used])
        T.append(inv.reshape(-1, 3) + base); TM.append(o['TM']); base += len(used)
    P = np.concatenate(P); N = np.concatenate(N); U = np.concatenate(U); T = np.concatenate(T); TM = np.concatenate(TM)
    subs = []; names = []
    for m in np.unique(TM):
        subs.append(T[TM == m].reshape(-1).astype(np.int32)); names.append(mats[m]['name'])
    return dict(pos=P.astype(np.float32), nrm=N.astype(np.float32), uv=U.astype(np.float32), subs=subs, mats=names), T, TM


def outdir(t):
    from impcx import OUTROOT
    d = os.path.join(OUTROOT, 'YJ_' + t); os.makedirs(d, exist_ok=True)
    return d


def do_front(t):
    from export import write_meshes
    objs, mats, front, bake, st = split_type(t)
    mesh, T, TM = merge_subset(front, mats)
    P = mesh['pos']
    # 정면·기준점: 정면 z = 앞쪽 메시 최소 z(처마 끝), x 가운데 = 전체(앞+뒤) 경계 가운데
    allP = np.concatenate([o['P'][o['T'].ravel()] for o in list(front.values()) + list(bake.values())])
    x0, x1 = float(allP[:, 0].min()), float(allP[:, 0].max()); z0, z1 = float(allP[:, 2].min()), float(allP[:, 2].max())
    piv = [(x0 + x1) / 2, TYPES[t]['ground'], float(P[:, 2].min())]
    # 메시 좌표 = 기준점 기준(Unity 에서 프리팹 루트 = 기준점, 메시 경계가 곧 로컬 경계가 되게)
    wm = dict(mesh, pos=(P - np.array(piv, np.float32)).astype(np.float32))
    write_meshes(os.path.join(outdir(t), 'YJ_%s_Front.bin' % t), [('YJ_%s_Front' % t, wm)])
    info = dict(type=t, src=TYPES[t]['src'], pivot=piv, bounds=[x0, x1, z0, z1], front_tris=int(len(T)), front_verts=int(len(P)),
                front_mats=len(mesh['mats']), front_bounds=[P.min(0).tolist(), P.max(0).tolist()],
                bake_tris=int(sum(len(o['T']) for o in bake.values())), dropped_tris=st['dropped'],
                front_objs={nm: int(len(o['T'])) for nm, o in front.items()})
    json.dump(info, open(os.path.join(outdir(t), 'front.json'), 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
    print(json.dumps(info, indent=1, ensure_ascii=False))
    return info


def bake_objects(t):
    """임포스터 굽기용 오브젝트: 앞쪽(카드·받침판 제외)을 합친 'FRONT' + 나머지 오브젝트 그대로.
    받침판·문짝 판은 이름 앞에 Card_ 를 붙여 굽기 장면에만 넣음(impcx.is_card)."""
    objs, mats, front, bake, st = split_type(t)
    out = {}
    fparts = {}
    for nm, o in front.items():
        if is_backing(nm) or is_doorcard(nm, o) or 'Card' in nm or 'Panel' in nm:
            out['Card_F_' + nm] = o
        else:
            fparts[nm] = o
    if fparts:
        P = []; N = []; U = []; T = []; TM = []; base = 0
        for nm, o in fparts.items():
            P.append(o['P']); N.append(o['N']); U.append(o['U']); T.append(o['T'] + base); TM.append(o['TM']); base += len(o['P'])
        out['FRONT'] = dict(P=np.concatenate(P), N=np.concatenate(N), U=np.concatenate(U), T=np.concatenate(T), TM=np.concatenate(TM))
    for nm, o in bake.items():
        if is_backing(nm) or is_doorcard(nm, o) or 'Card' in nm or 'Panel' in nm:
            out['Card_' + nm] = o
        else:
            out[nm] = o
    return out, mats


# ---- 행랑 방식(2026-09-30 사용자 지시 "멀쩡한 벽 한칸이랑 문을 떼가지고 계속 일자로 이어붙이세요") ----
# 길가 앞쪽은 사정전 남행각 조각을 이어 붙인 행랑(yj_row.py)이 맡음 → 단지마다 앞쪽(정면에서 12.6 m 안, 행랑 처마 뒤 끝 12.4 m)은 버리고
# 그 뒤만 임포스터로 굽는다(앞쪽은 굽기 장면에만). A 는 행랑 조각 자체의 먼 LOD 용 껍데기(ROW_BAY·ROW_GATE·ROW_END_W·ROW_END_E)와
# 뒤 행각을 옆 행각 없이 가운데(HG_BACK)만 따로 만든다(옆 행각은 줄 끝 조각 END_W/E 에 원래 메시로 들어감 — 줄 끝 관아는 A 틀).
# 행랑 처마 뒤 끝 = 정면에서 9.88 m(사정전 남행각 z 12.40) → 그 뒤(9.9 m)부터 임포스터로(옆 행각이 행랑 뒤에 바로 붙게)
ZREST = {'A': 2.52 + 9.9, 'B': -36.7712 + 9.9, 'C': 81.3317 + 9.9}
HALLS = {'A': ('SJC_SJJ', 'SJC_MCJ', 'SJC_CCJ'), 'B': ('GNJC_EJD', 'GNJC_YGD', 'GNJC_GSJ', 'GNJC_YSJ'), 'C': ('GTJ_GSG', 'GTJ_HHG', 'GTJ_WGH', 'GTJ_GSJ')}


def bake_objects_row(t):
    import yj_row as R
    T_ = TYPES[t]
    objs, mats = load_complex(T_['src'])
    dropm = np.array([any(k in m['name'] for k in DROP_MAT) for m in mats])
    out = {}
    ring = set(R.ROW_OBJS)
    rows = {k: [] for k in ('ROW_BAY', 'ROW_GATE', 'ROW_END_W', 'ROW_END_E')}
    for nm, o in objs.items():
        if nm.startswith('SM_GNJ_SoilFloor'):
            continue
        keep = ~dropm[o['TM']]
        if not keep.any():
            continue
        o = dict(o, T=o['T'][keep], TM=o['TM'][keep])
        small = is_backing(nm) or is_doorcard(nm, o) or 'Card' in nm or 'Panel' in nm
        if t == 'A' and (nm in ring or nm.startswith('SM_SJM_Door') or nm.startswith('SM_SJJ_HG_Door')):
            lo, hi = clip_plane(o, 2, R.ZROW)
            a, r1 = clip_plane(lo, 0, R.XG0); g, r2 = clip_plane(r1, 0, R.XG1); b, _ = clip_plane(r2, 0, R.XB1)
            wW, m1 = clip_plane(hi, 0, R.XW); m2, wE = clip_plane(m1, 0, R.XE)
            if small:
                for k, v in (('a', a), ('g', g), ('r2', r2), ('wW', wW), ('wE', wE), ('m2', m2)):
                    if len(v['T']):
                        out['Card_%s_%s' % (k, nm)] = v
                continue
            rows['ROW_BAY'].append(b); rows['ROW_GATE'].append(g)
            rows['ROW_END_W'] += [a, wW]; rows['ROW_END_E'] += [r2, wE]
            if nm == 'SJC_SJJ_HG':
                # 옆 행각만 따로(줄 끝 관아의 안쪽 옆 행각 — 행랑 뒤라 임포스터). 사정전 행각은 뒤가 트인 ㄷ자(뒤 행각 없음, 확인: 가운데 뒤쪽 16 삼각형)
                _, hi2 = clip_plane(o, 2, ZREST['A'])
                sW, sm = clip_plane(hi2, 0, R.XW); _, sE = clip_plane(sm, 0, R.XE)
                if len(sW['T']):
                    out['HG_W'] = sW
                if len(sE['T']):
                    out['HG_E'] = sE
            continue
        if nm in HALLS.get(t, ()):          # 안쪽 전각은 자르지 않음(앞이 잘리면 계단·툇간이 잘린 면으로 남음)
            out[nm] = o
            continue
        lo, hi = clip_plane(o, 2, ZREST[t])
        if len(lo['T']):
            out['Card_F_' + nm] = lo
        if len(hi['T']):
            out[('Card_' + nm) if small else nm] = hi
    for k, pl in rows.items():
        pl = [p for p in pl if len(p['T'])]
        if not pl:
            continue
        acc = pl[0]
        for p in pl[1:]:
            acc = cat(acc, p) if len(acc['P']) == len(p['P']) and acc['P'] is p['P'] else _cat_sep(acc, p)
        out[k] = acc
    return out, mats


def _cat_sep(a, b):
    base = len(a['P'])
    return dict(P=np.concatenate([a['P'], b['P']]), N=np.concatenate([a['N'], b['N']]), U=np.concatenate([a['U'], b['U']]),
                T=np.concatenate([a['T'], b['T'] + base]), TM=np.concatenate([a['TM'], b['TM']]))


if __name__ == '__main__':
    t = sys.argv[1]; what = sys.argv[2] if len(sys.argv) > 2 else 'all'
    if what in ('front', 'all'):
        do_front(t)
    if what in ('bake', 'all', 'rowbake'):
        import impcx
        cx = 'YJ_' + t
        o, m = bake_objects_row(t) if what == 'rowbake' else bake_objects(t)
        impcx.load_source = lambda name: (o, m)
        sys.argv = ['impcx.py', cx] + sys.argv[3:]
        impcx.main()
