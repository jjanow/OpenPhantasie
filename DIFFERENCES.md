# DIFFERENCES — manual vs DOS game code

Every place where the **1986 SSI manual** (`docs/manuals/phantasie1/Phantasie_I_1986_SSI_manual_text.pdf`) and the **DOS game code** disagree,
what the user decided, and what the remaster does. The manual is the source of truth until 1:1 is done; every conflict
goes to the user, who decides (`AGENTS.md`). This file is the user's end-of-project review list, so keep each entry to:
**Manual · DOS · Decision · Remaster · Side effects** (plus the spec). History of how a finding was made belongs in the
spec or `docs/plan/SESSION-LOG.md`, not here. Tests that pin an entry carry its ID in their name (e.g. `…_D046`).

**Options:** every `p1.*` rule option is locked per save (PLAN P3.2). The value that matches the decision is in the
Original (manual) preset (the default) and the Remaster preset; the DOS value is in the Original (DOS) preset. An entry
names its presets only where they differ from this.

**Status:** `PENDING` = raised, no decision yet (remaster unchanged) · `DECIDED: <side>` + `built` / `not built` ·
`NOT A CONFLICT` = checked, the two agree or the manual is silent. IDs D-039…D-045 were never assigned.

| ID | Topic | Status |
|---|---|---|
| D-001 | Halfling maximum DEX (19 vs 20) | DECIDED: manual · built; option `p1.halfling-dex` |
| D-002 | Guild roster size (22 vs 21) | DECIDED: manual · built; option `p1.guild-size` |
| D-003 | Race lifespans | DECIDED: manual (2-week unit + bands) · built; option `p1.lifespans` |
| D-004 | Town Inn stay doesn't advance time | DECIDED: manual · built; option `p1.town-inn-time` |
| D-005 | Wilderness inn price (256 vs "250 + tax") | NOT A CONFLICT (256) |
| D-006 | Monk learns SLEEP at level 7 | DECIDED: manual · built; option `p1.monk-sleep` |
| D-007 | Level-up HP averages vs the manual's chart | DECIDED: manual · built; option `p1.hit-dice` |
| D-008 | Wilderness inn stay length (~4 days vs two weeks) | DECIDED: manual · built; option `p1.wild-inn-time` |
| D-009 | CON loss at death (DOS) vs at resurrection (manual) | DECIDED: DOS · built |
| D-010 | Town HEALING range (0–10… vs 1–9…) | DECIDED: manual · built; option `p1.town-healing` |
| D-011 | ATTACK / SLASH need attack skill | DECIDED: manual · built; option `p1.attack-gate` |
| D-012 | LUNGE allowed for Thieves | DECIDED: manual · built; option `p1.lunge` |
| D-013 | BINDING makes negative-defense monsters harder to hit | DECIDED: manual · built; option `p1.binding` |
| D-014 | CONFUSION 4 doesn't fully stop monster specials | DECIDED: manual · built; option `p1.confusion-iv` |
| D-015 | STUNNING BLOW puts a just-killed member to SLEEP | DECIDED: dead stays dead · built; option `p1.stunning-blow` |
| D-016 | "[original text]" lowers weapon damage | DECIDED: lower armor · built; option `p1.armor-reduced` |
| D-017 | BEG FOR MERCY can fail | DECIDED: manual · built; option `p1.beg-mercy` |
| D-018 | Found-item capacity (60 vs 90) | DECIDED: manual · built; option `p1.sack-size` |
| D-019 | Monsters that run away still pay all their gold | DECIDED: DOS · built |
| D-020 | SLEEP lasts only the rest of the round | DECIDED: manual (user formula) · built; option `p1.sleep` |
| D-021 | FEAR doesn't make monsters run | DECIDED: manual · built; option `p1.fear` |
| D-022 | CHARM doesn't stop the fight | DECIDED: manual · built; option `p1.charm` |
| D-023 | TELEPORTATION can fail; round plays out | DECIDED: manual · built; option `p1.teleport` |
| D-024 | DISPELL UNDEAD also hits demons and BANTIR | DECIDED: manual · built; option `p1.dispell-undead` |
| D-025 | Party defeat: the Great Demon judges each member | NOT A CONFLICT (DOS rule) |
| D-026 | Won by killing the Black Lord, not Nikademus | NOT A CONFLICT (DOS rule) |
| D-027 | Dungeon re-entry: monsters/locks reset by time? | DECIDED: DOS + NO purges · built; option `p1.dungeon-map-no` |
| D-028 | Spell success ignores caster level | DECIDED: manual (remaster formula) · built; option `p1.spell-level` |
| D-029 | FIREFLASH damage scales with INT | DECIDED: manual ranges scaled by INT · built; option `p1.fireflash-damage` |
| D-030 | A failed monster run wakes SLEEP'd monsters | DECIDED: sleepers stay asleep · built; option `p1.failed-run` |
| D-031 | Wilderness treasure isn't automatic | DECIDED: manual + SCROLL 17 fix · built; option `p1.wild-treasure` |
| D-032 | Summoned elemental fights with zero stats | DECIDED: fix the bug · built; option `p1.elemental-prep` |
| D-033 | When [original text] works (5 vs 6 members) | DECIDED: manual · built; options `p1.summon-party`, `p1.monster-spell-target` |
| D-034 | Surprised fights skip PARTY OPTIONS in round 1 | NOT A CONFLICT (DOS rule) |
| D-035 | Town Cast spell offers combat spells | DECIDED: manual · built; option `p1.cast-list` |
| D-036 | Copy-protection quiz in dungeon 1 | DECIDED: not included |
| D-037 | Town-entry loot payout (share split vs equal split) | DECIDED: keep Core's equal split; option `p1.payout-remainder` |
| D-038 | Leaving a dungeon only by the entry door | DECIDED: DOS (manual error) · built |
| D-046 | Wilderness inn can raise MP max past the table | DECIDED: manual · built; option `p1.inn-magic-cap` |
| D-047 | Wilderness inn skips DEAD members | DECIDED: manual · built; option `p1.wild-inn-dead` |
| D-048 | Gold cap (65,000 vs 65,535) | DECIDED: manual · built; option `p1.gold-cap` (DOS 65,535 / lifted) |
| D-049 | Bank Deposit = share split | DECIDED: manual · built |
| D-050 | Training hides the cost while XP is short | DECIDED: manual · built |
| D-051 | Magical pool raises stats with no cap | DECIDED: manual · built; option `p1.pool-cap` |
| D-052 | Trap menu labels / who may disarm | DECIDED: manual labels · built; Amiga I requester 0 too |
| D-053 | End-of-combat display: per-survivor share vs totals | DECIDED: manual · built; option `p1.combat-shares` |
| D-054 | Armory restock also wipes armor and shields | DECIDED: manual · built (sold-out refill); option `p1.armory-restock` |
| D-055 | Path not faster than every terrain | DECIDED: manual · built (cost 30); option `p1.path-time` |
| D-056 | MONSTER EVALUATION ignores real monsters | DECIDED: manual · built (pool avg, 10 % ±1); option `p1.monster-eval` |
| D-057 | VISION reveals a small square, not "hundreds of miles" | DECIDED: manual · built (whole region); option `p1.vision` |
| D-058 | No "AN ENCOUNTER" flash before a fight | DECIDED: manual (day/night wording) · built; I Amiga screens: no-wait line · built |
| D-059 | FIREFLASH hits the back rank | DECIDED: manual (front rank) · built; option `p1.fireflash-target` |
| D-060 | APPLETON's town record sits on an inn tile | DECIDED: manual (town tile; inn moved) · built |
| D-061 | USE ITEM can't show an item's properties | DECIDED: manual (descriptions) · built |
| D-062 | TRANSPORTATION lists 12 towns incl. the current one | DECIDED: manual · built; option `p1.transport-list` |
| D-063 | Non-combat spell picked in combat | DECIDED: manual · built; option `p1.combat-spell-list` |
| D-064 | Character sheet wording | DECIDED: manual · built |
| D-065 | Renaming a party member | DECIDED: manual · built; option `p1.rename-party` |
| D-066 | Town menu: letter keys vs highlight bar | NOT A CONFLICT — both · built |
| D-067 | Guild Add ages a member already in this town (Q8) | DECIDED: no aging · built; option `p1.add-aging` |
| D-068 | Distribute: "type 7 to sell" vs SELL/SACK menu | NOT A CONFLICT — both · built |
| D-069 | Continue saved game: pick a character vs load every flagged record | DECIDED: manual · built; option `p1.continue` |
| D-070 | Inn MAGIC INCREASES: any cast vs combat casts only | DECIDED: manual · built; option `p1.cast-flag` |
| D-071 | Map of Gelnor: 16 sections vs 18 region files | NOT A CONFLICT |
| D-072 | YES/NO prompts: "answer Y or N" vs highlight only | NOT A CONFLICT — both (as D-066) · built |
| D-073 | Melee speed: T)IME LAG 0–9 + sound vs Options → Speed fast/normal/slow | DECIDED: manual · built; QoL switch: in the Options tab |
| D-074 | Guild S)PELLS list: spell numbers after "POSSIBLE SPELLS:" vs names under "Available spells:" | DECIDED: manual · built |
| D-075 | New member: full sheet + SPACE after naming vs straight back to the Guild menu | DECIDED: manual · built |
| D-076 | Wilderness keys: C/H/I/U/T + 3/N/E/S/W vs arrows + Return menu only | DECIDED: manual · built (+ numpad, QoL diagonals) |
| D-077 | Wilderness screen: party pictures + command list beside the map vs the map alone | DECIDED: manual · built (QoL status lines) |
| D-078 | Combat orders: "remain unchanged until you change it" vs reset to PARRY on leaving town / after a defeat | DECIDED: manual · built; option `p1.orders-reset` |
| D-079 | Amiga field: a monster's "%s CASTS A SPELL" vs the Amiga's blank line with sound 9 | DECIDED: keep the text, add sound 9, no wait · built |
| D-080 | Amiga map boxes: four texts differ from DOS ("[original text]" etc. with no blank before "!", "THAT IS A WALL") | DECIDED: Amiga text in the Amiga boxes · built |
| D-081 | Amiga POTIONS view: the Amiga's ring letters run into "…ND OF NIKADEMUS" (no end marker) | DECIDED: an Amiga bug, fixed: clean rows · built |
| D-082 | Amiga-only import: item names "HEAL POT1".."HEAL POT9" and "SROLL 19" vs the manual's Healing 1..9 / SCROLL n | DECIDED: Amiga's HEAL POT1..9, typo SROLL 19 → SCROLL 19 · built |
| D-083 | Amiga-only import: the fire elemental named "FIRE  ELE" vs the manual's FIRE (DOS "FIRE ELEM") | DECIDED: typo fixed to DOS's FIRE ELEM · built |

---

## D-001 — Halfling maximum DEX

- **Manual:** "Maximum Value for Attributes by Race": Halfling DEX **20**.
- **DOS:** creation roll `min(18, roll + classMod − 7) + raceMod − 10`, so max = `raceMod + 8`; Halfling DEX modifier
  (`TWNS.INT @0x942`) is 11 → max **19**. The table's other 74 cells all equal `raceMod + 8`.
- **Decision (user, 2026-10-10):** manual.
- **Remaster:** Halfling DEX modifier 12 in `creation-tables.json` (max 20).
- **Side effects:** the formula has no separate cap, so **every** Halfling DEX roll is 1 higher than DOS, minimum included.
- **Spec:** `character-creation.md` section Attributes.
- **Option `p1.halfling-dex`:** `manual` · `dos` (TWNS.INT's Halfling DEX modifier 11, ceiling 19). The DOS cell lives in `creation-tables.json` `dos_tables` and is applied per save (`CharacterCreationTables.ForRules`); it also sets the magical pool's Halfling DEX maximum (19).

## D-002 — Guild roster size

- **Manual:** "A maximum of **21** characters can be generated."
- **DOS:** `GUILD.DAT` holds 22 records; DOSBox (s9) accepted a 21st and 22nd, and the 23rd New member silently does nothing
  (fixture `reverse-engineering/dos/working/snapshots/roster-full-22-s9/GUILD.DAT`).
- **Decision (user, 2026-10-10):** manual — 21.
- **Remaster:** `Character.GuildMaxCharacters = 21`; `GuildHall.TryEnroll` refuses a 22nd with "Guild roster is full."
- **Side effects:** the save format keeps 22 slots (`Character.GuildSlotCount`), so a DOS save with 22 loads all 22; the
  guild creates no more until one is purged. DOS refuses silently, the remaster says why.
- **Note:** the manual's Guild page also says "As many as 37 characters can be generated" (its sample list numbers the
  elementals 38–40); the decision above follows the "maximum of 21" sentence.
- **Option `p1.guild-size`:** `manual` (21) · `dos` (Guild N)EW fills all 22 records, from `rules.json` `guild_by_platform.dos`). The save format keeps 22 slots either way. The DOS-import warning "x characters, the Guild holds 21" still reads the profile's 21.

## D-003 — Race lifespans

- **Manual:** elves live about **3000** years; dwarves and gnomes about **200**; humans about **80**; the others about **40 to 50**.
- **DOS:** lifespan `@252 = @10 × factor` (`@10` = seed 8..13): Human, Pixie ×1; Dwarf, Halfling, Gnome ×5/2; Elf ×19;
  all others ×2/5. Death when `@264 >> 8 > @252` (AGE = `@264/26`), decline (×4/5, once) past 70 %, both checked on town entry.

  | Race | DOS dies at AGE | Manual |
  |---|---|---|
  | Human | 88–137 | ~80 |
  | Pixie | 88–137 | 40–50 ("others") |
  | Dwarf, Gnome | 206–324 | ~200 |
  | Halfling | 206–324 | 40–50 ("others") |
  | Elf | 1506–2441 | ~3000 |
  | Sprite, Ogre, Gnoll, Troll, Kobold, Orc, Goblin, Liz.Man, Minotaur | 39–59 | 40–50 |

- **Decision (user, 2026-10-10; details approved s15):** manual lifespans, stored in finer units:
  - Unit = the age clock's own (1/26 year = 2 weeks); the check compares `@264` with the lifespan directly (DOS's `>> 8`
    dropped). Decline past 70 %, death past 100 %, one-time ×4/5 and town-entry timing stay DOS.
  - Lifespan = a band per race (manual figure ±10 %), picked by the seed:
    `lifespan = round(26 · (lo + (@10 − 8)·(hi − lo)/5))`. Elf 2700–3300 · Dwarf/Gnome 180–220 · Human 72–88 ·
    all others, **Pixie and Halfling included**, 40–50.
  - Starting age keeps the DOS formula (`[C]`, OV1 `0x0f04`): `@264 = 260 + trunc(lifespan · (0.1 + rand/1310720))` →
    starting AGE others 14–16, Human 17–21, Dwarf/Gnome 28–37, Elf 280–422.
  - The manual's sample human priest AGE 24 is a manual error (user s15). Short-lived races starting with ~⅓ of their life
    used is intended: "their short lifespans are the price they pay for extra high stats."
  - **DOS imports (user s17):** `@252` is ignored; the lifespan is recomputed from the birth race `@216` (an UNDEAD uses
    its living race) and `@10`, clamped to 8..13 (an already-declined seed can land 1–2 steps low `[~]`).
- **Remaster:** `CharacterCreation.LifespanFor` / `LifespanForImported` / `StartingAgeClock`, `Aging.ApplyTownEntry` (also
  takes travel time); bands in `creation-tables.json` `lifespan_bands`.
- **Side effects:** Pixies and Halflings become short-lived. A DOS character can be past its new lifespan on import and die
  or decline on its next town entry. DOS export writes `lifespan / 256` to `@252` (lossy); `@264` is a u16, so an elf
  older than AGE 2520 is clamped and listed on export (P1.4).
- **Spec:** `town-menus.md` section Aging; `save-file-format.md` `@252`.
- **Option `p1.lifespans`:** `manual` (the bands above) · `dos`: a new character gets DOS's `@252 = @10 · factor` (integer math, multiply first; `creation-tables.json` `dos_tables.lifespan_factors`), kept as `@252 · 256` age-clock units, and town-entry aging compares `@264 >> 8` with `@252` as DOS. A DOS import keeps its `@252` (instead of the recomputed band), so export writes it back unchanged. Starting age, decline and death timing are DOS's either way.

## D-004 — Town Inn stay doesn't advance time

- **Manual:** "inns located in towns are free, and each stay lasts about two weeks."
- **DOS:** the town Inn (OV1 `0x2a0d`) never writes the travel-time counter or the age clock: resting is free and instant.
- **Decision (user, 2026-10-10):** manual — a stay lasts two weeks.
- **Remaster:** `TownMenus.Rest` adds 1 to every member's age clock `@264` (= two weeks). Still free.
- **Side effects:** characters age by resting; the "has aged" / "has died" check still runs on the next town entry.
  Inn-spamming for MP-max raises now costs lifespan.
- **Spec:** `town-menus.md` section Town Inn rest.
- **Option `p1.town-inn-time`:** `manual` · `dos`.

## D-005 — Wilderness inn price

- **Manual:** independent inns "cost 250 GPs (plus tax) per stay."
- **DOS:** requires gold > 255 and takes **256** (OV2 `0x6700`).
- **Decision (user, 2026-10-10):** NOT A CONFLICT — 256 = 250 + tax. `WildernessInn` charges 256. Stay length → D-008.

## D-006 — Monk can learn SLEEP at level 7

- **Manual:** the Monk level-7 row lists spells **13** and **48** only.
- **DOS:** spell-level table (`TWNS.INT @0x05a`) gives Monk level 7 = 13, **42 (SLEEP)**, 48. Every other cell matches.
- **Decision (user, 2026-10-10):** manual — Monks never get SLEEP.
- **Remaster:** `creation-tables.json` `spell_levels` Monk 7 = 13, 48.
- **Side effects:** a DOS Monk that already knows SLEEP keeps it on import (imports load as-is, P1.4).
- **Spec:** `town-menus.md` section Level-up gains.
- **Option `p1.monk-sleep`:** `manual` · `dos` (TWNS.INT's Monk level 7 = 13, 42, 48: Monks can learn SLEEP). The DOS cell lives in `creation-tables.json` `dos_tables`.

## D-007 — Level-up HP averages vs the manual's chart

- **Manual:** the "Hit Points by Class and Level" chart (CON 10); every cell is `2 × hp_factor[class][level]`.
- **DOS:** each level re-rolls `(hp_factor × (CON+5)) >> 4` d3, HP max never drops (+1 if not higher). Simulated at CON 10
  (20 000 runs/class): below the chart at every level — L1 2 HP low, L15 5–7 % low (Fighter 159.6 vs 170).
- **Decision (user, 2026-10-10):** manual chart.
- **Remaster:** `LevelUp.HitDiceCount` = `(hp_factor × (CON + 5)) / 15` (at CON 10 exactly `hp_factor` d3); d3, re-roll,
  +1 floor and cap 255 stay DOS. Lands within ~1 HP of every chart cell.
- **Side effects:** the formula is shared with character creation, so new characters also start with more HP
  (+2 on average at CON 10; about 1/15 more at any CON).
- **Spec:** `town-menus.md` section Level-up gains.
- **Option `p1.hit-dice`:** `manual` · `dos`.

## D-008 — Wilderness inn stay length

- **Manual:** a wilderness inn stay lasts **two weeks**.
- **DOS:** a stay adds 1000 travel-time units = 3.9 days on DOS's own day counter; on its own it ages nobody.
- **Decision (user, 2026-10-10):** manual.
- **Remaster:** `WildernessInn.Stay` adds two weeks (`@264 += 1`) to every member, DEAD too, instead of 1000 time units.
  Price 256 (D-005); MP raise and HP/MP restore as DOS, except D-046 / D-047.
- **Side effects:** a stay always ages the party (applied at the next town entry). The day counter (`DAY OUT`) doesn't
  move for a stay (DOS: +3 days).
- **Spec:** `town-menus.md` section Wilderness inn, section Aging.
- **Option `p1.wild-inn-time`:** `manual` · `dos`.

## D-009 — CON loss: at death (DOS) vs at resurrection (manual)

- **Manual:** RESURRECTION "attempts to raise one party member from death. The shock of dying, however, will permanently
  lower the constitution of the victim by one or two points."
- **DOS:** CON is lost **at death**, not on revival. The death routine (OV2 `0x12cf`, used by combat, trap, dungeon-event
  and swimming deaths) does `CON −= (CON>0 ? 1 : 0) + (CON>1 && @10 < 11 ? 1 : 0)` — seeds 8–10 lose 2, 11–13 lose 1, no
  floor. RESURRECTION (`50 + 5·(caster lvl − target lvl)` % success) doesn't touch CON; a CON-0 corpse can't be raised.
  The C64 also puts the loss at death.
- **Decision (user s14):** DOS — CON lost at death.
- **Remaster:** `CombatResolver.DeathConLoss` via `ApplyPartyDamage` (traps too). RESURRECTION: DOS roll, first dead member
  with CON ≠ 0, HP := max, 4 MP spent even on failure, no CON change.
- **Side effects:** a character who dies and is never raised still loses CON (shows while dead; matters for the
  UNDEAD/DESTROYED fates). CON 0 = permanently dead. The writer for aging deaths isn't found `[~]`.
- **Spec:** `magic-spells.md` section Town casting.

## D-010 — Town HEALING range

- **Manual:** HEALING 1–4 restore **1–9 / 1–27 / 1–57 / 1–99** HP.
- **DOS:** combat (OV2 `0x027d`) and wilderness/dungeon (`0x4930`) use `(c·rng)/100`, rng 0..99, floor 1 = the manual.
  **Town** (OV1 `0x261a`) uses `rand / (32767/c)` with no floor (`c = 6·tier² + 4`) → **0–10 / 0–28 / 0–58 / 0–100**.
  The German C64 has a similar town/combat split, so it may be original design rather than a DOS slip.
- **Decision (user, 2026-10-10; kept s16 after the C64 check):** manual ranges everywhere.
- **Remaster:** `Magic.HealingAmount`, shared by every cast location.
- **Side effects:** town heals are never 0 and never reach DOS-town's top value.
- **Spec:** `magic-spells.md` section Town casting; C64: `reverse-engineering/c64/bytecode-notes.md` section HEALING.
- **Option `p1.town-healing`:** `manual` (the manual's ranges everywhere) · `dos`: a town HEALING draws a DOS word and heals `rand / (32767/c)`, no floor (0–10 / 0–28 / 0–58 / 0–100; `Magic.TownHealingAmountDos`). Combat and field HEALING keep the 1–9… ranges, as in DOS.

## D-011 — ATTACK / SLASH need enough attack skill

- **Manual (p.10):** ATTACK = two swings; SLASH = 3 or 4 quick swings, each less likely to hit and 1–2 less damage. No skill requirement.
- **DOS (OV2 `0x6f84`):** swings come from weapon-hit `@250`: ATTACK needs `@250 ≥ 75` (else THRUST); SLASH needs
  `@250 ≥ 150` and gives `@250/75 + 1` swings (else ATTACK, which can drop to THRUST). At level 1 nobody gets past THRUST.
- **Decision (user s20):** manual — no gate: ATTACK always 2 swings, SLASH 3, or 4 at `@250 ≥ 225` (= DOS count wherever
  DOS allows SLASH).
- **Remaster:** `CombatAction.SlashSwings` / `SlashFor`; the Client offers THRUST / ATTACK / SLASH.
- **Side effects:** per-swing hit chance is `@250/swings − defense`, so a weak character who picks SLASH does worse than with
  THRUST (the DOS gate protected against that). A HEALTH REPORT `A:` figure, if shown, reads 2 below 150 (DOS 1–2).
- **Spec:** `combat-resolution.md` section Action chooser.
- **Option `p1.attack-gate`:** `manual` · `dos`.
- **Amiga (s136, h71 `+0x608` / `+0x656`):** the same gate (record `+0xfa` = `@250`: ATTACK below 75 → THRUST, SLASH below
  150 → the ATTACK check), so it sides with DOS; seen live s132 (an ATTACK pick came back THRUST).

## D-012 — LUNGE allowed for Thieves too

- **Manual (p.10):** "LUNGE allows fighters to take one swing at a monster in Rank 2."
- **DOS (OV2 `0x6d2e`):** accepted for THIEF and FIGHTER; others get a rank-1 THRUST.
- **Decision (user s20):** manual — fighters only.
- **Remaster:** `CombatAction.CanLunge` / `LungeFor`: a non-fighter's LUNGE becomes a rank-1 THRUST.
- **Side effects:** tiny — thieves keep their any-rank THRUST/ATTACK/SLASH; LUNGE vs rank-2 THRUST differs by < 0.2 HP.
- **Spec:** `combat-resolution.md` section Action chooser.
- **Option `p1.lunge`:** `manual` · `dos`.

## D-013 — BINDING makes negative-defense monsters harder to hit

- **Manual:** BINDING "inhibits the movement of opponents, so those affected are easier to hit."
- **DOS (OV2 `0x1429`):** `defense := defense · pct / 100` (pct 85/70/55/45 for I–IV). Higher defense = harder to hit, and
  41 of 80 monsters have negative defense, so BINDING moves them toward 0 and makes them **harder** to hit
  (SKELETON −75 → −33 with IV).
- **Decision (user s23):** manual — BINDING always improves the party's hit chance, defense 0 included. Positive defense keeps
  DOS scaling; negative is pushed further down; a flat minimum treats `|defense|` as at least 20 (cut ≥ 3 / 6 / 9 / 11).
- **Remaster:** `Magic.BoundDefense`: `defense > 0` → `min(defense·pct/100, defense − minimum)`;
  `defense ≤ 0` → `defense − max(|defense|·(100−pct)/100, minimum)` (SKELETON −75 → −116, 0 → −11 with IV).
- **Side effects:** BINDING becomes useful against weak monsters; ~20 defense-0 monsters gain an effect; defense 1–19 gets
  slightly more than DOS's cut. Not raised (user: close enough): DOS cuts make III *equal* I + II, not "more than".
- **Spec:** `combat-resolution.md` section Monster record; `magic-spells.md`.
- **Option `p1.binding`:** `manual` · `dos`.

## D-014 — CONFUSION 4 doesn't fully stop monster specials

- **Manual:** CONFUSION 4 victims are "completely unable to do any spellcasting."
- **DOS (OV2 `0x1403`):** special gate × 80/60/40/**5** %; high-gate monsters keep a small chance (BLACK LORD 75 → 3).
- **Decision (user s23):** manual — IV sets the gate to 0.
- **Remaster:** `Magic.ConfusionPercent` = 80 / 60 / 40 / 0.
- **Side effects:** only top-gate monsters change (gates ≤ 19 already hit 0 in DOS).
- **Spec:** `combat-resolution.md` section Turn order; `magic-spells.md`.
- **Option `p1.confusion-iv`:** `manual` · `dos`.

## D-015 — STUNNING BLOW puts a just-killed member to SLEEP

- **Manual:** the dead stay dead until RESURRECTION.
- **DOS (OV2 `0xf85`):** special 34 sets SLEEP after the hit without checking for death — the corpse becomes SLEEP at HP 0,
  counts as alive, can die again (more CON loss) and blocks the party-wipe check.
- **Decision (user s26):** dead stays dead; only a survivor is stunned.
- **Remaster:** `MonsterSpecials.ApplyRider` skips DEAD targets. No other side effects.
- **Spec:** `combat-resolution.md` section Monster specials.
- **Option `p1.stunning-blow`:** `manual` · `dos`.

## D-016 — "[original text]" lowers weapon damage

- **Manual:** silent. Logged because the DOS message and effect disagree and the remaster changes DOS.
- **DOS (OV2 `0x187f`):** specials 17–20 print the message but scale **weapon damage** `@248` (likely an off-by-one field id).
- **Decision (user s26):** make the effect match the message — lower armor.
- **Remaster:** `MonsterSpecials` scales `CombatantPrep.ArmorEff` (`@246`) with the same formula and rolls.
- **Side effects:** the party is easier to damage instead of hitting softer; PROTECTION (not STRENGTH) now cancels it.
- **Spec:** `combat-resolution.md` section Monster specials.
- **Option `p1.armor-reduced`:** `armor` (the decision above) · `weapon` (the DOS side).

## D-017 — BEG FOR MERCY can fail

- **Manual:** BEG FOR MERCY lets the party surrender gold and some items, "then go free" — no condition.
- **DOS (OV2 `0x3bea`):** works only if `rand0_99 < Σreaction + 90` (~60–88 %); on a miss "[original text]".
- **Decision (user s27):** manual — always works.
- **Remaster:** `PartyOptions.Choose(BegMercy)` draws no roll.
- **Spec:** `combat-resolution.md` section Party options.
- **Option `p1.beg-mercy`:** `manual` (always works, no roll) · `dos` (`rand0_99 < Σreaction + 90`, else "[original text]" and orders, like a refused THREATEN).

## D-018 — Found-item capacity

- **Manual:** "The party can carry up to 90 items they find."
- **DOS:** the found-item list has **60** slots; BEG clears slots 0, 2, …, 48, leaving holes.
- **Decision (user s27):** manual — 90. The inventory screen uses this sack as the party stash.
- **Remaster:** `LootSack.MaxItems = 90`; the slots keep DOS's holes ([D✓] s65), so BEG empties slots 0, 2, …, 48 in place.
- **Side effects:** a sack over 60 items blocks DOS export with a message (P1.4).
- **Spec:** `combat-resolution.md` section Party options.
- **Option `p1.sack-size`:** `manual` (90) · `dos` (DOS's 60 slots, `LootSack.Capacity`). The holes stay either way; the inventory screen's SACK header shows the save's capacity.

## D-019 — Monsters that run away still pay all their gold

- **Manual:** gold shown at the end of combat is "any gold retrieved from the monsters".
- **DOS (OV2 `0x3b98`):** the reward is every monster's gold, summed at build; a monster run ends combat normally, so runners
  pay full gold (plus kill XP).
- **Decision (user s32):** DOS — full gold.
- **Remaster:** `PartyOptions.MonstersRan` pays `RewardGold`; the same for every "MONSTERS RUN" end (morale, DISPELL, FEAR).
- **Spec:** `combat-resolution.md` section Party options.

## D-020 — SLEEP lasts only the rest of the round; resist ignores the caster

- **Manual:** SLEEP victims "cannot attack, parry, or cast spells"; spells last for the combat; a second SLEEP doesn't deepen
  the trance; effectiveness depends on caster power and level and target strength.
- **DOS (OV2 `0x1501`):** per monster, resists if `rand < L²`; otherwise "done this round" only. Every monster resists once
  L ≥ 10; the caster plays no part.
- **Decision (user s32):** manual, with a rule built from DOS parts:
  - Resist: one roll per group, the CONFUSION/WEAKNESS/BINDING roll `rand ≤ 5·(caster MP max − group CV) + 50` (+ D-028's level term).
  - Duration: monster state "asleep" — no turns, wakes on `rand < 15` per pass, lasts across rounds; a second SLEEP skips sleepers.
  - Sleepers can be hit by melee and spells and stay asleep when damaged.
- **Remaster:** `SpellCaster` SLEEP + `CasterHitsGroup`; `MonsterStatusFlags.Asleep` (saved in combat snapshots).
- **Side effects:** RNG draw order differs from DOS.
- **Spec:** `combat-resolution.md` section Status effects.
- **Option `p1.sleep`:** `manual` · `dos`: OV2 `0x1501`, per living monster in rank order one discarded draw then `r`; `r ≥ L²` → "%s IS SLEPT" and done for the rest of the round only (no lasting asleep state, no caster roll; `L ≥ 10` never works).

## D-021 — FEAR doesn't make monsters run

- **Manual:** FEAR "frightens the monsters into running away in terror"; a second FEAR makes them more afraid.
- **DOS (OV2 `0x80c`):** on `rand < 5·(caster lvl − L) + 50`, encounter level `L −= 6` (stacks). No run. Its real use is
  setting up DISSOLVE.
- **Decision (user s32, s34):** manual — keep the DOS roll and `L −= 6`, then the monsters run (kill XP + full gold, D-019).
  Runners stop for the whole round. The Black Lord (`never_flees`) doesn't run, so FEAR + DISSOLVE still works on him.
- **Remaster:** `CombatSpells.Fear`; `Monster.NeverFlees`.
- **Side effects:** FEAR becomes a reliable way to collect full gold + kill XP without killing everything.
- **Spec:** `combat-resolution.md` section Combat spell effects.
- **Option `p1.fear`:** `manual` · `dos`.

## D-022 — CHARM doesn't stop the fight

- **Manual:** CHARM makes monsters "forget they are in combat."
- **DOS (OV2 `0x767`):** on success +15 reaction and GREETINGS can be retried; the fight goes on.
- **Decision (user s32):** manual — same roll; on success the fight ends at once like GREETINGS (kill XP kept, no gold, no treasure).
- **Remaster:** `CombatSpells.Charm` → `SpellCombatEnd.Charmed`.
- **Side effects:** the +15 reaction effect is gone; a CHARMed forced fight (fight cell / lock ambush) returns on the next step onto that cell.
- **Spec:** `combat-resolution.md` section Combat spell effects.
- **Option `p1.charm`:** `manual` · `dos`: same roll and "[original text]"; Σreaction += 15 (stacks) and GREETINGS works again after a hit; the fight goes on.

## D-023 — TELEPORTATION can fail and the round still plays out

- **Manual:** TELEPORTATION "teleports the party safely away from the combat."
- **DOS (OV2 `0x7b8`):** succeeds on `rand < 3·lvl + 50`; the escape happens at the next round top, so everyone after the caster still acts.
- **Decision (user s32):** manual — always works, at once. Kill XP kept, no gold, treasure forfeited (as DOS).
- **Remaster:** `CombatSpells.Teleport`; `SpellCombatEndExtensions.EndsAtOnce`.
- **Spec:** `combat-resolution.md` section Combat spell effects.
- **Option `p1.teleport`:** `manual` · `dos`: works on `rand < 3·lvl + 50`, no message either way; the round plays out (monsters still act, the party can still be wiped) and combat ends at the next round top (`SpellCombatEnd.TeleportedAtRoundTop`) with kill XP, no gold, no treasure and no fight-cell clear, as DOS's `[0x74b0] = 1`.

## D-024 — DISPELL UNDEAD also hits demons and BANTIR

- **Manual:** affects "skeletons, zombies, ghosts, and other creatures who have returned from the grave."
- **DOS (OV2 `0x8fc`):** "undead" = XP divisible by 7, which also catches BANTIR, DEMON and HIGH DEMON.
- **Decision (user s32):** manual — `undead` is a data flag (`monster-traits.json`): SKELETON, ZOMBIE, GHOUL, LARGE SKELETON,
  WIGHT, WRAITH, VAMPIRE, GHOST, SPIRIT.
- **Remaster:** `Monster.Undead` → `CombatSpells.IsUndead`. Rolls and outcomes unchanged.
- **Spec:** `combat-resolution.md` section Combat spell effects.
- **Option `p1.dispell-undead`:** `manual` · `dos`.

## D-025 — Party defeat: the Great Demon decides each member's fate

- **Manual:** silent on party defeat.
- **DOS (OV1 `0x3bc`):** per member: CON 0 → DESTROYED; else level > 2 and `rand/0x2000 < level − 2` → RESURRECTED; else
  `rand/0x2aab < level` → UNDEAD (race 15, level 20); else DESTROYED. Gold and items lost; survivors to OUT1 (20,5).
- **Decision:** NOT A CONFLICT — DOS rule. **Remaster:** `PartyDefeat.Resolve` (DOS odds and draw order).
- **Spec:** `endgame.md`.

## D-026 — The game is won by killing the Black Lord, not Nikademus

- **Manual (p.6):** frames the quest as "destroying Nikademus".
- **DOS (OV2 `0x4202`):** victory = killing the BLACK LORD (DISSOLVE counts) → Zeus screen; Nikademus never appears and the
  game continues afterwards.
- **Decision:** NOT A CONFLICT (story framing) — DOS rule. **Remaster:** `GameSession.Endgame` (`ResolveVictory`).
- **Spec:** `endgame.md`.

## D-027 — Dungeon re-entry: monsters and locks reset by time?

- **Manual (p.14):** on re-entry "the monsters may be prepared for you — new locks installed and new monsters recruited… if
  you return soon after you left the monsters you killed might still be dead."
- **DOS ([C], DOSBox-confirmed):** one save slot (`DNG.SAV` + `DNGX.SAV`), written only by "[original text]" YES →
  "[original text]" YES. Entry loads it only when its dungeon id matches; otherwise the original
  level. **No time value is read**: a saved level is restored exactly; an unsaved one is fully reset. NO writes nothing, so
  an older save of the same dungeon can come back. Wandering fights continue in a restored dungeon. Utility → Reset the
  game zeroes the slot `[~]`.
- **Decision (user s37):** DOS, except that NO purges a slot holding the same dungeon.
- **Remaster:** `DungeonSaveSlot`: YES overwrites, re-entry loads on id match, NO purges a same-dungeon slot. Defeat, the win
  and prompt-less exits touch nothing.
- **Side effects:** NO always means "next entry is the original level".
- **Spec:** `endgame.md` section Q6; `dungeon-representation.md` section Leaving and re-entering.
- **Option `p1.dungeon-map-no`:** `manual` · `dos`.

## D-028 — Spell success ignores the caster's level

- **Manual (p.14):** effectiveness depends on "the spellcaster's magic power and experience level, as well as the strength of the target."
- **DOS (OV2 `0x13b5`):** CONFUSION / WEAKNESS / BINDING hit if `rand ≤ 5·(caster MP max − group CV) + 50` — no level term.
- **Decision (user s34):** manual; remaster formula
  `chance = 5·(caster MP max − group CV) + 2·(caster level − group level) + 50`, hit if `rand0_99 ≤ chance`. Equal levels = DOS.
- **Remaster:** `SpellCaster.CasterHitsGroup`, `CasterLevelWeight = 2` (tuning knob).
- **Side effects:** SLEEP (D-020) uses the same roll. Example rates DOS → remaster: level-1 Wizard vs TROLL 51 % → 35 %;
  level 15 vs HIGH DEMON 71 % → 77 %. Against the **Black Lord** (L21) the level term is always ≤ −12, so these spells get
  weaker on the final boss (level 15: 46 % → 34 %). FEAR + DISSOLVE doesn't use this roll.
- **Spec:** `combat-resolution.md` section To-hit.
- **Option `p1.spell-level`:** `manual` · `dos`.

## D-029 — FIREFLASH damage scales with INT

- **Manual (p.15):** FIREFLASH 1–4 = 1–10 / 1–30 / 1–60 / 1–100 damage.
- **DOS (OV2 `0x339`):** `rand·param·INT/2000 + 1`; at INT 18 that is 1–9 / 1–26 / 1–52 / 1–90 (the manual needs INT ≈ 20,
  above every race maximum).
- **Amiga (s134, h35 `+0x2f0`):** the same formula and params 9 / 29 / 58 / 100; its roll runs 0…100 (`rand / 327`), so
  at INT 18 the tops are 9 / 27 / 53 / 91: still below the manual's. It sides with DOS; DOS wins on numbers, not built.
- **Decision (user s34):** manual ranges, still scaled by INT:
  `damage = 1 + rand0_99 · (max − 1) · min(INT, 18) / (99 · 18)`. INT 18+ gives exactly the manual range; INT 13 tops at
  7 / 21 / 43 / 72, INT 16 at 9 / 26 / 53 / 89.
- **Remaster:** `Magic.FireflashDamage`, `FireflashFullInt = 18`.
- **Side effects:** top damage is above DOS at every INT. Target choice → D-059.
- **Spec:** `magic-spells.md` section Effect coefficients.
- **Option `p1.fireflash-damage`:** `manual` · `dos`.

## D-030 — A failed monster run wakes SLEEP'd monsters

- **Manual:** silent; under D-020 sleepers wake only on the 15 % roll.
- **DOS (OV2 `0x3e19`):** a failed monster run marks every living monster awake, sleepers included.
- **Decision (user s34):** sleepers stay asleep. **Remaster:** `MonstersLoseRound` leaves `Asleep` alone.
- **Spec:** `combat-resolution.md` section Party options.
- **Option `p1.failed-run`:** `manual` · `dos`.

## D-031 — Wilderness treasure isn't automatic

- **Manual (p.10):** monsters defeated in the wilderness or surrendering in dungeons "automatically give away their treasure."
- **DOS (OV2 `0x41aa`):** a wilderness win or surrender drops treasure only if `rand0_99 ≤ 2·L + 20`. Dungeon rules match the manual.
  Also a bug: the "SCROLL 17 only in DNG9" guard tests the unshifted id, so FIRE ELEMENTAL drops the win scroll anywhere 20 % of the time.
- **Decision (user s34):** manual — wilderness treasure always drops; fix the SCROLL 17 leak.
- **Remaster:** `TreasureDrop` + the KEEP IT? prompt (`GameSession.TreasurePrompt`). SCROLL 17 drops only in the Fortress;
  those FIRE ELEMENTAL drops give nothing.
- **Side effects:** monsters that run in the wilderness leave their items too; wilderness items are 2.5–5× as common as DOS.
- **Spec:** `combat-resolution.md` section Outputs / rewards.
- **Option `p1.wild-treasure`:** `manual` · `dos`: a wilderness win or surrender drops treasure only on `rand0_99 ≤ 2·L + 20`, and the SCROLL 17 guard tests the unshifted id, so FIRE ELEMENTAL drops SCROLL 17 anywhere 20 % of the time. What that scroll does outside the Fortress is still `[?]` (P0.8).

## D-032 — A summoned elemental fights with zero stats

- **Manual:** [original text] calls up an elemental "to fight for the party."
- **DOS (OV2 `0x3263`):** combat prep stops at the party size at fight start (6), so slot 7 keeps zero attack, damage and armor (a bug).
- **Decision (user s34):** "If it's broken, fix it."
- **Remaster:** `ElementalPrepRule.Prepared` (default): the elemental gets its record's values (FIRE 200/40/7, WATER 150/30/14, EARTH 100/20/21).
- **Option `p1.elemental-prep`** ("SUMMONED ELEMENTAL"): FULL STATS · ZERO STATS (DOS).
- **Side effects:** [original text] becomes a real combat spell. Because prep now covers slot 7, buffs cast on the elemental
  (QUICKNESS/STRENGTH/PROTECTION) are rebuilt each fight like the party's; in DOS they lasted the whole trip, since prep never
  touched slot 7 (static s58: writers of `@246/@248/@250` = buff spells OV2 `0x4b3..0x5c5`, prep, Guild copy `0x27e1`).
- **Spec:** `combat-resolution.md` section [original text].

## D-033 — When [original text] works

- **Manual:** "The elemental will only serve a party of six adventurers."
- **DOS (OV2 `0x8cc`):** tests a word that is "last living index" after orders but "living count" after a monster turn, so a
  pass-3/5 cast works with **5** members and fails with 6.
- **Decision (user s34):** manual — six non-DEAD members (sleepers count), every pass. **Remaster:** `CombatSpells.SummonElemental`.
- **Spec:** `combat-resolution.md` section [original text].
- **Option `p1.summon-party`:** `manual` (six non-DEAD members, every pass) · `dos`: DOS's living list `[0x64a2]` and word `[0x6490]` (`Party.DosLiving`, kept for the session and saved, updated whatever the options): the `== 5` test (six non-DEAD after the orders phase, five once a monster has taken a turn; RESURRECTION only adds 1, so it keeps the meaning), the list entry written one place too far after a monster turn (entry 5 stays stale), and the second summon of an elemental already fighting (seven members, one DEAD: only the word and list change). A `manual` summon writes nothing to the list; the next rebuild (monster turn, orders phase) lists the elemental.
- **Option `p1.monster-spell-target`** (user s172: a toggle of its own, apart from `p1.summon-party`): who the single-target monster specials 1–4 (OV2 `0x1731`) hit. `manual`: any non-DEAD member, `rand·count/100` (no manual rule on targets). `dos`: `list[rand·[0x6490]/100]` as they stand: before any monster turn in a round with orders the word is the last index, so the last listed member can't be the target; a round entered by a failed Flee (or a surprise opening) has no orders phase, so the word keeps last round's count; a stale DEAD entry (only after a `p1.summon-party` `dos` summon in a count phase, or its second summon) wastes the spell. So the stale-entry effects need both options on `dos`. `[?]` only the start-up values (BSS memory; Core starts empty, word 0; an entry never written gives no target).

## D-034 — Surprised fights skip PARTY OPTIONS in round 1

- **Manual:** describes AN ENCOUNTER → PARTY OPTIONS; never mentions surprise.
- **DOS (OV2 `0x33ab`):** PARTY OPTIONS only in a normal opening; surprised / surprising / stalled openings go straight to round 1.
- **Decision (user s34):** NOT A CONFLICT — the manual describes the normal case. **Remaster:** DOS rule (`Surprise`).
- **Spec:** `combat-resolution.md` section Surprise.

## D-035 — Town Cast spell offers combat spells (MP spent, no effect)

- **Manual (p.12–13):** C)AST SPELL in town only works for spells allowed there (non-combat/wilderness and everywhere spells).
- **DOS (DOSBox s35):** every known spell is listed; a combat spell cast in town spends the MP and does nothing.
- **Decision (user s35):** manual.
- **Remaster:** `SpellCaster.CastOutOfCombat` refuses, before spending MP, combat spells and spells cast in the wrong place
  (`IsCastableAt`: MONSTER EVAL. wilderness/dungeon, VISION wilderness, TRANSPORTATION town; E spells everywhere). The
  Client lists only castable spells. `spells.json` MONSTER EVAL. category corrected to `N/WD`.
- **Side effects:** VISION can't be cast in dungeons.
- **Evidence:** `reverse-engineering/dos/working/capture_s35_2026-10-11.md` section 6.
- **Option `p1.cast-list`:** `manual` · `dos`: out-of-combat Cast lists every known spell (`SpellCaster.OutOfCombatChoices`) and refuses none; the MP is spent first and only DOS's switch cases do anything (`HasDosOutOfCombatEffect`, [C]: town OV1 `0x254e` HEALING, RESURRECTION, TRANSPORTATION; wilderness / dungeon OV2 `0x4790` HEALING, RESURRECTION, MONSTER EVAL., VISION in the wilderness). Any other spell, combat spells included, spends its MP for nothing, with no message.

## D-036 — Copy-protection quiz in dungeon 1

- **Manual:** never mentions it (the answers come from its item tables).
- **DOS:** asks "What is the item number of a SWORD? (page 40,41)" (also seen as "SWORD +7"); the trigger isn't tied to a map cell `[?]`.
- **Decision (user s35):** not included.
- **Remaster:** nothing; dungeon data must skip the check if it turns up.

## D-037 — Town-entry loot payout

- **Manual:** XP and gold are divided among party members on entering a town.
- **DOS:** on town entry it asks shares (1–3, a DEAD member 0–3) per member; gold shares go to each member's bank, XP shares
  to each member; remainders dropped.
- **Core:** equal split among living members, gold to the party purse, remainder to the first survivor.
- **Decision (user s35):** keep Core's payout.
- **Side effects:** no share prompt or bank deposit on entry (Bank Deposit does the share split, D-049). The split runs on
  town entry, as the manual, DOS, the ST II and the Amiga I do (`GameSession.EnterTown`, I and II). Found items still wait for Distribute; with nobody living the pool waits too.
- **Option `p1.payout-remainder`:** `first-survivor` (Core's payout) · `dropped` (the remainders are lost, as DOS). Q2 only. The DOS save export's automatic payout of the fight XP/gold follows it too; its split of the carried purse into the banks keeps the remainder with the first member either way.

## D-038 — Leaving a dungeon only through the door you came in by

- **Manual (p.14):** "LEAVING THE DUNGEON is done through the same door used when the party entered."
- **DOS (OV2 `0x2d46`):** any exit cell uses its own exit record. DNG7 has a one-way second exit to OUT8 (12,5), which has no
  entrance; DNG8 and DNG9 have two inside doors leading to one outside entrance.
- **Decision (user s37):** DOS — the manual is wrong.
- **Remaster:** `DungeonLevel.ExitTarget` / `AnswerDungeonExit` use exit record `k`. Ordinary exits are unchanged.
- **Side effects:** none known (the axis meaning of the record stays `[~]` but matches every entrance).

## D-046 — Wilderness inn can raise MP max one above the manual's maximum

- **Manual:** a table of maximum MP per class and level; inns "sometimes … increase spellcasters' maximum magic points".
- **DOS (OV2 `0x67a3`):** raises MP max by 1 when `rand0_99 <= 20·(table − MP max)`; at gap 0 a roll of 0 passes, so 1 % of
  stays go to table + 1. The town Inn uses `<`.
- **Decision (user s37):** manual. **Remaster:** `WildernessInn.Stay` uses `<`, never past the table.
- **Side effects:** odds below the cap drop by 1 % (gap 1: 20 % instead of 21 %).
- **Spec:** `town-menus.md` section Wilderness inn.
- **Option `p1.inn-magic-cap`:** `manual` · `dos`.

## D-047 — Wilderness inn skips DEAD members

- **Manual:** independent inns, "like the inns in town", restore every character's HP and MP.
- **DOS (OV2 `0x6782`):** skips a DEAD member entirely; the town Inn refills the dead too.
- **Decision (user s37):** manual. **Remaster:** `WildernessInn.Stay` refills a DEAD member's HP/MP (still DEAD, no line
  shown, no MP-max roll); under D-008 the dead member still ages.
- **Side effects:** only the dead member's stored HP/MP.
- **Spec:** `town-menus.md` section Wilderness inn.
- **Option `p1.wild-inn-dead`:** `manual` · `dos`.

## D-048 — Gold cap

- **Manual (p.10):** the party carries up to 65,000 GP; one bank account holds up to 65,000 GP.
- **DOS:** clamps at **65,535** and the excess is lost (Withdraw takes the full amount from the account first).
- **Decision (user s40):** manual — 65,000.
- **Remaster:** `Party.GoldCap` / `Party.BankGoldCap` = 65,000, applied wherever gold is added (Withdraw, Armory sale,
  town-entry payout, Deposit, combat gold into the sack). Gaps filled from DOS: the excess is lost silently; Withdraw takes the
  full amount from the account first.
- **Option `p1.gold-cap`:** `manual` 65,000 (Original (manual) preset, the default) · `dos`
  65,535 (Original (DOS) preset) · `lifted` = no practical cap, 2,147,483,647 (`int.MaxValue`; Remaster preset). It sets the
  purse, every bank account and the found-gold sack together. Under `lifted` the Withdraw amount field takes any amount up
  to the balance (DOS's field, and the other two values, stop at 65535).
- **Side effects:** found gold (`LootSack.Gold`) is capped separately from the purse, so out of town the two can hold up to
  twice the cap together until they merge in town. The DNG script gold op keeps its 16-bit clamp under every value. A DOS
  import over the cap is kept and listed as a rule break (checked against the running save's cap: 65,000, 65,535 under
  `dos`, never under `lifted`); an add can't raise it further. **DOS export** under `dos` / `lifted`: the automatic split into the banks stops at 65,535
  (the excess of a share is lost and listed), and a balance already above 65,535 is written as 65,535 and listed (the u16
  clamp) — the file stays valid; the live game keeps its gold.
- **Spec:** `town-menus.md` section Bank; `quirks-and-bugs.md` Q1.

## D-049 — What Bank Deposit does

- **Manual (p.10):** D)EPOSIT splits all carried gold among members by shares (1, 2 or 3 each).
- **DOS:** the same (OV1 `0x29f1` calls the town-entry share split). Core had a per-member amount deposit instead.
- **Decision (user s40):** manual. **Remaster:** `GameSession.BankDeposit(shares)` → `TownMenus.DepositShares`; the Client asks
  "Shares for: NAME" per member and lists each account's deposit. Amount deposit removed.
- **Gaps filled from DOS (manual silent):** a DEAD member may get 0–3 shares; the remainder is dropped; with no gold nothing is
  asked. Gold only (XP is paid on town entry, D-037). Each account capped at 65,000 (D-048).
- **Spec:** `town-menus.md` section Bank.

## D-050 — Training screen hides the cost while XP is short

- **Manual:** "you will be told how many experience points are required and how much the training will cost."
- **DOS (`0x3b35`):** while XP is short it shows only "You need N more [original text] GO AND LEARN".
- **Decision (user s40):** manual. **Remaster:** `TownMenus.Train` says "You need N more [original text] It will cost G gold."

## D-051 — Magical pool raises stats with no cap

- **Manual (p.8):** attributes range 3–22, capped by race ("Maximum Value for Attributes by Race").
- **DOS (OV2 `0x656b`):** each living member who hasn't used this pool kind gets `+isqrt(@10)·@10/11` (1–3) to a random stat,
  with no cap — three pools can add +9, past the race max and past 22.
- **Decision (user s40):** manual. **Remaster:** `MagicalPool.Apply` stops at the race maximum (`raceMod + 8`).
- **Gaps filled (manual silent):** a member already at the maximum still uses the pool and "IS CHANGED" (keeps DNG5's
  three-pool check reachable); an undead member uses its birth race's maximum; a value already above the max (DOS import) is kept.
- **Spec:** `overworld-movement.md` section Magical pool.
- **Option `p1.pool-cap`:** `manual` · `dos`.

## D-052 — Trap menu labels and who may disarm

- **Manual (p.13):** "THIEF SPOTS A TRAP / D)ISARM OR L)EAVE", then asks who will disarm.
- **DOS (OV2 `0x2f8e`):** "%s SPOTS A TRAP. DISARM IT ?" YES/NO; YES asks "[original text]" and lists only status-OK members.
- **Decision (user s40):** manual labels. **Remaster:** "%s SPOTS A TRAP" with **D)ISARM** / **L)EAVE** (letter keys);
  "[original text]" (DOS wording).
- **Gap filled from DOS (manual silent):** only status-OK members can disarm.
- **Amiga I:** the Amiga asks "%s [original text] IT?[YES|NO]" in requester 0 (h41 `+0x11c`). Its buttons
  are font text, so (as D-064's Amiga note) the manual's labels apply there too: the remaster's requester 0 shows the
  Amiga's three lines with the buttons DISARM / LEAVE (D / L, or a click). Kept by the user (s157).
- **Spec:** `dungeon-representation.md` section Traps.

## D-053 — End-of-combat XP/gold display and when shares are cut

- **Manual (p.15):** the encounter's XP and gold are shown at the end of combat and divided among members on entering a town.
- **DOS (OV2 `0x3fbe`):** divides at once, shows only the per-survivor share ("[original text]"), XP in tens; remainders lost.
- **Decision (user s42):** manual. **Remaster:** `GameSession.DepositEncounterTotals` puts the whole XP and gold in the loot sack
  and shows "{xp} [original text] AND {gold} GOLD" (remaster wording); the town-entry split (D-037) divides it.
- **Side effects:** no per-fight remainder loss (up to 10n−1 XP and n−1 GP more per fight than DOS); pool still capped at 655,350.
  BEG FOR MERCY (DOSBox s65): DOS keeps the purse's remainder after dividing it among the survivors (556 GP, 6 survivors → 4
  left); with no per-fight split the remaster's BEG takes all the gold, as the manual's "surrenders its gold" says.
- **Spec:** `combat-resolution.md` section Outputs.
- **Option `p1.combat-shares`:** `manual` (totals, no remainder loss) · `dos`: the end of combat shows `⌊XP/(10n)⌋·10` XP and `⌊gold/n⌋` GP, "[original text]", and pools only `n` shares (`GameSession.SurvivorShares`); after BEG the shown gold is `−⌊purse/n⌋` and `purse mod n` stays, as found gold. DOS's purse `[0x63f6]` is one word holding found and bank-withdrawn gold; Core's found gold is the faithful home for the leftover: DOS-mode fight gold goes there too, the found-gold screen shows `[0x63f6]`, a party defeat zeroes `[0x63f6]` as Core zeroes found gold, and town entry pays found gold into the carried purse.

## D-054 — Armory restock: which items can disappear between visits

- **Manual (p.10):** rings, scrolls and other treasures stay; "Combat weapons and potions, however, may appear and disappear."
- **DOS (OV1 `0x8e1`, at town entry):** 18 random wipes over ids 0..119 — weapons **and armor and shields**, plus HEALING/MAGIC
  potions — then 4 restocks (set to 1) from the town's item list. (The s35 capture saw Pelnor SM SHIELD 5 → 0.)
- **Decision (user s42):** manual — only weapons and potions change; armor and shields stay.
- **Remaster:** `ArmoryRestock.Apply` draws the same 18 + 4 random numbers as DOS. Wipes only hit weapon ids
  (41..100) and HEALING/MAGIC potions. A restock draw on armor or a shield sets it to 1 only when it is sold out, and never
  lowers a stack (user s57).
- **Option `p1.armory-restock`:** `manual` (the rule above) · `dos` (armor and shields are wiped and cut to 1 too,
  `ArmoryRestock.Apply(dosRule: true)`). Both draw the same random numbers.
- **Side effects:** armor and shields that were bought out return through the town's restock list, like weapons. Armor
  that isn't on the list stays gone once sold out, as in DOS.
- **Spec:** `town-menus.md` section Armory restock.

## D-055 — Path travel time

- **Manual (p.25):** path: "passing through takes less time relative to the other types of terrain"; paths have "a variety" of encounters.
- **DOS (OV2 `0x5f88`):** path (class 6, confirmed by "[original text]") costs 60 per step — cheaper than land classes 0–3
  (75 / 150 / 225 / 300) but **equal to** water, bridge (tile 74 `[~]`) and classes 8/9. DOS gives the "variety" encounter
  mix to class 0; class 6 gets humanoid / animal / magic users (not raised separately).
- **Decision (user s42):** manual — path is faster than every other terrain.
- **Remaster:** `travel-time.json` charges class 6 **30** per step (user s57; it is data). `TravelTimeRules.Dos` keeps 60.
- **Spec:** `overworld-movement.md` section Terrain classes.
- **Option `p1.path-time`:** `manual` · `dos`.

## D-056 — MONSTER EVALUATION

- **Manual (p.15):** "determines the approximate experience level of nearby monsters. Although it occasionally yields
  inaccurate results…"
- **DOS (OV2 `0x4860`):** no roll, no look at real monsters — wilderness `L = min(2·(tile%10 + 1), 12)`, dungeon `L = 2·tier + 2`.
- **Decision (user s42):** manual — reads nearby monsters, sometimes wrong.
- **Remaster:** `SpellCaster.ManualMonsterEvalLevel`: the rounded average level of the monsters that can be met
  where the party stands (wilderness: the cell's 12-monster block `12·T`; dungeon: the level's wandering range `T..T+12`);
  **10 %** of casts read off by ±1 (never below 1; user s57). The DOS formula stays as
  `MonsterEvalLevel`.
- **Spec:** `magic-spells.md`.
- **Option `p1.monster-eval`:** `manual` · `dos`.

## D-057 — VISION

- **Manual (p.16):** VISION sees "in all directions for hundreds of miles", useful for "mapping vast unknown areas".
- **DOS (OV2 `0x48b3`):** wilderness only; reveals a square of radius `(MP_left·100 + 500)/400` (1–3 cells) inside the current 26×20 region, once.
- **Decision (user s42):** manual — a very large area.
- **Remaster:** the whole current 26×20 region is revealed (`SpellCaster.VisionManualRadius`, user s57); it does not reach
  into neighbouring regions; in a dungeon it still cannot be cast (D-035). DOS radius kept as `VisionRadius`.
- **Spec:** `magic-spells.md`.
- **Option `p1.vision`:** `manual` · `dos`.

## D-058 — No "AN ENCOUNTER" message before a fight

- **Manual (p.9):** when party and monsters meet, "a message will flash on the screen: AN ENCOUNTER."
- **DOS:** no "AN ENCOUNTER" string; only "[original text]" before night wilderness fights. Day and dungeon fights flash nothing.
- **Decision (user s46, s53):** manual, worded by setting: "[original text]" / "A DAY ENCOUNTER" as appropriate.
- **Remaster:** `GameSession.EncounterBanner` before every fight: night branch (`NightEncounter`, first roll < 35) → "A NIGHT
  ENCOUNTER"; every other wilderness fight → "A DAY ENCOUNTER"; dungeon fights → "AN ENCOUNTER". Script text such as
  "MONSTERS APPEAR" still comes first.
- **I under AMIGA SCREENS:** the Amiga shows "[original text]" in requester 23 over the map (h74 `+0x6c`); it has no day
  or dungeon flash, nor any box before fixed / tile-class fights. Boxing the day flash like the night one added ~1 s
  before the fight.
- **Decision (user s140, final):** "Do whatever shows it but doesn't slow things down."
- **Remaster:** the night flash stays the Amiga's box. "A DAY ENCOUNTER" (rolled, fixed and tile-class
  wilderness fights) and the dungeon's "AN ENCOUNTER" are no box: they print on the combat screen's line (y 185, z6font)
  from the fight's start, over sound 17's black and the set-up, and stay until the fight's first line prints or its first
  run ends. No wait is added. Side effect: in a surprise opening with the Sound switch off (no black) it shows only during
  the set-up, a fraction of a second `[~]`. Amiga `FORMAT.md` "The map screen's requester 23" (D-058's flashes).
- **Spec:** `combat-resolution.md` section Surprise.

## D-059 — FIREFLASH target

- **Manual (p.15):** FIREFLASH hits "one monster close to the spellcaster".
- **DOS (OV2 `0x361`):** hits the rearmost group with a living monster; in a group of 2+ the last monster is never picked.
- **Amiga (s134, h35 `+0x2f0`):** the same walk as DOS (back rank first), so it sides with DOS against the manual. Its
  percent roll is 0…100 (h71 `+0x103a` = `rand / 327`), so 68 times in 32768 it picks the last monster outright. DOS wins on
  numbers: not built (Amiga `FORMAT.md` "The combat screen").
- **Decision (user s53):** manual — a monster in the front rank (first rank with a living monster), picked evenly; target before damage.
- **Remaster:** `SpellCaster.PickFireflashTarget`. D-029 (damage) unaffected.
- **Spec:** `combat-resolution.md` section FIREFLASH target.
- **Option `p1.fireflash-target`:** `manual` · `dos`.

## D-060 — APPLETON can't be entered from the wilderness

- **Manual (p.8):** "the 11 initial towns".
- **DOS:** APPLETON's entrance record is at OUT13 (17,19), an **inn** tile, so walking there opens the inn, never the town
  (likely a map-data slip). TRANSPORTATION still reaches it.
- **Decision (user s53):** manual — every initial town can be walked into. **User s54:** keep the inn, moved somewhere convenient.
- **Remaster:** `map-patches.json` makes OUT13 (17,19) a town tile and puts the inn (tile 41) at OUT13 (4,14), on the path
  beside the region's dungeon entrance (3,14). Both are drawn on the Amiga map.
- **Side effects:** the region's wilderness inn is now a rest stop at the dungeon instead of on the town's own cell.
- **Spec:** `overworld-movement.md`.

## D-061 — USE ITEM on rings / wand / treasure

- **Manual (p.12):** U)SE ITEM can "read a scroll, take a magic or healing potion, and find the unusual properties of an item."
- **DOS (OV1 `0x30f1`):** potions and scrolls only; every other item prints "UNUSABLE".
- **Decision (user s53):** manual — potions and scrolls as DOS; every other item shows a detailed description (damage / armor
  value, strength needed, sale value, plus remaster-written lore).
- **Remaster:** `ItemRules.Describe` + `item-descriptions.json`; town Option → Use item.
- **Spec:** `endgame.md` section 6.1.

## D-062 — TRANSPORTATION destinations

- **Manual:** "transports the party from one town to another" (p.20); "the 11 initial towns" (p.8).
- **DOS (COM `0x16a2`):** a cursor list of all 12 town records, the current town included; the 12th (OLYMPIA) prints blank.
- **Decision (user s53, confirmed s57):** manual — the current town is not offered. The blank 12th entry stays: the manual doesn't limit the
  list to 11, and it is the only route to Olympus and PHANTASIA.
- **Remaster:** `GameSession.TransportDestinations`; the Client list skips the current town.
- **Spec:** `endgame.md` section 2.4.
- **Option `p1.transport-list`:** `manual` · `dos` (all 12 towns, the current one included). Picking the current town runs the full town entry (aging, Armory restock), as DOS's `0x1954(pick)` (`endgame.md` section 2.4).

## D-063 — Non-combat spell picked in combat

- **Manual:** the spell table marks MONSTER EVAL., VISION and TRANSPORTATION **N** (non-combat); it doesn't say what happens if one is tried.
- **DOS ([C], COM `0x139c`, OV2 `0x6d75`):** the combat picker lists every known spell; casting one of these prints "%s CASTS
  A SPELL", spends the MP, and does nothing.
- **Decision (user s53, confirmed s57; DOS only where the manual is silent):** the N category means they can't be cast in combat.
- **Remaster:** the combat picker lists only combat and everywhere spells; a typed non-combat number becomes PARRY, no MP spent.
- **Spec:** `combat-resolution.md`.
- **Option `p1.combat-spell-list`:** `manual` · `dos`: the combat picker lists every known spell (`SpellCaster.IsCombatCastable`); MONSTER EVAL., VISION or TRANSPORTATION cast in combat prints "%s CASTS A SPELL", spends the MP and does nothing.
- **III:** DOS III (`0xfc41`) and the Amiga refuse a non-combat or unaffordable pick with a message and Thrust (R1),
  never "spends the MP and does nothing". **Decided (user s129):** the manual holds in every game; where a game's DOS
  version differs, that game gets its own option. III's option `p3.combat-spell-list` (`manual` = only combat and
  everywhere spells listed; `dos` = every spell listed, Original (DOS) preset) drives III's own list and the Amiga spell
  window alike; a typed or picked non-combat spell is refused with DOS III's message and a Thrust under both values, no
  MP spent (`P3CombatModule.CastRefusal`). I's `p1.combat-spell-list` is I's only.

## D-064 — Character sheet wording

- **Manual (example sheet):** "OKAY", "EXPER. POINTS", equipment on the same page.
- **DOS:** "OK", "EX. PTS", equipment on a separate page.
- **Decision (user s53):** manual. **Remaster:** `InspectDialog` main page shows OKAY (also in party lists), EXPER. POINTS and an
  EQUIPMENT column (weapon / armor / shield with values) beside SKILLS.
- **Amiga I (user s155):** the Amiga prints "OK" / "EX. PTS" as font text (not graphics), so the manual applies there
  too: requester 14 (the Amiga sheet), the map Health window and requester 6's rows print "OKAY" and "EXPER. POINTS"
  (the sheet widens when a long points value would reach its border).

## D-065 — Renaming a party member

- **Manual (p.4):** "Characters cannot be renamed while they are members of a party."
- **DOS (OV1 `0x22f6`):** renames party members too.
- **Decision (user s53):** manual.
- **Remaster:** `GameSession.TryRename` refuses party members (parked D-069 parties too); the Client R)ENAME lists only
  non-party members.
- **Option `p1.rename-party`:** `refused` (Original (manual) preset, the default) · `allowed` (DOS:
  party members are renamed too, and R)ENAME lists them; Original (DOS) and Remaster presets). One rule,
  `GameSession.CanRename`, for both Core and the Client list. INPUT NAME's rules (9 characters, unique) apply either way.
- **Spec:** `town-menus.md` section Guild.

## D-066 — Town menu: letter keys vs highlight bar

- **Manual (p.3):** places picked by key (A Armory, G Guild, M Mystic, N Inn, B Bank, L leave) plus other options (C cast, U use
  item, D distribute, I inspect…). The C-64 addendum says most keys became highlight menus.
- **DOS (`PHANTASI.COM 0x5e6e`):** one highlight bar "Option Bank Mystic Exit Armory Inn Guild"; Option opens Cast spell / Use
  item / Show party / Save game / Auto save / Sound / Printout / Exit.
- **Decision (user s53):** NOT A CONFLICT — **both**: the DOS bar plus the manual's letter keys. The Guild uses the manual's keys
  too (L list, P purge, N new, R rename, A add; S spells, T train, D drop; E exit); Train and Learn moved from the town bar to the Guild.
- **Spec:** `town-menus.md` (top).

## D-067 — Guild Add ages a member who already lives in this town

- **Manual (p.4):** A)DD MEMBERS — nothing about time passing.
- **DOS (OV1 `0x2835`, quirk Q8):** "[original text] to get here" is asked only for a member from another town, but every Add
  ages the member 2 months (`@264 += 4`), same town too.
- **Decision (user s53):** no aging for a member already in this town; a member from another town still gets the prompt and the 2 months.
- **Remaster:** `GuildHall.TryAddToParty`.
- **Spec:** `town-menus.md` section Aging; `quirks-and-bugs.md` Q8.
- **Option `p1.add-aging`:** `manual` · `dos`.

## D-068 — Distribute: "type 7 to sell" vs a SELL / SACK menu

- **Manual (p.12):** items listed best to worst with who can use each; "type the number of the character who will get that item
  or type 7 to sell it".
- **DOS (OV1 `0x2ab3`):** same pool, order and user lists; the pick is a highlight menu of SELL, SACK and the members.
- **Decision (s53):** NOT A CONFLICT — highlight menus per the C-64 addendum; SACK kept (manual silent). User: "distribute
  everything just like the actual game".
- **Remaster:** the DOS screen with both the menu and the manual's 1–6 / 7 keys (`ItemDistribution`, `DistributeDialog`).
- **Spec:** `town-menus.md` section Distribute.

## D-069 — Continue saved game: pick a character vs load every flagged record

- **Manual (ENCORE: Continuing Saved Games):** [original text] asks "which party member you want" — "Give the Guild number
  of the character you want to call up" — because several parties can be exploring in different towns; you go straight to that
  character's town with the party he or she is a member of.
- **DOS (static, s56; OV1 `0x1e15(2)` → `0x425c` → `0x7dd(2)`):** the load loop `0x866..0x89d` takes every roster record with
  `@218 ≠ 0`, in roster order, into the party (never reads `@260`/`@92`); the town is `@254` of the first one. With nobody
  flagged it opens Pelnor with an empty party (`0x7e7`). **[D✓] s56 DOSBox** (`capture_s56` section 9): "continue saved game" asks
  nothing and lands on the town bar; moving the `@218` flag from wizardo to thiefo loaded thiefo at its roster position.
  `@218` is a single flag, so a DOS save holds one party only.
- **Decision (user s57):** manual — several parties can be out at once, and Continue asks for a Guild number.
- **Remaster:** Load (F9) = [original text]: it asks "GUILD NUMBER OF THE CHARACTER?" (1..22, as L)IST MEMBERS
  numbers them; `ContinueDialog`) and `GameSession.Continue` calls up that character's party where it was saved. The
  JSON save keeps the active party plus the shared world (roster, towns, region bytes, dungeon slot) on its top level and
  every other party as a parked entry (`SessionSnapshot.OtherParties`, party-local state only: members, purse, sack,
  elemental, mode, town / wilderness / dungeon position, pending prompts, fight; `SeveralParties`). Manual-silent choices:
  (1) remaster saves anywhere, so a parked party comes back exactly where it was (in town when saved in town, as the
  manual assumes); (2) a character in no party → a new, empty party at that character's town menu (`@254`), the old
  active party parked — the way to start a second party; (3) a party with no members is never parked; (4) a character is
  in one party at a time: Guild A)DD and P)URGE refuse a parked party's member ("In another party.") and Add does not list
  them; R)ENAME refuses them as party members (D-065); (5) Guild numbers 23..25 (elementals, Town #0) and empty slots are refused and asked again; Esc cancels.
  DOS-save import (F7) still follows the DOS loop and makes one party with none parked (`DosSaveConverter.Import`).
- **Side effects:** DOS export writes the parked parties' members with `@218` = 0 (copies only), because DOS would load
  every flagged record into one party; a note names them and says their purse and sack are not in the DOS files.
  Without parked parties the export is unchanged. Older single-party saves load as one active party (no migration).
- **QoL option:** Options → game tab → CONTINUE FROM A LIST
  (`GameSettings.ContinueFromList`, a presentation switch in `settings.json`, not a locked rule: no save is loaded when
  Continue asks). On: Continue lists the save's parties (`SeveralParties.Parties`: active first, then parked; leader + the
  others' count + town / WILDERNESS / DUNGEON n, the selected party's names below) to pick with Up/Down + Return; the
  last row, BY GUILD NUMBER, asks the manual's question (the only way to call up a character in no party). A save with no
  party asks the question directly. **Off (default) = the manual's question**, unchanged.
- **Spec:** `save-file-format.md` section Remaster codecs.
- **Option `p1.continue`:** `manual` · `dos`: Continue reads the file's locked rules and, under `dos`, asks nothing and loads the saved party (`GameSession.ContinueSavedParty`). The remaster still restores where it was saved, and a file with nobody in the party opens with an empty party. A save made under `dos` never parks a party, since the Guild number is the only way to start a second one.

## D-070 — Inn MAGIC INCREASES: any cast vs combat casts only

- **Manual (INN):** "Characters who have cast spells during their adventures may find their maximum limit for magic has
  been raised after resting at an inn."
- **DOS (static, s58):** the cast flag `@94` that the Inn roll needs is set to 1 only on the combat MP-cost path (OV2 `0x0a55`).
  Town casting (OV1 `0x254e`) and wilderness/dungeon casting (OV2 `0x4790`) never set it; the Inn clears it (OV1 `0x2a46`,
  OV2 `0x6793`). So HEALING or VISION cast outside combat never counts toward the roll.
- **Decision (user, 2026-10-25):** manual — any cast counts.
- **Remaster:** `Character.CastSinceRest` is set on every cast that spends MP: combat (`ResolveCombatCast`) and out of combat
  (`SpellCaster.CastOutOfCombat` — town, wilderness, dungeon). Refused casts (wrong place, no MP) don't set it.
- **Side effects:** out-of-combat HEALING / VISION / MONSTER EVAL. / TRANSPORTATION etc. now feed the Inn roll, so MP max can
  grow faster than in DOS. No other rule reads `@94`; a DOS save export writes the flag as the remaster has it.
- **Spec:** `magic-spells.md` (cast flag), `town-menus.md` section Inn.
- **Option `p1.cast-flag`:** `manual` · `dos`.

## D-071 — Map of Gelnor: 16 sections vs 18 region files

- **Manual (Wilderness Travel):** "MAP of Gelnor is divided into 16 sections. If your party moves off the edge of the map, the
  next section will be displayed."
- **DOS:** ships 18 region files — OUT1..OUT16 (Gelnor) plus OUT18/OUT19, which are reached only through OLYMPIA / TRANSPORTATION
  (Olympus, not part of Gelnor's edge-linked map) (static s58).
- **Decision:** none needed — the 16 edge-linked Gelnor sections agree with the manual; the extra two are Olympus.
- **Remaster:** loads all 18, as DOS.
- **Spec:** `overworld-movement.md`.

## D-072 — YES/NO prompts: "answer Y or N" vs highlight only

- **Manual:** "Answer Y or N" at A TOWN / [original text], at an exit and at SAVE THE DUNGEON MAP (main rules); the C-64
  addendum says to highlight YES or NO.
- **DOS (static s58: menu loop COM `0x05e3` takes only arrows, Return, Esc; DOSBox s58 `capture_s58` section B):** Y / N do nothing;
  each prompt keeps its own highlight (per menu id, DS `0x24+16·id`).
- **Decision:** NOT A CONFLICT — **both**, following the user's D-066 rule for the town menu: the DOS highlight (with DOS's
  per-prompt cursor) plus the manual's Y/N keys.
- **Remaster:** `PromptMenu` — highlight + Return, and Y / N answer directly.
- **Spec:** `town-menus.md` (top), `overworld-movement.md` (ENTER?).

## D-073 — Melee speed: T)IME LAG 0–9 + sound vs Options → Speed fast/normal/slow

- **Manual (wilderness commands):** "T)IME LAG allows you to speed up or slow down melee action. Choose a number between 0
  and 9 (0 is the fastest, 9 the slowest). You will also be asked if you want sound: answer Y or N." Command summary:
  "Alter melee speed and toggles sound".
- **DOS (static s62, PHANTASI.COM strings `0x612e..0x6152`):** an **Options** menu with **Speed / Exit**; Speed opens menu
  `0x13` **fast / normal / slow / exit**, first highlight *normal* (`message-and-text.md`). No 0–9 scale; sound is a **±Sound** toggle in the
  field Options menu `0x14` (s67) and a town Option, not a question. What each step delays is undecoded `[?]`. Neither version skips showing the fight.
- **Decision:** **manual** (user s62). Lag 0 should be as close to instantaneous as possible, scaling up from there; the
  sound question is included.
- **Remaster:** wilderness **T** → "TIME LAG (0-9)?" then "SOUND? (Y/N)" (`TimeLagDialog`); Esc changes nothing. The pause
  before each combat round is `15 · lag²` ms (0 → none: the round shows as soon as orders are in; 5 ≈ 375 ms; 9 ≈ 1.2 s) —
  remaster curve, the manual gives no times. Default 5 (manual silent; DOS starts on *normal*). Orders are still asked every
  round. Sound is the same flag as the town Option's Sound. DOS's fast/normal/slow menu is not built. No F key
  speed (Normal/Fast/Instant), no Instant mode (fights resolved unseen), no wilderness T shortcut to town 1. Holding **Shift** still fast-forwards a fight (Phase 7 QoL, remaster-only).
- **QoL switch (P3):** the game tab's **TIME LAG AND SOUND** row: `T COMMAND` (off, the default = the manual's T above) or
  `IN OPTIONS` — then TIME LAG (0–9) and SOUND are rows of the Options tab (also from the Esc menu → Options, in game), and
  T is no command (left out of the wilderness / dungeon command list). A presentation switch in `settings.json`
  (`games.p1.timeLagInOptions`), not a per-save rule: melee speed and sound don't change the game, so no preset sets it.
  The lag and sound values are the same either way.
- **Side effects:** the defeat box shows the fight's report whenever a fight wipes the party (any lag, or Shift), so a
  wipe is never unseen.
- **Spec:** `message-and-text.md` (menu `0x13`).

## D-074 — Guild S)PELLS list: spell numbers vs spell names

- **Manual (Guild, party options):** "S)PELLS tells you which new spells can be learned by the character and how much they
  will cost. The spell will be identified by its number, not its name (see MAGIC section). If there are no numbers after
  POSSIBLE SPELLS:, then no new spells can be learned at this time."
- **DOS (s63, OV1 `0x1490..0x1677`, DOSBox):** a box "You can learn %d more spells." / "The cost is %d GP." /
  "Available spells:" and the cursor spell picker with **names only** (no numbers), Return picks, Esc backs out. With
  nothing to learn it shows a separate box "[original text]" instead of an empty list.
- **Decision:** user (s64): **manual** — "obey the manual".
- **Remaster:** the manual's form with the DOS lines it doesn't contradict: "You can learn N more spells." /
  "The cost is F GP." / "POSSIBLE SPELLS:" then numbered rows "1 #id Name" picked with a digit; nothing to learn = the
  heading with no rows. The rules behind it are DOS (manual silent): learn cap, ≈ INT/21 fail roll with the fee and two weeks
  still taken, one pick per opening, a dead member can't learn, "[original text]", "You've learned" / "You've failed".
- **Side effects:** none beyond the list's look; the fee, cap and roll are the same either way.
- **Spec:** `town-menus.md` section Learn spells.

## D-075 — New member: sheet after naming

- **Manual (Guild, N)EW MEMBER):** "Next, the full list of statistics for the new character will be shown … When you have
  finished studying the display, hit the SPACE BAR to be taken back to the Guild Menu."
- **DOS (s65 DOSBox, `capture_s65_guild.md` #10):** the sheet is on screen only while INPUT NAME runs; Return on the name goes
  straight back to the Guild menu, with no extra sheet and no SPACE pause.
- **Decision:** manual (user, s66).
- **Remaster:** the manual's form — after the name is accepted the full sheet shows with "PRESS SPACE"
  (`GuildNewDialog`, Step.Sheet).
- **Side effects:** presentation only; the roll, the name rules and the enrolment are the same either way.
- **Spec:** `character-creation.md`.

## D-076 — Wilderness command and movement keys

- **Manual (WILDERNESS TRAVEL):** C cast · H health · I inspect · U use potion · T time lag; move with **3 or N** north,
  **E** east, **S** south, **W** west.
- **DOS (s65 DOSBox, `capture_s65_overworld.md`):** none of those letters (either case) or `3` do anything. Only the arrow keys
  move; the commands are only in the Return bar (Options → Inspect / Health / Cast spell / Use potion / Sound, Speed, Exit).
- **Decision:** manual (user, s66); arrow keys instead of WASD, and the numpad, diagonals included.
- **Remaster:** the manual's keys in the wilderness **and the dungeon** (DUNGEON EXPLORATION: "the command line has a new
  option", i.e. the same command line plus L)OOK): **3 or N** north, **E** east, **S** south, **W** west; C cast, H health,
  **I** inspect, **U** use potion, **T** time lag (now also in a dungeon), **L** look (dungeon only, s67: `DungeonSight.Look`). Arrows, D-pad and numpad 8 / 2 / 4 / 6 move as
  well; WASD is gone (A does nothing), Q/E turning starts unbound. Bindings stay remappable. I and U act only on the
  wilderness / dungeon map — in town they keep the town bar's I)NFO / U)SE ITEM.
- **QoL "Diagonal moves" (Options → I tab; default ON as the user asked; OFF = the manual's four directions):** numpad 7 / 9 /
  1 / 3 (Home / PgUp / End / PgDn with Num Lock off). A diagonal is **two ordinary steps** — north/south first, then
  east/west; if the first is blocked the other order is tried; the second step runs only after a plain move (not after
  an encounter, a prompt, a sign, a swim…). So a diagonal costs two steps of time and gets two encounter / swim / trap
  rolls; no new movement rule.
- **Side effects:** an old `settings.json` (no `bindingsVersion`) gets the move, turn, Inspect and Use potion bindings put
  back to the new defaults once; other remaps are kept. The numpad 3 (SE) is a different key from the manual's 3 (north).
- **Spec:** `overworld-movement.md`.

## D-077 — Wilderness screen layout

- **Manual (WILDERNESS TRAVEL):** the map "along with pictures of the party members and a list of command options".
- **DOS (s65 DOSBox):** the map alone; commands only through the Return bar.
- **Decision:** manual (user, s66); the remaster's status / help lines become a QoL option.
- **Remaster:** the wilderness (and the dungeon, whose map replaces it) shows the map at the left with the party pictures
  (portrait cards) and the command list (C)AST H)EALTH I)NSPECT U)SE POTION T)IME LAG, 3/N E S W MOVE) beside it, and
  the message line under it. The map there is a **13×16-cell window that scrolls with the party**, not the whole 26×20
  section: the whole section at the Amiga cell size does not fit beside the pictures. **F2** switches to the Amiga
  map-only screen (whole section; setting `MapOnly`, default off).
- **QoL "Status lines" (Options → I tab; default OFF = manual):** the top status line (mode, gold, party, region, position,
  DAY OUT / dungeon level, facing) and the key help line. A dialog's own help line shows either way.
- **Side effects:** presentation only.
- **Spec:** `overworld-movement.md`.

## D-078 — Combat orders between trips

- **Manual (C-64 supplement, ENCOUNTERS):** "the individual combat option chosen in one turn of combat remains unchanged until
  you change it."
- **DOS (s65 DOSBox + COM `0x7c`, `capture_s65_combat.md`):** the repeat orders (`0x5b96`) persist across rounds, encounters,
  a failed FLEE and a surprise opening, but every member is reset to PARRY when the party leaves town (OV1 `0xd0`) and after
  a party defeat (`0x84`).
- **Decision:** manual (user, s66).
- **Remaster:** orders persist across rounds and encounters (both agree); no reset on leaving town or after a
  defeat (the manual). A party slot whose member changed starts on PARRY (remaster).
- **Side effects:** none beyond which order the first fight of a trip starts with.
- **Spec:** `combat-resolution.md` section Orders.
- **Option `p1.orders-reset`:** `manual` · `dos`: every member's repeat order goes back to PARRY when the party leaves town (OV1 `0xd0`) and after a party defeat (`0x84`). Core counts the resets (`GameSession.OrdersResets`, not saved); the Client's combat presenter, which keeps the orders, clears them when the count changes.

## D-079 — Amiga combat field: a monster's cast line

- **Manual:** silent (it names no combat line for a monster's spell).
- **DOS:** "%s CASTS A SPELL" with the monster's name, then the spell's own lines; the line waits like any other.
- **Amiga (h38 `+0x480`, FORMAT.md "The combat screen"):** the monster's silhouette and attack frame, then a one-space
  line `" "` with **sound 9**, no wait (strlen ≤ 3 skips the polls), then the spell's own lines. A party member's cast
  prints "%s CASTS A SPELL" with sound 1 and the Speed's wait on both. II ST does the same as I's Amiga (blank + 9).
- **Decision:** keep DOS's text and add the Amiga's sound 9 (user s138; the same as II's s137 decision, recorded in the
  ST's `FORMAT.md` 7.3).
- **Remaster:** on I's Amiga field (AMIGA SCREENS) a monster's "%s CASTS A SPELL" plays sound 9 after the silhouette and
  attack frame, prints the text and waits no polls (pack `line_sounds` `monster_no_wait`); the next line follows at once.
  The member's cast line is unchanged (sound 1, the Speed's wait). Screens off: unchanged.
- **Side effects:** on the field the monster's cast text is replaced as soon as the spell's first line prints (at once when
  that line has no sound), so it is seldom seen; a round with monster casts runs shorter by one wait per cast.
- **Spec:** `reverse-engineering/amiga/FORMAT.md` "The combat screen" (the line's sound table).

## D-080 — Amiga map boxes: four texts

- **Manual:** silent.
- **DOS:** "[original text]", "[original text]", "[original text]", "[original text]".
- **Amiga (h75 `+0xc0` / `+0xd3` / `+0x47`, h42 `+0x5d`):** no blank before "!", and "THAT IS A WALL".
- **Decision:** the Amiga's text in the Amiga's boxes (user s157). D-079 (the monster's cast line) is unaffected.
- **Remaster:** pack `map_message.texts` (Core's id → `p1field.box.*`), AMIGA SCREENS only. Core's log, the message
  line and AMIGA SCREENS off keep DOS's text.
- **Side effects:** none (the box's text only; sounds and timing unchanged).
- **Spec:** `reverse-engineering/amiga/FORMAT.md` "The map screen's requester 23" and "The dungeon's requester 23".

## D-081 — Amiga POTIONS view: the ring letters

- **Manual:** silent (it shows no sheet page).
- **DOS:** clean rows.
- **Amiga (h23 `+0x3b0`, FORMAT.md "requester 11"):** the ring letters are copied without an end marker over the wand
  text, so from the first POTIONS view on the rings row reads "RINGS: " + the letters + "ND OF NIKADEMUS" and the wand
  row loses its first six characters (static, not filmed `[~]`).
- **Decision:** an Amiga bug; fix it, keep the rows clean (user s157).
- **Remaster:** requester 11 prints "RINGS: " + the letters and "[original text]" in full.
- **Side effects:** none.
- **Spec:** `reverse-engineering/amiga/FORMAT.md` (the sheet's requesters).

## D-082 — Amiga-only import: potion and scroll item names

- **Manual:** the healing potions are "Healing 1" … "Healing 10" (TREASURES, POTIONS); the Armory list shows scrolls as
  "SCROLL 8" (TOWNS, the Armory example).
- **DOS:** `HEALING 1` … `HEALING10`, `SCROLL 1` … `SCROLL 20` (agrees with the manual).
- **Amiga (game h12 item names, `reverse-engineering/amiga/GAME-TABLES.md` section 6):** items 101-109 `HEAL POT1` …
  `HEAL POT9` (110 `HEALING10`), item 139 `SROLL 19` (a typo).
- **Decision (user s202):** keep the Amiga's own wording `HEAL POT1` … `HEAL POT9`, fix only the typo
  `SROLL 19` → `SCROLL 19`. Only an import made from the Amiga disk alone shows these (with DOS I imported, DOS's
  names are used).
- **Remaster:** an Amiga-only import uses the Amiga's names (the Amiga's text is used where the import takes the
  Amiga's tables, as the Apple II's for II), except item 139, fixed by the shipped `name_fixes` of
  `phantasie1/amiga-import.json` (`P1AmigaTables`).
- **Side effects:** none on rules (items are found by number).
- **Spec:** `reverse-engineering/amiga/GAME-TABLES.md`.

## D-083 — Amiga-only import: the fire elemental's name

- **Manual:** the three elementals are FIRE, WATER and EARTH (character creation, the roster's last three).
- **DOS:** `FIRE ELEM`, `WATER ELEM`, `EARTH ELEM` (TWNS.INT's records 22-24).
- **Amiga (twns.int 12424):** `FIRE  ELE` (two blanks, the M cut off); WATER / EARTH as DOS.
- **Decision (user s202):** fix the typo to DOS's `FIRE ELEM`. Only an Amiga-only import shows it.
- **Remaster:** an Amiga-only import names it `FIRE ELEM` (elemental 0 and its twns.int label, `name_fixes` in
  `phantasie1/amiga-import.json`).
- **Side effects:** none (the summon finds the record by number).
- **Spec:** `reverse-engineering/amiga/GAME-TABLES.md` section 2 / 6.
