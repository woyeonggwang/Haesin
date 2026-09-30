"""흥례문 데이터 읽기 공용: 원본/줄인/판 메시 + 배치 + 재질."""
import sys, os, numpy as np
_HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(_HERE, '..')); sys.path.insert(0, os.path.join(_HERE, '..', 'Simplify')); sys.path.insert(0, _HERE)
from pbsimp import read_bin
ROOT = os.path.join(_HERE, '..')
CX = 'Heungnyemun'

def read_instances(path):
    out = []
    for l in open(path, encoding='utf-8').read().splitlines():
        if not l.strip(): continue
        c = l.split('\t')
        k = int(c[0]); name = c[1]
        M = np.array([float(x) for x in c[2:18]], np.float64).reshape(4, 4)
        mats = c[18].split('|') if len(c) > 18 else []
        out.append(dict(k=k, name=name, M=M, mats=mats))
    return out

def read_materials(path):
    mt = {}
    for l in open(path, encoding='utf-8').read().splitlines():
        c = l.split('\t')
        if len(c) < 3: continue
        mt[c[0]] = c
    return mt

def load(cards=True):
    D = os.path.join(ROOT, 'data', CX)
    inst = read_instances(os.path.join(D, 'instances.tsv'))
    mats = read_materials(os.path.join(D, 'materials.tsv'))
    red = read_bin(os.path.join(ROOT, 'out', CX, 'reduced.bin'))
    if cards:
        cb = read_bin(os.path.join(ROOT, 'out_cards', CX, 'cards.bin'))
        rows = [l.split('\t') for l in open(os.path.join(ROOT, 'out_cards', CX, 'cards.tsv'), encoding='utf-8').read().splitlines()[1:] if l.strip()]
        for r, d in zip(rows, cb):
            k = int(r[0])
            d = dict(d); d['card_row'] = r
            red[k] = d
    return inst, mats, red

def xform(M, P):
    return P @ M[:3, :3].T + M[:3, 3]
