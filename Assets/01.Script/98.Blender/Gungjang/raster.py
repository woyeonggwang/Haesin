"""아주 단순한 확인용 렌더러(정사영, z 버퍼, 면 법선 음영). 삼각형 목록 → PNG"""
import numpy as np
from PIL import Image

COL = {'M_GHM_Ws02A': (0.62, 0.58, 0.52), 'M_GHM_Ws03A': (0.80, 0.30, 0.25), 'M_GHM_Top01A': (0.45, 0.47, 0.50),
       'MAT_RoofPlate_Black': (0.08, 0.08, 0.08), 'WING': (0.35, 0.55, 0.75)}


def render(tris, eye_dir, up, center, width_m, px=1400, aspect=0.4, light=(0.3, 0.8, -0.5), bg=(1, 1, 1), path=None, backface=True):
    """tris: list of (mat, P(3,3)). eye_dir: 보는 방향(카메라 → 장면)."""
    f = np.asarray(eye_dir, float); f /= np.linalg.norm(f)
    r = np.cross(f, up); r /= np.linalg.norm(r)
    u = np.cross(r, f)
    W = px; H = int(px * aspect)
    scale = W / width_m
    img = np.ones((H, W, 3)) * np.array(bg)
    zb = np.full((H, W), np.inf)
    lt = np.asarray(light, float); lt /= np.linalg.norm(lt)
    center = np.asarray(center, float)
    for mat, P in tris:
        P = np.asarray(P, float)
        n = np.cross(P[1] - P[0], P[2] - P[0])
        nn = np.linalg.norm(n)
        if nn < 1e-12:
            continue
        n /= nn
        facing = np.dot(n, -f)
        if backface and facing <= 0:
            continue
        d = P - center
        sx = d @ r * scale + W / 2
        sy = -(d @ u) * scale + H / 2
        sz = d @ f
        x0, x1 = int(max(0, np.floor(sx.min()))), int(min(W - 1, np.ceil(sx.max())))
        y0, y1 = int(max(0, np.floor(sy.min()))), int(min(H - 1, np.ceil(sy.max())))
        if x0 > x1 or y0 > y1:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        den = (sy[1] - sy[2]) * (sx[0] - sx[2]) + (sx[2] - sx[1]) * (sy[0] - sy[2])
        if abs(den) < 1e-12:
            continue
        l1 = ((sy[1] - sy[2]) * (xs - sx[2]) + (sx[2] - sx[1]) * (ys - sy[2])) / den
        l2 = ((sy[2] - sy[0]) * (xs - sx[2]) + (sx[0] - sx[2]) * (ys - sy[2])) / den
        l3 = 1 - l1 - l2
        m = (l1 >= -1e-9) & (l2 >= -1e-9) & (l3 >= -1e-9)
        if not m.any():
            continue
        z = l1 * sz[0] + l2 * sz[1] + l3 * sz[2]
        sub = zb[y0:y1 + 1, x0:x1 + 1]
        w = m & (z < sub)
        if not w.any():
            continue
        sub[w] = z[w]
        shade = 0.35 + 0.65 * abs(np.dot(n, lt))
        c = np.array(COL.get(mat, (0.7, 0.7, 0.2))) * shade
        img[y0:y1 + 1, x0:x1 + 1][w] = c
    im = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    if path:
        im.save(path)
    return im
