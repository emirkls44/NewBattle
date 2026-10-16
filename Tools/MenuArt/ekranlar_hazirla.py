"""
Sonbahar ekran tasarimlari: kullanicinin verdigi resimlerden oyun gorsellerini hazirlar.
Kaynaklar Tools/MenuArt/kaynak/*_tasarim.jpg (2000x1116), ciktilar Assets/GameArt/MenuUI/.

  Acilir pencereler (profil, ayarlar): resim ana menunun ustunde durur.
    - cerceve (kutuk baslik, dal kenarlar, yapraklar) resimden ayrilir; disi saydam, arkada
      gercek ana menu gorunur.
    - pencerenin mor ici yari saydam duz mor olur (arkadaki karakter hafifce gorunur);
      ustundeki dal / yaprak / yazilar kalir.
    - degisen yazilar (isim, seviye, sayilar, AÇIK/KAPALI, FPS, surum) silinir; oyun canli yazar.

Calistir:   python Tools/MenuArt/ekranlar_hazirla.py   (opencv-python gerekir)
Koordinatlar Unity tarafinda: Assets/Editor/Menu/MainMenuBuilder.AutumnScreens.cs.
"""

import os

import cv2
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, "Assets", "GameArt", "MenuUI")
SRC = os.path.join(HERE, "kaynak")
W, H = 2000, 1116

PANEL_ALPHA = 0.84  # mor pencere icinin opakligi


def load(name):
    return cv2.imread(os.path.join(SRC, name))


def menu_reference():
    """Ana menu tasarimi (pencere resimlerindeki arka plan) ayni olcude."""
    return cv2.resize(load("sonbahar_tasarim.jpg"), (W, H), interpolation=cv2.INTER_AREA)


def save_rgba(name, bgr, alpha):
    rgba = np.dstack([bgr, np.clip(alpha * 255, 0, 255).astype(np.uint8)])
    cv2.imwrite(os.path.join(OUT, name), rgba)


def frame_matte(img, core, margins):
    """
    Cerceveyi arka plandaki menuden ayirir: cekirdek dikdortgen kesin cerceve, kenar
    payinin disi kesin arka plan; aradaki bant menu resmiyle farka + GrabCut'a gore bolunur.
    """
    x0, y0, x1, y1 = core
    left, top, right, bottom = margins
    menu = menu_reference().astype(float)
    outside = np.ones((H, W), bool)
    outside[max(0, y0 - 150):y1 + 150, max(0, x0 - 150):x1 + 150] = False
    adjusted = menu.copy()
    for c in range(3):
        design = np.vstack([menu[..., c][outside], np.ones(outside.sum())]).T
        k, b = np.linalg.lstsq(design, img[..., c][outside].astype(float), rcond=None)[0]
        adjusted[..., c] = menu[..., c] * k + b
    diff = np.abs(cv2.GaussianBlur(img.astype(float), (0, 0), 2) -
                  cv2.GaussianBlur(adjusted, (0, 0), 2)).max(axis=2)

    mask = np.full((H, W), cv2.GC_PR_BGD, np.uint8)
    mask[diff > 45] = cv2.GC_PR_FGD
    mask[y0:y1, x0:x1] = cv2.GC_FGD
    allowed = np.zeros((H, W), bool)
    allowed[max(0, y0 - top):min(H, y1 + bottom), max(0, x0 - left):min(W, x1 + right)] = True
    mask[~allowed] = cv2.GC_BGD
    cv2.grabCut(img, mask, None, np.zeros((1, 65)), np.zeros((1, 65)), 4, cv2.GC_INIT_WITH_MASK)
    fg = ((mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD)).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(fg)
    biggest = 1 + np.argmax(stats[1:, cv2.CC_STAT_AREA])
    fg = (labels == biggest).astype(np.uint8)
    fg = cv2.morphologyEx(fg, cv2.MORPH_CLOSE, np.ones((7, 7), np.uint8))
    # delikleri doldur
    contours, _ = cv2.findContours(fg, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    fg = np.zeros_like(fg)
    cv2.drawContours(fg, contours, -1, 1, thickness=-1)
    return cv2.GaussianBlur(fg.astype(float), (0, 0), 1.2)


def purple_weight(img):
    """Pencerenin duz mor zemini (1) / ustundeki dal, yaprak, yazi (0)."""
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV).astype(float)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    hue = np.clip(1 - np.maximum(0, np.abs(h - 132) - 14) / 8, 0, 1)
    sat = np.clip((s - 30) / 25, 0, 1)
    val = np.clip((v - 40) / 20, 0, 1) * np.clip((200 - v) / 25, 0, 1)
    return cv2.GaussianBlur(hue * sat * val, (0, 0), 0.8)


def panel_interior(img, alpha, interior, flat_color, keep_rects=(), erase_rects=()):
    """Mor zemini yari saydam duz mor yapar; keep_rects opak kalir, erase_rects duz mor olur."""
    x0, y0, x1, y1 = interior
    weight = np.zeros((H, W))
    weight[y0:y1, x0:x1] = purple_weight(img)[y0:y1, x0:x1]
    for rx0, ry0, rx1, ry1 in erase_rects:
        weight[ry0:ry1, rx0:rx1] = 1.0
    for mask in keep_rects:
        weight[mask > 0] = 0.0
    edge = np.zeros((H, W))
    edge[y0:y1, x0:x1] = 1.0
    edge = cv2.GaussianBlur(edge, (0, 0), 3)
    weight *= edge
    flat = np.array(flat_color, float)
    out = img.astype(float) * (1 - weight[..., None]) + flat * weight[..., None]
    alpha = alpha * (1 - weight) + PANEL_ALPHA * weight
    return out.astype(np.uint8), alpha


def fill_rows(img, x0, y0, x1, y1, edge=4):
    """Yaziyi siler: her satir soldaki ve sagdaki renkten yatay doldurulur (dugme yuzleri icin)."""
    out = img.astype(float)
    t = np.linspace(0, 1, x1 - x0)[:, None]
    for y in range(y0, y1):
        left = out[y, max(0, x0 - edge):x0].mean(axis=0)
        right = out[y, x1:x1 + edge].mean(axis=0)
        out[y, x0:x1] = left * (1 - t) + right * t
    img[:] = np.clip(out, 0, 255).astype(np.uint8)


def dark_tiles(img, region):
    """Profildeki koyu istatistik kutulari (maske listesi + sinirlar)."""
    x0, y0, x1, y1 = region
    v = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)[..., 2]
    dark = np.zeros((H, W), np.uint8)
    dark[y0:y1, x0:x1] = (v[y0:y1, x0:x1] < 62).astype(np.uint8)
    dark = cv2.morphologyEx(dark, cv2.MORPH_CLOSE, np.ones((25, 25), np.uint8))
    count, labels, stats, _ = cv2.connectedComponentsWithStats(dark)
    tiles = []
    for i in range(1, count):
        x, y, w, h, area = stats[i]
        if area > 30000:
            tile = (labels == i).astype(np.uint8)
            contours, _ = cv2.findContours(tile, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
            tile = np.zeros_like(tile)
            cv2.drawContours(tile, contours, -1, 1, thickness=-1)
            tiles.append((tile, (x, y, x + w, y + h)))
    tiles.sort(key=lambda t: (t[1][1] // 100, t[1][0]))
    return tiles


def bright_buttons(img, region):
    """Ayarlardaki renkli dugmeler (yesil / mavi / mor), yukaridan asagi."""
    x0, y0, x1, y1 = region
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
    mask = np.zeros((H, W), np.uint8)
    sub = hsv[y0:y1, x0:x1]
    mask[y0:y1, x0:x1] = ((sub[..., 1] > 110) & (sub[..., 2] > 140)).astype(np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((15, 15), np.uint8))
    count, labels, stats, _ = cv2.connectedComponentsWithStats(mask)
    boxes = [tuple(int(v) for v in (x, y, x + w, y + h)) for x, y, w, h, area in stats[1:] if area > 15000]
    return sorted(boxes, key=lambda b: b[1])


def profile():
    img = load("profil_tasarim.jpg")
    alpha = frame_matte(img, core=(362, 135, 1635, 950), margins=(26, 60, 60, 50))
    interior = (392, 252, 1612, 930)
    tiles = dark_tiles(img, interior)
    print("profil kutulari:", [t[1] for t in tiles])
    # sayilar silinir (ust yari), etiketler kalir
    for tile, (x0, y0, x1, y1) in tiles:
        color = np.median(img[y0 + 12:y0 + 30, x0 + 30:x1 - 30].reshape(-1, 3), axis=0)
        number = np.zeros((H, W), bool)
        number[y0 + 14:y0 + int((y1 - y0) * 0.58), x0 + 14:x1 - 14] = True
        number &= tile > 0
        img[number] = color.astype(np.uint8)
    flat = (128, 70, 90)
    img, alpha = panel_interior(img, alpha, interior, flat, keep_rects=[t[0] for t in tiles],
                                erase_rects=[(760, 268, 1240, 425)])
    save_rgba("tasarim_profil.png", img, alpha)
    return [t[1] for t in tiles]


def settings():
    img = load("ayarlar_tasarim.jpg")
    alpha = frame_matte(img, core=(392, 40, 1600, 1050), margins=(30, 40, 70, 60))
    interior = (420, 160, 1575, 1035)
    # dugmeler (muzik, efektler, kare hizi, isim, hediye kodu); bright_buttons ile olculdu,
    # ikinci yesil dugme sagdaki yapraklara bitistigi icin elle verilir.
    buttons = [(1187, 197, 1546, 314), (1187, 331, 1546, 448), (1190, 463, 1545, 584),
               (1187, 605, 1546, 719), (1186, 733, 1546, 852)]
    flat = (128, 70, 90)
    img, alpha = panel_interior(img, alpha, interior, flat, erase_rects=[(455, 980, 700, 1040)])

    # Muzik / efektler: ayri dugme resmi (acik = yesil, kapali = gri); pencerede yerleri bos.
    padx, pady = 26, 12
    music = buttons[0]
    bx0, by0, bx1, by1 = music[0] - padx, music[1] - pady, music[2] + padx, music[3] + pady
    face = img.copy()
    cx0, cx1 = music[0] + 60, music[2] - 60
    fill_rows(face, cx0, music[1] + 22, cx1, music[3] - 18)
    on = face[by0:by1, bx0:bx1]
    on_alpha = alpha[by0:by1, bx0:bx1]
    save_rgba("ayar_acik.png", on, on_alpha)
    gray = cv2.cvtColor(cv2.cvtColor(on, cv2.COLOR_BGR2GRAY), cv2.COLOR_GRAY2BGR).astype(float)
    hsv = cv2.cvtColor(on, cv2.COLOR_BGR2HSV)
    green = ((hsv[..., 0] > 35) & (hsv[..., 0] < 85) & (hsv[..., 1] > 80))[..., None]
    off = np.where(green, gray * 0.95 + 10, on.astype(float))
    save_rgba("ayar_kapali.png", np.clip(off, 0, 255).astype(np.uint8), on_alpha)
    for box in buttons[:2]:
        alpha[box[1] - pady:box[3] + pady, box[0] - padx:box[2] + padx] = 0.0

    # Kare hizi yazisi silinir (canli)
    fps = buttons[2]
    fill_rows(img, fps[0] + 60, fps[1] + 22, fps[2] - 60, fps[3] - 18)
    save_rgba("tasarim_ayarlar.png", img, alpha)
    return buttons, (bx0, by0, bx1, by1)


if __name__ == "__main__":
    profile()
    settings()
    print("ok")
