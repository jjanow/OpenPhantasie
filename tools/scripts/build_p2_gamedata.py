#!/usr/bin/env python3
"""Build src/OpenPhantasie.Data/GameData/phantasie2/ from the Phantasie II (Atari ST v1.0) extracted tables.

Phase 09 T3 (03b-phantasie2-re.md). Input: reverse-engineering/atari-st/phantasie2/extracted/ (written by
extract_st_phantasie2.py and extract_st_p2_{maps,towns,dungeons,scrolls}.py). Output: JSON in the same schemas as
GameData/phantasie1 where they fit, plus game.json (the table manifest GameTables.Load reads) and rules.json.

Rules: only what is decoded [C] (or decided by the user / stated by the manual) goes in. A value nobody has decoded is
left out or null with a "[?]" note; nothing is guessed. Run from the repo root:

    python3 -I tools/scripts/build_p2_gamedata.py [--check]

--check rebuilds in memory and fails if a file on disk differs (for CI / review).
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "reverse-engineering", "atari-st", "phantasie2", "extracted")
P1 = os.path.join(ROOT, "src", "OpenPhantasie.Data", "GameData", "phantasie1")
OUT = os.path.join(ROOT, "src", "OpenPhantasie.Data", "GameData", "phantasie2")
GAME = "p2"
SOURCE = "Phantasie II, Atari ST v1.0 (reverse-engineering/atari-st/phantasie2/extracted/, FORMAT.md)"


def load(*parts):
    with open(os.path.join(*parts), encoding="utf-8") as f:
        return json.load(f)


def cid(n):
    return f"{GAME}:{n}"


# ---- monsters ---------------------------------------------------------------------------------------------------------

def monsters(src, gfx):
    # Sprite sizes: PHANT.PRG size table 0x2112e, (u16 width, u16 height) per monster [C], dumped with the live-checked
    # sprites by extract_st_p2_graphics.py (FORMAT.md section 10.3). The encounter builder copies the HEIGHT word
    # (0x2112e + 4m + 2) into the group record +0x35 and fits groups on sum(height + 3) < 135 (0xaa50, 0xab36;
    # logic-specs/encounters.md section 3) [C].
    size = {g["index"]: (g["width"], g["height"]) for g in gfx["monsters"]}
    out = []
    for m in src:
        rec = {"id": cid(m["index"])}
        rec.update({k: v for k, v in m.items() if k != "pad_23"})
        w, h = size[m["index"]]
        assert gfx["monsters"][m["index"]]["name"] == m["name"], f"sprite {m['index']} is not {m['name']}"
        rec.update(sprite_width=w, sprite_height=h)
        out.append(rec)
    return {
        "_source": f"{SOURCE}: PHANT.PRG data+0x1c76, 80 x 58 B big-endian (FORMAT.md section 4); sprite sizes "
                   "PHANT.PRG 0x2112e (FORMAT.md section 10.3)",
        "_fields": "Same record as the Amiga I game (DOS 57-B record + pad byte at +0x23, dropped here). The encounter "
                   "builder build_monster_groups 0x0a72e reads every field DOS I's builder reads, at the same "
                   "places [C] (logic-specs/encounters.md); the combat use of the other fields is carried over from "
                   "DOS I (GameData/phantasie1/monsters.json _fields) [~]. sprite_width / sprite_height = the "
                   "sprite's pixel size [C]; II's group fit test uses sprite_height + 3 (I's DOS table: "
                   "sprite_width), see encounters.md section 3. Traits: DISPELL UNDEAD on the ST hits monsters whose "
                   "XP is divisible by 7 (0x842c), FEAR only lowers the encounter level (0x8326), as DOS I; the "
                   "manual's versions are DIFFERENCES-II.md D2-022 / D2-023 (user s80: manual, as I; PLUTO'S UNDEAD SNAKE is undead): "
                   "monster-traits.json, the ST's tests as rules options p2.dispell-undead / p2.fear = st.",
        "monsters": out,
    }


# D2-022 (user s80): the manual's list, as I (GameData/phantasie1/monster-traits.json), plus PLUTO'S UNDEAD SNAKE.
P2_UNDEAD = ["SKELETON", "ZOMBIE", "GHOUL", "LARGE SKELETON", "WIGHT", "WRAITH", "VAMPIRE", "GHOST", "SPIRIT",
             "PLUTO'S UNDEAD SNAKE"]


def monster_traits(mon, p1traits, p1mon):
    by_name = {m["name"]: m["index"] for m in mon}
    p1_names = {m["index"]: m["name"] for m in p1mon["monsters"]}
    # I's undead list by name must be II's list minus the snake (II's table keeps I's names at I's indices).
    assert [p1_names[i] for i in p1traits["undead"]] == P2_UNDEAD[:-1], "I's undead list changed"
    undead = sorted(by_name[n] for n in P2_UNDEAD)
    return {
        "_source": "Remaster rule data, not in the ST monster table. Indices are monsters.json 'index'. "
                   "See DIFFERENCES-II.md D2-022 / D2-023.",
        "_fields": {
            "undead": "D2-022 (user s80: manual, as I's D-024, and PLUTO'S UNDEAD SNAKE (72) is undead): DISPELL UNDEAD "
                      "affects these (manual: skeletons, zombies, ghosts and others returned from the grave). I's nine "
                      "plus the snake. The ST's XP % 7 == 0 test (combat case 0x842c) is the rules option "
                      "p2.dispell-undead = st: it also hits BANTIR (1), DEMON (48), HIGH DEMON (60) and misses the snake.",
            "never_flees": "D2-023 (user s80: manual, as I: FEAR makes the monsters run; the ST's level drop is p2.fear = "
                           "st). User s81: Pluto (79), PLUTO'S ICE DRAGON (78) and Pluto's eight minions (17, 43, 72-77; "
                           "endgame.md 3.1) stand fast, as I's Black Lord; p2.fear-bosses = flee lets them run.",
        },
        "undead": undead,
        "never_flees": [17, 43, 72, 73, 74, 75, 76, 77, 78, 79],
    }


# ---- items ------------------------------------------------------------------------------------------------------------

def kind_of(i):
    # Manual equipment tables (same in the combined I + II rulebook): shields 1-20, armour 21-40, weapons 41-100.
    if 1 <= i <= 20:
        return "shield"
    if 21 <= i <= 40:
        return "armor"
    if 41 <= i <= 100:
        return "weapon"
    return "other"


def items(names, p1_items):
    p1 = {x["index"]: x for x in p1_items}
    out = []
    for it in names:
        i = it["index"]
        rec = {"id": cid(i), "index": i, "name": it["name"], "value_factor": it["value_factor"], "kind": kind_of(i)}
        if i <= 100:
            same = p1[i]["name"] == it["name"] and p1[i]["value_factor"] == it["value_factor"]
            assert same, f"item {i} differs from I: equipment data copied from I would be wrong"
            rec.update(points=p1[i]["points"], min_str=p1[i]["min_str"], str_check=p1[i]["str_check"])
            if i == 0:
                rec["kind"] = p1[i]["kind"]
        out.append(rec)
    return {
        "_source": f"{SOURCE}: PHANT.PRG value table data+0x180, name pointers data+0x2ec (FORMAT.md section 3) [C]",
        "_note": "Ids 0-100 have the same names and value factors as Phantasie I (all 181 values are identical). Their "
                 "points / min_str / str_check are Phantasie I's index formulas (phantasie1/items.json _formulas) [~] "
                 "for II: checked live only for the transfer starter kit (SM SHIELD 4, MACE 4, LEATHER 3 points, "
                 "parity/roster-live.md). 101-110 are renamed HEAL POT1..9 / HEALING10; 121-140 = 14 lettered scrolls "
                 "+ 6 blank slots; 141-148 AMULET 1-8; 149 FLMN WAND; 150 ---ORB---; 151-180 new treasures. What "
                 "potions, amulets, wand and orb do in II is [?] (00-function-map.md section 5.6).",
        "items": out,
    }


# ---- spells and names -------------------------------------------------------------------------------------------------

# Remaster-local spell numbering, as in Phantasie I: local 0-53 = ST slots 0-53 (the 54 shared spells), then the divine
# spells. Local 54 = DIVINE I (slot 57, as I), local 55 = DIVINE II (slot 56, new in II).
DIVINE = [(54, 57), (55, 56)]
DIVINE_EFFECT = {
    54: "DIVINE I (#58): in a dungeon, a god (DEMIGOD / ATHENA / APOLLO / ZEUS) gives advice, dungeon message 105; "
        "nothing elsewhere or in combat. Gained only in Phantasie I (magic-divine.md section 3) [C] static.",
    55: "DIVINE II (#57): in wilderness region 26 it summons a fight (monster r1*r2*0.0072, or PLUTO'S WYVERN / "
        "SNAKE-DRAGON on r < 25); nothing elsewhere or in combat. Gained at the DNG3 pool (magic-divine.md section 2) "
        "[C] static.",
}


def spell_slots(names):
    slots = {s["slot"]: s["name"] for s in names["spells"]}
    order = [(k, k) for k in range(54)] + DIVINE
    return [(local, slot, slots[slot]) for local, slot in order]


def spells(names, p1_spells):
    p1 = {s["index"]: s for s in p1_spells}
    out = []
    for local, slot, name in spell_slots(names):
        rec = {"id": cid(local), "index": local, "name": name, "dos_slot": slot}
        if local < 54:
            assert p1[local]["name"] == name, f"spell {local}: {name} vs I {p1[local]['name']}"
            # Manual: II keeps I's 54-spell list, costs and types (09-trilogy-manual-p2.md Magic row).
            rec.update(category=p1[local]["category"], magic_point_cost=p1[local]["magic_point_cost"],
                       effect_summary=p1[local]["effect_summary"],
                       effect_coefficients_note="As Phantasie I per the manual; II's spell code not compared [~].")
        else:
            # magic-divine.md section 1 [C]: cost = n mod 4 (0 -> 4) with n = slot + 1, i.e. (slot % 4) + 1, I's rule.
            # Field cast 0x6ecc, combat chooser 0xf298 and combat deduction 0x7610 agree. The manual gives no cost.
            rec.update(category="divine", magic_point_cost=(slot % 4) + 1,
                       effect_summary=DIVINE_EFFECT[local],
                       effect_coefficients_note="MP cost [C] (magic-divine.md section 1): (slot % 4) + 1 as every "
                                                "spell. Divine spells are never learnt at the Guild "
                                                "(guild_learn_spell 0x0d0d2, slot < 55) [C].")
        out.append(rec)
    return {
        "_schema": "phantasie.spells/v1",
        "_source": f"{SOURCE}: spell name table (pointer run data+0x5c4) [C]",
        "_note": "Local index = remaster spell number (I's numbering, DIVINE I = 54); dos_slot = the ST table slot (= the "
                 "GUILD @96 flag word). The game numbers spells slot + 1: DIVINE II = #57, DIVINE I = #58 (D2-004). "
                 "Slots 54, 55, 58, 59 have no name and are left out.",
        "spells": out,
    }


def names(nm, mon):
    def lst(xs):
        return [{"index": i, "name": x} for i, x in enumerate(xs)]
    return {
        "_source": f"{SOURCE}: PHANT.PRG data-segment pointer runs (FORMAT.md section 3) [C]",
        "_note": "16 races: index 15 = UNDEAD (I's names.json lists 15). spell_level_names and town_labels are not "
                 "extracted for II yet [?].",
        "classes": lst(nm["classes"]),
        "races": lst(nm["races"]),
        "status": lst(nm["status"]),
        "spells": [{"index": local, "name": name, "dos_slot": slot} for local, slot, name in spell_slots(nm)],
        "items": [{"index": x["index"], "name": x["name"]} for x in nm["items"]],
        "monsters": [{"index": m["index"], "name": m["name"]} for m in mon],
        "spell_level_names": [],
        "town_labels": [],
    }


# ---- world ------------------------------------------------------------------------------------------------------------

def overworld(ow):
    regions = {}
    for key, r in ow["regions"].items():
        regions[key] = {
            "region_id": r["region_id"], "size": r["size"], "grid_width": r["grid_width"],
            "grid_height": r["grid_height"], "tiles": r["tiles"],
            "features": [{k: f[k] for k in ("index", "x", "y", "arg", "dest_x", "dest_y")} for f in r["features"]],
            "text_pages": [{"index": p["index"], "text": p["text"]} for p in r["text_pages"]],
            "embedded_text": [],
        }
    return {
        "_source": f"{SOURCE}: {ow['_source']}",
        "_note": ow["_note"] + " Debug fields of the extract (raw feature bytes, the 1000-1499 gap, picture polygons) "
                               "are left out; see extracted/maps/overworld.json.",
        "grid_width": ow["grid_width"], "grid_height": ow["grid_height"],
        "tile_rules": ow["tile_rules"],
        "regions": regions,
    }


EVENT_RECORDS = 0x4f6  # DungeonScript.EventRecordBase: 5-byte records for features 0xdd.. (record k = feature 0xdc + k)
SCRIPT_BASE = 0x5aa    # DungeonScript.ScriptBase: script s starts at 0x5aa + 2 s
FOUR_BYTE_OPS = {12, 13, 14, 15, 18, 19, 21}


def live_events(raw):
    """Event features a party can meet: those on the grid, plus every GOTO EVENT (op 22) target of their scripts,
    following JUMP / RANDJUMP (ops 20 / 21). Record byte 1 bounds the choices read (Phantasie2DungeonScript)."""
    grid = {raw[i] for i in range(33 * 38) if raw[i] >= 0xdd}
    seen, todo, scripts = set(), list(grid), set()
    while todo:
        f = todo.pop()
        if f in seen or f > 0xff:
            continue
        seen.add(f)
        rec = EVENT_RECORDS + 5 * (f - 0xdc)
        work = [raw[rec + c + 1] for c in range(1, max(raw[rec + 1], 1) + 1) if rec + c + 1 < len(raw)]
        while work:
            s = work.pop()
            if s in scripts:
                continue
            scripts.add(s)
            p = SCRIPT_BASE + 2 * s
            for _ in range(17):
                if p + 3 >= len(raw):
                    break
                o, a, b = raw[p], raw[p + 1], raw[p + 2]
                op = o % 128
                op = op - 0x40 if op > 0x3f else op
                if op == 22:
                    todo.append(a + 0xdc)
                elif op == 20:
                    work.append(a)
                elif op == 21:
                    work.append(b)
                if o <= 0x7f:
                    break
                p += 4 if op in FOUR_BYTE_OPS or o == 0xc0 else 2
    return seen


def relabel_menus(key, lv, slots):
    """II menus (s82 play-through): the option count is event record byte 1 [C by data: it equals the decoded count in
    every other live record]; I's 11 / 3-column label rule cut DNG8's 9-button menu (msg 71) to two options and blanked
    msg 76's labels. Where the count differs or a label is blank, the labels are the message's last whitespace-separated
    words (two or more spaces apart), taken only when exactly ``count`` of them follow the text [~]: the ST's own label
    layout is not traced. Returns the ids changed."""
    import re
    raw = bytes.fromhex(lv["raw_hex"])
    msgs = lv["messages"]
    changed = []
    for f in sorted(live_events(raw)):
        rec = EVENT_RECORDS + 5 * (f - 0xdc)
        mid, count = raw[rec] & 0x7f, raw[rec + 1]
        m = msgs.get(str(mid))
        if m is None or count < 2 or (len(m["options"]) == count and all(o.strip() for o in m["options"])):
            continue
        for k in range(3):
            text = (slots.get(mid + k) or {}).get("text") or ""
            words = [w for w in re.split(r"\s{2,}", text.strip()) if w]
            if len(words) in (count, count + 1) and (k > 0 or len(words) == count + 1):
                m["options"] = words[-count:]
                if k == 0:
                    m["lines"] = [words[0]]
                changed.append(mid)
                break
    return changed


def dungeons(dg, ms):
    levels = {}
    relabelled = {}
    for key, lv in dg["levels"].items():
        levels[key] = {k: lv[k] for k in ("id", "size", "raw_hex", "embedded_text", "messages")}
        area = next(a for a in ms["areas"] if a["id"] == int(key[3:]))
        ids = relabel_menus(key, levels[key], {s["id"]: s for s in area["slots"] if not s.get("empty")})
        if ids:
            relabelled[key] = ids
    return {
        "_source": f"{SOURCE}: {dg['_source']}",
        "_note": "Phantasie I level format [C] (FORMAT.md section 9.4). Messages decoded with I's rules; II's event / "
                 "script interpreter is not compared with I's yet [?] (three references land on continuation records). "
                 "Menus of live events whose option count differs from event record byte 1 (or with blank labels) are "
                 "relabelled from the message's own words [~] (build_p2_gamedata.relabel_menus): "
                 + ", ".join(f"{k} msg {', '.join(map(str, v))}" for k, v in relabelled.items()) + ".",
        "levels": levels,
    }


def messages(ms):
    return {
        "_source": f"{SOURCE}: {ms['_source']}",
        "_cipher": ms["_cipher"],
        "_layout": ms["_layout"],
        "areas": [{"id": a["id"], "source": a["source"], "size": a["size"], "slot_count": len(a["slots"]),
                   "record_count": len(a["records"]), "slots": a["slots"], "records": a["records"]} for a in ms["areas"]],
    }


# find_items (PHANT.PRG 0xaeb2-0xaee6) never hands out a blank-named scroll slot: 122 / 123 -> 121, 133 -> 132,
# 135 / 136 -> 134, 137 -> 138 [C]. item_records maps those slots to the same lettered scroll.
BLANK_SCROLL_ITEMS = {122: 121, 123: 121, 133: 132, 135: 134, 136: 134, 137: 138}


def scroll_item_records(sc):
    """s201: scroll item 121-140 -> [id of the lettered scroll it shows] (the Client's "item - 120" fallback was wrong)."""
    by_item = {s["item_id"]: s["id"] for s in sc["scrolls"]}
    return {str(i): [by_item[BLANK_SCROLL_ITEMS.get(i, i)]] for i in range(121, 141)
            if BLANK_SCROLL_ITEMS.get(i, i) in by_item}


def scrolls(sc):
    return {
        "_source": f"{SOURCE}: {sc['_source']}",
        "_layout": sc["_layout"],
        "_item_map": sc["_item_map"],
        "item_records": scroll_item_records(sc),
        "page_layout": "rows",
        "scrolls": [{"id": s["id"], "step": 0, "item_id": s["item_id"], "name": s["name"], "records": s["records"],
                     "lines": s["lines"], "text": s["text"]} for s in sc["scrolls"]],
    }


def town_plane(town_id):
    """PHANT.PRG code, not TWNS data (logic-specs/towns.md section 3): towns 1-6 are on Ferronrah, 7-10 in the
    Netherworld (create_character 0x0c90c: origin 2 when the town number > 6); 11-12 are I's unreachable leftovers."""
    if 1 <= town_id <= 6:
        return "ferronrah"
    if 7 <= town_id <= 10:
        return "netherworld"
    return None


def transport_destinations(town_id):
    """TRANSPORTATION's town list (transport_menu 0x023ee, accepted 1..10 by 0x0d94c) [C] static: from a Ferronrah town
    the other five Ferronrah towns; from a Netherworld town only its pair, 7<->8 and 9<->10."""
    if 1 <= town_id <= 6:
        return [t for t in range(1, 7) if t != town_id]
    if 7 <= town_id <= 10:
        return [{7: 8, 8: 7, 9: 10, 10: 9}[town_id]]
    return []


def towns(tw):
    keep = ("id", "index", "name", "price_index", "stock", "restock_items", "exit_region", "exit_x", "exit_y",
            "map_town_cells")
    out = []
    for t in tw["towns"]:
        rec = {k: t[k] for k in keep if k in t}
        rec["plane"] = town_plane(t["id"])
        rec["transport_destinations"] = transport_destinations(t["id"])
        out.append(rec)
    return {
        "_source": f"{SOURCE}: TWNS.INT bytes 2446-5949 (the initial TWNS.DAT), 12 x 292 B (FORMAT.md section 9.5)",
        "_layout": tw["_layout"],
        "_notes": tw["_notes"] + [
            "plane and transport_destinations come from PHANT.PRG code, not from TWNS data "
            "(reverse-engineering/atari-st/phantasie2/logic-specs/towns.md sections 3-4) [C] static.",
            "Every town offers the same services (one town dispatch, 0x0db5a); towns differ only in this data and in "
            "TRANSPORTATION's list (towns.md section 2).",
        ],
        "_formulas": {
            "buy_price": "floor(V*V * price_index / 500) + 1 (0x0c2f2), as I",
            "sell_price": "V*V / 8 at the Inn's Distribute SELL row; stock + 1, capped at 99 (0x0eaf4)",
            "restock": "town entry (0x0c0a8): 18x stock[rand/274] = 0, then 4x stock[restock_items[rand/(32767/avg_level)]]"
                       " = 1 (index 0 when avg_level = 0), as I",
            "training_fee": "min(15000, floor(level^2.2) * (20 - CHA)^2 * price_index / 100) (0x0cff0..0x0d086); I clamps "
                            "20 - CHA at 0, II does not (no effect while CHA <= 20)",
            "learn_fee": "price_index * level * 90 / (CHA * INT * (9 - class)) (0x0d272), as I",
        },
        "towns": out,
    }


# ---- creation tables --------------------------------------------------------------------------------------------------

def creation(ct, p1ct):
    out = {k: v for k, v in ct.items() if not k.startswith("_") and k not in ("identical_to_amiga_p1_bytes_0_2445",
                                                                              "tables_differing_from_dos_p1_values")}
    # D2-009 (user: "manual, as in I"): the same two cells Phantasie I's GameData sets from the manual (D-001, D-006).
    hal = out["race_attribute_modifiers"]["by_race"]["HALFLING"]
    assert hal["modifiers"]["DEX"] == 11, "HALFLING DEX changed upstream"
    hal["modifiers"]["DEX"] = p1ct["race_attribute_modifiers"]["by_race"]["HALFLING"]["modifiers"]["DEX"]
    hal["ceilings"]["DEX"] = p1ct["race_attribute_modifiers"]["by_race"]["HALFLING"]["ceilings"]["DEX"]
    monk = out["spell_levels"]["by_class"]["MONK"]
    assert monk[41] == 7, "MONK spell 41 changed upstream"
    monk[41] = p1ct["spell_levels"]["by_class"]["MONK"][41]
    # D2-015 (user s79: manual, wizard level 13): the manual's "a high-level wizard using a RESURRECT spell" (CB p.10).
    wiz = out["spell_levels"]["by_class"]["WIZARD"]
    assert wiz[43] == 0, "WIZARD spell 43 changed upstream"
    wiz[43] = 13
    return {
        "_schema": ct.get("_schema", "phantasie.creation-tables/v1"),
        "_source": f"{SOURCE}: TWNS.INT bytes 0-2445 (byte-identical to Amiga I, value-identical to DOS I)",
        "_decisions": "D2-009 (DIFFERENCES-II.md, user: manual, as in I): HALFLING DEX modifier 12 / ceiling 20 "
                      "(D-001) and MONK spell 41 (#42 SLEEP) not learnable at level 7 (D-006). D2-015 (user s79: "
                      "manual): WIZARD spell 43 (#44 RESURRECTION) learnable at level 13, as the RANGER. Raw values: "
                      "extracted/towns/creation-tables.json.",
        "_lifespan": "lifespan_bands = the manual's bands +-10 %, as I (D2-021, user s80: manual, as I's D-003; copied "
                     "from GameData/phantasie1/creation-tables.json). ST rule [C] (logic-specs/encounters.md section 8; create_character 0x0cc84..0x0cd0a, town_aging 0x0bba6): lifespan "
                     "@252 = @10 x factor, @10 = rand/5461 + 8 (8..13, 14 when rand >= 32766); factor by race index: "
                     "HUMAN, PIXIE 1; DWARF, HALFLING, GNOME 5/2; ELF 19; every other race (UNDEAD too) 2/5 "
                     "(jump table 0x22976) = DOS I's factors. Aging on town entry as DOS: past 7/10 of @252 once "
                     "'has aged' (stats x 4/5), past @252 'has died'. It is dos_tables.lifespan_factors, played by the "
                     "rules option p2.lifespans = st. Race 15 UNDEAD (transfer / elementals only) has no row: the "
                     "manual counts it among 'the others' (40-50) and the ST gives it 2/5 [C].",
        **out,
        "lifespan_bands": p1ct["lifespan_bands"],
        "dos_tables": {
            "_source": "The Atari ST's own cells that the tables above replace by the manual, applied per save by the "
                       "rules options p2.wizard-resurrection = st and p2.lifespans = st (Original (Atari ST) preset). "
                       "D2-009 has no ST option (user: manual, as in I), so its cells are not here.",
            "spell_levels": {
                "_note": "D2-015: TWNS.INT +0x5a WIZARD spell slot 43 (#44 RESURRECTION) = 0, never learnable.",
                "WIZARD": {"43": 0},
            },
            "lifespan_factors": {
                "_note": "D2-021 [C] (create_character 0x0cc84..0x0cd0a, jump table 0x22976): @252 = @10 * num / den, "
                         "the same factors as DOS I (dos_tables.lifespan_factors of GameData/phantasie1); checked on "
                         "town entry as @264 >> 8 (town_aging 0x0bba6).",
                **{k: v for k, v in p1ct["dos_tables"]["lifespan_factors"].items() if not k.startswith("_")},
            },
        },
    }


def elementals(el):
    keep = ("name", "intelligence", "mp_ceiling", "magic_max", "magic_cur", "hp_max", "hp_cur", "status", "attack",
            "parry", "level", "guild_number", "weapon_damage_b", "shield_soak", "armor_soak", "race", "birth_race",
            "class", "known_spells")
    return {
        "_source": f"{SOURCE}: TWNS.INT bytes 5950+22*318 (the Guild template's three elementals) [C]",
        "_notes": "Every field equals GameData/phantasie1/elementals.json except the names (FIRE_ELEM ...). "
                  "known_spells = flag words equal to 1 (FIREFLASH I-IV), as the live SPELLS page shows (roster-live.md).",
        "elementals": [{k: e[k] for k in keep} for e in el["elementals"]],
    }


# ---- rules and manifest -----------------------------------------------------------------------------------------------

def rules():
    return {
        "_source": "Phase 09 T1.3 rules profile for Phantasie II (Atari ST v1.0 numbers; the combined I + II rulebook is "
                   "the source of truth). Only decoded [C] values or rules the manual states. Values marked [?] in "
                   "_open are placeholders the profile format requires.",
        "_gold": "Manual: the combined I + II rulebook's general rules (CB p.6) give 65,000 GP carried and 65,000 per "
                 "bank account, as I (09-trilogy-manual-p2.md 'Caps and limits'). ST [C] static (logic-specs/encounters.md "
                 "section 8): both clamp at 65,535 "
                 "(clamp 0x0dfa4; callers: bank withdraw 0x0df74, sale 0x0eb44, dungeon gold 0x11d98, town-entry "
                 "split into @262 0x0bef8; combat victory 0x0a43e; the gold entry box 0x01272 refuses > 65,535), as "
                 "DOS I. Conflict repeats I's D-048: DIFFERENCES-II.md D2-019 (user s80: manual, as I); the caps below are the "
                 "manual's, the ST's 65,535 is the rules option p2.gold-cap = st.",
        "_guild": "TWNS.INT Guild template = 22 character slots + FIRE/WATER/EARTH (FORMAT.md section 9.5) [C]. The ST "
                  "(logic-specs/encounters.md section 8) "
                  "Guild menu enables 'New member' while any of the 22 slots has @92 = 0 (guild_menu 0x003ea -> "
                  "0x0ebf6) and N)EW takes the first such slot (create_character 0x0c93a), so N)EW fills all 22 "
                  "[C], seen live (s80). guild_max_characters = 21 is the combined rulebook's C64 number (CB p.20), the same "
                  "choice as I (D-002); Apple: 37 (CB p.5). Conflict repeats D-002: DIFFERENCES-II.md D2-020 "
                  "(user s80: manual, as I); the ST's 22 (guild_by_platform.atari-st) is the rules option p2.guild-size = st.",
        "_original_platform": "The platform whose code is II's rules source (s71): the Original (Atari ST) preset reads "
                              "guild_by_platform under this key.",
        "original_platform": "atari-st",
        "_starting_gold": "256 GP in the bank [C] (logic-specs/encounters.md section 8): create_character 0x0cc2a writes 256 to @262; the manual agrees "
                          "(combined rulebook general rules, 09-trilogy-manual-p2.md).",
        "game": GAME,
        "party_gold_cap": 65000,
        "bank_gold_cap": 65000,
        "guild_slot_count": 22,
        "guild_max_characters": 21,
        "starting_gold": 256,
        "_equipment_slots": "Manual tables (same as I): shields 1-20, armour 21-40, weapons 41-100.",
        "equipment_slots": [
            {"id": "shield", "first_item": 1, "last_item": 20},
            {"id": "armor", "first_item": 21, "last_item": 40},
            {"id": "weapon", "first_item": 41, "last_item": 100},
        ],
        "guild_by_platform": {
            "atari-st": {"slots": 22, "max_characters": 22,
                         "_source": "[C] static. TWNS.INT Guild template: 22 character records + 3 elementals (25 x "
                                    "318 B GUILD.DAT); 'New member' stays enabled while one of the 22 slots is free "
                                    "(0x0ebf6) and N)EW takes the first free one (0x0c93a). Live s80: greyed at 22 occupied, "
                                    "enabled at 21 (encounters.md section 8)."},
        },
        "_training": "D2-012 (NOT A CONFLICT: the manual gives no formula; II's numbers apply). PHANT.PRG guild_train "
                     "0x0cdde [C] (00-function-map.md section 5.1): s = sum / divisor, F = 1.8^level, F = 1.0 when "
                     "level == 0 (new in II), target = (F - 0.8) * 1000 / (s + 1e-5). FIGHTER and PRIEST changed from "
                     "I; the other four as in I. Training gold for level 0 -> 1 is 0 (live s78). Constants are FFP "
                     "singles on the ST.",
        "_training_deltas": "training.md: the price's 20 - CHA is not clamped (section 1 item 5, as Amiga I); a "
                            "level-up marks every unknown spell of level 1 .. L + 1 (section 2 item 2, seen live); an "
                            "HP re-roll >= the old maximum becomes the new maximum (section 2 item 3). D2-013 (user s79: "
                            "manual, as I) is a rules option, not data: p2.training-cost and p2.hit-dice.",
        "training": {
            "level_factor_one_when": "level_zero",
            "charisma_clamp": False,
            "spell_marks": "up_to_new_level",
            "hp_tie": "new_maximum",
            "classes": [
                {"class": "THIEF", "weights": {"STR": 1, "DEX": 2}, "divisor": 19.8},
                {"class": "MONK", "weights": {"STR": 1, "INT": 1, "DEX": 1}, "divisor": 49.5},
                {"class": "RANGER", "weights": {"STR": 1, "INT": 1, "CHA": 1}, "divisor": 52.8},
                {"class": "FIGHTER", "weights": {"STR": 2, "DEX": 1}, "divisor": 33.0},
                {"class": "PRIEST", "weights": {"STR": 1, "INT": 2}, "divisor": 36.3},
                {"class": "WIZARD", "weights": {"INT": 2, "LUCK": 1}, "divisor": 42.9},
            ],
        },
        "_terrain": "logic-specs/terrain.md [C][L] (PHANT.PRG overworld_step 0x6022). Every roll is rand / 327 (0..100). "
                    "Classes 7 (dark voids) and 11 (mist) share one path (0x6338): while the trip's fight flag is clear, "
                    "r <= 8 fights the 0-based monster 12*T + r'/34 (T = the cell's digit; undead, devils and one insect) "
                    "and sets the flag (cleared on leaving a town, dungeon -> wilderness and a party wipe, as I). Class 8 "
                    "is molten lava: every living member takes r*T/10 + 1, digit 0 harmless (D2-007). Class 10 swims "
                    "(I's rule). Other classes roll r < 6 for a random encounter. Mist = 11 and voids = 7 is the "
                    "picture reading [~] (D2-008, D2-014).",
        "terrain": {
            "roll": "rand_327",
            "random_encounter_below": 6,
            "classes": {
                "7": {"kind": "terrain_fight", "fight_roll_max": 8, "monster_per_digit": 12, "monster_offset": 0,
                      "monster_spread": 34},
                "8": {"kind": "lava", "damage_divisor": 10},
                "10": {"kind": "swim"},
                "11": {"kind": "terrain_fight", "fight_roll_max": 8, "monster_per_digit": 12, "monster_offset": 0,
                       "monster_spread": 34},
            },
        },
        "_toss_rock": "logic-specs/toss-rock.md [C] live: TOSS bonus @62 = 2*L + 4*DEX + STR + 10 (+15 DWARF, +30 GNOME), "
                      "recomputed at every level-up (L = the level being left); TOSS grows by skill-coefficient column 6. "
                      "A toss tries 3 slots for a living target; damage = max(0, trunc(12*rnd/100) - armor) + "
                      "trunc(rnd*@28/100), at least 1. Used by the p2.rock-throwing module in any game.",
        "toss_rock": {
            "bonus_base": 10,
            "bonus_per_level": 2,
            "bonus_per_dex": 4,
            "bonus_per_str": 1,
            "race_bonus": {"DWARF": 15, "GNOME": 30},
            "coefficient_column": 6,
            "rock_damage": 12,
            "target_tries": 3,
        },
        "_encounters": "logic-specs/encounters.md section 1 [C] static (PHANT.PRG build_monster_groups 0x0a72e, "
                       "night_watch_encounter 0xfe38, dungeon 0x10b60): every roll is rand / 327 (0..100); L and the "
                       "reaction sum are divided by the group count (0xad68, as Amiga I); groups fit on the sprite height "
                       "(size table word 1, sum of height + 3 < 135); the dungeon wandering monster is r/33 + 12*T + b - 8 "
                       "(I: T once); the day encounter has no Black Knights roll (I: r > 91); no DNG6 TROLKIN override; "
                       "the first monster id is clamped to 0..79 (0xa742).",
        "encounters": {
            "roll": "rand_327",
            "level_and_reaction": "average",
            "group_fit": "sprite_height",
            "dungeon_tier_factor": 12,
            "day_black_knights": False,
            "clamp_first_monster": True,
        },
    }


def wilderness_encounters():
    return {
        "_source": "PHANT.PRG data 0x1fefe (16 bytes) [C] (logic-specs/encounters.md section 4): the same table as "
                   "Phantasie I's COM DS 0x1a5c, read by the day branch of night_watch_encounter 0xfe38.",
        "_note": "Day monster (0-based) = 12*(tile % 10) + 2*pair_table[tile / 10 + r / 25] + 2r/100 (r = rand / 327).",
        "pair_table": [4, 1, 2, 3, 3, 4, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1],
    }


def travel_time():
    return {
        "_source": "logic-specs/terrain.md section 2 step 4 [C][L] (PHANT.PRG 0x612c -> 0xef62): terrain class 0..3 "
                   "costs (class + 1) * 75, every other class 60 (lava, mist, dark voids, water and the path alike).",
        "_d2_040": "D2-040 (user s81: manual, as I's D-055): the rulebook's Apple-section terrain legend (CB p.18) says path "
                   "travel 'takes less time relative to the other types of terrain', so the path (class 6) costs 30. The "
                   "ST charges it 60 like water, mist and lava ([75, 150, 225, 300] then 60): rules option p2.path-time = "
                   "st plays that table.",
        "_wilderness_inn_dos": "Not decoded for II [?]: the field is left out (I's DOS value is 1000).",
        "step_cost_by_terrain_class": [75, 150, 225, 300, 60, 60, 30],
        "step_cost_other": 60,
    }


def manifest(built):
    return {
        "_schema": "phantasie.game/v1",
        "_source": "Built by tools/scripts/build_p2_gamedata.py from " + SOURCE,
        "_note": "The tables this folder holds and their sizes, read by OpenPhantasie.Data GameTables.Load (and "
                 "GameDataRepository.Load for play, with Core's Phantasie2Campaign; 09-trilogy.md T3). Its Atari ST graphics pack is graphics/atari-st.json "
                 "(not built by this script).",
        "game": GAME,
        "platform": "atari-st",
        "counts": {
            "monsters": len(built["monsters.json"]["monsters"]),
            "items": len(built["items.json"]["items"]),
            "spells": len(built["spells.json"]["spells"]),
            "classes": len(built["names.json"]["classes"]),
            "races": len(built["names.json"]["races"]),
            "overworld_regions": len(built["overworld.json"]["regions"]),
            "dungeon_levels": len(built["dungeons.json"]["levels"]),
            "message_areas": len(built["messages.json"]["areas"]),
            "scrolls": len(built["scrolls.json"]["scrolls"]),
            "towns": len(built["towns.json"]["towns"]),
            "elementals": len(built["elementals.json"]["elementals"]),
        },
        "spell_cost_rule": None,
        "_spell_cost_rule": "I: (DOS slot % 4) + 1 for every spell (OV2). II uses the same rule on its own slots [C] "
                            "(magic-divine.md section 1: DIVINE II slot 56 = 1 MP, DIVINE I slot 57 = 2 MP); every "
                            "spells.json cost is filled, but the checked rule stays null because the loader's slot map "
                            "(SpellId.DosSlot) only knows I's numbering.",
    }


def build():
    nm = load(SRC, "names.json")
    mon = load(SRC, "monsters.json")
    built = {
        "monsters.json": monsters(mon, load(SRC, "graphics", "extraction-index.json")),
        "monster-traits.json": monster_traits(mon, load(P1, "monster-traits.json"), load(P1, "monsters.json")),
        "items.json": items(nm["items"], load(P1, "items.json")["items"]),
        "spells.json": spells(nm, load(P1, "spells.json")["spells"]),
        "names.json": names(nm, mon),
        "overworld.json": overworld(load(SRC, "maps", "overworld.json")),
        "dungeons.json": dungeons(load(SRC, "dungeons", "dungeons.json"), load(SRC, "dungeons", "messages.json")),
        "messages.json": messages(load(SRC, "dungeons", "messages.json")),
        "scrolls.json": scrolls(load(SRC, "scrolls", "scrolls.json")),
        "towns.json": towns(load(SRC, "towns", "towns.json")),
        "creation-tables.json": creation(load(SRC, "towns", "creation-tables.json"), load(P1, "creation-tables.json")),
        "elementals.json": elementals(load(SRC, "towns", "elementals.json")),
        "rules.json": rules(),
        "travel-time.json": travel_time(),
        "wilderness-encounters.json": wilderness_encounters(),
    }
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
            with open(path, encoding="utf-8") as f:
                if f.read() != text:
                    bad.append(name)
        else:
            with open(path, "w", encoding="utf-8") as f:
                f.write(text)
            print(f"{path} ({len(text)} B)")
    if bad:
        sys.exit("out of date: " + ", ".join(bad))


if __name__ == "__main__":
    main()
