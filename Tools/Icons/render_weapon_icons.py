"""
Silah ikonlarini Unity mesh asset'lerinden (Assets/GameArt/Meshes/Loot/*.asset) ceker.

Unity'ye ihtiyac duymaz: mesh YAML'indaki ham kose verisini (konum, normal, renk)
okuyup yandan ortografik olarak cizer, yumusak isik ve koyu bir dis cizgi ekler.
Cikti: Assets/GameArt/HudIcons/Weapons/weapon_<id>.png (seffaf arka plan).

Kullanim:  python Tools/Icons/render_weapon_icons.py
Yeni silah eklersen asagidaki WEAPONS listesine (silah kimligi, mesh adi) ekle.
"""
import os
import re
import struct
import sys

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
MESH_DIR = os.path.join(ROOT, 'Assets', 'GameArt', 'Meshes', 'Loot')
OUT_DIR = os.path.join(ROOT, 'Assets', 'GameArt', 'HudIcons', 'Weapons')

# (silah kimligi = PlayerShooting dizisindeki sira, mesh dosyasi)
WEAPONS = [
    (1, 'Loot_AssaultRifle'),
    (2, 'Loot_LegendaryRifle'),
    (3, 'Loot_Pistol'),
    (4, 'Loot_Smg'),
    (5, 'Loot_Shotgun'),
    (6, 'Loot_Sniper'),
]

SIZE = 256            # cikti boyutu
SUPERSAMPLE = 4       # kenar yumusatma
PADDING = 0.10        # kenar boslugu (oran)
OUTLINE_PX = 7        # dis cizgi kalinligi (cikti pikseli, supersample oncesi)
OUTLINE_COLOR = (34, 28, 46)
LIGHT = np.array([0.35, 0.85, 0.4])  # isik yonu (x derinlik, y yukari, z sag)
AMBIENT = 0.62


def read_mesh(path):
    text = open(path, encoding='utf-8').read()
    vertex_count = int(re.search(r'm_VertexCount: (\d+)', text).group(1))
    index_hex = re.search(r'm_IndexBuffer: ([0-9a-f]*)', text).group(1)
    index_format = int(re.search(r'm_IndexFormat: (\d+)', text).group(1))
    data_hex = re.search(r'_typelessdata: ([0-9a-f]*)', text).group(1)

    channels = re.findall(
        r'- stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)', text)
    # Sira: 0 konum, 1 normal, 2 tanjant, 3 renk, ...
    position, normal, tangent, color = [tuple(map(int, c)) for c in channels[:4]]
    for ch in (position, normal, color):
        assert ch[2] == 0, 'sadece float32 kose verisi destekleniyor'

    stride = 0
    for stream, offset, fmt, dim in map(lambda c: tuple(map(int, c)), channels):
        if dim and stream == 0:
            stride = max(stride, offset + dim * 4)

    raw = bytes.fromhex(data_hex)
    assert len(raw) >= stride * vertex_count, (path, len(raw), stride, vertex_count)

    def channel(ch):
        _, offset, _, dim = ch
        out = np.zeros((vertex_count, dim), dtype=np.float32)
        for i in range(vertex_count):
            start = i * stride + offset
            out[i] = struct.unpack_from('<%df' % dim, raw, start)
        return out

    pos = channel(position)
    nrm = channel(normal)
    col = channel(color) if color[3] else np.ones((vertex_count, 4), np.float32)

    ib = bytes.fromhex(index_hex)
    fmt = '<%dH' % (len(ib) // 2) if index_format == 0 else '<%dI' % (len(ib) // 4)
    indices = np.array(struct.unpack(fmt, ib), dtype=np.int64).reshape(-1, 3)
    return pos, nrm, col, indices


def render(pos, nrm, col, tris):
    size = SIZE * SUPERSAMPLE
    # Yandan bakis: ekran x = mesh z (namlu saga), ekran y = mesh y, derinlik = mesh x.
    sx, sy, depth = pos[:, 2], pos[:, 1], pos[:, 0]
    min_x, max_x, min_y, max_y = sx.min(), sx.max(), sy.min(), sy.max()
    span = max(max_x - min_x, max_y - min_y)
    scale = size * (1 - 2 * PADDING) / span
    cx, cy = (min_x + max_x) / 2, (min_y + max_y) / 2
    px = (sx - cx) * scale + size / 2
    py = size / 2 - (sy - cy) * scale

    light = LIGHT / np.linalg.norm(LIGHT)
    lit = AMBIENT + (1 - AMBIENT) * np.clip(nrm[:, [0, 1, 2]] @ np.array([light[0], light[1], light[2]]), 0, 1)
    rgb = np.clip(col[:, :3] * lit[:, None], 0, 1)

    image = np.zeros((size, size, 3), np.float32)
    alpha = np.zeros((size, size), np.float32)
    zbuf = np.full((size, size), -np.inf, np.float32)

    for a, b, c in tris:
        x0, y0, x1, y1, x2, y2 = px[a], py[a], px[b], py[b], px[c], py[c]
        area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
        if abs(area) < 1e-9:
            continue
        lo_x = max(int(np.floor(min(x0, x1, x2))), 0)
        hi_x = min(int(np.ceil(max(x0, x1, x2))), size - 1)
        lo_y = max(int(np.floor(min(y0, y1, y2))), 0)
        hi_y = min(int(np.ceil(max(y0, y1, y2))), size - 1)
        if lo_x > hi_x or lo_y > hi_y:
            continue
        xs, ys = np.meshgrid(np.arange(lo_x, hi_x + 1) + 0.5, np.arange(lo_y, hi_y + 1) + 0.5)
        w0 = ((x1 - xs) * (y2 - ys) - (x2 - xs) * (y1 - ys)) / area
        w1 = ((x2 - xs) * (y0 - ys) - (x0 - xs) * (y2 - ys)) / area
        w2 = 1 - w0 - w1
        inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        if not inside.any():
            continue
        z = w0 * depth[a] + w1 * depth[b] + w2 * depth[c]
        region = zbuf[lo_y:hi_y + 1, lo_x:hi_x + 1]
        closer = inside & (z > region)
        if not closer.any():
            continue
        region[closer] = z[closer]
        color = (w0[..., None] * rgb[a] + w1[..., None] * rgb[b] + w2[..., None] * rgb[c])
        image[lo_y:hi_y + 1, lo_x:hi_x + 1][closer] = color[closer]
        alpha[lo_y:hi_y + 1, lo_x:hi_x + 1][closer] = 1

    return image, alpha


def compose(image, alpha):
    size = image.shape[0]
    body = Image.fromarray((np.dstack([image, alpha]) * 255).astype(np.uint8), 'RGBA')

    # Dis cizgi: siluetin genisletilmis hali, govdenin altina.
    mask = Image.fromarray((alpha * 255).astype(np.uint8), 'L')
    grow = OUTLINE_PX * SUPERSAMPLE
    outline_mask = mask.filter(ImageFilter.MaxFilter(grow * 2 + 1)) if grow < 50 else mask
    outline = Image.new('RGBA', (size, size), OUTLINE_COLOR + (0,))
    outline.putalpha(outline_mask)

    canvas = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    canvas.alpha_composite(outline)
    canvas.alpha_composite(body)
    return canvas.resize((SIZE, SIZE), Image.LANCZOS)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    only = set(sys.argv[1:])
    for weapon_id, mesh in WEAPONS:
        if only and mesh not in only:
            continue
        pos, nrm, col, tris = read_mesh(os.path.join(MESH_DIR, mesh + '.asset'))
        image, alpha = render(pos, nrm, col, tris)
        out = os.path.join(OUT_DIR, 'weapon_%d.png' % weapon_id)
        compose(image, alpha).save(out)
        print('yazildi:', os.path.relpath(out, ROOT))


if __name__ == '__main__':
    main()
