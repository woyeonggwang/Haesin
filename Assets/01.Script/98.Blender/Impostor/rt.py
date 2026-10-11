"""광선 추적 확인 그림: 원근 카메라, 해 방향 램버트 + 하늘빛. 원본(기본색)·껍데기(굽기 전 회색/굽기 후 텍스처) 비교용."""
import numpy as np
from PIL import Image
import common
from rayeval import RMesh

SUN = np.array([-0.45, 0.75, -0.48]); SUN /= np.linalg.norm(SUN)


def cam_rays(eye, target, W, H, fov=40.0):
    f = target - eye; f /= np.linalg.norm(f)
    r = np.cross(np.array([0, 1.0, 0]), f); r /= np.linalg.norm(r)        # 왼손 좌표: right = up × forward
    u = np.cross(f, r)
    t = np.tan(np.radians(fov) / 2)
    xs = (np.arange(W) + 0.5) / W * 2 - 1; ys = 1 - (np.arange(H) + 0.5) / H * 2
    X, Y = np.meshgrid(xs * t * W / H, ys * t)
    D = f[None, :] + X.reshape(-1, 1) * r[None, :] + Y.reshape(-1, 1) * u[None, :]
    D /= np.linalg.norm(D, axis=1)[:, None]
    O = np.tile(eye, (len(D), 1))
    return O.astype(np.float32), D.astype(np.float32)


def shade(rm, O, D, albedo_fn=None, shadow=True):
    t, tri, u, v = rm.cast(O, D, 1000.0)
    hit = tri >= 0
    n = rm.hit_normal(tri, u, v)
    flip = (n * D).sum(1) > 0
    n[flip] *= -1
    col = np.full((len(O), 3), (0.62, 0.72, 0.85), np.float32)
    if albedo_fn is None:
        alb = np.full((len(O), 3), 0.7, np.float32)
    else:
        alb = albedo_fn(tri, u, v, n)
    ndl = np.clip(n @ SUN, 0, 1)
    if shadow and hit.any():
        P = O[hit] + D[hit] * t[hit][:, None] + n[hit] * 0.01
        ts, tris2, _, _ = rm.cast(P.astype(np.float32), np.tile(SUN.astype(np.float32), (len(P), 1)), 200.0)
        lit = tris2 < 0
        ndl_h = ndl[hit] * lit
        ndl = ndl.copy(); ndl[hit] = ndl_h
    lin = alb ** 2.2 * (ndl[:, None] * 1.0 + 0.35 * (0.5 + 0.5 * n[:, 1:2]))
    c = np.clip(lin, 0, 1) ** (1 / 2.2)
    col[hit] = c[hit]
    return col


def save(col, W, H, path):
    Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8).reshape(H, W, 3)).save(path)
