"""5개 단지(흥례문·건춘문·영추문·신무문·광화문 부속단지): 창·문·홍살을 판으로 굽고 결과를 쓴다.
출력(out_cards/<cx>/):
  Cards/<PRE>_<k>_<메시>_card.png   판 아틀라스(RGBA, 앞·뒤(+옆면))
  cards.bin                          바뀐 메시(meshes.bin 형식, Unity 좌표). 이름 = <메시>#CARD(통째 판) / <메시>#SPLIT(틀 + 창살 판) (+ '#F' = 각진 음영)
  cards.tsv                          k, 이름, 종류, 판 재질 이름, 텍스처, 알파 클립, 남긴 서브메시(split), 판 위치 정보, 구워 넣은 쇠붙이 인스턴스 줄 번호
  removed.tsv                        문 판에 구워 넣어 지울 쇠붙이 인스턴스(instances.tsv 줄 번호, 메시 번호, 이름)
사용: python3 run_cards5.py [cx ...]"""
import sys, os, time, json, collections
import numpy as np
from PIL import Image
import os
_HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(_HERE, '..')); sys.path.insert(0, os.path.join(_HERE, '..', 'Simplify')); sys.path.insert(0, _HERE)
from pbsimp import read_bin, write_bin, compact
from cardsel import classify
from cardbake import Scene, bake, read_materials

PRE = {'Heungnyemun': 'HNM', 'Geonchunmun': 'GCM', 'Yeongchumun': 'YCM', 'Sinmumun': 'SMM', 'GwanghwamunComplex': 'GHC'}
ROOT = '/home/claude/palace_simplify/'


def run(cx):
    t_all = time.time()
    D = ROOT + 'data/' + cx
    OUT = ROOT + 'out_cards/' + cx + '/'
    os.makedirs(OUT + 'Cards', exist_ok=True)
    ms = read_bin(D + '/meshes.bin'); rs = read_bin(ROOT + 'out/%s/reduced.bin' % cx); mt = read_materials(D + '/materials.tsv')
    insts = []
    for line in open(D + '/instances.tsv', encoding='utf-8'):
        p = line.rstrip('\n').split('\t')
        insts.append((int(p[0]), p[1], np.array([float(x) for x in p[2:18]]).reshape(4, 4), p[18].split('|')))
    im = {}
    for k, n, M, mats in insts:
        im.setdefault(k, mats)
    sel, lattice = classify(ms, im)
    by_k = collections.defaultdict(list)
    for i, (k, n, M, mats) in enumerate(insts):
        by_k[k].append(i)
    # 천장: 인스턴스가 뒤집혀(메시 +Y 가 아래) 놓인 것이 있으면 그 쪽 그림을 씀(섞여 있으면 앞·뒤 두 장)
    for k, v in sel.items():
        if v.get('style') != 'down':
            continue
        ups = [insts[i][2][1, 1] > 0 for i in by_k[k]]
        if all(ups):
            continue
        v['style'] = 'up' if not any(ups) else 'flat'

    def bbox(k, subs=None):
        d = ms[k]
        if subs is None:
            P = d['pos']
        else:
            idx = np.unique(np.concatenate([d['subs'][s] for s in subs]))
            P = d['pos'][idx]
        return P.min(0).astype(np.float64), P.max(0).astype(np.float64)

    # 문 쇠붙이 → 붙은 문짝(상자 판) 찾기
    leaf_ks = [k for k, v in sel.items() if v.get('style') == 'box']
    acc_of_inst = collections.defaultdict(list)   # 문짝 인스턴스 → [(쇠붙이 인스턴스, 상대 행렬)]
    for ai, (ak, an, AM, amats) in enumerate(insts):
        if sel.get(ak, {}).get('kind') != 'acc':
            continue
        amn, amx = bbox(ak); c = np.append((amn + amx) / 2, 1.0)
        cw = AM @ c
        best = None
        for lk in leaf_ks:
            lmn, lmx = bbox(lk)
            ext = lmx - lmn; t = int(np.argmin(ext))
            pad = np.full(3, 0.02); pad[t] = 0.5
            for li in by_k[lk]:
                LM = insts[li][2]
                cl = np.linalg.solve(LM, cw)[:3]
                if np.all(cl >= lmn - pad) and np.all(cl <= lmx + pad):
                    dcen = np.linalg.norm(cl - (lmn + lmx) / 2)
                    if best is None or dcen < best[0]:
                        best = (dcen, li)
        if best is not None:
            li = best[1]
            acc_of_inst[li].append((ai, np.linalg.solve(insts[li][2], AM)))
    bake_acc = {}   # 문짝 종류 k → [(쇠붙이 메시 k, 상대 행렬)]
    removed = []
    for lk in leaf_ks:
        sets = []
        for li in by_k[lk]:
            s = sorted((insts[ai][0], tuple(np.round(R, 3).ravel())) for ai, R in acc_of_inst.get(li, []))
            sets.append(s)
        if sets and all(s == sets[0] for s in sets) and sets[0]:
            li0 = by_k[lk][0]
            bake_acc[lk] = [(insts[ai][0], R) for ai, R in acc_of_inst[li0]]
            for li in by_k[lk]:
                for ai, R in acc_of_inst[li]:
                    removed.append((ai, insts[ai][0], insts[ai][1]))
        elif any(sets):
            print('  acc not uniform for', ms[lk]['name'], [len(s) for s in sets])

    rows = []; outs = []
    stats = dict(before=0, after=0)
    for k in sorted(sel):
        v = sel[k]
        if v['kind'] == 'acc':
            continue
        d = ms[k]; name = d['name']
        subs = v['subs']
        # 광선 투사 장면: 원본(줄이기 전) 삼각형 + 구워 넣을 쇠붙이
        P = [d['pos'].astype(np.float64)]; U = [d['uv']]; T = []; TM = []; mats = []
        for s in subs:
            T.append(d['subs'][s].reshape(-1, 3)); TM.append(np.full(len(T[-1]), len(mats))); mats.append(mt.get(im[k][s]))
        base = len(d['pos'])
        for ak, R in bake_acc.get(k, []):
            a = ms[ak]
            ph = np.concatenate([a['pos'].astype(np.float64), np.ones((len(a['pos']), 1))], 1) @ R.T
            P.append(ph[:, :3]); U.append(a['uv'])
            for s, idx in enumerate(a['subs']):
                T.append(idx.reshape(-1, 3) + base); TM.append(np.full(len(idx) // 3, len(mats))); mats.append(mt.get(insts[by_k[ak][0]][3][s]))
            base += len(a['pos'])
        pos = np.concatenate(P).astype(np.float32); uv = np.concatenate(U).astype(np.float32)
        tris = np.concatenate(T).astype(np.int32); tmat = np.concatenate(TM).astype(np.int32)
        bmin, bmax = bbox(k, subs if v['kind'] == 'split' else None)
        t0 = time.time()
        sc = Scene(pos, tris, uv, tmat, mats)
        img, (cp, cn, cu, ci), info = bake(sc, bmin, bmax, v['mode'], v['style'])
        sc.free()
        tex = '%s_%03d_%s_card.png' % (PRE[cx], k, name)
        Image.fromarray(img, 'RGBA').save(OUT + 'Cards/' + tex, optimize=True)
        matname = 'CARD_%s_%03d' % (PRE[cx], k)
        r = rs[k]
        flat = r['name'].endswith('#F')
        if v['kind'] == 'full':
            mesh = dict(name=name + '#CARD', pos=cp, nrm=cn, uv=cu, subs=[ci])
            keep = []
        else:
            keep = [s for s in range(len(r['subs'])) if s not in subs]
            P2 = [r['pos']]; N2 = [r['nrm']]; U2 = [r['uv']]; S2 = []
            # 남길 서브메시만(정점은 그대로 두고 인덱스만), 판은 뒤에 붙임
            for s in keep:
                S2.append(r['subs'][s].astype(np.uint32))
            nb = len(r['pos'])
            P2.append(cp); N2.append(cn); U2.append(cu); S2.append((ci + nb).astype(np.uint32))
            pp, nn, uu, ss2 = compact(np.concatenate(P2).astype(np.float32), np.concatenate(N2).astype(np.float32), np.concatenate(U2).astype(np.float32), S2)
            mesh = dict(name=name + '#SPLIT' + ('#F' if flat else ''), pos=pp, nrm=nn, uv=uu, subs=ss2)
        outs.append(mesh)
        n_inst = len(by_k[k])
        before = sum(len(x) for x in [r['subs'][s] for s in subs]) // 3
        after = len(ci) // 3
        stats['before'] += before * n_inst; stats['after'] += after * n_inst
        rows.append([k, name, v['kind'], v['style'], ','.join(info['mode']), matname, tex, int(info['clip']), ','.join(map(str, keep)),
                     json.dumps(dict(axes=info['axes'], W=info['W'], H=info['H'], ppm=round(float(info['ppm']), 2), atlas=info['atlas']))])
        print('  %-30s %-5s %-4s x%-3d tris %6d -> %2d  mode %s clip %d  %dx%d  acc %d  %.1fs' % (
            name, v['kind'], v['style'], n_inst, before, after, info['mode'], info['clip'], info['atlas'][0], info['atlas'][1], len(bake_acc.get(k, [])), time.time() - t0), flush=True)
    # 구워 넣은 쇠붙이 삼각형(지움)
    acc_tris = sum(sum(len(s) for s in rs[insts[ai][0]]['subs']) // 3 for ai, _, _ in removed)
    write_bin(OUT + 'cards.bin', outs)
    with open(OUT + 'cards.tsv', 'w', encoding='utf-8') as f:
        f.write('k\tname\tkind\tstyle\tmode\tmaterial\ttexture\tclip\tkeep_subs\tinfo\n')
        for r in rows:
            f.write('\t'.join(str(x) for x in r[:10]) + '\n')
    with open(OUT + 'removed.tsv', 'w', encoding='utf-8') as f:
        for ai, ak, an in removed:
            f.write('%d\t%d\t%s\n' % (ai, ak, an))
    # 단지 전체 삼각형(데모 바닥 제외): 줄인 것 → 판 적용
    tot_red = 0
    for i, (k, n, M, mats) in enumerate(insts):
        nm = ms[k]['name']
        if nm.endswith('_Ground') or nm == 'Plane':
            continue
        tot_red += sum(len(s) for s in rs[k]['subs']) // 3
    tot_new = tot_red - stats['before'] + stats['after'] - acc_tris
    print('%s: card types %d, replaced tris %d -> %d, acc removed %d (%d tris); complex %d -> %d  (%.0fs)' % (
        cx, len(rows), stats['before'], stats['after'], len(removed), acc_tris, tot_red, tot_new, time.time() - t_all), flush=True)
    json.dump(dict(types=len(rows), before=stats['before'], after=stats['after'], acc=len(removed), acc_tris=acc_tris, total_reduced=tot_red, total_new=tot_new),
              open(OUT + 'stats.json', 'w'))


if __name__ == '__main__':
    for cx in (sys.argv[1:] or list(PRE)):
        run(cx)
