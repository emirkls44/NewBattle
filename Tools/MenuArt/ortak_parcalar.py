"""
Sonbahar temasinin ortak arayuz parcalari (onaylanan taslaklardaki gorunum):
ahsap pano, koyu ahsap baslik, parsomen, yazi kutusu, yaprak biçimli butonlar (yesil / turuncu /
kirmizi / mavi / ahsap / beyaz-boyanabilir), yuvarlak ahsap kapat dugmesi, kamuflaj pano ve
koselere konan yaprak kumesi. Hepsi 9 dilimli (kenar olculeri MenuArtImporter'da).

Calistir:   python Tools/MenuArt/ortak_parcalar.py   (Pillow + numpy gerekir)
"""

import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(os.path.dirname(HERE)), "Assets", "GameArt", "MenuUI")
SS = 4  # yumusak kenar icin buyuk cizip kucultme

INK = (62, 34, 16)
LIP = (44, 23, 10)
rng = np.random.default_rng(7)


def rounded_mask(w, h, radii):
    """radii: (sol ust, sag ust, sag alt, sol alt)"""
    W, H = w * SS, h * SS
    big = Image.new("L", (W, H), 255)
    d = ImageDraw.Draw(big)
    for (x0, y0, sx, sy), r in zip(((0, 0, 1, 1), (W, 0, -1, 1), (W, H, -1, -1), (0, H, 1, -1)), radii):
        r *= SS
        if r <= 0:
            continue
        d.rectangle((min(x0, x0 + sx * r), min(y0, y0 + sy * r), max(x0, x0 + sx * r) - 1, max(y0, y0 + sy * r) - 1), fill=0)
        cx, cy = x0 + sx * r, y0 + sy * r
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    return np.array(big.resize((w, h), Image.LANCZOS)).astype(float) / 255.0


def shrink(radii, by):
    return tuple(max(0, r - by) for r in radii)


def inset(mask_fn, w, h, radii, by):
    m = np.zeros((h, w))
    inner = rounded_mask(w - 2 * by, h - 2 * by, shrink(radii, by))
    m[by:h - by, by:w - by] = inner
    return m


def gradient(h, w, top, bottom):
    t = np.linspace(0, 1, h)[:, None, None]
    return np.array(top, float) * (1 - t) + np.array(bottom, float) * t + np.zeros((h, w, 3))


def wood_grain(h, w, strength=1.0):
    g = np.zeros((h, w))
    y = 0
    while y < h:
        y += int(rng.integers(6, 14))
        if y >= h:
            break
        thick = int(rng.integers(1, 3))
        wave = (np.sin(np.linspace(0, rng.uniform(2, 6), w) + rng.uniform(0, 6)) * rng.uniform(0, 1.5)).astype(int)
        for x in range(w):
            yy = y + wave[x]
            if 0 <= yy < h:
                g[yy:yy + thick, x] -= 0.07 * strength
    g += rng.normal(0, 0.012, (h, w))
    return g[..., None]


def compose(layers, w, h):
    """layers: (mask, rgb) ust uste; sonuc RGBA."""
    rgb = np.zeros((h, w, 3))
    alpha = np.zeros((h, w))
    for mask, color in layers:
        c = color if isinstance(color, np.ndarray) else np.array(color, float) + np.zeros((h, w, 3))
        rgb = rgb * (1 - mask[..., None]) + c * mask[..., None]
        alpha = alpha + mask * (1 - alpha)
    return rgb, alpha


def save(name, rgb, alpha):
    img = np.dstack([np.clip(rgb, 0, 255), np.clip(alpha * 255, 0, 255)]).astype(np.uint8)
    Image.fromarray(img, "RGBA").save(os.path.join(OUT, name))


def plank(name, w, h, radii, top, bottom, border=INK, lip=LIP, lip_h=10, edge=6, grain=1.0, highlight=0.28):
    body_h = h - lip_h
    outer = np.zeros((h, w))
    outer[lip_h:, :] = rounded_mask(w, body_h, radii)  # alt gölge (dudak)
    face_outer = np.zeros((h, w))
    face_outer[:body_h, :] = rounded_mask(w, body_h, radii)
    face_inner = np.zeros((h, w))
    face_inner[:body_h, :] = inset(None, w, body_h, radii, edge)
    fill = gradient(h, w, top, bottom)
    if grain:
        fill = fill * (1 + wood_grain(h, w, grain))
    # ust ic parlama / alt ic golge
    shade = np.zeros((h, w, 1))
    rows = np.arange(h)[:, None, None]
    shade += np.where((rows > edge) & (rows < edge + 6), highlight, 0)
    shade += np.where((rows > body_h - edge - 9) & (rows < body_h - edge), -0.18, 0)
    fill = fill * (1 + shade)
    rgb, alpha = compose([(outer, lip), (face_outer, border), (face_inner, fill)], w, h)
    save(name, rgb, alpha)


def parchment(name, w, h, radius, paper=(243, 222, 181), edge_color=(184, 138, 82), edge=5):
    radii = (radius,) * 4
    outer = rounded_mask(w, h, radii)
    inner = inset(None, w, h, radii, edge)
    fill = np.array(paper, float) + np.zeros((h, w, 3))
    cols = np.arange(w)[None, :, None]
    fill *= 1 - 0.035 * ((cols % 17) < 3)
    fill *= 1 + rng.normal(0, 0.01, (h, w, 1))
    rows = np.arange(h)[:, None, None]
    fill *= 1 - 0.12 * (rows > h - edge - 7)
    rgb, alpha = compose([(outer, edge_color), (inner, fill)], w, h)
    save(name, rgb, alpha)


def camo(name, w, h, radius):
    radii = (radius,) * 4
    outer = rounded_mask(w, h, radii)
    inner = inset(None, w, h, radii, 6)
    fill = np.array((102, 133, 53), float) + np.zeros((h, w, 3))
    yy, xx = np.mgrid[0:h, 0:w]
    for _ in range(40):
        cx, cy = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(18, 46)
        col = np.array([(111, 143, 60), (88, 119, 47), (125, 154, 69)][int(rng.integers(0, 3))], float)
        blob = ((xx - cx) ** 2 + (yy - cy) ** 2) < r * r
        fill[blob] = col
    rgb, alpha = compose([(outer, (38, 64, 26)), (inner, fill)], w, h)
    save(name, rgb, alpha)


def leaf_shape(kind, size, angle, color):
    """Tek yaprak (RGBA, buyuk olcekte)."""
    S = size * SS
    img = Image.new("RGBA", (S * 2, S * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = cy = S
    if kind == "maple":
        pts = [(50, 3), (58, 23), (72, 13), (70, 35), (92, 29), (80, 49), (97, 57), (76, 63), (82, 81), (60, 73),
               (53, 96), (50, 79), (47, 96), (40, 73), (18, 81), (24, 63), (3, 57), (20, 49), (8, 29), (30, 35),
               (28, 13), (42, 23)]
        scale = S / 100.0
        poly = [((x - 50) * scale, (y - 50) * scale) for x, y in pts]
    else:
        poly = []
        for t in np.linspace(0, 2 * math.pi, 60):
            x = math.cos(t) * S * 0.5
            y = math.sin(t) * S * 0.28 * (1 - 0.25 * math.cos(t))
            poly.append((x, y))
    a = math.radians(angle)
    rot = [(cx + x * math.cos(a) - y * math.sin(a), cy + x * math.sin(a) + y * math.cos(a)) for x, y in poly]
    d.polygon(rot, fill=color + (255,), outline=INK + (255,), width=int(3.5 * SS))
    if kind == "maple":
        for tip in ((50, 3), (92, 29), (8, 29), (53, 96)):
            x, y = (tip[0] - 50) * S / 100.0 * 0.8, (tip[1] - 50) * S / 100.0 * 0.8
            d.line([(cx, cy), (cx + x * math.cos(a) - y * math.sin(a), cy + x * math.sin(a) + y * math.cos(a))],
                   fill=INK + (130,), width=int(2 * SS))
        return img
    # damar
    vein = [(cx + x * math.cos(a), cy + x * math.sin(a)) for x in (-S * 0.42, S * 0.42)]
    d.line(vein, fill=INK + (150,), width=int(2 * SS))
    return img


def leaf_cluster(name):
    w, h = 230, 190
    canvas = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    leaves = [("oval", 104, -25, (126, 168, 58), (128, 46)), ("oval", 92, 70, (233, 162, 59), (52, 112)),
              ("maple", 132, -18, (224, 116, 44), (74, 68)), ("maple", 78, 20, (201, 84, 31), (160, 122)),
              ("oval", 74, 15, (233, 162, 59), (186, 58))]
    for kind, size, angle, color, (x, y) in leaves:
        leaf = leaf_shape(kind, size, angle, color)
        canvas.alpha_composite(leaf, (int(x * SS - leaf.width / 2), int(y * SS - leaf.height / 2)))
    canvas.resize((w, h), Image.LANCZOS).save(os.path.join(OUT, name))


def round_wood(name, size=128):
    w = h = size
    outer = np.zeros((h, w))
    outer[8:, :] = rounded_mask(w, h - 8, (60,) * 4)
    face = np.zeros((h, w))
    face[:h - 8, :] = rounded_mask(w, h - 8, (60,) * 4)
    inner = np.zeros((h, w))
    inner[:h - 8, :] = inset(None, w, h - 8, (60,) * 4, 6)
    fill = gradient(h, w, (160, 106, 58), (110, 66, 33)) * (1 + wood_grain(h, w, 0.6))
    rgb, alpha = compose([(outer, LIP), (face, INK), (inner, fill)], w, h)
    save(name, rgb, alpha)


if __name__ == "__main__":
    plank("ahsap_panel.png", 256, 512, (36,) * 4, (155, 102, 56), (127, 81, 43), lip_h=14, edge=9)
    plank("ahsap_baslik.png", 256, 128, (28, 28, 12, 12), (109, 64, 32), (86, 49, 26), lip_h=8, edge=8, highlight=0.18)
    parchment("parsomen.png", 128, 128, 22)
    parchment("yazi_kutusu.png", 128, 96, 18, paper=(255, 246, 228), edge_color=(184, 138, 82), edge=4)
    leaf_radii = (46, 14, 46, 14)
    buttons = {
        "yesil": ((143, 203, 69), (95, 156, 39), (47, 93, 19)),
        "turuncu": ((245, 154, 72), (220, 99, 32), (122, 46, 12)),
        "kirmizi": ((224, 98, 63), (184, 57, 31), (106, 28, 11)),
        "mavi": ((99, 183, 245), (47, 134, 208), (19, 74, 122)),
        "beyaz": ((255, 255, 255), (222, 222, 222), (120, 120, 120)),
    }
    for key, (top, bottom, edge_color) in buttons.items():
        plank(f"buton_{key}.png", 256, 160, leaf_radii, top, bottom, border=edge_color, lip=edge_color,
              lip_h=12, edge=7, grain=0.0, highlight=0.3)
    plank("buton_ahsap.png", 256, 160, (22,) * 4, (155, 102, 56), (127, 81, 43), lip_h=12, edge=7, grain=0.7)
    plank("cip_beyaz.png", 128, 96, (18,) * 4, (255, 255, 255), (226, 226, 226), border=(90, 60, 30), lip=(90, 60, 30),
          lip_h=6, edge=5, grain=0.0, highlight=0.2)
    round_wood("yuvarlak_ahsap.png")
    camo("kamuflaj_panel.png", 256, 512, 30)
    plank("kamuflaj_baslik.png", 256, 128, (28, 28, 12, 12), (70, 108, 40), (56, 92, 33), border=(38, 64, 26),
          lip=(29, 49, 18), lip_h=8, edge=8, grain=0.0, highlight=0.2)
    leaf_cluster("yaprak_kume.png")
    print("ok")
