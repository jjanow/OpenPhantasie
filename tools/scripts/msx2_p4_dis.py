#!/usr/bin/env python3
"""Phantasie IV (MSX2) disk reader and Z80 disassembler for code segments.

Phase 09 T6.2 cross-check (03d-phantasie4-re.md 3d.7). Format notes: reverse-engineering/msx2/phantasie4/FORMAT.md.
The game disks have no FAT files; sectors 1-8 hold the game's own directory of 8-byte entries
{u16 id, u16 start sector, u32 size}. Code segments (ids 0x1000-0x100d, 0x2000/0x2001, ...) load at 0x0100.
Text uses the MSX character set (single-byte hiragana/katakana, 0xDE = dakuten).

Usage:
  msx2_p4_dis.py DSK --dir                   list the directory
  msx2_p4_dis.py DSK --strings SEGID         strings of one segment (hex id, e.g. 1001)
  msx2_p4_dis.py DSK --find TEXT [TEXT...]   find kana/ASCII text in all segments
  msx2_p4_dis.py DSK SEGID [f=ADDR,..] [i=ADDR,..]
        linear-sweep disassembly at 0x0100. f= : runtime calls followed by an inline 10-byte float constant
        (seg 0x100c: f=306f); i= : calls followed by an inline 2-byte frame offset (seg 0x100c: i=32ce;
        seg 0x1001: i=4c49,4c86). Referenced strings are shown as STR.
Disassembly needs the z80dis package (pip install z80dis). Outputs are derived from the disk image: keep them out of
the repo (scratchpad only).
"""
import re
import struct
import sys

HIRA1 = "をぁぃぅぇぉゃゅょっ　あいうえおかきくけこさしすせそ"  # 0x86..0x9f
HIRA2 = "たちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわん"  # 0xe0..0xfd
KATA = "。「」、・ヲァィゥェォャュョッーアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン゛゜"  # 0xa1..0xdf
VOICED = dict(zip("がぎぐげござじずぜぞだぢづでどばびぶべぼガギグゲゴザジズゼゾダヂヅデドバビブベボ",
                  "かきくけこさしすせそたちつてとはひふへほカキクケコサシスセソタチツテトハヒフヘホ"))


def dec_byte(b):
    if 0x20 <= b < 0x7f:
        return chr(b)
    if 0x86 <= b <= 0x9f:
        return HIRA1[b - 0x86]
    if 0xe0 <= b <= 0xfd:
        return HIRA2[b - 0xe0]
    if 0xa1 <= b <= 0xdf:
        return KATA[b - 0xa1]
    return None


def enc(s):
    out = bytearray()
    for ch in s:
        if ch in VOICED:
            out += enc(VOICED[ch]) + b'\xde'
        elif ch in HIRA1:
            out.append(0x86 + HIRA1.index(ch))
        elif ch in HIRA2:
            out.append(0xe0 + HIRA2.index(ch))
        elif ch in KATA:
            out.append(0xa1 + KATA.index(ch))
        elif ord(ch) < 0x7f:
            out.append(ord(ch))
        else:
            raise ValueError(ch)
    return bytes(out)


def directory(d):
    t = d[512:512 * 9]
    for i in range(0, len(t), 8):
        a, b, c = struct.unpack('<HHI', t[i:i + 8])
        if a or b or c:
            yield i // 8, a, b, c


def segments(d):
    segs = {}
    for _, a, b, c in directory(d):
        if c and a != 0xffff:
            segs.setdefault(a, d[b * 512:b * 512 + c])   # first entry wins (some ids are listed twice)
    return segs


def decstr(data, off):
    s = []
    while off < len(data) and data[off] != 0:
        c = dec_byte(data[off])
        if c is None:
            return None
        s.append(c)
        off += 1
    return ''.join(s) if off < len(data) else None


def strings(data, base=0x100, minlen=3):
    res, start = [], None
    for i, b in enumerate(data + b'\1'):
        if i < len(data) and dec_byte(b) is not None:
            if start is None:
                start = i
            continue
        if start is not None and b == 0 and i - start >= minlen:
            res.append((base + start, decstr(data, start)))
        start = None
    return res


def f80(b):
    mant = int.from_bytes(b[:8], 'little')
    e = int.from_bytes(b[8:10], 'little')
    if mant == 0:
        return 0.0
    return (-1 if e & 0x8000 else 1) * mant / 2 ** 63 * 2.0 ** ((e & 0x7fff) - 0x3fff)


def disasm(data, base, fcalls, icalls):
    from z80dis import z80

    def sweep(skip):
        pc, end, out = base, base + len(data), []
        while pc < end:
            if pc in skip:
                out.append((pc, skip[pc], None))
                pc += skip[pc]
                continue
            off = pc - base
            dec = z80.decode(data[off:off + 4] + b'\0' * 4, pc)
            if dec.status != z80.DECODE_STATUS.OK or dec.len == 0 or any(pc < k < pc + dec.len for k in skip):
                out.append((pc, 1, 'db 0x%02x' % data[off]))
                pc += 1
                continue
            txt = z80.disasm(data[off:off + dec.len], pc)
            out.append((pc, dec.len, txt))
            pc += dec.len
            m = re.match(r'call 0x([0-9a-f]+)$', txt.lower())
            if m and int(m.group(1), 16) in fcalls:
                out.append((pc, 10, 'DF %r' % f80(data[pc - base:pc - base + 10])))
                pc += 10
            elif m and int(m.group(1), 16) in icalls:
                out.append((pc, 2, 'DW 0x%04x' % (data[pc - base] | data[pc - base + 1] << 8)))
                pc += 2
        return out

    skip = {}
    for _ in range(4):
        ins = sweep(skip)
        new = {}
        for pc, n, t in ins:
            if t and re.match(r'ld (hl|de|bc|ix|iy),0x', t.lower()):
                a = int(re.search(r'0x([0-9a-f]+)', t.lower()).group(1), 16)
                if base <= a < base + len(data) and decstr(data, a - base):
                    new[a] = len(decstr(data, a - base)) + 1
        if new == skip:
            break
        skip = new
    for pc, n, t in ins:
        if t is None:
            print(f"{pc:04x}  STR \"{decstr(data, pc - base)}\"")
            continue
        ann = ''
        m = re.search(r'0x([0-9a-f]{3,4})\b', t.lower())
        if m and int(m.group(1), 16) in skip:
            ann = '  ; "' + decstr(data, int(m.group(1), 16) - base) + '"'
        print(f"{pc:04x}  {data[pc - base:pc - base + n].hex():10s} {t}{ann}")


def main():
    d = open(sys.argv[1], 'rb').read()
    arg = sys.argv[2]
    if arg == '--dir':
        for i, a, b, c in directory(d):
            print(f"{i:3d} id={a:04x} sector={b:4d} offset={b * 512:06x} size={c}")
    elif arg == '--strings':
        for a, s in strings(segments(d)[int(sys.argv[3], 16)]):
            print(f"{a:04x} {s}")
    elif arg == '--find':
        segs = segments(d)
        for w in sys.argv[3:]:
            b = enc(w)
            for k, v in sorted(segs.items()):
                i = v.find(b)
                while i >= 0:
                    print(w, f"seg {k:04x}", f"addr {i + 0x100:04x}")
                    i = v.find(b, i + 1)
    else:
        fc, ic = set(), set()
        for a in sys.argv[3:]:
            k, v = a.split('=')
            (fc if k == 'f' else ic).update(int(x, 16) for x in v.split(','))
        disasm(segments(d)[int(arg, 16)], 0x100, fc, ic)


if __name__ == '__main__':
    main()
