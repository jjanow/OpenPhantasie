# Phantasie III (Commodore 64, SSI 1987): disks, files and tables

Where III's game tables live on the C64 disks and how they compare with DOS III (the reference,
`../../dos/phantasie3/FORMAT.md`, `extracted/`) and the Apple II (`../../apple2/phantasie3/FORMAT.md`). Data formats
s216; the importer and the character record s218 (section 7). Tags: `[C]` confirmed in the bytes on
every record against DOS's / the Apple's data, `[~]` probable, `[?]` not known. Offsets are into the file **after** its
2-byte PRG load address.

**Not I / II's engine.** C64 III is 6502 machine code (`PHANT.OBJ`, `TOWN.OBJ`, `DUNGEON.OBJ`), not compiled BASIC:
`../bytecode-notes.md` does not apply. Its data is a **hybrid**: DOS III's binary records (monsters, dungeons,
armory rows, the Scandor map, the item / spell tables) with the Apple II's upper-case text (item names, dungeon texts,
scroll pages) and the Apple's corrections of a few DOS data faults.

Extract in the repo: `extracted-v1.0/` = C64PP v1.0 (!) sides 1 + 2 (`tools/scripts/extract_c64_d64.py --out …`;
`manifest.json` lists every file with size, load address and hashes). v1.2 (alt) is not extracted in the repo (the importer and its tests read both C64PP `.g64` pairs).
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
| `PCHARS0` | s2, PRG 3,063, `$A300` | the Guild roster: 18 × 170 B; slots 0–5 the manual's **pre-generated party** (CHUMLEY, TRACKER, FINGERS, GRASSHOPPER, BONES, SAURIAN, level 1) (3.5) | `[C]` |
| `DNGIP1` | s2, PRG 5, `$687F` | dungeon-in-progress flag (`DUNGEON.OBJ` names `DNGIP` / `DNGIP1`) | `[~]` |
| `B` | s2, PRG 3,686, `$0801` | the utility (plain BASIC): 1 play the game, 2 make a dungeon disk, 3 / 4 transfer Phantasie I / II characters to III format; loads `UTILITIES.OBJ`, `P3COPY.OBJ` | `[C]` |
| `UTILITIES.OBJ`, `P3COPY.OBJ` | s2, PRG 313 `$C100` / 1,841 `$C800` | the utility's machine code (disk copy, transfer) | `[~]` |
| `TITLE`, `PSET0`, `PTOWN1`, `HEROSPRITES`, `GUT`, `TOT`, `SCHLECHT`, `SND` | s1 / s2 | title picture (10,003 B `$6000`, Koala), character sets (2,050 B: PSET0 the map's, PTOWN1 the town's), hero sprites (3,458 B: 18 sets of 3 blocks), the endgame pictures (4,098 B `$8000`: charset + screen + colour; German file names: GUT good = Zeus, TOT dead = the defeat's demon, SCHLECHT bad = Pluto), a sound table (177 B `$126E`) | `[C]` pictures (`GRAPHICS.md`), `[~]` SND |

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
Byte 27 is the fight's VIC sprite pointer (`GRAPHICS.md` 2). The giant pictures are `$A8` and `$BA`–`$CD`
(PHANT.OBJ `$2836`): when group 1 shows one, the field switches to the giant layout and group 2's picture is cleared, so
no second group is drawn `[C]`; whether the fight logic also drops the group / caps the count at 2 as DOS does is not read `[?]`.

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

### 3.5 Characters: 170-byte records `[C]` (s218)

`ELEMENTALS` (3 records) and `PCHARS0` (18 records + 1 byte; runtime `$A300`, record x at `$A300 + 170 x`, pointer in
zero page `$37`/`$38`, PHANT.OBJ `$44AA`) use a 170-byte record with the name at 131: the Apple III's size, but **not**
C64 I's field layout (class, race, skills and items sit elsewhere). Read from **TOWN.OBJ's creation code** (`$4055`–`$42E4`,
the fields it writes) and **PHANT.OBJ's character sheet** (`$5516`–`$5800` runtime `$4516`…; PHANT.OBJ runs at file
address − `$1000`, TOWN.OBJ where it loads), checked on the 6 + 6 starter records and the elementals. `P(n)` = byte n.

| `P(n)` | Field | Source | Tag |
|---|---|---|---|
| 0 | 2 on every used record (creation writes 2 after clearing all 170) | creation | `[?]` |
| 1–5, 6 | STR INT DEX CON CHA; LCK (= rnd(6) + 7) | creation (race / class tables `$90D1` / `$90EF`), sheet | `[C]` |
| 7 | defence rating, set at fight start (2 DEX + parry / 4 + shield points / 2 − armour term); elementals 200 / 100 / 25 | PHANT `$3D70` | `[~]` |
| 9, 10, 11 | magic ceiling, magic max, current | creation, sheet | `[C]` |
| 12, 13 | HP max, current | creation, sheet | `[C]` |
| 14, 15 | gold in the bank, little-endian (creation: P15 = social class → 256 per class) | sheet (`$56EF`), creation | `[C]` |
| 16, 17 | **age clock**, little-endian, 1/26 year (sheet prints it / 26 on AGE; creation (rnd(7) + 24) · P166 + 260; aging adds to it, TOWN `$1D4A`) | sheet `$56B0`, creation `$421A` | `[C]` |
| 18 | status 0 DEAD, 1 OKAY, 2 DOWN, 3 SLEP, 4 OUT (PHANT 1343) | sheet | `[C]` |
| 19, 56, 57, 58, 123, 125 | fight-start copies (level; armour pts; level + weapon pts; attack; 2 · bow pts + 4; fire bow) | PHANT `$3D27` | `[~]` |
| 20, 22, …, 36 | the nine skills: ATTACK, PARRY, FIND ITEM, SPOT TRAP, DISARM TRAP, LISTEN, **FIRE BOW** (32), PICK LOCK, SWIM | sheet (`$53CA` from 20 step 2), creation | `[C]` |
| 21, 23, …, 37 | skill bonuses, recomputed on every sheet view: 2 · attr + 30 (STR, DEX, INT, INT, DEX, INT, –, DEX, CON); 33 = Fire Bow's 2 L + 4 DEX + STR + 10 (+15 / +30 by class / race `[~]`) | PHANT `$5303` | `[C]` |
| 38, 39 | combat work bytes | PHANT | `[?]` |
| 40 | origin (PHANT's `$0953` table: 0 GELNOR 1 FERRONRAH 2 NETHERWORLD 3 SCANDOR 4 LIGHT 5 DARK = III's numbers); creation: 3, or by town 15 / 16 / 17 | sheet, creation `$42E5` | `[C]` |
| 43, 44, 45 | cleared at creation and by raise dead; written by the dungeon: the body areas `[~]` | creation, PHANT `$4D0F` | `[~]` |
| 47, 48, 49, 50 | worn bow, shield, armour, weapon (kit: 41 or 44 for a Thief, 4, 22, 64) | creation | `[C]` |
| 51, 52, 53 | experience, 24 bits little-endian | sheet | `[C]` |
| 54 | level | sheet, creation | `[C]` |
| 59 | creation number (the starters 1–6) | creation | `[~]` |
| 61–120 | spell n at 60 + n: 0, 1 known, 2 learnable (creation: Heal 1 for Ranger / Priest, Fireflash 1 for Monk / Wizard) | creation | `[C]` |
| 121 | bow points (item points of P47) | creation | `[~]` |
| 122 | score | sheet | `[C]` |
| 126 | flags; bit 2 = carries the Wand of Nikademus (the sheet lists it) | sheet `$579F` | `[C]` bit 2 |
| 128 | social class 0–3 (rnd(100), rnd(75) for races ≥ 5: < 30 / 60 / 90); the sheet's names 4 SUPER HERO, 5 DARK LORD | creation, sheet | `[C]` |
| 129, 130 | 21 at creation | creation | `[?]` |
| 131–150 | name | sheet, creation | `[C]` |
| 152–160 | nine carried items (the kit: 4, 64, 22, bow) | sheet `$5805`, creation | `[C]` |
| 161 | race, **Phantasie I's / Core's numbering** (PHANT's `$0852` table: HUMAN DWARF ELF HALFLING GNOME PIXIE SPRITE OGRE GNOLL TROLL KOBOLD ORC GOBLIN LIZARD MINOTAUR UNDEAD ELEMENTAL) | sheet, creation | `[C]` |
| 162, 163, 164 | weapon, shield, armour points (creation writes weapon then armour points into 162: 164 stays 0 `[~]`) | creation, fight start | `[~]` |
| 165 | town, 0-based (0 = Pendragon) | creation | `[C]` |
| 166 | maximum age ⌊LCK × race factor⌋ (DOS's +0x2a; never read) | creation `$41CD` | `[C]` |
| 169 | class, Core's order (THIEF MONK RANGER FIGHTER PRIEST WIZARD, PHANT 1235) | sheet, creation | `[C]` |
| 8, 41, 42, 46, 55, 60, 127, 151, 167, 168 | | | `[?]` |

**The starter party** (`PCHARS0` slots 0–5, the manual's six; slots 6–17 empty): CHUMLEY Troll Fighter, TRACKER Human
Ranger, FINGERS Sprite Thief, GRASSHOPPER Elf Monk, BONES Gnome Priest, SAURIAN Elf Wizard, level 1, in Pendragon,
origin Scandor, the class kit worn. v1.0 and v1.2 (alt) hold the same six with other rolls (attributes, HP, skills,
social class, gold, age): v1.2's ages fit the creation formula; **v1.0's do not** (6635 .. 8165 = 255 .. 314 years on
the sheet; P16 + P17 = 260 on every v1.0 record `[?]`): DIFFERENCES-III D3-078 (user s218: a v1.0 import gives the six
Guild New's start age; v1.2's stored ages are kept). The held cracked pair's `PCHARS0` is
played (other bytes in slots 0–5). `ELEMENTALS`: AIR, MIST, MUD ELEMENTAL (backspaces / `=` in the name field),
level 20, race 16, class FIGHTER (3).

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

Mostly not read (s218 read only the character record's code, 3.5: creation in TOWN.OBJ, the sheet in PHANT.OBJ, with a
throw-away capstone 6502 disassembly; PHANT.OBJ runs at file address − `$1000`, parts of TOWN.OBJ at + `$4000`). Candidates: the giant rule's fight side (3.1: the picture side is read), the town-row byte 0, the
training curves (no DOS-shaped table found).

## 6. Graphics and sound

Pictures: `GRAPHICS.md` (s220; `TITLE`, `MONSTERS` 0–4991, `HEROSPRITES`, `PSET0` / `PTOWN1`, `DATA`'s tile tables,
`GUT` / `TOT` / `SCHLECHT`, DUNGEON.OBJ's glyphs; the `c64` art pack). Not decoded: `SND` (177 B, a sound table `[~]`).

## 7. Importer: `p3-c64` / `p3-c64-1.2` (built s218)

The rule of the Apple and Amiga imports (`../../apple2/phantasie3/FORMAT.md` section 4): **DOS's values where the C64
differs or has none, the C64's own text**, into DOS III's GameData shapes. Code: `src/OpenPhantasie.Importer/Pipelines/P3C64/`
(`P3C64Tables`, `P3C64StarterGuild`, `P3C64Pipeline`); write-up `docs/plan/10-importer.md` 7.13.

**Containers.** `.d64` / `.g64` through the shared `D64Volume` / `G64Image`. Side 1 + side 2 of the one disk.

**Detection.** Two profiles (as II's Apple v1.0 / v2.1): `p3-c64` (v1.0) and `p3-c64-1.2` (v1.2 (alt)). Key files side 1
`PHANT.OBJ`, `MONSTERS`, `DATA`; side 2 `TOWN.OBJ`, `DUNGEON.OBJ`, `MATERIALMAP`, `DNG1` (each build's sha1s; v1.2 has its
own `PHANT.OBJ` / `TOWN.OBJ`). `B` is not a key file: the held v1.1 pair (v1.0's programs and roster, v1.2's `B`) and the
held cracked pair (v1.0's programs, played) import as v1.0. Mutable files: `PCHARS0` (per build), `TOWN A`–`Q`, `DNGIP1`.
A played copy imports its towns' stock as played and no starter party (both warned; D3-079).

**Built:**

| GameData file | From the C64 | Cells that take DOS's values (`phantasie3/c64-import.json`, 265) |
|---|---|---|
| `monsters.json` | `MONSTERS` + 4992 (`P3Extract.MonstersFrom`) | byte 27 (80), the five Apple-shared records (12) |
| `items.json` | `DATA` names, `PHANT.OBJ` points, `TOWN.OBJ` requirement / value / price index | requirement 42, values 27 / 69 / 111 |
| `spells.json`, `names.json` | `PHANT.OBJ` (56 spell names, races, classes, social classes, status, origins, towns, the sheet's skill words, the 11 dungeon descriptions mapped by meaning `[~]`) | Random race, body states (5) |
| `rule-tables.json` | `TOWN.OBJ` HP dice, ceilings, skill tables (levels 1–15 of 16 rows), spell rows; `PHANT.OBJ` area levels (v1.0 14888, v1.2 14890) | HP dice L16–22, the skill cells as DOS reads them + D3-032's fixes (the C64 has I's cells: same result), training curves, score weights, hit location, terrain offsets, dungeon entrances, the Air elemental's spells |
| `towns.json` | `TOWN A`–`Q`, price index | the stock cells of 3.4, Tirith's index |
| `dungeons.json` | `DNG0`–`DNG9` by DOS's letters (the Apple's text path: CR-ended, window font) | — (bytes to `0x593` equal DOS's) |
| `overworld.json` | `MATERIALMAP` (the C64's tile ids) | DOS's Scandor rows (75), the plane maps, places, start, terrain |
| `scrolls.json` | `SCROLL A`–`T` (screen codes; `F` = Scroll IV's map is no text record) | item → page map of items 123, 124, 130 |
| `elementals.json` | (P3Build's constants) | Air's spells |
| `starter-guild.json` (import only) | `PCHARS0` slots 0–5 of a fresh roster (3.5) | — |

Generator: `tools/scripts/build_c64_p3_import.py` (from the importer's raw build, `P3C64_DUMP`; reuses
`diff_a2_p3_vs_dos.py`'s rules). v1.2 gives v1.0's tables and takes the same cells without a warning. The starter
party keeps the record's values but v1.0's age clocks (D3-078: Guild New's start age at new game; v1.2's kept).

**Strings:** `p3-c64` and `p3-c64-1.2` alternatives for 16 ids found verbatim in each build's programs (status / class
words; key `<file>:low7` = bit 7 cleared), and (s221) 119 ids per build in the C64's own wording
(`tools/scripts/p3_c64_own_wording.json`, keyed per build; generator `working/gen_own3_c64.py`, which quotes the lines
and locates each one in each build). How the programs hold text `[C]`: PHANT.OBJ and TOWN.OBJ print inline strings
(`JSR $0D46` in PHANT.OBJ, then the text; bit 7 on the last character; a `$0D` inside is a row break; spaces in a row
are where a number is printed over); DUNGEON.OBJ's event lines are `$0D`-ended and reached by pointer, its dungeon
window lines use the Apple III's window font (`=` blank, `<` comma, `>` question mark) and its menu row another (`` ` ``
blank, `i` = `)`), and the Astral Plane / reward texts are 40-column rows padded with spaces. v1.0 and v1.2 (alt)
give the same text but one line (v1.2's combat rank prompt `WHAT RANK?`, v1.0 `WHICH RANK?`); TOWN.OBJ and
DUNGEON.OBJ hold it at the same offsets, PHANT.OBJ's v1.2 is shifted. The held v1.1 and cracked pairs carry v1.0's
three programs (same sha1). Mapped: the monsters' answers, surprise / night watch, combat lines, damage / death /
swimming / lava, the dungeon's lock / trap / event lines, the Mystic, the Guild / Armory / Trade / Inn / learning
prompts and refusals, the Astral Plane and its verdicts, the reward speech; the rest keeps the Apple III's
`fallback.p3-apple2.en.json`. The C64's spellings are kept (`YOU HERE A NOISE`, `BREATHS`, `ATTENDENTS`).

**Decided (user s218):** D3-078 (above) and D3-079 (precedence DOS > Amiga > Apple > C64, played copies, dungeon names by
content, the mixed text) as built.

**Art (s220):** the `c64` pack, recipe 2 (`GRAPHICS.md`).

**Open:** the tile → terrain table, the plane-map layouts, the places in code, the item →
scroll page map, the giant rule's fight side, town byte 0, the dungeon → file and entrance code, the transfer utility
`[?]`; own-wording lines placed by context only `[~]` (10-importer.md 7.13).
