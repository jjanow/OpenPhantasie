#!/usr/bin/env python3
"""Phantasie III (DOS SSI 1988) scroll texts -> reverse-engineering/dos/phantasie3/extracted/scrolls.json.

Sources (`reverse-engineering/dos/phantasie3/FORMAT.md` §9.4, `logic-specs/planes.md` §7):
- `SCROLLS.DAT`: 19 records x 1050 B of NUL-terminated text, record k at k * 0x41a (root `0xeb`) [C].
- Lines: the text has no line breaks; the window cuts it. A 38-character cut never splits a word in any of the 19
  records (every other width from 30 to 44 does) [~], so `lines` are 38-character slices, right-trimmed.
- Item -> records (`read_scroll`, root `0xeb`) [C] (planes.md §7): record = item - 121, minus one more above item 126;
  item 123 -> 4; items 122 / 124 / 129 show three consecutive records; 126 (Scroll IV) is the map picture, no text.

Run: python3 -I tools/scripts/extract_dos_p3_scrolls.py
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import extract_dos_p3_common as c  # noqa: E402
import extract_dos_p3_tables as t  # noqa: E402

RECORDS, SIZE, WIDTH = 19, 0x41A, 38


def item_records(item: int) -> list[int]:
    """planes.md §7 [C]."""
    if item == 126:
        return []
    if item == 123:
        return [4]
    first = item - 121 - (1 if item > 126 else 0)
    return [first, first + 1, first + 2] if item in (122, 124, 129) else [first]


def main() -> None:
    img, _, _ = c.unpack_root()
    strs = t.string_table(img)
    raw = c.read_file("SCROLLS.DAT")
    assert len(raw) == RECORDS * SIZE
    recs = []
    for k in range(RECORDS):
        text = raw[k * SIZE:(k + 1) * SIZE].split(b"\0")[0].decode("latin-1")
        lines = [text[i:i + WIDTH].rstrip() for i in range(0, len(text), WIDTH)]
        recs.append({"record": k, "length": len(text), "text": text, "lines": lines})
    items = [{"item": i, "name": strs[t.ITEM_BASE + i], "records": item_records(i)} for i in range(121, 141)]
    assert all(0 <= r < RECORDS for it in items for r in it["records"])
    c.write_json("scrolls.json", {
        "_source": "SCROLLS.DAT 19 x 1050 B NUL-terminated [C] (FORMAT.md section 9.4); item -> records = read_scroll "
                   "root 0xeb [C] (planes.md section 7); 38-character lines [~]",
        "_notes": ["Item 126 (Scroll IV) shows the map picture (SHAPES.CGA; caption: string useitem.scroll_picture_caption), not text.",
                   "Items 122 / 124 / 129 show three consecutive records (pages). Item 130 shows record 8, whose "
                   "text is cut at byte 869 (planes.md section 7); the duplicate III / VII items are partial copies."],
        "records": recs,
        "items": items,
    })
    print("records", len(recs), "items", len(items))


if __name__ == "__main__":
    main()
