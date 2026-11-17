# Phantasie III (Commodore 64, SSI 1987): pictures and the `c64` art pack

How III's C64 pictures are stored and drawn, and how the `p3-c64` / `p3-c64-1.2` imports turn them into the `c64`
graphics pack (s220). **Not I / II's engine** (`../phantasie1/GRAPHICS.md`): III is 6502 machine code with its own
files. Files and tables: `FORMAT.md` (section 6 points here). Tags: `[C]` read in the code or seen on the live screen and
checked against the files; `[~]` probable; `[?]` not known.

**Addresses.** PHANT.OBJ loads at `$1400` and runs at file address − `$1000` (runtime `$0400` = file offset 0);
DUNGEON.OBJ loads at `$2000` and runs at file address + `$3C20` (its pointers to `PSET0` and `TOT` land on the strings); DATA, MONSTERS and the pictures run where they load.
Offsets below are runtime addresses unless "file offset" (after the 2-byte load address).

**Evidence (s220).** A throw-away capstone disassembly of PHANT.OBJ and DUNGEON.OBJ (the fight's raster chain
`$4931`–`$4BAA`, the group set-up `$272C` / `$2836`, the party figures `$1D69`–`$1DCB`, the elemental `$5868`, the
map band `$04CA` / `$04F5`, the ending `$5C7A`); live in VICE 3.10 `x64sc` (flatpak, Xvfb, sound off, remote monitor)
on writable copies of the C64PP v1.0 (!) pair: RAM + I/O dumps of the town, the wilderness at three places, three
fights (Wild Cat, Sniverling, Scribe + Ranger), Dungeon 5 (`DNG1`) at three squares and the title (section 9). The
dumps are not kept in the repo.

**Palette.** Colodore (`C64Gfx.Colodore`, VICE's `colodore.vpl`), the pack's `palette`, as I's and II's packs.

## 1. Memory map while playing `[C]`

VIC bank 3 (`$DD00` = `$C4`): screen `$CC00`, sprite pointers `$CFF8`–`$CFFF`, sprite block n at `$C000 + 64 n`.
Character set `$D000` (`$D018` = `$35`) in the wilderness, fights and the dungeon: **PSET0**; `$D800` (`$D018` = `$37`)
in town: **PTOWN1**, which stays resident at `$D800` everywhere. MONSTERS' 78 picture blocks at `$E000` (block `$80`),
byte-equal to the file. A party member's three figure blocks at `$C780 + 192 slot` (blocks `$1E` + 3 slot).

**Raster split** (town and wilderness): `$04CA` at line 0 sets multicolour text (`$D016` bit 4, extended colour off) with
`$D022` / `$D023` from its own operands, then `$04F5` at the split line (town 72, wilderness 192) switches to extended
colour text for the panel below. `$D021` stays 0.

| Screen | upper band `$D021` / `$D022` / `$D023` |
|---|---|
| Town (the building strip) | 0 / 15 / 9 |
| Wilderness and fights (the map) | 0 / 1 / 5 (`$04E0`: constants) |

## 2. Monsters `[C]`

**Record byte 27 is the sprite pointer** (FORMAT.md 3.1): picture p shows VIC block p, MONSTERS file block p − 128
(Skeleton `$80`, Wild Cat `$88` seen live). Boo Boo Kitty's 126 points at `$DF80`, the last 128 bytes of PTOWN1 (live:
RAM `$DF80` = PTOWN1 + `$780`): a cat, two blocks, as the others.

**Colours:** two 80-byte tables indexed by the monster number (`$272C`: `LDA $37C0,Y` → `$4905,X` (`$D025`) and its high
nibble → `$4907,X` (`$D026`); `LDA $3810,Y` → `$4909,X`, the row's sprite colour; bit 7 set → hires, else the row's
multicolour mask `$3E`). File offsets in v1.0 `$33C0` / `$3410`, in v1.2 (alt) 2 bytes later. 34 monsters hires, 46
multicolour. Multicolour sprite pairs: 01 `$D025`, 10 the sprite colour, 11 `$D026`.

**Field** (the raster chain from `$4931`): normal: two rows of up to 5 sprites (group 2's row at y `$3E`, group 1's at
`$5E`), x `$1C` + 32 i, each row with its own pointers, colours and mask. **Giant** pictures `$A8` and `$BA`–`$CD`
(`$2836`): one row at y `$53` and, at the raster `$67`, the same sprites at y `$68` with every pointer + 1: **block p over
block p + 1** (24 × 42); the lower half takes the row's mask, but picture `$CA` (Great Beast, Ancient Beast) is hires
below. Group 2's picture is cleared (no second row).

**Attacking:** `$3060` `LDA $0BE3,Y; SEC; ADC $3059` → the pointer + 1, + 2 for a giant (`$3059` = 1 in the giant
layout): the next block, or the next pair (live: Wild Cat `$88` → `$89`). Back to idle at the next round.

The pack: `monster.N.idle` / `.attacking` for all 80, 24 × 21 (24 × 42 for the giants).

## 3. Party figures `[C]`

`HEROSPRITES` (3,456 B) = 18 sets of three blocks: idle, attacking (`$30BD` `INC $0BE3,X`), and a third (`$34D4`, two
`INC`s: a spell `[~]`; not seen live). `$1D94`: record byte 169 (class) not 0 (Thief) or 3 (Fighter) → the class's set
`$1DF2[class]` in colour `$1DF8[class]`; otherwise the race's set `$1DFE[race]` (byte 161) in colour `$1E0E[race]`. The
party row is hires (`$D01C` AND 1).

| Class | set, colour | | Race | set, colour |
|---|---|---|---|---|
| Monk | 6, 14 | | Human 0, 7 · Dwarf 3, 15 · Elf 8, 5 · Halfling 4, 10 | |
| Ranger | 7, 5 | | Gnome 1, 14 · Pixie 16, 7 · Sprite 16, 7 · Ogre 9, 10 | |
| Priest | 5, 4 | | Gnoll 2, 8 · Troll 12, 5 · Kobold 13, 13 · Orc 11, 8 | |
| Wizard | 6, 14 | | Goblin 10, 2 · Lizard Man 14, 5 · Minotaur 15, 8 · Undead 17, 1 | |

Seen live (PCHARS0's six): Troll Fighter → 12 green, Human Ranger → 7 green, Sprite Thief → 16 yellow, Elf Monk and Elf
Wizard → 6 light blue, Gnome Priest → 5 purple.

**The elemental** (party slot 7, `$5868`): pointer `$B2` (the Mud Elemental's monster picture) in `$5887[slot − 18]`:
Air 1, Mist 14, Mud 8 (ELEMENTALS' order `[~]`).

The pack: `portrait.{race}` (16 races) and, new in s220, `portrait.class.{class}.idle` / `.attacking` for Monk, Ranger,
Priest and Wizard, which the Client draws before the race's (`AssetCatalog.GetPortraitIdle(Character)`); the casting
frame is written (`portraits/casting`, `portraits/class/casting`) but not drawn. `portrait.elemental.*`: Air's colour
`[~]` (one slot; the three colours are in `portraits/elemental/`).

## 4. The title `[C]`

`TITLE` = a Koala picture (bitmap 8,000, screen 1,000, colour 1,000, background 1; loaded at `$6000`; shown with the
bitmap at `$E000` and the screen at `$CC00`, `$D018` = `$39`), multicolour bitmap, background 0. v1.2's differs in its
version line. `title.cover`, shown until a key.

## 5. The town `[C]`

Text rows 0–2 print characters `$40`–`$B7` of PTOWN1 in reading order (40 a row), multicolour, the colour-RAM nibbles
from the 120 bytes at `$055D` (both builds); row 3 prints GUILD ARMORY MYSTIC BANK INN LEAVE TOWN, row 4 the town's name.
`town.backdrop` = the strip at y 16 of a black 320 × 200 (the Client's bar sits above it; its town name and menu are the
Client's own).

## 6. The wilderness map `[C]`

A 10 × 9 view of 2 × 2-character cells (160 × 144) in the map band; the party always on view cell (4, 4) (three places
seen; at the map's edges not watched `[?]`). Cell byte t prints, top left / top right / bottom left / bottom right,
`DATA` file offset `$100 + 256 k + t` (character) with colour-RAM `$500 + 256 k + t` (live: every cell of three views).
The party's cell shows `$71 $74 $73 $72` in colour-RAM 11 instead.

**DOS's bytes:** the remaster plays DOS III's maps. The C64's `MATERIALMAP` keeps DOS's byte in 3,685 of 3,750 cells;
water 0 / 1 are swapped, the town tiles 30–38 permuted, and DOS's desert and mountain edge tiles are one C64 tile each.
The pack's `tile_swaps` (Scandor only, `P3C64ArtManifest.ScandorSwaps`) draw each DOS byte as the C64 tile found most
often in its cells `[~]`; the planes (C64 layouts not read) keep the byte's own tile `[~]`.

The pack: `overworld.tile.0`–`255` (16 × 16), `overworld.party`, `overworld_view` [10, 9], `overworld_party_cell` [4, 4]
(new in s220: the Client keeps the party on that cell; the view's edge stops are the Client's own).

## 7. The dungeon map `[C]`

No character map: DUNGEON.OBJ (its own interrupt `$6FA3`, extended-colour text) shows a 144 × 105 **multicolour sprite
bitmap**: sprites 0–5 at y `$32`, x `$20` + 24 i, re-used down four more bands (`$6C8C`, every 21 lines, pointers + 1),
blocks 0–29 (column c, band k = block 5 c + k), sprite colour 5, `$D025` 7, `$D026` 12, black ground. `$6CF8` fills them
grey. The level's 24 × 30 cells are 3 × 3 multicolour pixels at pixel-pair column 3 x, row 3 y + 3: the whole level
shows. Glyphs: 32 bytes each at `$6E77` (file offset `$1257`), four shift phases of four 16-bit rows (high byte first);
phase 0's top three rows:

| Glyph | 0 | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|---|
| Picture | blank | grey | a dot | an upright yellow bar | a level yellow bar | the exit's arch | the party |

Cells: walls and not-yet-seen cells grey; seen live 17 and 141 blank, 200 and 208 the upright bar (doors), 215 the exit,
the party's square the party. 185–199 → the level bar and 221+ → the dot follow DOS's ranges (ovl4 `0x21bb`) `[~]`
(the draw routine's test not read). The upright bar has a fourth row, drawn into the next cell's top `[~]`; the pack
keeps three.

## 8. The endings and the defeat `[C]`

`GUT`, `TOT`, `SCHLECHT` (4,096 B at `$8000`): a 2,048-byte multicolour character set, 1,000 screen codes, 24 spare
bytes, 1,000 colour-RAM bytes. DUNGEON.OBJ `$5C7A` loads one by its table (`$5D83` / `$5D86`: TOT, GUT, SCHLECHT) and
sets `$D021` 0, `$D022` / `$D023` = (8, 9), (14, 10), (11, 8). Picture 0 (TOT) comes from DUNGEON.OBJ's third entry
vector (`$5C26` → `$5C3E`: A = 0), which PHANT.OBJ `$1AA8` jumps to when `$4C5B` finds no member alive: after a fight
round (`$2CDF`) and after a wilderness step (`$4CC8`), the party's wipe. Pictures 1 / 2 (GUT / SCHLECHT) come from
`$81C8` with the negated result of the last fight (`$81FB` < 0). Rendered whole: TOT the horned demon on a burning
mountain, GUT Zeus over clouds with six angels, SCHLECHT Pluto with six demons. The pack: `scene.astral-plane` (TOT),
`scene.reward.good` (GUT), `scene.reward.evil` (SCHLECHT). Not seen on the live C64 (the dumps did not reach an ending).

## 9. Live check in VICE (s220)

Disks: writable copies of the C64PP v1.0 (!) pair. Dumps: the town before and after the Guild, the Armory; the
wilderness at Pendragon (view origin (20, 33)), (17, 33) and (17, 36); fights with Wild Cats, Sniverlings and a Scribe +
Ranger; Dungeon 5 at (11, 0), (11, 1), (11, 3); the title. Checked: MONSTERS at `$E000`, PSET0 at `$D000`, PTOWN1 at
`$D800`; every map cell of the three views against DATA's tables; the band registers; the party's six figure sets and
colours; the Wild Cat's pointer, colour and attack frame; the dungeon bitmap's cells against `DNG1`.

## 10. The `c64` pack (`GameData/phantasie3/graphics/c64.json`)

Written by the `p3-c64` / `p3-c64-1.2` imports (recipe 2, `P3C64Art`, `P3C64ArtManifest`) under
`reverse-engineering/c64/phantasie3/extracted/graphics` (497 PNGs; the repo holds the C64PP v1.0 pair's). Third in
`index.json`'s `default_order`: drawn when neither the Amiga nor the Apple copy is imported.

**Falls back (no C64 picture):** the grave, body figures, scroll frames and Scroll IV's picture, the reward scene's
flashes, the combat flash, the town doors, the buttons; the found-item pictures (III's C64 has none). Text: the remaster
5 × 7 font (PSET0 / PTOWN1 as sheets in `font/`).

`[?]` / `[~]` left: the view at the map's edges; the plane maps' tiles; the dungeon's 185–199 / 221+ glyphs and the bar's
fourth row; which elemental colour the pack shows; the figures' third frame. The sound: below.

## The sound: `SND` (s228) `[C]`

`PHANTASIE III` loads `PHANT.OBJ` at `$0400` (its PRG header says `$1400`: run-time addresses are `$1000` below the
file's); `PHANT.OBJ` then loads `SND` (177 B, 6502) at `$126E`, over its own copy routine (the two `JSR $126E` at file
`$217C` / `$2196` run before, copying character sets: not sounds). `SND`'s jump table:

| Entry | Writes (voice 1) | Called (file address) | When |
|---|---|---|---|
| 0 `$127D` | freq hi A, control `[$12A3]` ($10), freq lo `[$1287]` (0), pulse $8080, AD 0, SR `[$1299]` ($F6), control `[$129E]` ($11, triangle gate) and 6 cycles later `[$12A3]` (gate off) | `$4129`, `$4136`, `$4140` (A = $90, $B0, $90, 13 frames apart, `$30B6` with X = 2) | a party attack spell's flight to each monster it strikes: FIREFLASH 1–4, MINDBLAST 1–4 (a monster that fails its check), FLAMEBOLT 1–4; then the hit |
| 1 `$12A8` | control $80, freq hi $80, AD $71, SR 0, control $81 (noise; the gate left on) | `$2794`, `$3C21` | a member's miss (PARTY MISSES: THRUST / ATTACK / SLASH / LUNGE per swing, AIM BLOW, BOW) and a monster's (MONSTER MISSED) |
| 2 `$12C2` | entry 0 as noise ($81 / $80) | `$3B43` (A = $42: never loaded; the print routine `$0D46` leaves the low byte of "AND HITS"'s last text byte, `$2B42`), then 10 jiffies | any damaging hit (party melee, BOW, attack spells, a monster's attack or spell) |
| 3 `$12D9` | freq 0 | none | unreachable |
| 4 `$12E2` | SR $FA, freq lo $B4, entry 0 with A = 4; `$1310` twice (X = $FF: 255 × 1,791 cycles, 0.46 s each); freq lo $30, entry 0 with A = 4; then lo 0, SR $F6 again | `$4071` | a member's --DEATH-- (target slots 1–7) |

Each result line then waits 82 jiffies (`$1BB9`). `$D418` = 15 once at boot (`$213A`); nothing else writes the volume.
Voice 3 noise (`$D40E`, `$D40F`, `$D412` = $81 at `$1EDE`, `DUNGEON.OBJ` `$441F`) is the random-number source: its AD/SR
are never set and its gate never cleared, so it sounds at most once after power-on `[~]`. Entry 0's gate is on for 6
cycles with attack 0 (a step every 9 cycles): the spell pings, the hit and the death are a faint tick or silent unless an
interrupt falls inside the window `[~]` (not heard in VICE). v1.2: the same `SND`; `PHANT.OBJ` makes the same calls 3
bytes later, so its hit's A is $45 `[~]` (the pack keeps v1.0's $42).

**Played (s228):** the pack's `sid_sounds` (`fight.miss`, `fight.hit`, `fight.ping`, `fight.death`; each write at its CPU
cycle, the boot volume first) on `Sid6581`, from each round shown on the remaster's combat screen (I's GRAPHICS.md 9.8,
`C64PlaySoundCues`), in turn `[~]`.
