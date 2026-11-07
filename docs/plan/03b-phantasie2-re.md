# Phase 03b (+04b) — Phantasie II reverse engineering (Atari ST)

Phase 09 T3. The II counterpart of `03-dos-disassembly-and-logic-extraction.md` (logic) and
`04-amiga-graphics-extraction.md` (graphics), as `06-implementation-roadmap.md` planned. Those two stay the reference
for I and III; this file covers II only. What is left of II overall is on `PLAN.md` (T3).

- **Rules source:** Atari ST v1.0 (user, s71: the last-released of Apple II / C64 / ST). Images: the v1.0 Pasti set
  `reverse-engineering/atari-st/original-images/phantasie2/` (`SOURCES.txt` one folder up).
- **Source of truth:** the manual, `docs/manuals/phantasie2/phantasie2-manual.pdf` (the combined I + II rulebook; II's
  changes on p.27; full comparison `09-trilogy-manual-p2.md`). Manual ↔ code conflicts go to the user and into
  `DIFFERENCES-II.md` (same rules as `DIFFERENCES.md` for I).
- **Working folder:** `reverse-engineering/atari-st/phantasie2/` — `unpacked/` (files + README), `extracted/` (JSON
  tables), `FORMAT.md` (format notes with `[C]`/`[~]`/`[?]` tags; the address conventions are in its section 1),
  `logic-specs/` (rules), `parity/` (Hatari live logs).

## Status

- [x] **3b.1 Container.** `tools/scripts/stx_to_st.py` reads Pasti; `mtools` reads the FAT12 image. 59 files extracted,
      checked byte for byte against the TOSEC `.st` set (`unpacked/README.md`). Both sets turn out to be played copies.
- [x] **3b.2 Program inventory.** `PHANT.PRG` (game, unpacked DRI C, 155,785 B), `START.PRG` (loader), `UTILIT2.PRG`
      (backup + I↔II transfer utility). No packer, no disk-level protection; a rulebook-quiz exists (`FORMAT.md` section 3).
- [x] **3b.3 Master tables.** Items (181, values identical to I), classes, races, status, 60 spell slots, 80 monsters
      (Amiga I record layout) → `extracted/` via `tools/scripts/extract_st_phantasie2.py`, with a diff against I.
- [x] **3b.4 Character record.** `GUILD.DAT` = 25 × 318 B BE: I's 282-byte record + 36 new bytes (`FORMAT.md` section 5).
- [x] **3b.5 Transfer I → II** decoded statically from `UTILIT2.PRG` (`FORMAT.md` section 6). Conflicts with the manual →
      `DIFFERENCES-II.md` D2-001…D2-003.
- [x] **3b.6 Function map:** `logic-specs/00-function-map.md` (+ `.json`, `tools/scripts/fmap_st_p2_amiga.py`). 393 game
      functions: 276 paired with Amiga I (113 same, 164 changed), 116 new; its section 5 has the rule changes, section 6
      the spec work list. Ghidra set-up: 68000, base 0, text + data as one block, fixups from `st_prg.py`; OS bindings
      `0x19076` XBIOS, `0x19086` BIOS, `0x19096` GEMDOS.
- [x] **3b.7 Logic specs for II's deltas** (`logic-specs/`; the table below lists them). Static decodes that need no
      spec of their own: dungeon cell classes = I's ranges (`0x10b60`, `0x111d6`); creation's `@90 := 1` is the ST's
      own (`0xca3a`); the wall texts (`OUT.RSC` 15 / 17); terrain-fight surprise (full writer list); group compaction on
      every death (`0xb16e`); the Toss slot and DIVINE II / I in the program's FFP (ported as Core `MotorolaFfp`, checked
      by running `PHANT.PRG`'s routines in a 68000 emulator, `tools/scripts/st_p2_ffp_oracle.py`); II swimming
      (`rand / 327`, damage to 6) and lava's shared roll. Rules-profile values read from the code: starting gold 256,
      Guild fills 22 (live), gold clamps 65,535, divine costs 1 / 2 MP, lifespans = DOS factors; sprite sizes are in
      `monsters.json`. D2-042 (wilderness inn time) raised from this work.
- [x] **3b.8 Data and save formats:** `extract_st_p2_{towns,maps,dungeons,scrolls,saves}.py` → `extracted/`,
      `FORMAT.md` section 9. No factory-fresh save files exist; the fresh state is the `MAPS.INT` / `TWNS.INT`
      templates on disk 1 (the game reads them, seen live). File sizes vs Amiga I: `OUT*.DAT` 3000 vs 2500, `DNG*` 2048
      vs ~1983, `TWNS.INT` 13900 vs 13000, `MAPS.INT` 8840 vs 9360.
- [ ] **3b.9 Parity checks in Hatari** (2.6.1, EmuTOS 1.4; harness in `reverse-engineering/atari-st/working/`, with RAM
      peek / poke, roll forcing by breakpoint and quiz suppression, `parity/s82-tools/`). Done live:
      - transfers I → II and II → I byte for byte (`parity/transfer-live.md`); a real I roster converted and opened in
        `PHANT.PRG` shows what Core `TransferIToII` predicts; level-0 training costs 0 gold; the fresh game reads the
        `TWNS.INT` templates (`parity/roster-live.md`);
      - the endgame chain from an edited roster (`parity/endgame-live.md`): EXAMINE → Mystic 100 → DNG8 FEED THE ORB →
        Filmon ending (SCORE 200, wands to OK members, back in Pippacott) → wand "You are improved" (SCORE 250), plus TOO
        TERRIFIED, UNUSABLE and the orb-not-checked quirk; titles go to the first n records, n = OK members;
      - `parity/encounters-endgame-live.md` (set-ups `make_p2_s83.py`): minion 3 and its amulet (a DISSOLVE win counts for
        nothing), ATTACK on the Ice Dragon and its soft-lock through the dungeon save slot (D2-041), the Ice Dragon
        killed (wins nothing), two party defeats (OUT18 wake, sack kept, PIPPACOTT with no survivors, the 5th-defeat
        "run out of Demons" sign), the fate lines of a defeat with survivors (one per member, nothing past the party),
        DNG2 wandering ids, averaged L / reaction, FEAR, the r = 100 night watch (three members: no line, all sleep),
        TOSS with one rank (prompt shown);
      - terrain (`parity/terrain-live.md`), Toss Rock and training (`parity/toss-training-live.md`).
      Left: the combat-line pacing (FORMAT section 7.3) is built but not compared with Hatari (`PLAN.md` T3).
- [x] **4b Graphics extraction:** `extract_st_p2_graphics.py` + `build_st_p2_asset_manifest.py` →
      `extracted/graphics/` (screens, 4 palettes, 80 monsters × 2 poses, 16 portraits × 2, map patterns / icons / 17
      region pictures, dungeon glyphs + compass, small bitmaps, `pack.json` draft); `FORMAT.md` section 10. Live-checked
      in Hatari pixel for pixel (`verify_st_p2_graphics.py`). Wired as `GameData/phantasie2/graphics/atari-st.json` and
      drawn (multi-icon regions, the overworld colour cycle, each picture in its own ST palette, no frame palette; the ST
      has no map legend). Cycle period and scope: `FORMAT.md` section 10.1. Findings: `CROWD.PIC` is 3 animation frames
      of 235×31, not a screen; palettes are four tables (`start`, `town`, `map`, `combat`), combat sprites use `combat`;
      II draws every dungeon / castle icon of a region (Amiga I: one per kind); the overworld colour cycle runs on
      registers 4, 7, 14; text uses the TOS ROM font; the RSC files hold no images; the VDI polygon fill matches live OUT2
      pixel for pixel. Open: `PARTY2`'s role (alternate pose) not seen on a live frame `[~]`.

## Key facts that shape the RE

1. **II on the ST is the I engine plus deltas.** The data tables match I almost everywhere: item values (all 181),
   equipment names, class/race/status lists, 54 of 55 spell names, the monster record layout, the first 282 bytes of the
   character record. So most of I's logic specs (`reverse-engineering/dos/logic-specs/`) apply to II unchanged, and
   only the differences have specs here.
2. **The closest relative is the Amiga I `game`, not DOS I.** Both are 68000 C programs with LINK/UNLK frames, big-endian
   58-byte monster records (DOS: 57 bytes), 25-slot Guild files and the same file set (`mstr1/2.pat`, `party1/2.pat`,
   `maps.int`, `twns.int`, `OUT*.DAT`, `DNG*.SAV`, `mess*`, `scrolls.dtx`). DOS I stays the reference for what I's
   rules are.
3. **The binary is plain compiled C, unpacked, with readable strings** and GEM alerts that name what each code path is
   for. Ghidra's 68000 decompiler handles it well; no unpacking step is needed (unlike DOS I's EXEPACK).
4. **Floating point** (training thresholds and similar) uses the Motorola FFP library in both ST programs; FFP constants
   must be decoded as FFP, not IEEE (`FORMAT.md` section 6).

## II's deltas and their specs (`logic-specs/`)

| Area | Result | Spec |
|---|---|---|
| **Toss Rock** (combat option, any rank, own skill) | hit `rnd ≤ TOSS @42` `[C]`; damage max(0, 12-roll − armor) + `@28` roll, ≥ 1; TOSS grows at level-up from `@62 = 2L + 4·DEX + STR + 10` (+15/+30 DWARF/GNOME); live | `toss-rock.md` |
| **Terrain** (lava, mist, dark voids) | lava = class 8, mist = 11, voids = 7 `[~]`; live; Q10 answered; D2-008, D2-014 | `terrain.md` |
| **Transfer I → II** (and II → I) | `UTILIT2.PRG` (`FORMAT.md` section 6); Core `TransferIToII`; live | `transfer.md` |
| **Origin** (Gelnor / Ferronrah / Netherworld) | creation town > 6 → Netherworld, else Ferronrah; transfer 0 = Gelnor; text only, no rule reads it `[C]` | `endgame.md` section 9 |
| **Spells** DIVINE I / II | II = 1 MP, summons in region 26 (25 % beast V/VI), gained only at the DNG3 pool; I = 2 MP, dungeon advice (message 105), only from Phantasie I; no effect in combat `[C]` static | `magic-divine.md` |
| **Endgame** (orb, Pluto, Filmon, Netherworld) | win = DNG8 feed the orb to the Ice Dragon (op 23 → mode 7), gate SCORE ≥ 100 from the Mystic; Filmon ending, wand of Filmon; defeat as I, survivors wake OUT18 (18,14) `[C]`; chain live; autosave live | `endgame.md` |
| **Amulets** (8) and the orb | minion k kill → amulet k + `@286` flags; EXAMINE → beast rune `@302`; orb DNG5 (16,33) `[C]` static | `endgame.md` sections 3–4 |
| **Towns** (Q11) | same services everywhere; per-town data only; TRANSPORTATION by plane (D2-030); town Inn skips the dead (D2-031); `towns.json` has `plane` and `transport_destinations` | `towns.md` |
| **Monsters / encounters** | 30 new monsters (`diff-vs-p1.json`); builder changed (L / reaction averaged, group fit on sprite height, no Black Knight rules, rnd 0..100); dungeon wandering = r/33 + 12·T + b − 8; D2-019…D2-023; section 9 live | `encounters.md` |
| **Training** | FIGHTER/PRIEST formulas (D2-012), level 0 → factor 1.0 and cost 0, no XP-0 rule, level-up marks every unknown spell ≤ the new level; other four classes as I; D2-013; live | `training.md` |
| **Mouse / GEM UI** | menu bar in `PHANT.RSC` (Town options, Cast spell, Use item, Printout, Show party, Save game) | presentation only; no rules |

## Manual questions answered

From `09-trilogy-manual-p2.md` section 3 (all static, from `UTILIT2.PRG` unless noted):

| Q | Answer |
|---|---|
| 1 "pay" items / gold | Items are removed (every slot) and replaced by SM SHIELD, MACE, LEATHER (worn). Bank gold becomes ⌊(gold ÷ 256) ÷ 35⌋·256 + 100. |
| 2 experience | XP is cut to 0–2,816 by a step table, then raised to the class's training threshold; level set to 0 |
| 3 attributes | STR, INT, DEX, CON, CHA, luck, skills, age, race, class, status, name and roster slot are copied. MP → 5, HP → 0 (low byte cleared), current town → 1 |
| 4 divine spells | In the code, DIVINE II = #57 and DIVINE I = #58 (`PHANT.PRG` table); the transfer keeps #55–#60. The general text's "#58+" leaves out DIVINE II (D2-004) |
| 5 Rangers / Monks | Rangers get HEALING I (#1) like Priests; Monks get FIREFLASH I (#5) like Wizards (D2-002) |
| 6 entry point | ST: the BACKUP UTILITY (`UTILIT2.PRG`) menu "TRANSFER CHARACTERS → FROM PHANTASIE 1 TO PHANTASIE 2"; reads I's data disk `guild.dat` |
| 7 erase / repeat | The 22 character slots of II's `GUILD.DAT` are overwritten; the 3 elemental slots are not converted. No guard against repeating seen `[~]` |
| 8 wand of Nikademus | It does **not** survive on the ST (D2-001) |
| 9 Toss Rock | ST: a GEM button, the 7th action (THRUST, ATTACK, SLASH, LUNGE, CAST, PARRY, TOSS); no REDO on the ST. Any status-OK member, any rank (always asked). Hit if `rand/327 ≤ TOSS`, so TOSS ≥ 100 always hits, TOSS 0 ≈ 1 %. Damage = max(0, ⌊12·r/100⌋ − monster armor) + ⌊r·@28/100⌋, at least 1 (`@28` = town-exit (11·STR + 3·level·\|7 − 2·class\|)/66: STR, level and class, thieves best). No STR/DEX requirement. Start = max(0, 4·DEX + STR − 39), +15 DWARF, +30 GNOME; grows only at level-up. Shown on the sheet as TOSS (`logic-specs/toss-rock.md`) |
| 10 terrain | `logic-specs/terrain.md` section 7 (live) |
| 11 towns | 10 reachable towns (Ferronrah 1–6 PIPPACOTT … FERRON, Netherworld 7–10 BLACK MOOR … DARK ABYSS); STARVILLE and OLYMPIA are I's unreachable leftovers. Start = PIPPACOTT. Every town has the same services (Guild, Armory, Bank, Inn, Mystic, gate); they differ only in stock, restock list, price index and exit. TRANSPORTATION stays on the plane (Ferronrah: the other five; Netherworld: 7↔8, 9↔10) → D2-030; the town Inn no longer refills the dead → D2-031; I's inn decisions repeat → D2-032. Guild = 22 slots + 3 elementals (`logic-specs/towns.md`) |
| 12 scrolls, amulets, orb, win | 14 scrolls (SCROLL A–N) over the file's 19 records (`FORMAT.md` section 9.6); 8 amulets, the orb and the win condition in `endgame.md` |
| 13 monster roster | 80 monsters, 30 new vs I (`extracted/`, `diff-vs-p1.json`) |
| 14 resurrect wording | No wizard can learn RESURRECTION (#44: priest L9, ranger L13), so CB p.10's "wizard" disagrees with the code and the manual's own tables → D2-015 (`magic-divine.md` section 5) |
| 15 autosave | (live) ST: opt-in Town options → "Auto save", off at start; when on, leaving town writes TWNS/GUILD/SACK.DAT. Matches the ST section (CB p.25); the always-on text is Apple's → D2-016 (`endgame.md` section 11) |

Not answered: Q16 (the Demo option is on the Apple II only).
