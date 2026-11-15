#!/usr/bin/env python3
"""Build `GameData/phantasie1/c64-import.json`: DOS I's numbers for a Phantasie I import from the C64 copy alone.

The C64 import (`p1-c64`, `OpenPhantasie.Importer.Pipelines.P1C64.P1C64Tables`) keeps the C64's text, maps and names
and takes DOS I's value wherever a C64 number differs (DOS wins on numbers; `reverse-engineering/c64/phantasie1/
FORMAT.md` sections 3 and 7, DIFFERENCES.md D-084 .. D-086). This script compares the C64 v1.4 data, put into DOS's
layouts the way the importer does it, with the DOS-built GameData of the private repo, and writes what the importer
needs and the public repo can't hold otherwise:

- `dos_cells` (CellPatches, as `amiga-import.json`): monsters, overworld grids and features, dungeon bytes, elementals;
- `sprite_sizes`: DOS's 80 monster sprite (width, height) pairs (DS:0x2c3e), which the C64 has no table for;
- `text_patches`: D-084, the bleeb puzzle's lines of PMES 7 as DOS's MESS7 has them;
- `spell_names`: the manual's spell table (the C64 has no spell names; as II's Apple import, D2-050).

Reads the v1.4 G64 pair (`original-images/phantasie1/c64pp/`) and the private repo's DOS files. Run from the repo root
with `python3 -I tools/scripts/build_c64_p1_import.py` (`--check` compares with the committed file).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import extract_c64_d64 as d64  # noqa: E402
import decrypt_messages  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
GD = REPO / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie1"
OUT = GD / "c64-import.json"
IMAGES = REPO / "reverse-engineering" / "c64" / "original-images" / "phantasie1" / "c64pp"
SIDE1 = IMAGES / "phantasie_s1[ssi_1985](v1_4).g64"
SIDE2 = IMAGES / "phantasie_s2[ssi_1985](v1_4).g64"
DOS = REPO / "reverse-engineering" / "dos" / "original-files"

REGIONS = list(range(1, 17)) + [18, 19]
TOWN_FILES = [f"TWN {t}" for t in range(1, 12)] + ["TWN 99"]

# Side 2's PATTERN (the new-game template, FORMAT.md 2): the importer reads the towns, regions 2-19 and the elementals
# from it (never written by play). Offsets after its load address ($2000).
PATTERN_TOWNS = 4260  # TWN 99, then TWN t at + 252 t


def region_slot(n: int) -> int:
    return {2: 0x3C74, 3: 0x3C74 + 1012, 4: 0x3C74 + 2024, 5: 0x4850, 18: 0x6CE1, 19: 0x70D5}.get(n) or 0x4C44 + 759 * (n - 6)


def pattern_region(pattern: bytes, n: int) -> bytes:
    at = region_slot(n) - 0x2000
    return pattern[at:at + (759 if 6 <= n <= 16 else 1012)]


def pattern_town(pattern: bytes, t: int) -> bytes:
    at = PATTERN_TOWNS + 252 * (0 if t == 11 else t + 1)
    return pattern[at:at + 250]

# The manual's spell table (Phantasie_I_1986_SSI_manual_text.pdf page 18, checked on the rendered page), as it prints the names:
# spell n = slot n - 1. Slot 57 (remaster index 54, the divine spell Zeus grants) is not in the manual: the remaster's
# wording, as II's Apple import (D2-050).
MANUAL_SPELLS = [
    "HEALING 1", "HEALING 2", "HEALING 3", "HEALING 4", "FIREFLASH 1", "FIREFLASH 2", "FIREFLASH 3", "FIREFLASH 4",
    "QUICKNESS 1", "QUICKNESS 2", "QUICKNESS 3", "QUICKNESS 4", "STRENGTH 1", "STRENGTH 2", "STRENGTH 3", "STRENGTH 4",
    "PROTECTION 1", "PROTECTION 2", "PROTECTION 3", "PROTECTION 4", "CONFUSION 1", "CONFUSION 2", "CONFUSION 3",
    "CONFUSION 4", "WEAKNESS 1", "WEAKNESS 2", "WEAKNESS 3", "WEAKNESS 4", "BINDING 1", "BINDING 2", "BINDING 3",
    "BINDING 4", "MINDBLAST 1", "MINDBLAST 2", "MINDBLAST 3", "MINDBLAST 4", "FLAMEBOLT 1", "FLAMEBOLT 2", "FLAMEBOLT 3",
    "FLAMEBOLT 4", "CHARM", "SLEEP", "TELEPORTATION", "RESURRECTION", "NINJA 2", "FEAR", "DISSOLVE", "SUMMONS ELEMENTAL",
    "DISPELL UNDEAD", "NINJA 1", "AWAKEN", "MONSTER EVALUATION", "VISION", "TRANSPORTATION",
]
DIVINE = (57, "DIVINE I")

# D-084 (user s215): the bleeb puzzle's lines (the two pool hints' speakers, question 3) as DOS's MESS7. Offsets into
# PMES 7 after its load address = DOS's decrypted MESS7.
BLEEB_SPANS = [(1350, 4), (1432, 4), (2812, 41), (2853, 41)]


def files(image: Path) -> dict[str, bytes]:
    img = d64.load_image(image)
    return {e["name"]: d64.read_chain(img, *e["start"]) for e in d64.directory(img)}


def body(prg: bytes) -> bytes:
    return prg[2:]


def load(prg: bytes) -> int:
    return prg[0] | prg[1] << 8


# ---- the C64 tables in DOS layout (as P1C64Tables) ---------------------------------------------------------------

def monster_records(mn: bytes) -> list[bytes]:
    out = []
    for i in range(80):
        f = mn[i * 100:(i + 1) * 100].split(b"\r")
        name, v = f[0], [int(x) for x in f[1:24]]
        r = bytearray(0x39)
        r[:len(name)] = name

        def w16(off, x):
            r[off:off + 2] = (x & 0xFFFF).to_bytes(2, "little")

        def w32(off, x):
            r[off:off + 4] = (x & 0xFFFFFFFF).to_bytes(4, "little")
        w16(0x14, v[0]); w16(0x16, v[1]); w16(0x18, v[2]); w32(0x1A, v[3]); w16(0x1E, v[4])
        r[0x20], r[0x21], r[0x22] = v[5], v[6], v[7]
        w16(0x23, v[8]); w16(0x25, v[9]); r[0x27], r[0x28] = v[10], v[11]
        # fields 13, 14 are the C64's own (sprite blocks, [?])
        w16(0x29, v[14]); w16(0x2B, v[15]); w16(0x2D, v[16]); w32(0x2F, v[17])
        r[0x33], r[0x34], r[0x35], r[0x36] = v[18], v[19], v[20], v[21]
        w16(0x37, v[22])
        out.append(bytes(r))
    return out


def screen_page(b: bytes) -> bytes:
    if all(c == 0x0D for c in b):
        return bytes(len(b))  # CR padding, no page
    if any(c >= 0x40 for c in b):
        return bytes(b)  # DOS's leftover ASCII (OUT19 page 6), as it is
    return bytes(c + 0x40 if 1 <= c <= 26 else 0x40 if c == 0 else c for c in b)


def region_dos(c64: bytes) -> bytes:
    d = bytearray(1024)
    for x in range(26):
        for y in range(20):
            d[y * 26 + x] = c64[(25 - x) * 20 + y]
    for i in range(15):
        f = c64[520 + 5 * i:525 + 5 * i]
        d[520 + 5 * i] = (25 - f[1]) & 0xFF
        d[521 + 5 * i] = f[0]
        d[522 + 5 * i:525 + 5 * i] = f[2:5]
    pages = (len(c64) - 595) // 40
    for p in range(pages):
        d[595 + 40 * p:635 + 40 * p] = screen_page(c64[595 + 40 * p:635 + 40 * p])
    return bytes(d)


def elemental_fields(rec: bytes) -> dict:
    return {
        "intelligence": rec[2], "mp_ceiling": rec[9], "magic_max": rec[10], "magic_cur": rec[11], "hp_max": rec[12],
        "hp_cur": rec[13], "status": rec[18], "attack": rec[20], "parry": rec[22], "level": rec[54],
        "guild_number": rec[59], "weapon_damage_b": rec[162], "shield_soak": rec[163], "armor_soak": rec[164],
        "race": rec[161], "birth_race": rec[161], "class": rec[151],
    }


def main() -> int:
    s1, s2 = files(SIDE1), files(SIDE2)
    dos = {n: json.loads((GD / n).read_text()) for n in
           ("monsters.json", "overworld.json", "dungeons.json", "elementals.json", "towns.json")}
    cells: list[dict] = []

    # monsters: every field and raw byte; DOS's sprite sizes
    for i, r in enumerate(monster_records(s1["MN"])):
        m = dos["monsters.json"]["monsters"][i]
        raw = bytes.fromhex(m["raw_hex"])
        for at in range(0x39):
            if raw[at] != r[at]:
                cells.append({"why": f"{m['name']}: DOS record byte 0x{at:02x}", "file": "monsters.json",
                              "path": ["monsters", {"index": i}, "raw_hex"], "byte": at, "c64": r[at], "dos": raw[at]})
        if r[0x33] != raw[0x33]:
            cells.append({"why": f"{m['name']}: group-size factor (FORMAT.md 3.1)", "file": "monsters.json",
                          "path": ["monsters", {"index": i}, "group_size_factor"], "c64": r[0x33], "dos": raw[0x33]})
    sprite_sizes = [[m["sprite_width"], m["sprite_height"]] for m in dos["monsters.json"]["monsters"]]

    # overworld: grids (fog kept) and feature records
    pattern = body(s2["PATTERN"])
    for n in REGIONS:
        # OUT1 is not in PATTERN: side 1's, play-written but the same on every copy (FORMAT.md 2.1).
        d = region_dos(body(s1["OUT1"]) if n == 1 else pattern_region(pattern, n))
        reg = dos["overworld.json"]["regions"][f"OUT{n}.DAT"]
        played = n == 1
        for y in range(20):
            for x in range(26):
                c, v = d[y * 26 + x], reg["tiles"][y][x]
                cu = c - 120 if 120 <= c < 255 else c
                vu = v - 120 if 120 <= v < 255 else v
                if cu != vu:
                    cells.append({"why": f"OUT{n} ({x}, {y})" + (" (a region file play rewrote)" if played else ""),
                                  "file": "overworld.json", "path": ["regions", f"OUT{n}.DAT", "tiles", y, x],
                                  "fog": True, "c64": cu, "dos": vu})
                elif c != v:
                    print(f"OUT{n} ({x},{y}): fog differs only ({c} vs {v})" + (" played" if played else ""))
        for i, f in enumerate(reg["features"]):
            for k, at in (("x", 0), ("y", 1), ("arg", 2), ("dest_x", 3), ("dest_y", 4)):
                c = d[520 + 5 * i + at]
                if c != f[k]:
                    cells.append({"why": f"OUT{n} feature record {i}", "file": "overworld.json",
                                  "path": ["regions", f"OUT{n}.DAT", "features", i, k], "c64": c, "dos": f[k]})

    # dungeons: every differing byte of the C64 file (D-086 and the side tables of FORMAT.md 3.7)
    for n in range(1, 11):
        c = body(s2[f"DNG {n}"])
        raw = bytes.fromhex(dos["dungeons.json"]["levels"][f"DNG{n}"]["raw_hex"])
        for at in range(min(len(c), len(raw))):
            if c[at] != raw[at]:
                cells.append({"why": f"DNG{n} byte 0x{at:03x}" + (f" = cell ({at % 33}, {at // 33})" if at < 1254 else
                                                                   " (side tables)"),
                              "file": "dungeons.json", "path": ["levels", f"DNG{n}", "raw_hex"], "byte": at,
                              "c64": c[at], "dos": raw[at]})

    # elementals: PATTERN's CHAR2 records 21-23 (equal to every copy's CHAR2)
    char2 = pattern
    for i in range(3):
        rec = char2[(21 + i) * 170:(22 + i) * 170]
        e = dos["elementals.json"]["elementals"][i]
        for k, v in elemental_fields(rec).items():
            if v != e[k]:
                cells.append({"why": f"elemental {i}: {k} (FORMAT.md 3.5)", "file": "elementals.json",
                              "path": ["elementals", i, k], "c64": v, "dos": e[k]})
        known = [k for k in range(60) if rec[61 + k] == 1]
        if known != e["known_spells"]:
            cells.append({"why": f"elemental {i}: known spells", "file": "elementals.json",
                          "path": ["elementals", i, "known_spells"], "c64": known, "dos": e["known_spells"]})

    # towns: PATTERN's records equal DOS's (stock included), so no cells
    for t, (name, town) in enumerate(zip(TOWN_FILES, dos["towns.json"]["towns"])):
        c = pattern_town(pattern, t)
        rec = bytes.fromhex(town["dos_record"])
        restock = [int.from_bytes(rec[181 + 2 * k:183 + 2 * k], "little") for k in range(45)]
        assert list(c[180:225]) == restock, name
        assert c[226] == int.from_bytes(rec[0x10F:0x111], "little"), name
        assert c[227] == int.from_bytes(rec[0x111:0x113], "little"), name
        assert (c[228], 25 - c[229]) == (rec[0x115], rec[0x113]), name
        assert list(c[:180]) == town["stock"][1:], f"{name}: PATTERN's stock is not DOS's"

    # D-084: PMES 7's bleeb lines
    p7 = body(s2["PMES 7"])
    m7 = decrypt_messages.decrypt_mess((DOS / "MESS7").read_bytes(), 7)
    # Only the differing runs (bytes 2 apart or less joined), so the file holds the changed words, not DOS's lines.
    runs: list[list[int]] = []
    for lo, n in BLEEB_SPANS:
        for at in range(lo, lo + n):
            if p7[at] != m7[at]:
                if runs and at - runs[-1][1] <= 3:
                    runs[-1][1] = at + 1
                else:
                    runs.append([at, at + 1])
    text_patches = [{"why": "D-084 (user s215): the bleeb puzzle's lines as DOS's MESS7", "file": "PMES 7", "offset": a,
                     "c64": p7[a:b].decode("latin-1"), "dos": m7[a:b].decode("latin-1")} for a, b in runs]
    assert all(p7[a] == m7[a] for lo, n in BLEEB_SPANS for a in range(lo, lo + n)
               if not any(r[0] <= a < r[1] for r in runs))
    assert [a for a in range(len(p7)) if p7[a] != m7[a] and 1336 <= a < 2894] == \
        [a for r in runs for a in range(r[0], r[1]) if p7[a] != m7[a]]
    assert all(t["c64"] != t["dos"] for t in text_patches)

    spell_names = [{"slot": i, "name": s} for i, s in enumerate(MANUAL_SPELLS)] + [{"slot": DIVINE[0], "name": DIVINE[1]}]

    # The fire elemental's 12-byte name field, backspaces applied, reads FIRE ELE (its run-on to FIRE ELEMENTAL is
    # past the field): the M cut off, as the Amiga's FIRE  ELE that D-083 (user s202) fixed to DOS's FIRE ELEM.
    fire = char2[21 * 170 + 131:21 * 170 + 143]
    printed = ""
    for c in fire:
        printed = printed[:-1] if c == 8 else printed + chr(c)
    printed = printed.strip()
    assert printed == "FIRE ELE", printed
    name_fixes = [
        {"why": "the fire elemental's name, M cut off (CHAR2 record 21; as D-083)", "file": "elementals.json",
         "path": ["elementals", 0, "name"], "c64": printed, "fixed": "FIRE ELEM"},
        {"why": "the same name in the town labels", "file": "names.json", "path": ["town_labels", 12, "name"],
         "c64": printed, "fixed": "FIRE ELEM"},
    ]

    doc = {
        "_schema": "openphantasie.p1-c64-import/v1",
        "_source": "Remaster data for a Phantasie I import from the C64 copy alone (profile p1-c64, v1.4), applied by the "
                   "importer (OpenPhantasie.Importer.Pipelines.P1C64.P1C64Tables). Built by "
                   "tools/scripts/build_c64_p1_import.py from the C64 v1.4 disks and DOS I's data.",
        "_note": "Between versions DOS wins on numbers (AGENTS.md): a C64-only import takes the C64's text, maps and names, "
                 "and DOS I's value in every cell where the C64's number differs (reverse-engineering/c64/phantasie1/"
                 "FORMAT.md sections 3, 4 and 7; DIFFERENCES.md D-084 .. D-086).",
        "_dos_cells": "As amiga-import.json's dos_cells (CellPatches): a path into the GameData file the C64 import builds "
                      "(DOS shape); byte = an offset into a hex string; fog = a map tile whose fog (tile + 120) is kept. A "
                      "cell holding the C64 value takes the DOS value; one holding neither is kept and warned.",
        "dos_cells": cells,
        "_name_fixes": "As dos_cells, but the C64's name and the fixed one (the same cut-off name D-083 fixed on the Amiga).",
        "name_fixes": name_fixes,
        "_sprite_sizes": "DOS's monster sprite (width, height) per monster (PHANTASI.COM DS:0x2c3e): the encounter build's "
                         "group fit reads the width. The C64 has no such table (its sprites are 24 x 21 VIC sprites).",
        "sprite_sizes": sprite_sizes,
        "_text_patches": "Applied to the C64 file (after its load address) before it is read; only where it holds the c64 "
                         "text. Latin-1.",
        "text_patches": text_patches,
        "_spell_names": "The C64 has no spell names (spells are cast by number, FORMAT.md 3.10). Slots 0-53: the manual's "
                        "spell table as it prints the names; slot 57 (the divine spell): not in the manual, the remaster's "
                        "wording [~] (as II's Apple import, DIFFERENCES-II D2-050).",
        "spell_names": spell_names,
    }
    text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
    print(f"{len(cells)} cells ({', '.join(f'{f}: {sum(c['file'] == f for c in cells)}' for f in sorted({c['file'] for c in cells}))})")
    if "--check" in sys.argv:
        same = OUT.exists() and OUT.read_text() == text
        print("c64-import.json " + ("up to date" if same else "DIFFERS"))
        return 0 if same else 1
    OUT.write_text(text)
    print(f"wrote {OUT.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
