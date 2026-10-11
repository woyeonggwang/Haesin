"""영추문 데모 원본에서 한 건물(데모 x 구간)의 삼각형을 모은다 — 임포스터(지붕 모양 상자) 굽기용.

구간은 Gates/build_gates.py 의 REGIONS 와 같은 뜻(배치 경계 상자 가운데 x): ㄱ자 첫 채 BLD_L1 = 33~65.
데모 바닥(Plane·skysphere)은 뺌. 거울 배치(행렬식 < 0)는 감김을 뒤집고 법선은 역전치로 옮김.
"""
import sys, os, json
import numpy as np
import common
from pbsimp import read_bin
import load as L
DATA = common.DATA; TEXC = common.TEXC


def is_ground(name):
    return name == 'Plane' or name.startswith('Plane') or 'skysphere' in name.lower()


def read_mat_rows(path):
    rows = {}
    for l in open(path, encoding='utf-8').read().splitlines():
        c = l.split('\t')
        if len(c) >= 9:
            rows[c[0]] = c
    return rows


def load_building(cx='Yeongchumun', xmin=33.0, xmax=65.0):
    D = os.path.join(DATA, cx)
    inst = L.read_instances(os.path.join(D, 'instances.tsv'))
    meshes = read_bin(os.path.join(D, 'meshes.bin'))
    mrows = read_mat_rows(os.path.join(D, 'materials.tsv'))
    mm_path = os.path.join(TEXC, '%s_matmap.json' % cx)
    matmap = json.load(open(mm_path, encoding='utf-8')) if os.path.exists(mm_path) else {}
    P = []; N = []; U = []; T = []; TM = []; names = []; base = 0
    mat_id = {}; mats = []
    def mid(n):
        if n not in mat_id:
            mat_id[n] = len(mats)
            r = mrows.get(n)
            col = [float(x) for x in r[4].split(',')] if r and r[4] else [1, 1, 1, 1]
            st = [float(x) for x in r[5].split(',')] if r and r[5] else [1, 1, 0, 0]
            clip = bool(r and r[6] not in ('', '0'))
            sm = float(r[8]) if r and r[8] else 0.3
            f = matmap.get(n)
            npy = os.path.join(TEXC, f) if f else None
            # 512² npy 캐시가 없으면(Unity Temp 는 지워질 수 있음) materials.tsv 의 원본 텍스처 경로를 바로 읽음(common.png_rgba 가 1024 로 줄임)
            png = r[2] if (not npy or not os.path.exists(npy)) and r and r[2] and os.path.exists(r[2]) else None
            mats.append(dict(name=n, npy=npy if (npy and os.path.exists(npy)) else None, png=png, color=np.array(col, np.float32), st=np.array(st, np.float32), clip=clip,
                             cutoff=float(r[7]) if r and r[7] else 0.5, smooth=sm))
        return mat_id[n]
    for i in inst:
        d = meshes[i['k']]; nm = d['name']
        if is_ground(nm):
            continue
        M = i['M']; A = M[:3, :3]
        W = L.xform(M, d['pos'].astype(np.float64))
        xc = (W[:, 0].min() + W[:, 0].max()) / 2
        if not (xmin <= xc < xmax):
            continue
        Nn = d['nrm'].astype(np.float64) @ np.linalg.inv(A)
        Nn = Nn / np.maximum(np.linalg.norm(Nn, axis=1), 1e-12)[:, None]
        mir = np.linalg.det(A) < 0
        for s, idx in enumerate(d['subs']):
            t = np.asarray(idx).reshape(-1, 3).astype(np.int64)
            if len(t) == 0:
                continue
            if mir:
                t = t[:, [0, 2, 1]]
            mn = i['mats'][s] if s < len(i['mats']) else ''
            T.append(t + base); TM.append(np.full(len(t), mid(mn), np.int32))
        P.append(W); N.append(Nn); U.append(d['uv'].astype(np.float64)); names.append(nm); base += len(W)
    return dict(P=np.concatenate(P), N=np.concatenate(N), U=np.concatenate(U), T=np.concatenate(T), TM=np.concatenate(TM), mats=mats, names=names)


if __name__ == '__main__':
    b = load_building()
    P = b['P']; T = b['T']
    print('instances', len(b['names']), 'tris', len(T), 'verts', len(P))
    print('bbox', P.min(0).round(2), P.max(0).round(2))
    from collections import Counter
    c = Counter(); 
    for m, n in zip(*np.unique(b['TM'], return_counts=True)):
        c[b['mats'][m]['name']] = int(n)
    for k, v in c.most_common():
        mm = b['mats'][[x['name'] for x in b['mats']].index(k)]
        print('  %-26s %7d clip=%d st=%s npy=%s' % (k, v, mm['clip'], mm['st'].tolist(), os.path.basename(mm['npy']) if mm['npy'] else None))
    cn = Counter(n.split('#')[0] for n in b['names'])
    print(len(cn), 'mesh names:', ', '.join('%s×%d' % kv for kv in cn.most_common(40)))


def load_objects(cx, ranges):
    """여러 건물(이름 → (xmin, xmax))을 한 재질 목록으로: impcx 가 쓰는 objs(이름 → P, N, U, T, TM)와 mats"""
    mats = []; mat_id = {}; objs = {}
    for nm, (x0, x1) in ranges.items():
        b = load_building(cx, x0, x1)
        remap = np.zeros(len(b['mats']), np.int32)
        for k, m in enumerate(b['mats']):
            if m['name'] not in mat_id:
                mat_id[m['name']] = len(mats); mats.append(m)
            remap[k] = mat_id[m['name']]
        objs[nm] = dict(P=b['P'], N=b['N'], U=b['U'], T=b['T'], TM=remap[b['TM']])
    return objs, mats
