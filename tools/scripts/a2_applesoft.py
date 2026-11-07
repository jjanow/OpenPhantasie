#!/usr/bin/env python3
"""Applesoft BASIC detokeniser for Phantasie II (Apple II) programs read with `a2_rdos.py`.

An RDOS `A` file is a plain Applesoft program image: lines of
`<next-ptr u16 LE> <line-no u16 LE> <tokens...> 00`, ended by a zero next-pointer. `load` is the address the
first byte sits at (0x0801 for the main programs, 0x5100 for the overlays OUTDOORS2 / DUNGON2 / DEMO / ...).

Usage: python3 -I a2_applesoft.py <side.dsk> <PROGRAM NAME> [--offsets]
Library: `lines(data)` -> iterator of (offset, line_no, text); `program(disk, name)` -> {line_no: text}.
"""
from __future__ import annotations

import struct
import sys

TOKENS = ("END FOR NEXT DATA INPUT DEL DIM READ GR TEXT PR# IN# CALL PLOT HLIN VLIN HGR2 HGR HCOLOR= HPLOT "
          "DRAW XDRAW HTAB HOME ROT= SCALE= SHLOAD TRACE NOTRACE NORMAL INVERSE FLASH COLOR= POP VTAB HIMEM: "
          "LOMEM: ONERR RESUME RECALL STORE SPEED= LET GOTO RUN IF RESTORE & GOSUB RETURN REM STOP ON WAIT LOAD "
          "SAVE DEF POKE PRINT CONT LIST CLEAR GET NEW TAB( TO FN SPC( THEN AT NOT STEP + - * / ^ AND OR > = < "
          "SGN INT ABS USR FRE SCRN( PDL POS SQR RND LOG EXP COS SIN TAN ATN PEEK LEN STR$ VAL ASC CHR$ LEFT$ "
          "RIGHT$ MID$").split()


def detok(body: bytes) -> str:
    out, quoted = [], False
    for b in body:
        if b == 0x22:
            quoted = not quoted
            out.append('"')
        elif b >= 0x80 and not quoted and b - 0x80 < len(TOKENS):
            out.append(f" {TOKENS[b - 0x80]} ")
        elif 32 <= b < 127:
            out.append(chr(b))
        else:
            out.append(f"{{{b:02x}}}")
    return "".join(out)


def lines(data: bytes):
    p = 0
    while p + 4 <= len(data):
        nxt, ln = struct.unpack_from("<HH", data, p)
        if nxt == 0:
            return
        q = data.index(0, p + 4)
        yield p, ln, detok(data[p + 4:q])
        p = q + 1


def program(disk, name: str) -> dict[int, str]:
    return {ln: text for _, ln, text in lines(disk.read(name))}


if __name__ == "__main__":
    import os
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # `python3 -I` drops the script dir
    from a2_rdos import RdosDisk
    disk = RdosDisk(sys.argv[1])
    show = "--offsets" in sys.argv
    for off, ln, text in lines(disk.read(sys.argv[2])):
        print((f"{off:05x} " if show else "") + f"{ln} {text}")
