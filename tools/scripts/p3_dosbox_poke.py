"""Write Phantasie III (DOS) memory into a DOSBox-X save state (s110, 3c.8) — the poke half of p3_dosbox_state.py.

DOSBox-X here has no debugger, but a save state is a zip whose `Memory` entry is the guest RAM, and DOSBox-X loads an
edited state (F12+L, or Capture > Load state, slot = `[dosbox] saveslot`). So: save a state (F12+S), edit it with this
tool, load it back. Set `saveremark=false` in the conf (else F12+S starts a kdialog prompt and the emulator waits on it;
also run DOSBox-X with WAYLAND_DISPLAY unset so no such dialog can reach the real desktop) and `forceloadstate=true`.
A custom `mapperfile` with only a few lines unbinds the keyboard: keep the default mapper (host key F12).

Usage: python3 -I p3_dosbox_poke.py <in.sav> <out.sav> EDIT [EDIT ...]
  far:OFF=VAL            word at FAR:OFF (hex OFF, decimal VAL)
  mdown:R:M[:AREA]       monster member M of rank R (1/2) Down: leg area (2 R Leg, default; 4 L Leg) = 2 Broken and
                         status = 3 (what monster_status_from_body, root 0xec1a, gives for a leg >= Broken)
  mout:R:M               monster member unconscious: head = 2 Broken, status = 4
  mhp:R:M=VAL            monster member HP
  mdef:R=VAL / matk:R=VAL group defence (+0x28) / attack power (+0x22)
  cstat:I=VAL            party member I status (record +0x2c; 1 Okay, 2 Asleep, 4 Down)
  catk:I=VAL             party member I Attack % (record +0x30)
Monsters flee when strength() < half the start strength (FAR:0x9816; strength counts only members with odd HP,
logic-specs/encounters.md §1/§5): poke far:9816=0 to keep a test fight from ending.
Group records (logic-specs/encounters.md §1): R1 FAR:0xe5b0, R2 FAR:0xe63a; HP +4 + 2m, status +0x18 + 2m, body
areas +0x4c + 10k + 2m (k: Head, Torso, R Leg, R Arm, L Leg, L Arm). Party records FAR:0x236 + 0x106·i.
"""
import struct
import sys
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from p3_dosbox_state import open_state  # noqa: E402

GROUP = {1: 0xe5b0, 2: 0xe63a}


def edits(args):
    for a in args:
        kind, _, rest = a.partition(':')
        if kind == 'far':
            off, val = rest.split('=')
            yield int(off, 16), int(val)
        elif kind == 'mdown':
            p = [int(x) for x in rest.split(':')]
            g, m, area = GROUP[p[0]], p[1], (p[2] if len(p) > 2 else 2)
            yield g + 0x4c + 10 * area + 2 * m, 2
            yield g + 0x18 + 2 * m, 3
        elif kind == 'mout':
            r, m = (int(x) for x in rest.split(':'))
            yield GROUP[r] + 0x4c + 2 * m, 2       # head Broken
            yield GROUP[r] + 0x18 + 2 * m, 4       # unconscious ("O" in the combat list)
        elif kind == 'mhp':
            rm, val = rest.split('=')
            r, m = (int(x) for x in rm.split(':'))
            yield GROUP[r] + 4 + 2 * m, int(val)
        elif kind in ('mdef', 'matk'):
            r, val = rest.split('=')
            yield GROUP[int(r)] + (0x28 if kind == 'mdef' else 0x22), int(val)
        elif kind in ('cstat', 'catk'):
            i, val = rest.split('=')
            yield 0x236 + 0x106 * int(i) + (0x2c if kind == 'cstat' else 0x30), int(val)
        else:
            raise SystemExit('unknown edit ' + a)


def main():
    src, dst = sys.argv[1], sys.argv[2]
    raw, _ds, far = open_state(src)
    mem = bytearray(raw)
    for off, val in edits(sys.argv[3:]):
        old = struct.unpack_from('<h', mem, far + off)[0]
        struct.pack_into('<h', mem, far + off, val)
        print('FAR:%#06x %d -> %d' % (off, old, val))
    zin = zipfile.ZipFile(src)
    with zipfile.ZipFile(dst, 'w') as zout:
        for info in zin.infolist():
            data = bytes(mem) if info.filename == 'Memory' else zin.read(info.filename)
            zout.writestr(info, data)


if __name__ == '__main__':
    main()
