#!/usr/bin/env python3
"""Decode Phantasie IV (X68000) *.PKH pictures to PNG.

Phase 09 T6.2 / 03d 3d.8. Format from P4.X `_xunpack` -> `_scst_stacf` (header) -> `_scsvb1` (unpacker), see
reverse-engineering/x68000/phantasie4/FORMAT.md (PKH section):
  0x00  "X68000" + 0x00 0x00 (signature, not checked by P4.X)
  0x0C  16 x u16 BE palette, X68000 GRB 5-5-5-1 (G 15-11, R 10-6, B 5-1, I bit 0)
  0x34  u16 BE width (pixels), 0x36 u16 BE height
  0x38  packed data to end of file; control byte c, n = c & 0x7F (pixels):
        c & 0x80: one byte b follows, n pixels of b
        else:     ceil(n / 2) bytes follow, two pixels per byte (high nibble first)
  Each pixel is written to graphics RAM as a word; the 16-colour screen keeps the low 4 bits.

Usage: x68k_p4_pkh.py OUTDIR FILE.PKH [...]   (writes OUTDIR/<name>.png, prints size and pixel-count check)
       x68k_p4_pkh.py OUTDIR --mon p4mon.PIX SOME.PKH   (monster/person sprite sheet; palette taken from the PKH,
       all 56 PKH files carry the same palette; whether combat uses it is [?])
"""
import os
import struct
import sys

from PIL import Image


def palette(words):
    out = []
    for w in words:
        g, r, b, i = (w >> 11) & 31, (w >> 6) & 31, (w >> 1) & 31, w & 1
        out.append(tuple(((c << 1 | i) * 255 + 31) // 63 for c in (r, g, b)))
    return out


def unpack(data):
    """Return the pixel words exactly as _scsvb1 writes them."""
    px = []
    p = 0
    while p < len(data):
        c = data[p]
        p += 1
        n = c & 0x7F
        if c & 0x80:                 # run (0x3a940): n copies of the next byte; n = 0 would loop 256 times (subq.b/bne)
            b = data[p]
            p += 1
            px.extend([b] * (n or 256))
        else:                        # literal nibbles (0x3a972)
            k = n or 256
            while True:
                b = data[p]
                p += 1
                px.append(b >> 4)
                k -= 1
                if k == 0:
                    break
                px.append(b)
                k -= 1
                if k == 0:
                    break
    return px


def decode(raw):
    if raw[:6] != b'X68000':
        raise ValueError('no X68000 signature')
    pal = palette(struct.unpack('>16H', raw[0x0C:0x2C]))
    w, h = struct.unpack('>HH', raw[0x34:0x38])
    px = unpack(raw[0x38:])
    return w, h, pal, px


def mon_sheet(pix, pal, out):
    """p4mon.pix (`_hpix` / `_chpix`): records of 0x1C20 B = 5 frames x 0x5A0 B; a frame = 48 x 48 pixels:
    288 B mask (48 rows x 3 BE words, MSB = leftmost, bit 1 = transparent) then 1152 B 4-bit pixels (high nibble
    first). Writes one sheet: a row per record, 5 frames per row, magenta = transparent."""
    n = len(pix) // 0x1C20
    img = Image.new('RGB', (5 * 48, n * 48), (255, 0, 255))
    for r in range(n):
        for f in range(5):
            base = r * 0x1C20 + f * 0x5A0
            m, p = pix[base:base + 288], pix[base + 288:base + 0x5A0]
            for y in range(48):
                for x in range(48):
                    if (m[y * 6 + x // 8] >> (7 - x % 8)) & 1:
                        continue
                    b = p[y * 24 + x // 2]
                    img.putpixel((f * 48 + x, r * 48 + y), pal[b >> 4 if x % 2 == 0 else b & 15])
    img.save(out)
    return n


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    if sys.argv[2] == '--mon':     # x68k_p4_pkh.py OUTDIR --mon p4mon.PIX PALETTE.PKH
        pal = decode(open(sys.argv[4], 'rb').read())[2]
        n = mon_sheet(open(sys.argv[3], 'rb').read(), pal, os.path.join(out, 'p4mon.png'))
        print(f'p4mon: {n} records')
        return
    for path in sys.argv[2:]:
        raw = open(path, 'rb').read()
        w, h, pal, px = decode(raw)
        ok = 'ok' if len(px) == w * h else f'MISMATCH {len(px)} px'
        img = Image.new('P', (w, h))
        img.putpalette([v for c in pal for v in c])
        img.putdata([v & 15 for v in px[:w * h]] + [0] * max(0, w * h - len(px)))
        name = os.path.splitext(os.path.basename(path))[0]
        img.save(os.path.join(out, name + '.png'))
        print(f'{path}: {w}x{h} {ok}')


if __name__ == '__main__':
    main()
