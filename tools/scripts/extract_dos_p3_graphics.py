#!/usr/bin/env python3
"""Extract Phantasie III (DOS, SSI 1988) CGA art to PNG: the III CGA pack (s160).

Inputs: reverse-engineering/dos/phantasie3/unpacked/ssi-1988/ (TOWN.CMP, PICS.CMP, WORLD.CGA, SHAPES.CGA, MONSTERS.CGA,
CREATURE.DAT) and the shipped GameData/phantasie3/rule-tables.json (core_race: the portrait file names).
Output: reverse-engineering/dos/phantasie3/extracted/graphics/ (PNGs + extraction-index.json), the root of
GameData/phantasie3/graphics/dos-cga.json. The importer's P3Dos/P3CgaArt.cs writes the same pixels.
Formats: reverse-engineering/dos/phantasie3/FORMAT.md section 10. Run from anywhere: ``python3 -I <this file>``.

* Every picture is 2 bits a pixel, high bits first, rows one after another, shown in CGA palette 0 high.
* ``.cmp``: byte 0 picks the unpacker (2 = column order); runs: c 1..127 literal, c >= 128 the next byte 256 - c
  times, c = 0 a u16 count then the byte; 16,000 output bytes (80 a row).
* MONSTERS.CGA: picture index = byte 27 (- 1 above 27); < 41 normal (768 B, two 32x48 frames), 41..46 giant
  (2,880 B at 0x7b00, two 80x72 frames), else normal picture 15.
"""
from __future__ import annotations

import json
import os
import shutil
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parents[2]
SRC = REPO / "reverse-engineering/dos/phantasie3/unpacked/ssi-1988"
OUT = REPO / "reverse-engineering/dos/phantasie3/extracted/graphics"
RULE_TABLES = REPO / "src/OpenPhantasie.Data/GameData/phantasie3/rule-tables.json"

# CGA palette 0, high intensity (DOSBox colours, as Phantasie I's CGA pack).
PALETTE = [(0, 0, 0), (0x55, 0xFF, 0x55), (0xFF, 0x55, 0x55), (0xFF, 0xFF, 0x55)]

# The remaster's Race enum (Phantasie I's order), by core_race name (as extract_amiga_p3_graphics.py).
CORE_RACES = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD",
              "ORC", "GOBLIN", "LIZARDMAN", "MINOTAUR", "UNDEAD"]
# III race byte -> first picture of its pair (DS:0x9ea, the Amiga's 0x239a0); entry 18 = the grave.
RACE_PICTURE = [0, 2, 4, 6, 6, 0, 14, 16, 16, 10, 12, 14, 16, 8, 8, 20, 22, 18, 24]

PICS = [("title", "the title (ovl1 0x7f7)"), ("astral-plane", "the party defeat (root 0x1edd)"),
        ("reward-good", "Zeus (ovl4 0xae)"), ("reward-evil", "Pluto (ovl4 0xc9)")]

# SHAPES.CGA blocks.
SHAPE_SPARKLE, SHAPE_GLYPHS8, SHAPE_PARTY, SHAPE_DUNGEON, SHAPE_SCANDOR, SHAPE_BODY = \
    0x0000, 0x01B0, 0x02A0, 0x2820, 0x2890, 0x3C40
# Dungeon glyph number -> slot (ovl4 0x5a8, jump table 0x5f7).
DUNGEON_SLOT = {46: 1, 47: 2, 48: 3, 49: 6, 50: 5, 51: 4, 52: 0}
# Body figure (root 0xdcf): (stored piece, x, y, area or None), in draw order; set s of piece p at s*192 + p*16.
BODY_PIECES = [(4, 8, 0, "head"), (1, 0, 8, "right-arm"), (5, 8, 8, None), (9, 16, 8, "left-arm"),
               (2, 0, 16, "right-leg"), (6, 8, 16, "torso"), (10, 16, 16, "left-leg"), (3, 0, 24, "right-leg"),
               (11, 16, 24, "left-leg")]
BODY_STATES = 4


def cga(data: bytes, width: int, height: int, clear_zero: bool = False) -> Image.Image:
    """clear_zero: colour 0 left transparent (an OR blit, the flash root 0x1537), the rest opaque."""
    per_row = width // 4
    if len(data) < per_row * height:
        raise ValueError(f"{len(data)} bytes for {width}x{height}")
    im = Image.new("RGBA" if clear_zero else "RGB", (width, height))
    px = im.load()
    for y in range(height):
        for x in range(width):
            b = data[y * per_row + x // 4]
            c = (b >> (6 - 2 * (x % 4))) & 3
            px[x, y] = (PALETTE[c] + ((255 if c else 0),)) if clear_zero else PALETTE[c]
    return im


def unpack(src: bytes, at: int) -> tuple[bytes, int]:
    """One packed screen from ``at``; returns (16,000 bytes, offset after it)."""
    method = src[at]
    if method not in (1, 2):
        raise ValueError(f"packed screen at {at}: unpacker {method}")
    i = at + 1
    out = bytearray(16000)
    di, left, rows = 0, 16000, 200

    def put(v: int) -> None:
        nonlocal di, left, rows
        out[di] = v
        left -= 1
        if method == 2:
            di += 80
            rows -= 1
            if rows == 0:
                rows = 200
                di -= 16000 - 1
        else:
            di += 1

    while left > 0:
        c = src[i]
        if c == 0:
            n, i, run = src[i + 1] | (src[i + 2] << 8), i + 3, True
        elif c >= 0x80:
            n, i, run = 256 - c, i + 1, True
        else:
            n, i, run = c, i + 1, False
        if run:
            v = src[i]
            i += 1
            for _ in range(n):
                if left == 0:
                    break
                put(v)
        else:
            for _ in range(n):
                if left == 0:
                    break
                put(src[i])
                i += 1
    return bytes(out), i


def slug(name: str) -> str:
    return "".join(c if c.isascii() and c.isalnum() else "-" for c in name.strip().lower()).strip("-")


def monster_picture(byte27: int) -> tuple[int, bool]:
    """(offset, giant) of a monster's picture in MONSTERS.CGA (root 0x10b8b -> 0x7e89)."""
    p = byte27 - 1 if byte27 > 27 else byte27
    if 41 <= p <= 46:
        return 0x7B00 + 2880 * (p - 41), True
    if p < 0 or p > 46:
        p = 15
    return 768 * p, False


# D3-067 (user s126 / s161): monsters whose picture DOS gets wrong borrow an analogous creature's, as the Amiga pack
# does. Byte 27 -> (note, party picture?, picture index): Wolf the Wild Dog's, Mud Elemental the party elemental's,
# Troll Baby the party Troll's (DOS: the Skeleton's / the Forest Elf's).
BORROWED_PICTURE = {
    65: ("Wild Dog's picture (byte 27 = 0)", False, 0),
    48: ("the party elemental's picture (SHAPES.CGA party picture 22)", True, RACE_PICTURE[16]),
    27: ("the party Troll's picture (SHAPES.CGA party picture 20)", True, RACE_PICTURE[15]),
}


def main() -> None:
    if OUT.exists():
        shutil.rmtree(OUT)
    OUT.mkdir(parents=True)
    index: dict[str, dict] = {}

    def save(im: Image.Image, rel: str, **meta) -> None:
        path = OUT / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        im.save(path)
        index[rel] = meta

    # Screens.
    town, _ = unpack((SRC / "TOWN.CMP").read_bytes(), 0)
    save(cga(town, 320, 200), "screens/town.png", source="TOWN.CMP", note="the town street, every town (root 0x0)")
    pics = (SRC / "PICS.CMP").read_bytes()
    at = 0
    for name, note in PICS:
        screen, end = unpack(pics, at)
        save(cga(screen, 320, 200), f"screens/{name}.png", source="PICS.CMP", offset=at, note=note)
        at = end
    if at != len(pics):
        raise ValueError(f"PICS.CMP: {len(pics) - at} bytes after the fourth screen")

    # Overworld tiles.
    world = (SRC / "WORLD.CGA").read_bytes()
    for t in range(250):
        save(cga(world[64 * t:64 * t + 64], 16, 16), f"overworld/tiles/{t:03d}.png", tile=t)

    shapes = (SRC / "SHAPES.CGA").read_bytes()
    if len(shapes) != 16192:
        raise ValueError(f"SHAPES.CGA: {len(shapes)} bytes")

    # Party pictures by the remaster's race id; III's 16 is the elemental; the grave.
    core_race = json.loads(RULE_TABLES.read_text())["core_race"]
    for race in range(18):
        p = RACE_PICTURE[race]
        if race == 16:
            stem = "elemental"
        elif str(race) in core_race:
            stem = f"{CORE_RACES.index(core_race[str(race)]):02d}-{slug(core_race[str(race)])}"
        else:
            continue
        for frame, kind in ((0, "idle"), (1, "attacking")):
            off = SHAPE_PARTY + 384 * (p + frame)
            save(cga(shapes[off:off + 384], 32, 48), f"portraits/{kind}/{stem}.png", p3_race=race, picture=p + frame)
    save(cga(shapes[SHAPE_PARTY + 384 * 24:SHAPE_PARTY + 384 * 25], 32, 48), "portraits/grave.png", picture=24)

    # Dungeon map glyphs (8x5), the Scandor map (Scroll IV), the body figure pieces.
    for g, slot in DUNGEON_SLOT.items():
        off = SHAPE_DUNGEON + 16 * slot
        save(cga(shapes[off:off + 10], 8, 5), f"dungeon/glyph-{g}.png", glyph=g, slot=slot)
    save(cga(shapes[SHAPE_SCANDOR:SHAPE_SCANDOR + 5040], 120, 168), "scroll/scandor.png",
         note="Scroll IV (item 126), root 0x124")
    for state in range(BODY_STATES):
        for i, (piece, x, y, area) in enumerate(BODY_PIECES):
            off = SHAPE_BODY + 192 * state + 16 * piece
            save(cga(shapes[off:off + 16], 8, 8), f"body/{9 * state + i}.png", piece=piece, state=state, area=area)
    # The flash (root 0x1537, an OR blit: colour 0 transparent) and the text page's frame (root 0x4a3 / 0x636);
    # FORMAT.md section 10.
    for k in range(3):
        off = SHAPE_SPARKLE + 144 * k
        save(cga(shapes[off:off + 144], 24, 24, clear_zero=True), f"shapes/frame24-{k}.png",
             note="the flash's frame k (root 0x1537), colour 0 transparent")
    for k in range(15):
        off = SHAPE_GLYPHS8 + 16 * k
        save(cga(shapes[off:off + 16], 8, 8), f"shapes/glyph8-{k:02d}.png", note="the text page's frame glyph k (root 0x4a3)")

    # Monsters, by monster id (CREATURE.DAT: 80 records of 38 B, name in the first 14, byte 27 the picture).
    creature = (SRC / "CREATURE.DAT").read_bytes()
    monsters = (SRC / "MONSTERS.CGA").read_bytes()
    if len(monsters) != 48768:
        raise ValueError(f"MONSTERS.CGA: {len(monsters)} bytes")
    for mid in range(80):
        rec = creature[38 * mid:38 * mid + 38]
        name = rec[:14].split(b"\0")[0].decode("ascii").strip()
        b27 = rec[27]
        if b27 in BORROWED_PICTURE:
            note, party, p = BORROWED_PICTURE[b27]
            if party:
                for frame, kind in ((0, "idle"), (1, "attacking")):
                    off = SHAPE_PARTY + 384 * (p + frame)
                    save(cga(shapes[off:off + 384], 32, 48), f"monsters/{kind}/{mid:02d}-{slug(name)}.png",
                         name=name, byte27=b27, borrowed=note, picture=p + frame)
                continue
        off, giant = monster_picture(BORROWED_PICTURE[b27][2] if b27 in BORROWED_PICTURE else b27)
        w, h = (80, 72) if giant else (32, 48)
        size = w * h // 4
        for frame, kind in ((0, "idle"), (1, "attacking")):
            start = off + frame * size
            save(cga(monsters[start:start + size], w, h), f"monsters/{kind}/{mid:02d}-{slug(name)}.png",
                 name=name, byte27=b27, offset=start, giant=giant,
                 **({"borrowed": BORROWED_PICTURE[b27][0]} if b27 in BORROWED_PICTURE else {}))

    (OUT / "extraction-index.json").write_text(json.dumps(
        {"_source": "tools/scripts/extract_dos_p3_graphics.py; FORMAT.md section 10",
         "palette": ["#%02X%02X%02X" % c for c in PALETTE], "files": index}, indent=1) + "\n")
    print(f"{len(index)} PNG -> {OUT.relative_to(REPO)}")


if __name__ == "__main__":
    main()
