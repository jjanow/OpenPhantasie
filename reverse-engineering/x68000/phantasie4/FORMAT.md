# Phantasie IV (X68000) — format notes

Phase 09 T6.2. Plan and status: `docs/plan/03d-phantasie4-re.md`. Files: `unpacked/README.md`. Tags: `[C]` confirmed
(two sources, or structure that cannot be read another way), `[~]` likely, `[?]` unknown.

## 1. Address conventions

| Convention | Formula | Used by |
|---|---|---|
| **P4.X image address** | file offset − **0x40** (the `HU` header) | `x68k_x.py`, `extracted/*.json`, string ids `P4.X:0x…` |
| Text segment | image `0x00000`–`0x3d65d` (251,486 B) | symbol kind `text` |
| Data segment | image `0x3d65e`–`0x4a3c5` (52,584 B) | symbol kind `data`, all string literals |
| BSS | `0x4a3c6`, 287,654 B | symbols `bss` / `common` (game state: `_par`, `_fame`, `_anc`, `_item`, `_mons`, …) |
| Data files (`DATM`, `ELMS`, `FAME`, `O_0xx`, `SCR?`) | file offset | `extracted/*.json`, string ids `O_001:0x…` |

## 2. Container (3d.1)

See `unpacked/README.md`. `.dim` = 256-byte DIFC header + 154 tracks × 8 × 1024 B `[C]`. Human68k FAT12 with a big-endian
BPB `[C]`; FAT copy 2 stale `[C]`; deleted pre-release test saves on both masters, recovered into
`unpacked/deleted-disk{1,2}/` `[C]`/`[~]`.

## 3. P4.X (3d.2)

- **Format `[C]`:** Human68k `HU` executable. Header: entry `0x3cc28` (XC C runtime start-up, linked last), text
  `0x3d65e`, data `0xcd68`, bss `0x463a6`, relocation table `0x56e4` B (11,122 sites, all 32-bit), **symbol table
  `0x39c2` B = 1015 symbols** (705 text, 54 data, 14 bss, 217 common, 25 linker/absolute). So every function and global
  has its original C name (`_inn`, `_mage`, `_heal`, `_load`, `_save`, `_monexe`, `_trang`, …) → `extracted/p4x-symbols.json`.
- **Relocations `[C]`:** 16-bit deltas from the previous site, `0x0001` escapes to a 32-bit delta. Checked on `_race`
  (35 sites 4 B apart) and `_magic` (65). Every relocated longword is a pointer, so a string whose first byte is a
  relocation target is a confirmed string, and the site names the function that uses it (`strings.json` `refs`).
- **Not packed `[~]`; a start-up original-disk check** (`_xprtck` / `_xchkpgm`: IOCS read of cylinder 76 head 1 sector 1 on drive 0, `FUNCTIONS.md` §2; s82) `[~]`. Compiler: Sharp/Hudson XC (Human68k libc: `_fopen`, `__fmtout`, `_ecvt`, …).
  OS calls are `$FFxx` DOS words and floating point / long arithmetic is FLOAT2's line-F `$FExx` calls. Decoded at
  instruction starts (s79) there are 2,304 line-F words: 2,216 FPACK calls (`$FEE0` `__CLMUL` 1,531 the most common)
  and 88 `$FFxx` words — the real DOS calls among them all sit inside the XC library, the rest are data in `_wfinit` /
  `_timezone` (`FUNCTIONS.md` §3); the earlier counts (~10,600 / ~2,500) were raw
  word matches. capstone does not decode them: `tools/scripts/x68k_p4_ghidra_prep.py` patches them for Ghidra.
- **Version string:** `Phantasie IV Ver.1.02`; credits `原作 W.D.Wood`, `制作 STARCRAFT Inc.`, K.Yamaguchi, T.Sato,
  Y.Asakura, M.Togashi `[C]`.
- **Text encoding `[C]`:** Shift-JIS (cp932). Names of races, classes, items, spells, planes, origins and monsters are
  English in the binary; menus, messages and story text are Japanese (full-width and half-width katakana mixed).
- **Files the program opens** (strings, `[C]`): `P4.SND`, `VDATA`, `icon.pix`, `p4mon.pix`, `mout`, `fame`, the `*.PKH`
  pictures by name, save set `fames / goods / guils / pmemb / party / contr` + `.0`–`.3`, disk check `id2` on `A:\` / `B:\`.

## 4. Master tables (3d.3) → `extracted/`

Pointer tables in the data segment (`extracted/names.json`, `extract_x68k_p4_tables.py`):

| Symbol | n | Content |
|---|---|---|
| `_plname` | 7 | planes: Material Plane -Scandor Area-, Astral Plane, Netherworld Level 5–8, Netherworld `[C]` |
| `_origin` | 7 | Gelnor, Ferronrah, Scandor, Fishing Village, Pookny Islands, Skull Islands, Plane of Darkness `[C]` |
| `_towns` | 22 | 0 empty + 21 towns, Japanese `[C]` |
| `_inns` / `_dungeons` / `_dnames` | 7 / 22 / 22 | inn names, location names (dungeon index = `O_0xx` number `[~]`) |
| `_race` | 35 | 18 base (Human … Spriggan), 15 hybrids (Half Dwarf … Lizataur), `  ?`, Duck `[C]` |
| `_class` | 10 | Thief, Monk, Ranger, Fighter, Priest, Wizard, Shadow Warrior, Gargoyle, Sage, Lord `[C]` |
| `_social` | 10 | Peasant, Laborer, Craftsman, Noble, Super Hero, Dark Lord, Black Lord, Royalty, Outcast, Lord `[C]` |
| `_abil` | 6 | Strength, Intelligence, Dexterity, Constituion [sic], Charisma, Luck `[C]` |
| `_skill` | 11 | Attack, Parry, Spot Item, Spot Trap, Disarm, Listen, Fire Bow, Pick Lock, Swim, Navigation, Throwing `[C]` |
| `_body` / `_hurt` | 6 / 4 | hit locations, wound states (Japanese) `[C]` |
| `_itm` | 157 | item names `[C]` |
| `_magic` | 65 | spell names (Healing 1 … Super Transport); `*` = party-wide `[~]` |

- **Items `_idat` `[C]` layout, 6 B BE per item:** `b0` icon glyph, `b1` DEF/ATT value **`[C]` (equals the item
  catalogue for all 84 equipment items 0x01–0x54)**, `b2` strength/dexterity requirement `[C]`, `b3` signed movement %
  (body armour) `[C]`, `u16` price `[C]`. Decoded in §6.5. `_gen_itm` (32 B before it): the random-item list, §6.5.
- **Monsters `DATM` `[C]`:** 84 × 70 B, fields in §6.4. Includes Lord Castlebaum, Kor, Lord Keogh, the four Deaths, Duck,
  Warrior Duck, Arathamus.
- **Elementals `ELMS` `[C]`:** 3 × 190 B **character records** (§6.2), loaded into party slot 6 by Summon El.
- **Hall of Glory `FAME` `[C]`:** 11 × 28 B, slots 0–2 used: Lord Wood, Filmon, Nikademus; full layout in §6.3. Wood
  18/17/16/16/17 = the "Human Ranger" ancestor of the MSX Magazine 1991-03 sample (`09-trilogy-iv-parentage.md` §1), so that
  sample used Lord Wood from the Hall `[C]`.
- **`TABL`** (2130 B) `[C]`: six class/race tables for creation and level-up, §6.6 (`extracted/creation-tables.json`);
  **`VDATA`** (6609 B) = the 9 sound effects, OKI ADPCM, s151 `[C]` (section 6.13); **`MOUT`** overworld map (read in 700-byte records by `_omap_load`) `[~]`; `*.PKH` compressed pictures
  (`X68000` signature) `[?]`; `*.PIX` raw graphics `[?]`.

## 5. Text (T6.1) → `extracted/text/strings.json`

- `P4.X` data segment: NUL-terminated strings; 818 Japanese rows, 734 of them relocation-confirmed with `refs`.
- `O_001`–`O_021`: event bytecode with inline NUL-terminated text, `$` = line break `[~]` (the opcodes are not decoded).
  `O_0xx` = location `_dungeons[xx]` `[~]` (O_001 = Town Hall …, O_021 = Skull Fortress).
- `SCR1`–`SCR5`: quest scrolls, fixed 35-byte lines (17 full-width characters + NUL) `[C]`.
- Rows without Japanese are `source-english`: they include real UI words and some data bytes that look like ASCII
  (not filtered). Rows marked `not-text` are data bytes that decode as Shift-JIS (float constants).

## 6. Record layouts (3d.5)

Method: static RE of P4.X with its symbol table (Ghidra 68000 decompile of a copy where the FPACK integer calls were
rewritten to 68020 `muls.l`/`divs.l` so the arithmetic shows; float code read from the disassembly). Function names and
addresses: `FUNCTIONS.md`. Offsets are hex, big-endian. "Evidence" names the function that reads or writes the field.
Validated against the four recovered test save sets with `tools/scripts/x68k_p4_saves.py` (§6.8).

**FPACK operand order `[C]`:** for the stack-based double calls (`__CDADD/SUB/MUL/DIV`, `__CDCMP`), the value pushed
**last** is the left operand: `_avg_attr` pushes 2.0, then the sum, and divides (sum / 2); `_frand` pushes 100.0, then
`rand(100)` (rand / 100). Every float formula below is read with that order.

### 6.1 Save set (one slot) `[C]`

`_save` / `_load` (`0x1714` / `0x12fa`). Slot digit `N` = 0 (current game) or 1–3 (`*1:Save` … `*3:Save`).

| File | Bytes | Memory | Content |
|---|---|---|---|
| `goods.N` | 314 | `_item` | shop stock: 157 items × {u8 count, u8 town (`_cmapn`)} (§6.5) |
| `guils.N` | 1900 | `_gmb` | guild roster: 10 character records × 190 (§6.2) |
| `pmemb.N` | 1330 | `_pmb` | party: 7 character records × 190 (6 members + slot 6 = summoned elemental) |
| `fames.N` | 308 | `_fame` | Hall of Glory, 11 × 28 (§6.3); a new game reads `FAME` instead |
| `contr.N` | 6 | `_condat` | settings (§6.7) |
| `party.N` | 792 | `_par` | party state (§6.7) |

- **Where the files go `[C]`:** `_xread`/`_xwrite`/`_xexfile` take a drive number; `_setdir(1)` = `B:\` (or the hard-disk
  directory when `_drivetype` = 0). Every save file uses drive 1, the same drive that `DATM`, `ELMS`, `p4mon.pix`, `MOUT`,
  `*.pix`, `scr*` are read from; drive 0 (`A:`) supplies disk 1's files (`tabl`, `fame`, the `O_0xx` events and the
  disk-1 pictures through `_xunpack(0, …)`). So on a two-drive system the saves are written to **disk 2**, which `HD.BAT` calls the "player disk". `_diskmenu` marks a slot as saved
  when `fames.N` exists on drive 1.
- **New game `[C]`** (`_load(0)`, s106): all buffers cleared (goods, guild, party, Hall, `contr`, `party`, `_anc`), Hall =
  `FAME`, `contr` = {1, 5, 5, 1, BGM flag}; `_par` bytes 0–5 = place 0 (town), map 1, x 0, y 0, block 1, town type 0,
  and byte 0x12 (save slot) = 0. `_init` copies them into `_cnewf` / `_cnewn` / `_cnewx` / `_cnewy` / `_nblock` /
  `__towntyp` with `_cmovf` = 1, so `_game` runs `_town` for **town 1, Pendragon** (manual p. 6: "the starting town of the
  game, the town of Pendragon"), with the entry steps (`_townent`, `_welcome`) on an empty party. With no party the town
  menu has only the System icon and one door, G (the Guild) (`_townmenu`: party size `_par`+0C = 0). `_tload` restocks
  the shop only when the party is not empty (`_igen8` after the size test), and the first party is formed inside the
  Guild, so a new game's first party finds the Armory empty until the next town entry `[C]`.
- **`_anc` (80 B, not saved directly) `[C]`:** 20 × 4 B, index = guild slot × 2 + ancestor number; {social, race,
  class, 0} of each "new" ancestor (§6.2 +22). `_save` copies them into the owner's record at +B4/+B5/+BA (ancestor 1) and
  +BB/+BC/+BD (ancestor 2); `_load` copies them back.

### 6.2 Character record (190 B) — `guils`, `pmemb`, `ELMS` `[C]` size

Same layout in `_gmb[10]`, `_pmb[7]` and the three elementals. Field names from `_page1` (character sheet), `_mkdetail`
(creation), `_calpow` (level-up), `_mage`, `_equip`, `_cal_user`.

| Off | Type | Field | Tag | Evidence |
|---|---|---|---|---|
| 00 | u8 | own slot index in its table | `[C]` | `_mkdetail` |
| 01 | u8 | link: guild slot + 1 (0 = empty record) | `[C]` | `_mkpos`, `_guild` (`pmb.link − 1` = guild record) |
| 04 | u32 | experience | `[C]` | `_page1` "Exp" |
| 08 | u8 | level | `[C]` | `_page1` "Level", `_calpow` |
| 09 | u8 | "Score" | `[~]` | `_page1` |
| 0A | u8 | condition: 0 Dead, 1 Okay, 2 Sleep, … (strings at `0x409ea`) | `[C]` | `_page1`, `_mkdetail` sets 1 |
| 0C | u16 | wounds: six body parts × 2-bit level (0–3), packed (`_pa`); level 3 is never healed by the inn | `[C]` | `_heal`, `_setpc` (logic-specs/towns.md §4) |
| 0E | char[10] | name (NUL-padded) | `[C]` | `_page1`, `_mkdetail` |
| 18 | u8 | class (`_class`) | `[C]` | `_page1` |
| 19 | u8 | race (`_race`) | `[C]` | `_page1` |
| 1A | u8 | social class (`_social`) | `[C]` | `_page1` "Back Ground" |
| 1B | u8 | current town (`_cmapn`): set at creation, on every town load and when joining the party (+4/26 yr age if it differs) | `[C]` | `_mkdetail`, `_tload`, `_party` |
| 1C | u8 | origin (`_origin`) — derived from `_cmapn` | `[C]` | `_page1` "from", `_mkdetail` |
| 1E | u16 | age in 1/26 years (age shown = value / 26) | `[C]` | `_page1` "Age", `_mkdetail` formula (logic-specs/ancestry.md §7) |
| 20 | u8 | aging factor F (Luck scaled by race, cap 255): declines at age/256 ≥ 0.7 F, dies at ≥ F | `[C]` | `_mkdetail`, `_townent` (logic-specs/towns.md §2.2) |
| 21 | u8 | "has grown older" flag | `[C]` | `_townent` |
| 22 | u8 ×2 | ancestor 1: kind, index | `[C]` | `_page1`, `_set_anc_social` |
| 24 | u8 ×2 | ancestor 2: kind, index | `[C]` | kind 0 = none ("Unknown"), 1 = Hall person (index = Hall slot), 2 = new ancestor (index into `_anc`) |
| 26 | u16 | **bank account** (shown as "Gold"; manual p. 29 銀行預金, excludes party gold) | `[C]` | `_page1`; `_mkdetail` sets social × 256; `_calpow` adds on level-up; `_deposit` / `_withdraw` |
| 28 | u8 | set to 1 at creation | `[?]` | `_mkdetail` |
| 29 | u8 | combat movement base = race + class table (`_moverate`) | `[~]` | `_mkdetail`, `_movlost`/`_baserate` |
| 2A–2E | u8 ×5 | set to 2, 0, 2, 3, 3 at creation (+2B becomes 1 later) | `[?]` | `_mkdetail` |
| 2F–34 | u8 ×6 | Strength, Intelligence, Dexterity, Constitution, Charisma, Luck | `[C]` | `_page1` "(Abilities)" |
| 35 | u8 | combat defence D = trunc(Parry / 8) + 10 × flier (+B8) + 3 × base MV + Dex (+ trunc(shield value / 6)), set by `_set1` at combat start; monsters subtract it from their hit chance; also the trap-dodge roll (`_dtrap`: rand(100) < +35); scaled with the abilities by aging | `[C]` (s82) | `_set1`, `combat.md` §1, `skills.md` |
| 36 | u8 | magic level from `TABL` (+ levels above 14) | `[~]` | `_calpow` |
| 37 / 38 | u8 | magic points max / current | `[C]` | `_page1`; `_mkdetail`: max = trunc(0.75 × +36) |
| 39 / 3A | u8 | hit points max / current | `[C]` | `_page1`; `_calpow` |
| 3B | u8 | melee damage bonus = trunc(trunc(Str / 6) + \|class − 3.5\| × level / 11), set on leaving town | `[~]` | `_exittown` (combat.md §1) |
| 3C–51 | u8 ×2 ×11 | skills (`_skill` order): {value, ability bonus} | `[C]` pairs, `[~]` names | `_calsk`, `_calpow`, `_dsp_skill` |
| 52–5A | u8 ×9 | carried items (item ids) | `[C]` | `_page1`, `_equip`; creation gives Darts (79) unless Shadow Warrior |
| 5B–63 | u8 ×9 | charges of the carried items (rings 129/131: 20, refilled at the inn) | `[C]` | `_buyitem`, `_inn` |
| 64–68 | u8 ×5 | equipped: shield, armour, weapon, bow, thrown | `[C]` | `_page1` labels at `0x409f6`, `_equip`, `_baserate` |
| 69 / 6A / 6B | u8 | thrown-weapon / bow / melee-weapon value of the best usable item (`_equip`); +6A → +6D and +6B (+ +3B) → +6E at combat start; +69 is only displayed (thrown damage uses the bow value) | `[C]` (s82) | `_equip` `0x9024`, `_set1` (`combat.md` §1) |
| 6D / 6E | u8 | combat: missile damage Wr / melee damage Wm (+6B + +3B) | `[C]` | `_set1`, `_chatkd`, `_critical` use |
| 70 / 71 | u8 | combat: melee hit Hm (= skill 0 Attack, +3C) / missile hit Hr (= skill 6 Fire Bow, +48) | `[C]` | `_set1`, `_chatkp` |
| 72 | u8 | inn MP-growth flag (cleared by the inn, which may then raise +37) | `[C]` | `_inn` |
| 73–B3 | u8 ×65 | spell state per `_magic` index: 1 = known, ≥2 = may be learned | `[C]` | `_mage`, `_calpow` |
| B1 | (= spell 62) | Divine Aid known: set from Hall ancestors who completed a game | `[C]` | `_mkdetail` → `_chkwon` |
| B4, B5, BA | u8 | ancestor 1 snapshot (social, race, class) when kind = 2 | `[C]` | `_save`/`_load` |
| B6 / B7 | u8 | shield / armour value of the best usable item (`_equip`); +B7 → armour A (+6F) at combat start | `[C]` (s82) | `_equip`, `_set1` |
| B8 | u8 | 1 for Pixie, Sprite, Gargoyle, Sprixie (fliers) | `[~]` | `_mkdetail` |
| BB, BC, BD | u8 | ancestor 2 snapshot (social, race, class) when kind = 2 | `[C]` | `_save`/`_load` |

Unlisted bytes are `[?]`. The creation work buffer (22 B, pointer `_wkp`) is {name[10], race, class, 5 abilities, move
base, anc1 kind, anc1 index, anc2 kind, anc2 index}; `_tmp` holds three of them (ancestor 1, ancestor 2, spare) `[C]`.
`_wkp` itself is never assigned anywhere in P4.X (no relocation writes it), so as a BSS zero it points at address 0;
the game runs in supervisor mode (`_B_SUPER(0)`), so the buffer would sit on the low exception vectors `[~]` — an
original bug with no effect on the rules.

### 6.3 Hall of Glory record (28 B) — `FAME`, `fames.N` `[C]`

`_dsp_hinfo` (the Hall screen: labels at `0x41832`), `_fame2buf`, `_mcopy` (III import), `_mkdetail`, `_chkwon`.

| Off | Type | Field | Notes |
|---|---|---|---|
| 00 | u8 | used | |
| 01 | char[10] | name | |
| 0B | u8 | race | shown as `  ?` when > 14 |
| 0C | u8 | class | Filmon 8 (Sage), Nikademus 9 (Lord) |
| 0D | u8 | level | Wood 16, Filmon 19, Nikademus 19 |
| 0E | u8 | social class | Wood 7 Royalty, Filmon 3 Noble, Nikademus 6 Black Lord |
| 0F | u8 | origin | slots 0–2: `_origin`; slots 3–10: list at `0x4187a` (Gelnor, Ferronrah, Netherworld, Scandor, Plane of Light, Plane of Darkness) |
| 10–14 | s8 ×5 | Str, Int, Dex, Con, Cha | no Luck |
| 15 / 16 / 17 | u8 | Phantasie I / II / III result: 0 = not played, 1–99 = played, ≥ 100 = completed | shown "Completed" / "Not completed" / blank, slots ≥ 3 only |
| 18 | s8 | number of descendants (< 0 shown `?`) | +1 each time used as an ancestor, capped at 99 (`_mkdetail`) |
| 19 | u8 | deeds line index (slots 0–2 only; slots ≥ 3 compute it, `logic-specs/ancestry.md` §8) | Wood 0, Filmon 1, Nikademus 2 |
| 1A | u8 | status: 0 Dead, 1 Alive | slot 0 (Wood) is recomputed on display (`logic-specs/ancestry.md` §8); `_mcopy` sets 1 when the III record's byte 0x12 ≠ 0 |
| 1B | u8 | unused | `[?]` |

- **Who fills the Hall `[C]`:** only `_mcopy` (III transfer) writes slots; no code writes an IV character into the Hall.
  Slots 0–2 = the shipped three; slots 3–10 = up to eight imports.
- **III import `[C]`** (`_p3disk`, `_mtrans`, `_mcopy`): reads III's player disk as raw 256-byte sectors (`_rdiskv` →
  IOCS `B_READ`; 16 sectors per track side, i.e. a 2D-format disk); sector 0x120 must hold `"Pendragon"` at byte 230. Then
  15 sectors from sector 0x20 = 21 III character records of 160 B. Field map (III offset → Hall): name 0x82 (10 B),
  race 0x96, class 0x8C, level 0x35, social 0x7F, origin 0x26, abilities 0x01–0x05, I result 0x28, II result 0x08,
  III result 0x79, alive flag ← (0x12 ≠ 0); descendants = 0. The III record layout itself is `[?]` beyond these bytes.
  The screens and the remaster's mapping: `logic-specs/ancestry.md` section 8.2 (built s148).

### 6.4 Monster record `DATM` (70 B) `[C]` size

`_enc_mons` reads record N with `_xrndint(1,"datm",70)`; the 18-slot buffer is `_mns`. Values in `extracted/monsters.json`
(raw) and the dump in §6.8.

| Off | Type | Field | Tag | Evidence |
|---|---|---|---|---|
| 00 | char[20] | name | `[C]` | |
| 14 | u8 | max group size n: rand(n), but n = the party's average level (`_par`+0B) when n > +0B + 1 (s98; earlier read as "party size + 1") | `[C]` | `_enc_mons` |
| 16 | s16 | attack value: attacks per turn n = max(1, trunc(+16 / 115 + 0.7)), hit chance trunc(+16 / n) − D | `[C]` (s82) | `_monexe` (`combat.md` §4) |
| 18 | s16 | damage value: d = trunc((rand(100) / 100)^0.5 × +18) − armour (32767 for two Deaths) | `[C]` (s82) | `_monexe` (`combat.md` §4) |
| 1A | s16 | armour: subtracted from party damage (halved for arrows / thrown weapons); Protection-type spells raise it | `[C]` | `_chatkd` (logic-specs/combat.md §3.2), `_mmagic` |
| 1C | s16 | defence: subtracted from the party's hit chance (÷ 4 for arrows / thrown weapons) | `[C]` | `_chatkp` (combat.md §3.1), `_pmagic1` |
| 1E | s16 | level (`_emlevel` = the mean of the groups' levels, min 1; s98) | `[C]` | `_enc_mons` |
| 20 | u32 | experience per monster | `[C]` | `_enc_end`, `_mdamage` |
| 24 | u8 | magic resistance level: spell works if rand(100) ≤ (caster +37 − this) × 5 + 50 | `[~]` | `_pmagic1/2` |
| 26 | s16 | hit-point base; group cap = trunc(1.8 × base) | `[C]` | `_enc_mons` (per-monster formula `[~]`, see note) |
| 28 | s16 | "nice" (summed into `_emnice`; −10 for undead, −1000 for Deaths) | `[?]` | `_enc_mons` |
| 2A | u32 | gold per monster | `[C]` | `_enc_mons` |
| 2E | s16 | spell-casting ability | `[~]` | `_monexe`, `_pmagic1` |
| 30, 31 | u8 | spell / special attack ids (66–69 = breath / touch specials) | `[?]` | `_monexe` |
| 32 | s16 | `[?]` (non-zero only for casters) | | |
| 34 | s16 | % chance that another group follows | `[C]` | `_enc_mons` |
| 36, 37 | u8 | the two possible next-group monster ids (picked by rand(2)) | `[C]` | `_enc_mons` |
| 38 | u8 | movement | `[C]` | `_enc_mons`, `_mcalmove` |
| 3A, 3C, 3E | s16 | three treasure item ids (**outdoor** encounters only; s82: `_cmapf` 1 = outdoors) | `[C]` | `_enc_mons` (`_cmapf` = 1), `logic-specs/skills.md` |
| 40 | u8 | creature type 1–5 | `[?]` | |
| 41 | u8 | flier (set on sprites, bees, dragons, devils, ghosts) | `[~]` | `_mmovlost` |
| 42 | u8 | body shape for hit-location text: 1 normal, 2 body only, 3 snake, 4 winged | `[C]` | `_mlget` |
| 43 | u8 | damage-type flag (giants, Troll, Dragon King, Giant Ghost) | `[?]` | `_chdtyp` |
| 44 | u8 | undead/demon flag (exactly the undead and demons) | `[~]` | `_try_cast`, `_pmagic2` |

Per-monster HP `[C]` (CPU check, s82): `_enc_mons` computes trunc(base × ((rand(10) − 1) + 5.0 / 10.0) + 0.5) and then
caps it at trunc(1.8 × base). Run with the real P4.X code and FLOAT2's own line-F handler
(`tools/scripts/x68k_p4_cpu_harness.py monhp`, every roll 1–10 for 12 bases, 0 mismatches): roll 1 gives
trunc(0.5 × base + 0.5), roll 2 trunc(1.5 × base + 0.5), rolls 3–10 the cap trunc(1.8 × base) — so 80 % of X68000
monsters have 1.8 × base HP (base 10: 5, 15, then 18 × 8). The constants are 5.0 and 10.0 (doubles at `0x441BC` /
`0x441C4`), cap 1.8 (`0x441B4`), rounding 0.5 (`0x441CC`). **MSX2 differs `[C]`** (seg `4000` `0x051D`–`0x05E1`, run in a
Z80 emulator with rand stubbed, `tools/scripts/msx2_p4_z80_harness.py hp`): it pushes 10, 5 and rand(10) − 1, adds before dividing, and gets
trunc(base × (rand(10) + 4) / 10 + 0.5) = 0.5 … 1.4 × base in steps of 0.1 (base 10: 5, 6, … 14), so its 1.8 × cap
never bites. The X68000 build looks like a precedence slip of the MSX2 rule; the manual says nothing about monster HP.
PC-98 not checked (its `DATM` is the same size; code not read).

### 6.5 Items: `_idat` (6 B × 157), `_gen_itm`, `goods` `[C]`

| Off | Field | Evidence |
|---|---|---|
| b0 | icon glyph: font 2's character b0 + 0x20, 0–49 (section 6.9 "Item lists") | `_itemlist`, `_dvitmlst`, `_tritmlst` |
| b1 | DEF/ATT value | catalogue (s78) |
| b2 | requirement: shields/armour (1–0x1B): b2 ≤ Str; weapons (0x1C–0x3F): b2 ≤ 2 × Str + Dex; bows/thrown (0x40–0x54): b2 ≤ Str + Dex; items ≥ 0x55 always usable | `_cal_user`, `_itemlist` ("N-U" = not usable) |
| b3 | movement modifier in % (armour): move = trunc(m + m × b3 / 100 + 0.5), +2 with Shoes of Speed (item 130) | `_baserate` |
| u16 @4 | price; selling pays price / 4 + 1 (party gold capped at 65535) | `_sell`, `_buyitem` |

- `_gen_itm` (`0x3d65e`, 32 B): random-item list; `_igen` picks entry rand(2 × min(average party level, 12) + 5)
  (1-based) `[C]`. `goods` = for each of the 157 items {stock count, town where it is stocked}; `_sell` adds one to
  the stock in the current town; `_igen8` adds 8 random items to a town's shop.

### 6.6 `TABL` (2130 B, disk 1) `[C]`

Pointer `_tbl` = `_dtbl` (`_sysinit`). The sections tile the file exactly (0x852 B), which confirms the sizes; only player
classes 0–7 (Thief … Gargoyle) have rows. Dump: `extracted/creation-tables.json` (`extract_x68k_p4_creation.py`).

| Offset | Size | Content | Used by |
|---|---|---|---|
| 0x000 | 8 × 15 | HP growth per class × level (0–14): new max HP = Σ of n × rand(3), n = (value × (Con + 5)) >> 4; never lower than old max + 1; cap 255 | `_calpow` |
| 0x078 | 34 × 5 s8 | race ability modifiers (Str, Int, Dex, Con, Cha) | `_mdice`, `_sub_bonus` |
| 0x122 | 8 × 5 s8 | class ability modifiers | `_mdice`, `_sub_bonus` |
| 0x14A | 8 × 15 | magic level (+36) per class × level | `_calpow` |
| 0x1C2 | 11 × 8 × 15 | skill growth per skill × class × level | `_calsk` |
| 0x6EA | 8 × 15 × 3 | spells that become learnable at each level (1-based `_magic` ids) | `_calpow` |

Hall persons of class 8 (Sage: Filmon) or 9 (Lord: Nikademus) have no class row: `_sub_bonus` then reads the first
bytes of the 0x14A table as their "class modifiers" (Sage 6, 6, 7, 7, 8). `logic-specs/ancestry.md` §5.

### 6.7 `party` (792 B) and `contr` (6 B)

`party` (`_par`) `[~]` unless marked:

| Off | Field |
|---|---|
| 00–05 | map kind (`_cmapf`: 0 town / 1 outdoors / 2 dungeon `[C]`, s82: `_game`, `_odungeon`, `_tele`; earlier read as 1 dungeon / 2 outdoors), map number, x, y, block, town type `[C]` (`_save`, `_load`) |
| 06–09 | ship position / state (`_oship_mark`, `_oload`) |
| 0C | number of party members `[C]` |
| 0D | save slot `[C]` (matches the file suffix in all four test sets) |
| 14 | summoned elemental index (`_pmagic2`) |
| 15 | Levitation flag: set by casting Levitation outside combat (`_pmagic3`), cleared by `_encount` at the end of a fight; while set, no dungeon water damage `[C]` (s82; earlier read as daytime / light — the hour is `__daytime`) |
| 16 | u16 time counter: `_dayout(n)` adds (n + `__daytime`) / 24; town entry adds counter / 0x700 to every age and clears it `[C]`; the counter counts **days** (`__daytime` holds the hour, s82 `[C]`); why the age step divides by 0x700 `[?]` |
| 18 | u32 party experience pool, stored as XP ÷ 10 at combat end (`_esub01`), shown × 10 and shared out at the town gate (`_gatebank`) `[C]` (s81; earlier read as a bank balance) |
| 1C | u32 party gold `[C]` (`_mage`, `_sell`, `_bank`) |
| 20–9F / A0–11F | party pack: 128 item ids / 128 charge bytes (new item gets 20) `[C]` (`_setitm`) |
| 120–15F | story flags (`_exe_ev`, `_ending`: 158–15C ending state, 15E–15F party options) |
| 160–317 | per-location event flags: 22 maps × 20 B, index `_cmapn` × 20 `[C]` (size fits exactly) |

`contr` (`_condat`) `[~]`: 0 = 1 (set on new game), 1 = message delay (default 5; `_bwait`, `_timelag`), 2 = combat
message speed (default 5; combat code), 3 = sound effects on/off, 4 = music on/off (`_acswitch`), 5 = unused.

### 6.8 Validation against the recovered saves

`tools/scripts/x68k_p4_saves.py DIR SUFFIX` decodes a set. On the four sets (`deleted-disk2/*.1–.3`, `deleted-disk1/*.2`):
names, classes, races, social classes, skills, spells and equipment are all consistent with the layout; the `party` slot
byte equals the file suffix in every set; ages and aging factors match the creation formula including its slot-0 bug
(`logic-specs/ancestry.md` §7). The three `ELMS` records decode cleanly with the same layout (Mud / Mist / Air El.: level 20,
abilities 25, HP 250 / 200 / 150, MP 12 / 16 / 20, Mist knows the healing/protection spells, Air the attack spells),
which confirms ELMS = character records. None of the test characters has ancestors (all kinds 0: they were made with "No" at "descendant of a
mixed race?"), so the inheritance path itself could not be checked against save data. Experience values (0x100000 +
small) and gold 65 000 / 65 535 look like debug edits.

### 6.9 Pictures: `*.PKH`, `p4mon.pix`, other `*.pix`, `IND*.PDT` (3d.8, s82)

Decoder: `tools/scripts/x68k_p4_pkh.py OUTDIR FILE.PKH …` (`--mon p4mon.PIX SOME.PKH` renders the sprite sheet with the
PKH palette). All 56 PKH files decode; OPEN, TOWN_1, TOWN_1AN, SHIP and the sprite sheet were checked by eye.

Screen: `_screen` sets `_CRTMOD(0x110)` = mode 16 (768 × 512, 16 colours) `[C]`. Every picture is 4-bit and goes
through the 16-entry graphic palette (`_GPALET`); X68000 colour words are GRB 5-5-5-1 (G bits 15–11, R 10–6, B 5–1,
intensity bit 0).

**PKH `[C]`** (`_xunpack` / `_xunpack68` → `_xread` into the buffer at `0x82192` → `_scst_stacf` → `_scsvb1`):

| Off | Content |
|---|---|
| 00 | `"X68000"` + 00 00 (not checked by P4.X) |
| 08 | `00 00 00 01` `[?]` (same in all 56 files) |
| 0C | 16 × u16 BE palette (`_cv_pword`), copied into the picture slot and set by `_pal_set_all` on display |
| 2C–33 | zero in all files `[?]` |
| 34 / 36 | u16 BE width / height in pixels |
| 38… | packed pixels to the end of the file |

Packing (`_scsvb1` `0x3A8B0`): control byte c, n = c & 0x7F pixels.
- c & 0x80: one byte b follows; n pixels of value b (`0x3A940`).
- else: bytes follow with two pixels each, high nibble first, until n pixels are written; an odd n uses only the high
  nibble of the last byte (`0x3A972`).
- Each pixel is written as a word; the 16-colour graphics page keeps the low 4 bits. n = 0 would mean 256 (byte
  loop); it does not occur.

Decoding stops at the end of the file, not at width × height. In three files (SHIP, OT04, END3ANI) the last run
overshoots by 44–126 pixels; display copies only width × height (`_scdisp1` / `_scdisp2`, GVRAM row stride 1024
words), so the extra pixels are never shown. All 56 PKH files carry the **same palette** `[C]`.

Picture slots `_scst` (`0x820C8`, 0x32 B each, `_scst_max` in use): +00 id, +02 palette (16 words), +22 / +24 x / y,
+26 / +28 width / height, +2A store address in graphics memory, +2E end address. The first slot is stored at
`0xDA8000` (`0xDC8000` while `__battle`), later slots follow; a store past `0xE00000` prints an error and exits.

Uses: `*_AN.PKH` / `*ANI.PKH` are sheets of animation frames (`_setanim`; cut by the tables in §6.10, s102). SC_A–I
are the Hall of Glory scroll pictures, OT01–23 the outdoor location pictures (104 × 104), END1–3 the endings.

**Placement (s107, T6.4) `[C]`:** every `_xunpack` call names its file and its x, y in the 640 × 400 layout space
(`_cvsx` / `_cvsy` convert them; pictures are never scaled):

| Screen (function) | Picture at (x, y) | Notes |
|---|---|---|
| Town street (`_tload`) | `TOWN_<n>` at (160, 20), n = `_towntyp` + 1 | `_towntyp` = byte table `0x3F70E`[town] = `towns.json` `town_type` (0 Pendragon, 1 Shipwright towns, 2 the rest); `TOWN_<n>AN` sheet to page 1. `_town` clears the area (`_clrtdraw`) before `_townent` / `_welcome`, so the entry prompts (shares, aging, distribution) show no picture |
| Guild menu (`_guildmenu`) | `GUILD` (160, 64) | redrawn each menu pass; `_mlist` (roster, creation N), `_trang`, `_mage` clear it first (`_clrtdraw`: `_fbox` from `_cvsx(148)`, 564 × 512, and `_scclose`) |
| Inn / Bank / Shipwright | `INN` / `BANK` / `SHIP` (160, 32) | `*_AN` sheets at (240, 0) on page 1 |
| Mystic (`_mystic`) | `MYSTIC` (160, 90); after 10 000 + 3000 `_anima` passes `MYSTLK` (160, 90) over its top, then the score in the bubble (§6.10) | `MYSAN` sheet at (240, 0) |
| Hall (`_hall`, `_hprof`, `_htran`) | `HALL_A` (160, 52) + `HALL_B` at (`_cvsx(160)`, `_cvsy(52)` + 202) (`_xunpack68`) | the curtain with the stage under it |
| Hall history (`_hhist`) | `HALL_A` + page i's `SC_A`…`SC_I` (table `0x41962`) at (`_cvsx(160)` + 40, `_cvy(52)` + 24) | the text in a white box (`_wfbox`, window 16, 218, 280 × 78) |
| Scroll (`_use_sc`) | `SCBACK` (160, 12) | then window (160, 32, 312 × 352) and the page text in a box (`_wfbox` 24, 16, 272 × 304) over it |
| Endings (`_endset`, `_banquet`, `_arathamus`) | `_endset`: `_tload(1)` (Pendragon's street) then `END1` (160, 154) over its lower part; `_banquet` `END2`, `_arathamus` `END3` at (160, 32) in window 32 (312 × 352) | order: `ending.md` §1 (banquet before message blocks 4 and 7, `_common_proc` calls it; the cell before block 9) `[C]`; `END1ANI` animates records 36–42, `END2ANI` 43, `END3ANI` 44 (§6.10; pass lengths measured s117: street 7.39, banquet 5.64, cell 5.53 ms) |
| Outdoors (`_omap_load`) | see "Outdoor map" below | |
| Armory, Trade, Distribute, `_detail` | none (`_clrtdraw`) | |

**Outdoor map (s108) `[C]`:** `OT??.PKH` (disk 2, 104 × 104) is a whole 26 × 26 outdoor map at 4 pixels a square. The
file for map n comes from the two-digit table `_omaps` `0x42DA2` (entry n − 1): maps 1–24 → 01 02 03 04 05 06 **06** 08 09
10 **06 06** 13 14 15 16 17 **16 16** 20 21 22 23 **16** (the rapids and deep-river filler maps share OT06 / OT16; there
is no OT07, 11, 12, 18, 19, 24). `_omap_load` draws by the view flag `0x43002` (starts 1; M flips it):
- **1, close-up (default):** `_wcls(32)`, `_whole(32, 56, 52, 208, 208)`, then the map's OT at (160 + 56, 32 + 52) with
  `_xunpack68(…, 1, 0)`: the fifth argument picks `_scst_displays2` → `_scdisd2`, which writes every pixel **2 × 2**, so
  the close-up is 208 × 208 at 8 pixels a square.
- **0, overview:** when `_nblocf` is set, a 3 × 3 block of OT pictures at 1× from (160, 32), cell (i, j) at
  (+104 j, +104 i): 312 × 312. Block maps: `_omaps` `0x42ECC` + `_cblock` × 9 (block 0 = maps 1 2 3 / 6 7 8 / 11 12 13,
  block 1 = 3 4 5 / 8 9 10 / 13 14 15, block 2 = 16–24). With `_cmapn` ≥ 16 (the Netherworld) every cell but the party's own
  map shows map 16's picture (table `0x42EE8`, then "≠ `_cmapn` → 16").
- `_cblock`: `_omove` keeps it while walking, except stepping off the block's outer column (block 1's left column westward
  → 0, block 0's right column eastward → 1; `_oblkofs` `0x13F7E` = the map's cell in the block); `_tele` sets `_mvb`
  `0x43732`[map] (0 0 0 1 1 1 0 0 1 1 1 0 0 1 1 1 2 …), `_opool` 2 / 1 / 0 for its destinations 23 / 9 / 1, `_use_box` 2;
  `_tload` / `_exittown` keep it.
- Party marker `_omark` (`_getxy` `0x1435C`: square size 8 close-up, 4 overview; x = ((cell mod 3)·26 + x)·size + 56·flag +
  160, y likewise + 52·flag + 32): `_fbox` colour 0 square of one map square, then colour 15 one pixel inside `[C]`; the
  extra +56 / +46 in `_omark`'s `_fbox` calls `[~]` (window-relative?). `_oship_mark` draws the ship (look `[?]`).

Pack `GameData/phantasie4/graphics/x68000.json` (`scenes`, screen 640 × 400): extracted with
`tools/scripts/x68k_p4_pkh.py extracted/graphics/town unpacked/disk1/TOWN_{1,2,3}.PKH`, `… graphics/buildings
unpacked/disk1/{GUILD,INN,BANK,SHIP,MYSTIC,MYSTLK}.PKH`, `… graphics/hall unpacked/disk1/{HALL_A,HALL_B,SC_A…SC_I}.PKH`;
s108 `… graphics/outdoor unpacked/disk2/OT*.PKH`, `… graphics/ending unpacked/disk2/END{1,2,3}.PKH`, `… graphics/scroll
unpacked/disk2/SCBACK.PKH` (the same file as disk 1's). END2's top 50 rows are grey / green stripes in the file itself.
The remaster draws a scene 1:1 at the top left with the text beside it (the shared layout, not P4.X's window places).

**`p4mon.pix` `[C]`** (`_hpix`, `_chpix`, `_mopix`; `_xrndint(1,"p4mon.pix",0x1C20)`): 120 records of 0x1C20 B, each 5
frames of 0x5A0 B; a frame is 48 × 48 pixels: a 288-byte mask (48 rows × 3 BE words, MSB = leftmost pixel; bit 1 =
keep the background, bit 0 = clear it, then OR in the pixel), then 1152 bytes of 4-bit pixels, high nibble first.
Records 0–83 are the `DATM` monster types: `_enc_mons` reads record `_emons` (the type id) into `_mopixb` + group × 7200
`[C]`. Party member k (`_enc_char` helper `0x2078C`) reads record **race + 84** into `_chpixb` + k × 7200, except race 34
→ record 81; slot 6 (the elemental, race 33) adds `_par`+14 (0 Mud, 1 Mist, 2 Air) when it is below 3 → 117–119 `[C]`.
The Hall persons (`_hpix` `0xCA5C`) read record = their Hall record's race (`_fame` + 28 n + 0x0B) + 84, without the race-34 exception `[C]`.
**Hall stage (s151, static) `[C]`:** `_hall` (`0xDACC`) opens window `-1` at (160, 52), 312 × 312, unpacks HALL_A at its
origin, loads every used slot's record (`_hpix(1, slot)`), unpacks HALL_B at `_cvsx(160)`, `_cvsy(52)` + 202, then
draws them (`_hpix(0, slot)`); `_hprof` redraws them on return and the III transfer (`_htran`) reloads them. `_hpix`
skips slots 0–2 (Lord Wood, Filmon, Nikademus are painted into HALL_B); for p = slot − 3 it draws frame 0, unmirrored:
p 0–6 with `_chpix(x, y, 5 p, 0)` from `_chpixb` + p × 7200, p 7 (slot 10) with `_mopix(x, y, 0, 0)` from `_mopixb`.
`_chpix` / `_mopix` write GVRAM at 123 184 + 98 414 + 81 920 y + 96 x + [`0x72596`] + [`0x820C0`] (page): with the
2048-byte row that is screen (207 + 48 x, 108 + 40 y); `_hpix` sets `0x72596` = 32 (16 pixels) and (x, y) = (p + 3, 5)
for p 0–2, else 0x5040 (10 rows, 32 pixels) and (p − 3, 6). From HALL_A's screen origin (216, 99): p 0–2 at (151 + 48 p,
209), right after the three painted figures (7, 55, 103); p 3–7 at (23 + 48 (p − 3), 259), all on HALL_B. So a "?" race
(33) shows record 117, the Mud elemental. **Built (s151):** `P4DungeonScreen.HallFigures.cs` draws `p4.sprite.(race +
84)` frame 0 at those places on the `p4.hall` scene for the save's imports (III's transfer and the remaster's I / II
option), before the picture animations (`parity/shots/p4-remaster-hall-figures.png`). Frame use: `_dtb` (`0x44278`) holds (frame, mirrored) pairs, index
facing × 2 + step: facing 0 (0, no) / (1, no); 1 (4, no) / (4, yes); 2 (2, no) / (3, no); 3 (2, yes) / (3, yes) `[C]`. So
frames 0–1 face the viewer, 2–3 face left (mirrored = right), 4 is the back (mirrored for its step). The step pair is
drawn only while a combatant moves (`_obj`'s third argument, phase: `_dtb` index = facing · 2 + (square = the drawn
position) · phase; `_e_drpos` draws phase 1, and with its argument 2 ≠ 0 waits 1000 loop passes and draws phase 0; real
time `[?]`, §6.10 "Timing"); standing figures use
the first pair. The mirror (`_chpix` / `_mopix` argument 4 = 1) writes each row right to left in the same 48 pixels.

**Palette `[C]`:** no dungeon or combat routine sets one. `_GPALET` is reached only from `_pal_set_all` (picture
display, `_scst_display*`) and `_bapal` (`_p4_screen` at start-up, inside `_sysinit`, table `0x3F4FA`, before
`_openning`); `OPEN.PKH` sets the PKH palette right after, and every PKH carries the same one. So all `*.pix` art shows
in the shared PKH palette. (`_apalinit`'s 8-colour table `0x47834` runs only in `_xexit`.)

**Other `*.pix` `[C]`** (s108, T6.4; raw, no header, read whole by `_xread` or by records with `_xrndint` / `_xrndread`).
A masked sprite is w·h/8 mask bytes (one bit per pixel, row after row, MSB = leftmost, 1 = keep the background) followed
by w·h/2 pixel bytes (4-bit, high nibble first); `_ConvData(src, dst, w, n)` turns each mask byte into a long of nibble
masks (`_pix_and_table`: bit 1 → 0xF) in a parallel buffer (`_ypixa` / `_tpixa` / `_mpixa`) so the draw is AND then OR.

| File | Buffer, draw | Sprite | Bytes | Mask | Count | Use |
|---|---|---|---|---|---|---|
| `fl.pix` | `_fpixb` (`0x58194`), `_fpix` | 32 × 32 | 512 | none (opaque) | 44 | dungeon floor tiles (`_pinmap` bits 0–5) |
| `yoko.pix` | `_ypixb`, `_ypix` | 48 × 32 | 960 | yes | 20 | dungeon north-side wall pictures (bits 6–10; 31 = none) |
| `tate.pix` | `_tpixb`, `_tpix` | 16 × 64 | 640 | yes | 6 | dungeon west-side wall pictures (bits 11–13; 7 = none) |
| `mn.pix` | `_mpixb`, `_mpix` | 32 × 32 | 640 | yes | 8 | the party: `_dire` 0–3 (N E S W), + 4 in water |
| `bback.pix` | `_bkpixb`, `_bkpix` | 48 × 40 | 960 | none | 9 | combat ground; one record loaded (`_e_finit`) |
| `bobj.pix` | `_obpixb`, `_obpix` | 48 × 48 | 1440 | yes | 11 | combat obstacles: kind k → record k |
| `fly.pix` | `_flpixb`, `_flpix` | 32 × 32 | 640 | yes | 15 | missiles and spell flashes (`_f2f`, `_shoot`; placed in 8-pixel steps) |
| `icon.pix` | `_icbuf` (`0x7259C`), `_aicon` | 32 × 32 | 640 | yes | 40 | the Command panel's button pictures (s135) |

**`icon.pix` (s135) `[C]`:** `_sysinit` reads it whole into `_icbuf` (`_xread(1, "icon.pix", 0x7259C)`, disk 2). `_aicon(x,
y, n)` (`0x3B3B4`) draws record n (640 B: 128 mask bytes, 1 bit per pixel, bit 1 = keep the background, then 512 pixel
bytes, high nibble first) straight into graphic VRAM; `_xicon` (`0x3B430`) XORs the record's opaque pixels with 0xFFFF
(`_xbtn`'s look of a button under the mouse pointer, s151: a hover look, not a pressed one; "Dungeon waits, …" below). Only `_seticon` calls `_aicon`: a 32 × 32 button (`_setbtn`) with the picture and its
label centred under it in font 1, the hot-key letter in colour 2 ("N:New Game", the Guild's "[original text]" / "[original text]",
the location map's F / P / C / U / E / S column; XEiJ shots s135). 40 records: book, roster, party, speech, … EXIT, Okay
hand, music note, frog (`extracted/graphics/ui/icons/NN.png`, `x68k_p4_pix.py`). The remaster's menus were text lines and
drew none of them until s136 (see "Command panel" below). **Item icons** are not here: they are font 2's 32 × 32 glyphs (section 6.12), `_wputc(win, _idat[item].b0 +
0x20)` in colour 0 (`_itemlist` `0xE012`, `_dvitmlst`, `_tritmlst`), b0 = 0–49 for the 157 items
(`extracted/graphics/ui/items/NN.png`, `x68k_p4_fonts.py`); drawn by the remaster since s151 ("Item lists" below).

**Item lists (s151, static `[C]`; P4.X `_itemlist` `0xDE82`, `_dvitmlst` `0x9904`, `_tritmlst` `0x103B6` / `_init_tl`
`0x10664`, read from the disassembly):** `_wfinit(2)` has exactly three call sites (`0x9A8C`, `0xDFA2`, `0x1049A`), one
in each list routine, so these three are the only places item icons appear: the profile sheet (`_detail`), the found-item
window (`_findit`), the Hall and the equipment table draw none. Every list sits in the item window `_wopen(-1, 160, 32,
312, 352, 1, title)` (window grey, colour 14; `_font(0, 0, 0, 14)` before it: black text, cell boxes grey); coordinates
below are the window's. Each row is **32 pixels high**: the icon `_wputc(win, _idat[item].b0 + 0x20)` in font 2, colour
0, at the row's top (the glyph fills the 32 × 32 cell), the row's text in font 0 at **row top + 8** (centred on the icon).
- **Armory `_itemlist(str, 2 × str + dex, str + dex)`** (`_armory` `0xE552`, title 商　店): `_wfbox(win, 14, 16, 32, 280,
  192)` clears the list; up to **6 rows**, row i at y = 32 + 32 · i: the letter A + i in **colour 2** at (32, y + 8), the
  icon at **(44, y)**, the name (`_itm`) at (80, y + 8) in colour 0, at x 192 the points (`_wdoutc` 4 digits, `_idat`
  b1) or " N-U" (`0x42540`, the requirement not met; ids 1–84 only), at x 240 the price (`_wlocdi` 5 digits, the price
  times `_ctab[town]` / 100, float calls). Headers 品名 / ﾎﾟｲﾝﾄ / 価格 at (80 / 192 / 248, 16), a colour-0 line (32, 31,
  248, 1); the frame `_whole(win, 16, 16, 280, 208)`; each row a button `_setbtn(win, 16, 32 + 32 · i, 280, 32, 'A' +
  i)`. Paging: the scan goes on from the item after the last one listed (1 … 156, wrapping to 1) and stops after 6 in
  stock or a full turn; **MORE** (`0x42583` at (253, 224), a button `_setbtn(win, 245, 224, 51, 16, 'M')` in a `_whole`
  box) lists the next six; the marked member's name at (20, 256); messages at (32, 288).
- **Share out `_dvitmlst(k)`** (`_distribute` `0xA2BA`, title 分　配): **5 rows** of the sorted queue from the item being
  placed (k), row i at y = 32 + 32 · i: **no letter**; the icon at **(16, y)**, the name at (48, y + 8), the points at
  x 208 (`_wlocdc` 3 digits, ids < 0x55), then for each member who can use it (`_cal_user`) the digit m + 1 at x 240 + 8
  · m; a row past the queue is cleared (`_wfbox(win, 14, 16, y, 280, 32)`); row 0 is **highlighted** by `_wxbox(win, 15,
  16, 32, 280, 32)` (XOR with colour 15). Headers 品名 / ﾎﾟｲﾝﾄ / ﾕｰｻﾞｰ at (48 / 192 / 240, 16), line (16, 31, 280, 1);
  `_whole(win, 16, 208, 280, 4)` divides it from the equipment table (`_init_eq` / `_disp_eq`: # 名前 職業 武 鎧 盾 弓 投
  at y 224, a member per 16-pixel row from y 240 with the member buttons '1' + m); the current item's slot kind is boxed
  in the table's header (`_pwaku[kind]`, `_wxbox` 16 × 15 at y 224).
- **Trade / U Use item / Identify `_tritmlst(member)`** (`_trade` `0x1079A` 交　換, `_setuse` `0xFDEA` [original text],
  `_setident` `0x2DCF4` "Identify": all three call `_init_tl`, so one layout): the member's **9 carried slots**, row i at
  y = **64 + 32 · i** (the last ends at 352, the window's bottom): the letter A + i in colour 2 at (16, y + 8), the icon at
  **(24, y)**, the name at (56, y + 8), the points at x 208 (ids < 0x55), the users' digits at x 240 + 8 · m; an empty
  slot is a cleared row with its button off (no letter, no icon). Headers 品名 / ﾎﾟｲﾝﾄ / ﾕｰｻﾞｰ at (56 / 192 / 240, 48),
  line (16, 63, 280, 1); the member's name at (16, 16) (`_wfbox(win, 14, 16, 16, 280, 16)` first); buttons
  `_setbtn(win, 16, 64 + 32 · i, 280, 32, 'A' + i)`. Trade's held item has no row of its own (`_defmcn` = 3, a mouse
  cursor change `[~]`).
- **Built (s151):** the X68000 pack has the 50 glyphs as `p4.item.N` (N = b0, `ui/items`, black ink, the rest
  transparent); IV's items carry b0 as `icon` (`items.json`, `P4ItemInfo.Icon`), the pick rows their item
  (`P4Choice.Item`: the Armory's six, Trade's / Use's / Identify's nine slots), and the Client's box draws a row with an
  item as an **icon row** (`P4DungeonScreen.Items.cs`): the icon at the pack's scale (16 layout pixels) on a colour-14
  cell (the window grey; the remaster's box is dark) at the row's left, the text after it, wrapped in the narrower room
  and centred on the icon when it fits one line; the row takes two text rows (the fewest whose last glyph ends at or
  below the icon), so the box scrolls whole icon rows (a row cut by the scroll is left out until scrolled into view; the
  cursor's row is brought into view whole; a row whose wrapped text would be taller than the box, possible in the narrow
  column beside a picture, stays plain text without its icon). Share out shows the item being placed and the next four of the queue with
  their icons (the original's 5 rows); Trade's held-item line has none. Checked on Xvfb (s151:
  `parity/shots/p4-remaster-armory-items.png`, `-share-out-items.png`, `-trade-items.png`, `-use-items.png`, `-use-items-narrow.png` (a
  location map's narrow column); the last four with items given by a temporary, uncommitted preview change). `[~]`: the remaster keeps its text rows, so the
  letter (colour 2 in the original) comes after the icon with the remaster's digit, and the columns (points, price,
  users) are the remaster's text, not the original's x positions; Share out's headers and the equipment table are not
  drawn (s158: its row-0 highlight is: the item being placed drawn as `_wxbox` XOR 15 leaves it on the window grey, a
  colour-1 band across the row with the icon's cell colour 1, its ink and the row's text colour 15,
  `parity/shots/p4-remaster-share-out-lit.png`); Share out's four queue rows push its pick list down (it scrolls).

**Command panel (s136, static) `[C]`:** every icon menu goes through `_mkicon(count, table, clear, title)` (`0x61F4`):
with `clear` ≠ 0 it runs `_clrbtn` / `_clrbtd` and opens window 34 = `_wopen(34, 496, 48, 136, 324, 1, title)` (title ""
→ `0x401D2` "Command"; `_fgcol` 0, `_bgcol` 16 first), then for entry i of the 32-byte table calls `_seticon(34, x, y,
icon = [+20], label = +0, key = [+24], …)` at **x = 38 + 24 · (i & 1)** (`eor #24`: a zigzag) and **y = 8 + 52 · i**,
storing the button id at +28. `_seticon` (`0x2FB04`): `_setbtn(−1, 34, x, y, 32, 32, key, …)` (the click box is the
picture only), `_aicon` the record at the window's origin + (x, y), then the label in **font 1** (`_wfinit(1)`) at
`_wloc(34, x + 16 − (_fsize(label) >> 1), y + 32)`: its **first character in colour 2** (`_wputc`), the rest in the
colour `_mkicon` set (0) by `_wprint`, or **all in colour 2 when the label starts with a space** (" SPACE"); then font 0
again. 40 fixed tables (two calls compute theirs: `_acgamend`'s game list, `_diskmenu`'s save slots), extracted by
`tools/scripts/x68k_p4_panels.py` → `src/OpenPhantasie.Data/GameData/phantasie4/graphics/x68000-panels.json` (window, layout,
per panel: title id, icon / key / label id + the Shift-JIS text). The street's (`_townmenu` `0x28E6`): `_par`+0C (party
size) ≠ 0 → F Profile 2, C Spells 3, T Trade 4, D Share out 5, U Use 6, S System 7 (`0x3F724`), else S alone
(`0x3F7C4`) and the one Guild door; outdoors `_outdoormenu` F 2, P Party info 33, C 3, U 6, M Map 8, S 7; location map
`_dsetup` the same with E Examine 9 for M; `_syscon` SPACE 20, D Display speed 24, B Sound 25, S Save 1, E End game 26,
A About 36 (the s110 system-menu shot shows exactly this). s158: `_acgamend(m)` (`0xE52`) computes its call,
`_mkicon(3 − m, 0x3F362 + 32 m, 1, "Select Game")` at `0xEA0`: m = 0 (`_syscon`'s E, `0x1174`) SPACE 20 / [original text] 27 /
[original text] 28, m = 1 (`_endstory` `0xAC26`, `_deadmsg` `0x1E49C` after its `_mgetc`) E and T only; E → `_acend(0)`
(Okay / Cansel titled [original text]: O → `_xblack`, `_beep`, `_xexit` with "The game ends."), T → `_acend(1)` (titled
[original text]: O → the ten roster records' first bytes and, with `0x789D4` set, `_par` cleared, `__cflag` = 1: `_main`
again), C → the icons again (the manual's Figure 10: SPACE, M Return to MS-DOS, T Return to title, PC-98). The
extractor writes them as `_acgamend` / `_acgamend.1`. **Built (s158):** System's E opens `_acgamend`, the final screen
and a wiped-out party's key `_acgamend.1`; E's O goes to the main menu (`_xexit` leaves for Human68k `[~]`), T's O to
IV's title splash and its `_disk(0)` menu (`P4ScreenRequest.Title`). **Built (s136):** the Client draws the panel at the layout's
right edge (title bar of 21 rows as `_wopen`'s, window grey = colour 14, icons `p4.icon.N`, labels in the pack's
`p4.panel.label` font = P4.X font 1, at the pack's scale) for the street, outdoor and location-map prompts; a click on an
icon presses its key (the digit picks and keys work as before); the text column narrows beside it and wraps / scrolls; the strip is kept for the whole place (street picture, outdoors, location map), empty at prompts without a panel (s138: `_spc_wait`'s and `_confirm`'s panels read, see below; s151: `_ewait`'s bar is read, it is not a panel and `_dsetup`'s panel stays up under it), so the column keeps its width
(`parity/shots/p4-remaster-street-panel.png`, `-outdoor-panel.png`). s157 (the remaster's own layout, not P4.X's: the original has no text column: its messages open windows over the picture on the 768 × 512 screen, XEiJ shots `parity/shots/p4-x68k-ending-street.png`, `p4-x68k-system-menu.png`): where the strip is kept, the key help goes under the picture and the strip at the screen's full width (51 columns, two rows for every help line, broken between key groups, never between a key and its word), or left of the strip when a tall list window reaches the bottom, else in the column as before; the column's box and party lines then reach down to it (the party panel up to half the column, was 6 rows), and under a short picture (a building's) the party lines go under the picture one row a member (`P4DungeonScreen.LayOutColumn`, `HelpLines`; `parity/shots/p4-layout-s157-*-before/after.png`). s158 outdoors
(`parity/shots/p4-layout-s158-outdoor-closeup.png`, `-overview.png`): the help goes under the picture and the strip in
both views; under the close-up the party lines go under the picture (one row a member) and the box takes the column, under
the taller M overview they stay in the column (half of it), so M moves the party panel and narrows the box; every line
shows in both. The column itself stays about 11 columns of the 5×7 font beside the 320-pixel picture and the 159-pixel strip `[~]`. s151 `[C]`: the "pressed look" is a hover look
(`_s_ms_get_cp`), and the title bar's font is font 0, style 0, colour 0 at y −18 (both in "Dungeon waits, …" below);
s141: the frame lines are read and drawn ("Window frames" below).

**Where each panel shows (s137 / s138, static: `_spc_wait` `0x6A4C` / `_confirm` `0x6A8C` callers read from `jsr` sites,
the other panels by their `_mkicon` site's function) `[C]`:** `_spc_wait` (SPACE) is called by `_inn`, `_townent`,
`_deposit`, `_gatebank`, `_withdraw`, `_shipyard`, `_mystic`, `_distribute`, `_endmsg`, `_endstory`, `_common_proc`,
`_dsp_hinfo`, `_hprint`, `_hhist`, `_use_op` / `_use_sc` / `_use_box`, `_mtrans`, `_make`, `_make2`, `_err_fame`,
`_mopta`, `_make5`, `_obarrier`, `_oship_mark`, `_save_ship`, `_oinn`, `_oencgo`, `_esub01`, `_cast_50`, `_pmagic3`,
`_ident`; `_confirm` (Okay / Cansel) by `_trang`, `_dislodge`, `_re_name`, `_party`, `_endstory`, `_mtrans`, `_make3`,
`_mkcha`, `_make5`, `_obarrier`, `_oboatman`, `_opool`, `_odungeon`, `_oinn`, `_otown`, `_oencchk` (×2), `_cast_47`,
`_cast_50`. The shipwright asks with `_noyes(104, 300)` (`0x30E7C`, its only caller), a YES / NO box, not a panel.
Dungeon events wait in `_ewait` (`_setitm`, `_setgp`, `_listen`, `_exe_ev`, traps, chests, `_try_ul`), whose
SPACE bar is not a `_mkicon` panel (s151: read, "Dungeon waits, …" below). `_mlistsubset` (SPACE) is the roster pick of `_dislodge`, `_re_name`, `_inspect`
and `_party`; `_inspect`'s own panel ("Character info", SPACE) follows it while the sheet is up; `_party`'s ("Elemental",
SPACE) is the elemental choice after its `_confirm`. `_hhist` (`0xD97A`): pages 1–8 draw a page, then `_mkicon(2, L
Done, N Next picture)` and a key; L (the compare with `0x4191A`, entry 0's key) leaves at once, anything else turns the
page; page 9 reads a key under the old panel, then `_spc_wait`. `_hprof` (`0xD71C`): `_hprofmenu` (the person buttons and
"Character info" L Done) and a key; L leaves, a person runs `_dsp_hinfo` (its `_spc_wait`). `_trang` / `_mage`: E End
training while a member is picked.

**Built (s137, s138):** the Core screens name the panel at each prompt (`P4TownLoop.IconMenu`: `_guildmenu`, `_bank`,
`_hall`, `_armory`, `_trang`, `_mage`, `_mlistmenu`, `_mlistsubset`, `_inspect` (the roster profile's wait), `_party`,
`_distribute`, `_trade`, `_hhist` (pages 1–8; L leaves the history, `LeaveHistory`), `_hprofmenu`, `_makemenu`,
`_make1menu`, `_make5menu`; "" = none: the level-up and magic-lesson waits, which call no `_spc_wait`; s140:
`_noyes` = the shipwright's YES / NO box, drawn, see below); every other town wait shows `_spc_wait` (the inn, the bank, the
Mystic, the shipwright under 1500 GP, the entry, the Hall sheet, Share out's end, creation) and every other Okay / Cancel
question `_confirm` (creation, delete / rename / form party, training). Outdoors, each wait matches a `_spc_wait`
caller (boarding `_oship_mark`, the inn stay `_oinn`, leaving the ship `_save_ship`, the key before a fight `_oencgo`,
the barrier `_obarrier`) and each question a `_confirm` caller (town `_otown`, inn `_oinn`, location map `_odungeon`,
pool `_opool`, Charon `_oboatman`, barrier `_obarrier`, sleeping monsters / fight `_oencchk`), so all show those panels.
A town screen without a picture (`_clrtdraw`) keeps the strip: the text takes the picture's room. Clicks press the
remaster's key for the icon: a fixed pick list's line digit (Bank, Guild, Hall, roster N, `_makemenu` E / Y / N,
`_make1menu` N / H, `_make5menu` S / D / H), lines that follow the shop page or the party by position (Armory L, Share
out P / S, Trade P / S / L; a line past 9 puts the cursor there and presses Return), O / C = Y / N, End training / the
Hall list's Done / the history's N = SPACE, the Elemental pick's SPACE = Return; L on a history page is read as a key.
**System** (`_syscon`) is a Client screen opened by S on the street, outdoors and on a location map: SPACE back, S save
(the shell's F5, refused where F5 is), E `_acend` (O / Y → the main menu, C / N back), A `_about` (its ten credit
lines in the text column; the original prints them into the panel's window `[~]`); D (`_timelag`) and B (`_acsound`)
only say IV has no such setting here `[~]`. **Fit (the user's rule, no cropping):** English labels and titles run past
the 136-pixel window ("H:A person from the Hall" 150, "Back to the title screen" 155 in font 0), so the window takes
one width for all 40 panels, max(136, widest label or title + 4) in the current language (159 in English, 136 when
everything fits), and a label that would cross the frame moves in just enough; the others stay centred under their
icon. Checked on Xvfb (s138; `parity/shots/p4-remaster-armory-panel.png`, `-make5-`, `-trade-`, `-hall-history-`, `-system-panel.png`): street, entry wait, Armory, roster, creation (`_makemenu`, `_make1menu`, `_confirm`,
`_make5menu`), Hall (`_hhist`, `_hprofmenu`, sheet wait), Trade (a mouse click on L, line 13), Share out, Mystic wait,
training, form party / Elemental, outdoors action and `_otown`'s question, System / About / End game, location map.
Still text `[~]`: Bank
amounts and shares, names, the Armory's and Trade's item lines (their own buttons, not `_mkicon`) (s151: creation's
race / class / Hall lists, the spell list, the Magic Training Hall's pages and Transportation's towns are drawn, see
"Lists in window 34"; s152: the level-up skill picks `_sklup` are drawn, "Level-up skill picks" there),
`_trans` / `_trans.2` (the III transfer's disk prompts: no remaster moment; `_htran` and `_show_gm` built s148), `_acswitch`, `_acsound`, `_timelag`, `_acend.2`; dungeon
waits (`_ewait`). The digit picks and the remaster's keys stay; the original's letters are read only where noted (S, the
System page's letters, the history's L, s140 the profile's N).

**Party menus, icon rows, `_noyes`, `_findit` (s140, static `[C]`; P4.X read from the `jsr` sites, labels / keys from the
tables, `tools/scripts/x68k_p4_panels.py` writes them to `x68000-panels.json` "rows" / "noyes"):**
- **F Profile:** `_inspect(place ≠ 0)` → `_mkicon(1, 0x402A2, 1, "Character info")` (SPACE) + `_setwho` over the
  member pick; SPACE leaves. The sheet itself (`_detail`, window 35 = `_wopen(35, 160, 64, 472, 338)`) is **not a
  `_mkicon` panel**: each page draws its own buttons with `_seticon(36, 32 + 52 · i, 2, …)` in window 36 =
  `_wopen68(36, _cvx(160) + 314, _cvy(64) + 270, 150, 48, 0, "")` (untitled; s141 `[C]`: `_wopen` runs x / y through
  `_cvx` / `_cvy` and stores width / height as given, `_wopen68` stores all four as given, so window 36 sits at
  **(314, 270) in window 35's area**, screen pixels: 8 in from the sheet's right edge (472 − 314 − 150) and 20 up from
  its bottom (338 − 270 − 48); and with flag 0 `_wwind` returns at `0x333FA` before drawing anything, so window 36 has
  **no fill and no frame**: the icons and labels sit straight on window 35's grey, colour 14, the fill of a titled
  window, see "Window frames" below): page 1 `_detailmenu` (table `0x40A0A`:
  SPACE icon 20, N:次頁へ icon 23), page 2 `_p_info3menu` (`0x4061C`, the same two), page 3 `_page3` (`0x42A1E`: SPACE,
  N:前頁へ). Keys: towns.md 3 "Profile pages" (N 1 → 2 → 3, page 3's N back to 2, SPACE leaves).
- **P Party info:** `_party_info` → `_p_info1menu`: window 35 again, one `_seticon(36, 64, 2, …)` (`0x43312`: SPACE);
  SPACE (button `0x4332E`) leaves, A / B turn the party strategy.
- **C Spells:** `_ocast` → `_mkicon(1, 0x42EAC, 1, "Cast Spell")` ([original text]) + `_setwho` for the caster; then
  `_getmagic` opens its **"Spell List" in window 34 itself** (`_wopen(34, 496, 32, 136, 360)`, the panel's place) and
  closes it; a targeted spell's member pick `_mkicon(1, 0x42EAC, 1, "")` ("Command", `_ocast.2`). Transportation
  (`_cast_47`): `_mkicon(1, 0x44E32, 1, "Transport")` ([original text]), then the group's towns **in that window under the
  icon**: "A"… at (4, 60 + 16 · i), the name at x 16, each a `_setbtn(34, 0, y, 136, 15, 'A' + i)`; then `_confirm`.
  Identify (`_ident`): `_setident` = the item window (`_wopen(-1, 160, 32, 312, 352, "Identify")`) + `_mkicon(1, 0x454EC,
  1, "")` (" SPACE") + `_setwho`; **one** item is appraised (`_ident_mi`, `_spc_wait`) and the spell ends (the remaster
  does the same since s140). **Waits after a cast (s156, static `[C]`):** `_ocast` has none of its own: after
  `_pmagic3` it takes the MP (`_getmp`), `_healch`, redraws the view outdoors / on a location map / in town and goes
  straight back to the caster pick (`0x153C8` → `0x15458`). The only `_spc_wait`s are `_pmagic3`'s: Resurrection (40)
  after either line (`0x2D55E`); Transportation / Super Transport after "Transport!" (`0x2D6F0`; a caster outside the
  transport towns gets "Transport! …nothing changed" with none); the monster level (49, `0x2D7B4`); Divine Aid (63)
  after "… whispered in your ear" (`0x2D83E`) and after the hint (`0x2D86A`); `_cast_50` (Magic Bag) after each page,
  the find and the refusal; `_ident` after the appraisal. Healing (1–4) and Levitation (13) print their line and wait
  for nothing. The remaster follows this since s156 (before: one SPACE wait after every cast); the effect's lines stay in
  the text box above the next caster pick.
- **U Use item:** `_setuse` = the item window + `_mkicon(1, 0x42598, 1, "")` ("L:終わり", button `0x425B4` leaves) +
  `_setwho`; `_use_sc` (scrolls) and the 0x7E potion's text wait on `_spc_wait`, then `_setuse` again; `_spoile` (a
  potion gone bad) prints its line and waits for nothing (the remaster the same since s156: back to the item pick).
- **Shipwright (`_noyes(win, 104, 300)`, `0x30E7C`):** `_whole(win, x − 8, y − 8, 120, 32)` = `_hole` (two nested
  1-pixel frames outside the box: colour 1 top / left, colour 15 bottom / right), `_wfbox(win, 4, x, y, 48, 16)` and
  `(x + 56, y)` (colour 4 buttons), "YES" / "NO" (`0x4733A` / `0x4733E`) by `_wprcen` centred at x + 24 / x + 80 in
  font 0, `_setbtn(12, …, 'Y')`, `_setbtn(13, …, 'N')`, `_setbtn(14, 0, 0, 0, 640, 400, ESC)` (a click elsewhere is
  read and ignored); button 12 → yes, any other key → no; `_wfbox(win, 14, x − 10, y − 10, 124, 36)` clears it.
  s141 `[C]`: `win` is the shipwright's own window, `_wopen(-1, 160, 32, 312, 352, 1, title)`, and `SHIP.PKH`
  (312 × 220) is unpacked at the same (160, 32) (`_xunpack(0, 160, 32, …)` at `0x5F90`), so the picture's origin is
  the window's: the box lies at **(104, 300) of the picture's top-left**, its `_hole` frame at (96, 292) − 2, i.e. under
  the picture (rows 220 on are the window's grey 14, where the original prints the gold and the offer at y 236–268).
- **Combat's found item (`_findit`):** `_mkicon(2, 0x4438A, 1, "Found Item(s)")`: O:拾う / [original text]; O takes it, any
  other key leaves it; a full pack skips the question (combat.md 11). s141 `[C]`: before the panel, `_msg(1)` opens the
  fight's message window **over the field** (`0x2037C`): it saves the field (`_cut_buf(_cvx(152), _cvy(60), 336, 280)`,
  the squares), then `_wopen68(-1, _cvx(152) + 32, _cvy(60) + 112, 272, 48, 1, "")` (the field's squares at (32, 112),
  flag 1, title "" = untitled: white, colour 15, with the black frame below) and a scrolling text area
  `_scinit68(win, _cvx(152) + 48, _cvy(60) + 120, 240, 32, …)` (two 16-dot lines); `_xprintf` prints
  "\n[original text]" (`0x4444A`, the item) and "\n[original text]" (`0x4445B`) into it, then `_mkicon` puts window 34
  right of the field; after the key "\n[original text]" (`0x44477`) on O, and `_msg(0)` (`_bwait`, the field
  pasted back). So the question's text is in the window over the field, the panel beside it.

**Lists in window 34 (s151, static `[C]`; P4.X read from the functions, constants checked by
`tools/scripts/x68k_p4_panels.py` → `x68000-panels.json` "lists"):** these lists are text buttons in the Command window,
not icons. Each row: its key character, its text, and `_setbtn(-1, 34, 0, y, w, 15, key, 0x40, 2)` over the row. Font 0
(`_mkicon` ends on font 0; `_getmagic` sets `_font(0, -1, 0, 16)`), colour 0, on the window's grey 14. `_wcls(34)` clears
only the window's area to its fill (14 when titled) and keeps the title bar, so a list drawn after `_wcls` sits under the
preceding `_mkicon`'s title ("Command").
- **Spell list `_getmagic(member, place)` (`0x2BA96`):** its own window, `_wopen(34, 496, 32, 136, 360, 1, "Spell List")`
  (16 rows higher and 36 taller than the panel's); `_wbox(34, 1, 0, 96, 136, 3)` = a colour-1 outline 3 rows high (a
  groove) under the head. Head (`_spelltop` `0x2B718`, after `_clrbtn` / `_clrbtd`): places 1–7 one line, "[original text]"
  (`0x44C42`) at (8, 8), button (0, 8, 136, 15) on ' ' (`_btd`[5]); places 0 and 8 six lines at (8, 16 i), button (0, 16 i,
  136, 15) on '1' + i: combat (0) "[original text]" … "[original text]" (`0x44A98`[0–5]), Magic Training Hall (8) "[original text]" … "[original text]",
  "[original text]" (`0x44A98`[6–11]). Rows (`_splist` `0x2B86E`): `_wfbox(34, 14, 0, 104, 136, 256)`, then **every** spell of the
  page gets its letter 'A' + i at (4, 104 + 16 i) (`_wputc`, unconditional); the name at x 18 and the button (0, y, 136, 15)
  on 'A' + i (`_btd`[6 + i]) only where it may be picked (places 1–7: state 1 and MP ≥ cost; place 8: state 2). So the
  letters keep their page places with gaps (spells-noncombat.md, "Which spells each place offers").
- **Combat spell list `_getmagic(member, 0)` (s152, static `[C]`, from `_chrexe`'s order step, combat.md 2):** the same
  window, groove and font. The loop (`0x2BB58`–`0x2BBEA`): page = 0 + place = **page 0 each time C is chosen**; then
  `_spelltop(0)`, `_splist(member, page, 0)`, `_mgetc`, `_bvs`. `_spelltop` for place 0 (not 1–7) writes six head lines
  from `0x44A98`[0–5] at (8, 16 i), each `_setbtn(-1, 34, 0, 16 i, 136, 15, '1' + i, 0x40, 2)` stored at `_btd`[i]: "1
  :非攻撃系" (Non-combat), "[original text]" (Combat (received)), "[original text]", "[original text]", "[original text]" (Combat), "[original text]"
  (Quit). `_bvs` = i < 5 → page = i, the page is redrawn (`_splist` first clears rows 104–359, `_wfbox(34, 14, 0, 104, 136,
  256)`); i = 5 → returns 100 (no spell: the order step goes on); a letter (`_btd`[6 + j]) → returns list[first(page) + j]
  (0-based: spell j's number − 1 + 1). Rows as for places 1–7: every spell of the page lettered 'A' + j at (4, 104 + 16 j),
  name (x 18) and button only when state 1 and MP ≥ `_getmp`. Pages (`0x44AC8` {first, last} inclusive, into `0x44AE2`,
  0-based spell indices; `build_p4_gamedata.py` reads them into `rule-tables.json` `combat.spell_pages_code`):
  0 = 1–4, 13, 40; 1 = 11, 12, 16–19, 42, 44, 45; 2 = 5–8, 34–37, 51–58 (16 rows: 104 + 16 · 16 = 360, the window's
  height exactly); 3 = 20–23, 30–33, 59–62, 64; 4 = 9, 10, 14, 24, **26, 25**, 27, 28, 29, 38, 39, 41, 43, 46, 48 (not in
  number order). Their union is every spell but 15, 47, 49, 50, 63, 65 (combat.md 9.1). The table's next pages are the
  places': page 5 = 1–4, 13, 40, 15, 49, 50 (outdoors), and so on. The window is opened where the panel's is (x 496), right
  of the field (`_opn_field`'s squares end at x 488).
- **Transportation `_cast_47` (`0x2D0C6`):** the "Transport" panel first (`_mkicon(1, 0x44E32, 1, "Transport")`, its SPACE
  icon at y 8), then town i: 'A' + i at (4, 60 + 16 i), the name at x 16, button (0, y, 136, 15) on 'A' + i.
- **Creation (window 34 after `_wcls`, under "Command"):** `_racemenu` (`0x12838`): seven rows, the digit i + 1
  (`_wdouti(34, 1, n)`) at (4, 16 + 16 i), the race (five) / "Random Creature" / "戻る" at x 16, button (0, y, 135, 15) on
  '1' + i; `_rracemenu` (`0x125C8`): rows n = 1… at y 16 n, the digit, the race at x 16, button on '0' + n; `_classmenu`
  (`0x1238A`): the race's classes (mask `0x42B52`) numbered from 1 at (4, 16 + 16 i), name x 16, width 135;
  `_set_fame_lst` (`0x638C`, `_make3` / `_moptb`): "#    Name     Race" (`0x401DA`) by `_wprcen` centred on x 68 at y 8,
  each used Hall slot (row n) 'A' + n at (4, 24 + 16 n), the name at x 18, the race's first three characters at x 96
  (`_wputc` × 3), button (0, y, 135, 15) on 'A' + n (stored at `_btd`[slot], so `_bvs` returns the slot). The explaining
  lines ("種族 （ＲＡＣＥ） [original text]" etc.) go by `_wkprcen` into the creation windows 35 / 37 or the ancestor windows, not
  into window 34.
- **Level-up skill picks `_sklup` (`0xC0EE`, `_sklupmenu` `0xBF48`, `_sklpr` `0xBDF2`): not this machinery.** Its own
  window 32 = `_wopen(32, 160, 136, 312, 248, 0, "")` (flag 0: `_wwind` draws no fill or frame), a black bar `_wfbox(32, 0, 0, −20, 312, 20)` with
  "[original text]" (`0x4168B`) centred on x 156 at y −16 in colour 15, the header "[original text]" (`0x41698`) at
  (16, 20), a sunken `_whole(32, 4, 36, 304, 176)`, eleven rows at y 36 + 16 i: 'A' + i at x 16, the skill at x 40, now
  (3 digits) at x 112, after at x 192, button (4, y, 304, 16) on 'A' + i; the prompt "＊あなたは N [original text]" at
  (16, 2), after each pick "<skill>[original text]" at (16, 224); at the end "[original text]" in the bar in colour
  2. Read further (s152): the window has no grey of its own, it sits in `_trang`'s titled window 32 (`_wopen(32, 160,
  32, 312, 352, 1, …)` at `0x45F6`, grey 14); each row is first cleared (`_wfbox(32, 14, 4, y, 304, 16)`) and its button
  is a plain one (`_setbtn(-1, 32, 4, y, 304, 16, 'A' + i, 64, 2)`, hover `^ 5`); the ink is **colour 0** (s158 XEiJ
  `[C]`, `parity/shots/p4-x68k-sklup.png`, `-sklup-done.png`: rows, header, prompt and trained line black): `_trang`'s
  `_font(0, −1, 2, −1)` at `0x442A` comes before `_confirm`, whose `_mkicon` clears `_fgcol`, and the title's
  `_font(−1, −1, 15, 0)` is undone by `_font(−1, −1, −2, −2)` back to that 0 (s152 read colour 2 here), `_sklup` adds grey cell boxes (`_font(−1, −1, −1, 14)`); after each pick the
  trained line's row is cleared first and the prompt row after (`_wfbox(32, 14, 0, 2, 312, 14)`), so after the third
  only the trained line shows; at the end the bar is filled grey (`_wfbox(32, 14, 0, −20, 312, 20)`) before the colour-2
  line. `_trang` then goes straight on to its member pick with the window still drawn. **Built (s152):** the pack's
  `sklup` (`x68k_p4_panels.py`, each call site checked) and the Core's `P4TownLoop.SkillWindow` (`P4SkillWindow`: the
  eleven rows in skill order with now / after (`_calsk`), the picks left, the prompt, the last trained line, done), set
  from the first pick (s156: through `_trang`'s member pick after the third); the Client (`P4DungeonScreen.SkillUp.cs`) draws it in the
  picture's place (the training hall has none): the grey, the bar and title (at the end grey with "This training
  session is over."), the header, the sunken frame, the rows (now / after by `_wdouti`'s padding), the prompt and the
  trained line in font 0, colour 0 (s158; was 2); a click on a row presses the remaster's key for that skill (a digit, past 9 the cursor
  and Return), a row under the pointer is lit. The text column keeps its lines and keys.
  **`_trang`'s own window (s156, static `[C]`):** at each member pick other than SPACE (`0x45CC`) `_wopen(32, 160, 32,
  312, 352, 1, "訓練場")` opens it afresh (so a level-up's `_sklup` rows go only then; SPACE: `_clrtdraw`, `_wclose(32)`),
  the marker `_amark`, the name `_wlocpr(32, 16, 16, record + 0x16)`, then one note: poor condition (record + 0x12 ≠ 1)
  `_wprint("[original text]")` + `_wlocpr(32, 32, 32, "[original text]")`; level ≥ 125 `_wprint("はもはやこれ以上教える
  事はない。")`; experience short `_wprint("[original text]")`, `_wloc(32, 16, 32)`, `_font(1, −1, 11, −1)`,
  `_wdoutl(32, 137, need)`, `_font(0, −1, −2, −1)`, `_wprint(" [original text]")`; else the cost
  `_wprint("[original text]")`, `_wloc(32, 16, 32)`, `_font(1, −1, 8, −1)`, `_wdoutl(32, 137, cost)`, `_font(0, −1, −2,
  −1)`, `_wprint(" [original text]")`, `_wlocpr(32, 16, 48, "[original text]")`, `_font(0, −1, 2, −1)`, `_confirm`;
  a YES the purse cannot pay `_wprint("　[original text]")` (at window 32's cursor after the question: `_confirm` draws
  only in window 34 `[~]`), else `_calpow` (`_sklup`'s window 32 at (160, 136): its bar sits 84 rows into this window).
  The number goes in font 1 (`_trang` is one of its users), 137 = 0x89 is `_wdout`'s format (9 places, leading zeros
  dropped, no padding: left-aligned at (16, 32)). The ink at the name is colour 0: `_mkicon` clears `_fgcol` before each
  member pick and `_seticon` restores it (`_guild` set `_font(0, 0, 0, 16)` on entry); `_amark` and `_wopen` are not read
  for colour changes `[~]`. **Built (s156):** the pack's `trang` (`x68k_p4_panels.py`, each call site checked: the window,
  the name's place, the four notes as segments with their places, fonts and colours, "not enough gold" in the ink:
  s158 XEiJ `parity/shots/p4-x68k-training-short.png`, black, as `_confirm`'s `_mkicon` cleared `_fgcol`); the
  Core's `P4TownLoop.TrainingWindow` (`P4TrainingWindow`: the member, the note, the number, short of gold) from each
  member pick through the next (SPACE clears it); the Client draws the window in the picture's place (grey, the name and
  note, the number in font 1) with `_sklup`'s window in its lower part; English rows that would pass 16 short of the
  right edge wrap at a space to the line's start and push the later rows (and `_sklup`'s bar) down (the user's fit rule).
  `SkillWindow` now stays through `_trang`'s member pick after the third skill pick and goes at the next pick (the
  original's window 32 opening again), and the remaster's own wait after the picks is gone (`_calpow` / `_healch` call no
  `_spc_wait`). The English header is split at its spaces into its five words, each drawn where the original header's
  word stands (x from `_fsize`'s advances: 16 per full-width character, the font's for the rest; # 16, 技能 56, 現在
  120, -> 168, 訓練後 200), the last three moved with the now / after columns; a translation with another word count
  stays one string. **s158:** the window's title bar ("訓練場", `p4x.0x3fd0c`, the pack's `trang.title`) and frame are
  drawn (`P4DungeonScreen.DrawTitledBeside`, `_wwind` / `_wactive` as the Command panel's; the bar takes the screen's
  top margin so the s157 key help still fits under the window; a translation that would push it past the screen falls
  back to the untitled window `[~]`); seen on XEiJ with the experience and gold poked (`parity/shots/p4-x68k-training-needs.png`,
  `-training-cost.png`, `-sklup.png`, `-sklup-done.png`, `-training-short.png`): the bar as drawn, the name in colour 0,
  the cost number in colour 8 (blue), the experience still needed in colour 11 (gold), the end line in colour 2 on the
  grey bar; remaster `parity/shots/p4-remaster-training-titled.png`. `[~]`: the cell boxes are not drawn (grey on the
  same grey); now / after move right just enough when a skill name would come within 4 pixels of x 112 (the user's fit
  rule); whether the patched original's English header looks the same is unchecked `[?]`; window 34 keeps `_confirm`'s
  Okay / Cansel icons in the original during the picks (XEiJ shot: yes, nothing clears it), the remaster's strip is empty
  during the picks; on XEiJ the trained line's row (y 224 of `_sklup`'s window) overpaints the hall window's bottom frame
  line, the remaster keeps the frame. Checked on Xvfb (s156; `parity/shots/p4-remaster-training-cost.png`,
  `-training-short.png` (no gold: the line wraps), `-sklup.png`, `-sklup-done.png` (the member pick after the third pick,
  the window still up); a temporary, uncommitted preview tweak for the experience and gold).
- **Built (s151):** the Core names the list at each of these picks (`P4Menus.ListWindow` / `P4TownLoop.ListWindow`:
  `_getmagic`, `_getmagic.8` for the Magic Training Hall, `_cast_47`, `_racemenu`, `_rracemenu`, `_classmenu`,
  `_set_fame_lst`) with its rows in the original's order (a spell row with no choice = its letter alone) and the head
  lines' choices (SPACE = back; the Hall's page lines = the page choices, 6 = Quit); the Client draws it in the strip
  (`P4DungeonScreen.Lists.cs`): the spell list in its own titled 360-row window, the creation lists in window 34 under
  "Command", the towns under the Transport panel; a click on a row or head line presses the remaster's key for its
  choice (a digit, past 9 the cursor and Return; SPACE). The text column keeps its lines and keys. The strip's width also
  fits the lists' longest texts (spell, town, race, class names; 159 in English as before). `[~]`: the spell list's top
  stays at the strip's top (the original's window starts 16 rows above the panel's); the remaster's frame takes the
  window's first and last column, so the groove runs between them; a long Hall name pushes the race column right of x
  96 just enough (none of the shipped names do); the combat spell list (`_getmagic(member, 0)`) is built s152 (next
  bullet). Checked on Xvfb (s151; `parity/shots/p4-remaster-spell-list.png` (town), `-spell-list-dungeon.png` (location map),
  `-transport-towns.png` (a click on K, line 11, asked for Akihabara), `-magic-page.png` (a click on "2 :Page 2" turned
  the page), `-race-list.png`, `-class-list.png` (a click on Elf), `-hall-list.png`; spells / gold by a temporary,
  uncommitted preview tweak).
- **Built (s152):** the combat spell list. Core: `P4Battle.SpellPages` (the five pages, `rule-tables.json`
  `combat.spell_pages_code`), `CastableSpells(page)` (known, MP ≥ cost, in the page's order), `SpellList(page, names)` =
  `P4ListWindow("_getmagic.0")` with every spell of the page (name and choice only where castable, else the letter alone)
  and the head choices after the page's spells (the five pages, then Quit), `SpellListChoice` (spell / page / Quit),
  `SpellPageHeads` (the head lines' string ids). Client (`P4DungeonScreen.Battle.cs`): C opens the list on page 0; the
  text column lists the page's castable spells by name, then the pages and Quit (their text without the original's "1 :",
  since the column's digits differ); a digit or the cursor picks; a page line turns the page; Quit and Backspace go back
  to the orders. The window (pack list `_getmagic.0`: `_spelltop(0)`'s six lines from `0x44A98`, keys 1–6) is drawn as the
  found-item panel is (user s142, option a): over the field's right side, top at the field's, running below the field. A
  click on a row or a head line presses its line's key (a digit, past 9 the cursor and Return). `[~]`: the original's
  window is right of the field (x 496), not over it; its keys 1–5 (pages) and A… (spells) are not the remaster's keys
  (the column's digits are, as in the Magic Training Hall's list). As the original, C opens the list even when nothing
  is castable (letters only; Quit goes back; the remaster's earlier "No spell can be cast now." note is gone). Checked
  on Xvfb (s152; `parity/shots/p4-remaster-combat-spell-list.png`: a click on "3 :Combat" turned to page 2, whose 16
  rows fill the window; a click on "M Flameball 1" (row 13: cursor and Return) gave the order; a temporary, uncommitted
  forced fight with every spell known).

**Window frames (s141, `_wwind` `0x333E0`, `_wactive` `0x335B4`, static `[C]`):** `_wwind(x, y, w, h, flag, titled)`
draws nothing when flag = 0. Otherwise: fill colour 15 (untitled) or 14 (titled) over x, y, w × h; a black (colour 0)
1-pixel frame round it (x − 1, y − 1, w + 2, h + 2); a black shadow column at x + w + 1 from y + 1 down to y + h + 1 and
a black shadow row at y + h + 1 from x + 1 to x + w + 1. A titled window's bar: fill 14 over (x, y − 20, w, 19), a colour-1
line (dark grey, `#616161` in the pack palette) on its last row y − 2; `_wactive(win, 1)` then frames the bar (x − 1,
y − 21, w + 2, 21) in black (colour 1 when inactive), a shadow column at x + w + 1 for rows y − 20 … y − 1, and a black
line at y − 1. So the bar reads black, 18 grey rows, dark grey, black (the s110 shot); the shadow column misses row y.

**Built (s140):** `P4Menus.IconMenu` names the panel at each party-menu prompt (F's member pick `_inspect`, the
profile pages `_detailmenu` / `_p_info3menu` / `_page3`, P `_p_info1menu`, C's caster `_ocast`, the spell list none,
the target `_ocast.2`, Transportation's towns `_cast_47`, Identify `_setident`, U `_setuse`; their waits `_spc_wait`,
questions `_confirm`; the spoiled potion's wait none); the town's shipwright question names `_noyes` (kept by a save at
the question); a fight shows `_findit` at the found-item question. The window-36 rows' labels are centred under the
icons and moved apart just enough when English labels would touch (SPACE moves left of its icon for "N:Previous page";
every row still fits 150, so the strip keeps 159). Clicks: the profile's N = the page's N line, U's L = the Done line
(past 9: the cursor, then Return), SPACE = Space, YES / NO = Y / N, O / C = Y / N. Outdoors the view now stays up under a
party menu (`_inspect` skips `_clrtdraw` there, `_ocast` redraws the view with `_opts1`), which gives the menu's panel its
strip; where the original covers it with its own window (U's item window, P's and the profile's window 35) the remaster
still shows the view `[~]`. Checked on Xvfb (s140; `parity/shots/p4-remaster-use-panel.png` (a mouse click on L:Done
closed it), `-cast-panel.png`; the s140 row / box shots are replaced by s141's below).

**Built (s141):** the icon rows sit at the sheet's lower right: the remaster's sheet is the text box with the panel strip
beside it (empty while a row is up), so a row keeps window 36's insets, 8 from the strip's right edge and 20 above the
box's bottom (`P4DungeonScreen.RowAt`, the pack's `rows` carry `window.x / y` 314 / 270 and the `sheet` 472 × 338); no
frame, on the sheet's grey (colour 14) since the remaster's box is not grey; it never starts left of the strip. The
shipwright's YES / NO box is drawn in the picture's coordinates at (104, 300), under SHIP.PKH, on the window grey of
`_wfbox`'s clear rect (`clear` in the pack), no longer in the strip (`FittedPanelWidth` leaves it out). The found-item
question's lines (Core `P4Battle.TreasureLines`: the item, "pick it up?", the D4-036 appraisal) are drawn in `_msg`'s
window over the field: white, black frame and shadow, at the squares' (32, 112), 272 wide, text at (48, 120) in the
pack's font 0, 16-dot lines; more than two lines make the window taller (moved up when it would pass the field's bottom)
so none is dropped; `_findit`'s panel stays right of the field. **s142 (user, option a):** the panel now covers the
field's right side instead (drawn at the field's right edge, top at the field's; its 324-row window runs below the
field), so the text column keeps its full width; the message window moves left of the panel (`FindItMessageSpan`: from
x 8, ending 4 short of the panel) and wraps into more lines (`parity/shots/p4-remaster-found-item.png`, re-shot s142
with the same temporary preview). Command panels now have
`_wwind` / `_wactive`'s frame lines (the bar's dark grey last row, the black shadow right and below; one more row of
window grey at the bottom). Checked on Xvfb (s141, `parity/shots/p4-remaster-profile-row.png` (a mouse click on
N:Next page turned the page), `-party-info-row.png` (outdoors), `-shipwright-noyes.png` (1749 GP by a temporary
preview bump, not committed; a click on NO answered no), `-found-item.png` (a fight forced by a temporary, uncommitted
preview: an event fight map, a treasure always found)). s151: the spell list and Transportation's towns are drawn
("Lists in window 34"); the shipwright's window under the picture, the YES / NO text colour (0, `[C]`) and the buttons'
look under the pointer are read and drawn ("Dungeon waits, …" below).

**Dungeon waits, typed prompts, the keypad, the shipwright's window, the hover look, the title bar (s151, static `[C]`
unless tagged; P4.X read from the `jsr` sites, `tools/scripts/x68k_p4_panels.py` writes them to `x68000-panels.json`
"title", "hover", "ewait", "numin", "shipwright"):**
- **Title bar (`_wttl` `0x33520`, from `_wactive`):** `_wfopt` = 0, `_fgcol` 0, `_bgcol` 16, then `_wprcen(win, width >> 1,
  −18, title)` in the current font, which is font 0 wherever a window opens (`_seticon` puts font 0 back). So the title is
  font 0, plain, black, its cell top 3 rows under the bar's black line, x = width / 2 − (`_fsize` >> 1). The s135 XEiJ
  shot's "Command" is font 0's glyphs pixel for pixel, at that row. The remaster drew it 1 row high before (fixed).
- **Hover look (`_s_ms_get_cp` `0x350EA`, called by `_mcget` on every pass of `_mgetc`'s wait):** the button under the
  mouse pointer is XORed with `_xbtn` and XORed back when the pointer leaves it (`0x820BC` holds the lit button); on
  return `_mgetc` puts it back (a click: once; a key: the lit one back, the key's button XORed twice, so no look).
  `_xbtn` by the button's type (its `_setbtn` 8th argument): 64 = a plain button, `_xbox(5, x, y, w, h)` = every pixel's
  colour ^ 5; 128 = nothing; else an icon, `_xicon` of record type & 63 (`_seticon` passes the icon number): its opaque
  pixels' colour ^ 15. There is **no pressed look**: holding the button down (`_while_mbtn`) changes nothing. What
  lights is `_onbtn`'s first active slot under the pointer, from slot 0 up: `_ewait`, `_noyes`, `_numin`, `_getwho`,
  `_xmenu` and `_dready` register slot 14 over the whole screen (0, 0, 640 × 400, type 128, ESC), so while they wait only
  their own low slots light (the SPACE bar, YES / NO, the keypad's keys) and the Command panel's icons (slots 194 on,
  `_setbtn(-1, …)`) never do; `_spc_wait`, `_confirm` and `_findit` register no slot 14, so their icons light. Typed
  prompts (`_wscan` / `_wiscan`) never run `_mgetc`, so nothing lights there. The remaster (`P4DungeonScreen.Prompts`,
  `IconsLight`) draws the icon / plain button under the pointer lit by these rules
  (`parity/shots/p4-remaster-icon-hover.png`, at the location map's command prompt); with keys there is no look, as in
  the original. `_ewait` leaves slots 13 / 14 set on return; whether the command prompt after an event clears them
  before its `_mgetc2` (else its icons would not light, and clicks could land on slot 14) is not read `[~]`.
- **`_ewait` (`0x187BC`)** draws in the location map's message window (`0x7F122` = `_wopen(-1, 160, 40, 304, 320, 1,
  name)`; `_eprint` fills its top 96 rows colour 15): `_wbox(msg, 0, 0, 80, 304, 1)` (a black line), "ｽﾍﾟｰｽ" (`0x43796`)
  by `_wprcen` at (152, 82) in the current font and colour (font 0, black), `_setbtn(13, msg, 0, 81, 304, 15, ' ', 64, 1)`
  (a plain button over the bar, key Space) and 14 = the whole screen (ESC); `_iaflag` = 3, `_sdbeep`, then `_mgetc`
  until button 13. Window 34 is not closed: `_dsetup`'s Command panel stays drawn (the s135 XEiJ shot during an event)
  but its buttons, still registered, return their own numbers and are ignored. The remaster draws the bar as an
  18-row strip under the text box (white, the black line, "Space" centred; a click presses Space; lit ^ 5 under the
  pointer), the box keeps its lines (it scrolls), and `_dsetup`'s panel stays with no clicks
  (`parity/shots/p4-remaster-ewait-bar.png`). The strip's 18 rows (the label's cell runs 2 rows past the window's 96)
  and its place under the remaster's text box are the remaster's `[~]`.
- **INPUTS / INPUTN** (`logic-specs/events.md` 4.1): `_wfbox(msg, 14, 0, 80, 304, 16)` over the same rows, the prompt at
  (16, 80); the remaster draws the band in the same strip, colour 14, with the prompt, the typed text (font 0) and the
  1-pixel cursor bar, and leaves the prompt's line out of the box (`parity/shots/p4-remaster-typed-band.png`).
- **`_sdbeep` (`0x36078`) is not a beep:** it is `_voice(2)`, and `_voice(n)` (`0x35FBC`) plays sound effect n with
  `_a_play` (IOCS `_ADPCMOUT`, the OKI MSM6258 ADPCM) when sound effects are on (`0x789C5` = 1): from `VDATA`, which
  `_vinit` reads whole (6609 B) at start into `0x8E556`; effect n starts at the sum of lengths 0 … n − 1 of the table at
  `0x4A338` (201, 501, 501, 501, 201, 201, 1001, 1001, 2501: they sum to 6609), and plays length n − 1 bytes, at rate 1
  (5.2 kHz), both channels. So `_sdbeep` = bytes 702–1201 of `VDATA`, 1000 samples, about 192 ms. The others:
  `_noise` 0, `_bsetr` 1, `_sdnoi` 3, `_sdwlk` 4 / 5 by turns, `_sdatk` 6, `_sddam` 7, `_sdmag` 8. `_sdbeep` callers:
  `_ewait`, `_exe_ev` (INPUTS, INPUTN), `_xmenu`, `_common_proc`, `_deadmsg`, `_endmsg`, `_endstory` (twice),
  `_oencgo`. s152: the remaster plays them (section 6.13, with every caller).
- **`_numin(text_win, pad_win, x, y, digits)` (`0x309E4`)**, only caller `_withdraw`'s `_numin(bank, 34, 40, 224, 5)`:
  `_whole(pad, x − 8, y − 8, 72, 92)`; `_font(0, 0, 0, 4)`; twelve keys from the 12-byte table `0x472AA` {character, key,
  button}: 7 8 9 / 4 5 6 / 1 2 3 / B 0 R at (x + 20 · column, y + 20 · row), each `_wputc` " c " (black on colour-4
  cells) and `_setbtn(button, pad, x', y', 16, 16, key, 64, 1)`; buttons 12 / 13 (keys 29 and DEL) on B's square; 14 =
  the screen. The cursor is `_fbox(0, cx, cy, 8, _ylen)` at the bank window's text cursor: a black 8 × 16 block, drawn
  before each `_mgetc` and erased on a key (no blink). A digit (buttons 0–9) adds while fewer than `digits`; 11–13 erase
  one; 10 (R, Return) ends; then `_wfbox(pad, 14, x − 10, y − 10, 76, 96)`. Window 34 is the Bank's panel: its icons stay
  drawn, inert (`_withdraw` ran `_clrbtn`). The remaster draws the keypad there (a click on a digit types it, B =
  Backspace, R = Return; lit ^ 5 under the pointer) and the cursor as a steady block 8 pack pixels wide in the line's
  colour (the remaster's box is not grey, so not black `[~]`) (`parity/shots/p4-remaster-bank-keypad.png`). The colour-4
  cell box behind each key is taken as wide as the three glyphs' advances (4 + 8 + 4 = 16) `[~]`.
- **The shipwright's window (`_shipyard` `0x5E4E`):** `_wopen(-1, 160, 32, 312, 352, 1, "船　大　工")`, `_font(0, 0, 0,
  16)`; SHIP.PKH (312 × 220) at its origin, so rows 220–351 are the window's grey (titled: 14). `_disppcg` prints
  "[original text] " at (68, 336), the gold by `_wdouti(win, 8, …)` (`_wdout` pads each leading zero of the 8-digit field
  with two spaces, one digit's width in font 0) and " GP". The offer "[original text]" at (16, 236); under 1500
  GP "」" and `_spc_wait`; else "｡ " and, with a ship already, "船を買うと", then "[original text]" at
  (32, 252) and the cursor to (32, 268); then "[original text]" at the cursor (without a ship: on the first line, as the
  XEiJ shot `p4-x68k-shipwright-offer.png` shows), `_noyes(win, 104, 300)`, and after it `_wprcen(win, 156, 300,
  "[original text]" / "[original text]")` and `_spc_wait`. YES / NO are black (`_noyes`'s `_font(0, 0, 0, 16)`).
  Remaster (s151): Core prints the offer's lines as the original does and, after the answer, "Thank you very much." /
  "Come again." and a SPACE wait (`P4TownLoop.Answer`; it went straight back to the street before); the Client draws
  the window's grey under the picture, the rows from (16, 236) / (32, 252) / (32, 268) on, the answer centred on x 156
  at y 300, the gold line, in font 0, black, and no longer prints them in the text column
  (`parity/shots/p4-remaster-shipwright-window.png`, `-shipwright-answer.png`, 1500 GP by a temporary preview bump, not
  committed). English rows wrap inside the window; each row past the original's three moves the YES / NO box, the
  answer and the gold 16 rows down and makes the window taller `[~]`. s158: the window's title bar ("船　大　工",
  `p4x.0x400d4`) and frame are drawn as the training hall's (`DrawTitledBeside`; `parity/shots/p4-remaster-shipwright-titled.png`,
  as `p4-x68k-shipwright-offer.png`); it also holds with a ship owned and 1500 GP (four English offer rows, one extra:
  `-shipwright-titled-ship.png`; the key help then goes into the column, as it did untitled). The other titled building windows (all `_wopen(…, 160, 32, 312, 352, 1, title)`,
  s158 list from the call sites: `_inn` 宿　屋, `_townent` Town Gate, `_mage` 魔法訓練場, `_bank` 銀　行, `_distribute` 分　配,
  `_armory` 商　店, `_setuse` [original text], `_trade` 交　換, `_setident` [original text], `_endstory` Phantasie IV, `_arathamus`
  [original text], `_banquet` [original text]; the Guild's ギルド (XEiJ s158) is opened elsewhere) still have no bar or frame in
  the remaster `[~]`: their titles are not in the pack yet.

`fl.pix` is read into memory the combat buffers share (`_fpixb` = `0x58194`, `_bkpixb` = `0x58196`), which is why
`_denc` calls `_dpixset` again after a fight. The 21 maps use floor 0–43, north 0–19 / 31 and west 0, 1, 2, 5 / 7 only, all
inside the files (s108 scan). Extracted by `tools/scripts/x68k_p4_pix.py extracted/graphics OPEN.PKH unpacked/disk2`
→ `extracted/graphics/dungeon/{floor,north,west,party}/NN.png`, `combat/{ground,obstacle,missile}/NN.png`,
`combat/sprites/NNN.png` (one p4mon record, its 5 frames left to right).

**The location-map view (`_idraw` `0x171CE`) `[C]`** is a **9 × 9 overhead view**, walls standing up as slanted slabs
(not a first-person 3-D view). `_idcopy` copies `_pinmap` cells (`_cmapx` − 4 … + 4, `_cmapy` − 4 … + 4) into `_pibuf`
(9 × 9 longs; outside 0–29 = 0xFFFF). `_calim` marks the cells the party sees in `_lookf` (9 × 9 bytes): the centre;
then from column 4 west while the cell's west picture is 7 (none; the cell then entered), east while the entered cell's
west picture is 7; from each of those cells of row 4 north while (y > 0 and the cell's north picture is 31) or it is 7
(the grate: sight passes north only; with y = 0 and picture 7 the loop steps to y = −1 and writes `_lookf` row −1, the
end of `_pibuf`, a slip), and south while the entered cell's north picture is 31; the same with rows and columns
swapped from row 4 (north / south along column 4, then west / east along each row). Drawing, origin `(_cvsx(160),
_cvsy(40))`, screen pixels: the area 304 × 320 is cleared (top strip 304 × 32, each row's left strip 16 × 32); for each
cell (column c, row r, row after row): not seen or 0xFFFF → a black 32 × 32 box at (16 + 32c, 32 + 32r); else north
picture (≠ 31) at (32c, 32r), west picture (≠ 7) at (32c, 32r), floor at (16 + 32c, 32 + 32r) (opaque), then bit 15 → a
black 4 × 32 box at (44 + 32c, 32 + 32r) with a colour-15 2 × 30 line at (45 + 32c, 33 + 32r), bit 14 → black 32 × 4 at
(16 + 32c, 60 + 32r) with colour 15 30 × 2 at (17 + 32c, 61 + 32r); at the centre the party (`_mpix`) at the floor's
place. Later cells' walls (masked) cover earlier floors: a north wall stands over the floor north of it.
`_prex` (only from `_dload`) sets bit 14 when the south neighbour's north picture is 2, 3 or 16–19 and bit 15 when the
east neighbour's west picture is 0 or 5 (the near edges of doorways); events that change pictures later do not redo it.
**After loading a save (s110) `[C]`:** `_save` writes only the six party files (no map), and loading in a dungeon runs
`_game` → `_dungeon` → `_dload`: the map is read fresh from `O_0xx` (event changes to walls and floors are gone unless
the map-load trigger redoes them from flags), `_dreset` advances the per-location flag rotation once more, and `_prex`
marks the fresh pictures. Remaster (user s111, built s112): rule option `p4.dungeon-reload`. `x68000` (every preset)
does the original's reload at the saved square and, like the original, saves in a location map only at the command
prompt (`_syscon` is reached only from the town, outdoor and `_dmain` menus); the facing is kept from the save `[~]` (the
party file holds no `_dire`: a load without a reboot keeps memory's, a cold start has 0). `saved-map` keeps the live map
words, the loop state and mid-event saves, and computes the marks from the saved words `[~]`.
`_ianim` (from `_mgetc` / `_mgetc2`, every 51st call) steps floors 19–21, 24–26, 27–30, rand(2) + 41 = 42–43 on 42–43 (every
51st cell), north 10–12, 13–15 and west 2–4 in place (it rewrites `_pibuf`, not `_pinmap`; only seen cells). It skips the
view rows above `_iaflag` (`0x4358C`): 0 = all rows (cleared by `_openg`, `_listen`, `_setyoko`, `_dchest`, `_try_ul` and
after the windows below), 1 = from row 1 (`_exe_ev`'s TITLE, `0x19638`, the location name's band; s135: never what
`_ianim` reads, as every key wait inside an event sets its own value first), 3 = from row 3 (a window over the view's top: `_ewait`, `_ctrap`,
`_dtrap`, the last two just before `_getwho`'s member pick), 9 = none (`_dmain` while it carries out a command, `_xmenu`, `_denc` while their menus are up) (s109;
`_dmain` corrected s119: it sets 9 only after `_mgetc` returns, `0x17c70`, and clears it again, so the view **does**
animate while the command menu waits for a key, timed below). Every `_idraw` starts with `_idcopy`, so each redraw (17
call sites: steps, turns, closed windows) puts the map's own pictures back; the pass counter `0x43590` and the 42–43
cell counter `0x43594` are never reset.
**Typed input in an event (s135) `[C]`:** INPUTS / INPUTN ("Letter>" / "Number>", `logic-specs/events.md` §4) read keys
with `_wscan` / `_wiscan`, which poll `_wgetc` / `_inkey2` (IOCS `B_KEYSNS` / `B_KEYINP` only), never `_mgetc`: the view
does **not** animate while the player types, whatever `_iaflag` holds, and nothing redraws it afterwards. Seen on XEiJ
(s135, `logic-specs/events.md` §4.1): 10 s at Town Hall's "文字＞" with the view's torches still, only the cursor bar blinking. The remaster
(s135, `P4DungeonAnimation.NextRows`) stops the steps for both prompts and keeps the view as it was after them.
Frame time: measured s119 (§6.10 "Dungeon `_ianim`"): a step every 51 passes of 5.53 ms plus the step's own drawing,
328–539 ms in the views timed (a west wall 7.16 ms a draw, s120); the remaster plays it (s120, Core `P4DungeonAnimation`, §6.10 "Dungeon `_ianim`").

**The combat field (`_opn_field` `0x26482`, `_obj` `0x22040`) `[C]`:** origin `(_cvsx(152), _cvsy(60))`, square (column,
row) at (48 · column, 40 · row) (`_dsp_exit`). Ground: `_e_finit(n)` loads bback record n into slot 0 and every square
draws it (in the order of table `_dia`); n = ship 0, desert 1, hills 2, forest and grassland 3, mist and Netherworld 4;
on a location map 8 when `_etile` = 1 (the party's square is not floor 3 / 31) else 3. A strip of colour 14, 336 × 20,
lies above the field (`_terr`). Figures and obstacles are drawn 16 rows higher (`_ofs2` = −32768), so a 48 × 48 figure
stands on its square and overhangs the square above. Per square: an obstacle kind k < 11 → bobj record k (kind ≥ 11 →
bback record k − 10: dead code, s135 `[C]`, see below); a figure from its sprite slot (members `_chpixb`,
monsters `_mopixb` group × 5) with `_dtb`'s frame for its facing; a flying figure (record +7) is drawn over the obstacle,
others under it. Facing (record +3; members start 1 `_enc_char`, monsters 0 from `_enc_init`'s clear) changes only in
`_b_face`, after each step (`_b_chauto`, the step's square delta) and for both sides of every attack and of the spells
that call it (`logic-specs/combat.md` §9.4: every monster cast; party Healing, Slow and most others do not) (`_f2f`:
`_b_chkdir` gives the delta toward the other, ±1, ±7, ±6, ±8): from 0: −7 → 1, −8 / −1 → 2, +1 / −6 → 3; from 1: +7 → 0,
−1 / +6 → 2, +1 / +8 → 3; from 2: −6 / −7 → 1, +7 / +8 → 0, +1 → 3; from 3: −1 → 2, −7 / −8 → 1, +6 / +7 → 0; other
deltas keep it. In a walled fight (location map, ship) `_dsp_exit` opens a window "Exit" (`p4x.0x442b8`) on the exit
square: `_wopen68(-1, _cvx(152) + 48 · column, _cvy(60) + 40 · row, _fsize("Exit") + 16, 16, 0, "")` (no title), then
`_font(-1, 2, 15, 16)` (font 2, colour 15, `_bgcol` 16 = no cell box), `_wprcen(win, width / 2, 2, "Exit")`, `_font(-1, 0,
0, 16)` and `_wind_clr` (the window's record is freed, its drawing stays) `[C]`. **Seen on XEiJ (s120) `[C]`**
(`parity/shots/p4-x68k-exit-window.png`, `-zoom.png`; a random fight in the Fishing Village, `_encf` / `_encper` poked):
**no box and no frame**, just the word "Exit" in white (colour 15, 234 grey) in the small **italic** font 2, straight on
the floor (the floor's pattern shows round and between the letters), centred in the square's top 16 rows: ink rows
4–12 of the 48 × 40 square (here column 6, row 2). **Font (s135) `[C]`:** "font 2" is not a font but `_font`'s second
argument, the style bits `_wfopt`: style 2 = italic of the current font 0 (§6.12). Drawn from §6.12's glyphs it is the
shot pixel for pixel; `_fsize("Exit")` = 25, so the window is 41 wide, `_wprcen` puts the text at x 20 − (25 >> 1) = 8
(ink from 9: the E's bottom row starts one pixel in) and y 2 (ink rows 2–10 of the glyph cell → square rows 4–12). The
remaster (s135, `P4DungeonScreen.BattleArt`, pack font role `p4.combat.exit` = `x68k-p4-font0-italic`) draws it so, in
pack pixels like the field (`parity/shots/p4-remaster-exit-italic.png`, `-zoom.png`); a pack without the role keeps the
upright `PixelFont`. Over a figure on the exit square not seen `[~]`.

**Unused field records (s135) `[C]`:** `_e_finit(n)` (`0x2679A`; loads bback record n with `_xrndread` and the whole
`bobj.pix`, then sets all 49 squares to obstacle 0xFF, walkable 1, saved 1, occupant 0xFF) has exactly 8 callers, all
with a constant or `_e_terrain`'s 3 / 8: ship 0, desert 1, hills 2, forest 3, grass 3, mist 4, Netherworld 4, location
map 3 or 8 (the only other byte match is its symbol-table entry). So **bback records 5–7 are never loaded**: 5 is water,
6 water with planks on its right, 7 planks with water on its right (a ship's side over water; no fight uses them).
The obstacle byte of a square (`0x7A8E4` + 4 · square) is written only by `_e_finit` / `_init_terr` (0xFF) and
`_e_obstacle(kind, count, walkable, fixed, square)` (`0x266C8`; `count` random squares 7–41 that are still empty, or
one fixed square), which has 16 callers, all with a constant kind: ship 1 (barrel); desert 2, 3, 4 (bush, cactus, sand
heap); hills 5, 6, 7 (stones, bush, boulder); forest 8, 9, 6 (trees, tree, bush); grass 2, 6; mist 10 (fire);
Netherworld 5, 7, 10. **No kind 0 and no kind ≥ 11 is ever placed**: bobj record 0 (a plain grey checker) is unused, and
`_bo1pix`'s kind ≥ 11 branch (bback record k − 10) is dead code. The remaster draws kinds 1–10 only, as the original.

**`IND<n>.PDT` are not pictures `[C]`:** `_dload` reads `"ind" + map number + ".pdt"` into `_pinmap`: the dungeon maps
(1922 B = 2 + 30 × 32 u16 cells). `_prex` / `_mkdin` turn them into `_dinmap` (`logic-specs/events.md`).

### 6.10 The opening and picture animation (s102) `[C]`

**`_openning` (`0x0`)**, called by `_main` after `_sysinit` / `_xprtck` / `_rinit` / `_vinit`, before `_disk(0)`:
`_cls(1)`; `0x7b8ae` := 0; `_m_inn`; `_music(99)` = **replay song 0**, the title tune (§6.11; the remaster plays it on a YM2151 model, s109);
`_wopen(32, 160, 64, 320, 216, 1,
"Phantasie IV")` — window 32 at layout (160, 64), **320 × 216 pixels** (x / y go through `_cvx` / `_cvy`, width and
height are stored as given), titled "Phantasie IV" (s110, XEiJ `[C]`: the title is a bar **above** the window's area,
21 rows on screen 91–111 over the picture's first row 112: a black line, 18 grey rows with the title centred (glyph rows
96–104), a grey line, a black line; the frame's side lines run at x 215 / 536 around the 320 columns 216–535, its bottom
line at 328 right under the credit's 16-dot row 312–327, a 1-pixel shadow right and below); `_wprcen(32, 160, 84, "STARCRAFT Presents")` (centred on window x
160, window y 84; `_wloc` adds the window origin); `_setpage(1)`, `_xunpack(0, 0, 0, "OPENANI.PKH")` (the frame sheet to
graphics page 1); `_setpage(0)`, `_xunpack(0, 160, 64, "OPEN.PKH")` — the 320 × 200 picture at the window's origin,
over the "STARCRAFT Presents" line; `_clanim`, `_setanim(160, 64, 0, 0, 27, 29)`; `_wprcen(32, 160, 200, "By Winston
Douglas Wood")` — the line under the picture (200 + one 16-dot row = the window's 216). It returns; `_main` goes on to
`_disk(0)`, the New Game / Saved Game icons, which waits for a pick with the title still up (manual ch. 1). No timer
anywhere. Strings: `p4x.0x3f12b` / `0x3f138` / `0x3f160` in `GameData/phantasie4/strings/`.

**`_disk(0)` (s151, static + XEiJ shot `parity/shots/p4-x68k-disk-menu.png`) `[C]`:** `_disk` (`0x1C7E`) calls
`_diskmenu(mode)` (`0x1AB4`) and then **one** `_mgetc`. `_diskmenu`: `_clrret(5, 0x3F5C8)` (button ids −1), `_mkicon(1,
0x3F5C8 + 32 · mode, 1, 0x3F6BA "Select File")` (window 34, section 6.9 "Command panel"): mode 0 = "N:New Game" (icon.pix 0,
key N), mode 1 = " SPACE" (icon 20). Then for k = 1..3 the table entry k + 1 ("*k:Save", icon 1, key 'k'): `_xexfile("fames.k")`;
mode 0 shows it only when the file exists, at the next zigzag place (x 38 / 62, y 60 + 52 j, j = saves shown before
it: the panels' place j + 1); mode 1 shows all three and first writes '*' (saved) or ' ' over the label's first byte,
then `_wkprcen(34, 68, 308, "[original text]")`. `_mgetc` returns only a button (`_inkey3` upper-cases the key and looks it
up in the button table, else −1 and the loop goes on), so: New Game → `_load(0)` (the shipped start set), slot k →
`_load(k)` (mode 1: `_save(k)`), mode 1's SPACE → back; no other key does anything. In mode 0 the label keeps the
table's '*' (drawn in colour 2). On screen (XEiJ): window 34's bar at rows 73–93, its area 585–720 × 94–417, the title
window's bar at 91–111: both windows as `_wopen` places them. **Built (s151):** the splash's picture step carries the
panel (`title_sequence` step `"menu": "disk"`, the panels file's `"disk"`, `x68k_p4_panels.py`), drawn as the Command
panel at canvas (369, 0) of a 506 × 347 canvas = screen (216, 73)–(722, 420) (the title window and window 34 where the
original puts them); it waits for N, the save's digit or a click on an icon (Esc on "STARCRAFT Presents" skips to it;
nothing else ends it). New Game = the remaster's new game, the save icon = F9's load (an unreadable save starts a new
game with the reason in the note). The canvas is grey: `_openning` starts with `_cls(1)` =
`_fbox(1, 0, 0, 768, 512)` (pkh colour 1, (90, 90, 90) on XEiJ), so the pack's `title_sequence.background` is now
`#616161` (black before s151). `[~]`: the remaster keeps one IV save, shown as "*1:Save" (slots 2 and 3 never show);
window 32's bar and frame lines are not drawn (as before). **Scale (decided, user s152: fractional fit for the
title canvas):** the canvas grew from 320 × 238 to 506 × 347, so an integer fit dropped it to 2× on a 1920 × 1032 client
area (3× only from 1041 rows). **Built (s152):** the title splash's canvas (every game's `title_sequence`, one code path)
is presented at the largest fractional scale that fits, aspect kept, centred, point sampled
(`VirtualScreen.FractionalFit` / `FractionalPresentRect`; the mouse maps through the same rectangle): 1505 × 1032 at
x = 207 there (≈ 2.97×). In-game screens keep integer scaling. Shots: `parity/shots/p4-remaster-disk-menu.png`,
`-disk-menu-saved.png` (s151, 2×), `p4-remaster-title-fractional.png` (s152).

Coordinates (`_cvsx` / `_cvsy`, `0x31c82` / `0x31cd0`): the program lays out on 640 × 400; with `_stype` = 0 (the
768 × 512 screen) a non-zero x becomes `x · 704 / 640 + 40`, y `y · 456 / 400 + 40`; pictures are not scaled.

**Animation** (`_clanim` `0x2e6c8`, `_setanim` `0x2e6f8`, `_anima` `0x2e7f8`; s109 re-read) `[C]`:
`_setanim(dx, dy, sx, sy, first, last)` sets the display origin `_adis` (through `_cvsx` / `_cvsy`), the sheet origin
`_asou` (where the sheet was unpacked on graphics page 1) and arms records first..last. **Animation records** `0x46174 + n
· 84` (45 used; `_anima` scans 48): +0 x, +4 y (offsets from `_adis`), +8 width, +12 height, +16 the current position (0
in the file), +20… frame ids ending −1. **Frame records** `0x45764 + f · 16` (161): +0 sheet x, +4 sheet y (from
`_asou`), +8 base, +12 random range. Arming sets the record's count to `(rand(range) + base) >> 2` of the frame at its
current position (rand = 1..range, `_rand`) and draws nothing. Each `_anima` call walks records 0–47 in order; an armed
record whose count **was** 0 (post-decrement test) copies its current frame (`_ggcopy`, page 1 → page 0, width ×
height, x in 8-pixel columns: `(x >> 3) · 16` bytes), sets the count to that frame's `(rand(range) + base) >> 2`, and
steps on (−1 wraps to 0). So a frame's base / range is how long it stays up, and the first change comes after the next
frame's own wait (the plain picture until then). `_clanim` only disarms: the position (+16) is never reset, so a
picture seen again carries on where it stopped. All of it in the pack's `picture_animations`
(`tools/scripts/x68k_p4_anim.py`, `--check` pins it to P4.X).

Which picture arms which records (`_setanim` call sites; the frame sheets `*_AN` / `*ANI` decode with
`x68k_p4_pkh.py` → `extracted/graphics/anim/`):

| Picture (call site) | `_adis` = the picture's origin | Sheet → page 1 at | Records | Frames |
|---|---|---|---|---|
| Title `OPEN` (`_openning` `0xF8`) | (160, 64) | `OPENANI` (0, 0), 72 × 93 | 27–29 | 112–120 |
| Street, town type 0 / 1 / 2 (`_townmenu` `0x28D8`, table `0x3F82C` + 24 · `_towntyp`) | (160, 20) | `TOWN_<n>AN` (0, 0), 224 × 336 / 240 / 336 | 0–3 / 4–7 / 8–14 | 0–21 / 22–36 / 37–58 |
| Guild (`_guildmenu` `0x47AC`) | (160, 64) | `GUILD_AN` (240, 0), 144 × 144 | 33–35 | 132–141 |
| Inn (`_inn` `0x257C`) | (160, 32) | `INN_AN` (240, 0), 144 × 192 | 20–23 | 88–101 (98 unused) |
| Bank (`_bank` `0x59F4`) | (160, 32) | `BANK_AN` (240, 0), 144 × 144 | 24–26 | 102–111 |
| Shipwright (`_shipyard` `0x5F6E`) | (160, 32) | `SHIP_AN` (240, 0), 144 × 144 | 30–32 | 121–131 |
| Mystic (`_mystic` `0x6F52`, `0x7074`) | (160, 90) | `MYSAN` (240, 0), 160 × 275 | 15–18 | 59–84 |
| Mystic's reading (`_mystic` `0x6C2A`) | (160, 90) | `MYSAN` | 19 | 85–87 |
| Ending street `END1` (`_endset` `0xB34C`) | (160, 154) | `END1ANI` (240, 0), 96 × 336 | 36–42 | 142–155 |
| Banquet `END2` (`_banquet` `0xAEB8`) | (160, 32) | `END2ANI` (240, 0), 64 × 28 | 43 | 156–157 |
| Cell `END3` (`_arathamus` `0xADCA`) | (160, 32) | `END3ANI` (240, 0), 72 × 33 | 44 | 158–160 |

The Mystic's visit (`_mystic` `0x6F78` → `0x6BF0`, s115 re-read) `[C]`: `_wcls(34)`, `_clrtdraw`, `_wopen(-1, 160, 90, 312,
220, 1, "占　師")` (the window is the picture's size), `MYSAN` to page 1, `MYSTIC` at (160, 90), `_clanim` + `_setanim`
15–18; `_anima` 10 000 times in a bare loop; `_setanim` 19 (40 × 21 at (136, 110), frames 85–87; no `_clanim`, so 15–18
keep running) and 3000 more calls; `MYSTLK` unpacked at (160, 90) over the picture's top; the score (towns.md §7) printed
in the bubble: `_wlocpr(win, 64, 16, "[original text] ")`, `_wdouti(win, 132, score)` (132 = 0x84 is `_wdout`'s format,
not an x: low 6 bits = 4 digit places, bit 7 = leading zeros dropped with no padding, so the number is printed left-aligned
at the text cursor; s120), `_wprint(" じゃ、")`, the verdict
`_wprcen(win, 156, 40, …)` (window pixels; byte `0x789AC` is `_bgcol`, set to 15 around the text and 16 after: the
window text writers `_wprint` / `_wputc` / `_wkprint` fill each character cell with `_fbox(_bgcol, …)` first when `_bgcol`
< 16, so 15 = a cell box of text colour 15, 16 = none `[C]`; the score's digits in colour 2 (`0x3A2F6` = 2 around
`_wdouti`)); **3000
more bare calls** (record 19 still armed); `_clanim` + `_setanim` 15–18 (record 19's last frame stays on page 0) and
`_spc_wait` (the input loop, a key); `_wclose`, `_clanim`. None of the three counted loops reads a key. The remaster plays
it as Core `P4MysticReading` (s115): box and key help empty while the loops run, the bubble with the two lines as below. **Seen on XEiJ (s120) `[C]`** (`parity/shots/p4-x68k-mystic-bubble.png`; New Game, one
Human Thief; the reading is free: `_town` jumps straight to `_mystic` (`0x3320`) and neither tests or takes gold, as
Core; score 2 → "[original text]"): line 1 is **not centred**, it starts at window x 64 (ink x 64–228, rows 17–28): "汝のスコ
アーは" (ink 64–173, the window font is proportional: `_scprsub` advances by each glyph's width), the score in red right
after the space (ink 181–186 for "2"), then " じゃ、" (ink to 228); with `_wdout`'s unpadded format a longer score pushes
" じゃ、" right by its extra digits. Line 2 (the verdict) is `_wprcen`'s, centred on x 156 by the string's width (ink
93–211, rows 41–52). The bubble (part of `MYSTLK`) spans window x
40–262 (centre 151), rows 8–67, its tail below at x ≈ 187–204 down to row 77. The colour-15 cell boxes do not show: text colour 15 is the bubble's own
light grey (234, 234, 234 on XEiJ; the same colour 15 draws the field's "Exit" label, §6.9), so the glyphs simply sit on
the bubble. `_mystic` sets `_font(0, 0, 0, 16)` (`0x6F96`): font 0, style 0 (upright), colour 0; the kanji come from
the CG ROM (`_wkfnt`), the ANK characters and the score's digits from P4.X's font 0 (§6.12; the red "2" on the shot is
its glyph exactly, s135). The remaster (s120; font s135) draws line 1 from x 64, the score's digits in colour 2, the
verdict from x 156 − width >> 1 (`_wprcen`), in font 0 at pack pixels (pack font role `p4.mystic.bubble`, English
mixed case; `parity/shots/p4-remaster-mystic-bubble.png`); glyphs font 0 lacks (kanji) fall back to the pack font. **Doors** (`_animdoor` `0x303B4`, from `_town`
`0x32F6`): before a building is entered (index = `_bvs`'s pick 0–5; 5 on a type-2 street becomes 6), entry `0x470FA` +
16 · (7 · `_towntyp` + pick) = (x, y, sheet x, sheet y): one 40 × 80 copy of the open door from the street sheet's
right-hand strip (x 144 / 184), then a busy wait of 3 × 30 000 loop passes. Its zero entries (pick 6 on every street, so also type 2's
remapped pick 5) are never read: `_town` calls `_animdoor` only for picks 0–5 (`0x32D2` / `0x32D8`) `[C]`. **`NTA`:** P4.X started with the argument `NTA` (`_main` `0x1B6`, string `0x3E048`)
clears `_town_anim` (`0x7B8AA`, else 1); the street sheet load (`_tload` `0x2B6C`), the street, Guild, Inn, Bank and
Shipwright animations and the doors check it; the title, the Mystic and the endings do not.

The opening's records 27–29 (the torches; `OPENANI.PKH` is 72 × 93 = 3 × 3 cells of 24 × 31):

| Record | Display x, y (in OPEN) | Size | Frames (sheet x, y; base, range) |
|---|---|---|---|
| 27 | 0, 60 (left torch) | 24 × 31 | 112–114: (0, 0), (24, 0), (48, 0); 100, 50 |
| 28 | 280, 60 (right torch) | 24 × 31 | 115–117: (0, 31), (24, 31), (48, 31); 100, 50 |
| 29 | 32, 60 | 24 × 31 | 118–120: (0, 62), (24, 62), (48, 62); 300, 300 |

The count is set to `(rand(range) + base) >> 2` and the change fires on the pass that finds 0, so a frame stays up for
that count + 1 `_anima` calls: a torch frame 26–38 (`(rand(50) + 100) >> 2` = 25–37), record 29's 76–151.

**Timing (s109) `[C]` unless tagged.** P4.X never waits for the display. Its IOCS calls are only the XC library stubs
(`_APAGE`, `_B_CLR_AL`, `_B_DRVCHK`, `_B_KEYINP`, `_B_KEYSNS`, `_B_READ`, `_B_SUPER`, `_CRTMOD`, `_B_CUROFF`, `_GPALET`,
`_G_CLR_ON`, `_MS_*`, `_SKEY_MOD`, `_TIMEBIN`, `_TIMEGET`, `_TPALET2`, `_VPAGE`, `_DRVCTRL`) plus `_wkfnt` (`_FNTGET`),
`_a_play` (`_ADPCMOUT`), `_beep` and the OPMDRV `_m_*` calls: every `trap #15` in the text sits in one of those. There is
no `_VDISPST`, `_ONTIME`, `_TIMERDST`, `_B_INTVCS` (no interrupt vector is hooked), no absolute access to the MFP (`$E88000`)
or CRTC (`$E80000`) in hex or decimal, and `_TIMEGET` / `_TIMEBIN` only seed `_rinit` (`0x35CDE`). The CRT mode is
`_CRTMOD(0x110)` (mode 16, 768 × 512, 31 kHz; §6.9), but as nothing waits on Vsync its refresh rate times nothing. Every
delay is CPU-bound:

| What | Unit | Count |
|---|---|---|
| Picture animations (`_anima`) | one pass of `_mgetc` / `_mgetc2` (`_anima`, `_ianim`, `_mcget` → `_s_ms_move_on` (→ `_s_ms_trg_on` → IOCS `_MS_GETDT`) / `_s_ms_get_cp` → IOCS `_MS_CURGT`, `_inkey3` → `_inkey2` → IOCS `_B_KEYSNS`, `_onbtn`; more when a key or button is down) | frame's `(rand(range) + base) >> 2` |
| Mystic's reading | a bare `_anima` call | 10 000, then 3000 with record 19, then 3000 with the bubble up |
| Location-map cycling (`_ianim` `0x175BC`) | every 51st `_mgetc` / `_mgetc2` pass (counter `0x43590` > 50) | one step per seen cell (§6.9 "location-map view"); measured s119 (§6.10 "Dungeon `_ianim`") |
| Combat figure redraw (`_e_drpos` `0x22270`) | loop pass whose bound is an FPACK `__CLMUL` (1 · 1000) | 1000, between `_obj` phase 1 and phase 0; only with argument 2 ≠ 0 (else phase 1 is drawn and it returns, `0x222E8`) |
| Missile (`_shoot` `0x29296`) | plain `for` loop pass (`addq.l` / `move.l` / `cmp.l #` / `blt`) | per 8-pixel step: `_flpix`, 1000, `_ppp` restore; kind 20 (the beam): 16 `_xbox` lines × 400, then 1000 |
| Door (`_animdoor`) | plain loop pass | 3 × 30 000 |
| Battle message wait (`_bwait` `0x202DE` → `_apalwait`) | plain loop pass | speed (`0x789C3` / `0x789C4` while `0x7B8B6`, 0–10, `_timelag`) × 35 × 200 |

An `_mgetc` pass runs IPL-ROM IOCS code (`_B_KEYSNS`, `_MS_GETDT`, `_MS_CURGT`) whose length cannot be
counted without the ROM, so statically the real frame time is unknown; it was measured instead (below). The busy loops are pure P4.X
code (for the plain loop 24 + 16 + 14 + 10 = 64 cycles a pass on a 68000, i.e. 6.4 ms per 1000 at 10 MHz with no wait
states `[~]`), but the drawing around them (GVRAM access waits), interrupts and the machine's clock (10 MHz X68000,
16 MHz XVI, 25 MHz X68030) change the real time, so those are `[~]` lower bounds at best and are not used. The only way
to close it is a full-system run (an X68000 emulator with the IPL and CG ROMs, at 10 MHz) timing the title torches'
frame changes: done in s110 (below).

**PC-98 cross-check (s109).** The PC-98 `P4.EXE` (disk 1, 1990-09-14, Microsoft C, EXEPACK-packed; DGROUP paragraph
`0x6B97` in the unpacked image) holds the same 161 frame records as 16-bit words at DGROUP `0x1B5C` (image `0x6D4CC`) and
the same animation code (`_setanim` image `0x642`, `_anima` `0x6DF`, 48 records of 42 B at DGROUP `0x2064`), with one
difference `[C]`: **no `>> 2`**, the count is `rand(range) + base`, so a PC-98 frame lasts 4 × the X68000's passes. Its
callers are the same kind of loops (the input loop at image `0x247C`: `_anima`, `_ianim`, an `outp(0x7FDD, …)` around the
mouse read; the Mystic's 10 000 / 3000 at `0xF517` / `0xF547`), and no Vsync or timer wait was seen in them `[~]` (a short
read, not a full search). So the PC-98 does not give a real frame time either; the factor 4 only says Star Craft scaled
the counts per machine.

Extracted: `extracted/graphics/title/OPEN.png`, `OPENANI.png` (`tools/scripts/x68k_p4_pkh.py`), pack
`GameData/phantasie4/graphics/x68000.json` (`title_sequence`; the town pictures: §6.9; s109 the sheets `anim/*.png` as
`p4.anim.*` and `picture_animations`). The remaster's title splash shows "STARCRAFT Presents" and then the static
picture with its credit line, each until a key (the load time and the menu wait have no fixed length); the torches are
animated since s110 (below). s151: the picture step is `_disk(0)`'s panel too (above) and ends only on its pick; the title
tune plays on under it.

**"Welcome!" (s151, `_tload` `0x2AB4`–`0x2C24`, static + XEiJ) `[C]` unless tagged:** `_tload` runs once per `_town`
entry (every arrival, a load in town: `_cmovf` = 2 skips only `_townent` / `_welcome`) and for the ending (`_tload(1)`);
returns from a building redraw the street from `_paste_buf` instead. It opens window 32 (`_tw_open`: (160, 20), 312 ×
348, untitled; `_fbox(14)` round it and `_whole`'s sunken frame), `_whole(32, 4, 80, 304, 16)` and "Please Wait..."
(`0x3FA38`) at (156, 81) every time, then `TOWN_nAN.PKH` goes to page 1 (that load only with argument 0 and
`_town_anim`); then `_wcls(32)`,
`_font(0, 2, 15, 16)` and `_wprcen(32, 156, 81, "Welcome!")` (`0x3FA47`, font 0 italic, colour 15) on the cleared
window (colour 1 on the XEiJ shot `parity/shots/p4-x68k-welcome.png`, ink rows 145–153 centred on screen x 372), then
`_xunpack(TOWN_n.PKH)` at the window's origin covers it. No key is read on the way. **Timing (XEiJ, `XEIJ_PCTRACE` on
the draw and on `_xunpack`'s steps, New Game into Pendragon):** "Please Wait..." 1373.7 ms (AN sheet read 283.4,
decoded 592.4, copied 222.6, then `_wcls` 274.5), "Welcome!" alone **1136.5 ms** (`_xread` of the 49 590-byte TOWN_1
282.5, `_scst_stacf` decode 854.0), then `_scst_displays` copies the picture in 319.2 ms. `[~]`: XEiJ reads the floppy
at about 175 KB/s, faster than a real 2HD drive, so on floppies both texts stay longer; a hard-disk install
(`HD.BAT`) reads fast too. **Built (s151):** the pack's `picture_animations.welcome` (`x68k_p4_anim.py`: at (156, 81),
back 1, colour 15, hold 1137 ms, cover 319 ms); on each `_tload` (Core `P4TownLoop.Loads`, or a new game / load in town)
the Client draws the street's area in colour 1 with "Welcome!" in the pack's italic font 0 for 1137 ms, reads no keys
and shows no panel meanwhile, then the street (`P4DungeonScreen.Welcome.cs`, `parity/shots/p4-remaster-welcome.png`).
**"Please Wait..." (s156, static + XEiJ shot `parity/shots/p4-x68k-please-wait.png`) `[C]`:** `_tload` (`0x2AFE`)
runs `_clrtdraw`, `_tw_open` (window 32 at (160, 20), 312 × 348, flag 0: no fill; `_fbox(14)` round it from (152, 12),
328 × 364, and `_whole(32, 0, 0, 312, 348)`), `_whole(32, 4, 80, 304, 16)`, `_wprcen(32, 156, 81, "Please Wait...")`; the
shot shows the window grey (colour 14), the box's two rows / columns of colour 1 above / left and colour 15 below / right
(the dark ones reach the outer corners, one pixel shorter on the inner row / column), the text in plain font 0 (ink 87
pixels wide, font 0's), colour 0. Then the AN sheet (argument 0 with `_town_anim` only) and `_wcls(32)`. **Built
(s156):** the pack's `welcome.please_wait` (`x68k_p4_anim.py`) and the Client draws it before "Welcome!" for 1374 ms
(the sheet's read, decode and copy and `_wcls`, measured above), 275 ms (the `_wcls` part alone `[~]`, not timed on its
own) for the ending's `_tload(1)`, which loads no sheet (`parity/shots/p4-remaster-please-wait.png`, the hold lengthened
in the build's copy for the shot). `[~]`: the picture appears at once (its 319 ms copy is not animated); the text box
already lists the street's menu during both holds; `_tw_open`'s grey border and sunken frame round the window are not
drawn: s158 measured against the s157 layout: the street's 312 × 348 window with `_fbox(14)`'s 8 pixels round it is
328 × 364 pack pixels = 182 layout rows, so even with its top in the screen's top margin its bottom (182) passes the
178 the street's two-row key help needs under it; the help would go back into the column (5–7 rows). A layout
question for the user, not built `[~]`.

**Measured (s110, XEiJ 0.26.10.08; `docs/plan/RE-REFERENCE.md` "X68000 full-system runs") `[C]`.** Model "Shodai"
(the original X68000: MC68000 at 10 MHz, IPL ROM 1.0 — not XEiJ's IPL 1.6 — wait cycles on), the game booted from the
two `.dim` disks. GIF recordings (one frame per emulated vsync: CRTC mode 16, 69.5519 MHz / 2 / (138 · 8) / 568 lines =
55.46 Hz, 18.032 ms) were timed against the frame tables by `tools/scripts/xeij_anim_timing.py`: each record's changes
are matched to its frames on the sheet and aligned with the frame order, and pass = vsyncs · 18.032 ms / the expected
passes walked (`((rand(range) + base) >> 2) + 1`, rand = 1..range), using the frames whose life varies little (range 1
frames are exact). Mouse cursor resting off the picture. Since s117 / s118 every scene is also timed **directly** (the
mean interval between `_anima` calls, below); the pack uses the direct values:

| Loop (scene) | s110 GIF pass | Frames used | Direct pass (s117 / s118) | Passes |
|---|---|---|---|---|
| `_disk(0)` New Game / Saved Game over the title (`title`) | 5.75 ± 0.07 ms | 96 torch frames | **5.83** ms (s118; two boots 5.832 / 5.834; s117 5.83) | 7718 |
| `_townmenu`, a party in town (`p4.street.0`, Pendragon) | 6.64 ± 0.03 ms | 205 fixed (range 1) frames | **6.69** (6.685) | 6739 |
| `_townmenu`, no party yet (Pendragon) | 6.48 ± 0.04 ms | 106 fixed frames | 6.55 (6.546) | 6875 |
| `_townmenu` over `TOWN_2`, a party (`p4.street.1`) / no party | — | — | **6.38** (6.377) / 6.25 | 9413 / 7198 |
| `_townmenu` over `TOWN_3`, a party (`p4.street.2`) / no party | — | — | **6.80** (6.799) / 6.65 | 8827 / 6770 |
| `_guildmenu`, a party (`p4.guild`) / no party | 6.27 ± 0.12 ms | 40 | **6.04** (6.043) / 6.00 | 7453 / 7506 |
| `_inn`'s message wait (`p4.inn`) | 6.13 ± 0.07 ms | 105 | **6.06** (6.062) | 7431 |
| `_bank`'s menu (`p4.bank`) | 6.16 ± 0.10 ms | 40 | **5.95** (5.948) | 7576 |
| `_mystic`'s message wait (`p4.mystic`) | 6.12 ± 0.03 ms | 54 | **6.12** (6.124) | 7361 |
| `_mystic`'s reading: a bare `_anima` call (`p4.mystic.reading`) | 1.435 ± 0.011 ms | 366 | **1.359** (loops 1.330 / 1.405 / 1.409) | 16 000 |
| `_shipyard`'s yes / no offer, `_noyes` (`p4.shipwright`) | (6.36 ± 0.07) | | **6.06** (s117) | 11 427 |
| `_shipyard`'s message wait, `_spc_wait` (under 1500 GP) | (6.12 ± 0.06) | | 5.98 (s117) | 11 582 |
| `_endmsg`'s wait over `END1`, records 36–42 (`p4.ending.street`) | (7.40 ± 0.06) | | **7.39** (s117) | 9376 |
| `_common_proc`'s wait over `END2`, record 43 (`p4.ending.banquet`) | (5.59 ± 0.08) | | **5.64** (s117) | 6209 |
| `_endmsg`'s wait over `END3`, record 44 (`p4.ending.cell`) | (5.53 ± 0.10) | | **5.53** (s117) | 6332 |

So a torch frame stays 150–220 ms (26–38 passes of 5.83 ms), and the Mystic's visit (10 000 + 3000 + 3000 bare calls)
takes 13.3 + 4.2 + 4.2 s (plus 0.35 s for the bubble's drawing between the last two loops) before the key wait. The pass
changes with the X68000 cursor's place (the street with a party, s110 GIF: 5.74 ms with the cursor resting on a door's
hit area, 6.88 on the picture elsewhere; why is not read `[~]`, not re-timed directly) and slightly with the party (the
street about 0.12–0.15 ms longer with one, the Guild 0.04); the remaster uses the direct values with the cursor off the
picture and a party in town. The dungeon's `_ianim` was measured in s119 (below, "Dungeon `_ianim`").

**Re-timing (s118, XEiJ) `[C]`.** The GIF values above came from a frame model and short recordings; s118 re-timed
every s110 scene the way s117 did the Shipwright and the endings (`XEIJ_PCTRACE=7bcd8`, the mean interval between
`_anima` calls over 45–60 s of one idle input loop, mouse cursor parked off the picture). The **mean**, not the median:
the median (5.5–5.9 ms; 0.72–0.73 ms in the Mystic's bare loops) is a plain pass, while the mean carries the passes
that copy a frame (up to 27–39 ms), and real time turns into passes at the mean. Title, Pendragon, Guild, Inn, Bank and
Mystic were played in one normal boot: New Game, then one Human Thief made in the Guild (`g` roster, `n`, `n`, race 1,
class 1, Okay, a name) and taken as the party (`p`, `a`, SPACE, Okay, no elemental), so unlike s117 the game's own
songs played (the title tune on the title; from New Game on the town song, `_m_play` again on each Guild roster
visit). The **street types 1 / 2** have no type-1 / type-2 town reachable from a fresh boot, so the town-type table
`_twtyps` (image `0x3F70E`, by map; Pendragon is map 1) was poked at the title (`XEIJ_PCHOOK=4f15e,4f15e`, i.e. a poke
with no jump the first time `_disk` runs; `XEIJ_POKE=8cbef=1` or `=2`): New Game then puts Pendragon (its name and song)
on `TOWN_2` / `TOWN_3` with that type's sheet, records and doors, the same `_townmenu` loop the real towns run `[~]` (a
real Shipwright / plain town not visited). Against s110: title +1.4 %, street 0 +0.7 %, Inn −1.1 %, Mystic 0, Guild
−3.6 %, Bank −3.4 %, the reading −5.3 % (s110 timed record 19's frames, which run in loops 2 and 3 only, 1.405–1.409
ms a call there; the pack keeps one value for all three loops so the visit's total is right). The old `[~]` 6.64 for
streets 1 and 2 was 4.1 % too long / 2.3 % too short.

**Shipwright and endings (s117, XEiJ) `[C]`.** Neither is reached in a fresh boot (the Shipwright stands only in type-1
towns, the endings need the Skull Fortress flags), so a patched XEiJ (`tools/scripts/xeij_trace_build.sh`,
`XEIJ_PCHOOK`) sent `_main`'s `_disk(0)` call (runtime `0x4f15e`; P4.X loads at `0x4d4e0` in the two-disk boot) to
`_shipyard` (`0x5332e`; party gold `0x789E4` poked to 1500 for the offer, left 0 for the "not enough" message) or to
`_ending` (`0x58858`; flags `0x78B20`–`0x78B23` = 1, `0x78B24` = 0: the 200 ending, so `END1`, the banquet and the
cell in turn, a SPACE per message page). Cursor off the picture as above; no party, so the ending's member lines are
empty; the title tune plays under all of it (no `_music` call on the way), where in play the town / ending song
would (the OPM interrupt's share of the pass differs by song `[~]`, small). **Timed directly**: `XEIJ_PCTRACE` logs
every call of `_anima` (runtime `0x7bcd8`) with its emulated time, and the pass is the mean interval over about 35–70 s
of one wait (rows "direct" in the table; no frame model, no GIF). The intervals are uneven: 5.0–5.6 ms for a plain pass
(the median), up to 27 ms (Shipwright) or 37 ms (`END1`) for a pass that copies a frame, so `END1`'s seven 48 × 48
records make its mean the longest (7.39). Check of the method on the title's `_disk(0)` wait: **5.83 ms** direct
(11 878 passes) against the s110 GIF's 5.75 ± 0.07. The GIF method (`xeij_anim_timing.py`) on the same scenes: the
endings agree (street 7.40 ± 0.06, banquet 5.59 ± 0.08, cell 5.53 ± 0.10 over all gaps), the Shipwright reads 5 % high
(offer 6.36 ± 0.07, message 6.12 ± 0.06 from its low-variance frames, which are short and carry their own copy's long
pass) `[~]` why exactly; the s110 values above were re-timed directly in s118 (above). The pack's `p4.shipwright` takes the offer's 6.06 (the screen a party with the money sees first).
Two fixes to `xeij_anim_timing.py` came with it: the ending street's display origin was offset twice (its scene `at`
is the Client's offset of `END1` in the street picture, not an offset from `_adis`), and a copy that the recording's
end cut or that stood over two vsyncs (an OPM interrupt inside the `_ggcopy`) is dropped instead of breaking the frame
order. XEiJ 0.26.10.08 on JDK 26 crashes in its GIF compressor (`GIFAnimation.gifOutput` →
`ArraysSupport.vectorizedMismatch`, SIGSEGV) right after writing a 30 s recording; the GIF is complete, but each
recording needs a fresh boot. Screens: `parity/shots/p4-x68k-shipwright-offer.png`, `p4-x68k-ending-street.png`,
`p4-x68k-ending-cell.png`. **`_animdoor`** (Bank picked): the
open door is copied, and the street is cleared 44 vsyncs later = **790 ms** (3 × 30 000 busy passes ≈ 8.8 µs each, so
about 88 cycles a pass with the wait states, against the 64 counted above). Pack: `picture_animations.pass_ms` (default)
and each scene's `pass_ms`, `doors.hold_ms` (written by `tools/scripts/x68k_p4_anim.py`). The remaster's Core
`PictureAnimationPlayer` turns real time into passes (one animator for the whole game, so positions carry over); the
Client plays the title torches and the town / building / ending pictures, and shows a picked building's open door for
790 ms (no keys, no animation, as the busy wait).

**Dungeon `_ianim` (s119, XEiJ) `[C]`.** Timed directly like s118, with `XEIJ_PCTRACE` on `_ianim`'s entry (runtime
`0x64a9c`, called once a pass) and its step (`0x64e44`, the counter's clear at image `0x17964`, run on every 51st call):
New Game, one Human Thief made in the Guild and taken as the party (as s118), then the patched XEiJ's armed hook
(`XEIJ_PCHOOK_ARM`, `xeij_trace_build.sh`) poked `_cmovf` = 1, `_cnewf` = 2, `_cnewn` / `_cnewx` / `_cnewy` and
`0x7A8A4` = 1 (image `0x7A898`–`0x7A8A4`, what `_odungeon` writes) on the street and one town command (Profile, SPACE)
let `_town` see `_cmovf`, so `_game` ran `_dungeon` at that square; the party then idled 60–70 s at `_dmain`'s command
menu, the dungeon song playing, the X68000 cursor parked off the picture. In each view the step interval is 50 plain
passes plus one **step pass** to under 0.1 ms:

| View (map, square) | Seen cells with a picture that steps | Plain pass | Step pass | Step period (mean) | Steps | Mean of all passes |
|---|---|---|---|---|---|---|
| Town Hall (1), (16, 27), the entry | 2 north 10 (torches) | 5.533 ms | 51.4 ms | **328.1 ms** (two boots: 328.11 / 328.10) | 192 / 195 | 6.43 ms |
| Lord Keogh's Castle (5), (15, 29), the entry | 16 water (27) | 5.532 | 79.7 | **356.3** | 193 | 6.99 |
| Inn (9), (15, 7), the most of any square | 69 water (27) | 5.535 | 261.9 | **538.6** | 117 | 10.52 |
| Fishing Village (18), (19, 8), poked (s120; `parity/shots/p4-x68k-dungeon-west-torches.png`) | 3 west 2 (torches; no other stepping cell, 32 seen) | 5.531 | 55.2 | **331.9** (median 331.4) | 285 | 6.51 |

So the plain pass (**5.53 ms**, every view; the town and title loops' 5.5–5.9 ms median, s118) is a clean number, but
the step period is not: the step pass draws every picture it steps, and its length grows with the view. Its parts were
timed in the Town Hall and Inn boots (call and return of each draw, `_ianim`'s per-cell head `0x64ab6`; means):
a floor `2.99 ms` (`0x3ab3e`, 32 × 32), the party redrawn over a stepped centre floor `7.80` (`0x3af6c`), a north wall
`10.51` (`0x3ac28`), a west wall **`7.16`** (`0x3ae6a`; s120, the Fishing Village boot: 855 draws, 3 a step, median
7.19, 6.71–10.21 with the odd interrupt; traced at its call `0x64dc2` and return `0x64dc8`, runtime = image `0x178e2` /
`0x178e8` + `0x4d4e0`). Around the draws each cell costs (mostly
the line-F `0xFEE0` FPACK multiplies of the 9-wide index, one to three a cell `[~]`) about **0.14 ms** outside the map
(0xFFFF), **0.27** inside but unseen, **0.49** seen, **0.54** seen and stepped; row 0's cells about 0.1–0.15 less (a
product by 0 `[~]`). With the pass's own 5.53 these give the Keogh step pass as 79.5 against the 79.7 measured `[~]`,
and the Fishing Village one as 5.53 + 28.1 (32 seen, 3 of them stepped, 49 unseen, row 0 less) + 3 × 7.16 = 55.1
against 55.2.
So step period ≈ 50 × 5.53 + (5.53 + Σ cells + Σ draws): 328 ms in a plain corridor with two torches, about 540 ms
with the whole view on water (the views' seen cells from Core's `P4DungeonView`; the Inn square was poked, not walked
to). **Remaster (s120):** Core `P4DungeonAnimation` runs `_ianim` (`0x175BC`, read again s120)
with the pack's `dungeon_view_animation` (`x68000.json`: the cycles, `step_after` 50, the random floor and every cost
above, the west wall's 7.16 ms measured s120 in Fishing Village): per seen cell of the rows from `_iaflag` on, the floor
cycles (each tested on the value the one before left, so none chains), then `0x43594` + 1 for every seen cell and the
42–43 re-roll (rand(2) + 41, drawn even when it gives the same picture), the party (`_mpix`, facing + 4 on 27–30 without
Levitation) only when the centre floor stepped, then the north and the west cycles; the cell word goes back into the
view copy. A step's draws go straight into the view's graphics page (`_fpix` / `_ypix` / `_tpix` all address
`0xC00000 + [0x72596]`), so the remaster keeps the last draw of each (cell, kind) in drawing order and the Client
(`P4DungeonScreen.View3D` / `.ViewAnimation`) draws them over the plain view. `_fpix` is opaque: a stepped floor covers
the part of the wall south of it (and of a west wall east of it) that `_idraw` drew over it, and its own doorway marks,
until the next redraw, as in the original. **Walls seen on XEiJ (s125) `[C]`**, both in Monastery of the Shadow
Priests (20), the party poked there as above. At (6, 6) `_idraw` stands row 3's north walls (0 at columns 2, 3, 5, 7,
grates 7 at 4 and 6: the room's back edge) over row 2's water; the first step (≈ 120 ms of drawing, row after row)
puts water over all of them, and the room shows no back wall until the next redraw (Party info opened and closed). At
(5, 29) column 3's west walls (1) stand over column 2's water (rows 0–3) and the water covers them the same way. Only
the stepped cells' own pictures come back; the walls are never redrawn. The remaster (`--p4-dungeon 20 6,6` / `5,29`,
the preview's optional square) gives both views pixel-equal before and after the step (`parity/shots/`
`p4-x68k-dungeon-water-wall-drawn.png` / `-stepped.png`, `p4-x68k-dungeon-water-westwall-drawn.png` / `-stepped.png`,
the GIFs `p4-x68k-dungeon-water-over-wall.gif` / `-over-westwall.gif`, and the same names with `p4-remaster-`; test
`MonasteryWater_StepCoversTheWallsUnderIt_AsXEiJShows`). Scanned s125 over every square of every map: only map 20
puts a stepping floor under a seen wall (north walls south of it: party squares with y 5–8 below its water band; west
walls east of it: (5, 29) and the squares with the water band in row 7 of the view). No shipped map has a doorway
mark (bit 14 / 15) on a stepping floor, so the marks' clause is not seen on XEiJ `[~]` (an event that changes
pictures could make one; `_prex` runs only at load). Real time: plain passes of 5.53 ms, the step
pass its own cost (plain + cells + draws), the carry going negative after a step; one animator for the game (both
counters never reset). With the s119 / s120 constants Core gives step periods 327.8 / 356.0 / 538.2 / 331.5 ms against
the measured 328.1 / 356.3 / 538.6 / 331.9 (maps 1, 5, 9, 18; tests). s119's "69 water" at the Inn square are its 69
stepping floors: 68 water and the party's own floor 19. `_iaflag` from the writers of `0x4358C` (s120): `_dmain` 9 after
`_mgetc` returns and around each command, `_ewait` / `_ctrap` / `_dtrap` 3, `_xmenu` / `_denc` 9, `_exe_ev` 1 at its
start; every clear to 0 but one follows an `_idraw` (`_dmain`, `_xmenu`, `_listen`, `_denc`, `_openg`, `_setyoko`,
`_exe_ev`'s end, `_dchest`, `_dtrap`, `_try_ul`). The remaster maps its prompts on that `[~]`
(`P4DungeonAnimation.NextRows`): the command menu 0, a message wait 3, a menu 9, a party menu or command screen 9;
"Letter>" / "Number>" / the member pick keep the value before them (1 when an event starts at them, 9 in a command), 0
after a menu or typed input; the view is drawn plain again (`_idraw`) when the party moves or turns, back at the command
menu from another prompt, after a menu / typed input or a command screen. Passes run only while the view is up (not in
fights or towns). Seen on XEiJ (s125, map 20 at (5, 29)): Profile's member pick ("誰の詳細を見ますか?", a command) holds
the view still for its 7.5 s (no step; the water stepped every ~400 ms before it), and the remaster's does too (9 in a
command) `[C]`; the pick, "Letter>" and "Number>" inside an event (1) are still not compared `[~]`.

**System menu (seen s116, XEiJ).** With the System window up over Pendragon's street (no party; `parity/shots/
p4-x68k-system-menu.png`: SPACE, D display speed, B sound switch, S save game, E end game, A About), a 10 s GIF holds
**one frame**: the street does not animate while the menu waits. Closed again (SPACE), the same street gives 109 frames
in 10 s. The remaster has no System window (F5 / F9 / Esc and its own Options), so there is nothing to freeze.

### 6.11 Music: `_music`, `_mus`, `P4.SND` and the title tune (s108, s109)

`[C]` static unless tagged. Sharp's **OPMDRV.X** (`CONFIG.SYS`: `DEVICE = \sys\OPMDRV.X`, on disk 1 `SYS/`, 26 294 B)
is the driver; `P4.X` reaches it through the XC library's `_m_*` calls (IOCS `trap #15`, `d0` = `$F0`, `d1` = the sub-call:
0 `_m_init`, 1 `_m_alloc`, 2 `_m_assign`, 3 `_m_vget`, 4 `_m_vset`, 5 `_m_tempo`, 6 `_m_trk`, 7 `_m_free`, 8 `_m_play`,
9 `_m_stat`, 10 `_m_stop`, 11 `_m_cont`, 12 `_m_atoi`; `0x3b53e`…`0x3b612`). The chip is the YM2151 (OPM, 8 FM voices of
4 operators); the OKI ADPCM (`PCMDRV.SYS`) is not used by `_mus`.

- **`_m_inn` (`0x35c3a`)**: `fopen("P4.SND", "rb")` (retried until it opens), 200 records of **80 bytes** read one by one
  (`fread(buf, 1, 80)`), each turned into a 55-byte voice array: byte 0 = record `0x36` · 8 + record `0x37`, bytes 1–10
  from the record offsets in the table `0x47c3c` (`0x41, 0x38, 0x40, 0x39, 0x3c, 0x3d, 0x3a, 0x3b, 0x42, 0`), then
  the operators (`0x35b30…`, §6.11.2); `fclose`; `_m_init`; then
  `_m_vset(n, array)` for n = 1…200. So **`P4.SND` = 200 FM voices** (16 000 B), each starting with a 10-character name
  (`A.PIANO`, `H.PIANO`, `E.PIANO`, `CLAVINET`, `CELESTA`, `CEMBALO`, `A.GUITAR`, `E.GUITAR`, `W.BASS`, `E.BASS`, …;
  the X68000's stock voice set `[~]`). The record layout: §6.11.2.
- **`_music(n)` (`0xecc`)**: n < 0 → stop (`_mus(98)`). n > 6 → `_mus(current)`, current = `0x7b8ae` (re-start the
  current song: the music-on toggle `0xf72` calls `_music(99)`). n ≤ 6 → if the music switch `0x789c6` is off, stop and
  only remember n; else, when n is new, stop, a busy wait of 8000 loop passes, remember n and `_mus(n)`.
- **`_mus(n)` (`0x35344`)**: `_m_alloc(track, 2000)` + `_m_assign(channel, track)` for tracks 1–8 (each retried until it
  succeeds), then a switch on **n + 1** (`addq.l #1` before the compares): 1–7 = songs 0–6, 99 = n 98 = "stop" (five
  tracks of `|:R4 … :|`, an endless rest loop); each song sends an MML header and a body to tracks 1–5 with `_xm_trk`
  (`_m_trk`; an error prints `"sound no %d err %d"`), then `_m_play(1, 2, 3, 4, 5)`.
- **The title = song 0** (`_openning` clears `0x7b8ae` before `_music(99)`), case 1 at `0x35436`:

  | Track | Header | Body (start) | Voice |
  |---|---|---|---|
  | 1 | `o4v9q7l16\|:256` | `T120r2@114r8p3v10g#4.r2r8v11g#4.<b&<c>g#8…` (`0x47cef`, 194 B) | @114 |
  | 2 | same | `r4@74p3v10v12<c4V10>c2&q5c4Q7v12e4V10…` (`0x47db2`, 210 B) | @74 |
  | 3 | same | `p3v11@83>cc#cc#cc#cc#cv12c#cc#cc#dd#<>efef…` (`0x47e85`, 277 B) | @83 |
  | 4 | same | `@195v5r4r8>ccc8c8c8cc<>c8c8ccccc8c8c8cc<…` (`0x47f9b`, 209 B) | @195 |
  | 5 | same | `o4v9q7l16\|:r1r1r1r1r1r1r1r1r1r1:\|` (`0x4806d`, rests only) | — |

  Each body ends `:|`: with the header's `|:256` the song repeats 256 times `[C]` (§6.11.1: 256 is stored as 0), so the
  title tune plays on under New Game / Saved Game until a song change or the switch.
- **Played (s109)**: the remaster compiles song 0 with OPMDRV's own rules and plays it on a YM2151 model under the title
  splash (pack `title_sequence.tune`, format `x68k-opmdrv`; `OpenPhantasie.Data.Audio.OpmDrvScore`,
  `OpenPhantasie.Client.Audio.Ym2151`). The splash stops it (the original plays on under New Game / Saved Game).

#### 6.11.1 OPMDRV.X 1.01 (s109)

`[C]` static read of `SYS/OPMDRV.X` (image = file − 0x40: text 0x5326, data 0x11d2; a Human68k character device
"OPM", attribute 0x8020; addresses below are image addresses). Init (`0x5350`…) hooks IOCS call `$F0` (vector 0x1F0 →
`0x360`, a jump table at `0x384` on `d1` 0–15: 0 `_m_init` `0x820`, 1 alloc `0x4b2`, 2 assign `0x46a`, 3 vget `0x552`,
4 vset `0x57e`, 5 tempo `0x5aa`, 6 trk `0x672`, 7 free `0x5d2`, 8 play `0x6b2`, 9 stat `0x610`, 10 stop `0x78c`,
11 cont `0x7e2`, …) and the OPM interrupt (IOCS `_OPMINTST` `$6A`, handler `0x1c9e`). Every chip write goes through
IOCS `_OPMSET` (`$68`, `0x2804`).

- **Timing**: the interrupt is **timer A**. `0x1f36`: NA = 1024 − ⌊19531 / tempo⌋ (`divu`); `0x1d20` writes 0x10 =
  NA >> 2, 0x11 = NA & 3, 0x14 = 0x15 (load A, IRQ A, reset flag A) at the end of every interrupt. The handler counts
  interrupts (`0x248f`) and runs the sequencer on every 4th (`& 3` = 0), channels 0–7 in order. A timer A period is
  64 · (1024 − NA) OPM clocks (YM2151 data sheet), so one sequencer pass = 256 · ⌊19531 / T⌋ clocks (s117: XEiJ shows
  NA = 862 at T120 and the passes on every 4th interrupt; a voice-loading pass can lose one, §6.11.3). With the X68000's
  **4 MHz** OPM clock `[~]` (XEiJ only assumes it) that is 1.25 / T s: **48 passes to a quarter note** (19531 = 4 000 000 · 60 / (64 · 4 · 48),
  which is how the clock shows in the code), 10.368 ms at T120. Default tempo 120 (`_m_init`, `0x1c22`). A tempo event
  takes effect from the next reload: the interval already counting keeps the old NA `[~]` (no IV song changes tempo).
- **Voices**: `_m_vset(n, a)` copies 55 bytes into the table at `0x282e` + (n − 1) · 55, 200 voices. Layout (from the
  loader `0x249e`): 0 FL · 8 + CON, 1 slot mask (key-on bits), 2 LFO waveform, 3 LFO sync (key-on writes 0x01 = 2, then
  0), 4 LFRQ, 5 PMD, 6 AMD, 7 PMS, 8 AMS, 9 pan (0x20 bits 6–7), 10 unused; then 4 operators × 11 bytes: AR, D1R, D2R,
  RR, D1L, TL, KS, MUL, DT1, DT2, AME, in the order **M1, C1, M2, C2** (written to slot offsets 0, 16, 8, 24, i.e. the
  algorithm's OP1–OP4). Loading voice v on channel c writes 0x20+c, 0x38+c, the **global** LFO 0x18, 0x19 (PMD | 0x80),
  0x19 (AMD), 0x1B, then per register group the four operators (0x40 DT1/MUL, 0x60 TL, 0x80 KS/AR, 0xA0 AME/D1R, 0xC0
  DT2/D2R, 0xE0 D1L/RR); a carrier's TL (table `0x280e` by CON: 0–3 OP4; 4 OP2, OP4; 5–6 OP2–4; 7 all) gets the
  channel's attenuation added, at most 127. The last voice loaded on any channel sets the LFO for all.
- **`_m_init`** (`0x820`): stops all, tempo 120, then (through the interrupt) loads built-in voice 1 on all channels,
  sets **KF = 0x14** (key fraction 5/64 semitone) on 0x30–0x37, keys all off. No song writes KF again: KC note codes
  are those of the data sheet's 3.58 MHz chip shifted by the compiler (below), and KF 5 trims the 4 MHz clock's
  +1.92 semitones to +2 (o4a = KC 0x48 + KF 5 = 440.1 Hz).
- **MML compiler** (`_m_trk`, `0xa58`): appends to the track's buffer (`_m_alloc`) and keeps per-track state (octave,
  default length, q) across calls, so a header string and a body string sent one after the other make one stream.
  Letters are upper-cased first (`V10` = `v10`), spaces skipped. Each event is 3 bytes:

  | MML | Event | Notes |
  |---|---|---|
  | `a`–`g` [`#` `+` `-`] [n] [`.`…] | KC, len, gate | index = semitone (`0x147a`: C −3, D −1, E 1, F 2, G 4, A 6, B 8) ± 1 + octave · 12, must be < 96; KC = table `0x1482`[index] = (index / 12) · 16 + (0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14)[index % 12]. len = 192 / n (n 1–64) or the default; up to 3 dots, each adding half the previous addition; a byte. gate = ⌊len · q / 8⌋ − 1, 0 if that is 0 (`0x133e`). |
  | `r` [n] [`.`…] | 7F, len, gate | rest |
  | `&` | — | the previous event must be a note / rest: its gate := its len (no key-off; the next note keys on over the sounding one, a slur) |
  | `o` n, `<`, `>` | — | octave 0–8, default 4; **`<` = up**, `>` = down |
  | `l` n, `q` n | — | default len 192 / n (default 48 = `l4`); q 1–8 (default 8) |
  | `v` n | 80, att | att = table `0x146a`[n]: 42 40 37 34 32 29 26 24 21 18 16 13 10 8 5 2 (v 0–15); `@v` n: 127 − n |
  | `t` n | 90, n | 32–200 |
  | `@` n | A0, n | voice 1–200; `@l` n: default len in ticks; `@w` n: D0, 192 / n, 192 / n (a wait) |
  | `p` n | E0, n | 0x20+ch := n · 64 \| FL/CON (n 0–3: bit 0 L, bit 1 R) |
  | `y` r`,`v | B0, r, v | raw register write (not 1, 0x10–0x12, 0x14) |
  | `\|:` [n] … `:\|` | C0 0 n … C0 1 | loop n times (default 2, n ≤ 256, 256 stored as 0 = 256 rounds); one loop level per channel (a nested `\|:` replaces the outer one) |
  | `\|` n, `[`…`]`, `{`…`}` | C0 2…10, … | loop break, segno / coda / D.C. / D.S. / fine (strings `SEGNO`, `TOCODA`, …), tuplets: not used by IV, not decoded `[?]` |

  The stream ends 0xFF (written after every event).
- **Sequencer** (`0x1da6` per channel): an active channel with length counter 0 reads events; else it counts down and
  at 0 reads; else the gate counts down and at 0 (or while 0) keys off (0x08 = c). Reading runs commands until a note,
  rest or wait: note → 0x28+c = KC, key on (0x08 = mask · 8 + c), no key-off before; rest → counters only; 0x80 →
  carrier TLs; 0x90 → tempo; 0xA0 → voice load; 0xB0 → register write; 0xC0 0 / 1 → loop; 0xD0 → wait; 0xE0 → pan;
  0xFF → key off, the channel stops. `_m_play` (mask of channels) points each at its track's start, clears the
  counters, attenuation 21 (= `v8`).

#### 6.11.2 `P4.SND` → voice (s109)

`[C]` (`P4.X` `_m_inn` `0x35a6c`…`0x35bbe`, tables `0x47c3c` / `0x47c64`). An 80-byte record: 0x00 name (10
characters); per operator parameter 4 bytes (one per operator, M1, C1, M2, C2) at 0x0a AR, 0x0e D1R, 0x12 D2R, 0x16 RR,
0x1a D1L, 0x1e TL, 0x22 KS, 0x26 MUL, 0x2a DT1, 0x2e DT2, 0x32 AME; 0x36 FL, 0x37 CON, 0x38 LFO waveform, 0x39 LFRQ,
0x3a PMS, 0x3b AMS, 0x3c PMD, 0x3d AMD, 0x40 sync, 0x41 slot mask, 0x42 pan; 0x3e–0x3f, 0x43–0x4f not read. The
game builds the 55-byte array from them (byte 10 gets the name's first character) and `_m_vset`s voices 1–200. Song 0's
voices: @114 `VIBE` (CON 7, triangle LFO, AMD 33, AMS 1: tremolo), @74 `BRASS 3` (CON 5, PMD 50, PMS 3: vibrato),
@83 `PIANO 1` (CON 2), @195 `S.DR_o4b` (CON 4, a snare from FM, no noise).

#### 6.11.3 Song 0 decoded (s109)

Tracks 1–4 each play one 10-bar round (1920 passes) 256 times: **19.9 s a round, 85 min in all** (T120 throughout).
Track 1 VIBE (38 notes; from pass 120: o4 g#4.), track 2 BRASS 3 (29; o5 c from pass 48), track 3 PIANO 1 (160
sixteenths, o3 c / c# from pass 0), track 4 the snare (106, from pass 72, `@195` at passes 0, 384, 768, 1152, 1536 of every round); track 5 = 20 bars of
rests (its nested `|:` replaces the 256 loop), then it stops. The LFO is global and every voice load rewrites it, so
per round (sequencer log of 0x18 / 0x19 / 0x1B): passes 0–47 off (@83, @195: PMD 0, AMD 0), **48–95 the brass vibrato**
(@74: PMD 50, triangle, LFRQ 200), **96–383 the vibe tremolo** (@114: PMD 0, AMD 33, LFRQ 197), 384–1919 off again
(@195); as on the original, whose driver does the same. Checked by rendering: the first notes come out at 130.7 Hz
(o3 c, 130.81), 138.7 (o3 c#), 523.4 (o5 c), 415.2 (o4 g#), within 2 cents.

- **YM2151 model** (`Ym2151.cs`): ymfm's behaviour (BSD-3, checked against its source `ymfm_opm.cpp` /
  `ymfm_fm.ipp`) re-expressed: 4-operator channels, the 8 algorithms, OP1 feedback, envelope (every 3rd sample, the
  rate / increment table, key scaling, attack ≥ 62 jumps), DT1 table, DT2 (+600, +781, +950 cents), MUL, the
  chip-measured phase-step table (David Viens' data in ymfm) for KC / KF, LFO (4 waveforms, AMD / PMD, AMS / PMS), the
  YM3012 10-bit-mantissa output; log-sine / exponent tables equal to ymfm's. `[~]`: ymfm's own approximations (LFO
  rate formula and waveforms); noise and timers not modelled (unused: s156 `[C]`, noise enable is register 0x0F bit 7, which the IPL ROM, OPMDRV's
  init and voice loads never write (s117 trace, `LoadVoice`), and no IV song can: all 73 `_xm_trk` strings lack the
  `y` register command; IV's `_noise` is ADPCM effect 0, §6.13). The initial register contents other than the
  driver's own writes are taken as 0 (on the X68000 they hold OPMDRV's built-in voice 1 at `_m_play`: the s117 trace
  below; no difference in song 0's sound).
- **OPM trace (s117, XEiJ) `[C]` unless tagged.** XEiJ's Sound › OPM log window does not open on Xvfb, so a patched
  build (`tools/scripts/xeij_trace_build.sh`, `XEIJ_OPMTRACE`) writes every OPM register write, every timer A overflow
  and every `trap #15` with `d0` = `$F0` / `$68` / `$6A` with its emulated time from power-on (machine as §6.10
  "Measured": Shodai, IPL ROM 1.0, both disks). Boot to the title:
  - **Before OPMDRV** (0–3.82 s) the IPL ROM writes only `0x14` = 0 (timers off) and `0x1B` = `0x40` / `0` three
    times (the CT2 bit, the FDC's forced ready); no other register is written, so everything else holds the chip's reset
    value (XEiJ's model: 0 `[~]`).
  - **OPMDRV's init** (`CONFIG.SYS`, 3.82 s): `_OPMINTST` (handler), `0x14` = `0x15`, NA = 862 (`0x10` = `0xD7`,
    `0x11` = 2: T120), and from its first interrupts built-in voice 1 on all 8 channels (`0x20`+ch = `0xFA`: L + R,
    FL 7, CON 2; `0x38`+ch = 0; operators `0x40`… = `71 33 0C 01`, TL `25 25 25 15` (the carrier OP4 with the
    attenuation 21 added), `0x80`… `9C 5D 96 8F`, `0xA0`… `04 04 09 87`, `0xC0`… `00 03 01 00`, `0xE0`… `15 16 12 A5`),
    LFO `0x18` = `0xDC`, PMD 0, AMD 0, `0x1B` = 2 (triangle, CT bits 0), KF `0x30`–`0x37` = `0x14`, all keys off.
    P4.X's `_m_init` (14.87 s) writes the same again; the 200 `_m_vset` calls write nothing to the chip. So at
    `_m_play` every register is that state; **the remaster starts from zeros** with only KF and the key-offs: no
    difference in song 0's sound, as every channel that plays loads its voice before its first note and pass 0's voice
    loads rewrite the LFO (the LFO's free-running phase from boot is not modelled `[~]`). The header's `v9` before the
    first `@` writes the carrier TL of voice 1 (OP4 of channels 0–2 := 18), as the driver keeps voice 1's carrier mask;
    the remaster writes nothing there (those channels are silent until their own voice load).
  - **The first step** (`_m_play(1…5)` at 15.196 s): the 4-interrupt counter `0x248F` is only ever touched by the
    handler's `addq` (`0x1CEC`; skipped while a driver command `0x2490` is pending), never by `_m_init` or `_m_play`, so it
    runs free from the driver's start and its phase at `_m_play` is the count of interrupts since `CONFIG.SYS` mod 4.
    In this boot the first pass ran on the **first** timer A interrupt after `_m_play`, 1.98 ms later (the previous
    interrupt was 0.62 ms before it); the first key-on (channel 2, PIANO 1 o3c) came 4.96 ms after `_m_play`, behind the
    voice loads in the same pass. The remaster's start at pass 0 matches this run; on a real machine the phase depends on
    the boot's disk timing (0–3 interrupts) `[~]`.
  - **Lost interrupts:** a pass that loads voices runs longer than a timer A period (a register write through IOCS
    `_OPMSET` takes about 61 µs: the busy-flag wait), and the handler's closing `0x14` = `0x15` resets the overflow flag
    that came meanwhile, so that interrupt is never counted. The song's first pass (the header's TL writes and the
    @83 / @195 loads: 5.9 ms from the interrupt to the closing write) loses 2; after that every pass with a voice load
    loses 1 (pass 0 of a round: @83 + @195, 48: @74, 96: @114, 384 / 768 / 1152 / 1536: @195): 7 per round, so a round
    lasts 7680 + 7 interrupts = 19.925 s instead of 19.907 (+0.09 %). XEiJ measured
    19.931 s a round; the rest is its own timer re-arm, which starts the next period from the instruction boundary after
    the overflow (2592.84 µs a period on average against 2592.0; an emulator artefact). s156: the remaster models the measured
    rule (`OpmDrvSequencer.LostInterrupts`): a pass that loads a voice waits 5 timer periods, the song's first pass 6, so
    a round is 7687 periods = 19.925 s; the rule is measured for song 0 only, other songs `[~]` (their passes' CPU time
    is not known).
  - **Clock:** XEiJ's OPM runs at a fixed `OPM_OSC_FREQ` = 4 000 000 Hz (`xeij/OPM.java`), so the trace's 2.592 ms
    period cannot measure the clock: it only repeats XEiJ's own assumption. The 4 MHz stays `[~]` (the driver's 19531 and
    KF 5 agree with it, §6.11.1). s158 search: MAME's X68000 driver (`src/mame/sharp/x68k.cpp`) also clocks the YM2151
    at `16_MHz_XTAL / 4` = 4 MHz (and the MSM6258 and MFP from the same 16 MHz crystal), and a vgmrips forum thread says
    the same; both are emulator / hobby sources, not a Sharp document. No Sharp schematic, service manual or *Inside
    X68000* page with the OPM clock was found online, so it stays `[~]`.
- **Not used by IV `[C]`:** `_mus` makes 73 `_xm_trk` calls (all songs and the stop song), each with its own string;
  every byte of those 73 strings is one of `#&.0-9:<>@QRTVabcdefglopqrv|`: no `[`, `]`, `{`, `}`, no `|` other than
  `|:` / `:|`, and no command word, so OPMDRV's loop-break, segno / coda and
  tuplet commands never reach the compiler. Their encoding stays `[?]` (a static read of `_m_trk` `0xa58`; not needed).
- **Still `[?]`**: the YM3012 / X68000 analogue output filter (not modelled: no Sharp schematic with its component
  values was found (s158); MAME and XEiJ model none either); the real X68000's OPM clock (4 MHz `[~]`, above).

### 6.12 Fonts: P4.X's own ANK glyph tables (s135) `[C]`

P4.X draws half-width (ANK) text from three glyph tables of its own; full-width Shift-JIS text comes from the X68000 CG
ROM (`_wkfnt` `0x39DA2`, not extracted). s151: the only CG ROM on this machine,
`~/emulators/XEiJ-0.26.10.08/data/CGROM_XEiJ.DAT`, is XEiJ's own substitute (its licence, item 4: "Default font data is
composed of fonts designed by Keitarou Hiraki and Num Kadoma"; XEiJ copies it in when no `-cgrom` is given), not a
Sharp dump, so its kanji are not used as parity: the pack's 5 × 7 fallback stays for kanji `[?]` (a real CG ROM dump
is needed). `_font(f, style, fg, bg)` (`0x2E16A`; −1 = keep, −2 = restore the colour saved
by the last call): f ≥ 0 → `_wfinit(f)` (`0x36388`) points `_fstl` (`0x3A2FC`) at table f and copies two bytes after
it into `_xlen` (`0x3A2F5`) and `_ylen` (`0x3A2F8`); style → `_wfopt` (`0x3A2EE`); fg → `_fgcol` (`0x3A2F6`); bg →
`_bgcol` (`0x789AC`, 16 = no cell box).

| Font | Table | Glyphs | `_xlen` / `_ylen` | Use |
|---|---|---|---|---|
| 0 | `0x36E8A` | `0x36410`–`0x36E89` (170 with ink) | 3 / 16 | the window font: nearly every screen sets `_font(0, 0, …)` |
| 1 | `0x37BB6` | `0x3720D`–`0x37BB5` (160) | 3 / 16 | a thin variant: `_about`, `_dsp_spell`, `_endstory`, `_getmagic`, `_healch`, `_hprofmenu`, `_mage`, `_minfo`, `_page1`, `_pset`, `_trang` |
| 2 | `0x39902` | `0x37F39`–`0x39901` (50 distinct, 0x20–0x51; the rest point at 0x20's) | 5 / 32 | 32 × 32 item icons, glyph 0x20 + `_idat` b0 (`_itemlist`, `_dvitmlst`, `_tritmlst`, the only `_wfinit(2)` sites; section 6.9 "Item lists"), not text |

- **Table:** 224 longword pointers (relocated), character 0x20 + i. **Glyph:** 4 header bytes — b0 bytes per bitmap row
  (1 = 8 px, 2 = 16 px, 4 = 32 px), b1 rows, b2 pen advance, b3 0 — then b1 rows, top-aligned in the cell (capitals and
  x-height letters 11 rows, descenders 14; the cell is `_ylen` 16). Proportional: "E" 7, "x" 8, "i" 4, "t" 6.
- **Code page:** 0x20–0x7E ASCII (font 0's 0x7B / 0x7D are a circled L / F, font 1's are braces), 0xA1–0xDF JIS X 0201
  katakana, 0x80–0xA0 / 0xE0–0xFF symbols (font 0: arrows, diamonds, a hand, a check mark, the circled L / F).
- **`_wfnt` (`0x39C86`) styles, in this order** (`_xlen` 5 takes a 48-bit path with the same steps): the glyph rows go into
  a 32-bit-per-row buffer (`0x3A30C`); bit 0 **bold**: row |= row >> 1, advance + 1 (`0x3A034`); bit 1 **italic**
  (`0x3A096`): d = rows / 2, then each pair of rows from the top is shifted right by d, d − 1, … while d > 0 (the last
  pair by 1, a 9th / 11th odd row not shifted); the advance is unchanged, so the slant overhangs the next cell; bit 2
  **outline** (`0x3A0C2`: two more rows, advance + 2; `_flen` / `_fsize` add the 2 too) and bit 3 **shadow** (`0x3A236`),
  not decoded further. Then `0x39E66` writes `_fgcol` into graphic VRAM (`0xC00000` + 2048 · y + 2 · x + the page
  offset at `0x820C0`) only where the buffer has ink (transparent). `_fsize` (`0x2E45E`) sums `_flen` advances (+2 each
  with bit 2), 16 per Shift-JIS double byte.
- **Styles used** (`_font` call sites with a constant style): 1 bold (`_acswitch`, `_deadmsg`, `_enc_end`, `_endstory`,
  `_esub01`, `_ident`, `_tload`), 2 italic (`_dsp_exit`'s "Exit", `_tload`), 8 shadow (`_endstory`); every other call
  passes 0 or −1 (keep).
- **Extracted** by `tools/scripts/x68k_p4_fonts.py` → `src/OpenPhantasie.Data/GameData/phantasie4/graphics/fonts/`
  `x68k-p4-font0.json`, `x68k-p4-font0-italic.json` (style 2 applied per glyph), `x68k-p4-font0-bold.json` (style 1,
  s136: `_tload`'s street banner, pixel-equal to the s135 ending shot), `x68k-p4-font1.json`
  (`phantasie.font/v1`, previews in `extracted/graphics/fonts/`). The X68000 pack names them per place (`"fonts"`:
  `p4.combat.exit`, `p4.mystic.bubble`, s136 `p4.street.banner` and `p4.panel.label`); the rest of IV's text keeps the
  remaster font. Checked against the s120 XEiJ
  shots: the italic "Exit" and the Mystic's red "2" are the glyphs pixel for pixel (`P4X68kFontTests`).

### 6.13 Sound effects: `VDATA`, `_voice` and their callers (s152) `[C]`

- **Player:** `_vinit` (`0x35F68`, from `_main` `0x230`) reads `VDATA` (6609 B) whole into `0x8E556`. `_voice(n)`
  (`0x35FBC`) returns at once unless sound effects are on (byte `0x789C5` = 1, `contr` 3); else it calls
  `_a_play(VDATA + Σ len[0 … n − 1], len[n] − 1, 1, 3, 'NASI')` with `len` = the longs at `0x4A338`
  (201, 501, 501, 501, 201, 201, 1001, 1001, 2501; they sum to 6609, so no byte is left over). `_a_play` (`0x3B4FA`)
  skips its length clamp for `'NASI'` and calls IOCS `$60` `_ADPCMOUT` with A1 = the address, D2 = the length and
  D1 = `0x0103`: rate 1 in the high byte, pan 3 (both channels) in the low byte. Each byte is two 4-bit ADPCM samples;
  the last byte of each effect is not played.
- **Decoding:** the OKI MSM6258's ADPCM is the standard OKI / Dialogic scheme: 49 step sizes (16 … 1552), index moves
  {−1, −1, −1, −1, 2, 4, 6, 8} by the nibble's low 3 bits, the 12-bit signal clamped to −2048 … 2047. The **low nibble is
  played first**: decoded so, every effect stays centred (mean −68 … −91, peaks 543 … 2047); high nibble first, effects 3
  and 5 drift to −640 / −1493 and four effects hit the clamp. **Rate 1** of `_ADPCMOUT` is the MSM6258 at 4 MHz ÷ 768 =
  **5208.3 Hz** (the IOCS rates 0–4: 4 MHz ÷ 1024 / 768 / 512, 8 MHz ÷ 768 / 512 = 3.9, 5.2, 7.8, 10.4, 15.6 kHz).
  XEiJ 0.26.10.08 (`xeij/ADPCM.java`) agrees on both: its frequency table lists 4 MHz ÷ 768 = 5208.33 Hz, and it decodes
  "the low 4 bits, then the high 4 bits" of each byte. The IOCS rate number → oscillator / divider mapping itself is the
  IOCS documentation's, not read from the ROM `[~]`. The chip's state at the start of an output (signal 0, step index 0)
  is taken as reset `[~]`. Clipping: only effect 2 reaches the 12-bit clamp (13 of its 1000 samples at 2047), as the
  chip's own clamp does; the others peak at 543 … 1414.
- **The effects** (bytes in `VDATA`, samples, length at 5208.3 Hz) and their wrappers, all `_voice(k)` with a constant:

| n | Bytes | Samples | ms | Wrapper | Callers (call address) → moment |
|---|---|---|---|---|---|
| 0 | 0–199 | 400 | 77 | `_noise` (`0x36048`; its argument, 5 or 10, is ignored) | `_mopta` `0x13ACC`: each race that combines with ancestor 1, drawn by `_dsp_race` while S / D picks one; `_b_face` `0x22E74`: every turn or step of a combatant that does not fly (`_tmark`+7 = 0); `_mmagic` `0x29E6C` / `_pmagic1` `0x2C490`: Sink, each flier as it lands |
| 1 | 201–700 | 1000 | 192 | `_bsetr` (`0x3605E`) | none: no call or pointer reaches it (only the symbol table names it) |
| 2 | 702–1201 | 1000 | 192 | `_sdbeep` (`0x36078`) | `_ewait` `0x1887E` (14 callers: the event WAIT, `_setitm`, `_setgp`, `_listen`, the trap / chest / lock messages); `_xmenu` `0x18F46` (the event MENU, and `_ctrap` / `_dtrap`'s Disarm / Turn back); `_exe_ev` INPUTS `0x196EE` / INPUTN `0x197B4`; `_oencgo` `0x165F2` (with `_einf3`, before `_spc_wait`); `_deadmsg` `0x1E484` (from `_ddamage` and `_pdamage` when nobody is left); `_endmsg` `0xA922` (each page); `_common_proc` `0xAFFA` (each Lord title); `_endstory` `0xABFC` (before the save question) and `0xAC1A` (after it) |
| 3 | 1203–1702 | 1000 | 192 | `_sdnoi` (`0x36092`; argument ignored) | `_exe_ev` op 0x20 SOUND `0x19C4A`; `_openg` `0x18D5E` (after the gate's last picture, only when it was a gate); `_omove` `0x14AC0` (`_ochkedge` refuses the step); `_obarrier` `0x14022` (YES at the shining sea: the ship is destroyed); `_save_ship` `0x14C8E` ("The ship ran aground!") |
| 4 / 5 | 1704–1903 / 1905–2104 | 400 each | 77 | `_sdwlk` (`0x360AC`): `_wlkflg` (`0x4A35C`, 0 at load) ^= 1, then 4 if it is 1, else 5: 4, 5, 4, … | `_omain` `0x15F1C` (every pass top, after `_omark(1)`); `_dmove` `0x1ABDA` (when `_cmapx` or `_cmapy` changed). One flag for both |
| 6 | 2106–3105 | 2000 | 384 | `_sdatk` (`0x360DC`) | none (as 1) |
| 7 | 3107–4106 | 2000 | 384 | `_sddam` (`0x360F6`) | `_ddamage` `0x1A348` (after HP −= d; its callers `_evdam`, `_dswim`, `_ctrap`, `_dtrap`, `_spoile`, `_udam`); `_pdamage` `0x1E9C0` and `_mdamage` `0x21BF4` (on entry, once d ≥ 1), then again in their death branch: `_pdamage` `0x1EB4A` (HP ≤ d or condition 0), `_mdamage` `0x21CEE` (HP ≤ d or `_mc` ≤ 0), so a killing blow sounds twice |
| 8 | 4108–6607 | 5000 | 960 | `_sdmag` (`0x36110`) | `_chrexe` `0x28EA4` / `_monexe` `0x256AC` (after the spell's name; monsters' breath 66 / 67 are silent); `_pmagic3` `0x2D892` (every cast outside a fight); `_ident_mi` `0x2DBBC` (the appraisal window); `_use_hp` `0xEEAA`, `_use_mp` `0xED9A`, `_use_op` `0xF878` (not after a spoiled potion), `_use_mi` `0xFA82` |

- **Remaster (s152):** Core notes each moment above in the party's `P4SoundLog` (`P4Sounds` names; `P4Party.Sounds`);
  the X68000 pack's `sound_effects` (format `x68k-adpcm`: `VDATA` read from the player's copy at run time, as `P4.X` for
  the title tune; the lengths; rate 1; `moments` → effects) maps them, a list taken in turn (walk = [4, 5]; the lists alike
  share one turn, as `_wlkflg`). The Client decodes (`Msm6258`), holds each sample to 44.1 kHz (`AdpcmRenderer`; MonoGame
  plays nothing under 8 kHz) and plays with the Settings Sound switch on (`Game1.UpdateP4Sounds`). s156: `_wlkflg` is
  only toggled (`0x360C6`, no other write in P4.X), so it is 0 only at program start; the remaster's walk turn now goes
  on across a new game or a load in one run of the Client (`P4EffectCues.Reset` empties only the waiting effects).
  Not modelled `[~]`:
  - **timing:** the moments of one key play one after another, after the running effect (taken as `_ADPCMOUT` waiting
    for a running output), at most four waiting. The remaster resolves a fight's round at once, so its steps, casts and
    damage sound together after the key instead of with their animations; the original's `_voice` does not hold the game.
    s158: a round timeline needs the round's animations first: the remaster draws none (the walk pose and the missile /
    spell flights are `[?]` timing, not drawn, `P4DungeonScreen.BattleArt`), so there is nothing to time the sounds to;
    spacing them by guessed gaps would be an invented timing. Next step when the animations are built: Core marks each
    moment with its action step, and an XEiJ `XEIJ_PCTRACE` on the wrappers (`_noise` `0x83528`, `_sdbeep` `0x83558`,
    `_sdnoi` `0x83572`, `_sdwlk` `0x8358C`, `_sddam` `0x835D6`, `_sdmag` `0x835F0`: image + `0x4D4E0`) in a fight forced by
    the `_encper` poke gives the real spacing.
  - IV's own switch (`contr` 3) is not separate: the remaster's Sound switch stands for it.
  - (s158: `_endstory`'s second beep is built with the save question; `0xAC1A` runs after a YES's `_disk(1)` and after a
    NO alike, so both answers sound it; `ending.md` §3.)
  - Sink by the party: the remaster turns the monsters (`_f2f`) before they land, so their turn is silent (the original
    lands each first, so its `_b_face` sounds too).
  - The X68000's ADPCM output filter (XEiJ interpolates linearly between samples; the remaster holds each); the mix level
    (0.6 of full scale).
  - The remaster's post-fight item appraisal under a high Spot Item (`P4Battle`, a manual rule): P4.X calls `_ident_mi`
    only from `_ident` (Identify), so no original moment matches. s156: since the remaster prints `_ident_mi`'s text and
    `_ident_mi` sounds `_sdmag` on entry (`0x2DBB8` link, `0x2DBBC` the call, before any branch), the appraisal now notes
    `spell.identify` with it; the moment itself stays the remaster's (D4-036).
