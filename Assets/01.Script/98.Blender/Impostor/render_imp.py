"""굽은 단지 임포스터 확인 그림: 원본(텍스처 색) | 임포스터(구운 색 + 구운 노멀) 를 같은 시점에서 나란히.
python3 render_imp.py <단지> <출력.png> [시점 목록: 'ex,ey,ez,tx,ty,tz;...'] [가로]"""
import os, sys, numpy as np
from PIL import Image
import common
from rayeval import RMesh
from load_cx import load_complex, merge
from common import Bld
from rt import cam_rays, SUN

HERE = os.path.dirname(os.path.abspath(__file__))


def load_imp(cx):
    from impcx import OUTROOT
    od = os.path.join(OUTROOT, cx)
    z = np.load(os.path.join(od, 'uvmesh.npz'))
    names = sorted({k.split('|')[0] for k in z.files})
    import json
    lay = json.load(open(os.path.join(od, 'layout.json'), encoding='utf-8'))
    P = []; U = []; T = []; PG = []; base = 0
    for nm in names:
        pos = z[nm + '|pos']; uv = z[nm + '|uv']
        pages = lay['objects'][nm]['pages']
        for si, pg in enumerate(pages):
            t = z[nm + '|sub%d' % si].reshape(-1, 3)
            T.append(t + base); PG.append(np.full(len(t), pg, np.int32))
        P.append(pos); U.append(uv); base += len(pos)
    P = np.concatenate(P).astype(np.float64); U = np.concatenate(U).astype(np.float64); T = np.concatenate(T); PG = np.concatenate(PG)
    BC = []; NO = []
    for pg in range(lay['pages']):
        BC.append(np.asarray(Image.open(os.path.join(od, 'P%d_BC.png' % pg)).convert('RGB'))[::-1].astype(np.float32) / 255)
        NO.append(np.asarray(Image.open(os.path.join(od, 'P%d_NOS.png' % pg)).convert('RGB'))[::-1].astype(np.float32) / 255 * 2 - 1)
    return dict(P=P, U=U, T=T, PG=PG, BC=BC, NO=NO, lay=lay)


def sample(img, uv):
    H, W = img.shape[:2]
    x = np.clip((uv[:, 0] * W).astype(int), 0, W - 1); y = np.clip((uv[:, 1] * H).astype(int), 0, H - 1)
    return img[y, x]


def light(alb, n, hit, rm_shadow, O, D, t):
    ndl = np.clip(n @ SUN, 0, 1)
    if rm_shadow is not None and hit.any():
        Pp = O[hit] + D[hit] * t[hit][:, None] + n[hit] * 0.02
        _, tr2, _, _ = rm_shadow.cast(Pp.astype(np.float32), np.tile(SUN.astype(np.float32), (len(Pp), 1)), 300.0)
        ndl = ndl.copy(); ndl[hit] = ndl[hit] * (tr2 < 0)
    lin = alb ** 2.2 * (ndl[:, None] * 1.0 + 0.35 * (0.5 + 0.5 * n[:, 1:2]))
    return np.clip(lin, 0, 1) ** (1 / 2.2)


def render_orig(Bo, O, D):
    t, tri, u, v = Bo.cast(O, D, 2000.0)
    hit = tri >= 0
    col = np.full((len(O), 3), (0.62, 0.72, 0.85), np.float32)
    c, n, tm = Bo.shade(tri, u, v)
    flip = (n * D).sum(1) > 0; n[flip] *= -1
    col[hit] = light(c, n, hit, Bo.rm, O, D, t)[hit]
    return col


def render_imp(IM, rm, O, D):
    t, tri, u, v = rm.cast(O, D, 2000.0)
    hit = tri >= 0
    col = np.full((len(O), 3), (0.62, 0.72, 0.85), np.float32)
    uv = rm.hit_uv(tri, u, v)
    pg = IM['PG'][np.maximum(tri, 0)]
    alb = np.zeros((len(O), 3), np.float32); n = np.zeros((len(O), 3), np.float32); n[:, 1] = 1
    for k in range(len(IM['BC'])):
        s = hit & (pg == k)
        alb[s] = sample(IM['BC'][k], uv[s]); n[s] = sample(IM['NO'][k], uv[s])
    n /= np.maximum(np.linalg.norm(n, axis=1), 1e-9)[:, None]
    col[hit] = light(alb, n, hit, rm, O, D, t)[hit]
    return col


if __name__ == '__main__':
    cx = sys.argv[1]; out = sys.argv[2]
    W = int(sys.argv[4]) if len(sys.argv) > 4 else 640; H = int(W * 9 / 16)
    objs, mats = load_complex(cx)
    m = merge(objs, [n for n in objs]); m['mats'] = mats
    Bo = Bld(m)
    IM = load_imp(cx)
    # 원본 그대로 둔 것(땅판·작은 오브젝트)도 임포스터 장면에 넣음
    keep = IM['lay']['keep']
    kP = [IM['P']]; kT = [IM['T']]; kU = [IM['U']]; kPG = [IM['PG']]; base = len(IM['P'])
    rm = RMesh(IM['P'], IM['T'], IM['U'], np.zeros(len(IM['T']), np.int32))
    views = []
    for vs in sys.argv[3].split(';'):
        a = [float(x) for x in vs.split(',')]
        views.append((np.array(a[:3]), np.array(a[3:6]), a[6] if len(a) > 6 else 50.0))
    rows = []
    for eye, tgt, fov in views:
        O, D = cam_rays(eye, tgt, W, H, fov=fov)
        a = render_orig(Bo, O, D).reshape(H, W, 3); b = render_imp(IM, rm, O, D).reshape(H, W, 3)
        diff = np.abs(a - b).mean() * 255
        print('view', eye.round(1), 'mean abs diff %.1f' % diff)
        rows.append(np.concatenate([a, np.full((H, 4, 3), 1.0), b], 1))
    img = np.concatenate(rows, 0)
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(out); print(out, img.shape)
