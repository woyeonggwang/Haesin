"""창·문·홍살을 판으로 바꿀 대상 고르기(5개 단지 데모 씬 데이터 기준).
kind: 'full'  = 메시 전체를 판으로(창·문·홍살·창살 붙은 창틀)
      'split' = 창틀·벽 메시 안 창살 서브메시만 판으로(틀·벽은 메시로 남김)
      'acc'   = 문 쇠붙이(손잡이·자물쇠·빗장) — 붙은 문 판에 구워 넣을 후보
mode: 'paper' = 틀 안 빈 곳은 흰 창호지, 'solid' = 판문(빈 곳은 옆 색으로 메움), 'clear' = 홍살(빈 곳 투명)
style: 'flat' = 앞·뒤 판 2장, 'box' = 여는 문짝(옆면까지 6면), 'down' = 천장(아래 보는 판 1장)"""
import re, collections
import numpy as np

CARD_KEYS = ['window', 'changho', 'munsal', 'gyeokja', 'salchang', 'door', 'hongsal']
EXCL = ['doorframe', 'door_frame', 'frame', 'handle', 'iron', 'ornament', 'hinge', 'lock', 'knob', 'paper', 'nodoors', 'shadow']
FULL_FRAMES = {'SM_HNM_Windowframe_A', 'SM_HNM_Windowframe_B'}   # 틀과 창살이 한 재질·한 메시
ACC_KEYS = ['doorhandle', 'doorlock', 'doorwoodlock']
CEIL_KEYS = ['ceiling', 'celling', 'banja']
GATE_LEAF_PREFIX = ['SM_HNM_Door_', 'SM_GCM_Door0', 'SM_YCM_Door0', 'SM_SMM_Door0', 'SM_HSM_Door', 'SM_YSM_Door']

def is_gate_leaf(n):
    l = n.lower()
    if 'frame' in l or 'handle' in l or 'lock' in l:
        return False
    return any(n.startswith(p) for p in GATE_LEAF_PREFIX)

def size_ok(ext):
    a = sorted(ext)
    return a[0] <= 0.3 and a[1] >= 0.2 and a[2] <= 7.0

def classify(ms, inst_mats):
    """ms: read_bin 결과, inst_mats: 메시 번호 -> 서브메시 재질 이름 목록. 반환: {k: dict(kind, mode, style, subs)}"""
    out = {}
    lattice_mats = set()
    for k, mats in inst_mats.items():
        n = ms[k]['name']; l = n.lower()
        P = ms[k]['pos']; ext = P.max(0) - P.min(0)
        if any(a in l for a in ACC_KEYS):
            out[k] = dict(kind='acc'); continue
        if any(a in l for a in CEIL_KEYS):
            # 천장(반자·우물천장·홍예 천장): 아래에서 올려다본 그림 한 장(아래 보는 면만), 빈 곳은 투명
            if ext[1] <= 1.5 and ext[1] <= min(ext[0], ext[2]):
                out[k] = dict(kind='full', mode='clear', style='down', subs=list(range(len(ms[k]['subs']))))
            continue
        full = n in FULL_FRAMES or (any(c in l for c in CARD_KEYS) and not any(e in l for e in EXCL))
        if full and size_ok(ext):
            mode = 'clear' if 'hongsal' in l else 'auto'
            style = 'box' if is_gate_leaf(n) else 'flat'
            out[k] = dict(kind='full', mode=mode, style=style, subs=list(range(len(ms[k]['subs']))))
            # 창틀 속 창살 재질 = 창·(여닫이 아닌) 문 메시의 주 재질(쇠붙이 재질은 뺌)
            if n not in FULL_FRAMES and not is_gate_leaf(n) and 'hongsal' not in l:
                lattice_mats.update(m for m in mats if m and not any(x in m.lower() for x in ('metal', 'iron')))
    for k, mats in inst_mats.items():
        if k in out:
            continue
        n = ms[k]['name']; l = n.lower()
        if 'frame' not in l:
            continue
        subs = [s for s, m in enumerate(mats) if m in lattice_mats and len(ms[k]['subs'][s])]
        if subs:
            out[k] = dict(kind='split', mode='paper', style='flat', subs=subs)
    return out, lattice_mats
