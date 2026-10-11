"""단지 통째 임포스터(지붕 모양 상자): 오브젝트마다 껍데기(hullgen) → 여러 건물이 함께 쓰는 4096 페이지에 조각을 펴고
→ 텍셀마다 원본 쪽으로 광선을 쏴 기본색(AO 를 곱함)·물체 공간 노멀을 굽는다.

python3 impcx.py <단지> [페이지 수(0 = 자동)] [건물 이름 쉼표 목록(없으면 전부)]
입력: load_cx(단지 단순화판 LOD0 을 PalaceBinExport 로 내보낸 것 + 텍스처 캐시).
출력 out/<단지>/:
  <단지>_Imp.bin   HNMS: 오브젝트마다 '<이름>_Imp'(서브메시 = 페이지, 재질 IMP_<단지>_P<k>)
  <단지>_Keep.bin  HNMS: 그대로 둘 원본(삼각형이 아주 적은 오브젝트, 넓은 땅판) — 재질 = 원래 재질 이름
  P<k>_BC.png      기본색(sRGB 그대로, AO 를 곱함) PAGE²
  P<k>_NOS.png     물체 공간 노멀(0.5 + 0.5·n) PAGE/2 ²
  layout.json      오브젝트·페이지·삼각형·밀도 기록
판(문·창 카드)은 굽기 장면에만 넣고 결과에서는 빠짐(껍데기 벽에 그림으로 들어감).
"""
import os, sys, json, time
import numpy as np
from PIL import Image
from scipy import ndimage
import common
from common import Bld
from load_cx import load_complex, merge
from hullgen import build_hull
import bake as BK
from export import write_meshes

PAGE = int(os.environ.get('IMP_PAGE', '4096'))   # 페이지 크기(작은 묶음은 2048)
PAD = 4
SS = 2                 # 텍셀당 2×2 광선
AO_RAYS = 8
AO_DIST = 1.2
AO_MIX = 0.5           # 색 × (1 − AO_MIX + AO_MIX·AO)
FILL = 0.88            # 페이지 수 어림에 쓰는 채움률(길로틴)
KEEP_TRIS = 3000       # 이보다 적은 오브젝트는 원본 그대로
GROUND_AREA = 50.0     # 재질의 삼각형 평균 넓이가 이보다 크면 땅판 → 원본 그대로
CHUNK = 400000
# (부분, 방향 종류) → (px/m 기준, 앞 여유 m, 뒤 깊이 m)
DENS = {('roof', 'T'): (32, 0.6, 1.2), ('roof', 'B'): (12, 0.4, 1.5), ('roof', 'S'): (24, 0.4, 1.0),
        ('body', 'S'): (32, 0.35, 1.2), ('body', 'T'): (12, 0.3, 0.6), ('body', 'B'): (4, 0.3, 0.6),
        ('base', 'S'): (24, 0.3, 0.8), ('base', 'T'): (16, 0.3, 0.5), ('base', 'B'): (4, 0.3, 0.5),
        ('pillar', 'S'): (24, 0.2, 0.4), ('pillar', 'T'): (8, 0.2, 0.4), ('pillar', 'B'): (4, 0.2, 0.4)}
# 멀리서만 보는 단지(동궁 등): 벽·기단은 덜 촘촘히(길에서 100 m 면 화면에 약 9 px/m)
DENS_FAR = {('roof', 'T'): (32, 0.6, 1.2), ('roof', 'B'): (10, 0.4, 1.5), ('roof', 'S'): (20, 0.4, 1.0),
            ('body', 'S'): (20, 0.35, 1.2), ('body', 'T'): (8, 0.3, 0.6), ('body', 'B'): (4, 0.3, 0.6),
            ('base', 'S'): (16, 0.3, 0.8), ('base', 'T'): (16, 0.3, 0.5), ('base', 'B'): (4, 0.3, 0.5),
            ('pillar', 'S'): (16, 0.2, 0.4), ('pillar', 'T'): (8, 0.2, 0.4), ('pillar', 'B'): (4, 0.2, 0.4)}
if os.environ.get('IMP_PROFILE') == 'far':
    DENS = DENS_FAR
PART_NAMES = {0: 'roof', 1: 'body', 2: 'base', 3: 'pillar'}
# 결과 폴더: IMPOSTOR_OUT 또는 Unity 프로젝트의 Captures\impostor\out(Assets 밖 — 안에 두면 Unity 가 PNG 를 가져옴) 또는 스크립트 옆 out
OUTROOT = os.environ.get('IMPOSTOR_OUT') or (r'D:\Unity\Haesin\Captures\impostor\out' if os.path.isdir(r'D:\Unity\Haesin\Captures\impostor')
                                             else os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out'))


def is_card(name):
    return 'Card' in name or 'Panel' in name or 'GateDoors' in name


def tri_area(P, T):
    return 0.5 * np.linalg.norm(np.cross(P[T[:, 1]] - P[T[:, 0]], P[T[:, 2]] - P[T[:, 0]]), axis=1)


def make_hulls(cx, objs, mats, only=None, log=print):
    names = [n for n in objs if not is_card(n)]
    if only:
        names = [n for n in names if n in only]
    cards = [n for n in objs if is_card(n)]
    cP = [];
    cT = []; base = 0
    for n in cards:
        cP.append(objs[n]['P']); cT.append(objs[n]['T'] + base); base += len(objs[n]['P'])
    cP = np.concatenate(cP) if cP else np.zeros((0, 3)); cT = np.concatenate(cT) if cT else np.zeros((0, 3), np.int64)
    cC = cP[cT].mean(1) if len(cT) else np.zeros((0, 3))
    hulls = {}; keep = {}
    for nm in names:
        o = objs[nm]; P, T, TM = o['P'], o['T'], o['TM']
        if len(T) < KEEP_TRIS:
            keep[nm] = np.ones(len(T), bool); log('%-18s keep (tris %d)' % (nm, len(T))); continue
        ar = tri_area(P, T)
        ground = np.zeros(len(T), bool)
        for m in np.unique(TM):
            s = TM == m
            if ar[s].mean() > GROUND_AREA:
                ground[s] = True
        Tb = T[~ground]
        lo = P[Tb].reshape(-1, 3).min(0) - 0.5; hi = P[Tb].reshape(-1, 3).max(0) + 0.5
        sel = np.all((cC >= lo) & (cC <= hi), 1) if len(cT) else np.zeros(0, bool)
        # 이웃 오브젝트(판·땅판 제외) 중 내 경계 + 3 m 안 삼각형 — 경계에 걸친 통로를 닫는 데만 씀
        cxP = []; cxT = []; nb = 0
        for other in names:
            if other == nm or len(objs[other]['T']) < KEEP_TRIS:
                continue
            oo = objs[other]; C = oo['P'][oo['T']].mean(1)
            s_ = np.all((C >= lo - 3.0) & (C <= hi + 3.0), 1)
            if s_.any():
                cxP.append(oo['P']); cxT.append(oo['T'][s_] + nb); nb += len(oo['P'])
        ctx = (np.concatenate(cxP), np.concatenate(cxT)) if cxP else None
        r = build_hull(P, Tb, extra=(cP, cT[sel]) if sel.any() else None, name=nm, log=log, ctx=ctx)
        if r is None:
            keep[nm] = np.ones(len(T), bool); log('%-18s keep (no hull)' % nm); continue
        V, F, part, info = r
        hulls[nm] = dict(V=V, F=F, part=part, info=info, src_tris=int(len(T)))
        if ground.any():
            keep[nm] = ground; log('%-18s ground kept: %d tris (%s)' % (nm, ground.sum(), sorted({mats[m]['name'] for m in np.unique(TM[ground])})))
    return hulls, keep


def make_charts(hulls):
    charts = []
    for nm, h in hulls.items():
        V, F, part = h['V'], h['F'], h['part']
        fn, lab = BK.labels(V, F)
        h['fn'] = fn
        for p in np.unique(part):
            for L in ('+Y', '-Y', '+X', '-X', '+Z', '-Z'):
                sel = (part == p) & (lab == L)
                if not sel.any():
                    continue
                for tl in BK.components(F, sel):
                    tl = np.array(tl)
                    vids = np.unique(F[tl].ravel())
                    u, v = BK.proj(L, V[vids])
                    dens, front, back = DENS[(PART_NAMES[int(p)], BK.kind(L))]
                    charts.append(dict(obj=nm, part=int(p), lab=L, tris=tl, vids=vids, u=u - u.min(), v=v - v.min(),
                                       wm=float(u.max() - u.min()), hm=float(v.max() - v.min()), dens=dens, front=front, back=back))
    return charts


def pack(charts, s):
    """길로틴 빈칸 채우기(최소 짧은 변 남김): 높이 순으로, 빈 직사각형 중 가장 꼭 맞는 곳에 넣고 남은 곳을 둘로 나눔.
    예전 선반 방식은 키 큰 선반의 남는 폭을 못 써서 동궁 페이지의 33 % 만 찼음(2026-09-30)."""
    lim = PAGE - 2 * PAD - 2
    items = []
    for i, c in enumerate(charts):
        cs = s
        w = c['wm'] * c['dens'] * cs; h = c['hm'] * c['dens'] * cs
        if max(w, h) > lim:
            cs *= lim / max(w, h); w = c['wm'] * c['dens'] * cs; h = c['hm'] * c['dens'] * cs
        items.append((i, int(np.ceil(w)) + 2 * PAD, int(np.ceil(h)) + 2 * PAD, cs))
    items.sort(key=lambda t: (-t[2], -t[1]))
    cap = 4 * len(items) + 16
    FP = np.zeros(cap, np.int32); FX = np.zeros(cap, np.int64); FY = np.zeros(cap, np.int64); FW = np.zeros(cap, np.int64); FH = np.zeros(cap, np.int64)
    alive = np.zeros(cap, bool); nf = 0; pages = 0; place = {}
    def add(p, x, y, w, h):
        nonlocal nf
        if w <= 0 or h <= 0:
            return
        FP[nf] = p; FX[nf] = x; FY[nf] = y; FW[nf] = w; FH[nf] = h; alive[nf] = True; nf += 1
    for i, w, h, cs in items:
        ok = alive[:nf] & (FW[:nf] >= w) & (FH[:nf] >= h)
        if not ok.any():
            add(pages, 0, 0, PAGE, PAGE); pages += 1
            ok = alive[:nf] & (FW[:nf] >= w) & (FH[:nf] >= h)
        sc = np.where(ok, np.minimum(FW[:nf] - w, FH[:nf] - h) * 8 + FP[:nf] * (PAGE * 16), np.iinfo(np.int64).max)
        r = int(np.argmin(sc))
        p, x, y, fw, fh = int(FP[r]), int(FX[r]), int(FY[r]), int(FW[r]), int(FH[r])
        alive[r] = False
        place[i] = (p, x, y, cs)
        if fw - w < fh - h:        # 짧은 쪽으로 자름
            add(p, x + w, y, fw - w, h); add(p, x, y + h, fw, fh - h)
        else:
            add(p, x + w, y, fw - w, fh); add(p, x, y + h, w, fh - h)
    return place, pages


def layout(charts, pages=0, log=print):
    area = sum(c['wm'] * c['hm'] * c['dens'] ** 2 for c in charts)
    pad_area = sum((c['wm'] * c['dens'] + 2 * PAD) * (c['hm'] * c['dens'] + 2 * PAD) for c in charts)
    need = pad_area / (FILL * PAGE * PAGE)
    if pages <= 0:
        pages = max(1, int(np.floor(need + 0.7)))
    s = min(1.6, np.sqrt(pages * FILL * PAGE * PAGE / pad_area))
    while True:
        place, n = pack(charts, s)
        if n <= pages:
            break
        s *= 0.97
    log('charts %d  area@1 %.1f Mpx (+pad %.1f)  need %.2f pages → %d pages, scale %.3f' % (len(charts), area / 1e6, pad_area / 1e6, need, n, s))
    return place, n, s


def build_meshes(hulls, charts, place, cx):
    """오브젝트마다 조각별로 정점을 복사하고 UV = 페이지 픽셀/PAGE. 삼각형마다 (페이지, 조각) 기록."""
    by_obj = {}
    for ci, c in enumerate(charts):
        by_obj.setdefault(c['obj'], []).append(ci)
    out = {}
    for nm, h in hulls.items():
        V, F, fn = h['V'], h['F'], h['fn']
        NV = []; NN = []; NU = []; base = 0
        subs = {}; tri_rec = []
        for ci in by_obj.get(nm, []):
            c = charts[ci]; pg, ox, oy, cs = place[ci]
            s = c['dens'] * cs
            px = ox + PAD + c['u'] * s; py = oy + PAD + c['v'] * s
            remap = -np.ones(int(c['vids'].max()) + 1, np.int64); remap[c['vids']] = np.arange(len(c['vids']))
            acc = np.zeros((len(c['vids']), 3))
            for k in range(3):
                np.add.at(acc, remap[F[c['tris'], k]], fn[c['tris']])
            acc /= np.maximum(np.linalg.norm(acc, axis=1), 1e-12)[:, None]
            NV.append(V[c['vids']]); NN.append(acc); NU.append(np.stack([px / PAGE, py / PAGE], 1))
            tt = remap[F[c['tris']]] + base
            subs.setdefault(pg, []).append(tt)
            tri_rec.append((pg, ci, tt))
            base += len(c['vids'])
        NV = np.concatenate(NV); NN = np.concatenate(NN); NU = np.concatenate(NU)
        pages = sorted(subs)
        mesh = dict(pos=NV.astype(np.float32), nrm=NN.astype(np.float32), uv=NU.astype(np.float32),
                    subs=[np.concatenate(subs[p]).reshape(-1).astype(np.int32) for p in pages],
                    mats=['IMP_%s_P%d' % (cx, p) for p in pages])
        out[nm] = dict(mesh=mesh, tri_rec=tri_rec)
    return out


def raster(uvpx, tris):
    """삼각형마다 덮는 픽셀 → (y, x, 삼각형 번호, 무게 3)"""
    ys = []; xs = []; ts = []; ws = []
    A = uvpx[tris[:, 0]]; B = uvpx[tris[:, 1]]; C = uvpx[tris[:, 2]]
    for ti in range(len(tris)):
        a = A[ti]; b = B[ti]; c = C[ti]
        x0 = int(np.floor(min(a[0], b[0], c[0]) - 1)); x1 = int(np.ceil(max(a[0], b[0], c[0]) + 1))
        y0 = int(np.floor(min(a[1], b[1], c[1]) - 1)); y1 = int(np.ceil(max(a[1], b[1], c[1]) + 1))
        xr = np.arange(max(x0, 0), min(x1, PAGE)); yr = np.arange(max(y0, 0), min(y1, PAGE))
        if len(xr) == 0 or len(yr) == 0:
            continue
        X, Y = np.meshgrid(xr + 0.5, yr + 0.5)
        det = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(det) < 1e-12:
            continue
        w0 = ((b[1] - c[1]) * (X - c[0]) + (c[0] - b[0]) * (Y - c[1])) / det
        w1 = ((c[1] - a[1]) * (X - c[0]) + (a[0] - c[0]) * (Y - c[1])) / det
        w2 = 1 - w0 - w1
        e = 0.75 / max(np.sqrt(abs(det)), 1e-6)
        ins = (w0 >= -e) & (w1 >= -e) & (w2 >= -e)
        if not ins.any():
            continue
        w = np.stack([w0[ins], w1[ins], w2[ins]], 1); w = np.clip(w, 0, None); w /= w.sum(1)[:, None]
        ys.append((Y[ins] - 0.5).astype(np.int32)); xs.append((X[ins] - 0.5).astype(np.int32))
        ts.append(np.full(ins.sum(), ti, np.int32)); ws.append(w.astype(np.float32))
    return np.concatenate(ys), np.concatenate(xs), np.concatenate(ts), np.concatenate(ws)


def bake_page(Bm, pg, meshes, charts, log=print):
    t0 = time.time()
    # 이 페이지의 삼각형 모으기
    TA = []; UVT = []; FR = []; BA = []
    for nm, mo in meshes.items():
        m = mo['mesh']
        for (p, ci, tt) in mo['tri_rec']:
            if p != pg:
                continue
            TA.append(m['pos'][tt].astype(np.float64)); UVT.append(m['uv'][tt].astype(np.float64) * PAGE)
            FR.append(np.full(len(tt), charts[ci]['front'])); BA.append(np.full(len(tt), charts[ci]['back']))
    TA = np.concatenate(TA); UVT = np.concatenate(UVT); FR = np.concatenate(FR); BA = np.concatenate(BA)
    nt = len(TA)
    uvflat = UVT.reshape(-1, 2); tris = np.arange(nt * 3).reshape(-1, 3)
    py, px, pt, pw = raster(uvflat, tris)
    key = py.astype(np.int64) * PAGE + px
    _, first = np.unique(key, return_index=True)
    py, px, pt, pw = py[first], px[first], pt[first], pw[first]
    log('page %d: tris %d texels %d (%.1f s)' % (pg, nt, len(py), time.time() - t0))
    A = TA[:, 0]; B = TA[:, 1]; C = TA[:, 2]
    fnorm = np.cross(B - A, C - A); fnorm /= np.maximum(np.linalg.norm(fnorm, axis=1), 1e-12)[:, None]
    uA = UVT[:, 0]; uB = UVT[:, 1]; uC = UVT[:, 2]
    e1 = B - A; e2 = C - A; d1 = uB - uA; d2 = uC - uA
    det = d1[:, 0] * d2[:, 1] - d1[:, 1] * d2[:, 0]; det = np.where(np.abs(det) < 1e-12, 1e-12, det)
    dPdu = (e1 * d2[:, 1:2] - e2 * d1[:, 1:2]) / det[:, None]; dPdv = (e2 * d1[:, 0:1] - e1 * d2[:, 0:1]) / det[:, None]
    n_tex = len(py)
    BC = np.zeros((PAGE, PAGE, 3), np.float32); NO = np.zeros((PAGE, PAGE, 3), np.float32); AO = np.zeros((PAGE, PAGE), np.float32)
    covered = np.zeros((PAGE, PAGE), bool)
    offs = [(-0.25, -0.25), (0.25, -0.25), (-0.25, 0.25), (0.25, 0.25)] if SS == 2 else [(0.0, 0.0)]
    rng = np.random.default_rng(7 + pg)
    hit_total = 0
    for s0 in range(0, n_tex, CHUNK):
        sl = slice(s0, min(s0 + CHUNK, n_tex))
        ti = pt[sl]; w = pw[sl].astype(np.float64)
        P0 = w[:, 0:1] * A[ti] + w[:, 1:2] * B[ti] + w[:, 2:3] * C[ti]
        N0 = fnorm[ti]; fr = FR[ti]; tmax = fr + BA[ti] + 2.0
        m = len(ti)
        col = np.zeros((m, 3), np.float32); nrm = np.zeros((m, 3), np.float32); cnt = np.zeros(m, np.float32)
        hitP = np.full((m, 3), np.nan, np.float32); hitN = np.zeros((m, 3), np.float32)
        for si, (du, dv) in enumerate(offs):
            Ps = P0 + dPdu[ti] * du + dPdv[ti] * dv
            O = (Ps + N0 * fr[:, None]).astype(np.float32); D = (-N0).astype(np.float32)
            t, tri, u, v = Bm.cast(O, D, 1000.0)
            good = (tri >= 0) & (t <= tmax)
            gi = np.nonzero(good)[0]
            if len(gi) == 0:
                continue
            c, n, tm = Bm.shade(tri[gi], u[gi], v[gi])
            flip = (n * D[gi]).sum(1) > 0
            n[flip] *= -1
            col[gi] += c; nrm[gi] += n; cnt[gi] += 1
            if si == 0:
                hitP[gi] = O[gi] + D[gi] * t[gi][:, None]; hitN[gi] = n
        have = cnt > 0
        col[have] /= cnt[have, None]
        nl = np.linalg.norm(nrm, axis=1); nrm[have] /= np.maximum(nl[have], 1e-9)[:, None]
        # AO(첫 광선이 맞은 점에서)
        ao = np.ones(m, np.float32)
        hv = np.nonzero(have & ~np.isnan(hitP[:, 0]))[0]
        if len(hv):
            n = hitN[hv].astype(np.float64); n /= np.maximum(np.linalg.norm(n, axis=1), 1e-9)[:, None]
            dirs = BK.cos_dirs(n, AO_RAYS, rng)
            O = np.repeat(hitP[hv] + n.astype(np.float32) * 0.004, AO_RAYS, axis=0).astype(np.float32)
            t, tri, _, _ = Bm.rm.cast(O, dirs.reshape(-1, 3).astype(np.float32), AO_DIST)
            ao[hv] = 1 - (tri >= 0).reshape(-1, AO_RAYS).mean(1)
        yy = py[sl][have]; xx = px[sl][have]
        BC[yy, xx] = col[have]; NO[yy, xx] = nrm[have]; AO[yy, xx] = ao[have]; covered[yy, xx] = True
        hit_total += int(have.sum())
        log('  page %d chunk %d/%d (%.0f s)' % (pg, s0 // CHUNK + 1, (n_tex + CHUNK - 1) // CHUNK, time.time() - t0))
    # AO 잡음 줄이기(덮인 곳끼리만 가우스 평균) → 색에 곱함
    wgt = ndimage.gaussian_filter(covered.astype(np.float32), 1.0)
    AOs = ndimage.gaussian_filter(AO * covered, 1.0) / np.maximum(wgt, 1e-6)
    BC *= (1 - AO_MIX + AO_MIX * np.clip(AOs, 0, 1))[..., None]
    idx = ndimage.distance_transform_edt(~covered, return_distances=False, return_indices=True).astype(np.int32)
    BC = BC[idx[0], idx[1]]; NO = NO[idx[0], idx[1]]
    del idx
    # 노멀 반 크기
    NOh = NO.reshape(PAGE // 2, 2, PAGE // 2, 2, 3).mean((1, 3))
    NOh /= np.maximum(np.linalg.norm(NOh, axis=2), 1e-9)[..., None]
    st = dict(page=pg, tris=int(nt), texels=int(n_tex), hit=float(hit_total / max(n_tex, 1)), sec=round(time.time() - t0, 1))
    log('page %d done: hit %.1f %% (%.0f s)' % (pg, 100 * st['hit'], st['sec']))
    return BC, NOh * 0.5 + 0.5, st


def save_png(arr, path, mode='RGB'):
    a = (np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8)[::-1]
    Image.fromarray(a, mode).save(path, optimize=False, compress_level=6)


def keep_meshes(objs, keep):
    out = []
    for nm, sel in keep.items():
        o = objs[nm]; T = o['T'][sel]; TM = o['TM'][sel]
        used, inv = np.unique(T.ravel(), return_inverse=True)
        Tn = inv.reshape(-1, 3)
        subs = []; mats = []
        for m in np.unique(TM):
            subs.append(Tn[TM == m].reshape(-1).astype(np.int32)); mats.append(int(m))
        out.append((nm, dict(pos=o['P'][used].astype(np.float32), nrm=o['N'][used].astype(np.float32), uv=o['U'][used].astype(np.float32),
                             subs=subs, mats=mats)))
    return out


# 빈터 채우기용 건물(단지 데모 원본에서 데모 x 구간으로 한 채씩) — 영추문 문 남쪽 T자·ㄱ자 둘(Gates/build_gates.py REGIONS 와 같은 구간)
SOURCES = {'YCMFill': ('Yeongchumun', {'YCM_BLD_T': (-23.0, 33.0), 'YCM_BLD_L1': (33.0, 65.0), 'YCM_BLD_L2': (65.0, 1000.0)})}


def load_source(cx):
    if cx in SOURCES:
        from load_bld import load_objects
        src, ranges = SOURCES[cx]
        return load_objects(src, ranges)
    return load_complex(cx)


def main():
    cx = sys.argv[1]; pages = int(sys.argv[2]) if len(sys.argv) > 2 else 0
    only = set(sys.argv[3].split(',')) if len(sys.argv) > 3 and sys.argv[3] else None
    od = os.path.join(OUTROOT, cx); os.makedirs(od, exist_ok=True)
    logf = open(os.path.join(od, 'log.txt'), 'a', encoding='utf-8')
    def log(*a):
        s = ' '.join(str(x) for x in a); print(s, flush=True); logf.write(s + '\n'); logf.flush()
    t0 = time.time()
    objs, mats = load_source(cx)
    log('== %s: %d objects, %d mats' % (cx, len(objs), len(mats)))
    hulls, keep = make_hulls(cx, objs, mats, only, log)
    charts = make_charts(hulls)
    place, n, s = layout(charts, pages, log)
    meshes = build_meshes(hulls, charts, place, cx)
    write_meshes(os.path.join(od, '%s_Imp.bin' % cx), [('%s_Imp' % nm, mo['mesh']) for nm, mo in meshes.items()])
    km = keep_meshes(objs, keep)
    if km:
        write_meshes(os.path.join(od, '%s_Keep.bin' % cx), [('%s_Keep' % nm, dict(m, mats=[mats[k]['name'] for k in m['mats']])) for nm, m in km])
    sv = {}
    for nm, mo in meshes.items():
        for k in ('pos', 'nrm', 'uv'):
            sv[nm + '|' + k] = mo['mesh'][k]
        for si, sb in enumerate(mo['mesh']['subs']):
            sv[nm + '|sub%d' % si] = sb
    np.savez(os.path.join(od, 'uvmesh.npz'), **sv)
    # 오브젝트 경계(단지 좌표 x0, x1, z0, z1, y0, y1) — 빈터 묶음 배치(fillplan.py)가 씀
    json.dump({nm: [float(mo['mesh']['pos'][:, 0].min()), float(mo['mesh']['pos'][:, 0].max()), float(mo['mesh']['pos'][:, 2].min()),
                    float(mo['mesh']['pos'][:, 2].max()), float(mo['mesh']['pos'][:, 1].min()), float(mo['mesh']['pos'][:, 1].max())]
               for nm, mo in meshes.items()}, open(os.path.join(od, 'bbox.json'), 'w'), indent=0)
    lay = dict(complex=cx, pages=n, page_size=PAGE, scale=s, dens={'%s_%s' % k: v[0] for k, v in DENS.items()}, ao_mix=AO_MIX,
               objects={nm: dict(tris=int(sum(len(x) for x in mo['mesh']['subs']) // 3), verts=int(len(mo['mesh']['pos'])), src_tris=hulls[nm]['src_tris'],
                                 pages=[int(mn.rsplit('_P', 1)[1]) for mn in mo['mesh']['mats']],
                                 info=hulls[nm]['info']) for nm, mo in meshes.items()},
               keep={nm: dict(tris=int(sel.sum()), mats=sorted({mats[m]['name'] for m in np.unique(objs[nm]['TM'][sel])})) for nm, sel in keep.items()},
               cards=[nm for nm in objs if is_card(nm)])
    json.dump(lay, open(os.path.join(od, 'layout.json'), 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
    tot = sum(v['tris'] for v in lay['objects'].values())
    log('hull tris %d (objects %d), keep tris %d, pages %d (%.0f s)' % (tot, len(meshes), sum(v['tris'] for v in lay['keep'].values()), n, time.time() - t0))
    if os.environ.get('NOBAKE'):
        return
    m = merge(objs, list(objs.keys())); m['mats'] = mats
    Bm = Bld(m)
    log('scene tris %d (%.0f s)' % (len(m['T']), time.time() - t0))
    stats = []
    for pg in range(n):
        bcp = os.path.join(od, 'P%d_BC.png' % pg)
        if os.path.exists(bcp) and not os.environ.get('FORCE'):
            log('page %d exists, skip' % pg); continue
        BC, NOS, st = bake_page(Bm, pg, meshes, charts, log)
        save_png(BC, bcp); save_png(NOS, os.path.join(od, 'P%d_NOS.png' % pg))
        del BC, NOS
        stats.append(st)
    lay['bake'] = stats
    json.dump(lay, open(os.path.join(od, 'layout.json'), 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
    log('all done (%.0f s)' % (time.time() - t0))


if __name__ == '__main__':
    main()
