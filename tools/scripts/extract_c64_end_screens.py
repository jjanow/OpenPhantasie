#!/usr/bin/env python3
"""Write the C64 Phantasie I / II ending and party-wipe (defeat) pictures as PNGs (s221).

    python3 -I tools/scripts/extract_c64_end_screens.py [--repo DIR]

Reads the disk extracts under reverse-engineering/c64/phantasie{1,2}/extracted-v1.x/ and writes to
reverse-engineering/c64/phantasie{1,2}/extracted/graphics/{ending,defeat}/ (beside the c64 art pack's other
pictures; the importer's C64PhantasieArt writes the same files, s221). The layout, colours and sprite
positions are the ones read in the programs (P.END, CEMETARY, CEMETARY2, WIN) and seen live in VICE; see
reverse-engineering/c64/phantasie1/GRAPHICS.md section 9 and ../phantasie2/GRAPHICS.md section 8.

Full screens are 320 x 200 (the C64's display window, no border), opaque; single sprites and sprite strips are
RGBA with transparent background. Palette: Colodore (C64Gfx.Colodore, the packs' palette).

Phantasie I (side 1 ZALL, side 2 SKEL2 / CSPRIT / FONTO):
  ending/zeus.png            ZALL chars $60-$FB, text cols 13-24 x rows 2-14, white (the final state of the rise)
  ending/zeus-speech.png     the face chars blanked, the face sprite (block $C6, sprite 6) as during the speech
  ending/zeus-grant.png      also the hand chars blanked and the hand sprite (block $C5, sprite 7)
  ending/zeus-strike.png     Zeus with the lightning hand (block $CF, sprite 7, light blue)
  ending/sprites/*.png       ZALL's sprite blocks $C0-$D0 in their live colours (hands, faces, a ball, the
                             lightning hand, the members' cloud; $D1-$D2 are disk slack)
  ending/font.png            ZALL chars 0-95 (the ending's mixed-case font), 16 x 6 sheet
  defeat/temple.png          FONTO chars $40-$F3, cols 10-27 x rows 0-9, dark grey, open doorway
  defeat/temple-door.png     the same with the closed door (SKEL2 block 7, sprite 7 behind the chars)
  defeat/temple-demon.png    the same with the Great Demon's face (SKEL2 block 6 = CSPRIT block 15, sprite 6, purple)
  defeat/ghost.png, door.png, demon-face.png, font.png
  defeat/temple-demon-doorway.png  II only: temple-demon.png's (123, 19)-(171, 72), the doorway with the face, for
                             the ST-placed decor.demon-face slot (drawn at (123, 19); the C64 face is at (124, 31))
Phantasie II (side 2 PH2.END-4, TLUM1; SKEL2 / CSPRIT / FONTO byte-equal to I's):
  ending/hall.png            PH2.END-4: colour RAM, screen, character set (hires text), the cyan text panel
  ending/crowd-0.png, -1.png the TLUM1 crowd band (7 expanded green sprites, VIC y 160), its two frames
  ending/hall-crowd.png      hall + crowd frame 0
  defeat/*                   as I's, from II's own files
"""
import argparse
import os
import sys

from PIL import Image

COLODORE = [(0x00, 0x00, 0x00), (0xFF, 0xFF, 0xFF), (0x96, 0x28, 0x2E), (0x5B, 0xD6, 0xCE),
            (0x9F, 0x2D, 0xAD), (0x41, 0xB9, 0x36), (0x27, 0x24, 0xC4), (0xEF, 0xF3, 0x47),
            (0x9F, 0x48, 0x15), (0x5E, 0x35, 0x00), (0xDA, 0x5F, 0x66), (0x47, 0x47, 0x47),
            (0x78, 0x78, 0x78), (0x91, 0xFF, 0x84), (0x68, 0x64, 0xFF), (0xAE, 0xAE, 0xAE)]

P1_S1 = "reverse-engineering/c64/phantasie1/extracted-v1.4/phantasie_s1_ssi_1985__v1_4_"
P1_S2 = "reverse-engineering/c64/phantasie1/extracted-v1.4/phantasie_s2_ssi_1985__v1_4_"
P2_S2 = "reverse-engineering/c64/phantasie2/extracted-v1.2/phantasie_ii_s2_ssi_1986__v1_2__never_used____"
P1_OUT = "reverse-engineering/c64/phantasie1/extracted/graphics"
P2_OUT = "reverse-engineering/c64/phantasie2/extracted/graphics"


def prg(path):
    """A PRG's data (the 2-byte load address dropped) and its load address."""
    d = open(path, "rb").read()
    return d[2:], d[0] | d[1] << 8


class Screen:
    """A 320 x 200 C64 display: palette indices, a foreground mask for sprite priority."""

    def __init__(self, bg=0):
        self.px = [[bg] * 320 for _ in range(200)]
        self.fg = [[False] * 320 for _ in range(200)]

    def chars(self, charset, codes, colours):
        """Hires text: codes / colours are 25 x 40 lists (None = leave the cell)."""
        for row in range(25):
            for col in range(40):
                code = codes[row][col]
                if code is None:
                    continue
                for r in range(8):
                    b = charset[code * 8 + r] if code * 8 + r < len(charset) else 0
                    for p in range(8):
                        on = (b >> (7 - p)) & 1
                        y, x = row * 8 + r, col * 8 + p
                        if on:
                            self.px[y][x] = colours[row][col]
                        self.fg[y][x] = bool(on)

    def sprite(self, block, vx, vy, colour, xexp=False, yexp=False, behind=False, multi=None):
        """Draw a 63-byte sprite block at VIC position (vx, vy); multi = (mc1, mc2) for a multicolour sprite."""
        for y, x, c in sprite_pixels(block, colour, xexp, yexp, multi):
            sx, sy = vx - 24 + x, vy - 50 + y
            if 0 <= sx < 320 and 0 <= sy < 200 and not (behind and self.fg[sy][sx]):
                self.px[sy][sx] = c

    def image(self):
        img = Image.new("RGB", (320, 200))
        img.putdata([COLODORE[c] for row in self.px for c in row])
        return img


def sprite_pixels(block, colour, xexp=False, yexp=False, multi=None):
    xs, ys = (2 if xexp else 1), (2 if yexp else 1)
    for r in range(21):
        bits = block[3 * r] << 16 | block[3 * r + 1] << 8 | block[3 * r + 2]
        for p in range(24):
            if multi:
                k = (bits >> (22 - (p & ~1))) & 3
                c = None if k == 0 else (multi[0], colour, multi[1])[k - 1]
            else:
                c = colour if (bits >> (23 - p)) & 1 else None
            if c is None:
                continue
            for a in range(xs):
                for b in range(ys):
                    yield r * ys + b, p * xs + a, c


def sprite_image(block, colour, xexp=False, yexp=False, multi=None):
    img = Image.new("RGBA", (24 * (2 if xexp else 1), 21 * (2 if yexp else 1)), (0, 0, 0, 0))
    for y, x, c in sprite_pixels(block, colour, xexp, yexp, multi):
        img.putpixel((x, y), COLODORE[c] + (255,))
    return img


def font_sheet(charset, count, colour=1):
    img = Image.new("RGB", (128, 8 * ((count + 15) // 16)))
    for code in range(count):
        for r in range(8):
            b = charset[code * 8 + r]
            for p in range(8):
                if (b >> (7 - p)) & 1:
                    img.putpixel(((code % 16) * 8 + p, (code // 16) * 8 + r), COLODORE[colour])
    return img


def rect(first, col0, row0, cols, rows):
    """SYS Q,x0,y0,x1,y1,colour,first (UTIL-3.O): codes first.. column by column."""
    codes = [[None] * 40 for _ in range(25)]
    for c in range(cols):
        for r in range(rows):
            codes[row0 + r][col0 + c] = first + c * rows + r
    return codes


def full(colour):
    return [[colour] * 40 for _ in range(25)]


def save(img, out, rel, written):
    path = os.path.join(out, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, optimize=True)
    written.append(path)


# ---- Phantasie I: P.END + ZALL -------------------------------------------------------------------------------

ZALL_SPRITES = {  # block: (name, colour, multicolour) as live (P.END s221; [?] = not seen, drawn white)
    0xC0: ("hand-0", 1, None), 0xC1: ("hand-1", 1, None), 0xC2: ("hand-2", 1, None),
    0xC3: ("hand-3", 1, None), 0xC4: ("hand-4", 1, None), 0xC5: ("hand-5", 1, None),
    0xC6: ("face-0", 1, None), 0xC7: ("face-1", 1, None), 0xC8: ("face-2", 1, None), 0xC9: ("face-3", 1, None),
    0xCA: ("face-4", 1, None), 0xCB: ("face-5", 1, None), 0xCC: ("face-6", 1, None), 0xCD: ("face-7", 1, None),
    0xCE: ("orb", 1, None), 0xCF: ("lightning-hand", 14, None), 0xD0: ("cloud", 2, (7, 2)),
}   # $D1, $D2: copied with the rest but disk slack (directory entries TWN 6 .. TWN 9), not pictures


def p1_ending(repo, out, written):
    zall, at = prg(os.path.join(repo, P1_S1, "ZALL.prg"))
    assert at == 0x4000 and len(zall) == 3716, "ZALL: unexpected file"
    charset = zall[:0x7FF] + b"\0"          # P.END's copies leave the last byte (char $FF row 7) at 0 (live)
    sprites = zall[0x800:0x800 + 1216]       # $4800 -> $F000 = blocks $C0..$D2
    def zeus(blank=()):
        codes = rect(0x60, 13, 2, 12, 13)
        for row, col in blank:            # P.END prints spaces where a sprite takes over (live)
            codes[row][col] = 0x20
        s = Screen()
        s.chars(charset, codes, full(1))
        return s

    def block(n):
        return sprites[(n - 0xC0) * 64:][:63]

    save(zeus().image(), out, "ending/zeus.png", written)
    face_cells = [(4, 18), (4, 19), (5, 18), (5, 19)]
    hand_cells = [(3, 22), (3, 23), (3, 24), (4, 22), (4, 23), (4, 24)]
    s = zeus(face_cells)                  # the speech: face sprite 6 (frames $C6, $C7, $CC, $C9) at VIC (161, 77)
    s.sprite(block(0xC6), 0xA1, 0x4D, 1)
    save(s.image(), out, "ending/zeus-speech.png", written)
    s = zeus(face_cells + hand_cells)     # the grant: also the hand, sprite 7 (frames $C0-$C5) at VIC (200, 74)
    s.sprite(block(0xC6), 0xA1, 0x4D, 1)
    s.sprite(block(0xC5), 0xC8, 0x4A, 1)
    save(s.image(), out, "ending/zeus-grant.png", written)
    s = zeus()                            # a strike: sprite 7 = $CF, light blue, at VIC (202, 62), flashing
    s.sprite(block(0xCF), 0xCA, 0x3E, 14)
    save(s.image(), out, "ending/zeus-strike.png", written)
    for block, (name, colour, multi) in ZALL_SPRITES.items():
        data = sprites[(block - 0xC0) * 64:][:63]
        save(sprite_image(data, colour, multi=multi), out, f"ending/sprites/{block:02x}-{name}.png", written)
    save(font_sheet(charset, 96), out, "ending/font.png", written)


DOORWAY = (123, 19, 172, 73)  # PIL box: x 123-171, y 19-72 (49 x 54)


# ---- Phantasie I / II: CEMETARY + SKEL2 / CSPRIT / FONTO -----------------------------------------------------

def defeat(side2, out, written, doorway=False):
    skel, at = prg(os.path.join(side2, "SKEL2.prg"))
    assert at == 0x2000 and len(skel) == 512, "SKEL2: unexpected file"
    csprit, _ = prg(os.path.join(side2, "CSPRIT.prg"))
    fonto, _ = prg(os.path.join(side2, "FONTO.prg"))
    assert len(csprit) == 1025 and len(fonto) == 1968, "CSPRIT / FONTO: unexpected file"
    charset = fonto + bytes(2048 - len(fonto))
    ghost, door = skel[0:63], skel[7 * 64:7 * 64 + 63]
    face = skel[6 * 64:6 * 64 + 63]         # block $8A; byte-equal to CSPRIT block 15
    assert face == csprit[15 * 64:15 * 64 + 63]
    codes = rect(0x40, 10, 0, 18, 10)

    def temple():
        s = Screen()
        s.chars(charset, codes, full(11))
        return s

    save(temple().image(), out, "defeat/temple.png", written)
    s = temple()
    s.sprite(door, 0x94, 0x4D, 15, True, True, behind=True)
    save(s.image(), out, "defeat/temple-door.png", written)
    s = temple()
    s.sprite(face, 0x94, 0x51, 4, True, True)
    save(s.image(), out, "defeat/temple-demon.png", written)
    if doorway:  # the remaster's decor.demon-face is drawn at the ST's (123, 19); the face sprite covers (124, 31)-(171, 72)
        save(s.image().crop(DOORWAY), out, "defeat/temple-demon-doorway.png", written)
    save(sprite_image(ghost, 1), out, "defeat/ghost.png", written)
    save(sprite_image(door, 15, True, True), out, "defeat/door.png", written)
    save(sprite_image(face, 4, True, True), out, "defeat/demon-face.png", written)
    save(font_sheet(charset, 64), out, "defeat/font.png", written)


# ---- Phantasie II: WIN + PH2.END-4 + TLUM1 -------------------------------------------------------------------

def crowd_strip(band, blocks, frame):
    """TLUM1's band as WIN shows it: sprites 0-6 (enable $7F, WIN pokes sprite 6's x high bit), block base $30 + 8 frame."""
    img = Image.new("RGBA", (320, 42), (0, 0, 0, 0))
    xs = [band[1 + 2 * i] for i in range(8)]
    xs[6] = 47 + 256                       # WIN: POKE A1 + 13, 47 and $D010 := 64 (the file holds 0)
    for i in range(7):
        b = blocks[(band[26 + i] + 8 * frame) * 64:][:63]
        for y, x, c in sprite_pixels(b, band[17 + i] & 15, True, True):
            sx = xs[i] - 24 + x
            if 0 <= sx < 320:
                img.putpixel((sx, y), COLODORE[c] + (255,))
    return img


def p2_ending(repo, out, written):
    pic, at = prg(os.path.join(repo, P2_S2, "PH2.END-4.prg"))
    assert at == 0x2000 and len(pic) == 4095, "PH2.END-4: unexpected file"
    colour_ram, screen = pic[0:1000], pic[1024:2024]
    charset = pic[2048:2048 + 2047] + b"\0"  # WIN copies 2,047 bytes to $E800
    s = Screen()
    s.chars(charset, [list(screen[r * 40:r * 40 + 40]) for r in range(25)],
            [[c & 15 for c in colour_ram[r * 40:r * 40 + 40]] for r in range(25)])
    hall = s.image()
    save(hall, out, "ending/hall.png", written)
    tlum, at = prg(os.path.join(repo, P2_S2, "TLUM1.prg"))
    assert at == 0x1FDA, "TLUM1: unexpected file"
    band, blocks = tlum[:38], tlum[38:] + bytes(16 * 64 - (len(tlum) - 38))
    for frame in (0, 1):
        save(crowd_strip(band, blocks, frame), out, f"ending/crowd-{frame}.png", written)
    comp = hall.copy()
    strip = crowd_strip(band, blocks, 0)
    comp.paste(strip, (0, band[0] - 50), strip)  # band y $A0 (live: every sprite at VIC y 160)
    save(comp, out, "ending/hall-crowd.png", written)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--repo", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    a = ap.parse_args()
    repo = os.path.abspath(a.repo)
    written = []
    p1_ending(repo, os.path.join(repo, P1_OUT), written)
    defeat(os.path.join(repo, P1_S2), os.path.join(repo, P1_OUT), written)
    p2_ending(repo, os.path.join(repo, P2_OUT), written)
    defeat(os.path.join(repo, P2_S2), os.path.join(repo, P2_OUT), written, doorway=True)
    for p in written:
        print(os.path.relpath(p, repo))
    return 0


if __name__ == "__main__":
    sys.exit(main())
