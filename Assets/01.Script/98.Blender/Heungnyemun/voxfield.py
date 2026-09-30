"""바깥 공기 복셀장: 장면(계단 뺌)을 복셀로 막고 위·옆 경계에서 공기를 채워 '바깥 공기'를 구한다."""
import sys, os, ctypes, time
import numpy as np
_HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, _HERE)
from scene import *
from load import load, xform
from rayeval import RMesh

VX = ctypes.CDLL('/tmp/libvoxel.so'); FL = ctypes.CDLL('/tmp/libflood.so')
VX.voxelize.restype = ctypes.c_longlong
VX.voxelize.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, ctypes.c_float, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_float, ctypes.c_void_p, ctypes.c_uint8]
VX.mark_points.argtypes = [ctypes.c_void_p, ctypes.c_longlong, ctypes.c_void_p, ctypes.c_float, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_void_p, ctypes.c_uint8]
FL.flood.restype = ctypes.c_longlong
FL.flood.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_void_p, ctypes.c_longlong]

STAIR = {'SM_HNM_Stair_A', 'SM_HNM_Handrail_C', 'SM_HNM_RailPillar_A', 'SM_HNM_StoneStair_A'}


class Field:
    def __init__(self, o, v, shape, g):
        self.o = np.asarray(o, np.float32); self.v = float(v); self.shape = shape; self.g = g   # shape = (NZ, NY, NX)

    def idx(self, P):
        P = np.asarray(P, np.float64)
        i = np.floor((P - self.o) / self.v).astype(np.int64)
        NZ, NY, NX = self.shape
        ok = (i[:, 0] >= 0) & (i[:, 1] >= 0) & (i[:, 2] >= 0) & (i[:, 0] < NX) & (i[:, 1] < NY) & (i[:, 2] < NZ)
        return i, ok

    def value(self, P):
        """칸 값(0 안쪽 공기, 1 막힘, 2 바깥 공기). 격자 밖 = 2(바깥)."""
        i, ok = self.idx(P)
        out = np.full(len(i), 2, np.uint8)
        g = self.g.reshape(self.shape)
        out[ok] = g[i[ok, 2], i[ok, 1], i[ok, 0]]
        return out


def clip_points(sc, tris, h):
    """알파 판 삼각형을 점으로 찍고 알파 0.5 이상만 돌려줌."""
    pts = []
    for t in tris:
        a, b, c = sc.P[sc.T[t]].astype(np.float64)
        ua, ub, uc = sc.U[sc.T[t]].astype(np.float64)
        me = max(np.linalg.norm(b - a), np.linalg.norm(c - b), np.linalg.norm(a - c))
        m = max(1, int(np.ceil(me / h)))
        I, J = np.meshgrid(np.arange(m + 1), np.arange(m + 1), indexing='ij')
        k = (I + J) <= m
        u = I[k] / m; w = J[k] / m; s = 1 - u - w
        P = s[:, None] * a + u[:, None] * b + w[:, None] * c
        UV = s[:, None] * ua + u[:, None] * ub + w[:, None] * uc
        al = albedo(sc.mats[sc.TM[t]], UV.astype(np.float32))[:, 3]
        pts.append(P[al >= 0.5])
    return np.concatenate(pts).astype(np.float32) if pts else np.zeros((0, 3), np.float32)


def build_field(sc, ground, v=0.1, pad=1.0, opaque_mats=(), log=print):
    t0 = time.time()
    mn = sc.P.min(0) - pad; mx = sc.P.max(0) + pad
    mn[1] = min(mn[1], ground[0][:, 1].min() - pad)
    NX, NY, NZ = (np.ceil((mx - mn) / v).astype(int) + 1)
    log('grid', NX, NY, NZ, 'voxels %.0fM' % (NX * NY * NZ / 1e6))
    g = np.zeros(NX * NY * NZ, np.uint8)
    o = mn.astype(np.float32)
    clipm = np.array([m['clip'] and m['name'] not in opaque_mats for m in sc.mats])
    tc = clipm[sc.TM]
    Tn = np.ascontiguousarray(sc.T[~tc], np.int32); P = np.ascontiguousarray(sc.P, np.float32)
    npts = VX.voxelize(P.ctypes.data, Tn.ctypes.data, len(Tn), o.ctypes.data, ctypes.c_float(v), NX, NY, NZ, ctypes.c_float(v * 0.5), g.ctypes.data, 1)
    log('voxelized opaque tris', len(Tn), 'points %.1fM' % (npts / 1e6), '%.1fs' % (time.time() - t0))
    cp = clip_points(sc, np.where(tc)[0], v * 0.5)
    VX.mark_points(cp.ctypes.data, len(cp), o.ctypes.data, ctypes.c_float(v), NX, NY, NZ, g.ctypes.data, 1)
    log('clip tris', int(tc.sum()), 'opaque points', len(cp))
    # 땅: 칸 기둥마다 아래로 쏴서 땅 높이 아래를 막음
    GP, GT = ground
    grm = RMesh(GP, GT, np.zeros((len(GP), 2), np.float32), np.zeros(len(GT), np.int32))
    xs = o[0] + (np.arange(NX) + 0.5) * v; zs = o[2] + (np.arange(NZ) + 0.5) * v
    X, Z = np.meshgrid(xs, zs)          # (NZ, NX)
    O = np.stack([X.ravel(), np.full(X.size, mx[1] + 10, np.float32), Z.ravel()], 1).astype(np.float32)
    D = np.tile(np.array([0, -1, 0], np.float32), (len(O), 1))
    t, tri, _, _ = grm.cast(O, D, 1e4); grm.free()
    gy = np.where(tri >= 0, mx[1] + 10 - t, -1e9).reshape(NZ, NX)
    G = g.reshape(NZ, NY, NX)
    ys = o[1] + (np.arange(NY) + 0.5) * v
    for yi in range(NY):
        below = gy >= ys[yi] - 0.5 * v
        G[:, yi, :][below] = 1
    log('ground cols with ground', int((gy > -1e8).sum()), '%.1fs' % (time.time() - t0))
    # 씨앗: 위 면 + 네 옆 면
    seeds = []
    ii = np.arange(NX * NY * NZ, dtype=np.int64).reshape(NZ, NY, NX)
    seeds.append(ii[:, NY - 1, :].ravel()); seeds.append(ii[0, :, :].ravel()); seeds.append(ii[NZ - 1, :, :].ravel())
    seeds.append(ii[:, :, 0].ravel()); seeds.append(ii[:, :, NX - 1].ravel())
    seeds = np.ascontiguousarray(np.concatenate(seeds), np.int64); del ii
    filled = FL.flood(g.ctypes.data, NX, NY, NZ, seeds.ctypes.data, len(seeds))
    log('flood filled %.1fM exterior air' % (filled / 1e6), 'solid %.1fM' % ((g == 1).sum() / 1e6), 'interior air %.1fM' % ((g == 0).sum() / 1e6), '%.1fs' % (time.time() - t0))
    return Field(o, v, (NZ, NY, NX), g)


if __name__ == '__main__':
    inst, mats, red = load()
    names = [red[i['k']]['name'].split('#')[0] for i in inst]
    sc = build_scene(inst, red, keep=lambda ii, i: names[ii] not in STAIR)
    gi = [ii for ii, n in enumerate(names) if n == 'SM_HNM_Ground']
    GP = []; GT = []; b = 0
    for ii in gi:
        d = red[inst[ii]['k']]; W = xform(inst[ii]['M'], d['pos'].astype(np.float64))
        GP.append(W); GT += [s.reshape(-1, 3) + b for s in d['subs']]; b += len(W)
    ground = (np.concatenate(GP).astype(np.float32), np.concatenate(GT).astype(np.int32))
    v = float(sys.argv[1]) if len(sys.argv) > 1 else 0.1
    F = build_field(sc, ground, v=v, opaque_mats=('CARD_HNM_149',))
    np.save('/home/claude/palace_simplify/hnm/field_g.npy', F.g)
    np.save('/home/claude/palace_simplify/hnm/field_meta.npy', np.array(list(F.o) + [F.v] + list(F.shape), np.float64))
    probes = {'gate 2F inside': (-1, 10, 0), 'gate 2F inside2': (3, 9.5, 1.5), 'gate attic': (0, 14.5, 0), 'gate passage': (0, 2, -2), 'gate passage S': (0, 2, 2),
              'HG west inside': (-59, 2.5, -40), 'HG west inside2': (-59.5, 3.5, -60), 'HG south W inside': (-40, 2.5, 0), 'YHM inside': (55, 3, -68),
              'GBC inside': (55, 2.5, -81), 'courtyard': (0, 2, -40), 'outside S': (0, 2, 20), 'HG roof attic': (-40, 6.5, 0)}
    for k, p in probes.items():
        print('%-18s %s' % (k, F.value(np.array([p], np.float32))[0]))
