"""Extract the Phantasie I monster table from PHANTASI.COM (s22).

The game keeps 80 fixed-stride (0x39-byte) monster records at DS:0x1a6c of the
unpacked program image (see ``re_common.unpack_phantasi_com``). The OV2
encounter builder (file 0x34aa) copies a record into the combat struct at
``0x7522 + slot*0x39`` with the same layout, so every field role below is
pinned by an OV2 use-site (``combat-resolution.md`` §Monster record).

Older extracts parsed the *packed* bytes at file 0x7131 as variable-length
records — that ``monsters.json`` (``combat_rating``/``attacks``/``two_form``)
was EXEPACK tokens, not monster data. This script replaces it.

Cross-check: the result must equal the live DOSBox-X capture
``extracted/monster_runtime_table.bin`` byte for byte.
"""
from __future__ import annotations

import struct

from re_common import OUT, com_ds, unpack_phantasi_com, write_json

TABLE_DS = 0x1A6C
SPRITE_DIM_DS = 0x2C3E  # u16 width, u16 height per monster (OV2 0x38b2)
STRIDE = 0x39
COUNT = 80


def i16(rec: bytes, off: int) -> int:
    return struct.unpack_from("<h", rec, off)[0]


def u32(rec: bytes, off: int) -> int:
    return struct.unpack_from("<I", rec, off)[0]


def parse(image: bytes) -> list[dict]:
    table = com_ds(image, TABLE_DS, STRIDE * COUNT)
    capture = (OUT / "monster_runtime_table.bin").read_bytes()
    if table != capture:
        raise SystemExit("unpacked DS:0x1a6c differs from the DOSBox capture")
    dims = com_ds(image, SPRITE_DIM_DS, 4 * COUNT)

    monsters = []
    for i in range(COUNT):
        r = table[i * STRIDE:(i + 1) * STRIDE]
        w, h = struct.unpack_from("<HH", dims, 4 * i)
        monsters.append({
            "id": f"p1:{i}",  # game-prefixed id (Phase 09 T1.2)
            "index": i,
            "name": r[:0x14].split(b"\0", 1)[0].decode("latin1"),
            "armor": i16(r, 0x14),
            "damage": i16(r, 0x16),
            "level": i16(r, 0x18),
            "xp": u32(r, 0x1A),
            "special_chance": i16(r, 0x1E),
            "specials": list(r[0x20:0x23]),
            "hp_base": i16(r, 0x23),
            "combat_rating": i16(r, 0x25),
            "companions": [r[0x27], r[0x28]],
            "reaction_modifier": i16(r, 0x29),
            "next_group_chance": i16(r, 0x2B),
            "defense": i16(r, 0x2D),
            "gold": u32(r, 0x2F),
            "group_size_factor": r[0x33],
            "byte_34": r[0x34],
            "treasure_items": [r[0x35], r[0x36]],
            "attack_power": i16(r, 0x37),
            "sprite_width": w,
            "sprite_height": h,
            "raw_hex": r.hex(),
        })
    return monsters


def main() -> None:
    image = unpack_phantasi_com()
    monsters = parse(image)
    path = write_json("monsters.json", {
        "_source": "PHANTASI.COM unpacked (EXEPACK), DS:0x1a6c, 80 x 0x39; "
                   "equals extracted/monster_runtime_table.bin (DOSBox capture)",
        "_fields": {
            "armor": "+0x14 [C] party damage subtracts armor*10 (OV2 0xbce)",
            "damage": "+0x16 [C] monster damage factor (OV2 0xea9)",
            "level": "+0x18 [C] encounter level L += level once per group (OV2 0x3ac4)",
            "xp": "+0x1a u32 [C] XP per monster killed (OV2 0x1656)",
            "special_chance": "+0x1e [C] special-ability gate (OV2 0xd49)",
            "specials": "+0x20 3 bytes [C] special picked by rand*3/100; effects [C] OV2 0x1004 -> 0x16a9 (combat-resolution.md Monster specials, s26); a pick > 0x21 falls back to melee",
            "hp_base": "+0x23 [C] HP = ((rand0_99 + 50) * hp_base + 50) / 100 (OV2 0x3a27)",
            "combat_rating": "+0x25 [C] monster CV, spell/breath to-hit (OV2 0x13ce)",
            "companions": "+0x27/+0x28 [C] 0-based monster id of the next group (rand<50 -> +0x28 else +0x27) (OV2 0x3adc)",
            "reaction_modifier": "+0x29 [~] summed per group into [0x750c]; reaction roll rand < sum + 90 (OV2 0x3bed)",
            "next_group_chance": "+0x2b [C] another group follows if rand < this (max 3 groups) (OV2 0x3b39)",
            "defense": "+0x2d [C] party melee to-hit subtracts this (OV2 0xb5a); often negative",
            "gold": "+0x2f u32 [C] gold added per monster at encounter build (OV2 0x3a97)",
            "group_size_factor": "+0x33 [C] count = clamp(rand0_99 * f / 102, 0, 5) + 1 (OV2 0x39b1)",
            "byte_34": "+0x34 [~] overwritten in the combat copy by sprite width (OV2 0x38cb)",
            "treasure_items": "+0x35/+0x36 [C] item ids dropped as treasure, first group only (OV2 0x41e4)",
            "attack_power": "+0x37 [C] monster melee to-hit; swings = max(1, ap/82) (OV2 0xd86)",
            "sprite_width": "DS:0x2c3e [C] build stops adding groups once sum(width+3) >= 135 (OV2 0x399b)",
            "sprite_height": "DS:0x2c40 [~]",
        },
        "monsters": monsters,
    })
    print(f"wrote {path} ({len(monsters)} monsters)")


if __name__ == "__main__":
    main()
