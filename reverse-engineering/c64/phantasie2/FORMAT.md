# Phantasie II (Commodore 64, SSI 1986): disks, files and tables

Where each of II's game tables lives on the C64 disks, how it maps onto the Atari ST's tables (today's
`src/OpenPhantasie.Data/GameData/phantasie2/`, `../../atari-st/phantasie2/FORMAT.md`), and every place found so far
where the C64 differs from the ST (and whether the Apple II shares it, `../../apple2/phantasie2/FORMAT.md`,
`DIFFERENCES-II.md` D2-046…D2-050). First step of a C64 port (s216): data formats only, no importer code (section 7
is the plan). Same engine as C64 I (`../phantasie1/FORMAT.md`): BASIC compiled to the bytecode of
`../bytecode-notes.md`, the same file kinds and record layouts. Tags: `[C]` read in the code or confirmed on all
records against the ST's data, `[~]` probable, `[?]` not known. Offsets are into the file **after** its 2-byte PRG
load address; `P(n)` = byte n of a 170-byte character record.

Extract in the repo: `extracted-v1.2/` = C64PP v1.2 (never_used) sides 1 + 2 and v1.2 (alt1) side 1
(`tools/scripts/extract_c64_d64.py --out … *.g64`; `manifest.json` lists every file with its size, load address, type
and hashes). The v1.0 copies are not extracted in the repo; rerun the tool on their G64s. The comparisons below were
made with throw-away scripts that read these files and the ST's `unpacked/` files / GameData (the Apple's
`diff_a2_p2_vs_st.py` functions on C64-shaped input).

## 1. Disks and versions

Two disk sides (`s1` boot / town / wilderness, disk name `PHANTASIE`; `s2` dungeons and the utility, `PH2`), Commodore
DOS files only. Every G64 decodes to all 683 sectors (`tools/scripts/c64_g64.py`) except v1.0 plain side 1 (track 33
sector 5 unreadable, as SOURCES.txt says). Side 1's boot program `B` prints `COMMODORE 64 VERSION 1.x`, `COPYRIGHT
1986`, `BY W. P. KUBALSKI`, and "parts … copyrighted by … Drive Technology Ltd. 1983" (the fast loader) `[C]`.

| C64PP image | `B` says | State |
|---|---|---|
| **`phantasie_ii_s1/s2[ssi_1986](v1_2)(never_used)`** | 1.2 | **the reference**. Side 2 C64PP-verified, no `%DNG` save. Side 1: three files written by the game (2.1), their data equal to the template but `CHAR22` (two characters LOUIS, THUNDAR) |
| `(v1_2)(alt1)` (side 1 only) | 1.2 | **every mutable file at its mastered slot and equal to the template** (2.1): the cleanest side 1. Differs from never_used only in `CHAR22`, `TWN21`, `OUTT2` |
| `(v1_0)`, `(v1_0)(alt)(g2)` | 1.0 | played (full rosters, `%DNG` saves, every region / town rewritten). Plain v1.0's `SCROLLS` is short (11,684 B in 66 blocks: the chain through the unreadable sector) `[~]`; use (alt)(g2)'s (16,480 B) |
| held `../original-images/phantasie2/*.d64`, `v1.2-side-a-only/` | 1.2 | played (SOURCES.txt, s208) |

**Reference: v1.2.** Its new-game template `PATTERN2` (2.1) holds the shipped towns, regions and Guild, so the
side-1 copy's play state does not matter for the tables. v1.0 vs v1.2: section 4.1.

## 2. Files (v1.2)

Side 1 135 files, side 2 124. Both sides hold `MN` (**not identical**, 3.1), `MX 1`–`MX 80` (side 2's `MX 30`, `MX 37`,
`MX 62` differ from side 1's: pictures only `[~]`), `UTIL-3.O` and the nine 394-byte item pictures.

| File(s) | Type, bytes (with load address), load | Holds | Tag |
|---|---|---|---|
| `BOOT` (s1), `COPY` (s2) | PRG 95, `$02A7` | autostart stub (loads `B`) | `[~]` |
| `B` (s1) | PRG 2,400, `$0801` | boot BASIC: fast loader, title picture, `UTIL-3.O`, fonts, `SA = 39460`, then `TOWN2` | `[C]` |
| `B` (s2) | PRG 5,987, `$0801` | the utility (plain BASIC): PLAY GAME, transfer characters to / from the backup disk (`BAKFILE2`), START A NEW GAME, INITIALIZE DISK, TRANSFER CHARACTERS I → II / II → I | `[C]` |
| `FAST`, `UTIL-3.O`, `RTL-64` | as I's | fast loader, 6502 helpers (`SYS 49152 + n`: +33 save, +36 load), compiler runtime | `[C]` |
| `TOWN2` (s1), `OUTDOORS2` (s1), `DUNGEON2` (s2), `CEMETARY2` (s2), `WIN` (s2) | PRG 22,943 / 30,597 / 30,658 / 2,797 / 3,862, `$0801` | compiled programs: town, wilderness, dungeon, party wipe, ending | `[C]` |
| `MN` (s1 and s2) | **REL, record length 100**, 8,100 B = 81 records | monsters (3.1) | `[C]` |
| `MX 1`–`MX 80`, `TLUM1` (s2) | PRG 167–360 / 1,019, `$1FDA` | monster sprites (6); `TLUM1` = the ending's sprites (`WIN` loads it) | `[C]` layout, `[~]` TLUM1 |
| `ITEM` (s1) | SEQ 1,563 | item names (3.2) | `[C]` |
| `TINFO` (s1) | PRG 1,733, `$9920` | rule tables (3.3) | `[C]` |
| **`PATTERN2` (s2)** | PRG 19,566, `$2000` | **the new-game template**: Guild (`CHAR22`), the 10 towns, the 17 regions (2.1) | `[C]` |
| `TWN21`–`TWN210` (s1) | PRG 252 | towns (3.4) | `[C]` |
| `CHAR22` (s1) | PRG 4,252 | Guild roster + the 3 elementals, 25 × 170 B (3.5) | `[C]` |
| `OUTT1`–`OUTT31` (17 files, s1) | PRG 752 (760 when written by the game) | wilderness regions 1, 2, 3, 5, 6, 7, 8, 11, 12, 17, 18, 22, 26, 27, 28, 30, 31 (3.6) | `[C]` |
| `ERROR` (s1) | PRG 760, `$9448` | a game-written region file shipped by mistake: `OUTT3` with part of its fog lifted and feature record 5's arg / dest_x 6 / 16 (template 1 / 15). The same sha1 on all four side-1 copies; no program names it | `[C]` content, `[~]` nothing reads it |
| `SCROLLS` (s1) | **REL, record length 40**, 16,480 B = 412 records | the scrolls (3.9) | `[C]` |
| `SCROLL0` (s2) | PRG 786, `$2000` | Pluto's speech after he is beaten (`DUNGEON2` loads it to 52058): plain ASCII, 40-column rows | `[C]` |
| `DNG 1`–`DNG 8` (s2) | PRG 1,964, `$4000` | dungeon levels in the ST's layout (3.7) | `[C]` |
| `PMES 1`–`PMES 8` (s2) | PRG 1,873–3,513, `$1F1A` | dungeon messages in the ST's `MESSn` layout (3.8) | `[C]` |
| `%DNG n` / `%DNGSTS n` (s2, played copies) | PRG 1,257 `$8CA0` / 1,964 `$91B4` | the one-slot dungeon save (as I's) | `[~]` |
| `SHIELD`, `ARMOR`, `WEAPON`, `GOOD WEAPON`, `MG WEAPON`, `POTION`, `SCROLL`, `TREASURE`, `GOOD TREASURE`; s2 also `DOOR`, `TRAP`, `STAIRS`, `LOCK`, `EXIT` (386 B) | PRG 394, `$2200` | treasure / feature pictures (as I's) | `[~]` |
| `FONT`, `FONT+HER`, `HSPRIT` (s1) | PRG 2,050 / 4,333 / 1,923 | as I's (`B` loads `FONT` to 59392, `FONT+HER` to 8704; `TOWN2` `HSPRIT` to 61440) | `[C]` load |
| `PIC.MAP`, `PIC.SCR`, `PIC.CLR` (s1) | PRG 8,193 `$A000`, 1,025 `$8400`, 1,025 `$D800` | the title picture (multicolour bitmap, screen, colour RAM) | `[C]` |
| `PH2.END-4` (s2) | PRG 4,097, `$2000` | the ending picture (`WIN` loads it to 32768, copies 1,000 B to colour RAM and 1,023 / 2,047 B to 58368 / 59392) | `[~]` |
| `SKEL2`, `CSPRIT`, `FONTO` (s2) | PRG 514 / 1,027 / 1,970 | `CEMETARY2`'s skeleton, sprites, font | `[~]` |

### 2.1 The template `PATTERN2`, and files play rewrites `[C]`

Side 2's `B` START A NEW GAME (and both transfers, as on the ST) loads `PATTERN2` to 8192 and saves from it `CHAR22`
(4,250 B), then `TWN21`–`TWN210` (250 B each) and the 17 `OUTT` files (750 B each); it opens `PATTERN2` only to read
(`SYS …36` load, `OPEN 2,8,2,"PATTERN2,P,R"`; no save of it anywhere) and scratches `%DNG*`. Layout (offsets in the
file after its load address):

| Offset | Holds | Equals |
|---|---|---|
| 0–4249 | `CHAR22`: 25 records × 170 B (records 21–23 the elementals, 24 leftover `0xFF`s, the rest zero) | alt1's `CHAR22` |
| 4250–4259 | 10 zero bytes (v1.0: `0xFF`) | |
| 4260 + 252 (t − 1) | `TWN2t`, t = 1–10, 250 B + 2 | alt1's `TWN21`–`TWN210`; never_used's too (data bytes) |
| 6780 + 752 k | `OUTT` region k of the list above, 750 B + 2 | alt1's 17 `OUTT` files; never_used's too (data bytes) |
| 19500–19563 | 64 blanks | |

A mastered file loads at **`$4000` + its `PATTERN2` offset** (alt1: `CHAR22` `$4000`, `TWN21` `$50A4` … `TWN210`
`$5980`, `OUTT1` `$5A7C` … `OUTT31` `$897C`): the files were saved from a template loaded at `$4000`. A file the game
wrote loads at its buffer address, as in I: `TWN2n` `$81C3`, `OUTTn` `$9448` (and then 760 B: the game saves 758 data
bytes, the template 750 + 8 zero bytes), `CHAR22` `$898A` (`TOWN2` S)ave). never_used's `TWN21` (`$81C3`) and `OUTT2`
(`$9448`) have the template's data bytes: written back unchanged. SOURCES.txt's s208 reading ("alt1 rewrote OUTT2 to
752 B") is the other way round: 752 B at the slot is the mastered form, 760 B at `$9448` the game's.
**For an import, `PATTERN2` is the shipped state** of towns, regions and Guild, whatever side 1 holds.

**C64 I's `PATTERN`** (side 2, 21,707 B, `[?]` in `../phantasie1/FORMAT.md`) is read the same way by this utility's
II → I transfer (`OPEN 2,8,2,"PATTERN,P,R"`, then `CHAR2`, `TWN n`, `TWN 99`, `OUT n` saved from it) `[C]` here: it is
I's new-game template.

## 3. Tables

### 3.1 Monsters: `MN` `[C]`

I's REL layout (`OUTDOORS2` and `DUNGEON2` open `3,8,3,"MN"`): record m + 1 = monster m (0-based, the ST's order),
record 81 = `0xFF`; 25 CR-ended fields (name + 24 numbers; records 78 / 79 fill all 100 bytes and have no last CR).
Fields 0–12 and 15–23 are the ST's fields in the ST's order (I's table, `../phantasie1/FORMAT.md` 3.1), field 20 the
ST's `byte_34`; 13 = sprite blocks − 1 (all 80 equal `(MX size − 40) / 64` rounded up, − 1); 14 and 24 C64-only `[?]`
(as I's). **`OUTDOORS2` reads side 1's `MN`, `DUNGEON2` side 2's**, and the two files differ in two monsters.

Against the ST's 80 records (1,254 of 1,280 numbers equal; the names of 77 equal):

| Monster | Field | C64 | ST | Apple II |
|---|---|---|---|---|
| VIPER (6) | companions | 1, 1 | 0, 0 | = C64 (D2-047) |
| FIRE ELEMENTAL (53) | reaction modifier; treasure | −10; 0, 0 | 0; 0, 135 | = C64 (D2-047) |
| PLUTO'S WYVERN (74) | damage | 40 | 35 | = C64 (D2-047) |
| PLUTO'S GIANT WYVERN (76) / DRAGON-DEMON (77) | attack power | 350 / 800 | 300 / 400 | = C64 (D2-047) |
| CLOUD GIANT (68) | xp; companions | **44,464; 0, 4** | 44,614; 0, 0 | = ST |
| 17 monsters | group-size factor | **smaller**: RANGER 3 (ST 5), GOBLIN 2 (3), ILLUSIONIST 2 (3), WIGHT 2 (3), KILLER BEE 4 (6), WRAITH 2 (3), ASP 1 (2), OGRE 2 (3), LORD 3 (4), DEMON 2 (3), MASTER ILLUSIONIST 2 (3), HIGH RANGER 3 (5), HIGH DEMON 1 (2), SPIRIT 3 (4), GREAT TROLL 1 (2), WIZARD 3 (4), OVERLORD 3 (4) | | = ST |
| BABY DRAGON (30) | group-size factor | side 1 **3**, side 2 **2** | 4 | = ST |
| APPRENTICE DEVIL (12) | treasure | side 1 55, 24; side 2 55, **4** | 55, 24 | = ST |
| GIANT WASP (38) | group-size factor | 2 (v1.0: 1) | 2 | |

What the group-size factor does on the C64 (the ST: group size from it, `logic-specs`) is not read in `OUTDOORS2` /
`DUNGEON2` `[?]`; the C64 draws a group as up to 8 VIC sprites (6), a plausible reason for smaller groups `[?]`.
`byte_34` differs in 13 records (35 / 62 / 65 / 105 against the ST's 35–140): a picture-layout value on every version
(the Apple keeps a rank height there, D2-047's note) `[~]`. Names that differ: PLUTO'S GIANT CONSTR. (17, ST CONST., Apple
CONSTRICTOR), CREEPING MOLD (62, ST KILLER CRUD, = Apple), PLUTO'S DEMON FLY / SNAKE DRAGON / DRAGON DEMON (73, 75, 77:
no hyphen), PLUTO'S GIANT WYWERN (76).

### 3.2 Items: `ITEM` + `TINFO` `[C]`

`TOWN2` reads `OPEN 4,8,4,"ITEM,S,R": FOR I = 1 TO 180: INPUT#4, I$(I): NEXT: CLOSE 4: I$(0) = "NOTHING"` (bytecode at
`TOWN2` file offset `0x570`, PRG offsets). `ITEM` holds **181 CR-ended names with an empty line 46** (after `SM MACE`; v1.0 the same
blank, 180 lines, no `GOD RING`). Read with the empty line skipped, the 180 names are the ST's ids 1–180 in order; with
it counted, ids 46–180 would show the next item's name and `GOD RING` would never be read. v1.2 adding `GOD RING` as line
181 only makes sense if `INPUT#` skips the empty line (Commodore BASIC's `INPUT#` reads the next line when it gets an
empty one `[~]`; the compiler runtime's `INPUT#` is not traced): **skipped** `[~]`. Under that reading v1.0 has no name
for item 180. Names vs the ST: only id 53 `SH SWORD` (ST `SM SWORD`; the Apple's too) and v1.0's `GOLD BOK` (177).
Value factor V = `TINFO`[89 + id]: **all 180 equal the ST's** (and the Apple's).

### 3.3 Rule tables: `TINFO` `[C]`

1,731 B at `$9920`, byte-identical in v1.0 and v1.2: I's layout (`../phantasie1/FORMAT.md` 3.3), II's values. Against
the ST's `TWNS.INT` 0–2445: **1,541 of 1,545 cells equal**; the four that differ are the Apple's four (D2-049): HALFLING
DEX modifier **12** (ST 11; the manual's, D2-009), MONK spell slot 41 (SLEEP) **0** (ST 7; the manual's), MONK L10
skill slot 6 = 0 (ST 14), PRIEST L15 skill slot 8 = 0 (ST 4). Bytes 270–274 `6F FD CA 30 DA` (unread `[?]`; I's
`00 00 FF FF 00`). Training curves, travel and encounter tables are bytecode constants (not decoded `[?]`; `TOWN2` DATA
holds I's `7,9,3,15,12, 2,1,7,32,146,22,133,4050,4291,…,2861,-1` again, `OUTDOORS2` / `DUNGEON2` the 11 values
`17167 … 45830` again).

### 3.4 Towns: `TWN21`–`TWN210` (= `PATTERN2` 4260…) `[C]`

10 × 250 B, I's / the Apple's `TF2` record: 0–179 stock of ids 1–180, 180–224 restock pool, 225 = 0, 226 price index,
227 exit region, 228 exit y, 229 25 − exit x, 230–249 name padded with `0x15`. Town t = the ST's record t − 1
(PIPPACOTT … DARK ABYSS); the ST's unused STARVILLE / OLYMPIA are not on the C64 (nor the Apple). Against the ST:
**1,845 of 1,850 numbers equal**, names all equal:

| Town | Field | C64 | ST | Apple |
|---|---|---|---|---|
| IPSWICH (5), FERRON (6) | stock of SCROLL D (126) / SCROLL E (127) | 0 / 5 | 5 / 0 | = C64 (D2-048) |
| FERRON (6) | stock of id 115 (MAGIC 5) | **1** | 0 | = ST |
| DEMONIAC (9) | price index | 150 = ST | | 255 (D2-048) |

### 3.5 Characters: `CHAR22` and the 170-byte record `[C]`

I's record (`../phantasie1/FORMAT.md` 3.5). 25 records at `SA − 4250` (`SA` = 39460): **21 Guild slots** (0–20; the
ST has 22), the elementals at 21–23 (FIRE ELEM / WATER ELEM / EARTH ELEM, creation numbers 38–40), record 24 `0xFF`
filler. The template's elementals equal the ST's `elementals.json` in HP, attack / parry, level 20, class, race 16,
points (40 / 30 / 20, 100, 7 / 14 / 21) and known spells (slots 4–7, 20–31, 12–19), as in I; P(9) MP ceiling = 0 (ST
20), P(7) = 20, names with a space (`FIRE ELEM`, ST `FIRE_ELEM`).

### 3.6 Wilderness: `OUTT<n>` (= `PATTERN2` 6780…) `[C]`

750 B, the ST's `OUTn.DAT` bytes 0–749: grid 0–519 (cell (x, y) at `(25 − x) · 20 + y`, fog = tile + 120), features
520–594 (15 × `y, 25 − x, arg, dest x, dest y`), text pages from 595 in **screen codes** (`A` = 1), 3 pages + 35
bytes (the ST's text-page rule: arg < 10 one page, 10–19 two, 20+ three). The ST's pages 4–9 (leftovers such as
"[original text]") and its bytes 750–2999 (map picture) are not on the C64. The C64 orientation is the
ST's, not the Apple's quarter turn: the grid bytes, the OUT28 entrance table (v1.2) and every compass word of the scrolls
equal the ST's `[C]` data (the drawing code is not read `[~]`).

Against the ST (`PATTERN2` regions vs the ST GameData built from `MAPS.INT`): **fog equal in all 8,840 cells** (regions
1, 2 open, the rest fogged); tiles 8,814 of 8,840 equal; all 43 text cells' texts and every page 0–3 equal the ST's
(normalised); features equal but two junk records:

| Region | Cells (ST x, y) | C64 | ST | Apple | Reachable? |
|---|---|---|---|---|---|
| OUT3 | (5, 12…15) | 1 forest | 21 | = C64 (D2-049) | yes |
| OUT17 | (20, 6) | 76 | 72 | = C64 (D2-049) | |
| OUT31 | (17, 18), (17, 19) | 100 blocked | 85 lava | = C64 (D2-049) | |
| OUT5 | (17, 3), (18, 3), (19, 3) | **2** | 62 | = ST | in open water (101) next to a tile-2 cell; v1.0 = ST (a v1.2 change) |
| OUT26 / OUT27, OUT28, OUT30 | (0, 2), (1, 2) / (23, 2), (24, 2) | **101 water** | 100 blocked | = ST | **no** inside the region (only blocked neighbours); OUT26's pair lies on the west edge, so it could only be entered from the next region `[~]` |
| OUT28 | the 8 entrance cells around the castle (17…19, 12…14) | **52** | 50 | = ST | yes; tile class 5 (dungeon) either way, only the digit differs `[?]` (picture); v1.0's castle cell (18, 13) 52, v1.2 48 = ST |
| OUT1 | feature record at (25, 0) | none | dest_y 32 (junk) | none (D2-049) | unused |
| OUT5 | feature record 5 at (25, 10) | all 0 but y | none | | unused (no text / feature cell there) |

### 3.7 Dungeons: `DNG 1`–`DNG 8` `[C]`

1,962 B, the ST's `DNGn` (2,048 B) byte for byte in the level part (grid 0–1253 (33 × 38), level byte `0x4E7`, forced
table `0x4E8`–`0x4F1`, exit records `0x4F2 + 3k`, events `0x4F6 + 5k`, scripts from `0x5AA`, to ~1740); after it, `0x0D`
fill (the ST: `0x00` and I's `ENDING SEQUENCE` leftovers; DNG4's C64 tail holds other leftover bytes). Differences in
the level part (cell kinds: `../../dos/logic-specs/dungeon-representation.md`):

| Level | Offset | Cell / record | C64 | ST | Apple | Reachable? |
|---|---|---|---|---|---|---|
| DNG1 | `0x343` | (10, 25) | **15 wandering** | 0 wall | = ST | yes: west of it is a wandering cell (15) |
| DNG2 | `0xF7`–`0xF9` | (16…18, 7) | **142 message 24** "A LARGE STONE CLOSET" | 140 message 22 "THE CASTLE ENTRANCE" | = ST | yes (room label) |
| DNG2 | `0x34E` | (21, 25) | **184 message 66** (an empty slot in both files: shows nothing `[~]`) | 188 lock (bank 1, kind 4) | = ST | yes: between message cells |
| DNG3 | `0x205` | (22, 15) | **128 forced fight** (table entry 0: monster 34) | 0 wall | = ST | yes: east of it a wandering cell |
| DNG5 | `0x3CC`–`0x3CF` | (15…18, 29) | **144 message 26** "IMP LIVING AREA" | 140 message 22 "CAVERN WITH YELLOW TINT" | = ST | yes (room label) |
| DNG5 | `0x4D1` | (12, 37) | 17 wandering | 0 | = C64 (D2-049) | |
| DNG6 | `0x224` | (20, 16) | **130 forced fight** (entry 2: monster 56) | 146 message 28 "THE DINING ROOM" | = ST | yes |
| DNG8 | `0x447` | (6, 33) | 141 message 23 | 133 forced fight | = C64 (D2-049) | |
| DNG7 | `0x4F2`–`0x4FA` | exit records 0–2 (cells 215 / 216 / 217) | **OUT28 (16, 11), (20, 12), (18, 15)** | (20, 11), (19, 15), (16, 13) | = ST | yes: leaving the Black castle |
| DNG1 / DNG3 / DNG5 / DNG7 | `0x674` / `0x658` / `0x5C6`–`0x5C7` / `0x6A5` | script area | 16 / 239 / 152, 152 / `0x13` | 10 / 79 / 0, 0 / `0x1D` | = ST | `[?]` what each byte is |

DNG7's exits: on the C64 the north exits (row 0 and (0, 18), cell 215) land on the ring's north-west corner, the east
exit (32, 18) on its east side, the south exits (row 37) on its south side; the ST's land on the north-east corner, the
south side and the west side. All six target cells are mist / text cells of the ring around the castle `[C]` data.

### 3.8 Dungeon messages: `PMES 1`–`PMES 8` `[C]`

Plain ASCII in the ST's `MESSn` layout (slot table u16 LE at `2 (id − 1)`, records = prefix byte + 40 characters at
`0xE6 + word`); **the slot tables equal the ST's** for ids 1–113, so every message id a cell or event uses is the ST's.
The files end before the ST's junk records (ids above 110). Words (whole level text, ids 1–110 in order, normalised)
equal the ST's but: PMES1 `DETENTION` (ST `DETENSION`), "DROP GOLD AND WALK INTO THE SMOKE" (ST "DROP YOUR GOLD"),
"PLUTO'S ICE DRAGON, THE RETURN HERE" (ST THEN); PMES2 "A SLIT OF THE CEILING" (IN), "UNDER A TOWER" (THE); PMES3
`KEEPERS'S`; PMES4 "SMALL OAK CHEST" (no OAK), "THERE IS SMALL BUTTON" (IS A), `BEFORE` (BE FORE); PMES5 `DOOR` (DOOT),
`THESE` (THERE); PMES6 `IS` (IN), `TORTURE` (TORTURES), `BARREL` (BARBEL), id 107 shorter; PMES7 `AWESTRUCK`
(AVERSTRUCK), "THERE IS HUGE" (IS A), the menu label "FOLLOW HIM" (FOLLOW); PMES8 "DEVOUR OF HALF" (DEVOUR HALF),
`POSSESSED` (POSESSED), `COMPLETLY` / `CEVERN` (COMPLETELY / CAVERN), `DRAGONS` (DRAGON). Line breaks and centring
differ throughout (the C64 is mostly left-aligned); no difference changes a puzzle. The Apple's own wording (D2-050:
DNG1 msg 40, the DNG6 / DNG7 menus) is **not** on the C64: there it reads as the ST.

### 3.9 Scrolls: `SCROLLS` `[C]`

I's REL layout: records 1–4 = the index (21 values `5, 28, 51, …, 381, 404`: page k = records `idx[k] … idx[k+1] − 1`,
k = 1–20 = items 121–140), each page boxed in `+---+` / `! … !` rows in mixed-case PETSCII. Page 17 is empty (item
137, as on the ST and the Apple). The lettered scrolls take the ST's pages: A = 1–3, J = 12–13, K = 14–16, the others
one page each. Words equal the ST's but A `VENGENCE`, G "A MUCH SMALLER PALACE" (ST SUCH), K "AN ENOURMOUS **GREEN**
SNAKE" (ST BROWN; the Apple GREEN), N one `A` fewer. Records 404–411: a leftover page tail ("The other starts between the
Arena and the Jail Cell …", no scroll uses it) `[~]`. No spell names on either side (searched as in I).

### 3.10 Names and text in the programs `[C]`

`TOWN2` DATA: `AR, ER, FR, WT, GOD`, the town menu, classes, races (`"LIZ.MAN"`, UNDEAD), `DEAD, OKAY, SLEEP`.
`OUTDOORS2` DATA: terrain names `GRASS, FOREST, HILLS, MOUNTAINS, BUILDINGS, DUNGEON, PATHWAY, NETHERWORLD, LAVA, DESERT,
WATER, MIST, ASTRAL PLANE` (13, the Apple's list), treasure kinds, races (`ORGE`, `"LIZARD MAN"`). `WIN`: the Filmon
ending ("FILMON SPEAKS -", "PRESENTING THE HEROES OF FERRONRAH", the adjectives MAGNIFICENT … AVESOME … SUPERB). No
spell names (spells are cast by number, as in I and the Apple).

## 4. C64 vs the ST in short

- **Shared with the Apple II against the ST** (already decided for an Apple import, D2-047…D2-049: the ST's numbers):
  the five monster numbers, the four `TINFO` cells, the IPSWICH / FERRON scroll stock, OUT3 / OUT17 / OUT31's cells,
  DNG5 (12, 37), DNG8 (6, 33), the OUT1 junk feature. The C64 has none of the Apple's other differences (DEMONIAC's price,
  OUT1 (22, 0…1), OUT28's quarter-turned entrances in v1.2, the Apple's own message wording).
- **C64 only, numbers:** 17 smaller group-size factors, BABY DRAGON / APPRENTICE DEVIL different between the sides,
  CLOUD GIANT's xp and companions, FERRON's one MAGIC 5, OUT5's three cells, the unreachable water cells, the OUT28 ring
  digit, the DNG cells and DNG7's exits (3.7).
- **Text:** close to the ST's (unlike the Apple's); C64 names CREEPING MOLD (= Apple), CONSTR., WYWERN, no hyphens; item
  SH SWORD; scroll K GREEN (= Apple); a dozen PMES spellings.

### 4.1 v1.0 vs v1.2

Same `TINFO`, `UTIL-3.O`, fonts, pictures, `DNG 1`, `2`, `4`, `5`, `PMES 5`, `6`; programs differ in every file.
v1.0 against v1.2: `MN` GIANT WASP group 1 (both sides); `ITEM` `GOLD BOK`, no `GOD RING` (3.2); `PATTERN2`: OUT28's
eight entrance destinations are **the Apple's quarter-turned set** (D2-046), OUT28 (18, 13) 52, OUT5 (17…19, 3) = the
ST's 62, OUT6 / OUT26 / OUT30 text typos (`{1E}HERE`, `HOES`, a garbled `'83'`), OUT30 feature 0 x; `SCROLLS`
(alt)(g2): typos and **scroll L's route** "THE UNDEAD SERVANTS DOOR ON THE SOUTHEAST" (v1.2 = ST "THE MIDDLE DOOR ON THE
NORTH SIDE AND SNUCK PAST THE GUARD"); `DNG7` (5, 33) 235 (v1.2 233), `DNG8` (8, 36) / (10, 36) 133 / 141 swapped,
DNG3 / DNG6 fill bytes; `PMES` typos (CELLING, BEAST, FALLON, COMPLETLY; v1.0 PMES8 `CAVERN` = ST). v1.0 is an
earlier, rougher master; a v1.0 profile would need its own cell list.

## 5. Rules

Not re-read for II. The engine is I's (`../bytecode-notes.md`); II's `TOWN2` keeps I's training DATA. Where the ST
and the Apple differ in rules (restock 4 vs 9 items, D2-045) the C64 is not read yet `[?]`.

## 6. Graphics and sound

As C64 I (`../phantasie1/FORMAT.md` section 6): `MX n` = monster n − 1 (38-byte header + k 64-byte hardware sprites,
k = `MN` field 13 + 1; a group = up to 8 VIC sprites); `TLUM1` the ending's sprite set (15 blocks `[~]`); the title
(`PIC.*`), the item / feature pictures, `FONT`, `FONT+HER`, `HSPRIT`, `PH2.END-4`, `CEMETARY2`'s files: not decoded.
No sound files (SID code inside the programs `[?]`).

## 7. Importer plan: `p2-c64` (plan only, s216)

As the Apple import (D2-046…D2-050, `GameData/phantasie2/apple2-import.json`): **the C64's text, the ST's numbers**,
built through the ST builders so the GameData keeps the ST schema (the decisions below).

**Containers.** `.d64` / `.g64` as `p1-c64` (the G64 decoder and D64 / REL reader are shared).

**Detection.** Side 1: `TOWN2` + `OUTDOORS2` + `TINFO` + `MN` + `ITEM`; side 2: `DUNGEON2` + `PATTERN2` + `DNG 1`.
v1.2 key sha1s: `TOWN2` `736ab7bb…`, `OUTDOORS2` `9462ad6a…`, `DUNGEON2` `d45eef51…`, `MN` `1038e1a3…` (s1) /
`dd4951ab…` (s2), `ITEM` `f1933fff…`, `TINFO` `46767bce…`, `SCROLLS` `605b521f…`, `PATTERN2` `f71dd34e…`, `DNG n`,
`PMES n` (full hashes in `extracted-v1.2/manifest.json`); `B`'s `VERSION 1.2` for the report.

**Mutable files.** `CHAR22`, `TWN2n`, `OUTTn`, `%DNG*`. None needs to be read: towns, regions and the elementals come
from `PATTERN2`; played = loads at `$81C3` / `$9448` / `$898A` (a warning only, the import is the same).

**What maps where:**

| GameData file | From the C64 | Cells that take the ST's values |
|---|---|---|
| `monsters.json` | `MN` (either side) | the D2-047 five; the C64-only numbers; `sprite_*` / `raw_hex` / `byte_34` from the ST |
| `items.json` | `ITEM` (the blank line skipped), `TINFO` values (= ST) | — |
| `names.json` | `TOWN2` / `OUTDOORS2` DATA, `ITEM`, `MN`, town names | spell names: the manual's list (D2-050's rule) |
| `creation-tables.json`, `rules.json` | `TINFO` (= ST but D2-049's four cells) | as the Apple import |
| `towns.json` | `PATTERN2` towns | D2-048's four stock cells; FERRON MAGIC 5 |
| `elementals.json` | `PATTERN2` records 21–23 | MP ceiling 20 (as I's C64 plan) |
| `overworld.json` | `PATTERN2` regions (grid, fog, features, pages in screen codes) | D2-049's cells; the C64-only cells |
| `dungeons.json`, `messages.json` | `DNG n` + `PMES n` (the ST's layout, no cipher) | D2-049's two cells; the C64-only cells; DNG7's exits (open 1) |
| `scrolls.json` | `SCROLLS` (frame stripped; the ST's 14 lettered scrolls, A = pages 1–3, J = 12–13, K = 14–16) | — (C64 orientation = ST: no compass swap) |
| strings | `SCROLL0` (Pluto), `WIN` (Filmon ending), program PRINT text as `p2-c64` alternatives | lines the C64 lacks: the Apple import's fallback rule |

**Applied by default** (already decided; the importer follows these unless the user says otherwise):

- **The ST's numbers** for every cell the Apple shares (D2-046…D2-049 apply "for every source copy"): the five monster
  numbers (D2-047), the four `TINFO` cells (D2-049; HALFLING DEX / MONK SLEEP are the manual's, D2-009), the IPSWICH /
  FERRON scroll stock (D2-048), OUT3 / OUT17 / OUT31's cells and DNG5 (12, 37), DNG8 (6, 33) (D2-049).
- **The ST's numbers** for the C64-only ones too (DOS / the reference wins on numbers, AGENTS.md; C64 I's BLACK KNIGHT
  and D-086's reachable cells went the same way): the 17 group-size factors, CLOUD GIANT, both `MN` files' BABY DRAGON /
  APPRENTICE DEVIL (so which side's `MN` is read does not matter), FERRON's MAGIC 5, OUT5 (17…19, 3), the water pairs,
  the OUT28 ring digit, the DNG1 / DNG2 / DNG3 / DNG5 / DNG6 cells and the four script bytes of 3.7.
- **The C64's own text** (D-084 / D2-050 / D3-077: a port import keeps its wording): monster names (CREEPING MOLD,
  CONSTR., WYWERN, no hyphens), item 53 SH SWORD, scroll K's GREEN snake, the PMES spellings, `SCROLL0`, `WIN`; spell
  names from the manual's list (D2-050). No compass swap is needed (the C64 is ST-oriented).

**Open** (new evidence; the user decides):

1. **DNG7's exits** (3.7). The C64's land on the side of the Black castle you leave by; the ST's (and the Apple's, the
   same bytes) do not on the ST-oriented map: they may have been made for the Apple's quarter-turned map (D2-046) `[~]`,
   which the C64 v1.2 corrected along with its OUT28 entrances (v1.0 still had the Apple's entrance set, 4.1). The
   ST's exits as everywhere else, or the C64's?
2. **v1.0 support.** A second profile would need its own cells; its scroll L gives another route into Pluto's castle
   ("THE UNDEAD SERVANTS DOOR ON THE SOUTHEAST" vs v1.2 / ST "THE MIDDLE DOOR ON THE NORTH SIDE AND SNUCK PAST THE
   GUARD": which one fits the map is not checked), its OUT28 entrances are the Apple's quarter-turned set, and it has no
   name for item 180. Plain v1.0's `SCROLLS` is short (unreadable sector): (alt)(g2) only. Support v1.0, or v1.2 only?

**Graphics pack (`c64`).** As I's plan: `MX` sprites (80 sets + `TLUM1`), title, item / feature pictures, font; map
tiles, hero sprites, `PH2.END-4` not decoded.

**Not done here:** the bytecode rules (group size, restock, training), the C64-only `MN` fields 14 / 24, a live check.
