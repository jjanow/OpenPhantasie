#!/usr/bin/env python3
"""Phantasie II (Apple II, SSI v1.0 2/5/86): build II's GameData-shaped tables from the two RDOS disk sides.

Reference extractor for the first-run importer (docs/plan/10-importer.md section 6, decision 2: II is built from the
Apple II version, the Memorial Set's II). Format notes: reverse-engineering/apple2/phantasie2/FORMAT.md.

Input : side A (boot / town / outdoors) and side B (dungeon) `.dsk` images (4am crack, RDOS 2.1 13-sector layout).
Output: reverse-engineering/apple2/phantasie2/extracted/*.json (default) or --out DIR.

Every table is read from a pristine file (never from a save the game rewrites):

| Output              | Apple II source (side)                                   | Notes                                  |
|---------------------|----------------------------------------------------------|----------------------------------------|
| monsters.json       | MN20..MN29 (A or B; identical), 8 text records x 25 each | field order = OUTDOORS2 line 910 INPUT |
| items.json          | ITEM (A, 180 CR-ended names) + TINFO +89+id (factors)    | ids 1..180; id 0 is not on the disk    |
| names.json          | TOWN2 DATA 260 / 270, OUTDOORS2 290 (status), ITEM, MN2x | no spell names on the Apple (numbers)  |
| creation-tables.json| TINFO (A, 1730 B at $08FC = XA+310, XA = 1990)            | raw cells, ST layout names             |
| towns.json          | TF2 (A, 10 x 250 B new-game template; TWN2n are played)  | BACKUP line 500-550 copies TF2         |
| overworld.json      | OUTT<n> (A, 17 regions; play rewrites the fog only)      | grid (25-x)*20+y like the ST           |
| dungeons.json       | DNG1..8 (B, 6140 B): bytes 0..1739 level, 1740.. text    | messages = 40-B slots from 1700        |
| scrolls.json        | SCROLLL1..20 (A), SCROLLL0 (B): 40x24 text-screen dumps  | chained while row 22 col 0 is '!'      |

The JSON keeps the ST GameData field names where a field means the same thing, so `diff_a2_p2_vs_st.py` can compare
field by field. Apple-only fields sit under an `a2` key. Text is decoded the way the Apple shows it: high bit
stripped, then played onto a 40-column virtual screen (BS moves the cursor left, CR ends the line, BEL / ESC are not
printed), so typos the original fixed with backspaces come out as displayed ("DEMONICA<BS><BS>AC" -> "DEMONIAC").

Usage: python3 -I tools/scripts/extract_a2_p2_tables.py [--side-a A.dsk] [--side-b B.dsk] [--out DIR]
Port notes: pure functions `extract_*(disk_a, disk_b) -> dict`; no globals besides constants.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from a2_applesoft import program  # noqa: E402
from a2_rdos import RdosDisk  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
IMAGES = ROOT / "reverse-engineering" / "apple2" / "original-images" / "phantasie2" / "4am-crack"
SIDE_A = IMAGES / "phantasie2-4am-side-a.dsk"
SIDE_B = IMAGES / "phantasie2-4am-side-b.dsk"
OUT = ROOT / "reverse-engineering" / "apple2" / "phantasie2" / "extracted"
SOURCE = "Phantasie II, Apple II v1.0 2/5/86 (4am crack; reverse-engineering/apple2/phantasie2/FORMAT.md)"

REGIONS = [1, 2, 3, 5, 6, 7, 8, 11, 12, 17, 18, 22, 26, 27, 28, 30, 31]   # OUTT<n> files on side A
CLASSES_ORDER = ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"]
ATTRS = ["STR", "INT", "DEX", "CON", "CHA"]
SKILLS = ["attack", "parry", "find_item", "spot_trap", "disarm_trap", "listen", "_42_toss", "pick_lock",
          "_unused_46", "swim"]


# ---- text helpers --------------------------------------------------------------------------------------------------

def screen_text(raw: bytes, width: int = 40, stop_at_cr: bool = False) -> list[str]:
    """Play bytes onto a virtual `width`-column screen as COUT does; return the lines, right-stripped."""
    lines: list[list[str]] = [[]]
    row, col = 0, 0
    for b in raw:
        c = b & 0x7F
        if c == 0x0D:
            if stop_at_cr:
                break
            row, col = row + 1, 0
        elif c == 0x08:
            if col > 0:
                col -= 1
            elif row > 0:
                row, col = row - 1, width - 1
            continue
        elif c < 0x20 or c == 0x7F:
            continue
        else:
            while len(lines) <= row:
                lines.append([])
            line = lines[row]
            while len(line) <= col:
                line.append(" ")
            line[col] = chr(c)
            col += 1
            if col == width:
                row, col = row + 1, 0
        while len(lines) <= row:
            lines.append([])
    out = ["".join(x).rstrip() for x in lines]
    while out and not out[-1]:
        out.pop()
    return out


def cr_string(raw: bytes) -> str:
    """One CR-ended string as displayed (backspaces applied), lines joined by a space."""
    return " ".join(s.strip() for s in screen_text(raw, stop_at_cr=True))


def text_page(raw: bytes) -> list[str]:
    """A 1000/1024-byte Apple text page ($400) -> 24 rows of 40 chars (interleaved row layout)."""
    rows = []
    for r in range(24):
        o = (r % 8) * 0x80 + (r // 8) * 0x28
        chars = []
        for b in raw[o:o + 40]:
            c = b & 0x7F
            chars.append(chr(c + 0x40) if c < 0x20 else chr(c))   # inverse / flash glyphs @A..Z
        rows.append("".join(chars).rstrip())
    return rows


def sha1(b: bytes) -> str:
    return hashlib.sha1(b).hexdigest()


# ---- monsters ------------------------------------------------------------------------------------------------------

MONSTER_FIELDS = ["name", "armor", "damage", "level", "xp", "special_chance", "special_0", "special_1", "special_2",
                  "hp_base", "combat_rating", "companion_0", "companion_1", "a2_scale", "a2_hcolor",
                  "reaction_modifier", "next_group_chance", "defense", "gold", "group_size_factor",
                  "a2_group_height", "treasure_0", "treasure_1", "attack_power", "a2_shape"]


def extract_monsters(disk_a: RdosDisk) -> dict:
    """MN2<f> holds monsters 8f..8f+7 as CR-ended Applesoft INPUT fields (OUTDOORS2 / DUNGON2 lines 900-915)."""
    out = []
    for f in range(10):
        raw = disk_a.read(f"MN2{f}")
        fields = bytes(c & 0x7F for c in raw).split(b"\r")
        for h in range(8):
            rec = fields[h * 25:(h + 1) * 25]
            v = dict(zip(MONSTER_FIELDS, [cr_string(rec[0])] + [int(x) for x in rec[1:]]))   # BEL etc. dropped
            idx = f * 8 + h
            out.append({
                "id": f"p2:{idx}", "index": idx, "name": v["name"],
                "armor": v["armor"], "damage": v["damage"], "level": v["level"], "xp": v["xp"],
                "special_chance": v["special_chance"],
                "specials": [v["special_0"], v["special_1"], v["special_2"]],
                "hp_base": v["hp_base"], "combat_rating": v["combat_rating"],
                "companions": [v["companion_0"], v["companion_1"]],
                "reaction_modifier": v["reaction_modifier"], "next_group_chance": v["next_group_chance"],
                "defense": v["defense"], "gold": v["gold"], "group_size_factor": v["group_size_factor"],
                "treasure_items": [v["treasure_0"], v["treasure_1"]], "attack_power": v["attack_power"],
                "a2": {"shape": v["a2_shape"], "scale": v["a2_scale"], "hcolor": v["a2_hcolor"],
                       "group_height": v["a2_group_height"]},
            })
    return {
        "_source": f"{SOURCE}: MN20..MN29, 8 records x 25 CR-ended fields (OUTDOORS2 line 910)",
        "_fields": "ST names where the field means the same (GameData/phantasie2/monsters.json). a2.shape = SHAPES "
                   "shape number of pose A (pose B = shape + 1), a2.scale = SCALE, a2.hcolor = HCOLOR and the "
                   "group's x offset (DUNGON2 1540-1550), a2.group_height = the rank's height in pixels "
                   "(OUTDOORS2 1480-1500; the ST keeps the sprite height for this). reaction_modifier < -10 marks "
                   "Pluto's minion -(10 + n) (OUTDOORS2 916).",
        "monsters": out,
    }


# ---- items / names -------------------------------------------------------------------------------------------------

def item_names(disk_a: RdosDisk) -> list[str]:
    raw = bytes(c & 0x7F for c in disk_a.read("ITEM"))
    names = raw.split(b"\r")[:180]
    return [cr_string(n) for n in names]   # 1..180


def extract_items(disk_a: RdosDisk) -> dict:
    names = item_names(disk_a)
    tinfo = disk_a.read("TINFO")
    items = [{"id": f"p2:{i}", "index": i, "name": names[i - 1], "value_factor": tinfo[89 + i]}
             for i in range(1, 181)]
    return {
        "_source": f"{SOURCE}: ITEM (names, ids 1..180, read by TOWN2 line 230) and TINFO +89+id (value factor; "
                   "price = INT(f^2 / 5 * town factor + 1), TOWN2 1117)",
        "_note": "Id 0 (NOTHING) has no name or factor on the Apple; kind / points / min_str are index formulas "
                 "shared with the ST build (GameData/phantasie2/items.json), not disk data.",
        "items": items,
    }


def extract_names(disk_a: RdosDisk) -> dict:
    town = program(disk_a, "TOWN2")
    outd = program(disk_a, "OUTDOORS2")
    classes = [x.strip() for x in town[260].split("DATA", 1)[1].split(",")]
    races = [x.strip() for x in town[270].split("DATA", 1)[1].split(",")]
    races = [r for r in races if r]
    status = []
    for part in outd[290].split(":"):          # S$(0) = "DEAD":S$(1) = "OKAY":S$(2) = "SLEEP"
        if '"' in part:
            status.append(part.split('"')[1])
    terrain = [x.strip() for x in outd[120].split("DATA", 1)[1].split(",")]
    item_kinds = [x.strip() for x in outd[280].split("DATA", 1)[1].split(",")]
    mons = extract_monsters(disk_a)["monsters"]
    return {
        "_source": f"{SOURCE}: TOWN2 lines 260 / 270 (DATA), OUTDOORS2 lines 120 / 280 / 290, ITEM, MN2x",
        "_note": "The Apple II has no spell names: spells are listed and cast by number 1..60 (TOWN2 3060, 6882).",
        "classes": [{"index": i, "name": n} for i, n in enumerate(classes)],
        "races": [{"index": i, "name": n} for i, n in enumerate(races)],
        "status": [{"index": i, "name": n} for i, n in enumerate(status)],
        "items": [{"index": i + 1, "name": n} for i, n in enumerate(item_names(disk_a))],
        "monsters": [{"index": m["index"], "name": m["name"]} for m in mons],
        "spells": [],
        "a2": {"terrain_names": terrain, "treasure_kinds": item_kinds},
    }


# ---- creation tables (TINFO) ---------------------------------------------------------------------------------------

TINFO_LAYOUT = {   # offset in TINFO (= address - 2300); TOWN2 reads XA + n with XA = 1990
    "hp_factors": 0,              # XA+310 + class*15 + level   (TOWN2 1520)
    "value_factors": 89,          # XA+399 + item               (TOWN2 1117)
    "race_attribute_modifiers": 275,   # XA+585 + race*5 + attr (TOWN2 2260)
    "class_attribute_modifiers": 350,  # XA+660 + class*5 + attr (TOWN2 2250)
    "spell_levels": 380,          # XA+690 + class*60 + spell   (TOWN2 1570)
    "mp_ceiling": 740,            # XA+1050 + class*15 + level  (TOWN2 1505)
    "skill_coefficients": 830,    # XA+1140 + class*150 + level*10 + skill (TOWN2 1512), one byte each
}


def extract_creation_tables(disk_a: RdosDisk) -> dict:
    t = disk_a.read("TINFO")
    L = TINFO_LAYOUT
    races = [r["name"] for r in extract_names(disk_a)["races"]][:15]
    by = lambda off, n: {c: list(t[off + i * n:off + (i + 1) * n]) for i, c in enumerate(CLASSES_ORDER)}  # noqa: E731
    return {
        "_schema": "a2 raw creation tables (cells named as in GameData/phantasie2/creation-tables.json)",
        "_source": f"{SOURCE}: TINFO, 1730 B loaded at $08FC (TOWN2 830); offsets in a2_offsets",
        "a2_offsets": L,
        "hp_factors": {"layout": "class * 15 + level", "by_class": by(L["hp_factors"], 15)},
        "mp_ceiling": {"layout": "class * 15 + level", "by_class": by(L["mp_ceiling"], 15)},
        "spell_levels": {"layout": "class * 60 + spell slot; 0 = never", "by_class": by(L["spell_levels"], 60)},
        "skill_coefficients": {
            "layout": "class * 150 + level * 10 + skill (u8; the ST stores u16)", "skill_order": SKILLS,
            "by_class_level": {c: [list(t[L["skill_coefficients"] + i * 150 + lv * 10:
                                     L["skill_coefficients"] + i * 150 + lv * 10 + 10]) for lv in range(15)]
                               for i, c in enumerate(CLASSES_ORDER)}},
        "class_attribute_modifiers": {"by_class": {c: dict(zip(ATTRS, t[L["class_attribute_modifiers"] + i * 5:
                                                                        L["class_attribute_modifiers"] + i * 5 + 5]))
                                                   for i, c in enumerate(CLASSES_ORDER)}},
        "race_attribute_modifiers": {"by_race": {r: {"modifiers": dict(zip(ATTRS, t[L["race_attribute_modifiers"] + i * 5:
                                                                                 L["race_attribute_modifiers"] + i * 5 + 5]))}
                                                 for i, r in enumerate(races)}},
        "a2_unused_bytes": {"tinfo_270_274": list(t[270:275])},
    }


# ---- towns ---------------------------------------------------------------------------------------------------------

def extract_towns(disk_a: RdosDisk) -> dict:
    tf = disk_a.read("TF2")
    towns = []
    for n in range(10):
        r = tf[n * 250:(n + 1) * 250]
        towns.append({
            "id": n + 1, "index": n, "name": cr_string(r[230:250].replace(b"\x15", b"")),
            "price_index": r[226],
            "stock": [0] + list(r[0:180]),
            "restock_items": list(r[180:225]),
            "exit_region": r[227], "exit_x": 25 - r[229], "exit_y": r[228],
            "a2": {"restock_slot_45": r[225], "name_raw": r[230:250].hex()},
        })
    return {
        "_source": f"{SOURCE}: TF2 (10 x 250 B, the new-game template BACKUP 500-550 copies to TWN21..TWN210)",
        "_layout": {"record_stride": 250, "stock": "bytes 0..179 = item ids 1..180 (LA+1+id-1)",
                    "restock_items": "180..224 (TOWN2 860 reads LA+181+RND*L*3, L <= 15 -> slots 0..44)",
                    "slot_45": "225, never read", "price_index": "226 (TOWN2 825: /100)",
                    "exit": "227 region, 228 row y, 229 column 25 - x (TOWN2 6020 -> SA+1191..1193)",
                    "name": "230..249, padded with 0x15"},
        "towns": towns,
    }


# ---- overworld -----------------------------------------------------------------------------------------------------

def extract_overworld(disk_a: RdosDisk) -> dict:
    regions = {}
    for r in REGIONS:
        o = disk_a.read(f"OUTT{r}")
        grid = o[:520]
        tiles = [[grid[(25 - x) * 20 + y] for x in range(26)] for y in range(20)]
        feats = []
        for i in range(15):
            b = o[520 + i * 5:525 + i * 5]
            feats.append({"index": i, "x": 25 - b[1], "y": b[0], "arg": b[2], "dest_x": b[3], "dest_y": b[4]})
        pages = []
        for p in range((len(o) - 595) // 40):
            raw = o[595 + 40 * p:]
            first = bytes(c & 0x7F for c in o[595 + 40 * p:635 + 40 * p])
            if first.strip(b"\r\x00 "):
                pages.append({"index": p, "text": cr_string(raw)})
        for f in feats:   # text cells (tile % 10 == 6): the string the game prints (OUTDOORS2 6000-6020)
            if f["x"] in range(26) and f["y"] in range(20) and tiles[f["y"]][f["x"]] % 120 % 10 == 6:
                f["a2_text"] = cr_string(o[595 + 40 * f["arg"]:])
        regions[f"OUT{r}.DAT"] = {"region_id": r, "size": len(o), "grid_width": 26, "grid_height": 20,
                                  "tiles": tiles, "features": feats, "a2_text_pages": pages,
                                  "a2_played": len(o) == 756}
    return {
        "_source": f"{SOURCE}: OUTT<n> (768 B as shipped; OUTDOORS2 450). "
                   "OUTDOORS2 7700-7710 STOREs the region back (756 B) on leaving it; the only bytes play changes are "
                   "the fog flags (line 5030), so a stored-back file (a2_played) has the true tiles / features / text but "
                   "maybe not the starting fog. OUT2 ships stored back (the Memorial Set's unplayed disk, s166: OUT1 / OUT2 "
                   "open, the rest fogged, as the ST); a played copy is told by the profile's pristine sha1s. In the "
                   "reference dump only OUT2, OUT26 and OUT30 are as shipped",
        "_note": "OUTDOORS2 reads cell (CX, CY) at LA + CY*20 + CX (CX 0..19, CY 0..25): the ST's (25 - x) * 20 + y "
                 "with CX = y, CY = 25 - x, so tiles here are ST-oriented [y][x] rows. The Apple draws CX across "
                 "and CY down (140 + 7*CX, 2 + 6*CY), i.e. the ST map turned a quarter. Fog = tile + 120. Features "
                 "520..594 (CX, CY, arg, dest...). Text: one CR-ended string from 595 + 40*arg (CALL 800), "
                 "a2_text_pages lists each non-empty start slot as displayed.",
        "regions": regions,
    }


# ---- dungeons and messages -----------------------------------------------------------------------------------------

LEVEL_BYTES = 1740       # level data (grid, monsters, events, scripts) = file 0..1739; text slots from 1740
MSG_BASE = 1700          # DUNGON2 8650: message M1 at 3750 + 40*M1 = LA(2050) + 1700 + 40*M1


def extract_dungeons(disk_b: RdosDisk) -> dict:
    levels = {}
    for n in range(1, 9):
        d = disk_b.read(f"DNG{n}")
        msgs = {}
        covered_to = 0          # file offset up to which the previous message's text (to its CR) runs
        for mid in range(1, (len(d) - MSG_BASE) // 40):
            start = MSG_BASE + 40 * mid
            slot = bytes(c & 0x7F for c in d[start:start + 40])
            if start < covered_to or not slot.strip(b"\r\x00 "):
                continue        # continuation of the previous message, or empty (CR fill)
            end = start + bytes(c & 0x7F for c in d[start:]).find(b"\r")
            covered_to = end + 1
            msgs[str(mid)] = {"lines": screen_text(d[start:end + 1], stop_at_cr=True),
                              "a2_slots": (end - start) // 40 + 1}
        levels[f"DNG{n}"] = {"id": f"p2:{n}", "size": len(d), "raw_hex": d[:LEVEL_BYTES].hex(),
                             "messages": msgs,
                             "a2_text_stream": [ln for m in msgs.values() for ln in m["lines"]]}
    return {
        "_source": f"{SOURCE}: DNG1..DNG8 (6140 B, side B). Bytes 0..1739 = the Phantasie I level format (grid "
                   "33 x 38 at LA + 33*CY + CX, events at 1270 = 3320 - LA, scripts at 1450 = 3500 - LA, level "
                   "byte 1255), the rest = 40-B message slots (DUNGON2 8650, printed by CALL 800 until CR)",
        "_note": "Messages are listed by slot id; a message that runs past 40 chars continues in the next slot "
                 "(the ST cuts the same text into 41-B records). raw_hex holds only the level part.",
        "levels": levels,
    }


# ---- scrolls -------------------------------------------------------------------------------------------------------

def extract_scrolls(disk_a: RdosDisk, disk_b: RdosDisk) -> dict:
    pages = {}
    for f in disk_a.files:
        if f.name.startswith("SCROLLL") and not f.deleted:
            pages[int(f.name[7:])] = text_page(disk_a.read(f))
    chains = []
    for item in range(121, 141):
        k = item - 120
        if k not in pages:
            continue
        chain = [k]
        while pages[chain[-1]][22][:1] == "!" and chain[-1] + 1 in pages:   # TOWN2 8020: PEEK(1872) = '!'
            chain.append(chain[-1] + 1)
        chains.append({"item_id": item, "files": [f"SCROLLL{c}" for c in chain],
                       "lines": [ln for c in chain for ln in pages[c]]})
    return {
        "_source": f"{SOURCE}: SCROLLL1..20 (side A, 1000 B text-screen dumps loaded at $400; TOWN2 8000-8020: item "
                   "i shows SCROLLL(i - 120) and the next file while row 22 column 0 is '!')",
        "scrolls": chains,
        "a2_pluto_speech": {"file": "SCROLLL0", "side": "B", "shown": "DUNGON2 4300: after a win over monster 79",
                            "lines": text_page(disk_b.read("SCROLLL0"))},
        "a2_pages": {f"SCROLLL{k}": v for k, v in sorted(pages.items())},
    }


# ---- driver --------------------------------------------------------------------------------------------------------

KEY_FILES_A = ["PHANTASIE II", "TOWN2", "OUTDOORS2", "ITEM", "TINFO", "TF2", "SHAPES", "ICE DRAGON", "INFO"]
KEY_FILES_B = ["DUNGON2", "DNG1", "DNG8", "SCROLLL0"]


def build(side_a: Path, side_b: Path) -> dict[str, dict]:
    a, b = RdosDisk(side_a), RdosDisk(side_b)
    for i in range(10):     # the monster files are on both sides and must agree
        if a.read(f"MN2{i}") != b.read(f"MN2{i}"):
            raise SystemExit(f"MN2{i} differs between the sides")
    return {
        "monsters.json": extract_monsters(a),
        "items.json": extract_items(a),
        "names.json": extract_names(a),
        "creation-tables.json": extract_creation_tables(a),
        "towns.json": extract_towns(a),
        "overworld.json": extract_overworld(a),
        "dungeons.json": extract_dungeons(b),
        "scrolls.json": extract_scrolls(a, b),
        "key-files.json": {
            "_note": "sha1 of the read-only files the extract reads (importer detection, 10-importer.md 3.2). "
                     "SSI.INIT2 is left out: Passport patched it in the crack (4am-notes T0E,S07).",
            "side_a": {n: sha1(a.read(n)) for n in KEY_FILES_A},
            "side_b": {n: sha1(b.read(n)) for n in KEY_FILES_B},
        },
    }


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--side-a", type=Path, default=SIDE_A)
    ap.add_argument("--side-b", type=Path, default=SIDE_B)
    ap.add_argument("--out", type=Path, default=OUT)
    args = ap.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    for name, data in build(args.side_a, args.side_b).items():
        (args.out / name).write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
        print("wrote", args.out / name)


if __name__ == "__main__":
    main()
