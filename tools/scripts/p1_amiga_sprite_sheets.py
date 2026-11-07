#!/usr/bin/env python3
"""Check I's Amiga sprite colours on FS-UAE (s125): pack sprites into two 320x200 pictures and show them on the title.

    p1_amiga_sprite_sheets.py build ADF_COPY OUTDIR      sheets + layout to OUTDIR; their bytes replace decor.pic / final.pic
                                                         in ADF_COPY (run p1_amiga_patch_title.py ADF_COPY decor.pic
                                                         final.pic 0x94 first: title in the combat table)
    p1_amiga_sprite_sheets.py compare OUTDIR A.png B.png [OX OY]
                                                         A / B = FS-UAE window shots of the two sheets (fsuae_run.sh with
                                                         "scale_x = 1.0" "scale_y = 1.0" "texture_filter = nearest": the
                                                         Amiga screen at 2x, origin at window (160, 100))

The sheets hold the sprites' colour indices (same decode as extract_amiga_graphics.py), portraits first, then monsters by
height, as many as fit. compare samples every sprite pixel and counts matches against the extracted PNGs (hw4) and,
with OLD_REV=<git rev> set, against that revision's PNGs. The title path (h77 +0x524 loader, LoadRGB4 of the patched table)
stands in for the combat screen, which LoadRGB4s the same table (h53 +0x33c; FORMAT.md "Sprite colours on screen")."""
from __future__ import annotations

import io
import json
import os
import struct
import subprocess
import sys
from pathlib import Path

from PIL import Image

from amiga_gfx_common import EXTRACTED, REPO, UNPACKED, decode_planar

IDENT = [(i, 0, 0, 255) for i in range(16)]


def build(adf: str, out: Path) -> None:
    out.mkdir(parents=True, exist_ok=True)
    idx = json.loads((EXTRACTED / "extraction-index.json").read_text())
    items = []
    for group, files in (("monsters", ("mstr1.pat", "mstr2.pat")), ("races", ("party1.pat", "party2.pat"))):
        for rec in idx[group]:
            for kind, f in zip(("idle", "attacking"), files):
                data = (UNPACKED / f).read_bytes()
                items.append((rec[kind], decode_planar(data, rec["pat_offset"], rec["width"], rec["height"], IDENT)))
    items.sort(key=lambda t: (not t[0].startswith("portraits"), -t[1].height))
    sheets, layout, x, y, rowh = [], [], 0, 0, 0
    for rel, im in items:
        w, h = im.size
        if sheets and x + w > 320:
            x, y, rowh = 0, y + rowh, 0
        if not sheets or y + h > 200:
            if len(sheets) == 2:
                continue
            sheets.append(Image.new("L", (320, 200), 0))
            x, y, rowh = 0, 0, 0
        px = im.load()
        for j in range(h):
            for i in range(w):
                sheets[-1].putpixel((x + i, y + j), px[i, j][0])
        layout.append({"sheet": len(sheets) - 1, "path": rel, "x": x, "y": y, "w": w, "h": h})
        x, rowh = x + w, max(rowh, h)
    for n, (sheet, name) in enumerate(zip(sheets, ("decor.pic", "final.pic"))):
        p, planes = sheet.load(), bytearray(32000)
        for yy in range(200):
            for xx in range(320):
                for b in range(4):
                    if p[xx, yy] >> b & 1:
                        planes[b * 8000 + yy * 40 + xx // 8] |= 0x80 >> (xx % 8)
        (out / f"sheet{n}.pic").write_bytes(planes)
        ofs_replace(adf, name, bytes(planes))
    (out / "layout.json").write_text(json.dumps(layout, indent=1) + "\n")
    print(len(layout), "of", len(items), "sprite frames on", len(sheets), "sheets")


def ofs_replace(adf: str, name: str, new: bytes) -> None:
    """Overwrite an OFS file's data in place (same size, <= 72 data blocks), following its header's block table."""
    d = bytearray(Path(adf).read_bytes())
    u = lambda o: struct.unpack_from(">I", d, o)[0]  # noqa: E731
    hdrs = [b for b in range(len(d) // 512) if u(b * 512) == 2 and struct.unpack_from(">i", d, b * 512 + 508)[0] == -3
            and d[b * 512 + 433:b * 512 + 433 + d[b * 512 + 432]].lower() == name.encode().lower()]
    assert len(hdrs) == 1, hdrs
    h = hdrs[0] * 512
    assert u(h + 324) == len(new) and u(h + 8) <= 72 and u(h + 504) == 0
    for i in range(u(h + 8)):
        b = u(h + 24 + (71 - i) * 4) * 512
        assert u(b) == 8 and u(b + 8) == i + 1
        n = u(b + 12)
        d[b + 24:b + 24 + n] = new[i * 488:i * 488 + n]
        d[b + 20:b + 24] = b"\0\0\0\0"
        d[b + 20:b + 24] = struct.pack(">I", -sum(struct.unpack(">128I", d[b:b + 512])) & 0xFFFFFFFF)
    Path(adf).write_bytes(d)


def compare(out: Path, shots: list[str], ox: int, oy: int) -> None:
    layout = json.loads((out / "layout.json").read_text())
    caps = [Image.open(s).convert("RGB").load() for s in shots]
    old_rev = os.environ.get("OLD_REV")
    tally = {"pixels": 0, "ink": 0, "new": 0, "new_ink": 0, "old": 0, "old_ink": 0}
    for it in layout:
        new = Image.open(EXTRACTED / it["path"]).convert("RGB").load()
        old = None
        if old_rev:
            rel = (EXTRACTED / it["path"]).relative_to(REPO)
            blob = subprocess.run(["git", "-C", str(REPO), "show", f"{old_rev}:{rel}"], capture_output=True, check=True)
            old = Image.open(io.BytesIO(blob.stdout)).convert("RGB").load()
        cap = caps[it["sheet"]]
        for j in range(it["h"]):
            for i in range(it["w"]):
                c = cap[ox + 2 * (it["x"] + i), oy + 2 * (it["y"] + j)]
                ink = new[i, j] != (0, 0, 0)
                tally["pixels"] += 1
                tally["ink"] += ink
                tally["new"] += c == new[i, j]
                tally["new_ink"] += ink and c == new[i, j]
                if old:
                    tally["old"] += c == old[i, j]
                    tally["old_ink"] += ink and c == old[i, j]
    print(len(layout), "frames", tally)


if __name__ == "__main__":
    if sys.argv[1] == "build":
        build(sys.argv[2], Path(sys.argv[3]))
    else:
        ox, oy = (int(sys.argv[5]), int(sys.argv[6])) if len(sys.argv) > 6 else (160, 100)
        compare(Path(sys.argv[2]), sys.argv[3:5], ox, oy)
