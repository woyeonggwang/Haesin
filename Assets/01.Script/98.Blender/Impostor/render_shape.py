"""껍데기 모양만(회색) 원본과 나란히: python3 render_shape.py <단지> <출력.png> '시점;…' [가로]"""
import sys, os, json, numpy as np
from PIL import Image
import common
from rayeval import RMesh
from load_cx import load_complex, merge
from common import Bld
from rt import cam_rays, shade
import render_imp as RI
cx = sys.argv[1]; out = sys.argv[2]; W = int(sys.argv[4]) if len(sys.argv) > 4 else 560; H = int(W * 9 / 16)
objs, mats = load_complex(cx)
m = merge(objs, list(objs)); m['mats'] = mats
Bo = Bld(m)
from impcx import OUTROOT
od = os.path.join(OUTROOT, cx)
z = np.load(os.path.join(od, 'uvmesh.npz')); lay = json.load(open(os.path.join(od, 'layout.json'), encoding='utf-8'))
P = []; T = []; base = 0
for nm in lay['objects']:
    P.append(z[nm + '|pos'])
    for si in range(len(lay['objects'][nm]['pages'])):
        T.append(z[nm + '|sub%d' % si].reshape(-1, 3) + base)
    base += len(z[nm + '|pos'])
P = np.concatenate(P).astype(np.float64); T = np.concatenate(T)
rm = RMesh(P, T, np.zeros((len(P), 2)), np.zeros(len(T), np.int32))
rows = []
for vs in sys.argv[3].split(';'):
    a = [float(x) for x in vs.split(',')]
    O, D = cam_rays(np.array(a[:3]), np.array(a[3:6]), W, H, fov=a[6] if len(a) > 6 else 50)
    rows.append(np.concatenate([shade(Bo.rm, O, D).reshape(H, W, 3), np.ones((H, 4, 3)), shade(rm, O, D).reshape(H, W, 3)], 1))
img = np.concatenate(rows, 0)
Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(out); print(out, img.shape)
