#!/usr/bin/env python3
"""Shared helpers for Phantasie III (DOS, SSI 1988) static RE.

`MAIN.EXE` (152,564 B, MS C 5.x 1987 runtime) = an EXEPACK-packed root module plus four
MS-LINK overlays appended as separate MZ images (`docs/plan/03c-phantasie3-re.md` 3c.2,
`reverse-engineering/dos/phantasie3/FORMAT.md` §2).

Address conventions used by every P3 extractor and note:
- **img** = offset into the unpacked root load image (`unpack_root()`), segment 0 = img 0.
- **DS:x** = img `0x28c00 + x` (DGROUP paragraph 0x28c0; the MS C copyright string is at DS:8).
- **ovlN file** = offset into `MAIN.EXE`; **ovlN img** = offset into overlay N's load image
  (file − overlay MZ offset − header size).

Run with `python3 -I`. Reads only files under the repo.
"""
from __future__ import annotations

import json
import struct
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
P3 = REPO / "reverse-engineering" / "dos" / "phantasie3"
UNPACKED = P3 / "unpacked" / "ssi-1988"
EXTRACTED = P3 / "extracted"
MAIN_EXE = UNPACKED / "MAIN.EXE"

DS_IMG = 0x28C00  # DGROUP in the unpacked root image


def read_file(name: str) -> bytes:
    return (UNPACKED / name).read_bytes()


def mz_header(data: bytes, off: int = 0) -> dict:
    (sig, last, pages, nrel, hdrpar, minalloc, maxalloc, ss, sp, csum, ip, cs, relo,
     ovl) = struct.unpack_from("<2s13H", data, off)
    if sig != b"MZ":
        raise ValueError(f"no MZ at {off:#x}")
    size = (pages - 1) * 512 + (last or 512)
    return dict(off=off, size=size, nrel=nrel, hdr=hdrpar * 16, minalloc=minalloc,
                maxalloc=maxalloc, ss=ss, sp=sp, ip=ip, cs=cs, relo=relo, ovl=ovl)


def modules(data: bytes | None = None) -> list[dict]:
    """Root + overlays: each appended MZ starts at the next 512-byte boundary."""
    data = data if data is not None else MAIN_EXE.read_bytes()
    out, off = [], 0
    while off < len(data):
        h = mz_header(data, off)
        out.append(h)
        off += h["size"]
        off = (off + 511) & ~511
    return out


def unpack_root(data: bytes | None = None) -> tuple[bytes, list[tuple[int, int]], dict]:
    """Undo EXEPACK. Returns (image, relocations [(seg, off)], real entry regs)."""
    data = data if data is not None else MAIN_EXE.read_bytes()
    h = mz_header(data)
    img0 = h["hdr"]
    eh = img0 + h["cs"] * 16  # EXEPACK header
    rip, rcs, _mem, epsize, rsp, rss, dest_len = struct.unpack_from("<7H", data, eh)
    sig_at = 14 if data[eh + 14:eh + 16] == b"RB" else 16  # 16 = header with skip_len
    assert data[eh + sig_at:eh + sig_at + 2] == b"RB", "not EXEPACK"
    packed = data[img0:eh]
    out = bytearray(dest_len * 16)
    out[:len(packed)] = packed
    src, dst = len(packed), len(out)
    while out[src - 1] == 0xFF:
        src -= 1
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
            raise ValueError(f"bad EXEPACK op {op:#x}")
        dst -= cnt
        if op & 1:
            break
    # packed relocation table follows the "Packed file is corrupt" message
    msg = data.index(b"Packed file is corrupt", eh)
    p = msg + len(b"Packed file is corrupt")
    relocs = []
    for seg in range(16):
        n = struct.unpack_from("<H", data, p)[0]
        p += 2
        for _ in range(n):
            relocs.append((seg * 0x1000, struct.unpack_from("<H", data, p)[0]))
            p += 2
    regs = dict(ip=rip, cs=rcs, sp=rsp, ss=rss, exepack_size=epsize)
    return bytes(out), relocs, regs


OVERLAY_SEG = 0x13D5  # every overlay loads at img 0x13d50 (all overlay relocations name this segment)


def build_unpacked_mz(data: bytes | None = None, overlay: int | None = None,
                      patch_overlay_calls: bool = True) -> bytes:
    """An ordinary MZ (no EXEPACK) with the relocations restored, for Ghidra / rea.

    With `overlay=N`, overlay N's image is placed in the overlay area (img 0x13d50) and its
    relocations are added, so far calls between the overlay and the root resolve.
    """
    data = data if data is not None else MAIN_EXE.read_bytes()
    img, relocs, regs = unpack_root(data)
    if overlay is not None:
        oi, h = overlay_image(overlay, data)
        img = bytearray(img)
        base = OVERLAY_SEG * 16
        img[base:base + len(oi)] = oi
        img = bytes(img)
        for i in range(h["nrel"]):
            off, seg = struct.unpack_from("<HH", data, h["off"] + h["relo"] + 4 * i)
            relocs.append((seg, off))
    if patch_overlay_calls:
        # MS-LINK overlay calls are `int 3Fh; db ovl; dw offset` (5 bytes). For a disassembler,
        # rewrite them as `call far 13d5:offset` (also 5 bytes, + a relocation) when the target
        # overlay is the one loaded, else as `call far (0xE000 + 0x100*ovl):offset` (no memory
        # there, so the call is visible but unresolved).
        img = bytearray(img)
        sizes = {n: len(overlay_image(n, data)[0]) for n in range(1, 5)}
        p = img.find(b"\xcd\x3f", 0, DS_IMG)
        while p >= 0:
            n, off = img[p + 2], img[p + 3] | img[p + 4] << 8
            if 1 <= n <= 4 and off < sizes[n]:
                seg = OVERLAY_SEG if n == overlay else 0xE000 + 0x100 * n
                struct.pack_into("<BHH", img, p, 0x9A, off, seg)
                if n == overlay:
                    relocs.append((((p + 3) >> 16) << 12, (p + 3) & 0xFFFF))
            p = img.find(b"\xcd\x3f", p + 2, DS_IMG)
        img = bytes(img)
    hdr_len = (0x1C + 4 * len(relocs) + 15) & ~15
    hdr_len = (hdr_len + 511) & ~511
    total = hdr_len + len(img)
    hdr = bytearray(hdr_len)
    struct.pack_into("<2s13H", hdr, 0, b"MZ", total % 512, (total + 511) // 512, len(relocs),
                     hdr_len // 16, 0x156A, 0xFFFF, regs["ss"], regs["sp"], 0, regs["ip"],
                     regs["cs"], 0x1C, 0)
    for i, (seg, off) in enumerate(relocs):
        struct.pack_into("<HH", hdr, 0x1C + 4 * i, off, seg)
    return bytes(hdr) + img


def overlay_image(n: int, data: bytes | None = None) -> tuple[bytes, dict]:
    data = data if data is not None else MAIN_EXE.read_bytes()
    h = [m for m in modules(data) if m["ovl"] == n][0]
    return data[h["off"] + h["hdr"]:h["off"] + h["size"]], h


def ds(img: bytes, x: int) -> int:
    return DS_IMG + x


def cstr(img: bytes, at: int, enc: str = "latin-1") -> str:
    e = img.index(b"\0", at)
    return img[at:e].decode(enc)


def far_string_table(img: bytes, ds_off: int, count: int) -> list[str | None]:
    """Far pointers (off, seg) into DGROUP; seg must be 0x28c0."""
    out = []
    for i in range(count):
        off, seg = struct.unpack_from("<HH", img, DS_IMG + ds_off + 4 * i)
        out.append(cstr(img, DS_IMG + off) if seg == 0x28C0 else None)
    return out


def write_json(name: str, obj) -> Path:
    EXTRACTED.mkdir(parents=True, exist_ok=True)
    p = EXTRACTED / name
    p.write_text(json.dumps(obj, indent=1, ensure_ascii=False) + "\n")
    return p


if __name__ == "__main__":
    import hashlib
    import sys

    data = MAIN_EXE.read_bytes()
    for m in modules(data):
        print(f"module ovl={m['ovl']} file {m['off']:#07x} size {m['size']:#07x} hdr {m['hdr']:#x} "
              f"relocs {m['nrel']} cs:ip {m['cs']:04x}:{m['ip']:04x}")
    img, rel, regs = unpack_root(data)
    print(f"root unpacked: {len(img):#x} B, {len(rel)} relocations, entry {regs['cs']:04x}:{regs['ip']:04x} "
          f"ss:sp {regs['ss']:04x}:{regs['sp']:04x}; sha1 {hashlib.sha1(img).hexdigest()}")
    if len(sys.argv) > 1:  # write Ghidra inputs to a directory outside the repo
        out = Path(sys.argv[1])
        out.mkdir(parents=True, exist_ok=True)
        (out / "main_root_unpacked.exe").write_bytes(build_unpacked_mz(data))
        (out / "main_root.img").write_bytes(img)
        for n in range(1, 5):
            oi, h = overlay_image(n, data)
            (out / f"main_ovl{n}.img").write_bytes(oi)
            (out / f"main_with_ovl{n}.exe").write_bytes(build_unpacked_mz(data, n))
        print("wrote", out)
