"""Phase 09 T3 (s83): run Phantasie II PHANT.PRG's own Motorola FFP routines in a 68000 emulator, as the oracle for
Core's MotorolaFfp port (src/OpenPhantasie.Core/MotorolaFfp.cs) and the formulas that use it.

Needs `unicorn` (pip, in a venv). Routines (image addresses): itof 0x1dbc6, add 0x1db48, div 0x1db86, mul 0x1dcde,
ftoi 0x1dc56 (the C wrappers; args on the stack, result in d0). Usage:

  python st_p2_ffp_oracle.py [OUT]   # prints the Toss slot cases where FFP differs from exact / double; OUT = list
  (as a module, s92) train_n(divisor_ffp, sum, level, xp): guild_train's n; call(FSUB, a, b), call(POW, x, y)
  (as a module) call(fn, *args), slot(rnd, i, N, cnt)

Toss slot: k = ftoi( fmul( fdiv( fadd( itof(i), fmul(itof(rnd), 0.02) ), itof(N + 2) ), itof(L + 1) ) )   (0x77bc..0x784e)
"""
import os, sys, struct
from unicorn import Uc, UC_ARCH_M68K, UC_MODE_BIG_ENDIAN
from unicorn.m68k_const import UC_M68K_REG_D0, UC_M68K_REG_A7, UC_M68K_REG_PC, UC_CPU_M68K_M68000

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from st_prg import load_prg  # noqa: E402

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
img = load_prg(REPO + '/reverse-engineering/atari-st/phantasie2/unpacked/disk1/PHANT.PRG').image
uc = Uc(UC_ARCH_M68K, UC_MODE_BIG_ENDIAN)
try:
    uc.ctl_set_cpu_model(UC_CPU_M68K_M68000)
except Exception as e:
    print('cpu model', e, file=sys.stderr)
uc.mem_map(0, 0x40000)
uc.mem_write(0, img)
STACK = 0x3f000
HALT = 0x3fe00
uc.mem_write(HALT, b'\x4e\x71' * 4)  # nop


def call(fn, *args):
    sp = STACK
    for a in reversed(args):
        sp -= 4
        uc.mem_write(sp, struct.pack('>I', a & 0xffffffff))
    sp -= 4
    uc.mem_write(sp, struct.pack('>I', HALT))
    uc.reg_write(UC_M68K_REG_A7, sp)
    uc.emu_start(fn, HALT, count=100000)
    return uc.reg_read(UC_M68K_REG_D0) & 0xffffffff


ITOF, FADD, FDIV, FMUL, FTOI = 0x1dbc6, 0x1db48, 0x1db86, 0x1dcde, 0x1dc56
C002 = 0xA3D70A3B


def slot(rnd, i, N, cnt):
    m1 = call(FMUL, call(ITOF, rnd), C002)
    a1 = call(FADD, call(ITOF, i), m1)
    d1 = call(FDIV, a1, call(ITOF, N + 2))
    p = call(FMUL, d1, call(ITOF, cnt))
    v = call(FTOI, p)
    return v - (1 << 32) if v >= 1 << 31 else v


FSUB, POW = 0x1dd1a, 0x1dba6  # s92: fsub wrapper (0x1dd74), pow (training price)


def train_n(divisor_ffp, stat_sum, level, xp):
    """guild_train 0xcf58..0xd012 (s92): n = ftoi(itof(XP) - (F - 0.8) * 1000 / (sum / divisor + 1e-5)), F = 1.8^L by
    repeated fmul (1.0 at level 0). Negative n = "You need -n more"."""
    s = call(FADD, call(FDIV, call(ITOF, stat_sum), divisor_ffp), 0xA7C5AC30)
    f = 0xE6666641
    for _ in range(1, level):
        f = call(FMUL, f, 0xE6666641)
    if level == 0:
        f = 0x80000041
    x = call(FDIV, call(FMUL, call(FADD, f, 0xCCCCCCC0), 0xFA00004A), s)
    v = call(FTOI, call(FSUB, call(ITOF, xp), x))
    return v - (1 << 32) if v >= 1 << 31 else v


if __name__ == '__main__':
    # sanity: itof/ftoi round trip and known constants
    for x in (0, 1, 2, 3, 7, 100, 255):
        assert call(FTOI, call(ITOF, x)) == x, x
    print('itof(1)=%08x itof(100)=%08x' % (call(ITOF, 1), call(ITOF, 100)))
    from fractions import Fraction as Fr
    import math
    out = []
    tot = 0
    for N in range(1, 8):
        for i in range(0, N):
            for rnd in range(0, 101):
                for cnt in range(1, 16):
                    tot += 1
                    k = slot(rnd, i, N, cnt)
                    e = math.floor((Fr(rnd, 50) + i) / (N + 2) * cnt)
                    d = math.trunc(((rnd * 0.02 + i) / (N + 2)) * cnt)
                    if k != e or k != d:
                        out.append((N, i, rnd, cnt, k, e, d))
    print('cases', tot, 'ffp != exact or != double:', len(out))
    print('ffp != double:', sum(1 for o in out if o[4] != o[6]), ' ffp != exact:', sum(1 for o in out if o[4] != o[5]))
    with open(sys.argv[1] if len(sys.argv) > 1 else '/dev/null', 'w') as f:
        for o in out:
            f.write('%d %d %d %d %d %d %d\n' % o)
