#!/usr/bin/env python3
"""Disassemble P4.X (X68000 Phantasie IV) with function names and named call targets (s108).

Usage: x68k_p4_calls.py OUTDIR
Writes OUTDIR/p4x.bin (the image: file minus the 0x40 HU header, so offsets = P4.X image addresses),
OUTDIR/p4x.dis (m68k-linux-gnu-objdump) and OUTDIR/calls.txt ("== _name" at each symbol, "jsr ... <_callee>").
FLOAT2 line-F words ($FExx) show as `.short 0xfeXX` (objdump does not know them; FORMAT.md 3). Needs binutils-m68k-linux-gnu.
"""
import json
import pathlib
import re
import subprocess
import sys

root = pathlib.Path(__file__).resolve().parents[2] / 'reverse-engineering/x68000/phantasie4'
out = pathlib.Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
(out / 'p4x.bin').write_bytes((root / 'unpacked/disk1/P4.X').read_bytes()[0x40:])
dis = subprocess.run(['m68k-linux-gnu-objdump', '-D', '-b', 'binary', '-m', 'm68k:68000', str(out / 'p4x.bin')],
                     capture_output=True, text=True, check=True).stdout
(out / 'p4x.dis').write_text(dis)
d = json.loads((root / 'extracted/p4x-symbols.json').read_text())
syms = d['symbols'] if isinstance(d, dict) else d
names = {int(s['value'], 16): s['name'] for s in syms}
lines = []
for line in dis.splitlines():
    m = re.match(r'\s+([0-9a-f]+):\t[^\t]*\t(.*)', line)
    if not m:
        continue
    a = int(m.group(1), 16)
    if a in names:
        lines.append(f'== {names[a]}')
    ins = m.group(2)
    j = re.search(r'jsr 0x([0-9a-f]+)', ins)
    if j:
        ins += f'  <{names.get(int(j.group(1), 16), "?")}>'
    lines.append(f'{a:x}: {ins}')
(out / 'calls.txt').write_text('\n'.join(lines) + '\n')
print(out / 'calls.txt')
