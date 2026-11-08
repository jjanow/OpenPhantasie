#!/usr/bin/env python3
"""Extract the DOS Phantasie I party-defeat pictures to PNG (endgame.md section 4, OV1 ``0x2af`` / ``0x5fe`` / ``0x787``).

Sources (s157, static):
- ``CEMEN.IBM`` (10030 B): the temple with the Great Demon's face in its doorway. COM ``0x1b9e`` reads the file into
  buffer A and COM ``0x2398`` unpacks it (COM ``0x544``): a byte is copied; a 0 byte is followed by a count n and stands
  for n + 1 zeros. The result is a 16384-B CGA screen (even rows at 0x0000, odd rows at 0x2000, 80 bytes/row), as
  ``PELNOR.IBM``.
- OV1 ``0x2f4..0x378`` blanks the doorway in buffer A only (x 128..199, bytes 32..49 of a row): ``0x302`` zeroes them
  in even-bank row 8 (screen row 16) with COM ``0x4dc5`` (poke); ``0x31f`` copies 18 bytes even -> odd bank at the
  loop's end offset ``0x2b2`` (bytes 50..67 of row 8: screen row 17 takes row 16's x 200..271, its doorway stays);
  ``0x336..0x378`` copies the blank strip into bank rows 9..53 of both banks (offsets ``0x2f0`` while < ``0x1100``):
  screen rows 18..107. Rows 108..109 keep the picture. Buffer A is
  then the temple with an empty doorway (shown with ``core.defeat.arrived_at_astral_plane``, COM ``0xcf0``) and
  buffer B, copied before the blanking (COM ``0xd50``), the temple with the face (shown with
  ``core.defeat.great_demon_speaks``, COM ``0xd10``). There is no wipe on screen: both are whole-screen copies.
- The ghost (OV1 ``0x6a8``: DS ``0xba1a`` = OV1 file ``0x5a23``, 256 B): 32 x 32, 8 bytes a row, rows in order.
- The figure a revived member turns back into (OV1 ``0x787`` -> COM ``0x30ac``): DS ``0x3b96 + race * 256``, where COM
  ``0x19e3`` loads ``PRT1IBM.PAT`` (16 x 256 B, races 0..15, 15 = UNDEAD); 32 x 32 like the ghost.

Palette: OV1 ``0x2c7`` calls COM ``0x3abd(0)`` (INT 10h AH 0Bh BH 1: CGA palette 0) before the first line, so these
screens show black / light green / light red / yellow (the s63 DOSBox shots: yellow ghosts and text, red face).

Writes reverse-engineering/dos/extracted/ui/defeat-temple.png, defeat-demon.png, defeat-ghost.png and
defeat-figures/NN.png (NN = race 00..15).
"""
from __future__ import annotations

from PIL import Image

from re_common import DOS, OUT, read

WIDTH, HEIGHT, ROW_BYTES = 320, 200, 80
PALETTE0_HIGH = [(0, 0, 0), (0x55, 0xFF, 0x55), (0xFF, 0x55, 0x55), (0xFF, 0xFF, 0x55)]
DOORWAY = 0x2A0  # OV1 0x2fd: bank offset of the blank strip (bank row 8, byte 32), 0x12 bytes
STRIP = 0x12
ODD = 0x2000
GHOST_OFFSET = 0x5A23  # OV1 file offset of DS 0xba1a (OV1 string immediates = file + 0x5ff7)


def unrle(src: bytes) -> bytes:
    """COM 0x544: bytes copied; 0, n = n + 1 zeros."""
    out = bytearray()
    i = 0
    while i < len(src):
        b = src[i]
        i += 1
        out.append(b)
        if b == 0 and i < len(src):
            out += bytes(src[i])
            i += 1
    return bytes(out)


def picture(rows: list[bytes], width: int) -> Image.Image:
    im = Image.new("RGBA", (width, len(rows)))
    for y, row in enumerate(rows):
        for x in range(width):
            im.putpixel((x, y), PALETTE0_HIGH[(row[x // 4] >> (6 - 2 * (x % 4))) & 3] + (255,))
    return im


def screen_rows(screen: bytes) -> list[bytes]:
    return [screen[(y & 1) * 0x2000 + (y >> 1) * ROW_BYTES:][:ROW_BYTES] for y in range(HEIGHT)]


def sprite(data: bytes) -> Image.Image:
    assert len(data) == 256, len(data)
    return picture([data[r * 8:(r + 1) * 8] for r in range(32)], 32)


def main() -> None:
    demon = unrle(read("CEMEN.IBM"))
    assert len(demon) == 16384, len(demon)
    temple = bytearray(demon)  # mirrors OV1 0x2fd..0x378 on buffer A, the 0x31f quirk included
    off = DOORWAY
    while off < DOORWAY + STRIP:  # 0x302: poke 0 into even-bank bytes 0x2a0..0x2b1
        temple[off] = 0
        off += 1
    temple[ODD + off:ODD + off + STRIP] = temple[off:off + STRIP]  # 0x31f: even 0x2b2.. -> odd 0x2b2..
    off = 0x2F0
    while off < 0x1100:  # 0x336: the strip into both banks
        temple[off:off + STRIP] = temple[DOORWAY:DOORWAY + STRIP]
        temple[ODD + off:ODD + off + STRIP] = temple[DOORWAY:DOORWAY + STRIP]
        off += ROW_BYTES
    root = OUT / "ui"
    figures = root / "defeat-figures"
    figures.mkdir(parents=True, exist_ok=True)
    picture(screen_rows(demon), WIDTH).save(root / "defeat-demon.png")
    picture(screen_rows(bytes(temple)), WIDTH).save(root / "defeat-temple.png")
    ov1 = (DOS / "original-files" / "OV1.OVR").read_bytes()
    sprite(ov1[GHOST_OFFSET:GHOST_OFFSET + 256]).save(root / "defeat-ghost.png")
    prt = read("PRT1IBM.PAT")
    assert len(prt) == 4096, len(prt)
    for race in range(16):
        sprite(prt[race * 256:(race + 1) * 256]).save(figures / f"{race:02d}.png")
    print(f"wrote {root}/defeat-temple.png, defeat-demon.png, defeat-ghost.png, defeat-figures/00..15.png")


if __name__ == "__main__":
    main()
