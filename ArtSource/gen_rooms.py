# -*- coding: utf-8 -*-
"""
새 저택 구조(지도)에 생긴 방들의 데모 배경. 기획에서 진짜 배경을 뽑아 주기 전까지 쓰는 자리 채움입니다.

    python ArtSource/gen_rooms.py

출력: Assets/_Project/Resources/Art/Backgrounds/
    bg_dining.png            1층 식당
    bg_garden.png            3층 정원
    bg_corridor_2..5.png     2~5층 복도 (벽에 층 번호)
    bg_basement.png          지하실 — 엔딩 장소. 그 밤을 재현해 둔 방 (케이크, 3시 14분)

gen_art.py 의 도구와 색을 그대로 써서 기존 플레이스홀더 배경과 톤을 맞춥니다.
진짜 배경이 오면 같은 이름으로 덮어쓰고, 핫스팟 좌표(Hotspots 탭)를 그림에 맞춰 다시 잡으세요.
"""
import math, os
from PIL import ImageDraw, ImageFont

import gen_art as ga
from gen_art import (W, H, FLOOR, INK0, INK1, INK2, INK3, INK4, AMBER, AMBER2, TEAL, TEAL2, CRIM, PAPER,
                     canvas, vgrad, glow, poly, rect, rrect, ellipse, ring, line, vignette, grain, room_base,
                     wainscot, framed_picture, finish, S)

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Project", "Resources", "Art", "Backgrounds")
FONT = r"C:\Windows\Fonts\malgunbd.ttf"


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    finish(img, W, H).save(os.path.join(OUT, name + ".png"))
    print(f"  Backgrounds/{name}.png")


def bg_dining():
    img = room_base((40, 30, 32), (26, 20, 24), (34, 26, 24))
    wainscot(img, color=(46, 32, 30))
    # 창 두 개와 촛대 샹들리에
    for x in (420, 1500):
        rect(img, (x - 130, 170, x + 130, 470), (22, 26, 44))
        rect(img, (x - 6, 170, x + 6, 470), (60, 44, 40))
        rect(img, (x - 130, 314, x + 130, 326), (60, 44, 40))
    line(img, [(960, 0), (960, 150)], 4, (90, 76, 60))
    rrect(img, (840, 150, 1080, 176), 10, AMBER2)
    for k in range(5):
        x = 860 + k * 50
        rect(img, (x - 5, 120, x + 5, 152), PAPER)
        img = glow(img, x, 112, 60, AMBER, 0.55)
    # 긴 식탁 — 의자 여덟 개
    poly(img, [(380, 640), (1540, 640), (1640, 780), (280, 780)], (92, 60, 44))
    poly(img, [(280, 780), (1640, 780), (1640, 806), (280, 806)], (64, 42, 32))
    rect(img, (320, 806, 344, 920), (64, 42, 32))
    rect(img, (1576, 806, 1600, 920), (64, 42, 32))
    for k in range(4):
        x = 520 + k * 300
        rrect(img, (x - 50, 520, x + 50, 640), 12, (70, 46, 38))     # 뒤쪽 의자 등받이
        rrect(img, (x - 60, 840, x + 60, 980), 12, (58, 38, 32))     # 앞쪽 의자
    for x in (620, 960, 1300):                                       # 접시
        ellipse(img, (x - 60, 680, x + 60, 720), (214, 208, 196))
    return vignette(grain(img), 0.74)


def bg_garden():
    img = canvas(W, H, INK0 + (255,))
    vgrad(img, (0, 0, W, 560), (18, 22, 40), (34, 40, 60))            # 유리 천장 너머 밤하늘
    for x in range(0, W + 1, 240):                                   # 온실 유리틀
        line(img, [(x, 0), (x, 560)], 4, (60, 70, 90))
    line(img, [(0, 280), (W, 280)], 4, (60, 70, 90))
    ellipse(img, (1500, 70, 1600, 170), (230, 226, 210))              # 달
    img = glow(img, 1550, 120, 220, (200, 210, 230), 0.4)
    vgrad(img, (0, 560, W, H), (30, 44, 34), (16, 24, 20))           # 잔디
    # 마른 화단과 나무
    for cx in (330, 1580):
        rect(img, (cx - 20, 360, cx + 20, 640), (60, 44, 34))
        for r, dy in ((150, 300), (120, 220), (90, 160)):
            ellipse(img, (cx - r, dy - r * 0.7, cx + r, dy + r * 0.7), (36, 52, 40))
    rrect(img, (560, 700, 1360, 820), 30, (54, 40, 32))
    for k in range(9):
        x = 620 + k * 85
        line(img, [(x, 700), (x - 10, 640)], 5, (90, 80, 60))
        ellipse(img, (x - 24, 610, x + 4, 640), (110, 70, 80))       # 시든 꽃
    # 가운데 해시계 — 그림자가 멈춰 있다
    rect(img, (930, 600, 990, 700), (110, 106, 100))
    ellipse(img, (880, 570, 1040, 620), (140, 136, 128))
    line(img, [(960, 594), (1010, 580)], 6, (60, 58, 56))
    return vignette(grain(img), 0.70)


def bg_corridor(floor):
    img = canvas(W, H, INK0 + (255,))
    vgrad(img, (0, 0, W, H), (26, 30, 46), (12, 15, 26))
    cx, cy = 960, 520
    poly(img, [(0, H), (W, H), (cx + 220, cy + 140), (cx - 220, cy + 140)], (28, 24, 26))
    poly(img, [(0, 0), (W, 0), (cx + 220, cy - 150), (cx - 220, cy - 150)], (20, 24, 38))
    poly(img, [(0, 0), (cx - 220, cy - 150), (cx - 220, cy + 140), (0, H)], (30, 34, 52))
    poly(img, [(W, 0), (cx + 220, cy - 150), (cx + 220, cy + 140), (W, H)], (24, 28, 44))
    rect(img, (cx - 220, cy - 150, cx + 220, cy + 140), (16, 19, 32))
    # 양옆 문 — 이 층의 두 방
    for side in (-1, 1):
        x0 = cx + side * 420
        poly(img, [(x0, cy - 120), (x0 + side * 160, cy - 170), (x0 + side * 160, cy + 230), (x0, cy + 160)],
             (52, 36, 30))
        ellipse(img, (x0 + side * 120 - 8, cy + 40, x0 + side * 120 + 8, cy + 56), AMBER)
    # 복도 끝 계단
    for k in range(6):
        y = cy + 140 - k * 34
        rect(img, (cx - 140 + k * 12, y - 34, cx + 140 - k * 12, y), (40 - k * 3, 36 - k * 3, 46 - k * 3))
    # 층 번호
    d = ImageDraw.Draw(img)
    d.text((S(cx), S(cy - 250)), f"{floor}F", font=ImageFont.truetype(FONT, S(120)), fill=AMBER2 + (255,), anchor="mm")
    for x in (420, 1500):
        rect(img, (x - 10, 210, x + 10, 260), AMBER2)
        img = glow(img, x, 220, 170, AMBER, 0.45)
    return vignette(grain(img), 0.78)


def bg_basement():
    img = room_base((22, 20, 26), (14, 12, 16), (20, 16, 16))
    for y in (120, 180):                                             # 천장 배관
        line(img, [(0, y), (W, y)], 14, (44, 40, 40))
    # 멈춘 벽시계 3:14
    cx, cy, r = 1460, 300, 90
    ellipse(img, (cx - r, cy - r, cx + r, cy + r), (210, 200, 180))
    ring(img, (cx - r, cy - r, cx + r, cy + r), 8, (70, 54, 40))
    for ang, ln, wd in (((3 + 14 / 60) * 30, 48, 10), (14 * 6, 74, 6)):
        a = math.radians(ang - 90)
        line(img, [(cx, cy), (cx + ln * math.cos(a), cy + ln * math.sin(a))], wd, INK0)
    # 그 밤의 식탁 — 케이크와 초
    poly(img, [(600, 640), (1320, 640), (1400, 760), (520, 760)], (90, 64, 50))
    rect(img, (560, 760, 586, 900), (60, 42, 34))
    rect(img, (1334, 760, 1360, 900), (60, 42, 34))
    ellipse(img, (820, 560, 1100, 640), (236, 220, 206))
    rect(img, (820, 520, 1100, 600), (236, 214, 200))
    ellipse(img, (820, 490, 1100, 550), (246, 232, 222))
    for k in range(7):
        x = 860 + k * 34
        rect(img, (x - 6, 470, x + 6, 512), (200, 150, 170))
        if k != 2:                                                   # 일곱 자리 중 하나는 꺼져 있다
            ellipse(img, (x - 6, 452, x + 6, 470), AMBER)
            img = glow(img, x, 458, 50, AMBER, 0.5)
    img = glow(img, 960, 520, 520, AMBER, 0.28)
    return vignette(grain(img), 0.86)


if __name__ == "__main__":
    save(bg_dining(), "bg_dining")
    save(bg_garden(), "bg_garden")
    for f in (2, 3, 4, 5):
        save(bg_corridor(f), f"bg_corridor_{f}")
    save(bg_basement(), "bg_basement")
