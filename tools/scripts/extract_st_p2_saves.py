#!/usr/bin/env python3
"""Phantasie II (Atari ST): played-state files on disk 2 -> extracted/saves/saves.json (task 3b.8).

Every file here is played state (FAT date 1995-10-06 on the Pasti set; 1987-04-22 on TOSEC), not a shipped default.
Factory state is rebuilt by UTILIT2.PRG 0xb2c (START NEW GAME): OUTn.DAT grids from MAPS.INT, and Fdelete of
TWNS.DAT, GUILD.DAT, DNG.SAV, DNGX.SAV, SACK.DAT; PHANT.PRG then starts from TWNS.INT's templates (FORMAT.md §9.5).

  DNGX.SAV 2500 B  the live level buffer (PHANT.PRG reads/writes 2500 B at 0x5bdc / 0x5e2a), as DOS I
  DNG.SAV          u16 dungeon id + the dungeon-map screen rectangle (0x5d06 writes 2 + w*h/2 B; the TOSEC copy is
                   13378 B with id 1). The Pasti copy is 1024 B and equals DNGX.SAV's first 1024 B: its directory
                   entry shares DNGX.SAV's first cluster (cross-linked), so it is damaged, not a format.
  SACK.DAT 120 B   60 x u16 BE item ids (DOS I layout [~]; UTILIT2 0xcd8 zero-fills a 120-B sack buffer) - all zero
  TWNS.DAT         see extract_st_p2_towns.py (twns-dat-played.json); OUTn.DAT see extract_st_p2_maps.py
Run from the repo root: python3 -I tools/scripts/extract_st_p2_saves.py
"""
from __future__ import annotations

import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_st_p2_common import DUNGEONS, d2, rel, write_json  # noqa: E402

GW, GRID = 33, 33 * 38


def main() -> None:
    x = d2("DNGX.SAV")
    assert len(x) == 2500
    best = min(DUNGEONS, key=lambda n: sum(a != b for a, b in zip(d2(f"DNG{n}"), x)))
    lvl = d2(f"DNG{best}")
    changes = []
    for i, (a, b) in enumerate(zip(lvl, x)):
        if a != b:
            e = {"offset": i, "level": hex(a), "saved": hex(b)}
            if i < GRID:
                e.update({"x": i % GW, "y": i // GW})
                if 0x80 <= a <= 0x89 and b == 0x8A:
                    e["reading"] = "forced fight cleared (DOS 0x2a26 rule)"
            changes.append(e)
    dng_sav = d2("DNG.SAV")
    sack = d2("SACK.DAT")
    doc = {
        "_source": "Phantasie II ST disk2 save files (Pasti set, played state, 1995-10-06)",
        "DNGX.SAV": {
            "size": len(x),
            "dungeon": best,
            "differences_from_DNG": changes,
            "tail_after_2048_all_zero": not any(x[2048:]),
            "note": "= DNG%d with in-play rewrites; same meaning as DOS I DNGX.SAV (save-file-format.md) [C by "
                    "data for the fight cells, ~ for the rest]" % best,
        },
        "DNG.SAV": {
            "size": len(dng_sav),
            "equals_DNGX_first_1024": dng_sav == x[:1024],
            "note": "damaged (cross-linked with DNGX.SAV). Format from code + the TOSEC copy: u16 BE dungeon id, "
                    "then 13376 B of the 165x152 map-screen rectangle (13378 B in all, the Amiga I size too).",
        },
        "SACK.DAT": {
            "size": len(sack),
            "item_ids": list(struct.unpack(">60H", sack)),
            "all_zero": not any(sack),
        },
    }
    p = write_json("saves", "saves.json", doc)
    print("wrote", rel(p))


if __name__ == "__main__":
    main()
