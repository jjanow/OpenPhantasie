#!/usr/bin/env python3
"""Phantasie II (Atari ST): MAPS.INT + OUT*.DAT -> extracted/maps/ (task 3b.8).

OUTn.DAT, 3000 B (Amiga I 2500 B; PHANT.PRG 0x4b88 reads 3000 B into the region buffer [0x248b6]) [C]:
    0..519     26 x 20 cell grid, cell (x, y) at (25 - x) * 20 + y (0x48e6, 0x6792); x 0..25 west->east, y 0..19
               north->south; fog = tile + 120
    520..594   15 feature records x 5 B: (y, 25 - x, arg, dest_x, dest_y) (0x6528 matches rec[0] with y and
               rec[1] with 25 - x; dungeon dest = the start cell in DNG(arg), checked below)
    595..994   10 text pages x 40 B (0x615e: arg < 10 -> page arg; 10..19 -> 2 pages from arg - 10; else 3 pages
               from arg - 20); 995..999 tail
    1000..1499 500 B with no code reference found; holds editor / disk-buffer leftovers (file names, text) [~]
    1500..2999 map picture: u16 background pattern, then polygons [count][pattern][count x (x, y)] until count = -1
               (0x5028 via the pointer [0x1fbd0] = buffer + 1500; Amiga I has the same block at 1000)
MAPS.INT, 8840 B = 17 x 520: the initial grid of each region in REGIONS order. UTILIT2.PRG 0xb2c (new game)
copies each 520-byte block over bytes 0..519 of OUTn.DAT and leaves the rest of the file alone [C].

Writes: overworld.json (shape of GameData/phantasie1/overworld.json, tiles as [y][x] rows), maps-int.json,
ascii/OUTn.txt, png/OUTn.png (cells coloured by tile class; a debug render, not the game's art).
Run from the repo root: python3 -I tools/scripts/extract_st_p2_maps.py
"""
from __future__ import annotations

import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_st_p2_common import FOG, REGIONS, d1, d2, out_dir, rel, unfog, write_json, write_text  # noqa: E402

W, H = 26, 20
GRID = W * H
FEAT_OFF, FEAT_N = 520, 15
TEXT_OFF, TEXT_PAGES, PAGE = 595, 10, 40
GAP_OFF, POLY_OFF, SIZE = 1000, 1500, 3000

# Step rules from PHANT.PRG 0x60c6..0x651e (FORMAT.md §9.3) [C]; names in brackets are inferences.
CLASS_RULES = {
    "tile 100": "blocked (no move, no time)",
    "tile % 10 == 6": "location text (feature arg -> pages)",
    "tile % 10 == 7": "fixed encounter, monster = feature arg + 1 (1-based)",
    "tile % 10 == 8": "plain move, no encounter roll",
    "class 5": "dungeon entrance (state 13; feature arg = dungeon, dest = start cell)",
    "class 7, class 11": "if not yet this trip: 9 % per step (rand0_99 <= 8) fight, monster (1-based) = "
                         "rand0_99 / 34 + 12 * (tile % 10) + 1; never the 6 % roll. [Mist / Dark Voids]",
    "class 8, tile % 10 != 0": "state 8 = 0xfac2(0) ' L A V A :': every non-dead member takes "
                               "1 + rand0_99 * (tile % 10) / 10 damage; no encounter. [Molten Lava]",
    "class 8, tile 80": "plain move, no encounter roll",
    "tile 40": "town (feature arg - 1 = town index)",
    "tile 41": "wilderness inn (state 9)",
    "class 10": "water: state 10 = 0xfac2(1) swimming",
    "otherwise": "6 % random encounter (rand0_99 < 6, 0xfe38)",
    "step cost": "class <= 3: (class + 1) * 75; class >= 4: 60 (0x612c)",
}

COLOURS = {0: (150, 200, 90), 1: (40, 130, 40), 2: (170, 140, 80), 3: (120, 100, 90), 4: (255, 255, 255),
           5: (200, 0, 200), 6: (60, 60, 60), 7: (120, 140, 220), 8: (250, 110, 0), 9: (230, 200, 120),
           10: (30, 70, 200), 11: (70, 0, 110)}
ASCII = {0: ".", 1: "f", 2: "h", 3: "^", 4: "+", 5: "D", 6: "=", 7: "m", 8: "L", 9: "s", 10: "~", 11: "v"}


def ascii_char(tile: int) -> str:
    if tile == 100:
        return "#"
    if tile == 40:
        return "T"
    if tile == 41:
        return "I"
    if tile % 10 == 6:
        return "?"
    if tile % 10 == 7:
        return "!"
    return ASCII.get(tile // 10, "x")


def grid_rows(g: bytes) -> list[list[int]]:
    return [[g[(25 - x) * 20 + y] for x in range(W)] for y in range(H)]


def polygons(o: bytes) -> dict:
    p = POLY_OFF
    bg = struct.unpack_from(">h", o, p)[0]
    p += 2
    polys = []
    while p + 2 <= len(o):
        n = struct.unpack_from(">h", o, p)[0]
        if n == -1:
            p += 2
            break
        pat = struct.unpack_from(">h", o, p + 2)[0]
        pts = [list(struct.unpack_from(">hh", o, p + 4 + 4 * k)) for k in range(n)]
        polys.append({"pattern": pat, "points": pts})
        p += 4 + 4 * n
    return {"background_pattern": bg, "polygons": polys, "end_offset": p}


def parse_region(o: bytes, r: int) -> dict:
    assert len(o) == SIZE, (r, len(o))
    feats = []
    for i in range(FEAT_N):
        b0, b1, arg, dx, dy = o[FEAT_OFF + 5 * i:FEAT_OFF + 5 * i + 5]
        feats.append({"index": i, "x": 25 - b1, "y": b0, "arg": arg, "dest_x": dx, "dest_y": dy,
                      "raw": [b0, b1, arg, dx, dy]})
    used = {}
    for f in feats:
        t = unfog(o[(25 - f["x"]) * 20 + f["y"]]) if 0 <= f["x"] < W and 0 <= f["y"] < H else None
        if t is None or t % 10 != 6:
            continue
        a = f["arg"]
        first, n = (a, 1) if a < 10 else (a - 10, 2) if a < 20 else (a - 20, 3)
        for k in range(first, first + n):
            used.setdefault(k, []).append(f["index"])
    pages = []
    for i in range(TEXT_PAGES):
        raw = o[TEXT_OFF + i * PAGE:TEXT_OFF + (i + 1) * PAGE]
        text = raw.split(b"\0", 1)[0].decode("latin-1").strip()
        if text:
            page = {"index": i, "text": "".join(c if " " <= c <= "~" else "\u00b7" for c in text),
                    "used_by_features": used.get(i, [])}
            if not used.get(i):
                page["_note"] = "no text-tile feature points here (leftover) [~]"
            pages.append(page)
    gap = o[GAP_OFF:POLY_OFF]
    return {
        "region_id": r,
        "size": len(o),
        "grid_width": W,
        "grid_height": H,
        "tiles": grid_rows(o[:GRID]),
        "features": feats,
        "text_pages": pages,
        "text_tail_hex": o[TEXT_OFF + TEXT_PAGES * PAGE:GAP_OFF].hex(),
        "gap_1000_1499": {"nonzero_bytes": sum(1 for b in gap if b), "hex": gap.rstrip(b"\0").hex()},
        "picture": polygons(o),
    }


def render(r: int, rows: list[list[int]]) -> None:
    lines = [f"OUT{r}.DAT initial grid (MAPS.INT), fog ignored. Legend: . class0  f class1  h class2  ^ class3  "
             "+ class4  D dungeon  = path(6)  m class7  L class8  s class9  ~ water  v class11  # tile100  T town  I inn  "
             "? text  ! fixed fight", "    " + "".join(str(x % 10) for x in range(W))]
    for y, row in enumerate(rows):
        lines.append(f"{y:3d} " + "".join(ascii_char(unfog(b)) for b in row))
    write_text("maps/ascii", f"OUT{r}.txt", "\n".join(lines) + "\n")
    try:
        from PIL import Image, ImageDraw
    except ImportError:
        return
    cw, ch = 12, 9
    img = Image.new("RGB", (W * cw, H * ch))
    dr = ImageDraw.Draw(img)
    for y, row in enumerate(rows):
        for x, b in enumerate(row):
            t = unfog(b)
            col = (0, 0, 0) if t == 100 else COLOURS.get(t // 10, (255, 0, 0))
            if t == 80:
                col = (180, 90, 40)
            dr.rectangle([x * cw, y * ch, x * cw + cw - 1, y * ch + ch - 1], fill=col)
            if t in (40, 41) or t % 10 in (6, 7):
                dr.text((x * cw + 3, y * ch - 1), ascii_char(t), fill=(0, 0, 0) if t != 40 else (200, 0, 0))
            if b >= FOG:
                for k in range(0, cw, 3):
                    dr.point((x * cw + k, y * ch + ch - 1), fill=(0, 0, 0))
    img = img.resize((W * cw * 2, H * ch * 2), Image.NEAREST)
    img.save(out_dir("maps/png") / f"OUT{r}.png")


def main() -> None:
    m = d1("MAPS.INT")
    assert len(m) == GRID * len(REGIONS)
    regions, init = {}, {}
    for k, r in enumerate(REGIONS):
        block = m[k * GRID:(k + 1) * GRID]
        o = d2(f"OUT{r}.DAT")
        parsed = parse_region(block + o[GRID:], r)   # initial state = MAPS.INT grid + the static rest of OUTn
        regions[f"OUT{r}.DAT"] = parsed
        played = o[:GRID]
        init[f"OUT{r}.DAT"] = {
            "maps_int_offset": k * GRID,
            "fogged_cells_initial": sum(1 for b in block if b >= FOG),
            "played_grid_equals_initial": played == block,
            "played_cells_differing": sum(1 for a, b in zip(block, played) if a != b),
            "played_cells_differing_other_than_fog": sum(1 for a, b in zip(block, played) if unfog(a) != unfog(b)),
        }
        render(r, parsed["tiles"])

    # Dungeon entrances: dest must land on an exit cell (0xd7..0xd9) of DNG(arg).
    checks = []
    for name, reg in regions.items():
        rows = reg["tiles"]
        for f in reg["features"]:
            if 0 <= f["x"] < W and 0 <= f["y"] < H and unfog(rows[f["y"]][f["x"]]) // 10 == 5 and 1 <= f["arg"] <= 8:
                dng = d2(f"DNG{f['arg']}")
                v = dng[f["dest_y"] * 33 + f["dest_x"]]
                checks.append({"region": name, "x": f["x"], "y": f["y"], "dungeon": f["arg"],
                               "dest": [f["dest_x"], f["dest_y"]], "dest_cell": hex(v), "ok": 0xD7 <= v <= 0xD9})
    assert all(c["ok"] for c in checks), checks

    counts = {}
    for reg in regions.values():
        for row in reg["tiles"]:
            for b in row:
                counts[unfog(b)] = counts.get(unfog(b), 0) + 1
    doc = {
        "_source": "Phantasie II ST: disk1/MAPS.INT (initial grids) + disk2/OUTn.DAT bytes 520..2999 (static)",
        "_note": ("26x20 grid stored column-major as (25 - x) * 20 + y; tiles here are [y][x] rows like Phantasie "
                  "I's overworld.json (DOS y * 26 + x). Feature x/y are converted from the stored (y, 25 - x). "
                  "Fog encodes as tile + 120. FORMAT.md section 9."),
        "grid_width": W,
        "grid_height": H,
        "tile_rules": CLASS_RULES,
        "tile_counts_unfogged": dict(sorted((str(k), v) for k, v in counts.items())),
        "dungeon_entrance_checks": checks,
        "regions": regions,
    }
    p1 = write_json("maps", "overworld.json", doc)
    p2 = write_json("maps", "maps-int.json", {
        "_source": "disk1/MAPS.INT (8840 B = 17 x 520) vs disk2/OUTn.DAT grids (played state, 1995-10-06)",
        "_note": "UTILIT2.PRG 0xb2c copies these blocks over OUTn.DAT bytes 0..519 on START NEW GAME [C]. Only "
                 "regions 1 and 2 start unfogged.",
        "region_order": REGIONS,
        "regions": init,
    })
    print("wrote", rel(p1))
    print("wrote", rel(p2))
    print(f"wrote ascii/png renders for {len(REGIONS)} regions; {len(checks)} dungeon entrances checked")


if __name__ == "__main__":
    main()
