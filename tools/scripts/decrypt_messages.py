#!/usr/bin/env python3
"""Decrypt MESS1..MESS10 → extracted/messages.json.

Cipher (confirmed against all 10 files; English narration, Filmon/Bleeb/etc.):

  For file number n = 1..10:
      key  = (0xFF - 3*n) & 0xFF
      phase = (6 - 3*n) % 8
      for i in 0..len-1:
          plain[i] = (cipher[i] - key) & 0xFF
          key = (key + 1 + 0x20 * ((phase + i) % 8)) & 0xFF

  Equivalently, the running key advances by the 8-step cycle
      [0x01, 0x21, 0x41, 0x61, 0x81, 0xA1, 0xC1, 0xE1]
  starting at index ``phase``.

Layout after decrypt (per file):
  - Leading u16 LE **slot table** (0 or multiples of 41). Slot value S
    addresses a record at file offset ``base + S`` where
    ``base = text_start - 41`` and ``text_start`` is the first record.
  - **Records** of 41 bytes: 1 prefix byte + 40 columns of text
    (space-padded). Prefix 0x28/0x50/0x01/0xFF appear to be type/length
    hints (40 or 80 cols, continuation, etc.); keep them as data.
  - A few pad bytes may sit between the slot table and the first record
    (5 bytes on MESS2..MESS10; none on MESS1).

Does **not** use the SCROLLS.DTX scheme (see decrypt_scrolls.py).
"""
from __future__ import annotations

import struct

from re_common import read, write_json

REC = 41  # 1 prefix + 40 text columns
LINE = 40
MESS_IDS = range(1, 11)
STEP_CYCLE = (0x01, 0x21, 0x41, 0x61, 0x81, 0xA1, 0xC1, 0xE1)


def decrypt_mess(cipher: bytes, n: int) -> bytes:
    """Decrypt one MESS file; ``n`` is the file number 1..10."""
    key = (0xFF - 3 * n) & 0xFF
    phase = (6 - 3 * n) % 8
    out = bytearray(len(cipher))
    for i, c in enumerate(cipher):
        out[i] = (c - key) & 0xFF
        key = (key + STEP_CYCLE[(phase + i) % 8]) & 0xFF
    return bytes(out)


def parse_slot_table(plain: bytes) -> list[int]:
    """Leading u16 LE slot offsets (0 or multiples of REC)."""
    slots: list[int] = []
    i = 0
    while i + 2 <= len(plain):
        w = struct.unpack_from("<H", plain, i)[0]
        if w != 0 and (w % REC != 0 or w >= len(plain)):
            break
        slots.append(w)
        i += 2
    return slots


def find_text_start(plain: bytes, slots: list[int], table_end: int) -> int:
    """Locate first 41-byte record after the slot table.

    Some files insert a few pad bytes between the table and records
    (MESS2..MESS10 use 5); choose the alignment where slot offsets land
    on records and the first line is mostly printable.
    """
    nonzero = [s for s in slots if s]
    if not nonzero:
        return table_end
    max_off = max(nonzero)

    def score(ts: int) -> float:
        base = ts - REC
        if ts < table_end or base < 0:
            return -1e9
        if base + max_off + REC > len(plain):
            return -1e9
        # Every slot must address a record boundary relative to text_start.
        for s in nonzero:
            addr = base + s
            if (addr - ts) % REC != 0:
                return -1e9
        chunk = plain[ts : ts + REC]
        if len(chunk) < REC:
            return -1e9
        printable = sum(1 for c in chunk[1:] if 32 <= c < 127)
        return printable + (0.5 if 32 <= chunk[0] < 127 or chunk[0] in (0x01, 0xFF) else 0)

    best_ts, best_sc = table_end, score(table_end)
    for pad in range(0, REC):
        ts = table_end + pad
        sc = score(ts)
        if sc > best_sc:
            best_ts, best_sc = ts, sc
    return best_ts


def records_from(plain: bytes, text_start: int) -> list[dict]:
    out = []
    pos = text_start
    while pos + REC <= len(plain):
        rec = plain[pos : pos + REC]
        prefix = rec[0]
        text = rec[1:1 + LINE]
        out.append({
            "offset": pos,
            "record_index": (pos - text_start) // REC,
            "prefix": prefix,
            "text": text.decode("ascii", errors="replace").rstrip(" "),
            "raw_line": text.decode("ascii", errors="replace"),
        })
        pos += REC
    return out


def printable_ratio(b: bytes) -> float:
    return sum(1 for c in b if c == 0 or 32 <= c < 127) / max(1, len(b))


def main():
    areas = []
    for n in MESS_IDS:
        name = f"MESS{n}"
        cipher = read(name)
        plain = decrypt_mess(cipher, n)
        slots = parse_slot_table(plain)
        table_end = len(slots) * 2
        text_start = find_text_start(plain, slots, table_end)
        nonzero = [s for s in slots if s]
        base = text_start - REC if nonzero else text_start
        recs = records_from(plain, text_start)
        pr = printable_ratio(plain)

        # Resolve each slot to a record when possible.
        slot_msgs = []
        for si, off in enumerate(slots):
            if off == 0:
                slot_msgs.append({"slot": si, "offset": 0, "text": None})
                continue
            addr = base + off
            rel = addr - text_start
            if rel >= 0 and rel % REC == 0 and rel // REC < len(recs):
                r = recs[rel // REC]
                slot_msgs.append({
                    "slot": si,
                    "offset": off,
                    "file_offset": addr,
                    "prefix": r["prefix"],
                    "text": r["text"],
                })
            else:
                slot_msgs.append({
                    "slot": si,
                    "offset": off,
                    "file_offset": addr,
                    "text": None,
                    "_note": "offset did not land on a record",
                })

        areas.append({
            "id": n,
            "source": name,
            "size": len(cipher),
            "initial_key": (0xFF - 3 * n) & 0xFF,
            "phase": (6 - 3 * n) % 8,
            "printable_ratio": round(pr, 4),
            "text_start": text_start,
            "base": base,
            "slot_count": len(slots),
            "record_count": len(recs),
            "slots": slot_msgs,
            "records": recs,
        })
        assert pr > 0.90, f"{name} decrypt looks wrong (pr={pr})"

    path = write_json("messages.json", {
        "_source": "MESS1..MESS10",
        "_cipher": (
            "key=(0xFF-3*n); phase=(6-3*n)%8; "
            "plain[i]=(cipher[i]-key)&0xFF; "
            "key=(key+1+0x20*((phase+i)%8))&0xFF; "
            "records=41 bytes (1 prefix + 40 text)"
        ),
        "areas": areas,
    })
    total_recs = sum(a["record_count"] for a in areas)
    print(f"wrote {path} ({len(areas)} files, {total_recs} records)")


if __name__ == "__main__":
    main()
