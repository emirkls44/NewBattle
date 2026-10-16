"""
Sonbahar ana menu: kullanicinin verdigi iki resimden oyun gorsellerini hazirlar.
  kaynak/sonbahar_arkaplan.jpg -> Assets/GameArt/MenuUI/bg_sonbahar.png (kenar kirpik)
  kaynak/sonbahar_tasarim.jpg  -> Assets/GameArt/MenuUI/tasarim_sonbahar.png
     (degisen yazilar silinir: para, seviye, isim, mod, online, cift XP; oyun bunlari canli yazar)
  kaynak/oyunmodu_tasarim.jpg  -> Assets/GameArt/MenuUI/tasarim_oyunmodu.png (oyun modu penceresi)

Calistir:   python Tools/MenuArt/tasarim_hazirla.py   (opencv-python gerekir)
Koordinatlar Unity tarafinda: Assets/Editor/Menu/MainMenuBuilder.AutumnMenu.cs (ayni piksel olcusu).
"""

import os

import cv2
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, "Assets", "GameArt", "MenuUI")
LEFT, RIGHT = 16, 1900  # resimlerin kenarlarindaki koyu cerceve disarida kalsin

ERASE = {
    "para": (1572, 62, 1668, 116),
    "seviye": (180, 274, 342, 404),
    "isim": (800, 848, 1122, 896),
    "mod": (1562, 904, 1652, 936),
    "online": (54, 108, 650, 170),
    "ciftxp": (1636, 612, 1840, 676),
}

if __name__ == "__main__":
    background = cv2.imread(os.path.join(HERE, "kaynak", "sonbahar_arkaplan.jpg"))
    background = cv2.resize(background[:, LEFT:RIGHT], (1920, 1080), interpolation=cv2.INTER_AREA)
    cv2.imwrite(os.path.join(OUT, "bg_sonbahar.png"), background)

    design = cv2.imread(os.path.join(HERE, "kaynak", "sonbahar_tasarim.jpg"))
    mask = np.zeros(design.shape[:2], np.uint8)
    for x0, y0, x1, y1 in ERASE.values():
        mask[y0:y1, x0:x1] = 255
    design = cv2.inpaint(design, mask, 9, cv2.INPAINT_TELEA)[:, LEFT:RIGHT]
    cv2.imwrite(os.path.join(OUT, "tasarim_sonbahar.png"), design)

    # Oyun modu penceresi (tam ekran, 2000x1116): sabit tik bos kutuyla degisir (secim canli);
    # isim, para, cift XP ve ipucu yazilari silinir (oyun yazar).
    mode = cv2.imread(os.path.join(HERE, "kaynak", "oyunmodu_tasarim.jpg"))
    empty = mode[466:522, 1122:1180]
    mode[684:738, 1120:1180] = cv2.resize(empty, (60, 54), interpolation=cv2.INTER_AREA)

    mask = np.zeros(mode.shape[:2], np.uint8)
    for x0, y0, x1, y1 in ((842, 20, 1200, 72), (1612, 58, 1732, 112)):
        mask[y0:y1, x0:x1] = 255
    mode = cv2.inpaint(mode, mask, 9, cv2.INPAINT_TELEA)

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
