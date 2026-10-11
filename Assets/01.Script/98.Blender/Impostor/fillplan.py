"""빈터(궁 안 서쪽 A·동남 B·동궁 옆 B′)에 임포스터 묶음(단지 일부를 그대로 쓴 것)을 줄지어 놓는 배치 계획.

묶음 = 동궁 임포스터 오브젝트 몇 개(그림 페이지를 같이 써서 그림 메모리가 안 늘어남) + 영추문 남쪽 건물 3채(2048 한 장).
줄(동서 방향)마다 북쪽부터 채우고, 묶음 사이 골목 7 m·줄 사이 8 m, 건물 정면(동궁 본채는 데모 +x)이 남쪽을 보게 90° 돌림
(일부는 270° — 북향). 결과 fillplan.json: [{kit, x, z, rot}] (x, z = 묶음 기준점의 월드 좌표, rot = y 회전)
"""
import json, random
import numpy as np
from PIL import Image, ImageDraw

# 묶음: 단지, 오브젝트, 데모 좌표 경계(x0, x1, z0, z1), 기준 땅 높이(데모 y)
import os
from impcx import OUTROOT
DG = json.load(open(os.path.join(OUTROOT, 'Donggung', 'bbox.json')))
YCM = {}
_z = np.load(os.path.join(OUTROOT, 'YCMFill', 'uvmesh.npz'))
for nm in ('YCM_BLD_T', 'YCM_BLD_L1', 'YCM_BLD_L2'):
    P = _z[nm + '|pos']; YCM[nm] = [float(P[:, 0].min()), float(P[:, 0].max()), float(P[:, 2].min()), float(P[:, 2].max())]
KIT_OBJS = {'DG_S': 'DG_Bld_06,DG_HG_03,DG_HG_09,DG_HG_18,DG_HG_08,DG_HG_02',
            'DG_E': 'DG_Bld_04,DG_Bld_05,DG_Wall_01,DG_Wall_03,DG_Bld_02',
            'DG_NE': 'DG_Bld_08,DG_Bld_09,DG_Wall_08,DG_HG_20,DG_HG_22',
            'DG_MID': 'DG_HG_04,DG_HG_05,DG_Bld_03,DG_Wall_02,DG_HG_10,DG_HG_11,DG_HG_13,DG_Wall_05,DG_Wall_06',
            'DG_WM': 'DG_HG_14,DG_HG_15,DG_HG_12,DG_HG_06,DG_HG_07,DG_HG_21',
            'DG_N': 'DG_HG_24,DG_HG_19,DG_HG_25,DG_HG_23',
            'YCM_T': 'YCM_BLD_T', 'YCM_L1': 'YCM_BLD_L1', 'YCM_L2': 'YCM_BLD_L2'}
GROUND = {'DG': -0.9, 'YCM': 7.37}


def kit_info():
    out = {}
    for k, v in KIT_OBJS.items():
        ns = v.split(',')
        src = DG if k.startswith('DG') else YCM
        x0 = min(src[n][0] for n in ns); x1 = max(src[n][1] for n in ns); z0 = min(src[n][2] for n in ns); z1 = max(src[n][3] for n in ns)
        out[k] = dict(cx='Donggung' if k.startswith('DG') else 'YCMFill', objs=v, pivot=[(x0 + x1) / 2, GROUND['DG' if k.startswith('DG') else 'YCM'], (z0 + z1) / 2],
                      sx=x1 - x0, sz=z1 - z0)
    return out


def dims(kit, rot, sc=(1.0, 1.0, 1.0)):
    """월드 (폭 x, 깊이 z). sc = 묶음 로컬 배율(x, y, z) — x 부호(거울)는 크기에 안 씀"""
    sx = kit['sx'] * abs(sc[0]); sz = kit['sz'] * abs(sc[2])
    return (sz, sx) if rot in (90, 270) else (sx, sz)


def pick_scale(rng, vary):
    """크기 다양화(2026-09-30, 사용자 지시 '반복을 줄이기 위해 몇 개의 크기를 각각 다르게'):
    약 3/4 을 로컬 x(깊이) 0.85~1.2 · z(길이) 0.8~1.3 · y(높이) 0.9~1.12 로 늘이고 줄임(높이는 넓이를 조금 따라감),
    그중 약 40 % 는 x 를 뒤집어(거울) 배치가 같아 보이지 않게. 나머지 1/4 은 원래 크기."""
    if not vary or rng.random() < 0.25:
        return (1.0, 1.0, 1.0)
    sx = rng.uniform(0.85, 1.2); sz = rng.uniform(0.8, 1.3)
    sy = min(1.12, max(0.9, 1 + 0.45 * ((sx * sz) ** 0.5 - 1) + rng.uniform(-0.04, 0.04)))
    if rng.random() < 0.4:
        sx = -sx
    return (round(sx, 3), round(sy, 3), round(sz, 3))


# 구역(월드 x0, x1, z0, z1) — 궁장·이웃 단지에서 6~8 m 띄움(2026-09-30 Unity 렌더러 경계로 확인)
ZONES = {'A': [(-3000.0, -2819.0, 3898.0, 4189.0)],
         'B': [(-2670.0, -2451.0, 3981.0, 4137.0), (-2640.0, -2451.0, 3896.0, 3975.0)],
         'Bp': [(-2592.0, -2485.0, 4149.0, 4304.0)]}
LANE_X = 7.0
LANE_Z = 8.0


def plan(seed=3, vary=False):
    rng = random.Random(seed)
    K = kit_info()
    big = ['DG_S', 'DG_E', 'DG_NE', 'DG_MID', 'DG_WM', 'DG_N', 'YCM_T']
    small = ['YCM_L1', 'YCM_L2']
    placed = []
    queue = []
    for zname, rects in ZONES.items():
        for (x0, x1, z0, z1) in rects:
            zc = z1
            while zc - z0 >= 28:
                row = []; w_used = 0.0; depth = 0.0; used_small = False; skipped = []
                for _ in range(20):
                    if not queue:
                        queue = big[:]; rng.shuffle(queue)
                    k = queue.pop(0)
                    rot = 90 if rng.random() < 0.75 else 270
                    sc = pick_scale(rng, vary)
                    w, d = dims(K[k], rot, sc)
                    lane = rng.uniform(6.0, 11.0) if row else 0.0
                    if d <= zc - z0 and w_used + lane + w <= x1 - x0:
                        row.append((k, rot, w, d, lane, sc)); w_used += lane + w; depth = max(depth, d)
                    else:
                        skipped.append(k)
                        if len(skipped) >= 4:
                            break
                queue = skipped + queue
                # 남는 폭에 작은 채 하나
                for k in rng.sample(small, len(small)):
                    sc = pick_scale(rng, vary)
                    w, d = dims(K[k], 90, sc); lane = rng.uniform(6.0, 11.0) if row else 0.0
                    if not used_small and d <= zc - z0 and w_used + lane + w <= x1 - x0:
                        row.append((k, 90, w, d, lane, sc)); w_used += lane + w; depth = max(depth, d); used_small = True
                if not row:
                    break
                slack = (x1 - x0) - w_used
                rng.shuffle(row)
                extra = slack / (len(row) + 1)
                xc = x0 + extra * rng.uniform(0.3, 1.0)
                for i, (k, rot, w, d, lane, sc) in enumerate(row):
                    if i:
                        xc += (lane if lane else 7.0) + extra
                    jz = rng.uniform(0, max(0.0, depth - d)) if depth > d else 0.0
                    placed.append(dict(kit=k, x=round(xc + w / 2, 2), z=round(zc - jz - d / 2, 2), rot=rot, w=w, d=d, zone=zname, sc=list(sc)))
                    xc += w
                zc -= depth + rng.uniform(7.0, 12.0)
    return K, placed


def draw(K, placed, path):
    X0, X1, Z0, Z1 = -3050, -2430, 3870, 4340
    s = 2.0
    im = Image.new('RGB', (int((X1 - X0) * s), int((Z1 - Z0) * s)), (40, 40, 40)); d = ImageDraw.Draw(im)
    def P(x, z):
        return ((x - X0) * s, (Z1 - z) * s)
    for zn, rects in ZONES.items():
        for (x0, x1, z0, z1) in rects:
            d.rectangle([P(x0, z1), P(x1, z0)], outline=(200, 160, 0))
    for p in placed:
        col = (90, 140, 220) if p['kit'].startswith('DG') else (220, 120, 90)
        d.rectangle([P(p['x'] - p['w'] / 2, p['z'] + p['d'] / 2), P(p['x'] + p['w'] / 2, p['z'] - p['d'] / 2)], outline=col, fill=tuple(c // 3 for c in col))
        d.text(P(p['x'] - p['w'] / 2 + 2, p['z'] + p['d'] / 2 - 2), '%s %d' % (p['kit'], p['rot']), fill=(255, 255, 255))
        # 정면(남쪽 90 / 북쪽 270) 표시
        fz = -1 if p['rot'] == 90 else (1 if p['rot'] == 270 else 0)
        d.line([P(p['x'], p['z']), P(p['x'], p['z'] + fz * p['d'] / 2)], fill=(255, 255, 0))
    # 길(x −2745)
    d.line([P(-2745, Z0), P(-2745, Z1)], fill=(255, 0, 0))
    im.save(path)


if __name__ == '__main__':
    import sys
    seed = int(sys.argv[1]) if len(sys.argv) > 1 else 3
    K, placed = plan(seed, vary=True)
    tris = {'DG_S': 9454, 'DG_E': 4814, 'DG_NE': 4585, 'DG_MID': 9156, 'DG_WM': 15346, 'DG_N': 5849}
    from collections import Counter
    print(len(placed), 'placements', Counter(p['kit'] for p in placed))
    json.dump(dict(kits=K, placed=placed), open(os.path.join(OUTROOT, 'fillplan.json'), 'w'), indent=1)
    with open(os.path.join(OUTROOT, 'fillplan.tsv'), 'w', encoding='utf-8') as f:
        f.write('# kit\tx\tz\trotY\tscaleX\tscaleY\tscaleZ (월드 x·z = 묶음 기준점, 배율 = 묶음 로컬, scaleX 음수 = 거울) — fillplan.py 결과\n')
        for p in placed:
            f.write('%s\t%.2f\t%.2f\t%d\t%.3f\t%.3f\t%.3f\n' % (p['kit'], p['x'], p['z'], p['rot'], p['sc'][0], p['sc'][1], p['sc'][2]))
    draw(K, placed, os.path.join(OUTROOT, 'fillplan.png'))
