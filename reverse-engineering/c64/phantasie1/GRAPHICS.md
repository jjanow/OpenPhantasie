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

**The ending and the party wipe (s221, section 9.3):** the ending runs on the remaster's ending screen
(`ending_scene`, the Amiga's steps: picture, the row of clouds, the hand, a figure per member, the speech in yellow on
text row 21); the party wipe on DOS's pages (`defeat_pictures`, `P1DosDefeatScene`: the art follows the pack, whichever
layout is on). Live: `parity/s221-c64-end-screens-live.md`. Not modelled `[~]`: Zeus's bottom-up wipe (13 s; here the
shortest fade, `fade_vbl` 1), the rising clouds beside him (here a row centred under him), the hand flashing per strike,
the talking face and the grant hand (their states are written: `zeus-speech.png`, `zeus-grant.png`, sprites `c0`–`ce`),
the figure's light grey for a member with status 0, the rising door (`temple-door.png`, `door.png`: no slot; the Amiga's
door sinks), the fate colours (UNDEAD cyan, DESTROYED dark grey: DOS's rule clears a destroyed slot), the C64's slot
positions and its line colours (one colour, blue); waits and VBL rate not measured.

**Falls back (no C64 picture):** UNDEAD's portrait and `defeat.figure.15` (CSPRIT has no UNDEAD figure: the Client
draws none, as for any missing portrait); the title's credits screen, `ui.decor` / `decor.*` (the Amiga's own defeat
screens: its sinking door, the sorry sign), combat effects (`combat.grave`: the C64 shows no grave; `.sparkle`: the
`$E000` sparkle sprite is not mapped), the town doors; these slots draw the Client's own default (black / text), as
III's Apple pack does. Text: the remaster 5 × 7 font (the C64 sets are written as sheets under `font/`, the ending's and
the defeat's as `ending/font.png`, `defeat/font.png`).

`[?]` left: `DOOR`'s colour (no program shows it); the hero colour rule (yellow / light blue); the water's
moving characters (not drawn); what sets `P(168)` (the ending's figure); sprite blocks `$C8`, `$CA`, `$CB`, `$CD`,
`$CE` not seen live; the code printing the town buildings and the dungeon map.

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
| 3392– | `$4D40` | 6502: a raster-interrupt sound routine (SID `$D400`–`$D418`) `[~]` |

`P.END` first clears characters `$60`–`$FF` (`SYS Q,60160,60161,1280`), so Zeus is drawn only by the wipe; its copy of the
character set leaves character `$FF`'s last byte 0 (the file holds 4; `$FF` is not drawn). Zeus's cells: text columns
13–24, rows 2–14 (`SYS Q,ZX,ZY,ZX+11,ZY+12,1,96`, `ZX` = 13, `ZY` = 2), white (1): a 96 × 104 picture at screen (104, 16).

**Sprite blocks** (`$F000` = block `$C0`):

| Block | Picture | Live use |
|---|---|---|
| `$C0`–`$C5` | Zeus's right hand, six frames | sprite 7 at (200, 74), white, while P.END prints spaces over the hand's six cells (rows 3–4, columns 22–24): seen with `$C5` at the grant `[C]`; the order of the frames `[~]` |
| `$C6`–`$CD` | Zeus's face, eight frames | sprite 6 at (161, 77), white, over the face's four cells (rows 4–5, columns 18–19, printed as spaces): while the speech is typed it cycles `$C7`, `$CC`, `$C9`, `$C6` `[C]`; `$C8`, `$CA`, `$CB`, `$CD` not seen |
| `$CE` | a striped ball | not seen `[?]` |
| `$CF` | the hand sending lightning | sprite 7 at (202, 62), light blue (14), switched on and off while each member is struck `[C]` |
| `$D0` | the members' cloud (multicolour) | sprites 0 … n, colour 2, `$D025` (MC1) flashing 7 / 2, `$D026` (MC2) 2 `[C]` |

**Sequence** (live, `PEEK(SA + 1203)` poked to 0 and to 4): the "INSERT BOOT DISK AGAIN" box; `ZALL` loads; a black
screen with "You have arrived at the astral plane" (yellow, row 23) and **one cloud per slot** `0 … PEEK(SA + 1203)`
(P.END tests no slot; with 4 poked over a four-member party, slot 4 was empty and its figure came out as junk, block
`$40`: a state the game itself would not make): sprites 0–2 at x 90, 60, 30 and 3–4 at 240, 270, rising from y 231 to 150. The wipe (about 13 s).
Per member Zeus strikes (`$CF` flashing) and the cloud becomes the member's figure: **pointer `64 + P(168)`** (`P.END`
`0x6a0`: `POKE screen + $3F8 + member, FN P(168) + 64`), a block of `FONT+HER`'s copy at `$D000` (blocks `$4C`, `$4E`,
`$52`, `$54` seen = `HSPRIT` blocks 0, 4, 12, 16), multicolour off, colour 7 if `P(18)` (status) ≠ 0, else 15; an OK
member also gets `POKE C(118),1` (spell 58, "Your First Divine Spell"). Then "Zeus smiles and says" and the speech, typed
a letter at a time in yellow from row 21 with the face talking; the hand at the grant; a key; a text page without
pictures ("Now Your Party will be transported to The City of Pelnor …", green); `FONT` back to `$E800`; `TOWNR`.

`P(168)` (record byte 168, in FORMAT.md 3.5's `[?]` range) is the figure's block offset: GRUNT 12, HIT 20, SCOTT 18,
BORACK 14 with races 0 / 3 / 2 / 2, so it is not the race; what sets it `[?]`.

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
at (148, 81), in the doorway; "A GREAT DEMON APPEARS AND SPEAKS..." (blue). Per member with a level (`P(54)` ≠ 0): its
name (row 21), then its ghost block is rewritten in place into `CSPRIT` block `P(145)` (`CEMETARY` `0x877`: from
`8192 + 64 · P(145)`; seen: GRUNT block 0, HIT 3, SCOTT and BORACK 2), coloured as the fate's line: **GRANTED UNDEAD
STATUS 3 (cyan)**, **DESTROYED 11 (dark grey)** (both seen), RESURRECTED 7 (yellow: the code's value, not seen) `[~]`. A
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
two scenes pick differently: the ending by `P(168)`, the defeat by `P(145)` (HIT and SCOTT showed other figures in each). Not found on the C64: a grave (`combat.grave`) and a sorry sign
(`decor.sorry-sign`); they stay fallbacks. The ending's cloud and the defeat's ghost are different pictures (the Amiga
uses one ghost for both).
