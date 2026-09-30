"""흥례문 1층 천장 판(SM_HNM_PatternCeilingA 판)의 계단 구멍 메우기.

판 텍스처(400 px/m)에서 계단 구멍(알파 < 128 인 큰 덩어리)과 그 둘레 틀(가로 106 px, 세로 3 px)을
같은 무늬가 반복되는 옆 자리(가로 주기 192.5 px × 4 = 770 px 왼쪽)에서 그대로 복사해 채운다.
주기는 무늬의 자기상관으로 잼(가로 192.5 px, 세로 약 207.3 px).
원본은 work/cards/..._orig.png 로 보관, 결과는 work/cards/<같은 이름>.png
"""
import os, shutil
import numpy as np
from PIL import Image
from scipy import ndimage

_HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(_HERE, '..', 'out_cards', 'Heungnyemun', 'Cards', 'HNM_149_SM_HNM_PatternCeilingA_card.png')
WORK = os.path.join(_HERE, 'work', 'cards')


def run():
    os.makedirs(WORK, exist_ok=True)
    bak = os.path.join(WORK, 'HNM_149_SM_HNM_PatternCeilingA_card_orig.png')
    if not os.path.exists(bak):
        shutil.copy2(SRC, bak)
    a = np.array(Image.open(bak).convert('RGBA'))
    lab, n = ndimage.label(a[..., 3] < 128)
    sz = ndimage.sum(np.ones(lab.shape), lab, range(1, n + 1)); k = int(np.argmax(sz)) + 1
    ys, xs = np.where(lab == k)
    x0, x1, y0, y1 = xs.min() - 106, xs.max() + 106, ys.min() - 3, ys.max() + 3
    dx = -770
    blk = a[y0:y1 + 1, x0 + dx:x1 + 1 + dx].copy()
    assert (blk[..., 3] >= 128).all(), '복사할 자리에 구멍이 있음'
    b = a.copy(); b[y0:y1 + 1, x0:x1 + 1] = blk
    Image.fromarray(b).save(os.path.join(WORK, 'HNM_149_SM_HNM_PatternCeilingA_card.png'), optimize=True)
    return (x0, x1, y0, y1)


if __name__ == '__main__':
    print(run())
