#!/usr/bin/env python3
"""Phantasie III (Apple II, SSI 1987): the game tables in the shapes DOS III's extract gives (s212).

Reads the two sides of disk 1 (`.woz` originals or `.dsk`; side A = boot: towns, wilderness, monsters; side B =
utilities: dungeons; the [cr] Player / Dungeon disks are copies of them, `DISK MAKER`) and writes
`reverse-engineering/apple2/phantasie3/extracted/` with the keys of `reverse-engineering/dos/phantasie3/extracted/`
(monsters, items, spells, names, world, dungeons, scrolls, towns, rule-tables), so `diff_a2_p3_vs_dos.py` can compare
them with DOS III's and the importer's `P3Apple2Tables` (the C# port) can feed `P3Build`. A value the Apple does not
hold is `null` (never DOS's); text is the Apple's own (the hi-res window font decoded, `window_text`). Layouts, line
numbers and every Apple != DOS cell: `reverse-engineering/apple2/phantasie3/FORMAT.md`.

Usage: python3 -I tools/scripts/extract_a2_p3_tables.py [SIDE_A SIDE_B] [--out DIR]
       (default: the v3.0 `.woz` pair in reverse-engineering/apple2/original-images/phantasie3/woz-original-v3.0/)
"""
from __future__ import annotations

import argparse
import json
import os
import struct
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # `python3 -I` drops the script dir
from a2_applesoft import detok  # noqa: E402
from a2_rdos import RdosDisk  # noqa: E402
from a2_woz import load_image  # noqa: E402
from extract_a2_p2_tables import text_page  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
IMAGES = ROOT / "reverse-engineering/apple2/original-images/phantasie3/woz-original-v3.0"
DEFAULT_A = IMAGES / "phantasie3-v3.0-disk1-side-a-boot.woz"
DEFAULT_B = IMAGES / "phantasie3-v3.0-disk1-side-b-utilities.woz"
OUT = ROOT / "reverse-engineering/apple2/phantasie3/extracted"

III_CLASSES = ["Fighter", "Thief", "Priest", "Ranger", "Monk", "Wizard"]

# The Apple's class numbers (TOWN3 260 DATA: Phantasie I's order) -> III's (P3Extract.Classes), by name.
APPLE_CLASS = ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"]

# III's race numbering (the remaster's schema, names.json _note: 5 = the 'Random' menu entry, 16 Elemental, 17 Undead)
# -> the Apple's race number (TOWN3 270 DATA, Phantasie I's order). The Apple has no Elemental and no 'Random' race.
III_RACE_FROM_APPLE = [0, 1, 2, 4, 3, None, 8, 12, 10, 13, 14, 7, 11, 5, 6, 9, None, 15]
# III's social classes (4 Darklord, 5 Superhero) -> the Apple's record value (TOWN3 295: 4 SUPER HERO, 5 DARK LORD).
III_SOCIAL_FROM_APPLE = [0, 1, 2, 3, 5, 4]
# III's terrain numbers (DOS's 22 names) -> the Apple's TR$ index (STARTER 120) of the same ground [~]: the planes'
# floors are TR$ 12..14 (shown for terrain 7 there, STARTER 5400), BRIDGE is DOS's 13, MIST its 16.
III_TERRAIN_FROM_APPLE = [0, 1, 2, 3, 4, 5, 6, 14, 8, 9, 10, 12, None, 7, 13, None, 11, None, None, None, None, None]

# III dungeon n (1..11, the remaster's DNG<n>) and inn n (1..8) -> the Apple wilderness section (OT<s>) holding it. The
# Apple numbers neither: a section holds at most one dungeon (its file = OT byte 151) and one inn (name at byte 171);
# towns carry their number (byte 150). The section of each is where III's place list puts that place (FORMAT.md
# 'World'). Dungeons 1 and 8 both name DNG10 (DOS's records J and K are byte-identical).
DUNGEON_SECTION = [5, 3, 15, 14, 13, 11, 23, 21, 41, 29, 31]
INN_SECTION = [8, 10, 15, 13, 7, 6, 18, 21]
# The order of III's place list (overworld.json places: DOS's place test, ovl3 0xa22, in code order), by (type, number);
# the Apple's places are listed in it.
PLACE_ORDER = [("town", 17), ("dungeon", 9), ("town", 11), ("town", 12), ("town", 13), ("dungeon", 1), ("dungeon", 2),
               ("inn", 1), ("inn", 2), ("inn", 6), ("town", 3), ("town", 8), ("town", 14), ("dungeon", 3),
               ("dungeon", 6), ("inn", 3), ("inn", 4), ("inn", 5), ("town", 1), ("town", 2), ("town", 4), ("town", 5),
               ("town", 9), ("dungeon", 4), ("dungeon", 5), ("inn", 7), ("town", 6), ("town", 7), ("town", 10),
               ("dungeon", 7), ("dungeon", 8), ("inn", 8), ("town", 15), ("dungeon", 10), ("town", 16), ("dungeon", 11)]

# The hi-res window font (IMPTEXT, glyph k = character 37 + k): these characters draw as another one.
WINDOW_FONT = {"=": " ", ":": "-", "@": ":", "<": ",", "?": ")", ">": "?", ";": '"'}


# ---- disks, programs, text -------------------------------------------------------------------------------------------

class Sides:
    def __init__(self, a, b):
        self.a = RdosDisk(load_image(a))
        self.b = RdosDisk(load_image(b))
        self.sources = {"A": str(a), "B": str(b)}

    def read(self, name: str, side: str = "A") -> bytes:
        return (self.a if side == "A" else self.b).read(name)

    def program(self, name: str, side: str = "A") -> dict[int, str]:
        disk = self.a if side == "A" else self.b
        f = disk.entry(name)
        data = b"".join(disk.sector(f.start + i) for i in range(f.sectors))
        out, p = {}, 0
        while p + 4 <= len(data):
            nxt, ln = struct.unpack_from("<HH", data, p)
            if nxt < 0x100:
                break
            q = data.find(0, p + 4)
            out[ln] = detok(data[p + 4:q])
            p = q + 1
        return out


def ascii7(raw: bytes) -> str:
    return "".join(chr(c & 0x7F) for c in raw)


def font(s: str) -> str:
    """A string as the hi-res window font draws it (IMPTEXT)."""
    return "".join(WINDOW_FONT.get(c, c) for c in s)


def window_text(s: str, width: int, decode_font: bool = True) -> str:
    """A string laid out for a `width`-column window, as one line: the hi-res font decoded (`decode_font`; the
    40-column text screen uses the normal font), each window row stripped, a row ending in '-' (the font's ':') joined
    to the next without a space (a hyphenated word) [~]."""
    rows = [(font(s[i:i + width]) if decode_font else s[i:i + width]).strip() for i in range(0, len(s), width)]
    out = ""
    for r in rows:
        if not r:
            continue
        if out and not out.endswith("-"):
            out += " "
        elif out.endswith("-"):
            out = out[:-1]
        out += r
    return out


def layout(text: str, width: int) -> str:
    """One line of text word-wrapped into `width`-column rows, each but the last padded with spaces (as DOS's texts
    are laid out, so P3Build's 38-column slices are the rows) [~]; a word longer than a row is cut."""
    rows, cur = [], ""
    for word in text.split():
        while len(word) > width:
            if cur:
                rows.append(cur)
                cur = ""
            rows.append(word[:width])
            word = word[width:]
        if not cur:
            cur = word
        elif len(cur) + 1 + len(word) <= width:
            cur += " " + word
        else:
            rows.append(cur)
            cur = word
    if cur or not rows:
        rows.append(cur)
    return "".join(r.ljust(width) for r in rows[:-1]) + rows[-1]


def cr_string(raw: bytes, at: int) -> str:
    end = raw.index(0x0D, at) if 0x0D in raw[at:] else len(raw)
    return "".join(c for c in ascii7(raw[at:end]) if " " <= c < "\x7f")


def data_items(line: str) -> list[str]:
    """The items of a BASIC DATA statement (`... DATA a,b,c`)."""
    body = line.split(" DATA ", 1)[1]
    return [x.strip() for x in body.split(",")]


def assignments(line: str, var: str) -> dict[int, str]:
    """`var(k) = "text"` assignments on a line."""
    out = {}
    for part in line.split(":"):
        part = part.strip()
        if part.startswith(f"{var}(") and "=" in part and '"' in part:
            k = int(part[len(var) + 1:part.index(")")])
            out[k] = part.split('"')[1]
    return out


# ---- monsters --------------------------------------------------------------------------------------------------------

# OUTDOORS3 / DUNGON3 line 910: the INPUT list of one monster record (22 CR-ended fields).
MON_FIELDS = ["name", "armor", "damage", "level", "xp", "special_chance", "special_0", "special_1", "special_2",
              "hp_base", "combat_rating", "companion_0", "companion_1", "reaction_modifier", "next_group_chance",
              "defense", "gold", "group_max", "treasure_0", "treasure_1", "attack_power", "shape"]


def monster_records(sides: Sides) -> list[list[str]]:
    recs = []
    for f in range(10):
        raw = sides.read(f"MN3{f}")
        if raw != sides.read(f"MN3{f}", "B"):
            raise ValueError(f"MN3{f}: side A and side B differ")
        text = ascii7(raw.split(b"\0")[0])
        fields = text.split("\r")
        for k in range(8):
            rec = fields[22 * k:22 * (k + 1)]
            if len(rec) != 22:
                raise ValueError(f"MN3{f}: record {k} short")
            recs.append(rec)
    return recs


def monsters(sides: Sides) -> dict:
    out = []
    for i, rec in enumerate(monster_records(sides)):
        v = dict(zip(MON_FIELDS, rec))
        n = {k: int(x) for k, x in v.items() if k != "name"}
        out.append({
            "index": i, "name": "".join(c for c in font(v["name"]) if " " <= c < "\x7f").strip(),
            "group_max": n["group_max"], "attack_power_div10": None, "attack_power": n["attack_power"],
            "damage": n["damage"], "armor": n["armor"], "defense": n["defense"], "level": n["level"],
            "special_chance": n["special_chance"], "specials": [n["special_0"], n["special_1"], n["special_2"]],
            "combat_rating": n["combat_rating"], "hp_base": n["hp_base"], "reaction_modifier": n["reaction_modifier"],
            "byte_27": None, "next_group_chance": n["next_group_chance"],
            "companions": [n["companion_0"], n["companion_1"]], "treasure_items": [n["treasure_0"], n["treasure_1"]],
            "byte_33": None, "gold": n["gold"], "xp": n["xp"], "shape": n["shape"],
        })
    return {
        "_source": "MN30..MN39 (side A = side B), 8 text records each, 22 CR-ended fields read by OUTDOORS3 / DUNGON3 "
                   "line 910 [C]; monster m = record m mod 8 of MN3<m div 8>. Values as the Apple holds them (full "
                   "numbers, no DOS byte encoding); attack_power is the whole value (DOS stores / 10); byte_27 and "
                   "byte_33 have no Apple field (shape = the Apple's picture number)",
        "monsters": out,
    }


# ---- items -----------------------------------------------------------------------------------------------------------

def item_kind(i: int) -> str:
    for last, kind in ((20, "shield"), (40, "armor"), (60, "bow"), (100, "weapon"), (110, "healing_potion"),
                       (120, "magic_potion"), (140, "scroll")):
        if i <= last:
            return kind
    return "treasure"


def item_points(y: int) -> tuple[int, int]:
    """TOWN3 1150-1190 (the armory's PTS column and the 'usable' test): (points, requirement) of item y (1..100)."""
    if y < 21:
        return y, y
    if y < 31:
        return y - 20, y * 2 - 40
    if y < 41:
        return y - 29, y * 2 - 64
    if y < 61:
        return y - 40, y * 2 - 82
    if y < 81:
        return int(y / 1.36 - 43), y * 3 - 180
    return int(y * 1.2 - 95), y * 3 - 240


def items(sides: Sides, tinfo: bytes, towns_raw: bytes) -> dict:
    names = ascii7(sides.read("ITEM")).split("\r")
    out = [{"id": 0, "name": None, "kind": None, "value": None, "points": None, "requirement": None}]
    for i in range(1, 181):
        f = tinfo[89 + i]
        it = {"id": i, "name": names[i - 1], "kind": item_kind(i), "value": int(f * f / 5 + 1), "value_factor": f}
        if i <= 100:
            it["points"], it["requirement"] = item_points(i)
        out.append(it)
    return {
        "_source": "names ITEM (180 CR-ended, TOWN3 230); value = INT(f^2 / 5 + 1), f = TINFO[89 + id] (the armory price "
                   "INT(f^2 / 5 * CF + 1) at price factor 100, TOWN3 1117) [C]; points / requirement: TOWN3 1150-1190 "
                   "[C]; town price index = the town record's byte 226 (TOWN3 825)",
        "town_price_index": [{"town": t, "name": None if t == 0 else town_name(towns_raw, t),
                              "index": None if t == 0 else towns_raw[250 * (t - 1) + 226]} for t in range(18)],
        "items": out,
    }


# ---- spells, names -----------------------------------------------------------------------------------------------------

def spells() -> dict:
    return {
        "_source": "the Apple has no spell names: spells are learnt and cast by number 1-60 (TOWN3 3060, 6882) [C]",
        "spells": [{"number": n, "name": None} for n in range(1, 58)],
    }


def lst(names, start=0):
    return [{"index": i, "name": x} for i, x in enumerate(names, start)]


def names(sides: Sides, towns_raw: bytes, sections: dict[int, bytes]) -> dict:
    town = sides.program("TOWN3")
    starter = sides.program("STARTER")
    classes_apple = data_items(town[260])
    races_apple = data_items(town[270])[:16]
    skills_apple = data_items(town[130])
    social = assignments(town[295], "SC$")
    origins = assignments(town[290], "H$")
    body = assignments(town[290], "S$")
    status_short = assignments(starter[450], "ST$")
    class_short = data_items(starter[430])
    if data_items(starter[120]) != APPLE_TERRAIN:
        raise ValueError("STARTER 120: terrain names changed")
    terrain_apple = [font(x) for x in APPLE_TERRAIN]
    by_class = {c: classes_apple[APPLE_CLASS.index(c.upper())] for c in III_CLASSES}
    races = [None if a is None else races_apple[a] for a in III_RACE_FROM_APPLE]
    # III's skill order (names.json): attack, parry, swimming, listen, find trap, disarm trap, find item, pick lock,
    # fire bow -> the Apple's slot (TOWN3 130, record +20 + 2k).
    skills = [skills_apple[k] for k in (0, 1, 9, 5, 3, 4, 2, 7, 6)]
    places = [window_text(cr_string(sections[s], 226), 15) for s in DUNGEON_SECTION]
    # OUTDOORS3 7200: "THE", the name (CALL 800), "INN".
    inns = ["THE " + window_text(cr_string(sections[s], 171), 8) + " INN" for s in INN_SECTION]
    return {
        "_source": "TOWN3 / STARTER program text (DATA lines and string assignments) and the OT section texts [C]; the "
                   "Apple's own wording; null where the Apple has no such name",
        "combat_actions": None, "status_short": lst([status_short[k] for k in (0, 4, 2, 1, 3)]),
        "class_short": lst([class_short[APPLE_CLASS.index(c.upper())] for c in III_CLASSES]),
        "rank_labels": None,
        "places": lst(places, 0), "inns": lst(inns, 0),
        "terrain": lst([None if a is None else terrain_apple[a] for a in III_TERRAIN_FROM_APPLE]), "skills": lst(skills),
        "races": lst(races), "classes": lst([by_class[c] for c in III_CLASSES]),
        "towns": lst([town_name(towns_raw, t) for t in range(1, 15)]),
        "origins": lst([town_name(towns_raw, t) for t in (15, 16, 17)] + [origins[k] for k in range(6)]),
        "status": lst(["DEAD", "OKAY", None, None, None]),
        "social_classes": lst([social[a] for a in III_SOCIAL_FROM_APPLE]),
        "body_states": lst([body[1], body[2], None, body[3]]),
        "transfer_class_map_I_to_III": [III_CLASSES.index(c.title()) for c in APPLE_CLASS],
        "transfer_race_map_I_to_III": [III_RACE_FROM_APPLE.index(a) for a in range(16)],
        "_maps": "BACKUP 4000-5015 copies Phantasie I / II records as they are: the I -> III maps are the identity in "
                 "the Apple's numbering, given here in III's numbering",
    }


# ---- towns -----------------------------------------------------------------------------------------------------------

def town_name(raw: bytes, t: int) -> str:
    rec = raw[250 * (t - 1):250 * t]
    return ascii7(rec[230:250]).rstrip("\x15").strip()


def towns(towns_raw: bytes) -> dict:
    out = [{"town": 0, "name": None, "price_index": None, "kinds": None, "stock": None, "stocked": []}]
    for t in range(1, 18):
        rec = towns_raw[250 * (t - 1):250 * t]
        stock = [0] + list(rec[0:180])
        out.append({
            "town": t, "name": town_name(towns_raw, t), "price_index": rec[226], "kinds": None, "stock": stock,
            "restock_pool": list(rec[180:226]), "exit": {"section": rec[227], "x": rec[228], "y": rec[229]},
            "stocked": [{"id": i, "count": stock[i]} for i in range(181) if stock[i]],
        })
    return {
        "_source": "TF3 (the new-game template; 17 x 250 B, record t = town t, file offset k = address LA + 1 + k): stock "
                   "of item id at id - 1 (TOWN3 1115, 1290, 8450), restock pool 180..225 (TOWN3 860), price factor 226 "
                   "(825), exit section / x / y 227..229 (6020), name 230..249 padded with 0x15 (810) [C]",
        "_layout": "stock[id] for item id 0..180; no 'kinds' byte on the Apple",
        "towns": out,
    }


# ---- scrolls ---------------------------------------------------------------------------------------------------------

def screen_lines(raw: bytes) -> list[str]:
    """A 1000-B dump of the 40 x 24 text page ($400, interleaved rows) as its 24 lines, right-stripped (II's
    `text_page`: inverse / flash glyphs as their letters)."""
    return text_page(raw)


# Text files in DOS's record order: SC1..SC20 without SC6 (Scroll IV: the map drawn in characters; DOS's picture).
SCROLL_FILES = [1, 2, 3, 4, 5] + list(range(7, 21))


def scroll_inner(lines: list[str]) -> list[str]:
    """The text inside the scroll frame: rows 1..22 (rows 0 and 23 are the '+---+' rules), columns 1..38 (inside the
    '!' borders), right-stripped; trailing empty rows dropped."""
    inner = [ln.ljust(40)[1:39].rstrip() for ln in lines[1:23]]
    while inner and not inner[-1]:
        inner.pop()
    return inner


def scrolls(sides: Sides) -> dict:
    recs, cont = [], {}
    for n in range(1, 21):
        raw = sides.read(f"SC{n}")
        cont[n] = raw[1872 - 1024] == 161  # TOWN3 8020: row 22 column 0 is '!' -> the next file follows
        if n == 6:
            continue
        inner = scroll_inner(screen_lines(raw))
        recs.append({"record": SCROLL_FILES.index(n), "file": f"SC{n}", "length": None, "text": "\n".join(inner),
                     "lines": inner})
    out_items = []
    for item in range(121, 141):
        files, f = [], item - 120
        while True:
            files.append(f)
            if not cont[f] or f >= 20:
                break
            f += 1
        out_items.append({"item": item, "name": None, "records": [SCROLL_FILES.index(x) for x in files if x != 6]})
    return {
        "_source": "SC1..SC20 (side A): 1000-B dumps of the text page $400, shown as is (TOWN3 8010); item i (121..140) "
                   "shows SC(i - 120) and the next file while row 22 column 0 is '!' (8020) [C]. Records in DOS's "
                   "numbering: SC6 (Scroll IV, item 126: the map drawn in characters, DOS's picture) is not a text "
                   "record. text = the 38 columns inside the frame, rows 1..22, joined by newlines",
        "scroll_iv_lines": screen_lines(sides.read("SC6")),
        "records": recs, "items": out_items,
    }


# ---- world -----------------------------------------------------------------------------------------------------------

# STARTER 120 TR$: terrain t = tile byte / 10 (OUTDOORS3 5070); the ones digit is the square's monster level
# (6 / 7: a sign / place text square, 8: a 'no encounter' marker, OUTDOORS3 5080-5095).


def plane_grid(sections: dict[int, bytes], first: int, cols: int, rows: int) -> list[list[int]]:
    grid = [[0] * (10 * cols) for _ in range(15 * rows)]
    for r in range(rows):
        for c in range(cols):
            raw = sections[first + c + 5 * r]
            for y in range(15):
                for x in range(10):
                    grid[15 * r + y][10 * c + x] = raw[10 * y + x]
    return grid


APPLE_TERRAIN = ["GRASS", "FOREST", "HILLS", "MOUNTAINS", "BUILDINGS", "DUNGEON", "PATHWAY", "BRIDGE", "LAVA", "DESERT",
                 "WATER", "MIST", "THE=LIGHT=PLANE", "THE=DARK=PLANE", "NETHERWORLD====LEVEL=THREE"]


def world(sections: dict[int, bytes], towns_raw: bytes) -> dict:
    planes = [
        {"plane": 0, "key": "scandor", "source": "OT1..OT25 (5 x 5 sections of 10 x 15)", "width": 50, "height": 75,
         "arrival": None, "tiles": plane_grid(sections, 1, 5, 5)},
        {"plane": 1, "key": "light", "source": "OT29", "width": 10, "height": 15, "arrival": None,
         "tiles": plane_grid(sections, 29, 1, 1)},
        {"plane": 2, "key": "dark", "source": "OT31", "width": 10, "height": 15, "arrival": None,
         "tiles": plane_grid(sections, 31, 1, 1)},
        {"plane": 4, "key": "netherworld", "source": "OT41..OT43", "width": 30, "height": 15, "arrival": None,
         "tiles": [sum((list(sections[s][10 * y:10 * y + 10]) for s in (41, 42, 43)), []) for y in range(15)]},
    ]
    places = []
    plane_of = {29: ("light", 0, 0), 31: ("dark", 0, 0), 41: ("netherworld", 0, 0), 42: ("netherworld", 10, 0),
                43: ("netherworld", 20, 0)}
    for sec in sorted(sections):
        raw = sections[sec]
        if sec <= 25:
            plane, ox, oy = "scandor", 10 * ((sec - 1) % 5), 15 * ((sec - 1) // 5)
        else:
            plane, ox, oy = plane_of[sec]
        for y in range(15):
            for x in range(10):
                b = raw[10 * y + x]
                if b == 40:
                    places.append({"plane": plane, "x": ox + x, "y": oy + y, "type": "town", "number": raw[150],
                                   "name": window_text(cr_string(raw, 154), 8), "tile": b, "section": sec})
                elif b == 41:
                    places.append({"plane": plane, "x": ox + x, "y": oy + y, "type": "inn",
                                   "number": INN_SECTION.index(sec) + 1, "name": window_text(cr_string(raw, 171), 8),
                                   "tile": b, "section": sec})
                elif b // 10 == 5:
                    places.append({"plane": plane, "x": ox + x, "y": oy + y, "type": "dungeon",
                                   "number": DUNGEON_SECTION.index(sec) + 1,
                                   "name": window_text(cr_string(raw, 226), 15), "tile": b, "section": sec,
                                   "dungeon_file": raw[151], "entrance": {"x": raw[152], "y": raw[153]}})
    places.sort(key=lambda p: PLACE_ORDER.index((p["type"], p["number"])))
    if len(places) != len(PLACE_ORDER):
        raise ValueError(f"{len(places)} places, expected {len(PLACE_ORDER)}")
    # A new game starts in Pendragon (TOWN3 710 TN = 1); leaving a town puts the party on the town record's exit
    # square (TOWN3 6020: section, x, y = record bytes 227..229).
    sec, sx, sy = towns_raw[227], towns_raw[228], towns_raw[229]
    terrain_names = [None if a is None else font(APPLE_TERRAIN[a]) for a in III_TERRAIN_FROM_APPLE]
    ranges = [{"first": 10 * t, "last": 10 * t + 9, "terrain": III_TERRAIN_FROM_APPLE.index(t)} for t in range(12)]
    return {
        "_source": "OT<n> sections (side A), 10 x 15 tile bytes at LA + 10y + x (OUTDOORS3 210), then the section's "
                   "town / dungeon / inn and texts [C]; tile = 10 * terrain + monster level (5070)",
        "planes": planes,
        "start": {"plane": 0, "x": 10 * ((sec - 1) % 5) + sx, "y": 15 * ((sec - 1) // 5) + sy},
        "places": places,
        "terrain": {
            "_note": "Apple tile byte b: terrain b // 10 (STARTER 120 TR$, here in III's numbering), level b % 10; 40 "
                     "town, 41 inn, 50..59 a dungeon; 100 ocean / River Styx (blocked, 5270); no tile-id ranges as DOS's",
            "names": terrain_names, "ranges": ranges,
            "other": None, "netherworld_overrides": None, "blocked_from": None, "no_encounter": None,
        },
    }


# ---- dungeons --------------------------------------------------------------------------------------------------------

DNG_PTR, DNG_TEXT_BASE, DNG_W, DNG_H = 0x593, 0x800, 24, 30


def dungeon_texts(rec: bytes) -> tuple[int, list[dict]]:
    ptrs, o, end = [], DNG_PTR, len(rec)
    while o + 1 < end:
        p = rec[o] << 8 | rec[o + 1]
        if p:
            end = min(end, p - DNG_TEXT_BASE)
        ptrs.append(p)
        o += 2
    ptrs = ptrs[:(end - DNG_PTR) // 2]
    texts = []
    for i, p in enumerate(ptrs):
        if not p:
            continue
        at = p - DNG_TEXT_BASE
        raw = ascii7(rec[at:rec.index(0x0D, at)])
        # A text with the window font's spaces ('=') is laid out for the 15-column window (DUNGON3 6000 / 6310 via
        # 800); the others print on the 40-column text screen (8210 via 7300).
        text = layout(window_text(raw, 15) if "=" in raw else window_text(raw, 40, decode_font=False), 38)
        texts.append({"index": i, "offset": hex(at), "text": text})
    return len(ptrs), texts


def dungeons(sides: Sides, sections: dict[int, bytes], names_places: list[dict]) -> dict:
    out = []
    for n, sec in enumerate(DUNGEON_SECTION, 1):
        f = sections[sec][151]
        rec = sides.read(f"DNG{f}", "B")
        cells = [list(rec[y * DNG_W:(y + 1) * DNG_W]) for y in range(DNG_H)]
        squares = []
        for k in range(35):
            b = rec[0x2E4 + 5 * k:0x2E4 + 5 * k + 5]
            squares.append({"cell": 0xDD + k, "message": b[0] & 0x7F, "once": bool(b[0] & 0x80), "choices": b[1],
                            "programs": [b[2], b[3], b[4]]})
        exits = [{"cell": 0xD7 + k, "destination": rec[0x2DB + 3 * k], "x": rec[0x2DC + 3 * k],
                  "y": rec[0x2DD + 3 * k]} for k in range(3)]
        slots, texts = dungeon_texts(rec)
        out.append({
            "dungeon": n, "record": f - 1, "letter": chr(ord("A") + f - 1), "file": f"DNG{f}",
            "name": names_places[n - 1]["name"], "level": rec[0x2D0], "width": DNG_W, "height": DNG_H, "cells": cells,
            "forced_fights": list(rec[0x2D1:0x2DB]), "exits": exits, "event_squares": squares,
            "event_program_hex": rec[0x393:0x593].hex(), "text_slots": slots, "texts": texts, "raw_hex": rec.hex(),
        })
    return {
        "_source": "DNG1..DNG10 (side B), one record each in DOS DNGS.DAT's layout (map 24 x 30, tables to 0x593, text "
                   "pointers, texts) [C]: 4132 B (the text area is shorter). DUNGON3 / DNGLD3: dungeon file = the "
                   "section's OT byte 151; dungeons 1 and 8 both use DNG10",
        "dungeons": out,
    }


# ---- rule tables -----------------------------------------------------------------------------------------------------

def rule_tables(tinfo: bytes, info: bytes, sections: dict[int, bytes]) -> dict:
    def by_class(base: int, stride: int, n: int) -> dict:
        return {c: list(tinfo[base + stride * APPLE_CLASS.index(c.upper()):][:n]) for c in III_CLASSES}

    # skill coefficients: TINFO 830 + 150 class + 10 level + slot (TOWN3 1512); the Apple's slots (TOWN3 130).
    slot = {"attack": 0, "parry": 1, "swim": 9, "listen": 5, "find_trap": 3, "disarm_trap": 4, "find_item": 2,
            "pick_lock": 7}
    skills = {s: {c: [tinfo[830 + 150 * APPLE_CLASS.index(c.upper()) + 10 * lv + k] for lv in range(15)]
                  for c in III_CLASSES} for s, k in slot.items()}
    # spell levels: TINFO 380 + 60 class + (spell - 1) = the level (1..15) at which the Guild offers it (TOWN3 1570:
    # learnable when the new level VL + 1 equals it), 0 = never.
    levels = {c: list(tinfo[380 + 60 * APPLE_CLASS.index(c.upper()):][:60]) for c in III_CLASSES}
    rows = {}
    for c in III_CLASSES:
        per = {}
        for sp, lv in enumerate(levels[c][:57], 1):
            if lv:
                per.setdefault(lv, []).append(sp)
        depth = max((len(v) for v in per.values()), default=0)
        rows[c] = [[per.get(lv, [])[r] if r < len(per.get(lv, [])) else 0 for lv in range(1, 16)] for r in range(depth)]
    letters = [chr(ord("A") + sections[s][151] - 1) for s in DUNGEON_SECTION]
    entrance = {}
    # Dungeon 1's entrance x is P3Build's constant 5 (DOS FAR:0xe59a); the Apple's section 5 says 5 as well. Every
    # other dungeon's goes by its letter, so DNG10 (dungeons 1 and 8) carries dungeon 8's x.
    for n, sec in enumerate(DUNGEON_SECTION, 1):
        raw = sections[sec]
        if n == 1:
            if raw[152] != 5:
                raise ValueError("dungeon 1's entrance x is not 5")
            continue
        entrance[chr(ord("A") + raw[151] - 1)] = raw[152]
    return {
        "_source": "TINFO (loaded at XA + 310 = $08FC, XA = 1990; offsets = address - 2300) and INFO ($302) [C]",
        "classes": III_CLASSES,
        "hp_dice": {"_address": "TINFO 0 + 15 class + level (TOWN3 1520: dice = v * (CON + 5) / 16 of rnd 1..3)",
                    "_note": "levels 1..15 (the Apple has no level 16+ row)", "by_class": by_class(0, 15, 15)},
        "training_xp_curve": {"_address": "none: TOWN3 2820 computes the XP needed (1000 * (1.8^L - 0.8) / class "
                                          "score) [C]", "values": None},
        "training_gold_curve": {"_address": "none: TOWN3 2900 computes the cost (CF * (20 - CHA)^2 * L^2.2, at most "
                                            "15000) [C]", "values": None},
        "magic_ceiling": {"_address": "TINFO 740 + 15 class + level (TOWN3 1505)", "by_class": by_class(740, 15, 15)},
        "skill_tables": {"_address": "TINFO 830 + 150 class + 10 level + slot (TOWN3 1512)",
                         "_bases": {s: f"slot {k}" for s, k in slot.items()}, "by_skill": skills},
        "spell_rows": {"_address": "TINFO 380 + 60 class + spell - 1 = level; rows rebuilt by level, spells ascending",
                       "by_class": rows},
        "spell_levels": {"_note": "TINFO 380 + 60 class + spell - 1 (spells 1..60)", "by_class": levels},
        "score_weights": {"_address": "none: the Mystic's score is level / 6 + log(1 + quest bits) * 16.23 (TOWN3 "
                                      "4630-4640) [C]", "values": None},
        "hit_location": {"_address": "none: OUTDOORS3 8100 rolls the area (rnd ^ (DT / 2) * 6) [C]",
                         "_columns": None, "_rows": None, "rows": None},
        "air_elemental_spells": {"_address": "none: the Apple has no elementals", "values": None},
        "wilderness_area_levels": {"_address": "none: each square's tile byte carries its own level (b mod 10, "
                                               "OUTDOORS3 5070)", "values": None},
        "wilderness_terrain_offsets": {
            "_address": "INFO 223 + terrain + rnd 0..3 (OUTDOORS3 6216 PEEK(993 + TR + RND * 4)); in DOS's index "
                        "(t + rnd 1..4) entry k + 1 = INFO 223 + k; entries 0 and 16..25 are not in INFO",
            "values": [None] + list(info[223:238]) + [None] * 10},
        "dungeon_letters": {"_address": "the section's OT byte 151 = DNG file (letter A + file - 1); dungeons 1 and 8 "
                                        "share DNG10", "values": letters},
        "dungeon_entrance_x": {"_address": "the section's OT byte 152 (y = byte 153)", "by_letter": entrance},
    }


# ---- main ------------------------------------------------------------------------------------------------------------

def extract(side_a, side_b) -> dict[str, dict]:
    sides = Sides(side_a, side_b)
    tinfo, info, towns_raw = sides.read("TINFO"), sides.read("INFO"), sides.read("TF3")
    sections = {n: sides.read(f"OT{n}") for n in list(range(1, 26)) + [29, 31, 41, 42, 43]}
    nm = names(sides, towns_raw, sections)
    return {
        "monsters.json": monsters(sides),
        "items.json": items(sides, tinfo, towns_raw),
        "spells.json": spells(),
        "names.json": nm,
        "towns.json": towns(towns_raw),
        "scrolls.json": scrolls(sides),
        "world.json": world(sections, towns_raw),
        "dungeons.json": dungeons(sides, sections, nm["places"]),
        "rule-tables.json": rule_tables(tinfo, info, sections),
    }


def main(argv: list[str]) -> None:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("sides", nargs="*", default=[str(DEFAULT_A), str(DEFAULT_B)])
    ap.add_argument("--out", type=Path, default=OUT)
    args = ap.parse_args(argv[1:])
    if len(args.sides) != 2:
        ap.error("give side A and side B")
    tables = extract(*args.sides)
    args.out.mkdir(parents=True, exist_ok=True)
    for name, data in tables.items():
        (args.out / name).write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"wrote {len(tables)} files to {args.out}")


if __name__ == "__main__":
    main(sys.argv)
