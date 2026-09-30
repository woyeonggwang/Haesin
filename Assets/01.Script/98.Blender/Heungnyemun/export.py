"""흥례문 결과 메시 정리·저장.

finalize: 서브메시(재질, 각짐 여부)를 재질별 서브메시로 합치고 법선을 채움
  - 각진 음영(#F·판): 삼각형 면 법선, (위치·UV·법선)이 같은 정점은 합침
  - 부드러운 음영: 저장된 정점 법선
write_meshes: 'HNMS' 이진 파일(Unity 좌표 그대로)
  int32 매직 0x534D4E48, int32 메시 수
  메시마다: 이름(int32 길이 + UTF-8), int32 정점 수 nv, int32 서브메시 수 ns,
            float32 위치 nv*3, float32 법선 nv*3, float32 UV nv*2,
            서브메시마다: 재질 이름(int32 길이 + UTF-8), int32 인덱스 수 ni, int32 인덱스 ni
"""
import struct
import numpy as np


def finalize(m, min_area=1e-10):
    P = []; N = []; U = []; per_mat = {}; order = []; base = 0
    for idx, mn, flat in zip(m['subs'], m['mats'], m['flats']):
        t = np.asarray(idx, np.int64).reshape(-1, 3)
        if len(t) == 0:
            continue
        pos = m['pos']; A = pos[t[:, 0]]; B = pos[t[:, 1]]; C = pos[t[:, 2]]
        fn = np.cross(B - A, C - A); ar = 0.5 * np.linalg.norm(fn, axis=1)
        ok = ar > min_area
        t = t[ok]; fn = fn[ok]
        if len(t) == 0:
            continue
        if flat:
            fn = fn / np.linalg.norm(fn, axis=1)[:, None]
            p = pos[t].reshape(-1, 3); uv = m['uv'][t].reshape(-1, 2); n = np.repeat(fn, 3, axis=0)
            key = np.concatenate([np.round(p * 1e4), np.round(uv * 1e5), np.round(n * 1e3)], 1).astype(np.int64)
            uk, first, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
            inv = inv.reshape(-1)
            vp = p[first]; vn = n[first]; vu = uv[first]; ii = inv
        else:
            used, inv = np.unique(t.ravel(), return_inverse=True)
            vp = pos[used]; vn = m['nrm'][used]; vu = m['uv'][used]; ii = inv.reshape(-1)
            l = np.linalg.norm(vn, axis=1)
            bad = l < 1e-6
            if bad.any():
                # 법선이 없는 정점: 붙은 면 법선 평균
                acc = np.zeros_like(vp)
                tt = ii.reshape(-1, 3)
                for k in range(3):
                    np.add.at(acc, tt[:, k], fn)
                al = np.linalg.norm(acc, axis=1); al[al < 1e-12] = 1
                vn = np.where(bad[:, None], acc / al[:, None], vn)
            l = np.linalg.norm(vn, axis=1); l[l < 1e-12] = 1; vn = vn / l[:, None]
        P.append(vp); N.append(vn); U.append(vu)
        if mn not in per_mat:
            per_mat[mn] = []; order.append(mn)
        per_mat[mn].append(ii.astype(np.int64) + base)
        base += len(vp)
    if not P:
        return None
    return dict(pos=np.concatenate(P).astype(np.float32), nrm=np.concatenate(N).astype(np.float32), uv=np.concatenate(U).astype(np.float32),
                subs=[np.concatenate(per_mat[mn]).astype(np.int32) for mn in order], mats=order)


def write_meshes(path, meshes):
    """meshes = [(이름, finalize 결과)]"""
    with open(path, 'wb') as f:
        f.write(struct.pack('<ii', 0x534D4E48, len(meshes)))
        for name, m in meshes:
            nb = name.encode('utf-8'); f.write(struct.pack('<i', len(nb))); f.write(nb)
            f.write(struct.pack('<ii', len(m['pos']), len(m['subs'])))
            f.write(np.ascontiguousarray(m['pos'], '<f4').tobytes())
            f.write(np.ascontiguousarray(m['nrm'], '<f4').tobytes())
            f.write(np.ascontiguousarray(m['uv'], '<f4').tobytes())
            for mn, s in zip(m['mats'], m['subs']):
                b = mn.encode('utf-8'); f.write(struct.pack('<i', len(b))); f.write(b)
                f.write(struct.pack('<i', len(s))); f.write(np.ascontiguousarray(s, '<i4').tobytes())


def read_meshes(path):
    buf = open(path, 'rb').read(); off = 0

    def i32():
        nonlocal off
        v = struct.unpack_from('<i', buf, off)[0]; off += 4; return v
    assert i32() == 0x534D4E48
    n = i32(); out = []
    for _ in range(n):
        ln = i32(); name = buf[off:off + ln].decode('utf-8'); off += ln
        nv = i32(); ns = i32()
        pos = np.frombuffer(buf, '<f4', nv * 3, off).reshape(-1, 3); off += nv * 12
        nrm = np.frombuffer(buf, '<f4', nv * 3, off).reshape(-1, 3); off += nv * 12
        uv = np.frombuffer(buf, '<f4', nv * 2, off).reshape(-1, 2); off += nv * 8
        subs = []; mats = []
        for _ in range(ns):
            lm = i32(); mats.append(buf[off:off + lm].decode('utf-8')); off += lm
            ni = i32(); subs.append(np.frombuffer(buf, '<i4', ni, off)); off += ni * 4
        out.append((name, dict(pos=pos, nrm=nrm, uv=uv, subs=subs, mats=mats)))
    return out
