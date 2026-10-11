"""육조거리 배치 계획 — 길 양옆 앞줄 = 관아(YJ_A 사정전형·YJ_B 강녕전형·YJ_C 교태전형), 그 뒤(길에서 약 110 m 까지) = 빈터 묶음(동궁·영추문 임포스터).

길(2026-09-30 곧게 정리): 남북 x -2762 ~ -2728(광화문 축 x -2745, 폭 34 m), 동서 z 3462 ~ 3496. 광화문 앞 띠(사용자, 10.75 m) z 3852 ~ 3861.
관아 정면(처마 끝) = 길 가에서 4 m: 서쪽 x -2766, 동쪽 x -2724. 서쪽 관아는 동쪽(길)을, 동쪽 관아는 서쪽을 봄.
결과: out/yj_front.tsv, out/yj_back.tsv (PalaceImpostorTool.PlaceFill 형식: 묶음, x, z, 회전, 배율 x, y, z), out/yj_plan.png
"""
import json, random, os
from PIL import Image, ImageDraw
import fillplan as FP

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')
ROAD_W, ROAD_E = -2762.0, -2728.0
Z_S, Z_N = 3502.0, 3846.0          # 관아 줄 남북 끝(동서 길 북쪽 가 + 6, 띠 남쪽 가 - 6)
FRONT_W, FRONT_E = ROAD_W - 4.0, ROAD_E + 4.0
DEPTH = 110.0                       # 길 가에서 이만큼까지 관아 구역
GAP = 18.0                          # 관아 사이 골목
N_MARGIN = 10.0


# 섞은 관아(사용자 지시 2026-09-30 "너무 획일적이라 섞어도 돼요"): 묶음 → (앞쪽 형식, 안쪽 명세 — PalaceYukjoTool.BuildGwanaMix)
KITS = {
    'YJ_A': ('A', ''), 'YJ_B': ('B', ''), 'YJ_C': ('C', ''),
    'YJ_AB': ('A', 'A:SJC_SJJ_HG:0:0;B:GNJ:0:-4.5;B:GNJC_GSJ:0:3.5;B:GNJC_YSJ:0:3.5'),
    'YJ_AB2': ('A', 'A:SJC_SJJ_HG:0:0;A:SJC_SJJ:0:0;B:GNJC_EJD:0:-8;B:GNJC_YGD:0:-8'),
    'YJ_BA': ('B', 'B:GNJC_GNJ_HG:0:0;A:SJC_SJJ:0:4;B:GNJC_EJD:0:0;B:GNJC_YGD:0:0;A:SJC_MCJ:0:-6;A:SJC_CCJ:0:-6;A:Keep/SM_MCJ_FoundationStone_Keep:0:-6;A:Keep/SJC_MCJ_Keep:0:-6;A:Keep/SJC_CCJ_Keep:0:-6'),
    'YJ_BA2': ('B', 'B:GNJC_GNJ_HG:0:0;B:GNJ:0:0;B:GNJC_EJD:0:0;B:GNJC_YGD:0:0;A:SJC_MCJ:0:-6;A:SJC_CCJ:0:-6;A:Keep/SM_MCJ_FoundationStone_Keep:0:-6;A:Keep/SJC_MCJ_Keep:0:-6;A:Keep/SJC_CCJ_Keep:0:-6'),
    'YJ_C2': ('C', 'C:GTJ_GTJ:0:0;C:GTJ_HHG:0:0;C:GTJ_WGH:0:0'),
}
# 앞줄(북→남): (묶음, 배율 x(음수 = 거울), y, z)
WEST = [('YJ_AB2', -1.00, 1.03, 0.95), ('YJ_C', 1.00, 1.00, 1.00), ('YJ_BA', 0.92, 0.97, 1.06)]
EAST = [('YJ_C2', -1.06, 1.05, 0.92), ('YJ_BA2', 1.00, 1.00, 1.00), ('YJ_AB', -0.94, 0.98, 1.05)]


def fronts():
    info = {t: json.load(open(os.path.join(OUT, 'YJ_' + t, 'front.json'), encoding='utf-8')) for t in 'ABC'}
    def wd(kit, sx, sz):
        t = KITS[kit][0]; b = info[t]['bounds']; piv = info[t]['pivot']
        a0, a1 = (b[0] - piv[0]) * sx, (b[1] - piv[0]) * sx
        return min(a0, a1), max(a0, a1), (b[3] - piv[2]) * sz      # 로컬 x 최소·최대(배율·거울 반영), 깊이
    rows = []
    # 서쪽 — rot 270: 로컬 +x → 월드 +z(북), 로컬 +z(깊이) → 월드 -x
    z = Z_N - N_MARGIN
    for kit, sx, sy, sz in WEST:
        lx0, lx1, dep = wd(kit, sx, sz)
        zc = z - lx1
        rows.append(dict(kit=kit, sc=(sx, sy, sz), x=FRONT_W, z=zc, rot=270, side='W', z0=zc + lx0, z1=zc + lx1, x0=FRONT_W - dep, x1=FRONT_W))
        z = zc + lx0 - GAP
    # 동쪽 — rot 90: 로컬 +x → 월드 -z(남), 로컬 +z → 월드 +x
    z = Z_N - N_MARGIN
    for kit, sx, sy, sz in EAST:
        lx0, lx1, dep = wd(kit, sx, sz)
        zc = z + lx0
        rows.append(dict(kit=kit, sc=(sx, sy, sz), x=FRONT_E, z=zc, rot=90, side='E', z0=zc - lx1, z1=zc - lx0, x0=FRONT_E, x1=FRONT_E + dep))
        z = zc - lx1 - GAP
    return rows


def backs(front_rows, seed=11):
    rng = random.Random(seed)
    K = FP.kit_info()
    pool = ['DG_E', 'DG_NE', 'DG_MID', 'DG_WM', 'DG_N', 'YCM_T', 'YCM_L2', 'DG_S']
    placed = []
    for side in ('W', 'E'):
        deep = max(r['x1'] - r['x0'] for r in front_rows if r['side'] == side)
        if side == 'W':
            x0, x1 = ROAD_W - DEPTH, FRONT_W - deep - 8.0
        else:
            x0, x1 = FRONT_E + deep + 8.0, ROAD_E + DEPTH
        zc = Z_N - 4.0; queue = []
        while zc - Z_S > 15:
            ok = False
            for _ in range(12):
                if not queue:
                    queue = pool[:]; rng.shuffle(queue)
                k = queue.pop(0)
                sc = FP.pick_scale(rng, True)
                rot = (0 if side == 'W' else 180) if rng.random() < 0.7 else (180 if side == 'W' else 0)
                w, d = FP.dims(K[k], rot, sc)            # w = 월드 x 폭, d = 월드 z 길이
                if w <= (x1 - x0) and d <= zc - Z_S:
                    ok = True; break
            if not ok:
                break
            xc = x0 + (x1 - x0 - w) * rng.uniform(0.2, 0.8) + w / 2
            placed.append(dict(kit=k, x=round(xc, 2), z=round(zc - d / 2, 2), rot=rot, w=w, d=d, sc=list(sc), side=side))
            zc -= d + rng.uniform(7.0, 11.0)
    return placed


def draw(front_rows, back_rows, path):
    X0, X1, Z0, Z1 = -2900, -2590, 3440, 3900
    s = 2.0
    im = Image.new('RGB', (int((X1 - X0) * s), int((Z1 - Z0) * s)), (50, 60, 40)); d = ImageDraw.Draw(im)
    P = lambda x, z: ((x - X0) * s, (Z1 - z) * s)
    d.rectangle([P(ROAD_W, 3872), P(ROAD_E, 3440)], fill=(140, 120, 90))
    d.rectangle([P(-2920, 3496), P(-2560, 3462)], fill=(140, 120, 90))
    d.rectangle([P(-2826, 3861), P(-2664, 3852)], fill=(90, 110, 70))
    for r in front_rows:
        d.rectangle([P(r['x0'], r['z1']), P(r['x1'], r['z0'])], outline=(255, 220, 0), fill=(120, 60, 50))
        d.text(P(r['x0'] + 3, r['z1'] - 3), r['kit'], fill=(255, 255, 255))
        fx = r['x'] ; d.line([P(fx, r['z0']), P(fx, r['z1'])], fill=(255, 255, 0), width=3)
    for p in back_rows:
        d.rectangle([P(p['x'] - p['w'] / 2, p['z'] + p['d'] / 2), P(p['x'] + p['w'] / 2, p['z'] - p['d'] / 2)], outline=(90, 140, 220), fill=(40, 60, 100))
        d.text(P(p['x'] - p['w'] / 2 + 2, p['z'] + p['d'] / 2 - 2), '%s %d' % (p['kit'], p['rot']), fill=(255, 255, 255))
    d.line([P(ROAD_W - DEPTH, Z0), P(ROAD_W - DEPTH, Z1)], fill=(255, 0, 0)); d.line([P(ROAD_E + DEPTH, Z0), P(ROAD_E + DEPTH, Z1)], fill=(255, 0, 0))
    im.save(path)


if __name__ == '__main__':
    import sys
    seed = int(sys.argv[1]) if len(sys.argv) > 1 else 11
    fr = fronts(); bk = backs(fr, seed)
    with open(os.path.join(OUT, 'yj_front.tsv'), 'w', encoding='utf-8') as f:
        f.write('# 관아\tx\tz\t회전\t배율 x\ty\tz — yj_plan.py\n')
        for r in fr:
            f.write('%s\t%.2f\t%.2f\t%d\t%.3f\t%.3f\t%.3f\n' % (r['kit'], r['x'], r['z'], r['rot'], r['sc'][0], r['sc'][1], r['sc'][2]))
    json.dump(KITS, open(os.path.join(OUT, 'yj_kits.json'), 'w', encoding='utf-8'), indent=1)
    with open(os.path.join(OUT, 'yj_back.tsv'), 'w', encoding='utf-8') as f:
        f.write('# 묶음\tx\tz\t회전\t배율 x\ty\tz — yj_plan.py\n')
        for p in bk:
            f.write('%s\t%.2f\t%.2f\t%d\t%.3f\t%.3f\t%.3f\n' % (p['kit'], p['x'], p['z'], p['rot'], p['sc'][0], p['sc'][1], p['sc'][2]))
    draw(fr, bk, os.path.join(OUT, 'yj_plan.png'))
    for r in fr:
        print(r['kit'], r['side'], 'x %.1f z %.1f  z %.1f..%.1f  x %.1f..%.1f' % (r['x'], r['z'], r['z0'], r['z1'], r['x0'], r['x1']))
    from collections import Counter
    print(len(bk), Counter(p['kit'] for p in bk))
