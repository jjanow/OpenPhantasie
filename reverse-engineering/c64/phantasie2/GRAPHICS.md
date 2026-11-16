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

Written by the `p2-c64` import (recipe 2, `P2C64Art`, `P2C64ArtManifest`; the steps shared with I in
`C64PhantasieArt`, the manifest in `C64PhantasieArtManifest`) under `reverse-engineering/c64/phantasie2/extracted/graphics`
(248 PNGs, the repo's extract of the C64PP v1.2 never_used pair; the held `.d64` pair gives the same pictures). In
`index.json`'s `default_order` after the ST and the Apple II: drawn when neither of those copies is imported. Slots and
fallbacks as I's pack (I's GRAPHICS.md 8). Not drawn: the item / feature pictures (`items/`), the ending (`PH2.END-4`,
`TLUM1`), the party-wipe screen (`CEMETARY2`'s files), II's `decor.*` / `ending.*` / `combat.*` slots (the Client's own
drawing).

`[?]` left: the item picture colours but EXIT (EXIT green, 8 × 6 at text column 10, row 11, live); the hero colour in a
party of more than two; `PH2.END-4`, `TLUM1` and `CEMETARY2`'s pictures not decoded; the code that prints the town and
the dungeon map not read. `[~]`: the attack rule for the 29 several-sprite monsters not watched.

## 7. Live check in VICE (s219)

VICE 3.10 `x64sc` (flatpak, Xvfb, sound off, remote monitor) on writable copies of the C64PP v1.2 never_used pair. RAM
(64 K) and I/O (`$D000`–`$DFFF` with colour RAM) dumps on: the town (no party, then a two-member party; `FONT+HER` at
`$CFDA`, `FONT` at `$E800` and `HSPRIT` at `$F000` byte-equal to the files), region 1 at Pippacott (all 520 map cells
equal the routine's output), two fights (BANTIR × 3 at `$CB5A`; VIPER, by poking DNG 1's forced-fight entry 0 from 9
(KOBOLD) to 6, band records polled while they attacked), DNG 1 (natural cells, then cells next to the party poked to
213 / 235 / 218 / 200 / 209 / 185 / 220) and the EXIT picture. The dumps and snapshots are not kept in the repo.
