#!/usr/bin/env python3
"""Extract the DOS Phantasie I town picture to PNG (P2 town menu).

Source: ``PELNOR.IBM`` (16384 B) — a raw CGA 320x200 2 bpp screen (even rows at 0x0000, odd rows at 0x2000,
80 bytes/row). OV1 names only this one file (``pelnor.ibm``), so every town shows the same picture; the town menu
bar (rows 0..8) and the town name (rows 191..199) are blank in the file and drawn by the game.

Palette: CGA palette 1 high intensity, the same as the map tiles (extract_dos_map_tiles.py).

Writes reverse-engineering/dos/extracted/ui/town.png.
"""
from __future__ import annotations

from extract_dos_map_tiles import cga_rows
from re_common import OUT, read

WIDTH, HEIGHT, ROW_BYTES = 320, 200, 80


def main() -> None:
    data = read("PELNOR.IBM")
    assert len(data) == 16384, len(data)
    rows = [data[(y & 1) * 0x2000 + (y >> 1) * ROW_BYTES:][:ROW_BYTES] for y in range(HEIGHT)]
    root = OUT / "ui"
    root.mkdir(parents=True, exist_ok=True)
    cga_rows(rows, WIDTH).save(root / "town.png")
    print(f"wrote {root / 'town.png'}")


if __name__ == "__main__":
    main()
