#!/usr/bin/env python3
"""Extract the fixed name/lookup tables baked into PHANTASI.COM + TWNS.INT.

PHANTASI.COM is EXEPACK-packed (s22), so every table is read from the unpacked
program image through the game's own DS pointer tables (``re_common.com_ds``).
Each ``offset`` in the output is a DS offset. The string tables happen to sit in
long literal runs, so the names are byte-identical to the old raw-file read
(H5, s24); the old monster "raw stat bytes" were packed tokens and are gone --
monster records live in ``monsters.json`` (``extract_monsters.py``).

Output: reverse-engineering/dos/extracted/names.json
"""
from __future__ import annotations

import re
import struct

from re_common import com_ds, read, unpack_phantasi_com, write_json

# --- DS pointer tables in the unpacked image (H5, s24) -----------------------
CLASS_PTRS = (0x167E, 6)        # sheet class name table (COM 0x2ef4 renderer)
RACE_PTRS = (0x16B2, 15)        # 16 slots in DOS; slot 15 = UNDEAD (race only, not playable)
SPELL_PTRS = (0x174D, 60)       # slots 0..53 + 57 (DIVINE I) used, 54..56 / 58..59 empty
ITEM_PTRS = (0x0EC0, 181)       # item ids 0..180
EQUIPMENT_COUNT = 101           # names.json "items" = ids 0..100 (equipment only)
POTION_IDS = range(101, 111)    # HEALING 1..HEALING10 -- the old "spell_level_names"
DIVINE_SLOT = 57

MONSTER_TABLE_DS, MONSTER_STRIDE, MONSTER_COUNT = 0x1A6C, 0x39, 80


def pointers(image: bytes, table) -> list[int]:
    ds, n = table
    return list(struct.unpack_from(f"<{n}H", com_ds(image, ds, 2 * n)))


def cstring(image: bytes, ds: int) -> str:
    return com_ds(image, ds, 0x40).split(b"\0", 1)[0].decode("latin1")


def named(image: bytes, ptrs: list[int], label: str):
    items = [{"index": i, "offset": f"0x{p:04x}", "name": cstring(image, p)}
             for i, p in enumerate(ptrs)]
    assert all(it["name"] for it in items), f"{label}: empty name"
    print(f"  {label}: {len(items)} entries")
    return items


def spells(image: bytes):
    ptrs = pointers(image, SPELL_PTRS)
    used = list(range(54)) + [DIVINE_SLOT]
    assert all(not cstring(image, ptrs[i]) for i in range(60) if i not in used), "spell slot layout moved"
    # Remaster index 54 = DOS slot 57 (spells.json uses 0..54; see PLAN.md P0.6 DIVINE slot item).
    out = named(image, [ptrs[i] for i in used], "spells")
    out[-1]["dos_slot"] = DIVINE_SLOT
    return out


def monsters(image: bytes):
    table = com_ds(image, MONSTER_TABLE_DS, MONSTER_STRIDE * MONSTER_COUNT)
    recs = []
    for i in range(MONSTER_COUNT):
        rec = table[i * MONSTER_STRIDE:(i + 1) * MONSTER_STRIDE]
        recs.append({"index": i,
                     "offset": f"0x{MONSTER_TABLE_DS + i * MONSTER_STRIDE:04x}",
                     "name": rec[:0x14].split(b"\0", 1)[0].decode("latin1")})
    print(f"  monsters: {len(recs)} names (DS:0x{MONSTER_TABLE_DS:04x} x 0x{MONSTER_STRIDE:x})")
    return recs


def extract_towns():
    """Each town/interior map in TWNS.INT is preceded by a 10-char space-padded
    name header terminated with NUL. Find those isolated padded headers."""
    data = read("TWNS.INT")
    names = []
    for m in re.finditer(rb"[A-Z][A-Z0-9 .]{2,}\x00", data):
        off = m.start()
        # header must sit at a name-field boundary (preceded by NUL/padding)
        if off > 0 and data[off - 1] not in (0x00, 0x20):
            continue
        s = m.group()[:-1].decode("latin1").strip()
        if s:
            names.append({"offset": f"0x{off:04x}", "name": s})
    print(f"  towns/interiors (TWNS.INT): {len(names)} labels")
    return names


def main():
    image = unpack_phantasi_com()
    items = pointers(image, ITEM_PTRS)
    out = {
        "_source": "PHANTASI.COM unpacked (EXEPACK) via DS pointer tables + TWNS.INT",
        "_note": "offset = DS offset (TWNS.INT labels: file offset). spell_level_names are really "
                 "the potion item names, ids 101..110 (only referenced from the item pointer table). "
                 "Monster stats: monsters.json.",
        "classes": named(image, pointers(image, CLASS_PTRS), "classes"),
        "races": named(image, pointers(image, RACE_PTRS), "races"),
        "spells": spells(image),
        "spell_level_names": named(image, [items[i] for i in POTION_IDS], "spell_level_names"),
        "items": named(image, items[:EQUIPMENT_COUNT], "items"),
        "monsters": monsters(image),
        "town_labels": extract_towns(),
    }
    p = write_json("names.json", out)
    print(f"wrote {p}")


if __name__ == "__main__":
    main()
