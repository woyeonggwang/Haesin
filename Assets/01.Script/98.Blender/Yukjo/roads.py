"""육조거리 길 곧게 정리 — 사용자가 손으로 올린 길(13.52)·띠(10.75)·얼룩(10.4)을 곧은 모양으로 다시 만든다.

입력: Unity 에서 내보낸 높이 창(float32, 행 = j(z), 열 = i(x), 월드 높이 m).
출력: 새 높이 창(같은 형식) + 확인 그림.
"""
import numpy as np
from scipy import ndimage
from scipy.sparse import lil_matrix
from scipy.sparse.linalg import spsolve
import sys

SRC = '/mnt/user-data/uploads/Haesin/Captures/terrain_backup/Island_Joseon_Large_i444_j347_w280_h271_before_YJ.f32'
I0, J0, W, Hh = 444, 347, 280, 271
SY, PY = 300.0, -60.0   # 높이 = 정규값 * 300 - 60
CELL = 2500 / 1024
PX, PZ = -4166.0, 2412.0

# 새 길(월드 좌표)
NS = dict(x0=-2762.0, x1=-2728.0, z0=3296.0, z1=3872.0)     # 남북 길 — 광화문 축 x -2745, 폭 34, 남쪽 끝 = 그리신 끝
EW = dict(x0=-2920.0, x1=-2560.0, z0=3462.0, z1=3496.0)     # 동서 길 — 폭 34, 동쪽 끝은 언덕으로
BAND = dict(x0=-2826.0, x1=-2664.0, z0=3852.0, z1=3861.0, h=10.75)   # 광화문 앞 띠
ROAD_UP = 0.30          # 길 바닥 = 주변 땅 + 0.3
FEATHER = CELL          # 가장자리 한 칸에 걸쳐 섞음


def main(out_path):
    H = np.fromfile(SRC, dtype=np.float32).reshape(Hh, W).astype(np.float64) * SY + PY
    X = PX + (I0 + np.arange(W)) * CELL
    Z = PZ + (J0 + np.arange(Hh)) * CELL
    XX, ZZ = np.meshgrid(X, Z)

    # 1) 사용자가 손댄 칸 찾기
    plateau = (H > 13.4) & (H < 13.6) & (XX < -2535)
    lab, _ = ndimage.label(plateau)
    keep = set()
    for (sx, sz) in ((-2745, 3700), (-2860, 3470), (-2735, 3350)):
        i = int(round((sx - X[0]) / CELL)); j = int(round((sz - Z[0]) / CELL))
        if lab[j, i]:
            keep.add(lab[j, i])
    plateau = np.isin(lab, list(keep))
    road_old = ndimage.binary_dilation(plateau, iterations=2) & (XX < -2535)
    # 광화문 앞 얼룩·띠 (10.02~10.8, 궁장 남쪽, 길 둘레)
    patch = (H > 10.02) & (H < 10.8) & (ZZ > 3800) & (ZZ < 3867) & (XX > -2835) & (XX < -2655)
    M = road_old | patch
    # 동쪽 언덕 안쪽(원래 땅)은 건드리지 않음: 언덕 구간에서 13.5 등고선에 걸린 칸 제외
    hill = (XX > -2610) & (ZZ < 3600) & ~ndimage.binary_dilation(plateau & (XX < -2600), iterations=2)
    M &= ~(hill & ~plateau)
    # 동서 길 동쪽 끝(언덕에 붙은 곳): 13.3~13.7 칸과 둘레 2칸을 다시 보간 — 등고선 위 원래 칸도 둘레 값으로 거의 같게 채워짐
    box = (XX > -2625) & (XX < -2535) & (ZZ > 3440) & (ZZ < 3530)
    M |= ndimage.binary_dilation((H > 13.3) & (H < 13.7) & box, iterations=2) & box
    # 광화문 앞(띠·얼룩 자리)은 원래 10.0 평지 — 고정값으로 두고 나머지만 보간
    FIX = M & (ZZ > 3800) & (ZZ < 3866)

    # 2) 손대기 전 땅 추정 — 둘레 값으로 조화 보간(라플라스)
    idx = -np.ones(H.shape, int)
    jj, ii = np.where(M & ~FIX)
    idx[jj, ii] = np.arange(len(jj))
    n = len(jj)
    A = lil_matrix((n, n))
    b = np.zeros(n)
    for k in range(n):
        j, i = jj[k], ii[k]
        A[k, k] = 4.0
        for dj, di in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            j2, i2 = j + dj, i + di
            if j2 < 0 or j2 >= Hh or i2 < 0 or i2 >= W:
                A[k, k] -= 1.0
                continue
            if M[j2, i2] and not FIX[j2, i2]:
                A[k, idx[j2, i2]] = -1.0
            elif FIX[j2, i2]:
                b[k] += 10.0
            else:
                b[k] += H[j2, i2]
    sol = spsolve(A.tocsr(), b)
    B = H.copy()
    B[jj, ii] = sol
    B[FIX] = 10.0
    # 광화문 앞 돋운 땅(문루 기단 앞 경사): 길 자리 줄은 길 양옆(x -2770, -2721 — 예전에 돋운 그대로 남은 칸) 평균으로
    ia = int(round((-2770 - X[0]) / CELL)); ib = int(round((-2721 - X[0]) / CELL))
    for j in range(Hh):
        if 3864 <= Z[j] <= 3900:
            v = 0.5 * (H[j, ia] + H[j, ib])
            for i in range(ia + 1, ib):
                if M[j, i]:
                    B[j, i] = v

    # 3) 새 모양
    def rect_w(r):
        dx = np.maximum(np.maximum(r['x0'] - XX, XX - r['x1']), 0)
        dz = np.maximum(np.maximum(r['z0'] - ZZ, ZZ - r['z1']), 0)
        d = np.hypot(dx, dz)
        return np.clip(1.0 - d / FEATHER, 0, 1)

    N = B.copy()
    # 띠
    wb = rect_w(BAND)
    N = N + wb * (np.maximum(BAND['h'], B) - N)
    # 길: 주변 땅 + 0.3 (광화문 앞 돋운 땅(>= 10.8)에서는 0 으로 줄임)
    base = B   # 길은 땅을 따라감(남쪽 끝·동쪽 낮은 땅에서도 둑이 생기지 않게)
    up = ROAD_UP * np.clip((11.0 - B) / 0.7, 0, 1)
    R = base + up
    wr = np.maximum(rect_w(NS), rect_w(EW))
    N = N + wr * (R - N)
    return H, B, N, X, Z, M


if __name__ == '__main__':
    out = sys.argv[1]
    H, B, N, X, Z, M = main(out)
    ((N - PY) / SY).astype(np.float32).tofile(out)
    d = N - H
    print('changed cells', int((np.abs(d) > 1e-3).sum()), 'min', d.min(), 'max', d.max())
    np.save(out + '.B.npy', B)
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    ext = [X[0], X[-1], Z[0], Z[-1]]
    fig, ax = plt.subplots(1, 3, figsize=(27, 9))
    for a, arr, t in zip(ax, (H, B, N), ('before', 'base (fill)', 'new')):
        im = a.imshow(np.clip(arr, 9, 14), origin='lower', extent=ext, cmap='terrain', interpolation='nearest')
        a.set_title(t); a.grid(alpha=0.3)
    plt.tight_layout(); plt.savefig('/home/claude/yukjo/roads_cmp.png', dpi=60)
