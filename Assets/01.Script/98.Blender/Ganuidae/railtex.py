"""간의대 돌난간 판 그림: 흥례문 데모의 영제교 서쪽 난간을 옆(서쪽)에서 평행 광선으로 찍어
[반 기둥 0.195 m][하엽동자 2개 칸 1.443 m][반 기둥 0.195 m] 한 마디(폭 1.833 m × 높이 1.0 m, 512×256)로 붙임.
결과 GYD_Rail_BC.png (RGBA, 알파 = 난간 있는 곳). 빛·그림자는 굽지 않음(색만)."""
import sys, os
for _d in (os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Impostor'), '/home/claude/impostor'):
    if os.path.isdir(_d): sys.path.insert(0, os.path.normpath(_d))
import numpy as np
from PIL import Image
import common
from railsrc import load_region
from railrender import ortho

PPM = 300
b = load_region('Heungnyemun', [(-12, -3, -50), (12, 4, -28)], names_ok=['Hayebdongja', 'BridgePiler', 'SM_HNM_Bridge'])
Bo = common.Bld(b)
Z0, Z1, Y0, Y1 = -44.4, -40.0, 0.29, 1.62
W, H, hit, c, n, t, tm = ortho(Bo, Z0, Z1, Y0, Y1, PPM, -9.0, 4.6)
print('render', W, H, hit.mean())
col = np.clip(c, 0, 1)
# 기둥(난간석주) 찾기: 위쪽(1.2~1.3 m)에서 맞는 열
yrow = lambda y: int(round((Y1 - y) * PPM))
colhit = hit[yrow(1.25)]
xs = np.where(colhit)[0]
print('post columns at y1.25:', xs.min(), xs.max(), 'z', Z0 + xs.min() / PPM, Z0 + (xs.max() + 1) / PPM)
np.save('rail_hit.npy', hit); np.save('rail_col.npy', col)
Image.fromarray(np.concatenate([(col * 255).astype(np.uint8), (hit * 255).astype(np.uint8)[..., None]], -1)).save('rail_hi.png')

# ---- 한 마디 붙이기 ----
M_POST = 0.39; HALF = M_POST / 2; SPAN = 2 * 0.7215; M = M_POST + SPAN   # 1.833 m
POST_Z0, POST_Z1 = -44.03, -43.64; PC = (POST_Z0 + POST_Z1) / 2
SPAN_Z0 = -42.28 - 0.7215 / 2
N = 512; NH = 256; TEX_H_M = 1.0
from scipy.ndimage import map_coordinates, distance_transform_edt
out = np.zeros((NH, N, 4), np.float32)
px = (np.arange(N) + 0.5) / N * M
zsrc = np.where(px < HALF, PC + px, np.where(px < HALF + SPAN, SPAN_Z0 + (px - HALF), POST_Z0 + (px - HALF - SPAN)))
hgt = (1 - (np.arange(NH) + 0.5) / NH) * TEX_H_M          # 행 0 = 위
ysrc = Y0 + hgt
cc = (zsrc - Z0) * PPM - 0.5; rr = (Y1 - ysrc) * PPM - 0.5
R, C = np.meshgrid(rr, cc, indexing='ij')
inside = (R >= 0) & (R <= H - 1)
alpha = map_coordinates(hit.astype(np.float32), [R, C], order=1, mode='constant', cval=0.0) * inside
rgb = np.stack([map_coordinates(col[..., k] * hit, [R, C], order=1, mode='nearest') for k in range(3)], -1)
wsum = np.maximum(alpha, 1e-4)[..., None]
rgb = np.where(alpha[..., None] > 1e-3, rgb / wsum, 0)
# 빈 곳 색을 가까운 난간 색으로 채움(밉맵 가장자리 검은 테 방지)
mask = alpha > 0.5
idx = distance_transform_edt(~mask, return_distances=False, return_indices=True)
rgb = rgb[idx[0], idx[1]]
out[..., :3] = rgb; out[..., 3] = np.clip(alpha, 0, 1)
img = (np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)
Image.fromarray(img).save('GYD_Rail_BC.png')
prev = img.astype(np.float32); bg = np.array([90, 130, 170], np.float32)
pv = prev[..., :3] * (prev[..., 3:] / 255) + bg * (1 - prev[..., 3:] / 255)
Image.fromarray(np.concatenate([pv, pv], 1).astype(np.uint8)).save('GYD_Rail_preview.png')
print('module M %.3f m, tex 512 = %.1f px/m, opaque %.3f' % (M, N / M, (img[..., 3] > 127).mean()))
