# Phantasie IV (MSX2) — disk and code format notes (cross-check, 3d.7)

Cross-check platform for IV (rules source is X68000: `../../x68000/phantasie4/`). Plan: `docs/plan/03d-phantasie4-re.md`.
Tags: `[C]` read from the bytes/code, `[~]` likely, `[?]` open. Static reading only; no MSX emulator was run (openMSX is
not installed, and booting the disk needs real MSX2 BIOS + disk ROMs).

Tool: `tools/scripts/msx2_p4_dis.py` (directory, strings, kana search, Z80 linear-sweep disassembly via `z80dis`).

## 1. Images

`../original-images/phantasie4/` (`SOURCES.txt`). TOSEC labels are swapped:

- **Player disk** = TOSEC "Disk 1 of 2 (Scenario Disk)", sha256 `475449b5…5142d597` (not the `[a]` dump). All addresses
  below are on this image unless noted.
- **Scenario disk #1** = TOSEC "Disk 2 of 2 (User Disk)", sha256 `8cade1c3…8c28833`.

## 2. Disk layout `[C]`

- 720 KB, 512-byte sectors. Sector 0 is an MSX boot sector (OEM name `PHANTASIE4`); the FAT area is unused.
- **Directory:** sectors 1–8, 8-byte entries `{u16 id, u16 start sector, u32 size in bytes}`, little-endian. Id `0xFFFF`
  entries 0/1 are the boot loader (sector 9) and the resident loader (sector 10, 8580 B). Entries with size 0 are
  absent on that disk (the scenario disk lists the save ids with size 0). A few ids appear twice (`1008`, `100A`,
  `2001`, `9005`); the bytes agree.
- **Boot:** the boot sector reads the directory (sector 1) to `0x8000`, then directory entry 0 (sector 9) to `0x4000`
  and runs it (DOS `PHYDIO`, `0xFFA7`); that stage loads entry 1 (sector 10) to `0xB58B` and jumps there. The resident code keeps a jump table at `0xB58B`; `0xB58E` loads a segment by id (`LD IX,id; CALL 0xB58E`).
- **Code segments** (`0x0001`, `0x0800`, `0x1000`–`0x100D`, `0x2000`/`0x2001`, `0x9xxx`, …) load at **`0x0100`** and
  start with `JP`. Each one links its own copy of the C runtime (frame helpers, long/float library), so runtime
  addresses differ per segment. Globals live at about `0x7000`–`0x81FF` `[~]` (from the addresses seen).
- **Only one code build is known:** the TOSEC `[a]` player disk differs from this one only in sectors 284–311 (SOURCES.txt),
  which are directory ids `8000`–`8010`, the save set. So `[a]` is the same code with a save game on it.
- **Data ids** (player disk): `8000`–`800B` the save set in pairs, e.g. `8002`/`8003` = guild (1900 B = 10 × 190);
  `8006`/`8007` and **`8200` = the Hall of Glory, 11 × 27 B** (sector 853, offset `0x6AA00`; the shipped three are only
  in `8200`); `8300` = the three elementals (3 × 190 B, "Mud El.", "Mist El.", "Air El."); `8400` 5880 B `[?]`.

| Segment | Disk sector / offset | Content (as read so far) |
|---|---|---|
| `1001` | 501 / `0x3EA00`, 19347 B | guild: character creation (ancestry, `_combi`, inheritance, `_get_social`, `_mkdetail`), skill training menu |
| `1006` | 603 / `0x4B600` | character sheet / Hall display (class, race, social-class name tables) |
| `100C` | 757 / `0x5EA00`, 12763 B | training hall (level-up: `_trang` + `_calpow` equivalents) |
| `100D` | 782 / `0x61C00` | magic training hall |

## 3. Code conventions `[C]`

- Compiled C, Z80. First three arguments in HL, DE, BC, the rest on the stack (caller pops); result in HL.
- Frame helpers with an inline 2-byte operand after the `CALL` (seg `1001`: `0x4C49` store HL / `0x4C86` load HL at
  `SP+n−2`; seg `100C`: `0x32CE` load). Signed compares: seg `1001` `0x4B97` (carry iff HL < BC), `0x4BA5` (HL < DE).
- `rand(n)` = resident `0xB591` (HL = n; returns 1..n like the X68000 `_rand` `[~]`). 16-bit multiply `0xB606`.
- **Floats:** 10-byte extended format (8-byte mantissa with explicit leading 1, then u16 sign + exponent, bias 0x3FFF).
  Constants follow `CALL 0x306F` inline (seg `100C`). Locals are 4-byte floats; doubles are pushed as 10 bytes.

## 4. Text `[C]`

- MSX character set, one byte per character: hiragana `0x86`–`0x9F` and `0xE0`–`0xFD`, half-width katakana `0xA1`–`0xDF`,
  `0xDE` = dakuten after the base kana. Most game text is kana (e.g. `ふかのうです`, `ざんねんですが あなたの みつけたそせんは`).
- Names of classes, races, social classes, skills, items and the Hall persons are **ASCII English** already on the
  original Japanese disk (e.g. `Lord Wood`, `Filmon`, `Nikademus`; `Peasant` … `Lord` in seg `1006`), which the 2008
  patch keeps.

## 5. Tables found (same values as X68000 unless noted) `[C]`

- **Race list** (seg `1001` `0x432A`): the X68000's 35 races in the same order, `  ?` = 33, Duck = 34.
- **Class list** (seg `1001` `0x41F9`): Thief, Monk, Ranger, Fighter, Priest, Wizard, S. Warrior, Gargoyle, Sage, Lord.
- **Social classes** (seg `1006` `0x258D`): Peasant, Laborer, Craftsman, Noble, Super Hero, Dark Lord, Black Lord,
  Royalty, Outcast, Lord (0–9, the X68000 order).
- **Hall record (27 B, seg `8200`)**: the X68000 28-byte record (`../../x68000/phantasie4/FORMAT.md` section 6.3) without its
  unused last byte. The shipped three are **byte-identical** to X68000 `FAME` bytes 0–26: Lord Wood (Human Ranger, L16,
  Royalty, 18/17/16/16/17, deeds 0), Filmon (Human Sage, L19, Noble, 12/21/14/13/15), Nikademus (race 33 `  ?`, Lord,
  L19, Black Lord) in slot 2. The creation code uses +0E (social) and +18 (descendants, +1 while < 100) as on X68000.
- **Training town factor** (seg `100C` `0x2A1E`, by map number `(0x8005)`): 80 × 10, 100 × 3, 120 × 9 (X68000 `_ctab`).

## 6. Functions read (seg, load base `0x0100`)

| Seg:addr | X68000 twin | Note |
|---|---|---|
| `1001:0x0355` | `_make1` | sets `(0x8002)` = −1 (no Hall ancestor 1) |
| `1001:0x10FC` → `0x120B(i, 0)` | `_make3` / `_fame2buf(i, 0)` | Hall person as ancestor 1; `0x1223` stores the slot in `(0x8002)` |
| `1001:0x1335` | `_make5` | S/D/H menu: S → `0x1507(50, 0)`, D → `0x1507(25, 1)`, H → `0x1821` |
| `1001:0x1507` | `_mopta` | S/D race roll; **`0x1621`: if `(0x8002)` = 2 → `0x17CC` "ふかのうです" (Not possible)**; `0x1641` "Sorry, the ancestor you found was …" |
| `1001:0x171E` | `_combi` | 5×5 word table at `0x16EC`; packed-pair compares at `0x1778` (`65 70 97 B8 A1 CA B0 C4 ED` → races 24–32) |
| `1001:0x17CC` | `_err_fame` | "ふかのうです" |
| `1001:0x1821` | `_moptb` | **`0x1822`: if `(0x8002)` = 2 → Not possible**; then combine < 0, same person, both in slots 0–2 → Not possible; else `0x120B(i, 1)` |
| `1001:0x1A5A` | `_get_social` | rand(75), rand(100) for races 0–4; thresholds 29 / 59 / 89 |
| `1001:0x1AE6` | `_mkdetail` | `0x1C05` social = `_get_social(race)`; loop `0x1C11`: per ancestor of kind 1 (Hall): descendants +1 (< 100), Hall social 4 → record +1A = 7, 5 → 8 (`0x1C95`–`0x1CD7`), Divine Aid via `0x21CD` |
| `100C:0x0176`–`0x07F8` | `_trang` | level-up training (spec: `../../x68000/phantasie4/logic-specs/training.md`) |
| `100C:0x0A3C` | `_calpow` | level-up; gold += social^social × level × 30 (`0x0A65`–`0x0ADE`) |
