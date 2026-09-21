# -*- coding: utf-8 -*-
"""
SevenDoctors 플레이스홀더 아트 생성기.

아트 담당이 실제 리소스를 그리기 전까지 게임이 '보이게' 하기 위한 것.
전부 코드로 그리므로 색·비율·구도를 숫자만 바꿔 몇 번이든 다시 뽑을 수 있습니다.

출력:
  Backgrounds/*.png   1920x1080   (Rooms 탭 '배경키'와 파일명이 일치)
  Characters/*.png     700x1200   (Characters 탭 '포트레이트키'와 일치) — 몸
  Faces/*.png          700x1200   (Dialogues 탭 '표정'과 일치) — 표정 합본 (폴백)
  Faces/{표정}/*.png                부위별 레이어 — 눈 깜빡임·시선·립싱크용
  Faces/faceparts.json              부위가 캔버스 어디에 있었는지 기록
  Evidence/*.png       512x512    (Evidence 탭 '아이콘키'와 일치)

표정을 몸과 분리한 이유: 일곱 박사는 전부 같은 얼굴이라는 게 설정이라서,
얼굴 한 세트를 전원이 공유하는 게 구조적으로도 맞고 리소스도 1/8 로 줄어듭니다.
"""
import json, math, os, random
from PIL import Image, ImageDraw, ImageFilter

random.seed(7)

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "out")
SS = 2  # 슈퍼샘플링 배율 (그린 뒤 축소해서 계단 제거)

# ── 팔레트 ────────────────────────────────────────────────────────────────────
INK0   = (10, 13, 22)
INK1   = (19, 26, 43)
INK2   = (30, 39, 64)
INK3   = (44, 55, 82)
INK4   = (62, 74, 104)
AMBER  = (216, 178, 106)
AMBER2 = (138, 110, 62)
TEAL   = (62, 107, 114)
TEAL2  = (96, 158, 160)
CRIM   = (122, 51, 64)
PAPER  = (232, 228, 216)
RUST   = (150, 92, 58)

ACCENTS = {
    "doctor": (120, 170, 180),
    "sloth":  (108, 118, 140),
    "lust":   (176, 78, 96),
    "sorrow": (84, 108, 164),
    "wrath":  (186, 86, 52),
    "envy":   (96, 150, 104),
    "greed":  (196, 160, 72),
    "pride":  (136, 96, 168),
}


# ── 그리기 헬퍼 ───────────────────────────────────────────────────────────────

def canvas(w, h, color=(0, 0, 0, 0)):
    return Image.new("RGBA", (w * SS, h * SS), color)

def finish(img, w, h):
    return img.resize((w, h), Image.LANCZOS)

def S(v):
    """좌표를 슈퍼샘플 배율로 변환."""
    if isinstance(v, (list, tuple)):
        return type(v)(S(x) for x in v)
    return int(round(v * SS))

def vgrad(img, box, top, bottom):
    """세로 그라디언트를 사각 영역에 채웁니다."""
    x0, y0, x1, y1 = S(box)
    h = max(1, y1 - y0)
    d = ImageDraw.Draw(img)
    for i in range(h):
        t = i / h
        c = tuple(int(top[k] + (bottom[k] - top[k]) * t) for k in range(3))
        d.line([(x0, y0 + i), (x1, y0 + i)], fill=c + (255,))

def glow(img, cx, cy, radius, color, strength=1.0):
    """부드러운 원형 광원을 더합니다."""
    r = S(radius)
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    steps = 26
    for i in range(steps, 0, -1):
        t = i / steps
        rr = int(r * t)
        a = int(255 * strength * (1 - t) ** 2.1 * 0.55)
        d.ellipse([S(cx) - rr, S(cy) - rr, S(cx) + rr, S(cy) + rr], fill=color + (a,))
    layer = layer.filter(ImageFilter.GaussianBlur(r * 0.10))
    return Image.alpha_composite(img, layer)

def poly(img, pts, color, alpha=255):
    ImageDraw.Draw(img, "RGBA").polygon([S(p) for p in pts], fill=color + (alpha,))

def rect(img, box, color, alpha=255):
    ImageDraw.Draw(img, "RGBA").rectangle(S(box), fill=color + (alpha,))

def rrect(img, box, radius, color, alpha=255):
    ImageDraw.Draw(img, "RGBA").rounded_rectangle(S(box), S(radius), fill=color + (alpha,))

def ellipse(img, box, color, alpha=255):
    ImageDraw.Draw(img, "RGBA").ellipse(S(box), fill=color + (alpha,))

def ring(img, box, width, color, alpha=255):
    ImageDraw.Draw(img, "RGBA").ellipse(S(box), outline=color + (alpha,), width=S(width))

def line(img, pts, width, color, alpha=255):
    ImageDraw.Draw(img, "RGBA").line([S(p) for p in pts], fill=color + (alpha,), width=S(width))

def vignette(img, strength=0.72):
    w, h = img.size
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)
    pad = int(min(w, h) * 0.10)
    d.ellipse([-pad, -pad, w + pad, h + pad], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(min(w, h) * 0.13))
    dark = Image.new("RGBA", (w, h), INK0 + (int(255 * strength),))
    inv = Image.eval(mask, lambda v: 255 - v)
    dark.putalpha(inv)
    return Image.alpha_composite(img, dark)

def grain(img, amount=7):
    w, h = img.size
    n = Image.new("L", (w // 3, h // 3))
    n.putdata([random.randint(128 - amount, 128 + amount) for _ in range((w // 3) * (h // 3))])
    n = n.resize((w, h), Image.BILINEAR)
    layer = Image.merge("RGBA", (n, n, n, Image.new("L", (w, h), 26)))
    return Image.blend(img, Image.alpha_composite(img, layer), 0.55)

def save(img, folder, name, w, h):
    path = os.path.join(OUT, folder)
    os.makedirs(path, exist_ok=True)
    finish(img, w, h).save(os.path.join(path, name + ".png"))
    print(f"  {folder}/{name}.png")


# ── 배경 공통 뼈대 ────────────────────────────────────────────────────────────
W, H = 1920, 1080
FLOOR = 760  # 바닥선 y

def room_base(wall_top, wall_bottom, floor_color):
    img = canvas(W, H, INK0 + (255,))
    vgrad(img, (0, 0, W, FLOOR), wall_top, wall_bottom)
    vgrad(img, (0, FLOOR, W, H), floor_color, tuple(max(0, c - 14) for c in floor_color))
    line(img, [(0, FLOOR), (W, FLOOR)], 3, tuple(min(255, c + 22) for c in wall_bottom))
    return img

def wainscot(img, y=FLOOR, height=90, color=INK2):
    rect(img, (0, y - height, W, y), color)
    line(img, [(0, y - height), (W, y - height)], 3, tuple(min(255, c + 26) for c in color))

def framed_picture(img, cx, cy, w, h, frame=AMBER2, inner=INK1, plate=None):
    rect(img, (cx - w / 2 - 9, cy - h / 2 - 9, cx + w / 2 + 9, cy + h / 2 + 9), frame)
    rect(img, (cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2), inner)
    # 흐릿한 인물 실루엣
    ellipse(img, (cx - w * 0.17, cy - h * 0.30, cx + w * 0.17, cy + h * 0.04), INK3)
    poly(img, [(cx - w * 0.30, cy + h / 2), (cx - w * 0.22, cy - h * 0.02),
               (cx + w * 0.22, cy - h * 0.02), (cx + w * 0.30, cy + h / 2)], INK3)
    if plate is not None:
        rect(img, (cx - 46, cy + h / 2 + 16, cx + 46, cy + h / 2 + 34), plate)


# ── 방별 배경 ────────────────────────────────────────────────────────────────

def bg_lab():
    img = room_base((22, 34, 46), (14, 22, 32), (18, 26, 34))
    # 뒤쪽 모니터 벽
    for i in range(6):
        x = 150 + i * 145
        rect(img, (x, 210, x + 108, 300), (26, 44, 54))
        rect(img, (x + 8, 218, x + 100, 292), TEAL, 120)
        for j in range(4):
            line(img, [(x + 16, 232 + j * 16), (x + 16 + random.randint(30, 78), 232 + j * 16)], 3, TEAL2, 200)
    # 중앙 타임머신
    cx, cy = 1180, 470
    img = glow(img, cx, cy, 420, TEAL2, 0.85)
    poly(img, [(cx - 240, FLOOR), (cx - 150, FLOOR - 70), (cx + 150, FLOOR - 70), (cx + 240, FLOOR)], INK1)
    for r, wdt, a in ((300, 16, 255), (240, 10, 220), (176, 7, 190)):
        ring(img, (cx - r, cy - r, cx + r, cy + r), wdt, (46, 74, 84), a)
    ring(img, (cx - 300, cy - 300, cx + 300, cy + 300), 5, TEAL2, 150)
    ellipse(img, (cx - 66, cy - 66, cx + 66, cy + 66), TEAL2, 230)
    ellipse(img, (cx - 34, cy - 34, cx + 34, cy + 34), (200, 240, 245), 240)
    # 케이블
    for i in range(7):
        x0 = cx - 260 + i * 86
        pts = [(x0, FLOOR - 60)]
        for k in range(1, 7):
            pts.append((x0 - 40 - k * 26 + random.randint(-14, 14), FLOOR - 60 + k * 26))
        line(img, pts, 6, INK2)
    # 작업대
    rect(img, (140, 600, 520, 620), INK3)
    rect(img, (156, 620, 178, FLOOR), INK2)
    rect(img, (482, 620, 504, FLOOR), INK2)
    rect(img, (200, 566, 260, 600), (36, 50, 60))
    rect(img, (300, 578, 400, 600), PAPER, 90)
    return vignette(grain(img))

def bg_front():
    img = canvas(W, H, INK0 + (255,))
    vgrad(img, (0, 0, W, H), (16, 20, 38), (8, 10, 20))
    # 달
    img = glow(img, 1470, 210, 360, (226, 216, 180), 0.55)
    ellipse(img, (1410, 150, 1530, 270), (232, 224, 196), 240)
    # 구름띠
    for i, y in enumerate((300, 380, 470)):
        rect(img, (0, y, W, y + 26), (30, 36, 60), 60 - i * 12)
    # 저택 실루엣
    base = 860
    poly(img, [(430, base), (430, 430), (620, 300), (810, 430), (810, base)], INK1)
    poly(img, [(810, base), (810, 380), (1080, 220), (1350, 380), (1350, base)], (16, 21, 36))
    poly(img, [(1350, base), (1350, 450), (1500, 340), (1650, 450), (1650, base)], INK1)
    # 탑
    poly(img, [(1040, 230), (1080, 120), (1120, 230)], (13, 17, 30))
    # 창문
    for (x, y) in ((880, 440), (980, 440), (1180, 440), (1280, 440), (930, 570), (1230, 570)):
        rect(img, (x, y, x + 54, y + 78), AMBER, 200)
        img = glow(img, x + 27, y + 39, 90, AMBER, 0.42)
    # 현관
    rect(img, (1040, 640, 1120, base), (9, 12, 22))
    img = glow(img, 1080, 650, 150, AMBER, 0.35)
    # 지면 + 안개
    vgrad(img, (0, base, W, H), (14, 18, 30), (9, 11, 20))
    for i in range(4):
        y = base + 10 + i * 46
        rect(img, (0, y, W, y + 30), (60, 72, 100), 26 - i * 5)
    # 철책
    for i in range(26):
        x = 40 + i * 76
        rect(img, (x, 790, x + 8, 980), (14, 18, 30))
    rect(img, (0, 800, W, 812), (14, 18, 30))
    return vignette(grain(img), 0.8)

def bg_living():
    img = room_base((44, 36, 34), (26, 22, 24), (34, 26, 24))
    wainscot(img, FLOOR, 110, (40, 30, 28))
    # 벽난로
    fx = 1360
    rect(img, (fx - 180, 300, fx + 180, FLOOR), (30, 24, 24))
    rect(img, (fx - 120, 430, fx + 120, FLOOR - 30), (12, 9, 10))
    img = glow(img, fx, 640, 330, (226, 140, 66), 0.9)
    poly(img, [(fx - 70, FLOOR - 30), (fx - 20, 540), (fx + 10, 590), (fx + 60, 520), (fx + 70, FLOOR - 30)],
         (232, 152, 72), 220)
    rect(img, (fx - 200, 282, fx + 200, 306), AMBER2)
    # 초상화
    framed_picture(img, fx, 190, 160, 190)
    # 소파
    sx, sy = 560, 600
    rrect(img, (sx - 280, sy - 40, sx + 280, sy + 150), 26, (58, 44, 44))
    rrect(img, (sx - 300, sy + 60, sx + 300, sy + 170), 22, (48, 36, 38))
    rrect(img, (sx - 200, sy - 10, sx - 20, sy + 70), 16, (70, 54, 52))
    rrect(img, (sx + 20, sy - 10, sx + 200, sy + 70), 16, (70, 54, 52))
    # 앉은 자국 (스토리 디테일)
    ellipse(img, (sx + 44, sy + 6, sx + 176, sy + 54), (40, 30, 30), 200)
    # 탁자 + 수첩
    rrect(img, (900, 660, 1130, 690), 8, (52, 40, 36))
    rect(img, (930, 640, 1010, 662), PAPER, 210)
    # 러그
    ellipse(img, (420, 800, 1180, 980), (62, 40, 40), 150)
    # 바닥 톱니
    ellipse(img, (1180, 880, 1250, 940), (96, 104, 116), 230)
    ellipse(img, (1200, 900, 1230, 920), (34, 26, 24), 255)
    return vignette(grain(img))

def bg_hall():
    img = canvas(W, H, INK0 + (255,))
    vgrad(img, (0, 0, W, H), (26, 30, 46), (12, 15, 26))
    cx, cy = 960, 520
    # 원근 복도
    poly(img, [(0, H), (W, H), (cx + 200, cy + 130), (cx - 200, cy + 130)], (26, 22, 24))
    poly(img, [(0, 0), (W, 0), (cx + 200, cy - 140), (cx - 200, cy - 140)], (20, 24, 38))
    poly(img, [(0, 0), (cx - 200, cy - 140), (cx - 200, cy + 130), (0, H)], (30, 34, 52))
    poly(img, [(W, 0), (cx + 200, cy - 140), (cx + 200, cy + 130), (W, H)], (24, 28, 44))
    # 복도 끝 문
    rect(img, (cx - 200, cy - 140, cx + 200, cy + 130), (16, 19, 32))
    rect(img, (cx - 70, cy - 90, cx + 70, cy + 130), (11, 14, 24))
    img = glow(img, cx, cy + 20, 220, AMBER, 0.30)
    # 바닥 타일 원근선
    for i in range(9):
        t = (i + 1) / 10
        y = cy + 130 + (H - (cy + 130)) * (t ** 1.7)
        line(img, [(cx - 200 - (cx - 200) * (t ** 1.7) * 1.0, y), (cx + 200 + (W - cx - 200) * (t ** 1.7), y)],
             2, (46, 42, 46), 150)
    # 오른쪽 벽 초상화 7개 — 6개만 명패가 있고 하나는 비어 있음
    for i in range(4):
        t = i / 4
        x = W - 120 - i * 190
        s = 1.0 - t * 0.35
        plate = AMBER2 if i != 1 else None
        framed_picture(img, x, 330 + t * 40, 120 * s, 150 * s,
                       inner=INK1 if i != 1 else (9, 11, 20),
                       plate=plate)
    for i in range(3):
        t = i / 4
        x = 120 + i * 190
        s = 1.0 - t * 0.35
        framed_picture(img, x, 330 + t * 40, 120 * s, 150 * s, plate=AMBER2)
    # 벽등
    for x in (330, 1590):
        rect(img, (x - 10, 200, x + 10, 250), AMBER2)
        img = glow(img, x, 210, 170, AMBER, 0.5)
    return vignette(grain(img), 0.78)

def bg_storage():
    img = room_base((34, 32, 30), (20, 19, 20), (28, 25, 22))
    # 선반
    for row in range(3):
        y = 260 + row * 150
        rect(img, (90, y, 760, y + 16), (56, 44, 34))
        for i in range(5):
            x = 110 + i * 128
            rect(img, (x, y - 68, x + 92, y), (44, 38, 34))
            rect(img, (x + 10, y - 58, x + 82, y - 10), (62, 52, 42), 180)
    rect(img, (90, 260, 108, FLOOR), (46, 38, 30))
    rect(img, (742, 260, 760, FLOOR), (46, 38, 30))
    # 상자 더미
    for (x, y, s) in ((860, 620, 140), (1010, 650, 110), (900, 480, 120)):
        rect(img, (x, y, x + s, y + s), (66, 52, 38))
        rect(img, (x + 6, y + 6, x + s - 6, y + s - 6), (52, 42, 32))
        line(img, [(x + 10, y + 10), (x + s - 10, y + s - 10)], 5, (78, 62, 46))
        line(img, [(x + s - 10, y + 10), (x + 10, y + s - 10)], 5, (78, 62, 46))
    # 자물쇠 캐비닛
    cx = 1480
    rect(img, (cx - 190, 330, cx + 190, FLOOR), (48, 46, 50))
    rect(img, (cx - 170, 350, cx - 6, FLOOR - 24), (36, 35, 40))
    rect(img, (cx + 6, 350, cx + 170, FLOOR - 24), (36, 35, 40))
    img = glow(img, cx, 560, 190, AMBER, 0.35)
    ellipse(img, (cx - 46, 514, cx + 46, 606), (88, 82, 70))
    ellipse(img, (cx - 30, 530, cx + 30, 590), (26, 25, 30))
    for i in range(8):
        a = i * math.pi / 4
        line(img, [(cx + math.cos(a) * 34, 560 + math.sin(a) * 34),
                   (cx + math.cos(a) * 44, 560 + math.sin(a) * 44)], 4, AMBER)
    # 천장 전구
    line(img, [(1180, 0), (1180, 150)], 4, (50, 48, 52))
    ellipse(img, (1160, 150, 1200, 190), (240, 220, 170), 240)
    img = glow(img, 1180, 170, 420, AMBER, 0.5)
    return vignette(grain(img))

def bg_secret():
    img = canvas(W, H, INK0 + (255,))
    vgrad(img, (0, 0, W, H), (16, 14, 20), (6, 6, 10))
    vgrad(img, (0, FLOOR, W, H), (14, 12, 16), (7, 6, 9))
    # 천장 램프 + 빛 원뿔
    line(img, [(960, 0), (960, 170)], 4, (40, 38, 44))
    poly(img, [(930, 175), (990, 175), (1240, H), (680, H)], (236, 214, 160), 26)
    ellipse(img, (924, 168, 996, 206), (240, 222, 170), 240)
    img = glow(img, 960, 190, 620, (226, 200, 150), 0.55)
    # 벽의 아이 그림들
    cray = [(214, 96, 86), (96, 140, 200), (226, 186, 86), (120, 180, 120)]
    for i in range(6):
        x = 300 + (i % 3) * 460 + random.randint(-30, 30)
        y = 250 + (i // 3) * 210
        rect(img, (x, y, x + 210, y + 160), PAPER, 42)
        c = cray[i % 4]
        # 사람 둘
        for k, dx in enumerate((60, 140)):
            a = 200 if k == 0 else 90
            ellipse(img, (x + dx - 18, y + 46, x + dx + 18, y + 82), c + (0,)[:0] or c, a)
            line(img, [(x + dx, y + 82), (x + dx, y + 126)], 5, c, a)
            line(img, [(x + dx - 24, y + 100), (x + dx + 24, y + 100)], 5, c, a)
        # 한 명은 까맣게 덧칠
        if i % 2 == 1:
            ellipse(img, (x + 122, y + 44, x + 160, y + 84), (12, 10, 14), 235)
    # 작은 의자
    rect(img, (830, 620, 950, 636), (46, 40, 44))
    rect(img, (836, 636, 850, FLOOR), (40, 34, 38))
    rect(img, (930, 636, 944, FLOOR), (40, 34, 38))
    rect(img, (930, 500, 944, 636), (40, 34, 38))
    return vignette(grain(img), 0.88)

def bg_bedroom():
    img = room_base((46, 34, 42), (26, 20, 28), (34, 24, 28))
    wainscot(img, FLOOR, 100, (44, 30, 38))
    # 침대
    rrect(img, (220, 540, 880, 720), 18, (70, 44, 54))
    rrect(img, (240, 470, 880, 560), 16, (88, 56, 66))
    rrect(img, (280, 496, 420, 552), 14, PAPER, 180)
    rect(img, (200, 430, 232, 760), (54, 36, 44))
    rect(img, (868, 470, 900, 760), (54, 36, 44))
    # 화장대 + 거울
    dx = 1420
    rect(img, (dx - 230, 560, dx + 230, 600), (62, 44, 50))
    rect(img, (dx - 210, 600, dx - 180, FLOOR), (52, 36, 42))
    rect(img, (dx + 180, 600, dx + 210, FLOOR), (52, 36, 42))
    ellipse(img, (dx - 170, 200, dx + 170, 540), AMBER2)
    ellipse(img, (dx - 150, 220, dx + 150, 520), (52, 56, 66))
    ellipse(img, (dx - 130, 250, dx + 60, 470), (72, 78, 92), 120)
    img = glow(img, dx, 380, 300, (200, 150, 170), 0.3)
    # 향수 3병
    for i, x in enumerate((dx - 110, dx - 40, dx + 40)):
        h = 46 + i * 12
        rect(img, (x, 560 - h, x + 34, 560), (176, 78, 96), 190)
        rect(img, (x + 10, 560 - h - 14, x + 24, 560 - h), AMBER2)
    # 립스틱들
    for i in range(7):
        x = 980 + i * 34
        rect(img, (x, 546, x + 16, 562), CRIM)
    return vignette(grain(img))

def bg_bath():
    img = room_base((34, 42, 46), (20, 26, 30), (26, 32, 34))
    # 타일
    for r in range(7):
        for c in range(18):
            x, y = c * 108, 120 + r * 92
            if y > FLOOR - 92:
                continue
            rect(img, (x + 3, y + 3, x + 102, y + 86), (38, 48, 54), 120)
    # 거울
    mx = 960
    rect(img, (mx - 300, 200, mx + 300, 560), AMBER2)
    rect(img, (mx - 282, 218, mx + 282, 542), (50, 62, 70))
    poly(img, [(mx - 282, 542), (mx - 120, 218), (mx + 40, 218), (mx - 122, 542)], (86, 102, 112), 70)
    # 김 서림
    ellipse(img, (mx - 200, 280, mx + 140, 500), (210, 224, 230), 40)
    # 세면대
    rrect(img, (mx - 180, 580, mx + 180, 660), 22, (196, 200, 202))
    ellipse(img, (mx - 120, 596, mx + 120, 648), (150, 158, 162))
    rect(img, (mx - 24, 540, mx + 24, 584), (176, 182, 186))
    rect(img, (mx - 40, 660, mx + 40, FLOOR), (170, 176, 180))
    img = glow(img, mx, 400, 420, (150, 190, 200), 0.26)
    return vignette(grain(img))

def bg_laundry():
    img = room_base((30, 34, 36), (18, 21, 24), (24, 26, 28))
    # 세탁기 3대
    for i in range(3):
        x = 300 + i * 420
        rect(img, (x - 150, 420, x + 150, FLOOR), (66, 70, 74))
        rect(img, (x - 130, 440, x + 130, FLOOR - 24), (46, 50, 54))
        ellipse(img, (x - 90, 500, x + 90, 680), (30, 34, 38))
        ring(img, (x - 90, 500, x + 90, 680), 10, (92, 96, 100))
        ellipse(img, (x - 66, 524, x + 66, 656), (18, 22, 26))
        if i == 1:
            img = glow(img, x, 590, 220, TEAL2, 0.35)
    # 파이프
    for y in (180, 236):
        rect(img, (0, y, W, y + 22), (58, 56, 52))
        for i in range(10):
            rect(img, (110 + i * 190, y - 6, 140 + i * 190, y + 28), (74, 70, 64))
    # 멈춘 벽시계 (3시 12분)
    cxx, cyy = 1640, 330
    ellipse(img, (cxx - 76, cyy - 76, cxx + 76, cyy + 76), (208, 202, 188))
    ellipse(img, (cxx - 64, cyy - 64, cxx + 64, cyy + 64), (44, 44, 46))
    line(img, [(cxx, cyy), (cxx + 44, cyy + 6)], 6, PAPER)
    line(img, [(cxx, cyy), (cxx - 18, cyy - 40)], 5, PAPER)
    # 흩어진 톱니
    for (x, y, r) in ((700, 880, 48), (820, 920, 34), (1180, 860, 40)):
        ellipse(img, (x - r, y - r, x + r, y + r), (96, 100, 108), 220)
        ellipse(img, (x - r * 0.4, y - r * 0.4, x + r * 0.4, y + r * 0.4), (24, 26, 28))
    return vignette(grain(img))

def bg_guest():
    img = room_base((36, 38, 46), (22, 24, 32), (28, 28, 34))
    wainscot(img, FLOOR, 96, (34, 36, 46))
    # 책상
    rect(img, (220, 560, 900, 596), (60, 52, 46))
    rect(img, (250, 596, 282, FLOOR), (48, 42, 38))
    rect(img, (838, 596, 870, FLOOR), (48, 42, 38))
    # 쌓인 노트
    for i in range(6):
        x = 300 + (i % 3) * 190
        y = 560 - (i // 3) * 26 - 22
        rect(img, (x, y, x + 150, y + 22), PAPER, 200 - i * 12)
        line(img, [(x, y + 22), (x + 150, y + 22)], 3, (150, 146, 136))
    # 벽에 붙인 종이
    for i in range(9):
        x = 1080 + (i % 3) * 200
        y = 200 + (i // 3) * 170
        rect(img, (x, y, x + 150, y + 130), PAPER, 160)
        for k in range(5):
            line(img, [(x + 16, y + 24 + k * 20), (x + 16 + random.randint(50, 118), y + 24 + k * 20)], 3, (110, 108, 104), 190)
    # 바닥에 떨어진 종이
    for i in range(7):
        x = 300 + i * 190 + random.randint(-40, 40)
        rect(img, (x, 860 + random.randint(-30, 40), x + 130, 960 + random.randint(-20, 30)), PAPER, 110)
    img = glow(img, 1420, 320, 460, (150, 160, 190), 0.22)
    return vignette(grain(img))


# ── 인물 ─────────────────────────────────────────────────────────────────────
CW, CH = 700, 1200
HEAD_CX, HEAD_CY, HEAD_R = 350, 250, 132

def character(accent, prop=None, shadow_face=False):
    img = canvas(CW, CH)
    coat = (222, 224, 228)
    coat_sh = (176, 180, 188)

    # 몸통 (가운)
    poly(img, [(350, 380), (560, 470), (600, 1200), (100, 1200), (140, 470)], coat)
    poly(img, [(350, 380), (140, 470), (100, 1200), (350, 1200)], coat_sh, 90)
    # 가운 앞섶
    poly(img, [(350, 392), (420, 430), (392, 1200), (308, 1200)], (240, 242, 246))
    line(img, [(350, 420), (350, 1200)], 4, (168, 172, 180))
    # 옷깃 (강조색)
    poly(img, [(350, 380), (452, 428), (392, 560), (350, 470)], accent)
    poly(img, [(350, 380), (248, 428), (308, 560), (350, 470)], tuple(int(c * 0.82) for c in accent))
    # 팔
    rrect(img, (96, 500, 176, 940), 40, coat_sh)
    rrect(img, (524, 500, 604, 940), 40, coat_sh)

    # 목
    rect(img, (318, 330, 382, 400), (226, 200, 182))
    # 머리
    ellipse(img, (HEAD_CX - HEAD_R, HEAD_CY - HEAD_R - 14, HEAD_CX + HEAD_R, HEAD_CY + HEAD_R + 6), (238, 212, 192))
    # 머리카락
    poly(img, [(HEAD_CX - HEAD_R - 14, HEAD_CY + 40), (HEAD_CX - HEAD_R - 6, HEAD_CY - 120),
               (HEAD_CX, HEAD_CY - HEAD_R - 30), (HEAD_CX + HEAD_R + 6, HEAD_CY - 120),
               (HEAD_CX + HEAD_R + 14, HEAD_CY + 40), (HEAD_CX + HEAD_R - 10, HEAD_CY - 40),
               (HEAD_CX, HEAD_CY - 110), (HEAD_CX - HEAD_R + 10, HEAD_CY - 40)], (52, 44, 52))
    rrect(img, (HEAD_CX - HEAD_R - 16, HEAD_CY - 60, HEAD_CX - HEAD_R + 26, HEAD_CY + 210), 20, (52, 44, 52))
    rrect(img, (HEAD_CX + HEAD_R - 26, HEAD_CY - 60, HEAD_CX + HEAD_R + 16, HEAD_CY + 210), 20, (52, 44, 52))

    if shadow_face:
        ellipse(img, (HEAD_CX - HEAD_R, HEAD_CY - HEAD_R - 14, HEAD_CX + HEAD_R, HEAD_CY + HEAD_R + 6),
                (16, 14, 22), 232)

    # 소품
    if prop == "mug":
        rrect(img, (500, 760, 592, 856), 12, (206, 206, 210))
        ring(img, (576, 782, 626, 832), 10, (206, 206, 210))
        ellipse(img, (508, 764, 584, 792), (78, 54, 40))
    elif prop == "ledger":
        poly(img, [(452, 720), (640, 690), (656, 880), (468, 910)], (196, 160, 72))
        poly(img, [(462, 734), (630, 706), (642, 866), (476, 894)], PAPER, 220)
    elif prop == "flask":
        poly(img, [(524, 740), (568, 740), (568, 790), (600, 866), (492, 866)], (200, 214, 218), 220)
        poly(img, [(506, 830), (586, 830), (600, 866), (492, 866)], accent, 220)

    return img


# ── 표정 ─────────────────────────────────────────────────────────────────────
# Live2D 느낌은 "부위가 각자 움직인다"에서 나옵니다. 그래서 표정을 한 장으로
# 합치지 않고 눈썹/눈/동공/입/기타를 따로 뽑습니다. 각 PNG는 내용에 맞게 잘라
# 내고, 원래 700x1200 캔버스 어디에 있었는지를 faceparts.json 에 적어 둡니다.
# 런타임(PortraitView)은 그 좌표로 부위를 제자리에 놓고 따로 움직입니다.
#
#   eyes   — 흰자. 세로로 눌러서 깜빡입니다.
#   pupils — 동공. 눈 안에서 미세하게 흔들려 시선을 만듭니다.
#   mouth / mouth_open — 대사 출력 중 번갈아 찍어 입을 움직입니다.
#   brows, extra(코·눈물·그늘) — 표정 전환 반응 모션에만 실립니다.
FACE_PARTS = ("extra", "brows", "eyes", "pupils", "mouth", "mouth_open")

# 눈이 이미 선으로 감겨 있는 표정은 깜빡여 봐야 티가 안 나서 건너뜁니다.
NO_BLINK = ("sleepy",)


def face_layers(kind):
    """표정 하나를 부위별 레이어로 그립니다. 전부 같은 CWxCH 캔버스."""
    ex, ey, dx = HEAD_CX, HEAD_CY + 10, 52
    ink = (38, 32, 40)
    L = {p: canvas(CW, CH) for p in FACE_PARTS}

    def eye_white(cx, h=22, w=26):
        ellipse(L["eyes"], (cx - w, ey - h, cx + w, ey + h), (250, 248, 246))
    def pupil(cx, r=12):
        ellipse(L["pupils"], (cx - r, ey - r, cx + r, ey + r), ink)
    def eye_open(cx, h=22, w=26):
        eye_white(cx, h, w); pupil(cx)
    def eye_line(cx, w=28):
        line(L["eyes"], [(cx - w, ey), (cx + w, ey)], 7, ink)
    def brow(cx, y, tilt, w=34):
        line(L["brows"], [(cx - w, y - tilt), (cx + w, y + tilt)], 8, ink)
    def mouth(pts, width=7):
        line(L["mouth"], pts, width, ink)
    def mouth_open(cy, w=20, h=22):
        ellipse(L["mouth_open"], (ex - w, cy - h, ex + w, cy + h), ink)

    if kind == "normal":
        eye_open(ex - dx); eye_open(ex + dx)
        brow(ex - dx, ey - 52, 0); brow(ex + dx, ey - 52, 0)
        mouth([(ex - 24, ey + 78), (ex + 24, ey + 78)])
        mouth_open(ey + 84, 18, 20)
    elif kind == "surprised":
        eye_open(ex - dx, 30, 30); eye_open(ex + dx, 30, 30)
        brow(ex - dx, ey - 64, 0); brow(ex + dx, ey - 64, 0)
        ellipse(L["mouth"], (ex - 20, ey + 62, ex + 20, ey + 106), ink)
        mouth_open(ey + 84, 24, 30)
    elif kind == "angry":
        eye_open(ex - dx, 20); eye_open(ex + dx, 20)
        brow(ex - dx, ey - 48, 16); brow(ex + dx, ey - 48, -16)
        mouth([(ex - 28, ey + 86), (ex, ey + 74), (ex + 28, ey + 86)])
        mouth_open(ey + 84, 22, 24)
    elif kind == "sad":
        eye_open(ex - dx, 18); eye_open(ex + dx, 18)
        brow(ex - dx, ey - 50, -14); brow(ex + dx, ey - 50, 14)
        mouth([(ex - 26, ey + 88), (ex, ey + 74), (ex + 26, ey + 88)])
        mouth_open(ey + 86, 16, 18)
        ellipse(L["extra"], (ex - dx - 34, ey + 18, ex - dx - 14, ey + 56), (140, 190, 220), 210)
    elif kind == "sleepy":
        eye_line(ex - dx); eye_line(ex + dx)
        brow(ex - dx, ey - 54, -6); brow(ex + dx, ey - 54, 6)
        mouth([(ex - 20, ey + 80), (ex + 20, ey + 80)])
        mouth_open(ey + 86, 16, 20)
        # 눈 밑 그늘
        line(L["extra"], [(ex - dx - 22, ey + 22), (ex - dx + 22, ey + 22)], 5, (150, 130, 140), 180)
        line(L["extra"], [(ex + dx - 22, ey + 22), (ex + dx + 22, ey + 22)], 5, (150, 130, 140), 180)

    # 코 — 어느 표정에서나 같습니다.
    line(L["extra"], [(ex, ey + 28), (ex - 8, ey + 48)], 5, (196, 160, 146), 200)
    return L


def face(kind):
    """부위를 전부 겹친 한 장. 부위 PNG 를 못 읽는 환경을 위한 폴백입니다."""
    img = canvas(CW, CH)
    L = face_layers(kind)
    for p in FACE_PARTS:
        if p == "mouth_open":
            continue  # 기본 상태는 다문 입
        img = Image.alpha_composite(img, L[p])
    return img


def save_face_parts(kind, manifest):
    """부위를 내용 경계로 잘라 저장하고, 원래 캔버스 위치를 manifest 에 남깁니다."""
    entry = {"key": kind, "canBlink": kind not in NO_BLINK, "parts": []}
    folder = os.path.join(OUT, "Faces", kind)
    os.makedirs(folder, exist_ok=True)

    L = face_layers(kind)
    for name in FACE_PARTS:
        img = finish(L[name], CW, CH)
        box = img.getbbox()
        if box is None:
            continue  # 이 표정엔 없는 부위 (예: normal 의 눈물)
        # LANCZOS 축소로 가장자리 알파가 깎이므로 1px 여유를 둡니다.
        x0, y0, x1, y1 = box
        x0 = max(0, x0 - 1); y0 = max(0, y0 - 1)
        x1 = min(CW, x1 + 1); y1 = min(CH, y1 + 1)

        img.crop((x0, y0, x1, y1)).save(os.path.join(folder, name + ".png"))
        entry["parts"].append({"name": name, "x": x0, "y": y0, "w": x1 - x0, "h": y1 - y0})
        print(f"  Faces/{kind}/{name}.png")

    manifest["faces"].append(entry)


# ── 증거 아이콘 ──────────────────────────────────────────────────────────────
IW = 512

def icon_base():
    img = canvas(IW, IW)
    rrect(img, (16, 16, IW - 16, IW - 16), 40, (28, 32, 46))
    rrect(img, (16, 16, IW - 16, IW - 16), 40, (0, 0, 0), 0)
    ImageDraw.Draw(img, "RGBA").rounded_rectangle(S((16, 16, IW - 16, IW - 16)), S(40),
                                                 outline=(64, 72, 96, 255), width=S(4))
    return img

def ic_gear():
    img = icon_base()
    c, r = IW / 2, 130
    for i in range(10):
        a = i * math.pi / 5
        poly(img, [(c + math.cos(a - .14) * r, c + math.sin(a - .14) * r),
                   (c + math.cos(a - .10) * (r + 46), c + math.sin(a - .10) * (r + 46)),
                   (c + math.cos(a + .10) * (r + 46), c + math.sin(a + .10) * (r + 46)),
                   (c + math.cos(a + .14) * r, c + math.sin(a + .14) * r)], (150, 158, 172))
    ellipse(img, (c - r, c - r, c + r, c + r), (150, 158, 172))
    ellipse(img, (c - 56, c - 56, c + 56, c + 56), (28, 32, 46))
    # 안쪽에서 깨진 흔적
    poly(img, [(c - 10, c - 56), (c + 40, c - 130), (c + 70, c - 96), (c + 24, c - 48)], (28, 32, 46))
    return img

def ic_note():
    img = icon_base()
    rrect(img, (128, 92, 384, 420), 12, PAPER)
    rect(img, (128, 92, 168, 420), (176, 168, 150))
    for i in range(7):
        line(img, [(196, 150 + i * 36), (196 + [120, 150, 96, 140, 110, 60, 130][i], 150 + i * 36)],
             6, (120, 118, 114))
    return img

def ic_drawing():
    img = icon_base()
    rrect(img, (96, 110, 416, 402), 10, PAPER)
    for k, dx in enumerate((190, 310)):
        c = (214, 96, 86) if k == 0 else (12, 10, 14)
        ellipse(img, (dx - 34, 170, dx + 34, 238), c)
        line(img, [(dx, 238), (dx, 320)], 10, (96, 140, 200) if k == 0 else (12, 10, 14))
        line(img, [(dx - 44, 268), (dx + 44, 268)], 10, (96, 140, 200) if k == 0 else (12, 10, 14))
    line(img, [(130, 360), (380, 360)], 8, (226, 186, 86))
    return img

def ic_ledger():
    img = icon_base()
    poly(img, [(120, 100), (392, 100), (392, 412), (120, 412)], (196, 160, 72))
    rect(img, (140, 120, 372, 392), PAPER)
    for i in range(7):
        y = 146 + i * 36
        line(img, [(154, y), (358, y)], 4, (150, 146, 136))
        if i != 6:
            line(img, [(170, y - 14), (170 + [70, 90, 60, 110, 80, 96][i], y - 14)], 8, (100, 100, 104))
    line(img, [(256, 120), (256, 392)], 4, (150, 146, 136))
    return img

def ic_person(accent, shadow=False):
    img = icon_base()
    ellipse(img, (196, 130, 316, 250), (16, 14, 22) if shadow else (238, 212, 192))
    poly(img, [(256, 210), (200, 190), (188, 130), (256, 100), (324, 130), (312, 190)], (52, 44, 52))
    poly(img, [(256, 250), (360, 300), (380, 430), (132, 430), (152, 300)], (222, 224, 228))
    poly(img, [(256, 250), (310, 276), (288, 340), (256, 300)], accent)
    poly(img, [(256, 250), (202, 276), (224, 340), (256, 300)], tuple(int(c * 0.82) for c in accent))
    return img


# ── 실행 ─────────────────────────────────────────────────────────────────────
if __name__ == "__main__":
    print("배경:")
    for name, fn in [("bg_lab", bg_lab), ("bg_front", bg_front), ("bg_living", bg_living),
                     ("bg_hall", bg_hall), ("bg_storage", bg_storage), ("bg_secret", bg_secret),
                     ("bg_bedroom", bg_bedroom), ("bg_bath", bg_bath),
                     ("bg_laundry", bg_laundry), ("bg_guest", bg_guest)]:
        save(fn(), "Backgrounds", name, W, H)

    print("인물:")
    props = {"sloth": "mug", "greed": "ledger", "doctor": "flask"}
    for key, accent in ACCENTS.items():
        save(character(accent, props.get(key), shadow_face=(key == "pride")),
             "Characters", key, CW, CH)

    print("표정:")
    face_manifest = {"canvasW": CW, "canvasH": CH, "faces": []}
    for k in ("normal", "surprised", "angry", "sad", "sleepy"):
        save(face(k), "Faces", k, CW, CH)   # 합본 (폴백용)
        save_face_parts(k, face_manifest)   # 부위별 (애니메이션용)

    with open(os.path.join(OUT, "Faces", "faceparts.json"), "w", encoding="utf-8") as f:
        json.dump(face_manifest, f, ensure_ascii=False, indent=1)
    print("  Faces/faceparts.json")

    print("증거:")
    save(ic_gear(),    "Evidence", "ic_gear", IW, IW)
    save(ic_note(),    "Evidence", "ic_note", IW, IW)
    save(ic_drawing(), "Evidence", "ic_drawing", IW, IW)
    save(ic_ledger(),  "Evidence", "ic_ledger", IW, IW)
    save(ic_person(ACCENTS["sloth"]), "Evidence", "ic_sloth", IW, IW)
    save(ic_person(ACCENTS["greed"]), "Evidence", "ic_greed", IW, IW)
    save(ic_person(ACCENTS["pride"], shadow=True), "Evidence", "ic_pride", IW, IW)

    print("\n완료:", OUT)
