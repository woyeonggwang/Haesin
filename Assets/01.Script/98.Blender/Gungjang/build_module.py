"""광화문 동쪽 날개(A3) 평평한 구간에서 궁장 반복 부품을 만든다.

- 기와(Top01A): 좁은 기와줄 주기 p = 0.26816 m, 줄 가운데 c_k = 3.3456 + k p (날개 좌표 X).
  c_1 ~ c_60 (15.82 m) 을 평면으로 잘라 3번 이어 붙임 → 부품 길이 L = 3 x 59 p = 47.464 m.
  c_1, c_60 은 불규칙 조각(서까래 끝)을 가르지 않는 자리(검사함). 광화문 날개도 c_60 에서 자른다.
- 용마루(긴 면): u 를 X 에 비례하게 다시 매김(부품 끝에서 정수만큼 → 부품끼리 이어짐).
- 몸통(Ws03A)·기단(Ws02A) + 땅속 2줄: 새 사각형, u 는 부품 길이에 6 / 5 번 반복.
- LOD1: 몸통·기단 + 기와 윗면을 단면으로 밀어 낸 검은 판(+ 처마 밑면).
- 마구리(Cap): 벽 끝 단면(몸통·기단 + 검은 기와 단면).
좌표: 부품 X = 진행 방향, Y = 위(0 = 날개 기단 바닥, 월드 10.59), Z = 진행 방향 왼쪽.
결과: HNMS 형식 meshes.bin (PalaceLodBinImport.ImportMeshes 로 가져옴) + params.json
실행: GHM_WALL_DIR=<GHM_Wall*.obj 폴더> python build_module.py out/meshes.bin out/params.json
확인 그림: python check_module.py <그림 폴더> (raster.py 로 그림)
Unity: ImportMeshes(<meshes.bin 폴더>, "Assets/.../_Baked/Gungjang", 20, "", "meshes.bin", "Meshes", 10)
  → 궁장_Gungjang 의 PalaceWallPath 에 메시·재질 연결 → 컨텍스트 메뉴 "궁장 다시 깔기"
  광화문 날개 끝 자르기: PalaceWallPath.TrimGwanghwamunWings(<광화문 단순화판 프리팹>, <출력 폴더>, XT)
"""
import sys, os, struct, json, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from module_common import load_tris, wing_frame, to_local

P_S = 0.26816
O_S = 3.3456
C1 = O_S + P_S
C60 = O_S + 60 * P_S
LU = C60 - C1
NU = 3
L = NU * LU
XT = C60  # 날개 자르는 자리

ZF, ZB = -1.685, 1.690            # 몸통 앞·뒷면
Y_BASE_TOP, Y_BODY_MID, Y_BODY_TOP = 1.248, 2.928, 3.338
Y_SKIRT = -2.496
# 날개 X_t 에서의 u(측정) → 부품 X=0 의 u
U_BASE0, U_LOW0, U_UP0 = 6.5684, 2.3544, 2.3579
REP_BODY, REP_BASE = 6, 5


def v_base(y, row):  # row 0 = 기단, 1·2 = 땅속
    return 0.1158 * (y + row * Y_BASE_TOP) + 0.2230


def v_low(y):
    return 0.1133 * y + 0.2291


def v_up(y):
    return 0.0905 * y + 0.6916


# ---------------------------------------------------------------- 자르기
def clip_poly(verts, axis, value, keep_ge):
    """verts: list of (pos(3), uv(2), nrm(3)) ; 평면 pos[axis] = value 로 자름"""
    out = []
    n = len(verts)
    for i in range(n):
        a = verts[i]; b = verts[(i + 1) % n]
        da = a[0][axis] - value; db = b[0][axis] - value
        if not keep_ge:
            da, db = -da, -db
        ina, inb = da >= 0, db >= 0
        if ina:
            out.append(a)
        if ina != inb:
            t = da / (da - db)
            p = a[0] + t * (b[0] - a[0]); u = a[1] + t * (b[1] - a[1]); nn = a[2] + t * (b[2] - a[2])
            nn = nn / (np.linalg.norm(nn) + 1e-12)
            p = p.copy(); p[axis] = value
            out.append((p, u, nn))
    return out


def fan(poly):
    return [(poly[0], poly[i], poly[i + 1]) for i in range(1, len(poly) - 1)]


class Builder:
    def __init__(self, mats):
        self.mats = mats
        self.tris = {m: [] for m in mats}

    def add_tri(self, mat, a, b, c):
        self.tris[mat].append((a, b, c))

    def add_quad(self, mat, p00, p10, p11, p01, uv00, uv10, uv11, uv01, nrm):
        """사각형(p00→p10→p11→p01). 앞면 = nrm 방향이 되도록 감는 순서를 맞춤"""
        nrm = np.asarray(nrm, float)
        P = [np.asarray(p, float) for p in (p00, p10, p11, p01)]
        U = [np.asarray(u, float) for u in (uv00, uv10, uv11, uv01)]
        V = [(P[i], U[i], nrm) for i in range(4)]
        t1, t2 = (V[0], V[1], V[2]), (V[0], V[2], V[3])
        for t in (t1, t2):
            cr = np.cross(t[1][0] - t[0][0], t[2][0] - t[0][0])
            if np.dot(cr, nrm) < 0:
                t = (t[0], t[2], t[1])
            self.add_tri(mat, *t)

    def mesh(self, name):
        pos, nrm, uv, subs = [], [], [], []
        index = {}
        for m in self.mats:
            idx = []
            for tri in self.tris[m]:
                for (p, u, n) in tri:
                    key = (round(p[0], 5), round(p[1], 5), round(p[2], 5), round(u[0], 5), round(u[1], 5), round(n[0], 3), round(n[1], 3), round(n[2], 3))
                    j = index.get(key)
                    if j is None:
                        j = len(pos); index[key] = j
                        pos.append(p); nrm.append(n); uv.append(u)
                    idx.append(j)
            subs.append((m, np.array(idx, np.int32)))
        return dict(name=name, pos=np.array(pos, np.float32), nrm=np.array(nrm, np.float32), uv=np.array(uv, np.float32), subs=subs)


def body_quads(b, x0, x1, u_of_x=True):
    """몸통·기단·땅속 사각형(앞·뒷면)"""
    def uu(u0, rep, x):
        return u0 + rep * x / L
    bands = [
        ('M_GHM_Ws02A', Y_SKIRT, -Y_BASE_TOP, lambda y: v_base(y, 2), U_BASE0, REP_BASE),
        ('M_GHM_Ws02A', -Y_BASE_TOP, 0.0, lambda y: v_base(y, 1), U_BASE0, REP_BASE),
        ('M_GHM_Ws02A', 0.0, Y_BASE_TOP, lambda y: v_base(y, 0), U_BASE0, REP_BASE),
        ('M_GHM_Ws03A', Y_BASE_TOP, Y_BODY_MID, v_low, U_LOW0, REP_BODY),
        ('M_GHM_Ws03A', Y_BODY_MID, Y_BODY_TOP, v_up, U_UP0, REP_BODY),
    ]
    for mat, y0, y1, vf, u0, rep in bands:
        for z, nz in ((ZF, -1.0), (ZB, 1.0)):
            b.add_quad(mat, (x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z),
                       (uu(u0, rep, x0), vf(y0)), (uu(u0, rep, x1), vf(y0)), (uu(u0, rep, x1), vf(y1)), (uu(u0, rep, x0), vf(y1)),
                       (0.0, 0.0, nz))


# 기와 윗면 단면(대칭, 측정한 윗선을 줄인 것). (Z, Y)
PROFILE = [(-2.34, 3.14), (-2.34, 3.44), (-2.26, 3.53), (-0.24, 4.40), (-0.14, 4.58), (0.0, 4.70),
           (0.14, 4.58), (0.24, 4.40), (2.26, 3.53), (2.34, 3.44), (2.34, 3.14)]
Y_SOFFIT = 3.14


def plate(b, x0, x1, mat):
    """단면을 X 로 밀어 낸 판(윗면·처마 앞면) + 처마 밑면"""
    for i in range(len(PROFILE) - 1):
        (z0, y0), (z1, y1) = PROFILE[i], PROFILE[i + 1]
        d = np.array([0.0, y1 - y0, z1 - z0]); d /= np.linalg.norm(d)
        # 바깥(위·옆)을 보는 법선: 진행 방향(0, dy, dz) 을 X 축 둘레로 돌림
        nrm = np.array([0.0, -d[2], d[1]])
        if i == 0 or i == len(PROFILE) - 2:
            nrm = np.array([0.0, 0.0, -1.0 if z0 < 0 else 1.0])
        elif nrm[1] < 0:
            nrm = -nrm
        b.add_quad(mat, (x0, y0, z0), (x1, y0, z0), (x1, y1, z1), (x0, y1, z1),
                   (x0 / 4, i / 10), (x1 / 4, i / 10), (x1 / 4, (i + 1) / 10), (x0 / 4, (i + 1) / 10), nrm)
    for zo, zi in ((-2.34, ZF), (2.34, ZB)):
        b.add_quad(mat, (x0, Y_SOFFIT, zo), (x1, Y_SOFFIT, zo), (x1, Y_SOFFIT, zi), (x0, Y_SOFFIT, zi),
                   (x0 / 4, 0), (x1 / 4, 0), (x1 / 4, 0.1), (x0 / 4, 0.1), (0.0, -1.0, 0.0))


def main(out_path, params_path):
    R0, a, c = wing_frame('A')
    src = load_tris('GHM_WallA3', {'M_GHM_Top01A'})
    tiles = []
    for m, P, UV, N in src:
        Lc = to_local(P, R0, a, c)
        Nl = np.stack([N @ a, N[:, 1], N @ c], 1)
        Nl /= np.linalg.norm(Nl, axis=1, keepdims=True)
        tiles.append((Lc, UV.copy(), Nl))

    # 용마루 긴 면: u = r X + b (삼각형마다) → 새 비율
    ridge_new = {}
    for i, (Lc, UV, Nl) in enumerate(tiles):
        if Lc[:, 0].max() - Lc[:, 0].min() > 10 and Lc[:, 1].min() > 4.2:
            A = np.c_[Lc[:, 0], np.ones(3)]
            r, b0 = np.linalg.lstsq(A, UV[:, 0], rcond=None)[0]
            ut = r * XT + b0
            rn = round(r * L) / L
            ridge_new[i] = (ut, rn, r)

    lod0 = Builder(['M_GHM_Ws02A', 'M_GHM_Ws03A', 'M_GHM_Top01A'])
    n_src = 0
    for u in range(NU):
        shift = u * LU - C1
        for i, (Lc, UV, Nl) in enumerate(tiles):
            if Lc[:, 0].max() <= C1 or Lc[:, 0].min() >= C60:
                continue
            poly = [(Lc[k].copy(), UV[k].copy(), Nl[k].copy()) for k in range(3)]
            poly = clip_poly(poly, 0, C1, True)
            if len(poly) >= 3:
                poly = clip_poly(poly, 0, C60, False)
            if len(poly) < 3:
                continue
            out = []
            for (p, uv, nn) in poly:
                p = p.copy(); p[0] += shift
                uv = uv.copy()
                if i in ridge_new:
                    ut, rn, r = ridge_new[i]
                    uv[0] = ut + rn * p[0]
                out.append((p, uv, nn))
            for t in fan(out):
                cr = np.cross(t[1][0] - t[0][0], t[2][0] - t[0][0])
                if np.linalg.norm(cr) < 1e-10:
                    continue
                lod0.add_tri('M_GHM_Top01A', *t)
            n_src += 1
    body_quads(lod0, 0.0, L)
    m0 = lod0.mesh('Gungjang_Module_LOD0')

    lod1 = Builder(['M_GHM_Ws02A', 'M_GHM_Ws03A', 'MAT_RoofPlate_Black'])
    body_quads(lod1, 0.0, L)
    plate(lod1, 0.0, L, 'MAT_RoofPlate_Black')
    m1 = lod1.mesh('Gungjang_Module_LOD1')

    # 마구리: X = 0 에서 -X 를 봄
    cap = Builder(['M_GHM_Ws02A', 'M_GHM_Ws03A', 'MAT_RoofPlate_Black'])
    nx = (-1.0, 0.0, 0.0)
    bands = [('M_GHM_Ws02A', Y_SKIRT, -Y_BASE_TOP, lambda y: v_base(y, 2), 0.10495),
             ('M_GHM_Ws02A', -Y_BASE_TOP, 0.0, lambda y: v_base(y, 1), 0.10495),
             ('M_GHM_Ws02A', 0.0, Y_BASE_TOP, lambda y: v_base(y, 0), 0.10495),
             ('M_GHM_Ws03A', Y_BASE_TOP, Y_BODY_MID, v_low, 0.12116),
             ('M_GHM_Ws03A', Y_BODY_MID, Y_SOFFIT, v_up, 0.12134)]
    for mat, y0, y1, vf, ur in bands:
        cap.add_quad(mat, (0, y0, ZF), (0, y0, ZB), (0, y1, ZB), (0, y1, ZF),
                     (ur * ZF, vf(y0)), (ur * ZB, vf(y0)), (ur * ZB, vf(y1)), (ur * ZF, vf(y1)), nx)
    ctr = np.array([0.0, 3.6, 0.0])
    ring = [np.array([0.0, y, z]) for (z, y) in PROFILE] + [np.array([0.0, Y_SOFFIT, -2.34])]
    for i in range(len(ring) - 1):
        a_, b_ = ring[i], ring[i + 1]
        if np.linalg.norm(a_ - b_) < 1e-6:
            continue
        t = [(ctr, np.array([0.5, 0.5]), np.array(nx)), (a_, np.array([0.0, 0.0]), np.array(nx)), (b_, np.array([1.0, 0.0]), np.array(nx))]
        cr = np.cross(t[1][0] - t[0][0], t[2][0] - t[0][0])
        if np.dot(cr, nx) < 0:
            t = [t[0], t[2], t[1]]
        cap.add_tri('MAT_RoofPlate_Black', *t)
    mc = cap.mesh('Gungjang_Cap')

    meshes = [m0, m1, mc]
    with open(out_path, 'wb') as f:
        f.write(struct.pack('<ii', 0x534D4E48, len(meshes)))
        for d in meshes:
            nb = d['name'].encode('utf-8')
            f.write(struct.pack('<i', len(nb))); f.write(nb)
            f.write(struct.pack('<ii', len(d['pos']), len(d['subs'])))
            f.write(np.ascontiguousarray(d['pos'], '<f4').tobytes())
            f.write(np.ascontiguousarray(d['nrm'], '<f4').tobytes())
            f.write(np.ascontiguousarray(d['uv'], '<f4').tobytes())
            for mname, idx in d['subs']:
                mb = mname.encode('utf-8')
                f.write(struct.pack('<i', len(mb))); f.write(mb)
                f.write(struct.pack('<i', len(idx)))
                f.write(np.ascontiguousarray(idx, '<i4').tobytes())
    stats = {d['name']: dict(verts=int(len(d['pos'])), tris={m: int(len(i) // 3) for m, i in d['subs']},
                             bmin=d['pos'].min(0).round(4).tolist(), bmax=d['pos'].max(0).round(4).tolist()) for d in meshes}
    params = dict(P_S=P_S, O_S=O_S, C1=C1, C60=C60, LU=LU, L=L, XT=XT, ridge_rates={str(k): [float(v[2]), float(v[1])] for k, v in list(ridge_new.items())[:4]},
                  stats=stats)
    json.dump(params, open(params_path, 'w'), indent=1, ensure_ascii=False)
    print(json.dumps(stats, indent=1))
    print('L', L, 'XT', XT, 'ridge tris', len(ridge_new))


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
