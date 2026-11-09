# Phase 9 T5.0 — Startup screen and options

**Decided (user, s73, 2026-10-26):** the remaster opens on a main menu where the player picks the game, plus an Options
screen with a General tab and one tab per game. Drawn in the remaster's own style with the 320×200 pixel font (no modern
high-resolution layer). Rules options are **locked per save**. Each game tab has four presets: **Original (manual)**,
**Original (DOS)**, **Remaster**, **Custom**. Built before the inventory rework so every later QoL item plugs into it.

This is remaster UI, not a manual↔code question: it adds no `DIFFERENCES.md` entry. The options it switches each keep their
own D-entry.

---

## 1. Main menu

```
            PHANTASIE
     >PHANTASIE I
      PHANTASIE II
      PHANTASIE III
      PHANTASIE IV
      OPTIONS
      QUIT
```

- A shell state of the Client, **not** a `GameMode`: no session exists until a game is picked. `GameMode.Title` stays I's
  own Title/Guild screen.
- A game can be picked when it can be started (`Campaigns.IsPlayable`; all four are) and its data folder is present
  (`MainMenuScreen.IsPlayable`). A game that can't be picked is shown greyed with "not built yet". The game names come
  from the string table (`menu.game.p1` …).
- Picking a game builds that game's session with the game tab's **new-game rules** (§4) and opens the game's own start
  (I–III: `GameSession.StartNewGame`, the first town with an empty party; IV: `Phantasie4World.NewGame`, Pendragon with
  an empty party).
- `--test-party N` skips the menu and starts I straight away (scripted runs and tests).
- Room is kept for a later **Characters / Hall of Glory** entry (not built). The transfers run inside the receiving game:
  I–III from the save / transfer dialog (`DosSaveDialog`), IV from its Hall.
- The Esc game menu gains **Options** (§2, opened on the current game's tab) and **Main menu** (Y/N like Quit; unsaved
  progress is lost). The Diagonal moves and Status lines switches move from the Esc menu into the game tab.

## 2. Options screen

```
 OPTIONS
 GENERAL  [I]  II  III  IV          ← Left/Right on this row switches tab
 GRAPHICS            AMIGA
 TITLE SCREEN        ON
 TITLE TUNE          ON
 AMIGA AUDIO FILTER  ON                ← I and III only
 DIAGONAL MOVES      ON
 STATUS LINES        OFF
 CONTINUE FROM A LIST OFF
 NEW INVENTORY SCREEN OFF
 TIME LAG AND SOUND  T COMMAND
 COLORBLIND PALETTE  OFF
 AUTO-MAP BOX        ON                 ← I, II and III
 TERRAIN KEY         OFF                ← II only
 MYSTIC SKIP         OFF                ← IV only
 RULES FOR NEW GAMES
 PRESET              ORIGINAL (MANUAL)
 SUMMONED ELEMENTAL  FULL STATS
 ARMORY RESTOCK      WEAPONS, POTIONS
 GOLD CAP            65,000
 RENAME PARTY MEMBERS NO
 …
 QUALITY OF LIFE                       ← the rules options with Qol (11-guild-treasury.md section 6)
 ITEM STASH          IN TOWN
 …
 GUILD TREASURY      OFF
 GUILD ROSTER LIMIT  THE GAME'S
 FROM OTHER GAMES
  TOSS ROCK (II)     OFF
  BODY-PART INJURY (III) NOT BUILT
```

- Keys: Up/Down pick a row, PgUp/PgDn a page (8 rows; both wrap at the ends like Up/Down), Home/End the tab row / last
  row, Left/Right or Return change its value (Left/Right on the tab row change the tab), Esc goes back (to the main menu,
  or to the game menu it was opened from) and saves `settings.json`.
- **Description box** (s100, user): under the list, a box describes the row under the cursor: its label, what it does and
  what each value means (`option.<id>.desc`, `module.<id>.desc`, `options.<row>.desc`, `options.tab.desc` in the shared
  string table), then for a rules option the value each preset gives and its D-entry. A longer text scrolls with [ and ],
  the wheel over the box or its bar, and starts at its top for each row. Every row must have a `.desc` string (pinned by
  a Client test).
- **General tab:** window scale, start maximized, then (main menu only) IMPORT GAMES (s158), **CHECK UPDATES AT START**
  (s174, on by default; release builds ask GitHub's latest release at start and open the update screen when a newer one
  has a download for this machine; a failed check says nothing) and (main menu only) **UPDATE NOW** (value: the running
  version; opens the update screen and checks at once; `UpdateScreen`, `Updates/`; packaging and CI in `10-importer.md`
  section 8.4). (Key bindings stay in the settings file until a rebinding screen exists.
  Sound and T)IME LAG are I's own options, D-073: the manual's wilderness **T** by default, or rows of I's tab, below.)
- **Game tab**, top to bottom: presentation (graphics, title screen and title tune (section 8), on I and III **Amiga
  audio filter** (section 8), diagonal moves, status lines, continue from a list, new inventory screen, **time lag and
  sound**: `T COMMAND` / `IN OPTIONS`, the D-073 QoL switch; with `IN OPTIONS` a TIME LAG 0–9 row and a SOUND row follow and T is no command), **colorblind palette** (P3
  QoL, off = the original colours), on I, II and III **auto-map box** (s172 P3 QoL, below), on II **terrain key** and on IV **Mystic skip** (both s171 P3 QoL, below), then
  **Rules for new games**: the preset row, every rules option of that game, then the feature modules of other games
  ("from other games"), which show "NOT BUILT" until they are decoded.
- **Quality of life** (s175, `options.quality_of_life`, `11-guild-treasury.md` section 6): the rules part splits in two.
  The preset row and the game's original rules (every option with a manual / DOS meaning, D-entry or not) come first;
  the options whose descriptor sets `Qol` (pure convenience, no manual↔code conflict: `treasury`, `p1.stash-access`,
  `guild-limit`, `elemental-load`, `elemental-drop`, `elemental-purge`, `shared-aging` (`13-aging-normalised.md`)) follow under a QUALITY OF LIFE header, before
  FROM OTHER GAMES. The locked in-game view (THIS SAVE'S RULES) is split the same way. The preset still sets them all.
- **Terrain key** (II only, `terrainKey`, `options.terrain_key`; off = the ST, no key, D2-014): the rulebook's terrain
  key beside the wilderness map, rows from `GameData/phantasie2/terrain-key.json` (string id of the manual's name + the
  pack pictures as swatches: the ST fill patterns `overworld.pattern.N`, the map icons, the party sprite). The party keeps
  its first row of cards and the key takes the room under it; both scroll (wheel, bar) what does not fit. Shown only when
  the pack holds the pictures (the ST pack, not the Apple II's). s172 (user): keys too (`SidebarScroll`): `[` / `]` scroll
  the party cards (also I / III's sidebar, e.g. with an elemental), `,` / `.` the key; pad: right stick up / down the cards,
  right trigger + right stick the key. The field help line names them (`game.3_n_e_s_w_arrows*`).
- **Auto-map box** (I, II and III, `autoMap`, `options.auto_map`; **on** by default, user s172; off = the originals, which
  draw no box): the dungeon's whole-level overview (`UiOverlays.TileOverviewPlacement` / `DrawDungeonOverview`), opaque,
  inside the dungeon view left of the member cards, in the view's corner farthest from the party's glyph, never over it
  or a card (III: 3 × 2 px cells, 76 × 64; I / II glyph view: 2 × 1 px, 70 × 42; the whole level, no cell cut). Seen
  cells take their glyph's commonest colour, the party cyan; a click on a cell walks there. Tab in the dungeon flips this
  same setting (saved), so the row shows what Tab last set. IV's dungeon screen (`P4DungeonScreen`) has no box, so no
  row. Replaces the top-level `autoMapVisible` (s171 and before, one switch for all games; no longer read).
- **Mystic skip** (IV only, `mysticSkip`, `options.mystic_skip`; off = P4.X): Space, Return, a left click or the pad's A
  during the Mystic's three counted loops (~23 s, no keys in P4.X) runs their remaining passes at once
  (`P4MysticReading.SkipLoops`): the same final picture, then the original wait for a key. The score and verdict are the
  town loop's either way. That input closes nothing.
- **Save slots** (every game, `saveSlots`, `options.save_slots`; off = the one save, IV: its original's three slots on
  its own Save page): numbered slots beside the autosave, a slot picker for Save / Load and the main menu
  (`12-save-slots.md`). A presentation switch, not a rule:
  Load must know it before any save is read.
- **Combat speed** (presentation, per game): I and III show AMIGA SCREENS and COMBAT SPEED (the Amiga's Speed menu, s117 /
  s132); **II shows COMBAT SPEED only** (s136: the ST's Speed menu, FAST 5 / NORMAL 10 / SLOW 1000 ms a combat line; at
  NORMAL and SLOW each combat sound waits for the one before, `reverse-engineering/atari-st/phantasie2/FORMAT.md` 7.3).
  The ST's own menu bar is not built, so this row stands for it.
- A game that is not built shows "NOT BUILT YET" in its tab and nothing else.
- **Opened in game** (Esc → Options): the General tab and the running game's tab only. The rules part shows **this
  save's** rules, read-only (header "THIS SAVE'S RULES (LOCKED)"). There is no override. Up/Down still stop on the
  locked rows (and on placeholder modules), so a list longer than the screen scrolls and can be read; Left/Right change
  nothing there.

## 3. Scopes

| Scope | Examples | Changed | Stored |
|---|---|---|---|
| **Global** | window scale, start maximized, key bindings, DOS save folder | any time | `settings.json` top level |
| **Per-game presentation** | graphics pack, title screen and title tune (section 8), diagonal moves (D-076), status lines (D-077), continue from a list (D-069 QoL), inventory screen, combat speed (I, II, III), time lag and sound in the options (D-073 QoL; the lag and sound values themselves stay top level), colorblind palette (P3 QoL), terrain key (II) and Mystic skip (IV) (s171 P3 QoL), auto-map box (I, II, III; s172 P3 QoL, on by default) | any time, also in game | `settings.json` → `games.<p1>` |
| **Per-game rules** | DOS-vs-manual choices, QoL rule toggles, cross-game feature modules, transfer and lineage options | game tab = defaults for **new** games | copied into the save at new game; locked |

Presets cover **rules only**. Presentation switches stay outside them, so a fresh install (diagonal moves ON at the user's
request) never reads as "Custom".

## 4. Rules options and presets

- Each rules option is a descriptor in Core (`RuleOptionRegistry`, code like `FeatureModuleRegistry`, because an option only
  means something with C# behind it): id (`p1.elemental-prep`), game, its values, the value each preset gives it, and the
  D-entry it comes from. Labels come from the string table: `option.<id>` and `option.<id>.<value>`, the description
  from `option.<id>.desc` (labels ≤ 22 characters so the value column lines up).
- **Original (manual):** the manual's side of every option. The default for new games.
- **Original (DOS):** the DOS side of each option that has a D-entry; every other option keeps its original value.
- **Remaster:** the recommended set (manual rules plus the QoL options worth having on).
- **Custom:** any other mix. Changing one option keeps the current preset if the values still match it, else picks the
  first preset that matches (manual, DOS, remaster), else Custom. Picking Custom by hand keeps the current values.
- **Feature modules:** every preset gives the game its own modules (`FeatureModuleDescriptor.IsDefaultIn`). A module from
  another game is switched on only through Custom. A placeholder module can't be switched on.
- **Shipped options (s73):** `p1.elemental-prep` (D-032): manual and Remaster = the elemental fights with its record's
  values; DOS = the zero-stat slot 7. `p1.stash-access` (09 T2, QoL, no D-entry, `inventory-actions.md` §4): `town` (manual
  and DOS; the original hands items out in town only) or `anywhere` outside combat (Remaster).
- **Shipped options (P3):** `p1.armory-restock` (D-054): `manual` (manual and Remaster) or `dos` (armor and shields wiped
  too; DOS). `p1.gold-cap` (D-048): `manual` 65,000 (manual), `dos` 65,535 (DOS) or `lifted` (Remaster; no practical cap,
  `int.MaxValue`; DOS export clamps to 65,535 and lists it). `p1.rename-party` (D-065): `refused` (manual) or `allowed`
  (DOS and Remaster).
- **Shipped options (P3.2 Authentic mode):** `p1.stunning-blow` (D-015): `manual` (only a survivor is stunned; manual and
  Remaster) or `dos` (a member the blow killed becomes SLEEP at HP 0; DOS). `p1.armor-reduced` (D-016): `armor` (manual and
  Remaster) or `weapon` (specials 17–20 scale weapon damage `@248`; DOS). `p1.payout-remainder` (Q2 under D-037):
  `first-survivor` (manual and Remaster) or `dropped` (DOS). Q1 needs no option of its own: `p1.gold-cap` = `dos` is DOS's
  16-bit gold.
- **P3.2 manual-or-DOS switches** (`manual` in the Original (manual) and Remaster presets, `dos` in Original (DOS); one
  per DECIDED D-entry whose DOS side is a contained switch; `GameOptions.IsDos`): `p1.attack-gate` D-011, `p1.lunge`
  D-012, `p1.binding` D-013, `p1.confusion-iv` D-014, `p1.fear` D-021, `p1.dispell-undead` D-024, `p1.spell-level` D-028,
  `p1.fireflash-damage` D-029, `p1.fireflash-target` D-059, `p1.failed-run` D-030, `p1.town-inn-time` D-004,
  `p1.hit-dice` D-007, `p1.wild-inn-time` D-008, `p1.inn-magic-cap` D-046, `p1.wild-inn-dead` D-047, `p1.pool-cap` D-051,
  `p1.path-time` D-055, `p1.monster-eval` D-056, `p1.vision` D-057, `p1.add-aging` D-067, `p1.cast-flag` D-070,
  `p1.dungeon-map-no` D-027.
- **P3.2 Authentic mode rest** (s78; same `manual` / `dos` shape and presets): `p1.halfling-dex` D-001, `p1.guild-size`
  D-002, `p1.lifespans` D-003, `p1.monk-sleep` D-006. The DOS table cells and lifespan factors live in
  `creation-tables.json` `dos_tables` (`CharacterCreationTables.ForRules`, `GameSession.CreationTables`); the Guild size
  comes from `rules.json` `guild_by_platform.dos`. Then `p1.town-healing` D-010, `p1.beg-mercy` D-017, `p1.sack-size`
  D-018, `p1.sleep` D-020, `p1.charm` D-022, `p1.teleport` D-023, `p1.wild-treasure` D-031, `p1.summon-party` D-033,
  `p1.monster-spell-target` D-033 (split from `p1.summon-party`, user s172), `p1.combat-shares` D-053, `p1.continue` D-069 (Continue reads the file's own rules before it asks), and the lists
  `p1.cast-list` D-035, `p1.transport-list` D-062, `p1.combat-spell-list` D-063, plus `p1.orders-reset` D-078 (the
  Client's combat presenter clears its kept orders when `GameSession.OrdersResets` changes).
- **Phantasie II (T3, I → II transfer, `TransferIToII`):** `p2.transfer-spells` (D2-002): `manual` (Original (manual)) or
  `st` (Original (Atari ST)); Remaster plays `manual` (user s76: the ST unless the manual contradicts it). `p2.transfer-divine` (D2-004): `p27` in every preset, or `p14`.
  `p2.transfer-gold` (D2-003): `st` in every preset, or `no-gain` (a player choice, in no preset; user s77).
- **Phantasie II training and saving (s79, user decisions):** `p2.training-cost` (D2-013, I's D-050): `manual` (the cost
  shown with "You need N") or `st` (hidden). `p2.hit-dice` (D2-013, I's D-007): `manual` (the chart's `/15`) or `st`
  (`>> 4`). `p2.wizard-resurrection` (D2-015): `manual` (wizards learn #44 at level 13) or `st` (never). The first three
  are `manual` in Original (manual) and Remaster and `st` in Original (Atari ST). `p2.autosave` (D2-016): `toggle` (the
  ST's Town options item; every preset) or `always` (the Apple text; a player choice in no preset).
- **Phantasie II, I's decisions again (s81, user s80: manual, as I):** `p2.gold-cap` (D2-019; `manual` 65,000 or `st`
  65,535), `p2.guild-size` (D2-020; `manual` 21 or `st` 22 from `guild_by_platform` under `rules.json`
  `original_platform`), `p2.lifespans` (D2-021; `manual` bands or `st` factors), `p2.dispell-undead` (D2-022; `manual` =
  II's `monster-traits.json`, I's list plus PLUTO'S UNDEAD SNAKE, or `st` = XP divisible by 7) and `p2.fear` (D2-023;
  `manual` run or `st` level drop). Each is `manual` in Original (manual) and Remaster and `st` in Original (Atari ST).
  Core reads them through the accessors I's options already feed (`GameOptions.GoldCap`, `OriginalGuildSize`,
  `OriginalLifespans`, `OriginalDispellUndead`, `OriginalFear`), so a II session plays them with no II-only branch.
- **Phantasie II dungeon map NO (s82, user: as I's D-027):** `p2.dungeon-map-no` (D2-041): `manual` a NO forgets the
  same dungeon's slot, `st` keeps it (the older save comes back); read in `GameSession.AnswerDungeonExit` beside `p1.dungeon-map-no`.
- **Phantasie II towns, inns, travel and combat (user s81 / s84):** `manual` in Original (manual) and Remaster, `st` in
  Original (Atari ST): `p2.transport-list` D2-030, `p2.inn-dead` D2-031, `p2.town-inn-time` and `p2.inn-magic-cap`
  D2-032, `p2.wild-inn-time` D2-042, `p2.path-time` D2-040, `p2.combat-shares` D2-043. Plus `p2.fear-bosses` (D2-023
  follow-up): `stand` (Pluto and his beasts stand fast under the manual FEAR) in every preset, or `flee`.
- **Phantasie III (T4 Core rules, user s81 decisions; `P3RuleOptions`, accessors `P3RuleOptionsExtensions`):** `manual` in
  Original (manual) and Remaster, `dos` in Original (DOS): `p3.slash-fallback` D3-010, `p3.lunge` D3-011,
  `p3.thief-attack` D3-012, `p3.heal-order` D3-031, `p3.aim` D3-013, `p3.down-targets` D3-019, `p3.armour-defence` D3-018, `p3.arrow-flame`
  D3-017, `p3.bow-spells` D3-039, `p3.parry` D3-036, `p3.status-letter` D3-021, `p3.combat-spell-list` D-063,
  `p3.healing-iv` D3-014, `p3.inn-healing` D3-015, `p3.hit-points` D3-022, `p3.level-gold` D3-023, `p3.skill-tables`
  D3-032, `p3.encounter-block` D3-024, `p3.mercy` D3-024, `p3.gold-cap` D3-026 (`dos` = lifted), `p3.armory-restock`
  D3-033, `p3.autosave` D3-028, `p3.roster` D3-029 (`dos` 17; `guild-limit` = `none` lifts it), `p3.lifespans` D3-041,
  `p3.dungeon-map-no` D3-038; the transfer (s147) `p3.transfer-source` D3-004, `p3.transfer-level` D3-007,
  `p3.transfer-divine` D3-002, `p3.transfer-wand` D3-001. Three-value options (the middle value in no preset):
  `p3.found-items` D3-020 (`manual` 90, `unlimited`, `dos` 60), `p3.bank` D3-025 (`manual`, `no-fee`, `dos`),
  `p3.trade` D3-027 (`manual`, `give-only`, `dos`); D3-040's former `p3.purge` is folded into `elemental-purge` (s172). Same value in
  every preset: `p3.transfer-roster` D3-003 (`add`, or `erase`) and `p3.boss-flee` D3-024 (`blocked`, or `allowed`).
  DOS bugs fixed by default, no D-entry (manual silent): `p3.altar` and `p3.event-gold` (`fixed`, `dos` in Original
  (DOS)). QoL (QUALITY OF LIFE section): `p3.transport-planes` D3-035 (`off` in every preset, or `visited`: Transportation
  also to the plane towns the party has entered). Menu order: `P3RuleOptions.All`.
- **Phantasie IV (T6.3 Core rules and T5 transfer; `P4RuleOptions`, accessors `P4RuleOptionsExtensions`):** 48 options
  (list and menu order: `P4RuleOptions.All`). Most are `manual` in Original (manual) and Remaster and `x68000` in the
  Original preset (labelled "ORIGINAL (X68000)", `options.preset.dos.p4`), one per D4-entry. Exceptions:
  `p4.gold-cap` D4-031 is `manual` / `uncapped` (the X68000 preset takes `uncapped`); `p4.aging` D4-034,
  `p4.monster-hp` D4-055 and `p4.level-wrap` D4-060 choose a code platform, `msx2` (Original (manual), Remaster) /
  `x68000`. Same value in every preset, the others a player choice: `p4.ship-life` D4-038 (`4000`, `2000`, `8000`),
  `p4.teleport` D4-052 / D4-070 (`previous`, `random`, `entrance`, `stay`), `p4.overview` D4-072 (`x68000`, `centred`),
  `p4.dungeon-reload` (`x68000`, `saved-map`; no D-entry), `p4.annihilate` D4-071 (`x68000`, `divine`), and the T5
  transfer options (s149) `p4.transfer-join` D4-017 (`hall`, or `guild`: a III character joins IV's Guild) and
  `p4.transfer-games` (`iii`, or `all`: I and II characters into the Hall too).
- **Feature module `p2.rock-throwing` (Toss Rock, built s79):** on in II's presets. In I, III and IV it is listed under
  "from other games" and can be switched on through Custom.
- **Feature module `p4.lineage` (descendants at creation, built s149, `ancestry.md` section 13):** in I, II and III it
  is listed under "from other games", off in every preset. IV always has it (`BuiltIntoHome`), so it is no switch there.
- Placeholder modules (shown "NOT BUILT"): III's `p3.body-part-injury`, `p3.bows`, `p3.party-ranks` and `p3.aim` for the
  other games. III's own combat is the built module `p3.combat-actions`.
- **Every-game option (P3 QoL, user s79 from D3-003):** `guild-limit` is one descriptor with `EveryGame` set
  (`RuleOptionDescriptor.AppliesTo`): no game prefix, shown last in every game's tab and locked per save like the rest.
  `game` (both Original presets: the game's own cap from its `rules.json`, I's 21 or 22 under `p1.guild-size` = `dos`)
  or `none` (Remaster: no roster limit; it overrides `p1.guild-size`). Under `none` Guild N)EW fills holes first, then
  grows the roster one slot past the game's slots; saves keep the longer roster; Continue takes the longer Guild numbers.
  DOS export writes the active party and then the others by Guild number into GUILD.DAT's 22 records (a moved character
  is a note, everyone left out is named in a clamp warning; the remaster save keeps them all); DOS import gives no D-002
  size note. The I → II transfer converts every slot of a grown roster when II's save has `guild-limit` = `none`, else
  the ST's first 22 (`GameSession.TransferFromPhantasieI`).
- **Elementals across the games (P3 QoL, user D3-040):** three every-game options limited to I, II and III
  (`RuleOptionDescriptor.Games`; IV picks its elemental when the party is formed and has no Guild Add / Drop / Purge of
  one, so they are not in its tab), listed before `guild-limit`. Every preset gives `game` = the game's own rule, so no
  preset changed; the other values are a player choice through Custom, each an original's decoded rule:
  `elemental-load` (`game`: I six characters first and nobody after it, II (ST, s172) no count and characters beside
  it, III one character and more in front of it; `phantasie-i`, `phantasie-iii`), `elemental-drop` (`game`: I every Drop
  unloads it, II it stays even alone, III it stays while a character is left; `phantasie-i`, `phantasie-iii`) and
  `elemental-purge` (D3-040; `game`: I purges and unloads, II purges and keeps it, III follows the preset (s172: Manual /
  Remaster / Custom `as-drop`, Original `refused`; `p3.purge` folded in); `phantasie-i` unloads, `as-drop` does what this
  save's Drop does (III's manual), `refused` = DOS III's "Drop first"). Each game's own rule is data: rules.json
  `elementals` (`min_members`, `blocks_add`, `drop_keeps`, `keeps_alone`, `drops_elemental`, `purge`, `purge_original`,
  `summon_min_members`). Core: `ElementalGuildRules` (`GameSession.GuildElementalRules`,
  `GameSession.PurgeRule`); summoning in combat (D-033) and I's town-entry unload keep the game's own rules. In every game
  and value no elemental leaves the game: the records stay in the Guild (`ElementalTemplates`), Purge refuses an
  elemental (`GuildHall.PurgeRefusal`, also a record of race 16 in a roster), and Drop / Purge only unload the party copy.
- The original-code preset keeps the id `dos`; its label is per game: `options.preset.dos.<game>` when the string table
  has it (II: "ORIGINAL (ATARI ST)"), else `options.preset.dos` (`OptionPresetExtensions.LabelId`).
- I's menu order: elemental prep, item stash, Armory restock, gold cap, rename party members, stunning blow, armor reduced,
  payout remainder, then the P3.2 switches in the order above, then the every-game options (`elemental-load`, `elemental-drop`, `elemental-purge`, `guild-limit`).
- The time lag (D-073) is **not** a rules option: it changes pacing, not the game, so it is a presentation switch (sections 2 and 3).
- Each later rules item (`PLAN.md` P3.2) adds one descriptor and nothing else.

## 5. Locking per save

- A session holds one immutable `GameOptions` (preset, option values, enabled module ids). It is set when the session is
  built for a new game and replaced only by loading a save. The session's `ElementalPrep` and `FeatureModules` follow it.
- The save writes it once at the top level (`options`), next to the roster and towns. D-069 parked parties never carry it:
  the whole save shares one set of rules.
- A save without an `options` block (written before s73) loads as Original (manual). An unknown option id is ignored; a
  missing one takes its preset's value (Custom → the manual value). No save version bump.
- **DOS import (F7)** keeps the running session's rules: a DOS save holds no remaster options, and the running rules are the
  game tab's defaults (new game) or the loaded save's.
- **Continue (D-069)** loads the file's rules with the chosen party.

## 6. Settings file

```json
{
  "integerScale": 3,
  "games": {
    "p1": {
      "graphics": "amiga",
      "titleScreen": true,
      "titleTune": true,
      "diagonalMoves": true,
      "statusLines": false,
      "continueFromList": false,
      "inventoryScreen": false,
      "timeLagInOptions": false,
      "colorblindPalette": false,
      "newGameRules": { "preset": "manual", "values": { "p1.elemental-prep": "prepared" }, "modules": ["p1.combat-orders"] }
    }
  }
}
```

The top-level `numpadDiagonals` / `statusLines` of older settings files move into `games.p1` on first load.

**Colorblind palette (P3 QoL):** `colorblindPalette` on → the Client's `ColorblindFilter` remaps exact colours of the
finished frame from `GameData/shared/colorblind.json` (`red-green`, for deuteranopia and protanopia: the status/HP colours,
the map fallback and auto-map colours, the confusable Amiga map-art and combat-palette colours, to Okabe-Ito targets), after the pack's
`ScreenPalette`. Off (the default, and on the main menu) there is no filter and the render target is presented untouched.

## 7. Not in this step

- Drawing the DOS CGA pack fully (per-cell overworld and dungeon, town crop) is still T5 work; the graphics row offers every
  pack whose files are present, and a CGA picture the pack lacks shows nothing.
- The Characters / Hall entry on the main menu (section 1). Party transfer and the IV transfer and lineage options are
  built (`09-trilogy.md` T5).
- Gamepad navigation of the new screens follows the existing `InputMap` actions (Confirm / Cancel / directions).

## 8. Title splash (s102)

Picking a game in the main menu first shows that game's start-up pictures as its original showed them at boot, then the
game's own start. Data: the chosen pack's `title_sequence` (a canvas size, then steps: a picture drawn 1:1, text lines
from the game's string table, a time in seconds or VBLs, or none = until a key); Client `TitleSplash` (a shell state,
its own letterboxed canvas, no pack palette). A key or click ends the current picture (as a key ends the Amiga I tune),
Esc skips the rest. A step whose original length is not decoded waits for a key. Per-game presentation switch
**TITLE SCREEN** (`titleScreen`, on by default = as the originals).

**Title tunes (s108):** the sequence's `tune` (format, the original file under the pack root, offset / length,
`until_step`, `vbl_hz`, `clock_hz`) is decoded headless (`OpenPhantasie.Data.Audio`) and played on a model of the original's
chip (`OpenPhantasie.Client.Audio`: Paula, YM2149), streamed through MonoGame; no sound device → silent. It starts with the
first picture and stops with the splash (I: with cover1, `until_step` 1), unless the tune has `plays_on` (s157, III): then
it plays on into the game until its end or the game's first sound effect (`Audio.TitleTuneCarry`; also stopped when the
player goes back to the main menu or starts another game, or switches TITLE TUNE off; a new game or a load in the same
game keeps it). TITLE SCREEN off shows no splash and starts no tune. A step with `until_tune_ends` lasts until the tune's
end, counted from the splash's start (I's cover1, 53.56 s; s156 II's composite, to 64.62 s), whether the tune plays or not. Per-game switch **TITLE TUNE** (`titleTune`, on by default, the
row under TITLE SCREEN, `options.title_tune` + `.desc`). I's town "Sound" option is the game's own and does not gate it.
**Amiga audio filter (s160, user s159):** every Paula sound (I's and III's title tunes, their sound effects and tones,
III's reward tune) goes through the A500's analogue output filters (`A500AudioFilter`, Amiga `FORMAT.md` "Title tune":
the fixed 4421 Hz pole and the LED filter in, as both games leave it). Per-game presentation switch **AMIGA AUDIO
FILTER** (`a500Filter`, on by default = as the A500; off = Paula's raw output; the row under TITLE TUNE on the I and III
tabs only, `options.a500_filter` + `.desc`). Read when a tune or effect run starts (`TuneRenderer.For`, the
`SoundEffectPlayer.A500Filter` callback), so a sound already playing keeps the old setting. The ST's and X68000's
chips have no such switch: each always goes through its machine's output stage (the STF's C10 low-pass in `Ym2149`,
s163; the first X68000's `X68000AudioFilter`, s162 / s163).
IV's tune (s109) is OPMDRV.X MML compiled by the driver's rules and played on a YM2151 model (`OpmDrvScore`,
`Ym2151`; 4 MHz, 85 min of 256 rounds with the measured lost timer ticks of voice-loading passes (s156), cut off with
the splash). A pack without a
sequence (I's DOS CGA) shows nothing. `--title-screen N` (1–4) opens on game N's splash whatever the switch says, then
starts game N as the menu would; `--test-party` and the `--p4-*` previews skip it.

| Game (pack) | Original boot | Remaster steps |
|---|---|---|
| I (Amiga) | cover1 while `tamb.dat` plays (a key / click ends it), cover2 (credits) for `Delay(250)` = 5 s | `tamb.dat` on Paula; cover1 for the tune (53.56 s) or until a key; cover2 5 s |
| II (Atari ST) | Dosound, COVER1, 251 VBLs, COVER2 over it at line 65, PHANT.PRG loads and waits (`0xb8f2`) until the tune has ended (s156) | the Dosound tune (64.62 s) on the YM2149 under both; COVER1 251 VBLs (pack `vbl_hz` 60: the US release's NTSC TOS, decided s109); the composite until the tune's end (`until_tune_ends`, counted from the tune's start) |
| III (Amiga) | Title.cmp while the files load (D/M0 playing), cleared with no key; manual: "Hit any key" (D3-065: wait for a key) | `D/m0` (48.28 s at 60 Hz `[~]`); title until a key; the tune plays on into the game until its end or the first sound effect (s157, `plays_on`) |
| IV (X68000) | window "Phantasie IV": song 0 starts (FM, 5 tracks), "STARCRAFT Presents" while OPENANI / OPEN load, the picture with "By Winston Douglas Wood" under it, torches animated, then New Game / Saved Game over it | "STARCRAFT Presents" until a key; picture + credit (320 × 238 canvas: the 21-row title bar seen in XEiJ, s110) until a key; the torches animate at the measured 5.75 ms a pass (s110); song 0 on the YM2151 under both, stopped with the splash |

Notes per platform: `reverse-engineering/amiga/FORMAT.md` "Title", `atari-st/phantasie2/FORMAT.md` §7 / §10.2,
`amiga/phantasie3/FORMAT.md` "Title", `x68000/phantasie4/FORMAT.md` §6.10 / §6.11.
