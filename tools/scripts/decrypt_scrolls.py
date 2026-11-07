#!/usr/bin/env python3
"""Decrypt SCROLLS.DTX → extracted/scrolls.json.

Cipher (confirmed against all 20 scrolls, printable_ratio=1.0):

  Each scroll is a fixed 800-byte record (20 lines × 40 bytes).
  For scroll index n = 0..19 (SCROLL 1..20):
      step = 9 + 4*n
      plain[i] = ((cipher[i] - (0x0D + step*i)) & 0xFF) & 0x7F
  where i is the offset *within the scroll* (0..799).

High bit on intermediate subtract results is discarded (`& 0x7F`); on-disk
bytes are not ASCII until this transform. MESS1..MESS10 use a different
scheme and are not handled here (see logic-specs/message-and-text.md).
"""
from __future__ import annotations

from re_common import read, write_json

BASE = 0x0D
SCROLL_SIZE = 800  # 20 lines × 40 cols
LINE = 40
NUM_SCROLLS = 20


def decrypt_scroll(cipher: bytes, scroll_index: int) -> bytes:
    step = 9 + 4 * scroll_index
    return bytes(
        ((b - ((BASE + step * i) & 0xFF)) & 0xFF) & 0x7F
        for i, b in enumerate(cipher)
    )


def lines_of(plain: bytes) -> list[str]:
    out = []
    for li in range(SCROLL_SIZE // LINE):
        row = plain[li * LINE : (li + 1) * LINE]
        # NULs pad the remainder of each 40-col row
        text = bytes(c if 32 <= c < 127 else 0 for c in row).split(b"\x00")[0]
        out.append(text.decode("ascii"))
    return out


def main():
    data = read("SCROLLS.DTX")
    assert len(data) == SCROLL_SIZE * NUM_SCROLLS, (
        f"expected {SCROLL_SIZE * NUM_SCROLLS} bytes, got {len(data)}"
    )
    scrolls = []
    for n in range(NUM_SCROLLS):
        chunk = data[n * SCROLL_SIZE : (n + 1) * SCROLL_SIZE]
        plain = decrypt_scroll(chunk, n)
        rows = lines_of(plain)
        body = "\n".join(r.rstrip() for r in rows if r.strip())
        pr = sum(32 <= c < 127 or c == 0 for c in plain) / len(plain)
        scrolls.append({
            "id": n + 1,
            "step": 9 + 4 * n,
            "printable_ratio": round(pr, 4),
            "lines": [r.rstrip() for r in rows],
            "text": body,
        })
        assert pr == 1.0, f"SCROLL {n+1} failed to decrypt cleanly (pr={pr})"
    path = write_json("scrolls.json", {
        "_source": "SCROLLS.DTX",
        "_cipher": (
            "plain[i] = (cipher[i] - (0x0D + step*i)) & 0x7F; "
            "step = 9+4*(id-1); record = 800 bytes"
        ),
        "scrolls": scrolls,
    })
    print(f"wrote {path} ({NUM_SCROLLS} scrolls)")


if __name__ == "__main__":
    main()
