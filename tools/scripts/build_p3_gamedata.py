#!/usr/bin/env python3
"""Build src/OpenPhantasie.Data/GameData/phantasie3/ from the Phantasie III (DOS, SSI 1988) extracted tables.

Phase 09 T4 (03c-phantasie3-re.md). Input: reverse-engineering/dos/phantasie3/extracted/ (written by
extract_dos_p3_tables.py and extract_dos_p3_{world,towns,scrolls,dungeons,rules}.py). Output: JSON in the shared schemas
(GameData/phantasie1, phantasie2) where they fit, III-only fields beside them, plus game.json, rules.json and
rule-tables.json (III's own tables: skills, body areas, training curves, class kit, social classes …).

Rules: only what is decoded [C] / [~] (tagged), what the user decided (DIFFERENCES-III.md) or what the III manual states
goes in. Undecoded values are left out or null with a "[?]" note. Values with I semantics (I's value_factor, I's group
size factor, I's training / terrain / encounter blocks) are not filled with III numbers. Run from the repo root:

    python3 -I tools/scripts/build_p3_gamedata.py [--check]

--check rebuilds in memory and fails if a file on disk differs.
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "reverse-engineering", "dos", "phantasie3", "extracted")
P1 = os.path.join(ROOT, "src", "OpenPhantasie.Data", "GameData", "phantasie1")
OUT = os.path.join(ROOT, "src", "OpenPhantasie.Data", "GameData", "phantasie3")
GAME = "p3"
SOURCE = "Phantasie III, DOS SSI 1988 (reverse-engineering/dos/phantasie3/extracted/, FORMAT.md)"

III_CLASSES = ["Fighter", "Thief", "Priest", "Ranger", "Monk", "Wizard"]
# Core's CharacterClass names (Phantasie I order) for III's classes; III class k = I class with the same name
# (transfer map DS:0x48 [C]).
CORE_CLASS = {"Fighter": "FIGHTER", "Thief": "THIEF", "Priest": "PRIEST", "Ranger": "RANGER", "Monk": "MONK",
              "Wizard": "WIZARD"}
CORE_CLASS_INDEX = {"THIEF": 0, "MONK": 1, "RANGER": 2, "FIGHTER": 3, "PRIEST": 4, "WIZARD": 5}
BODY_AREAS = ["head", "left_arm", "right_arm", "torso", "left_leg", "right_leg"]
BODY_STATES = ["okay", "injured", "broken", "gone"]
PLANE_KEYS = {0: "scandor", 1: "light", 2: "dark", 4: "netherworld"}


def load(*parts):
    with open(os.path.join(*parts), encoding="utf-8") as f:
        return json.load(f)


def cid(n):
    return f"{GAME}:{n}"


def lst(xs):
    return [{"index": i, "name": x} for i, x in enumerate(xs)]


# ---- monsters ---------------------------------------------------------------------------------------------------------

def monsters(src):
    out = []
    for m in src["monsters"]:
        out.append({
            "id": cid(m["index"]), "index": m["index"], "name": m["name"],
            "armor": m["armor"], "damage": m["damage"], "level": m["level"], "xp": m["xp"],
            "special_chance": m["special_chance"], "specials": m["specials"], "hp_base": m["hp_base"],
            "combat_rating": m["combat_rating"], "companions": m["companions"],
            "reaction_modifier": m["reaction_modifier"], "next_group_chance": m["next_group_chance"],
            "defense": m["defense"], "gold": m["gold"], "treasure_items": m["treasure_items"],
            "attack_power": m["attack_power_div10"] * 10,
            "group_max": m["group_max"], "byte_27": m["byte_27"], "byte_33": m["byte_33"],
        })
    return {
        "_source": f"{SOURCE}: CREATURE.DAT, 80 x 38 B (FORMAT.md section 4.4; = Amiga monsinfo.dat)",
        "_fields": "Group build fill_group root 0xe3e4 (logic-specs/encounters.md section 1) [C]: group_max = byte 14, the "
                   "group count is rnd(group_max) (not I's group_size_factor, which is left out); attack_power = byte 15 "
                   "* 10; damage 16, armor 17, defense 18 (s8), level 19, special_chance 20, specials 21-23, "
                   "combat_rating 24, hp_base 25 (raw: values > 200 mean 100 * b - 20000 HP), reaction_modifier 26 (s8; "
                   "the manual's reputation is its sign [~]), byte_27 (s8 > 41: this group alone, at most 2 monsters, "
                   "a large picture [~]), next_group_chance 28, companions 29-30, treasure_items 31-32 (III item ids), "
                   "byte_33 (1 on undead / demons and Constrictor; copied to the group +0x2e, purpose [?]), gold 34-35 "
                   "and xp 36-37 big-endian raw (values over 64,000 mean (v - 64,000) * 1000, encounters.md section 4). "
                   "No undead list or never-flees list: III has no decision on either (D3-024's final bosses 77-79 "
                   "are in rule-tables.json).",
        "monsters": out,
    }


# ---- items ------------------------------------------------------------------------------------------------------------

def items(src):
    check = {"shield": "str", "armor": "str", "bow": "str_plus_dex", "weapon": "str2_plus_dex"}
    out = []
    for it in src["items"]:
        i = it["id"]
        rec = {"id": cid(i), "index": i, "name": "NOTHING" if i == 0 else it["name"],
               "value_factor": 0, "value": it["value"], "kind": it["kind"]}
        if 1 <= i <= 100:
            rec.update(points=it["points"], min_str=it["requirement"], str_check=check[it["kind"]])
        out.append(rec)
    return {
        "_source": f"{SOURCE}: names = master string 150 + id; points DS:0x113c, requirement DS:0x11a2 (ids 0-100), "
                   "value DS:0xf92 (u16) [C] (FORMAT.md section 4.1)",
        "_note": "D3-005 (user s79: DOS names): 'Dagger +1/+2', 'Compound Bow +2', 'God Armour', 'Halberd' as the DOS "
                 "string table spells them. Every point and requirement value equals the manual's tables (III PDF "
                 "11-12). kind: shield 1-20, armor 21-40, bow 41-60, weapon 61-100 (melee), healing_potion 101-110, "
                 "magic_potion 111-120, scroll 121-140, treasure 141-180 (180 = Wand of Nikademus). min_str = the "
                 "attribute requirement, compared with str_check: str (shields, armour), str_plus_dex (bows), "
                 "str2_plus_dex (melee) [C] (ovl2 0x39b5). value_factor is Phantasie I's V (price V*V*index/500) and "
                 "is 0 here: III's price is value * town price_index / 100 (armory root 0x0fb8) [~]. Item 0 is "
                 "'NOTHING' (the DOS string is '0').",
        "items": out,
    }


# ---- spells -----------------------------------------------------------------------------------------------------------

# III manual spell table (III PDF 10 / pr.15, read on the page render s82): type codes; costs = 1-4 in the
# Magic Pts column (= (n - 1) % 4 + 1 for every listed spell, #54 Transportation 2).
MANUAL_TYPE = {**{n: "E" for n in (1, 2, 3, 4, 44)}, **{n: "C/A" for n in range(5, 9)},
               **{n: "C/P" for n in list(range(9, 21)) + [45, 48, 50, 55, 56]},
               **{n: "C/A" for n in list(range(21, 44)) + [46, 47, 49, 51]}, 52: "N/WD", 54: "N/T"}


# III's own spells decoded since the first build (number -> (effect_summary, effect_coefficients_note)).
DECODED_SPELLS = {
    55: ("The caster's Fire Bow percentage is 250 for the rest of the fight.",
         "[C] root 0xca34 (combat_spell_29_56): bow % word FAR:0xdd9c + 2*member = 250; damage word untouched (an "
         "Arrow Flame's damage stays). Matches the manual (bows.md section 5). Cost 3 = slot 54 % 4 + 1."),
    56: ("Every party member's Fire Bow percentage is 250 for the rest of the fight.",
         "[C] root 0xca46: the same 250 for every party slot (0 .. party count - 1, whatever the status). Matches the "
         "manual (bows.md section 5). Cost 4 = slot 55 % 4 + 1."),
}


def spells(src, p1_spells):
    p1 = {s["name"].replace(" ", "").upper(): s for s in p1_spells}
    out = []
    for s in src["spells"]:
        n, name = s["number"], s["name"]
        rec = {"id": cid(n - 1), "index": n - 1, "name": name, "dos_slot": n - 1}
        if n == 53:
            rec.update(category="removed", magic_point_cost=None,
                       effect_summary="Empty slot: Vision (#53) was removed (manual III PDF 15 / pr.24: 'All areas are "
                                      "already mapped').")
        elif n == 57:
            rec.update(category="divine", magic_point_cost=None,
                       effect_summary="Divine Aid: III's only divine spell; learned outside the Guild, held in record "
                                      "+0xf0 (FORMAT.md section 5). Effect and cost not decoded [?].")
        else:
            cost = (n - 1) % 4 + 1
            rec.update(category=MANUAL_TYPE[n], magic_point_cost=cost)
            same = p1.get(name.replace(" ", "").replace(".", "").upper()) or p1.get(name.replace(" ", "").upper())
            if same and same["category"] == MANUAL_TYPE[n]:
                rec.update(effect_summary=same.get("effect_summary"),
                           effect_coefficients_note="Same spell, type and cost as Phantasie I per the manual "
                                                    "(09-trilogy-manual-p3.md 1d); III's spell code not compared [?].")
            elif n in DECODED_SPELLS:
                rec.update(zip(("effect_summary", "effect_coefficients_note"), DECODED_SPELLS[n]))
            else:
                rec.update(effect_summary=None,
                           effect_coefficients_note="New or changed in III (manual III PDF 10-11): effect not decoded [?].")
        out.append(rec)
    return {
        "_schema": "phantasie.spells/v1",
        "_source": f"{SOURCE}: names = master string 331 + n [C]; type and cost = the manual's spell table (III PDF 10 / "
                   "pr.15) [C] (render)",
        "_note": "index = spell number - 1 (as Phantasie I for #1-52 and #54). #53 is the DOS table's empty slot, kept so "
                 "positions line up; #57 Divine Aid's cost is not in the manual and not decoded [?]. #13-16 Arrow "
                 "Flame / Party Arrow Flame replace I's Strength; #55 / #56 Accuracy / Party Accuracy are new.",
        "spells": out,
    }


def names(nm, mon, it, sp):
    strip = [x["name"].strip() for x in nm["races"]]
    return {
        "_source": f"{SOURCE}: MAIN.EXE master string table DS:0x1308 (FORMAT.md section 3) [C]",
        "_note": "III's own numbering: classes Fighter 0 .. Wizard 5; races Human 0 .. Troll 15 with 5 = the 'Random' "
                 "menu choice (rnd(10) + 5 -> 6..15), Elemental 16, Undead 17; status Dead 0, Okay 1, Asleep 2, Out 3, "
                 "Down 4. Core's enums keep Phantasie I's order: rule-tables.json core_class / core_race map them by "
                 "name. origins = record +0x58 values (master strings 129 + n). body_areas = record +0xf2 + 2k order "
                 "(string 456).",
        "classes": lst([x["name"].strip() for x in nm["classes"]]),
        "races": lst(strip),
        "status": lst([x["name"] for x in nm["status"]]),
        "spells": [{"index": s["index"], "name": s["name"]} for s in sp],
        "items": [{"index": x["index"], "name": x["name"]} for x in it],
        "monsters": [{"index": m["index"], "name": m["name"]} for m in mon],
        "spell_level_names": [],
        "town_labels": [],
        "skills": lst([x["name"] for x in nm["skills"]]),
        "social_classes": lst([x["name"] for x in nm["social_classes"]]),
        "origins": lst([x["name"] for x in nm["origins"][3:]]),
        "body_areas": lst(["Head", "Left Arm", "Right Arm", "Torso", "Left Leg", "Right Leg"]),
        "body_states": lst([x["name"] for x in nm["body_states"]]),
    }


# ---- world ------------------------------------------------------------------------------------------------------------

def overworld(w):
    regions = {}
    for p in w["planes"]:
        regions[f"PLANE{p['plane']}"] = {
            "region_id": p["plane"], "plane": p["key"], "size": p["width"] * p["height"],
            "grid_width": p["width"], "grid_height": p["height"], "source": p["source"],
            "arrival": p["arrival"], "tiles": p["tiles"], "features": [], "text_pages": [], "embedded_text": [],
        }
    return {
        "_source": f"{SOURCE}: {w['_source']}",
        "_note": "One region per plane, keyed PLANE<n> with region_id = the game's plane number (0 Scandor, 1 Light, "
                 "2 Dark, 4 Netherworld; logic-specs/planes.md section 1-2). tiles[y][x]. III has no feature records: "
                 "its places are hard-coded squares (places below). arrival = the square a party lands on when it "
                 "comes from another plane. Terrain: tile ranges below (Phantasie I's tile_rules do not apply).",
        "regions": regions,
        "start": w["start"],
        "places": [{k: p[k] for k in ("plane", "x", "y", "type", "number", "name", "tile")} for p in w["places"]],
        "terrain": w["terrain"],
    }


def dungeons(dg):
    levels = {}
    for d in dg["dungeons"]:
        msgs = {}
        for t in d["texts"]:
            txt = t["text"]
            lines = [txt[i:i + 38].rstrip() for i in range(0, len(txt), 38)]
            while len(lines) > 1 and not lines[-1]:
                lines.pop()
            lines = lines or [""]
            msgs[str(t["index"])] = {"lines": lines, "options": []}
        levels[f"DNG{d['dungeon']}"] = {
            "id": cid(d["dungeon"]), "name": d["name"], "size": len(d["raw_hex"]) // 2,
            "grid_width": d["width"], "grid_height": d["height"], "raw_hex": d["raw_hex"],
            "embedded_text": [], "messages": msgs,
            "tables": {"level": d["level"], "forced_fights": d["forced_fights"], "exits": d["exits"],
                       "event_squares": d["event_squares"], "event_program_hex": d["event_program_hex"],
                       "text_slots": d["text_slots"]},
        }
    return {
        "_source": f"{SOURCE}: {dg['_source']}",
        "_note": "DNG<n> = dungeon n = DNGS.DAT record letter - 'A' (letters J H E D A C B K I F G, FORMAT.md 9.5). Grid 24 x 30 (cell(x, y) = raw byte 24y + x). messages: "
                 "text slot index -> the text (pointer table at 0x593, text at pointer - 0x800), cut into 38-character "
                 "lines [~] (the dungeon message width is not traced; 38 is the scroll window's). Which slot an event "
                 "square's message byte names (message - 1 or message) is [?]. tables: level (0-5, the monster band "
                 "12L), forced_fights (cells 0x80-0x89), exits (cells 0xd7-0xd9; destinations 29 / 31 / 41 = Light / "
                 "Dark / Netherworld [C], others [?]), event_squares (cells 0xdd-0xff), event programs raw (op types "
                 "1-4 traps, 5 fight; the rest [?]).",
        "_cells": dg["_cells"],
        "levels": levels,
    }


def scrolls(sc):
    return {
        "_source": f"{SOURCE}: {sc['_source']}",
        "_layout": "id = SCROLLS.DAT record (0-18); lines = 38-character slices [~]",
        "_notes": sc["_notes"],
        "item_records": {str(i["item"]): i["records"] for i in sc["items"]},
        "scrolls": [{"id": r["record"], "step": 0, "text": r["text"], "lines": r["lines"]} for r in sc["records"]],
    }


def towns(tw):
    def plane(n):
        return "scandor" if n <= 14 else {15: "light", 16: "dark", 17: "netherworld"}[n]
    out = []
    for t in tw["towns"]:
        if t["town"] == 0:
            continue
        out.append({"id": t["town"], "index": t["town"] - 1, "name": t["name"], "price_index": t["price_index"],
                    "stock": t["stock"], "plane": plane(t["town"])})
    return {
        "_source": f"{SOURCE}: {tw['_source']}",
        "_layout": tw["_layout"],
        "_notes": ["id = the town number (FAR:0xdd98; 1 Pendragon .. 14 Sciattica, 15 Light, 16 Dark, 17 Death). The "
                   "INITARMS row 0 is unused and left out.",
                   "plane: the plane the town is on (root 0x1e18 picks the map by town: 15 Light, 16 Dark, 17 "
                   "Netherworld) [C] (planes.md section 2). III has no TRANSPORTATION list and no restock list "
                   "(restocking [?]); the town square is in overworld.json places.",
                   "Price: value * price_index / 100 (armory root 0x0f22, FORMAT.md section 4.1) [~]; training gold "
                   "uses the same index (training.md section 3)."],
        "towns": out,
    }


# ---- creation tables --------------------------------------------------------------------------------------------------

def creation(rt, p1ct):
    def by_core(table):
        return {CORE_CLASS[k]: v for k, v in table.items()}
    # Phantasie I layout: [class][level 0..14][slot], slot order attack, parry, find_item, spot_trap, disarm_trap,
    # listen, _unused_42, pick_lock, _unused_46, swim. III keeps the same eight class tables; slot 6 (III's Fire Bow
    # record slot) has no class table and stays 0, slot 8 is unused.
    order = ["attack", "parry", "find_item", "find_trap", "disarm_trap", "listen", None, "pick_lock", None, "swim"]
    sk = rt["skill_tables"]["by_skill"]
    skills = {CORE_CLASS[cl]: [[sk[s][cl][lv] if s else 0 for s in order] for lv in range(15)] for cl in III_CLASSES}
    return {
        "_schema": "phantasie.creation-tables/v1",
        "_source": f"{SOURCE}: MAIN.EXE DGROUP tables (extracted/rule-tables.json, tools/scripts/extract_dos_p3_rules.py) "
                   "and the III manual",
        "_note": "Keys are Core's class / race names (Phantasie I order). hp_factors = III's HP dice for levels 1-15 "
                 "(DS:0xa78) [C] (L16-22 in rule-tables.json); mp_ceiling = DS:0xafb [C], cell for cell the manual's "
                 "Maximum Magic Points table (III PDF 10 / pr.14); skill_coefficients = III's eight class tables in I's "
                 "slot layout [C]; spell_levels = DS:0x1208 [C] bytes, = the manual's Spells Available table cell for "
                 "cell (index = spell number - 1, 57 entries). class / race attribute modifiers and ceilings: the III "
                 "manual's tables (III PDF 4 / pr.3), which are Phantasie I's manual tables value for value "
                 "(09-trilogy-manual-p3.md 1a), copied from GameData/phantasie1 (manual values, D-001 applied); DOS "
                 "III's own tables are static data in MAIN.EXE [C] (s88, FORMAT.md section 4.3) and match these except "
                 "Kobold CHA max (DOS 3, manual 13: D3-034). No "
                 "lifespan_bands: III's maximum age is float maths not decoded [~] (FORMAT.md section 5 +0x2a), and "
                 "I's D-003 bands are a Phantasie I decision.",
        "_skill_tables_vs_I": "III's skill tables equal Phantasie I's TWNS.INT tables except five cells, kept as read: "
                              "Thief Pick Lock L3 35 (I 55), Ranger Attack L15 25 (I 250), Ranger Parry L15 60 (I 85), "
                              "and Monk / Wizard Parry read one level ahead of I's rows (III's Parry table is one byte "
                              "short after Ranger L14, so Monk L15 = 40 and Wizard L15 = 0) [C] bytes.",
        "hp_factors": {"layout": "class, level 1..15 (DS:0xa78 + 22*class + level - 1)",
                       "by_class": by_core({k: v[:15] for k, v in rt["hp_dice"]["by_class"].items()})},
        "mp_ceiling": {"layout": "class, level 1..15 (DS:0xafb + 15*class + level)",
                       "by_class": by_core(rt["magic_ceiling"]["by_class"])},
        "skill_coefficients": {"layout": "by_class_level[class][level - 1][slot] (I's slot layout)",
                               "skill_order": ["attack", "parry", "find_item", "spot_trap", "disarm_trap", "listen",
                                               "_fire_bow_no_table", "pick_lock", "_unused", "swim"],
                               "by_class_level": skills},
        "spell_levels": {"layout": "class, spell number - 1; value = level that makes it learnable, 0 = never",
                         "by_class": by_core(rt["spell_levels"]["by_class"])},
        "class_attribute_modifiers": {"by_class": p1ct["class_attribute_modifiers"]["by_class"]},
        "race_attribute_modifiers": {"by_race": p1ct["race_attribute_modifiers"]["by_race"]},
    }


def elementals(rt):
    # logic-specs/elementals.md section 1 [C] (ovl2 0x3f04): Air, Mist, Mud in roster slots 0-2; race 16 Elemental,
    # class 0 Fighter, level 15, status Okay, all six attributes 25, kit shield 4 / armour 22 / weapon 64 / bow 41.
    rows = [("Air", 30, 150, 150, 32, rt["air_elemental_spells"]["values"]),
            ("Mist", 20, 200, 210, 42, list(range(25, 33))),
            ("Mud", 12, 250, 255, 38, [])]
    out = []
    for slot, (name, mp, hp, atk, parry, spells_) in enumerate(rows):
        out.append({"name": name, "intelligence": 25, "attributes": [25] * 6, "mp_ceiling": mp, "magic_max": mp,
                    "magic_cur": mp, "hp_max": hp, "hp_cur": hp, "status": 1, "attack": atk, "parry": parry,
                    "level": 15, "guild_number": slot, "race": 16, "birth_race": 16,
                    "class": CORE_CLASS_INDEX["FIGHTER"],
                    "worn": {"shield": 4, "armor": 22, "bow": 41, "weapon": 64},
                    "known_spells": [n - 1 for n in spells_]})
    return {
        "_source": f"{SOURCE}: ovl2 0x3f04 (logic-specs/elementals.md section 1) [C] static; Air's spell list DS:0x1bb2 "
                   "[C] bytes",
        "_notes": "class = Core's FIGHTER (3; III's own number is 0). race / birth_race 16 = III's Elemental (no Core "
                  "enum member). attributes = STR, INT, DEX, CON, CHA, LCK. worn = the class kit, worn, carried count 0. "
                  "known_spells = spell number - 1. guild_number = DOS roster slot (0-2; the manual lists them last). "
                  "Other skills 0. In III the elemental needs one party member (not six) and does not count as one.",
        "elementals": out,
    }


# ---- III's own tables -------------------------------------------------------------------------------------------------

def skill_table_fixes(rt, p1ct):
    """D3-032 (user s82): the rows where III's tables differ from Phantasie I's, with I's values."""
    slot = {"attack": 0, "parry": 1, "find_item": 2, "find_trap": 3, "disarm_trap": 4, "listen": 5, "pick_lock": 7,
            "swim": 9}
    p1 = p1ct["skill_coefficients"]["by_class_level"]
    out = {}
    for skill, rows in rt["skill_tables"]["by_skill"].items():
        for cl in III_CLASSES:
            mine = [p1[CORE_CLASS[cl]][lv][slot[skill]] for lv in range(15)]
            if mine != rows[cl]:
                out.setdefault(skill, {})[cl] = mine
    return out


def rule_tables(rt, nm, p1ct):
    # I -> III transfer maps (DS:0x48 / DS:0x4e) [C]; inverted: III race -> Core race name.
    core_races = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL",
                  "KOBOLD", "ORC", "GOBLIN", "LIZARDMAN", "MINOTAUR", "UNDEAD"]
    race_map = nm["transfer_race_map_I_to_III"]
    core_race = {str(r): core_races[race_map.index(r)] for r in range(18) if r in race_map}
    return {
        "_schema": "phantasie.p3-rule-tables/v1",
        "_source": f"{SOURCE}: extracted/rule-tables.json (MAIN.EXE DGROUP, tools/scripts/extract_dos_p3_rules.py) "
                   "and the specs named per block. Data for Core's III rules; nothing here is a formula.",
        "classes": III_CLASSES,
        "core_class": {str(k): CORE_CLASS[c] for k, c in enumerate(III_CLASSES)},
        "_core_race": "III race -> Core Race name, from the inverse of the I -> III transfer map DS:0x4e [C]. 5 (the "
                      "'Random' choice) and 16 (Elemental) have no Core member.",
        "core_race": core_race,
        "skills": {
            "_note": "The nine skills in record order (+0x30 + 4k value, +0x32 + 4k seed; FORMAT.md section 5) [C]. "
                     "seed: '2*STAT+30' (root 0xa1f2); Fire Bow: 2*Level + 4*DEX + STR + 10, +30 Thief, +15 Elf "
                     "(the manual's bonuses). table = the class table in skill_tables (training.md section 5).",
            "order": [
                {"id": "attack", "name": "Attack", "seed_stat": "STR", "table": "attack"},
                {"id": "parry", "name": "Parry", "seed_stat": "DEX", "table": "parry"},
                {"id": "find_item", "name": "Find Item", "seed_stat": "INT", "table": "find_item"},
                {"id": "find_trap", "name": "Find Trap", "seed_stat": "INT", "table": "find_trap"},
                {"id": "disarm_trap", "name": "Disarm Trap", "seed_stat": "DEX", "table": "disarm_trap"},
                {"id": "listen", "name": "Listen", "seed_stat": "INT", "table": "listen"},
                {"id": "fire_bow", "name": "Fire Bow", "seed_stat": None, "table": None},
                {"id": "pick_lock", "name": "Pick Lock", "seed_stat": "DEX", "table": "pick_lock"},
                {"id": "swim", "name": "Swimming", "seed_stat": "CON", "table": "swim"},
            ],
            "fire_bow_seed": {"base": 10, "per_level": 2, "per_dex": 4, "per_str": 1, "class_bonus": {"Thief": 30},
                              "race_bonus": {"2": 15}},
            "cap": 250,
            "trainings_per_level": 3,
        },
        "skill_tables": {"_note": "C_k[class][level 1..15], III class order. " + rt["skill_tables"]["_address"],
                         "by_skill": rt["skill_tables"]["by_skill"]},
        "skill_table_fixes": {"_note": "D3-032 (user s82): five cells of skill_tables look like data errors (Thief Pick "
                                       "Lock L3, Ranger Attack L15, Ranger Parry L15, the Parry table one byte short so "
                                       "Monk / Wizard read a level ahead). These rows are Phantasie I's values, used by "
                                       "default; skill_tables as read is the p3.skill-tables = dos option.",
                              "by_skill": skill_table_fixes(rt, p1ct)},
        "hp_dice": {"_note": rt["hp_dice"]["_note"] + "; levels 1..22", "by_class": rt["hp_dice"]["by_class"]},
        "training_xp_curve": {"_note": "G[level], need = (1000 * G[L] - 80000) / max(class score, 17) (training.md "
                                       "section 2). " + rt["training_xp_curve"]["_address"],
                              "values": rt["training_xp_curve"]["values"]},
        "training_gold_curve": {"_note": "T[level], cost = T[L] * price_index * (20 - CHA)^2 / 100, 5..15000 "
                                         "(training.md section 3). " + rt["training_gold_curve"]["_address"],
                                "values": rt["training_gold_curve"]["values"]},
        "max_level": {"_note": "training clamps the level at 21; level >= 21 cannot train (training.md section 1) [C]",
                      "value": 21},
        "spell_rows": {"_note": rt["spell_rows"]["_address"], "by_class": rt["spell_rows"]["by_class"]},
        "body": {
            "_note": "Six areas in record order (+0xf2 + 2k), states 0..3 [C] (FORMAT.md section 5, "
                     "logic-specs/body-areas.md). hit_location rows: cumulative percent in the column order given "
                     "(DS:0x12cc) [C]; for a character the column k maps to record area [0, 3, 5, 2, 4, 1][k] (root "
                     "0xe9ee). tall_monster_ids: the first group's id that sets the 'tall' flag (root 0x104c0) [C] "
                     "(44 is probably meant to be 43 Valley Giant [~]). limbless_monster_ids: injury messages say "
                     "\"'s body\" for every area (0xe98c) [C].",
            "areas": BODY_AREAS,
            "states": BODY_STATES,
            "hit_location_columns": rt["hit_location"]["_columns"],
            "hit_location_rows": [{"use": u, "cumulative": r} for u, r in zip(rt["hit_location"]["_rows"],
                                                                               rt["hit_location"]["rows"])],
            "character_area_for_column": [0, 3, 5, 2, 4, 1],
            "tall_monster_ids": [44, 67, 68],
            "limbless_monster_ids": [2, 6, 17, 27, 29, 39, 40, 41, 51, 52, 62, 65],
        },
        "social_classes": {
            "_note": "Creation (ovl2 0x227d) [C]: r = rnd(100) for races 0-4 (Human, Dwarven, Elven, Gnome, Halfling), "
                     "rnd(75) for the others; r < 30 Peasant, < 60 Labourer, < 90 Craftsman, else Noble. Starting gold "
                     "in the bank = class * gold_per_class (FORMAT.md section 5.1). 4 Darklord / 5 Superhero are endgame "
                     "titles (D3-006). D3-023 (user s81: manual): each level-up also pays class * gold_per_class "
                     "(DOS pays nothing: an option, not built here). rules.json starting_gold is 0 for this reason.",
            "names": [x["name"] for x in nm["social_classes"]],
            "roll_die_by_race_group": {"base_races": 100, "others": 75},
            "base_races": [0, 1, 2, 3, 4],
            "thresholds": [30, 60, 90],
            "gold_per_class": 256,
        },
        "class_kit": {
            "_note": "Every new (and transferred) character (ovl2 0x227d) [C]: carried [4, 22, bow, 64], all worn; bow 44 "
                     "Short Bow for a Thief, else 41 Self Bow. first_spell by class (spell number; 0 = none).",
            "shield": 4, "armor": 22, "weapon": 64, "bow": {"Thief": 44, "default": 41},
            "first_spell": {"Fighter": 0, "Thief": 0, "Priest": 1, "Ranger": 1, "Monk": 5, "Wizard": 5},
        },
        "score_weights": {"_note": "Quest bits 0..7 of record +0x8a; score = sum + level / 6 (endgame.md section 2). "
                                   + rt["score_weights"]["_address"], "values": rt["score_weights"]["values"]},
        "final_bosses": {"_note": "D3-024 (user s81): the party cannot Flee from these (Nikademus x2, Lord Wood); an "
                                  "option lets it. Monster ids.", "ids": [77, 78, 79]},
        "found_items": {"_note": "D3-020 (user s81: manual 90; options unlimited, DOS 60)", "manual": 90, "dos": 60},
        "roster": {"_note": "D3-029 (user s81: manual 'dozens' = no limit by default; DOS 20 records incl. the three "
                            "elementals, so 17 characters, as an option).", "dos_records": 20, "dos_characters": 17},
        "wilderness_encounters": {
            "_note": "s83, root 0xde54 (logic-specs/encounters.md section 7) [C]: a step on any terrain but 10 / 8 / 15 "
                     "fights when rnd(100) < chance; L = area_levels[x / 10 + 5 * (y / 15)] (root 0x1198); night "
                     "encounters below night_below, then monster 12L + {0, 1, 4}; else kind 2 (12L + rnd(10) + 1) below "
                     "kind2_below, kind 3 (12L + terrain_offsets[t + rnd(4)]) otherwise; ids > 74 or < 0 become 11, 61 "
                     "becomes 60 (root 0xf143). Sleeping monsters below sleeping_below; the monsters notice the party "
                     "when rnd(100) < 8L.",
            "chance": 8, "night_below": 38, "kind2_below": 26, "sleeping_below": 37, "notice_per_level": 8,
            "surprise_below": 51, "party_surprise_below": 31, "run_away_below": 81,
            "area_levels": rt["wilderness_area_levels"]["values"],
            "terrain_offsets": rt["wilderness_terrain_offsets"]["values"],
        },
        "dungeon_entries": {
            "_note": "Entrance square (x, 0) of dungeon n (logic-specs/dungeon-events.md section 1 / 7) [C]: x = the "
                     "record letter's entrance x (DS:0x1bb6); dungeon 1 enters at x 5 (FAR:0xe59a, ovl4 0x84e). "
                     "Index = dungeon number - 1.",
            "x": [5 if n == 0 else rt["dungeon_entrance_x"]["by_letter"][ch]
                  for n, ch in enumerate(rt["dungeon_letters"]["values"])],
            "letters": rt["dungeon_letters"]["values"],
        },
        "transfer_maps": {"_note": "I -> III (DS:0x48 class, DS:0x4e race), indexed by Phantasie I's number [C]",
                          "class": nm["transfer_class_map_I_to_III"], "race": nm["transfer_race_map_I_to_III"]},
    }


# ---- rules and manifest -----------------------------------------------------------------------------------------------

def rules():
    return {
        "_source": "Phase 09 T1.3 rules profile for Phantasie III (DOS SSI 1988 numbers; the III manual is the source "
                   "of truth). Only decided or decoded values. III's training, terrain and encounter rules do not fit "
                   "the I / II blocks (training / terrain / encounters are left out); their tables are in "
                   "rule-tables.json for Core's III rules.",
        "_gold": "D3-026 (user s81: manual 65,000 per party and per account; DOS has no cap, an option).",
        "_guild": "D3-029 (user s81): no roster limit by default (manual 'dozens'); built as a rules option by Core's III "
                  "rules. The numbers below are DOS's: 20 records, the 3 elementals in slots 0-2, so 17 characters "
                  "(logic-specs/elementals.md). guild_by_platform.dos records the same.",
        "_starting_gold": "0 here: III's starting gold is social class * 256 (rule-tables.json social_classes), added "
                          "by III's creation rule (FORMAT.md section 5.1).",
        "_equipment_slots": "Manual (III PDF 6 / pr.6, 11-12) and DOS (ovl2 0x37f1) [C]: shield 1-20, armour 21-40, "
                            "bow 41-60, melee weapon 61-100.",
        "original_platform": "dos",
        "game": GAME,
        "party_gold_cap": 65000,
        "bank_gold_cap": 65000,
        "guild_slot_count": 17,
        "guild_max_characters": 17,
        "starting_gold": 0,
        "equipment_slots": [
            {"id": "shield", "first_item": 1, "last_item": 20},
            {"id": "armor", "first_item": 21, "last_item": 40},
            {"id": "bow", "first_item": 41, "last_item": 60},
            {"id": "weapon", "first_item": 61, "last_item": 100},
        ],
        "guild_by_platform": {
            "dos": {"slots": 17, "max_characters": 17,
                    "_source": "[C] static: roster FAR:0xe820 x 20 records, creation needs a count below 20, a new data "
                               "disk starts with Air / Mist / Mud in slots 0-2 (root 0xa856, ovl2 0x3f04)."},
        },
    }


def manifest(built):
    return {
        "_schema": "phantasie.game/v1",
        "_source": "Built by tools/scripts/build_p3_gamedata.py from " + SOURCE,
        "_note": "The tables this folder holds and their sizes, read by OpenPhantasie.Data GameTables.Load (09-trilogy.md "
                 "T4). III has no MESS message files (its dungeon texts are in each DNGS record: dungeons.json "
                 "messages), so message_areas is 0 and there is no messages.json. rule-tables.json holds III's own "
                 "tables (skills, body areas, training curves, social classes, class kit, wilderness encounters, dungeon entries). Played by Core's Phantasie3Campaign (09-trilogy.md T4).",
        "game": GAME,
        "platform": "dos",
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
        "_spell_cost_rule": "Costs come from the manual's spell table, filled per spell; #53 (empty) and #57 Divine Aid "
                            "have none [?].",
    }


def build():
    nm = load(SRC, "names.json")
    mon = load(SRC, "monsters.json")
    rt = load(SRC, "rule-tables.json")
    built = {
        "monsters.json": monsters(mon),
        "items.json": items(load(SRC, "items.json")),
        "spells.json": spells(load(SRC, "spells.json"), load(P1, "spells.json")["spells"]),
    }
    built["names.json"] = names(nm, mon["monsters"], built["items.json"]["items"], built["spells.json"]["spells"])
    built.update({
        "overworld.json": overworld(load(SRC, "world.json")),
        "dungeons.json": dungeons(load(SRC, "dungeons.json")),
        "scrolls.json": scrolls(load(SRC, "scrolls.json")),
        "towns.json": towns(load(SRC, "towns.json")),
        "creation-tables.json": creation(rt, load(P1, "creation-tables.json")),
        "elementals.json": elementals(rt),
        "rule-tables.json": rule_tables(rt, nm, load(P1, "creation-tables.json")),
        "rules.json": rules(),
    })
    built["game.json"] = manifest(built)
    return built


def main():
    check = "--check" in sys.argv[1:]
    built = build()
    os.makedirs(OUT, exist_ok=True)
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
