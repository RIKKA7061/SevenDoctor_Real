# -*- coding: utf-8 -*-
"""
배경 그림을 게임 배경 규격(1920x1080)에 맞춥니다. 비율이 다르면 화면을 꽉 채우도록
키운 뒤 가운데를 기준으로 넘치는 가장자리만 잘라 냅니다 (게임도 배경을 늘려 꽉 채우므로
미리 맞춰 두면 그림이 찌그러지지 않습니다).

    python ArtSource/fit_background.py ArtSource/sheets/bg_lab_source.webp bg_lab

출력: Assets/_Project/Resources/Art/Backgrounds/{배경키}.png
배경이 바뀌면 그 방 핫스팟(Hotspots 탭 x/y/w/h)도 새 그림의 물건 자리에 맞춰 다시 잡으세요.
"""
import os, sys
from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "..", "Assets", "_Project", "Resources", "Art", "Backgrounds")
W, H = 1920, 1080


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)
    src = Image.open(sys.argv[1]).convert("RGB")
    k = max(W / src.width, H / src.height)
    img = src.resize((round(src.width * k), round(src.height * k)), Image.LANCZOS)
    x, y = (img.width - W) // 2, (img.height - H) // 2
    img = img.crop((x, y, x + W, y + H))
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, sys.argv[2] + ".png"))
    print(f"  Backgrounds/{sys.argv[2]}.png  원본 {src.width}x{src.height} → {W}x{H} (가장자리 {x}, {y}px 잘림)")


if __name__ == "__main__":
    main()
