"""
Sonbahar ekran tasarimlari: kullanicinin verdigi resimlerden oyun gorsellerini hazirlar.
Kaynaklar Tools/MenuArt/kaynak/*_tasarim.jpg (2000x1116), ciktilar Assets/GameArt/MenuUI/.

  Acilir pencereler (profil, ayarlar): resim ana menunun ustunde durur.
    - cerceve (kutuk baslik, dal kenarlar, yapraklar) resimden ayrilir; disi saydam, arkada
      gercek ana menu gorunur.
    - pencerenin mor ici yari saydam duz mor olur (arkadaki karakter hafifce gorunur);
      ustundeki dal / yaprak / yazilar kalir.
    - degisen yazilar (isim, seviye, sayilar, AÇIK/KAPALI, FPS, surum) silinir; oyun canli yazar.

  Seviye yolu: arka plan (kartlar, seviye yazilari ve XP dolumu silinmis), ustte cerceve
    (dal kenarlar, kutuk, yapraklar; kartlar altindan kayar) ve dort kart resmi:
    alinan (gri), simdiki (krem), siradaki (altin yaprakli), gelecek (ahsap). Kart yazilari,
    para ikonu ve tik silinir; oyun yazar. Yesil tik ayri resim.

  Magaza / Karakter: arka plan (kartlar, karakter, para, isim yazilari silinmis), kart
    resimleri (yazi ve ikon silinmis), secili kart (altin yaprakli), tikler, sekme tahtasi ve
    islem butonu (secili: resimdeki, sec: yesil, satin al: altin).

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


def smooth_fill(img, mask, scale=0.25, blur=4):
    """Buyuk alanlari yumusak renkle doldurur (kucuk olcekte inpaint)."""
    small = cv2.resize(img, None, fx=scale, fy=scale, interpolation=cv2.INTER_AREA)
    small_mask = cv2.resize(mask, (small.shape[1], small.shape[0]), interpolation=cv2.INTER_NEAREST)
    filled = cv2.inpaint(small, small_mask, 9, cv2.INPAINT_TELEA)
    filled = cv2.GaussianBlur(cv2.resize(filled, (img.shape[1], img.shape[0]), interpolation=cv2.INTER_CUBIC),
                              (0, 0), blur)
    out = img.copy()
    out[mask > 0] = filled[mask > 0]
    return out


def is_purple(img):
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV).astype(int)
    return (hsv[..., 0] > 118) & (hsv[..., 0] < 152) & (hsv[..., 1] > 60) & (hsv[..., 2] < 160)


ROAD_CARDS = [(-229, 85), (110, 424), (449, 763), (788, 1102), (1128, 1442), (1467, 1782), (1806, 2120)]
ROAD_TOP, ROAD_BOTTOM, ROAD_MARGIN = 388, 998, 12
ROAD_VIEW = (60, 350, 1940, 1036)  # kartlarin kaydigi alan


def road_card_sprite(img, x0, x1, name, check=False, patch_from=None):
    m = ROAD_MARGIN
    cx0, cy0, cx1, cy1 = x0 - m, ROAD_TOP - m, x1 + m, ROAD_BOTTOM + m
    card = img[cy0:cy1, cx0:cx1].copy()
    alpha = (~is_purple(card)).astype(float)
    alpha = cv2.morphologyEx(alpha, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    h, w = card.shape[:2]
    hsv = cv2.cvtColor(card, cv2.COLOR_BGR2HSV).astype(int)
    erase = np.zeros((h, w), np.uint8)
    inner = np.zeros((h, w), bool)
    inner[m + 20:h - m - 30, m + 18:w - m - 18] = True
    dark = (hsv[..., 2] < 120) & inner
    text_rows = np.zeros((h, w), bool)
    text_rows[m + 25:m + 150] = True     # SEVIYE / rutbe
    text_rows[m + 410:m + 590] = True    # odul
    erase[dark & text_rows] = 1
    green = (hsv[..., 0] > 40) & (hsv[..., 0] < 85) & (hsv[..., 1] > 90) & (hsv[..., 2] > 120)
    erase[(green & text_rows)] = 1
    cv2.circle(erase, (w // 2, m + 277), 84, 1, -1)   # para / yildiz
    erase = cv2.dilate(erase, np.ones((7, 7), np.uint8))
    erase[~inner] = 0
    if check:  # yesil tik
        tick = np.zeros((h, w), np.uint8)
        area = hsv[m + 4:m + 84, w - m - 80:w - m - 2]
        tick[m + 4:m + 84, w - m - 80:w - m - 2] = ((area[..., 0] > 38) & (area[..., 0] < 90) & (area[..., 1] > 50))
        erase |= cv2.dilate(tick, np.ones((9, 9), np.uint8))
    card = cv2.inpaint(card, erase * 255, 9, cv2.INPAINT_TELEA)
    if patch_from is not None:
        # cercevenin buyuk yapraklari bu kartin sol alt kosesini kapatiyordu: komsu kartin
        # ayni kosesi bu kartin rengine boyanip konur.
        px0, py0 = 0, h - 130
        donor = patch_from[py0:, px0:px0 + 130].astype(float)
        dh = cv2.cvtColor(patch_from[py0:, px0:px0 + 130], cv2.COLOR_BGR2HSV).astype(int)
        surface = (dh[..., 2] > 170) & (dh[..., 1] < 90)
        own = np.median(card[h - 260:h - 140, m + 40:m + 140].reshape(-1, 3), axis=0)
        cream = np.median(donor[surface], axis=0)
        donor[surface] = own + (donor[surface] - cream) * 0.5
        card[py0:, px0:px0 + 130] = np.clip(donor, 0, 255).astype(np.uint8)
        alpha[py0:, px0:px0 + 130] = (~is_purple(patch_from[py0:, px0:px0 + 130])).astype(float)
    # komsu kartin bosluga tasan yaprak parcalari alinmaz
    alpha[:, :m - 3] = 0.0
    alpha[:, w - m + 3:] = 0.0
    save_rgba(name, card, cv2.GaussianBlur(alpha, (0, 0), 0.8))
    return card


def level_road():
    img = load("seviyeyolu_tasarim.jpg")
    current = road_card_sprite(img, 449, 763, "yol_kart_simdiki.png", check=True)
    road_card_sprite(img, 110, 424, "yol_kart_alinan.png", check=True, patch_from=current)
    check = img[400:456, 690:762].copy()
    road_card_sprite(img, 788, 1102, "yol_kart_siradaki.png")
    road_card_sprite(img, 1128, 1442, "yol_kart_gelecek.png")
    hsv = cv2.cvtColor(check, cv2.COLOR_BGR2HSV).astype(int)
    check_alpha = ((hsv[..., 0] > 40) & (hsv[..., 0] < 85) & (hsv[..., 1] > 80)).astype(float)
    save_rgba("yol_tik.png", check, cv2.GaussianBlur(check_alpha, (0, 0), 0.7))

    # Kartlarin yeri mor zeminle doldurulur.
    cards = np.zeros((H, W), np.uint8)
    for x0, x1 in ROAD_CARDS:
        cards[ROAD_TOP - 30:ROAD_BOTTOM + 14, max(0, x0 - 24):min(W, x1 + 26)] = 1
    view = np.zeros((H, W), np.uint8)
    vx0, vy0, vx1, vy1 = ROAD_VIEW
    view[vy0:vy1, vx0:vx1] = 1
    leaves = np.zeros((H, W), bool)  # sol alttaki buyuk yaprak kumesi kartin onunde kalir
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV).astype(int)
    box = hsv[915:1080, 20:205]
    leaves[915:1080, 20:205] = (box[..., 0] < 35) & (box[..., 1] > 90) & (box[..., 2] > 120)
    leaves = cv2.dilate(leaves.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
    hole = ((cards > 0) | is_purple(img)) & (view > 0) & ~leaves
    hole = cv2.morphologyEx(hole.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))

    background = smooth_fill(img, (((cards > 0) | ~is_purple(img)) & (view > 0) & ~leaves).astype(np.uint8) * 255)

    # Seviye / rutbe yazilari (orman uzerinde) ve XP dolumu + yazisi silinir.
    words = np.zeros((H, W), np.uint8)
    words[150:285, 50:360] = 1
    background = smooth_fill(background, words * 255, scale=0.5, blur=2)
    track_color = np.median(background[205:222, 700:1000].reshape(-1, 3), axis=0)
    background[181:221, 519:1307] = track_color.astype(np.uint8)
    cv2.imwrite(os.path.join(OUT, "seviyeyolu_arkaplan.png"), background)

    frame_alpha = 1.0 - cv2.GaussianBlur(hole.astype(float), (0, 0), 1.0)
    frame_alpha[:300] = 0.0  # ust kisim (baslik, XP) yalniz arka planda
    frame_alpha[:360, :1400] = 0.0
    save_rgba("seviyeyolu_cerceve.png", background, frame_alpha)

def erase_text(img, rect, dark=True, threshold=125, pad=6, radius=7):
    """Dikdortgendeki koyu (ya da acik) yaziyi silip komsulardan tamamlar."""
    x0, y0, x1, y1 = rect
    v = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)[..., 2]
    mask = np.zeros(img.shape[:2], np.uint8)
    region = v[y0:y1, x0:x1]
    mask[y0:y1, x0:x1] = (region < threshold) if dark else (region > threshold)
    mask = cv2.dilate(mask, np.ones((pad, pad), np.uint8))
    return cv2.inpaint(img, mask * 255, radius, cv2.INPAINT_TELEA)


def erase_plank_text(img, rect, pad=9):
    """Tahta / dugme uzerindeki acik yaziyi (ve koyu kenarini) satir satir doldurarak siler."""
    x0, y0, x1, y1 = rect
    hsv = cv2.cvtColor(img[y0:y1, x0:x1], cv2.COLOR_BGR2HSV).astype(int)
    light = ((hsv[..., 1] < 95) & (hsv[..., 2] > 150)).astype(np.uint8)
    light = cv2.dilate(light, np.ones((pad, pad), np.uint8))
    ys, xs = np.nonzero(light)
    out = img.copy()
    if len(xs):
        fill_rows(out, x0 + xs.min(), y0 + ys.min(), x0 + xs.max() + 1, y0 + ys.max() + 1, edge=5)
    return out


def fill_person(img, rect):
    """Karakterin yeri: cevreden yumusak doldurma (oyunda 3D karakter ustunde durur)."""
    x0, y0, x1, y1 = rect
    mask = np.zeros((H, W), np.uint8)
    cv2.rectangle(mask, (x0, y0), (x1, y1), 1, -1)
    filled = cv2.inpaint(img, mask * 255, 25, cv2.INPAINT_NS)
    soft = cv2.GaussianBlur(filled, (0, 0), 14)
    inner = cv2.GaussianBlur(mask.astype(float), (0, 0), 18)[..., None]
    edge = cv2.GaussianBlur(mask.astype(float), (0, 0), 4)[..., None]
    out = img * (1 - edge) + (filled * (1 - inner) + soft * inner) * edge
    return out.astype(np.uint8)


def leaf_alpha(img):
    """Doygun yaprak / altin renkleri (turuncu, sari, kirmizi, yesil)."""
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV).astype(int)
    warm = ((hsv[..., 0] < 32) | (hsv[..., 0] > 170)) & (hsv[..., 1] > 110) & (hsv[..., 2] > 100)
    green = (hsv[..., 0] > 30) & (hsv[..., 0] < 85) & (hsv[..., 1] > 90) & (hsv[..., 2] > 70)
    leaves = (warm | green).astype(np.uint8)
    leaves = cv2.morphologyEx(leaves, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
    return cv2.dilate(leaves, np.ones((5, 5), np.uint8)).astype(float)


def recolor(img, hue_shift, sat_gain, val_gain, select):
    """select maskesindeki pikselleri yeni renge cevirir (buton cesitleri)."""
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV).astype(float)
    shifted = hsv.copy()
    shifted[..., 0] = (shifted[..., 0] + hue_shift) % 180
    shifted[..., 1] = np.clip(shifted[..., 1] * sat_gain, 0, 255)
    shifted[..., 2] = np.clip(shifted[..., 2] * val_gain, 0, 255)
    out = cv2.cvtColor(shifted.astype(np.uint8), cv2.COLOR_HSV2BGR)
    return np.where(select[..., None], out, img)


def colorize(img, color, select, strength=0.85):
    """Gri yuzeyi verilen renge boyar (parlaklik korunur)."""
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY).astype(float)[..., None] / 255.0
    tinted = np.clip(np.array(color, float) * (0.35 + gray * 0.95), 0, 255)
    mixed = img.astype(float) * (1 - strength) + tinted * strength
    return np.where(select[..., None], mixed, img.astype(float)).astype(np.uint8)


def button_variants(prefix, plank, alpha, face_select):
    """Islem butonu: resimdeki (kapali) + yesil (sec) + altin (satin al)."""
    save_rgba(f"{prefix}_kapali.png", plank, alpha)
    save_rgba(f"{prefix}_sec.png", colorize(plank, (60, 190, 95), face_select), alpha)
    save_rgba(f"{prefix}_al.png", colorize(plank, (40, 185, 245), face_select), alpha)


SHOP_CARD = (487, 197, 878, 655)          # normal kart (Rahat Durus)
SHOP_PREVIEW = (52, 180, 490, 680)        # altin yaprakli secili kart (Bekleme)
SHOP_VIEW = (60, 195, 1295, 1065)


def shop():
    img = load("magaza_tasarim.jpg")
    x0, y0, x1, y1 = SHOP_CARD

    def clean_card(src, ox, oy):
        """Kart icindeki ad, tik, yildiz, not ve tahta yazisi silinir (ox, oy: kartin sol ustu)."""
        out = src.copy()
        out = erase_text(out, (ox + 20, oy + 38, ox + 380, oy + 98), threshold=120)    # ad + tik
        tick = np.zeros(out.shape[:2], np.uint8)
        tick[oy + 26:oy + 80, ox + 312:ox + 376] = 1
        out = cv2.inpaint(out, tick * 255, 7, cv2.INPAINT_TELEA)
        out = erase_text(out, (ox + 40, oy + 292, ox + 350, oy + 340), threshold=120)  # not
        star = np.zeros(out.shape[:2], np.uint8)
        cv2.circle(star, (ox + 196, oy + 196), 100, 1, -1)
        out = cv2.inpaint(out, star * 255, 11, cv2.INPAINT_TELEA)
        # tahta yazisi (acik renk) tahtanin rengiyle
        out = erase_plank_text(out, (ox + 60, oy + 356, ox + 340, oy + 432))
        return out

    clean = clean_card(img, x0, y0)
    card = clean[y0:y1, x0:x1]
    save_rgba("magaza_kart.png", card, np.ones(card.shape[:2]))

    # Secili kart: Bekleme (ayni kart + altin yaprakli cerceve), disari tasan yerde yalniz yapraklar.
    px0, py0, px1, py1 = SHOP_PREVIEW
    bx0, by0 = 97, 197  # Bekleme kartinin normal kart esdegeri sol ustu
    preview_src = clean_card(img, bx0, by0)
    preview = preview_src[py0:py1, px0:px1]
    alpha = leaf_alpha(preview)
    alpha[by0 - py0:by0 - py0 + (y1 - y0), bx0 - px0:bx0 - px0 + (x1 - x0)] = 1.0
    save_rgba("magaza_kart_secili.png", preview, cv2.GaussianBlur(alpha, (0, 0), 1.0))

    tick = img[226:272, 803:852].copy()
    hsv = cv2.cvtColor(tick, cv2.COLOR_BGR2HSV).astype(int)
    save_rgba("magaza_tik.png", tick, cv2.GaussianBlur(((hsv[..., 2] < 150) & (hsv[..., 1] > 60)).astype(float), (0, 0), 0.7))

    # Islem butonu (kahverengi tahta, sarmasikli); yesil / altin cesitleri gri yuzden.
    bx0, by0, bx1, by1 = 1395, 925, 1905, 1092
    plank = erase_plank_text(img, (1500, 955, 1810, 1055))[by0:by1, bx0:bx1]
    hsv = cv2.cvtColor(plank, cv2.COLOR_BGR2HSV).astype(int)
    wood = (hsv[..., 0] >= 3) & (hsv[..., 0] <= 28) & (hsv[..., 1] > 50) & (hsv[..., 2] > 50)
    inner = np.zeros(wood.shape, bool)
    inner[22:150, 55:455] = True
    button_variants("magaza_buton", plank, np.ones(plank.shape[:2]), wood & inner)

    # Arka plan: kartlar, para, karakter ve isim silinir.
    background = img.copy()
    vx0, vy0, vx1, vy1 = SHOP_VIEW
    panel_color = np.median(img[1062:1072, 300:900].reshape(-1, 3), axis=0)
    background[vy0:vy1, vx0:vx1] = panel_color.astype(np.uint8)
    edge = np.zeros((H, W), np.uint8)
    edge[vy0 - 6:vy1 + 6, vx0 - 6:vx1 + 6] = 1
    edge[vy0 + 6:vy1 - 6, vx0 + 6:vx1 - 6] = 0
    background = cv2.inpaint(background, edge * 255, 5, cv2.INPAINT_TELEA)
    fill_rows(background, 1552, 50, 1750, 102, edge=5)  # para yazisi
    background = fill_person(background, (1478, 240, 1830, 815))
    background = erase_text(background, (1530, 890, 1785, 945), dark=False, threshold=190, pad=9, radius=9)
    cv2.imwrite(os.path.join(OUT, "magaza_arkaplan.png"), background)


CHAR_CARD = (452, 190, 828, 350)
CHAR_VIEW = (70, 190, 830, 1068)


def character():
    img = load("karakter_tasarim.jpg")
    x0, y0, x1, y1 = CHAR_CARD
    card = img.copy()
    card = erase_text(card, (x0 + 20, y0 + 35, x0 + 365, y0 + 140), threshold=120, pad=7, radius=9)
    star = np.zeros(card.shape[:2], np.uint8)
    cv2.circle(star, (x0 + 66, y0 + 78), 42, 1, -1)
    card = cv2.inpaint(card, star * 255, 9, cv2.INPAINT_TELEA)
    card = card[y0:y1, x0:x1]
    save_rgba("karakter_kart.png", card, np.ones(card.shape[:2]))

    tick = img[218:260, 384:432].copy()
    hsv = cv2.cvtColor(tick, cv2.COLOR_BGR2HSV).astype(int)
    save_rgba("karakter_tik.png", tick, cv2.GaussianBlur(((hsv[..., 0] > 40) & (hsv[..., 0] < 90) & (hsv[..., 1] > 70)).astype(float), (0, 0), 0.7))

    # Sekme tahtasi (turuncu; kapali sekme oyunda koyulastirilir), yazisi silinir.
    tab = erase_plank_text(img, (548, 34, 844, 84), pad=5)[22:92, 534:852]
    save_rgba("karakter_sekme.png", tab, np.ones(tab.shape[:2]))

    # Islem butonu (gri tahta, sarmasikli): kapali; yesil / altin cesitleri.
    bx0, by0, bx1, by1 = 1462, 928, 1960, 1092
    plank = erase_plank_text(img, (1580, 970, 1840, 1050), pad=7)[by0:by1, bx0:bx1]
    hsv = cv2.cvtColor(plank, cv2.COLOR_BGR2HSV).astype(int)
    face = (hsv[..., 1] < 45) & (hsv[..., 2] > 90)
    alpha = np.ones(plank.shape[:2])
    button_variants("karakter_buton", plank, alpha, face)

    # Arka plan
    background = img.copy()
    # kartlarin yeri: hemen altindaki bos tahtadan kopya (damarlar dikey)
    band = img[360:526, 72:832].astype(float)
    background[188:354, 72:832] = band.astype(np.uint8)
    seam = np.linspace(0, 1, 14)[:, None, None]
    background[354:368, 72:832] = (band[-14:] * (1 - seam) + img[354:368, 72:832] * seam).astype(np.uint8)
    background = erase_text(background, (55, 112, 520, 172), threshold=120, pad=7)     # alt baslik
    background = erase_text(background, (1555, 40, 1748, 100), dark=False, threshold=200, pad=7)  # para
    background = fill_person(background, (1138, 150, 1585, 892))
    background = erase_text(background, (1262, 905, 1460, 965), threshold=120, pad=9, radius=9)  # isim
    cv2.imwrite(os.path.join(OUT, "karakter_arkaplan.png"), background)

    # Panel cercevesi ustte: alttaki yapraklar kartlarin onunde kalir.
    vx0, vy0, vx1, vy1 = CHAR_VIEW
    frame_alpha = np.ones((H, W))
    inside = np.zeros((H, W), bool)
    inside[vy0:vy1, vx0:vx1] = True
    frame_alpha[inside] = leaf_alpha(background)[inside]
    outside_panel = np.ones((H, W), bool)
    outside_panel[150:1116, 0:900] = False
    frame_alpha[outside_panel] = 0.0
    save_rgba("karakter_cerceve.png", background, cv2.GaussianBlur(frame_alpha, (0, 0), 1.0))


if __name__ == "__main__":
    profile()
    settings()
    level_road()
    shop()
    character()
    print("ok")
