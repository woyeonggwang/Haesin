"""간의대 기구·정자·취로정 연못용 그림을 코드로 만든다(외부 그림 없음, 석재 바탕만 원본 에셋 텍스처 캐시가 있으면 씀).

실행: python3 maketex.py <출력 폴더> [석재 캐시 폴더]
만드는 것(모두 PNG):
  PAV_RoofTile_BC / _N  기와 — 1.08 m × 1.08 m 한 장(가로 = 처마 방향 4줄 × 0.27 m, 세로 = 물매 방향 3장 × 0.36 m), 이어 붙임
  PAV_Rail_BC           정자 난간(알파) — 가로 1.2 m(한 칸) × 세로 0.65 m
  PAV_Palette_BC / _Mask 단색 팔레트 4×4 칸(16 px) — 마스크: R 금속, G AO(1), B 0, A 매끄러움
  GYD_Honsang_BC        혼상 별그림(위경도 펼침, 2:1) — 적도(빨강)·황도(노랑)·28수 경계(금색)·은하수(흐리게)·별(무작위)
  GYD_Jeongbangan_BC    정방안 윗면(돌 + 동심원·십자)
  GYD_Gyu_BC            규 윗면(푸른 돌 + 1척 눈금·양옆 물홈) — 가로 0.9 m × 세로 2.06 m(10척), 세로로 이어 붙임
  CRJ_Lotus_BC          연잎 무리(알파) — 한 장 = 한 무리
별 위치·28수 경계는 장식(실제 성도 아님). 28수 너비는 흔히 쓰는 옛 도수(각 12 … 진 17, 합 365)로 나눔.
"""
import os, sys, math
import numpy as np
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else 'tex'
CACHE = sys.argv[2] if len(sys.argv) > 2 else '/mnt/user-data/uploads/Haesin/Temp/PalaceSimplify/tex'
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)


def srgb(c):
    return np.array(c, dtype=np.float32)


def save_rgb(a, name):
    Image.fromarray(np.clip(a * 255 + 0.5, 0, 255).astype(np.uint8)).save(os.path.join(OUT, name))


def save_rgba(rgb, alpha, name):
    a = np.dstack([rgb, alpha[..., None]])
    Image.fromarray(np.clip(a * 255 + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, name))


def value_noise(h, w, cells, seed, tile=True):
    """이어 붙는 값 노이즈(0..1)"""
    r = np.random.default_rng(seed)
    g = r.random((cells, cells)).astype(np.float32)
    y = np.linspace(0, cells, h, endpoint=False); x = np.linspace(0, cells, w, endpoint=False)
    y0 = np.floor(y).astype(int); x0 = np.floor(x).astype(int)
    fy = (y - y0)[:, None]; fx = (x - x0)[None, :]
    fy = fy * fy * (3 - 2 * fy); fx = fx * fx * (3 - 2 * fx)
    y1 = (y0 + 1) % cells; x1 = (x0 + 1) % cells
    a = g[y0][:, x0]; b = g[y0][:, x1]; c = g[y1][:, x0]; d = g[y1][:, x1]
    return a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c * (1 - fx) * fy + d * fx * fy


def fbm(h, w, seed, octs=((4, .5), (8, .25), (16, .15), (32, .1))):
    s = np.zeros((h, w), np.float32)
    for i, (c, a) in enumerate(octs):
        s += a * value_noise(h, w, c, seed + i)
    return s / sum(a for _, a in octs)


def dilate_rgb(rgb, alpha, iters=24):
    """투명한 곳 색을 가까운 불투명 색으로 채움(밉맵에서 가장자리가 검게 번지지 않게)"""
    rgb = rgb.copy(); m = alpha > 0.5
    for _ in range(iters):
        if m.all():
            break
        acc = np.zeros_like(rgb); cnt = np.zeros(m.shape, np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            mm = np.roll(np.roll(m, dy, 0), dx, 1); cc = np.roll(np.roll(rgb, dy, 0), dx, 1)
            acc += cc * mm[..., None]; cnt += mm
        new = (~m) & (cnt > 0)
        rgb[new] = acc[new] / cnt[new][:, None]
        m = m | new
    return rgb


def stone_base(size, seed, tint=(1, 1, 1)):
    """석재 바탕: 캐시(T_GCM_Stone03_BC, 0.94 m 한 장)가 있으면 그것, 없으면 노이즈"""
    p = os.path.join(CACHE, 'T_GCM_Stone03_BC.png.npy')
    if os.path.exists(p):
        a = np.load(p).astype(np.float32) / 255.0
        im = Image.fromarray((a * 255).astype(np.uint8)).resize((size, size), Image.BILINEAR)
        a = np.asarray(im).astype(np.float32) / 255.0
    else:
        n = fbm(size, size, seed)
        a = np.dstack([0.62 + 0.12 * n, 0.60 + 0.12 * n, 0.57 + 0.12 * n])
    return np.clip(a * np.array(tint, np.float32), 0, 1)


# ---------------- 기와 ----------------
def roof_tile():
    N = 512; M = 1.08; px = M / N
    p = 0.27; a = 0.074; course = 0.36
    u = (np.arange(N) + 0.5) * px            # 가로(처마 방향) m
    v = (np.arange(N) + 0.5) * px            # 세로(물매 방향, 위로) m — 그림 줄 0 이 맨 위라 뒤에서 뒤집음
    U, V = np.meshgrid(u, v)
    x = (U % p) - p / 2                      # 수키와 가운데 = 0
    row = np.floor(U / p).astype(int)
    cpos = V / course; ci = np.floor(cpos).astype(int); cf = cpos - ci
    # 암키와(오목): 수키와 밑이 높고 가운데 골이 낮음 + 장마다 위로 갈수록 조금 올라감
    ha = 0.035 * (np.cos(2 * np.pi * x / p) + 1) / 2 + 0.010 * cf
    # 수키와(볼록): 둥근 단면 + 장 이음 턱
    inside = np.abs(x) < a
    hs = np.where(inside, np.sqrt(np.clip(a * a - x * x, 0, None)) * 0.95 + 0.030 + 0.012 * cf, -1)
    h = np.maximum(ha, hs)
    su = hs >= ha
    # 장마다 색 조금씩 다르게
    rv = np.random.default_rng(11).random((N // 8 + 8, 64))
    var = rv[(ci * 3 + (row % 4) * 7 + su * 13) % rv.shape[0], (row * 5 + su) % 64]
    base = srgb((0.285, 0.295, 0.315)) * (0.90 + 0.16 * var)[..., None]
    n = fbm(N, N, 21)
    base = base * (0.9 + 0.2 * n)[..., None]
    # 골·장 이음 어둡게(가림)
    occl = np.clip((h - 0.0) / 0.08, 0, 1) * 0.35 + 0.65
    step = np.where(cf < 0.07, 0.72 + 4 * cf, 1.0)
    col = base * (occl * step)[..., None]
    col = col[::-1]; h = h[::-1]                # 줄 0 = 맨 위(v = 1)
    save_rgb(col, 'PAV_RoofTile_BC.png')
    # 노멀(탄젠트 공간, OpenGL: 초록 = +v)
    dhdu = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) / (2 * px)
    dhdv = -(np.roll(h, -1, 0) - np.roll(h, 1, 0)) / (2 * px)
    nx, ny, nz = -dhdu, -dhdv, np.ones_like(h)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    nm = np.dstack([nx / ln, ny / ln, nz / ln]) * 0.5 + 0.5
    save_rgb(nm, 'PAV_RoofTile_N.png')


# ---------------- 정자 난간 ----------------
def rail():
    W, H = 512, 256; LW, LH = 1.2, 0.65
    x = (np.arange(W) + 0.5) / W * LW; y = (np.arange(H) + 0.5) / H * LH
    X, Y = np.meshgrid(x, y[::-1])            # Y: 아래 0 → 위 LH (그림 줄 0 이 위)
    A = np.zeros((H, W), bool); shade = np.ones((H, W), np.float32)
    def band(y0, y1, round_=False):
        m = (Y >= y0) & (Y <= y1); A[m] = True
        if round_:
            t = (Y[m] - y0) / (y1 - y0); shade[m] = 0.75 + 0.35 * np.sin(np.pi * t)
    band(0.0, 0.07); band(0.30, 0.355); band(0.565, 0.635, True)
    xp = np.minimum(X, LW - X)                  # 양 끝 기둥(이어 붙이면 한 기둥)
    A[(xp < 0.035)] = True
    for cx in (0.3, 0.6, 0.9):                  # 난간동자
        A[(np.abs(X - cx) < 0.022) & (Y > 0.355) & (Y < 0.565)] = True
    # 아래 칸: 마름모 살(투각 느낌)
    m = (Y > 0.07) & (Y < 0.30)
    s = 0.115
    d1 = np.abs(((X + Y) % (2 * s)) - s); d2 = np.abs(((X - Y) % (2 * s)) - s)
    A[m & ((d1 < 0.014) | (d2 < 0.014))] = True
    n = fbm(H, W, 31, ((8, .6), (32, .4)))
    col = srgb((0.50, 0.17, 0.11))[None, None, :] * (0.88 + 0.2 * n)[..., None] * shade[..., None]
    alpha = A.astype(np.float32)
    col = dilate_rgb(col, alpha)
    save_rgba(col, alpha, 'PAV_Rail_BC.png')


# ---------------- 팔레트 ----------------
PALETTE = [  # (이름, sRGB, 금속, 매끄러움) — 칸 번호 = 목록 순서(왼쪽 아래부터 가로로)
    ('post_red', (0.50, 0.17, 0.11), 0.0, 0.25),
    ('beam_green', (0.28, 0.46, 0.38), 0.0, 0.20),
    ('ridge_white', (0.80, 0.80, 0.77), 0.0, 0.15),
    ('dark', (0.20, 0.21, 0.22), 0.0, 0.20),
    ('soffit', (0.26, 0.34, 0.30), 0.0, 0.15),
    ('wood', (0.45, 0.33, 0.21), 0.0, 0.30),
    ('bronze', (0.58, 0.45, 0.27), 0.55, 0.50),
    ('bronze_dark', (0.42, 0.33, 0.21), 0.45, 0.40),
    ('lacquer', (0.06, 0.06, 0.07), 0.0, 0.60),
    ('stone', (0.60, 0.58, 0.55), 0.0, 0.10),
    ('gold', (0.80, 0.64, 0.30), 0.70, 0.55),
    ('gable', (0.46, 0.20, 0.13), 0.0, 0.20),
]


def palette():
    S = 64; B = 16
    col = np.zeros((S, S, 3), np.float32); mask = np.zeros((S, S, 4), np.float32)
    for i, (_, c, met, sm) in enumerate(PALETTE):
        bx, by = i % 4, i // 4
        r0 = S - (by + 1) * B; c0 = bx * B          # 그림 줄 0 = 위 → v 아래부터 칸 by
        col[r0:r0 + B, c0:c0 + B] = c
        mask[r0:r0 + B, c0:c0 + B] = (met, 1.0, 0.0, sm)
    save_rgb(col, 'PAV_Palette_BC.png')
    Image.fromarray(np.clip(mask * 255 + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'PAV_Palette_Mask.png'))


# ---------------- 혼상 ----------------
SU28 = [12, 9, 15, 5, 5, 18, 11, 26, 8, 12, 10, 17, 16, 9, 16, 12, 14, 11, 16, 2, 9, 33, 4, 15, 7, 18, 18, 17]


def honsang():
    W, H = 1024, 512
    lon = (np.arange(W) + 0.5) / W * 2 * np.pi
    lat = (0.5 - (np.arange(H) + 0.5) / H) * np.pi       # 줄 0 = 북극
    LON, LAT = np.meshgrid(lon, lat)
    n = fbm(H, W, 41, ((6, .6), (24, .4)))
    col = np.dstack([0.07 + 0.025 * n, 0.09 + 0.025 * n, 0.17 + 0.035 * n])
    # 은하수(흐린 띠): 적도에 약 62° 기운 큰 원
    inc = math.radians(62); pole = np.array([math.sin(inc) * math.cos(4.4), math.sin(inc) * math.sin(4.4), math.cos(inc)])
    P = np.dstack([np.cos(LAT) * np.cos(LON), np.cos(LAT) * np.sin(LON), np.sin(LAT)])
    gb = np.abs(P @ pole)
    mw = np.exp(-(gb / 0.16) ** 2) * (0.5 + 0.8 * fbm(H, W, 43, ((8, .5), (32, .5))))
    col += mw[..., None] * np.array([0.10, 0.10, 0.11])
    # 28수 경계선(적도 기준, 금색 가는 선)
    edges = np.cumsum([0] + SU28[:-1]) / 365.25 * 2 * np.pi
    for e in edges:
        dx = np.abs(((LON - e + np.pi) % (2 * np.pi)) - np.pi) * np.cos(LAT) * W / (2 * np.pi)
        m = np.clip(1.2 - dx, 0, 1) * (np.abs(LAT) < math.radians(80))
        col = col * (1 - 0.45 * m[..., None]) + 0.45 * m[..., None] * np.array([0.62, 0.50, 0.24])
    # 적도(빨강)·황도(노랑)
    def line(latf, c, w):
        d = np.abs(LAT - latf) * H / np.pi
        m = np.clip(w - d, 0, 1)
        return col * (1 - m[..., None]) + m[..., None] * np.array(c)
    col = line(0.0, (0.72, 0.12, 0.08), 2.2)
    ecl = np.arctan(np.tan(math.radians(23.44)) * np.sin(LON))
    col = line(ecl, (0.85, 0.72, 0.20), 2.0)
    # 별(무작위, 극 근처 밀도 보정)
    r = np.random.default_rng(5)
    k = 1600
    zz = r.uniform(-1, 1, k); th = r.uniform(0, 2 * np.pi, k)
    la = np.arcsin(zz)
    mag = r.power(3.0, k)                       # 밝은 별 적게
    for i in range(k):
        cy = (0.5 - la[i] / np.pi) * H; cx = th[i] / (2 * np.pi) * W
        rad = 0.7 + 1.9 * mag[i]
        sx = rad / max(0.25, math.cos(la[i]))    # 펼침에서 가로로 늘림
        y0, y1 = int(cy - rad - 1), int(cy + rad + 2); x0, x1 = int(cx - sx - 1), int(cx + sx + 2)
        yy, xx = np.mgrid[y0:y1, x0:x1]
        dd = np.sqrt(((yy + 0.5 - cy) / rad) ** 2 + ((xx + 0.5 - cx) / sx) ** 2)
        a = np.clip(1.3 - dd * 1.3, 0, 1) * (0.55 + 0.45 * mag[i])
        yy = np.clip(yy, 0, H - 1); xx = xx % W
        c = np.array([0.95, 0.88, 0.62]) if r.random() < 0.6 else np.array([0.92, 0.92, 0.88])
        col[yy, xx] = col[yy, xx] * (1 - a[..., None]) + a[..., None] * c
    save_rgb(np.clip(col, 0, 1), 'GYD_Honsang_BC.png')


# ---------------- 정방안 ----------------
def jeongbangan():
    S = 512
    col = stone_base(S, 51, (0.95, 0.97, 1.0))
    y, x = np.mgrid[0:S, 0:S]
    cx = cy = (S - 1) / 2
    r = np.sqrt((x - cx) ** 2 + (y - cy) ** 2) / (S / 2)     # 0..1(가장자리 가운데)
    groove = np.zeros((S, S), np.float32)
    for k in range(1, 13):                                     # 동심원 12개
        rr = k / 13.0 * 0.92
        groove = np.maximum(groove, np.clip(1.4 - np.abs(r - rr) * S / 2, 0, 1))
    groove = np.maximum(groove, np.clip(1.6 - np.abs(x - cx), 0, 1) * (r < 0.93))
    groove = np.maximum(groove, np.clip(1.6 - np.abs(y - cy), 0, 1) * (r < 0.93))
    hole = np.clip((0.035 - r) * S / 2, 0, 1)
    edge = np.clip(1.5 - np.minimum.reduce([x, y, S - 1 - x, S - 1 - y]) / 3.0, 0, 1) * 0.3   # 모서리 살짝 어둡게
    dark = np.clip(groove * 0.45 + hole * 0.7 + edge, 0, 0.8)
    col = col * (1 - dark[..., None])
    save_rgb(col, 'GYD_Jeongbangan_BC.png')


# ---------------- 규 ----------------
def gyu():
    W, H = 256, 512; LW, LH = 0.9, 2.06
    base = stone_base(512, 61, (0.74, 0.82, 0.90))
    im = Image.fromarray((base * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR)
    col = np.asarray(im).astype(np.float32) / 255.0
    X = (np.arange(W) + 0.5) / W * LW; Y = (np.arange(H) + 0.5) / H * LH
    X, Y = np.meshgrid(X, Y[::-1])
    dark = np.zeros((H, W), np.float32)
    for cxm in (0.075, LW - 0.075):                        # 양옆 물홈
        d = np.abs(X - cxm)
        dark = np.maximum(dark, np.clip((0.028 - d) / 0.006, 0, 1) * 0.55)
        dark = np.maximum(dark, np.clip((0.034 - d) / 0.004, 0, 1) * 0.2)
    chi = 0.206
    t = Y / chi; ti = np.round(t); dd = np.abs(t - ti) * chi       # 가장 가까운 눈금까지 m
    k = ti.astype(int) % 10
    half = np.where(k == 0, 0.33, np.where(k == 5, 0.27, 0.18))
    wid = np.where(k == 0, 0.006, np.where(k == 5, 0.0045, 0.003))
    tick = (np.abs(X - LW / 2) < half) & (dd < wid)
    dark = np.maximum(dark, tick * 0.6)
    dark = np.maximum(dark, (np.abs(X - LW / 2) < 0.003) * 0.35)   # 가운데 세로선
    col = col * (1 - dark[..., None])
    save_rgb(col, 'GYD_Gyu_BC.png')


# ---------------- 연잎 ----------------
def lotus():
    S = 1024; L = 6.0                                     # 한 장 = 6 m 무리
    rgb = np.zeros((S, S, 3), np.float32); alpha = np.zeros((S, S), np.float32)
    r = np.random.default_rng(3)
    pads = []
    for i in range(400):
        # 무리 가운데 쪽이 빽빽하게
        rr = abs(r.normal(0, 0.26)); th = r.uniform(0, 2 * np.pi)
        cx, cy = 0.5 + rr * math.cos(th), 0.5 + rr * math.sin(th)
        rad = r.uniform(0.18, 0.40) / L
        if rr > 0.44:
            continue
        if any(math.hypot(cx - px, cy - py) < 0.72 * (rad + pr) for px, py, pr in pads):
            continue
        pads.append((cx, cy, rad))
        if len(pads) >= 70:
            break
    pads.sort(key=lambda p: r.random())
    for (cx, cy, rad) in pads:
        R = rad * S; x0, x1 = int(cx * S - R - 2), int(cx * S + R + 3); y0, y1 = int(cy * S - R - 2), int(cy * S + R + 3)
        yy, xx = np.mgrid[y0:y1, x0:x1]
        dx = (xx + 0.5 - cx * S) / R; dy = (yy + 0.5 - cy * S) / R
        d = np.sqrt(dx * dx + dy * dy); ang = np.arctan2(dy, dx)
        notch_a = r.uniform(-np.pi, np.pi)
        da = np.abs(((ang - notch_a + np.pi) % (2 * np.pi)) - np.pi)
        m = (d < 1.0) & ~((da < 0.10 + 0.25 * d * 0) & (d > 0.15))
        g = r.uniform(0, 1)
        base = np.array([0.20 + 0.10 * g, 0.40 + 0.12 * g, 0.12 + 0.05 * g])
        vein = (np.abs(((ang * 11 / np.pi) % 2) - 1) < 0.08) & (d > 0.12)
        shade = (0.78 + 0.28 * d)[..., None] * np.where(vein, 1.12, 1.0)[..., None]
        c = base * shade
        c = np.where((d < 0.10)[..., None], base * 1.25 + 0.05, c)
        rim = (d > 0.9)[..., None]
        c = np.where(rim, c * 0.8, c)
        yy = np.clip(yy, 0, S - 1); xx = np.clip(xx, 0, S - 1)
        rgb[yy[m], xx[m]] = c[m]; alpha[yy[m], xx[m]] = 1.0
    # 꽃 몇 송이(분홍)
    for i in range(7):
        rr = abs(r.normal(0, 0.18)); th = r.uniform(0, 2 * np.pi)
        cx, cy = 0.5 + rr * math.cos(th), 0.5 + rr * math.sin(th)
        R = r.uniform(0.10, 0.14) / L * S
        yy, xx = np.mgrid[int(cy * S - R - 2):int(cy * S + R + 3), int(cx * S - R - 2):int(cx * S + R + 3)]
        dx = (xx + 0.5 - cx * S) / R; dy = (yy + 0.5 - cy * S) / R
        d = np.sqrt(dx * dx + dy * dy); ang = np.arctan2(dy, dx)
        petal = d < (0.65 + 0.35 * np.abs(np.cos(ang * 4)))
        c = np.where((d < 0.25)[..., None], np.array([0.95, 0.80, 0.35]), np.array([0.93, 0.55, 0.66]) * (0.85 + 0.25 * (1 - d))[..., None])
        yy = np.clip(yy, 0, S - 1); xx = np.clip(xx, 0, S - 1)
        rgb[yy[petal], xx[petal]] = c[petal]; alpha[yy[petal], xx[petal]] = 1.0
    rgb = dilate_rgb(rgb, alpha, 40)
    save_rgba(rgb, alpha, 'CRJ_Lotus_BC.png')


if __name__ == '__main__':
    roof_tile(); rail(); palette(); honsang(); jeongbangan(); gyu(); lotus()
    print('textures ->', OUT, sorted(os.listdir(OUT)))
