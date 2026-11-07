#!/usr/bin/env python3
"""Phantasie III (DOS SSI 1988) master tables -> reverse-engineering/dos/phantasie3/extracted/.

Sources (all static, `docs/plan/03c-phantasie3-re.md` 3c.3; tags as in `../FORMAT.md`):
- `MAIN.EXE` root (EXEPACK removed): the far-pointer string table at DS:0x1308 (one table for
  combat verbs, terrain, skills, races, classes, towns, origins, status, social classes, body
  states, item names 150+id, spell names 331+n, then UI messages) [C];
  item points DS:0x113c+id and attribute requirement DS:0x11a2+id, ids 0..100 [C bytes; read
  as `[bx+0x113c]` / `[bx+0x11a2]` in item code];
  transfer remap tables DS:0x48 (I class -> III class) and DS:0x4e (I race -> III race) [C].
- `CREATURE.DAT`: 80 x 38-byte monster records (+32 pad bytes). Byte-identical to the Amiga
  `monsinfo.dat`. Field names are by value match against DOS I's `monsters.json` (35 monsters
  share a name) [~] unless tagged otherwise.
- `INITARMS.DAT`: byte-identical to Amiga `inititem.dat` (checked here); decoded by extract_dos_p3_towns.py.

Also writes `diff-vs-p1.json` against `reverse-engineering/dos/extracted/{items,names,monsters}.json`.
Run: python3 -I tools/scripts/extract_dos_p3_tables.py
"""
from __future__ import annotations

import hashlib
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import extract_dos_p3_common as c  # noqa: E402

STR_TABLE = 0x1308
ITEM_BASE = 150          # string index of item id 0
SPELL_BASE = 331         # string index of spell 0 ("0"); spell n = 331 + n
POINTS = 0x113C
REQ = 0x11A2
VALUE = 0x0F92       # u16[181] item value, ends where the town price index starts
TOWN_PRICE = 0x10FC  # u16[18] price index by town number 0..17 (0 unused, 1 Pendragon; FORMAT §4.1)
TOWN_PRICE_COUNT = 18
CLASS_MAP = 0x48
RACE_MAP = 0x4E

GROUPS = {  # string-table index ranges -> names (end exclusive)
    "combat_actions": (0, 19),
    "status_short": (19, 24),
    "class_short": (24, 30),
    "rank_labels": (30, 33),
    "places": (34, 45),
    "inns": (46, 54),
    "terrain": (54, 76),
    "skills": (76, 85),
    "races": (85, 103),
    "classes": (103, 109),
    "towns": (112, 126),
    "origins": (126, 135),
    "status": (135, 140),
    "social_classes": (140, 146),
    "body_states": (146, 150),
}


def s8(x: int) -> int:
    return x - 256 if x > 127 else x


def string_table(img: bytes) -> list[str]:
    out = []
    i = 0
    while True:
        off, seg = struct.unpack_from("<HH", img, c.DS_IMG + STR_TABLE + 4 * i)
        if seg != 0x28C0:
            break
        out.append(c.cstr(img, c.DS_IMG + off))
        i += 1
    return out


def item_kind(i: int) -> str:
    if i == 0:
        return "none"
    if i <= 20:
        return "shield"
    if i <= 40:
        return "armor"
    if i <= 60:
        return "bow"
    if i <= 100:
        return "weapon"
    if i <= 110:
        return "healing_potion"
    if i <= 120:
        return "magic_potion"
    if i <= 140:
        return "scroll"
    return "treasure"


MON_FIELDS = [  # (offset, name, type, tag/notes)
    (0, "name", "char[14]", "[C] NUL-padded"),
    (14, "group_max", "u8", "[~] 1 for every boss (Nikademus, Lord Wood, the three Beasts), 2 for dragons/giants"),
    (15, "attack_power_div10", "u8", "[~] = DOS I attack_power / 10 (rounded) for 33 of 35 shared monsters"),
    (16, "damage", "u8", "[~] DOS I damage, often +1"),
    (17, "armor", "u8", "[~] equals DOS I armor for all 35 shared monsters"),
    (18, "defense", "s8", "[~] equals DOS I defense (33/35)"),
    (19, "level", "u8", "[~] equals DOS I level (35/35)"),
    (20, "special_chance", "u8", "[~] equals DOS I special_chance (32/35)"),
    (21, "specials", "u8[3]", "[~] same values as DOS I specials where shared"),
    (24, "combat_rating", "u8", "[~] equals DOS I combat_rating (35/35)"),
    (25, "hp_base", "u8", "[~] equals DOS I hp_base (35/35)"),
    (26, "reaction_modifier", "s8", "[~] equals DOS I reaction_modifier (34/35)"),
    (27, "byte_27", "u8", "[?] (sprite index / picture id?)"),
    (28, "next_group_chance", "u8", "[~] DOS I next_group_chance (16/35 equal, rest close)"),
    (29, "companions", "u8[2]", "[~] monster ids (self-references like I's)"),
    (31, "treasure_items", "u8[2]", "[~] III item ids"),
    (33, "byte_33", "u8", "[?] 1 on undead and demons (Skeleton, Zombie, Ghoul, Wight, Wraith, Vampire, devils, demons), also Constrictor"),
    (34, "gold", "u16be", "[~] equals DOS I gold (34/35); BIG-ENDIAN like the Amiga file it shares"),
    (36, "xp", "u16be", "[~] close to DOS I xp (Skeleton 77 = 77, Viper 731 vs 732); big-endian"),
]


def monsters(raw: bytes) -> list[dict]:
    out = []
    for i in range(80):
        r = raw[i * 38:(i + 1) * 38]
        m = {"index": i, "name": r[:14].split(b"\0")[0].decode("latin-1").rstrip()}
        m.update(group_max=r[14], attack_power_div10=r[15], damage=r[16], armor=r[17],
                 defense=s8(r[18]), level=r[19], special_chance=r[20], specials=list(r[21:24]),
                 combat_rating=r[24], hp_base=r[25], reaction_modifier=s8(r[26]), byte_27=r[27],
                 next_group_chance=r[28], companions=list(r[29:31]),
                 treasure_items=list(r[31:33]), byte_33=r[33],
                 gold=r[34] << 8 | r[35], xp=r[36] << 8 | r[37], raw_hex=r.hex())
        out.append(m)
    return out


def main() -> None:
    data = c.MAIN_EXE.read_bytes()
    img, _, _ = c.unpack_root(data)
    strs = string_table(img)
    ds = c.DS_IMG

    names = {"_source": "MAIN.EXE root far-pointer string table DS:0x1308 [C]; index = position",
             "string_count": len(strs)}
    for k, (a, b) in GROUPS.items():
        names[k] = [{"index": i - a, "string_index": i, "name": strs[i]} for i in range(a, b)]
    c.write_json("strings.json", {"_source": names["_source"],
                                  "strings": [{"index": i, "text": s} for i, s in enumerate(strs)]})

    items = []
    for i in range(181):
        it = {"id": i, "name": strs[ITEM_BASE + i], "kind": item_kind(i),
              "value": struct.unpack_from("<H", img, ds + VALUE + 2 * i)[0]}
        if i <= 100:
            it["points"] = img[ds + POINTS + i]
            it["requirement"] = img[ds + REQ + i]
        items.append(it)
    c.write_json("items.json", {
        "_source": "names: string table 150+id [C]; points DS:0x113c+id, requirement DS:0x11a2+id "
                   "(ids 0..100) [C bytes]; which attribute sum the requirement is compared with "
                   "(STR shields/armour, STR+DEX bows, 2·STR+DEX melee, as the manual) [C] (ovl2 0x39b5, s79); "
                   "value DS:0xf92+2·id [C bytes]; armory build (root 0x0fb8..0x102e) computes "
                   "value · town_price_index[FAR:0xdd98] / 100 as a long for each stocked id [~]; FAR:0xdd98 "
                   "= current town number, 1 = Pendragon (s81) [C]. The table holds 18 u16 (towns 0..17, the "
                   "rows of INITARMS.DAT; entry 0 unused) [C] bytes, extent [~] (s82: 14 Sciattica = 123, "
                   "15-17 = 100; the next bytes are not prices)",
        "town_price_index": [{"town": t, "name": strs[111 + t] if t else None,
                              "index": struct.unpack_from("<H", img, ds + TOWN_PRICE + 2 * t)[0]}
                             for t in range(TOWN_PRICE_COUNT)],
        "items": items})

    spells = [{"number": n, "name": strs[SPELL_BASE + n]} for n in range(1, 58)]
    c.write_json("spells.json", {"_source": "string table 331+n [C]; #53 is an empty string (Vision removed)",
                                 "spells": spells})

    names["transfer_class_map_I_to_III"] = list(img[ds + CLASS_MAP:ds + CLASS_MAP + 6])
    names["transfer_race_map_I_to_III"] = list(img[ds + RACE_MAP:ds + RACE_MAP + 16])
    names["_maps"] = ("DS:0x48 / DS:0x4e [C] (ovl1 0x245 / 0x27b), indexed by DOS I class "
                      "(THIEF..WIZARD) / race (HUMAN..MINOTAUR, 15 = UNDEAD)")
    c.write_json("names.json", names)

    craw = c.read_file("CREATURE.DAT")
    mons = monsters(craw)
    c.write_json("monsters.json", {
        "_source": "CREATURE.DAT, 80 x 38 B + 32 B pad (0x60); identical to Amiga monsinfo.dat",
        "_fields": [{"offset": o, "name": n, "type": t, "note": note} for o, n, t, note in MON_FIELDS],
        "_pad_hex": craw[80 * 38:].hex(),
        "monsters": mons})

    # ---- diff against DOS I --------------------------------------------------------------
    i_dir = c.REPO / "reverse-engineering" / "dos" / "extracted"
    i_items = {x["index"] if "index" in x else x["id"]: x for x in json.loads((i_dir / "items.json").read_text())["items"]}
    i_names = json.loads((i_dir / "names.json").read_text())
    i_mons = json.loads((i_dir / "monsters.json").read_text())["monsters"]
    i_item_names = {k: (v.get("name") or "") for k, v in i_items.items()}
    item_diff = []
    for it in items:
        old = i_item_names.get(it["id"], "")
        if old.lower() != it["name"].lower():
            item_diff.append({"id": it["id"], "I": old, "III": it["name"]})
    i_spells = [s["name"] for s in i_names["spells"]]
    spell_diff = []
    for s in spells:
        old = i_spells[s["number"] - 1] if s["number"] - 1 < len(i_spells) else ""
        if old.replace(" ", "").lower() != s["name"].replace(" ", "").lower():
            spell_diff.append({"number": s["number"], "I": old, "III": s["name"]})
    i_mon_names = {m["name"].lower() for m in i_mons}
    shared = [m["name"] for m in mons if m["name"].lower() in i_mon_names]
    c.write_json("diff-vs-p1.json", {
        "_source": "DOS III extracted tables vs DOS I reverse-engineering/dos/extracted/*.json (names only for items/spells)",
        "classes": {"I": [x["name"] for x in i_names["classes"]],
                    "III": [x["name"] for x in names["classes"]],
                    "I_to_III": names["transfer_class_map_I_to_III"]},
        "races": {"I": [x["name"] for x in i_names["races"]] + ["UNDEAD"],
                  "III": [x["name"] for x in names["races"]],
                  "I_to_III": names["transfer_race_map_I_to_III"]},
        "items_renamed_or_moved": item_diff,
        "items_changed_count": len(item_diff),
        "spells_changed": spell_diff,
        "monsters": {"I_count": len(i_mons), "III_count": len(mons), "shared_names": shared,
                     "III_only": [m["name"] for m in mons if m["name"].lower() not in i_mon_names]},
    })

    amiga = c.REPO / "reverse-engineering" / "amiga" / "phantasie3" / "unpacked" / "PhantasieIII"
    same = {}
    for dos, ami in (("CREATURE.DAT", "monsinfo.dat"), ("INITARMS.DAT", "inititem.dat")):
        a = (amiga / ami).read_bytes() if (amiga / ami).exists() else b""
        same[f"{dos} == amiga {ami}"] = a == c.read_file(dos)
    print("strings", len(strs), "items", len(items), "spells", len(spells), "monsters", len(mons),
          "item names changed vs I", len(item_diff), same,
          "sha1 CREATURE", hashlib.sha1(craw).hexdigest()[:12])


if __name__ == "__main__":
    main()
