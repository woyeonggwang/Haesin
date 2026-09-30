"""흥례문 보임 검사 2단계(1단계에서 놓친 것 되살리기).

1) 지붕 훑기: 지붕 재질 표면에서 3 cm 간격(넓이 비례) 꼴로 점을 뽑아, 위에서 내려다보는 13방향(수직 + 고도 55° 방위 8 + 고도 30° 방위 4)으로
   모든 부재보다 높은 곳에서 평행 광선을 쏜다. 처음 맞은 삼각형 = 보임(기와 사이 머리카락 틈으로 보이는 아래 부재).
2) 사람 눈높이: 걸을 수 있는 곳(마당·행각 복도·문 통로·문 앞) 4 m 격자, 눈높이 1.7 m 에서 모든 방향 광선(점마다 4만 개).
   처음 맞은 삼각형 = 보임(벽·창 틈으로 보이는 것).
결과: work/vis_keep2.npy
"""
import sys, os, time
import numpy as np
_HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, _HERE)
from scene import *
from load import load, xform
from voxfield import STAIR

OUT = os.path.join(_HERE, 'work')
ROOF_MATS = ('MI_HNM_RoofTile', 'MI_HNM_RoofLine', 'MI_HNM_YeonDan_A', 'MI_HNM_YeonDan_B', 'MI_HNM_YeonDan_C', 'MI_HNM_RoofWall', 'MI_HNM_RoofOrnament')


def area_samples_chunks(P, T, spacing, rng, chunk=1500000):
    """넓이 비례로 표면 점을 뽑아 chunk 개씩 돌려줌(메모리 절약)."""
    A = P[T[:, 0]].astype(np.float64); B = P[T[:, 1]].astype(np.float64); C = P[T[:, 2]].astype(np.float64)
    ar = 0.5 * np.linalg.norm(np.cross(B - A, C - A), axis=1)
    n = int(ar.sum() / (spacing * spacing))
    cdf = np.cumsum(ar) / ar.sum()
    done = 0
    while done < n:
        m = min(chunk, n - done)
        t = np.searchsorted(cdf, rng.random(m))
        t = np.minimum(t, len(T) - 1)
        u = rng.random(m); v = rng.random(m); f = u + v > 1; u[f] = 1 - u[f]; v[f] = 1 - v[f]
        yield A[t] * (1 - u - v)[:, None] + B[t] * u[:, None] + C[t] * v[:, None]
        done += m


def run(log=print):
    t0 = time.time()
    inst, mats, red = load()
    names = [red[i['k']]['name'].split('#')[0] for i in inst]
    sc = build_scene(inst, red, keep=lambda ii, i: names[ii] not in STAIR, skip_ground=False)
    vis = np.load(os.path.join(OUT, 'vis_keep.npy'))
    assert len(vis) == len(sc.T)
    rm = RMesh(sc.P, sc.T, sc.U, sc.TM)
    # 알파 판(구멍 있는 판)은 광선이 지나갈 수 있어야 하지만, 첫 맞음만 보므로 판 뒤는 되살리지 않음(판 구멍은 작고 대부분 창틀 바깥)
    log('bvh %.0fs' % (time.time() - t0))
    rng = np.random.default_rng(7)
    added = np.zeros(len(vis), bool)
    # 1) 지붕 훑기
    roof = np.isin(sc.TM, [sc.mat_id[m] for m in ROOF_MATS if m in sc.mat_id])
    dirs = [np.array([0, -1.0, 0])]
    for el, naz, off in ((55, 8, 22.5), (30, 4, 0.0)):
        for az in range(naz):
            a = np.radians(az * 360.0 / naz + off); e = np.radians(el)
            dirs.append(np.array([np.cos(e) * np.cos(a), -np.sin(e), np.cos(e) * np.sin(a)]))
    n1 = 0
    for d in dirs:
        nn = 0; nnew = 0
        for S in area_samples_chunks(sc.P, sc.T[roof], 0.03, rng):
            L = np.maximum(5.0, (25.0 - S[:, 1]) / (-d[1]))          # 모든 부재보다 높은 곳(바깥 공기)에서 출발
            O = S - d[None, :] * L[:, None]
            q = np.floor((O - O.min(0)) / 1.0).astype(np.int64)
            order = np.argsort((q[:, 0] * 8192 + q[:, 2]) * 64 + q[:, 1], kind='stable'); O = O[order]
            t, tri, _, _ = rm.cast(O.astype(np.float32), np.tile(d, (len(O), 1)).astype(np.float32), 200.0)
            h = tri[tri >= 0]
            new = np.unique(h[~vis[h] & ~added[h]])
            added[new] = True; n1 += len(O); nn += len(O); nnew += len(new)
        log('roof dir (%.2f,%.2f,%.2f) rays %.1fM new %d total added %d  %.0fs' % (d[0], d[1], d[2], nn / 1e6, nnew, added.sum(), time.time() - t0))
    # 2) 사람 눈높이
    GP = sc.P; ground = np.isin(sc.TI, [ii for ii, n in enumerate(names) if n == 'SM_HNM_Ground'])
    xs = np.arange(-70, 70.1, 4.0); zs = np.arange(-95, 40.1, 4.0)
    X, Z = np.meshgrid(xs, zs); X = X.ravel(); Z = Z.ravel()
    # 바닥 높이: 위에서 아래로 쏴서 처음 맞은 곳(지붕 밑이면 지붕에 맞으므로 두 번째 이후도 봄)
    views = []
    for x, z in zip(X, Z):
        o = np.array([[x, 40.0, z]], np.float32)
        for _ in range(12):
            t, tri, _, _ = rm.cast(o, np.array([[0, -1, 0]], np.float32), 100.0)
            if tri[0] < 0:
                break
            y = o[0, 1] - t[0]
            n = rm.fn[tri[0]]
            # 위를 보는 면 + 그 위 2 m 가 비어 있으면 걸을 수 있는 바닥으로 봄
            if abs(n[1]) > 0.8:
                e = np.array([[x, y + 0.05, z]], np.float32)
                tu, triu, _, _ = rm.cast(e, np.array([[0, 1, 0]], np.float32), 2.2)
                if triu[0] < 0:
                    views.append((x, y + 1.7, z))
            o = np.array([[x, y - 0.01, z]], np.float32)
    views = np.array(views)
    # 바깥 공기에 있는 눈 위치만(문루 2층 같은 닫힌 방 안은 뺌)
    from voxfield import Field
    meta = np.load(os.path.join(_HERE, 'field_meta.npy')); g = np.load(os.path.join(_HERE, 'field_g.npy'))
    F = Field(meta[:3], meta[3], tuple(int(x) for x in meta[4:7]), g)
    ok = F.value(views) == 2
    log('eye viewpoints', len(views), 'in exterior air', int(ok.sum()), '%.0fs' % (time.time() - t0))
    views = views[ok]
    nd = 40000
    i = np.arange(nd) + 0.5
    phi = np.arccos(1 - 2 * i / nd); th = np.pi * (1 + 5 ** 0.5) * i
    D0 = np.stack([np.cos(th) * np.sin(phi), np.cos(phi), np.sin(th) * np.sin(phi)], 1)
    n2 = 0
    for k in range(0, len(views), 20):
        V = views[k:k + 20]
        O = np.repeat(V, nd, axis=0)
        R = rng.normal(size=(3, 3)); Q, _ = np.linalg.qr(R)       # 점마다 방향 틀 돌림(같은 틈을 여러 각도로)
        D = np.tile(D0 @ Q.T, (len(V), 1))
        t, tri, _, _ = rm.cast(O.astype(np.float32), D.astype(np.float32), 400.0)
        h = tri[tri >= 0]
        new = np.unique(h[~vis[h]])
        added[new] = True; n2 += len(O)
        if (k // 20) % 20 == 0:
            log('eye %d/%d rays %.1fM added %d  %.0fs' % (k + len(V), len(views), n2 / 1e6, added.sum(), time.time() - t0))
    rm.free()
    vis2 = vis | added
    log('added %d tris (roof pass rays %.1fM, eye pass rays %.1fM); visible %d -> %d' % (added.sum(), n1 / 1e6, n2 / 1e6, vis.sum(), vis2.sum()))
    np.save(os.path.join(OUT, 'vis_keep2.npy'), vis2)
    np.save(os.path.join(OUT, 'vis_eye_views.npy'), views)


if __name__ == '__main__':
    run()
