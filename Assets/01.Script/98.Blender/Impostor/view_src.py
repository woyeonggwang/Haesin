"""단지 단순화판 원본을 여러 시점에서 그려 보기(클라우드 확인용).
python3 view_src.py <단지> <출력.png> 'ex,ey,ez,tx,ty,tz[,fov];...' [가로] [오브젝트 쉼표 목록]"""
import sys, numpy as np
from PIL import Image
import common
from common import Bld
from load_cx import load_complex, merge
from render_imp import render_orig
from rt import cam_rays

cx = sys.argv[1]; out = sys.argv[2]
W = int(sys.argv[4]) if len(sys.argv) > 4 else 640; H = int(W * 9 / 16)
objs, mats = load_complex(cx)
names = sys.argv[5].split(',') if len(sys.argv) > 5 and sys.argv[5] else list(objs)
m = merge(objs, names); m['mats'] = mats
Bo = Bld(m)
rows = []
for vs in sys.argv[3].split(';'):
    a = [float(x) for x in vs.split(',')]
    O, D = cam_rays(np.array(a[:3]), np.array(a[3:6]), W, H, fov=a[6] if len(a) > 6 else 50.0)
    rows.append(render_orig(Bo, O, D).reshape(H, W, 3))
img = np.concatenate(rows, 0)
Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(out)
print(out, img.shape)
