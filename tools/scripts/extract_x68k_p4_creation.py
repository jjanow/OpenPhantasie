#!/usr/bin/env python3
"""Extract Phantasie IV (X68000) character-creation tables: TABL (disk 1) and the P4.X tables used by
_mdice / _cmbattr / _combi / _rrace / _classmenu / _moverate / _get_modifer.

Phase 09 T6.2 (03d 3d.5 / 3d.6). Layout and evidence: reverse-engineering/x68000/phantasie4/FORMAT.md section 6.6,
rules: logic-specs/ancestry.md.  Output: extracted/creation-tables.json.

Usage: extract_x68k_p4_creation.py
"""
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from x68k_x import XFile  # noqa: E402

BASE = os.path.join(HERE, '..', '..', 'reverse-engineering', 'x68000', 'phantasie4')
T = json.load(open(os.path.join(BASE, 'extracted', 'names.json')))['tables']
RACE, CLASS, SKILL, MAGIC = T['race'], T['class'], T['skill'], T['magic']
PC_CLASSES = 8      # TABL rows exist for classes 0-7 only (Thief .. Gargoyle)
LEVELS = 15


def s8(b):
    return b - 256 if b > 127 else b


def main():
    tabl = open(os.path.join(BASE, 'unpacked', 'disk1', 'TABL'), 'rb').read()
    assert len(tabl) == 0x852
    x = XFile(open(os.path.join(BASE, 'unpacked', 'disk1', 'P4.X'), 'rb').read())
    img = x.image
    i32 = lambda a, n: [struct.unpack('>i', img[a + 4 * k:a + 4 * k + 4])[0] for k in range(n)]
    f64 = lambda a, n: [struct.unpack('>d', img[a + 8 * k:a + 8 * k + 8])[0] for k in range(n)]
    out = dict(
        source='TABL (disk 1, 2130 B) and P4.X image tables; tools/scripts/extract_x68k_p4_creation.py',
        note='Abilities order: Str, Int, Dex, Con, Cha. Signed bytes. Rules: logic-specs/ancestry.md.',
        tabl=dict(
            hp_growth_by_class_level={CLASS[c]: list(tabl[c * LEVELS:(c + 1) * LEVELS]) for c in range(PC_CLASSES)},
            race_ability_mod={RACE[r]: [s8(b) for b in tabl[0x78 + 5 * r:0x7d + 5 * r]] for r in range(34)},
            class_ability_mod={CLASS[c]: [s8(b) for b in tabl[0x122 + 5 * c:0x127 + 5 * c]] for c in range(PC_CLASSES)},
            b36_by_class_level={CLASS[c]: list(tabl[0x14a + c * LEVELS:0x14a + (c + 1) * LEVELS])
                                        for c in range(PC_CLASSES)},
            skill_growth_by_skill_class_level={SKILL[s]: {CLASS[c]: list(tabl[0x1c2 + (s * 8 + c) * LEVELS:
                                                                         0x1c2 + (s * 8 + c + 1) * LEVELS])
                                                          for c in range(PC_CLASSES)} for s in range(11)},
            spells_granted_by_class_level={CLASS[c]: [[MAGIC[i - 1] for i in tabl[0x6ea + (c * LEVELS + lv) * 3:
                                                                                0x6ea + (c * LEVELS + lv) * 3 + 3] if i]
                                                      for lv in range(LEVELS)] for c in range(PC_CLASSES)},
        ),
        p4x=dict(
            combi_5x5=dict(addr='0x41516', rows=[i32(0x41516 + 20 * a, 5) for a in range(5)]),
            combi_pairs={'0x65': 24, '0x70': 25, '0x97': 26, '0xa1': 28, '0xb0': 30, '0xb8': 27, '0xc4': 31,
                         '0xca': 29, '0xed': 32},
            moverate_race=dict(addr='0x41472', values=i32(0x41472, 33)),
            moverate_class=dict(addr='0x414f6', values=i32(0x414f6, 10)),
            inheritance_exponent=dict(addr='0x4157a', values=f64(0x4157a, 27),
                                      note='index i applies when avg >= 18 - 0.5*i (see _get_modifer)'),
            new_ancestor_exponent=f64(0x42b74, 1)[0],
            random_race_lists={str(k): i32(0x42a9e + 36 * k, 9) for k in range(5)},
            class_mask_by_race={RACE[r]: format(img[0x42b52 + r], '08b') for r in range(35)},
        ),
    )
    p = os.path.join(BASE, 'extracted', 'creation-tables.json')
    open(p, 'w').write(json.dumps(out, indent=1, ensure_ascii=False) + '\n')
    print('wrote', p)


if __name__ == '__main__':
    sys.exit(main())
