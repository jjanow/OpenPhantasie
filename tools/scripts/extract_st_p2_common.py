"""Shared helpers for the Phantasie II (Atari ST) data-file extractors (Phase 09 T3, task 3b.8).

Inputs: reverse-engineering/atari-st/phantasie2/unpacked/disk{1,2}/ (the clean v1.0 Pasti set, see its README.md).
Outputs: reverse-engineering/atari-st/phantasie2/extracted/<topic>/.  Format notes: ../FORMAT.md §9 onwards.

Run every extractor from the repo root with ``python3 -I tools/scripts/extract_st_p2_<topic>.py``.
"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

REPO = Path(__file__).resolve().parents[2]
P2 = REPO / "reverse-engineering" / "atari-st" / "phantasie2"
DISK1 = P2 / "unpacked" / "disk1"
DISK2 = P2 / "unpacked" / "disk2"
EXTRACTED = P2 / "extracted"
DOS_ORIG = REPO / "reverse-engineering" / "dos" / "original-files"
AMIGA = REPO / "reverse-engineering" / "amiga" / "unpacked" / "PHANT"

# The 17 overworld regions II ships (OUTn.DAT on disk 2). UTILIT2.PRG 0xb2c (new game, mode 2) walks n = 1..31 and
# skips 4, 9, 10, 13..16, 19..21, 23..25, 29, i.e. exactly this list, in this order, for MAPS.INT [C].
REGIONS = [1, 2, 3, 5, 6, 7, 8, 11, 12, 17, 18, 22, 26, 27, 28, 30, 31]
DUNGEONS = list(range(1, 9))
FOG = 120


def d1(name: str) -> bytes:
    return (DISK1 / name).read_bytes()


def d2(name: str) -> bytes:
    return (DISK2 / name).read_bytes()


def amiga(name: str) -> bytes | None:
    for p in AMIGA.iterdir():
        if p.name.lower() == name.lower():
            return p.read_bytes()
    return None


def out_dir(topic: str) -> Path:
    p = EXTRACTED / topic
    p.mkdir(parents=True, exist_ok=True)
    return p


def write_json(topic: str, name: str, obj) -> Path:
    path = out_dir(topic) / name
    path.write_text(json.dumps(obj, indent=2, ensure_ascii=False) + "\n")
    return path


def write_text(topic: str, name: str, text: str) -> Path:
    path = out_dir(topic) / name
    path.write_text(text)
    return path


def rel(p: Path) -> str:
    return str(p.relative_to(REPO))


def unfog(b: int) -> int:
    return b - FOG if b >= FOG else b
