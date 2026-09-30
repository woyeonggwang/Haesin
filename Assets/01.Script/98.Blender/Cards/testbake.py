import sys, time, collections, numpy as np
import os
_HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(_HERE, '..')); sys.path.insert(0, os.path.join(_HERE, '..', 'Simplify')); sys.path.insert(0, _HERE)
from pbsimp import read_bin
from cardbake import Scene, bake, read_materials
from PIL import Image
OUT = '/tmp/claude-0/-home-claude/d3523ed0-164d-5ce5-81f3-4f3efb3b93be/scratchpad/cardtest/'
import os; os.makedirs(OUT, exist_ok=True)
cx = sys.argv[1]; names = sys.argv[2].split(','); mode = sys.argv[3] if len(sys.argv) > 3 else 'auto'; style = sys.argv[4] if len(sys.argv) > 4 else 'flat'
D = '/home/claude/palace_simplify/data/' + cx
ms = read_bin(D + '/meshes.bin'); mt = read_materials(D + '/materials.tsv')
im = {}
for line in open(D + '/instances.tsv', encoding='utf-8'):
    p = line.rstrip('\n').split('\t'); im.setdefault(int(p[0]), p[18].split('|'))
for nm in names:
    k = [i for i, d in enumerate(ms) if d['name'] == nm][0]
    d = ms[k]
    subs = [s.reshape(-1, 3) for s in d['subs']]
    tris = np.concatenate(subs).astype(np.int32)
    tmat = np.concatenate([np.full(len(s), i) for i, s in enumerate(subs)])
    mats = [mt.get(m) for m in im[k]]
    print(nm, [ (m['name'], bool(m['bc'])) if m else None for m in mats])
    t0 = time.time()
    sc = Scene(d['pos'], tris, d['uv'], tmat, mats)
    img, mesh, info = bake(sc, d['pos'].min(0), d['pos'].max(0), mode, style)
    sc.free()
    Image.fromarray(img, 'RGBA').save(OUT + nm + '.png')
    prev = Image.fromarray(img, 'RGBA'); bg = Image.new('RGBA', prev.size, (255, 0, 255, 255)); bg.alpha_composite(prev)
    bg = bg.convert('RGB'); bg.thumbnail((900, 900)); bg.save(OUT + nm + '_prev.png')
    print('  %.1fs' % (time.time() - t0), info, 'tris', len(mesh[3]) // 3)
