"""Shared Amiga planar-graphics helpers for Phantasie I extraction.

Graphics are **not** IFF ILBM — they are raw bitplane blobs (Kroah / Sprite
Viewer layout). Palette words are Amiga OCS 12-bit ``0x0RGB``. Every I screen sets its table with LoadRGB4 (4-bit
guns), so what the Amiga shows is ``c * 17`` per nibble (``load_palette(hw4=True)``): the sprites (combat table), the
covers, town, defeat and ending pictures use it. The Amiga→ST nibble LUT (Kroah's published reference PNGs) is still
the default for the map art (``extract_amiga_map_graphics.py``) and the palette swatches.
"""
from __future__ import annotations

import struct
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parents[2]
AMIGA = REPO / "reverse-engineering" / "amiga"
UNPACKED = AMIGA / "unpacked" / "PHANT"
EXTRACTED = AMIGA / "extracted"
DOS_EXTRACTED = REPO / "reverse-engineering" / "dos" / "extracted"

# Amiga nibble -> 8 bits on the house 3-bit ramp: even n -> round(n / 2 * 255 / 7) (the ST-compatible values the art
# mostly uses). Odd nibbles are real Amiga colours (I's combat palette has 0xEBB, a pink) and take the same ramp,
# round(n * 255 / 14); zeroing them turned that pink red (s102).
AMIGA_TO_ST = [min(255, round(n * 255 / 14)) for n in range(16)]

# Combat / sprite palette inside ``game`` (Kroah: MonstersManagerP1).
PALETTE_COMBAT_OFF = 0x194BC
# Alternate 16-color table immediately after (title / town screens often share).
PALETTE_ALT_OFF = 0x194DC

MONSTER_COUNT = 80
MONSTER_ADDR_OFF = 0x16842
MONSTER_DIM_OFF = 0x1EA8
MONSTER_NAME_OFF = 0xC88
MONSTER_NAME_STRIDE = 58

RACE_COUNT = 16
RACE_NAME_OFF = 0x69BE
RACE_BYTES = 32 * 32 * 4 // 8  # 512


def read_word(data: bytes, off: int) -> int:
    return struct.unpack_from(">H", data, off)[0]


def read_long(data: bytes, off: int) -> int:
    return struct.unpack_from(">I", data, off)[0]


def read_cstring(data: bytes, off: int) -> str:
    end = data.index(0, off)
    return data[off:end].decode("ascii", errors="replace")


def load_palette(game: bytes, address: int = PALETTE_COMBAT_OFF, n: int = 16, hw4: bool = False) -> list[tuple]:
    """The 16 colours at ``address``. Default: the Amiga→ST nibble LUT (the house expansion of the sprite art).
    ``hw4``: what LoadRGB4 puts on the screen, every 4-bit nibble ``c`` → ``c * 17`` (the title covers, whose
    palette uses odd nibbles such as ``0xEB0`` that the LUT would zero; FORMAT.md "Title")."""
    colors = []
    for i in range(n):
        rgb = read_word(game, address + 2 * i)
        if hw4:
            r, g, b = (((rgb >> s) & 0xF) * 17 for s in (8, 4, 0))
        else:
            r = AMIGA_TO_ST[(rgb >> 8) & 0xF]
            g = AMIGA_TO_ST[(rgb >> 4) & 0xF]
            b = AMIGA_TO_ST[rgb & 0xF]
        colors.append((r, g, b, 255))
    return colors


def palette_raw_words(game: bytes, address: int = PALETTE_COMBAT_OFF, n: int = 16) -> list[str]:
    return [f"{read_word(game, address + 2 * i):03x}" for i in range(n)]


def decode_planar(
    data: bytes,
    address: int,
    width: int,
    height: int,
    palette: list[tuple],
    bp: int = 4,
) -> Image.Image:
    """Sequential (non-interleaved) bitplanes — Amiga PAT / .pic layout."""
    if width % 8 != 0:
        raise ValueError(f"width must be multiple of 8, got {width}")
    interleave_size = width // 8
    scanline_stride = width // 8
    bitplane_stride = width * height // 8
    needed = bitplane_stride * bp
    if address + needed > len(data):
        raise ValueError(
            f"planar read past EOF: addr={address} need={needed} len={len(data)}"
        )

    img = Image.new("RGBA", (width, height))
    px = img.load()
    for y in range(height):
        for x in range(width):
            color = 0
            byte_x = x >> 3
            for bitplane in range(bp):
                offset = byte_x % interleave_size
                offset += (byte_x // interleave_size) * (bp * interleave_size)
                offset += bitplane * bitplane_stride
                offset += y * scanline_stride
                if data[address + offset] & (1 << (7 - (x % 8))):
                    color |= 1 << bitplane
            px[x, y] = palette[color]
    return img


def slug(name: str) -> str:
    s = name.strip().lower().replace(" ", "-").replace(".", "")
    return "".join(c if c.isalnum() or c == "-" else "-" for c in s)


def ensure_dir(path: Path) -> Path:
    path.mkdir(parents=True, exist_ok=True)
    return path
