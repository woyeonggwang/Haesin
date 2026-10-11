"""간의대 위·옆 천문 기구 — 간의, 정방안, 규표(동표·규), 소각(작은 집) + 혼의·혼상. 저삼각형.

실행(Blender 백그라운드): <blender> --background --factory-startup --python build_instruments.py -- <출력.fbx> [<재질표.json>]
bpy 없이 실행하면 삼각형 수만 출력하고 OBJ 미리보기를 씀(python3 build_instruments.py preview.obj).

좌표 = 간의대 홀더 기준 Unity 좌표(X 동, Y 위, Z 북, 원점 = 간의대 가운데 땅, 대 윗면 y 6.3).
근거: 『세종실록』 세종 19년(1437) 4월 15일 「주야 측후기인 일성정시의가 이룩되다」 — 후원 경회루 북쪽에 돌로 대(높이 31척·길이 47척·넓이 32척)를
쌓고 돌난간을 두르고 간의를 놓음, 정방안을 그 남쪽에 폄, 대 서쪽에 동표(높이 = 8척 얼의 5배 = 40척), 청석을 깎아 규를 만들고 면에 장·척·촌·분을 새김,
표 서쪽에 작은 집을 세워 혼의(동쪽)·혼상(서쪽)을 놓음, 혼상은 베에 칠을 해 몸통을 만들고 둘레 10척 8촌 6분.
확인 안 된 것(가정): 간의 크기·짜임(원나라 곽수경 간의의 일반 구조를 줄인 것), 규 길이(동지 그림자 약 14.9 m 가 들어가게 15.8 m),
소각 크기·지붕(팔작), 혼의 고리 구성·크기, 기구 받침 모양. 1척 = 0.206 m(주척 가정), 한양 위도 37.57°.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lowpoly as L
from lowpoly import V, add, sub, mul, norm, rot_axis

PHI = math.radians(37.57)
P_AX = (0.0, math.sin(PHI), math.cos(PHI))      # 천구 북극 방향(북쪽으로 위도만큼 들림)
XH = (1.0, 0.0, 0.0); UP = (0.0, 1.0, 0.0)
H_TOP = 6.3                                       # 간의대 윗면 높이
CHI = 0.206


def ganui():
    o = L.Obj('GYD_Ganui')
    y0 = H_TOP
    fb = y0 + 0.07
    # 받침틀(남북 긴 틀 2 + 가로 틀 4)
    for x in (-0.9, 0.9):
        L.beam(o, (x, fb, -1.5), (x, fb, 2.3), 0.14, 0.14, 'bronze_dark')
    for z in (-1.5, -0.9, 0.4, 2.3):
        L.beam(o, (-0.97, fb, z), (0.97, fb, z), 0.14, 0.14, 'bronze_dark', ends=False)
    # 극축: 남쪽 굴대 S → 북쪽 굴대 N (위도 기울기)
    S = (0.0, y0 + 0.85, -0.9)
    N = add(S, mul(P_AX, 3.0))
    L.beam(o, (0.0, y0 + 0.14, -0.9), S, 0.12, 0.12, 'bronze_dark', ends=False)          # 남쪽 받침 기둥
    L.beam(o, S, N, 0.045, 0.045, 'bronze_dark')                                          # 극축
    # 백각환(고정)·적도환(돌아감): 극축에 수직, 남쪽 끝
    Ec = add(S, mul(P_AX, 0.28))
    L.ring(o, Ec, P_AX, 0.95, 0.07, 0.05, 16, 'bronze')
    L.ring(o, add(Ec, mul(P_AX, 0.035)), P_AX, 0.84, 0.06, 0.045, 16, 'bronze')
    w = norm(L.cross(P_AX, XH))                      # 고리 평면 안 방향(위·남쪽)
    for sx in (1, -1):                                # 백각환 받침 다리
        d = add(mul(w, -math.cos(math.radians(55))), mul(XH, sx * math.sin(math.radians(55))))
        top = add(Ec, mul(d, 0.95))
        L.beam(o, (0.9 * sx, y0 + 0.14, top[2]), top, 0.07, 0.07, 'bronze_dark', ends=False)
    # 사유환(쌍고리, 극축을 품고 돎) + 규형(가늠대)
    M = add(S, mul(P_AX, 1.5))
    ns = rot_axis(XH, P_AX, math.radians(25))
    for s in (1, -1):
        L.ring(o, add(M, mul(ns, 0.05 * s)), ns, 0.78, 0.05, 0.04, 16, 'bronze', ref=P_AX)
    g = rot_axis(P_AX, ns, math.radians(70))
    L.beam(o, sub(M, mul(g, 0.76)), add(M, mul(g, 0.76)), 0.05, 0.05, 'bronze_dark')
    # 정극환(북극 고리) + 북쪽 받침(ㅅ자 기둥 + 가로대 + 매단 대)
    L.ring(o, N, P_AX, 0.2, 0.045, 0.04, 12, 'bronze')
    ytop = N[1] + 0.28
    for sx in (1, -1):
        L.beam(o, (0.9 * sx, y0 + 0.14, 2.0), (0.32 * sx, ytop, N[2]), 0.11, 0.11, 'bronze_dark', ends=False)
    L.beam(o, (-0.4, ytop, N[2]), (0.4, ytop, N[2]), 0.1, 0.1, 'bronze_dark')
    L.beam(o, (0.0, ytop, N[2]), N, 0.05, 0.05, 'bronze_dark', ends=False)
    return o


def jeongbangan():
    o = L.Obj('GYD_Jeongbangan')
    y0 = H_TOP
    x0, x1, z0, z1 = -0.6, 0.6, -2.9, -1.7
    L.box(o, (x0, y0 - 0.02, z0), (x1, y0 + 0.12, z1), {'top': 'JBA', 'n': 'STEP', 's': 'STEP', 'e': 'STEP', 'w': 'STEP', 'bottom': 'STEP'},
          skip=('bottom',), uvfun=lambda k, pts: [((p[0] - x0) / (x1 - x0), (p[2] - z0) / (z1 - z0)) for p in pts])
    return o


def gyupyo():
    o = L.Obj('GYD_Gyupyo')
    X = -13.0
    ZT = -5.95                    # 표 자리
    # 규(청석, 윗면 눈금 그림 — 10척 = 2.06 m 마다 되풀이)
    gx0, gx1, gz0, gz1 = X - 0.45, X + 0.45, -5.3, 10.5
    L.box(o, (gx0, -0.1, gz0), (gx1, 0.45, gz1), {'top': 'GYU', 'n': 'STEP', 's': 'STEP', 'e': 'STEP', 'w': 'STEP', 'bottom': 'STEP'},
          skip=('bottom', 's'), uvfun=lambda k, pts: [((p[0] - gx0) / 0.9, (p[2] - gz0) / (10 * CHI)) for p in pts])
    # 표 받침돌
    L.box(o, (X - 0.65, -0.1, ZT - 0.65), (X + 0.65, 0.6, ZT + 0.65), 'STEP', skip=('bottom',))
    # 동표(구리 기둥, 규 윗면에서 40척 = 8.24 m 에 횡량)
    yb = 0.45 + 40 * CHI
    L.column(o, (X, 0.6, ZT), (X, yb - 0.05, ZT), 0.2, 0.13, 8, 'bronze')
    L.column(o, (X - 0.8, yb, ZT), (X + 0.8, yb, ZT), 0.07, 0.07, 8, 'bronze', cap0=True, cap1=True)
    for sx in (1, -1):
        L.beam(o, (X, yb - 0.75, ZT), (X + 0.5 * sx, yb - 0.04, ZT), 0.06, 0.06, 'bronze_dark', ends=False)
    return o


SOGAK_C = (-24.0, 0.0, -5.5)


def sogak():
    o = L.Obj('GYD_Sogak')
    cx, _, cz = SOGAK_C
    yb = 0.45
    L.box(o, (cx - 3.2, -0.1, cz - 2.1), (cx + 3.2, yb, cz + 2.1), {'top': 'FLOOR', 'n': 'STEP', 's': 'STEP', 'e': 'STEP', 'w': 'STEP', 'bottom': 'STEP'},
          skip=('bottom',))
    L.box(o, (cx + 3.2, -0.1, cz - 0.6), (cx + 3.6, 0.22, cz + 0.6), 'STEP', skip=('bottom', 'w'))        # 동쪽 디딤돌
    xs = (cx - 2.6, cx, cx + 2.6); zs = (cz - 1.5, cz + 1.5)
    ytop = 3.3
    for x in xs:
        for z in zs:
            L.beam(o, (x, yb, z), (x, ytop, z), 0.22, 0.22, 'post_red', ends=False)
    yc = ytop - 0.15
    for z in zs:
        L.beam(o, (xs[0] - 0.12, yc, z), (xs[-1] + 0.12, yc, z), 0.16, 0.30, 'beam_green')
    for x in (xs[0], xs[-1]):
        L.beam(o, (x, yc, zs[0] - 0.12), (x, yc, zs[1] + 0.12), 0.16, 0.30, 'beam_green')
    # 팔작지붕: 처마 내밈 1.0, 네 물매 같게(Ex - Gx = Ez - Gz)
    L.roof(o, (cx, 0.0, cz), Ex=3.6, Ez=2.5, ye=2.87, lift=0.35, flare=0.25, yr=4.41, Gx=2.3, Gz=1.2, thick=0.18, K=4)
    return o


def honui():
    o = L.Obj('GYD_Honui')
    bx, bz = SOGAK_C[0] + 1.3, SOGAK_C[2]
    y0 = 0.45
    C = (bx, y0 + 1.15, bz)
    # 받침: 열십자 바닥틀 + 기둥 4
    L.beam(o, (bx - 0.68, y0 + 0.06, bz), (bx + 0.68, y0 + 0.06, bz), 0.1, 0.12, 'bronze_dark')
    L.beam(o, (bx, y0 + 0.06, bz - 0.68), (bx, y0 + 0.06, bz + 0.68), 0.1, 0.12, 'bronze_dark')
    for dx, dz in ((0.64, 0), (-0.64, 0), (0, 0.64), (0, -0.64)):
        L.beam(o, (bx + dx, y0 + 0.12, bz + dz), (bx + dx, C[1], bz + dz), 0.07, 0.07, 'bronze_dark', ends=False)
    L.ring(o, C, UP, 0.67, 0.07, 0.05, 16, 'bronze')                                  # 지평환
    L.ring(o, C, XH, 0.61, 0.05, 0.05, 16, 'bronze', ref=UP)                          # 자오환
    L.ring(o, C, P_AX, 0.55, 0.045, 0.04, 16, 'bronze')                               # 적도환
    ecl = rot_axis(rot_axis(P_AX, XH, math.radians(23.44)), P_AX, math.radians(70))
    L.ring(o, C, ecl, 0.545, 0.045, 0.04, 16, 'gold')                                 # 황도환
    L.ring(o, C, rot_axis(XH, P_AX, math.radians(40)), 0.47, 0.04, 0.035, 16, 'bronze_dark', ref=P_AX)   # 사유환
    L.beam(o, sub(C, mul(P_AX, 0.63)), add(C, mul(P_AX, 0.63)), 0.03, 0.03, 'bronze_dark')             # 극축
    return o


def honsang():
    o = L.Obj('GYD_Honsang')
    bx, bz = SOGAK_C[0] - 1.3, SOGAK_C[2]
    y0 = 0.45
    R = 10.86 * CHI / (2 * math.pi)            # 둘레 10척 8촌 6푼
    G = (bx, y0 + 0.95, bz)
    L.sphere(o, G, R, P_AX, 12, 8, 'HONSANG')
    L.ring(o, G, UP, R + 0.075, 0.06, 0.05, 16, 'bronze')                             # 지평
    L.ring(o, G, XH, R + 0.04, 0.035, 0.035, 16, 'bronze', ref=UP)                    # 자오
    rl = (R + 0.045) / math.sqrt(2)
    for sx in (1, -1):
        for sz in (1, -1):
            L.beam(o, (bx + rl * sx, y0 + 0.1, bz + rl * sz), (bx + rl * sx, G[1], bz + rl * sz), 0.06, 0.06, 'lacquer', ends=False)
    for sz in (1, -1):
        L.beam(o, (bx - rl - 0.05, y0 + 0.06, bz - (rl + 0.05) * sz), (bx + rl + 0.05, y0 + 0.06, bz + (rl + 0.05) * sz), 0.08, 0.1, 'lacquer')
    return o


def build():
    return [ganui(), jeongbangan(), gyupyo(), sogak(), honui(), honsang()]


def write_obj(objs, path):
    with open(path, 'w') as fh:
        base = 1
        for o in objs:
            fh.write('o %s\n' % o.name)
            for v in o.verts:
                fh.write('v %.4f %.4f %.4f\n' % (-v[0], v[1], v[2]))
            for (idx, mat, uv, n, sm) in o.faces:
                fh.write('f ' + ' '.join(str(base + i) for i in idx) + '\n')
            base += len(o.verts)


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
            print('%-18s tris %4d  bounds %s %s' % (o.name, o.tris(), tuple(round(v, 2) for v in lo), tuple(round(v, 2) for v in hi)))
            tot += o.tris()
        print('total', tot)
        if argv:
            write_obj(objs, argv[0])
