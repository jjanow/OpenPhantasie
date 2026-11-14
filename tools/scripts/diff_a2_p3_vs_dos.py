#!/usr/bin/env python3
"""Phantasie III: the Apple II tables against DOS III's, cell by cell (s212).

Two modes:

* extract (default): `reverse-engineering/apple2/phantasie3/extracted/` (extract_a2_p3_tables.py) against
  `reverse-engineering/dos/phantasie3/extracted/` (DOS III's extract, the input of the GameData build), table by table,
  with a summary per group (numbers, text, what the Apple does not hold).
* built: `--built DIR` compares the GameData files an Apple-only import built (the importer's P3Apple2Tables raw build,
  written by its test helper or an import's staging folder) with `src/OpenPhantasie.Data/GameData/phantasie3/`, as the
  importer's tests do (provenance keys ignored). `--cells OUT` writes those differences as `dos_cells` rows (the
  shipped `phantasie3/apple2-import.json` is made from them; see FORMAT.md 'Apple != DOS').

Usage: python3 -I tools/scripts/diff_a2_p3_vs_dos.py [--apple DIR] [--dos DIR] [--all]
       python3 -I tools/scripts/diff_a2_p3_vs_dos.py --built DIR [--cells OUT.json]
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
APPLE = ROOT / "reverse-engineering/apple2/phantasie3/extracted"
DOS = ROOT / "reverse-engineering/dos/phantasie3/extracted"
GAMEDATA = ROOT / "src/OpenPhantasie.Data/GameData/phantasie3"
TABLES = ["monsters.json", "items.json", "spells.json", "names.json", "overworld.json", "dungeons.json",
          "scrolls.json", "towns.json", "elementals.json", "rule-tables.json"]
EXTRACTS = ["monsters.json", "items.json", "spells.json", "names.json", "world.json", "dungeons.json", "scrolls.json",
            "towns.json", "rule-tables.json"]
PROVENANCE = re.compile(r"^\$\.(_source|_note|_notes|_layout)(\[\d+\])?$|\._source$|\._note$|\._address$|\.source$")
SKIP_EXTRACT = re.compile(r"\._source$|\._address$|\._note$|\._notes|\.raw_hex$|\._bases|\.code$|\._fields|_pad_hex")


def walk(a, b, path, out):
    """(path, dos value, apple value) of every differing leaf; whole values where a container is missing or the
    lengths differ (then the common items are walked too)."""
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


def show(path) -> str:
    return "$" + "".join(f"[{p}]" if isinstance(p, int) else f".{p}" for p in path)


def is_text(v) -> bool:
    return isinstance(v, str)


def summarize(name: str, diffs, show_all: bool) -> None:
    groups = defaultdict(list)
    for path, d, a in diffs:
        key = re.sub(r"\[\d+\]", "[]", show(path))
        groups[key].append((path, d, a))
    total = len(diffs)
    text = sum(1 for _, d, a in diffs if is_text(d) or is_text(a))
    missing = sum(1 for _, d, a in diffs if a is None and d is not None)
    print(f"== {name}: {total} differing cells ({text} text, {missing} the Apple does not hold)")
    for key, rows in sorted(groups.items(), key=lambda kv: -len(kv[1])):
        sample = rows if show_all else rows[:4]
        print(f"  {key}: {len(rows)}")
        for path, d, a in sample:
            ds, as_ = json.dumps(d, ensure_ascii=False), json.dumps(a, ensure_ascii=False)
            print(f"    {show(path)}: DOS {ds[:70]} | Apple {as_[:70]}")


def extract_mode(apple: Path, dos: Path, show_all: bool) -> None:
    for f in EXTRACTS:
        a = json.loads((apple / f).read_text(encoding="utf-8"))
        d = json.loads((dos / f).read_text(encoding="utf-8"))
        diffs = []
        walk(d, a, [], diffs)
        diffs = [x for x in diffs if not SKIP_EXTRACT.search(show(x[0]))]
        summarize(f, diffs, show_all)


# Text the Apple-only import keeps as the Apple's (its own wording): a string cell on these paths is not patched unless
# the Apple has none there (null). The same list is in the importer's P3Apple2ImportTests.
TEXT = re.compile(r"\.name$|\.names\[\d+\]$|^\$\.levels\.DNG\d+\.(messages\.|size$)|^\$\.scrolls\[\d+\]\.(text|lines)")
DNG_TEXT_FROM = 0x593  # dungeon records: bytes from the text pointers on are text (raw_hex byte cells stop here)

WHY = [  # (path pattern, group) for the cells' "why"
    (r"^monsters\.json \$\.monsters\[\d+\]\.byte_(27|33)", "monster bytes 27 / 33: no Apple field"),
    (r"^monsters\.json", "monster numbers"),
    (r"^names\.json \$\.(monsters|items|spells)", "names the Apple does not hold"),
    (r"^names\.json", "names the Apple does not hold"),
    (r"^spells\.json", "spell names: the Apple has none"),
    (r"^items\.json \$\.items\[0\]", "item 0: no Apple record"),
    (r"^items\.json", "item numbers"),
    (r"^overworld\.json \$\.regions\.PLANE0\.tiles", "Scandor grid: Apple tile bytes (10 x terrain + level), not DOS tile ids"),
    (r"^overworld\.json \$\.regions", "plane maps: the Apple's own (10 x 15 / 30 x 15) maps"),
    (r"^overworld\.json \$\.places", "places: squares and tiles"),
    (r"^overworld\.json \$\.terrain", "terrain of a tile"),
    (r"^dungeons\.json", "dungeon event numbers"),
    (r"^scrolls\.json", "scroll pages of an item"),
    (r"^towns\.json", "armory stock and price index"),
    (r"^elementals\.json", "elementals: none on the Apple"),
    (r"^rule-tables\.json \$\.skill", "skill tables: the Apple has Phantasie I's cells (D3-032)"),
    (r"^rule-tables\.json \$\.hp_dice", "HP dice: 15 levels on the Apple (DOS 22)"),
    (r"^rule-tables\.json \$\.dungeon_entries", "dungeon letters / entrances"),
    (r"^rule-tables\.json", "rule tables the Apple computes or lacks"),
]


def cells_for(f: str, d, a) -> list[dict]:
    """The shipped cells of one file: every differing number / structure (whole arrays where the lengths differ), the
    text the Apple lacks, and the dungeon records' bytes before the text area."""
    out: list[dict] = []

    def rec(dv, av, path):
        p = show(path)
        if PROVENANCE.search(p):
            return
        if isinstance(dv, dict) and isinstance(av, dict):
            if any(k not in av for k in dv) and ".messages" not in p:
                out.append({"path": path, "apple": av, "dos": dv})  # keys only DOS has: the whole object
                return
            for k in dv:
                if k in av:
                    rec(dv[k], av[k], path + [k])
            return  # keys only one side has: the Apple's extra text slots stay; DOS-only keys do not occur
        if isinstance(dv, list) and isinstance(av, list):
            if re.search(r"^\$\.regions\.PLANE\d\.tiles\[\d+\]$", p) and dv != av:
                out.append({"path": path, "apple": av, "dos": dv})  # map rows whole
                return
            if len(dv) != len(av):
                if not TEXT.search(p + ".x") and not re.search(r"\.lines$", p):
                    out.append({"path": path, "apple": av, "dos": dv})
                return
            for i in range(len(dv)):
                rec(dv[i], av[i], path + [i])
            return
        if dv == av:
            return
        if path and path[-1] == "raw_hex" and isinstance(dv, str) and isinstance(av, str):
            db, ab = bytes.fromhex(dv), bytes.fromhex(av)
            for i in range(min(DNG_TEXT_FROM, len(db), len(ab))):
                if db[i] != ab[i]:
                    out.append({"path": path, "byte": i, "apple": ab[i], "dos": db[i]})
            return
        if TEXT.search(p) and av is not None:
            return
        out.append({"path": path, "apple": av, "dos": dv})

    rec(d, a, [])
    for c in out:
        key = f"{f} {show(c['path'])}"
        c["why"] = next(w for pat, w in WHY if re.search(pat, key))
        c["file"] = f
    return [{"why": c["why"], "file": c["file"], "path": c["path"], **({"byte": c["byte"]} if "byte" in c else {}),
             "apple": c["apple"], "dos": c["dos"]} for c in out]


def built_mode(built: Path, cells_out: Path | None, show_all: bool) -> None:
    rows, cells = [], []
    for f in TABLES:
        a = json.loads((built / f).read_text(encoding="utf-8"))
        d = json.loads((GAMEDATA / f).read_text(encoding="utf-8"))
        diffs = []
        walk(d, a, [], diffs)
        diffs = [x for x in diffs if not PROVENANCE.search(show(x[0]))]
        summarize(f, diffs, show_all)
        rows += [(f, p, dv, av) for p, dv, av in diffs]
        cells += cells_for(f, d, a)
    print(Counter(f for f, *_ in rows))
    print("cells by group:")
    for why, n in Counter(c["why"] for c in cells).most_common():
        print(f"  {n:5d}  {why}")
    if cells_out:
        doc = {
            "_schema": "openphantasie.p3-apple2-import/v1",
            "_source": "Remaster data for a Phantasie III import from the Apple II copy alone (profile p3-apple2), applied by "
                       "the importer (OpenPhantasie.Importer.Pipelines.P3Apple2.P3Apple2Tables.Write) to the GameData "
                       "P3Apple2Tables builds: DOS III's value in every number / structure cell where the Apple's differs "
                       "or it has none (the rule of II's Apple and III's Amiga imports: the reference version's numbers), "
                       "so III plays the same whichever version was imported. The Apple's own text is kept. Made by "
                       "tools/scripts/diff_a2_p3_vs_dos.py --built ... --cells; the groups: "
                       "reverse-engineering/apple2/phantasie3/FORMAT.md 'Apple != DOS'.",
            "_dos_cells": "As phantasie3/amiga-import.json (CellPatches): a path into the GameData file the Apple import "
                          "builds; a string is an object key, a number an array index; 'byte' = an offset into a hex "
                          "string. A cell holding the Apple value takes the DOS value; one holding neither is kept and "
                          "warned. Whole arrays or objects where the shapes differ; map rows whole.",
            "dos_cells": cells,
        }
        text = json.dumps({k: v for k, v in doc.items() if k != "dos_cells"}, indent=1, ensure_ascii=False)[:-2]
        text += ',\n "dos_cells": [\n' + ",\n".join("  " + json.dumps(c, ensure_ascii=False) for c in cells) + "\n ]\n}\n"
        json.loads(text)
        cells_out.write_text(text, encoding="utf-8")
        print(f"wrote {len(cells)} cells to {cells_out}")


def main(argv: list[str]) -> None:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--apple", type=Path, default=APPLE)
    ap.add_argument("--dos", type=Path, default=DOS)
    ap.add_argument("--built", type=Path)
    ap.add_argument("--cells", type=Path)
    ap.add_argument("--all", action="store_true")
    args = ap.parse_args(argv[1:])
    if args.built:
        built_mode(args.built, args.cells, args.all)
    else:
        extract_mode(args.apple, args.dos, args.all)


if __name__ == "__main__":
    main(sys.argv)
