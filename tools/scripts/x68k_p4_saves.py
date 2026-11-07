#!/usr/bin/env python3
"""Decode Phantasie IV (X68000) save sets and the Hall of Glory with the 3d.5 record layouts.

Phase 09 T6.2 (03d-phantasie4-re.md 3d.5). Layouts: reverse-engineering/x68000/phantasie4/FORMAT.md section 6.
Reads a save set (pmemb/guils/party/goods/fames/contr files of one slot) or FAME, prints a readable dump.

Usage:
  x68k_p4_saves.py DIR [SUFFIX]      e.g. unpacked/deleted-disk2 .3   (files named _memb.3, _uils.3, ...)
  x68k_p4_saves.py --fame FILE       the Hall of Glory (FAME or fames.N)
"""
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
NAMES = os.path.join(HERE, '..', '..', 'reverse-engineering', 'x68000', 'phantasie4', 'extracted', 'names.json')
T = json.load(open(NAMES))['tables']
RACE, CLASS, SOCIAL, ORIGIN, ITM, MAGIC, SKILL = (T[k] for k in ('race', 'class', 'social', 'origin', 'itm', 'magic', 'skill'))
REC = 190            # character record (guils, pmemb, ELMS)
FAME_REC = 28


def nm(tab, i):
    return tab[i] if 0 <= i < len(tab) else f'#{i}'


def cstr(b):
    return b.split(b'\0')[0].decode('cp932', 'replace')


def char(r):
    """One 190-byte character record -> dict (offsets: FORMAT.md 6.2)."""
    u8 = lambda o: r[o]
    u16 = lambda o: struct.unpack('>H', r[o:o + 2])[0]
    u32 = lambda o: struct.unpack('>I', r[o:o + 4])[0]
    anc = []
    for k in range(2):
        kind, idx = r[0x22 + 2 * k], r[0x23 + 2 * k]
        snap = (r[0xB4], r[0xB5], r[0xBA]) if k == 0 else (r[0xBB], r[0xBC], r[0xBD])
        anc.append(dict(kind={0: 'none', 1: 'hall', 2: 'new'}.get(kind, kind), index=idx,
                        snapshot_social_race_class=snap if kind == 2 else None))
    return dict(
        slot=u8(0x00), guild_link=u8(0x01), exp=u32(0x04), level=u8(0x08), score=u8(0x09), condition=u8(0x0A),
        name=cstr(r[0x0E:0x18]), cls=nm(CLASS, u8(0x18)), race=nm(RACE, u8(0x19)), social=nm(SOCIAL, u8(0x1A)),
        made_in_map=u8(0x1B), origin=nm(ORIGIN, u8(0x1C)), age=round(u16(0x1E) / 26, 1), age_units=u16(0x1E),
        aging_factor=u8(0x20), ancestors=anc, gold=u16(0x26), b28=u8(0x28), move_base=u8(0x29),
        b2a_2e=list(r[0x2A:0x2F]), abilities=list(r[0x2F:0x35]), spell_power=u8(0x36),
        mp=(u8(0x38), u8(0x37)), hp=(u8(0x3A), u8(0x39)),
        skills={SKILL[i]: (r[0x3C + 2 * i], r[0x3D + 2 * i]) for i in range(11)},
        pack=[nm(ITM, i) for i in r[0x52:0x5B] if i], equipped=[nm(ITM, i) if i else '-' for i in r[0x64:0x69]],
        spells_known=[MAGIC[i] for i in range(65) if r[0x73 + i] == 1],
        spells_learnable=[MAGIC[i] for i in range(65) if r[0x73 + i] >= 2],
        flier_b8=u8(0xB8))


def fame(r):
    """One 28-byte Hall of Glory record (FORMAT.md 6.3)."""
    return dict(used=r[0], name=cstr(r[1:0x0B]), race=nm(RACE, r[0x0B]), cls=nm(CLASS, r[0x0C]), level=r[0x0D],
                social=nm(SOCIAL, r[0x0E]), origin_idx=r[0x0F], stats=list(r[0x10:0x15]),
                p1_p2_p3=list(r[0x15:0x18]), descendants=struct.unpack('b', r[0x18:0x19])[0], deed=r[0x19],
                alive=r[0x1A], b1b=r[0x1B])


def dump_fame(blob):
    for i in range(len(blob) // FAME_REC):
        r = blob[i * FAME_REC:(i + 1) * FAME_REC]
        if r[0]:
            print(f'  hall[{i}]', json.dumps(fame(r), ensure_ascii=False))


def main():
    if sys.argv[1] == '--fame':
        dump_fame(open(sys.argv[2], 'rb').read())
        return
    d, sfx = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else '.0'
    f = lambda stem: open(os.path.join(d, stem + sfx), 'rb').read()
    for label, stem in (('pmemb (party members, 7 x 190)', '_memb'), ('guils (guild roster, 10 x 190)', '_uils')):
        blob = f(stem)
        print(label)
        for i in range(len(blob) // REC):
            r = blob[i * REC:(i + 1) * REC]
            if any(r):
                print(f'  [{i}]', json.dumps(char(r), ensure_ascii=False))
    p = f('_arty')
    print('party (792):', dict(map_flag=p[0], map=p[1], x=p[2], y=p[3], block=p[4], town_type=p[5], ship=list(p[6:10]),
                              members=p[0x0C], slot=p[0x0D], day=struct.unpack('>H', p[0x16:0x18])[0],
                              bank=struct.unpack('>I', p[0x18:0x1C])[0], gold=struct.unpack('>I', p[0x1C:0x20])[0],
                              pack=[(nm(ITM, p[0x20 + i]), p[0xA0 + i]) for i in range(128) if p[0x20 + i]]))
    print('contr (6):', list(f('_ontr')))
    g = f('_oods')
    print('goods (157 x {stock, town}):', [(nm(ITM, i), g[2 * i], g[2 * i + 1]) for i in range(157) if g[2 * i]])
    print('fames (Hall, 11 x 28):')
    dump_fame(f('_ames'))


if __name__ == '__main__':
    sys.exit(main())
