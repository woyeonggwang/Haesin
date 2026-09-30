"""흥례문 작업용 장면: 배치된 메시를 한 덩어리로 모으고(삼각형마다 배치·서브메시 번호 유지) 광선 추적으로 확인 그림을 그린다."""
import sys, os, json
import numpy as np
from PIL import Image
_HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(_HERE, '..')); sys.path.insert(0, os.path.join(_HERE, '..', 'Simplify')); sys.path.insert(0, os.path.join(_HERE, '..', 'cards')); sys.path.insert(0, _HERE)
from load import load, xform, ROOT, CX
from rayeval import RMesh
from cardbake import load_tex, sample

TEXC = '/mnt/user-data/uploads/Haesin/Temp/PalaceSimplify/tex/'


class Scene:
    """parts = [(이름, pos(N,3) 월드, uv(N,2), [(재질이름, 삼각형(M,3))...], 배치번호)] 를 모은다."""

    def __init__(self):
        self.P = []; self.T = []; self.U = []; self.N = []; self.TM = []; self.TI = []; self.TS = []
        self.base = 0; self.mat_id = {}; self.mats = []
        self.matmap = json.load(open(TEXC + '%s_matmap.json' % CX, encoding='utf-8'))
        self.cardtex = {}

    def mid(self, name):
        if name not in self.mat_id:
            self.mat_id[name] = len(self.mats)
            if name in self.cardtex:
                info = dict(name=name, path=self.cardtex[name][0], npy=False, clip=self.cardtex[name][1])
            elif name.startswith('BLACK') or name == 'MAT_RoofPlate_Black':
                info = dict(name=name, path=None, npy=False, clip=False, color=(0.03, 0.03, 0.03, 1))
            else:
                f = self.matmap.get(name)
                info = dict(name=name, path=(TEXC + f) if f else None, npy=True, clip=False)
            self.mats.append(info)
        return self.mat_id[name]

    def add(self, pos, uv, subs, matnames, inst_id=-1, nrm=None):
        """subs = 삼각형 인덱스 배열 목록(서브메시별), matnames = 서브메시별 재질 이름."""
        self.P.append(np.asarray(pos, np.float32)); self.U.append(np.asarray(uv, np.float32))
        self.N.append(np.zeros((len(pos), 3), np.float32) if nrm is None else np.asarray(nrm, np.float32))
        for s, idx in enumerate(subs):
            t = np.asarray(idx).reshape(-1, 3)
            if len(t) == 0:
                continue
            self.T.append(t.astype(np.int64) + self.base)
            self.TM.append(np.full(len(t), self.mid(matnames[s] if s < len(matnames) else ''), np.int32))
            self.TI.append(np.full(len(t), inst_id, np.int32)); self.TS.append(np.full(len(t), s, np.int32))
        self.base += len(pos)

    def finish(self):
        self.P = np.concatenate(self.P); self.U = np.concatenate(self.U); self.N = np.concatenate(self.N)
        self.T = np.concatenate(self.T).astype(np.int32); self.TM = np.concatenate(self.TM)
        self.TI = np.concatenate(self.TI); self.TS = np.concatenate(self.TS)
        return self


def card_info(red):
    """판 메시의 재질 이름 → (텍스처 경로, 알파 컷)."""
    out = {}
    for d in red:
        r = d.get('card_row')
        if r:
            out[r[5]] = (os.path.join(ROOT, 'out_cards', CX, 'Cards', r[6]), r[7] == '1')
    return out


def instance_mats(i, d):
    """배치 하나의 서브메시별 재질 이름(판이면 남긴 서브메시 재질 + 판 재질)."""
    r = d.get('card_row')
    if r:
        keep = [int(x) for x in r[8].split(',') if x != ''] if r[2] == 'split' else []
        return [i['mats'][s] for s in keep] + [r[5]]
    return i['mats']


def build_scene(inst, red, keep=None, skip_ground=True):
    sc = Scene(); sc.cardtex = card_info(red)
    for ii, i in enumerate(inst):
        if keep is not None and not keep(ii, i):
            continue
        d = red[i['k']]
        nm = d['name'].split('#')[0]
        if skip_ground and (nm.endswith('_Ground') or nm == 'Plane'):
            continue
        W = xform(i['M'], d['pos'].astype(np.float64))
        N = d['nrm'].astype(np.float64) @ i['M'][:3, :3].T
        sc.add(W, d['uv'], d['subs'], instance_mats(i, d), ii, N)
    return sc.finish()


_npy = {}


def albedo(info, uv):
    if info.get('color') is not None:
        return np.tile(np.array(info['color'], np.float32), (len(uv), 1))
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


def render(sc, cam, look, out, fov=60.0, W=960, H=640, rm=None, highlight=None):
    """highlight = 삼각형 bool 배열(빨갛게 칠함)."""
    cam = np.array(cam, float); look = np.array(look, float)
    own = rm is None
    if own:
        rm = RMesh(sc.P, sc.T, sc.U, sc.TM)
    f = look - cam; f /= np.linalg.norm(f)
    up = np.array([0, 1.0, 0]) if abs(f[1]) < 0.95 else np.array([0, 0, 1.0])
    r = np.cross(up, f); r /= np.linalg.norm(r); u = np.cross(f, r)
    th = np.tan(np.radians(fov) / 2)
    xs = (np.arange(W) + 0.5) / W * 2 - 1; ys = 1 - (np.arange(H) + 0.5) / H * 2
    X, Y = np.meshgrid(xs * th * W / H, ys * th)
    D = (f[None, :] + X.reshape(-1, 1) * r[None, :] + Y.reshape(-1, 1) * u[None, :])
    D /= np.linalg.norm(D, axis=1)[:, None]
    col = np.tile(np.array([0.62, 0.74, 0.90], np.float32), (len(D), 1))
    todo = np.arange(len(D)); Oc = np.repeat(cam[None, :], len(D), 0).astype(np.float32); Dc = D.astype(np.float32)
    L = np.array([0.35, 0.85, 0.4]); L /= np.linalg.norm(L)
    for layer in range(8):
        if len(todo) == 0:
            break
        t, tri, uu, vv = rm.cast(Oc[todo], Dc[todo], 5000.0)
        hit = tri >= 0
        idx = todo[hit]
        if len(idx) == 0:
            break
        uvh = rm.hit_uv(tri[hit], uu[hit], vv[hit]); mi = sc.TM[tri[hit]]
        c = np.zeros((len(idx), 4), np.float32)
        for m in np.unique(mi):
            s = mi == m
            c[s] = albedo(sc.mats[m], uvh[s])
        keep = np.ones(len(idx), bool)
        for m in np.unique(mi):
            if sc.mats[m]['clip']:
                s = np.where(mi == m)[0]
                keep[s[c[s, 3] < 0.5]] = False
        n = rm.fn[tri[hit]]
        sh = 0.45 + 0.55 * np.abs(n @ L)
        cc = c[:, :3] * sh[:, None]
        if highlight is not None:
            hl = highlight[tri[hit]]
            cc[hl] = cc[hl] * 0.3 + np.array([0.7, 0, 0], np.float32)
        ok = idx[keep]
        col[ok] = cc[keep]
        again = idx[~keep]
        Oc[again] = Oc[again] + Dc[again] * (t[hit][~keep] + 1e-4)[:, None]
        todo = again
    if own:
        rm.free()
    img = (np.clip(col.reshape(H, W, 3), 0, 1) * 255).astype(np.uint8)
    Image.fromarray(img).save(out)
