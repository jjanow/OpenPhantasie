#!/usr/bin/env python3
"""Convert EmuTOS's 8x8 (s160) and 6x6 (s162) ST system fonts into II's remaster font files (``phantasie.font/v1``).

s162: ``fnt_st_6x6.c`` (the same tag) as a further argument writes ``emutos-st-6x6.json``: the small font GEM forms use
for ``te_font`` 5, the ending's OK button label. Its ``dat_table`` is one 1536 x 6 bit form (``form_width`` 192 bytes),
character c at bits 6c .. 6c + 5 (``F_MONOSPACE``, ``max_cell_width`` 6):

    gh api "repos/emutos/emutos/contents/bios/fnt_st_6x6.c?ref=VERSION_1_4" --jq .content | base64 -d > fnt_st_6x6.c
    python3 -I tools/scripts/emutos_st_font.py fnt_st_8x8.c fnt_st_6x6.c

Phantasie II on the Atari ST draws its alert boxes (AES ``form_alert``) and the ending's text (VDI ``v_gtext`` /
``v_justified``) in the TOS ROM's 8x8 system font; no font is on the disks (FORMAT.md 10.5, "Font"). The look was
measured on EmuTOS in Hatari (FORMAT.md 7.4, endgame.md 7.1), so the remaster ships EmuTOS's own 8x8 font
(user, s159). EmuTOS is GPL-2.0-or-later; the font's source and licence go beside the output
(``emutos-st-8x8.LICENSE.txt``).

Input: EmuTOS ``bios/fnt_st_8x8.c`` (tag VERSION_1_4, the EmuTOS the shots were taken on), e.g.

    gh api "repos/emutos/emutos/contents/bios/fnt_st_8x8.c?ref=VERSION_1_4" --jq .content | base64 -d > fnt_st_8x8.c
    python3 -I tools/scripts/emutos_st_font.py fnt_st_8x8.c

The C file's ``dat_table`` is one 2048 x 8 bitmap (``form_width`` 256 bytes, ``form_height`` 8, big-endian words, MSB =
left pixel); character c (0..255, monospaced 8 px: ``off_8x8_table``) is byte c of each row.

Output (next to II's ST pack manifest, where ``GraphicsPack`` looks for fonts):
  src/OpenPhantasie.Data/GameData/phantasie2/graphics/fonts/emutos-st-8x8.json         plain (alerts, the ending's lines)
  src/OpenPhantasie.Data/GameData/phantasie2/graphics/fonts/emutos-st-8x8-italic.json  VDI italic (the ending's pages)

Italic is VDI text effect 4 (F_SKEW) as EmuTOS draws it (``vdi/vdi_textblit.c`` / ``vdi_tblit.S``): from the bottom
row up, the font's ``skew`` mask 0x5555 is rotated left once per row and the row moves one pixel right whenever the
bit rotated out is set, so the rows from the bottom sit 0, 1, 1, 2, 2, 3, 3, 4 px right; the pen still moves 8 px a
character. The glyphs here are 12 px wide with that shift baked in. EmuTOS's ``v_gtext`` with top alignment (vertical
alignment 5) starts the skewed cell left_offset + right_offset = 1 + 3 = 4 px left of the given x (``vdi_text.c``), so
the slanted top row sits at x; the pack's page says so (``font_dx`` -4), not the font. Both II ending pages match the
EmuTOS shots pixel for pixel that way.

Code points: 0x20-0x7E as ASCII; 0x80-0xAF (the Atari ST character set's accented Latin letters and signs, as code
page 437 there except 0x9E = U+00DF) as Unicode. The rest (control pictures, Hebrew, Greek, maths) is left out.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
FONT_DIR = REPO / "src/OpenPhantasie.Data/GameData/phantasie2/graphics/fonts"
SOURCE = ("EmuTOS bios/fnt_st_8x8.c, tag VERSION_1_4 (commit 978e37569bff95841e42675d11fcc6799aad8483), "
          "https://github.com/emutos/emutos; Copyright (C) 2001-2021 The EmuTOS development team; GPL-2.0-or-later "
          "(emutos-st-8x8.LICENSE.txt beside this file)")
HEIGHT = 8
SKEW_MASK = 0x5555


def dat_table(c_source: str) -> bytes:
    body = re.search(r"dat_table\[\]\s*=\s*\{(.*?)\};", c_source, re.S)
    if not body:
        raise SystemExit("dat_table not found")
    words = [int(w, 16) for w in re.findall(r"0x([0-9a-fA-F]{4})", body.group(1))]
    if len(words) != 256 * HEIGHT // 2:
        raise SystemExit(f"dat_table has {len(words)} words, expected {256 * HEIGHT // 2}")
    return b"".join(w.to_bytes(2, "big") for w in words)


def code_point(byte: int) -> int | None:
    if 0x20 <= byte <= 0x7E:
        return byte
    if 0x80 <= byte <= 0xAF:
        return 0xDF if byte == 0x9E else ord(bytes([byte]).decode("cp437"))
    return None


def skew_shifts() -> list[int]:
    """Pixels each row (top to bottom) moves right under F_SKEW: the mask rotated before each row from the bottom."""
    mask, shift, shifts = SKEW_MASK, 0, []
    for _ in range(HEIGHT):
        shifts.append(shift)
        mask = ((mask << 1) | (mask >> 15)) & 0xFFFF
        if mask & 0x8000:
            shift += 1
    return list(reversed(shifts))


def glyph_rows(table: bytes, byte: int, italic: bool) -> list[str]:
    shifts = skew_shifts() if italic else [0] * HEIGHT
    width = 8 + max(shifts)
    rows = []
    for r in range(HEIGHT):
        bits = format(table[r * 256 + byte], "08b").replace("1", "#").replace("0", ".")
        rows.append(("." * shifts[r] + bits).ljust(width, "."))
    return rows


def build(table: bytes, italic: bool) -> dict:
    fid = "emutos-st-8x8-italic" if italic else "emutos-st-8x8"
    note = ("The Atari ST's 8x8 system font as EmuTOS has it: " + SOURCE + ". Converted by "
            "tools/scripts/emutos_st_font.py. Monospaced: every glyph 8 px wide, the pen moves 8. II ST draws its alert "
            "boxes (form_alert) and the ending's text in the TOS ROM's 8x8 font, which is not on the disks; the look was "
            "measured on EmuTOS (FORMAT.md 7.4, endgame.md 7.1).")
    if italic:
        note += (" VDI italic (F_SKEW, EmuTOS vdi_textblit.c / vdi_tblit.S): skew mask 0x5555 rotated once per row from "
                 "the bottom, the row moved 1 px right when the rotated-out bit is set: rows 0..7 sit 4, 3, 3, 2, 2, 1, 1, "
                 "0 px right in a 12-px cell; the pen still moves 8. Top-aligned v_gtext starts the cell 4 px left of its "
                 "x (left_offset 1 + right_offset 3, vdi_text.c vertical alignment 5): the pack's font_dx.")
    glyphs = {}
    for byte in range(256):
        cp = code_point(byte)
        if cp is None:
            continue
        glyphs[chr(cp)] = {"rows": glyph_rows(table, byte, italic), "advance": 8}
    return {
        "_schema": "phantasie.font/v1",
        "_note": note,
        "id": fid,
        "height": HEIGHT,
        "line_height": HEIGHT,
        "space_advance": 8,
        "fallback": "?",
        "fold_to_upper": False,
        "glyphs": glyphs,
    }


SOURCE_6X6 = ("EmuTOS bios/fnt_st_6x6.c, tag VERSION_1_4 (commit 978e37569bff95841e42675d11fcc6799aad8483), "
              "https://github.com/emutos/emutos; Copyright (C) 2001-2021 The EmuTOS development team; GPL-2.0-or-later "
              "(emutos-st-6x6.LICENSE.txt beside this file)")
SMALL = 6  # the 6x6 font: 6 rows, 6-px cells (max_cell_width 6), form_width 192 bytes = 256 x 6 bits


def rows_6x6(c_source: str) -> list[str]:
    """The 6x6 font's form as 6 rows of 1536 bits ('#' / '.'), character c at bits 6c .. 6c + 5 (F_MONOSPACE)."""
    body = re.search(r"dat_table\[\d*\]\s*=\s*\{(.*?)\};", c_source, re.S)
    if not body:
        raise SystemExit("dat_table not found")
    words = [int(w, 16) for w in re.findall(r"0x([0-9a-fA-F]{4})", body.group(1))]
    if len(words) != 192 * SMALL // 2:
        raise SystemExit(f"dat_table has {len(words)} words, expected {192 * SMALL // 2}")
    bits = "".join(format(w, "016b") for w in words).replace("1", "#").replace("0", ".")
    return [bits[r * 1536:(r + 1) * 1536] for r in range(SMALL)]


def build_6x6(rows: list[str]) -> dict:
    note = ("The Atari ST's 6x6 small system font as EmuTOS has it: " + SOURCE_6X6 + ". Converted by "
            "tools/scripts/emutos_st_font.py (s162). Monospaced: every glyph 6 px wide, the pen moves 6. II ST's GEM "
            "forms use it for G_TEXT / G_BOXTEXT with te_font 5 (the small font): the ending's OK button label "
            "(OUT.RSC tree 18, endgame.md 7.1); TOS's own is not on the disks. Checked against the s80 Hatari shot "
            "p2-endgame-filmon-1.png (the O and the K pixel for pixel).")
    glyphs = {}
    for byte in range(256):
        cp = code_point(byte)
        if cp is None:
            continue
        glyphs[chr(cp)] = {"rows": [r[byte * SMALL:(byte + 1) * SMALL] for r in rows], "advance": SMALL}
    return {
        "_schema": "phantasie.font/v1",
        "_note": note,
        "id": "emutos-st-6x6",
        "height": SMALL,
        "line_height": SMALL,
        "space_advance": SMALL,
        "fallback": "?",
        "fold_to_upper": False,
        "glyphs": glyphs,
    }


def write(font: dict) -> None:
    FONT_DIR.mkdir(parents=True, exist_ok=True)
    path = FONT_DIR / f"{font['id']}.json"
    path.write_text(json.dumps(font, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"{path.relative_to(REPO)}: {len(font['glyphs'])} glyphs")


def main() -> None:
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    for name in sys.argv[1:]:
        source = Path(name).read_text(encoding="latin-1")
        if re.search(r"dat_table\[576\]", source):  # fnt_st_6x6.c
            write(build_6x6(rows_6x6(source)))
            continue
        table = dat_table(source)
        for italic in (False, True):
            write(build(table, italic))


if __name__ == "__main__":
    main()
