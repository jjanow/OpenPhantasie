#!/usr/bin/env python3
"""Parse a Phantasie I GUILD.DAT roster into JSON.

Record layout was reverse-engineered in the Phase-3 dynamic pass by creating
three characters (wizardo=ELF/WIZARD, priesto=HUMAN/PRIEST, thiefo=LIZ.MAN/THIEF)
in DOSBox and byte-diffing GUILD.DAT against a zero-filled snapshot, then reading
each character sheet on-screen to label every offset. Offsets marked CONFIRMED
were validated against all three sheets; a few tail fields are still open (see
logic-specs/save-file-format.md).

Usage:  python3 parse_guild.py [path/to/GUILD.DAT]
Default reads the 3-character fixture in extracted/samples/.
"""
from __future__ import annotations

import json
import struct
import sys
from pathlib import Path

from re_common import DOS, write_json

REC = 282          # CONFIRMED: 6204 / 282 = 22 records exactly
SLOTS = 22         # CONFIRMED

# Displayed race table (PHANTASI.COM 0x6d77): 15 playable races + 15 = UNDEAD [C].
RACES = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE",
         "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC", "GOBLIN", "LIZ.MAN",
         "MINOTAUR", "UNDEAD"]
CLASSES = ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"]
# Status word @24 (PHANTASI.COM status table 0x6dfe) [C], verified in DOSBox (s10,
# PLAN.md H3). Other values have no name in DOS and are emitted as raw ints.
STATUS = {0: "DEAD", 1: "OK", 2: "SLEEP"}


def u16(r, o):
    return struct.unpack_from("<H", r, o)[0]


def parse_record(r: bytes):
    name = r[270:282].split(b"\x00")[0].rstrip(b" ").decode("latin1")
    if not name:
        return None  # empty slot
    return {
        # --- CONFIRMED (validated vs. 3 on-screen sheets) ---
        "name": name,                       # @270, 12-byte space/NUL-padded
        "race": RACES[r[216]] if r[216] < len(RACES) else r[216],   # @216 u8 (birth race)
        "displayed_race": RACES[u16(r, 256)] if u16(r, 256) < len(RACES) else u16(r, 256),  # @256
        "class": CLASSES[r[220]] if r[220] < len(CLASSES) else r[220],  # @220 u8
        "status": STATUS.get(u16(r, 24), u16(r, 24)),               # @24 u16
        "level": u16(r, 88),                # @88 (sheet LEVEL, PHANTASI.COM 0x2f1d)
        "in_party": u16(r, 218),            # @218: 1 = in active party (Guild Add/Drop)
        "strength": u16(r, 0),
        "intelligence": u16(r, 2),
        "dexterity": u16(r, 4),
        "constitution": u16(r, 6),
        "charisma": u16(r, 8),
        "luck": u16(r, 10),
        "word_12": u16(r, 12),              # @12 defense rating, written by combat prep OV2 0x32f7 [C] (s19)
        "mp_ceiling": u16(r, 14),           # @14 MP ceiling for class+level (s11; was "combat rating")
        "magic_max": u16(r, 16),            # @16 MP max (s11 relabel — was read as cur)
        "magic_cur": u16(r, 18),            # @18 MP current
        "hp_max": u16(r, 20),               # @20/@22 — pass 3 corrected vs sheet
        "hp_cur": u16(r, 22),
        "gold_in_bank": u16(r, 262),        # @262
        "experience": u16(r, 266),          # @266; treat as low word of s32 until XP>65535
        "weapon_dmg_a": u16(r, 28),         # @28 — combat damage = @28 + @240 (OV2 0x3284); writer [?] (s19)
        "inventory": [u16(r, 222 + 2 * i) for i in range(9) if u16(r, 222 + 2 * i)],  # @222..@238, worn incl.
        "worn_shield": u16(r, 82),          # @82 — pass 3 crusho sheet match
        "worn_armor": u16(r, 84),           # @84
        "worn_weapon": u16(r, 86),          # @86
        "skills": {                         # u16; full set CONFIRMED pass 4 vs thiefo sheet
            "attack": u16(r, 30),
            "parry": u16(r, 32),
            "find_item": u16(r, 34),
            "spot_trap": u16(r, 36),
            "disarm_trap": u16(r, 38),
            "listen": u16(r, 40),
            "pick_lock": u16(r, 44),
            "swim": u16(r, 48),
        },
        "skill_bonus": [u16(r, 50 + 2 * i) for i in range(10)],  # @50..@68, record order incl. unused @42/@46
        "score": u16(r, 70),                # @70 — mystic party score, mirrored [C]
        "cast_since_rest": u16(r, 94),      # @94 — Inn MAGIC INCREASES flag [C]
        "lifespan_dos": u16(r, 252),        # @252 — DOS lifespan in @264>>8 steps [C]
        "current_town": u16(r, 254),        # @254
        "has_aged": u16(r, 258),            # @258 — one-time old-age decline latch [C]
        "age_clock": u16(r, 264),           # @264 — 1 unit = 2 weeks; AGE = @264 / 26 [C]
    }


def main():
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else \
        DOS / "extracted" / "samples" / "GUILD.3chars.dat"
    data = path.read_bytes()
    roster = []
    for i in range(SLOTS):
        rec = parse_record(data[i * REC:(i + 1) * REC])
        if rec:
            rec["slot"] = i
            roster.append(rec)
    out = {"_source": str(path.name), "record_size": REC, "slots": SLOTS,
           "characters": roster}
    p = write_json("guild.json", out)
    print(f"parsed {len(roster)} characters -> {p}\n")
    for c in roster:
        print(f"  [{c['slot']:2d}] {c['name']:12s} {c['race']:8s} {c['class']:7s} "
              f"L{c['level']} STR{c['strength']} INT{c['intelligence']} "
              f"DEX{c['dexterity']} CON{c['constitution']} CHA{c['charisma']} "
              f"LUCK{c['luck']} HP{c['hp_cur']}/{c['hp_max']} "
              f"MAG{c['magic_cur']}/{c['magic_max']} GOLD{c['gold_in_bank']}")


if __name__ == "__main__":
    main()
