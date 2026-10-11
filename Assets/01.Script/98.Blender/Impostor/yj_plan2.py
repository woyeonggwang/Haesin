"""육조거리 행랑 방식 배치 계획(2026-09-30 사용자 지시 "멀쩡한 벽 한칸이랑 문을 떼가지고 그걸 계속 일자로 이어붙이세요",
서울역사박물관 육조거리 복원 모형처럼 길 양옆에 이어진 행랑 + 문, 그 뒤에 관아).

줄(길 한쪽) = END_W · GATE · BAY×n1 · GATE · BAY×n2 · GATE · END_E (조각은 yj_row.py).
줄 끝 관아 두 채 = 사정전 틀(END 조각에 바깥 옆 행각이 원래 메시로 들어 있고, 안쪽 옆 행각 HG_W/HG_E 는 임포스터),
가운데 관아 = 다른 틀(교태전 / 강녕전 행각 + 사정전 전각). 관아 안쪽은 PalaceYukjoTool.BuildRest.
서쪽 줄: 회전 270(조각 로컬 +x → 북), 정면 x -2766, 남쪽 끝 z 3502 부터 북으로. 동쪽 줄: 회전 90(로컬 +x → 남), 정면 x -2724, 북쪽 끝 z 3844 부터 남으로.
결과: out/yj_row.tsv(조각), out/yj_front.tsv(관아 안쪽), out/yj_rest_kits.json(BuildRest 명세), out/yj_plan2.png
"""
import json, os
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')
L_BAY, L_GATE = 6.075, 22.083
ENDW_MIN, ENDE_MAX = -45.6726, 40.7355          # 조각 로컬 x 범위(yj_row modules.json)
PIV_A = 8.573                                   # GATE 시작에서 사정전 틀 기준점(원래 x 0.62)까지
PIV_SJM = 8.003                                 # GATE 시작에서 사정문 가운데(원래 x 0.05)까지
N1, N2 = 16, 15
WEST = dict(x=-2766.0, z0=3502.0, dirz=+1, rot=270, kits=('YJ_R_AB_S', 'YJ_R_C', 'YJ_R_AB2_N'))
EAST = dict(x=-2724.0, z0=3844.0, dirz=-1, rot=90, kits=('YJ_R_A_N', 'YJ_R_BA', 'YJ_R_AB3_S'))
# BuildRest 명세: 형식:오브젝트:dx:dz(형식 기준점 기준, 정면 처마 끝 = z 0). 줄 끝 관아의 안쪽 옆 행각 = HG_E(END_W 쪽 관아) / HG_W(END_E 쪽 관아)
KA = 'A:Keep/SM_MCJ_FoundationStone_Keep:0:{d};A:Keep/SJC_MCJ_Keep:0:{d};A:Keep/SJC_CCJ_Keep:0:{d}'
REST = {
    'YJ_R_AB_S': 'A:HG_E:0:0;B:GNJ:0:-4.5;B:GNJC_GSJ:0:3.5;B:GNJC_YSJ:0:3.5',
    'YJ_R_AB2_N': 'A:HG_W:0:0;A:SJC_SJJ:0:0;B:GNJC_EJD:0:-8;B:GNJC_YGD:0:-8',
    'YJ_R_A_N': 'A:HG_E:0:0;A:SJC_SJJ:0:0;A:SJC_MCJ:0:0;A:SJC_CCJ:0:0;' + KA.format(d=0),
    'YJ_R_AB3_S': 'A:HG_W:0:0;A:SJC_SJJ:0:0;B:GNJC_GSJ:0:3.5;B:GNJC_YSJ:0:3.5',
    'YJ_R_C': 'C:GTJ_GTJ:0:0;C:GTJ_GSG:0:0;C:GTJ_HHG:0:0;C:GTJ_WGH:0:0;C:Keep/GTJ_GSJ_Keep:0:0',
    'YJ_R_BA': 'B:GNJC_GNJ_HG:0:0;A:SJC_SJJ:0:4;B:GNJC_EJD:0:0;B:GNJC_YGD:0:0;A:SJC_MCJ:0:-6;A:SJC_CCJ:0:-6;' + KA.format(d=-6),
}
# 관아 기준점 = 틀 기준점. 가운데 관아는 사정문 가운데에 맞춤
MID_PIVOT = {'YJ_R_C': PIV_SJM, 'YJ_R_BA': PIV_SJM}


def row(side):
    mods = []; comp = []
    s = -ENDW_MIN
    mods.append(('YJR_END_W', s))
    mods.append(('YJR_GATE', s)); comp.append((side['kits'][0], s + PIV_A))
    s += L_GATE
    for _ in range(N1):
        mods.append(('YJR_BAY', s)); s += L_BAY
    mods.append(('YJR_GATE', s)); comp.append((side['kits'][1], s + MID_PIVOT[side['kits'][1]]))
    s += L_GATE
    for _ in range(N2):
        mods.append(('YJR_BAY', s)); s += L_BAY
    mods.append(('YJR_GATE', s)); comp.append((side['kits'][2], s + PIV_A))
    s += L_GATE
    mods.append(('YJR_END_E', s))
    total = s + ENDE_MAX
    W = lambda sv: (side['x'], side['z0'] + side['dirz'] * sv)
    return [(k, *W(sv), side['rot']) for k, sv in mods], [(k, *W(sv), side['rot']) for k, sv in comp], total


if __name__ == '__main__':
    rows = []; comps = []
    for side in (WEST, EAST):
        m, c, total = row(side)
        rows += m; comps += c
        print('row', side['rot'], 'modules', len(m), 'length %.2f' % total, 'z %.2f..%.2f' % (side['z0'], side['z0'] + side['dirz'] * total))
        for k, x, z, r in c:
            print('   ', k, 'pivot z %.2f' % z)
    with open(os.path.join(OUT, 'yj_row.tsv'), 'w', encoding='utf-8') as f:
        f.write('# 행랑 조각\tx\tz\t회전 — yj_plan2.py\n')
        for k, x, z, r in rows:
            f.write('%s\t%.3f\t%.3f\t%d\t1\t1\t1\n' % (k, x, z, r))
    with open(os.path.join(OUT, 'yj_front.tsv'), 'w', encoding='utf-8') as f:
        f.write('# 관아 안쪽\tx\tz\t회전 — yj_plan2.py\n')
        for k, x, z, r in comps:
            f.write('%s\t%.3f\t%.3f\t%d\t1\t1\t1\n' % (k, x, z, r))
    json.dump(REST, open(os.path.join(OUT, 'yj_rest_kits.json'), 'w', encoding='utf-8'), indent=1)
    print(len(rows), 'modules', len(comps), 'compounds')
