"""Unity 단순화판에 남은 문·창·홍살·문 쇠붙이를 판으로: PalaceCardTool.ExportTargets 결과(targets.bin/tsv, materials.tsv)를 읽어
대상 서브메시를 조각(맞닿은 삼각형 묶음)으로 나누고 조각마다 판을 구워, 메시마다 아틀라스 1장 + 바뀐 메시를 쓴다.
출력(out_unity/):
  ucards2.bin  판 교체 정보(write_ucards2 참고, 메시 로컬 좌표 = Unity). 서브메시 = [대상이 아닌 원래 서브메시(원래 순서)] + [판으로 못 바꾼 조각] + [판]
  ucards.tsv   메시 번호, 메시 이름, 새 서브메시 재질 이름들, 판 재질 이름, 텍스처 파일, 알파 클립, 조각 수, 삼각형(전→후), 아틀라스 배율
  Cards/<메시>_card.png"""
import sys, os, json, time, collections
import numpy as np
from PIL import Image
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
import os
_HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(_HERE, '..')); sys.path.insert(0, os.path.join(_HERE, '..', 'Simplify')); sys.path.insert(0, _HERE)
from pbsimp import read_bin
from cardbake import Scene, bake, read_materials

U = '/mnt/user-data/uploads/Haesin/Temp/PalaceSimplify/unity_cards/'
OUT = '/home/claude/palace_simplify/out_unity/'

# 메시 이름 → (고를 재질 정규식, 모드, 모양). 모양 'box' = 여닫는 문짝(옆면까지), 'flat' = 앞·뒤 판
import re
RULES = [
    (r'^GHM_GateLeaf\d$', r'.*', 'auto', 'box'),
    (r'^GHM_SmallGate_', r'.*', 'auto', 'box'),
    (r'^SM_GNJ_HG_Door02A_024$', r'.*', 'auto', 'box'),
    (r'^SM_HOM_Door0\dA$', r'.*', 'auto', 'box'),
    # 판(앞·뒤)으로 바꾸는 것은 모두 'clear'(맞은 곳만 불투명): 단순화판은 창·문 판이 이미 따로 있어 빈 곳을 창호지로 채우면
    # 그 판을 가림(강녕전 행각 Door03 틀이 흰 창호지로 창살 판을 덮었던 일)
    (r'^SM_GJM_DoorOrnament03A$', r'.*', 'clear', 'flat'),
    (r'^GJ_GJJ$', r'DoorOrnament', 'clear', 'flat'),
    (r'^GJ_RR$', r'DoorOrnament', 'clear', 'flat'),
    (r'^GNJC_EJD$', r'HongSal', 'clear', 'flat'),
    (r'^GNJC_EJD$', r'Irondoor', 'clear', 'flat'),
    (r'^GNJC_GNJ_HG$', r'Door03', 'clear', 'flat'),
    (r'^GNJC_YGD$', r'IronDoor', 'clear', 'flat'),
    (r'^GTJ_HHG$', r'^MI_HHG_Window01$', 'clear', 'flat'),
    # 동궁 DG_Bld_06·HG_24·HG_17·HG_19 의 MI_DG_Door01A 는 문짝이 아니라 3 cm 막대·꼭지라 뺌(판으로 바꾸면 납작해짐)
    (r'^DG_HG_26(_LOD1)?$', r'^MI_DG_Door01A$', 'clear', 'flat'),
    (r'^DG_(HG_26|HG_01|HG_08)(_LOD1)?$', r'IronDoor', 'clear', 'flat'),
    (r'^DG_GateDoors$', r'.*', 'clear', 'flat'),
]


def pieces(pos, tris, gap=0.01):
    """맞닿은(위치가 같은 정점을 공유하는) 삼각형 묶음 → 경계 상자가 gap 안에서 겹치는 묶음끼리 합침. 반환: 삼각형별 조각 번호"""
    q = np.round(pos * 1e4).astype(np.int64)
    _, inv = np.unique(q, axis=0, return_inverse=True); inv = inv.reshape(-1)
    T = inv[tris]
    nv = inv.max() + 1
    r = np.concatenate([T[:, 0], T[:, 1], T[:, 2]]); c = np.concatenate([T[:, 1], T[:, 2], T[:, 0]])
    g = coo_matrix((np.ones(len(r)), (r, c)), shape=(nv, nv))
    ncomp, lab = connected_components(g, directed=False)
    tl = lab[T[:, 0]]
    comps = np.unique(tl)
    P = pos[tris]  # (M,3,3)
    mn = np.full((ncomp, 3), np.inf); mx = np.full((ncomp, 3), -np.inf)
    np.minimum.at(mn, tl, P.min(1)); np.maximum.at(mx, tl, P.max(1))
    ids = comps
    parent = {int(i): int(i) for i in ids}

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]; a = parent[a]
        return a
    changed = True
    while changed:
        changed = False
        roots = sorted(set(find(int(i)) for i in ids))
        R = np.array(roots)
        bmn = np.array([mn[r_] for r_ in R]); bmx = np.array([mx[r_] for r_ in R])
        for i in range(len(R)):
            ov = np.all(bmn[i] - gap <= bmx, axis=1) & np.all(bmx[i] + gap >= bmn, axis=1)
            ov[i] = False
            for j in np.nonzero(ov)[0]:
                a, b = find(int(R[i])), find(int(R[j]))
                if a != b:
                    parent[b] = a
                    mn[a] = np.minimum(mn[a], mn[b]); mx[a] = np.maximum(mx[a], mx[b])
                    changed = True
    return np.array([find(int(x)) for x in tl])


def shelf(images, W):
    order = sorted(range(len(images)), key=lambda i: (-images[i].shape[0], -images[i].shape[1]))
    pos = [None] * len(images); x = y = rowh = 0
    for i in order:
        h, w = images[i].shape[:2]
        if x + w > W:
            x = 0; y += rowh; rowh = 0
        pos[i] = (x, y); x += w; rowh = max(rowh, h)
    H = y + rowh
    return max(p[0] + images[i].shape[1] for i, p in enumerate(pos)), H, pos


def pack(images, maxs=4096, fill=0.8):
    """선반 채우기: 폭 후보마다 배치해 넓이가 가장 작은 것. 반환 (아틀라스, [(x, y)], 줄인 그림들, 배율)
    전체 넓이가 maxs² × fill 을 넘으면 넓이 기준으로 줄여 시작하고, 그래도 안 들어가면 0.95 배씩 더 줄임."""
    area0 = sum(im.shape[0] * im.shape[1] for im in images)
    scale = min(1.0, float(np.sqrt(maxs * maxs * fill / area0)))
    for _ in range(40):
        ims = images if scale >= 0.999 else [np.array(Image.fromarray(im, 'RGBA').resize((max(4, int(round(im.shape[1] * scale))), max(4, int(round(im.shape[0] * scale)))), Image.LANCZOS)) for im in images]
        mw = max(im.shape[1] for im in ims)
        area = sum(im.shape[0] * im.shape[1] for im in ims)
        best = None
        for k in (1.0, 1.1, 1.25, 1.4, 1.6, 1.8, 2.0, 2.5):
            W = int(min(maxs, max(mw, np.sqrt(area) * k)))
            AW, AH, pos = shelf(ims, W)
            if AH <= maxs and (best is None or AW * AH < best[0] * best[1]):
                best = (AW, AH, pos)
        if best is not None and best[0] <= maxs:
            break
        scale *= 0.95
    AW, AH, pos = best
    at = np.zeros((AH, AW, 4), np.uint8)
    for i, im in enumerate(ims):
        x0, y0 = pos[i]; h, w = im.shape[:2]
        at[y0:y0 + h, x0:x0 + w] = im
    return at, pos, ims, (scale if scale < 0.999 else 1.0)


def write_ucards2(path, outs):
    """판 교체 정보(PUC2): 메시마다 이름, 원래 정점 수 nb, 판 정점(위치·법선·UV), 서브메시 목록.
    서브메시 = (0, 원래 서브메시 번호) 또는 (1, 인덱스 배열 — nb 보다 작으면 원래 정점, 크거나 같으면 판 정점 nb + i)"""
    import struct
    with open(path, 'wb') as f:
        f.write(struct.pack('<ii', 0x32435550, len(outs)))
        for o in outs:
            nm = o['name'].encode('utf-8'); f.write(struct.pack('<i', len(nm))); f.write(nm)
            f.write(struct.pack('<ii', o['nb'], len(o['pos'])))
            f.write(np.ascontiguousarray(o['pos'], '<f4').tobytes()); f.write(np.ascontiguousarray(o['nrm'], '<f4').tobytes())
            f.write(np.ascontiguousarray(o['uv'], '<f4').tobytes())
            f.write(struct.pack('<i', len(o['subs'])))
            for kind, v in o['subs']:
                if kind == 0:
                    f.write(struct.pack('<ii', 0, int(v)))
                else:
                    f.write(struct.pack('<ii', 1, len(v))); f.write(np.ascontiguousarray(v, '<i4').tobytes())


def read_ucards2(path):
    import struct
    buf = open(path, 'rb').read(); off = [0]

    def i32():
        v = struct.unpack_from('<i', buf, off[0])[0]; off[0] += 4; return v
    i32(); n = i32(); res = collections.OrderedDict()
    for _ in range(n):
        ln = i32(); name = buf[off[0]:off[0] + ln].decode('utf-8'); off[0] += ln
        nb = i32(); nc = i32()
        pos = np.frombuffer(buf, '<f4', nc * 3, off[0]).reshape(-1, 3); off[0] += nc * 12
        nrm = np.frombuffer(buf, '<f4', nc * 3, off[0]).reshape(-1, 3); off[0] += nc * 12
        uv = np.frombuffer(buf, '<f4', nc * 2, off[0]).reshape(-1, 2); off[0] += nc * 8
        subs = []
        for _ in range(i32()):
            kind = i32(); v = i32()
            if kind == 0:
                subs.append((0, v))
            else:
                subs.append((1, np.frombuffer(buf, '<i4', v, off[0]).copy())); off[0] += v * 4
        res[name] = dict(name=name, nb=nb, pos=pos, nrm=nrm, uv=uv, subs=subs)
    return res


def run(only=None):
    os.makedirs(OUT + 'Cards', exist_ok=True)
    prev = read_ucards2(OUT + 'ucards2.bin') if only else {}
    prev_tsv = {}
    if only:
        for l in open(OUT + 'ucards.tsv', encoding='utf-8').read().splitlines()[1:]:
            prev_tsv[l.split('\t')[1]] = l.split('\t')
    ms = read_bin(U + 'targets.bin'); mt = read_materials(U + 'materials.tsv')
    rows = collections.OrderedDict()
    for line in open(U + 'targets.tsv', encoding='utf-8'):
        p = line.rstrip('\n').split('\t'); rows.setdefault(int(p[0]), p)
    outs = []; tsv = []; cache = {}
    for k, p in rows.items():
        d = ms[k]; name = d['name']; names = p[7].split('|')
        sel = []; modes = {}
        for rx_mesh, rx_mat, mode, style in RULES:
            if re.search(rx_mesh, name):
                for s, mn_ in enumerate(names):
                    if s not in modes and mn_ and re.search(rx_mat, mn_, re.I) and 'card' not in mn_.lower() and len(d['subs'][s]):
                        modes[s] = (mode, style)
        if not modes:
            continue
        if only and name not in only:
            if name in prev:
                outs.append(prev[name]); tsv.append(prev_tsv[name])
            continue
        t0 = time.time()
        sel = sorted(modes)
        tris = np.concatenate([d['subs'][s].reshape(-1, 3) for s in sel]).astype(np.int64)
        tsub = np.concatenate([np.full(len(d['subs'][s]) // 3, s) for s in sel])
        pos = d['pos'].astype(np.float64)
        whole = all(modes[s][1] == 'box' for s in sel)
        if whole:
            pid = np.zeros(len(tris), np.int64)
        else:
            pid = pieces(pos, tris)
        key = (name.replace('_LOD1', ''), hash(pos[np.unique(tris)].round(4).tobytes()), tuple(sel))
        imgs = []; quads = []; clip_any = False; npieces = 0
        leftover = collections.defaultdict(list)   # 판으로 못 바꾼 조각(입체 덩어리) → 원래 서브메시로 남김
        for pi in np.unique(pid):
            m = pid == pi
            tt = tris[m]; ts = tsub[m]
            mode, style = modes[int(ts[0])]
            # 같은 조각 안에 모드가 섞이면(홍살+문) 투명 우선
            if any(modes[int(s)][0] == 'clear' for s in np.unique(ts)):
                mode = 'clear'
            P = pos[np.unique(tt)]; bmin = P.min(0); bmax = P.max(0); ext = bmax - bmin
            srt = np.sort(ext)
            if style != 'box' and srt[0] > max(0.35, 0.3 * srt[1]):
                # 판으로 보기 어려운 입체 덩어리는 그대로 둠
                for s_ in np.unique(ts):
                    leftover[int(s_)].append(tt[ts == s_])
                continue
            mats = []; mi = []
            for s in np.unique(ts):
                mi.append(s)
            tmat = np.array([mi.index(s) for s in ts], np.int32)
            mats = [mt.get(names[s].replace('_Shell', '')) or mt.get(names[s]) for s in mi]
            sc = Scene(d['pos'], tt.astype(np.int32), d['uv'], tmat, mats)
            img, (cp, cn, cu, ci), info = bake(sc, bmin, bmax, mode, style)
            sc.free()
            imgs.append(img); quads.append((cp, cn, cu, ci)); clip_any |= info['clip']; npieces += 1
        if npieces == 0:
            continue
        if key in cache:
            atlas_file, pos_, at_shape, sizes, scale = cache[key]
        else:
            at, pos_, ims_, scale = pack(imgs)
            at_shape = at.shape; sizes = [im.shape[:2] for im in ims_]
            atlas_file = '%s_card.png' % name
            if not clip_any:
                at[..., 3] = 255
            Image.fromarray(at, 'RGBA').save(OUT + 'Cards/' + atlas_file, optimize=True)
            cache[key] = (atlas_file, pos_, at_shape, sizes, scale)
        AH, AW = at_shape[:2]
        # 판 정점 UV 를 조각 칸 → 아틀라스로
        CP = []; CN = []; CU = []; CI = []; base = 0
        for (cp, cn, cu, ci), (h, w), (x0, y0) in zip(quads, sizes, pos_):
            u = (x0 + cu[:, 0] * w) / AW
            v = 1.0 - (y0 + (1.0 - cu[:, 1]) * h) / AH
            CP.append(cp); CN.append(cn); CU.append(np.stack([u, v], 1)); CI.append(ci + base); base += len(cp)
        CP = np.concatenate(CP); CN = np.concatenate(CN); CU = np.concatenate(CU); CI = np.concatenate(CI)
        keep = [s for s in range(len(d['subs'])) if s not in modes]
        nb = len(d['pos'])
        subs = [(0, s) for s in keep]
        matnames = [names[s] for s in keep]
        for s_, parts in sorted(leftover.items()):
            subs.append((1, np.concatenate(parts).reshape(-1).astype(np.int32))); matnames.append(names[s_])
        subs.append((1, (CI + nb).astype(np.int32))); matnames.append('CARD_' + name)
        outs.append(dict(name=name, nb=nb, pos=CP.astype(np.float32), nrm=CN.astype(np.float32), uv=CU.astype(np.float32), subs=subs))
        before = sum(len(d['subs'][s]) for s in modes) // 3
        after = len(CI) // 3 + sum(len(np.concatenate(v)) for v in leftover.values())
        tsv.append([k, name, '|'.join(matnames), 'CARD_' + name, cache[key][0], int(clip_any), npieces, before, after, '%.3f' % cache[key][4]])
        print('%-28s sel %s pieces %3d tris %6d -> %4d clip %d atlas %dx%d  %.1fs' % (name, [names[s] for s in sel][:3], npieces, before, after, clip_any, AW, AH, time.time() - t0), flush=True)
    write_ucards2(OUT + 'ucards2.bin', outs)
    with open(OUT + 'ucards.tsv', 'w', encoding='utf-8') as f:
        f.write('k\tname\tmaterials\tcard_material\ttexture\tclip\tpieces\ttris_before\ttris_after\tatlas_scale\n')
        for r in tsv:
            f.write('\t'.join(str(x) for x in r) + '\n')
    print('done', len(outs))


if __name__ == '__main__':
    run(set(sys.argv[1:]) or None)
