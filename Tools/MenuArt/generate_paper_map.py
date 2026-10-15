"""
Harita secim ekrani icin "eski kagit harita" gorselleri. Mevcut harita resmini
(harita_gecici.png) kagit / murekkep gorunumune cevirir; bolgeler ayni yerde kalir.

Calistir:   python Tools/MenuArt/generate_paper_map.py
Cikti:      Assets/GameArt/DropMap/harita_kagit.png  (1024x1024)
            Assets/GameArt/MenuUI/pin_kayisi.png     (inis noktasi igne isareti)
"""

import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Assets", "GameArt", "DropMap", "harita_gecici.png")
OUT_MAP = os.path.join(ROOT, "Assets", "GameArt", "DropMap", "harita_kagit.png")
OUT_PIN = os.path.join(ROOT, "Assets", "GameArt", "MenuUI", "pin_kayisi.png")

PAPER = np.array([243, 226, 190], float)
PAPER_DARK = np.array([214, 188, 140], float)
INK = np.array([92, 58, 30], float)


def paper_map():
    src = Image.open(SRC).convert("RGB")
    a = np.array(src, float)
    h, w, _ = a.shape
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0
    water = (b > r + 25) & (b > g)

    # Kara: rengin %30'u kalir, gerisi sepya murekkep tonu.
    gray = lum[..., None]
    sepia = PAPER_DARK * 0.35 + (PAPER - INK) * gray + INK * 0.55
    muted = a * 0.3 + sepia * 0.7
    out = np.where(water[..., None], PAPER_DARK, muted)

    # Su: kagit uzerinde ince dalga taramasi.
    yy, xx = np.mgrid[0:h, 0:w]
    wave = (np.sin((yy + 6 * np.sin(xx / 22.0)) / 7.0) > 0.92)
    out = np.where((water & wave)[..., None], out * 0.82 + INK * 0.18, out)

    # Murekkep kenar cizgileri.
    edges = np.array(Image.fromarray((lum * 255).astype(np.uint8)).filter(ImageFilter.FIND_EDGES), float) / 255.0
    edges = np.clip((edges - 0.08) * 4.0, 0, 1)[..., None]
    out = out * (1 - edges * 0.7) + INK * (edges * 0.7)

    # Kagit dokusu ve yanik kenarlar.
    rng = np.random.default_rng(3)
    grain = rng.normal(0, 5.0, (h, w))
    grain = np.array(Image.fromarray(np.clip(grain + 128, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2)), float) - 128
    out += grain[..., None]
    d = np.sqrt(((xx - w / 2) / (w / 2)) ** 2 + ((yy - h / 2) / (h / 2)) ** 2) / 1.42
    burn = np.clip((d - 0.55) / 0.45, 0, 1) ** 1.8
    out = out * (1 - burn[..., None] * 0.45) + np.array([120, 76, 38], float) * (burn[..., None] * 0.45)

    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(OUT_MAP, optimize=True)


def apricot_pin(size=256):
    s = size * 4
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = s / 2
    outline = (122, 48, 26, 255)
    body = (242, 153, 74, 255)
    # Damla: ustte yuvarlak kayisi, altta sivri uc (yere batan igne).
    d.ellipse([cx - s * 0.30, s * 0.10, cx + s * 0.30, s * 0.70], fill=outline)
    d.polygon([(cx - s * 0.20, s * 0.56), (cx + s * 0.20, s * 0.56), (cx, s * 0.96)], fill=outline)
    d.ellipse([cx - s * 0.26, s * 0.14, cx + s * 0.26, s * 0.66], fill=body)
    d.polygon([(cx - s * 0.16, s * 0.56), (cx + s * 0.16, s * 0.56), (cx, s * 0.90)], fill=body)
    # Kayisi cizgisi ve parlama
    d.arc([cx - s * 0.12, s * 0.16, cx + s * 0.14, s * 0.64], 250, 110, fill=(214, 110, 50, 255), width=int(s * 0.02))
    d.ellipse([cx - s * 0.16, s * 0.22, cx - s * 0.06, s * 0.34], fill=(255, 214, 160, 255))
    # Sap ve yaprak
    d.rectangle([cx - s * 0.015, s * 0.04, cx + s * 0.015, s * 0.15], fill=(110, 70, 36, 255))
    d.polygon([(cx + s * 0.01, s * 0.11), (cx + s * 0.20, s * 0.02), (cx + s * 0.27, s * 0.09), (cx + s * 0.10, s * 0.15)],
              fill=(79, 122, 52, 255))
    img.resize((size, size), Image.LANCZOS).save(OUT_PIN)


if __name__ == "__main__":
    paper_map()
    apricot_pin()
    print("ok")
