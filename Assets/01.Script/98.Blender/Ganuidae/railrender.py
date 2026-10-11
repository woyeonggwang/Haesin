import sys, os
for _d in (os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Impostor'), '/home/claude/impostor'):
    if os.path.isdir(_d): sys.path.insert(0, os.path.normpath(_d))
import numpy as np
from PIL import Image
import common
from railsrc import load_region

def ortho(Bo, z0, z1, y0, y1, ppm, x_from, tmax):
    W = int(round((z1 - z0) * ppm)); H = int(round((y1 - y0) * ppm))
    zs = z0 + (np.arange(W) + 0.5) / ppm; ys = y1 - (np.arange(H) + 0.5) / ppm
    Z, Y = np.meshgrid(zs, ys)
    O = np.stack([np.full(Z.size, x_from), Y.ravel(), Z.ravel()], 1).astype(np.float32)
    D = np.tile(np.array([1, 0, 0], np.float32), (len(O), 1))
    t, tri, u, v = Bo.cast(O, D, tmax)
    hit = tri >= 0
    c, n, tm = Bo.shade(tri, u, v)
    return W, H, hit.reshape(H, W), c.reshape(H, W, 3), n.reshape(H, W, 3), t.reshape(H, W), tm.reshape(H, W)

if __name__ == '__main__':
    b = load_region('Heungnyemun', [(-12, -3, -50), (12, 4, -28)], names_ok=['Hayebdongja', 'BridgePiler', 'SM_HNM_Bridge', 'Dragon'])
    b['mats'] = b['mats']
    Bo = common.Bld(b)
    W, H, hit, c, n, t, tm = ortho(Bo, -45.5, -31.5, -1.0, 1.6, 120, -9.0, 4.6)
    img = np.zeros((H, W, 4), np.uint8); img[..., :3] = (np.clip(c, 0, 1) * 255).astype(np.uint8); img[..., 3] = hit * 255
    Image.fromarray(img).save('rail_side_raw.png')
    # shaded preview
    L = np.array([-0.6, 0.6, -0.5]); L /= np.linalg.norm(L)
    sh = np.clip((n * L).sum(-1), 0, 1) * 0.7 + 0.3
    pv = np.where(hit[..., None], np.clip(c * sh[..., None], 0, 1), np.array([0.2, 0.4, 0.6]))
    Image.fromarray((pv * 255).astype(np.uint8)).save('rail_side_preview.png')
    print(W, H, hit.mean())
    # depth histogram of hits (x position)
    xs = -9.0 + t[hit]
    print('x hits', np.percentile(xs, [1, 25, 50, 75, 99]).round(2))
