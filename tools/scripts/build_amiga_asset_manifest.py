#!/usr/bin/env python3
"""Build asset-manifest.json cross-referencing Amiga PNGs to Phase 3 data IDs.

Reads:
  - reverse-engineering/amiga/extracted/extraction-index.json
  - reverse-engineering/dos/extracted/{monsters,names}.json

Writes:
  - reverse-engineering/amiga/extracted/asset-manifest.json
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

from amiga_gfx_common import DOS_EXTRACTED, EXTRACTED


def load_json(path: Path):
    return json.loads(path.read_text())


def main() -> int:
    index_path = EXTRACTED / "extraction-index.json"
    if not index_path.is_file():
        print(f"Missing {index_path}; run extract_amiga_graphics.py first", file=sys.stderr)
        return 1

    index = load_json(index_path)
    dos_monsters = load_json(DOS_EXTRACTED / "monsters.json")["monsters"]
    dos_races = load_json(DOS_EXTRACTED / "names.json")["races"]

    assets = []
    unmapped = []

    def norm_name(s: str) -> str:
        return s.upper().rstrip(".")

    # --- monsters (Phase 3 index == Amiga sprite index) ---
    by_amiga = {m["index"]: m for m in index["monsters"]}
    for dm in dos_monsters:
        idx = dm["index"]
        am = by_amiga.get(idx)
        if am is None:
            unmapped.append(
                {
                    "kind": "monster",
                    "data_id": idx,
                    "name": dm["name"],
                    "reason": "no Amiga sprite at this index",
                }
            )
            continue
        note = None
        if norm_name(am["name"]) != norm_name(dm["name"]):
            note = f"name drift: Amiga={am['name']!r} DOS={dm['name']!r}"
        entry = {
            "kind": "monster",
            "data_id": idx,
            "name": dm["name"],
            "amiga_name": am["name"],
            "sprites": {
                "idle": am["idle"],
                "attacking": am["attacking"],
            },
            "size": {"width": am["width"], "height": am["height"]},
        }
        if note:
            entry["note"] = note
        assets.append(entry)

    extra_monsters = set(by_amiga) - {m["index"] for m in dos_monsters}
    for idx in sorted(extra_monsters):
        am = by_amiga[idx]
        unmapped.append(
            {
                "kind": "monster_sprite",
                "amiga_index": idx,
                "name": am["name"],
                "path": am["idle"],
                "reason": "Amiga sprite index not present in Phase 3 monsters.json",
            }
        )

    # --- race portraits ---
    by_race = {r["index"]: r for r in index["races"]}
    dos_race_ids = set()
    for dr in dos_races:
        idx = dr["index"]
        dos_race_ids.add(idx)
        ar = by_race.get(idx)
        if ar is None:
            unmapped.append(
                {
                    "kind": "race",
                    "data_id": idx,
                    "name": dr["name"],
                    "reason": "no Amiga portrait at this index",
                }
            )
            continue
        assets.append(
            {
                "kind": "race",
                "data_id": idx,
                "name": dr["name"],
                "amiga_name": ar["name"],
                "sprites": {
                    "idle": ar["idle"],
                    "attacking": ar["attacking"],
                },
                "size": {"width": 32, "height": 32},
            }
        )

    for idx in sorted(set(by_race) - dos_race_ids):
        ar = by_race[idx]
        unmapped.append(
            {
                "kind": "race_portrait",
                "amiga_index": idx,
                "name": ar["name"],
                "path": ar["idle"],
                "reason": (
                    "Amiga portrait present but no matching race in Phase 3 "
                    "names.json (playable-race table is 0..14 on DOS)"
                ),
            }
        )

    # --- UI / screens (no Phase 3 data-table ID) ---
    # .pic screens carry "source" (the ADF file); bitmaps / sprites cut from the `game` binary carry
    # "hunk69_offset" (offset inside hunk 69) or "file_offset" (offset in the file) instead (s124-s133).
    def screen_source(scr: dict) -> str:
        if "source" in scr:
            return scr["source"]
        if "hunk69_offset" in scr:
            return f"game hunk 69 +{scr['hunk69_offset']}"
        if "file_offset" in scr:
            return f"game @ {scr['file_offset']}"
        raise KeyError(f"screen {scr.get('path')!r} has no source / hunk69_offset / file_offset")

    for scr in index["screens"]:
        unmapped.append(
            {
                "kind": "ui_screen",
                "path": scr["path"],
                "source": screen_source(scr),
                "reason": "UI/full-screen art — no monster/item/spell data ID",
            }
        )

    unmapped.append(
        {
            "kind": "tiles",
            "reason": (
                "Overworld/dungeon tile bitmaps are not separate ADF files; "
                "map cells in OUT*.DAT / dng* index graphics embedded in the "
                "`game` binary (or drawn at runtime). Needs FS-UAE memory dump "
                "+ Maptapper / blit-routine RE — see FORMAT.md."
            ),
        }
    )

    manifest = {
        "_schema": "phantasie.amiga.asset-manifest/v1",
        "_generated_by": "tools/scripts/build_amiga_asset_manifest.py",
        "_phase3_sources": [
            "reverse-engineering/dos/extracted/monsters.json",
            "reverse-engineering/dos/extracted/names.json",
        ],
        "_amiga_source": "reverse-engineering/amiga/extracted/extraction-index.json",
        "assets": assets,
        "unmapped": unmapped,
        "counts": {
            "mapped_assets": len(assets),
            "unmapped_notes": len(unmapped),
            "monsters": sum(1 for a in assets if a["kind"] == "monster"),
            "races": sum(1 for a in assets if a["kind"] == "race"),
        },
    }

    out = EXTRACTED / "asset-manifest.json"
    out.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")
    print(
        f"Wrote {out} — {manifest['counts']['monsters']} monsters, "
        f"{manifest['counts']['races']} races, "
        f"{manifest['counts']['unmapped_notes']} unmapped notes"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
