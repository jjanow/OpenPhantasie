#!/usr/bin/env python3
"""Check the Phantasie II ST graphics extract against live Hatari screens (task 4b).

Shots: reverse-engineering/atari-st/phantasie2/parity/shots/gfx-*.png, 320x200 point-sampled from the 4x Hatari window
(``convert shot.png -sample 320x200! out.png``; harness reverse-engineering/atari-st/working/). Hatari shows an ST gun
value v as v * 0x22, the extract as v * 255 / 7, so colours are compared as 3-bit gun values. The colour-cycled map
registers (4, 7, 14) differ between frames, so the map check maps extract colours to shot colours by majority and counts
the pixels that break that mapping. Run from the repo root: ``python3 -I tools/scripts/verify_st_p2_graphics.py``.
"""
from __future__ import annotations

import os
import sys
from collections import Counter

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_st_p2_common import EXTRACTED, P2  # noqa: E402

GFX = EXTRACTED / "graphics"
SHOTS = P2 / "parity" / "shots"


def guns(path, scale: float) -> np.ndarray:
    a = np.array(Image.open(path).convert("RGB")).astype(np.int32)
    g = np.rint(a / scale).astype(np.int32)
    return (g[..., 0] << 8) | (g[..., 1] << 4) | g[..., 2]


def shot(name: str) -> np.ndarray:
    return guns(SHOTS / name, 0x22)


def ref(rel: str) -> np.ndarray:
    return guns(GFX / rel, 255 / 7)


def mapped_mismatch(s: np.ndarray, r: np.ndarray) -> int:
    m: dict[int, Counter] = {}
    for a, b in zip(r.ravel().tolist(), s.ravel().tolist()):
        m.setdefault(a, Counter())[b] += 1
    return sum(sum(c.values()) - c.most_common(1)[0][1] for c in m.values())


def find(s: np.ndarray, r: np.ndarray, box=None) -> tuple[int, tuple[int, int]]:
    h, w = r.shape
    x0, y0, x1, y1 = box or (0, 0, 320 - w, 200 - h)
    best = (h * w + 1, (0, 0))
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            bad = int(np.count_nonzero(s[y:y + h, x:x + w] != r))
            if bad < best[0]:
                best = (bad, (x, y))
    return best


def main() -> int:
    ok = True

    def report(label: str, bad: int, total: int, allowed: int = 0) -> None:
        nonlocal ok
        good = bad <= allowed
        ok &= good
        print(f"{'OK  ' if good else 'FAIL'} {label}: {bad} of {total} px differ" + (f" (allowed {allowed})" if allowed else ""))

    s = shot("gfx-title.png")
    report("title = COVER1 + COVER2@65, start palette", int(np.count_nonzero(s != ref("screens/title-credits.png"))), 64000)

    s, r = shot("gfx-town.png"), ref("screens/pelnor.png")
    diff = s != r
    for x0, y0, x1, y1 in [(0, 0, 200, 9), (100, 18, 200, 34), (150, 105, 180, 125), (170, 150, 195, 180)]:
        diff[y0:y1, x0:x1] = False  # live overlays: GEM menu text, town name, mouse, walker
    report("PELNOR.PIC, town palette (outside menu text / name / mouse / walker)", int(diff.sum()), 64000)
    bad, pos = find(s, ref("town/walker-5.png"), (100, 140, 200, 158))
    report(f"town walker frame 5 at {pos}", bad, 352)

    s = shot("gfx-overworld-out2.png")
    region = s[15:195, 4:316].copy()
    report("OUT2 map picture, map palette (only the 3 icons + mouse differ, s135)",
           mapped_mismatch(region, ref("maps/overworld/regions/out02.png")), 312 * 180, 244)
    for icon, cell in [("party", (16, 3)), ("inn", (12, 0)), ("dungeon", (10, 2))]:
        x, y = 4 + cell[0] * 12, 15 + cell[1] * 9
        r = ref(f"maps/overworld/icons/{icon}.png")
        report(f"map icon {icon} at cell {cell}", int(np.count_nonzero(s[y:y + 9, x:x + 12] != r)), 108)

    s = shot("gfx-combat.png")
    for rel_, pos in [("portraits/idle/00-human.png", (68, 150)), ("portraits/idle/02-elf.png", (18, 150)),
                      ("portraits/idle/04-gnome.png", (168, 150)), ("portraits/idle/09-troll.png", (268, 150)),
                      ("portraits/idle/11-orc.png", (218, 150)), ("monsters/idle/07-wild-dog.png", (144, 75))]:
        r = ref(rel_)
        h, w = r.shape
        report(f"{rel_} at {pos}, combat palette", int(np.count_nonzero(s[pos[1]:pos[1] + h, pos[0]:pos[0] + w] != r)), h * w)
    s, r = shot("gfx-combat-attack.png"), ref("monsters/attacking/07-wild-dog.png")
    report("monsters/attacking/07-wild-dog.png at (144,125)", int(np.count_nonzero(s[125:147, 144:176] != r)), 704)
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
