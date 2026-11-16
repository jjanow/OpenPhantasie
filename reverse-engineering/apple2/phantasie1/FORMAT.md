# Phantasie I (Apple II, SSI 1984, VERSION 1.0): disk and data formats

The importer builds I from the Apple II copy alone (profile `p1-apple2`, s222; `docs/plan/10-importer.md` 7.14):
the Apple's text, DOS I's numbers where the Apple differs (the rule of the C64, Amiga and II / III Apple imports). This
file covers the disks, where each table lives, how each maps onto I's GameData (DOS I's schema) and what differs from
DOS I. Tags: `[C]` read in the code or data, `[~]` probable, `[?]` not known. Containers and the WOZ decoding:
`../phantasie2/FORMAT.md` section 1 (RDOS 2.1, 13-sector; I's `.woz` sides are WOZ 2 with `D4 AA B7` address marks).

Tools (all `python3 -I`, run from the repo root):

| Script | Does |
|---|---|
| `tools/scripts/a2_rdos.py <side.dsk> [--extract DIR]` | RDOS catalog + file reader |
| `tools/scripts/a2_applesoft.py <side.dsk> "<PROGRAM>"` | Applesoft detokeniser (line numbers below are its output) |
| `tools/scripts/diff_a2_p1_vs_dos.py [--built DIR]` | the Apple build against DOS I's GameData, cell by cell; writes `GameData/phantasie1/apple2-import.json` (section 5) |

There is no Python extractor: the C# pipeline (`src/OpenPhantasie.Importer/Pipelines/P1Apple2/P1Apple2Tables.cs`) is
the reader, and its raw build (cells not applied) is what the diff script compares (the importer test
`P1Apple2TablesTests.RawBuild_DumpsForTheCellScript` writes it when `OPENPHANTASIE_DUMP_P1_APPLE2=DIR` is set).

## 1. Images, version and played state

- Repo images: `reverse-engineering/apple2/original-images/phantasie1/`: the 4am crack (`4am-crack/`, two 140K `.dsk`,
  side A boot, side B dungeon; `4am-notes.txt`) and the original's flux (`woz-original/`, two `.woz`). `HELLO` prints
  "VERSION 1.0", `INTRO` / `BACKUP` "COPYRIGHT 1984 BY WINSTON DOUGLAS WOOD" `[C]`.
- The `.woz` pair is the copy the crack was made from: every file but `RDOS 2.1`, `SSI.INIT` and `QWERTY` is equal
  (`WozImageTests`), so both pairs import byte-equal (`P1Apple2TablesTests.WozPair_ImportsAsTheDskPair`).
- **Played** `[C]`: `CHAR` holds a roster (AARON, VIS, YORL, CASTER, ... 37 records), `TWN1`–`TWN11` / `TWN99` differ
  from the new-game template `TF` (9–54 bytes each), the regions are explored (fog lifted, fixed fights spent) and
  `DNGSAV` / `DNGCHK` hold a dungeon save. No unplayed Apple II I is known (`../SOURCES.txt`, s208: two more flux copies
  exist, both played).
- **What a new game resets** (side B `BACKUP`, option 5, lines 500–550) `[C]`: `CHAR` records 0–36 are zeroed (8225 to
  14514), `TF` is loaded and its twelve 250-byte slices stored as `TWN1`–`TWN11` / `TWN99` (line 450). Nothing else:
  the `OUTn` regions keep their fog and spent fights and `DNGSAV` / `DNGCHK` stay (DIFFERENCES.md D-088). So `TF` is the
  towns' pristine copy, and the elementals in `CHAR` records 37–39 survive every new game.

## 2. Programs and memory

Applesoft BASIC; `& RECALL / STORE / RUN / DEL / READ` are RDOS ampersand commands. Side A: `HELLO`, `INTRO` (start in
Pelnor / continue; loads `SHAPES` at $451F and `INFO` at $302; title picture `HPIC`), `TOWN` (town, Guild, shops,
training), `OUTDOORS` (wilderness + combat), `CEMETARY` (defeat). Side B: `HELLO` → `BACKUP` (backup disk, new game),
`DNGLD` / `DNGOUT` (enter / leave a dungeon, the dungeon save), `DUNGON` (dungeon + combat), `FLASH?` (Zeus, line 4300 of
`DUNGON` after monster 72).

Memory as II's (`../phantasie2/FORMAT.md` section 2) `[C]`: party records at `SA = 16385`, 170 B each; global state
`SA + 1189..1269` (1191 region, 1192 / 1193 cell, 1203 party size); region / level buffer `LA = 2050` (towns 2049);
`XA = 1990` (TINFO at XA + 310 = $08FC); `CALL 800` prints the CR-ended string whose address is POKEd at 803 / 804.

## 3. Tables and their GameData mapping

`P1Apple2Tables` puts each table into DOS I's layout (as `P1C64Tables` does for the C64) and runs DOS's builders
(`P1Dos/ComTables`, `DataTables`, `MapTables`, `Derived`), so every file has DOS I's schema.

### 3.1 Monsters: `MN0`–`MN9` (both sides, identical) `[C]`

Text files read with `& READ "MN"+MF` and `INPUT` (OUTDOORS 900–915, DUNGON 900): monster `MO` is record `MO mod 8` of
`MN<MO div 8>`, 80 monsters, 25 CR-ended fields (high-bit ASCII); stale bytes after a NUL are never read. Field order
(line 910) = the C64's (`../../c64/phantasie1/FORMAT.md` 3.1): `N$` name · `MA%` armor · `DM%` damage · `TM` level ·
`EX` xp · `SL%` special chance · `SP%(0..2)` specials · `DM` hp base · `PW%` combat rating · `WG`, `ML` companions ·
**`C%` SCALE, `H%` HCOLOR** (drawing) · `PM` reaction · `PL` next-group chance · `D%` defense · `X` gold · `Y` group size
factor · **`YS%`** (DOS byte 0x34) · `IA`, `IB` treasure items · `AK%` attack power · **`S%` shape**. Put into DOS's
0x39-byte record; fields 13, 14 and 24 are the art's (`GRAPHICS.md` 2.1), not the tables'. DOS's sprite (width, height) table has no Apple counterpart: the
shipped `sprite_sizes` (DOS's).

### 3.2 Items: `ITEM` + `TINFO` `[C]`

`ITEM` (side A): 180 CR-ended names, ids 1–180 (TOWN 230). No id 0: the import names it `NOTHING` (remaster wording, as
III's). Value of item i = `TINFO[89 + i]` (`PEEK(XA + 399 + i)`, TOWN 1117 / 1285 / 8430): all 180 equal DOS's.
`TINFO` is byte-identical to Phantasie II's Apple `TINFO` (sha1 `41823569…`; layout `../phantasie2/FORMAT.md` 3.4;
I's TOWN reads it at the same offsets: 0 HP factors (1520), 275 race and 350 class attribute modifiers (2260 / 2250),
380 spell levels (1570), 740 MP ceiling (1505), 830 skill coefficients (1512)). The import reads only the values; I's
creation tables are class (a) data (DOS's), not imported.

### 3.3 Names `[C]`

Classes TOWN 260 (`DATA THIEF,MONK,...`), races TOWN 270 (17: `LIZARD MAN` spelled out, `MINOTAUR`, `UNDEAD`; DOS's
table holds the first 15), items `ITEM`, monsters `MN*`, town names from `TF`. **No spell names**: spells are learnt,
listed and cast by number (OUTDOORS 1310 "CAST SPELL #", 6882). The import takes the manual's spell table and `DIVINE I`
for the divine spell (`spell_names`, as the C64's, D-087).

### 3.4 Elementals: `CHAR` records 37–39 `[C]`

`CHAR` (side A, 6800 B at 8225) holds 40 records of 170 B: the Guild (0–36) and the three elementals, names `FIRE
⌫⌫ELE`, `WATER ⌫ELEM`, `EARTH ⌫ELEM` (backspaces applied: `FIRE ELE`, `WATER ELEM`, `EARTH ELEM`). The record layout is
the C64's `CHAR2` (`../../c64/phantasie1/FORMAT.md` 3.5). A new game keeps them (section 1). Put into DOS's 282-byte
record; DOS's MP ceiling, creation number (23–25; the Apple's 38–40) and race (15; the Apple's 16) are cells, and `FIRE
ELE` takes DOS's `FIRE ELEM` (`name_fixes`, D-087's rule).

### 3.5 Towns: `TF` (side B, the template) and `TWN1`–`TWN11`, `TWN99` (played) `[C]`

`TF` (3000 B, load 5000) = the twelve towns in DOS order (Pelnor .. Starville, Olympia), 250 B each. Record = the C64's
and II's: 0–179 stock of ids 1–180 (`LA + id`), 180–224 restock pool, 226 price factor, 227 exit region, 228 exit row
y, 229 25 − x, 230–249 name (0x15-padded; Pelnor's `PELOR⌫⌫NOR`). The import reads `TF` only; the `TWNn` are the
profile's `mutable_files` at the sha1s of `TF`'s slices (a fresh game has exactly those). Restock on entry (TOWN
850–860): 18 stock cells of ids 0..120 cleared, then **5** pool items set (DOS 4, II's Apple 9) — a rule, noted here,
not imported (Core plays DOS's).

### 3.6 Wilderness: `OUT1`–`OUT16`, `OUT18`, `OUT19` (side A) `[C]`

1024 B (regions 1–5, 18, 19) or 768 B (6–16) (`LN = 256 * (3 + (FL < 6 OR FL > 16))`, OUTDOORS 7700):

- 0–519 grid: cell (CX, CY) at `LA + CY·20 + CX`, CX 0–19, CY 0–25 (line 390): DOS's `(25 − x)·20 + y` with CX = y,
  CY = 25 − x, the **same bytes** as DOS's grid. Fog = tile + 120 (5030); a fixed fight (tile mod 10 = 7) becomes
  tile + 1 when fought (6090), as on DOS.
- 520–594 features, 15 × 5 B (CX, CY, arg, dest x, dest y) = DOS's (25 − byte 1, byte 0, ...).
- 595– text: a text cell (tile mod 10 = 6) prints the string from `595 + 40·arg` to its CR (6000–6020), so one arg is one
  message of any length. DOS cuts text into 40-byte pages and adds 10 to arg per extra page; the import puts the
  Apple's screen lines into DOS's page form (`arg + 10·(lines − 1)`), as II's Apple import does for the ST's.
- **Orientation**: the Apple draws CX across and CY down (`DRAW ... AT 140 + 7·CX, 2 + 6·CY`, 5040) and moves N =
  CY − 1, E = CX + 1 (5220–5260): Apple north = DOS east, east = DOS south. The map is DOS's turned a quarter and the
  texts follow the Apple's compass (OUT1: "TO THE SOUTH IS A BUILDING CALLED THE RED PONY INN" = DOS's "TO THE WEST";
  scroll 5's "NORTHWEST PART OF GELNOR" = DOS's "NORTHEAST"). The import turns the region texts' and scrolls' compass
  words to DOS's map (`scroll_compass`, compounds whole), as II's Apple import (DIFFERENCES-II D2-046). Dungeon texts
  are not turned (the dungeon grid is drawn the same way on both: DNG1's "TO THE SOUTHWEST YOU HEAR ORC VOICES" is
  DOS's too).
- **Played state**: play stores the region back on leaving (7700–7710) with its fog and spent fights, and a new game
  does not restore it (section 1). The import gives every region DOS I's start state: DOS's Reset rule
  (`P1DosPipeline.RestoredRegion`: unfog, x8 → x7, fog) on all but OUT1 / OUT5, which DOS ships open. The cells that
  still differ are the Apple's own data (OUT1's row 0 and (18, 14), OUT13 / OUT15 one cell each) and take DOS's value.

### 3.7 Dungeons and messages: `DNG1`–`DNG10` (side B; 6140 B, DNG3 / DNG10 5120 B) `[C]`

- 0–1699: the Phantasie I level format, DOS's layout (grid 33 × 38 at `LA + 33·CY + CX`, events at 1270 = `3320 − LA`
  (8200), scripts at 1450 = `3500 − LA` (8240), level number at 1255 (455; monster level `12·VL`, 7735)). Byte-equal to
  DOS's level files up to 1962 but 28 bytes (section 5). The import keeps bytes 0–1961 (`LevelBytes`; DOS's files are
  these plus a 20-byte tail, DNG6 1922 B).
- Messages: slot M is 40 B at `1700 + 40·M` (`TM = 3750 + 40·M1`, 8650), high-bit ASCII, printed by CALL 800 to its CR,
  so a long message runs on into the next slots; backspaces overprint (`1-OPEN IT⌫⌫THE CHEST` shows `1-OPEN THE
  CHEST`). Menus are part of the text ("1-OPEN THE CHEST / 2-LEAVE IT  SELECT 1 OR 2"); an event record asks for
  `PEEK(LC + 1)` choices (8220). The import lists each message by its first slot (`lines` = its screen lines) and splits a
  menu into `options` when an event record asks for that many choices (else when the markers 1-, 2-, ... are there), the
  trailing "SELECT ..." dropped; the code of II's Apple import (`A2GameData.SplitMenu`). Every event a level's grid holds
  finds its choices (`P1Apple2TablesTests.EveryEventMenu_HasItsChoices`). Script ops 1–19 (8260; the import keeps the
  bytes, Core runs DOS's ops; whether each Apple op does what DOS's of that number does is not checked `[~]`).
- `messages.json`: the loader wants ten areas; the import writes each level's slots in DOS's area shape (the game reads
  dungeon text from `dungeons.json`). Wording differs from DOS's in places (punctuation, layout); DNG7's bleeb puzzle
  reads as DOS's (the speakers and question 3 of D-084 are DOS's on the Apple).
- `DNGSAV` / `DNGCHK` are the dungeon save (DNGOUT 50–70), never read.

### 3.8 Scrolls: `SCROLL1`–`SCROLL20` (side A) `[C]`

1000-B dumps of the 40 × 24 text page ($400, interleaved rows), loaded straight onto the screen (TOWN 8010:
`& RECALL "SCROLL" + STR$(IM - 120)`); `SCROLL20` is 768 B (rows 6, 7, 14, 15, 22, 23 not in the file, blank). One file
per scroll (no continuation pages, unlike II). The import takes the rows between the frame's two `+---+` borders, inside
the `! ... !` sides where a scroll has them, compass words turned (3.6). Scroll 5's third pool lies "EAST OF THE STAR" on
the Apple = DOS's SOUTH (the C64 says NORTH, D-085; the Apple agrees with DOS here).

### 3.9 Other files (not read)

`CHAR` records 0–36 (the roster), `TWNn`, `DNGSAV`, `DNGCHK` (play state); `SHAPES` (2930 B at $451F, shape table),
`HPIC` (title picture), `INFO` (238 B at $302, byte-identical to II's `INFO`, sha1 `af28cae4…`), `QWERTY`, `SSI.INIT` (RDOS init, cracked on
the 4am pair), `RDOS 2.1` / `SYSTEMBOOT`. The art (`SHAPES`, `HPIC`; the only hi-res files) is the `apple2` graphics
pack (s223): `GRAPHICS.md`.

## 4. Key files (importer detection)

Profile `p1-apple2` (`src/OpenPhantasie.Importer/Profiles/profiles.json`): side A `TOWN`, `OUTDOORS`, `CEMETARY`, `ITEM`,
`TINFO`, `SCROLL1`; side B `DUNGON`, `DNG1`, `DNG10`, `TF` (names on both sides, `MN*`, `HELLO`, `QWERTY`, are not key
files). Name markers `TOWN` + `OUTDOORS` (II's Apple programs are `TOWN2` / `OUTDOORS2`, the C64 I's `TOWNR`).
`mutable_files`: `TWN1`–`TWN11`, `TWN99` at `TF`'s slices (section 3.5); the held pair reports all twelve played and
imports as a fresh copy would (`ImportRunner.PlayedCopySafe`). `CHAR` and `OUTn` have no pristine sha1 (no unplayed
copy); the import reads only CHAR's elementals and gives the regions DOS's start state, so a played copy loses nothing.

## 5. Apple II vs DOS: what the diff finds (s222)

`diff_a2_p1_vs_dos.py --built` on the raw build; every number below is a `dos_cells` row of `apple2-import.json` (394):

- **Monsters** (276 cells): 28 monsters differ in numbers (group size factor 23, companions, next-group chance,
  defense, BLACK KNIGHT's group size as the C64's; FIRE ELEMENTAL's reaction, next-group chance, defense, gold, second
  treasure and attack power, where DOS's read like the Apple's one field on), and byte 0x34
  (`YS%`, the Apple's rank height; DOS 35 / 62) on 79. The Apple's v1.0 is an older balance than DOS's 1987 numbers.
- **Regions** (79): OUT1's row 0 (100 vs DOS 101), (18, 14), its feature order (as the C64's) and leftover bytes in
  unused feature records on DOS's side; OUT4's two one-line texts DOS gives two pages (arg 10).
- **Dungeons** (28 bytes): DNG3 (2, 14), DNG6 (12, 11), DNG7 (19, 11) as the C64's D-086, other grid cells of DNG2 /
  3 / 5 / 6 (reachability not checked `[~]`), DNG3 / DNG10 row 0 leftovers, DNG10's level byte (24 vs DOS 5), event / script bytes of DNG5, 7, 8, 9.
- **Elementals** (9): MP ceiling, creation number, race. **Towns** (2): Pelnor's stock of item 22 (5 vs DOS 0).
- **Items**: all 180 values equal. **Scrolls / messages**: text only.
- **Text kept as the Apple's**: item names (`SH SWORD`, `HEAL POT1..9`, rings `RING I`..`RING IX`, `--WAND--`, the
  treasures `QUARTZ`, `CRYSTAL`, ..., `GOLDEN OX`, `GOD RING` where DOS has `EMERALD A`..`DIAMOND E`), town 4
  `HOBBITON` (DOS `HALFLINGOR`), race `LIZARD MAN`, message and scroll wording. The Apple's monster typos `APRENTICE DEVIL`,
  `MASTER ILLUSTIONIST` import as DOS's `APPRENTICE DEVIL`, `MASTER ILLUSIONIST` (`name_fixes`, DIFFERENCES.md D-089).
- **Ending**: the Apple's `FLASH?` shows a short Zeus scene ("YOU ARE HEROES OF GELNOR !!! I GIVE YOU THREE GIFTS AND
  YOUR FIRST DIVINE SPELL."), gives items 177–179 and the divine spell, and puts the party on Olympus (OUT19 (13, 18)).
  The import keeps DOS I's ending (Core's rules); Zeus's lines have no `p1-apple2` text (section 6).

## 6. Strings

`build_string_sources.py` adds `p1-apple2` alternatives for the ids whose DOS I (or Amiga I) parts are all in `TOWN`,
`OUTDOORS`, `CEMETARY` or `DUNGON` verbatim (254 ids: I 174, shared 67, II 11, III 2). An Apple-only import writes them,
the common fallback and `phantasie1/strings/fallback.p1-apple2.en.json`: the 32 I ids the Apple lacks verbatim (23 shared
field / map menu words, box titles and sheet labels with the table's text; Zeus's speech and the Black Lord line in the
remaster's wording, since the Apple's `FLASH?` words its own short scene). No I line stays "..." in a public build.
The Apple's own Zeus lines as an own-wording file (as the C64's `P.END`) `[~]`.
