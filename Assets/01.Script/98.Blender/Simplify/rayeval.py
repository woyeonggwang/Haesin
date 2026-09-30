"""여러 방향 평행 광선으로 원본과 줄인 메시가 같게 보이는지 비교(깊이·텍스처 색·맞음/안 맞음)."""
import ctypes, os
import numpy as np

RC = ctypes.CDLL(os.environ.get("RAYCAST_LIB", "/tmp/libraycast.so"))
RC.bvh_build.restype = ctypes.c_void_p
RC.bvh_build.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, ctypes.c_int]
RC.bvh_free.argtypes = [ctypes.c_void_p]
RC.bvh_cast.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_int, ctypes.c_float,
                        ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p]


class RMesh:
    def __init__(self, pos, tris, uv, tmat, vn=None):
        self.vn = None if vn is None else np.asarray(vn, np.float32)
        self.pos = np.ascontiguousarray(pos, np.float32)
        self.tris = np.ascontiguousarray(tris, np.int32).reshape(-1, 3)
        self.uv = np.ascontiguousarray(uv, np.float32)
        self.tmat = np.asarray(tmat, np.int32)
        self.h = RC.bvh_build(self.pos.ctypes.data, len(self.pos), self.tris.ctypes.data, len(self.tris))
        A = self.pos[self.tris[:, 0]].astype(np.float64); B = self.pos[self.tris[:, 1]].astype(np.float64); C = self.pos[self.tris[:, 2]].astype(np.float64)
        n = np.cross(B - A, C - A); l = np.linalg.norm(n, axis=1); l[l < 1e-20] = 1
        self.fn = n / l[:, None]

    def free(self):
        if self.h:
            RC.bvh_free(self.h); self.h = None

    def cast(self, O, D, tmax):
        n = len(O); O = np.ascontiguousarray(O, np.float32); D = np.ascontiguousarray(D, np.float32)
        t = np.empty(n, np.float32); tri = np.empty(n, np.int32); u = np.empty(n, np.float32); v = np.empty(n, np.float32)
        RC.bvh_cast(self.h, O.ctypes.data, D.ctypes.data, n, ctypes.c_float(tmax), t.ctypes.data, tri.ctypes.data, u.ctypes.data, v.ctypes.data)
        return t, tri, u, v

    def hit_normal(self, tri, u, v):
        ok = tri >= 0; out = np.zeros((len(tri), 3), np.float32)
        if self.vn is None:
            out[ok] = self.fn[tri[ok]]
        else:
            tt = self.tris[tri[ok]]
            n = self.vn[tt[:, 0]] * (1 - u[ok] - v[ok])[:, None] + self.vn[tt[:, 1]] * u[ok][:, None] + self.vn[tt[:, 2]] * v[ok][:, None]
            l = np.linalg.norm(n, axis=1); l[l < 1e-12] = 1
            out[ok] = n / l[:, None]
        return out

    def hit_uv(self, tri, u, v):
        ok = tri >= 0; out = np.zeros((len(tri), 2), np.float32)
        tt = self.tris[tri[ok]]
        out[ok] = self.uv[tt[:, 0]] * (1 - u[ok] - v[ok])[:, None] + self.uv[tt[:, 1]] * u[ok][:, None] + self.uv[tt[:, 2]] * v[ok][:, None]
        return out


def directions():
    d = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]
    for sx in (1, -1):
        for sy in (1, -1):
            for sz in (1, -1):
                d.append((sx, sy, sz))
    d = np.array(d, np.float64)
    return d / np.linalg.norm(d, axis=1)[:, None]


def rays_for(bmin, bmax, d, per_axis=96):
    c = (bmin + bmax) / 2; rad = np.linalg.norm(bmax - bmin) / 2 + 0.01
    a = np.array([1.0, 0, 0]) if abs(d[0]) < 0.9 else np.array([0, 1.0, 0])
    u = np.cross(d, a); u /= np.linalg.norm(u); v = np.cross(d, u)
    corners = np.array([[x, y, z] for x in (bmin[0], bmax[0]) for y in (bmin[1], bmax[1]) for z in (bmin[2], bmax[2])]) - c
    pu = corners @ u; pv = corners @ v
    su = pu.max() - pu.min(); sv = pv.max() - pv.min()
    step = max(su, sv) / per_axis
    if step <= 0:
        return np.zeros((0, 3)), np.zeros((0, 3))
    gu = np.arange(pu.min() + step / 2, pu.max(), step); gv = np.arange(pv.min() + step / 2, pv.max(), step)
    GU, GV = np.meshgrid(gu, gv)
    O = c - d * (rad + 1.0) + GU.reshape(-1, 1) * u + GV.reshape(-1, 1) * v
    D = np.repeat(d.reshape(1, 3), len(O), axis=0)
    return O, D


def sample_tex(arr, uv):
    s = arr.shape[0]
    x = np.mod(uv[:, 0], 1.0) * s - 0.5; y = np.mod(uv[:, 1], 1.0) * s - 0.5
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int); fx = (x - x0)[:, None]; fy = (y - y0)[:, None]
    x0 %= s; y0 %= s; x1 = (x0 + 1) % s; y1 = (y0 + 1) % s
    a = arr.astype(np.float32) / 255.0 if arr.dtype == np.uint8 else arr
    return a[y0, x0] * (1 - fx) * (1 - fy) + a[y0, x1] * fx * (1 - fy) + a[y1, x0] * (1 - fx) * fy + a[y1, x1] * fx * fy


def compare(RO, RD, texs, tol, per_axis=96, col_thr=0.15):
    """texs: 재질 번호 → (s,s,3) 배열 또는 None. 반환: dict(오류 비율들)."""
    bmin = RO.pos.min(0).astype(np.float64); bmax = RO.pos.max(0).astype(np.float64)
    tot = 0; miss = 0; deep = 0; colr = 0
    rad = np.linalg.norm(bmax - bmin) + 4.0
    for d in directions():
        O, D = rays_for(bmin, bmax, d, per_axis)
        if len(O) == 0:
            continue
        tO, iO, uO, vO = RO.cast(O, D, rad * 2); tD, iD, uD, vD = RD.cast(O, D, rad * 2)
        hitO = iO >= 0; hitD = iD >= 0
        anyh = hitO | hitD
        tot += int(anyh.sum())
        miss += int((hitO != hitD).sum())
        both = hitO & hitD
        if both.any():
            cos = np.abs(RO.fn[iO[both]] @ d)
            allow = tol / np.maximum(cos, 0.2) + 0.001
            dd = np.abs(tO[both] - tD[both])
            deep += int((dd > allow).sum())
            uvO = RO.hit_uv(iO, uO, vO)[both]; uvD = RD.hit_uv(iD, uD, vD)[both]
            mO = RO.tmat[iO[both]]; mD = RD.tmat[iD[both]]
            cdiff = np.zeros(int(both.sum()))
            for m in np.unique(np.concatenate([mO, mD])):
                arr = texs.get(int(m)) if texs else None
                if arr is None:
                    continue
                s1 = mO == m; s2 = mD == m
                if s1.any():
                    cO = sample_tex(arr, uvO[s1])
                    cD = np.zeros_like(cO)
                    same = s2[s1]
                    if same.any():
                        cD[same] = sample_tex(arr, uvD[s1][same])
                    cd = np.abs(cO - cD).max(1); cd[~same] = 1.0
                    cdiff[np.nonzero(s1)[0]] = np.maximum(cdiff[np.nonzero(s1)[0]], cd)
            colr += int((cdiff > col_thr).sum())
    tot = max(tot, 1)
    return dict(miss=miss / tot, depth=deep / tot, color=colr / tot, rays=tot)


def _basis(d):
    a = np.array([1.0, 0, 0]) if abs(d[0]) < 0.9 else np.array([0, 1.0, 0])
    u = np.cross(d, a); u /= np.linalg.norm(u); v = np.cross(d, u)
    return u, v


def _hits(R, O, D, tmax, texs):
    t, tri, u, v = R.cast(O, D, tmax)
    hit = tri >= 0
    uv = R.hit_uv(tri, u, v)
    col = np.full((len(O), 3), -1.0, np.float32)
    mat = np.full(len(O), -1, np.int32)
    mat[hit] = R.tmat[tri[hit]]
    for m in np.unique(mat[hit]):
        arr = texs.get(int(m)) if texs else None
        s = mat == m
        if arr is None:
            col[s] = 0.0
        else:
            col[s] = sample_tex(arr, uv[s])
    cos = np.zeros(len(O)); cos[hit] = np.abs((R.fn[tri[hit]] * D[hit]).sum(1))
    nrm = R.hit_normal(tri, u, v)
    # 앞면 기준으로 맞춤(광선 쪽을 보게)
    sgn = np.sign((nrm * -D).sum(1)); sgn[sgn == 0] = 1
    return hit, t, col, cos, nrm * sgn[:, None]


def compare2(RO, RD, texs, tol, per_axis=96, col_thr=0.15, nrm_cos=None):
    """오차 허용(tol)만큼 가장자리가 움직인 것은 봐주는 비교: 어긋난 광선은 줄인 쪽에서만 ±tol 만큼 옮긴 광선 4개로 다시 확인
    (원본 쪽도 옮기면 tol 보다 가는 부재가 사라진 것까지 봐주게 됨)."""
    bmin = RO.pos.min(0).astype(np.float64); bmax = RO.pos.max(0).astype(np.float64)
    rad = np.linalg.norm(bmax - bmin) + 4.0
    delta = tol + 0.0005
    tot = 0; bad = {'miss': 0, 'depth': 0, 'color': 0}
    for d in directions():
        O, D = rays_for(bmin, bmax, d, per_axis)
        if len(O) == 0:
            continue
        hO, tO, cO, kO, nO = _hits(RO, O, D, rad * 2, texs)
        hD, tD, cD, kD, nD = _hits(RD, O, D, rad * 2, texs)
        anyh = hO | hD; tot += int(anyh.sum())

        def match(ha, ta, ca, ka, na, hb, tb, cb, nb):
            both = ha & hb
            allow = tol / np.maximum(ka, 0.2) + 0.001
            dep = np.abs(ta - tb) <= allow
            colok = np.abs(ca - cb).max(1) <= col_thr
            nok = np.ones(len(ha), bool) if nrm_cos is None else ((na * nb).sum(1) >= nrm_cos)
            return (~ha & ~hb) | (both & dep & colok & nok)
        ok = match(hO, tO, cO, kO, nO, hD, tD, cD, nD)
        idx = np.nonzero(anyh & ~ok)[0]
        if len(idx) == 0:
            continue
        u, v = _basis(d)
        fixed = np.zeros(len(idx), bool)
        for off in (u, -u, v, -v):
            Oo = O[idx] + off * delta; Do = D[idx]
            hD2, tD2, cD2, kD2, nD2 = _hits(RD, Oo, Do, rad * 2, texs)
            fixed |= match(hO[idx], tO[idx], cO[idx], kO[idx], nO[idx], hD2, tD2, cD2, nD2)
        rem = idx[~fixed]
        for i in rem:
            if hO[i] != hD[i]:
                bad['miss'] += 1
            elif abs(tO[i] - tD[i]) > tol / max(kO[i], 0.2) + 0.001:
                bad['depth'] += 1
            elif np.abs(cO[i] - cD[i]).max() > col_thr:
                bad['color'] += 1
            else:
                bad['shade'] = bad.get('shade', 0) + 1
    tot = max(tot, 1)
    return dict(miss=bad['miss'] / tot, depth=bad['depth'] / tot, color=bad['color'] / tot, shade=bad.get('shade', 0) / tot, rays=tot)
