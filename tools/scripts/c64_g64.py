#!/usr/bin/env python3
"""Decode a C64 1541 G64 (GCR-1541) image to a standard 35-track D64 (683 sectors).

Usage (from the repo root, `python3 -I`):
    tools/scripts/c64_g64.py IN.g64 OUT.d64 [--report]

Reads every full track 1..35 (half-track index 2·(t−1)) as a circular bit stream, finds each sync (≥ 10 one bits),
decodes the GCR block after it: a header block (0x08, checksum, sector, track, id2, id1) is matched with the next
data block (0x07, 256 bytes, checksum). A sector is kept when its header's track equals the track read and both
checksums are good; the first good copy wins. Sectors not found are filled with 0x00 and listed (`--report` lists
every track's count). Exit status 1 if any sector is missing. Copy protection (extra tracks 36+, half tracks, odd
syncs) is ignored: only the standard sectors are read.

Library: `decode(data) -> (d64_bytes, missing [(track, sector)], {track: sectors decoded})`.
"""
from __future__ import annotations

import sys
from pathlib import Path

GCR = {0x0A: 0, 0x0B: 1, 0x12: 2, 0x13: 3, 0x0E: 4, 0x0F: 5, 0x16: 6, 0x17: 7,
       0x09: 8, 0x19: 9, 0x1A: 10, 0x1B: 11, 0x0D: 12, 0x1D: 13, 0x1E: 14, 0x15: 15}


def sectors_per_track(track: int) -> int:
    return 21 if track <= 17 else 19 if track <= 24 else 18 if track <= 30 else 17


def d64_offset(track: int, sector: int) -> int:
    return (sum(sectors_per_track(t) for t in range(1, track)) + sector) * 256


def gcr_decode(bits: str, pos: int, nbytes: int) -> bytes | None:
    """nbytes plain bytes from the bit string at pos (10 GCR bits per byte), None on a bad code."""
    out = bytearray()
    for i in range(nbytes):
        b = bits[pos + 10 * i: pos + 10 * i + 10]
        if len(b) < 10:
            return None
        hi = GCR.get(int(b[:5], 2))
        lo = GCR.get(int(b[5:], 2))
        if hi is None or lo is None:
            return None
        out.append(hi << 4 | lo)
    return bytes(out)


def blocks(track_bytes: bytes):
    """Yield (bit position after the sync, bit string) for each sync on the track; the string is the track twice
    (wrap-around)."""
    n = len(track_bytes) * 8
    if n == 0:
        return
    raw = "".join(f"{b:08b}" for b in track_bytes)
    bits = raw + raw  # wrap-around
    i = 0
    while i < n:
        j = bits.find("1111111111", i)
        if j < 0 or j >= n:
            break
        k = j
        while k < len(bits) and bits[k] == "1":
            k += 1
        yield k, bits
        i = k


def decode_track(track_bytes: bytes, track: int) -> dict[int, bytes]:
    found: dict[int, bytes] = {}
    pending = None  # sector number from the last good header
    for pos, bits in blocks(track_bytes):
        first = gcr_decode(bits, pos, 1)
        if first is None:
            pending = None
            continue
        if first[0] == 0x08:
            h = gcr_decode(bits, pos, 6)
            if h and h[1] == h[2] ^ h[3] ^ h[4] ^ h[5] and h[3] == track:
                pending = h[2]
            else:
                pending = None
        elif first[0] == 0x07 and pending is not None:
            d = gcr_decode(bits, pos, 258)
            if d:
                chk = 0
                for b in d[1:257]:
                    chk ^= b
                if chk == d[257] and pending not in found:
                    found[pending] = d[1:257]
            pending = None
    return found


def decode(data: bytes):
    if data[:8] != b"GCR-1541":
        raise ValueError("not a G64 image")
    ntracks = data[9]
    offs = [int.from_bytes(data[12 + 4 * i:16 + 4 * i], "little") for i in range(ntracks)]
    d64 = bytearray(683 * 256)
    missing = []
    counts = {}
    for track in range(1, 36):
        o = offs[2 * (track - 1)]
        secs = {}
        if o:
            ln = int.from_bytes(data[o:o + 2], "little")
            secs = decode_track(data[o + 2:o + 2 + ln], track)
        counts[track] = len(secs)
        for s in range(sectors_per_track(track)):
            if s in secs:
                d64[d64_offset(track, s):d64_offset(track, s) + 256] = secs[s]
            else:
                missing.append((track, s))
    return bytes(d64), missing, counts


def main() -> int:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    if len(args) != 2:
        print(__doc__)
        return 2
    d64, missing, counts = decode(Path(args[0]).read_bytes())
    Path(args[1]).write_bytes(d64)
    if "--report" in sys.argv:
        for t, c in counts.items():
            print(f"track {t:2}: {c}/{sectors_per_track(t)}")
    print(f"{args[0]}: {683 - len(missing)}/683 sectors" + (f", missing {missing}" if missing else ""))
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
