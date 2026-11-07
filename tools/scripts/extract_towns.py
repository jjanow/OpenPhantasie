#!/usr/bin/env python3
"""Extract mutable town state from TWNS.DAT (Armory stock + price index).

Layout (save-file-format.md): 12 towns × 291-byte records.
  0..180   u8 stock quantity by item id
  0xb5     u16[45] restock list: OV1 0x8e1 (town entry) sets stock[list[i]] = 1 for 4 random i <= avg party level
  0x10f    u16 town_price_index (buy formula)
  279..288 10-char space-padded name
  dos_record = the full record (hex), carried for DOS export (P1.4)

Output: reverse-engineering/dos/extracted/towns.json
(also copy into GameData/phantasie1 when shipping).
"""
from __future__ import annotations

import struct

from re_common import read, write_json

TWNS = "TWNS.DAT"
STRIDE = 291
TOWN_COUNT = 12
STOCK_SLOTS = 181
PRICE_OFF = 0x10F
RESTOCK_OFF = 0xB5  # 45 u16 item ids, OV1 0x8e1 (town-menus.md §Armory restock)
RESTOCK_COUNT = 45
NAME_OFF = 279


def main() -> None:
    data = read(TWNS)
    if len(data) < STRIDE * TOWN_COUNT:
        raise SystemExit(f"{TWNS}: expected {STRIDE * TOWN_COUNT} bytes, got {len(data)}")

    towns = []
    for index in range(TOWN_COUNT):
        off = index * STRIDE
        rec = data[off : off + STRIDE]
        name = rec[NAME_OFF : NAME_OFF + 10].split(b"\0")[0].decode("ascii", errors="replace").strip()
        price = struct.unpack_from("<H", rec, PRICE_OFF)[0]
        stock = list(rec[:STOCK_SLOTS])
        restock = list(struct.unpack_from(f"<{RESTOCK_COUNT}H", rec, RESTOCK_OFF))
        towns.append(
            {
                "id": index + 1,  # overworld feature arg is 1-based (Pelnor=1)
                "index": index,
                "name": name,
                "price_index": price,
                "stock": stock,
                "restock_items": restock,
                # P1.4: the whole 291-byte record, so a DOS export of a town the remaster built from this file keeps
                # the unmapped bytes (region word 0x111, 181..270, ...) instead of zeros.
                "dos_record": rec.hex(),
            }
        )

    write_json(
        "towns.json",
        {
            "_source": "TWNS.DAT (SSI 1987 IBM/DOS)",
            "_layout": {
                "record_stride": STRIDE,
                "town_count": TOWN_COUNT,
                "stock_slots": STOCK_SLOTS,
                "price_index_offset": hex(PRICE_OFF),
                "restock_items_offset": hex(RESTOCK_OFF),
                "name_offset": NAME_OFF,
            },
            "_formulas": {
                "buy_price": "floor(V*V * town_price_index / 500) + 1 (full charged price; OV1 FUN_0000_1d5b)",
                "sell_price": "V*V / 8",
                "restock": "town entry (OV1 0x8e1): 18x stock[rand/274] = 0 (ids 0..119), then 4x stock[restock_items[rand/(32767/avg_level)]] = 1 (index 0, no draw, when avg_level = 0)",
                "cha_discount": "none [C]: pass-2's full 177-GP shared-purse charge settled as 59 GP from each of 3 persisted @262 carried-gold shares",
            },
            "towns": towns,
        },
    )


if __name__ == "__main__":
    main()
