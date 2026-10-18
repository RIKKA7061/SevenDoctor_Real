# -*- coding: utf-8 -*-
"""
캐릭터 시트(ArtSource/sheets/{키}_sheet.webp)에서 포트레이트를 뽑습니다.

    python ArtSource/make_portraits.py            전부
    python ArtSource/make_portraits.py wrath lust  고른 것만

시트는 왼쪽에 전신(정면) 한 장, 오른쪽에 머리만 세 각도(정면·3/4·옆얼굴)가 있는 형식입니다.

    {키}.png              정면 — 전신 그대로
    {키}_side_left.png    3/4 얼굴로 머리만 갈아 끼움   ┐ CHARACTERS 에 heads 를 적은
    {키}_back_left.png    옆얼굴로 머리만 갈아 끼움      │ 인물만 만듭니다 (지금은 슬픔)
    *_right               머리만 좌우 반전              ┘

뒷모습(back)은 시트에 없습니다. PortraitView 가 가장 가까운 방향으로 대신합니다.

전신 자르기는 cut_turnaround 의 배경 지우기를 그대로 씁니다. 시트 한 장에 설명 그림과
글자가 같이 있으므로, 몸 위의 한 점(body)에서 이어진 덩어리만 남깁니다.
팔과 몸 사이처럼 외곽선에 갇힌 종이는 테두리에서 번져 들어가지 못하니, holes 에 그 안의
한 점을 적어 주면 거기서부터 지웁니다 (흰 옷까지 지우지 않으려고 자동으로 찾지 않습니다).

머리 갈아 끼우기 (heads 가 있는 인물):
  1) 턱선(cut) 위의 옛 머리를 지웁니다.
  2) 턱선 아래 어깨에 걸쳐 있던 옛 머리카락 끝도 지우고, 드러난 옷 가장자리에 먹선을
     새로 긋습니다 — 원래 머리카락이 덮고 있어서 선이 없던 자리입니다.
  3) 새 머리를 head_scale 만큼 키우고, 그 머리의 기준점을 몸의 목(neck) 위에 맞춰 얹습니다.

좌표는 전부 시트 픽셀입니다. 시트를 바꾸면 다시 잡으세요. 캔버스 배치는 cut_turnaround 과
같은 규칙(키를 캔버스 높이의 95%, 다리 기준 가운데)이라 인물끼리 바닥선이 맞습니다.
그림을 다시 뽑으면 입 위치가 달라질 수 있으니 paint_mouths.py 와 gen_evidence.py 도 다시 돌리세요.
"""
import os, sys
import numpy as np
from PIL import Image, ImageOps

import cut_turnaround as ct

ROOT = os.path.dirname(os.path.abspath(__file__))
SHEETS = os.path.join(ROOT, "sheets")
INK = (34, 28, 30)

CHARACTERS = {
    "sorrow": dict(
        body=(250, 600),
        neck=(228, 372), cut=368, head_scale=1.2, hair=(30, 120, 220),
        heads={
            "side_left": ((650, 368, 1012, 668), (796, 640)),
            "back_left": ((694, 660, 1012, 1000), (835, 946)),
        },
    ),
    "wrath": dict(body=(260, 560)),
    "arrogance": dict(body=(250, 480)),
    "greed": dict(body=(330, 700)),
    "lust": dict(body=(230, 640), holes=[(196, 520), (330, 500)]),   # 허리에 올린 두 팔 안쪽
}

TIP_BAND = 60        # 턱선 아래 이만큼에서 옛 머리카락 끝을 찾습니다
HAIR_HUE = 0.85      # 머리카락 색과 색 방향이 이만큼 비슷하고 (코사인)
HAIR_SATURATION = 45 # 이만큼은 진해야 머리카락으로 봅니다 — 옷의 옅은 천 조각을 거르려고


# ── 자르기 ───────────────────────────────────────────────────────────────────

def analyse(sheet):
    rgb = np.asarray(sheet)
    lum = ct.luminance(rgb.astype(np.float32))
    bg = ct.find_background(rgb)
    return rgb, lum, bg


def component(fg, seed_xy, box=None):
    """seed 점에서 이어진 덩어리. box 를 주면 그 안에서만 찾습니다 (빠르고, 옆 그림에 안 번집니다)."""
    region = fg
    if box is not None:
        region = np.zeros_like(fg)
        region[box[1]:box[3], box[0]:box[2]] = fg[box[1]:box[3], box[0]:box[2]]
    seed = np.zeros_like(fg)
    x, y = seed_xy
    seed[y - 3:y + 4, x - 3:x + 4] = True
    if not (seed & region).any():
        raise SystemExit(f"기준점 {seed_xy} 이 그림 위에 있지 않습니다. 좌표를 다시 잡으세요.")
    return ct.spread(seed & region, region)


def to_rgba(rgb, lum, bg, fg):
    color, alpha = ct.soft_alpha(rgb, fg, float(np.median(lum[bg])))
    return Image.fromarray(np.dstack([color, alpha * 255]).clip(0, 255).astype(np.uint8), "RGBA")


def punch_holes(rgb, lum, bg, seeds):
    """외곽선에 갇힌 종이를 seeds 에서부터 지웁니다."""
    sat = rgb.max(axis=2).astype(np.int16) - rgb.min(axis=2).astype(np.int16)
    paper = (lum > ct.PAPER_LUM) & (sat < ct.PAPER_SAT) & ~bg   # 스티커 테두리의 옅은 그림자까지
    for x, y in seeds:
        seed = np.zeros_like(bg)
        seed[y - 2:y + 3, x - 2:x + 3] = True
        if not (seed & paper).any():
            print(f"    경고: holes {(x, y)} 자리가 종이 색이 아닙니다. 건너뜁니다.")
            continue
        bg = bg | ct.spread(seed & paper, paper)
    return bg


# ── 머리 갈아 끼우기 ─────────────────────────────────────────────────────────

def hair_like(rgb, hair):
    """머리카락 색인지. 밝은 곳·그늘진 곳이 같은 색으로 잡히도록 밝기 말고 색 방향을 봅니다."""
    rgb = rgb.astype(np.float32)
    chroma = rgb - rgb.mean(axis=2, keepdims=True)
    ref = np.array(hair, np.float32)
    ref = ref - ref.mean()
    cos = (chroma * ref).sum(axis=2) / (np.linalg.norm(chroma, axis=2) * np.linalg.norm(ref) + 1e-6)
    saturation = rgb.max(axis=2) - rgb.min(axis=2)
    return (cos > HAIR_HUE) & (saturation > HAIR_SATURATION)


def headless(body, cut, hair):
    """머리를 떼어 낸 몸. 어깨에 남은 머리카락 끝을 지우고 옷 가장자리에 선을 긋습니다."""
    a = np.asarray(body).astype(np.int16).copy()
    a[:cut, :, 3] = 0

    rgb, al = a[..., :3], a[..., 3]
    lum = rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114
    near_hair = (al > 0) & hair_like(rgb, hair)
    dark = (al > 0) & (lum < 110)
    band = np.zeros(dark.shape, bool)
    band[cut:cut + TIP_BAND, :] = True
    seed = np.zeros_like(band)
    seed[cut, :] = True

    # 턱선에서 이어진 머리카락만 고릅니다. 머리카락 안의 바느질 선을 건너가려고 어두운 선도
    # 길로는 쓰지만, 지우는 건 머리카락 색과 그 바로 옆 외곽선뿐입니다 — 어두운 선을 다
    # 지우면 옷의 단추 줄·바느질 자국까지 같이 사라져 구멍이 납니다.
    reach = ct.spread((near_hair | dark) & seed & band, (near_hair | dark) & band)
    hair_tips = reach & near_hair
    rim = hair_tips.copy()
    for _ in range(6):
        rim = ct.grow(rim)
    tips = hair_tips | (dark & band & rim)
    a[tips, 3] = 0

    # 가시처럼 남은 한두 픽셀짜리 조각을 걷어 냅니다 (띠 안에서만 열림 연산).
    opaque = a[..., 3] > 128
    shrunk = opaque.copy()
    for _ in range(2):
        shrunk &= ~ct.grow(~shrunk)
    opened = shrunk.copy()
    for _ in range(2):
        opened = ct.grow(opened) & opaque
    spikes = opaque & ~opened & band
    a[spikes, 3] = 0

    # 새로 드러난 가장자리에 먹선
    opaque = a[..., 3] > 128
    exposed = ct.grow(tips | spikes)
    for _ in range(3):
        exposed = ct.grow(exposed)
    edge = opaque & ct.grow(~opaque) & exposed
    for _ in range(2):
        edge = ct.grow(edge) & opaque & exposed
    a[edge, 0:3] = INK
    return Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")


def with_head(base, head, anchor, neck, scale, mirror=False):
    ax, ay = anchor
    if mirror:
        head = ImageOps.mirror(head)
        ax = head.width - ax
    head = head.resize((round(head.width * scale), round(head.height * scale)), Image.LANCZOS)
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    layer.paste(head, (round(neck[0] - ax * scale), round(neck[1] - ay * scale)))
    out = base.copy()
    out.alpha_composite(layer)
    return out


# ── 캔버스 ───────────────────────────────────────────────────────────────────

def placer(fg):
    """cut_turnaround 과 같은 배치. 같은 인물의 모든 방향이 이 한 가지 변환을 씁니다."""
    top, bottom = ct.figure_rows(fg)
    scale = (ct.CH * ct.FILL) / (bottom - top)
    cut = fg[top:bottom]
    low = cut[int(cut.shape[0] * 0.6):, :]
    cx = np.nonzero(low)[1].mean()

    # 인물이 캔버스 폭(700)에 다 들어오는지 — 넓은 드레스나 옆으로 뻗은 머리는 잘릴 수 있습니다.
    cols = np.nonzero(fg.any(axis=0))[0]
    left = ct.CW / 2 + (cols.min() - cx) * scale
    right = ct.CW / 2 + (cols.max() - cx) * scale
    if left < 0 or right > ct.CW:
        print(f"    경고: 인물이 캔버스 폭을 넘습니다 ({left:.0f}..{right:.0f}). 가장자리가 잘립니다.")

    def put(img):
        img = img.crop((0, top, img.width, bottom))
        w, h = round(img.width * scale), round(img.height * scale)
        img = img.resize((w, h), Image.LANCZOS)
        canvas = Image.new("RGBA", (ct.CW, ct.CH), (0, 0, 0, 0))
        canvas.paste(img, (round(ct.CW / 2 - cx * scale), ct.CH - ct.BOTTOM_MARGIN - h))
        return canvas
    return put


def build(key, cfg):
    sheet = Image.open(os.path.join(SHEETS, f"{key}_sheet.webp")).convert("RGB")
    rgb, lum, bg = analyse(sheet)
    if cfg.get("holes"):
        bg = punch_holes(rgb, lum, bg, cfg["holes"])
    fg_all = ~bg

    fg = component(fg_all, cfg["body"], box=(0, 0, 640, sheet.height))
    body = to_rgba(rgb, lum, bg, fg)
    put = placer(fg)
    made = {"front": put(body)}

    if cfg.get("heads"):
        base = headless(body, cfg["cut"], cfg["hair"])
        for pose, (box, anchor) in cfg["heads"].items():
            seed = ((box[0] + box[2]) // 2, (box[1] + box[3]) // 2)
            head_fg = component(fg_all, seed, box=box)
            head = to_rgba(rgb, lum, bg, head_fg).crop(box)
            local = (anchor[0] - box[0], anchor[1] - box[1])
            made[pose] = put(with_head(base, head, local, cfg["neck"], cfg["head_scale"]))
            if pose.endswith("_left"):
                right = pose[:-len("_left")] + "_right"
                made[right] = put(with_head(base, head, local, cfg["neck"], cfg["head_scale"], mirror=True))

    os.makedirs(ct.OUT, exist_ok=True)
    for pose, img in made.items():
        name = key if pose == "front" else f"{key}_{pose}"
        img.save(os.path.join(ct.OUT, name + ".png"))
        print(f"  Portraits/{name}.png")


if __name__ == "__main__":
    wanted = sys.argv[1:] or list(CHARACTERS)
    for key in wanted:
        if key not in CHARACTERS:
            raise SystemExit(f"'{key}' 은 CHARACTERS 에 없습니다: {', '.join(CHARACTERS)}")
        print(f"{key}:")
        build(key, CHARACTERS[key])
