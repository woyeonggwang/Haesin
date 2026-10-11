"""행랑 조각(yj_row.py 와 같은 자르기, 조각 좌표) 그려 보기 — 확인용.
python3 yjr_view.py <조각 BAY|GATE|END_W|END_E> <출력.png> 'ex,ey,ez,tx,ty,tz[,fov];...' [가로] [덧댈 obj 파일(조각 좌표)]"""
import sys, numpy as np
from PIL import Image
import common
from common import Bld
from render_imp import render_orig
from rt import cam_rays
import yj_row as R

k, out, vs = sys.argv[1:4]
W = int(sys.argv[4]) if len(sys.argv) > 4 else 640; H = int(W * 9 / 16)
parts, mats = R.row_parts()
x0 = {'BAY': R.XG1, 'GATE': R.XG0, 'END_W': R.XG0, 'END_E': R.XG1}[k]
m = R.merge(parts[k], mats, x0)
extra = None
if len(sys.argv) > 5 and sys.argv[5]:
    import json
    extra = json.load(open(sys.argv[5]))       # {pos, subs, mats}
    base = len(m['pos'])
    m['pos'] = np.concatenate([m['pos'], np.array(extra['pos'], np.float32)])
    m['nrm'] = np.concatenate([m['nrm'], np.array(extra['nrm'], np.float32)])
    m['uv'] = np.concatenate([m['uv'], np.array(extra['uv'], np.float32)])
    for s, mn in zip(extra['subs'], extra['mats']):
        m['subs'].append(np.array(s, np.uint32) + base); m['mats'].append(mn)
name2i = {mm['name']: i for i, mm in enumerate(mats)}
T = []; TM = []
for s, mn in zip(m['subs'], m['mats']):
    t = np.asarray(s, np.int64).reshape(-1, 3); T.append(t); TM.append(np.full(len(t), name2i[mn], np.int32))
b = dict(P=m['pos'].astype(np.float64), N=m['nrm'].astype(np.float64), U=m['uv'].astype(np.float64), T=np.concatenate(T), TM=np.concatenate(TM), mats=mats)
Bo = Bld(b)
rows = []
for v in vs.split(';'):
    a = [float(x) for x in v.split(',')]
    O, D = cam_rays(np.array(a[:3]), np.array(a[3:6]), W, H, fov=a[6] if len(a) > 6 else 50.0)
    rows.append(render_orig(Bo, O, D).reshape(H, W, 3))
img = np.concatenate(rows, 0)
Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(out); print(out)
