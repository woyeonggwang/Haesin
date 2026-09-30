"""흥례문 부분별 LOD 만들기 → work/out/meshes.bin, layout.txt, doors.bin, cards.tsv, Cards/*.png, stats.tsv

LOD0 = 밖에서 보이는 삼각형(vis_keep2) 그대로 + 기와 밑 받침판(지운 지붕 속이 기와 틈으로 비치지 않게, 기와 골 바닥 3 cm 아래)
LOD1 = LOD0 에서 기와(RoofTile)·막새(YeonDan) 빼고 검은 판 + 나머지 줄임(잔부재 6 cm / 구조재 3 cm, UV 가중 20)
LOD2 = 같은 방식, 줄임 20 cm / 10 cm(UV 가중 5, UV 이음매 넘어 접기 허용) + 작은 떨어진 조각 없앰(Prune) — 500 m 에서 화소 약 0.5 m
전환: 부분 크기 기준 화면 높이 — LOD0 은 약 200 m, LOD1 은 약 500 m 까지, 끝 0.004 (FOV 60°: 화면 높이 = 크기 / (거리 × 1.1547))
"""
import sys, os, re, time, json, shutil
import numpy as np
_HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, _HERE)
from load import load, xform, ROOT, CX
from parts import *
from plate import build_sheets, fill_small_holes, triangulate, underlay
from export import finalize, write_meshes
import pbsimp
from pbsimp import weld, simplify, compact

WORK = os.path.join(_HERE, 'work')
OUT = os.path.join(WORK, 'out')
TILE_MATS = {'MI_HNM_RoofTile'}
EAVE_MATS = {'MI_HNM_YeonDan_A', 'MI_HNM_YeonDan_B', 'MI_HNM_YeonDan_C'}
FINE_RE = re.compile(r'Gongpo|Ornament|Dragon|Hayebdongja|Handrail|Hwaban|JuduSoro|Grating|Fence|Iron|Entranceparts|Buyeon|AngleRafter|Signboard|Wood$')
TOL = {1: dict(fine=0.06, coarse=0.03, prune=False, w_uv=20.0, permissive=True), 2: dict(fine=0.20, coarse=0.10, prune=True, w_uv=5.0, permissive=True)}
PERMISSIVE = 1 << 5          # meshopt_SimplifyPermissive: UV 이음매를 넘어 접기 허용(먼 LOD 만)
D_LOD0, D_LOD1, H_CULL = 200.0, 500.0, 0.004
BLACK = 'MAT_RoofPlate_Black'
W_UV, W_N = 100.0, 0.2


def weld_pos(pos, nrm, uv, subs, q=1e3):
    """위치(1 mm)만 같으면 정점을 합침(서브메시별). UV·법선은 먼저 나온 정점 값."""
    P = []; N = []; U = []; S = []; base = 0
    for s in subs:
        t = np.asarray(s, np.int64).reshape(-1, 3)
        key = np.round(pos[t.ravel()] * q).astype(np.int64)
        uk, first, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
        inv = inv.reshape(-1); src = t.ravel()[first]
        P.append(pos[src]); N.append(nrm[src]); U.append(uv[src])
        tri = inv.reshape(-1, 3)
        good = (tri[:, 0] != tri[:, 1]) & (tri[:, 1] != tri[:, 2]) & (tri[:, 0] != tri[:, 2])
        S.append((tri[good].astype(np.uint32) + base).reshape(-1)); base += len(src)
    return np.concatenate(P).astype(np.float32), np.concatenate(N).astype(np.float32), np.concatenate(U).astype(np.float32), S


def decimate(m, lod, log=print):
    """merge_chunks 결과(m)를 재질 부류별로 줄임. 판(CARD_) 은 그대로."""
    if m is None:
        return None
    cls = []
    for mn in m['mats']:
        cls.append('keep' if mn.startswith('CARD_') or mn == BLACK else ('fine' if FINE_RE.search(mn) else 'coarse'))
    outP = []; outN = []; outU = []; outS = []; outM = []; outF = []; base = 0
    for c in ('keep', 'fine', 'coarse'):
        ks = [k for k in range(len(cls)) if cls[k] == c]
        if not ks:
            continue
        for flat in (True, False):
            kk = [k for k in ks if m['flats'][k] == flat]
            if not kk:
                continue
            subs0 = [np.asarray(m['subs'][k], np.int64) for k in kk]
            used = np.unique(np.concatenate(subs0))
            remap = np.full(len(m['pos']), -1, np.int64); remap[used] = np.arange(len(used))
            pos = m['pos'][used].astype(np.float32); nrm = m['nrm'][used].astype(np.float32); uv = m['uv'][used].astype(np.float32)
            subs = [remap[s].astype(np.uint32) for s in subs0]
            if c != 'keep':
                pos, nrm, uv, subs = weld(pos, nrm, uv, subs, use_normals=not flat)
                opt = pbsimp.ErrorAbsolute | (pbsimp.Prune if TOL[lod]['prune'] else 0) | (PERMISSIVE if TOL[lod].get('permissive') else 0)
                subs, err = simplify(pos, nrm, uv, subs, TOL[lod][c], TOL[lod]['w_uv'], 0.0 if flat else W_N, options=opt)
                pos, nrm, uv, subs = compact(pos, nrm, uv, subs)
            outP.append(pos.astype(np.float64)); outN.append(nrm.astype(np.float64)); outU.append(uv.astype(np.float64))
            for k, s in zip(kk, subs):
                outS.append(np.asarray(s, np.int64) + base); outM.append(m['mats'][k]); outF.append(flat)
            base += len(pos)
    return dict(pos=np.concatenate(outP), nrm=np.concatenate(outN), uv=np.concatenate(outU), subs=outS, mats=outM, flats=outF)


def roof_sheets_meshes(chunks, plate_offset=0.03, tol=0.01, log=print):
    """부분의 기와 삼각형 → (검은 판, 받침판) — merge 형식 dict 두 개(없으면 None)."""
    from plate import underlay
    from rayeval import RMesh
    P = []; T = []; b = 0; KP = []; KT = []; kb = 0
    for (W, N, UV, tt, mn, flat, ii, s) in chunks:
        used, inv = np.unique(tt.ravel(), return_inverse=True)
        if mn in TILE_MATS:
            P.append(W[used]); T.append(inv.reshape(-1, 3) + b); b += len(used)
        else:
            KP.append(W[used]); KT.append(inv.reshape(-1, 3) + kb); kb += len(used)
    if not P:
        return None, None
    P = np.concatenate(P).astype(np.float32); T = np.concatenate(T).astype(np.int32)
    rmK = None
    if KP:
        KP = np.concatenate(KP).astype(np.float32); KT = np.concatenate(KT).astype(np.int32)
        rmK = RMesh(KP, KT, np.zeros((len(KP), 2), np.float32), np.zeros(len(KT), np.int32))
    sheets, rm = build_sheets(P, T, cell=0.25, gap=0.8, log=log)
    VV = []; FF = []; base = 0; UV_ = []; UF = []; ub = 0
    for sh in sheets:
        fill_small_holes(sh)
        V, F = triangulate(sh, plate_offset)
        if len(F):
            VV.append(V); FF.append(F + base); base += len(V)
        V2, F2 = underlay(sh, need_rm=rmK)
        if len(F2):
            UV_.append(V2); UF.append(F2 + ub); ub += len(V2)
    rm.free()
    if rmK is not None:
        rmK.free()

    def pack(VL, FL, simp):
        if not VL:
            return None
        V = np.concatenate(VL).astype(np.float32); F = np.concatenate(FL).astype(np.uint32)
        n0 = len(F)
        nrm = np.zeros_like(V); uv = np.zeros((len(V), 2), np.float32)
        if simp:
            subs, err = simplify(V, nrm, uv, [F.reshape(-1)], tol, 0.0, 0.0, options=pbsimp.ErrorAbsolute | pbsimp.LockBorder)
            V, _, _, subs = compact(V, nrm, uv, subs)
            t = subs[0].reshape(-1, 3).astype(np.int64)
        else:
            t = F.astype(np.int64)
        fn = np.cross(V[t[:, 1]] - V[t[:, 0]], V[t[:, 2]] - V[t[:, 0]]).astype(np.float64)
        vn = np.zeros((len(V), 3))
        for k in range(3):
            np.add.at(vn, t[:, k], fn)
        vn /= np.maximum(np.linalg.norm(vn, axis=1), 1e-12)[:, None]
        log('  sheet mesh tris %d -> %d' % (n0, len(t)))
        return dict(pos=V.astype(np.float64), nrm=vn, uv=np.zeros((len(V), 2)), subs=[t.reshape(-1)], mats=[BLACK], flats=[False])
    return pack(VV, FF, True), pack(UV_, UF, True)


def trim_exposed(ul, rm_all, F, log=print):
    """받침판 삼각형 중 옆·아래(기와를 거치지 않는 방향)에서 바깥 공기가 보이는 것은 뺀다(두 지붕이 만나는 틈 등)."""
    if ul is None:
        return None
    P = ul['pos']; t = ul['subs'][0].reshape(-1, 3)
    A = P[t[:, 0]]; B = P[t[:, 1]]; C = P[t[:, 2]]
    samples = [(A + B + C) / 3, 0.7 * A + 0.15 * B + 0.15 * C, 0.15 * A + 0.7 * B + 0.15 * C, 0.15 * A + 0.15 * B + 0.7 * C]
    dirs = []
    for el in (-60, -20, 5):
        for az in range(0, 360, 45):
            a = np.radians(az + (22.5 if el == -20 else 0)); e = np.radians(el)
            dirs.append((np.cos(e) * np.cos(a), np.sin(e), np.cos(e) * np.sin(a)))
    dirs.append((0, -1, 0))
    exposed = np.zeros(len(t), bool)
    taus = np.array([0.15, 0.3, 0.6, 1.2, 2.4, 4.8, 9.6, 19.2])
    for S in samples:
        for d in dirs:
            d = np.array(d, np.float64)
            O = S + d * 0.01
            tt, tri, _, _ = rm_all.cast(O.astype(np.float32), np.tile(d, (len(O), 1)).astype(np.float32), 300.0)
            seen = tri < 0
            for tau in taus:
                m = (~seen) & (tt > tau + 0.1)
                if not m.any():
                    break
                seen[np.where(m)[0][F.value(O[m] + d * tau) == 2]] = True
            exposed |= seen
    keep = ~exposed
    if not keep.any():
        log('  underlay all exposed -> none')
        return None
    tk = t[keep]
    used, inv = np.unique(tk.ravel(), return_inverse=True)
    log('  underlay trim %d -> %d tris' % (len(t), len(tk)))
    return dict(pos=P[used], nrm=ul['nrm'][used], uv=ul['uv'][used], subs=[inv.astype(np.int64)], mats=ul['mats'], flats=ul['flats'])


def concat(a, b):
    if a is None:
        return b
    if b is None:
        return a
    n = len(a['pos'])
    return dict(pos=np.concatenate([a['pos'], b['pos']]), nrm=np.concatenate([a['nrm'], b['nrm']]), uv=np.concatenate([a['uv'], b['uv']]),
                subs=a['subs'] + [s + n for s in b['subs']], mats=a['mats'] + b['mats'], flats=a['flats'] + b['flats'])


def run(log=print):
    t0 = time.time()
    os.makedirs(OUT, exist_ok=True); os.makedirs(os.path.join(OUT, 'Cards'), exist_ok=True)
    inst, mats, red = load()
    names = [red[i['k']]['name'].split('#')[0] for i in inst]
    import parts as P_
    vis = np.load(os.path.join(WORK, 'vis_keep2.npy'))
    np.save(os.path.join(WORK, 'vis_keep_final.npy'), vis)
    # load_vis 가 vis_keep.npy 를 읽으므로 최종본을 넘김
    meta = np.load(os.path.join(WORK, 'vis_scene_meta.npz'))
    TI, TS = meta['TI'], meta['TS']
    key = TI.astype(np.int64) * 64 + TS
    change = np.concatenate([[True], key[1:] != key[:-1]]); starts = np.where(change)[0]; ends = np.concatenate([starts[1:], [len(key)]])
    keep = {(int(TI[s]), int(TS[s])): vis[s:e] for s, e in zip(starts, ends)}
    parts = collect(inst, mats, red, keep)
    # 받침판 노출 검사용: 모든 부분의 LOD0 삼각형 + 문짝, 바깥 공기 복셀장
    from rayeval import RMesh
    from voxfield import Field
    AP = []; AT = []; ab = 0
    for pn_, ch_ in parts.items():
        for (W, N, UV, tt, mn, flat, ii, s_) in ch_:
            used, inv = np.unique(tt.ravel(), return_inverse=True)
            AP.append(W[used]); AT.append(inv.reshape(-1, 3) + ab); ab += len(used)
    for ii, i in enumerate(inst):
        if DOOR_RE.match(names[ii]):
            d = red[i['k']]; W = xform(i['M'], d['pos'].astype(np.float64))
            for s_ in d['subs']:
                AT.append(s_.reshape(-1, 3).astype(np.int64) + ab)
            AP.append(W); ab += len(W)
    AP = np.concatenate(AP).astype(np.float32); AT = np.concatenate(AT).astype(np.int32)
    rm_all = RMesh(AP, AT, np.zeros((len(AP), 2), np.float32), np.zeros(len(AT), np.int32))
    fm = np.load(os.path.join(_HERE, 'field_meta.npy')); FLD = Field(fm[:3], fm[3], tuple(int(x) for x in fm[4:7]), np.load(os.path.join(_HERE, 'field_g.npy')))
    meshes = []; layout = ['# 흥례문 단순화판: PART 이름 기준점(x y z) 크기 LOD0→1 LOD1→2 끝 / MESH LOD 메시 [collider]', 'PREFAB\tHeungnyemun_Simplified']
    stats = ['part\tlod0\tlod1\tlod2\tplate\tunderlay\tsize\tref']
    used_mats = set()
    for pn in PART_NAMES:
        ch = parts[pn]
        if not ch:
            continue
        m0 = merge_chunks(ch)
        pl, ul = roof_sheets_meshes(ch, log=log) if pn != 'GROUND' else (None, None)
        ul = trim_exposed(ul, rm_all, FLD, log=log)
        lod0 = finalize(concat(m0, ul))
        mb = merge_chunks(ch, drop_mats=TILE_MATS | EAVE_MATS)
        lods = [lod0]
        for lod in (1, 2):
            d = decimate(mb, lod, log=log)
            lods.append(finalize(concat(d, pl)))
        lo = lod0['pos'].min(0); hi = lod0['pos'].max(0)
        ref = (lo + hi) / 2; size = float((hi - lo).max())
        h0 = size / (D_LOD0 * 1.1547); h1 = size / (D_LOD1 * 1.1547)
        h0 = min(h0, 0.95); h1 = min(h1, h0 * 0.9)
        layout.append('PART\tHNM_%s\t%.4f\t%.4f\t%.4f\t%.3f\t%.5f\t%.5f\t%.5f' % (pn, ref[0], ref[1], ref[2], size, h0, h1, H_CULL))
        tri = []
        for l, m in enumerate(lods):
            nm = 'HNM_%s_LOD%d' % (pn, l)
            meshes.append((nm, m)); used_mats |= set(m['mats'])
            layout.append('MESH\t%d\t%s%s' % (l, nm, '\tcollider' if l == 0 else ''))
            tri.append(sum(len(s) for s in m['subs']) // 3)
        stats.append('%s\t%d\t%d\t%d\t%d\t%d\t%.1f\t%s' % (pn, tri[0], tri[1], tri[2], 0 if pl is None else len(pl['subs'][0]) // 3, 0 if ul is None else len(ul['subs'][0]) // 3, size, ','.join('%.1f' % x for x in ref)))
        log('%-7s LOD0 %8d LOD1 %8d LOD2 %8d size %.1f  %.0fs' % (pn, tri[0], tri[1], tri[2], size, time.time() - t0))
    layout.append('DOORS\tHeungnyemun_DoorsCard')
    write_meshes(os.path.join(OUT, 'meshes.bin'), meshes)
    # 여는 문짝 판(문짝 메시 자기 좌표)
    doors = []; seen = set()
    for ii, i in enumerate(inst):
        nm = names[ii]
        if DOOR_RE.match(nm) and nm not in seen:
            seen.add(nm)
            d = red[i['k']]
            r = d['card_row']
            m = dict(pos=d['pos'].astype(np.float64), nrm=d['nrm'].astype(np.float64), uv=d['uv'].astype(np.float64),
                     subs=[np.asarray(s, np.int64) for s in d['subs']], mats=[r[5]] * len(d['subs']), flats=[True] * len(d['subs']))
            doors.append((nm, finalize(m))); used_mats.add(r[5])
    write_meshes(os.path.join(OUT, 'doors.bin'), doors)
    # 판 재질 목록 + 텍스처
    rows = ['material\ttexture\tclip']
    for d in red:
        r = d.get('card_row')
        if r and r[5] in used_mats:
            src = os.path.join(ROOT, 'out_cards', CX, 'Cards', r[6])
            patched = os.path.join(WORK, 'cards', r[6])
            shutil.copy2(patched if os.path.exists(patched) else src, os.path.join(OUT, 'Cards', r[6]))
            rows.append('%s\t%s\t%s' % (r[5], r[6], r[7]))
    open(os.path.join(OUT, 'cards.tsv'), 'w', encoding='utf-8').write('\n'.join(rows) + '\n')
    open(os.path.join(OUT, 'layout.txt'), 'w', encoding='utf-8').write('\n'.join(layout) + '\n')
    open(os.path.join(OUT, 'stats.tsv'), 'w', encoding='utf-8').write('\n'.join(stats) + '\n')
    log('materials', sorted(used_mats))
    log('done %.0fs' % (time.time() - t0))


if __name__ == '__main__':
    run()
