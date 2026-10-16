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

    # Oyun modu penceresi: sabit tik bos kutuyla degisir (secim canli), cift XP ve ipucu
    # yazilari silinir (oyun yazar).
    mode = cv2.imread(os.path.join(HERE, "kaynak", "oyunmodu_tasarim.jpg"))
    empty = mode[336:394, 1118:1178]
    mode[440:500, 1116:1182] = cv2.resize(empty, (66, 60), interpolation=cv2.INTER_AREA)
    # Yazi bolgelerine ayni panelin temiz parsomen seridi yumusak kenarla kopyalanir.
    for (sx0, sy0, sx1, sy1), (x0, y0, x1, y1) in (
            ((150, 262, 580, 300), (146, 198, 584, 260)),
            ((1445, 506, 1895, 546), (1440, 304, 1900, 502))):
        patch = cv2.resize(mode[sy0:sy1, sx0:sx1], (x1 - x0, y1 - y0), interpolation=cv2.INTER_CUBIC).astype(float)
        alpha = np.zeros((y1 - y0, x1 - x0), float)
        alpha[8:-8, 8:-8] = 1.0
        alpha = cv2.GaussianBlur(alpha, (0, 0), 4)[..., None]
        region = mode[y0:y1, x0:x1].astype(float)
        mode[y0:y1, x0:x1] = (region * (1 - alpha) + patch * alpha).astype(np.uint8)
    cv2.imwrite(os.path.join(OUT, "tasarim_oyunmodu.png"), mode)
    print("ok")
