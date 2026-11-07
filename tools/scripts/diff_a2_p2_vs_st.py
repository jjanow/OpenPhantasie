#!/usr/bin/env python3
"""Compare the Apple II Phantasie II extract with the committed (Atari ST built) GameData, field by field.

Reads reverse-engineering/apple2/phantasie2/extracted/*.json (from extract_a2_p2_tables.py; run that first, or pass
--build to extract in memory) and src/OpenPhantasie.Data/GameData/phantasie2/*.json, plus the ST's TWNS.INT for the raw
creation cells (GameData's creation-tables.json holds the manual-corrected cells, not the ST's own).

Each compared value is reported as equal / differs / a2-only / st-only. Text is compared after normalising
(upper case, punctuation and runs of blanks dropped) and reported separately from numbers, because the two versions
lay text out differently (Apple: CR-ended strings and 40x24 screen dumps; ST: centred 40-char records, mixed case).

Usage: python3 -I tools/scripts/diff_a2_p2_vs_st.py [--build] [--json OUT.json] [--quiet]
Exit code 0 always (it is a report, not a gate); `--expect FILE` makes it fail when the report differs from FILE.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import struct
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

ROOT = Path(__file__).resolve().parents[2]
A2 = ROOT / "reverse-engineering" / "apple2" / "phantasie2" / "extracted"
GD = ROOT / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie2"
TWNS_INT = ROOT / "reverse-engineering" / "atari-st" / "phantasie2" / "unpacked" / "disk1" / "TWNS.INT"
CLASSES = ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"]


def norm(s: str) -> str:
    return re.sub(r"[^A-Z0-9]+", " ", s.upper()).strip()


def word_diff(rep, table: str, key: str, a2_text: str, st_text: str):
    """Add one row per changed run of words (difflib on normalised words); one 'equal' row if none."""
    import difflib
    wa, ws = norm(a2_text).split(), norm(st_text).split()
    sm = difflib.SequenceMatcher(a=wa, b=ws, autojunk=False)
    changed = False
    for op, i1, i2, j1, j2 in sm.get_opcodes():
        if op == "equal":
            continue
        changed = True
        ctx = " ".join(wa[max(0, i1 - 3):i1])
        rep.rows.append({"table": table, "key": f"{key} after '{ctx}'", "kind": "text",
                         "status": {"replace": "differs", "delete": "a2-only", "insert": "st-only"}[op],
                         "a2": " ".join(wa[i1:i2]) or None, "st": " ".join(ws[j1:j2]) or None})
    if not changed:
        rep.rows.append({"table": table, "key": key, "kind": "text", "status": "equal", "a2": None, "st": None})


class Report:
    def __init__(self):
        self.rows: list[dict] = []

    def add(self, table: str, key: str, a2, st, kind: str = "number"):
        if a2 is None and st is None:
            return
        if a2 is None:
            status = "st-only"
        elif st is None:
            status = "a2-only"
        else:
            status = "equal" if (norm(a2) == norm(st) if kind == "text" else a2 == st) else "differs"
        self.rows.append({"table": table, "key": key, "kind": kind, "status": status, "a2": a2, "st": st})

    def summary(self) -> dict:
        out: dict = {}
        for r in self.rows:
            t = out.setdefault(r["table"], {})
            k = f'{r["kind"]}:{r["status"]}'
            t[k] = t.get(k, 0) + 1
        return out


def load(p: Path) -> dict:
    return json.loads(p.read_text(encoding="utf-8"))


def diff_monsters(rep: Report, a2: dict, st: dict):
    nums = ["armor", "damage", "level", "xp", "special_chance", "specials", "hp_base", "combat_rating", "companions",
            "reaction_modifier", "next_group_chance", "defense", "gold", "group_size_factor", "treasure_items",
            "attack_power"]
    for ma, ms in zip(a2["monsters"], st["monsters"]):
        rep.add("monsters", f'{ma["index"]}.name', ma["name"], ms["name"], "text")
        for f in nums:
            rep.add("monsters", f'{ma["index"]}.{f}', ma[f], ms[f])


def diff_items(rep: Report, a2: dict, st: dict):
    by = {i["index"]: i for i in st["items"]}
    rep.add("items", "0.name", None, by[0]["name"], "text")
    for it in a2["items"]:
        s = by[it["index"]]
        rep.add("items", f'{it["index"]}.name', it["name"], s["name"].strip(), "text")
        rep.add("items", f'{it["index"]}.value_factor', it["value_factor"], s["value_factor"])


def diff_names(rep: Report, a2: dict, st: dict):
    for key in ("classes", "races", "status"):
        sa = {x["index"]: x["name"] for x in a2[key]}
        ss = {x["index"]: x["name"] for x in st[key]}
        for i in sorted(set(sa) | set(ss)):
            rep.add("names", f"{key}.{i}", sa.get(i), ss.get(i), "text")
    for s in st["spells"]:
        rep.add("names", f'spells.{s["index"]}', None, s["name"], "text")


def diff_creation(rep: Report, a2: dict):
    """Raw cells against the ST's own TWNS.INT (not the manual-corrected GameData file)."""
    w = TWNS_INT.read_bytes()
    st_tab = {
        "hp_factors": list(w[0:90]), "spell_levels": list(w[0x5A:0x5A + 360]), "mp_ceiling": list(w[0x1C2:0x1C2 + 90]),
        "skill_coefficients": [struct.unpack_from(">H", w, 0x21C + 2 * i)[0] for i in range(900)],
        "class_attribute_modifiers": list(w[0x924:0x924 + 30]), "race_attribute_modifiers": list(w[0x942:0x942 + 75]),
    }
    flat = {
        "hp_factors": [v for c in CLASSES for v in a2["hp_factors"]["by_class"][c]],
        "mp_ceiling": [v for c in CLASSES for v in a2["mp_ceiling"]["by_class"][c]],
        "spell_levels": [v for c in CLASSES for v in a2["spell_levels"]["by_class"][c]],
        "skill_coefficients": [v for c in CLASSES for lv in a2["skill_coefficients"]["by_class_level"][c] for v in lv],
        "class_attribute_modifiers": [v for c in CLASSES for v in a2["class_attribute_modifiers"]["by_class"][c].values()],
        "race_attribute_modifiers": [v for r in a2["race_attribute_modifiers"]["by_race"].values()
                                     for v in r["modifiers"].values()],
    }
    for t, cells in flat.items():
        for i, (x, y) in enumerate(zip(cells, st_tab[t])):
            rep.add("creation", f"{t}[{i}]", x, y)


def diff_towns(rep: Report, a2: dict, st: dict):
    for ta, ts in zip(a2["towns"], st["towns"]):
        n = ta["index"]
        rep.add("towns", f"{n}.name", ta["name"], ts["name"], "text")
        for f in ("price_index", "exit_region", "exit_x", "exit_y", "restock_items"):
            rep.add("towns", f"{n}.{f}", ta[f], ts[f])
        for i, (x, y) in enumerate(zip(ta["stock"], ts["stock"])):
            if i:
                rep.add("towns", f"{n}.stock[{i}]", x, y)
    for ts in st["towns"][len(a2["towns"]):]:
        rep.add("towns", f'{ts["index"]}.name', None, ts["name"], "text")


def diff_overworld(rep: Report, a2: dict, st: dict):
    unfog = lambda t: t - 120 if 120 <= t < 255 else t  # noqa: E731
    for name, ra in a2["regions"].items():
        rs = st["regions"][name]
        for y in range(20):
            for x in range(26):
                ta, ts = ra["tiles"][y][x], rs["tiles"][y][x]
                rep.add("overworld.tiles", f"{name}({x},{y})", unfog(ta), unfog(ts))
                rep.add("overworld.fog", f"{name}({x},{y})", ta >= 120, ts >= 120, "flag")
        # Features are compared as sets keyed by cell: the two versions order some records differently (OUT28).
        key = lambda f: (f["x"], f["y"])  # noqa: E731
        used = lambda f: any((f["arg"], f["dest_x"], f["dest_y"], f["y"], 25 - f["x"]))  # noqa: E731
        fa = {key(f): f for f in ra["features"] if used(f)}
        fs = {key(f): f for f in rs["features"] if used(f)}
        pages = {p["index"]: p["text"] for p in rs.get("text_pages", [])}
        for cell in sorted(set(fa) | set(fs)):
            a, s_ = fa.get(cell), fs.get(cell)
            if a and s_ and "a2_text" in a:
                # text cell: the arg encodes the text differently (Apple: start slot; ST: pages * 10 + first page)
                n, first = (1, s_["arg"]) if s_["arg"] < 10 else ((2, s_["arg"] - 10) if s_["arg"] < 20
                                                                   else (3, s_["arg"] - 20))
                st_text = " ".join(pages.get(first + k, "") for k in range(n))
                word_diff(rep, "overworld.text", f"{name}{cell}", a["a2_text"], st_text)
                continue
            for f in ("arg", "dest_x", "dest_y"):
                rep.add("overworld.features", f"{name}{cell}.{f}", a and a[f], s_ and s_[f])


def diff_dungeons(rep: Report, a2: dict, st: dict, st_msgs: dict):
    for name, la in a2["levels"].items():
        ls = st["levels"][name]
        ra, rs = bytes.fromhex(la["raw_hex"]), bytes.fromhex(ls["raw_hex"])
        for i in range(len(ra)):
            if i < len(rs):
                rep.add("dungeons.level", f"{name}[{i}]", ra[i], rs[i])
        # Only the ids the level references (GameData lists those); ST continuation records join their parent.
        # The Apple shows menus inline ("1-LEAVE  2-OPEN IT"), the ST as option labels: single digits are dropped.
        st_by = ls["messages"]
        root = {}
        for mid, m in st_by.items():
            r = mid
            while st_by.get(r, {}).get("continuation_of") is not None:
                r = str(st_by[r]["continuation_of"])
            root.setdefault(r, []).append(mid)
        strip_digits = lambda t: re.sub(r"\b[1-9]\b", " ", t)  # noqa: E731
        for r, members in sorted(root.items(), key=lambda kv: int(kv[0])):
            st_text = " ".join(" ".join(st_by[m]["lines"] + st_by[m].get("options", []))
                               for m in sorted(members, key=int) if m in st_by)
            ma = la["messages"].get(r)
            if ma is None:
                rep.add("dungeons.messages", f"{name}#{r}", None, st_text, "text")
                continue
            word_diff(rep, "dungeons.messages", f"{name}#{r}", strip_digits(" ".join(ma["lines"])),
                      strip_digits(st_text))


def diff_scrolls(rep: Report, a2: dict, st: dict):
    sa = {s["item_id"]: s for s in a2["scrolls"]}
    ss = {int(s["item_id"]): s for s in st["scrolls"]}
    for item in sorted(set(sa) | set(ss)):
        a = sa.get(item)
        s = ss.get(item)
        ta = " ".join(ln.strip("!| ") for ln in a["lines"] if not set(ln) <= set("+-! ")) if a else None
        if ta:
            ta = re.sub(r"\(PRESS [A-Z ]+\)", "", ta)
        if ta and s:
            word_diff(rep, "scrolls", str(item), ta, s["text"])
        else:
            rep.add("scrolls", str(item), ta, s["text"] if s else None, "text")


def run(build: bool) -> Report:
    if build:
        from extract_a2_p2_tables import SIDE_A, SIDE_B, build as do_build
        data = do_build(SIDE_A, SIDE_B)
    else:
        data = {p.name: load(p) for p in A2.glob("*.json")}
    rep = Report()
    diff_monsters(rep, data["monsters.json"], load(GD / "monsters.json"))
    diff_items(rep, data["items.json"], load(GD / "items.json"))
    diff_names(rep, data["names.json"], load(GD / "names.json"))
    diff_creation(rep, data["creation-tables.json"])
    diff_towns(rep, data["towns.json"], load(GD / "towns.json"))
    diff_overworld(rep, data["overworld.json"], load(GD / "overworld.json"))
    diff_dungeons(rep, data["dungeons.json"], load(GD / "dungeons.json"), load(GD / "messages.json"))
    diff_scrolls(rep, data["scrolls.json"], load(GD / "scrolls.json"))
    return rep


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--build", action="store_true", help="extract from the disk images in memory")
    ap.add_argument("--json", type=Path, help="write every non-equal row + the summary here")
    ap.add_argument("--quiet", action="store_true")
    ap.add_argument("--expect", type=Path, help="fail if the summary differs from this JSON file's summary")
    args = ap.parse_args()
    rep = run(args.build)
    summ = rep.summary()
    print(json.dumps(summ, indent=1))
    bad = [r for r in rep.rows if r["status"] != "equal"]
    if not args.quiet:
        for r in bad:
            if r["table"] == "overworld.fog" and r["status"] == "differs":
                continue   # counted in the summary; listed in --json
            print(f'{r["table"]:20s} {r["key"]:28s} {r["status"]:8s} a2={r["a2"]!r} st={r["st"]!r}'[:300])
    if args.json:
        args.json.write_text(json.dumps({"summary": summ, "rows": bad}, indent=1, ensure_ascii=False) + "\n",
                             encoding="utf-8")
    if args.expect:
        want = load(args.expect)["summary"]
        if want != summ:
            print("summary differs from", args.expect)
            sys.exit(1)


if __name__ == "__main__":
    main()
