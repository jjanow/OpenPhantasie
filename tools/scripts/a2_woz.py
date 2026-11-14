#!/usr/bin/env python3
"""Apple II `.woz` (WOZ 1 / WOZ 2) flux images, 16-sector 6-and-2 disks only, to a DOS-order 140K `.dsk`.

The Python twin of the importer's `WozImage.cs` for 16-sector disks (RDOS 3.3: Phantasie II v2.1 and III; the
13-sector 5-and-3 decode of Phantasie I / II stays in C#). Format: `reverse-engineering/apple2/phantasie2/FORMAT.md`
section 1. Per track (TMAP quarter-track 4t): the bit stream is read twice round (a sector across the splice is still
found), bytes are shifted in until the top bit is set; address field D5 AA 96 + 4-and-4 volume / track / sector /
checksum, data field D5 AA AD + 342 + 1 nibbles (6-and-2, XOR chain, checksum). The first copy of a sector whose
address checksum, track number and data checksum hold is kept. Output: DOS 3.3 logical sector l of track t at
t * 4096 + l * 256 holds physical sector DOS33_TO_IMAGE[l] (as `WozImage` and `a2_rdos.DOS33_TO_IMAGE`).

Usage: python3 -I a2_woz.py <in.woz> <out.dsk>
Library: `decode(data) -> (image, missing)`; `load_image(path)` reads a `.dsk` as is or decodes a `.woz`.
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

DOS33_TO_IMAGE = (0, 13, 11, 9, 7, 5, 3, 1, 14, 12, 10, 8, 6, 4, 2, 15)
NIBBLES_62 = bytes.fromhex(
    "96979A9B9D9E9FA6A7ABACADAEAFB2B3B4B5B6B7B9BABBBCBDBEBFCBCDCECFD3"
    "D6D7D9DADBDCDDDEDFE5E6E7E9EAEBECEDEEEFF2F3F4F5F6F7F9FAFBFCFDFEFF")
DECODE_62 = {b: i for i, b in enumerate(NIBBLES_62)}


def _chunks(data: bytes) -> dict[bytes, bytes]:
    out, p = {}, 12
    while p + 8 <= len(data):
        cid, size = data[p:p + 4], struct.unpack_from("<I", data, p + 4)[0]
        out[cid] = (p + 8, size)
        p += 8 + size
    return out


def _track_bits(data: bytes, version: int, chunks: dict, track: int) -> tuple[bytes, int] | None:
    tmap_off, _ = chunks[b"TMAP"]
    index = data[tmap_off + 4 * track]
    if index == 0xFF:
        return None
    trks_off, _ = chunks[b"TRKS"]
    if version == 1:
        base = trks_off + index * 6656
        bit_count = struct.unpack_from("<H", data, base + 6648)[0]
        return data[base:base + 6646], bit_count
    start, blocks, bit_count = struct.unpack_from("<HHI", data, trks_off + index * 8)
    return data[start * 512:(start + blocks) * 512], bit_count


def _disk_bytes(bits: bytes, bit_count: int) -> list[int]:
    out, reg = [], 0
    for turn in range(2):
        for i in range(bit_count):
            reg = ((reg << 1) | ((bits[i >> 3] >> (7 - (i & 7))) & 1)) & 0xFF
            if reg & 0x80:
                out.append(reg)
                reg = 0
    return out


def _sectors(stream: list[int], track: int) -> dict[int, bytes]:
    found: dict[int, bytes] = {}
    n = len(stream)
    i = 0
    while i + 3 < n:
        if stream[i:i + 3] != [0xD5, 0xAA, 0x96] or i + 11 > n:
            i += 1
            continue
        f = [((stream[i + 3 + 2 * k] << 1) | 1) & stream[i + 4 + 2 * k] for k in range(4)]
        vol, trk, sec, chk = f
        i += 11
        if vol ^ trk ^ sec ^ chk or trk != track or sec in found:
            continue
        j = i
        while j + 3 < n and j < i + 64 and stream[j:j + 3] != [0xD5, 0xAA, 0xAD]:
            j += 1
        if j + 3 + 343 > n or stream[j:j + 3] != [0xD5, 0xAA, 0xAD]:
            continue
        raw = stream[j + 3:j + 3 + 343]
        if any(b not in DECODE_62 for b in raw):
            continue
        vals, prev = [], 0
        for b in raw:
            prev ^= DECODE_62[b]
            vals.append(prev)
        if prev != 0:  # the running XOR through the checksum nibble ends at 0
            continue
        aux, main = vals[:86], vals[86:342]
        sector = bytearray(256)
        for k in range(256):
            bits2 = (aux[k % 86] >> (2 * (k // 86))) & 3
            sector[k] = ((main[k] << 2) | ((bits2 & 1) << 1) | (bits2 >> 1)) & 0xFF
        found[sec] = bytes(sector)
    return found


def decode(data: bytes) -> tuple[bytes, list[tuple[int, int]]]:
    if data[:4] not in (b"WOZ1", b"WOZ2"):
        raise ValueError("not a WOZ image")
    version = 1 if data[:4] == b"WOZ1" else 2
    chunks = _chunks(data)
    image = bytearray(35 * 4096)
    missing = []
    for t in range(35):
        tb = _track_bits(data, version, chunks, t)
        secs = _sectors(_disk_bytes(*tb), t) if tb else {}
        for logical in range(16):
            phys = DOS33_TO_IMAGE[logical]
            if phys in secs:
                image[t * 4096 + logical * 256:t * 4096 + (logical + 1) * 256] = secs[phys]
            else:
                missing.append((t, phys))
    return bytes(image), missing


def load_image(path) -> bytes:
    data = Path(path).read_bytes()
    if data[:4] in (b"WOZ1", b"WOZ2"):
        image, _ = decode(data)
        return image
    return data


if __name__ == "__main__":
    img, miss = decode(Path(sys.argv[1]).read_bytes())
    Path(sys.argv[2]).write_bytes(img)
    print(f"{len(miss)} sectors missing")
