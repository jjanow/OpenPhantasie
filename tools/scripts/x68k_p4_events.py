#!/usr/bin/env python3
"""Disassemble Phantasie IV (X68000) location event files O_001..O_021.

Phase 09 T6.2 (03d 3d.8). Spec: reverse-engineering/x68000/phantasie4/logic-specs/events.md.
Interpreter in P4.X: `_event` (0x1a1f2, trigger table), `_exe_ev` (0x195b0, opcode switch), `_evadr` / `_evval`
(little-endian u16 operands), `_evj` (conditional jumps / call). The file is loaded whole into `_evbuf` by `_dload`.

File layout [C]:
  byte 0      n = number of trigger entries
  bytes 1-2   unused (0)
  bytes 3-4   u16 LE base = start of the code area (always (n + 1) * 5)
  entry i (1..n) at 5*i: x, y, dir, u16 LE offset (relative to base)
      x = 0x20 -> runs when the map is loaded (`_dload`: _event(0x20, 0, 4))
      x = 0x21 -> runs when the party walks off the map edge (`_dmain`: _dmove returned 7)
      dir 4 = any facing; else facing 0 N, 1 E, 2 S, 3 W must match
  Addresses inside the code (jumps, text) are base-relative; text operands point at NUL-terminated Shift-JIS.

Usage:
  x68k_p4_events.py                 -> validate all 21 files against extracted/text/strings.json (coverage)
  x68k_p4_events.py --dump OUTDIR   -> also write one listing per file (O_0xx.txt)
"""
import json
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
P4 = os.path.join(ROOT, 'reverse-engineering', 'x68000', 'phantasie4')
DISK1 = os.path.join(P4, 'unpacked', 'disk1')
STRINGS = os.path.join(P4, 'extracted', 'text', 'strings.json')

# opcode: (mnemonic, operand kinds). Kinds: b = byte, w = u16 LE value, a = code address, t = text address.
# A 'p' is a padding byte the interpreter skips (opcodes that read one byte but advance by 2 do not exist; every
# one-byte operand op advances exactly 2).
OPS = {
    0x00: ('END', ''),
    0x01: ('JMP', 'a'), 0x02: ('JT', 'a'), 0x03: ('JF', 'a'),
    0x04: ('JGT', 'a'), 0x05: ('JEQ', 'a'), 0x06: ('JLT', 'a'),
    0x07: ('CALL', 'a'), 0x08: ('RET', ''),
    0x09: ('PRINT', 't'), 0x0A: ('TITLE', 't'), 0x0B: ('WAIT', ''),
    0x0C: ('INPUTS', ''), 0x0D: ('CMPS', 't'), 0x0E: ('INPUTN', ''), 0x0F: ('CMPN', 'w'),
    0x10: ('HASITEM', 'b'), 0x11: ('GIVEITEM', 'b'), 0x12: ('CMPGOLD', 'w'), 0x13: ('GIVEGOLD', 'w'),
    0x14: ('TESTLF', 'b'), 0x15: ('SETLF', 'b'), 0x16: ('CLRLF', 'b'),
    0x17: ('TESTSF', 'b'), 0x18: ('SETSF', 'b'), 0x19: ('CLRSF', 'b'),
    0x1A: ('DAMAGE', 'b'), 0x1B: ('WALL2', 'bbb'), 0x1C: ('WALL0', 'bbb'), 0x1D: ('OPENGATE', 'bb'),
    0x1E: ('TELEPORT', 'bbbbb'), 0x1F: ('NOP', 'b'), 0x20: ('SOUND', 'b'), 0x21: ('REDRAW', ''),
    0x22: ('NOENC', ''), 0x23: ('FIGHT', 'b'), 0x24: ('CHEST', 'b'), 0x25: ('CMPLVL', 'b'),
    0x26: ('CHANCE', 'b'), 0x27: ('MCLEAR', ''), 0x28: ('MADD', 't'), 0x29: ('MENU', ''),
    0x2A: ('MJMP', 'ba'), 0x2B: ('POOLADD', 'b'), 0x2C: ('ENCPCT', 'b'), 0x2D: ('POOLCLR', ''),
    0x2E: ('WALL3', 'bbb'), 0x2F: ('LISTEN', ''), 0x30: ('TRAP', 'b'), 0x31: ('STEPBACK', ''),
    0x32: ('TAKEITEM', 'b'), 0x33: ('TAKEGOLD', 'w'), 0x34: ('RACEIS', 'b'), 0x35: ('ENCOPT', 'b'),
    0x36: ('TGLSF', 'b'), 0x37: ('WALLPIC5', 'bb'), 0x38: ('WALLPIC4', 'bb'), 0x39: ('RESTORE', ''),
    0x3A: ('SETFLOOR', 'bbb'),
}
SIZE = {'b': 1, 'w': 2, 'a': 2, 't': 2}
STOP = {0x00, 0x01, 0x08}          # no fall-through


def oplen(op):
    return 1 + sum(SIZE[k] for k in OPS[op][1])


def parse(buf):
    n = buf[0]
    base = buf[3] | buf[4] << 8
    entries = []
    for i in range(1, n + 1):
        x, y, d = buf[5 * i], buf[5 * i + 1], buf[5 * i + 2]
        off = buf[5 * i + 3] | buf[5 * i + 4] << 8
        entries.append((x, y, d, off))
    return n, base, entries


def cstr(buf, p):
    e = buf.index(0, p)
    return buf[p:e].decode('cp932', 'replace'), e + 1


def disasm(buf):
    """Recursive traversal from every trigger entry. Returns (ins dict pc->(op, args, len), texts set, problems)."""
    n, base, entries = parse(buf)
    ins, texts, problems = {}, {}, []
    todo = [base + off for (_, _, _, off) in entries]
    while todo:
        pc = todo.pop()
        while pc not in ins:
            if pc >= len(buf):
                problems.append(f'pc {pc:#x} past end')
                break
            op = buf[pc]
            if op not in OPS:
                problems.append(f'unknown opcode {op:#04x} at {pc:#x}')
                break
            args, q = [], pc + 1
            for k in OPS[op][1]:
                if k == 'b':
                    args.append(buf[q])
                else:
                    v = buf[q] | buf[q + 1] << 8
                    if k in 'at':
                        v += base
                    args.append(v)
                    if k == 'a':
                        todo.append(v)
                    elif k == 't':
                        texts[v] = cstr(buf, v)[0]
                q += SIZE[k]
            ins[pc] = (op, args, q - pc)
            if op in STOP:
                break
            pc = q
    return n, base, entries, ins, texts, problems


def fmt(op, args, texts):
    name, kinds = OPS[op]
    out = []
    for k, v in zip(kinds, args):
        if k == 'a':
            out.append(f'L{v:04x}')
        elif k == 't':
            out.append(f'T{v:04x}')
        elif k == 'w':
            out.append(str(v))
        else:
            out.append(str(v))
    s = f'{name:9s} ' + ', '.join(out)
    for k, v in zip(kinds, args):
        if k == 't':
            s += f'    ; "{texts[v]}"'
    return s.rstrip()


def listing(name, buf):
    n, base, entries, ins, texts, problems = disasm(buf)
    lines = [f'; {name}: {len(buf)} bytes, {n} triggers, code base {base:#x}']
    targets = {a for (op, args, _) in ins.values() for k, a in zip(OPS[op][1], args) if k == 'a'}
    for (x, y, d, off) in entries:
        where = {0x20: 'on map load', 0x21: 'on leaving by the map edge'}.get(x, f'at ({x},{y})')
        lines.append(f';   trigger {where}, facing {"any" if d == 4 else "NESW"[d] if d < 4 else d} -> L{base + off:04x}')
    entry_pcs = {base + off for (*_, off) in entries}
    for pc in sorted(ins):
        op, args, ln = ins[pc]
        lab = f'L{pc:04x}:' if (pc in targets or pc in entry_pcs) else ''
        raw = ' '.join(f'{b:02x}' for b in buf[pc:pc + ln])
        lines.append(f'{lab:7s}{pc:04x}  {raw:20s} {fmt(op, args, texts)}')
    lines.append('; texts')
    for t in sorted(texts):
        lines.append(f'T{t:04x}: {texts[t]}')
    for p in problems:
        lines.append(f'; PROBLEM {p}')
    return lines, ins, texts, problems


def main():
    dump = sys.argv[2] if len(sys.argv) > 2 and sys.argv[1] == '--dump' else None
    known = {}
    for s in json.load(open(STRINGS, encoding='utf-8'))['strings']:
        if s['src'].startswith('O_'):
            known.setdefault(s['src'], set()).add(int(s['off'], 16))
    tot_known = tot_hit = tot_bytes = tot_cov = 0
    for i in range(1, 22):
        name = f'O_{i:03d}'
        buf = open(os.path.join(DISK1, name), 'rb').read()
        lines, ins, texts, problems = listing(name, buf)
        n, base, *_ = parse(buf)
        cov = set(range(0, base))
        for pc, (_, _, ln) in ins.items():
            cov.update(range(pc, pc + ln))
        for t in texts:
            cov.update(range(t, buf.index(0, t) + 1))
        k = known.get(name, set())
        hit = k & set(texts)
        miss = sorted(k - set(texts))
        extra = sorted(set(texts) - k)
        tot_known += len(k)
        tot_hit += len(hit)
        tot_bytes += len(buf)
        tot_cov += len(cov)
        print(f'{name}: {len(ins):4d} ins, texts {len(texts):3d}, known strings {len(hit)}/{len(k)}, '
              f'bytes covered {len(cov)}/{len(buf)}' + (f', missed {[hex(m) for m in miss]}' if miss else '')
              + (f', new {[hex(e) for e in extra]}' if extra else '') + (f', problems {problems}' if problems else ''))
        if dump:
            os.makedirs(dump, exist_ok=True)
            with open(os.path.join(dump, name + '.txt'), 'w', encoding='utf-8') as f:
                f.write('\n'.join(lines) + '\n')
    print(f'TOTAL known strings reached {tot_hit}/{tot_known}; bytes covered {tot_cov}/{tot_bytes}')


if __name__ == '__main__':
    main()
