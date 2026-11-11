#!/usr/bin/env python3
"""Build src/OpenPhantasie.Data/GameData/phantasie4/ from the Phantasie IV (X68000, Star Craft 1991) files and tables.

Phase 09 T6.3 (03d-phantasie4-re.md). Inputs:
  reverse-engineering/x68000/phantasie4/extracted/   names, items, monsters (DATM raw), elementals (ELMS raw),
                                                    creation tables (TABL + P4.X), hall-initial (FAME), text/strings.json
  reverse-engineering/x68000/phantasie4/unpacked/    disk2/MOUT (outdoor maps), disk2/IND1..21.PDT (dungeon maps),
                                                    disk1/O_001..O_021 (event files), disk2/SCR1..5 (scrolls),
                                                    disk1/P4.X (a few small code tables, read at their image address)
Output: JSON in the shared schemas (GameData/phantasie1..3) where they fit, IV-only fields beside them, plus game.json,
rules.json, rule-tables.json (IV's own tables) and strings/en.json + strings/ja.json (in-game text, s78 translation).

Rules: only what is decoded [C] / [~] (tagged), what the user decided (DIFFERENCES-IV.md) or what the IV manual states
goes in. Undecoded values are null with a "[?]" note. Run from the repo root:

    python3 -I tools/scripts/build_p4_gamedata.py [--check]

--check rebuilds in memory and fails if a file on disk differs.
"""
import functools
import json
import os
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
P4 = os.path.join(ROOT, "reverse-engineering", "x68000", "phantasie4")
EXT = os.path.join(P4, "extracted")
DISK1 = os.path.join(P4, "unpacked", "disk1")
DISK2 = os.path.join(P4, "unpacked", "disk2")
OUT = os.path.join(ROOT, "src", "OpenPhantasie.Data", "GameData", "phantasie4")
GAME = "p4"
SOURCE = "Phantasie IV, X68000 Star Craft 1991 Ver.1.02 (reverse-engineering/x68000/phantasie4/, FORMAT.md)"

CLASSES = ["Thief", "Monk", "Ranger", "Fighter", "Priest", "Wizard", "Shadow Warrior", "Gargoyle", "Sage", "Lord"]
PLAYER_CLASSES = CLASSES[:8]
# IV classes 0-5 are Phantasie I's six in I's order (Core CharacterClass); races 0-14 are I's fifteen in I's order.
CORE_CLASS = ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"]
CORE_RACE = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC",
             "GOBLIN", "LIZ.MAN", "MINOTAUR"]
SKILLS = ["Attack", "Parry", "Spot Item", "Spot Trap", "Disarm", "Listen", "Fire Bow", "Pick Lock", "Swim", "Navigation",
          "Throwing"]
# combat.md section 1: _body order [C]
BODY = ["Head", "Torso", "Right Arm", "Left Arm", "Right Leg", "Left Leg"]
WOUNDS = ["None", "Light", "Serious", "Lost"]
# towns.md section 1 [C]: map number 1-21
TOWNS_EN = ["Pendragon", "Pendleton", "Delta", "Tiris", "Flagler", "Scanport", "Pelnor", "Pippacott", "Almanar",
            "Elisar", "Akihabara", "Lingstroff", "Skull City", "Astral City", "Nether Village", "Styx Harbour",
            "Town of Mist", "Titan Village", "Lava City", "Town of Plutars", "Astral Town"]
# outdoor.md section 1 [C]: MOUT record by outdoor map number 1-24 (table 0x42DD2)
MOUT_RECORD = {1: 0, 2: 1, 3: 2, 4: 3, 5: 4, 6: 5, 7: 5, 8: 6, 9: 7, 10: 8, 11: 5, 12: 5, 13: 9, 14: 10, 15: 11, 16: 12,
               17: 13, 18: 12, 19: 12, 20: 14, 21: 15, 22: 16, 23: 17, 24: 12}
TERRAIN = ["water", "rapids", "desert", "hills", "mountains", "forest", "grassland", "lava", "mist", "River of Hell",
           "deep River of Hell", "Netherworld ground [~]", "Netherworld ground [~]", "shining sea", "town", "inn",
           "dungeon", "pool", "Charon", "open sea"]


def load(*parts):
    with open(os.path.join(*parts), encoding="utf-8") as f:
        return json.load(f)


def binary(*parts):
    with open(os.path.join(*parts), "rb") as f:
        return f.read()


P4X = binary(DISK1, "P4.X")


def image(addr, n):
    """n bytes of P4.X at image address addr (file offset - 0x40, FORMAT.md section 1)."""
    return P4X[addr + 0x40:addr + 0x40 + n]


def longs(addr, n):
    return list(struct.unpack(f">{n}i", image(addr, 4 * n)))


def spell_pages(first, count):
    """_getmagic's pages first.. (table 0x44AC8 {first, last} inclusive into the byte list 0x44AE2, 0-based spells)."""
    bounds = image(0x44AC8 + 2 * first, 2 * count)
    return [[n + 1 for n in image(0x44AE2 + bounds[2 * i], bounds[2 * i + 1] - bounds[2 * i] + 1)] for i in range(count)]


# The table's pages 5-7 are the places' lists written out under services.spell_places.x68000 (cross-check).
assert spell_pages(5, 3) == [[1, 2, 3, 4, 13, 40, 15, 49, 50], [1, 2, 3, 4, 13, 40, 15, 49, 50, 63],
                             [1, 2, 3, 4, 13, 40, 15, 47, 50, 65]], spell_pages(5, 3)


def cid(n):
    return f"{GAME}:{n}"


def lst(xs):
    return [{"index": i, "name": x} for i, x in enumerate(xs)]


def s16(b, o):
    return struct.unpack(">h", b[o:o + 2])[0]


def u32(b, o):
    return struct.unpack(">I", b[o:o + 4])[0]


# ---- text (s78 translation) -------------------------------------------------------------------------------------------

def text_rows():
    rows = load(EXT, "text", "strings.json")["strings"]
    return rows + event_text_rows(rows)


def event_text_rows(rows):
    """Event texts the bytecode prints (PRINT / TITLE / MADD) that the string extractor skipped (short room names, a few
    lines inside other segments; found s93 by the Core interpreter's coverage test). English from translation/en.json
    (keyed by the exact Japanese); a line with no Japanese is the game's own English and stays as it is."""
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import x68k_p4_events as ev
    have = {(r["src"], int(r["off"], 16)) for r in rows if r["status"] != "not-text"}
    tr = load(P4, "translation", "en.json")
    extra, missing = [], []
    for n in range(1, 22):
        src = f"O_{n:03d}"
        buf = binary(DISK1, src)
        _, _, _, ins, texts, _ = ev.disasm(buf)
        for op, args, _ in sorted(ins.values(), key=lambda i: i[1]):
            if op not in (0x09, 0x0A, 0x28) or (src, args[0]) in have:
                continue
            have.add((src, args[0]))
            ja = texts[args[0]]
            if all(ord(c) < 0x80 for c in ja):
                en = ja
            elif ja in tr:
                en = tr[ja]
            else:
                missing.append(ja)
                continue
            extra.append({"src": src, "off": f"0x{args[0]:x}", "ja": ja, "en": en, "status": "translated"})
    if missing:
        sys.exit("event texts with no translation (add them to translation/en.json): " + ", ".join(missing))
    return extra


# ---- monsters ---------------------------------------------------------------------------------------------------------

def monsters(src, item_count):
    out = []
    for m in src["monsters"]:
        # extracted/monsters.json "raw" = the 50 bytes after the name, i.e. record offsets 0x14..0x45 (FORMAT.md 6.4)
        raw = bytes.fromhex(m["raw"].replace(" ", ""))
        rec = bytes(20) + raw
        assert len(rec) == 70

        def item(o):
            v = s16(rec, o)
            return v if 1 <= v < item_count else None  # skeleton ids 0 / > 156 are skipped by _findit [C]
        treasure = [v for v in (item(0x3A), item(0x3C), item(0x3E)) if v is not None]
        out.append({
            "id": cid(m["id"]), "index": m["id"], "name": m["name"],
            "group_max": rec[0x14], "attack_power": s16(rec, 0x16), "damage": s16(rec, 0x18),
            "armor": s16(rec, 0x1A), "defense": s16(rec, 0x1C), "level": s16(rec, 0x1E), "xp": u32(rec, 0x20),
            "magic_resistance": rec[0x24], "hp_base": s16(rec, 0x26), "nice": s16(rec, 0x28), "gold": u32(rec, 0x2A),
            "spell_ability": s16(rec, 0x2E), "spell_ids": [rec[0x30], rec[0x31], rec[0x32]],
            "next_group_chance": s16(rec, 0x34), "companions": [rec[0x36], rec[0x37]], "movement": rec[0x38],
            "treasure_items": treasure, "creature_type": rec[0x40], "flier": rec[0x41] != 0, "body_shape": rec[0x42],
            "damage_type_flag": rec[0x43] != 0, "undead_or_demon": rec[0x44] != 0,
            "special_chance": 0, "specials": [], "combat_rating": 0, "reaction_modifier": 0,
        })
    return {
        "_source": f"{SOURCE}: DATM (disk 2), 84 x 70 B big-endian (FORMAT.md section 6.4)",
        "_fields": "group_max +14 [C] (rand 1..n; n = the party average level _par+0B when +14 > +0B + 1, s98); attack_power +16 [C] (attacks per turn "
                   "max(1, trunc(v / 115 + 0.7)), hit trunc(v / n) - D; combat.md 4.2); damage +18 [C] (trunc(u^0.5 * v) - "
                   "armour); armor +1A [C] (subtracted from party damage); defense +1C [C] (subtracted from the party's "
                   "hit chance); level +1E [~]; xp +20 [C]; magic_resistance +24 [~] (resistance roll, combat.md 9); "
                   "hp_base +26 [C] (per-monster HP: D4-055, rule-tables.json monster_hp); nice +28 [?] (summed into "
                   "_emnice: Greet / Beg chances); gold +2A [C]; spell_ability +2E [~] (cast chance %); spell_ids +30 / +31 "
                   "/ +32 bytes (1-65 spells, 66 / 67 breath, 68 paralysis, 69 plain attack; combat.md 4.1) [C]; "
                   "next_group_chance +34 [C]; companions +36 / +37 = the two possible next-group ids [C]; movement +38 "
                   "[C]; treasure_items +3A / +3C / +3E [C] (outdoor encounters only; ids 0 and > 156 dropped); "
                   "creature_type +40 [?]; flier +41 [~]; body_shape +42 [C] (1 normal, 2 body only, 3 snake, 4 winged); "
                   "damage_type_flag +43 [?] (giants etc., combat.md 3.4 type 5); undead_or_demon +44 [~] (Dispel Undead, "
                   "Dissolve). special_chance / specials / combat_rating / reaction_modifier are Phantasie I fields with "
                   "no IV meaning: 0 / empty.",
        "monsters": out,
    }


# ---- items ------------------------------------------------------------------------------------------------------------

def item_kind(i):
    if i == 0:
        return "none"
    if i <= 0x08:
        return "shield"
    if i <= 0x1B:
        return "armor"
    if i <= 0x3F:
        return "weapon"
    if i <= 0x4B:
        return "bow"
    if i <= 0x54:
        return "thrown"
    if i <= 0x5E:
        return "healing_potion"
    if i <= 0x68:
        return "magic_potion"
    if i <= 0x80:
        return "special_potion"
    if i <= 0x8B:
        return "magic_item"
    if i <= 0x90:
        return "scroll"
    if i == 0x91:
        return "box"
    return "quest_item"


STR_CHECK = {"shield": "str", "armor": "str", "weapon": "str2_plus_dex", "bow": "str_plus_dex", "thrown": "str_plus_dex"}


def items(src):
    out = []
    for it in src["items"]:
        i = it["id"]
        kind = item_kind(i)
        rec = {"id": cid(i), "index": i, "name": "NOTHING" if i == 0 else it["name"], "value_factor": 0,
               "value": it["price"], "kind": kind, "icon": it["b0_icon"]}
        if kind in STR_CHECK:
            rec.update(points=it["value"], min_str=it["b2_requirement"], str_check=STR_CHECK[kind])
        if kind == "armor" and it["b3_move_pct"]:
            rec["move_pct"] = it["b3_move_pct"]
        out.append(rec)
    return {
        "_source": f"{SOURCE}: names _itm, records _idat (6 B BE: icon, value, requirement, move %, u16 price) [C] "
                   "(FORMAT.md section 6.5, extracted/items.json)",
        "_note": "kind by id range [C] (_equip / _use): shield 1-8, armor 9-27, weapon 28-63 (melee), bow 64-75, thrown "
                 "76-84, healing_potion 85-94 (Healing 1-10), magic_potion 95-104 (Magic 1-10), special_potion 105-128, "
                 "magic_item 129-139, scroll 140-144 (item 140 + k shows SCR(k+1), _use_sc 0xfad0 [C]), box 145, "
                 "quest_item 146-156 (keys, gems, pen, emerald, diamond, explosives). points = DEF / ATT (equals the item "
                 "catalogue for every equipment item); min_str = the requirement b2, compared by str_check: str (shields, "
                 "armour: b2 <= Str), str2_plus_dex (melee: b2 <= 2 Str + Dex), str_plus_dex (bows, thrown: b2 <= Str + "
                 "Dex) [C] (_cal_user). value = the base price: shops sell at trunc(value * town price_index / 100) and "
                 "buy at trunc(value / 4) + 1 [C] (towns.md 6). move_pct = armour's signed movement % (b3) [C]. icon = "
                 "b0 [C]. D4-018 (user s81: manual Long Sword 14 points) is a rules option: the record keeps the code's "
                 "13; rule-tables.json item_points_manual holds 14. Item 0 is 'NOTHING' (the table entry is empty).",
        "items": out,
    }


# ---- spells -----------------------------------------------------------------------------------------------------------

# The spell list (phantasie4-pc98-spell-list-jp.pdf; docs/manuals/phantasie4/phantasie4-manual-en.md "Spell list"):
# type per spell number, and its effect line (English column).
SPELL_TYPE = {**{n: "E" for n in (1, 2, 3, 4, 13, 40)}, **{n: "N" for n in (15, 50)}, 47: "N/T", 49: "N/WD",
              **{n: "C/P" for n in (11, 12, 16, 17, 18, 19, 42, 44, 45)}}
SPELL_EFFECT = {
    1: "restores 1-9 HP; heals wounds", 2: "restores 1-27 HP; heals wounds", 3: "restores 1-57 HP; heals wounds",
    4: "restores 1-99 HP; heals wounds", 5: "1-10 damage to one chosen enemy", 6: "1-30 damage", 7: "1-60 damage",
    8: "1-100 damage", 9: "party stride +2", 10: "party stride +5", 11: "enemy stride to 1/2", 12: "enemy stride to 3/4",
    13: "the whole party can fly", 14: "pulls flying enemies to the ground", 15: "identifies an item's nature",
    16: "party armour +1", 17: "party armour +3", 18: "party armour +6", 19: "party armour +10",
    20: "confuses the enemy, costing it chances to cast", 21: "confuses the enemy", 22: "confuses the enemy",
    23: "confuses the enemy", 24: "bow hit chance 50 %, damage +15 (one person)", 25: "bow hit chance 100 %, damage +25",
    26: "bow hit chance 50 %, damage +15 (party)", 27: "bow hit chance 100 %, damage +25 (party)",
    28: "bow hit chance to 250 % (one person)", 29: "bow hit chance to 250 % (party)",
    30: "saps the enemy's strength, lowering its attack", 31: "saps the enemy's strength", 32: "saps the enemy's strength",
    33: "saps the enemy's strength", 34: "1-8 damage to all enemies", 35: "1-18 damage to all enemies",
    36: "1-30 damage to all enemies", 37: "1-44 damage to all enemies", 38: "puts the enemy to sleep",
    39: "moves everyone from the battlefield elsewhere", 40: "revives one dead companion",
    41: "frightens the enemy into fleeing", 42: "summons an elemental (Mud, Mist, Air)",
    43: "dissolves zombies, skeletons etc.", 44: "caster's stride raised to 11; a superhuman warrior",
    45: "caster's stride raised to 14", 46: "wakes sleeping companions", 47: "moves the party to another town",
    48: "slowly dissolves the enemy", 49: "tells the level of the area's enemies", 50: "obtains one item (100 gold)",
    51: "7 damage to friend and foe in front", 52: "15 damage", 53: "24 damage", 54: "35 damage",
    55: "1-18 damage to all friend and foe around", 56: "1-25 damage", 57: "1-35 damage", 58: "1-43 damage",
    59: "party damage +2", 60: "party damage +4", 61: "party damage +7", 62: "party damage +11",
    63: "Divine Aid: a god whispers a hint for the current map (spells-noncombat.md)",
    64: "Annihilate Enemy: not handled in the combat code [?]; casting empties MP (combat.md 9)",
    65: "Super Transport: Transportation to a random town of the group (spells-noncombat.md)",
}
# MP per the manual: the spell list, with the manual pages' own values where they differ (D4-044, user s82: manual).
MANUAL_MP_PAGES = {11: 1, 12: 2, 48: 1}          # manual pp. 51, 53 (the list says 2, 3, 3)
LIST_MP = [1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 2, 3, 3, 2, 1, 1, 2, 3, 4, 1, 2, 3, 4, 1, 3, 2, 4, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 2,
           3, 4, 2, 4, 1, 1, 1, 3, 2, 3, 4, 2, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4]
assert len(LIST_MP) == 62


def spells(names):
    code_mp = list(image(0x44B7C, 65))           # _getmp table by spell number - 1 [C]
    rank = list(image(0x44C59, 66))[1:]          # rank table by spell number (byte 0 unused) [C]
    out = []
    for i, name in enumerate(names):
        n = i + 1
        if n <= 62:
            mp = MANUAL_MP_PAGES.get(n, LIST_MP[i])
            cat = SPELL_TYPE.get(n, "C/A")
        else:
            mp = code_mp[i]                          # manual silent ("????????"): the code stands
            cat = {63: "N/WD", 64: "C/A", 65: "N/T"}[n]
        out.append({"id": cid(i), "index": i, "name": name, "dos_slot": i, "category": cat, "magic_point_cost": mp,
                    "code_magic_point_cost": code_mp[i], "rank": rank[i], "party_wide": name.startswith("*"),
                    "effect_summary": SPELL_EFFECT[n],
                    "effect_coefficients_note": "Combat effects: logic-specs/combat.md 9; outside combat: "
                                                "spells-noncombat.md."})
    return {
        "_schema": "phantasie.spells/v1",
        "_source": f"{SOURCE}: names _magic (spell number = index + 1) [C]; magic_point_cost / category / effect = the "
                   "manual's spell list and pages (docs/manuals/phantasie4/phantasie4-manual-en.md); "
                   "code_magic_point_cost = _getmp table 0x44B7C [C]; rank = table 0x44C59 [C]",
        "_note": "D4-044 (user s82: manual): magic_point_cost is the manual's (Slow 1 / 2 = 1 / 2, Arrow Flame 2 = 3, "
                 "Party Arrow Flame 1 = 2, Dissolve = 1); the code's table (2 / 3, 2, 3, 3) is code_magic_point_cost, the "
                 "X68000 option. #63-65 (Divine Aid, Annihilate Enemy, Super Transport) are the manual's undisclosed "
                 "'????????' spells: cost and type from the code / the manual's section headings [~]. A name starting "
                 "with '*' is the party-wide form [~].",
        "spells": out,
    }


def names(nm, mon, it, sp):
    t = nm["tables"]
    return {
        "_source": f"{SOURCE}: P4.X pointer tables (extracted/names.json) [C]",
        "_note": "IV's own numbering. classes 0-9 (0-5 = Phantasie I's six in I's order; 6 Shadow Warrior, 7 Gargoyle; 8 "
                 "Sage / 9 Lord only for Hall persons). races 0-34 (0-14 = I's fifteen in I's order; 15-17 Hill Giant, "
                 "Gargoyle, Spriggan; 18-32 the mixed races; 33 '?' (Nikademus, the elementals); 34 Duck). Core's enums "
                 "hold only I's members: IV-only classes and races stay raw numbers. body_areas in IV's order (_body: "
                 "head, torso, right arm, left arm, right leg, left leg); body_states = wound levels 0-3 (_hurt holds the "
                 "Japanese wound messages, strings/ja.json). origins = _origin; planes = _plname.",
        "classes": lst(CLASSES),
        "races": lst([x.strip() for x in t["race"]]),
        "spells": [{"index": s["index"], "name": s["name"]} for s in sp],
        "items": [{"index": x["index"], "name": x["name"]} for x in it],
        "monsters": [{"index": m["index"], "name": m["name"]} for m in mon],
        "spell_level_names": [],
        "town_labels": [],
        "skills": lst(SKILLS),
        "social_classes": lst(t["social"]),
        "origins": lst(t["origin"]),
        "planes": lst(t["plname"]),
        "abilities": lst(["Strength", "Intelligence", "Dexterity", "Constitution", "Charisma", "Luck"]),
        "body_areas": lst(BODY),
        "body_states": lst(WOUNDS),
    }


# ---- world ------------------------------------------------------------------------------------------------------------

# _inns (0x3DABC) [C]: name pointers by inn index (0 and 7 are empty strings; 1-6 used by MOUT)
INNS = longs(0x3DABC, 7)


def outdoor_maps():
    mout = binary(DISK2, "MOUT")
    assert len(mout) == 18 * 700
    regions, places = {}, []
    for n in range(1, 25):
        r = MOUT_RECORD[n]
        rec = mout[r * 700:(r + 1) * 700]
        tiles = [list(rec[y * 26:(y + 1) * 26]) for y in range(26)]
        h = rec[0x2A4:]
        header = {
            "towns": [{"town": h[2 * k], "byte_2": h[2 * k + 1]} for k in range(3)],
            "dungeons": [{"location": h[6 + 3 * k], "x": h[7 + 3 * k], "y": h[8 + 3 * k]} for k in range(3)],
            "inns": list(h[16:20]),
            "destination": {"map_kind": h[20], "map": h[21], "x": h[22], "y": h[23]},
        }
        plane = f"map{n}"
        found = {}
        for y in range(26):
            for x in range(26):
                v = tiles[y][x]
                kind, sub = v // 10, v % 10
                key = (kind, sub)
                if kind in (14, 15, 16, 17, 18) and key not in found:
                    found[key] = (x, y)   # each place is a 2 x 2 block; the first (top-left) square is listed
        for (kind, sub), (x, y) in sorted(found.items(), key=lambda kv: (kv[1][1], kv[1][0])):
            if kind == 14 and header["towns"][sub]["town"]:
                t = header["towns"][sub]["town"]
                places.append({"plane": plane, "x": x, "y": y, "type": "town", "number": t, "name": TOWNS_EN[t - 1],
                               "tile": kind * 10 + sub})
            elif kind == 16 and header["dungeons"][sub]["location"]:
                d = header["dungeons"][sub]
                places.append({"plane": plane, "x": x, "y": y, "type": "dungeon", "number": d["location"],
                               "name": f"location {d['location']}", "tile": kind * 10 + sub,
                               "entry": {"x": d["x"], "y": d["y"]}})
            elif kind == 15 and header["inns"][sub]:
                inn = header["inns"][sub]
                places.append({"plane": plane, "x": x, "y": y, "type": "inn", "number": inn,
                               "name": f"inn {inn}", "name_id": "p4x.0x%x" % INNS[inn], "tile": kind * 10 + sub})
            elif kind in (17, 18):
                header.setdefault("specials", []).append(
                    {"type": "pool" if kind == 17 else "charon", "x": x, "y": y})
        regions[f"OUT{n}"] = {
            "region_id": n, "plane": plane, "size": 700, "grid_width": 26, "grid_height": 26, "mout_record": r,
            "tiles": tiles, "features": [], "text_pages": [], "embedded_text": [], "header": header,
        }
    return regions, places


def overworld(dnames, en):
    regions, places = outdoor_maps()
    for p in places:
        if p["type"] == "dungeon":
            p["name"] = dnames[p["number"]]
        elif p["type"] == "inn":
            p["name"] = en[p["name_id"]]
    return {
        "_source": f"{SOURCE}: MOUT (disk 2), 18 records x 700 B; record by map number = table 0x42DD2 [C] "
                   "(logic-specs/outdoor.md section 1)",
        "_note": "One region per outdoor map number 1-24, keyed OUT<n> with region_id = n and plane = 'map<n>' (IV has "
                 "no planes in the III sense; the key lets places name their map). Maps sharing a MOUT record (6, 7, 11, "
                 "12 = rapids; 16, 18, 19, 24 = deep River of Hell) repeat its tiles. tiles[y][x] = the tile byte: "
                 "terrain type = byte / 10, sub-index = byte % 10 [C]; rule-tables.json outdoor_terrain holds the type "
                 "table (names, hours per step, encounter per mille, blocking). header = the record's bytes 0x2A4-0x2BB: "
                 "towns {town id, byte 2 [?]}, dungeons {location = O_0xx, entry x, y}, inns (_inns index), destination "
                 "of this map's pool / Charon {map kind, map, x, y} [C]; specials = the pool / Charon squares. Each town, "
                 "inn, dungeon, pool and Charon is a 2 x 2 block of tiles; places list the top-left square; an inn's name_id is "
                 "its _inns string (0x3DABC) [C]. Map grid: "
                 "maps 1-15 sit 5 wide, maps 16-24 (Netherworld) 3 wide (outdoor.md section 1) [C].",
        "regions": regions,
        "places": places,
    }


@functools.lru_cache(maxsize=None)
def event_file(n):
    b = binary(DISK1, f"O_{n:03d}")
    count = b[0]
    base = b[3] | (b[4] << 8)
    assert base == (count + 1) * 5, n
    triggers = []
    for i in range(1, count + 1):
        e = b[5 * i:5 * i + 5]
        triggers.append({"x": e[0], "y": e[1], "dir": e[2], "address": e[3] | (e[4] << 8)})
    return b, base, triggers


def dungeons(rows, dnames, dungeon_names_en):
    by_file = {}
    for r in rows:
        if r["src"].startswith("O_") and r["status"] != "not-text":
            by_file.setdefault(r["src"], []).append(r)
    levels = {}
    for n in range(1, 22):
        pdt = binary(DISK2, f"IND{n}.PDT")
        assert len(pdt) == 1922
        words = struct.unpack(">961H", pdt)
        floor = bytes(words[y * 31 + x] & 0x3F for y in range(30) for x in range(30))
        ev, base, triggers = event_file(n)
        msgs = {}
        for r in by_file.get(f"O_{n:03d}", []):
            off = int(r["off"], 16)
            if off < base:
                continue
            msgs[str(off - base)] = {"lines": r["en"].split("$"), "options": [], "file_offset": off}
        levels[f"DNG{n}"] = {
            "id": cid(n), "name": dungeon_names_en.get(n) or dnames[n], "name_ja": dnames[n], "size": 1922,
            "grid_width": 30, "grid_height": 30, "raw_hex": floor.hex(), "words_hex": pdt.hex(),
            "embedded_text": [], "messages": msgs,
            "events": {"triggers": triggers, "code_base": base, "code_hex": ev[base:].hex()},
        }
    return {
        "_source": f"{SOURCE}: IND<n>.PDT (disk 2, 31 x 31 big-endian words) + O_0<nn> (disk 1, event file) "
                   "(logic-specs/events.md sections 1-2) [C]",
        "_note": "DNG<n> = location / dungeon map n (1-21, _dungeons; IND0.PDT is on the disk but no map number reads it "
                 "[~]). The shared byte grid (raw_hex, 30 x 30, cell(x, y) = byte 30 y + x) holds each square's FLOOR TYPE "
                 "only (word bits 0-5: 27-30 water, 6 opened chest; events.md 1). words_hex = the whole map as stored: "
                 "31 x 31 big-endian words, word(x, y) at 2 (31 y + x): bits 0-5 floor, 6-10 north wall picture, 11-13 "
                 "west wall, 14-15 [~]; row and column 30 hold the south / east walls of the last row / column. Doors 2 "
                 "and 3 exist only where an event writes them. events.triggers = the event file's entries {x, y, dir (4 "
                 "= any), address} in file order; x = 0x20 runs on map load, x = 0x21 on walking off the edge; "
                 "events.code_hex = the bytecode from the code base (all addresses inside are base-relative, operands "
                 "little-endian; opcodes events.md 4, tools/scripts/x68k_p4_events.py). messages: base-relative text "
                 "address -> its English lines (s78 translation, '$' = line break; strings/ja.json holds the Japanese "
                 "under the same address). Not checked in the running game.",
        "levels": levels,
    }


def scrolls(rows):
    out = []
    for k in range(1, 6):
        lines = [r for r in rows if r["src"] == f"SCR{k}"]
        lines.sort(key=lambda r: r["line"])
        n = max(r["line"] for r in lines) + 1
        text = [""] * n
        for r in lines:
            text[r["line"]] = r["en"]
        out.append({"id": k - 1, "step": 0, "text": "\n".join(text), "lines": text})
    return {
        "_source": f"{SOURCE}: SCR1-SCR5 (disk 2), fixed 35-byte lines, translated (extracted/text/strings.json) [C]",
        "_layout": "id = SCR file - 1; lines = the file's lines (blank where the line is empty); a line '*' ends a page "
                   "(_use_sc shows up to 19 lines per page) [C]",
        "_notes": "item_records: scroll item 140 + k shows SCR(k + 1) (_use_sc builds 'scr' + ('1' + item - 0x8C)) [C]. "
                  "Translations are keyed by the exact Japanese line, so a short line that recurs elsewhere can carry "
                  "another context's English (e.g. SCR1 line 22); checked in game: not yet.",
        "item_records": {str(140 + k): [k] for k in range(5)},
        "scrolls": out,
    }


def towns(town_names_ja):
    twtyps = list(image(0x3F70E, 22))                          # town type by map [C]
    ctab = longs(0x3FB3C, 22)                                  # town price factor by map [C]
    exits = longs(0x3FF20, 3 * 21)                             # _townexit: {map, x, y} by map - 1 [C]
    ships = longs(0x4001C, 2 * 21)                             # _shipexit: {x, y} by map - 1, -1 = none [C]
    out = []
    for n in range(1, 22):
        name = TOWNS_EN[n - 1]
        m, x, y = exits[3 * (n - 1):3 * n]
        sx, sy = ships[2 * (n - 1):2 * n]
        out.append({"id": n, "index": n - 1, "name": name, "name_ja": town_names_ja[n], "price_index": ctab[n],
                    "town_type": twtyps[n], "exit": {"map": m, "x": x, "y": y},
                    "ship": None if sx < 0 else {"x": sx, "y": sy}, "stock": [0] * 157})
    return {
        "_source": f"{SOURCE}: _towns (names), _twtyps 0x3F70E (type), _ctab 0x3FB3C (price factor) [C] "
                   "(logic-specs/towns.md section 1)",
        "_layout": "id = map number _cmapn (1-21); price_index = T[map] in % (80 maps 1-9, 100 maps 10-12, 120 maps "
                   "13-21), used by the shop, training and magic training; town_type 0 = Pendragon (Guild, Mystic, Armory, "
                   "Bank, Inn, Hall of Glory), 1 = the same with a Shipwright instead of the Hall, 2 = no fifth building "
                   "[C]; stock = the saved shop stock ('goods') per item id, all 0 in a new game (_load(0) clears it); each "
                   "town load adds 8 random items from rule-tables.json shop_random_items (towns.md 6.3).",
        "_notes": ["No town has a temple (towns.md 1). exit = where leaving puts the party (_townexit 0x3FF20, int32 "
                   "{outdoor map, x, y} by town - 1, read by _exittown) [C]; ship = the shipwright's mooring square on "
                   "the exit map (_shipexit 0x4001C, int32 {x, y}; -1 = none: Pendragon, Tiris, Nether Village) [C]. "
                   "As shipped: Astral City's and Astral Town's exits are a square of their own town block, Lingstroff's "
                   "and Skull City's are two squares off their blocks, and Skull City's exit and ship squares are "
                   "shining sea (towns.md 2). The outdoor map of each town is also in overworld.json places."],
        "towns": out,
    }


# ---- creation tables (Phantasie I shape, for the shared loader) -------------------------------------------------------

def creation(ct):
    tabl = ct["tabl"]
    i_slots = ["Attack", "Parry", "Spot Item", "Spot Trap", "Disarm", "Listen", "Fire Bow", "Pick Lock", None, "Swim"]
    sk = tabl["skill_growth_by_skill_class_level"]
    skills = {CORE_CLASS[c]: [[sk[s][cl][lv] if s else 0 for s in i_slots] for lv in range(15)]
              for c, cl in enumerate(CLASSES[:6])}
    spell_names = load(EXT, "names.json")["tables"]["magic"]
    levels = {}
    for c, cl in enumerate(CLASSES[:6]):
        row = [0] * 65
        for lv, granted in enumerate(tabl["spells_granted_by_class_level"][cl]):
            for name in granted:
                row[spell_names.index(name)] = lv + 1
        levels[CORE_CLASS[c]] = row
    attrs = ["STR", "INT", "DEX", "CON", "CHA"]
    return {
        "_schema": "phantasie.creation-tables/v1",
        "_source": f"{SOURCE}: TABL (disk 1, 2130 B; extracted/creation-tables.json, FORMAT.md section 6.6) [C]",
        "_note": "The shared loader's Phantasie I shape, filled for IV's classes 0-5 and races 0-14 (I's members in I's "
                 "order) only where the meaning carries over. IV's full tables (8 player classes, 35 races, 11 skills) "
                 "and its rules are in rule-tables.json; Core's IV rules read those, not this file. hp_factors = TABL "
                 "0x000 HP growth values (new max HP = sum of n x rand(3), n = (value x (Con + 5)) >> 4; training.md, "
                 "FUNCTIONS.md _calpow) - not I's dice; mp_ceiling = TABL 0x14A magic level (+36; max MP = trunc(0.75 x "
                 "magic level) at creation) - not I's MP ceiling; skill_coefficients = TABL 0x1C2 growth targets in I's "
                 "slot layout (find_item = Spot Item, slot 6 = Fire Bow; Navigation and Throwing have no I slot); "
                 "spell_levels = the level whose TABL 0x6EA row grants each spell (index = spell number - 1, 65 "
                 "entries, 0 = never); class / race attribute modifiers = TABL 0x122 / 0x078 SIGNED DELTAS added to the "
                 "3-21 roll (ancestry.md 5.1), not I's base values. No lifespan_bands (IV's lifespans: rule-tables.json "
                 "lifespans).",
        "hp_factors": {"layout": "class, level 1..15 (TABL 0x000)",
                       "by_class": {CORE_CLASS[c]: tabl["hp_growth_by_class_level"][cl] for c, cl in enumerate(CLASSES[:6])}},
        "mp_ceiling": {"layout": "class, level 1..15 (TABL 0x14A magic level)",
                       "by_class": {CORE_CLASS[c]: tabl["b36_by_class_level"][cl] for c, cl in enumerate(CLASSES[:6])}},
        "skill_coefficients": {"layout": "by_class_level[class][level - 1][slot] (I's slot layout)",
                               "skill_order": ["attack", "parry", "find_item", "spot_trap", "disarm_trap", "listen",
                                               "fire_bow", "pick_lock", "_unused", "swim"],
                               "by_class_level": skills},
        "spell_levels": {"layout": "class, spell number - 1; value = level that makes it learnable, 0 = never",
                         "by_class": levels},
        "class_attribute_modifiers": {"by_class": {CORE_CLASS[c]: dict(zip(attrs, tabl["class_ability_mod"][cl]))
                                                   for c, cl in enumerate(CLASSES[:6])}},
        "race_attribute_modifiers": {"by_race": {CORE_RACE[r]: {"modifiers": dict(zip(attrs, tabl["race_ability_mod"][rn]))}
                                                 for r, rn in enumerate(list(tabl["race_ability_mod"])[:15])}},
    }


def elementals(src):
    out = []
    for slot, e in enumerate(src["elementals"]):
        r = bytes.fromhex(e["raw"].replace(" ", ""))
        assert len(r) == 190
        out.append({
            "name": e["name"], "intelligence": r[0x30], "attributes": list(r[0x2F:0x35]),
            "mp_ceiling": r[0x37], "magic_max": r[0x37], "magic_cur": r[0x38], "hp_max": r[0x39], "hp_cur": r[0x3A],
            "status": r[0x0A], "attack": r[0x3C], "parry": r[0x3E], "level": r[0x08], "guild_number": slot,
            "race": r[0x19], "birth_race": r[0x19], "class": r[0x18],
            "weapon_damage_b": r[0x6B], "shield_soak": r[0xB6], "armor_soak": r[0xB7],
            "known_spells": [i for i in range(65) if r[0x73 + i] == 1],
            "magic_level": r[0x36], "movement_base": r[0x29], "social": r[0x1A],
            "strategy": list(r[0x2A:0x2F]), "melee_bonus": r[0x3B],
        })
    return {
        "_source": f"{SOURCE}: ELMS (disk 2), 3 x 190 B character records [C] (FORMAT.md 6.2, 6.8)",
        "_notes": "Slot order as on disk: Mud, Mist, Air (Summon El. loads one into party slot 6; _par+14 picks it). "
                  "class 3 = Fighter (Core's FIGHTER), race 33 = '?' (no Core member; raw). attributes = Str, Int, Dex, "
                  "Con, Cha, Luck. attack / parry = skill values +3C / +3E; weapon_damage_b = the melee weapon value +6B; "
                  "shield_soak / armor_soak = shield / armour values +B6 / +B7 (no item is worn; the values are "
                  "stored). known_spells = spell number - 1 (state 1). strategy = the combat strategy bytes +2A..+2E "
                  "(combat.md 8.2); melee_bonus = +3B (added to weapon_damage_b for the melee damage Wm by _set1).",
        "elementals": out,
    }


# ---- IV's own tables --------------------------------------------------------------------------------------------------

def ident_lines(i):
    """_ident_mi's text for item id i (string ids), as the branches at 0x2DA08 pick them."""
    if 0x55 <= i < 0x69:
        return ["p4x.0x45511" if i < 0x5F else "p4x.0x4551c", "p4x.0x45529"]
    if 0x69 <= i < 0x6E or (0x75 <= i < 0x7E and i not in (0x79, 0x7C)):
        return ["p4x.0x45546"]
    if 0x6E <= i < 0x73:
        return ["p4x.0x45564"]
    fixed = {0x14: 0x4557A, 0x19: 0x4557A, 0x73: 0x455B6, 0x74: 0x455B6, 0x79: 0x455CC, 0x7C: 0x455EB, 0x7E: 0x4560F,
             0x7F: 0x45679, 0x82: 0x45679, 0x80: 0x4568F, 0x83: 0x4568F, 0x81: 0x45651, 0x84: 0x456B8, 0x85: 0x456D8,
             0x87: 0x45631, 0x89: 0x45704, 0x91: 0x4571E}
    for k in (0x34, 0x35, 0x3B, 0x3F, 0x4A, 0x4B, 0x53, 0x54):
        fixed[k] = 0x45598
    return ["p4x.0x%x" % fixed.get(i, 0x4573C)]


def rule_tables(ct, nm, hall, spell_list):
    tabl, p4x = ct["tabl"], ct["p4x"]
    t = nm["tables"]
    spell_names = t["magic"]

    def granted(cl):
        return [[spell_names.index(s) + 1 for s in lv] for lv in tabl["spells_granted_by_class_level"][cl]]
    races = [x.strip() for x in t["race"]]
    combi_pairs = {int(k, 16): v for k, v in p4x["combi_pairs"].items()}
    pairs = []
    for key, res in sorted(combi_pairs.items()):
        pairs.append({"a": key >> 4, "b": key & 0x0F, "result": res})
    moverate_race = p4x["moverate_race"]["values"]
    en_ids = set(strings(text_rows(), "en")["strings"])  # empty lines have no id (the ending's blank lines)
    extra = longs(0x443CA, 31)
    return {
        "_schema": "phantasie.p4-rule-tables/v1",
        "_source": f"{SOURCE}: TABL, P4.X tables (addresses per block), the logic specs and DIFFERENCES-IV.md. Data for "
                   "Core's IV rules (src/OpenPhantasie.Core/Phantasie4/); nothing here is a formula.",
        "classes": CLASSES,
        "player_classes": 8,
        "races": races,
        "skills": SKILLS,
        "social_classes": t["social"],
        "creation": {
            "_note": "ancestry.md [C]. race_ability_mod: 34 rows (races 0-33); class_ability_mod: 8 player classes; "
                     "sub_bonus_pseudo_rows: what _sub_bonus subtracts for a Hall person of class 8 (Sage) / 9 (Lord), "
                     "the first bytes of TABL 0x14A (ancestry.md 5.2) [C]. combine_5x5 rows/cols = races 0-4, -1 = none; "
                     "combine_pairs = the nine packed keys (a > b). inheritance_exponent[i] applies when avg >= 18 - 0.5 i; "
                     "new_person_exponent 0.6. random_race_lists (P4.X stores a count, then the races; the count is "
                     "dropped here): 0 = plain normal list, 1 = plain 25 % list, 2 = plain 25 % list when a Hill Giant or "
                     "Gargoyle is in the guild, 3 / 4 = ancestor-1 lists. "
                     "class_mask_by_race: 8 chars, Thief..Gargoyle. stride_race / stride_class = _moverate (D4-012: "
                     "stride_manual_overrides). social: rand(100) for races 0-4 else rand(75); <= 29 Peasant, <= 59 "
                     "Laborer, <= 89 Craftsman, else Noble (D4-010: social_bias_manual).",
            "race_ability_mod": [tabl["race_ability_mod"][r] for r in list(tabl["race_ability_mod"])],
            "class_ability_mod": [tabl["class_ability_mod"][c] for c in PLAYER_CLASSES],
            "sub_bonus_pseudo_rows": {"8": [6, 6, 7, 7, 8], "9": [8, 9, 10, 11, 12]},
            "combine_5x5": p4x["combi_5x5"]["rows"],
            "combine_pairs": pairs,
            "inheritance_exponent": [round(x, 6) for x in p4x["inheritance_exponent"]["values"]],
            "new_person_exponent": p4x["new_ancestor_exponent"],
            "random_race_lists": {k: v[1:1 + v[0]] for k, v in p4x["random_race_lists"].items()},
            "random_race_rare_chance": 26,
            "class_mask_by_race": [p4x["class_mask_by_race"][r] for r in races[:33]],
            "stride_race": moverate_race,
            "stride_class": p4x["moverate_class"]["values"],
            "stride_manual_overrides": {"_note": "D4-012 (user s81: manual pp. 12-16): Halfling 5, Orc 8",
                                        "3": 5, "11": 8},
            "gargoyle_classes_manual": {"_note": "D4-013 (user s81: manual pp. 17-18): a Gargoyle may also be a Thief or "
                                                 "Fighter", "race": 16, "mask": "10010001"},
            "social": {"thresholds": [29, 59, 89], "die_base_races": 100, "die_other_races": 75, "base_races": 5,
                       "hall_super_hero_gives": 7, "hall_dark_lord_gives": 8},
            "social_bias_manual": {"_note": "D4-010 (user s82): Elves and Dwarves roll rand(100) + 10, capped at 100",
                                   "races": [1, 2], "bonus": 10, "cap": 100},
            "luck": {"_note": "rand(6) + 8 = 9..14 [C]", "die": 6, "add": 8},
            "gold_per_social": 256,
            "starting_spells": {"_note": "spell numbers by class (ancestry.md 7 item 7) [C]",
                                "Monk": [5, 28], "Ranger": [1, 5], "Priest": [1, 46], "Wizard": [5, 9], "Gargoyle": [9]},
            "starting_item": {"_note": "everyone except Shadow Warriors carries Darts (79) [C]", "item": 79,
                              "except_class": 6},
            "fliers": {"_note": "record +B8 for Pixie, Sprite, Gargoyle, Sprixie [~]", "races": [5, 6, 16, 24]},
            "divine_aid_spell": 63,
            "nikademus_hall_slot": 2,
            "nikademus_counts_as_race": {"_note": "D4-001 (user s81): for the race combination Nikademus counts as Human",
                                         "race": 0},
            "max_descendants": 99,
        },
        "aging": {
            "_note": "towns.md 2.2, ancestry.md 7 [C]. Age in 1/26 years (+1E). X68000 aging factor F = Luck x factor "
                     "(cap 255): Elf x19, Dwarf / Halfling / Gnome x2.5, Human / Pixie / Sprite x1, races >= 7 / 2.5; "
                     "grown older at trunc(age / 256) >= 0.7 F, dies at >= F; starting age = trunc(260 + 256 F (0.1 + "
                     "(rand(100) - 1) / 4000)). One town entry adds trunc(days / 0x700) to every age.",
            "factor_by_race": {"2": 19.0, "1": 2.5, "3": 2.5, "4": 2.5, "0": 1.0, "5": 1.0, "6": 1.0, "other": 0.4},
            "factor_cap": 255,
            "decline_at": 0.7,
            "days_per_age_unit": 0x700,
            "decline_factor": {"msx2": 0.8, "x68000": 0.0},
        },
        "lifespans_manual": {
            "_note": "D4-033 (user s81: manual p. 30 lifespans; exact values and spread set at build, s83): death age in "
                     "years = base x (0.9 + 0.04 x (Luck - 9)), i.e. +/-10 % over Luck 9..14 (Luck above 14 or below 9 "
                     "keeps the 0.9..1.1 clamp); grown older at 70 % of it (the code's point). Bases from the manual: Elf "
                     "about 2,000, Dwarf and Gnome 200, Human 80, every other race (Halflings, the monster races and the "
                     "mixed races included, as the manual says 'the others') 45, the middle of 40-50, so the spread "
                     "stays inside 40-50.",
            "base_years": {"2": 2000, "1": 200, "4": 200, "0": 80, "other": 45},
            "spread_min": 0.9, "spread_per_luck": 0.04, "luck_min": 9, "luck_max": 14,
            "decline_at": 0.7,
        },
        "hp_growth": {"_note": "TABL 0x000, 8 player classes x 15 levels [C]; D4-019 (user s82): Thief = lowest other "
                               "class at that level - 1, floor 1 (thief_hp_manual)",
                      "by_class": [tabl["hp_growth_by_class_level"][c] for c in PLAYER_CLASSES]},
        "magic_level": {"_note": "TABL 0x14A (+36) [C]", "by_class": [tabl["b36_by_class_level"][c] for c in PLAYER_CLASSES]},
        "skill_growth": {"_note": "TABL 0x1C2 [C]: [skill][class][level 0..14]; _calsk: v = (old + G[min(L, 14)]) / 2 + "
                                  "bonus, min 1, rounded (skills.md 2)",
                         "by_skill": [[tabl["skill_growth_by_skill_class_level"][s][c] for c in PLAYER_CLASSES]
                                      for s in SKILLS]},
        "spells_granted": {"_note": "TABL 0x6EA [C]: [class][level 0..14] = spell numbers made learnable at that level. "
                                    "D4-016 (user s81): Fighters only their first 4 (fighter_spell_limit_manual); D4-015 "
                                    "(user s82): Priests to level 35, levels 31-35 each grant 6 of the missing spells in "
                                    "number order (priest_manual).",
                           "by_class": [granted(c) for c in PLAYER_CLASSES]},
        "fighter_spell_limit_manual": 4,
        "priest_manual": {"max_level": 35, "first_extra_level": 31, "spells_per_level": 6},
        "max_level": {"_note": "_calpow stops at 30; the hall refuses level >= 31 or Score >= 125 [C]", "level": 30,
                      "score_gate": 125},
        "training": {
            "_note": "training.md [C]: need = trunc(1350 (1.8^L - 0.8) / r), r = sum / divisor (L = 0: - 3333); cost = "
                     "trunc(L^2.2 (20 - Cha)^2 T / 100), cap 25,000; level-up gold = social^social x L x 30 into the "
                     "account. Ability weights: Str, Int, Dex, Con, Cha, Luck. Experience over ~1.8^25 is the open "
                     "overflow [?] (computed in double here).",
            "weights": [[1, 0, 2, 0, 0, 0], [1, 1, 1, 0, 0, 0], [1, 1, 0, 0, 1, 0], [2, 0, 1, 0, 0, 0],
                        [1, 2, 0, 0, 0, 0], [0, 2, 0, 0, 0, 1], [0, 1, 1, 0, 0, 1], [1, 0, 1, 1, 0, 0]],
            "divisors": [19.8, 49.5, 52.8, 33.0, 36.3, 42.9, 75.0, 48.0],
            "cost_cap": 25000, "gold_per_level": 30,
            "magic_quota_class_term": [1, 2, 3, 1, 5, 6, 7, 8],
            "magic_price_base": 9000,
        },
        "towns": {"_note": "towns.md [C]", "price_index_by_map": longs(0x3FB3C, 22), "type_by_map": list(image(0x3F70E, 22)),
                  "inn_heal_points": 2, "outdoor_inn_cost": 256, "outdoor_inn_hours": 1000, "ship_cost": 1500,
                  "bank_cap": 65000, "town_inn_days_manual": 14,
                  "magic_training_age_units": 2, "join_from_other_town_age_units": 4,
                  "transport_groups": {"_note": "spells-noncombat.md (_twnt 0x44EE2): caster's town -> group of towns",
                                       "groups": [list(range(1, 13)), list(range(1, 13)) + [17, 18], [14, 17, 18, 21],
                                                  [17, 18]],
                                       "town_group": {**{str(n): 0 for n in range(1, 13)}, "14": 1, "17": 2, "18": 2,
                                                      "21": 3}}},
        "shop_random_items": {"_note": "_gen_itm (32 B) [C]; _igen picks entry rand(2 min(m, 12) + 5) (1-based index "
                                       "into this list), m = the SUM of the party's levels (s102: _igen adds the level "
                                       "bytes)", "items": list(bytes.fromhex(
                                           load(EXT, "items.json")["gen_itm_raw"].replace(" ", "")))},
        "resale_manual": {"_note": "D4-020 (user s82): a sold item is resold at trunc(paid x 1.25), no town factor",
                          "numerator": 5, "denominator": 4},
        "item_points_manual": {"_note": "D4-018 (user s81): Long Sword 14 points", "40": 14},
        "gold_caps": {"_note": "D4-031 (user s81: manual 65,000 on every path; option uncapped). X68000: bank 65,000, "
                               "sell 65,535, loot 999,999,999, level-up into the account 65,535 [C]",
                      "manual": 65000, "x68000_sell": 65535, "x68000_loot": 999999999, "x68000_level_gold": 65535},
        "potions": {"_note": "items.md 3 [C]; D4-035 (user s82: manual Tables 2-3)",
                    "manual_hp": [1, 4, 9, 16, 25, 35, 49, 64, 81, 100],
                    "manual_heal_points": [1, 1, 1, 2, 2, 2, 3, 3, 3, 4],
                    "spoil_above": 90, "spoil_damage_die": 8},
        "combat": {
            "_note": "combat.md [C]",
            "mp_cost_manual": [x["magic_point_cost"] for x in spell_list],
            "mp_cost_code": [x["code_magic_point_cost"] for x in spell_list],
            "crit_threshold_by_item": {"35": 35, "36": 35, "74": 35, "59": 75, "62": 50, "75": 50, "63": 40, "84": 40,
                                       "83": 25},
            "missile_class_bonus": {"bow": longs(0x4476E, 10), "thrown": longs(0x44796, 10)},
            "slash_from": 150, "attack_from": 75,
            "aim_penalty": 15, "lunge_penalty": 30,
            "manual_strikes": {"_note": "D4-042 (user s82: manual p. 43): Attack strikes twice, Slash 3-4 times (n = "
                                        "trunc(Hm / 75) + 1). User s84: lower accuracy for more damage: each strike rolls "
                                        "Hm - hit_penalty to hit (Slash's Hm not divided by n) and Attack does full damage "
                                        "(no -1); Slash does 1-2 less than Attack as the manual says (user s86: -(n - 2)); "
                                        "penalties chosen by Claude at the user's request.",
                               "attack": 2, "hit_penalty": {"attack": 10, "slash": 20}},
            "arrow_flame": {"hit": [50, 100], "damage": [15, 25]},
            "weakness_factor": [0.8, 0.6, 0.4, 0.2],
            "fire_flash_n": [10, 30, 60, 100], "mind_blast_n": [8, 18, 30, 44], "healing_manual_n": [9, 27, 57, 99],
            "annihilate": {"_note": "Remaster effect for spell 64 under p4.annihilate = divine (user s117, D4-071; no "
                                    "original has one): every monster takes M x base_per_mp + rand(M x spread_per_mp) - "
                                    "1, M = the caster's MP at the cast.", "base_per_mp": 2, "spread_per_mp": 2},
            "shock_wave": [7, 15, 24, 35], "flameball_n": [18, 25, 35, 43],
            "flameball_radius_manual": [1.0, 2.0, 2.5, 3.0],
            "flameball_code_squares": list(image(0x44950, 4)),
            "flameball_code_offsets": longs(0x44954, 21),
            "spell_pages_code": {"_note": "s152: _getmagic(member, 0)'s five category pages (FORMAT.md 6.9 'Combat "
                                          "spell list'): table 0x44AC8 {first, last} into 0x44AE2 (0-based), as spell "
                                          "numbers in the page's order [C]",
                                 "pages": spell_pages(0, 5),
                                 "heads": ["p4x.0x%x" % a for a in longs(0x44A98, 6)]},
            "protection": [1, 3, 6, 10], "speed": [2, 5], "strength": [2, 4, 7, 11], "ninja_stride": [11, 14],
            "auto_spells_code": {"defence_random": list(longs(0x44146, 7)), "attack_walk": list(longs(0x44162, 20))},
            "auto_spells_manual": {"_note": "D4-046 (user s82: manual p. 32)",
                                   "attack": [5, 6, 7, 8, 11, 12, 14, 20, 38, 43, 48, 59, 60, 61, 62],
                                   "defence": [1, 2, 3, 4, 9, 10, 13, 16, 17, 18, 19, 40]},
            "monster_hp": {"_note": "D4-055 (user s82: MSX2 by default) [C] FORMAT.md 6.4: msx2 = trunc(b (rand(10) + "
                                    "4) / 10 + 0.5); x68000: roll 1 trunc(0.5 b + 0.5), 2 trunc(1.5 b + 0.5), 3-10 "
                                    "trunc(1.8 b); both capped at trunc(1.8 b)", "cap": 1.8},
            "treasure": {"dungeon_chance": 45, "outdoor_level_add": 18, "dungeon_extra_below": 65,
                         "extra_items": extra},
            "night_attack_chance": 37,
            "battle": {
                "_note": "combat.md 1, 7, 8.1 [C] (s98 static reading): the 7 x 7 field (square = 7 row + column, row 0 at "
                         "the top). _e_terrain (0x26E0C) picks the field by the outdoor terrain type of the party's square "
                         "(tile / 10); a type with no entry (lava, the River of Hell, places) calls no _e_on_* routine "
                         "[C] (never reached: no encounter roll there). Each field: _e_obstacle(kind, count, passable) "
                         "calls, count = rand(die) + add, each on a free square rand(35) + 6 (7-41); passable obstacles "
                         "can be walked over, the others only flown over. Outdoor first monster type (_emons): "
                         "rand(die) - 1 + 12 x area level (_al) + add; then with rand(100) < override_below it becomes "
                         "rand(override_die) - 1 + override_add (mist / Netherworld 49 %, the sea 64 %: Swashbuckler 76, "
                         "else Pirate 75). Location maps: _denc names the type; no obstacles.",
                "fields": {
                    "ship": {"types": [0, 1, 13], "obstacles": [[1, 6, 1, True]],
                             "monster": {"die": 1, "add": 75, "area_level_times": 0, "override_below": 65,
                                         "override_die": 1, "override_add": 76}},
                    "desert": {"types": [2], "obstacles": [[2, 2, 0, True], [3, 4, 2, False], [4, 2, 0, True]],
                               "monster": {"die": 4, "add": 8, "area_level_times": 12}},
                    "hill": {"types": [3, 4], "obstacles": [[5, 2, 0, True], [6, 3, -1, True], [7, 6, 0, False]],
                             "monster": {"die": 10, "add": 2, "area_level_times": 12}},
                    "forest": {"types": [5], "obstacles": [[8, 11, 4, True], [9, 11, 4, False], [6, 5, 0, True]],
                               "monster": {"die": 10, "add": 0, "area_level_times": 12}},
                    "grass": {"types": [6], "obstacles": [[2, 6, -1, True], [6, 6, -1, True]],
                              "monster": {"die": 8, "add": 2, "area_level_times": 12}},
                    "mist": {"types": [8, 12], "obstacles": [[10, 25, -1, False]],
                             "monster": {"die": 12, "add": 60, "area_level_times": 0, "override_below": 50,
                                         "override_die": 2, "override_add": 60}},
                    "nether": {"types": [11], "obstacles": [[5, 5, -1, True], [7, 5, -1, True], [10, 3, -1, False]],
                               "monster": {"die": 12, "add": 60, "area_level_times": 0, "override_below": 50,
                                           "override_die": 2, "override_add": 60}},
                },
                "obstacle_squares": {"die": 35, "add": 6},
                "placement_columns": longs(0x44508, 7),
                "edge_squares": longs(0x446E0, 24),
                "side_exits": [list(p) for p in zip(longs(0x44670, 28)[0::2], longs(0x44670, 28)[1::2])],
                "_exits_note": "_init_edge: outdoors every edge square is an exit; in a location map or a sea fight "
                               "every edge is a wall except one side exit rand(14) - 1 of side_exits [square, edge index].",
                "groups": {"_note": "_enc_mons [C]: up to 3 groups; group size rand(n), n = DATM +14, but n = party "
                                    "+0B (average level) when +14 > +0B + 1; another group follows when rand(100) <= "
                                    "+34, of type companions[rand(2) - 1]. _emlevel / _emnice = the mean of the "
                                    "groups' +1E / +28 (min level 1).", "max": 3},
                "wake_monster_below": 15,
                "monster_flee_below_percent": 30,
                "event_monsters": {"_note": "_monevf 0x20CDC [C] (s110): an encounter whose first DATM type is a key "
                                            "is an event fight (_evmon): its monsters never flee (_b_chkflee), and "
                                            "winning it by end 101 (all killed) or 103 (melted) sets that story flag. "
                                            "72 Lord Castlebaum, 73 Kor, 83 Arathamus.",
                                   "72": 12, "73": 9, "83": 59},
            },
        },
        "outdoor_terrain": {
            "_note": "outdoor.md 2 [C]: by terrain type (tile / 10). hours per step (table 0x42D60); encounter per mille "
                     "(0x42FB2; null = no check); blocked: 1 rapids, 10, 19 open sea always; 9 River of Hell needs "
                     "Charon's Raft (132) and Staff (133); 13 shining sea until story flag 0x15C.",
            "names": TERRAIN,
            "name_ids_by_code_index": ["p4x.0x%x" % a for a in longs(0x42D30, 12)],
            "_name_ids_note": "the name printed on entering a new terrain (_omain, table 0x42D30): index type + 1 for types 0-10, else 0 (an empty string) [C]",
            "hours": [36, 36, 8, 20, 20, 14, 6, 20, 12, 48, 48, 8, 8, 8, 8, 8, 8, 8, 8, 8],
            "encounter_per_mille": [60, 60, 25, 30, 30, 50, 40, None, 60, 0, 0, 60, 60, 0, None, None, None, None,
                                    None, None],
            "blocked": [1, 10, 19], "needs_charon_items": {"type": 9, "items": [132, 133]},
            "area_level_by_map": [4, 3, 2, 1, 1, 0, 0, 2, 0, 1, 0, 0, 2, 2, 2, 5, 5, 5, 5, 6, 6, 8, 7, 5],
            "swim_damage": {"die": 5, "add": 1}, "lava_damage_die": 5,
            "ship_storm_below": 15, "storm_moves": 10,
            "levitation_manual": {"_note": "D4-039 (user s82: manual p. 55): aloft for 5 x the slowest member's stride "
                                           "(steps), wilds and dungeons", "stride_multiple": 5},
            "navigation_manual": {"_note": "D4-040 (user s84 reading of manual p. 21 'do not use below 50 %'): advice "
                                           "with a risk: the best navigator always steers; below drift_below each step "
                                           "also drifts off course on a (drift_below - skill) % roll, besides the code's "
                                           "own drift roll", "drift_below": 50},
            "overview": {"_note": "Display only (s108, FORMAT.md 6.9 'Outdoor map'): the 3 x 3 block of maps the M "
                                  "overview shows (_omaps 0x42ECC, 9 a block, row-major) [C]. The block (_cblock) "
                                  "stays while the party walks across map edges if the new map is in it, else it "
                                  "changes (_omove: block 0 <-> 1 when stepping off its outer column); a jump (pool, "
                                  "Charon, _tele, The Box) sets block_by_map (_mvb 0x43732, by map 0-24) [C]. From "
                                  "netherworld_from on, only the party's own map is shown; every other cell shows "
                                  "hidden_map (_omap_load: entry != _cmapn -> 16) [C].",
                         "blocks": [list(image(0x42ECC + 9 * k, 9)) for k in range(3)],
                         "block_by_map": longs(0x43732, 25), "netherworld_from": 16, "hidden_map": 16},
        },
        "dungeon_monster_level_by_map": [1, 1, 4, 2, 4, 3, 2, 2, 4, 0, 5, 6, 10, 8, 0, 0, 7, 10, 12, 6, 6],
        "magic_bag": {"_note": "spells-noncombat.md (0x44E52) [C]", "cost": 100, "items": longs(0x44E52, 10)},
        "services": {
            "_note": "s102: the town buildings, the guild and the out-of-combat menus (towns.md 3, 6; training.md 4.1, "
                     "4.2, 5; spells-noncombat.md). [C] unless noted.",
            "roster_slots": 10, "party_max": 6,
            "elementals": {"_note": "_party: E cycles Earth / Mist / Air spirit / Do not summon; starts on 3 (_par+14)",
                           "choices": 4, "default": 3},
            "party_strategy": {"_note": "_party_info: A attitude (4), B flight (4); _party resets both to 1 "
                                       "(_par+15E / +15F = story flags 0x3E / 0x3F)", "choices": [4, 4], "reset": [1, 1]},
            "member_strategy": {"_note": "_page2: +2A..+2E = A..E, 0-based; creation 2, 0, 2, 3, 3",
                                "choices": [3, 2, 5, 7, 6], "creation": [2, 0, 2, 3, 3]},
            "mp_growth_percent_per_point": {"_note": "_inn: max MP + 1 if rand(100) <= (magic level - max MP) x 20 "
                                                     "(training.md 4.2)", "value": 20},
            "level_row_wrap": {"_note": "_calpow: learnable-spell row x = L + 1, x mod 15 above 15 (training.md 4.1); "
                                        "row -1 (L = 29) reads the previous class's level-15 row, for Thieves the three "
                                        "TABL bytes before the table (0x6E7)", "period": 15,
                               "thief_row_before_table": list(binary(DISK1, "TABL")[0x6E7:0x6EA])},
            "spell_places": {
                "_note": "_getmagic places 5-7 (spells-noncombat.md; table 0x44AC8 / 0x44AE2) [C]; D4-061 manual pp. "
                         "54-55: Identify / Magic Bag anywhere, Healing / Resurrection anywhere, Transportation and "
                         "#65 town only, Levitation / Monster Evaluation / #63 in the wilds or dungeons",
                "x68000": {"outdoors": [1, 2, 3, 4, 13, 40, 15, 49, 50], "dungeon": [1, 2, 3, 4, 13, 40, 15, 49, 50, 63],
                           "town": [1, 2, 3, 4, 13, 40, 15, 47, 50, 65]},
                "manual": {"outdoors": [1, 2, 3, 4, 13, 40, 15, 49, 50, 63],
                           "dungeon": [1, 2, 3, 4, 13, 40, 15, 49, 50, 63],
                           "town": [1, 2, 3, 4, 40, 15, 47, 50, 65]},
                "targeted": [1, 2, 3, 4, 40]},
            "identify_lines": {"_note": "_ident_mi (0x2DA08) [C]: the Item Appraisal text of each item id 0-156",
                               "by_item": [ident_lines(i) for i in range(157)]},
            "box": {"_note": "_use_box [C]: The Box moves the party to outdoor map 21 (6, 14); a ship the party is "
                             "aboard is left moored", "map": 21, "x": 6, "y": 14,
                    "lines": ["p4x.0x4268a", "p4x.0x426ab", "p4x.0x426c8", "p4x.0x426e9"]},
            "town_name_ids": {"_note": "_towns (0x3DA64) by map number 0-21 [C]",
                              "ids": ["p4x.0x%x" % a for a in longs(0x3DA64, 22)]},
            "magic_hall_page": 13,
            "shop_page": 6,
            "party_info_kinds": {"_note": "_p_item: pack ids by kind, first id of each (shields, armour, weapons, "
                                          "missiles, potions, treasures)", "first_ids": [1, 9, 28, 64, 85, 129]},
            "divine_aid_hints": ["p4x.0x%x" % a for a in longs(0x44E8A, 22)],
            "ending": {"_note": "ending.md 1-2: scores by ending flag f0..f4 (_par+158..15C = story flags 0x38..0x3C); "
                                "175 makes every member Noble (3), 200 / 225 Lord (9); every member: HP / MP full, "
                                "Charisma at least 20, Okay, wounds cleared; Score +09 = ending score + rounded average "
                                "level (byte)", "first_flag": 0x38, "scores": [125, 150, 175, 200, 225],
                       "noble_at": 175, "noble": 3, "lord_from": 200, "lord": 9, "min_charisma": 20,
                       "_messages_note": "_endmsg(n): blocks of lines (table 0x40C00 {first, last}, line ids 0x40C58); "
                                         "_ending's order per ending; _common_proc (200, 225) shows block 7 then, per "
                                         "member, '<origin> of <name> Lord' (0x41450 / 0x41459); block 5 is never shown",
                       "messages": [[i for i in ("p4x.0x%x" % a for a in longs(0x40C58 + 4 * f, l - f + 1))
                                     if i in en_ids]
                                    for f, l in zip(longs(0x40C00, 22)[0::2], longs(0x40C00, 22)[1::2])],
                       "sequences": [[0, 1], [0, 2], [0, 3, 4], [0, 6, 7, 8, 9], [0, 7, 10]], "lord_title_block": 7,
                       "final_lines": ["p4x.0x412c9", "p4x.0x412e7"], "score_ids": ["p4x.0x413d5", "p4x.0x413e4"],
                       "code_ids": ["p4x.0x413ec", "p4x.0x413fb", "p4x.0x41400"],
                       "postcard_lines": ["p4x.0x41300", "p4x.0x4131e", "p4x.0x4133f", "p4x.0x41360", "",
                                          "p4x.0x41374"],
                       "_postcard_note": "s158: _endstory's lines 2-7 of its table 0x40D1C (0x41373 is an empty "
                                         "string: \"\" = a blank row); then _spc_wait (0xAB88), the save question "
                                         "(line 8, save_question) in font 1, _sdbeep, _confirm.",
                       "save_question": "p4x.0x4139f",
                       "of_id": "p4x.0x41450", "lord_id": "p4x.0x41459",
                       "banquet_before_blocks": [4, 7], "cell_before_block": 9,
                       "_pictures_note": "s108 (ending.md 1): _endset draws Pendragon's street with END1 before "
                                         "block 0; _banquet (END2, the banquet) runs before blocks 4 and 7 "
                                         "(_common_proc calls it before 7); _arathamus (END3, Arathamus in his cell) "
                                         "before block 9 [C]."},
        },
        "manual_amounts": {
            "_note": "Rules the user decided for the manual (s82) where the manual gives no number and no platform has "
                     "one; amounts set by the user s84 (DIFFERENCES-IV.md). null = [?]: a question for the user; Core's "
                     "manual path refuses to guess. The ship's life (4000 sea steps) and Teleportation's destination are "
                     "rules options (p4.ship-life, p4.teleport), not amounts.",
            "ship_wreck_chance_per_sea_fight": 5,
            "spot_item_identify_at": 50,
            "examine_search_bonus": 20,
            "flameball_strong_armour_at": 10,
            "beg_items_per_member": 1,
            "dissolve_rounds": 3,
            "identify_chance_base": 50,
            "identify_chance_per_level": 5,
            "monster_eval_wrong_base": 50,
            "monster_eval_wrong_per_level": 5,
            "monster_eval_wrong_min": 10,
            "monster_eval_wrong_by": 1,
        },
        "hall": {"_note": "FAME as shipped, slots 0-2 (extracted/hall-initial.json, FORMAT.md 6.3) [C]; 11 slots",
                 "slots": 11, "persons": [{k: h[k] for k in ("slot", "name", "race", "cls", "level", "social", "origin",
                                                             "stats_str_int_dex_con_cha", "p1_p2_p3_results",
                                                             "descendants", "deeds_line", "alive")}
                                          for h in hall["hall"]]},
    }


# ---- rules and manifest -----------------------------------------------------------------------------------------------

def rules():
    return {
        "_source": "Phase 09 T1.3 rules profile for Phantasie IV (X68000 numbers; the PC-98 manual is the source of "
                   "truth). Only decided or decoded values. IV's training, terrain and encounter rules do not fit the I / "
                   "II blocks; their tables are in rule-tables.json for Core's IV rules.",
        "_gold": "D4-031 (user s81: manual 65,000 for the party and each account on every path; option uncapped).",
        "_guild": "10 guild slots (_gmb, guils = 10 x 190 B) [C]; the party is 6 (+ the elemental in slot 6).",
        "_starting_gold": "0 here: IV's starting gold is social class x 256 into the account (rule-tables.json creation "
                          "gold_per_social), added by IV's creation rule.",
        "_equipment_slots": "_equip [C]: shield 1-8, armour 9-27, melee weapon 28-63, bow 64-75, thrown weapon 76-84.",
        "original_platform": "x68000",
        "game": GAME,
        "party_gold_cap": 65000,
        "bank_gold_cap": 65000,
        "guild_slot_count": 10,
        "guild_max_characters": 10,
        "starting_gold": 0,
        "equipment_slots": [
            {"id": "shield", "first_item": 1, "last_item": 8},
            {"id": "armor", "first_item": 9, "last_item": 27},
            {"id": "weapon", "first_item": 28, "last_item": 63},
            {"id": "bow", "first_item": 64, "last_item": 75},
            {"id": "thrown", "first_item": 76, "last_item": 84},
        ],
        "guild_by_platform": {
            "x68000": {"slots": 10, "max_characters": 10, "_source": "[C] guils = 10 records (FORMAT.md 6.1)"},
        },
    }


def strings(rows, lang):
    out = {}
    for r in rows:
        if r["status"] == "not-text":
            continue
        if r["src"] == "P4.X":
            if r["status"] == "source-english" and not r.get("ref_confirmed"):
                continue
            key = "p4x." + r["off"]
        elif r["src"].startswith("SCR"):
            key = f"scr{r['src'][3:]}.{r['line']}"
        else:
            b = event_file(int(r["src"][2:]))[1]
            key = f"o{int(r['src'][2:]):02d}.{int(r['off'], 16) - b}"
        out[key] = r[lang]
    return {
        "_schema": "phantasie.strings/v1",
        "_note": ("Phantasie IV in-game text (X68000 P4.X, O_001-O_021, SCR1-5), " +
                  ("English: the s78 translation (translation/en.json, merged into extracted/text/strings.json; notes in "
                   "docs/manuals/phantasie4/translation-notes.md), read in the running game s201 (parity/s201-p4-translation.md). " if lang == "en"
                   else "Japanese as in the binary (Shift-JIS decoded). ") +
                  "Ids: p4x.<image address> (relocation-confirmed rows and every Japanese row of the data segment), "
                  "o<map>.<base-relative address> (event text, as the bytecode addresses it; '$' = line break), "
                  "scr<n>.<line>. Built by tools/scripts/build_p4_gamedata.py."),
        "game": "phantasie4",
        "language": lang,
        "strings": out,
    }


def manifest(built):
    return {
        "_schema": "phantasie.game/v1",
        "_source": "Built by tools/scripts/build_p4_gamedata.py from " + SOURCE,
        "_note": "The tables this folder holds and their sizes, read by OpenPhantasie.Data GameTables.Load (09-trilogy.md "
                 "T6). IV has no MESS message files (its location texts are in the event files: dungeons.json messages "
                 "and strings/), so message_areas is 0. rule-tables.json holds IV's own tables (creation and ancestry, "
                 "training, towns, combat, outdoor, Hall). No campaign yet.",
        "game": GAME,
        "platform": "x68000",
        "counts": {
            "monsters": len(built["monsters.json"]["monsters"]),
            "items": len(built["items.json"]["items"]),
            "spells": len(built["spells.json"]["spells"]),
            "classes": len(built["names.json"]["classes"]),
            "races": len(built["names.json"]["races"]),
            "overworld_regions": len(built["overworld.json"]["regions"]),
            "dungeon_levels": len(built["dungeons.json"]["levels"]),
            "message_areas": 0,
            "scrolls": len(built["scrolls.json"]["scrolls"]),
            "towns": len(built["towns.json"]["towns"]),
            "elementals": len(built["elementals.json"]["elementals"]),
        },
        "spell_cost_rule": None,
        "_spell_cost_rule": "Costs per spell from the manual (D4-044); the code's are code_magic_point_cost.",
    }


def build():
    nm = load(EXT, "names.json")
    rows = text_rows()
    ja_to_en = {r["ja"]: r["en"] for r in rows}
    dnames = nm["tables"]["dungeons"]
    dungeon_names_en = {n: ja_to_en.get(dnames[n]) for n in range(1, 22)}
    built = {
        "monsters.json": monsters(load(EXT, "monsters.json"), 157),
        "items.json": items(load(EXT, "items.json")),
        "spells.json": spells(nm["tables"]["magic"]),
    }
    built["names.json"] = names(nm, built["monsters.json"]["monsters"], built["items.json"]["items"],
                                built["spells.json"]["spells"])
    dn_en = [""] + [dungeon_names_en[n] or dnames[n] for n in range(1, 22)]
    built.update({
        "overworld.json": overworld(dn_en, strings(rows, "en")["strings"]),
        "dungeons.json": dungeons(rows, dnames, dungeon_names_en),
        "scrolls.json": scrolls(rows),
        "towns.json": towns(nm["tables"]["towns"]),
        "creation-tables.json": creation(load(EXT, "creation-tables.json")),
        "elementals.json": elementals(load(EXT, "elementals.json")),
        "rule-tables.json": rule_tables(load(EXT, "creation-tables.json"), nm, load(EXT, "hall-initial.json"),
                                        built["spells.json"]["spells"]),
        "rules.json": rules(),
        os.path.join("strings", "en.json"): strings(rows, "en"),
        os.path.join("strings", "ja.json"): strings(rows, "ja"),
    })
    built["game.json"] = manifest(built)
    return built


def main():
    check = "--check" in sys.argv[1:]
    built = build()
    os.makedirs(os.path.join(OUT, "strings"), exist_ok=True)
    bad = []
    for name, data in built.items():
        text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
        path = os.path.join(OUT, name)
        if check:
            if not os.path.exists(path) or open(path, encoding="utf-8").read() != text:
                bad.append(name)
        else:
            with open(path, "w", encoding="utf-8") as f:
                f.write(text)
            print(f"{path} ({len(text)} B)")
    if bad:
        sys.exit("out of date: " + ", ".join(bad))


if __name__ == "__main__":
    main()
