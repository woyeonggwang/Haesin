"""한 건물의 '지붕 모양 상자'(껍데기) 만들기: 지붕 판(윗면 높이장 + 처마 밑면 + 가장자리 띠, 닫힌 판) + 몸통 벽 + 기단 + 툇간 기둥.
좌표 = 데모(단지 프리팹 루트) 좌표, Unity 규칙(삼각형 앞면 = cross(v1-v0, v2-v0) 방향)."""
import numpy as np, time
from scipy import ndimage
from load_bld import load_building
from common import Bld
from geo import (Grid, nearest_fill, slice_segments, seg_mask, close_fill, rectilinear, point_in_poly, poly_area,
                 nearest_on_poly, earclip, simplify)

GS = 0.05          # 분석 격자(m)
SLAB_STEP = 0.25   # 지붕 판 격자(m)
Y0_BELOW = 0.05    # 몸통·기단 바닥 = 가장 낮은 부재 - 이 값
SLAB_ERR = 0.035   # 지붕 판 줄이기 허용 오차(m)


def orient_outward(tris, V, outward):
    """삼각형 (k,3) 들의 앞면이 outward(3,) 쪽이 되게 뒤집음"""
    A = V[tris[:, 0]]; B = V[tris[:, 1]]; C = V[tris[:, 2]]
    n = np.cross(B - A, C - A)
    flip = (n @ outward) < 0
    tris[flip] = tris[flip][:, [0, 2, 1]]
    return tris


class MeshAcc:
    def __init__(self):
        self.V = []; self.F = []; self.part = []; self.n = 0
    def add(self, V, F, part):
        V = np.asarray(V, float); F = np.asarray(F, np.int64)
        self.V.append(V); self.F.append(F + self.n); self.part.append(np.full(len(F), part, np.int32)); self.n += len(V)
    def get(self):
        return np.concatenate(self.V), np.concatenate(self.F), np.concatenate(self.part)


def build(xmin=33.0, xmax=65.0, log=print):
    t0 = time.time()
    b = load_building(xmin=xmin, xmax=xmax); B = Bld(b)
    P = b['P']; T = b['T']
    lo = P.min(0) - 1.0; hi = P.max(0) + 1.0
    G = Grid(lo[0], lo[2], GS, int(np.ceil((hi[0] - lo[0]) / GS)), int(np.ceil((hi[2] - lo[2]) / GS)))
    X, Z = G.centers()
    def cast_grid(y0, dy, tmax):
        O = np.stack([X.ravel(), np.full(X.size, y0), Z.ravel()], 1).astype(np.float32)
        D = np.tile(np.array([0, dy, 0], np.float32), (len(O), 1))
        t, tri, u, v = B.cast(O, D, tmax)
        return np.where(tri >= 0, y0 + dy * t, np.nan).reshape(G.nz, G.nx)
    Htop = cast_grid(hi[1] + 2, -1.0, 100.0)
    # 땅(데모 바닥) 높이: 벽돌·돌담 재질의 가장 낮은 곳 대신 → 건물 밖 둘레에서 위→아래 맞은 높이의 중앙값은 데모 바닥이 빠져 없음
    # → 기단 윗면을 먼저 찾음: 처마 밑(약 +1.2 m)에서 아래로 쏜 광선이 맞는 높이 분포
    ystone = P[T[b['TM'] == [m['name'] for m in b['mats']].index('MI_YCM_Stone')]].reshape(-1, 3)[:, 1] if 'MI_YCM_Stone' in [m['name'] for m in b['mats']] else P[:, 1]
    y_ground = float(np.percentile(P[:, 1], 0.5))
    y_min = float(P[:, 1].min())
    Hmid = cast_grid(y_ground + 1.5, -1.0, 3.0)     # 처마·지붕 아래에서 아래로: 기단 윗면·마루·땅
    roof = np.nan_to_num(Htop, nan=-1e9) > y_ground + 2.2
    # 기단 윗면 = 지붕 밖으로 드러난 것도, 지붕 밑의 것도 포함한 Hmid 의 높은 값 쪽(마루 제외하려고 중앙값)
    hm0 = Hmid[~np.isnan(Hmid)]
    y_sl = float(np.median(hm0[hm0 > y_ground + 0.2])) if (hm0 > y_ground + 0.2).any() else y_ground + 0.5   # 단면 기준(마루 높이)
    log('ground %.2f slice ref %.2f roof cells %d' % (y_ground, y_sl, roof.sum()))
    # 벽 자리: 기단 위 0.4~1.7 m 단면
    wall = np.zeros((G.nz, G.nx), bool)
    for h in np.arange(y_sl + 0.4, y_sl + 1.75, 0.25):
        wall |= seg_mask(G, slice_segments(P, T, h))
    base = np.zeros((G.nz, G.nx), bool)
    for h in (y_sl - 0.35, y_sl - 0.12):
        base |= seg_mask(G, slice_segments(P, T, h))
    wallS = close_fill(wall, 7)                      # 0.35 m 틈(문·창 사이) 메움
    lab, n = ndimage.label(wallS)
    areas = ndimage.sum(np.ones_like(lab), lab, index=np.arange(1, n + 1)) * GS * GS
    main = 1 + int(np.argmax(areas))
    body = lab == main
    pillars = []
    for k in range(1, n + 1):
        if k != main and areas[k - 1] < 0.8:
            rr, cc = np.nonzero(lab == k)
            pillars.append((G.x0 + cc.min() * GS, G.x0 + (cc.max() + 1) * GS, G.z0 + rr.min() * GS, G.z0 + (rr.max() + 1) * GS))
    baseS = close_fill(base | body, 6)
    baseS = ndimage.binary_opening(baseS, structure=np.ones((7, 7), bool)) | body
    ring = baseS & ~ndimage.binary_dilation(body, iterations=2) & ~np.isnan(Hmid)
    hm = Hmid[ring]; hm = hm[hm > y_ground - 0.5]
    y_base = float(np.median(hm)) if len(hm) else y_ground + 0.5
    log('base top (ring median) %.2f' % y_base)
    roofS = close_fill(roof, 4)
    log('body %.1f m2, pillars %d, base %.1f m2, roof %.1f m2' % (body.sum() * GS * GS, len(pillars), baseS.sum() * GS * GS, roofS.sum() * GS * GS))
    # 처마 밑면(아래→위): 몸통·기둥 안쪽은 빼고 바깥에서 채움
    Hbot = cast_grid(y_base + 0.7, 1.0, 8.0)
    pil_mask = np.zeros_like(body)
    for (x0, x1, z0, z1) in pillars:
        pil_mask |= (X >= x0) & (X <= x1) & (Z >= z0) & (Z <= z1)
    excl = ndimage.binary_dilation(body | pil_mask, iterations=3)
    valid_bot = ~np.isnan(Hbot) & ~excl & roofS
    Hbot_f = ndimage.gaussian_filter(nearest_fill(np.nan_to_num(Hbot), valid_bot), 6.0)
    valid_top = ~np.isnan(Htop) & roof
    Htop_f = ndimage.gaussian_filter(nearest_fill(np.nan_to_num(Htop), valid_top), 2.5)
    Hbot_f = np.minimum(Hbot_f, Htop_f - 0.08)
    acc = MeshAcc()
    # ---------- 지붕 판 ----------
    outline = G.contour_poly(roofS, 0.05)
    s = SLAB_STEP
    bx0, bz0 = outline.min(0) - s; bx1, bz1 = outline.max(0) + s
    cx = np.arange(bx0, bx1 + s, s); cz = np.arange(bz0, bz1 + s, s)
    CX, CZ = np.meshgrid(cx[:-1] + s / 2, cz[:-1] + s / 2)
    inside = point_in_poly(CX, CZ, outline)
    ncx = len(cx); vid = -np.ones((len(cz), ncx), np.int64)
    rows, cols = np.nonzero(inside)
    for (r, c) in zip(rows, cols):
        for (rr, cc) in ((r, c), (r + 1, c), (r, c + 1), (r + 1, c + 1)):
            vid[rr, cc] = 1
    vr, vc = np.nonzero(vid > 0)
    vid[vr, vc] = np.arange(len(vr))
    V2 = np.stack([cx[vc], cz[vr]], 1).astype(float)
    tris = []
    for (r, c) in zip(rows, cols):
        v00 = vid[r, c]; v01 = vid[r + 1, c]; v10 = vid[r, c + 1]; v11 = vid[r + 1, c + 1]
        tris.append((v00, v01, v10)); tris.append((v10, v01, v11))     # 위(+y)를 봄
    Ft = np.array(tris, np.int64)
    # 경계 변(한 삼각형에만 속한 변)
    E = np.concatenate([Ft[:, [0, 1]], Ft[:, [1, 2]], Ft[:, [2, 0]]])
    key = np.sort(E, 1); uk, inv, cnt = np.unique(key, axis=0, return_inverse=True, return_counts=True)
    bmask = cnt[inv.reshape(-1)] == 1
    bedges = E[bmask]                                   # 삼각형 감김 방향 그대로
    third = np.concatenate([Ft[:, 2], Ft[:, 0], Ft[:, 1]])[bmask]
    bverts = np.unique(bedges.ravel())
    # 다각형 꼭짓점 → 가장 가까운 경계 정점을 정확히, 나머지 경계 정점은 둘레 위 가장 가까운 점으로
    fixed = {}
    for p in outline:
        d = np.linalg.norm(V2[bverts] - p, axis=1); k = bverts[int(np.argmin(d))]
        if k not in fixed:
            fixed[k] = p
    rest = np.array([k for k in bverts if k not in fixed])
    q, _ = nearest_on_poly(V2[rest], outline)
    V2s = V2.copy(); V2s[rest] = q
    for k, p in fixed.items():
        V2s[k] = p
    ytop = G.sample(Htop_f, V2s[:, 0], V2s[:, 1]); ybot = G.sample(Hbot_f, V2s[:, 0], V2s[:, 1])
    ybot = np.minimum(ybot, ytop - 0.08)
    nv = len(V2s)
    Vt = np.stack([V2s[:, 0], ytop, V2s[:, 1]], 1); Vb = np.stack([V2s[:, 0], ybot, V2s[:, 1]], 1)
    Vs = np.concatenate([Vt, Vb])
    F_top = Ft; F_bot = Ft[:, [0, 2, 1]] + nv
    side = []
    for (a, bb), c in zip(bedges, third):
        e = V2s[bb] - V2s[a]; outward = np.array([e[1], -e[0]])
        if (V2s[c] - V2s[a]) @ outward > 0:
            outward = -outward
        q1 = np.array([[a, bb, bb + nv], [a, bb + nv, a + nv]], np.int64)
        side.append(orient_outward(q1, Vs, np.array([outward[0], 0, outward[1]])))
    Fs = np.concatenate([F_top, F_bot] + side)
    # 퇴화 삼각형 빼기
    A_ = Vs[Fs[:, 0]]; B_ = Vs[Fs[:, 1]]; C_ = Vs[Fs[:, 2]]
    ar = np.linalg.norm(np.cross(B_ - A_, C_ - A_), axis=1)
    Fs = Fs[ar > 1e-8]
    n0 = len(Fs)
    Vs2, Fs2, err = simplify(Vs, Fs, SLAB_ERR)
    log('roof slab tris %d -> %d (err %.3f)' % (n0, len(Fs2), err))
    acc.add(Vs2, Fs2, 0)
    # ---------- 몸통 벽 ----------
    y0 = y_min - Y0_BELOW
    def walls(poly, top_fn, part, step=0.5, tol=0.02):
        poly = np.asarray(poly, float)
        if poly_area(poly) < 0:
            poly = poly[::-1]
        n = len(poly); cnt = 0
        for i in range(n):
            a = poly[i]; c = poly[(i + 1) % n]; L = np.linalg.norm(c - a)
            if L < 1e-6:
                continue
            k = max(1, int(np.ceil(L / step)))
            ts = np.linspace(0, 1, k + 1); pts = a + ts[:, None] * (c - a)
            top = top_fn(pts)
            keep = dp_keep(ts * L, top, tol)
            pts = pts[keep]; top = top[keep]
            d = (c - a) / L; outward = np.array([d[1], 0.0, -d[0]])
            # 반시계(넓이 > 0, x-z 평면) 다각형의 바깥 = 진행 방향의 오른쪽? → 가운데 점으로 확인
            mid = (a + c) / 2 + np.array([outward[0], outward[2]]) * 0.01
            if point_in_poly(np.array([mid[0]]), np.array([mid[1]]), poly)[0]:
                outward = -outward
            V = []; F = []
            for j in range(len(pts)):
                V.append((pts[j][0], y0, pts[j][1])); V.append((pts[j][0], top[j], pts[j][1]))
            V = np.array(V)
            for j in range(len(pts) - 1):
                i0 = 2 * j
                q = np.array([[i0, i0 + 1, i0 + 3], [i0, i0 + 3, i0 + 2]], np.int64)
                F.append(orient_outward(q, V, outward))
            acc.add(V, np.concatenate(F), part); cnt += 2 * (len(pts) - 1)
        return cnt
    def dp_keep(u, y, tol):
        keep = np.zeros(len(u), bool); keep[0] = keep[-1] = True
        st = [(0, len(u) - 1)]
        while st:
            i, j = st.pop()
            if j <= i + 1:
                continue
            t = (u[i + 1:j] - u[i]) / max(u[j] - u[i], 1e-12)
            yl = y[i] + t * (y[j] - y[i]); d = np.abs(y[i + 1:j] - yl); k = int(np.argmax(d))
            if d[k] > tol:
                m = i + 1 + k; keep[m] = True; st.append((i, m)); st.append((m, j))
        return keep
    body_poly = rectilinear(G.contour_poly(body, 0.08), min_len=0.3)
    base_poly = rectilinear(G.contour_poly(baseS, 0.08), min_len=0.6)
    def top_body(pts):
        yb = G.sample(Hbot_f, pts[:, 0], pts[:, 1]); yt = G.sample(Htop_f, pts[:, 0], pts[:, 1])
        return np.minimum(yb + 0.15, yt - 0.05)
    nb = walls(body_poly, top_body, 1)
    nbs = walls(base_poly, lambda pts: np.full(len(pts), y_base), 2, step=100.0)
    # 기단 윗면
    tri = earclip(base_poly)
    Vtop = np.stack([base_poly[:, 0], np.full(len(base_poly), y_base), base_poly[:, 1]], 1)
    tri = orient_outward(tri.copy(), Vtop, np.array([0, 1.0, 0]))
    acc.add(Vtop, tri, 2)
    # 툇간 기둥
    npil = 0
    for (x0, x1, z0, z1) in pillars:
        cxp = (x0 + x1) / 2; czp = (z0 + z1) / 2
        top = float(top_body(np.array([[cxp, czp]]))[0])
        sq = np.array([[x0, z0], [x1, z0], [x1, z1], [x0, z1]])
        yb_save = y0
        def wall_p(poly=sq, top=top):
            nonlocal npil
            poly = np.asarray(poly, float)
            for i in range(4):
                a = poly[i]; c = poly[(i + 1) % 4]; d = (c - a) / np.linalg.norm(c - a)
                outward = np.array([d[1], 0.0, -d[0]])
                mid = (a + c) / 2 + np.array([outward[0], outward[2]]) * 0.001
                if point_in_poly(np.array([mid[0]]), np.array([mid[1]]), poly)[0]:
                    outward = -outward
                V = np.array([(a[0], y_base, a[1]), (a[0], top, a[1]), (c[0], y_base, c[1]), (c[0], top, c[1])])
                F = orient_outward(np.array([[0, 1, 3], [0, 3, 2]], np.int64), V, outward)
                acc.add(V, F, 3); npil += 2
        wall_p()
    V, F, part = acc.get()
    log('body walls %d, base %d + top %d, pillars %d, total %d tris (%.1f s)' % (nb, nbs, len(tri), npil, len(F), time.time() - t0))
    info = dict(y_ground=y_ground, y_base=y_base, body_poly=body_poly.tolist(), base_poly=base_poly.tolist(), outline=outline.tolist(), pillars=pillars)
    return V, F, part, info, b, B


if __name__ == '__main__':
    import json
    V, F, part, info, b, B = build()
    np.savez('hull.npz', V=V, F=F, part=part)
    json.dump(info, open('hull_info.json', 'w'), indent=1)
    print('body poly', np.round(info['body_poly'], 2).tolist())
    print('base poly', np.round(info['base_poly'], 2).tolist())
    print('pillars', np.round(info['pillars'], 2).tolist())
