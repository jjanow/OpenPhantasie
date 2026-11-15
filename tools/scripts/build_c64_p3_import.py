#!/usr/bin/env python3
"""Build `GameData/phantasie3/c64-import.json`: DOS III's values for a Phantasie III import from the C64 copy alone (s218).

The C64 import (`p3-c64` / `p3-c64-1.2`, `OpenPhantasie.Importer.Pipelines.P3C64.P3C64Tables`) keeps the C64's text and
takes DOS III's value in every number / structure cell where the C64's differs or it has none (the rule of the Apple and
Amiga III imports; `reverse-engineering/c64/phantasie3/FORMAT.md` section 7). As for the Apple
(`diff_a2_p3_vs_dos.py --built ... --cells`, whose comparison rules this reuses), the cells are made from the importer's
own raw build: run the importer test that dumps it, then this script.

    P3C64_DUMP=/tmp/p3c64-built dotnet test tests/OpenPhantasie.Importer.Tests --filter \
        "FullyQualifiedName~P3C64TablesTests.Build_GivesEveryTableDosIIIWrites"
    python3 -I tools/scripts/build_c64_p3_import.py --built /tmp/p3c64-built [--check]

`--check` compares the cells with the committed file instead of writing it. Needs the private repo's DOS-built
`GameData/phantasie3/` (the comparison's other side).
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import diff_a2_p3_vs_dos as a2  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
GAMEDATA = REPO / "src/OpenPhantasie.Data/GameData/phantasie3"
OUT = GAMEDATA / "c64-import.json"

WHY = [  # (path pattern, group) for the cells' "why" (FORMAT.md section 3 / 7)
    (r"^monsters\.json \$\.monsters\[\d+\]\.byte_27", "monster byte 27: the C64's own picture numbers"),
    (r"^monsters\.json", "monster numbers: the five Apple-shared records"),
    (r"^names\.json", "names the C64 does not hold"),
    (r"^spells\.json", "spells"),
    (r"^items\.json", "item numbers"),
    (r"^overworld\.json \$\.regions\.PLANE0\.tiles", "Scandor grid: the C64's own tile ids (no tile -> terrain table read)"),
    (r"^overworld\.json \$\.regions", "plane maps: the C64's layouts not decoded"),
    (r"^overworld\.json \$\.(places|start)", "places and start: in the C64's programs, not read"),
    (r"^overworld\.json \$\.terrain", "terrain of a tile: not read on the C64"),
    (r"^dungeons\.json", "dungeon records"),
    (r"^scrolls\.json", "scroll pages of an item"),
    (r"^towns\.json", "armory stock, byte 0 and price index"),
    (r"^elementals\.json", "elementals: the Air elemental's spells not read"),
    (r"^rule-tables\.json \$\.skill", "skill tables: the C64 has Phantasie I's cells (D3-032's default; DOS's as read + fixes)"),
    (r"^rule-tables\.json \$\.hp_dice", "HP dice: 15 levels on the C64 (DOS 22)"),
    (r"^rule-tables\.json \$\.dungeon_entries", "dungeon entrances: not read on the C64"),
    (r"^rule-tables\.json \$\.transfer_maps", "transfer maps: the C64's utility not read"),
    (r"^rule-tables\.json", "rule tables the C64 lacks"),
]


def cells(built: Path) -> list[dict]:
    a2.WHY = a2.WHY + [(r".", "")]  # the Apple's groups are replaced by WHY below
    out: list[dict] = []
    for f in a2.TABLES:
        c64 = json.loads((built / f).read_text(encoding="utf-8"))
        dos = json.loads((GAMEDATA / f).read_text(encoding="utf-8"))
        for c in a2.cells_for(f, dos, c64):
            key = f"{f} {a2.show(c['path'])}"
            row = {"why": next(w for pat, w in WHY if re.search(pat, key)), "file": f, "path": c["path"]}
            if "byte" in c:
                row["byte"] = c["byte"]
            row["c64"] = c["apple"]
            row["dos"] = c["dos"]
            out.append(row)
    return out


def document(rows: list[dict]) -> str:
    doc = {
        "_schema": "openphantasie.p3-c64-import/v1",
        "_source": "Remaster data for a Phantasie III import from the C64 copy alone (profiles p3-c64, p3-c64-1.2), applied "
                   "by the importer (OpenPhantasie.Importer.Pipelines.P3C64.P3C64Tables.Write) to the GameData P3C64Tables "
                   "builds: DOS III's value in every number / structure cell where the C64's differs or it has none (the "
                   "rule of the Apple and Amiga III imports: the reference version's numbers), so III plays the same "
                   "whichever version was imported. The C64's own text is kept. Made by tools/scripts/build_c64_p3_import.py "
                   "from the importer's raw build; the groups: reverse-engineering/c64/phantasie3/FORMAT.md sections 3 and 7.",
        "_dos_cells": "As phantasie3/apple2-import.json (CellPatches): a path into the GameData file the C64 import builds; "
                      "a string is an object key, a number an array index; 'byte' = an offset into a hex string. A cell "
                      "holding the C64 value takes the DOS value; one holding neither is kept and warned. Whole arrays or "
                      "objects where the shapes differ; map rows whole.",
    }
    text = json.dumps(doc, indent=1, ensure_ascii=False)[:-2]
    text += ',\n "dos_cells": [\n' + ",\n".join("  " + json.dumps(c, ensure_ascii=False) for c in rows) + "\n ]\n}\n"
    json.loads(text)
    return text


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--built", type=Path, required=True, help="the importer's raw build (P3C64_DUMP)")
    ap.add_argument("--check", action="store_true", help="compare with the committed file instead of writing it")
    args = ap.parse_args(argv[1:])
    rows = cells(args.built)
    for why, n in Counter(c["why"] for c in rows).most_common():
        print(f"  {n:5d}  {why}")
    text = document(rows)
    if args.check:
        same = OUT.exists() and OUT.read_text(encoding="utf-8") == text
        print(f"{OUT.relative_to(REPO)}: {'up to date' if same else 'DIFFERS'} ({len(rows)} cells)")
        return 0 if same else 1
    OUT.write_text(text, encoding="utf-8")
    print(f"wrote {len(rows)} cells to {OUT.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
