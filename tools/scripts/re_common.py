"""Shared helpers for Phantasie I DOS data-extraction scripts.

All scripts read the untouched master files under
``reverse-engineering/dos/original-files`` and write JSON into
``reverse-engineering/dos/extracted``. Never mutate the originals.
"""
from __future__ import annotations

import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
DOS = REPO / "reverse-engineering" / "dos"
ORIG = DOS / "original-files"
OUT = DOS / "extracted"


def read(name: str) -> bytes:
    return (ORIG / name).read_bytes()


def write_json(name: str, obj) -> Path:
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / name
    path.write_text(json.dumps(obj, indent=2, ensure_ascii=False) + "\n")
    return path


def cstrings_at(data: bytes, start: int, end: int):
    """Yield (offset, text) for NUL-terminated ASCII runs in [start, end).

    Records the byte offset of each string so specs can cite exact locations.
    """
    i = start
    while i < end:
        if data[i] == 0:
            i += 1
            continue
        j = i
        while j < end and data[j] != 0:
            j += 1
        chunk = data[i:j]
        if all(9 <= b < 127 for b in chunk):
            yield i, chunk.decode("latin1")
        i = j + 1


# --- PHANTASI.COM is EXEPACK-packed (s22) -----------------------------------
# File layout: a small COM loader stub, then an embedded MZ header at file 0xf0
# (load image at 0xf0 + 16*e_cparhdr = 0x2f0) whose entry point is a Microsoft
# EXEPACK stub (header "RB" at image CS:0). Data in the raw file is therefore
# run-length packed (0xb0 = fill, 0xb2 = copy, decoded backwards); only long
# literal runs survive byte-for-byte, which is why older extracts needed
# per-window "file = DS + delta" fudges. Read data from the unpacked image.

COM_MZ_OFFSET = 0xF0
COM_DS_PARA = 0x0C9F  # DS = SS of the unpacked program (s22)


def unpack_phantasi_com(data: bytes | None = None) -> bytes:
    """Return the unpacked PHANTASI.COM load image (relocations not applied)."""
    import struct

    f = data if data is not None else read("PHANTASI.COM")
    mz = COM_MZ_OFFSET
    if f[mz:mz + 2] != b"MZ":
        raise ValueError("embedded MZ header not found at 0xf0")
    cparhdr = struct.unpack_from("<H", f, mz + 8)[0]
    cs = struct.unpack_from("<H", f, mz + 0x16)[0]
    img = mz + cparhdr * 16
    return _exepack(f[img:], cs * 16)


def _exepack(image: bytes, hdr: int) -> bytes:
    """Undo Microsoft EXEPACK on a load image whose EXEPACK header ("RB") sits at image offset ``hdr``."""
    import struct

    *_, dest_len, sig = struct.unpack_from("<HHHHHHH2s", image, hdr)
    if sig != b"RB":
        raise ValueError("EXEPACK signature missing")
    out = bytearray(dest_len * 16)
    out[:hdr] = image[:hdr]
    src = hdr
    while out[src - 1] == 0xFF:  # stub skips up to 15 bytes of 0xff padding
        src -= 1
    dst = len(out)
    while True:
        op = out[src - 1]
        cnt = out[src - 3] | out[src - 2] << 8
        src -= 3
        if op & 0xFE == 0xB0:
            fill = out[src - 1]
            src -= 1
            out[dst - cnt:dst] = bytes([fill]) * cnt
        elif op & 0xFE == 0xB2:
            out[dst - cnt:dst] = out[src - cnt:src]
            src -= cnt
        else:
            raise ValueError(f"bad EXEPACK opcode {op:#x}")
        dst -= cnt
        if op & 1:
            return bytes(out)


def unlzexe090(f: bytes) -> tuple[bytes, int]:
    """Undo LZEXE 0.90 on an MZ file: (inner load image, inner CS in paragraphs). Relocations not applied."""
    import struct

    if f[:2] != b"MZ" or f[0x1C:0x20] != b"LZ09":
        raise ValueError("not an LZEXE 0.90 file")
    hdr = struct.unpack_from("<H", f, 8)[0] * 16
    loader = hdr + struct.unpack_from("<H", f, 0x16)[0] * 16
    _ip, cs, *_ = struct.unpack_from("<7H", f, loader)
    data = f[hdr:loader]
    pos = 0
    out = bytearray()

    def word() -> int:
        nonlocal pos
        w = data[pos] | data[pos + 1] << 8
        pos += 2
        return w

    bits, count = word(), 16

    def bit() -> int:
        nonlocal bits, count
        b = bits & 1
        bits >>= 1
        count -= 1
        if count == 0:
            bits, count = word(), 16
        return b

    def byte() -> int:
        nonlocal pos
        pos += 1
        return data[pos - 1]

    while True:
        if bit():
            out.append(byte())
            continue
        if not bit():
            n = (bit() << 1 | bit()) + 2
            span = byte() - 0x100
        else:
            lo, hi = byte(), byte()
            span = (lo | (hi & 0xF8) << 5 | 0xE000) - 0x10000
            n = hi & 7
            if n == 0:
                n = byte()
                if n == 0:
                    break
                if n == 1:  # segment normalisation marker
                    continue
                n += 1
            else:
                n += 2
        for _ in range(n):
            out.append(out[span])
    return bytes(out), cs


def unpack_phantasi_exe(data: bytes) -> bytes:
    """The Bonus Edition's PHANTASI.EXE (WizardWorks 1990/91): LZEXE 0.90 over EXEPACK. The result equals 1987's
    unpacked PHANTASI.COM image except DS:0x3094 (bonus-edition.md)."""
    inner, cs = unlzexe090(data)
    return _exepack(inner, cs * 16)


def unpack_phantasi(data: bytes) -> bytes:
    """PHANTASI.COM (1987) or PHANTASI.EXE (Bonus Edition) to the same unpacked load image."""
    if data[:2] == b"MZ" and data[0x1C:0x20] == b"LZ09":
        return unpack_phantasi_exe(data)
    return unpack_phantasi_com(data)


def com_ds(image: bytes, ds_offset: int, length: int) -> bytes:
    """Slice ``length`` bytes at ``DS:ds_offset`` of the unpacked image."""
    base = COM_DS_PARA * 16 + ds_offset
    return image[base:base + length]
