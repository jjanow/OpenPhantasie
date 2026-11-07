#!/usr/bin/env python3
"""Phantasie III (DOS SSI 1988) rule tables in MAIN.EXE's DGROUP -> reverse-engineering/dos/phantasie3/extracted/rule-tables.json.

Every table below is read from the bytes at the DS address its spec names; tags as in the specs:
- HP dice `DS:0xa78 + 22·class + (level − 1)` (u8, levels 1..22) [C] (`logic-specs/training.md` §4).
- Training XP curve G `DS:0xb88` (u32, index = level 0..21) and gold curve T `DS:0xb6c` (u16, index = level; T[16..20]
  run into G, a data overrun, training.md §3) [C].
- Magic ceiling ("MaxPow") `DS:0xafb + 15·class + level` (u8, level 1..15; the inn reads min(level, 15)) [C]
  (`FORMAT.md` §5 +0x1a: `0xafb` of segment `[0x5e16]` = DGROUP, found s82 by the manual's values).
- Skill class tables `C_k[class][L]` = `DS:base_k + 15·class + L` (u8, L 1..15) for eight skills [C] (training.md §5).
- Spells available by class and level `DS:0x1208`: 13 rows x 15 levels (u8 spell number, 0 = none) [C] bytes. Which
  rows belong to which class (Fighter 1, Thief 1, Priest 3, Ranger 2, Monk 2, Wizard 4, in that order) is [~] from the
  rows being cell-for-cell the manual's table (III PDF 10 / pr.14); the Learn Spells code (ovl2 `0x1a6c`) was not read.
- Score weights `DS:0x1c50` (u16 x 8, quest bits 0..7) [C] (`logic-specs/endgame.md` §2).
- Hit-location odds `DS:0x12cc` (u16, 5 rows x 6 cumulative %) [C] (`logic-specs/body-areas.md`).
- The Air elemental's spells `DS:0x1bb2` (u16 list, 0-terminated) [C] (`logic-specs/elementals.md` §1).
- Wilderness encounters (s83, root `0xde54`, `logic-specs/encounters.md` §7): area level `DS:0x9fe` (s8 x 25, section
  `x / 10 + 5·(y / 15)`, root `0x1198`) and the terrain offsets `DS:0xa17` (u8, index terrain + rnd(4), root `0xf143`) [C].
- Dungeon entries (`logic-specs/dungeon-events.md` §1, §7): record letter `DS:0x1c84 + 2·dungeon` (u16, 'A'..'K') and
  the entrance x `DS:0x1bb6 + 2·letter` (u16, indexed by the letter's character code) [C].

Classes are III's order: Fighter 0, Thief 1, Priest 2, Ranger 3, Monk 4, Wizard 5.
Run: python3 -I tools/scripts/extract_dos_p3_rules.py
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import extract_dos_p3_common as c  # noqa: E402

CLASSES = ["Fighter", "Thief", "Priest", "Ranger", "Monk", "Wizard"]
SKILL_TABLES = [("attack", 0xC53), ("parry", 0xCAD), ("swim", 0xD07), ("listen", 0xD61), ("find_trap", 0xDBB),
                ("disarm_trap", 0xE15), ("find_item", 0xE6F), ("pick_lock", 0xEC9)]
SPELL_ROWS = 0x1208
SPELL_ROW_COUNT = {"Fighter": 1, "Thief": 1, "Priest": 3, "Ranger": 2, "Monk": 2, "Wizard": 4}


def main() -> None:
    img, _, _ = c.unpack_root()
    D = c.DS_IMG

    def b(a: int) -> int:
        return img[D + a]

    def u16(a: int, n: int) -> list[int]:
        return list(struct.unpack_from(f"<{n}H", img, D + a))

    hp = {cl: [b(0xA78 + 22 * k + lv) for lv in range(22)] for k, cl in enumerate(CLASSES)}
    mp = {cl: [b(0xAFB + 15 * k + lv) for lv in range(1, 16)] for k, cl in enumerate(CLASSES)}
    skills = {name: {cl: [b(base + 15 * k + lv) for lv in range(1, 16)] for k, cl in enumerate(CLASSES)}
              for name, base in SKILL_TABLES}

    rows, a = {}, SPELL_ROWS
    for cl in CLASSES:
        rows[cl] = []
        for _ in range(SPELL_ROW_COUNT[cl]):
            rows[cl].append([b(a + i) for i in range(15)])
            a += 15
    levels = {}
    for cl, rs in rows.items():
        lv = [0] * 57
        for r in rs:
            for i, n in enumerate(r):
                if n:
                    assert lv[n - 1] == 0, f"{cl}: spell {n} twice"
                    lv[n - 1] = i + 1
        levels[cl] = lv

    air, p = [], 0x1BB2
    while (v := u16(p, 1)[0]) != 0:
        air.append(v)
        p += 2
    hit = [u16(0x12CC + 12 * r, 6) for r in range(5)]
    area_levels = list(struct.unpack_from("<25b", img, D + 0x9FE))
    terrain_offsets = [b(0xA17 + i) for i in range(26)]
    letters = [chr(u16(0x1C84 + 2 * n, 1)[0]) for n in range(1, 12)]
    entrance_x = {ch: u16(0x1BB6 + 2 * ord(ch), 1)[0] for ch in sorted(set(letters))}

    c.write_json("rule-tables.json", {
        "_source": "MAIN.EXE DGROUP tables; addresses and tags in tools/scripts/extract_dos_p3_rules.py",
        "classes": CLASSES,
        "hp_dice": {"_address": "DS:0xa78 + 22*class + (level - 1)", "_note": "levels 1..22; dice of rnd(3) at "
                    "training, rnd(2) + 1 at creation; twice each L1-15 value = the manual's CON-10 HP chart",
                    "by_class": hp},
        "training_xp_curve": {"_address": "DS:0xb88 u32, index = level (0..21)", "values": list(
            struct.unpack_from("<22I", img, D + 0xB88))},
        "training_gold_curve": {"_address": "DS:0xb6c u16, index = level; 16..20 overrun into the XP curve",
                                "values": u16(0xB6C, 21)},
        "magic_ceiling": {"_address": "DS:0xafb + 15*class + level (1..15)", "by_class": mp},
        "skill_tables": {"_address": "DS:base + 15*class + level (1..15)",
                         "_bases": {n: f"{a:#x}" for n, a in SKILL_TABLES}, "by_skill": skills},
        "spell_rows": {"_address": "DS:0x1208, 13 rows x 15 levels; class grouping [~] (manual match)",
                       "by_class": rows},
        "spell_levels": {"_note": "derived from spell_rows: level at which spell #n (index n - 1) becomes learnable, "
                                  "0 = never", "by_class": levels},
        "score_weights": {"_address": "DS:0x1c50 u16 x 8", "values": u16(0x1C50, 8)},
        "hit_location": {"_address": "DS:0x12cc u16, 5 rows x 6 cumulative percent",
                         "_columns": ["head", "torso", "right_leg", "right_arm", "left_leg", "left_arm"],
                         "_rows": ["bow hit on a monster", "spells [~]", "melee hit; every monster hit on a character",
                                   "Aim blow", "any hit on a 'tall' monster"],
                         "rows": hit},
        "air_elemental_spells": {"_address": "DS:0x1bb2 u16, 0-terminated", "values": air},
        "wilderness_area_levels": {"_address": "DS:0x9fe s8 x 25, index x / 10 + 5 * (y / 15) (root 0x1198)",
                                   "values": area_levels},
        "wilderness_terrain_offsets": {"_address": "DS:0xa17 u8, index terrain + rnd(4) (root 0xf143 kind 3); "
                                                   "26 entries cover terrain 0..21", "values": terrain_offsets},
        "dungeon_letters": {"_address": "DS:0x1c84 + 2 * dungeon (dungeons 1..11)", "values": letters},
        "dungeon_entrance_x": {"_address": "DS:0x1bb6 + 2 * letter code (ovl4 0x836); y = 0", "by_letter": entrance_x},
    })
    print("spell rows", sum(len(r) for r in rows.values()), "air spells", len(air))


if __name__ == "__main__":
    main()
