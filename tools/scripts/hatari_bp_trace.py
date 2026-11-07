#!/usr/bin/env python3
"""Trace Phantasie II's (Atari ST) sound players and text boxes with Hatari debugger breakpoints (s140).

    hatari_bp_trace.py arm OUT [BASE_HEX] [+ADDR ...]   # arm the trace breakpoints in the running Hatari
    hatari_bp_trace.py parse OUT [FROM_BYTE]            # print the hits logged so far, one line each

OUT is the harness out dir (`reverse-engineering/atari-st/working/start.sh`: `hatari.fifo`, `hatari.stdout`). BASE is
PHANT.PRG's text segment in RAM (default 0x143ec, the debugger's `info basepage` with EmuTOS 1.4 + GEMDOS C:). `arm`
writes small command files to OUT/bp/ and adds non-stopping breakpoints (`b pc = <abs> :trace :file <f>`): on each hit
Hatari prints the breakpoint, then runs the file (`e VBL`, a stack dump, for text routines the string), and goes on.
`parse` reads those blocks back from hatari.stdout and prints

    VBL  kind  details (image addresses, i.e. minus BASE)

kinds: `sound n` (`0xb80e`, the table player; n and its caller), `script` (`0xb8b0`, every Dosound start, incl. `0xb7fa`'s
default script and `0xb7e6`'s click; the script and its caller), `repeat` (`0xb92a`), `line` (`0x18cc2` message line:
text and n), `box` (`0xa18c` the timed box), `dmsg` / `dask` (`0x10486` / `0x103aa`, the dungeon's member lines), `alert` (AES form_alert, opcode 52: the caller's return address), `form`
(AES form_do, 50), `alertn` / `alertt` (`0x1628(n)` / `0x1668(text)`, the beeping alert wrappers). VBL is the emulator's frame count (50 Hz on the colour monitor), so gaps read as frames.

s141: `+ADDR` (image address, hex) adds a label stop `at ADDR` (any instruction: a routine's entry, the instruction after
a call, a branch target), logged with the long at the stack top (`caller` is then only meaningful at a routine's entry).
Every hit also reads the TOS 200 Hz timer `_hz_200` (`$4ba`), printed as `t` in ms, so gaps can be read in time as well
as in VBLs (the VBL rate follows the video mode: 50 Hz PAL, 60 Hz NTSC; s141 measured 50.0 on this harness).
"""
import os
import re
import sys
import time

ROUTINES = {  # image address: (kind, stack bytes, offset of a text pointer on the stack or None)
    0xb80e: ('sound', 8, None),
    0xb8b0: ('script', 8, None),
    0xb92a: ('repeat', 12, None),
    0x18cc2: ('line', 10, 4),
    0xa18c: ('box', 8, 4),
    0x1628: ('alertn', 8, None),
    0x1668: ('alertt', 8, 4),
    0x10486: ('dmsg', 10, 6),   # dungeon "<member> <text>" message (trap / disarm lines)
    0x103aa: ('dask', 10, 6),   # dungeon "<member> <text>" question ("SPOTS A TRAP. DISARM IT ?")
}
AES = {52: 'alert', 50: 'form'}


def send(out, lines):
    for line in lines:  # one open per line, as working/cmd.sh (several lines per open hit FIFO read errors)
        with open(os.path.join(out, 'hatari.fifo'), 'w') as f:
            f.write(line + '\n')
        time.sleep(0.4)


def arm(out, base, extra=()):
    d = os.path.join(out, 'bp')
    os.makedirs(d, exist_ok=True)
    cmds = []
    for a in extra:
        p = os.path.join(d, 'at_%x.ini' % a)
        with open(p, 'w') as f:
            f.write('e VBL\nm "a7" 8\nm $4ba 4\n')
        cmds.append('hatari-debug b pc = $%x :trace :file %s' % (base + a, p))
    for a, (kind, n, txt) in ROUTINES.items():
        p = os.path.join(d, kind + '.ini')
        with open(p, 'w') as f:
            f.write('e VBL\nm "a7" %d\n' % n)
            if txt is not None:
                f.write('m "(a7+%d).l" 64\n' % txt)
            if kind == 'script':  # called from 0xb80e / 0xb7e6 / 0xb7fa, which link a6: their caller at a6 + 4
                f.write('m "a6" 8\n')
            f.write('m $4ba 4\n')
        cmds.append('hatari-debug b pc = $%x :trace :file %s' % (base + a, p))
    for op, kind in AES.items():
        p = os.path.join(d, kind + '.ini')
        with open(p, 'w') as f:
            f.write('e VBL\nm $4ba 4\n')
        cmds.append('hatari-debug b AesOpcode = %d :trace :file %s' % (op, p))
    send(out, ['hatari-debug b all'] + cmds)


HIT = re.compile(r'^\d+\. CPU breakpoint condition\(s\) matched \d+ times\.\n\t(.*?)\n', re.M)


def dump_bytes(block):
    out = []
    for m in re.finditer(r'^> m \$([0-9a-f]+) \d+\n((?:[0-9A-F]{8}: .*\n)+)', block, re.M):
        b = bytearray()
        for line in m.group(2).splitlines():
            hexpart = line[10:10 + 16 * 3]
            b += bytes(int(h, 16) for h in hexpart.split())
        out.append(bytes(b))
    return out


def parse(out, base, start=0):
    txt = open(os.path.join(out, 'hatari.stdout'), encoding='latin-1').read()[start:]
    starts = [m.start() for m in HIT.finditer(txt)] + [len(txt)]
    rows = []
    for i in range(len(starts) - 1):
        block = txt[starts[i]:starts[i + 1]]
        cond = HIT.match(block).group(1)
        vbl = re.search(r'#(\d+) \(dec\)', block)
        vbl = int(vbl.group(1)) if vbl else -1
        dumps = dump_bytes(block)
        hz = re.search(r'^000004BA: ((?:[0-9A-Fa-f]{2} ){4})', block, re.M)
        t = int(hz.group(1).replace(' ', ''), 16) * 5 if hz else -1
        dumps = [b for b, a in zip(dumps, re.findall(r'^> m (\S+)', block, re.M)) if a != '$4ba']
        st = dumps[0] if dumps else b''
        ret = int.from_bytes(st[0:4], 'big') - base if len(st) >= 4 else 0
        m = re.match(r'pc = \$([0-9a-f]+)', cond)
        if m:
            pc = int(m.group(1), 16) - base
            kind = ROUTINES.get(pc, ('at %#x' % pc,))[0]
            if kind.startswith('at '):
                det = 'top %#x' % ret
            elif kind in ('sound', 'alertn'):
                det = 'n %d' % int.from_bytes(st[4:6], 'big', signed=True)
            elif kind in ('script', 'repeat'):
                det = 'arg %#x' % (int.from_bytes(st[4:8], 'big') - base)
                if kind == 'script' and len(dumps) > 1:
                    det += ' from %#x' % (int.from_bytes(dumps[1][4:8], 'big') - base - 6)
                if kind == 'repeat':
                    det += ' x%d' % int.from_bytes(st[8:10], 'big')
            else:
                s = dumps[1] if len(dumps) > 1 else b''
                det = repr(s.split(b'\0')[0].decode('latin-1'))
                if kind == 'line':
                    det += ' n %d' % int.from_bytes(st[8:10], 'big', signed=True)
            rows.append((vbl, t, kind, det if kind.startswith('at ') else 'caller %#x %s' % (ret - 6, det)))
        else:
            m = re.match(r'AesOpcode = (\d+)', cond)
            kind = AES.get(int(m.group(1)), 'aes') if m else '?'
            rows.append((vbl, t, kind, ''))
    return rows


if __name__ == '__main__':
    cmd, out = sys.argv[1], sys.argv[2]
    if cmd == 'arm':
        rest = sys.argv[3:]
        base = int(rest.pop(0), 16) if rest and not rest[0].startswith('+') else 0x143ec
        arm(out, base, [int(a[1:], 16) for a in rest if a.startswith('+')])
    elif cmd == 'parse':
        start = int(sys.argv[3]) if len(sys.argv) > 3 else 0
        for vbl, t, kind, det in parse(out, 0x143ec, start):
            print('%7d %9d  %-6s %s' % (vbl, t, kind, det))
