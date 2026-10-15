"""
Magazada satilan ana menu arka planlarini sifirdan cizer (lisans derdi yok).

Calistir:   python Tools/MenuArt/generate_menu_backgrounds.py
Cikti:      Assets/GameArt/MenuBackgrounds/*.png  (1600x900)

Kendi fotografini eklemek icin bu klasore bir resim koy ve
Assets/Resources/ArkaPlanKutuphanesi dosyasinda listeye ekle.
"""

import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "GameArt", "MenuBackgrounds")
W, H = 1600, 900


def gradient(top, bottom, height=H, power=1.0):
    t = (np.linspace(0, 1, height) ** power)[:, None, None]
    top, bottom = np.array(top, float), np.array(bottom, float)
    col = top + (bottom - top) * t
    return np.broadcast_to(col, (height, W, 3)).copy()


def to_image(arr):
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB")


def ridge(seed, base, amp, freq, rough=0.35):
    """Yumusak tepe / dag silueti: x -> y."""
    rng = random.Random(seed)
    phases = [rng.uniform(0, 6.28) for _ in range(4)]
    xs = np.arange(W)
    y = np.full(W, base, float)
    for i, ph in enumerate(phases):
        f = freq * (2 ** i)
        y += amp * (rough ** i) * np.sin(xs / W * f * 2 * math.pi + ph)
    return y


def fill_below(img, ys, color, blur=0):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    pts = [(x, float(ys[x])) for x in range(0, W, 4)] + [(W, float(ys[-1])), (W, H), (0, H)]
    d.polygon(pts, fill=tuple(color) + (255,))
    if blur:
        layer = layer.filter(ImageFilter.GaussianBlur(blur))
    img.alpha_composite(layer)


def glow(img, cx, cy, radius, color, strength=1.0):
    yy, xx = np.mgrid[0:H, 0:W]
    dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / radius
    a = np.clip(1 - dist, 0, 1) ** 2 * strength
    base = np.array(img.convert("RGB"), float)
    col = np.array(color, float)
    base = base + (col - base) * a[..., None]
    return Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).convert("RGBA")


def vignette(img, amount=0.35):
    yy, xx = np.mgrid[0:H, 0:W]
    d = np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2) / 1.42
    k = 1 - amount * np.clip(d, 0, 1) ** 2
    arr = np.array(img.convert("RGB"), float) * k[..., None]
    return to_image(arr)


def sunset():
    img = to_image(gradient((255, 120, 90), (255, 214, 120), power=0.8)).convert("RGBA")
    img = glow(img, W * 0.5, H * 0.62, 420, (255, 245, 200), 0.9)
    d = ImageDraw.Draw(img)
    d.ellipse([W * 0.5 - 110, H * 0.62 - 110, W * 0.5 + 110, H * 0.62 + 110], fill=(255, 236, 170, 255))
    fill_below(img, ridge(1, H * 0.66, 40, 1.2), (214, 92, 96))
    fill_below(img, ridge(2, H * 0.74, 34, 1.7), (160, 62, 92))
    fill_below(img, ridge(3, H * 0.84, 26, 2.3), (102, 40, 80))
    return vignette(img, 0.3)


def night():
    img = to_image(gradient((14, 18, 52), (58, 46, 120), power=1.2)).convert("RGBA")
    rng = random.Random(7)
    d = ImageDraw.Draw(img)
    for _ in range(260):
        x, y = rng.uniform(0, W), rng.uniform(0, H * 0.65)
        r = rng.choice([1, 1, 1.5, 2, 2.5])
        a = rng.randint(140, 255)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 240, a))
    img = glow(img, W * 0.78, H * 0.22, 260, (150, 160, 255), 0.5)
    d = ImageDraw.Draw(img)
    sky = img.getpixel((int(W * 0.78 + 110), int(H * 0.22 - 80)))
    d.ellipse([W * 0.78 - 70, H * 0.22 - 70, W * 0.78 + 70, H * 0.22 + 70], fill=(250, 248, 225, 255))
    d.ellipse([W * 0.78 - 30, H * 0.22 - 92, W * 0.78 + 100, H * 0.22 + 38], fill=sky)  # hilal
    fill_below(img, ridge(4, H * 0.72, 46, 1.1), (36, 30, 84))
    fill_below(img, ridge(5, H * 0.82, 30, 1.9), (22, 20, 56))
    return vignette(img, 0.35)


def forest():
    img = to_image(gradient((140, 220, 255), (220, 248, 220), power=0.9)).convert("RGBA")
    img = glow(img, W * 0.25, H * 0.2, 380, (255, 255, 230), 0.6)
    fill_below(img, ridge(6, H * 0.55, 50, 1.0), (150, 205, 150))
    rng = random.Random(11)
    for layer, (base, color, size) in enumerate([(0.66, (90, 170, 90), 48), (0.78, (52, 130, 62), 64), (0.9, (30, 96, 46), 84)]):
        ys = ridge(20 + layer, H * base, 18, 1.6)
        fill_below(img, ys, color)
        d = ImageDraw.Draw(img)
        x = rng.uniform(-40, 40)
        while x < W + 60:
            gy = float(ys[int(min(W - 1, max(0, x)))])
            s = size * rng.uniform(0.7, 1.25)
            d.polygon([(x, gy - s * 2.1), (x - s * 0.62, gy + 6), (x + s * 0.62, gy + 6)], fill=color + (255,))
            x += s * rng.uniform(0.9, 1.6)
    return vignette(img, 0.28)


def ocean():
    sky = gradient((90, 190, 255), (200, 236, 255), height=int(H * 0.58), power=0.9)
    sea = gradient((40, 150, 210), (10, 70, 140), height=H - int(H * 0.58), power=0.8)
    img = to_image(np.vstack([sky, sea])).convert("RGBA")
    img = glow(img, W * 0.68, H * 0.3, 300, (255, 255, 235), 0.7)
    d = ImageDraw.Draw(img)
    d.ellipse([W * 0.68 - 70, H * 0.3 - 70, W * 0.68 + 70, H * 0.3 + 70], fill=(255, 250, 220, 255))
    rng = random.Random(3)
    for i in range(70):
        y = H * 0.6 + (i / 70) ** 1.6 * H * 0.4
        x = rng.uniform(0, W)
        w = 30 + (i / 70) * 160
        d.line([(x - w, y), (x + w, y)], fill=(210, 240, 255, 120), width=2 + i // 25)
    # ufukta kucuk bir ada: kum + yesil tepe
    hy = H * 0.58
    d.ellipse([W * 0.12, hy - 26, W * 0.34, hy + 22], fill=(240, 220, 160, 255))
    d.ellipse([W * 0.15, hy - 64, W * 0.31, hy + 6], fill=(70, 160, 95, 255))
    d.ellipse([W * 0.19, hy - 92, W * 0.27, hy - 30], fill=(56, 140, 82, 255))
    return vignette(img, 0.25)


def desert():
    img = to_image(gradient((255, 190, 110), (255, 236, 190), power=0.8)).convert("RGBA")
    img = glow(img, W * 0.7, H * 0.28, 300, (255, 255, 230), 0.8)
    d = ImageDraw.Draw(img)
    d.ellipse([W * 0.7 - 80, H * 0.28 - 80, W * 0.7 + 80, H * 0.28 + 80], fill=(255, 248, 215, 255))
    fill_below(img, ridge(12, H * 0.62, 36, 0.9, 0.3), (238, 176, 98))
    fill_below(img, ridge(13, H * 0.72, 44, 0.8, 0.3), (222, 146, 74))
    fill_below(img, ridge(14, H * 0.85, 40, 0.7, 0.3), (196, 116, 58))
    return vignette(img, 0.28)


def snow():
    img = to_image(gradient((120, 170, 235), (225, 240, 255), power=1.0)).convert("RGBA")
    d = ImageDraw.Draw(img)
    rng = random.Random(21)
    peaks = [(W * 0.15, H * 0.3), (W * 0.45, H * 0.18), (W * 0.78, H * 0.26), (W * 1.02, H * 0.36)]
    for px, py in peaks:
        w = W * 0.32
        d.polygon([(px, py), (px - w, H * 0.75), (px + w, H * 0.75)], fill=(110, 130, 175, 255))
        d.polygon([(px, py), (px - w * 0.28, py + (H * 0.75 - py) * 0.28),
                   (px - w * 0.08, py + (H * 0.75 - py) * 0.22), (px + w * 0.1, py + (H * 0.75 - py) * 0.3),
                   (px + w * 0.28, py + (H * 0.75 - py) * 0.28)], fill=(250, 252, 255, 255))
    fill_below(img, ridge(15, H * 0.8, 24, 1.4), (235, 244, 255))
    for _ in range(140):
        x, y, r = rng.uniform(0, W), rng.uniform(0, H), rng.uniform(1.5, 3.5)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 255, 170))
    return vignette(img, 0.25)


BACKGROUNDS = {
    "gun_batimi": sunset,
    "gece": night,
    "orman": forest,
    "okyanus": ocean,
    "col": desert,
    "karli_daglar": snow,
}

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, draw in BACKGROUNDS.items():
        image = draw().convert("RGB")
        image.save(os.path.join(OUT, f"arkaplan_{name}.png"), optimize=True)
        print("ok", name)
