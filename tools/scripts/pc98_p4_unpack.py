#!/usr/bin/env python3
"""Unpack the PC-98 Phantasie IV program (P4.EXE, Microsoft EXEPACK) to its load image (s201, combat.md section 15).

    pc98_p4_unpack.py P4.EXE OUT.img

P4.EXE comes off `reverse-engineering/pc98/original-images/phantasie4/Phantasie IV (Disk 1).hdm` with
`mcopy -i "<disk>" ::P4.EXE <dir>/` (mtools; the image is plain MS-DOS FAT12). The output is the unpacked load image
(relocations not applied): linear offsets in it are the addresses used in combat.md section 15; DGROUP is paragraph
0x6B97 (`mov di, 0x6b97` in the start-up code at 2214:0016), so a DS offset d sits at image offset 0x6B970 + d.
"""
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from re_common import _exepack  # noqa: E402

DGROUP = 0x6B97


def unpack(exe: bytes) -> bytes:
    if exe[:2] != b"MZ":
        raise ValueError("not an MZ file")
    hdr = struct.unpack_from("<H", exe, 8)[0] * 16
    cs = struct.unpack_from("<H", exe, 0x16)[0]
    return _exepack(exe[hdr:], cs * 16)


def main() -> None:
    src, out = sys.argv[1:3]
    image = unpack(Path(src).read_bytes())
    Path(out).write_bytes(image)
    print(f"{out}: {len(image):#x} bytes; DGROUP at {DGROUP * 16:#x}")


if __name__ == "__main__":
    main()
