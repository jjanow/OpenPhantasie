#!/usr/bin/env python3
"""Read SSI's RDOS disks (Apple II) as used by Phantasie II (and I): catalog + file contents.

Format (decoded s158 from the 4am crack of Phantasie II, `reverse-engineering/apple2/phantasie2/FORMAT.md`):

* RDOS 2.1 is a 13-sector DOS. The 4am crack (Passport) keeps the 13-sector layout inside a 16-sector `.dsk`:
  logical RDOS sector n lives at `.dsk` track n // 13, sector n % 13 (DOS-order image offset
  `(n // 13) * 4096 + (n % 13) * 256`); `.dsk` sectors 13..15 of every track are unused (zero).
* The catalog is track 1, sectors 0..10: 8 entries of 32 bytes per sector, ended by an entry whose first byte is 0.
  Entry: bytes 0..23 name (high-bit ASCII, space padded), 24 type (`A` Applesoft, `B` binary, `T` text/data),
  25 sector count, 26..27 load address, 28..29 length in bytes, 30..31 first logical sector. Files are contiguous.
  A deleted entry has its first name byte replaced by 0x80 (TWN210's stale twin on side A).
* Sectors 11..12 of track 1 hold RWTS code, not catalog entries.
* The Memorial Set (GOG / Steam) ships the same disks in DOS 3.3 sector order: its sector s is the 4am image's
  sector `DOS33_TO_IMAGE[s]` on every track (s166). Both orders share catalog sector 0, so `RdosDisk` reads the image
  both ways and keeps the catalog with more entries (as the importer's `RdosVolume.TryOpen`).

Usage (CLI): python3 -I a2_rdos.py <side.dsk> [--extract OUTDIR]
Library: `RdosDisk(path).files` -> list of `RdosFile`; `disk.read(name)` -> bytes (exactly `length` bytes).

Port notes: pure byte arithmetic; no Apple II emulation needed. `.woz` originals are a 13-sector 5-and-3 GCR flux
image and are not read here (see FORMAT.md "Containers").
"""
from __future__ import annotations

import struct
import sys
from dataclasses import dataclass
from pathlib import Path

SECTORS_PER_TRACK = 13          # RDOS 13-sector layout
DSK_TRACK_BYTES = 16 * 256      # .dsk track stride (16 sectors)
CATALOG_TRACK = 1
CATALOG_SECTORS = 11            # track 1 sectors 0..10
ENTRY_SIZE = 32
DOS33_TO_IMAGE = (0, 13, 11, 9, 7, 5, 3, 1, 14, 12, 10, 8, 6, 4, 2, 15)


def from_dos33_order(image: bytes) -> bytes:
    out = bytearray(len(image))
    for t in range(len(image) // DSK_TRACK_BYTES):
        for s in range(16):
            src = t * DSK_TRACK_BYTES + s * 256
            dst = t * DSK_TRACK_BYTES + DOS33_TO_IMAGE[s] * 256
            out[dst:dst + 256] = image[src:src + 256]
    return bytes(out)


@dataclass(frozen=True)
class RdosFile:
    name: str
    type: str          # 'A' Applesoft, 'B' binary, 'T' text/data
    sectors: int
    load: int
    length: int
    start: int         # first logical sector
    deleted: bool


class RdosDisk:
    def __init__(self, path_or_bytes):
        image = path_or_bytes if isinstance(path_or_bytes, (bytes, bytearray)) else Path(path_or_bytes).read_bytes()
        if len(image) != 143360:
            raise ValueError(f"not a 140K .dsk image ({len(image)} bytes)")
        self.image = from_dos33_order(image)
        remapped = self._catalog()
        self.image = image
        self.files = self._catalog()
        if len(remapped) > len(self.files):
            self.image, self.files = from_dos33_order(image), remapped

    def sector(self, n: int) -> bytes:
        off = (n // SECTORS_PER_TRACK) * DSK_TRACK_BYTES + (n % SECTORS_PER_TRACK) * 256
        return self.image[off:off + 256]

    def _catalog(self) -> list[RdosFile]:
        out: list[RdosFile] = []
        for s in range(CATALOG_SECTORS):
            sec = self.sector(CATALOG_TRACK * SECTORS_PER_TRACK + s)
            for e in range(256 // ENTRY_SIZE):
                b = sec[e * ENTRY_SIZE:(e + 1) * ENTRY_SIZE]
                if b[0] == 0:
                    return out
                deleted = b[0] == 0x80
                name = "".join(chr(c & 0x7F) for c in b[:24]).rstrip()
                load, length, start = struct.unpack_from("<HHH", b, 26)
                out.append(RdosFile(name, chr(b[24] & 0x7F), b[25], load, length, start, deleted))
        return out

    def entry(self, name: str) -> RdosFile:
        for f in self.files:
            if f.name == name and not f.deleted:
                return f
        raise KeyError(name)

    def read(self, name_or_entry) -> bytes:
        f = name_or_entry if isinstance(name_or_entry, RdosFile) else self.entry(name_or_entry)
        data = b"".join(self.sector(f.start + i) for i in range(f.sectors))
        # Applesoft files: length counts the program bytes only (sector count covers them).
        return data[:f.length] if f.length and f.length <= len(data) else data


def main(argv: list[str]) -> None:
    disk = RdosDisk(argv[1])
    out = Path(argv[argv.index("--extract") + 1]) if "--extract" in argv else None
    for f in disk.files:
        flag = " (deleted)" if f.deleted else ""
        print(f"{f.name:24s} {f.type} sectors={f.sectors:3d} load=${f.load:04X} len={f.length:5d} start={f.start}{flag}")
        if out and not f.deleted:
            out.mkdir(parents=True, exist_ok=True)
            (out / f.name.replace(" ", "_")).write_bytes(disk.read(f))


if __name__ == "__main__":
    main(sys.argv)
