"""취로정(翠露亭) 연못 + 정자 — 저삼각형.

실행(Blender 백그라운드): <blender> --background --factory-startup --python build_chwirojeong.py -- <출력.fbx> [<재질표.json>]
bpy 없이 실행하면 삼각형 수만 출력.

좌표 = 연못 홀더 기준 Unity 좌표(X 동, Y 위, Z 북, 원점 = 연못 가운데 땅 높이 — 월드 (-2745, 10.0, 4376)).
근거: 위키백과 「향원정」 — 세조 2년(1456) 후원에 취로정을 짓고 그 앞에 연못을 파 연꽃을 심었다는 기록이 『세조실록』에 있고, 그 연못이 지금 향원지 자리로 여겨짐.
향원지 = 모서리를 둥글게 한 네모 연못, 4,605 m². 『세조실록』 세조 5년(1459) 4월 22일 「경회루에 나아가 관사하고, 취로정에 나아가 관가하다」.
세조 때 연못의 모양·크기 기록은 확인 못 함 → 향원지 모양(모서리 둥근 네모)을 따르고 자리에 맞춰 52 × 48 m(약 2,450 m²)로 줄임(사용자 선택).
정자는 북쪽 둑의 사방이 트인 1칸 사모정(사용자 선택) — 크기·지붕·난간·계단은 가정.
오브젝트: Chwirojeong_Pond(잔디 둔덕 + 둘레돌 + 안쪽 석축, 충돌체용) · Chwirojeong_Water · Chwirojeong_Lotus · Chwirojeong_Pavilion.
"""
import sys, os, math
_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _here)
sys.path.insert(0, os.path.join(os.path.dirname(_here), 'Ganuidae'))
import lowpoly as L

A, B, R, SEG = 26.0, 24.0, 7.0, 6        # 연못 반너비(동서)·반깊이(남북)·모서리 반지름
COPE_W, Y_COPE = 0.5, 0.32               # 둘레돌 너비·윗면 높이
Y_APRON_IN, Y_APRON_OUT = 0.18, 0.06     # 잔디 둔덕 안쪽(둘레돌 옆)·바깥 끝 높이
Y_WALL_BOT, Y_WATER = -0.72, -0.40       # 안쪽 석축 밑(물 아래로 숨김)·물 높이(월드 9.60)
APRON = (0.0, 2.75, 33.0, 33.25, 10.0)   # 잔디 바깥 둥근 네모(가운데 x, z, 반너비, 반깊이, 모서리) — 남쪽 끝 z -30.5
PAV_C = (0.0, 0.0, 28.0)                 # 정자 가운데
LOTUS = [(-13.0, -12.0, 10.0, 20), (12.0, -14.0, 9.0, 110), (17.0, 8.0, 9.5, 200), (-17.0, 10.0, 9.0, 300),
         (-2.0, 14.0, 7.0, 45), (6.0, -2.0, 7.0, 160), (-5.0, -16.0, 6.0, 250), (20.0, -4.0, 6.0, 80)]   # (x, z, 판 크기, 돌림°) — 잎은 판 가운데 지름 약 0.88 배 안


def ring_pts(rr, y):
    return [(x, y, z) for (x, z) in rr]


def arclen(pts):
    s = [0.0]
    for i in range(1, len(pts) + 1):
        a, b = pts[i - 1], pts[i % len(pts)]
        s.append(s[-1] + math.hypot(b[0] - a[0], b[2] - a[2]))
    return s


def outward(a, b):
    """위에서 볼 때 시계 반대 방향 둘레의 a→b 변 바깥 법선(수평)"""
    dx, dz = b[0] - a[0], b[2] - a[2]
    l = math.hypot(dx, dz)
    return (dz / l, 0.0, -dx / l)


def pond():
    o = L.Obj('Chwirojeong_Pond')
    inn = L.rounded_rect(0, 0, A, B, R, SEG)
    cop = L.rounded_rect(0, 0, A + COPE_W, B + COPE_W, R + COPE_W, SEG)
    apx, apz, aax, aaz, ar = APRON
    apr = L.rounded_rect(apx, apz, aax, aaz, ar, SEG)
    n = len(inn)
    I = ring_pts(inn, Y_COPE); C = ring_pts(cop, Y_COPE)
    s_in = arclen(I); s_c = arclen(C)
    t = L.TILE['STEP']; tw = L.TILE['WALL']; tl = L.TILE['LAWN']
    for i in range(n):
        j = (i + 1) % n
        # 둘레돌 윗면
        o.face([I[i], I[j], C[j], C[i]], 'STEP', n=(0, 1, 0),
               uv=[(s_in[i] / t, 0), (s_in[i + 1] / t, 0), (s_in[i + 1] / t, COPE_W / t), (s_in[i] / t, COPE_W / t)])
        # 둘레돌 바깥 옆(잔디 밑으로 들어감)
        on = outward(C[i], C[j])
        o.face([C[i], C[j], (C[j][0], 0.0, C[j][2]), (C[i][0], 0.0, C[i][2])], 'STEP', n=on,
               uv=[(s_c[i] / t, Y_COPE / t), (s_c[i + 1] / t, Y_COPE / t), (s_c[i + 1] / t, 0), (s_c[i] / t, 0)])
        # 안쪽 석축(연못 쪽을 봄)
        inn_n = L.mul(outward(I[i], I[j]), -1)
        o.face([I[i], I[j], (I[j][0], Y_WALL_BOT, I[j][2]), (I[i][0], Y_WALL_BOT, I[i][2])], 'WALL', n=inn_n,
               uv=[(s_in[i] / tw, Y_COPE / tw), (s_in[i + 1] / tw, Y_COPE / tw), (s_in[i + 1] / tw, Y_WALL_BOT / tw), (s_in[i] / tw, Y_WALL_BOT / tw)])
        # 잔디 둔덕(둘레돌 옆 → 바깥 끝)
        q = [(cop[i][0], Y_APRON_IN, cop[i][1]), (cop[j][0], Y_APRON_IN, cop[j][1]),
             (apr[j][0], Y_APRON_OUT, apr[j][1]), (apr[i][0], Y_APRON_OUT, apr[i][1])]
        o.face(q, 'LAWN', n=(0, 1, 0), uv=[(p[0] / tl, p[2] / tl) for p in q])
        # 잔디 바깥 끝 옆(땅속으로)
        a0, a1 = (apr[i][0], Y_APRON_OUT, apr[i][1]), (apr[j][0], Y_APRON_OUT, apr[j][1])
        q = [a0, a1, (a1[0], -0.3, a1[2]), (a0[0], -0.3, a0[2])]
        o.face(q, 'LAWN', n=outward(a0, a1), uv=[(math.hypot(p[0], p[2]) / tl, p[1] / tl) for p in q])
    return o


def water():
    o = L.Obj('Chwirojeong_Water')
    inn = L.rounded_rect(0, 0, A, B, R, SEG)
    o.face([(x, Y_WATER, z) for (x, z) in inn], 'WATER', n=(0, 1, 0))
    return o


def lotus():
    o = L.Obj('Chwirojeong_Lotus')
    y = Y_WATER + 0.012
    for (cx, cz, s, rot) in LOTUS:
        a = math.radians(rot); h = s / 2
        ux, uz = math.cos(a) * h, math.sin(a) * h
        vx, vz = -uz, ux
        pts = [(cx - ux - vx, y, cz - uz - vz), (cx + ux - vx, y, cz + uz - vz), (cx + ux + vx, y, cz + uz + vz), (cx - ux + vx, y, cz - uz + vz)]
        o.face(pts, 'LOTUS', n=(0, 1, 0), uv=[(0, 0), (1, 0), (1, 1), (0, 1)])
    return o


def pavilion():
    o = L.Obj('Chwirojeong_Pavilion')
    cx, _, cz = PAV_C
    # 기단(북쪽으로 조금 길게 — 계단 자리)
    L.box(o, (cx - 2.2, -0.3, cz - 2.2), (cx + 2.2, 0.55, cz + 2.6), 'STEP', skip=('bottom',))
    # 기둥 4
    ytop = 3.71
    xs = (cx - 1.5, cx + 1.5); zs = (cz - 1.5, cz + 1.5)
    for x in xs:
        for z in zs:
            L.beam(o, (x, 0.55, z), (x, ytop, z), 0.22, 0.22, 'post_red', ends=False)
    yc = ytop - 0.15
    for z in zs:
        L.beam(o, (xs[0] - 0.12, yc, z), (xs[1] + 0.12, yc, z), 0.16, 0.30, 'beam_green')
    for x in xs:
        L.beam(o, (x, yc, zs[0] - 0.12), (x, yc, zs[1] + 0.12), 0.16, 0.30, 'beam_green')
    # 마루(나무)
    L.box(o, (cx - 1.95, 1.0, cz - 1.95), (cx + 1.95, 1.15, cz + 1.95), 'PAL', pal='wood')
    # 난간(동·남·서, 북쪽은 오르는 곳) — 알파 판, u = 칸 수(1.2 m 안팎)
    y0, y1 = 1.15, 1.80
    def rail(a, b, nrm):
        ln = math.hypot(b[0] - a[0], b[2] - a[2]); k = max(1, round(ln / 1.2))
        o.face([(a[0], y0, a[2]), (b[0], y0, b[2]), (b[0], y1, b[2]), (a[0], y1, a[2])], 'RAIL', n=nrm,
               uv=[(0, 0), (k, 0), (k, 1), (0, 1)])
    e = 1.93
    rail((cx + e, 0, cz - 1.95), (cx + e, 0, cz + 1.95), (1, 0, 0))
    rail((cx - 1.95, 0, cz - e), (cx + 1.95, 0, cz - e), (0, 0, -1))
    rail((cx - e, 0, cz - 1.95), (cx - e, 0, cz + 1.95), (-1, 0, 0))
    # 북쪽 나무 계단 2단
    L.box(o, (cx - 0.6, 0.55, cz + 1.95), (cx + 0.6, 0.95, cz + 2.25), 'PAL', pal='wood', skip=('bottom',))
    L.box(o, (cx - 0.6, 0.55, cz + 2.25), (cx + 0.6, 0.75, cz + 2.55), 'PAL', pal='wood', skip=('bottom',))
    # 사모지붕 + 절병통
    L.roof(o, (cx, 0.0, cz), Ex=2.6, Ez=2.6, ye=3.115, lift=0.38, flare=0.3, yr=4.96, thick=0.18, K=4, finial=True)
    return o


def build():
    return [pond(), water(), lotus(), pavilion()]


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    objs = build()
    try:
        import bpy  # noqa
        out = argv[0]
        js = argv[1] if len(argv) > 1 else os.path.splitext(out)[0] + '_materials.json'
        L.export_fbx(objs, out, js)
    except ImportError:
        tot = 0
        for o in objs:
            lo, hi = o.bounds()
            print('%-22s tris %4d  bounds %s %s' % (o.name, o.tris(), tuple(round(v, 2) for v in lo), tuple(round(v, 2) for v in hi)))
            tot += o.tris()
        print('total', tot)
