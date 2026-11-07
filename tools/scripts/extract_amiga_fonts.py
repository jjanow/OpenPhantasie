#!/usr/bin/env python3
"""Extract the two Amiga fonts Phantasie I draws with into the pack's font files (``phantasie.font/v1``).

The game names both in hunk 78 (file 0x20860, Amiga FORMAT.md "Fonts"):
  - TextAttr h78 +0x36 = { "z6font.font", 6, 0, FPF_ROMFONT }: the boot code opens it (h77 +0x13e, stub h233 =
    diskfont LVO -30 OpenDiskFont) and sets it on the window's RastPort (h77 +0x158, stub h236 +0x28 = graphics LVO -66
    SetFont), so plain Text() on the window uses it. The h84 IntuiTexts (+0x1d2a, 20 B each) name it, all but three
    (123, 184, 194: requesters 14, 21 and 22's ReqText, which name topaz; requester 21's is empty on the s132 shots);
    so does h81 +0x5e (pen 0, TopEdge 185). The font is on the game disk: ``fonts/z6font/6`` (a DiskFontHeader hunk).
  - TextAttr h78 +0x22 = { "topaz.font", 8, 0, FPF_ROMFONT }: the screen's NewScreen.Font (h78 +0x4e, "ZOLI'S SCREEN"),
    so the screen title / menu bar, and the menu items' IntuiTexts (h81 +0x36 / +0x4a). Topaz 8 is the Kickstart ROM's
    own font (graphics.library); it is read from the user-supplied KS 1.3 image
    (``original-images/kickstart/kick34005.A500``), whose font is an InitStruct table at ROM 0x910a.

Both are fixed-width (no FPF_PROPORTIONAL, no CharSpace / CharKern): each character's bits (CharLoc offset / width)
are drawn at the pen and the pen moves tf_XSize. A character the font leaves out (width 0) is drawn as the default
glyph (the entry after HiChar); the files list only the characters the font has, plus that default as the fallback.

Output: ``src/OpenPhantasie.Data/GameData/phantasie1/graphics/fonts/<id>.json`` (next to the pack manifest, where
``GraphicsPack`` looks for fonts) and a preview sheet ``reverse-engineering/amiga/extracted/ui/fonts/<id>.png``.
"""
from __future__ import annotations

import json
import struct
import sys
from pathlib import Path

from amiga_gfx_common import AMIGA, EXTRACTED, REPO, UNPACKED, ensure_dir

KICKSTART = AMIGA / "original-images" / "kickstart" / "kick34005.A500"
ROM_BASE = 0xFC0000
TOPAZ8_INIT = 0x910A  # the ROM's topaz 8 TextFont values (InitStruct data): name ptr, then tf_YSize … tf_CharLoc
FONT_DIR = REPO / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie1" / "graphics" / "fonts"
PREVIEW_DIR = EXTRACTED / "ui" / "fonts"


def hunk_code(data: bytes) -> bytes:
    """The single code/data hunk of a disk font file (HUNK_HEADER, one hunk, its RELOC32 ignored)."""
    if struct.unpack_from(">I", data, 0)[0] != 0x3F3:
        raise ValueError("not a hunk file")
    p = 4
    while struct.unpack_from(">I", data, p)[0]:
        p += 4
    p += 4
    _, first, last = struct.unpack_from(">III", data, p)
    p += 12 + 4 * (last - first + 1)
    kind, longs = struct.unpack_from(">II", data, p)
    if kind & 0x3FFFFFFF not in (0x3E9, 0x3EA):
        raise ValueError("first hunk is not code / data")
    return data[p + 8:p + 8 + longs * 4]


def decode(mem: bytes, ysize: int, xsize: int, baseline: int, lo: int, hi: int, chardata: int, modulo: int,
           charloc: int) -> dict:
    """Glyph bitmaps of a TextFont whose CharData / CharLoc are offsets into ``mem``."""
    def bits(offset: int, width: int) -> list[str]:
        rows = []
        for y in range(ysize):
            row = mem[chardata + y * modulo:chardata + (y + 1) * modulo]
            rows.append("".join("#" if row[(offset + x) >> 3] & (0x80 >> ((offset + x) & 7)) else "."
                                for x in range(width)))
        return rows

    glyphs = {}
    for i in range(hi - lo + 2):
        offset, width = struct.unpack_from(">HH", mem, charloc + 4 * i)
        if width == 0:
            continue
        glyphs[lo + i if i <= hi - lo else -1] = bits(offset, width)
    return dict(ysize=ysize, xsize=xsize, baseline=baseline, lo=lo, hi=hi, glyphs=glyphs)


def z6font() -> dict:
    code = hunk_code((UNPACKED / "fonts" / "z6font" / "6").read_bytes())
    # DiskFontHeader: moveq #0,d0 / rts, dfh_DF (Node 14), FileID 0x0f80, Revision, Segment, Name[32], then the TextFont.
    if struct.unpack_from(">H", code, 0x12)[0] != 0x0F80:
        raise ValueError("z6font: no DiskFontHeader")
    tf = 0x3A
    ysize, _style, _flags, xsize, baseline = struct.unpack_from(">HBBHH", code, tf + 20)
    lo, hi = code[tf + 32], code[tf + 33]
    chardata, modulo, charloc = struct.unpack_from(">IHI", code, tf + 34)
    return decode(code, ysize, xsize, baseline, lo, hi, chardata, modulo, charloc)


def topaz8() -> dict:
    rom = KICKSTART.read_bytes()
    p = TOPAZ8_INIT
    name = struct.unpack_from(">I", rom, p)[0] - ROM_BASE
    if rom[name:name + 11] != b"topaz.font\0":
        raise ValueError("Kickstart: topaz 8 not at the expected place (not KS 1.3 34.5?)")
    ysize, _style, _flags, xsize, baseline = struct.unpack_from(">HBBHH", rom, p + 6)
    lo, hi = rom[p + 18], rom[p + 19]
    chardata, modulo, charloc = struct.unpack_from(">IHI", rom, p + 20)
    return decode(rom, ysize, xsize, baseline, lo, hi, chardata - ROM_BASE, modulo, charloc - ROM_BASE)


def to_font_json(font_id: str, font: dict, source: str) -> dict:
    """``phantasie.font/v1``: the glyphs by character, the advance tf_XSize, the default glyph as the fallback."""
    glyphs = {}
    fallback_rows = font["glyphs"].get(-1)
    for code, rows in sorted(font["glyphs"].items()):
        if code < 0 or 0x7F <= code < 0xA0:  # the default glyph, DEL and the C1 range are not characters
            continue
        glyphs[chr(code)] = {"rows": rows, "advance": font["xsize"]}
    fallback = "?"
    if fallback_rows is not None and fallback_rows != glyphs.get("?", {}).get("rows"):
        # The default glyph as a private-use character, so a missing character draws what the Amiga draws.
        fallback = "U+E000"
        glyphs[fallback] = {"rows": fallback_rows, "advance": font["xsize"]}
    return {
        "_schema": "phantasie.font/v1",
        "_note": f"{source} tf_YSize {font['ysize']}, tf_XSize {font['xsize']}, tf_Baseline {font['baseline']}, "
                 f"characters {font['lo']:#04x}-{font['hi']:#04x}; extracted by tools/scripts/extract_amiga_fonts.py. "
                 "Fixed width: each glyph is its CharLoc bits, the pen moves tf_XSize. The fallback is the font's "
                 "default glyph (the entry after HiChar).",
        "id": font_id,
        "height": font["ysize"],
        "line_height": font["ysize"],
        "space_advance": font["xsize"],
        "fallback": fallback,
        "fold_to_upper": False,
        "glyphs": glyphs,
    }


def write_preview(font_id: str, font: dict) -> Path:
    from PIL import Image
    codes = sorted(c for c in font["glyphs"] if c >= 0)
    per_row = 32
    cw, ch = font["xsize"] + 2, font["ysize"] + 2
    img = Image.new("RGB", (per_row * cw, ((len(codes) + per_row - 1) // per_row) * ch), (0, 0, 0))
    for n, code in enumerate(codes):
        ox, oy = (n % per_row) * cw + 1, (n // per_row) * ch + 1
        for y, row in enumerate(font["glyphs"][code]):
            for x, c in enumerate(row):
                if c == "#":
                    img.putpixel((ox + x, oy + y), (238, 238, 238))
    out = ensure_dir(PREVIEW_DIR) / f"{font_id}.png"
    img.save(out)
    return out


def main() -> int:
    fonts = [
        ("amiga-z6font-6", z6font(), "Phantasie I Amiga disk, fonts/z6font/6 (TextAttr h78 +0x36: requesters, the line)."),
        ("amiga-topaz-8", topaz8(), "Kickstart 1.3 (34.5) ROM topaz 8, ROM 0x910a (TextAttr h78 +0x22: the screen "
                                    "font, menu bar and menu items)."),
    ]
    ensure_dir(FONT_DIR)
    for font_id, font, source in fonts:
        path = FONT_DIR / f"{font_id}.json"
        data = to_font_json(font_id, font, source)
        path.write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
        preview = write_preview(font_id, font)
        print(f"{font_id}: {len(data['glyphs'])} glyphs -> {path.relative_to(REPO)}, {preview.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
