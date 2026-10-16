"""
Ana menu: tasarim resmindeki butonlar / kartlar / logo arka plandan ayrilir, arka plan ayri olur.
  Assets/GameArt/MenuUI/tasarim_sonbahar.png (tasarim_hazirla.py ciktisi, yazilari silinmis)
      -> Assets/GameArt/MenuUI/tasarim_sonbahar_ust.png (saydam: sadece butonlar, kartlar, logo)
  kaynak/anamenu_arkaplan.jpg (kullanicinin bos orman resmi)
      -> Assets/GameArt/MenuUI/anamenu_arkaplan.png (Unity'de "ArkaPlan" nesnesi, Inspector'dan degisir)

Her parca kendi kutusunda GrabCut ile ayrilir: kutunun ici kesin parca, cevresi olasi arka plan.
Calistir:   python Tools/MenuArt/anamenu_katman.py   (opencv-python gerekir)
"""

import os

import cv2
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, "Assets", "GameArt", "MenuUI")

# Tasarim resmindeki parcalar (2000x1120 piksel): (sol, ust, sag, alt)
PARTS = {
    "para": (1404, 34, 1764, 136),
    "ayarlar": (1774, 40, 1878, 140),
    "sezon": (56, 204, 366, 422),
    "kostum": (64, 478, 194, 630),
    "oduller": (228, 472, 360, 640),
    "mucadeleler": (54, 664, 422, 864),
    "etkinlik_karti": (1560, 300, 1836, 492),
    "cift_xp": (1502, 538, 1876, 754),
    "battle_pass": (40, 930, 186, 1064),
    "magaza": (204, 930, 334, 1064),
    "liderlik": (350, 930, 482, 1064),
    "isim": (724, 836, 1196, 906),
    "karakter": (816, 926, 954, 1072),
    "arkadaslar": (972, 926, 1108, 1072),
    "etkinlikler": (1132, 914, 1480, 1078),
    "hediyeli": (1342, 906, 1488, 946),
    "oyna": (1492, 900, 1898, 1078),
    "oyuncu_cipi": (1510, 898, 1672, 942),
}
MARGIN = 34   # kutunun cevresinde aranan yaprak / kenar payi
SURE = 0.18   # kutunun kenarindan bu oran iceri kesin parca
YENI_KOSTUM = (58, 608, 202, 636)  # hediye butonunun altindaki koyu yazi
LOGO = (0, 0, 560, 140)


def logo_mask(img):
    """NEW BATTLE ve arkasindaki yapraklar: koyu kenar cizgilerinin ici (harf aralarindaki sis atilir)."""
    x0, y0, x1, y1 = LOGO
    light = cv2.cvtColor(img[y0:y1, x0:x1], cv2.COLOR_BGR2LAB)[..., 0]
    outline = cv2.morphologyEx((light < 105).astype(np.uint8), cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
    contours, _ = cv2.findContours(outline, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    full = np.zeros(img.shape[:2], np.uint8)
    cv2.drawContours(full[y0:y1, x0:x1], [c for c in contours if cv2.contourArea(c) > 300], -1, 1, thickness=-1)
    return full


def haze(img):
    """Eski arka planin acik bej sisi (parcalarin kenarinda kesin arka plan sayilir)."""
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV).astype(int)
    return (hsv[..., 1] < 100) & (hsv[..., 2] > 185)


def part_mask(img, box):
    x0, y0, x1, y1 = box
    h, w = img.shape[:2]
    rx0, ry0 = max(0, x0 - MARGIN * 2), max(0, y0 - MARGIN * 2)
    rx1, ry1 = min(w, x1 + MARGIN * 2), min(h, y1 + MARGIN * 2)
    roi = img[ry0:ry1, rx0:rx1]
    mask = np.full(roi.shape[:2], cv2.GC_BGD, np.uint8)
    mask[max(0, y0 - MARGIN - ry0):y1 + MARGIN - ry0, max(0, x0 - MARGIN - rx0):x1 + MARGIN - rx0] = cv2.GC_PR_BGD
    mask[y0 - ry0:y1 - ry0, x0 - rx0:x1 - rx0] = cv2.GC_PR_FGD
    sx, sy = int((x1 - x0) * SURE), int((y1 - y0) * SURE)
    mask[y0 + sy - ry0:y1 - sy - ry0, x0 + sx - rx0:x1 - sx - rx0] = cv2.GC_FGD
    mask[(mask != cv2.GC_FGD) & haze(roi)] = cv2.GC_BGD
    cv2.grabCut(roi, mask, None, np.zeros((1, 65)), np.zeros((1, 65)), 6, cv2.GC_INIT_WITH_MASK)
    fg = ((mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD)).astype(np.uint8)
    full = np.zeros((h, w), np.uint8)
    full[ry0:ry1, rx0:rx1] = fg
    return full


def clean(mask, min_area=400):
    """Kucuk kopuk parcalari atar, delikleri doldurur."""
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    count, labels, stats, _ = cv2.connectedComponentsWithStats(mask)
    keep = np.zeros_like(mask)
    for index in range(1, count):
        if stats[index, cv2.CC_STAT_AREA] >= min_area:
            keep[labels == index] = 1
    contours, _ = cv2.findContours(keep, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    filled = np.zeros_like(keep)
    cv2.drawContours(filled, contours, -1, 1, thickness=-1)
    return filled


if __name__ == "__main__":
    design = cv2.imread(os.path.join(OUT, "tasarim_sonbahar.png"))
    mask = np.zeros(design.shape[:2], np.uint8)
    for name, box in PARTS.items():
        mask |= part_mask(design, box)
    mask = clean(mask) | logo_mask(design)
    x0, y0, x1, y1 = YENI_KOSTUM
    text = np.zeros_like(mask)
    text[y0:y1, x0:x1] = cv2.cvtColor(design[y0:y1, x0:x1], cv2.COLOR_BGR2GRAY) < 120
    mask |= cv2.dilate(text, np.ones((3, 3), np.uint8))
    alpha = cv2.GaussianBlur(mask.astype(float), (0, 0), 0.9)
    rgba = np.dstack([design, np.clip(alpha * 255, 0, 255).astype(np.uint8)])
    cv2.imwrite(os.path.join(OUT, "tasarim_sonbahar_ust.png"), rgba)

    background = cv2.imread(os.path.join(HERE, "kaynak", "anamenu_arkaplan.jpg"))
    background = cv2.resize(background, (design.shape[1], design.shape[0]), interpolation=cv2.INTER_AREA)
    cv2.imwrite(os.path.join(OUT, "anamenu_arkaplan.png"), background)
    print("ok")
