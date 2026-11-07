#!/usr/bin/env python3
"""Extract Phantasie IV's (X68000) own ANK fonts from P4.X into the IV pack's font files (``phantasie.font/v1``), s135.

P4.X carries three glyph tables of its own (FORMAT.md 6.12); the Shift-JIS kanji come from the CG ROM (`_wkfnt`) and are
not extracted. `_wfinit(n)` (P4.X image 0x36388) points `_fstl` (0x3A2FC) at table n and copies two bytes after it into
`_xlen` (0x3A2F5) and `_ylen` (0x3A2F8):

  font 0  table 0x36E8A, glyphs 0x36410-0x36E89, _xlen 3, _ylen 16   (the window font: _font(0, ...) everywhere)
  font 1  table 0x37BB6, glyphs 0x3720D-0x37BB5, _xlen 3, _ylen 16   (a thin variant: _page1, _dsp_spell, _endstory, ...)
  font 2  table 0x39902, glyphs 0x37F39-0x39901, _xlen 5, _ylen 32   (32 x 32 item icons, `_itemlist` / `_dvitmlst` / `_tritmlst`; not a text font)

Each table is 224 longword pointers (character 0x20 + i). A glyph is 4 header bytes (b0 = bytes per bitmap row: 1, 2
or 4; b1 = rows; b2 = pen advance; b3 = 0) and b1 rows, top-aligned (lower case and descenders simply have more rows).
`_wfnt` (0x39C86) builds the glyph in a 32-bit-per-row buffer and applies the style bits of `_wfopt` (0x3A2EE, the
second argument of `_font`): bit 0 bold (row |= row >> 1, advance + 1, `0x3A034`), bit 1 italic (`0x3A096`: d = rows / 2;
each pair of rows from the top is shifted right by d, then d - 1, ... until d reaches 0; the advance is unchanged, so the
slant overhangs the next cell), bit 2 outline (+2 rows, +2 advance) and bit 3 shadow (`0x3A0C2` / `0x3A236`, not
extracted). The pixels are written only where the glyph has ink (transparent), in `_fgcol`.

Output (next to the pack manifest, where `GraphicsPack` looks for fonts):
  src/OpenPhantasie.Data/GameData/phantasie4/graphics/fonts/x68k-p4-font0.json         font 0, style 0
  src/OpenPhantasie.Data/GameData/phantasie4/graphics/fonts/x68k-p4-font0-italic.json  font 0, style 2 (`_dsp_exit`'s "Exit")
  src/OpenPhantasie.Data/GameData/phantasie4/graphics/fonts/x68k-p4-font0-bold.json    font 0, style 1 (`_tload`'s banner, s136)
  src/OpenPhantasie.Data/GameData/phantasie4/graphics/fonts/x68k-p4-font1.json         font 1, style 0
and preview sheets reverse-engineering/x68000/phantasie4/extracted/graphics/fonts/<id>.png. Font 2 (the item icons:
`_itemlist` / `_dvitmlst` / `_tritmlst` draw `_wputc(win, _idat[item].b0 + 0x20)` in it, colour 0) goes to
reverse-engineering/x68000/phantasie4/extracted/graphics/ui/items/NN.png (NN = `_idat` b0, 0-49; ink in colour 0 =
black, the rest transparent).

Code points: 0x20-0x7E as ASCII, 0xA1-0xDF (JIS X 0201 katakana) as U+FF61-U+FF9F; the font's own symbols (font 0's
0x7B / 0x7D circled L / F, both fonts' 0x7F block, 0x80-0xA0 and 0xE0-0xFF) as U+E000 + byte. Glyphs without ink are
left out (but the space). Usage: x68k_p4_fonts.py
"""
from __future__ import annotations

import json
import struct
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
P4X = REPO / "reverse-engineering/x68000/phantasie4/unpacked/disk1/P4.X"
FONT_DIR = REPO / "src/OpenPhantasie.Data/GameData/phantasie4/graphics/fonts"
PREVIEW_DIR = REPO / "reverse-engineering/x68000/phantasie4/extracted/graphics/fonts"
HU_HEADER = 0x40

TABLES = {0: 0x36E8A, 1: 0x37BB6}
ICON_TABLE, ICON_COUNT = 0x39902, 50
ICON_DIR = REPO / "reverse-engineering/x68000/phantasie4/extracted/graphics/ui/items"
ITALIC = 2
BOLD = 1


def image() -> bytes:
    return P4X.read_bytes()[HU_HEADER:]


def glyph(img: bytes, table: int, byte: int) -> tuple[list[int], int]:
    """Rows as 32-bit masks (MSB = left pixel) and the pen advance, as `_wfnt` loads them (style 0)."""
    p = struct.unpack_from(">I", img, table + 4 * (byte - 0x20))[0]
    per_row, rows, advance, _ = img[p:p + 4]
    if per_row not in (1, 2, 4):
        raise ValueError(f"glyph 0x{byte:02X}: {per_row} bytes per row")
    out = [int.from_bytes(img[p + 4 + r * per_row:p + 4 + (r + 1) * per_row], "big") << (32 - 8 * per_row)
           for r in range(rows)]
    return out, advance


def styled(rows: list[int], advance: int, style: int) -> tuple[list[int], int]:
    """`_wfnt`'s bold (bit 0) and italic (bit 1) passes, in its order (0x3A034, then 0x3A096)."""
    rows = list(rows)
    if style & BOLD:
        rows = [r | (r >> 1) for r in rows]
        advance += 1
    if style & ITALIC:
        d, i = len(rows) // 2, 0
        while d:
            for _ in range(2):
                if i < len(rows):
                    rows[i] >>= d
                i += 1
            d -= 1
    return rows, advance


def code_point(byte: int, font: int) -> int:
    if byte == 0x7F or (font == 0 and byte in (0x7B, 0x7D)):
        return 0xE000 + byte
    if 0x20 <= byte <= 0x7E:
        return byte
    if 0xA1 <= byte <= 0xDF:
        return 0xFF61 + byte - 0xA1
    return 0xE000 + byte


def key(cp: int) -> str:
    return chr(cp) if 0x20 <= cp <= 0x7E else f"U+{cp:04X}"


def build(img: bytes, font: int, style: int, fid: str, note: str) -> dict:
    table = TABLES[font]
    height = img[table + 896 + 2]  # _ylen
    glyphs = {}
    for byte in range(0x20, 0x100):
        rows, advance = styled(*glyph(img, table, byte), style)
        if byte != 0x20 and not any(rows):
            continue
        width = max((32 - (r & -r).bit_length() + 1 for r in rows if r), default=0)
        width = max(width, 1)
        text = ["".join("#" if r >> (31 - c) & 1 else "." for c in range(width)) for r in rows]
        text += ["." * width] * (height - len(text))
        glyphs[key(code_point(byte, font))] = {"rows": text, "advance": advance}
    return {
        "_schema": "phantasie.font/v1",
        "_note": note,
        "id": fid,
        "height": height,
        "line_height": height,
        "space_advance": glyphs[" "]["advance"],
        "fallback": "?",
        "fold_to_upper": False,
        "glyphs": glyphs,
    }


def preview(font: dict, path: Path) -> None:
    from PIL import Image
    cells = list(font["glyphs"].values())
    h = font["height"]
    cols = 16
    img = Image.new("RGB", (cols * 26, ((len(cells) + cols - 1) // cols) * (h + 4)), (40, 40, 40))
    for i, g in enumerate(cells):
        x0, y0 = (i % cols) * 26 + 1, (i // cols) * (h + 4) + 1
        for y, row in enumerate(g["rows"]):
            for x, ch in enumerate(row):
                if ch == "#":
                    img.putpixel((x0 + x, y0 + y), (255, 255, 255))
        for x in range(g["advance"]):
            img.putpixel((x0 + x, y0 + h + 1), (120, 120, 120))
    path.parent.mkdir(parents=True, exist_ok=True)
    img.resize((img.width * 3, img.height * 3), Image.NEAREST).save(path)


def icons(img: bytes) -> None:
    """Font 2's 32 x 32 glyphs 0x20 + b0 as PNGs (ink black, transparent elsewhere)."""
    from PIL import Image
    ICON_DIR.mkdir(parents=True, exist_ok=True)
    for b0 in range(ICON_COUNT):
        rows, _ = glyph(img, ICON_TABLE, 0x20 + b0)
        pic = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
        for y, r in enumerate(rows):
            for x in range(32):
                if r >> (31 - x) & 1:
                    pic.putpixel((x, y), (0, 0, 0, 255))
        pic.save(ICON_DIR / f"{b0:02d}.png")
    print("item icons", ICON_COUNT)


def main() -> None:
    img = image()
    icons(img)
    common = ("Phantasie IV X68000, P4.X's own glyph table {t} (_wfinit({f}), FORMAT.md 6.12); extracted by "
              "tools/scripts/x68k_p4_fonts.py. Proportional: each glyph's pen advance is P4.X's. Rows are top-aligned "
              "in the {h}-row cell as _wfnt draws them.")
    jobs = [
        (0, 0, "x68k-p4-font0", "The window font (_font(0, 0, ...)): the Mystic's bubble, every window's ANK text."),
        (0, ITALIC, "x68k-p4-font0-italic",
         "Style 2 (_wfopt bit 1, italic: rows sheared right in pairs by rows/2 ... 1, advance unchanged): _dsp_exit's "
         "'Exit' on the fight's exit square (_font(-1, 2, 15, 16)); matches the XEiJ shot s120 pixel for pixel."),
        (0, BOLD, "x68k-p4-font0-bold",
         "Style 1 (_wfopt bit 0, bold: row |= row >> 1, advance + 1): _tload's town name on the street banner "
         "(_font(0, 1, 15, 16)) and 'Congraturation!' after the ending; matches the XEiJ shot s135 (ending street). "
         "_fsize centres with the plain advances (advance - 1 here), s136."),
        (1, 0, "x68k-p4-font1", "Font 1 (_font(1, ...)): the thin variant (_page1, _dsp_spell, _getmagic, _endstory, ...)."),
    ]
    FONT_DIR.mkdir(parents=True, exist_ok=True)
    for font, style, fid, extra in jobs:
        table = TABLES[font]
        data = build(img, font, style, fid,
                     common.format(t=f"0x{table:05X}", f=font, h=img[table + 896 + 2]) + " " + extra)
        (FONT_DIR / f"{fid}.json").write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
        preview(data, PREVIEW_DIR / f"{fid}.png")
        print(fid, len(data["glyphs"]), "glyphs")


if __name__ == "__main__":
    main()
