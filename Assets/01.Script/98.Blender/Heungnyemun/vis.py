"""흥례문: 밖에서 보이는 삼각형 찾기.

- 장면(계단 뺌, 땅 포함)을 광선 추적용으로 모으고, 바깥 공기 복셀장(voxfield)을 만든다.
- 삼각형마다 표면 점(작은 삼각형 1~4점, 큰 삼각형은 0.2 m 간격)을 찍고, 전역 방향 96개로 평행 광선을 쏜다(점마다 그 방향 쪽 면에서).
- 광선이 아무것도 안 맞고 빠져나가거나, 맞기 전 광선 위의 공기가 '바깥 공기'면 그 점은 보임 → 삼각형은 보임.
- 판(카드) 삼각형은 한쪽 면만 그려지므로 저장된 법선 쪽 방향으로만 검사한다.
결과: vis_keep.npy (장면 삼각형 순서의 bool), vis_scene_meta.npz(삼각형 → 배치·서브메시·원래 삼각형 번호)
"""
import sys, os, time
import numpy as np
_HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, _HERE)
from scene import *
from load import load, xform
from voxfield import build_field, STAIR, Field

OUT = os.path.join(_HERE, 'work')
os.makedirs(OUT, exist_ok=True)


def fib_dirs(n):
    i = np.arange(n) + 0.5
    phi = np.arccos(1 - 2 * i / n); th = np.pi * (1 + 5 ** 0.5) * i
    return np.stack([np.cos(th) * np.sin(phi), np.cos(phi), np.sin(th) * np.sin(phi)], 1)


def spread_order(D):
    """앞에서부터 고르게 퍼지도록 방향 순서를 섞음(가장 먼 방향 먼저)."""
    n = len(D); order = [int(np.argmax(D[:, 1]))]; md = np.full(n, 9.0)
    for _ in range(n - 1):
        md = np.minimum(md, np.linalg.norm(D - D[order[-1]], axis=1))
        md[order] = -1
        order.append(int(np.argmax(md)))
    return D[order]


def sample_points(P, T, small=0.03, mid=0.10, step=0.25, cap=200, seed=1):
    """삼각형마다 표면 점. 작은 것(<3 cm) 1점, 중간(<10 cm) 2점(가운데 + 가장 긴 변 양 끝 쪽), 큰 것은 길이·넓이에 맞춰 3~200점.
    반환 (점 N×3 float32, 삼각형 번호 N int32)."""
    rng = np.random.default_rng(seed)
    A = P[T[:, 0]].astype(np.float64); B = P[T[:, 1]].astype(np.float64); C = P[T[:, 2]].astype(np.float64)
    e = np.stack([np.linalg.norm(B - A, axis=1), np.linalg.norm(C - B, axis=1), np.linalg.norm(A - C, axis=1)], 1)
    me = e.max(1)
    area = 0.5 * np.linalg.norm(np.cross(B - A, C - A), axis=1)
    cen = (A + B + C) / 3
    pts = []; tid = []
    s1 = np.where(me < mid)[0]
    pts.append(cen[s1]); tid.append(s1)
    s2 = np.where((me >= small) & (me < mid))[0]
    # 가장 긴 변의 두 끝점 쪽(가운데로 35% 당김)
    k = np.argmax(e[s2], axis=1)
    V = np.stack([A[s2], B[s2], C[s2]], 1)
    p0 = V[np.arange(len(s2)), k]; p1 = V[np.arange(len(s2)), (k + 1) % 3]
    c2 = cen[s2]
    pts.append(p0 + 0.65 * (c2 - p0)); tid.append(s2)
    pts.append(p1 + 0.65 * (c2 - p1)); tid.append(s2)
    big = np.where(me >= mid)[0]
    n = np.clip(np.maximum(np.ceil(me[big] / step) + 1, np.ceil(area[big] / (step * step))), 3, cap).astype(np.int64)
    pts.append(cen[big]); tid.append(big)
    rep = np.repeat(big, n - 1)
    r1 = rng.random(len(rep)); r2 = rng.random(len(rep))
    flip = r1 + r2 > 1; r1[flip] = 1 - r1[flip]; r2[flip] = 1 - r2[flip]
    u = 0.03 + 0.94 * r1; w = 0.03 + 0.94 * r2; s = 1 - u - w
    pts.append(s[:, None] * A[rep] + u[:, None] * B[rep] + w[:, None] * C[rep]); tid.append(rep)
    return np.concatenate(pts).astype(np.float32), np.concatenate(tid).astype(np.int32)


def run(v=0.1, ndir=64, batch_dirs=8, log=print):
    t0 = time.time()
    inst, mats, red = load()
    names = [red[i['k']]['name'].split('#')[0] for i in inst]
    sc = build_scene(inst, red, keep=lambda ii, i: names[ii] not in STAIR, skip_ground=False)
    isg = np.isin(sc.TI, [ii for ii, n in enumerate(names) if n == 'SM_HNM_Ground'])
    log('scene tris', len(sc.T), 'ground', int(isg.sum()))
    # 땅 삼각형
    GT = sc.T[isg]
    ground = (sc.P, GT)
    F = build_field(sc if True else None, ground, v=v, opaque_mats=('CARD_HNM_149',), log=log)
    rm = RMesh(sc.P, sc.T, sc.U, sc.TM)
    log('bvh %.0fs' % (time.time() - t0))
    # 판 삼각형: 한쪽 면(저장 법선 쪽)
    iscard = np.array([m['name'].startswith('CARD_') for m in sc.mats])[sc.TM]
    ct = np.where(iscard)[0]
    front = np.zeros((len(sc.T), 3), np.float32)
    fn = sc.N[sc.T[ct]].mean(1)
    fn /= np.maximum(np.linalg.norm(fn, axis=1), 1e-9)[:, None]
    front[ct] = fn
    log('card tris', len(ct))
    P, tid = sample_points(sc.P, sc.T)
    log('sample points', len(P), 'tris', len(sc.T), '%.0fs' % (time.time() - t0))
    vis = np.zeros(len(sc.T), bool)
    vis[isg] = True                      # 땅은 늘 남김
    active = ~vis[tid]
    D = spread_order(fib_dirs(ndir)).astype(np.float64)
    taus = np.array([0.15, 0.3, 0.6, 1.2, 2.4, 4.8, 9.6, 19.2, 38.4], np.float64)
    nrays = 0
    for di, d in enumerate(D):
        idx = np.where(active)[0]
        if len(idx) == 0:
            break
        t_ = tid[idx]
        n = rm.fn[t_].astype(np.float64)
        dn = n @ d
        # 판: 앞면 쪽만
        cf = iscard[t_]
        ok = np.abs(dn) > 0.1
        if cf.any():
            fd = front[t_[cf]].astype(np.float64) @ d
            okc = fd > 0.1
            tmp = ok[cf]; tmp &= okc; ok[cf] = tmp
        idx = idx[ok]; t_ = t_[ok]; n = n[ok]; dn = dn[ok]
        if len(idx) == 0:
            continue
        side = np.sign(dn)
        O = P[idx].astype(np.float64) + n * (side * 0.002)[:, None]
        # 공간 순서로 정렬(캐시)
        q = np.floor((O - O.min(0)) / 2.0).astype(np.int64)
        key = (q[:, 0] * 4096 + q[:, 2]) * 4096 + q[:, 1]
        order = np.argsort(key, kind='stable')
        idx = idx[order]; O = O[order]
        Dd = np.tile(d, (len(O), 1))
        t, tri, _, _ = rm.cast(O.astype(np.float32), Dd.astype(np.float32), 400.0)
        nrays += len(O)
        seen = tri < 0
        hitd = t.astype(np.float64)
        for tau in taus:
            m = (~seen) & (hitd > tau + 0.1)
            if not m.any():
                break
            val = F.value(O[m] + d * tau)
            seen[np.where(m)[0][val == 2]] = True
        vt = np.unique(tid[idx[seen]])
        vis[vt] = True
        active &= ~vis[tid]
        if di % batch_dirs == batch_dirs - 1 or di == len(D) - 1:
            log('dir %d/%d rays %.1fM visible tris %d (%.1f%%) active pts %d  %.0fs' % (di + 1, len(D), nrays / 1e6, vis.sum(), 100 * vis.mean(), active.sum(), time.time() - t0))
    rm.free()
    np.save(os.path.join(OUT, 'vis_keep.npy'), vis)
    np.savez(os.path.join(OUT, 'vis_scene_meta.npz'), TI=sc.TI, TS=sc.TS, ntri=len(sc.T))
    return vis, sc


if __name__ == '__main__':
    run()
