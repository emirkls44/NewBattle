"""
Malatya temali ana menu gorselleri (sifirdan cizilir, lisans derdi yok).

Calistir:   python Tools/MenuArt/generate_malatya_menu.py
Cikti:      Assets/GameArt/MenuUI/bg_kayisi_bahcesi.png  (1920x1080 arka plan)
            Assets/GameArt/MenuUI/kilim_serit.png        (yatay dosenen kilim deseni)
"""

import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "GameArt", "MenuUI")
W, H = 1920, 1080

CREAM = (255, 241, 220)
SAND = (233, 211, 168)
HILL_LIGHT = (156, 192, 106)
HILL_DARK = (110, 154, 69)
LEAF = (79, 122, 52)
TRUNK = (122, 74, 38)
APRICOT = (242, 153, 74)
RUST = (181, 69, 43)
GOLD = (242, 184, 75)


def ridge(seed, base, amp, freq):
    rng = random.Random(seed)
    xs = np.arange(W + 1)
    y = np.full(len(xs), base, float)
    for i in range(3):
        y += amp * (0.45 ** i) * np.sin(xs / W * freq * (2 ** i) * 2 * math.pi + rng.uniform(0, 6.28))
    return y


def fill_below(draw, ys, color):
    pts = [(x, float(ys[x])) for x in range(0, W + 1, 8)] + [(W, H), (0, H)]
    draw.polygon(pts, fill=color)


def tree(draw, x, ground, size, rng, alpha=255):
    trunk_w = size * 0.16
    draw.rectangle([x - trunk_w / 2, ground - size * 0.9, x + trunk_w / 2, ground], fill=TRUNK + (alpha,))
    for dx, dy, r in ((0, -1.25, 0.62), (-0.42, -1.0, 0.45), (0.45, -1.02, 0.47), (0, -0.85, 0.5)):
        cx, cy, rr = x + dx * size, ground + dy * size, r * size
        draw.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=LEAF + (alpha,))
    for _ in range(9):
        ang = rng.uniform(0, 2 * math.pi)
        dist = rng.uniform(0.1, 0.55) * size
        cx = x + math.cos(ang) * dist
        cy = ground - size * 1.05 + math.sin(ang) * dist * 0.8
        rr = size * 0.075
        draw.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=APRICOT + (alpha,))


def background():
    img = Image.new("RGBA", (W, H), CREAM + (255,))
    # gok: ustten hafif sicak krem tonlari (yumusak bant)
    sky = np.zeros((H, W, 3))
    t = np.linspace(0, 1, H)[:, None]
    top, bottom = np.array([255, 236, 205]), np.array(CREAM)
    sky[:] = (top + (bottom - top) * t[..., None])[:, :, :] if False else (top + (bottom - top) * t)[:, None, :]
    img = Image.fromarray(sky.astype(np.uint8)).convert("RGBA")

    # gunes
    # Renk her yerde ayni, sadece saydamlik bulanik: koyu kenar halkasi olusmaz.
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).ellipse([W * 0.5 - 240, 140 - 240, W * 0.5 + 240, 140 + 240], fill=110)
    glow = Image.new("RGBA", (W, H), (255, 214, 150, 0))
    glow.putalpha(mask.filter(ImageFilter.GaussianBlur(70)))
    img.alpha_composite(glow)

    d = ImageDraw.Draw(img)
    fill_below(d, ridge(3, H * 0.66, 34, 0.9), SAND)
    fill_below(d, ridge(5, H * 0.72, 44, 1.1), HILL_LIGHT)

    # uzak agaclar (soluk)
    far = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    fd = ImageDraw.Draw(far)
    rng = random.Random(11)
    for x in list(range(80, 640, 120)) + list(range(1300, 1880, 120)):
        tree(fd, x + rng.uniform(-25, 25), H * 0.72 + rng.uniform(-10, 10), rng.uniform(70, 95), rng)
    far.putalpha(Image.eval(far.split()[3], lambda a: int(a * 0.6)))
    img.alpha_composite(far)

    d = ImageDraw.Draw(img)
    fill_below(d, ridge(9, H * 0.82, 30, 1.3), HILL_DARK)

    # on agaclar: kenarlarda, ortayi (karakter) bos birak
    near = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    nd = ImageDraw.Draw(near)
    for x in (110, 330, 1600, 1830):
        tree(nd, x, H * 0.86, rng.uniform(150, 185), rng)
    img.alpha_composite(near)

    # hafif kenar golgesi: menuler okunur kalsin
    yy, xx = np.mgrid[0:H, 0:W]
    dist = np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2) / 1.42
    k = 1 - 0.08 * np.clip(dist, 0, 1) ** 2
    arr = np.array(img.convert("RGB"), float) * k[..., None]
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))


def kilim():
    """Yatay dosenebilen kilim seridi (128x32): elmas + zikzak."""
    w, h = 128, 32
    img = Image.new("RGB", (w, h), GOLD)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w, 4], fill=RUST)
    d.rectangle([0, h - 5, w, h], fill=RUST)
    for i in range(2):
        cx = 32 + i * 64
        d.polygon([(cx, 6), (cx + 14, 16), (cx, 26), (cx - 14, 16)], fill=RUST)
        d.polygon([(cx, 11), (cx + 7, 16), (cx, 21), (cx - 7, 16)], fill=LEAF)
        for side in (-1, 1):
            bx = cx + side * 26
            d.polygon([(bx, 10), (bx + 5, 16), (bx, 22), (bx - 5, 16)], fill=LEAF)
    return img


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    background().save(os.path.join(OUT, "bg_kayisi_bahcesi.png"), optimize=True)
    kilim().save(os.path.join(OUT, "kilim_serit.png"))
    print("ok")
