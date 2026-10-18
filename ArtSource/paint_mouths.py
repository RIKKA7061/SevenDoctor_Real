# -*- coding: utf-8 -*-
"""
손그림 포트레이트에 '벌린 입' 덮개를 그립니다. 말하는 동안 게임이 이걸 켰다 껐다 합니다.
다문 입은 원래 그림에 있으므로 벌린 입 한 장이면 됩니다.

    python ArtSource/paint_mouths.py

출력: Assets/_Project/Resources/Art/Portraits/{그림}_mouth.png
    그림과 같은 700x1200 캔버스에 입만 불투명합니다. 그냥 겹치면 자리가 맞습니다.

입 위치는 아래 MOUTHS 에 캔버스 좌표로 적습니다. make_portraits.py 로 그림을 다시 뽑으면
위치가 바뀔 수 있으니 이것도 다시 돌리세요. 여기 없는 그림(뒷모습 등)은 입이 안 움직입니다.

    box    벌린 입 타원 (x0, y0, x1, y1). 다문 입을 통째로 덮을 만큼 잡습니다.
    cover  다문 입이 커서 타원 밖으로 삐져나올 때, 먼저 살색으로 덮을 범위
    skin   그때 쓸 살색
    lip    입술 색. 주면 먹선 대신 이 색으로 테두리를 두릅니다 (립스틱 바른 인물)
"""
import os
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.abspath(__file__))
PORTRAITS = os.path.join(ROOT, "..", "Assets", "_Project", "Resources", "Art", "Portraits")

SS = 4          # 작게 그리는 거라 크게 그린 뒤 줄여서 가장자리를 부드럽게 합니다
PAD = 10

INK    = (44, 28, 30)      # 그림 외곽선과 같은 먹색
INSIDE = (122, 52, 58)     # 입 안
TONGUE = (212, 126, 124)

MOUTHS = {
    # 슬픔 — 물결 모양 입. 오른쪽 그림은 머리만 목(x=297.4) 기준으로 뒤집은 것이라
    # 좌표도 그 축으로 뒤집었습니다 (x' = 594.9 - x).
    "sorrow":            dict(box=(267, 364, 335, 392)),
    "sorrow_side_left":  dict(box=(244, 365, 310, 392)),
    "sorrow_side_right": dict(box=(285, 365, 351, 392)),
    "sorrow_back_left":  dict(box=(179, 347, 226, 368)),
    "sorrow_back_right": dict(box=(369, 347, 416, 368)),

    "arrogance": dict(box=(311, 256, 343, 272)),
    "greed":     dict(box=(376, 245, 410, 263)),
    "wrath":     dict(box=(315, 279, 354, 298)),
    # 색욕 — 입술이 크고 한쪽이 올라가 있어서 살색으로 먼저 덮습니다.
    "lust":      dict(box=(296, 388, 346, 418), cover=(286, 384, 350, 421),
                      skin=(250, 220, 209), lip=(190, 70, 92)),
}


def draw(spec):
    x0, y0, x1, y1 = spec["box"]
    cover = spec.get("cover")
    left = min(x0, cover[0]) if cover else x0
    top = min(y0, cover[1]) if cover else y0
    right = max(x1, cover[2]) if cover else x1
    bottom = max(y1, cover[3]) if cover else y1
    ox, oy = left - PAD, top - PAD
    w, h = (right - left + PAD * 2) * SS, (bottom - top + PAD * 2) * SS
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))

    def local(box):
        return [(box[0] - ox) * SS, (box[1] - oy) * SS, (box[2] - ox) * SS, (box[3] - oy) * SS]

    if cover:
        # 가장자리를 흐려서 덮은 자국이 안 남게 합니다.
        patch = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        ImageDraw.Draw(patch).ellipse(local(cover), fill=spec["skin"] + (255,))
        soft = patch.getchannel("A").filter(ImageFilter.GaussianBlur(2.2 * SS))
        patch.putalpha(soft)
        img.alpha_composite(patch)

    d = ImageDraw.Draw(img)
    outer = local(spec["box"])
    lip = spec.get("lip")
    edge = (4.2 if lip else 2.6) * SS
    inner = [outer[0] + edge, outer[1] + edge, outer[2] - edge, outer[3] - edge]
    d.ellipse(outer, fill=lip or INK)
    d.ellipse(inner, fill=INSIDE)

    # 혀 — 입 안쪽 아래에만. 입 모양 밖으로 나가지 않게 안쪽 타원으로 오려 냅니다.
    tongue = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(tongue).ellipse([inner[0] + 3 * SS, (inner[1] + inner[3]) / 2,
                                    inner[2] - 3 * SS, inner[3] + 4 * SS], fill=TONGUE)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).ellipse(inner, fill=255)
    tongue.putalpha(ImageChops.multiply(tongue.getchannel("A"), mask))
    img.alpha_composite(tongue)
    return img.resize((w // SS, h // SS), Image.LANCZOS), ox, oy


def main():
    for name, spec in MOUTHS.items():
        path = os.path.join(PORTRAITS, name + ".png")
        if not os.path.exists(path):
            print(f"  건너뜀: Portraits/{name}.png 가 없습니다.")
            continue
        patch, ox, oy = draw(spec)
        canvas = Image.new("RGBA", Image.open(path).size, (0, 0, 0, 0))
        canvas.alpha_composite(patch, (ox, oy))
        canvas.save(os.path.join(PORTRAITS, name + "_mouth.png"))
        print(f"  Portraits/{name}_mouth.png")


if __name__ == "__main__":
    main()
