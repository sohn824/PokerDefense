# CardFront.png 마감용 후처리.
# 생성기가 카드의 "평탄 불투명 크림 면"을 신뢰성 있게 못 뽑아서 (HISTORY 2026-08-28~29),
# AI 결과에서 금 테두리·모서리 플로리시·사각 윤곽만 살리고 안쪽을 코드로 채운다.
#
# 입력: AI가 생성한 CardFront 원본 (금 테두리는 깨끗하고 가운데만 망가진 것)
# 출력: 같은 경로에 덮어쓰기 — 안쪽 평탄 크림 #F3EEDF·알파 255, 금색 채도 낮춤
#
# usage: python Docs/tools/cardfront_composite.py <in.png> [out.png]
import sys, colorsys
import numpy as np
from PIL import Image
from scipy import ndimage

src = sys.argv[1]
dst = sys.argv[2] if len(sys.argv) > 2 else src
CREAM = np.array([243, 238, 223], np.uint8)   # #F3EEDF

im = Image.open(src).convert("RGBA")
a = np.array(im).astype(np.int16)
H, W = a.shape[:2]
alpha = a[:, :, 3]
R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]

# 1) 카드 실루엣 = alpha>40의 최대 연결 덩어리 + 구멍 메우기
soft = alpha > 40
lbl, n = ndimage.label(soft)
soft = lbl == (np.argmax(ndimage.sum(soft, lbl, range(1, n + 1))) + 1)
card = ndimage.binary_fill_holes(soft)
ys, xs = np.where(card)
x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()

# 2) 금 픽셀 = 따뜻하고 밝고 불투명, 그리고 가장자리 밴드 or 모서리 사각 안쪽만
gold = (R > 120) & (G > 80) & (R - B > 50) & (alpha > 120) & card
dist = ndimage.distance_transform_edt(card)
band = dist < (0.085 * W)
cs = int(0.24 * W)
corners = np.zeros((H, W), bool)
corners[y0:y0 + cs, x0:x0 + cs] = corners[y0:y0 + cs, x1 - cs:x1] = True
corners[y1 - cs:y1, x0:x0 + cs] = corners[y1 - cs:y1, x1 - cs:x1] = True
keep = ndimage.binary_closing(gold & (band | corners), iterations=2)

# 3) 금색 채도 낮춤 — HSV S*0.55, V*0.88, hue 살짝 오렌지로
gpx = np.stack([R[keep], G[keep], B[keep]], 1).astype(float) / 255.0
hsv = np.array([colorsys.rgb_to_hsv(*p) for p in gpx])
hsv[:, 1] *= 0.55
hsv[:, 2] *= 0.88
hsv[:, 0] = np.clip(hsv[:, 0] * 0.96, 0, 1)
gold_new = np.clip(np.array([colorsys.hsv_to_rgb(*h) for h in hsv]) * 255, 0, 255).astype(np.uint8)

# 4) 합성: 카드 전체를 평탄 크림·불투명 → 살린 금 픽셀 덮어쓰기 → 바깥 1~2px는 원본 알파로 AA 복원
out = np.zeros((H, W, 4), np.uint8)
out[..., 3] = np.where(card, 255, 0)
out[card, 0:3] = CREAM
out[keep, 0:3] = gold_new
out[keep, 3] = 255
edge = card & ~ndimage.binary_erosion(card, iterations=2)
out[edge, 3] = np.minimum(alpha[edge], 255).astype(np.uint8)

Image.fromarray(out).save(dst)

o = np.array(Image.open(dst)); oa = o[:, :, 3]
inA = oa[int(H * .14):int(H * .86), int(W * .18):int(W * .82)]
cc = oa[int(H * .4):int(H * .6), int(W * .4):int(W * .6)]
cream = o[:, :, :3][card & ~keep & (oa > 250)]
print(f"{dst}: {W}x{H}  interior alpha>=250 {(inA >= 250).mean() * 100:.1f}%  "
      f"centre alpha>=250 {(cc >= 250).mean() * 100:.1f}%  cream std {cream.std(0).round(2)}")
