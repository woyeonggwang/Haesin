"""껍데기에 그림 굽기: 상자 투영(면 방향 6가지)으로 조각(차트)을 나눠 아틀라스에 펴고, 텍셀마다 원본 쪽으로 광선을 쏘아
기본색(BC, sRGB)·물체 공간 노멀(NOS)·마스크(MASK: G = AO, A = 매끄러움)를 굽는다. 결과 메시는 HNMS 로 씀."""
import numpy as np, json, time, os, sys
from PIL import Image
from scipy import ndimage
from common import Bld, mat_color
from export import write_meshes

ATLAS = 2048
PAD = 6
SS = 2              # 텍셀당 2×2 광선
AO_RAYS = 12
AO_DIST = 1.2
# (부분, 방향종류) → (px/m, 앞쪽 여유 m, 뒤쪽 깊이 m)
PART_NAMES = {0: 'roof', 1: 'body', 2: 'base', 3: 'pillar'}
DENS = {('roof', 'T'): (64, 0.6, 1.2), ('roof', 'B'): (24, 0.4, 1.5), ('roof', 'S'): (40, 0.4, 1.0),
        ('body', 'S'): (64, 0.35, 1.2), ('body', 'T'): (32, 0.3, 0.6), ('body', 'B'): (16, 0.3, 0.6),
        ('base', 'S'): (48, 0.3, 0.8), ('base', 'T'): (24, 0.3, 0.5), ('base', 'B'): (8, 0.3, 0.5),
        ('pillar', 'S'): (64, 0.2, 0.4), ('pillar', 'T'): (16, 0.2, 0.4), ('pillar', 'B'): (8, 0.2, 0.4)}


def labels(V, F):
    A = V[F[:, 0]]; B = V[F[:, 1]]; C = V[F[:, 2]]
    n = np.cross(B - A, C - A); n /= np.maximum(np.linalg.norm(n, axis=1), 1e-12)[:, None]
    lab = np.empty(len(F), object)
    for i, v in enumerate(n):
        if v[1] > 0.55:
            lab[i] = '+Y'
        elif v[1] < -0.55:
            lab[i] = '-Y'
        elif abs(v[0]) >= abs(v[2]):
            lab[i] = '+X' if v[0] > 0 else '-X'
        else:
            lab[i] = '+Z' if v[2] > 0 else '-Z'
    return n, lab


def proj(lab, P):
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    return {'+Y': (x, z), '-Y': (x, -z), '+X': (z, y), '-X': (-z, y), '+Z': (-x, y), '-Z': (x, y)}[lab]


def kind(lab):
    return 'T' if lab == '+Y' else ('B' if lab == '-Y' else 'S')


def components(F, sel):
    """sel 삼각형들의 정점 공유 연결 성분"""
    idx = np.nonzero(sel)[0]
    parent = {}
    def find(a):
        while parent.get(a, a) != a:
            parent[a] = parent.get(parent[a], parent[a]); a = parent[a]
        return a
    for t in idx:
        a, b, c = F[t]
        ra, rb, rc = find(a), find(b), find(c)
        parent[rb] = ra; parent[find(rc)] = ra
    groups = {}
    for t in idx:
        groups.setdefault(find(F[t][0]), []).append(t)
    return list(groups.values())


def make_charts(V, F, part):
    n, lab = labels(V, F)
    charts = []
    for p in np.unique(part):
        for L in ('+Y', '-Y', '+X', '-X', '+Z', '-Z'):
            sel = (part == p) & (lab == L)
            if not sel.any():
                continue
            for tl in components(F, sel):
                tl = np.array(tl)
                vids = np.unique(F[tl].ravel())
                u, v = proj(L, V[vids])
                dens, front, back = DENS[(PART_NAMES[int(p)], kind(L))]
                charts.append(dict(part=int(p), lab=L, tris=tl, vids=vids, u=u, v=v, dens=dens, front=front, back=back))
    return charts, n


def pack(charts, scale):
    items = []
    for i, c in enumerate(charts):
        w = (c['u'].max() - c['u'].min()) * c['dens'] * scale; h = (c['v'].max() - c['v'].min()) * c['dens'] * scale
        items.append((i, int(np.ceil(w)) + 2 * PAD, int(np.ceil(h)) + 2 * PAD))
    items.sort(key=lambda t: -t[2])
    x = 0; y = 0; row_h = 0; place = {}
    for i, w, h in items:
        if w > ATLAS:
            return None
        if x + w > ATLAS:
            x = 0; y += row_h; row_h = 0
        place[i] = (x, y); x += w; row_h = max(row_h, h)
    if y + row_h > ATLAS:
        return None
    return place


def build_uv(V, F, part):
    charts, fn = make_charts(V, F, part)
    scale = 1.0
    while True:
        place = pack(charts, scale)
        if place is not None:
            break
        scale *= 0.95
    # 차트마다 정점 복사 + UV(픽셀 → 0..1, v 위쪽)
    NV = []; NN = []; NU = []; NF = []; tri_chart = np.zeros(len(F), np.int32); base = 0
    for ci, c in enumerate(charts):
        ox, oy = place[ci]
        s = c['dens'] * scale
        px = ox + PAD + (c['u'] - c['u'].min()) * s; py = oy + PAD + (c['v'] - c['v'].min()) * s
        remap = {int(v): k for k, v in enumerate(c['vids'])}
        # 정점 법선 = 이 차트 안에서 붙은 면 법선 평균
        acc = np.zeros((len(c['vids']), 3))
        for t in c['tris']:
            for vv in F[t]:
                acc[remap[int(vv)]] += fn[t]
        acc /= np.maximum(np.linalg.norm(acc, axis=1), 1e-12)[:, None]
        NV.append(V[c['vids']]); NN.append(acc); NU.append(np.stack([px / ATLAS, py / ATLAS], 1))
        for t in c['tris']:
            NF.append([remap[int(vv)] + base for vv in F[t]]); tri_chart[len(NF) - 1] = ci
        base += len(c['vids'])
    NV = np.concatenate(NV); NN = np.concatenate(NN); NU = np.concatenate(NU); NF = np.array(NF, np.int64)
    tri_chart = tri_chart[:len(NF)]
    used = sum(((c['u'].max() - c['u'].min()) * c['dens'] * scale) * ((c['v'].max() - c['v'].min()) * c['dens'] * scale) for c in charts)
    return dict(V=NV, N=NN, UV=NU, F=NF, tri_chart=tri_chart, charts=charts, scale=scale, fill=used / ATLAS / ATLAS)


def raster(uvpx, tris):
    """삼각형마다 덮는 픽셀(가운데) → (픽셀 y, x, 삼각형, 무게 3)"""
    out_y = []; out_x = []; out_t = []; out_w = []
    for ti, (a, b, c) in enumerate(tris):
        A = uvpx[a]; B = uvpx[b]; C = uvpx[c]
        x0 = int(np.floor(min(A[0], B[0], C[0]) - 1)); x1 = int(np.ceil(max(A[0], B[0], C[0]) + 1))
        y0 = int(np.floor(min(A[1], B[1], C[1]) - 1)); y1 = int(np.ceil(max(A[1], B[1], C[1]) + 1))
        xs = np.arange(max(x0, 0), min(x1, ATLAS)); ys = np.arange(max(y0, 0), min(y1, ATLAS))
        if len(xs) == 0 or len(ys) == 0:
            continue
        X, Y = np.meshgrid(xs + 0.5, ys + 0.5)
        det = (B[1] - C[1]) * (A[0] - C[0]) + (C[0] - B[0]) * (A[1] - C[1])
        if abs(det) < 1e-12:
            continue
        w0 = ((B[1] - C[1]) * (X - C[0]) + (C[0] - B[0]) * (Y - C[1])) / det
        w1 = ((C[1] - A[1]) * (X - C[0]) + (A[0] - C[0]) * (Y - C[1])) / det
        w2 = 1 - w0 - w1
        # 경계 픽셀도 포함(반 픽셀 여유) — 무게는 삼각형 안쪽으로 잘라 씀
        e = 0.75 / max(np.sqrt(abs(det)), 1e-6)
        ins = (w0 >= -e) & (w1 >= -e) & (w2 >= -e)
        if not ins.any():
            continue
        w = np.stack([w0[ins], w1[ins], w2[ins]], 1); w = np.clip(w, 0, None); w /= w.sum(1)[:, None]
        out_y.append((Y[ins] - 0.5).astype(np.int32)); out_x.append((X[ins] - 0.5).astype(np.int32))
        out_t.append(np.full(ins.sum(), ti, np.int32)); out_w.append(w)
    return np.concatenate(out_y), np.concatenate(out_x), np.concatenate(out_t), np.concatenate(out_w)


def cos_dirs(n, k, rng):
    """법선 n (m,3) 둘레 코사인 분포 방향 k 개씩 → (m,k,3)"""
    m = len(n)
    u1 = rng.random((m, k)); u2 = rng.random((m, k))
    r = np.sqrt(u1); th = 2 * np.pi * u2
    x = r * np.cos(th); y = r * np.sin(th); z = np.sqrt(np.maximum(0, 1 - u1))
    a = np.where(np.abs(n[:, 0:1]) < 0.9, np.array([[1.0, 0, 0]]), np.array([[0, 1.0, 0]]))
    t = np.cross(n, a); t /= np.linalg.norm(t, axis=1)[:, None]; bb = np.cross(n, t)
    return x[..., None] * t[:, None, :] + y[..., None] * bb[:, None, :] + z[..., None] * n[:, None, :]


def bake(Bm, M, log=print):
    t0 = time.time()
    b = Bm.b
    uvpx = M['UV'] * ATLAS
    py, px, pt, pw = raster(uvpx, M['F'])
    log('raster texels %d (%.1f s)' % (len(py), time.time() - t0))
    # 같은 픽셀을 여러 삼각형이 덮으면 첫 것만
    key = py.astype(np.int64) * ATLAS + px
    _, first = np.unique(key, return_index=True)
    py, px, pt, pw = py[first], px[first], pt[first], pw[first]
    Fv = M['F']; V = M['V']
    fnorm = np.cross(V[Fv[:, 1]] - V[Fv[:, 0]], V[Fv[:, 2]] - V[Fv[:, 0]]); fnorm /= np.linalg.norm(fnorm, axis=1)[:, None]
    ch = M['charts']; tc = M['tri_chart']
    front = np.array([ch[c]['front'] for c in tc])[pt]; back = np.array([ch[c]['back'] for c in tc])[pt]
    # 부분 픽셀(2×2) 위치: UV 에서 조금씩 밀어 무게 다시 계산하는 대신, 삼각형 평면 위에서 픽셀 크기만큼 흩뜨림
    # → 픽셀 가운데 점 + 삼각형 안 두 방향(UV 기울기) 오프셋
    A = V[Fv[pt, 0]]; Bv = V[Fv[pt, 1]]; C = V[Fv[pt, 2]]
    P0 = pw[:, 0:1] * A + pw[:, 1:2] * Bv + pw[:, 2:3] * C
    uA = uvpx[Fv[pt, 0]]; uB = uvpx[Fv[pt, 1]]; uC = uvpx[Fv[pt, 2]]
    # dP/du, dP/dv (삼각형마다 상수)
    e1 = Bv - A; e2 = C - A; d1 = uB - uA; d2 = uC - uA
    det = d1[:, 0] * d2[:, 1] - d1[:, 1] * d2[:, 0]; det = np.where(np.abs(det) < 1e-12, 1e-12, det)
    dPdu = (e1 * d2[:, 1:2] - e2 * d1[:, 1:2]) / det[:, None]; dPdv = (e2 * d1[:, 0:1] - e1 * d2[:, 0:1]) / det[:, None]
    N0 = fnorm[pt]
    n_tex = len(py)
    col = np.zeros((n_tex, 3), np.float32); nrm = np.zeros((n_tex, 3), np.float32); smo = np.zeros(n_tex, np.float32); cnt = np.zeros(n_tex, np.float32)
    hitP = np.full((n_tex, 3), np.nan, np.float32); hitN = np.zeros((n_tex, 3), np.float32)
    smooth_of = np.array([m['smooth'] for m in b['mats']], np.float32)
    offs = [(-0.25, -0.25), (0.25, -0.25), (-0.25, 0.25), (0.25, 0.25)] if SS == 2 else [(0, 0)]
    CH = 400000
    for si, (du, dv) in enumerate(offs):
        Ps = P0 + dPdu * du + dPdv * dv
        for s0 in range(0, n_tex, CH):
            sl = slice(s0, s0 + CH)
            O = (Ps[sl] + N0[sl] * front[sl, None]).astype(np.float32); D = (-N0[sl]).astype(np.float32)
            tmax = front[sl] + back[sl]
            t, tri, u, v = Bm.cast(O, D, 1000.0)
            ok = (tri >= 0) & (t <= tmax)
            # 못 맞으면 더 깊이(2 m 더)
            t2 = t.copy(); tri2 = tri.copy()
            miss = ~ok
            ok2 = miss & (tri >= 0) & (t <= tmax + 2.0)
            good = ok | ok2
            if not good.any():
                continue
            gi = np.nonzero(good)[0]
            c, n, tm = Bm.shade(tri[gi], u[gi], v[gi])
            flip = (n * D[gi]).sum(1) > 0
            n[flip] *= -1
            idx = s0 + gi
            col[idx] += c; nrm[idx] += n; smo[idx] += smooth_of[tm]; cnt[idx] += 1
            if si == 0:
                hitP[idx] = O[gi] + D[gi] * t[gi][:, None]; hitN[idx] = n
        log('sample %d/%d (%.1f s)' % (si + 1, len(offs), time.time() - t0))
    have = cnt > 0
    col[have] /= cnt[have, None]; smo[have] /= cnt[have]
    nl = np.linalg.norm(nrm, axis=1); nrm[have] /= np.maximum(nl[have], 1e-9)[:, None]
    # AO: 첫 부분 광선의 맞은 점에서
    rng = np.random.default_rng(7)
    ao = np.ones(n_tex, np.float32)
    hv = np.nonzero(have & ~np.isnan(hitP[:, 0]))[0]
    for s0 in range(0, len(hv), 100000):
        ids = hv[s0:s0 + 100000]
        n = hitN[ids].astype(np.float64); n /= np.maximum(np.linalg.norm(n, axis=1), 1e-9)[:, None]
        dirs = cos_dirs(n, AO_RAYS, rng)
        O = np.repeat(hitP[ids] + n.astype(np.float32) * 0.004, AO_RAYS, axis=0).astype(np.float32)
        D = dirs.reshape(-1, 3).astype(np.float32)
        t, tri, _, _ = Bm.cast(O, D, AO_DIST)
        occ = (tri >= 0).reshape(-1, AO_RAYS).mean(1)
        ao[ids] = 1 - occ
    log('ao (%.1f s)' % (time.time() - t0))
    # 이미지(행 0 = v 0 = 아래)
    BC = np.zeros((ATLAS, ATLAS, 3), np.float32); NO = np.zeros((ATLAS, ATLAS, 3), np.float32); MK = np.zeros((ATLAS, ATLAS, 4), np.float32)
    covered = np.zeros((ATLAS, ATLAS), bool)
    BC[py[have], px[have]] = col[have]; NO[py[have], px[have]] = nrm[have] * 0.5 + 0.5
    MK[py[have], px[have], 1] = ao[have]; MK[py[have], px[have], 3] = smo[have]
    covered[py[have], px[have]] = True
    idx = ndimage.distance_transform_edt(~covered, return_distances=False, return_indices=True)
    BC = BC[tuple(idx)]; NO = NO[tuple(idx)]; MK = MK[tuple(idx)]
    MK[..., 0] = 0.0; MK[..., 2] = 0.0
    log('texels %d, hit %.1f %%, (%.1f s)' % (n_tex, 100 * have.mean(), time.time() - t0))
    return BC, NO, MK, dict(texels=int(n_tex), hit=float(have.mean()), ao_mean=float(ao[have].mean()))


def save_png(arr, path, mode='RGB'):
    a = (np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8)[::-1]
    Image.fromarray(a, mode).save(path)


if __name__ == '__main__':
    from load_bld import load_building
    name = sys.argv[1] if len(sys.argv) > 1 else 'YCM_BLD_L1'
    h = np.load('hull.npz'); V, F, part = h['V'], h['F'], h['part']
    M = build_uv(V, F, part)
    print('charts', len(M['charts']), 'scale %.3f fill %.2f' % (M['scale'], M['fill']), 'verts', len(M['V']), 'tris', len(M['F']))
    Bm = Bld(load_building())
    BC, NO, MK, st = bake(Bm, M)
    os.makedirs('out', exist_ok=True)
    save_png(BC, 'out/%s_Imp_BC.png' % name); save_png(NO, 'out/%s_Imp_NOS.png' % name); save_png(MK, 'out/%s_Imp_MASK.png' % name, 'RGBA')
    mesh = dict(pos=M['V'].astype(np.float32), nrm=M['N'].astype(np.float32), uv=M['UV'].astype(np.float32), subs=[M['F'].reshape(-1).astype(np.int32)], mats=['IMP_%s' % name])
    write_meshes('out/%s_Imp.bin' % name, [('%s_Imp' % name, mesh)])
    np.savez('out/%s_uvmesh.npz' % name, V=M['V'], N=M['N'], UV=M['UV'], F=M['F'])
    json.dump(dict(st, charts=len(M['charts']), scale=M['scale'], fill=M['fill'], verts=len(M['V']), tris=len(M['F'])), open('out/%s_stats.json' % name, 'w'), indent=1)
    print(st)
