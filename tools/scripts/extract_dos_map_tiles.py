#!/usr/bin/env python3
"""Extract DOS Phantasie I map tile graphics to PNG (P0.7).

Sources (format: reverse-engineering/dos/logic-specs/map-tile-graphics.md):
  * ``PAT`` (450 B)  — 15 overworld patterns, 12x10 px, CGA 2 bpp, 3 bytes/row,
    selected by OV2 0x54fa (pattern = tile / 10; tile 41 -> 13; OUT18/19 class 6/7 -> 14;
    pattern 12 = party marker, 0x53c5).
  * ``OV2.OVR`` file 0x7e2e — 3-byte fog row (all rows of a fogged cell).
  * ``OV2.OVR`` file 0x7d5d (DS 0xc09a) — 8 dungeon map glyphs 0xe6..0xed, 5x4 px,
    one byte per pixel (colour index), selected by OV2 0x21df.

Palette: CGA palette 1 high intensity as DOSBox shows it (capture s10).

Writes reverse-engineering/dos/extracted/tiles/{overworld,dungeon}/*.png and tiles.json.
"""
from __future__ import annotations

import json

from PIL import Image

from re_common import OUT, read

PALETTE = [(0, 0, 0), (0x55, 0xFF, 0xFF), (0xFF, 0x55, 0xFF), (0xFF, 0xFF, 0xFF)]
PALETTE_HEX = ["#%02X%02X%02X" % c for c in PALETTE]

OW_W, OW_H, OW_BYTES = 12, 10, 30
OW_PATTERNS = 15
FOG_OFFSET = 0x7E2E          # OV2 file offset of the 3-byte fog row (imm 0xc16b)
GLYPH_OFFSET = 0xC09A - 0x433D  # OV2 file 0x7d5d
GLYPH_W, GLYPH_H = 5, 4
GLYPH_IDS = list(range(0xE6, 0xEE))

PATTERN_NAMES = {
    0: "class0", 1: "class1", 2: "class2", 3: "class3", 4: "town", 5: "dungeon",
    6: "class6-path", 7: "class7-bridge", 8: "class8", 9: "class9", 10: "water",
    11: "pool", 12: "party", 13: "inn", 14: "realm",
}
GLYPH_NAMES = {
    0xE6: "wall", 0xE7: "floor", 0xE8: "event", 0xE9: "lock-v",
    0xEA: "lock-h", 0xEB: "exit", 0xEC: "stairs", 0xED: "party",
}


def cga_rows(rows: list[bytes], width: int) -> Image.Image:
    im = Image.new("RGBA", (width, len(rows)))
    for y, row in enumerate(rows):
        for x in range(width):
            b = row[x // 4]
            im.putpixel((x, y), PALETTE[(b >> (6 - 2 * (x % 4))) & 3] + (255,))
    return im


def main() -> None:
    pat = read("PAT")
    ov2 = read("OV2.OVR")
    assert len(pat) == OW_PATTERNS * OW_BYTES, len(pat)
    root = OUT / "tiles"
    (root / "overworld").mkdir(parents=True, exist_ok=True)
    (root / "dungeon").mkdir(parents=True, exist_ok=True)

    patterns = []
    for p in range(OW_PATTERNS):
        blob = pat[p * OW_BYTES:(p + 1) * OW_BYTES]
        rows = [blob[r * 3:r * 3 + 3] for r in range(OW_H)]
        name = f"overworld/pat-{p:02d}-{PATTERN_NAMES[p]}.png"
        cga_rows(rows, OW_W).save(root / name)
        patterns.append({"index": p, "image": name})

    fog = ov2[FOG_OFFSET:FOG_OFFSET + 3]
    cga_rows([fog] * OW_H, OW_W).save(root / "overworld/fog.png")

    glyphs = []
    for i, gid in enumerate(GLYPH_IDS):
        px = ov2[GLYPH_OFFSET + i * 20:GLYPH_OFFSET + (i + 1) * 20]
        assert all(v < 4 for v in px), (hex(gid), px)
        im = Image.new("RGBA", (GLYPH_W, GLYPH_H))
        for k, v in enumerate(px):
            im.putpixel((k % GLYPH_W, k // GLYPH_W), PALETTE[v] + (255,))
        name = f"dungeon/glyph-{gid:02x}-{GLYPH_NAMES[gid]}.png"
        im.save(root / name)
        glyphs.append({"glyph": gid, "image": name})

    meta = {
        "_source": "DOS PAT (450 B) + OV2.OVR fog row 0x7e2e + glyph table 0x7d5d; "
                   "see logic-specs/map-tile-graphics.md",
        "palette": PALETTE_HEX,
        "overworld": {"tile_width": OW_W, "tile_height": OW_H, "fog_image": "overworld/fog.png",
                      "patterns": patterns},
        "dungeon": {"glyph_width": GLYPH_W, "glyph_height": GLYPH_H, "glyphs": glyphs},
    }
    (root / "tiles.json").write_text(json.dumps(meta, indent=2) + "\n")
    print(f"wrote {len(patterns)} patterns + fog + {len(glyphs)} glyphs to {root}")


if __name__ == "__main__":
    main()
