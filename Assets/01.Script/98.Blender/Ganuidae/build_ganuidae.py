"""간의대(簡儀臺) 저삼각형 모델 — 백그라운드로 실행해 FBX 로 내보냄(열려 있는 작업 파일은 건드리지 않음, .blend 저장 안 함).

실행: <blender> --background --factory-startup --python build_ganuidae.py -- <출력.fbx>

크기(근거): 높이 31척·길이 47척·너비(깊이) 32척 — 위키백과 「간의대」 환산 약 6.3 × 9.7 × 6.6 m. 돌로 쌓고 돌난간을 두름(우리역사넷 『신편 한국사』).
확인 안 된 것(가정): 계단 위치·모양(남쪽 면에 붙여 동쪽으로 오르는 곧은 계단 + 소맷돌), 난간 모양(흥례문 영제교 돌난간 그림), 꼭대기 간의·정방안은 넣지 않음.

좌표: 아래 값은 Unity 좌표(X 동, Y 위, Z 북, 원점 = 대 가운데 땅). Blender 로는 (−X, −Z, Y) 로 옮겨 넣고,
FBX(앞 −Z, 위 Y, 변환 적용)로 내보내면 Unity 에서 다시 (X, Y, Z) 가 됨.
재질 칸 이름 = Unity 재질 이름(MAT_GYD_Wall 석축 · MAT_GYD_Step 계단돌 · MAT_GYD_Floor 윗면 전돌 · MAT_GYD_Rail 돌난간 판, 양면·알파) — 서브메시도 이 순서.
UV = 미터 ÷ 그림 한 장 크기(석축 4.19 m · 계단돌 0.94 m · 전돌 3.16 m — 신무문 원본 메시에서 잰 값), 난간 판은 u = 마디 수, v = 높이(1 m).
"""
import sys, math
import bpy, bmesh
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = argv[0] if argv else None

# ---- 치수 ----
L, D, H = 9.7, 6.6, 6.3          # 길이(동서) · 너비(남북) · 높이
BUR = 0.4                        # 땅속으로 묻는 깊이
X0, X1, Z0, Z1 = -L / 2, L / 2, -D / 2, D / 2
N = 24                           # 계단 단 수
R = H / N                        # 챌면 높이 0.2625
G = 0.34                         # 디딤면 너비
XL = X1 - 1.4                    # 계단참 시작(동쪽 끝 1.4 m 가 계단참)
ZP_OUT, ZP_IN = Z0 - 1.6, Z0 - 1.3   # 소맷돌(난간 벽) 바깥·안쪽 면
P = 0.55                         # 소맷돌 윗면이 디딤 모서리선보다 높은 만큼
RAIL_H, RAIL_M = 1.0, 1.833      # 돌난간 판 높이 · 한 마디(기둥 간격)
RAIL_IN = 0.12                   # 난간 판을 대 가장자리에서 들인 거리
TILE = {'Wall': 4.19, 'Step': 0.94, 'Floor': 3.16}
MATS = ['MAT_GYD_Wall', 'MAT_GYD_Step', 'MAT_GYD_Floor', 'MAT_GYD_Rail']
MI = {n.split('_')[-1]: i for i, n in enumerate(MATS)}

faces = []   # (unity 꼭짓점 목록, 재질, 바깥 법선(unity), uv 목록)

def uv_box(pts, n, tile):
    """상자 투영: 법선이 가장 큰 축을 빼고 두 축을 미터 ÷ tile 로"""
    ax = max(range(3), key=lambda i: abs(n[i]))
    if ax == 0:   return [(p[2] / tile, p[1] / tile) for p in pts]
    if ax == 2:   return [(p[0] / tile, p[1] / tile) for p in pts]
    return [(p[0] / tile, p[2] / tile) for p in pts]

def add(pts, mat, n, uv=None):
    if uv is None:
        uv = uv_box(pts, n, TILE[mat])
    faces.append((pts, MI[mat], n, uv))

# ---- 대 몸체(석축 네 면 + 윗면) ----
add([(X0, -BUR, Z1), (X1, -BUR, Z1), (X1, H, Z1), (X0, H, Z1)], 'Wall', (0, 0, 1))
add([(X0, -BUR, Z0), (X1, -BUR, Z0), (X1, H, Z0), (X0, H, Z0)], 'Wall', (0, 0, -1))
add([(X1, -BUR, Z0), (X1, -BUR, Z1), (X1, H, Z1), (X1, H, Z0)], 'Wall', (1, 0, 0))
add([(X0, -BUR, Z0), (X0, -BUR, Z1), (X0, H, Z1), (X0, H, Z0)], 'Wall', (-1, 0, 0))
add([(X0, H, Z0), (X1, H, Z0), (X1, H, Z1), (X0, H, Z1)], 'Floor', (0, 1, 0))

# ---- 계단(남쪽 면, 서→동으로 오름) ----
xk = [XL - (N - 1 - k) * G for k in range(N)]        # 챌면 x
for k in range(N):
    add([(xk[k], k * R, ZP_IN), (xk[k], k * R, Z0), (xk[k], (k + 1) * R, Z0), (xk[k], (k + 1) * R, ZP_IN)], 'Step', (-1, 0, 0))
    if k < N - 1:
        y = (k + 1) * R
        add([(xk[k], y, ZP_IN), (xk[k + 1], y, ZP_IN), (xk[k + 1], y, Z0), (xk[k], y, Z0)], 'Step', (0, 1, 0))
add([(XL, H, ZP_IN), (X1, H, ZP_IN), (X1, H, Z0), (XL, H, Z0)], 'Floor', (0, 1, 0))     # 계단참
slope = R / G
def yp(x):   # 소맷돌 윗선
    return (R + slope * (x - xk[0]) + P) if x <= XL else H + P
XB = xk[0] - 0.3
# 바깥 면(소맷돌 + 계단 몸체 옆)
add([(XB, -BUR, ZP_OUT), (X1, -BUR, ZP_OUT), (X1, H + P, ZP_OUT), (XL, H + P, ZP_OUT), (XB, yp(XB), ZP_OUT)], 'Wall', (0, 0, -1))
# 안쪽 면(계단 쪽, 아래 가장자리는 디딤면 아래로 숨김)
yl = lambda x: yp(x) - P - 0.35
add([(XB, yl(XB), ZP_IN), (XL, yl(XL), ZP_IN), (X1, H - 0.35, ZP_IN), (X1, H + P, ZP_IN), (XL, H + P, ZP_IN), (XB, yp(XB), ZP_IN)], 'Wall', (0, 0, 1))
# 소맷돌 윗면: 비탈(경사 길이로 u) + 계단참 위 평평한 부분
sl = math.hypot(XL - XB, yp(XL) - yp(XB))
t = TILE['Step']
add([(XB, yp(XB), ZP_OUT), (XL, H + P, ZP_OUT), (XL, H + P, ZP_IN), (XB, yp(XB), ZP_IN)], 'Step', (0, 1, 0),
    uv=[(0, ZP_OUT / t), (sl / t, ZP_OUT / t), (sl / t, ZP_IN / t), (0, ZP_IN / t)])
add([(XL, H + P, ZP_OUT), (X1, H + P, ZP_OUT), (X1, H + P, ZP_IN), (XL, H + P, ZP_IN)], 'Step', (0, 1, 0),
    uv=[(sl / t, ZP_OUT / t), ((sl + X1 - XL) / t, ZP_OUT / t), ((sl + X1 - XL) / t, ZP_IN / t), (sl / t, ZP_IN / t)])
# 소맷돌 서쪽 끝
add([(XB, -BUR, ZP_OUT), (XB, -BUR, ZP_IN), (XB, yp(XB), ZP_IN), (XB, yp(XB), ZP_OUT)], 'Wall', (-1, 0, 0))
# 계단 몸체 동쪽 끝(대 동쪽 면과 이어짐)
add([(X1, -BUR, ZP_OUT), (X1, -BUR, Z0), (X1, H, Z0), (X1, H, ZP_IN), (X1, H + P, ZP_IN), (X1, H + P, ZP_OUT)], 'Wall', (1, 0, 0))

# ---- 돌난간 판(양면 재질) ----
def rail(a, b):
    a = Vector(a); b = Vector(b); ln = (b - a).length
    n = max(1, round(ln / RAIL_M))
    pts = [(a.x, H, a.z), (b.x, H, b.z), (b.x, H + RAIL_H, b.z), (a.x, H + RAIL_H, a.z)]
    d = (b - a).normalized(); nn = (d.z, 0, -d.x)
    faces.append((pts, MI['Rail'], nn, [(0, 0), (n, 0), (n, 1), (0, 1)]))
ri = RAIL_IN
rail((X0 + ri, 0, Z1 - ri), (X1 - ri, 0, Z1 - ri))          # 북
rail((X0 + ri, 0, Z0 + ri), (XL, 0, Z0 + ri))                # 남(계단참 쪽은 열림)
rail((X1 - ri, 0, ZP_IN), (X1 - ri, 0, Z1 - ri))             # 동(계단참 동쪽 가장자리까지)
rail((X0 + ri, 0, Z0 + ri), (X0 + ri, 0, Z1 - ri))           # 서

# ---- Blender 메시 ----
def to_b(p):
    return Vector((-p[0], -p[2], p[1]))

for ob in list(bpy.data.objects):
    bpy.data.objects.remove(ob, do_unlink=True)
me = bpy.data.meshes.new('Ganuidae')
bm = bmesh.new()
uvl = bm.loops.layers.uv.new('UVMap')
for (pts, mi, n, uv) in faces:
    vs = [bm.verts.new(to_b(p)) for p in pts]
    f = bm.faces.new(vs)
    f.material_index = mi; f.smooth = False
    bm.normal_update()
    want = to_b(n)
    for lp, (u, v) in zip(f.loops, uv):
        lp[uvl].uv = (u, v)
    if f.normal.dot(want) < 0:
        f.normal_flip()
bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
# 면을 재질 칸 순서로 정렬 — Unity 는 FBX 재질을 처음 나온 순서로 서브메시를 만들어서, 이렇게 해야 서브메시 순서 = MATS 순서
bm.faces.sort(key=lambda f: f.material_index); bm.faces.index_update()
bm.to_mesh(me); bm.free()
for n in MATS:
    m = bpy.data.materials.get(n) or bpy.data.materials.new(n)
    me.materials.append(m)
ob = bpy.data.objects.new('Ganuidae', me)
bpy.context.scene.collection.objects.link(ob)
tris = len(me.polygons)
cnt = [0] * len(MATS)
for p in me.polygons: cnt[p.material_index] += 1
xs = [(-v.co.x, v.co.z, -v.co.y) for v in me.vertices]
lo = [min(c[i] for c in xs) for i in range(3)]; hi = [max(c[i] for c in xs) for i in range(3)]
print('GANUIDAE tris %d per material %s unity bounds %s %s' % (tris, dict(zip(MATS, cnt)), [round(v, 3) for v in lo], [round(v, 3) for v in hi]))
if OUT:
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             mesh_smooth_type='FACE', use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False,
                             path_mode='STRIP', use_custom_props=False)
    print('GANUIDAE exported', OUT)
