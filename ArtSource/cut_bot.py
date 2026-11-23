# -*- coding: utf-8 -*-
"""
도우미 로봇 그림. 턴어라운드 시트에서 정면 한 장을 잘라 몸통으로 쓰고,
그 검은 화면 자리에 맞춰 표정(빛나는 눈) 여섯 장을 그립니다.

※ 지금 게임은 gen_art.py 의 도형 로봇을 씁니다. 이 스크립트로 다시 바꾸려면
  Docs/도우미로봇.md 의 '그림' 절에 적힌 대로 HelperBotView 숫자도 함께 바꾸세요.

    python ArtSource/cut_bot.py
    python ArtSource/cut_bot.py ArtSource/sheets/bot_turnaround.webp

출력: Assets/_Project/Resources/Art/Bot/
    bot_body.png            몸통 — 검은 화면과 볼 터치까지 그림 그대로. 눈은 없습니다
    bot_face_{표정}.png      눈만. 몸통과 같은 캔버스라 그냥 겹치면 자리가 맞습니다
                            normal happy curious hint sleepy panic

끝에 전구 자리를 0~1 로 출력합니다. 막힘 표시 불빛을 전구에 얹을 때 씁니다
(지금 HelperBotView 는 도형 로봇용 안테나 불빛이라 이 값을 쓰지 않습니다).

배경은 흰 종이 위에 전구·부유 유닛의 주황 빛이 번져 있습니다. 빛을 잘라 버리면
떠 있는 느낌이 사라져서, 바깥에서 이어지는 밝은 칸은 '흰색을 투명으로' 바꿔
반투명한 빛으로 남깁니다. 외곽선(어두운 선)이 막아 주므로 크림색 몸은 안 번집니다.
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "..", "Assets", "_Project", "Resources", "Art", "Bot")
SHEET = os.path.join(ROOT, "sheets", "bot_turnaround.webp")

CW, CH = 400, 640       # 캔버스. 몸통과 표정이 같은 크기여야 합니다
FILL = 0.97             # 인물(빛 포함) 키가 캔버스 높이에서 차지하는 비율
LIGHT_LUM = 200         # 이보다 밝으면 종이나 빛
LINE_LUM = 110          # 이보다 어두우면 외곽선
LINE_GAP = 2
SCREEN_LUM = 60         # 화면의 검은 유리
SS = 4                  # 표정은 크게 그려 줄입니다

EYE = (255, 236, 182)   # 눈 — 전구와 같은 따뜻한 빛
EYE_GLOW = (255, 214, 140)
SWEAT = (170, 214, 245)


def luminance(rgb):
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


def grow(m):
    o = m.copy()
    o[1:] |= m[:-1]; o[:-1] |= m[1:]; o[:, 1:] |= m[:, :-1]; o[:, :-1] |= m[:, 1:]
    return o


def spread(seed, passable):
    region = seed.copy()
    while True:
        g = grow(region) & passable
        g |= seed
        if np.array_equal(g, region):
            return region
        region = g


def first_figure(rgb):
    """시트 왼쪽부터 첫 번째 인물(정면)의 열 범위. 빛까지 포함하려고 '흰색이 아닌 열' 로 나눕니다."""
    not_white = (luminance(rgb.astype(np.float32)) < 246).sum(axis=0) > 3
    xs = np.nonzero(not_white)[0]
    x0 = xs[0]
    x = x0
    while x < len(not_white) and (not_white[x] or not_white[x:x + 12].any()):
        x += 1
    return x0, x


def cut_body(sheet_path):
    rgb = np.asarray(Image.open(sheet_path).convert("RGB"))
    x0, x1 = first_figure(rgb)
    pad = 12
    rgb = rgb[:, max(0, x0 - pad):x1 + pad].astype(np.float32)
    lum = luminance(rgb)

    line = lum < LINE_LUM
    for _ in range(LINE_GAP):
        line = grow(line)
    light = (lum > LIGHT_LUM) & ~line
    border = np.zeros_like(light)
    border[0], border[-1], border[:, 0], border[:, -1] = True, True, True, True
    outside = spread(border & light, light)
    for _ in range(LINE_GAP):                       # 틈 막느라 남긴 테두리를 걷어 냅니다
        outside = outside | (grow(outside) & (lum > LIGHT_LUM))

    # 바깥 칸은 '흰색을 투명으로': 흰 종이는 완전히 투명, 주황 빛은 반투명한 주황.
    a = np.clip((255.0 - rgb).max(axis=2) / 255.0, 0, 1)
    a = np.clip(a * 1.6, 0, 1)                      # 빛이 너무 옅어지지 않게 조금 진하게
    safe = np.maximum(a, 1e-3)[..., None]
    unblended = np.clip((rgb - 255.0 * (1 - safe)) / safe, 0, 255)

    color = np.where(outside[..., None], unblended, rgb)
    alpha = np.where(outside, a, 1.0)
    rgba = np.dstack([color, alpha * 255]).astype(np.uint8)

    rows = np.nonzero((alpha > 0.06).sum(axis=1) > 2)[0]
    cols = np.nonzero((alpha > 0.06).sum(axis=0) > 2)[0]
    piece = Image.fromarray(rgba, "RGBA").crop((cols[0], rows[0], cols[-1] + 1, rows[-1] + 1))
    solid = (~outside)[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1]
    dark = (lum < SCREEN_LUM)[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1] & solid

    k = min(CH * FILL / piece.height, CW * 0.98 / piece.width)
    w, h = round(piece.width * k), round(piece.height * k)
    px, py = (CW - w) // 2, (CH - h) // 2
    canvas = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    canvas.alpha_composite(piece.resize((w, h), Image.LANCZOS), (px, py))

    screen = find_screen(dark)
    screen = tuple(float(v * k + o) for v, o in zip(screen, (px, py, px, py)))
    solid_rows = np.nonzero(solid.sum(axis=1) > 2)[0]
    bulb_y = float(py + solid_rows[0] * k + 30 * k)  # 전구 알 가운데쯤 (맨 위 외곽선에서 조금 아래)
    return canvas, screen, (CW / 2, bulb_y)


def find_screen(dark):
    """검은 화면 — 위쪽 절반에서 가장 많이 어두운 줄·열이 모인 네모."""
    upper = dark.copy()
    upper[dark.shape[0] // 2:] = False
    rows = upper.sum(axis=1)
    cols = upper.sum(axis=0)
    ry = np.nonzero(rows > rows.max() * 0.5)[0]
    cx = np.nonzero(cols > cols.max() * 0.5)[0]
    return cx[0], ry[0], cx[-1] + 1, ry[-1] + 1


# ── 표정 ─────────────────────────────────────────────────────────────────────

def face(kind, screen, bulb):
    x0, y0, x1, y1 = screen
    sw, sh = x1 - x0, y1 - y0
    u = sw / 156.0                                   # 예전 얼굴판 폭(156px) 기준 배율
    cy = y0 + sh * 0.42                              # 볼 터치보다 위
    lx, rx = x0 + sw * 0.34, x0 + sw * 0.66

    big = Image.new("RGBA", (CW * SS, CH * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)

    def S(v):
        return v * SS

    def pill(cx, cy_, w, h, color=EYE):
        d.rounded_rectangle([S(cx - w), S(cy_ - h), S(cx + w), S(cy_ + h)], radius=S(w), fill=color)

    def stroke(points, width, color=EYE):
        d.line([(S(x), S(y)) for x, y in points], fill=color, width=round(S(width)), joint="curve")
        r = width / 2
        for x, y in (points[0], points[-1]):
            d.ellipse([S(x - r), S(y - r), S(x + r), S(y + r)], fill=color)

    if kind in ("normal", "hint"):
        w, h = (14 * u, 30 * u) if kind == "normal" else (17 * u, 35 * u)
        for cx in (lx, rx):
            pill(cx, cy, w, h)
    elif kind == "happy":
        for cx in (lx, rx):
            stroke([(cx - 19 * u, cy + 8 * u), (cx - 6 * u, cy - 10 * u),
                    (cx + 6 * u, cy - 10 * u), (cx + 19 * u, cy + 8 * u)], 10 * u)
    elif kind == "curious":
        pill(lx, cy, 14 * u, 30 * u)
        pill(rx, cy, 11 * u, 19 * u)
    elif kind == "sleepy":
        for cx in (lx, rx):
            stroke([(cx - 15 * u, cy), (cx + 15 * u, cy)], 10 * u, EYE + (215,))
    elif kind == "panic":
        for cx in (lx, rx):
            d.ellipse([S(cx - 11 * u), S(cy - 11 * u), S(cx + 11 * u), S(cy + 11 * u)], fill=EYE)
        sx, sy = x1 - sw * 0.14, y0 + sh * 0.14      # 땀 한 방울
        d.ellipse([S(sx - 9 * u), S(sy - 13 * u), S(sx + 9 * u), S(sy + 13 * u)], fill=SWEAT + (225,))

    eyes = big.resize((CW, CH), Image.LANCZOS)

    # 화면 속에서 빛나 보이게 눈 둘레로 흐린 빛을 한 겹 깝니다.
    halo = Image.new("RGBA", (CW, CH), EYE_GLOW + (0,))
    halo.putalpha(eyes.getchannel("A").filter(ImageFilter.GaussianBlur(6 * u)).point(lambda v: v * 120 // 255))
    out = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    out.alpha_composite(halo)
    out.alpha_composite(eyes)

    if kind == "hint":
        # 머리 위 전구가 환하게 켜집니다 — '알았다!'
        glow = Image.new("RGBA", (CW, CH), (255, 220, 140, 0))
        m = Image.new("L", (CW, CH), 0)
        bx, by = bulb
        r = 30 * u
        ImageDraw.Draw(m).ellipse([bx - r, by - r, bx + r, by + r], fill=230)
        glow.putalpha(m.filter(ImageFilter.GaussianBlur(14 * u)))
        out.alpha_composite(glow)
    return out


def main():
    sheet = sys.argv[1] if len(sys.argv) > 1 else SHEET
    body, screen, bulb = cut_body(sheet)
    os.makedirs(OUT, exist_ok=True)
    body.save(os.path.join(OUT, "bot_body.png"))
    print("  Bot/bot_body.png")
    for k in ("normal", "happy", "curious", "hint", "sleepy", "panic"):
        face(k, screen, bulb).save(os.path.join(OUT, f"bot_face_{k}.png"))
        print(f"  Bot/bot_face_{k}.png")

    print(f"\n전구 자리 (캔버스 왼쪽 위 기준 0~1):")
    print(f"  new Vector2({bulb[0] / CW:.3f}f, {bulb[1] / CH:.3f}f)")


if __name__ == "__main__":
    main()
