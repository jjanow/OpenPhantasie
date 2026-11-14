#!/usr/bin/env python3
"""Build Phantasie III's Apple II graphics pack manifest (phantasie.graphics-pack/v1).

Reads  reverse-engineering/apple2/phantasie3/extracted/graphics/extraction-index.json (extract_a2_p3_graphics.py)
Writes src/OpenPhantasie.Data/GameData/phantasie3/graphics/apple2.json

The pack holds only what the Apple version has (GRAPHICS.md): monsters (MONSTER SHAPES sprites), party figures, the
overworld tiles and dungeon marks (SHAPES), and, from an original copy (not the [cr] crack, whose title and ending
are the cracker's), the title and the gods' room of the ending. Text: the remaster's 5x7 font (the Apple's hi-res font,
extracted to font/, lacks '.', '!' and more). Anything else falls back to the Client's plain drawing. Run from the repo
root: ``python3 -I tools/scripts/build_a2_p3_asset_manifest.py``. The importer's port is
src/OpenPhantasie.Importer/Pipelines/P3Apple2/P3Apple2ArtManifest.cs (byte-equal output, pinned by a test).
"""
from __future__ import annotations

import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
ROOT = "reverse-engineering/apple2/phantasie3/extracted/graphics"
OUT = REPO / "src/OpenPhantasie.Data/GameData/phantasie3/graphics/apple2.json"


def pack(index: dict) -> dict:
    for need in ("monsters", "portraits", "tiles", "dungeon", "screens"):
        if not index.get(need):
            raise SystemExit(f"extraction-index.json has no {need}: run extract_a2_p3_graphics.py first")
    screens = index["screens"]
    pictures = {}
    if "title" in screens:
        pictures["title.cover"] = screens["title-picture"]["file"]
        pictures["title.credits"] = screens["title"]["file"]
    if "ending" in screens:
        pictures["scene.reward.good"] = screens["ending"]["file"]
        pictures["scene.reward.evil"] = screens["ending"]["file"]
    pictures["overworld.party"] = index["tiles"]["party"]["file"]
    out = {
        "_schema": "phantasie.graphics-pack/v1",
        "_note": "Apple II Phantasie III art (tools/scripts/extract_a2_p3_graphics.py + build_a2_p3_asset_manifest.py, "
                 "reverse-engineering/apple2/phantasie3/GRAPHICS.md). Monsters are MONSTER SHAPES sprites (raw hi-res "
                 "bytes, drawn by MONSTER PLOT at an odd byte column, as the game); party figures, map tiles and dungeon "
                 "marks are Applesoft shapes (SHAPES) at the game's HCOLOR; colour is an NTSC artifact model [~]. The "
                 "title and the ending's gods' room come only from an original copy: the [cr] copy's ICE DRAGON and "
                 "WIN PIC are the cracker's screens. Text: the remaster 5x7 font (the Apple's hi-res font, IMPTEXT, "
                 "lacks '.', '!', '/' and more; it is in font/). Screen kept at the remaster's 320 x 200 layout: Apple "
                 "pictures (280 x 192) are drawn 1:1 in it.",
        "id": "apple2",
        "game": "phantasie3",
        "platform": "apple2",
        "name": "Apple II",
        "auto_select": True,
        "root": ROOT,
        "requires": ["monsters", "overworld/tiles", "dungeon"],
        "screen": {"width": 320, "height": 200, "pixel_aspect": [1, 1], "background": "#000000"},
        "font": "remaster-5x7",
        "_tile_maps": "OUTDOORS3 line 30: map byte b draws shape 37 + INT(b / 10) in HCOLOR INT(1 + b / 20), 7 x 6 cells, "
                      "a 10 x 15 region at a time (OT files). Tiles here are keyed by the remaster's (DOS) map byte, drawn "
                      "as the Apple class the Apple's own maps give it (GRAPHICS.md section 5); tiles 300 + class are the "
                      "class pictures, which tile_swaps draws for the Netherworld's own terrain. PATHWAY / BRIDGE are drawn "
                      "in HCOLOR 4 (black) or are empty shapes: not visible, as on the Apple. DUNGPLOT / DUNGON3 5030: 3 x 3 "
                      "marks, only next to the party; wall 31; 185-199 32, 200-214 33, 215-217 drawn black (glyph 0), "
                      "218-220 shape 35, empty in III (glyph 0), 221+ 36; any other byte erases the cell (0); the box starts "
                      "white (DNGLD3 200: glyph 99, not yet seen); the party is the XDRAW cursor 30.",
        "tile_maps": {
            "overworld_tile": [7, 6],
            "overworld_view": [10, 15],
            "dungeon_glyph": [3, 3],
            "dungeon_unseen": 99,
            "dungeon_party": 30,
            "dungeon_default": 0,
            "dungeon_cells": [[0, 1, 31], [185, 199, 32], [200, 214, 33], [215, 217, 0], [218, 220, 0], [221, 255, 36]],
            "tile_swaps": index["tile_swaps"],
        },
    }
    if "title" in screens:
        out["title_sequence"] = {
            "_note": "PHANTASIE III (boot program): ICE DRAGON on hi-res page 1, a FOR-loop pause, then the 7 rows of "
                     "'HIT ANY KEY TO CONTINUE' POKEd in (lines 245-290) and a wait for a key; a key at any point goes on "
                     "(GOSUB 300). The pauses are BASIC FOR loops (time not decoded [?]): only the last screen is shown, "
                     "until a key.",
            "canvas": [280, 192],
            "background": "#000000",
            "steps": [{"picture": "title.credits"}],
        }
    out["pictures"] = pictures
    out["_portraits"] = ("portrait.{race}: the party figure (TOWN3 2350-2360: races 7+ shape 26, DWARF / HALFLING / GNOME "
                         "/ PIXIE / SPRITE 25, HUMAN / ELF 19, or 27 for a MONK / PRIEST / WIZARD; social class 3 turns "
                         "any but 25 into 4, which a race-keyed picture cannot show) with the arm 20 (idle) or 22 "
                         "(attacking). Every body by pose is in figures/.")
    out["picture_sets"] = [
        {"name": "monster.{n}.idle", "dir": "monsters/idle", "prefix": "{n:D2}-", "ext": ".png"},
        {"name": "monster.{n}.attacking", "dir": "monsters/attacking", "prefix": "{n:D2}-", "ext": ".png"},
        {"name": "portrait.{n}.idle", "dir": "portraits/idle", "prefix": "{n:D2}-", "ext": ".png"},
        {"name": "portrait.{n}.attacking", "dir": "portraits/attacking", "prefix": "{n:D2}-", "ext": ".png"},
        {"name": "overworld.tile.{n}", "dir": "overworld/tiles", "prefix": "{n:D3}.", "ext": ".png"},
        {"name": "dungeon.cell.{n}", "dir": "dungeon", "prefix": "glyph-{n}.", "ext": ".png"},
    ]
    return out


def main() -> None:
    index = json.loads((REPO / ROOT / "extraction-index.json").read_text())
    out = pack(index)
    for k, v in out["pictures"].items():
        if not (REPO / ROOT / v).is_file():
            raise SystemExit(f"{k}: {v} missing")
    OUT.write_text(json.dumps(out, indent=2) + "\n")
    print(f"wrote {OUT.relative_to(REPO)}")


if __name__ == "__main__":
    main()
