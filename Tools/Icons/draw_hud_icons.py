"""
HUD rozet ikonlarini (can, kalkan, kuru kafa, oyuncu) sifirdan cizer. Internetten
alinmis, lisansi belirsiz eski ikonlarin yerine gecer; dosya adlari ayni kaldigi icin
sahnedeki baglantilar bozulmaz.

Calistir:   python Tools/Icons/draw_hud_icons.py
Cikti:      Assets/GameArt/HudIcons/{health_2.jpg, Shield.png, Skull.png, Person.png}
"""

import math
import os

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "GameArt", "HudIcons")
SS = 4  # kenarlar yumusak olsun diye buyuk cizip kucultulur

OUTLINE = (40, 38, 44, 255)


def canvas(size):
    return Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))


def finish(img, size):
    return img.resize((size, size), Image.LANCZOS)


def health(size=290):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    s = size * SS
    pad, w = 0.03 * s, 0.035 * s
    d.ellipse([pad, pad, s - pad, s - pad], fill=(255, 52, 82, 255), outline=OUTLINE, width=int(w))
    arm, thick = 0.27 * s, 0.085 * s
    c = s / 2
    for box in ([c - arm, c - thick, c + arm, c + thick], [c - thick, c - arm, c + thick, c + arm]):
        d.rounded_rectangle(box, radius=thick * 0.45, fill=OUTLINE)
    inner = w * 0.75
    for box in ([c - arm + inner, c - thick + inner, c + arm - inner, c + thick - inner],
                [c - thick + inner, c - arm + inner, c + thick - inner, c + arm - inner]):
        d.rounded_rectangle(box, radius=thick * 0.35, fill=(255, 255, 255, 255))
    out = finish(img, size)
    bg = Image.new("RGB", out.size, (255, 255, 255))
    bg.paste(out, mask=out.split()[3])
    return bg


def shield_points(s, inset):
    top, bottom = 0.06 * s + inset, 0.95 * s - inset
    left, right = 0.12 * s + inset, 0.88 * s - inset
    c = s / 2
    pts = [(c, top), (right, top + 0.07 * s), (right, 0.45 * s)]
    # alt kisim: yanlardan uca dogru kavis
    steps = 24
    for i in range(1, steps + 1):
        t = i / steps
        x = right + (c - right) * (1 - math.cos(t * math.pi / 2))
        y = 0.45 * s + (bottom - 0.45 * s) * math.sin(t * math.pi / 2)
        pts.append((x, y))
    for i in range(steps - 1, -1, -1):
        t = i / steps
        x = left + (c - left) * (1 - math.cos(t * math.pi / 2))
        y = 0.45 * s + (bottom - 0.45 * s) * math.sin(t * math.pi / 2)
        pts.append((x, y))
    pts += [(left, 0.45 * s), (left, top + 0.07 * s)]
    return pts


def shield(size=240):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    s = size * SS
    d.polygon(shield_points(s, 0), fill=(205, 214, 222, 255))
    inner = shield_points(s, 0.05 * s)
    d.polygon(inner, fill=(92, 172, 238, 255))
    # sag yari koyu
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).polygon(inner, fill=255)
    half = Image.new("L", img.size, 0)
    ImageDraw.Draw(half).rectangle([s / 2, 0, s, s], fill=255)
    dark = Image.new("RGBA", img.size, (34, 102, 160, 255))
    img.paste(dark, mask=Image.fromarray(__import__("numpy").minimum(
        __import__("numpy").array(mask), __import__("numpy").array(half))))
    return finish(img, size)


def skull(size=512):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    s = size * SS
    w = int(0.022 * s)
    fill = (232, 242, 250, 255)
    # golge
    d.ellipse([0.27 * s, 0.86 * s, 0.73 * s, 0.93 * s], fill=(0, 0, 0, 40))
    # kafa + cene
    d.ellipse([0.14 * s, 0.06 * s, 0.86 * s, 0.70 * s], fill=fill, outline=OUTLINE, width=w)
    d.rounded_rectangle([0.27 * s, 0.50 * s, 0.73 * s, 0.84 * s], radius=0.08 * s, fill=fill, outline=OUTLINE, width=w)
    d.rectangle([0.27 * s + w, 0.48 * s, 0.73 * s - w, 0.62 * s], fill=fill)
    # dis aralari
    for x in (0.42, 0.58):
        d.rounded_rectangle([x * s - w * 0.5, 0.72 * s, x * s + w * 0.5, 0.845 * s], radius=w * 0.5, fill=OUTLINE)
    # gozler
    for cx in (0.35, 0.65):
        r = 0.095 * s
        d.ellipse([cx * s - r, 0.43 * s - r, cx * s + r, 0.43 * s + r], fill=(110, 112, 120, 255), outline=OUTLINE, width=w)
    # burun
    d.polygon([(0.5 * s, 0.53 * s), (0.465 * s, 0.61 * s), (0.535 * s, 0.61 * s)], fill=OUTLINE)
    # parlama
    d.ellipse([0.40 * s, 0.11 * s, 0.60 * s, 0.15 * s], fill=(255, 255, 255, 230))
    return finish(img, size)


def person(size=640):
    import numpy as np
    s = size * SS
    mask = Image.new("L", (s, s), 0)
    d = ImageDraw.Draw(mask)
    d.ellipse([0.28 * s, 0.03 * s, 0.72 * s, 0.56 * s], fill=255)          # bas
    d.rounded_rectangle([0.40 * s, 0.45 * s, 0.60 * s, 0.75 * s], radius=0.05 * s, fill=255)  # boyun
    d.ellipse([0.02 * s, 0.70 * s, 0.98 * s, 1.40 * s], fill=255)          # omuzlar
    d.rectangle([0, 0.99 * s, s, s], fill=0)
    # ustten alta koyulasan mavi
    y = np.linspace(0, 1, s)[:, None]
    top, bottom = np.array([126, 168, 200]), np.array([62, 94, 130])
    rgb = (top + (bottom - top) * y[..., None]).astype(np.uint8)
    rgb = np.broadcast_to(rgb, (s, s, 3)).copy()
    alpha = np.array(mask)
    img = Image.fromarray(np.dstack([rgb, alpha]), "RGBA")
    return finish(img, size)


if __name__ == "__main__":
    health().save(os.path.join(OUT, "health_2.jpg"), quality=92)
    shield().save(os.path.join(OUT, "Shield.png"))
    skull().save(os.path.join(OUT, "Skull.png"))
    person().save(os.path.join(OUT, "Person.png"))
    print("ok")
