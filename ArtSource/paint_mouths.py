# -*- coding: utf-8 -*-
"""
손그림 포트레이트에 '벌린 입' 덮개를 그립니다. 말하는 동안 게임이 이걸 켰다 껐다 합니다.
다문 입은 원래 그림에 있으므로 벌린 입 한 장이면 됩니다.

    python ArtSource/paint_mouths.py

출력: Assets/_Project/Resources/Art/Portraits/{그림}_mouth.png
    그림과 같은 700x1200 캔버스에 입만 불투명합니다. 그냥 겹치면 자리가 맞습니다.
    _left 그림의 입은 좌우를 뒤집어 _right 에도 씁니다 (그림도 그렇게 만들었으니까).

입 위치는 아래 MOUTHS 에 캔버스 좌표로 적습니다. make_sorrow.py / cut_turnaround.py 로 그림을 다시
자르면 위치가 바뀔 수 있으니 이것도 다시 돌리세요. 뒷모습처럼 입이 안 보이는 그림은
빼 두면 그 방향에서는 입이 안 움직입니다.
"""
import os
from PIL import Image, ImageChops, ImageDraw, ImageOps

ROOT = os.path.dirname(os.path.abspath(__file__))
PORTRAITS = os.path.join(ROOT, "..", "Assets", "_Project", "Resources", "Art", "Portraits")

SS = 4          # 작게 그리는 거라 크게 그린 뒤 줄여서 가장자리를 부드럽게 합니다
PAD = 8

INK    = (44, 28, 30)      # 그림 외곽선과 같은 먹색
INSIDE = (122, 52, 58)     # 입 안
TONGUE = (212, 126, 124)

MOUTHS = {
    # 새 슬픔 시트(make_sorrow.py). 물결 모양 입을 통째로 덮는 타원.
    # 오른쪽 그림은 머리만 목(x=301.5) 기준으로 뒤집은 것이라 좌표도 그 축으로 뒤집었습니다.
    "sorrow": ("oval", (270, 390, 338, 418)),
    "sorrow_side_left": ("oval", (252, 388, 316, 414)),
    "sorrow_side_right": ("oval", (287, 388, 351, 414)),
    "sorrow_back_left": ("oval", (197, 366, 235, 390)),
    "sorrow_back_right": ("oval", (368, 366, 406, 390)),
}


def _scaled(points, ox, oy):
    return [((x - ox) * SS, (y - oy) * SS) for x, y in points]


def _shrink(points, amount):
    """무게중심 쪽으로 amount 만큼 당깁니다. 테두리 안쪽 면을 만들 때 씁니다."""
    cx = sum(p[0] for p in points) / len(points)
    cy = sum(p[1] for p in points) / len(points)
    out = []
    for x, y in points:
        dx, dy = x - cx, y - cy
        d = max((dx * dx + dy * dy) ** 0.5, 1e-6)
        k = max(d - amount, 0) / d
        out.append((cx + dx * k, cy + dy * k))
    return out


def draw_oval(box):
    x0, y0, x1, y1 = box
    ox, oy = x0 - PAD, y0 - PAD
    w, h = (x1 - x0 + PAD * 2) * SS, (y1 - y0 + PAD * 2) * SS
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    outer = [(x0 - ox) * SS, (y0 - oy) * SS, (x1 - ox) * SS, (y1 - oy) * SS]
    edge = 2.6 * SS
    inner = [outer[0] + edge, outer[1] + edge, outer[2] - edge, outer[3] - edge]
    d.ellipse(outer, fill=INK)
    d.ellipse(inner, fill=INSIDE)

    # 혀 — 입 안쪽 아래에만. 입 모양 밖으로 나가지 않게 안쪽 타원으로 오려 냅니다.
    tongue = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(tongue).ellipse([inner[0] + 3 * SS, (inner[1] + inner[3]) / 2,
                                    inner[2] - 3 * SS, inner[3] + 4 * SS], fill=TONGUE)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).ellipse(inner, fill=255)
    tongue.putalpha(ImageChops.multiply(tongue.getchannel("A"), mask))
    img.alpha_composite(tongue)
    return img, ox, oy


def draw_wedge(points):
    xs, ys = [p[0] for p in points], [p[1] for p in points]
    ox, oy = min(xs) - PAD, min(ys) - PAD
    w, h = (max(xs) - ox + PAD) * SS, (max(ys) - oy + PAD) * SS
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon(_scaled(points, ox, oy), fill=INK)
    d.polygon(_scaled(_shrink(points, 2.2), ox, oy), fill=INSIDE)
    return img, ox, oy


def paint(name, kind, spec):
    base = Image.open(os.path.join(PORTRAITS, name + ".png"))
    patch, ox, oy = draw_oval(spec) if kind == "oval" else draw_wedge(spec)
    patch = patch.resize((patch.width // SS, patch.height // SS), Image.LANCZOS)

    canvas = Image.new("RGBA", base.size, (0, 0, 0, 0))
    canvas.alpha_composite(patch, (ox, oy))
    return canvas


def main():
    for name, (kind, spec) in MOUTHS.items():
        img = paint(name, kind, spec)
        img.save(os.path.join(PORTRAITS, name + "_mouth.png"))
        print(f"  Portraits/{name}_mouth.png")

        if name.endswith("_left"):
            right = name[:-len("_left")] + "_right"
            if right not in MOUTHS and os.path.exists(os.path.join(PORTRAITS, right + ".png")):
                ImageOps.mirror(img).save(os.path.join(PORTRAITS, right + "_mouth.png"))
                print(f"  Portraits/{right}_mouth.png")


if __name__ == "__main__":
    main()
