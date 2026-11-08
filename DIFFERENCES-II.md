# DIFFERENCES-II — Phantasie II manual vs Atari ST game code

Every place where the **Phantasie II manual** (`docs/manuals/phantasie2/phantasie2-manual.pdf`, the combined "Phantasie I
and Phantasie II Rulebook"; II's own changes on p.27; comparison in `docs/plan/09-trilogy-manual-p2.md`) and the
**Atari ST v1.0 game code** (II's rules source, user s71) disagree, what the user decided, and what the remaster does.
Same rules as `DIFFERENCES.md` (Phantasie I): the manual is the source of truth, every conflict goes to the user, who
decides. Keep each entry to **Manual · ST · Decision · Remaster · Side effects** (plus the spec). How a finding was made
belongs in the spec (`reverse-engineering/atari-st/phantasie2/FORMAT.md`, later its `logic-specs/`) or
`docs/plan/SESSION-LOG.md`. IDs are `D2-nnn` so they never clash with I's `D-nnn`.

**Status:** `PENDING` = raised, no decision yet · `DECIDED: <side>` + `built` / `not built` · `NOT A CONFLICT` =
checked, the two agree or the manual is silent.

**Evidence level:** D2-001…004 are confirmed **live** in Hatari + EmuTOS (s76, `reverse-engineering/atari-st/phantasie2/
parity/transfer-live.md`: the converted roster matches the decoded byte table byte for byte). D2-005 onward are static, except D2-007, D2-008 and D2-014, also seen live
(s78, `parity/terrain-live.md`), D2-020 (s80, Guild menu) and D2-041 (s82, `parity/encounters-endgame-live.md` §6).

| ID | Topic | Status |
|---|---|---|
| D2-001 | I → II transfer: the wand of Nikademus is not kept | NOT A CONFLICT (the wand is a score flag and is kept) |
| D2-002 | I → II transfer: Rangers keep HEALING I, Monks keep FIREFLASH I | DECIDED: manual (option `p2.transfer-spells`) · built |
| D2-003 | I → II transfer: bank gold becomes a flat amount (can go up) | DECIDED: ST formula (option `p2.transfer-gold`) · built |
| D2-004 | Divine spell numbers (#58+ vs #55+; DIVINE II is #57) | DECIDED: p.27, #55+ (option `p2.transfer-divine`) · built |
| D2-005 | Towns: 11 towns and "start in Pelnor" vs 10 reachable towns, start in Pippacott | NOT A CONFLICT |
| D2-006 | 16 map sections vs 17 regions | NOT A CONFLICT |
| D2-007 | Lava "almost always" damages vs every step (except digit-0 cells) | NOT A CONFLICT · built |
| D2-008 | Mist "makes travel difficult", Dark Voids "dangerous" vs cheap steps and only a fight roll | NOT A CONFLICT (ST rules) · built |
| D2-009 | Creation tables repeat I's D-001 (Halfling DEX) and D-006 (Monk SLEEP) | DECIDED: manual, as in I · built (II data) |
| D2-010 | Magical pools are gone in II | NOT A CONFLICT |
| D2-011 | Rulebook quiz (copy protection) hangs the game after two wrong answers | DECIDED: not included, as I's D-036 · not built |
| D2-012 | Training: FIGHTER and PRIEST need about a third more XP than in I | NOT A CONFLICT (manual gives no formula) · built |
| D2-013 | Training repeats I's D-050 (cost hidden while XP is short) and D-007 (HP dice vs the chart) | DECIDED: manual, as I (options `p2.training-cost`, `p2.hit-dice`) · built |
| D2-014 | Terrain pictures: orange-wave lava and blue-dot mist vs the ST's blue/red swirl and grey dots | NOT A CONFLICT (ST art) · built (drawn s82) |
| D2-015 | Mortality text: a "high-level wizard" resurrects, but no wizard can learn RESURRECTION | DECIDED: manual, wizard level 13 (option `p2.wizard-resurrection`) · built |
| D2-016 | Autosave on leaving town: always (Apple text) vs opt-in "Auto save" menu item on the ST | NOT A CONFLICT (option `p2.autosave`) · built |
| D2-017 | DIVINE I can't be gained in II; it only arrives by transfer from I | NOT A CONFLICT |
| D2-018 | Quest "free Ferronrah from Nikademus" vs win = feed the orb to Pluto's Ice Dragon; Nikademus never met | NOT A CONFLICT (story framing) |
| D2-019 | Gold cap repeats I's D-048 (65,000 vs 65,535) | DECIDED: manual, as I (option `p2.gold-cap`) · built |
| D2-020 | Guild size repeats I's D-002 (21 / 37 vs 22) | DECIDED: manual 21, as I (option `p2.guild-size`) · built |
| D2-021 | Lifespans repeat I's D-003 | DECIDED: manual bands, as I (option `p2.lifespans`) · built |
| D2-022 | DISPELL UNDEAD repeats I's D-024 (XP divisible by 7) | DECIDED: manual flag list + PLUTO'S UNDEAD SNAKE (option `p2.dispell-undead`) · built |
| D2-023 | FEAR repeats I's D-021 (monsters don't run) | DECIDED: manual, as I (option `p2.fear`); Pluto + beasts stand fast (option `p2.fear-bosses`) · built |
| D2-030 | TRANSPORTATION: "from one town to another" vs only the same plane (Ferronrah 1–6; Netherworld pairs 7↔8, 9↔10) | DECIDED: manual (any town, across planes; ST same-plane as option) · built |
| D2-031 | Town Inn: "hit points and magic restored" vs DEAD members not refilled (I refilled them) | DECIDED: manual (dead refilled; ST skip as option) · built |
| D2-032 | Inns repeat I's D-004 (town Inn takes no time) and D-046 (wilderness inn MP past the table) | DECIDED: manual, as I (inn ~2 weeks, MP never past table; ST as options) · built |
| D2-040 | Path "takes less time" than other terrain (I's D-055 again) vs the ST's 60, as water, mist and lava | **DECIDED: manual, as I (path 30; ST 60 as option)** · built |
| D2-041 | Dungeon re-entry repeats I's D-027 (manual: monsters / locks reset with time; ST: one saved slot, no time; NO keeps an older save) | DECIDED: as I's D-027 (NO forgets the same dungeon's slot; ST as option `p2.dungeon-map-no`) · built |
| D2-042 | Wilderness inn stay repeats I's D-008 (manual: two weeks; ST: +1000 travel time, DOS I: 3.9 days) | DECIDED: manual, as I's D-008 (user s84; ST +1000 time as option) · built (`p2.wild-inn-time`) |
| D2-043 | End-of-combat XP / gold repeats I's D-053 (manual: totals shown, divided on town entry; ST: "[original text]" shares at once) | DECIDED: manual, as I's D-053 (user s84; ST shares as option) · built (`p2.combat-shares`) |
| D2-044 | DIVINE I's deity index 4 (both draws 100): the ST reads past its four-name table and faults; manual silent | **DECIDED** (user s92: show ZEUS) |
| D2-045 | The ST repeats I's DOS side on 29 more shared rules (D-010…D-078 audit, s92 + s95; D-015: the ST already plays I's decision; D-033 differs): II plays I's manual decisions through shared code, with no ST option | **DECIDED** (user s92: I's decisions hold for II; D-033 too) |
| D2-046 | Apple II vs ST: the wilderness is turned a quarter (compass words in scrolls, movement keys, OUT28 castle entrances) | DECIDED |
| D2-047 | Apple II vs ST: five monster numbers (VIPER, FIRE ELEMENTAL, three of Pluto's beasts) | DECIDED |
| D2-048 | Apple II vs ST: DEMONIAC price factor 255 vs 150; SCROLL D / E in IPSWICH and FERRON | DECIDED |
| D2-049 | Apple II vs ST: creation cells (2 agree with the manual / D2-009, 2 ST-only skill cells) and 9 map / 2 dungeon cells | DECIDED |
| D2-050 | Apple II vs ST: text (names, scroll and message wording, menus); the Apple has no spell names | DECIDED |

---

## D2-001 — I → II transfer: the wand of Nikademus is not kept

- **Manual:** "Your characters must pay all items except the wand of Nikademus." (p.27 Updated Rules; same in the Apple
  and C64 sections and the readme card.)
- **ST:** the transfer in `UTILIT2.PRG` (converter `0x0edc`, mode 2) empties item slots 4–9 and overwrites slots 1–3 and
  the worn items with SM SHIELD (4), MACE (49) and LEATHER (23) for every character. No item is carried over, the wand
  (I item 150) included. In II, item 150 is `---ORB---` and 149 is `FLMN WAND`, so even a copied id would not be the wand.
- **Later games:** Apple III turns the flag into a wand bit and tests it in dungeon scripts ("YOU DO NOT HAVE THE WAND, STUPID"); DOS III drops it → `DIFFERENCES-III.md` D3-001. T5's II → III transfer must carry the flag.
- **Decision:** (user s76: "see how the other versions handle this") NOT A CONFLICT. No version of II has a wand item, and every version (ST, Apple, C64, Atari 8-bit, PC-88, PC-98) clears all items. II represents the wand as a **flag**: a character whose Phantasie I score (ST word @70) is over 99 shows "- WAND OF NIK -" (`PHANT.PRG 0x3a16`) and the Mystic adds "heroes of Gelnor" (`0xd7e4`). The transfer copies that score unchanged, so the wand survives as the manual says. In II it does nothing else `[~]` (Apple labels it "WAND OF NIK(USELESS NOW)").
- **Remaster:** the converter keeps the I score; "has the wand" = score > 99. No option.
- **Side effects:** the free starter kit (shield, mace, leather) is not in the manual either; it is part of the same
  rule. Phantasie III's manual also has the wand surviving a transfer (into III), which may matter for T5 party transfer.
- **Spec:** `reverse-engineering/atari-st/phantasie2/FORMAT.md` §6.

## D2-002 — I → II transfer: Rangers keep HEALING I, Monks keep FIREFLASH I

- **Manual:** characters lose "all spells except for any divine spells (#55 and above). Wizards will keep spell #5 and
  Priests spell #1." (p.27; readme.)
- **ST:** all spell flags for #1–#54 are cleared, then HEALING I (#1) is set for **Rangers and Priests** and FIREFLASH I
  (#5) for **Monks and Wizards** (class bytes 2/4 and 1/5). #55–#60 are kept. The flags are set whether or not the
  character knew the spell in I.
- **Decision:** (user s76) the ST rule is fine unless the manual contradicts it. p.27 read strictly does ("all spells except" the divine ones, with only Wizards and Priests named), so the manual wins by default. Every decoded version (Apple, C64, Atari 8-bit, PC-88, PC-98, Apple III) does what the ST does, so the ST rule is the original design.
- **Remaster:** rules option, manual default (Manual and Remaster presets: only Wizards #5, Priests #1); the Original (ST) preset gives Rangers HEALING I and Monks FIREFLASH I.
- **Side effects:** none beyond the two classes (in I, Rangers and Monks learn these spells at level 1 anyway).
- **Spec:** `FORMAT.md` §6.

## D2-003 — I → II transfer: bank gold becomes a flat amount

- **Manual:** "They must also pay the majority of their money." (p.27; readme.)
- **ST:** the bank gold word becomes ⌊(old ÷ 256) ÷ 35⌋ · 256 + 100: anyone with up to 8,959 GP ends with exactly 100 GP,
  the most anyone keeps is 1,892 GP. So a character with less than 100 GP **gains** gold, and one with 100–199 GP keeps
  half or more. Above ~200 GP the manual's "majority" holds.
- **Decision:** (user s76) the ST formula, "weird but fine". Apple, C64, PC-88 and PC-98 use the same formula; Atari 8-bit has a bug that gives everyone one flat amount (≈868 GP `[~]`).
- **Remaster:** rules option: the ST formula in every preset. Second value `no-gain` (the formula, but never more than the character had) closer to the manual's "pay the majority": kept as a player-selectable value, in no preset (user s77).
- **Side effects:** the shared sack / carried gold is not in the Guild record; the transfer writes II's `SACK.DAT` from a
  template, so found gold is lost too `[~]`.
- **Spec:** `FORMAT.md` §6.

## D2-004 — Divine spell numbers

- **Manual:** the general rules say "DIVINE SPELLS (#58+) may only be acquired in the course of the game" (p.14); the II
  transfer text says "divine spells (#55 and above)" (p.27). The manual contradicts itself.
- **ST:** the spell-name table (`PHANT.PRG` data+`0x5c4`, slots 0–59) has **DIVINE II at #57** and **DIVINE I at #58**
  (#55, #56, #59, #60 empty). So "#58+" leaves out DIVINE II; "#55 and above" covers both, and the transfer code keeps
  exactly #55–#60.
- **Decision:** (user s76) p.27: divine spells are #55 and above (both DIVINE I #58 and DIVINE II #57 survive), which is also what the ST transfer keeps. Facts from `PHANT.PRG`: DIVINE I works only in a dungeon (a deity's advice); DIVINE II only in wilderness region 26 (a forced fight, 25 % one of Pluto's dragons).
- **Remaster:** rules option: #55+ in every preset; `p14` (#58+) as the alternative.
- **Side effects:** I's DOS table has DIVINE I at #58 and nothing at #57, so "#58+" is right for I; the p.14 text is shared
  I/II text.
- **Spec:** `FORMAT.md` §3, §6.

## D2-005 — Towns: 11 towns and "start in Pelnor"

- **Manual:** the shared Towns text: "START IN PELNOR … one of the 11 towns of Gelnor"; "Pelnar is Town #1".
- **ST:** `TWNS.INT` holds 12 town records; 10 are reachable on II's map (PIPPACOTT, HASLEMERE, SAXLINGHAM, LLITHFAEN,
  IPSWICH, FERRON, BLACK MOOR, DEATHPORT, DEMONIAC, DARK ABYSS). STARVILLE and OLYMPIA are byte copies of I's records with
  no map cell. Town 1 = PIPPACOTT (the transfer's and probably a new game's start `[~]`).
- **Decision:** (user s77) NOT A CONFLICT: the text is I's, shared in the combined rulebook; II's towns are undocumented.
- **Remaster:** II's code: 10 reachable towns, start in PIPPACOTT; STARVILLE and OLYMPIA stay unreachable (built).
- **Spec:** `FORMAT.md` §9.

## D2-006 — 16 map sections vs 17 regions

- **Manual:** 16 map sections.
- **ST:** `MAPS.INT` = 17 × 520 B starting grids (regions 1, 2, 3, 5, 6, 7, 8, 11, 12, 17, 18, 22, 26, 27, 28, 30, 31).
- **Decision:** (user s77) NOT A CONFLICT: the 16-sections text is I's (shared rulebook; same reasoning as I's D-071).
- **Remaster:** loads all 17 regions, as the ST (built).
- **Spec:** `FORMAT.md` §9.

## D2-007 — Lava damage

- **Manual:** lava "almost always causes damage" (p.27).
- **ST:** every living member takes 1 + ⌊r·d/10⌋ on every lava step (r 0–100, d = the cell's last digit). A digit-0
  lava cell does nothing; lava has no encounters. Probably consistent if d = 0 cells are the "almost" `[~]`.
- **Decision:** (user s77) NOT A CONFLICT: the digit-0 cells are the "almost".
- **Remaster:** the ST formula as is: `rules.json` `terrain` class 8 `lava`, `Lava.Resolve` (built s79; `terrain.md` §10).
- **Spec:** `logic-specs/00-function-map.md` §5.3, `FORMAT.md` §9.

## D2-008 — Mist and Dark Voids

- **Manual:** mist "makes travelling difficult" and "undead and insect encounters abound"; Dark Voids are "dangerous to
  travel through" and "undead and insects flourish" (p.27).
- **ST:** two terrain classes run one shared code path (`PHANT.PRG` `0x6338`):
  - **Cost:** 60 per step, the cheapest rate (grassland 75, mountains 300).
  - **Damage:** none.
  - **Fights:** no 6 % random-encounter roll. Instead, while a per-trip flag is clear, each step has a ≈ 9 % chance
    (r ≤ 8) of a fight. The lead monster is one of three that depend on the cell's digit (undead/devils plus one
    insect). The fight sets the flag. The flag is cleared only on leaving a town, on dungeon → wilderness and on a
    party wipe. After the first fight, these cells have **no** encounters until then.
- **Which cells (evidence for the decision, s78; static + live):** **Mist = class 11** (tiles 111/112/113, 372 cells
  + 7 text cells 116) and **Dark Voids = class 7** (tiles 72/73/74, 1035 cells + 2 text cells 76) `[~]`.
  - **How the classes were matched:** the ST map polygons are filled with pattern = class (`0x545a`, patterns at data
    `0x21dd6`, checked on all 17 maps). Pattern 11 is sparse dots and pattern 7 is solid black. The manual calls mist
    "an area of … dots" and voids "dark areas".
  - **Live (Hatari, `parity/terrain-live.md`):** OUT17 un-fogged shows the 112 block round DEATHPORT as grey dots and
    the 72 area as black. A step on 112 gave a WIGHT fight with ZOMBIE and GHOUL ranks (RAM: monster 26, flag 1, time
    +60 a step). After Flee, 40 mist steps and 40 void steps had no fight.
  - **Regions:** mist in OUT17, 18, 22, 26, 27 and 28; voids in OUT17, 18, 22, 26, 27, 28 and 31.
  - **Caveats:** no game text names either class. The ST dots are grey, not the manual's blue (see D2-014).
- **Decision:** (user s79) NOT A CONFLICT: the manual's "difficult" / "dangerous" gives no numbers; the ST rules apply.
- **Remaster:** II's terrain rules as the ST (`terrain.md` §5–§7): cost 60, no damage, one undead/insect fight per trip.
  Built s79: `rules.json` `terrain` classes 7 and 11 `terrain_fight`, played by `OverworldTraversal` (`terrain.md` §10).
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/terrain.md` §3, §5–§7; `FORMAT.md` §9.3.

## D2-009 — Creation tables repeat D-001 and D-006

- **Manual:** the same creation text as I (Halfling maximum DEX, Monk spells).
- **ST:** `TWNS.INT`'s creation tables are byte-identical to Amiga I, so they carry the values behind I's D-001 (Halfling
  DEX) and D-006 (Monk SLEEP at level 7).
- **Decision:** same manual text, same code, so I's decisions (manual) carry over. Change here if II should differ.
- **Remaster:** `GameData/phantasie2/creation-tables.json` carries I's decided values (HALFLING DEX 12 / 20, MONK spell 41
  not at level 7), applied by `tools/scripts/build_p2_gamedata.py` (s78). The raw ST values stay in `extracted/`.

## D2-010 — Magical pools are gone

- **Manual:** I's pool text is in the combined rulebook's shared part; II's p.27 terrain text doesn't mention pools.
- **ST:** I's pool terrain class became a special-encounter terrain; I's pool effect ("%s IS CHANGED") has no II code.
- **Decision:** (user s77) NOT A CONFLICT: the pools are I's island; II has none.
- **Remaster:** no pools in II, as the ST. Idea for later: pools in II as a cross-game feature option (T5).
- **Spec:** `00-function-map.md` §5.3.

## D2-011 — Rulebook quiz

- **Manual:** never mentions it.
- **ST:** `rulebook_quiz 0x19d42` asks rulebook questions once per run, on the 3rd–8th dungeon-loop entry; two wrong
  answers put the game into an endless loop.
- **Decision:** copy protection, as I's D-036 (user s35: not included); the same decision applies.
- **Remaster:** nothing.
- **Spec:** `00-function-map.md` §5.5.

## D2-012 — Training XP for FIGHTER and PRIEST

- **Manual:** gives no training formula; says nothing changed.
- **ST:** II's Guild training (`0x0cdde`) uses FIGHTER (2·STR + DEX)/33.0 and PRIEST (STR + 2·INT)/36.3 (I: (STR + DEX)/16.5,
  (STR + INT)/18.1): about a third more XP at equal stats. At level 0 the level factor is 1.0.
- **Live (s78):** FIGHTER STR 22 DEX 15 level 1 → "You need 549" (I's formula: 435), `parity/toss-training-live.md`.
- **Decision:** NOT A CONFLICT (a number the manual doesn't state; II's own numbers apply).
- **Remaster:** II's rules profile takes the ST numbers (`rules.json` `training`, `TrainingRules`; built s79, `training.md` §4).
- **Spec:** `00-function-map.md` §5.1.

## D2-013 — Training repeats I's D-050 and D-007

- **Manual:** the shared training text (CB pp.4–5, I's text): "you will be told how many experience points are required
  and how much the training will cost"; the "Hit Points by Class and Level" chart (CB p.11). II's p.27 changes neither.
- **ST:** the same code as I. While XP is short the Guild shows only "You need N more experience points" [GO AND LEARN],
  no cost (`confirm_cost 0x017e4`; live s78). Level-up re-rolls `(hp_factor · (CON + 5)) >> 4` d3 and keeps the old
  maximum + 1 on a lower roll (`level_up 0x0c71a`, as I's DOS/Amiga), so HP stays below the chart, as in I.
- **Decision:** (user s79) manual, as in I: D-050 (show the cost too) and D-007 (the chart HP formula).
- **Remaster:** II's training uses I's decided rules. Built s79 (`training.md` §4) as two rules options:
  - `p2.training-cost`: `manual` shows the cost with "You need N"; `st` hides it.
  - `p2.hit-dice`: `manual` is `factor·(CON + 5)/15`; `st` is `>> 4`.
  - Both are `manual` in the Original (manual) and Remaster presets and `st` in Original (Atari ST).
  - II's own level-up deltas (`training.md` §2: spell marks, HP tie) are data, not options.
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/training.md` §1, §3.

## D2-014 — How lava and mist look

- **Manual:** lava "appears as an orange wave pattern"; mist "appears as an area of blue dots" (p.27).
- **ST:** lava (class 8) is drawn as a dense blue / red / grey / green swirl and mist (class 11, `[~]`, D2-008) as sparse
  grey dots on black. Dark voids (class 7) are solid black, which agrees with "dark areas". The only orange pattern
  on the ST map is class 9 dashes, which is ordinary land (6 % random encounters). Seen in Hatari with EmuTOS
  (`parity/shots/p2-out17-terrain.png`, `p2-fill-patterns.png`). That real TOS gives the same colours is `[~]`.
- The rulebook covers several platforms, so the manual's pictures likely describe another version `[~]`.
- **Decision:** (user s79) NOT A CONFLICT: the manual's colours describe another platform; the ST patterns are shown.
- **Remaster:** II's graphics pack is the ST's (`GameData/phantasie2/graphics/atari-st.json`, the default and only II
  pack; the same file as the extract's `pack.json`). Its region pictures are the ST's own polygon fills, and the ST
  fill patterns resolve as `overworld.pattern.N` (pattern = cell class: 7 dark voids, 8 lava, 11 mist). Built s81 (pack
  wired); s82 the Client draws II's overworld with the ST pictures, every dungeon / castle icon and the ST's water /
  lava colour cycle. The ST has no on-screen legend, so the remaster shows none (the patterns stay available for a
  later QoL legend).
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/terrain.md` §5.

## D2-015 — Mortality text: "a high-level wizard using a RESURRECT spell"

- **Manual:** "When they reach 0, the character is dead (although he or she might be brought back to life by a high-level
  **wizard** using a RESURRECT spell)" (CB p.10 / PDF 7). I's manual says "priest" (P1 p.13). The same rulebook's spell
  tables give RESURRECTION (#44) to priests and rangers only (CB p.14 / PDF 9).
- **ST:** the class/level table (`TWNS.INT` +0x5a, byte-identical to I) gives slot 43 (#44) to PRIEST at level 9 and
  RANGER at level 13; WIZARD 0 (never). The Guild cannot teach it outside that table. So no wizard can cast RESURRECTION.
  The manual's own tables agree with the code; only the p.10 sentence differs (probably a misprint, as `09-trilogy-manual-p2.md`
  guessed).
- **Decision:** (user s79) manual: wizards can learn RESURRECTION, at level 13 (as the ranger; the manual says only "high-level").
- **Remaster:** II's class/level table gets WIZARD slot 43 = 13 (`creation-tables.json`, set by
  `tools/scripts/build_p2_gamedata.py`). The ST's 0 is in its `dos_tables`, played by the rules option
  `p2.wizard-resurrection` = `st` (Original (Atari ST) preset; `manual` in the other two). Built s79.
- **Side effects:** text only if the code wins; if the manual's sentence were followed, wizards would need a spell-table
  entry the manual's own tables don't have.
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/magic-divine.md` §5 (manual Q14).

## D2-016 — Autosave when leaving a town

- **Manual:** Apple section: "When you leave a town, your characters are automatically saved onto the game disk" (CB p.19 /
  PDF 12). Atari ST section: "To save a game, you must be in a town. Click Save under the Town Menu. You may also click on
  Auto Save to save the game each time you leave town" (CB p.25). Mac: "Auto Save" (CB p.26).
- **ST:** Town options has an **Auto save** item. It is off at start (`PHANT.PRG` word `0x1f6ea` = 0) and toggled with a
  check mark (`0x28ac`; seen live in Hatari, `parity/shots/p2-autosave-*.png`). When it is on, leaving a town
  ("LEAVING %s", `0x2856`) writes `twns.dat`, `guild.dat` and `sack.dat`, the same as Save game. When it is off, nothing is
  written on leaving.
- **Decision:** (user s79) NOT A CONFLICT: the ST matches its own section (opt-in "Auto save", off at start).
- **Remaster:** II gets the ST's Auto save toggle, the same Town options item I already has (Client
  `ClientSettings.AutoSave`, off at start). Built s79 as the rules option `p2.autosave`:
  - `toggle` (the ST, every preset) lets the toggle decide.
  - `always` (the Apple text) saves on every town exit; a player choice in no preset.
  - The Client's `LeaveTown` reads `GameOptions.AutoSaveAlways`.
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/endgame.md` §11 (manual Q15).

## D2-017 — DIVINE I cannot be gained in Phantasie II

- **Manual:** "DIVINE SPELLS (#58+) may only be acquired in the course of the game for special purposes" (CB p.14); the
  transfer keeps divine spells (p.27). The rulebook does not say which game "the game" is.
- **ST:** DIVINE I (#58, slot 57) has no source in II. No dungeon script sets it, the Guild cannot teach it, and no level-up
  or item grants it. It only arrives on characters who won Phantasie I (Zeus' grant) and were transferred. In II it costs
  2 MP and, in a dungeon, shows a deity and the level's hint message 105. DIVINE II (#57) is gained in II, at the DNG3 pool.
- **Decision:** (user s79) NOT A CONFLICT: "the game" is I; II has no source for DIVINE I.
- **Remaster:** as the ST: DIVINE I only on characters transferred from I (built: the I → II transfer keeps it,
  `p2.transfer-divine`, D2-004).
- **Side effects:** a II-only party never has DIVINE I, so it never gets the dungeon hints. If the remaster added a II
  source, it would be new content.
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/magic-divine.md` §3.

## D2-018 — How Phantasie II is won

- **Manual:** the story: "I shall free Ferronrah from Nikademus's evil power" (CB p.2); "amulets and an enchanted orb are the
  important artifacts" (p.27). There is no endgame text.
- **ST:**
  - The game is won by feeding the orb (the vessel of Nikademus's curse, scroll A) to Pluto's Ice Dragon in DNG8. The
    dragon allows this only once a Mystic visit has given SCORE 100 (orb carried + 8 beast runes from Pluto's minions).
  - The orb is not checked at the feeding.
  - Then comes Filmon's ending ("the curse of Nikademus will slowly fade"; Nikademus marches on the northern continent, a
    lead-in to III). Nikademus is never met, and the game continues afterwards.
- **Decision:** (user s79) NOT A CONFLICT: story framing, as I's Black Lord.
- **Remaster:** II's endgame as the ST (`endgame.md`) (built: `Phantasie2Campaign.ResolveEnding`).
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/endgame.md` §0, §6–7.

## D2-019 — Gold cap repeats D-048

- **Manual:** the shared general rules (CB p.6, I's text): a party carries up to 65,000 GP; one bank account holds up to
  65,000 GP.
- **ST:** both stop at **65,535**.
  - Party gold (`0x25eb4`) goes through the clamp `0x0dfa4` on a bank withdrawal (`0x0df74`), an item sale (`0x0eb44`)
    and a dungeon gold script (`0x11d98`), and is clamped at the combat victory (`0x0a43e`).
  - The bank `@262` is clamped when found gold is split on town entry (`0x0bef8`).
  - The gold entry box (`0x01272`) refuses amounts over 65,535.
  - This is DOS I's behaviour (D-048). Static, not run.
- **Decision (user s80):** manual, as I (65,000); the ST's 65,535 as a rules option. I's was the same (option `p1.gold-cap`).
- **Remaster:** `GameData/phantasie2/rules.json` plays the manual's 65,000. Built s81 as the rules option `p2.gold-cap`:
  `manual` (Original (manual) and Remaster) or `st` (Original (Atari ST); the 16-bit clamp 65,535 for the purse, every
  account and the found gold, `GoldCapRule.Dos`). I's QoL `lifted` value is not offered for II (no decision).
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/encounters.md` §8.

## D2-020 — Guild size repeats D-002

- **Manual:** "A maximum of 21 characters can be generated" (C64 section, CB p.20); "As many as 37 characters" (Apple /
  general text, CB p.5). The ST section names no number.
- **ST:** 22 character slots (`TWNS.INT` Guild template).
  - The Guild menu enables "New member" while any slot has `@92` = 0 (`guild_menu 0x003ea` → `0x0ebf6`).
  - N)EW takes the first free slot (`create_character 0x0c93a`), so all **22** can be filled.
  - DOS I behaves the same (D-002).
  - Seen live (s80): "New member" is greyed with 22 occupied slots and enabled with 21
    (`parity/shots/p2-guild-menu-22-full.png`, `p2-guild-menu-21.png`).
- **Decision (user s80):** manual, as I (21); the ST's 22 as a rules option. I's was the same (option `p1.guild-size`).
- **Remaster:** `rules.json` keeps `guild_max_characters` 21; `guild_by_platform.atari-st` records slots 22,
  max_characters 22. Built s81 as the rules option `p2.guild-size`: `manual` 21 (Original (manual) and Remaster) or `st`
  22 (Original (Atari ST)), read from `guild_by_platform` under `rules.json` `original_platform` = `atari-st`. The
  every-game QoL `guild-limit` = `none` still overrides both.
- **Side effect (s128, I → II transfer):** the transfer converts the ST's 22 slots, so under `manual` (21) the Guild
  can hold 22 and N)EW is refused until a purge. **Decided (user s129): keep it** — all 22 come across, N)EW stays
  refused until a purge, as a DOS save import into I behaves (D-002).
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/encounters.md` §8.

## D2-021 — Lifespans repeat D-003

- **Manual:** the shared creation text (I's): elves about 3000 years, dwarves and gnomes about 200, humans about 80, the
  others 40–50.
- **ST:** DOS I's rule, unchanged (`create_character 0x0cc84..0x0cd0a`, jump table `0x22976`).
  - Lifespan `@252` = `@10` × factor. `@10` = 8..13, and 14 when rand ≥ 32766.
  - Factors: HUMAN and PIXIE ×1; DWARF, HALFLING and GNOME ×5/2; ELF ×19; all others ×2/5, UNDEAD included.
  - Town entry (`town_aging 0x0bba6`): past 7/10 of `@252` the character "has aged" once (stats ×4/5); past `@252` the
    character "has died" (stats zeroed).
  - So a human dies at age about 88–137, as in DOS.
- **Decision (user s80):** manual, as I (bands ±10 %); the ST factors as a rules option. I's was the same (option `p1.lifespans`).
- **Remaster:** `GameData/phantasie2/creation-tables.json` has I's `lifespan_bands` (manual ±10 %) and the ST factors in
  `dos_tables.lifespan_factors` (DOS I's). Built s81 as the rules option `p2.lifespans`: `manual` (Original (manual) and
  Remaster) or `st` (Original (Atari ST); `@252 = @10 × factor`, aged and died checked as `@264 >> 8`).
- **Side effects:** II adds race 15 UNDEAD (transfer / elementals only). Under the manual's text it is one of "the others".
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/encounters.md` §8.

## D2-022 — DISPELL UNDEAD repeats D-024

- **Manual:** DISPELL UNDEAD affects "skeletons, zombies, ghosts, and other creatures who have returned from the grave"
  (shared spell text, I's).
- **ST:** "undead" = the front group's XP divisible by 7 (combat case `0x842c`), DOS's test.
  - On II's table it hits SKELETON, BANTIR, ZOMBIE, GHOUL, LARGE SKELETON, WIGHT, WRAITH, DEMON, VAMPIRE, GHOST,
    HIGH DEMON and SPIRIT. That is I's list: BANTIR, DEMON and HIGH DEMON wrongly in.
  - It misses PLUTO'S UNDEAD SNAKE (72), new in II.
- **Decision (user s80):** manual, as I (a data flag, `monster-traits.json`), and PLUTO'S UNDEAD SNAKE (72) **is** undead;
  the ST's XP-divisible-by-7 test as a rules option.
- **Remaster:** `GameData/phantasie2/monster-traits.json`: `undead` = I's nine + PLUTO'S UNDEAD SNAKE (72);
  `never_flees` empty. Built s81 as the rules option `p2.dispell-undead`: `manual` (the flag; Original (manual) and
  Remaster) or `st` (XP divisible by 7; Original (Atari ST)).
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/encounters.md` §6.

## D2-023 — FEAR repeats D-021

- **Manual:** FEAR "frightens the monsters into running away in terror" (shared spell text, I's).
- **ST:** on r < 5·(caster level − L) + 50 the encounter level drops by 6 (combat case `0x8326`). Nothing runs, as in DOS.
  - L is the **average** group level on the ST (`encounters.md` §3), so the roll succeeds more often against
    several groups than in DOS.
- **Decision (user s80):** manual, as I (monsters run); the ST's level drop as a rules option. I's was the same (`never_flees` for I's Black Lord). II has no Black
  Lord. Follow-up (user s81): Pluto (79), PLUTO'S ICE DRAGON (78) and Pluto's eight minions (17, 43, 72–77) stand fast
  (`monster-traits.json` `never_flees`, as I's Black Lord); option `p2.fear-bosses` = `flee` lets them run.
- **Remaster:** built s81 as the rules option `p2.fear`: `manual` (the monsters run; Original (manual) and Remaster) or
  `st` (level drop only; Original (Atari ST)). II's `monster-traits.json` `never_flees` lists Pluto, the Ice Dragon
  and the eight minions (the s81 follow-up above), so under `manual` they stand fast; `p2.fear-bosses` = `flee` lets them run.
  The ST's averaged L (above) belongs to II's encounter builder.
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/encounters.md` §6.

## D2-030 — TRANSPORTATION only reaches towns on the same plane

- **Manual:** "TRANSPORTATION (#54) inter-dimensionally transports the party from one town to another" (MAGIC section,
  spell descriptions); spell list: 2 MP, town only. No limit is named.
- **ST:** the picker (`PHANT.PRG` `0x23ee`, called from Town → Cast spell `0xd94c`) depends on the current town:
  - from a Ferronrah town (1–6: PIPPACOTT, HASLEMERE, SAXLINGHAM, LLITHFAEN, IPSWICH, FERRON) it lists the other five;
  - from a Netherworld town it offers one town only: BLACK MOOR ↔ DEATHPORT, DEMONIAC ↔ DARK ABYSS.
  - So the spell never crosses between Ferronrah and the Netherworld. Phantasie I lists every other town.
- **Decision (user s81):** manual: TRANSPORTATION lists every other town, across Ferronrah and the Netherworld. The ST's same-plane lists (`transport_destinations`) as a `p2.*` rules option.
- **Remaster:** built (s81). TRANSPORTATION lists towns 1–10 but the current one (`Phantasie2Campaign.TransportTowns`); `p2.transport-list` = `st` (Original preset) uses the current town's `transport_destinations` from `towns.json`.
- **Side effects:** with the manual's reading, one cast would carry a party into or out of the Netherworld, skipping the
  vortex routes and the one-way town exits (BLACK MOOR → OUT6, DEMONIAC → OUT18).
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/towns.md` §4.

## D2-031 — Town Inn does not restore DEAD members

- **Manual:** "INN is where characters can have their hit points and magic restored" (Town Menu, INN). Inns outside towns
  work "like the inns in town". No exception for the dead.
- **ST:** the town Inn (`0xe5fc`) refills HP and MP only for members whose status is not DEAD. A DEAD member gets "IS
  RESTING IN PEACE" and keeps the HP / MP it had. SLEEP members are refilled. Phantasie I's town Inn refills everyone,
  DEAD too (status unchanged). The wilderness inn skips DEAD members in both games (I's D-047).
- **Decision (user s81):** manual: the town Inn refills HP / MP for every member, DEAD too (status unchanged), as I's town Inn. The ST's "IS RESTING IN PEACE" skip as a `p2.*` rules option.
- **Remaster:** built (s81). The town Inn refills every member; `p2.inn-dead` = `st` (Original preset) leaves DEAD members' HP / MP as they were (`TownMenus.Rest skipDead`).
- **Side effects:** only a dead member's stored HP / MP, which matter once the member is resurrected.
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/towns.md` §5.

## D2-032 — Inns repeat I's D-004 and D-046

- **Manual:** a town Inn stay lasts about two weeks; the MP maximum can rise after resting, up to the "Maximum Magic
  Points" table.
- **ST:** as Phantasie I. The town Inn takes no time and ages nobody (I's D-004). The wilderness inn's MP roll succeeds
  on `roll ≤ gap·20`, so at gap 0 a member has a 1 % chance per stay to go one past the table (I's D-046; `0xf864`). The
  town Inn uses `<`, as I.
- **Decision (user s81):** manual, as I: a town Inn stay takes about two weeks (aging); the wilderness inn never raises MP past the table. The ST behaviour as `p2.*` rules options (mirroring `p1.town-inn-time`, `p1.inn-magic-cap`).
- **Remaster:** built (s81). The town Inn takes two weeks (`p2.town-inn-time`, `st` = no time) and the wilderness inn stops at the table (`p2.inn-magic-cap`, `st` = the `≤` roll); same code as I's options (`GameOptions.TownInnNoTime` / `InnMagicPastTable`).
- **Side effects:** as I's D-004 / D-046.
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/towns.md` §2, §5.

## D2-040 — Path travel time (I's D-055 again)

- **Manual:** the terrain legend in the rulebook's **Apple II** section: Path, "Travelling here is easy (passing through
  takes less time relative to the other types of terrain)" (CB p.18 / PDF 11). This is the same text as I's terrain key
  (P1 p.25), which D-055 follows. The **Atari ST** section ("The wilderness terrain will appear as follows", CB p.24 /
  PDF 15) shows only the pictures, with no text. No other part of the rulebook gives travel times.
- **ST:** a step costs `(class + 1)·75` for classes 0–3 and 60 for every other class (`PHANT.PRG` `0x612c` → `0xef62`,
  `terrain.md` §2 step 4, live). So the path (class 6) costs 60, the same as water, lava, mist and dark voids. It is
  cheaper than land (75–300) but not cheaper than every other terrain. This is the same code as DOS I (D-055).
- **Decision (user s81):** manual, as I (D-055): the path costs 30, less than every other terrain. The ST's 60 as `p2.path-time`.
- **Remaster:** built (s81). `GameData/phantasie2/travel-time.json` charges the path 30; `p2.path-time` = `st` (Original preset) plays the ST table (path 60, `TravelTimeRules.Dos`).
- **Side effects:** none beyond travel time (aging on town entry). Mist (D2-008) shares the 60.
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/terrain.md` §2, §10.

## D2-041 — Dungeon re-entry repeats I's D-027

- **Manual:** the combined rulebook keeps I's text (PDF p.7, Dungeons, "Playing Hint"): on the second entry "the
  monsters may be prepared for you - new locks installed and new monsters recruited … if you return soon after you left,
  the monsters you killed might still be dead", and leaving a dungeon asks "if you want to save its current status".
- **ST:** I's one-slot rule, unchanged (live s82). Leaving by an exit asks "WOULD YOU LIKE TO SAVE THE DUNGEON MAP ?"; YES
  writes `DNG.SAV` (dungeon id + map picture) and `DNGX.SAV` (the live level buffer). An entry loads that slot when its id
  matches, with no time test; otherwise the level loads from its file, fully reset. NO writes nothing, so an older save of
  the same dungeon comes back. Seen with the DNG8 Ice Dragon: after ATTACK the dragon's cell is a plain message cell; a
  YES save keeps it gone, a later NO still loads that save, and only a slot holding another dungeon (or none) brings the
  dragon back (`endgame.md` §6).
- **Decision (user s82):** as I's D-027: the ST slot rule, except that NO forgets a slot of the same dungeon; the plain
  ST behaviour as an option.
- **Remaster:** shared `DungeonSaveSlot` / `GameSession.AnswerDungeonExit`; option `p2.dungeon-map-no` = `st` keeps the
  slot through a NO. Test `D2041_*`.
- **Side effects:** the ATTACK soft-lock on the Ice Dragon lasts while the slot holds DNG8 (ST: also through a NO; remaster:
  a NO clears it). Every other one-shot dungeon event follows the same rule.
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/endgame.md` §6; I: `dungeon-representation.md` §Leaving
  and re-entering.

## D2-042 — Wilderness inn stay length (I's D-008 again)

- **Manual:** the combined rulebook keeps I's text: independent inns "cost 250 GP plus tax for a single stay of two
  weeks" (Inns, PDF p.7), and a town Inn "stay lasts about two weeks" (PDF p.6).
- **ST:** as DOS I. A stay at a wilderness inn (tile 41) adds 1000 to the travel-time word (`PHANT.PRG 0xfa38` →
  `0xef62`, the same adder as a step), which ages the party only through the next town entry (`@264 += time / 1792`,
  `towns.md` §2); on DOS I that is 3.9 days of the day counter (II's day divisor not read `[~]`). It does not age anyone
  by two weeks.
- **Decision (user s84):** as I's D-008: the manual's two-week stay by default; the ST's +1000 travel time, no aging, as
  the rules option `p2.wild-inn-time` = `st`.
- **Remaster:** today plays the manual reading, as I's D-008 decision does: `WildernessInn.Stay` ages every member two
  weeks and adds no travel time. Built s85: `p2.wild-inn-time` = `st` adds 1000 (`phantasie2/travel-time.json`
  `wilderness_inn_dos`) and ages nobody (`GameOptions.WildernessInnTravelTime`, shared with `p1.wild-inn-time`). Test `D2042_*`.
- **Side effects:** as I's D-008 (the day counter doesn't move for a stay under the manual reading).
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/towns.md` §2.

## D2-043 — End-of-combat XP and gold (I's D-053 again)

- **Manual:** the combined rulebook keeps I's text: "[original text] AND GOLD are displayed at the end of combat - the
  number of experience points … divided among individual party members when they enter a town" (PDF p.7).
- **ST:** as DOS I. Live s83 (`parity/encounters-endgame-live.md` §8): killing the Ice Dragon (XP 300,000) with six living
  members showed **"[original text] 50000 XP AND 0 GP"**, the per-survivor share at the end of combat. The pooling and
  remainder rule are DOS I's `[C]` (s95, `0xa32e..0xa484`): n = non-DEAD members, shown `10·⌊XP/10n⌋` XP and
  `⌊gold/n⌋` GP, the town pools `0x25eb2` (XP in tens) / `0x25eb4` (gold) grow by those shares × n, capped 65535, so the
  remainders are lost; the town-entry split `0xbda6` is D2-045's D-037 row.
- **Decision (user s84):** as I's D-053: the manual's totals, divided on town entry, by default; the ST's per-survivor
  shares as the rules option `p2.combat-shares` = `st`.
- **Remaster:** today plays the manual reading through I's shared code (`p1.combat-shares` at its `manual` default:
  "{xp} [original text] AND {gold} GOLD", the whole amount into the sack, split on town entry).
  Built s85: `p2.combat-shares` = `st` runs I's DOS share code (`GameOptions.SurvivorShares`,
  shared with `p1.combat-shares`; ST pooling / remainders as DOS I `[C]`). Test `D2043_*`.
- **Side effects:** as I's D-053 (no per-fight remainder loss; BEG FOR MERCY takes the whole purse).
- **Spec:** `reverse-engineering/atari-st/phantasie2/parity/encounters-endgame-live.md` §8; I: `combat-resolution.md` §Outputs.

## D2-044 — DIVINE I's fifth deity (an ST fault)

- **Manual:** silent. It says divine spells "may only be acquired in the course of the game for special purposes" (p.14)
  and says nothing about who appears.
- **ST:** `0x72a4`: in a dungeon the deity is `table[⌊r₁·r₂·0.0004⌋]` from the four names DEMIGOD, ATHENA, APOLLO, ZEUS
  (`0x1fd12`). With r₁ = r₂ = 100 (about 1 in 232,000 casts; `rand / 327` reaches 100) the index is 4. The fifth longword
  is the unrelocated text "WHO " (`0x57484f20`); `%s` then reads address `0x484f20` on the 68000's 24-bit bus, past 4 MB
  of ST memory, which should end in a bus error (the program crashes) `[~]` (static; not run live).
- **Decision:** DECIDED (user, s92): index 4 shows **ZEUS** (not a manual conflict: an ST bug on a reachable roll).
- **Remaster:** index 4 is shown as ZEUS, the last name, followed by the level's advice as for any deity
  (`Phantasie2Campaign.CastDivineI`). Test `DivineI_*`.
- **Side effects:** none; the deity name is cosmetic and the advice (message 105) is the same for every deity.
- **Spec:** `reverse-engineering/atari-st/phantasie2/logic-specs/magic-divine.md` §3.

## D2-045 — I's other rule decisions, audited for II (the ST repeats DOS)

- **Manual:** the combined rulebook keeps I's text for every rule below (II's own changes are only on p.27).
- **ST:** the s92 audit read each rule's ST code (static, `PHANT.PRG`); s95 read the rest and settled D-020, D-030,
  D-031 and D-059. On 29 of the 31 rules the ST does what DOS I does, so each of I's decided conflicts is a conflict in
  II too (D-015 and D-033 are the exceptions):

  | I's entry | Option | ST evidence | ST = DOS? |
  |---|---|---|---|
  | D-010 HEALING range | `p1.town-healing` | field cast `0x7172`: `rnd100·(6t² + 4)/100`, no floor (0–10 … 0–100) for **every** field cast, not only town | yes (DOS town range, wider use) `[C]` |
  | D-011 ATTACK / SLASH gate | `p1.attack-gate` | order menu `0xf42a` `@250 ≥ 75`, `0xf470` `@250 ≥ 150` | yes `[C]` |
  | D-012 LUNGE for Thieves | `p1.lunge` | `0xf4cc`: class 0 (THIEF) or 3 (FIGHTER) | yes `[C]` |
  | D-013 BINDING | `p1.binding` | `0x80d8`: 85 / 70 / 55 / 45 % into the group routine `0x8f0a` | yes `[C]` |
  | D-014 CONFUSION IV | `p1.confusion-iv` | `0x7fe8`: 80 / 60 / 40 / 5 % | yes `[C]` |
  | D-017 BEG FOR MERCY | `p1.beg-mercy` | `0x9e34`: `rnd < Σreaction + 90`, else "[original text]" | yes `[C]` |
  | D-018 found items | `p1.sack-size` | `SACK.DAT` 60 × u16; BEG clears words 0, 2 … 48 (`0x9e78`) | yes (60, holes) `[C]` |
  | D-020 SLEEP | `p1.sleep` | `0x90b8`: resists on `rnd < L²`; a slept monster (HP ≥ 1) gets state 2 "done this round" in `0x25e6a` (`0x9138`), never the lasting asleep state 1, and the round-top compaction `0xb16e` turns 2 back into 3 | yes `[C]` (s95: the round-only effect confirmed) |
  | D-022 CHARM | `p1.charm` | `0x8236`: success → reaction + 15, greet flag cleared, fight goes on | yes `[C]` |
  | D-023 TELEPORTATION | `p1.teleport` | `0x829e`: `rnd < 3·L + 50` sets the escape flag read at the next round top | yes `[C]` |
  | D-028 spell success | `p1.spell-level` | `0x8f2e`: `rnd ≤ 5·(caster @16 − group @38) + 50`, no level | yes `[C]` |
  | D-029 FIREFLASH damage | `p1.fireflash-damage` | `0x7bc4`: `rnd·{9, 29, 58, 100}·INT/2000 + 1` | yes `[C]` |
  | D-059 FIREFLASH target | `p1.fireflash-target` | `0x7c14..0x7d20`: rank `[0x24c92] − 1` (the back rank) first, a wiped group moves one rank forward; slot `k = r·L/100` (L = last living index), the same wrap search; damage roll first | yes `[C]` (s95); only difference: II's `r` is `rand()/327` = 0..100, so `r = 100` (68 in 32768) can pick the last slot, which DOS's 0..99 never does |
  | D-031 wilderness treasure | `p1.wild-treasure` | victory `0xa674..0xa69e`: wilderness and the drop flag `0x25b1a` set → `0x25b1a := (r ≤ 2·[0x25b86] + 20)`, `0x25b86` = the encounter level L (the value SLEEP squares and FEAR lowers by 6); the dungeon branch `0xa57a` is DOS's `45·surrender + 2·L + 50` with the finder roll | yes `[C]` (s95) |
  | D-047 wilderness inn, DEAD | `p1.wild-inn-dead` | `0xf8ca`: a member with status 0 is skipped | yes `[C]` |
  | D-056 MONSTER EVALUATION | `p1.monster-eval` | `0x705c`: wilderness `min(2·(t + 1), 12)`, dungeon `2·T + 2`, no roll | yes `[C]` |
  | D-057 VISION | `p1.vision` | `0x6ff4`: wilderness only, radius `(MP·100 + 500)/400` | yes `[C]` |
  | D-070 Inn MAGIC INCREASES | `p1.cast-flag` | `@94` set only at `0x85e6` (combat cast) | yes `[C]` |
  | D-033 [original text] | `p1.summon-party` | `0x83e2`: party size == 6 (DEAD members count) and record 6 holds the Guild elemental | **no**: not DOS's living-index quirk; the remaster's "six non-DEAD" differs from it `[C]` |
  | D-030 failed monster run | `p1.failed-run` | `0xa056..0xa10c`: "MONSTERS RUN", `r < 80 − abs(10·(1 − T))` ends the fight, else THREATEN factor `0x24960 := 45` and state 2 for every living slot of every group in `0x25e6a`. `0x25e6a` (16 B per group, a word per slot) is DOS's monster state table `0x64b0`: 1 asleep, 2 done this round, 3 ready. Build `0xac38` := 3 − H (`0x25e9a`, so H = 2 surprise sleepers start at 1); the monster pass `0x8946` acts only on 3, rolls `r < 15` "%s AWAKENS" on 1 (`0x8e04`); compaction `0xb16e` with `0x25f3a` = 1 makes every state > 1 a 3 and keeps 1. So a failed run wakes surprise sleepers (1 → 2 → 3). `0x25bfa` is the HP table, not a sleep table (s92 misread) | yes `[C]` (s95) |
  | D-032 elemental prep | `p1.elemental-prep` | prep `0xafb6` (`@246 := @244`, `@248 := @28 + @240`, `@250 := @30`) loops `< [0x2831c]` and runs only at the build (`0x9d44`, `0x62f8`…); SUMMON `0x83e2` sets the party size to 7 with no prep, and II's elementals (`TWNS.INT` 22–24) hold `@246/@248/@250` = 0, so slot 7 fights with zero attack, damage and armor | yes `[C]` (s95) |
  | D-035 town cast list | `p1.cast-list` | `0xe3f6`: picker `0x16c6` → `0xc70` lists all 60 slots with `@96 + 2k` = 1 (too dear greyed), `@18 −= k mod 4 + 1` before the switch (table `0x22ac8`: slots 0–3 HEALING `0xe4d4`, 43 RESURRECTION `0xe766`, 53 TRANSPORTATION `0xd94c`); any other spell, DIVINE I/II included, spends the MP for nothing | yes `[C]` (s95) |
  | D-063 combat spell list | `p1.combat-spell-list` | orders `0xf56c`: picker `0x17d9c` lists every known spell, no category test; cast `0x79ec`: "%s CASTS A SPELL" then the switch `0x8560` (table `0x212a2`, slots 40–50) — MONSTER EVAL., VISION, TRANSPORTATION (51–53) and DIVINE II / I (56, 57) fall through, MP spent at `0x85d2` | yes `[C]` (s95) |
  | D-065 rename | `p1.rename-party` | `0xda82`: any of the 22 Guild records; a party member's copy is written to the Guild first, renamed (`0xd9a4`), copied back | yes `[C]` (s95) |
  | D-067 Add aging | `p1.add-aging` | Add `0xd442`: alert 6 "Not in the town / [original text] / to get here" only when `@254` ≠ this town, then `@264 += 4` for every Add, same town too (`0xd52c`) | yes `[C]` (s95) |
  | D-069 Continue | `p1.continue` | start dialog `0x238c` (RSC tree 6: object 2 "start this turn in Pippacott" → 1, object 3 "continue saved game" → 2); `0xbf5a` on 2 loads every Guild record with `@218` ≠ 0 in roster order (slots 22–24 to the elemental buffer `0x258d6`), no question; new game clears `@218` on all 25 and opens town 1 | yes `[C]` (s95); the town is the **last** loaded member's `@254` (`0xc06e`), DOS takes the first |
  | D-078 orders reset | `p1.orders-reset` | `0x75e2` sets all 7 repeat orders `0x25f6c` to 4; called after the town loop (main-loop mode 1, `0x7400`) and after the defeat fates (mode 6, `0x74ea`) | yes `[C]` (s95) |
  | D-015 STUNNING BLOW | `p1.stunning-blow` | rider 100 at `0x8c60`: `@24 := 2` (SLEEP) and orders := 4 only when `@24 == 1` (OK); a member the hit killed is already DEAD (`0x9794` → `0x9894`) | **no**: the ST does what I's D-015 decision does (a corpse is never stunned) `[C]` (s95) |
  | D-016 ARMOR IS REDUCED | `p1.armor-reduced` | specials 17–20 `0x945e`: `0x95e8(10 − 2n, 57)`; the field map `0x12078` gives id 57 → `@248` (ids 56–58 → `@246/@248/@250`, `2·id + 134`) | yes `[C]` (s95): weapon damage, as DOS |
  | D-037 town-entry payout | `p1.payout-remainder` | town entry `0xbda6`: a "Shares for:" dialog (`0x2db2`) per member, S = Σ shares (0 → 1); XP per share `min(65535, 10·[0x25eb2]/S)`, gold per share `[0x25eb4]/S`; each member `@262 += share·xp` (capped 65535), bank `@266 += share·gold`; both pools cleared, remainders lost. The fight payout `0xa34c..0xa484` pools `gold/n·n` and `XP/(10n)·n` (tens, cap 65535) as DOS | yes `[C]` (s95) |
  | D-054 Armory restock | `p1.armory-restock` | town entry `0xc0a8`: 18 × `stock[rand/274] := 0` (ids 0..119: weapons, armor, shields, potions), then 4 × `stock[list[rand/(32767/[0x25eaa])]] := 1` (list at town `+0xb6`); skipped on the start / load entry (`0x25ea6`) | yes `[C]` (s95) |

  s95 checked the last rows; nothing is left unchecked. `p1.stash-access` is QoL. Already covered elsewhere:
  D-001 / D-006 (D2-009), D-002 (D2-020), D-003 (D2-021), D-004 / D-046 (D2-032), D-007 / D-050 (D2-013), D-008 (D2-042),
  D-021 (D2-023), D-024 (D2-022), D-027 (D2-041), D-036 (D2-011), D-048 (D2-019), D-051 (D2-010), D-053 (D2-043), D-055
  (D2-040).
- **Decision:** DECIDED (user, s92): **I's decisions hold for II**, for every row above, D-033 included (the remaster's
  "six non-DEAD members" rule, not the ST's party-size test) and the rows s95 checked.
- **Remaster:** unchanged: a II save has no `p1.*` value, so the shared code takes I's decided side. No `p2.*` options
  for the ST side (not asked for).
- **Side effects:** II's "Original (Atari ST)" preset does not reproduce these ST behaviours; the ST evidence stays here.
  Two rows are not DOS: D-015, where the ST already does what I's decision does (that preset is authentic there), and
  D-033, where neither DOS nor the remaster matches the ST.
- **Spec:** the addresses above (`PHANT.PRG` image addresses, `reverse-engineering/atari-st/phantasie2/FORMAT.md` §1).

## Apple II entries (s158)

The importer builds II from the Apple II version (user s158, `docs/plan/10-importer.md` section 6). D2-046 to D2-050
compare the Apple II v1.0 (2/5/86) data with the ST data today's GameData holds; "Manual" is the same combined
rulebook. Evidence: static, `reverse-engineering/apple2/phantasie2/FORMAT.md` (section 5) and
`tools/scripts/diff_a2_p2_vs_st.py`. An ST build plays the ST data; an Apple-only import is built as each entry's
"Remaster" line says (s160: the importer's `P2Apple2` pipeline with the shipped `GameData/phantasie2/apple2-import.json`
and `strings/fallback.en.json`).

## D2-046 — The wilderness is turned a quarter between the Apple II and the ST

- **Manual:** gives no map of Ferronrah; it says only that Ferronrah lies "just south of the Isle of Gelnor" (CB p.2).
- **Apple II:** the grid bytes are the ST's, but the Apple draws them turned a quarter and its compass follows its own
  screen: N moves to the ST's east, E to the ST's south (OUTDOORS2 5040, 5220–5260). Its scrolls say SOUTH where the ST
  says WEST, NORTH for EAST, WEST for NORTH (scrolls 121, 124, 126–129, 131, 138, 140), and the eight entrances of the
  Black castle (OUT28) lead to DNG7's start cells shifted by a quarter turn (the dungeon maps themselves are the same).
- **ST:** today's GameData: the ST's compass, scroll words and OUT28 entrance table.
- **Decision:** (user s159) keep the ST's world whatever the source copy: ST compass, keys and OUT28 table. An Apple
  import's scroll text has its compass words swapped to the ST orientation at import (D2-050).
- **Remaster:** ST orientation (built). Apple-only import (s160): OUT28's eight entrances take the ST's destinations
  (`apple2-import.json` `st_cells`); the compass words of SCROLLL1-20 are swapped at import (NORTH → EAST, EAST → SOUTH,
  SOUTH → WEST, WEST → NORTH, all at once, at the start of a word: SOUTH-ERN, NORTHERN), each boxed line keeping its
  width. After the swap the scrolls' 21 compass words equal the ST's in order (test). Dungeon messages and wilderness
  texts are not touched (the dungeon maps are the same; the 43 wilderness texts agree with the ST's).
- **Side effects:** taking the Apple's scroll text without turning the map makes every direction in it wrong.

## D2-047 — Five monster numbers

- **Manual:** gives no monster numbers.
- **Apple II / ST** (`MN2x` / `PHANT.PRG` table): VIPER (6) companions 1, 1 / 0, 0; FIRE ELEMENTAL (53) reaction
  modifier −10 / 0 and treasure 0, 0 / 0, 135; PLUTO'S WYVERN (74) damage 40 / 35; PLUTO'S GIANT WYVERN (76) attack
  power 350 / 300; PLUTO'S DRAGON-DEMON (77) attack power 800 / 400. Every other number of the 80 monsters agrees.
- **Decision:** (user s159) ST values for every source copy; an Apple import takes the ST's five numbers.
- **Remaster:** ST values (built). Apple-only import (s160): the five numbers set to the ST's from `apple2-import.json`.

## D2-048 — Town data

- **Manual:** gives no prices or stock.
- **Apple II / ST** (`TF2` / `TWNS.INT`): DEMONIAC's price factor 255 / 150 (Apple prices there ×1.7); IPSWICH and FERRON
  stock 5 × item 127 SCROLL E / 5 × item 126 SCROLL D. Everything else in the ten towns agrees (stock, restock pool,
  exits, names; the ST's two unused towns are not on the Apple).
- **Decision:** (user s159) ST values for every source copy (DEMONIAC 150, SCROLL D in IPSWICH and FERRON).
- **Remaster:** ST values (built). Apple-only import (s160): DEMONIAC's factor and the four stock cells set to the ST's from
  `apple2-import.json`.

## D2-049 — Creation-table cells, map and dungeon cells

- **Manual:** HALFLING DEX modifier 12 and MONK SLEEP not at level 7 (as D2-009); silent on the rest.
- **Apple II:** HALFLING DEX 12 and MONK spell slot 41 never learnable, i.e. **the manual's values** (D2-009's decided
  side). MONK level 10 skill 6 (`_42_toss`) 0, PRIEST level 15 skill 8 (`_unused_46`) 0. Map cells (ST x, y): OUT1
  (22, 0), (22, 1) tile 100 (blocked); OUT3 (5, 12…15) tile 1 (forest); OUT17 (20, 6) tile 76; OUT31 (17, 18),
  (17, 19) tile 100. Dungeon cells: DNG5 byte 1233 (x 12, y 37) 0x11 (floor); DNG8 byte 1095 (x 6, y 33) 0x8D.
- **ST:** HALFLING DEX 11, MONK spell 41 at level 7 (both already replaced by D2-009), MONK L10 skill 6 = 14, PRIEST L15
  skill 8 = 4; tiles 101, 21, 72, 85; DNG5 cell 0 (wall), DNG8 cell 0x85 (forced fight).
- **Decision:** (user s159) ST cells for every source copy (the two skill cells, the 9 map and 2 dungeon cells); D2-009's
  cells need nothing (the Apple agrees with that decision).
- **Remaster:** ST values (D2-009: the manual's). Apple-only import (s160): the two skill cells, the 9 map cells (a fogged
  cell keeps its fog) and the 2 dungeon bytes set to the ST's from `apple2-import.json`; every number of the Apple build
  now equals the ST build's except OUT1's unused feature record 14 (no cell uses it).

## D2-050 — Text

- **Manual:** silent on these texts.
- **Apple II:** monster names APRENTICE DEVIL (12), PLUTO'S GIANT CONSTRICTOR (17), MASTER ILLUSTIONIST (58), CREEPING
  MOLD (62); item 53 SH SWORD; race LIZARD MAN; status OKAY. All upper case. Scroll and message wording differs in
  places (beside D2-046): DNG1 msg 40 "AN UNGRATEFUL SPRITE" / ST "AN EVIL GHOST"; DNG6 msgs 45, 53, 62, 77 and DNG7
  msgs 50, 77, 79, 85 have other menu choices (e.g. DNG6 53: HOLD BREATH / BREAK THE GATE / STUFF UP THE GAS SOURCE /
  PRAY TO YOUR GODS vs the ST's HOLD / RUN AWAY / SHUT OFF), plus spelling (DETENTION / DETENSION, HERE / HEAR …). The
  Apple has **no spell names** (spells are numbers 1–60). String tables (corrected s160; s158 said "none of the ST's
  `strings/en.json` lines except Pluto's speech"): of the 330 ids whose text the ST gives (shared 53, I's table 195,
  II's 82), the Apple's programs hold 217 verbatim (most of I's engine lines, e.g. [original text], {0} TAKES AN
  AMULET); 95 more it prints in its own wording (the Mystic's ranks, Pluto's speech in `SCROLLL0`, the whole Filmon
  ending in `WIN`, LIZARD MAN, DROP WHO, ...); 18 it lacks (VALUE, MG WEAPON, IN PEACE, nine spell words, PLUTO'S
  MINION ! !, ...).
- **ST:** today's GameData text (mixed case in scrolls; GIANT CONST., KILLER CRUD, SM SWORD, LIZ.MAN, OK).
- **Decision:** (user s159) an Apple-only import shows the Apple's own text (names, wording, menu labels) with the compass
  words in its scrolls swapped to the ST orientation (D2-046); spell names come from the manual's spell list; lines the
  Apple lacks show the remaster's own wording instead of "...". An ST import keeps the ST text. Menu wording that differs changes which choice is which only if the
  event scripts differ, and they do not (level bytes 0–1739 agree): the labels differ, the outcomes do not `[~]`.
- **Remaster:** ST text (built). Apple-only import (s160): names, scrolls and dungeon text are the Apple's (scroll compass
  turned, D2-046). Spell names: the manual's list (CB p.12: HEALING 1 ... SUMMONS ELEMENTAL, MONSTER EVALUATION ...,
  `apple2-import.json`); the two divine spells, which the manual does not name, are "DIVINE #57" / "DIVINE #58"
  (remaster wording `[~]`). String tables: the 217 verbatim ids and the 95 in the Apple's own wording come from the
  player's Apple programs (`p2-apple2` alternatives in `en.sources.json`, the own-wording ones from
  `tools/scripts/p2_apple2_own_wording.json`, applied to II only and only while II's tables are the Apple's, so I keeps
  DOS I's lines and an ST import keeps the ST's); the 18 it lacks take the remaster's wording
  (`GameData/phantasie2/strings/fallback.en.json`), laid under every imported file so a DOS I or ST import still wins.
  The Filmon ending keeps the ST's line ids with the Apple's 40-column lines (the counts match); the Apple's party-size
  number in "THESE n BRAVE ADVEN-" is left out (the id has no placeholder) `[~]`. Not checked in the Client yet: the
  18-character MONSTER EVALUATION in II's spell windows (fit or scroll) and the Apple ending lines on the ending screens.
