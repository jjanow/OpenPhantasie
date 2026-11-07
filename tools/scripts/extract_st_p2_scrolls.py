#!/usr/bin/env python3
"""Phantasie II (Atari ST): SCROLLS.DTX -> extracted/scrolls/scrolls.json (task 3b.8).

PHANT.PRG 0x13608(buf, n) opens scrolls.dtx, seeks n * 840 and reads 840 B [C]; 0x292a(first, extra) shows records
first .. first + extra, 21 lines x 40 chars each (NUL-padded) [C]. Plain text (Phantasie I's DOS file is
enciphered with 800-B records; the Amiga I file is plain 20 x 800).
16384 B = 19 full records + 424 B (Phantasie I "ENDING SEQUENCE" leftover).

Item -> record (0xe1e8, items 121..140 = the 20 scroll slots of the item table) [C]:
    i = item id; if i > 137: i -= 1; first = i - 121; extra = 2 for 121, 1 for 132, 2 for 134, else 0.
Run from the repo root: python3 -I tools/scripts/extract_st_p2_scrolls.py
"""
from __future__ import annotations

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_st_p2_common import EXTRACTED, d2, rel, write_json  # noqa: E402

REC, LINE, LINES = 840, 40, 21
FIRST_ITEM, LAST_ITEM = 121, 140
EXTRA = {121: 2, 132: 1, 134: 2}


def item_records(item: int) -> list[int]:
    i = item - 1 if item > 137 else item
    first = i - FIRST_ITEM
    return list(range(first, first + EXTRA.get(item, 0) + 1))


def main() -> None:
    data = d2("SCROLLS.DTX")
    names = json.loads((EXTRACTED / "names.json").read_text())["items"]
    name_of = {}
    for it in names:
        if isinstance(it, dict):
            name_of[it.get("index")] = it.get("name")
    full = len(data) // REC
    records = []
    for n in range(full + (1 if len(data) % REC else 0)):
        chunk = data[n * REC:(n + 1) * REC]
        lines = [chunk[k * LINE:(k + 1) * LINE].split(b"\0")[0].decode("latin-1").rstrip()
                 for k in range(LINES) if k * LINE < len(chunk)]
        records.append({"record": n, "size": len(chunk), "lines": lines,
                        "title": next((ln.strip() for ln in lines if ln.strip()), "")})
    items = []
    for item in range(FIRST_ITEM, LAST_ITEM + 1):
        recs = item_records(item)
        items.append({"item_id": item, "item_name": name_of.get(item), "records": recs,
                      "title": records[recs[0]]["title"] if recs[0] < full else None})
    lettered = [i for i in items if i["item_name"] and i["item_name"].strip() != "SCROLL"]
    scrolls = []
    for k, it in enumerate(lettered):
        text_lines = [ln for r in it["records"] for ln in records[r]["lines"]]
        scrolls.append({"id": k + 1, "item_id": it["item_id"], "name": it["item_name"], "records": it["records"],
                        "lines": text_lines, "text": "\n".join(ln for ln in text_lines if ln.strip())})
    used = sorted({r for it in lettered for r in it["records"]})
    p = write_json("scrolls", "scrolls.json", {
        "_source": "Phantasie II ST disk2/SCROLLS.DTX (16384 B, plain text)",
        "_layout": "840-B records = 21 lines x 40 chars (PHANT.PRG 0x13608 / 0x292a) [C]",
        "_item_map": "PHANT.PRG 0xe1e8: item i in 121..140 -> first record (i > 137 ? i - 1 : i) - 121; "
                     "SCROLL A (121) shows 3 records, the 132 and 134 scrolls 2 and 3 [C]",
        "lettered_scroll_count": len(lettered),
        "records_used_by_lettered_scrolls": used,
        "records_not_used": [r["record"] for r in records if r["record"] not in used],
        "scrolls": scrolls,
        "items": items,
        "records": records,
    })
    print("wrote", rel(p), f"({len(lettered)} lettered scrolls, {full} full records)")


if __name__ == "__main__":
    main()
