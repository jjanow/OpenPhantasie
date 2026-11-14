# Phantasie III (Apple II, SSI 1987): graphics

s212. Images: `../original-images/phantasie3/` (the `[cr]` v1.0 sides 1A / 1B / 2A / 3A, and the original v3.0 disk 1
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

Copies (s212, compared file by file): 1A = 3A and 1B = 2A on every art file; v3.0 disk 1 equals them on `SHAPES`,
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
- Checked in the Client on Xvfb (s212, `parity/shots/s212-p3-apple2-*.png`): title, combat (monsters), Scandor and the
  Netherworld from Apple tiles (tile swaps), a dungeon level (white box, cursor). Client code a III pack without the
  Amiga's layouts needs: the gods' reward draws its picture page only with a `scroll_frame` (the parchment of the
  speech page, `UiOverlays.DrawRewardScene`), so with this pack the reward is the plain text box and
  `scene.reward.*` is not shown yet; III's AUTO-MAP BOX (QoL, Tab) overlaps the small 72 × 90 dungeon map.

## 9. Open

- [?] Sound: `CALL 770` (INFO, the tone routine at $302) and the `PEEK(-16336)` clicks are not decoded (as II).
- [?] The BASIC pauses (FOR loops: title, effects, the ending's walk) have no decoded time.
- [~] The ending's animation and the graveyard (CEMETARY3: HPLOT gate, figures, the demon's rows) could be replayed
  as scenes; only the WIN picture is extracted.
- [~] The colour model is the emulator-style one of II; the half-pixel shift of high-bit bytes is not drawn.
- [~] Which character '?' ($BF) was meant to show; the glyphs of $DB-$DF (body-diagram bars) are not mapped.
- [?] Whether v1.0 (before the crack) had the v3.0 title and ending: only the cracked v1.0 is held.
- [?] How the Apple draws a summoned elemental: no party-figure body for one is set in TOWN3 / OUTDOORS3 / DUNGON3
  (only record byte 168, TOWN3 2350-2360); the pack has no `portrait.elemental`.
