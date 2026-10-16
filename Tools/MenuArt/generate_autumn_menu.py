"""
Sonbahar temali ana menu arka plani (sifirdan cizilir, lisans derdi yok).
Kenarlarda dalli agaclar ve turuncu / kahve yapraklar, ortada karakter icin sakin alan,
altta yumusak tepeler ve dusen yapraklar.

Calistir:   python Tools/MenuArt/generate_autumn_menu.py
Cikti:      Assets/GameArt/MenuUI/bg_sonbahar.png  (1920x1080)
"""

import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "GameArt", "MenuUI", "bg_sonbahar.png")
W, H = 1920, 1080
SS = 2  # yumusak kenar icin buyuk ciz

BARK = (122, 78, 46)
BARK_FAR = (196, 150, 108)
LEAVES = [(217, 102, 43), (224, 138, 60), (192, 80, 42), (233, 176, 74), (160, 82, 45), (205, 120, 52)]
OUTLINE = (110, 58, 28)

rng = random.Random(21)


def base():
    yy, xx = np.mgrid[0:H, 0:W]
    d = np.sqrt(((xx - W * 0.5) / (W * 0.62)) ** 2 + ((yy - H * 0.45) / (H * 0.75)) ** 2)
    inner, outer = np.array([246, 222, 182], float), np.array([226, 182, 136], float)
    t = np.clip(d, 0, 1)[..., None] ** 1.4
    return Image.fromarray((inner + (outer - inner) * t).astype(np.uint8)).convert("RGBA")


def leaf(draw, x, y, size, angle, color, alpha=255, outline=True):
    """Ucu sivri badem yaprak + orta damar."""
    pts = []
    for i in range(24):
        t = i / 23 * math.pi
        px = math.cos(t) * size
        py = math.sin(t) * size * 0.42
        pts.append((px, py))
    for i in range(24):
        t = math.pi + i / 23 * math.pi
        px = math.cos(t) * size
        py = math.sin(t) * size * 0.42
        pts.append((px, py))
    ca, sa = math.cos(angle), math.sin(angle)
    poly = [(x + px * ca - py * sa, y + px * sa + py * ca) for px, py in pts]
    fill = color + (alpha,)
    draw.polygon(poly, fill=fill, outline=(OUTLINE + (alpha,)) if outline else None)
    draw.line([(x - size * ca, y - size * sa), (x + size * ca, y + size * sa)],
              fill=(OUTLINE + (int(alpha * 0.7),)), width=max(1, int(size * 0.08)))


def maple(draw, x, y, size, angle, color, alpha=255):
    """Bes uclu akcaagac yapragi."""
    pts = []
    lobes = 5
    for i in range(lobes * 2):
        a = angle + i * math.pi / lobes
        r = size if i % 2 == 0 else size * 0.45
        pts.append((x + math.cos(a) * r, y + math.sin(a) * r))
    draw.polygon(pts, fill=color + (alpha,), outline=OUTLINE + (alpha,))


def branch(draw, x, y, angle, length, width, depth, color, leaf_alpha, tips):
    x2 = x + math.cos(angle) * length
    y2 = y + math.sin(angle) * length
    draw.line([(x, y), (x2, y2)], fill=color, width=max(2, int(width)))
    draw.ellipse([x2 - width / 2, y2 - width / 2, x2 + width / 2, y2 + width / 2], fill=color)
    if depth <= 0:
        tips.append((x2, y2, leaf_alpha))
        return
    for spread in (-0.45, 0.4) if depth > 1 else (-0.5, 0.0, 0.45):
        branch(draw, x2, y2, angle + spread + rng.uniform(-0.15, 0.15), length * rng.uniform(0.62, 0.78),
               width * 0.62, depth - 1, color, leaf_alpha, tips)


def tree(img, x, ground, height, lean, far=False):
    layer = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    color = (BARK_FAR if far else BARK) + (255,)
    tips = []
    s = SS
    trunk_w = height * (0.042 if not far else 0.035)
    top = ground - height * 0.45
    d.polygon([(x * s - trunk_w * s, ground * s), (x * s + trunk_w * s, ground * s),
               (x * s + trunk_w * 0.5 * s, top * s), (x * s - trunk_w * 0.5 * s, top * s)], fill=color)
    for a in (-math.pi / 2 + lean - 0.55, -math.pi / 2 + lean, -math.pi / 2 + lean + 0.6):
        branch(d, x * s, top * s, a, height * 0.3 * s, trunk_w * 0.8 * s, 3, color, 120 if far else 255, tips)
    for tx, ty, alpha in tips:
        for _ in range(5 if not far else 3):
            ox, oy = rng.uniform(-40, 40) * s, rng.uniform(-30, 30) * s
            size = rng.uniform(16, 30) * s * (0.8 if far else 1.0)
            col = rng.choice(LEAVES)
            if rng.random() < 0.35:
                maple(d, tx + ox, ty + oy, size, rng.uniform(0, 6.28), col, alpha)
            else:
                leaf(d, tx + ox, ty + oy, size, rng.uniform(0, 6.28), col, alpha, outline=not far)
    layer = layer.resize((W, H), Image.LANCZOS)
    if far:
        layer.putalpha(Image.eval(layer.split()[3], lambda v: int(v * 0.45)))
    img.alpha_composite(layer)


def hills(img):
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    xs = np.arange(0, W + 1, 8)
    for base_y, amp, col in ((H * 0.74, 28, (214, 166, 120, 255)), (H * 0.82, 22, (204, 150, 104, 255))):
        ph = rng.uniform(0, 6.28)
        ys = base_y + amp * np.sin(xs / W * 2.2 * math.pi + ph) + amp * 0.4 * np.sin(xs / W * 5.3 * math.pi)
        d.polygon([(int(x), float(y)) for x, y in zip(xs, ys)] + [(W, H), (0, H)], fill=col)
    img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(1.5)))


def falling_leaves(img, count=26):
    layer = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(count):
        x = rng.uniform(0.2, 0.8) * W if rng.random() < 0.45 else rng.uniform(0.05, 0.95) * W
        y = rng.uniform(0.15, 0.9) * H
        if 0.36 * W < x < 0.64 * W and 0.25 * H < y < 0.8 * H:
            continue  # karakterin onunu bos birak
        leaf(d, x * SS, y * SS, rng.uniform(14, 24) * SS, rng.uniform(0, 6.28), rng.choice(LEAVES), 235)
    img.alpha_composite(layer.resize((W, H), Image.LANCZOS))


if __name__ == "__main__":
    img = base()
    for x in (520, 1400, 760, 1180):
        tree(img, x, H * 0.78, rng.uniform(520, 640), rng.uniform(-0.25, 0.25), far=True)
    hills(img)
    # Sol agaclar sola, sag agaclar saga yatik: dallar ortaya (karaktere) girmesin.
    tree(img, 70, H * 0.98, 960, -0.35)
    tree(img, 1850, H * 0.98, 980, 0.35)
    tree(img, 300, H * 1.02, 700, -0.5)
    tree(img, 1630, H * 1.02, 700, 0.5)
    falling_leaves(img)
    img.convert("RGB").save(OUT, optimize=True)
    print("ok")
