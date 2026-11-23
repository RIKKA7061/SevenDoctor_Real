# -*- coding: utf-8 -*-
"""
손그림 방 배경을 게임 배경(1920x1080)으로 맞춥니다.

    py -3.14 ArtSource/make_rooms_art.py

원본은 sheets/ 에 두고, 16:9 보다 넓으면 좌우를, 높으면 위아래를 가운데 기준으로 잘라 맞춥니다.
늘려서 맞추면 그림이 찌그러집니다.
"""
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
BG = os.path.join(HERE, "..", "Assets", "_Project", "Resources", "Art", "Backgrounds")
SIZE = (1920, 1080)

# 원본 → 배경키
ROOMS = {
    "greed_room.webp": "bg_storage",      # 탐욕의 방
    "arrogance_room.webp": "bg_guest",    # 오만의 방
}


def fit(src):
    target = SIZE[0] / SIZE[1]
    w, h = src.size
    if w / h > target:
        nw = round(h * target)
        box = ((w - nw) // 2, 0, (w - nw) // 2 + nw, h)
    else:
        nh = round(w / target)
        box = (0, (h - nh) // 2, w, (h - nh) // 2 + nh)
    return src.crop(box).resize(SIZE, Image.LANCZOS), box


for name, key in ROOMS.items():
    img, box = fit(Image.open(os.path.join(HERE, "sheets", name)).convert("RGB"))
    img.save(os.path.join(BG, key + ".png"))
    print(f"{name} → {key}.png  (원본 잘라낸 곳 {box})")
