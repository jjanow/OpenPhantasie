# Phantasie II (Commodore 64 v1.2): pictures and the `c64` art pack

How II's C64 pictures are stored and drawn, and how the `p2-c64` import turns them into the `c64` graphics pack (s219).
II runs C64 I's engine, so this file gives only what differs from or adds to `../phantasie1/GRAPHICS.md` (the memory map,
the sprite-band record, the monster figure rule, the town and dungeon screens). Files and tables: `FORMAT.md` (section 6
points here). Tags as there: `[C]` read in the 6502 code or the BASIC, or seen on the live screen and checked against the
files; `[~]` probable; `[?]` not known.

**Evidence (s219).** Byte compares of II's v1.2 files against I's v1.4; II's `UTIL-3.O` map routine disassembled
(`$C5C3`–`$C6A3`); `B` listed with `petcat`. Live: VICE 3.10 `x64sc` (flatpak, on Xvfb, sound off) running the C64PP
v1.2 never_used pair (section 7), and the remaster's player build with only the C64 import
(`parity/s219-p2-c64-art-live.md`).

## 1. What II shares with I `[C]`

Byte for byte equal to C64 I v1.4's files: `FONT+HER` (the mixed-case font, the hero band, the map and dungeon party
sprites, the town's character set), `HSPRIT` (the 30 hero blocks) and the nine side-1 item pictures (`SHIELD` …
`GOOD TREASURE`); side 2's `DOOR`, `TRAP`, `STAIRS`, `LOCK`, `EXIT` are the same files as I's. So II's portraits, town
buildings, map sprites, dungeon party and item / feature pictures are I's pictures.

`FONT` differs from I's in eight characters only: `$48`–`$4B` (the map's classes 4 and 5), `$50`, `$51` and `$70`,
`$71` (not printed by the map routine). The dungeon characters `$E6`–`$EC` and `$20` are I's.

Own to II: the 80 `MX n` monster files, the title (`PIC.*`), `UTIL-3.O`'s map tables (section 3), the ending
(`PH2.END-4`, `TLUM1`) and the party-wipe screen's files (`SKEL2`, `CSPRIT`, `FONTO`).

## 2. Monsters `[C]`

`MX n` = monster n − 1, I's layout (I's GRAPHICS.md 2: the 38-byte band record + k blocks, k = `MN` field 13 + 1) and
I's figure rule (one monster = sprites 0 .. j − 1 up to the first repeated block index `[~]`). The pack takes side 1's
files: side 2's `MX 30` (COBRA), `MX 37` (DEVIL) and `MX 62` (HIGH DEVIL) differ only in band bytes (sprite colours of
sprites 3–7, block offsets of sprites 5 / 7, the y-expand mask), which sprites past the first figure use or not `[~]`.
Sizes: 24 × 21 up to 144 × 84 (PLUTO'S GIANT CONSTR., FIRE ELEMENTAL, PLUTO'S DRAGON DEMON).

**Attacking** (live, section 7): a one-sprite monster's block goes 0 → 1 → 0 while it attacks (BANTIR) `[C]`; VIPER (2
sprites, 3 blocks) went from blocks 0 1 to 2 1: the extra block replaces **sprite 0** `[C]`. The pack's
`monster.N.attacking` = the figure with sprite 0 showing block j (j = the figure's sprite count), for all 80; for the 30
several-sprite monsters other than VIPER the rule is VIPER's `[~]`. In all 80 files (and I's 80) the figure's sprites
show blocks 0 … j − 1 and the file holds j + 1 blocks, so block j is always the one spare block (checked s219).

## 3. The overworld map `[C]`

II's `UTIL-3.O` (2,291 bytes; I's is 2,281) has I's routine at `$C5C3` (`SYS 49152 + 63`), rearranged (the row / column
step comes before the cell is printed, so the sprite y no longer reads the previous row) with its own tables:

| Class (byte div 10) | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 (fog, ≥ 120) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| colour `$C68A` | 5 | 5 | 9 | 9 | **12** | **7** | 11 | 0 | 9 | 9 | 6 | 6 | 0 |
| PETSCII `$C697` | `$60` | `$62` | `$64` | `$66` | `$68` | `$6A` | `$6C` | `$6E` | **`$74`** | `$72` | `$74` | **`$72`** | `$20` |

(bold: differs from I's). `OUTDOORS2` names the classes in a DATA list: GRASS, FOREST, HILLS, MOUNTAINS, BUILDINGS,
DUNGEON, PATHWAY, NETHERWORLD, LAVA, DESERT, WATER, MIST, ASTRAL PLANE (0–12). As in I, the text column's bit 0 is OR'd into the character (a pair of characters by
column parity), the region's 26 × 20 cells are printed whole (text columns 26 … 1, rows 5 … 24; cell (x, y) of the
buffer `$9448`, index (25 − x) · 20 + y, at text column x + 1, row y + 5, live). LAVA (8) and WATER (10) print the same
character pair in brown and blue, DESERT (9) and MIST (11) likewise. NETHERWORLD (7) prints in black; BUILDINGS (4) in
grey, DUNGEON (5) in yellow, where I printed both black. The water characters 84 / 85 drift (live: `UTIL-3.O` `$C6E0`,
from the raster interrupt every 4 frames, rotates their rows right by one bit); the pack keeps the file's frame.

**Map sprites:** byte 40 → sprite 1 (town), 41 → sprite 2 (inn), and **only byte 50** → sprite 3 (dungeon); I's routine
takes 50–59 (by the code; region 1 has only byte 50, so the live screen could not tell them apart). x = 8 · column + 20,
y = 8 · row + 50 (VIC; the cell's top left minus 4 pixels in x, as I's); blocks 93 / 94 / 95 in yellow (7), the party
block 92 in white (1), the blocks of `FONT+HER` (I's); all live on region 1 (town at text (17, 8), inn (13, 5), dungeon
(11, 7)). The rest of the C64's map screen (a cyan prompt box in rows 1–3, the grey frame, the command menu, a brown
panel with a small locator box, the terrain's name at row 22) is the Client's own.

The pack: tile b for even map columns, b + 256 for odd ones (`odd_column_tile`), the three sprites as `overlays`
(dungeon on 50 only), the party at its own size, offset (−4, 0); the Client's box holds 19 × 18 of the cells.

## 4. Title `[C]`

`B` (v1.2) first prints a credits text screen (STRATEGIC SIMULATIONS INCORPORATED PROUDLY PRESENTS / PHANTASIE II /
Copyright 1986 by W.D.Wood / Commodore 64 Version 1.2; lines 590–670, held at least 5 s, live), then lines 185–290:
`PIC.SCR` → `$8400`, `PIC.CLR` → colour RAM, `PIC.MAP` → `$A000`, multicolour bitmap in VIC bank
2, `$D021` = 0 (line 595, run first through line 110's `GOSUB 590`). I's decoder, I's layout: 160 × 200 drawn 320 × 200.
Then `UTIL-3.O` and the fonts load and the text screen "1 - Start in Pippacott / 2 - Continue saved game" follows.

## 5. Town and dungeon (live)

**Town:** the six buildings at I's places, sizes, characters and colours (I's GRAPHICS.md 4), numbered down each column;
none added or dropped. `$D018` = `$97` (the town set at `$D800`), `$DD00` bank 3, border 15, background 0. The ENTER
arrow is sprite 0 (block `$29` at `$CA40`, red, x-expanded). The party rows (grey) and the right panel (the building's
name, the town's name) are the Client's own.

**Dungeon:** cell (x, y) of the grid at `$91B4` + 33 y + x at text column x, row y + 3, all in grey (12), rows 0–2 the
message bar. Seen: wall 0 → `$E6`, 15 → `$E7`, exit 215 → `$EB`, unseen → `$20`; with poked cells 235 → `$E8`,
200 / 209 / 213 → `$E9`, 185 → `$EA`, 218 / 220 → `$EC`: DOS's ranges, as the pack has them. The party: sprite 0, block 91,
yellow, on its cell (x = 8 · column + 24: no offset). `$D018` = `$9B` (`FONT` at `$E800`) on the map, in fights and in
the dungeon.

**Fights:** the hero band at `$CFDA` (I's record); with two members slots 3 and 4, both yellow (7), also while acting
(the light-blue case of a bigger party not seen); the attacker's block went `$0C` → `$0D` (its race's pair).

## 6. The `c64` pack (`GameData/phantasie2/graphics/c64.json`)

Written by the `p2-c64` import (recipe 3 since s221, `P2C64Art`, `P2C64ArtManifest`; the steps shared with I in
`C64PhantasieArt`, the manifest in `C64PhantasieArtManifest`) under `reverse-engineering/c64/phantasie2/extracted/graphics`
(263 PNGs since s221 with `ending/` and `defeat/`, section 8.3; the repo's extract of the C64PP v1.2 never_used pair; the held `.d64` pair gives the same pictures). In
`index.json`'s `default_order` after the ST and the Apple II: drawn when neither of those copies is imported. Slots and
fallbacks as I's pack (I's GRAPHICS.md 8). The item / feature pictures (s221): I's files, I's rule and colours (I's
GRAPHICS.md 7: `DUNGEON2` / `OUTDOORS2` run the same bytecode; finds by `(id − 1) / 20` on II's ids, so a weapon 61–80
shows `GOOD WEAPON`, 81–100 `MG WEAPON`; TRAP 10, STAIRS 12, LOCK 9, EXIT 5; DOOR never shown), drawn through the
same `event.*` slots and `find_pictures` table; sizes EXIT 8 × 6 characters, the others 7 × 7. s221: the ending
(`ending.final` = the hall, `ending.crowd.0` / `.1` = TLUM1's crowd, with `ending_scene`: Filmon's intro and closing
pages, the row by record, the lines in the panel) and the party wipe through II's defeat scene (`ui.decor` = the temple,
`decor.demon-face` = the face's doorway, `portrait.ghost` = the ghost); section 8.3, live in
`parity/s221-c64-end-screens-live.md`. s223: the party wipe runs as `CEMETARY2` under ORIGINAL SCREENS (`c64_defeat`,
I's GRAPHICS.md 8 / 9.4: the rising door `defeat.door`, the face `defeat.face`, the fates' colours; `defeat.temple` /
`defeat.ghost` added for it); the ending's row is `WIN`'s (all members from the start at x 86 + 24 k, `row_left`; the
presented one in its attacking pose in light red, `present_colour` 10); the intro page black on yellow without an OK
form; `parity/s223-c64-endings-live.md`. **Falls back:** `decor.door` (II's own defeat scene, ORIGINAL SCREENS off),
`decor.sorry-sign` (none on the C64), UNDEAD's portrait, `combat.*`; II's own drawing for those.

`[?]` left: `DOOR`'s colour (never shown); the hero colour in a party of more than two; the code that prints the town and
the dungeon map not read. (s223: the panel's text is cyan on `PH2.END-4`'s inverted characters, so black on cyan as
drawn, section 8.1.) `[~]`: the
attack rule for the 29 several-sprite monsters not watched.

## 7. Live check in VICE (s219)

VICE 3.10 `x64sc` (flatpak, Xvfb, sound off, remote monitor) on writable copies of the C64PP v1.2 never_used pair. RAM
(64 K) and I/O (`$D000`–`$DFFF` with colour RAM) dumps on: the town (no party, then a two-member party; `FONT+HER` at
`$CFDA`, `FONT` at `$E800` and `HSPRIT` at `$F000` byte-equal to the files), region 1 at Pippacott (all 520 map cells
equal the routine's output), two fights (BANTIR × 3 at `$CB5A`; VIPER, by poking DNG 1's forced-fight entry 0 from 9
(KOBOLD) to 6, band records polled while they attacked), DNG 1 (natural cells, then cells next to the party poked to
213 / 235 / 218 / 200 / 209 / 185 / 220) and the EXIT picture. The dumps and snapshots are not kept in the repo.

## 8. Ending and defeat screens (s221)

Live in VICE 3.10 (as section 7) on the v1.2 never_used pair, from a poked town state (LOUIS and THUNDAR copied into the
party slots, the last slot's index at `SA + 1203` = 1), `TOWN2`'s `OUTDOORS2` load redirected to `WIN` / `CEMETARY2`; RAM +
I/O dumps not kept. I's GRAPHICS.md section 9 has the method and the engine's details. This settles section 6's
`PH2.END-4`, `TLUM1` and `CEMETARY2` `[?]` items; the slots are wired since s221 (section 6).

### 8.1 The ending: `WIN`, `PH2.END-4`, `TLUM1` `[C]`

`DUNGEON2` loads `WIN` (side 2; `DUNGEON2` also still names `P.END`, which II's disks do not hold). `WIN`:

1. A text page ("THE PARTY IS SUDDENLY TELEPORTED INTO A SMALL ROOM. FILMON THE SAGE …"; s223: `GOSUB 0x84a` sets the
   background `$D021` to yellow 7 and prints in black, border black). No key: it stays while `PH2.END-4` and `TLUM1` load
   and a `FOR U = 1 TO 2000` runs (s223). The red ENTER arrow on it in the live run was the town's sprite left on by the
   redirect, not part of the screen.
2. `PH2.END-4` (4,095 bytes, loaded to `$8000`): bytes 0–999 → colour RAM, 1,024–2,046 → screen `$E400` (1,023 bytes),
   2,048–4,094 → character set `$E800` (2,047 bytes); hires text, background black. **The hall**: a grey (12) pediment
   and two pairs of columns on a grey floor (rows 1–12) and a cyan (3) text panel, rows 20–23.
3. `TLUM1` (load address `$1FDA`: an `MX`-style band record, I's section 2, + 15 blocks) goes to `$CBDA` and is added as
   a band (`SYS Q,A2,A1,0`); `WIN` then pokes record byte 13 := 47, `$D010` := 64 (sprite 6 at x 303) and the enable
   mask := `$7F`. **The crowd**: sprites 0–6, x- and y-expanded, green (5), y 160 (screen rows 110–151), x 15, 63, 111,
   159, 207, 255, 303; blocks `$31 $30 $32 $33 $35 $34 $36` and, for the second frame, the same + 8 (`$39` …): two
   frames alternating under "[original text]" (the ST has three crowd pictures).
4. The party in the hall: the hero band `$CFDA` (I's record) at y 125, x 110 + 24 k, yellow; the member being presented
   in light red (10) with its attacking block while the panel names it ("LOUIS / THE MIGHTY ONE / FROM FERRONRAH": one
   of the ten epithets and three homelands in `WIN`'s DATA). Then "FILMON SPEAKS - 'PRESENTING THE HEROES OF FERRONRAH'",
   the wands of Filmon, "INSERT BOOT DISK AND PRESS A KEY", `TOWN2`.

**s223, `WIN` step by step** (compiled BASIC, code `0xb9`–`0xe3d`, variables from `0xe44`; I's GRAPHICS.md 9.4 has the
opcodes) `[C]`:

1. The text page above (yellow background, black text), no key.
2. `PH2.END-4` copied in (colour RAM, screen, characters), screen on; `POKE 646,3`: the panel's text is printed in cyan
   (3). `PH2.END-4`'s characters are drawn inverted (character 32 is solid, each letter a hole), so the text shows as black
   letters on the cyan panel: the s221 `[?]` "the colour `WIN` prints the panel's text in" is settled. `FOR U = 1 TO 2000`.
3. `TLUM1` added as a band; the hero band set up: slots 0–5 at x 110 + 24 k, y 125, colour 7, pointer `P(168)` (I's
   GRAPHICS.md 9.1: the member's own idle figure), the enable mask 2^(PO + 1) − 1, so **every member stands in the row
   from the start** (in slot order, whatever its status). `SYS SY+57` starts the band IRQ.
4. "FILMON SPEAKS -" / " 'PRESENTING THE HEROES OF FERRONRAH'" in the panel; `FOR U = 1 TO 4000`; `GOSUB 0x6ba`, the cheer.
5. For each slot m = 0 … PO: `FOR I = 1 TO 4000`; member m's pointer + 1 (its attacking block) and colour 10 (light red);
   `GOSUB 0x9fb`: the panel cleared and the name (row 20), "THE <one of ten epithets, RND> ONE" (row 21) and "FROM
   <homeland by P(40)>" (row 22), centred, then `FOR U = 1 TO 1000`; `FOR I = 1 TO 4000`; the cheer; the pointer − 1 and
   colour 7 again.
6. The cheer (`0x6ba`): the panel cleared, "[original text]" (column 10, row 21); the crowd's pointers + 8 (the second
   frame); `GOSUB 0x749`, the SID cheer (two pitch sweeps with `FOR T = 1 TO 400` between and a fade with `FOR M = 1 TO
   500` per volume step, 16 steps); the pointers − 8 (the first frame again); the panel cleared. One frame change a cheer.
7. "FILMON SPEAKS -" / "'THEY WILL EACH RECEIVE A WAND OF FILMON WHICH HAS GREAT POWER BUT CAN ONLY BE USED ONCE' (PRESS
   RET)", until a key; `SYS SY+60,0`; the closing page (22 lines + "(PRESS RETURN)", printed in yellow 7 with the hall's inverted characters still loaded, so black letters on yellow cells, VICE s223) until a key;
   "INSERT BOOT DISK AND PRESS A KEY"; `TOWN2`.

The busy loops' times are in I's GRAPHICS.md 9.5.

**s224, the rest of the program** (read and checked in VICE, I's 9.5) `[C]`:

- The start (`0xe6`–`0x352`): the intro page (`0x84a`) while `PH2.END-4` loads; the screen off, the hall copied in, the
  screen on, `POKE 646,3`, `FOR U = 1 TO 2000`; **then** `TLUM1` loads (`SY+36`) and the bands are set up and started
  (`0x352`–`0x388`). So the hall stands alone, no crowd and no party, for the `FOR 2000` and the load (1.74 + 7.75 s in
  VICE, true drive `[~]`); the crowd's first frame and the whole row then appear together, with the header.
- The header: `SYS Q,2,20,"FILMON SPEAKS -"`, then `PRINT " 'PRESENTING THE HEROES OF FERRONRAH'"`, which goes to the
  next row at column 0 (VICE: the wands prompt's rows, printed the same way, start at column 0).
- Each slot 0 … PO is presented (`0x3e9`): name, "THE " V$ " ONE" and R$ at columns 20 − LEN / 2, 16 − LEN(V$) / 2 and
  20 − LEN / 2, rows 20–22 (`0x9fb`), V$ one of the ten words by `RND`, R$ the homeland by `P(40)`; the member stays light red
  through the `FOR 1000`, the `FOR 4000` and its cheer.
- The cheer (`0x6ba`): the panel cleared (`SYS SY+30,0,20,39,23,3,32`), "[original text]" at column 10, row 21, the
  seven pointers + 8, the SID cheer (`0x749`, I's 9.6), − 8, the panel cleared. No interrupt changes the frame (I's 9.5,
  s224 watchpoint).
- The wands (`0x622`): "FILMON SPEAKS -" at column 2, row 20, then three `PRINT`s from column 0, rows 21–23 (their own
  leading spaces), the last ending "' (PRESS RET)"; `POKE 198,0`, `GET` until a key (`0x9d0`), the panel cleared; `SYS
  SY+60,0` (the bands off); the closing page (`0xa74`) and its `GET`; "INSERT BOOT DISK AND PRESS A KEY"; `TOWN2`.

The remaster (s224, `ending_scene.c64_win`, `C64WinScreen`) runs these steps. Not as the C64: Core titles records
0 … n − 1 (n = OK members, the ST's rule, endgame.md 7.1) and draws a title only for those, where the C64 presents every
slot `[~]`; the closing page is Core's 21 lines (the ST's breaks, as before) `[~]`. s226: the SID cheer plays at each cheer (I's
GRAPHICS.md 9.6), and `CEMETARY2`'s two sounds as I's. s228: the fights' sounds of `OUTDOORS2` / `DUNGEON2` (I's 9.8, timed in
VICE on these programs): I's routines but a member's death waits 70 jiffies (`OUTDOORS2` `0x6c1d`), `DUNGEON2`'s cast
clears all 25 registers, the rock's miss (`0x3934`) and hit (one mark, `SP` = 2, `0x3304`) sound; `TOWN2`'s ring tune
(`0x5525`) is not called.

### 8.2 The party wipe: `CEMETARY2` `[C]`

`SKEL2`, `CSPRIT` and `FONTO` are byte-equal to I's side-2 files, and `CEMETARY2` (2,795 bytes) shows I's screen (I's
section 9.2): the temple, the ghosts (two, at x `$5C` and `$7F`), the rising door, the face, the fates (LOUIS undead:
block 0 in cyan), seen live. s223: `CEMETARY2` is `CEMETARY`'s program but for the file names and the record writes after
the fates (bytecode diff); a destroyed member's figure is switched off right after it is coloured dark grey (I's 9.2 /
9.4), so THUNDAR's "dark grey" was the colour register of a sprite already off.

### 8.3 PNGs and the remaster's slots

`tools/scripts/extract_c64_end_screens.py` → `extracted/graphics/{ending,defeat}/` (s221: moved from
`extracted/end-screens/`; the importer's `C64EndScreens`, recipe 3, writes the same files), from II's own files; checked
pixel for pixel against renders of the live dumps (the whole hall; hall + each crowd frame, rows 0–159; the temple with
the demon, rows 0–99) and by palette index against VICE's screenshots in the hall's rows 0–99 and the crowd's rows:
0 differing pixels.

| PNG | Shows | Slot (s221: wired) |
|---|---|---|
| `ending/hall.png` | `PH2.END-4`, the cyan panel empty | `ending.final` |
| `ending/crowd-0.png`, `crowd-1.png` | the crowd strip, 320 × 42, at screen y 110 | `ending.crowd.0`, `.1` (`crowd` at (0, 110); s223: frame 1 for each cheer's sound, 9.92 s, then frame 0 (I's 9.5); s224: from the hall's band on, as `WIN` (8.1); transparent pixels keep the hall: the ending screen leaves a transparent pixel's pen, s221) |
| `ending/hall-crowd.png` | the hall + crowd frame 0 | — |
| `defeat/*` | as I's (`temple`, `temple-door`, `temple-demon`, `ghost`, `door`, `demon-face`, `font`) | `ui.decor` = `temple.png`, `portrait.ghost` = `ghost.png`; no `decor.door` (the ST's defeat scene sinks its door; the C64's rises) |
| `defeat/temple-demon-doorway.png` | s221: `temple-demon.png`'s (123, 19)–(171, 72), 49 × 54 | `decor.demon-face` (the defeat scene draws it at the ST's (123, 19); the C64's face covers (124, 31)–(171, 72)) |
