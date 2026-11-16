#!/usr/bin/env python3
"""Phantasie I: the Apple II import's tables against DOS I's, cell by cell, and `GameData/phantasie1/apple2-import.json`.

The Apple-only I import (`p1-apple2`, `OpenPhantasie.Importer.Pipelines.P1Apple2.P1Apple2Tables`) puts each Apple table
into DOS I's layout and runs DOS's builders, keeping the Apple's text (names, messages, scroll and region pages) and
taking DOS I's value wherever an Apple number differs (DOS wins on numbers, AGENTS.md; as the C64 and Amiga imports).
This script writes the shipped remaster data the importer applies:

- `dos_cells` (CellPatches): every number / structure where the Apple build differs from the DOS-built GameData of the
  private repo, from the raw build (`--built DIR`: the eleven files the importer's test helper writes with
  `OPENPHANTASIE_DUMP_P1_APPLE2=DIR`, cells not applied);
- `sprite_sizes`: DOS's 80 monster sprite (width, height) pairs (the Apple has none);
- `spell_names`: the manual's spell table (the Apple has no spell names; as the C64, DIFFERENCES.md D-087);
- `name_fixes`: the fire elemental's cut-off name (as D-083 / D-087);
- `scroll_compass`: the compass word map the importer turns the Apple's region texts and scrolls with (the Apple draws the
  wilderness turned a quarter, FORMAT.md 3.6; as II's Apple import, DIFFERENCES-II D2-046).

Without `--built` the file is rewritten with its cells kept (or none, the first time). Formats:
`reverse-engineering/apple2/phantasie1/FORMAT.md`.

Usage: python3 -I tools/scripts/diff_a2_p1_vs_dos.py [--built DIR] [--all] [--check]
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
GD = REPO / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie1"
OUT = GD / "apple2-import.json"
TABLES = ["monsters.json", "items.json", "names.json", "towns.json", "elementals.json", "scrolls.json",
          "messages.json", "overworld.json", "dungeons.json", "item-descriptions.json", "spells.json"]

# Text the import keeps as the Apple's: never a cell unless the Apple has none there (null / ""). Files that are text
# through and through (messages.json: the Apple's message slots; scrolls.json) give no cells.
TEXT_FILES = {"messages.json", "scrolls.json"}
SKIP = re.compile(
    r"^\$\.(_source|_note|_format|_cipher|_layout|_fields|_formulas|_notes|_messages_note|_grid_note)(\..*)?$"
    r"|\.(name_ptr_ds|name_ptr_a2|offset|embedded_text|text_pages|messages|options|lines|text|raw)(\[|\.|$)"
    r"|^\$\.levels\.DNG\d+\.size$|^\$\.towns\[\d+\]\.dos_record$")

# Byte cells in hex strings: monster records from 0x14 (0..0x13 = the name), dungeon levels as far as both go, town
# records before the name (279).
HEX_FROM_TO = {"monsters.json": (0x14, None), "dungeons.json": (0, None), "towns.json": (0, 279)}

WHY = [
    (r"^monsters\.json .*\.(raw_hex|byte_34)", "monster record bytes (FORMAT.md 3.1)"),
    (r"^monsters\.json", "monster numbers (FORMAT.md 3.1)"),
    (r"^items\.json", "item numbers"),
    (r"^names\.json", "names the Apple does not hold"),
    (r"^towns\.json", "town stock / numbers (FORMAT.md 3.5)"),
    (r"^elementals\.json", "elementals (FORMAT.md 3.4)"),
    (r"^overworld\.json .*\.tiles", "region cells (FORMAT.md 3.6)"),
    (r"^overworld\.json", "region feature records (FORMAT.md 3.6)"),
    (r"^dungeons\.json", "dungeon level bytes (FORMAT.md 3.7)"),
    (r"^item-descriptions\.json", "item descriptions (derived numbers)"),
    (r"^spells\.json", "spells"),
]

# The manual's spell table (Phantasie_I_1986_SSI_manual_text.pdf page 18), as tools/scripts/build_c64_p1_import.py
# lists it (D-087): spell n = slot n - 1; slot 57, the divine spell, the remaster's wording.
C64_IMPORT = GD / "c64-import.json"

# Apple north = DOS east (FORMAT.md 3.6); the compounds turn whole (the Apple's NORTHWEST part of Gelnor = DOS's NORTHEAST).
COMPASS = {"NORTHWEST": "NORTHEAST", "NORTHEAST": "SOUTHEAST", "SOUTHEAST": "SOUTHWEST", "SOUTHWEST": "NORTHWEST",
           "NORTH": "EAST", "EAST": "SOUTH", "SOUTH": "WEST", "WEST": "NORTH"}


def show(path) -> str:
    return "$" + "".join(f"[{p}]" if isinstance(p, int) else f".{p}" for p in path)


def walk(a, b, path, out):
    if isinstance(a, dict) and isinstance(b, dict):
        for k in list(a) + [k for k in b if k not in a]:
            if k not in a or k not in b:
                out.append((path + [k], a.get(k), b.get(k)))
            else:
                walk(a[k], b[k], path + [k], out)
    elif isinstance(a, list) and isinstance(b, list):
        if len(a) != len(b):
            out.append((path + ["length"], len(a), len(b)))
        for i in range(min(len(a), len(b))):
            walk(a[i], b[i], path + [i], out)
    elif a != b:
        out.append((path, a, b))


def is_fog(t) -> bool:
    return isinstance(t, int) and 120 <= t < 255


def cells_for(f: str, d, a) -> list[dict]:
    out: list[dict] = []
    if f in TEXT_FILES:
        return out

    def rec(dv, av, path):
        p = show(path)
        if SKIP.search(p):
            if path and path[-1] == "dos_record" and isinstance(dv, str) and isinstance(av, str):
                hexcells(dv, av, path)
            return
        if isinstance(dv, dict) and isinstance(av, dict):
            for k in dv:
                if k in av:
                    rec(dv[k], av[k], path + [k])
            return  # keys only one side has: provenance / the Apple's own pointer keys
        if isinstance(dv, list) and isinstance(av, list):
            if len(dv) != len(av):
                out.append({"path": path, "apple": av, "dos": dv})
                return
            for i in range(len(dv)):
                rec(dv[i], av[i], path + [i])
            return
        if dv == av:
            return
        if path and path[-1] == "raw_hex" and isinstance(dv, str) and isinstance(av, str):
            hexcells(dv, av, path)
            return
        if isinstance(dv, str) and isinstance(av, str) and av != "":
            return  # the Apple's own text
        if len(path) >= 2 and path[-3:-1] and "tiles" in path and isinstance(dv, int) and isinstance(av, int):
            du, au = (dv - 120 if is_fog(dv) else dv), (av - 120 if is_fog(av) else av)
            if du != au:
                out.append({"path": path, "fog": True, "apple": au, "dos": du})
            elif dv != av:
                out.append({"path": path, "apple": av, "dos": dv})  # fog only (the start state differs)
            return
        out.append({"path": path, "apple": av, "dos": dv})

    def hexcells(dv: str, av: str, path):
        lo, hi = HEX_FROM_TO.get(f, (0, None))
        db, ab = bytes.fromhex(dv), bytes.fromhex(av)
        end = min(len(db), len(ab), hi if hi is not None else 1 << 30)
        for i in range(lo, end):
            if db[i] != ab[i]:
                out.append({"path": path, "byte": i, "apple": ab[i], "dos": db[i]})

    rec(d, a, [])
    rows = []
    for c in out:
        key = f"{f} {show(c['path'])}"
        why = next(w for pat, w in WHY if re.search(pat, key))
        row = {"why": why, "file": f, "path": c["path"]}
        for k in ("byte", "fog"):
            if k in c:
                row[k] = c[k]
        row["apple"] = c["apple"]
        row["dos"] = c["dos"]
        rows.append(row)
    return rows


def summarize(name: str, diffs, show_all: bool) -> None:
    groups = defaultdict(list)
    for path, d, a in diffs:
        groups[re.sub(r"\[\d+\]", "[]", show(path))].append((path, d, a))
    print(f"== {name}: {len(diffs)} differing leaves")
    for key, rows in sorted(groups.items(), key=lambda kv: -len(kv[1])):
        print(f"  {key}: {len(rows)}")
        for path, d, a in (rows if show_all else rows[:3]):
            print(f"    {show(path)}: DOS {json.dumps(d, ensure_ascii=False)[:70]} | Apple {json.dumps(a, ensure_ascii=False)[:70]}")


def constants() -> dict:
    c64 = json.loads(C64_IMPORT.read_text(encoding="utf-8"))
    monsters = json.loads((GD / "monsters.json").read_text(encoding="utf-8"))["monsters"]
    sprite_sizes = [[m["sprite_width"], m["sprite_height"]] for m in monsters]
    assert sprite_sizes == c64["sprite_sizes"]
    name_fixes = [
        {"why": "the fire elemental's name, M cut off (CHAR record 37; as D-083 / D-087)", "file": "elementals.json",
         "path": ["elementals", 0, "name"], "apple": "FIRE ELE", "fixed": "FIRE ELEM"},
        {"why": "the same name in the town labels", "file": "names.json", "path": ["town_labels", 12, "name"],
         "apple": "FIRE ELE", "fixed": "FIRE ELEM"},
    ]
    return {"sprite_sizes": sprite_sizes, "spell_names": c64["spell_names"], "name_fixes": name_fixes}


def document(cells: list[dict]) -> str:
    k = constants()
    doc = {
        "_schema": "openphantasie.p1-apple2-import/v1",
        "_source": "Remaster data for a Phantasie I import from the Apple II copy alone (profile p1-apple2), applied by the "
                   "importer (OpenPhantasie.Importer.Pipelines.P1Apple2.P1Apple2Tables). Made by "
                   "tools/scripts/diff_a2_p1_vs_dos.py --built from the importer's raw Apple build and DOS I's data.",
        "_note": "Between versions DOS wins on numbers (AGENTS.md): an Apple-only import takes the Apple's text and names, "
                 "and DOS I's value in every cell where the Apple's number differs (reverse-engineering/apple2/phantasie1/"
                 "FORMAT.md section 5).",
        "_dos_cells": "CellPatches, as c64-import.json's: a path into the GameData file the Apple import builds (DOS shape); "
                      "byte = an offset into a hex string; fog = a map tile whose fog (tile + 120) is kept. A cell holding "
                      "the Apple value takes the DOS value; one holding neither is kept and warned.",
        "dos_cells": cells,
        "_name_fixes": "As dos_cells, but the Apple's name and the fixed one (CHAR's 12-byte field reads FIRE ELE, as the "
                       "C64's; D-087).",
        "name_fixes": k["name_fixes"],
        "_sprite_sizes": "DOS's monster sprite (width, height) per monster (PHANTASI.COM DS:0x2c3e): the encounter build's "
                         "group fit reads the width. The Apple has no such table (it draws shapes at a SCALE).",
        "sprite_sizes": k["sprite_sizes"],
        "_scroll_compass": "The Apple draws the wilderness turned a quarter (its north = DOS's east) and its region texts "
                           "and scrolls follow its own compass. Each word on the left, at the start of a word (SOUTH-, a "
                           "compound before its first half), becomes the one on the right, all at once; a scroll box's closing ! keeps its "
                           "column (as II's Apple import, DIFFERENCES-II D2-046).",
        "scroll_compass": COMPASS,
        "_spell_names": "The Apple has no spell names (spells are cast by number, FORMAT.md 3.3). Slots 0-53: the manual's "
                        "spell table as it prints the names; slot 57 (the divine spell): not in the manual, the remaster's "
                        "wording [~] (as the C64's, D-087).",
        "spell_names": k["spell_names"],
    }
    head = json.dumps({key: v for key, v in doc.items() if key != "dos_cells"}, indent=1, ensure_ascii=False)
    # dos_cells one per line, after _dos_cells
    parts = head.split('\n "_name_fixes"')
    body = ',\n "dos_cells": [\n' + ",\n".join("  " + json.dumps(c, ensure_ascii=False) for c in cells) + "\n ]"
    text = parts[0].rstrip(",") + body + ',\n "_name_fixes"' + parts[1] + "\n"
    assert json.loads(text) == doc
    return text


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--built", type=Path)
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args(argv[1:])
    cells: list[dict] = []
    if args.built:
        for f in TABLES:
            a = json.loads((args.built / f).read_text(encoding="utf-8"))
            d = json.loads((GD / f).read_text(encoding="utf-8"))
            diffs = []
            walk(d, a, [], diffs)
            diffs = [x for x in diffs if not SKIP.search(show(x[0]))]
            summarize(f, diffs, args.all)
            cells += cells_for(f, d, a)
        print("cells by group:")
        for why, n in Counter(c["why"] for c in cells).most_common():
            print(f"  {n:5d}  {why}")
    elif OUT.exists():
        cells = json.loads(OUT.read_text(encoding="utf-8"))["dos_cells"]
    text = document(cells)
    if args.check:
        same = OUT.exists() and OUT.read_text(encoding="utf-8") == text
        print(f"{OUT.name} " + ("up to date" if same else "DIFFERS"))
        return 0 if same else 1
    OUT.write_text(text, encoding="utf-8")
    print(f"wrote {OUT.relative_to(REPO)}: {len(cells)} cells")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
