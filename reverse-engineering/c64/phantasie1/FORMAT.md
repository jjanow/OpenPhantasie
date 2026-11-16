# Phantasie I (Commodore 64, SSI 1985): disks, files and tables

Where each of I's game tables lives on the English C64 disks, how it maps onto DOS I's tables (and so onto
`src/OpenPhantasie.Data/GameData/phantasie1/`), and every place found so far where the C64 differs from DOS I. Formats
s214; the `p1-c64` importer that reads them is built (s216, section 7). Tags: `[C]` read in the
code or confirmed on all records against DOS's data, `[~]` probable (data pattern, no code read), `[?]` not known.
Offsets are into the file **after** its 2-byte PRG load address unless "file offset" is said; `P(n)` = the
program's `FN P(X) = PEEK(SA + 170 · PM + X)` (`../bytecode-notes.md`).

Tools (`python3 -I`, from the repo root):

| Script | Does |
|---|---|
| `tools/scripts/c64_g64.py IN.g64 OUT.d64 [--report]` | G64 (GCR-1541) → 683-sector D64: every sync, header block `0x08` + data block `0x07`, both checksums; copy protection ignored. Library `decode(data)` |
| `tools/scripts/extract_c64_d64.py --out DIR IMAGE.d64\|.g64 ...` | every directory file → `DIR/<image stem>/<NAME>.<type>` + `DIR/manifest.json` (size, load / end address, REL record length, sha1, sha256). No arguments = the German D64s into `../extracted/` (unchanged) |

Extract in the repo: `extracted-v1.4/` = C64PP v1.4 sides 1 + 2 (`manifest.json` lists every file with its size,
load address, type and hashes). The other versions are not extracted in the repo; rerun the tool on their G64s.

## 1. Disks and versions

The C64 game is two disk sides (`s1` boot / towns / wilderness, `s2` dungeons), **Commodore DOS files** (no raw-sector
data): every G64 decodes to all 683 sectors (`c64_g64.py`, s214; the s208 scratch decoder agreed). Programs are BASIC
compiled to the bytecode of `../bytecode-notes.md` (runtime `RTL-64`, loaded at `$A000`); their PRINT / DATA text stays
readable. The boot program `B` (plain BASIC V2, side 1) names the build: `COMMODORE 64 VERSION 1.x` (line 670),
`COPYRIGHT 1985 BY W.D.WOOD`, `BY W. P. KUBALSKI`, `LDW SOFTWARE INC.` `[C]`.

| C64PP image | `B` says | Notes |
|---|---|---|
| `phantasie_s1/s2[ssi_1985](v1_0)(ntsc)` | 1.0 | own TOWNR / OUTDOORS / SCROLLS / ITEM; scrolls say HOBBITS / HOBBITON (v1.4: HALFLINGS / HALFLINGOR) and `SCROLL 19` (v1.4 `SROLLL 19`); MN, TINFO, MX equal v1.4 |
| `(v1_1)` | 1.1 | `MN` and `SCROLLS` stored as SEQ (no REL side sectors), `MN` record 22 (ILLUSIONIST) garbled from field 8 on: a damaged master, do not use |
| `(v1_2)(ntsc)(g2)` | 1.2 | side 1 only; its TWN / OUT files load at `$4000` / `$2000` (copied by a tool, not by the game) and hold DOS's stock except TWN 1 |
| `(v1_3)` | 1.3 | program files = v1.2's; disk name blank |
| **`(v1_4)`** | **1.4** | **the reference** (below) |
| `(v1_4)(alt)`, held `../original-images/phantasie1/en/` | 1.4 | same program; more files rewritten by play |

Side 2 has no version string (its `B`, 2,628 B, is a disk utility that reads `PATTERN`). Disk names: v1.4 side 1 `PH1`,
side 2 `AA`. The German set (`../original-images/phantasie1/de/`, `../extracted/`) has the same layout: `TINFO`,
`MX n`, `UTIL-3.O`, `RTL-64` byte-identical to v1.4; `MN`, `ITEM` (181 names, id 0 `NICHTS`), `SCROLLS` (13,960 B),
`FONT`, `OUT` texts translated.

**Reference: v1.4 plain** (`(v1_4).g64`, D64 decode sha256 `ec9c5f43…` / `052fdc1f…`, `../original-images/SOURCES.txt`).
No C64PP copy is unplayed, but plain v1.4 keeps the mastered state of everything except `CHAR2`, `TWN 1`, `TWN 2`,
`OUT1`, `OUT5` (section 2.1) and has no `%DNG` saves.

## 2. Files (v1.4)

Side 1 (138 files), side 2 (125 files); both sides also hold `MN` and `MX 1`–`MX 80` (identical copies) and the nine
394-byte item pictures.

| File(s) | Type, bytes (with load address), load | Holds | Tag |
|---|---|---|---|
| `BOOT` (s1), `COPY` (s2) | PRG 95, `$02A7` | autostart stub (loads and runs `B`) | `[~]` |
| `B` (s1) | PRG 2,395, `$0801` | boot BASIC: drive check, fast loader, title picture, `UTIL-3.O`, fonts, Start in Pelnor / Continue, then `TOWNR`. Sets `SA = 39460` (party base), clears `SA+1190..1309`, `SA+1020..1189` := 7 | `[C]` |
| `FAST` | PRG 701, `$CC00` | fast loader (only on a 1541 that answers `73,CBM DOS V2.6`) | `[C]` |
| `UTIL-3.O` | PRG 2,283 (s2: 2,180), `$C000` | 6502 helpers called `SYS 49152 + n` (print at, load to address, copy, ...) | `[~]` |
| `RTL-64` | PRG 8,193, `$0801` | the compiler runtime (`../bytecode-notes.md`) | `[C]` |
| `TOWNR` (s1), `OUTDOORS` (s1), `DUNGEON` (s2), `CEMETARY` (s2), `P.END` (s2) | PRG 21,631 / 29,204 / 29,235 / 2,815 / 3,544, `$0801` | compiled programs: town, wilderness, dungeon, party wipe, endgame | `[C]` |
| `MN` | **REL, record length 100**, 8,100 B = 81 records | monsters (3.1) | `[C]` |
| `MX 1`–`MX 80` | PRG 168 / 232 / 296 / 360, `$1FDA` | monster sprites (6.1) | `[C]` |
| `ITEM` | SEQ 1,589 | 180 item names (3.2) | `[C]` |
| `TINFO` | PRG 1,733, `$9920` | rule tables: HP, item values, creation modifiers, spell levels, MP, skills (3.3) | `[C]` |
| `TWN 1`–`TWN 11`, `TWN 99` | PRG 252 | towns (3.4) | `[C]` |
| `CHAR2` | PRG 4,252, `$898A` | Guild roster + the 3 elementals, 25 × 170 B (3.5) | `[C]` |
| `OUT1`–`OUT16`, `OUT18`, `OUT19` | PRG 1,014 (regions 1–5, 18, 19) / 761 (6–16) | wilderness regions (3.6) | `[C]` |
| `SCROLLS` | **REL, record length 40**, 16,480 B = 412 records | the 20 scrolls (3.9) | `[C]` |
| `DNG 1`–`DNG 10` (s2) | PRG 1,964–1,966, `$4000` | dungeon levels in DOS's layout (3.7) | `[C]` |
| `PMES 1`–`PMES 10` (s2) | PRG 1,832–3,882, `$61A8` | dungeon messages = DOS `MESSn` decrypted (3.8) | `[C]` |
| `%DNG n` / `%DNGSTS n` (s2, played copies only) | PRG 1,257 `$8CA0` / 1,964 `$91B4` | the one-slot dungeon save (DOS `DNG.SAV` / `DNGX.SAV`'s role) | `[~]` |
| `SHIELD`, `ARMOR`, `WEAPON`, `GOOD WEAPON`, `MG WEAPON`, `POTION`, `SCROLL`, `TREASURE`, `GOOD TREASURE`; s2 also `DOOR`, `TRAP`, `STAIRS`, `LOCK`, `EXIT` (386 B) | PRG 394, `$2200` | 392-byte pictures (49 × 8 bytes, 7 × 7 character cells) shown for found treasure and dungeon features; names = `OUTDOORS` / `DUNGEON` DATA; which moment and colour: GRAPHICS.md 7 (`DOOR` never loaded) | `[C]` |
| `FONT` | PRG 2,050, `$2000` | 256-glyph character set, loaded to `$E800` (`B` 370) | `[C]` load, `[~]` content |
| `FONT+HER` | PRG 4,333, `$21DA` | loaded to `$21DA`, 4,330 B copied to `$CFDA` (`B` 380, 400) `[C]`; a 38-byte header + 4,292 B (sprites? `[?]`) | `[?]` |
| `HSPRIT` | PRG 1,923, `$2000` | loaded to `$F000` by `TOWNR`; hero sprites `[?]` | `[?]` |
| `PIC.MAP`, `PIC.SCR`, `PIC.CLR` | PRG 8,194 `$A000`, 1,026 `$8400`, 1,026 `$D800` | the title picture: multicolour bitmap, screen RAM, colour RAM (`B` 200–290: MULTI COLOR, bank, BIT mode) | `[C]` |
| `SKEL2`, `CSPRIT`, `FONTO` (s2) | PRG 514 / 1,027 / 1,970 | `CEMETARY`'s ghosts, face and door / the fate figures / the temple and its font (GRAPHICS.md 9.2) | `[C]` |
| `ZALL` (s1) | PRG 3,718, `$4000` | the ending: font, Zeus's characters, sprite blocks `$C0`–`$D0`, the wipe and sound code (GRAPHICS.md 9.1) | `[C]` |
| `PATTERN` (s2) | PRG 21,707, `$2000` | **the new-game template** (s216, from II's utility, `../phantasie2/FORMAT.md` 2.1): `CHAR2` 0–4249, `TWN 99` 4260, `TWN t` 4260 + 252 t (t = 1–11), `OUT2`–`OUT19` from 7284 (1,012 / 759 B + 2 each); **no `OUT1`**. Absent on v1.0 / v1.1 side 2 | `[C]` layout |

### 2.1 Files play rewrites, and how to tell `[C]`

The game saves a town or a region from its memory buffer, so a rewritten file **loads at the buffer address**:
`TWN n` at `$81C3`, `OUT n` at `$9448`. Mastered files load at their own slot: `TWN 99` `$30A4`, then `TWN t` at
`$30A4 + 252 t` (`TWN 1` `$31A0` ... `TWN 11` `$3B78`); `OUT2`–`OUT4` from `$3C74` by 1,012, `OUT6`–`OUT16` from
`$4C44` by 759, `OUT18` `$6CE1`, `OUT19` `$70D5`. Checked on all seven side-1 copies (`TOWNR` S)ave writes
`S0:TWN<n>` and `S0:CHAR2`, then saves `TWN` from `LA` and the 4,250-byte `CHAR2` from `SA − 4250`, its load address `$898A`).

- **v1.4 plain:** `TWN 1`, `TWN 2`, `OUT1`, `OUT5` rewritten; everything else at its slot. Its slot files equal DOS's
  data (sections 3.4, 3.6), so slot = mastered.
- **`OUT1` and `OUT5` load at `$9448` on every copy**: no mastered region 1 or 5 file exists in C64PP. But side 2's
  `PATTERN` (v1.4 plain, alt, en and v1.3: one file, sha1 `431d10d5…`) holds mastered `TWN 1`, `TWN 2` and `OUT5` (s216) `[C]`: its `TWN 1`, `TWN 2`
  (and `TWN 3`, `TWN 99`) equal DOS's records in stock, restock, price, region, cell and name (its `TWN 1` differs from
  plain v1.4's file in exactly the 7 stock bytes below); its `OUT5` grid and fog equal DOS's `OUT5.DAT` (all open) and
  plain v1.4's file; its elementals equal `CHAR2`'s. Region 1 is not in it. **The slot rule comes from it:** a mastered
  file loads at `$2000` + its `PATTERN` offset (`TWN 99` 4260 → `$30A4`, `TWN 1` → `$31A0`, `OUT5` 10320 → `$4850`), as
  II's at `$4000` + its `PATTERN2` offset. `TWN 1` loads at
  `$81C3` on every copy; v1.2's `TWN 2` (`$4000`) equals DOS's record. Every region 2–19 and town of `PATTERN` equals
  plain v1.4's slot file byte for byte (s216).
- `CHAR2` (roster), `%DNG*` (dungeon save) are always play data. Its records 21–23 (the elementals) are equal on every
  C64PP copy and the held en copy (s216).
- OUT1 and OUT5 are byte-identical on every copy of every version (s216): their grid, features and pages are one state,
  so DOS's cells for them hold on any copy.
- The held en copy's `OUT6` loads at its slot (`$4C44`) but holds another grid (about 220 cells differ from v1.4 plain,
  unfogged values that are not DOS's tiles + 120): not the game's save `[?]`. The importer reads regions 2–19 from
  `PATTERN`, so it never reads this file (s216).

## 3. Tables

### 3.1 Monsters: `MN` `[C]`

REL file, 100-byte records (`OUTDOORS` / `DUNGEON` open it as `3,8,3,"MN"`), record m + 1 = monster m (0-based, DOS
order); record 81 is the REL end marker (`0xFF`). A record is 24 CR-ended decimal fields, zero-padded:

| Field | DOS (`data-tables.md`, record `+0x..`) | Field | DOS |
|---|---|---|---|
| 0 | name `+0x00` | 13 | **C64 only:** sprite blocks − 1 (= `(MX file size − 40) / 64 − 1`, all 80 `[C]`) |
| 1 | armor `+0x14` | 14 | **C64 only** `[?]` (20, 45, 47, 85, 87, 90, 100, 120; not DOS's byte_34 or sprite size) |
| 2 | damage `+0x16` | 15 | reaction modifier `+0x29` |
| 3 | level `+0x18` | 16 | next-group chance `+0x2B` |
| 4 | xp `+0x1A` | 17 | defense `+0x2D` |
| 5 | special chance `+0x1E` | 18 | gold `+0x2F` |
| 6–8 | specials `+0x20..+0x22` | 19 | group-size factor `+0x33` |
| 9 | hp_base `+0x23` | 20 | byte_34 `+0x34` |
| 10 | combat rating `+0x25` | 21, 22 | treasure items `+0x35`, `+0x36` |
| 11, 12 | companions `+0x27`, `+0x28` | 23 | attack power `+0x37` |
| | | (24) | **C64 only** `[?]` (0 in 68 records, 1–3 in 12, e.g. GREEN DRAGON 1, TITAN 3) |

**All 80 records equal DOS's in every DOS field but one**: BLACK KNIGHT (76) group-size factor **2** (DOS 3). Names
equal DOS's. No sprite width / height (DOS DS:`0x2C3E`) on the C64.

### 3.2 Items: `ITEM` + `TINFO` `[C]`

`ITEM`: 180 CR-ended names, ids 1–180 (no id 0; `TOWNR` reads them into `I$(1..180)`, `I$(0)` = `NOTHING` in code).
Value factor V = `TINFO`[89 + id]: **all 180 equal DOS's** `V[]`. Points / requirements are computed by the programs
(`TOWNR` "PT SHIELD / ARMOR / WEAPON", "UNUSABLE"); not compared with DOS's formula `[?]`. Names that differ from DOS
(the C64's own spelling, kept by a C64 import as the Amiga's are). `TOWNR` overwrites ids 156–160 after reading `ITEM`:
the bytecode after `I$(0) = "NOTHING"` reads as `FOR X = 0 TO 4: I$(156 + X) = "SAPPHIRE" + CHR$(49 + X)`, so the
town shows `SAPPHIRE1`..`SAPPHIRE5` `[~]` (opcodes `89 9C`, `8A 08 "SAPPHIRE"`, `89 31`, `B8 13` = CHR$, `E2 14` at
`TOWNR` file offset `0x52D`; `OUTDOORS` / `DUNGEON` have no such line).

| Id | C64 v1.4 | DOS | Amiga |
|---|---|---|---|
| 53 | `SH SWORD` | `SM SWORD` | |
| 70, 88, 90, 91, 93–96 | `HALBRED`, `HALBRED+1..+7` | `HALBERD...` | |
| 101–109 | `HEAL POT1`..`HEAL POT9` | `HEALING 1`..`9` | `HEAL POT1..9` (D-082) |
| 139 | `SROLLL 19` (v1.0 `SCROLL 19`) | `SCROLL 19` | `SROLL 19` |
| 150 | `--WAND--` | `WAND` | |
| 156–160 | `SAPHIRE A, B, D, C, E` | `SAPPHIRE A, B, D, C, E` (the same order) | |

### 3.3 Rule tables: `TINFO` `[C]`

1,731 B at `$9920` (`TOWNR` loads it to `XA`). **The layout of Apple II III's
`TINFO`** (`../../apple2/phantasie3/FORMAT.md`, "Rule tables"), here with Phantasie I's DOS values. Class order
THIEF, MONK, RANGER, FIGHTER, PRIEST, WIZARD; all bytes.

| Offset | Table | vs DOS `TWNS.INT` / `creation-tables.json` |
|---|---|---|
| 0 + 15 class + level − 1 | HP factors | = `TWNS.INT` 0x000 (90 B) |
| 89 + id | item value factor (id 1–180) | = DOS `V[]` (PHANTASI.COM DS:`0xD54`), all 180 |
| 270–274 | `00 00 FF FF 00` | `[?]` |
| 275 + 5 race + attr | race attribute modifiers (15 races × STR INT DEX CON CHA) | = `TWNS.INT` 0x942 (75 B): **Halfling DEX 11, DOS's** (D-001) |
| 350 + 5 class + attr | class attribute modifiers | = `TWNS.INT` 0x924 (30 B) |
| 380 + 60 class + slot | spell levels (DOS slot order, 0 = never) | = `TWNS.INT` 0x05A (360 B) but one cell: **MONK slot 41 (SLEEP) = 0, the manual's** (DOS 7; D-006) |
| 740 + 15 class + level − 1 | MP ceiling | = `TWNS.INT` 0x1C2 (90 B) |
| 830 + 150 class + 10 (level − 1) + slot | skill coefficients, DOS slot order (attack, parry, find item, spot trap, disarm, listen, unused, pick lock, unused, swim) | = `TWNS.INT` 0x21C (u16 there, bytes here) but the two unused slots: MONK L10 slot 6 (C64 0, DOS 14), PRIEST L15 slot 8 (0, DOS 4) |

Training XP / gold curves, the wilderness pair table and travel costs are not in `TINFO`; they are bytecode constants
in the programs (not decoded `[?]`).

### 3.4 Towns: `TWN 1`–`TWN 11`, `TWN 99` `[C]`

250 bytes each (the Apple II III `TF3` record layout). `TWN t` = DOS town record t − 1 (Pelnor = `TWN 1`), `TWN 99` =
Olympia (DOS record 11; the PMES5 line calls it "TOWN 99").

| Offset | Field | DOS record (`TWNS.INT` 2445 + 291 · k) |
|---|---|---|
| 0–179 | stock of item id 1–180 | `+1..+180` (DOS has a slot for id 0) |
| 180–224 | 45 restock item ids (bytes) | `+181` 45 u16 |
| 225 | 0 | |
| 226 | price index | `+0x10F` |
| 227 | region of the town cell | `+0x111` |
| 228, 229 | the cell as `y`, `25 − x` (the Amiga's transposition) | `+0x115`, `+0x113` |
| 230–249 | name, padded with `0x15` | `+279` (12 B) |

The 10 slot files of v1.4 (`TWN 3`–`11`, `99`) equal DOS's records in every field, stock included; so does v1.2's
`TWN 2` (load `$4000`). Bytes 180–229 match DOS for all 12 towns on every copy. `TWN 1` is play-written everywhere (v1.4: stock of ids 4, 21, 42, 46, 49, 111, 128 changed; its name
bytes `PELOR⌫⌫NOR` with two `0x08`, printed as PELNOR `[~]`).

### 3.5 Characters: `CHAR2` and the 170-byte record

25 records × 170 B at `SA − 4250` (`SA = 39460`; `TOWNR` loads / saves `CHAR2` with length 4,250 `[C]`). Records
0–20 the roster `[~]`, **21–23 the elementals** FIRE / WATER / EARTH (DOS: roster 22, elementals after it), 24 and
unused records hold leftover memory. `P(n)` = byte n of the record (byte 0 unused `[?]`). Fields named by the
programs' own PRINT text (`TOWNR` sheet, Inn, Guild list) are `[C]`:

| `P(n)` | Field | DOS `@` | Tag |
|---|---|---|---|
| 1–6 | STR INT DEX CON CHA LUCK | 0–10 | `[C]` |
| 7 | defense rating? (players 0–45; elementals 20) | 12? | `[?]` |
| 9, 10, 11 | MP ceiling, MP max, MP current | 14, 16, 18 | `[C]` (`../bytecode-notes.md`; level-1 values = `TINFO` MP table) |
| 12, 13 | HP max, current | 20, 22 | `[C]` |
| 14, 15 | gold in bank, high and low byte (a fresh character holds 1, 0 = 256, DOS's fresh 256) | 262 | `[C]` sheet, `[~]` order |
| 16, 17 | age clock, high and low byte | 264 | `[C]` sheet, `[~]` unit |
| 18 | status 0 DEAD, 1 OKAY, 2 SLEEP (`S$`) | 24 | `[C]` |
| 20, 22, 24, 26, 28, 30, 34, 38 | ATTACK, PARRY, FIND ITEM, SPOT TRAP, DISARM, LISTEN, PICK LOCK, SWIM | 30–48 | `[C]` |
| 21–39 odd | skill bonus `2 · attr + 30` (STR, DEX, INT, INT, DEX, INT, –, DEX, –, CON), set at creation | 50–68 | `[C]` (13 of the 16 roster records exactly; records 1 and 5 (dead: swim +2 = CON lost at death) and 15 (STR / DEX changed) fit DOS's "not rewritten when stats change") |
| 42 | score | 70 | `[C]` |
| 48, 49, 50 | worn shield, armor, weapon | 82, 84, 86 | `[~]` |
| 51, 52, 53 | experience, 24 bits high first | 266 | `[C]` sheet, `[~]` order |
| 54 | level | 88 | `[C]` |
| 55 | magical-pool product (1 fresh) | 90 | `[~]` |
| 56, 57, 58 | fight-start copies (58 = attack) | 246, 248, 250 | `[~]` |
| 59 | creation number, nonzero = used (elementals **38, 39, 40**, the manual's numbers) | 92 | `[~]` |
| 61–120 | spell n (1-based, DOS slot n − 1) known: 0 / 1 known / 2 learnable | 96–214 | `[C]` |
| 131–142 | name (12 B; the elementals' run on with `0x08` to `FIRE ELEMENTAL`) | 270 | `[C]` |
| 151 | class | 220 | `[C]` |
| 152–160 | nine carried items | 222–238 | `[~]` |
| 161 | race (elementals 16) | 216 / 256 | `[C]` |
| 162, 163, 164 | weapon, shield, armor points (elementals 40 / 30 / 20, 100, 7 / 14 / 21 = DOS's) | 240, 242, 244 | `[C]` |
| 165 | town | 254 | `[C]` (Guild list `TWN#`) |
| 8, 19, 40–41, 43–47, 60, 121–130, 143–150, 166–169 | | | `[?]` |

Elementals vs DOS's `elementals.json`: HP, attack / parry, level 20, the points and the spell sets agree; P(7) = 20,
P(9) = 0 (DOS: defense 0, ceiling 20) and the names differ `[~]`. The 12-byte name fields with their backspaces applied
read `FIRE ELE`, `WATER ELEM`, `EARTH ELEM` (DOS: `FIRE ELEM`, ...); how far the game prints past the field is `[?]`.
DOS's race word (15, UNDEAD shown) has no C64 counterpart: P(161) = 16 = DOS's birth race.

### 3.6 Wilderness: `OUT1`–`OUT19` `[C]`

The DOS `OUTn.DAT` content in the Amiga's arrangement (`../../amiga/GAME-TABLES.md` section 3):

- **Grid** 0–519: cell (x, y) at `(25 − x) · 20 + y`; tile ids and fog (`+120`, `overworld-movement.md`; play pairs
  `124 → 4`, `213 → 93`, ...) as DOS.
- **Features** 520–594: 15 × 5 bytes `y, 25 − x, arg, dest x, dest y` (unused: all 0; DOS keeps leftover bytes there).
- **Text pages** from 595: 40-byte pages in **screen codes** (`A` = 1; no NUL: DOS's last byte is a blank), 10 pages +
  17 bytes in the 1,012-byte regions, 4 pages + 4 bytes in the 759-byte ones (6–16). No polygon area.

Against DOS, region by region (v1.4 slot files): grids, features and pages equal DOS's but

| Region | Cell / record | C64 | DOS | Note |
|---|---|---|---|---|
| OUT9 | (1, 18), (15, 19), (20, 9) | 188 | 187 | the Amiga's three cells (fixed fight spent / C64 data) |
| OUT18 | (5, 10), (5, 15), (10, 10), (10, 15), (15, 10), (15, 15), (20, 10), (20, 15) | 188 | 187 | the same kind (not in the Amiga's list) |
| OUT13 | (9, 0) | 220 | 123 | |
| OUT15 | (0, 11) | 220 | 143 | |
| OUT1 (play-written everywhere) | row 0 | 100 | 101 | `[?]` play or data |
| OUT1 | (18, 14) | 0 | 10 | `[?]` |
| OUT1 | features | DOS's 9 records in another order | | `[?]` |
| OUT2 / OUT12 | Amiga's lake cells (11, 8) / (11, 14) | = DOS | | the C64 does **not** share the Amiga's lake |
| OUT16 | (4, 15) | 87 = DOS | | the Amiga has 88 |

Text: every page is DOS's text but OUT1's pages (one column further right), OUT2 / OUT3 "HILLSIDE." and
"[original text] TE PLE HERE." (DOS no full stops, `TEMPLE`), OUT18 "SURROUNDED BY A BRILLIANT WHITE LIGHT" (DOS
"[original text]"), OUT19 "...PLANE OF OLYMPUS." (full stop).

### 3.7 Dungeons: `DNG 1`–`DNG 10` `[C]`

DOS's `DNGn` byte for byte to the C64 file's end (DOS's files are 20 bytes longer: `"\rENDING SEQUENCE\r\x1a\x1a\0"`
leftovers; DNG6 is 41 bytes longer on the C64, `0x0D` padding). Differences (cell kinds: `dungeon-representation.md`):

| Level | Offset | Cell | C64 | DOS | Reachable? |
|---|---|---|---|---|---|
| DNG2 | `0x2D8` | (2, 22) | 0 wall | 1 floor | DOS cell walled in |
| DNG3 | `0x1D0` | (2, 14) | 0 wall | 15 wandering | **next to two wandering cells**: C64 geometry differs |
| DNG3 | `0x361`, `0x3B2`, `0x49E` | (7, 26), (22, 28), (27, 35) | 0 | 144 message, 15, 130 forced fight | DOS cells walled in |
| DNG5 | `0x35A` | (0, 26) | 0 | 153 message | walled in |
| DNG6 | `0x177` | (12, 11) | 0 | 156 message | **next to a message and a script cell** |
| DNG6 | `0x491` | (14, 35) | `0xD6` | `0xC7` | the Amiga's lock cell (bank 2 vs bank 1) |
| DNG7 | `0x17E` | (19, 11) | 144 message | 0 wall | **C64 adds a cell** next to a forced fight and a message |
| DNG5, DNG7, DNG8, DNG9 | `0x4FF`, `0x50E`, `0x57E`, `0x5EA`–`0x5ED`, `0x626`, `0x640` | side tables | 0 / 65 / 72 | 238 / 8 / 16 / 193, 81, 76 / 65 / 71 | `[?]` what each byte is |

v1.0 / v1.1 differ from v1.4 only in DNG8 `0x546` (82; v1.4 = DOS 210).

### 3.8 Dungeon messages: `PMES 1`–`PMES 10` `[C]`

Plain ASCII in the layout DOS's `MESSn` decrypts to (`decrypt_messages.py`: slot table, 41-byte records from 230), 18
bytes shorter (DOS's `ENDING SEQUENCE` tail; PMES8 21). Byte-identical to DOS's plaintext but:

| File | C64 | DOS |
|---|---|---|
| PMES1 | `THERE IS A CHEST HERE ---` | `HERE. ---` (the Amiga also drops the full stop) |
| PMES3 | `THE SKELETON OF THE WARRIOR.` | `OF A WARRIOR.` |
| PMES5 | `'TO VISIT THE GODS TELEPORT TO TOWN 99'` | `[original text]` |
| **PMES7** | the bleebs: `A RED BLEEB` / `A BLUE BLEEB` lines swapped, `[original text] 'WHAT KIND OF BLEE...`, `...THAT IT SOMETIMES LIES?` | `A BLUE` / `A RED`, `A GREEN BLEEB SAYS 'WHAT KIND OF BLEEB...`, `...THAT I SOMETIMES LIE ?` (**the liar puzzle reads differently**) |
| PMES9 | `SENCE` | `SENSE` |

v1.0: PMES9's option row `  LEAVE      FI...` (v1.4 `LEAVE CAVE FIGH...`); v1.1: PMES8 `GODS SPEAKS`.

### 3.9 Scrolls: `SCROLLS` `[C]`

REL, 40-byte records. Records 1–4: the index, CR-ended decimals: scroll k is records `idx[k]..idx[k+1] − 1`
(`5, 28, 51, ..., 391, 412`). Each scroll is its page as printed, inside a `+---+` / `! ... !` frame (16–23 rows,
some indented 1–3 columns), in PETSCII for the lower / upper-case set: `0xC1..0xDA` capitals, `0x41..0x5A` small
letters (mixed case, as DOS). Words equal DOS's scrolls but: 1 `IT'S PEOPLE` (DOS `its`), 3 `THIER`, 4 `DOORS`,
**5 "THE THIRD POOL LIES NORTH OF THE STAR" (DOS south)**: the C64's matches the map `[~]`: the third pool (tile 113)
is OUT14 (12, 6) on both, and OUT14's pages put the wooden star "on the beach to the south" of the signs at (12, 0) /
(12, 1). Which tiles draw the star is `[?]`, but both shapes south of the signs (tile 35 at x 2–6, y 10–15, holding
Phantasia's cell (5, 11); tile 33 at x 19–24, y 10–14) lie south of the pool's row 6 (y grows southward). Also 10 `HALFLINGS AND ELVES` (DOS `elves, halflings`),
12 `HAL-FLINGOR`, `NORTHWEST` (DOS `N W`). v1.4's `DIFFERNECES`, `DESCENDENTS` are DOS's spellings too (v1.0 has
`DIFFERENCES`, `DESCENDANTS`).

### 3.10 Names and text in the programs `[C]`

Plain in DATA / PRINT: classes, races (`TOWNR`: ... `"LIZ.MAN"`, `UNDEAD`; `OUTDOORS` / `DUNGEON`: `ORGE`,
`"LIZARD MAN"`), status `DEAD, OKAY, SLEEP`, runes `AR, ER, FR, WT, GOD`, Guild / town menu words, Mystic ranks
(`NOVICE ADVENTURERS` ... `HEROES OF GELNOR`, `SUPREME BEINGS`), the endgame (`P.END`: Zeus "Well done, my small
friends..." in its own line breaks, ending "You will learn later how to use it properly." where DOS ends "properly in
Phantasie II.", `endgame.md`). **No spell names anywhere on either side** (searched case-insensitively without the first letter, so PETSCII
capitals cannot hide them: only dungeon texts with `TELEPORT` match): spells are cast by number (the Apple II
port does the same). `TOWNR` DATA also holds `7,9,3,15,12, 2,1,7,32,146,22,133,4050,4291,3215,2145,2145,2408,2703,2861,-1`
and `OUTDOORS` / `DUNGEON` `17167, 21629, ... 45830` (11 values) `[?]`.

## 4. C64 vs DOS in short

- **Numbers that differ:** BLACK KNIGHT's group size (2 vs 3); MONK SLEEP never (the manual's, D-006); 2 unused skill
  cells; the DNG cells of 3.7 (3 change reachable geometry); the OUT cells of 3.6.
- **Text that changes play:** the PMES7 bleeb puzzle, scroll 5's third pool (north vs south; the C64's fits the map). Import choices: D-084…D-086 (section 7, built s216).
  Cosmetic: item spellings (3.2), PMES / OUT / scroll typos.
- **Shared with the Amiga against DOS:** HEAL POT1..9, the PMES1 chest line without a full stop, DNG6's lock bank,
  OUT9's three cells, the region / town cell transposition `(y, 25 − x)`, plain-text messages and scrolls. Not shared:
  the Amiga's OUT2 / OUT12 lakes and OUT16 (4, 15). A common ancestor of the C64 and Amiga data, apart from DOS's, is
  the simplest reading `[~]`.

## 5. Rules seen so far (from `../bytecode-notes.md`, German build)

Resurrection and the inn's MP raise equal DOS; CON loss at death equals DOS; healing tiers `6 t² + 4`. Not re-read on
the English build `[~]` (the English programs are other sizes).

## 6. Graphics and sound

Decoded s218, with the 6502 code of `UTIL-3.O`, `B`'s BASIC and live VICE screens: **`GRAPHICS.md`** (beside this file),
which also describes the `c64` art pack the import builds from them. In short:

### 6.1 Monster sprites: `MX n` `[C]`

Monster n − 1. Loaded at `$1FDA` (in play: to a group's slot, e.g. `$CB5A`): a 38-byte **sprite-band record** (the
raster interrupt `$C529` loads it into the VIC: y base, 8 (x, y) byte pairs, 8 colours, block base, 8 block indices,
x / y expand and enable masks, next raster line; GRAPHICS.md 2), then k = MN field 13 + 1 hires sprite blocks. The record
lays out the whole group; one monster = its first sprites up to the first repeated block `[~]`. A one-sprite monster
attacks in block 1 `[C]`; a several-sprite monster's extra block replaces sprite 0 (II's VIPER live, s219; taken for all
`[~]`). (s214's reading "8 × 16-bit x
positions" was wrong.) `FONT+HER`'s first 38 bytes are the same record for the party's sprites in a fight.

### 6.2 Other pictures

| File | What | Tag |
|---|---|---|
| `HSPRIT` | 15 (idle, attacking) hero sprite pairs, pair = race 0–14 (no UNDEAD) | `[C]` |
| `PIC.MAP` / `.SCR` / `.CLR` | the title: multicolour bitmap, background black (`B` 200–290, 595) | `[C]` |
| `FONT` (`$E800`) | upper-case screen font, the map's terrain characters (64–87), the dungeon map's (`$E6`–`$EC`) | `[C]` |
| `FONT+HER` (`$CFDA`) | the hero band record; a mixed-case font; sprite blocks 91–95 (dungeon party; map party, town, inn, dungeon); the town's character set (the six buildings) | `[C]` |
| `SHIELD` … `EXIT` | character pictures printed at `$EAC0` on: 7 × 7 characters (`EXIT` 8 × 6), numbered down each column; moments and colours GRAPHICS.md 7 | `[C]`, `DOOR`'s colour `[?]` (never shown) |
| `CEMETARY`'s `SKEL2`, `CSPRIT`, `FONTO`; `ZALL` | the defeat and ending screens | decoded s221 (GRAPHICS.md 9), in the `c64` pack |

The overworld is printed by `UTIL-3.O` `$C5C3` (class = map byte div 10, a character pair by column parity, colours from
`$C688`); the dungeon map in `FONT`'s `$E6`–`$EC` by DOS's cell ranges, grey (GRAPHICS.md 5, 6). Palette: the VIC-II's
16 colours as Colodore. No sound or music files: the SID code is inside the programs; the end screens' sounds are decoded
and played (GRAPHICS.md 9.6, s226), the fights' and the town's are listed there `[?]`.

## 7. The importer: `p1-c64` (built s216; plan s214)

The rule of the Amiga and Apple imports: **the C64's text, DOS I's numbers** (DOS wins on numbers), through DOS's
builders, so the GameData files keep DOS's schema. Built (s216): `src/OpenPhantasie.Importer/Pipelines/P1C64/`
(`P1C64Pipeline`, `P1C64Tables`), `Containers/D64Volume.cs` (`D64Volume`, `G64Image`), profiles `p1-c64` (v1.4) and
`p1-c64-de` (recognised only), the shipped `GameData/phantasie1/c64-import.json` (from
`tools/scripts/build_c64_p1_import.py`), tests `P1C64TablesTests`, `D64VolumeTests`. Write-up:
`docs/plan/10-importer.md` 7.11. The subsections below are the s214 plan with what became of each point.

**Containers (built).** `.d64` (174,848 B, or 175,531 with error bytes) and `.g64` (`GCR-1541` signature): the C# port
of `c64_g64.py` gives the D64; `.nbz` is not read (C64PP ships G64s of every dump). The D64 reader takes the directory
chain, file chains and a REL file as its data-block chain (records by the entry's length); DEL / odd entries are
skipped. Every file of the v1.4 G64s equals `extract_c64_d64.py`'s; every held C64 I image decodes all 683 sectors.

**Detection (built).** Side 1: `TOWNR`, `TINFO`, `MN`, `ITEM`, `SCROLLS`; side 2: `DUNGEON`, `DNG 1`, `PMES 7`
+ `PATTERN` (v1.4's sha1s, `extracted-v1.4/manifest.json`); name markers `TOWNR`, `OUTDOORS`, `TINFO` (another build reports as
such). `B`'s version string is not read. The German D64s: profile `p1-c64-de` (its own key files and name markers `SCHILD`, `WAFFE`, recognised). Other
versions against v1.4: `MN`, `TINFO`, `MX`, `DNG` (but v1.0's DNG8 `0x546`), `PMES` (but v1.0's
PMES9 option row) are equal in v1.0 and v1.3; `SCROLLS` differs in v1.0 (HOBBITS / HOBBITON + 2 spellings) and in v1.2 /
v1.3 (HOBBITS / HOBBITON); `ITEM` in v1.0 (id 139); the programs (`TOWNR`, `OUTDOORS`, side 2 `DUNGEON`, `B`) differ
in every version. v1.0 / v1.3 could be extra profiles reading the same layouts (not built: they report as another
build); v1.1 is refused (damaged `MN`). German: recognised only (German text).

**Mutable files and played copies (built).** `CHAR2`, `TWN n`, `OUT n`, `%DNG*`, `%DNGSTS*`. Played = loads at `$81C3` /
`$9448` (2.1). **s216: side 2's `PATTERN` holds the mastered towns (`TWN 1` = DOS's record), regions 2–19 and the
elementals** (byte-identical on every copy, sha1 `431d10d5…`; a key file of side 2), so the import reads towns,
regions 2–19 and elementals from it whatever side 1 holds; only OUT1 has no mastered copy (its fog and fights come
from DOS's tables by cells, as for the Amiga's played regions; OUT1 is the same on every copy, 2.1). This replaces the
s214 plan's "OUT1 / OUT5 / TWN 1 are never available mastered". The profile's mutable files are v1.4's slot files
(OUT2–4, 6–16, 18, 19; TWN 3–11, 99), so a played copy is reported, but nothing it rewrote is read: it imports as
a fresh one (the held en copy's import equals the C64PP pair's, its odd OUT6 included; no map-restore question).

**What maps where (built as planned):**

| GameData file | From the C64 | DOS's values (class (a) `phantasie1/c64-import.json`) |
|---|---|---|
| `monsters.json` | `MN` (all DOS fields; fields 13, 14, 24 unused) | BLACK KNIGHT group size 3 (field and `raw_hex` byte); `sprite_sizes` (DOS's 80 pairs; none on the C64) |
| `items.json` | `ITEM` names (ids 1–180), `TOWNR`'s `NOTHING` (id 0), `TINFO` 89 + id values (= DOS) | — (id 0's value 0, no item) |
| `names.json` | classes, races (`TOWNR` DATA), items, monsters, town labels (`PATTERN`'s town and elemental names) | spell names: none on the C64 → `spell_names`: the manual's table (slots 0–53) + `DIVINE I` (remaster wording, as II's Apple import, D2-050) |
| `towns.json` | `PATTERN`'s `TWN 1`–`11`, `99` (stock, restock, price, region, cell, name with backspaces applied) | — (= DOS's records) |
| `elementals.json` | `PATTERN`'s `CHAR2` 21–23 (INT, MP, HP, status, attack, parry, level, creation number, points, race, class, spells, name) | MP ceiling 20, creation numbers 23–25, race 15; `FIRE ELE` → `FIRE ELEM` (`name_fixes`, as D-083) |
| `overworld.json` | `PATTERN`'s regions 2–19 + side 1's `OUT1`: grids (transposed), features, pages (screen codes; a CR-padding page is no page; OUT19's page 6 is DOS's leftover ASCII, kept) | OUT1 row 0 and (18, 14), OUT1's feature order, the OUT9 / OUT13 / OUT15 / OUT18 cells, DOS's leftover bytes in unused feature records (OUT3, 4, 12–16, 19) |
| `dungeons.json` | `DNG n` + `PMES n` (DOS layout: DOS's builders without the cipher) | every differing byte of 3.7 (18) |
| `messages.json` | `PMES n` | the bleeb puzzle's words (`text_patches`, D-084), applied to `PMES 7` before both files read it |
| `scrolls.json` | `SCROLLS` (the rows between each frame's `!`s, the frame's blank first / last rows dropped; 14–21 lines) | — (scroll 5 keeps the C64's NORTH, D-085) |
| `item-descriptions.json`, `spells.json` | the builders (`SAPHIRE` takes the sapphire text) | spell names as above |
| `creation-tables.json`, `rules.json`, travel / encounter tables | shipped (class a); `TINFO` equals DOS but MONK SLEEP | — |

Strings (built): 259 ids found verbatim in `TOWNR`, `OUTDOORS`, `DUNGEON`, `CEMETARY`, `P.END` get `p1-c64`
alternatives (`build_string_sources.py`); Zeus's speech in the C64's own wording (`P.END`'s DATA, PETSCII mixed case,
`tools/scripts/p1_c64_own_wording.json`, read through the importer's `P.END:petscii` key). 22 ids of the shared table's
I field / map screens are not in the C64's programs verbatim and stay "..." in a public C64-only build `[~]`.

**Decided (user s215, D-084…D-086), built s216:** PMES7's bleeb puzzle (the two pool hints and question 3) takes DOS's
lines, the rest of the C64's text stays; scroll 5 keeps the C64's NORTH; the three reachable DNG cells (DNG3 (2, 14),
DNG6 (12, 11), DNG7 (19, 11)) take DOS's values like the walled-in ones.

**Graphics pack (`c64`): built s218** (`P1C64Art`, recipe 2; GRAPHICS.md 8): monsters, hero sprites, title, town
buildings, the character-tile overworld and dungeon maps, Colodore colours; drawn when no Amiga copy is imported. No sound.

**Not done here:** the bytecode tables (training curves, pair table, step costs) and the 170-byte record's `[?]` bytes
(only needed for a C64 roster transfer); a live check in VICE. The C64's own wording for its town / Guild / sheet
lines (PRINT text not found verbatim) is not mapped.
