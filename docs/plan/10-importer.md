# Phase 10: First-run importer (design, s132)

> **Active** (user, s158, 2026-11-07): SNEG never replied; release work proceeds. The six design questions are answered in section 6.

Design and state of the PLAN "Release readiness" item **First-run importer**. Sections 1–5 are the design; section 6
the user's decisions; section 7 what is built and what is left; section 8 the public repo. Decided by the user (PLAN,
s105) and not reopened here: the public repo (OpenPhantasie) ships only our own code, tools, tests and specs; pure
numbers may ship; text, maps, art and music are built on the player's machine from the player's own copy of **any
supported version** of each game; IV's English ships as a patch keyed by id over the player's Japanese text, and
`ja.json` is built by the importer, never shipped.

Tags: `[C]` checked in the files, `[~]` likely, `[?]` unknown, `[U]` the user decides (all decided, section 6).
Counts were taken on 2026-11-01 (worktree of `fcaa0aa`); the GameData file list, class counts and script line counts
(5.2) were rechecked on 2026-11-02. Per-file bytes, id counts and the PNG counts in 2.6 are the 2026-11-01 values.

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

86 files, 3.8 MB on 2026-11-01; the tables below add the files later sections name (2.7: 89). On 2026-11-14
GameData holds 110 files; not listed here are the per-table `strings/en.sources.json` (7.6), the shipped string
fallbacks (`fallback.en.json`, III's `fallback.p3-apple2.en.json`) and III's `apple2-import.json` (7.10). Classes:

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
| `amiga-import.json` | 3,049 | DOS I's value in the 8 cells where the Amiga's numbers differ, for an Amiga-only import (7.8) | a | hand-written | — |
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

I: **a 11, b 7, c 8, f 1** (27 files; the heading keeps the earlier 26).

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
| `apple2-import.json` | 11,617 | a | hand-made: the 37 ST cells, the compass word map and the manual's spell names an Apple-only import takes (D2-046..050, 7.5) |
| `strings/en.json` | 6,065 | c | hand-written; 83 ids, the `core.p2.*` lines quoted from PHANT.PRG |
| `strings/fallback.en.json` | 1,538 | a | hand-written: the remaster's wording for the 18 ids an Apple-only II lacks (7.6) |
| `graphics/index.json` | 353 | a | hand-written |
| `graphics/atari-st.json` | 8,413 | a | `build_st_p2_asset_manifest.py` |

II: **a 10, b 5, c 6** (21 files).

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
| `graphics/dos-cga.json` | 4,784 | a | hand-written, the DOS CGA pack (art by `extract_dos_p3_graphics.py` / `P3CgaArt`) |
| `amiga-import.json` | 36,693 | a | DOS III's value in the 156 cells where the Amiga's differ, for an Amiga-only import (user s209, 7.9) |
| `graphics/fonts/amiga-topaz-8.json` | 34,659 | b | a copy of I's (Kickstart 1.3 ROM topaz 8) |

III: **a 7, b 5, c 7** (19 files; the heading and 2.7 keep the 2026-11-02 count, a 5, 17 files, before `dos-cga.json` and `amiga-import.json`).

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
| `graphics/x68000-panels.json` | 17,283 | b | the Command panel tables (`_mkicon`), the window-36 icon rows and `_noyes` from P4.X by `x68k_p4_panels.py`; holds P4.X's Shift-JIS labels / titles (`text`), so it sits with `ja.json` (imported) |
| `graphics/fonts/x68k-p4-*.json` (4) | 205,836 | b | P4.X's own glyph tables (font 0, its italic and bold styles, font 1) by `x68k_p4_fonts.py` |

IV: **a 5, b 10, c 7** (the four font files counted one by one).

### 2.6 Outside GameData: art roots and raw files (all b)

File counts are the 2026-11-01 ones; the importer's golden tests (7.2) count I Amiga 256 PNG + 5 JSON, II ST 262 PNG +
4 JSON, IV 382 PNG (every PNG has a generator). The II and III Apple II packs (7.2, 7.10) are not in this table.

| Pack | Root (today) | Files | Bytes | Scripts | Raw files read verbatim |
|---|---|---|---|---|---|
| I Amiga | `reverse-engineering/amiga/extracted` | 249 PNG + 5 JSON | 431,975 | `extract_amiga_graphics.py`, `extract_amiga_map_graphics.py` (reads I's `overworld.json` + `map-patches.json`), `build_amiga_asset_manifest.py` | `unpacked/PHANT/tamb.dat` (title tune), `unpacked/PHANT/game` (sound effects) |
| I DOS CGA | `reverse-engineering/dos/extracted` (`tiles/`, `ui/`) | 25 PNG + `tiles.json` | — | `extract_dos_map_tiles.py`, `extract_dos_town_picture.py` | — |
| II Atari ST | `reverse-engineering/atari-st/phantasie2/extracted/graphics` | 260 PNG + 4 JSON | 348,197 | `extract_st_p2_graphics.py`, `build_st_p2_asset_manifest.py` | `unpacked/disk1/START.PRG` (title tune) |
| III DOS CGA | `reverse-engineering/dos/phantasie3/extracted/graphics` | 512 PNG + `extraction-index.json` | 267 KB | `extract_dos_p3_graphics.py` (also reads III's `rule-tables.json` `core_race`) | — |
| III Amiga | `reverse-engineering/amiga/phantasie3/extracted/graphics` | 579 PNG + 2 JSON | 952,584 | `extract_amiga_p3_graphics.py` (also reads DOS `WORLD.DAT` / `PLNS.DAT` and III's `rule-tables.json`) | `unpacked/PhantasieIII/D/m0`, `D/m1` (tunes), `PhantasieIII` (sound effects) |
| IV X68000 | `reverse-engineering/x68000/phantasie4/extracted/graphics` | 288 PNG | 999,194 | `x68k_p4_pkh.py`, `x68k_p4_pix.py`, `x68k_p4_fonts.py` (`ui/items/`, and the pack's `fonts/x68k-p4-*.json` from `P4.X`), `x68k_p4_panels.py` (the pack's `x68000-panels.json`, the Command panel tables from `P4.X`) (no single driver script; the importer port regenerates every PNG, 7.2) | `unpacked/disk1/P4.X` (OPMDRV tunes), `unpacked/disk1/P4.SND` (voices), `unpacked/disk1/VDATA` (the ADPCM sound effects) |

### 2.7 Totals

| | a | b | c | f | Files |
|---|---|---|---|---|---|
| Shared | 1 | 0 | 1 | 0 | 2 |
| I | 11 | 7 | 8 | 1 | 27 |
| II | 10 | 5 | 6 | 0 | 21 |
| III | 5 | 5 | 7 | 0 | 17 |
| IV | 5 | 10 | 7 | 0 | 22 |
| **All** | **32** | **27** | **29** | **1** | **89** |

Plus the art roots of 2.6 (5 roots, 1,401 PNG on 2026-11-01) and 8 raw files, all (b).

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
| I | DOS Bonus Edition Ver 1.1 11/12/90 (WizardWorks 1991 `.ima`) | every I table + DOS CGA pack, as 1987 (`PHANTASI.EXE` is LZEXE 0.90 over the same EXEPACK image; unpacked it is 1987's but one byte; every data file and every offset read is the same, `dos/bonus-edition.md`) | as 1987 (+ `re_common.unpack_phantasi`) | supported (`p1-dos-bonus-1991`, `P1DosBonusPipeline`: output equal to 1987's) |
| I | Memorial Set DOS I (Steam app 2702050, GOG product 1919929332; DOSBox): the **Bonus Edition Ver 1.0 9/21/90**, every game file = Ver 1.1's (`dos/original-images/memorial-set-gog/README.md`) | as Bonus Ver 1.1 | as 1987 | supported (found as `p1-dos-bonus-1991`, unplayed) |
| I | **Amiga v1.0 1987** (`.adf`, also `.zip`) | every I table (b, c) without DOS I (7.8: the Amiga's text, DOS's numbers) + Amiga pack: art, map pictures, title tune, sounds | `amiga_gfx_common`, `extract_amiga_graphics`, `extract_amiga_map_graphics`, `build_amiga_asset_manifest`, `extract_amiga_fonts` (topaz 8 needs the Kickstart 1.3 ROM) (disk unpack: amitools `xdftool`) | supported (tables too, `P1AmigaTables`, `reverse-engineering/amiga/GAME-TABLES.md`; DOS I's tables win when both are imported) |
| I | **C64 v1.4** (C64PP `.g64` pair, or the held en `.d64` pair, a played v1.4) | every I table (b, c) without DOS I (7.11: the C64's text, DOS's numbers) + C64 art pack (s218) | `extract_c64_d64.py`, `c64_g64.py` (G64 → D64), `build_c64_p1_import.py` (the shipped cells) | supported (`p1-c64`, `P1C64Tables`, s216; towns / regions from side 2's `PATTERN`, so a played copy imports as fresh; formats `reverse-engineering/c64/phantasie1/FORMAT.md`; DOS I's or the Amiga's tables win when imported too) |
| I | C64 v1.0–v1.3 (C64PP `.g64`); C64 German (`.d64`) | — | as v1.4 | v1.0–v1.3: "another build" (their programs differ; v1.1's `MN` is damaged); German: recognised (`p1-c64-de`, its own key files) |
| I | **Apple II v1.0** 1984 (4am `.dsk` pair, or the original `.woz` pair: one played copy) | every I table (b, c) without DOS I (7.14: the Apple's text, DOS's numbers) | `a2_rdos`, `a2_applesoft`, `diff_a2_p1_vs_dos.py` (the shipped cells; the C# pipeline is the reader) | supported (`p1-apple2`, `P1Apple2Tables`, s222; towns from side B's `TF`, regions given DOS's start state, so a played copy imports as fresh; formats `reverse-engineering/apple2/phantasie1/FORMAT.md`; DOS I's, the Amiga's or the C64's tables win when imported too); the `apple2` art pack (s223, `P1Apple2Art`, `GRAPHICS.md`) |
| I | Atari ST (v2.0 en, v1.0 de), Atari 8-bit, MSX2, PC-88, PC-98, X1 | — | — | recognised by hash only |
| II | **Atari ST v1.0** (Pasti `.stx`; TOSEC `.st` has byte-identical program and data files) | every II table + ST pack + title tune | `stx_to_st`, FAT12 unpack (mtools today), `st_prg`, `extract_st_phantasie2`, `extract_st_p2_maps` / `_dungeons` / `_scrolls` / `_towns`, `build_p2_gamedata`, `extract_st_p2_graphics`, `build_st_p2_asset_manifest` | supported |
| II | **Apple II** v1.0 2/5/86 (4am `.dsk`, or the original `.woz` pair): the Memorial Set's II | reference extract: monsters, items, names, creation tables, towns, wilderness, dungeons + messages, scrolls; Apple II art pack | `a2_rdos`, `a2_applesoft`, `extract_a2_p2_tables`, `diff_a2_p2_vs_st`, `extract_a2_p2_graphics`, `build_a2_p2_asset_manifest` (`reverse-engineering/apple2/phantasie2/FORMAT.md`) | supported (C# port, `p2-apple2`, 7.2 / 7.5): the Apple's text, the ST's numbers (D2-046…050, user s159). No spell names on the Apple: the manual's list is shipped; divine #57 / #58, unnamed there, take the ST's names (D2-050, user s170, 7.5). The Memorial Set's disks (unplayed, shipped fog) are in DOS 3.3 sector order; the reader tries both orders and finds them (their save files are the profile's pristine sha1s) |
| II | Apple II v2.1 8/19/86 (RDOS 3.3 `.dsk` pair; the A2CAN original, played) | as v1.0: every II table + Apple II art pack (the same data files, `apple2/phantasie2/FORMAT.md` section 4) | `a2_rdos` (reads RDOS 3.3), `build_string_sources.py` (its own string offsets) | supported (`p2-apple2-2.1`, `P2Apple2V21Pipeline`; 7.5). Restored, its import equals the Memorial Set's v1.0 import but for the version's name. With v1.0 in the same run (or an earlier import), v2.1's tables win. Its DUNGON2 gold-op fix changes nothing here (Core plays the ST's script; D2-053) |
| II | **C64 v1.2** (C64PP `.g64` pair, or the held `.d64` pair, a played v1.2) | every II table (7.12: the C64's text, the ST's numbers) + C64 art pack (s219) | `extract_c64_d64.py`, `c64_g64.py`, `build_c64_p2_import.py` (the shipped cells) | supported (`p2-c64`, `P2C64Tables`, s218; towns / regions / elementals from side 2's `PATTERN2`, so a played copy imports as fresh; formats `reverse-engineering/c64/phantasie2/FORMAT.md`; the ST's or the Apple's tables win when imported too) |
| II | C64 v1.0 (C64PP `.g64`) | — | — | recognised (`p2-c64-1.0`, its own key files; user s217: v1.2 only) |
| II | Atari 8-bit, MSX2, PC-88, PC-98 | — | — | recognised by hash only |
| III | **DOS SSI 1988** (`.ima`) | every III table + DOS CGA pack (drawn when no Amiga III pack is imported) | `extract_dos_p3_common` (EXEPACK unpack), `extract_dos_p3_tables` / `_dungeons` / `_rules` / `_scrolls` / `_towns` / `_world`, `build_p3_gamedata` | supported |
| III | DOS WizardWorks 1991 (`.ima`) | as 1988 (its unpacked folder in the repo holds only the extra `.BAT` files) | as 1988 | supported: detected by `p3-dos-1988` (its image is in that profile's `images`), identical output (7.2) |
| III | Memorial Set DOS III | as 1988: every game file byte-identical | as 1988 | supported (found as `p3-dos-1988`) |
| III | **Amiga v1.0 1987** (`.adf`) | every III table without DOS III (7.9: DOS's values where the Amiga differs, user s209) + Amiga pack: art, tunes, sounds (the map previews also read DOS's `WORLD.DAT` / `PLNS.DAT` when given; `monsinfo.dat` = DOS `CREATURE.DAT`, same sha1) | `extract_amiga_p3_common`, `extract_amiga_p3_graphics` | supported (tables too, `P3AmigaTables`, `amiga/phantasie3/FORMAT.md` "Game tables for a standalone import"; DOS III's tables win when both are imported) |
| III | **Apple II** v3.0 5/26/87 `.woz` pair (uncracked, unplayed; the reference) / v1.0 `[cr]` `.dsk` sides (played, five scroll files damaged) | tables + Apple II art pack: monsters, items, names, towns, rule tables, Scandor / planes / places, dungeons + texts, scrolls | `a2_woz`, `a2_rdos`, `detok_apple2_p3`, `extract_a2_p3_tables`, `diff_a2_p3_vs_dos` (`reverse-engineering/apple2/phantasie3/FORMAT.md`) | supported (`p3-apple2`, 7.10): the Apple's text, DOS III's numbers (`apple2-import.json`, 653 cells). Key files the data both builds share (`TF3`, `ITEM`, `TINFO`; `DNG1`, `DNG10`); string alternatives from v3.0's programs only (17 verbatim, 128 in its own wording, D3-077); the rest in `fallback.p3-apple2.en.json` (218 ids, all of them for a [cr] copy). Art: the Apple II pack (`P3Apple2Art`, `extract_a2_p3_graphics`, `build_a2_p3_asset_manifest`; `apple2/phantasie3/GRAPHICS.md`), drawn after the Amiga's; the real title / ending only from v3.0 |
| III | C64 v1.1 (`.d64`, the ASS crack lineage); C64PP v1.0 (!) / v1.2 (alt) (`.g64`) | tables + starter party + C64 art pack (s220) | `extract_c64_d64.py`, `c64_g64.py` (G64 → D64) | supported s218 (`p3-c64` v1.0, also the held v1.1 / cracked pairs; `p3-c64-1.2`), 7.13; formats `reverse-engineering/c64/phantasie3/FORMAT.md` |
| III | Atari ST, MSX2, PC-88, PC-98 | — | — | recognised by hash only |
| IV | **X68000 Ver.1.02** (`.dim`) | every IV table, `ja.json`, X68000 pack, tunes, voices | `x68k_dim`, `x68k_x`, `extract_x68k_p4_tables` / `_creation` / `_text`, `x68k_p4_events`, `build_p4_gamedata`, `x68k_p4_pkh`, `x68k_p4_pix`, `x68k_p4_fonts`, `x68k_p4_panels`, `x68k_p4_anim` | supported |
| IV | PC-98 (`.hdm`), MSX2 (`.dsk`, + 2008 patch images), PC-88 (`.d88`) | nothing; IV's string ids are X68000 addresses | — (`msx2_p4_dis.py` is a disassembler) | not supported |

Consequence (checked on the GOG install): a player with only the Memorial Set gets I's tables (the Bonus build), II's
(Apple) and III's. Neither I nor III gets **default art** from it (the DOS CGA is never the default:
`phantasie1/graphics/index.json`, user 2026-10-25), but the CGA packs are drawn when no Amiga pack is imported
(`fallback_order` in both indexes), and II gets the Apple II pack.

### 3.2 Detection

1. **Container** by extension, size and signature: ADF 901,120 B (OFS); `.ima` / `.st` / MSX `.dsk` FAT12 (360K
   368,640 B, 720K 737,280 B); `.stx` starts `RSY\0`; `.dim` 1,261,824 B with `DIFC HEADER` at 0xAB; `.xdf` / `.hdm` raw
   2HD 1,261,568 B (the `.dim` payload without its 256-byte header `[~]`); `.d64` 174,848 B; `.d88` header; Apple `.dsk`
   143,360 B, `.woz` `WOZ1`/`WOZ2` (decoded by `WozImage`: 13-sector 5-and-3 to the 4am layout, or 16-sector 6-and-2 to a DOS-order `.dsk`, whichever decodes more sectors; read as RDOS, else fingerprinted; the container keeps the `.woz` sha1. The SSI disks' undecoded track 0 sector 12 holds the 16-sector boot sector, not data: `apple2/phantasie2/FORMAT.md` section 1); `.zip` (opened in memory); a plain folder (searched recursively).
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
| I Amiga | `OUT*.DAT` on the disk: the game rewrites a region's file (upper-case name, `OUT%d.DAT`) with fog lifted and fixed fights spent (`inc` of the cell). The pipeline reads only the polygons (offset 1000) and the town / dungeon cells (classes 4 / 5, fog stripped); fog and fights come from DOS I's tables, so play changes nothing it imports. An Amiga-only import takes the grids from `maps.int` (a key file, never written) and only the feature records and text pages, which play leaves, from `out*.dat` (GAME-TABLES.md section 3). Checked on the reference dump: its played cells differ only in fog and in spent fights; OUT9's three 68s at (1, 18), (15, 19), (20, 9) are the Amiga's own data, DOS 67 | yes (icons; features and text pages) | not needed. All 18 files have pristine sha1s (`mutable_files`): the reference dump's eleven lower-case files, dated 06.11.1986 12:12–12:13 (mastering) with every cell fogged, and for the seven played ones the unplayed WHDLoad package `phantasie-v1.0-whdload-1185.lha` (all 18 dated at mastering; its eleven equal the dump's; every played file in four ADFs differs from it only by fog and spent fights). The reference dump is played: `OUT1` / `OUT5` / `OUT9` written 22.04.1992 with `guild.dat` and the saves, `OUT10` / `11` / `13` / `14` by the game on 06.11.1986 17:48–18:53 (a clockless Amiga's date, fog partly lifted); it reports five played files (OUT9 / 10 / 11 / 13 / 14). So OUT16 (4, 15) = 88, DOS 87, is the Amiga's own data, not a spent fight; OUT2 (11, 8) and OUT12 (11, 14) also differ from DOS. A mismatch warns that the import is unchanged |
| I Apple | `CHAR` (Guild save), `TWN1`–`TWN11` / `TWN99` (town Save), `OUTn` (stored back on leaving a region: fog and spent fixed fights), `DNGSAV` / `DNGCHK`. The Apple's new game rewrites only `CHAR` records 0–36 and the towns (from `TF`), never the regions (D-088) | `OUTn` (grids, features, texts); `CHAR` records 37–39 (the elementals, kept by every new game) | towns: `TF` (side B), whose slices are the `TWNn` sha1s (`mutable_files`). Regions: none (no unplayed copy known): every region gets DOS I's start state (DOS's Reset rule, all but OUT1 / OUT5), so a played copy imports as fresh (`PlayedCopySafe`) |
| II ST | saves, `TWNS.DAT`, `OUTn.DAT` play ranges | no (`.INT` + static ranges) | yes |
| II Apple | `CHAR2`, `TWN21`–`TWN210` (town Save), `DNGSAV` / `DNGCHK`, `OUTTn` (stored back on leaving a region, fog only; FORMAT.md 3.6) | `OUTTn` (`TF2` for towns) | the Memorial Set's unplayed disks give the pristine sha1s (`mutable_files`). `OUTT2` ships stored back (756 B, open as on the ST), so size says nothing; the 4am crack is a played copy (15 regions + the saves). A played copy warns (fog only: the Apple stores no fights there) or, if the player chooses, gets the start fog back (D2-052, decision 6). v2.1: no unplayed copy is known; its `OUTTn` / `TWN2n` pristine sha1s are v1.0's `[~]` (derived: its untouched regions and towns have them, its stored-back regions differ from them only in fog, `TF2` is identical; FORMAT.md section 4); `CHAR2` / `DNGSAV` / `DNGCHK` have none (not read). The A2CAN pair reports 10 regions + 6 towns played |
| III Amiga | `gamesave.dat` (`0x1faf0`) and the dungeon map saves `D/DNG<c>` / `D/DNP<c>` (loader `0x1f5d8` in mode 0, `0x1be22` / `0x1bdf8`); every other open is read-only, and the disk check's write into `D/PLN3` is never called `[C]` (7.9) | no | not needed: a played copy loses nothing (the reference ADF is played: saves dated 1988, every file read dated 03.09.1986) |
| III Apple | `CHAR3`, `TWN31`–`TWN317` (town Save, `BACKUP`), `DNGSAV` / `DNGCHK` / `PIC` (`DNGOUT3`) `[C]` (every `& STORE` of both sides) | no (`TF3` is read for towns) | v3.0's unplayed factory files give the pristine sha1s (`mutable_files`); the [cr] sides are played (`CHAR3`, `TWN31`, `TWN317`): the import is the same and the note says so |
| III DOS | only the dungeon map saves `DNP<c>` / `DNG<c>` (ovl4 `0x90a` / `0x93a`, root `0x7bff` in write mode) and `gamesave.dat` (armory stock); `DNGS.DAT` / `PLNS.DAT` are read in read mode, `WORLD.DAT` / `CREATURE.DAT` / `INITARMS.DAT` opened read-only by the start-up loader (root `0x7e08`) `[C]` | `DNGS.DAT`, `WORLD.DAT`, `PLNS.DAT`, `INITARMS.DAT`, `SCROLLS.DAT` | not needed: a played copy loses nothing |
| IV X68000 | saves go to disk 2 (`FORMAT.md`), which also holds `DATM`, `MOUT`, `IND*.PDT`; whether those are rewritten is `[?]` | yes | hash per file, not per image |

---

## 4. Importer flow

### 4.1 Where it runs

- **Client, first run** (and whenever a game has no import): an Import screen comes before the main menu. It lists the
  four games with their state and an "Add files or folder" action. Options gets an "Import games" entry to re-run it later.
- **CLI** `OpenPhantasie.Importer` console host (`openphantasie-import <path>... [--out DIR] [--list] [--check] [--steam]`,
  `openphantasie-import --refresh [--out DIR]`), the same library the Client calls. `--check` runs the
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
  in `libraryfolders.vdf`; GOG: the Galaxy games folder). The Memorial Set's layout:
  `reverse-engineering/dos/original-images/memorial-set-gog/README.md`; detection stays by content, never by a fixed
  path.

### 4.3 Output location

`ClientSettings.DataDirectory/import/` (today `LocalApplicationData/OpenPhantasie/`, renamed with the project),
never the install folder. As built:

| Path | Holds |
|---|---|
| `import.json` | `importer_version`; per game: `tables` (the version that built the tables), `art` (pack id → version), `versions` (per version: title, recipe, date, sources, key-file sha1s, played files, warnings, every output path with its sha1) |
| `GameData/<game>/…` | the (b) and (c) files in today's schema, plus (copied at each Client start by `GameDataLocator`) the shipped files the import doesn't build, so the folder is complete and loaders stay unchanged |
| `files/<repo-relative path>` | art roots and the raw files the audio reads, at the **same relative paths** the packs name today (`files/reverse-engineering/amiga/extracted/…`, `files/reverse-engineering/amiga/unpacked/PHANT/tamb.dat`), so pack `root` and `../unpacked/…` paths resolve unchanged. Renaming those paths to neutral ones is optional later work |

Loader changes made: `GameDataRepository.DefaultPathFor` / `StringTable` `extends` go through `GameDataLocator`
(the import's game folder when the game's tables are imported, else the shipped folder); `GraphicsPackCatalog` looks
for pack roots in the import's `files/` first. The import root is opt-in (only the Client sets it), so tests always read
the committed GameData.

A version's writes are buffered and committed only when its pipeline finishes; an exception fails that version alone.
The C64 (I / II / III) and Apple II III art steps run through `ImportContext.TryOptional` (s222): a missing or
malformed file (`FileNotFoundException`, `InvalidDataException`, a truncated read) warns "No C64 art: …" /
"No Apple II art: …", takes back that step's writes (no half pack) and keeps the version's tables.

### 4.4 Versioning and re-runs

- Each pipeline has a **recipe version**. When the app's recipe for a game is newer than `import.json`'s, the Client
  re-runs that game from the stored source paths if the key-file hashes still match, otherwise it asks for the files again.
  `ImportRunner.StaleVersions` lists the versions whose recorded `recipe` is older than the pipeline's;
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

- Main menu: the game stays listed but greyed, with "(NOT IMPORTED)". Saves of a missing game are kept. A game
  with pictures but no tables gets a note in the import report naming the copies that add its data (no supported
  version is such a case today; Amiga III was, before it gained its own tables, 7.9).
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
| III DOS CGA art | `extract_dos_p3_graphics` 217 (written with its C# port `P3CgaArt`) | 217 |
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
the "fresh repo" item, not of the importer itself. Built (8.2): such tests skip when the data is absent.

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
   unpacked in `reverse-engineering/dos/original-images/memorial-set-gog/`.
6. **Played DOS I copy:** the player is asked (user s205): RESTORE THE MAPS runs DOS I's own Reset rule
   (`GameSession.DosResetCells`, OV1 `0x4914`; `16-reset-game.md` section 3) on the 26×20 grid of every played region but
   OUT1 / OUT5, which gives a fresh copy's cells back (fog on, fixed fights armed; checked against the unplayed image);
   KEEP THEM AS PLAYED imports as it is and warns what was lost (fog already lifted, fixed fights spent; decision s158).
   Asked only when a region file (`OUT*.DAT`) is played and the version reads its grids from it
   (`ImportRunner.CanRestoreMaps`: DOS I 1987 and the Bonus Edition; Amiga I reads `maps.int`).
   **Played Apple II II** (D2-052, user s209): asked the same way (the question and report name the fog only:
   the Apple stores no fights in `OUTTn`). RESTORE THE MAPS puts the shipped start fog back per region (`apple2-import.json`
   `start_fog`: OUT1 / OUT2 open, the other 15 fogged in every cell; the ST's `MAPS.INT`, which the Memorial Set's unplayed
   Apple disk equals; derived and checked by `tools/scripts/derive_a2_p2_start_fog.py`), so a new game and RESET GAME
   start fogged; the 4am dump restored equals the Memorial import cell for cell. Not asked when the ST copy is in the same
   scan (its tables win; `RestorableMaps`). The Client asks after the scan (Y / N, rows, pad A / B; Esc imports nothing); the CLI
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
- **`src/OpenPhantasie.Importer.Cli`**: `openphantasie-import`, usage in 4.1 (`--refresh`: 4.4).
- **Client**: `ImportScreen` (first run when no game is available; Options → GENERAL → IMPORT GAMES): typed or dropped
  paths, Steam / GOG shortcuts, the import on a background task with a progress line, then the report (per version:
  imported / warnings / not imported and why), wrapped and scrolled, nothing cut. The main menu greys a game without
  data as "(NOT IMPORTED)".
- **`OpenPhantasie.Data.GameDataLocator`** + the switch: MSBuild property **`OpenPhantasieBuiltInGameData`** in
  `OpenPhantasie.Data.csproj` (default `true` here). `false` (the public repo) = no fallback to the committed GameData or
  the repo's art roots: a game without an import is unavailable. Checked on Xvfb with `false`: the first-run screen,
  importing DOS I + Amiga I + Kickstart through it, then I starts with the imported Amiga art; II and III (imported by
  the CLI into the same folder) start too; DOS-only I draws the CGA pack (town and screens; DOS-only III has its CGA pack too, 7.7). This
  proves the wiring and art lookup, **not** that the public repo runs: the shipped folder still held the full private
  GameData, which `GameDataLocator` copies into the import. Public-tree runs: 7.5, 7.9, 8.3.
- Scanning reads only what detection needs (sizes from directory entries, zips on disk read per entry): the 380 GB
  Steam library here lists in 3 s with 200 MB.
- **`tests/OpenPhantasie.Importer.Tests`**: golden tests per pipeline on the reference images (skipped when absent), the
  container / detection tests, the flow and locator tests.
- `graphics/index.json` of I gained `fallback_order: ["dos-cga"]`: with no Amiga pack (a DOS-only import) the CGA
  pack is drawn (decision 1; checked on Xvfb, above).

### 7.2 Per version

| Profile | Pipeline | Verified against the committed files (reference images) |
|---|---|---|
| `p1-dos-1987` | `P1DosPipeline` (+ `P1Dos/`) | 11 tables byte-identical (dungeons, elementals, item-descriptions, items, messages, monsters, names, overworld, scrolls, spells, towns); DOS CGA pack: `tiles.json` byte-identical, 44 PNG pixel-equal (tiles, town, and the defeat pictures of `extract_dos_defeat_pictures.py`). Towns from `TWNS.INT`. `spells.json` also checked with the names stripped from the shipped copy (the public overlay) |
| `p1-amiga-1.0` | `P1AmigaPipeline` | the whole pack root (256 PNG pixel-equal + 5 JSON byte-equal), `amiga-z6font-6.json` byte-equal, `asset-manifest.json` JSON-equal (shipped `map_tiles` + extracted `assets`), raw `tamb.dat` / `game` byte-equal. Map pictures read I's `overworld.json` (this run's, DOS I's or the Amiga's own). Tables (`P1AmigaTables`, no Python counterpart, 7.8): JSON-equal to the DOS GameData but for an allow-list of text and provenance paths (`P1AmigaTablesTests`); the same from a public-repo shipped folder; loads and starts a game |
| `p1-c64` | `P1C64Pipeline` (+ `P1C64/P1C64Tables`, `P1C64/P1C64Art`, `Containers/D64Volume` with `G64Image`) | tables (7.11; no Python counterpart) and, s218, the `c64` art pack (`P1C64ArtTests`: equal to the committed extract, pixel checksums, the shipped `c64.json`): with `c64-import.json`'s cells, JSON-equal to the DOS GameData but for an allow-list of text, provenance and the C64's shorter files (`P1C64TablesTests`); the same from a public-repo shipped folder; loads and starts a game. Containers: every file of both v1.4 `.g64`s equals `extract_c64_d64.py`'s (`D64VolumeTests`); every held C64 I image decodes all 683 sectors; recipe 5 (s223): the C64 end-screen animations (`ending_scene.c64`, `c64_defeat`, the face / hand / door / demon-face sprite slots; `c64/phantasie1/GRAPHICS.md` 9.4); recipe 6 (s224): the closing page after the grant (`ending_scene.c64.closing`, P.END's lines as C64-only `c64.endgame.closing_*` strings), the party wipe's UNDEAD name row (`c64_defeat.undead_name_row`) |
| `p1-apple2` | `P1Apple2Pipeline` (+ `P1Apple2/P1Apple2Tables`, `Containers/RdosVolume`, `WozImage`) | tables + art (7.14; no Python counterpart): with `apple2-import.json`'s cells, JSON-equal to the DOS GameData but for an allow-list of text, provenance, the Apple's message slots and its shorter level files (`P1Apple2TablesTests`); every shipped cell holds DOS's value and the raw build's; the `.woz` pair imports byte-equal to the `.dsk` pair; the same from a public-repo shipped folder; loads and starts a game; every event menu a level's grid holds has its choices; s223 `P1Apple2ArtTests`: the art equals the committed extract (the `.woz` pair gives the same), I's `SHAPES` is II's but for shapes 45 / 46 / 48, the pack equals the shipped `apple2.json`, a side without `SHAPES` warns and keeps the tables; s224 `P1Apple2ScenesTests`: the replayed CEMETARY / FLASH? pages equal the ROM run's (sha1 at eight statements), page 2 holds `SHAPES` / FLASH? where the game keeps them, the import writes the scene files and lines |
| `kickstart-1.3` | `KickstartFontPipeline` | topaz 8: I's `amiga-topaz-8.json` + preview PNG and III's copy, byte-equal. Its own pipeline so either Amiga game gets it |
| `p2-st-1.0` | `P2StPipeline` (+ `P2St/`) | 15 GameData files byte-equal (incl. `graphics/atari-st.json`), 262 PNG pixel-equal + 4 JSON byte-equal, raw `START.PRG` / `PHANT.PRG` byte-equal; the TOSEC `.st` pair gives identical output. Needs only I's shipped numbers (no names) |
| `p2-apple2` | `P2Apple2Pipeline` (+ `P2Apple2/`, `Containers/RdosVolume`) | the 4am crack's two RDOS `.dsk` sides (key files from `extracted/key-files.json`; or the original `.woz` pair (`Containers/WozImage`): every output byte-equal to the `.dsk` pair's; the `.woz` disks are the same played copy (`WozImageTests`); the 4am pair written out as 16-sector 6-and-2 `.woz`, as a Memorial Set disk would be, imports byte-equal too). Art: 545 PNG pixel-equal, `extraction-index.json` and `graphics/apple2.json` byte-equal (the pack's constants live in `A2Manifest`, not read from the shipped file). Raw tables: the 9 `extracted/*.json` of `extract_a2_p2_tables.py` byte-equal. GameData (no Python counterpart, see 7.5): loads through `GameDataRepository.Load` and a new game starts; every number equals the ST build (the ST cells of `apple2-import.json` applied, OUT1's unused feature record 14 included); the shipped cells equal the ST GameData; the scrolls' compass words, turned, equal the ST's. Strings: 217 ids verbatim + 95 in the Apple's own wording from the copy, the rest from the shipped fallbacks (7.6: the common shared / I file + II's 3 ids); under the public "..." mask an Apple-only import leaves no id of II's whole chain on "..." |
| `p2-apple2-2.1` | `P2Apple2V21Pipeline` (the same `P2Apple2/` code, `P2Apple2PipelineBase`) | the A2CAN RDOS 3.3 pair. Restored (D2-052), every GameData table, art file and resolved string line equals the Memorial Set's v1.0 import but for the version's name (`_source`, `game.json` `profile`, notes) and each region's file length (`P2Apple2V21Tests`); loads and starts a game fogged. Strings: every `p2-apple2` alternative has a `p2-apple2-2.1` twin (326; 155 `OUTDOORS2` references moved by Applesoft line; the test compares the resolved lines of all three tables, shared, I and II) |
| `p3-dos-1988` | `P3DosPipeline` (+ `P3Dos/`) | 10 tables JSON-equal, 9 byte-identical (rule-tables JSON-equal only: hand-added blocks). WizardWorks 1991 detected by the same profile, identical output. III's spells take only `category` / `effect_summary` from I's shipped spells. The DOS CGA pack (`P3CgaArt`), 512 PNG pixel-equal to `extract_dos_p3_graphics.py`'s (7.7) |
| `p3-apple2` | `P3Apple2Pipeline` (+ `P3Apple2/P3Apple2Tables`) | tables only (7.10): the C# extract equals `extract_a2_p3_tables.py`'s nine files (`P3Apple2TablesTests`); with `apple2-import.json`'s cells, JSON-equal to the DOS GameData but for provenance and text (`P3Apple2ImportTests`); the same from a public-repo shipped folder; loads and starts a game |
| `p3-c64`, `p3-c64-1.2` | `P3C64Pipeline` / `P3C64V12Pipeline` (+ `P3C64/P3C64Tables`, `P3C64StarterGuild`) | tables, the starter party, (recipe 2, s220) the `c64` art pack (`P3C64Art`, `P3C64ArtManifest`) and (recipe 3, s221) the C64's own wording, 119 ids per build (`P3StringFallbackTests`) (7.13; tables: no Python counterpart): with `c64-import.json`'s cells, JSON-equal to the DOS GameData but for provenance and text (`P3C64ImportTests`); v1.2 gives v1.0's tables; the same from a public-repo shipped folder; loads, starts a game with the disk's six in the Guild, leaves town |
| `p3-amiga-1.0` | `P3AmigaPipeline` | 579 PNG pixel-equal, 2 JSON byte-equal, raw `D/m0`, `D/m1`, `PhantasieIII` byte-equal. Without DOS III only `previews/scandor-dos.png` (unused by the pack) is left out. Tables (`P3AmigaTables`, no Python counterpart, 7.9): with `amiga-import.json`'s cells, JSON-equal to the DOS GameData but for provenance (`P3AmigaImportTests`); the same from a public-repo shipped folder; loads and starts a game |
| `p4-x68000-1.02` | `P4X68kPipeline` (+ `P4X68k/`) | 16 files JSON- and byte-equal (monsters, items, spells, names, overworld, dungeons, scrolls, towns, elementals, rule-tables, `strings/ja.json`, `graphics/x68000-panels.json`, the four `x68k-p4-*` fonts); 382 PNG pixel-equal (every PNG has a generator: 55 PKH, 273 `.pix`, 54 fonts / item icons); raw `P4.X`, `P4.SND`, `VDATA` byte-equal. English by id only (decision 3): `en.json[id]`, else the Japanese from the copy; `translation/en.json` is never read. A test with `en.json` cut to the differing ids and `rule-tables.json` without the copy's fields gives the same tables. `x68k_p4_anim.py` not ported (its numbers live in the shipped `x68000.json`); the panel call sites found by objdump are a fixed table (P4.X is pinned by sha1) |

### 7.3 Findings of the port (committed files vs scripts)

- `build_p2_gamedata.py` and `build_p3_gamedata.py` emit the blocks once added by hand: II `rules.json`
  (`elementals`, `arithmetic: motorola_ffp`, `divisor_ffp`) / `travel-time.json` (D2-042 `wilderness_inn_dos`), III
  `creation-tables` (`lifespan_bands`, `_note`), `rules` (`elementals`, `dungeon_save_per_dungeon`,
  `drop_gold_share`), `towns` (`_layout` / `_notes`), `rule-tables` (`creation_age`, `armory_restock`,
  `town_entry_payout`, `transfer`). `divisor_ffp` is computed from the divisor and checked to occur in `PHANT.PRG`;
  the restock pool is checked against the Apple III disk 1A; `--check` passes with a zero diff. The importer leaves
  those class (a) files shipped and takes the hand-added parts of (c) files (III towns `_layout` / `_notes`,
  rule-tables blocks) from the shipped copy.
- II `dungeons.json` lacks the `option_cells` that `extract_dungeon_events.message()` adds; the port matches
  the committed file.
- I `dungeons.json` keeps three hand-written top-level fields (`_note`, `_grid_byte_length`, `_grid_note`) no script
  writes; the port writes them as constants. `_note` takes `extract_maps.py`'s wording ("raw feature bytes; [C] grid =
  first 1254 B = 33x38, …") in the shipped file and in `MapTables.DungeonNote` (user s159); the other two stay
  hand-written.
- PNG counts: see 2.6.
- `x68000-panels.json` gained `_acgamend` / `_acgamend.1`, the training hall title and two ink fields
  (ported); II ST's alert boxes are the disks' `form_alert` strings: rebuilt from `PHANT.RSC` / `OUT.RSC` /
  `PHANT.PRG` instead of taken from the shipped pack.
- II scroll lookup (found while mapping the Apple): II's ST `scrolls.json` has no `item_records`, so the Client's
  fallback `Scrolls.Id == item - 120` shows record `item - 120` of the 14 (SCROLL B, item 124, would show id 4 = SCROLL D's
  text). Not changed here: a Client / data question.
- The ST and Apple II OUT1 differ in unused feature record 14 at (25, 0) (ST dest_y 32, Apple 0); no cell uses it (record 13 on the same cell is found first). An `apple2-import.json` cell (D2-049, keyed on the record index) gives it the ST's 32.

### 7.5 What an Apple-only II import gives

II's GameData in the ST build's shape, from the Apple's files alone (`A2GameData`); with the ST copy imported too (same
run or earlier) the ST's tables win and the Apple adds only `apple2.json` + its art (a warning says so;
`import.json` `tables` stays the ST's; the runner records `tables` only for a profile that wrote GameData tables).
v1.0 (`p2-apple2`) and v2.1 (`p2-apple2-2.1`) build the same tables (same data files; `game.json` `profile` and
the `_source` lines name the version). With both (same run or earlier), v2.1's win: v1.0 runs after it and then adds
only its art pack, and only v2.1 is asked about restoring its maps.

- **From the Apple:** monsters (names, numbers; `sprite_width` = the idle picture's width, `sprite_height` = the
  Apple's rank height YS%), items 1-180 and value factors, names (classes, races incl. LIZARD MAN, status OKAY),
  the ten towns (TF2), creation tables (TINFO; one-byte skill cells), the 17 regions (grid, features, fog as on the
  disk), dungeons (level bytes, 2048 B as the ST's; messages from the Apple's slots, menus written in the text become
  options: event record byte 1 or the 1- / 2- markers, else digits), scrolls (SCROLLL0-20 as records 0-20 with
  `item_records`, 17 empty).
- **Decided entries applied as for the ST:** D2-009, D2-015, D2-021, D2-022 / 023 (from I's shipped tables, the rules).
- **The ST's numbers whatever the source (D2-046..049, user s159):** the shipped class (a) file
  `phantasie2/apple2-import.json` lists the 37 cells where the Apple differs (5 monster fields, DEMONIAC's factor and
  the SCROLL D / E stock, 2 skill cells, 9 map tiles, OUT28's 8 entrances, 2 dungeon bytes, OUT1 feature record 14) as paths into the built
  files with the Apple and ST values (`A2StValues.ApplyCells`). A cell holding the Apple value takes the ST's (a fogged
  tile keeps its fog); one holding neither is kept and warned. Pure numbers, so they ship (the ST's own `monsters`,
  `towns`, `overworld`, `dungeons` are dropped from the public repo and can't be read instead).
- **Text (D2-050):** the Apple's names, scrolls and dungeon messages; the scrolls' compass words turned to the ST
  orientation (`scroll_compass` in the same file, all at once, at word starts, boxed lines keep their width). Spell
  names: the manual's list (CB p.12) from `apple2-import.json` `spell_names`; the two divine spells, unnamed in the
  manual and on the Apple disks, take the ST's DIVINE II (#57) / DIVINE I (#58) (user s170, `from_st`). String tables: see 7.6.
- **Missing on the Apple:** `messages.json` (the Apple keeps dungeon text in the levels; `game.json` has no
  `message_areas`), elementals (not on the Apple disks: the remaster's three, I's / the ST's numbers, names
  FIRE / WATER / EARTH ELEMENTAL). Played copy: 15 of 17 OUTT files saved back (fog lifted), warned; the player may
  restore the start fog (D2-052, decision 6 above).
- Text cells: the Apple's CR-ended string from slot `arg` is put in the ST page form (its screen lines as pages,
  `arg + 10` per extra line) so Core's `LocationTextPages` shows it.
- Seen in the Client on Xvfb from a public-tree import (the export built with no built-in GameData, the public
  CLI importing the Apple pair + DOS III into a throwaway data folder): the Filmon ending (`--p2-victory`: the Apple's
  40-column lines in the remaster's box, scrolled, nothing cut), the Mystic (score line from the fallback: "SCORE: 0.
  YOU ARE NOVICE ADVENTURERS"), the town Option cast window. The 18-character MONSTER EVALUATION seen whole in the wilderness cast window (`parity/shots/s169-p2-apple2-field-cast-monster-evaluation.png`);
  it never shows in a fight's list (spell #52 is not a combat spell; the Apple's fights take 1-51, `OUTDOORS2` /
  `DUNGON2` line 1310). The ending's "THESE {0} BRAVE ADVEN-" line takes the party's member count, dead included (`WIN`
  line 3116; a summoned elemental is left out: the Apple has none).

### 7.4 Left to do

- **II Apple II v2.1:** not run in the Client yet (its tables equal v1.0's, so nothing new to see), and no unplayed
  v2.1 copy is known to confirm its derived pristine sha1s (3.3).
- **Not seen in the Client on Xvfb `[~]`:** Amiga-only I (7.8).
- **Open for the user:** Apple III's Scandor and plane grids (7.10).
- **Other `[~]` / `[?]`:** IV disk 2 rewrites (3.3); the updater on Windows and macOS (8.4).
- **I Bonus Edition** (the Memorial Set's I; `dos/bonus-edition.md`: `OV1.OVR` / `OV2.OVR` differ only in the
  hidden-save flag and the manual page lines; `IBMCOVER`, the WizardWorks cover, is not read): an import made before
  s168 recorded only the versions it imported, so the recipe re-run (4.4) does not find I in it: the player imports
  the Memorial Set again (II / III are re-imported unchanged).
- **Review items the export lists** (8.3): 2 files, both accepted: the shared `en.json`'s option texts naming a game
  line, and IV's Japanese panel labels (`x68k_p4_panels.py`); long names in notes.
- Built: public overlays, per-id string provenance, public-repo loading (`guild.json` optional; I's
  `asset-manifest.json` loads without `assets`), tests that need the original data skip without it (8.2). Memorial
  Set II disks (DOS 3.3 sector order): `RdosVolume.TryOpen` reads both orders and keeps the longer catalog (catalog
  sector 0 is the same in both). Imports without I's tables are covered by the shipped fallbacks (7.6).

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
- **Phantasie II's Apple II** (`p2-apple2`, D2-050): the script adds its alternatives after the rest, so no other
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
  (`{"tables": {table: {id: text}}}`; the shared / I lines from the common `phantasie1/strings/fallback.en.json`,
  II's own three from `phantasie2/strings/fallback.en.json`, next bullet): `ImportedStrings.WriteFallback` merges them
  and writes `strings/en.fallback.<profile>.json` per table, and `GameDataLocator.ImportedStringFiles` lays fallback
  files under every other imported file, so DOS I's or the ST's own line still wins.
- **Imports without I's tables:** a version gives only the lines its files hold, so without fallbacks a game's chain
  (shared → I → its own table) shows "..." for the rest: DOS-only III (395 ids), ST-only II and Apple-only II (175 ids
  each, the Mystic's score line among them). Three shipped files fill them (plus III's Apple-only file, 7.10):
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

### 7.9 What an Amiga-only III import gives (s210)

`P3AmigaTables` (`Pipelines/P3Amiga/`, layouts and code sites in `reverse-engineering/amiga/phantasie3/FORMAT.md`
"Game tables for a standalone import") reads every table DOS III's import takes from its Amiga counterpart, in
`P3Extract`'s shapes, and the unchanged `P3Build` writes the ten files `P3DosPipeline` writes (monsters, items,
spells, names, overworld, dungeons, scrolls, towns, elementals, rule-tables). `P3AmigaPipeline` (recipe 2) writes them
before the art steps (the portraits read `core_race`).

- **From the Amiga:** `PhantasieIII`'s DATA tables (names, item numbers, HP / magic / skill / spell / training rows,
  the encounter tables, dungeon letters and entrances, the place test), `monsinfo.dat`, `inititem.dat`,
  `D/dng<a..k>.dat`, `Graphics/phantasy.set` (Scandor), `D/pln1` / `pln2` / `pln4`, `D/s1..s20`.
- **DOS's values (user s209: DOS for all eight Amiga ≠ DOS differences):** the shipped class (a)
  `phantasie3/amiga-import.json` lists 156 cells (HP dice rows, training gold L16..20, Parry Wizard L15, the terrain
  offsets past the table, Scandor's 54 cells and Pendragon's place tile, the place tiles' terrain, Scroll III's pages,
  the text: treasure articles, Flame Arrow, spell #53, dungeon 2, the inns, terrain names), applied by `CellPatches`.
  With them every file equals the DOS build but for provenance, so III plays the same whichever copy was imported.
- **`hp_dice`:** the Amiga holds 21 levels per class (`0x221dc`, level clamped at 21), rising past L15; the loader
  wants DOS's 22. The cell replaces each row with DOS's (L16..22 = L15's value), so the 22nd level is DOS's. Also
  DOS's: the XP / gold curves' index 0 and the terrain offsets' entry 0 (no Amiga counterpart; none is read).
- **Strings:** the Amiga gives most of III's lines itself (`en.sources.json` holds `p3-amiga-1.0` alternatives for
  94 of the 98 `core.p3.*` ids DOS gives); the six only DOS III holds and those neither holds take the shipped
  fallbacks (the common shared / I file + III's, `en.fallback.p3-amiga-1.0.json`), as a DOS-only III. User s212: two
  lines the Amiga words its own way are the Amiga's, as own-wording alternatives (`tools/scripts/p3_amiga_own_wording.json`
  → `en.own.p3-amiga-1.0.json`, III only, while III's tables are the Amiga's; recipe 3): `mystic.heroes_of_scandor` =
  the Amiga's line at `PhantasieIII` +74800 (22 B, next to the three other `heroes_of_*` lines it already takes) and
  `greetings` = its line at +108174 (15 B); both left III's fallback file. The Amiga has no counterpart for the other six (it always trades; no trap, transport or rank
  line; Scroll IV's caption is part of its picture `D/s6`, "[original text]", so the caption id is drawn only by
  the DOS CGA pack and never shows in an Amiga-only import).
- **With DOS III** (same run: DOS runs first; or an earlier / later import): DOS III's tables win; the Amiga writes no
  table, no fallback, and warns. Amiga, then DOS, then an Amiga re-run leaves DOS's tables.
- **Played copy `[C]` (static):** play rewrites no file the import reads, so only the three key files are pinned
  and no `mutable_files` are needed. Every dos.library Open goes through the wrapper at `0x21418`; the only write-mode
  opens reached are the save writer `0x1faf0` (`gamesave.dat`, `0x1fb16`) and the disk loader `0x1f5d8` in mode 0 from
  the dungeon's save (`0x1bdf8` `D/DNP<c>`, `0x1be22` `D/DNG<c>`). `D/DNG<c>.DAT` (= `D/dng<c>.dat`), `D/PLN<n>`,
  `D/S<n>`, `monsinfo.dat`, `inititem.dat`, `Phantasy.set`, the `.cmp` / `.csh` pictures and `D/M0` / `D/M1` are opened
  read-only. Two write paths have no caller: the disk check `0x1f87e` in mode 3 (a word into `D/PLN3`, which the import
  does not read; callers pass 0 or 1) and `0x20a5e` (`D/SXX`, `D/DNGX.DAT`, `D/WX.cmp`, `D/PLNX`, `D/MX`), called only
  from `0x20bb4`, which nothing calls `[~]` (direct, absolute and PC-relative references searched; the start-up's two
  `jsr (a0)` not traced). `Guild.dat` is a Phantasie I disk's roster, read for the transfer. The reference ADF agrees:
  it is played (`gamesave.dat`, the 22 `D/DNG<c>` / `D/DNP<c>` dated 12-14.01.1988), while every file the import reads
  keeps its 03.09.1986 date.
- Recipe 2 (an older Amiga III import re-runs and gains the tables, 4.4); recipe 3 adds the two own-wording lines.
  Seen in the Client on Xvfb from a public-tree import of the ADF alone (`parity/s212-p3-amiga-only-live.md`): a new game in Pendragon, the Guild, the
  Armory, the Bank, Trade, the Mystic, leaving town, the overworld (DOS's Pendragon tile and terrain names), fights by
  the Amiga's screens and DOS's, a trap, Scroll IV's picture.

### 7.10 What an Apple-only III import gives (s212)

`P3Apple2Tables` (`Pipelines/P3Apple2/`, the C# port of `tools/scripts/extract_a2_p3_tables.py`; layouts in
`reverse-engineering/apple2/phantasie3/FORMAT.md`) reads III's tables from the two sides of disk 1 (the v3.0 `.woz`
pair or the [cr] `.dsk` sides) in `P3Extract`'s shapes; the unchanged `P3Build` writes the ten files `P3DosPipeline`
writes. `P3Apple2Pipeline` (recipe 3; 1 had no fallback wording, 2 not the own wording) runs after `p3-dos-1988` and `p3-amiga-1.0`.

- **From the Apple:** `MN30`–`MN39` (monster records, text), `ITEM` + `TINFO` (items, HP / magic / skill / spell
  tables), `TF3` (armory stock, price index, town names), `OT1`–`OT43` (Scandor in 25 sections, the plane maps, the
  places), `DNG1`–`DNG10` (DOS's record layout; `DNG10` serves dungeons 1 and 8), `SC1`–`SC20` (scroll pages), the
  programs' DATA lines (names), `INFO` (terrain monster offsets).
- **DOS's values** (the rule of II's Apple and III's Amiga imports): the shipped class (a) `phantasie3/apple2-import.json`
  holds 653 cells made by `diff_a2_p3_vs_dos.py --built … --cells` (FORMAT.md section 4: monster numbers and bytes
  27 / 33, item 69's value, 44 stock cells and Tirith's index, dungeon event bytes, scroll pages, the Scandor grid by
  rows and the three plane maps, places, the terrain table, HP L16–22, the skill cells of D3-032, the tables the
  Apple computes, spell and missing names), applied by `CellPatches`. With them every file equals the DOS build but
  for provenance and the Apple's own text (names, dungeon messages laid out in 38-column rows, scroll pages); the
  dungeon records equal DOS's to 0x593.
- **Open (for the user):** the Scandor and plane grids are not derivable from the Apple's bytes (terrain × 10 + level,
  not DOS's tile ids), so the cells carry DOS's whole grids (75 Scandor rows, 3 plane maps) into the shipped (public)
  file; the alternative is the Apple's own maps with an Apple tile → terrain table (no DOS art fits them).
- **Strings:** `en.sources.json` holds `p3-apple2` alternatives for 17 ids (status / class words found verbatim in
  v3.0's programs; v1.0's programs are laid out differently, so a [cr] import resolves none). The 218 ids III
  shows that neither the Apple nor the common / III fallbacks give (206 DOS III or Amiga lines, 10 of the v3.0 words a
  [cr] copy lacks, and `greetings` / `mystic.heroes_of_scandor`, which left III's file for the Amiga's own lines) take
  the remaster's wording from `phantasie3/strings/fallback.p3-apple2.en.json` (shared 78, I 5, III 135; recipe 2).
  `ImportedStrings.WriteFallback` merges a game's `strings/fallback.<profile>.en.json`, when shipped, after its
  `fallback.en.json`, so only this profile gets it. Wording as in 7.6 (88 game terms, labels and status words keep the
  table's text; 130 lines reworded, format items, line count and the aligned header's length kept). Tests
  (`P3StringFallbackTests`): v3.0 and [cr] Apple-only imports leave no id III shows on "...".
- **The Apple's own wording (s216, D3-077, recipe 3):** `tools/scripts/p3_apple2_own_wording.json` maps 103 of those
  218 ids (shared 6, III's table 97) and 25 shared / I ids an Apple-only III took from the common fallback (128 in all) to references into v3.0's TOWN3, OUTDOORS3, DUNGON3, CEMETARY3, WIN and DNGOUT3,
  added to `en.sources.json` as `p3-apple2` alternatives marked `"wording": "own"` (`build_string_sources.py
  --show-apple3` prints them). A v3.0 import whose III tables are the Apple's writes them, from every table, to
  `phantasie3/strings/en.own.p3-apple2.json` (`ImportedStrings.Write(ownWordingGame: "phantasie3")`, as II's Apple),
  which `ImportedStringFiles` puts on top of III's chain only while `import.json` names `p3-apple2` as III's tables;
  the fallback file stays under it. The hi-res window texts are decoded through the Apple's font (FORMAT.md section 2,
  section 4 Strings): letters are references, spaces / punctuation / format items our glue. What is mapped and what
  still falls back (115 ids on v3.0: 74 terms, 41 lines with no counterpart or other answer keys): DIFFERENCES-III
  D3-077. A [cr] copy writes none (its programs differ). Tests (`P3StringFallbackTests`): the own file holds exactly
  the sources' own-wording ids, only in III's folder, decoded; sample lines; none for [cr] or beside DOS III.
- **With DOS III or the Amiga** (same run or earlier): their tables win; the Apple writes none and warns (the Amiga's
  `OwnsTables` also takes over the Apple's).
- **Played copy:** none of the files read is rewritten by play (3.3); a [cr] copy imports the same.
- Played in the Client on Xvfb from a player's build (s214, `parity/s214-p3-apple2-live.md`): new game, Guild,
  town, overworld, a dungeon, fights, save / load. Wording review: `p3-apple2-fallback-review.md`.

### 7.11 What a C64-only I import gives (s216)

`P1C64Tables` (`Pipelines/P1C64/`, layouts in `reverse-engineering/c64/phantasie1/FORMAT.md` section 3) writes the
eleven files `P1DosPipeline` writes from the two v1.4 disk sides. As for the Amiga (7.8), each C64 table is put into
DOS layout and goes through DOS's builders. `P1C64Pipeline` (recipe 1) runs after `p1-dos-1987`,
`p1-dos-bonus-1991` and `p1-amiga-1.0`.

- **Containers:** `D64Volume` reads a `.d64` (directory chain from 18/1, sector chains; a REL file as its data
  blocks, records by the entry's length); `G64Image` decodes C64PP's `.g64` (GCR sync / header / data blocks, both
  checksums) to the 683 sectors first. The scanner opens both (a `.g64` keeps its own sha1). A PRG keeps its load
  address, which the pipeline strips.
- **Detection:** side 1 `TOWNR`, `TINFO`, `MN`, `ITEM`, `SCROLLS`; side 2 `DUNGEON`, `DNG 1`, `PMES 7`, `PATTERN`
  (v1.4's sha1s). Mutable files: v1.4's slot files (OUT2–4, 6–16, 18, 19; TWN 3–11, 99); OUT1, OUT5, TWN 1 (and TWN 2
  on plain v1.4) are play-written on every copy and not listed, so plain v1.4 reads as fresh. A played copy is
  reported but imports as a fresh one (`PlayedCopySafe`, no map-restore question): nothing it rewrote is read.
- **From the C64:** `MN` (monsters), `ITEM` + `TINFO` 89 + id (item names, values; id 0's name from `TOWNR`),
  `TOWNR`'s DATA (classes, races), side 2's `PATTERN` (the new-game template, never written by play, one sha1 on every
  copy: the towns, regions 2–19 and the elementals of its `CHAR2`; FORMAT.md 2.1), side 1's `OUT1` (not in `PATTERN`;
  play-written but the same bytes on every copy), `DNG n` + `PMES n` (DOS's layouts, plain), `SCROLLS` (the rows
  inside each page's frame). The held en copy (played, an odd `OUT6`) imports byte-equal to the C64PP pair.
- **DOS's numbers:** the shipped class (a) `phantasie1/c64-import.json` (`tools/scripts/build_c64_p1_import.py`):
  112 `dos_cells` (BLACK KNIGHT's group size; OUT1's row 0, (18, 14) and its feature order; the OUT9 / OUT18 fixed
  fights; OUT13 / OUT15 cells; DOS's leftover bytes in unused feature records; every differing DNG byte, D-086's
  three reachable cells and the side tables; the elementals' MP ceiling, creation number (23–25, DOS's) and race),
  DOS's monster sprite sizes (the C64 has none; the encounter build reads the width), and D-084's bleeb lines
  as byte patches of `PMES 7` (only the differing words). With them every number equals
  the DOS build; the dungeon bytes equal DOS's as far as both files go.
- **The C64's text:** item spellings (`SH SWORD`, `HALBRED`, `HEAL POT1..9`, `SROLLL 19`, `--WAND--`, `SAPHIRE`),
  messages (PMES 1, 3, 5, 9 differ), scrolls (mixed case, the C64's own line breaks; scroll 5 NORTH, D-085), the
  overworld pages. The fire elemental's 12-byte name reads `FIRE ELE` (backspaces applied): fixed to `FIRE ELEM`
  by `name_fixes`, as D-083 fixed the Amiga's. **Spell names:** none on the C64; the manual's spell table (slots
  0–53) and `DIVINE I` for the divine spell (remaster wording, as II's Apple import, D2-050), in `c64-import.json`.
- **Strings:** `p1-c64` alternatives for 259 ids found verbatim (I 180, shared 68, II 10, III 1) in `TOWNR`, `OUTDOORS`, `DUNGEON`, `CEMETARY`,
  `P.END` (`build_string_sources.py`), and Zeus's speech in the C64's own wording (`p1_c64_own_wording.json`, through
  the importer's `P.END:petscii` key) → `en.own.p1-c64.json`. Lines only DOS I holds take the common fallback. Left on
  "..." in the public repo: 22 ids of the shared table's I field and map screens (menu words, box titles, sheet words
  such as `FIRE`, ` AT RANK`; `p1field.*` / `p1map.*`) not found verbatim in the C64's programs `[~]`.
- **With DOS I or the Amiga** (same run or earlier): their tables win; the C64 writes none and warns. The Amiga's
  `OwnsTables` takes over the C64's (an Amiga import after a C64 one replaces its tables).
- Played in the Client on Xvfb (s216, a dev build with the import in a throwaway home): new game, Guild (new member,
  add), Pelnor, outdoors, a step, with the dev build's built-in Amiga art. Again with a no-built-in-data build
  (`-p:OpenPhantasieBuiltInGameData=false`, as a release): it plays without pictures (town menus over black, the map as
  coloured cells, an empty portrait box): a player with only the C64 copy gets no I art.
- **Art (s218):** the `c64` pack (`P1C64Art`, `P1C64ArtManifest`, recipe 2; `reverse-engineering/c64/phantasie1/GRAPHICS.md`,
  generic decoders `Formats/C64Gfx.cs`: palette, characters, sprites, multicolour bitmap, the `MX` sprite-band record
  for II's `MX` too): the 80 monsters (with an attacking frame each; s219, recipe 3: the 30 several-sprite ones by II's live sprite-0 rule `[~]`), hero sprites by race 0–14, the title, the town's six
  buildings as `town.backdrop`, the overworld from `FONT`'s characters as `UTIL-3.O` prints them (a character pair by
  column parity, `odd_column_tile`; the town / inn / dungeon sprites as `overlays` and the party at their size,
  `party_offset`; 19 × 18 cells around the party), the dungeon map's characters (`dungeon_party_over`). Colodore colours.
  Written whichever copy's tables win; `index.json` lists it after the Amiga in `default_order`, so a C64-only (or C64 +
  DOS) player gets it. Played on Xvfb with a player's build (`parity/s218-p1-c64-art-live.md`); the default combat
  screen's rank box now puts a rank's line below a picture too wide to leave room beside it (PYTHON, 144 px). **Falls back** (the
  Client's own drawing): UNDEAD's portrait (and `defeat.figure.15`), `ui.decor` / `decor.*`, combat effects. Since s221
  (recipe 4) the ending (`ending_scene`: Zeus, the clouds, the lightning hand) and the party wipe (DOS's pages with
  `CEMETARY`'s temple, face, ghost and the HSPRIT figures) are drawn (`parity/s221-c64-end-screens-live.md`; not
  modelled `[~]`: Zeus's wipe, the rising door, the fate colours). The item / feature pictures (`items/`) are drawn beside the find / trap / stairs / lock / exit texts since s221 (`event.*` slots, `find_pictures`; `parity/s221-c64-item-pictures-live.md`).
- **Left:** v1.0 / v1.3 profiles (other program files; v1.0's `ITEM` / `SCROLLS` / DNG8 byte); the German C64 (German
  text); the C64's own wording for its town / Guild lines; the art's `[?]` (GRAPHICS.md 8: `DOOR`'s colour (never shown), what sets `P(168)`, the hero colour rule, the moving water).

### 7.12 What a C64-only II import gives (s218)

`P2C64Tables` (`Pipelines/P2C64/`, layouts in `reverse-engineering/c64/phantasie2/FORMAT.md` sections 2–3, the plan
and its build in section 7) writes every table `P2StPipeline` writes, from the two v1.2 disk sides. As the Apple
import (7.5): **the C64's text, the ST's numbers** (DIFFERENCES-II D2-054…D2-056). Each C64 table is put into the
shape of the ST's extract and goes through the ST's builders (`P2GameDataBuilder`), so the GameData keeps the ST
schema. `P2C64Pipeline` (recipe 2, s219: + the art pack; 3, s221: item / feature and end pictures; 4, s223: `c64_defeat` and `WIN`'s row, presented member and pages) runs after `p2-st-1.0`, `p2-apple2` and `p2-apple2-2.1`.

- **Containers:** `D64Volume` / `G64Image` as I's (7.11). Each side is read on its own (`P2C64Source`): `MN` and `B`
  differ between the sides.
- **Detection:** side 1 `TOWN2`, `OUTDOORS2`, `TINFO`, `MN`, `ITEM`, `SCROLLS`; side 2 `DUNGEON2`, `PATTERN2`,
  `DNG 7`, `MN` (v1.2's sha1s; never `B`: the held `.d64` side A's boot program differs). Mutable files: `CHAR22`,
  `TWN21`–`TWN210` and the 17 `OUTT` files at their mastered sha1s (alt1's). The C64PP never_used pair reports
  `CHAR22`, `TWN21`, `OUTT2`; the held `.d64` pair (played v1.2) more; both import byte-equal to a fresh copy
  (`PlayedCopySafe`, no map-restore question). **v1.0** (user s217: v1.2 only) is the recognised profile
  `p2-c64-1.0` (its own `TOWN2` / `OUTDOORS2` / `DUNGEON2` / `PATTERN2` sha1s), not "another build".
- **From the C64:** side 1's `MN` (monsters; fields 13 / 14 / 24 are the C64's own and not kept, no `raw_hex`),
  `ITEM` (180 names, the empty line 46 skipped) + `TINFO` 89 + id (values; id 0 `TOWN2`'s NOTHING, its value I's),
  `TOWN2`'s DATA (classes, 16 races, status DEAD / OKAY / SLEEP), `TINFO` (creation tables, I's / the Apple's layout;
  the ST builder's `Creation` takes the copy's manual cells, `manualCopy`), side 2's `PATTERN2` (the new-game template:
  the ten towns, the 17 regions' 750 bytes with pages 0–3 in screen codes, the elementals of its `CHAR22`), `DNG n` +
  `PMES n` (the ST's layouts, plain; a real `messages.json`), `SCROLLS` (the rows inside each page's frame; the ST's 14
  lettered scrolls by the ST's item → record map, the C64's page = record + 1, from record 16 + 2).
- **The ST's numbers:** the shipped class (a) `phantasie2/c64-import.json` (`tools/scripts/build_c64_p2_import.py`,
  which asserts the count per file against FORMAT.md section 3): 104 `st_cells` (37 monsters: D2-047's five, CLOUD
  GIANT, 18 group sizes, 11 `byte_34`; 2 skill cells; 5 town stock cells; 28 map cells / features; 23 dungeon bytes
  including DNG7's exits, user s217; 9 elemental fields), the ST's 80 monster sprite sizes (the group fit reads the
  height), the manual's spell names (as `apple2-import.json`'s). With them every number equals the ST build
  (`P2C64TablesTests.EveryNumber_EqualsTheStBuild`: the ten towns field for field, each level's bytes 0–1739, the menus'
  option counts).
- **The C64's text:** monster names (CREEPING MOLD, CONSTR., WYWERN, no hyphens), SH SWORD, OKAY, FIRE / WATER /
  EARTH ELEM, the PMES wording (DNG2's message 7 shows its second line; PMES8's blank record 95), the scrolls (mixed
  case; A, G, K GREEN, N differ in words), OUT6's two pages.
- **Strings:** `p2-c64` alternatives found verbatim (`build_string_sources.py`: shared 56, I 158, II 17) in `TOWN2`,
  `OUTDOORS2`, `DUNGEON2`, `CEMETARY2`, `WIN`, `SCROLL0`, and 66 II ids in the C64's own wording
  (`tools/scripts/p2_c64_own_wording.json` → `en.own.p2-c64.json`: Pluto's speech, the Filmon ending, the Mystic, three
  field lines; two joins are the remaster's `[~]`, D2-056). `phantasie2/strings/fallback.p2-c64.en.json` gives
  `{0} SWIMS`; the common and II fallbacks the rest. A C64-only import leaves no "..." in the public repo
  (`P3StringFallbackTests.PublicMask_C64IiOnly_NoEllipsis`).
- **With the ST or the Apple** (same run or earlier): their tables win; the C64 writes none and warns. An Apple import
  after a C64-only one replaces the C64's tables (`A2GameData.Write` treats `platform` `c64` as its own): the
  Apple-over-C64 order is the user's (s218), as III's.
- Played in the Client on Xvfb (s218, a dev build, the import in a throwaway home): new game, PIPPACOTT, Guild (new
  member OKAY, Add lists the C64's elemental names), the Mystic in the C64's wording, EXIT to the wilderness, a step.
  Again with a no-built-in-data build (`-p:OpenPhantasieBuiltInGameData=false`, as a release): new game, Guild, EXIT,
  a step, no errors (then without pictures; the art pack came in s219, below).
- **Art (s219):** the `c64` pack (`P2C64Art`, `P2C64ArtManifest`, recipe 2; `reverse-engineering/c64/phantasie2/GRAPHICS.md`).
  I's steps, moved into the shared `P1C64/C64PhantasieArt` (+ `C64PhantasieArtManifest`), with II's own monsters, title,
  `FONT` and map routine tables (`UTIL-3.O` `$C68A` / `$C697`; the dungeon sprite on byte 50 only); the portraits, town
  buildings, map sprites and dungeon characters are I's (the same files). Files from side 1 first, then side 2
  (`P2C64Source.Either`). `index.json` lists it after the ST and the Apple, so a C64-only player gets it. Played on Xvfb
  with a player's build (`parity/s219-p2-c64-art-live.md`): title, Guild, Pippacott, the wilderness (II's terrain
  colours, the town / inn / dungeon sprites), a fight (GREEN MUCK), Dungeon 1. Reference: the original in VICE (town,
  overworld, two fights, the dungeon; GRAPHICS.md 7): its VIPER shows a several-sprite monster's attack (the extra block
  in sprite 0), so I's and II's packs now give all 80 monsters an attacking frame. **Falls back**: UNDEAD's
  portrait, `decor.door` / `decor.sorry-sign`, combat effects (since s221, recipe 3, the ending: the hall, TLUM1's crowd,
  Filmon's pages; the party wipe through II's defeat scene: the temple, the face's doorway, the ghost); the
  item / feature pictures are drawn as I's since s221 (the same files, rule and slots). Recipe 5 (s224): `WIN` as the C64
  runs it (`ending_scene.c64_win`, `C64WinScreen`; GRAPHICS.md 8.1), with the C64-only lines `c64.p2.crowd_goes_wild`,
  `title_the_x_one`, `title_from_x`, `press_ret` (the json's `only` ids, plain `p2-c64` alternatives in
  `en.p2-c64.json`), and the party wipe's UNDEAD name row.
- **Left:** the C64's bytecode rules (group size, restock, training: not read `[?]`); `MN` fields 14 / 24 `[?]`; the
  art's `[?]` (GRAPHICS.md 6: the panel's text colour, I's open points).

### 7.13 What a C64-only III import gives (s218, art s220, own wording s221)

`P3C64Tables` (`Pipelines/P3C64/`, layouts in `reverse-engineering/c64/phantasie3/FORMAT.md` sections 3 and 7) reads III's
tables from the two sides of the C64 disk (C64PP's `.g64` or `.d64`) in `P3Extract`'s shapes; the unchanged `P3Build`
writes the ten files `P3DosPipeline` writes. Two profiles, one pipeline base (recipe 3; 1 tables and starter party, 2 the art, 3 the own wording): `p3-c64` (v1.0, also the held
v1.1 and cracked pairs, which carry v1.0's programs) and `p3-c64-1.2` (v1.2 (alt): its own `PHANT.OBJ` / `TOWN.OBJ` and
starter rolls). They run after `p3-dos-1988`, `p3-amiga-1.0` and `p3-apple2`.

- **From the C64:** `MONSTERS` (DOS's 38-B records), `DATA` (item names), `PHANT.OBJ` (item points, spell names, the name
  tables, area levels), `TOWN.OBJ` (requirement, value, price index, HP dice, ceilings, skill tables, spell rows),
  `TOWN A`–`Q` (stock), `DNG0`–`DNG9` (DOS's record layout; text through the Apple III's window-font path),
  `SCROLL A`–`T` (screen-code pages), `MATERIALMAP`. A PRG's load address is dropped.
- **DOS's values:** the shipped class (a) `phantasie3/c64-import.json`, 265 cells made by
  `tools/scripts/build_c64_p3_import.py` from the importer's raw build (byte 27, the five Apple-shared monster records,
  four item cells, stock and Tirith, HP L16–22, the tables the C64 lacks, the skill tables as DOS reads them with
  D3-032's fixes, DOS's Scandor rows, plane maps, places, start and terrain, three scroll page maps, the Air
  elemental's spells, five names), applied by `CellPatches`. With them every file equals the DOS build but for
  provenance and the C64's own text (upper-case names, dungeon messages, scroll pages; overworld place names DOS's).
- **The starter party (user s217):** `P3C64StarterGuild` reads `PCHARS0` slots 0–5 of a fresh roster (each build's own
  sha1; FORMAT.md 3.5 decodes the 170-B record from the code) into the import's `phantasie3/starter-guild.json` (never
  shipped), in Core's terms. `StarterGuildLoader` (Data) loads it only while `monsters.json`'s `_source` starts with
  the file's `tables` label; `GameSessionFactory` hands it to the session; `GameSession.StartNewGame` puts each member in
  its free Guild slot through `ICampaign.CreateStarterMember` (III: the record's values, Core's rules for the rest with
  no draw). They are in Pendragon, so Add needs no two-month wait. A played roster gives none (warned); a fresh run then
  a played run of the same version leaves no stale file. **D3-078 (user s218):** v1.0's six carry age clocks of 255–314
  years, under the manual's lifespans four of them died at the first town entry; a v1.0 import now writes them without
  an age clock (`record_age_clock` keeps the stored one) and a new game gives them Guild New's start age (DOS's age roll,
  then D3-041's start age for the lifespan); v1.2's stored ages fit the C64's creation formula and are kept.
- **Strings:** `p3-c64` / `p3-c64-1.2` alternatives for 16 ids found verbatim (status / class words; the importer's
  `<file>:low7` key clears bit 7, which ends each C64 string); everything else III shows takes the Apple III's shipped
  fallback (`ImportedStrings.WriteFallback` with extra profile files), so a C64-only III leaves no id on "..."
  (`P3StringFallbackTests.PublicMask_C64IiiOnly_NoEllipsis`).
- **The C64's own wording (s221, recipe 3):** `tools/scripts/p3_c64_own_wording.json`, keyed per build, maps 119 ids
  per build (shared 8, I 27, III 84; 87 of them in `fallback.p3-apple2.en.json`, 32 from the common fallback) to references into its `PHANT.OBJ` / `TOWN.OBJ` / `DUNGEON.OBJ` (`<file>:low7`),
  as the Apple III's (D3-077): `build_string_sources.py` adds them as `p3-c64` / `p3-c64-1.2` alternatives marked
  `"wording": "own"` (`--show-c64-3`), and an import whose III tables are the C64's writes them to
  `phantasie3/strings/en.own.<profile>.json`, on top of III's chain over the Apple's fallback. Generator (quotes the
  lines, not exported): `reverse-engineering/c64/phantasie3/working/gen_own3_c64.py`. The two builds give the same
  text but `p3combat.rank_question` (v1.0 `WHICH RANK?`, v1.2 `WHAT RANK?`). Mapped: the monsters' answers (`MONSTERS
  RESPOND`, `NO RESPONSE`, `GOLD TAKEN!`, `MONSTERS FLEE`, `YOU ESCAPE`), surprise and night watch, combat (hits,
  misses, awakens, casts, `BREATHS`, `--DEATH--`, `NOT ENOUGH MAGIC`, `WHICH SPELL?`, `ON WHO?`), damage / death /
  lava / swimming (`{0} TAKES {1} DAMAGE`, `DIES!`, `SINKS FOR {1} DAMAGE`), ocean / Styx, `{0} ENTER?`, the dungeon's
  lock / trap / event lines, `PRESS ANY KEY`, the Mystic (`YOU ARE: … ADVENTURERS`, the heroes lines), Guild / Armory /
  Trade / Inn / learning prompts and refusals, the Astral Plane and its verdicts, the reward speech (from `BEFORE YOU
  IS THE GOD {0}`: the C64 words each god's room its own way, so the room sentence is left out; `{0}` = `ZEUS.` /
  `PLUTO.`). Glue we add: one space for a row break or padding, `. ` between the farewell's two sentences, `, ` in the
  night-watch line, `?` after `GIVE TO WHOM` (the C64's `(7=SELL)` dropped), `\r` in the journey question. Placed by
  the routine's neighbours only `[~]`: `NOT ENOUGH GOLD` (the field inn), `MONSTERS RESPOND` (Greet), `ON WHO?` (the
  combat heal), `ALSO HEROES OF FERRONRAH` (of the C64's two Ferronrah lines), `{0} MADE IT!` (trap disarmed),
  `{0} SPOTS ITEMS: {1}`, `HE HASN'T ENOUGH POINTS` (field casting), `THERE IS NOT ENOUGH ROOM` (roster). Kept on the
  fallback: prompts whose answer keys differ (`YOU HEAR MONSTERS. F)IGHT OR R)UN?`, `SLEEPING MONSTERS LEAVE?`), the
  status sheet's fixed-width labels, terms (orders, skills, body areas, statuses), the monster spell lines, the
  surrender, the reward's teleport line, and lines the C64 lacks. Tests: `P3StringFallbackTests.C64IiiOnly_OwnWording_PerBuild_OverTheFallback`, `C64IiiOwnWording_NotBesideDosIii`.
- **With DOS III, the Amiga or the Apple** (same run or earlier): their tables win, the C64 writes none and no starter
  party, and a later import of one of them replaces the C64's (their `OwnsTables` take over the C64's label).
- **Art (s220, recipe 2):** the `c64` pack (`P3C64Art`, `P3C64ArtManifest`; `reverse-engineering/c64/phantasie3/GRAPHICS.md`),
  written whichever copy's tables win; third in III's `default_order` (after the Amiga and the Apple). III's own engine,
  not I / II's: monsters from `MONSTERS`' sprite blocks by record byte 27 with `PHANT.OBJ`'s colour tables (Boo Boo
  Kitty's two blocks are `PTOWN1`'s tail), `HEROSPRITES` figures by class (Monk / Ranger / Priest / Wizard, the new
  `portrait.class.*` slots the Client draws before the race's) or by race (Thief / Fighter), the Koala title, the town
  strip, the map from `DATA`'s tile tables over `PSET0` (Scandor's DOS bytes swapped to the C64's tile, `tile_swaps`; a
  10 × 9 view with the party on cell (4, 4), the new `overworld_party_cell`), the dungeon's 6 × 3 sprite-bitmap glyphs,
  and `TOT` / `GUT` / `SCHLECHT` as the defeat and the gods' pictures. Seen live with a player's build
  (`parity/s220-p3-c64-art-live.md`); the original in VICE (GRAPHICS.md section 9).
- **Played copy:** the towns' stock files are read as played (warned, with the cells that hold neither value), and a
  played `PCHARS0` gives no starter party (D3-079, decided as built s218, with the precedence, the dungeon names by
  content and the mixed text).
- **Live (s218, Xvfb, dev build, v1.2 (alt) imported into a throwaway home):** new game in Pendragon, the Armory (the
  C64's item names, DOS's stock), the Guild's Add lists the six and each joins at once, CHUMLEY's sheet (AGE 13, 12 HP,
  256 gold, MACE / ROBES / SM SHIELD / SELF BOW), leaving town, the overworld, a fight with four Drip Slimes (shown
  "DRIP SLIMES(S)": the C64's plural name gets the remaster's "(S)", D3-079).
- **Left:** the undecoded tables of FORMAT.md section 7 (DOS's used); the own-wording lines marked `[~]` above.

### 7.14 What an Apple-only I import gives (s222)

`P1Apple2Tables` (`Pipelines/P1Apple2/`, layouts in `reverse-engineering/apple2/phantasie1/FORMAT.md`) writes the
eleven files `P1DosPipeline` writes from the boot and dungeon sides (the 4am `.dsk` pair or the original `.woz` pair,
one played copy). As for the C64 (7.11), each Apple table is put into DOS layout and goes through DOS's builders.
`P1Apple2Pipeline` (recipe 4; 2 = s223's art pack, 3 = s224's combat field: the spell hit at every monster SCALE and `apple2_field`, 4 = s224's end scenes) runs after `p1-dos-1987`, `p1-dos-bonus-1991`, `p1-amiga-1.0` and `p1-c64`.

- **Detection:** side A `TOWN`, `OUTDOORS`, `CEMETARY`, `ITEM`, `TINFO` (byte-identical to II's Apple `TINFO`),
  `SCROLL1`; side B `DUNGON`, `DNG1`, `DNG10`, `TF`; name markers `TOWN` + `OUTDOORS`. Mutable files: `TWN1`–`TWN11`,
  `TWN99` at the sha1s of `TF`'s slices (the new game's towns); the held pair reports all twelve played and imports as
  fresh (`PlayedCopySafe`: towns from `TF`, regions reset, elementals from `CHAR`'s records a new game keeps).
- **From the Apple:** `MN0`–`MN9` (monsters, 25 text fields), `ITEM` + `TINFO` 89 + id (item names, values), `TOWN`'s
  DATA (classes, races), `TF` (towns), `CHAR` records 37–39 (elementals), `OUT1`–`OUT19` (grids, features, CR-ended
  texts put into DOS's page form), `DNG1`–`DNG10` (bytes 0–1961: the level format, DOS's layout; messages from the
  40-byte slots at 1700, menus split into options by the event records' choice counts), `SCROLL1`–`SCROLL20` (text
  pages, the rows inside the frame).
- **DOS's numbers:** the shipped class (a) `phantasie1/apple2-import.json` (`tools/scripts/diff_a2_p1_vs_dos.py
  --built`, from the raw build `P1Apple2TablesTests` dumps): 394 `dos_cells` (28 monsters' numbers and byte 0x34;
  OUT1's row 0, (18, 14) and feature order, leftover bytes in unused feature records, OUT13 / OUT15 cells; 28 dungeon
  bytes incl. D-086's three squares and DNG10's level byte; the elementals' MP ceiling, creation number, race;
  Pelnor's stock of item 22), DOS's monster sprite sizes; every region gets DOS's start state (DOS's Reset rule on all
  but OUT1 / OUT5). With them every number equals the DOS build; the dungeon bytes equal DOS's as far as both go.
- **The Apple's text:** item names (`SH SWORD`, `HEAL POT1..9`, `RING I`..`IX`, its own treasures), town 4 `HOBBITON`,
  `LIZARD MAN`, two monster typos fixed to DOS's spelling (D-089, user s223: `APPRENTICE DEVIL`,
  `MASTER ILLUSIONIST`, `name_fixes`), messages and scrolls in its own wording. The Apple draws the
  wilderness turned a quarter: the region texts' and scrolls' compass words are turned to DOS's map (`scroll_compass`,
  as II's Apple import, D2-046). Spell names: the manual's table + `DIVINE I` (D-087's rule); `FIRE ELE` → `FIRE ELEM`.
  Item descriptions (remaster-written) gained texts for the Apple's treasure names and its ring numerals.
- **Strings:** `p1-apple2` alternatives for 254 ids found verbatim in `TOWN`, `OUTDOORS`, `CEMETARY`, `DUNGON` (I 174,
  shared 67, II 11, III 2; `build_string_sources.py`); the common fallback and `phantasie1/strings/fallback.p1-apple2.en.json`
  for the rest (32 ids: 23 shared field / map menu words, box titles and sheet labels with the table's text; Zeus's
  speech and the Black Lord line reworded, as the Apple's `FLASH?` words its own short scene). No I line stays "..." in
  the public repo. s224: the end scenes' 13 lines (`apple2.defeat.*`, `apple2.ending.*`) from CEMETARY / FLASH?
  (`p1_apple2_scene_text.json`); Core's Zeus ids keep the remaster's wording (the Apple's scene speaks its own lines).
- **With DOS I, the Amiga or the C64** (same run or earlier): their tables win; the Apple writes none and warns. The
  Amiga's and the C64's `OwnsTables` take over the Apple's.
- **Decided (user s223):** D-088 manual (every region in DOS's start state, as built), D-089 fix.
- **Art (s223):** `P1Apple2Art` (through `TryOptional`, whichever copy's tables win) runs II's Apple shape steps
  (`A2Graphics.RunShapes`; I's engine is II's) on I's `SHAPES` (58 shapes) and `MN0`–`MN9` (shape, SCALE, HCOLOR), plus
  `HPIC` as the title: the pack `phantasie1/graphics/apple2.json` (shipped, layout only), pictures under
  `reverse-engineering/apple2/phantasie1/extracted/graphics` (`GRAPHICS.md`). Third in I's `default_order` (Amiga, C64,
  Apple). No town / item / ending / defeat pictures (the town is text). Played on Xvfb s223
  (`parity/s223-p1-apple2-art-live.md`).
- **Combat field (s224, recipe 3):** the pack's `apple2_field` (OUTDOORS / DUNGON 1480–2006, `GRAPHICS.md` section 7)
  and the spell hit at every monster SCALE; the Client reads H% / SCALE / YS% and the DRAW points from the import's
  `extraction-index.json`. Played on Xvfb (`parity/s224-p1-apple2-combat-live.md`).
- **End scenes (s224, recipe 4):** `P1Apple2Scenes` writes `scenes/shapes.bin` (`SHAPES`) and `scenes/page2.bin` (hi-res page 2
  as FLASH? flashes it, rebuilt from `SHAPES` and FLASH? `[~]`) beside the art; the pack's `apple2_scenes` lists
  CEMETARY's and FLASH?'s statements with the interpreter's times (the Apple II+ ROM traced in a 6502 emulator,
  `tools/scripts/a2_basic_trace.py`), replayed by the Client on a ROM-exact hi-res model (`GRAPHICS.md` section 8;
  `parity/s224-p1-apple2-endings-live.md`).
- **Open:** the art's `[~]` / `[?]` (`GRAPHICS.md` sections 6–8).

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
  tile overworld applies it (`TileMapLayout.DrawnTile`). Golden test: 512 PNG pixel-equal to the Python extract. An
  import made before s160 gets the pack from the recipe re-run at the next start (4.4).
- **III CGA pack in the Client** (Xvfb). Chosen in the settings (III test party): Guild portraits, the town street
  with the name on its banner, the overworld equal to the DOS shot `s143_map_e_key_nothing` cell for cell, the body
  figures. From a DOS-only public-tree import (no Amiga pack anywhere, so the catalog's fallback chose it): the same,
  plus a fight (the CGA monster pictures on the field, then the orders panel). Details found and fixed (FORMAT.md
  section 10):
  - DOS switches CGA palettes per screen (`0x284:0xaaf`; palette 1 on the dungeon map, planes, combat field, gods and
    Astral Plane; the pack's `palettes` / `scene_palettes`); Astral Plane, gods, Scroll IV and the dungeon map fixed.
    The Astral scene's palette 0 (`0x240c`) comes after its last line and a cleared screen, as the remaster's scene ends.
  - The 15 glyphs are the text page frame (`scroll_frame`); the three 24×24 frames the hit / gods flash
    (`combat_flash`), ORed onto the screen (`blend: "or"`, a max blend, `0x284:0x19c`).
  - The town name is centred in 12 cells at row 2 (root `0xa9e2`); `view_counters` from root `0x125d`; the Amiga does
    not swap the Scandor tiles.
  - Scroll IV's caption under the map (`scroll_frame.picture_caption`, string `useitem.scroll_picture_caption` from
    DS:`0x2699`, root `0x1b4`).
  - The four overworld steps (ovl3 `0x13ac` / `0x1470` / `0x154e` / `0x1612`): the view's centre stops at columns − 5
    and rows − 4 (`tile_maps.view_centre_margin` [5, 4]), so Scandor's view ends at columns 41–49 and DOS never reads
    past a map. Shots `parity/shots/p3-dos-cga-s170-*`.
  - The remaster's auto-map box is not drawn over a tile dungeon (III, both packs: it covered the second member card).
    Instead a tile dungeon draws its own whole-level overview (QoL, the same Tab switch `AutoMapVisible`): 24 × 30
    cells of 3 × 2 px in a 76 × 64 box inside the view left of the cards, in the corner farthest from the party; a
    seen cell takes its glyph's commonest colour (DOS CGA palette 1: stone white, floor magenta), the party cyan.
    Neither original draws a box (the Amiga's dungeon screen is the whole level). Shots `parity/shots/p3-s171-automap-*`.
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
  `THIRD-PARTY-NOTICES.md` (MonoGame MS-PL, SDL2, OpenAL Soft, xUnit, test SDK, coverlet), `.gitignore`, and
  the README's five Client screenshots (`tools/public/screenshots/*.png` → `docs/images/`, the only images besides the icon).
- Never: disk images, ROMs, manuals, other screenshots, parity shots, extracted art / audio / text, translation sources, the
  DOS dungeon save fixtures (`DNG*.sav` hold a level's map).

### 8.2 Tests in the public tree

- `tests/Shared/OriginalData.cs` (linked into the three test projects): `[OriginalDataFact]` / `[OriginalDataTheory]`
  skip when the full GameData (one (b) table per game) is not next to the tests. 1,838 tests carry them (the ones that
  failed in the public tree). Importer golden tests keep `RequiresFilesFact` (reference images).
- Test methods whose assertions quote original text live in `*.OriginalText.cs` partials; the export leaves out every
  test file that quotes a text-file line or a string-table line, when all its tests need the data and it holds at
  least one test (12 files on s158, 53 on s169). Fixture lines that were original text are made-up lines of the same
  length.
- Public-tree runs, pass / skipped, no failures: s158 Core 1,079 / 1,155, Client 493 / 601, Importer 6 / 50 (private
  unchanged, 2,730 + 1,331 + 140, Importer +7 new); s163 (local export, not pushed) Core 1,079 / 1,157, Client 545 / 629, Importer 21 / 80.
  Importer tests that read files the public repo leaves out are gated on those files (I's `dungeons.json` note, the
  ST cells against the ST GameData).

### 8.3 Checks and the manifest

The export fails on: a file over 512 KB, any image / audio / disk / binary data file except the client icon and our
`GUILD.*.dat` test characters, an original-data path, any line of the I-III text files or IV's Japanese story lines
(16+ characters) outside `ALLOWED_QUOTES`, the per-game probe lines, a Steam user name. It lists for review: short menu
/ message lines quoted in code, tests and notes, long names in shipped GameData, the left-out tests. **s169: 2 files of
893 (12.1 MB), 53 left-out tests** (7.4). Code comments, XML docs and `_note` fields name the string id in backticks
instead of quoting the line, quoting tests live in gated `*.OriginalText.cs` partials, stand-in lines are made up.
Earlier: 146 of 865 files (11.5 MB, s158; 11 left-out tests), 149 of 886 before s163, 61 of 891 (12.0 MB) after it
(pack files the extractors write byte for byte were left then).
Acceptance (s158, by hand): the public CLI on the reference images (DOS I, Amiga I + Kickstart, ST II, DOS + Amiga III,
X68000 IV) + the public shipped GameData give all 88 GameData files JSON-equal to this repo's (string tables layered
as `StringTable.ForGame` does); the Apple II II import is byte-identical to the private build's; the public Client on
Xvfb lists all four games and starts I with the imported Amiga art.

### 8.4 Player builds, CI and the self-updater (s174)

- **Downloads:** `tools/scripts/package_release.py --rid <rid> --version <v>` publishes the Client self-contained
  (`OpenPhantasieBuiltInGameData=false`, so a build holds only the shipped GameData; players import their copies) and
  packs `OpenPhantasie-<v>-win-x64.zip` (folder `OpenPhantasie/`, `OpenPhantasie.exe`), `-linux-x64.tar.gz` (folder
  `OpenPhantasie/`) or `-osx-x64` / `-osx-arm64.tar.gz` (an `OpenPhantasie.app`, program in `Contents/MacOS/`, signed ad
  hoc by `codesign` on a Mac; not notarised). The program file is `OpenPhantasie` (the Client's `AssemblyName`). The
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
| apple2 | `phantasie2/v2.1-a2can/phantasie2-v2.1-side-a-boot.dsk` (RDOS 3.3) | 143360 | `b5efe8c9e28989e2e1689b7143d4202503237031` |
| apple2 | `phantasie2/v2.1-a2can/phantasie2-v2.1-side-b-dungeon.dsk` | 143360 | `b52f391ca2b94346f4d44f10105d1da6c798f769` (both in `p2-apple2-2.1`'s `images`, s212) |
| apple2 | `phantasie3/Phantasie_III_1987_SSI_cr_Disk_1_of_3_Side_A.dsk` | 143360 | `b7ae3f30298c41fd9a2abfa2bcd566cf4051a39a` |
| apple2 | `phantasie3/Phantasie_III_1987_SSI_cr_Disk_1_of_3_Side_B.dsk` | 143360 | `51446a1e54c1ef84c13bb480d559f9b240b5b4e1` |
| apple2 | `phantasie3/Phantasie_III_1987_SSI_cr_Disk_2_of_3_Side_A_Dungeon.dsk` | 143360 | `da829c612540f772012cc2bc11674a16ac72d4f4` |
| apple2 | `phantasie3/Phantasie_III_1987_SSI_cr_Disk_3_of_3_Side_A_Player.dsk` | 143360 | `4aac758b75a1e1d682e9477ebebb611a1b159dbc` |
| apple2 | `phantasie3/woz-original-v3.0/phantasie3-v3.0-disk1-side-a-boot.woz` | 233475 | `534d51086d981163ac5fa47f6947c0b85ecb37b8` |
| apple2 | `phantasie3/woz-original-v3.0/phantasie3-v3.0-disk1-side-b-utilities.woz` | 233475 | `3a2344b877cce46b57e29801e39f607f74ed603e` |
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
| I C64 v1.4 | `TOWNR`, `TINFO`, `MN`, `ITEM`, `SCROLLS` (side 1); `DUNGEON`, `DNG 1`, `PMES 7`, `PATTERN` (side 2) | | `profiles.json` `p1-c64` (= `c64/phantasie1/extracted-v1.4/manifest.json`; sizes with the load address) |
| III C64 v1.0 / v1.2 (alt) | `PHANT.OBJ`, `MONSTERS`, `DATA` (side 1); `TOWN.OBJ`, `DUNGEON.OBJ`, `MATERIALMAP`, `DNG1` (side 2); v1.2 its own `PHANT.OBJ` / `TOWN.OBJ` | | `profiles.json` `p3-c64` / `p3-c64-1.2` |
| I Apple II v1.0 | `TOWN`, `OUTDOORS`, `CEMETARY`, `ITEM`, `TINFO`, `SCROLL1` (side A); `DUNGON`, `DNG1`, `DNG10`, `TF` (side B) | | `profiles.json` `p1-apple2` |
| II ST v1.0 | `PHANT.PRG` | 155785 | `1386c07157720536852d853d1a80d8237e9a8592` |
| II ST v1.0 | `START.PRG` | 2567 | `e79d928c3628f58b40062016a73cf25366202c91` |
| II ST v1.0 | `TWNS.INT` | 13900 | `b5c4b95f38e72b0c06d4029729dc67ba2d055cb5` |
| II ST v1.0 | `MAPS.INT` | 8840 | `16c993df0bb09e14e9779a6e989255bc34b7d484` |
| II Apple v1.0 | `PHANTASIE II` (side A), `TOWN2`, `OUTDOORS2`, `ITEM`, `TINFO`, `TF2`, `SHAPES`, `ICE DRAGON`, `INFO`; side B `DUNGON2`, `DNG1`, `DNG8`, `SCROLLL0` | | `profiles.json` `p2-apple2` (= `apple2/phantasie2/extracted/key-files.json`) |
| II Apple v2.1 | `OUTDOORS2` | 18650 | `298afe2c09b4a2f7f03feb0a1bc37dfcda6f4ce6` |
| II Apple v2.1 | `DUNGON2` (side B) | 19580 | `43fcedaed28f56ff10aa8f7218be369b2e23ab29` |
| II Apple v2.1 | `SCROLLL0` (side B) | 1000 | `d573bf031805acd426352a734730f50ec332d16f` |
| II Apple v2.1 | `INTRO2`, `MAP` | 676, 750 | `0eb578a682d7584cfc46a5bddce41f2564a3f4ab`, `8d4d3e0e6de46e3611e0a1544f20c47e8487d8a6` |
| II Apple v2.1 | `PHANTASIE II`, `TOWN2`, `ITEM`, `TINFO`, `TF2`, `SHAPES`, `ICE DRAGON`, `INFO`, `DNG1`, `DNG8` | | as v1.0 (s212) |
| III DOS 1988 | `MAIN.EXE` | 152564 | `b85cd0eaab25884af4e90d298407e42a288b5ede` |
| III DOS 1988 | `CREATURE.DAT` | 3072 | `a6341c8b7c4bb4bb473682f5267df4ad30850803` |
| III DOS 1988 | `DNGS.DAT` | 46464 | `20bfda72d55ee07801764ed33a3c00fb46804d9f` |
| III DOS 1988 | `WORLD.DAT` | 3750 | `3b4aaa861bf40760eb9dd727593ba159094f6ac0` |
| III DOS 1988 | `PLNS.DAT` | 658 | `5f851b3ffdd8e7663ba3b9e97452692f81f7220a` |
| III DOS 1988 | `INITARMS.DAT`, `SCROLLS.DAT` | 3328, 19950 | `56a360b7b000150cba73c546d5030522be54641b`, `643f7bbfb2ec53920103fe0ce2472030d20b7565` (s158: read at fixed offsets) |
| III DOS 1988 | `TOWN.CMP`, `PICS.CMP`, `WORLD.CGA`, `SHAPES.CGA`, `MONSTERS.CGA` | 10736, 30170, 16000, 16192, 48768 | `642b4e3e…`, `a4c9c882…`, `3ac9f4ac…`, `e7a81eb9…`, `088129fb…` (s160: not key files; the CGA pack checks them, `P3CgaArt.Sources`) |
| III Apple II (v3.0 and v1.0) | `TF3` | 4250 | `83382f6dbd996edd659ba8bc4f9ff1b69499bd1b` |
| III Apple II | `ITEM` | 1792 | `3a40c2587a49976ff853017c7f09418ede2f3b6f` |
| III Apple II | `TINFO` | 1730 | `b0208442d7f8d82f59140530da690ba6431478d0` |
| III Apple II | `DNG1` (side B) | 4132 | `b6dde4904567dfdf94e593cd682c8ce28efefd84` |
| III Apple II | `DNG10` (side B) | 4132 | `78c88193a938baf2d53f3e43904bf35a36405587` |
| III Amiga v1.0 | `PhantasieIII` | 174928 | `40548339ddbcbc3be8c2a17055f322211b538150` |
| III Amiga v1.0 | `monsinfo.dat` | 3072 | `a6341c8b7c4bb4bb473682f5267df4ad30850803` |
| III Amiga v1.0 | `D/m0` | 896 | `b7cfea849f37d08467e065b02c7b8abb856ed290` |
| IV X68000 1.02 | `P4.X` | 341164 | `2a7e8ce31693524e11f896f30a3950a73c70ec48` |
| IV X68000 1.02 | `P4.SND` | 16000 | `9ff80524abfd2c4941b176300c3d6b10d37db591` |
| IV X68000 1.02 | `VDATA` | 6609 | `82d268e44a397db7e0ce6a6386091bc61d41f0a6` |
| IV X68000 1.02 | `TABL` | 2130 | `aed9290319305193de884a4a16c01843b517c29f` |
| IV X68000 1.02 | `DATM` | 5880 | `a4912cb4f4ed43bc9ab34754fbefa398b4dc3fbd` |
| IV X68000 1.02 | `MOUT` | 12600 | `9e9f0cc61c3409381c3fcf9b99390472980475f5` |

III WizardWorks 1991: only its extra `.BAT` files are unpacked in the repo; it is detected by `p3-dos-1988`'s key files
(3.1, 7.2). The IV disk listings with per-file sha1s are in
`reverse-engineering/x68000/phantasie4/unpacked/disk1.json` / `disk2.json`.
