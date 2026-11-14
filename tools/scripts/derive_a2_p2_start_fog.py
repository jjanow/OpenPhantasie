#!/usr/bin/env python3
"""Derive Phantasie II's start fog per region for the Apple-only import (D2-052, user s209).

The start fog of a new game is the ST's `MAPS.INT` (UTILIT2.PRG 0xb2c copies its 17 x 520-B grids over every
OUTn.DAT; `reverse-engineering/atari-st/phantasie2/FORMAT.md` 9.1). The Apple's OUTTn grids hold the same bytes
(fog = tile + 120; `reverse-engineering/apple2/phantasie2/FORMAT.md` 3.6), and the Memorial Set's unplayed Apple disk
has the start fog (s167). This reads both, checks that every region is all open or all fogged in each, and that the
two agree, then prints the `start_fog` block of `src/OpenPhantasie.Data/GameData/phantasie2/apple2-import.json`.

Usage (repo root): python3 -I tools/scripts/derive_a2_p2_start_fog.py [--check]
  --check: exit 1 unless apple2-import.json's start_fog equals the derived one.
"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from a2_rdos import RdosDisk  # noqa: E402
from extract_st_p2_common import DISK1, FOG, REGIONS, REPO  # noqa: E402

MEMORIAL_A = REPO / "reverse-engineering/apple2/original-images/phantasie2/memorial-set-gog/PHANT2A.DSK"
IMPORT_JSON = REPO / "src/OpenPhantasie.Data/GameData/phantasie2/apple2-import.json"
GRID = 520


def classify(grid: bytes, what: str) -> bool:
    """True when every cell is fogged, False when none is; anything else is an error (a list per region is wrong)."""
    fogged = sum(FOG <= b < 255 for b in grid)
    if fogged not in (0, len(grid)):
        sys.exit(f"{what}: {fogged} of {len(grid)} cells fogged; the start fog is not all-or-nothing per region")
    return fogged == len(grid)


def derive() -> dict:
    maps_int = (DISK1 / "MAPS.INT").read_bytes()
    assert len(maps_int) == GRID * len(REGIONS), len(maps_int)
    disk = RdosDisk(str(MEMORIAL_A))
    out = {"open": [], "fogged": []}
    for i, n in enumerate(REGIONS):
        st = classify(maps_int[i * GRID:(i + 1) * GRID], f"MAPS.INT region {n}")
        a2 = classify(disk.read(f"OUTT{n}")[:GRID], f"Memorial OUTT{n}")
        if st != a2:
            sys.exit(f"region {n}: MAPS.INT {'fogged' if st else 'open'}, Memorial Apple {'fogged' if a2 else 'open'}")
        out["fogged" if st else "open"].append(f"OUT{n}.DAT")
    return out


def main(argv: list[str]) -> int:
    fog = derive()
    if "--check" in argv:
        have = json.loads(IMPORT_JSON.read_text())["start_fog"]
        if have != fog:
            print(f"apple2-import.json start_fog differs: {have} != {fog}")
            return 1
        print("start_fog matches MAPS.INT and the Memorial Set's OUTTn")
        return 0
    print(json.dumps(fog))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
