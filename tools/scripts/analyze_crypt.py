#!/usr/bin/env python3
"""Cipher diagnostics for MESS*/SCROLLS.DTX.

Both ciphers are cracked:
  - SCROLLS.DTX → ``decrypt_scrolls.py``
  - MESS1..MESS10 → ``decrypt_messages.py``

This script only prints a quick sanity check (printable ratios).
"""
from __future__ import annotations

from decrypt_messages import decrypt_mess, printable_ratio
from decrypt_scrolls import decrypt_scroll, SCROLL_SIZE
from re_common import read


def main():
    scrolls = read("SCROLLS.DTX")
    s0 = decrypt_scroll(scrolls[:SCROLL_SIZE], 0)
    print(f"SCROLLS.DTX scroll1 printable_ratio={printable_ratio(s0):.3f}")

    for n in range(1, 11):
        d = read(f"MESS{n}")
        plain = decrypt_mess(d, n)
        sample = "".join(
            chr(c) if 32 <= c < 127 else "." for c in plain[200:280]
        )
        print(f"MESS{n}: ratio={printable_ratio(plain):.3f} sample={sample!r}")


if __name__ == "__main__":
    main()
