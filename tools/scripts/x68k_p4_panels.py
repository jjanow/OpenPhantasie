#!/usr/bin/env python3
"""Extract Phantasie IV's (X68000) Command-panel icon menus from P4.X (s136; FORMAT.md 6.9 "Command panel").

`_mkicon(count, table, clear, title)` (P4.X image 0x61F4) opens window 34 = `_wopen(34, 496, 48, 136, 324, 1, title)`
(title "" -> 0x401D2 "Command") and lays out `count` entries of a 32-byte table: entry i at x = 38 + 24 * (i & 1)
(`eor #24`), y = 8 + 52 * i, through `_seticon(34, x, y, icon, label, key, ...)`: icon.pix record +20 (`_aicon`, 32 x 32),
hot key +24 (the button's `_setbtn` key), label = the string at +0, drawn in font 1 centred on x + 16 at y + 32 (`_fsize`
>> 1), its first character in colour 2, the rest in colour 0 (all colour 2 when it starts with a space).

This script finds every `_mkicon` call (constant count / table / title pushed just before the jsr) and writes
src/OpenPhantasie.Data/GameData/phantasie4/graphics/x68000-panels.json: the window, the layout constants and one panel per
call site, keyed by the calling function (a second call in the same function gets ".2"; `_townmenu`'s are named by
their branch). Labels / titles are P4.X string ids (`p4x.0x<addr>`, the text tables' keys) with the Shift-JIS text
beside them (`text`), which the Client shows when the string tables lack the id. Usage: x68k_p4_panels.py
"""
from __future__ import annotations

import json
import re
import struct
import subprocess
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
P4X = REPO / "reverse-engineering/x68000/phantasie4/unpacked/disk1/P4.X"
SYMS = REPO / "reverse-engineering/x68000/phantasie4/extracted/p4x-symbols.json"
OUT = REPO / "src/OpenPhantasie.Data/GameData/phantasie4/graphics/x68000-panels.json"
HU_HEADER = 0x40
MKICON = 0x61F4
DEFAULT_TITLE = 0x401D2
# _townmenu (0x28E6): _par+0C (party size) = 0 -> the System icon alone (0x2818), else the six (0x2944).
NAMES = {0x2818: "_townmenu.no_party", 0x2944: "_townmenu"}
SETICON = 0x2FB04
# s140: the profile's and Party info's icon rows (not _mkicon): `_seticon(36, x0 + dx * i, 2, ...)` over a 32-byte table in
# window 36 = `_wopen68(36, _cvx(160) + 314, _cvy(64) + 270, 150, 48, 0, "")` (untitled, inside the sheet's window 35).
# name: (jsr _seticon site, table, count, x0, dx). Each site is checked to push the table address before the jsr.
ROWS = {
    "_detailmenu": (0x8E6E, 0x40A0A, 2, 32, 52),   # profile page 1: SPACE, N:次頁へ
    "_p_info3menu": (0x815A, 0x4061C, 2, 32, 52),  # page 2 (_page2): SPACE, N:次頁へ
    "_page3": (0x11E66, 0x42A1E, 2, 32, 52),       # page 3: SPACE, N:前頁へ
    "_p_info1menu": (0x163FC, 0x43312, 1, 64, 0),  # Party info: SPACE
}
# s141: window 36's place in window 35 (`_wopen68` stores x / y as given, `_wopen` runs them through _cvx / _cvy, so the
# offsets 314 / 270 are pixels of the sheet's area, 472 x 338 = `_wopen(35, 160, 64, 472, 338, 1, title)` at 0x8EE2).
ROW_WINDOW = {"x": 314, "y": 270, "width": 150, "height": 48}
ROW_SHEET = {"width": 472, "height": 338}
ROW_Y = 2
# _noyes(win, x, y) (0x30E7C), its only call _shipyard's _noyes(window, 104, 300) at 0x6066: `_whole(win, x - 8, y - 8,
# 120, 32)` (a 2-pixel sunken frame: colour 1 top / left, colour 15 bottom / right), two `_wfbox` colour-4 boxes 48 x 16
# at (x, y) and (x + 56, y), "YES" / "NO" by `_wprcen` centred at x + 24 / x + 80 in font 0 (`_font(16, 0, 0, 0)`), buttons
# `_setbtn(12, win, x, y, 48, 16, 'Y', ...)` / `_setbtn(13, ..., 'N', ...)` and 14 = the whole screen (ESC; clicks there
# are ignored). Button 12 (Y or a click on YES) returns 1, anything else 0; then `_wfbox(win, 14, x - 10, y - 10, 124, 36)`.
NOYES_SITE, NOYES_YES, NOYES_NO = 0x6066, 0x4733A, 0x4733E
# s151: the lists P4.X draws as text buttons in window 34 (FORMAT.md 6.9 "Lists in window 34"). Each row i: its key
# character ('A' + i by _wputc, or the digit i + 1 by _wdouti(34, 1, n)) at (key_x, top + step * i), the text at
# (text_x, same y) by _wlocpr, a _setbtn(-1, 34, button_x, y, button_width, button_height, key, 0x40, 2) over the row; font
# 0, colour 0 (_mkicon's _fgcol 0 / _bgcol 16, or _font(0, -1, 0, 16)). The loop's y arithmetic (asll #4 + top) is read
# from the disassembly by hand; every number listed in `check` must occur as an immediate in that function (asserted).
# window None = window 34 as the preceding _mkicon left it (_wcls clears the area to colour 14 and keeps the title bar);
# panel = the _mkicon panel drawn in the same window first (Transport's SPACE icon).
LISTS = {
    # _getmagic(member, place) 0x2BA96: _wopen(34, 496, 32, 136, 360, 1, "Spell List"), _wbox(34, 1, 0, 96, 136, 3) (an
    # outline: a groove under the head), _font(0, -1, 0, 16). _splist 0x2B86E: _wfbox(34, 14, 0, 104, 136, 360 - 104), then
    # for every spell of the page the letter at (4, 104 + 16 i); the name at x 18 and the button only when the spell is
    # castable (place 8: state 2). _spelltop 0x2B718, places 1-7: "SPACE:やめる" (0x44C42) at (8, 8), button key ' '.
    "_getmagic": {"fn": 0x2BA96, "check": {0x2BA96: [496, 32, 136, 360, 96, 3], 0x2B86E: [104, 4, 18, 136, 15, 65],
                                          0x2B718: [8, 136, 15, 32]},
                  "window": {"x": 496, "y": 32, "width": 136, "height": 360}, "title": 0x44C4F,
                  "rule": {"x": 0, "y": 96, "width": 136, "height": 3, "colour": 1},
                  "clear": {"x": 0, "y": 104, "width": 136, "height": 256, "colour": 14},
                  "head": [{"x": 8, "y": 8, "key": "Space", "label": 0x44C42}],
                  "rows": {"top": 104, "step": 16, "key_x": 4, "text_x": 18, "keys": "letter"}},
    # _getmagic(member, 8) from _mage (the Magic Training Hall): _spelltop's six page buttons "1 :第１頁" … "6 :やめる"
    # (pointers 0x44A98 + 4 * (6 + i)) at (8, 16 i), keys '1' + i; the rows as above.
    "_getmagic.8": {"fn": 0x2BA96, "check": {0x2B718: [8, 136, 15, 49]},
                    "window": {"x": 496, "y": 32, "width": 136, "height": 360}, "title": 0x44C4F,
                    "rule": {"x": 0, "y": 96, "width": 136, "height": 3, "colour": 1},
                    "clear": {"x": 0, "y": 104, "width": 136, "height": 256, "colour": 14},
                    "head_table": (0x44A98 + 4 * 6, 6, 8, 0, 16),
                    "rows": {"top": 104, "step": 16, "key_x": 4, "text_x": 18, "keys": "letter"}},
    # s152: _getmagic(member, 0) from the fight's order step: _spelltop's six category lines "1 :非攻撃系" … "6 :やめる"
    # (pointers 0x44A98 + 4 * i) at (8, 16 i), keys '1' + i (1-5 turn the page, 6 quits); the rows as above.
    "_getmagic.0": {"fn": 0x2BA96, "check": {0x2B718: [8, 136, 15, 49], 0x2BA96: [496, 32, 136, 360, 96, 3, 100]},
                    "window": {"x": 496, "y": 32, "width": 136, "height": 360}, "title": 0x44C4F,
                    "rule": {"x": 0, "y": 96, "width": 136, "height": 3, "colour": 1},
                    "clear": {"x": 0, "y": 104, "width": 136, "height": 256, "colour": 14},
                    "head_table": (0x44A98, 6, 8, 0, 16),
                    "rows": {"top": 104, "step": 16, "key_x": 4, "text_x": 18, "keys": "letter"}},
    # _cast_47 0x2D0C6: _mkicon(1, 0x44E32, 1, "Transport"), then town i: 'A' + i at (4, 60 + 16 i), the name at x 16,
    # _setbtn(-1, 34, 0, y, 136, 15, 'A' + i).
    "_cast_47": {"fn": 0x2D0C6, "check": {0x2D0C6: [60, 4, 16, 136, 15, 65]}, "panel": "_cast_47",
                 "rows": {"top": 60, "step": 16, "key_x": 4, "text_x": 16, "keys": "letter"}},
    # _racemenu 0x12838: _wcls(34); row i (0-6): _wdouti(34, 1, i + 1) at (4, 16 + 16 i), the race (five) / "Random
    # Creature" / "戻る" at x 16, _setbtn(-1, 34, 0, y, 135, 15, '1' + i).
    "_racemenu": {"fn": 0x12838, "check": {0x12838: [16, 4, 135, 15, 49, 7]},
                  "rows": {"top": 16, "step": 16, "key_x": 4, "text_x": 16, "keys": "digit"}},
    # _rracemenu 0x125C8: row n = 1.. (y = 16 n), the digit n, the race at x 16, _setbtn(…, 0, y, 135, 15, '0' + n).
    "_rracemenu": {"fn": 0x125C8, "check": {0x125C8: [4, 16, 135, 15, 48]},
                   "rows": {"top": 16, "step": 16, "key_x": 4, "text_x": 16, "keys": "digit"}},
    # _classmenu 0x1238A: the race's classes (mask 0x42B52), numbered from 1 at (4, 16 + 16 i), name x 16, width 135.
    "_classmenu": {"fn": 0x1238A, "check": {0x1238A: [16, 4, 135, 15, 49]},
                   "rows": {"top": 16, "step": 16, "key_x": 4, "text_x": 16, "keys": "digit"}},
    # _set_fame_lst 0x638C: _wcls(34), "#    Name     Race" (0x401DA) by _wprcen centred on x 68 at y 8; each used Hall slot
    # (row n): 'A' + n at (4, 24 + 16 n), the name at x 18, the race's first three characters at x 96 (_wputc × 3),
    # _setbtn(-1, 34, 0, y, 135, 15, 'A' + n).
    "_set_fame_lst": {"fn": 0x638C, "check": {0x638C: [68, 8, 24, 4, 18, 96, 135, 15, 65, 3]},
                      "centred": [{"x": 68, "y": 8, "label": 0x401DA}],
                      "rows": {"top": 24, "step": 16, "key_x": 4, "text_x": 18, "extra_x": 96, "extra_chars": 3,
                               "keys": "letter"}},
}
LIST_BUTTON = {"button_x": 0, "button_height": 15}
LIST_WIDTHS = {"_racemenu": 135, "_rracemenu": 135, "_classmenu": 135, "_set_fame_lst": 135}
# s151: the smaller pieces (FORMAT.md 6.9 "Dungeon waits, typed prompts, the keypad, the shipwright's window").
# _wttl (0x33520, from _wactive): _wfopt = 0, then (_wactive) _fgcol 0, _bgcol 16, the title by _wprcen at (width >> 1, -18)
# in the current font (font 0 everywhere: _seticon puts font 0 back; pixel-equal to the s135 XEiJ "Command").
WTTL_PRCEN, WTTL_Y = 0x3359E, -18
# _ewait (0x187BC): _wbox(msg, 0, 0, 80, 304, 1) (a black line), _wprcen(msg, 152, 82, 0x43796 "ｽﾍﾟｰｽ"),
# _setbtn(13, msg, 0, 81, 304, 15, ' ', 64, 1) (a plain button, type 64), button 14 = the whole screen (ESC, ignored);
# _iaflag = 3, _sdbeep, _mgetc until button 13 (Space or a click on the bar). msg = 0x7F122, the location map's window
# (_wopen(-1, 160, 40, 304, 320, 1, map name)), whose top 96 rows _eprint fills white (_wfbox(msg, 15, 0, 0, 304, 96)).
EWAIT_WBOX, EWAIT_PRCEN, EWAIT_SETBTN, EWAIT_LABEL = 0x187DC, 0x187FE, 0x1883A, 0x43796
EPRINT_WFBOX = 0x1876A
# INPUTS / INPUTN (_exe_ev 0x196A2 / 0x19768, events.md 4.1): _wfbox(msg, 14, 0, 80, 304, 16), the prompt by _wlocpr at
# (16, 80), _sdbeep, the reader.
TYPED_LETTERS, TYPED_NUMBER = 0x437D8, 0x437DF
# _numin(text_win, pad_win, x, y, digits) (0x309E4), its only call _withdraw's _numin(bank, 34, 40, 224, 5) at 0x5538:
# _whole(pad, x - 8, y - 8, 72, 92); _font(0, 0, 0, 4) (colour 0 on colour-4 cells); key k of the 12-byte table 0x472AA
# {char, key, button} at (x + 20 * (k % 3), y + 20 * (k / 3)): " c " by _wputc, _setbtn(button, pad, x', y', 16, 16, key,
# 64, 1); buttons 12 / 13 (keys 29 / 127, Backspace's other keys) on the B key's square, 14 = the screen (ESC); the
# cursor _fbox(0, cx, cy, 8, _ylen) at the text window's cursor, black, steady (no blink); digits 0-9 add (up to
# `digits`), buttons 11-13 erase one, 10 (R, Return) ends; then _wfbox(pad, 14, x - 10, y - 10, 76, 96).
NUMIN_SITE, NUMIN_TABLE = 0x5538, 0x472AA
# _shipyard (0x5E4E) in its window _wopen(-1, 160, 32, 312, 352, 1, 0x400D4): _font(0, 0, 0, 16); SHIP.PKH (312 x 220) at
# the window's origin; _disppcg(win) = _wlocpr(win, 68, 336, 0x3FE66) + _wdouti(win, 8, gold) + " GP" (0x3FE76; _wdout pads
# each leading zero with two spaces, one digit's width); _wlocpr(win, 16, 236, 0x400F4); under 1500 GP "」" + _spc_wait;
# else "｡ " (0x4010D); with a ship already "船を買うと" (0x40110) then _wlocpr(32, 252, 0x4011B) and _wloc(32, 268); then
# "買いますか?」" (0x40139) at the cursor; _noyes(win, 104, 300); then _wprcen(win, 156, 300, 0x40147 yes / 0x40160 no)
# and _spc_wait.
SHIP_DISPPCG, SHIP_OFFER, SHIP_LINE2, SHIP_LINE3, SHIP_YES, SHIP_NO = 0x4952, 0x5FBC, 0x6020, 0x603A, 0x6092, 0x5E66
# s151: _diskmenu(mode) (0x1AB4), the title's New Game / Saved Game panel (_disk(0) from _main) and the System menu's
# Save (_disk(1)): `_clrret(5, 0x3F5C8)`, then `_mkicon(1, 0x3F5C8 + 32 * mode, 1, 0x3F6BA "Select File")` at 0x1C58 (mode
# 0: entry 0 "N:New Game"; mode 1: entry 1 " SPACE"), then for slot k = 1..3 entry k + 1 ("*k:Save", icon 1, key 'k'):
# mode 0 adds it only when `fames.k` exists (`_xexfile`), at the next zigzag place (x 38 / 62, y 60 + 52 j, j = the
# saves shown before it; = icon index j + 1 of the panel layout); mode 1 adds all three and writes '*' (saved) or ' '
# over the label's first byte, then `_wkprcen(34, 68, 308, 0x3F6C6 "＊はセーブ済み")`. `_disk` takes one `_mgetc`: New
# Game -> `_load(0)` (the shipped start), slot k -> `_load(k)` (mode 1: `_save(k)`), the SPACE button -> back.
DISK_SITE, DISK_TABLE, DISK_TITLE = 0x1C58, 0x3F5C8, 0x3F6BA
ACGAMEND_SITE, ACGAMEND_TABLE, ACGAMEND_TITLE = 0xEA0, 0x3F362, 0x3F4C1
# s152: _sklup (0xC0EE) from _calpow, the level-up's three skill picks. _sklupmenu (0xBF48): `_wopen(32, 160, 136, 312,
# 248, 0, "")` (flag 0: no fill, no frame; it sits in _trang's titled window 32 = _wopen(32, 160, 32, 312, 352, 1, ...) at
# 0x45F6, whose grey 14 shows through), `_wfbox(32, 0, 0, -20, 312, 20)` (a black bar over its top), `_font(-1, -1, 15,
# 0)`, `_wkprcen(32, 156, -16, 0x4168B)`, `_font(-1, -1, -2, -2)` (back to the ink before: _trang's `_font(0, -1, 2, -1)` at
# 0x442A comes before _confirm, whose _mkicon clears _fgcol, so 0: black, s158 XEiJ), `_wlocpr(32, 16, 20, 0x41698)`, `_whole(32, 4, 36, 304,
# 176)`, then row i = 0..10: _sklpr (0xBDF2) `_wfbox(32, 14, 4, 36 + 16 i, 304, 16)`, 'A' + i at (16, y) (_wputc), the
# skill's name (0x3DC7C table) at x 40, `_wdouti(32, 3, now)` at x 112, `_wdouti(32, 3, _calsk(...))` at x 192; and
# `_setbtn(-1, 32, 4, y, 304, 16, 'A' + i, 64, 2)`. _sklup: `_font(-1, -1, -1, 14)` (grey cell boxes), three times:
# `_wlocpr(32, 16, 2, 0x416B6)` + `_wdouti(1, 3 - k)` + 0x416C2, _mgetc / _bvs, the skill raised, its row redrawn,
# `_wfbox(32, 14, 0, 224, 312, 16)`, `_wlocpr(32, 16, 224, name)` + 0x416DC, `_wfbox(32, 14, 0, 2, 312, 14)` (the prompt
# cleared); at the end `_wfbox(32, 14, 0, -20, 312, 20)` (the bar grey), `_font(-1, -1, 2, -1)` and `_wkprcen(32, 156,
# -16, 0x416F3)`. _trang then goes on to its member pick (_mkicon, _setwho, _mgetc) with the window still drawn.
SKL_WOPEN, SKL_BAR, SKL_TITLE, SKL_HEADER, SKL_WHOLE = 0xBFFE, 0xC024, 0xC064, 0xC0A8, 0xC0D0
SKL_ROW_CLEAR, SKL_NAME, SKL_NOW, SKL_AFTER, SKL_SETBTN = 0xBE28, 0xBE84, 0xBEB6, 0xBF10, 0xBFA4
SKL_PROMPT, SKL_TRAINED, SKL_PROMPT_CLEAR, SKL_END_BAR, SKL_END = 0xC10A, 0xC1F4, 0xC232, 0xC26E, 0xC2B2
SKL_TRANG_WOPEN, SKL_TRANG_INK, SKL_TITLE_TEXT, SKL_HEADER_TEXT, SKL_END_TEXT = 0x45F6, 0x443E, 0x4168B, 0x41698, 0x416F3
# s156: _trang (0x3C30) itself. At each member pick (0x45F6) `_wopen(32, 160, 32, 312, 352, 1, "訓練場")`, then the name
# `_wlocpr(32, 16, 16, record + 0x16)` and one of: poor condition (record + 0x12 != 1) `_wprint("は体調が悪いので")` +
# `_wlocpr(32, 32, 32, "訓練は受けられない。")`; level 125 on `_wprint(<the nothing-more-to-teach line>)`; short of
# experience `_wprint("は次のレベルまでに")`, `_wloc(32, 16, 32)`, `_font(1, -1, 11, -1)`, `_wdoutl(32, 137, need)`,
# `_font(0, -1, -2, -1)`, `_wprint(" の経験値が必要だ。")`; else the cost: `_wprint("は訓練の費用として")`, `_wloc(32, 16,
# 32)`, `_font(1, -1, 8, -1)`, `_wdoutl(32, 137, cost)`, `_font(0, -1, -2, -1)`, `_wprint(" ゴールドを支払ってもらうが")`,
# `_wlocpr(32, 16, 48, "よろしいか？")`, `_font(0, -1, 2, -1)`, `_confirm`; YES short of gold `_wprint("　ゴールドが足りない。")`
# at window 32's cursor (_confirm draws in window 34). The ink at the name: colour 0 (_mkicon clears _fgcol before each
# member pick; _guild's `_font(0, 0, 0, 16)` on entry).
TRG_NAME, TRG_NEED, TRG_NEED_LOC, TRG_NEED_FONT, TRG_NEED_NUM, TRG_NEED_AFTER = 0x4652, 0x4228, 0x4242, 0x4264, 0x427E, 0x42B2
TRG_COST, TRG_COST_LOC, TRG_COST_FONT, TRG_COST_NUM, TRG_COST_AFTER = 0x4376, 0x4390, 0x43B2, 0x43CC, 0x4400
TRG_QUESTION, TRG_SHORT, TRG_POOR, TRG_POOR_2, TRG_NOTHING = 0x4420, 0x446A, 0x44DE, 0x44FE, 0x46F0
TRG_NEED_TEXT, TRG_NEED_AFTER_TEXT, TRG_COST_TEXT, TRG_COST_AFTER_TEXT = 0x3FD34, 0x3FD47, 0x3FD5B, 0x3FD6E
TRG_QUESTION_TEXT, TRG_SHORT_TEXT, TRG_POOR_TEXT, TRG_POOR_2_TEXT, TRG_NOTHING_TEXT = 0x3FD8A, 0x3FD97, 0x3FDAE, 0x3FDBF, 0x3FD13
# s158: the window's title (訓練場), pushed first (last argument) before the _wopen at SKL_TRANG_WOPEN (0x45F6).
TRG_TITLE_TEXT = 0x3FD0C
# s162: the other titled windows' _wopen call sites (name: site). _townent opens its window at two places (two branches
# of the entry), _endstory is the final screen, _arathamus / _banquet the ending's cell and banquet.
WOPEN = 0x3379E
# s162: _endstory's call sites (FORMAT.md 6.9 "The final screen"): the heading's _font / _wprcen, the line loop's _wlocpr,
# the score line, its number's _font / _wdouti, the code line, its word's _font / _wprint, the postcard loop and the
# question's _font / _wlocpr; END_TABLE = its line table (0x40D1C: lines 0-1, 2-7, 8 = the question).
END_HEAD_FONT, END_HEAD, END_HEAD_TEXT = 0xAC8C, 0xACAC, 0x413C8
END_LINES, END_SCORE, END_SCORE_TEXT, END_NUM_FONT, END_NUM = 0xAA12, 0xAA48, 0x413D5, 0xAA66, 0xAA80
END_SCORE_AFTER, END_SCORE_AFTER_TEXT = 0xAAAC, 0x413E4
END_CODE, END_CODE_TEXT, END_WORD_FONT, END_WORD, END_WORD_TEXT = 0xAAD0, 0x413EC, 0xAAEE, 0xAB04, 0x413FB
END_CODE_AFTER, END_CODE_AFTER_TEXT = 0xAB2E, 0x41400
END_POSTCARD, END_QUESTION_FONT, END_QUESTION, END_TABLE = 0xAB6E, 0xABAE, 0xABDC, 0x40D1C
# s162: _deadmsg's call sites: _xblack, _wopen (untitled: its title is the empty string 0x44096), the black fill, the two
# white outlines, _font(-1, 1, 15, -1), the two centred lines, _xblack after the key.
DEAD_XBLACK, DEAD_WOPEN, DEAD_TITLE, DEAD_FILL, DEAD_FRAME_1, DEAD_FRAME_2 = 0x1E30C, 0x1E338, 0x44096, 0x1E35A, 0x1E388, 0x1E3B6
DEAD_FONT, DEAD_LINE_1, DEAD_LINE_1_TEXT, DEAD_LINE_2, DEAD_LINE_2_TEXT = 0x1E3D8, 0x1E3FA, 0x44097, 0x1E41C, 0x440B0
DEAD_XBLACK_2 = 0x1E490
# s162: _townent's aging rows: the name / line call sites of its two branches and their strings.
GATE_NAME_1, GATE_LINE_1, GATE_OLDER, GATE_NAME_2, GATE_LINE_2, GATE_DIED = 0x2F24, 0x2F3A, 0x3FA6F, 0x2FFC, 0x3012, 0x3FA89
TITLED = {
    "_inn": 0x24F8, "_townent": 0x2ECA, "_townent.2": 0x2F96, "_mage": 0x3A96, "_guildmenu": 0x4752,
    "_gatebank": 0x5326, "_bank": 0x596C, "_mystic": 0x6FD6, "_distribute": 0xA81A, "_endstory": 0xAC66,
    "_arathamus": 0xAD26, "_banquet": 0xAE14, "_hall": 0xDCA2, "_armory": 0xEA0A, "_setuse": 0xFE34,
    "_trade": 0x10D9A, "_setident": 0x2DD3E,
}


def text(img: bytes, a: int) -> str:
    return img[a:img.index(b"\0", a)].decode("cp932")


def main() -> None:
    img = P4X.read_bytes()[HU_HEADER:]
    tmp = Path("/tmp") / f"p4x-panels-{__import__('os').getpid()}.bin"
    tmp.write_bytes(img)
    try:
        dis = subprocess.run(["m68k-linux-gnu-objdump", "-D", "-b", "binary", "-m", "m68k:68000", str(tmp)],
                             capture_output=True, text=True, check=True).stdout
    finally:
        tmp.unlink()
    d = json.loads(SYMS.read_text())
    syms = d["symbols"] if isinstance(d, dict) else d
    funcs = sorted((int(s["value"], 16), s["name"]) for s in syms if int(s["value"], 16) < 0x3C000)
    lines = [m.groups() for m in (re.match(r"\s+([0-9a-f]+):\t[^\t]*\t(.*)", l) for l in dis.splitlines()) if m]
    panels: dict[str, dict] = {}
    for i, (addr, ins) in enumerate(lines):
        if ins.strip() != f"jsr 0x{MKICON:x}":
            continue
        a = int(addr, 16)
        args = []
        for _, prev in reversed(lines[max(0, i - 12):i]):
            m = re.match(r"movel #(-?\d+),%sp@-|clrl %sp@-", prev)
            if m:
                args.append(int(m.group(1)) if m.group(1) else 0)
            elif "%sp@-" in prev:
                args.append(None)
            if len(args) == 4:
                break
        count, table, _clear, title = args
        if count is None or table is None:
            continue  # computed (_acgamend's game list, _diskmenu's save slots): not a fixed panel
        fn = max((f for f in funcs if f[0] <= a), default=(0, "?"))[1]
        name = NAMES.get(a, fn)
        while name in panels:
            name += ".2"
        title_addr = DEFAULT_TITLE if not title or img[title] == 0 else title
        icons = []
        for k in range(count):
            e = table + 32 * k
            icon, key = struct.unpack_from(">ii", img, e + 20)
            icons.append({"icon": icon, "key": chr(key) if key != 0x20 else "Space", "label": f"p4x.0x{e:x}",
                          "text": text(img, e)})
        panels[name] = {"site": f"0x{a:X}", "title": f"p4x.0x{title_addr:x}", "title_text": text(img, title_addr),
                        "icons": icons}
    out = {
        "_schema": "phantasie.p4.panels/v1",
        "_source": "Phantasie IV X68000 P4.X _mkicon / _seticon (FORMAT.md 6.9 'Command panel'), extracted by "
                   "tools/scripts/x68k_p4_panels.py (s136).",
        "_layout": "Pack pixels (P4.X's 640 x 400 window space). window = _wopen(34, x, y, width, height); icon i at "
                   "(icon_x[i & 1], icon_top + icon_step * i) in the window, icon_size square (icon.pix record 'icon'); "
                   "label in font 1 centred on icon x + icon_size / 2 at icon y + icon_size, first character colour 2, "
                   "the rest colour 0 (all colour 2 when the label starts with a space). key = the hot key (_setbtn); "
                   "the clickable box is the icon only.",
        "window": {"x": 496, "y": 48, "width": 136, "height": 324},
        "icon_x": [38, 62],
        "icon_top": 8,
        "icon_step": 52,
        "icon_size": 32,
        "panels": panels,
    }
    # s140: the window-36 icon rows and _noyes's box.
    at = {int(a, 16): i for i, (a, _) in enumerate(lines)}

    def pushed_before(site: int, value: int, span: int = 40) -> bool:
        i = at[site]
        return any(f"#{value}," in ins for _, ins in lines[max(0, i - span):i])

    # s158: _acgamend(m) (0xE52) computes its call: `_mkicon(3 - m, 0x3F362 + 32 * m, 1, "Select Game")` at 0xEA0, so m = 0
    # (_syscon's E) lists SPACE, E:End game, T:Back to title and m = 1 (_endstory, _deadmsg) only E and T. Checked: the
    # title push and the table base (`addl #base,%d0` after `asll #5`) just before the call.
    assert lines[at[ACGAMEND_SITE]][1].strip() == f"jsr 0x{MKICON:x}" and pushed_before(ACGAMEND_SITE, ACGAMEND_TITLE, 12)
    assert any(f"addl #{ACGAMEND_TABLE}," in ins for _, ins in lines[at[ACGAMEND_SITE] - 12:at[ACGAMEND_SITE]])
    for m in (0, 1):
        icons = []
        for k in range(3 - m):
            e = ACGAMEND_TABLE + 32 * (m + k)
            icon, key = struct.unpack_from(">ii", img, e + 20)
            icons.append({"icon": icon, "key": chr(key) if key != 0x20 else "Space", "label": f"p4x.0x{e:x}",
                          "text": text(img, e)})
        panels["_acgamend" + (".1" if m else "")] = {"site": f"0x{ACGAMEND_SITE:X}",
                                                    "title": f"p4x.0x{ACGAMEND_TITLE:x}",
                                                    "title_text": text(img, ACGAMEND_TITLE), "icons": icons}

    rows = {}
    for name, (site, table, count, x0, dx) in ROWS.items():
        assert lines[at[site]][1].strip() == f"jsr 0x{SETICON:x}", (name, hex(site))
        assert pushed_before(site, table), (name, hex(table))
        icons = []
        for k in range(count):
            e = table + 32 * k
            icon, key = struct.unpack_from(">ii", img, e + 20)
            icons.append({"icon": icon, "key": chr(key) if key != 0x20 else "Space", "label": f"p4x.0x{e:x}",
                          "text": text(img, e)})
        rows[name] = {"site": f"0x{site:X}", "window": ROW_WINDOW, "sheet": ROW_SHEET, "icon_x": [x0 + dx * k for k in range(count)],
                      "icon_y": ROW_Y, "icons": icons}
    out["_rows"] = ("s140: icon rows outside _mkicon: _seticon(36, x, y) in window 36 (_wopen68, 150 x 48); s141: at "
                    "(window.x, window.y) = (314, 270) of the sheet's area (window 35, 'sheet', 472 x 338: 8 pixels in "
                    "from its right edge, 20 up from its bottom), flag 0 = no fill and no frame (the icons sit on the "
                    "sheet's grey, colour 14); same picture / label rules as the panels. icon i at (icon_x[i], icon_y).")
    out["rows"] = rows
    assert lines[at[NOYES_SITE]][1].strip() == "jsr 0x30e7c" and pushed_before(NOYES_SITE, 300, 6) \
        and pushed_before(NOYES_SITE, 104, 6)
    out["_noyes"] = ("s140: _noyes(win, 104, 300) in the shipwright's window (_wopen(-1, 160, 32, 312, 352)): the frame "
                     "_whole(x - 8, y - 8, 120, 32) sunken 2 pixels (colour 1 top / left, 15 bottom / right), buttons "
                     "48 x 16 filled colour 4 at (x, y) and (x + 56, y) with their text centred in font 0; key Y or a click "
                     "on YES = yes, any other key = no. s141: (x, y) is in the shipwright's window, whose origin is "
                     "SHIP.PKH's (the picture's top-left); 'clear' = _wfbox(win, 14, x - 10, y - 10, 124, 36), the "
                     "window's grey round the frame.")
    out["noyes"] = {
        "site": f"0x{NOYES_SITE:X}", "window": {"x": 160, "y": 32, "width": 312, "height": 352}, "x": 104, "y": 300,
        "frame": {"x": -8, "y": -8, "width": 120, "height": 32, "dark": 1, "light": 15},
        "clear": {"x": -10, "y": -10, "width": 124, "height": 36},
        "button_colour": 4, "back_colour": 14,
        "buttons": [
            {"x": 0, "y": 0, "width": 48, "height": 16, "key": "Y", "label": f"p4x.0x{NOYES_YES:x}",
             "text": text(img, NOYES_YES)},
            {"x": 56, "y": 0, "width": 48, "height": 16, "key": "N", "label": f"p4x.0x{NOYES_NO:x}",
             "text": text(img, NOYES_NO)},
        ],
    }
    # s151: the lists in window 34.
    funcs_at = [f for f, _ in funcs]

    def immediates(fn: int) -> set[int]:
        end = min((f for f in funcs_at if f > fn), default=fn + 0x400)
        vals = set()
        for a, ins in lines[at[fn]:]:
            if int(a, 16) >= end:
                break
            vals.update(int(v) for v in re.findall(r"#(-?\d+)", ins))
        return vals

    lists = {}
    for name, spec in LISTS.items():
        for fn, values in spec["check"].items():
            missing = [v for v in values if v not in immediates(fn)]
            assert not missing, (name, hex(fn), missing)
        rows = dict(spec["rows"], **LIST_BUTTON, button_width=LIST_WIDTHS.get(name, 136))
        entry: dict = {"function": f"0x{spec['fn']:X}"}
        if "window" in spec:
            entry["window"] = spec["window"]
            entry["title"] = f"p4x.0x{spec['title']:x}"
            entry["title_text"] = text(img, spec["title"])
        if "panel" in spec:
            assert spec["panel"] in panels, name
            entry["panel"] = spec["panel"]
        for k in ("rule", "clear"):
            if k in spec:
                entry[k] = spec[k]
        head = [{"x": h["x"], "y": h["y"], "key": h["key"], "label": f"p4x.0x{h['label']:x}",
                 "text": text(img, h["label"])} for h in spec.get("head", [])]
        if "head_table" in spec:
            table, count, x, y0, dy = spec["head_table"]
            for k in range(count):
                p = struct.unpack_from(">I", img, table + 4 * k)[0]
                head.append({"x": x, "y": y0 + dy * k, "key": str(k + 1), "label": f"p4x.0x{p:x}", "text": text(img, p)})
        if head:
            entry["head"] = head
        if "centred" in spec:
            entry["centred"] = [{"x": c["x"], "y": c["y"], "label": f"p4x.0x{c['label']:x}", "text": text(img, c["label"])}
                                for c in spec["centred"]]
        entry["rows"] = rows
        lists[name] = entry
    out["_lists"] = ("s151: lists P4.X draws as text buttons in window 34 (FORMAT.md 6.9 'Lists in window 34'). window = "
                     "the list's own _wopen (absent: window 34 as the Command panel left it, title bar kept), panel = the "
                     "_mkicon panel drawn first in the same window. head = text buttons over the list (_spelltop: text at "
                     "(x, y), button (rows.button_x, y, rows.button_width, rows.button_height) on key); centred = a "
                     "_wprcen line centred on x; rule = a _wbox outline; clear = the list area's _wfbox. Row i: the key "
                     "('A' + i for keys 'letter', the digit i + 1 for 'digit') at (key_x, top + step * i), its text at "
                     "text_x, extra_chars characters of a second field at extra_x, a button over the row; font 0, colour 0.")
    out["lists"] = lists
    # s151: title bar, hover look, _ewait, typed prompts, _numin, the shipwright's window (constants above).
    jsr = lambda site, target: lines[at[site]][1].strip() == f"jsr 0x{target:x}"
    assert jsr(WTTL_PRCEN, 0x32F2C) and pushed_before(WTTL_PRCEN, WTTL_Y, 12)
    out["_title"] = ("s151: _wttl: the title in the current font (0), style 0, colour 0, no cell box, centred on "
                     "width >> 1 (x - (_fsize >> 1)) at y -18 of the window, i.e. 3 rows under the bar's black top line.")
    out["title"] = {"font": 0, "colour": 0, "y": WTTL_Y}
    out["_hover"] = ("s151: _s_ms_get_cp (every _mcget pass of _mgetc's wait) XORs the button under the mouse pointer and "
                     "puts it back when the pointer leaves (0x820BC = the lit button); _mgetc clears it before returning. "
                     "_xbtn: type 64 (a plain button) = _xbox(5, ...) (each pixel's colour ^ 5), type 128 = nothing, else "
                     "_xicon(type & 63): the icon's opaque pixels ^ 0xFFFF (colour ^ 15). A key press shows no look.")
    out["hover"] = {"icon_xor": 15, "box_xor": 5}
    assert jsr(EWAIT_WBOX, 0x32FCC) and all(pushed_before(EWAIT_WBOX, v, 8) for v in (304, 80))
    assert jsr(EWAIT_PRCEN, 0x32F2C) and all(pushed_before(EWAIT_PRCEN, v, 8) for v in (EWAIT_LABEL, 82, 152))
    assert jsr(EWAIT_SETBTN, 0x2F9EA) and all(pushed_before(EWAIT_SETBTN, v, 10) for v in (64, 32, 15, 304, 81, 13))
    assert jsr(EPRINT_WFBOX, 0x3302E) and all(pushed_before(EPRINT_WFBOX, v, 8) for v in (96, 304, 15))
    out["_ewait"] = ("s151: _ewait in the location map's message window (304 wide; _eprint fills its top 96 rows colour "
                     "15): a colour-0 line (line), the label centred on label_x at label_y (font 0, colour 0), a plain "
                     "button (type 64) over 'button' pressing Space; only Space or that click ends the wait. 'band' = "
                     "INPUTS / INPUTN's colour-14 band over the same rows, their prompt at (prompt_x, band y).")
    out["ewait"] = {
        "site": f"0x{EWAIT_SETBTN:X}", "message": {"width": 304, "height": 96, "colour": 15},
        "line": {"x": 0, "y": 80, "width": 304, "height": 1, "colour": 0},
        "label": f"p4x.0x{EWAIT_LABEL:x}", "text": text(img, EWAIT_LABEL), "label_x": 152, "label_y": 82,
        "button": {"x": 0, "y": 81, "width": 304, "height": 15}, "key": "Space",
        "band": {"x": 0, "y": 80, "width": 304, "height": 16, "colour": 14}, "prompt_x": 16,
        "letters": f"p4x.0x{TYPED_LETTERS:x}", "letters_text": text(img, TYPED_LETTERS),
        "number": f"p4x.0x{TYPED_NUMBER:x}", "number_text": text(img, TYPED_NUMBER),
    }
    assert jsr(NUMIN_SITE, 0x309E4) and all(pushed_before(NUMIN_SITE, v, 8) for v in (5, 224, 40, 34))
    assert pushed_before(0x30AD8, NUMIN_TABLE + 8, 40)  # the table's button field, read at the keypad's _setbtn
    keys = []
    for k in range(12):
        ch, key, button = struct.unpack_from(">iii", img, NUMIN_TABLE + 12 * k)
        keys.append({"text": chr(ch), "key": {8: "Back", 13: "Enter"}.get(key, chr(key)), "value": button})
    out["_numin"] = ("s151: _numin's keypad in window 34 (the Bank's panel) at (x, y): key k at (x + step * (k % 3), "
                     "y + step * (k / 3)), size x size, ' c ' in font 0 colour text_colour on cells of cell_colour; "
                     "frame = _whole (sunken, dark top / left, light bottom / right) relative to (x, y), clear = the "
                     "window grey drawn over it after; each key a plain button (type 64). value: 0-9 digits, 10 Return, "
                     "11 erase. cursor = the steady block at the typed amount's end.")
    out["numin"] = {
        "site": f"0x{NUMIN_SITE:X}", "window": 34, "x": 40, "y": 224, "step": 20, "size": 16, "digits": 5,
        "frame": {"x": -8, "y": -8, "width": 72, "height": 92, "dark": 1, "light": 15},
        "clear": {"x": -10, "y": -10, "width": 76, "height": 96},
        "cell_colour": 4, "text_colour": 0, "back_colour": 14, "keys": keys,
        "cursor": {"width": 8, "height": 16, "colour": 0},
    }
    assert jsr(SHIP_DISPPCG, 0x3403C) and all(pushed_before(SHIP_DISPPCG, v, 8) for v in (0x3FE66, 336, 68))
    assert jsr(SHIP_OFFER, 0x3403C) and all(pushed_before(SHIP_OFFER, v, 8) for v in (0x400F4, 236, 16))
    assert jsr(SHIP_LINE2, 0x3403C) and all(pushed_before(SHIP_LINE2, v, 8) for v in (0x4011B, 252, 32))
    assert jsr(SHIP_LINE3, 0x32C68) and all(pushed_before(SHIP_LINE3, v, 6) for v in (268, 32))
    assert jsr(SHIP_YES, 0x32F2C) and all(pushed_before(SHIP_YES, v, 8) for v in (0x40147, 300, 156))
    assert jsr(SHIP_NO, 0x32F2C) and all(pushed_before(SHIP_NO, v, 8) for v in (0x40160, 300, 156))
    out["_shipwright"] = ("s151: _shipyard's window (titled, so its grey is colour 14) with SHIP.PKH (picture_height rows) "
                          "at its origin: the offer's lines start at 'lines' (the first at the window's text cursor, "
                          "continued by _wprint), the answer centred on answer x at answer y after _noyes's box is "
                          "cleared, the gold line at gold (x, y): label, the amount right-aligned in 'digits' digit "
                          "widths (each leading zero = two spaces), the unit. Font 0, colour text_colour.")
    out["shipwright"] = {
        "window": {"x": 160, "y": 32, "width": 312, "height": 352}, "picture_height": 220, "back_colour": 14,
        "text_colour": 0, "title": "p4x.0x400d4", "title_text": text(img, 0x400D4),
        "lines": [{"x": 16, "y": 236}, {"x": 32, "y": 252}, {"x": 32, "y": 268}],
        "answer": {"x": 156, "y": 300, "yes": "p4x.0x40147", "no": "p4x.0x40160"},
        "gold": {"x": 68, "y": 336, "label": "p4x.0x3fe66", "label_text": text(img, 0x3FE66), "digits": 8,
                 "unit": "p4x.0x3fe76", "unit_text": text(img, 0x3FE76)},
    }
    assert lines[at[DISK_SITE]][1].strip() == f"jsr 0x{MKICON:x}" and pushed_before(DISK_SITE, DISK_TABLE, 12) \
        and pushed_before(DISK_SITE, DISK_TITLE, 12)
    disk = []
    for k in (0, 2, 3, 4):  # mode 0's New Game, then the three save slots (entry 1 is mode 1's SPACE)
        e = DISK_TABLE + 32 * k
        icon, key = struct.unpack_from(">ii", img, e + 20)
        disk.append({"icon": icon, "key": chr(key) if key != 0x20 else "Space", "label": f"p4x.0x{e:x}",
                     "text": text(img, e)})
    out["_disk"] = ("s151: _diskmenu(0) (0x1AB4), the title's New Game / Saved Game panel (_disk(0) after _openning, no "
                    "key between): window 34 as the panels' (title 'Select File'), New Game at icon place 0, then one "
                    "'*k:Save' icon per save slot k (1-3) that exists (fames.k), at the next places 1, 2, 3; the label as "
                    "written (its first character '*' in colour 2). One _mgetc: New Game = a new game (_load(0)), slot k "
                    "= load save k; no other key does anything.")
    out["disk"] = {"site": f"0x{DISK_SITE:X}", "title": f"p4x.0x{DISK_TITLE:x}", "title_text": text(img, DISK_TITLE),
                   "icons": disk}
    for site, target, values, span in (
            (SKL_WOPEN, 0x3379E, (248, 312, 136, 160, 32), 14), (SKL_BAR, 0x3302E, (20, 312, -20, 32), 12),
            (SKL_TITLE, 0x3406E, (SKL_TITLE_TEXT, -16, 156, 32), 8), (SKL_HEADER, 0x3403C, (SKL_HEADER_TEXT, 20, 16, 32), 8),
            (SKL_WHOLE, 0x32F6E, (176, 304, 36, 4, 32), 10), (SKL_ROW_CLEAR, 0x3302E, (16, 304, 4, 14, 32), 12),
            (SKL_NAME, 0x3403C, (40, 32), 8), (SKL_NOW, 0x32C68, (112, 32), 6), (SKL_AFTER, 0x32C68, (192, 32), 6),
            (SKL_SETBTN, 0x2F9EA, (2, 64, 16, 304, 4, 32, -1), 32), (SKL_PROMPT, 0x3403C, (0x416B6, 2, 16, 32), 8),
            (SKL_TRAINED, 0x3403C, (224, 16, 32), 8), (SKL_PROMPT_CLEAR, 0x3302E, (14, 312, 2, 32), 12),
            (SKL_END_BAR, 0x3302E, (20, 312, -20, 14, 32), 12), (SKL_END, 0x3406E, (SKL_END_TEXT, -16, 156, 32), 8),
            (SKL_TRANG_WOPEN, 0x3379E, (352, 312, 32, 160), 14), (SKL_TRANG_INK, 0x2E16A, (2,), 6)):
        assert jsr(site, target) and all(pushed_before(site, v, span) for v in values), hex(site)
    out["_sklup"] = ("s152: _sklup's level-up skill window (FORMAT.md 6.9 'Level-up skill picks'): window 32 at "
                     "(window.x, window.y), drawn with no fill or frame over _trang's titled window ('under', its grey "
                     "back_colour); bar = the black box over its top with the title centred on title x at title y in "
                     "title colour (at the end: the bar in done.back, done's line in done.colour); header at (x, y); "
                     "frame = _whole (sunken, 2 pixels outside the rect); row i: its area cleared to rows.back, 'A' + i "
                     "at key_x, the skill at skill_x, now and after as _wdouti(3) at now_x / after_x, a plain button "
                     "(type 64) over (button x, y, width, height) on 'A' + i; the prompt at prompt (x, y) (cleared after "
                     "each pick), the trained line at trained (x, y). Text in font 0, colour text_colour, cell boxes "
                     "cell_colour (_font(-1, -1, -1, 14)). y < 0 is above the window (the bar).")
    out["sklup"] = {
        "site": "0xC0EE", "window": {"x": 160, "y": 136, "width": 312, "height": 248},
        "under": {"x": 160, "y": 32, "width": 312, "height": 352}, "back_colour": 14,
        "bar": {"x": 0, "y": -20, "width": 312, "height": 20, "colour": 0},
        "title": {"x": 156, "y": -16, "colour": 15, "label": f"p4x.0x{SKL_TITLE_TEXT:x}", "text": text(img, SKL_TITLE_TEXT)},
        "done": {"x": 156, "y": -16, "colour": 2, "back": 14, "label": f"p4x.0x{SKL_END_TEXT:x}",
                 "text": text(img, SKL_END_TEXT)},
        "header": {"x": 16, "y": 20, "label": f"p4x.0x{SKL_HEADER_TEXT:x}", "text": text(img, SKL_HEADER_TEXT)},
        "frame": {"x": 4, "y": 36, "width": 304, "height": 176, "dark": 1, "light": 15},
        "rows": {"count": 11, "top": 36, "step": 16, "key_x": 16, "skill_x": 40, "now_x": 112, "after_x": 192,
                 "digits": 3, "back": 14, "button": {"x": 4, "width": 304, "height": 16}},
        "prompt": {"x": 16, "y": 2}, "trained": {"x": 16, "y": 224},
        # s158 (XEiJ, parity/shots/p4-x68k-sklup.png / -sklup-done.png): the rows, header, prompt and trained line are
        # black. _trang's _font(0, -1, 2, -1) at 0x442A comes before _confirm, whose _mkicon clears _fgcol (0), so
        # _sklup's `_font(-1, -1, -2, -2)` restores 0, not 2.
        "text_colour": 0, "cell_colour": 14,
    }
    # s156: _trang's own window 32 (TRG_* sites below), the lines over _sklup's bar.
    wprint, wloc, wlocpr, wdoutl, font = 0x32CE4, 0x32C68, 0x3403C, 0x343FC, 0x2E16A
    for site, target, values, span in (
            (TRG_NAME, wlocpr, (16, 16, 32), 12),
            (TRG_NEED, wprint, (TRG_NEED_TEXT, 32), 8), (TRG_NEED_LOC, wloc, (32, 16), 8),
            (TRG_NEED_FONT, font, (11, 1), 12), (TRG_NEED_NUM, wdoutl, (137, 32), 8),
            (TRG_NEED_AFTER, wprint, (TRG_NEED_AFTER_TEXT, 32), 8),
            (TRG_COST, wprint, (TRG_COST_TEXT, 32), 8), (TRG_COST_LOC, wloc, (32, 16), 8),
            (TRG_COST_FONT, font, (8, 1), 12), (TRG_COST_NUM, wdoutl, (137, 32), 8),
            (TRG_COST_AFTER, wprint, (TRG_COST_AFTER_TEXT, 32), 8),
            (TRG_QUESTION, wlocpr, (TRG_QUESTION_TEXT, 48, 16, 32), 12), (TRG_SHORT, wprint, (TRG_SHORT_TEXT, 32), 8),
            (TRG_POOR, wprint, (TRG_POOR_TEXT, 32), 8), (TRG_POOR_2, wlocpr, (TRG_POOR_2_TEXT, 32), 12),
            (TRG_NOTHING, wprint, (TRG_NOTHING_TEXT, 32), 8),
            (SKL_TRANG_WOPEN, 0x3379E, (TRG_TITLE_TEXT, 1, 352, 312, 32, 160), 14)):
        assert jsr(site, target) and all(pushed_before(site, v, span) for v in values), hex(site)

    def seg(a: int, **kw) -> dict:
        return {**kw, "label": f"p4x.0x{a:x}", "text": text(img, a)}

    out["_trang"] = ("s156: _trang's own titled window 32 (FORMAT.md 6.9 'Level-up skill picks'), opened again at each "
                     "member pick (0x45F6): grey back_colour; the member's name at name (x, y); then the note's segments "
                     "in order, each at its (x, y) when it has one, else at the text cursor after the last: a string "
                     "(label / text) or the number (_wdoutl(32, 137, n): left-aligned, no padding) in its font / colour "
                     "(default font 0, text_colour); short_of_gold follows the cost question at the cursor after a YES the "
                     "purse cannot pay. A level-up's _sklup window (sklup) sits in its lower part.")
    out["trang"] = {
        "site": "0x3C30", "window": {"x": 160, "y": 32, "width": 312, "height": 352}, "back_colour": 14,
        "title": f"p4x.0x{TRG_TITLE_TEXT:x}", "title_text": text(img, TRG_TITLE_TEXT),
        "text_colour": 0, "line_step": 16, "name": {"x": 16, "y": 16},
        "notes": {
            "poor_condition": [seg(TRG_POOR_TEXT), seg(TRG_POOR_2_TEXT, x=32, y=32)],
            "nothing_more": [seg(TRG_NOTHING_TEXT)],
            "needs_experience": [seg(TRG_NEED_TEXT), {"x": 16, "y": 32, "number": True, "font": 1, "colour": 11},
                                 seg(TRG_NEED_AFTER_TEXT)],
            "cost": [seg(TRG_COST_TEXT), {"x": 16, "y": 32, "number": True, "font": 1, "colour": 8},
                     seg(TRG_COST_AFTER_TEXT), seg(TRG_QUESTION_TEXT, x=16, y=48)],
        },
        # s158 (XEiJ, parity/shots/p4-x68k-training-short.png): black, as _confirm's _mkicon cleared _fgcol (see sklup).
        "short_of_gold": seg(TRG_SHORT_TEXT),
    }
    # s162: the other titled windows (_wopen flag 1) of the buildings, the town gate, the item commands and the ending.
    # Each site's seven pushes are read back: _wopen(win, x, y, width, height, 1, title).
    titled = {}
    for name, site in TITLED.items():
        assert jsr(site, WOPEN), (name, hex(site))
        i = at[site]
        pushes = [ins for _, ins in lines[i - 7:i]]
        vals = []
        for ins in pushes:
            m = re.match(r"movel #(-?\d+),%sp@-$", ins.strip())
            assert m, (name, ins)
            vals.append(int(m.group(1)))
        title, flag, h, w, y, x, win = vals
        assert flag == 1, (name, flag)
        titled[name] = {"site": f"0x{site:X}", "window": {"x": x, "y": y, "width": w, "height": h},
                        "title": f"p4x.0x{title:x}", "title_text": text(img, title)}
    out["_titled"] = ("s162: the titled windows (_wopen(win, x, y, width, height, 1, title): window grey colour 14, the "
                      "title bar and frame of _wwind / _wactive, FORMAT.md 6.9 'Window frames') that the buildings, the town "
                      "gate, the item commands and the ending open in the picture's place; the training hall's (trang) and "
                      "the shipwright's are their own entries.")
    out["titled"] = titled
    # s162: _endstory's final screen (0xA9E6) in its window (titled["_endstory"]): y = 48 for the heading, then 80.
    wlocpr, wprint, wprcen, wdouti = 0x3403C, 0x32CE4, 0x32F2C, 0x34386
    for site, target, values, span in (
            (END_HEAD_FONT, font, (16, 1, 8), 12), (END_HEAD, wprcen, (END_HEAD_TEXT, 140), 12),
            (END_LINES, wlocpr, (32,), 8), (END_SCORE, wlocpr, (END_SCORE_TEXT, 48, 32), 14),
            (END_NUM_FONT, font, (16, 8, 1), 12), (END_NUM, wdouti, (3, 32), 8), (END_SCORE_AFTER, wprint, (END_SCORE_AFTER_TEXT,), 6),
            (END_CODE, wlocpr, (END_CODE_TEXT, 64, 32), 14), (END_WORD_FONT, font, (16, 8, 1), 12),
            (END_WORD, wprint, (END_WORD_TEXT,), 6), (END_CODE_AFTER, wprint, (END_CODE_AFTER_TEXT,), 6),
            (END_POSTCARD, wlocpr, (64, 32), 14), (END_QUESTION_FONT, font, (16, 2, 1), 12),
            (END_QUESTION, wlocpr, (64, 32), 14)):
        assert jsr(site, target) and all(pushed_before(site, v, span) for v in values), hex(site)
    table = [struct.unpack_from(">I", img, END_TABLE + 4 * i)[0] for i in range(9)]
    out["_endstory"] = ("s162: _endstory's final screen (0xA9E6) in its titled window (titled._endstory): the heading "
                        "centred on heading.x at heading.y in font 0, style 8 (shadow: FORMAT.md 6.12), colour 1; then the "
                        "segments in order, each at its (x, y) when it has one, else at the text cursor after the last: a "
                        "string (label / text) or the score (_wdouti(32, digits, n)) in its font / colour (default font 0, "
                        "text_colour); after the SPACE wait the question in font 1, style 1 (bold), colour 2.")
    out["endstory"] = {
        "site": "0xA9E6", "text_colour": 0, "line_step": 16,
        "heading": seg(END_HEAD_TEXT, x=140, y=48, font=0, style=8, colour=1),
        "segments": [seg(table[0], x=32, y=80), seg(table[1], x=32, y=96),
                     seg(END_SCORE_TEXT, x=32, y=128), {"number": True, "digits": 3, "font": 1, "colour": 8},
                     seg(END_SCORE_AFTER_TEXT),
                     seg(END_CODE_TEXT, x=32, y=144), seg(END_WORD_TEXT, font=1, colour=8), seg(END_CODE_AFTER_TEXT)]
                    + [seg(table[i], x=32, y=80 + 64 + 16 * i) for i in range(2, 8)],
        "question": seg(table[8], x=32, y=80 + 64 + 16 * 9, font=1, style=1, colour=2),
    }
    # s162: _townent's aging rows (0x2CF2): in its Town Gate window (titled._townent, opened at the first member who ages),
    # member m's line at (8, 8 + 16 m): the name (_wlocpr), then " has grown older." / " has died of old age." (_wprint).
    for site, target, values, span in ((GATE_NAME_1, wlocpr, (8, 32), 14), (GATE_LINE_1, wprint, (GATE_OLDER,), 6),
                                       (GATE_NAME_2, wlocpr, (8, 32), 14), (GATE_LINE_2, wprint, (GATE_DIED,), 6)):
        assert jsr(site, target) and all(pushed_before(site, v, span) for v in values), hex(site)
    assert any("addql #8,%d0" in ins for _, ins in lines[at[GATE_NAME_1] - 6:at[GATE_NAME_1]])
    out["_townent"] = ("s162: _townent's aging rows in its Town Gate window (titled._townent): member m's row at (x, y + "
                       "step * m), the name then the line (grown_older / died); the ink as the window's (text_colour, not "
                       "read: [~]).")
    out["townent"] = {"site": "0x2CF2", "x": 8, "y": 8, "step": 16, "text_colour": 0,
                      "grown_older": seg(GATE_OLDER), "died": seg(GATE_DIED)}
    # s162: _deadmsg (0x1E308): _xblack (the whole 768 x 512 screen colour 0), its untitled window 32, the two lines; after
    # the key _xblack again, then _acgamend(1).
    wbox, wfbox, wkprcen, xblack = 0x32FCC, 0x3302E, 0x3406E, 0x31EDC
    for site, target, values, span in (
            (DEAD_XBLACK, xblack, (), 0), (DEAD_WOPEN, WOPEN, (DEAD_TITLE, 64, 440, 168, 100, 32), 14),
            (DEAD_FILL, wfbox, (64, 440, 32), 14), (DEAD_FRAME_1, wbox, (60, 436, 2, 15), 14),
            (DEAD_FRAME_2, wbox, (58, 434, 3, 15), 14), (DEAD_FONT, font, (15, 1), 12),
            (DEAD_LINE_1, wkprcen, (DEAD_LINE_1_TEXT, 16, 220), 12), (DEAD_LINE_2, wkprcen, (DEAD_LINE_2_TEXT, 32, 220), 12),
            (DEAD_XBLACK_2, xblack, (), 0)):
        assert jsr(site, target) and all(pushed_before(site, v, span) for v in values), hex(site)
    assert img[DEAD_TITLE] == 0  # untitled
    out["_deadmsg"] = ("s162: _deadmsg (0x1E308), a wiped-out party: _xblack (screen: the whole 768 x 512 graphic screen "
                       "filled colour 0), then its untitled window (window: no frame of _wwind's) filled colour fill, two "
                       "frames (_wbox outlines) in frame_colour, the lines centred on x at y in font 0, style 1 (bold), "
                       "colour text_colour; _sdbeep, any key; _xblack again before _acgamend(1).")
    out["deadmsg"] = {
        "site": "0x1E308", "screen": {"width": 768, "height": 512, "colour": 0},
        "window": {"x": 100, "y": 168, "width": 440, "height": 64}, "fill": 0,
        "frames": [{"x": 2, "y": 2, "width": 436, "height": 60}, {"x": 3, "y": 3, "width": 434, "height": 58}],
        "frame_colour": 15, "font": 0, "style": 1, "text_colour": 15,
        "lines": [seg(DEAD_LINE_1_TEXT, x=220, y=16), seg(DEAD_LINE_2_TEXT, x=220, y=32)],
    }
    building_windows(out, img, jsr, pushed_before, seg)
    OUT.write_text(json.dumps(out, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print(OUT, len(panels), "panels")


def building_windows(out: dict, img: bytes, jsr, pushed_before, seg) -> None:
    """s163: what the inn, the bank and the ending's message pages draw in the grey under their pictures (FORMAT.md 6.9
    'Building windows under the picture'). Each (site, target, pushed values) is checked against the disassembly."""
    wprint, wlocpr, wfbox, whole, wprcen, wlocdi, wdouti, wdout, wloc, font, setbtn, wopen68, wputc = (
        0x32CE4, 0x3403C, 0x3302E, 0x32F6E, 0x32F2C, 0x343B6, 0x34386, 0x340B0, 0x32C68, 0x2E16A, 0x2F9EA, 0x33A5C,
        0x33E2A)
    slept, mp_up = 0x3F6F0, 0x3F6FC
    acc, bal, dep_head, dep_acc, dep_share, per, per_unit = 0x3FF14, 0x3FF19, 0x3FE7A, 0x3FE83, 0x3FE88, 0x3FE99, 0x3FEA8
    wd_head, wd_acc, wd_bal, wd_limit, wd_amount = 0x3FED7, 0x3FEE0, 0x3FEE5, 0x3FEEA, 0x3FEF1
    gold_label, gold_unit, share_left, share_right = 0x3FE66, 0x3FE76, 0x3FE93, 0x3FE96
    for site, target, values, span in (
            # _inn (0x2188): _font(0, 0, 0, 16), the box _whole(win, 8, 210, 296, 16 * party size + 4), member m's name at
            # (16, 212 + 16 m) (asll #4, addl #212), " は眠った．", " MP が上がった !" after it on a won MP roll.
            (0x24BE, font, (16,), 12), (0x25D0, whole, (296, 210, 8), 14), (0x21EA, wlocpr, (16, 212), 14),
            (0x21FE, wprint, (slept,), 6), (0x22DE, wprint, (mp_up,), 6),
            # _bank (0x57A4): _font(0, 0, 0, 14), the grey under the picture, the box, the headers, the line, the rows
            # (name at x 64, balance _wlocdi 5 digits at x 208, y = 212 + 16 i), _disppcg's gold line.
            (0x5938, font, (14,), 12), (0x5A40, wfbox, (172, 312, 180, 14), 14), (0x5A68, whole, (116, 248, 194, 32), 12),
            (0x5A8A, wlocpr, (acc, 196, 64), 10), (0x5AAC, wlocpr, (bal, 196, 216), 10),
            (0x5AD6, wfbox, (1, 184, 211, 64), 14), (0x57E4, wlocpr, (64,), 10), (0x582A, wlocdi, (5, 208), 12),
            (0x4952, wlocpr, (gold_label, 336, 68), 10), (0x496C, wdouti, (8,), 8), (0x4980, wprint, (gold_unit,), 6),
            # _deposit (0x4992): the grey, the black band (0, 180, 312, 16) and its heading centred white, the box, the
            # headers, the line, the share buttons " 0 " … " 3 " at (272, 240 + 20 i) 24 x 16 on colour-4 cells, member
            # m's row at y 228 + 16 m (name x 64, the share typed at x 196), then "１分配につき" + 6 digits + " GP".
            (0x4DA4, wfbox, (172, 312, 180, 14), 14), (0x4DCA, wfbox, (16, 312, 180), 14), (0x4DE4, font, (16, 15), 12),
            (0x4E06, wprcen, (dep_head, 180, 156), 10), (0x4E1C, font, (14,), 12),
            (0x4E52, whole, (116, 216, 210, 32), 12), (0x4E74, wlocpr, (dep_acc, 212, 64), 10),
            (0x4E96, wlocpr, (dep_share, 212, 148), 10), (0x4EC0, wfbox, (1, 160, 227, 64), 14),
            (0x49E2, setbtn, (1, 64, 16, 24, 240, 272), 24), (0x4A22, wloc, (240, 272), 12),
            (0x4A38, wprint, (share_left,), 6), (0x4A64, wprint, (share_right,), 6),
            (0x4ACA, wlocpr, (64, 228), 14), (0x4AE4, wloc, (196,), 8), (0x4B6C, wdouti, (1,), 8),
            (0x4C28, wfbox, (16, 312, 336, 14), 14), (0x4C4A, wlocpr, (per, 336, 16), 10), (0x4C70, wdout, (1, 6), 14),
            (0x4C86, wprint, (per_unit,), 6),
            # _withdraw (0x5414): the same band with its heading, the box, four headers, the line; member m's row at
            # y 228 + 16 m: name x 32, balance x 96, limit x 160 (each _wlocdi 5 digits); _numin's amount at x 224.
            (0x5632, wfbox, (172, 312, 180, 14), 14), (0x5658, wfbox, (16, 312, 180), 14), (0x5672, font, (16, 15), 12),
            (0x5694, wprcen, (wd_head, 180, 156), 10), (0x56AA, font, (14,), 12),
            (0x56E0, whole, (116, 280, 210, 16), 12), (0x5702, wlocpr, (wd_acc, 212, 32), 10),
            (0x5724, wlocpr, (wd_bal, 212, 96), 10), (0x5746, wlocpr, (wd_limit, 212, 160), 10),
            (0x5768, wlocpr, (wd_amount, 212, 224), 10), (0x5792, wfbox, (1, 240, 227, 32), 14),
            (0x54A6, wlocpr, (32, 228), 14), (0x54CA, wlocdi, (5, 96), 12), (0x54EE, wlocdi, (5, 160), 12),
            (0x5538, 0x309E4, (5, 224, 40, 34), 12), (0x5570, wlocdi, (5, 224), 12),
            # _endmsg (0xA8B2): window 33 = _wopen68(33, _cvx(160), _cvy(20) + 48 + offset, 312, 52, 1, "") (offset 232
            # for blocks > 3 other than 6), _wcls(33) before each page, line k at (16, 14 k + 4), at most 3 a page.
            (0xA9D2, wopen68, (0x413BA, 1, 52, 312), 18), (0xA8FE, wlocpr, (16, 33), 12)):
        assert jsr(site, target) and all(pushed_before(site, v, span) for v in values), hex(site)
    assert img[0x413BA] == 0  # _endmsg's window is untitled
    assert text(img, share_left) == text(img, share_right) == "  "
    out["_inn"] = ("s163: _inn's rows in its titled window (titled._inn) under INN.PKH: the sunken box _whole(x, y, width, "
                   "box_pad + row_step * party size) (2-pixel frame outside it, FORMAT.md 6.9), member m's row at (text_x, "
                   "text_y + row_step * m) for each living member: the name, slept, and mp_up after it on the same row when "
                   "the MP roll is won. Font 0, ink text_colour, on the window's grey.")
    out["inn"] = {"site": "0x2188", "box": {"x": 8, "y": 210, "width": 296}, "box_pad": 4, "row_step": 16,
                  "text_x": 16, "text_y": 212, "text_colour": 0, "slept": seg(slept), "mp_up": seg(mp_up)}
    out["_bank"] = ("s163: _bank / _deposit / _withdraw in the bank's titled window (titled._bank) under BANK.PKH: 'clear' "
                    "the grey under the picture; accounts = _bank's table (sunken box, headers, a colour-0 line, member i's "
                    "name and balance at y row_y + row_step * i, balances _wlocdi(digits): each leading zero two spaces); "
                    "deposit / withdraw: a black band with the heading centred in colour 15, their box, headers, line and "
                    "rows (y = row_y + row_step * m); the deposit's share buttons ' n ' (n = 0-3) on button_colour cells; "
                    "after the shares 'clear_per' and the per-share line. gold = _disppcg's line (the shipwright's).")
    band = {"x": 0, "y": 180, "width": 312, "height": 16, "colour": 0}
    out["bank"] = {
        "site": "0x57A4", "clear": {"x": 0, "y": 180, "width": 312, "height": 172, "colour": 14}, "text_colour": 0,
        "accounts": {"frame": {"x": 32, "y": 194, "width": 248, "height": 116},
                     "headers": [seg(acc, x=64, y=196), seg(bal, x=216, y=196)],
                     "line": {"x": 64, "y": 211, "width": 184, "height": 1, "colour": 0},
                     "row_y": 212, "row_step": 16, "columns": {"name": 64, "balance": 208}, "digits": 5},
        "deposit": {"band": band, "heading": seg(dep_head, x=156, y=180, colour=15),
                    "frame": {"x": 32, "y": 210, "width": 216, "height": 116},
                    "headers": [seg(dep_acc, x=64, y=212), seg(dep_share, x=148, y=212)],
                    "line": {"x": 64, "y": 227, "width": 160, "height": 1, "colour": 0},
                    "row_y": 228, "row_step": 16, "columns": {"name": 64, "share": 196},
                    "buttons": {"x": 272, "y": 240, "step": 20, "width": 24, "height": 16, "count": 4, "colour": 4,
                                "pad": seg(share_left)},
                    "clear_per": {"x": 0, "y": 336, "width": 312, "height": 16, "colour": 14},
                    "per_share": seg(per, x=16, y=336), "per_digits": 6, "per_unit": seg(per_unit)},
        "withdraw": {"band": band, "heading": seg(wd_head, x=156, y=180, colour=15),
                     "frame": {"x": 16, "y": 210, "width": 280, "height": 116},
                     "headers": [seg(wd_acc, x=32, y=212), seg(wd_bal, x=96, y=212), seg(wd_limit, x=160, y=212),
                                 seg(wd_amount, x=224, y=212)],
                     "line": {"x": 32, "y": 227, "width": 240, "height": 1, "colour": 0},
                     "row_y": 228, "row_step": 16,
                     "columns": {"name": 32, "balance": 96, "limit": 160, "amount": 224}, "digits": 5},
        "gold": {"x": 68, "y": 336, "label": f"p4x.0x{gold_label:x}", "label_text": text(img, gold_label), "digits": 8,
                 "unit": f"p4x.0x{gold_unit:x}", "unit_text": text(img, gold_unit)},
    }
    # s163: the item windows' rows (FORMAT.md 6.9 "Item lists", read s151): the headers and lines checked here.
    for site, target, values, span in (
            (0xEB90, wlocpr, (0x42588, 16, 80), 10), (0xEBBA, wlocpr, (0x4258D, 16, 192), 10),
            (0xEBE4, wlocpr, (0x42593, 16, 248), 10), (0xEC16, wfbox, (1, 248, 31, 32), 14),
            (0xEABA, whole, (208, 280, 16), 12), (0xEA8A, whole, (16, 51, 224, 245), 12), (0xEAF0, wfbox, (18, 51, 222, 245, 14), 14),
            (0xEB1A, wlocpr, (0x42583, 224, 253), 10), (0xEB62, setbtn, (64, 77, 16, 51, 224, 245), 18),
            (0xE1C4, wprint, (0x42540,), 8), (0xE1A4, 0x34300, (4,), 8),
            (0x10702, wlocpr, (0x4277C, 48, 56), 10), (0x1072C, wlocpr, (0x42781, 48, 192), 10),
            (0x10756, wlocpr, (0x42787, 48, 240), 10), (0x10788, wfbox, (1, 280, 63, 16), 14),
            (0x1053C, wlocpr, (56,), 10), (0x10592, 0x34338, (3, 208), 12), (0x105E2, wlocdi, (1, 240), 14),
            (0x9CBE, wlocpr, (0x40BAC, 16, 48), 10), (0x9CE8, wlocpr, (0x40BB1, 16, 192), 10),
            (0x9D12, wlocpr, (0x40BB7, 16, 240), 10), (0x9D44, wfbox, (1, 280, 31, 16), 14)):
        assert jsr(site, target) and all(pushed_before(site, v, span) for v in values), hex(site)
    item_rows = {"top": 64, "step": 32, "count": 9, "letter_x": 16, "icon_x": 24, "name_x": 56, "points_x": 208,
                 "points_digits": 3, "users_x": 240, "users_step": 8}
    out["_item_windows"] = ("s163: the item windows' rows (FORMAT.md 6.9 'Item lists'; titled.<key> is each window): row i "
                            "at y = top + step * i, 32 high; the letter 'A' + i in letter_colour at (letter_x, y + 8), the "
                            "icon (font 2, 32 x 32) at (icon_x, y), the name at (name_x, y + 8), the points (ids 1-84) "
                            "padded to points_digits at points_x (or not_usable when the marked member cannot use it), the "
                            "price padded to price_digits at price_x, the users' digits m + 1 at users_x + users_step * m; "
                            "headers, the colour-0 line, sunken frames (_whole) and the member's name at their places. "
                            "_distribute's equipment table under its divider is not read [~].")
    out["item_windows"] = {
        "letter_colour": 2, "text_colour": 0, "back_colour": 14,
        "_armory": {"rows": {"top": 32, "step": 32, "count": 6, "letter_x": 32, "icon_x": 44, "name_x": 80,
                             "points_x": 192, "points_digits": 4, "price_x": 240, "price_digits": 5},
                    "headers": [seg(0x42588, x=80, y=16), seg(0x4258D, x=192, y=16), seg(0x42593, x=248, y=16)],
                    "line": {"x": 32, "y": 31, "width": 248, "height": 1, "colour": 0},
                    "frames": [{"x": 16, "y": 16, "width": 280, "height": 208}, {"x": 245, "y": 224, "width": 51, "height": 16}],
                    "more": seg(0x42583, x=253, y=224), "not_usable": seg(0x42540),
                    "member": {"x": 20, "y": 256}, "message": {"x": 32, "y": 288}},
        "_trade": {"rows": item_rows,
                   "headers": [seg(0x4277C, x=56, y=48), seg(0x42781, x=192, y=48), seg(0x42787, x=240, y=48)],
                   "line": {"x": 16, "y": 63, "width": 280, "height": 1, "colour": 0}, "member": {"x": 16, "y": 16}},
        "_distribute": {"rows": {"top": 32, "step": 32, "count": 5, "icon_x": 16, "name_x": 48, "points_x": 208,
                                 "points_digits": 3, "users_x": 240, "users_step": 8},
                        "headers": [seg(0x40BAC, x=48, y=16), seg(0x40BB1, x=192, y=16), seg(0x40BB7, x=240, y=16)],
                        "line": {"x": 16, "y": 31, "width": 280, "height": 1, "colour": 0},
                        "frames": [{"x": 16, "y": 208, "width": 280, "height": 4}], "lit_xor": 15},
    }
    out["_endmsg"] = ("s163: _endmsg(block)'s window 33 for each ending page: untitled (white, colour 15, black frame and "
                      "shadow) at window (x, y) of the screen, y + lower_offset for blocks > 3 other than 6; line k at "
                      "(text_x, text_y + line_step * k), at most 3 a page, ink colour 0. _common_proc's Lord titles go in "
                      "the same window (its first line). The pictures under it: the ending's street (origin (160, 20)), "
                      "the banquet / cell windows (160, 32).")
    out["endmsg"] = {"site": "0xA8B2", "window": {"x": 160, "y": 68, "width": 312, "height": 52}, "lower_offset": 232,
                     "upper_blocks": [0, 1, 2, 3, 6], "text_x": 16, "text_y": 4, "line_step": 14, "lines": 3,
                     "fill": 15, "text_colour": 0,
                     "scene_y": {"p4.ending.street": 20, "p4.ending.banquet": 32, "p4.ending.cell": 32}}


if __name__ == "__main__":
    main()
