"""신사 고원 돌 축대(이시가키) 만들기(2026-10-04) — 성 이시가키와 같은 방식.
LOD0 = 돌 하나하나(보로노이 칸, 앞면·모따기·옆면, 돌마다 약 25~35 삼각형, 재질 Ishigaki_Natural_Stone_1~7 무작위)
       + 돌 뒤 줄눈 껍데기(Ishigaki_Shadowed_Stone_Joints) + 윗면 갓돌(Ishigaki_Worn_Step_And_Coping_Stone)
LOD1 = 껍데기 + 갓돌에 LOD0 를 구운 그림(페이지 IMP_JPShrineWall_P0).
축대 기울기 74.5°(성 이시가키 서쪽 면에서 잰 값), 높이는 shrine3.py 가 만든 땅(wall_G.npy)에서 발치를 찾음.
좌표 = Unity 월드에서 원점 O 를 뺀 값(오브젝트를 O 에 놓음). 출력 HNMS: SHRINE_WALL_LOD0 · SHRINE_WALL_LOD1(굽기 뒤) · SHRINE_WALL_COL(충돌)."""
import os, sys, json, math, time
os.environ.setdefault('IMPOSTOR_DATA', '/home/claude/jplod/data'); os.environ.setdefault('IMPOSTOR_TEX', '/home/claude/jplod/data/tex')
os.environ.setdefault('IMP_PAGE', '2048')
for d in ['/home/claude/jplod', '/home/claude/impostor', '/home/claude/palace_simplify/hnm', '/home/claude/palace_simplify']:
    sys.path.append(d)
import numpy as np
from scipy.spatial import Voronoi
R = 1025; CELL = 1500 / 1024; OXT, OZT = 1001, -1945
JT = '/home/claude/jptown/'
G = np.load(JT + 'wall_G.npy'); Hfin = np.load(JT + 'H_shrine3.npy')
TS = 44.0
O = np.array([1687.5, TS, -1082.0])
TH = math.radians(74.5); CH, SH = math.cos(TH), math.sin(TH)   # 경사 길이 1 당 수평 CH, 수직 SH
GW = SH / CH
EMB = 0.6        # 땅속으로 묻는 깊이(경사 길이)
rng = np.random.default_rng(11)


def bil(A, x, z):
    fx = (x - OXT) / CELL; fz = (z - OZT) / CELL
    i = int(np.floor(fx)); j = int(np.floor(fz)); a = fx - i; b = fz - j
    return (A[j, i] * (1 - a) * (1 - b) + A[j, i + 1] * a * (1 - b) + A[j + 1, i] * (1 - a) * b + A[j + 1, i + 1] * a * b)


# 변: 시작 모서리, 변 방향 U, 바깥 N(수평), 길이
SIDES = [('W', (1668, -1102), (0, 1), (-1, 0), 40.0), ('N', (1668, -1062), (1, 0), (0, 1), 39.0),
         ('E', (1707, -1062), (0, -1), (1, 0), 40.0), ('S', (1707, -1102), (-1, 0), (0, -1), 39.0)]
RAMP = (1685.0, 1691.0)      # 북쪽 경사로 자리(축대 끊김)


def foot_s(P0, U, N, u):
    """변 위치 u 에서 발치까지 경사 길이(땅 위로 보이는 부분). 축대가 없으면 0."""
    x0 = P0[0] + U[0] * u; z0 = P0[1] + U[1] * u
    for s in np.arange(0.02, 30, 0.02):
        d = s * CH; y = TS - s * SH
        if y <= bil(G, x0 + N[0] * d, z0 + N[1] * d):
            return s
    return 30.0


def clip(poly, a, b, c):
    """볼록 다각형을 a*u + b*s <= c 쪽으로 자름(Sutherland–Hodgman)."""
    out = []
    n = len(poly)
    for i in range(n):
        p = poly[i]; q = poly[(i + 1) % n]
        fp = a * p[0] + b * p[1] - c; fq = a * q[0] + b * q[1] - c
        if fp <= 0: out.append(p)
        if (fp < 0) != (fq < 0) and fp != fq:
            t = fp / (fp - fq); out.append((p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t))
    return out


def domain_side(name, P0, U, N, L):
    """변마다 (u 범위 조각들, 바닥 곡선 sb(u)) — u 는 -d..L+d (모서리 맞댐)."""
    us = np.arange(-4.0, L + 4.01, 0.25)
    sb = np.array([foot_s(P0, U, N, u) for u in us])
    vis = sb.copy()
    sb = np.where(vis > 0.25 / SH, sb + EMB, 0.0)          # 보이는 높이 25 cm 미만은 축대 없음
    pieces = [(-1e9, 1e9)]
    if name == 'N':
        pieces = [(-1e9, RAMP[0] - P0[0]), (RAMP[1] - P0[0], 1e9)]
    return us, sb, pieces


def stones_for(name, P0, U, N, L, out, shell, ends):
    us, sb, pieces = domain_side(name, P0, U, N, L)
    smax = sb.max()
    if smax <= 0: return 0
    f = lambda u: np.interp(u, us, sb)
    P0 = np.array([P0[0], 0, P0[1]], float); Uv = np.array([U[0], 0, U[1]], float); Nv = np.array([N[0], 0, N[1]], float)
    Dn = Nv * CH - np.array([0, 1, 0]) * SH          # 경사 아래 방향(단위)
    Fn = Nv * SH + np.array([0, 1, 0]) * CH          # 면 바깥 법선
    def to3(u, s, off=0.0):
        p = P0 + Uv * u + Dn * s + Fn * off
        return p + np.array([0, TS, 0])
    # 점 뿌리기: 엇갈린 격자 + 흔들기(돌 크기 약 1.2 m)
    sp = 1.2; pts = []
    for k, s in enumerate(np.arange(-sp, smax + sp, sp * 0.87)):
        off = (k % 2) * sp * 0.5
        for u in np.arange(-6 - off, L + 6, sp):
            pts.append((u + rng.uniform(-0.33, 0.33) * sp, s + rng.uniform(-0.25, 0.25) * sp))
    pts = np.array(pts)
    vor = Voronoi(pts)
    n_st = 0
    for pi, ri in enumerate(vor.point_region):
        reg = vor.regions[ri]
        if -1 in reg or len(reg) < 3: continue
        poly = [tuple(vor.vertices[k]) for k in reg]
        # 볼록 칸을 영역으로: 윗변 s>=0, 모서리 맞댐 u>=-CH*s, u<=L+CH*s, 경사로 조각, 바닥(칸 u 범위 현으로)
        poly = clip(poly, 0, -1, 0)
        poly = clip(poly, -1, -CH, 0)          # u >= -CH s
        poly = clip(poly, 1, -CH, L)           # u <= L + CH s
        if len(poly) < 3: continue
        uu = [p[0] for p in poly]
        u0, u1 = min(uu), max(uu)
        ok = False
        for (a, b) in pieces:
            if u1 > a and u0 < b:
                poly = clip(poly, -1, 0, -a) if a > -1e8 else poly
                poly = clip(poly, 1, 0, b) if b < 1e8 else poly
                ok = True; break
        if not ok or len(poly) < 3: continue
        uu = [p[0] for p in poly]; u0, u1 = min(uu), max(uu)
        f0, f1 = f(u0), f(u1)
        if max(f0, f1) <= 0: continue
        if u1 - u0 > 1e-6:
            # s <= f0 + (f1-f0)(u-u0)/(u1-u0)
            k = (f1 - f0) / (u1 - u0)
            poly = clip(poly, -k, 1, f0 - k * u0)
        if len(poly) < 3: continue
        A = np.array(poly)
        area = 0.5 * abs(np.dot(A[:, 0], np.roll(A[:, 1], 1)) - np.dot(A[:, 1], np.roll(A[:, 0], 1)))
        if area < 0.04: continue
        c = A.mean(0)
        # 링: 뒤(안쪽 3 cm), 모따기(안쪽 5 cm), 앞(안쪽 9 cm)
        def inset(d):
            v = A - c; l = np.linalg.norm(v, axis=1, keepdims=True)
            return c + v * np.maximum(l - d, l * 0.35) / np.maximum(l, 1e-9)
        depth = 0.09 + rng.uniform(0, 0.06)
        tilt = rng.normal(0, 0.03, 2)
        B0 = inset(0.03); B1 = inset(0.05); B2 = inset(0.10)
        r0 = [to3(p[0], p[1], 0.0) for p in B0]
        r1 = [to3(p[0], p[1], depth * 0.75 + tilt[0] * (p[0] - c[0]) + tilt[1] * (p[1] - c[1])) for p in B1]
        r2 = [to3(p[0], p[1], depth + tilt[0] * (p[0] - c[0]) + tilt[1] * (p[1] - c[1])) for p in B2]
        mat = int(rng.integers(0, 7))
        n = len(A)
        # 다각형 감김: (u,s) 에서 Fn 쪽으로 보이게 — u 오른쪽·s 아래 → 3D 에서 확인 후 맞춤
        tris = []
        for i in range(n):
            j = (i + 1) % n
            tris += [(r0[i], r0[j], r1[j]), (r0[i], r1[j], r1[i]), (r1[i], r1[j], r2[j]), (r1[i], r2[j], r2[i])]
        for i in range(1, n - 1):
            tris.append((r2[0], r2[i], r2[i + 1]))
        T = np.array(tris)
        cen = to3(c[0], c[1], depth * 0.5)
        fn = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
        # 앞면이 바깥(Fn)을 보게, 옆면은 돌 가운데에서 바깥을 보게
        mid = T.mean(1)
        out_dir = mid - cen + Fn * 0.02
        flip = (fn * out_dir).sum(1) < 0
        T[flip] = T[flip][:, [0, 2, 1]]
        out.setdefault('Ishigaki_Natural_Stone_%d' % (mat + 1), []).append(T)
        n_st += 1
    # 껍데기(줄눈 면): u 0.5 m 간격 띠, 위 s=0 → 아래 sb(u). 모서리는 맞댐선까지
    for (a, b) in pieces:
        ua = max(a, -CH * smax); ub = min(b, L + CH * smax)
        ug = np.unique(np.r_[np.arange(np.ceil(ua * 2) / 2, ub, 0.5), ua, ub])
        ug.sort()
        for i in range(len(ug) - 1):
            q = [ug[i], ug[i + 1]]
            def sbot(u):
                v = f(u)
                lim = max((-u) / CH if u < 0 else 0, (u - L) / CH if u > L else 0)
                return v, lim
            v0, l0 = sbot(q[0]); v1, l1 = sbot(q[1])
            if v0 <= max(l0, 0) and v1 <= max(l1, 0): continue
            p00 = to3(q[0], l0); p10 = to3(q[1], l1); p01 = to3(q[0], max(v0, l0)); p11 = to3(q[1], max(v1, l1))
            T = np.array([(p00, p01, p11), (p00, p11, p10)])
            fn = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
            flip = (fn * Fn).sum(1) < 0
            T[flip] = T[flip][:, [0, 2, 1]]
            shell.append(T)
        # 끝막음(경사로 쪽): 축대 면이 끊기는 곳에 수직 면
        for e in (a, b):
            if -1e8 < e < 1e8:
                v = f(e)
                if v <= 0: continue
                # 면 단면: 위 모서리(s=0) → 발치(s=v), 뒤로 1.5 m 수평
                top = to3(e, 0); bot = to3(e, v); back = top - Nv * 1.5; backb = np.array([back[0], bot[1], back[2]])
                ends.append((top, bot, backb, back, Uv * (1 if e == a else -1)))
    return n_st


def coping(segs_out):
    """윗면 갓돌: 가장자리에서 안쪽 1.6 m, 높이 TS-0.35 ~ TS+0.12, 1.1~1.5 m 마다 끊어 놓은 돌판(틈 1 cm)."""
    T = []
    for name, P0, U, N, L in SIDES:
        P0 = np.array([P0[0], 0, P0[1]], float); Uv = np.array([U[0], 0, U[1]], float); Nv = np.array([N[0], 0, N[1]], float)
        u = 0.0
        while u < L - 0.05:
            w = min(rng.uniform(1.1, 1.5), L - u)
            if name == 'N' and (P0[0] + u + w > RAMP[0] and P0[0] + u < RAMP[1]):
                if P0[0] + u < RAMP[0]: w = RAMP[0] - (P0[0] + u)
                else: u = RAMP[1] - P0[0]; continue
            a = u + 0.005; b = u + w - 0.005
            y1 = TS + 0.12 + rng.uniform(-0.01, 0.01); y0 = TS - 0.35
            corners = []
            for uu in (a, b):
                for dd in (0.02, -1.6):
                    corners.append(P0 + Uv * uu + Nv * dd)
            # 상자 8점
            V = [np.array([p[0], y, p[2]]) for y in (y0, y1) for p in corners]
            F = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
            cen = sum(V) / 8
            for fq in F:
                for t in ((fq[0], fq[1], fq[2]), (fq[0], fq[2], fq[3])):
                    tri = np.array([V[t[0]], V[t[1]], V[t[2]]])
                    fn = np.cross(tri[1] - tri[0], tri[2] - tri[0])
                    if np.dot(fn, tri.mean(0) - cen) < 0: tri = tri[[0, 2, 1]]
                    T.append(tri)
            u += w
    return np.array(T)


def tri_mesh(groups):
    """groups: {재질: [삼각형 배열 (n,3,3)]} → 정점 따로(면 법선), 삼면 투영 UV(×0.25)"""
    pos = []; nrm = []; uv = []; subs = []; mats = []; base = 0
    for m, arrs in groups.items():
        T = np.concatenate([np.asarray(a).reshape(-1, 3, 3) for a in arrs])
        fn = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]); fn /= np.maximum(np.linalg.norm(fn, axis=1, keepdims=True), 1e-12)
        P = T.reshape(-1, 3) - O
        Nn = np.repeat(fn, 3, axis=0)
        ax = np.abs(fn).argmax(1).repeat(3)
        Pw = T.reshape(-1, 3)
        U = np.where((ax == 0)[:, None], Pw[:, [2, 1]], np.where((ax == 1)[:, None], Pw[:, [0, 2]], Pw[:, [0, 1]])) * 0.25
        pos.append(P); nrm.append(Nn); uv.append(U)
        subs.append(np.arange(base, base + len(P), dtype=np.int32)); mats.append(m); base += len(P)
    return dict(pos=np.concatenate(pos).astype(np.float32), nrm=np.concatenate(nrm).astype(np.float32), uv=np.concatenate(uv).astype(np.float32), subs=subs, mats=mats)


def main():
    t0 = time.time()
    stones = {}; shell = []; ends = []; ns = {}
    for name, P0, U, N, L in SIDES:
        ns[name] = stones_for(name, P0, U, N, L, stones, shell, ends)
    # 끝막음 면(수직 사각형, 바깥을 봄)
    endT = []
    for top, bot, backb, back, out in ends:
        for tri in ((top, bot, backb), (top, backb, back)):
            tri = np.array(tri); fn = np.cross(tri[1] - tri[0], tri[2] - tri[0])
            if np.dot(fn, out) < 0: tri = tri[[0, 2, 1]]
            endT.append(tri)
    cop = coping(None)
    g0 = dict(sorted(stones.items()))
    g0['Ishigaki_Shadowed_Stone_Joints'] = shell + ([np.array(endT)] if endT else [])
    g0['Ishigaki_Worn_Step_And_Coping_Stone'] = [cop]
    L0 = tri_mesh(g0)
    n0 = sum(len(s) for s in L0['subs']) // 3
    print('stones', ns, 'LOD0 tris', n0, 'shell tris', sum(len(x) for x in shell), 'coping', len(cop), '(%.0f s)' % (time.time() - t0))
    np.savez(JT + 'shrine_wall_L0.npz', **{k: L0[k] for k in ('pos', 'nrm', 'uv')}, subs=np.array(L0['subs'], dtype=object), mats=np.array(L0['mats']))
    # 굽기용 껍데기(+갓돌): 위치는 O 기준
    shellT = np.concatenate([np.concatenate(shell)] + ([np.array(endT)] if endT else []) + [cop]) - O
    np.save(JT + 'shrine_wall_shellT.npy', shellT)
    json.dump(dict(O=O.tolist(), stones=ns, lod0_tris=n0), open(JT + 'shrine_wall.json', 'w'))


if __name__ == '__main__':
    main()
