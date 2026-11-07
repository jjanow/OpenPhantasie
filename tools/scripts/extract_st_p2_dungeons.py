#!/usr/bin/env python3
"""Phantasie II (Atari ST): DNG1-8 + MESS1-8 -> extracted/dungeons/ (task 3b.8).

DNGn (2048 B each; Amiga I / DOS I 1922-1984 B): the Phantasie I level format, unchanged [C]:
    PHANT.PRG 0x10320 reads DNGn into a 2500-byte buffer [0x25f62] (unless the DNG.SAV slot matches, then DNGX.SAV);
    cell = buf[y * 33 + x] (0x1066c), 33 x 38; monster bytes at buf + 0x468 + cell (0x10d44 reads +1128);
    event records at +0x4f6 (0x11ad2) and scripts at +0x5aa (0x11b3e), the DOS offsets
    (dungeon-representation.md, endgame.md section 2.1). Bytes past the level data hold Phantasie I's "A STRANGE PURPLE
    GAS F / ENDING SEQUENCE" leftovers and 0x0d/0x00 fill.
MESSn: plain text (not encrypted on the ST; DOS I's MESS files are). PHANT.PRG 0x1159e is DOS OV2 0x2418: slot word
    w = u16 LE at 2 * (id - 1), empty when equal to the next word; the record is 1 prefix byte + 40 chars at
    0xe6 + w [C]. The 4000-byte buffer [0x24eb8] is read in full (0x1038a).

Writes: dungeons.json (shape of GameData/phantasie1/dungeons.json: id "p2:n", raw_hex, messages), messages.json
(shape of Phantasie I's messages.json, no cipher), ascii/DNGn.txt, png/DNGn.png.
Run from the repo root: python3 -I tools/scripts/extract_st_p2_dungeons.py
"""
from __future__ import annotations

import os
import re
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_st_p2_common import DUNGEONS, amiga, d2, out_dir, rel, write_json, write_text  # noqa: E402
from extract_dungeon_events import MAX_ID, TEXT_BASE, continuation_of, message, referenced_ids  # noqa: E402

GW, GH = 33, 38
GRID = GW * GH
REC = 41
ASCII_RUN = re.compile(rb"[ -~]{6,}")


def cell_char(b: int) -> str:
    """DOS legend (dungeon-representation.md); [~] for II until its handlers are compared."""
    if b == 0:
        return "#"
    if b in (1, 7):
        return "."
    if b <= 6:
        return "t"           # trap
    if b <= 0x11:
        return ","           # wandering-encounter floor
    if b <= 0x7F:
        return "d"           # description cell
    if b <= 0x89:
        return "F"           # forced fight
    if b == 0x8A:
        return "x"           # cleared fight
    if b <= 0xB8:
        return "m"           # message cell
    if b <= 0xD6:
        return "L"           # lock / ambush
    if b <= 0xD9:
        return "E"           # exit
    if b <= 0xDC:
        return "S"           # stairs message
    return "*"               # scripted event


def render(n: int, raw: bytes) -> None:
    lines = [f"DNG{n} (33 x 38). DOS legend [~ for II]: # wall  . floor  t trap  , wandering-fight floor  "
             "d description  F forced fight  x cleared fight  m message  L lock/ambush  E exit  S stairs  * script",
             "    " + "".join(str(x % 10) for x in range(GW))]
    for y in range(GH):
        lines.append(f"{y:3d} " + "".join(cell_char(raw[y * GW + x]) for x in range(GW)))
    write_text("dungeons/ascii", f"DNG{n}.txt", "\n".join(lines) + "\n")
    try:
        from PIL import Image, ImageDraw
    except ImportError:
        return
    pal = {"#": (40, 40, 40), ".": (220, 220, 220), "t": (230, 60, 60), ",": (200, 200, 170), "d": (180, 200, 230),
           "F": (200, 0, 0), "x": (150, 80, 80), "m": (120, 160, 220), "L": (230, 170, 0), "E": (0, 200, 0),
           "S": (0, 140, 0), "*": (200, 0, 200)}
    s = 8
    img = Image.new("RGB", (GW * s, GH * s))
    dr = ImageDraw.Draw(img)
    for y in range(GH):
        for x in range(GW):
            dr.rectangle([x * s, y * s, x * s + s - 1, y * s + s - 1], fill=pal[cell_char(raw[y * GW + x])])
    img.resize((GW * s * 2, GH * s * 2), Image.NEAREST).save(out_dir("dungeons/png") / f"DNG{n}.png")


def mess_area(n: int, plain: bytes) -> dict:
    slots = []
    for sid in range(MAX_ID + 1):
        w = struct.unpack_from("<H", plain, 2 * sid)[0]
        nxt = struct.unpack_from("<H", plain, 2 * sid + 2)[0] if 2 * sid + 4 <= TEXT_BASE + 2 else None
        e = {"id": sid + 1, "offset": w, "empty": w == nxt}
        if w != nxt:
            p = TEXT_BASE + w
            r = plain[p:p + REC]
            e.update({"file_offset": p, "prefix": r[0] if r else None,
                      "text": r[1:].decode("latin-1").rstrip()})
        slots.append(e)
    recs = []
    pos = TEXT_BASE
    while pos + REC <= len(plain):
        r = plain[pos:pos + REC]
        recs.append({"offset": pos, "record_index": (pos - TEXT_BASE) // REC, "prefix": r[0],
                     "text": r[1:].decode("latin-1").rstrip()})
        pos += REC
    am_same = [k for k in [f"mess{i}" for i in range(1, 11)] if (amiga(k) or b"") and len(amiga(k)) == len(plain)]
    return {"id": n, "source": f"MESS{n}", "size": len(plain), "text_base": TEXT_BASE,
            "same_size_as_amiga_p1": am_same, "trailing_bytes_hex": plain[pos:].hex(), "slots": slots,
            "records": recs}


def main() -> None:
    levels, areas, issues = {}, [], []
    for n in DUNGEONS:
        raw = d2(f"DNG{n}")
        plain = d2(f"MESS{n}")
        msgs = {}
        for mid in range(1, MAX_ID + 1):
            m = message(plain, mid)
            if m is not None:
                c = continuation_of(plain, mid)
                if c is not None:
                    m["continuation_of"] = c
                msgs[str(mid)] = m
        refs = referenced_ids(raw)
        missing, cont = [], []
        for mid, why in sorted(refs.items()):
            if str(mid) not in msgs:
                missing.append({"id": mid, "why": why})
            elif "continuation_of" in msgs[str(mid)]:
                cont.append({"id": mid, "why": why, "continuation_of": msgs[str(mid)]["continuation_of"]})
        if cont:
            issues.append({"level": f"DNG{n}", "references_to_continuations": cont})
        exits = [{"x": i % GW, "y": i // GW, "cell": hex(b)} for i, b in enumerate(raw[:GRID]) if 0xD7 <= b <= 0xD9]
        levels[f"DNG{n}"] = {
            "id": f"p2:{n}",
            "size": len(raw),
            "raw_hex": raw.hex(),
            "fight_monsters_0x4e8": list(raw[0x4E8:0x4F2]),
            "exit_cells": exits,
            "embedded_text": [{"offset": f"0x{m.start():04x}", "text": m.group().decode("latin-1")}
                              for m in ASCII_RUN.finditer(raw[GRID:]) if b" " in m.group()],
            "messages": msgs,
            "referenced_message_ids_missing": missing,
        }
        areas.append(mess_area(n, plain))
        render(n, raw)
    p1 = write_json("dungeons", "dungeons.json", {
        "_source": "Phantasie II ST disk2/DNG1..DNG8 + MESS1..MESS8 (static data; identical in the TOSEC set)",
        "_note": ("Phantasie I level format [C] (FORMAT.md section 9.4). raw_hex is the whole 2048-B file; the grid "
                  "is the first 1254 B (33 x 38, y * 33 + x). embedded_text lists ASCII runs after the grid "
                  "(Phantasie I leftovers, not used). messages: MESSn as DOS OV2 0x2a95 shows them, built with "
                  "extract_dungeon_events.message(); the II display routine is not yet compared [~]. "
                  "fight_monsters_0x4e8: monster ids for cells 0x80..0x89 (DOS rule [~] for II)."),
        "_grid_byte_length": GRID,
        "_checks": {"references_to_continuation_records": issues},
        "levels": levels,
    })
    p2 = write_json("dungeons", "messages.json", {
        "_source": "Phantasie II ST disk2/MESS1..MESS8",
        "_cipher": "none (plain text on the ST)",
        "_layout": "u16 LE slot table (115 words, ids 1..115) then 41-B records (prefix + 40 chars) from 0xe6; "
                   "slot id -> record at 0xe6 + word; empty when word == next word (PHANT.PRG 0x1159e) [C]",
        "areas": areas,
    })
    print("wrote", rel(p1))
    print("wrote", rel(p2))
    for i in issues:
        print("note:", i["level"], "references continuation records:", i["references_to_continuation_records"]
              if "references_to_continuation_records" in i else i["references_to_continuations"])


if __name__ == "__main__":
    main()
