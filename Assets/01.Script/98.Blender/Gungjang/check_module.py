import sys, os, struct, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from module_common import load_tris, wing_frame, to_local
from raster import render
import build_module as bm

OUT = sys.argv[1] if len(sys.argv) > 1 else '.'


def read_hnms(path):
    buf = open(path, 'rb').read(); off = 0
    def i32():
        nonlocal off
        v = struct.unpack_from('<i', buf, off)[0]; off += 4; return v
    assert i32() == 0x534D4E48
    n = i32(); out = {}
    for _ in range(n):
        ln = i32(); name = buf[off:off + ln].decode(); off += ln
        nv = i32(); ns = i32()
        pos = np.frombuffer(buf, '<f4', nv * 3, off).reshape(-1, 3); off += nv * 12
        nrm = np.frombuffer(buf, '<f4', nv * 3, off).reshape(-1, 3); off += nv * 12
        uv = np.frombuffer(buf, '<f4', nv * 2, off).reshape(-1, 2); off += nv * 8
        subs = []
        for _ in range(ns):
            lm = i32(); m = buf[off:off + lm].decode(); off += lm
            ni = i32(); idx = np.frombuffer(buf, '<i4', ni, off); off += ni * 4
            subs.append((m, idx))
        out[name] = dict(pos=pos, nrm=nrm, uv=uv, subs=subs)
    return out


def tris_of(d, shift=0.0, tag=None):
    res = []
    for m, idx in d['subs']:
        P = d['pos'][idx].reshape(-1, 3, 3).astype(float)
        P[:, :, 0] += shift
        for t in P:
            res.append((tag or m, t))
    return res


def wing_tris(xt, tag=None):
    R0, a, c = wing_frame('A')
    res = []
    for nm, mats in (('GHM_WallA3', None), ('GHM_WallA2', {'M_GHM_Ws02A'}), ('GHM_WallA1', {'M_GHM_Ws02A'})):
        for m, P, UV, N in load_tris(nm, mats):
            Lc = to_local(P, R0, a, c)
            poly = [(Lc[k].copy(), UV[k].copy(), N[k].copy()) for k in range(3)]
            poly = bm.clip_poly(poly, 0, xt, False)
            if len(poly) < 3:
                continue
            for t in bm.fan(poly):
                res.append((tag or m, np.array([t[0][0], t[1][0], t[2][0]])))
    return res


if __name__ == '__main__':
    ms = read_hnms('/home/claude/gungjang/out/meshes.bin')
    m0 = ms['Gungjang_Module_LOD0']; m1 = ms['Gungjang_Module_LOD1']; cap = ms['Gungjang_Cap']
    T0 = tris_of(m0)
    # 1) 부품 안 이음매(X = LU) 위에서·옆에서
    s = bm.LU
    render(T0, (0, -1, 0.0001), (1, 0, 0), (s, 4, 0), 6, px=1400, aspect=0.9, path=OUT + '/mod_seam_top.png')
    render(T0, (0.35, -0.45, 1), (0, 1, 0), (s, 3.5, -1), 7, px=1400, aspect=0.6, path=OUT + '/mod_seam_side.png')
    # 2) 날개와 잇는 자리: 날개(잘린 것) + 부품(X_t 만큼 밀기)
    W = wing_tris(bm.XT)
    J = W + tris_of(m0, bm.XT)
    render(J, (0.35, -0.45, 1), (0, 1, 0), (bm.XT, 3.0, -1), 9, px=1400, aspect=0.6, path=OUT + '/join_side.png')
    render(J, (0, -1, 0.0001), (1, 0, 0), (bm.XT, 4, 0), 7, px=1400, aspect=0.9, path=OUT + '/join_top.png')
    Wtag = wing_tris(bm.XT, 'WING')
    render(Wtag + tris_of(m0, bm.XT), (0.35, -0.45, 1), (0, 1, 0), (bm.XT, 3.0, -1), 9, px=1400, aspect=0.6, path=OUT + '/join_side_tag.png')
    # 3) 전체 부품 + LOD1 + 마구리
    render(T0, (0.2, -0.3, 1), (0, 1, 0), (bm.L / 2, 1.5, 0), 50, px=1600, aspect=0.25, path=OUT + '/mod_full.png')
    render(tris_of(m1) + tris_of(cap, 0.0), (-0.6, -0.3, 1), (0, 1, 0), (2, 1.5, 0), 12, px=1400, aspect=0.6, path=OUT + '/lod1_cap.png')
    print('done')
