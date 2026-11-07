#!/usr/bin/env python3
"""Shared helpers for Phantasie III (Amiga v1.0, SSI 1987) static RE.

`PhantasieIII` is an AmigaDOS hunk executable (not packed). `hunks()` returns each loadable
hunk with its data and HUNK_RELOC32 entries; `load_flat()` lays the hunks end to end (each at
its own base) the way Ghidra / dis68k see them when given one flat image.

Run with `python3 -I`. Notes: `reverse-engineering/amiga/phantasie3/FORMAT.md`.
"""
from __future__ import annotations

import struct
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
P3A = REPO / "reverse-engineering" / "amiga" / "phantasie3"
UNPACKED = P3A / "unpacked" / "PhantasieIII"
PROGRAM = UNPACKED / "PhantasieIII"

HUNK_NAMES = {0x3E7: "UNIT", 0x3E8: "NAME", 0x3E9: "CODE", 0x3EA: "DATA", 0x3EB: "BSS",
              0x3EC: "RELOC32", 0x3ED: "RELOC16", 0x3EE: "RELOC8", 0x3EF: "EXT",
              0x3F0: "SYMBOL", 0x3F1: "DEBUG", 0x3F2: "END", 0x3F3: "HEADER", 0x3F5: "OVERLAY",
              0x3F6: "BREAK"}


def hunks(data: bytes | None = None) -> list[dict]:
    data = data if data is not None else PROGRAM.read_bytes()
    p = 0

    def u32():
        nonlocal p
        v = struct.unpack_from(">I", data, p)[0]
        p += 4
        return v

    assert u32() == 0x3F3, "not a hunk executable"
    while u32():  # resident library names (none expected)
        pass
    table_size, first, last = u32(), u32(), u32()
    sizes = [u32() & 0x3FFFFFFF for _ in range(last - first + 1)]
    out, cur = [], None
    while p < len(data):
        t = u32() & 0x3FFFFFFF
        name = HUNK_NAMES.get(t, hex(t))
        if t in (0x3E9, 0x3EA):
            n = u32() * 4
            cur = dict(index=len(out), type=name, alloc=sizes[len(out)] * 4, data=data[p:p + n],
                       file_off=p, relocs={})
            p += n
        elif t == 0x3EB:
            u32()
            cur = dict(index=len(out), type="BSS", alloc=sizes[len(out)] * 4, data=b"",
                       file_off=p, relocs={})
        elif t == 0x3EC:
            while True:
                n = u32()
                if not n:
                    break
                target = u32()
                cur["relocs"].setdefault(target, []).extend(u32() for _ in range(n))
        elif t == 0x3F0:
            while True:
                n = u32()
                if not n:
                    break
                p += n * 4 + 4
        elif t == 0x3F1:
            p += u32() * 4
        elif t == 0x3F2:
            out.append(cur)
            cur = None
        else:
            raise ValueError(f"unhandled hunk {t:#x} at {p - 4:#x}")
    return out


def load_flat(data: bytes | None = None, align: int = 0x10) -> tuple[bytes, list[int]]:
    """All hunks in one image, relocations applied. Returns (image, hunk base addresses)."""
    hs = hunks(data)
    bases, at = [], 0
    for h in hs:
        bases.append(at)
        at += (h["alloc"] + align - 1) & ~(align - 1)
    img = bytearray(at)
    for h, b in zip(hs, bases):
        img[b:b + len(h["data"])] = h["data"]
    for h, b in zip(hs, bases):
        for target, offs in h["relocs"].items():
            for o in offs:
                v = struct.unpack_from(">I", img, b + o)[0]
                struct.pack_into(">I", img, b + o, (v + bases[target]) & 0xFFFFFFFF)
    return bytes(img), bases


if __name__ == "__main__":
    import hashlib
    import sys

    hs = hunks()
    for h in hs:
        nrel = sum(len(v) for v in h["relocs"].values())
        print(f"hunk {h['index']:2d} {h['type']:4s} alloc {h['alloc']:#07x} data {len(h['data']):#07x} "
              f"file {h['file_off']:#07x} relocs {nrel} -> {sorted(h['relocs'])}")
    img, bases = load_flat()
    print("flat image", hex(len(img)), "bases", [hex(b) for b in bases],
          "sha1", hashlib.sha1(img).hexdigest())
    if len(sys.argv) > 1:
        Path(sys.argv[1]).write_bytes(img)
