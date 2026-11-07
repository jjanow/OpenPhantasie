#!/usr/bin/env python3
"""Phantasie III (DOS SSI 1988) armory stock per town -> reverse-engineering/dos/phantasie3/extracted/towns.json.

Sources (`reverse-engineering/dos/phantasie3/FORMAT.md` §4.1, §9.3; `logic-specs/town.md`):
- `INITARMS.DAT` (3328 B; = Amiga `inititem.dat`): 18 rows x 181 B (town 0..17), then 70 pad bytes [C]. Byte 0 = the
  number of item kinds stocked (copied to FAR:0xe810), bytes 1..180 = stock of item id 1..180 [C].
- Price index DS:0x10fc + 2 * town (u16, 18 entries) [C] bytes, extent [~].
- Town names: master string 111 + town (1..17; 15..17 = Light, Dark, Death) [C].

Run: python3 -I tools/scripts/extract_dos_p3_towns.py
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import extract_dos_p3_common as c  # noqa: E402
import extract_dos_p3_tables as t  # noqa: E402

ROWS, ROW = 18, 181


def main() -> None:
    img, _, _ = c.unpack_root()
    strs = t.string_table(img)
    raw = c.read_file("INITARMS.DAT")
    assert len(raw) == 3328 and set(raw[ROWS * ROW:]) == {0x3A}, "INITARMS layout changed"
    towns = []
    for n in range(ROWS):
        row = raw[n * ROW:(n + 1) * ROW]
        stock = [0] + list(row[1:])
        kinds = row[0]
        assert kinds == sum(1 for v in stock if v), f"town {n}: kinds byte {kinds} != stocked ids"
        towns.append({
            "town": n, "name": strs[111 + n] if n else None,
            "price_index": struct.unpack_from("<H", img, c.DS_IMG + t.TOWN_PRICE + 2 * n)[0],
            "kinds": kinds, "stock": stock,
            "stocked": [{"id": i, "name": strs[t.ITEM_BASE + i], "count": v} for i, v in enumerate(stock) if v],
        })
    c.write_json("towns.json", {
        "_source": "INITARMS.DAT rows 0..17 x 181 B [C] (FORMAT.md section 9.3); price index DS:0x10fc + 2 * town [C] "
                   "bytes (extent [~]); names = master string 111 + town [C]",
        "_layout": "stock[id] for item id 0..180 (stock[0] is always 0; the file's byte 0 is 'kinds', the number of "
                   "ids with a non-zero count, checked here). Row 0 is unused; 15..17 (the plane towns) stock nothing. "
                   "Counts of 200 / 99 / 77 on potions and scrolls = plenty. Restocking between visits [?].",
        "towns": towns,
    })
    print("towns", len(towns), "stocked rows", sum(1 for x in towns if x["kinds"]))


if __name__ == "__main__":
    main()
