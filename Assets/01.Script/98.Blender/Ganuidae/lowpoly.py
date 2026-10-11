"""저삼각형 모델용 도형 도우미(순수 파이썬 — bpy 없이도 모양을 만들 수 있음) + Blender FBX 내보내기.

좌표는 Unity 좌표(X 동, Y 위, Z 북). 면마다 '바깥 법선 힌트'를 적어 두면 내보낼 때 감김 방향을 맞춘다.
재질은 짧은 이름(PAL, ROOF, …)으로 적고, 내보낼 때 MATS 표로 Unity 재질 이름을 붙인다.
PAL = 단색 팔레트 재질(4×4 칸, 칸 가운데 UV) — 칸 이름은 PALETTE(그림 만드는 maketex.py 와 같은 순서).
"""
import math, json, os

PALETTE = ['post_red', 'beam_green', 'ridge_white', 'dark', 'soffit', 'wood', 'bronze', 'bronze_dark',
           'lacquer', 'stone', 'gold', 'gable']
MATS = {  # 짧은 이름 → Unity 재질 이름(서브메시 순서는 이 표 순서)
    'LAWN': 'MI_KHL_Ground', 'WALL': 'MAT_GYD_Wall', 'STEP': 'MAT_GYD_Step', 'FLOOR': 'MAT_GYD_Floor',
    'WATER': 'MI_KHL_Water', 'LOTUS': 'MAT_CRJ_Lotus', 'JBA': 'MAT_GYD_Jeongbangan', 'GYU': 'MAT_GYD_Gyu',
    'HONSANG': 'MAT_GYD_Honsang', 'PAL': 'MAT_PAV_Palette', 'ROOF': 'MAT_PAV_Roof', 'RAIL': 'MAT_PAV_Rail',
}
MAT_ORDER = list(MATS.keys())
TILE = {'WALL': 4.19, 'STEP': 0.94, 'FLOOR': 3.16, 'LAWN': 152.09, 'WATER': 30.0}   # 미터 ÷ 이 값 = UV


# ---------------- 벡터 ----------------
def V(x, y, z): return (float(x), float(y), float(z))
def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def length(a): return math.sqrt(dot(a, a))
def norm(a):
    l = length(a)
    return mul(a, 1.0 / l) if l > 1e-12 else (0.0, 0.0, 0.0)
def lerp(a, b, t): return add(a, mul(sub(b, a), t))
def rot_axis(v, k, ang):
    """v 를 단위축 k 둘레로 ang(라디안) 돌림(로드리게스)"""
    c, s = math.cos(ang), math.sin(ang)
    return add(add(mul(v, c), mul(cross(k, v), s)), mul(k, dot(k, v) * (1 - c)))
def perp_frame(a, ref=(1.0, 0.0, 0.0)):
    """a 에 수직인 단위 벡터 u, w (u 는 ref 를 a 에 수직으로 만든 것)"""
    a = norm(a)
    u = sub(ref, mul(a, dot(ref, a)))
    if length(u) < 1e-6:
        ref = (0.0, 1.0, 0.0) if abs(a[1]) < 0.9 else (0.0, 0.0, 1.0)
        u = sub(ref, mul(a, dot(ref, a)))
    u = norm(u)
    return u, cross(a, u)
def newell(pts):
    n = [0.0, 0.0, 0.0]
    for i in range(len(pts)):
        a, b = pts[i], pts[(i + 1) % len(pts)]
        n[0] += (a[1] - b[1]) * (a[2] + b[2]); n[1] += (a[2] - b[2]) * (a[0] + b[0]); n[2] += (a[0] - b[0]) * (a[1] + b[1])
    return norm(tuple(n))


def pal_uv(name):
    i = PALETTE.index(name)
    return ((i % 4 + 0.5) / 4.0, (i // 4 + 0.5) / 4.0)


def uv_box(pts, n, tile):
    ax = max(range(3), key=lambda i: abs(n[i]))
    if ax == 0:
        return [(p[2] / tile, p[1] / tile) for p in pts]
    if ax == 2:
        return [(p[0] / tile, p[1] / tile) for p in pts]
    return [(p[0] / tile, p[2] / tile) for p in pts]


class Obj:
    """한 오브젝트(= Unity 자식 하나). verts 는 공유 정점(매끈한 면용), 평면 면은 face() 가 정점을 따로 만든다."""

    def __init__(self, name):
        self.name = name
        self.verts = []
        self.faces = []      # (정점 번호들, 재질, uv 들, 법선 힌트, 매끈)

    def vert(self, p):
        self.verts.append(tuple(float(c) for c in p))
        return len(self.verts) - 1

    def face(self, pts, mat, uv=None, n=None, pal=None, smooth=False):
        pts = [tuple(p) for p in pts]
        # 연달아 같은 점 빼기
        q = []
        for p in pts:
            if not q or length(sub(p, q[-1])) > 1e-7:
                q.append(p)
        if len(q) > 2 and length(sub(q[0], q[-1])) < 1e-7:
            q.pop()
        if len(q) < 3:
            return
        if n is None:
            n = newell(q)
        if uv is None:
            if mat == 'PAL':
                uv = [pal_uv(pal)] * len(q)
            else:
                uv = uv_box(q, n, TILE.get(mat, 1.0))
        elif len(uv) != len(q):
            raise ValueError('uv count')
        idx = [self.vert(p) for p in q]
        self.faces.append((idx, mat, list(uv), tuple(n), smooth))

    def face_idx(self, idx, mat, uv, n, smooth=True):
        self.faces.append((list(idx), mat, list(uv), tuple(n), smooth))

    def tris(self):
        return sum(len(f[0]) - 2 for f in self.faces)

    def bounds(self):
        xs = [v[0] for v in self.verts]; ys = [v[1] for v in self.verts]; zs = [v[2] for v in self.verts]
        return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))


# ---------------- 도형 ----------------
def box(o, lo, hi, mat, pal=None, skip=(), uvfun=None):
    """축 정렬 상자. skip: 'bottom','top','n','s','e','w' 중 빼는 면"""
    x0, y0, z0 = lo; x1, y1, z1 = hi
    F = {
        'top': ([(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], (0, 1, 0)),
        'bottom': ([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, -1, 0)),
        'n': ([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], (0, 0, 1)),
        's': ([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], (0, 0, -1)),
        'e': ([(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)], (1, 0, 0)),
        'w': ([(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)], (-1, 0, 0)),
    }
    for k, (pts, n) in F.items():
        if k in skip:
            continue
        m = mat[k] if isinstance(mat, dict) else mat
        uv = uvfun(k, pts) if (uvfun and k in ('top',) and m not in ('PAL',)) else None
        o.face(pts, m, uv=uv, n=n, pal=pal)


def beam(o, p0, p1, w, h, pal, mat='PAL', up=(0.0, 1.0, 0.0), ends=True, bottom=True):
    """p0→p1 을 따라가는 네모 기둥(단면 w × h, 중심선 = p0→p1). up 은 단면 높이 방향 기준"""
    d = norm(sub(p1, p0))
    u = sub(up, mul(d, dot(up, d)))
    if length(u) < 1e-6:
        u = (1.0, 0.0, 0.0) if abs(d[0]) < 0.9 else (0.0, 0.0, 1.0)
        u = sub(u, mul(d, dot(u, d)))
    u = norm(u); s = cross(d, u)
    hw, hh = w / 2.0, h / 2.0
    def c(p, a, b): return add(add(p, mul(s, a)), mul(u, b))
    q0 = [c(p0, -hw, -hh), c(p0, hw, -hh), c(p0, hw, hh), c(p0, -hw, hh)]
    q1 = [c(p1, -hw, -hh), c(p1, hw, -hh), c(p1, hw, hh), c(p1, -hw, hh)]
    sides = [((0, 1), mul(u, -1)), ((1, 2), s), ((2, 3), u), ((3, 0), mul(s, -1))]
    for (i, j), n in sides:
        if not bottom and i == 0:
            continue
        o.face([q0[i], q0[j], q1[j], q1[i]], mat, n=n, pal=pal)
    if ends:
        o.face(q0, mat, n=mul(d, -1), pal=pal)
        o.face(q1, mat, n=d, pal=pal)


def column(o, p0, p1, r0, r1, n, pal, mat='PAL', cap0=False, cap1=True, ref=(1.0, 0.0, 0.0), smooth=True):
    """p0→p1 원기둥(n 각, 반지름 r0→r1). 옆면 매끈"""
    a = norm(sub(p1, p0)); u, w = perp_frame(a, ref)
    i0 = []; i1 = []
    for k in range(n):
        t = 2 * math.pi * (k + 0.5) / n
        dr = add(mul(u, math.cos(t)), mul(w, math.sin(t)))
        i0.append(o.vert(add(p0, mul(dr, r0)))); i1.append(o.vert(add(p1, mul(dr, r1))))
    uvp = pal_uv(pal) if mat == 'PAL' else (0.5, 0.5)
    for k in range(n):
        k2 = (k + 1) % n
        t = 2 * math.pi * (k + 1.0) / n
        dr = add(mul(u, math.cos(t)), mul(w, math.sin(t)))
        o.face_idx([i0[k], i0[k2], i1[k2], i1[k]], mat, [uvp] * 4, dr, smooth)
    if cap1:
        o.face([o.verts[i] for i in i1], mat, n=a, pal=pal)
    if cap0:
        o.face([o.verts[i] for i in i0], mat, n=mul(a, -1), pal=pal)


def ring(o, c, a, r, rw, aw, n, pal, ref=(1.0, 0.0, 0.0), mat='PAL'):
    """고리(環): 판(반지름 r-rw..r, 두께 없음 — 양면 재질) + 바깥 띠(너비 aw, 매끈). 삼각형 4n"""
    a = norm(a); u, w = perp_frame(a, ref)
    uvp = pal_uv(pal)
    dirs = [add(mul(u, math.cos(2 * math.pi * k / n)), mul(w, math.sin(2 * math.pi * k / n))) for k in range(n)]
    for k in range(n):
        k2 = (k + 1) % n
        o.face([add(c, mul(dirs[k], r - rw)), add(c, mul(dirs[k], r)), add(c, mul(dirs[k2], r)), add(c, mul(dirs[k2], r - rw))],
               mat, n=a, pal=pal)
    lo = [o.vert(add(add(c, mul(d, r)), mul(a, -aw / 2))) for d in dirs]
    hi = [o.vert(add(add(c, mul(d, r)), mul(a, aw / 2))) for d in dirs]
    for k in range(n):
        k2 = (k + 1) % n
        mid = norm(add(dirs[k], dirs[k2]))
        o.face_idx([lo[k], lo[k2], hi[k2], hi[k]], mat, [uvp] * 4, mid, True)


def sphere(o, c, r, axis, nlon, nlat, mat, ref=(1.0, 0.0, 0.0)):
    """UV 구(축 = axis 방향이 북극). uv: u = 경도/2π, v = 0(남극)~1(북극). 매끈"""
    a = norm(axis); u, w = perp_frame(a, ref)
    grid = []
    for j in range(1, nlat):
        la = -math.pi / 2 + math.pi * j / nlat
        row = []
        for i in range(nlon + 1):
            lo = 2 * math.pi * i / nlon
            d = add(mul(a, math.sin(la)), mul(add(mul(u, math.cos(lo)), mul(w, math.sin(lo))), math.cos(la)))
            row.append(o.vert(add(c, mul(d, r))))
        grid.append(row)
    def uvof(i, j): return (i / float(nlon), j / float(nlat))
    for j in range(len(grid) - 1):
        for i in range(nlon):
            idx = [grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]]
            nn = norm(sub(o.verts[idx[0]], c))
            o.face_idx(idx, mat, [uvof(i, j + 1), uvof(i + 1, j + 1), uvof(i + 1, j + 2), uvof(i, j + 2)], nn, True)
    sp = o.vert(add(c, mul(a, -r))); npo = o.vert(add(c, mul(a, r)))
    top = len(grid) - 1
    for i in range(nlon):
        o.face_idx([sp, grid[0][i + 1], grid[0][i]], mat, [((i + 0.5) / nlon, 0.0), uvof(i + 1, 1), uvof(i, 1)], mul(a, -1), True)
        o.face_idx([npo, grid[top][i], grid[top][i + 1]], mat, [((i + 0.5) / nlon, 1.0), uvof(i, nlat - 1), uvof(i + 1, nlat - 1)], a, True)


def lathe(o, base, axis, profile, n, pal, mat='PAL', ref=(1.0, 0.0, 0.0)):
    """축 둘레로 돌린 모양. profile = [(높이, 반지름), …] 아래→위, 반지름 0 이면 뾰족"""
    a = norm(axis); u, w = perp_frame(a, ref)
    dirs = [add(mul(u, math.cos(2 * math.pi * (k + 0.5) / n)), mul(w, math.sin(2 * math.pi * (k + 0.5) / n))) for k in range(n)]
    uvp = pal_uv(pal)
    rings = []
    for (hgt, rad) in profile:
        cc = add(base, mul(a, hgt))
        if rad <= 1e-6:
            rings.append([o.vert(cc)])
        else:
            rings.append([o.vert(add(cc, mul(d, rad))) for d in dirs])
    for j in range(len(rings) - 1):
        A, B = rings[j], rings[j + 1]
        for k in range(n):
            k2 = (k + 1) % n
            nn = norm(add(dirs[k], dirs[k2]))
            if len(A) == 1:
                o.face_idx([A[0], B[k2], B[k]], mat, [uvp] * 3, nn, True)
            elif len(B) == 1:
                o.face_idx([A[k], A[k2], B[0]], mat, [uvp] * 3, nn, True)
            else:
                o.face_idx([A[k], A[k2], B[k2], B[k]], mat, [uvp] * 4, nn, True)
    if len(rings[0]) > 1:
        o.face([o.verts[i] for i in rings[0]], mat, n=mul(a, -1), pal=pal)


# ---------------- 지붕 ----------------
def eave_point(side, t, Ex, Ez, ye, lift, flare):
    """처마선 점. side: 's','e','n','w', t: -1..1(모서리→모서리). 모서리는 솟고(lift) 바깥으로 뻗음(flare)"""
    tt = t * t
    y = ye + lift * tt
    if side == 's':
        return (t * (Ex + flare * tt), y, -(Ez + flare * tt))
    if side == 'n':
        return (-t * (Ex + flare * tt), y, (Ez + flare * tt))
    if side == 'e':
        return ((Ex + flare * tt), y, t * (Ez + flare * tt))
    return (-(Ex + flare * tt), y, -t * (Ez + flare * tt))


def roof(o, c, Ex, Ez, ye, lift, flare, yr, Gx=0.0, Gz=0.0, thick=0.18, K=4, tile=1.08,
         ridge=True, finial=False):
    """팔작(Gx, Gz > 0) 또는 사모(Gx = Gz = 0) 지붕. c = 지붕 가운데 (x, 0, z) 기준점.
    처마선은 모서리로 갈수록 솟고(lift) 바깥으로 뻗음(flare). 팔작은 네 물매가 같도록 Ex - Gx = Ez - Gz 로 줄 것.
    윗면 ROOF(기와, u = 처마 방향, v = 물매 방향 미터 ÷ tile), 밑면 PAL soffit, 처마 옆 PAL dark, 합각 PAL gable,
    마루(용마루·내림마루·추녀마루) PAL ridge_white, 사모 꼭대기 절병통 PAL dark."""
    cx, cz = c[0], c[2]
    def P(p): return (p[0] + cx, p[1], p[2] + cz)
    pyramid = Gx <= 1e-6 and Gz <= 1e-6
    ts = [-1.0 + 2.0 * k / K for k in range(K + 1)]
    E = {s: [eave_point(s, t, Ex, Ez, ye, lift, flare) for t in ts] for s in 'senw'}
    # 처마선 방향: s 서→동, e 남→북, n 동→서, w 북→남 (위에서 볼 때 시계 반대)
    sl_z = (yr - ye) / Ez
    sl_x = (yr - ye) / Ex if pyramid else sl_z
    def tile_uv(pts, side):
        out = []
        for p in pts:
            if side == 's':
                u, v = p[0], (Ez + p[2]) * math.sqrt(1 + sl_z * sl_z)
            elif side == 'n':
                u, v = -p[0], (Ez - p[2]) * math.sqrt(1 + sl_z * sl_z)
            elif side == 'e':
                u, v = p[2], (Ex - p[0]) * math.sqrt(1 + sl_x * sl_x)
            else:
                u, v = -p[2], (Ex + p[0]) * math.sqrt(1 + sl_x * sl_x)
            out.append((u / tile, v / tile))
        return out
    tops = []   # (세모, 면)
    half = K // 2
    if pyramid:
        apex = (0.0, yr, 0.0)
        for s in 'senw':
            for k in range(K):
                tops.append(([apex, E[s][k], E[s][k + 1]], s))
    else:
        yg = yr - (yr - ye) * Gz / Ez
        Rw, Re = (-Gx, yr, 0.0), (Gx, yr, 0.0)
        Ges, Gen, Gws, Gwn = (Gx, yg, -Gz), (Gx, yg, Gz), (-Gx, yg, -Gz), (-Gx, yg, Gz)
        for s, (A, B, C2, D) in (('s', (Gws, Rw, Re, Ges)), ('n', (Gen, Re, Rw, Gwn))):
            e = E[s]; m = e[half]
            for k in range(half):
                tops.append(([A, e[k], e[k + 1]], s))
            for k in range(half, K):
                tops.append(([D, e[k], e[k + 1]], s))
            for tri in ([A, B, m], [B, C2, m], [C2, D, m]):
                tops.append((tri, s))
        for s, (G1, G2) in (('e', (Ges, Gen)), ('w', (Gwn, Gws))):
            e = E[s]
            for k in range(half):
                tops.append(([G1, e[k], e[k + 1]], s))
            for k in range(half, K):
                tops.append(([G2, e[k], e[k + 1]], s))
            tops.append(([G1, e[half], G2], s))
    upn = {'s': (0, 1, -1), 'n': (0, 1, 1), 'e': (1, 1, 0), 'w': (-1, 1, 0)}
    dn = (0.0, -thick, 0.0)
    for tri, s in tops:
        o.face([P(p) for p in tri], 'ROOF', uv=tile_uv(tri, s), n=upn[s])
        o.face([P(add(p, dn)) for p in tri], 'PAL', n=(0, -1, 0), pal='soffit')
    # 처마 옆(두께 띠)
    for s in 'senw':
        e = E[s]
        outn = {'s': (0, 0, -1), 'n': (0, 0, 1), 'e': (1, 0, 0), 'w': (-1, 0, 0)}[s]
        for k in range(K):
            a, b = e[k], e[k + 1]
            o.face([P(a), P(b), P(add(b, dn)), P(add(a, dn))], 'PAL', n=outn, pal='dark')
    if not pyramid:
        # 합각(세운 세모 벽) + 그 밑 두께 띠(밑면과 합각 사이 틈 막음)
        for sx in (1, -1):
            gx = Gx * sx
            o.face([P((gx, yg, -Gz)), P((gx, yg, Gz)), P((gx, yr, 0.0))], 'PAL', n=(sx, 0, 0), pal='gable')
            o.face([P((gx, yg, -Gz)), P((gx, yg, Gz)), P((gx, yg - thick, Gz)), P((gx, yg - thick, -Gz))], 'PAL', n=(sx, 0, 0), pal='dark')
    if ridge:
        if pyramid:
            for s in 'senw':
                cn = eave_point(s, 1.0, Ex, Ez, ye, lift, flare)
                beam(o, P((0.0, yr + 0.03, 0.0)), P(add(cn, (0, 0.05, 0))), 0.16, 0.14, 'ridge_white')
        else:
            beam(o, P((-Gx - 0.25, yr + 0.12, 0.0)), P((Gx + 0.25, yr + 0.12, 0.0)), 0.26, 0.34, 'ridge_white')
            for sx in (1, -1):
                for sz in (1, -1):
                    beam(o, P((Gx * sx, yr + 0.05, 0.0)), P((Gx * sx, yg + 0.06, Gz * sz)), 0.16, 0.16, 'ridge_white')
                    cn = (sx * (Ex + flare), ye + lift, sz * (Ez + flare))
                    beam(o, P((Gx * sx, yg + 0.06, Gz * sz)), P(add(cn, (0, 0.05, 0))), 0.16, 0.14, 'ridge_white')
    if finial and pyramid:
        lathe(o, P((0.0, yr - 0.02, 0.0)), (0, 1, 0), [(0, 0.15), (0.16, 0.21), (0.34, 0.14), (0.44, 0.07), (0.56, 0.0)], 6, 'dark')


def rounded_rect(cx, cz, ax, az, r, seg):
    """둥근 네모(시계 반대 방향, 위에서 볼 때 x 동·z 북). 모서리마다 seg 칸"""
    pts = []
    for (ccx, ccz, a0) in ((ax - r, az - r, 0.0), (-(ax - r), az - r, 90.0), (-(ax - r), -(az - r), 180.0), (ax - r, -(az - r), 270.0)):
        for k in range(seg + 1):
            a = math.radians(a0 + 90.0 * k / seg)
            pts.append((cx + ccx + r * math.cos(a), cz + ccz + r * math.sin(a)))
    return pts


# ---------------- 내보내기(Blender) ----------------
def to_b(p):
    return (-p[0], -p[2], p[1])


def export_fbx(objs, out_fbx, out_json=None):
    import bpy, bmesh
    from mathutils import Vector
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)
    info = {}
    made = []
    for o in objs:
        used = [m for m in MAT_ORDER if any(f[1] == m for f in o.faces)]
        mi = {m: i for i, m in enumerate(used)}
        me = bpy.data.meshes.new(o.name)
        bm = bmesh.new()
        uvl = bm.loops.layers.uv.new('UVMap')
        bv = [bm.verts.new(Vector(to_b(p))) for p in o.verts]
        bm.verts.ensure_lookup_table()
        skipped = 0
        for (idx, mat, uv, n, smooth) in o.faces:
            try:
                f = bm.faces.new([bv[i] for i in idx])
            except ValueError:
                skipped += 1
                continue
            f.material_index = mi[mat]; f.smooth = bool(smooth)
            for lp, (u, v) in zip(f.loops, uv):
                lp[uvl].uv = (u, v)
            f.normal_update()
            if f.normal.dot(Vector(to_b(n))) < 0:
                f.normal_flip()
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
        bm.faces.sort(key=lambda f: f.material_index); bm.faces.index_update()
        bm.to_mesh(me); bm.free()
        for m in used:
            mm = bpy.data.materials.get(MATS[m]) or bpy.data.materials.new(MATS[m])
            me.materials.append(mm)
        ob = bpy.data.objects.new(o.name, me)
        bpy.context.scene.collection.objects.link(ob)
        cnt = [0] * len(used)
        for p in me.polygons:
            cnt[p.material_index] += 1
        info[o.name] = {'materials': [MATS[m] for m in used], 'tris': len(me.polygons), 'per_material': cnt, 'skipped_faces': skipped}
        made.append(ob)
        print('OBJ %s tris %d %s skipped %d' % (o.name, len(me.polygons), dict(zip([MATS[m] for m in used], cnt)), skipped))
    bpy.ops.object.select_all(action='DESELECT')
    for ob in made:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = made[0]
    bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             mesh_smooth_type='FACE', use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False,
                             path_mode='STRIP', use_custom_props=False)
    if out_json:
        with open(out_json, 'w', encoding='utf-8') as fh:
            json.dump(info, fh, ensure_ascii=False, indent=1)
    print('EXPORTED', out_fbx, 'total tris', sum(v['tris'] for v in info.values()))
    return info
