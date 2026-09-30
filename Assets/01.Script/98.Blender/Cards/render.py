"""확인용 간이 렌더러: 데모 씬 데이터(원본/줄인 것/판 적용)를 원근 카메라로 광선 추적(알베도 + 간단한 음영).
python3 render.py <cx> <variant: orig|reduced|cards> <cam x,y,z> <look x,y,z> <out.png> [fov] [w,h] [crop radius]"""
import sys, os, json
import numpy as np
from PIL import Image
import os
_HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(_HERE, '..')); sys.path.insert(0, os.path.join(_HERE, '..', 'Simplify')); sys.path.insert(0, _HERE)
from pbsimp import read_bin
from rayeval import RMesh
from cardbake import load_tex, sample

ROOT = '/home/claude/palace_simplify/'
TEXC = '/mnt/user-data/uploads/Haesin/Temp/PalaceSimplify/tex/'


def load(cx, variant, center=None, radius=None):
    D = ROOT + 'data/' + cx
    ms = read_bin(D + '/meshes.bin')
    red = read_bin(ROOT + 'out/%s/reduced.bin' % cx) if variant != 'orig' else None
    cards = {}; removed = set()
    if variant == 'cards':
        C = ROOT + 'out_cards/%s/' % cx
        cb = read_bin(C + 'cards.bin')
        rows = [l.rstrip('\n').split('\t') for l in open(C + 'cards.tsv', encoding='utf-8')][1:]
        for r, m in zip(rows, cb):
            cards[int(r[0])] = (m, r)
        for l in open(C + 'removed.tsv', encoding='utf-8'):
            removed.add(int(l.split('\t')[0]))
    matmap = json.load(open(TEXC + '%s_matmap.json' % cx, encoding='utf-8'))
    mats = []; mat_id = {}

    def mid(key, info):
        if key not in mat_id:
            mat_id[key] = len(mats); mats.append(info)
        return mat_id[key]
    P = []; T = []; U = []; M_ = []; base = 0
    for i, line in enumerate(open(D + '/instances.tsv', encoding='utf-8')):
        p = line.rstrip('\n').split('\t'); k = int(p[0])
        if i in removed:
            continue
        nm = ms[k]['name']
        if nm.endswith('_Ground') or nm == 'Plane':
            continue
        Mx = np.array([float(x) for x in p[2:18]]).reshape(4, 4)
        inst_mats = p[18].split('|')
        if k in cards:
            m, r = cards[k]
            keep = [int(x) for x in r[8].split(',') if x != ''] if r[2] == 'split' else []
            mnames = [inst_mats[s] for s in keep] + [r[5]]
            tex_of = {r[5]: ROOT + 'out_cards/%s/Cards/%s' % (cx, r[6])}
            clip_of = {r[5]: r[7] == '1'}
        else:
            m = ms[k] if variant == 'orig' else red[k]
            mnames = inst_mats; tex_of = {}; clip_of = {}
        pos = m['pos'].astype(np.float64)
        if center is not None:
            c = (Mx @ np.append((pos.min(0) + pos.max(0)) / 2, 1))[:3]
            if np.linalg.norm(c - center) > radius:
                continue
        wp = (np.concatenate([pos, np.ones((len(pos), 1))], 1) @ Mx.T)[:, :3]
        P.append(wp.astype(np.float32)); U.append(m['uv'])
        for s, idx in enumerate(m['subs']):
            if len(idx) == 0:
                continue
            mn = mnames[s] if s < len(mnames) else ''
            if mn in tex_of:
                info = dict(path=tex_of[mn], npy=False, clip=clip_of[mn])
            else:
                f = matmap.get(mn)
                info = dict(path=(TEXC + f) if f else None, npy=True, clip=False)
            T.append(idx.reshape(-1, 3).astype(np.int64) + base)
            M_.append(np.full(len(idx) // 3, mid(mn, info)))
        base += len(pos)
    return np.concatenate(P), np.concatenate(T).astype(np.int32), np.concatenate(U).astype(np.float32), np.concatenate(M_).astype(np.int32), mats


_npy = {}


def albedo(info, uv):
    if info['path'] is None:
        return np.full((len(uv), 4), 0.6, np.float32)
    if info['npy']:
        if info['path'] not in _npy:
            a = np.load(info['path']).astype(np.float32) / 255.0
            if a.shape[2] == 3:
                a = np.concatenate([a, np.ones(a.shape[:2] + (1,), np.float32)], 2)
            _npy[info['path']] = a
        return sample(_npy[info['path']], uv)
    return sample(load_tex(info['path']), uv)


def render(cx, variant, cam, look, out, fov=60.0, W=960, H=640, radius=None):
    cam = np.array(cam, float); look = np.array(look, float)
    P, T, U, TM, mats = load(cx, variant, center=cam if radius else None, radius=radius)
    rm = RMesh(P, T, U, TM)
    f = look - cam; f /= np.linalg.norm(f)
    up = np.array([0, 1.0, 0]) if abs(f[1]) < 0.95 else np.array([0, 0, 1.0])
    r = np.cross(up, f); r /= np.linalg.norm(r); u = np.cross(f, r)
    th = np.tan(np.radians(fov) / 2)
    xs = (np.arange(W) + 0.5) / W * 2 - 1; ys = 1 - (np.arange(H) + 0.5) / H * 2
    X, Y = np.meshgrid(xs * th * W / H, ys * th)
    D = (f[None, :] + X.reshape(-1, 1) * r[None, :] + Y.reshape(-1, 1) * u[None, :])
    D /= np.linalg.norm(D, axis=1)[:, None]
    O = np.repeat(cam[None, :], len(D), 0)
    col = np.tile(np.array([0.62, 0.74, 0.90], np.float32), (len(D), 1))
    todo = np.arange(len(D)); Oc = O.astype(np.float32).copy(); Dc = D.astype(np.float32)
    L = np.array([0.35, 0.85, 0.4]); L /= np.linalg.norm(L)
    for layer in range(6):
        if len(todo) == 0:
            break
        t, tri, uu, vv = rm.cast(Oc[todo], Dc[todo], 5000.0)
        hit = tri >= 0
        idx = todo[hit]
        if len(idx) == 0:
            break
        uvh = rm.hit_uv(tri[hit], uu[hit], vv[hit]); mi = TM[tri[hit]]
        c = np.zeros((len(idx), 4), np.float32)
        for m in np.unique(mi):
            s = mi == m
            c[s] = albedo(mats[m], uvh[s])
        keep = np.ones(len(idx), bool)
        for m in np.unique(mi):
            if mats[m]['clip']:
                s = np.where(mi == m)[0]
                keep[s[c[s, 3] < 0.5]] = False
        n = rm.fn[tri[hit]]
        sh = 0.45 + 0.55 * np.abs(n @ L)
        ok = idx[keep]
        col[ok] = c[keep, :3] * sh[keep, None]
        again = idx[~keep]
        Oc[again] = Oc[again] + Dc[again] * (t[hit][~keep] + 1e-4)[:, None]
        todo = again
    rm.free()
    img = (np.clip(col.reshape(H, W, 3), 0, 1) * 255).astype(np.uint8)
    Image.fromarray(img).save(out)
    return len(T)


if __name__ == '__main__':
    a = sys.argv
    cx, variant = a[1], a[2]
    cam = [float(x) for x in a[3].split(',')]; look = [float(x) for x in a[4].split(',')]
    fov = float(a[6]) if len(a) > 6 else 60.0
    W, H = (int(x) for x in a[7].split(',')) if len(a) > 7 else (960, 640)
    rad = float(a[8]) if len(a) > 8 else None
    n = render(cx, variant, cam, look, a[5], fov, W, H, rad)
    print('tris', n, '->', a[5])
