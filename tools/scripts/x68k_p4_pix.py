#!/usr/bin/env python3
"""Decode Phantasie IV (X68000) raw *.pix sprite files to PNG sheets (T6.4, s108).

Layouts read from P4.X's draw routines (reverse-engineering/x68000/phantasie4/FORMAT.md 6.9):
  file      draw      sprite (w x h)  bytes  mask     count
  fl.pix    _fpix     32 x 32         512    none     44   dungeon floor tiles
  yoko.pix  _ypix     48 x 32         960    1-bit    20   dungeon north-side walls (_ConvData 192, 20)
  tate.pix  _tpix     16 x 64         640    1-bit     6   dungeon west-side walls (_ConvData 128, 6)
  mn.pix    _mpix     32 x 32         640    1-bit     8   the party marker (_ConvData 128, 8)
  bback.pix _bkpix    48 x 40         960    none      9   combat ground, one record per terrain (_e_finit)
  bobj.pix  _obpix    48 x 48        1440    1-bit    11   combat obstacles (_e_obstacle kinds)
  fly.pix   _flpix    32 x 32         640    1-bit    15   combat missiles / effects
  icon.pix  _aicon    32 x 32         640    1-bit    40   the Command panel's button pictures (_seticon; s135)
  p4mon.pix _mopix/_chpix 48 x 48 x 5 frames 7200 1-bit 120 monsters (0-83) and persons (84-119)
A masked sprite is w*h/8 mask bytes (1 bit per pixel, MSB = leftmost, row after row; bit 1 = keep the background)
then w*h/2 pixel bytes (4-bit, high nibble first). Unmasked tiles are only the pixel bytes.
The palette: the dungeon and combat code set none, so the shared PKH palette is in effect (FORMAT.md 6.9).

Usage: x68k_p4_pix.py OUTDIR PALETTE.PKH DISK2DIR
Writes one PNG per sprite, OUTDIR/<folder>/NN.png (dungeon/floor, north, west, party; combat/ground, obstacle,
missile; ui/icons), and OUTDIR/combat/sprites/NNN.png (one p4mon record: its 5 frames left to right, 240 x 48).
Masked pixels are transparent.
"""
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from x68k_p4_pkh import decode  # noqa: E402

SHEETS = [  # (output folder, file, width, height, masked)
    ('dungeon/floor', 'fl', 32, 32, False),
    ('dungeon/north', 'yoko', 48, 32, True),
    ('dungeon/west', 'tate', 16, 64, True),
    ('dungeon/party', 'mn', 32, 32, True),
    ('combat/ground', 'bback', 48, 40, False),
    ('combat/obstacle', 'bobj', 48, 48, True),
    ('combat/missile', 'fly', 32, 32, True),
    ('ui/icons', 'icon', 32, 32, True),
]


def sprite(data, w, h, masked, pal):
    """One sprite as an RGBA image."""
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    mask = data[:w * h // 8] if masked else None
    px = data[w * h // 8:] if masked else data
    for i in range(w * h):
        if mask is not None and (mask[i // 8] >> (7 - i % 8)) & 1:
            continue
        b = px[i // 2]
        img.putpixel((i % w, i // w), pal[b >> 4 if i % 2 == 0 else b & 15] + (255,))
    return img


def sprites(raw, w, h, masked, pal):
    size = w * h // 2 + (w * h // 8 if masked else 0)
    n = len(raw) // size
    assert n * size == len(raw), (len(raw), size)
    return [sprite(raw[i * size:(i + 1) * size], w, h, masked, pal) for i in range(n)]


def strip(images):
    out = Image.new('RGBA', (sum(i.width for i in images), images[0].height), (0, 0, 0, 0))
    for k, img in enumerate(images):
        out.paste(img, (k * img.width, 0))
    return out


def find(folder, name):
    for f in os.listdir(folder):
        if f.lower() == name.lower():
            return os.path.join(folder, f)
    raise FileNotFoundError(name)


def main():
    out, pkh, disk = sys.argv[1], sys.argv[2], sys.argv[3]
    pal = decode(open(pkh, 'rb').read())[2]
    for sub, name, w, h, masked in SHEETS:
        os.makedirs(os.path.join(out, sub), exist_ok=True)
        imgs = sprites(open(find(disk, name + '.pix'), 'rb').read(), w, h, masked, pal)
        for i, img in enumerate(imgs):
            img.save(os.path.join(out, sub, f'{i:02d}.png'))
        print(f'{name}.pix: {len(imgs)} x {w}x{h}')
    mon = open(find(disk, 'p4mon.pix'), 'rb').read()
    os.makedirs(os.path.join(out, 'combat', 'sprites'), exist_ok=True)
    for r in range(len(mon) // 0x1C20):
        strip(sprites(mon[r * 0x1C20:(r + 1) * 0x1C20], 48, 48, True, pal)).save(
            os.path.join(out, 'combat', 'sprites', f'{r:03d}.png'))
    print(f'p4mon.pix: {len(mon) // 0x1C20} records x 5 frames')


if __name__ == '__main__':
    main()
