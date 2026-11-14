#!/usr/bin/env python3
"""Phantasie III (Apple II, SSI 1987 [cr]): list the Applesoft BASIC programs on a disk side.

The disks use SSI's RDOS 3.3; files are read by name through `a2_rdos.RdosDisk` (catalog, sector order).
A program is stored as linked for its own address (`$0801`, `$4A6A`, `$4970`), which need not match the catalog
load address, so the lines are walked in sequence (4-byte header: link, line number; text up to a 0 byte), never by
absolute link. The program ends at a link whose high byte is 0 (as Applesoft's LIST); the catalog length can stop a
few bytes short of that, the sector count covers it. Offsets printed (`--offsets`) are file-relative.

Usage: python3 -I detok_apple2_p3.py <side.dsk>                          catalog's Applesoft programs
       python3 -I detok_apple2_p3.py <side.dsk> <PROGRAM> [--offsets] [first last]
       python3 -I detok_apple2_p3.py <side.dsk> --all [--offsets]        every program, headed by name
Notes: reverse-engineering/apple2/phantasie3/README.md
"""
from __future__ import annotations

import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # `python3 -I` drops the script dir
from a2_applesoft import detok  # noqa: E402
from a2_rdos import RdosDisk  # noqa: E402


def program_bytes(disk: RdosDisk, name: str) -> bytes:
    f = disk.entry(name)
    return b"".join(disk.sector(f.start + i) for i in range(f.sectors))


def lines(data: bytes):
    """Yield (offset, line_no, text) up to the end link (high byte 0)."""
    p = 0
    while p + 4 <= len(data):
        nxt, ln = struct.unpack_from("<HH", data, p)
        if nxt < 0x100:
            return
        q = data.find(0, p + 4)
        if q < 0:
            return
        yield p, ln, detok(data[p + 4:q])
        p = q + 1


def show(disk: RdosDisk, name: str, offsets: bool, first: int = 0, last: int = 65535) -> None:
    for off, ln, text in lines(program_bytes(disk, name)):
        if first <= ln <= last:
            print((f"{off:05x} " if offsets else "") + f"{ln} {text}")


def main(argv: list[str]) -> None:
    offsets = "--offsets" in argv
    args = [a for a in argv[1:] if a != "--offsets"]
    disk = RdosDisk(args[0])
    programs = [f.name for f in disk.files if f.type == "A" and not f.deleted]
    if len(args) == 1:
        print("\n".join(programs))
    elif args[1] == "--all":
        for name in programs:
            print(f"==== {name}")
            show(disk, name, offsets)
    else:
        rng = [int(a) for a in args[2:4]]
        show(disk, args[1], offsets, *rng)


if __name__ == "__main__":
    main(sys.argv)
