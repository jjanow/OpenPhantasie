#!/usr/bin/env python3
"""Prepare Phantasie IV's P4.X for a Ghidra (68000) decompile: raw image + symbols + FPACK/DOS call list.

Phase 09 T6.2 (03d 3d.4/3d.5). Ghidra's 68000 language does not know the Human68k line-F calls (`$FExx` FPACK floating
point / integer helpers, `$FFxx` DOS calls), so a raw import stops at each one. This script writes a patched copy:
  - every `$FExx` / `$FFxx` word at an instruction start -> `trap #14` / `trap #15` (listed in linef.json, which
    GhidraP4Setup.java turns into PRE comments that show in the decompile);
  - `movem.l Da/Db,-(sp); FPACK __CLMUL/__CLDIV/__CLMOD/__CUMUL/__CUDIV/__CUMOD; movem.l (sp)+,Da/Db`
    -> 68020 `muls.l` / `divs.l` / `divsl.l + exg` (+ nops), so the integer arithmetic (array strides!) is visible.
    The result of those calls is left in Da (checked: `_rand` = rreg % n + 1).
  - float calls (`__CD*`, `__CF*`) stay traps: read them in the disassembly. Operand order: the double pushed last is
    the left operand (FORMAT.md section 6).
Outputs in OUTDIR: p4x.bin (image, load at 0), linef.json, syms.json.

Then (one Ghidra at a time, capped heap):
  MAXMEM=4G flock /tmp/claude-1000/ghidra.lock <ghidra>/support/analyzeHeadless OUTDIR p4 -import OUTDIR/p4x.bin \
      -loader BinaryLoader -processor 68000:BE:32:default -noanalysis -scriptPath tools/scripts \
      -postScript GhidraP4Setup.java OUTDIR
which writes OUTDIR/decomp.c (all 705 text functions).

Usage: x68k_p4_ghidra_prep.py OUTDIR
"""
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from x68k_x import XFile  # noqa: E402

P4X = os.path.join(HERE, '..', '..', 'reverse-engineering', 'x68000', 'phantasie4', 'unpacked', 'disk1', 'P4.X')
PAIRS = {'48e7c000': (0, 1, '4cdf0003'), '48e76000': (1, 2, '4cdf0006'), '48e73000': (2, 3, '4cdf000c')}


def main():
    import capstone
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    x = XFile(open(P4X, 'rb').read())
    img = bytearray(x.image)
    md = capstone.Cs(capstone.CS_ARCH_M68K, capstone.CS_MODE_M68K_000 | capstone.CS_MODE_BIG_ENDIAN)
    texts = sorted((s['value'], s['name']) for s in x.symbols if s['kind'] == 'text')
    lf = {}
    for i, (a, _) in enumerate(texts):           # linear sweep per function; line-F words at instruction starts
        end = texts[i + 1][0] if i + 1 < len(texts) else x.text
        p = a
        while p < end:
            w = struct.unpack('>H', img[p:p + 2])[0]
            if (w >> 8) in (0xFE, 0xFF):
                lf[p] = w
                p += 2
                continue
            ins = next(md.disasm(bytes(img[p:min(end, p + 10)]), p), None)
            p += ins.size if ins else 2
    for p, w in lf.items():
        img[p:p + 2] = b'\x4e\x4e' if (w >> 8) == 0xFE else b'\x4e\x4f'
    n = 0
    for p, w in lf.items():
        if not 0xFEE0 <= w <= 0xFEE5:
            continue
        pre = bytes(img[p - 4:p]).hex()
        if pre not in PAIRS or bytes(img[p + 2:p + 6]).hex() != PAIRS[pre][2]:
            continue
        a, b, _ = PAIRS[pre]
        op = w - 0xFEE0
        signed = 0x0800 if op < 3 else 0
        if op % 3 == 0:      # mul: mul[su].l Db,Da
            ins = struct.pack('>HH', 0x4C00 | b, (a << 12) | signed)
        elif op % 3 == 1:    # div: div[su].l Db,Da (32-bit quotient)
            ins = struct.pack('>HH', 0x4C40 | b, (a << 12) | signed | a)
        else:                # mod: div[su]l.l Db,Db:Da (remainder in Db) then exg Da,Db
            ins = struct.pack('>HHH', 0x4C40 | b, (a << 12) | signed | b, 0xC140 | (a << 9) | b)
        img[p - 4:p + 6] = ins + b'\x4e\x71' * ((10 - len(ins)) // 2)
        n += 1
    open(os.path.join(out, 'p4x.bin'), 'wb').write(bytes(img))
    json.dump({'%x' % p: '%04x' % w for p, w in lf.items()}, open(os.path.join(out, 'linef.json'), 'w'))
    json.dump([(s['name'], s['value'], s['kind']) for s in x.symbols], open(os.path.join(out, 'syms.json'), 'w'))
    print(f'{len(lf)} line-F words, {n} integer FPACK calls rewritten -> {out}')


if __name__ == '__main__':
    sys.exit(main())
