"""관아 형식 앞쪽(원래 메시)만 / 전체를 여러 시점에서 그려 보기. python3 yj_view.py <형식> <front|all|back> <출력.png> 'ex,ey,ez,tx,ty,tz[,fov];...' [가로]"""
import sys, numpy as np
from PIL import Image
import common
from common import Bld
from render_imp import render_orig
from rt import cam_rays
import yj_build as Y
t, what, out, vs = sys.argv[1:5]
W = int(sys.argv[5]) if len(sys.argv) > 5 else 640; H = int(W * 9 / 16)
objs, mats, front, bake, st = Y.split_type(t)
parts = dict(front) if what in ('front', 'all') else {}
if what in ('all', 'back'):
    for k, v in bake.items():
        parts['B_' + k] = v
m, T, TM = Y.merge_subset(parts, mats)
b = dict(P=m['pos'].astype(np.float64), N=m['nrm'].astype(np.float64), U=m['uv'].astype(np.float64), T=T, TM=TM, mats=mats)
Bo = Bld(b)
rows = []
for v in vs.split(';'):
    a = [float(x) for x in v.split(',')]
    O, D = cam_rays(np.array(a[:3]), np.array(a[3:6]), W, H, fov=a[6] if len(a) > 6 else 50.0)
    rows.append(render_orig(Bo, O, D).reshape(H, W, 3))
img = np.concatenate(rows, 0)
Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(out); print(out)
