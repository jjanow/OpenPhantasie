"""Read Phantasie III (DOS) state out of a DOSBox-X save state (s81, 3c.8).

The installed DOSBox-X has no debugger, so live checks read memory from save states instead: run DOSBox-X with
`[dosbox] autosave=4 1` and `-savedir <dir>`, and slot 1 is rewritten every 4 s. The `Memory` entry of the zip holds
the guest RAM; the game is found by the MS C runtime banner at DS:8, so no load segment has to be known.

Writing memory back: p3_dosbox_poke.py (s110).

Usage: python3 -I p3_dosbox_state.py <slot.sav> [roster|party|def|groups|ev|all]
(ev, s82: quest / deed bits, II score, XP shares, Divine Aid, spells, body areas, gold, XP, positions, plane.)
"""
import struct
import sys
import zipfile

SIG = b'MS Run-Time Library - Copyright (c) 1987, Microsoft Corp'


def open_state(path):
    raw = zipfile.ZipFile(path).read('Memory')
    ds = raw.find(SIG) - 8            # raw offset of DS:0 (= unpacked img 0x28c00)
    if ds < 0:
        raise SystemExit('Phantasie III not found in this save state')
    img0 = ds - 0x28c00               # raw offset of unpacked img 0
    return raw, ds, img0 + 0x18200    # FAR:0 = img 0x18200


def ev(raw, b):
    """Fields the dungeon events read and write (logic-specs/dungeon-events.md)."""
    n = w(raw, b + 0x56)
    return dict(name=raw[b:b + 8].split(b'\0')[0].decode('latin-1'), status=w(raw, b + 0x2c), hp=w(raw, b + 0x22),
                mp=(w(raw, b + 0x1e), w(raw, b + 0x1c)), STR_INT_DEX_CON_CHA=[w(raw, b + 0xa + 2 * k) for k in range(5)],
                p1score=w(raw, b + 0x5c), p2score=w(raw, b + 0x18), shares=w(raw, b + 0x54), score=w(raw, b + 0x82),
                quest=hex(w(raw, b + 0x8a)), deeds=hex(w(raw, b + 0x8c)), divine=w(raw, b + 0xf0),
                spells=[w(raw, b + 0xb0 + 2 * k) for k in range(max(0, min(n, 32)))], action=w(raw, b + 0x6e),
                body=[w(raw, b + 0xf2 + 2 * k) for k in range(6)], xp=struct.unpack_from('<I', raw, b + 0x102)[0],
                bank=struct.unpack_from('<I', raw, b + 0xfe)[0])


def w(raw, a):
    return struct.unpack_from('<h', raw, a)[0]


def rec(raw, b):
    """Selected 262-B character record fields (FORMAT.md §5)."""
    return dict(name=raw[b:b + 8].split(b'\0')[0].decode('latin-1'), rank=w(raw, b + 8),
                STR=w(raw, b + 0xa), DEX=w(raw, b + 0xe), hp=(w(raw, b + 0x22), w(raw, b + 0x20)),
                status=w(raw, b + 0x2c), attack=w(raw, b + 0x30), parry=w(raw, b + 0x34), level=w(raw, b + 0x74),
                cls=w(raw, b + 0x94), race=w(raw, b + 0xa8), social=w(raw, b + 0x8e),
                gold=struct.unpack_from('<I', raw, b + 0xfe)[0], xp=struct.unpack_from('<I', raw, b + 0x102)[0],
                shield=w(raw, b + 0x68), armour=w(raw, b + 0x6a), weapon=w(raw, b + 0x6c), bow=w(raw, b + 0x86),
                action=w(raw, b + 0x6e), body=[w(raw, b + 0xf2 + 2 * k) for k in range(6)])


def main():
    raw, ds, far = open_state(sys.argv[1])
    mode = sys.argv[2] if len(sys.argv) > 2 else 'all'
    if mode in ('roster', 'all'):
        for i in range(20):
            r = rec(raw, far + 0xe820 + 0x106 * i)
            if r['name']:
                print('roster', i, r)
    if mode in ('party', 'all'):
        print('party size', w(raw, far + 0xb614))
        for i in range(7):
            r = rec(raw, far + 0x236 + 0x106 * i)
            if r['name']:
                print('party', i, r)
    if mode in ('def', 'all'):
        # written once per fight by root 0xd730, read by monster melee root 0xb64e (0xb809)
        print('defence FAR:0x488e', [w(raw, far + 0x488e + 2 * i) for i in range(7)])
        print('item points DS:0x113c ids 0..100', list(raw[ds + 0x113c: ds + 0x113c + 101]))
    if mode in ('ev', 'all'):
        img0 = ds - 0x28c00
        print('party gold', struct.unpack_from('<I', raw, far + 0x9de)[0], 'party xp', struct.unpack_from('<I', raw, far + 0x9da)[0],
              'found', w(raw, far + 0x9d8), [w(raw, far + 0x960 + 2 * k) for k in range(min(60, max(0, w(raw, far + 0x9d8))))])
        print('wild x,y', w(raw, far + 0x4882), w(raw, far + 0x4884), 'plane 0x27f3:0xcc4', w(raw, img0 + 0x27f30 + 0xcc4),
              'loaded plane FAR:0x9e6', w(raw, far + 0x9e6), 'town', w(raw, far + 0xdd98), 'in town', w(raw, far + 0xe596),
              'dungeon x,y', w(raw, far + 0xfd0e), w(raw, far + 0xfd24), 'record letter', chr(raw[far + 0xb5b0] or 32))
        for i in range(7):
            b = far + 0x236 + 0x106 * i
            if raw[b]:
                print('party', i, ev(raw, b))
        print('round actions FAR:0x228', [w(raw, far + 0x228 + 2 * i) for i in range(7)],
              'bow damage FAR:0xb58c', [w(raw, far + 0xb58c + 2 * i) for i in range(6)],
              'bow % FAR:0xdd9c', [w(raw, far + 0xdd9c + 2 * i) for i in range(6)])
    if mode in ('groups', 'all'):
        for g in (0xe5b0, 0xe63a):    # monster group records R1 / R2
            print('group FAR:%#x' % g, 'monster id', w(raw, far + g), 'attack +0x22', w(raw, far + g + 0x22),
                  'damage +0x24', w(raw, far + g + 0x24), 'defence +0x28', w(raw, far + g + 0x28))
            if w(raw, far + g) < 0:
                continue
            # s110: per member HP +4, status +0x18 (0 dead, 1 Okay, 2 Asleep, 3 Down, 4 Out), body areas +0x4c + 10k
            # in the monster order Head, Torso, R Leg, R Arm, L Leg, L Arm (logic-specs/body-areas.md §1)
            print('  count +0x02', w(raw, far + g + 2))
            for m in range(5):
                print('  member', m, 'hp', w(raw, far + g + 4 + 2 * m), 'status', w(raw, far + g + 0x18 + 2 * m),
                      'body H T RL RA LL LA', [w(raw, far + g + 0x4c + 10 * k + 2 * m) for k in range(6)])


if __name__ == '__main__':
    main()
