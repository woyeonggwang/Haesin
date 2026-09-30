"""창·문·홍살 메시를 '판(카드)'으로 굽는다: 두께 방향 앞·뒤(여는 문짝은 옆면까지)에서 정사영 광선을 쏴
맞은 면의 텍스처 색(재질 색 곱)을 그대로 이미지로 만든다(빛·그림자 없음 = 이미지만).

- 좌표는 Unity 좌표(메시 로컬) 그대로. 결과 판 메시도 Unity 규칙(삼각형 앞면 = cross(v1-v0, v2-v0) 방향).
- 창틀 안에서 아무것도 안 맞은 곳: 'paper' = 흰 창호지 텍스처, 'solid' = 옆 색으로 메움(판문), 'clear' = 투명(홍살).
- 창틀 바깥(사각 판 모서리가 창 밖으로 나오는 곳)은 투명 → 그런 판만 알파 클립 재질.
"""
import os, math
import numpy as np
from PIL import Image
from scipy import ndimage
import sys
_HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(_HERE, '..')); sys.path.insert(0, os.path.join(_HERE, '..', 'Simplify')); sys.path.insert(0, _HERE)
from rayeval import RMesh

UPLOAD = '/mnt/user-data/uploads/Haesin/'
TEXFULL = UPLOAD + 'Temp/PalaceSimplify/texfull/'
PAPER_TEX = UPLOAD + 'Assets/02.Resources/01.Asset/Models/Gyeongbokgung/_Shared/Textures/T_GSJ_Koreanpaper_BC.png'
PAPER_PX_PER_M = 1240.0     # 창호지 텍스처 픽셀 밀도(무늬 크기 어림)
# T_GSJ_Koreanpaper_BC 는 창호지 + 창살 그림자 무늬가 섞인 아틀라스 → 무늬 없는 곳(위에서 392~888 행, 64~1520 열)만 거울 반복해 씀
PPM = 400.0                 # 1 m 당 픽셀
MAXPX = 2048
SS = 2                      # 픽셀당 2×2 광선
PAD = 4                     # 아틀라스 칸 사이 여백(px)
TRIM_FRAC = 0.08            # 가장자리 칸/줄의 불투명 비율이 이보다 작으면 잘라 냄
TRIM_MAX = 0.08             # 한 변에서 최대로 잘라 내는 길이(m)

_tex = {}


def local_path(p):
    if not p:
        return None
    q = p.replace('\\', '/')
    key = 'D:/Unity/Haesin/'
    if q.startswith(key):
        lp = UPLOAD + q[len(key):]
        if os.path.exists(lp):
            return lp
    lp = TEXFULL + os.path.basename(q)
    if os.path.exists(lp):
        return lp
    return None


def load_tex(path):
    """RGBA float32, 행 0 = 아래(v=0)."""
    if path in _tex:
        return _tex[path]
    Image.MAX_IMAGE_PIXELS = None
    im = Image.open(path).convert('RGBA')
    a = np.ascontiguousarray(np.asarray(im, dtype=np.uint8)[::-1])   # 메모리 때문에 uint8 로 둠
    _tex[path] = a
    return a


def sample(arr, uv):
    """반복(wrap) 이중선형 샘플. uv (N,2) → (N,4)"""
    H, W = arr.shape[:2]
    x = uv[:, 0] * W - 0.5; y = uv[:, 1] * H - 0.5
    x0 = np.floor(x); y0 = np.floor(y); fx = (x - x0)[:, None]; fy = (y - y0)[:, None]
    x0 = x0.astype(np.int64) % W; y0 = y0.astype(np.int64) % H; x1 = (x0 + 1) % W; y1 = (y0 + 1) % H
    sc = (1.0 / 255.0) if arr.dtype == np.uint8 else 1.0
    fx = fx.astype(np.float32); fy = fy.astype(np.float32)
    return (arr[y0, x0] * ((1 - fx) * (1 - fy) * sc) + arr[y0, x1] * (fx * (1 - fy) * sc) + arr[y1, x0] * ((1 - fx) * fy * sc) + arr[y1, x1] * (fx * fy * sc)).astype(np.float32)


def read_materials(path):
    mt = {}
    for line in open(path, encoding='utf-8'):
        p = line.rstrip('\n').split('\t')
        if len(p) < 7:
            continue
        col = [float(x) for x in p[4].split(',')] if p[4] else [1, 1, 1, 1]
        st = [float(x) for x in p[5].split(',')] if p[5] else [1, 1, 0, 0]
        clip = p[6] not in ('', '0')
        cut = float(p[7]) if len(p) > 7 and p[7] else 0.5
        mt[p[0]] = dict(name=p[0], shader=p[1], bc=local_path(p[2]), bc_src=p[2], color=np.array(col, np.float32), st=np.array(st, np.float32), clip=clip, cutoff=cut)
    return mt


def mat_sample(m, uv):
    """재질 m 의 색(RGBA). 텍스처가 없으면 재질 색."""
    if m is None:
        return np.tile(np.array([0.5, 0.5, 0.5, 1.0], np.float32), (len(uv), 1))
    uv2 = uv * m['st'][:2] + m['st'][2:]
    if m['bc']:
        c = sample(load_tex(m['bc']), uv2)
    else:
        c = np.ones((len(uv), 4), np.float32)
    return c * m['color']


class Scene:
    """광선 투사용 삼각형 묶음: pos (N,3), tris (M,3), uv (N,2), tmat (M,) → mats[idx]"""

    def __init__(self, pos, tris, uv, tmat, mats):
        self.rm = RMesh(pos, tris, uv, tmat)
        self.mats = mats
        self.tmat = np.asarray(tmat, np.int32)

    def free(self):
        self.rm.free()

    def shoot(self, O, D, tmax=100.0, max_layers=8):
        """가장 가까운 '불투명' 맞음의 RGBA(없으면 알파 0), 맞은 거리"""
        n = len(O)
        rgba = np.zeros((n, 4), np.float32); dist = np.full(n, np.inf, np.float32)
        todo = np.arange(n); Ocur = O.astype(np.float32).copy(); travelled = np.zeros(n, np.float32)
        for layer in range(max_layers):
            if len(todo) == 0:
                break
            t, tri, u, v = self.rm.cast(Ocur[todo], D[todo], tmax)
            hit = tri >= 0
            idx = todo[hit]
            if len(idx) == 0:
                break
            uvh = self.rm.hit_uv(tri[hit], u[hit], v[hit])
            mi = self.tmat[tri[hit]]
            col = np.zeros((len(idx), 4), np.float32)
            keep = np.ones(len(idx), bool)
            for m_id in np.unique(mi):
                sel = mi == m_id
                m = self.mats[m_id]
                c = mat_sample(m, uvh[sel])
                col[sel] = c
                if m is not None and m['clip']:
                    keep[np.where(sel)[0][c[:, 3] < m['cutoff']]] = False
            ok = idx[keep]
            rgba[ok, :3] = col[keep, :3]; rgba[ok, 3] = 1.0
            dist[ok] = travelled[ok] + t[hit][keep]
            # 알파로 잘린 곳은 그 뒤에서 다시 쏜다
            again = idx[~keep]
            if len(again) == 0:
                break
            tt = t[hit][~keep]
            Ocur[again] = Ocur[again] + D[again] * (tt + 1e-4)[:, None]
            travelled[again] += tt + 1e-4
            todo = again
        return rgba, dist


def axes_for(ext):
    """두께 축 t, 가로 축 a, 세로 축 b (세로는 가능하면 Unity Y)"""
    t = int(np.argmin(ext))
    rest = [i for i in range(3) if i != t]
    b = 1 if t != 1 else 2
    a = [i for i in rest if i != b][0]
    return t, a, b


def render_view(scene, bmin, bmax, t, a, b, sign, W, H, ss=SS, horiz=None, vert=None):
    """정사영: 축 t 의 sign 쪽 바깥에서 -sign 방향으로. 이미지 가로 = horiz 축(+방향 = 오른쪽), 세로 = vert 축(+방향 = 위).
    반환: 색 합(H,W,3), 맞은 수(H,W)"""
    horiz = a if horiz is None else horiz
    vert = b if vert is None else vert
    ha, hb = bmin[horiz], bmax[horiz]; va, vb = bmin[vert], bmax[vert]
    sx = ((np.arange(W * ss) + 0.5) / (W * ss)).astype(np.float32); sy = ((np.arange(H * ss) + 0.5) / (H * ss)).astype(np.float32)
    xs = (ha + sx * (hb - ha)).astype(np.float32); ys = (vb - sy * (vb - va)).astype(np.float32)   # 행 0 = 위
    o_t = (bmax[t] + 1.0) if sign > 0 else (bmin[t] - 1.0)
    col = np.zeros((H, W, 3), np.float32); cnt = np.zeros((H, W), np.float32)
    samp = []
    rows = max(1, (1 << 21) // max(1, W * ss * ss))   # 한 번에 약 200만 광선(메모리)
    rng = np.random.default_rng(0)
    for r0 in range(0, H, rows):
        r1 = min(H, r0 + rows)
        Xg, Yg = np.meshgrid(xs, ys[r0 * ss:r1 * ss])
        n = Xg.size
        O = np.zeros((n, 3), np.float32)
        O[:, horiz] = Xg.ravel(); O[:, vert] = Yg.ravel(); O[:, t] = o_t
        D = np.zeros((n, 3), np.float32); D[:, t] = -sign
        rgba, dist = scene.shoot(O, D, tmax=(bmax[t] - bmin[t]) + 2.5)
        hd = dist[rgba[:, 3] > 0]
        if len(hd) > 20000:
            hd = rng.choice(hd, 20000, replace=False)
        samp.append(hd)
        rb = rgba.reshape(r1 - r0, ss, W, ss, 4)
        col[r0:r1] = rb[..., :3].sum(axis=(1, 3)); cnt[r0:r1] = rb[..., 3].sum(axis=(1, 3))
        del O, D, rgba, dist, rb, Xg, Yg
    hd = np.concatenate(samp) if samp else np.zeros(0)
    # 판을 놓을 깊이: 맞은 면 깊이의 중앙값(튀어나온 문고리·돌쩌귀가 아니라 문짝 면에 판이 오게)
    plane = None
    if len(hd):
        plane = o_t - sign * float(np.median(hd))
    return col, cnt, xs, ys, plane


_paper = {}


def paper_tile():
    if 'a' not in _paper:
        im = Image.open(PAPER_TEX).convert('RGBA')
        a = np.asarray(im, dtype=np.float32)[392:888, 64:1520] / 255.0
        a = a[::-1]
        t = np.concatenate([a, a[:, ::-1]], axis=1)
        t = np.concatenate([t, t[::-1]], axis=0).copy()
        _paper['a'] = t
    return _paper['a']


def paper_sample(xs, ys, H, W, ss=SS):
    """창호지 색(xs, ys = 서브샘플 격자의 가로·세로 좌표) → 픽셀 평균. 줄 묶음으로 나눠 계산(메모리)"""
    pt = paper_tile()
    th, tw = pt.shape[0] / PAPER_PX_PER_M, pt.shape[1] / PAPER_PX_PER_M
    out = np.zeros((H, W, 3), np.float32)
    rows = max(1, (1 << 20) // max(1, W * ss * ss))
    for r0 in range(0, H, rows):
        r1 = min(H, r0 + rows)
        Xg, Yg = np.meshgrid(xs, ys[r0 * ss:r1 * ss])
        uv = np.stack([Xg.ravel() / tw, Yg.ravel() / th], 1).astype(np.float32)
        out[r0:r1] = sample(pt, uv)[:, :3].reshape(r1 - r0, ss, W, ss, 3).mean(axis=(1, 3))
    return out


def nearest_fill(col, have):
    """have 가 False 인 픽셀을 가장 가까운 have 픽셀 색으로"""
    if have.all() or not have.any():
        return col
    _, (iy, ix) = ndimage.distance_transform_edt(~have, return_indices=True)
    return col[iy, ix]


def compose(col, cnt, mode, X, Y, ppm, ss=SS):
    """한 면 그림: RGBA(0~1), 바깥(투명) 비율"""
    H, W = cnt.shape
    n = float(ss * ss)
    cov = cnt / n
    hit = cnt > 0
    avg = np.where(hit[..., None], col / np.maximum(cnt, 1)[..., None], 0)
    if mode == 'clear':
        alpha = cov
        rgb = nearest_fill(avg, hit)
        return np.concatenate([rgb, alpha[..., None]], -1), float((alpha < 0.5).mean()), 'clear'
    # 틀 안/밖: 맞은 곳을 넓혔다 줄여(가장자리는 채워진 것으로 봄) 작은 칸(창살 사이)은 닫고, 닫힌 영역의 구멍을 채운 것 = 틀 안
    st = ndimage.generate_binary_structure(2, 1)
    rc = max(1, int(round(0.015 * ppm)))
    dil = ndimage.binary_dilation(hit, structure=st, iterations=rc)
    closed = ndimage.binary_erosion(dil, structure=st, iterations=rc, border_value=0) | hit
    # 본체(가장 큰 덩어리)의 구멍만 창호지/메움 대상 — 튀어나온 문고리 고리 속 같은 것은 투명
    lab, nl = ndimage.label(closed, structure=np.ones((3, 3), bool))
    if nl > 1:
        sizes = ndimage.sum(np.ones_like(lab), lab, index=np.arange(1, nl + 1))
        main = lab == (int(np.argmax(sizes)) + 1)
    else:
        main = closed
    inside = ndimage.binary_fill_holes(main) | hit
    if mode == 'auto':
        gap = ((1 - cov) * inside).sum() / max(1, inside.sum())
        mode = 'solid' if gap < 0.03 else 'paper'
    if mode == 'paper':
        pc = paper_sample(X, Y, H, W, ss)
        rgb = (col + (n - cnt)[..., None] * pc) / n
        rgb = np.where(inside[..., None], rgb, avg)
    else:  # solid
        rgb = nearest_fill(avg, hit)
    alpha = np.where(inside, 1.0, cov)
    rgb = nearest_fill(rgb, alpha >= 0.5)
    outside_frac = float((alpha < 0.5).mean())
    return np.concatenate([rgb, alpha[..., None]], -1), outside_frac, mode


def close_border(alpha, ppm, width_m=0.025):
    """판 가장자리의 좁은 투명 띠를 막음: 쇠장식·고리가 판 밖으로 조금 튀어나와 경계 상자가 판보다 크면 가장자리에
    투명 띠가 생기고, 밉맵에서 알파가 번져 짝 문짝이 맞닿는 곳에 빈틈(흰 줄)이 보임.
    줄(가로)마다 판의 맨 왼쪽/오른쪽 불투명 픽셀과 경계 사이, 칸(세로)마다 맨 위/아래와 경계 사이가 width_m 이하이면 그 사이를 불투명으로
    (색은 이미 가장 가까운 판 색). 더 넓은 빈 곳(홍예 윗부분 등)은 그대로."""
    d = max(1, int(round(width_m * ppm)))
    ins = alpha >= 0.5
    if ins.all() or not ins.any():
        return alpha
    H, W = ins.shape
    out = alpha.copy()
    xx = np.arange(W)[None, :]; yy = np.arange(H)[:, None]
    rany = ins.any(1)
    first = np.argmax(ins, axis=1); last = W - 1 - np.argmax(ins[:, ::-1], axis=1)
    lf = rany & (first > 0) & (first <= d); rf = rany & (W - 1 - last > 0) & (W - 1 - last <= d)
    out[(xx < first[:, None]) & lf[:, None]] = 1.0
    out[(xx > last[:, None]) & rf[:, None]] = 1.0
    cany = ins.any(0)
    top = np.argmax(ins, axis=0); bot = H - 1 - np.argmax(ins[::-1, :], axis=0)
    tf = cany & (top > 0) & (top <= d); bf = cany & (H - 1 - bot > 0) & (H - 1 - bot <= d)
    out[(yy < top[None, :]) & tf[None, :]] = 1.0
    out[(yy > bot[None, :]) & bf[None, :]] = 1.0
    return out


def to_img(rgba):
    return (np.clip(rgba, 0, 1) * 255 + 0.5).astype(np.uint8)


def bake(scene, bmin, bmax, mode, style, ppm=PPM, maxpx=MAXPX):
    """한 종류의 판: 아틀라스 이미지(RGBA uint8, 행 0 = 위), 판 메시(pos, nrm, uv, idx), 알파 클립 여부, 정보"""
    bmin = np.array(bmin, np.float64); bmax = np.array(bmax, np.float64)
    ext = bmax - bmin
    t, a, b = axes_for(ext)
    eff = ppm
    down = style == 'down' and t == 1
    up = style == 'up' and t == 1
    if down or up:
        maxpx = max(maxpx, 8192)   # 천장은 한 장이라 크게(광화문 천장 판과 같은 8192)
    if max(ext[a], ext[b]) * eff > maxpx:
        eff = maxpx / max(ext[a], ext[b])
    W = max(4, int(math.ceil(ext[a] * eff))); H = max(4, int(math.ceil(ext[b] * eff)))
    views = []   # (이름, rgba, 가로축, 세로축, 법선축, 부호)
    if up:
        # 뒤집혀 놓인 천장: 메시의 +Y 쪽 그림이 아래를 봄
        col, cnt, X, Y, pf = render_view(scene, bmin, bmax, t, a, b, +1, W, H)
        f_img, f_out, f_mode = compose(col, cnt, mode, X, Y, eff)
        pf = bmax[t] if pf is None else pf
        b_out, b_mode = 0.0, f_mode
        views.append(('front', f_img, a, b, t, +1, pf))
    elif down:
        col, cnt, X, Y, pb = render_view(scene, bmin, bmax, t, a, b, -1, W, H)
        b_img, b_out, b_mode = compose(col, cnt, mode, X, Y, eff)
        pb = bmin[t] if pb is None else pb
        f_out, f_mode = 0.0, b_mode
        views.append(('back', b_img, a, b, t, -1, pb))
    else:
        col, cnt, X, Y, pf = render_view(scene, bmin, bmax, t, a, b, +1, W, H)
        f_img, f_out, f_mode = compose(col, cnt, mode, X, Y, eff)
        col, cnt, X, Y, pb = render_view(scene, bmin, bmax, t, a, b, -1, W, H)
        b_img, b_out, b_mode = compose(col, cnt, mode, X, Y, eff)
        pf = bmax[t] if pf is None else pf; pb = bmin[t] if pb is None else pb
        if pf - pb < 0.002:
            c0 = (pf + pb) / 2; pf, pb = c0 + 0.001, c0 - 0.001
        views.append(('front', f_img, a, b, t, +1, pf))
        views.append(('back', b_img, a, b, t, -1, pb))
    # 가장자리 다듬기: 앞·뒤 그림을 합친 불투명 영역에서 가장자리 칸/줄의 불투명 비율이 TRIM_FRAC 미만이면(판 밖으로 튀어나온
    # 문고리 고리·돌쩌귀만 있는 띠) 최대 TRIM_MAX 까지 잘라 내고 판 크기도 그만큼 줄임 — 짝 문짝 사이 빈틈·겹침 방지
    M = np.zeros((H, W), bool)
    for v in views:
        M |= v[1][..., 3] >= 0.5
    if M.any():
        colf = M.mean(0); rowf = M.mean(1); mx = int(round(TRIM_MAX * eff))

        def lead(f):
            n = 0
            while n < min(mx, len(f) - 4) and f[n] < TRIM_FRAC:
                n += 1
            return n
        cl, cr, rt, rb = lead(colf), lead(colf[::-1]), lead(rowf), lead(rowf[::-1])
        if cl or cr or rt or rb:
            da = (bmax[a] - bmin[a]) / W; db = (bmax[b] - bmin[b]) / H
            views = [(v[0], v[1][rt:H - rb, cl:W - cr].copy()) + tuple(v[2:]) for v in views]
            bmin[a] += cl * da; bmax[a] -= cr * da
            bmax[b] -= rt * db; bmin[b] += rb * db      # 행 0 = 위(세로 축 큰 쪽)
            W -= cl + cr; H -= rt + rb
    # 좁은 투명 띠 막기(투명 모드 제외) 후 바깥 비율 다시
    outs = []
    for v in views:
        vm = f_mode if v[0] == 'front' else b_mode
        if vm != 'clear':
            v[1][..., 3] = close_border(v[1][..., 3], eff)
        outs.append(float((v[1][..., 3] < 0.5).mean()))
    f_out = max(outs) if outs else 0.0; b_out = f_out
    T = max(2, int(math.ceil(ext[t] * eff)))
    if style == 'box':
        for ax, horiz, vert, w_, h_ in ((a, t, b, T, H), (b, a, t, W, T)):
            for sgn in (+1, -1):
                c2, n2, X2, Y2, p2 = render_view(scene, bmin, bmax, ax, horiz, vert, sgn, w_, h_, horiz=horiz, vert=vert)
                img = compose(c2, n2, 'solid', X2, Y2, eff)[0]
                img[..., 3] = 1.0
                p2 = (bmax[ax] if sgn > 0 else bmin[ax]) if p2 is None else p2
                views.append(('side', img, horiz, vert, ax, sgn, p2))
    clip = max(f_out, b_out) > 0.002
    if not clip:
        for v in views:
            v[1][..., 3] = 1.0
    # 아틀라스 배치: 1줄 = 앞, 뒤, (±a 옆면), 2줄 = (±b 옆면)
    row1 = [v for v in views if v[0] != 'side' or v[4] == a]
    row2 = [v for v in views if v[0] == 'side' and v[4] == b]
    AW = sum(v[1].shape[1] + 2 * PAD for v in row1)
    AW = max(AW, sum(v[1].shape[1] + 2 * PAD for v in row2))
    AH = H + 2 * PAD + (T + 2 * PAD if row2 else 0)
    atlas = np.zeros((AH, AW, 4), np.float32)
    have = np.zeros((AH, AW), bool)
    rects = []
    x = 0
    for v in row1:
        h, w = v[1].shape[:2]
        atlas[PAD:PAD + h, x + PAD:x + PAD + w] = v[1]; have[PAD:PAD + h, x + PAD:x + PAD + w] = True
        rects.append((v, x + PAD, PAD, w, h)); x += w + 2 * PAD
    x = 0; y0 = H + 2 * PAD
    for v in row2:
        h, w = v[1].shape[:2]
        atlas[y0 + PAD:y0 + PAD + h, x + PAD:x + PAD + w] = v[1]; have[y0 + PAD:y0 + PAD + h, x + PAD:x + PAD + w] = True
        rects.append((v, x + PAD, y0 + PAD, w, h)); x += w + 2 * PAD
    # 여백은 가장 가까운 색으로(밉맵 번짐 방지), 알파는 칸 가장자리 값
    atlas[..., :3] = nearest_fill(atlas[..., :3], have)
    atlas[..., 3] = nearest_fill(atlas[..., 3:4], have)[..., 0]
    # 판 메시
    P = []; N = []; U = []; I = []
    planes = {}
    for (v, px, py, w, h) in rects:
        planes[(v[0], v[4], v[5])] = v[6]
    for (v, px, py, w, h) in rects:
        name, img, horiz, vert, nax, sgn, plane = v
        lo = bmin.copy(); hi = bmax.copy()
        if name == 'side':
            # 옆면은 두께 방향으로 앞·뒤 판 사이까지만
            lo[t] = planes.get(('back', t, -1), bmin[t]); hi[t] = planes.get(('front', t, +1), bmax[t])
        # 네 모서리: (가로 lo, 세로 lo) (가로 hi, 세로 lo) (가로 hi, 세로 hi) (가로 lo, 세로 hi)
        corners = []
        uvs = []
        for (hs, vs) in ((0, 0), (1, 0), (1, 1), (0, 1)):
            p = np.zeros(3)
            p[nax] = plane
            p[horiz] = lo[horiz] if hs == 0 else hi[horiz]
            p[vert] = lo[vert] if vs == 0 else hi[vert]
            corners.append(p)
            fh = (p[horiz] - bmin[horiz]) / max(1e-9, bmax[horiz] - bmin[horiz])
            fv = (p[vert] - bmin[vert]) / max(1e-9, bmax[vert] - bmin[vert])
            u = (px + fh * w) / AW
            vv = 1.0 - (py + (1.0 - fv) * h) / AH
            uvs.append((u, vv))
        nrm = np.zeros(3); nrm[nax] = sgn
        c = np.array(corners)
        base = len(P)
        P.extend(c); N.extend([nrm] * 4); U.extend(uvs)
        # 앞면 = cross(v1-v0, v2-v0) 가 nrm 방향
        tri1 = [0, 1, 2]; tri2 = [0, 2, 3]
        cr = np.cross(c[1] - c[0], c[2] - c[0])
        if np.dot(cr, nrm) < 0:
            tri1 = [0, 2, 1]; tri2 = [0, 3, 2]
        I.extend([base + i for i in tri1]); I.extend([base + i for i in tri2])
    info = dict(W=W, H=H, T=T, ppm=eff, clip=bool(clip), out=(f_out, b_out), mode=(f_mode, b_mode), axes=(t, a, b), atlas=(AW, AH),
                planes=[round(float(p), 4) for p in planes.values()])
    return to_img(atlas), (np.array(P, np.float32), np.array(N, np.float32), np.array(U, np.float32), np.array(I, np.uint32)), info
