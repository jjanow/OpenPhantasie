#!/usr/bin/env python3
"""Phantasie III (DOS SSI 1988) dungeon records -> reverse-engineering/dos/phantasie3/extracted/dungeons.json.

Source: `DNGS.DAT`, 11 records x 4224 (0x1080) B, record = dungeon number - 1 (names = master strings 34..44)
(`reverse-engineering/dos/phantasie3/FORMAT.md` §9.5). Per record:

| Offset | Content | Tag |
|---|---|---|
| 0x000 | map 24 x 30, cell(x, y) = byte 24y + x | [C] |
| 0x2d0 | dungeon level L (0..5) | [C] |
| 0x2d1 | monster id for forced-fight cells 0x80..0x89 | [C] |
| 0x2db | 3 exits (destination, x, y) for exit cells 0xd7..0xd9 | [C] layout; destinations 29 / 31 / 41 = Light / Dark / Netherworld [C], others [?] |
| 0x2e4 | 35 event squares x 5 B for cells 0xdd..0xff: message (+1, bit 7 = once), choices, 3 program indices | [C] |
| 0x393 | event programs, 2-byte ops (bit 7 continue, bit 6 clear after use, bits 0-5 type 1..27) | [C] layout; types beyond 1-5 [?] |
| 0x593 | text pointers, u16 big-endian, text at pointer - 0x800 | [C]; the table runs up to the first text [~] |

Texts are NUL-terminated (latin-1). The meaning of the rest of the record past the texts is [?] and stays in raw_hex.

Run: python3 -I tools/scripts/extract_dos_p3_dungeons.py
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import extract_dos_p3_common as c  # noqa: E402
import extract_dos_p3_tables as t  # noqa: E402

COUNT, SIZE = 11, 0x1080
W, H = 24, 30
PTR, TEXT_BASE = 0x593, 0x800


def texts(rec: bytes) -> tuple[int, list[dict]]:
    """Pointer table from 0x593 up to the lowest text it points at [~]; zero entries are empty slots."""
    ptrs = []
    o = PTR
    end = SIZE
    while o + 1 < end:
        p = rec[o] << 8 | rec[o + 1]
        if p:
            assert TEXT_BASE <= p < TEXT_BASE + SIZE, f"pointer {p:#x} at {o:#x}"
            end = min(end, p - TEXT_BASE)
        ptrs.append(p)
        o += 2
    ptrs = ptrs[:(end - PTR) // 2]
    out = []
    for i, p in enumerate(ptrs):
        if not p:
            continue
        at = p - TEXT_BASE
        txt = rec[at:rec.index(b"\0", at)].decode("latin-1")
        out.append({"index": i, "offset": f"{at:#x}", "text": txt})
    return len(ptrs), out


# Dungeon n loads record letter - 'A', letter from DS:0x1c84 + 2*dungeon (FORMAT.md section 9.5, s82).
LETTERS = "JHEDACBKIFG"


def main() -> None:
    img, _, _ = c.unpack_root()
    strs = t.string_table(img)
    raw = c.read_file("DNGS.DAT")
    assert len(raw) == COUNT * SIZE
    out = []
    for n in range(COUNT):
        record = ord(LETTERS[n]) - ord("A")
        rec = raw[record * SIZE:(record + 1) * SIZE]
        cells = [list(rec[y * W:(y + 1) * W]) for y in range(H)]
        squares = []
        for k in range(35):
            b = rec[0x2E4 + 5 * k:0x2E9 + 5 * k]
            squares.append({"cell": 0xDD + k, "message": b[0] & 0x7F, "once": bool(b[0] & 0x80),
                            "choices": b[1], "programs": list(b[2:5])})
        slots, txt = texts(rec)
        out.append({
            "dungeon": n + 1, "record": record, "letter": LETTERS[n], "name": strs[33 + n + 1], "level": rec[0x2D0],
            "width": W, "height": H, "cells": cells,
            "forced_fights": list(rec[0x2D1:0x2DB]),
            "exits": [{"cell": 0xD7 + k, "destination": rec[0x2DB + 3 * k], "x": rec[0x2DC + 3 * k],
                       "y": rec[0x2DD + 3 * k]} for k in range(3)],
            "event_squares": squares,
            "event_program_hex": rec[0x393:0x593].hex(),
            "text_slots": slots, "texts": txt,
            "raw_hex": rec.hex(),
        })
    c.write_json("dungeons.json", {
        "_source": "DNGS.DAT 11 x 4224 B (FORMAT.md section 9.5; dungeon n = record LETTERS[n-1] - 'A'); field tags in tools/scripts/extract_dos_p3_dungeons.py",
        "_cells": {"0": "floor", "1": "wall [~]", "7": "secret door [~]",
                   "8-17": "random-encounter square: fight when FAR:0xdd8c > rand() % 100, monster 12L + (cell - 8) + "
                           "rand() % 3 (ovl4 0xeb5)",
                   "0x80-0x89": "forced fight, monster forced_fights[cell - 0x80]",
                   "0xb9 / 0xc8": "doors; 0xba-0xc6 / 0xc9-0xd5 doors with monsters behind (monster cell - 186 / "
                                  "cell - 201, plus 12L)",
                   "0xc7 / 0xd6": "special doors (event 0x2a) [?]",
                   "0xd7-0xd9": "exits (table 'exits')",
                   ">= 0xdd": "event squares (table 'event_squares')"},
        "dungeons": out,
    })
    print("dungeons", len(out), "texts", sum(len(d["texts"]) for d in out))


if __name__ == "__main__":
    main()
