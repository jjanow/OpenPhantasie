#!/usr/bin/env python3
"""Extract the three SUMMON ELEMENTAL templates from TWNS.INT into elementals.json.

The last 3 x 282 bytes of TWNS.INT (file 12141, 12423, 12705) are character records: FIRE ELEM, WATER ELEM,
EARTH ELEM. The 6204 zero bytes before them (= 22 records = GUILD.DAT) are the roster buffer, so the elementals
are roster records 22-24 (Guild numbers 23-25). Guild -> Add copies one of them into party slot 7 (OV1 0x2786).
See combat-resolution.md section "SUMMON ELEMENTAL" (s33).

The script checks that every field it does not export is zero (spells: 1 known / 2 otherwise), so nothing in the
records is silently dropped.

Usage:  python3 extract_elementals.py
"""
from __future__ import annotations

import json
import struct

from re_common import REPO, read

REC = 282
COUNT = 3
SPELL_SLOTS = 60  # record @0x60..@0xd7 (DOS slot 57 = DIVINE I, endgame.md)
TARGET = REPO / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie1" / "elementals.json"

# offset -> json key (u16 words)
WORDS = {
    2: "intelligence",
    14: "mp_ceiling",
    16: "magic_max",
    18: "magic_cur",
    20: "hp_max",
    22: "hp_cur",
    24: "status",
    30: "attack",
    32: "parry",
    88: "level",
    92: "guild_number",
    240: "weapon_damage_b",
    242: "shield_soak",
    244: "armor_soak",
    256: "race",
}
BYTES = {216: "birth_race", 220: "class"}
SPELLS = 0x60


def parse(rec: bytes) -> dict:
    w = lambda o: struct.unpack_from("<H", rec, o)[0]
    out = {"name": rec[270:282].split(b"\0")[0].decode("ascii").strip()}
    for off, key in WORDS.items():
        out[key] = w(off)
    for off, key in BYTES.items():
        out[key] = rec[off]
    spells = [w(SPELLS + 2 * k) for k in range(SPELL_SLOTS)]
    assert set(spells) <= {1, 2}, spells
    out["known_spells"] = [k for k, v in enumerate(spells) if v == 1]

    covered = set()
    for off in WORDS:
        covered |= {off, off + 1}
    covered |= set(BYTES)
    covered |= set(range(SPELLS, SPELLS + 2 * SPELL_SLOTS))
    covered |= set(range(270, 282))
    stray = {o: rec[o] for o in range(REC) if o not in covered and rec[o] != 0}
    assert not stray, f"{out['name']}: unexported nonzero bytes {stray}"
    return out


def main() -> None:
    data = read("TWNS.INT")
    start = len(data) - COUNT * REC
    assert start == 12141, start
    assert not any(data[start - 22 * REC:start]), "roster placeholder block is not zero"
    templates = [parse(data[start + i * REC:start + (i + 1) * REC]) for i in range(COUNT)]
    doc = {
        "_source": "TWNS.INT tail, 3 x 282-byte character records at file 12141/12423/12705 "
                   "(tools/scripts/extract_elementals.py). Roster records 22-24.",
        "_notes": "Every field not listed is 0, including @12 defense and the combat fields @246/@248/@250 "
                  "(DOS prep never reaches party slot 7, combat-resolution.md section SUMMON ELEMENTAL). "
                  "known_spells: record @0x60 slots set to 1; every other slot is 2. race 15 = UNDEAD (displayed); "
                  "birth_race 16 is outside the race table. class 3 = FIGHTER.",
        "elementals": templates,
    }
    TARGET.write_text(json.dumps(doc, indent=2) + "\n")
    print(f"wrote {TARGET} ({len(templates)} elementals)")


if __name__ == "__main__":
    main()
