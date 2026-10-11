"""일본성 LOD0 | LOD1 | LOD2 비교 그림(실내·성문 제외). python3 jc_cmp.py out.png"""
import os, sys, json, numpy as np
os.environ.setdefault('IMPOSTOR_DATA', '/home/claude/jplod/data'); os.environ.setdefault('IMPOSTOR_TEX', '/home/claude/jplod/data/tex')
_HERE = os.path.dirname(os.path.abspath(__file__))
for _d in (_HERE, os.path.join(_HERE, '..', 'Heungnyemun'), '/home/claude/jplod', '/home/claude/impostor', '/home/claude/palace_simplify/hnm'):
    if os.path.isdir(_d): sys.path.append(os.path.normpath(_d))
from PIL import Image
import common
from rayeval import RMesh
from load_cx import load_complex, merge
from common import Bld, mat_color
from rt import cam_rays
from render_imp import light, sample, render_orig
from export import read_meshes
import jc_lod as J
OUT = 'out_castle'
objs0, mats = load_complex('JPCastle'); mname = [m['name'] for m in mats]
objsB = J.triplanar_uv(objs0, mats)
names = [n for n in objsB if J.group_of(n) not in ('INTERIOR',)]
m = merge(objsB, names); m['mats'] = mats; Bo = Bld(m)
lay = json.load(open(OUT + '/layout.json'))
BC = [np.asarray(Image.open(OUT + '/P%d_BC.png' % k).convert('RGB'))[::-1].astype(np.float32) / 255 for k in range(lay['pages'])]
NO = [np.asarray(Image.open(OUT + '/P%d_NOS.png' % k).convert('RGB'))[::-1].astype(np.float32) / 255 * 2 - 1 for k in range(lay['pages'])]
L = dict(read_meshes(OUT + '/JPCastle_LOD.bin'))
TPS = {k: v for k, v in J.TRIPLANAR.items()}
def build(level):
    P = []; N = []; U = []; T = []; TM = []; b = 0
    for g in ['TENSHU', 'YAGURA_NE', 'YAGURA_SW', 'WALL']:
        mm = L['%s_LOD%d' % (g, level)]
        for mn, s in zip(mm['mats'], mm['subs']):
            t = np.asarray(s).reshape(-1, 3) + b; T.append(t)
            TM.append(np.full(len(t), -1 - int(mn.rsplit('_P', 1)[1]) if mn.startswith('IMP_') else mname.index(mn)))
        P.append(mm['pos'].astype(np.float64)); N.append(mm['nrm']); U.append(mm['uv']); b += len(mm['pos'])
    # 성문(원본)
    for n in objsB:
        if J.group_of(n) == 'GATE':
            o = objsB[n]; T.append(o['T'] + b); TM.append(o['TM']); P.append(o['P']); N.append(o['N']); U.append(o['U']); b += len(o['P'])
    P = np.concatenate(P); N = np.concatenate(N).astype(np.float64); U = np.concatenate(U).astype(np.float64); T = np.concatenate(T); TM = np.concatenate(TM)
    return dict(P=P, TM=TM, rm=RMesh(P, T, U, np.maximum(TM, 0).astype(np.int32), vn=N), T=T)
def render(S, O, D):
    rm = S['rm']; t, tri, u, v = rm.cast(O, D, 3000.0); hit = tri >= 0
    col = np.full((len(O), 3), (0.62, 0.72, 0.85), np.float32)
    uv = rm.hit_uv(tri, u, v); tm = S['TM'][np.maximum(tri, 0)]
    alb = np.zeros((len(O), 3), np.float32); n = rm.hit_normal(tri, u, v).astype(np.float32)
    flip = (n * D).sum(1) > 0; n[flip] *= -1
    for k in range(len(BC)):
        s = hit & (tm == -1 - k); alb[s] = sample(BC[k], uv[s]); n[s] = sample(NO[k], uv[s])
    # 원래 재질(삼면 투영은 위치로)
    P = O + D * t[:, None]
    for k in np.unique(tm[hit & (tm >= 0)]):
        s = hit & (tm == k); sc = 0
        for pre, v_ in TPS.items():
            if mname[k].startswith(pre): sc = v_
        if sc:
            nn = np.abs(n[s]); ax = nn.argmax(1); p = P[s]
            uvt = np.where((ax == 0)[:, None], p[:, [2, 1]], np.where((ax == 1)[:, None], p[:, [0, 2]], p[:, [0, 1]])) * sc
            alb[s] = mat_color(mats[k], uvt)
        else:
            alb[s] = mat_color(mats[k], uv[s])
    n /= np.maximum(np.linalg.norm(n, axis=1), 1e-9)[:, None]
    col[hit] = light(alb, n, hit, rm, O, D, t)[hit]
    return col
S1 = build(1); S2 = build(2)
views = [((-60, 30, 90), (-15, 15, 10), 30), ((140, 60, -40), (0, 15, 0), 30), ((-25, 12, -70), (-25, 22, 25), 40), ((300, 120, 250), (0, 10, 0), 22)]
H = int(sys.argv[2]) if len(sys.argv) > 2 else 260; W = int(H * 1.5); rows = []
for eye, tgt, fov in views:
    O, D = cam_rays(np.array(eye, float), np.array(tgt, float), W, H, fov=fov)
    a = render_orig(Bo, O, D).reshape(H, W, 3); b = render(S1, O, D).reshape(H, W, 3); c = render(S2, O, D).reshape(H, W, 3)
    print(eye, 'diff L1 %.1f L2 %.1f' % (np.abs(a - b).mean() * 255, np.abs(a - c).mean() * 255))
    rows.append(np.concatenate([a, np.ones((H, 3, 3)), b, np.ones((H, 3, 3)), c], 1))
Image.fromarray((np.clip(np.concatenate(rows, 0), 0, 1) * 255).astype(np.uint8)).save(sys.argv[1])
