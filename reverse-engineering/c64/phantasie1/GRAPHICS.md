# Phantasie I (Commodore 64 v1.4): pictures and the `c64` art pack

How I's C64 pictures are stored and drawn, and how the `p1-c64` import turns them into the `c64` graphics pack (s218).
Files and tables: `FORMAT.md` (section 6 points here). Tags as there: `[C]` read in the 6502 code or the BASIC, or seen
on the live screen and checked against the files; `[~]` probable; `[?]` not known.

**Evidence (s218).** The 6502 helpers of `UTIL-3.O` (loaded at `$C000`; side 2's copy is byte-equal to side 1's up to
byte 2,164, the tail differs) were disassembled in VICE's monitor; `B` (plain BASIC) was listed with `petcat`. Live
screens: VICE 3.10 `x64sc` (flatpak `net.sf.VICE`, on Xvfb, sound off, remote monitor) running the C64PP v1.4 pair and the
held en `.d64` pair; RAM + I/O dumps taken on the town, overworld, combat, dungeon and feature-picture screens and
rendered back from the dumps (the rendering matches VICE's own screenshot). The dumps are not kept in the repo.

**Palette.** The VIC-II's 16 colours as **Colodore** (Pepto's 2017 measurement, colodore.com; the RGB values of VICE's
`colodore.vpl`, PAL): `C64Gfx.Colodore`, also the pack's `palette`.

## 1. Memory map while playing `[C]`

`B` (lines 370–410) loads `FONT` to `$E800`, `FONT+HER` to `$21DA` and copies its 4,330 bytes to `$CFDA`; copies the 200
bytes at `$E000` (three sprites: the town's ENTER arrow, a moon, a sparkle) to `$CA40`. The VIC runs in bank 3
(`$DD00` = `%00`): screen `$E400`, character set `$E800` (`$D018` = `$9B` on the map, in fights and in the dungeon,
`$97` = `$D800` in town; II's live values, s219, the same engine),
sprite pointers `$E7F8`.

| Address | From | Holds |
|---|---|---|
| `$CFDA`–`$CFFF` | `FONT+HER` 0–37 | the hero band's record (section 2), the party's sprites in a fight |
| `$D000`–`$D7FF` | `FONT+HER` 38–2085 | a mixed-case font (characters 0–95), then sprite blocks: 91 (`$D6C0`) the dungeon's party, 92–95 (`$D700`) the map's party, town, inn, dungeon |
| `$D800`–`$DFFF` | `FONT+HER` 2086– | the town's character set: the six buildings (section 4) |
| `$E800`–`$EFFF` | `FONT` | the screen font (upper case, 0–63), the map's terrain characters (64–87), the dungeon's characters (`$E6`–`$EC`); characters 84 / 85 (water) are rewritten while the map is up: in II (live s219) `UTIL-3.O` `$C6E0`, from the raster interrupt every 4 frames, rotates both characters' rows right by one bit (a sideways drift; I's `[~]`); the pack keeps the file's frame |
| `$F000`– | `HSPRIT` (TOWNR) | 30 hero sprite blocks (block 192 on) |
| `$CB5A`, … | `MX n` (`$1FDA` in the file) | a monster group's band record + its blocks, loaded per group (`$CB5A`, `$CCDA`, `$CE5A` seen) |

## 2. Sprite bands: `MX n` and the hero band `[C]`

The raster interrupt `$C529` walks a list of band records (`$C49F`, 2 bytes each; `SYS 49152 + 57` = `$C3DF` adds a
record's address and sets its byte 25 to `(address + 38) / 64`) and loads each into the VIC when the beam reaches it:

| Byte | Goes to | Notes |
|---|---|---|
| 0 | added to every y | the band's y (run time; the file holds `$78`) |
| 1–16 | `$D000`–`$D00F` | 8 (x, y) pairs, y relative to byte 0; `$D010` is cleared (`$C450`), so x < 256 |
| 17–24 | `$D027`–`$D02E` | each sprite's colour |
| 25 | | the block base (run time; the file holds `$80`) |
| 26–33 | `$E7F8`–`$E7FF` | base + this = each sprite's block |
| 34 / 35 / 36 | `$D01D` / `$D017` / `$D015` | x-expand, y-expand, enable masks |
| 37 | `$D012` | the next band's raster line (run time) |

Sprites are hires (`$D01C` = 0). The blocks follow the record: `MX n` is the record + k blocks of 64 bytes, k = MN field
13 + 1 (FORMAT.md 3.1). The old reading (8 × 16-bit x positions, s214) was wrong: they are (x, y) byte pairs.

### 2.1 One monster

A group shows several monsters: the record lays out the whole group (e.g. SKELETON: 6 one-sprite skeletons 28 px apart,
enable `$3F`; BLACK KNIGHT: two knights of four expanded sprites each, enable `$FF`). **One monster = sprites 0 .. j − 1,
j = up to the first block index that repeats** `[~]` (data rule; it holds for all 80: every one gives a figure whose
sprites touch), drawn at their (x, y) with their expansion and colour. The pack's `monster.N.idle` is that figure cut to
its sprites' box: 24 × 21 (one plain sprite) up to 144 wide (PYTHON) and 126 tall (BLACK KNIGHT).

**Attacking:** while STING BEETLES attacked (live, en copy), single block offsets in the group's record went 0 → 1
and back: a one-sprite monster's attacking frame is block 1 `[C]` (50 monsters). A monster of j > 1 sprites has one
block more than it shows (k = j + 1); in II (live s219, the same engine) VIPER's two sprites went from blocks 0 1 to
2 1 while it attacked: the extra block replaces **sprite 0** `[C]` for VIPER. The pack takes that rule for all 30
several-sprite monsters `[~]`: `monster.N.attacking` = the figure with sprite 0 showing block j (for one sprite, block 1),
80 monsters. In every `MX` file the figure shows blocks 0 … j − 1 and the file holds j + 1: block j is the spare one.

### 2.2 Heroes

`HSPRIT` (1,921 bytes at `$F000`): 30 blocks = 15 pairs, pair r = race r (0 HUMAN … 14 MINOTAUR), first idle, second
the attacking pose `[C]` (in a fight the band at `$CFDA` points each member's slot at a copy of its race's pair: halfling
→ 6/7, human → 0/1, elf → 4/5, seen live). No UNDEAD pair. Colour: yellow (7) in the fights seen; in a six-member fight
the members were light blue (14) with the acting one yellow `[~]`; the pack draws yellow. The hero band's template
(`FONT+HER`'s first 38 bytes): x 27 + 24 i, colours 3 7 5 10 1 1 1 1.

## 3. The title `[C]`

`B` lines 200–290: `PIC.SCR` → `$8400`, `PIC.CLR` → `$D800` (colour RAM), `PIC.MAP` → `$A000`; VIC bank 2, multicolour
bitmap (`$D016` bit 4, `$D011` bit 5), screen `$0400` and bitmap `$2000` in the bank; background `$D021` = 0 (line 595).
Bit pairs: 00 background, 01 the screen byte's high nibble, 10 its low nibble, 11 colour RAM. 160 × 200, drawn 320 ×
200 with doubled pixels (`screens/title.png`, `title.cover`). The C64 shows it while `UTIL-3.O` and the fonts load, then
the "1 - Start in Pelnor / 2 - Continue" text screen.

## 4. The town screen

Six buildings printed from `FONT+HER`'s town set (`$D800`), characters numbered down each column (live, `TOWNR`'s
screen; the code that prints them is not read `[~]`):

| Building | Text column, row | Size | Characters | Colour |
|---|---|---|---|---|
| Guild | 1, 9 | 6 × 6 | `$40`–`$63` | 7 yellow |
| Armory | 11, 9 | 6 × 6 | `$64`–`$87` | 9 brown |
| Bank | 21, 9 | 6 × 6 | `$88`–`$AB` | 3 cyan |
| Inn | 1, 17 | 6 × 6 | `$AC`–`$CF` | 15 light grey |
| Mystic | 21, 17 | 6 × 6 | `$D0`–`$F3` | 12 grey |
| Exit (gate) | 12, 20 | 4 × 3 | `$F4`–`$FF` | 9 brown |

The rest of the C64's town screen is text (the party list rows 0–7, the menu and the town's name on the right) and the
ENTER arrow sprite that the cursor keys move between buildings. `town.backdrop` = the six buildings on black at their
places (320 × 200; the Client draws its own menu bar and name over it).

## 5. The overworld map `[C]`

`UTIL-3.O` `$C5C3` (`SYS 49152 + 63`) prints the region buffer (`$9448`, FORMAT.md 3.6) column by column from text
column 26 down to 1, rows 5–24, one character per cell (the whole 26 × 20 region, no window):

- map byte ≥ 120 (fog): class 12; otherwise class = byte div 10;
- colour `$C688[class]` = 5 5 9 9 0 0 11 11 9 9 6 6 0, PETSCII `$C695[class]` = `$60 $62 … $74`, then `$20` for
  classes 11 and 12, **OR'd with the text column's bit 0**: each terrain is a pair of characters by column parity
  (screen codes `$40 + 2 class` / `+ 1`). Fog is black; class 11 (bytes 110–119, the pools) prints `$20` / `$21` in
  blue (a solid block, then `!`, in `FONT`).
- bytes 40, 41 and 50–59 also move sprite 1 (town, block 93), 2 (inn, 94) or 3 (dungeon, 95) to the cell: x = 8 ·
  column + 20, y = 8 · row + 50 (VIC), i.e. 4 pixels left of the cell, top aligned; colour 7 (`$C5E7`). One sprite each:
  a second town byte in a region moves the same sprite (no region checked for that). The party is sprite 0 (block 92,
  white), placed the same way (seen live on the town cell).

The pack draws tile b (`overworld.tile.b`) for even map columns and tile b + 256 for odd ones (`odd_column_tile`), the
three sprites as `overlays` (drawn at their own size, offset (−4, 0)) and the party at its own size at the same offset
(`party_offset`). The Client's map box holds 19 × 18 cells around the party (the content area is 150 pixels high; the
C64 shows 26 × 20).

## 6. The dungeon map `[C]` (live)

The dungeon map is printed in `FONT`'s characters, all in grey (12) on black (colour RAM over the whole map area), the
level's cell (x, y) at text column x, row y + 3 (33 × 38 levels; seen on DNG 1). Correlated with the `DNG 1` file:
wall 0 → `$E6` (solid), 2–184 → `$E7` (blank), 185 → `$EA`, 200 / 209 / 213 → `$E9`, 215 → `$EB` (the exit
triangle), 235 → `$E8` (dot): DOS's ranges (OV2 `0x21df`), which the pack uses whole, `$EC` (stripes) for 218–220
(seen in II with poked cells, s219, the same characters). A cell not yet seen is `$20` (solid grey, like a wall). The party is sprite 0, block 91, yellow, on its
cell (`$D6C0`'s face fits the cell's top-left 8 × 8; drawn over the cell's glyph, `dungeon_party_over`). The code that
prints it is in `DUNGEON`'s bytecode (not read).

## 7. Item and feature pictures `[C]` (s221, read in the compiled BASIC)

The 14 files of FORMAT.md section 2 are character data loaded into the map's set at `$EAC0` (characters `$58` on) and
printed numbered down each column: 392-byte ones 7 × 7 characters (56 × 56), `EXIT` (384 bytes) 8 × 6 (64 × 48), in
one colour each (hi-res characters, as the town's and the map's: colours 8–15 print as plain colours).

**Who prints them.** `OUTDOORS` and `DUNGEON` (II: `OUTDOORS2`, `DUNGEON2`, the same bytecode) have one subroutine
(`DUNGEON` file offset `0xB2D`, `OUTDOORS` `0xB12`): it loads the file named in `IT$` to `$EAC0` (`DUNGEON` only when
the name differs from the one loaded last), then `SYS … Q,x0,y0,x1,y1,IC,88` prints characters 88 on over the map in colour `IC`: `DUNGEON`
text columns 11–17, rows 11–17; `OUTDOORS` columns 10–16, rows 11–17. A caller sets `IT$` (compiled store `D6 02 7F`
in `DUNGEON`) and `IC` (`D4 04 EE`) and calls it (`OUTDOORS`: `IT` `04BD`, `IX` `04C4`, `IC` `04CB`). `EXIT` has its own call (`DUNGEON` `0x445A`: `Q,"EXIT",QC` then
`Q,10,11,17,16,5,88`: columns 10–17, rows 11–16, the colour the literal 5). Variable names: the compiled program keeps a
BASIC variable table (7-byte entries, name + value; `DUNGEON` file offset `0x6AE5` on), an operand being the value's
offset in it: `04E0` = `IT`, `04E7` = `IX`, `04EE` = `IC`, `027F` = `IT$` (II's `DUNGEON2`: `04F5`, `050A`, `0511`,
`028D`). The names list (`I$`) comes from the programs' `DATA`: `DIM I$(8)`, then `FOR X = 0 TO 8: READ I$(X)` (II
`OUTDOORS2` `0x82D`) reads SHIELD, ARMOR, WEAPON, GOOD WEAPON, MG WEAPON, POTION, SCROLL, TREASURE, GOOD TREASURE.

| Moment (the text under it) | Where (`DUNGEON` file offset) | File | Colour |
|---|---|---|---|
| A found item, "YOU FIND %s. KEEP IT?" (fights' spoils, the dungeon's finds; `OUTDOORS` too) | `0x357F` (`OUTDOORS` `0x340E`) | `I$(IX)`, `IX = (IT − 1) / 20` | by item id, below |
| A trap someone spotted: "%s SPOTS A TRAP. DISARM IT?" (none spotted: the code jumps past it, no picture) | `0x3E06` | `TRAP` | 10 light red |
| Stairs: "[original text] %d TO %d" | `0x48D7` | `STAIRS` | 12 grey |
| A locked door (lock kind 14 = cell − `$B8`): then "%s PICKS THE LOCK" or "[original text]". A jammed one (kind 15) jumps to [original text] (`0x4AB9`) with no picture; kinds 2–13 (the listening doors) none | `0x4A33` | `LOCK` | 9 brown |
| An exit: "AN EXIT. TAKE IT ?" | `0x445A` | `EXIT` | 5 green (also live s218) |
| — | none: no program loads it (no `"DOOR"` string in `TOWNR`, `OUTDOORS`, `DUNGEON`, `CEMETARY`, `P.END`, nor in II's programs) `[~]` | `DOOR` | `[?]` (never shown) |

**The find colour goes by the item id, not only its group.** The code: `IX = (IT − 1) / 20: IT$ = I$(IX): IC = 15: IF
IX = 2 OR IX >= 6 THEN IC = 7: IF IX = 3 THEN IC = 14: IF IX = 4 THEN IC = 3`. `IT`, `IX` and `IC` are floating-point
variables (no `%` in their names) and `/` divides in floating point: RTL-64's `04` handler (`$ACFF`) converts two
integers to float when the program's flag `$0882` is 1, which it is in all four programs; the store (`D4`, `$B079`)
keeps the float, and a compare is a float compare when either side is one (`$ADFE`). So `IX` is whole only for ids 1,
21, 41, …, and the picture (the array index truncates) follows the group of 20 while the colour tests hit single ids.
Run in py65 on RTL-64 + `DUNGEON` (s221): id 45 → `IX` 2.2, `IC` 15; 41 → 2, 7; 61 → 3, 14; 81 → 4, 3; 101 → 5, 15;
121 / 141 / 161 / 180 → 7. II's bytecode is the same (byte compare of the block).

| Item ids | Picture | Colour |
|---|---|---|
| 1–20 | `SHIELD` | 15 light grey |
| 21–40 | `ARMOR` | 15 |
| 41 | `WEAPON` | 7 yellow |
| 42–60 | `WEAPON` | 15 |
| 61 | `GOOD WEAPON` | 14 light blue |
| 62–80 | `GOOD WEAPON` | 15 |
| 81 | `MG WEAPON` | 3 cyan |
| 82–100 | `MG WEAPON` | 15 |
| 101–120 | `POTION` | 15 |
| 121–140 | `SCROLL` | 7 |
| 141–160 | `TREASURE` | 7 |
| 161–180 | `GOOD TREASURE` | 7 |

The groups are I's item blocks (1–20 shields … 161–180 rings and gems). II uses the same rule on its own ids, whose
blocks differ (41–100 all weapons, 101–110 healing potions, 111–120 magic potions, 141–160 amulets): II's C64 shows
`GOOD WEAPON` for a weapon 61–80, `MG WEAPON` for 81–100 (`DUNGEON2` `0x3937`, `OUTDOORS2` `0x3768`).

Not read `[~]`: whether an event script's trap (op 16) shows `TRAP` (the C64's script interpreter not walked; the
remaster shows it at every "SPOTS A TRAP" question); where on the C64 screen the texts are printed beside the picture.

**Written** to `items/` in those colours: `shield`, `armor`, `weapon`, `good-weapon`, `mg-weapon`, `potion` light grey,
`weapon-yellow`, `good-weapon-light-blue`, `mg-weapon-cyan` (the single-id colours), `scroll`, `treasure`,
`good-treasure` yellow, `trap`, `stairs`, `lock`, `exit` as the table; `door` white, a placeholder (its colour `[?]`,
never shown; no slot).

**Drawn** (s221) through the pack's `event.*` slots (section 8): the Client draws the picture beside the text of the
moment: in the question's box (YOU FIND, SPOTS A TRAP, AN EXIT) as a column left of the text, which wraps narrower; for
the stairs and lock lines in a box of its own next to the line (under I's DOS band, above the message line in II). It
follows the art pack, not the AMIGA SCREENS switch; a pack without the slots draws nothing there and nothing moves.

## 8. The `c64` pack (`GameData/phantasie1/graphics/c64.json`)

Written by the `p1-c64` import (recipe 4 since s221, `P1C64Art`, `P1C64ArtManifest`; since s219 the steps and the manifest's
layout are shared with II's pack in `C64PhantasieArt` / `C64PhantasieArtManifest`, `../phantasie2/GRAPHICS.md`) under
`reverse-engineering/c64/phantasie1/extracted/graphics` (280 PNGs since s221: the three single-id find colourings, and
`ending/` + `defeat/`, section 9.3; the repo holds the extract of the C64PP v1.4 pair).
In `index.json`'s `default_order` after the Amiga: the Amiga stays I's default; with no Amiga copy imported (a C64-only
or C64 + DOS player) the C64's art is drawn, DOS CGA stays pick-only.

| Slot | C64 picture |
|---|---|
| `monster.N.idle` / `.attacking` | section 2.1 (attacking: all 80; the 30 several-sprite ones by II's rule `[~]`) |
| `portrait.R.idle` / `.attacking` | `HSPRIT` pair R, R = 0–14 |
| `title.cover` | `PIC.*` |
| `town.backdrop` | section 4 |
| `overworld.tile.*`, `overworld.party` | section 5 |
| `dungeon.cell.*` | section 6 |
| `event.trap`, `event.stairs`, `event.lock`, `event.exit` | section 7: `TRAP`, `STAIRS`, `LOCK`, `EXIT` in their colours |
| `event.find.*` + `find_pictures` | section 7: `find_pictures` = item id ranges → an `event.find.*` picture (the twelve rows of the id table) |
| `ending.final`, `ending.ghost`, `ending.arm` + `ending_scene` | s221, section 9.1: `ending/zeus.png`, the cloud (`sprites/d0-cloud.png`), the lightning hand (`sprites/cf-lightning-hand.png`) at (178, 12) |
| `defeat.temple`, `defeat.demon`, `defeat.ghost`, `defeat.figure.N` + `defeat_pictures`; `portrait.ghost` | s221, section 9.2: the temple, the temple with the face, the ghost; the figures = `portraits/idle` (CSPRIT = HSPRIT's idle blocks, yellow) |

**The ending and the party wipe (s221, section 9.3; s223, section 9.4):** since s223 both run as the C64's programs do.
The ending: `ending_scene.c64` (`C64EndingLayout`, the Client's `C64EndingScreen` / `C64EndingView`): the astral line,
the clouds rising beside Zeus at the C64's places, Zeus wiped in from the bottom up, a strike per slot (the lightning hand,
then the member's own figure in the cloud's place, yellow or light grey), "Zeus smiles and says" with the face cycling
`ending.face.0`–`7`, the speech typed a word at a time on rows 18–24, the grant hand cycling `ending.hand.0`–`5`; sprites
at their own 24 × 21 (not scaled). The party wipe: `c64_defeat` (`C64DefeatLayout`, `C64DefeatScreen` / `C64DefeatView`,
the same for II) under ORIGINAL SCREENS: the defeated line, the temple with the door behind it, the ghosts one by one at
the C64's places, the door rising (`defeat.door`), the face (`defeat.face`), each fate's line in its colour and the
member's own figure (`P(145)`: `BirthRace`) in cyan (UNDEAD) or yellow, a destroyed member's slot empty; it runs by itself.
With ORIGINAL SCREENS off, the party wipe stays on DOS's pages (`defeat_pictures`, `P1DosDefeatScene`). Waits: the `TI`
seconds and frame counts of section 9.4; the interpreter's own time measured in VICE (section 9.5) `[~]`; `vbl_hz` 50
(VICE's PAL) `[~]`. Not modelled `[~]`: the closing text page after the grant (not imported: the screen ends after the
grant); the C64's own speech wording (Core's seven lines on its rows); the undead member's name on row 21; the ghost's
block being rewritten byte by byte (the figure appears at once after `fate_copy_seconds`). The SID sounds play since s226
(section 9.6). Live:
`parity/s223-c64-endings-live.md`.

**Falls back (no C64 picture):** UNDEAD's portrait and `defeat.figure.15` (CSPRIT has no UNDEAD figure: the Client
draws none, as for any missing portrait); the title's credits screen, `ui.decor` / `decor.*` (the Amiga's own defeat
screens: its sinking door, the sorry sign), combat effects (`combat.grave`: the C64 shows no grave; `.sparkle`: the
`$E000` sparkle sprite is not mapped), the town doors; these slots draw the Client's own default (black / text), as
III's Apple pack does. Text: the remaster 5 × 7 font (the C64 sets are written as sheets under `font/`, the ending's and
the defeat's as `ending/font.png`, `defeat/font.png`).

`[?]` left: `DOOR`'s colour (no program shows it); the hero colour rule (yellow / light blue); the water's
moving characters (not drawn); the code printing the town buildings and the dungeon map. (s223: `P(168)` and the sprite
blocks `$C8`–`$CE` are settled in section 9.1.)

## 9. Ending and defeat screens (s221)

Decoded s221 from `P.END` and `CEMETARY` (compiled; their `SYS` lines are plain BASIC, the rest read with
`../bytecode-notes.md`), the data files, and live VICE 3.10 `x64sc` (flatpak, Xvfb, sound off) on writable copies of the
C64PP v1.4 pair. Method: the town state poked (roster records copied into the party slots at `SA` = `$9A24`; both
programs loop over the slots `0 … PEEK(SA + 1203)` `[C]`, so that byte is the last slot's index, members − 1 `[~]`), then `TOWNR`'s `OUTDOORS` load redirected to `P.END` / `CEMETARY` (the file name patched, the
bytecode pointer `$39` set to that `LOAD`). RAM + I/O dumps about once a second; not kept in the repo. This settles
section 8's `ZALL` and `CEMETARY` `[?]` items; the slots are wired since s221 (section 8).

Both screens run in VIC bank 3: screen `$E400`, character set `$E800` (`$D018` = `$9B`), hires text (`$D016` = `$C8`),
border and background black, sprites hires unless noted. Positions below are VIC sprite coordinates (screen x + 24,
y + 50).

### 9.1 The ending: `P.END` and `ZALL` `[C]`

**`ZALL`** (side 1, 3,716 bytes at `$4000`):

| Bytes | At run time | Holds |
|---|---|---|
| 0–767 | `$E800` (`SYS Q,DS,59392,96*8`) | characters 0–95: the ending's mixed-case font |
| 768–2047 | `$EB00`–`$EFFF`, copied in by the wipe | characters `$60`–`$FB`: **Zeus on his throne**, 12 columns × 13 rows of characters numbered down each column (`$60 + 13 c + r`) |
| 2048–3263 | `$F000` (`SYS Q,DS+2048,61440,1216`) | sprite blocks `$C0`–`$D0` (below); `$D1`–`$D2` are disk slack (directory entries `TWN 6` … `TWN 9`), copied but never shown |
| 3264–3391 | `$4CC0` | 6502: the wipe (`$4CC3`, `SYS` with a frame count): 104 steps, each waits that many frames, then copies one byte per column (12 columns, 104 bytes apart) from `ZALL` into the character set, starting at the bottom row (`$EB67`) and moving up: Zeus appears from the bottom up |
| 3392– | `$4D40` | 6502: the wind, a sound routine hooked into the KERNAL IRQ (`$0314`; SID `$D400`–`$D41C`), section 9.6 `[C]` |

`P.END` first clears characters `$60`–`$FF` (`SYS Q,60160,60161,1280`), so Zeus is drawn only by the wipe; its copy of the
character set leaves character `$FF`'s last byte 0 (the file holds 4; `$FF` is not drawn). Zeus's cells: text columns
13–24, rows 2–14 (`SYS Q,ZX,ZY,ZX+11,ZY+12,1,96`, `ZX` = 13, `ZY` = 2), white (1): a 96 × 104 picture at screen (104, 16).

**Sprite blocks** (`$F000` = block `$C0`):

| Block | Picture | Live use |
|---|---|---|
| `$C0`–`$C5` | Zeus's right hand, six frames | sprite 7 at (200, 74), white, while P.END prints spaces over the hand's six cells (rows 3–4, columns 22–24): the grant, cycled `$C0`, `$C1` … `$C5`, `$C0` … by `UTIL-3.O`'s animator, 10 frames each (s223, section 9.4) `[C]` |
| `$C6`–`$CD` | Zeus's face, eight frames | sprite 6 at (161, 77), white, over the face's four cells (rows 4–5, columns 18–19, printed as spaces): cycled `$C6` … `$CD` in order, 10 frames each, from "Zeus smiles and says" to the end of the speech (s223, section 9.4) `[C]`. s221's once-a-second dumps caught every fifth frame (`$C7`, `$CC`, `$C9`, `$C6`: 50 frames = 5 steps mod 8), so all eight are used |
| `$CE` | a striped ball | no pointer in `P.END` reaches it (blocks used: `$C0`–`$C5`, `$C6`–`$CD`, `$CF`, `$D0`): unused (s223) `[C]` |
| `$CF` | the hand sending lightning | sprite 7 at (202, 62), light blue (14), switched on and off while each member is struck `[C]` |
| `$D0` | the members' cloud (multicolour) | sprites 0 … n, colour 2, `$D025` (MC1) flashing 7 / 2, `$D026` (MC2) 2 `[C]` |

**Sequence** (live, `PEEK(SA + 1203)` poked to 0 and to 4): the "INSERT BOOT DISK AGAIN" box; `ZALL` loads; a black
screen with "You have arrived at the astral plane" (yellow, row 23) and **one cloud per slot** `0 … PEEK(SA + 1203)`
(P.END tests no slot; with 4 poked over a four-member party, slot 4 was empty and its figure came out as junk, block
`$40`: a state the game itself would not make): sprites 0–2 at x 90, 60, 30 and 3–4 at 240, 270, rising from y 240 to 150 (s223: they start at y 255, off the screen; s221 read 231 from a dump taken during the rise). The wipe (104 × 7 frames, section 9.4).
Per member Zeus strikes (`$CF` flashing) and the cloud becomes the member's figure: **pointer `64 + P(168)`** (`P.END`
`0x6a0`: `POKE screen + $3F8 + member, FN P(168) + 64`), a block of `FONT+HER`'s copy at `$D000` (blocks `$4C`, `$4E`,
`$52`, `$54` seen = `HSPRIT` blocks 0, 4, 12, 16), multicolour off, colour 7 if `P(18)` (status) ≠ 0, else 15; an OK
member also gets `POKE C(118),1` (spell 58, "Your First Divine Spell"). Then "Zeus smiles and says" (row 24) and the speech, typed
word by word in yellow on rows 18–24 with the face talking (s223: section 9.4; s221's "row 21" was a line part-way down); the hand at the grant; a text page without
pictures ("Now Your Party will be transported to The City of Pelnor …", green) until a key (s224: imported and drawn, 9.4
step 10; s226: with `p1.victory` = `apple`, the remaster's default (D-090), the party goes to Olympus and line 2 reads
"The  Olympic Plane. After arriving the-", `c64.endgame.closing_2.olympus` via the pack's `closing.olympus_lines`); `FONT` back to `$E800`; `TOWNR`.

`P(168)` (record byte 168, in FORMAT.md 3.5's `[?]` range) is the figure's block offset. s223: `TOWNR` `0x2c41`–`0x2c60`
sets it, for each party slot k, to **12 + 2 k** right after copying the member's `HSPRIT` pair (from `$F000 + 128 · P(145)`,
`SYS SY+27`) to `$D300 + 128 k` (block `$4C + 2 k`); `0x170d` sets it the same way for a member joining, slot 6 gets 25, and
the fights (`OUTDOORS` / `DUNGEON`) add 1 for the attack frame and take it back `[~]`. So in a state the game made, the
ending's figure is the member's own idle figure, `HSPRIT` block 2 · `P(145)` (the pack's `portrait.N.idle`) `[C]`. s221's
GRUNT 12, HIT 20, SCOTT 18, BORACK 14 (races 0 / 3 / 2 / 2) came from roster records poked into other slots, whose old
offsets pointed at other members' copies.

### 9.2 The party wipe: `CEMETARY`, `SKEL2`, `CSPRIT`, `FONTO` `[C]`

| File (side 2) | Bytes, loaded to | Holds |
|---|---|---|
| `FONTO` | 1,968 at `$E800` (`SYS Q,"FONTO",59392`) | 246 characters: 0–63 an upper-case font (the defeat's text); `$40`–`$F3` **the temple**, 18 columns × 10 rows numbered down each column |
| `SKEL2` | 512 at `$E100` = blocks `$84`–`$8B` (`SYS Q,"SKEL2",57600`) | `$84`–`$89`: six identical **ghosts**, one block per party slot; `$8A`: the **Great Demon's face**; `$8B`: a solid block, the **door** |
| `CSPRIT` | 1,025 at `$2000` (`SYS Q,"CSPRIT",8192`; outside the VIC's bank) | 16 blocks: 0–14 byte-equal to `HSPRIT`'s idle blocks 0, 2, … 28 (the figures); 15 byte-equal to `SKEL2`'s face |

**Screen** (live, parties of 1, 4 and 2): "[original text]" (red, row 23) on black; the temple at text columns
10–27, rows 0–9 (`SYS Q,DX,DY,DX+17,DY+9,11,64`), dark grey (11), its doorway open (background); a ghost per party slot
`0 … PEEK(SA + 1203)` = n: sprite k, block `$84 + k`, white, y 170, x `$6E` (n = 0), `$5C` `$7F` (n = 1), `$28` + 35 k
(n = 4; only the four slots with a level were switched on, `$D015` = `$8F`), i.e. centred: x = 110 − 17.5 n + 35 k `[~]`
(the formula; the values `[C]`). The **door**: sprite 7, block `$8B`, light grey (15), x- and y-expanded, **behind the
characters** (`$D01B` = `$80`), at (148, 77), filling the doorway; its y then falls by about 10 a second to 0, so the door **rises** into the lintel (the Amiga's door sinks).
"YOU HAVE ARRIVED ON THE ASTRAL PLANE !!" (blue). Then the face: sprite 6, block `$8A`, purple (4), expanded, in front,
at (148, 81), in the doorway; "[original text]" (blue). Per member with a level (`P(54)` ≠ 0): its
line (row 23; an UNDEAD fate also its name on row 21, s224: 9.4 step 6), then its ghost block is rewritten in place into `CSPRIT` block `P(145)` (`CEMETARY` `0x877`: from
`8192 + 64 · P(145)`; seen: GRUNT block 0, HIT 3, SCOTT and BORACK 2), coloured as the fate's line: **GRANTED UNDEAD
STATUS 3 (cyan)**, RESURRECTED 7 (yellow), **DESTROYED 11 (dark grey) and then switched off**: s223, `CEMETARY` `0x905`
is `ON DF GOSUB 0x863, 0x869` (opcode `$4E`, RTL-64 `$B528`: ON … GOSUB, the targets read in order); DF = 1 (destroyed)
runs `0x863` = the copy and the colour (`0x869`), then `0x80c` turns the sprite off, so a destroyed member's slot is
empty as on DOS; DF = 2 (resurrected, undead) keeps it `[C]`. s221's "dark grey, seen" was the colour register `$D027 + k`
in a dump, which keeps 11 with the sprite off. A
member whose race `P(161)` is already 15 (UNDEAD) takes the undead branch again. Then "PLEASE INSERT YOUR BOOT DISK
AGAIN".

### 9.3 PNGs and the remaster's slots

`tools/scripts/extract_c64_end_screens.py` writes them under `extracted/graphics/{ending,defeat}/` (s221: moved from
`extracted/end-screens/`; the importer's `C64EndScreens`, recipe 4, writes the same files, which the `p1-c64` tests
compare pixel for pixel), in Colodore; full screens 320 × 200 without the border, sprites RGBA.
Checked pixel for pixel (0 differing) against renders of the live RAM / I/O dumps, whole rows 0–119 / 0–99 (Zeus risen,
speech, grant and strike states; the temple, door and demon), and by palette index against parts of VICE's screenshots
(crop (32, 35) of the 384 × 272 shot): Zeus left of the face and his lower body, the temple with the demon (rows 0–99):
0 differing.

| PNG | Shows | Slot (s221: wired as proposed, except where marked) |
|---|---|---|
| `ending/zeus.png` | Zeus, all his characters | `ending.final` |
| `ending/zeus-speech.png`, `zeus-grant.png`, `zeus-strike.png` | the face sprite / face and hand / the lightning hand over Zeus, as live | — (states of `ending.final`) |
| `ending/sprites/d0-cloud.png` | the members' cloud | `ending.ghost` (the Amiga's ghost in each slot) |
| `ending/sprites/cf-lightning-hand.png` | the striking hand | `ending.arm` (the Amiga's raised arm) |
| `ending/sprites/c0`–`ce` | hands, faces, the ball | — |
| `ending/font.png` | `ZALL` characters 0–95 | — |
| `defeat/temple.png` | the temple, doorway open | `defeat.temple` (the DOS-style pages) |
| `defeat/temple-demon.png` | the same with the demon's face | `defeat.demon` |
| `defeat/ghost.png` | the ghost | `defeat.ghost`, `portrait.ghost` (drawn 32 × 28 in DOS's 32-pixel slots) |
| `defeat/temple-door.png`, `door.png` | the closed door / the 48 × 42 door sprite | — (`decor.door` only with a rising door; the Amiga's `defeat_screen` sinks it) |
| `defeat/demon-face.png` | the face sprite, 48 × 42 | — (I uses DOS's pages: `defeat.demon`; II's `decor.demon-face` is its doorway crop, II's GRAPHICS.md 8.3) |
| `defeat/font.png` | `FONTO` characters 0–63 | — |

The fate figures are `HSPRIT`'s idle blocks (already the pack's `portrait.N.idle`, section 2.2), so `defeat.figure.N`
can take those in the fate's colour; but DOS picks `defeat.figure.N` by race (15 = UNDEAD) and the C64 by `P(145)`
(equal to the race for these members), and `CSPRIT` holds no UNDEAD figure, so `defeat.figure.15` stays a fallback. The
two scenes pick the same figure in a state the game made (s223: `P(168)` = 12 + 2 · slot points at the copy of
`HSPRIT` pair `P(145)`, section 9.1); s221's HIT and SCOTT showed other figures only because of the poked records. An
UNDEAD fate keeps `P(145)` (only the race `P(161)` becomes 15), so the C64 draws the member's own figure in cyan. Not found on the C64: a grave (`combat.grave`) and a sorry sign
(`decor.sorry-sign`); they stay fallbacks. The ending's cloud and the defeat's ghost are different pictures (the Amiga
uses one ghost for both).

### 9.4 The programs step by step (s223) `[C]`

Read in s223 from the compiled BASIC of `P.END` (code `0x197`–`0xcc5`, variables from `0xccd`) and `CEMETARY` (`0x102`–
`0x9de`, variables from `0x9e4`) with a scratch disassembler of the opcodes in `../bytecode-notes.md` (new there: `$4E` /
`$4D` ON … GOSUB / GOTO, `$42` / `$43` FOR, `$46` NEXT, `$4C` RETURN, word operands big-endian, a statement's BASIC
token `$87` READ, `$99` PRINT, `$9F` OPEN, `$A0` CLOSE, `$A1` GET, `$84` INPUT# followed by its plain text), `ZALL`'s
6502 at `$4CC0` and `UTIL-3.O`'s at `$C86E` / `$C4D1` (side 1's copy: side 2's ends before `$C86E`).

**Three kinds of wait.** (a) `TI` waits: `GOSUB 0xb99` (`P.END`) / `0x8c2` (`CEMETARY`) set `TI$ = "000000"` and loop
until `TI` ≥ 60 · TD (TL), so TD seconds of the 60 Hz jiffy clock (60 Hz on PAL and NTSC: the KERNAL sets the CIA for
1/60 s on both), less up to one jiffy (the clock is reset mid-jiffy). (b) VIC frames: `ZALL`'s wipe and `UTIL-3.O`'s
animator count raster frames (PAL 50, NTSC 60 a second). (c) Busy loops: empty `FOR … NEXT`s and the BASIC around the
waits, whose time is the interpreter's (measured in VICE, PAL, section 9.5).

**`P.END`** (ZX = 13, ZY = 2, HB = `$C0`, FB = `$C6`; SC = `$E400`; positions VIC):

1. Setup: sprites 0–5 red (2), MC1 yellow (7), MC2 red (2); sprite 7 = the lightning hand at (HX + 2, HY − 12) = (202, 62),
   light blue (14). "INSERT BOOT DISK AGAIN", a key, `ZALL` loads (disk).
2. "You have arrived at the astral plane" (yellow, column 3, row 23); TD = 2.
3. The screen cleared; `SYS $4CC6` starts `ZALL`'s wind (`$4D40`, a KERNAL-IRQ hook, section 9.6). PO = `PEEK(SA + 1203)` (last slot), PL =
   INT(PO / 2 + 1.1); multicolour on for sprites 0–5. Slot m < PL: x = ZL − 30 m = 90 − 30 m; else x = ZR + 30 (m − PL) =
   240 + 30 (m − PL); y 255; pointer `$D0` (the cloud); all slots 0 … PO switched on at once.
4. The rise: for y = 240 down to 150 (91 steps): MC1 := C, `SYS $4CC3,y` (sprites 0–5 to y), TD = 0.05; every second
   step C := 9 − C (yellow ↔ red). Then MC1 := 7; TD = 2.
5. Zeus's 156 characters printed (white, still blank); `SYS $4CC0,7`: the wipe, 104 steps; each waits 7 frames, then copies
   one byte into each of Zeus's 12 character columns from the bottom pixel row up, so one pixel row of the 96-wide
   picture appears every 7 frames (PAL 14.6 s, NTSC 12.2 s). TD = 2. `SYS $4CC9` stops the music (P.END `0x644`, s226) `[C]`.
6. Sprite 7's pointer := `$CF`; for each slot m = 0 … PO (every slot, whatever its status): sprite 7 on; `GOSUB 0xbb6`
   (thunder: SID setup, TD = 0.3, TD = 0.2, `FOR I = 1 TO 1400`); multicolour off for sprite m; colour 7 if `P(18)` ≠ 0
   else 15; pointer 64 + `P(168)` (the member's figure, section 9.1); TD = 0.3; sprite 7 off; TD = 0.7; an OK member gets
   spell 58. One hand on / off per member (no flashing).
7. Multicolour off; sprite 7 to (200, 74), white; "Zeus smiles and says" (yellow, column 8, row 24); sprite 6 (the face)
   at (161, 77), white, pointer `$C6`, on; spaces over the face's cells; `SYS SY+51,6,$C6,10×8,0`: the animator cycles
   sprite 6 through `$C6` … `$CD`, 10 frames each. TD = 3.
8. Rows 18–24 cleared; the seven `DATA` lines, line I at column 1, row 18 + I, yellow, all left on screen. Each line is
   printed a character at a time (`MID$`, `PRINT X$;`); after each space `GOSUB 0xc43`: a SID click (gate on, a random
   pitch), `TI` from 0 until it reaches IT = 16 (16 jiffies), gate off, `TI` from 0 until IT := RND · 8 (0–8 jiffies). After
   each line the same `GOSUB` with IT still RND · 8 from the last click (so two short random waits). So the text appears a
   word at a time, ~0.3–0.4 s a word.
9. Sprite 7's pointer := `$C0`, on; spaces over the hand's cells; `SYS SY+51,7,$C0,10×6,0`: the animator now cycles the
   hand `$C0` … `$C5` (it has one slot, so the face stops), the face set back to `$C6`. TD = 12.
10. Zeus printed again, sprites 6–7 off, Zeus's cells cleared, all sprites off; `SYS SY+60,0` removes the animator. A text
    page in green from row 2 ("Now your party will be transported to the City of Pelnor …", 9 lines) and "(PRESS A KEY WHEN
    READY)" on row 24 until a key; `TOWNR` loads. s224, read in full (`0x92d`–`0xaf0`): `PRINT CHR$(147)`; `SYS SY+60,0`;
    `SYS Q,0,2,"<CHR$(30)>Now  Your Party …"` (column 0, row 2; CHR$(30) = green) and eight `PRINT`s, one row each (rows
    3–10, the C64's own line breaks: "the-" / "re,", "dis-" / "tribute"); `SYS Q,7,24,"(PRESS A KEY WHEN READY)"` (the
    unshifted letters print lower case in the mixed set); `GET A$` until a key (`0xae1`); then the disk check and `TOWNR`.
    The lines are P.END's PETSCII (`P.END:petscii` offsets 2374 … 2755): imported as `c64.endgame.closing_1` … `_9` and
    `closing_key`, C64-only plain alternatives (`build_string_sources.py`, the json's `only` ids) `[C]`.

**`CEMETARY`** (`CEMETARY2` is the same program but for its file names and the record writes after the fates, s223
diff): sprites 6–7 expanded, sprite 7 behind the characters. SS = INT((220 − 35 · PO) / 2): ghost k at (SS + 35 k, 170),
block `$84 + k`, white; the door (sprite 7, `$8B`, light grey) at (148, 77); the face (sprite 6, `$8A`, purple) at
(148, 81).

1. "[original text]" (red, column 6, row 23); TL = 2.
2. The temple printed (dark grey); the door on; "YOU HAVE ARRIVED ON THE ASTRAL PLANE !!" (blue, column 1, row 23).
3. For each slot with a level (`P(54)` ≠ 0): TL = 0.3, then its ghost on (one at a time).
4. A SID sound; TL = 2.
5. The door rises: y = 77 down to 0 (78 steps), TL = 0.1 each (behind the temple's characters, so it shows only in the
   doorway). Then the face on; "[original text]" (blue, row 23); TL = 4.
6. For each slot with a level: the fate (`0x433`–`0x576`: TX$ = "YOU HAVE BEEN " + DESTROYED / RESURRECTED / GRANTED
   UNDEAD STATUS, the colour E = 11 / 7 / 3, DF = 1 / 2 / 2; the destroyed and resurrected TX$ get "name-" in front, the
   UNDEAD one does not); rows 21–24 cleared (`0x577`); for UNDEAD only, the name in cyan at column 1, row 21 (`0x58e`,
   `"<CHR$(159)>"A$(PM)`), before the sound. Then (s224 order, `0x5a1`–`0x5a7`): `GOSUB 0x97b`, the SID sound with its TL =
   1.08; `GOSUB 0x8ee`: `POKE 646,E` and TX$ at column 1, row 23 in the fate's colour, then `ON DF GOSUB 0x863, 0x869`;
   `GOSUB 0x8bf`, TL = 2. `0x869`, the copy: YS = 8192 + 64 · P(145), DS = 57600 + 64 · slot; `FOR I = 0 TO 63`: a dummy
   expression (Y = P(X) / 0.77 + 2 ^ 1.777, the same each pass) then `POKE DS + I, PEEK(YS + I)`; after the loop
   `POKE 53287 + slot, E`. So the ghost turns into the figure one sprite byte a pass (byte i = pixel row i / 3, 8 pixels;
   byte 63 unused), **still white**, over the 4.1 s, and takes the fate's colour at the end (the s223 VICE shot
   `s223-vice-p1-defeat-morph.png` shows a white half-copied ghost); `0x863` (DF = 1) runs the copy, then `0x80c` turns the
   sprite off. Every pass runs the same statements, so the bytes come at an even rate `[C]` (the 4.1 s is VICE's, 9.5).
7. With no member left: "ALL MEMBERS ARE DESTROYED" (red); TL = 2. The record writes; TL = 6; the screen cleared, "PLEASE
   INSERT YOUR BOOT DISK AGAIN" until a key.

The fate rule itself (CON, level, `RND`) is the C64's: not the remaster's, which keeps DOS's (Core); only the pictures
follow it.

### 9.5 Measured in VICE (s223) `[C]`

VICE 3.10 `x64sc`, PAL (50.125 frames a second, CPU 985,248 Hz), true drive, sound off, no warp; `.d64` images built with
`c1541` from the v1.4 (I) and v1.2 (II) PRGs, `UTIL-3.O` and `FONT` loaded and a party poked at `SA` = `$9A24` through
the monitor (I: GRUNT OK, HIT dead with CON 0, SCOTT race 15, BORACK OK; II: LOUIS, THUNDAR), then `P.END`, `CEMETARY` or
`WIN` run. Times: the monitor's stopwatch at conditional breakpoints on the bytecode dispatch (`$A090` with `$39` / `$3A`,
written `(@cpu:$39 == $xx) && (@cpu:$3a == $yy)`), so emulated time; two runs each of `P.END` and `CEMETARY`. Logs and
the harness are not kept in the repo.

| What | Seconds | The pack |
|---|---|---|
| A cloud step (TD = 0.05) | 0.0606 mean (0.050–0.069); the rise 5.46–5.53 | `cloud_step_overhead` 0.0106 |
| The wipe | 14.521 = 728 frames (PAL; NTSC 12.17) | `vbl_hz` 50, `wipe_frames` 7 |
| `GOSUB 0xbb6` (thunder: TD 0.3 + 0.2, `FOR 1400` 1.231) | 1.79; a strike 2.82–2.86 | `thunder_seconds` 1.79 |
| A speech character | ~0.0064 (+ ~0.012 at a line start) | `letter_seconds` 0.0064 |
| A space's click to the next word | 0.342 mean (0.286–0.403) = (16 + RND · 8) / 60 | `space_jiffies`, `random_jiffies` |
| A line's end | 0.105–0.225 | (two RND · 8 waits) |
| The speech, seven lines | 17.5–18.4 | |
| The grant | 12.02 | `grant_seconds` 12 |
| A ghost (TL = 0.3) | 0.33 apart | `ghost_seconds` 0.33 |
| A door step (TL = 0.1) | 0.0999 (0.097–0.101); the door 7.80 | `door_step_seconds` 0.1 |
| A fate's sound (TL = 1.08) | 1.17 | `fate_sound_seconds` 1.17 |
| A fate's figure copy (`GOSUB 0x869`) | 4.04–4.14: the ghost morphs while it runs | `fate_copy_seconds` 4.1 |
| A fate, whole | 7.4–7.7 (0.12–0.41 of BASIC before the sound) | |
| `WIN`'s empty `FOR 4000` | 4.169 (1.04 ms a pass, the band IRQ running) | II `grant_ticks` 4170 |
| `WIN`'s name routine `0x9fb` (prints + `FOR 1000`) | 1.087 | II `line_ticks` 5260 (+ `FOR 4000`) |
| `WIN`'s cheer `0x6ba` (its sound `0x749` 9.92) | 10.22 | II crowd `flash_ticks` 9920 |
| `WIN`'s intro page (the hall's load) | 16.85 (true drive) `[~]` | II `intro_page.seconds` |

Seen and confirmed: the rows 18–24 speech, word by word, with the face on; the clouds switched on at y 255, then 91 steps
240 → 150, before the wipe (Zeus absent while they rise); the cloud places (4 members: x 90 / 60 left, 240 / 270 right);
light grey for a status-0 member; the rising light-grey door; the fates: UNDEAD figure and line cyan with its name on row
21, RESURRECTED yellow, DESTROYED's line dark grey and its slot empty (the sprite goes off ~0.035 s after the colour);
II's presented member light red in its attacking pose in its own place; II's intro and closing pages black letters on
yellow (the closing page prints with the hall's inverted characters still loaded). Screenshots: `parity/s223-vice-*.png`.

**s224, `WIN` again** (same harness, rebuilt: RTL-64 at `$A000` and `$02FF` = 100 so the stub loads nothing, UTIL-3.O,
LOUIS and THUNDAR at `SA` with `P(168)` = 12 / 14 and their `HSPRIT` pairs at `$D300`, `g $0819`; logs not kept):

| What | Seconds |
|---|---|
| The hall on (`POKE 646,3`) → `TLUM1`'s load starts (the `FOR 2000`) | 1.743 |
| `TLUM1`'s load (true drive) → the bands start | 7.745 `[~]` |
| The bands → the header printed → its `FOR 4000` starts | 0.025, 0.032 |
| The header's `FOR 4000` | 4.171 |
| A cheer: `GOSUB` → pointers + 8 → the sound → its end (pointers − 8) → the panel cleared → the next step | 0.102, 0.058, 9.928, 0.058, 0.088 (10.23) |
| A member: `FOR 4000` → the pose and titles (`0x9fb`, its `FOR 1000` included) → `FOR 4000` → the cheer | 4.172, 1.094, 4.172 |
| The last cheer → the wands prompt up | 0.095 + 0.062 |

A watchpoint on stores to the crowd band's pointer bytes (`$CBF4`–`$CBFA`, `TLUM1`'s record at `$CBDA` + 26) from the start
to the wands prompt: 7 stores by the KERNAL load (`$F51E`, `TLUM1` itself) and 42 by RTL-64's `POKE` (`$B604`) = 3 cheers ×
(7 × + 8, 7 × − 8); `WIN` calls UTIL-3.O's `SY+57` (bands) and `SY+60` only, never `SY+51` (the animator). So **no interrupt
alternates the crowd**: one frame change a cheer `[C]` (s223's `[~]`). Shots `parity/s224-vice-p2-ending-*.png` (the
"SEARCHING FOR TLUM1" lines are the harness's, KERNAL messages on in direct mode).

### 9.6 Sounds (SID): read s224, checked again and played s226 `[C]`

Every end-screen sound is plain SID register writes; none is a sample or a tune table. s226 read each again from the
bytecode (the `POKE` offsets from `S` = 54272) and `ZALL`'s 6502, which corrected three of s224's readings: the thunder's
clear loop pokes `$D400` only, the defeat's first sound is voice 2 (not 3), and the clear loops differ in range.

| Where | What it writes (`$D400` + n, in order) | Timing |
|---|---|---|
| `ZALL` `$4CC6` → `$4D40` (P.END `0x4f4`, right after the screen is cleared for the rise) | Y = 28 down to 0: n := the byte at `$4E65` + n (`00 64 00 00 80 F0 1E 00 64 00 00 10 F0 C0`, ten 0, `0F`, four 0: voice 1 noise, AD `$F0` SR `$1E`, voice 2 triangle, AD `$F0` SR `$C0`, freq hi 100 both, gates off, volume 15); then `$0314` → `$4D73`, the wind below | once a jiffy (the KERNAL IRQ, 60 Hz on PAL and NTSC) |
| `ZALL` `$4CC9` → `$4E46` (P.END `0x644`: after the wipe and its TD = 2, s224's `[~]` settled) | Y = 28 down to 0: n := 0; `$0314` back | at the first strike |
| P.END `0xbb6`, the thunder (first in each strike) | `FOR I = S TO S + 22: POKE S, 0` (S, not I: only `$D400`, 23 times); `$D418` = 143 (volume 15, voice 3 off), AD 10 / 10 (voices 1, 3), SR 170 / 169, `$D404` = 21 (triangle, ring, gate), `$D412` = 17, freq hi 68 / 42 (voices 1, 3); TD 0.3; `$D404` = 20, `$D412` = 16; TD 0.2; `FOR 1400` | 1.79 s (9.5) |
| P.END `0xc43`, a click (after each space; after each line) | `$D400` = 188, `$D401` = 2, `$D418` = 15, `$D407` = 15, `$D408` = 0, `$D40C` = 0, `$D40D` = 244 (voice 2: freq 15, AD 0, SR `$F4`); `TI$` = "000000"; `$D40B` = 21 (triangle, ring by voice 1, gate); `$D400` = 110 + RND · 70; `TI` to IT; `$D40B` = 16; IT := RND · 8, `TI` to IT | IT = 16 jiffies after a space (the caller), the last RND · 8 after a line |
| CEMETARY `0x90e` (step 4, after the last ghost) | `FOR V = 0 TO 20: POKE S + V, 0`; `$D415` = 7, `$D417` = 242 (resonance 15, voice 2 filtered), `$D418` = 31 (low-pass, volume 15), `$D416` = 0, `$D40D` = 240, `$D40A` = 4, `$D40F` = 48, `$D40C` = 245, `$D408` = 8, `$D416` = 0, `$D40B` = 135 (voice 2: noise, ring, sync, gate; AD `$F5`, SR `$F0`) | never gated off: it sounds through the TL = 2, the door and the demon until the first fate's clear |
| CEMETARY `0x97b`, a fate's sound (`CEMETARY2` the same bytes) | `FOR V = 0 TO 24: POKE S + V, 0`; `$D401` = 3, `$D405` = 41, `$D40F` = 30, `$D403` = 8, `$D418` = 31, `$D417` = 4, `$D415` = 1, `$D404` = 21 (voice 1 triangle, ring by voice 3, gate; AD `$29`, SR 0); TL = 1.08; `$D404` = 20 | 1.17 s |
| WIN `0x749`, a cheer | `FOR V = 0 TO 24: POKE S + V, 0`; AD 238, SR 172, `$D416` = 126, `$D417` = 0, `$D404` = 133 (noise, ring, gate), `$D418` = 79; `FOR F = 11650 TO 10500 STEP -40` (`$D400` low, `$D401` high byte, 29 passes); `$D404` = 132; `FOR 400`; `$D404` = 133; `FOR F = 10500 TO 11650 STEP 40` (29); `FOR V = 15 TO 0 STEP -1: POKE S + 24, V: FOR M = 1 TO 500` (the filter mode bits go with the first step; no voice is filtered) | 9.93 s |

**The wind** (`ZALL` `$4D73`, its state `$4E5F` pitch, `$4E60` period, `$4E61` R, `$4E62` state, `$4E63` count, `$4E64`
counter; the random bits are CIA 1 timer A's low byte `$DC04`) `[C]`: state 3 (start: counter 2) counts its counter down,
then both gates on (`$81`, `$11`), R := 1 + (rnd AND 1), the period := 1 + (rnd AND 3), state 0. State 0, the rise: every
period jiffies the pitch + 1 into both voices' freq hi; at 200 both gates off and a pause of 1 + (rnd AND 7), state 1.
State 1: the period := 1 + (rnd AND 1), state 2; on the gust's last fall (counter = R) voice 2's freq hi := 0. State 2, the
fall: every period jiffies the pitch − 1 into voice 1 (and voice 2 unless the last fall); at 100 the counter + 1: while
≤ R rise again (a new period 1 + (rnd AND 3)), else state 3 with the counter := (rnd AND 127) + 31 jiffies of silence (the
`ADC #$1E` runs with the carry of the compare before it set). The gates go off at the top of each rise, so every fall is
the release (voice 1: release 14, so the noise dies slowly; voice 2: release 0). `$DC04` is read as uniform `[~]`; the
hook runs at the KERNAL IRQ's 60 Hz (no raster IRQ is running before P.END's step 7) `[~]`.

**Played (s226).** The pack's `sid_sounds` (both C64 packs; `C64PhantasieArtManifest.SidSounds`) holds these writes as
data, rendered on the Client's 6581 model (`Sid6581`, PAL 985,248 Hz: three oscillators, the four waveforms, the noise
register, ring modulation and sync, the envelope's rate counters with the exponential divider and the 15-bit wrap; the
filter an approximate 6581 curve `[~]`; combined waveforms as an AND `[~]` (none used here); no DC offsets `[~]`). Each
screen builds its whole score when it starts (`SoundCues`: I's wind from the rise to the first strike, a thunder per
strike, a click per space and line end; the defeat's sound after the ghosts, a fate's sound per member; II's cheer per
cheer) and plays it on one chip, so phases and envelopes carry over as on the C64; it stops when the screen ends or the
Sound switch is off. Times: the `TI` waits as written; the cheer's sweep pass `[~]` = its measured 9.928 s less its
`FOR 400` and 16 × `FOR 500` at `WIN`'s 1.042 ms a pass, over its 58 passes. Live: `parity/s226-c64-sid-live.md`.

**Other SID sites, not decoded `[?]`** (s226 byte search of the v1.4 / v1.2 PRGs for the float 54272 and 6502 stores to
`$D4xx`): I `OUTDOORS` (`0x283`–`0x2ab` the base variables `$D400`, `$D401`, `$D404`–`$D406`, `$D418`; code near
`0x6703` / `0x6710`) and `DUNGEON` (`0x230`–`0x258`, `0x66a5` / `0x66b2`): the fights' sounds; I `TOWNR` `0x5041`–`0x5154`
(`$D400`–`$D406`, `$D40F`, `$D418`): a town sound; II's `OUTDOORS2` (`0x2e1`, `0x6c4f`), `DUNGEON2` (`0x22c`, `0x6c16`) and
`TOWN2` (`0x5527`–`0x563a`) the same. III: `SND` (177 B at `$126E`, 6502 writing voice 1) and `PHANT.OBJ`'s stores to
`$D40E`, `$D40F`, `$D412` (`$1EDE`–`$1EE4`: voice 3 noise, the usual random-number source) and `$D418` (`$213A`).

### 9.7 PAL or NTSC `[~]` (s224)

Nothing in the programs picks one: no read of `$02A6` (the KERNAL's PAL / NTSC flag) in any v1.4 or II v1.2 file; the wipe
counts frames on `$D011` bit 7 and UTIL-3.O's band IRQ compares raster lines from its records (all below 256), which run
on both. C64PP tags some copies `(ntsc)` (I v1.0, v1.2), but v1.2 `(ntsc)` and the untagged v1.3 carry the same program
files, so the disks hold no region: the held v1.4 pair's untagged name says where that copy came from, not what it runs
on. The waits counted in frames (the wipe, the face / hand animator) are a PAL machine's (`vbl_hz` 50, as measured in
VICE); an NTSC C64 runs them 6/5 faster. Kept PAL; which machine a player had is not knowable from the images `[~]`.
