#!/usr/bin/env python3
"""Extract Phantasie III (Amiga v1.0, SSI 1987) art to PNG (Phase 09 T4, 4c).

Inputs: reverse-engineering/amiga/phantasie3/unpacked/PhantasieIII/ (Graphics/, D/, monsinfo.dat) and, for the map
previews only, the DOS WORLD.DAT / PLNS.DAT the remaster plays (reverse-engineering/dos/phantasie3/unpacked/ssi-1988/).
Output: reverse-engineering/amiga/phantasie3/extracted/graphics/ (PNGs + extraction-index.json + pack.json).
Format notes: reverse-engineering/amiga/phantasie3/FORMAT.md. Run from anywhere: ``python3 -I <this file>``.

Program addresses are offsets in the flat image of ``PhantasieIII`` (extract_amiga_p3_common.load_flat: CODE at 0,
DATA at 0x21890).

* ``.cmp`` / ``.csh`` / ``D/s6``: 32-B palette (16 words), u16 packed length, packed data (loader 0x123c). The packed
  data's first byte picks the unpacker (0x1208): 1 -> 0x2b72 (rows in screen order), else -> 0x2bc6 (each of the 4
  planes in turn, column by column: 200 bytes down one 8-pixel column, then the next column). The run code is the
  same in both: c = 0 -> u16 count + 1 byte repeated; c > 0 -> c literal bytes; c < 0 -> -c copies of the next byte.
  The result is a 320x200 screen, 4 sequential planes of 8000 B.
* ``.csh`` = a .cmp screen holding up to 15 shapes (0x293e): shape k sits in a 48x48 grid cell (5 per row) at
  (48 (k mod 5), 48 (k div 5) + 1); its size is in screen row 0: byte (k div 4)*2 of plane (k mod 4) = rows - 1,
  the next byte = width in bytes.
* ``phantasy.set`` (0x27d8): 3 bytes (0xfa, rows 75, columns 50), 32-B palette, the 50x75 Scandor map (3750 B), then
  250 tiles of 16x16 (128 B each: 16 rows of 4 plane words, interleaved) = 32000 B. The overworld draws a 9x9 window of
  tiles (0x9b92); tile id = map byte (0x3834, ids clamped to 250); the party is tile 89 OR-ed over the cell's tile
  (0x378a: colour 0 transparent).
* Palettes: words 0x0RGB with 3 bits per gun; 0x11ae loads ``(w & 0x777) << 1`` into the colour registers, so the art is
  ST-style (even Amiga nibbles only). PNG colours use the same Amiga->ST nibble table as Phantasie I's Amiga pack
  (amiga_gfx_common.AMIGA_TO_ST: 3-bit c -> round(c * 255 / 7)).
"""
from __future__ import annotations

import functools
import json
import os
import shutil
import struct
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from amiga_gfx_common import AMIGA_TO_ST  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
P3A = REPO / "reverse-engineering" / "amiga" / "phantasie3"
SRC = P3A / "unpacked" / "PhantasieIII"
OUT = P3A / "extracted" / "graphics"
DOS = REPO / "reverse-engineering" / "dos" / "phantasie3" / "unpacked" / "ssi-1988"
PACK_COPY = REPO / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie3" / "graphics" / "amiga.json"

TILE_COUNT = 250
PARTY_TILE = 89

# Sheet order of the encounter pictures (pointer table DATA 0x220a6, loaded packed by 0x7f3c).
SHEETS = ["heros", "purple1", "yellow1", "purple2", "yellow2", "giant1", "giant2"]

# Race -> first picture of its pair on heros.cmp (byte table 0x239a0, read by 0x1b2b2 with the record's race byte).
RACE_PICTURE = [0, 2, 4, 6, 6, 0, 14, 16, 16, 10, 12, 14, 16, 8, 8, 20, 22, 18, 24]

# dng.csh shapes load into slots 45.. (0x74d6); the dungeon map (0x1e47a -> 0x1e518 -> 0x1e750) uses slots 45..53.
DUNGEON_FIRST_SLOT = 45

RULE_TABLES = REPO / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie3" / "rule-tables.json"
# The remaster's Race enum (src/OpenPhantasie.Core/CharacterStats.cs, Phantasie I's order), by core_race name.
CORE_RACES = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC",
              "GOBLIN", "LIZARDMAN", "MINOTAUR", "UNDEAD"]

# Body-area figure (0x9694): shapes 16 + 7 * state + piece, states Okay / Injured / Broken (Gone clears instead).
BODY_FIRST_SLOT, BODY_LAST_SLOT = 16, 36
# Text-scroll frame (0x1e2c2): scroll.csh slots 54..68, blitted 8 px wide by 0x1e838.
SCROLL_FIRST_SLOT, SCROLL_LAST_SLOT = 54, 68
# The flash (0x1b814): anatomy2.csh slots 37..39, the sparkle's three frames.
FLASH_SLOTS = (37, 38, 39)
# dlgbttns.cmp (cut by 0x8012 into slots 1..10): 5 buttons of 64x18, normal at y 0, second state at y 19.
BUTTONS = ["ok", "cancel", "more", "yes", "no"]


# --- decoding --------------------------------------------------------------------------------------------------------

def palette(words: bytes) -> list[tuple[int, int, int]]:
    out = []
    for w in struct.unpack(">16H", words):
        w = (w & 0x777) << 1  # 0x11ae
        out.append((AMIGA_TO_ST[w >> 8 & 0xF], AMIGA_TO_ST[w >> 4 & 0xF], AMIGA_TO_ST[w & 0xF]))
    return out


def _runs(d: bytes, p: int, sink, limit: int) -> int:
    n = 0
    while n < limit:
        c = d[p]
        p += 1
        if c == 0:
            count, run = d[p] << 8 | d[p + 1], True
            p += 2
        elif c < 0x80:
            count, run = c, False
        else:
            count, run = 0x100 - c, True
        if run:
            b = d[p]
            p += 1
            for _ in range(count):
                sink(b)
            n += count
        else:
            for _ in range(count):
                sink(d[p])
                p += 1
            n += count
    return p


def unpack_screen(packed: bytes) -> bytes:
    """0x1208: a 320x200x4 screen (4 sequential planes of 8000 B) from a .cmp body."""
    if packed[0] == 1:  # 0x2b72: screen order
        buf = bytearray()
        p = _runs(packed, 1, buf.append, 32000)
        screen = bytes(buf[:32000])
    else:  # 0x2bc6: plane by plane, 8-pixel column by column
        out = bytearray(32000)
        p = 1
        for plane in range(4):
            k = [0]

            def sink(b, plane=plane, k=k):
                col, row = divmod(k[0], 200)
                if k[0] < 8000:
                    out[plane * 8000 + row * 40 + col] = b
                k[0] += 1

            p = _runs(packed, p, sink, 8000)
        screen = bytes(out)
    if p != len(packed):
        raise ValueError(f"packed length {len(packed)} but the unpacker used {p}")
    return screen


def read_cmp(path: Path) -> tuple[list, bytes, list[int]]:
    d = path.read_bytes()
    n = struct.unpack_from(">H", d, 32)[0]
    return palette(d[:32]), unpack_screen(d[34:34 + n]), struct.unpack(">16H", d[:32])


def screen_image(screen: bytes, pal) -> Image.Image:
    im = Image.new("RGB", (320, 200))
    px = im.load()
    for y in range(200):
        for xb in range(40):
            planes = [screen[pl * 8000 + y * 40 + xb] for pl in range(4)]
            for bit in range(8):
                v = sum(((planes[pl] >> (7 - bit)) & 1) << pl for pl in range(4))
                px[xb * 8 + bit, y] = pal[v]
    return im


@functools.lru_cache(maxsize=16)
def screen_indices(screen: bytes) -> list[list[int]]:
    rows = []
    for y in range(200):
        row = []
        for xb in range(40):
            planes = [screen[pl * 8000 + y * 40 + xb] for pl in range(4)]
            for bit in range(8):
                row.append(sum(((planes[pl] >> (7 - bit)) & 1) << pl for pl in range(4)))
        rows.append(row)
    return rows


def indexed_crop(screen: bytes, box: tuple[int, int, int, int], pal, transparent0: bool = False) -> Image.Image:
    """A part of a screen in palette `pal` (RGBA; colour 0 transparent when asked)."""
    x0, y0, x1, y1 = box
    rows = screen_indices(screen)
    im = Image.new("RGBA", (x1 - x0, y1 - y0))
    px = im.load()
    for y in range(y0, y1):
        for x in range(x0, x1):
            v = rows[y][x]
            px[x - x0, y - y0] = (0, 0, 0, 0) if transparent0 and v == 0 else (*pal[v], 255)
    return im


def csh_shapes(screen: bytes) -> list[tuple[int, int, int, int, int]]:
    """0x293e: (k, x, y, width px, rows) of each non-empty shape k < 15."""
    out = []
    for k in range(15):
        off = (k % 4) * 8000 + (k >> 2) * 2
        rows, wbytes = screen[off] + 1, screen[off + 1]
        if rows > 1 and 0 < wbytes < 0x80:
            out.append((k, (k % 5) * 48, (k // 5) * 48 + 1, wbytes * 8, rows))
    return out


def tile_image(tiles: bytes, i: int, pal, transparent0: bool = False) -> Image.Image:
    im = Image.new("RGBA", (16, 16))
    px = im.load()
    t = tiles[i * 128:(i + 1) * 128]
    for r in range(16):
        ws = struct.unpack_from(">4H", t, r * 8)
        for x in range(16):
            v = sum(((ws[pl] >> (15 - x)) & 1) << pl for pl in range(4))
            px[x, r] = (0, 0, 0, 0) if transparent0 and v == 0 else (*pal[v], 255)
    return im


# --- output ----------------------------------------------------------------------------------------------------------

def save(im: Image.Image, rel: str, index: dict, **meta) -> None:
    path = OUT / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path)
    index[rel] = meta


def slug(name: str) -> str:
    return "".join(c if c.isalnum() else "-" for c in name.strip().lower()).strip("-")


def monster_records() -> list[tuple[str, int]]:
    d = (SRC / "monsinfo.dat").read_bytes()
    return [(d[i * 38:i * 38 + 14].split(b"\0")[0].decode("ascii").strip(), d[i * 38 + 27]) for i in range(80)]


def monster_cell(picture: int) -> tuple[str, int, bool] | None:
    """0x1b4c8: record byte 27 -> (sheet, picture on the sheet, giant), or None (>= 48: nothing drawn)."""
    if picture < 15:
        return "yellow1", picture, False
    if picture < 28:
        p = picture - 15
        return ("heros", 10, False) if p == 12 else ("purple1", p, False)
    if picture < 36:
        return "purple2", picture - 28, False
    if picture < 42:
        return "yellow2", picture - 36, False
    if picture < 44:
        return "giant1", picture - 42, True
    if picture < 48:
        return "giant2", picture - 44, True
    return None


# Remaster fix (D3-041): byte 27 >= 48 has no picture on the Amiga (a stale picture or a hang, FORMAT.md 6 / 8.8), so
# these two borrow the game's own art: Wolf (65) the Wild Dog's (byte 0), Mud Elemental (48) the summoned elemental's
# (heros picture 22, RACE_PICTURE[16]). Only the picture: byte 27 still makes them giants in Core.
BORROWED_PICTURE = {
    65: ("Wild Dog's picture (byte 27 = 0)", ("yellow1", 0, False)),
    48: ("the party elemental's picture (heros cell 22 = frame-box picture 11)", ("heros", 11, False)),
}


def frame_box(p: int, frame: int, giant: bool) -> tuple[int, int, int, int]:
    """0x1b5f2 (32x48, 10 per row) / 0x1b654 (giants, 80x72, 4 per row); frame 0 idle, 1 attacking."""
    i = 2 * p
    if giant:
        x, y = ((i % 4) + frame) * 80, (i // 4) * 72
        return x, y, x + 80, y + 72
    x, y = ((i % 10) + frame) * 32, (i // 10) * 48
    return x, y, x + 32, y + 48


def main() -> None:
    if OUT.exists():
        shutil.rmtree(OUT)
    OUT.mkdir(parents=True)
    index: dict = {}
    palettes: dict = {}

    def hexes(pal):
        return ["#%02X%02X%02X" % c for c in pal]

    # Full screens.
    screens = {
        "title": ("Graphics/title.cmp", "title picture (0x73fe)"),
        "w0": ("D/w0.cmp", "D/WX.cmp (string 35), X = 0: the reward scene for an evil win, Pluto (0x1ee82, outcome 3)"),
        "w1": ("D/w1.cmp", "D/WX.cmp, X = 1: the reward scene for a good win, Zeus (0x1ee82, outcome 2)"),
        "w2": ("D/w2.cmp", "D/WX.cmp, X = 2: the party's defeat, the gates of the Astral Plane (0x1dc16 / 0x1dc70)"),
        "w3": ("D/w3.cmp", "town shop-front pieces, loaded at start (0x7788) [~] use"),
        "w4": ("D/w4.cmp", "town picture, loaded at start (0x7776)"),
        "scroll-iv": ("D/s6", "Scroll IV's picture (D/s6 loaded and unpacked by 0x1f7ee)"),
    }
    sheets: dict = {}
    for name, (src, note) in screens.items():
        pal, scr, words = read_cmp(SRC / src)
        palettes[name] = hexes(pal)
        save(screen_image(scr, pal), f"screens/{name}.png", index, source=src, note=note)
    for name in SHEETS + ["dlgbttns"]:
        pal, scr, words = read_cmp(SRC / "Graphics" / f"{name}.cmp")
        palettes[name] = hexes(pal)
        sheets[name] = scr
        save(screen_image(scr, pal), f"sheets/{name}.png", index, source=f"Graphics/{name}.cmp",
             note="whole sheet in its own file palette")

    # Encounter and party pictures: the combat screen (0x198a2) draws both monster groups, then loads the heros.cmp
    # palette (0x19906 -> 0x2445e, saved by 0x7f76): every picture shows in that palette.
    combat_pal = palette(bytes.fromhex("".join("%04x" % w for w in read_cmp(SRC / "Graphics/heros.cmp")[2])))
    palettes["combat"] = hexes(combat_pal)
    sheet_img = {name: screen_image(scr, combat_pal) for name, scr in sheets.items() if name in SHEETS}

    pictured, unpictured = {}, {}
    for mid, (name, pic) in enumerate(monster_records()):
        cell = monster_cell(pic)
        borrowed = None
        if cell is None:
            unpictured[mid] = {"name": name, "byte27": pic}
            if pic not in BORROWED_PICTURE:
                continue
            borrowed, cell = BORROWED_PICTURE[pic]
        sheet, p, giant = cell
        for frame, kind in ((0, "idle"), (1, "attacking")):
            save(sheet_img[sheet].crop(frame_box(p, frame, giant)), f"monsters/{kind}/{mid:02d}-{slug(name)}.png",
                 index, monster=mid, byte27=pic, sheet=sheet, picture=p, giant=giant,
                 **({"borrowed": borrowed} if borrowed else {}))
        pictured[mid] = {"name": name, "byte27": pic, "sheet": sheet, "picture": p, "giant": giant,
                         **({"borrowed": borrowed} if borrowed else {})}

    # Portraits are filed by the remaster's race id (Core Race, Phantasie I's order: the Client asks portrait.<Race>),
    # mapped from III's race byte through rule-tables.json core_race. III's 5 (the "Random" choice) has no character;
    # 16 (Elemental, set for party slot 7 by the fight set-up 0x14fb6) is portrait.elemental.
    core_race = json.loads(RULE_TABLES.read_text())["core_race"]
    for race, p in enumerate(RACE_PICTURE[:18]):
        if race == 16:
            stem = "elemental"
        elif str(race) in core_race:
            core = CORE_RACES.index(core_race[str(race)])
            stem = f"{core:02d}-{slug(core_race[str(race)])}"
        else:
            continue
        for frame, kind in ((0, "idle"), (1, "attacking")):
            x, y = ((p % 10) + frame) * 32, (p // 10) * 48
            save(sheet_img["heros"].crop((x, y, x + 32, y + 48)), f"portraits/{kind}/{stem}.png",
                 index, p3_race=race, picture=p)

    # The grave (heros sheet x 128, y 96): sprite 16 of 0x1b2b2, drawn for a member DESTROYED in the party-defeat
    # scene (race byte 22 -> sprite 16 at 0x1b6be). portrait.grave.
    save(sheet_img["heros"].crop((128, 96, 160, 144)), "portraits/grave.png", index, sprite=16, sheet="heros")

    # Shapes (.csh), each in its file's palette.
    csh_slots = {"anatomy1": 16, "anatomy2": 31, "dng": 45, "scroll": 54}
    # The body figure is drawn on the main screen, whose palette is the overworld's = anatomy1.csh's (0x24010); anatomy2's
    # own palette differs in registers 11, 12 and 14 and is not shown.
    body_pal = read_cmp(SRC / "Graphics" / "anatomy1.csh")[0]
    dng_pal = read_cmp(SRC / "Graphics" / "dng.csh")[0]
    for name, first in csh_slots.items():
        pal, scr, _ = read_cmp(SRC / "Graphics" / f"{name}.csh")
        palettes[name] = hexes(pal)
        full = screen_image(scr, pal)
        save(full, f"sheets/{name}.png", index, source=f"Graphics/{name}.csh")
        for k, x, y, w, h in csh_shapes(scr):
            if name == "dng":
                # 0x1e838 blits only the first byte of each plane word: the glyph is the shape's left 8 pixels.
                w = 8
            save(full.crop((x, y, x + w, y + h)), f"shapes/{name}/{first + k:02d}.png", index,
                 source=f"Graphics/{name}.csh", shape=k, slot=first + k)
            slot = first + k
            if name.startswith("anatomy") and BODY_FIRST_SLOT <= slot <= BODY_LAST_SLOT:
                # 0x2ce4 is a masked blit (mask = OR of the planes): colour 0 is transparent.
                save(indexed_crop(scr, (x, y, x + w, y + h), body_pal, transparent0=True), f"body/{slot}.png", index,
                     source=f"Graphics/{name}.csh", shape=k, slot=slot,
                     note="body-area figure piece (0x9694), main-screen palette, colour 0 transparent")
            if name == "anatomy2" and slot in FLASH_SLOTS:
                # The flash 0x1b814 (FORMAT.md 2.4): shapes 37-39 blitted masked (0x2ce4, colour 0 transparent). Cut in
                # the dungeon palette 0x244de (= dng.csh's, copied at 0x74fa): the one in force during the gods' reward
                # scene's intro (0x1da5c sets it at 0x1db8e; 0x1ee82 sets none before the flashes) [C] s109.
                save(indexed_crop(scr, (x, y, x + w, y + h), dng_pal, transparent0=True), f"flash/{slot}.png", index,
                     source="Graphics/anatomy2.csh", shape=k, slot=slot,
                     note="flash sparkle frame (0x1b814), dungeon palette 0x244de, colour 0 transparent")
                # The same sparkle as a combat spell effect (0x1833e / 0x17e16 with their flash flag set): the combat
                # screen loads the heros.cmp palette (0x19906), so registers 12-15 show red, orange, yellow, white.
                save(indexed_crop(scr, (x, y, x + w, y + h), combat_pal, transparent0=True), f"flash/combat/{slot}.png",
                     index, source="Graphics/anatomy2.csh", shape=k, slot=slot,
                     note="flash sparkle frame (0x1b814) in combat, heros.cmp palette (0x19906), colour 0 transparent")
            if name == "scroll" and SCROLL_FIRST_SLOT <= slot <= SCROLL_LAST_SLOT:
                # 0x1e838 again: only the left 8 pixels of each frame piece are drawn.
                save(full.crop((x, y, x + 8, y + h)), f"scroll/{slot}.png", index, source="Graphics/scroll.csh",
                     shape=k, slot=slot, note="text-scroll frame piece (0x1e2c2), left 8 px (0x1e838)")

    # Dialog buttons: 0x8012 cuts slots 1..10 at (64 i, 0) and (64 i, 19), 64x18, slot 2i+1 = y 0, 2i+2 = y 19.
    btn = sheets["dlgbttns"]
    btn_pal = read_cmp(SRC / "Graphics" / "dlgbttns.cmp")[0]
    for i, bname in enumerate(BUTTONS):
        for state, y in ((0, 0), (1, 19)):
            save(indexed_crop(btn, (64 * i, y, 64 * i + 64, y + 18), btn_pal), f"buttons/{bname}-{state}.png", index,
                 source="Graphics/dlgbttns.cmp", slot=2 * i + 1 + state,
                 note="dialog button (0x8012); state 1 = the second row, shown while chosen [~]")

    # Dungeon map glyphs (slots 45..53, 8x5).
    for slot in range(45, 54):
        im = Image.open(OUT / f"shapes/dng/{slot:02d}.png")
        save(im, f"dungeon/glyph-{slot}.png", index, slot=slot)

    # Overworld tiles.
    d = (SRC / "Graphics" / "phantasy.set").read_bytes()
    set_pal = palette(d[3:35])
    palettes["map"] = hexes(set_pal)
    rows, cols = d[1], d[2]
    amiga_world = d[35:35 + rows * cols]
    tiles = d[35 + rows * cols:35 + rows * cols + TILE_COUNT * 128]
    tile_ims = [tile_image(tiles, i, set_pal) for i in range(TILE_COUNT)]
    for i, im in enumerate(tile_ims):
        save(im, f"overworld/tiles/{i:03d}.png", index, tile=i)
    save(tile_image(tiles, PARTY_TILE, set_pal, transparent0=True), "overworld/party.png", index,
         tile=PARTY_TILE, note="0x378a: OR-ed over the cell's tile, colour 0 transparent")

    def render(grid: bytes, w: int, h: int) -> Image.Image:
        im = Image.new("RGBA", (w * 16, h * 16), (0, 0, 0, 255))
        for y in range(h):
            for x in range(w):
                im.paste(tile_ims[min(grid[y * w + x], TILE_COUNT - 1)], (x * 16, y * 16))
        return im

    # Previews (not used by the pack): the Amiga's own Scandor, and the DOS maps the remaster plays, in Amiga tiles.
    save(render(amiga_world, cols, rows), "previews/scandor-amiga.png", index, note="phantasy.set's own map")
    diffs = []
    if (DOS / "WORLD.DAT").exists():
        world = (DOS / "WORLD.DAT").read_bytes()
        save(render(world, 50, 75), "previews/scandor-dos.png", index, note="DOS WORLD.DAT in Amiga tiles")
        diffs = [[i % 50, i // 50, world[i], amiga_world[i]] for i in range(3750) if world[i] != amiga_world[i]]
        planes = (DOS / "PLNS.DAT").read_bytes()
        save(render(planes[0:81], 9, 9), "previews/plane-light.png", index, note="DOS PLNS.DAT")
        save(render(planes[81:162], 9, 9), "previews/plane-dark.png", index, note="DOS PLNS.DAT")
        save(render(planes[162:658], 31, 16), "previews/netherworld.png", index, note="DOS PLNS.DAT")

    (OUT / "extraction-index.json").write_text(json.dumps({
        "_source": "tools/scripts/extract_amiga_p3_graphics.py; notes: reverse-engineering/amiga/phantasie3/FORMAT.md",
        "palettes": palettes,
        "monsters_pictured": pictured,
        "monsters_without_picture": unpictured,
        "scandor_dos_vs_amiga": {"_note": "[x, y, DOS tile, Amiga tile]; the remaster draws the DOS grid", "cells": diffs},
        "files": index,
    }, indent=1) + "\n")
    if PACK_COPY.exists():
        shutil.copyfile(PACK_COPY, OUT / "pack.json")
    print(f"{len(index)} files -> {OUT.relative_to(REPO)}; {len(pictured)} monsters pictured, "
          f"{len(unpictured)} without; {len(diffs)} Scandor cells differ from DOS")


if __name__ == "__main__":
    main()
