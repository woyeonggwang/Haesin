"""단지 단순화판(Unity 프리팹 LOD0)을 PalaceBinExport 로 내보낸 것(meshes.bin·instances.tsv·materials.tsv)과
텍스처 캐시(tex/index.tsv: 원래 경로 → 512²(판은 256²) PNG)를 읽어, 오브젝트(렌더러)별 삼각형·재질을 만든다.
좌표 = 단지 프리팹 루트 좌표(Unity). 거울 배치는 감김을 뒤집고 법선은 역전치로."""
import os, json
import numpy as np
from PIL import Image
import common
from pbsimp import read_bin
import load as L

# 단지 단순화판 내보내기 폴더(<단지>/meshes.bin …)와 텍스처 캐시(tex/index.tsv + PNG)
CXDATA = common.first_dir([r'D:\Unity\Haesin\Captures\impostor\src', os.path.join(os.path.dirname(os.path.abspath(__file__)), 'data'), '/home/claude/impostor/data'], 'IMPOSTOR_DATA')
TEXDIR = common.first_dir([os.path.join(CXDATA, 'tex'), os.path.join(os.path.dirname(CXDATA), 'tex')], 'IMPOSTOR_TEX')
_texcache = {}


def tex_rgba(png):
    """행 0 = v 0(아래) RGBA uint8"""
    if png not in _texcache:
        a = np.asarray(Image.open(png).convert('RGBA'))[::-1]
        _texcache[png] = np.ascontiguousarray(a)
    return _texcache[png]


def read_mat_rows(path):
    rows = {}
    for l in open(path, encoding='utf-8').read().splitlines():
        c = l.split('\t')
        if len(c) >= 9:
            rows[c[0]] = c
    return rows


def load_complex(cx):
    D = os.path.join(CXDATA, cx)
    inst = L.read_instances(os.path.join(D, 'instances.tsv'))
    meshes = read_bin(os.path.join(D, 'meshes.bin'))
    mrows = read_mat_rows(os.path.join(D, 'materials.tsv'))
    tidx = {}
    for l in open(os.path.join(TEXDIR, 'index.tsv'), encoding='utf-8').read().splitlines():
        c = l.split('\t')
        if len(c) == 2:
            tidx[c[0]] = os.path.join(TEXDIR, c[1])
    mat_id = {}; mats = []
    def mid(n):
        if n not in mat_id:
            mat_id[n] = len(mats)
            r = mrows.get(n)
            col = [float(x) for x in r[4].split(',')] if r and r[4] else ([0.03, 0.03, 0.03, 1] if 'Black' in n else [1, 1, 1, 1])
            st = [float(x) for x in r[5].split(',')] if r and r[5] else [1, 1, 0, 0]
            clip = bool(r and r[6] not in ('', '0'))
            sm = float(r[8]) if r and len(r) > 8 and r[8] else 0.3
            png = tidx.get(r[2]) if r and r[2] else None
            mats.append(dict(name=n, png=png, npy=None, color=np.array(col, np.float32), st=np.array(st, np.float32), clip=clip,
                             cutoff=float(r[7]) if r and r[7] else 0.5, smooth=sm))
        return mat_id[n]
    objs = {}
    for i in inst:
        d = meshes[i['k']]
        M = i['M']; A = M[:3, :3]
        W = L.xform(M, d['pos'].astype(np.float64))
        Nn = d['nrm'].astype(np.float64) @ np.linalg.inv(A)
        Nn = Nn / np.maximum(np.linalg.norm(Nn, axis=1), 1e-12)[:, None]
        mir = np.linalg.det(A) < 0
        T = []; TM = []
        for s, idx in enumerate(d['subs']):
            t = np.asarray(idx).reshape(-1, 3).astype(np.int64)
            if len(t) == 0:
                continue
            if mir:
                t = t[:, [0, 2, 1]]
            T.append(t); TM.append(np.full(len(t), mid(i['mats'][s] if s < len(i['mats']) else ''), np.int32))
        nm = i['name'][:-5] if i['name'].endswith('_LOD0') else i['name']
        # 같은 이름 인스턴스(사정문 문짝 DoorB_01·DoorB_02·DoorA_01, 강녕전 향오문·행각 문짝 등)는 덮어쓰지 않게 뒤에 __2, __3 …
        # (2026-09-30 — 예전엔 dict 에 덮어써 사정문 오른쪽 칸 문짝이 빠졌음)
        if nm in objs:
            k = 2
            while '%s__%d' % (nm, k) in objs:
                k += 1
            nm = '%s__%d' % (nm, k)
        objs[nm] = dict(P=W, N=Nn, U=d['uv'].astype(np.float64), T=np.concatenate(T), TM=np.concatenate(TM))
    return objs, mats


def merge(objs, names):
    P = []; N = []; U = []; T = []; TM = []; OB = []; base = 0
    for k, nm in enumerate(names):
        o = objs[nm]
        P.append(o['P']); N.append(o['N']); U.append(o['U']); T.append(o['T'] + base); TM.append(o['TM']); OB.append(np.full(len(o['T']), k, np.int32))
        base += len(o['P'])
    return dict(P=np.concatenate(P), N=np.concatenate(N), U=np.concatenate(U), T=np.concatenate(T), TM=np.concatenate(TM), OB=np.concatenate(OB))


if __name__ == '__main__':
    import sys, time
    for cx in sys.argv[1:] or ['Gyotaejeon']:
        t0 = time.time()
        objs, mats = load_complex(cx)
        print(cx, len(objs), 'objects', len(mats), 'mats', '%.1f s' % (time.time() - t0))
        for nm, o in objs.items():
            print('  %-24s tris %8d  bbox %s ~ %s' % (nm, len(o['T']), o['P'].min(0).round(1), o['P'].max(0).round(1)))
        miss = [m['name'] for m in mats if m['png'] is None]
        print('  materials without texture:', miss[:10], len(miss))
