"""육조거리 행랑(길가 줄행랑) 조각 — 사정전 남행각(단순화판)에서 멀쩡한 칸을 떼어 일자로 이어 붙이는 부품.

사용자 지시(2026-09-30): "멀쩡한 벽 한칸이랑 문을 떼가지고 그걸 계속 일자로 이어붙이세요"(서울역사박물관 육조거리 복원 모형처럼 길 양옆이 이어진 행랑).
한 칸(기둥 사이 약 3.03 m, 들쭉날쭉 2.89~3.17)은 기와 줄 간격(약 0.3565 m)의 정수배가 아니라 이어 붙이면 이음매마다 기와가 최대 18 cm 어긋남 →
기와 줄이 17줄로 딱 맞는 두 칸(기둥 가운데 x 14.130 ~ 20.205, 6.075 m, 어긋남 +1.5 cm)을 BAY 로 씀.
조각(원래 단지 좌표 x 구간, 앞쪽 = z < ZROW(처마 뒤 끝 12.4 m + 여유)):
  BAY    [14.130, 20.205]            두 칸
  GATE   [-7.953, 14.130]            사정문 + 양옆 행각(서쪽 끝은 BAY 끝과 기와 -0.6 cm, 동쪽 끝은 BAY 시작과 같은 기둥)
  END_W  x < -7.953 앞쪽 + 서쪽 옆 행각 전체(x < XW) — 줄 끝(모서리 추녀 + 옆 행각이 길·네거리 쪽으로 드러나서 원래 메시)
  END_E  x > 14.130 앞쪽 + 동쪽 옆 행각 전체(x > XE)
조각 좌표 = (x - 조각 기준 x, y - 땅 0.55, z - 처마 끝 2.52). 기준 x: BAY/END_E = 14.130, GATE/END_W = -7.953.
LOD: 0 = 그대로 / 1 = 8 cm 줄임(UV 이음매 넘어 접기, 자른 면 정점은 고정 — 이웃 조각과 LOD 가 달라도 틈이 안 생김) / 2 = 임포스터 껍데기(yj_build.py rowbake 의 ROW_<조각>).
python3 yj_row.py → out/YJR/YJR_Modules.bin (HNMS: YJR_<조각>_LOD<k>) + modules.json
"""
import os, sys, json
import numpy as np
import ctypes
import common
from load_cx import load_complex
import yj_build as Y
from export import write_meshes
import pbsimp
from pbsimp import weld, compact

ZROW = 15.12
XW, XE = -41.6, 42.9
XG0, XG1, XB1 = -7.953, 14.130, 20.205
GROUND, ZF = 0.55, 2.52
ROW_OBJS = ('SJC_SJJ_HG', 'SJJ_HGba_RoofBacking', 'SJC_SJM', 'SJM_RoofBacking')
PERMISSIVE = 1 << 5
TOL = {1: (0.08, 10.0, True)}      # (오차 m, UV 가중, 이음매 넘어 접기) — LOD2 는 임포스터 껍데기(YJ_A 의 ROW_<조각>)


# 길 쪽 지붕면 기와 = 원본(사용자 지시 2026-09-30 "거리쪽 기와는 조금 살려줄수있나요" → "길 쪽 지붕면만 원본처럼").
# 원본 = Unity `Sajeongjeon_RoofLOD.prefab` 의 LOD0(데모 원본 기와, 긴 판은 16 m 칸으로 잘린 것)에서 x -10~22 · 무게중심 z < 9.5 인
# 남행각 `MI_SJJ_HG_Roof01`·사정문 `MI_SJM_Kiwa` 삼각형을 내보낸 orig_tiles.bin(HNMS, 단지 프리팹 루트 좌표).
# 앞쪽(처마 ~ 용마루 선) 기와만 원본으로 바꾸고, 뒤쪽 지붕면은 단순화판 그대로. 용마루(Yongmaru)·용머리·마루돌은 단순화판.
TILE_MATS = {'MI_SJJ_HG_Roof01': 'HG', 'MI_SJJ_HG_Roof01_Shell': 'HG', 'MI_SJM_Kiwa': 'SJM', 'MI_SJM_Kiwa_Shell': 'SJM'}
RIDGE_HG = ZF + 4.97        # 남행각 용마루 가운데(단지 z)
ORIG_FRONT = True


def _orig_path():
    from impcx import OUTROOT
    for p in (os.path.join(OUTROOT, 'YJR', 'orig_tiles.bin'), '/mnt/user-data/uploads/Haesin/Captures/impostor/out/YJR/orig_tiles.bin'):
        if os.path.exists(p):
            return p
    raise FileNotFoundError('orig_tiles.bin')


def _sub(o, sel):
    return dict(P=o['P'], N=o['N'], U=o['U'], T=o['T'][sel], TM=o['TM'][sel])


def _cat(a, b):
    base = len(a['P'])
    return dict(P=np.concatenate([a['P'], b['P']]), N=np.concatenate([a['N'], b['N']]), U=np.concatenate([a['U'], b['U']]),
                T=np.concatenate([a['T'], b['T'] + base]), TM=np.concatenate([a['TM'], b['TM']]))


def orig_front(objs, mats):
    """단순화판 기와의 앞쪽 지붕면을 지우고(용마루 면에서 잘라 뒤쪽만 남김) 원본 앞쪽 기와를 오브젝트 ORIG_TILES 로 더함.
    사정문 용마루 면 z = 원본 사정문 기와 가장 높은 곳(위 5 cm 안)의 z 평균."""
    import yj_corner as C
    name2i = {m['name']: i for i, m in enumerate(mats)}
    org = C.read_hnms(_orig_path())
    sjm = org['ORIG_MI_SJM_Kiwa']['pos']
    top = sjm[:, 1] > sjm[:, 1].max() - 0.05
    ridge = {'HG': RIDGE_HG, 'SJM': float(sjm[top, 2].mean())}
    tid = {name2i[k]: v for k, v in TILE_MATS.items() if k in name2i}
    for nm in list(objs):
        o = objs[nm]
        kind = np.array([tid.get(int(m), '') for m in o['TM']])
        if not (kind != '').any():
            continue
        keep = _sub(o, kind == '')
        for kd in ('HG', 'SJM'):
            sel = kind == kd
            if sel.any():
                _, back = Y.clip_plane(_sub(o, sel), 2, ridge[kd])
                keep = _cat(keep, back)
        objs[nm] = keep
    P = []; N = []; U = []; T = []; TM = []; base = 0
    for key, m in org.items():
        mn = key[5:]
        lo, _ = Y.clip_plane(dict(P=m['pos'].astype(np.float64), N=m['nrm'].astype(np.float64), U=m['uv'].astype(np.float64),
                                  T=np.concatenate(m['subs']).reshape(-1, 3).astype(np.int64),
                                  TM=np.full(len(np.concatenate(m['subs'])) // 3, name2i[mn], np.int32)), 2, ridge[TILE_MATS[mn]])
        P.append(lo['P']); N.append(lo['N']); U.append(lo['U']); T.append(lo['T'] + base); TM.append(lo['TM']); base += len(lo['P'])
    objs['ORIG_TILES'] = dict(P=np.concatenate(P), N=np.concatenate(N), U=np.concatenate(U), T=np.concatenate(T), TM=np.concatenate(TM))
    return ridge


def row_parts():
    objs, mats = load_complex('Sajeongjeon')
    if ORIG_FRONT:
        orig_front(objs, mats)
    dropm = np.array([any(k in m['name'] for k in Y.DROP_MAT) for m in mats])
    parts = {k: [] for k in ('BAY', 'GATE', 'END_W', 'END_E', 'BACK')}
    for nm, o in objs.items():
        if not (nm in ROW_OBJS or nm == 'ORIG_TILES' or nm.startswith('SM_SJM_Door') or nm.startswith('SM_SJJ_HG_Door')):
            continue
        keep = ~dropm[o['TM']]
        o = dict(o, T=o['T'][keep], TM=o['TM'][keep])
        lo, hi = Y.clip_plane(o, 2, ZROW)
        a, r1 = Y.clip_plane(lo, 0, XG0)
        g, r2 = Y.clip_plane(r1, 0, XG1)
        b, _ = Y.clip_plane(r2, 0, XB1)
        wW, m1 = Y.clip_plane(hi, 0, XW)
        m2, wE = Y.clip_plane(m1, 0, XE)
        for k, v in (('END_W', a), ('GATE', g), ('BAY', b), ('END_E', r2), ('END_W', wW), ('END_E', wE), ('BACK', m2)):
            if len(v['T']):
                parts[k].append(v)
    return parts, mats


def merge(plist, mats, x0):
    """조각 목록 → 메시 하나(재질마다 서브메시, 서브메시마다 정점 따로), 조각 좌표로"""
    P = []; N = []; U = []; S = []; M = []
    by = {}
    for o in plist:
        for m in np.unique(o['TM']):
            by.setdefault(int(m), []).append((o, o['T'][o['TM'] == m]))
    base = 0
    for m in sorted(by, key=lambda k: mats[k]['name']):
        for o, t in by[m]:
            used, inv = np.unique(t.ravel(), return_inverse=True)
            P.append(o['P'][used]); N.append(o['N'][used]); U.append(o['U'][used])
            S.append((m, inv.reshape(-1, 3) + base)); base += len(used)
    P = np.concatenate(P) - np.array([x0, GROUND, ZF]); N = np.concatenate(N); U = np.concatenate(U)
    subs = []; names = []
    for m in sorted({k for k, _ in S}, key=lambda k: mats[k]['name']):
        subs.append(np.concatenate([t for k, t in S if k == m]).reshape(-1).astype(np.uint32)); names.append(mats[m]['name'])
    return dict(pos=P.astype(np.float32), nrm=N.astype(np.float32), uv=U.astype(np.float32), subs=subs, mats=names)


def simplify_locked(pos, nrm, uv, subs, lock, tol, w_uv, permissive):
    idx = np.concatenate(subs).astype(np.uint32)
    attr = np.ascontiguousarray(np.concatenate([nrm, uv], axis=1).astype(np.float32))
    wts = np.array([0.2, 0.2, 0.2, w_uv, w_uv], np.float32)
    dst = np.empty(len(idx), np.uint32); err = ctypes.c_float(0)
    lk = np.ascontiguousarray(lock.astype(np.uint8))
    opt = pbsimp.ErrorAbsolute | (PERMISSIVE if permissive else 0)
    n = pbsimp.LIB.meshopt_simplifyWithAttributes(dst.ctypes.data, idx.ctypes.data, len(idx), np.ascontiguousarray(pos, np.float32).ctypes.data, len(pos), 12,
                                                  attr.ctypes.data, 20, wts.ctypes.data, 5, lk.ctypes.data, 0, ctypes.c_float(tol), opt, ctypes.byref(err))
    out = dst[:n].reshape(-1, 3)
    vsub = np.empty(len(pos), np.int32)
    for k, s in enumerate(subs):
        vsub[s] = k
    k_of = vsub[out[:, 0]] if len(out) else np.zeros(0, np.int32)
    return [out[k_of == k].reshape(-1) for k in range(len(subs))]


def lod(mesh, cuts, level):
    tol, w_uv, perm = TOL[level]
    subs0 = [np.asarray(s, np.uint32) for s in mesh['subs']]
    pos, nrm, uv, subs = weld(mesh['pos'], mesh['nrm'], mesh['uv'], subs0, use_normals=True)
    lock = np.zeros(len(pos), bool)
    for c in cuts:
        lock |= np.abs(pos[:, 0] - c) < 2e-4
    subs = simplify_locked(pos, nrm, uv, subs, lock, tol, w_uv, perm)
    pos, nrm, uv, subs = compact(pos, nrm, uv, subs)
    keep = [k for k in range(len(subs)) if len(subs[k])]
    return dict(pos=pos, nrm=nrm, uv=uv, subs=[subs[k] for k in keep], mats=[mesh['mats'][k] for k in keep])


def tris(m):
    return int(sum(len(s) for s in m['subs']) // 3)


if __name__ == '__main__':
    from impcx import OUTROOT
    od = os.path.join(OUTROOT, 'YJR'); os.makedirs(od, exist_ok=True)
    parts, mats = row_parts()
    # 줄 끝 END_W/END_E 조각은 2026-09-30 부터 안 씀(ㄱ자 모서리 yj_corner.py 로 바꿈)
    spec = {'BAY': (XG1, [0.0, XB1 - XG1]), 'GATE': (XG0, [0.0, XG1 - XG0])}
    out = []; info = {}
    for k, (x0, cuts) in spec.items():
        m0 = merge(parts[k], mats, x0)
        L = [m0, lod(m0, cuts, 1)]
        for i, m in enumerate(L):
            out.append(('YJR_%s_LOD%d' % (k, i), m))
        lo = m0['pos'].min(0); hi = m0['pos'].max(0)
        info[k] = dict(x0=x0, length=(cuts[-1] if len(cuts) > 1 else None), tris=[tris(m) for m in L], bounds=[lo.tolist(), hi.tolist()], mats=len(m0['mats']))
        print(k, info[k])
    write_meshes(os.path.join(od, 'YJR_Modules.bin'), out)
    json.dump(dict(modules=info, zrow=ZROW, xw=XW, xe=XE, ground=GROUND, zfront=ZF), open(os.path.join(od, 'modules.json'), 'w'), indent=1)
    print('written', os.path.join(od, 'YJR_Modules.bin'))
