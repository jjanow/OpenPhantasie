#!/usr/bin/env python3
"""Phantasie III (Apple II, SSI 1987): render the art to PNGs.

Notes, evidence and the [C] / [~] / [?] tags: `reverse-engineering/apple2/phantasie3/GRAPHICS.md`. The art of III's
Apple version (both the `[cr]` v1.0 sides and the original v3.0 disk 1, whose art files are byte-identical except
`ICE DRAGON`, `WIN PIC` and the font `IMPTEXT`):

* `MONSTER SHAPES` ($1A00, 1,536 B): 24 slots of 64 bytes; drawn by `MONSTER PLOT` (machine code at $4582: shape
  $457F, byte column $4580, y $4581) as raw hi-res bytes, 3 bytes (21 pixels) wide, 21 rows, or 42 rows (two slots)
  for shape >= 21. Monsters by the MN3x record's S% (pose B = S% + 1, but 23 for 21). `NIKADEMUS` (side B, $1F00,
  256 B) replaces slots 21-24 for monsters 77 and 78 (DUNGON3 line 899).
* `SHAPES` ($45DC, 48 Applesoft shapes, INTRO3 line 5): party bodies and arms, effects, the overworld terrain (37 +
  class) and the dungeon map marks (30-36), drawn with DRAW / XDRAW (the II pipeline's `HiresPage` / `ShapeTable`).
* `SCREEN`, `ICE DRAGON`, `WIN PIC`: hi-res pages. `ICE DRAGON` and `WIN PIC` of the `[cr]` copy are the cracker's
  screens (known SHA-1s below): not used. The v3.0 `WIN PIC` is scrambled on the disk; the ending (WIN lines 83-85)
  runs `ROUTINE` 30000 (stride X = 1, 8, ... 253) then 30050 over it, which `decode_win_pic` replays.
* `PIC1` / `PIC2` / `PIC3` / `OUTPIC` / `PIC`: 12 x 96-byte panels (x bytes 0-11, y 96-191) that `PICPLOT` copies to
  the map box (v3.0 DEMO: wilderness / strange lands / dungeons; OUTPIC the empty box; PIC the saved dungeon map).
* `IMPTEXT` / `IMPREVERSE` ($1828): the hi-res text output routine (88 bytes) and its font, glyph of character c at
  $1800 + 8 (c - $A0) for c in $A0-$BF, $1900 + 8 (c - $C0) for $C0-$DF.

Overworld tiles are keyed by the remaster's map byte (DOS III's tile numbers): DOS_TO_APPLE gives the Apple terrain
class each byte is drawn as (from the Apple's own region files OT1-OT25, which are Scandor cut in 5 x 5 blocks of
10 x 15, cell for cell; the planes by terrain, GRAPHICS.md section 5).

Usage (repo root): python3 -I tools/scripts/extract_a2_p3_graphics.py [side-a.(dsk|woz) side-b.(dsk|woz)]
Default: the original v3.0 WOZ pair. Writes reverse-engineering/apple2/phantasie3/extracted/graphics/.
"""
from __future__ import annotations

import hashlib
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # `python3 -I` drops the script dir

from a2_applesoft import lines  # noqa: E402
from a2_rdos import RdosDisk  # noqa: E402
from a2_woz import load_image  # noqa: E402
from extract_a2_p2_graphics import HiresPage, ShapeTable, bbox, save_png  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
IMAGES = REPO / "reverse-engineering/apple2/original-images/phantasie3"
SIDE_A = IMAGES / "woz-original-v3.0/phantasie3-v3.0-disk1-side-a-boot.woz"
SIDE_B = IMAGES / "woz-original-v3.0/phantasie3-v3.0-disk1-side-b-utilities.woz"
OUT = REPO / "reverse-engineering/apple2/phantasie3/extracted/graphics"

# The [cr] copy's own screens (crack credits, not SSI's art): never drawn.
CRACK_SHA1 = {
    "ICE DRAGON": "49891f3766cef70d35f0ded9f89f10032f70a747",
    "WIN PIC": "d5e303bd929cd39d424d7d924fd61eab1287a98d",
}

# Apple terrain classes (STARTER line 120, TR$); shape 37 + class, HCOLOR INT(1 + byte / 20) = 1 + class // 2
# (OUTDOORS3 line 30). Classes 12-14 have no shape (the table has 48), and no map uses them.
TERRAINS = ["GRASS", "FOREST", "HILLS", "MOUNTAINS", "BUILDINGS", "DUNGEON", "PATHWAY", "BRIDGE", "LAVA", "DESERT",
            "WATER", "MIST"]
TILE_CLASS_BASE = 300  # overworld.tile.{300 + class}: a class's picture, the target of tile_swaps

# DOS map byte -> Apple terrain class, as [first, last, class] (GRAPHICS.md section 5). Scandor: the majority class of
# each byte in the Apple's OT1-OT25 laid over DOS PLANE0 (every byte with 2+ cells agrees with these ranges); bytes
# Scandor lacks: by the DOS terrain name (overworld.json terrain) and the Apple planes' own files [~].
DOS_TO_APPLE = [
    [0, 25, 10], [26, 38, 4], [39, 46, 5], [47, 54, 4], [55, 55, 5], [56, 58, 4], [59, 60, 5], [61, 67, 7],
    [68, 74, 11], [75, 77, 7], [78, 87, 11], [88, 89, 8], [90, 114, 3], [115, 154, 2], [155, 187, 9],
    [188, 205, 0], [206, 209, 6], [210, 213, 8], [214, 224, 6], [225, 241, 1], [242, 255, 10],
]
# The Netherworld (region 4) draws the DOS overrides' terrain (Black Path 61-77, Mist 78-82, Steam 83-87, Lava
# 88-241): the bytes whose class differs from DOS_TO_APPLE get a tile_swap.
NETHER_REGION = 4
NETHER = [[61, 77, 7], [78, 87, 11], [88, 241, 8]]

RACES = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC",
         "GOBLIN", "LIZARD MAN", "MINOTAUR", "UNDEAD"]  # TOWN3 line 270
BODIES = [(19, "human-elf-fighter"), (25, "small-races"), (26, "monster-races"), (27, "human-elf-robed"),
          (4, "high-social-class")]
POSES = [(20, "idle"), (22, "attacking"), (21, "parrying"), (5, "casting")]
DUNGEON_MARKS = [(31, "wall"), (32, "cells-185-199"), (33, "cells-200-214"), (34, "cells-215-217"),
                 (35, "cells-218-220"), (36, "cells-221-up"), (30, "party-cursor")]
EFFECTS = [(1, "arrow", 220), (2, "bow", 147), (3, "bow-fired", 147), (6, "body-down", 147), (23, "spell-sparkle", 161),
           (24, "spell-hit", 150), (47, "spell-hit-sp0", 150)]
VIEWS = [("PIC1", "a", "wilderness"), ("PIC2", "a", "strange-lands"), ("PIC3", "a", "dungeons"),
         ("OUTPIC", "b", "empty-map-box"), ("PIC", "b", "saved-dungeon-map")]
UNSEEN_GLYPH = 99  # DNGLD3 line 200: the dungeon map box starts filled white (HCOLOR 3)

MONSTER_COLUMN = 21  # X% = 21 + 4k (OUTDOORS3 / DUNGON3 1550): always an odd byte column
PARTY_X = 147        # Q = 19 * slot + 147 (9100), slot 0
TILE_X, TILE_ODD_X = 14, 7   # map cells at X = 7 + 7 * column (line 30): even / odd byte columns
DUNGEON_X = 3        # cells at 3 + 3X (DUNGPLOT / DUNGON3 5030), X = 0


def class_of(b: int, table=DOS_TO_APPLE) -> int | None:
    for lo, hi, c in table:
        if lo <= b <= hi:
            return c
    return None


def slug(name: str) -> str:
    return "".join(c if c.isalnum() else "-" for c in name.lower()).strip("-")


def party_body_shape(race: int, cls: int) -> int:
    """TOWN3 2350-2356 (RC, CL 0-based); a social class 3 member not of a small race gets 4 (2360)."""
    if race > 6:
        return 26
    if race not in (0, 2):
        return 25
    return 27 if cls == 1 or cls > 3 else 19


class Writer:
    def __init__(self, out: Path):
        self.out = out

    def rel(self, path: Path) -> str:
        return str(path.relative_to(self.out))

    def shape(self, table: ShapeTable, parts, x: int, scale: int, rel: str, cell=None) -> dict:
        """DRAW `parts` = [(shape, hcolor)] at screen x on a scratch page (pad 140 / 200 keeps the column phase), crop
        to the drawn box (or the cell from the origin) and save. origin = the DRAW point in the PNG."""
        px, py = 140, 200
        page = HiresPage(cols=80, rows=400)
        boxes = []
        for shape, hcolor in parts:
            vec = table.vectors(shape)
            page.draw(vec, px + x, py, hcolor, scale)
            if (b := bbox(vec, scale)) is not None:
                boxes.append(b)
        if cell or not boxes:
            w, h = cell or (1, 1)
            x0, y0, x1, y1 = px + x, py, px + x + w, py + h
        else:
            x0 = px + x + min(b[0] for b in boxes) - 1
            y0 = py + min(b[1] for b in boxes)
            x1 = px + x + max(b[2] for b in boxes) + 2
            y1 = py + max(b[3] for b in boxes) + 1
        size = save_png(page.to_rgb((x0, y0, x1, y1)), self.out / rel)
        return {"file": rel, "size": list(size), "origin": [px + x - x0, py - y0],
                "shapes": [p[0] for p in parts], "hcolors": [p[1] for p in parts], "scale": scale, "screen_x": x}

    def sprite(self, data: bytes, shape: int, rel: str) -> dict:
        """MONSTER PLOT: 3 bytes x 21 rows (42 for shape >= 21) from (shape - 1) * 64, at byte column 21."""
        rows = 42 if shape >= 21 else 21
        base = (shape - 1) * 64
        page = HiresPage(cols=40, rows=rows)
        for r in range(rows):
            for c in range(3):
                # the plot advances only the low byte of its source pointer: stays in the slot's 256-byte page
                src = (base & ~0xFF) | ((base + r * 3 + c) & 0xFF)
                page.mem[r][MONSTER_COLUMN + c] = data[src]
        x0 = MONSTER_COLUMN * 7
        size = save_png(page.to_rgb((x0, 0, x0 + 21, rows)), self.out / rel)
        return {"file": rel, "size": list(size), "shape": shape, "rows": rows, "byte_column": MONSTER_COLUMN}

    def page(self, data: bytes, rel: str) -> dict:
        save_png(HiresPage.from_apple(data).to_rgb(), self.out / rel, transparent_black=False)
        return {"file": rel, "size": [280, 192]}

    def panel(self, data: bytes, rel: str) -> dict:
        """PICPLOT: 96 rows of 12 bytes to x bytes 0-11, y 96-191."""
        page = HiresPage(cols=40, rows=96)
        for r in range(96):
            page.mem[r][0:12] = data[r * 12:(r + 1) * 12]
        save_png(page.to_rgb((0, 0, 84, 96)), self.out / rel, transparent_black=False)
        return {"file": rel, "size": [84, 96], "screen": [0, 96]}

    def block(self, rel: str, w: int, h: int, rgb) -> dict:
        from PIL import Image
        (self.out / rel).parent.mkdir(parents=True, exist_ok=True)
        Image.new("RGBA", (w, h), (*rgb, 255)).save(self.out / rel)
        return {"file": rel, "size": [w, h]}


def read_monsters(read) -> list[dict]:
    """MN30..MN39: 8 text records of 22 CR-ended fields (OUTDOORS3 line 910); S% is the last. '=' is a space; control
    characters are dropped (BEEPING BLOB's name rings the bell, CHR$(7))."""
    out = []
    for f in range(10):
        text = bytes(c & 0x7F for c in read(f"MN3{f}")).split(b"\r")
        for r in range(8):
            rec = text[r * 22:(r + 1) * 22]
            name = "".join(c for c in rec[0].decode("ascii") if c >= " ").replace("=", " ")
            out.append({"index": f * 8 + r, "name": name, "shape": int(rec[21])})
    return out


def boot_data(program: bytes) -> list[int]:
    """The numbers of the boot program's DATA lines 260-290, in order."""
    nums: list[int] = []
    for _, ln, text in lines(program):
        if 260 <= ln <= 290 and text.strip().startswith("DATA"):
            nums += [int(v) for v in text.strip()[4:].replace(" ", "").split(",") if v]
    return nums


def decode_win_pic(data: bytes) -> bytes:
    """WIN (v3.0) lines 83-85: ROUTINE 30000 with stride X = 1, 8, ... 253, then 30050 with the same strides."""
    mem = bytearray(data[:8192].ljust(8192, b"\0"))
    for step in range(1, 256, 7):  # 30000 ($7530): EOR every step-th byte with 41, 42, ... (8-bit)
        v, p = 0x40, 0
        while True:
            v = (v + 1) & 0xFF
            mem[p] ^= v
            p = (p + step) & 0x1FFF
            if p == 0:
                break
    for step in range(1, 256, 7):  # 30050 ($7562): v = ROR v (a 0 rotates on through the carry); EOR non-zero bytes
        v, p = 0x40, 0
        while True:
            carry = 0
            while True:
                v, carry = (v >> 1) | (carry << 7), v & 1
                if v:
                    break
            a = mem[p]
            if a and a ^ v:
                mem[p] = a ^ v
            p = (p + step) & 0x1FFF
            if p == 0:
                break
    return bytes(mem)


def font_glyph(font_file: bytes, char: int) -> bytes:
    """The 8 bytes IMPTEXT draws for character `char` ($A0-$DF): table $1800 + 8 (c & $3F)... loaded at $1828."""
    page = 0x18 if char < 0xC0 else 0x19
    addr = (page << 8) | ((char << 3) & 0xFF)
    off = addr - 0x1828
    return font_file[off:off + 8] if 0 <= off <= len(font_file) - 8 else bytes(8)


# The characters the font's glyphs show, by the code the game prints (GRAPHICS.md section 7): digits and letters as
# themselves; '=' is the game's space; '@' ':' ';' '<' '>' print ':' '-' '"' ',' '?' (COMMAND@, :PRESS=RETURN:,
# ;...;, WILDERNESS<, REDO>). '?' ($BF) draws ')' [~].
FONT_MAP = {**{c: chr(c & 0x7F) for c in range(0xB0, 0xBA)}, **{c: chr(c & 0x7F) for c in range(0xC1, 0xDB)},
            0xBD: " ", 0xC0: ":", 0xBA: "-", 0xBB: '"', 0xBC: ",", 0xBE: "?", 0xBF: ")"}


def font_json(font_file: bytes, version: str) -> dict:
    glyphs = {}
    for code, ch in sorted(FONT_MAP.items(), key=lambda kv: kv[1]):
        g = font_glyph(font_file, code)
        rows = ["".join("#" if (g[r] >> b) & 1 else "." for b in range(7)) if r < 7 else "......." for r in range(8)]
        glyphs[ch] = {"rows": rows, "advance": 7}
    return {"_schema": "phantasie.font/v1",
            "_note": f"Phantasie III Apple II hi-res text font (IMPTEXT, v{version}; tools/scripts/extract_a2_p3_graphics.py, "
                     "GRAPHICS.md section 7): 7 x 8 cells, the routine draws rows 0-6 (bit 0 = leftmost pixel). "
                     "Keyed by the character each glyph shows (the game prints '=' for a space, '@' for ':'...). No "
                     "'.', '!', '/', '(' or lower case: not the pack's font.",
            "id": "apple2-p3-hires", "height": 8, "line_height": 8, "space_advance": 7, "fallback": " ",
            "fold_to_upper": True, "glyphs": glyphs}


def main(argv: list[str]) -> None:
    a_path, b_path = (Path(argv[1]), Path(argv[2])) if len(argv) > 2 else (SIDE_A, SIDE_B)
    disk_a, disk_b = RdosDisk(load_image(a_path)), RdosDisk(load_image(b_path))
    names_a = {f.name for f in disk_a.files if not f.deleted}
    names_b = {f.name for f in disk_b.files if not f.deleted}
    intro = bytes(c & 0x7F for c in disk_a.read("INTRO3")).decode("ascii", "replace")
    version = "3.0" if "VERSION 3.0" in intro else "1.0" if "VERSION 1.0" in intro else "?"
    w = Writer(OUT)
    table = ShapeTable(disk_a.read("SHAPES"))
    index: dict = {
        "_source": f"Phantasie III Apple II v{version}, disk 1 sides A / B: SHAPES ($45DC, 48 shapes), "
                   "MONSTER SHAPES + NIKADEMUS (MONSTER PLOT sprites), SCREEN / ICE DRAGON / WIN PIC (hi-res pages), "
                   "PIC1-3 / OUTPIC / PIC (map-box panels), IMPTEXT (font), MN30..MN39 (S%). "
                   "tools/scripts/extract_a2_p3_graphics.py; notes reverse-engineering/apple2/phantasie3/GRAPHICS.md.",
        "version": version,
        "origin_note": "origin = where the DRAW point sits inside the PNG (x, y); screen_x = the in-game x the colour "
                       "phase was taken from.",
        "shapes": {}, "sprites": {}, "monsters": {}, "figures": {}, "portraits": {}, "tiles": {}, "tile_swaps": [],
        "dungeon": {}, "effects": {}, "screens": {}, "views": {}, "font": {}}

    # 1. every shape of SHAPES, white, scale 1, at x 0
    for n in range(1, table.count + 1):
        index["shapes"][n] = w.shape(table, [(n, 3)], 0, 1, f"shapes/{n:02d}.png")

    # 2. monster sprites: the 24 slots (+ NIKADEMUS over 21-24), then every monster's two poses
    sprites = disk_a.read("MONSTER SHAPES")
    nik = sprites[:0x500] + disk_b.read("NIKADEMUS") + sprites[0x600:]
    for s in [*range(1, 21), 21, 23]:
        index["sprites"][s] = w.sprite(sprites, s, f"sprites/{s:02d}.png")
    for s in (21, 23):
        index["sprites"][f"nikademus.{s}"] = w.sprite(nik, s, f"sprites/nikademus-{s:02d}.png")
    for m in read_monsters(disk_a.read):
        s = m["shape"]
        src = nik if m["index"] in (77, 78) else sprites
        name = f"{m['index']:02d}-{slug(m['name'])}.png"
        idle = w.sprite(src, s, f"monsters/idle/{name}")
        act = w.sprite(src, s + 1 + (s == 21), f"monsters/attacking/{name}")
        index["monsters"][m["index"]] = {"name": m["name"], "shape": s, "idle": idle, "attacking": act}

    # 3. party figures: body FN P(168) + arm 20 (stand), 22 (attack 1695), 21 (parry 9210), 5 (cast 1760), white, x 147
    for body, label in BODIES:
        for arm, pose in POSES:
            index["figures"][f"{body}.{pose}"] = w.shape(table, [(body, 3), (arm, 3)], PARTY_X, 1,
                                                         f"figures/{pose}/{body:02d}-{label}.png")
    for race, rname in enumerate(RACES):
        body = party_body_shape(race, 3)
        for arm, pose in POSES[:2]:
            e = w.shape(table, [(body, 3), (arm, 3)], PARTY_X, 1, f"portraits/{pose}/{race:02d}-{slug(rname)}.png")
            e["body"] = body
            index["portraits"][f"{race}.{pose}"] = e

    # 4. overworld: one 7 x 6 cell per Apple class (shape 37 + class, HCOLOR 1 + class // 2), even and odd columns;
    #    then the tiles by DOS map byte, and the Netherworld's swaps
    for c, tname in enumerate(TERRAINS):
        hc = 1 + c // 2
        index["tiles"][f"class.{c}"] = w.shape(table, [(37 + c, hc)], TILE_X, 1,
                                               f"overworld/tiles/{TILE_CLASS_BASE + c}.class-{slug(tname)}.png", (7, 6))
        index["tiles"][f"class.{c}.odd"] = w.shape(table, [(37 + c, hc)], TILE_ODD_X, 1,
                                                   f"overworld/odd/{c:02d}-{slug(tname)}.png", (7, 6))
    for b in range(256):
        c = class_of(b)
        index["tiles"][b] = w.shape(table, [(37 + c, 1 + c // 2)], TILE_X, 1,
                                    f"overworld/tiles/{b:03d}.{slug(TERRAINS[c])}.png", (7, 6))
        index["tiles"][b]["class"] = c
    for b in range(256):
        c = class_of(b, NETHER)
        if c is not None and c != class_of(b):
            index["tile_swaps"].append([NETHER_REGION, b, TILE_CLASS_BASE + c])
    # the party mark: XDRAW 43 over its cell (5175), here DRAW in white
    index["tiles"]["party"] = w.shape(table, [(43, 3)], TILE_X, 1, "overworld/party.png", (7, 6))

    # 5. dungeon map marks, 3 x 3 at 3 + 3X, 99 + 3Y (DUNGPLOT / DUNGON3 5030); glyph = shape number; 0 = erased
    #    (black), 99 = not yet seen (DNGLD3 200 fills the box white)
    for n, what in DUNGEON_MARKS:
        e = w.shape(table, [(n, 3)], DUNGEON_X, 1, f"dungeon/glyph-{n}.{what}.png", (3, 3))
        e["use"] = what
        index["dungeon"][n] = e
    index["dungeon"][0] = w.block("dungeon/glyph-0.blank.png", 3, 3, (0, 0, 0))
    index["dungeon"][UNSEEN_GLYPH] = w.block(f"dungeon/glyph-{UNSEEN_GLYPH}.unseen.png", 3, 3, (255, 255, 255))

    # 6. effects and loose shapes (XDRAW in the game; DRAW in white here)
    for n, what, x in EFFECTS:
        e = w.shape(table, [(n, 3)], x, 1, f"effects/{n:02d}-{what}.png")
        e["use"] = what
        index["effects"][n] = e

    # 7. screens
    index["screens"]["frame"] = w.page(disk_a.read("SCREEN"), "screens/frame.png")
    ice = disk_a.read("ICE DRAGON")
    if hashlib.sha1(ice).hexdigest() == CRACK_SHA1["ICE DRAGON"]:
        index["screens"]["_title"] = "ICE DRAGON is the [cr] copy's crack screen: no title picture."
    else:
        index["screens"]["title-picture"] = w.page(ice, "screens/title-picture.png")
        page = HiresPage.from_apple(ice)
        nums = boot_data(disk_a.read("PHANTASIE III"))
        for r in range(7):  # lines 245-250: 7 rows, POKE L1 - 6 for L1 = ML .. ML + 24 (page 1)
            row = nums[r * 26:(r + 1) * 26]
            for i, v in enumerate(row[1:]):
                page.poke_apple(row[0] - 6 + i - 8192, v)
        save_png(page.to_rgb(), OUT / "screens/title.png", transparent_black=False)
        index["screens"]["title"] = {"file": "screens/title.png", "size": [280, 192],
                                     "note": "ICE DRAGON + the boot program's DATA rows (lines 245-290)."}
    win = disk_b.read("WIN PIC")
    if "ROUTINE" in names_b and hashlib.sha1(win).hexdigest() != CRACK_SHA1["WIN PIC"]:
        index["screens"]["ending"] = w.page(decode_win_pic(win), "screens/ending.png")
        index["screens"]["ending"]["note"] = "WIN PIC after ROUTINE 30000 / 30050 (WIN lines 83-85)."
    else:
        index["screens"]["_ending"] = "WIN PIC is the [cr] copy's crack screen (its WIN is a stub): no ending picture."

    # 8. the map-box panels
    for name, side, what in VIEWS:
        disk = disk_a if side == "a" else disk_b
        if name in (names_a if side == "a" else names_b):
            index["views"][slug(what)] = w.panel(disk.read(name), f"views/{name.lower()}-{what}.png")
            index["views"][slug(what)]["source"] = name

    # 9. the hi-res font (PNG sheet + phantasie.font/v1 JSON; not the pack's font)
    for name, label in (("IMPTEXT", "imptext"), ("IMPREVERSE", "impreverse")):
        data = disk_a.read(name)
        page = HiresPage(cols=40, rows=16)
        for i, code in enumerate(range(0xA0, 0xE0)):
            g = font_glyph(data, code)
            for r in range(8):
                page.mem[(i // 32) * 8 + r][i % 32] = g[r] & 0x7F  # bit 7 = colour set: drawn as white sheet
        save_png(page.to_rgb((0, 0, 224, 16)), OUT / f"font/{label}.png", transparent_black=False)
        index["font"][label] = {"file": f"font/{label}.png", "size": [224, 16],
                                "note": "characters $A0-$DF, 32 per row"}
    (OUT / "font").mkdir(parents=True, exist_ok=True)
    (OUT / "font/apple2-p3-hires.json").write_text(json.dumps(font_json(disk_a.read("IMPTEXT"), version), indent=1) + "\n")
    index["font"]["json"] = {"file": "font/apple2-p3-hires.json"}

    (OUT / "extraction-index.json").write_text(json.dumps(index, indent=1) + "\n")
    print(f"v{version}: {table.count} shapes, {len(index['monsters'])} monsters, "
          f"{len(index['tile_swaps'])} tile swaps -> {OUT.relative_to(REPO)}")


if __name__ == "__main__":
    main(sys.argv)
