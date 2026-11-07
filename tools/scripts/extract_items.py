#!/usr/bin/env python3
"""Extract Phantasie I item tables from PHANTASI.COM.

Item *names* are a contiguous C-string block; combat effectiveness, min-STR,
and kind are **not** stored as parallel record fields — they are derived from
the item index by ``PHANTASI.COM`` ``FUN_0000_3053`` / ``OV1`` ``FUN_0000_17df``.

The only numeric array indexed by item id is the **value factor** ``V`` used
for Armory buy/sell prices (``DS:0xd54``).

Name pointers live at ``DS:0xec0`` (u16 LE, 181 entries: equipment 0..100 plus
potions/scrolls/treasure 101..180). Both are read from the unpacked EXEPACK
image (H5, s24); output is identical to the old raw-file read, which only worked
because these tables sit in one long literal run.

Output: reverse-engineering/dos/extracted/items.json
"""
from __future__ import annotations

import struct

from re_common import com_ds, unpack_phantasi_com, write_json

VALUE_TABLE_DS = 0x0D54   # u16 V[182]; last entry unused (name ptr 181 == 0)
NAME_PTRS_DS = 0x0EC0     # u16 name_ptr[181]
NAME_COUNT = 181          # ids 0..180


def kind_points_req(item: int) -> dict:
    """Mirror FUN_0000_3053 / FUN_0000_17df.

    Returns kind, combat point value, and the STR (or STR*2+DEX) gate used for
    the UNUSABLE check. Potions/scrolls/treasure fall through to kind=other.
    """
    if item < 0x15:  # 0..20 shields (incl. NOTHING/GLOVE)
        return {
            "kind": "shield",
            "points": item,
            "min_str": item,
            "str_check": "str",  # need STR >= min_str
        }
    if item < 0x1f:  # 21..30 body armor (plain)
        return {
            "kind": "armor",
            "points": item - 0x14,
            "min_str": item * 2 - 0x28,
            "str_check": "str",
        }
    if item < 0x29:  # 31..40 body armor (enchanted / god)
        return {
            "kind": "armor",
            "points": item - 0x1d,
            "min_str": item * 2 - 0x3e,
            "str_check": "str",
        }
    if item < 0x47:  # 41..70 weapons (non-magic)
        return {
            "kind": "weapon",
            "points": (item >> 1) - 0x14,
            "min_str": item * 2 - 0x50,  # compared against STR*2+DEX
            "str_check": "str2_plus_dex",
        }
    if item < 0x65:  # 71..100 weapons (magic / god)
        return {
            "kind": "weapon",
            "points": (item * 3 >> 2) - 0x32,
            "min_str": item * 2 - 0x8c,
            "str_check": "str2_plus_dex",
        }
    # 101+ : healing/magic potions, scrolls, quest/treasure names
    return {
        "kind": "other",
        "points": 0,
        "min_str": None,
        "str_check": None,
    }


def cstring_at(image: bytes, ds: int) -> str:
    return com_ds(image, ds, 0x40).split(b"\0", 1)[0].decode("latin1")


def main():
    image = unpack_phantasi_com()
    values = com_ds(image, VALUE_TABLE_DS, 2 * NAME_COUNT)
    ptrs = com_ds(image, NAME_PTRS_DS, 2 * NAME_COUNT)

    items = []
    for i in range(NAME_COUNT):
        name_ptr = struct.unpack_from("<H", ptrs, i * 2)[0]
        name = cstring_at(image, name_ptr)
        V = struct.unpack_from("<H", values, i * 2)[0]
        meta = kind_points_req(i)
        # Sell = V²/8; list buy uses town price index (TWNS.DAT town+0x10f):
        #   buy ≈ V² * town_price_index / 500 + 1  (OV1 FUN_0000_1d5b)
        rec = {
            "id": f"p1:{i}",  # game-prefixed id (Phase 09 T1.2)
            "index": i,
            "name": name,
            "name_ptr_ds": f"0x{name_ptr:04x}",
            "value_factor": V,
            "sell_price_v2_div_8": (V * V) // 8,
            **meta,
        }
        # Convenience: potion tier 1..10 from the name index
        if 101 <= i <= 110:
            rec["potion_tier"] = i - 100
            rec["heal_hp_n_squared"] = (i - 100) ** 2  # Schultz [X]
        elif 111 <= i <= 120:
            rec["potion_tier"] = i - 110
            rec["restore_magic_3n"] = 3 * (i - 110)  # Schultz [X]
        items.append(rec)

    out = {
        "_source": "PHANTASI.COM unpacked (EXEPACK), SSI 1987 IBM/DOS",
        "_note": (
            "Combat points / min-STR / kind are formula-derived from index "
            "(FUN_0000_3053), not a fixed-size stat record. value_factor V is "
            "the only parallel numeric table (DS:0xd54)."
        ),
        "_layout": {
            "value_table_ds": f"0x{VALUE_TABLE_DS:04x}",
            "name_ptrs_ds": f"0x{NAME_PTRS_DS:04x}",
            "equipment_count": 101,
            "extended_id_count": NAME_COUNT,
        },
        "_formulas": {
            "sell_price": "V*V / 8",
            "buy_price": "V*V * town_price_index / 500 + 1",
            "town_price_index": "TWNS.DAT town_record + 0x10f (u16 LE; Pelnor=150)",
            "town_record_stride": 291,
            "shield_points": "index",
            "armor_plain_points": "index - 20",
            "armor_magic_points": "index - 29",
            "weapon_plain_points": "(index >> 1) - 20",
            "weapon_magic_points": "(index * 3 >> 2) - 50",
            "usable_shield_armor": "STR >= min_str",
            "usable_weapon": "STR*2 + DEX >= min_str",
        },
        "items": items,
    }
    path = write_json("items.json", out)
    eq = sum(1 for it in items if it["kind"] != "other")
    print(f"  items: {len(items)} ids ({eq} equipment 0..100)")
    print(f"wrote {path}")


if __name__ == "__main__":
    main()
