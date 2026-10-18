# -*- coding: utf-8 -*-
"""
증거 그림 생성기. 노트에서 클릭하면 크게 보이므로, 추리에 쓰는 작은 디테일
(날짜·시각·표시)은 설명에 적지 않고 그림 안에만 넣어 둡니다 — 확대해야 보입니다.

    python ArtSource/gen_evidence.py

출력: Assets/_Project/Resources/Art/Evidence/
    ic_{물건}.png      1024x1024  단서 오브젝트 (Evidence 탭 '아이콘키' 와 일치)
    ic_chr_{키}.png     512x512   인물 카드 — 포트레이트에서 얼굴만 잘라 냅니다.
                                   Portraits/{키}.png 가 없으면 슬픔 그림을 씁니다.
                                   인물 그림이 들어오면 다시 돌리세요.

그림체는 새 포트레이트에 맞춰 굵은 먹선 + 탁한 색입니다. 전부 코드라서 숫자만 바꿔
다시 뽑을 수 있습니다. 아트 담당이 그린 그림이 오면 같은 이름으로 덮어쓰면 됩니다.
"""
import math, os, random
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps

random.seed(14)

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "..", "Assets", "_Project", "Resources", "Art", "Evidence")
PORTRAITS = os.path.join(ROOT, "..", "Assets", "_Project", "Resources", "Art", "Portraits")

N = 1024         # 결과 크기
S = 2            # 슈퍼샘플링
INK = (38, 32, 34)
PAPER = (226, 220, 208)
LINE = 7         # 기본 먹선 굵기 (결과 px)

FONT_DIR = r"C:\Windows\Fonts"
FONT = os.path.join(FONT_DIR, "malgun.ttf")
FONT_BOLD = os.path.join(FONT_DIR, "malgunbd.ttf")


# ── 도구 ─────────────────────────────────────────────────────────────────────

def c(v):
    return int(round(v * S))


def pts(points):
    return [(c(x), c(y)) for x, y in points]


def canvas(w=N, h=N):
    return Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))


def finish(img, w=N, h=N):
    return img.resize((w, h), Image.LANCZOS)


def poly(d, points, fill, w=LINE, outline=INK):
    p = pts(points)
    d.polygon(p, fill=fill)
    if w:
        d.line(p + [p[0]], fill=outline, width=c(w), joint="curve")


def ell(d, box, fill, w=LINE, outline=INK):
    d.ellipse([c(v) for v in box], fill=fill, outline=outline if w else None, width=c(w) if w else 0)


def rrect(d, box, r, fill, w=LINE, outline=INK):
    d.rounded_rectangle([c(v) for v in box], radius=c(r), fill=fill,
                        outline=outline if w else None, width=c(w) if w else 0)


def line(d, points, color, w):
    d.line(pts(points), fill=color, width=c(w), joint="curve")


def font(size, bold=False):
    return ImageFont.truetype(FONT_BOLD if bold else FONT, c(size))


def text(img, xy, s, size, color, bold=False, anchor="mm"):
    ImageDraw.Draw(img).text((c(xy[0]), c(xy[1])), s, font=font(size, bold), fill=color, anchor=anchor)


def hand(img, xy, s, size, color, bold=True, tilt=6, jitter=3, spacing=0.92, center=False):
    """손글씨 흉내 — 한 글자씩 조금씩 기울이고 들쭉날쭉하게 찍습니다."""
    f = font(size, bold)
    widths = [f.getlength(ch) * spacing for ch in s]
    x = c(xy[0]) - (sum(widths) / 2 if center else 0)
    y = c(xy[1])
    for ch, w in zip(s, widths):
        if ch != " ":
            box = c(size * 1.6)
            tile = Image.new("RGBA", (box, box), (0, 0, 0, 0))
            ImageDraw.Draw(tile).text((box / 2, box / 2), ch, font=f, fill=color, anchor="mm")
            tile = tile.rotate(random.uniform(-tilt, tilt), resample=Image.BICUBIC)
            dy = random.uniform(-jitter, jitter) * S
            img.alpha_composite(tile, (int(x + w / 2 - box / 2), int(y - box / 2 + dy)))
        x += w


def layer_like(img):
    return Image.new("RGBA", img.size, (0, 0, 0, 0))


def shadow(img, shape_layer, offset=(10, 14), blur=16, alpha=90):
    """물건 아래 부드러운 그림자. shape_layer 의 알파를 흐려서 깝니다."""
    a = shape_layer.getchannel("A").filter(ImageFilter.GaussianBlur(c(blur)))
    a = a.point(lambda v: v * alpha // 255)
    sh = Image.new("RGBA", img.size, (20, 14, 12, 0))
    sh.putalpha(a)
    img.alpha_composite(sh, (c(offset[0]), c(offset[1])))


def card_background():
    """증거 카드 바탕 — 살짝 누런 종이. 가장자리는 조금 어둡게."""
    img = canvas()
    d = ImageDraw.Draw(img)
    rrect(d, (12, 12, N - 12, N - 12), 36, PAPER, w=0)

    noise = Image.effect_noise((N * S // 4, N * S // 4), 22).resize(img.size, Image.BILINEAR)
    grain = Image.merge("RGBA", (noise, noise, noise, Image.new("L", img.size, 18)))
    mask = img.getchannel("A")
    grain.putalpha(ImageChops.multiply(grain.getchannel("A"), mask))
    img.alpha_composite(grain)

    vig = Image.new("L", img.size, 0)
    ImageDraw.Draw(vig).ellipse([c(-160), c(-160), c(N + 160), c(N + 160)], fill=255)
    vig = ImageOps.invert(vig.filter(ImageFilter.GaussianBlur(c(120)))).point(lambda v: v * 70 // 255)
    dark = Image.new("RGBA", img.size, (60, 44, 30, 0))
    dark.putalpha(ImageChops.multiply(vig, mask))
    img.alpha_composite(dark)
    return img


def save(img, name, size=N):
    os.makedirs(OUT, exist_ok=True)
    finish(img, size, size).save(os.path.join(OUT, name + ".png"))
    print(f"  Evidence/{name}.png")


# ── 저택 도면 ────────────────────────────────────────────────────────────────
# 숨은 것: 방마다 빨간 X '동력부', 구석의 연필 메모 '3:12' 와 바를 정 7획.

def house_plan():
    img = card_background()
    sheet = layer_like(img)
    d = ImageDraw.Draw(sheet)
    outline = [(96, 128), (930, 110), (940, 902), (88, 918)]
    poly(d, outline, (46, 78, 126))
    shadow(img, sheet)
    img.alpha_composite(sheet)

    # 모눈 — 종이 안에만
    grid = layer_like(img)
    g = ImageDraw.Draw(grid)
    for k in range(120, 930, 32):
        line(g, [(k, 110), (k, 918)], (230, 240, 255, 26), 1.2)
        line(g, [(88, k), (940, k)], (230, 240, 255, 26), 1.2)
    # 접힌 자국 — 오래 들고 다닌 흔적
    line(g, [(512, 112), (514, 912)], (255, 255, 255, 60), 3)
    line(g, [(90, 512), (938, 506)], (255, 255, 255, 60), 3)
    for cx, cy in ((140, 170), (890, 860), (160, 870)):
        g.ellipse([c(cx - 60), c(cy - 60), c(cx + 60), c(cy + 60)], fill=(255, 255, 255, 22))
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).polygon(pts(outline), fill=255)
    grid.putalpha(ImageChops.multiply(grid.getchannel("A"), mask))
    img.alpha_composite(grid)

    d = ImageDraw.Draw(img)
    white = (236, 242, 250)
    text(img, (290, 172), "저택 단면도", 40, white, bold=True)

    # 지도(Rooms 탭 지도층·지도칸)와 같은 배치 — 도면을 받으면 지도가 열리는 설정이라 둘이 같아야 합니다.
    # 비밀 통로(교만의 방 → 지하실)는 그리지 않습니다. 교만이 숨긴 것이니까요.
    unit, x0, top, row = 85.5, 170, 230, 104
    floors = ["5F", "4F", "3F", "2F", "1F", "B1"]
    rooms = [  # (층 줄, 칸, 폭, 이름, 동력부 표시)
        (0, 3, 1, "색욕", True), (0, 4, 1, "5층", False), (0, 5, 1, "교만", False),
        (1, 3, 1, "분노", True), (1, 4, 1, "4층", False), (1, 5, 1, "오만", True),
        (2, 2, 2, "정원", False), (2, 4, 1, "3층", False), (2, 5, 1, "탐욕", True),
        (3, 3, 1, "슬픔", True), (3, 4, 1, "2층", False), (3, 5, 1, "나태", True),
        (4, 0, 3, "앞마당", False), (4, 3, 3, "로비", False), (4, 6, 1, "식당", False),
        (5, 3, 5, "지하실", False),
    ]
    for i, name in enumerate(floors):
        text(img, (130, top + i * row + row / 2), name, 20, (170, 190, 220), bold=True)
    marks = []
    for r, col, span, name, power in rooms:
        bx0, by0 = x0 + col * unit, top + r * row
        bx1, by1 = bx0 + span * unit, by0 + row - 8
        width = 3 if name == "앞마당" else 5          # 마당은 건물 밖이라 가는 선
        line(d, [(bx0, by0), (bx1, by0), (bx1, by1), (bx0, by1), (bx0, by0)], white, width)
        text(img, ((bx0 + bx1) / 2, by0 + 34), name, 24, white, bold=True)
        if power:
            marks.append(((bx0 + bx1) / 2, by1 - 30))

    # 숨은 것 1: 동력부 표시 — 여섯 박사의 방에만
    red = (222, 74, 62)
    for cx, cy in marks:
        line(d, [(cx - 11, cy - 11), (cx + 11, cy + 11)], red, 5)
        line(d, [(cx + 11, cy - 11), (cx - 11, cy + 11)], red, 5)
        text(img, (cx, cy + 20), "동력부", 13, red, bold=True)

    # 숨은 것 2: 연필 메모
    pencil = (196, 206, 220)
    hand(img, (760, 862), "3:12", 26, pencil, bold=False, tilt=4, jitter=1.5)
    for i in range(7):
        x = 830 + i * 12 + (8 if i >= 5 else 0)
        line(d, [(x, 846), (x + 2, 878)], pencil, 2.4)
    line(d, [(824, 872), (890, 852)], pencil, 2.4)   # 다섯 개 묶음 사선
    return img


# ── 찢어진 초대장 ────────────────────────────────────────────────────────────
# 숨은 것: 맨 아래 연필 글씨 '엄마가 끝나고 바로 온다고 약속했음!'

def invitation_card(img, for_puzzle=False):
    """찢기 전의 초대장 한 장.

    for_puzzle 은 슬라이딩 퍼즐용입니다 (gen_puzzles.py). 조각마다 그림이 달라야 맞출 수
    있으므로 빈 곳에 낙서와 날짜를 더 넣고, 약속 메모는 오른쪽 아래 칸 안으로 옮깁니다 —
    그 칸이 퍼즐의 빈칸이라 다 맞춘 순간에야 보입니다."""
    card = layer_like(img)
    d = ImageDraw.Draw(card)
    rrect(d, (170, 190, 854, 830), 26, (246, 216, 222))
    # 크레파스 낙서 — 풍선과 케이크
    ell(d, (214, 236, 300, 334), (230, 96, 104), w=5)
    line(d, [(257, 334), (250, 380), (262, 420)], INK, 3)
    rrect(d, (700, 300, 800, 360), 10, (250, 238, 226), w=5)
    for i in range(7):
        x = 712 + i * 13
        line(d, [(x, 300), (x, 274)], (120, 150, 220), 5)
        ell(d, (x - 4, 260, x + 4, 272), (250, 190, 70), w=0)

    hand(card, (512, 300), "생일 초대장", 60, (204, 70, 96), center=True)
    hand(card, (512, 470), "엄마, 꼭 6시까지 와줘", 56, (64, 52, 76), center=True)
    hand(card, (620, 580), "— 딸이", 48, (64, 52, 76))
    note = (120, 112, 128)

    if not for_puzzle:
        heart = [(566, 718), (580, 706), (594, 718), (580, 736)]
        poly(d, heart, (230, 96, 104), w=0)
        # 찢어진 선에 안 걸리게 오른쪽 조각 안에만 씁니다 (파트5 '끝나고 바로 갈게' 복선).
        hand(card, (608, 722), "엄마가 끝나고 바로", 21, note, bold=False, tilt=3, jitter=1.2)
        hand(card, (608, 758), "온다고 약속했음!", 21, note, bold=False, tilt=3, jitter=1.2)
        return card

    # 왼쪽 아래 — 손잡은 두 사람 (엄마와 나)
    crayon = (96, 110, 190)
    for cx, r, body in ((262, 26, 96), (352, 20, 70)):
        top = 800 - body - r * 2
        ell(d, (cx - r, top, cx + r, top + r * 2), (250, 226, 200), w=5, outline=crayon)
        line(d, [(cx, top + r * 2), (cx, 770)], crayon, 6)
        line(d, [(cx, 770), (cx - 18, 806)], crayon, 6)
        line(d, [(cx, 770), (cx + 18, 806)], crayon, 6)
    line(d, [(232, 730), (262, 716), (308, 742), (352, 730), (384, 744)], crayon, 6)
    # 가운데 아래 — 날짜
    hand(card, (512, 744), "6월 14일", 44, (204, 70, 96), center=True)
    # 오른쪽 아래 칸(퍼즐의 빈칸) — 약속 메모
    poly(d, [(706, 712), (720, 700), (734, 712), (720, 730)], (230, 96, 104), w=0)
    hand(card, (700, 752), "엄마가 끝나고 바로", 17, note, bold=False, tilt=3, jitter=1)
    hand(card, (700, 782), "온다고 약속했음!", 17, note, bold=False, tilt=3, jitter=1)
    return card


def invitation():
    img = card_background()
    card = invitation_card(img)

    # 가운데를 들쭉날쭉하게 찢습니다.
    tear = [(512 + random.uniform(-26, 26), y) for y in range(170, 860, 34)]
    left_mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(left_mask).polygon(pts([(0, 0)] + tear + [(0, N)]), fill=255)
    right_mask = ImageOps.invert(left_mask)

    card_alpha = card.getchannel("A")
    pieces = []
    for mask, angle, dx in ((left_mask, 4, -30), (right_mask, -3, 30)):
        piece = card.copy()
        piece.putalpha(ImageChops.multiply(card_alpha, mask))
        fiber = layer_like(img)
        ImageDraw.Draw(fiber).line(pts(tear), fill=(255, 250, 250), width=c(7), joint="curve")
        fiber.putalpha(ImageChops.multiply(fiber.getchannel("A"), piece.getchannel("A")))
        piece.alpha_composite(fiber)
        piece = piece.rotate(angle, resample=Image.BICUBIC, center=(c(512), c(512)))
        moved = layer_like(img)
        moved.alpha_composite(piece, (c(dx), 0))
        pieces.append(moved)

    for p in pieces:
        shadow(img, p, offset=(8, 12), blur=12, alpha=80)
    for p in pieces:
        img.alpha_composite(p)
    return img


# ── 다 녹아버린 초 6개 ────────────────────────────────────────────────────────
# 숨은 것: 꽂는 자리는 일곱 개인데 초는 여섯 개. 한 자리가 비어 있다.

def candles():
    img = card_background()
    obj = layer_like(img)
    d = ImageDraw.Draw(obj)
    side, top, cream = (240, 206, 194), (252, 240, 228), (255, 250, 244)

    ell(d, (90, 650, 934, 930), (238, 234, 226))                  # 접시
    ell(d, (140, 680, 884, 900), (246, 242, 236), w=3)
    ell(d, (190, 560, 834, 780), side)                             # 케이크 아랫단 — 먼저 깔고
    poly(d, [(190, 440), (834, 440), (834, 670), (190, 670)], side, w=0)   # 옆면으로 위 절반을 덮습니다
    line(d, [(190, 440), (190, 670)], INK, LINE)
    line(d, [(834, 440), (834, 670)], INK, LINE)
    for i in range(11):   # 크림 흘러내림
        x = 205 + i * 57
        drop = 40 + (i * 37) % 50
        poly(d, [(x, 470), (x + 46, 470), (x + 40, 470 + drop), (x + 6, 470 + drop - 8)], cream, w=0)
        ell(d, (x + 4, 450 + drop, x + 42, 486 + drop), cream, w=0)
    ell(d, (190, 330, 834, 550), top)                              # 윗면

    colors = [(236, 150, 168), (140, 180, 232), (246, 214, 110), (150, 206, 150),
              (190, 150, 220), (246, 170, 110), (236, 150, 168)]
    spots = []
    for k in range(7):
        a = math.pi / 2 + k * 2 * math.pi / 7
        spots.append((512 + 228 * math.cos(a), 440 + 70 * math.sin(a), k))
    empty = 2   # 오른쪽 앞 — 여기만 초가 없습니다
    for x, y, k in sorted(spots, key=lambda s: s[1]):
        if k == empty:
            ell(d, (x - 17, y - 8, x + 17, y + 8), (150, 112, 96), w=4)
            ell(d, (x - 8, y - 4, x + 8, y + 4), (70, 50, 44), w=0)
            continue
        col = colors[k]
        dark = tuple(int(v * 0.82) for v in col)
        h = random.uniform(38, 66)
        ell(d, (x - 44, y - 14, x + 44, y + 18), col, w=4)        # 녹아 퍼진 웅덩이
        poly(d, [(x - 21, y - h), (x + 21, y - h), (x + 21, y), (x - 21, y)], col, w=5)
        ell(d, (x - 21, y - h - 8, x + 21, y - h + 8), dark, w=5)
        line(d, [(x, y - h), (x + 4, y - h - 14), (x - 3, y - h - 22)], INK, 4)   # 타 버린 심지
        for dx, ln in ((-14, 18), (11, 28)):                        # 촛농 줄기
            line(d, [(x + dx, y - h + 6), (x + dx, y - h + 6 + ln)], dark, 7)

    shadow(img, obj, offset=(10, 16), blur=18, alpha=80)
    img.alpha_composite(obj)
    return img


# ── 시상식 트로피 축소 모형 ──────────────────────────────────────────────────
# 숨은 것: 명판의 날짜 '6. 14.'

def trophy():
    img = card_background()
    obj = layer_like(img)
    d = ImageDraw.Draw(obj)
    gold, gold_hi, gold_sh = (224, 180, 76), (250, 224, 140), (168, 124, 44)

    rrect(d, (280, 740, 744, 880), 14, (54, 44, 42))              # 받침
    rrect(d, (330, 680, 694, 752), 12, (70, 58, 54))
    rrect(d, (372, 768, 652, 858), 6, (206, 172, 100), w=5)      # 명판
    text(obj, (512, 794), "평행세계 이론 최고상", 22, (86, 64, 36), bold=True)
    text(obj, (512, 830), "6. 14.", 24, (86, 64, 36), bold=True)

    poly(d, [(466, 560), (558, 560), (580, 690), (444, 690)], gold)   # 목
    ell(d, (430, 540, 594, 600), gold)
    cup = [(296, 200)]
    for i in range(1, 12):
        t = i / 12
        cup.append((296 + 150 * t ** 0.8, 200 + 350 * t))
    cup += [(512, 560)]
    cup += [(1024 - x, y) for x, y in reversed(cup[:-1])]
    for side in (-1, 1):   # 손잡이 — 컵보다 먼저 그려서 뿌리를 컵이 덮게 합니다
        cx = 512 + side * 176
        box = [c(cx - 112), c(236), c(cx + 112), c(430)]
        start, end = (250, 110) if side > 0 else (70, 290)
        d.arc(box, start, end, fill=INK, width=c(30))
        d.arc(box, start, end, fill=gold, width=c(16))
    poly(d, cup, gold)
    ell(d, (296, 170, 728, 234), gold_sh)                        # 컵 입구
    line(d, [(380, 250), (430, 470)], gold_hi, 16)               # 반사광
    star = [(512 + (46 if k % 2 == 0 else 20) * math.cos(-math.pi / 2 + k * math.pi / 5),
             340 + (46 if k % 2 == 0 else 20) * math.sin(-math.pi / 2 + k * math.pi / 5)) for k in range(10)]
    poly(d, star, gold_hi, w=4)

    shadow(img, obj)
    img.alpha_composite(obj)
    return img


# ── 안 뜯은 선물상자 ─────────────────────────────────────────────────────────
# 숨은 것: 리본에 끼운 카드 '6.14 생일 축하해 — 엄마가'

def gift():
    img = card_background()
    obj = layer_like(img)
    d = ImageDraw.Draw(obj)
    blue, blue_sh, blue_hi = (92, 128, 196), (64, 94, 150), (130, 164, 220)
    pink, pink_sh = (228, 118, 150), (180, 80, 110)

    poly(d, [(220, 450), (690, 450), (690, 850), (220, 850)], blue)
    poly(d, [(690, 450), (820, 380), (820, 770), (690, 850)], blue_sh)
    poly(d, [(220, 450), (350, 380), (820, 380), (690, 450)], blue_hi)
    for x in range(260, 680, 70):   # 포장지 무늬
        for y in range(490, 830, 80):
            ell(d, (x - 7, y - 7, x + 7, y + 7), (170, 196, 236), w=0)

    poly(d, [(430, 450), (480, 450), (480, 850), (430, 850)], pink, w=5)           # 세로 리본
    poly(d, [(430, 450), (480, 450), (610, 380), (560, 380)], pink, w=5)
    poly(d, [(690, 620), (820, 550), (820, 596), (690, 666)], pink_sh, w=5)        # 옆면 리본
    poly(d, [(220, 620), (690, 620), (690, 666), (220, 666)], pink, w=5)           # 가로 리본
    poly(d, [(290, 412), (760, 412), (790, 396), (320, 396)], pink, w=4)
    ell(d, (400, 300, 520, 420), pink)                                             # 매듭 고리
    ell(d, (520, 300, 640, 420), pink)
    ell(d, (490, 370, 550, 420), pink_sh)

    tag = layer_like(img)
    t = ImageDraw.Draw(tag)
    rrect(t, (560, 470, 780, 600), 10, (250, 246, 236), w=5)
    ell(t, (572, 490, 588, 506), (200, 190, 180), w=2)
    hand(tag, (600, 518), "6.14", 30, (60, 60, 92), bold=True, tilt=3, jitter=1.5)
    hand(tag, (596, 558), "생일 축하해", 22, (60, 60, 92), bold=False, tilt=3, jitter=1)
    hand(tag, (676, 586), "— 엄마가", 18, (60, 60, 92), bold=False, tilt=3, jitter=1)
    tag = tag.rotate(-6, resample=Image.BICUBIC, center=(c(670), c(535)))
    line(d, [(470, 470), (580, 498)], (200, 190, 180), 3)          # 카드 끈
    obj.alpha_composite(tag)

    shadow(img, obj)
    img.alpha_composite(obj)
    return img


# ── 멈춘 손목시계 ────────────────────────────────────────────────────────────
# 숨은 것: 3시 자리의 날짜창 '14'

def watch():
    img = card_background()
    obj = layer_like(img)
    d = ImageDraw.Draw(obj)
    leather, leather_sh = (124, 80, 52), (96, 60, 38)

    poly(d, [(420, 60), (604, 60), (590, 300), (434, 300)], leather)
    poly(d, [(434, 724), (590, 724), (604, 964), (420, 964)], leather)
    for y in (800, 850, 900):
        ell(d, (502, y - 8, 522, y + 8), leather_sh, w=3)
    line(d, [(430, 80), (440, 290)], leather_sh, 5)

    ell(d, (262, 262, 762, 762), (196, 200, 206))
    rrect(d, (752, 488, 800, 536), 8, (180, 184, 190))
    ell(d, (296, 296, 728, 728), (150, 154, 162), w=4)
    ell(d, (312, 312, 712, 712), (244, 238, 222), w=4)

    cx, cy = 512, 512
    for k in range(60):
        a = k * math.pi / 30 - math.pi / 2
        r0 = 176 if k % 5 == 0 else 186
        line(d, [(cx + r0 * math.cos(a), cy + r0 * math.sin(a)),
                 (cx + 194 * math.cos(a), cy + 194 * math.sin(a))], INK, 5 if k % 5 == 0 else 2)
    for label, a in (("12", -90), ("6", 90), ("9", 180)):
        r = math.radians(a)
        text(obj, (cx + 138 * math.cos(r), cy + 138 * math.sin(r)), label, 46, INK, bold=True)
    rrect(d, (620, 494, 668, 530), 4, (255, 255, 255), w=3)      # 날짜창
    text(obj, (644, 512), "14", 24, INK, bold=True)

    def hand_at(angle_deg, length, width, color=INK):
        a = math.radians(angle_deg - 90)
        line(d, [(cx - 22 * math.cos(a), cy - 22 * math.sin(a)),
                 (cx + length * math.cos(a), cy + length * math.sin(a))], color, width)

    hand_at((3 + 12 / 60) * 30, 104, 14)      # 시침 3:12
    hand_at(12 * 6, 158, 9)                   # 분침
    hand_at(40 * 6, 170, 3, (196, 60, 56))    # 초침 — 40초에서 멈춤
    ell(d, (cx - 14, cy - 14, cx + 14, cy + 14), INK, w=0)

    glass = layer_like(img)
    g = ImageDraw.Draw(glass)
    g.arc([c(330), c(330), c(694), c(694)], 200, 250, fill=(255, 255, 255, 110), width=c(14))
    line(g, [(360, 380), (420, 430), (410, 470), (470, 520), (540, 540)], (255, 255, 255, 170), 3)   # 금 간 유리
    line(g, [(420, 430), (470, 410)], (255, 255, 255, 150), 2.5)
    obj.alpha_composite(glass)

    shadow(img, obj)
    img.alpha_composite(obj)
    return img


# ── 낡은 캘린더 ─────────────────────────────────────────────────────────────
# 숨은 것: 14일 칸의 아이 글씨 '내 생일!!' 과 어른 글씨 '시상식'

def calendar():
    img = card_background()
    obj = layer_like(img)
    d = ImageDraw.Draw(obj)
    paper = (238, 226, 196)

    poly(d, [(180, 130), (844, 124), (852, 906), (172, 912)], paper)
    for x in range(230, 800, 52):   # 스프링
        ell(d, (x - 10, 110, x + 10, 150), (80, 80, 86), w=3)
    stains = layer_like(img)   # 얼룩 — 겹쳐 칠해야 종이에 구멍이 안 납니다
    for cx, cy, r in ((720, 820, 70), (300, 260, 50)):
        ImageDraw.Draw(stains).ellipse([c(cx - r), c(cy - r), c(cx + r), c(cy + r)], fill=(190, 160, 110, 40))
    obj.alpha_composite(stains)

    text(obj, (300, 222), "6월", 86, (150, 56, 46), bold=True)
    text(obj, (460, 238), "JUNE", 34, (120, 96, 80), bold=True)

    days = "일월화수목금토"
    gx0, gx1, gy0 = 214, 810, 330
    cw = (gx1 - gx0) / 7
    ch = 108
    for i, dname in enumerate(days):
        text(obj, (gx0 + cw * (i + 0.5), 306), dname, 24,
             (170, 60, 50) if i == 0 else (90, 80, 70), bold=True)
    for r in range(6):
        line(d, [(gx0, gy0 + r * ch), (gx1, gy0 + r * ch)], (160, 146, 120), 2)
    for k in range(8):
        line(d, [(gx0 + k * cw, gy0), (gx0 + k * cw, gy0 + 5 * ch)], (160, 146, 120), 2)

    first = 3   # 1일이 수요일
    for day in range(1, 31):
        idx = first + day - 1
        col, row = idx % 7, idx // 7
        if row > 4:
            continue
        x, y = gx0 + col * cw, gy0 + row * ch
        text(obj, (x + 12, y + 20), str(day), 24, (170, 60, 50) if col == 0 else (70, 62, 56),
             bold=True, anchor="lm")
        if day == 14:
            hand(obj, (x + 8, y + 58), "내 생일!!", 15, (214, 66, 86), bold=True, tilt=5, jitter=1)
            text(obj, (x + 12, y + 88), "시상식", 13, (52, 60, 110), anchor="lm")
            for k in range(2):   # 빨간 크레파스 동그라미 두 번
                d.ellipse([c(x - 6 + k * 4), c(y - 4 + k * 3), c(x + cw + 6 - k * 3), c(y + ch + 4 - k * 4)],
                          outline=(206, 48, 48), width=c(5))

    shadow(img, obj)
    img.alpha_composite(obj)
    return img


# ── 인물 카드 ────────────────────────────────────────────────────────────────

CHARACTERS = ["doctor", "wrath", "sorrow", "arrogance", "greed", "lust", "sloth", "pride"]
FALLBACK = "sorrow"

# 700x1200 포트레이트에서 얼굴 둘레(정사각형). 인물마다 머리 크기와 자리가 달라서 따로 잡습니다.
# make_portraits.py 로 그림을 다시 뽑거나 새 인물 그림이 들어오면 여기에 한 줄 더하세요.
FACE_BOXES = {
    "sorrow":    (112, 90, 552, 530),
    "wrath":     (168, 45, 488, 365),
    "arrogance": (200, 35, 500, 335),
    "greed":     (240, 55, 530, 345),
    "lust":      (125, 55, 585, 515),
    # 아래 셋은 make_standins.py 가 슬픔 그림 색만 바꾼 임시 그림이라 얼굴 자리가 슬픔과 같습니다.
    "doctor":    (112, 90, 552, 530),
    "sloth":     (112, 90, 552, 530),
    "pride":     (112, 90, 552, 530),
}


def character_card(key):
    # 자기 그림이 없는 인물은 게임에서도 FALLBACK 그림을 빌려 쓰므로 카드도 그 얼굴입니다.
    art = key if key in FACE_BOXES and os.path.exists(os.path.join(PORTRAITS, key + ".png")) else FALLBACK
    face = Image.open(os.path.join(PORTRAITS, art + ".png")).convert("RGBA")
    face = face.crop(FACE_BOXES[art]).resize((440, 440), Image.LANCZOS)

    size = 512
    img = card_background().resize((size, size), Image.LANCZOS)
    frame = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(frame).rounded_rectangle([36, 36, size - 36, size - 36], radius=18, fill=(196, 188, 176))
    img.alpha_composite(frame)
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle([36, 36, size - 36, size - 36], radius=18, fill=255)
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    layer.alpha_composite(face, (36, 36))
    layer.putalpha(ImageChops.multiply(layer.getchannel("A"), mask))
    img.alpha_composite(layer)
    ImageDraw.Draw(img).rounded_rectangle([36, 36, size - 36, size - 36], radius=18, outline=INK, width=6)
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, f"ic_chr_{key}.png"))
    print(f"  Evidence/ic_chr_{key}.png")


if __name__ == "__main__":
    print("단서:")
    for name, fn in (("ic_house_plan", house_plan), ("ic_invitation", invitation), ("ic_candles", candles),
                     ("ic_trophy", trophy), ("ic_gift", gift), ("ic_watch", watch), ("ic_calendar", calendar)):
        save(fn(), name)
    print("인물 카드:")
    for key in CHARACTERS:
        character_card(key)
