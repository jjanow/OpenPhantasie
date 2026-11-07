#!/usr/bin/env python3
"""Phantasie II (Atari ST): TWNS.INT / TWNS.DAT -> extracted/towns/ (task 3b.8).

TWNS.INT (13900 B) is read by PHANT.PRG 0xed50 as eight consecutive blocks [C]:
    90 (hp factors) + 360 (spell levels) + 90 (MP ceiling) + 1800 (skill coefficients) + 30 (class modifiers)
    + 76 (race modifiers) = 2446 B of creation tables, then 3504 B = the initial TWNS.DAT (12 x 292),
    then 7950 B = the initial GUILD.DAT (25 x 318: 22 empty slots + FIRE/WATER/EARTH ELEM).
Amiga I twns.int (13000 B) is the same with a 282-byte Guild record (2446 + 3504 + 7050); DOS I (12987 B) has a
75-byte race block and 291-byte town records (2445 + 3492 + 7050). Words are big-endian on the ST.

Town record, 292 B (DOS 291 B + one pad byte at 181, so every DOS field from 181 on moves up by one) [C]:
    0..180 u8 stock by item id | 181 pad | 182 u16[45] restock list | 272 u16 price index | 274 u16 exit region
    | 276 u16 exit row y | 278 u16 exit column 25 - x | 280 name (10 chars) | 290 u16 (0 in all records)
The (y, 25 - x) order is the ST/Amiga map convention (FORMAT.md §9.2); it is checked here against the town cells.

Writes: towns.json, creation-tables.json, elementals.json, twns-dat-played.json.
Run from the repo root: python3 -I tools/scripts/extract_st_p2_towns.py
"""
from __future__ import annotations

import json
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_st_p2_common import DOS_ORIG, REGIONS, amiga, d1, d2, rel, unfog, write_json  # noqa: E402

CREATION_END = 2446
TOWN_OFF, TOWN_STRIDE, TOWN_COUNT = 2446, 292, 12
GUILD_OFF, GUILD_STRIDE, GUILD_COUNT = 5950, 318, 25
BLOCKS = [("hp_factors", 0x000, 90), ("spell_levels", 0x05A, 360), ("mp_ceiling", 0x1C2, 90),
          ("skill_coefficients", 0x21C, 1800), ("class_attribute_modifiers", 0x924, 30),
          ("race_attribute_modifiers", 0x942, 76)]
CLASSES = ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"]
RACES = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD",
         "ORC", "GOBLIN", "LIZ.MAN", "MINOTAUR"]
STATS = ["STR", "INT", "DEX", "CON", "CHA"]
SKILL_ORDER = ["attack", "parry", "find_item", "spot_trap", "disarm_trap", "listen", "_42_toss", "pick_lock",
               "_unused_46", "swim"]

# Elemental fields: DOS 282-byte record offsets (save-file-format.md), read as big-endian words here.
ELEM_WORDS = {2: "intelligence", 14: "mp_ceiling", 16: "magic_max", 18: "magic_cur", 20: "hp_max", 22: "hp_cur",
              24: "status", 30: "attack", 32: "parry", 88: "level", 92: "guild_number", 216: "birth_race",
              220: "class", 240: "weapon_damage_b", 242: "shield_soak", 244: "armor_soak", 256: "race"}


def creation_tables(d: bytes, le: bool) -> dict:
    w = "<H" if le else ">H"
    hp = {c: list(d[i * 15:(i + 1) * 15]) for i, c in enumerate(CLASSES)}
    sp = {c: list(d[0x5A + i * 60:0x5A + (i + 1) * 60]) for i, c in enumerate(CLASSES)}
    mp = {c: list(d[0x1C2 + i * 15:0x1C2 + (i + 1) * 15]) for i, c in enumerate(CLASSES)}
    sk = {c: [[struct.unpack_from(w, d, 0x21C + i * 0x12C + lv * 0x14 + 2 * k)[0] for k in range(10)]
              for lv in range(15)] for i, c in enumerate(CLASSES)}
    cm = {c: dict(zip(STATS, d[0x924 + i * 5:0x924 + (i + 1) * 5])) for i, c in enumerate(CLASSES)}
    rm = {r: {"modifiers": dict(zip(STATS, d[0x942 + i * 5:0x942 + (i + 1) * 5])),
              "ceilings": {s: v + 8 for s, v in zip(STATS, d[0x942 + i * 5:0x942 + (i + 1) * 5])}}
          for i, r in enumerate(RACES)}
    return {"hp_factors": hp, "spell_levels": sp, "mp_ceiling": mp, "skill_coefficients": sk,
            "class_attribute_modifiers": cm, "race_attribute_modifiers": rm}


def town_record(rec: bytes) -> dict:
    price, region, row, col = struct.unpack_from(">4H", rec, 272)
    return {
        "name": rec[280:290].split(b"\0")[0].decode("latin-1").strip(),
        "price_index": price,
        "exit_region": region,
        "exit_raw": [row, col],
        "exit_x": 25 - col,
        "exit_y": row,
        "stock": list(rec[:181]),
        "restock_items": list(struct.unpack_from(">45H", rec, 182)),
        "pad_181": rec[181],
        "word_290": struct.unpack_from(">H", rec, 290)[0],
    }


def region_cells() -> dict:
    """{region: {(x, y): (tile, feature arg or None)}} for the town checks (FORMAT.md §9.2 conventions)."""
    out = {}
    for r in REGIONS:
        o = d2(f"OUT{r}.DAT")
        feats = {}
        for i in range(15):
            b0, b1, arg, _, _ = o[520 + 5 * i:525 + 5 * i]
            feats[(25 - b1, b0)] = arg
        out[r] = {(x, y): (unfog(o[(25 - x) * 20 + y]), feats.get((x, y))) for x in range(26) for y in range(20)}
    return out


def main() -> None:
    t = d1("TWNS.INT")
    assert len(t) == 13900
    assert sum(b[2] for b in BLOCKS) == CREATION_END == TOWN_OFF
    assert TOWN_OFF + TOWN_COUNT * TOWN_STRIDE == GUILD_OFF and GUILD_OFF + GUILD_COUNT * GUILD_STRIDE == len(t)

    # --- creation tables -------------------------------------------------------------------------------------
    am = amiga("twns.int")
    dos = (DOS_ORIG / "TWNS.INT").read_bytes()
    st_tables = creation_tables(t, le=False)
    dos_tables = creation_tables(dos, le=True)
    same_amiga = am is not None and t[:CREATION_END] == am[:CREATION_END]
    diffs = []
    for key in st_tables:
        if st_tables[key] != dos_tables[key]:
            diffs.append(key)
    ct = {
        "_schema": "phantasie.creation-tables/v1 (shape of GameData/phantasie1/creation-tables.json, raw values)",
        "_source": "Phantasie II ST disk1/TWNS.INT bytes 0..2445 (PHANT.PRG 0xed50 block reads) [C]",
        "_note": ("Raw binary values, no remaster decisions applied: HALFLING DEX modifier and MONK spell 41 "
                  "(manual #42 SLEEP) at level 7 are as in the file. Phantasie I's GameData applies D-001 and D-006 "
                  "to the same cells; whether II follows those decisions is the user's call."),
        "identical_to_amiga_p1_bytes_0_2445": same_amiga,
        "tables_differing_from_dos_p1_values": diffs,
        "hp_factors": {"offset": "0x0000", "layout": "class * 15 + level", "by_class": st_tables["hp_factors"]},
        "mp_ceiling": {"offset": "0x01c2", "layout": "class * 15 + level", "by_class": st_tables["mp_ceiling"]},
        "skill_coefficients": {"offset": "0x021c",
                               "layout": "class * 0x12c + level * 0x14 + skill * 2 (u16 BE)",
                               "skill_order": SKILL_ORDER,
                               "by_class_level": st_tables["skill_coefficients"]},
        "class_attribute_modifiers": {"offset": "0x0924", "by_class": st_tables["class_attribute_modifiers"]},
        "race_attribute_modifiers": {"offset": "0x0942",
                                     "note": "15 races x 5 bytes + 1 pad byte (76-byte block; DOS 75). ceiling = "
                                             "modifier + 8 as in Phantasie I [~] for II",
                                     "pad_byte": t[0x942 + 75],
                                     "by_race": st_tables["race_attribute_modifiers"]},
        "spell_levels": {"offset": "0x005a", "layout": "class * 60 + spell slot (u8); 0 = never",
                         "by_class": st_tables["spell_levels"]},
    }
    p_ct = write_json("towns", "creation-tables.json", ct)

    # --- towns ------------------------------------------------------------------------------------------------
    cells = region_cells()
    town_cells = {}  # town index -> map cells whose tile is 40 and feature arg - 1 == index
    for r, cs in cells.items():
        for (x, y), (tile, arg) in cs.items():
            if tile == 40 and arg:
                town_cells.setdefault(arg - 1, []).append({"region": r, "x": x, "y": y})
    towns = []
    for i in range(TOWN_COUNT):
        rec = t[TOWN_OFF + i * TOWN_STRIDE:TOWN_OFF + (i + 1) * TOWN_STRIDE]
        tr = town_record(rec)
        ex = cells.get(tr["exit_region"], {}).get((tr["exit_x"], tr["exit_y"]))
        am_rec = am[TOWN_OFF + i * TOWN_STRIDE:TOWN_OFF + (i + 1) * TOWN_STRIDE] if am else b""
        towns.append({
            "id": i + 1,
            "index": i,
            **tr,
            "exit_cell_tile": ex[0] if ex else None,
            "map_town_cells": town_cells.get(i, []),
            "identical_to_amiga_p1_record": rec == am_rec,
            "record_hex": rec.hex(),
        })
    doc = {
        "_source": "Phantasie II ST disk1/TWNS.INT bytes 2446..5949 = the initial TWNS.DAT (12 x 292 B) [C]",
        "_layout": {"record_stride": TOWN_STRIDE, "town_count": TOWN_COUNT, "stock_slots": 181,
                    "restock_items_offset": "0xb6 (u16 BE x 45)", "price_index_offset": "0x110",
                    "exit_region_offset": "0x112", "exit_row_offset": "0x114", "exit_col_offset": "0x116",
                    "name_offset": 280},
        "_notes": [
            "Field meanings carried over from DOS I (save-file-format.md section Town state) with the +1 shift [~].",
            "exit_x/exit_y: the record holds (row y, column 25 - x), the ST map convention [C by data]: 8 of the 10 "
            "map towns' exit cells are their own tile-40 cell.",
            "map_town_cells: cells with tile 40 whose feature arg - 1 == this town index. Town numbers are 1-based "
            "(PHANT.PRG 0x26cc: record n - 1, n = 99 -> record 11) [C]; GUILD @254 holds the number (1 = PIPPACOTT).",
            "STARVILLE and OLYMPIA are byte-identical to Phantasie I's records and no map cell leads to them.",
        ],
        "towns": towns,
    }
    p_t = write_json("towns", "towns.json", doc)

    # --- initial Guild + elementals ---------------------------------------------------------------------------
    guild = t[GUILD_OFF:]
    assert not any(guild[:22 * GUILD_STRIDE]), "TWNS.INT: the 22 character slots are not empty"
    elems = []
    for i in range(22, 25):
        rec = guild[i * GUILD_STRIDE:(i + 1) * GUILD_STRIDE]
        e = {"slot": i, "name": rec[270:282].split(b"\0")[0].decode("latin-1")}
        for off, key in ELEM_WORDS.items():
            e[key] = struct.unpack_from(">H", rec, off)[0]
        e["known_spells"] = [k for k in range(60) if struct.unpack_from(">H", rec, 0x60 + 2 * k)[0] == 1]
        e["bytes_282_317_nonzero"] = {str(o): rec[o] for o in range(282, 318) if rec[o]}
        e["record_hex"] = rec.hex()
        elems.append(e)
    p_e = write_json("towns", "elementals.json", {
        "_source": "Phantasie II ST disk1/TWNS.INT bytes 5950..13899 = the initial GUILD.DAT (25 x 318 B) [C]; "
                   "slots 0..21 are all zero, slots 22..24 are the elementals",
        "_notes": "Field names from Phantasie I's elementals.json (DOS offsets, big-endian words). Compare with "
                  "GameData/phantasie1/elementals.json before reuse.",
        "elementals": elems,
    })

    # --- played TWNS.DAT vs the template ----------------------------------------------------------------------
    dat = d2("TWNS.DAT")
    assert len(dat) == TOWN_COUNT * TOWN_STRIDE
    played = []
    for i in range(TOWN_COUNT):
        a = t[TOWN_OFF + i * TOWN_STRIDE:TOWN_OFF + (i + 1) * TOWN_STRIDE]
        b = dat[i * TOWN_STRIDE:(i + 1) * TOWN_STRIDE]
        ch = [{"offset": o, "item_id" if o < 181 else "field": o if o < 181 else hex(o), "init": a[o],
               "played": b[o]} for o in range(TOWN_STRIDE) if a[o] != b[o]]
        if ch:
            played.append({"index": i, "name": towns[i]["name"], "changes": ch})
    p_p = write_json("towns", "twns-dat-played.json", {
        "_source": "disk2/TWNS.DAT (played state, FAT date 1995-10-06) compared with TWNS.INT's template",
        "_note": "Only Armory stock bytes differ: the played file is the template after shopping in PIPPACOTT.",
        "towns_changed": played,
    })
    for p in (p_ct, p_t, p_e, p_p):
        print("wrote", rel(p))


if __name__ == "__main__":
    main()
