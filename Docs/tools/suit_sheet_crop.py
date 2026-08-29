# Suit_Sheet.png (2x2 무늬 시트) → Suit_Spade/Heart/Diamond/Club.png 4장.
# 4등분 + 알파 트림 + 네 심볼 bbox 높이 통일(정규화) + 256 캔버스 중앙 배치.
#
# 시트 셀 순서: TL=Spade, TR=Heart, BL=Diamond, BR=Club (ART_REQUEST §4 프롬프트 레이아웃)
#
# usage: python Docs/tools/suit_sheet_crop.py <Suit_Sheet.png> <out_dir>
import sys, os
import numpy as np
from PIL import Image

sheet_path, out_dir = sys.argv[1], sys.argv[2]
OUT = 256
TARGET_H = int(OUT * 0.82)   # 209px — 네 심볼 공통 cap-height
CELLS = {"Spade": (0, 0), "Heart": (0, 1), "Diamond": (1, 0), "Club": (1, 1)}

src = Image.open(sheet_path).convert("RGBA")
H, W = np.array(src).shape[:2]
hh, hw = H // 2, W // 2

def trim(img):
    al = np.array(img)[:, :, 3]
    ys, xs = np.where(al > 16)
    return img.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))

for name, (r, c) in CELLS.items():
    sym = trim(src.crop((c * hw, r * hh, (c + 1) * hw, (r + 1) * hh)))
    w, h = sym.size
    nw = max(1, round(w * TARGET_H / h))
    sym = sym.resize((nw, TARGET_H), Image.LANCZOS)
    maxw = OUT - 24                       # 폭이 넘치면 12px 여백 남기고 캡
    if sym.width > maxw:
        sym = sym.resize((maxw, max(1, round(sym.height * maxw / sym.width))), Image.LANCZOS)
    canvas = Image.new("RGBA", (OUT, OUT), (0, 0, 0, 0))
    canvas.alpha_composite(sym, ((OUT - sym.width) // 2, (OUT - sym.height) // 2))
    p = os.path.join(out_dir, f"Suit_{name}.png")
    canvas.save(p)
    v = np.array(canvas); va = v[:, :, 3]
    ys, xs = np.where(va > 16)
    fill = tuple(np.median(v[:, :, :3][va > 200], axis=0).astype(int))
    print(f"{p}: bbox {xs.max()-xs.min()+1}x{ys.max()-ys.min()+1}  fill {fill}  aMax {va.max()}")
