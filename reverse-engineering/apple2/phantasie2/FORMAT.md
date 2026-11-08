# Phantasie II (Apple II, SSI v1.0 2/5/86): disk and data formats

s158. The importer builds II from the Apple II version, the Memorial Set's II (user s158, `docs/plan/10-importer.md`
section 6, decision 2). This file covers the containers, where each table lives, and how each maps to II's GameData
(which is built from the Atari ST today, `reverse-engineering/atari-st/phantasie2/FORMAT.md`). Graphics:
`GRAPHICS.md` next to this file. Tags: `[C]` read in the code or data, `[~]` probable, `[?]` not known.

Tools (all `python3 -I`, run from the repo root):

| Script | Does |
|---|---|
| `tools/scripts/a2_rdos.py <side.dsk> [--extract DIR]` | RDOS catalog + file reader (library `RdosDisk`) |
| `tools/scripts/a2_applesoft.py <side.dsk> "<PROGRAM>"` | Applesoft detokeniser (library `program(disk, name)`) |
| `tools/scripts/extract_a2_p2_tables.py [--side-a A] [--side-b B] [--out DIR]` | every table below -> `extracted/*.json` |
| `tools/scripts/diff_a2_p2_vs_st.py [--build] [--json OUT] [--expect extracted/diff-vs-st.json]` | field-by-field report against `src/OpenPhantasie.Data/GameData/phantasie2/`; `--expect` fails when the summary moves from the committed one |
| `tools/scripts/extract_a2_p2_graphics.py`, `build_a2_p2_asset_manifest.py` | the Apple II art pack (`GRAPHICS.md`) |

## 1. Images and containers

- Repo images: `reverse-engineering/apple2/original-images/phantasie2/` (`SOURCES.txt` one folder up): the 4am crack
  (two 140K `.dsk`, sides A and B) and `.woz` flux images of the original protected disk.
- **RDOS 2.1** (SSI's DOS, "COPYRIGHT 1981"), a **13-sector** DOS `[C]`. Passport's crack keeps the 13-sector layout
  inside a 16-sector `.dsk`: logical sector n is `.dsk` track n // 13, sector n % 13 (DOS-order offset
  `(n // 13) * 4096 + (n % 13) * 256`); `.dsk` sectors 13–15 of every track are zero.
- **Catalog** = track 1, sectors 0–10, 32-byte entries, ended by a first byte 0 `[C]`: name 0–23 (high-bit ASCII,
  space padded), 24 type (`A` Applesoft, `B` binary, `T` text), 25 sector count, 26–27 load address, 28–29 length,
  30–31 first logical sector. Files are contiguous. A deleted entry starts with 0x80 (side A has a stale `TWN210`).
  Track 1 sectors 11–12 hold RWTS code, not entries.
- **Crack patches** (4am notes): side A `T0E,S07` = logical sector 189 = the first sector of `SSI.INIT2`, plus boot /
  RWTS sectors on track 0 of both sides. So `SSI.INIT2` differs from the original and is not a key file; every other
  file equals the original `[C]` (s169, from the decoded `.woz`, below). The Memorial Set's II (s166) is a cracked
  `.dsk` pair in DOS 3.3 sector order (another crack: track 0 / RDOS differ), unplayed:
  `reverse-engineering/apple2/original-images/SOURCES.txt`.
- **The `.woz` originals** (s169, `src/OpenPhantasie.Importer/Containers/WozImage.cs`) `[C]`. II's two sides are
  WOZ 1 (Applesauce 1.0.5), I's two (`phantasie1/woz-original/`) WOZ 2 (Applesauce 1.88.4, INFO boot-sector format
  3 = both 13- and 16-sector). Track t is the bit stream at `TMAP[4t]` (WOZ 1: 6656-byte records in `TRKS`, bit count
  u16 at +6648; WOZ 2: `TRKS` entry t = first 512-byte block, block count, bit count). Every track 0–34 is a plain
  13-sector track:
  - address field `D5 AA B5`, 4-and-4 volume, track, sector, checksum (volume ^ track ^ sector), `DE AA`; I from track 1
    on uses `D4 AA B7` instead (track 0 `D5 AA B5`), with `D5 AB` repeated as gap filler. These are what the 4am notes'
    RWTS patches undo (`T00,S0A`: `C9D4 -> C9D5`, `B7 -> 96`; II: `B5 -> 96`);
  - data field `D5 AA AD`, 410 + 1 bytes of 5-and-3 GCR (32-byte table `AB AD AE AF B5 … FE FF`, index = value),
    each value XORed with the one before, the last one the checksum (the chain ends at 0);
  - the 410 values to 256 bytes (DOS 3.2's layout, read off the sectors that equal the 4am `.dsk`): values 0–153 hold
    the low 3 bits, 154–409 the top 5. Bytes 5g..5g+4 (g = 0–50) take their top bits from value 154 + 51k + 50 − g
    (k = 0–4); with A = value 103 + g, B = 52 + g, C = 1 + g their low bits are A >> 2, B >> 2, C >> 2 for k = 0–2,
    then bit 1 (k = 3) and bit 0 (k = 4) of A, B, C (A highest). Byte 255 = value 409 over value 0's low 3 bits.
  - Track 0 sector 12 has an address field and no data field on all four disks: a 16-sector `D5 AA 96` sector (6-and-2,
    the boot sector a 16-sector ROM loads) sits in its gap. It lies inside the `RDOS 2.1` file, which nothing reads;
    the reader leaves it zero (so does the 4am `.dsk`). I's track 0 also has a `D4 AA B7` sector-0 address field
    before that boot sector. Every other sector of tracks 0–34 decodes with good checksums (the reader reads each
    track twice round, so a sector across the bit stream's end is found too); whatever SSI's protection check reads
    does not stop the files being read.
  - Decoded in the 4am layout (sector s of track t at `t * 4096 + s * 256`), every sector of tracks 1–34 equals the 4am
    `.dsk` except the patches its notes list: II side A T14 S7 `$19` `20 A0 08 B0` (the crack: `BD 88 C0 90`); I side A
    T33 S12 the same bytes and T34 S2 `$0F` `C6` (crack `A9`); I side B T34 S5 `$0F` `C6`. Track 0 differs (the
    crack's own boot and RWTS). The catalogs are identical; besides `RDOS 2.1`, only `SSI.INIT2` (II A), `SSI.INIT`
    and `QWERTY` (I A) and `QWERTY` (I B) differ.
  - **The `.woz` disks are the same played copy** the crack was made from: their `CHAR2`, `TWN2x`, `OUTTn`, `DNGSAV`
    equal the 4am dump's (and differ from the Memorial Set's). An import from the `.woz` pair gives output byte-equal
    to the 4am pair's (`WozImageTests`) and the same "played copy" warning.
- **The reference dump is a played copy** `[C]`: `CHAR2` holds a roster, `TWN21`–`TWN210` differ from their template,
  and 15 of the 17 `OUTT` files were rewritten (756 B instead of the shipped 768 B, fog partly lifted; section 3.6).

## 2. Program

Applesoft BASIC plus small machine-code helpers `[C]`; `& RECALL / STORE / RUN / DEL / READ` are RDOS ampersand
commands. Side A: `PHANTASIE II` (title, ICE DRAGON picture), `INTRO2` (menu: start in Pippacott / continue / demo; loads
`SHAPES` at $451F and `INFO` at $302), `TOWN2` (town, Guild, shops, training, transfer bookkeeping), `OUTDOORS2`
(wilderness + combat), `CEMETARY2` (defeat). Side B: `DUNGON2` (dungeon + combat), `DNGLD2` / `DNGOUT2` (enter / leave),
`WIN` (ending), `DEMO`, `BACKUP` (backup disk, new game, I ↔ II transfer). Lines quoted below are BASIC line numbers.

Memory map used by the tables `[C]`: party / Guild records `SA = 16385` ($4001), 170 B each (`FN P(X) = PEEK(SA +
170*PM + X)`); global state `SA + 1189..1269`; town / region / level buffer `LA` = 2048 (towns, file at 2049) or 2050
(regions, levels); `XA = 1990` (TINFO at XA + 310 = $08FC). `INFO` ($302–$3EF): `CALL 770` tone, `CALL 800` prints a
CR-ended string whose address is POKEd at 803 / 804, `CALL 816` draws the visible wilderness map with ROM `DRAW`.

## 3. Tables and their GameData mapping

`extract_a2_p2_tables.py` writes each table in the ST GameData field names where a field means the same; Apple-only
fields go under `a2`. Diff summary of the reference dump (s158): section 5.

### 3.1 Monsters: `MN20`–`MN29` (side A and B, identical) `[C]`

Text files (type `T`), read with `& READ "MN2"+MF` and `INPUT` (OUTDOORS2 / DUNGON2 line 900–915): monster `MO` is
record `MO mod 8` of file `MN2<MO div 8>`, 80 monsters, 25 CR-ended fields each; each 768-B file has stale bytes after a
`\0` that the game never reads. Field order (line 910) → ST name:

`N$` name · `MA%` armor · `DM%` damage · `TM` level · `EX` xp · `SL%` special_chance · `SP%(0..2)` specials ·
`DM` hp_base · `PW%` combat_rating · `WG`, `ML` companions · **`C%` SCALE** · **`H%` HCOLOR and group x offset** ·
`PM` reaction_modifier (< −10: Pluto's minion −(10 + n), line 916) · `PL` next_group_chance · `D%` defense · `X` gold ·
`Y` group_size_factor · **`YS%` rank height in pixels** (lines 1480–1500; the ST keeps the sprite height for this, so
the ST's `byte_34` 35 vs Apple 22 is a layout value, not a rule number) · `IA`, `IB` treasure_items · `AK%`
attack_power · **`S%` shape number** (pose A = S%, pose B = S% + 1, `GRAPHICS.md`).
One name has BEL characters (`BEE^GPING^G BL^GOBS`): the extractor drops control characters.

### 3.2 Items: `ITEM` + `TINFO` `[C]`

`ITEM` (side A, type T): 180 CR-ended names, ids 1–180 (TOWN2 230). There is no id 0 (`NOTHING`) on the disk. Value
factor of item i = `TINFO[89 + i]` (`PEEK(XA + 399 + i)`; price `INT(f² / 5 * town factor + 1)`, TOWN2 1117; sell
price `f² / 8`, line 8430). All 180 factors equal the ST's. kind / points / min_str are index formulas (TOWN2 1150–1190
gives the same "PTS" rule as I), not data.

### 3.3 Names `[C]`

Classes TOWN2 260 (`DATA`), races TOWN2 270 (16 incl. UNDEAD, "LIZARD MAN" spelled out), status OUTDOORS2 290
(DEAD / OKAY / SLEEP), terrain names OUTDOORS2 120 (13), treasure kinds OUTDOORS2 280, items `ITEM`, monsters `MN2x`.
**The Apple II has no spell names**: spells are learnt, listed and cast by number 1–60 (TOWN2 3060, 6882; DUNGON2
order menu). `spells.json` names, `strings/en.json` lines and the elementals' names have no Apple source.

### 3.4 Creation and level tables: `TINFO` `[C]`

1730 B loaded at $08FC (TOWN2 830); offsets in the file (address − 2300):

| Offset | Table | Read by |
|---|---|---|
| 0 | hp_factors, class·15 + level | TOWN2 1520 |
| 89 + id | item value factors (90–269) | TOWN2 1117 |
| 270–274 | 5 bytes, not read `[?]` | — |
| 275 | race attribute modifiers, race·5 + attr (15 races) | TOWN2 2260 |
| 350 | class attribute modifiers, class·5 + attr | TOWN2 2250 |
| 380 | spell levels, class·60 + spell (0 = never) | TOWN2 1570 |
| 740 | MP ceiling, class·15 + level | TOWN2 1505 |
| 830 | skill coefficients, class·150 + level·10 + skill, **one byte** (ST: u16) | TOWN2 1512 |

Same cells as the ST's `TWNS.INT` 0–2445 except four (section 5).

### 3.5 Towns: `TF2` (template) and `TWN21`–`TWN210` (played) `[C]`

10 towns × 250 B. `TF2` (load 5000) is the new-game template: BACKUP 500–550 RECALLs it and STOREs the ten 250-B
slices as `TWN21`–`TWN210`; the game rewrites `TWN2n` on Save (TOWN2 3730). The extractor reads `TF2` only. Record:
0–179 stock of items 1–180 (`LA + id`), 180–224 restock pool (TOWN2 860 reads `LA + 181 + RND·L·3`, L ≤ 15, so slots
0–44; byte 225 is never read), 226 price factor (/100, TOWN2 825), 227 exit region, 228 exit row y, 229 exit column
25 − x (TOWN2 6020: the ST's order), 230–249 name padded with 0x15. The ST's unused STARVILLE / OLYMPIA records have no
Apple counterpart. Restock: the Apple sets **9** pool items per entry (line 860, `FOR X = 0 TO 8`), the ST 4 (D2-045,
D-054 row) — a rule, noted here, not raised.

### 3.6 Wilderness: `OUTT<n>` (17 regions, side A) `[C]`

Regions 1, 2, 3, 5, 6, 7, 8, 11, 12, 17, 18, 22, 26, 27, 28, 30, 31 (the ST's set). 768 B as shipped:

- 0–519 grid: OUTDOORS2 reads cell (CX, CY) at `LA + CY·20 + CX` with CX 0–19, CY 0–25 (line 390). That is the ST's
  `(25 − x)·20 + y` with CX = y, CY = 25 − x: the **same bytes** as the ST's grid. Fog = tile + 120 (line 5030).
- 520–594 features, 15 × 5 B (CX, CY, arg, dest x, dest y): the ST's (y, 25 − x, …) again. The search loop (5950–5980)
  runs to offset 625, past the 15 records, into the text (harmless).
- 595– text: the string printed for a text cell starts at `595 + 40·arg` and runs to its CR (CALL 800, line 6020), so
  one arg = one message of any length. The ST cuts text into 40-B pages and encodes the page count in arg (arg 10–19 = 2
  pages, 20+ = 3), so text-cell args differ (OUT6, 18, 26, 28, 30) while the text agrees.
- **Play rewrites the file** (OUTDOORS2 7700–7710 `STORE W$,2050,756` on leaving), changing only the fog. The shipped
  size is 768 B: only OUT26 and OUT30 still have it in the reference dump, and both are fully fogged as on the ST;
  the shipped fog of the other 15 is on the Memorial Set's unplayed disk (s166). There `OUTT2` is 756 B (stored back
  before shipping) and `OUTT1` / `OUTT2` are open, the other 15 fully fogged: the ST's start fog cell for cell `[C]`
  (s167, `MemorialSet_StartFogIsTheSts`). So 756 B does not mean played; the importer tells a played copy by the
  Memorial disks' sha1s (profile `mutable_files`).

**Orientation** `[C]`: the Apple draws CX across and CY down (`DRAW … AT 140 + 7·CX, 2 + 6·CY`, line 5040) and moves
N = CY − 1, E = CX + 1 (lines 5220–5260). In ST terms Apple north = ST east, Apple east = ST south: **the Apple map is
the ST map turned a quarter**, and the texts follow each version's own compass (scrolls: Apple SOUTH = ST WEST, NORTH =
EAST, WEST = NORTH). The Black castle's eight entrances in OUT28 lead to the same eight DNG7 start cells, shifted by a
quarter turn between the versions. D2-046.

### 3.7 Dungeons and messages: `DNG1`–`DNG8` (6140 B, side B) `[C]`

- 0–1739: the Phantasie I level format, as the ST's `DNGn` (grid 33 × 38 at `LA + 33·CY + CX`, monster bytes,
  events at 1270 = `3320 − LA` (line 8200), scripts at 1450 = `3500 − LA` (8240), level number at 1255 (455)).
  Byte-identical to the ST's except two cells (section 5).
- Messages: slot `M` is 40 B at `1700 + 40·M` (`TM = 3750 + 40·M1`, line 8650), printed by CALL 800 to its CR, so a long
  message runs on into the next slots. Menus are part of the text ("1-LEAVE  2-OPEN IT"); the ST keeps them as option
  labels. Ids agree with the ST's MESS slot ids. The text area ends at slot 110; the ST's MESS files carry extra
  records past their last referenced id (junk).
- `DNGSAV` / `DNGCHK` are save state (DNGOUT2), never read by the extractor.

### 3.8 Scrolls: `SCROLLL1`–`SCROLLL20` (side A), `SCROLLL0` (side B) `[C]`

1000-B dumps of the 40 × 24 text screen ($400, interleaved rows), loaded straight onto the screen (TOWN2 8010). Item
i (121–140) shows `SCROLLL(i − 120)`, then the next file while row 22 column 0 is `!` (TOWN2 8020, `PEEK(1872) = 161`):
scroll A (121) = files 1–3, 132 = 12–13, 134 = 14–16; there is no `SCROLLL17` (item 137, as on the ST). `SCROLLL0` is
Pluto's speech after he is beaten (DUNGON2 4300; on the ST a `strings/en.json` line).

### 3.9 Save state and other files (not read by the extractor)

`CHAR2` (40 × 170-B records at 8225, the Guild + party), `TWN2n`, `DNGSAV`, `DNGCHK`, `BACKFILE2` (backup disk).
`SSI.INIT2` (RDOS init, cracked), `RDOS 2.1` / `SYSTEMBOOT` (DOS). Record layout of `CHAR2` and the I ↔ II transfer:
`reverse-engineering/cross-version/wand-and-transfer.md` (Apple column).

## 4. Key files (importer detection)

`extracted/key-files.json` has the sha1 of each file the extract reads (side A: PHANTASIE II, TOWN2, OUTDOORS2, ITEM,
TINFO, TF2, SHAPES, ICE DRAGON, INFO; side B: DUNGON2, DNG1, DNG8, SCROLLL0). Played files (`OUTT`, `TWN2n`, `CHAR2`)
are not key files; with `DNGSAV` / `DNGCHK` they are the profile's `mutable_files`, hashed from the Memorial Set's
unplayed disks (s167). Version string: INTRO2 192–193 "VERSION 1.0  2/5/86".

## 5. Apple II vs ST: what the diff finds (s158, reference dump)

`diff_a2_p2_vs_st.py` summary: monsters 1274 / 1280 numbers equal; items 180 / 180 factors; creation 1541 / 1545
cells; towns 1845 / 1850 numbers; wilderness 8831 / 8840 cells (fog excluded), features equal except the text args and
OUT28's entrances; dungeon levels 13918 / 13920 bytes. Every number difference is in `DIFFERENCES-II.md` D2-046 to
D2-050 (decided s159: an Apple-only import takes the ST's numbers and keeps the Apple's text; built s160). Text: names, scroll and message wording (D2-050). The message comparison is noisy because the ST
GameData's continuation records hold fragments; read its rows as pointers, not as a list of real differences.
