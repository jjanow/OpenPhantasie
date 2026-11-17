# Phantasie III (Apple II, SSI 1987): graphics

Images: `../original-images/phantasie3/` (the `[cr]` v1.0 sides 1A / 1B / 2A / 3A, and the original v3.0 disk 1
sides A / B in `woz-original-v3.0/`). Scripts: `tools/scripts/extract_a2_p3_graphics.py` (PNGs +
`extracted/graphics/extraction-index.json`; default source the v3.0 WOZ pair, read by `tools/scripts/a2_woz.py`),
`tools/scripts/build_a2_p3_asset_manifest.py` (the Client pack `src/OpenPhantasie.Data/GameData/phantasie3/graphics/apple2.json`).
Importer port: `src/OpenPhantasie.Importer/Pipelines/P3Apple2/P3Apple2Art.cs` + `P3Apple2ArtManifest.cs` (byte- and
pixel-equal, `P3Apple2ArtTests`). Program lines: `detok_apple2_p3.py`. Hi-res colour model and Applesoft shape
format: as II, `../phantasie2/GRAPHICS.md` sections 2-3 (the same `HiresPage` / `ShapeTable` code).
Tags: [C] read from the files / BASIC / 6502 code, [~] probable, [?] not known.

## 1. What art the Apple II version has

| Art | Where | Form |
|---|---|---|
| Monsters (80, two poses) | `MONSTER SHAPES` ($1A00, 1,536 B) drawn by `MONSTER PLOT` ($451F, entry $4582); `NIKADEMUS` ($1F00, 256 B, side B) for monsters 77 / 78 | raw hi-res byte sprites [C] (section 2) |
| Party figures, arms, bow, effects, overworld terrain, dungeon marks | `SHAPES` ($45DC, 913 B, 48 shapes; INTRO3 line 5 points $E8/$E9 at it) | Applesoft shape table [C] (section 3) |
| Game screen frame (name / class / HP boxes, body-part labels, the map and message boxes) | `SCREEN` ($2000, 8 KB; STARTER line 600) | hi-res page [C] |
| Title | `ICE DRAGON` ($2000) + the boot program's DATA rows | hi-res page [C], section 6 |
| Ending (the gods' room) | `WIN PIC` (side B) + `ROUTINE` | scrambled hi-res page [C], section 6 |
| Map-box panels | `PIC1`-`PIC3` (side A), `OUTPIC`, `PIC` (side B), copied by `PICPLOT` | 12 × 96-byte panels [C], section 6 |
| Text on the hi-res screen | `IMPTEXT` / `IMPREVERSE` ($1828) | output routine + 7 × 8 font [C], section 7 |
| Text pages (town, scrolls `SC1`-`SC20`, CEMETARY3's lines) | TEXT mode, the Apple's character ROM | not on the disks |
| Graveyard (CEMETARY3) | HPLOT gate + party figures + the demon from sprite rows | procedural: not extracted [~ could be replayed] |
| Sound | `CALL 770` with pitch / duration at 768 / 769 (`INFO`, $302: as II's), `PEEK(-16336)` clicks | not decoded [?] |

Copies (compared file by file): 1A = 3A and 1B = 2A on every art file; v3.0 disk 1 equals them on `SHAPES`,
`MONSTER SHAPES`, `MONSTER PLOT`, `SCREEN`, `IMPREVERSE`, `PIC*`, `OUTPIC`, `NIKADEMUS`, `DUNGPLOT`, `PICPLOT`, the
MN3x and OT files. They differ on three [C]:
- `ICE DRAGON`: the `[cr]` file is the cracker's screen ("ALBERT FOR YOU ON 27.MAY.87", "SanYauTim 325", an italic
  "The Wrath of Nikademus"); v3.0's is SSI's ("STRATEGIC SIMULATIONS PRESENTS / THE WRATH OF NIKADEMUS / Phantasie III /
  BY WINSTON DOUGLAS WOOD"). The `[cr]` INTRO3 also reads "PIONEER SOFTWARE PRESENT".
- `WIN` / `WIN PIC`: the `[cr]` `WIN` is a 233-byte stub ("CONGRUATION !! YOU HAVE DESTROY THE TERRIBLE WIZARD") that
  shows a "pioneer software" logo; v3.0's `WIN` (3,132 B) is the real ending (section 6). **The `[cr]` copy has no
  ending sequence at all**: worth knowing for the tables / text import.
- `IMPTEXT`: v1.0's glyphs have 1-pixel strokes, v3.0's 2-pixel ones (no colour fringes). `[\]^_` hold different
  body-diagram pieces.
- v3.0's side A also has a working `DEMO` (the `[cr]` one is `CALL -1400`): the party, the PIC1-3 panels and a staged
  fight (section 6).

The pack takes the title and the ending only from a copy whose `ICE DRAGON` / `WIN PIC` are not the `[cr]` files
(SHA-1 49891f37… / d5e303bd…) and, for the ending, whose side B has `ROUTINE`; a `[cr]`-only import gets neither and
the Client falls back to its own screens.

## 2. Monster sprites [C]

`MONSTER PLOT` (6502 at $4582; parameters POKEd to $457F shape, $4580 byte column, $4581 y; OUTDOORS3 / DUNGON3
lines 1550, 2006, 4145): source = $1A00 + (shape − 1) · 64; 3 bytes per row, 21 rows, or 42 rows when shape ≥ 21; each
byte is stored with a plain `STA` to row(y) + column, the row address from the file's own tables (hi bytes at $451F,
lo bytes at $454F, rows 96-143); shape 0 stores zeros (erase). Only the low byte of the source pointer is
incremented (it stays in its 256-byte page; no used shape crosses one). So the 24 slots are 20 small sprites (21 × 21
px) and 2 giants (21 × 42: slots 21-22 and 23-24).

- A monster's pose A is its MN3x record's S% (field 22 of 22, INPUT line 910); pose B (attack, line 2006) is S% + 1,
  or 23 when S% = 21. S% values: odd 1-21 [C]. The 80 records are in the remaster's monster order (names equal; the
  Apple spells LIGHT FIARY, and BEEPING BLOB's name has CHR$(7) bells in it, dropped).
- Positions: X% = 21 + 4 · INT(5 (Y + .5) / (GN% + 1)) byte columns, always odd, so the colour phase is fixed: the
  extract draws at byte column 21 (x 147) and is exactly the game's look. Rank 1 at y 120 (a giant at 99), rank 2 at
  96 (line 1540).
- `NIKADEMUS` is RECALLed over $1F00-$1FFF (slots 21-24) when monster 77 or 78 is met (DUNGON3 line 899): those two
  are drawn from it.
- `CEMETARY3` lines 660-689 copy rows of sprites 5 and 7 (6912 / 7296 = $1B00 / $1C80) to columns 14-16, 18-20, 22-24
  for the Great Demon [C].

## 3. Shapes (`SHAPES`, 48) [C]

| Shapes | Use | Evidence |
|---|---|---|
| 19, 25, 26, 27, 4 | party body (record byte 168): races 7+ 26; DWARF / HALFLING / GNOME / PIXIE / SPRITE 25; HUMAN / ELF 19, or 27 for a MONK / PRIEST / WIZARD; social class 3 (byte 128) turns any but 25 into 4 | TOWN3 2350-2360 |
| 7-19 | one shape (same offset): the body 19 | table |
| 20, 21, 22, 5 | the arm: 20 standing, 21 parry (9210 `IN% = 3`), 22 attack (1695), 5 casting (1760); white, at (19 · slot + 147, 198 − 8 · row) | OUTDOORS3 / DUNGON3 9100-9220 |
| 6 | drawn instead of the body when `PC%` = 2 (down) | 9205 |
| 2, 3 | the bow (LD = 2, FR = 3: XDRAW 1624; DEMO 520 draws 2 then 3) | |
| 1 | the arrow of a miss, XDRAW at (220, 104) | 1628 |
| 23 | spell sparkle over the caster, XDRAW at (Q + 14, PP − 23) | 1762 |
| 24, 47 | spell hit on a monster: XDRAW 47 − 23 · SP | 4132 |
| 30 | dungeon party cursor (XDRAW) | DUNGON3 5175 |
| 31-36 | dungeon marks, 3 × 3 (section 4); 35 is empty in III; 34 is wider than a cell but drawn in HCOLOR 0 | DUNGPLOT |
| 37-48 | overworld terrain classes 0-11 (section 5); 44 (BRIDGE) is empty; 43 is also the party mark (XDRAW 43) | OUTDOORS3 30, 5175 |
| 28, 29, 35, 44 | empty | table |

The extract draws every shape white at x 0 (`shapes/`), every body × arm at x 147 (`figures/`), and the pack's
`portrait.{race}` as II's: the race's body for a fighter with arm 20 (idle) / 22 (attacking).

## 4. Dungeon map [C]

`DUNGPLOT` ($4970, entry $4978; DUNGON3 5025 on an Apple, `PEEK(798)` = 0) and its BASIC twin (5030-5060, for
"OTHER" machines) draw only the 3 × 3 cells around the party, each a 3 × 3 mark at (3 + 3X, 99 + 3Y) of the 24 × 30
level: byte < 2 → shape 31 white (wall); else 31 in HCOLOR 0 (erase), then for byte ≥ 185 shape 32 + (b > 199) +
(b > 214) + (b > 217) + (b > 220) in HCOLOR 3, but HCOLOR 0 for 215-217. Shape 35 (218-220) is empty, so stairs show
nothing. The party is XDRAW 30. `DNGLD3` line 200 fills the box (0-82, 95-191) white when a level is entered fresh, so
a cell not yet seen is white (pack glyph 99); a saved level comes back from `PIC` (`DNGOUT3` saves the box with
`PICSAVE`). Pack: glyph = shape number; 0 = erased (black).

## 5. Overworld [C] / [~]

`OUTDOORS3` line 30: the region file `OT<n>` (256 B, loaded at $0800) holds a 10 × 15 map; cell b is drawn at
(7 + 7 · column, 99 + 6 · row) as shape 37 + INT(b / 10) in HCOLOR INT(1 + b / 20) (the BASIC truncates both): class
c = b div 10 → GRASS / FOREST green, HILLS / MOUNTAINS violet, BUILDINGS / DUNGEON white, PATHWAY / BRIDGE black
(HCOLOR 4: invisible; BRIDGE's shape is empty anyway), LAVA / DESERT orange, WATER / MIST blue. v3.0 adds
`HCOLOR = 4 · (R > 5): HPLOT X, Y TO X, Y + 5` before each cell, which sets the cell's high bit for classes 6+ (no
change in this colour model). The map shows one whole region at a time (paging between regions [~]); the party is
XDRAW 43 over its cell.

The Apple's world is cut differently from DOS's, but the same: OT1-OT25 are Scandor's 50 × 75 in 5 × 5 blocks of
10 × 15 (OT n at column 10 · ((n − 1) mod 5), row 15 · ((n − 1) div 5)); 3,714 of 3,750 cells give the class of
`P3Apple2Art.DosToApple` for the DOS byte (the others are single cells where the maps differ: a town drawn as grass,
a forest cell). OT29 (TOWN OF LIGHT), OT31 (TOWN OF DARK) and OT41-43 (the Netherworld) are laid out unlike DOS's 9 × 9
planes and 31 × 16 Netherworld [C], so plane-only bytes are mapped by terrain [~]: the Light plane is MIST (Apple byte
114), the Dark plane is class 7 (74: black), the Netherworld black path (75, class 7), lava (81) and mist (115). DOS's
Netherworld overrides give some bytes other terrain there: the pack's `tile_swaps` draw tile 300 + class for those
155 bytes in region 4. Steam (DOS 83-87) has no Apple counterpart: drawn as mist [~].

The tiles are keyed by the remaster's (DOS) map byte: `overworld/tiles/<byte>.<class>.png` (even byte column, x 14),
`overworld/tiles/3<cc>.class-*.png` the class pictures, `overworld/odd/` the odd-column look. Pack view: 10 × 15
cells of 7 × 6, the Apple's window size (the remaster centres it on the party).

## 6. Screens [C]

- `screens/frame.png`: `SCREEN` as loaded (STARTER 600), before the names / classes are printed.
- Both title pages have a white 1-pixel border of their own (row 0 and column 0 lit; `SCREEN` too, along its boxes).
- Title (v3.0): `ICE DRAGON` on page 1 (boot lines 8-75), then lines 245-250 POKE 7 rows of 25 bytes from the DATA
  lines at ML − 6 (page 1; II poked page 2): "HIT ANY KEY TO CONTINUE". `screens/title-picture.png` / `title.png`.
- Ending (v3.0 `WIN`): lines 74-85 dissolve the screen, load `WIN PIC` (scrambled) and run `ROUTINE` $7530 with the
  stride byte $754B = 1, 8, … 253 (each pass EORs every stride-th byte, from $2000 and wrapping in the page, with $41,
  $42, … in turn), then $7562 with the same strides (EOR with a value rotated right each byte, $20, $10, … $01, $80, …;
  zero bytes and bytes that would become zero are left). `decode_win_pic` replays both; the result is the gods' room:
  a figure on a pedestal and six attendants. Over it the BASIC draws the party (bodies at x 28, 70, 112, 168, 210,
  252, y 190), the speech in the text window, and for each member slides sprite 15 down over an attendant (y 97-114,
  blacking the row above each step, lines 111-115), walks the member up (125-135) and draws sprite 16 at y 114 (and a
  small white mark for ZEUS, line 152) [C]; not in the pack (the picture only).
  The text says ZEUS / SUPER HERO or, for monster 79 (MO = 79), PLUTO / DARK LORD: one picture for both.
- Panels (`views/`): `PIC1` wilderness, `PIC2` strange lands, `PIC3` a dungeon map (v3.0 DEMO lines 280-320 show
  them in the map box with those captions), `OUTPIC` the empty box (DNGOUT3 100), `PIC` the saved dungeon map of side B
  (equal to PIC3).

## 7. Hi-res text font [C]

STARTER 605-630 point CSW ($36/$37) at $1828 and $06/$07 at $1800. The routine (88 bytes at $1828, both files) takes
the character in A: control characters go to COUT; $A0-$BF use the table at $1800, $C0-$DF at $1900, $E0-$FF at $1A00
(the sprites: never printed); glyph = table + 8 · (c mod 32). It writes the bytes as they are (high bit 0) to the
text cell (BASL / BASH, CH), EORed with $7F when INVERSE; `IMPTEXT` draws 7 rows, `IMPREVERSE` 8 (an inverse-video
copy, used for the names on row 1). Glyphs exist for 0-9, A-Z and $BA-$C0; $A0-$AF fall before the file or on the
routine's code. The game prints around the gaps: '=' ($BD) is blank and stands for a space ("FIRES=AND"), '@' draws ':'
("COMMAND@"), ':' draws '-' (":PRESS=RETURN:", "INCREDI:BLE"), ';' draws '"', '<' draws ',' ("WILDERNESS<"), '>'
draws '?' ("REDO>"); '?' draws ')' [~]. `font/apple2-p3-hires.json` keys the glyphs by the character they show
(`phantasie.font/v1`, 7 × 8). It is **not** the pack's font: it has no '.', '!', '/', '(', '\'' or lower case, which the
remaster's text needs; the pack uses the remaster 5 × 7 font as II's does.

## 8. The pack (`apple2.json`)

- Pictures: `monster.{n}.idle` / `.attacking` (80), `portrait.{race}.idle` / `.attacking` (16), `overworld.tile.{n}`
  (0-255 by DOS byte, 300-311 the classes), `overworld.party`, `dungeon.cell.{0, 30-36, 99}`; from an original copy
  `title.cover`, `title.credits` (title_sequence: the last title screen, until a key) and `scene.reward.good` /
  `.evil` (the ending's room).
- `index.json`: `default_order` amiga, apple2 (as II's ST, Apple): the Amiga stays the default; with no Amiga copy the
  Apple's art is drawn, before DOS CGA (pick-only, `fallback_order`).
- Checked in the Client on Xvfb (`parity/shots/s212-p3-apple2-*.png`): title, combat (monsters), Scandor and the
  Netherworld from Apple tiles (tile swaps), a dungeon level (white box, cursor). Client code a III pack without the
  Amiga's layouts needs: the gods' reward draws its picture page only with a `scroll_frame` (the parchment of the
  speech page, `UiOverlays.DrawRewardScene`), so with this pack the reward is the plain text box and
  `scene.reward.*` is not shown yet; III's AUTO-MAP BOX (QoL, Tab) overlaps the small 72 × 90 dungeon map.

## 9. Combat field (s228)

Read from v3.0's `OUTDOORS3` / `DUNGON3` (the WOZ pair, `detok_apple2_p3.py` on the decoded sides) against the `[cr]`
v1.0 listings: every drawing line below has the same text in the four (two programs × two versions) but where noted.
Line numbers are the listing's. III's field is not I / II's (`../phantasie1/GRAPHICS.md` section 7): no mixed-mode
round, no TEXT status page, its own sprite routine.

### 9.1 The screen `[C]`

The whole game runs on one hi-res page (STARTER 600-610: `SCREEN` loaded, full screen, page 1), text drawn by
`IMPTEXT` / `IMPREVERSE` (section 7). A fight draws on it too, in its boxes:

| Box | Where | What a fight puts there |
|---|---|---|
| Party columns | text rows 0-8, columns 4 + 6·slot (5 wide; `HTAB HT`, `HT = 6·WG + 5`, 1-based) | 600-635 (GOSUB 600 after a member changes): row 0 the name (STARTER 620, `IMPREVERSE`: inverse), row 1 the class (`Z$`, 640), row 2 magic `cur@max` (`@` shows ':'; one column right when the maximum is below 10), row 3 hits `cur@max` (a maximum of 100 or more: the current at column + 1 on row 3, `@max` on row 4), rows 5-8 the body letters `C$` (O / I / B / G for OK / injured / broken / gone: head at +1 on row 5, left arm at +0 and right arm at +2 on row 6, torso +1 row 7, left leg +0 and right leg +2 row 8) and the status word `S$` down column +4 (DEAD, OUT, SLEP, DOWN, OKAY = byte 18 + PC%) |
| Message box | text rows 9-11, columns 17-38 (650: `POKE 33,22: POKE 32,17: POKE 34,9: POKE 35,12`) | the round's lines; HOMEd at each member slot of each pass (1610), each swing (1690), each monster slot (1860), each further monster swing (1932), each target of a party-wide special (3130) or an area spell (2550); 4005 / 4120 put the "TO ..." part at `VTAB 11` and 4020 / 4140 the death at `VTAB 12: HTAB 14` (fixed rows, not scrolled) |
| Order box | text rows 12-23, columns 12-19 (550) | the options and orders (1068-1470); HOMEd (blank) at 1520 for the round |
| Map box | x 0-82, y 95-191 | the region map (line 30) or the dungeon marks (section 4), untouched by a fight |
| Field | x 147-279, y 96-190 (inside the frame's box, x 145-146 its left edge) | sprites and figures, below |

The fight's text before and after the round (1051 the ranks, 1062-1160 the monsters' answers, 4260-4400 the reward)
goes to the message and order boxes too.

### 9.2 Monsters `[C]`

- `MONSTER PLOT` (section 2): a sprite's top-left at byte column X%, row Y%; the bytes are stored with `STA`, so a sprite
  covers its 21-pixel-wide box whole (black where it has no pixels), and shape 0 blanks a box.
- Ranks: 1047-1050 make the group with more living monsters RN%(0) (the second group on a tie; a group with none never
  leads). 1540: `GOSUB 500` blanks every slot (byte columns 21, 25 … 37 at rows 96, 109, 123), then Y%(RN%(0)) = 120,
  or 99 when its S% is 21 (a 42-row giant), and Y%(RN%(1)) = 96.
- Monster Y of a group of GN% + 1 living (the dead are dropped at 1020-1046, each round): X% = 21 + 4·INT(5·(Y + .5) /
  (GN% + 1)) (1550), so five even slots 28 pixels apart from x 147; a group has at most 5 (`DIM HP%(1,4)`) and 2 groups.
- 1540 runs at 1068, before the options: the field shows every living monster in pose A while the orders are given.
- Pose B (S% + 1, or 23 for S% 21) at 2006, from 1895 (before a melee) and 1940 (before a spell, a special touch or a
  breath); it stays until the next 1540.
- A death (4140-4145): ":IT=DIES:" then shape 0 at the monster's box (and again at row 120 for a giant: both halves).

### 9.3 The party `[C]`

- Slot k (PM) at Q = 147 + 19·k, PP = 198 − 8·PP%(k) (9100 / 1610), PP% = record byte 0 + 1 (STARTER 330; 9040 stores
  J − 1): back (0) at y 190, middle (1) 182, front (2) 174 (DEMO 210 / 480 / 550 draws the middle at 182 and steps to
  174 and 190). SCALE 1 (STARTER 300; never changed in a fight).
- 1525 (each round, after the orders): for each member not dead (byte 18 ≠ 0) 9100 erases every shape the slot may hold
  (arms 20-22, body, shapes 2, 3, 5, 6), then 9200:
  - a FIRE order (IN% < 0): shape 2 (LD, an archer with a drawn bow) XDRAWn, **instead of** the body;
  - a member with a hurt leg (PC% = 2, 8210): shape 6 (kneeling) and arm 20;
  - else the body (byte 168, section 3) and arm 21 for a parry that took (1527: IN% 5 → 3 when `RND·100 < byte 22`, or
    with a hurt shield arm, PA%(5) > 1), else arm 20.
- A swing (1695, each of the member's swings): arm 20 erased, arm 22 drawn; stays to the next 1525.
- A shot (1624): XDRAW 2, XDRAW 3 (FR): the archer turns into shape 3 (bow loosed); stays.
- A cast (1750-1764): "X=CASTS" (with "=A=SPELL" and 250 loops for spell orders 109-199), arm 20 erased, **arm 5**
  drawn (stays), the sparkle 23 XDRAWn at (Q + 14, PP − 23), the four cast tones (7570; none with the sound off), XDRAW
  23 again (off).
- A member's death (4020-4025): ":DEATH:" at row 12, the death tones, then 9100: **the figure is erased at once**.
- The summoned elemental is slot 6 (2870: PO = 6, 9100 / 9200 at x 261), drawn with its record's byte 168: what that
  holds is not known `[?]` (section 10).

### 9.4 Effects `[C]`

- 4132 (after "=DOING=n ... TO=name", the tones and the HP step, before the death check): `D = 47 − SP·23`, XDRAW D at
  (3 + 7·X%, 20 + Y%) at SCALE 1 (HCOLOR 3), 500 empty loops, XDRAW again (off). SP = 1 for a party spell's damage
  (2050 Fireflash, 2580 the area spells) → **shape 24**; SP = 2 for a bow hit (1627) → **shape 1, the arrow**, on the
  monster. A melee hit leaves SP = 0: 4131 skips 4132, so nothing is drawn (shape 47 is never reached).
- A bow miss (1628): "X=FIRES=AND / MISSES", the arrow (shape 1) XDRAWn at (220, 104) in `OUTDOORS3`, (220, 105) in
  `DUNGON3` (both versions), two speaker clicks, 250 loops, XDRAW off, then the PA pause (750).
- 2065 (party Fireflash): page 2 / page 1 flipped `SQR(damage)` times with a click each (`POKE -16299 / -16300`), as I's.

### 9.5 Pauses `[C]`

PA = 120 × TIME LAG (STARTER 361, 7560) loops after most lines (GOSUB 750); a swing's "HITS" goes straight to its damage
(1710 → 4100); the cast and death tones (`CALL 770`, 7575) are skipped with the sound off (NS = 1). The fight's end
(4260): 2000 loops, `GOSUB 500` (the monsters blanked), the reward in the order box, 6000 loops.

### 9.6 In the remaster

The pack's `apple2_field` with a `monster_plot` block (`Apple2FieldLayout.Plot`, `P3Apple2ArtManifest`; `p3-apple2`
recipe 4: the pack only, the extract is unchanged): behind ORIGINAL SCREENS each round of III plays on the Apple's
screen (280 × 192 at (20, 4) of the remaster's 320 × 200): `SCREEN` (`screens/frame.png`), the party columns from the
round's start, the field and the message box (`Apple2FieldRun.PlanPlot`, `UiOverlays.Apple2PlotPlaces` /
`DrawApple2Field`); the options and orders stay the remaster's screen (as I / II). The pictures are the index's
(`monsters/`, `figures/`, `effects/`, `shapes/20-22, 5`): nothing new is extracted. Core's tags drive it: a member's
swing tag (arm 22, or for a FIRE order the loosed bow), a member's cast tag (sound 21: arm 5 and the sparkle), a
monster's swing or cast tag or its breath line (pose B), a `PartySpell` hit (shape 24) and an archer's `PartyMelee` hit
(the arrow) after the damage line, an archer's MISSES (the arrow at the miss point, 250 loops, then the pause), a death
line after a hit on that monster or member (erased; a same-named monster is told apart by the hit before its line).
Parries that took (Core rolls them in the round, `P3Fight.Parried`) show arm 21 from the round's start. Tests:
`Apple2PlotTests`. Live: `parity/s228-p3-apple2-combat-live.md`.

`[~]` / `[?]`:

- `[~]` the map box is drawn empty (the Apple keeps its map there); the order box is blank during the round, as 1520.
- `[~]` the party columns show the round's start values (the Apple updates a member's column at each change, GOSUB
  600); names cut to 5 letters, the class word is the remaster's (cut to 5), the status word the remaster's III combat
  status (`p3combat.status.*`); the remaster's 5 × 7 font, not `IMPTEXT`.
- `[~]` Core's rank order stands for the Apple's size order (1047), Core's round order and texts are DOS III's (one action
  a turn, not 1600's three passes); the message box wraps Core's lines to 22 columns and keeps the last 3 rows (the
  Apple places "TO ..." and the death at fixed rows).
- `[~]` one pause a line at `loop_ms` (I's rule of thumb); `[?]` `sparkle_ms`; no speaker sound (tones, the miss
  clicks); 2065's page flip not shown; XDRAWs drawn over, not XORed (a second shot by one archer would toggle 2 / 3 on
  the Apple).
- `[~]` a kneeling member's arm is `shapes/` art (drawn at x 0's colour phase); every figure keeps the colour phase of
  x 147 (the index's), the Apple's varies with the slot.
- `[~]` the noble body (social class 3 → shape 4) uses the remaster's social class now (the Apple sets byte 168 once,
  at creation).
- `[?]` the elemental (slot 6) is not drawn: its byte 168 is not known.

## 10. Open

- [?] Sound: `CALL 770` (INFO, the tone routine at $302) and the `PEEK(-16336)` clicks are not decoded (as II).
- [?] The BASIC pauses (FOR loops: title, effects, the ending's walk) have no decoded time.
- [~] The ending's animation and the graveyard (CEMETARY3: HPLOT gate, figures, the demon's rows) could be replayed
  as scenes; only the WIN picture is extracted.
- [~] The colour model is the emulator-style one of II; the half-pixel shift of high-bit bytes is not drawn.
- [~] Which character '?' ($BF) was meant to show; the glyphs of $DB-$DF (body-diagram bars) are not mapped.
- [?] Whether v1.0 (before the crack) had the v3.0 title and ending: only the cracked v1.0 is held.
- [?] How the Apple draws a summoned elemental: no party-figure body for one is set in TOWN3 / OUTDOORS3 / DUNGON3
  (only record byte 168, TOWN3 2350-2360); the pack has no `portrait.elemental`.
