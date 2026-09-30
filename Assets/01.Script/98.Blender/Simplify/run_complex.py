"""단지(데모 씬) 하나의 고유 메시를 모두 줄인다: meshopt 로 허용 오차 후보별 결과를 만들고,
14방향 평행 광선 비교(깊이·텍스처 색·맞음/안 맞음, 허용 오차만큼의 가장자리 이동은 봐줌)를 통과한 것 중 가장 많이 줄인 것을 고른다.
출력: out/<cx>/reduced.bin(원본과 같은 형식, 같은 메시 순서, 이름 끝 '#F' = 각진 음영 — 받는 쪽에서 면 법선으로),
      out/<cx>/report.tsv
사용: python3 run_complex.py <cx> [시작번호]"""
import sys, json, time, os, math
import numpy as np
from pbsimp import read_bin, write_bin, flat_fraction, weld, simplify, compact
from rayeval import RMesh, compare2

cx = sys.argv[1]
DATA = 'data/%s' % cx
TEX = '/mnt/user-data/uploads/Haesin/Temp/PalaceSimplify/tex/'
OUT = 'out/%s' % cx + os.environ.get('PB_OUT_SUFFIX', '')
os.makedirs(OUT, exist_ok=True)
TOLS = [0.002, 0.004, 0.008, 0.015]
MAX_ERR = 0.002          # 광선 오류 비율(맞음/안 맞음 + 깊이 + 색) 한도
GAIN_MIN = 0.05          # 더 큰 허용 오차가 삼각형을 5 % 이상 더 줄일 때만 그쪽을 고름
W_UV, W_N = 100.0, 0.2
NRM_COS = math.cos(math.radians(15))   # 음영 법선이 15° 넘게 달라지면 오류로 셈
OUT_SUFFIX = os.environ.get('PB_OUT_SUFFIX', '')

matmap = json.load(open(TEX + '%s_matmap.json' % cx, encoding='utf-8'))
inst_mats = {}; inst_M = {}
for line in open(DATA + '/instances.tsv', encoding='utf-8'):
    p = line.rstrip('\n').split('\t'); k = int(p[0])
    M = np.array([float(x) for x in p[2:18]]).reshape(4, 4)
    inst_mats.setdefault(k, p[18].split('|')); inst_M.setdefault(k, []).append(M)
ms = read_bin(DATA + '/meshes.bin')

allmin = []; per_min = {}; smax = {}
for k, Ms in inst_M.items():
    d = ms[k]; bmin = d['pos'].min(0); bmax = d['pos'].max(0)
    corners = np.array([[x, y, z, 1.0] for x in (bmin[0], bmax[0]) for y in (bmin[1], bmax[1]) for z in (bmin[2], bmax[2])])
    ys = [float((M @ corners.T)[1].min()) for M in Ms]
    per_min[k] = min(ys); allmin += ys
    smax[k] = max(float(np.linalg.norm(M[:3, :3], axis=0).max()) for M in Ms)
ground = float(np.percentile(allmin, 5))

texcache = {}
def tex_for(mname):
    fn = matmap.get(mname)
    if not fn:
        return None
    if fn not in texcache:
        texcache[fn] = np.load(TEX + fn)
    return texcache[fn]

def rmesh(pos, uv, subs, vn=None):
    tris = np.concatenate([s.reshape(-1, 3) for s in subs]).astype(np.int32) if subs else np.zeros((0, 3), np.int32)
    tmat = np.concatenate([np.full(len(s) // 3, k) for k, s in enumerate(subs)]) if subs else np.zeros(0, np.int32)
    return RMesh(pos, tris, uv, tmat, vn)

results = [None] * len(ms)
start = int(sys.argv[2]) if len(sys.argv) > 2 else 0
rep = open(OUT + '/report.tsv', 'a' if start else 'w', encoding='utf-8')
if not start:
    rep.write('idx\tname\tinst\theight\tsmax\tflat\ttris0\ttol\ttris1\terr\tnote\n')
t_all = time.time()
for k, d in enumerate(ms):
    if k < start:
        continue
    t0 = time.time()
    subs0 = [s.astype(np.uint32) for s in d['subs']]
    tris0 = sum(len(s) for s in subs0) // 3
    name = d['name']
    if k not in inst_M or name.endswith('_Ground') or name == 'Plane' or tris0 <= 12:
        # 쓰이지 않거나 데모 바닥이거나 아주 작은 메시는 그대로
        results[k] = dict(name=name, pos=d['pos'], nrm=d['nrm'], uv=d['uv'], subs=subs0)
        rep.write('%d\t%s\t%d\t\t\t\t%d\t0\t%d\t0\tkeep\n' % (k, name, len(inst_M.get(k, [])), tris0, tris0)); continue
    h = per_min[k] - ground
    cap = 0.004 if h < 2.5 else (0.008 if h < 5.0 else 0.015)
    tols = [t for t in TOLS if t <= cap + 1e-9]
    ff = flat_fraction(d); flat = ff >= 0.9
    pos, nrm, uv, S = weld(d['pos'], d['nrm'], d['uv'], subs0, use_normals=not flat)
    texs = {i: tex_for(n) for i, n in enumerate(inst_mats[k])}
    RO = rmesh(d['pos'], d['uv'], [s.astype(np.int32) for s in subs0], d['nrm'])
    passed = []   # (tol, tris, err, res)
    for tw in tols:
        tl = tw / smax[k]
        res, rerr = simplify(pos, nrm, uv, S, tl, W_UV, 0.0 if flat else W_N)
        nt = sum(len(s) for s in res) // 3
        if passed and nt >= passed[-1][1]:
            passed.append((tw, nt, passed[-1][2], res)); continue
        RD = rmesh(pos, uv, [s.astype(np.int32) for s in res], None if flat else nrm)
        r = compare2(RO, RD, texs, tl, nrm_cos=NRM_COS)
        RD.free()
        e = r['miss'] + r['depth'] + r['color'] + r['shade']
        if e > MAX_ERR:
            break
        passed.append((tw, nt, e, res))
    if not passed:
        # 2 mm 도 안 되면 1 mm 시도, 그것도 안 되면 원본(합친 정점)
        tl = 0.001 / smax[k]
        res, rerr = simplify(pos, nrm, uv, S, tl, W_UV, 0.0 if flat else W_N)
        RD = rmesh(pos, uv, [s.astype(np.int32) for s in res], None if flat else nrm); r = compare2(RO, RD, texs, tl, nrm_cos=NRM_COS); RD.free()
        e = r['miss'] + r['depth'] + r['color'] + r['shade']
        if e <= MAX_ERR:
            passed.append((0.001, sum(len(s) for s in res) // 3, e, res))
    RO.free()
    if passed:
        best = passed[0]
        for cand in passed[1:]:
            if cand[1] <= best[1] * (1 - GAIN_MIN):
                best = cand
        tw, nt, e, res = best
        note = ''
    else:
        tw, nt, e, res = 0.0, tris0, 0.0, S
        note = 'orig'
    p2, n2, u2, s2 = compact(pos, nrm, uv, res)
    if flat:
        n2 = np.zeros_like(n2)
    results[k] = dict(name=name + ('#F' if flat else ''), pos=p2, nrm=n2, uv=u2, subs=s2)
    rep.write('%d\t%s\t%d\t%.2f\t%.2f\t%.2f\t%d\t%.3f\t%d\t%.4f\t%s\n' % (k, name, len(inst_M[k]), h, smax[k], ff, tris0, tw, nt, e, note))
    rep.flush()
    print('%3d %-34s inst %3d h %5.2f flat %.2f tris %7d -> %7d tol %.3f err %.4f %.1fs' % (k, name[:34], len(inst_M[k]), h, ff, tris0, nt, tw, e, time.time() - t0), flush=True)
rep.close()
if all(r is not None for r in results):
    write_bin(OUT + '/reduced.bin', results)
    print('WROTE', OUT + '/reduced.bin', os.path.getsize(OUT + '/reduced.bin'), 'total %.0fs' % (time.time() - t_all))
else:
    np.save(OUT + '/partial_%d.npy' % start, np.array([0]))
    print('PARTIAL (restart needed for full file)')
