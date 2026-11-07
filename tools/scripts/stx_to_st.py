#!/usr/bin/env python3
"""Convert an Atari ST Pasti (.stx) image to a plain sector image (.st).

Phase 09 T3 (Phantasie II, Atari ST). Reads the Pasti container (format as implemented by
Hatari's stx.c), pulls the 512-byte sectors out of every track and writes them in the usual
.st order (track 0 side 0, track 0 side 1, track 1 side 0, ...). The protection-only parts
(fuzzy masks, timing, raw track images, extra/odd sectors) are reported but not kept.

Usage:
    python3 -I tools/scripts/stx_to_st.py IN.stx OUT.st [--report]

The geometry (sectors per track, sides, tracks) is taken from the FAT boot sector in track 0
and cross-checked against what the tracks contain. Sectors the image lacks are written as
zero-filled and listed in the report.
"""
from __future__ import annotations

import argparse
import struct
import sys
from dataclasses import dataclass, field

FLAG_SECTOR_BLOCK = 0x01
FLAG_TRACK_IMAGE = 0x40
FLAG_TRACK_IMAGE_SYNC = 0x80
FDC_RNF = 0x10  # record not found
FDC_CRC = 0x08  # CRC error


@dataclass
class Sector:
    track: int
    head: int
    number: int
    size_code: int
    fdc_flags: int
    data: bytes | None
    fuzzy: bool = False


@dataclass
class Track:
    cyl: int
    side: int
    flags: int
    sectors: list[Sector] = field(default_factory=list)


def parse_stx(blob: bytes) -> tuple[dict, list[Track]]:
    if blob[:4] != b"RSY\0":
        raise ValueError("not a Pasti STX file (missing RSY magic)")
    version, tool, _res, track_count, revision = struct.unpack_from("<HHHBB", blob, 4)
    header = {"version": version, "tool": tool, "tracks": track_count, "revision": revision}
    pos = 16
    tracks: list[Track] = []
    for _ in range(track_count):
        rec_size, fuzzy_count, sect_count, flags, _trk_len, trk_num, _trk_type = struct.unpack_from(
            "<IIHHHBB", blob, pos
        )
        t = Track(cyl=trk_num & 0x7F, side=(trk_num >> 7) & 1, flags=flags)
        body = pos + 16
        if not flags & FLAG_SECTOR_BLOCK:
            # Standard track: sect_count plain 512-byte sectors numbered 1..n.
            for i in range(sect_count):
                d = blob[body + i * 512 : body + (i + 1) * 512]
                t.sectors.append(Sector(t.cyl, t.side, i + 1, 2, 0, d))
        else:
            descs = []
            p = body
            for _ in range(sect_count):
                off, _bitpos, _rtime, id_trk, id_head, id_num, id_size, _crc, fdc, _r = struct.unpack_from(
                    "<IHHBBBBHBB", blob, p
                )
                descs.append((off, id_trk, id_head, id_num, id_size, fdc))
                p += 16
            fuzzy = blob[p : p + fuzzy_count]
            p += fuzzy_count
            track_data = p  # sector data offsets are relative to here
            fuzzy_pos = 0
            for off, id_trk, id_head, id_num, id_size, fdc in descs:
                size = 128 << (id_size & 3)
                is_fuzzy = bool(fdc & 0x80)
                if fdc & FDC_RNF:
                    data = None
                else:
                    data = blob[track_data + off : track_data + off + size]
                if is_fuzzy:
                    fuzzy_pos += size
                t.sectors.append(Sector(id_trk, id_head, id_num, id_size, fdc, data, is_fuzzy))
        tracks.append(t)
        pos += rec_size
    return header, tracks


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("stx")
    ap.add_argument("st")
    ap.add_argument("--report", action="store_true")
    a = ap.parse_args()

    blob = open(a.stx, "rb").read()
    header, tracks = parse_stx(blob)

    # Geometry from the FAT boot sector (track 0 side 0 sector 1).
    t0 = next(t for t in tracks if t.cyl == 0 and t.side == 0)
    boot = next(s.data for s in t0.sectors if s.number == 1 and s.data)
    bps, spc, _rsv, _nfat, _ndir, total, _media, _spf, spt, sides = struct.unpack_from("<HBHBHHBHHH", boot, 11)
    if bps != 512 or not 1 <= sides <= 2 or not 8 <= spt <= 11:
        raise SystemExit(f"unexpected boot sector geometry bps={bps} spt={spt} sides={sides}")
    cyls = total // (spt * sides)

    by_pos = {(t.cyl, t.side): t for t in tracks}
    out = bytearray()
    missing, odd = [], []
    for cyl in range(cyls):
        for side in range(sides):
            t = by_pos.get((cyl, side))
            for num in range(1, spt + 1):
                sec = None
                if t:
                    cands = [s for s in t.sectors if s.number == num and s.data is not None and s.size_code == 2]
                    good = [s for s in cands if not s.fdc_flags & FDC_CRC]
                    sec = (good or cands or [None])[0]
                if sec is None:
                    missing.append((cyl, side, num))
                    out += bytes(512)
                else:
                    out += sec.data
            if t:
                for s in t.sectors:
                    if not (1 <= s.number <= spt) or s.size_code != 2 or s.fdc_flags or s.track != cyl:
                        odd.append((cyl, side, s.track, s.head, s.number, 128 << (s.size_code & 3), s.fdc_flags))
    extra_tracks = sorted(k for k in by_pos if k[0] >= cyls or k[1] >= sides)

    open(a.st, "wb").write(out)
    print(f"{a.stx}: Pasti v{header['version']} rev {header['revision']}, {header['tracks']} track records")
    print(f"  boot geometry: {cyls} cyl x {sides} side x {spt} spt = {len(out)} bytes -> {a.st}")
    print(f"  missing sectors: {len(missing)}; odd sectors (protection/ID mismatch): {len(odd)}; "
          f"tracks outside geometry: {extra_tracks}")
    if a.report:
        for m in missing:
            print("   missing", m)
        for o in odd:
            print("   odd cyl=%d side=%d id(trk=%d head=%d num=%d) size=%d fdc=0x%02x" % o)
        for t in tracks:
            if t.flags not in (0x00, 0x01, 0x21, 0x41, 0x61, 0xC1, 0xE1):
                print(f"   track {t.cyl}/{t.side} flags=0x{t.flags:02x}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
