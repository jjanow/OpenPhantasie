#!/usr/bin/env python3
"""Extract the TWNS.INT level-up tables into creation-tables.json.

Adds the full 15-level skill-coefficient table (TWNS.INT @0x21c) and the spell-level
table (@0x05a, 60 bytes per class) that the level-up routine (OV1 0x9ee, see
town-menus.md §Level-up gains) reads. Cross-checks the rows creation-tables.json
already holds (hp_factors, mp_ceiling, level-0 skill coefficients) against the binary
and checks the four tables tile 0x000 -> 0x05a -> 0x1c2 -> 0x21c -> 0x924.

Remaster decision applied in the data (DIFFERENCES.md D-006, user 2026-10-10): the
MONK row drops SLEEP (spell index 41 = manual #42) from level 7, per the manual.

Usage:  python3 extract_level_tables.py
"""
from __future__ import annotations

import json
import struct

from re_common import REPO, read

CLASSES = ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"]
LEVELS = 15
SKILLS = 10
SPELL_SLOTS = 60

HP, SPELL_LV, MP_CEIL, SKILL, END = 0x000, 0x05A, 0x1C2, 0x21C, 0x924
SKILL_CLASS_STRIDE, SKILL_LEVEL_STRIDE = 0x12C, 0x14

# D-006: manual's Monk level 7 = spells 13 and 48 only (1-based), DOS also lists 42 SLEEP.
D006_MONK_SLEEP_INDEX = 41

TARGET = REPO / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie1" / "creation-tables.json"


def main():
    d = read("TWNS.INT")
    assert SPELL_LV - HP == len(CLASSES) * LEVELS
    assert MP_CEIL - SPELL_LV == len(CLASSES) * SPELL_SLOTS
    assert SKILL - MP_CEIL == len(CLASSES) * LEVELS
    assert END - SKILL == len(CLASSES) * SKILL_CLASS_STRIDE == len(CLASSES) * LEVELS * SKILL_LEVEL_STRIDE

    tables = json.loads(TARGET.read_text())
    skills, spells = {}, {}
    for c, name in enumerate(CLASSES):
        assert list(d[HP + c * LEVELS:HP + (c + 1) * LEVELS]) == tables["hp_factors"]["by_class"][name], name
        assert list(d[MP_CEIL + c * LEVELS:MP_CEIL + (c + 1) * LEVELS]) == tables["mp_ceiling"]["by_class"][name], name

        rows = []
        for lv in range(LEVELS):
            base = SKILL + c * SKILL_CLASS_STRIDE + lv * SKILL_LEVEL_STRIDE
            rows.append([struct.unpack_from("<H", d, base + 2 * k)[0] for k in range(SKILLS)])
        old_l0 = tables["skill_coefficients"].get("by_class_level0", {}).get(name)
        assert old_l0 is None or rows[0] == old_l0, name
        skills[name] = rows

        row = list(d[SPELL_LV + c * SPELL_SLOTS:SPELL_LV + (c + 1) * SPELL_SLOTS])
        assert all(v == 0 for v in row[55:]), f"{name}: spell slots 55..59 not empty"
        if name == "MONK":
            assert row[D006_MONK_SLEEP_INDEX] == 7, "DOS Monk SLEEP cell moved?"
            row[D006_MONK_SLEEP_INDEX] = 0
        spells[name] = row[:55]

    sc = tables["skill_coefficients"]
    sc.pop("by_class_level0", None)
    sc["layout"] = "class * 0x12c + level * 0x14 + skill * 2 (u16 LE); by_class_level[class][level][skill]"
    sc["by_class_level"] = skills

    tables["spell_levels"] = {
        "offset": "0x005a",
        "layout": "class * 60 + spell (u8); value = character level that makes the spell learnable, 0 = never",
        "note": "Slots 55..59 are 0 in DOS and dropped. MONK spell 41 (manual #42 SLEEP) at level 7 removed "
                "per the manual (DIFFERENCES.md D-006); DOS has 7 there.",
        "by_class": spells,
    }
    TARGET.write_text(json.dumps(tables, indent=2, ensure_ascii=False) + "\n")
    print(f"wrote {TARGET.relative_to(REPO)}")


if __name__ == "__main__":
    main()
