#!/usr/bin/env python3
"""Extract Phantasie IV (X68000) master tables from P4.X and the data files → JSON.

Phase 09 T6.2 3d.3 (docs/plan/03d-phantasie4-re.md). Static only. Field meanings are tagged in the output:
`[C]` confirmed (by a second source or by the code), `[~]` likely, `[?]` unknown. See
reverse-engineering/x68000/phantasie4/FORMAT.md.

Inputs : reverse-engineering/x68000/phantasie4/unpacked/{disk1/P4.X, disk1/FAME, disk2/DATM, disk2/ELMS}
         docs/manuals/phantasie4/phantasie4-item-catalog-v1.00-utf8.txt (cross-check of item values)
Outputs: reverse-engineering/x68000/phantasie4/extracted/{p4x-symbols,names,items,monsters,elementals,hall-initial}.json
"""
import json
import os
import re
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from x68k_x import XFile  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
P4 = os.path.join(ROOT, 'reverse-engineering', 'x68000', 'phantasie4')
UNP = os.path.join(P4, 'unpacked')
OUT = os.path.join(P4, 'extracted')
CATALOG = os.path.join(ROOT, 'docs', 'manuals', 'phantasie4', 'phantasie4-item-catalog-v1.00-utf8.txt')

# Pointer tables in P4.X's data segment: symbol -> entry count. Counts are the length of the relocated pointer run
# starting at the symbol (x68k_x relocation table), not the distance to the next symbol: `_magic`'s symbol span also
# covers the string pool that follows it.
NAME_TABLES = {
    '_plname': 7, '_origin': 7, '_towns': 22, '_inns': 7, '_dungeons': 22, '_dnames': 22, '_race': 35,
    '_class': 10, '_social': 10, '_abil': 6, '_skill': 11, '_body': 6, '_hurt': 4, '_itm': 157, '_magic': 65,
}
ITEM_REC = 6
MON_REC = 70
ELM_REC = 190
FAME_REC = 28


def write(name, obj):
    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, name)
    with open(p, 'w', encoding='utf-8') as f:
        json.dump(obj, f, ensure_ascii=False, indent=1)
        f.write('\n')
    print('wrote', os.path.relpath(p, ROOT))


def cstr(b, a, enc='cp932'):
    j = b.index(b'\0', a)
    return b[a:j].decode(enc)


def reloc_run(x, start):
    sites = {s for s, z in x.relocs if z == 4}
    n = 0
    while start + 4 * n in sites:
        n += 1
    return n


def catalog_values():
    """{item index: value column} from the item catalogue (H. hex index + DEF/ATT/No.)."""
    vals = {}
    if not os.path.exists(CATALOG):
        return vals
    for line in open(CATALOG, encoding='utf-8'):
        for m in re.finditer(r'(?:^|\s)([0-9A-F]{2})\s+(\d+)\s+([a-z][a-z0-9\'.+ ]+?)(?=\s{2,}|\s*$)', line):
            if int(m.group(1), 16) < 0x55:  # equipment only (later columns are potion numbers, not values)
                vals[int(m.group(1), 16)] = (int(m.group(2)), m.group(3).strip())
    return vals


def main():
    x = XFile(open(os.path.join(UNP, 'disk1', 'P4.X'), 'rb').read())
    im = x.image
    sym = {s['name']: s['value'] for s in x.symbols}

    write('p4x-symbols.json', dict(
        source='unpacked/disk1/P4.X', note='image address = file offset - 0x40; text from 0, data from header.data_start',
        header=x.header(), symbols=[dict(s, value=f"0x{s['value']:06x}") for s in x.symbols]))

    names = {}
    for tname, count in NAME_TABLES.items():
        a = sym[tname]
        run = reloc_run(x, a)
        if run < count:
            raise SystemExit(f'{tname}: relocated run {run} < expected {count}')
        names[tname.lstrip('_')] = [cstr(im, x.long(a + 4 * i)) for i in range(count)]
    write('names.json', dict(
        source='unpacked/disk1/P4.X data segment pointer tables (symbol names from the .X symbol table) [C]',
        note='Index = the game\'s own id. English text is as in the binary (typos kept: "Constituion", "Halbred", '
             '"Thuk\'s Hmmer", "Illustionists"). Japanese entries are translated in extracted/text/strings.json.',
        tables=names))

    cat = catalog_values()
    items = []
    base = sym['_idat']
    for i in range(NAME_TABLES['_itm']):
        pic, val, req, mod, price = struct.unpack('>BBBbH', im[base + ITEM_REC * i: base + ITEM_REC * (i + 1)])
        row = dict(id=i, hex=f'0x{i:02x}', name=names['itm'][i], b0_icon=pic, value=val,
                   b2_requirement=req, b3_move_pct=mod, price=price)
        if i in cat:
            row['catalog_value'], row['catalog_name'] = cat[i]
        items.append(row)
    mism = [r['id'] for r in items if 'catalog_value' in r and r['catalog_value'] != r['value']]
    write('items.json', dict(
        source='P4.X _itm (names) + _idat (6 B per item, big-endian) [C]',
        fields={
            'value': '[C] DEF for shields/armour, ATT for weapons/bows/throwing weapons: equals the item catalogue\'s '
                     'H.DEF / H.ATT column for every equipment item (0x01-0x54) except those in catalog_mismatch',
            'b0_icon': '[C] icon glyph, printed as character b0 + 0x20 in shop lists (_itemlist)',
            'b2_requirement': '[C] items 0x01-0x1B: b2 <= Str; 0x1C-0x3F: b2 <= 2*Str + Dex; 0x40-0x54: '
                              'b2 <= Str + Dex; >= 0x55 always usable (_cal_user)',
            'b3_move_pct': '[C] signed % combat-movement modifier (body armour): '
                           'move = trunc(m + m*b3/100 + 0.5) (_baserate)',
            'price': '[C] u16 price in gold; selling pays price/4 + 1 (_sell)',
            'layout': 'FORMAT.md section 6.5',
        },
        catalog_mismatch=mism, gen_itm_raw=im[sym['_gen_itm']:sym['_idat']].hex(' '), items=items))

    d = open(os.path.join(UNP, 'disk2', 'DATM'), 'rb').read()
    mons = []
    for i in range(len(d) // MON_REC):
        r = d[i * MON_REC:(i + 1) * MON_REC]
        words = list(struct.unpack('>25H', r[20:70]))
        mons.append(dict(id=i, name=cstr(r, 0, 'ascii'), group_max=r[0x14],
                         xp=struct.unpack('>I', r[0x20:0x24])[0],
                         hp_base=struct.unpack('>h', r[0x26:0x28])[0],
                         gold=struct.unpack('>I', r[0x2A:0x2E])[0],
                         u16be_from_20=words, raw=r[20:].hex(' ')))
    write('monsters.json', dict(
        source='unpacked/disk2/DATM: 84 records x 70 B, big-endian [C] (size, names)',
        fields={'name': '[C] @0, 20 B NUL-padded ASCII',
                'group_max': '[C] @0x14 byte: max group size (_enc_mons)',
                'xp': '[C] @0x20 u32 experience per monster (_enc_end, _mdamage)',
                'hp_base': '[C] @0x26 s16 hit-point base (_enc_mons)',
                'gold': '[C] @0x2A u32 gold per monster (_enc_mons)',
                'layout': 'all 70 bytes: FORMAT.md section 6.4',
                'u16be_from_20': '[?] the 50 bytes after the name as 25 BE words, for decoding against the code'},
        monsters=mons))

    e = open(os.path.join(UNP, 'disk2', 'ELMS'), 'rb').read()
    els = [dict(id=i, name=cstr(e[i * ELM_REC:], 14, 'ascii'), raw=e[i * ELM_REC:(i + 1) * ELM_REC].hex(' '))
           for i in range(len(e) // ELM_REC)]
    write('elementals.json', dict(
        source='unpacked/disk2/ELMS: 3 records x 190 B [C]: character records (FORMAT.md section 6.2), loaded '
               'into party slot 6 by Summon El. (_pmagic2); names at +0x0E; the combat-strategy '
               'menu lists exactly three: 土の精 earth, 霧の精 mist, 空気の精 air', elementals=els))

    f = open(os.path.join(UNP, 'disk1', 'FAME'), 'rb').read()
    hall = []
    for i in range(len(f) // FAME_REC):
        r = f[i * FAME_REC:(i + 1) * FAME_REC]
        if not any(r):
            continue
        hall.append(dict(slot=i, used=r[0], name=cstr(r, 1, 'ascii'), race=r[0x0B], cls=r[0x0C], level=r[0x0D],
                         social=r[0x0E], origin=r[0x0F], stats_str_int_dex_con_cha=list(r[0x10:0x15]),
                         p1_p2_p3_results=list(r[0x15:0x18]), descendants=struct.unpack('b', r[0x18:0x19])[0],
                         deeds_line=r[0x19], alive=r[0x1A], raw=r.hex(' ')))
    write('hall-initial.json', dict(
        source='unpacked/disk1/FAME: 11 slots x 28 B; the Hall of Glory as shipped [C] (names; 3 used)',
        fields={'name': '[C] @1, NUL-terminated (bytes after the NUL are not cleared)',
                'race_cls_level_social_origin': '[C] @0x0B..0x0F (_dsp_hinfo, _fame2buf, _mcopy)',
                'stats_str_int_dex_con_cha': '[C] @0x10..0x14. Lord Wood = 18/17/16/16/17, the exact "Human Ranger" '
                                             'ancestor of the MSX Magazine 1991-03 sample',
                'p1_p2_p3_results': '[C] @0x15..0x17: 0 not played, 1-99 played, >=100 completed',
                'descendants': '[C] @0x18 s8 (<0 shown "?")',
                'deeds_line': '[C] @0x19 (slots 0-2; imports compute it)',
                'alive': '[C] @0x1A 0 dead / 1 alive (slot 0 recomputed on display)',
                'layout': 'FORMAT.md section 6.3'},
        hall=hall))


if __name__ == '__main__':
    main()
