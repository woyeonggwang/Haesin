"""건춘문·영추문·신무문 단순화판(가까이 = 원형 유지 감면 + 판, 멀리 = 예전 구운 덩어리) 만들기.

흥례문(hnm/build_lods.py)과 같은 HNMS 형식으로 내보내되, 먼 LOD 는 새로 만들지 않고 Unity 의
<단지>_Complex 프리팹에 있는 구운 덩어리(<덩어리>_Baked 의 먼 단계: 석재·창호·목부 + 지붕 껍데기)를 그대로 가져다 씀
(layout.txt 의 COPYFAR 줄 — PalaceLodBinImport.BuildPrefab 이 처리).

부분 = 구운 덩어리와 같은 묶음: 메시 이름의 건물 코드(SM_<코드>_…) → 덩어리 이름. 덩어리 목록에 없는 작은 코드는 SMALL.
빼는 것: 데모 바닥(Plane), removed.tsv 의 쇠붙이(문짝 판에 구워 넣음), 여는 문짝(문짝 판 프리팹으로 따로).
거울 배치(행렬식 < 0)는 삼각형 감김을 뒤집고, 법선은 역전치로 옮김(비균등 크기 배치가 많음).
출력: out/<단지>/meshes.bin, doors.bin, layout.txt, cards.tsv, Cards/*.png, stats.tsv
입력(작업 폴더 기준): data/<단지>/(instances.tsv·materials.tsv), out/<단지>/reduced.bin, out_cards/<단지>/(cards.bin·cards.tsv·removed.tsv·Cards/)
  = 장치의 D:\Blender\Enviroment\PalaceJoseon\FourDoor\Source\<단지>\ 에 있는 같은 이름 파일들.
주의: PalaceBinExport 는 서브메시 수만큼만 재질을 적음(렌더러에 남는 재질 슬롯은 빠짐). 영추문 SM_YCM_Door02A 는
  Unity 에서 남는 슬롯(MI_GCM_Wood01)이 널판 위에 그려져 붉게 보이는데 데이터에는 MI_GCM_Metal01 만 있어 판이 검게 구워졌음 →
  같은 메시인 건춘문 SM_GCM_Door02A 판 그림(GCM_137)을 YCM_145 로 복사해 씀(2026-09-30). 판을 다시 구우면 다시 복사할 것.
"""
import sys, os, re, json, shutil
import numpy as np
_HERE = os.path.dirname(os.path.abspath(__file__))
for _d in ('hnm', 'Heungnyemun'):   # 흥례문 공용 모듈(load·export·scene): 작업 폴더 이름 hnm / Unity 98.Blender 폴더 이름 Heungnyemun
    if os.path.isdir(os.path.join(_HERE, '..', _d)):
        sys.path.insert(0, os.path.join(_HERE, '..', _d))
sys.path.insert(0, os.path.join(_HERE, '..'))
import load as L
from export import finalize, write_meshes
from scene import instance_mats

ROOT = os.path.join(_HERE, '..')
CHUNKS = {'Geonchunmun': ['GCM', 'GSB', 'SMALL', 'SMJC'], 'Yeongchumun': ['SMJC', 'YCM'], 'Sinmumun': ['SMALL', 'SMJC', 'SMM']}
LEAF_PREFIXES = ('sm_gcm_door0', 'sm_ycm_door0', 'sm_smm_door0')
UNITY_BAKED = 'Assets/02.Resources/01.Asset/Models/Gyeongbokgung/_Baked'
D_LOD0 = 200.0          # 가까운 모델을 쓰는 거리(부분 중심에서)
H_CULL = 0.002          # 구운 덩어리와 같은 끝 화면 높이
MAX_TRIS = 300000       # 메시 하나 최대 삼각형(가져오기 메모리)
# 먼 LOD 로 구운 덩어리를 쓰지 않는 부분(구운 것이 오히려 더 무거움: 건춘문 SMALL 구운 24,778 > 줄인 10,276 삼각형)
NO_FAR = {('Geonchunmun', 'SMALL')}
# 부분을 건물 코드 대신 자리(데모 좌표 x 구간)로 나누는 단지: (이름, x 최소, x 최대, 가까운 모델 거리 m).
# 영추문 데모에는 문 말고도 궁 안쪽 문 남쪽(데모 +x)에 건물 3채가 있어, 코드별로 묶으면 문 앞에서도 100 m 넘게 떨어진
# ㄱ자 두 채까지 가까운 모델로 그렸음(LOD0 의 66 %) → 건물마다 부분을 나누고 세 채는 약 70 m 안에서만 가까운 모델(사용자 선택 2026-09-30).
# 경계는 배치 경계 상자 사이 빈틈 가운데: 문(+ 북쪽 작은 건물) x ≤ −31.0 | T자 −14.3~19.1 | ㄱ자 46.5~64.0 | ㄱ자 66.0~80.8 + 일영대 84.6~91.5
REGIONS = {'Yeongchumun': [('GATE', -1e5, -23.0, 200.0), ('BLD_T', -23.0, 33.0, 70.0),
                           ('BLD_L1', 33.0, 65.0, 70.0), ('BLD_L2', 65.0, 1e5, 70.0)]}


def code_of(name):
    p = name.split('_')
    if p[0] != 'SM' or len(p) < 2:
        return 'MISC'
    code = p[1]
    if len(p) > 2 and p[2] in ('HG', 'IN'):
        code += '_' + p[2]
    if code.endswith('_IN'):
        code = code[:-3]
    return code


def is_leaf(name):
    l = name.lower()
    if 'frame' in l or 'handle' in l or 'lock' in l:
        return False
    return l.startswith(LEAF_PREFIXES)


def is_ground(name):
    return name == 'Plane' or name.startswith('Plane') or 'skysphere' in name.lower()


def split_tris(pieces, max_tris):
    """pieces = [(W, N, UV, tt, mn, flat)] → 삼각형 무게중심을 가장 긴 축으로 나눠 max_tris 이하 묶음 목록"""
    total = sum(len(p[3]) for p in pieces)
    if total <= max_tris:
        return [pieces]
    C = np.concatenate([p[0][p[3]].mean(1) for p in pieces])
    ext = C.max(0) - C.min(0); ax = int(np.argmax([ext[0], 0, ext[2]]))
    k = int(np.ceil(total / max_tris))
    edges = np.quantile(C[:, ax], np.linspace(0, 1, k + 1)[1:-1])
    groups = [[] for _ in range(k)]
    for (W, N, UV, tt, mn, flat) in pieces:
        c = W[tt].mean(1)[:, ax]
        g = np.searchsorted(edges, c)
        for j in range(k):
            s = tt[g == j]
            if len(s):
                groups[j].append((W, N, UV, s, mn, flat))
    return [g for g in groups if g]


def merge(pieces):
    groups = {}
    for (W, N, UV, tt, mn, flat) in pieces:
        groups.setdefault((mn, flat), []).append((W, N, UV, tt))
    P = []; NN = []; U = []; subs = []; mats = []; flats = []; base = 0
    for (mn, flat), lst in sorted(groups.items(), key=lambda kv: (kv[0][0], kv[0][1])):
        idx = []
        for (W, N, UV, tt) in lst:
            used, inv = np.unique(tt.ravel(), return_inverse=True)
            P.append(W[used]); NN.append(N[used]); U.append(UV[used])
            idx.append(inv.reshape(-1).astype(np.int64) + base); base += len(used)
        subs.append(np.concatenate(idx)); mats.append(mn); flats.append(flat)
    return dict(pos=np.concatenate(P), nrm=np.concatenate(NN), uv=np.concatenate(U).astype(np.float64), subs=subs, mats=mats, flats=flats)


def run(cx, log=print):
    L.CX = cx
    inst, mats, red = L.load()
    out = os.path.join(_HERE, 'out', cx); os.makedirs(os.path.join(out, 'Cards'), exist_ok=True)
    removed = set(int(l.split('\t')[0]) for l in open(os.path.join(ROOT, 'out_cards', cx, 'removed.tsv'), encoding='utf-8').read().splitlines() if l.strip())
    chunks = CHUNKS[cx]
    regs = REGIONS.get(cx)
    order = [r[0] for r in regs] if regs else chunks
    parts = {c: [] for c in order}
    doors = {}
    skipped = {'ground': 0, 'removed': 0, 'leaf': 0}
    for ii, i in enumerate(inst):
        d = red[i['k']]; nm = d['name'].split('#')[0]
        if is_ground(nm):
            skipped['ground'] += 1; continue
        if ii in removed:
            skipped['removed'] += 1; continue
        if is_leaf(nm):
            skipped['leaf'] += 1
            if nm not in doors:
                doors[nm] = d
            continue
        M = i['M']; A = M[:3, :3]
        W = L.xform(M, d['pos'].astype(np.float64))
        if regs:
            # 자리로 나눔: 배치 경계 상자 가운데 x 가 든 구간
            xc = (W[:, 0].min() + W[:, 0].max()) / 2
            part = next(r[0] for r in regs if r[1] <= xc < r[2])
        else:
            c = code_of(nm)
            part = c if c in parts else 'SMALL'
            if part not in parts:
                raise RuntimeError('덩어리 없음: %s %s' % (cx, nm))
        N = d['nrm'].astype(np.float64) @ np.linalg.inv(A)
        N = N / np.maximum(np.linalg.norm(N, axis=1), 1e-12)[:, None]
        flat = d['name'].endswith('#F') or d['name'].endswith('#CARD')
        mir = np.linalg.det(A) < 0
        mnames = instance_mats(i, d)
        for s, idx in enumerate(d['subs']):
            t = np.asarray(idx).reshape(-1, 3).astype(np.int64)
            if len(t) == 0:
                continue
            if mir:
                t = t[:, [0, 2, 1]]
            mn = mnames[s] if s < len(mnames) else ''
            parts[part].append((W, N, d['uv'], t, mn, flat))
    meshes = []; layout = ['# %s 단순화판: PART 이름 기준점 크기 LOD0→1 (안 씀) 끝 / MESH LOD 메시 [collider] / COPYFAR LOD 프리팹 오브젝트'
                           ' / COPYFARBOX LOD 프리팹 오브젝트 x최소 x최대 z최소 z최대(루트 좌표, 삼각형 무게중심으로 자름)' % cx,
                           'PREFAB\t%s_Simplified' % cx]
    stats = ['part\tmeshes\tlod0_tris\tsize\tref\tlod0_dist']
    used_mats = set()
    complex_prefab = '%s/%s/%s_Complex.prefab' % (UNITY_BAKED, cx, cx)
    for c in order:
        pieces = parts[c]
        if not pieces:
            log(cx, c, 'empty'); continue
        groups = split_tris(pieces, MAX_TRIS)
        names = []; tris = 0; lo = np.full(3, 1e9); hi = np.full(3, -1e9)
        for gi, g in enumerate(groups):
            m = finalize(merge(g))
            nm = '%s_%s_LOD0%s' % (cx, c, '' if len(groups) == 1 else '_%d' % gi)
            meshes.append((nm, m)); names.append(nm); used_mats |= set(m['mats'])
            tris += sum(len(s) for s in m['subs']) // 3
            lo = np.minimum(lo, m['pos'].min(0)); hi = np.maximum(hi, m['pos'].max(0))
        ref = (lo + hi) / 2; size = float((hi - lo).max())
        reg = next(r for r in regs if r[0] == c) if regs else None
        dist = reg[3] if reg else D_LOD0
        h0 = min(size / (dist * 1.1547), 0.95)
        layout.append('PART\t%s_%s\t%.4f\t%.4f\t%.4f\t%.3f\t%.5f\t%.5f\t%.5f' % (cx, c, ref[0], ref[1], ref[2], size, h0, h0 * 0.5, H_CULL))
        for nm in names:
            layout.append('MESH\t0\t%s\tcollider' % nm)
        if reg:
            # 먼 LOD = 구운 덩어리 전부에서 이 구간(x) 안의 삼각형만(구운 덩어리는 코드별로 합쳐져 있어 건물별로 잘라 씀)
            for c2 in chunks:
                layout.append('COPYFARBOX\t1\t%s\t%s_Baked\t%.3f\t%.3f\t%.3f\t%.3f' % (complex_prefab, c2, reg[1], reg[2], -1e5, 1e5))
        elif (cx, c) not in NO_FAR:
            layout.append('COPYFAR\t1\t%s\t%s_Baked' % (complex_prefab, c))
        stats.append('%s\t%d\t%d\t%.1f\t%s\t%.0f' % (c, len(names), tris, size, ','.join('%.1f' % x for x in ref), dist))
        log('%s %-6s meshes %d tris %d size %.1f h0 %.3f (LOD0 ~%.0f m)' % (cx, c, len(names), tris, size, h0, dist))
    layout.append('DOORS\t%s_DoorsCard' % cx)
    write_meshes(os.path.join(out, 'meshes.bin'), meshes)
    dl = []
    for nm, d in sorted(doors.items()):
        r = d.get('card_row')
        if r is None:
            raise RuntimeError('문짝 판 없음: ' + nm)
        m = dict(pos=d['pos'].astype(np.float64), nrm=d['nrm'].astype(np.float64), uv=d['uv'].astype(np.float64),
                 subs=[np.asarray(s, np.int64) for s in d['subs']], mats=[r[5]] * len(d['subs']), flats=[True] * len(d['subs']))
        dl.append((nm, finalize(m))); used_mats.add(r[5])
    write_meshes(os.path.join(out, 'doors.bin'), dl)
    rows = ['material\ttexture\tclip']
    for d in red:
        r = d.get('card_row')
        if r and r[5] in used_mats:
            shutil.copy2(os.path.join(ROOT, 'out_cards', cx, 'Cards', r[6]), os.path.join(out, 'Cards', r[6]))
            rows.append('%s\t%s\t%s' % (r[5], r[6], r[7]))
    open(os.path.join(out, 'cards.tsv'), 'w', encoding='utf-8').write('\n'.join(rows) + '\n')
    open(os.path.join(out, 'layout.txt'), 'w', encoding='utf-8').write('\n'.join(layout) + '\n')
    open(os.path.join(out, 'stats.tsv'), 'w', encoding='utf-8').write('\n'.join(stats) + '\n')
    log(cx, 'doors', sorted(doors), 'skipped', skipped, 'cards', len(rows) - 1)
    return out


if __name__ == '__main__':
    for cx in (sys.argv[1:] or list(CHUNKS)):
        run(cx)
