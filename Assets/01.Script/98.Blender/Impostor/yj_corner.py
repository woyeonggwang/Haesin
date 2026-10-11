"""육조거리 행랑 ㄱ자 모서리(사용자 지시 2026-09-30 "모서리 근정전 행각모서리나 흥례문 모서리 행각처럼 해주세요").

사정전 원래 모서리(줄 끝 END 조각)는 궁에서 다른 행각에 붙던 자리라 옆 행각 앞머리가 열려 서까래가 드러남 →
같은 두 칸 조각(BAY)을 45° 로 잘라 맞댄 ㄱ자 모서리를 새로 만든다(같은 단면 두 날개를 대각선 면에서 맞대면 바깥은 추녀, 안쪽은 골).
모서리 조각 좌표(BAY 와 같은 뜻: x = 길 따라, y = 땅 위, z = 처마 앞 끝부터 안쪽):
  줄 날개(A): x 0(이음 — BAY 끝과 기와 맞음) ~ K = 11.475, 바깥 처마 z = 0
  옆 날개(B): x ∈ [K - 9.88, K](바깥 처마 x = K), z 0 ~ 14.5(이음 — BAY 시작과 기와 맞음)
  바깥 모서리 기둥: 줄 날개 앞 기둥 줄(z 2.35) 에서 x = K - 2.35 에 기둥(두 날개가 반씩 — 이어 붙이면 기둥 하나)
LOD0/1 = BAY LOD0/1 로 만듦 + 추녀마루(BAY 용마루 한 토막을 대각선을 따라 기와 윗면 높이로 내린 것, 폭·높이 0.8 배, 아래 끝 막음),
LOD2 = 임포스터 ROW_BAY 껍데기로 만듦(UV 그대로 — 페이지 재질 IMP_YJ_A_P<k>).
python3 yj_corner.py → out/YJR/YJR_Corner.bin (YJR_CORNER_LOD0/1/2)
"""
import os, sys, struct
import numpy as np
import common
import yj_row as R
from export import write_meshes

L_BAY = R.XB1 - R.XG1          # 6.075
DEPTH = 9.88                   # 처마 앞 끝 ~ 뒤 끝
COL_F = 2.35                   # 앞 기둥 줄(조각 z)
K = round(9.125 + COL_F, 4)    # 11.475
SIDE_OFF = COL_F               # 옆 날개: BAY u = 0 기둥이 모서리 기둥(z = 2.35)에 오게
SIDE_END = SIDE_OFF + 2 * L_BAY   # 14.5


def read_hnms(path):
    out = {}
    with open(path, 'rb') as f:
        magic, n = struct.unpack('<ii', f.read(8))
        for _ in range(n):
            ln = struct.unpack('<i', f.read(4))[0]; name = f.read(ln).decode('utf-8')
            nv, ns = struct.unpack('<ii', f.read(8))
            pos = np.frombuffer(f.read(nv * 12), '<f4').reshape(-1, 3).copy()
            nrm = np.frombuffer(f.read(nv * 12), '<f4').reshape(-1, 3).copy()
            uv = np.frombuffer(f.read(nv * 8), '<f4').reshape(-1, 2).copy()
            subs = []; mats = []
            for _ in range(ns):
                l2 = struct.unpack('<i', f.read(4))[0]; mats.append(f.read(l2).decode('utf-8'))
                ni = struct.unpack('<i', f.read(4))[0]; subs.append(np.frombuffer(f.read(ni * 4), '<i4').copy())
            out[name] = dict(pos=pos, nrm=nrm, uv=uv, subs=subs, mats=mats)
    return out


def clip_keep(m, d):
    """메시 m 에서 d(정점별 부호 값) < 0 쪽만 남김 — 걸친 삼각형은 잘라 새 정점(보간)"""
    P, N, U = m['pos'].astype(np.float64), m['nrm'].astype(np.float64), m['uv'].astype(np.float64)
    NP = [P]; NN = [N]; NU = [U]; nv = len(P)
    subs = []
    for s in m['subs']:
        t = np.asarray(s, np.int64).reshape(-1, 3)
        dt = d[t]
        keep = (dt < 0).all(1); cross = ~keep & ~(dt >= 0).all(1)
        out = [t[keep]]
        for tri in t[cross]:
            ring = []
            for a in range(3):
                i0, i1 = tri[a], tri[(a + 1) % 3]; d0, d1 = d[i0], d[i1]
                if d0 < 0:
                    ring.append(i0)
                if (d0 < 0) != (d1 < 0):
                    tt = d0 / (d0 - d1)
                    NP.append((P[i0] * (1 - tt) + P[i1] * tt)[None]); nn = N[i0] * (1 - tt) + N[i1] * tt
                    NN.append((nn / max(np.linalg.norm(nn), 1e-12))[None]); NU.append((U[i0] * (1 - tt) + U[i1] * tt)[None])
                    ring.append(nv); nv += 1
            for q in range(1, len(ring) - 1):
                out.append(np.array([[ring[0], ring[q], ring[q + 1]]]))
        subs.append(np.concatenate(out).reshape(-1))
    return dict(pos=np.concatenate(NP), nrm=np.concatenate(NN), uv=np.concatenate(NU), subs=subs, mats=list(m['mats']))


def xform(m, fn_pos, fn_dir, flip=False):
    P = fn_pos(m['pos'].astype(np.float64)); N = fn_dir(m['nrm'].astype(np.float64))
    subs = [np.asarray(s).reshape(-1, 3)[:, [0, 2, 1]].reshape(-1) if flip else np.asarray(s) for s in m['subs']]
    return dict(pos=P, nrm=N, uv=m['uv'].astype(np.float64), subs=subs, mats=list(m['mats']))


def concat(ms):
    """같은 재질 이름끼리 서브메시를 합침"""
    P = []; N = []; U = []; by = {}; base = 0
    for m in ms:
        P.append(m['pos']); N.append(m['nrm']); U.append(m['uv'])
        for s, mn in zip(m['subs'], m['mats']):
            by.setdefault(mn, []).append(np.asarray(s, np.int64) + base)
        base += len(m['pos'])
    names = sorted(by)
    return dict(pos=np.concatenate(P), nrm=np.concatenate(N), uv=np.concatenate(U), subs=[np.concatenate(by[n]) for n in names], mats=names)


def compact(m):
    used = np.unique(np.concatenate([np.asarray(s) for s in m['subs']]))
    remap = np.full(len(m['pos']), -1, np.int64); remap[used] = np.arange(len(used))
    keep = [k for k, s in enumerate(m['subs']) if len(s)]
    return dict(pos=m['pos'][used].astype(np.float32), nrm=m['nrm'][used].astype(np.float32), uv=m['uv'][used].astype(np.float32),
                subs=[remap[np.asarray(m['subs'][k])].astype(np.int32) for k in keep], mats=[m['mats'][k] for k in keep])


def shift(m, dx):
    return xform(m, lambda p: p + np.array([dx, 0, 0]), lambda n: n)


RIDGE_MAT = 'MI_SJJ_HG_Yongmaru'
ROOF_MATS = ('MI_SJJ_HG_Roof01', 'MI_SJJ_HG_Roof01_Shell')
HIP_ZE = 0.35          # 추녀마루 아래 끝(처마 앞 끝에서 안쪽 거리)
HIP_SW, HIP_SH = 0.8, 0.8   # 용마루 단면에 곱할 폭·높이(추녀마루는 용마루보다 작게)


def roof_profile(bay, zs):
    """기와 윗면 높이(마루 방향 x 여러 곳에서 위→아래로 잰 가장 높은 곳) — z 마다"""
    P = bay['pos']; T = []
    for s, mn in zip(bay['subs'], bay['mats']):
        if mn in ROOF_MATS:
            T.append(np.asarray(s, np.int64).reshape(-1, 3))
    T = np.concatenate(T)
    A, B, Cc = P[T[:, 0]], P[T[:, 1]], P[T[:, 2]]
    out = np.full(len(zs), -1e9)
    for x in np.arange(0.2, L_BAY - 0.1, 0.15):
        for i, z in enumerate(zs):
            # xz 에서 (x, z) 를 품는 삼각형의 높이
            v0 = Cc[:, [0, 2]] - A[:, [0, 2]]; v1 = B[:, [0, 2]] - A[:, [0, 2]]; v2 = np.array([x, z]) - A[:, [0, 2]]
            d00 = (v0 * v0).sum(1); d01 = (v0 * v1).sum(1); d11 = (v1 * v1).sum(1); d20 = (v2 * v0).sum(1); d21 = (v2 * v1).sum(1)
            den = d00 * d11 - d01 * d01; ok = np.abs(den) > 1e-12
            den = np.where(ok, den, 1)
            a = (d11 * d20 - d01 * d21) / den; b = (d00 * d21 - d01 * d20) / den
            ins = ok & (a >= -1e-6) & (b >= -1e-6) & (a + b <= 1 + 1e-6)
            if ins.any():
                y = A[ins, 1] + a[ins] * (Cc[ins, 1] - A[ins, 1]) + b[ins] * (B[ins, 1] - A[ins, 1])
                out[i] = max(out[i], y.max())
    return out


def hip_ridge(bay):
    """BAY 의 용마루 한 토막(길이 6.075)을 추녀마루로 — 줄·옆 날개 용마루가 만나는 점에서 바깥 모서리 처마 쪽으로,
    기와 윗면 높이(roof_profile)를 따라 내림. 아래 끝은 단면을 막음."""
    k = bay['mats'].index(RIDGE_MAT)
    s = np.asarray(bay['subs'][k], np.int64)
    used, inv = np.unique(s, return_inverse=True)
    P = bay['pos'][used].astype(np.float64); N = bay['nrm'][used].astype(np.float64); U = bay['uv'][used].astype(np.float64)
    t = inv.reshape(-1, 3)
    zc = 0.5 * (P[:, 2].min() + P[:, 2].max())
    zs = np.linspace(HIP_ZE, zc, 40)
    prof = roof_profile(bay, zs)
    ytop = prof[-1]
    u = np.clip(P[:, 0] / L_BAY, 0, 1)
    zh = zc - u * (zc - HIP_ZE)
    yh = np.interp(zh, zs, prof)
    c = (P[:, 2] - zc) * HIP_SW
    perp = np.array([1.0, 0.0, 1.0]) / np.sqrt(2)
    Q = np.stack([K - zh, yh + (P[:, 1] - ytop) * HIP_SH, zh], 1) + c[:, None] * perp
    # 법선: 단면 방향(z) 성분은 perp 로, 길이 방향(x) 성분은 추녀 방향으로 — 대략
    dirh = np.array([1.0, 0.0, -1.0]) / np.sqrt(2)
    Nn = N[:, 0:1] * dirh + N[:, 1:2] * np.array([0, 1.0, 0]) + N[:, 2:3] * perp
    Nn /= np.maximum(np.linalg.norm(Nn, axis=1, keepdims=True), 1e-12)
    # 감김: (x, z) → (along, perp) 는 (1,0)->(0.707,-0.707)... 행렬식 확인
    M = np.stack([dirh, np.array([0, 1.0, 0]), perp], 1)   # 열 = 원래 x, y, z 축이 가는 곳
    if np.linalg.det(M) < 0:
        t = t[:, [0, 2, 1]]
    # 아래 끝 막기
    end = np.where(P[:, 0] > L_BAY - 2e-3)[0]
    capT = []
    if len(end) >= 3:
        from scipy.spatial import ConvexHull
        q2 = np.stack([Q[end] @ perp, Q[end, 1]], 1)
        try:
            h = ConvexHull(q2).vertices
            cen = len(Q)
            Q = np.concatenate([Q, Q[end[h]].mean(0)[None]]); Nn = np.concatenate([Nn, dirh[None]]); U = np.concatenate([U, U[end[h]].mean(0)[None]])
            for a in range(len(h)):
                i0, i1 = end[h[a]], end[h[(a + 1) % len(h)]]
                tri = [cen, i0, i1]
                nrm = np.cross(Q[i0] - Q[cen], Q[i1] - Q[cen])
                if nrm @ dirh < 0:
                    tri = [cen, i1, i0]
                capT.append(tri)
        except Exception as e:
            print('cap fail', e)
    tt = np.concatenate([t] + ([np.array(capT)] if capT else []))
    return dict(pos=Q, nrm=Nn, uv=U, subs=[tt.reshape(-1)], mats=[RIDGE_MAT])


def build_corner(bay, hip=None):
    """bay = BAY 조각 메시(u 0 ~ 6.075). 줄 날개 + 옆 날개(돌림)를 대각선 x + z = K 에서 맞댐."""
    row = concat([shift(bay, 0.0), shift(bay, L_BAY)])                        # x 0 ~ 12.15
    row = clip_keep(row, row['pos'][:, 0] + row['pos'][:, 2] - K)            # x + z <= K
    side0 = concat([shift(bay, -L_BAY), shift(bay, 0.0), shift(bay, L_BAY)])  # u -6.075 ~ 12.15
    # (u, v) → (x, z) = (K - v, u + SIDE_OFF): 돌림(행렬식 +1) — 앞 처마(v 0)가 바깥 x = K 를 봄
    side = xform(side0, lambda p: np.stack([K - p[:, 2], p[:, 1], p[:, 0] + SIDE_OFF], 1), lambda n: np.stack([-n[:, 2], n[:, 1], n[:, 0]], 1))
    side = clip_keep(side, -(side['pos'][:, 0] + side['pos'][:, 2] - K))    # x + z >= K
    zc = side['pos'][:, 2]
    side = clip_keep(side, zc - SIDE_END)                                     # z <= 14.5
    return compact(concat([row, side] + ([hip] if hip is not None else [])))


def tris(m):
    return int(sum(len(s) for s in m['subs']) // 3)


if __name__ == '__main__':
    from impcx import OUTROOT
    parts, mats = R.row_parts()
    bay0 = R.merge(parts['BAY'], mats, R.XG1)
    bay1 = R.lod(bay0, [0.0, L_BAY], 1)
    hul = read_hnms(os.path.join(OUTROOT, 'YJ_A', 'YJ_A_Imp.bin'))['ROW_BAY_Imp']
    hul = xform(hul, lambda p: p - np.array([R.XG1, R.GROUND, R.ZF]), lambda n: n)
    out = []
    b0 = dict(bay0, pos=np.asarray(bay0['pos'], np.float64), subs=[np.asarray(s) for s in bay0['subs']])
    hr = hip_ridge(b0)
    print('hip ridge tris', tris(hr))
    for i, b in enumerate((bay0, bay1, hul)):
        b = dict(b, pos=np.asarray(b['pos'], np.float64), subs=[np.asarray(s) for s in b['subs']])
        c = build_corner(b, hr if i < 2 else None)
        out.append(('YJR_CORNER_LOD%d' % i, c))
        print('CORNER LOD%d tris %d  bounds %s %s' % (i, tris(c), c['pos'].min(0).round(2), c['pos'].max(0).round(2)))
    write_meshes(os.path.join(OUTROOT, 'YJR', 'YJR_Corner.bin'), out)
    print('K', K, 'SIDE_END', SIDE_END)
