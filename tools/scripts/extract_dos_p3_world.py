#!/usr/bin/env python3
"""Phantasie III (DOS SSI 1988) wilderness maps and places -> reverse-engineering/dos/phantasie3/extracted/world.json.

Sources (`../../reverse-engineering/dos/phantasie3/FORMAT.md` §9.1-9.2, `logic-specs/planes.md` §2-4):
- `WORLD.DAT`: Scandor, 50 x 75 tile bytes, row-major [C].
- `PLNS.DAT`: Light 9 x 9 at 0x000, Dark 9 x 9 at 0x051, Netherworld 31 x 16 at 0x0a2 (root `0x125d`) [C].
- Places: hard-coded in overlay 3 `place_at_party` (ovl3 `0xa22`). This script scans the routine for its
  `cmp word [0x4882], x` / `cmp word [0x4884], y` / `mov word [0x0cbc], type` / `mov word [0xdd84], n` instructions and
  keeps every (x, y, type, n) it finds [C] code bytes. The four Light / Dark squares are set by another code shape
  (`planes.md` §4, read by hand) and are listed below; every place is checked against the tile under it.
- Terrain: the tile-id ranges of ovl3 `0x375` (`planes.md` §3) [C], transcribed (the names are master strings 54 + t).

Run: python3 -I tools/scripts/extract_dos_p3_world.py
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import extract_dos_p3_common as c  # noqa: E402
import extract_dos_p3_tables as t  # noqa: E402

PLACE_ROUTINE = (0xA22, 0x1300)  # ovl3 image range scanned for place squares
PLACE_TYPES = {1: "town", 2: "dungeon", 3: "inn", 4: "map_edge"}

# planes.md §2: plane id -> (key, map source, width, height, arrival square)
PLANES = [
    (0, "scandor", "WORLD.DAT", 0x000, 50, 75, None),
    (1, "light", "PLNS.DAT", 0x000, 9, 9, (4, 1)),
    (2, "dark", "PLNS.DAT", 0x051, 9, 9, (4, 1)),
    (4, "netherworld", "PLNS.DAT", 0x0A2, 31, 16, (7, 3)),
]

# planes.md §4 (read by hand, not by the scan): the Light / Dark places.
HAND_PLACES = [
    {"plane": "light", "x": 4, "y": 1, "type": "town", "number": 15},
    {"plane": "light", "x": 6, "y": 7, "type": "dungeon", "number": 10},
    {"plane": "dark", "x": 4, "y": 1, "type": "town", "number": 16},
    {"plane": "dark", "x": 2, "y": 7, "type": "dungeon", "number": 11},
]

# planes.md §3 [C] (ovl3 0x375): tile-id ranges -> terrain index t (name = master string 54 + t).
TERRAIN_RANGES = [
    (0, 25, 10), (26, 38, 19), (39, 46, 20), (47, 54, 21), (61, 67, 14), (68, 74, 11), (90, 114, 3),
    (115, 154, 2), (155, 187, 9), (188, 205, 0), (206, 209, 6), (214, 221, 6), (222, 222, 13), (223, 224, 6),
    (225, 241, 1), (242, 255, 15),
]
TERRAIN_OTHER = 4
NETHER_OVERRIDES = [(61, 77, 17), (78, 82, 16), (83, 87, 18), (88, 241, 8)]


def scan_places(ovl3: bytes) -> list[dict]:
    """(x, y, type, number) from the immediate operands of place_at_party, in code order."""
    s, e = PLACE_ROUTINE
    x = y = typ = None
    out = []
    i = s
    while i < e:
        b = ovl3[i:i + 6]
        if b[:4] == bytes.fromhex("833e8248"):
            x, i = b[4], i + 5
        elif b[:4] == bytes.fromhex("833e8448"):
            y, i = b[4], i + 5
        elif b[:4] == bytes.fromhex("c706bc0c"):
            typ, i = b[4], i + 6
        elif b[:4] == bytes.fromhex("c70684dd"):
            out.append({"x": x, "y": y, "type": PLACE_TYPES[typ], "number": b[4], "code": f"ovl3 {i:#x}"})
            i += 6
        else:
            i += 1
    return out


def main() -> None:
    img, _, _ = c.unpack_root()
    strs = t.string_table(img)
    ovl3, _ = c.overlay_image(3)

    maps = []
    by_key = {}
    for pid, key, src, off, w, h, arrive in PLANES:
        raw = c.read_file(src)[off:off + w * h]
        assert len(raw) == w * h
        rows = [list(raw[y * w:(y + 1) * w]) for y in range(h)]
        by_key[key] = rows
        maps.append({"plane": pid, "key": key, "source": f"{src} +{off:#x}", "width": w, "height": h,
                     "arrival": None if arrive is None else {"x": arrive[0], "y": arrive[1]}, "tiles": rows})

    scanned = scan_places(ovl3)
    edges = [p for p in scanned if p["type"] == "map_edge"]
    places = []
    for p in scanned:
        if p["type"] == "map_edge":
            continue
        plane = "netherworld" if (p["type"], p["number"]) in (("town", 17), ("dungeon", 9)) else "scandor"
        places.append({"plane": plane, **p})
    places += [{**p, "code": "planes.md §4 (by hand)"} for p in HAND_PLACES]

    # Check every square against its tile (FORMAT §9.1-9.2): Scandor towns 26-38, dungeons 39-46, inns 47-54;
    # plane places on the place tiles 55-60.
    for p in places:
        tile = by_key[p["plane"]][p["y"]][p["x"]]
        p["tile"] = tile
        if p["plane"] == "scandor":
            lo, hi = {"town": (26, 38), "dungeon": (39, 46), "inn": (47, 54)}[p["type"]]
            assert lo <= tile <= hi, p
        else:
            assert 55 <= tile <= 60, p
        p["name"] = {"town": strs[111 + p["number"]], "dungeon": strs[33 + p["number"]],
                     "inn": strs[45 + p["number"]]}[p["type"]]
    counts = {k: sum(1 for p in places if p["type"] == k) for k in ("town", "dungeon", "inn")}
    assert counts == {"town": 17, "dungeon": 11, "inn": 8}, counts

    terrain_names = [strs[54 + i] for i in range(22)]
    c.write_json("world.json", {
        "_source": "WORLD.DAT (Scandor) and PLNS.DAT (planes) tile bytes [C]; places scanned from ovl3 place_at_party "
                   "0xa22 [C] (Light / Dark squares by hand, planes.md section 4); terrain ranges of ovl3 0x375 "
                   "(planes.md section 3) [C]. tiles[y][x], x west -> east, y north -> south.",
        "planes": maps,
        "start": {"plane": 0, "x": 24, "y": 37},
        "places": places,
        "map_edge": {"_note": "x = 0, y = 0, the last row or column is blocked: type 4, number 12 (Scandor) / 15 "
                              "(planes) [C]", "scanned": edges},
        "terrain": {
            "_note": "terrain index t: name = master string 54 + t; the first matching range wins; any other tile = "
                     "4 (no name). Netherworld overrides first. Tiles >= 242 are blocked (River Styx; 'Ocean' on "
                     "Scandor). No random encounters on t 10 (Water), 8 (Lava), 15 (Styx); Water swims, Lava hurts "
                     "(damage size [?]).",
            "names": terrain_names,
            "ranges": [{"first": a, "last": b, "terrain": k} for a, b, k in TERRAIN_RANGES],
            "other": TERRAIN_OTHER,
            "netherworld_overrides": [{"first": a, "last": b, "terrain": k} for a, b, k in NETHER_OVERRIDES],
            "blocked_from": 242,
            "no_encounter": [10, 8, 15],
        },
    })
    print("planes", len(maps), "places", len(places), counts)


if __name__ == "__main__":
    main()
