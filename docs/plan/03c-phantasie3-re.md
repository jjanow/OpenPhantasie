# Phase 03c (+04c) — Phantasie III reverse engineering (DOS + Amiga)

Phase 09 T4. The III counterpart of `03-dos-disassembly-and-logic-extraction.md` (logic), `04-amiga-graphics-extraction.md`
(graphics) and `03b-phantasie2-re.md` (II). One file for 03c + 04c. What is left of III overall is on `PLAN.md`
("T4 III leftovers", "T4 III art left", "T4 III RE left").

- **Rules source: DOS** (SSI 1988 `[!]`, `reverse-engineering/dos/original-images/phantasie3/`); **art source: Amiga** v1.0
  (`reverse-engineering/amiga/original-images/phantasie3/`). DOS wins over Amiga on numbers, as for I. The WizardWorks
  1991 DOS disk has the same program and data (`unpacked/README.md`).
- **Source of truth:** the manual, `docs/manuals/phantasie3/phantasie3-manual.pdf` (comparison `09-trilogy-manual-p3.md`).
  Manual ↔ code conflicts go to the user and into `DIFFERENCES-III.md`.
- **Working folders:** `reverse-engineering/dos/phantasie3/` — `unpacked/` (files + README), `extracted/` (JSON tables),
  `FORMAT.md` (format and RE notes, `[C]`/`[~]`/`[?]`, address conventions in its section 1; also covers the Amiga
  program), `FUNCTIONS.md`, `logic-specs/`; `reverse-engineering/amiga/phantasie3/` — `unpacked/` (files + README),
  `FORMAT.md` (Amiga graphics, screens, sounds), `extracted/graphics/`.

## Status

- [x] **3c.1 Container.** DOS: `mtools`, 14 files (+4 WizardWorks batch files; all program/data files identical between
      the two DOS images). Amiga: `xdftool` (amitools), 101 files; the ADF is a played copy (`gamesave.dat`, dungeon
      saves). READMEs with sizes, dates, sha1 and roles.
- [x] **3c.2 Program inventory.** `MAIN.EXE` = MS C 5.x, **EXEPACK** root + **4 MS-LINK overlays** (all load at segment
      `0x13d5`), far data segment `0x1820`; rulebook quiz as copy protection; a hidden character editor. Amiga
      `PhantasieIII` = unpacked Aztec C hunk exe. Helpers: `tools/scripts/extract_dos_p3_common.py` (unpack, overlay
      split, Ghidra-ready MZ), `tools/scripts/extract_amiga_p3_common.py` (hunks, flat image) (`FORMAT.md` section 2).
- [x] **3c.3 Master tables** → `extracted/` via `tools/scripts/extract_dos_p3_tables.py`: the 554-entry string table
      (skills, races, classes, towns, origins, social classes, body states, 181 item names, 57 spell names, messages),
      item points + requirements (ids 0–100, all equal to the manual), item values + town price index (18 entries),
      spells, 80 monsters (38-B records; field names by value match with I `[~]`), I → III class/race maps,
      `diff-vs-p1.json`. Monster and item data files are byte-identical to the Amiga's.
- [x] **3c.4 Character record and transfer** (`FORMAT.md` sections 5–6). 262-B record: stats, HP/MP, age, status, skills
      (value + seed pairs, Fire Bow included), level, score, origin, social class, class, race, **six body areas**, gold,
      XP `[C]`. Worn slots `[C]` shield `+0x68`, armour `+0x6a`, weapon `+0x6c`, **bow `+0x86`**; rank `+0x08`, item
      count `+0x26`, spell count `+0x56` / list `+0xb0` / Divine Aid `+0xf0`; `+0x18` = II score, `+0x54` XP shares,
      `+0x6e` last combat action, `+0x88` unused, `+0x8a` writers, `+0x8c` deed bits. Body-area order Head, L Arm, R Arm,
      Torso, L Leg, R Leg; roster `FAR:0xe820` ×20, party `FAR:0x236` ×7. The shared creation routine ovl2 `0x227d`:
      class kit (4 Small Shield, 22 Robes, 41 Self Bow / 44 Short Bow for Thieves, 64 Mace), first spell (Priest/Ranger
      #1, Monk/Wizard #5), social-class roll and gold (`FORMAT.md` section 5.1). I → III transfer: adds instead of
      erasing, no spells, social class Noble, Undead at level 20, transferred characters start at level 1 (D3-016); built
      as Core `Phantasie3Campaign.AdmitTransferred` / `GameSession.TransferIntoPhantasieIII`. Left: the Amiga converter.
- [x] **3c.5 Function map:** `reverse-engineering/dos/phantasie3/FUNCTIONS.md` from `tools/scripts/fmap_p3.py`
      — root 251 game functions (128 labelled by strings / tables / record fields / hand labels, 123 unlabelled) + 95
      MS C runtime; ovl1 3/1, ovl2 28/1, ovl3 5/8, ovl4 9/18 (labelled / unlabelled); 47 paired with Amiga
      `PhantasieIII` functions by shared strings (only 68 of 786 Amiga functions reference text directly).
- [~] **3c.6 Logic specs** in `reverse-engineering/dos/phantasie3/logic-specs/` (open points are the `[~]` / `[?]` tags
      inside each spec):
      - `body-areas.md`: injury severity, hit-location odds table `DS:0x12cc`, status from areas, restrictions, healing
        points, inns; death step = HP 0 → Dead, no CON loss (Resurrection takes 1–2).
      - `bows.md`: STR+DEX rule, bow slot, Fire Bow chance and damage, Arrow Flame / Accuracy.
      - `combat-actions-and-ranks.md`: action menu rules, Aim, ranks = 30 · (rank − party average), melee to-hit both
        ways. `combat-round.md`: the round order.
      - `training.md` (XP, cost, HP dice, skill picks), `elementals.md`, `endgame.md` (score, final fights in the dark
        fortress, Zeus / Pluto titles).
      - `encounters.md`: groups, status letters S/D/O/K, Greet / Surrender / Mercy / Flee, monsters fleeing,
        reputation = byte 26; post-fight treasure = the lead monster's two items by Find Item (section 4); the
        wilderness encounter root `0xde54` (section 7: area levels `DS:0x9fe`, terrain offsets `DS:0xa17`, night watch,
        sleeping / heard monsters, surprise, the greet-block and free-flee flags).
      - `town.md`: bank, no Trade, no autosave, Mystic, Drop; sale price 75 % of the town price (section 3); armory
        stock never restocks (section 7, D3-033); Transportation = towns 1–14 only (section 8, D3-035).
      - `planes.md`: maps, terrain, hard-coded places, Ocean / Styx, Astral Plane on a party wipe, scrolls; step time
        ⅓ / ⅔ / 1 day and aging on town entry (section 3a).
      - `dungeon-events.md`: all 27 event ops, quest / deed bit writers, Divine Aid, exits; record = letter − 'A' from
        `DS:0x1c84` (not dungeon − 1); the dungeon step ovl4 `0xe14` (section 7a: traps, doors, locks, the chance 10,
        the fight routine `0x1d30`; cell 0 = wall `[C]`); entrance x `DS:0x1bb6 + 2·letter`.
      - Creation tables and draw order: `FORMAT.md` sections 4.3 / 5.1 (Kobold CHA max 3, D3-034).
      Conflicts raised from this work: D3-010…D3-030, D3-033…D3-035 (`DIFFERENCES-III.md`).
- [~] **3c.7 Data and save formats.** `gamesave.dat` = 20 roster records + party block (7 records, 60 found items,
      party XP / gold / days) + 18 globals + 0xcba B of armory state; dungeon saves `DNP<n>` (4224-B map) and `DNG<n>`
      (90 B) (`FORMAT.md` section 8). `WORLD.DAT` 50 × 75 tiles, `PLNS.DAT` Light / Dark 9 × 9 + Netherworld 31 × 16,
      `INITARMS.DAT` 18 towns × 181 stock bytes, `SCROLLS.DAT` 19 × 1050-B texts, `DNGS.DAT` 24 × 30 map + level +
      forced-fight table + exits + event squares / programs + texts (section 9). Extractors
      `extract_dos_p3_{world,towns,scrolls,dungeons,rules}.py` → `GameData/phantasie3/` (section 9.6); spell-level table
      `DS:0x1208` and MaxPow `DS:0xafb`. Event texts: an event square's message byte E[0] names text (E[0] & 0x7f) − 1
      (`dungeon-events.md` section 2.1); exits read only the destination byte (29 / 31 / 41 = planes, all else = back on
      the dungeon square). Left: the Amiga `gamesave.dat` layout (25 roster slots, 11,812 B) is not decoded.
- [~] **3c.8 DOSBox parity checks.** DOSBox-X on a private Xvfb display with III's own conf (scratch game copy, autoexec
      `main`, A: and C: both mounted on it). The installed DOSBox-X has **no debugger**, so memory is read from save
      states (`[dosbox] autosave=4 1`, `-savedir`) with `tools/scripts/p3_dosbox_state.py` (`groups` prints each
      member's HP, status and body areas). Set-ups: edit `GAMESAVE.DAT` (FORMAT section 8) or the scratch `DNGS.DAT` and
      Load Game, or save a state (F12+S), edit its RAM with `tools/scripts/p3_dosbox_poke.py` (monster HP / status / body
      areas, group defence / attack, party status / Attack %, any FAR word) and load it (F12+L, slot 2). Disk swaps:
      rename `TOWN.CMP` on the host (FORMAT section 6). Screens: `screens-s81/`, `-s82/`, `-s109/`, `-s110/`. Done live:
      transfer (D3-016 / D3-007: level 1, Undead 20, XP 100 × level); D3-002 (DIVINE I → no Divine Aid); D3-003 (roster
      10 → 12, nothing erased); D3-004 (II branch cannot succeed); D3-010…D3-013 (menu fallbacks, round array
      `FAR:0x228`); D3-014 (Healing IV: one Gone + one Injury); D3-015 (town inn heals every non-Gone area; the
      wilderness / independent inn's 2 cure points); D3-017 (bow damage words 6 / 44 → 16 / 35, lasting the fight);
      D3-018 (defence words equal the formula: armour raises the monster's chance; God Armour +45 points); D3-019 (attacks on Down
      targets always hit: 14 of 14 monster attacks on Down characters; Thrusts at 31 % hit Okay Skeletons 4 / 18, Down
      ones 18 / 18, unconscious ones 12 / 12, damage `1 + rnd(4)` with no bonus; Out targets too); D3-020 (cap 60);
      damage running sum (Attack second swings 5 and 6 with a 4-point Mace); group-record fields; dungeon events (the
      altar's Torso fault, the wizard's Divine Aid sale and a gold sign-extension, the record letters `DNPA`, exits
      ignoring x, y).
- [x] **T4 campaign:** `Phantasie3Campaign` built on these specs (`09-trilogy.md` T4).
- [~] **4c Graphics (Amiga).** Formats in `reverse-engineering/amiga/phantasie3/FORMAT.md`, extractor
      `tools/scripts/extract_amiga_p3_graphics.py` → `extracted/graphics/`, pack `GameData/phantasie3/graphics/amiga.json`.
      Decoded `[C]` and drawn in the Client unless noted:
      - the `.cmp` / `.csh` packer (two orders, one run code), `.csh` shape sheets, palettes (`& 0x777 << 1`),
        `phantasy.set` (palette, Scandor map, 250 overworld tiles 16×16; party = tile 89); overworld / planes as the
        9×9 tile window, view edges, the view past the map and the planes' / Netherworld's counters (FORMAT section 4);
      - the dungeon map (8×5 `dng.csh` glyphs, map byte → glyph table, seen bitmap = `D/DNGx`); `D/dng*.dat` = factory
        `DNGS.DAT` records; the dungeon event-text page `0x1d9ba` (Core `DungeonTextPage`, section 2.2);
      - monster pictures (record byte 27 → 7 sheets, idle / attacking; Wolf and Mud Elemental have none, D3-067: Wolf
        borrows the Wild Dog's, Mud Elemental the party elemental's); DOS drops the second group for giant pictures as
        the Amiga does;
      - party portraits (race table `0x239a0`, filed by the remaster's race id, drawn whole on taller cards; combat
        keeps portraits, user s99); elemental portrait = picture 22; body-area figure (roster `0x93e0` → `0x9694`,
        section 2.1) on the wilderness / dungeon party cards; text-scroll parchment (`0x1e2c2`, section 2.2); Scroll IV
        = `D/s6`; town picture, title, towns 16–17 in the dim palette `0x23050` (black name, odd-gap names half a cell
        left); dialog buttons; YES / NO are never drawn by the Amiga (its keep questions are OK / CANCEL windows 1 and
        23, section 2.3);
      - `w2` = defeat at the Astral Plane's gates (behind the outcome); the gods' reward scene `0x1ee82` (`w1` / `w0`
        with the party's portraits, the speech on the parchment `0x1f018`; Core `RewardScene`, Client `GodsRewardScene`;
        section 3.1) with its flashes (shapes 37–39 in `flash/`, dungeon palette `0x244de`, `0x1f30a` timing 525 + 1035
        VBLs) and `D/m1` from the god's picture (`0x1efe8`) to the scene's end (`0x1f24a`), pack `reward_scene`
        (section 2.4); undead fate lines in colour 14 on the Astral Plane;
      - the flash as a combat spell effect (section 2.4.1: `0x1833e` / `0x17e16` flash flags, speed word 6 = 21 VBLs a
        flash, combat palette = heros.cmp's; Core `CombatResult.Hits`, Client `CombatFieldRun`, pack `combat_flash`),
        played mid-round per target;
      - sound effects (section 7.2: `0xb126(n)` = driver start `0x1f52` with streams n, n + 1, n + 2 of table
        `0x23e1a`; pack `sound_effects`); `D/m0` / `m1` = three-voice music (the title tune / the reward scene; driver
        and streams in section 7.1, both played);
      - not drawn: `w3` (five 48×48 open-door pieces flashed for 15 ticks on going into a town building, `0xacc8`: the
        remaster has one shared town screen).
      Amiga screens (combat field, orders screen, windows, menus) are in Amiga `FORMAT.md` section 8. 13 Scandor cells
      differ in terrain between DOS and Amiga (Amiga FORMAT section 4; the remaster plays DOS's grid). Emulator: FS-UAE
      with Kickstart 1.3 boots III to its start menu (`tools/scripts/fsuae_run.sh`, `RE-REFERENCE.md`).

## Key facts that shape the RE

1. **III's data files are shared between DOS and Amiga** (`CREATURE.DAT` = `monsinfo.dat`, `INITARMS.DAT` = `inititem.dat`,
   `DNGS.DAT` = the `DNPx` format), with big-endian 16-bit fields. One extractor serves both; the code differs.
2. **III keeps tables where I computed from ids** (item points/requirements). The DOS root has readable strings and one
   master string table, so most handlers can be found by string reference.
3. **Most game state lives in far segment `0x1820`** (`FAR:x`), reached through segment words in DGROUP. Ghidra needs the
   rebuilt MZ (`build_unpacked_mz`) with relocations, and one composite image per overlay.
4. The 262-B character record is new (not I's 282-B one plus extras, as II's was); the transfer remaps class and race ids.

## III's deltas and their specs (`logic-specs/`)

| Area | Evidence | Spec |
|---|---|---|
| **Body areas** (6 per character, O/I/B/G; death by head/torso; limb limits; healing tiers; inn cures) | record +0xf2…+0xfc `[C]`; strings Okay/Injured/Broken/Gone; Amiga `anatomy1/2.csh` | `body-areas.md` |
| **Bows** (slot, Fire Bow skill, STR+DEX requirement, armour less effective, Fire at a rank) | item 41–60 table `[C]`; Fire Bow seed formula `[C]`; bow slot +0x86 `[C]`; "Fires Bow (R1/R2)" | `bows.md` |
| **Aim**, party ranks Front/Middle/Back | "Aims Blow (R1)", "Front/Middle/Back", "(Back)/(Mid)/(Front)" | `combat-actions-and-ranks.md` |
| **Skill training** (3 per level, diminishing) | "[original text]", "Skill: Now: After:" (DS:`0x4d60`); seed words | `training.md` |
| **Social class** (gold at start and per level; endgame titles) | +0x8e; 6 names; Pluto/Zeus text | `FORMAT.md` section 5.1, `training.md`, `endgame.md` |
| **Monster status U/S/D/K, fleeing, reputation** | status names; monster bytes 14/26/33 | `encounters.md` |
| **Spells** Arrow Flame, Accuracy, Divine Aid; Healing cures areas | names `[C]` | `bows.md`, `body-areas.md`, `dungeon-events.md` |
| **Bank fee, Trade, Rename, autosave** | Guild menu strings (Rename, Inspect, Purge…), bank strings | `town.md` |
| **Elementals** Mud/Mist/Air as a 7th member | "You can only add an elemental.", "[original text]" | `elementals.md` |
| **Planes, seas, River Styx, Netherworld** | terrain names; `PLNS.DAT` | `planes.md` |
| **Transfer I → III** | `FORMAT.md` section 6 | `FORMAT.md` section 6; Core `Phantasie3Campaign.Transfer.cs` |
| **Endgame** (Nikademus ×2, Lord Wood, Pluto/Zeus, Darklord/Superhero) | strings, monsters 77–79 | `endgame.md` |

## Manual questions answered

`FORMAT.md` section 7 (transfer platforms and details, divine numbering, social classes, body-area count, Slow, bow #52,
towns and scrolls, Fire Bow bonuses, Vision). Conflicts raised: `DIFFERENCES-III.md` D3-002…D3-007.
