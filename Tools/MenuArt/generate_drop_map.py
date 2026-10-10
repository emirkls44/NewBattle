"""
Harita secim ekrani icin GECICI kare harita resmi ve bolge listesi.

Calistirma (proje klasorunden):
    python Tools/MenuArt/generate_drop_map.py

Cikti:
    Assets/GameArt/DropMap/harita_gecici.png   (1024x1024)
    Assets/GameArt/DropMap/harita_bolgeleri.json

Bolgeler Unity koordinatinda tanimli: (0,0) sol ALT, (1,1) sag UST kose.
Kurulum araci JSON'u okuyup HaritaVerisi dosyasina yazar; sonrasinda
bolgeleri Unity'de Inspector'dan degistirirsin. Gercek haritanin kusbakisi
resmi gelince bu dosya tamamen devre disi kalir.
"""

import json
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "GameArt", "DropMap")
SIZE = 1024
SS = 2

# id, ad, x, y, genislik, yukseklik, cizim tipi
REGIONS = [
    ("koy", "Köy", 0.09, 0.66, 0.20, 0.25, "village"),
    ("havalimani", "Havalimanı", 0.32, 0.66, 0.22, 0.26, "airfield"),
    ("tren", "Tren İstasyonu", 0.60, 0.66, 0.32, 0.26, "train"),
    ("santiye", "Şantiye", 0.09, 0.38, 0.20, 0.24, "construction"),
    ("mezarlik", "Mezarlık", 0.32, 0.38, 0.21, 0.24, "cemetery"),
    ("ciftlik", "Çiftlik", 0.60, 0.38, 0.18, 0.24, "farm"),
    ("labirent", "Labirent", 0.80, 0.38, 0.12, 0.24, "maze"),
    ("arena", "Arena", 0.09, 0.08, 0.20, 0.25, "arena"),
    ("kamp", "Kampçı Cenneti", 0.31, 0.08, 0.18, 0.25, "camp"),
    ("liman", "Liman", 0.505, 0.05, 0.14, 0.27, "port"),
    ("maden", "Maden", 0.68, 0.08, 0.24, 0.25, "mine"),
]

OCEAN = (39, 116, 168)
SHALLOW = (61, 143, 196)
SAND = (243, 217, 160)
LAND = (201, 164, 108)
ROAD = (122, 104, 88)
CONCRETE = (190, 181, 170)
WATER = (61, 143, 196)
GRASS = (111, 175, 78)
PALM = (52, 112, 60)

random.seed(7)


def px(x, y):
    """Unity normalize koordinati (sol alt orijin) -> resim pikseli (sol ust)."""
    return x * SIZE * SS, (1 - y) * SIZE * SS


def box(x, y, w, h):
    x0, y1 = px(x, y)
    x1, y0 = px(x + w, y + h)
    return x0, y0, x1, y1


def rect(d, x, y, w, h, color, radius=0):
    if radius:
        d.rounded_rectangle(box(x, y, w, h), radius * SS, fill=color)
    else:
        d.rectangle(box(x, y, w, h), fill=color)


def sub(r, fx, fy, fw, fh):
    """Bolge icinde oransal alt dikdortgen."""
    x, y, w, h = r
    return x + fx * w, y + fy * h, fw * w, fh * h


# ---------------------------------------------------------------- bolge cizimleri

def village(d, r):
    rect(d, *r, (214, 178, 122), 10)
    for i in range(3):
        for j in range(3):
            color = random.choice([(169, 69, 47), (150, 60, 44), (140, 140, 140), (186, 92, 60)])
            rect(d, *sub(r, 0.08 + i * 0.31, 0.1 + j * 0.3, 0.2, 0.18), color, 3)
    d.ellipse(box(*sub(r, 0.36, 0.38, 0.28, 0.24)), fill=(126, 186, 96))


def airfield(d, r):
    rect(d, *r, CONCRETE, 10)
    rect(d, *sub(r, 0.62, 0.05, 0.14, 0.9), (110, 116, 124))
    for k in range(7):
        rect(d, *sub(r, 0.685, 0.1 + k * 0.12, 0.015, 0.06), (240, 240, 240))
    # ucak
    cx, cy = px(r[0] + r[2] * 0.36, r[1] + r[3] * 0.62)
    s = r[2] * SIZE * SS
    d.polygon([(cx - s * .02, cy - s * .25), (cx + s * .02, cy - s * .25), (cx + s * .02, cy + s * .2), (cx - s * .02, cy + s * .2)], fill=(120, 140, 150))
    d.polygon([(cx - s * .22, cy), (cx + s * .22, cy), (cx + s * .22, cy + s * .05), (cx - s * .22, cy + s * .05)], fill=(120, 140, 150))
    d.polygon([(cx - s * .08, cy + s * .17), (cx + s * .08, cy + s * .17), (cx + s * .08, cy + s * .2), (cx - s * .08, cy + s * .2)], fill=(120, 140, 150))
    # hangarlar
    for k in range(2):
        rect(d, *sub(r, 0.06, 0.1 + k * 0.22, 0.2, 0.15), (88, 98, 110), 3)


def train(d, r):
    rect(d, *r, CONCRETE, 10)
    for k in range(3):
        x = 0.12 + k * 0.12
        rect(d, *sub(r, x, 0.04, 0.025, 0.92), (96, 84, 70))
        for t in range(14):
            rect(d, *sub(r, x - 0.012, 0.06 + t * 0.065, 0.05, 0.012), (120, 100, 80))
        for c, color in enumerate([(70, 110, 200), (230, 196, 60), (190, 60, 50)]):
            if (k + c) % 2 == 0:
                rect(d, *sub(r, x - 0.012, 0.15 + c * 0.25, 0.05, 0.16), color, 2)
    rect(d, *sub(r, 0.55, 0.45, 0.3, 0.4), (232, 222, 206), 4)
    rect(d, *sub(r, 0.58, 0.5, 0.24, 0.3), (150, 140, 128), 3)
    rect(d, *sub(r, 0.6, 0.08, 0.28, 0.2), (88, 98, 110), 3)


def construction(d, r):
    rect(d, *r, CONCRETE, 10)
    for k, color in enumerate([(64, 96, 150), (80, 80, 90), (64, 96, 150)]):
        rect(d, *sub(r, 0.1, 0.12 + k * 0.2, 0.2, 0.13), color, 2)
    rect(d, *sub(r, 0.55, 0.2, 0.06, 0.62), (232, 140, 40))
    rect(d, *sub(r, 0.35, 0.68, 0.5, 0.06), (232, 140, 40))
    for _ in range(10):
        rect(d, *sub(r, random.uniform(0.35, 0.85), random.uniform(0.1, 0.5), 0.05, 0.04), (150, 120, 90), 2)


def cemetery(d, r):
    rect(d, *r, (184, 148, 99), 10)
    x0, y0, x1, y1 = box(*sub(r, 0.06, 0.08, 0.88, 0.84))
    d.rectangle((x0, y0, x1, y1), outline=(110, 104, 98), width=4 * SS)
    for i in range(4):
        for j in range(4):
            rect(d, *sub(r, 0.16 + i * 0.2, 0.18 + j * 0.18, 0.07, 0.08), (150, 146, 140), 2)
    d.ellipse(box(*sub(r, 0.62, 0.18, 0.22, 0.2)), fill=(232, 226, 210))


def farm(d, r):
    rect(d, *r, (227, 162, 92), 10)
    for i in range(3):
        for j in range(3):
            if (i + j) % 2 == 0:
                d.ellipse(box(*sub(r, 0.12 + i * 0.28, 0.12 + j * 0.28, 0.1, 0.08)), fill=(240, 210, 70))
    rect(d, *sub(r, 0.5, 0.55, 0.3, 0.3), (150, 70, 52), 3)
    rect(d, *sub(r, 0.15, 0.62, 0.2, 0.2), (118, 118, 118), 3)


def maze(d, r):
    rect(d, *r, (150, 210, 110), 8)
    color = (90, 160, 70)
    w = 0.08
    for k in range(5):
        y = 0.1 + k * 0.19
        x = 0.1 if k % 2 == 0 else 0.25
        rect(d, *sub(r, x, y, 0.65, w * 0.5), color)
    for k in range(4):
        x = 0.1 + k * 0.27
        rect(d, *sub(r, x, 0.1 + (k % 2) * 0.2, w, 0.55), color)


def arena(d, r):
    rect(d, *r, LAND, 10)
    cx, cy = r[0] + r[2] / 2, r[1] + r[3] / 2
    rr = min(r[2], r[3]) * 0.42
    d.ellipse(box(cx - rr, cy - rr, rr * 2, rr * 2), fill=(214, 186, 140), outline=(150, 120, 90), width=5 * SS)
    d.ellipse(box(cx - rr * 0.55, cy - rr * 0.55, rr * 1.1, rr * 1.1), outline=(170, 140, 100), width=3 * SS)
    for a in range(8):
        ang = a * math.tau / 8
        x = cx + math.cos(ang) * rr * 0.8
        y = cy + math.sin(ang) * rr * 0.8
        d.ellipse(box(x - 0.008, y - 0.008, 0.016, 0.016), fill=(140, 140, 140))


def camp(d, r):
    rect(d, *r, GRASS, 10)
    for i, color in enumerate([(240, 140, 50), (70, 130, 210), (220, 70, 60), (240, 200, 60)]):
        x, y = r[0] + r[2] * (0.2 + (i % 2) * 0.45), r[1] + r[3] * (0.25 + (i // 2) * 0.4)
        a, b, c = px(x, y + 0.04), px(x - 0.03, y - 0.02), px(x + 0.03, y - 0.02)
        d.polygon([a, b, c], fill=color)
    d.ellipse(box(*sub(r, 0.42, 0.42, 0.14, 0.12)), fill=(120, 90, 60))


def port(d, r):
    rect(d, *r, CONCRETE, 6)
    rect(d, *sub(r, 0.35, 0.0, 0.3, 1.0), WATER)
    for k in range(3):
        rect(d, *sub(r, 0.05, 0.12 + k * 0.28, 0.3, 0.06), (150, 110, 70))
        rect(d, *sub(r, 0.65, 0.12 + k * 0.28, 0.3, 0.06), (150, 110, 70))
    rect(d, *sub(r, 0.4, 0.15, 0.2, 0.35), (245, 245, 245), 6)
    rect(d, *sub(r, 0.44, 0.3, 0.12, 0.12), (220, 70, 60), 3)
    for k, color in enumerate([(230, 196, 60), (70, 110, 200), (190, 60, 50)]):
        rect(d, *sub(r, 0.06 + (k % 2) * 0.66, 0.55 + k * 0.1, 0.22, 0.08), color, 2)


def mine(d, r):
    rect(d, *r, (226, 150, 88), 10)
    for _ in range(9):
        x, y = random.uniform(0.1, 0.85), random.uniform(0.1, 0.8)
        d.ellipse(box(*sub(r, x, y, 0.07, 0.07)), fill=(236, 200, 70))
    for k in range(2):
        rect(d, *sub(r, 0.32 + k * 0.22, 0.48, 0.14, 0.32), (100, 108, 120), 3)
    d.ellipse(box(*sub(r, 0.75, 0.6, 0.18, 0.28)), fill=(210, 130, 90))


DRAW = {
    "village": village, "airfield": airfield, "train": train, "construction": construction,
    "cemetery": cemetery, "farm": farm, "maze": maze, "arena": arena, "camp": camp,
    "port": port, "mine": mine,
}


def palm(d, x, y):
    cx, cy = px(x, y)
    r = 7 * SS
    for a in range(5):
        ang = a * math.tau / 5
        d.line([(cx, cy), (cx + math.cos(ang) * r * 1.6, cy + math.sin(ang) * r * 1.6)], fill=PALM, width=4 * SS)
    d.ellipse((cx - 3 * SS, cy - 3 * SS, cx + 3 * SS, cy + 3 * SS), fill=(90, 70, 40))


def inside_region(x, y):
    for _, _, rx, ry, rw, rh, _ in REGIONS:
        if rx - 0.01 <= x <= rx + rw + 0.01 and ry - 0.01 <= y <= ry + rh + 0.01:
            return True
    return False


def main():
    os.makedirs(OUT, exist_ok=True)
    S = SIZE * SS
    img = Image.new("RGB", (S, S), OCEAN)
    d = ImageDraw.Draw(img)

    # sig su halkasi, kum, kara
    d.rounded_rectangle(box(0.02, 0.02, 0.96, 0.96), 70 * SS, fill=SHALLOW)
    d.rounded_rectangle(box(0.045, 0.045, 0.91, 0.91), 60 * SS, fill=SAND)
    d.rounded_rectangle(box(0.07, 0.065, 0.86, 0.865), 48 * SS, fill=LAND)

    # yollar
    road = 0.016
    for y in (0.345, 0.645):
        rect(d, 0.07, y - road / 2, 0.86, road, ROAD)
    for x in (0.305, 0.79):
        rect(d, x - road / 2, 0.065, road, 0.865, ROAD)

    # nehir (kuzeyden limana)
    rect(d, 0.555, 0.25, 0.03, 0.73, WATER)
    for y in (0.345, 0.645):
        rect(d, 0.548, y - 0.012, 0.044, 0.024, (150, 110, 70))

    for _, _, x, y, w, h, kind in REGIONS:
        DRAW[kind](d, (x, y, w, h))

    # dagilmis palmiyeler (bolgelerin ve yollarin disinda)
    for _ in range(140):
        x, y = random.uniform(0.08, 0.92), random.uniform(0.08, 0.92)
        if inside_region(x, y) or 0.54 < x < 0.6:
            continue
        if any(abs(y - ry) < 0.02 for ry in (0.345, 0.645)) or any(abs(x - rx) < 0.02 for rx in (0.305, 0.79)):
            continue
        palm(d, x, y)

    img = img.resize((SIZE, SIZE), Image.LANCZOS)
    img.save(os.path.join(OUT, "harita_gecici.png"))

    regions = [
        {"id": rid, "name": name, "x": x, "y": y, "w": w, "h": h}
        for rid, name, x, y, w, h, _ in REGIONS
    ]
    with open(os.path.join(OUT, "harita_bolgeleri.json"), "w", encoding="utf-8") as f:
        json.dump({"regions": regions}, f, ensure_ascii=False, indent=2)

    print("Harita uretildi:", os.path.abspath(OUT))


if __name__ == "__main__":
    main()
