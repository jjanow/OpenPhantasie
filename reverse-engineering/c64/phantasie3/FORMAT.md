# Phantasie III (Commodore 64, SSI 1987): disks, files and tables

Where III's game tables live on the C64 disks and how they compare with DOS III (the reference,
`../../dos/phantasie3/FORMAT.md`, `extracted/`) and the Apple II (`../../apple2/phantasie3/FORMAT.md`). First step of
a C64 port (s216): data formats only, no importer code (section 7 is the plan). Tags: `[C]` confirmed in the bytes on
every record against DOS's / the Apple's data, `[~]` probable, `[?]` not known. Offsets are into the file **after** its
2-byte PRG load address.

**Not I / II's engine.** C64 III is 6502 machine code (`PHANT.OBJ`, `TOWN.OBJ`, `DUNGEON.OBJ`), not compiled BASIC:
`../bytecode-notes.md` does not apply. Its data is a **hybrid**: DOS III's binary records (monsters, dungeons,
armory rows, the Scandor map, the item / spell tables) with the Apple II's upper-case text (item names, dungeon texts,
scroll pages) and the Apple's corrections of a few DOS data faults.

Extract in the repo: `extracted-v1.0/` = C64PP v1.0 (!) sides 1 + 2 (`tools/scripts/extract_c64_d64.py --out …`;
`manifest.json` lists every file with size, load address and hashes). v1.2 (alt) is not extracted in the repo.
Comparisons below were made with throw-away scripts reading these files and DOS III's `unpacked/ssi-1988/` files /
`extracted/*.json` and the Apple's `extracted/*.json`.

## 1. Disks and versions

One disk, two sides: side 1 (no disk name; 9 files) the boot and the main program, side 2 (`DUNGEON`; 64 files)
towns, dungeons, scrolls, maps, the roster and the utility. Every G64 decodes to all 683 sectors
(`tools/scripts/c64_g64.py`). No version string was found in the programs; C64PP's tags are used.

| C64PP image | State |
|---|---|
| **`phantasie_iii_s1/s2[ssi_1987](v1_0)(!)`** | **the reference**: C64PP-verified |
| `(v1_2)(alt)` | data files byte-identical to v1.0's but `TITLE` and `PCHARS0`; programs differ: `PHANT.OBJ` 22,555 B (v1.0 22,549), `TOWN.OBJ` (same size), side 2's `B` (5,177 B; v1.0 3,686); `PCHARS0` = the starter party re-rolled (3.5); every table of section 3 sits at the same offsets with the same bytes (`PHANT.OBJ`'s area levels 2 bytes later) `[C]` |
| plain `(v1_2)` (not taken, SOURCES.txt) | a patched copy |
| held `../original-images/phantasie3/*.d64`, `v1.1/` | the ASS crack lineage (SOURCES.txt) |

**Side 1's free sectors** (correction to SOURCES.txt's "mostly raw sectors behind a 9-file directory"): apart from the
9 directory files, side 1 holds only the 1541 format fill (`4B 01 01 …`) and **a stale program chain** from track 12
sector 13, free in the BAM: an earlier `PHANT.OBJ` (89 blocks, load `$1400`; v1.0: 22,545 B, another build; v1.2 (alt):
byte-identical to v1.0's `PHANT.OBJ`), plus 3 stray sectors on track 17 and 17 BAM-used, unlinked ones on track 35
`[C]`. The game's data is all in directory files.

## 2. Files

| File(s) | Side, bytes (with load address), load | Holds | Tag |
|---|---|---|---|
| `PHANTASIE III` | s1, PRG 65, `$02C5` | autostart stub | `[~]` |
| `PHANT.OBJ` | s1, PRG 22,549, `$1400` | main program (wilderness, combat, party screens); the spell, class, race, social-class, town and inn **names**; item points (3.2), wilderness area levels (3.6) | `[C]` names / tables |
| `TOWN.OBJ` | s2, PRG 14,473, `$1C19` | town program; item requirement / value / price tables, HP dice, magic ceilings, skill tables, spell availability (3.2, 3.3) | `[C]` |
| `DUNGEON.OBJ` | s2, PRG 9,829, `$2000` | dungeon program; endgame texts (Zeus / Pluto, Super Hero / Dark Lord) | `[C]` strings |
| `MONSTERS` | s1, PRG 8,035, `$2000` | 4,992 B of monster pictures, then **80 × 38-B monster records** at 4992 (DOS's `CREATURE.DAT` layout), 1 byte (3.1) | `[C]` records |
| `ELEMENTALS` | s1, PRG 512, `$2000` | AIR / MIST / MUD ELEMENTAL, 3 × 170-B records (3.5) | `[C]` |
| `DATA` | s1, PRG 3,936, `$2000` | 6502 code (disk routines) then **180 item names** at 2313, 9 characters each, last one with bit 7 set (3.2) | `[C]` names |
| `TOWN A`–`TOWN Q` | s2, PRG 183, `$2000` | armory stock of towns 1–17 = DOS `INITARMS.DAT` rows 1–17 (3.4) | `[C]` |
| `MATERIALMAP` | s2, PRG 3,753, `$4000` | the Scandor map, 50 × 75, DOS's `WORLD.DAT` layout + 1 byte (3.6) | `[C]` layout |
| `LIGHTMAP`, `DRKPLN`, `NETHERMAP` | s2, PRG 165 `$7000` / 168 `$2000` / 450 `$6000` | the Light, Dark and Netherworld plane maps: **not** DOS's 81 / 81 / 496-B `PLNS.DAT` parts (163 / 166 / 448 B) | `[?]` layout |
| `DNG1`–`DNG9`, `DNG0` | s2, PRG 4,135 (`DNG4` 4,138, load `$2000`), `$1000` | dungeons in DOS's `DNGS.DAT` record layout (3.7) | `[C]` |
| `SCROLL A`–`SCROLL T` | s2, PRG 963 (`T` 961, load `$2000`), `$6000` | the 20 scroll pages: 40 × 24 screen codes, framed (3.8) | `[C]` |
| `PCHARS0` | s2, PRG 3,063, `$A300` | the Guild roster: 18 × 170 B; slots 0–5 the manual's **pre-generated party** (CHUMLEY, TRACKER, FINGERS, GRASSHOPPER, BONES, SAURIAN, level 1) (3.5) | `[C]` names, `[~]` layout |
| `DNGIP1` | s2, PRG 5, `$687F` | dungeon-in-progress flag (`DUNGEON.OBJ` names `DNGIP` / `DNGIP1`) | `[~]` |
| `B` | s2, PRG 3,686, `$0801` | the utility (plain BASIC): 1 play the game, 2 make a dungeon disk, 3 / 4 transfer Phantasie I / II characters to III format; loads `UTILITIES.OBJ`, `P3COPY.OBJ` | `[C]` |
| `UTILITIES.OBJ`, `P3COPY.OBJ` | s2, PRG 313 `$C100` / 1,841 `$C800` | the utility's machine code (disk copy, transfer) | `[~]` |
| `TITLE`, `PSET0`, `PTOWN1`, `HEROSPRITES`, `GUT`, `TOT`, `SCHLECHT`, `SND` | s1 / s2 | title picture (10,003 B `$6000`), character sets (2,050 B), hero sprites (3,458 B), the three endgame pictures (4,098 B `$8000`; German file names: good, dead, bad), a sound table (177 B `$126E`) | `[~]` |

**Files play rewrites** `[~]`: `PCHARS0` (the roster), `TOWN A`–`Q` (stock: buying / selling; `TOWN.OBJ` has
`SAVE(Y/N)? … SAVING...`), `DNGIP`, `DNGIP1`, and maybe `DNGn` (`DUNGEON.OBJ` names `DNG1`). The two C64PP copies have
equal `TOWN` and `DNG` files, so those are probably as shipped; their `PCHARS0` differ (3.5). Every `DNGn`'s event
table equals DOS's shipped one, once-only bytes included (3.7): no dungeon was played. `DNG4` and `SCROLL T` load at
`$2000` where their siblings load at `$1000` / `$6000` (`DNG4` is also 3 bytes longer); under I / II's rule that would
mark a file written another way, but their contents are DOS's / the Apple's `[?]`.

## 3. Tables

### 3.1 Monsters: `MONSTERS` + 4992 `[C]`

80 records of 38 bytes in DOS's `CREATURE.DAT` layout (`../../dos/phantasie3/FORMAT.md` 4.4: name 14 B mixed case,
group max, attack power / 10, damage, armour, defence, level, specials, rating, HP base, reaction, picture byte 27,
next-group chance, companions, treasure, flag 33, gold and XP big-endian), then one byte (`04`). **Every byte equals
DOS's but:**

| Monster | Field | C64 | DOS | Apple |
|---|---|---|---|---|
| all 80 | byte 27 (DOS: picture id; > 41 = a giant picture, which drops the second group and caps the count at 2) | **126–202, even** (the C64's own picture numbering) | 0–65 | none |
| Drip Slime (2) | name; XP | `Drip Slimes`; **87** | `Drip Slime`; 97 | 87 |
| Kobold (9) | next-group chance | **100** | 7 | 100 |
| Wight (25) | reaction | **−10** | −11 | −10 |
| Wolf (31) | the record | **a full record**: damage 16, armour 2, level 5, special 3 = 0, rating 7, HP 14, next group 65, companions 31, 31 | bytes 16–29 shifted by one (DOS's Wolf is a zero-level stub) | = C64 |
| Dragon (54) | next-group chance | **60** | 0 | 60 |

So the C64 has DOS's numbers everywhere the Apple differs from DOS (attack powers, Goblin gold, Kobold damage, Giant
Ghost, Dragon King, Spirit, the bosses' packed values) **except these five records, where it has the Apple's fix**.
How the C64 applies the giant rule with its byte 27 values is not read `[?]`.

### 3.2 Items `[C]`

- **Names** (`DATA` 2313, 9 characters, upper case): the Apple's abbreviations (`GLOVE`, `WOOD SHLD`, … `NIKS WAND`),
  equal to the Apple's 180 but `HALBERD` / `HALBERD+n` (Apple `HALBRED`) and ids 90–92 `SWORD +3` / `+4` / `+5` (Apple
  and DOS `+4` / `+5` / `+6`).
- **Points**, ids 0–100: `PHANT.OBJ` 1912, **all 101 equal DOS's** (`DS:0x113c`).
- **Requirement**, ids 0–100: `TOWN.OBJ` 1447, equal DOS's but **id 42 (Self Bow +1) 0** (DOS 2).
- **Value**, ids 0–180, u16 split into a low-byte table (`TOWN.OBJ` 1548) and a high-byte table (1729): equal DOS's but
  **id 27 (Chain Mail) 234** (DOS 232), **id 69 (Hammer) 89** (DOS 59; the Apple's 89), **id 111 (Magic 1) 16** (DOS 29).
- **Town price index**, towns 0–17: `TOWN.OBJ` 1955, equal DOS's but **Tirith (6) 199** (DOS 200; the Apple's 199) and
  the unused town 0 218 (DOS 100).

### 3.3 Rule tables: `TOWN.OBJ` `[C]`

Level-major, **Phantasie I's class order** (THIEF, MONK, RANGER, FIGHTER, PRIEST, WIZARD; the class names in
`PHANT.OBJ`): byte = `base + 6 · (level − 1) + class`.

| Offset | Table | vs DOS (`rule-tables.json`) | vs Apple |
|---|---|---|---|
| 2346 | HP dice, levels 1–15 | equal (levels 1–15; DOS's 16–22, all = 15's, are not stored) | equal |
| 13601 | magic ceiling, levels 1–15 | equal | equal |
| 685 + 96 k | skill tables, 16 rows each, k = ATTACK, PARRY, FIND ITEM, FIND TRAP, DISARM, LISTEN, PICK LOCK, SWIM | **the Apple's cells** (Phantasie I's): DOS's five D3-032 data errors are not here (Ranger Attack L15 250, Thief Pick Lock L3 55, Parry not shifted) | **equal, all 8 tables** |
| 2448 | spell availability, per class (Thief 1 row, Monk 2, Ranger 2, Fighter 1, Priest 3, Wizard 4; level-major within a class, ends 2643) | **equal** (every (spell, level) pair of every class) | |

The training XP / gold curves, score weights, hit-location rows, terrain offsets and the Air elemental's spell list were
not found as DOS-shaped tables in the program files `[?]` (searched as bytes, u16 LE / BE, both class orders).

### 3.4 Towns: `TOWN A`–`TOWN Q` `[C]`

181 B = DOS's `INITARMS.DAT` row (byte 0, then the stock of ids 1–180); `TOWN A` = row 1 (Pendragon) … `TOWN N` = row
14 (Sciattica), `O` / `P` / `Q` = the plane towns 15–17 (Light, Dark, Death). Byte 0 is **0 on every C64 town**
(DOS: the number of item kinds stocked, which DOS copies but the C64 may count itself `[?]`). Stock differences (id:
C64 / DOS / Apple):

| Town | Differences | Shared with the Apple |
|---|---|---|
| Pendragon | Knife (61) 5 / 1, Club (63) 5 / 1 | both |
| Pendleton | Knife 5 / 0 | yes |
| Rocky Hills | Club 0 / 1 (Apple 5), id 67 1 / 0 | id 67 |
| Deltor | Leather (23) 1 / 0, id 41 0 / 1, id 43 1 / 0 | all three |
| Asmith, Town of Death | Staff (66) 1 / 0 | **no** (C64 only) |
| Xanador | Leather 1 / 3; potions 103, 113 **72** / 77; scroll **134 99** / 0, **137 0** / 99 | no |
| Flagler, Lansing, Scanport | Leather **5** / 1 (Apple 0); Lansing id 67 1 / 0 (Apple 1); Scanport scroll **134 200** / 0, **137 0** / 200 | id 67 only |
| Sciattica | id 83 1 / 0, id 84 0 / 1 | no |

Xanador and Scanport stock Scroll 134 where DOS and the Apple stock Scroll 137 `[~]` (which scroll that is on the
C64 depends on the item → page map, not read).

### 3.5 Characters: 170-byte records `[~]`

`ELEMENTALS` (3 records) and `PCHARS0` (18 records) use a 170-byte record with the name at 131 (Phantasie I / II's
record, the Apple III's too; not DOS III's 262-byte one): `ELEMENTALS` = AIR, MIST, MUD ELEMENTAL (DOS's Air / Mist /
Mud), level 20 at P(54), race 16; HP 150 / 200 / 250 at P(12–13). `PCHARS0` slots 0–5 = the manual's pre-generated,
pre-equipped party ("listed as the first 6 members of the Guild", manual p. 7 and Appendix I) `[C]`; slots 6–17 empty.
v1.0 and v1.2 (alt) hold the same six names with other attributes, HP and skills (126 bytes differ): re-rolled per
release, or play `[?]`. DOS III's pre-generated party (if it has one) was not compared.

### 3.6 Wilderness `[C]` layout, `[?]` tile meaning

- **`MATERIALMAP`** = 3,750 B in DOS `WORLD.DAT`'s layout (50 × 75, row-major) + 1 byte (`0x14`). Its tile ids are
  DOS's in 2,215 cells; the rest are other ids of the same terrain, as far as DOS's ranges tell: water 0 ↔ 1 swapped (DOS
  row 0 = 1, C64 0), mountain variants 242–244 (DOS's Styx range), desert edges 16–19 (DOS's water range). By DOS's
  terrain table, 3,664 of 3,750 cells keep their terrain; the other 86 (26 mountain → 242–244, 20 meadow → desert ids,
  19 desert → 16–19, …) need the C64's own tile → terrain table (in `PHANT.OBJ`, not read). **Place tiles**: the 13
  town, 8 dungeon and 8 inn squares are DOS's squares, but the town tile ids are permuted (DOS 30 → 31, 31 → 30,
  32 → 33 … 37 → 38, 38 → 32), and **Pendragon's square (24, 37) holds 25**, not a town tile `[?]` (the C64's start town
  may be drawn by code: `TOWN.OBJ` asks "IN WHO'S TOWN WILL YOU START?").
- **Area levels**: `PHANT.OBJ` 14888, 25 bytes = DOS's `wilderness_area_levels` exactly.
- **Planes**: `LIGHTMAP` 163 B, `DRKPLN` 166 B, `NETHERMAP` 448 B: own layouts (DOS: 9 × 9, 9 × 9, 31 × 16) `[?]`.
- Town, inn, terrain and plane names: `PHANT.OBJ` (`PENDRAGON` … `TOWN OF DEATH`, `MYSTIC INN` … `DEADLY INN`,
  `GRASS FOREST HILLS MOUNTAINS MIST PATH BLACK P…`).

### 3.7 Dungeons: `DNG1`–`DNG9`, `DNG0` `[C]`

DOS's `DNGS.DAT` record layout (`../../dos/phantasie3/FORMAT.md` 9.5): `DNGn` = DOS record letter n (A…I), **`DNG0` =
record J** (DOS's J and K are byte-identical: dungeons 1 and 8 share it, as on the Apple). **Bytes 0–`0x5D2` (map,
level, forced fights, exits, event squares, event programs, text pointers) equal DOS's in all ten**, including the
three places where the Apple differs from DOS (dungeon 5 = `DNG1` square / programs, dungeon 4 = `DNG4` square
`0xE2`'s message / program, dungeon 9 = `DNG9` programs): the C64 has DOS's there. The texts sit at DOS's pointers (pointer − `0x800`) as
**CR-ended upper-case strings in the Apple's window font** (`=` blank, `:` hyphen, `<` comma; the Apple III's wording:
"ARROWS FIRE FROM A SLIT IN THE CEILING" where DOS has "[original text]"). Seven files equal the
Apple's `DNGn` byte for byte (+1 byte); `DNG1`, `DNG4`, `DNG9` differ from the Apple's only where the Apple differs
from DOS (and `DNG1`'s text area follows DOS's pointers, without the Apple's two extra slots "YOU SCUM." / "YOU MUST
RETIRE.").

### 3.8 Scrolls: `SCROLL A`–`SCROLL T` `[C]`

960-B pages of 40 × 24 screen codes (framed `+---+` / `! … !`, upper case): `SCROLL A`–`T` = the Apple's `SC1`–`SC20`,
word for word but hyphenation and the first text row of four pages (`C`, `D`, `J`, `K`), which the
Apple's extract leaves out `[~]` (`F` = `SC6`, Scroll IV's map of the known world in characters; `T` "DELIGHTFULLY",
Apple "DELIGHFULLY"). So C64 scroll text is the Apple's, not DOS's mixed-case records. Which item shows which page is
in the programs, not read `[?]` (the Apple: item i shows `SC(i − 120)` and chains on, D3's item → record map).

### 3.9 Names `[C]`

`PHANT.OBJ`: classes (I's order), races (`HUMAN … UNDEAD ELEMENTAL`), social classes (`PEASANT LABORER CRAFTSMAN NOBLE
SUPER HERO DARK LORD`), status (`DEAD OKAY DOWN SLEP OUT`), planes (`GELNOR FERRONRAH NETHERWORLD SCANDOR LIGHT DARK`),
**56 spell names in DOS's order #1–52, 54–57** (no #53, as DOS: `HEAL 1` … `ARROW FLAME 1`, `P ARROW FLAME 1` …,
`DISPELL UNDEAD`, `MONSTER EVAL.`, `TRANSPORT`, `ACCURACY`, `P ACCURACY`, `DIVINE`; unlike C64 I / II and the Apple, III's C64 has spell names), combat
options, body areas. `TOWN.OBJ`: the town menus, the Mystic's ranks (`NOVICE ADVENTURERS` … `ALSO HEROES OF
FERRONRAH`), skills. `DUNGEON.OBJ`: the endgame. Monster names: DOS's mixed case (`MONSTERS`).

## 4. C64 vs DOS in short

- **Numbers = DOS's** in the dungeon tables, the monster records (but five), item points, the spell table, HP dice,
  magic ceilings, area levels, most stock.
- **The Apple's fixes / values** where the Apple differs from DOS in: five monster records (Drip Slime XP, Kobold, Wight,
  Wolf, Dragon), the eight skill tables (no D3-032 errors), Hammer's value 89, Tirith's 199, part of the starting stock.
- **C64 only:** byte 27 (pictures), requirement 42, values 27 / 111, the stock cells marked "no" in 3.4, the world map's
  tile ids and Pendragon's square, the plane maps, byte 0 of every town row, `SWORD +3…+5`, the starter party.
- **Text:** the Apple's upper-case wording (items, dungeon texts, scrolls), DOS's monster names, own spell-name
  abbreviations.

## 5. Rules

Not read (machine code; no disassembly yet). Candidates: the giant rule with byte 27 (3.1), the town-row byte 0, the
training curves (no DOS-shaped table found).

## 6. Graphics and sound

Not decoded: `MONSTERS` 0–4991 (monster pictures; byte 27 points into them `[~]`), `TITLE`, `PSET0` / `PTOWN1`
(character sets `[~]`), `HEROSPRITES`, `GUT` / `TOT` / `SCHLECHT` (endgame), `SND`. Palette: the VIC-II's 16 colours.

## 7. Importer plan: `p3-c64` (plan only, s216)

The rule of the Apple and Amiga imports (`../../apple2/phantasie3/FORMAT.md` section 4): **DOS's values where the C64
differs or has none, the C64's own text**, built into DOS III's GameData shapes (the decisions below).

**Containers.** `.d64` / `.g64` as `p1-c64` (shared reader). Side 1 + side 2 of the one disk.

**Detection.** Side 1: `PHANT.OBJ` + `MONSTERS` + `DATA`; side 2: `TOWN.OBJ` + `DUNGEON.OBJ` + `MATERIALMAP` + `DNG1`.
v1.0 sha1s: `PHANT.OBJ` `f80c4b13…`, `TOWN.OBJ` `4da0e556…`, `DUNGEON.OBJ` `ca18138f…`, `MONSTERS` `4e5823e5…`, `DATA`
`b445fbad…`, `MATERIALMAP` `e29ddbfd…` (full hashes in `extracted-v1.0/manifest.json`); v1.2 (alt) shares every data
file and has its own `PHANT.OBJ` / `TOWN.OBJ` (same table offsets).

**What maps where:**

| GameData file | From the C64 | Cells that take DOS's values |
|---|---|---|
| `monsters.json` | `MONSTERS` records | byte 27 (DOS's picture ids), the five Apple-shared records |
| `items.json` | `DATA` names, `PHANT.OBJ` points, `TOWN.OBJ` requirement / value / price index | requirement 42, values 27 / 69 / 111, Tirith, town 0 |
| `rule-tables.json` | `TOWN.OBJ` HP dice, ceilings, skills, spells; `PHANT.OBJ` area levels | HP dice 16–22, the tables the C64 lacks; the skill cells are already D3-032's default |
| `towns.json` | `TOWN A`–`Q` | the stock cells of 3.4, byte 0 |
| `dungeons.json` | `DNG0`–`DNG9` (tables = DOS's; text in the Apple's window font) | — (tables equal) |
| `world.json` | `MATERIALMAP` (needs the C64 tile → terrain table first) | the tile ids / place tiles; plane maps (C64 layouts not decoded) |
| `scrolls.json` | `SCROLL A`–`T` (the Apple's pages) | — |
| `names.json`, `spells.json` | `PHANT.OBJ`, `TOWN.OBJ`, `DATA`, `MONSTERS` | — (the C64 has every spell name) |
| `elementals.json` | `ELEMENTALS` (170-B records) | DOS's numbers |

**Applied by default** (already decided; the importer follows these unless the user says otherwise):

- **DOS's numbers** wherever the C64 differs (DOS wins on numbers; the Apple III import's rule, `apple2-import.json`;
  D-086 for C64 I): the five Apple-shared monster records (Drip Slime XP, Kobold, Wight, the Wolf stub, Dragon) and
  byte 27, requirement 42, values 27 / 69 / 111, Tirith's 199 and town 0's index, every starting-stock cell of 3.4,
  town byte 0, the tables the C64 lacks (HP dice 16–22, training curves, score weights, hit location, terrain offsets,
  the Air elemental's spells), the world's tile ids and place tiles and the plane maps (DOS's `WORLD.DAT` / `PLNS.DAT`).
- **Skill tables:** the C64's cells are Phantasie I's, which is D3-032's decided default (I's values; DOS's tables as
  read only as the `p3.skill-tables` option): the import lands on the same cells.
- **The C64's own text** (D3-077 for the Apple, D-084 / D2-050): item names (the Apple's abbreviations, `HALBERD`,
  `SWORD +3…+5` for 90–92), the dungeon texts, the scroll pages, `Drip Slimes`; the C64's own spell names (it has all 56).

**Open** (the user decides):

1. **The pre-generated party.** The manual promises six pre-generated, pre-equipped characters as the first Guild
   members (`docs/plan/09-trilogy-manual-p3.md`: not on the IBM version; the trilogy matrix lists it for III). The C64's
   `PCHARS0` has them (CHUMLEY, TRACKER, FINGERS, GRASSHOPPER, BONES, SAURIAN; v1.0 and v1.2 (alt) rolled differently).
   Import them into the Guild, and from which version? (Their 170-byte layout needs decoding first.)
2. **v1.2 (alt) support** (the same data files; other programs and starter party), or v1.0 only?

**Graphics pack (`c64`).** Not started: monster pictures (`MONSTERS` 0–4991), title, character sets, hero sprites,
endgame pictures.

**Not done here:** a disassembly of `PHANT.OBJ` / `TOWN.OBJ` / `DUNGEON.OBJ` (tile table, item → scroll page map,
giant rule, town byte 0, training), the plane-map layouts, `PCHARS0`'s field layout, a live check.
