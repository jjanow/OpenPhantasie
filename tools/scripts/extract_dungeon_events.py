#!/usr/bin/env python3
"""Add DOS-accurate event messages/menus to dungeons.json (s36, P0.8).

For every level n (DNGn + MESSn) this emulates OV2 ``0x2a95`` (message display + menu) on the decrypted
MESSn image, exactly as the code reads it (endgame.md §2.1, "Message and menu format"):

  - ``0x2418(id)``: slot word ``w = u16[2*(id-1)]``; the slot is empty when ``w == u16[2*id]``; otherwise the
    record at ``0xe6 + w`` is ``prefix, 40 chars`` (the text base 0xe6 is hard-coded: ``0x65ec - 0x6506``).
  - The first record's prefix is the message length ``len`` in characters; the text runs on through records
    ``id+1, id+2 …`` (one per 40 characters).
  - Option count: the prefix of record ``id+1`` (read when the 40th character is passed), minus 1; if
    ``len < 40`` the count is forced to 3 − 1 = 2. A menu exists only when ``2 <= prefix <= 20``.
  - Each option label is the next ``w`` characters of the same text stream (``w = 11`` when prefix <= 7, else 3).
  - The return value is 1 for plain text (or an empty slot), else the 1-based choice.

Output per level: ``messages`` = {id: {"lines": [...], "options": [...]}} for every non-empty id 1..114.
The interpreter itself runs on the raw level bytes (``raw_hex``); this table only feeds the text/menus.

Continuation records (s41): a message longer than 40 characters runs on through the next slots, and the prefix of
those records is the option-count byte (<= 20), not a length. ``0x2a95`` would still show such a slot if asked for
it, so it stays in the table, tagged ``"continuation_of": s`` (s = the message it belongs to). Example: DNG1 7 is the
second line of DNG1 6 ("THE PARTY MEMBERS CHOKE AND GASP"); asked for on its own it would show a blank line and a
stray menu "THE PARTY" / "MEMBERS CHO". Nothing in DNG1..10 asks for a continuation: ``main`` fails if any referenced
id (cells, event records, script ops 11/16/19, SET-CELL values) is tagged.
"""
from __future__ import annotations

import json
import struct
from pathlib import Path

from decrypt_messages import decrypt_mess
from re_common import read

ROOT = Path(__file__).resolve().parents[2]
DUNGEONS = ROOT / "src/OpenPhantasie.Data/GameData/phantasie1/dungeons.json"
TEXT_BASE = 0xE6
MAX_ID = 114  # slot table = 115 words (0xe6 / 2)


def load_slot(plain: bytes, msg_id: int):
    """OV2 0x2418: (prefix, 40 chars) or None for an empty slot."""
    i = 2 * (msg_id - 1)
    if i < 0 or i + 4 > TEXT_BASE + 2:
        return None
    w, nxt = struct.unpack_from("<HH", plain, i)
    if w == nxt:
        return None
    p = TEXT_BASE + w
    rec = plain[p:p + 41]
    if len(rec) < 41:
        rec = rec + bytes(41 - len(rec))
    return rec[0], rec[1:41]


def message(plain: bytes, msg_id: int):
    first = load_slot(plain, msg_id)
    if first is None:
        return None
    length, buf = first
    opts = 1
    cur = msg_id
    text = []
    di = 0
    while di < length:
        text.append(buf[di % 40])
        di += 1
        if di % 40 == 0:
            cur += 1
            nxt = load_slot(plain, cur)
            if nxt is not None:
                if di == 40:
                    opts = nxt[0]
                buf = nxt[1]
    if length < 40:
        opts = 3
    options = []
    cells = []
    if 2 <= opts <= 20:
        width = 11 if opts <= 7 else 3
        for _ in range(opts - 1):
            label = []
            for _ in range(width):
                label.append(buf[di % 40])
                di += 1
                if di % 40 == 0:
                    cur += 1
                    nxt = load_slot(plain, cur)
                    if nxt is not None:
                        buf = nxt[1]
            options.append(bytes(label).decode("latin-1").strip())
            cells.append(bytes(label).decode("latin-1"))
    raw = bytes(text).decode("latin-1")
    lines = [raw[i:i + 40].rstrip() for i in range(0, len(raw), 40)]
    out = {"lines": lines, "options": options}
    if options:
        # s153: the labels as stored (11 or 3 characters, blanks kept): I Amiga's buttons print them as they are (h41 +0x1f6).
        out["option_cells"] = cells
    return out


OPTION_BYTE_MAX = 20  # 0x2be5: a prefix above 20 is never read as an option count


def continuation_of(plain: bytes, msg_id: int):
    """The start id whose text runs into slot ``msg_id``, or None when ``msg_id`` starts a message.

    Walk back through records whose prefix is an option byte (<= 20) to the first record whose prefix is a length
    (> 20); ``msg_id`` belongs to it when that length reaches this far. Orphan starts (DNG1 39, DNG3/8/10 111) are
    followed by a record with a length prefix, so the walk stops there and the next message stays a start.
    """
    rec = load_slot(plain, msg_id)
    if rec is None or rec[0] > OPTION_BYTE_MAX:
        return None
    s = msg_id - 1
    while s >= 1:
        prev = load_slot(plain, s)
        if prev is None:
            return None
        if prev[0] > OPTION_BYTE_MAX:
            return s if prev[0] > 40 * (msg_id - s) else None
        s -= 1
    return None


SET_CELL_OPS = (14, 15)
FOUR_BYTE_OPS = (12, 13, 14, 15, 18, 19)


def referenced_ids(raw: bytes):
    """Every MESS id the level can ask 0x2a95 / 0x23c0 for (dungeon-representation.md, endgame.md section 2.1)."""
    refs = {}

    def cell(b, why):
        if 2 <= b <= 6:
            refs.setdefault(b + 4, why)            # trap message a + 4 (0x306f)
        elif 8 <= b <= 0x11:
            refs.setdefault(b + 3, why)            # wandering cell, no fight (0x2265)
        elif 0x12 <= b <= 0x7f:
            refs.setdefault(b - 0x11, why)         # description cell (0x298b)
        elif 0x8b <= b <= 0xb8:
            refs.setdefault(b - 0x76, why)         # message cell (0x22c8)

    for i, b in enumerate(raw[:1254]):
        cell(b, f"cell {i % 33},{i // 33}")
    scripts = set()
    for k in range(1, 36):
        r = raw[0x4f6 + 5 * k:0x4f6 + 5 * k + 6]
        if r[0] in (0, 0xff):
            continue
        refs.setdefault(r[0] & 0x7f, f"event {k}")
        scripts.update(r[2:6])                     # r[c + 1], c = 1..4 (option 4 spills into the next record)
    for sid in scripts:
        q = 0x5aa + 2 * sid
        for _ in range(6):
            ob, a = raw[q], raw[q + 1]
            op = ob % 128
            if op > 0x3f:
                op -= 0x40
            if op == 11:
                refs.setdefault(a, f"script {sid} op 11")
            elif op == 16:
                refs.setdefault(a + 4, f"script {sid} op 16")
            elif op == 19:
                refs.setdefault(raw[q + 3], f"script {sid} TEST")
            elif op in SET_CELL_OPS:
                cell(raw[q + 3], f"script {sid} SET-CELL")
            q += 4 if op in FOUR_BYTE_OPS else 2
            if ob < 0x80:
                break
    return refs


def main():
    data = json.loads(DUNGEONS.read_text())
    for n in range(1, 11):
        plain = decrypt_mess(read(f"MESS{n}"), n)
        msgs = {}
        for mid in range(1, MAX_ID + 1):
            m = message(plain, mid)
            if m is not None:
                c = continuation_of(plain, mid)
                if c is not None:
                    m["continuation_of"] = c
                msgs[str(mid)] = m
        raw = bytes.fromhex(data["levels"][f"DNG{n}"]["raw_hex"])
        for mid, why in sorted(referenced_ids(raw).items()):
            if "continuation_of" in msgs.get(str(mid), {}):
                raise SystemExit(f"DNG{n}: {why} asks for message {mid}, tagged as a continuation of "
                                 f"{msgs[str(mid)]['continuation_of']} - the rule is wrong")
        data["levels"][f"DNG{n}"]["messages"] = msgs
    data["_messages_note"] = (
        "levels.*.messages: MESSn text + menu options as OV2 0x2a95 shows them (s36, "
        "tools/scripts/extract_dungeon_events.py). Event records/scripts are read from raw_hex at run time. "
        "continuation_of (s41): the slot is a later line of that message; DNG1..10 never ask for one.")
    DUNGEONS.write_text(json.dumps(data, indent=2) + "\n")


if __name__ == "__main__":
    main()
