"""기와 윗면을 따라가는 검은 판 만들기(멀리서 기와 대신 쓰는 한 장짜리 면).

기와 삼각형만 모은 BVH 에 격자(기본 0.25 m) 꼭짓점마다 위에서 아래로 광선을 여러 번 쏘아 층(겹지붕의 위·아래 지붕)을 나누고,
이웃 꼭짓점끼리 높이가 이어지는 것끼리 한 장(sheet)으로 묶은 뒤, 칸마다 마칭 스퀘어로 면을 만든다.
가장자리 칸은 안/밖 꼭짓점 사이를 반으로 나눠 가며(6번) 기와가 끝나는 곳을 찾아 테두리가 계단 모양이 되지 않게 한다.
판 높이 = 그 자리 기와 윗면 − offset.
"""
import sys, os
import numpy as np
_HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(_HERE, '..')); sys.path.insert(0, os.path.join(_HERE, '..', 'Simplify'))
from rayeval import RMesh


class UF:
    def __init__(self, n):
        self.p = np.arange(n)

    def find(self, a):
        p = self.p
        r = a
        while p[r] != r:
            r = p[r]
        while p[a] != r:
            p[a], a = r, p[a]
        return r

    def union(self, a, b):
        ra, rb = self.find(a), self.find(b)
        if ra != rb:
            self.p[max(ra, rb)] = min(ra, rb)


def multi_hits(rm, X, Z, ytop, maxhits=16):
    """(X,Z) 마다 아래로 쏜 광선의 모든 맞은 높이 목록."""
    n = len(X)
    out = [[] for _ in range(n)]
    O = np.stack([X, np.full(n, ytop), Z], 1).astype(np.float32)
    D = np.tile(np.array([0, -1, 0], np.float32), (n, 1))
    act = np.arange(n)
    for _ in range(maxhits):
        if len(act) == 0:
            break
        t, tri, _, _ = rm.cast(O[act], D[act], 1e4)
        hit = tri >= 0
        a = act[hit]
        y = O[a, 1] - t[hit]
        for i, yy in zip(a, y):
            out[i].append(float(yy))
        O[a, 1] = y - 0.004
        act = a
    return out


def layers_of(hits, gap):
    """내림차순 높이를 gap 보다 크게 벌어지면 다른 층으로. 층마다 (위 높이, 아래 높이)."""
    if not hits:
        return []
    h = sorted(hits, reverse=True)
    L = [[h[0], h[0]]]
    for y in h[1:]:
        if L[-1][1] - y > gap:
            L.append([y, y])
        else:
            L[-1][1] = y
    return L


def build_sheets(P, T, cell=0.25, gap=0.8, slope_step=None, min_cells=6, log=print):
    """기와 삼각형(P,T) → 장(sheet) 목록. 각 장 = dict(x0, z0, cell, nx, nz, inside(nz,nx), height(nz,nx), rm)."""
    rm = RMesh(P, T, np.zeros((len(P), 2), np.float32), np.zeros(len(T), np.int32))
    mn = P.min(0) - cell; mx = P.max(0) + cell
    nx = int(np.ceil((mx[0] - mn[0]) / cell)) + 1; nz = int(np.ceil((mx[2] - mn[2]) / cell)) + 1
    xs = mn[0] + np.arange(nx) * cell; zs = mn[2] + np.arange(nz) * cell
    X, Z = np.meshgrid(xs, zs)
    hits = multi_hits(rm, X.ravel(), Z.ravel(), mx[1] + 5)
    lay = [layers_of(h, gap) for h in hits]
    # 노드 = (꼭짓점, 층)
    node_of = {}
    tops = []
    for ci, L in enumerate(lay):
        for li, (yt, yb) in enumerate(L):
            node_of[(ci, li)] = len(tops); tops.append(yt)
    tops = np.array(tops)
    uf = UF(len(tops))
    step = slope_step if slope_step is not None else max(0.5, cell * 2.8)
    for iz in range(nz):
        for ix in range(nx):
            ci = iz * nx + ix
            for dz, dx in ((0, 1), (1, 0)):
                jz, jx = iz + dz, ix + dx
                if jz >= nz or jx >= nx:
                    continue
                cj = jz * nx + jx
                for li in range(len(lay[ci])):
                    a = node_of[(ci, li)]
                    best = None
                    for lj in range(len(lay[cj])):
                        b = node_of[(cj, lj)]
                        dd = abs(tops[a] - tops[b])
                        if dd <= step and (best is None or dd < best[0]):
                            best = (dd, b)
                    if best:
                        uf.union(a, best[1])
    roots = np.array([uf.find(i) for i in range(len(tops))])
    sheets = []
    for r in np.unique(roots):
        members = np.where(roots == r)[0]
        if len(members) < min_cells:
            continue
        inside = np.zeros((nz, nx), bool); height = np.full((nz, nx), np.nan)
        inv = {v: k for k, v in node_of.items() if roots[v] == r}
        for nid in members:
            ci, li = inv[nid]
            iz, ix = divmod(ci, nx)
            if inside[iz, ix]:
                # 같은 꼭짓점에 같은 장의 층이 둘이면 위쪽
                height[iz, ix] = max(height[iz, ix], tops[nid])
            else:
                inside[iz, ix] = True; height[iz, ix] = tops[nid]
        sheets.append(dict(x0=mn[0], z0=mn[2], cell=cell, nx=nx, nz=nz, inside=inside, height=height, rm=rm, gap=gap))
    log('sheets', len(sheets), [int(s['inside'].sum()) for s in sheets])
    return sheets, rm


def fill_small_holes(sh, max_cells=12):
    """장 안쪽의 작은 구멍(기와 틈으로 광선이 빠진 곳)을 메우고 높이는 이웃 평균."""
    from scipy import ndimage
    ins = sh['inside']
    lab, n = ndimage.label(~ins)
    border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]])))
    H = sh['height']
    for k in range(1, n + 1):
        if k in border:
            continue
        m = lab == k
        if m.sum() > max_cells:
            continue
        # 이웃 평균으로 채움(몇 번 반복)
        ins[m] = True
        for _ in range(8):
            Hn = np.where(np.isnan(H), 0, H); W = (~np.isnan(H)).astype(float)
            s = ndimage.convolve(Hn, np.ones((3, 3)), mode='nearest'); w = ndimage.convolve(W, np.ones((3, 3)), mode='nearest')
            upd = m & np.isnan(H) & (w > 0)
            H[upd] = s[upd] / w[upd]
            if not np.isnan(H[m]).any():
                break
    sh['inside'] = ins; sh['height'] = H


def edge_cross(sh, p_in, y_in, p_out, iters=6):
    """안(p_in, 높이 y_in) → 밖(p_out) 사이에서 기와가 끝나는 점(x, y, z)."""
    rm = sh['rm']; gap = sh['gap']
    a = np.array(p_in, float); b = np.array(p_out, float); ya = y_in
    for _ in range(iters):
        m = (a + b) / 2
        hs = multi_hits(rm, np.array([m[0]]), np.array([m[1]]), y_in + gap + 3.0, maxhits=8)[0]
        cand = [h for h in hs if abs(h - ya) <= gap]
        if cand:
            a = m; ya = max(cand)
        else:
            b = m
    return a[0], ya, a[1]


def triangulate(sh, offset, refine=True):
    """마칭 스퀘어 → (정점 N×3, 삼각형 M×3). 높이 = 기와 윗면 − offset."""
    nx, nz, c = sh['nx'], sh['nz'], sh['cell']
    ins = sh['inside']; H = sh['height']
    V = []; vid = {}
    def corner(iz, ix):
        k = ('c', iz, ix)
        if k not in vid:
            vid[k] = len(V); V.append((sh['x0'] + ix * c, H[iz, ix] - offset, sh['z0'] + iz * c))
        return vid[k]
    def edge(iz0, ix0, iz1, ix1):
        # 안 꼭짓점(iz0,ix0) → 밖(iz1,ix1)
        k = ('e',) + tuple(sorted([(iz0, ix0), (iz1, ix1)]))
        if k not in vid:
            pin = (sh['x0'] + ix0 * c, sh['z0'] + iz0 * c); pout = (sh['x0'] + ix1 * c, sh['z0'] + iz1 * c)
            if refine:
                x, y, z = edge_cross(sh, pin, H[iz0, ix0], pout)
            else:
                x, y, z = (pin[0] + pout[0]) / 2, H[iz0, ix0], (pin[1] + pout[1]) / 2
            vid[k] = len(V); V.append((x, y - offset, z))
        return vid[k]
    F = []
    for iz in range(nz - 1):
        for ix in range(nx - 1):
            # 꼭짓점 순서(반시계, 위에서 볼 때 x 오른쪽 z 위): (iz,ix) (iz,ix+1) (iz+1,ix+1) (iz+1,ix)
            cs = [(iz, ix), (iz, ix + 1), (iz + 1, ix + 1), (iz + 1, ix)]
            f = [ins[a] for a in cs]
            if not any(f):
                continue
            poly = []
            for k in range(4):
                a = cs[k]; b = cs[(k + 1) % 4]
                if ins[a]:
                    poly.append(corner(*a))
                if ins[a] != ins[b]:
                    if ins[a]:
                        poly.append(edge(a[0], a[1], b[0], b[1]))
                    else:
                        poly.append(edge(b[0], b[1], a[0], a[1]))
            if sum(f) == 2 and f[0] == f[2]:
                # 대각선만 안(안장점): 두 조각으로
                for k in (0, 2) if f[0] else (1, 3):
                    a = cs[k]; prv = cs[(k + 3) % 4]; nxt = cs[(k + 1) % 4]
                    F.append((corner(*a), edge(a[0], a[1], nxt[0], nxt[1]), edge(a[0], a[1], prv[0], prv[1])))
                continue
            for k in range(1, len(poly) - 1):
                F.append((poly[0], poly[k], poly[k + 1]))
    V = np.array(V, np.float64); F = np.array(F, np.int64).reshape(-1, 3)
    # 위를 향하게(법선 y>0) 감김 맞춤 — Unity 앞면 = cross(B-A, C-A)
    if len(F):
        n = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
        flip = n[:, 1] < 0
        F[flip] = F[flip][:, [0, 2, 1]]
    return V, F


def underlay(sh, fine=0.05, win=0.7, extra=0.03, shrink_cells=3, need_rm=None, need_dist=0.6):
    """LOD0 용 받침판: 기와 골(암키와) 바닥보다 extra 만큼 아래, 테두리에서 shrink_cells 칸 안쪽까지만,
    기와 바로 아래(need_dist)에 남은 부재가 없는 곳(지운 지붕 속)만.
    기와 사이 머리카락 틈으로 (지운) 지붕 속이 비치지 않게 검은 면을 깐다. 반환 (V, F)."""
    from scipy import ndimage
    rm = sh['rm']; c = sh['cell']; nx, nz = sh['nx'], sh['nz']
    ins = ndimage.binary_erosion(sh['inside'], iterations=shrink_cells)
    if ins.sum() < 4:
        return np.zeros((0, 3)), np.zeros((0, 3), np.int64)
    H = sh['height']
    # 고운 격자(0.05 m): 굵은 격자 높이를 이웃 보간 → 그 위 0.3 m 에서 아래로 쏴 이 장의 기와 윗면
    k = int(round(c / fine))
    fx = sh['x0'] + np.arange((nx - 1) * k + 1) * fine; fz = sh['z0'] + np.arange((nz - 1) * k + 1) * fine
    Hc = np.where(sh['inside'], H, np.nan)
    # 굵은 높이의 빈 곳은 가까운 값으로(보간용)
    idx = ndimage.distance_transform_edt(np.isnan(Hc), return_distances=False, return_indices=True)
    Hf0 = Hc[idx[0], idx[1]]
    Hup = ndimage.zoom(Hf0, k, order=1)[:len(fz), :len(fx)]
    X, Z = np.meshgrid(fx, fz)
    near = ndimage.zoom(ndimage.binary_dilation(sh['inside'], iterations=1).astype(np.uint8), k, order=0)[:len(fz), :len(fx)].astype(bool)
    O = np.stack([X[near], Hup[near] + 0.3, Z[near]], 1).astype(np.float32)
    t, tri, _, _ = rm.cast(O, np.tile(np.array([0, -1, 0], np.float32), (len(O), 1)), 0.9)
    hf = np.full(X.shape, np.nan); hv = np.where(tri >= 0, O[:, 1] - t, np.nan); hf[near] = hv
    ok = ~np.isnan(hf)
    # 추세(가우시안) + 잔차 최솟값(창 win)
    sig = win / 2 / fine
    w = ndimage.gaussian_filter(ok.astype(float), sig); s = ndimage.gaussian_filter(np.where(ok, hf, 0.0), sig)
    trend = np.where(w > 1e-6, s / np.maximum(w, 1e-6), np.nan)
    r = np.where(ok, hf - trend, np.inf)
    rmin = ndimage.minimum_filter(r, size=int(round(win / fine)) + 1)
    low = trend + np.where(np.isfinite(rmin), rmin, 0.0) - extra
    # 굵은 격자 꼭짓점 값
    L = low[::k, ::k][:nz, :nx]
    L = np.where(np.isnan(L), H - 0.15, L)
    if need_rm is not None:
        # 기와 바로 아래(need_dist 안)에 남긴(보이는) 부재가 있으면 그 부재가 틈으로 보이므로 받침판이 필요 없음
        iz, ix = np.where(ins)
        O2 = np.stack([sh['x0'] + ix * c, L[iz, ix] + extra - 0.005, sh['z0'] + iz * c], 1).astype(np.float32)
        t2, tri2, _, _ = need_rm.cast(O2, np.tile(np.array([0, -1, 0], np.float32), (len(O2), 1)), need_dist)
        need = np.zeros_like(ins); need[iz, ix] = tri2 < 0
        need = ndimage.binary_dilation(need, iterations=1) & ins
        ins = need
        if ins.sum() < 4:
            return np.zeros((0, 3)), np.zeros((0, 3), np.int64)
    sh2 = dict(sh); sh2['inside'] = ins; sh2['height'] = L
    return triangulate(sh2, 0.0, refine=False)
