# -*- coding: utf-8 -*-
"""
턴어라운드 시트(한 장에 여러 방향이 나란히 그려진 그림)를 포트레이트로 자릅니다.

    python ArtSource/cut_turnaround.py ArtSource/sheets/wrath_turnaround.png wrath
    python ArtSource/cut_turnaround.py 시트.png 키 front,side_left,back_left,back

왼쪽부터 그려진 순서대로 방향 이름을 붙입니다. 기본값은 front,side_left,back_left,back.
_left 로 끝나는 방향은 좌우를 뒤집어 _right 도 같이 만듭니다 (시트에 따로 있으면 그걸 씁니다).

출력: Assets/_Project/Resources/Art/Portraits/
    {키}.png            front
    {키}_{방향}.png     나머지

Portraits 폴더에 있는 인물은 그림에 얼굴이 이미 있는 것으로 보고, 게임이 공용 표정을
겹치지 않습니다 (PortraitView). 도형 플레이스홀더(gen_art.py)는 Characters 폴더라
서로 덮어쓰지 않습니다.

배경 지우기는 '테두리에서 시작해 종이 색인 칸만 따라 칠하기' 입니다. 손그림 외곽선은
붓 자국 사이에 틈이 있어서, 외곽선을 살짝 부풀려 틈을 막은 뒤 칠하고 막았던 만큼만
다시 넓힙니다. 종이 색(밝은 무채색)이 아니면 칠하지 않으므로 머리카락·피부로는 안
번지고, 외투처럼 종이와 비슷한 색은 외곽선이 막아 줍니다.
"""
import os, sys
import numpy as np
from PIL import Image, ImageOps

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "..", "Assets", "_Project", "Resources", "Art", "Portraits")

CW, CH = 700, 1200     # 다른 포트레이트와 같은 캔버스 — 게임의 초상화 칸이 이 비율입니다
FILL = 0.95            # 가장 큰 인물의 키가 캔버스 높이에서 차지하는 비율
BOTTOM_MARGIN = 20     # 발밑 여백

LINE_LUM = 110         # 이보다 어두우면 외곽선. 배경 칠하기가 여기서 멈춥니다
LINE_GAP = 2           # 외곽선을 이만큼 부풀려 붓 자국 사이 틈을 막습니다
PAPER_LUM = 180        # 종이는 이보다 밝고
PAPER_SAT = 20         # 거의 무채색입니다. 머리카락(보라)·피부는 여기서 걸러집니다
PAPER_LUM_GAP = 185    # 다리 사이 같은 좁은 틈은 이만큼 확실히 밝은 종이만 따라 들어갑니다
SHADOW_LUM = 140       # 발밑 그림자. 바닥 띠 안에서만 이 정도 어두운 회색까지 지웁니다
GROUND_BAND = 0.05     # 바닥 띠 높이 (인물 키 대비)
EDGE_INK = (34, 28, 30)
MIN_GAP = 6            # 인물 사이 빈 열이 이만큼 이어져야 다른 인물로 봅니다
MIN_FIGURE_PIXELS = 4000


def luminance(rgb):
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


def grow(mask):
    """상하좌우로 한 칸 넓힙니다."""
    out = mask.copy()
    out[1:, :] |= mask[:-1, :]
    out[:-1, :] |= mask[1:, :]
    out[:, 1:] |= mask[:, :-1]
    out[:, :-1] |= mask[:, 1:]
    return out


def find_background(rgb):
    lum = luminance(rgb.astype(np.float32))
    sat = rgb.max(axis=2).astype(np.int16) - rgb.min(axis=2).astype(np.int16)
    paper = (lum > PAPER_LUM) & (sat < PAPER_SAT)

    line = lum < LINE_LUM
    for _ in range(LINE_GAP):
        line = grow(line)
    passable = paper & ~line

    # 테두리에 닿은 종이 칸에서 시작해 이웃한 종이 칸으로 번져 나갑니다.
    border = np.zeros_like(passable)
    border[0, :] = border[-1, :] = True
    border[:, 0] = border[:, -1] = True
    bg = spread(border & passable, passable)

    # 틈을 막느라 외곽선 옆에 남겨 둔 종이를 도로 걷어 냅니다.
    line = lum < LINE_LUM
    for _ in range(LINE_GAP):
        bg = grow(bg) & paper

    # 다리 사이 같은 좁은 틈은 부풀린 외곽선에 막혀 못 들어갔습니다. 대개 발밑 그림자로
    # 이어져 있으니, 그림자(바닥 띠 안의 어두운 회색)를 지우면서 거기서부터 진짜
    # 외곽선만 막고 다시 번집니다. 외투도 종이와 거의 같은 색이라 '더 밝은 종이' 로
    # 한 번 더 거릅니다.
    top, bottom = figure_rows(~bg)
    ground = np.zeros_like(bg)
    ground[int(bottom - (bottom - top) * GROUND_BAND):, :] = True
    shadow = ground & (lum > SHADOW_LUM) & (sat < PAPER_SAT + 5)
    gap = (lum > PAPER_LUM_GAP) & (sat < PAPER_SAT)
    return spread(bg, (shadow | gap) & ~line)


def figure_rows(fg):
    """인물이 차지한 세로 범위. 종이 얼룩 몇 점이 섞여도 흔들리지 않게 한 줄에
    어느 정도 이상 칠해진 줄만 셉니다."""
    rows = np.where(fg.sum(axis=1) > 20)[0]
    return rows.min(), rows.max() + 1


def spread(seed, passable):
    """seed 에서 시작해 passable 인 이웃으로 더 이상 안 넓어질 때까지 번집니다."""
    region = seed.copy()
    while True:
        grown = grow(region) & (passable | seed)
        if np.array_equal(grown, region):
            return region
        region = grown


def soft_alpha(rgb, fg, bg_lum):
    """배경과 맞닿은 한두 픽셀은 외곽선과 종이가 섞인 색이라 그대로 두면 회색 테두리가
    남습니다. 어두운 정도만큼만 불투명한 '잉크' 로 바꿔 매끄럽게 떨어지게 합니다."""
    alpha = fg.astype(np.float32)
    out = rgb.astype(np.float32).copy()

    edge = np.zeros_like(fg)
    ring = fg.copy()
    for _ in range(2):
        near_bg = np.zeros_like(fg)
        bgm = ~ring
        near_bg[1:, :] |= bgm[:-1, :]
        near_bg[:-1, :] |= bgm[1:, :]
        near_bg[:, 1:] |= bgm[:, :-1]
        near_bg[:, :-1] |= bgm[:, 1:]
        layer = ring & near_bg
        edge |= layer
        ring &= ~layer

    lum = luminance(rgb.astype(np.float32))
    a = np.clip((bg_lum - lum) / (bg_lum - 40.0), 0.0, 1.0)
    alpha[edge] = a[edge]
    out[edge] = EDGE_INK
    return out, alpha


def split_figures(fg):
    """빈 열을 경계로 인물을 나눕니다."""
    cols = fg.sum(axis=0)
    runs, start, gap = [], None, 0
    for x, n in enumerate(cols):
        if n > 0:
            if start is None:
                start = x
            gap = 0
        elif start is not None:
            gap += 1
            if gap >= MIN_GAP:
                runs.append((start, x - gap + 1))
                start, gap = None, 0
    if start is not None:
        runs.append((start, len(cols)))
    return [(x0, x1) for x0, x1 in runs if fg[:, x0:x1].sum() >= MIN_FIGURE_PIXELS]


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)

    sheet_path, key = sys.argv[1], sys.argv[2]
    poses = (sys.argv[3] if len(sys.argv) > 3 else "front,side_left,back_left,back").split(",")

    rgb = np.asarray(Image.open(sheet_path).convert("RGB"))
    lum = luminance(rgb.astype(np.float32))

    bg = find_background(rgb)
    bg_lum = float(np.median(lum[bg]))

    # 종이에 묻은 먼지 점을 버립니다. 인물의 허리 높이에서 시작해 이어진 것만 남깁니다 —
    # 손끝·머리카락 한 올까지 외곽선으로 몸에 이어져 있습니다.
    fg = ~bg
    top, bottom = figure_rows(fg)
    waist = np.zeros_like(fg)
    waist[top + (bottom - top) // 2, :] = True
    fg = spread(fg & waist, fg)

    figures = split_figures(fg)
    if len(figures) != len(poses):
        sys.exit(f"인물 {len(figures)}명을 찾았는데 방향 이름은 {len(poses)}개입니다: {figures}")

    color, alpha = soft_alpha(rgb, fg, bg_lum)
    rgba = np.dstack([color, alpha * 255.0]).clip(0, 255).astype(np.uint8)

    # 모든 방향을 같은 배율·같은 바닥선에 놓아야 방향을 바꿀 때 인물이 튀지 않습니다.
    top, bottom = figure_rows(fg)
    scale = (CH * FILL) / (bottom - top)

    os.makedirs(OUT, exist_ok=True)
    made = {}
    for pose, (x0, x1) in zip(poses, figures):
        cut = fg[top:bottom, x0:x1]
        # 가로 기준은 아래쪽 40%(외투·다리)의 무게중심. 머리카락이 날려도 몸이 제자리에 섭니다.
        low = cut[int(cut.shape[0] * 0.6):, :]
        cx = (np.nonzero(low)[1].mean() if low.any() else cut.shape[1] / 2)

        piece = Image.fromarray(rgba[top:bottom, x0:x1], "RGBA")
        w, h = round(piece.width * scale), round(piece.height * scale)
        piece = piece.resize((w, h), Image.LANCZOS)

        canvas = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
        px = round(CW / 2 - cx * scale)
        py = CH - BOTTOM_MARGIN - h
        if px < 0 or px + w > CW:
            print(f"  경고: {pose} 가 캔버스 폭을 넘어 가장자리가 잘립니다 ({px}..{px + w})")
        canvas.paste(piece, (px, py))   # 넘치는 부분은 paste 가 알아서 잘라 냅니다
        made[pose] = canvas

    for pose in list(made):
        if pose.endswith("_left"):
            right = pose[:-len("_left")] + "_right"
            if right not in made:
                made[right] = ImageOps.mirror(made[pose])

    for pose, img in made.items():
        name = key if pose == "front" else f"{key}_{pose}"
        img.save(os.path.join(OUT, name + ".png"))
        print(f"  Portraits/{name}.png")


if __name__ == "__main__":
    main()
