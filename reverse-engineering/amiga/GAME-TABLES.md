# Phantasie I Amiga: the game tables (s201, static) `[C]`

Where the Amiga v1.0 disk keeps every table DOS I's importer reads, and how each maps onto DOS I's layout. Built
into the importer as `P1AmigaTables` (`src/OpenPhantasie.Importer/Pipelines/P1Amiga/`): an Amiga-only import writes
the same eleven GameData files as `P1DosPipeline` (10-importer.md 7.8). Found by reading the files against DOS I's
(`reverse-engineering/dos/original-files/`) and the `game` hunks; every mapping below was checked on all records of
the reference dump (`P1AmigaTablesTests`). Hunk numbers as in `FORMAT.md` (`hN +off` = offset in hunk N's data;
AmigaDOS hunk order, BSS hunks counted). All Amiga numbers are big-endian; DOS's little-endian.

## 1. `game`: monsters, names, item values

| Table | Amiga | DOS | Mapping |
|---|---|---|---|
| Monsters (80) | h3 `+0x14`, 80 × `0x3a` | DS:`0x1a6c`, 80 × `0x39` | below |
| Sprite sizes (80 × w, h) | h3 `+0x1236` (right after the table + 2) | DS:`0x2c3e` | u16 pairs |
| Item names (181) | h12 `+0x7aa`, 181 × u32 pointers into h12 (HUNK_RELOC32 to h12) | DS:`0x0ec0`, u16 | C strings |
| Classes (6) | h12 `+0xaaa` | DS:`0x167e` | |
| Races | h12 `+0xb2a`, 16 pointers (the 16th is UNDEAD) | DS:`0x16b2`, 15 read | first 15 |
| Spells (60) | h12 `+0xe18`; slots 54-56, 58, 59 empty, as DOS | DS:`0x174d` | 0..53 + 57 |
| Item values V (182) | h12 `+0x63e`, u16 (the last 0, unused) | DS:`0x0d54` | |

Monster record, Amiga → DOS offset: the name `+0..+0x13` and fields `+0x14` armor, `+0x16` damage, `+0x18` level,
`+0x1a` xp (u32), `+0x1e` special chance, `+0x20..+0x22` the three specials (bytes) sit at the same offsets; the Amiga
then has a pad byte `+0x23` (0 in all 80) and every later field one byte on: hp_base `+0x24` (DOS `+0x23`), combat
rating `+0x26` (`+0x25`), companions `+0x28/+0x29` (bytes), reaction `+0x2a`, next group `+0x2c`, defense `+0x2e`,
gold `+0x30` (u32), group size / byte_34 / two treasure bytes `+0x34..+0x37`, attack power `+0x38`. Converted to the
DOS layout, all 80 records equal DOS's byte for byte (so does `raw_hex`).

The names are found through the pointer tables, as DOS's are (every entry is a relocated pointer: checked).

## 2. `twns.int` (13,000 B; DOS `TWNS.INT` 12,987 B)

- Bytes 0..2445: the creation tables (class (a) on our side, shipped; not read). They differ from DOS's by word
  alignment `[~]` (not mapped: not needed).
- **Towns** at 2446, 12 × 292 (DOS 2445, 12 × 291): stock 181 bytes, a pad byte (0), 45 restock words, the price
  index, a word (DOS `+0x111`), two words that are DOS's `+0x113 / +0x115` in the map's transposition (Amiga = DOS
  `y`, `25 − x`; the town's cell, read from the data, not from code `[~]`), the 12-byte name at `+280` (DOS `+279`).
  Rebuilt in the DOS layout, all 12 records equal DOS's 291 bytes (`dos_record`).
- Roster placeholder 22 × 282 zero bytes from 5950, then the **three elementals** at 12154 (DOS 12141), 282-byte
  character records: 135 words then the 12-byte name at `+270`. DOS's two byte fields `+216 / +220` are the low bytes
  of words; word-swapped, the records equal DOS's except the first name (section 6).
- `town_labels` (names.json): DOS's scan of `TWNS.INT` finds the 12 town and 3 elemental names; the Amiga's words
  before a name are not 0, so the scan would miss them: the import lists the same 15 name fields directly.

## 3. The overworld: `maps.int` + `out*.dat`

- `maps.int` (9,360 B = 18 × 520, a key file, never written by the game): the 18 regions' grids in region order
  (1..16, 18, 19), fog included, cell (x, y) at `(25 − x)·20 + y` (the layout `h56 +0x214` reads in `OUT*.DAT`). They
  equal the unplayed `out*.dat` grids and DOS's pristine `OUTn.DAT` grids except the cells of section 5.
- `out*.dat` (2,500 B): grid 0..519 (rewritten by play: fog, spent fights, 3.3 of 10-importer.md), the 15 feature
  records at 520 (5 bytes: Amiga `y`, `25 − x` (byte arithmetic: OUT3's unused record 12 is DOS x 68 = Amiga 213),
  arg, dest x, dest y, the last three as DOS), the 40-byte text pages at 595 up to 1000 (DOS's bytes, identical), the
  polygon records from 1000 (`FORMAT.md`, Overworld). Features and pages are the same in the played and the unplayed
  files, so the import takes the grids from `maps.int` and only those two parts from `out*.dat`: a played copy
  imports the same as a fresh one.
- DOS's bytes 1000..1023 are leftovers (old text); the Amiga has its polygons there, so `embedded_text` (a debug
  list, unused) loses those runs.

## 4. Dungeons, messages, scrolls

- `dng1..dng10`: DOS's `DNG1..10` byte for byte except one cell (section 5).
- `mess1..mess10`: **plain text**, the bytes DOS's `MESSn` decrypt to (same slot table, same 41-byte records), except
  two characters (section 6).
- `scrolls.dtx` (16,000 B): **plain text**, 20 records of 20 × 40 characters, NUL-padded: DOS's lines upper-cased,
  with the same line breaks (DOS's are mixed case, ciphered).
- Zeus's speech (`core.endgame.zeus_1..7`): h33 `+0x2` (file 50826), seven C strings in upper case with the
  Amiga's own line breaks (`FORMAT.md`, the victory). The import takes them as the Amiga's own wording
  (`tools/scripts/p1_amiga_own_wording.json`, `strings/en.own.p1-amiga-1.0.json`).

## 5. Amiga vs DOS: numbers

DOS wins over Amiga on numbers (AGENTS.md); an Amiga-only import gets DOS's value in each cell from
`GameData/phantasie1/amiga-import.json` (class a). Everything else of the eleven tables has DOS's numbers.

| Cell | Amiga | DOS | Note |
|---|---|---|---|
| OUT2 (11, 8) | 100 (lake) | 34 | user s53: DOS terrain (`FORMAT.md` "vs DOS") |
| OUT12 (11, 14) | 100 (lake) | 16 | user s53 |
| OUT9 (1, 18), (15, 19), (20, 9) | 68 | 67 | fixed fights: spent (x8) on the Amiga as mastered, live (x7) on DOS |
| OUT16 (4, 15) | 88 | 87 | the same |
| DNG6 byte 1169 = cell (14, 35) | `0xd6` | `0xc7` | a jammed lock, bank 2 vs bank 1 (`dungeon-representation.md` Locks) |
| monster 79 sprite_height | 2570 | 22356 | the size table's last word; no sprite height in either `[?]` |

## 6. Amiga vs DOS: text (the Amiga's text is used, except the two typos fixed by `name_fixes`, user s202)

| Where | Amiga | DOS |
|---|---|---|
| items 101-109 (names.json `spell_level_names` 0-8) | `HEAL POT1`..`HEAL POT9` (110 `HEALING10` as DOS) | `HEALING 1`..`9` (DIFFERENCES D-082) |
| item 139 | `SROLL 19` | `SCROLL 19` (D-082: the import fixes it to DOS's) |
| elemental 0 (and its town label) | `FIRE  ELE` | `FIRE ELEM` (D-083: the import fixes it to DOS's) |
| MESS1 record of the chest line (DNG1 message) | no full stop after "HERE" | full stop |
| MESS5 record of the BLANK TOWN line (DNG5 message) | a blank before " x" | an apostrophe |
| scrolls 1-20 | upper case | mixed case (so are the scroll quotes of item descriptions 121-140) |
| Zeus's speech | upper case, its own breaks | DOS's 40-column lines |

Names of classes, races, spells, monsters, towns and every other item are the same.
