# -*- coding: utf-8 -*-
"""
슬픔 포트레이트 — 캐릭터 시트(ArtSource/sheets/sorrow_sheet.webp)에서 뽑습니다.

    python ArtSource/make_sorrow.py

시트에는 전신이 정면 한 장뿐이고, 오른쪽에 머리만 세 각도(정면·3/4·옆얼굴)가 있습니다.
그래서 몸은 그대로 두고 머리만 갈아 끼워 '고개를 돌린' 그림을 만듭니다:

    sorrow.png              정면 — 전신 그대로
    sorrow_side_left.png    3/4 얼굴 (왼쪽을 봄)
    sorrow_back_left.png    옆얼굴 (더 돌아섬)
    *_right                 머리만 좌우 반전 (몸까지 뒤집으면 벨트·주머니 자리가 바뀝니다)

뒷모습(back)은 시트에 없습니다. PortraitView 가 가장 가까운 방향으로 대신합니다.

머리 갈아 끼우기:
  1) 턱선(CUT) 위의 옛 머리를 지웁니다.
  2) 턱선 아래 어깨에 걸쳐 있던 옛 머리카락 끝도 지우고, 그 자리에 드러난 옷 가장자리에
     먹선을 새로 긋습니다 — 원래 머리카락이 덮고 있어서 선이 없던 자리입니다.
  3) 새 머리를 안경알 크기에 맞춰 키우고(HEAD_SCALE), 턱을 몸의 목 위에 맞춰 얹습니다.

좌표는 전부 시트 픽셀입니다. 시트를 바꾸면 BODY_BOX / HEADS / 기준점을 다시 잡으세요.
캔버스 배치는 cut_turnaround.py 와 같은 규칙이라 다른 인물과 크기·바닥선이 맞습니다.
"""
import os
import numpy as np
from PIL import Image, ImageOps

import cut_turnaround as ct

ROOT = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(ROOT, "sheets", "sorrow_sheet.webp")
KEY = "sorrow"

BODY_BOX = (0, 0, 460, 1024)       # 왼쪽 전신 (오른쪽 설명 그림은 안 들어가게)
BODY_NECK = (230, 370)             # 몸의 턱 아래 / 목깃 위 가운데
CUT = 366                          # 이 줄 위의 옛 머리를 지웁니다
TIP_BAND = 60                      # 턱선 아래 이만큼에서 옛 머리카락 끝을 찾습니다
HEAD_SCALE = 1.2                   # 오른쪽 머리들은 전신보다 작게 그려져 있습니다 (안경알 기준)
HEADS = {                          # 방향: (시트에서 자를 칸, 그 머리의 턱/목 기준점)
    "side_left": ((650, 368, 1012, 668), (795, 642)),
    "back_left": ((694, 660, 1012, 1000), (835, 948)),
}
INK = (34, 28, 30)


def cut_piece(sheet, box):
    """배경을 지운 조각 하나. 가운데 줄에서 이어진 것만 남겨 옆 그림 부스러기를 버립니다."""
    rgb = np.asarray(sheet.crop(box))
    lum = ct.luminance(rgb.astype(np.float32))
    bg = ct.find_background(rgb)
    fg = ~bg
    top, bottom = ct.figure_rows(fg)
    seed = np.zeros_like(fg)
    seed[top + (bottom - top) // 2, :] = True
    fg = ct.spread(fg & seed, fg)
    color, alpha = ct.soft_alpha(rgb, fg, float(np.median(lum[bg])))
    return Image.fromarray(np.dstack([color, alpha * 255]).clip(0, 255).astype(np.uint8), "RGBA"), fg


def headless(body):
    """머리를 떼어 낸 몸. 어깨에 남은 머리카락 끝을 지우고 옷 가장자리에 선을 긋습니다."""
    a = np.asarray(body).astype(np.int16).copy()
    a[:CUT, :, 3] = 0

    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    lum = r * 0.299 + g * 0.587 + b * 0.114
    hairish = (al > 0) & ((((b - r) > 40) & (b > 90)) | (lum < 110))   # 파란 머리카락 + 그 외곽선
    band = np.zeros(hairish.shape, bool)
    band[CUT:CUT + TIP_BAND, :] = True
    seed = np.zeros_like(band)
    seed[CUT, :] = True
    tips = ct.spread(hairish & seed & band, hairish & band)
    a[tips, 3] = 0

    # 가시처럼 남은 한두 픽셀짜리 조각을 걷어 냅니다 (띠 안에서만 열림 연산).
    opaque = a[..., 3] > 128
    shrunk = opaque.copy()
    for _ in range(2):
        shrunk &= ~ct.grow(~shrunk)
    opened = shrunk.copy()
    for _ in range(2):
        opened = ct.grow(opened) & opaque
    spikes = opaque & ~opened & band
    a[spikes, 3] = 0

    # 새로 드러난 가장자리에 먹선
    opaque = a[..., 3] > 128
    exposed = ct.grow(tips | spikes)
    for _ in range(3):
        exposed = ct.grow(exposed)
    edge = opaque & ct.grow(~opaque) & exposed
    for _ in range(2):
        edge = ct.grow(edge) & opaque & exposed
    a[edge, 0:3] = INK
    return Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")


def with_head(base, sheet, box, anchor, mirror=False):
    head, _ = cut_piece(sheet, box)
    ax = anchor[0] - box[0]
    if mirror:
        head = ImageOps.mirror(head)
        ax = head.width - ax
    head = head.resize((round(head.width * HEAD_SCALE), round(head.height * HEAD_SCALE)), Image.LANCZOS)
    dx = round(BODY_NECK[0] - ax * HEAD_SCALE)
    dy = round(BODY_NECK[1] - (anchor[1] - box[1]) * HEAD_SCALE)
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    layer.paste(head, (dx, dy))
    out = base.copy()
    out.alpha_composite(layer)
    return out


def place(piece, fg):
    """cut_turnaround 과 같은 배치 — 캔버스 높이의 FILL 만큼, 발은 바닥 여백 위, 다리 기준 가운데."""
    top, bottom = ct.figure_rows(fg)
    scale = (ct.CH * ct.FILL) / (bottom - top)
    cut = fg[top:bottom]
    low = cut[int(cut.shape[0] * 0.6):, :]
    cx = np.nonzero(low)[1].mean()

    def put(img):
        img = img.crop((0, top, img.width, bottom))
        w, h = round(img.width * scale), round(img.height * scale)
        img = img.resize((w, h), Image.LANCZOS)
        canvas = Image.new("RGBA", (ct.CW, ct.CH), (0, 0, 0, 0))
        canvas.paste(img, (round(ct.CW / 2 - cx * scale), ct.CH - ct.BOTTOM_MARGIN - h))
        return canvas
    return put


def main():
    sheet = Image.open(SHEET).convert("RGB")
    body, fg = cut_piece(sheet, BODY_BOX)
    put = place(body, fg)

    made = {"front": put(body)}
    base = headless(body)
    for pose, (box, anchor) in HEADS.items():
        made[pose] = put(with_head(base, sheet, box, anchor))
        if pose.endswith("_left"):
            made[pose[:-len("_left")] + "_right"] = put(with_head(base, sheet, box, anchor, mirror=True))

    os.makedirs(ct.OUT, exist_ok=True)
    for pose, img in made.items():
        name = KEY if pose == "front" else f"{KEY}_{pose}"
        img.save(os.path.join(ct.OUT, name + ".png"))
        print(f"  Portraits/{name}.png")


if __name__ == "__main__":
    main()
