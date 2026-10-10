"""
Ana menu icin gecici (placeholder) gorselleri uretir.

Calistirma (proje klasorunden):
    python Tools/MenuArt/generate_menu_art.py

Cikti: Assets/GameArt/MenuUI/*.png
Beyaz ikonlar Unity'de Image.color ile boyanir; renkli olanlar (coin, rutbe,
arka plan) oldugu gibi kullanilir. Kendi cizimlerini ayni isimle koyarsan
Unity onlari kullanir; bu scripti tekrar calistirmazsan uzerine yazilmaz.
"""

import math
import os

from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "GameArt", "MenuUI")
SS = 4  # supersampling: 4x buyuk ciz, sonra kucult (yumusak kenar)


def canvas(size):
    s = size * SS
    mask = Image.new("L", (s, s), 0)
    return mask, ImageDraw.Draw(mask), s


def save_mask(mask, size, name, color=(255, 255, 255)):
    small = mask.resize((size, size), Image.LANCZOS)
    img = Image.new("RGBA", (size, size), color + (0,))
    img.putalpha(small)
    img.save(os.path.join(OUT, name))


def save_rgba(img, size, name):
    img.resize((size, size), Image.LANCZOS).save(os.path.join(OUT, name))


def p(s, x, y):
    """0..1 koordinati piksele cevirir."""
    return (x * s, y * s)


# ----------------------------------------------------------------------------
# Paneller ve arka planlar
# ----------------------------------------------------------------------------

def rounded_panel():
    size, r = 128, 32
    s = size * SS
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, s - 1, s - 1), r * SS, fill=255)
    save_mask(mask, size, "ui_round.png")


def circle():
    mask, d, s = canvas(128)
    d.ellipse((0, 0, s - 1, s - 1), fill=255)
    save_mask(mask, 128, "ui_circle.png")


def gold_background():
    w, h = 512, 256
    img = Image.new("RGB", (w, h))
    px = img.load()
    inner = (255, 214, 64)
    outer = (233, 158, 22)
    for y in range(h):
        for x in range(w):
            dx = (x - w * 0.5) / (w * 0.55)
            dy = (y - h * 0.45) / (h * 0.75)
            t = min(1.0, math.sqrt(dx * dx + dy * dy))
            t = t * t * (3 - 2 * t)
            px[x, y] = tuple(int(inner[i] + (outer[i] - inner[i]) * t) for i in range(3))
    img.save(os.path.join(OUT, "bg_gold.png"))


def vertical_gradient(name, top, bottom, w=8, h=256):
    img = Image.new("RGB", (w, h))
    px = img.load()
    for y in range(h):
        t = y / (h - 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3))
        for x in range(w):
            px[x, y] = c
    img.save(os.path.join(OUT, name))


def shadow_blob():
    w, h = 256, 96
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).ellipse((24, 18, w - 24, h - 18), fill=150)
    mask = mask.filter(ImageFilter.GaussianBlur(14))
    img = Image.new("RGBA", (w, h), (40, 20, 0, 0))
    img.putalpha(mask)
    img.save(os.path.join(OUT, "shadow_blob.png"))


# ----------------------------------------------------------------------------
# Beyaz ikonlar (Unity'de boyanir)
# ----------------------------------------------------------------------------

def icon_crown():
    m, d, s = canvas(128)
    pts = [p(s, .12, .30), p(s, .32, .52), p(s, .50, .22), p(s, .68, .52), p(s, .88, .30),
           p(s, .80, .72), p(s, .20, .72)]
    d.polygon(pts, fill=255)
    d.rounded_rectangle((*p(s, .20, .76), *p(s, .80, .86)), 4 * SS, fill=255)
    for x, y in ((.12, .27), (.50, .19), (.88, .27)):
        d.ellipse((*p(s, x - .06, y - .06), *p(s, x + .06, y + .06)), fill=255)
    save_mask(m, 128, "icon_crown.png")


def icon_cart():
    m, d, s = canvas(128)
    w = int(.07 * s)
    d.line([p(s, .06, .20), p(s, .20, .20), p(s, .32, .66), p(s, .80, .66)], fill=255, width=w, joint="curve")
    d.polygon([p(s, .24, .30), p(s, .92, .30), p(s, .82, .58), p(s, .32, .58)], fill=255)
    for x in (.38, .74):
        d.ellipse((*p(s, x - .07, .76), *p(s, x + .07, .90)), fill=255)
    save_mask(m, 128, "icon_cart.png")


def icon_trophy():
    m, d, s = canvas(128)
    d.pieslice((*p(s, .26, .02), *p(s, .74, .62)), 0, 180, fill=255)
    d.rectangle((*p(s, .26, .14), *p(s, .74, .33)), fill=255)
    w = int(.07 * s)
    d.arc((*p(s, .08, .16), *p(s, .40, .48)), 90, 270, fill=255, width=w)
    d.arc((*p(s, .60, .16), *p(s, .92, .48)), 270, 90, fill=255, width=w)
    d.rectangle((*p(s, .44, .58), *p(s, .56, .76)), fill=255)
    d.rounded_rectangle((*p(s, .28, .76), *p(s, .72, .90)), 4 * SS, fill=255)
    save_mask(m, 128, "icon_trophy.png")


def icon_gear():
    m, d, s = canvas(128)
    c = s / 2
    teeth = 8
    for i in range(teeth):
        a = i * math.tau / teeth
        pts = []
        for da, rr in ((-.18, .30), (-.12, .46), (.12, .46), (.18, .30)):
            pts.append((c + math.cos(a + da) * rr * s, c + math.sin(a + da) * rr * s))
        d.polygon(pts, fill=255)
    d.ellipse((c - .34 * s, c - .34 * s, c + .34 * s, c + .34 * s), fill=255)
    d.ellipse((c - .14 * s, c - .14 * s, c + .14 * s, c + .14 * s), fill=0)
    save_mask(m, 128, "icon_gear.png")


def icon_plus():
    m, d, s = canvas(128)
    d.rounded_rectangle((*p(s, .40, .12), *p(s, .60, .88)), 6 * SS, fill=255)
    d.rounded_rectangle((*p(s, .12, .40), *p(s, .88, .60)), 6 * SS, fill=255)
    save_mask(m, 128, "icon_plus.png")


def person(d, s, cx, top, scale):
    hr = .13 * scale
    d.ellipse((*p(s, cx - hr, top), *p(s, cx + hr, top + 2 * hr)), fill=255)
    bw = .26 * scale
    d.pieslice((*p(s, cx - bw, top + 2.2 * hr), *p(s, cx + bw, top + 2.2 * hr + 2 * bw)), 180, 360, fill=255)


def icon_people():
    m, d, s = canvas(128)
    person(d, s, .24, .22, .75)
    person(d, s, .76, .22, .75)
    # ortadaki kisiyi ayirmak icin bosluk
    person_gap = Image.new("L", m.size, 0)
    person(ImageDraw.Draw(person_gap), s, .50, .14, 1.0)
    grown = person_gap.filter(ImageFilter.MaxFilter(4 * SS + 1))
    m.paste(0, mask=grown)
    person(d, s, .50, .14, 1.0)
    save_mask(m, 128, "icon_people.png")


def icon_friends():
    m, d, s = canvas(128)
    person(d, s, .64, .14, 1.0)
    front = Image.new("L", m.size, 0)
    person(ImageDraw.Draw(front), s, .36, .22, 1.0)
    m.paste(0, mask=front.filter(ImageFilter.MaxFilter(5 * SS + 1)))
    person(d, s, .36, .22, 1.0)
    save_mask(m, 128, "icon_friends.png")


def icon_head():
    """Karakter/kostum butonu: maskeli kafa."""
    m, d, s = canvas(128)
    d.ellipse((*p(s, .20, .08), *p(s, .80, .74)), fill=255)
    d.pieslice((*p(s, .10, .66), *p(s, .90, 1.30)), 180, 360, fill=255)
    for x in (.36, .64):
        d.ellipse((*p(s, x - .08, .34), *p(s, x + .08, .46)), fill=0)
    save_mask(m, 128, "icon_head.png")


def icon_pencil():
    m, d, s = canvas(128)
    body = Image.new("L", m.size, 0)
    bd = ImageDraw.Draw(body)
    bd.rounded_rectangle((*p(s, .40, .04), *p(s, .60, .70)), 3 * SS, fill=255)
    bd.polygon([p(s, .40, .74), p(s, .60, .74), p(s, .50, .94)], fill=255)
    body = body.rotate(-45, resample=Image.BICUBIC, center=(s / 2, s / 2))
    m.paste(255, mask=body)
    save_mask(m, 128, "icon_pencil.png")


def icon_play():
    m, d, s = canvas(128)
    d.polygon([p(s, .28, .14), p(s, .86, .50), p(s, .28, .86)], fill=255)
    save_mask(m, 128, "icon_play.png")


def icon_lock():
    m, d, s = canvas(128)
    d.arc((*p(s, .28, .08), *p(s, .72, .56)), 180, 360, fill=255, width=int(.09 * s))
    d.rectangle((*p(s, .28, .30), *p(s, .37, .46)), fill=255)
    d.rectangle((*p(s, .63, .30), *p(s, .72, .46)), fill=255)
    d.rounded_rectangle((*p(s, .18, .44), *p(s, .82, .92)), 8 * SS, fill=255)
    d.ellipse((*p(s, .44, .58), *p(s, .56, .70)), fill=0)
    save_mask(m, 128, "icon_lock.png")


def icon_check():
    m, d, s = canvas(128)
    d.line([p(s, .14, .52), p(s, .40, .78), p(s, .88, .24)], fill=255, width=int(.14 * s), joint="curve")
    save_mask(m, 128, "icon_check.png")


def icon_close():
    m, d, s = canvas(128)
    w = int(.16 * s)
    d.line([p(s, .18, .18), p(s, .82, .82)], fill=255, width=w)
    d.line([p(s, .82, .18), p(s, .18, .82)], fill=255, width=w)
    save_mask(m, 128, "icon_close.png")


def icon_clock():
    m, d, s = canvas(128)
    d.ellipse((*p(s, .06, .06), *p(s, .94, .94)), fill=255)
    d.ellipse((*p(s, .16, .16), *p(s, .84, .84)), fill=0)
    w = int(.08 * s)
    d.line([p(s, .50, .50), p(s, .50, .26)], fill=255, width=w)
    d.line([p(s, .50, .50), p(s, .68, .60)], fill=255, width=w)
    save_mask(m, 128, "icon_clock.png")


def icon_skull():
    m, d, s = canvas(128)
    d.ellipse((*p(s, .14, .06), *p(s, .86, .70)), fill=255)
    d.rounded_rectangle((*p(s, .28, .52), *p(s, .72, .90)), 6 * SS, fill=255)
    for x in (.34, .66):
        d.ellipse((*p(s, x - .11, .32), *p(s, x + .11, .54)), fill=0)
    d.polygon([p(s, .50, .54), p(s, .44, .66), p(s, .56, .66)], fill=0)
    for x in (.40, .50, .60):
        d.rectangle((*p(s, x - .015, .76), *p(s, x + .015, .90)), fill=0)
    save_mask(m, 128, "icon_skull.png")


def icon_star():
    m, d, s = canvas(128)
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        r = .46 if i % 2 == 0 else .20
        pts.append((s / 2 + math.cos(a) * r * s, s * .53 + math.sin(a) * r * s))
    d.polygon(pts, fill=255)
    save_mask(m, 128, "icon_star.png")


def icon_gift():
    """Haberci/kostum kartinda kullanilan hediye kutusu."""
    m, d, s = canvas(128)
    d.rounded_rectangle((*p(s, .12, .40), *p(s, .88, .92)), 6 * SS, fill=255)
    d.rounded_rectangle((*p(s, .06, .28), *p(s, .94, .44)), 5 * SS, fill=255)
    d.rectangle((*p(s, .45, .28), *p(s, .55, .92)), fill=0)
    d.rectangle((*p(s, .06, .44), *p(s, .94, .48)), fill=0)
    d.ellipse((*p(s, .22, .08), *p(s, .50, .30)), outline=255, width=int(.06 * s))
    d.ellipse((*p(s, .50, .08), *p(s, .78, .30)), outline=255, width=int(.06 * s))
    save_mask(m, 128, "icon_gift.png")


# ----------------------------------------------------------------------------
# Renkli ikonlar
# ----------------------------------------------------------------------------

def coin():
    size = 128
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((*p(s, .04, .04), *p(s, .96, .96)), fill=(214, 130, 18, 255))
    d.ellipse((*p(s, .10, .08), *p(s, .90, .88)), fill=(255, 196, 46, 255))
    d.ellipse((*p(s, .20, .18), *p(s, .80, .78)), fill=(240, 160, 24, 255))
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        r = .22 if i % 2 == 0 else .10
        pts.append((s * .5 + math.cos(a) * r * s, s * .48 + math.sin(a) * r * s))
    d.polygon(pts, fill=(255, 236, 160, 255))
    save_rgba(img, size, "coin.png")


def hexagon(d, s, cx, cy, r, fill):
    pts = [(cx + math.cos(math.radians(60 * i - 90)) * r * s, cy + math.sin(math.radians(60 * i - 90)) * r * s)
           for i in range(6)]
    d.polygon(pts, fill=fill)


def rank_badge():
    size = 128
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    hexagon(d, s, s * .5, s * .5, .48, (92, 52, 22, 255))
    hexagon(d, s, s * .5, s * .5, .42, (196, 124, 62, 255))
    hexagon(d, s, s * .5, s * .5, .32, (150, 88, 40, 255))
    w = int(.07 * s)
    for y in (.40, .54):
        d.line([p(s, .32, y), p(s, .50, y + .10), p(s, .68, y)], fill=(255, 226, 170, 255), width=w, joint="curve")
    save_rgba(img, size, "rank_bronze.png")


def challenge_book():
    size = 128
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((*p(s, .18, .10), *p(s, .86, .92)), 8 * SS, fill=(30, 88, 170, 255))
    d.rounded_rectangle((*p(s, .14, .08), *p(s, .80, .88)), 8 * SS, fill=(66, 140, 230, 255))
    d.rectangle((*p(s, .14, .08), *p(s, .24, .88)), fill=(40, 100, 190, 255))
    d.polygon([p(s, .62, .02), p(s, .74, .02), p(s, .74, .30), p(s, .68, .24), p(s, .62, .30)], fill=(230, 50, 60, 255))
    q = Image.new("L", (s, s), 0)
    qd = ImageDraw.Draw(q)
    qd.arc((*p(s, .36, .26), *p(s, .64, .52)), 200, 90, fill=255, width=int(.08 * s))
    qd.line([p(s, .50, .52), p(s, .50, .62)], fill=255, width=int(.08 * s))
    qd.ellipse((*p(s, .45, .68), *p(s, .55, .78)), fill=255)
    img.paste((255, 255, 255, 255), mask=q)
    save_rgba(img, size, "challenge_book.png")


def battlepass_badge():
    size = 128
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    hexagon(d, s, s * .5, s * .5, .48, (214, 120, 16, 255))
    hexagon(d, s, s * .5, s * .5, .40, (255, 196, 46, 255))
    crown = Image.new("L", (s, s), 0)
    cd = ImageDraw.Draw(crown)
    cd.polygon([p(s, .28, .36), p(s, .40, .50), p(s, .50, .30), p(s, .60, .50), p(s, .72, .36),
                p(s, .67, .64), p(s, .33, .64)], fill=255)
    cd.rectangle((*p(s, .33, .67), *p(s, .67, .73)), fill=255)
    img.paste((255, 250, 235, 255), mask=crown)
    save_rgba(img, size, "battlepass_badge.png")


def icon_person():
    m, d, s = canvas(128)
    person(d, s, .50, .08, 1.45)
    save_mask(m, 128, "icon_person.png")


def mode_header():
    """Oyun modu penceresinin ustundeki manzara: gokyuzu, bulut, ada, ucak."""
    w, h = 640, 360
    S = 2
    img = Image.new("RGBA", (w * S, h * S))
    px = img.load()
    top, bottom = (92, 186, 246), (190, 232, 252)
    for y in range(h * S):
        t = y / (h * S - 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,)
        for x in range(w * S):
            px[x, y] = c
    d = ImageDraw.Draw(img)

    def e(x0, y0, x1, y1, color):
        d.ellipse((x0 * S, y0 * S, x1 * S, y1 * S), fill=color)

    # gunes
    e(470, 40, 560, 130, (255, 236, 150, 255))
    # bulutlar
    for cx, cy, sc in ((120, 80, 1.0), (380, 120, 0.8), (560, 190, 0.7)):
        for dx, dy, r in ((-40, 8, 30), (0, 0, 40), (42, 10, 28), (20, 18, 30)):
            e(cx + (dx - r) * sc, cy + (dy - r) * sc, cx + (dx + r) * sc, cy + (dy + r) * sc, (255, 255, 255, 235))
    # ada: kum ve yesil tepeler
    e(-120, 250, 760, 520, (247, 214, 138, 255))
    e(-60, 268, 330, 470, (120, 196, 92, 255))
    e(300, 280, 700, 480, (102, 180, 80, 255))
    e(150, 300, 480, 470, (140, 210, 104, 255))
    # agaclar
    for tx, ty in ((80, 285), (230, 300), (430, 296), (560, 300)):
        d.rectangle(((tx - 4) * S, ty * S, (tx + 4) * S, (ty + 26) * S), fill=(120, 80, 40, 255))
        e(tx - 22, ty - 26, tx + 22, ty + 10, (60, 150, 60, 255))
    # ucak
    plane = Image.new("RGBA", img.size, (0, 0, 0, 0))
    pd = ImageDraw.Draw(plane)
    cx, cy = 300 * S, 70 * S
    pd.rounded_rectangle((cx - 90 * S, cy - 14 * S, cx + 90 * S, cy + 14 * S), 14 * S, fill=(90, 110, 130, 255))
    pd.polygon([(cx - 20 * S, cy), (cx + 30 * S, cy), (cx - 10 * S, cy + 70 * S), (cx - 40 * S, cy + 70 * S)], fill=(70, 90, 110, 255))
    pd.polygon([(cx - 20 * S, cy), (cx + 30 * S, cy), (cx - 10 * S, cy - 70 * S), (cx - 40 * S, cy - 70 * S)], fill=(70, 90, 110, 255))
    pd.polygon([(cx - 90 * S, cy), (cx - 70 * S, cy), (cx - 90 * S, cy - 34 * S), (cx - 104 * S, cy - 34 * S)], fill=(70, 90, 110, 255))
    plane = plane.rotate(8, resample=Image.BICUBIC, center=(cx, cy))
    img.alpha_composite(plane)
    img.resize((w, h), Image.LANCZOS).save(os.path.join(OUT, "mode_header.png"))


def running_frames(count=8):
    """Yukleme ekrani: kosan chibi karakter silueti (beyaz), yandan."""
    size = 256
    s = size * SS

    def limb(d, start, angles, lengths, width):
        x, y = start
        heading = 0.0
        points = [(x, y)]
        for angle, length in zip(angles, lengths):
            heading += angle
            rad = math.radians(heading)
            x += math.sin(rad) * length
            y += math.cos(rad) * length
            points.append((x, y))
        w = width * SS
        for (x0, y0), (x1, y1) in zip(points, points[1:]):
            d.line([(x0 * SS, y0 * SS), (x1 * SS, y1 * SS)], fill=255, width=int(w))
        for px_, py_ in points:
            d.ellipse((px_ * SS - w / 2, py_ * SS - w / 2, px_ * SS + w / 2, py_ * SS + w / 2), fill=255)
        return points[-1]

    for i in range(count):
        t = i / count * math.tau
        bob = -5 * abs(math.sin(t))
        m = Image.new("L", (s, s), 0)
        d = ImageDraw.Draw(m)
        hip = (118, 160 + bob)
        shoulder = (136, 120 + bob)  # one egik govde

        for phase in (t + math.pi, t):  # once arkadaki bacak/kol
            swing = math.sin(phase)
            knee = 20 + 55 * max(0.0, -math.cos(phase))
            foot = limb(d, hip, (38 * swing, -knee), (30, 30), 22)
            d.ellipse(((foot[0] - 6) * SS, (foot[1] - 9) * SS, (foot[0] + 16) * SS, (foot[1] + 7) * SS), fill=255)
            limb(d, shoulder, (-50 * swing, 70), (24, 22), 15)

        # govde
        d.line([(shoulder[0] * SS, shoulder[1] * SS), (hip[0] * SS, hip[1] * SS)], fill=255, width=40 * SS)
        for cx, cy in (shoulder, hip):
            d.ellipse(((cx - 20) * SS, (cy - 20) * SS, (cx + 20) * SS, (cy + 20) * SS), fill=255)
        # buyuk kafa + sac/kask
        hx, hy = 150, 78 + bob
        d.ellipse(((hx - 40) * SS, (hy - 40) * SS, (hx + 40) * SS, (hy + 40) * SS), fill=255)
        d.rounded_rectangle(((hx - 44) * SS, (hy - 46) * SS, (hx + 30) * SS, (hy - 14) * SS), 14 * SS, fill=255)
        # silah (one dogru)
        d.rounded_rectangle(((shoulder[0] + 4) * SS, (shoulder[1] + 22) * SS,
                             (shoulder[0] + 44) * SS, (shoulder[1] + 32) * SS), 4 * SS, fill=255)

        # ayak tozu: arka ayak yere vururken
        if math.cos(t) > 0.6:
            for dx, dy, r in ((-34, 0, 7), (-48, -6, 5), (-58, 2, 4)):
                cx, cy = hip[0] + dx, 222 + dy
                d.ellipse(((cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS), fill=200)

        save_mask(m, size, f"run_{i:02d}.png")


def spinner():
    """Sol ustteki yukleme gostergesi: kalin bir yay."""
    m, d, s = canvas(128)
    d.arc((*p(s, .12, .12), *p(s, .88, .88)), 180, 290, fill=255, width=int(.13 * s))
    for a in (180, 290):
        r = math.radians(a)
        cx, cy = s / 2 + math.cos(r) * .315 * s, s / 2 + math.sin(r) * .315 * s
        w = .065 * s
        d.ellipse((cx - w, cy - w, cx + w, cy + w), fill=255)
    save_mask(m, 128, "spinner.png")


def ocean_background():
    w, h = 512, 256
    img = Image.new("RGB", (w, h))
    px_ = img.load()
    inner, outer = (36, 120, 176), (24, 92, 146)
    for y in range(h):
        for x in range(w):
            dx = (x - w * 0.35) / (w * 0.7)
            dy = (y - h * 0.5) / (h * 0.9)
            t = min(1.0, math.sqrt(dx * dx + dy * dy))
            px_[x, y] = tuple(int(inner[i] + (outer[i] - inner[i]) * t) for i in range(3))
    img.save(os.path.join(OUT, "bg_ocean.png"))


def cloud():
    w, h = 256, 160
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)
    for cx, cy, r in ((80, 95, 45), (128, 70, 55), (175, 95, 45), (128, 105, 45)):
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=235)
    mask = mask.filter(ImageFilter.GaussianBlur(10))
    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    img.putalpha(mask)
    img.save(os.path.join(OUT, "cloud.png"))


def soft_glow():
    """Buyuk geri sayim rakaminin arkasindaki yumusak parlama."""
    w = 256
    mask = Image.new("L", (w, w), 0)
    ImageDraw.Draw(mask).ellipse((40, 60, 216, 196), fill=150)
    mask = mask.filter(ImageFilter.GaussianBlur(28))
    img = Image.new("RGBA", (w, w), (255, 255, 255, 0))
    img.putalpha(mask)
    img.save(os.path.join(OUT, "glow.png"))


def drop_pin():
    """Sari inis isaretcisi: alti sivri kalkan + beyaz asagi ok."""
    size = 128
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    body = [p(s, .18, .08), p(s, .82, .08), p(s, .82, .62), p(s, .50, .94), p(s, .18, .62)]
    d.polygon(body, fill=(214, 140, 10, 255))
    inner = [p(s, .24, .12), p(s, .76, .12), p(s, .76, .58), p(s, .50, .86), p(s, .24, .58)]
    d.polygon(inner, fill=(255, 198, 26, 255))
    d.line([p(s, .32, .38), p(s, .50, .56), p(s, .68, .38)], fill=(255, 255, 255, 255), width=int(.09 * s), joint="curve")
    save_rgba(img, size, "drop_pin.png")


def ring():
    m, d, s = canvas(128)
    d.ellipse((*p(s, .04, .04), *p(s, .96, .96)), fill=255)
    d.ellipse((*p(s, .16, .16), *p(s, .84, .84)), fill=0)
    save_mask(m, 128, "ring.png")


def round_outline():
    size, r, wdt = 128, 32, 5
    s = size * SS
    mask = Image.new("L", (s, s), 0)
    d = ImageDraw.Draw(mask)
    d.rounded_rectangle((0, 0, s - 1, s - 1), r * SS, fill=255)
    d.rounded_rectangle((wdt * SS, wdt * SS, s - 1 - wdt * SS, s - 1 - wdt * SS), (r - wdt) * SS, fill=0)
    save_mask(mask, size, "ui_round_outline.png")


def compass_star():
    m, d, s = canvas(128)
    c = s / 2
    for a in range(4):
        ang = a * math.pi / 2
        tip = (c + math.cos(ang) * .46 * s, c + math.sin(ang) * .46 * s)
        l = (c + math.cos(ang + math.pi / 2) * .09 * s, c + math.sin(ang + math.pi / 2) * .09 * s)
        r = (c + math.cos(ang - math.pi / 2) * .09 * s, c + math.sin(ang - math.pi / 2) * .09 * s)
        d.polygon([tip, l, (c, c), r], fill=255)
    for a in range(4):
        ang = math.pi / 4 + a * math.pi / 2
        tip = (c + math.cos(ang) * .26 * s, c + math.sin(ang) * .26 * s)
        l = (c + math.cos(ang + math.pi / 2) * .06 * s, c + math.sin(ang + math.pi / 2) * .06 * s)
        r = (c + math.cos(ang - math.pi / 2) * .06 * s, c + math.sin(ang - math.pi / 2) * .06 * s)
        d.polygon([tip, l, (c, c), r], fill=200)
    d.ellipse((c - .3 * s, c - .3 * s, c + .3 * s, c + .3 * s), outline=255, width=int(.03 * s))
    save_mask(m, 128, "compass_star.png")


def main():
    os.makedirs(OUT, exist_ok=True)
    ocean_background()
    cloud()
    soft_glow()
    drop_pin()
    ring()
    round_outline()
    compass_star()
    running_frames()
    spinner()
    icon_person()
    mode_header()
    vertical_gradient("bg_blue.png", (64, 170, 244), (110, 200, 252))
    rounded_panel()
    circle()
    gold_background()
    vertical_gradient("bg_purple.png", (196, 70, 214), (130, 46, 190))
    vertical_gradient("bg_teal.png", (40, 200, 170), (22, 160, 136))
    shadow_blob()
    icon_crown()
    icon_cart()
    icon_trophy()
    icon_gear()
    icon_plus()
    icon_people()
    icon_friends()
    icon_head()
    icon_pencil()
    icon_play()
    icon_lock()
    icon_check()
    icon_close()
    icon_clock()
    icon_skull()
    icon_star()
    icon_gift()
    coin()
    rank_badge()
    challenge_book()
    battlepass_badge()
    print("Gorseller uretildi:", os.path.abspath(OUT))


if __name__ == "__main__":
    main()
