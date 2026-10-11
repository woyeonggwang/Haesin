"""일반화한 '지붕 모양 상자'(임포스터 껍데기) 만들기 — 건물 여러 채·안마당(구멍)·ㄷ/ㅁ자 행각을 한 오브젝트에서 처리.

입력: 한 오브젝트의 삼각형(단지 프리팹 루트 좌표) + (선택) 문·창 판 삼각형(단면 마스크의 문 구멍 메우기용).
만드는 것(모두 닫힌 판, 격자 → 가장자리를 윤곽선에 붙임 → meshoptimizer 로 줄임):
  - 지붕 판: 윗면 = 위→아래 광선 높이(가우스 0.125 m), 밑면 = 아래→위 광선(처마 밑, 몸통 안쪽은 바깥 값으로 채움)
  - 몸통: 기단 위 0.4~1.75 m 단면(벽·기둥·창)을 닫고, 지붕에 덮인 구멍(방)은 채우고 덮이지 않은 구멍(안마당)은 남김.
          위끝 = 처마 밑면 + 0.15 m(지붕 판 속), 지붕 밖이면 실제 윗면 높이
  - 기단: 기단 윗면 바로 아래 단면, 윗면 높이 = 덩어리마다 기단 위 광선 높이의 중앙값
  - 툇간 기둥: 지붕 아래 작은 단면 덩어리 → 네모 기둥
좌표계 = Unity(왼손), 삼각형 앞면 = cross(v1-v0, v2-v0) 방향."""
import numpy as np, time
from scipy import ndimage
import common
from rayeval import RMesh
from geo import Grid, nearest_fill, slice_segments, seg_mask, simplify, nearest_on_poly
from skimage import measure

GS = 0.05
STEP = 0.25
SLAB_ERR = 0.05
BASE_REACH = 5.0          # 기단·계단으로 볼 범위(몸통에서 m)
BELOW_GROUND = 0.6        # 판 밑바닥 = 땅 − 이것
ROOF_ABOVE_GROUND = 2.2   # 땅에서 이보다 높은 윗면 = 지붕(행각처럼 낮은 지붕도 잡히게 — 오브젝트 최고 높이 비율로 하면 한 오브젝트 안 낮은 행각 지붕을 놓침)


def fill_holes_if(m, keep_fn):
    """m 의 구멍(테두리와 안 닿은 배경 덩어리) 중 keep_fn(구멍 마스크) 가 False 인 것만 채움"""
    bg = ~m
    lab, n = ndimage.label(bg)
    border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]])))
    out = m.copy()
    for k in range(1, n + 1):
        if k in border:
            continue
        h = lab == k
        if not keep_fn(h):
            out |= h
    return out


def closing(m, r):
    st = np.ones((2 * r + 1, 2 * r + 1), bool); p = r + 2
    return ndimage.binary_closing(np.pad(m, p), structure=st)[p:-p, p:-p]


def contours(G, m, tol):
    out = []
    for c in measure.find_contours(np.pad(m.astype(float), 1), 0.5):
        c = c - 1
        if len(c) < 4:
            continue
        ap = measure.approximate_polygon(c, tolerance=tol / G.g)
        xy = np.stack([G.x0 + (ap[:, 1] + 0.5) * G.g, G.z0 + (ap[:, 0] + 0.5) * G.g], 1)
        if np.allclose(xy[0], xy[-1]):
            xy = xy[:-1]
        if len(xy) >= 3:
            out.append(xy)
    return out


def orient(tris, V, outward):
    A = V[tris[:, 0]]; B = V[tris[:, 1]]; C = V[tris[:, 2]]
    n = np.cross(B - A, C - A)
    flip = (n * outward).sum(-1) < 0 if outward.ndim == 2 else (n @ outward) < 0
    tris[flip] = tris[flip][:, [0, 2, 1]]
    return tris


def grid_slab(G, mask, top_fn, bot_fn, step=STEP, tol=0.05, err=SLAB_ERR, min_thick=0.05):
    """mask(분석 격자) 모양의 닫힌 판. top_fn/bot_fn(x, z) → 높이. 반환 V, F(줄인 것)"""
    rr, cc = np.nonzero(mask)
    if len(rr) == 0:
        return None
    x0 = G.x0 + cc.min() * G.g - step; x1 = G.x0 + (cc.max() + 1) * G.g + step
    z0 = G.z0 + rr.min() * G.g - step; z1 = G.z0 + (rr.max() + 1) * G.g + step
    cx = np.arange(x0, x1 + step, step); cz = np.arange(z0, z1 + step, step)
    CX, CZ = np.meshgrid(cx[:-1] + step / 2, cz[:-1] + step / 2)
    ci = ((CX - G.x0) / G.g).astype(int); ri = ((CZ - G.z0) / G.g).astype(int)
    ok = (ci >= 0) & (ci < G.nx) & (ri >= 0) & (ri < G.nz)
    inside = np.zeros(CX.shape, bool)
    inside[ok] = mask[ri[ok], ci[ok]]
    rows, cols = np.nonzero(inside)
    if len(rows) == 0:
        return None
    vid = -np.ones((len(cz), len(cx)), np.int64)
    for dr, dc in ((0, 0), (1, 0), (0, 1), (1, 1)):
        vid[rows + dr, cols + dc] = 1
    vr, vc = np.nonzero(vid > 0)
    vid[vr, vc] = np.arange(len(vr))
    V2 = np.stack([cx[vc], cz[vr]], 1).astype(float)
    v00 = vid[rows, cols]; v01 = vid[rows + 1, cols]; v10 = vid[rows, cols + 1]; v11 = vid[rows + 1, cols + 1]
    Ft = np.concatenate([np.stack([v00, v01, v10], 1), np.stack([v10, v01, v11], 1)])
    E = np.concatenate([Ft[:, [0, 1]], Ft[:, [1, 2]], Ft[:, [2, 0]]])
    third = np.concatenate([Ft[:, 2], Ft[:, 0], Ft[:, 1]])
    key = np.sort(E, 1); _, inv, cnt = np.unique(key, axis=0, return_inverse=True, return_counts=True)
    bm = cnt[inv.reshape(-1)] == 1
    bedges = E[bm]; bthird = third[bm]
    bverts = np.unique(bedges.ravel())
    # 윤곽선에 붙이기
    polys = contours(G, mask, tol)
    if polys:
        segs_a = np.concatenate([p for p in polys]); segs_b = np.concatenate([np.roll(p, -1, 0) for p in polys])
        corners = segs_a
        fixed = {}
        for p in corners:
            d = np.linalg.norm(V2[bverts] - p, axis=1); k = bverts[int(np.argmin(d))]
            if d.min() < step * 1.5 and k not in fixed:
                fixed[k] = p
        rest = np.array([k for k in bverts if k not in fixed], np.int64)
        if len(rest):
            best = V2[rest].copy(); bd = np.full(len(rest), np.inf)
            for a, b in zip(segs_a, segs_b):
                d = b - a; L2 = max(d @ d, 1e-12)
                t = np.clip(((V2[rest] - a) @ d) / L2, 0, 1)
                q = a + t[:, None] * d; dd = np.linalg.norm(V2[rest] - q, axis=1)
                s = dd < bd; best[s] = q[s]; bd[s] = dd[s]
            mv = bd < step * 1.2
            V2[rest[mv]] = best[mv]
        for k, p in fixed.items():
            V2[k] = p
    yt = top_fn(V2[:, 0], V2[:, 1]); yb = bot_fn(V2[:, 0], V2[:, 1])
    yb = np.minimum(yb, yt - min_thick)
    nv = len(V2)
    Vs = np.concatenate([np.stack([V2[:, 0], yt, V2[:, 1]], 1), np.stack([V2[:, 0], yb, V2[:, 1]], 1)])
    F_top = Ft; F_bot = Ft[:, [0, 2, 1]] + nv
    # 옆: 경계 변마다 사각형, 바깥 = 세 번째 점 반대쪽
    a = bedges[:, 0]; b = bedges[:, 1]
    e = V2[b] - V2[a]; out2 = np.stack([e[:, 1], -e[:, 0]], 1)
    flip = ((V2[bthird] - V2[a]) * out2).sum(1) > 0
    out2[flip] *= -1
    q1 = np.stack([a, b, b + nv], 1); q2 = np.stack([a, b + nv, a + nv], 1)
    out3 = np.stack([out2[:, 0], np.zeros(len(out2)), out2[:, 1]], 1)
    q1 = orient(q1.copy(), Vs, out3); q2 = orient(q2.copy(), Vs, out3)
    Fs = np.concatenate([F_top, F_bot, q1, q2])
    A_ = Vs[Fs[:, 0]]; B_ = Vs[Fs[:, 1]]; C_ = Vs[Fs[:, 2]]
    ar = np.linalg.norm(np.cross(B_ - A_, C_ - A_), axis=1)
    Fs = Fs[ar > 1e-9]
    V3, F3, e_ = simplify(Vs, Fs, err)
    return V3, F3


def build_hull(Pb, Tb, extra=None, log=print, name='', wall=None, ctx=None):
    """wall=None 이면 자동: 높이(최고 − 최저)가 3 m 안 되거나 지붕(땅 + 2.2 m 위 윗면)이 없으면 담장 방식 —
    땅 = 최저점, 기와 머리(윗면이 높이의 55 % 위) = 지붕, 단면은 땅 위 0.15 m ~ 기와 머리 아래 0.1 m, 기단 없음."""
    t0 = time.time()
    if wall is None:
        wall = (Pb[:, 1].max() - Pb[:, 1].min()) < 3.0
        if not wall:
            r = build_hull(Pb, Tb, extra, log, name, wall=False, ctx=ctx)
            return r if r is not None else build_hull(Pb, Tb, extra, log, name, wall=True, ctx=ctx)
    if extra is not None and len(extra[1]):
        P = np.concatenate([Pb, extra[0]]); T = np.concatenate([Tb, extra[1] + len(Pb)])
    else:
        P, T = Pb, Tb
    rm = RMesh(P, T, np.zeros((len(P), 2)), np.zeros(len(T), np.int32))
    lo = Pb.min(0) - 1.0; hi = Pb.max(0) + 1.0
    G = Grid(lo[0], lo[2], GS, int(np.ceil((hi[0] - lo[0]) / GS)), int(np.ceil((hi[2] - lo[2]) / GS)))
    X, Z = G.centers()
    def cast(y0, dy, tmax):
        y0 = np.broadcast_to(np.asarray(y0, np.float64), X.shape).ravel()
        O = np.stack([X.ravel(), y0, Z.ravel()], 1).astype(np.float32)
        D = np.tile(np.array([0, dy, 0], np.float32), (len(O), 1))
        t, tri, u, v = rm.cast(O, D, tmax)
        return np.where(tri >= 0, y0 + dy * t, np.nan).reshape(G.nz, G.nx)
    y_ground = float(np.percentile(Pb[:, 1], 0.5)); y_min = float(Pb[:, 1].min()); y_max = float(Pb[:, 1].max())
    Htop = cast(hi[1] + 2, -1.0, 200.0)
    Hmid = cast(y_ground + 2.5, -1.0, 4.0)
    if wall:
        y_ground = y_min; y_sl = y_min
        roof_th = y_min + 0.55 * (y_max - y_min)
        sl_lo, sl_hi = y_min + 0.15, roof_th - 0.1
    else:
        hm = Hmid[~np.isnan(Hmid)]; hm = hm[(hm > y_ground + 0.15) & (hm < y_ground + 2.0)]   # 문 위 인방 등(땅 + 2 m 위)은 바닥으로 안 봄
        y_sl = float(np.median(hm)) if len(hm) else y_ground + 0.4
        roof_th = y_ground + ROOF_ABOVE_GROUND
        sl_lo, sl_hi = y_ground + 0.4, y_ground + 2.0 + 1.76
    # 칸마다 바닥 높이(한 오브젝트에 높이가 다른 건물이 여럿 — 교태전 본채 기단 1.4 m, 행각 0.5 m 등): 0.25 m 칸 7×7 중앙값
    if wall:
        Hfloor = np.full((G.nz, G.nx), y_min)
    else:
        hf = np.where((Hmid > y_ground - 0.1) & (Hmid < y_ground + 2.0), Hmid, np.nan)
        k = 5; nz5 = G.nz // k * k; nx5 = G.nx // k * k
        blk = hf[:nz5, :nx5].reshape(nz5 // k, k, nx5 // k, k)
        with np.errstate(all='ignore'):
            import warnings
            warnings.simplefilter('ignore')
            low = np.nanmedian(blk, axis=(1, 3))
        ok = ~np.isnan(low)
        low = ndimage.median_filter(np.where(ok, low, np.nan_to_num(low, nan=y_ground)), size=7) if ok.any() else np.full(low.shape, y_ground)
        low = nearest_fill(low, ok) if ok.any() else low
        Hfloor = np.repeat(np.repeat(low, k, 0), k, 1)
        Hfloor = np.pad(Hfloor, ((0, G.nz - nz5), (0, G.nx - nx5)), mode='edge')
    roof = np.nan_to_num(Htop, nan=-1e9) > roof_th
    roof = closing(roof, 4)
    roof = fill_holes_if(roof, lambda h: h.sum() * GS * GS > 4.0)          # 4 m² 넘는 구멍(안마당)만 남김
    def wall_slices(PP, TT):
        wm = np.zeros((G.nz, G.nx), bool)
        for hh in np.arange(sl_lo, max(sl_hi, sl_lo + 0.01), 0.25):
            segs = slice_segments(PP, TT, hh)
            if len(segs) == 0:
                continue
            sm = seg_mask(G, segs)
            if not wall:
                rel = hh - Hfloor
                sm &= (rel >= 0.4) & (rel <= 1.76)
            wm |= sm
        return wm
    wmask = wall_slices(P, T)
    wallc = closing(wmask, 7)
    covered = lambda h: (roof[h].mean() > 0.5)
    body_all = fill_holes_if(wallc, lambda h: not covered(h))          # 지붕 덮인 구멍(방)은 채움, 안 덮인 구멍(안마당)은 남김
    if ctx is not None and len(ctx[1]):
        # 이웃 오브젝트 벽까지 넣어야 닫히는 곳(오브젝트 경계에 걸친 통로·협문 — 교태전 행각 협문은 북쪽 벽이 옆 건물 것)은
        # 내 지붕 아래인 '안쪽'만 채움(이웃 벽 칸 자체는 이웃 껍데기가 가짐)
        wctx = wall_slices(ctx[0], ctx[1])
        both = closing(wmask | wctx, 7)
        inner = fill_holes_if(both, lambda h: not covered(h)) & ~both & ~ndimage.binary_dilation(wctx, iterations=2) & roof
        body_all |= inner
    lab, n = ndimage.label(body_all)
    areas = ndimage.sum(np.ones_like(lab), lab, index=np.arange(1, n + 1)) * GS * GS if n else np.zeros(0)
    body = np.zeros_like(body_all); pillars = []
    for k in range(1, n + 1):
        comp = lab == k
        if areas[k - 1] >= 0.8:
            body |= comp
        elif roof[comp].mean() > 0.5:
            rr, cc = np.nonzero(comp)
            pillars.append((G.x0 + cc.min() * GS, G.x0 + (cc.max() + 1) * GS, G.z0 + rr.min() * GS, G.z0 + (rr.max() + 1) * GS))
    pil_mask = np.zeros_like(body)
    for (x0, x1, z0, z1) in pillars:
        pil_mask |= (X >= x0) & (X <= x1) & (Z >= z0) & (Z <= z1)
    # 기단: 땅보다 8 cm 넘게 높은 곳(위→아래 광선) 중 몸통·기둥에서 BASE_REACH 안, 몸통에 닿는 덩어리 — 계단까지 들어감
    base = np.zeros((G.nz, G.nx), bool)
    if not wall:
        raised = ~np.isnan(Hmid) & (Hmid > y_ground + 0.08) & (Hmid < Hfloor + 0.5)
        dist_b = ndimage.distance_transform_edt(~(body | pil_mask)) * GS
        lab_r, nr = ndimage.label(raised & (dist_b <= BASE_REACH))
        ids = np.unique(lab_r[(dist_b <= 0.5) & (lab_r > 0)])
        base = np.isin(lab_r, ids)
        base = closing(base | body, 6)
        base = fill_holes_if(base, lambda h: not covered(h))
        base = ndimage.binary_opening(base, structure=np.ones((7, 7), bool)) | body
    # 처마 밑면
    Hbot = cast(y_min + 0.15, 1.0, 12.0) if wall else cast(Hfloor + 0.7, 1.0, 12.0)
    excl = ndimage.binary_dilation(body | pil_mask, iterations=3)
    valid_top = ~np.isnan(Htop) & roof
    if valid_top.sum() == 0:
        log(name, 'no roof'); return None
    Htop_f = ndimage.gaussian_filter(nearest_fill(np.nan_to_num(Htop), valid_top), 2.5)
    valid_bot = ~np.isnan(Hbot) & ~excl & roof
    if valid_bot.sum() == 0:
        Hbot_f = Htop_f - 0.4
    else:
        Hbot_f = ndimage.gaussian_filter(nearest_fill(np.nan_to_num(Hbot), valid_bot), 6.0)
    Hbot_f = np.minimum(Hbot_f, Htop_f - 0.08)
    Hraw = nearest_fill(np.nan_to_num(Htop, nan=y_ground), ~np.isnan(Htop))
    samp = lambda A: (lambda x, z: G.sample(A, x, z))
    V_all = []; F_all = []; part = []; nbase = 0
    def add(r, pid):
        nonlocal nbase
        if r is None:
            return
        V, F = r
        V_all.append(V); F_all.append(F + nbase); part.append(np.full(len(F), pid, np.int32)); nbase += len(V)
    y0 = y_min - 0.05 if wall else y_ground - BELOW_GROUND      # 땅속으로 깊이 내려가면 그림 칸만 버림
    add(grid_slab(G, roof, samp(Htop_f), samp(Hbot_f)), 0)
    # 몸통 위끝: 지붕 아래면 처마 밑면 + 0.15(판 속), 아니면 실제 윗면
    body_top = np.where(roof, np.minimum(Hbot_f + 0.15, Htop_f - 0.05), Hraw)
    body_top = nearest_fill(body_top, body | roof)
    add(grid_slab(G, body, samp(body_top), lambda x, z: np.full(len(x), y0), err=0.02), 1)
    # 기단: 덩어리마다 윗면 높이
    labb, nb = ndimage.label(base)
    ring_ok = ~np.isnan(Hmid)
    for k in range(1, nb + 1):
        comp = labb == k
        if comp.sum() * GS * GS < 1.0:
            continue
        r_ = comp & ~ndimage.binary_dilation(body, iterations=2) & ring_ok
        hv = Hmid[r_]; ok_ = (hv > y_ground - 0.5) & (hv < Hfloor[r_] + 0.3); hv = hv[ok_]
        yb = float(np.median(hv)) if len(hv) > 20 else float(np.median(Hfloor[comp]))
        # 윗면 = 위→아래 광선 높이(계단은 비탈로), 덩어리 대표 높이(yb) 위로 솟은 것(주초·문지방)은 잘라 냄
        valid = r_ & (Hmid > y_ground - 0.5)
        if valid.sum() > 20:
            hb = np.clip(np.nan_to_num(Hmid, nan=yb), y_ground - 0.05, yb)
            hb = np.minimum(ndimage.gaussian_filter(nearest_fill(hb, valid), 4.0), yb)
            top_fn = samp(hb)
        else:
            top_fn = (lambda x, z, yb=yb: np.full(len(x), yb))
        add(grid_slab(G, comp, top_fn, lambda x, z: np.full(len(x), y0), err=0.03), 2)
    # 기둥
    for (x0, x1, z0, z1) in pillars:
        cxp = (x0 + x1) / 2; czp = (z0 + z1) / 2
        top = float(G.sample(body_top if roof.any() else Hraw, np.array([cxp]), np.array([czp]))[0])
        yb = float(G.sample(Hfloor, np.array([cxp]), np.array([czp]))[0]) - 0.1
        Vp = np.array([(x0, yb, z0), (x1, yb, z0), (x1, yb, z1), (x0, yb, z1), (x0, top, z0), (x1, top, z0), (x1, top, z1), (x0, top, z1)])
        Fp = []
        c = Vp.mean(0)
        for (i0, i1) in ((0, 1), (1, 2), (2, 3), (3, 0)):
            q = np.array([[i0, i1, i1 + 4], [i0, i1 + 4, i0 + 4]], np.int64)
            mid = (Vp[i0] + Vp[i1]) / 2; out = mid - c; out[1] = 0
            Fp.append(orient(q, Vp, out))
        add((Vp, np.concatenate(Fp)), 3)
    if not V_all:
        return None
    V = np.concatenate(V_all); F = np.concatenate(F_all); part = np.concatenate(part)
    info = dict(wall=bool(wall), y_ground=y_ground, y_sl=y_sl, roof_m2=float(roof.sum() * GS * GS), body_m2=float(body.sum() * GS * GS), pillars=len(pillars),
                tris=int(len(F)), sec=round(time.time() - t0, 1))
    log('%-18s %s tris %5d  roof %6.1f m2 body %6.1f m2 pillars %2d  ground %.2f floor %.2f  (%.1f s)' % (name, 'W' if wall else 'B', len(F), info['roof_m2'], info['body_m2'], len(pillars), y_ground, y_sl, info['sec']))
    return V, F, part, info
