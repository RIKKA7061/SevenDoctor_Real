# -*- coding: utf-8 -*-
"""
퍼즐에 쓰는 그림.

    python ArtSource/gen_puzzles.py

출력: Assets/_Project/Resources/Art/Puzzles/
    pz_invitation.png   1024x1024   슬라이딩 퍼즐 (Puzzles 탭 '정답' 칸: pz_invitation:3)

슬라이딩 퍼즐 그림을 만들 때 지킬 것:
  · 정사각형. 게임이 가로세로 같은 칸수로 자릅니다.
  · 어느 조각도 다른 조각과 똑같아 보이면 안 됩니다. 빈 배경만 있는 칸이 둘이면
    서로 바뀌어도 눈으로 알 수 없어서, 다 맞춘 것 같은데 안 끝나는 일이 생깁니다.
  · 오른쪽 아래 칸은 퍼즐의 빈칸입니다. 다 맞춘 순간에만 보이므로 반전을 숨기기 좋습니다.
"""
import os
from PIL import Image, ImageChops, ImageDraw, ImageFilter

import gen_evidence as ge
from gen_evidence import N, c, line, layer_like

OUT = os.path.join(ge.ROOT, "..", "Assets", "_Project", "Resources", "Art", "Puzzles")


def desk():
    """나무 책상 바탕. 초대장 둘레의 칸도 판자 결이 달라서 서로 구별됩니다."""
    img = Image.new("RGBA", (N * ge.S, N * ge.S), (124, 88, 60, 255))
    d = ImageDraw.Draw(img)
    for i, y in enumerate(range(0, N, 146)):
        tone = (116, 80, 54) if i % 2 else (132, 94, 64)
        d.rectangle([0, c(y), c(N), c(y + 146)], fill=tone)
        line(d, [(0, y), (N, y)], (84, 56, 38), 4)
        for k in range(3):   # 옹이·결
            x = (i * 311 + k * 257) % N
            line(d, [(x, y + 40 + k * 30), (x + 150, y + 46 + k * 30)], (100, 68, 46), 3)
    noise = Image.effect_noise((N * ge.S // 4, N * ge.S // 4), 26).resize(img.size, Image.BILINEAR)
    img.alpha_composite(Image.merge("RGBA", (noise, noise, noise, Image.new("L", img.size, 20))))
    return img


def invitation_puzzle():
    img = desk()
    card = ge.invitation_card(img, for_puzzle=True)
    ge.shadow(img, card, offset=(10, 14), blur=14, alpha=110)
    img.alpha_composite(card)
    return img


# 초대장 둘레만 정사각형으로 잘라 씁니다. 책상 여백이 넓으면 조각 절반이 나무판뿐이라
# 맞추는 재미도 단서도 없습니다. 3x3 으로 자르면 칸마다 풍선·제목·케이크·글씨·낙서·날짜가
# 하나씩 들어가고, 오른쪽 아래 칸(빈칸)에 약속 메모가 옵니다.
CROP = (142, 140, 882, 880)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    whole = ge.finish(invitation_puzzle())
    whole.crop(CROP).resize((N, N), Image.LANCZOS).save(os.path.join(OUT, "pz_invitation.png"))
    print("  Puzzles/pz_invitation.png")
