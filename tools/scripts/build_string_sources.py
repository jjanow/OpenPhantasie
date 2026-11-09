#!/usr/bin/env python3
"""Per-id provenance of the original text in the string tables (10-importer.md 2.7 / 7.4, s158).

The string tables (`GameData/<folder>/strings/en.json`) mix our own text with lines the games print. The public repo
must not ship the games' lines, so this script finds, for every id, which parts of its value are original text and
where each part sits in the player's own copy, and writes `GameData/<folder>/strings/en.sources.json`:

    "strings": { "<id>": [ { "from": "<version profile>", "parts": [ "<our text>", ["<file>", offset, length], ... ] },
                           ... alternatives, preferred first ... ] }

A part is either our own literal text (format placeholders such as `{0,-10}`, glue spaces, punctuation) or a reference
`[file, offset, length]` into one of the version's files (latin-1 bytes). Files: the raw key file by name, or a decoded
image (`PHANTASI.COM:unpacked` = the EXEPACK-unpacked load image, `MAIN.EXE:root` = III's unpacked root module).
Concatenating the parts of any alternative gives the private table's value exactly (checked here and by the importer's
golden test). The importer (`OpenPhantasie.Importer.Pipelines.ImportedStrings`) resolves the references from the
player's copy into `strings/en.<profile>.json`, which `StringTable.ForGame` lays over the shipped table; the public
repo (10-importer.md 8) ships each such id with the references replaced by "...".

Phantasie IV is not covered: its public `en.json` is the id-keyed patch (ids whose English differs from the Japanese)
and the importer adds the rest from the player's P4.X (4.6).

Phantasie II's Apple II copy (`p2-apple2`, user s159, DIFFERENCES-II D2-050) is added after the rest, so it changes no
other alternative: an id the ST gives gets a `p2-apple2` alternative when every original part of the ST's is found
verbatim in the Apple's programs (the same text), and the ids the Apple prints in its own wording get the
hand-mapped alternatives of `p2_apple2_own_wording.json` (references only, marked `"wording": "own"`: they resolve
to the Apple's text, not to the committed value). The ids neither covers get the remaster's own wording from
`GameData/phantasie2/strings/fallback.en.json` (written by the importer under every imported file).

Run with `python3 -I tools/scripts/build_string_sources.py` (`--check` compares with the committed files).
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
import re_common  # noqa: E402
import extract_dos_p3_common as p3  # noqa: E402
from a2_rdos import RdosDisk  # noqa: E402
from extract_a2_p2_tables import text_page  # noqa: E402

GAMEDATA = REPO / "src" / "OpenPhantasie.Data" / "GameData"
RE = REPO / "reverse-engineering"

# Version profile -> {file key: bytes}. Keys are what the importer resolves (ImportedStrings.cs).
def load_versions() -> dict[str, dict[str, bytes]]:
    dos1 = RE / "dos" / "original-files"
    p3exe = p3.MAIN_EXE.read_bytes()
    return {
        "p1-dos-1987": {
            "OV1.OVR": (dos1 / "OV1.OVR").read_bytes(),
            "OV2.OVR": (dos1 / "OV2.OVR").read_bytes(),
            "PHANTASI.COM:unpacked": re_common.unpack_phantasi_com(),
        },
        "p1-amiga-1.0": {"game": (RE / "amiga" / "unpacked" / "PHANT" / "game").read_bytes()},
        "p2-st-1.0": {"PHANT.PRG": (RE / "atari-st" / "phantasie2" / "unpacked" / "disk1" / "PHANT.PRG").read_bytes()},
        "p3-dos-1988": {"MAIN.EXE": p3exe, "MAIN.EXE:root": p3.unpack_root(p3exe)[0]},
        "p3-amiga-1.0": {"PhantasieIII": (RE / "amiga" / "phantasie3" / "unpacked" / "PhantasieIII" / "PhantasieIII").read_bytes()},
        "p4-x68000-1.02": {"P4.X": (RE / "x68000" / "phantasie4" / "unpacked" / "disk1" / "P4.X").read_bytes()},
    }

# Phantasie II, Apple II (4am crack): the Applesoft programs whose names are on one side only (FileSet.Find takes the
# first match, and MN2x / PHANTASIE II are on both), plus SCROLLL0 (Pluto's speech) as its decoded text page.
A2_IMAGES = RE / "apple2" / "original-images" / "phantasie2" / "4am-crack"
A2_PROGRAMS = {"a": ["OUTDOORS2", "TOWN2", "CEMETARY2", "CHAR2", "INTRO2", "INFO"], "b": ["DUNGON2", "WIN"]}
A2_OWN = Path(__file__).resolve().parent / "p2_apple2_own_wording.json"


def a2_page_text(raw: bytes) -> bytes:
    """A text-page dump as the importer's `<file>:page` key: its 24 rows (right-stripped) joined by newlines."""
    return "\n".join(text_page(raw)).encode("latin-1")


def load_apple2() -> dict[str, bytes]:
    out: dict[str, bytes] = {}
    for side, names in A2_PROGRAMS.items():
        disk = RdosDisk(A2_IMAGES / f"phantasie2-4am-side-{side}.dsk")
        for n in names:
            out[n] = disk.read(n)
    out["SCROLLL0:page"] = a2_page_text(RdosDisk(A2_IMAGES / "phantasie2-4am-side-b.dsk").read("SCROLLL0"))
    return out


P1 = ["p1-dos-1987", "p1-amiga-1.0"]
P2 = ["p2-st-1.0"]
P3 = ["p3-dos-1988", "p3-amiga-1.0"]
P4 = ["p4-x68000-1.02"]
ALL = P1 + P2 + P3 + P4

# Areas that are ours by definition (the remaster's shell: options, menus, window title).
OURS = ("option.", "options.", "menu.", "game.", "core.treasury.")  # s175: the Guild treasury is our own text

# folder -> (primary versions, secondary versions) for ids not covered by AREA_RULES.
FOLDERS = {
    "phantasie1": (P1, P2 + P3),  # II and III extend I's table
    "phantasie2": (P2, P1),
    "phantasie3": (P3, P1),
    "shared": (ALL, []),
}
# shared table areas that belong to one game
AREA_RULES = [("p1", P1, P2 + P3), ("p2", P2, P1), ("p3", P3, P1), ("p4", P4, [])]

PLACEHOLDER = re.compile(r"\{[^{}]*\}")
MIN_LEN = 4


def candidates(folder: str, sid: str) -> tuple[list[str], list[str]]:
    if folder == "shared":
        area = sid.split(".", 1)[0]
        for prefix, prim, sec in AREA_RULES:
            if area.startswith(prefix) and (len(area) == len(prefix) or not area[len(prefix)].isdigit()):
                return prim, sec
    return FOLDERS[folder]


def segments(value: str) -> list[tuple[str, bool]]:
    """Split into (text, is_placeholder) runs."""
    out, pos = [], 0
    for m in PLACEHOLDER.finditer(value):
        if m.start() > pos:
            out.append((value[pos:m.start()], False))
        out.append((m.group(0), True))
        pos = m.end()
    if pos < len(value):
        out.append((value[pos:], False))
    return out


def core_of(text: str) -> tuple[str, str, str]:
    stripped = text.strip()
    if not stripped:
        return text, "", ""
    lead = text[:len(text) - len(text.lstrip())]
    trail = text[len(text.rstrip()):]
    return lead, stripped, trail


def eligible(core: str) -> bool:
    return len(core) >= MIN_LEN and sum(c.isalpha() for c in core) >= 2


def find(files: dict[str, bytes], core: str):
    try:
        needle = core.encode("latin-1")
    except UnicodeEncodeError:
        return None
    for name, data in files.items():
        at = data.find(needle)
        if at >= 0:
            return [name, at, len(needle)]
    return None


def pieces(files: dict[str, bytes], text: str, runs: bool) -> list[tuple[int, int, list]]:
    """(start, end, ref) pieces of one literal run that are found in a version's files: the whole run (less its outer
    spaces), else (with ``runs``: the game's own versions, not the secondary ones, whose matches would be
    coincidences), for a run of three or more words (the ST joins a box's lines into one sentence), the longest runs of
    two or more whole words (10 characters or more) found, left to right."""
    lead, core, _ = core_of(text)
    if not eligible(core):
        return []
    if (ref := find(files, core)) is not None:
        return [(len(lead), len(lead) + len(core), ref)]
    toks = [(m.start(), m.end()) for m in re.finditer(r"\S+", text)]
    if not runs or len(toks) < 3:
        return []
    out, i = [], 0
    while i < len(toks):
        for j in range(len(toks), i, -1):
            a, b = toks[i][0], toks[j - 1][1]
            piece = text[a:b]
            if j - i >= 2 and len(piece) >= 10 and (ref := find(files, piece)) is not None:
                out.append((a, b, ref))
                i = j
                break
        else:
            i += 1
    return out


def sources_for(versions, folder: str, sid: str, value: str):
    prim, sec = candidates(folder, sid)
    segs = segments(value)
    found: dict[str, dict[int, list]] = {}
    covered: dict[str, set] = {}
    for v in prim + sec:
        found[v] = {i: pieces(versions[v], text, v in prim) for i, (text, ph) in enumerate(segs) if not ph}
        covered[v] = {(i, c) for i, ps in found[v].items() for a, b, _ in ps for c in range(a, b)}
    original = set().union(*(covered[v] for v in prim)) or set().union(*(covered[v] for v in sec))
    if not original:
        return None
    if not any(original <= covered[v] for v in prim + sec):
        # Versions found different pieces (one version's wording, another's coincidences): keep the version that
        # covers most, as the original parts, and the alternatives that cover at least that.
        best = max(prim + sec, key=lambda v: (len(covered[v]), v in prim))
        original = covered[best]
    alts = []
    for v in prim + sec:
        if not original <= covered[v]:
            continue
        parts: list = []
        for i, (text, ph) in enumerate(segs):
            pos = 0
            for a, b, ref in ([] if ph else found[v][i]):
                if a > pos:
                    parts.append(text[pos:a])
                parts.append(ref)
                pos = b
            if pos < len(text):
                parts.append(text[pos:])
        merged: list = []
        for p in parts:
            if isinstance(p, str) and merged and isinstance(merged[-1], str):
                merged[-1] += p
            else:
                merged.append(p)
        resolved = "".join(p if isinstance(p, str) else versions[v][p[0]][p[1]:p[1] + p[2]].decode("latin-1")
                           for p in merged)
        assert resolved == value, (sid, v, resolved, value)
        alts.append({"from": v, "parts": merged})
    return alts


def apple2_verbatim(apple: dict[str, bytes], versions, alts: list) -> dict | None:
    """The `p2-apple2` alternative of an id whose ST parts are all found verbatim in the Apple's programs."""
    st = next((a for a in alts if a["from"] == "p2-st-1.0"), None)
    if st is None:
        return None
    parts: list = []
    for p in st["parts"]:
        if isinstance(p, str):
            parts.append(p)
            continue
        if (ref := find(apple, versions["p2-st-1.0"][p[0]][p[1]:p[1] + p[2]].decode("latin-1"))) is None:
            return None
        parts.append(ref)
    return {"from": "p2-apple2", "parts": parts}


def resolve(files: dict[str, bytes], parts: list) -> str:
    return "".join(p if isinstance(p, str) else files[p[0]][p[1]:p[1] + p[2]].decode("latin-1") for p in parts)


def build(versions, folder: str, apple: dict[str, bytes], own: dict) -> dict:
    table = json.loads((GAMEDATA / folder / "strings" / "en.json").read_text(encoding="utf-8"))
    out = {}
    for sid, value in table["strings"].items():
        if not isinstance(value, str) or sid.startswith(OURS):
            continue
        if (alts := sources_for(versions, folder, sid, value)) is not None:
            if not alts:
                raise SystemExit(f"{folder} {sid}: original parts but no version has them all")
            if (a2 := apple2_verbatim(apple, versions, alts)) is not None:
                assert resolve(apple, a2["parts"]) == value, (folder, sid)
                alts.append(a2)
            elif sid in own.get(folder, {}):
                parts = own[folder][sid]
                text = resolve(apple, parts)
                assert text.strip() and "\x00" not in text, (folder, sid, text)
                alts.append({"from": "p2-apple2", "wording": "own", "parts": parts})
            out[sid] = alts
    for sid in own.get(folder, {}):
        if sid not in out or not any(a["from"] == "p2-apple2" for a in out[sid]):
            raise SystemExit(f"{folder} {sid}: in {A2_OWN.name} but not an id the Apple lacks verbatim")
    return {
        "_schema": "openphantasie.string-sources/v1",
        "_note": "Generated by tools/scripts/build_string_sources.py: where each id's original text sits in the "
                 "player's own copy (version profile, file, offset, length; latin-1). Literal parts are ours. The "
                 "importer resolves these into strings/en.<profile>.json; the public en.json ships the ids with "
                 "\"...\" in place of each reference. \"wording\": \"own\" = the version prints this line in its own "
                 "wording (Phantasie II's Apple II, D2-050): it resolves to that text, not to the en.json value.",
        "game": table.get("game", folder),
        "language": "en",
        "strings": out,
    }


def main() -> int:
    check = "--check" in sys.argv[1:]
    versions = load_versions()
    apple = load_apple2()
    own = json.loads(A2_OWN.read_text(encoding="utf-8"))["strings"] if A2_OWN.exists() else {}
    if "--show-apple" in sys.argv[1:]:
        for folder, ids in own.items():
            for sid, parts in ids.items():
                print(f"{folder} {sid}: {resolve(apple, parts)!r}")
        return 0
    bad = 0
    for folder in FOLDERS:
        data = build(versions, folder, apple, own)
        path = GAMEDATA / folder / "strings" / "en.sources.json"
        text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
        if check:
            if not path.exists() or path.read_text(encoding="utf-8") != text:
                print(f"stale: {path.relative_to(REPO)}")
                bad = 1
        else:
            path.write_text(text, encoding="utf-8")
            print(f"{path.relative_to(REPO)}: {len(data['strings'])} ids")
    return bad


if __name__ == "__main__":
    raise SystemExit(main())
