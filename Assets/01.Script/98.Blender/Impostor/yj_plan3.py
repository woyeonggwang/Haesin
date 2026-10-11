"""육조거리 행랑 배치 3(2026-09-30 사용자 지시 "모서리 근정전 행각모서리나 흥례문 모서리 행각처럼 해주세요").

줄(길 한쪽, 줄 좌표 s = 길 따라, d = 길에서 안쪽) =
  [시작 모서리] BAY×5 GATE BAY×16 GATE BAY×15 GATE BAY×5 [끝 모서리]
문 자리는 yj_plan2 와 같음(관아 안쪽 위치 그대로). 모서리 = yj_corner.py 의 ㄱ자 조각(YJR_CORNER, 바깥 추녀·안쪽 골).
줄 끝 관아 두 채는 행각으로 둘러쌈(모든 행각은 BAY 앞(돌벽·창)이 바깥, 뒤(문)가 마당 — 위에서 보아 반시계로 돌면 바깥이 오른쪽):
  길가 쪽 줄 → 모서리(진짜) → 바깥 옆 행각 BAY×4(진짜 — 네거리·광화문 앞에서 보임) → 뒤 모서리(진짜)
  → 뒤 행각 BAY×14 + 안쪽 모서리 + 안쪽 옆 행각(길 쪽 끝은 줄 용마루 면 d 4.97 에서 자름) = 임포스터 껍데기 한 덩어리(YJR_RING_START/END).
조각 놓기 = (종류, 줄 기준 회전 θ, 줄 좌표 원점). Unity 회전 θ: 로컬 (x, z) → (x cosθ + z sinθ, -x sinθ + z cosθ).
결과: out/yj_row3.tsv(PlaceFill), out/YJR/YJR_Ring.bin(YJR_RING_START/END — 줄 좌표, 원점 = 표에 적은 자리), out/yj_plan3.json
"""
import os, json, math
import numpy as np
import yj_corner as C
import yj_row as R
from export import write_meshes

L = C.L_BAY; K = C.K; SIDE_END = C.SIDE_END
L_GATE = 22.083
ENDW_MIN = -45.6726
D_RIDGE = 4.97            # 줄 용마루 가운데(BAY z) — 안쪽 옆 행각을 여기서 자름
N_ST = 5                  # 길가 모서리 ~ 문 사이 BAY
N_SIDE = 4                # 바깥 옆 행각 BAY(진짜)
N_BACK = 14               # 뒤 행각 BAY(껍데기)
N1, N2 = 16, 15
WEST = dict(x=-2766.0, z0=3502.0, rot=270)
EAST = dict(x=-2724.0, z0=3844.0, rot=90)
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')
EPS = 0.05                # 껍데기 BAY 는 u ∈ [-EPS, L] 로 잘라 이음(틈 막기, 겹침 최소)


def rot2(theta, x, z):
    t = math.radians(theta); c, s = math.cos(t), math.sin(t)
    return x * c + z * s, -x * s + z * c


def compose(th0, o0, th1, o1):
    """부모(th0, o0) 안의 자식(th1, o1) → 부모의 부모 기준"""
    ox, oz = rot2(th0, *o1)
    return (th0 + th1) % 360, (o0[0] + ox, o0[1] + oz)


def layout():
    s_g = -ENDW_MIN                          # 첫 문 시작 45.6726
    s_start = s_g - N_ST * L                 # 첫 길가 BAY 시작
    T_x = s_start - SIDE_END
    mods = []                                # (종류, θ, (s, d))
    # 시작 끝: 길가 모서리(θ 90)
    mods.append(('YJR_CORNER', 90, (T_x, K)))
    for k in range(1, N_SIDE + 1):
        mods.append(('YJR_BAY', 90, (T_x, K + k * L)))
    A = T_x + K; B = K + N_SIDE * L + SIDE_END    # 뒤 모서리(θ 180), B = 뒤 처마 d
    mods.append(('YJR_CORNER', 180, (A, B)))
    # 길가 줄
    s = s_start
    for _ in range(N_ST):
        mods.append(('YJR_BAY', 0, (s, 0.0))); s += L
    gates = []
    for n in (N1, N2, None):
        mods.append(('YJR_GATE', 0, (s, 0.0))); gates.append(s); s += L_GATE
        for _ in range(n if n else N_ST):
            mods.append(('YJR_BAY', 0, (s, 0.0))); s += L
    s_e = s
    mods.append(('YJR_CORNER', 0, (s_e, 0.0)))
    for k in range(1, N_SIDE + 1):
        mods.append(('YJR_BAY', 270, (s_e + K, SIDE_END + (k - 1) * L)))
    A2 = s_e + K; B2 = SIDE_END + N_SIDE * L
    mods.append(('YJR_CORNER', 270, (A2, B2)))
    geo = dict(s_start=s_start, T_x=T_x, A=A, d_back=B, s_e=s_e, A2=A2, B2=B2, gates=gates, s_end=s_e + K)
    return mods, geo


def ring_parts(geo):
    """껍데기 조각 목록(종류 'BAY'/'CORNER', θ, 원점, 안쪽 옆 행각이면 True) — 줄 좌표"""
    A, B = geo['A'], geo['d_back']
    # 시작 관아: 안쪽 옆 행각(θ 270, 북쪽이 바깥) → 안쪽 모서리 → 뒤 행각(θ 180)
    A1 = A + N_BACK * L + SIDE_END; B1 = B - K
    st = [('CORNER', 270, (A1, B1), False)]
    for j in range(1, 8):
        o = (A1, B1 - j * L)
        if o[1] + L <= D_RIDGE:
            break
        st.append(('BAY', 270, o, True))
    for i in range(1, N_BACK + 1):
        st.append(('BAY',) + compose(270, (A1, B1), 270, (K, SIDE_END + (i - 1) * L)) + (False,))
    # 끝 관아: 뒤 행각(뒤 모서리 옆 날개에서 이어짐) → 안쪽 모서리(θ 180) → 안쪽 옆 행각(θ 90)
    A2, B2 = geo['A2'], geo['B2']
    en = []
    for i in range(1, N_BACK + 1):
        en.append(('BAY',) + compose(270, (A2, B2), 270, (K, SIDE_END + (i - 1) * L)) + (False,))
    A3 = A2 - SIDE_END - N_BACK * L; B3 = B2 + K
    en.append(('CORNER', 180, (A3, B3), False))
    for j in range(1, 8):
        th, o = compose(180, (A3, B3), 270, (K, SIDE_END + (j - 1) * L))
        if o[1] - L >= D_RIDGE or o[1] <= D_RIDGE:
            pass
        if B3 - SIDE_END - (j - 1) * L <= D_RIDGE:
            break
        en.append(('BAY', th, o, True))
    geo.update(A1=A1, B1=B1, A3=A3, B3=B3)
    return st, en


def place(m, theta, o):
    def fp(p):
        x, z = rot2(theta, p[:, 0], p[:, 2])
        return np.stack([x + o[0], p[:, 1], z + o[1]], 1)
    def fn(n):
        x, z = rot2(theta, n[:, 0], n[:, 2])
        return np.stack([x, n[:, 1], z], 1)
    return C.xform(m, fp, fn)


def build_ring(parts, hul, chul, origin):
    ms = []
    for kind, th, o, inner in parts:
        src = hul if kind == 'BAY' else chul
        m = place(src, th, (o[0] - origin[0], o[1] - origin[1]))
        if inner:
            m = C.clip_keep(m, D_RIDGE - m['pos'][:, 2])       # d >= 4.97 만
        ms.append(m)
    return C.compact(C.concat(ms))


if __name__ == '__main__':
    from impcx import OUTROOT
    mods, geo = layout()
    st, en = ring_parts(geo)
    hul = C.read_hnms(os.path.join(OUTROOT, 'YJ_A', 'YJ_A_Imp.bin'))['ROW_BAY_Imp']
    hul = C.xform(hul, lambda p: p - np.array([R.XG1, R.GROUND, R.ZF]), lambda n: n)
    hul = dict(hul, pos=np.asarray(hul['pos'], np.float64), subs=[np.asarray(s) for s in hul['subs']])
    hc = C.build_corner(hul)
    hc = dict(hc, pos=hc['pos'].astype(np.float64), subs=[np.asarray(s) for s in hc['subs']])
    hc = C.clip_keep(hc, -EPS - hc['pos'][:, 0])                # 줄 날개 앞머리 넘침 자름
    hb = C.clip_keep(hul, -EPS - hul['pos'][:, 0])
    hb = C.clip_keep(hb, hb['pos'][:, 0] - L)
    rs = build_ring(st, hb, hc, (0.0, 0.0))
    re_ = build_ring(en, hb, hc, (geo['s_e'], 0.0))
    for nm, m in (('START', rs), ('END', re_)):
        print('RING', nm, 'tris', C.tris(m), 'bounds', m['pos'].min(0).round(2), m['pos'].max(0).round(2))
    write_meshes(os.path.join(OUTROOT, 'YJR', 'YJR_Ring.bin'), [('YJR_RING_START', rs), ('YJR_RING_END', re_)])
    rows = []
    for side in (WEST, EAST):
        def W(o):
            x, z = rot2(side['rot'], o[0], o[1])
            return side['x'] + x, side['z0'] + z
        for k, th, o in mods:
            rows.append((k, *W(o), (side['rot'] + th) % 360))
        rows.append(('YJR_RING_START', *W((0.0, 0.0)), side['rot']))
        rows.append(('YJR_RING_END', *W((geo['s_e'], 0.0)), side['rot']))
    with open(os.path.join(OUT, 'yj_row3.tsv'), 'w', encoding='utf-8') as f:
        f.write('# 행랑·모서리·둘레 행각\tx\tz\t회전 — yj_plan3.py\n')
        for k, x, z, r in rows:
            f.write('%s\t%.4f\t%.4f\t%d\t1\t1\t1\n' % (k, x, z, int(round(r)) % 360))
    geo['n_modules'] = len(rows)
    json.dump(geo, open(os.path.join(OUT, 'yj_plan3.json'), 'w'), indent=1)
    from collections import Counter
    print(Counter(r[0] for r in rows)); print(json.dumps(geo))
