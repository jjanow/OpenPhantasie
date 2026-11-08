#!/usr/bin/env python3
"""Phase 09 T4, task 3c.5: function map of Phantasie III DOS `MAIN.EXE` (root + 4 overlays), paired with the
Amiga `PhantasieIII` by shared strings.

DOS: functions = MS C prologues (`55 8b ec`) + relocated far-call targets + `push cs; call` targets + near-call
targets that start with `push bp`; each function runs to the next start. Per function: master string-table refs
(`es:[0x1308 + 4k]`, also indexed), DGROUP literal strings pushed as immediates, message-box calls (index 389 + n),
master tables (item points/requirement/value, town price index) and the 262-B record / roster / party areas.
Amiga: functions = `link a5` + `jsr`/`bsr` targets (m68k objdump); strings = PC-relative and absolute refs to
NUL-terminated text, plus the Amiga pointer table (0x22a14, 400 entries). Pairing = most shared normalised strings.

Writes `reverse-engineering/dos/phantasie3/FUNCTIONS.md` (`--write`) and prints a summary. Hand labels live in
NAMES below (evidence in `FORMAT.md` and `logic-specs/`). Needs capstone and binutils-m68k-linux-gnu.
Run with `python3 tools/scripts/fmap_p3.py [--write]` (capstone lives in the user site, so no `-I`).
"""
from __future__ import annotations

import json
import os
import re
import struct
import subprocess
import sys
import tempfile
from bisect import bisect_right
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import capstone  # noqa: E402
from capstone import x86  # noqa: E402

import extract_dos_p3_common as C  # noqa: E402
import extract_amiga_p3_common as A  # noqa: E402

OUT_MD = C.P3 / "FUNCTIONS.md"
OVB = C.OVERLAY_SEG * 16           # overlays load at img 0x13d50
LIB = (0x3B20, 0x6F60)             # MS C runtime (_TEXT, seg 0x3b2)
FPEM = 0x118F0                     # MSEM87 float emulator starts here
STR_T = 0x1308                     # master string table (far pointers)
NSTR = 554
MSGFN = {0x8BD8, 0x929A, 0x999F, 0x9BFD}   # message helpers: string 389 + first argument
TABLES = {'item_points': (0x113C, 0x113C + 101), 'item_req': (0x11A2, 0x11A2 + 101),
          'item_value': (0xF92, 0xF92 + 2 * 181), 'town_price_idx': (0x10FC, 0x10FC + 28),
          'hit_location_odds': (0x12CC, 0x12CC + 60)}
REC = (('buf', 0x9DE8, False), ('party', 0x236, True), ('roster', 0xE820, True))
REC_FIELDS = {0x08: 'rank', 0x22: 'hp', 0x2c: 'status', 0x30: 'attack', 0x48: 'firebow', 0x68: 'shield',
              0x6a: 'armour', 0x6c: 'weapon', 0x86: 'bow', 0x8e: 'social', 0x94: 'class', 0xa8: 'race',
              0xf2: 'areas'}

md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_16)
md.detail = True

# --------------------------------------------------------------------------------------------- hand labels
# (module, addr) -> (name, note). addr = root img offset or overlay image offset.
NAMES = {
    ('root', 0x0000): ('load_town_graphics', 'dtown.cmp'),
    ('root', 0x00eb): ('read_scroll', 'SCROLLS.DAT records, Scroll IV = map picture (logic-specs/planes.md §7)'),
    ('root', 0x03e6): ('dos_error_box', 'S550'),
    ('root', 0x0802): ('draw_party_roster_line', 'S19 status, S30 rank labels, rec +0x08'),
    ('root', 0x0a0c): ('draw_party_header', 'S452'),
    ('root', 0x0f22): ('build_armory_stock', 'value · town price index / 100 (FORMAT.md §4.1)'),
    ('root', 0xf421): ('add_found_item', 'cap 60 (FORMAT.md §8, D3-020)'),
    ('root', 0xf713): ('draw_combat_status_labels', 'S450'),
    ('ovl3', 0x0088): ('leave_town', 'clears the in-town flag, sets FAR:0x487c'),
    ('ovl3', 0x06c8): ('enter_place', '"Will you enter?", sets the in-town flag'),
    ('ovl3', 0x0375): ('terrain_at_party', 'tile -> terrain name (logic-specs/planes.md §3)'),
    ('ovl3', 0x092c): ('blocked_square', '"Ocean" / "River Styx", push back'),
    ('ovl4', 0x1d30): ('dungeon_fight', 'forced / event fight, endgame outcome 2/3'),
    ('ovl4', 0x1299): ('dungeon_event_square', 'DNGS 0x2e4 entries, choices, program loop (logic-specs/dungeon-events.md §2)'),
    ('ovl4', 0x14b0): ('dungeon_event_op', 'op types 1..27, jump table 0x1aea (logic-specs/dungeon-events.md §3)'),
    ('ovl4', 0x1b9e): ('dungeon_teleport', 'party to (x, y), redraw'),
    ('ovl4', 0x1bf9): ('dungeon_text_ptr', 'DNGS text n: BE pointer at 0x593 + 2n, minus 0x800'),
    ('ovl4', 0x1c4d): ('dungeon_show_text', 'event text, Press a key'),
    ('ovl4', 0x1ed4): ('party_record_plus8', 'far ptr FAR:0x23e + 0x106*i (event field base)'),
    ('ovl4', 0x09da): ('dungeon_step', 'movement, cell dispatch (fights, doors, exits, event squares)'),
    ('root', 0x0d00): ('draw_stick_figure', 'six body areas +0xf2..+0xfc'),
    ('root', 0x107b): ('char_status_from_body', 'HP/status/body areas -> Dead/Out/Asleep/Down/Okay (logic-specs/body-areas.md)'),
    ('root', 0x18b6): ('resurrect_char', 'HP 1, MP max, CON -rnd(2), areas cleared'),
    ('root', 0x194d): ('cast_healing', 'S433; HP rnd(table 0xc1a), heal_body_areas(level)'),
    ('root', 0x1b6d): ('party_status_screen', 'S444'),
    ('root', 0x1dbb): ('clear_body_areas', 'areas := Okay'),
    ('root', 0x1e71): ('party_death_astral_plane', 'resurrect / undead L20 / destroy, random town (logic-specs/planes.md §6)'),
    ('root', 0x1e18): ('plane_from_town', 'town 15/16/17 -> plane 1/2/4 (logic-specs/planes.md §2)'),
    ('root', 0x125d): ('load_plane_map', 'WORLD.DAT / PLNS.DAT offsets (FORMAT.md §9)'),
    ('root', 0x9884): ('compute_scores', 'quest bits +0x8a, titles clamp (logic-specs/endgame.md §2)'),
    ('root', 0xd171): ('monsters_give_up_test', 'strength < start/2, rnd 50, id < 44 (logic-specs/encounters.md)'),
    ('root', 0xf460): ('monster_strength', 'sum of levels, odd-HP members (logic-specs/encounters.md §1)'),
    ('root', 0xefd8): ('make_encounter', 'rank 1 + companion rank 2'),
    ('root', 0xe3e4): ('fill_monster_group', 'group record from CREATURE.DAT'),
    ('root', 0x110f5): ('list_group_status', 'letters S/D/O/K (D3-021)'),
    ('root', 0x10dcb): ('accept_surrender', '`core.p3.monsters_surrender`'),
    ('root', 0x10e14): ('beg_for_mercy', '`core.p3.they_take_your_gold`'),
    ('root', 0x112e9): ('greet_monsters', '"Greetings..."'),
    ('root', 0x2623): ('trap_damage_report', '"Damage Report"'),
    ('root', 0x2c31): ('random_1_to_n', 'rand() % n + 1'),
    ('root', 0x7b92): ('disk_error_box', 'S552'),
    ('root', 0x7bff): ('file_io', 'read/write a data file named by string 500+n (DNG/DNP/plns/dngs)'),
    ('root', 0x7da4): ('prompt_disk_swap', 'S546/S547'),
    ('root', 0x7e89): ('load_monster_pictures', 'monsters.cga'),
    ('root', 0x7f8b): ('load_game', 'gamesave.dat (FORMAT.md §8)'),
    ('root', 0x8176): ('save_game', 'gamesave.dat (FORMAT.md §8)'),
    ('root', 0x8348): ('town_other_options', 'S389/S390/S447, redistribute'),
    ('root', 0x86f0): ('inspect_inventory_spells', "\"'s Inventory:\", \"'s Spells:\""),
    ('root', 0x8bd8): ('msg_item_list', 'message 389+n with item names'),
    ('root', 0x8e50): ('cast_spell_noncombat', 'S434/S436/S437/S484/S486'),
    ('root', 0x929a): ('msg_yes_no', 'message 389+n, Yes/No'),
    ('root', 0x93b8): ('pick_item', 'item names'),
    ('root', 0x94a5): ('use_item', 'potions: HP n^2, body heal tier (logic-specs/body-areas.md)'),
    ('root', 0x999f): ('msg_box', 'message 389+n'),
    ('root', 0x9bfd): ('msg_pick_member', 'message 389+n ("Who shall ...")'),
    ('root', 0x9ea0): ('initial_skills', 'creation: skill = table/2 + seed - 49 (logic-specs/training.md §5)'),
    ('root', 0xa1f2): ('seed_skills', 'skill seeds incl. Fire Bow (FORMAT.md §5)'),
    ('root', 0xa292): ('inspect_sheet', 'S392/S393/S456'),
    ('root', 0xaafc): ('heal_body_areas', 'cure points P (logic-specs/body-areas.md)'),
    ('root', 0xabbe): ('learn_spell_add', 'spell list +0xb0, count +0x56 (max 32)'),
    ('root', 0xac21): ('guild_menu', 'S404/S405/S408/S409/S414/S418'),
    ('root', 0xb116): ('damage_monster', '" damage!", " dies!"'),
    ('root', 0xb4ec): ('pick_monster_target', ''),
    ('root', 0xb550): ('right_arm_disabled', 'area 2 >= Broken'),
    ('root', 0xb582): ('monster_breath_or_area_attack', ''),
    ('root', 0xb64e): ('monster_melee_attack', '" is drained!", " misses", " is stunned!"'),
    ('root', 0xb934): ('party_average_rank', ''),
    ('root', 0xbbb6): ('damage_character', '" takes ", " damage! ", " dies! "'),
    ('root', 0xbf9a): ('party_casts', 'party member name + " casts " + spell name S331+n, sound 21; callers 0xce66 (s122)'),
    ('root', 0xc03b): ('character_attack', '" hits!", " misses" (logic-specs/combat-actions-and-ranks.md)'),
    ('root', 0xc3cc): ('combat_spell_5_28', 'Fireflash, Quickness, Arrow Flame, Protection, ...'),
    ('root', 0xc687): ('combat_spell_29_56', 'Binding ... Accuracy / Party Accuracy'),
    ('root', 0xcaac): ('combat_spell_dispatch', ''),
    ('root', 0xcad7): ('monster_spell', '" casts Fireflash" ...'),
    ('root', 0xd730): ('combat_char_stats', 'attack %, weapon/bow damage, bow %, defence (logic-specs/bows.md)'),
    ('root', 0xd8c7): ('combat_round', '`core.p3.monsters_flee`'),
    ('root', 0xe0a7): ('night_encounter', 'the night-encounter line'),
    ('root', 0xe6c6): ('injure_monster_area', "\"'s head \" ... (logic-specs/body-areas.md)"),
    ('root', 0xe94d): ('injury_severity', 'HP-left / damage -> Gone/Broken/Injured/none'),
    ('root', 0xe98c): ('monster_has_no_limbs', 'snakes, slimes, ...'),
    ('root', 0xe9ee): ('injure_character_area', "\"'s torso \" ... (logic-specs/body-areas.md)"),
    ('root', 0xec1a): ('monster_status_from_body', ''),
    ('root', 0xecde): ('combat_rewards', 'S468'),
    ('root', 0xf2eb): ('found_item_keep', '`core.p3.will_the_party_keep_it`'),
    ('root', 0xf794): ('combat_status_panel', 'S450, S146..S149'),
    ('root', 0xf9ca): ('combat_select_member', 'S499'),
    ('root', 0xfb5f): ('combat_change_rank', 'S0..S2 Front/Middle/Back -> rec +0x08'),
    ('root', 0xfc41): ('combat_choose_action', 'action menu checks (logic-specs/combat-actions-and-ranks.md)'),
    ('root', 0x104c0): ('combat_main', 'tall-monster flag, round loop'),
    ('root', 0x10be4): ('validate_action_body', 'Parry/Thrust forced by body areas'),
    ('root', 0x10cb8): ('area_at_least', 'rec area k >= v'),
    ('root', 0x10cf3): ('choose_target_rank', 'S483; R1 -> R2 action codes'),
    ('root', 0x10f30): ('flee', ''),
    ('root', 0x1101c): ('monster_identification', ''),
    ('ovl1', 0x0000): ('open_guild_dat', 'Guild.dat'),
    ('ovl1', 0x00a5): ('transfer_from_phantasie1', 'FORMAT.md §6'),
    ('ovl1', 0x05e0): ('startup_load_data', 'world.dat, creature.dat, initarms.dat; transfer question'),
    ('ovl2', 0x0000): ('distribute_experience', 'S480'),
    ('ovl2', 0x061f): ('disburse_or_sell', '"Player: Class:  SH AR BW WP"'),
    ('ovl2', 0x1031): ('training_level_up', '"must gain more experience points"'),
    ('ovl2', 0x16d0): ('training_skills', 'S482'),
    ('ovl2', 0x1a6c): ('learn_spells', '"can learn spell(s)"'),
    ('ovl2', 0x21de): ('rename_character', 'S413'),
    ('ovl2', 0x227d): ('create_or_admit_character', 'race/class, social class, kit (FORMAT.md §5.2)'),
    ('ovl2', 0x297a): ('bank', 'S419/S489'),
    ('ovl2', 0x2ca2): ('mystic', 'S424'),
    ('ovl2', 0x2f2a): ('armory', "\"Party's Gold:\""),
    ('ovl2', 0x347d): ('inn', '" Inn welcomes you." (logic-specs/body-areas.md)'),
    ('ovl2', 0x37f1): ('ready_best_equipment', 'worn slots +0x68/+0x6a/+0x86/+0x6c'),
    ('ovl2', 0x39b5): ('attribute_sum_for_item', 'STR / STR+DEX / 2*STR+DEX'),
    ('ovl2', 0x3a0a): ('add_member', 'S405/S410/S411/S412/S445'),
    ('ovl2', 0x3f04): ('summon_elemental_record', '"Mist", "Mud"'),
    ('ovl4', 0x0000): ('endgame_gods', 'S536..S545 (Pluto/Zeus)'),
    ('ovl4', 0x043e): ('copy_protection_quiz', 'S524..S533'),
    ('ovl4', 0x062d): ('dungeon_enter_save', '"Load previously saved dungeon", DNP/DNG files'),
    ('ovl4', 0x0d36): ('dungeon_trap', '"A trap!!!"'),
    ('ovl4', 0x0e14): ('dungeon_door_skills', 'listen, pick lock, find/disarm trap'),
}


# --------------------------------------------------------------------------------------------- DOS
def ds_literals(img: bytes) -> dict[int, str]:
    dsb = img[C.DS_IMG:C.DS_IMG + 0x6C00]
    return {m.start(): m.group()[:-1].decode('latin-1')
            for m in re.finditer(rb'[\x09\x0d\x20-\x7e]{3,}\x00', dsb)}


def patch_ovl_calls(d: bytes, sizes: dict[int, int]) -> bytes:
    b = bytearray(d)
    p = b.find(b'\xcd\x3f')
    while p >= 0:
        n, off = b[p + 2], b[p + 3] | b[p + 4] << 8
        if 1 <= n <= 4 and off < sizes[n]:
            struct.pack_into('<BHH', b, p, 0x9A, off, 0xE000 + 0x100 * n)
        p = b.find(b'\xcd\x3f', p + 2)
    return bytes(b)


def disasm(d: bytes, a: int, b: int):
    p = a
    while p < b:
        n = 0
        for i in md.disasm(d[p:b], p):
            yield i
            p = i.address + i.size
            n += 1
        if not n:
            p += 1


def dos_starts(d: bytes, lo: int, hi: int, relocs: set[int] | None) -> list[int]:
    st = set()
    p = d.find(b'\x55\x8b\xec', lo, hi)
    while p >= 0:
        st.add(p)
        p = d.find(b'\x55\x8b\xec', p + 1, hi)
    for p in range(lo, hi - 5):
        if d[p] == 0x9A and (relocs is None or p + 3 in relocs):
            o, s = struct.unpack_from('<HH', d, p + 1)
            t = s * 16 + o
            if lo <= t < hi and (relocs is not None or s == C.OVERLAY_SEG):
                st.add(t)
        if d[p] == 0xE8:
            t = p + 3 + struct.unpack_from('<h', d, p + 1)[0]
            if lo <= t < hi and (d[p - 1] == 0x0E or d[t] == 0x55):
                st.add(t)
    return sorted(st)


def dos_scan():
    data = C.MAIN_EXE.read_bytes()
    root, rel, _ = C.unpack_root(data)
    relocs = {s * 16 + o for s, o in rel}
    ovl = {n: C.overlay_image(n, data)[0] for n in range(1, 5)}
    sizes = {n: len(v) for n, v in ovl.items()}
    lit = ds_literals(root)
    strs = {}
    for k in range(NSTR):
        off, seg = struct.unpack_from('<HH', root, C.DS_IMG + STR_T + 4 * k)
        strs[k] = C.cstr(root, C.DS_IMG + off) if seg == 0x28C0 else ''
    mods = [('root', patch_ovl_calls(root[:OVB], sizes), 0, FPEM, relocs)]
    for n in range(1, 5):
        b = bytearray(root[:OVB + sizes[n]])
        b[OVB:OVB + sizes[n]] = ovl[n]
        mods.append((f'ovl{n}', patch_ovl_calls(bytes(b), sizes), OVB, OVB + sizes[n], None))
    out = []
    for module, d, lo, hi, rl in mods:
        st = dos_starts(d, lo, hi, rl) + [hi]
        for a, b in zip(st, st[1:]):
            f = dict(module=module, addr=a - lo, size=b - a, lib=module == 'root' and LIB[0] <= a < LIB[1],
                     strings={}, tables=set(), fields=set(), calls=set())
            ax, stack = None, []
            for i in disasm(d, a, b):
                if i.mnemonic == 'mov' and i.op_str.startswith('ax, ') and i.operands[1].type == x86.X86_OP_IMM:
                    ax = i.operands[1].imm & 0xFFFF
                elif i.mnemonic == 'push':
                    op = i.operands[0] if i.operands else None
                    stack.append(ax if i.op_str == 'ax' else
                                 (op.imm & 0xFFFF) if op is not None and op.type == x86.X86_OP_IMM else None)
                for op in i.operands:
                    if op.type == x86.X86_OP_MEM:
                        disp = op.mem.disp & 0xFFFF
                        indexed = bool(op.mem.base or op.mem.index)
                        if STR_T <= disp < STR_T + 4 * NSTR:
                            k = (disp - STR_T) // 4
                            f['strings'].setdefault(f'S{k}', (strs[k], 'idx' if indexed else 'tab'))
                        for nm, (tlo, thi) in TABLES.items():
                            if tlo <= disp < thi and op.mem.segment == x86.X86_REG_ES and indexed:
                                f['tables'].add(nm)
                        if op.mem.segment == x86.X86_REG_ES:
                            for nm, base, needs_index in REC:
                                o = disp - base
                                if 0 <= o < 262 and indexed == needs_index:
                                    f['fields'].add(o)
                            if disp == 0xFD1E:
                                f['tables'].add('monsters')
                    elif op.type == x86.X86_OP_IMM and i.mnemonic in ('mov', 'push'):
                        v = op.imm & 0xFFFF
                        if v >= 0x100 and v in lit and len(lit[v]) >= 4:
                            f['strings'].setdefault(f'DS:{v:#x}', (lit[v], 'lit'))
                tgt = None
                if i.mnemonic == 'lcall':
                    try:
                        sg, of = (int(x, 16) for x in i.op_str.split(','))
                        if sg >= 0xE000:
                            f['calls'].add(f'ovl{(sg - 0xE000) >> 8}:{of:#x}')
                        else:
                            tgt = sg * 16 + of
                            f['calls'].add(f'ovl{module[-1]}:{of:#x}' if sg == C.OVERLAY_SEG else f'{tgt:#x}')
                    except ValueError:
                        pass
                elif i.mnemonic == 'call' and i.bytes[0] == 0xE8:
                    tgt = i.address + 3 + struct.unpack_from('<h', i.bytes, 1)[0]
                    f['calls'].add(f'{module}:{tgt - lo:#x}' if module != 'root' else f'{tgt:#x}')
                if i.mnemonic in ('lcall', 'call'):
                    if tgt in MSGFN and stack and stack[-1] is not None and stack[-1] < 200:
                        k = 389 + stack[-1]
                        f['strings'].setdefault(f'S{k}', (strs.get(k, ''), 'msg'))
                    stack = []
            out.append(f)
    return out, strs


# --------------------------------------------------------------------------------------------- Amiga
def amiga_scan():
    img, bases = A.load_flat()
    code_end = A.hunks()[0]['alloc']
    tab0, ntab = 0x22A14, 400
    pr = set(range(0x20, 0x7F)) | {9, 10, 13}

    def sstart(v):
        if not (0 < v < len(img)) or img[v] not in pr or img[v - 1] in pr:
            return None
        e = v
        while e < len(img) and img[e] in pr:
            e += 1
        return img[v:e].decode('latin-1') if e < len(img) and img[e] == 0 and e - v >= 3 else None

    with tempfile.TemporaryDirectory() as tmp:
        fn = os.path.join(tmp, 'code.bin')
        open(fn, 'wb').write(img[:code_end])
        dis = subprocess.run(['m68k-linux-gnu-objdump', '-D', '-b', 'binary', '-m', 'm68k', fn],
                             capture_output=True, text=True, check=True).stdout
    line = re.compile(r'^\s*([0-9a-f]+):\t([0-9a-f ]+?)\s*\t(\S+)\s*(.*)$')
    ins = [(int(m.group(1), 16), m.group(3), m.group(4)) for m in map(line.match, dis.splitlines()) if m]
    tgt = re.compile(r'(?:%pc@\()?(0x[0-9a-f]+)\)?$')
    starts = set()
    for a, mn, op in ins:
        if mn.startswith('link') and '%a5' in op:
            starts.add(a)
        if mn == 'jsr' or mn.startswith('bsr'):
            m = tgt.match(op)
            if m and int(m.group(1), 16) < code_end:
                starts.add(int(m.group(1), 16))
    starts = sorted(starts) + [code_end]
    res = {s: dict(addr=s, size=e - s, strings={}) for s, e in zip(starts, starts[1:])}
    for a, mn, op in ins:
        k = bisect_right(starts, a) - 1
        if not 0 <= k < len(starts) - 1:
            continue
        f = res[starts[k]]
        for h in re.findall(r'0x[0-9a-f]+', op):
            v = int(h, 16)
            if tab0 <= v < tab0 + 4 * ntab and (v - tab0) % 4 == 0:
                p = struct.unpack_from('>I', img, v)[0]
                f['strings'].setdefault(f'T{(v - tab0) // 4}', (sstart(p) or '', 'tab'))
            elif v >= 0x100 and not mn.startswith(('jsr', 'jmp', 'b')):
                s = sstart(v)
                if s:
                    f['strings'].setdefault(f'A{v:#x}', (s, 'lit'))
    return list(res.values())


# --------------------------------------------------------------------------------------------- pairing + output
def norm(s: str) -> str:
    return re.sub(r'[^a-z0-9]', '', s.lower())


def keyset(f) -> set[str]:
    return {norm(v[0]) for v in f['strings'].values() if len(norm(v[0])) >= 4}


def pair(dos, amiga):
    am = [(a, keyset(a)) for a in amiga]
    df = Counter()
    for a, ks in am:
        df.update(ks)
    for f in dos:
        ks = keyset(f)
        f['amiga'] = None
        if not ks:
            continue
        best, score = None, 0.0
        for a, aks in am:
            sh = ks & aks
            if not sh:
                continue
            sc = sum(1.0 / df[k] for k in sh)  # rare strings weigh more
            if sc > score:
                best, score = (a['addr'], len(sh)), sc
        if best and score >= 0.5:
            f['amiga'] = best


def label(f) -> tuple[str, str]:
    key = (f['module'], f['addr'])
    if key in NAMES:
        return NAMES[key]
    texts = [v[0] for v in f['strings'].values() if len(v[0].strip()) >= 3]
    if texts:
        t = texts[0].strip().replace('\r', ' ')
        return 'str_' + re.sub(r'[^a-z0-9]+', '_', t.lower())[:28].strip('_'), ''
    return '', ''


def md_escape(s: str) -> str:
    return s.replace('|', '\\|').replace('\r', ' ')


def main():
    dos, _ = dos_scan()
    amiga = amiga_scan()
    pair(dos, amiga)
    rows, counts = [], Counter()
    for f in dos:
        fl = sorted(f['fields'])
        has = f['strings'] or f['tables'] or fl or (f['module'], f['addr']) in NAMES
        counts[(f['module'], 'lib' if f['lib'] else ('labelled' if has else 'unlabelled'))] += 1
        if not has or f['lib']:
            continue
        name, note = label(f)
        ev = []
        for k, (t, how) in list(f['strings'].items())[:4]:
            ev.append(f'{k}{"" if how == "tab" else "/" + how} "{md_escape(t.strip()[:30])}"')
        if len(f['strings']) > 4:
            ev.append(f'+{len(f["strings"]) - 4} str')
        if f['tables']:
            ev.append('tbl ' + ','.join(sorted(f['tables'])))
        if fl:
            named = [REC_FIELDS[o] for o in fl if o in REC_FIELDS]
            ev.append(f'rec {len(fl)} fields' + (f' ({",".join(named)})' if named else ''))
        if note:
            ev.append(md_escape(note))
        am = f['amiga']
        rows.append(f"| {f['module']} | `{f['addr']:#06x}` | {f['size']} | {name} | "
                    f"{'; '.join(ev)} | {f'`{am[0]:#07x}` ({am[1]})' if am else ''} |")
    paired = sum(1 for f in dos if f['amiga'])
    summ = []
    for m in ('root', 'ovl1', 'ovl2', 'ovl3', 'ovl4'):
        summ.append(f"| {m} | {counts[(m, 'labelled')]} | {counts[(m, 'unlabelled')]} | {counts[(m, 'lib')]} |")
    print('\n'.join(summ))
    print('paired with Amiga:', paired, '| Amiga functions:', len(amiga),
          '| with strings:', sum(1 for a in amiga if a['strings']))
    if '--write' in sys.argv:
        head = OUT_MD.read_text().split('\n<!-- TABLE -->\n')[0] + '\n' if OUT_MD.exists() else ''
        body = ['<!-- TABLE -->', '', '### Counts', '', '| Module | Labelled | Unlabelled | MS C runtime |',
                '|---|---|---|---|', *summ, '',
                f'Paired with an Amiga function by shared strings: **{paired}**. Amiga `PhantasieIII`: '
                f'{len(amiga)} functions, {sum(1 for a in amiga if a["strings"])} with strings.', '',
                '### Labelled functions', '',
                '| Module | Addr | Size | Name | Evidence | Amiga (shared strings) |', '|---|---|---|---|---|---|',
                *rows, '']
        OUT_MD.write_text(head + '\n'.join(body))
        print('wrote', OUT_MD)


if __name__ == '__main__':
    main()
