#!/usr/bin/env python3
"""Phantasie IV (X68000) picture animations: dump P4.X's animation tables into the X68000 graphics pack (T6.4, s109).

Spec: reverse-engineering/x68000/phantasie4/FORMAT.md 6.10 ("Animation", "Timing"). Tables in P4.X's data segment
(image addresses = file offset - 0x40):
  0x45764  frame records, 16 B each (161 used): +0 sheet x, +4 sheet y, +8 base, +12 random range (s32 BE)
  0x46174  animation records, 84 B each (45 used, `_anima` scans 48): +0 x, +4 y, +8 width, +12 height (offsets from
           the picture's origin `_adis`), +16 current position (0 in the file), +20.. frame ids ending -1
  0x470FA  `_animdoor` door table, 16 B each, index town type * 7 + building: x, y, sheet x, sheet y (40 x 80)
Which picture arms which records comes from the `_setanim` call sites (listed in SCENES below with their addresses).

Usage: x68k_p4_anim.py            print the pack section (JSON) built from P4.X
       x68k_p4_anim.py --check    compare it with src/OpenPhantasie.Data/GameData/phantasie4/graphics/x68000.json
       x68k_p4_anim.py --write    replace the pack's "picture_animations" section with it
"""
import json
import pathlib
import struct
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
P4X = ROOT / 'reverse-engineering/x68000/phantasie4/unpacked/disk1/P4.X'
PACK = ROOT / 'src/OpenPhantasie.Data/GameData/phantasie4/graphics/x68000.json'

FRAMES, NFRAMES = 0x45764, 161
RECORDS, NRECORDS = 0x46174, 45
DOORS = 0x470FA

# (pack scene, sheet picture, [x, y] of _adis from the scene's top-left, first record, last record, _setanim site)
SCENES = [
    ('title', 'title.animation', [0, 0], 27, 29, '_openning 0xF8 (origin = OPEN.PKH)'),
    ('p4.street.0', 'p4.anim.street.1', [0, 0], 0, 3, '_townmenu 0x28D8, table 0x3F82C[_towntyp]'),
    ('p4.street.1', 'p4.anim.street.2', [0, 0], 4, 7, '_townmenu 0x28D8'),
    ('p4.street.2', 'p4.anim.street.3', [0, 0], 8, 14, '_townmenu 0x28D8'),
    ('p4.guild', 'p4.anim.guild', [0, 0], 33, 35, '_guildmenu 0x47AC'),
    ('p4.inn', 'p4.anim.inn', [0, 0], 20, 23, '_inn 0x257C'),
    ('p4.bank', 'p4.anim.bank', [0, 0], 24, 26, '_bank 0x59F4'),
    ('p4.shipwright', 'p4.anim.shipwright', [0, 0], 30, 32, '_shipyard 0x5F6E'),
    ('p4.mystic', 'p4.anim.mystic', [0, 0], 15, 18, '_mystic 0x6F52 / 0x7074'),
    ('p4.mystic.reading', 'p4.anim.mystic', [0, 0], 19, 19, '_mystic 0x6C2A (after 10000 _anima calls, for 3000)'),
    ('p4.ending.street', 'p4.anim.ending.1', [0, 134], 36, 42, '_endset 0xB34C (origin = END1 at (160, 154))'),
    ('p4.ending.banquet', 'p4.anim.ending.2', [0, 0], 43, 43, '_banquet 0xAEB8'),
    ('p4.ending.cell', 'p4.anim.ending.3', [0, 0], 44, 44, '_arathamus 0xADCA'),
]

# Real length of one _anima pass, measured in XEiJ (original X68000, 10 MHz MC68000, IPL ROM 1.0, wait cycles on; s110,
# tools/scripts/xeij_anim_timing.py on GIF recordings, the X68000 mouse cursor resting off the picture) [C]. The pass is
# CPU-bound, so it differs per input loop: scenes not measured use DEFAULT_PASS_MS [~] (the building loops' value; since
# s117 every scene has its own value, so it is only the loader's fallback). s117: the Shipwright and the endings were
# reached by a redirect in a patched XEiJ (tools/scripts/xeij_trace_build.sh: _disk -> _shipyard / _ending, the ending
# flags poked), the title tune playing, and timed directly: the mean interval between _anima calls (XEIJ_PCTRACE, about
# 70 s / 6000-11000 passes each, cursor off the picture). The GIF method agrees for the endings (7.40, 5.59, 5.53) but
# read the Shipwright 5 % high (6.36 / 6.12): its short low-variance frames carry their own copy's cost.
# s118: every other scene re-timed the same direct way (45-60 s / 6700-9400 passes each), in a normal New Game (one
# Human made and taken as the party; the game's own songs: the title tune on the title, the town song from New Game on);
# the street types 1 / 2 in Pendragon with its _twtyps entry (image 0x3F70F, runtime 0x8CBEF) poked to 1 / 2 at the
# title's _disk call (the same _townmenu loop over TOWN_2 / TOWN_3). The mean, not the median: the median (5.5-5.9 ms)
# is a plain pass; the mean carries the frame copies' long passes, which is what turns real time into passes.
DEFAULT_PASS_MS = 6.1
PASS_MS = {
    'title': 5.83,              # _disk(0) New Game / Saved Game wait, 7718 passes (two boots 5.832 / 5.834; s110 GIF 5.75)
    'p4.street.0': 6.69,        # _townmenu with a party, 6739 passes: 6.685 (no party 6.55; s110 GIF 6.64 / 6.48)
    'p4.street.1': 6.38,        # the same loop over TOWN_2, a party, 9413 passes: 6.377 (no party 6.25)
    'p4.street.2': 6.80,        # the same loop over TOWN_3, a party, 8827 passes: 6.799 (no party 6.65)
    'p4.guild': 6.04,           # _guildmenu with a party, 7453 passes: 6.043 (no party 6.00; s110 GIF 6.27)
    'p4.inn': 6.06,             # _inn's message wait, 7431 passes: 6.062 (s110 GIF 6.13)
    'p4.bank': 5.95,            # _bank's menu, 7576 passes: 5.948 (s110 GIF 6.16)
    'p4.mystic': 6.12,          # _mystic's message wait (_spc_wait), 7361 passes: 6.124 (s110 GIF 6.12)
    'p4.mystic.reading': 1.359,  # the three bare-_anima loops, 16000 calls: 13.30 + 4.21 + 4.22 s (1.330 / 1.405 / 1.409
                                 # ms a call) without the 353 ms bubble draw between loops 2 and 3 (s110 GIF 1.435)
    'p4.shipwright': 6.06,      # _shipyard's yes / no offer (_noyes, 1500 GP or more), 11427 passes (s117); its
                                # message wait (_spc_wait, under 1500 GP or after buying): 5.98, 11582 passes
    'p4.ending.street': 7.39,   # _endmsg's wait (_spc_wait) over END1, records 36-42, 9376 passes (s117)
    'p4.ending.banquet': 5.64,  # _common_proc's wait over END2, record 43, 6209 passes (s117)
    'p4.ending.cell': 5.53,     # _endmsg's wait over END3, record 44, 6332 passes (s117)
}
DOOR_HOLD_MS = 790              # _animdoor: the open door's copy to the street's clearing, 44 vsyncs of 18.03 ms [C]
# s151: _tload's "Welcome!" (P4.X 0x2BB4-0x2C1E): _wcls(32) (the street window cleared to colour 1), _font(0, 2, 15, 16)
# (font 0 italic, colour 15), _wprcen(32, 156, 81, "Welcome!"), then _xunpack(TOWN_n.PKH) at the window's origin covers
# it. XEiJ (s151, New Game into Pendragon, XEIJ_PCTRACE): text drawn -> _xread done 282.5 ms -> _scst_stacf (decode) done
# 854.0 ms more -> _scst_displays copies the picture in 319.2 ms. hold_ms = read + decode (the text alone), cover_ms = the
# copy. [~]: XEiJ reads the floppy far faster than a drive (49 590 bytes in 0.28 s), so on a floppy it stays longer.
# s156: before it, _tload's "Please Wait..." (0x2AFE-0x2BAC): _clrtdraw, _tw_open (window 32 at (160, 20), 312 x 348,
# no fill; _fbox(14) round it, _whole's sunken frame), _whole(32, 4, 80, 304, 16) (2 rows / columns of colour 1 above /
# left, colour 15 below / right, outside the rect), _wprcen(32, 156, 81, "Please Wait...") (0x3FA38), then TOWN_nAN.PKH
# to page 1 (argument 0 with _town_anim only), _wcls(32). XEiJ shot (s156, New Game into Pendragon,
# parity/shots/p4-x68k-please-wait.png): the window grey (colour 14), the text font 0 (not bold or italic: its ink is 87
# pixels, font 0's), colour 0. Times (s151 XEIJ_PCTRACE): sheet read 283.4 + decode 592.4 + copy 222.6, then _wcls 274.5
# = 1373.7 ms; without the sheet (the ending's _tload(1)) only the _wcls part, 274.5 ms [~] (not timed on its own).
PLEASE_WAIT = {'at': [156, 81], 'back': 14, 'colour': 0, 'box': [4, 80, 304, 16], 'frame': [1, 15], 'hold_ms': 1374,
               'hold_no_sheet_ms': 275}
WELCOME = {'at': [156, 81], 'back': 1, 'colour': 15, 'hold_ms': 1137, 'cover_ms': 319, 'please_wait': PLEASE_WAIT}

NOTE = ("P4.X _setanim / _anima (FORMAT.md 6.10, tools/scripts/x68k_p4_anim.py) [C]: frames = the frame records 0x45764 "
        "[sheet x, sheet y, base, range]; records = the animation records 0x46174 (at = offset from the picture's origin, "
        "size, frame ids in order, wrapping); scenes = which sheet and records each picture arms (_setanim call sites). "
        "_setanim sets a record's count to (rand(range) + base) >> 2 (rand = 1..range) of the frame it shows next, and "
        "each _anima call counts it down; the pass that finds 0 copies that frame (width x height from the sheet), sets "
        "the count from the frame just shown and steps on, so a frame stays up for count + 1 calls. _anima runs "
        "once per pass of the input loops (_mgetc / _mgetc2) and in _mystic's counted loops, with no Vsync or timer, so "
        "a pass is CPU-bound and its length differs per loop: measured in XEiJ (original X68000 at 10 MHz, IPL ROM 1.0; "
        "s110: GIF recordings, tools/scripts/xeij_anim_timing.py; s117 / s118: every scene by the mean interval between "
        "_anima calls, xeij_trace_build.sh) [C] and stored as a scene's pass_ms (every scene has one; streets 1 and 2 "
        "timed in Pendragon with its town type poked); the top-level pass_ms (6.1, the building loops' value) is only a fallback for a "
        "scene without one. The pass is longer while the "
        "X68000 mouse cursor's place changes it (the street: 5.74 ms with the cursor on a door, 6.88 on the picture "
        "elsewhere) [C]; the values are with it resting off the picture. "
        "doors = _animdoor (town type x 7 + building): the open door, 40 x 80 from the street sheet, shown "
        "before a building is entered (picks 0-5 only: null entries are never read), then a busy wait of 3 x 30000 loop "
        "passes: hold_ms 790 from the door's copy to the street's clearing (XEiJ) [C]. P4.X's start-up option NTA "
        "(_town_anim = 0) turns off the town and building animations and the doors. welcome (s151) = _tload's "
        "'Welcome!' before each street load (every _town entry, a load in town and the ending's _tload(1)): the street "
        "window cleared to colour 'back', the text in font 0 italic colour 'colour' centred on 'at'; it stays while "
        "TOWN_n.PKH is read and decoded (hold_ms, XEiJ: 282.5 + 854.0 ms), then the picture's copy (cover_ms) covers it "
        "[~] (XEiJ's floppy reads are faster than a real drive's).")


def load():
    b = P4X.read_bytes()[0x40:]

    def s32(a):
        return struct.unpack('>i', b[a:a + 4])[0]

    frames = [[s32(FRAMES + 16 * f + 4 * i) for i in range(4)] for f in range(NFRAMES)]
    records = []
    for n in range(NRECORDS):
        a = RECORDS + 84 * n
        ids = []
        for i in range(16):
            v = s32(a + 20 + 4 * i)
            if v == -1:
                break
            ids.append(v)
        records.append({'at': [s32(a), s32(a + 4)], 'size': [s32(a + 8), s32(a + 12)], 'frames': ids})
    doors = []
    for t in range(3):
        row = []
        for k in range(7):
            a = DOORS + 16 * (7 * t + k)
            v = [s32(a + 4 * i) for i in range(4)]
            row.append(None if v == [0, 0, 0, 0] else v)
        doors.append(row)
    scenes = {}
    for name, sheet, at, first, last, site in SCENES:
        scenes[name] = {'sheet': sheet, 'at': at, 'records': [first, last], 'site': site}
        if name in PASS_MS:
            scenes[name]['pass_ms'] = PASS_MS[name]
    return {'_note': NOTE, 'pass_ms': DEFAULT_PASS_MS, 'frames': frames, 'records': records, 'scenes': scenes,
            'doors': {'size': [40, 80], 'busy_wait': [3, 30000], 'hold_ms': DOOR_HOLD_MS, 'by_town_type': doors},
            'welcome': WELCOME}


def render(sec):
    """The section in the pack's style: one frame / record / scene per line."""
    lines = ['{', f'    "_note": {json.dumps(sec["_note"], ensure_ascii=False)},', f'    "pass_ms": {json.dumps(sec["pass_ms"])},',
             '    "frames": [']
    lines += [f'      {json.dumps(f)}' + (',' if i < len(sec['frames']) - 1 else '') for i, f in enumerate(sec['frames'])]
    lines += ['    ],', '    "records": [']
    lines += [f'      {json.dumps(r)}' + (',' if i < len(sec['records']) - 1 else '') for i, r in enumerate(sec['records'])]
    lines += ['    ],', '    "scenes": {']
    items = list(sec['scenes'].items())
    lines += [f'      {json.dumps(k)}: {json.dumps(v)}' + (',' if i < len(items) - 1 else '') for i, (k, v) in enumerate(items)]
    d = sec['doors']
    lines += ['    },', '    "doors": {', f'      "size": {json.dumps(d["size"])},', f'      "busy_wait": {json.dumps(d["busy_wait"])},',
              f'      "hold_ms": {json.dumps(d["hold_ms"])},', '      "by_town_type": [']
    lines += [f'        {json.dumps(r)}' + (',' if i < 2 else '') for i, r in enumerate(d['by_town_type'])]
    lines += ['      ]', '    },', f'    "welcome": {json.dumps(sec["welcome"])}', '  }']
    return '\n'.join(lines)


def main():
    sec = load()
    if '--check' in sys.argv:
        pack = json.loads(PACK.read_text())
        ok = pack.get('picture_animations') == sec
        print('picture_animations matches P4.X' if ok else 'picture_animations DIFFERS from P4.X')
        sys.exit(0 if ok else 1)
    if '--write' in sys.argv:
        text = PACK.read_text()
        key = '  "picture_animations": '
        if key in text:
            start = text.index(key) + len(key)
            depth, i = 0, start
            while True:
                c = text[i]
                if c == '{':
                    depth += 1
                elif c == '}':
                    depth -= 1
                    if depth == 0:
                        break
                elif c == '"':
                    i += 1
                    while text[i] != '"':
                        i += 2 if text[i] == '\\' else 1
                i += 1
            text = text[:start] + render(sec) + text[i + 1:]
        else:
            end = text.rstrip().rindex('}')
            text = text[:end].rstrip() + ',\n' + key + render(sec) + '\n}\n'
        PACK.write_text(text)
        json.loads(text)
        print(PACK)
        return
    print(render(sec))


if __name__ == '__main__':
    main()
