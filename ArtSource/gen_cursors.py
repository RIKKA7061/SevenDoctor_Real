# -*- coding: utf-8 -*-
"""
마우스 커서 그림. 올려놓은 대상이 무엇을 하는 곳인지에 따라 모양이 바뀝니다 (UI/GameCursor.cs).

    python ArtSource/gen_cursors.py
    python ArtSource/gen_cursors.py --preview 미리보기.png   # 어두운 방·밝은 종이 위에 놓아 보기

출력: Assets/_Project/Resources/Art/Cursors/cursor_*.png  (64x64, 배경 투명)

    cursor_arrow   기본 — 평소, UI 버튼, 퍼즐, 지도
    cursor_look    조사 — look 핫스팟, 증거 크게 보기
    cursor_get     줍기 — get 핫스팟
    cursor_talk    대화 — talk 핫스팟
    cursor_move    이동 — move 핫스팟

크림색 채움에 굵은 먹선입니다. 어두운 방에서는 채움이, 밝은 노트·증거 카드 위에서는 먹선이
보여야 해서 둘 다 있어야 합니다.

다른 gen_*.py 와 달리 Pillow 를 쓰지 않습니다. 64px 다섯 장이라 표준 라이브러리로 충분하고,
그러면 파이썬만 깔린 PC 에서도 돕니다. 도형은 '부호 거리'(안쪽은 음수, 바깥은 양수)로 적습니다 —
여러 도형을 합쳐도 그 둘레에 같은 굵기의 먹선이 저절로 둘립니다.

좌표는 32 격자이고 결과는 그 두 배입니다. 클릭 지점(아래 CURSORS 의 마지막 칸, 결과 px 기준)을
바꾸면 GameCursor.cs 의 표도 같이 고치세요. 미리보기의 빨간 십자가 그 자리입니다.

아트 담당 그림이 오면 같은 이름의 투명 PNG 로 덮어쓰면 됩니다 (64x64 권장).
"""
import math, os, struct, sys, zlib

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "..", "Assets", "_Project", "Resources", "Art", "Cursors")

GRID = 32        # 도형 좌표의 격자
SIZE = 64        # 결과 크기 (px)
SS = 4           # 슈퍼샘플링 — 한 픽셀을 SS x SS 로 쪼개 평균
STROKE = 2.2     # 먹선 굵기 (격자 단위)

INK = (38, 32, 34)          # gen_evidence.INK 와 같은 먹색
CREAM = (243, 234, 211)     # 핫스팟 점(prop_marker)과 같은 계열
GOLD = (217, 184, 107)      # UIFactory.Accent
GLASS = (190, 220, 245)


# ── 도형 (부호 거리) ─────────────────────────────────────────────────────────

def circle(cx, cy, r):
    return lambda x, y: math.hypot(x - cx, y - cy) - r


def ring(cx, cy, r, half):
    """가운데가 빈 고리. r 은 고리 한가운데까지의 반지름, half 는 고리 굵기의 절반."""
    return lambda x, y: abs(math.hypot(x - cx, y - cy) - r) - half


def capsule(ax, ay, bx, by, r):
    """양 끝이 둥근 막대 — 손가락, 손잡이, 짧은 선."""
    ex, ey = bx - ax, by - ay
    ll = ex * ex + ey * ey

    def f(x, y):
        wx, wy = x - ax, y - ay
        t = (wx * ex + wy * ey) / ll
        t = 0.0 if t < 0.0 else 1.0 if t > 1.0 else t
        return math.hypot(wx - ex * t, wy - ey * t) - r
    return f


def rrect(x0, y0, x1, y1, r):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    hx, hy = (x1 - x0) / 2 - r, (y1 - y0) / 2 - r

    def f(x, y):
        qx, qy = abs(x - cx) - hx, abs(y - cy) - hy
        return math.hypot(max(qx, 0.0), max(qy, 0.0)) + min(max(qx, qy), 0.0) - r
    return f


def polygon(points):
    edges = []
    for i, (ax, ay) in enumerate(points):
        bx, by = points[(i + 1) % len(points)]
        ex, ey = bx - ax, by - ay
        edges.append((ax, ay, by, ex, ey, ex * ex + ey * ey))

    def f(x, y):
        best, inside = 1e18, False
        for ax, ay, by, ex, ey, ll in edges:
            wx, wy = x - ax, y - ay
            t = (wx * ex + wy * ey) / ll
            t = 0.0 if t < 0.0 else 1.0 if t > 1.0 else t
            dx, dy = wx - ex * t, wy - ey * t
            dd = dx * dx + dy * dy
            if dd < best:
                best = dd
            if (ay > y) != (by > y) and x < ax + (y - ay) * ex / ey:
                inside = not inside
        d = math.sqrt(best)
        return -d if inside else d
    return f


def union(*shapes):
    return lambda x, y: min(s(x, y) for s in shapes)


# ── 칠하기 ───────────────────────────────────────────────────────────────────
# 한 겹 = (도형, ((넓히기, 색, 불투명도), ...)). 도형에서 '넓히기' 만큼 부풀린 안쪽을 그 색으로 칠합니다.

def flat(shape, color, alpha=1.0):
    return (shape, ((0.0, color, alpha),))


def outlined(shape, fill, out=STROKE, inn=0.0):
    """
    먹선을 두르고 안을 채웁니다. 먹선은 도형 바깥으로 out, 안쪽으로 inn 만큼 먹습니다.
    여러 도형을 합친 것(union)은 inn 을 0 으로 두세요 — 합친 도형의 안쪽 거리는 정확하지 않아서
    이음매에 먹선이 새어 나옵니다.
    """
    return (shape, ((out, INK, 1.0), (-inn, fill, 1.0)))


def render(layers):
    """겹들을 아래부터 차례로 덧칠해 SIZE x SIZE RGBA(알파를 곱하지 않은 값)로 돌려줍니다."""
    n = SIZE * SS
    unit = GRID / n               # 서브픽셀 한 칸의 격자 길이
    edge = 1.0 / unit             # 경계는 서브픽셀 한 칸 폭으로 부드럽게
    acc = [0.0] * (SIZE * SIZE * 4)

    for sy in range(n):
        y = (sy + 0.5) * unit
        row = (sy // SS) * SIZE
        for sx in range(n):
            x = (sx + 0.5) * unit
            r = g = b = a = 0.0   # 알파를 곱한 값으로 쌓습니다
            for shape, paints in layers:
                d = shape(x, y)
                for grow, color, alpha in paints:
                    cover = (grow - d) * edge + 0.5
                    if cover <= 0.0:
                        continue
                    cover = (1.0 if cover > 1.0 else cover) * alpha
                    keep = 1.0 - cover
                    r = color[0] * cover + r * keep
                    g = color[1] * cover + g * keep
                    b = color[2] * cover + b * keep
                    a = cover + a * keep
            if a > 0.0:
                i = (row + sx // SS) * 4
                acc[i] += r
                acc[i + 1] += g
                acc[i + 2] += b
                acc[i + 3] += a

    out = bytearray(SIZE * SIZE * 4)
    for p in range(SIZE * SIZE):
        i = p * 4
        a = acc[i + 3]
        if a <= 0.0:
            # 완전히 투명한 곳에도 먹색을 넣어 둡니다. 커서가 확대·축소될 때 이웃 픽셀과 섞이는데,
            # 여기가 검정이나 흰색이면 가장자리에 테가 낍니다.
            out[i:i + 3] = bytes(INK)
            continue
        for k in range(3):
            out[i + k] = min(255, max(0, round(acc[i + k] / a)))
        out[i + 3] = min(255, round(a / (SS * SS) * 255))
    return out


# ── 커서 ─────────────────────────────────────────────────────────────────────

def arrow():
    body = polygon([(5, 2.5), (5, 25.5), (11, 20.2), (15.2, 29.5), (19.2, 27.6), (15, 18.6), (23.2, 18.6)])
    return [outlined(body, CREAM, out=STROKE / 2, inn=STROKE / 2)]


def look():
    """돋보기. 렌즈 안은 거의 비워 둡니다 — 들여다보는 물건이 가려지면 안 됩니다."""
    cx = cy = 13.0
    return [
        flat(circle(cx, cy, 6.3), GLASS, 0.20),
        outlined(capsule(20.8, 20.8, 26.6, 26.6, 1.4), GOLD, out=2.0),      # 손잡이 — 렌즈 테 밑으로 들어갑니다
        outlined(ring(cx, cy, 8.6, 1.2), CREAM, out=1.5),
        flat(capsule(9.0, 11.2, 10.6, 9.4, 0.5), CREAM, 0.85),               # 유리에 비친 빛
    ]


def get():
    """펼친 손. 가운뎃손가락 끝이 클릭 지점이라 주울 물건이 손 위쪽에 그대로 보입니다."""
    fingers = [capsule(x, top, x, 16.0, 1.7) for x, top in ((11.0, 8.8), (15.6, 6.0), (20.2, 6.8), (24.8, 9.6))]
    thumb = capsule(6.0, 18.3, 8.6, 21.8, 2.4)
    palm = rrect(9.3, 14.0, 26.5, 27.5, 5.0)
    # 손가락 사이 틈은 먹선이 메웁니다. 그 선을 손바닥 안으로 조금 더 이어 줍니다.
    # 틈이 이보다 좁으면 화면 배율 100% 에서 손가락이 한 덩어리로 뭉쳐 보입니다.
    seams = union(*[capsule(x, 12.6, x, 17.4, 0.6) for x in (13.3, 17.9, 22.5)])
    return [outlined(union(palm, thumb, *fingers), CREAM), flat(seams, INK)]


def talk():
    """말풍선. 꼬리 끝이 클릭 지점 — 말을 걸 사람을 꼬리로 가리킵니다."""
    bubble = union(rrect(5.0, 5.5, 27.0, 21.5, 4.5), polygon([(15.5, 19.5), (9.5, 27.5), (9.5, 19.5)]))
    dots = union(*[circle(x, 13.5, 1.5) for x in (11.0, 16.0, 21.0)])
    return [outlined(bubble, CREAM), flat(dots, INK)]


def move():
    body = polygon([(3.5, 12), (16, 12), (16, 6), (28.5, 16), (16, 26), (16, 20), (3.5, 20)])
    return [outlined(body, CREAM, out=STROKE / 2, inn=STROKE / 2)]


# (이름, 그리는 함수, 클릭 지점 — 결과 그림의 왼쪽 위에서 잰 px)
#
# 뾰족한 끝은 먹선 끝과 크림색 끝의 중간에 둡니다. 어두운 방에서는 먹선이 배경에 묻혀
# 크림색 끝이 '끝'으로 보이고, 밝은 종이 위에서는 먹선 끝이 그렇게 보이기 때문입니다.
CURSORS = (
    ("arrow", arrow, (11, 7)),     # 화살 끝
    ("look",  look,  (26, 26)),    # 렌즈 한가운데
    ("get",   get,   (31, 6)),     # 가운뎃손가락 끝
    ("talk",  talk,  (18, 57)),    # 꼬리 끝
    ("move",  move,  (57, 32)),    # 화살 끝
)


# ── PNG ──────────────────────────────────────────────────────────────────────

def write_png(path, width, height, pixels, channels=4):
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    stride = width * channels
    raw = b"".join(b"\x00" + bytes(pixels[y * stride:(y + 1) * stride]) for y in range(height))
    header = struct.pack(">IIBBBBB", width, height, 8, 6 if channels == 4 else 2, 0, 0, 0)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


# ── 미리보기 ─────────────────────────────────────────────────────────────────
# 게임에서는 배경 위에 놓이므로, 투명 PNG 만 봐서는 잘 보이는지 알 수 없습니다.
# 어두운 방과 밝은 종이 위에 4배(클릭 지점 표시) · 제 크기 · 절반 크기로 놓아 봅니다.
# 절반 크기가 Windows 배율 100% 화면에서 실제로 보이는 크기입니다.

def preview(path, images):
    backgrounds = ((35, 30, 31), (233, 227, 213))
    zoom, pad = 4, 16
    cell = SIZE * zoom + pad
    band = SIZE * zoom + SIZE + pad * 3
    width, height = cell * len(images) + pad, band * len(backgrounds)
    canvas = bytearray(width * height * 3)

    def put(x, y, color, alpha):
        if 0 <= x < width and 0 <= y < height and alpha > 0.0:
            i = (y * width + x) * 3
            for k in range(3):
                canvas[i + k] = round(color[k] * alpha + canvas[i + k] * (1.0 - alpha))

    def pixel(img, x, y):
        i = (y * SIZE + x) * 4
        return img[i:i + 3], img[i + 3] / 255.0

    for b, bg in enumerate(backgrounds):
        top = b * band
        for y in range(top, top + band):
            for x in range(width):
                canvas[(y * width + x) * 3:(y * width + x) * 3 + 3] = bytes(bg)

        for c, (name, _, hot) in enumerate(CURSORS):
            img = images[name]
            left = pad + c * cell

            for y in range(SIZE):                                  # 4배 — 픽셀이 보이게 그대로 키웁니다
                for x in range(SIZE):
                    color, alpha = pixel(img, x, y)
                    for dy in range(zoom):
                        for dx in range(zoom):
                            put(left + x * zoom + dx, top + pad + y * zoom + dy, color, alpha)

            hx, hy = left + hot[0] * zoom + zoom // 2, top + pad + hot[1] * zoom + zoom // 2
            for k in range(-14, 15):                               # 클릭 지점
                put(hx + k, hy, (230, 40, 40), 0.9)
                put(hx, hy + k, (230, 40, 40), 0.9)

            base = top + pad * 2 + SIZE * zoom
            for y in range(SIZE):                                  # 제 크기
                for x in range(SIZE):
                    color, alpha = pixel(img, x, y)
                    put(left + x, base + y, color, alpha)

            for y in range(SIZE // 2):                             # 절반 — 네 픽셀을 알파를 곱해 평균
                for x in range(SIZE // 2):
                    rgb, total = [0.0, 0.0, 0.0], 0.0
                    for dy in range(2):
                        for dx in range(2):
                            color, alpha = pixel(img, x * 2 + dx, y * 2 + dy)
                            total += alpha
                            for k in range(3):
                                rgb[k] += color[k] * alpha
                    if total > 0.0:
                        put(left + SIZE + pad + x, base + SIZE // 4 + y, [v / total for v in rgb], total / 4)

    write_png(path, width, height, canvas, channels=3)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    images = {}
    for name, draw, hot in CURSORS:
        images[name] = render(draw())
        write_png(os.path.join(OUT, f"cursor_{name}.png"), SIZE, SIZE, images[name])
        print(f"  Cursors/cursor_{name}.png  {SIZE}x{SIZE}  클릭 지점 {hot}")

    if "--preview" in sys.argv:
        target = sys.argv[sys.argv.index("--preview") + 1]
        preview(target, images)
        print(f"  미리보기: {target}")
