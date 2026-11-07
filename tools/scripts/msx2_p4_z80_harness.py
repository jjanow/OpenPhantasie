#!/usr/bin/env python3
"""Z80-level check of two Phantasie IV (MSX2) formulas: runs the real segment code from the scenario disk.

Phase 09 T6.2 (03d 3d.7), companion of x68k_p4_cpu_harness.py. Not a full MSX2 emulator: the segment is loaded at
0x0100 in a bare 64 KB Z80 (pip package `z80`), the resident rand (0xB591) and 16-bit multiply (0xB606) are stubbed,
and the float runtime the segment links is the original code.
  hp     seg 4000 0x051D-0x05E1: per-monster HP for every rand(10) roll (X68000 twin: _enc_mons)
  melee  seg 1000 0x2568-0x26A0: record +3B for classes 0-7 x levels 1-30 x Str 3-25 (X68000 twin: _exittown)
Usage: msx2_p4_z80_harness.py [hp|melee]
"""
import os
import struct
import sys

import z80

HERE = os.path.dirname(os.path.abspath(__file__))
DSK = os.path.join(HERE, '..', '..', 'reverse-engineering', 'msx2', 'original-images', 'phantasie4',
                   'Phantasie IV - The Birth of Hero (1991)(Starcraft)(JP)(Disk 1 of 2)(Scenario Disk).dsk')
D = open(DSK, 'rb').read()
SEG_4000 = D[0x7000:0x7000 + 14500]      # directory: id 4000, sector 56
SEG_1000 = D[0x3A600:0x3A600 + 17018]    # directory: id 1000, sector 467
REC = 0x77DD                             # party record 0 (seg 1000)


def machine(seg):
    m = z80.Z80Machine()
    m.set_memory_block(0, bytes(0x10000))
    m.set_memory_block(0x100, seg)
    m.set_memory_block(0xB591, b'\xc9')  # rand stub (RET; HL set by the loop)
    m.set_memory_block(0xB606, b'\xc9')  # multiply stub (RET; HL = HL * DE set by the loop)
    m.sp = 0xE000
    return m


def run(m, start, stop, roll=None):
    m.pc = start
    for a in (0xB591, 0xB606, stop):
        m.set_breakpoint(a)
    calls = []
    for _ in range(100000):
        m.ticks_to_stop = 10_000_000
        m.run()
        if m.pc == 0xB591:
            calls.append(m.hl)
            m.hl = roll
            m.step_over_breakpoint()
        elif m.pc == 0xB606:
            m.hl = (m.hl * m.de) & 0xFFFF
            m.step_over_breakpoint()
        elif m.pc == stop:
            return calls
    raise RuntimeError('stop address not reached')


def hp(base, roll):
    m = machine(SEG_4000)
    m.set_memory_block(0x8059, struct.pack('<H', 0))            # group 0
    m.set_memory_block(0x818B + 0x26, struct.pack('<h', base))  # monster record +0x26: HP base
    m.set_memory_block(0x86F2, struct.pack('<H', 1))            # one monster in the group
    calls = run(m, 0x051D, 0x05E1, roll)
    assert calls == [10], calls
    return m.hl


def melee(cls, lv, st):
    m = machine(SEG_1000)
    m.set_memory_block(0x74CD, bytes([1]))                      # party size 1
    for off, v in ((0x08, lv), (0x18, cls), (0x2F, st), (0x3B, 0xEE)):
        m.set_memory_block(REC + off, bytes([v]))
    run(m, 0x2568, 0x26A1)
    return m.memory[REC + 0x3B]


def main():
    what = sys.argv[1] if len(sys.argv) > 1 else 'hp'
    if what == 'hp':
        print('base | roll 1..10 (value before the 1.8 x base cap compare)')
        for b in (1, 2, 3, 5, 8, 10, 13, 20, 33, 50, 100, 250):
            print(f'{b:4d} | ' + ' '.join(f'{hp(b, r):5d}' for r in range(1, 11)))
    else:
        bad = 0
        for cls in range(8):
            for lv in range(1, 31):
                for st in range(3, 26):
                    if melee(cls, lv, st) != int(st // 6 + abs(cls - 3.5) * lv / 11) & 0xFF:
                        bad += 1
        print(f'+3B: {bad} mismatches of {8 * 30 * 23} against trunc(trunc(Str/6) + |class - 3.5| x level / 11)')


if __name__ == '__main__':
    main()
