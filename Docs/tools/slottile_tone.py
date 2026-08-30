# SlotTile.png 금색 장식 톤 다운.
# 빈 슬롯 타일 15칸을 5x3으로 깔면 밝은 금색 필리그리가 60개 흩뿌려져 보드가 시끄럽다
# (HISTORY 2026-08-30 미해결 ②). 금색 픽셀만 채도·명도를 낮춰 차분하게 만든다.
# 돌 판·테두리 회색은 건드리지 않는다.
#
# 입력: 임포트된 SlotTile.png (원본은 git에 있으므로 되돌리기 쉬움)
# 출력: 같은 경로에 덮어쓰기
#
# usage: python Docs/tools/slottile_tone.py [in.png] [out.png]
import sys
import numpy as np
from PIL import Image

DESAT = 0.55   # 금색을 자기 밝기 쪽으로 이만큼 당겨 채도를 뺀다
DARKEN = 0.82  # 그 뒤 이만큼 어둡게

src = sys.argv[1] if len(sys.argv) > 1 else "Assets/_Project/Art/Stage/SlotTile.png"
dst = sys.argv[2] if len(sys.argv) > 2 else src

im = Image.open(src).convert("RGBA")
a = np.asarray(im).astype(np.float32)
rgb, alpha = a[:, :, :3], a[:, :, 3]
r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]

# 금색 가중치: 따뜻하고(r-b) 불투명한 픽셀일수록 1에 가깝게, 경계가 딱딱하지 않도록 완만하게
warm = np.clip((r - b - 15.0) / 40.0, 0.0, 1.0)
weight = warm * np.clip(alpha / 255.0, 0.0, 1.0)
weight = weight[:, :, None]

lum = (0.299 * r + 0.587 * g + 0.114 * b)[:, :, None]
toned = ((rgb * (1.0 - DESAT) + lum * DESAT) * DARKEN)

out = rgb * (1.0 - weight) + toned * weight
a[:, :, :3] = np.clip(out, 0.0, 255.0)

Image.fromarray(a.astype(np.uint8), "RGBA").save(dst)

gold_px = int((weight[:, :, 0] > 0.3).sum())
print("toned %d gold-ish px -> %s" % (gold_px, dst))
