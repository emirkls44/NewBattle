"""
Yumruk ikonu: silah ikonlariyla ayni tarzda (ten rengi, yumusak golge, koyu dis cizgi).
Cikti: Assets/GameArt/HudIcons/Weapons/weapon_0.png  (0 = yumruk silah kimligi)

Kullanim:  python Tools/Icons/render_fist_icon.py
"""
import os

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'GameArt', 'HudIcons', 'Weapons', 'weapon_0.png')

SIZE = 256
SS = 4
OUTLINE_PX = 7
OUTLINE_COLOR = (34, 28, 46, 255)
SKIN = (246, 196, 148, 255)
SKIN_SHADE = (214, 152, 108, 255)
SKIN_LIGHT = (255, 222, 186, 255)
CREASE = (168, 108, 78, 255)
CUFF = (225, 72, 58, 255)
CUFF_SHADE = (178, 46, 40, 255)


def s(*values):
    return [v * SS for v in values]


def main():
    size = SIZE * SS
    body = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(body)

    # Bilek manseti (solda).
    d.rounded_rectangle(s(22, 104, 84, 196), radius=14 * SS, fill=CUFF)
    d.rounded_rectangle(s(22, 160, 84, 196), radius=14 * SS, fill=CUFF_SHADE)

    # Avuc / el sirti.
    d.rounded_rectangle(s(66, 58, 196, 204), radius=44 * SS, fill=SKIN)
    d.rounded_rectangle(s(66, 150, 196, 204), radius=40 * SS, fill=SKIN_SHADE)
    d.rounded_rectangle(s(66, 58, 196, 158), radius=44 * SS, fill=SKIN)

    # Dort parmak bogumu (sagda, ust uste).
    top, height, gap = 56, 36, 0
    for i in range(4):
        y0 = top + i * (height + gap)
        d.rounded_rectangle(s(150, y0, 232, y0 + height), radius=17 * SS, fill=SKIN)
        d.rounded_rectangle(s(150, y0 + height * 0.62, 232, y0 + height), radius=14 * SS, fill=SKIN_SHADE)
        d.rounded_rectangle(s(158, y0 + 5, 214, y0 + 13), radius=4 * SS, fill=SKIN_LIGHT)
        if i > 0:
            d.line(s(156, y0, 226, y0), fill=CREASE, width=3 * SS)

    # Basparmak (onden, parmaklarin uzerine yatik).
    d.rounded_rectangle(s(96, 150, 196, 192), radius=21 * SS, fill=SKIN)
    d.rounded_rectangle(s(96, 176, 196, 192), radius=12 * SS, fill=SKIN_SHADE)
    d.rounded_rectangle(s(108, 156, 168, 164), radius=4 * SS, fill=SKIN_LIGHT)

    # El sirtinda parlama.
    d.rounded_rectangle(s(86, 72, 140, 86), radius=7 * SS, fill=SKIN_LIGHT)

    alpha = body.split()[3]
    grow = OUTLINE_PX * SS
    outline_mask = alpha.filter(ImageFilter.MaxFilter(grow * 2 + 1))
    outline = Image.new('RGBA', (size, size), OUTLINE_COLOR[:3] + (0,))
    outline.putalpha(outline_mask)

    canvas = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    canvas.alpha_composite(outline)
    canvas.alpha_composite(body)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    canvas.resize((SIZE, SIZE), Image.LANCZOS).save(OUT)
    print('yazildi:', os.path.relpath(OUT, ROOT))


if __name__ == '__main__':
    main()
