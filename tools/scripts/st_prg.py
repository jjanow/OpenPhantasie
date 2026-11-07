"""Atari ST GEMDOS executable (.PRG) reader: header, segments and the relocation (fixup) table.

Phase 09 T3 (Phantasie II, Atari ST). Addresses used in the II notes are **text-relative**
("image" addresses: text starts at 0, data at text_size, bss after data), which is what the
program's relocated pointers hold before TOS adds the load base. File offset = image + 0x1c.
"""
from __future__ import annotations

import struct
from dataclasses import dataclass

HEADER = 0x1C


@dataclass
class Prg:
    raw: bytes
    text_size: int
    data_size: int
    bss_size: int
    sym_size: int
    flags: int
    abs_flag: int
    relocs: list[int]  # image offsets of longwords that get the load base added

    @property
    def image(self) -> bytes:
        return self.raw[HEADER : HEADER + self.text_size + self.data_size]

    @property
    def data_start(self) -> int:
        return self.text_size

    def u8(self, a: int) -> int:
        return self.raw[HEADER + a]

    def u16(self, a: int) -> int:
        return struct.unpack_from(">H", self.raw, HEADER + a)[0]

    def i16(self, a: int) -> int:
        return struct.unpack_from(">h", self.raw, HEADER + a)[0]

    def u32(self, a: int) -> int:
        return struct.unpack_from(">I", self.raw, HEADER + a)[0]

    def cstr(self, a: int, limit: int = 256) -> str:
        img = self.raw[HEADER + a : HEADER + a + limit]
        return img.split(b"\0", 1)[0].decode("latin-1")


def load_prg(path: str) -> Prg:
    raw = open(path, "rb").read()
    magic, tsize, dsize, bsize, ssize, _res, flags, absflag = struct.unpack_from(">HIIIIIIH", raw, 0)
    if magic != 0x601A:
        raise ValueError(f"{path}: not a GEMDOS PRG (magic {magic:#x})")
    relocs: list[int] = []
    if absflag == 0:
        p = HEADER + tsize + dsize + ssize
        first = struct.unpack_from(">I", raw, p)[0]
        p += 4
        if first:
            cur = first
            relocs.append(cur)
            while True:
                b = raw[p]
                p += 1
                if b == 0:
                    break
                if b == 1:
                    cur += 254
                    continue
                cur += b
                relocs.append(cur)
    return Prg(raw, tsize, dsize, bsize, ssize, flags, absflag, relocs)
