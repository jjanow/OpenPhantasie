#!/usr/bin/env python3
"""Build the Phantasie II Atari ST asset manifest + a T1.5 graphics-pack manifest draft (Phase 09 T3, task 4b).

Reads  reverse-engineering/atari-st/phantasie2/extracted/graphics/extraction-index.json (extract_st_p2_graphics.py)
       reverse-engineering/atari-st/phantasie2/extracted/{monsters,names}.json (extract_st_phantasie2.py)
Writes reverse-engineering/atari-st/phantasie2/extracted/graphics/asset-manifest.json
         (monster / race ids -> sprite paths, as reverse-engineering/amiga/extracted/asset-manifest.json)
       reverse-engineering/atari-st/phantasie2/extracted/graphics/pack.json
         (phantasie.graphics-pack/v1, the shape of GameData/phantasie1/graphics/amiga.json)
       src/OpenPhantasie.Data/GameData/phantasie2/graphics/atari-st.json
         (the same file, wired as II's graphics pack; a test pins the two together)
Run from the repo root: ``python3 -I tools/scripts/build_st_p2_asset_manifest.py``.
"""
from __future__ import annotations

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from extract_st_p2_common import DISK1, DISK2, EXTRACTED, REPO, rel  # noqa: E402
from st_prg import load_prg  # noqa: E402

GFX = EXTRACTED / "graphics"


# TOS's VDI pen -> hardware register map for 16 colours (low resolution).
VDI_TO_REGISTER = [0, 15, 1, 2, 4, 6, 3, 5, 7, 8, 9, 10, 12, 14, 11, 13]


def town_name_colours(town_palette: list[str]) -> dict[str, str]:
    """Town number -> banner ink: pen = town number except 3 -> 11 and 9 -> 13 (PHANT.PRG 0x4082); 0 = new game."""
    pens = {t: {3: 11, 9: 13}.get(t, t) for t in range(13)}
    return {str(t): town_palette[VDI_TO_REGISTER[pen]] for t, pen in pens.items()}


# PHANT.PRG 0xb6b0 (s135, FORMAT.md 7.2): the Dosound table 0x24a8e, image addresses. Every entry starts as 0x21ada.
ST_SOUND_DEFAULT = 0x21ADA
ST_SOUNDS = {n: ST_SOUND_DEFAULT for n in range(33) if n not in (1, 9, 27)}
ST_SOUNDS.update({0: 0x21BA6, 2: 0x21BD2, 3: 0x21A50, 4: 0x21A70, 5: 0x21A8C, 8: 0x21A7E, 10: 0x21BF2, 11: 0x21C0C,
                  14: 0x21C2A, 15: 0x21B0C, 16: 0x21AFC, 17: 0x21B44, 18: 0x21B2C, 19: 0x21AE8, 20: 0x21CA6,
                  21: 0x21C8E, 22: 0x21C70, 23: 0x21C5E, 24: 0x21CCA, 25: 0x21CDC, 26: 0x21D32, 28: 0x21D56,
                  29: 0x21D70, 30: 0x21D8A, 31: 0x21D9C, 32: 0x21DB8})
ST_FORM_CLICK = 33  # the pack's number for 0xb7e6 = Dosound(0x21ab0), not a table entry
ST_DEFAULT_PLAY = 34  # s139: the pack's number for 0xb7fa = 0xb8b0(0x21ada), the default script played straight
ST_SPARKLE_SOUND = 35  # s156: the pack's number for 0xb9ce = 0xb8b0(0x21c48), the sparkle's noise burst (FORMAT.md 7.3)
ST_SPARKLE_SCRIPT = 0x21C48
# s139 (FORMAT.md 7.4): the moments outside combat (Core FieldCues) -> the ST's sound. [C] static, traced live s140 / s141.
ST_FIELD_CUES = {
    "overworld.town_prompt": 19,      # 0x63e2, before "A TOWN. ENTER ?"
    "overworld.dungeon_prompt": 19,   # 0x106a0, before "A DUNGEON. ENTER ?"
    "dungeon.wall": ST_DEFAULT_PLAY,  # 0x10470 (0x1046c): "THAT IS A WALL", cell 0 or off the map
    "dungeon.disarm_fails": 29,       # 0x10e9c, before "%s FAILS"
    "dungeon.event.spell_cast": 28,   # 0x11d02, op 3
    "dungeon.event.party_moved": 16,  # 0x11dfc, op 12
    "dungeon.event.noise": 26,        # 0x11e88, op 14
    "town.transport": 22,             # 0x262c, TRANSPORTATION's pick (then the palette fade, transport_fade)
    "field.resurrected": 12,          # 0x92d8, after the box (12 = the default script's bytes)
    "town.cast_done": ST_DEFAULT_PLAY,  # 0xe5ec / 0xe7f2: the "D O N E !" alert's beep (0x1628 -> 0xb7fa)
}
# s140 (FORMAT.md 7.4, endgame.md 8): the defeat scene. 0x12694 saves the Speed word and sets 20, so each sound waits
# for the one before (0xb8f2), whatever the player picked.
ST_DEFEAT_SOUNDS = {
    "_note": "0x12678 sets Speed [0x1fefa] := 20 (restored at 0x127ae): every 0xb80e waits for the running sound. 0x12bea: "
             "'YOUR PARTY HAS BEEN DEFEATED' (0x12a16: the line, 3000 ms or a key), the ghost row (0x12b5e(0)), ghosts "
             "(0x12d5e), 'YOU HAVE ARRIVED AT THE ASTRAL PLANE' (3000 ms), door (0x12d86) then the door's 90 steps of 4 "
             "VBL, (5th defeat: the sign, 3000 ms), 3000 ms, demon (0x12e36), the face, 'THE GREAT DEMON APPEARS AND "
             "SPEAKS:'; then fate (0x126d2) before each member's fate, DESTROYED included. Traced live s141 (Hatari): 23 "
             "3060 ms after line 1 (ghosts_after_ms: the line's hold), 22 3090 ms after the astral line, the door 6.87 s "
             "(413 VBL at 60 Hz, drawing included), 20 3010 ms after it; each fate line follows its 21 within 15 ms, and "
             "21 waits for the running sound (20 ~5.3 s, 21 ~3.9 s).",
    "speed_ms": 20, "ghosts": 23, "door": 22, "demon": 20, "fate": 21, "ghosts_after_ms": 3000,
}
# s141 (FORMAT.md 7.4, capture_s141_sounds.md): a cue whose sound the ST plays after a timed box rather than before it.
# Field Resurrection: 0xa18c shows "%s IS RESURRECTED" (tree 40), holds Speed + 1000 ms (0xa16a; a key or a click ends the
# hold), redraws the party view (0x17210), then 0xb80e(12). Traced: 1005 + 355 ms at normal, 2005 + 355 ms at slow.
ST_CUE_DELAYS = {
    "field.resurrected": {
        "_note": "0x92ce 0xa18c: the box (shown ~95 ms after the call), its hold Speed + hold_ms (0x16dce -> 0x16cbe, a key "
                 "ends it), the party-view redraw 0x17210 (after_ms, measured 355 ms on Hatari s141 [~]), then sound 12 "
                 "(0x92d8). s165: box_palettes: the line shows in end_boxes' one-line box (tree 40 at (12, 75), 0xa124; "
                 "0x92c8 calls only 0xa18c, nothing on the message line), its pens (border 0, fill 3 = VDI 6, text 0) "
                 "in the palette the mode shows.",
        "hold_ms": 1000, "plus_speed": True, "after_ms": 355,
        "box_palettes": {"overworld": "map", "dungeon": "map"},
    },
}
TOWN_MODES = ["town", "guild", "title"]  # PHANT.RSC is loaded for the town (0xec36 -> 0x4706)
FIELD_MODES = ["overworld", "dungeon"]  # OUT.RSC is loaded for the field (0x572c)
# s140 (FORMAT.md 7.4): every alert that beeps first: 0x1628(n) (rsrc_gaddr(5, n), 0xb7fa, form_alert), 0x1668(text)
# (0xb7fa, form_alert) and 0xb7fa called straight before a form_alert, each caller read in the objdump. Keys are the
# remaster's string ids for the same moment; a line beeps in the modes listed. 0x1628(9) "D O N E !" after a town cast is
# the cue town.cast_done (not here). No remaster moment: 13 / 14 the printout questions (0x2962 / 0x296e / 0x3a58).
ST_ALERTS = [
    {"key": "core.guild.party_is_full", "modes": TOWN_MODES,
     "st": "PHANT.RSC 0 'There is no room': guild_add, six members (0xd500 -> 0x2356)"},
    {"key": "core.armory.not_enough_gold", "modes": TOWN_MODES,
     "st": "PHANT.RSC 1 'You can not afford it': shop_menu (0xc276 -> 0x15e0)"},
    {"key": "core.guild.learn_cant_afford", "modes": TOWN_MODES,
     "st": "PHANT.RSC 1: the guild's spell learning 0x974 (0x9d6)"},
    {"key": "core.guild.train_cant_afford", "modes": TOWN_MODES,
     "st": "PHANT.RSC 1: training's cost agreed, gold short (0x188a)"},
    {"key": "core.p2.you_are_improved", "modes": TOWN_MODES,
     "st": "PHANT.RSC 2 'You are improved': use_item (0xe2b4 -> 0x1616)"},
    {"key": "core.town.you_must_assemble_a_group", "modes": TOWN_MODES,
     "st": "PHANT.RSC 3 'You must assemble a group' (0xdc60 -> 0x28cc)"},
    {"key": "core.guild.learn_learned", "modes": TOWN_MODES,
     "st": "PHANT.RSC 4 'You've learned': 0x974 (0x9ea / 0x9f6)"},
    {"key": "core.guild.no_one_wise_enough", "modes": TOWN_MODES,
     "st": "PHANT.RSC 5 'No one is wise enough to teach You': guild_train (0xce22 -> 0x15f2)"},
    {"key": "core.guild.move_not_in_town", "modes": TOWN_MODES,
     "st": "PHANT.RSC 6 'Not in the town / it takes 2 months / to get here' YES / NO: guild_add (0xd51a -> 0x2366)"},
    {"key": "core.guild.learn_no_more", "modes": TOWN_MODES,
     "st": "PHANT.RSC 7 'You can't learn any more': 0x974 (0x98a)"},
    {"key": "core.magic.x_drinks_healing_x_x", "modes": ["town"],
     "st": "PHANT.RSC 9 'D O N E !': use_item, a healing potion drunk (0xe164 -> 0x15ce)"},
    {"key": "core.magic.x_drinks_magic_x_x", "modes": ["town"],
     "st": "PHANT.RSC 9 'D O N E !': use_item, a magic potion drunk (0xe1de -> 0x15ce)"},
    {"key": "core.p2.unusable", "modes": TOWN_MODES, "dialog_only": True,
     "st": "PHANT.RSC 10 'UNUSABLE': use_item (0xe07e -> 0x1604); only a line the Use item dialog returns (the Armory's "
           "I-rule 'UNUSABLE' after a buy has the same text and no ST alert)"},
    {"key": "core.guild.learn_failed", "modes": TOWN_MODES,
     "st": "PHANT.RSC 11 'You've failed': 0x974 (0x9f2 / 0x9f6)"},
    {"key": "core.armory.ninth_item", "modes": TOWN_MODES,
     "st": "PHANT.RSC 12 'He has nine items': shop_menu, a shopper with nine items (0xc24c -> 0x1586(0)); the box "
           "after the ninth buy (0x1586(1), 'He's just bought ninth item') has no beep"},
    {"key": "core.aging.x_has_died", "modes": TOWN_MODES,
     "st": "0x1668 '%8s has died': town_aging 0xba9a (0xbc14)"},
    {"key": "core.aging.x_has_aged", "modes": TOWN_MODES,
     "st": "0x1668 '%8s has aged': town_aging 0xba9a (0xbc4e)"},
    {"key": "core.guild.you_need_x_more_xp", "modes": TOWN_MODES,
     "st": "0xb7fa straight (0x1814), then form_alert 'You need %ld more experience points' (confirm_cost 0x17e4)"},
    {"key": "core.magic.not_enough_mp", "modes": FIELD_MODES,
     "st": "OUT.RSC 0 'Not enough Magic': the field cast (0x6fb8), magic +18 below the cost"},
]


def rsc_free_strings(path) -> list[str]:
    """A GEM .RSC file's free strings (rsrc_gaddr(R_STRING / 5, n)): header word 5 = rs_frstr, word 15 = rs_nstring."""
    import struct
    raw = path.read_bytes()
    header = struct.unpack(">18H", raw[:36])
    frstr, count = header[5], header[15]
    out = []
    for i in range(count):
        off = struct.unpack(">I", raw[frstr + 4 * i: frstr + 4 * i + 4])[0]
        out.append(raw[off: raw.index(bytes(1), off)].decode("latin-1"))
    return out


def prg_string(address: int) -> str:
    """A NUL-terminated string of PHANT.PRG at an image address (FORMAT.md 1)."""
    return load_prg(str(DISK1 / "PHANT.PRG")).cstr(address)


# s158: the box each alert line draws: the disks' own form_alert string (PHANT.RSC / OUT.RSC free string n, or the
# PHANT.PRG data string 0x1668 / 0x1814 pass). PHANT.RSC 6 (YES / NO, the Guild's move question) gets none: the remaster
# asks that question in its own dialog, so a box would not take the answer [~].
ST_ALERT_BOXES = {
    "core.guild.party_is_full": ("PHANT", 0),
    "core.armory.not_enough_gold": ("PHANT", 1),
    "core.guild.learn_cant_afford": ("PHANT", 1),
    "core.guild.train_cant_afford": ("PHANT", 1),
    "core.p2.you_are_improved": ("PHANT", 2),
    "core.town.you_must_assemble_a_group": ("PHANT", 3),
    "core.guild.learn_learned": ("PHANT", 4),
    "core.guild.no_one_wise_enough": ("PHANT", 5),
    "core.guild.learn_no_more": ("PHANT", 7),
    "core.magic.x_drinks_healing_x_x": ("PHANT", 9),
    "core.magic.x_drinks_magic_x_x": ("PHANT", 9),
    "core.p2.unusable": ("PHANT", 10),
    "core.guild.learn_failed": ("PHANT", 11),
    "core.armory.ninth_item": ("PHANT", 12),
    "core.aging.x_has_died": ("PRG", 0x229CA),  # 0xbc14: 0x1668(sprintf("%8s has died"))
    "core.aging.x_has_aged": ("PRG", 0x229EA),  # 0xbc4e
    "core.guild.you_need_x_more_xp": ("PRG", 0x1F572),  # 0x1814: form_alert("You need %ld more ...")
    "core.magic.not_enough_mp": ("OUT", 0),
}
# s158: cues that open a box: the town casts' "D O N E !" (0xe5ec / 0xe7f2 -> 0x15ce = 0x1628(9)).
ST_CUE_BOXES = {"town.cast_done": ("PHANT", 9)}
# s158: the boxes' look, measured on EmuTOS 1.4's form_alert in Hatari (TOS's AES and font are not on the disks) [~].
ST_ALERT_LOOK = {
    "_note": "AES form_alert, measured on EmuTOS 1.4 in Hatari (parity/shots/p2-endgame-wand-improved.png, "
             "p2-endgame-wand-unusable.png, p2-train-*.png; EmuTOS's form_alert comes from DRI's GEM AES as TOS's does) "
             "[~]: root box (widest line + 2) x (lines + 4) cells, its 1-px border inward, OUTLINED 3 px outside; centred "
             "in area (x, y, w, h); lines from cell (1, 1); one blank row, then the buttons (widest label + 1 cells "
             "each, button_gap cells apart, the row centred on whole cells), the default button's border 3 px outside. "
             "Colours: VDI 0 (fill) = register fill_pen, VDI 1 (lines, text) = register ink_pen of the palette the "
             "game mode shows. Font (s160): TOS's 8x8 is not on the disks; EmuTOS's 8x8 (the pack's fonts role "
             "p2.alert), one glyph per 8-px cell from its top-left, as the shots pixel for pixel. Only Return / Enter "
             "(the default button, EmuTOS fm_keybd) or a click on the button closes the box.",
    "cell": 8, "outline": 3, "button_border": 3, "button_gap": 2, "area": [0, 9, 320, 191],
    "fill_pen": VDI_TO_REGISTER[0], "ink_pen": VDI_TO_REGISTER[1],
    "palettes": {"town": "town", "guild": "town", "title": "town", "overworld": "map", "dungeon": "map"},
}


def alert_text(source: tuple[str, int]) -> str:
    kind, n = source
    if kind == "PRG":
        return prg_string(n)
    return rsc_free_strings((DISK2 / f"{kind}.RSC"))[n]


def alerts_with_boxes() -> list[dict]:
    out = []
    for line in ST_ALERTS:
        entry = dict(line)
        if line["key"] in ST_ALERT_BOXES:
            entry["box"] = alert_text(ST_ALERT_BOXES[line["key"]])
        out.append(entry)
    return out


# s140 (FORMAT.md 7.4): TRANSPORTATION's fade, 0x260a after sound 22: registers 0..15 := 0 one each out_vbl Vsyncs,
# hold_vbl Vsyncs, registers 0..15 := the town palette 0x23a62 one each in_vbl Vsyncs, then 0xb9e2 (Dosound(0x21a32), the
# silence script) ends the sound.
ST_TRANSPORT_FADE = {
    "_note": "0x260a: 0x4082(' ', 6, 15) blanks the message line, sound 22 (0xb80e), registers 0..15 := 0 (XBIOS 7 "
             "Setcolor) each followed by out_vbl Vsyncs (XBIOS 37), hold_vbl Vsyncs, registers 0..15 := palette (the town "
             "palette 0x23a62) each followed by in_vbl Vsyncs, then 0xb9e2: Dosound(0x21a32) silences sound 22 (385 "
             "ticks, 7.7 s, longer than the fade) and waits (0xb8f2). No key is read. Traced live s141 (Hatari, EmuTOS "
             "US = 60 Hz VBL, measured against the 200 Hz timer): out 160, hold 100, in 112 VBL, 6180 ms from the first "
             "blackout to the silence; 22 starts 2 VBL before the first blackout.",
    "palette": "town", "vbl_hz": 60, "out_vbl": 10, "hold_vbl": 100, "in_vbl": 7, "silences_sound": True,
}
# s136: the second player 0xb92a(count, body): body bytes up to its 0xff, count times, then the 30-byte reset 0x21a32.
ST_SOUND_TAIL = 0x21A32
ST_REPEATS = {1: (0x21BC0, 10), 9: (0x21B54, 10), 27: (0x21B66, 6)}


def side(sound: int, **extra) -> dict:
    return {"sound": sound} | extra


# s136 (FORMAT.md 7.3): each combat line's sound per the 27 0x18cc2 call sites, keyed by the remaster's string ids.
# "member" / "monster": whose line (the name in it); "any": a line without a name. sound -1 = none.
ST_COMBAT_LINES = {
    "core.combat.x_awakens": {"member": side(0), "monster": side(10)},
    "core.combat.party_awakens": {"any": side(0)},
    "core.combat.x_throws_a_rock": {"member": side(3, attack=True)},
    "core.combat.x_hits": {"member": side(3, attack=True), "monster": side(5, attack=True)},
    "core.combat.x_misses": {"member": side(4, attack=True), "monster": side(8, attack=True)},
    "core.combat.x_casts_a_spell": {"member": side(1, pose=True), "monster": side(9, hold=False, pose=True)},
    "core.combat.x_breathes": {"monster": side(13, pre=[9], pose=True)},
    "core.magic.x_is_healed": {"member": side(-1)},
    "core.magic.x_is_resurrected": {"member": side(12)},
    "core.combat.x_is_slept": {"member": side(14), "monster": side(11)},
    "core.combat.x_takes_x_damage": {"member": side(-1), "monster": side(-1)},
    "core.combat.x_dies": {"member": side(-1, death=True, hold=False), "monster": side(16, erase=True)},
    "core.combat.magic_drained": {"any": side(6)},
    "core.combat.a_stunning_blow": {"any": side(7)},
    "core.combat.monsters_are_charmed": {"any": side(-1)},
    "core.combat.monsters_start_dissolving": {"any": side(-1)},
    "core.combat.monsters_run": {"any": side(2)},
    "core.combat.armor_reduced": {"any": side(-1)},
    "core.combat.party_slowed": {"any": side(-1)},
}

# s137 / s138 (FORMAT.md 7.3a): the end-of-fight boxes, OUT.RSC trees 40 (one line, 0xa124) and 41 (two lines, 0xa1c2),
# shown by 0x16dce(tree, 12, 75, Speed + hold). Pens are registers of the combat palette: the root G_BOX ob_spec
# 0x00020076 = border 2 px inward in VDI 0 (register 0), solid fill VDI 6 (register 3); the G_TEXTs draw VDI 0 (register
# 0), transparent. Text cells are the objects' (column, row) in 8-px cells; each text is centred in 35 columns (0x17266).
ST_END_BOXES = {
    "_note": "0xa25a(text) -> 0xa124: tree 40, the text centred in center_columns (0x17266: floor((35 - len) / 2) "
             "spaces first) into object 1, then 0x16dce(40, 12, 75, Speed + one_line.hold_ms): the background saved, "
             "objc_draw, evnt_multi (a key, a click or the timer ends it), the combat screen redrawn (0x18446). "
             "0xa272(a, b) -> 0xa1c2: tree 41, a in object 1 (upper row), b in object 2 (lower row), Speed + "
             "two_line.hold_ms. pairs: [upper, lower] string ids shown as one two-line box (0xa3da: 'EACH SURVIVOR GETS' "
             "over '%ld XP AND %ld GP'; 0xa4fa: 'PLUTO'S MINION IS KILLED !' over '%s TAKES AN AMULET'; both seen live "
             "s82). outside_round_keys: lines that are boxes only outside a round (the morale check's 'MONSTERS RUN' "
             "0xa066; in a round the spell's line, sound 2). sounds: played before the box (0xb80e: 0xa05a plays 2 "
             "before 'MONSTERS RUN'); the other boxes have none. Font (s160): EmuTOS's 8x8 system font, the pack's "
             "fonts role p2.end_box, one glyph per 8-px cell from its top-left.",
    "palette": "combat", "x": 12, "y": 75, "cell": 8, "center_columns": 35,
    "border": 2, "border_pen": 0, "fill_pen": 3, "text_pen": 0,
    "one_line": {"columns": 38, "rows": 5, "text_cells": [[2, 2]], "hold_ms": 1000},
    "two_line": {"columns": 37, "rows": 6, "text_cells": [[1, 1], [1, 3]], "hold_ms": 1500},
    "keys": ["core.combat.monsters_take_gold", "core.combat.monsters_dont_respond", "core.combat.monsters_surrender",
             "core.combat.monsters_greet_you", "core.combat.monsters_killed"],
    "outside_round_keys": ["core.combat.monsters_run"],
    "pairs": [["core.combat.each_survivor_gets", "core.combat.x_xp_and_x_gp"],
              ["core.p2.plutos_minion_is_killed", "core.p2.x_takes_an_amulet"]],
    "sounds": {"core.combat.monsters_run": 2},
}


def main() -> int:
    index_path = GFX / "extraction-index.json"
    if not index_path.is_file():
        print(f"missing {rel(index_path)}; run extract_st_p2_graphics.py first", file=sys.stderr)
        return 1
    index = json.loads(index_path.read_text())
    monsters = json.loads((EXTRACTED / "monsters.json").read_text())
    races = json.loads((EXTRACTED / "names.json").read_text())["races"]

    assets, unmapped = [], []
    by_idx = {m["index"]: m for m in index["monsters"]}
    for m in monsters:
        sp = by_idx.get(m["index"])
        if sp is None:
            unmapped.append({"kind": "monster", "data_id": m["index"], "name": m["name"], "reason": "no sprite"})
            continue
        if sp["name"] != m["name"]:
            raise SystemExit(f"monster {m['index']}: sprite name {sp['name']!r} != table name {m['name']!r}")
        assets.append({"kind": "monster", "data_id": m["index"], "name": m["name"],
                       "sprites": {"idle": sp["idle"], "attacking": sp["attacking"]},
                       "size": {"width": sp["width"], "height": sp["height"]}})
    for r in index["races"]:
        entry = {"kind": "race", "data_id": r["index"], "name": r["name"],
                 "sprites": {"idle": r["idle"], "attacking": r["attacking"]}, "size": {"width": 32, "height": 32}}
        if r["index"] >= len(races) or races[r["index"]] != r["name"]:
            entry["note"] = "portrait slot without a race name in names.json"
        assets.append(entry)

    manifest = {
        "_schema": "phantasie.st-p2.asset-manifest/v1",
        "_generated_by": "tools/scripts/build_st_p2_asset_manifest.py",
        "_sources": [rel(index_path), rel(EXTRACTED / "monsters.json"), rel(EXTRACTED / "names.json")],
        "palette": "combat (monsters and portraits are drawn on the combat screen, palette PHANT.PRG 0x23a82)",
        "assets": assets,
        "unmapped": unmapped,
        "counts": {"monsters": sum(a["kind"] == "monster" for a in assets),
                   "races": sum(a["kind"] == "race" for a in assets)},
    }
    (GFX / "asset-manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")

    screens = {s["path"].split("/")[-1]: s["path"] for s in index["screens"]}
    pack = {
        "_schema": "phantasie.graphics-pack/v1",
        "_note": "Atari ST Phantasie II art (tools/scripts/extract_st_p2_graphics.py + build_st_p2_asset_manifest.py; "
                 "the same manifest as reverse-engineering/atari-st/phantasie2/extracted/graphics/pack.json, pinned by "
                 "a test). Paths are relative to root. The map layout is amiga-maps/v1-compatible (one icon cell per "
                 "kind); II draws several dungeon/castle icons per region, listed in maps.json icon_cells. Its "
                 "overworld patterns (maps.json patterns: pattern number = cell class, terrain.md section 5) become "
                 "overworld.pattern.N: the ST fill patterns that draw lava (8), mist (11) and dark voids (7), "
                 "D2-014. Text uses "
                 "the TOS system font (not on the disks), so no font file. Colour 0 is opaque in every sprite (the "
                 "game blits with vro_cpyfm replace). The ST switches between four palettes per screen (FORMAT.md "
                 "section 10.1, listed in palettes); every PNG already holds its own screen's colours, and the "
                 "remaster layout mixes pictures of several ST screens (portraits beside the map), so no palette is "
                 "forced on the frame (no top-level palette, as the Amiga pack). colour_cycle: the VBL routine "
                 "0x1382e rotates registers 4 -> 7 -> 14 of the map palette every 14 VBLs (byte 0x13863), installed "
                 "only when the wilderness loop starts (0x7410) and removed on town, dungeon, encounter and defeat "
                 "(0x137e6 callers), so only the overworld pictures shimmer (water, part of lava); vbl_hz 60: the US release on a US ST "
                 "(NTSC TOS, os_conf bit 0 clear; a PAL TOS would run 50; decided s109). The ST has no map legend.",
        "id": "atari-st",
        "game": "phantasie2",
        "platform": "atari-st",
        "name": "Atari ST",
        "auto_select": True,
        "root": rel(GFX),
        "requires": ["monsters"],
        "screen": {"width": 320, "height": 200, "pixel_aspect": [1, 1], "background": "#000000"},
        "palettes": {name: index["palettes"][name]["rgb"] for name in ("start", "town", "map", "combat")},
        "colour_cycle": {"palette": "map", "registers": index["palettes"]["map"]["colour_cycle"]["registers"],
                         "period_vbl": 14, "vbl_hz": 60,
                         "pictures": ["overworld.region.", "overworld.icon.", "overworld.party"]},
        "font": "remaster-5x7",
        "_fonts_note": "s160 (user, s159): the ST draws its alert boxes (AES form_alert) and end-of-fight boxes (OUT.RSC "
                       "trees 40 / 41, G_TEXT font 3) in the TOS ROM's 8x8 system font, which is not on the disks; the "
                       "look was measured on EmuTOS, so these places use EmuTOS's 8x8 font (fonts/emutos-st-8x8.json, "
                       "GPL-2.0-or-later, tools/scripts/emutos_st_font.py; matches the EmuTOS alert shots pixel for "
                       "pixel). Glyphs it lacks come from the pack's font. The ending's own fonts are in ending_scene.",
        "fonts": {"p2.alert": "emutos-st-8x8", "p2.end_box": "emutos-st-8x8"},
        "map_layout": {"format": "amiga-maps/v1", "file": "maps/maps.json"},
        # Town screen on PELNOR.PIC (s98, never crop scenes): the GEM bar rows 0..9, the name in the scroll (0x4082; x:
        # s109 live, the ink centre is 155.5-156 in all ten towns, as 0x4082 computes), a message on the black rows 192..199 (0x4082's message branch, y 192).
        # Name colour (s109 live, towns 1-10 + a new game, parity/shots/p2-town-*.png): 0x4082 sets vst_color to the
        # town number [0x25b94] (town 3 -> pen 11, town 9 -> pen 13), TOS's VDI pen -> register map picks a `town`
        # palette colour. Key 0 = a new game's first Pippacott (the town global still 0: pen 0, black).
        "town_screen": {"bar_rows": 10, "name_x": 156, "name_y": 24, "name_colour": "#000000", "message_y": 192,
                        "name_colours": town_name_colours(index["palettes"]["town"]["rgb"]),
                        # the new game's nudge: town global 0 -> "the rest" = 5, ink centre 151 (s109 live, as s78 read)
                        "fresh_name_x": 151},
        # The title splash (s102): START.PRG's boot, FORMAT.md section 10.2.
        "title_sequence": {
            "_note": "START.PRG (reverse-engineering/atari-st/phantasie2/FORMAT.md 10.2): black palette, Dosound(0x1b3) "
                     "starts the title tune (a YM2149 register script TOS plays at 50 Hz from Timer C, 285 waits, "
                     "3231 ticks = 64.6 s, the script [C], TOS's timing [~]; FORMAT.md 7.1), which runs on under both pictures, COVER1 to the "
                     "screen, the start palette, 251 Vsyncs (dbf "
                     "from 250), COVER2 over it at line 65 (title-credits.png is the composite), then Pexec PHANT.PRG "
                     "[C]. No key is read. The credits stay up while PHANT.PRG loads and then until the tune ends: PHANT.PRG's "
                     "start-up (0xec36) waits at 0xb8f2 (Speed 10 >= 9) on Dosound(-1) until the running script has "
                     "ended, and only then shows its 'insert your Data Disk' page [C] (s156, static + Hatari trace; "
                     "atari-st/phantasie2/parity/capture_s156_title.md). So the composite lasts until the tune's end "
                     "(until_tune_ends, counted from the tune's start) and the tune never plays into PHANT.PRG's "
                     "screens; in the remaster a key still ends a picture early. The VBL rate is the colour cycle's vbl_hz (60: the US release on a US ST's NTSC TOS, decided s109). s158: the first (blank) step is the 1.96 s START.PRG spends reading COVER1 on the black palette after Dosound has started the tune (Hatari's FDC, EmuTOS US: VBL 217 -> 335, capture_s156_title.md) [~] (emulated floppy timing).",
            "canvas": [320, 200],
            "background": "#000000",
            # s108: the Dosound script START.PRG passes (image 0x1b3 = file 0x1cf), played on the YM2149 model.
            "tune": {"format": "atari-st-dosound", "file": "../../unpacked/disk1/START.PRG", "offset": 463,
                     "clock_hz": 2000000},
            "steps": [
                {"blank": True, "seconds": 1.96},  # s158: COVER1 read on the black palette, the tune running
                {"picture": "title.cover", "vbl": 251},
                {"picture": "title.credits", "until_tune_ends": True},  # s156: PHANT.PRG waits at 0xb8f2
            ],
        },
        # s135: PHANT.PRG's sound table 0x24a8e (filled by 0xb6b0; FORMAT.md 7.2): sound n -> its Dosound script
        # (file offset = image + 0x1c). s136: 1, 9 and 27 (the second player 0xb92a) as repeats.
        "sound_effects": {
            "_note": "0xb80e(n): YM silenced (0xba02), then XBIOS 32 Dosound(table[n]); TOS plays it at 50 Hz, the game "
                     "goes on (at Speed >= 9 the next sound waits for it, 0xb8f2). 33 is no table entry: 0xb7e6's fixed script "
                     "0x21ab0, a click (24 callers, among them the OK form 0xae1a). repeats (s136): 0xb92a builds the script "
                     "from the body's bytes up to its 0xff, count times, then the tail's 30 bytes (0x21a32). 34 (s139) is "
                     "no table entry either: 0xb7fa = 0xb8b0(0x21ada), the default script played straight (the wall, every "
                     "alert's beep; FORMAT.md 7.4). 35 (s156): 0xb9ce = 0xb8b0(0x21c48), the spell sparkle's noise "
                     "burst (combat_lines.sparkle; 0xb9e2 silences it after the sprite's 300 ms). Wired: the ending's sound 32 and its pages' click, the combat lines "
                     "(combat_lines), the moments outside combat (field_sounds).",
            "format": "atari-st-dosound", "file": "../../unpacked/disk1/PHANT.PRG", "clock_hz": 2000000,
            "sounds": {str(n): [f"{a + 0x1c:#x}"] for n, a in ST_SOUNDS.items()}
                      | {str(ST_FORM_CLICK): [f"{0x21AB0 + 0x1c:#x}"]}
                      | {str(ST_DEFAULT_PLAY): [f"{ST_SOUND_DEFAULT + 0x1c:#x}"]}
                      | {str(ST_SPARKLE_SOUND): [f"{ST_SPARKLE_SCRIPT + 0x1c:#x}"]},
            "repeats": {str(n): {"body": f"{b + 0x1c:#x}", "count": c, "tail": f"{ST_SOUND_TAIL + 0x1c:#x}",
                                 "tail_length": 30} for n, (b, c) in ST_REPEATS.items()},
        },
        # s136: the combat round line by line (FORMAT.md 7.3): each line's sound, the Speed menu's ms, the member's death.
        "combat_lines": {
            "_note": "0x18cc2(text, n): sound n (n >= 0; at Speed >= sound_wait_speed it first waits for the running sound, "
                     "unless the last was no_wait_after), the text, then Speed ms unless the text has short_text characters "
                     "or fewer or hold is false (a key ends the hold). attack (s156, 0x186a8 = pose mode 1): the actor's "
                     "attacking picture for attack_pose_ms first (no key, every Speed), then the idle one (a member's swing "
                     "0x86fe and rock 0x7748, a monster's swing 0x8ac4; a rock's own MISSES has none). pose (mode 2, "
                     "0x18f7a): the actor's silhouette (silhouette_pens of the combat palette: the attacking picture's "
                     "shape in the first, the rest of its rectangle in the second) pose_ms when Speed > pose_speed, else "
                     "pose_fast_ms, then the attack pose (the member's cast 0x79fc, the monster's special 0x8d8c). erase "
                     "(mode 0, 0x9b06): the monster's picture cleared before its line. pre: sounds played first without a "
                     "line (a breath: the special's blank line, sound 9). death: the member's mark, death.sound, "
                     "death.hold_ms, wait for the sound, the row re-laid (0x18a02). round_end_sound: 0x18416 at each of "
                     "combat_round's seven exits, after the round's last line and before combat_victory's boxes. sparkle: "
                     "0x18b16 before a damage spell's (or a rock's) damage line on a monster, amount / per + 1 times: "
                     "sound, the sprite (spell: amount >= 0; rock: the rock's damage, negative) at a random 16x16 spot "
                     "inside the monster's picture (x + rand(w - 16), y + rand(h - 16)), hold_ms (no key), the sound "
                     "silenced (0xb9e2); rock_miss: 0x18c6c after a rock's MISSES, the rock sprite at the screen spot, "
                     "hold_ms, no sound; rock_miss_area: the ST's monster area (x, y, w, h) the spot lies in (s163). Lines not listed: no sound [?], the Speed hold. monster_slots (s162, 0x18288 / "
                     "0x18856): a picture per monster, each group a row of its own monsters; the poses, sparkles and "
                     "erases on the monster's own slot, an erased slot keeping its place until the round's re-rank.",
            "speed_ms": {"fast": 5, "normal": 10, "slow": 1000},
            "sound_wait_speed": 9, "no_wait_after": 18, "short_text": 3,
            "pose_ms": 300, "pose_fast_ms": 10, "pose_speed": 9, "attack_pose_ms": 300, "silhouette_pens": [15, 0],
            "monster_slots": True, "fight_start_sound": 17, "round_end_sound": 18,
            "sparkle": {"sound": ST_SPARKLE_SOUND, "spell": "combat.sparkle", "rock": "combat.rock", "per": 32,
                        "size": 16, "hold_ms": 300, "rock_miss": [100, 10],
                        # s163: the ST's monster area for the miss spot (0x18458 clears from y 13; groups stack up
                        # from [0x23eb4] - 3 = 147); the remaster moves the spot by the same fraction onto its own.
                        "rock_miss_area": [0, 13, 320, 134]},
            "death": {"sound": 15, "hold_ms": 500},
            "lines": ST_COMBAT_LINES,
            "end_boxes": ST_END_BOXES,
        },
        # s139: the moments outside combat (FORMAT.md 7.4) -> sound; each goes through 0xb8f2 first.
        "field_sounds": {
            "_note": "Core's FieldCues moment -> sound of sound_effects (FORMAT.md 7.4, [C] static; traced live in Hatari s140 "
                     "(capture_s140_sounds.md)). Each play goes through 0xb8f2: at Speed >= wait_speed_ms (normal 10 / slow 1000) it waits "
                     "for the running sound, at fast (5) it cuts it off. defeat_scene (s140): the defeat scene's sounds, "
                     "at its own Speed. alerts (s140): the boxes 0x1628 / 0x1668 that beep first. click (s140, s141): "
                     "0xb7e6 after a GEM pick: town menu-bar picks (0x294), the Guild's menu picks (0x4d2 -> 0x56a), a "
                     "list window's exit (0x1964a / form_do sites), pick_member's cancel (0x674), the transport picker "
                     "(0x25b4) and every key or command of the field loop (0x16fea -> 0x1719a). cue_delays (s141): a "
                     "cue whose sound follows a timed box. 0x42d4 (0xb9b4) has no caller.",
            "wait_speed_ms": 9,
            "cues": ST_FIELD_CUES,
            "cue_delays": ST_CUE_DELAYS,
            "defeat_scene": ST_DEFEAT_SOUNDS,
            "alerts": {"_note": "0xb7fa (the default script, 34) before the box; a key matches a shown line from its start, "
                                "in the modes listed. Matched against the remaster's lines [~]. s158: box = the disks' "
                                "form_alert string drawn for the line (its printf holes filled from the line's own "
                                "values, in turn); cue_boxes: a cue's box; look: how the boxes are drawn.",
                       "sound": ST_DEFAULT_PLAY, "lines": alerts_with_boxes(),
                       "cue_boxes": {cue: alert_text(src) for cue, src in ST_CUE_BOXES.items()},
                       "look": ST_ALERT_LOOK},
            "click": ST_FORM_CLICK,
        },
        "transport_fade": ST_TRANSPORT_FADE,
        "pictures": {
            "title.cover": screens["cover1.png"],
            "title.credits": screens["title-credits.png"],
            "town.backdrop": screens["pelnor.png"],
            "ui.decor": screens["decor.png"],
            # The defeat sequence on DECOR.PIC (0x12bea, FORMAT.md section 10.4): door at (123,14), demon face at
            # (123,19), the 5th-defeat sign at (139,39).
            "decor.door": "decor/door.png",
            "decor.demon-face": "decor/demon-face.png",
            "decor.sorry-sign": "decor/sorry-sign.png",
            # FORMAT.md section 10.3 (s95): the ghost portrait that replaces every member's portrait in the defeat
            # scene (0x12b5e) and the mark on a member dying in combat (0x188fc mode 0, at portrait + (8,13)).
            "portrait.ghost": "ui/empty-portrait.png",
            "portrait.death-mark": "ui/ghost-mark.png",
            # FORMAT.md 7.3 (s156): the line-A sprites of 0x18b16 / 0x18c6c (RGBA: unset pixels transparent).
            "combat.sparkle": "combat/sparkle.png",
            "combat.rock": "combat/rock.png",
            "ending.final": screens["final2.png"],
            # crowd.pic: 3 frames of 235x31 blitted at (40,127) over final2.pic (FORMAT.md 10.2, endgame.md 7.1).
            "ending.crowd.0": "screens/crowd-0.png",
            "ending.crowd.1": "screens/crowd-1.png",
            "ending.crowd.2": "screens/crowd-2.png",
        },
        # Filmon's ceremony (s128 static, endgame.md 7.1; the s80 live shots): the I Amiga victory screen's layout with
        # II's optional parts.
        "ending_scene": {
            "_note": "0x12f2a: intro page (10 lines by v_gtext at (2, 50 + 10i), VDI 3 = register 2 of the map palette "
                     "the dungeon left, the OK form until OK; s160: italic 8x8 = EmuTOS's font in VDI italic (intro_page.font, "
                     "font_dx -4: top-aligned skewed text starts left_offset + right_offset px left of the point, EmuTOS "
                     "vdi_text.c), the OK label (G_BOXTEXT te_font 5) in EmuTOS's 6x6 small font, button_font, s162: the "
                     "s80 shot's O at (277, 184) pixel for pixel); final2.pic on "
                     "black; registers 0..15 := combat, 10 Vsync each (vbl_hz 60, as the colour cycle); bottom lines "
                     "(0x12a16: v_justified centred on 160, y 192, 310 wide, VDI 5 = register 6, plain 8x8 = font; spacing "
                     "EmuTOS's vdi_v_justified, s160; each 3000 ms or a key) "
                     "'Filmon speaks:', 'Presenting ...'; evnt_timer 2000; per record 0..n-1 (n = OK members): its idle "
                     "portrait on the door spot (150,75), its title line, the other pose (PARTY2), sound 32 (s135: "
                     "Dosound, not waited for), crowd frames 0/1 x10 at (40,127) 330 ms apart, its portrait in row slot d7 (y 158, I's gap "
                     "rule) and crowd frame 2; the door spot cleared; the wand lines and a blank line; closing page (21 "
                     "lines at (2, 4 + 8i), VDI 4 = register 4, OK form); registers 15..0 := 0, 10 Vsync each.",
            "palette": index["palettes"]["combat"]["rgb"],
            "vbl_hz": 60, "fade_vbl": 10, "tick_hz": 1000, "hold_ticks": 2000,
            "row_top": 158, "slot_size": 32, "grant_ticks": 0, "row_by_record": True,
            "stage": [150, 75],
            "crowd": {"frames": ["ending.crowd.0", "ending.crowd.1", "ending.crowd.2"], "x": 40, "y": 127,
                      "flashes": 10, "flash_ticks": 330, "sound": 32},
            "font": "emutos-st-8x8",
            "text_y": 192, "text_pen": 6, "line_ticks": 3000, "justify_width": 310,
            "intro_page": {"palette": index["palettes"]["map"]["rgb"], "x": 2, "y": 50, "line_gap": 10,
                           "text_pen": 2, "button_pen": 15, "sound": ST_FORM_CLICK,
                           "font": "emutos-st-8x8-italic", "font_dx": -4, "button_font": "emutos-st-6x6"},
            "closing_page": {"x": 2, "y": 4, "line_gap": 8, "text_pen": 4, "button_pen": 15, "sound": ST_FORM_CLICK,
                             "font": "emutos-st-8x8-italic", "font_dx": -4, "button_font": "emutos-st-6x6"},
        },
        "picture_sets": [
            {"name": "monster.{n}.idle", "dir": "monsters/idle", "prefix": "{n:D2}-", "ext": ".png"},
            {"name": "monster.{n}.attacking", "dir": "monsters/attacking", "prefix": "{n:D2}-", "ext": ".png"},
            {"name": "portrait.{n}.idle", "dir": "portraits/idle", "prefix": "{n:D2}-", "ext": ".png"},
            {"name": "portrait.{n}.attacking", "dir": "portraits/attacking", "prefix": "{n:D2}-", "ext": ".png"},
        ],
    }
    text = json.dumps(pack, indent=2, ensure_ascii=False) + "\n"
    wired = REPO / "src" / "OpenPhantasie.Data" / "GameData" / "phantasie2" / "graphics" / "atari-st.json"
    (GFX / "pack.json").write_text(text)
    wired.write_text(text)
    print(f"wrote {rel(GFX / 'asset-manifest.json')} ({manifest['counts']}), {rel(GFX / 'pack.json')} and {rel(wired)}")
    assert (REPO / pack["root"]).is_dir()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
