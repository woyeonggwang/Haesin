"""광화문 양옆 궁장(A: 동쪽 날개, B: 서쪽 날개)을 모듈 좌표로 읽는 공용 함수.
OBJ(문루 프리팹 루트 좌표, x 부호 반대) -> Unity 프리팹 좌표(x_u = -x_obj, 면 순서 뒤집음)."""
import os
import numpy as np
from objload import load_obj

# 입력 OBJ 폴더(광화문 단순화판 궁장 GHM_WallA1~B3.obj). 기본 = 현재 폴더.
# 원본: D:\\Blender\\Enviroment\\PalaceJoseon\\광화문\\LOD\\
SRC_DIR = os.environ.get('GHM_WALL_DIR', '.')

SLOPE = 0.008713  # 날개가 바깥으로 갈수록 z_obj 가 느는 기울기(몸통 면에서 잰 값)

def load_tris(name, mats=None):
    V, VT, VN, F = load_obj(os.path.join(SRC_DIR, name + '.obj'))
    out = []
    for f in F:
        if mats and f[0] not in mats:
            continue
        idx = [f[1], f[3], f[2]]  # 면 순서 뒤집음(OBJ -> Unity)
        P = np.array([V[i[0] - 1] for i in idx], float); P[:, 0] *= -1
        UV = np.array([VT[i[1] - 1] for i in idx], float)
        N = np.array([VN[i[2] - 1] for i in idx], float); N[:, 0] *= -1
        out.append((f[0], P, UV, N))
    return out

def wing_frame(side):
    """side 'A'(OBJ +x, 월드 동쪽) / 'B'(OBJ -x, 월드 서쪽). Unity 프리팹 좌표에서
    R0 = 중심선 위 기준점(|x_obj| 57.84, 바닥 y 1.49), a = 바깥 방향 단위벡터, c = 진행 방향 왼쪽(모듈 +Z)."""
    if side == 'A':
        # 중심선 z_obj = -29.645 + SLOPE (x_obj - 14.735), x_u = -x_obj
        xo = 57.84; zc = -29.645 + SLOPE * (xo - 14.735)
        R0 = np.array([-xo, 1.49, zc]); a = np.array([-1.0, 0.0, SLOPE])
    else:
        # 중심선 z_obj = -29.905 - SLOPE (|x_obj| - 14.735), x_obj 음수 -> x_u 양수
        xo = 57.84; zc = -29.905 - SLOPE * (xo - 14.735)
        R0 = np.array([xo, 1.49, zc]); a = np.array([1.0, 0.0, -SLOPE])
    a = a / np.linalg.norm(a)
    c = np.array([-a[2], 0.0, a[0]])
    return R0, a, c

def to_local(P, R0, a, c):
    d = P - R0
    return np.stack([d @ a, d[..., 1] if d.ndim == 1 else d[:, 1], d @ c], -1) if d.ndim == 1 else np.stack([d @ a, d[:, 1], d @ c], 1)
