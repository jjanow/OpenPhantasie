#!/usr/bin/env python3
"""Extract Phantasie II (Atari ST v1.0) art to PNG (Phase 09 T3, task 4b).

Inputs: reverse-engineering/atari-st/phantasie2/unpacked/disk{1,2}/ (the clean Pasti set).
Output: reverse-engineering/atari-st/phantasie2/extracted/graphics/ (PNGs + extraction-index.json; the pack manifest
comes from build_st_p2_asset_manifest.py). Format notes: reverse-engineering/atari-st/phantasie2/FORMAT.md §10.
Run from the repo root: ``python3 -I tools/scripts/extract_st_p2_graphics.py``.

Image addresses below are PHANT.PRG image addresses (text at 0, data at 0x1e298; FORMAT.md §1).

* Raster format everywhere is the ST low-res device format: 4 word-interleaved planes, 8 bytes per 16 pixels, row
  stride = ceil(w / 16) * 8. ``.PIC`` files are headerless screen dumps (160 B per line). Sprites are GEM MFDBs in
  device format (``0x19a72`` builds fd_stand = 0, 4 planes) copied with vro_cpyfm mode 3 (replace), so colour 0 is
  opaque. The overworld fill patterns are the one exception: vsf_udpat data, 4 planes *sequential* (16 words each).
* Palettes are ST hardware words 0x0RGB with 3 bits per gun (Setpalette = XBIOS 6). Pixel values are raw plane bits
  = hardware colour registers (raster copies and multi-plane user patterns bypass the VDI colour mapping).
"""
from __future__ import annotations

import json
import math
import os
import struct
import sys
from pathlib import Path

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from extract_st_p2_common import DISK1, DISK2, EXTRACTED, FOG, REGIONS, rel  # noqa: E402
from st_prg import load_prg  # noqa: E402

OUT = EXTRACTED / "graphics"

# --- palettes -------------------------------------------------------------------------------------------------------
# START.PRG: Setpalette(image 0x176) = all black, load COVER1.PIC to the screen, Setpalette(0x156), wait 250 VBLs,
# load COVER2.PIC at screen + 10400 (line 65), Pexec PHANT.PRG.
START_PALETTE_ADDR = 0x156
PALETTES = {
    # name: (PHANT.PRG image address, what sets it)
    "town": (0x23A62, "0x238c, 0x26xx town entry (fade in by Setcolor 0x2682), 0x2d0e; PELNOR.PIC (live-checked)"),
    "map": (0x22956, "0x5712 overworld map, 0x5cea dungeon map (live-checked on the overworld)"),
    "combat": (0x23A82, "combat screen (live-checked: monsters + portraits), 0x12d34 DECOR.PIC screen, "
                        "0x130ec ending FINAL2/CROWD fade-in"),
}

# --- PHANT.PRG tables -----------------------------------------------------------------------------------------------
MONSTER_OFFSETS = 0x23D74   # u32 x 80: byte offset into MSTR1/2.PAT (0x1836c)
MONSTER_DIMS = 0x2112E      # (u16 w, u16 h) x 80 (0xfdca, read by the encounter builder 0xfd96 -> 0x18288)
MONSTER_RECORDS = 0x1E298 + 0x1C76
MONSTER_COUNT = 80
PATTERNS = 0x21DD6          # 12 x 128 B vsf_udpat fill patterns, index = terrain class (0x53ee / 0x545a)
PATTERN_COUNT = 12
ICONS = 0x223D6             # 5 x 16x16 MFDBs (0x5230): 0 town (tile 40), 1 inn (41), 2 dungeon (class 5), 3 party, 4 other class 4
ICON_KINDS = ["town", "inn", "dungeon", "party", "castle"]
GLYPHS = 0x22656            # 8 x 16x4 MFDBs, glyph id 0xe6..0xed (0x5e50: (id - 0xe6) * 32)
EMPTY_PORTRAIT = 0x22756    # 32x32, drawn for an empty portrait slot (0x12bc0)
PORTRAIT_MARK = 0x23CDC     # 16x19 white ghost, drawn at portrait + (8, 13) (0x18a02)
# s156 (FORMAT.md 7.3): line-A sprite blocks (A00D, 74 bytes: hot x, hot y, format, bg colour, fg colour, 16 x (bg mask,
# fg mask) words). Format 1 = VDI: an fg bit takes the fg colour, else a bg bit the bg colour, else the screen shows.
LINEA_SPRITES = [
    ("combat/sparkle", 0x23C48, "0x18b16, amount >= 0: a damage spell's sparkle on a monster (bg 5, fg 2)"),
    ("combat/rock", 0x23C92, "0x18b16, amount < 0: the thrown rock on its monster; 0x18c6c: a rock's miss at (100, 10) (bg 13, fg 15)"),
]

# Bitmaps that sit inside the text segment (0x13862..0x1640c, after the colour-cycle VBL routine 0x1382e).
TEXT_BITMAPS = [
    # (name, address, w, h, frames, palette, where drawn)
    ("decor/demon-face", 0x13864, 80, 80, 1, "combat", "red demon face: 0x12e50 at (123,19) behind the door on DECOR; 0xdd92 at (146,114)"),
    ("decor/door", 0x144E4, 80, 90, 1, "combat", "wooden door on DECOR: 0x12c9c at (123,14); 0x12dca redraws it shorter step by step (opening)"),
    ("town/walker", 0x152F4, 16, 22, 7, "town", "0x4824: walker in the town street (y 156, x 110..183); frame 0 = blank"),
    ("maps/dungeon/compass", 0x157C4, 64, 63, 1, "map", "0x5c7a at (214,68) on the dungeon map"),
    ("decor/sorry-sign", 0x15FA4, 48, 40, 1, "combat", "sign 'Sorry, we've run out of Demons': 0x12e0c at (139,39) on the fifth run of the DECOR sequence (counter 0x231fa)"),
]

# --- screens --------------------------------------------------------------------------------------------------------
SCREENS = [
    # (file, disk, palette, top line on screen, note)
    ("COVER1.PIC", 1, "start", 0, "START.PRG title"),
    ("COVER2.PIC", 1, "start", 65, "START.PRG credits, loaded over COVER1 at line 65"),
    ("PELNOR.PIC", 2, "town", 0, "town backdrop (0x43ec), GEM menu bar baked in"),
    ("DECOR.PIC", 2, "combat", 0, "DECOR screen (0x12bea: door + demon-face sequence), 155 lines"),
    ("FINAL2.PIC", 2, "combat", 0, "ending (0x13002), 160 lines"),
]
CROWD = ("CROWD.PIC", 2, "combat", 40, 127, 235, 31, 3)  # 3 frames of 235x31 at (40,127) (0x1320c..0x1327a)

# --- overworld map geometry (0x5760; dungeon 0x5b06) ------------------------------------------------------------------
CELL_W, CELL_H, ORIGIN_X, ORIGIN_Y = 12, 9, 4, 15
COLS, ROWS = 26, 20
POLY_OFF = 1500
DNG_CELL_W, DNG_CELL_H = 5, 4

# 0x1382e (VBL routine): every N VBLs rotates hardware colours 4 -> 7 -> 14 -> 4 (0xff8248/0xff824e/0xff825c).
COLOUR_CYCLE = [4, 7, 14]


def st_rgb(word: int) -> tuple[int, int, int]:
    """ST 3-bit gun -> 8 bit (v * 255 / 7). Hatari shows v * 0x22; the index match is what verification checks."""
    return tuple(((word >> s) & 7) * 255 // 7 for s in (8, 4, 0))


def hexcol(word: int) -> str:
    return "#%02X%02X%02X" % st_rgb(word)


def decode_interleaved(data: bytes, off: int, w: int, h: int, pal: list[int]) -> Image.Image:
    wpl = (w + 15) // 16
    im = Image.new("RGB", (w, h))
    px = im.load()
    rgb = [st_rgb(c) for c in pal]
    for y in range(h):
        for g in range(wpl):
            p0, p1, p2, p3 = struct.unpack_from(">4H", data, off + (y * wpl + g) * 8)
            for b in range(16):
                x = g * 16 + b
                if x >= w:
                    break
                s = 15 - b
                c = (p0 >> s & 1) | (p1 >> s & 1) << 1 | (p2 >> s & 1) << 2 | (p3 >> s & 1) << 3
                px[x, y] = rgb[c]
    return im


def decode_udpat(data: bytes, off: int, pal: list[int]) -> Image.Image:
    rgb = [st_rgb(c) for c in pal]
    im = Image.new("RGB", (16, 16))
    px = im.load()
    for y in range(16):
        planes = [struct.unpack_from(">H", data, off + 32 * p + 2 * y)[0] for p in range(4)]
        for x in range(16):
            c = sum((planes[p] >> (15 - x) & 1) << p for p in range(4))
            px[x, y] = rgb[c]
    return im


def decode_linea_sprite(data: bytes, off: int, pal: list[int]) -> tuple[Image.Image, dict]:
    """A line-A sprite block as a 16x16 RGBA picture (unset pixels transparent) and its header."""
    hot_x, hot_y, fmt, bg, fg = struct.unpack_from(">5h", data, off)
    words = struct.unpack_from(">32H", data, off + 10)
    rgb = [st_rgb(c) for c in pal]
    im = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    px = im.load()
    for y in range(16):
        back, fore = words[2 * y], words[2 * y + 1]
        for x in range(16):
            s = 15 - x
            if fore >> s & 1:
                px[x, y] = rgb[fg] + (255,)
            elif back >> s & 1:
                px[x, y] = rgb[bg] + (255,)
    return im, {"hot_spot": [hot_x, hot_y], "format": fmt, "background": bg, "foreground": fg}


def palette_png(words: list[int]) -> Image.Image:
    im = Image.new("RGB", (16 * 8, 8))
    d = ImageDraw.Draw(im)
    for i, w in enumerate(words):
        d.rectangle((i * 8, 0, i * 8 + 7, 7), fill=st_rgb(w))
    return im


def slug(s: str) -> str:
    out = "".join(ch.lower() if ch.isalnum() else "-" for ch in s.strip().rstrip("."))
    while "--" in out:
        out = out.replace("--", "-")
    return out.strip("-")


def save(im: Image.Image, relpath: str) -> str:
    p = OUT / relpath
    p.parent.mkdir(parents=True, exist_ok=True)
    im.save(p, optimize=True)
    return relpath


def render_region(data: bytes, patterns: list[Image.Image]) -> Image.Image:
    """0x5028: fill the map rect with the background pattern (first word at +1500), then v_fillarea each polygon
    ``[count][pattern][count x (x, y)]`` until count = -1 (perimeter off, interior = user pattern, replace mode).
    Patterns are screen-aligned (x & 15, y & 15). Returns the map rect (312x180) without icons or fog."""
    tiled = []
    for pat in patterns:
        t = Image.new("RGB", (320, 200))
        for y in range(0, 200, 16):
            for x in range(0, 320, 16):
                t.paste(pat, (x, y))
        tiled.append(t)
    screen = Image.new("RGB", (320, 200))
    off = POLY_OFF
    (bg,) = struct.unpack_from(">h", data, off)
    off += 2
    rect = (ORIGIN_X, ORIGIN_Y, ORIGIN_X + COLS * CELL_W, ORIGIN_Y + ROWS * CELL_H)
    screen.paste(tiled[bg].crop(rect), rect[:2])
    while True:
        count, pattern = struct.unpack_from(">hh", data, off)
        if count == -1:
            break
        off += 4
        pts = struct.unpack_from(f">{2 * count}h", data, off)
        off += 4 * count
        mask = scanline_mask(list(zip(pts[0::2], pts[1::2])), rect)
        screen.paste(tiled[pattern], (0, 0), mask)
    return screen.crop(rect)


def scanline_mask(pts: list[tuple[int, int]], rect: tuple[int, int, int, int]) -> Image.Image:
    """Even-odd scanline fill as the VDI does it (perimeter off): rows min_y + 1 ... max_y (the polygon's top row is
    not filled: GEM's fill loop runs while y > fill_miny), on row y an edge counts when min_y <= y < max_y, crossing
    x = x0 + (y - y0)(x1 - x0)/(y1 - y0) rounded half up, spans filled inclusive. s135: matches Hatari's OUT2 pixel
    for pixel outside the mouse and the icons (FORMAT.md 10.5; filling the top row too left 15 pixels off)."""
    mask = Image.new("L", (320, 200), 0)
    mp = mask.load()
    ys = [p[1] for p in pts]
    for y in range(max(rect[1], min(ys) + 1), min(rect[3], max(ys) + 1)):
        xs = []
        for i in range(len(pts)):
            (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % len(pts)]
            if y0 != y1 and min(y0, y1) <= y < max(y0, y1):
                xs.append(x0 + (y - y0) * (x1 - x0) / (y1 - y0))
        xs.sort()
        for a, b in zip(xs[0::2], xs[1::2]):
            for x in range(max(rect[0], math.floor(a + 0.5)), min(rect[2] - 1, math.floor(b + 0.5)) + 1):
                mp[x, y] = 255
    return mask


def tile(data: bytes, x: int, y: int) -> int:
    t = data[(COLS - 1 - x) * ROWS + y]
    return t - FOG if t >= FOG else t


def icon_cells(data: bytes) -> dict[str, list[list[int]]]:
    """0x4cd0: columns outer, rows inner. Tile 40 -> town (one slot), 41 -> inn (one slot), other class 4 -> castle
    (5 slots), class 5 -> dungeon (8 slots). Single slots keep the last hit; multi slots fill in scan order, extra
    hits beyond the slot count are not drawn [~] (the 0x1fbec jump table caps them)."""
    found: dict[str, list[list[int]]] = {"town": [], "inn": [], "dungeon": [], "castle": []}
    for x in range(COLS):
        for y in range(ROWS):
            t = tile(data, x, y)
            if t // 10 == 4:
                kind = {0: "town", 1: "inn"}.get(t % 10, "castle")
                if kind in ("town", "inn"):
                    found[kind] = [[x, y]]
                elif len(found[kind]) < 5:
                    found[kind].append([x, y])
            elif t // 10 == 5 and len(found["dungeon"]) < 8:
                found["dungeon"].append([x, y])
    return {k: v for k, v in found.items() if v}


def main() -> int:
    prg = load_prg(str(DISK1 / "PHANT.PRG"))
    img = prg.image
    start = load_prg(str(DISK1 / "START.PRG")).image

    def words(buf: bytes, a: int) -> list[int]:
        return list(struct.unpack_from(">16H", buf, a))

    pals = {"start": words(start, START_PALETTE_ADDR)}
    for name, (addr, _) in PALETTES.items():
        pals[name] = words(img, addr)
    index: dict = {
        "_source": "Phantasie II Atari ST v1.0 (unpacked/disk1, disk2); tools/scripts/extract_st_p2_graphics.py",
        "_format": "reverse-engineering/atari-st/phantasie2/FORMAT.md §10",
        "palettes": {},
    }
    for name, words16 in pals.items():
        where = "START.PRG image 0x156 (Setpalette before COVER1/COVER2)" if name == "start" else \
            f"PHANT.PRG image {PALETTES[name][0]:#x} (data+{PALETTES[name][0] - prg.text_size:#x}): {PALETTES[name][1]}"
        index["palettes"][name] = {
            "source": where, "st_words": [f"{w:03x}" for w in words16], "rgb": [hexcol(w) for w in words16],
            "swatch": save(palette_png(words16), f"palettes/{name}.png"),
        }
    index["palettes"]["map"]["colour_cycle"] = {
        "registers": COLOUR_CYCLE,
        "note": "VBL routine 0x1382e rotates hardware colours 4 -> 7 -> 14 -> 4 (counter 0x13862/0x13863)"}

    # screens
    index["screens"] = []
    for name, disk, pal, top, note in SCREENS:
        data = ((DISK1 if disk == 1 else DISK2) / name).read_bytes()
        h = len(data) // 160
        path = save(decode_interleaved(data, 0, 320, h, pals[pal]), f"screens/{slug(name.split('.')[0])}.png")
        index["screens"].append({"file": name, "path": path, "width": 320, "height": h, "top": top,
                                 "palette": pal, "note": note})
    c1 = Image.open(OUT / "screens/cover1.png").convert("RGB")
    c1.paste(Image.open(OUT / "screens/cover2.png").convert("RGB"), (0, 65))
    index["screens"].append({"file": "COVER1.PIC + COVER2.PIC", "path": save(c1, "screens/title-credits.png"),
                             "width": 320, "height": 200, "top": 0, "palette": "start",
                             "note": "the title screen after START.PRG's 250-VBL wait"})
    name, disk, pal, x0, y0, w, h, frames = CROWD
    data = (DISK2 / name).read_bytes()
    stride = ((w + 15) // 16) * 8 * h
    assert stride * frames == len(data)
    index["crowd"] = {"file": name, "x": x0, "y": y0, "width": w, "height": h, "palette": pal,
                      "note": "ending: frames 0/1 alternate 10 times over FINAL2 (330-tick waits), then frame 2",
                      "frames": [save(decode_interleaved(data, k * stride, w, h, pals[pal]), f"screens/crowd-{k}.png")
                                 for k in range(frames)]}

    # monsters
    mstr = [(DISK1 / "MSTR1.PAT").read_bytes(), (DISK2 / "MSTR2.PAT").read_bytes()]
    index["monsters"] = []
    for i in range(MONSTER_COUNT):
        off = prg.u32(MONSTER_OFFSETS + 4 * i)
        w, h = prg.u16(MONSTER_DIMS + 4 * i), prg.u16(MONSTER_DIMS + 4 * i + 2)
        nm = prg.cstr(MONSTER_RECORDS + 58 * i, 20)
        assert off + ((w + 15) // 16) * 8 * h <= len(mstr[0])
        fn = f"{i:02d}-{slug(nm)}.png"
        index["monsters"].append({
            "index": i, "name": nm, "width": w, "height": h, "pat_offset": off,
            "idle": save(decode_interleaved(mstr[0], off, w, h, pals["combat"]), f"monsters/idle/{fn}"),
            "attacking": save(decode_interleaved(mstr[1], off, w, h, pals["combat"]), f"monsters/attacking/{fn}"),
        })

    # portraits
    races = json.loads((EXTRACTED / "names.json").read_text())["races"]
    p1, p2 = (DISK1 / "PARTY1.PAT").read_bytes(), (DISK1 / "PARTY2.PAT").read_bytes()
    index["races"] = []
    for i in range(16):
        nm = races[i] if i < len(races) else f"race-{i}"
        fn = f"{i:02d}-{slug(nm)}.png"
        index["races"].append({
            "index": i, "name": nm,
            "idle": save(decode_interleaved(p1, 512 * i, 32, 32, pals["combat"]), f"portraits/idle/{fn}"),
            "attacking": save(decode_interleaved(p2, 512 * i, 32, 32, pals["combat"]), f"portraits/attacking/{fn}"),
        })

    # small bitmaps
    index["bitmaps"] = []
    for name, addr, w, h, frames, pal, note in TEXT_BITMAPS:
        size = ((w + 15) // 16) * 8 * h
        paths = [save(decode_interleaved(img, addr + k * size, w, h, pals[pal]),
                      f"{name}{'-%d' % k if frames > 1 else ''}.png") for k in range(frames)]
        index["bitmaps"].append({"name": name, "address": f"{addr:#x}", "width": w, "height": h,
                                 "palette": pal, "note": note, "frames": paths})
    for name, addr, w, h, note in [("ui/empty-portrait", EMPTY_PORTRAIT, 32, 32, "0x12bc0: empty party slot"),
                                   ("ui/ghost-mark", PORTRAIT_MARK, 16, 19, "white ghost, 0x18a02: drawn at portrait + (8,13) [?] which status")]:
        index["bitmaps"].append({"name": name, "address": f"{addr:#x}", "width": w, "height": h, "palette": "combat",
                                 "note": note, "frames": [save(decode_interleaved(img, addr, w, h, pals["combat"]),
                                                               f"{name}.png")]})

    index["sprites"] = []
    for name, addr, note in LINEA_SPRITES:
        sprite, header = decode_linea_sprite(img, addr, pals["combat"])
        index["sprites"].append({"name": name, "address": f"{addr:#x}", "width": 16, "height": 16, "palette": "combat",
                                 "note": note, **header, "path": save(sprite, f"{name}.png")})

    # overworld + dungeon map
    patterns = [decode_udpat(img, PATTERNS + 128 * i, pals["map"]) for i in range(PATTERN_COUNT)]
    maps = {
        "_source": "PHANT.PRG 0x5760 geometry, 0x5028 map draw, 0x53ee/0x545a patterns, 0x5230 icons, 0x4cd0 icon scan, "
                   "0x5b06 dungeon geometry, 0x5e50 glyphs, 0x5c7a compass; tools/scripts/extract_st_p2_graphics.py",
        "palette": "map",
        "overworld": {
            "cell_width": CELL_W, "cell_height": CELL_H, "origin": [ORIGIN_X, ORIGIN_Y], "fog_threshold": FOG,
            "patterns": {str(i): save(p, f"maps/overworld/patterns/{i:02d}.png") for i, p in enumerate(patterns)},
            # 16x16 bitmaps, blitted through a 12x9 cell rect (0x4af2(rect, x, y, 1, 1)): only the top-left 12x9 shows
            "icons": {k: save(decode_interleaved(img, ICONS + 128 * i, 16, 16, pals["map"]).crop((0, 0, CELL_W, CELL_H)),
                              f"maps/overworld/icons/{k}.png") for i, k in enumerate(ICON_KINDS) if k != "party"},
            "party": save(decode_interleaved(img, ICONS + 128 * 3, 16, 16, pals["map"]).crop((0, 0, CELL_W, CELL_H)),
                          "maps/overworld/icons/party.png"),
            "regions": {},
        },
        "dungeon": {
            "glyph_width": DNG_CELL_W, "glyph_height": DNG_CELL_H, "origin": [10, 16],
            "glyphs": {str(0xE6 + i): save(decode_interleaved(img, GLYPHS + 32 * i, 16, 4, pals["map"]).crop(
                (0, 0, DNG_CELL_W, DNG_CELL_H)), f"maps/dungeon/glyph-{0xE6 + i:02x}.png") for i in range(8)},
            "compass": "dungeon/compass.png",  # written by the TEXT_BITMAPS pass
        },
    }
    for n in REGIONS:
        data = (DISK2 / f"OUT{n}.DAT").read_bytes()
        cells = icon_cells(data)
        maps["overworld"]["regions"][str(n)] = {
            "image": save(render_region(data, patterns), f"maps/overworld/regions/out{n:02d}.png"),
            "background_pattern": struct.unpack_from(">h", data, POLY_OFF)[0],
            # "icons" keeps the amiga-maps/v1 shape (one cell per kind, read by AmigaMapManifest); II draws several
            # dungeon / castle icons per region, so "icon_cells" lists every drawn cell (Client wiring: later).
            "icons": {k: v[0] for k, v in cells.items()},
            "icon_cells": cells,
        }
    # maps.json paths are relative to its own folder (as the Amiga maps.json, read by AmigaMapManifest)
    def strip(o):
        if isinstance(o, dict):
            return {k: strip(v) for k, v in o.items()}
        if isinstance(o, list):
            return [strip(v) for v in o]
        return o[len("maps/"):] if isinstance(o, str) and o.startswith("maps/") else o
    (OUT / "maps" / "maps.json").write_text(json.dumps(strip(maps), indent=2) + "\n")
    index["maps"] = "maps/maps.json"
    (OUT / "extraction-index.json").write_text(json.dumps(index, indent=2, ensure_ascii=False) + "\n")
    print(f"wrote {rel(OUT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
