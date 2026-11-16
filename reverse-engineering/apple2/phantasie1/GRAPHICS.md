# Phantasie I (Apple II, VERSION 1.0): graphics

How I's Apple II pictures are stored and drawn, and how the `p1-apple2` import turns them into the `apple2` graphics
pack (s223). Disks, files and tables: `FORMAT.md`. I's Apple engine is II's: the drawing lines below (OUTDOORS / DUNGON
1480–1550, 1695, 1760–1764, 2006, 4132, 5035–5062, 5040, 5175; TOWN 2350–2356) were compared with II's `OUTDOORS2` /
`DUNGON2` / `TOWN2` (4am sides) and are the same text but for two: 1760 (II adds a RETURN: flow, not drawing) and 4132
(I always XDRAWs shape 24; II XDRAWs `SP·35 − 11`, 24 for a spell or 59 for a rock). So the format and colour notes of
`../phantasie2/GRAPHICS.md` apply as they stand; this file gives I's evidence and what differs. Tags: `[C]` read in the
code or data, `[~]` probable, `[?]` not known.

Code: `src/OpenPhantasie.Importer/Pipelines/P1Apple2/P1Apple2Art.cs` (I's steps) over II's
`Pipelines/P2Apple2/A2Graphics.cs` (`HiresPage`, `ShapeTable`, `A2Graphics.RunShapes`, shared since s223), manifest
`P1Apple2ArtManifest.cs`. Output: `extracted/graphics/` (private: the Apple's pixels) and the shipped layout
`src/OpenPhantasie.Data/GameData/phantasie1/graphics/apple2.json` (paths and numbers only). Line numbers are
`tools/scripts/a2_applesoft.py`'s listing.

## 1. What art the Apple II version has `[C]`

| Art | Where | Form |
|---|---|---|
| Monsters (80, two poses), party figures, overworld tiles, dungeon marks, spell effects | `SHAPES` (side A, 2,930 B at $451F; INTRO 5 points $E8/$E9 there, line 30 RECALLs it) | Applesoft shape table, **58** shapes |
| Title picture | `HPIC` (side A, 8,192 B at $2000; INTRO 1000 RECALLs it onto hi-res page 1, 1010 shows it full-screen, 1276 waits for a key) | raw hi-res page |
| Party wipe | `CEMETARY` 400–680: shapes 19 / 20 / 21 / 22 at ROT 8–56, SCALE 1–2 (the fallen party), shapes 5 / 6 XDRAWn in turn (dancing monsters), then the Astral Plane: an HPLOT temple, the party as shape 19 + 20, shapes 11 (SCALE 3, HCOLOR 6) and the demon, shape 18 (SCALE 4, HCOLOR 2) | procedural BASIC: not extracted `[~]` |
| Ending (Zeus) | `FLASH?` (side B, run by DUNGON 4300): a ground line, HPLOT lightning (1000–1030) flashed with speaker clicks, the party as SCALE 2 figures (2000), Zeus = shape 9 at SCALE 4, HCOLOR 5 (110), screen flashes (2100) | procedural BASIC: not extracted `[~]` |
| Town, Guild, shops, scrolls, messages | TOWN is a TEXT page (TOWN has no DRAW / HPLOT); scrolls are text-page dumps (`FORMAT.md` 3.8) | none |
| Item / find / trap pictures | none: OUTDOORS / DUNGON print text only for finds, traps, stairs and locks | none |
| Text | the Apple II character ROM | not on the disks: the remaster's 5×7 font |
| Sound | speaker clicks (`CALL 770` with 768 / 769, `INFO` at $302, as II) | not decoded `[?]` |

## 2. Shape table `[C]`

Format, DRAW rules and plotting: `../phantasie2/GRAPHICS.md` section 2. I's table has 58 shapes; against II's 59 it is
byte-equal in shapes 1–44, 47 and 49–58. It differs in **45, 46 and 48** (overworld terrain classes 8, 9 and 11: other
tile patterns) and has **no shape 59** (II's tossed rock; I's OUTDOORS / DUNGON never draw 59). Test
`P1Apple2ArtTests.Shapes_AreIIsButForThreeTerrainTiles`.

### 2.1 Shape map (I's lines; II's have the same numbers)

| Shapes | Use | Evidence |
|---|---|---|
| S%, S% + 1 | monster pose A (OUTDOORS / DUNGON 1550) and pose B (2006: drawn when it attacks or casts, pose A erased first). MN record (FORMAT.md 3.1): `C%` SCALE, `H%` HCOLOR **and** the first figure's x (`X% = H% + Y·120 / (GN% + 1)`), `S%` shape, `YS%` the rank height (1480–1500: rank 1 at y 135, the next at 135 − YS% ...) | `[C]` |
| S% values | 1, 3, 5, 6, 7, 9, 11, 13, 15, 17, 28, 49, 51, 53, 55, 57: 16 shapes for 80 monsters (SCALE 1–8, HCOLOR 1–7). **SPLAT BEAST** (78) has S% = 6, so its pose A is shape 6 (pose B of the shape-5 monsters: KOBOLD, GOBLIN, ...) and its pose B is shape 7 (DRIP SLIME's pose A); the extract draws exactly that | data `[C]`, intent `[~]` |
| 19, 25, 26, 27 | party body (record byte 168, TOWN 2350–2356): races 7+ → 26; DWARF, HALFLING, GNOME, PIXIE, SPRITE → 25; HUMAN / ELF → 19, but 27 for a MONK, PRIEST or WIZARD (classes 1, 4, 5 of TOWN 260). The elementals in `CHAR` 37–39 hold 26 | `[C]` |
| 20, 21, 22 | the figure's arm: 20 standing (1534), 21 parrying (1532) or casting (1760), 22 attacking (1695); at `(19·slot, 158)`, HCOLOR 3, SCALE 1 | `[C]` |
| 23 | spell sparkle over the caster, XDRAW at `(19·slot + 19, 136)` (1762 / 1764) | `[C]` |
| 24 | spell hit on a monster, XDRAW at the monster at its SCALE (4132; I has no rock, so always 24) | `[C]` |
| 30 | dungeon party cursor, XDRAW at `(144 + 4·CX, 4 + 4·CY)` (DUNGON 5175) | `[C]` |
| 31–36 | dungeon marks, 4 × 4 at `(J, IA)`, drawn for the cells around the party (DUNGON 5035–5062): cell byte < 2 → 31 white (wall); any other byte erases 31, then for bytes ≥ 185: 32 (185–199), 33 (200–214), 34 (215–217, in HCOLOR 0: never visible), 35 (218–220), 36 (> 220) | `[C]` |
| 37–48 | overworld terrain classes 0–11 (shape 37 + map byte / 10), 7 × 6 at `(140 + 7·CX, 2 + 6·CY)`, HCOLOR `INT(1 + .51·class)`: classes 0–1 green, 2–3 violet, 4–5 white, 6–7 HCOLOR 4 (black: not visible; 44 also has no vectors), 8–9 orange, 10–11 blue (OUTDOORS 5040). I's programs name no terrain (II's OUTDOORS2 line 120 does), so the files are labelled `class-NN` | `[C]` |
| 43 | also the overworld party cursor: XDRAW 43 over the party's cell (5175) | `[C]` |
| 1–18, 49–58 | monster shapes (S% above); 5–7, 9, 11, 18–22 also CEMETARY's / FLASH?'s drawings | `[C]` |

## 3. Colour

`../phantasie2/GRAPHICS.md` section 3 (`HiresPage.ToImage`): the same NTSC artifact model `[~]`, each picture at the
game's own x (monsters at x = H%, figures at slot 0, map tiles at 140 (the even map column; `overworld/odd/` holds the odd
column's look), dungeon marks at 144).

## 4. Title `[C]`

HELLO: `HGR`, then a TEXT page "STRATEGIC SIMULATIONS PRESENTS / VERSION 1.0" and `RUN INTRO`. INTRO 5–35 point the shape
table at $451F, set SCALE 2 and load `SHAPES` and `INFO`; 1000 RECALLs `HPIC` onto page 1, 1010 switches to full-screen
graphics, 1276 clears the keyboard strobe and waits for a key; then the text menu (200–225: PHANTASIE / COPYRIGHT 1984 /
1 - START IN PELNOR / 2 - [original text]). `HPIC` holds the whole screen: the PHANTASIE / BY / WINSTON DOUGLAS WOOD
scroll and "PRESS ANY KEY TO CONTINUE" (no POKEd prompt rows, unlike II). `screens/title.png` is the plain page. The two
copies' `HPIC` are equal (sha1 `7dde3441…`, `P1Apple2ArtTests.WozPair_GivesTheSameArt`).

## 5. The pack (`apple2.json`)

II's Apple pack's layout (the same engine), with I's files:

- Pictures: `monster.{n}.idle` / `.attacking` (80), `portrait.{race}.idle` / `.attacking` (16: the race's body with arm
  20 / 22, HUMAN / ELF as the fighter body 19), `overworld.tile.{0..255}` (0–119 the terrain shapes, 120–255 fog, black),
  `overworld.party` (43), `dungeon.cell.{0, 30–36}`, `combat.sparkle` (23), `title.cover` = `title.credits` = `HPIC` (the
  title sequence's only step, until a key).
- `tile_maps`: tiles 7 × 6, view 22 × 20 cells (the Apple shows its whole 20 × 26 map, rotated against the remaster's
  26 × 20 grid, FORMAT.md 3.6; the pack follows the remaster's grid), dungeon glyphs 4 × 4 by II's cell ranges.
- Font: `remaster-5x7`. Screen: the remaster's 320 × 200 layout, Apple pixels 1:1.
- No `town.backdrop`, item / event pictures, `ending.final` or `defeat.*`: the Client's plain screens stand in.
- `index.json`: `default_order` amiga, c64, apple2: the Amiga stays I's default, then the C64; with neither imported the
  Apple's pack is drawn (`Phantasie1AppleIIPackTests`).
- Import: `P1Apple2Pipeline` runs the art through `ImportContext.TryOptional` after the tables and text, whichever copy's
  tables win; a missing or malformed `SHAPES`, `HPIC` or `MN*` warns ("No Apple II art: ...") and the tables stay.

## 6. Open

- `[~]` The party figure is race-keyed: a HUMAN / ELF MONK, PRIEST or WIZARD shows body 19, not the Apple's robed 27
  (`figures/` has all four bodies × three poses). Needs a race + class portrait key in the Client.
- `[~]` CEMETARY's party wipe (ROT 8–56 bodies, dancing monsters, the Astral Plane temple and demon) and FLASH?'s Zeus
  scene are BASIC drawings: not replayed (the first needs ROT support in the rasterizer; the Astral Plane and Zeus parts
  are ROT 0 and could be drawn from the listing). The Client draws DOS's text pages there.
- `[?]` Speaker sounds (`CALL 770`, FLASH?'s DATA 1105) and the FOR-loop pauses (combat effects 500 loops, FLASH? 500 /
  5000) not timed.
- `[~]` The colour model is an emulator-style approximation; colour fringes per screen column not shown (one look per
  picture).
