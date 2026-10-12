# -*- coding: utf-8 -*-
"""
방 안에 놓는 소품 그림. 핫스팟을 글자 버튼 대신 화면 속 물건으로 누르게 하려는 것입니다.
Hotspots 탭 '이미지' 칸에 파일 이름(확장자 없이)을 적으면 그 핫스팟 자리에 세워집니다.

    python ArtSource/gen_props.py

출력: Assets/_Project/Resources/Art/Props/prop_*.png  (배경 투명, 물건 둘레로 잘라 냄)

단서 물건은 gen_evidence.py 의 그림을 그대로 씁니다 — 방에서 본 물건과 노트 속 그림이
같아야 하니까요. 노트용 종이 바탕과 그림자만 빼고 물건만 떼어 냅니다.
나머지(레버·괘종시계·기록 더미·출구 화살표·깜빡이는 점)는 여기서 그립니다.

아트 담당 그림이 오면 같은 이름의 투명 PNG 로 덮어쓰면 됩니다.
"""
import math, os
from PIL import Image, ImageDraw, ImageFilter

import gen_evidence as ge
from gen_evidence import INK, c, pts, poly, ell, rrect, line, text, layer_like

OUT = os.path.join(ge.ROOT, "..", "Assets", "_Project", "Resources", "Art", "Props")
MAX_SIDE = 512     # 화면에서는 100~250px 로 보이므로 이 정도면 넉넉합니다
PAD = 12


def bare(draw_fn):
    """gen_evidence 의 그림 함수에서 종이 바탕과 그림자를 빼고 물건만 받습니다."""
    keep_bg, keep_shadow = ge.card_background, ge.shadow
    ge.card_background = ge.canvas
    ge.shadow = lambda *a, **k: None
    try:
        return draw_fn()
    finally:
        ge.card_background, ge.shadow = keep_bg, keep_shadow


def save(img, name, max_side=MAX_SIDE):
    img = ge.finish(img)
    box = img.getbbox()
    box = (max(box[0] - PAD, 0), max(box[1] - PAD, 0), min(box[2] + PAD, img.width), min(box[3] + PAD, img.height))
    img = img.crop(box)
    k = max_side / max(img.size)
    if k < 1:
        img = img.resize((round(img.width * k), round(img.height * k)), Image.LANCZOS)
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"))
    print(f"  Props/{name}.png  {img.size[0]}x{img.size[1]}")


# ── 기동 레버 (연구실) ───────────────────────────────────────────────────────

def lever():
    img = ge.canvas()
    d = ImageDraw.Draw(img)
    steel, steel_sh, steel_hi = (96, 112, 130), (66, 80, 96), (150, 168, 186)

    rrect(d, (340, 560, 684, 940), 26, steel)                       # 받침 기둥
    poly(d, [(540, 572), (672, 572), (672, 928), (540, 928)], steel_sh, w=0)
    rrect(d, (300, 900, 724, 960), 18, steel_sh)                    # 발판
    rrect(d, (300, 500, 724, 590), 22, steel_hi)                    # 윗판
    rrect(d, (470, 520, 554, 572), 14, (34, 38, 46))                # 레버가 꽂힌 홈

    ell(d, (392, 650, 448, 706), (250, 196, 86))                    # 표시등
    ell(d, (392, 740, 448, 796), (90, 100, 112))
    for y in (660, 700, 740, 780):                                  # 눈금
        line(d, [(520, y), (640, y)], INK, 5)
    poly(d, [(560, 836), (600, 800), (640, 836)], (222, 74, 62), w=5)   # 경고 표시

    line(d, [(512, 548), (318, 190)], INK, 46)                      # 레버 자루 (먹선)
    line(d, [(512, 548), (318, 190)], (188, 196, 206), 30)
    ell(d, (238, 96, 398, 256), (214, 62, 54))                      # 손잡이
    ell(d, (270, 124, 318, 164), (244, 140, 130), w=0)
    return img


# ── 멈춘 괘종시계 (저택 홀) — 3시 14분 ───────────────────────────────────────

def grandfather_clock():
    img = ge.canvas()
    d = ImageDraw.Draw(img)
    wood, wood_sh, wood_hi = (112, 72, 48), (82, 50, 32), (150, 102, 68)

    rrect(d, (360, 960, 664, 1004), 10, wood_sh)                    # 발
    rrect(d, (376, 330, 648, 970), 12, wood)                        # 몸통
    poly(d, [(560, 340), (640, 340), (640, 962), (560, 962)], wood_sh, w=0)
    rrect(d, (344, 60, 680, 350), 20, wood_hi)                      # 머리
    poly(d, [(344, 76), (512, 14), (680, 76)], wood_sh)             # 지붕
    rrect(d, (420, 400, 604, 900), 10, (40, 34, 36))                # 추 창
    line(d, [(512, 410), (540, 760)], (190, 150, 70), 8)            # 추 — 기운 채 멈춤
    ell(d, (496, 730, 588, 822), (222, 180, 84))

    cx, cy, r = 512, 206, 112
    ell(d, (cx - r, cy - r, cx + r, cy + r), (244, 238, 222))
    for k in range(12):
        a = k * math.pi / 6 - math.pi / 2
        line(d, [(cx + 88 * math.cos(a), cy + 88 * math.sin(a)),
                 (cx + 102 * math.cos(a), cy + 102 * math.sin(a))], INK, 6)

    def hand(angle_deg, length, width):
        a = math.radians(angle_deg - 90)
        line(d, [(cx, cy), (cx + length * math.cos(a), cy + length * math.sin(a))], INK, width)

    hand((3 + 14 / 60) * 30, 58, 12)      # 시침
    hand(14 * 6, 88, 8)                   # 분침
    ell(d, (cx - 9, cy - 9, cx + 9, cy + 9), INK, w=0)
    return img


# ── 파손 기록 더미 (저택 홀) ─────────────────────────────────────────────────

def records():
    img = ge.canvas()
    paper, paper_sh = (236, 230, 214), (210, 202, 184)

    def sheet(cx, cy, w, h, angle, fill):
        layer = layer_like(img)
        d = ImageDraw.Draw(layer)
        rrect(d, (cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2), 6, fill)
        for i in range(5):
            y = cy - h / 2 + 46 + i * 34
            line(d, [(cx - w / 2 + 34, y), (cx + w / 2 - 34 - (i % 3) * 40, y)], (120, 112, 104), 5)
        img.alpha_composite(layer.rotate(angle, resample=Image.BICUBIC, center=(c(cx), c(cy))))

    sheet(450, 640, 420, 300, -9, paper_sh)
    sheet(590, 600, 420, 300, 7, paper)
    sheet(500, 560, 420, 300, -2, paper)

    d = ImageDraw.Draw(img)
    rrect(d, (560, 420, 800, 590), 10, (70, 96, 150))               # 수첩
    rrect(d, (592, 452, 768, 500), 6, (232, 226, 210), w=4)
    line(d, [(320, 760), (560, 720)], INK, 22)                      # 연필
    line(d, [(320, 760), (560, 720)], (232, 186, 70), 12)
    poly(d, [(300, 764), (326, 748), (330, 770)], (60, 50, 46), w=3)
    ge.ell(d, (386, 520, 470, 580), None, w=6, outline=(206, 48, 48))   # 빨간 동그라미 — 3:12
    text(img, (428, 550), "3:12", 22, (206, 48, 48), bold=True)
    return img


# ── 방에서 나가기 (모든 방) ──────────────────────────────────────────────────

def exit_arrow():
    img = ge.canvas()
    glow = layer_like(img)
    ImageDraw.Draw(glow).ellipse([c(200), c(340), c(824), c(760)], fill=(250, 206, 110, 70))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(c(46))))
    d = ImageDraw.Draw(img)
    amber, amber_hi = (236, 184, 88), (252, 226, 150)
    poly(d, [(300, 400), (512, 560), (724, 400), (724, 500), (512, 672), (300, 500)], amber, w=9)
    poly(d, [(340, 430), (512, 560), (684, 430), (684, 452), (512, 584), (340, 452)], amber_hi, w=0)
    return img


# ── 클릭 영역 표시 점 ────────────────────────────────────────────────────────

def marker():
    size = 128
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse([26, 26, 102, 102], fill=(255, 232, 170, 150))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(14)))
    d = ImageDraw.Draw(img)
    d.ellipse([46, 46, 82, 82], fill=(255, 246, 220, 235))
    d.ellipse([40, 40, 88, 88], outline=(255, 240, 200, 200), width=3)
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, "prop_marker.png"))
    print("  Props/prop_marker.png  128x128")


if __name__ == "__main__":
    print("단서 (gen_evidence 그림에서 물건만):")
    for name, fn in (("prop_invitation", ge.invitation), ("prop_candles", ge.candles), ("prop_trophy", ge.trophy),
                     ("prop_gift", ge.gift), ("prop_watch", ge.watch), ("prop_calendar", ge.calendar)):
        save(bare(fn), name)
    print("장치·표시:")
    save(lever(), "prop_lever")
    save(grandfather_clock(), "prop_clock")
    save(records(), "prop_records")
    save(exit_arrow(), "prop_exit", max_side=256)
    marker()
