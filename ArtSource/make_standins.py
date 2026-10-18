# -*- coding: utf-8 -*-
"""
아직 그림이 없는 인물의 임시 그림. 슬픔 그림(방향·입 포함)의 머리색과 옷 색만 바꿔서
서로 다른 사람으로 보이게 합니다. 진짜 그림이 오면 같은 이름으로 덮어쓰면 됩니다.

    python ArtSource/make_standins.py

    박사 doctor   갈색 머리 + 흰 가운
    나태 sloth    잿빛 초록 머리 + 칙칙한 초록 옷
    교만 pride    검은 머리 + 짙은 보라 옷

출력: Assets/_Project/Resources/Art/Portraits/{키}.png, {키}_{방향}.png, …_mouth.png
슬픔 그림을 다시 뽑았다면(make_portraits.py, paint_mouths.py) 이것도 다시 돌리세요.
얼굴은 슬픔 그대로라 우는 얼굴입니다 — 어디까지나 자리 채움입니다.
"""
import glob, os
import numpy as np
from PIL import Image

import make_portraits as mp

PORTRAITS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Project", "Resources", "Art", "Portraits")
SOURCE = "sorrow"
SOURCE_HAIR = (30, 120, 220)

# (색상 각도, 채도, 밝기 배율) — 머리카락 / 옷
STANDINS = {
    "doctor": dict(hair=(25, 0.55, 0.62), coat=(40, 0.04, 1.04)),
    "sloth":  dict(hair=(100, 0.30, 0.82), coat=(90, 0.20, 0.82)),
    "pride":  dict(hair=(275, 0.35, 0.42), coat=(270, 0.22, 0.52)),
}


def coat_like(rgb):
    """슬픔의 크림색 외투. 피부(붉은 기)와 장화(갈색)는 빼고 누르스름한 천만 고릅니다.
    천에 그려진 얼룩·그늘까지 잡아야 어두운 색으로 바꿨을 때 크림색 점이 안 남습니다."""
    r, g, b = (rgb[..., i].astype(np.int16) for i in range(3))
    lum = r * 0.299 + g * 0.587 + b * 0.114
    cloth = (lum > 95) & (r - g < 22) & (r - b > 6) & (r - b < 95)
    skin_or_boot = (r - g >= 22)
    # 천 사이에 낀 한두 픽셀 얼룩을 메웁니다 (닫힘 연산). 피부·장화는 다시 빼고요.
    closed = cloth.copy()
    for _ in range(2):
        closed = mp.ct.grow(closed)
    for _ in range(2):
        closed &= ~mp.ct.grow(~closed)
    return closed & (lum > 70) & ~skin_or_boot


def recolor(img, mask, hue_deg, sat, val):
    hsv = np.asarray(img.convert("RGB").convert("HSV")).astype(np.float32)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    h[mask] = hue_deg / 360 * 255
    s[mask] = np.clip(s[mask] * 0 + sat * 255 * (0.6 + 0.4 * s[mask] / 255), 0, 255)
    v[mask] = np.clip(v[mask] * val, 0, 255)
    out = Image.fromarray(hsv.astype(np.uint8), "HSV").convert("RGB")
    out.putalpha(img.getchannel("A"))
    return out


def make(key, spec):
    for path in sorted(glob.glob(os.path.join(PORTRAITS, SOURCE + "*.png"))):
        name = os.path.basename(path)
        target = os.path.join(PORTRAITS, key + name[len(SOURCE):])
        img = Image.open(path).convert("RGBA")
        if name.endswith("_mouth.png"):
            img.save(target)            # 입 자리는 같으니 그대로
            continue
        rgb = np.asarray(img.convert("RGB"))
        alpha = np.asarray(img.getchannel("A")) > 0
        hair = mp.hair_like(rgb, SOURCE_HAIR) & alpha
        coat = coat_like(rgb) & alpha & ~hair
        img = recolor(img, hair, *spec["hair"])
        img = recolor(img, coat, *spec["coat"])
        img.save(target)
        print(f"  Portraits/{os.path.basename(target)}")


if __name__ == "__main__":
    for key, spec in STANDINS.items():
        make(key, spec)
