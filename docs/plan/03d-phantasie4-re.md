# Phase 03d — Phantasie IV reverse engineering (X68000)

Phase 09 T6.2 (and the text half of T6.1). The IV counterpart of `03b-phantasie2-re.md` (II, Atari ST). I's
`03-dos-disassembly-and-logic-extraction.md` stays the reference for I's rules. What is left of IV overall is on
`PLAN.md` (T6).

- **Rules source:** X68000, Star Craft, released 1991-03-28 (user, s71: the last release). Images:
  `reverse-engineering/x68000/original-images/phantasie4/` (2 × 2HD `.dim`, TOSEC; `SOURCES.txt` one folder up).
  Cross-checks: PC-98 (`pc98/original-images/phantasie4/`, x86 MS-DOS), MSX2 + the 2008 DJANGO English patch
  (`msx2/original-images/phantasie4/patch-2008/`, cross-check only).
- **Source of truth:** the **Japanese PC-9801 manual** (found s81 on MOCAGH), as the manuals are for I–III (user s81:
  manual first, above the magazines and catalogue): `docs/manuals/phantasie4/phantasie4-pc98-manual-jp.pdf`, with the
  spell list and the supplementary (start-up) manual; transcription + English in
  `docs/manuals/phantasie4/phantasie4-manual-en.md`. Secondary written sources: the MSX Magazine articles, the postcard
  advert and the item catalogue (summarised in `09-trilogy-iv-parentage.md`; more in
  `phantasie4-secondary-sources.md`). Conflicts between the manual and the code go to the user and into
  `DIFFERENCES-IV.md` (same rules as `DIFFERENCES.md` for I); where the manual is silent, the code stands.
- **Working folder:** `reverse-engineering/x68000/phantasie4/` — `unpacked/` (files + README + sha1s), `extracted/` (JSON
  tables, string table), `FORMAT.md` (format notes, `[C]`/`[~]`/`[?]`), `FUNCTIONS.md`, `logic-specs/`.
- **Translation:** in-game text → `extracted/text/strings.json` (Japanese + English column); hand translations in
  `reverse-engineering/x68000/phantasie4/translation/en.json` (keyed by the exact Japanese text, merged by the extractor);
  judgement calls in `docs/manuals/phantasie4/translation-notes.md`.

## Status

- [x] **3d.1 Container.** `tools/scripts/x68k_dim.py` reads the `.dim` container and Human68k FAT12 (big-endian BPB).
      131 files extracted (66 on disk 1, 65 on disk 2), sha1s in `unpacked/README.md`. FAT copy 2 is stale; `autoexec.bat` is
      dated 2000 (not pristine). Both masters hold **deleted pre-release test saves** (1991-02-28 to 03-13), recovered
      intact into `unpacked/deleted-disk{1,2}/` (4 save sets: `goods/guils/pmemb/fames/contr/party`).
- [ ] **The third floppy `[?]`.** `_save` / `_load` write and read all six save files (`fames/goods/guils/pmemb/party/
      contr.0–3`) on drive 1 (`B:`), the drive `DATM` / `ELMS` / `p4mon.pix` / `MOUT` come from, so with two drives the
      saves go to **disk 2**, the disk `HD.BAT` calls the player disk (`FORMAT.md` section 6.1) `[C]`. P4.X checks `id2`
      on `A:\` / `B:\`; the III transfer screen swaps "the disk in drive 1" for III's player disk. No dump of a player
      disk exists, so there is no factory-fresh save; a new game is built by code plus `FAME` (the initial Hall). Open:
      what refuge.tokyo's third floppy is (a separate player disk in some releases, or a miscount).
- [x] **3d.2 Program inventory** (`FORMAT.md` section 3). `P4.X` (341,164 B) is an unpacked Human68k `HU` executable
      **with its full symbol table** (1015 names: every function and global keeps its C name). Relocations decoded
      (`tools/scripts/x68k_x.py`). XC compiler runtime; FLOAT2 line-F floating point; Shift-JIS text. Data files: `DATM`
      (monsters), `ELMS` (elementals), `FAME` (Hall), `TABL`, `MOUT`, `VDATA`, `O_001–021` (locations + event text),
      `SCR1–5` (scrolls), `P4.SND`, pictures `*.PKH` / `*.PIX` / `*.PDT`. Human68k 2.0x, `COMMAND.X` and three drivers
      are on disk 1. `_xprtck` / `_xchkpgm` is a start-up original-disk check (cylinder 76 head 1 sector 1 on drive 0) `[~]`.
- [x] **3d.3 Master tables** (`tools/scripts/extract_x68k_p4_tables.py` → `extracted/`): names of planes, origins, towns,
      inns, locations, 35 races, 10 classes, 10 social classes, 6 abilities, 11 skills, 157 items, 65 spells
      (`names.json`); item records `_idat` (`items.json`, DEF/ATT `[C]` against the catalogue); 84 monsters
      (`monsters.json`); 3 elementals; the initial Hall of Glory (`hall-initial.json`). Symbols: `p4x-symbols.json`.
      Record fields: 3d.5.
- [x] **T6.1 in-game text** (`tools/scripts/extract_x68k_p4_text.py` → `extracted/text/strings.json`): 2125 rows from
      P4.X, `O_001–021` and `SCR1–5`; **all 1279 unique Japanese strings translated** (`translation/en.json`; notes in
      `docs/manuals/phantasie4/translation-notes.md`). Not yet checked in the running game; picture text is not covered.
- [x] **3d.4 Function map:** a Ghidra (68000) project over P4.X with the symbol table applied
      (`tools/scripts/x68k_p4_ghidra_prep.py` + `GhidraP4Setup.java`: all 705 functions decompiled, integer FPACK calls
      rewritten so arithmetic shows). `FUNCTIONS.md` lists all 705 text symbols (gameplay read; UI / graphics plumbing
      grouped, partly `[~]` from callees) and the XC library with its `$FFxx` DOS / `$FExx` FPACK calls.
- [x] **3d.5 Record layouts** (`FORMAT.md` section 6): save set and which drive (6.1); 190-byte character record = guild,
      party and `ELMS` records (6.2); Hall record incl. bytes 12–27 and the III-import field map (6.3); `DATM` 70 B (6.4);
      `_idat` b0 icon, b2 Str/Dex requirement, b3 armour movement %, price, `_gen_itm`, `goods` (6.5); `TABL` = six
      class/race tables tiling the file exactly (6.6, `extracted/creation-tables.json`); `party`, `contr` (6.7).
      Validated on all four recovered save sets (`tools/scripts/x68k_p4_saves.py`, 6.8). Left `[?]`: a few record bytes
      (+09, +28, +2A–2E), several `DATM` fields (+28, +30–32, +40, +43), `party` 120–15F flag meanings.
- [x] **3d.6 Logic specs** (`logic-specs/`):
      - `ancestry.md`: creation menu, both ancestors (N/H, S/D/H), Nikademus / "Not possible" / "Sorry, the ancestor you
        found was …" rules, all 15 mixed-race pairs, the inheritance formula (exponent table on the ancestors'
        bonus-free average) and new-person rolls, class masks, random-race lists, social class from ancestry, the rest
        of the new record (Luck, age, gold, origin, starting spells, Divine Aid), Hall deeds lines + status and how
        `Character.Deeds` can feed them, III transfer (`FORMAT.md` section 6.3) and the transfers / lineage options
        (sections 8.2–8.4, 13).
      - `training.md`: `_trang` (experience needed by class-ability ratio, cost = L^2.2 × (20 − Cha)^2 × town factor,
        cap 25 000, no social / ancestry term); `_calpow` (level-up gold = social^social × L × 30); `_mage` (spell quota,
        price and success roll); cross-checked on MSX2 and against the manual (`_par`+16 counts days).
      - `towns.md`: town types and buildings, town-gate experience share-out, aging and death of old age, inn, bank,
        shop prices / restock / selling, Mystic score, shipwright, outdoor inns, Charon (corrected by `outdoor.md`);
        checked against the manual (pp. 22–32) and MSX2 seg `1000`.
      - `combat.md`: round phases, party weapon hit / damage, criticals, equipment, monster turn, wounds and hit
        location, movement, encounter options, flight, all combat spells and MP costs, auto mode, rewards.
      - `events.md`: dungeon maps, `O_0xx` triggers and the 59 opcodes, traps, chests, doors, per-map flags reset in
        rotation, Examine, the advanced-quest unlock.
      - `skills.md`: the skill formula reproduces all 88 starting skills of the test saves; 3 picks per level-up; where
        each skill is read (Spot Item and Throwing never).
      - `outdoor.md` (`MOUT`, steps, swimming, lava, ships, storms, the shining sea, outdoor encounters, Charon),
        `items.md` (Use Item, potions, magic items, the four special items with no effect), `spells-noncombat.md`
        (incl. `_getmagic`), `ending.md` (five endings, Score +09 = ending score + average level, final screen with
        score and the fixed completion code "Duck"; the ending-flag events).
- [ ] **3d.7 Cross-checks.** MSX2 (`reverse-engineering/msx2/phantasie4/FORMAT.md`: the player disk's own directory,
      segment loading, charset, float format; `tools/scripts/msx2_p4_dis.py`): creation / ancestry (seg `1001`) and
      training (seg `100C`) have the same rules and constants as X68000; the shipped Hall is byte-identical. PC-98:
      `TABL` byte-identical, `_get_social` and `_sell` identical to X68000. CPU-level harnesses:
      `tools/scripts/x68k_p4_cpu_harness.py` runs the real P4.X code with FLOAT2.X's own line-F handler in unicorn;
      `msx2_p4_z80_harness.py` runs the MSX2 segments in a Z80 emulator. Results: `_rand` model exact; the per-monster
      HP formula and the +3B melee bonus hold as read statically (`FORMAT.md` section 6.4, `combat.md` section 1);
      `_get_modifer` below 5.0 confirmed (`ancestry.md` section 5.2); MSX2's monster HP differs (0.5–1.4 × base in 0.1
      steps; X68000 gives 0.5×, 1.5× or the 1.8× cap, a precedence slip; D4-055). Full-system X68000 runs in XEiJ
      (IPL ROM 1.0; how-to in `RE-REFERENCE.md`). Left: the PC-98 combat cross-check; the MSX2 III → IV transfer (MSX・FAN
      1991-03 says it was cut; the MSX2 disk still has the transfer screens) needs an MSX2 emulator (none set up).
- [x] **3d.8 Event bytecode, `MOUT`, pictures.** `tools/scripts/x68k_p4_events.py` disassembles all 21 `O_0xx` files
      (473 of 484 known strings reached plus 70 the string extractor skipped; the rest is unreachable content), listings
      in `extracted/events/`, spec `events.md`; `MOUT` in `outdoor.md` section 1; `tools/scripts/x68k_p4_pkh.py` decodes
      all 56 PKH pictures and `p4mon.pix` (`FORMAT.md` section 6.9; `IND*.PDT` are dungeon maps). Animation sheets and
      tables: `tools/scripts/x68k_p4_anim.py` (below). Left `[?]`: other `*.pix` layouts.

## Built from these specs (T6.3 / T6.4)

The build history is in `SESSION-LOG.md`; what is left is on `PLAN.md` T6. RE facts found while building:

- **GameData:** `src/OpenPhantasie.Data/GameData/phantasie4/` built by `tools/scripts/build_p4_gamedata.py` (`--check`) from
  `extracted/` and the unpacked files; loads through `GameTables.Load(GameId.P4)` (`Phantasie4RuleTables` for
  `rule-tables.json`). Holds DATM decoded per `FORMAT.md` section 6.4, items with the thrown slot, spells (manual MP /
  types, code MP beside), 24 outdoor maps (MOUT) + places, 21 dungeon maps (IND floor grid + full words, O_0xx triggers,
  bytecode and base-relative texts), SCR1–5, towns (with town exits `_townexit` `0x3FF20` and ship squares `_shipexit`
  `0x4001C`), inn names (`_inns` `0x3DABC` → `overworld.json` `name_id`), ELMS, the Hall, and `strings/en.json` +
  `ja.json` (ids `p4x.<addr>`, `o<map>.<addr>`, `scr<n>.<line>`).
- **Core** `src/OpenPhantasie.Core/Phantasie4/`: `Phantasie4Campaign` (not a `GameSession` campaign; IV's records are its
  own) with the town, outdoor and dungeon loops (`P4TownLoop`, `P4OutdoorLoop`, `P4Dungeon`: every key a prompt,
  resumable mid-event), `P4Battle` (7 × 7 field), creation (`P4Creation`, `P4CreationRecord`; reproduces a recovered
  test-save Thief exactly), the `p4.*` options for the `DIFFERENCES-IV.md` decisions. Saves: `P4CampaignSnapshot` →
  `Phantasie4SaveCodec` (`"game": "p4"`; item, dungeon-map and monster ids `p4:`, town / outdoor numbers bare).
- **Code facts found on the way `[C]`:** `_rrace`'s lists start with a count byte (`random_race_lists` in
  `creation-tables.json` keeps it; the GameData drops it); `_use_sc` shows `scr` + (item − 0x8C + 1); facing is written
  only by `_dmove` and SETDIR; a new game is `_load(0)` → town 1, Pendragon, with an empty guild (`FORMAT.md` section
  6.1), and with no party the town shows only the Guild's door; the X68000 Nikademus refusal comes after `_mopta`'s
  roll; combat casts set the inn MP-growth flag `+72`; `_calpow` wraps the level (D4-060); live shop stock is never
  cleared; the found-item appraisal is built as a `[~]` reading (D4-036). Combat (`combat.md` sections 7.1–7.2, 8.2, 9,
  11, 14): the automatic walk (`_b_chauto`) and targets (`_b_amove`, `_obj_select`, `_b_chkflee`), breath's ring scan,
  Greet's `_emcham`, Shock Wave (`_shoot` path), the return square after flight / Beg / Teleportation (`_enc_end` →
  `_cox`, outdoors only; D4-070), auto combat / round / M (`_partyact`, `_eauto1`, `_try_attack`, `_protect`),
  `_atk_list`, spell 64 (D4-071). Not emulated: the monster MV carry-over (an uninitialised stack value).
- **Art (X68000 pack `GameData/phantasie4/graphics/x68000.json`):** town streets, buildings, the Hall, title, outdoor
  map, endings, scroll page, the location-map view (a 9 × 9 overhead view, not 3-D), the combat field with `p4mon.pix`
  sprites, P4.X's own fonts and the Command panels (`FORMAT.md` sections 6.9–6.12).
- **Animation timing (`FORMAT.md` section 6.10) `[C]`:** P4.X never waits for Vsync or a timer (no `_VDISPST` /
  `_ONTIME` / `_TIMERDST` / `_B_INTVCS`, no MFP or CRTC access; `_CRTMOD(0x110)`, mode 16, times nothing). Picture
  animations count `_anima` calls, one per input-loop pass (`_mgetc` / `_mgetc2`); the combat step pose, missiles,
  doors and battle-message waits are plain busy loops. So a pass is CPU-bound and differs per input loop; the pass
  times were measured on XEiJ (10 MHz, IPL ROM 1.0) and are in the pack (per-scene `pass_ms`, `doors.hold_ms`). The
  PC-98 `P4.EXE` has the same tables and code without the `>> 2` (4 × the passes), also pass-counted `[~]`. Sheets:
  `*_AN` / `*ANI` (`extracted/graphics/anim/`, `p4.anim.*`), the 161 frame and 45 animation records with the
  `_setanim` call sites (`picture_animations`), the `_animdoor` door table, the `NTA` start-up switch (`_town_anim`),
  `_ianim`'s `_iaflag` rows; Core `PictureAnimator` / `PictureAnimationPlayer`, `P4DungeonAnimation`, `P4MysticReading`.

## RE targets from the manual (all reached)

Rules the manual states, each with its spec and the conflicts raised (`DIFFERENCES-IV.md` has the manual text, the code
and the decisions):

| Target (manual pages) | Spec | Conflicts |
|---|---|---|
| Spell MP costs, damage ranges and where each spell may be cast (pp. 48–55 + spell list) | `combat.md` section 9, `spells-noncombat.md` | D4-044, 045, 047, 050–054 |
| Auto-combat spell sets, profile and party strategy (p. 32) | `combat.md` section 9 | D4-046 |
| Combat: 7 × 7 field, MV, actions, encounter options, spoils, night watch (pp. 39–46) | `combat.md` | D4-042, 043, 048, 049 |
| Body parts and wounds, healing potions, inn healing (pp. 34, 45–46) | `combat.md` section 6, `items.md` section 3 | D4-035, 043 |
| Training details: 3 skill picks, Priests' higher levels (p. 27) | `skills.md` section 3, `training.md` | D4-015 |
| Towns: aging, inns, bank, shops, Mystic, shipwright, Distribute (pp. 22–32, 37) | `towns.md` | D4-020, 030…034, 038 |
| Outdoor / water: area maps, swimming, open sea, Navigation (pp. 35–37) | `outdoor.md` | D4-038, 039, 040 |
| Final screen: score and completion code (registration card) | `ending.md` (score 125–225 by ending, code ダック "Duck") | — |

Amounts behind D4-010 / 015 / 019 / 020: no evidence on any platform or in any written source (PC-98 tables and code
identical to X68000); the user set them (`DIFFERENCES-IV.md`). Not raised (manual silent; the code stands): the
advanced-quest unlock (`events.md`: two mini-quests, or average level ≥ 8, or ≥ 6 plus one quest; matches neither the
handbook nor the PC-88 fan page); Magic Power Potion / Ring of Magic Power raise Strength (`items.md`; the catalogue says
MP); the catalogue's spell numbers for three spell potions; potions going bad.

## Facts that settle earlier `[?]`s (`09-trilogy-iv-parentage.md`)

- **35 races `[C]`:** 18 base (I's 15 + Hill Giant, Gargoyle, Spriggan) + 15 hybrids, exactly the parentage doc's list,
  plus `  ?` and Duck. Classes (10) include Gargoyle and Sage; social classes (10) are III's four + the six listed.
- **The MSX Magazine sample's Human Ranger 18/17/16/16/17 is Lord Wood** from the shipped Hall (`FAME`) `[C]`.
- **The Hall ships with three persons** (Lord Wood, Filmon, Nikademus) `[C]`, as magazine B says; 11 slots in all.
- **Deeds lines `[C]`** (P4.X): "Leader of the armies of Scandor who fought Nikademus", "A sage, adviser to Scandor for
  over 75 years", "Attempted to conquer Scandor", "Defeated the Black Lord and defended / failed to defend Gelnor",
  "freed / failed to free Ferronrah from the curse of Nikademus's orb", "Defeated Lord Wood and aided Nikademus …",
  "Defeated Nikademus and saved / failed to save Scandor", "Nothing of note". Hall columns: name, race, class, level,
  social class, origin, the six abilities, Phantasie I / II / III, descendants, deeds, status (alive / dead,
  completed / not).
- **III transfer** caps the count ("N more can be transferred") and reads III's player disk in drive 1 `[C]` (strings).

## The magazines vs the code (D4-001, D4-002)

Code facts behind the two entries (the decisions are in `DIFFERENCES-IV.md`):

1. **Nikademus as an ancestor** (magazine B "very strong descendant of Nikademus" vs fan page D): both codes agree with
   D `[C]`. X68000: with Nikademus (Hall slot 2) as the first ancestor every option ends in "不可能です" (Not possible)
   and creation restarts; as the second ancestor his race (`  ?`) combines with nothing, also "Not possible". MSX2
   segment `1001` checks "first ancestor is Hall slot 2" on both the S/D path (`0x1621`) and the H path (`0x1822`) and
   answers "ふかのうです" (`0x17CC`); his race 33 forms no `combine` pair (`0x171E`). "Sorry, the ancestor you found was
   …" is the S/D race-roll message, not a Nikademus joke (`ancestry.md` sections 3.2, 9).
2. **"With Lord Wood or Filmon as an ancestor you start of noble birth, so levelling up costs no money"** (magazine C,
   1991-07): in both codes a descendant gets Royalty only from a Hall ancestor who is a Super Hero (social 4) and
   Outcast from a Dark Lord (5) (MSX2 `_mkdetail` twin `0x1C11`–`0x1CF3`); Lord Wood (Royalty) and Filmon (Noble) pass
   nothing on, and the social class is otherwise random by race `[C]`. Training cost has no social-class or ancestry
   term, but every level-up pays social^social × L × 30 gold (Noble 810 × L, Royalty the 65 535 cap), so a noble-born
   character's levelling does pay for itself `[~]`. No second MSX2 build is known (the TOSEC `[a]` disk differs only in
   the save-set sectors), so both are magazine-vs-code questions, not platform differences.

The catalogue: every equipment DEF/ATT value matches; its differences are only spelling (`translation-notes.md`).
Technopolis's PC-98 sheet of a Lord Wood descendant who is a Craftsman fits D4-002.

Open RE targets from the secondary sources (`phantasie4-secondary-sources.md` section 11): the item-slot count (8 vs 9
bytes); monster record fields +30–32 `[?]` and +41 / +44 `[~]` (`FORMAT.md` section 6.4). Test data there: PC-98 shop prices =
0.8 × table price; a 627-gold training screen with before → after skills.
