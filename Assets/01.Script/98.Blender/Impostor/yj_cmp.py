"""관아 임포스터 확인: 원본(앞쪽+나머지) | 임포스터 를 같은 시점에서. python3 yj_cmp.py <형식> <출력.png> 'ex,ey,ez,tx,ty,tz[,fov];...' [가로]"""
import sys, numpy as np
from PIL import Image
import common
from common import Bld
from rayeval import RMesh
from render_imp import render_orig, render_imp, load_imp
from rt import cam_rays
import yj_build as Y
from load_cx import merge
t, out, vs = sys.argv[1:4]
W = int(sys.argv[4]) if len(sys.argv) > 4 else 640; H = int(W * 9 / 16)
objs, mats = Y.bake_objects(t)
m = merge(objs, list(objs)); m['mats'] = mats
Bo = Bld(m)
IM = load_imp('YJ_' + t)
rm = RMesh(IM['P'], IM['T'], IM['U'], np.zeros(len(IM['T']), np.int32))
rows = []
for v in vs.split(';'):
    a = [float(x) for x in v.split(',')]
    O, D = cam_rays(np.array(a[:3]), np.array(a[3:6]), W, H, fov=a[6] if len(a) > 6 else 50.0)
    A = render_orig(Bo, O, D).reshape(H, W, 3); B = render_imp(IM, rm, O, D).reshape(H, W, 3)
    rows.append(np.concatenate([A, np.full((H, 4, 3), 1.0), B], 1))
img = np.concatenate(rows, 0)
Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(out); print(out)
