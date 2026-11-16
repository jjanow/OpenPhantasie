# Phantasie I (Apple II, VERSION 1.0): graphics

How I's Apple II pictures are stored and drawn, and how the `p1-apple2` import turns them into the `apple2` graphics
pack (s223). Disks, files and tables: `FORMAT.md`. I's Apple engine is II's: the drawing lines below (OUTDOORS / DUNGON
1480–1550, 1695, 1760–1764, 2006, 4132, 5035–5062, 5040, 5175; TOWN 2350–2356) were compared with II's `OUTDOORS2` /
`DUNGON2` / `TOWN2` (4am sides) and are the same text but for two: 1760 (II adds a RETURN, so its rock throw, 1623,
can draw arm 21 without the cast's sparkle) and 4132 (I always XDRAWs shape 24; II XDRAWs `SP·35 − 11`, 24 for a spell
or 59 for a rock). II's whole combat field is compared line by line in `../phantasie2/GRAPHICS.md` section 7 (s226). So the format and colour notes of
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
| Party wipe | `CEMETARY` (side A; OUTDOORS 4200, DUNGON 4205): the fallen party (shapes 19–22 at ROT 8–56), the dancing monsters (5 / 6), the Astral Plane's temple, the party row (19 + 20), the gargoyles (11) and the demon (18) | procedural BASIC: replayed from its statements (section 8, s224) |
| Ending (Zeus) | `FLASH?` (side B, DUNGON 4300 after monster 72): ground line, HPLOT lightning, the party (arm 20 + own body at SCALE 2), Zeus (shape 9, SCALE 4, HCOLOR 5), page-2 flashes, thunder | procedural BASIC: replayed (section 8, s224) |
| Town, Guild, shops, scrolls, messages | TOWN is a TEXT page (TOWN has no DRAW / HPLOT); scrolls are text-page dumps (`FORMAT.md` 3.8) | none |
| Item / find / trap pictures | none: OUTDOORS / DUNGON print text only for finds, traps, stairs and locks | none |
| Text | the Apple II character ROM | not on the disks: the remaster's 5×7 font |
| Sound | speaker clicks (`CALL 770` with 768 / 769, `INFO` at $302, as II): the routine decoded (section 8.3) | `FLASH?`'s thunder played; combat's (OUTDOORS / DUNGON 7570–7575) not, the field (section 7) is silent `[~]` |

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
- No `town.backdrop`, item / event pictures, `ending.final` or `defeat.*`; the end scenes are `apple2_scenes` (section 8).
- `index.json`: `default_order` amiga, c64, apple2: the Amiga stays I's default, then the C64; with neither imported the
  Apple's pack is drawn (`Phantasie1AppleIIPackTests`).
- Import: `P1Apple2Pipeline` runs the art through `ImportContext.TryOptional` after the tables and text, whichever copy's
  tables win; a missing or malformed `SHAPES`, `HPIC` or `MN*` warns ("No Apple II art: ...") and the tables stay.

## 6. Open

- `[~]` The party cards (Guild, map, the remaster's combat screen) are race-keyed: a HUMAN / ELF MONK, PRIEST or WIZARD
  shows body 19, not the Apple's robed 27 (`figures/` has all four bodies × three poses). The combat field (section 7)
  draws the right body by race and class.
- The end scenes' `[~]`: section 8.5. Combat's speaker sounds and FOR-loop pauses (OUTDOORS / DUNGON) are decodable with
  section 8's routine and trace; the combat field (section 7) still uses its own `loop_ms` / `sparkle_ms` `[?]` and plays
  no speaker sound `[~]`.
- `[~]` The colour model is an emulator-style approximation; colour fringes per screen column not shown (one look per
  picture).

## 7. Combat field (s224) `[C]`

OUTDOORS and DUNGON hold the same combat lines (I's 1000–2006 differ only in odds and texts, not in drawing; II's
`OUTDOORS2` / `DUNGON2` are the same drawing lines but 4132, see the top). A fight has two screens:

- **The status and orders: a TEXT page** (1000–1470): `TEXT : HOME`, the party table (# NAME ATTACKS MAGIC HIT PTS
  STATUS), `RANK n  k NAME(S)` lines, PARTY OPTIONS 1)FIGHT … 5)FLEE, then INDIVIDUAL OPTIONS member by member. The
  Client keeps the remaster's combat screen there.
- **The round: the mixed hi-res screen** (1560 `POKE -16304,0 : POKE 34,20` to 2005 `GOTO 1000`): graphics rows 0–159,
  the four text rows 20–23 below. The set-up (4790–4811, once per OUTDOORS / DUNGON run) draws white lines at y 0 and
  y 159 across the screen and x 138 down, the party row, and clears x 139–279 (the map / dungeon marks go there). So
  the field is x 0–137; the map stays beside it during the round.

What the round draws:

| What | Where | Lines |
|---|---|---|
| Party row | slot PM's body (`FN P(168)`, TOWN 2350–2356: section 2.1) + arm 20, SCALE 1, HCOLOR 3, DRAW at (19·PM, 158); a member DEAD at the set-up is erased and not drawn (its slot stays empty); a parry that took (1430–1435, `IN% = 3`) gets arm 21 | 1525–1536, from 1520 at each round |
| Monster ranks | groups sorted by size each round (1047–1050: biggest first); rank 1 at y 135, rank 2 at 135 − YS%(rank 1), rank 3 at that − YS%(rank 2), 0 if that is ≤ 0; monster Y of a group of GN% + 1 living at x = INT(H% + Y·120 / (GN% + 1)), SCALE C%, HCOLOR H% | 1480–1500, 1540–1550 |
| The round start's erase | at the status page (1018 `HCOLOR = 0`, 1030) every monster is erased in both poses before the dead are dropped from the groups (1040–1046), so the next round's 1550 re-spaces the living | 1018–1046 |
| A member's swing | arm 20 erased, arm 22 drawn, before "HITS" / "MISSES"; it stays until the next round's 1525 | 1695 |
| A member's cast | "X CASTS A SPELL", then arm 21 (stays to the round's end), shape 23 XDRAWn at (19·PM + 19, 136), the four cast tones (7570), shape 23 XDRAWn again (off) | 1750–1764 |
| A monster's swing or special | pose A erased, pose B (S% + 1) drawn (2006, from 1895 before a melee and 1940 before a spell / breath); stays to the round's end. A third-rank monster with YS% < 40 does not swing (1891) | 2006 |
| Spell hit | after "TAKES n DAMAGE" from a party damage spell (`SP = 1`, set by the spell routines 2050 …), shape 24 XDRAWn on the monster at its SCALE, 500 empty loops, XDRAWn again | 4132 |
| A monster's death | "X DIES", then both poses erased | 4140–4145 |
| A member's death | "X DIES" only (4020): the figure stays drawn until 1530 skips the dead at the next set-up | 4020 |
| Text window | HOME at each member's turn (1610), each swing (1690) and each monster's turn (1860); the rows scroll past four | |
| Pauses | `FOR PS = 1 TO PA` after most lines (PA = 120 × TIME LAG, 361 / 7560: the T)IME LAG command's digit); the tones (`CALL 770`, 7570) are skipped with the sound off (NS = 1) | |

Not drawn: no grave, no flash of the screen for a hit (only 2065, the party's fire spells, toggles page 2 / page 1 with a
click per `SQR(damage)`: `POKE -16299 / -16300`, page 2 holds whatever is there `[~]`), no rank or monster names.

The remaster (`apple2_field` in `apple2.json`, `Apple2FieldLayout`; the Client's `Apple2FieldRun` /
`UiOverlays.DrawApple2Field`; tests `Apple2FieldTests`): with the ORIGINAL SCREENS switch on, each round plays on this
field (the Apple's 280 × 192 screen at (20, 4) of the 320 × 200 screen), the status and orders stay the remaster's
screen. The import (recipe 3) also draws shape 24 at every SCALE a monster record holds (`effects/24-spell-hit-scale-N.png`);
H%, SCALE, YS% and the DRAW points come from `extraction-index.json` (the Apple's data: the shipped tables hold DOS's YS%).
Differences, all `[~]` / `[?]`:

- The right half (the map) is drawn black; the Apple keeps the map there.
- Core's rank order stands for the Apple's size sort (1047); Core's round order and texts are DOS's (the field shows
  whatever Core's round prints, in the Apple's place and poses).
- One pause a line (TIME LAG × 120 loops at `loop_ms` 1 ms a loop, a rule of thumb for an empty Applesoft FOR / NEXT
  pass `[~]`), none after a "HITS" swing line (its damage line follows at once); the Apple pauses per swing / damage /
  special, after their lines.
- The cast tones' length (`sparkle_ms` 300) is a guess `[?]` (`CALL 770` not decoded); with the Sound switch off the
  sparkle is not shown (7570 returns at once).
- XDRAW is an exclusive-or: the hit and the sparkle are drawn over the picture, not XORed with it; colours per screen
  column are the extract's (one look per picture).
- 2065's page flip for the fire spells is not shown.
- Not compared with an Apple II emulator (none is set up: MAME is not installed and needs the Apple II ROMs).

II's Apple pack plays its rounds on the same field since s226 (`../phantasie2/GRAPHICS.md` section 7: I's numbers plus
the tossed rock). III's (its own field: `MONSTER PLOT` sprites, arms at (19·slot + 147, 198 − 8·row),
`../phantasie3/GRAPHICS.md` sections 2–3) still plays combat on the remaster's screen.
## 8. End scenes: `CEMETARY` and `FLASH?` (s224)

Both are short Applesoft programs that draw with HPLOT / DRAW / XDRAW, print in the four text rows under the graphics
(mixed mode) and wait with FOR loops and GETs. The remaster replays them statement by statement: the import writes the
game's shape table (`scenes/shapes.bin` = `SHAPES`) and a page-2 image (`scenes/page2.bin`), the pack's `apple2_scenes`
lists the statements with the interpreter's time for each, and the Client runs them on a hi-res page model
(`OpenPhantasie.Data.Apple2Hires`, `Apple2Scene`) under ORIGINAL SCREENS. Code: importer
`Pipelines/P1Apple2/P1Apple2Scenes.cs`; Client `Game1.Apple2EndScenes.cs`, `Apple2SceneView.cs`,
`Audio/Apple2SpeakerRenderer.cs`. Line numbers are `a2_applesoft.py`'s listing.

### 8.1 How it was read `[C]`

- **The ROM's drawing.** DRAW / XDRAW with ROT (the fallen party uses ROT 8–56) and HPLOT TO are the Apple II+ ROM's
  routines, ported as the ROM runs them: `DRAW` $F601 / `XDRAW` $F65D (ROT / 16 = quadrant; the cosine table at $F5BA
  gives each vector a major and a minor step, each carry plotting then moving, so SCALE 1 and 2 rotate unevenly), the
  cursor moves $F465–$F52E (left / right wrap within the row; up / down step the base address the ROM's way), `HPLOT TO`
  $F53A, `HPOSN` $F411 (the colour byte swapped on odd byte columns), `HCLR` $F3F2. XDRAW inverts the seven pixel bits
  only (never the colour bit). Checked: 150 random DRAW / XDRAW / HPLOT sequences byte-equal to the ROM's own code run in
  a 6502 emulator (py65).
- **The programs.** `tools/scripts/a2_basic_trace.py` boots the Apple II+ ROM to Applesoft in py65 (a 12 KiB ROM image
  and py65 kept outside the repo), puts the machine in the state the game leaves (TXTTAB $5101 as HELLO sets it,
  `SHAPES` at $451F, `INFO` at $302, HPAG page 1), pokes a party at SA = 16385, loads the program and RUNs it; every GET
  is answered at once. It logs every statement's start, the soft switches, key waits and speaker clicks in 6502 cycles
  (1.020484 MHz). `tools/scripts/a2_p1_end_scenes.py` turns two CEMETARY runs and a FLASH? run into the steps (each
  statement's time = until the next one starts). Test `P1Apple2ScenesTests`: the replayed page equals the ROM run's
  (sha1) at eight points (CEMETARY 550, 600, 665, 690; FLASH? 20, 100, 122, 160) for a party of four with bodies 19 /
  25 / 26 / 27. (py65 counts `DEC abs` as 3 cycles; the 6502 takes 6: corrected for the runs.)

### 8.2 What the programs do `[C]`

**CEMETARY** (party wipe; 42.4 s for four members without the key waits):

| Lines | Does | Time |
|---|---|---|
| 80–340 | HOME, CALL 62450 (clear page 1); the members' names from bytes 131–140 | 1.56 s (depends on the names `[~]`) |
| 400–530 | the fallen party: shape 19 at ROT 8 (SCALE 2) twice, 7s, 19 at the top (240, 0 / 130, 0: drawn upward from row 0, so the ROM's up-step leaves the page: only the bits it still steps onto the screen show, as the replay draws them), lines at rows 159 and 0, 19 + 21 / 22 / 20 at ROT 32 / 16 / 56 (SCALE 1), 19 at ROT 48 and 36 (SCALE 2) | 0.6 s; visible as drawn when OUTDOORS ran it; DUNGON 4200 switched to TEXT for its INSERT BOOT DISK page first, so there the field appears at 550 `[~]` |
| 540 | VTAB 24: [original text] / [original text] (the window scrolls: rows 22–23) | |
| 550–580 | POKE -16304 (graphics); 70 passes of XDRAW 6 at y 40 and DRAW 5 at y 50 for X = 100, 130, 160, then XDRAW 5 and DRAW 6. The first pass shows both; from then on 5 (y 50) and 6 (y 40) take turns: the monsters jump | 21.3 s (0.304 s a pass) |
| 600 | CALL 62450, TEXT, HOME: a blank text page while 610–660 draw unseen | 0.55 s |
| 610–665 | the temple (HPLOT), the party row (shape 19 + 20 at X = 65 TO 185 STEP 120 / (PO + (PO = 0)): every member as the fighter body, a lone member twice), the gargoyles (shape 11, SCALE 3, HCOLOR 6, each twice a pixel apart), then POKE -16304: the whole picture at once | |
| 667–670 | YOU HAVE AR[original text] (the Apple's spelling; the remaster prints ARRIVED, D-091), FOR 9000 | 8.96 s |
| 680–700 | the demon (shape 18, SCALE 4, HCOLOR 2); HOME, [original text], FOR 7000 | 7.06 s |
| 710–1500 | per member: name YOU HAVE BEEN, then DESTROYED (750: and 54 record bytes zeroed), RESURRECTED (900) or a return and [original text] (1100); GET; HOME; VTAB 24 | 0.67 / 0.10 / 0.15 s before the GET, 0.02 s after |
| 1550–1560 | the party state reset, HGR, `& RUN "OUTDOORS"` (Pelnor) | 0.36 s |

The Apple's own fate rule (725–740: P(4) < 1 destroyed; P(54) > RND·4 + 2 resurrected; P(54) > RND·3 undead; else
destroyed) is not used: the remaster prints Core's fates (DOS's rule) in the Apple's words with the Apple's time for that
path, as the C64's replay does.

**FLASH?** (ending; 15.9 s for four members without the key waits):

| Lines | Does | Time |
|---|---|---|
| 10–15 | HOME, HGR (graphics, mixed, page 1, cleared), the ground line (HPLOT 0,159 TO 279,159), FOR 500 | 0.82 s |
| 20–25 | lightning: GOSUB 1010 / 1030 draw a bolt (HPLOT, 9 points, HCOLOR 3) and erase it (the same in HCOLOR 0); GOSUB 1100 the thunder; FOR 500 pauses between: bolt 1, thunder, bolt 2, thunder, three pauses, bolt 2, thunder, two pauses, bolt 1, thunder, two pauses | 6.3 s; a bolt shows for its 0.09–0.10 s draw |
| 30–50 | the party: per member DRAW 20 (arm) and DRAW P(168) (its own body) at SCALE 2, x = 35 + PM · 180 / PO (120 alone), y 159 | 0.34 s for four |
| 100 | VTAB 24: [original text]; GET | |
| 110–120 | Zeus (shape 9, SCALE 4, HCOLOR 5 at 101, 80); twice: HOME, ten flips to hi-res page 2 and back (POKE -16299 / -16300: 6.9 ms page 2, 8.2 ms page 1), the thunder | 1.45 s |
| 122–125 | [original text] (3 spaces in), FOR 5000 | 4.9 s |
| 130 | VTAB 23: [original text] / [original text] / [original text] (2, 4, 2 spaces in; the last without a return); GET | |
| 135 | bolt 1 and its thunder, bolt 2 and its thunder: the erase in HCOLOR 0 cuts black gaps into Zeus and the figures and clears the colour bit of the bytes it crosses (Zeus's orange turns green there) | 1.43 s |
| 140–175 | the outcome (below) | |
| 160–200 | HGR (cleared), TEXT, INSERT BOOT DISK AND PRESS A KEY, `& RUN "OUTDOORS"` | the replay ends after the HGR |

**FLASH?'s outcome (140–200) `[C]`** (s226; record bytes are `FN P(X) = PEEK(SA + 170 · PM + X)`, SA = 16385):

- 140: `POKE SA + 1269, 177: POKE SA + 1268, 178: POKE SA + 1267, 179`: the last three slots (59, 58, 57) of the
  party's found-item list `SA + 1210..1269` (60 slots; OUTDOORS 7410 lists it, TOWN 8165 sorts and hands it out at
  D)ISTRIBUTE), written whatever they held. So the gifts go to the party, to be handed out in town.
- 150: for PM = 0 TO PO (every member, no status test): byte 42 := 100 and byte 120 := NOT byte 120. Byte 42 is the
  score (TOWN 4660–4663 the Mystic: raised to SC when lower; 6010 the party's highest to `SA + 1201`; 7452 printed as
  SCORE), DOS's field 42 / `@70`; 100 is tier 6, "SUPREME BEINGS" (TOWN 4656). Bytes 61..120 are the known flags of
  spells 1..60 (TOWN 3060 / 6885, OUTDOORS 7445: 1 = known), so byte 120 is spell #60, the Apple's divine spell; NOT
  makes 0 → 1 and anything else → 0 (a second win takes it away). #60 is cast only in the dungeon (DUNGON 6930 →
  5500: every member with byte 4 = 0 and byte 1 > 0 IS REVIVED, status 1, byte 4 := 3 + RND · 9; then the caster's
  byte 120 and MP (byte 11) := 0); TOWN 6930 refuses it ("YOU CAN NOT CAST THAT NOW").
- 170: `SA + 1194..1200` := 255, then 175 `SA + 1196` := 0: days out (`1194..1195`, OUTDOORS 7500–7520) 65,535, the
  XP pool (`1197..1198`, in tens) 655,350 XP, the gold pool (`1199..1200`) 65,535; `SA + 1196` = 0 is a plain town entry
  next time (TOWN 306). Region (`1191`) 19, cell (`1192`, `1193`) = (CX, CY) = (13, 18) (OUTDOORS 5000), DOS / Core
  (x, y) = (25 − CY, CX) = **(7, 13)** (FORMAT.md section 3.6; the same turn takes CEMETARY 1550's (5, 5) on OUT1 to
  DOS's wake cell (20, 5)): tile 75 ground, joined to OUT19's row 0 (OUT18, OLYMPIA) through the (11, 6) / (11, 5) text
  cells and to DNG10's entrance (20, 10).
- 200: `& RUN "OUTDOORS"`: the party stands in the wilderness; nothing is shared out, aged or restocked until the next
  town entry (TOWN 490 ages each member by days out / 7, about 36; 600–690 splits the pools by shares).

In the remaster (D-090, built s226): `p1.victory` = `apple` (default) gives 140's items (`Endgame.GiveVictoryGifts`:
the first free slots of Core's sack; a full sack's last slots `[~]`), 170's place (`Endgame.OlympusRegion / X / Y`) and
DOS's DIVINE I grant (status-OK members, after the roster write) for 150's toggle; 150's score and 170's pools and days
are **not built** (found s226, for the user: D-090). `dos` keeps DOS's Pelnor.

### 8.3 The speaker (`INFO` at $302, `CALL 770`) `[C]`

`$302 LDA $C030` (a click: the speaker cone flips) · `DEY` · `BNE $30D` · `DEC $301` · `BEQ $316` (RTS) · `$30D DEX` ·
`BNE $305` · `LDX $300` · `JMP $302`. So $300 (POKE 768) is the pitch: a click every 10 · pitch + 10 cycles (7 more
when Y wraps); $301 (POKE 769) the length: done after duration · 256 DEYs. FLASH? 1100 READs 15 pairs of DATA 1105
((1,1), (250,4), (1,1), (1,1), (255,7), (255,7), (1,4), (230,4), (1,5), (1,6), (210,3), (1,7), (1,9), (160,2), (1,9))
and CALLs 770 for each, entered with X = 157, Y = 2 (what Applesoft's CALL leaves): 7810 clicks in 0.52 s, a crackle
(pitch 1: a click every 20 cycles) with a few low knocks (pitch 160–255: about 200 Hz). The model's clicks equal the
emulator's to the cycle (`Apple2EndSceneTests`). The Client renders them as a two-level cone signal, box-filtered to
44.1 kHz with a DC block `[~]` (the speaker's analogue response is not modelled); the Sound switch silences it.
OUTDOORS / DUNGON 7570–7575 (combat) use the same routine with four pairs each (P1–P4, D1–D4): decoded by this, not
played (the combat field, section 7, plays no speaker sound) `[~]`.

### 8.4 In the remaster

- `apple2_scenes.defeat` / `.ending`: steps `{op, …, s}`; `repeat` (the dance), `party` (`xs` by party size, `shapes`
  with 0 = the member's body from `bodies`, TOWN 2350–2356: races 7+ 26, small races 25, HUMAN / ELF 19 or 27 for a
  MONK / PRIEST / WIZARD), `fates`, `flash`, `sound` (`sounds.thunder`: the 15 calls with their start times).
- The Client: the party wipe answers Core's first two prompts at once and runs CEMETARY on the outcome with Core's
  fates; FLASH? runs on the victory prompt with the party's members (the elemental left out), then Core's outcome goes
  on (`p1.victory`: Olympus by default, D-090; Pelnor under `dos`). A key, a click or the pad's A / D-pad is latched for the next GET, as the Apple's keyboard strobe holds a key
  (a key pressed during the dance answers the first fate at once). 280 × 192 centred on the 320 × 200 screen; text in
  the remaster's 5 × 7 font on 7 × 8 cells. Seen on Xvfb: `parity/s224-p1-apple2-endings-live.md`.
- Text: 13 string ids (`apple2.defeat.*`, `apple2.ending.*`) whose `p1-apple2` alternatives point into CEMETARY /
  FLASH? (`tools/scripts/p1_apple2_scene_text.json`); the public repo ships them as "...", an Apple import writes them.

### 8.5 Open

- `[~]` Times are the interpreter's for the traced party (names and bodies change them by milliseconds; the name loop
  of CEMETARY 320–340 by up to a second); a statement's drawing appears at its start, not pixel by pixel.
- `[~]` Page 2 (FLASH? 2100) is memory $4000–$5FFF: `scenes/page2.bin` holds `SHAPES` at $451F and FLASH? at $5101;
  the party records ($4001…), the variables and DUNGON's leftovers after FLASH? are not known and stay 0, and the text
  rows (text page 2) show blank. Each flip lasts 7–8 ms: at 60 frames a second a flash is a frame or two.
- `[~]` CEMETARY run from DUNGON (text mode until 550) is drawn as from OUTDOORS; DUNGON's / FLASH?'s INSERT BOOT DISK
  pages are not shown.
- `[~]` Text in the remaster font, not the Apple's character ROM (not on the disks); no flashing cursor.
- `[~]` `scenes/shapes.bin` / `page2.bin` exist only in an import's `files/` (the committed extract has no `scenes/`):
  a dev build that finds the pack in the repo without an Apple import draws the plain end screens.
- `[U]` FLASH? 150's score := 100 and 170's pools (655,350 XP, 65,535 gold) and days out (65,535) are not built (found
  s226, D-090: the decision named the gifts, the divine spell and Olympus only); the Apple's spell #60 (a dungeon mass
  revive) is DOS's DIVINE I in the remaster.
