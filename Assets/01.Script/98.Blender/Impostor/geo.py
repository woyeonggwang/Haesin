"""껍데기(임포스터) 모양 만들기에 쓰는 2D·3D 도구."""
import numpy as np, ctypes
from scipy import ndimage
from skimage import draw, measure

import os
MO = ctypes.CDLL(os.environ.get('MESHOPT_LIB', '/tmp/libmeshopt.so'))
MO.meshopt_simplify.restype = ctypes.c_size_t
MO.meshopt_simplify.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_size_t,
                                ctypes.c_size_t, ctypes.c_float, ctypes.c_uint, ctypes.c_void_p]
LOCK_BORDER = 1; ERROR_ABS = 4


def simplify(P, F, err, lock_border=False):
    P = np.ascontiguousarray(P, np.float32); idx = np.ascontiguousarray(F.reshape(-1), np.uint32)
    dst = np.zeros_like(idx); res = ctypes.c_float(0)
    n = MO.meshopt_simplify(dst.ctypes.data, idx.ctypes.data, len(idx), P.ctypes.data, len(P), 12, 0, ctypes.c_float(err),
                            ERROR_ABS | (LOCK_BORDER if lock_border else 0), ctypes.byref(res))
    F2 = dst[:n].reshape(-1, 3).astype(np.int64)
    used, inv = np.unique(F2.ravel(), return_inverse=True)
    return P[used].astype(np.float64), inv.reshape(-1, 3), float(res.value)


class Grid:
    """x·z 평면 격자(행 = z 오름차순, 열 = x 오름차순), 칸 가운데 좌표"""
    def __init__(self, x0, z0, g, nx, nz):
        self.x0, self.z0, self.g, self.nx, self.nz = x0, z0, g, nx, nz
        self.xs = x0 + (np.arange(nx) + 0.5) * g; self.zs = z0 + (np.arange(nz) + 0.5) * g

    def centers(self):
        X, Z = np.meshgrid(self.xs, self.zs)
        return X, Z

    def sample(self, A, x, z):
        """이중선형(A 는 NaN 없음)"""
        c = (np.asarray(x) - self.x0) / self.g - 0.5; r = (np.asarray(z) - self.z0) / self.g - 0.5
        return ndimage.map_coordinates(A, [r, c], order=1, mode='nearest')

    def contour_poly(self, m, tol):
        cs = measure.find_contours(np.pad(m.astype(float), 1), 0.5)
        c = max(cs, key=len) - 1
        ap = measure.approximate_polygon(c, tolerance=tol / self.g)
        xy = np.stack([self.x0 + (ap[:, 1] + 0.5) * self.g, self.z0 + (ap[:, 0] + 0.5) * self.g], 1)
        if np.allclose(xy[0], xy[-1]):
            xy = xy[:-1]
        return xy


def nearest_fill(A, valid):
    idx = ndimage.distance_transform_edt(~valid, return_distances=False, return_indices=True)
    return A[tuple(idx)]


def slice_segments(P, T, h):
    A = P[T[:, 0]]; B = P[T[:, 1]]; C = P[T[:, 2]]
    ys = np.stack([A[:, 1], B[:, 1], C[:, 1]], 1)
    s = (ys.min(1) <= h) & (ys.max(1) >= h)
    A = A[s]; B = B[s]; C = C[s]
    Q = []
    for (X, Y) in ((A, B), (B, C), (C, A)):
        d = Y[:, 1] - X[:, 1]; ok = np.abs(d) > 1e-9
        t = np.where(ok, (h - X[:, 1]) / np.where(ok, d, 1), -1.0)
        cr = ok & (t >= 0) & (t <= 1)
        q = X + (Y - X) * t[:, None]
        Q.append(np.where(cr[:, None], q, np.nan))
    Q = np.stack(Q, 1)
    out = []
    for i in range(len(Q)):
        v = Q[i][~np.isnan(Q[i][:, 0])]
        if len(v) >= 2:
            out.append((v[0], v[1]))
    return out


def seg_mask(G, segs):
    m = np.zeros((G.nz, G.nx), bool)
    for a, c in segs:
        c0 = int((a[0] - G.x0) / G.g); r0 = int((a[2] - G.z0) / G.g); c1 = int((c[0] - G.x0) / G.g); r1 = int((c[2] - G.z0) / G.g)
        rr, cc = draw.line(r0, c0, r1, c1)
        ok = (rr >= 0) & (rr < G.nz) & (cc >= 0) & (cc < G.nx)
        m[rr[ok], cc[ok]] = True
    return m


def close_fill(m, r):
    st = np.ones((2 * r + 1, 2 * r + 1), bool)
    p = r + 2
    m2 = ndimage.binary_closing(np.pad(m, p), structure=st)[p:-p, p:-p]
    return ndimage.binary_fill_holes(m2)


def rectilinear(poly, min_len=0.12):
    """거의 직각인 다각형을 축에 맞춘 직각 다각형으로(짧은 톱니는 합침)"""
    P = np.asarray(poly, float); n = len(P)
    E = []
    for i in range(n):
        a = P[i]; b = P[(i + 1) % n]; d = b - a
        E.append(['H' if abs(d[0]) >= abs(d[1]) else 'V', a, b])
    # 같은 방향 연속 변 합치기
    changed = True
    while changed and len(E) > 4:
        changed = False
        for i in range(len(E)):
            j = (i + 1) % len(E)
            if E[i][0] == E[j][0]:
                E[i] = [E[i][0], E[i][1], E[j][2]]; del E[j]; changed = True; break
        if not changed:
            # 아주 짧은 변(톱니) 없애기: 짧은 변을 지우면 양옆이 같은 방향이 되어 합쳐짐
            L = [np.linalg.norm(e[2] - e[1]) for e in E]
            k = int(np.argmin(L))
            if L[k] < min_len and len(E) > 4:
                del E[k]; changed = True
    # 좌표 정하기: H 변은 z 고정(두 끝 평균), V 변은 x 고정
    val = []
    for t, a, b in E:
        val.append((a[1] + b[1]) / 2 if t == 'H' else (a[0] + b[0]) / 2)
    out = []
    for i in range(len(E)):
        j = (i + 1) % len(E)
        if E[i][0] == 'H':
            out.append((val[j], val[i]))      # 다음 V 변의 x, 이 H 변의 z
        else:
            out.append((val[i], val[j]))
    return np.array(out)


def point_in_poly(x, z, poly):
    x = np.asarray(x); z = np.asarray(z); ins = np.zeros(x.shape, bool)
    n = len(poly)
    for i in range(n):
        x1, z1 = poly[i]; x2, z2 = poly[(i + 1) % n]
        c = ((z1 > z) != (z2 > z)) & (x < (x2 - x1) * (z - z1) / np.where(z2 != z1, z2 - z1, 1e-12) + x1)
        ins ^= c
    return ins


def poly_area(poly):
    x = poly[:, 0]; z = poly[:, 1]
    return 0.5 * (np.dot(x, np.roll(z, -1)) - np.dot(z, np.roll(x, -1)))


def nearest_on_poly(p, poly):
    """p (N,2) → 다각형 둘레 위 가장 가까운 점"""
    best = None; bd = None
    n = len(poly)
    for i in range(n):
        a = poly[i]; b = poly[(i + 1) % n]; d = b - a; L2 = max(d @ d, 1e-12)
        t = np.clip(((p - a) @ d) / L2, 0, 1)
        q = a + t[:, None] * d
        dd = np.linalg.norm(p - q, axis=1)
        if best is None:
            best = q; bd = dd
        else:
            s = dd < bd; best[s] = q[s]; bd[s] = dd[s]
    return best, bd


def earclip(poly):
    """단순 다각형(반시계/시계 무관) 귀 자르기 → 삼각형 인덱스"""
    P = np.asarray(poly, float); idx = list(range(len(P)))
    ccw = poly_area(P) > 0
    tris = []
    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    guard = 0
    while len(idx) > 3 and guard < 10000:
        guard += 1
        n = len(idx); found = False
        for k in range(n):
            i0, i1, i2 = idx[(k - 1) % n], idx[k], idx[(k + 1) % n]
            a, b, c = P[i0], P[i1], P[i2]
            cr = cross(a, b, c)
            if (cr > 1e-12) != ccw or abs(cr) < 1e-12:
                continue
            ok = True
            for j in idx:
                if j in (i0, i1, i2):
                    continue
                p = P[j]
                c1 = cross(a, b, p); c2 = cross(b, c, p); c3 = cross(c, a, p)
                if ccw and c1 >= -1e-12 and c2 >= -1e-12 and c3 >= -1e-12:
                    ok = False; break
                if (not ccw) and c1 <= 1e-12 and c2 <= 1e-12 and c3 <= 1e-12:
                    ok = False; break
            if ok:
                tris.append((i0, i1, i2)); del idx[k]; found = True; break
        if not found:
            break
    if len(idx) == 3:
        tris.append(tuple(idx))
    return np.array(tris, np.int64)
