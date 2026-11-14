#!/usr/bin/env python3
"""Extract every file from C64 D64 (or G64) disk images.

Default (no arguments): the Phantasie I German D64s below. With arguments:
    extract_c64_d64.py --out DIR IMAGE[.d64|.g64] ...
writes DIR/<image stem>/<NAME>.<type> and DIR/manifest.json. G64 images are decoded to their 683 standard
sectors first (`c64_g64.py`).

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
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import c64_g64  # noqa: E402

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
                "record_length": e[23],
            }
        track, sector = blk[0], blk[1]


def load_image(image: Path) -> bytes:
    data = image.read_bytes()
    if data[:8] == b"GCR-1541":
        data, missing, _ = c64_g64.decode(data)
        if missing:
            print(f"{image.name}: {len(missing)} sectors not decoded: {missing}")
    return data


def safe_dir(stem: str) -> str:
    return re.sub(r"[^A-Za-z0-9.+-]", "_", stem)


def main() -> None:
    args = sys.argv[1:]
    out = OUT
    if "--out" in args:
        i = args.index("--out")
        out = Path(args[i + 1])
        del args[i:i + 2]
    images = [Path(a) for a in args] or sorted(IMAGES.glob("*.d64"))
    manifest = {}
    for image in images:
        img = load_image(image)
        side = safe_dir(image.stem) if args else "side_" + image.stem.rsplit("_", 1)[-1].lower()
        dest = out / side
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
                "sha1": hashlib.sha1(data).hexdigest(),
            }
            if ent["type"] == "rel":
                rec["record_length"] = ent["record_length"]
            if ent["type"] == "prg" and len(data) >= 2:
                load = data[0] | data[1] << 8
                rec["load"] = f"0x{load:04x}"
                rec["end"] = f"0x{load + len(data) - 3:04x}"
            files.append(rec)
        manifest[image.name] = {"disk_name": petscii_name(disk[0x90:0xa0]), "files": files}
        print(f"{image.name}: {len(files)} files -> {dest}")
    out.mkdir(parents=True, exist_ok=True)
    (out / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")


if __name__ == "__main__":
    main()
