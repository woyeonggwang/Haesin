"""모서리 조각(YJR_Corner.bin) 그려 보기 — 확인용.
python3 yjc_view.py <LOD 번호> <출력.png> 'ex,ey,ez,tx,ty,tz[,fov];...' [가로] [덧붙일 이름:dx:dz:rot90 ...(조각 BAY 를 옆에 붙여 보기)]"""
import sys, os, numpy as np
from PIL import Image
import common
from common import Bld
from render_imp import render_orig
from rt import cam_rays
import yj_row as R
import yj_corner as C
from impcx import OUTROOT

lv, out, vs = sys.argv[1:4]
W = int(sys.argv[4]) if len(sys.argv) > 4 else 640; H = int(W * 9 / 16)
parts, mats = R.row_parts()
cm = C.read_hnms(os.path.join(OUTROOT, 'YJR', 'YJR_Corner.bin'))['YJR_CORNER_LOD%s' % lv]
ms = [cm]
bay = R.merge(parts['BAY'], mats, R.XG1)
bay = dict(bay, pos=bay['pos'].astype(np.float64))
for spec in sys.argv[5:]:
    nm, dx, dz, r = spec.split(':'); dx = float(dx); dz = float(dz)
    src = bay if nm == 'BAY' else R.merge(parts[nm], mats, {'GATE': R.XG0, 'END_W': R.XG0, 'END_E': R.XG1}[nm])
    src = dict(src, pos=np.asarray(src['pos'], np.float64))
    if r == '1':   # 옆 날개 방향: (u, v) → (K - v, u + dz)
        m = C.xform(src, lambda p: np.stack([C.K - p[:, 2], p[:, 1], p[:, 0] + dz], 1), lambda n: np.stack([-n[:, 2], n[:, 1], n[:, 0]], 1))
    else:
        m = C.shift(src, dx)
    ms.append(m)
m = C.concat([dict(x, pos=np.asarray(x['pos'], np.float64), subs=[np.asarray(s) for s in x['subs']]) for x in ms])
name2i = {mm['name']: i for i, mm in enumerate(mats)}
T = []; TM = []
for s, mn in zip(m['subs'], m['mats']):
    if mn not in name2i:
        continue
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
