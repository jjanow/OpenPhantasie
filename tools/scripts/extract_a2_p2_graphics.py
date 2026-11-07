#!/usr/bin/env python3
"""Phantasie II (Apple II, 4am crack v2/5/86): render the art to PNGs.

Apple II II has almost no bitmap art: every monster, party figure, map tile and dungeon mark is a vector shape of one
Applesoft shape table (`SHAPES`, side A, 59 shapes, loaded at $451F), drawn with DRAW at a SCALE and an HCOLOR the
BASIC picks. The only bitmap is the title picture `ICE DRAGON` (a raw 8 KB hi-res page). Notes and the shape map:
`reverse-engineering/apple2/phantasie2/GRAPHICS.md`.

Pipeline (each step is plain byte arithmetic, meant to be ported):
1. `ShapeTable` decodes the Applesoft shape bytes (3 vectors per byte: A = bits 0-2, B = bits 3-5, C = bits 6-7;
   direction = low 2 bits (0 up, 1 right, 2 down, 3 left), plot = bit 2 (C has none); a byte ends the shape when 0;
   the rest of a byte is skipped when what is left of it is 0).
2. `HiresPage.draw` executes DRAW like the Applesoft ROM: per vector, SCALE times: plot (if the plot bit is set),
   then move one pixel. ROT is always 0 here. A plot writes the HCOLOR's byte pattern through the mask
   `0x80 | 1 << (x % 7)` (Applesoft MSKTBL), so the high bit of the byte follows the colour; the colour byte
   alternates on even / odd byte columns (HCOLOR 1/2/5/6).
3. `HiresPage.to_rgb` turns the page into RGB with one colour model (GRAPHICS.md "Colour model").

Usage (repo root): python3 -I tools/scripts/extract_a2_p2_graphics.py [side-a.dsk]
Writes reverse-engineering/apple2/phantasie2/extracted/graphics/ (PNGs + extraction-index.json).
"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # `python3 -I` drops the script dir

from a2_rdos import RdosDisk  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
SIDE_A = REPO / "reverse-engineering/apple2/original-images/phantasie2/4am-crack/phantasie2-4am-side-a.dsk"
OUT = REPO / "reverse-engineering/apple2/phantasie2/extracted/graphics"

# ---------------------------------------------------------------------------------------------------------------
# Hi-res page model
# ---------------------------------------------------------------------------------------------------------------

# Applesoft HCOLOR= n -> (byte on an even byte column, byte on an odd byte column). The ROM keeps one byte and flips it
# for odd columns (COLOR.SHIFT), which gives these pairs.
HCOLOR_BYTES = {
    0: (0x00, 0x00),  # black
    1: (0x2A, 0x55),  # green
    2: (0x55, 0x2A),  # violet
    3: (0x7F, 0x7F),  # white
    4: (0x80, 0x80),  # black (high bit set)
    5: (0xAA, 0xD5),  # orange
    6: (0xD5, 0xAA),  # blue
    7: (0xFF, 0xFF),  # white (high bit set)
}
HCOLOR_NAMES = ["black", "green", "violet", "white", "black2", "orange", "blue", "white2"]

# Colour model (GRAPHICS.md): two lit neighbours = white; a lit pixel alone takes its artifact colour from
# (high bit, x parity); a dark pixel between two lit pixels of the same colour takes that colour (the 140-wide
# colour fill). RGB values: the usual emulator "RGB" set (AppleWin / MAME style) [~].
RGB = {
    "black": (0, 0, 0),
    "white": (255, 255, 255),
    "violet": (255, 68, 253),
    "green": (20, 245, 60),
    "blue": (20, 207, 253),
    "orange": (255, 106, 60),
}


def artifact(x: int, high: bool) -> str:
    if high:
        return "orange" if x & 1 else "blue"
    return "green" if x & 1 else "violet"


class HiresPage:
    """A hi-res page as rows of byte columns. `cols` may exceed 40 for a scratch canvas (wider than the screen);
    keep x offsets a multiple of 14 so byte-column and pixel parity stay what they would be on screen."""

    def __init__(self, cols: int = 40, rows: int = 192):
        self.cols, self.rows = cols, rows
        self.mem = [bytearray(cols) for _ in range(rows)]

    @classmethod
    def from_apple(cls, data: bytes) -> "HiresPage":
        """An 8 KB page image in Apple memory order (row y at ((y&7)<<10) | (((y>>3)&7)<<7) | (y>>6)*40)."""
        page = cls()
        for y in range(192):
            base = ((y & 7) << 10) | (((y >> 3) & 7) << 7) | ((y >> 6) * 40)
            page.mem[y][:] = data[base:base + 40]
        return page

    def poke_apple(self, offset: int, value: int) -> None:
        """POKE into the page at byte `offset` from its base ($2000 / $4000); screen holes are ignored."""
        off = offset & 0x1FFF
        y_lo, rest = off >> 10, off & 0x3FF
        y_mid, rest = rest >> 7, rest & 0x7F
        if rest >= 120:
            return
        third, col = divmod(rest, 40)
        self.mem[third * 64 + y_mid * 8 + y_lo][col] = value

    def plot(self, x: int, y: int, hcolor: int, xor: bool = False) -> None:
        if not (0 <= y < self.rows and 0 <= x < self.cols * 7):
            return
        col, bit = divmod(x, 7)
        mask = 0x80 | (1 << bit)
        row = self.mem[y]
        if xor:  # XDRAW: flip the pixel bit (the high bit is not touched)
            row[col] ^= 1 << bit
            return
        colour = HCOLOR_BYTES[hcolor][col & 1]
        row[col] = (row[col] & ~mask & 0xFF) | (colour & mask)

    def draw(self, vectors: list[tuple[int, bool]], x: int, y: int, hcolor: int, scale: int = 1, xor: bool = False):
        """Applesoft DRAW (ROT=0): each vector SCALE times: plot if its plot bit is set, then move one pixel."""
        for direction, plot in vectors:
            dx, dy = ((0, -1), (1, 0), (0, 1), (-1, 0))[direction]
            for _ in range(scale or 256):  # SCALE= 0 means 256 in the ROM
                if plot:
                    self.plot(x, y, hcolor, xor)
                x, y = x + dx, y + dy
        return x, y

    def to_rgb(self, box: tuple[int, int, int, int] | None = None) -> list[list[tuple[int, int, int]]]:
        """RGB rows of the page, or of `box` = (x0, y0, x1, y1) (end exclusive; neighbours outside it still count)."""
        width = self.cols * 7
        x0, y0, x1, y1 = box or (0, 0, width, self.rows)
        out = []
        for y in range(y0, y1):
            row = self.mem[y] if 0 <= y < self.rows else bytes(self.cols)
            bits = [(row[x // 7] >> (x % 7)) & 1 for x in range(width)]
            high = [row[x // 7] >> 7 for x in range(width)]
            line = []
            for x in range(x0, x1):
                if not 0 <= x < width:
                    line.append(RGB["black"])
                    continue
                left = bits[x - 1] if x > 0 else 0
                right = bits[x + 1] if x + 1 < width else 0
                if bits[x]:
                    name = "white" if (left or right) else artifact(x, bool(high[x]))
                elif left and right:
                    c_left = artifact(x - 1, bool(high[x - 1]))
                    lone_left = not (bits[x - 2] if x > 1 else 0)
                    lone_right = not (bits[x + 2] if x + 2 < width else 0)
                    name = c_left if lone_left and lone_right else "black"
                else:
                    name = "black"
                line.append(RGB[name])
            out.append(line)
        return out


# ---------------------------------------------------------------------------------------------------------------
# Shape table
# ---------------------------------------------------------------------------------------------------------------

class ShapeTable:
    """Applesoft shape table: byte 0 = count, byte 1 unused, then count u16 LE offsets from the table start."""

    def __init__(self, data: bytes):
        self.data = data
        self.count = data[0]
        self.offsets = [data[2 + 2 * i] | data[3 + 2 * i] << 8 for i in range(self.count)]

    def vectors(self, n: int) -> list[tuple[int, bool]]:
        """Shape n (1-based, as DRAW n) -> list of (direction, plot)."""
        p = self.offsets[n - 1]
        out: list[tuple[int, bool]] = []
        while p < len(self.data):
            b = self.data[p]
            p += 1
            if b == 0:
                break
            out.append((b & 3, bool(b & 4)))            # A
            b >>= 3
            if b == 0:
                continue
            out.append((b & 3, bool(b & 4)))            # B
            b >>= 3
            if b == 0:
                continue
            out.append((b & 3, False))                  # C: move only
        return out


def bbox(vectors, scale=1):
    x = y = 0
    pts = []
    for direction, plot in vectors:
        dx, dy = ((0, -1), (1, 0), (0, 1), (-1, 0))[direction]
        for _ in range(scale):
            if plot:
                pts.append((x, y))
            x, y = x + dx, y + dy
    if not pts:
        return None
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    return min(xs), min(ys), max(xs), max(ys)


# ---------------------------------------------------------------------------------------------------------------
# Rendering helpers
# ---------------------------------------------------------------------------------------------------------------

def save_png(rgb_rows, path: Path, crop=None, transparent_black=True) -> tuple[int, int]:
    from PIL import Image
    h, w = len(rgb_rows), len(rgb_rows[0])
    img = Image.new("RGBA", (w, h))
    img.putdata([(r, g, b, 0 if transparent_black and (r, g, b) == (0, 0, 0) else 255)
                 for row in rgb_rows for (r, g, b) in row])
    if crop:
        img = img.crop(crop)
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path)
    return img.size


# Scratch canvas: 80 byte columns (560 px), shapes drawn at an origin offset of PAD_X (multiple of 14) + the in-game x.
PAD_X, PAD_Y = 140, 200


def render_shape(table: ShapeTable, parts: list[tuple[int, int]], x: int, scale: int, path: Path,
                 cell: tuple[int, int] | None = None) -> dict:
    """Draw shapes `parts` = [(shape, hcolor), ...] at screen x (sets the colour phase), crop to the drawn box (or to
    `cell` = (w, h) from the origin) and save. Returns the index entry (origin = the DRAW point inside the PNG)."""
    page = HiresPage(cols=80, rows=400)
    boxes = []
    for shape, hcolor in parts:
        vec = table.vectors(shape)
        page.draw(vec, PAD_X + x, PAD_Y, hcolor, scale)
        if (b := bbox(vec, scale)) is not None:
            boxes.append(b)
    if cell or not boxes:  # a cell crop, or a shape that plots nothing (44): a 1 x 1 blank
        w, h = cell or (1, 1)
        x0, y0, x1, y1 = PAD_X + x, PAD_Y, PAD_X + x + w, PAD_Y + h
    else:
        x0 = PAD_X + x + min(b[0] for b in boxes) - 1
        y0 = PAD_Y + min(b[1] for b in boxes)
        x1 = PAD_X + x + max(b[2] for b in boxes) + 2
        y1 = PAD_Y + max(b[3] for b in boxes) + 1
    size = save_png(page.to_rgb((x0, y0, x1, y1)), path)
    return {"file": str(path.relative_to(OUT)), "size": list(size), "origin": [PAD_X + x - x0, PAD_Y - y0],
            "shapes": [p[0] for p in parts], "hcolors": [p[1] for p in parts], "scale": scale, "screen_x": x}


def read_monsters(disk: RdosDisk) -> list[dict]:
    """MN20..MN29: 8 text records each (CR-separated, high-bit ASCII), field order of OUTDOORS2 line 910."""
    fields = ["name", "armor", "damage", "level", "xp", "special_chance", "special_0", "special_1", "special_2",
              "hp_base", "combat_rating", "companion_0", "companion_1", "scale", "hcolor", "reaction_modifier",
              "next_group_chance", "defense", "gold", "group_size_factor", "group_height", "treasure_0",
              "treasure_1", "attack_power", "shape"]
    out = []
    for f in range(10):
        text = bytes(c & 0x7F for c in disk.read(f"MN2{f}")).split(b"\r")
        for r in range(8):
            rec = text[r * 25:(r + 1) * 25]
            m = {"index": f * 8 + r, "name": rec[0].decode("ascii")}
            m.update({k: int(v) for k, v in zip(fields[1:], rec[1:])})
            out.append(m)
    return out


def slug(name: str) -> str:
    return "".join(c if c.isalnum() else "-" for c in name.lower()).strip("-")


# Party figure (TOWN2 lines 2350-2356): body shape by race index (0-based) and class index.
def party_body_shape(race: int, cls: int) -> int:
    if race > 6:
        return 26
    if race not in (0, 2):
        return 25
    return 27 if cls == 1 or cls > 3 else 19


TERRAINS = ["GRASS", "FOREST", "HILLS", "MOUNTAINS", "BUILDINGS", "DUNGEON", "PATHWAY", "NETHERWORLD", "LAVA",
            "DESERT", "WATER", "MIST", "ASTRAL PLANE"]  # OUTDOORS2 line 120


RACES = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC",
         "GOBLIN", "LIZARD MAN", "MINOTAUR", "UNDEAD"]  # TOWN2 line 270
BODIES = {19: "human-elf-fighter", 25: "small-races", 26: "monster-races", 27: "human-elf-robed"}
# Dungeon map marks (DUNGON2 5035-5062): the pack's glyph number = the shape number; 0 = nothing drawn (black).
DUNGEON_MARKS = {31: "wall", 32: "cells-185-199", 33: "cells-200-214", 34: "cells-215-217", 35: "cells-218-220",
                 36: "cells-221-up", 30: "party-cursor"}


def _blank(path: Path, w: int, h: int) -> dict:
    from PIL import Image
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.new("RGBA", (w, h), (0, 0, 0, 255)).save(path)
    return {"file": str(path.relative_to(OUT)), "size": [w, h]}


def main(argv: list[str]) -> None:
    disk = RdosDisk(argv[1] if len(argv) > 1 else SIDE_A)
    table = ShapeTable(disk.read("SHAPES"))
    index: dict = {"_source": "Phantasie II Apple II (4am crack v2/5/86) side A: SHAPES ($451F, 59 shapes), ICE DRAGON "
                              "(hi-res page), MN20..MN29 (monster shape / scale / hcolor). tools/scripts/"
                              "extract_a2_p2_graphics.py; notes reverse-engineering/apple2/phantasie2/GRAPHICS.md.",
                   "origin_note": "origin = where the DRAW point sits inside the PNG (x, y); screen_x = the in-game x "
                                  "the colour phase was taken from.",
                   "shapes": {}, "monsters": {}, "figures": {}, "portraits": {}, "tiles": {}, "dungeon": {},
                   "effects": {}, "screens": {}}

    # 1. every shape, white (HCOLOR 3), scale 1, at screen x 0.
    for n in range(1, table.count + 1):
        index["shapes"][n] = render_shape(table, [(n, 3)], 0, 1, OUT / "shapes" / f"{n:02d}.png")

    # 2. monsters: pose A = S% (OUTDOORS2 / DUNGON2 1550), pose B = S%+1 (2006: drawn when the monster attacks or
    #    casts, 1895 / 1940), SCALE = C%, HCOLOR = H%, first figure of a group at x = H% (1550).
    for m in read_monsters(disk):
        s, c, h = m["shape"], m["scale"], m["hcolor"]
        name = f"{m['index']:02d}-{slug(m['name'])}.png"
        idle = render_shape(table, [(s, h)], h, c, OUT / "monsters" / "idle" / name)
        act = render_shape(table, [(s + 1, h)], h, c, OUT / "monsters" / "attacking" / name)
        index["monsters"][m["index"]] = {"name": m["name"], "shape": s, "scale": c, "hcolor": h,
                                         "group_height": m["group_height"], "idle": idle, "attacking": act}

    # 3. party figures (OUTDOORS2 1525-1534, 1695, 1760; TOWN2 2350-2356): body FN P(168) + overlay 20 (stand),
    #    22 (attack, 1695), 21 (cast 1760 / parry 1532), HCOLOR 3, SCALE 1, at x = 19 * slot (slot 0 here).
    for body, label in BODIES.items():
        for overlay, pose in ((20, "idle"), (22, "attacking"), (21, "casting")):
            index["figures"][f"{body}.{pose}"] = render_shape(
                table, [(body, 3), (overlay, 3)], 0, 1, OUT / "figures" / pose / f"{body}-{label}.png")
    # The pack's portrait.{race}: race -> body as TOWN2 2350-2356; HUMAN / ELF use the fighter-type body 19 (their
    # MONK / PRIEST / WIZARD get 27 in the original, which a race-keyed picture cannot show).
    for race, rname in enumerate(RACES):
        body = party_body_shape(race, 3)
        for overlay, pose in ((20, "idle"), (22, "attacking")):
            index["portraits"][f"{race}.{pose}"] = render_shape(
                table, [(body, 3), (overlay, 3)], 0, 1, OUT / "portraits" / pose / f"{race:02d}-{slug(rname)}.png")
            index["portraits"][f"{race}.{pose}"]["body"] = body

    # 4. overworld tiles by map byte (OUTDOORS2 5030-5040): byte b < 120 draws shape 37 + b // 10 in HCOLOR
    #    INT(1 + .51 * (b // 10)) in a 7 x 6 cell at (140 + 7X, 2 + 6Y); b >= 120 is fog (not drawn: black). The colour
    #    phase flips with X: the pack uses the even-column cell (X = 0, screen x 140); the odd ones are written too.
    for t, tname in enumerate(TERRAINS[:12]):
        hc = int(1 + 0.51 * t)
        index["tiles"][f"class.{t}.odd"] = render_shape(
            table, [(37 + t, hc)], 147, 1, OUT / "overworld" / "odd" / f"{t:02d}-{slug(tname)}.png", cell=(7, 6))
        for b in range(t * 10, t * 10 + 10):
            index["tiles"][b] = render_shape(
                table, [(37 + t, hc)], 140, 1, OUT / "overworld" / "tiles" / f"{b:03d}.{slug(tname)}.png", cell=(7, 6))
    for b in range(120, 256):
        index["tiles"][b] = _blank(OUT / "overworld" / "tiles" / f"{b:03d}.fog.png", 7, 6)
    # The party mark: XDRAW 43 (5175) over the cell, a filled 7 x 6 block; drawn here as DRAW in white.
    index["tiles"]["party"] = render_shape(table, [(43, 3)], 140, 1, OUT / "overworld" / "party.png", cell=(7, 6))

    # 5. dungeon map marks: 4 x 4 cells at (144 + 4X, 4 + 4Y). 31 = wall (HCOLOR 3), open cells erase 31 (black),
    #    32..36 by cell class; 34 (215-217) is drawn with HCOLOR 0, so it never shows; 30 = party cursor (XDRAW).
    for n, what in DUNGEON_MARKS.items():
        index["dungeon"][n] = render_shape(table, [(n, 3)], 144, 1, OUT / "dungeon" / f"glyph-{n}.{what}.png",
                                           cell=(4, 4))
        index["dungeon"][n]["use"] = what
    index["dungeon"][0] = _blank(OUT / "dungeon" / "glyph-0.blank.png", 4, 4)

    # 6. combat effects: 23 spell sparkle over a member (1762), 24 = spell hit on a monster (4132: SP 1 -> 35 - 11),
    #    59 = the tossed rock (1628 miss at 70,20; 4132 SP 2 -> 59). All XDRAW in the game, drawn here in white.
    for n, what in ((23, "spell-sparkle"), (24, "spell-hit"), (59, "rock")):
        index["effects"][n] = render_shape(table, [(n, 3)], 0, 1, OUT / "effects" / f"{n:02d}-{what}.png")
        index["effects"][n]["use"] = what

    # 7. the title picture: ICE DRAGON is one 8 KB hi-res page (PHANTASIE II lines 20 / 30 load it into pages 1 and 2).
    page = HiresPage.from_apple(disk.read("ICE DRAGON"))
    save_png(page.to_rgb(), OUT / "screens" / "ice-dragon.png", transparent_black=False)
    index["screens"]["ice-dragon"] = {"file": "screens/ice-dragon.png", "size": [280, 192]}
    # Page 2 after the boot program's POKE loop (lines 243-257): DATA 260-290 = 7 rows of (address, 25 bytes) poked at
    # address + 8192, rows 21341.. and 21469.. cleared before / after: the "HIT ANY KEY TO CONTINUE" line.
    nums = _boot_data(disk.read("PHANTASIE II"))
    for addr in range(21341, 21366):
        page.poke_apple(addr - 16384, 0)
    for r in range(7):
        row = nums[r * 26:(r + 1) * 26]
        for i, v in enumerate(row[1:]):
            page.poke_apple(row[0] + 8192 + i - 16384, v)
    for addr in range(21469, 21494):
        page.poke_apple(addr - 16384, 0)
    save_png(page.to_rgb(), OUT / "screens" / "title.png", transparent_black=False)
    index["screens"]["title"] = {"file": "screens/title.png", "size": [280, 192],
                                 "note": "ICE DRAGON + the boot program's POKE rows (lines 243-257); the HPLOT "
                                         "lightning (lines 50-94, random) is not drawn."}

    (OUT / "extraction-index.json").write_text(json.dumps(index, indent=1) + "\n")
    print(f"{table.count} shapes, {len(index['monsters'])} monsters -> {OUT.relative_to(REPO)}")


def _boot_data(program: bytes) -> list[int]:
    """The numbers of the boot program's DATA lines (260-290), in order."""
    from a2_applesoft import lines
    nums: list[int] = []
    for _, ln, text in lines(program):
        if 260 <= ln <= 290 and text.strip().startswith("DATA"):
            nums += [int(v) for v in text.strip()[4:].replace(" ", "").split(",") if v]
    return nums


if __name__ == "__main__":
    main(sys.argv)
