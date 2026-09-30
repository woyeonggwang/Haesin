"""흥례문 부분 나누기 + 부분별 LOD0 메시 만들기(보이는 삼각형만).

부분: 흥례문 본체(GATE), 행각 토막 10개(SW1·SW2·W1~W3·SE1·SE2·E1·E2), 유화문(YHM), 기별청(GBC), 바닥·영제교(GROUND).
- 삼각형마다 무게중심 xz 로 부분 상자에 넣는다(긴 지붕판은 토막 경계에서 삼각형 단위로 갈림).
- 바닥·영제교 쪽 메시(이름 목록)는 위치와 상관없이 GROUND.
- 계단(STAIR)은 뺀다. 문짝(Door_*, 27짝)은 여는 문짝 프리팹(DoorsCard)으로 따로 가므로 뺀다.
- 거울 배치(행렬식 < 0)는 삼각형 감김을 뒤집는다(합친 메시는 변환이 없으므로).
"""
import sys, os, re, json
import numpy as np
_HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, _HERE)
from load import load, xform
from scene import instance_mats, card_info
from voxfield import STAIR

GROUND_NAMES = {'SM_HNM_Ground', 'SM_HNM_Road', 'SM_HNM_Fond', 'SM_HNM_Bridge', 'SM_HNM_BridgePiler', 'SM_HNM_Dragon',
                'SM_HNM_HayebdongjaStone', 'SM_HNM_Handrail_A', 'SM_HNM_Handrail_B', 'SM_HNM_StonePillar', 'SM_HNM_Grating'}
DOOR_RE = re.compile(r'^SM_HNM_Door_[A-I](_\d)?$')

# (이름, x0, x1, z0, z1) — 데모(단지 프리팹) 좌표
BOXES = [
    ('GATE', -15.3, 14.3, -9.0, 9.0),
    ('HG_SW1', -38.5, -15.3, -9.0, 9.0),
    ('HG_SW2', -70.0, -38.5, -9.0, 9.0),
    ('HG_W1', -70.0, -45.0, -31.0, -9.0),
    ('HG_W2', -70.0, -45.0, -57.0, -31.0),
    ('HG_W3', -70.0, -45.0, -95.0, -57.0),
    ('HG_SE1', 14.3, 37.5, -9.0, 9.0),
    ('HG_SE2', 37.5, 70.0, -9.0, 9.0),
    ('HG_E1', 45.0, 70.0, -31.0, -9.0),
    ('HG_E2', 45.0, 70.0, -57.0, -31.0),
    ('YHM', 40.0, 70.0, -77.8, -57.0),
    ('GBC', 40.0, 70.0, -95.0, -77.8),
]
PART_NAMES = [b[0] for b in BOXES] + ['GROUND']


def part_of_points(C):
    """무게중심(N,3) → 부분 번호(상자에 없으면 GROUND)."""
    out = np.full(len(C), len(BOXES), np.int32)
    for k, (nm, x0, x1, z0, z1) in enumerate(BOXES):
        m = (out == len(BOXES)) & (C[:, 0] >= x0) & (C[:, 0] < x1) & (C[:, 2] >= z0) & (C[:, 2] < z1)
        out[m] = k
    return out


def load_vis(inst, red, names, workdir):
    vis = np.load(os.path.join(workdir, 'vis_keep.npy'))
    meta = np.load(os.path.join(workdir, 'vis_scene_meta.npz'))
    TI, TS = meta['TI'], meta['TS']
    key = TI.astype(np.int64) * 64 + TS
    # 장면은 배치 순서·서브메시 순서로 쌓였으므로 key 는 구간별로 연속
    change = np.concatenate([[True], key[1:] != key[:-1]])
    starts = np.where(change)[0]
    ends = np.concatenate([starts[1:], [len(key)]])
    keep = {}
    for s, e in zip(starts, ends):
        keep[(int(TI[s]), int(TS[s]))] = vis[s:e]
    return keep


def collect(inst, mats, red, keep=None, use_vis=True):
    """부분별 조각 목록: dict(part -> [chunk]), chunk = (pos(N,3) f64 월드, nrm(N,3), uv(N,2), tris(M,3), 재질 이름, flat, 배치 번호, 서브메시)."""
    names = [red[i['k']]['name'].split('#')[0] for i in inst]
    parts = {p: [] for p in PART_NAMES}
    for ii, i in enumerate(inst):
        nm = names[ii]
        if nm in STAIR or DOOR_RE.match(nm):
            continue
        d = red[i['k']]
        flat = d['name'].endswith('#F') or d['name'].endswith('#CARD')
        M = i['M']
        W = xform(M, d['pos'].astype(np.float64))
        N = d['nrm'].astype(np.float64) @ M[:3, :3].T
        l = np.linalg.norm(N, axis=1); N = N / np.maximum(l, 1e-12)[:, None]
        mir = np.linalg.det(M[:3, :3]) < 0
        mnames = instance_mats(i, d)
        for s, idx in enumerate(d['subs']):
            t = idx.reshape(-1, 3).astype(np.int64)
            if len(t) == 0:
                continue
            if use_vis and keep is not None and nm != 'SM_HNM_Ground':
                kv = keep.get((ii, s))
                if kv is None or len(kv) != len(t):
                    raise RuntimeError('vis mismatch %d %s sub %d' % (ii, nm, s))
                t = t[kv]
                if len(t) == 0:
                    continue
            if mir:
                t = t[:, [0, 2, 1]]
            mn = mnames[s] if s < len(mnames) else ''
            if nm in GROUND_NAMES:
                pidx = np.full(len(t), len(BOXES), np.int32)
            else:
                C = W[t].mean(1)
                pidx = part_of_points(C)
            for p in np.unique(pidx):
                tt = t[pidx == p]
                parts[PART_NAMES[p]].append((W, N, d['uv'], tt, mn, flat, ii, s))
    return parts


def merge_chunks(chunks, drop_mats=()):
    """조각들을 재질·음영(각짐/부드러움)별 서브메시로 합침 → (pos, nrm, uv, subs, mats, flats). 정점은 조각마다 따로(쓰는 것만)."""
    groups = {}
    for (W, N, UV, tt, mn, flat, ii, s) in chunks:
        if mn in drop_mats:
            continue
        groups.setdefault((mn, flat), []).append((W, N, UV, tt))
    P = []; NN = []; U = []; subs = []; mats = []; flats = []; base = 0
    for (mn, flat), lst in sorted(groups.items(), key=lambda kv: (kv[0][0], kv[0][1])):
        idx = []
        for (W, N, UV, tt) in lst:
            used, inv = np.unique(tt.ravel(), return_inverse=True)
            P.append(W[used]); NN.append(N[used]); U.append(UV[used])
            idx.append(inv.reshape(-1).astype(np.int64) + base); base += len(used)
        subs.append(np.concatenate(idx)); mats.append(mn); flats.append(flat)
    if not P:
        return None
    return dict(pos=np.concatenate(P), nrm=np.concatenate(NN), uv=np.concatenate(U).astype(np.float64), subs=subs, mats=mats, flats=flats)


def tri_count(m):
    return 0 if m is None else sum(len(s) for s in m['subs']) // 3
