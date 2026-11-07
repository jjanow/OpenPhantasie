#!/usr/bin/env python3
"""CPU-level check of Phantasie IV (X68000) formulas: runs the real P4.X code with the real FLOAT2.X line-F handler.

Phase 09 T6.2 (03d 3d.6 / 3d.7). This is NOT a full-system emulator check (no IPL ROM, no Human68k, no screen): it
loads the P4.X image (relocated) and SYS/FLOAT2.X (relocated) into a unicorn 68000 CPU, points the line-F vector
(vector 11, 0x2C) at FLOAT2's own handler (FLOAT2 image +0x6A, the address its start-up installs with IOCS
`B_INTVCS`), seeds the memory a code range reads, and runs that exact instruction range. Every `$FExx` FPACK call in
the range is therefore executed by FLOAT2's real code, so the float operand order and the float->long conversions are
the original's, not a reading of them. Calls to `_rand` are either run for real (rreg seeded) or replaced by a stub
that returns a chosen value (to sweep every roll).

Checks (each prints a table):
  rand      `_rand(n)` vs the model rreg-shift-register + unsigned mod + 1 (FUNCTIONS.md)
  monhp     `_enc_mons` per-monster HP (0x20EDC-0x20F32 cap, 0x20F3A-0x2104A roll + cap) (FORMAT.md 6.4 note)
  melee     `_exittown` record +3B (0x5B3E-0x5C88) for every class, level, Str (combat.md 1)
  consts    the double constants the two ranges read
  ancestry  `_get_modifer` below avg 5.0 and the `_mdice` rolls it gives (ancestry.md 5.2)

Needs: pip install unicorn (2.x) capstone. Run with a venv python:
  x68k_p4_cpu_harness.py [rand|monhp|melee|consts|all]
"""
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from x68k_x import XFile  # noqa: E402

import unicorn  # noqa: E402
from unicorn import m68k_const as M  # noqa: E402

ROOT = os.path.join(HERE, '..', '..', 'reverse-engineering', 'x68000', 'phantasie4', 'unpacked', 'disk1')
FLT_BASE = 0x8000           # FLOAT2.X load address
P4_BASE = 0x100000          # P4.X load address (image address a -> P4_BASE + a)
STACK_TOP = 0xF00000
HALT = 0x7000               # address of a `stop`-like halt (we stop emulation when PC reaches it)
RAND_STUB = 0x7100


def load_x(path, base):
    x = XFile(open(path, 'rb').read())
    img = bytearray(x.image)
    for site, size in x.relocs:
        if size == 4:
            v = struct.unpack('>I', img[site:site + 4])[0]
            img[site:site + 4] = struct.pack('>I', (v + base) & 0xFFFFFFFF)
        else:
            v = struct.unpack('>H', img[site:site + 2])[0]
            img[site:site + 2] = struct.pack('>H', (v + base) & 0xFFFF)
    return x, bytes(img)


class Cpu:
    def __init__(self):
        self.p4, p4img = load_x(os.path.join(ROOT, 'P4.X'), P4_BASE)
        self.fl, flimg = load_x(os.path.join(ROOT, 'SYS', 'FLOAT2.X'), FLT_BASE)
        self.sym = {s['name']: s['value'] for s in self.p4.symbols}
        self.mu = unicorn.Uc(unicorn.UC_ARCH_M68K, unicorn.UC_MODE_BIG_ENDIAN)
        self.mu.ctl_set_cpu_model(M.UC_CPU_M68K_M68000)
        self.mu.mem_map(0, 0x1000000)
        self.p4img, self.flimg = p4img, flimg
        self.reset()
        self.rand_values = None
        self.mu.hook_add(unicorn.UC_HOOK_CODE, self._code)
        self.mu.hook_add(unicorn.UC_HOOK_INTR, self._intr)
        self.linef = 0

    def _intr(self, mu, intno, ud):
        """unicorn stops on a 68000 exception instead of vectoring: build the 68000 short frame (SR, PC of the
        line-F word) on the supervisor stack and enter the handler from the vector table, as the CPU would."""
        pc = mu.reg_read(M.UC_M68K_REG_PC)
        word = struct.unpack('>H', mu.mem_read(pc, 2))[0]
        if word == 0x4E73:                              # rte: unicorn hands it back too; pop the short frame
            sp = mu.reg_read(M.UC_M68K_REG_A7)
            sr, ret = struct.unpack('>HI', mu.mem_read(sp, 6))
            mu.reg_write(M.UC_M68K_REG_A7, sp + 6)
            mu.reg_write(M.UC_M68K_REG_SR, sr)
            mu.reg_write(M.UC_M68K_REG_PC, ret)
            return
        if word >> 12 != 0xF:
            raise RuntimeError(f'exception {intno} at {pc - P4_BASE:#x} (word {word:#06x})')
        self.linef += 1
        sr = mu.reg_read(M.UC_M68K_REG_SR)
        sp = mu.reg_read(M.UC_M68K_REG_A7) - 6
        mu.mem_write(sp, struct.pack('>HI', sr, pc))
        mu.reg_write(M.UC_M68K_REG_A7, sp)
        mu.reg_write(M.UC_M68K_REG_SR, (sr | 0x2000) & ~0x8000)
        mu.reg_write(M.UC_M68K_REG_PC, struct.unpack('>I', mu.mem_read(0x2C, 4))[0])

    def reset(self):
        mu = self.mu
        mu.mem_write(0, b'\0' * 0x400000)
        mu.mem_write(FLT_BASE, self.flimg)
        mu.mem_write(P4_BASE, self.p4img)
        # vector 11 (line F) -> FLOAT2's handler; FLOAT2 keeps the previous vector at +0x86 (non-$FE line-F words)
        mu.mem_write(0x2C, struct.pack('>I', FLT_BASE + 0x6A))
        mu.mem_write(FLT_BASE + 0x86, struct.pack('>I', HALT))
        mu.mem_write(HALT, b'\x4e\x71' * 8)
        mu.mem_write(RAND_STUB, b'\x4e\x75')            # rts (d0 set by the hook)

    def _code(self, mu, addr, size, ud):
        if addr == P4_BASE + self.sym['_rand'] and self.rand_values is not None:
            n = struct.unpack('>I', mu.mem_read(mu.reg_read(M.UC_M68K_REG_A7) + 4, 4))[0]
            v = self.rand_values.pop(0)
            self.rand_calls.append(n)
            mu.reg_write(M.UC_M68K_REG_D0, v)
            mu.reg_write(M.UC_M68K_REG_PC, RAND_STUB)
        elif addr == HALT:
            mu.emu_stop()

    def run(self, start, end, a6=0xE00000, sr=0x2700):
        """Run P4 image range [start, end) (image addresses) in supervisor mode with frame pointer a6."""
        mu = self.mu
        mu.reg_write(M.UC_M68K_REG_SR, sr)
        mu.reg_write(M.UC_M68K_REG_A7, STACK_TOP)
        mu.reg_write(M.UC_M68K_REG_A6, a6)
        self.rand_calls = []
        mu.emu_start(P4_BASE + start, P4_BASE + end, count=2_000_000)
        if mu.reg_read(M.UC_M68K_REG_PC) != P4_BASE + end:
            raise RuntimeError(f'stopped at {mu.reg_read(M.UC_M68K_REG_PC) - P4_BASE:#x}')

    # memory helpers (image addresses)
    def w(self, a, fmt, *v):
        self.mu.mem_write(P4_BASE + a, struct.pack('>' + fmt, *v))

    def r(self, a, fmt):
        return struct.unpack('>' + fmt, self.mu.mem_read(P4_BASE + a, struct.calcsize('>' + fmt)))

    def call_rand(self, rreg, n):
        """Call the real _rand(n) with _rreg = rreg; returns (result, new rreg)."""
        mu = self.mu
        rr = self.sym['_rreg']
        self.w(rr, 'I', rreg)
        sp = STACK_TOP - 8
        mu.mem_write(sp, struct.pack('>II', HALT, n))      # return address, argument
        mu.reg_write(M.UC_M68K_REG_SR, 0x2700)         # SR first: writing it can swap A7 between USP and SSP
        mu.reg_write(M.UC_M68K_REG_A7, sp)
        mu.emu_start(P4_BASE + self.sym['_rand'], HALT, count=200_000)
        return mu.reg_read(M.UC_M68K_REG_D0), self.r(rr, 'I')[0]


def call(c, name, args, rreg=None):
    """Call a P4.X function (C calling convention, args = bytes already big-endian) until it returns."""
    mu = c.mu
    if rreg is not None:
        c.w(c.sym['_rreg'], 'I', rreg)
    sp = STACK_TOP - 4 - len(args)
    mu.mem_write(sp, struct.pack('>I', HALT) + args)
    mu.reg_write(M.UC_M68K_REG_SR, 0x2700)
    mu.reg_write(M.UC_M68K_REG_A7, sp)
    c.rand_values = None
    mu.emu_start(P4_BASE + c.sym[name], HALT, count=5_000_000)
    return mu.reg_read(M.UC_M68K_REG_D0), mu.reg_read(M.UC_M68K_REG_D1)


def chk_ancestry(c):
    """ancestry.md 5.2: _get_modifer(avg) for avg below 5.0, then _mdice with the k it returns."""
    c.reset()
    tabl = open(os.path.join(ROOT, 'TABL'), 'rb').read()
    buf = 0x90000                                     # image address of a scratch copy of TABL
    c.w(buf, f'{len(tabl)}s', tabl)
    c.w(c.sym['_tbl'], 'I', P4_BASE + buf)
    print('_get_modifer(avg) -> k (raw d0:d1 and as a double)')
    ks = {}
    for avg in (18.0, 13.5, 5.5, 5.0, 4.5, 4.0, 3.0, 0.0, -3.0):
        d0, d1 = call(c, '_get_modifer', struct.pack('>d', avg))
        k = struct.unpack('>d', struct.pack('>II', d0, d1))[0]
        ks[avg] = (d0, d1)
        print(f'  avg {avg:5.1f}: d0:d1 = {d0:08x}:{d1:08x}  k = {k!r}')
    print('_mdice(Human, Fighter, Strength, k), 400 calls with the real _rand / _frand / _mexp: roll histogram')
    for avg in (13.5, 5.0, 4.5, 3.0):
        d0, d1 = ks[avg]
        hist = {}
        rreg = 0x2468ACE1
        for i in range(400):
            v, _ = call(c, '_mdice', struct.pack('>iiiII', 0, 3, 0, d0, d1), rreg=rreg if i == 0 else None)
            hist[v] = hist.get(v, 0) + 1
        print(f'  avg {avg:4.1f}: ' + ', '.join(f'{k}:{n}' for k, n in sorted(hist.items())))


def model_rand(rreg, n):
    for _ in range(16):
        b = ((rreg >> 30) & 1) ^ ((rreg >> 26) & 1)
        rreg = ((rreg << 1) & 0xFFFFFFFF) | b
    if n == 0:
        return 1, None
    return (rreg if rreg else 1) % n + 1, rreg


def chk_rand(c):
    import random
    rng = random.Random(4)
    bad = 0
    cases = [(rng.getrandbits(32), rng.choice([1, 2, 3, 6, 10, 75, 100, 255, 1000, 0x7FFFFFFF])) for _ in range(3000)]
    cases += [(0, 10), (1, 10), (0x80000000, 100), (0xFFFFFFFF, 7), (0x12345678, 0)]
    for rreg, n in cases:
        got, nr = c.call_rand(rreg, n)
        exp, er = model_rand(rreg, n)
        if got != exp or (er is not None and nr != er):
            bad += 1
            if bad < 10:
                print('MISMATCH', hex(rreg), n, got, exp)
    print(f'_rand: {len(cases)} cases, {bad} mismatches against the model (rreg shift register, unsigned mod, +1)')


def dbl(c, a):
    return struct.unpack('>d', bytes(c.mu.mem_read(P4_BASE + a, 8)))[0]


def chk_consts(c):
    for a in (0x441b4, 0x441bc, 0x441c4, 0x441cc, 0x400c4):
        print(f'  double @ {a:#x} = {dbl(c, a)!r}')


def monhp(c, base, roll):
    mns, grp = c.sym['_mns'], c.sym['_emgrp']
    c.reset()
    c.w(grp, 'I', 0)
    c.w(mns + 0x26, 'h', base)
    c.rand_values = None
    c.run(0x20EDC, 0x20F32, a6=0xE00000)            # cap = trunc(1.8 * base) into -0x10(a6)
    cap = struct.unpack('>i', bytes(c.mu.mem_read(0xE00000 - 0x10, 4)))[0]
    c.mu.mem_write(0xE00000 - 4, struct.pack('>i', 0))   # monster index in group = 0
    c.rand_values = [roll]
    c.run(0x20F3A, 0x2104A, a6=0xE00000)
    hp = c.r(0x7b17c, 'i')[0]
    assert c.rand_calls == [10], c.rand_calls
    return cap, hp


def chk_monhp(c):
    bases = [1, 2, 3, 5, 8, 10, 13, 20, 33, 50, 100, 250]
    print('per-monster HP = f(base, rand(10)) from the real code; columns = roll 1..10, last = cap trunc(1.8 base)')
    print('base | ' + ' '.join(f'{r:>5}' for r in range(1, 11)) + ' |   cap')
    rows = {}
    for b in bases:
        vals = []
        for roll in range(1, 11):
            cap, hp = monhp(c, b, roll)
            vals.append(hp)
        rows[b] = (vals, cap)
        print(f'{b:4d} | ' + ' '.join(f'{v:5d}' for v in vals) + f' | {cap:5d}')
    # compare with the static reading: min(trunc(b*((r-1)+0.5)+0.5), trunc(1.8 b))
    bad = 0
    for b, (vals, cap) in rows.items():
        for r, v in zip(range(1, 11), vals):
            exp = min(int(b * ((r - 1) + 0.5) + 0.5), int(1.8 * b))
            if v != exp:
                bad += 1
    print(f'static reading min(trunc(base x (roll - 1 + 0.5) + 0.5), trunc(1.8 base)): {bad} mismatches')


def melee(c, cls, level, st):
    pmb = c.sym['_pmb']
    c.mu.mem_write(0xE00000 - 8, struct.pack('>i', 0))       # member index 0
    c.w(pmb + 0x08, 'B', level)
    c.w(pmb + 0x18, 'B', cls)
    c.w(pmb + 0x2F, 'B', st)
    c.w(pmb + 0x3B, 'B', 0xEE)
    c.run(0x5B3E, 0x5C8A, a6=0xE00000)
    return c.r(pmb + 0x3B, 'B')[0]


def chk_melee(c):
    c.reset()
    bad = 0
    tab = {}
    for cls in range(8):
        for lv in range(1, 31):
            for st in range(3, 26):
                v = melee(c, cls, lv, st)
                exp = int(st // 6 + abs(cls - 3.5) * lv / 11) & 0xFF
                if v != exp:
                    bad += 1
                    if bad < 10:
                        print('MISMATCH', cls, lv, st, v, exp)
                tab[cls, lv, st] = v
    print(f'+3B over classes 0-7 x levels 1-30 x Str 3-25: {bad} mismatches against '
          'trunc(trunc(Str/6) + |class - 3.5| x level / 11)')
    names = ['Thief', 'Monk', 'Ranger', 'Fighter', 'Priest', 'Wizard', 'ShadowW', 'Gargoyle']
    print('Str 18, level:   ' + ' '.join(f'{lv:3d}' for lv in (1, 5, 10, 15, 20, 25, 30)))
    for cls in range(8):
        print(f'{names[cls]:8s}        ' + ' '.join(f'{tab[cls, lv, 18]:3d}' for lv in (1, 5, 10, 15, 20, 25, 30)))


def main():
    what = sys.argv[1] if len(sys.argv) > 1 else 'all'
    c = Cpu()
    if what in ('consts', 'all'):
        chk_consts(c)
    if what in ('rand', 'all'):
        chk_rand(c)
    if what in ('monhp', 'all'):
        chk_monhp(c)
    if what in ('melee', 'all'):
        chk_melee(c)
    if what in ('ancestry', 'all'):
        chk_ancestry(c)


if __name__ == '__main__':
    main()
