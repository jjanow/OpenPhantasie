#!/usr/bin/env python3
"""Phantasie III (Apple II, SSI 1987 [cr]): list the Applesoft BASIC programs on a disk side.

The disks use SSI's own DOS (no DOS 3.3 catalog). Files are written with sectors in descending order
within a track, so the image is first re-laid per track as sectors 15..0; the Applesoft lines then
mostly chain (a line that crosses an out-of-order sector shows as garbage). Offsets printed are in
that re-laid image: track = off // 4096, physical sector = 15 - (off % 4096) // 256.

Usage: python3 -I detok_apple2_p3.py <side.dsk> [start_hex end_hex] > listing.txt
Notes: reverse-engineering/apple2/phantasie3/README.md
"""
import struct
import sys

TOKENS = ("END FOR NEXT DATA INPUT DEL DIM READ GR TEXT PR# IN# CALL PLOT HLIN VLIN HGR2 HGR HCOLOR= HPLOT "
          "DRAW XDRAW HTAB HOME ROT= SCALE= SHLOAD TRACE NOTRACE NORMAL INVERSE FLASH COLOR= POP VTAB HIMEM: "
          "LOMEM: ONERR RESUME RECALL STORE SPEED= LET GOTO RUN IF RESTORE & GOSUB RETURN REM STOP ON WAIT LOAD "
          "SAVE DEF POKE PRINT CONT LIST CLEAR GET NEW TAB( TO FN SPC( THEN AT NOT STEP + - * / ^ AND OR > = < "
          "SGN INT ABS USR FRE SCRN( PDL POS SQR RND LOG EXP COS SIN TAN ATN PEEK LEN STR$ VAL ASC CHR$ LEFT$ "
          "RIGHT$ MID$").split()


def relay(dsk: bytes) -> bytes:
    out = bytearray()
    for t in range(len(dsk) // 4096):
        for s in range(15, -1, -1):
            out += dsk[t * 4096 + s * 256:t * 4096 + s * 256 + 256]
    return bytes(out)


def listing(d: bytes, p: int, end: int):
    while p + 4 <= min(end, len(d)):
        nxt, ln = struct.unpack_from("<HH", d, p)
        q, s = p + 4, ""
        while q < len(d) and d[q]:
            b = d[q]
            i = b - 0x80
            s += (f" {TOKENS[i]} " if 0 <= i < len(TOKENS) else f"{{{b:02x}}}") if b >= 0x80 else chr(b)
            q += 1
        yield p, ln, nxt, s
        p = q + 1


if __name__ == "__main__":
    img = relay(open(sys.argv[1], "rb").read())
    lo = int(sys.argv[2], 16) if len(sys.argv) > 2 else 0
    hi = int(sys.argv[3], 16) if len(sys.argv) > 3 else len(img)
    for off, ln, nxt, text in listing(img, lo, hi):
        print(f"{off:06x} {ln:5d} {nxt:04x}: {text}")
