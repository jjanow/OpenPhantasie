# Phase 10: First-run importer (design, s132)

> **Active** (user, s158, 2026-11-07): SNEG never replied; release work proceeds as planned. The six questions are
> answered in section 6.

Design for the PLAN "Release readiness" item **First-run importer**. **Built s158**: what exists and what is left is in
section 7 (s160 additions in 7.7), the public repo in section 8; sections 1–5 are the design (kept, with the points s158 changed marked). Decisions already made
by the user (PLAN, s105) and not reopened here: the public repo (OpenPhantasie) ships only our own code, tools, tests and
specs; pure numbers may ship; text, maps, art and music are built on the player's machine from the player's own copy of
**any supported version** of each game; IV's English ships as a patch keyed by id over the player's Japanese text, and
`ja.json` is built by the importer, never shipped.

Tags: `[C]` checked in the files, `[~]` likely, `[?]` unknown, `[U]` the user decides (decided s158, section 6).
Counts were taken from the files on 2026-11-01 (worktree of `fcaa0aa`). Rechecked on 2026-11-02 (s150): the GameData
file list and class counts (section 2, which gained font and panel files) and the script line counts (5.2). Per-file
bytes, id counts and the PNG counts in 2.6 are still the 2026-11-01 values.

---

## 1. What the remaster reads today

| Input | Where it is read from | Code |
|---|---|---|
| Game tables (JSON) | `AppContext.BaseDirectory/GameData/<game>/` (copied from `src/OpenPhantasie.Data/GameData/` at build) | `GameDataRepository.Load` (I, `RequireFile` per file), `GameTables.Load` (II–IV), `StringTable.Load` |
| Art (PNG + layout JSON) | each graphics pack's `root`, a repo-relative folder under `reverse-engineering/`, found by walking up from `AppContext.BaseDirectory` | `GraphicsPackCatalog.Load` / `FindRoot` (`src/OpenPhantasie.Data/GraphicsPack.cs`). A pack whose root or `requires` files are missing is listed as missing |
| Music and sound | original program / data files, read verbatim and rendered at run time | pack `title_sequence.tune`, `sound_effects`, `voices` (see table 2.6) |
| Settings, saves | `LocalApplicationData/OpenPhantasie/` | `ClientSettings.DataDirectory` |

So the importer has three kinds of output: JSON tables in the GameData schema, PNG art under pack roots, and a few
original files copied verbatim for the audio players.

---

## 2. Inventory of `src/OpenPhantasie.Data/GameData/`

86 files, 3.8 MB (2026-11-01; 88 with s160's two II Apple-import files). Classes:

- **(a) ships**: numbers, our own rules / decisions / layout, our own text.
- **(b) importer builds**: text, maps, names, art or music taken from an original.
- **(c) mixed**: numbers or our text plus original text; the "Split" column says how.
- **(f) fixture**: test data, not game data.

"Script" names the generator in `tools/scripts/`; "hand-written" = no generator. The "+ ids" note means the file is the
extract output plus the T1.2 id pass (`2ecb673`, adds `id: "p1:n"`); checked by comparing with
`reverse-engineering/dos/extracted/` (items/monsters differ only by `id`; messages, names, overworld, scrolls, towns,
guild are byte-identical).

### 2.1 Shared (2 files)

| File | Bytes | Content | Class | Script | Split |
|---|---|---|---|---|---|
| `shared/colorblind.json` | 2,829 | colour remap table (QoL) | a | hand-written | — |
| `shared/strings/en.json` | 86,052 | 1,004 ids: option labels (600), Options shell, menus, plus UI areas `p4` (112), `p3combat` (58), `p3field` (22), `p3town` (21) | c `[~]` | hand-written | Most is ours. The `p3*` / `p4` labels copy some original menu wording ("Move Front", "[original text]"). Audit per id; original lines go to the game's imported table |

### 2.2 Phantasie I (26 files)

| File | Bytes | Content | Class | Script | Split |
|---|---|---|---|---|---|
| `asset-manifest.json` | 30,150 | DOS map-tile rules + 80 Amiga monster sprite entries with `name` / `amiga_name` | c | `map_tiles` hand-written; `assets` copied from `build_amiga_asset_manifest.py` output | ship `map_tiles`; importer writes `assets` (it is per pack anyway) |
| `creation-tables.json` | 29,815 | TWNS.INT class/race/skill tables, D-003 lifespan bands, DOS tables | a | `extract_level_tables.py` (writes this file in place) + hand-written blocks | — |
| `dungeons.json` | 168,898 | 10 levels: grid bytes, 903 message lines, 764 menu options | b | `extract_maps.py` → `extract_dungeon_events.py` (writes this file in place) + ids | — |
| `elementals.json` | 2,161 | 3 summon records, numbers + 3 names | c | `extract_elementals.py` (writes in place) | names from TWNS.INT by the importer |
| `game.json` | 602 | table counts | a | hand-written | — |
| `guild.json` | 7,234 | 6 test characters from a DOSBox session (`GUILD.pass4.dat`) | f | `parse_guild.py` | move to tests; `GameDataRepository.Load` requires it today (`RequireFile`) |
| `item-descriptions.json` | 25,218 | 180 U)SE ITEM texts (D-061): ours, but scroll items quote each scroll's first line; names from `items.json` | c | `build_item_descriptions.py` | importer runs the builder after scrolls / items |
| `items.json` | 49,623 | 181 items: numbers + 181 names | c | `extract_items.py` + ids | names by index from the importer |
| `map-patches.json` | 726 | 2 remaster map fixes (D-060) | a | hand-written | — |
| `amiga-import.json` | 3,049 | s201: DOS I's value in the 8 cells where the Amiga's numbers differ, for an Amiga-only import (7.8) | a | hand-written | — |
| `messages.json` | 346,148 | MESS1–10 decrypted, 1,708 texts | b | `decrypt_messages.py` | — |
| `monster-traits.json` | 679 | undead / never-flees index lists | a | hand-written | — |
| `monsters.json` | 63,232 | 80 records: numbers + 80 names | c | `extract_monsters.py` + ids | names by index |
| `names.json` | 23,679 | class, race, spell, item, monster, town-label names (282) | b | `extract_names.py` | — |
| `overworld.json` | 207,703 | 18 regions: grids, features, 186 text pages | b | `extract_maps.py` | — |
| `rules.json` | 5,179 | rules profile constants | a | hand-written | — |
| `scrolls.json` | 26,443 | 20 scrolls decrypted | b | `decrypt_scrolls.py` | — |
| `spells.json` | 19,753 | 55 spells: original names, our summaries / coefficient notes, MP costs | c | hand-written | names by index from `names.json` |
| `strings/en.json` | 38,847 | 682 ids: our UI text and lines quoted from DOS (e.g. `core.endgame.zeus_1`, `core.overworld.a_town_enter`) | c | hand-written | tag each id "ours" or "original + file/address"; the importer fills the original ones |
| `towns.json` | 40,308 | 12 towns: stock, restock, price index + 12 names | c | `extract_towns.py` | names by index |
| `travel-time.json` | 781 | step costs | a | hand-written | — |
| `wilderness-encounters.json` | 623 | 16-byte pair table | a | hand-written | — |
| `graphics/index.json` | 520 | pack list, default order | a | hand-written | — |
| `graphics/amiga.json` | 8,614 | Amiga pack: paths, palettes, timings, sound list | a | hand-written | root changes to the import folder (4.3) |
| `graphics/dos-cga.json` | 2,781 | DOS CGA pack (never the default) | a | hand-written | as above |
| `graphics/fonts/amiga-z6font-6.json` | 13,202 | the Amiga disk's `fonts/z6font/6` glyphs (requesters, the line) | b | `extract_amiga_fonts.py` | — |
| `graphics/fonts/amiga-topaz-8.json` | 34,443 | topaz 8 glyphs (screen font, menus) from the **Kickstart 1.3 ROM**, not from the game disk | b | `extract_amiga_fonts.py` | — |

I: **a 11, b 7, c 8, f 1** (s201: + `amiga-import.json`).

### 2.3 Phantasie II (21 files)

All top-level files are written by `build_p2_gamedata.py` from the Atari ST extracts (`extract_st_phantasie2.py`,
`extract_st_p2_maps.py`, `extract_st_p2_dungeons.py`, `extract_st_p2_scrolls.py`, `extract_st_p2_towns.py`). Its
`rules`, `travel-time`, `wilderness-encounters` and `monster-traits` come from constants in the script. It also reads
I's GameData (`monsters`, `monster-traits`, `items`, `spells`, `creation-tables`, `elementals`): a ported II build must
take only I's shipped numbers, so that importing II does not require I.

| File | Bytes | Class | Note / split |
|---|---|---|---|
| `creation-tables.json` | 22,043 | a | TWNS.INT numbers + manual tables |
| `dungeons.json` | 109,408 | b | 8 levels, 723 lines, 561 options |
| `elementals.json` | 1,762 | c | 3 names |
| `game.json` | 1,079 | a | |
| `items.json` | 26,932 | c | 181 names |
| `messages.json` | 191,962 | b | MESS1–8 |
| `monster-traits.json` | 995 | a | |
| `monsters.json` | 53,950 | c | 80 names |
| `names.json` | 17,794 | b | 341 names |
| `overworld.json` | 127,763 | b | 17 regions, 52 text pages |
| `rules.json` | 7,087 | a | |
| `scrolls.json` | 27,400 | b | 14 scrolls |
| `spells.json` | 17,127 | c | 56 names + our notes |
| `travel-time.json` | 925 | a | |
| `wilderness-encounters.json` | 412 | a | |
| `towns.json` | 25,713 | c | 12 names |
| `apple2-import.json` | 11,617 | a | hand-made (s160): the 36 ST cells, the compass word map and the manual's spell names an Apple-only import takes (D2-046..050, 7.5) |
| `strings/en.json` | 6,065 | c | hand-written; 83 ids, the `core.p2.*` lines quoted from PHANT.PRG |
| `strings/fallback.en.json` | 1,538 | a | hand-written (s160): the remaster's wording for the 18 ids an Apple-only II lacks (7.6) |
| `graphics/index.json` | 353 | a | hand-written |
| `graphics/atari-st.json` | 8,413 | a | `build_st_p2_asset_manifest.py` |

II: **a 10, b 5, c 6** (21 files with s160's two Apple-import files).

### 2.4 Phantasie III (17 files)

Top-level files are written by `build_p3_gamedata.py` from the DOS extracts (`extract_dos_p3_tables.py`,
`extract_dos_p3_dungeons.py`, `extract_dos_p3_rules.py`, `extract_dos_p3_scrolls.py`, `extract_dos_p3_towns.py`,
`extract_dos_p3_world.py`). It reads I's `spells.json` and `creation-tables.json`.

| File | Bytes | Class | Note / split |
|---|---|---|---|
| `creation-tables.json` | 21,035 | a | |
| `dungeons.json` | 220,518 | b | 11 records: grids, 691 lines, names |
| `elementals.json` | 2,346 | c | 3 names |
| `game.json` | 1,070 | a | |
| `items.json` | 30,565 | c | 180 names |
| `monsters.json` | 40,050 | c | 80 names |
| `names.json` | 19,139 | b | 376 names |
| `overworld.json` | 51,424 | b | 4 planes' grids, 36 place names, terrain names |
| `rule-tables.json` | 23,626 | c | numbers and our decisions, plus 94 text values (class, skill, social-class, body-area names): replace by name ids |
| `rules.json` | 2,997 | a | |
| `scrolls.json` | 34,294 | b | 19 records |
| `spells.json` | 19,695 | c | 57 names + our notes |
| `towns.json` | 25,042 | c | 17 names |
| `strings/en.json` | 7,967 | c | hand-written; 117 ids, `core.p3.*` quoted from MAIN.EXE |
| `graphics/index.json` | 318 | a | hand-written |
| `graphics/amiga.json` | 22,996 | a | written by `extract_amiga_p3_graphics.py` |
| `graphics/dos-cga.json` | 4,784 | a | s160: hand-written, the DOS CGA pack (art by `extract_dos_p3_graphics.py` / `P3CgaArt`) |
| `graphics/fonts/amiga-topaz-8.json` | 34,659 | b | a copy of I's (Kickstart 1.3 ROM topaz 8) |

III: **a 5, b 5, c 7** (s160: + `dos-cga.json`, a 6; the totals below are the 2026-11-02 count).

### 2.5 Phantasie IV (22 files)

Top-level files and both string tables are written by `build_p4_gamedata.py` from the X68000 extracts
(`extract_x68k_p4_tables.py`, `extract_x68k_p4_creation.py`, `extract_x68k_p4_text.py`, `x68k_p4_events.py`). English
names in the tables come from `reverse-engineering/x68000/phantasie4/translation/en.json`, which is **keyed by the
Japanese text** (1,279 unique lines), so it can't ship as it is (see 4.6).

| File | Bytes | Class | Note / split |
|---|---|---|---|
| `creation-tables.json` | 17,918 | a | TABL numbers |
| `dungeons.json` | 383,522 | b | 21 maps: grids, events, 610 lines, `name` + `name_ja` |
| `elementals.json` | 2,930 | c | 3 names |
| `game.json` | 960 | a | |
| `items.json` | 29,938 | c | 156 names |
| `monsters.json` | 58,092 | c | 84 names |
| `names.json` | 20,755 | b | 401 names |
| `overworld.json` | 180,830 | b | 24 maps' grids, 41 places |
| `rule-tables.json` | 58,672 | c | numbers; service texts already by id (`p4x.*`); but `classes` (10), `races` (35), `skills` (11), `social_classes` (10) hold the translated names: replace by ids, as III's |
| `rules.json` | 1,447 | a | |
| `scrolls.json` | 11,161 | b | SCR1–5 |
| `spells.json` | 24,748 | c | 65 names + our notes |
| `towns.json` | 29,722 | c | 21 names + `name_ja` |
| `strings/ja.json` | 122,389 | b | 2,156 ids, Japanese as in the binary: never shipped |
| `strings/en.json` | 103,736 | c | 2,156 ids: 1,477 differ from `ja.json` (the translation) and 679 are equal (678 pure ASCII: the game's own English, the `source-english` rows of `extracted/text/strings.json`). Ship the 1,477 as the patch; the 679 fall back to `ja.json` |
| `graphics/index.json` | 308 | a | hand-written |
| `graphics/x68000.json` | 27,761 | a | hand-written; animation tables from `x68k_p4_anim.py` |
| `graphics/x68000-panels.json` | 17,283 | b | s136: the Command panel tables (`_mkicon`) from P4.X by `x68k_p4_panels.py` (s140: plus the window-36 icon rows and `_noyes`); holds P4.X's Shift-JIS labels / titles (`text`), so it sits with `ja.json` (imported) |
| `graphics/fonts/x68k-p4-*.json` (4) | 205,836 | b | s135 / s136: P4.X's own glyph tables (font 0, its italic and bold styles, font 1) by `x68k_p4_fonts.py` |

IV: **a 5, b 10, c 7** (the four font files counted one by one).

### 2.6 Outside GameData: art roots and raw files (all b)

| Pack | Root (today) | Files | Bytes | Scripts | Raw files read verbatim |
|---|---|---|---|---|---|
| I Amiga | `reverse-engineering/amiga/extracted` | 249 PNG + 5 JSON | 431,975 | `extract_amiga_graphics.py`, `extract_amiga_map_graphics.py` (reads I's `overworld.json` + `map-patches.json`), `build_amiga_asset_manifest.py` | `unpacked/PHANT/tamb.dat` (title tune), `unpacked/PHANT/game` (sound effects) |
| I DOS CGA | `reverse-engineering/dos/extracted` (`tiles/`, `ui/`) | 25 PNG + `tiles.json` | — | `extract_dos_map_tiles.py`, `extract_dos_town_picture.py` | — |
| II Atari ST | `reverse-engineering/atari-st/phantasie2/extracted/graphics` | 260 PNG + 4 JSON | 348,197 | `extract_st_p2_graphics.py`, `build_st_p2_asset_manifest.py` | `unpacked/disk1/START.PRG` (title tune) |
| III DOS CGA (s160) | `reverse-engineering/dos/phantasie3/extracted/graphics` | 512 PNG + `extraction-index.json` | 267 KB | `extract_dos_p3_graphics.py` (also reads III's `rule-tables.json` `core_race`) | — |
| III Amiga | `reverse-engineering/amiga/phantasie3/extracted/graphics` | 579 PNG + 2 JSON | 952,584 | `extract_amiga_p3_graphics.py` (also reads DOS `WORLD.DAT` / `PLNS.DAT` and III's `rule-tables.json`) | `unpacked/PhantasieIII/D/m0`, `D/m1` (tunes), `PhantasieIII` (sound effects) |
| IV X68000 | `reverse-engineering/x68000/phantasie4/extracted/graphics` | 288 PNG | 999,194 | `x68k_p4_pkh.py`, `x68k_p4_pix.py`, `x68k_p4_fonts.py` (s135: `ui/items/`, and the pack's `fonts/x68k-p4-*.json` from `P4.X`), `x68k_p4_panels.py` (s136: the pack's `x68000-panels.json`, the Command panel tables from `P4.X`) (per the pack note; no single driver script, so whether every PNG regenerates from them is `[~]`) | `unpacked/disk1/P4.X` (OPMDRV tunes), `unpacked/disk1/P4.SND` (voices), `unpacked/disk1/VDATA` (s152: the ADPCM sound effects) |

### 2.7 Totals

| | a | b | c | f | Files |
|---|---|---|---|---|---|
| Shared | 1 | 0 | 1 | 0 | 2 |
| I | 11 | 7 | 8 | 1 | 27 |
| II | 10 | 5 | 6 | 0 | 21 |
| III | 5 | 5 | 7 | 0 | 17 |
| IV | 5 | 10 | 7 | 0 | 22 |
| **All** | **32** | **27** | **29** | **1** | **89** |

Plus 5 art roots (1,401 PNG on 2026-11-01) and 8 raw files, all (b).

**Split rule for (c) files.** The importer writes each (c) file **whole, in today's schema**, into the import folder:
extracted fields from the player's copy, plus our fields from a shipped overlay. The overlay is the same file with its
original text fields removed (names, `amiga_name`, quoted lines). This keeps every loader unchanged, and it gives one
test: on the reference images, importer output must be JSON-equal to today's GameData file. The schema change of moving
names into id-keyed tables everywhere is optional later work, not needed for the release. Strings tables need a
per-id provenance tag first (`ours` / `original: <file> <offset>`). Only II's and III's `_note`s say where their lines
come from, and I's 682 ids and the shared `p3*` / `p4` areas are not tagged.

---

## 3. Supported source versions

### 3.1 Per game

"Feeds" = what an existing pipeline builds from it today. Image hashes: Appendix A; key-file hashes: Appendix B.

| Game | Version (repo images) | Feeds today | Scripts | Status |
|---|---|---|---|---|
| I | **DOS SSI 1987** (`dos/…/phantasie-1987-ssi-360k.ima`, files in `dos/original-files/`) | every I table (b, c) + DOS CGA pack | `re_common`, `extract_monsters` / `_items` / `_names` / `_towns` / `_maps` / `_elementals` / `_level_tables`, `decrypt_messages`, `decrypt_scrolls`, `extract_dungeon_events`, `build_item_descriptions`, `extract_dos_map_tiles`, `extract_dos_town_picture` | supported (parity reference) |
| I | DOS Bonus Edition Ver 1.1 11/12/90 (WizardWorks 1991 `.ima`) | every I table + DOS CGA pack, as 1987 (s168: `PHANTASI.EXE` is LZEXE 0.90 over the same EXEPACK image; unpacked it is 1987's but one byte; every data file and every offset read is the same, `dos/bonus-edition.md`) | as 1987 (+ `re_common.unpack_phantasi`) | supported (`p1-dos-bonus-1991`, `P1DosBonusPipeline`: output equal to 1987's) |
| I | Memorial Set DOS I (Steam app 2702050, GOG product 1919929332; DOSBox): the **Bonus Edition Ver 1.0 9/21/90**, every game file = Ver 1.1's (s166, `dos/original-images/memorial-set-gog/README.md`) | as Bonus Ver 1.1 | as 1987 | supported (s168, found as `p1-dos-bonus-1991`, unplayed) |
| I | **Amiga v1.0 1987** (`.adf`, also `.zip`) | every I table (b, c) without DOS I (s201, 7.8: the Amiga's text, DOS's numbers) + Amiga pack: art, map pictures, title tune, sounds | `amiga_gfx_common`, `extract_amiga_graphics`, `extract_amiga_map_graphics`, `build_amiga_asset_manifest`, `extract_amiga_fonts` (topaz 8 needs the Kickstart 1.3 ROM) (disk unpack: amitools `xdftool`) | supported (s201: tables too, `P1AmigaTables`, `reverse-engineering/amiga/GAME-TABLES.md`; DOS I's tables win when both are imported) |
| I | C64 en / de (`.d64`) | files only | `extract_c64_d64.py` | recognised, no extractor |
| I | Apple II, Atari ST (v2.0 en, v1.0 de), Atari 8-bit, MSX2, PC-88, PC-98, X1 | — | — | recognised by hash only |
| II | **Atari ST v1.0** (Pasti `.stx`; TOSEC `.st` has byte-identical program and data files) | every II table + ST pack + title tune | `stx_to_st`, FAT12 unpack (mtools today), `st_prg`, `extract_st_phantasie2`, `extract_st_p2_maps` / `_dungeons` / `_scrolls` / `_towns`, `build_p2_gamedata`, `extract_st_p2_graphics`, `build_st_p2_asset_manifest` | supported |
| II | **Apple II** v1.0 2/5/86 (4am `.dsk`, or the original `.woz` pair, decoded since s169): the Memorial Set's II | reference extract (s158): monsters, items, names, creation tables, towns, wilderness, dungeons + messages, scrolls; Apple II art pack | `a2_rdos`, `a2_applesoft`, `extract_a2_p2_tables`, `diff_a2_p2_vs_st`, `extract_a2_p2_graphics`, `build_a2_p2_asset_manifest` (`reverse-engineering/apple2/phantasie2/FORMAT.md`) | supported (s158 C# port, `p2-apple2`, 7.2 / 7.5): the Apple's text, the ST's numbers (D2-046…050, decided s159, built s160). Gaps: no spell names on the Apple (the manual's list is shipped; divine #57 / #58 unnamed there, the ST names them, D2-050 `[U]`). The Memorial Set's disks (s166, unplayed, shipped fog) are in DOS 3.3 sector order; since s167 the reader tries both orders and they are found (their save files are the profile's pristine sha1s) |
| II | C64 v1.0 / v1.2, Atari 8-bit, MSX2, PC-88, PC-98 | — | — | recognised by hash only |
| III | **DOS SSI 1988** (`.ima`) | every III table + DOS CGA pack (s160; drawn when no Amiga III pack is imported) | `extract_dos_p3_common` (EXEPACK unpack), `extract_dos_p3_tables` / `_dungeons` / `_rules` / `_scrolls` / `_towns` / `_world`, `build_p3_gamedata` | supported |
| III | DOS WizardWorks 1991 (`.ima`) | as 1988 `[~]`: its unpacked folder holds only the extra `.BAT` files, so the data files are taken to be the same | as 1988 | check by key-file hashes |
| III | Memorial Set DOS III (s166) | as 1988: every game file byte-identical | as 1988 | supported (found as `p3-dos-1988`) |
| III | **Amiga v1.0 1987** (`.adf`) | Amiga pack: art, tunes, sounds. Needs the DOS III files too (map previews read `WORLD.DAT` / `PLNS.DAT`; `monsinfo.dat` = DOS `CREATURE.DAT`, same sha1) | `extract_amiga_p3_common`, `extract_amiga_p3_graphics` | supported for art only, with DOS III |
| III | Apple II `[cr]`, C64 v1.1, Atari ST, MSX2, PC-88, PC-98 | — | — | recognised by hash only |
| IV | **X68000 Ver.1.02** (`.dim`) | every IV table, `ja.json`, X68000 pack, tunes, voices | `x68k_dim`, `x68k_x`, `extract_x68k_p4_tables` / `_creation` / `_text`, `x68k_p4_events`, `build_p4_gamedata`, `x68k_p4_pkh`, `x68k_p4_pix`, `x68k_p4_fonts`, `x68k_p4_panels`, `x68k_p4_anim` | supported |
| IV | PC-98 (`.hdm`), MSX2 (`.dsk`, + 2008 patch images), PC-88 (`.d88`) | nothing; IV's string ids are X68000 addresses | — (`msx2_p4_dis.py` is a disassembler) | not supported |

Consequence (s166, checked on the GOG install): a player with only the Memorial Set can build I's (the Bonus build,
since s168), II's (Apple, since s167) and III's tables. Neither I nor III gets **default art** from it
(the DOS CGA is never the default: `phantasie1/graphics/index.json`, user 2026-10-25).
(Since s158 / s160 the CGA packs are drawn when no Amiga pack is imported: `fallback_order` in both indexes; s158 added
the Apple II II importer.)

### 3.2 Detection

1. **Container** by extension, size and signature: ADF 901,120 B (OFS); `.ima` / `.st` / MSX `.dsk` FAT12 (360K
   368,640 B, 720K 737,280 B); `.stx` starts `RSY\0`; `.dim` 1,261,824 B with `DIFC HEADER` at 0xAB; `.xdf` / `.hdm` raw
   2HD 1,261,568 B (the `.dim` payload without its 256-byte header `[~]`); `.d64` 174,848 B; `.d88` header; Apple `.dsk`
   143,360 B, `.woz` `WOZ1`/`WOZ2` (decoded by `WozImage`: 13-sector 5-and-3 to the 4am layout, or, s170, 16-sector 6-and-2 to a DOS-order `.dsk`, whichever decodes more sectors; read as RDOS, else fingerprinted; the container keeps the `.woz` sha1. The SSI disks' undecoded track 0 sector 12 holds the 16-sector boot sector, not data: `apple2/phantasie2/FORMAT.md` section 1); `.zip` (opened in memory); a plain folder (searched recursively).
2. **Version** by the file names inside (case-insensitive): DOS I `PHANTASI.COM` + `OV1.OVR` + `OV2.OVR` + `TWNS.INT`;
   Bonus `PHANTASI.EXE`; Amiga I `game` + `tamb.dat` + `mstr1.pat`; ST II `PHANT.PRG` + `START.PRG` + `MAPS.INT`; DOS III
   `MAIN.EXE` + `DNGS.DAT` + `WORLD.DAT`; Amiga III `PhantasieIII` + `monsinfo.dat`; X68000 IV `P4.X` + `MOUT` + `DATM`.
3. **Exact build** by sha1 of read-only key files (Appendix B), **not** of whole images: an image changes as soon as
   it is played (saves are written to the game disk on most versions) or comes from another dump. Image hashes
   (Appendix A) only say "this is our reference dump".
4. A file set that matches by name but not by hash is reported as "unknown build" and not imported (the extractors read
   fixed offsets).
5. **Played copy**: the mutable files (3.3) are compared with their pristine sha1s, taken from `dos/original-files/`
   and the unpacked reference disks; a mismatch is reported (3.3 says what then happens).

### 3.3 Played copies

Store copies are almost always played (DOSBox mounts the game folder read-write), and some extractors read files the
game rewrites. II's pipeline already avoids this (both ST sets are played copies, so it takes the starting state from
`MAPS.INT`, `TWNS.INT` and the static byte ranges of `OUTn.DAT`).

| Game | Files play rewrites | Read by an extractor? | Pristine source in the player's copy |
|---|---|---|---|
| I DOS | `GUILD.DAT`, `SACK.DAT`, `TWNS.DAT` (town Save game), `DNG.SAV` / `DNGX.SAV` (`save-file-format.md` "When files are written") and `OUT<n>.DAT`: saved on leaving a region, with fog cleared and fixed fights spent (`inc byte [cell]`; `overworld-movement.md`, OV2 `0x5ea2` / `0x535d`) | `TWNS.DAT` (`extract_towns.py`), `OUT*.DAT` (`extract_maps.py`) | towns: yes, `TWNS.INT` bytes 2445–5936 equal the pristine `TWNS.DAT` byte for byte `[C]`, so the port reads towns from `TWNS.INT`. Regions: **none** (DOS I has no `MAPS.INT`): decision 6 (import + warn) |
| I Amiga | `OUT*.DAT` on the disk: the game rewrites a region's file (upper-case name, `OUT%d.DAT`) with fog lifted and fixed fights spent (`inc` of the cell). The pipeline reads only the polygons (offset 1000) and the town / dungeon cells (classes 4 / 5, fog stripped); fog and fights come from DOS I's tables, so play changes nothing it imports; s201: an Amiga-only import takes the grids from `maps.int` (a key file, never written) and only the feature records and text pages, which play leaves, from `out*.dat` (GAME-TABLES.md section 3) (s163, checked on the reference dump: its played cells differ only in fog and in spent fights; OUT9's three 68s at (1, 18), (15, 19), (20, 9) are the Amiga's own data, DOS 67) | yes (icons; s201 features and text pages) | not needed. s163: the reference dump is played (AmigaDOS dates: `OUT1` / `OUT5` / `OUT9` written 22.04.1992 with `guild.dat` and the saves; `OUT10` / `11` / `13` / `14` written by the game on 06.11.1986 17:48–18:53, a clockless Amiga's date, fog partly lifted); the eleven lower-case files dated 06.11.1986 12:12–12:13 (mastering) with every cell fogged are pristine, and their sha1s are the profile's `mutable_files` (so OUT16 (4, 15) = 88, DOS 87, is the Amiga's own data, not a spent fight; OUT2 (11, 8) and OUT12 (11, 14) also differ from DOS). s169: the seven played ones take their pristine sha1s from a second source, the unplayed WHDLoad package `phantasie-v1.0-whdload-1185.lha` (all 18 files dated at mastering; eleven equal the earlier sha1s; every played file in four ADFs differs from it only by fog and spent fights), so all 18 have one and the reference dump now reports five played files (OUT9 / 10 / 11 / 13 / 14). A mismatch warns that the import is unchanged |
| II ST | saves, `TWNS.DAT`, `OUTn.DAT` play ranges | no (`.INT` + static ranges) | yes |
| II Apple | `CHAR2`, `TWN21`–`TWN210` (town Save), `DNGSAV` / `DNGCHK`, `OUTTn` (stored back on leaving a region, fog only; FORMAT.md 3.6) | `OUTTn` (`TF2` for towns) | the Memorial Set's unplayed disks give the pristine sha1s (`mutable_files`, s167). `OUTT2` ships stored back (756 B, open as on the ST), so size says nothing; the 4am crack is a played copy (15 regions + the saves). A played copy warns (fog only: the Apple stores no fights there) |
| III DOS | only the dungeon map saves `DNP<c>` / `DNG<c>` (ovl4 `0x90a` / `0x93a`, root `0x7bff` in write mode) and `gamesave.dat` (armory stock); `DNGS.DAT` / `PLNS.DAT` are read in read mode, `WORLD.DAT` / `CREATURE.DAT` / `INITARMS.DAT` opened read-only by the start-up loader (root `0x7e08`) `[C]` (s158) | `DNGS.DAT`, `WORLD.DAT`, `PLNS.DAT`, `INITARMS.DAT`, `SCROLLS.DAT` | not needed: a played copy loses nothing |
| IV X68000 | saves go to disk 2 (`FORMAT.md`), which also holds `DATM`, `MOUT`, `IND*.PDT`; whether those are rewritten is `[?]` | yes | hash per file, not per image |

---

## 4. Importer flow

### 4.1 Where it runs

- **Client, first run** (and whenever a game has no import): an Import screen comes before the main menu. It lists the
  four games with their state and an "Add files or folder" action. Options gets an "Import games" entry to re-run it later.
- **CLI** `OpenPhantasie.Importer` console host (`openphantasie-import <path>... [--out DIR] [--list] [--check] [--steam]`,
  `openphantasie-import --refresh [--out DIR]`), the same library the Client calls. **Built s163:** `--check` runs the
  scanned versions into a scratch copy of the import (the import is not changed) and compares every output's sha1 with
  the one `import.json` recorded for that version: `same:` / `differs:` with each changed, new and no-longer-written
  path, `not in the import:` for a version it holds no record of; exit 0 only when all match (`ImportRunner.Check`).
  The comparison with today's GameData stays the private repo's golden tests. `--refresh` re-runs the versions built by
  an older recipe (4.4).

### 4.2 Pointing it at files

- The player picks files or folders (picker, drag and drop, or a typed path). Images, zips and folders are all accepted
  and searched recursively (zips opened in memory; nothing is extracted next to the originals). Images are opened read-only.
- **Multi-disk games** (ST II disk 1 + 2, X68000 IV disk 1 + 2, Amiga III one disk) are grouped by version; a missing
  disk is named ("Phantasie II (Atari ST): disk 2 not found").
- **Steam / GOG**: the Client offers the usual library folders as a shortcut (Steam: `steamapps/common` of each library
  in `libraryfolders.vdf`; GOG: the Galaxy games folder). The Memorial Set's layout is known since s166
  (`reverse-engineering/dos/original-images/memorial-set-gog/README.md`); detection stays by content, never by a fixed
  path.

### 4.3 Output location

`ClientSettings.DataDirectory/import/` (today `LocalApplicationData/OpenPhantasie/`, renamed with the project),
never the install folder. **As built (s158):**

| Path | Holds |
|---|---|
| `import.json` | `importer_version`; per game: `tables` (the version that built the tables), `art` (pack id → version), `versions` (per version: title, recipe, date, sources, key-file sha1s, played files, warnings, every output path with its sha1) |
| `GameData/<game>/…` | the (b) and (c) files in today's schema, plus (copied at each Client start by `GameDataLocator`) the shipped files the import doesn't build, so the folder is complete and loaders stay unchanged |
| `files/<repo-relative path>` | art roots and the raw files the audio reads, at the **same relative paths** the packs name today (`files/reverse-engineering/amiga/extracted/…`, `files/reverse-engineering/amiga/unpacked/PHANT/tamb.dat`), so pack `root` and `../unpacked/…` paths resolve unchanged. Renaming those paths to neutral ones is optional later work |

Loader changes made: `GameDataRepository.DefaultPathFor` / `StringTable` `extends` go through `GameDataLocator`
(the import's game folder when the game's tables are imported, else the shipped folder); `GraphicsPackCatalog` looks
for pack roots in the import's `files/` first. The import root is opt-in (only the Client sets it), so tests always read
the committed GameData.

### 4.4 Versioning and re-runs

- Each pipeline has a **recipe version**. When the app's recipe for a game is newer than `import.json`'s, the Client
  re-runs that game from the stored source paths if the key-file hashes still match, otherwise it asks for the files again.
  **Built s163:** `ImportRunner.StaleVersions` lists the versions whose recorded `recipe` is older than the pipeline's;
  `ImportRunner.Refresh` scans their recorded `sources`, keeps only detections of those versions (a Ready detection
  means every key file's sha1 matched the profile) and runs them as a normal import; a version whose files are gone or
  no longer detected stays as it is, and the report names it with its recorded paths and asks to import it again. The
  Client calls it at start when the import holds a stale version (`ImportScreen.OpenRefresh`: the import screen opens
  with the progress line, then the report). So a DOS III import made before s160 (recipe 1, no CGA pack) or before
  s163 (recipe 2, no fallback lines) gets both at the next start, from its own files (`ImportRefreshTests`).
- A run builds into `import.tmp/` and swaps on success, so a failed run leaves the old import working.
- Adding a second version later (e.g. Amiga I after DOS I) adds only what it feeds (the Amiga pack); outputs from other
  versions stay. If two versions can feed the same output, the matrix's preferred version wins.

### 4.5 A game that is missing

- Main menu: the game stays listed but greyed, with "(NOT IMPORTED)" (s158). Saves of a missing game are kept. A game
  with pictures but no tables (Amiga III alone) gets a note in the import report naming the copies that add its data.
- A game whose tables are imported but which has no default art pack: decision 1.
- Cross-game features (I → II → III transfer, III / I / II → IV Hall) are greyed while either game is missing.

### 4.6 IV English patch

- **Shipped**: `phantasie4/strings/en.json` reduced to the 1,477 ids whose English differs from the Japanese
  (`p4x.*`, `o<map>.*`, `scr<n>.*`), with the P4.X sha1 it was made against.
- **Imported**: `ja.json` (2,156 ids) built from P4.X, O_001–O_021 and SCR1–5 (`extract_x68k_p4_text.py` +
  `build_p4_gamedata.py` strings), and every table with Japanese names (dungeon, town, monster, item, spell names).
- **Lookup**: id in the patch → English; otherwise the `ja.json` text (this is how the 679 ids of the game's own English
  appear without being shipped).
- **Port change**: `build_p4_gamedata.py` maps names through `translation/en.json` keyed by Japanese text. The importer
  must use the id-keyed patch instead; the Japanese-keyed file stays in the private repo as the translators' source.
- The ids are X68000 image addresses and event-file offsets, so the patch fits only the X68000 build: decision 3 (X68000 only).

---

## 5. Porting the Python extractors

### 5.1 Options

| Option | For | Against |
|---|---|---|
| A. Ship Python (embedded CPython + Pillow per OS) and call the scripts | scripts reused | a second runtime in every build; Pillow is native code; amitools / mtools are external; every script hard-codes repo paths (`REPO = parents[2]`) and writes into the repo, so they need rework anyway |
| **B. Port to C#**: new `OpenPhantasie.Importer` class library (no MonoGame; referenced by the Client) + console host | one runtime; xUnit tests; no external tools; detection, overlay and versioning live beside the loaders | about 9,500 lines of Python to port |
| C. Keep the Python in the private repo as the reference | the golden test: the C# output must equal the Python output on the reference images | only with B |

**Recommendation: B + C.** Port pipeline by pipeline, each closed by a JSON-equality test against today's GameData and
a PNG pixel-equality test against today's extracts. Version profiles (key files, hashes, recipe ids) go in a data file,
not in code. PNG writing needs a small encoder (`System.IO.Compression.ZLibStream`); Pillow is used by 10 of the
in-scope scripts. The external tools to replace are amitools `xdftool` (ADF) and mtools (FAT12).

### 5.2 Steps to port, with today's line counts

| Pipeline | Scripts (lines) | Lines |
|---|---|---|
| Containers | `stx_to_st` 153, `x68k_dim` 222, `extract_c64_d64` 120; new: ADF OFS reader (replaces `xdftool`), FAT12 reader (`.ima` / `.st` / MSX `.dsk`, replaces mtools), zip (BCL) | 495 + new |
| I tables (DOS) | `re_common` 102 (EXEPACK unpack), `extract_monsters` 108, `extract_items` 149, `extract_names` 109, `extract_towns` 81, `extract_maps` 127, `decrypt_messages` 196, `decrypt_scrolls` 76, `extract_dungeon_events` 194, `extract_elementals` 90, `extract_level_tables` 81, `build_item_descriptions` 171 | 1,484 |
| I DOS CGA art | `extract_dos_map_tiles` 97, `extract_dos_town_picture` 31 | 128 |
| I Amiga art | `amiga_gfx_common` 121, `extract_amiga_graphics` 328, `extract_amiga_map_graphics` 271, `build_amiga_asset_manifest` 195, `extract_amiga_fonts` 161 | 1,076 |
| II tables (ST) | `extract_st_p2_common` 70, `st_prg` 74, `extract_st_phantasie2` 177, `extract_st_p2_maps` 229, `extract_st_p2_dungeons` 172, `extract_st_p2_scrolls` 76, `extract_st_p2_towns` 215, `build_p2_gamedata` 664 | 1,677 |
| II ST art | `extract_st_p2_graphics` 367, `build_st_p2_asset_manifest` 418 | 785 |
| III tables (DOS) | `extract_dos_p3_common` 203 (EXEPACK unpack), `extract_dos_p3_tables` 232, `_dungeons` 106, `_rules` 117, `_scrolls` 60, `_towns` 54, `_world` 142, `build_p3_gamedata` 597 | 1,511 |
| III DOS CGA art (s160, new) | `extract_dos_p3_graphics` 217 (written with its C# port `P3CgaArt`) | 217 |
| III Amiga art | `extract_amiga_p3_common` 108, `extract_amiga_p3_graphics` 438 | 546 |
| IV tables + text (X68000) | `x68k_x` 109, `extract_x68k_p4_tables` 176, `extract_x68k_p4_creation` 72, `extract_x68k_p4_text` 170, `x68k_p4_events` 190, `build_p4_gamedata` 1,098 | 1,815 |
| IV art | `x68k_p4_pkh` 111, `x68k_p4_pix` 96, `x68k_p4_anim` 189, `x68k_p4_fonts` 186, `x68k_p4_panels` 168 | 750 |
| **In scope** | | **10,267** of 13,755 |

Out of scope (RE tools, not import steps): emulator harnesses and drivers (`fsuae_*`, `xeij_*`, `p3_dosbox_*`,
`x68k_p4_cpu_harness`, `msx2_*`), `fmap_*`, Ghidra helpers, `parse_guild`, `extract_st_p2_saves`, `x68k_p4_saves`,
`verify_st_p2_graphics`, test-disk patchers.

Steps that can't run in the importer as they are:
- `extract_monsters.py` cross-checks against `monster_runtime_table.bin`, a DOSBox capture: keep that check in the
  private repo's tests only.
- `build_p4_gamedata.py` needs the Japanese-keyed `translation/en.json` (4.6).
- `extract_amiga_map_graphics.py` reads I's GameData (`overworld.json`, `map-patches.json`) and `build_p2_gamedata.py` /
  `build_p3_gamedata.py` read I's: these become importer-internal dependencies (I's DOS tables before I's Amiga art;
  II / III read only I's shipped numbers).
- `extract_dungeon_events.py`, `extract_level_tables.py` and `extract_elementals.py` write into GameData in place; the
  port must make them pure (input files → output object).

### 5.3 Effect on the public repo's tests

On 2026-11-01, 105 of the 156 test files read GameData and 8 read `reverse-engineering/`. In the public repo, tests that need (b)
data must either run against an import (skipped when there is none) or use small made-up fixtures. This is part of
the "fresh repo" item, not of the importer itself. **Built s158** (8.2): such tests skip when the data is absent.

---

## 6. Decisions (user, s158)

1. **No built-in data at all.** The remaster does not run without the player's files: at first launch it asks for their
   copy and imports whatever it is given. Art follows what was imported (a DOS-only I / III gets its CGA pack; an Amiga
   copy adds the Amiga pack). A game with nothing imported stays unavailable.
2. **II from the Apple II (Memorial Set):** build an Apple II extractor for II's data **and art** (its own art pack).
   The Atari ST copy stays supported.
3. **IV patch:** keep the X68000 ids; only the X68000 build is supported for IV for now.
4. **Importer language:** C# (`OpenPhantasie.Importer`); players need no Python.
5. **Memorial Set layout:** detect files by content (fingerprints), not by folder names. The GOG installer is kept and
   unpacked since s166 (`reverse-engineering/dos/original-images/memorial-set-gog/`).
6. **Played DOS I copy:** the player is asked (user s205): RESTORE THE MAPS runs DOS I's own Reset rule
   (`GameSession.DosResetCells`, OV1 `0x4914`; `16-reset-game.md` section 3) on the 26×20 grid of every played region but
   OUT1 / OUT5, which gives a fresh copy's cells back (fog on, fixed fights armed; checked against the unplayed image);
   KEEP THEM AS PLAYED imports as it is and warns what was lost (fog already lifted, fixed fights spent; decision s158).
   Asked only when a region file (`OUT*.DAT`) is played and the version reads its grids from it
   (`ImportRunner.CanRestoreMaps`: DOS I 1987 and the Bonus Edition; Amiga I reads `maps.int`, the Apple's II stores fog only
   and has no decoded reset rule). The Client asks after the scan (Y / N, rows, pad A / B; Esc imports nothing); the CLI
   asks on a terminal or takes `--restore-maps` / `--keep-maps` (no answer: keep). The choice is recorded in `import.json`
   (`maps_restored`) and kept by recipe re-runs and `--check`.

Public repo: built fresh, pushed to GitHub as **private** `jjanow/OpenPhantasie`; the user makes it public.

---

## 7. What is built (s158)

### 7.1 Projects

- **`src/OpenPhantasie.Importer`** (class library, no MonoGame): `Containers/` (FAT12 for `.ima` / `.st` / MSX `.dsk`,
  Human68k for `.dim` / `.xdf`, AmigaDOS OFS/FFS for `.adf`, Pasti `.stx` → `.st`, zip in memory, folders; every
  reader gives the same bytes as the repo's unpacked copies, `ContainerTests`), `Profiles/profiles.json` (every
  version: key files, mutable files with pristine sha1s, image fingerprints; status supported / planned / recognised)
  + `Detector` (content only), `Pipelines/` (one `IImportPipeline` per supported profile, found by reflection),
  `Formats/` (PNG writer/reader, a Python-`json.dumps`-compatible writer), `ImportRunner` (scan → detect → run into
  `import.tmp/` → `import.json` → swap), `StoreLibraries` (Steam `libraryfolders.vdf` / GOG folders as shortcuts).
- **`src/OpenPhantasie.Importer.Cli`**: `openphantasie-import <path>... [--out DIR] [--list] [--check] [--steam]`, `--refresh` (s163, 4.1 / 4.4).
- **Client**: `ImportScreen` (first run when no game is available; Options → GENERAL → IMPORT GAMES): typed or dropped
  paths, Steam / GOG shortcuts, the import on a background task with a progress line, then the report (per version:
  imported / warnings / not imported and why), wrapped and scrolled, nothing cut. The main menu greys a game without
  data as "(NOT IMPORTED)".
- **`OpenPhantasie.Data.GameDataLocator`** + the switch: MSBuild property **`OpenPhantasieBuiltInGameData`** in
  `OpenPhantasie.Data.csproj` (default `true` here). `false` (the public repo) = no fallback to the committed GameData or
  the repo's art roots: a game without an import is unavailable. Checked on Xvfb with `false`: the first-run screen,
  importing DOS I + Amiga I + Kickstart through it, then I starts with the imported Amiga art; II and III (imported by
  the CLI into the same folder) start too; DOS-only I draws the CGA pack; DOS-only III starts without pictures (s158; s160 adds its CGA pack, 7.7). This
  proves the wiring and art lookup, **not** that the public repo runs: the shipped folder still held the full private
  GameData, which `GameDataLocator` copies into the import (see 7.4).
- Scanning reads only what detection needs (sizes from directory entries, zips on disk read per entry): the 380 GB
  Steam library here lists in 3 s with 200 MB.
- **`tests/OpenPhantasie.Importer.Tests`**: golden tests per pipeline on the reference images (skipped when absent), the
  container / detection tests, the flow and locator tests.
- `graphics/index.json` of I gained `fallback_order: ["dos-cga"]`: with no Amiga pack (a DOS-only import) the CGA
  pack is drawn (decision 1); checked on Xvfb: DOS-only I draws the CGA town and screens.

### 7.2 Per version

| Profile | Pipeline | Verified against the committed files (reference images) |
|---|---|---|
| `p1-dos-1987` | `P1DosPipeline` (+ `P1Dos/`) | 11 tables byte-identical (dungeons, elementals, item-descriptions, items, messages, monsters, names, overworld, scrolls, spells, towns); DOS CGA pack: `tiles.json` byte-identical, 44 PNG pixel-equal (tiles, town, and the defeat pictures of `extract_dos_defeat_pictures.py`). Towns from `TWNS.INT`. `spells.json` also checked with the names stripped from the shipped copy (the public overlay) |
| `p1-amiga-1.0` | `P1AmigaPipeline` | the whole pack root (256 PNG pixel-equal + 5 JSON byte-equal), `amiga-z6font-6.json` byte-equal, `asset-manifest.json` JSON-equal (shipped `map_tiles` + extracted `assets`), raw `tamb.dat` / `game` byte-equal. Map pictures read I's `overworld.json` (this run's, DOS I's or the Amiga's own). s201 tables (`P1AmigaTables`, no Python counterpart, 7.8): JSON-equal to the DOS GameData but for an allow-list of text and provenance paths (`P1AmigaTablesTests`); the same from a public-repo shipped folder; loads and starts a game |
| `kickstart-1.3` | `KickstartFontPipeline` | topaz 8: I's `amiga-topaz-8.json` + preview PNG and III's copy, byte-equal. Its own pipeline so either Amiga game gets it |
| `p2-st-1.0` | `P2StPipeline` (+ `P2St/`) | 15 GameData files byte-equal (incl. `graphics/atari-st.json`), 262 PNG pixel-equal + 4 JSON byte-equal, raw `START.PRG` / `PHANT.PRG` byte-equal; the TOSEC `.st` pair gives identical output. Needs only I's shipped numbers (no names) |
| `p2-apple2` | `P2Apple2Pipeline` (+ `P2Apple2/`, `Containers/RdosVolume`) | the 4am crack's two RDOS `.dsk` sides (key files from `extracted/key-files.json`; or the original `.woz` pair (s169, `Containers/WozImage`): every output byte-equal to the `.dsk` pair's; the `.woz` disks are the same played copy (`WozImageTests`); s170: the 4am pair written out as 16-sector 6-and-2 `.woz`, as a Memorial Set disk would be, imports byte-equal too). Art: 545 PNG pixel-equal, `extraction-index.json` and `graphics/apple2.json` byte-equal (the pack's constants live in `A2Manifest`, not read from the shipped file). Raw tables: the 9 `extracted/*.json` of `extract_a2_p2_tables.py` byte-equal. GameData (no Python counterpart, see 7.5): loads through `GameDataRepository.Load` and a new game starts; every number equals the ST build (the ST cells of `apple2-import.json` applied; s169: OUT1's unused feature record 14 too); the shipped cells equal the ST GameData; the scrolls' compass words, turned, equal the ST's. Strings: 217 ids verbatim + 95 in the Apple's own wording from the copy, the rest from the shipped fallbacks (7.6; s163: the common shared / I file + II's 3 ids); under the public "..." mask an Apple-only import leaves no id II shows on "..." (s160's check covered only the ids the ST gives; s163 covers II's whole chain) |
| `p3-dos-1988` | `P3DosPipeline` (+ `P3Dos/`) | 10 tables JSON-equal, 9 byte-identical (rule-tables JSON-equal only: hand-added blocks). WizardWorks 1991 detected by the same profile, identical output. III's spells take only `category` / `effect_summary` from I's shipped spells. s160: the DOS CGA pack (`P3CgaArt`), 512 PNG pixel-equal to `extract_dos_p3_graphics.py`'s (7.7) |
| `p3-amiga-1.0` | `P3AmigaPipeline` | 579 PNG pixel-equal, 2 JSON byte-equal, raw `D/m0`, `D/m1`, `PhantasieIII` byte-equal. Without DOS III only `previews/scandor-dos.png` (unused by the pack) is left out |
| `p4-x68000-1.02` | `P4X68kPipeline` (+ `P4X68k/`) | 16 files JSON- and byte-equal (monsters, items, spells, names, overworld, dungeons, scrolls, towns, elementals, rule-tables, `strings/ja.json`, `graphics/x68000-panels.json`, the four `x68k-p4-*` fonts); 382 PNG pixel-equal (every PNG has a generator: 55 PKH, 273 `.pix`, 54 fonts / item icons; 2.6's 288 is stale); raw `P4.X`, `P4.SND`, `VDATA` byte-equal. English by id only (decision 3): `en.json[id]`, else the Japanese from the copy; `translation/en.json` is never read. A test with `en.json` cut to the differing ids and `rule-tables.json` without the copy's fields gives the same tables. `x68k_p4_anim.py` not ported (its numbers live in the shipped `x68000.json`); the panel call sites found by objdump are a fixed table (P4.X is pinned by sha1) |

### 7.3 Findings of the port (committed files vs scripts)

- `build_p2_gamedata.py` and `build_p3_gamedata.py` are behind hand edits (`--check` fails): II `rules.json` /
  `travel-time.json`, III `creation-tables`, `rules`, `towns`, `rule-tables`. The importer leaves those class (a)
  files shipped and takes the hand-added parts of (c) files (III towns `_layout` / `_notes`, rule-tables blocks) from
  the shipped copy, so a re-run of the Python would lose edits the importer keeps.
- II `dungeons.json` lacks the `option_cells` that `extract_dungeon_events.message()` adds since s153; the port matches
  the committed file.
- I `dungeons.json` keeps three hand-written top-level fields (`_note`, `_grid_byte_length`, `_grid_note`) no script
  writes; the port writes them as constants. s160 (user s159): `_note` takes `extract_maps.py`'s wording ("raw feature
  bytes; [C] grid = first 1254 B = 33x38, …") in the shipped file and `MapTables.DungeonNote` (it kept the script's
  older "grid geometry TBD" note); the other two stay hand-written.
- Counts in 2.6 are stale: I Amiga root 256 PNG + 5 JSON, II ST root 262 PNG + 4 JSON.
- s158 resync: `x68000-panels.json` gained `_acgamend` / `_acgamend.1`, the training hall title and two ink fields
  (ported); II ST's alert boxes are the disks' `form_alert` strings: rebuilt from `PHANT.RSC` / `OUT.RSC` /
  `PHANT.PRG` instead of taken from the shipped pack.
- II scroll lookup (found while mapping the Apple): II's ST `scrolls.json` has no `item_records`, so the Client's
  fallback `Scrolls.Id == item - 120` shows record `item - 120` of the 14 (SCROLL B, item 124, would show id 4 = SCROLL D's
  text). Not changed here: a Client / data question.
- The ST and Apple II OUT1 differ in unused feature record 14 at (25, 0) (ST dest_y 32, Apple 0); no cell uses it (record 13 on the same cell is found first). s169: an `apple2-import.json` cell (D2-049, keyed on the record index) gives it the ST's 32.

### 7.5 What an Apple-only II import gives

II's GameData in the ST build's shape, from the Apple's files alone (`A2GameData`); with the ST copy imported too (same
run or earlier) the ST's tables win and the Apple adds only `apple2.json` + its art (a warning says so;
`import.json` `tables` stays the ST's; the runner now records `tables` only for a profile that wrote GameData tables).

- **From the Apple:** monsters (names, numbers; `sprite_width` = the idle picture's width, `sprite_height` = the
  Apple's rank height YS%), items 1-180 and value factors, names (classes, races incl. LIZARD MAN, status OKAY),
  the ten towns (TF2), creation tables (TINFO; one-byte skill cells), the 17 regions (grid, features, fog as on the
  disk), dungeons (level bytes, 2048 B as the ST's; messages from the Apple's slots, menus written in the text become
  options: event record byte 1 or the 1- / 2- markers, else digits), scrolls (SCROLLL0-20 as records 0-20 with
  `item_records`, 17 empty).
- **Decided entries applied as for the ST:** D2-009, D2-015, D2-021, D2-022 / 023 (from I's shipped tables, the rules).
- **The ST's numbers whatever the source (D2-046..049, user s159, built s160):** the shipped class (a) file
  `phantasie2/apple2-import.json` lists the 37 cells where the Apple differs (5 monster fields, DEMONIAC's factor and
  the SCROLL D / E stock, 2 skill cells, 9 map tiles, OUT28's 8 entrances, 2 dungeon bytes, s169: OUT1 feature record 14) as paths into the built
  files with the Apple and ST values (`A2StValues.ApplyCells`). A cell holding the Apple value takes the ST's (a fogged
  tile keeps its fog); one holding neither is kept and warned. Pure numbers, so they ship (the ST's own `monsters`,
  `towns`, `overworld`, `dungeons` are dropped from the public repo and can't be read instead).
- **Text (D2-050):** the Apple's names, scrolls and dungeon messages; the scrolls' compass words turned to the ST
  orientation (`scroll_compass` in the same file, all at once, at word starts, boxed lines keep their width). Spell
  names: the manual's list (CB p.12) from `apple2-import.json` `spell_names`; the two divine spells, unnamed in the
  manual and on the Apple disks, the ST's DIVINE II (#57) / DIVINE I (#58) (user s170, `from_st`). String tables: see 7.6.
- **Missing on the Apple:** `messages.json` (the Apple keeps dungeon text in the levels; `game.json` has no
  `message_areas`), elementals (not on the Apple disks: the remaster's three, I's / the ST's numbers, names
  FIRE / WATER / EARTH ELEMENTAL). Played copy: 15 of 17 OUTT files saved back (fog lifted), warned.
- Text cells: the Apple's CR-ended string from slot `arg` is put in the ST page form (its screen lines as pages,
  `arg + 10` per extra line) so Core's `LocationTextPages` shows it.
- s163, seen in the Client on Xvfb from a public-tree import (the export built with no built-in GameData, the public
  CLI importing the Apple pair + DOS III into a throwaway data folder): the Filmon ending (`--p2-victory`: the Apple's
  40-column lines in the remaster's box, scrolled, nothing cut), the Mystic (its score line showed "..." until the
  s163 fallback; now "SCORE: 0. YOU ARE NOVICE ADVENTURERS"), the town Option cast window. s169: the 18-character
  MONSTER EVALUATION seen whole in the wilderness cast window (`parity/shots/s169-p2-apple2-field-cast-monster-evaluation.png`);
  it never shows in a fight's list (spell #52 is not a combat spell; the Apple's fights take 1-51, `OUTDOORS2` /
  `DUNGON2` line 1310). The ending's "THESE {0} BRAVE ADVEN-" line takes the party's member count, dead included (`WIN`
  line 3116; a summoned elemental is left out, s170: the Apple has none).

### 7.4 Left to do

- **II Apple II:** run in the Client (s163 / s169, 7.5). `.woz` pairs read since s169. Memorial Set disks (DOS 3.3 sector order): read and found since s167 (`RdosVolume.TryOpen` reads
  both orders and keeps the longer catalog; catalog sector 0 is the same in both); their saves are the pristine sha1s.
- **I Bonus Edition** (the Memorial Set's I): imported since s168 (`dos/bonus-edition.md`: `OV1.OVR` / `OV2.OVR` differ
  only in the hidden-save flag and the manual page lines; `IBMCOVER`, the WizardWorks cover, is not read).
  An import made before s168 recorded only the versions it imported, so the recipe re-run (4.4) does not find I in
  it: the player imports the Memorial Set again (II / III are re-imported unchanged).
- **Recipe re-runs** (4.4) and the CLI's `--check` (4.1): built s163.
- **III DOS CGA pack** (built s160, 7.7): seen on Xvfb with the pack chosen in the settings (III test party: Guild
  portraits, the town street with the name on its banner, the overworld equal to the DOS shot `s143_map_e_key_nothing`
  cell for cell, the body figures). s163: run from a DOS-only public-tree import (no Amiga pack anywhere, so the
  catalog's fallback chose it): Guild portraits, the town street with its banner, the overworld with the body
  figures, a fight (the CGA monster pictures on the field, then the orders panel) seen; an import made before s160
  gets the pack from the recipe re-run at the next start (4.4). s169 (FORMAT.md section 10): DOS switches CGA palettes
  per screen (`0x284:0xaaf`; palette 1 on the dungeon map, planes, combat field, gods and Astral Plane; the pack's
  `palettes` / `scene_palettes`); Astral Plane, gods, Scroll IV and the dungeon map seen and fixed; the 15 glyphs are the
  text page frame (`scroll_frame`), the three 24×24 frames the hit / gods flash (`combat_flash`); the town name centred
  in 12 cells at row 2 (root `0xa9e2`); the Amiga does not swap the Scandor tiles; `view_counters` from root `0x125d`.
  s170: Scroll IV's caption under the map (`scroll_frame.picture_caption`, string `useitem.scroll_picture_caption` from
  DS:`0x2699`, root `0x1b4`); the hit / gods flash ORed onto the screen (`blend: "or"`, a max blend, `0x284:0x19c`); the
  four overworld steps read (ovl3 `0x13ac` / `0x1470` / `0x154e` / `0x1612`): the view's centre stops at columns − 5 and
  rows − 4 (`tile_maps.view_centre_margin` [5, 4]), so Scandor's view ends at columns 41–49 and DOS never reads past a
  map; the Astral scene's palette 0 (`0x240c`) comes after its last line and a cleared screen, as the remaster's scene
  ends; the remaster's auto-map box is no longer drawn over a tile dungeon (III, both packs: it covered the second
  member card). Shots `parity/shots/p3-dos-cga-s170-*`. s171: in its place a tile dungeon draws its own whole-level
  overview (QoL, the same Tab switch `AutoMapVisible`): 24 × 30 cells of 3 × 2 px in a 76 × 64 box inside the view
  left of the cards, in the corner farthest from the party; a seen cell takes its glyph's commonest colour (DOS CGA
  palette 1: stone white, floor magenta), the party cyan. Neither original draws a box (the Amiga's dungeon screen is
  the whole level). Shots `parity/shots/p3-s171-automap-*`.
- **Partial imports and the string tables** (7.6): an id resolves only from a version that holds it. Imports without
  I's tables (ST-only or Apple-only II, DOS-only III, IV) are covered by the shipped fallbacks (s163).
- **Review items the export lists** (8.3): 2 files (s169, from 79), both accepted: the shared `en.json`'s option texts
  naming a game line, and IV's Japanese panel labels (`x68k_p4_panels.py`); long names in notes.
- Done s158: public overlays, per-id string provenance, public-repo loading (`guild.json` optional; I's
  `asset-manifest.json` loads without `assets`), tests that need the original data skip without it (8.2).

### 7.6 The games' own lines in the string tables (s158)

- `tools/scripts/build_string_sources.py` writes `strings/en.sources.json` next to `en.json` for `shared`,
  `phantasie1`, `phantasie2` and `phantasie3`: per id whose text holds original parts, one or more alternatives
  `{from: <version profile>, parts: [our literal | [file, offset, length]]}`. Files are raw key files or decoded
  images (`PHANTASI.COM:unpacked`, `MAIN.EXE:root`). A part is original when it is found verbatim in the id's own
  game's files (shared areas `p1*` / `p2*` / `p3*` / `p4*` by game; I's table also by II and III, which extend it);
  a run the ST joined from box lines is found word run by word run (the game's own versions only). Areas `option.`,
  `options.`, `menu.`, `game.` are ours. Counts (s158): shared 269 ids, I 346, II 84, III 124.
- Every pipeline calls `ImportedStrings.Write` (`src/OpenPhantasie.Importer/Pipelines/ImportedStrings.cs`): the ids its
  version can give go to `GameData/<table>/strings/en.<profile>.json` in the import. IV writes its ids whose English is
  the game's own (equal to the copy's text; the shipped patch leaves them out, 4.6) the same way. These files don't
  make a game "imported" (`import.json` `tables` ignores `strings/`).
- `StringTable.ForGame` lays every `en.<profile>.json` of a table (`GameDataLocator.ImportedStringFiles`) over that
  table, for the shared one and for an `extends` chain too. Without an import (tests, dev builds) nothing changes.
- The public `en.json` keeps each such id with "..." in place of each original part, so no id is missing and a partial
  import shows "..." instead of failing (7.4).
- **Phantasie II's Apple II** (`p2-apple2`, s160, D2-050): the script adds its alternatives after the rest, so no other
  alternative changes. An id the ST gives gets one when every original part of the ST's is found verbatim in the
  Apple's programs (217 ids; files by RDOS name, only those on one side: OUTDOORS2, TOWN2, CEMETARY2, CHAR2, INTRO2,
  INFO, DUNGON2, WIN). The ids the Apple prints in its own wording (95: the Mystic, Pluto's speech, the Filmon ending,
  prompts such as DROP WHO) get hand-mapped references from `tools/scripts/p2_apple2_own_wording.json`, marked
  `"wording": "own"` (they resolve to the Apple's text, not the `en.json` value; `--show-apple` prints them, and the C#
  import resolves the same 95); `SCROLLL0:page` is SCROLLL0 decoded as a text page. Own-wording lines are II's only:
  `ImportedStrings.Write(ownWordingGame: "phantasie2")` puts them, from every table, into
  `phantasie2/strings/en.own.p2-apple2.json` (top of II's chain), never into the shared or I table, so I and III keep
  DOS I's / Amiga I's lines; it is written only when the Apple gives II's tables, and `ImportedStringFiles` loads an
  `en.own.<profile>.json` only while `import.json` names that profile as the game's `tables` (an ST imported in a later
  run turns it off). The ids the Apple lacks take the remaster's wording from the shipped fallback files
  (`{"tables": {table: {id: text}}}`; since s163 the shared / I lines from the common `phantasie1/strings/fallback.en.json`,
  II's own three from `phantasie2/strings/fallback.en.json`, next bullet): `ImportedStrings.WriteFallback` merges them
  and writes `strings/en.fallback.<profile>.json` per table, and `GameDataLocator.ImportedStringFiles` lays fallback
  files under every other imported file, so DOS I's or the ST's own line still wins.
- **Imports without I's tables** (s163): a version gives only the lines its files hold, so a game's chain (shared → I →
  its own table) had "..." for the rest: DOS-only III (395 ids), and also ST-only II and Apple-only II (175 ids each,
  the Mystic's score line among them: s160's Apple check covered only the ids the ST gives). Now three shipped files:
  `phantasie1/strings/fallback.en.json` (shared 84 + I 322 ids: every id of those tables with original parts that the
  ST II, the Apple II II, DOS III or X68000 IV does not give, for a game that shows it; II leaves out III's ids, IV
  takes only its Hall's `core.p4.` ids), `phantasie2/...` (II's table: Pluto's minion, plus `core.p2.mystic.score_x`
  and `core.p2.amulet_leave`, whose only source is DOS I) and `phantasie3/...` (III's table, 3 ids). Left out: I's
  Amiga field / map / town screens (`p1field.`, `p1map.`, `p1town.`, `amiga.`), I's ending (`core.endgame.`), IV's
  `p4.` ids. `ImportedStrings.WriteFallback(ctx, games…)` merges the files per table; the ST (recipe 2), the Apple
  (recipe 3) and DOS III (recipe 3) write the common file and their own, IV (recipe 2) the common one; older imports
  get them through the recipe re-run (4.4). Wording: single words, game terms (classes, races, stats, spell names),
  short labels and fixed-width sheet rows keep the table's text; lines are reworded with every format item kept; the
  Apple's earlier 17 shared / I lines moved into the common file unchanged. Tests (`P3StringFallbackTests`): each
  file holds only its tables, no id in two files, format items kept; over the public mask a DOS-only III, an
  ST-only II, an Apple-only II, and Apple II + DOS III together leave no id their game shows on "...", nor does an
  X68000-only IV in the shared table (scopes built from `en.sources.json` at test time).
- Golden test `ImportedStringsTests`: on the reference images every listed id resolves to the committed text, per
  version, and IV's patch + imported ids = the committed `en.json`.

### 7.8 What an Amiga-only I import gives (s201)

`P1AmigaTables` (`Pipelines/P1Amiga/`, layouts in `reverse-engineering/amiga/GAME-TABLES.md`) writes the eleven files
`P1DosPipeline` writes (monsters, items, names, towns, elementals, scrolls, messages, overworld, dungeons,
item-descriptions, spells) from the Amiga disk alone, before the art steps (the map pictures and asset manifest read
them). Each Amiga table is put into DOS layout and goes through the DOS builders (`ComTables`, `DataTables`,
`MapTables`, `Derived`), so the files keep DOS I's schema and the loaders are unchanged.

- **From the Amiga:** `game` hunk 3 (monsters, sprite sizes) and hunk 12 (item / class / race / spell names through
  their pointer tables, item values), `twns.int` (towns, elementals, town labels), `maps.int` (the grids, never
  written) + `out*.dat` (features, text pages), `dng*`, `mess*` and `scrolls.dtx` (plain text on the Amiga).
- **DOS's numbers (DOS wins over Amiga):** the shipped class (a) `phantasie1/amiga-import.json` lists the 8 cells where
  the Amiga differs (OUT2 / OUT12 terrain, user s53; four fixed fights the Amiga ships spent; DNG6's lock cell;
  ORANGE DRAGON's sprite height), applied by `CellPatches` (shared with II's Apple import). Every other number equals
  the DOS build, `dos_record` and `raw_hex` included.
- **The Amiga's text:** names, messages, scrolls (upper case), Zeus's speech (own wording,
  `tools/scripts/p1_amiga_own_wording.json` → `en.own.p1-amiga-1.0.json`, I only, while I's tables are the Amiga's);
  every difference is listed in GAME-TABLES.md section 6. D-082 / D-083 (user s202): HEAL POT1..9 kept, the
  two typos (`SROLL 19`, `FIRE  ELE`) fixed by `amiga-import.json`'s `name_fixes`. Lines only DOS I holds take the common fallback (`en.fallback.p1-amiga-1.0.json`).
- **With DOS I** (same run: DOS runs first; or an earlier / later import): DOS I's tables win; the Amiga writes no
  table and warns. The runner no longer counts `asset-manifest.json` as a table (`tables` stays DOS's), and a re-run
  keeps files another version recorded since (Amiga, then DOS, then an Amiga recipe re-run leaves DOS's tables).
- **Played copy:** nothing read is rewritten by play, so the warning says the import equals a fresh copy's.
- Recipe 2 (an older Amiga import re-runs and gains the tables, 4.4). Not seen in the Client on Xvfb `[~]` (the
  load-and-start test covers the loader, a new game and leaving town).

### 7.7 s160: the III CGA pack, the topaz offer, I's dungeon note

- **III DOS CGA pack.** `P3DosPipeline` (recipe 2) also cuts the DOS pictures (`P3CgaArt`, port of
  `tools/scripts/extract_dos_p3_graphics.py`; formats in `reverse-engineering/dos/phantasie3/FORMAT.md` section 10)
  into `files/reverse-engineering/dos/phantasie3/extracted/graphics/`: the town (`TOWN.CMP`, or a data disk's
  `DTOWN.CMP`), the title / Astral Plane / Zeus / Pluto screens (`PICS.CMP`), 250 overworld tiles (`WORLD.CGA`),
  the party pictures, grave, dungeon glyphs, Scandor map and body-figure pieces (`SHAPES.CGA`) and the 80 monsters by
  DOS's own picture choice (`MONSTERS.CGA`), except D3-067 (s161): Wolf, Mud Elemental and Troll Baby take the Wild
  Dog's, the party elemental's and the party Troll's pictures (DOS: Skeleton / Skeleton / Forest Elf). The pack
  (`phantasie3/graphics/dos-cga.json`, class a) is never the default; `index.json` `fallback_order` draws it when no
  Amiga III pack is there, so a DOS-only III shows pictures (decision 1). The five picture files are checked by sha1;
  a copy without them (or another build's) imports the tables with a warning and records no art pack. New pack field
  `tile_maps.tile_swaps` ([region, map byte, tile]): DOS draws six Scandor tiles as others (ovl3 `0x0`); the Client's
  tile overworld applies it (`TileMapLayout.DrawnTile`). Golden test: 512 PNG pixel-equal to the Python extract.
- **The topaz offer.** A profile's `companions` (profiles.json; I and III Amiga: `kickstart-1.3`, output
  `GameData/<game>/graphics/fonts/amiga-topaz-8.json`, `gives` a string id) names a file its import wants from another
  copy. After a run, `ImportReport.Offers` lists each companion no version in `import.json` has written (read from the
  manifest's outputs, not the folder: the Client copies shipped GameData in). The Client's report shows "Missing for
  …: the Amiga screen font, topaz 8 … Press A to add that file"; A asks for one file (typed or dropped) and imports it
  alone: the Kickstart pipeline writes the font exactly as a full import with the ROM does, and the rest of the import
  stays. The CLI prints the same as a `missing:` line. I Amiga's old "No Kickstart 1.3 ROM" pipeline warning is gone
  (the offer replaces it, and it appeared even when an earlier import had the font).
- **I's dungeon note**: 7.3.

## 8. The public repo (s158)

`github.com/jjanow/OpenPhantasie`, created **private** (the user makes it public), one initial commit built by
`tools/scripts/export_public.py` into a folder next to this repo (`../OpenPhantasie`). This repo stays the private RE
workspace. Re-export into an empty folder to refresh it (the script refuses a non-empty target).

### 8.1 What the export takes (allow-list of tracked files)

- Code: `src/**`, `tests/**`, `tools/scripts/**` (not `export_public.py` itself, nor the `sNN_capture/` session scripts), `OpenPhantasie.sln`, `play.sh`, `go.sh`.
  `OpenPhantasie.Data.csproj` is written with **`OpenPhantasieBuiltInGameData` = false** (no fallback to committed
  GameData or the repo's art roots: every game needs an import). Executable bits are kept.
- Docs: `docs/plan/*.md` (not `SESSION-LOG.md`, `BOARD-ARCHIVE.md`, `HANDOFF-*.md`, `01-legal-and-sourcing.md`),
  `DIFFERENCES*.md`, and the specs `reverse-engineering/**/*.md` outside `working/`, `parity/`, `extracted/`,
  `unpacked/`, `original-*`, `translation/`, `ghidra/`, `screens-*` and `capture_*`. Every quoted line of the I-III
  text files, the string tables' original parts and IV's Japanese is replaced by `[original text]` (852 on the s158
  export). `AGENTS.md` and `PLAN.md` stay private.
- GameData by the class table in the script (`GAMEDATA_RULES`; a new GameData file without a class stops the export):
  (a) and (f) ship; (b) never; (c) files a pipeline reads ship as overlays with the copy's fields removed (I
  `asset-manifest` `assets`, `items` / `spells` names; II `atari-st.json` alert boxes; III `rule-tables` class / race /
  skill / social-class names and `towns` names; IV `rule-tables` `P4GameData.RuleTablePathsFromCopy`); the other (c)
  files are left out (the import writes them whole); string tables as in 7.6; IV `en.json` = the patch (1,477 ids).
- From `tools/public/`: `README.md`, `LICENSE` (MIT, "Jimmy Janow and OpenPhantasie contributors", 2026),
  `THIRD-PARTY-NOTICES.md` (MonoGame MS-PL, SDL2, OpenAL Soft, xUnit, test SDK, coverlet), `.gitignore`, and (s195)
  the README's five Client screenshots (`tools/public/screenshots/*.png` → `docs/images/`, the only images besides the icon).
- Never: disk images, ROMs, manuals, other screenshots, parity shots, extracted art / audio / text, translation sources, the
  DOS dungeon save fixtures (`DNG*.sav` hold a level's map).

### 8.2 Tests in the public tree

- `tests/Shared/OriginalData.cs` (linked into the three test projects): `[OriginalDataFact]` / `[OriginalDataTheory]`
  skip when the full GameData (one (b) table per game) is not next to the tests. 1,838 tests carry them (the ones that
  failed in the public tree). Importer golden tests keep `RequiresFilesFact` (reference images).
- Test methods whose assertions quote original text live in `*.OriginalText.cs` partials; the export leaves out every
  test file that quotes a text-file line, or (s169) a string-table line, when all its tests need the data and it holds
  at least one test (12 files on s158, 53 on s169). Fixture lines that were
  original text are made-up lines of the same length.
- s158 public tree: Core 1,079 pass / 1,155 skipped, Client 493 / 601, Importer 6 / 50, no failures. Private counts
  unchanged (2,730 + 1,331 + 140; Importer +7 new).
- s163 public tree (local export, not pushed): Core 1,079 / 1,157 skipped, Client 545 / 629, Importer 21 / 80, no
  failures. Two Importer tests read files the public repo leaves out without being gated (I's `dungeons.json` note,
  the ST cells against the ST GameData) and failed there since s160; now gated on those files.

### 8.3 Checks and the manifest

The export fails on: a file over 512 KB, any image / audio / disk / binary data file except the client icon and our
`GUILD.*.dat` test characters, an original-data path, any line of the I-III text files or IV's Japanese story lines
(16+ characters) outside `ALLOWED_QUOTES`, the per-game probe lines, a Steam user name. It lists for review: short menu
/ message lines quoted in code, tests and notes (146 files in the s158 export of 865 files, 11.5 MB; 149 of 886 before
s163, **61 of 891 (12.0 MB) after s163**: comments, XML docs and `_note` fields now name the string id in backticks
instead of quoting the line, by a one-off pass over the review list's comment lines; pack files the extractors write
byte for byte were left), long names in shipped GameData, the left-out tests (11). **s169: 2 of 893 (12.1 MB)**: the
rest name the string id, quoting tests moved into gated `*.OriginalText.cs` partials, stand-in lines made up; 53
left-out tests.
Acceptance (s158, by hand): the public CLI on the reference images (DOS I, Amiga I + Kickstart, ST II, DOS + Amiga III,
X68000 IV) + the public shipped GameData give all 88 GameData files JSON-equal to this repo's (string tables layered
as `StringTable.ForGame` does); the Apple II II import is byte-identical to the private build's; the public Client on
Xvfb lists all four games and starts I with the imported Amiga art.

### 8.4 Player builds, CI and the self-updater (s174)

- **Downloads:** `tools/scripts/package_release.py --rid <rid> --version <v>` publishes the Client self-contained
  (`OpenPhantasieBuiltInGameData=false`, so a build holds only the shipped GameData; players import their copies) and
  packs `OpenPhantasie-<v>-win-x64.zip` (folder `OpenPhantasie/`, `OpenPhantasie.exe`), `-linux-x64.tar.gz` (folder
  `OpenPhantasie/`) or `-osx-x64` / `-osx-arm64.tar.gz` (an `OpenPhantasie.app`, program in `Contents/MacOS/`, signed ad
  hoc by `codesign` on a Mac; not notarised). The program file is `OpenPhantasie` (the Client's `AssemblyName`, s174). The
  script deletes nothing: `--out` must not hold its `work-<rid>` folder.
- **CI:** `tools/public/github/workflows/build.yml`, exported as the public repo's `.github/workflows/build.yml` (the
  private repo runs no CI). **Runs only for a release (user s174):** a tag `v1.2.3` runs the tests (Ubuntu), builds the
  four downloads (Windows on `windows-latest` so the icon goes into the .exe, both macOS builds on `macos-latest`) as
  version 1.2.3 and publishes a GitHub release with `docs/release-notes/v1.2.3.md` as its notes (exported with the
  docs). Ordinary pushes and pull requests run nothing; a manual run builds artifacts as `0.0.0-ci.<run>`, no release.
- **Version:** a local build is `0.0.0-dev` (the Client's `Version` default); CI passes `-p:Version`. Only a version
  without a `-` suffix is a release build (`AppVersion.IsRelease`), and only a release build checks or updates.
- **Updater** (`src/OpenPhantasie.Client/Updates/`): `UpdateService` asks `GET /repos/<UpdateRepository>/releases/latest`
  (no sign-in; the repository is assembly metadata, `jjanow/OpenPhantasie` unless CI passes another; a private
  repository answers 404, read as "no release") and picks the asset `OpenPhantasie-*-<platform>.zip|.tar.gz` for this
  machine. `UpdateInstaller` downloads into `<install>/.update/`, unpacks, finds the folder laid out as the install (the
  one holding the program at the same relative path, so a top folder or an `.app` is fine), then moves every file in,
  renaming each one it replaces to `*.update-old` (all or nothing: a failed move puts everything back). The new process
  is started and the old one exits; at the next start a release build deletes the `*.update-old` files and the work
  folder. The install folder must be writable, else the screen says so and points to the releases page. Settings, saves
  and the import live in the user data folder and are not touched. `OPENPHANTASIE_UPDATE_API` points the check at
  another API root (testing).
- **Checked (s174):** Linux on Xvfb end to end: a packaged 1.0.0 against a local server offering 1.0.1 opened the update
  screen at start with the notes, downloaded, installed, restarted as 1.0.1 (old files cleaned), and UPDATE NOW then said
  "latest version". Windows (renaming the running .exe / DLLs) and macOS (the bundle) are by the code and CI only `[~]`.

## Appendix A: master images in the repo (reference fingerprints)

`reverse-engineering/<platform>/original-images/`, SOURCES.txt files left out. Size in bytes, sha1.

| Platform | File | Size | sha1 |
|---|---|---|---|
| amiga | `kickstart/kick34005.A500` | 262144 | `891e9a547772fe0c6c19b610baf8bc4ea7fcb785` |
| amiga | `phantasie1/phantasie-v1.0-1987-ssi.adf` | 901120 | `110e9768b9b3cf9d0b913aa1b4b89b0d9c469df9` |
| amiga | `phantasie1/phantasie-v1.0-1987-ssi.zip` | 308134 | `b8749a43f979df9f013108543d4286c74abd5a97` |
| amiga | `phantasie3/phantasie3-v1.0-1987-ssi.adf` | 901120 | `e4c128de73f5ebae4f83efadf3c15ffd6a109c60` |
| apple2 | `phantasie1/4am-crack/4am-notes.txt` | 1787 | `e76c8d0debe2a9cd799c8e98b01d852039ff69f8` |
| apple2 | `phantasie1/4am-crack/phantasie1-4am-side-a-boot.dsk` | 143360 | `42cae93c68cfb5bf7a52de3d695b876aac393736` |
| apple2 | `phantasie1/4am-crack/phantasie1-4am-side-b-dungeon.dsk` | 143360 | `5823879b2fa8f7471023b3b494fd2d686315bc26` |
| apple2 | `phantasie1/woz-original/phantasie1-boot.woz` | 234816 | `5ddc4bbd45d380105302a49b351a5c28647799ed` |
| apple2 | `phantasie1/woz-original/phantasie1-dungeon.woz` | 234819 | `2c9ad81a049c342eb097fc2916c6b2f7ef694211` |
| apple2 | `phantasie2/4am-crack/4am-notes.txt` | 1505 | `4e55720c9ae2d4fa23d3eeb28eeb21b5612b539d` |
| apple2 | `phantasie2/4am-crack/phantasie2-4am-side-a.dsk` | 143360 | `ffad268effaff2c501d5c3eb45f7db1a6998e088` |
| apple2 | `phantasie2/4am-crack/phantasie2-4am-side-b.dsk` | 143360 | `c766716e1c7e20e1c80402df0c3df838299e4d8a` |
| apple2 | `phantasie2/woz-original/phantasie2-disk1-side-a.woz` | 233452 | `37dfa92285776b261605221f85fcf3fb6adc4842` |
| apple2 | `phantasie2/woz-original/phantasie2-disk1-side-b.woz` | 233452 | `b395fd5175d875063dd5bfcaee040457ea1cd603` |
| apple2 | `phantasie2/memorial-set-gog/PHANT2A.DSK` (DOS 3.3 sector order) | 143360 | `e21f26ddefd9d6d4fa1b966cece09cedfb42966b` |
| apple2 | `phantasie2/memorial-set-gog/PHANT2B.DSK` | 143360 | `2f5d2bce122ffa417ac4c2b2873881f386c58991` |
| apple2 | `phantasie3/Phantasie_III_1987_SSI_cr_Disk_1_of_3_Side_A.dsk` | 143360 | `b7ae3f30298c41fd9a2abfa2bcd566cf4051a39a` |
| apple2 | `phantasie3/Phantasie_III_1987_SSI_cr_Disk_1_of_3_Side_B.dsk` | 143360 | `51446a1e54c1ef84c13bb480d559f9b240b5b4e1` |
| apple2 | `phantasie3/Phantasie_III_1987_SSI_cr_Disk_2_of_3_Side_A_Dungeon.dsk` | 143360 | `da829c612540f772012cc2bc11674a16ac72d4f4` |
| apple2 | `phantasie3/Phantasie_III_1987_SSI_cr_Disk_3_of_3_Side_A_Player.dsk` | 143360 | `4aac758b75a1e1d682e9477ebebb611a1b159dbc` |
| atari-st | `phantasie1/tosec-v2.0-repacked/phantasie1-v2.0-tosec-disk1.st` | 819200 | `81090efe59010c517f92b374206b86deb08b93ff` |
| atari-st | `phantasie1/tosec-v2.0-repacked/phantasie1-v2.0-tosec-disk2.st` | 737280 | `62f71840b671b2e10675cc839bf289956f6a4e53` |
| atari-st | `phantasie1/v1.0-de/phantasie1-v1.0-de-disk1.stx` | 792086 | `48cf671aa017a5f4d8540d32e19edb15cdada3b2` |
| atari-st | `phantasie1/v1.0-de/phantasie1-v1.0-de-disk2.stx` | 749856 | `6721ad525b7d32475a5235bfb0daa8cf20c459c5` |
| atari-st | `phantasie1/v2.0-en/phantasie1-v2.0-ssi.stx` | 1897860 | `74d1bce3b531dddabf73065280ecd1f573ba25cb` |
| atari-st | `phantasie2/phantasie2-v1.0-disk1.stx` | 883088 | `7d7d7a5dbe765c5e5adf6e8793699e43521700b7` |
| atari-st | `phantasie2/phantasie2-v1.0-disk2.stx` | 883970 | `096af7614f3b629c5a6c98f392d41c835a5ee682` |
| atari-st | `phantasie2/tosec-st/phantasie2-v1.0-tosec-disk1.st` | 368640 | `0168c78aaf7a600500e07e0db27d6bb672698da1` |
| atari-st | `phantasie2/tosec-st/phantasie2-v1.0-tosec-disk2.st` | 368640 | `d1f04b95b4ed951ad32cb7b84c367bc1ddb3b14e` |
| atari-st | `phantasie3/phantasie3-1987-ssi.st` | 737280 | `304d422a3e91ca9f78aad637c51453625f668847` |
| atari8bit | `phantasie1/phantasie1-side-a.atr` | 92176 | `a93d5f5350dfabb4d39abfded255378b64be24c2` |
| atari8bit | `phantasie1/phantasie1-side-b.atr` | 92176 | `fe5adccc9876e1b9568009fb5850f58449199385` |
| atari8bit | `phantasie1/phantasie1-side-c.atr` | 92176 | `850d849dd63643847dbc0c91956c49586e90f4cf` |
| atari8bit | `phantasie1/phantasie1-side-d.atr` | 92176 | `8170ed9b118b1fff34286f611601c7ad02c5c941` |
| atari8bit | `phantasie2/phantasie2-side-a.atr` | 92176 | `c9b7461427a23765831081f72b3ff1f7e9c2d48a` |
| atari8bit | `phantasie2/phantasie2-side-b.atr` | 92176 | `1a64f62011da91e190ba20fee232c35f97f47974` |
| atari8bit | `phantasie2/phantasie2-side-c.atr` | 92176 | `2abc0ca74b70dd81c81be1cc8298f29c4a7ad948` |
| atari8bit | `phantasie2/phantasie2-side-d.atr` | 92176 | `aee550286d4c24bc0f38793ad3b8a58b9e60549d` |
| c64 | `phantasie1/de/Phantasie_I_1986_SSI_de_Side_A.d64` | 174848 | `9b46f52dc155c3f2585b61fa14a0d575ad6bf30a` |
| c64 | `phantasie1/de/Phantasie_I_1986_SSI_de_Side_B.d64` | 174848 | `1f6cda627716c7a2d75a5cafd593cff1e1649feb` |
| c64 | `phantasie1/en/Phantasie_I_1986_SSI_Side_A.d64` | 174848 | `be66cf9d9aca3072ef2013c062b8c5d3a77635cf` |
| c64 | `phantasie1/en/Phantasie_I_1986_SSI_Side_B.d64` | 174848 | `a29d02baa60bb50c15d58d93a5979c395127c0bd` |
| c64 | `phantasie2/Phantasie_II_1986_SSI_Side_A.d64` | 174848 | `3c69be20e0a9106c5ccb1402564cc3305a938254` |
| c64 | `phantasie2/Phantasie_II_1986_SSI_Side_B.d64` | 174848 | `27d27c679519ff366c949d836cf1a2e5d9157609` |
| c64 | `phantasie2/v1.2-side-a-only/Phantasie_II_v1.2_1986_SSI_Side_A.d64` | 174848 | `9d6c9d848e2be80857711be14ce01d723911b6ed` |
| c64 | `phantasie3/Phantasie_III_1987_SSI_Disk_1_of_2_Side_A.d64` | 174848 | `440db1ba6d4b8cd87a324be3a05232dcf3a03eff` |
| c64 | `phantasie3/Phantasie_III_1987_SSI_Disk_1_of_2_Side_B.d64` | 174848 | `3993a0c86f4b91f9502dde4af7f24cd20cb2c86c` |
| c64 | `phantasie3/Phantasie_III_1987_SSI_Disk_2_of_2_Side_A.d64` | 174848 | `48437d833f99cd56188a14dcbd0670d6b8f159a9` |
| c64 | `phantasie3/v1.1/Phantasie_III_v1.1_1987_SSI_Side_A.d64` | 174848 | `bdfe269c7f4190a1d6494f18df8e7a391da46754` |
| c64 | `phantasie3/v1.1/Phantasie_III_v1.1_1987_SSI_Side_B.d64` | 174848 | `61be8fa4b53fb30bfe744267f0aa0235507e2618` |
| dos | `phantasie1/phantasie-1987-ssi-360k.ima` | 368640 | `40eeb75aaaa46382caeb256cfb0c7930ce78d6c2` |
| dos | `phantasie1/phantasie-1987-ssi-360k.zip` | 170670 | `89b33da4356b780182ce0ef6a310040bdb82ce14` |
| dos | `phantasie1/bonus-v1.1-1991-wizardworks/phantasie-bonus-v1.1-1991-wizardworks-360k.ima` | 368640 | `87bc9e6a9a28c0290ce31dd305bc6477735ed0d6` |
| dos | `phantasie3/phantasie3-1988-ssi-360k.ima` | 368640 | `6307862a0e46d3da86bec630f89d2915559cf369` |
| dos | `phantasie3/wizardworks-1991/phantasie3-1991-wizardworks-360k.ima` | 368640 | `1170478efe9bb98fae0f4ada180e0fe5e44159ed` |
| dos | `memorial-set-gog/setup_phantasie_memorial_set_1.0c_(69737).exe` (GOG installer; unpacked I / III folders in `phantasie1/` / `phantasie3/memorial-set-gog/`, `SHA256SUMS` there) | 14594240 | `87e2cd6cc3103869748d3a16972f94dc170e530d` |
| msx2 | `phantasie1/Phantasie (1988)(Bothtec)(JP).dsk` | 737280 | `00f86b4baa57b2378ae60c4cb66207a026e111be` |
| msx2 | `phantasie2/Phantasie II - Story of Ferronrah (1988)(Bothtec)(JP).dsk` | 737280 | `f1ac4dbe51977a594fb396c39290941d7f16ee03` |
| msx2 | `phantasie3/… (Disk 1 of 2)(Scenario Disk).dsk` | 737280 | `f5694994b35d6550de89fd9de39d0e53f8943822` |
| msx2 | `phantasie3/… (Disk 2 of 2)(User Disk).dsk` | 737280 | `6511beda64953ab063bea4c0bd25e11823b4b9b9` |
| msx2 | `phantasie4/… (Disk 1 of 2)(Scenario Disk).dsk` | 737280 | `d38b9de3a630e443d0deeaa7f89bf5c9833fde99` |
| msx2 | `phantasie4/… (Disk 2 of 2)(User Disk).dsk` | 737280 | `7542c7608e85c0755e13b718b98051ee312c4ab3` |
| msx2 | `phantasie4/patch-2008/phantasie4-Player DISC-django.dsk` | 737280 | `94a7a928152c47bbed0a2990f7e380547b08789f` |
| msx2 | `phantasie4/patch-2008/phantasie4-Scenario DISC1-django.dsk` | 737280 | `8c023e8e924d2e91e4da33336f2753c8bd28331c` |
| msx2 | `phantasie4/patch-2008/phantasie4.pdf` | 11120 | `2500e58d2acadf48616837ba5e4aa751722523c0` |
| pc88 | `phantasie1/Phantasie (1986)(Star Craft)(Disk 1 of 2)(Program Disk).d88` | 346080 | `23012af327b15753ad0b13cf704d6a8545d7f5bf` |
| pc88 | `phantasie1/Phantasie (1986)(Star Craft)(Disk 2 of 2)(Player Disk).d88` | 348848 | `892fe8bf8ff7b691aae45eb0b005c983d21c4bf2` |
| pc88 | `phantasie2/… (Disk 1 of 3)(Program Disk).d88` | 348848 | `124e5403bec4c8b65413b47b0fce606d91b49db2` |
| pc88 | `phantasie2/… (Disk 2 of 3)(Player Disk).d88` | 348848 | `271803cb620d16a8d43a788421b1fa091d8d7c34` |
| pc88 | `phantasie2/… (Disk 3 of 3)(Utility Disk).d88` | 348848 | `b1cdd304e82a5e82b0ed6c79461b9a48703e31bd` |
| pc88 | `phantasie3/… (Disk 1 of 3)(Program Disk).d88` | 345568 | `78937911c34df1b06206a4320d5bb086e78f4ac2` |
| pc88 | `phantasie3/… (Disk 2 of 3)(Player Disk).d88` | 348848 | `a5afe30093e84ac5ba16d68b0b0759cf857c05d5` |
| pc88 | `phantasie3/… (Disk 3 of 3)(Utility Disk).d88` | 348848 | `31851ccac0cb2de059eda15e3c893920800b2961` |
| pc88 | `phantasie4/… (Disk 1 of 3)(Player Disk).d88` | 348848 | `1f778bad497fe332941ce672ca225cf9223e2eb7` |
| pc88 | `phantasie4/… (Disk 2 of 3)(Scenario Disk 1).d88` | 348848 | `ec67217339edf0b149e07fd6d065aa3f8f59fd68` |
| pc88 | `phantasie4/… (Disk 3 of 3)(Scenario Disk 2).d88` | 348848 | `12303f8008bed337b1ac736be623512bfa8467dd` |
| pc98 | `phantasie1/Phantasie (Player disk).d88` | 697008 | `3b527a461ffcc6c5372f18644c379b3e3f97406f` |
| pc98 | `phantasie1/Phantasie (Program disk).d88` | 697008 | `a90e6d87916d73d748c24f089f7c0679675095e5` |
| pc98 | `phantasie2/Phantasie II (Player disk).d88` | 697008 | `d1addb2402b52ad2fe965aa9c622e480e5439a4d` |
| pc98 | `phantasie2/Phantasie II (Program disk).d88` | 697008 | `770be69d5e6c35517146859e8e0ec6c90a264ceb` |
| pc98 | `phantasie2/Phantasie II (Utility disk).d88` | 697008 | `a548c1330c376da08b5268d0646d62eeea021430` |
| pc98 | `phantasie3/Phantasie III [Set 1] (Player disk).d88` | 1086448 | `a5b4d1260ddaf7196bdf376750be18625ff3c582` |
| pc98 | `phantasie3/Phantasie III [Set 1] (Program disk).d88` | 1080448 | `22f4c7d8143109a4ec9d8a9ef822226260b6e9dc` |
| pc98 | `phantasie3/Phantasie III [Set 1] (Utility disk).d88` | 1086448 | `03a5c359e141a7cd3f98f3b6a8024bd1c20e4675` |
| pc98 | `phantasie4/Phantasie IV (Disk 1).hdm` | 1261568 | `1cc635da46bfbec0faabfcf449d575e77dcf7f0f` |
| pc98 | `phantasie4/Phantasie IV (Disk 2).hdm` | 1261568 | `8a97eb5c8c32f4e136d2433b69587041619545aa` |
| x1 | `phantasie1/Phantasie (Disk 1 - Program disk).d88` | 348848 | `bca1f085513e4a33f0a8ac648c05954df4cf4f8f` |
| x1 | `phantasie1/Phantasie (Disk 2 - Player disk).d88` | 348848 | `5443436262270e298f92883e1f03a884deebddae` |
| x68000 | `phantasie4/Phantasie IV (1991)(Starcraft)(Disk 1 of 2).dim` | 1261824 | `3f458eec2e33bf47d0be3c7c0327ba92828478b8` |
| x68000 | `phantasie4/Phantasie IV (1991)(Starcraft)(Disk 2 of 2).dim` | 1261824 | `281afb0d30e3b9d784cab27445afff393ddce029` |

`…` = the TOSEC name as in the folder (shortened here; full names in each `SOURCES.txt`).

## Appendix B: key files of the supported builds

Read-only program and data files (never written by play), so they identify a build inside any container or folder.
Taken from the unpacked copies in the repo.

| Build | File | Size | sha1 |
|---|---|---|---|
| I DOS 1987 | `PHANTASI.COM` | 34816 | `9d5d4bf41e0342091264231ed3c535949fb2111c` |
| I DOS 1987 | `OV1.OVR` | 25703 | `672632f4292cbbee86af27107a2e6e393ab5c4e2` |
| I DOS 1987 | `OV2.OVR` | 32963 | `334456689e017fd7e2fb134d57801ea349ea47da` |
| I DOS 1987 | `TWNS.INT` | 12987 | `82532a99586d62c5b2a33060038e9755cbddecbf` |
| I DOS 1987 | `PAT` | 450 | `0d2a8b41008609482390f16feb02389b11d30c49` |
| I DOS 1987 | `PELNOR.IBM` | 16384 | `fc8415d91dc1e3ce5c14887f9b3f7be22885aa05` |
| I DOS 1987 | `SCROLLS.DTX` | 16000 | `389814ca45f5c4c60aa5919fafb7f1efe8d360e8` |
| I DOS Bonus | `PHANTASI.EXE` | 21101 | `9ea72256094488bd00568205520e5b20df6298ca` |
| I DOS Bonus | `OV1.OVR` | 25703 | `3cd725a48954cb7ba508890c769eed153a2b6928` |
| I DOS Bonus | `OV2.OVR` | 32963 | `bf215b3d4d376ca532b2264dd8204e699fbfc27e` |
| I DOS Bonus | `TWNS.INT`, `PAT`, `PELNOR.IBM`, `SCROLLS.DTX` | | as 1987 (Ver 1.0 and 1.1 alike) |
| I Amiga v1.0 | `game` | 193804 | `fc3ce94d649684ea3003b404f662dec74ddd5e9e` |
| I Amiga v1.0 | `tamb.dat` | 2850 | `a2e342a5ed03642bc0d15c3d001aaf613f36e6ff` |
| I Amiga v1.0 | `twns.int` | 13000 | `d1d6e5d4b9243f48b08eceb8cb3652527b5b92c4` |
| I Amiga v1.0 | `mstr1.pat` | 68016 | `f959eb9858b8bfa0018a1a0913cc2227a12cc3d8` |
| I Amiga v1.0 | `maps.int` | 9360 | `8dc6438f33f0ad92dd65bef8a8dbc77f73dea81a` |
| II ST v1.0 | `PHANT.PRG` | 155785 | `1386c07157720536852d853d1a80d8237e9a8592` |
| II ST v1.0 | `START.PRG` | 2567 | `e79d928c3628f58b40062016a73cf25366202c91` |
| II ST v1.0 | `TWNS.INT` | 13900 | `b5c4b95f38e72b0c06d4029729dc67ba2d055cb5` |
| II ST v1.0 | `MAPS.INT` | 8840 | `16c993df0bb09e14e9779a6e989255bc34b7d484` |
| III DOS 1988 | `MAIN.EXE` | 152564 | `b85cd0eaab25884af4e90d298407e42a288b5ede` |
| III DOS 1988 | `CREATURE.DAT` | 3072 | `a6341c8b7c4bb4bb473682f5267df4ad30850803` |
| III DOS 1988 | `DNGS.DAT` | 46464 | `20bfda72d55ee07801764ed33a3c00fb46804d9f` |
| III DOS 1988 | `WORLD.DAT` | 3750 | `3b4aaa861bf40760eb9dd727593ba159094f6ac0` |
| III DOS 1988 | `PLNS.DAT` | 658 | `5f851b3ffdd8e7663ba3b9e97452692f81f7220a` |
| III DOS 1988 | `INITARMS.DAT`, `SCROLLS.DAT` | 3328, 19950 | `56a360b7b000150cba73c546d5030522be54641b`, `643f7bbfb2ec53920103fe0ce2472030d20b7565` (s158: read at fixed offsets) |
| III DOS 1988 | `TOWN.CMP`, `PICS.CMP`, `WORLD.CGA`, `SHAPES.CGA`, `MONSTERS.CGA` | 10736, 30170, 16000, 16192, 48768 | `642b4e3e…`, `a4c9c882…`, `3ac9f4ac…`, `e7a81eb9…`, `088129fb…` (s160: not key files; the CGA pack checks them, `P3CgaArt.Sources`) |
| III Amiga v1.0 | `PhantasieIII` | 174928 | `40548339ddbcbc3be8c2a17055f322211b538150` |
| III Amiga v1.0 | `monsinfo.dat` | 3072 | `a6341c8b7c4bb4bb473682f5267df4ad30850803` |
| III Amiga v1.0 | `D/m0` | 896 | `b7cfea849f37d08467e065b02c7b8abb856ed290` |
| IV X68000 1.02 | `P4.X` | 341164 | `2a7e8ce31693524e11f896f30a3950a73c70ec48` |
| IV X68000 1.02 | `P4.SND` | 16000 | `9ff80524abfd2c4941b176300c3d6b10d37db591` |
| IV X68000 1.02 | `VDATA` | 6609 | `82d268e44a397db7e0ce6a6386091bc61d41f0a6` |
| IV X68000 1.02 | `TABL` | 2130 | `aed9290319305193de884a4a16c01843b517c29f` |
| IV X68000 1.02 | `DATM` | 5880 | `a4912cb4f4ed43bc9ab34754fbefa398b4dc3fbd` |
| IV X68000 1.02 | `MOUT` | 12600 | `9e9f0cc61c3409381c3fcf9b99390472980475f5` |

III WizardWorks 1991: only its extra `.BAT` files are unpacked in the repo; hash its data files from the `.ima` when
that profile is written. The IV disk listings with per-file sha1s are in
`reverse-engineering/x68000/phantasie4/unpacked/disk1.json` / `disk2.json`.
