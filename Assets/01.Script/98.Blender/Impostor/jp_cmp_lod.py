"""LOD0(원본) | LOD1 | LOD2 를 같은 시점에서(4K 화면 화소 밀도로) 나란히. python3 cmp_lod.py out.png dist"""
import os, sys, json, numpy as np
# 입력: IMPOSTOR_DATA(JPTown 폴더의 부모), IMPOSTOR_TEX(tex/index.tsv), JPLOD_OUT(결과 폴더)
os.environ.setdefault('IMPOSTOR_DATA', '/home/claude/jplod/data'); os.environ.setdefault('IMPOSTOR_TEX', '/home/claude/jplod/data/tex')
_HERE = os.path.dirname(os.path.abspath(__file__))
for _d in (_HERE, os.path.join(_HERE, '..', 'Heungnyemun'), '/home/claude/impostor', '/home/claude/palace_simplify/hnm'):
    if os.path.isdir(_d): sys.path.append(os.path.normpath(_d))
from PIL import Image
import common
from rayeval import RMesh
from load_cx import load_complex, merge
from common import Bld, mat_color
from rt import cam_rays
from render_imp import light, sample, render_orig
from export import read_meshes
objs, mats = load_complex('JPTown'); mname = [m['name'] for m in mats]
m = merge(objs, list(objs)); m['mats'] = mats; Bo = Bld(m)
lay = json.load(open('out/layout.json'))
BC = np.asarray(Image.open('out/P0_BC.png').convert('RGB'))[::-1].astype(np.float32) / 255
NO = np.asarray(Image.open('out/P0_NOS.png').convert('RGB'))[::-1].astype(np.float32) / 255 * 2 - 1
L = dict(read_meshes('out/JPTown_LOD.bin'))
def build(suffix):
    P = []; N = []; U = []; T = []; TM = []; b = 0
    for h, off in lay['offs'].items():
        mm = L[h + suffix]; p = mm['pos'].astype(np.float64).copy(); p[:, 0] += off
        for mn, s in zip(mm['mats'], mm['subs']):
            t = np.asarray(s).reshape(-1, 3) + b; T.append(t); TM.append(np.full(len(t), -1 if mn.startswith('IMP_') else mname.index(mn)))
        P.append(p); N.append(mm['nrm']); U.append(mm['uv']); b += len(p)
    P = np.concatenate(P); N = np.concatenate(N).astype(np.float64); U = np.concatenate(U).astype(np.float64); T = np.concatenate(T); TM = np.concatenate(TM)
    return dict(P=P, N=N, U=U, T=T, TM=TM, rm=RMesh(P, T, U, np.maximum(TM, 0).astype(np.int32), vn=N))
def render(S, O, D):
    rm = S['rm']; t, tri, u, v = rm.cast(O, D, 3000.0); hit = tri >= 0
    col = np.full((len(O), 3), (0.62, 0.72, 0.85), np.float32)
    uv = rm.hit_uv(tri, u, v); tm = S['TM'][np.maximum(tri, 0)]
    alb = np.zeros((len(O), 3), np.float32); n = rm.hit_normal(tri, u, v).astype(np.float32)
    flip = (n * D).sum(1) > 0; n[flip] *= -1
    s = hit & (tm < 0); alb[s] = sample(BC, uv[s]); n[s] = sample(NO, uv[s])
    for k in np.unique(tm[hit & (tm >= 0)]):
        s = hit & (tm == k); alb[s] = mat_color(mats[k], uv[s])
    n /= np.maximum(np.linalg.norm(n, axis=1), 1e-9)[:, None]
    col[hit] = light(alb, n, hit, rm, O, D, t)[hit]
    return col
S1 = build('_LOD1'); S2 = build('_LOD2')
out = sys.argv[1]; dist = float(sys.argv[2]); H = int(sys.argv[3]) if len(sys.argv) > 3 else 300
fov = float(os.environ.get('FOV', H * 60.0 / 2160.0)); W = int(H * 1.5)
houses = sys.argv[4].split(',') if len(sys.argv) > 4 else list(lay['offs'])
rows = []
for h in houses:
    off = lay['offs'][h]
    ang = np.radians(35); eye = np.array([off + dist * np.sin(ang), 1.7 + dist * 0.05, -dist * np.cos(ang)]); tgt = np.array([off, 3.2, 0])
    O, D = cam_rays(eye, tgt, W, H, fov=fov)
    a = render_orig(Bo, O, D).reshape(H, W, 3); b = render(S1, O, D).reshape(H, W, 3); c = render(S2, O, D).reshape(H, W, 3)
    print(h, 'diff L1 %.1f L2 %.1f' % (np.abs(a - b).mean() * 255, np.abs(a - c).mean() * 255))
    rows.append(np.concatenate([a, np.ones((H, 3, 3)), b, np.ones((H, 3, 3)), c], 1))
img = np.concatenate(rows, 0)
Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(out); print(img.shape)
