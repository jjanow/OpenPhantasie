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

## 7. Item and feature pictures

The 14 files of FORMAT.md section 2 are character data loaded into the map's set at `$EAC0` (characters `$58` on) and
printed numbered down each column: 392-byte ones 7 × 7 characters (56 × 56), `EXIT` (384 bytes) 8 × 6 (64 × 48); `EXIT`
in green (5, live). The others' colours are set by the programs `[?]`. Written to `items/` (white but EXIT), **not
drawn**: the Client has no slot for a found-treasure or dungeon-feature picture.

## 8. The `c64` pack (`GameData/phantasie1/graphics/c64.json`)

Written by the `p1-c64` import (recipe 2, `P1C64Art`, `P1C64ArtManifest`; since s219 the steps and the manifest's
layout are shared with II's pack in `C64PhantasieArt` / `C64PhantasieArtManifest`, `../phantasie2/GRAPHICS.md`) under
`reverse-engineering/c64/phantasie1/extracted/graphics` (248 PNGs since s219; the repo holds the extract of the C64PP v1.4 pair).
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

**Falls back (no C64 picture):** UNDEAD's portrait (none: the Client draws no figure, as for any missing portrait);
the title's credits screen, the ending
(`ending.*`: `ZALL` / `P.END` not decoded), the defeat screens (`CEMETARY`'s `SKEL2` / `CSPRIT` / `FONTO` not
decoded), `ui.decor` / `decor.*`, combat effects (`combat.grave` / `.sparkle`; the `$E000` sparkle sprite is not
mapped), the town doors; these slots draw the Client's own default (black / text), as III's Apple pack does. Text: the
remaster 5 × 7 font (the C64 sets are written as sheets under `font/`).

`[?]` left: the colours of the item / feature pictures but EXIT; the hero colour rule (yellow / light blue); the water's
moving characters (not drawn); `ZALL`, `CEMETARY`'s files; the code printing the
town buildings and the dungeon map.
