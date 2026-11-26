# -*- coding: utf-8 -*-
"""
빛 효과용 그림 (RoomLights). 전부 흰색 + 알파 그라데이션이라 게임에서 색을 입혀 씁니다.

    python ArtSource/gen_fx.py

출력: Assets/_Project/Resources/Art/FX/
    fx_glow.png    256x256   부드럽게 번지는 빛 — 전구 후광, 벽에 번진 빛, 테두리 조명 점
    fx_core.png    128x128   작고 진한 빛 — 전구 알 자체가 하얗게 타는 부분
    fx_beam.png    256x512   위가 좁고 아래가 넓은 빛줄기 — 갓 아래로 떨어지는 빛 (위쪽 가운데가 광원)
    fx_spark.png    64x64    네 갈래 반짝임 — 크리스탈 둘레에 떠오르는 마법 가루

그라데이션이 압축되면 계단이 생겨서, ArtImportSettings 가 FX 폴더만 무압축으로 들입니다.
"""
import math, os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "..", "Assets", "_Project", "Resources", "Art", "FX")


def save(alpha, name):
    a = np.clip(alpha, 0, 1)
    h, w = a.shape
    rgba = np.zeros((h, w, 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = (a * 255 + 0.5).astype(np.uint8)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, name + ".png"))
    print(f"  FX/{name}.png  {w}x{h}")


def radial(size):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    c = (size - 1) / 2
    return np.hypot(x - c, y - c) / c          # 가운데 0, 가장자리 1


def glow():
    r = radial(256)
    # 가운데가 또렷하고 꼬리는 짧은 빛. 꼬리를 길게 끌면 화면 전체가 뿌옇게 떠 보여서
    # 번짐은 아주 옅게만 남깁니다 ('선명하고 깨끗하게').
    a = 0.85 * np.exp(-(r / 0.20) ** 2) + 0.15 * np.exp(-(r / 0.42) ** 2)
    a *= np.clip((1 - r) / 0.12, 0, 1)        # 네모 가장자리에서 딱 끊기지 않게
    return a


def core():
    r = radial(128)
    # 전구 알 — 가운데는 하얗게 꽉 차고 가장자리만 짧게 풀림
    return np.clip(1.25 * np.exp(-(r / 0.34) ** 4), 0, 1) * np.clip((1 - r) / 0.15, 0, 1)


def beam():
    h, w = 512, 256
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    t = y / (h - 1)                            # 0 = 광원(위), 1 = 바닥(아래)
    half = 0.10 + 0.90 * t                     # 위는 폭의 10%, 아래는 전체
    dx = np.abs(x - (w - 1) / 2) / ((w - 1) / 2) / half
    across = np.clip(1 - dx, 0, 1) ** 1.6      # 가운데가 밝고 가장자리로 흐려짐
    along = (1 - t) ** 1.3 * np.clip(t / 0.04, 0, 1)   # 아래로 갈수록 옅어짐
    return across * along


def spark():
    s = 64
    y, x = np.mgrid[0:s, 0:s].astype(np.float32)
    c = (s - 1) / 2
    dx, dy = np.abs(x - c) / c, np.abs(y - c) / c
    rays = np.exp(-(dx / 0.07) ** 2) * (1 - dy) ** 2 + np.exp(-(dy / 0.07) ** 2) * (1 - dx) ** 2
    dot = np.exp(-(np.hypot(dx, dy) / 0.22) ** 2)
    return np.clip(rays * 0.9 + dot, 0, 1)


if __name__ == "__main__":
    save(glow(), "fx_glow")
    save(core(), "fx_core")
    save(beam(), "fx_beam")
    save(spark(), "fx_spark")
