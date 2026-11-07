#!/usr/bin/env python3
"""Extract the Amiga Phantasie I map graphics (overworld + dungeon map) to PNG (P0.7, Amiga graphics set).

Overworld — the Amiga map is not a tile grid. The map module (``game`` hunk 56, data hunk 57, BSS hunk 58) draws it as:

  * ``h56:0x948`` cell geometry 12x9 px, map origin (4,15); the map rect is 26x20 cells = (4,15,312,180).
  * ``h56:0x6bc`` the background: the first word at ``OUT*.DAT + 0x3e8`` is a pattern index; the map rect is filled
    with it. Then polygon records ``[count][pattern][count x (x,y)]`` (big-endian words, absolute screen pixels)
    until count = -1, each area-filled (graphics.library AreaMove/AreaDraw/AreaEnd via ``h77:0x614``). The filled
    area is the scan-line interior plus each edge as the blitter draws it (``area_mask``, matched on FS-UAE s128).
  * Fill patterns: 16x16 multicolour (RastPort AreaPtSz = -4), 4 sequential bitplanes, 128 B each at hunk 69 +
    0x294 (file 0x196bc), ``pattern * 128``; the pattern is aligned to the screen raster (x & 15, y & 15).
  * Palette: hunk 69 + 0x74 (file 0x1949c), loaded by ``h56:0x550`` (LoadRGB4: extracted c * 17, ``hw4``, s127).
  * ``h56:0x5a4`` icons: a class-4 cell (tile / 10 = 4) except tile 41 = town, tile 41 = inn, class 5 = dungeon
    (fogged cells count, tile - 120). Only the last cell found per kind (column-major scan) gets an icon.
    ``h77:0xaf4`` blits icon ``kind`` (0 town, 1 inn, 2 dungeon) 12x9 from a 16x9 4-plane bitmap at hunk 69 +
    0x1bc + kind * 0x48 (file 0x195e4) to the cell's top-left.
  * Party: ``h77:0xdfe`` (kind 3) shows a hardware sprite: hunk 69 + 0x128 (file 0x19550), SimpleSprite height 9,
    colours 1..3 = 0x000, 0xe00, 0xeb0 (SetRGB4 in the same routine).
  * Fog: ``h56:0x254/0x2ea/0x860`` clears the map rect to black and copies back the rects of cells < 120.

Grid order in the Amiga ``OUT*.DAT``: cell (x, y) at (25 - x) * 20 + y (``h56:0x214``); it equals the DOS grid
(y * 26 + x) except region 2 (11,8) and region 12 (11,14) (Amiga water, DOS land).

Remaster fixes (the pictures follow the rules Core runs):
  * Picture vs DOS terrain (user s53: keep the DOS terrain, patch the picture): where the Amiga cell class differs
    from the DOS one, the last polygon that covers the cell's centre with the Amiga class is filled with the DOS
    class pattern instead (the OUT2 / OUT12 lakes become mountain / forest).
  * ``map-patches.json`` (OpenPhantasie.Data GameData) cells are applied to the Amiga grid before the icon scan, so
    APPLETON's OUT13 (17,19) cell gets a town icon and the moved inn at (4,14) an inn icon (D-060).

Dungeon map — ``h56:0xd10`` cell geometry 5x4 px, origin (10,16) (33x38 cells = (10,16,165,152)); ``h56:0xf76``
draws glyph id 0xe6..0xed (the DOS ids; 0xed = party) via ``h77:0xbc0``: a 16x4 4-plane bitmap (8 B per plane)
at hunk 69 + 0x894 + (id - 0xe6) * 32 (file 0x19cbc), blitted 5x4. ``h56:0xe98`` draws the 64x63 compass at
hunk 69 + 0x2af4 (file 0x1bf1c) to (214,68). Same palette as the overworld.

Writes reverse-engineering/amiga/extracted/maps/{overworld/{regions,patterns,icons},dungeon}/*.png and maps.json.
"""
from __future__ import annotations

import json
import struct

from PIL import Image, ImageDraw

from amiga_gfx_common import EXTRACTED, REPO, UNPACKED, decode_planar, ensure_dir, load_palette

PALETTE_OFF = 0x1949C
PATTERN_OFF = 0x196BC
PATTERN_COUNT = 12          # classes 0..11; 4 and 5 (town, dungeon) are plain black under the icons
ICON_OFF = 0x195E4
ICON_KINDS = ["town", "inn", "dungeon"]
SPRITE_OFF = 0x19550
SPRITE_COLOURS = [0x000, 0xE00, 0xEB0]

CELL_W, CELL_H = 12, 9
ORIGIN_X, ORIGIN_Y = 4, 15
COLS, ROWS = 26, 20
POLY_OFF = 0x3E8
FOG = 120
REGIONS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 18, 19]

GLYPH_OFF = 0x19CBC
GLYPH_IDS = list(range(0xE6, 0xEE))
GLYPH_W, GLYPH_H = 5, 4
COMPASS_OFF = 0x1BF1C
COMPASS_W, COMPASS_H = 64, 63


def nibble(v: int) -> int:
    # SetRGB4 / LoadRGB4 take 4-bit guns: the screen shows c * 17 (hw4, s127; FORMAT.md "Palette tables in use").
    return v * 17


def rgb(word: int) -> tuple:
    return (nibble(word >> 8 & 15), nibble(word >> 4 & 15), nibble(word & 15), 255)


def region_file(n: int) -> bytes:
    names = {p.name.lower(): p for p in UNPACKED.iterdir()}
    return names[f"out{n}.dat"].read_bytes()


def words(data: bytes, off: int, n: int) -> list[int]:
    return list(struct.unpack_from(f">{n}h", data, off))


GAME_DATA = REPO / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie1"


ICON_CLASSES = {4, 5}  # tile // 10: 4 town / inn, 5 dungeon (icon_cells)


def dos_grid(n: int) -> list[list[int]]:
    """Visible DOS tiles of region n with the remaster map patches applied."""
    ow = json.loads((GAME_DATA / "overworld.json").read_text())["regions"][f"OUT{n}.DAT"]["tiles"]
    grid = [[t - FOG if t >= FOG else t for t in row] for row in ow]
    for p in map_patches(n):
        grid[p["y"]][p["x"]] = p["tile"]
    return grid


def map_patches(n: int) -> list[dict]:
    path = GAME_DATA / "map-patches.json"
    return [p for p in json.loads(path.read_text()).get("overworld", []) if p["region"] == n] if path.exists() else []


def amiga_tile(data: bytes, x: int, y: int) -> int:
    t = data[(COLS - 1 - x) * ROWS + y]
    return t - FOG if t >= FOG else t


def blitter_line(x0: int, y0: int, x1: int, y1: int) -> list[tuple[int, int]]:
    """The pixels of a line as the Amiga blitter draws it from (x0, y0) to (x1, y1): error term 2*dmin - dmax, a minor
    step whenever it is >= 0 (then += 2*(dmin - dmax), else += 2*dmin)."""
    dx, dy = abs(x1 - x0), abs(y1 - y0)
    sx, sy = (1 if x1 > x0 else -1), (1 if y1 > y0 else -1)
    x_major = dx >= dy
    dmax, dmin = (dx, dy) if x_major else (dy, dx)
    err, x, y = 2 * dmin - dmax, x0, y0
    pts = [(x, y)]
    for _ in range(dmax):
        if err >= 0:
            if x_major:
                y += sy
            else:
                x += sx
            err += 2 * (dmin - dmax)
        else:
            err += 2 * dmin
        if x_major:
            x += sx
        else:
            y += sy
        pts.append((x, y))
    return pts


def area_mask(pts: list[tuple[int, int]]) -> Image.Image:
    """Pixels an AreaMove / AreaDraw / AreaEnd polygon covers on the Amiga screen (s128, FS-UAE capture of OUT1: all
    56,160 map pixels equal, PIL's own outline was off by one row on 235): the scan-line interior plus every edge drawn
    as a blitter line in the record's vertex order (last vertex back to the first)."""
    mask = Image.new("L", (320, 200), 0)
    ImageDraw.Draw(mask).polygon(pts, fill=255)
    px = mask.load()
    for i, (x0, y0) in enumerate(pts):
        x1, y1 = pts[(i + 1) % len(pts)]
        for x, y in blitter_line(x0, y0, x1, y1):
            if 0 <= x < 320 and 0 <= y < 200:
                px[x, y] = 255
    return mask


def render_region(data: bytes, patterns: list[Image.Image], dos: list[list[int]] | None = None) -> Image.Image:
    """The full map rect in screen coordinates (320x200), then cropped to 312x180. With ``dos``, a polygon that paints a
    cell whose Amiga class differs from the DOS class is filled with the DOS class pattern (last such polygon wins)."""
    screen = Image.new("RGBA", (320, 200), (0, 0, 0, 255))
    tiled = []
    for pat in patterns:
        t = Image.new("RGBA", (320, 200))
        for y in range(0, 200, 16):
            for x in range(0, 320, 16):
                t.paste(pat, (x, y))
        tiled.append(t)

    rect = (ORIGIN_X, ORIGIN_Y, ORIGIN_X + COLS * CELL_W, ORIGIN_Y + ROWS * CELL_H)
    off = POLY_OFF
    background = words(data, off, 1)[0]
    off += 2
    mask = Image.new("L", (320, 200), 0)
    ImageDraw.Draw(mask).rectangle((rect[0], rect[1], rect[2] - 1, rect[3] - 1), fill=255)
    screen.paste(tiled[background], (0, 0), mask)
    polys = []
    while True:
        count, pattern = words(data, off, 2)
        if count == -1:
            break
        off += 4
        pts = words(data, off, 2 * count)
        off += 4 * count
        polys.append([pattern, area_mask(list(zip(pts[0::2], pts[1::2])))])
    if dos is not None:
        for y in range(ROWS):
            for x in range(COLS):
                ami, want = amiga_tile(data, x, y) // 10, dos[y][x] // 10
                if ami == want or ICON_CLASSES & {ami, want}:
                    continue  # towns, inns and dungeons are icons drawn over the terrain, not polygon classes
                centre = (ORIGIN_X + x * CELL_W + CELL_W // 2, ORIGIN_Y + y * CELL_H + CELL_H // 2)
                hits = [p for p in polys if p[0] == ami and p[1].getpixel(centre)]
                if hits:
                    hits[-1][0] = want
    for pattern, mask in polys:
        screen.paste(tiled[pattern], (0, 0), mask)
    return screen.crop(rect)


def icon_cells(data: bytes, patches: list[dict] | None = None) -> dict[str, list[int]]:
    """h56:0x5a4 — columns outer, rows inner; the last hit per kind wins. ``patches`` override cells first."""
    patched = {(p["x"], p["y"]): p["tile"] for p in patches or []}
    found: dict[str, list[int]] = {}
    for x in range(COLS):
        for y in range(ROWS):
            tile = patched.get((x, y), amiga_tile(data, x, y))
            if tile // 10 == 4:
                found["inn" if tile % 10 == 1 else "town"] = [x, y]
            elif tile // 10 == 5:
                found["dungeon"] = [x, y]
    return found


def party_sprite(game: bytes) -> Image.Image:
    colours = [(0, 0, 0, 0)] + [rgb(c) for c in SPRITE_COLOURS]
    img = Image.new("RGBA", (16, 9))
    for y in range(9):
        p0, p1 = struct.unpack_from(">HH", game, SPRITE_OFF + 4 + 4 * y)
        for x in range(16):
            bit = 15 - x
            img.putpixel((x, y), colours[(p0 >> bit & 1) | (p1 >> bit & 1) << 1])
    return img


def main() -> None:
    game = (UNPACKED / "game").read_bytes()
    palette = load_palette(game, PALETTE_OFF, hw4=True)  # h56 +0x550 LoadRGB4s it: c * 17 on screen (s127)
    root = ensure_dir(EXTRACTED / "maps")
    ow = ensure_dir(root / "overworld")
    patterns = [decode_planar(game, PATTERN_OFF + 128 * i, 16, 16, palette) for i in range(PATTERN_COUNT)]
    for i, pat in enumerate(patterns):
        pat.save(ensure_dir(ow / "patterns") / f"{i:02d}.png")
    for i, kind in enumerate(ICON_KINDS):
        decode_planar(game, ICON_OFF + 0x48 * i, 16, 9, palette).crop((0, 0, CELL_W, CELL_H)).save(
            ensure_dir(ow / "icons") / f"{kind}.png")
    party_sprite(game).save(ow / "icons" / "party.png")

    regions = {}
    for n in REGIONS:
        data = region_file(n)
        render_region(data, patterns, dos_grid(n)).save(ensure_dir(ow / "regions") / f"out{n:02d}.png")
        regions[str(n)] = {"image": f"overworld/regions/out{n:02d}.png", "icons": icon_cells(data, map_patches(n))}

    dng = ensure_dir(root / "dungeon")
    glyphs = {}
    for i, glyph in enumerate(GLYPH_IDS):
        name = f"dungeon/glyph-{glyph:02x}.png"
        decode_planar(game, GLYPH_OFF + 32 * i, 16, GLYPH_H, palette).crop((0, 0, GLYPH_W, GLYPH_H)).save(root / name)
        glyphs[str(glyph)] = name
    decode_planar(game, COMPASS_OFF, COMPASS_W, COMPASS_H, palette).save(dng / "compass.png")

    manifest = {
        "_source": "Amiga game h56 map module (overworld 0x5a4 icons, 0x6bc polygons, 0x948 geometry; dungeon 0xd10 "
                   "geometry, 0xf76 glyphs, 0xe98 compass), h77 0xaf4/0xbc0/0xdfe; "
                   "tools/scripts/extract_amiga_map_graphics.py",
        "overworld": {
            "cell_width": CELL_W, "cell_height": CELL_H,
            "fog_threshold": FOG,
            "icons": {kind: f"overworld/icons/{kind}.png" for kind in ICON_KINDS},
            "party": "overworld/icons/party.png",
            "regions": regions,
        },
        "dungeon": {
            "glyph_width": GLYPH_W, "glyph_height": GLYPH_H,
            "glyphs": glyphs,
            "compass": "dungeon/compass.png",
        },
    }
    (root / "maps.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"wrote {root}")


if __name__ == "__main__":
    main()
