#!/usr/bin/env python3
"""Extract every file from the Phantasie I C64 (German) D64 disk images.

Standard 35-track D64 layout: directory chain starts at track 18 sector 1,
8 × 32-byte entries per sector; files are sector chains (byte 0/1 = next
track/sector, track 0 = last sector, byte 1 then = index of last used byte).

Input:  reverse-engineering/c64/original-images/phantasie1/de/*.d64 (never modified)
Output: reverse-engineering/c64/extracted/<side>/<NAME>.<type>
        reverse-engineering/c64/extracted/manifest.json
        (PRG load address = first 2 bytes, little endian)
"""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
C64 = REPO / "reverse-engineering" / "c64"
IMAGES = C64 / "original-images" / "phantasie1" / "de"
OUT = C64 / "extracted"

TYPES = {0: "del", 1: "seq", 2: "prg", 3: "usr", 4: "rel"}


def sectors_per_track(track: int) -> int:
    if track <= 17:
        return 21
    if track <= 24:
        return 19
    if track <= 30:
        return 18
    return 17


def offset(track: int, sector: int) -> int:
    return (sum(sectors_per_track(t) for t in range(1, track)) + sector) * 256


def read_chain(img: bytes, track: int, sector: int) -> bytes:
    out = bytearray()
    seen = set()
    while track:
        if (track, sector) in seen:
            raise ValueError(f"sector loop at {track}/{sector}")
        seen.add((track, sector))
        blk = img[offset(track, sector):offset(track, sector) + 256]
        nt, ns = blk[0], blk[1]
        out += blk[2:] if nt else blk[2:ns + 1]
        track, sector = nt, ns
    return bytes(out)


def petscii_name(raw: bytes) -> str:
    raw = raw.rstrip(b"\xa0")
    return "".join(chr(b) if 0x20 <= b < 0x7f else f"{{{b:02x}}}" for b in raw)


def safe(name: str) -> str:
    return re.sub(r"[^A-Za-z0-9.+-]", "_", name.strip()) or "_"


def directory(img: bytes):
    track, sector = 18, 1
    while track:
        blk = img[offset(track, sector):offset(track, sector) + 256]
        for i in range(8):
            e = blk[i * 32:i * 32 + 32]
            ftype = e[2]
            if ftype & 0x07 == 0 and not ftype & 0x80:
                continue  # scratched / empty slot
            yield {
                "name": petscii_name(e[5:21]),
                "type": TYPES.get(ftype & 0x07, f"?{ftype:02x}"),
                "closed": bool(ftype & 0x80),
                "start": (e[3], e[4]),
                "blocks": e[30] | e[31] << 8,
            }
        track, sector = blk[0], blk[1]


def main() -> None:
    manifest = {}
    for image in sorted(IMAGES.glob("*.d64")):
        img = image.read_bytes()
        side = "side_" + image.stem.rsplit("_", 1)[-1].lower()
        dest = OUT / side
        dest.mkdir(parents=True, exist_ok=True)
        disk = img[offset(18, 0):offset(18, 0) + 256]
        files = []
        used = set()
        for ent in directory(img):
            data = read_chain(img, *ent["start"])
            fname = f"{safe(ent['name'])}.{ent['type']}"
            while fname in used:
                fname = "_" + fname
            used.add(fname)
            (dest / fname).write_bytes(data)
            rec = {
                "name": ent["name"],
                "file": f"{side}/{fname}",
                "type": ent["type"],
                "blocks": ent["blocks"],
                "size": len(data),
                "sha256": hashlib.sha256(data).hexdigest(),
            }
            if ent["type"] == "prg" and len(data) >= 2:
                load = data[0] | data[1] << 8
                rec["load"] = f"0x{load:04x}"
                rec["end"] = f"0x{load + len(data) - 3:04x}"
            files.append(rec)
        manifest[image.name] = {"disk_name": petscii_name(disk[0x90:0xa0]), "files": files}
        print(f"{image.name}: {len(files)} files -> {dest.relative_to(REPO)}")
    (OUT / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")


if __name__ == "__main__":
    main()
