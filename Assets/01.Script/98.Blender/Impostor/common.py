"""임포스터 굽기 공용: 경로 설정·텍스처 샘플·원본 광선 추적(Bld).

필요한 것(같은 98.Blender 아래 폴더 또는 작업 폴더): Simplify/(pbsimp.py·rayeval.py·raycast.c.txt → /tmp/libraycast.so 로 컴파일),
Heungnyemun/(load.py·export.py), meshoptimizer 공유 라이브러리(/tmp/libmeshopt.so, MESHOPT_LIB 로 바꿀 수 있음).
"""
import sys, os
import numpy as np
_HERE = os.path.dirname(os.path.abspath(__file__))
for _d in ('../Simplify', '../Heungnyemun', '/home/claude/palace_simplify', '/home/claude/palace_simplify/hnm'):
    _p = os.path.normpath(os.path.join(_HERE, _d))
    if os.path.isdir(_p) and _p not in sys.path:
        sys.path.append(_p)


def first_dir(cands, env):
    if os.environ.get(env):
        return os.environ[env]
    for c in cands:
        if os.path.isdir(c):
            return c
    return cands[-1]

# 데모 씬 원본 내보내기(PalaceBinExport: meshes.bin·instances.tsv·materials.tsv) 폴더들의 부모
DATA = first_dir([r'D:\Blender\Enviroment\PalaceJoseon\FourDoor\Source', '/home/claude/palace_simplify/data'], 'PALACE_DATA')
# 재질 기본색 텍스처를 512² npy(행 0 = v 0)로 줄인 캐시 + <단지>_matmap.json(재질 이름 → npy 파일)
TEXC = first_dir([r'D:\Unity\Haesin\Temp\PalaceSimplify\tex', '/mnt/user-data/uploads/Haesin/Temp/PalaceSimplify/tex'], 'PALACE_TEXC')
from rayeval import RMesh

_tex = {}
def tex(path):
    if path not in _tex:
        _tex[path] = np.load(path)
    return _tex[path]

def sample_wrap(arr, uv):
    H, W = arr.shape[:2]
    x = uv[:, 0] * W - 0.5; y = uv[:, 1] * H - 0.5
    x0 = np.floor(x); y0 = np.floor(y); fx = (x - x0)[:, None].astype(np.float32); fy = (y - y0)[:, None].astype(np.float32)
    x0 = x0.astype(np.int64) % W; y0 = y0.astype(np.int64) % H; x1 = (x0 + 1) % W; y1 = (y0 + 1) % H
    a = arr
    return ((a[y0, x0] * (1 - fx) * (1 - fy) + a[y0, x1] * fx * (1 - fy) + a[y1, x0] * (1 - fx) * fy + a[y1, x1] * fx * fy) / 255.0).astype(np.float32)

_png = {}
PNG_MAX = 1024     # 원본 텍스처(2048~4096)를 바로 읽을 때 줄이는 크기 — 굽기 밀도(32 px/m)에는 충분
def png_rgba(path):
    """그림 파일 → RGBA uint8, 행 0 = v 0(아래). 한 변이 PNG_MAX 보다 크면 줄여서 둠(메모리)."""
    if path not in _png:
        from PIL import Image
        im = Image.open(path).convert('RGBA')
        if max(im.size) > PNG_MAX:
            k = PNG_MAX / max(im.size)
            im = im.resize((max(1, int(im.size[0] * k)), max(1, int(im.size[1] * k))), Image.LANCZOS)
        _png[path] = np.ascontiguousarray(np.asarray(im)[::-1])
    return _png[path]


def mat_rgba(m, uv):
    """재질 색 RGBA(텍스처 × 재질 색). 텍스처: m['png'](RGBA PNG) 또는 m['npy'](RGB 512² 캐시)."""
    uv2 = uv * m['st'][:2] + m['st'][2:]
    if m.get('png'):
        c = sample_wrap(png_rgba(m['png']), uv2)
    elif m.get('npy'):
        c3 = sample_wrap(tex(m['npy']), uv2)[:, :3]
        c = np.concatenate([c3, np.ones((len(uv), 1), np.float32)], 1)
    else:
        c = np.ones((len(uv), 4), np.float32)
    return c * m['color']


def mat_color(m, uv):
    return mat_rgba(m, uv)[:, :3]


class Bld:
    """원본 광선 추적: b = dict(P, N, U, T, TM, mats)"""
    def __init__(self, b):
        self.b = b
        self.rm = RMesh(b['P'], b['T'], b['U'], b['TM'], vn=b['N'])
        self.clipmat = np.array([m['clip'] for m in b['mats']], bool)
        self.cutoff = np.array([m['cutoff'] for m in b['mats']], np.float32)

    def cast(self, O, D, tmax=100.0, layers=6):
        """알파 컷 재질(판)의 뚫린 곳은 건너뛰고 그 뒤를 봄"""
        O = np.ascontiguousarray(O, np.float32); D = np.ascontiguousarray(D, np.float32)
        t, tri, u, v = self.rm.cast(O, D, tmax)
        if not self.clipmat.any():
            return t, tri, u, v
        travelled = np.zeros(len(O), np.float32)
        todo = np.arange(len(O))
        for _ in range(layers):
            h = tri[todo] >= 0
            cand = todo[h]
            if len(cand) == 0:
                break
            tm = self.b['TM'][tri[cand]]
            cm = self.clipmat[tm]
            cand = cand[cm]
            if len(cand) == 0:
                break
            uv = self.rm.hit_uv(tri[cand], u[cand], v[cand])
            a = np.zeros(len(cand), np.float32)
            tmc = self.b['TM'][tri[cand]]
            for mi in np.unique(tmc):
                s = tmc == mi
                a[s] = mat_rgba(self.b['mats'][mi], uv[s])[:, 3]
            cut = a < self.cutoff[tmc]
            again = cand[cut]
            if len(again) == 0:
                break
            step = t[again] + 1e-3
            O2 = O[again] + D[again] * step[:, None]
            travelled[again] += step
            t2, tri2, u2, v2 = self.rm.cast(O2, D[again], tmax)
            t[again] = np.where(tri2 >= 0, t2 + travelled[again], t2); tri[again] = tri2; u[again] = u2; v[again] = v2
            todo = again
        return t, tri, u, v

    def shade(self, tri, u, v):
        """맞은 곳 기본색(텍스처 sRGB 값 그대로), 법선, 재질 번호"""
        ok = tri >= 0
        col = np.zeros((len(tri), 3), np.float32)
        uv = self.rm.hit_uv(tri, u, v)
        tm = self.b['TM'][np.maximum(tri, 0)]
        for m in np.unique(tm[ok]):
            s = ok & (tm == m)
            col[s] = mat_color(self.b['mats'][m], uv[s])
        nrm = self.rm.hit_normal(tri, u, v)
        return col, nrm, tm
