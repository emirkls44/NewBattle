"""
Sonbahar menu tasarimlari: kullanicinin verdigi resimlerden oyun gorsellerini hazirlar.
  kaynak/sonbahar_arkaplan.jpg -> Assets/GameArt/MenuUI/bg_sonbahar.png (kenar kirpik)
  kaynak/sonbahar_tasarim.jpg  -> Assets/GameArt/MenuUI/tasarim_sonbahar.png
     - alttaki duz zemin seridi yerine orman devam eder (butonlar korunur)
     - degisen yazilar silinir (para, seviye, isim, mod, online, cift XP); oyun canli yazar.
       Tahtalarda yazinin solundaki / sagindaki temiz tahtadan satir satir doku tasinir
       (damarlar yatay oldugu icin bulaniklik olmaz); sahne uzerindeki yerlere bos arka
       plan resmi konur.
  kaynak/oyunmodu_tasarim.jpg  -> Assets/GameArt/MenuUI/tasarim_oyunmodu.png (oyun modu penceresi)

Calistir:   python Tools/MenuArt/tasarim_hazirla.py   (opencv-python gerekir)
Koordinatlar Unity tarafinda: Assets/Editor/Menu/MainMenuBuilder.AutumnMenu.cs / ModeDesign.cs.
"""

import os

import cv2
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, "Assets", "GameArt", "MenuUI")
LEFT, RIGHT = 16, 1900  # resimlerin kenarlarindaki koyu cerceve disarida kalsin
BAND_TOP = 912          # ana menu resminde alttaki duz zemin seridinin basladigi satir

# Tahta uzerindeki yazilar (satir satir doku tasima)
PLANK_TEXT = {
    "para": (1572, 62, 1668, 116),
    "seviye": (182, 272, 344, 372),
    "xp_yazisi": (182, 372, 300, 406),  # sagdaki sarmasiktan ornek alinmasin
    "isim": (800, 848, 1122, 896),
    "mod": (1562, 904, 1652, 936),
    "ciftxp": (1636, 612, 1840, 676),
}
# Sahne uzerindeki yazilar (bos arka plan resminden)
SCENE_TEXT = {
    "online": (34, 117, 664, 180),
}

rng = np.random.default_rng(3)


def plank_fill(img, x0, y0, x1, y1, edge=5):
    """Her satiri soldaki ve sagdaki temiz tahta renginden yatay olarak doldurur."""
    out = img.astype(float)
    t = np.linspace(0, 1, x1 - x0)[:, None]
    for y in range(y0, y1):
        left = out[y, max(0, x0 - edge):x0].mean(axis=0)
        right = out[y, x1:x1 + edge].mean(axis=0)
        out[y, x0:x1] = left * (1 - t) + right * t
    # cok duz kalmasin: hafif yatay damarli gren
    grain = rng.normal(0, 3.0, (y1 - y0, 1, 1)) + rng.normal(0, 1.5, (y1 - y0, x1 - x0, 1))
    out[y0:y1, x0:x1] += grain
    return np.clip(out, 0, 255).astype(np.uint8)


def feather_paste(dst, src, x0, y0, x1, y1, soft=6):
    alpha = np.zeros((y1 - y0, x1 - x0), float)
    alpha[soft:-soft, soft:-soft] = 1.0
    alpha = cv2.GaussianBlur(alpha, (0, 0), soft / 2)[..., None]
    region = dst[y0:y1, x0:x1].astype(float)
    dst[y0:y1, x0:x1] = (region * (1 - alpha) + src[y0:y1, x0:x1].astype(float) * alpha).astype(np.uint8)


def band_buttons_mask(design):
    """Alt seritteki butonlarin maskesi: zemin (yumusak renk gecisi) tahmin edilip ondan ayrilir."""
    top = BAND_TOP - 20
    band = design[top:].copy()
    rough = np.abs(band.astype(float) - cv2.medianBlur(band, 151).astype(float)).max(axis=2) > 28
    rough = cv2.dilate(rough.astype(np.uint8), np.ones((31, 31), np.uint8))
    small = cv2.resize(band, None, fx=0.25, fy=0.25, interpolation=cv2.INTER_AREA)
    small_mask = cv2.resize(rough, (small.shape[1], small.shape[0]), interpolation=cv2.INTER_NEAREST)
    background = cv2.inpaint(small, small_mask * 255, 15, cv2.INPAINT_TELEA)
    background = cv2.GaussianBlur(cv2.resize(background, (band.shape[1], band.shape[0])), (0, 0), 6)

    diff = np.abs(band.astype(float) - background.astype(float)).max(axis=2)
    mask = (diff > 20).astype(np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
    # delikleri doldur (butonun ici)
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    filled = np.zeros_like(mask)
    for contour in contours:
        if cv2.contourArea(contour) > 400:
            cv2.drawContours(filled, [contour], -1, 1, thickness=-1)
    filled = cv2.dilate(filled, np.ones((5, 5), np.uint8))
    soft = cv2.GaussianBlur(filled.astype(float), (0, 0), 2.0)
    full = np.zeros(design.shape[:2], float)
    full[top:] = soft
    full[:BAND_TOP] = 1.0  # serit ustu: tasarim aynen
    return full


def extend_forest(design, background):
    """Alt serit: arka plan sahnesinin alt kismi aynalanarak orman asagi devam eder."""
    height = design.shape[0] - BAND_TOP
    scene = background[BAND_TOP - height:BAND_TOP][::-1]
    shade = np.linspace(1.0, 0.86, height)[:, None, None]
    ground = (scene.astype(float) * shade).astype(np.uint8)
    out = design.copy()
    out[BAND_TOP:] = ground
    # dikis yeri yumusak: tasarimin son satirlari ile karisir
    blend = 24
    for i in range(blend):
        a = (i + 1) / (blend + 1)
        y = BAND_TOP + i
        out[y] = (design[BAND_TOP - 1 - i].astype(float) * (1 - a) + ground[i].astype(float) * a).astype(np.uint8)
    return out


if __name__ == "__main__":
    background_src = cv2.imread(os.path.join(HERE, "kaynak", "sonbahar_arkaplan.jpg"))
    background = cv2.resize(background_src[:, LEFT:RIGHT], (1920, 1080), interpolation=cv2.INTER_AREA)
    cv2.imwrite(os.path.join(OUT, "bg_sonbahar.png"), background)

    design = cv2.imread(os.path.join(HERE, "kaynak", "sonbahar_tasarim.jpg"))
    clean = cv2.resize(background_src, (design.shape[1], design.shape[0]))

    # Alt serit: orman devam eder, butonlar uzerine geri konur.
    keep = band_buttons_mask(design)[..., None]
    forest = extend_forest(design, clean)
    design = (forest.astype(float) * (1 - keep) + design.astype(float) * keep).astype(np.uint8)

    for x0, y0, x1, y1 in PLANK_TEXT.values():
        design = plank_fill(design, x0, y0, x1, y1)
    for x0, y0, x1, y1 in SCENE_TEXT.values():
        feather_paste(design, clean, x0, y0, x1, y1, soft=3)
    # Online cubugunun logoya degen ust kenari: arka plandan daha gri (renksiz) ama ayni
    # parlaklikta pikseller temizlenir; logonun turuncu harfleri ve koyu kenari kalir.
    strip = design[100:128, 34:664].astype(float)
    base = clean[100:128, 34:664].astype(float)
    chroma = strip.max(axis=2) - strip.min(axis=2)
    base_chroma = base.max(axis=2) - base.min(axis=2)
    same_light = np.abs(strip.mean(axis=2) - base.mean(axis=2)) < 35
    alpha = (np.clip((base_chroma - chroma) / 20.0, 0, 1) * same_light)[..., None]
    design[100:128, 34:664] = (strip * (1 - alpha) + base * alpha).astype(np.uint8)
    # Arka planin farkli oldugu yerde (yaprak ustu) kalan ince gri parcalar: komsu
    # piksellerden tamamlanir (birkac piksel yukseklik, bulaniklik gorunmez).
    strip = design[100:128, 34:664].astype(int)
    leftover = ((strip.max(axis=2) - strip.min(axis=2) < 50) & (strip.mean(axis=2) > 120)).astype(np.uint8)
    leftover = cv2.dilate(leftover, np.ones((3, 3), np.uint8))
    design[100:128, 34:664] = cv2.inpaint(design[100:128, 34:664], leftover * 255, 3, cv2.INPAINT_TELEA)

    cv2.imwrite(os.path.join(OUT, "tasarim_sonbahar.png"), design[:, LEFT:RIGHT])

    # Oyun modu penceresi (tam ekran, 2000x1116): sabit tik bos kutuyla degisir (secim canli);
    # isim, para, cift XP ve ipucu yazilari silinir (oyun yazar).
    mode = cv2.imread(os.path.join(HERE, "kaynak", "oyunmodu_tasarim.jpg"))
    empty = mode[466:522, 1122:1180]
    mode[684:738, 1120:1180] = cv2.resize(empty, (60, 54), interpolation=cv2.INTER_AREA)

    mask = np.zeros(mode.shape[:2], np.uint8)
    mask[20:72, 842:1200] = 255  # isim: gokyuzu / yaprak uzerinde
    mode = cv2.inpaint(mode, mask, 9, cv2.INPAINT_TELEA)
    mode = plank_fill(mode, 1612, 58, 1732, 112)  # para tahtasi

    # Parsomen uzerindeki yazilar: ayni panelin temiz seridi yumusak kenarla kopyalanir.
    for (sx0, sy0, sx1, sy1), (x0, y0, x1, y1) in (
            ((160, 404, 580, 436), (146, 334, 590, 406)),
            ((1455, 636, 1895, 674), (1440, 434, 1905, 642))):
        patch = cv2.resize(mode[sy0:sy1, sx0:sx1], (x1 - x0, y1 - y0), interpolation=cv2.INTER_CUBIC).astype(float)
        alpha = np.zeros((y1 - y0, x1 - x0), float)
        alpha[8:-8, 8:-8] = 1.0
        alpha = cv2.GaussianBlur(alpha, (0, 0), 4)[..., None]
        region = mode[y0:y1, x0:x1].astype(float)
        mode[y0:y1, x0:x1] = (region * (1 - alpha) + patch * alpha).astype(np.uint8)
    cv2.imwrite(os.path.join(OUT, "tasarim_oyunmodu.png"), mode)
    print("ok")
