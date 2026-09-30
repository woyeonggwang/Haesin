"""meshoptimizer 로 궁궐 메시(Unity 형식: 이음매마다 정점이 나뉜 것)를 모양·UV 를 지키며 줄인다.

- 입력/출력 형식 = PalaceBinExport 의 meshes.bin (위치·법선·UV0·서브메시 인덱스, Unity 좌표).
- meshopt 는 정점을 새로 만들거나 옮기지 않고 남길 정점을 고르기만 한다 → UV·법선 값은 원래 값 그대로.
- 같은 위치에 정점이 여럿이면(UV·법선 이음매) 이음매로 보고 양쪽을 같이 접는다 → 텍스처가 번지지 않음.
- 서브메시(재질)끼리 정점을 공유하면 재질 경계가 번질 수 있으므로 서브메시마다 정점을 따로 복사해 경계를 이음매로 만든다.
- 필요한 것: numpy, scipy, meshoptimizer 공유 라이브러리(1.3, git 4c203430 에서 확인 —
  g++ -O2 -shared -fPIC meshoptimizer/src/*.cpp -o libmeshopt.so, MESHOPT_LIB 환경 변수로 경로 지정, 없으면 /tmp/libmeshopt.so).
"""
import ctypes, struct, os, math
import numpy as np
from scipy.spatial import cKDTree

LIB = ctypes.CDLL(os.environ.get("MESHOPT_LIB", "/tmp/libmeshopt.so"))
LIB.meshopt_simplifyWithAttributes.restype = ctypes.c_size_t
LIB.meshopt_simplifyWithAttributes.argtypes = [
    ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_size_t,
    ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p,
    ctypes.c_size_t, ctypes.c_float, ctypes.c_uint, ctypes.POINTER(ctypes.c_float)]
LIB.meshopt_optimizeVertexFetchRemap.restype = ctypes.c_size_t

LockBorder = 1 << 0
ErrorAbsolute = 1 << 2
Prune = 1 << 3


def read_bin(path):
    buf = np.fromfile(path, dtype=np.uint8)
    off = 0

    def i32():
        nonlocal off
        v = int(np.frombuffer(buf, dtype='<i4', count=1, offset=off)[0]); off += 4; return v
    magic = i32(); n = i32(); out = []
    for _ in range(n):
        ln = i32(); name = bytes(buf[off:off + ln]).decode('utf-8'); off += ln
        nv = i32(); ns = i32()
        pos = np.frombuffer(buf, '<f4', nv * 3, off).reshape(-1, 3); off += nv * 12
        nrm = np.frombuffer(buf, '<f4', nv * 3, off).reshape(-1, 3); off += nv * 12
        uv = np.frombuffer(buf, '<f4', nv * 2, off).reshape(-1, 2); off += nv * 8
        subs = []
        for _ in range(ns):
            ni = i32(); subs.append(np.frombuffer(buf, '<i4', ni, off).astype(np.uint32)); off += ni * 4
        out.append(dict(name=name, pos=pos, nrm=nrm, uv=uv, subs=subs))
    return out


def write_bin(path, meshes):
    with open(path, 'wb') as f:
        f.write(struct.pack('<ii', 0x31584250, len(meshes)))
        for d in meshes:
            nb = d['name'].encode('utf-8'); f.write(struct.pack('<i', len(nb))); f.write(nb)
            f.write(struct.pack('<ii', len(d['pos']), len(d['subs'])))
            f.write(np.ascontiguousarray(d['pos'], '<f4').tobytes())
            f.write(np.ascontiguousarray(d['nrm'], '<f4').tobytes())
            f.write(np.ascontiguousarray(d['uv'], '<f4').tobytes())
            for s in d['subs']:
                f.write(struct.pack('<i', len(s))); f.write(np.ascontiguousarray(s, '<i4').tobytes())


def split_per_submesh(d):
    """서브메시마다 쓰는 정점을 따로 복사(재질 경계를 이음매로). 퇴화 삼각형(같은 정점 반복)은 뺀다."""
    P = []; N = []; U = []; S = []; base = 0
    for idx in d['subs']:
        t = idx.reshape(-1, 3)
        good = (t[:, 0] != t[:, 1]) & (t[:, 1] != t[:, 2]) & (t[:, 0] != t[:, 2])
        t = t[good]
        used, inv = np.unique(t.ravel(), return_inverse=True)
        P.append(d['pos'][used]); N.append(d['nrm'][used]); U.append(d['uv'][used])
        S.append((inv.astype(np.uint32) + base).reshape(-1))
        base += len(used)
    if not P:
        return np.zeros((0, 3), np.float32), np.zeros((0, 3), np.float32), np.zeros((0, 2), np.float32), []
    return np.concatenate(P).astype(np.float32), np.concatenate(N).astype(np.float32), np.concatenate(U).astype(np.float32), S


def simplify(pos, nrm, uv, subs, tol, w_uv, w_n, options=ErrorAbsolute):
    """모든 서브메시를 한 번에 줄임(서브메시는 정점이 따로라 경계가 이음매). 서브메시별 새 인덱스와 결과 오차를 돌려줌."""
    idx = np.concatenate(subs).astype(np.uint32) if subs else np.zeros(0, np.uint32)
    mid = np.concatenate([np.full(len(s) // 3, k, np.int32) for k, s in enumerate(subs)]) if subs else np.zeros(0, np.int32)
    attr = np.ascontiguousarray(np.concatenate([nrm, uv], axis=1).astype(np.float32))
    wts = np.array([w_n, w_n, w_n, w_uv, w_uv], np.float32)
    dst = np.empty(len(idx), np.uint32)
    err = ctypes.c_float(0)
    pos = np.ascontiguousarray(pos, np.float32)
    n = LIB.meshopt_simplifyWithAttributes(dst.ctypes.data, idx.ctypes.data, len(idx), pos.ctypes.data, len(pos), 12,
                                           attr.ctypes.data, 20, wts.ctypes.data, 5, None, 0, ctypes.c_float(tol), options, ctypes.byref(err))
    out = dst[:n].reshape(-1, 3)
    # 결과 삼각형이 어느 서브메시인지: 첫 정점이 속한 서브메시(정점이 서브메시별로 따로라 정확)
    vsub = np.empty(len(pos), np.int32)
    for k, s in enumerate(subs):
        vsub[s] = k
    k_of = vsub[out[:, 0]] if len(out) else np.zeros(0, np.int32)
    return [out[k_of == k].reshape(-1) for k in range(len(subs))], float(err.value)


def compact(pos, nrm, uv, subs):
    used = np.unique(np.concatenate(subs)) if subs and sum(len(s) for s in subs) else np.zeros(0, np.uint32)
    remap = np.full(len(pos), -1, np.int64); remap[used] = np.arange(len(used))
    return pos[used], nrm[used], uv[used], [remap[s].astype(np.uint32) for s in subs]


def surface_samples(pos, tris, n, rng):
    A = pos[tris[:, 0]].astype(np.float64); B = pos[tris[:, 1]].astype(np.float64); C = pos[tris[:, 2]].astype(np.float64)
    area = 0.5 * np.linalg.norm(np.cross(B - A, C - A), axis=1)
    if area.sum() <= 0:
        return A[:0]
    t = rng.choice(len(tris), n, p=area / area.sum())
    u = rng.random(n); v = rng.random(n); f = u + v > 1; u[f] = 1 - u[f]; v[f] = 1 - v[f]
    return A[t] * (1 - u - v)[:, None] + B[t] * u[:, None] + C[t] * v[:, None]


def hausdorff_est(posA, trisA, posB, trisB, n=60000, seed=1):
    """양쪽 표면에서 면적 비례로 점을 뽑아 서로의 점 구름까지 거리(표본 간격만큼 과대평가될 수 있음)의 최대·99.9%."""
    rng = np.random.default_rng(seed)
    SA = np.concatenate([surface_samples(posA, trisA, n, rng), posA[np.unique(trisA)].astype(np.float64)])
    SB = np.concatenate([surface_samples(posB, trisB, n, rng), posB[np.unique(trisB)].astype(np.float64)])
    dA, _ = cKDTree(SB).query(SA); dB, _ = cKDTree(SA).query(SB)
    d = np.concatenate([dA, dB])
    return float(d.max()), float(np.percentile(d, 99.9))


def flat_fraction(d):
    P = d['pos'].astype(np.float64); N = d['nrm'].astype(np.float64)
    t = np.concatenate(d['subs']).reshape(-1, 3).astype(np.int64) if d['subs'] else np.zeros((0, 3), np.int64)
    if len(t) == 0:
        return 1.0
    fn = np.cross(P[t[:, 1]] - P[t[:, 0]], P[t[:, 2]] - P[t[:, 0]]); l = np.linalg.norm(fn, axis=1); ok = l > 1e-12
    if not ok.any():
        return 1.0
    fn = fn[ok] / l[ok, None]; tt = t[ok]
    dots = np.stack([(N[tt[:, k]] * fn).sum(1) for k in range(3)], 1)
    return float((dots > 0.999).all(1).mean())


def weld(pos, nrm, uv, subs, use_normals):
    """같은 (위치, UV[, 법선]) 정점을 합침. 서브메시별로 따로(재질 경계는 이음매로 남음)."""
    P = []; N = []; U = []; S = []; base = 0
    for s in subs:
        t = s.reshape(-1, 3)
        q = [np.round(pos[t.ravel()] * 1e5).astype(np.int64), np.round(uv[t.ravel()] * 1e6).astype(np.int64)]
        if use_normals:
            q.append(np.round(nrm[t.ravel()] * 1e3).astype(np.int64))
        key = np.concatenate(q, 1)
        uk, first, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
        inv = inv.reshape(-1)
        src = t.ravel()[first]
        P.append(pos[src]); N.append(nrm[src]); U.append(uv[src])
        tri = inv.reshape(-1, 3)
        good = (tri[:, 0] != tri[:, 1]) & (tri[:, 1] != tri[:, 2]) & (tri[:, 0] != tri[:, 2])
        S.append((tri[good].astype(np.uint32) + base).reshape(-1)); base += len(src)
    return np.concatenate(P).astype(np.float32), np.concatenate(N).astype(np.float32), np.concatenate(U).astype(np.float32), S


def unweld_flat(pos, uv, subs):
    """삼각형마다 정점 3개 + 면 법선(원래처럼 각진 음영)."""
    P = []; N = []; U = []; S = []; base = 0
    for s in subs:
        t = s.reshape(-1, 3).astype(np.int64)
        p = pos[t].reshape(-1, 3).astype(np.float64)
        fn = np.cross(p[1::3] - p[0::3], p[2::3] - p[0::3]); l = np.linalg.norm(fn, axis=1); l[l < 1e-20] = 1
        fn = (fn / l[:, None]).repeat(3, axis=0)
        P.append(p); N.append(fn); U.append(uv[t].reshape(-1, 2))
        S.append(np.arange(base, base + len(p), dtype=np.uint32)); base += len(p)
    if not P:
        return np.zeros((0, 3), np.float32), np.zeros((0, 3), np.float32), np.zeros((0, 2), np.float32), []
    return np.concatenate(P).astype(np.float32), np.concatenate(N).astype(np.float32), np.concatenate(U).astype(np.float32), S
