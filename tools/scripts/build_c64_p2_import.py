#!/usr/bin/env python3
"""Build `GameData/phantasie2/c64-import.json`: the Atari ST's numbers for a Phantasie II import from the C64 copy alone.

The C64 import (`p2-c64`, `OpenPhantasie.Importer.Pipelines.P2C64.P2C64Tables`) keeps the C64's text and takes the ST's
value wherever a C64 number differs (as the Apple import, DIFFERENCES-II D2-046 .. D2-050; the C64-only numbers the same
way, `reverse-engineering/c64/phantasie2/FORMAT.md` section 7). This script reads the C64 v1.2 disks the way the importer
does, compares them with the ST-built GameData of the private repo, and writes what the importer needs and the public
repo can't hold otherwise:

- `st_cells` (CellPatches, as `apple2-import.json`): monsters, towns, creation tables, overworld grids and features,
  dungeon bytes (the level part only), elementals;
- `sprite_sizes`: the ST's 80 monster sprite (width, height) pairs (II's group fit reads the height), which the C64 has
  no table for;
- `spell_names`: the manual's spell table (the C64 has no spell names; D2-050, as `apple2-import.json`'s).

Every cell is checked against the lists of FORMAT.md 3.1 / 3.3 / 3.4 / 3.6 / 3.7 (the counts below), so a new
difference stops the script. Reads the v1.2 G64 pair (`original-images/phantasie2/c64pp/`). Run from the repo root with
`python3 -I tools/scripts/build_c64_p2_import.py` (`--check` compares with the committed file).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import extract_c64_d64 as d64  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
GD = REPO / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie2"
OUT = GD / "c64-import.json"
IMAGES = REPO / "reverse-engineering" / "c64" / "original-images" / "phantasie2" / "c64pp"
SIDE1 = IMAGES / "phantasie_ii_s1[ssi_1986](v1_2)(never_used).g64"
SIDE2 = IMAGES / "phantasie_ii_s2[ssi_1986](v1_2)(never_used)(!).g64"

REGIONS = [1, 2, 3, 5, 6, 7, 8, 11, 12, 17, 18, 22, 26, 27, 28, 30, 31]
# PATTERN2 (side 2, the new-game template; FORMAT.md 2.1), offsets after its load address.
PATTERN_TOWNS, PATTERN_REGIONS = 4260, 6780
CHAR, ELEMENTAL_FIRST = 170, 21
# The level part of a DNG file (grid, level byte, forced table, exits, events, scripts; FORMAT.md 3.7). After it the
# C64 has CR fill and the ST zeros and leftovers: no cells there.
LEVEL_PART = 1740

# MN field k -> the ST's field (FORMAT.md 3.1): 0-12 and 15-23 in the ST's order, 20 = byte_34; 13, 14, 24 the C64's.
MN_FIELDS = ["name", "armor", "damage", "level", "xp", "special_chance", "special_0", "special_1", "special_2",
             "hp_base", "combat_rating", "companion_0", "companion_1", None, None, "reaction_modifier",
             "next_group_chance", "defense", "gold", "group_size_factor", "byte_34", "treasure_0", "treasure_1",
             "attack_power", None]

# The manual's spell table (combined rulebook p.12), as apple2-import.json's spell_names (D2-050): the same list.
APPLE_IMPORT = GD / "apple2-import.json"


def files(image: Path) -> dict[str, bytes]:
    img = d64.load_image(image)
    return {e["name"]: d64.read_chain(img, *e["start"]) for e in d64.directory(img)}


def body(prg: bytes) -> bytes:
    return prg[2:]


# ---- the C64 tables in the ST's extract shape (as P2C64Tables) -----------------------------------------------------

def monsters(mn: bytes) -> list[dict]:
    out = []
    for i in range(80):
        f = mn[i * 100:(i + 1) * 100].split(b"\r")
        v = {k: int(f[n]) for n, k in enumerate(MN_FIELDS) if k and k != "name"}
        out.append({
            "index": i, "name": f[0].decode("latin-1"),
            **{k: v[k] for k in ("armor", "damage", "level", "xp", "special_chance")},
            "specials": [v["special_0"], v["special_1"], v["special_2"]],
            "hp_base": v["hp_base"], "combat_rating": v["combat_rating"],
            "companions": [v["companion_0"], v["companion_1"]],
            **{k: v[k] for k in ("reaction_modifier", "next_group_chance", "defense", "gold", "group_size_factor",
                                 "byte_34")},
            "treasure_items": [v["treasure_0"], v["treasure_1"]], "attack_power": v["attack_power"],
        })
    return out


def town(rec: bytes) -> dict:
    return {"price_index": rec[226], "stock": [0] + list(rec[:180]), "restock_items": list(rec[180:225]),
            "exit_region": rec[227], "exit_x": 25 - rec[229], "exit_y": rec[228]}


def tinfo_tables(t: bytes) -> dict:
    """TINFO in the ST's creation-table shape (I's / the Apple's TINFO layout, FORMAT.md 3.3)."""
    classes = ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"]
    races = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC",
             "GOBLIN", "LIZ.MAN", "MINOTAUR"]
    stats = ["STR", "INT", "DEX", "CON", "CHA"]
    return {
        "hp_factors": {c: list(t[i * 15:i * 15 + 15]) for i, c in enumerate(classes)},
        "mp_ceiling": {c: list(t[740 + i * 15:740 + i * 15 + 15]) for i, c in enumerate(classes)},
        "spell_levels": {c: list(t[380 + i * 60:380 + i * 60 + 60]) for i, c in enumerate(classes)},
        "skills": {c: [list(t[830 + i * 150 + lv * 10:830 + i * 150 + lv * 10 + 10]) for lv in range(15)]
                   for i, c in enumerate(classes)},
        "class_mods": {c: {s: t[350 + i * 5 + k] for k, s in enumerate(stats)} for i, c in enumerate(classes)},
        "race_mods": {r: {s: t[275 + i * 5 + k] for k, s in enumerate(stats)} for i, r in enumerate(races)},
    }


def screen_page(b: bytes) -> str:
    """A 40-byte text page in screen codes, as ASCII (P1C64Tables.ScreenPage): A = 1; CR padding is no page."""
    if all(c == 0x0D for c in b):
        return ""
    out = bytes(c + 0x40 if 1 <= c <= 26 else 0x40 if c == 0 else c for c in b)
    return out.split(b"\0")[0].decode("latin-1").strip()


def region(o: bytes) -> dict:
    tiles = [[o[(25 - x) * 20 + y] for x in range(26)] for y in range(20)]
    feats = [{"index": i, "x": 25 - o[521 + 5 * i], "y": o[520 + 5 * i], "arg": o[522 + 5 * i],
              "dest_x": o[523 + 5 * i], "dest_y": o[524 + 5 * i]} for i in range(15)]
    return {"tiles": tiles, "features": feats}


def elemental(rec: bytes) -> dict:
    return {
        "intelligence": rec[2], "mp_ceiling": rec[9], "magic_max": rec[10], "magic_cur": rec[11], "hp_max": rec[12],
        "hp_cur": rec[13], "status": rec[18], "attack": rec[20], "parry": rec[22], "level": rec[54],
        "guild_number": rec[59], "weapon_damage_b": rec[162], "shield_soak": rec[163], "armor_soak": rec[164],
        "race": rec[161], "birth_race": rec[161], "class": rec[151],
        "known_spells": [k for k in range(60) if rec[61 + k] == 1],
    }


# ---- the expected differences (FORMAT.md section 3) -----------------------------------------------------------------

EXPECTED = {
    # 3.1: the five D2-047 monsters (6 cells), CLOUD GIANT xp + companions, the 17 smaller group sizes + BABY DRAGON,
    # byte_34 in 11 records.
    "monsters.json": 6 + 2 + 18 + 11,
    # 3.3 / D2-049: two skill cells (HALFLING DEX / MONK SLEEP are the manual's on both sides after D2-009).
    "creation-tables.json": 2,
    # 3.4: D2-048's four scroll-stock cells, FERRON's MAGIC 5.
    "towns.json": 5,
    # 3.6: OUT3 x4, OUT17, OUT31 x2 (D2-049); OUT5 x3; the water pairs (OUT26/27/28/30, 2 each); OUT28's 8 entrance
    # cells; OUT1's junk feature record 14 (dest_y, D2-049); OUT5's junk feature record 5 (its y).
    "overworld.json": 4 + 1 + 2 + 3 + 8 + 8 + 1 + 1,
    # 3.7: cells DNG1 1, DNG2 4, DNG3 1, DNG5 5, DNG6 1, DNG8 1; DNG7's exit records (5 of their 9 bytes differ);
    # the script-area bytes DNG1 1, DNG3 1, DNG5 2, DNG7 1.
    "dungeons.json": 1 + 4 + 1 + 5 + 1 + 1 + 5 + 5,
    # 3.5: each elemental's MP ceiling (P(9) 0, the ST 20), creation number (38-40, the ST 23-25) and race (P(161) 16,
    # the ST 15; birth_race 16 on both).
    "elementals.json": 9,
}


def main() -> int:
    s1, s2 = files(SIDE1), files(SIDE2)
    st = {n: json.loads((GD / n).read_text()) for n in
          ("monsters.json", "overworld.json", "dungeons.json", "elementals.json", "towns.json", "creation-tables.json")}
    cells: list[dict] = []

    def cell(why: str, file: str, path: list, c64, stv, **extra) -> None:
        cells.append({"why": why, "file": file, "path": path, **extra, "c64": c64, "st": stv})

    # monsters: every number (side 1's MN; OUTDOORS2 reads it, side 2's differs in BABY DRAGON / APPRENTICE DEVIL)
    shared = {6: "D2-047", 53: "D2-047", 74: "D2-047", 76: "D2-047", 77: "D2-047"}
    for c, m in zip(monsters(s1["MN"]), st["monsters.json"]["monsters"]):
        for k, v in c.items():
            if k in ("index", "name"):
                continue
            if v != m[k]:
                tag = shared.get(c["index"], "C64") if k != "byte_34" else "C64 (picture layout)"
                cell(f"{tag}: {m['name']} {k} (FORMAT.md 3.1)", "monsters.json",
                     ["monsters", {"index": c["index"]}, k], v, m[k])
    # side 2's MN: the same numbers but two (FORMAT.md 3.1); the importer reads side 1's.
    side2 = {(c["index"], k): v for c in monsters(s2["MN"]) for k, v in c.items()}
    diff2 = sorted((i, k) for (i, k), v in side2.items() if k not in ("index", "name")
                   and v != monsters(s1["MN"])[i][k])
    assert diff2 == [(12, "treasure_items"), (30, "group_size_factor")], diff2
    sprite_sizes = [[m["sprite_width"], m["sprite_height"]] for m in st["monsters.json"]["monsters"]]

    # creation tables: TINFO against the built ST file (D2-009 / D2-015 cells are the manual's on both)
    t = body(s1["TINFO"])
    ct, sct = tinfo_tables(t), st["creation-tables.json"]
    for c, rows in ct["skills"].items():
        for lv, row in enumerate(rows):
            for k, v in enumerate(row):
                sv = sct["skill_coefficients"]["by_class_level"][c][lv][k]
                if v != sv:
                    cell(f"D2-049: {c} level {lv + 1} skill {k}", "creation-tables.json",
                         ["skill_coefficients", "by_class_level", c, lv, k], v, sv)
    for key, src in (("hp_factors", "hp_factors"), ("mp_ceiling", "mp_ceiling"), ("spell_levels", "spell_levels")):
        for c, row in ct[src].items():
            for k, v in enumerate(row):
                sv = sct[key]["by_class"][c][k]
                if v != sv and not (key == "spell_levels" and (c, k) in (("MONK", 41), ("WIZARD", 43))):
                    raise AssertionError(f"TINFO {key} {c} {k}: {v} vs {sv}")
    for c, mods in ct["class_mods"].items():
        assert mods == sct["class_attribute_modifiers"]["by_class"][c], c
    for r, mods in ct["race_mods"].items():
        assert mods == sct["race_attribute_modifiers"]["by_race"][r]["modifiers"], r
    assert ct["spell_levels"]["MONK"][41] == sct["spell_levels"]["by_class"]["MONK"][41]  # D2-009: 0 on both
    assert ct["spell_levels"]["WIZARD"][43] == sct["dos_tables"]["spell_levels"]["WIZARD"]["43"]  # D2-015: 0 = the ST's
    # items: ITEM's 180 names (its empty line 46 skipped, FORMAT.md 3.2) and TINFO 89 + id: every value the ST's
    sit = json.loads((GD / "items.json").read_text())["items"]
    names = [n.decode("latin-1") for n in s1["ITEM"].split(b"\r") if n][:180]
    assert len(names) == 180
    assert [t[89 + i] for i in range(1, 181)] == [it["value_factor"] for it in sit[1:181]]
    assert [i + 1 for i, n in enumerate(names) if n.strip() != sit[i + 1]["name"].strip()] == [53], "item names (FORMAT.md 3.2)"

    # towns: PATTERN2's ten (FORMAT.md 3.4)
    pattern = body(s2["PATTERN2"])
    d2048 = {(4, 126), (4, 127), (5, 126), (5, 127)}
    for n in range(10):
        c = town(pattern[PATTERN_TOWNS + 252 * n:PATTERN_TOWNS + 252 * n + 250])
        s = st["towns.json"]["towns"][n]
        for k, v in c.items():
            if k == "stock":
                for i, (a, b) in enumerate(zip(v, s["stock"])):
                    if a != b:
                        tag = "D2-048" if (n, i) in d2048 else "C64"
                        cell(f"{tag}: {s['name']} stock of item {i}", "towns.json",
                             ["towns", {"index": n}, "stock", i], a, b)
            elif v != s[k]:
                cell(f"C64: {s['name']} {k}", "towns.json", ["towns", {"index": n}, k], v, s[k])

    # overworld: PATTERN2's 17 regions, grids (fog kept) and feature records
    text_diffs: list[str] = []
    shared_tiles = {("OUT3", 5, y) for y in range(12, 16)} | {("OUT17", 20, 6), ("OUT31", 17, 18), ("OUT31", 17, 19)}
    for k, n in enumerate(REGIONS):
        o = pattern[PATTERN_REGIONS + 752 * k:PATTERN_REGIONS + 752 * k + 750]
        c = region(o)
        reg = st["overworld.json"]["regions"][f"OUT{n}.DAT"]
        for y in range(20):
            for x in range(26):
                a, b = c["tiles"][y][x], reg["tiles"][y][x]
                au = a - 120 if 120 <= a < 255 else a
                bu = b - 120 if 120 <= b < 255 else b
                assert (a >= 120) == (b >= 120), f"OUT{n} ({x}, {y}): fog differs"
                if au != bu:
                    tag = "D2-049" if (f"OUT{n}", x, y) in shared_tiles else "C64"
                    cell(f"{tag}: OUT{n} ({x}, {y})", "overworld.json", ["regions", f"OUT{n}.DAT", "tiles", y, x],
                         au, bu, fog=True)
        for f, sf in zip(c["features"], reg["features"]):
            for key in ("x", "y", "arg", "dest_x", "dest_y"):
                if f[key] != sf[key]:
                    tag = "D2-049" if n == 1 else "C64 (junk record)"
                    cell(f"{tag}: OUT{n} feature record {f['index']}", "overworld.json",
                         ["regions", f"OUT{n}.DAT", "features", {"index": f["index"]}, key], f[key], sf[key])
        # text pages: every page the ST shows from 0-3 equals the C64's (normalised: case, blanks)
        pages = {p["index"]: p["text"] for p in reg["text_pages"]}
        for p in range(4):
            ctext = screen_page(o[595 + 40 * p:635 + 40 * p])
            if " ".join(ctext.split()) != " ".join(pages.get(p, "").split()):
                text_diffs.append(f"OUT{n} page {p}: {ctext!r} (ST {pages.get(p)!r})")

    # dungeons: every differing byte of the level part (FORMAT.md 3.7)
    d2049 = {(5, 0x4D1), (8, 0x447)}
    for n in range(1, 9):
        c = body(s2[f"DNG {n}"])
        raw = bytes.fromhex(st["dungeons.json"]["levels"][f"DNG{n}"]["raw_hex"])
        for at in range(LEVEL_PART):
            if c[at] != raw[at]:
                where = f"cell ({at % 33}, {at // 33})" if at < 1254 else \
                    "exit record" if n == 7 and 0x4F2 <= at <= 0x4FA else "script area"
                tag = "D2-049" if (n, at) in d2049 else "decided s217 (ST exits)" if where == "exit record" else "C64"
                cell(f"{tag}: DNG{n} byte 0x{at:03x} = {where}", "dungeons.json", ["levels", f"DNG{n}", "raw_hex"],
                     c[at], raw[at], byte=at)

    # elementals: PATTERN2's CHAR22 records 21-23
    for i in range(3):
        rec = pattern[(ELEMENTAL_FIRST + i) * CHAR:(ELEMENTAL_FIRST + i + 1) * CHAR]
        e = st["elementals.json"]["elementals"][i]
        for k, v in elemental(rec).items():
            if v != e[k]:
                cell(f"C64: elemental {i} {k} (FORMAT.md 3.5)", "elementals.json", ["elementals", i, k], v, e[k])

    print("\n".join(text_diffs))
    counts = {f: sum(c["file"] == f for c in cells) for f in EXPECTED}
    if "--dry" in sys.argv:
        for c in cells:
            print(json.dumps(c))
        return 0
    assert counts == EXPECTED, f"cells per file {counts}, expected {EXPECTED} (FORMAT.md section 3)"
    assert len(cells) == sum(EXPECTED.values())

    spell_names = json.loads(APPLE_IMPORT.read_text())["spell_names"]

    doc = {
        "_schema": "openphantasie.p2-c64-import/v1",
        "_source": "Remaster data for a Phantasie II import from the C64 copy alone (profile p2-c64, v1.2), applied by the "
                   "importer (OpenPhantasie.Importer.Pipelines.P2C64.P2C64Tables). Built by "
                   "tools/scripts/build_c64_p2_import.py from the C64 v1.2 disks and the Atari ST's data. Numbers and the "
                   "manual's spell names only; no text from the game disks.",
        "_note": "As the Apple import (DIFFERENCES-II D2-046 .. D2-050, user s159; the C64-only numbers the same way, "
                 "s217): a C64-only import keeps the C64's text and takes the ST's value in every cell where the C64's "
                 "number differs (reverse-engineering/c64/phantasie2/FORMAT.md sections 3 and 7). DNG7's exits are the "
                 "ST's (user s217).",
        "_st_cells": "As apple2-import.json's st_cells (CellPatches): a path into the GameData file the C64 import builds "
                     "(the ST's shape); byte = an offset into a hex string; fog = a map tile whose fog (tile + 120) is "
                     "kept. A cell holding the C64 value takes the ST value; one holding neither is kept and warned. why = "
                     "the decision (D2-047 .. D2-049 shared with the Apple; C64 = the C64-only numbers, FORMAT.md 7).",
        "st_cells": cells,
        "_sprite_sizes": "The ST's monster sprite (width, height) per monster (PHANT.PRG 0x2112e): II's group fit reads "
                         "the height (encounters.md 3). The C64 has no such table (its groups are up to 8 VIC sprites).",
        "sprite_sizes": sprite_sizes,
        "_spell_names": "D2-050: the C64 has no spell names (spells are cast by number, FORMAT.md 3.10); the manual's list "
                        "as apple2-import.json's (slots 56 / 57, the divine spells: the ST's names, user s170).",
        "spell_names": spell_names,
    }
    text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
    print(f"{len(cells)} cells ({', '.join(f'{f}: {n}' for f, n in counts.items())})")
    if "--check" in sys.argv:
        same = OUT.exists() and OUT.read_text() == text
        print("c64-import.json " + ("up to date" if same else "DIFFERS"))
        return 0 if same else 1
    OUT.write_text(text)
    print(f"wrote {OUT.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
