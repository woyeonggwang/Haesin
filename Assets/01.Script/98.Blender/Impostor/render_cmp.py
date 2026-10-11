"""원본 vs 껍데기(구운 그림) 같은 시점 비교 그림 — 게임 화면(1080p, 세로 시야 60°)과 같은 픽셀 밀도, 3×3 초표본."""
import numpy as np, sys
from PIL import Image
from load_bld import load_building
from common import Bld, sample_wrap
from rt import RMesh
SUN = np.array([-0.45, 0.75, -0.48]); SUN /= np.linalg.norm(SUN)
SKY = np.array([0.62, 0.72, 0.85])
PX_DEG = 60.0 / 1080


def rays(eye, target, W, H, fov, jit):
    f = target - eye; f /= np.linalg.norm(f)
    r = np.cross(np.array([0, 1.0, 0]), f); r /= np.linalg.norm(r); u = np.cross(f, r)
    t = np.tan(np.radians(fov) / 2)
    xs = ((np.arange(W) + 0.5 + jit[0]) / W * 2 - 1) * t * W / H; ys = (1 - (np.arange(H) + 0.5 + jit[1]) / H * 2) * t
    X, Y = np.meshgrid(xs, ys)
    D = f[None] + X.reshape(-1, 1) * r[None] + Y.reshape(-1, 1) * u[None]
    D /= np.linalg.norm(D, axis=1)[:, None]
    return np.tile(eye, (len(D), 1)).astype(np.float32), D.astype(np.float32)


def light(alb, n, ao, P, occ_rm_list):
    ndl = np.clip(n @ SUN, 0, 1)
    lit = np.ones(len(P), bool)
    for rm in occ_rm_list:
        t, tri, _, _ = rm.cast((P + n * 0.02).astype(np.float32), np.tile(SUN.astype(np.float32), (len(P), 1)), 300.0)
        lit &= tri < 0
    amb = 0.45 * (0.55 + 0.45 * n[:, 1]) * ao
    lin = (alb ** 2.2) * (ndl * lit * 1.1 + amb)[:, None]
    return np.clip(lin, 0, 1) ** (1 / 2.2)


def render(kind, eye, target, fov, W, H, ctx, ss=3):
    acc = np.zeros((W * H, 3))
    for jx in range(ss):
        for jy in range(ss):
            O, D = rays(eye, target, W, H, fov, ((jx + 0.5) / ss - 0.5, (jy + 0.5) / ss - 0.5))
            col = np.tile(SKY, (len(O), 1))
            if kind == 'orig':
                B = ctx['B']
                t, tri, u, v = B.cast(O, D, 2000.0); hit = tri >= 0
                if hit.any():
                    c, n, tm = B.shade(tri[hit], u[hit], v[hit])
                    fl = (n * D[hit]).sum(1) > 0; n[fl] *= -1
                    P = O[hit] + D[hit] * t[hit][:, None]
                    col[hit] = light(c, n, np.ones(len(n)), P, [B.rm])
            else:
                R = ctx['R']
                t, tri, u, v = R.cast(O, D, 2000.0); hit = tri >= 0
                if hit.any():
                    uv = R.hit_uv(tri[hit], u[hit], v[hit])
                    c = sample_wrap(ctx['BC'], uv)[:, :3]
                    n = sample_wrap(ctx['NO'], uv)[:, :3] * 2 - 1; n /= np.maximum(np.linalg.norm(n, axis=1), 1e-6)[:, None]
                    ao = sample_wrap(ctx['MK'], uv)[:, 1]
                    P = O[hit] + D[hit] * t[hit][:, None]
                    col[hit] = light(c, n, ao, P, [R])
            acc += col
    return (acc / (ss * ss)).reshape(H, W, 3)


def load_ctx(name='YCM_BLD_L1'):
    b = load_building(); B = Bld(b)
    m = np.load('out/%s_uvmesh.npz' % name)
    R = RMesh(m['V'], m['F'], m['UV'], np.zeros(len(m['F']), np.int32))
    def img(p, ch):
        a = np.asarray(Image.open(p))[::-1]   # 행 0 = v 0
        return np.ascontiguousarray(a[..., :ch] if a.ndim == 3 else a[..., None])
    BC = img('out/%s_Imp_BC.png' % name, 3); NO = img('out/%s_Imp_NOS.png' % name, 3); MK = img('out/%s_Imp_MASK.png' % name, 4)
    return dict(B=B, R=R, BC=BC, NO=NO, MK=MK)


if __name__ == '__main__':
    ctx = load_ctx()
    c = np.array([55.0, 9.3, 12.0])
    eye_y = 7.4 + 1.6
    shots = []
    for dist, az in [(30, 20), (70, 20), (70, 65), (120, 35)]:
        a = np.radians(az)
        eye = np.array([c[0] + dist * np.cos(a), eye_y, c[2] - dist * np.sin(a)])
        span = 28.0          # 건물 둘레 폭(m)
        fov = np.degrees(2 * np.arctan(span / 2 / dist)); H = int(round(fov / PX_DEG)); W = int(H * 1.6)
        A = render('orig', eye, c, fov, W, H, ctx); Bh = render('hull', eye, c, fov, W, H, ctx)
        shots.append((dist, az, A, Bh))
        print(dist, az, W, H)
    # 나란히(각 줄 = 한 시점: 왼쪽 원본, 오른쪽 껍데기), 보기 좋게 같은 높이로 키움(최근접)
    rows = []
    for dist, az, A, Bh in shots:
        k = max(1, int(round(360 / A.shape[0])))
        A2 = np.repeat(np.repeat(A, k, 0), k, 1); B2 = np.repeat(np.repeat(Bh, k, 0), k, 1)
        gap = np.ones((A2.shape[0], 12, 3))
        rows.append(np.concatenate([A2, gap, B2], 1))
    Wm = max(r.shape[1] for r in rows)
    rows = [np.pad(r, ((0, 12), (0, Wm - r.shape[1]), (0, 0)), constant_values=1) for r in rows]
    img = np.concatenate(rows, 0)
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save('cmp_bake.png')
    print(img.shape)
