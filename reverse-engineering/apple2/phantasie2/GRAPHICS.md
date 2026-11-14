# Phantasie II (Apple II, v2/5/86): graphics

Images: `../original-images/phantasie2/4am-crack/` (side A holds every art file; `SHAPES` and `ICE DRAGON` equal the
`.woz` originals', FORMAT.md section 1) [C]. Disk and file system:
`FORMAT.md`. Scripts: `tools/scripts/extract_a2_p2_graphics.py` (PNGs + `extracted/graphics/extraction-index.json`),
`tools/scripts/build_a2_p2_asset_manifest.py` (the Client pack `src/OpenPhantasie.Data/GameData/phantasie2/graphics/apple2.json`).
Tags: [C] read from the files / BASIC, [~] probable, [?] not known.

## 1. What art the Apple II version has

| Art | Where | Form |
|---|---|---|
| Monsters (80 × 2 poses), party figures, overworld tiles, dungeon map marks, combat effects | `SHAPES` (side A, 2,932 B, `RECALL`ed by INTRO2 line 30; INTRO2 line 5 points Applesoft's shape-table pointer $E8/$E9 at $451F) | one Applesoft shape table, 59 shapes [C] |
| Title picture | `ICE DRAGON` (side A, 8,192 B; PHANTASIE II lines 20 / 30 load it into hi-res pages 1 and 2) | raw hi-res page in Apple memory order [C] |
| "HIT ANY KEY TO CONTINUE" | PHANTASIE II DATA 260–290, POKEd into page 2 by lines 243–257 | 7 rows × 25 bytes of hi-res data [C] |
| Title lightning | PHANTASIE II lines 50–96: 30 + 13 + 11 + 39 random `HPLOT 81,69 TO …` lines in HCOLOR 5 / 7 | procedural, random: not extracted |
| Graveyard (CEMETARY2), Filmon's ending (WIN) | BASIC: HPLOT bars and shapes 19–22 / 1–2 / 5–7 at ROT 8–56 and SCALE 2 | procedural: not extracted [~ could be replayed from the listing] |
| Text (every menu, town, list) | the Apple II character ROM (TEXT page / mixed mode) | **not on the disks**: the pack uses the remaster's 5×7 font |
| Town screens, Guild, shops | text only | none |
| Sound | speaker clicks: `CALL 770` with pitch / duration at 768 / 769 (e.g. OUTDOORS2 7575), `PEEK(-16336)` clicks | not decoded [?] |

There are no palettes: hi-res colour is NTSC artifacting from the bit pattern (section 3).

## 2. Shape table (`SHAPES`) [C]

Applesoft format: byte 0 = shape count (59), byte 1 unused, then 59 u16 LE offsets from the table start; shape n
(DRAW n, 1-based) starts at offset[n−1]. Each shape byte holds up to three vectors, low bits first:

- A = bits 0–2, B = bits 3–5: direction = low 2 bits (0 up, 1 right, 2 down, 3 left), plot = bit 2;
- C = bits 6–7: direction only, never plots;
- a 0 byte ends the shape; once what is left of a byte (after A, after B) is 0, the rest of that byte is skipped.

DRAW (ROT = 0, the only rotation the game uses for these): for every vector, SCALE times: plot if the plot bit is set,
then move one pixel (SCALE= 0 is 256). With SCALE > 1 a shape becomes an outline of SCALE-long lines (the Apple's own
look, kept). Shape 44 has no vectors (an empty shape: terrain 7, NETHERWORLD, draws nothing).

Plotting (Applesoft HPLOT / DRAW): byte column `x / 7`, bit `x % 7`; the byte gets the HCOLOR byte through the mask
`0x80 | 1 << bit` (MSKTBL $81…$C0), so a plot also sets the byte's high bit to the colour's. The HCOLOR byte for even /
odd byte columns: 0 = 00/00, 1 green = 2A/55, 2 violet = 55/2A, 3 white = 7F/7F, 4 black = 80/80, 5 orange = AA/D5,
6 blue = D5/AA, 7 white = FF/FF. XDRAW flips the pixel bit (used for the cursors and effects; the extract draws those
with DRAW in white). [C] for the format; the ROM's plot-then-move order and XDRAW's high-bit handling are from the
Applesoft ROM as commonly documented [~].

### 2.1 Shape map

| Shapes | Use | Evidence |
|---|---|---|
| S%, S%+1 of a monster record | monster pose A (standing, OUTDOORS2 / DUNGON2 line 1550) and pose B (drawn when it attacks or casts, line 2006 from 1895 / 1940; pose A is erased first). Monster record (MN2x text, INPUT line 910): `C%` = SCALE, `H%` = HCOLOR **and** the first figure's x (line 1550 `X% = H% + Y·120/(GN%+1)`), `S%` = shape, `YS%` = the group's height for stacking ranks (lines 1480–1500: rank 1 at y 135, the next at 135 − YS% …) | [C] |
| Monster S% values used | 1, 3, 5, 7, 9, 11, 13, 14, 15, 16, 17, 28, 49, 51, 53, 55, 57 (17 shapes for 80 monsters, told apart by SCALE 1–8 and HCOLOR); S% = 14 (PLUTO'S SNAKE-DRAGON) and 16 (PLUTO'S DRAGON-DEMON) make pose B the next species' shape 15 / 17 | [C] data, intent [~] |
| 19, 25, 26, 27 | party body (record byte 168, TOWN2 lines 2350–2356): races 7+ → 26; DWARF, HALFLING, GNOME, PIXIE, SPRITE → 25; HUMAN / ELF → 19, but 27 for a MONK, PRIEST or WIZARD | [C] |
| 20, 21, 22 | the party figure's arm overlay: 20 standing (1534), 21 casting (1760) or parrying (1532 `IN% = 3`), 22 attacking (1695); drawn at `(19·slot, 158)`, HCOLOR 3, SCALE 1 | [C] |
| 23 | spell sparkle over the caster, XDRAW at `(19·slot + 19, 136)` (1762 / 1764) | [C] |
| 24 | spell hit on a monster: XDRAW `SP·35 − 11` with SP = 1 (4132) | [C] |
| 59 | the tossed rock: XDRAW at (70, 20) on a miss (1628), on the target with SP = 2 (4132) | [C] |
| 28, 29 | the bees' poses (GIANT BEE, KILLER BEE: S% = 28) | [C] |
| 30 | dungeon party cursor, XDRAW at `(144 + 4·CX, 4 + 4·CY)` (DUNGON2 5175) | [C] |
| 31–36 | dungeon map marks, 4 × 4 at `(144 + 4X, 4 + 4Y)`, drawn only for the cells around the party (5025–5062): cell byte < 2 → 31 in white (wall); any other byte erases 31, then for byte ≥ 185: 32 (185–199), 33 (200–214), 34 (215–217, but in HCOLOR 0: never visible), 35 (218–220, stairs 7800), 36 (> 220, events 8200) | [C] |
| 37–48 | overworld terrain tiles 0–11 (shape 37 + map byte / 10), 7 × 6 at `(140 + 7X, 2 + 6Y)`, HCOLOR `INT(1 + .51·terrain)`: GRASS, FOREST green; HILLS, MOUNTAINS violet; BUILDINGS, DUNGEON white; PATHWAY, NETHERWORLD HCOLOR 4 (black: not visible); LAVA, DESERT orange; WATER, MIST blue (OUTDOORS2 5040, names line 120) | [C] |
| 43 | also the overworld party cursor: XDRAW 43 (a full 7 × 6 block) over the party's cell (5175) | [C] |
| 49–58 | monster shapes (see S% above); terrain 12 (ASTRAL PLANE) would map to 49, but map bytes ≥ 120 are fog (5030), so no map cell draws it | [C] |
| 1–18 | monster shapes (S% above); 2, 4, 6, 8, 10, 12, 18 are pose-B shapes | [C] |
| 5–7, 1–2, 19–22 | also used by the CEMETARY2 / WIN drawings | [C] |

## 3. Colour model (`HiresPage.to_rgb`) [~]

One model for shapes and the title picture, at the hi-res pixel grid (280 × 192, no half-pixel shift):
- a lit pixel with a lit neighbour is white;
- a lit pixel alone is coloured by its absolute x parity and its byte's high bit: high bit 0: even x violet, odd x green;
  high bit 1: even x blue, odd x orange;
- a dark pixel between two lone lit pixels takes their colour (the 140-wide colour fill, so a 0x2A / 0x55 run reads as
  solid green, not stripes); anything else is black.

RGB values: violet #FF44FD, green #14F53C, blue #14CFFD, orange #FF6A3C, white, black: the usual emulator "RGB" set
(AppleWin / MAME style). A real NTSC monitor blurs and fringes more; the half-pixel delay of high-bit bytes is not drawn.

Colour depends on screen position: the same shape drawn one column over lights different pixels (a green pattern
lights only odd x). The extract therefore draws each picture at the game's own x where it is fixed: monsters at
x = H% (the first figure of a group), party figures at slot 0 (x 0), dungeon marks at 144, map tiles at 140 (the even
map column; `extracted/graphics/overworld/odd/` holds the odd-column look of each terrain). In the game a group's later
figures and every other map column look slightly different; the pack shows one look per picture.

## 4. Title picture [C]

`ICE DRAGON`: 8,192 bytes = a hi-res page, row y at `((y & 7) << 10) | (((y >> 3) & 7) << 7) | (y >> 6)·40` (the screen
holes hold no picture data). `screens/ice-dragon.png` is the plain page; `screens/title.png` adds the boot program's
POKEs (243: clear 21341–21365, then the 7 DATA rows at address + 8192 on page 2, then 257: clear 21469–21493), which
write "HIT ANY KEY TO CONTINUE" along the bottom. Boot order (PHANTASIE II): a text page (SSI PRESENTS / PHANTASIE II /
DESCENT INTO THE NETHERWORLD), the dragon on page 2 with random lightning on page 1, the page flips at lines 70–94, the
prompt rows, then a wait for a key (a key at any time goes on, line 300). The pauses are FOR loops, so their time
depends on Applesoft speed [?]; the pack shows only the last screen, until a key.

## 5. The pack (`apple2.json`)

- Pictures: `monster.{n}.idle` / `.attacking` (80), `portrait.{race}.idle` / `.attacking` (16; HUMAN / ELF use body 19,
  the robed 27 of their MONK / PRIEST / WIZARD can't be keyed by race: `extracted/graphics/figures/` has all four
  bodies × three poses), `overworld.tile.{0..255}` (0–119 the terrain shapes; 120–255 fog, black), `overworld.party`,
  `dungeon.cell.{0, 30–36}` (glyph number = shape number; 0 = nothing drawn), `combat.sparkle` (23), `combat.rock` (59),
  `title.cover` (the dragon), `title.credits` (the dragon + prompt; the title sequence's only step).
- `tile_maps`: tiles 7 × 6, view 22 × 20 cells (the Apple shows its whole 20 × 26 map, drawn rotated against the
  remaster's 26 × 20 grid; the pack follows the remaster's grid and the view scrolls over the last 4 columns),
  dungeon glyphs 4 × 4.
- Font: `remaster-5x7` (the ROM font is not on the disks). Screen: the remaster's 320 × 200 layout, Apple pixels 1:1.
- Not in the pack: town / ending / defeat pictures (the Apple has none, or draws them in BASIC), sounds, the ST's
  combat line timings. The Client draws its plain screens for those (checked live on Xvfb: Guild, town bar,
  overworld from Apple tiles, combat with Apple monsters and party figures).
- `index.json`: `default_order` = atari-st, then apple2: the ST stays the default; with only the Apple II files
  imported, the Apple pack is the default (test `Phantasie2AppleIIPackTests`).

## 6. Open

- [?] Timings of the title (FOR loops) and the combat effects (`FOR PS = 1 TO 250 / 500`).
- [?] Speaker sounds (`CALL 770`, the machine code at $302 from `INFO`): not decoded.
- [~] Replaying CEMETARY2 / WIN's drawings (shapes at ROT 8–56) would need ROT support in the rasterizer (Applesoft
  rotates by 64ths of a turn, only multiples of 16 exact).
- [~] The colour model is an emulator-style approximation; the original monitor look varies.
