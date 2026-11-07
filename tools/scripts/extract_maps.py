#!/usr/bin/env python3
"""Extract overworld (OUT*.DAT) and dungeon (DNG*) map files.

Overworld geometry (OV2.OVR FUN_538B / FUN_6214) [C]:
  - bytes 0x000..0x207: 26×20 tile grid (520 bytes)
  - bytes 0x208..0x252: 15 feature records × 5 bytes (x, y, arg, dest_x, dest_y)
  - bytes 0x253..0x3FF: 40-byte text pages

Dungeon files: 33×38 walkable grid (1254 bytes) + side tables + embedded text.
Geometry confirmed via OV2 FUN_0000_20c3 / FUN_0000_1f05 (stride 0x21, height 0x26).

Output: reverse-engineering/dos/extracted/overworld.json, dungeons.json
"""
from __future__ import annotations

import re

from re_common import read, write_json

OVERWORLD = [f"OUT{i}.DAT" for i in list(range(1, 17)) + [18, 19]]
DUNGEONS = [f"DNG{i}" for i in range(1, 11)]

OW_WIDTH = 26
OW_HEIGHT = 20
OW_GRID_BYTES = OW_WIDTH * OW_HEIGHT  # 520
OW_FEATURE_OFF = 0x208
OW_FEATURE_COUNT = 15
OW_FEATURE_SIZE = 5
OW_TEXT_OFF = 0x253
OW_TEXT_PAGE = 40

ASCII_RUN = re.compile(rb"[ -~]{4,}")


def _is_text(s: str) -> bool:
    if " " not in s or len(s) < 6:
        return False
    upper = sum(c.isupper() or c == " " for c in s)
    return upper / len(s) >= 0.6 and len(set(s)) >= 4


def text_runs(data: bytes):
    out = []
    for m in ASCII_RUN.finditer(data):
        s = m.group().decode("latin1")
        if _is_text(s):
            out.append({"offset": f"0x{m.start():04x}", "text": s.strip()})
    return out


def parse_overworld(data: bytes, name: str) -> dict:
    if len(data) != 1024:
        raise ValueError(f"{name}: expected 1024 bytes, got {len(data)}")

    grid = data[:OW_GRID_BYTES]
    tiles = [list(grid[r:r + OW_WIDTH]) for r in range(0, OW_GRID_BYTES, OW_WIDTH)]

    features = []
    feat_blob = data[OW_FEATURE_OFF:OW_FEATURE_OFF + OW_FEATURE_COUNT * OW_FEATURE_SIZE]
    for i in range(OW_FEATURE_COUNT):
        x, y, arg, dx, dy = feat_blob[i * 5:(i + 1) * 5]
        # Trailing unused slots often repeat (25,0,0,0,0) on ocean/wall corners.
        features.append({
            "index": i,
            "x": x,
            "y": y,
            "arg": arg,
            "dest_x": dx,
            "dest_y": dy,
        })

    text_blob = data[OW_TEXT_OFF:]
    pages = []
    for i in range(0, len(text_blob) // OW_TEXT_PAGE):
        page = text_blob[i * OW_TEXT_PAGE:(i + 1) * OW_TEXT_PAGE]
        # Pages are often space-padded with a trailing NUL.
        raw = page.split(b"\x00", 1)[0].decode("latin1")
        text = raw.strip()
        if text:
            # s153: "raw" = the page as stored up to its NUL. Its leading blanks place the text in I Amiga's requester-23
            # box (h65 +0x1f4 centres by strlen); DOS and Amiga pages 0-9 are byte-equal in every region.
            pages.append({"index": i, "text": text, "raw": raw})

    return {
        "size": len(data),
        "grid_width": OW_WIDTH,
        "grid_height": OW_HEIGHT,
        "tiles": tiles,
        "features": features,
        "text_pages": pages,
        "embedded_text": text_runs(data),  # legacy/debug: raw ASCII runs
    }


def main():
    ow = {
        "_source": "OUT*.DAT (SSI 1987 IBM/DOS overworld regions)",
        "_note": "26×20 tile grid + 15 feature records + 40-byte text pages "
                 "(OV2.OVR FUN_538B / FUN_6214). Fog encodes as tile+120.",
        "grid_width": OW_WIDTH,
        "grid_height": OW_HEIGHT,
        "regions": {},
    }
    for f in OVERWORLD:
        region_id = int("".join(ch for ch in f if ch.isdigit()))
        parsed = parse_overworld(read(f), f)
        parsed["region_id"] = region_id
        ow["regions"][f] = parsed
    p1 = write_json("overworld.json", ow)
    print(f"wrote {p1} ({len(ow['regions'])} regions, {OW_WIDTH}x{OW_HEIGHT})")

    dn = {
        "_source": "DNG1..DNG10 (SSI 1987 IBM/DOS dungeon levels)",
        "_note": "raw feature bytes; [C] grid = first 1254 B = 33x38, then side tables / events / scripts (dungeon-representation.md).",
        "levels": {},
    }
    for f in DUNGEONS:
        d = read(f)
        dn["levels"][f] = {
            "id": f"p1:{f[3:]}",  # game-prefixed id = level number (Phase 09 T1.2)
            "size": len(d),
            "raw_hex": d.hex(),
            "embedded_text": text_runs(d),
        }
    p2 = write_json("dungeons.json", dn)
    print(f"wrote {p2} ({len(dn['levels'])} levels)")


if __name__ == "__main__":
    main()
