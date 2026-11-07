#!/usr/bin/env python3
"""Build Phantasie II's Apple II graphics pack manifest (phantasie.graphics-pack/v1, as atari-st.json).

Reads  reverse-engineering/apple2/phantasie2/extracted/graphics/extraction-index.json (extract_a2_p2_graphics.py)
Writes src/OpenPhantasie.Data/GameData/phantasie2/graphics/apple2.json

The pack holds only what the Apple II version has on its disks (GRAPHICS.md): the monster and party shapes, the map
tiles and dungeon marks, two combat effects and the title picture. Not in the pack: a font (the Apple II's text is the
ROM character set, not on the disks; the remaster's 5x7 font stands in), palettes (hi-res colour is NTSC artifacting,
baked into the PNGs), the town / ending / defeat screens (the Apple shows text there, or draws them with BASIC:
CEMETARY2, WIN), sounds (speaker clicks from BASIC, not decoded). Keys the Client finds missing fall back to its
plain drawing. Run from the repo root: ``python3 -I tools/scripts/build_a2_p2_asset_manifest.py``.
"""
from __future__ import annotations

import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
ROOT = "reverse-engineering/apple2/phantasie2/extracted/graphics"
OUT = REPO / "src/OpenPhantasie.Data/GameData/phantasie2/graphics/apple2.json"


def main() -> None:
    index = json.loads((REPO / ROOT / "extraction-index.json").read_text())
    for need in ("monsters", "portraits", "tiles", "dungeon", "effects", "screens"):
        if not index.get(need):
            raise SystemExit(f"extraction-index.json has no {need}: run extract_a2_p2_graphics.py first")
    pack = {
        "_schema": "phantasie.graphics-pack/v1",
        "_note": "Apple II Phantasie II art (v2/5/86; tools/scripts/extract_a2_p2_graphics.py + build_a2_p2_asset_manifest.py, "
                 "reverse-engineering/apple2/phantasie2/GRAPHICS.md). Every picture is an Applesoft shape (SHAPES) drawn "
                 "at the game's SCALE / HCOLOR and turned into colour by an NTSC artifact model [~], except the title "
                 "picture (ICE DRAGON). The Apple's text is the ROM font (not on the disks): the remaster 5x7 font "
                 "stands in. Screen kept at the remaster's 320 x 200 layout: Apple pictures (280 x 192 hi-res pixels) "
                 "are drawn 1:1 in it. Map: the Apple draws its 20 x 26 map rotated against the remaster's 26 x 20 "
                 "grid (FORMAT.md overworld); here the tiles follow the remaster's grid, 22 x 20 cells of 7 x 6. A tile's "
                 "colour fringes depend on its screen column on the Apple: the pack uses the even-column look.",
        "id": "apple2",
        "game": "phantasie2",
        "platform": "apple2",
        "name": "Apple II",
        "auto_select": True,
        "root": ROOT,
        "requires": ["monsters", "overworld/tiles", "dungeon"],
        "screen": {"width": 320, "height": 200, "pixel_aspect": [1, 1], "background": "#000000"},
        "font": "remaster-5x7",
        "_tile_maps": "OUTDOORS2 5030-5040: map byte b < 120 draws shape 37 + b / 10 (7 x 6 cell); b >= 120 is fog, not "
                      "drawn (black tiles 120-255). DUNGON2 5025-5062: only cells next to the party are drawn; 4 x 4 marks: "
                      "bytes 0-1 wall (31), 185-199 (32), 200-214 (33), 215-217 drawn in HCOLOR 0 (nothing: glyph 0), "
                      "218-220 (35), 221-255 (36), every other byte erases the cell (glyph 0); the party is the XDRAW "
                      "cursor 30. Glyph numbers are the shape numbers.",
        "tile_maps": {
            "overworld_tile": [7, 6],
            "overworld_view": [22, 20],
            "dungeon_glyph": [4, 4],
            "dungeon_unseen": 0,
            "dungeon_party": 30,
            "dungeon_default": 0,
            "dungeon_cells": [[0, 1, 31], [185, 199, 32], [200, 214, 33], [215, 217, 0], [218, 220, 35],
                              [221, 255, 36]],
        },
        "title_sequence": {
            "_note": "PHANTASIE II (boot program): a text page (SSI PRESENTS / PHANTASIE II / DESCENT INTO THE "
                     "NETHERWORLD, ROM font), then ICE DRAGON on hi-res page 2 with HPLOT lightning flashing on page 1 "
                     "(random lines, lines 50-96), then the 'HIT ANY KEY TO CONTINUE' rows POKEd in (243-257) and a wait "
                     "for a key; a key at any point goes on (line 300). The pauses are BASIC FOR loops (time not "
                     "decoded [?]) and the lightning is random, so only the last screen is shown, until a key.",
            "canvas": [280, 192],
            "background": "#000000",
            "steps": [{"picture": "title.credits"}],
        },
        "pictures": {
            "title.cover": index["screens"]["ice-dragon"]["file"],
            "title.credits": index["screens"]["title"]["file"],
            "combat.sparkle": index["effects"]["23"]["file"],
            "combat.rock": index["effects"]["59"]["file"],
            "overworld.party": index["tiles"]["party"]["file"],
        },
        "_portraits": "portrait.{race}: the party figure (TOWN2 2350-2356: races 7+ shape 26, DWARF / HALFLING / GNOME / "
                      "PIXIE / SPRITE 25, HUMAN / ELF 19, or 27 for a MONK / PRIEST / WIZARD, which a race-keyed picture "
                      "cannot show) with the arm overlay 20 (idle) or 22 (attacking). The figures by body and pose are "
                      "in figures/.",
        "picture_sets": [
            {"name": "monster.{n}.idle", "dir": "monsters/idle", "prefix": "{n:D2}-", "ext": ".png"},
            {"name": "monster.{n}.attacking", "dir": "monsters/attacking", "prefix": "{n:D2}-", "ext": ".png"},
            {"name": "portrait.{n}.idle", "dir": "portraits/idle", "prefix": "{n:D2}-", "ext": ".png"},
            {"name": "portrait.{n}.attacking", "dir": "portraits/attacking", "prefix": "{n:D2}-", "ext": ".png"},
            {"name": "overworld.tile.{n}", "dir": "overworld/tiles", "prefix": "{n:D3}.", "ext": ".png"},
            {"name": "dungeon.cell.{n}", "dir": "dungeon", "prefix": "glyph-{n}.", "ext": ".png"},
        ],
    }
    for k, v in pack["pictures"].items():
        if not (REPO / ROOT / v).is_file():
            raise SystemExit(f"{k}: {v} missing")
    OUT.write_text(json.dumps(pack, indent=2) + "\n")
    print(f"wrote {OUT.relative_to(REPO)}")


if __name__ == "__main__":
    main()
