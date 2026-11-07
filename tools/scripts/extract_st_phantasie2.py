#!/usr/bin/env python3
"""Extract Phantasie II (Atari ST) master tables from PHANT.PRG and compare them with Phantasie I.

Phase 09 T3. Input: reverse-engineering/atari-st/phantasie2/unpacked/disk1/PHANT.PRG (extracted from the
clean v1.0 Pasti set with tools/scripts/stx_to_st.py + mtools). Output (all in
reverse-engineering/atari-st/phantasie2/extracted/):

  names.json     classes, races, status, 60 spell slots, 181 item names + value factors
  monsters.json  80 monster records (58-byte big-endian records, the Amiga I layout)
  diff-vs-p1.json  by-index / by-name comparison against Phantasie I (DOS extracted tables)

Addresses are image (text-relative) offsets; data segment = image + text_size; file = image + 0x1c.
See reverse-engineering/atari-st/phantasie2/FORMAT.md.

Run: python3 -I tools/scripts/extract_st_phantasie2.py   (from the repo root)
"""
from __future__ import annotations

import json
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from st_prg import load_prg  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PRG = os.path.join(ROOT, "reverse-engineering/atari-st/phantasie2/unpacked/disk1/PHANT.PRG")
OUT = os.path.join(ROOT, "reverse-engineering/atari-st/phantasie2/extracted")
P1_MONSTERS = os.path.join(ROOT, "reverse-engineering/dos/extracted/monsters.json")
P1_NAMES = os.path.join(ROOT, "reverse-engineering/dos/extracted/names.json")
P1_ITEMS = os.path.join(ROOT, "reverse-engineering/dos/extracted/items.json")
AMIGA_GAME = os.path.join(ROOT, "reverse-engineering/amiga/unpacked/PHANT/game")
AMIGA_MONSTERS = 0xC88

# Data-segment offsets (data+X), all [C] by relocation-table pointer runs (FORMAT.md §PHANT.PRG).
ITEM_VALUES = 0x180  # u16 x 181, = DOS DS 0x0d54 table
ITEM_PTRS = 0x2EC  # 181 relocated longs -> item names
NAME_PTRS = 0x5C4  # 85 relocated longs: 6 classes, 16 races, 3 status, 60 spell slots
MONSTERS = 0x1C76  # 80 x 58-byte records
MONSTER_STRIDE = 58
MONSTER_COUNT = 80

# 58-byte record = DOS 57-byte record with one pad byte at +0x23 so the i16 fields stay even (68000).
# (offset, struct fmt, field name as in reverse-engineering/dos/logic-specs/data-tables.md)
MONSTER_FIELDS = [
    (0x14, ">h", "armor"),
    (0x16, ">h", "damage"),
    (0x18, ">h", "level"),
    (0x1A, ">I", "xp"),
    (0x1E, ">h", "special_chance"),
    (0x20, ">3B", "specials"),
    (0x23, ">B", "pad_23"),
    (0x24, ">h", "hp_base"),
    (0x26, ">h", "combat_rating"),
    (0x28, ">2B", "companions"),
    (0x2A, ">h", "reaction_modifier"),
    (0x2C, ">h", "next_group_chance"),
    (0x2E, ">h", "defense"),
    (0x30, ">I", "gold"),
    (0x34, ">B", "group_size_factor"),
    (0x35, ">B", "byte_34"),
    (0x36, ">2B", "treasure_items"),
    (0x38, ">h", "attack_power"),
]
COMPARE = [f for _, _, f in MONSTER_FIELDS if f != "pad_23"]


def main() -> int:
    p = load_prg(PRG)
    D = p.data_start
    os.makedirs(OUT, exist_ok=True)

    ptrs = [p.u32(D + NAME_PTRS + 4 * i) for i in range(85)]
    names = [p.cstr(a) for a in ptrs]
    items = []
    for i in range(181):
        a = p.u32(D + ITEM_PTRS + 4 * i)
        items.append({"index": i, "name": p.cstr(a), "value_factor": p.u16(D + ITEM_VALUES + 2 * i)})
    spells = [{"slot": i, "number": i + 1, "name": n} for i, n in enumerate(names[25:])]
    names_out = {
        "_source": "PHANT.PRG (Atari ST Phantasie II v1.0), data segment pointer runs via the PRG fixup table",
        "classes": names[:6],
        "races": names[6:22],
        "status": names[22:25],
        "spells": spells,
        "items": items,
    }

    monsters = []
    for i in range(MONSTER_COUNT):
        base = D + MONSTERS + i * MONSTER_STRIDE
        raw = p.image[base : base + MONSTER_STRIDE]
        m = {"index": i, "name": raw[:20].split(b"\0", 1)[0].decode("latin-1")}
        for off, fmt, name in MONSTER_FIELDS:
            v = struct.unpack_from(fmt, raw, off)
            m[name] = list(v) if len(v) > 1 else v[0]
        m["raw_hex"] = raw.hex()
        monsters.append(m)

    # ---- comparison with Phantasie I: DOS tables (the I parity reference) and the Amiga I `game`
    # (the 68000 sibling: same 58-byte record at file 0xC88, FORMAT.md of amiga) ----
    diff: dict = {}
    p1m = json.load(open(P1_MONSTERS))
    p1m = p1m["monsters"] if isinstance(p1m, dict) else p1m
    p1_by_name = {m["name"]: m for m in p1m}
    amiga = open(AMIGA_GAME, "rb").read()
    am_by_name = {}
    for i in range(MONSTER_COUNT):
        raw = amiga[AMIGA_MONSTERS + i * MONSTER_STRIDE : AMIGA_MONSTERS + (i + 1) * MONSTER_STRIDE]
        rec = {"index": i}
        for off, fmt, name in MONSTER_FIELDS:
            v = struct.unpack_from(fmt, raw, off)
            rec[name] = list(v) if len(v) > 1 else v[0]
        am_by_name.setdefault(raw[:20].split(b"\0", 1)[0].decode("latin-1"), rec)
    rows = []
    for m in monsters:
        o = p1_by_name.get(m["name"])
        if not o:
            rows.append({"index": m["index"], "name": m["name"], "status": "new in II"})
            continue
        changed = {f: [o[f], m[f]] for f in COMPARE if o.get(f) != m[f]}
        a = am_by_name.get(m["name"])
        changed_am = {f: [a[f], m[f]] for f in COMPARE if a and a.get(f) != m[f]} if a else None
        rows.append(
            {
                "index": m["index"],
                "name": m["name"],
                "p1_index": o["index"],
                "status": "identical" if not changed else "changed",
                "changed": changed,
                "changed_vs_amiga_i": changed_am,
            }
        )
    p2_names = {m["name"] for m in monsters}
    diff["monsters"] = {
        "summary": {
            "identical": sum(r["status"] == "identical" for r in rows),
            "changed": sum(r["status"] == "changed" for r in rows),
            "new_in_ii": sum(r["status"] == "new in II" for r in rows),
            "p1_only": [m["name"] for m in p1m if m["name"] not in p2_names],
        },
        "rows": rows,
    }
    p1n = json.load(open(P1_NAMES))
    p1_spells = p1n["spells"]
    p1_spell_names = [s["name"] if isinstance(s, dict) else s for s in p1_spells]
    diff["spells"] = {
        "p1_names": p1_spell_names,
        "p2_slots_used": [s for s in spells if s["name"]],
    }
    p1i = json.load(open(P1_ITEMS))["items"]
    diff["items"] = [
        {"index": k, "p1": p1i[k]["name"], "p2": items[k]["name"], "p1_value": p1i[k]["value_factor"],
         "p2_value": items[k]["value_factor"]}
        for k in range(181)
        if p1i[k]["name"] != items[k]["name"] or p1i[k]["value_factor"] != items[k]["value_factor"]
    ]

    def dump(name, obj):
        with open(os.path.join(OUT, name), "w") as f:
            json.dump(obj, f, indent=1)
            f.write("\n")

    dump("names.json", names_out)
    dump("monsters.json", monsters)
    dump("diff-vs-p1.json", diff)
    s = diff["monsters"]["summary"]
    print(f"monsters: {s['identical']} identical to I, {s['changed']} changed, {s['new_in_ii']} new; "
          f"I-only: {len(s['p1_only'])}")
    print(f"items: {len(diff['items'])} differ from I (names/values)")
    print("spell slots used:", [(x['number'], x['name']) for x in spells if x['name']][-6:])
    return 0


if __name__ == "__main__":
    sys.exit(main())
