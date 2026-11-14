# Phase 9 — The series: Phantasie I, II, III and IV in one game

**Goal (user, 2026-10-25; IV added the same day):** one remaster plays all four Phantasie games from a single interface.
The file keeps its `09-trilogy` name so existing links stay valid.

- **Cross-game features.** What II, III and IV added (rock throwing, body-part injury, lineage…) can be used in every
  game as an on/off option. Each game's own behaviour is the default.
- **Party transfer.** Characters move between games the way the originals allowed, so one party can play all four.
- **Graphics per game and platform.** For each game the player can pick any platform that game shipped on. Amiga is the
  default where it exists; otherwise the best-looking platform by common sense (II: Atari ST; IV: X68000).

**Source of truth:** each game's own manual, the same rule `AGENTS.md` sets for I. II: the combined "Phantasie I and
Phantasie II Rulebook" (`docs/manuals/phantasie2/phantasie2-manual.pdf`; II's changes on its p.27). III:
`docs/manuals/phantasie3/phantasie3-manual.pdf`. IV: the Japanese PC-9801 manual
(`docs/manuals/phantasie4/phantasie4-pc98-manual-jp.pdf`; English in `phantasie4-manual-en.md`). Each game has its own
differences file: `DIFFERENCES.md` (I), `DIFFERENCES-II.md` / `-III.md` / `-IV.md`. Between versions of one game, the
version numbers come from is set per game (T0; decisions at the end of this file).

**Rules for new code:** nothing may hardwire Phantasie I, Amiga, or English-only text. I must keep playing as it did:
every existing test stays green and nothing DOS ✅ in `PLAN.md` changes.

**Where state lives:** `PLAN.md` owns the open items (section 3, "Phase 09"). This file defines the packages and says
what each one built, with pointers to the specs. Session detail is in `SESSION-LOG.md`.

---

## T0 — Sourcing and feature matrix (done s69)

Manuals in `docs/manuals/<game>/` (index `docs/manuals/README.md`). Disk images per game and platform under
`reverse-engineering/<platform>/original-images/<game>/`, each platform with a `SOURCES.txt` (origin, version,
clean/cracked, sha256); 11 platforms including the Japanese ports. Platform matrix, feature matrix (home game per
feature) and transfer rules: `09-trilogy-matrix.md`, with page citations in `09-trilogy-manual-p2.md` / `-p3.md`.

## T1 — Groundwork inside Phantasie I (no behaviour change; built s71–s76)

- **T1.1 Shared character model.** `Character` is the remaster's own record with room for all four games; I-only
  fields (runes, `Word90`, the kept DOS record) live in `Phantasie1CharacterData`. `GuildCodec.Field` owns I's 282-byte
  layout. `Lineage`, `BodyCondition` and `Deeds` slots are on the shared model.
- **T1.2 Game-prefixed ids.** `ContentId` (`p1:42`…`p4:`) at the data and save boundary; Core keeps local numbers. DOS
  save import/export maps to the bare DOS ids. Old remaster saves are not migrated.
- **T1.3 Rules profile per game.** Constants that differ between games live in `GameData/<game>/rules.json`
  (`RulesProfile`); constants shared by all games stay in code.
- **T1.4 Shared engine vs campaign.** `GameSession` runs the shared loop; `ICampaign` (`Phantasie1Campaign` … 
  `Phantasie4Campaign`) owns each game's scripts, endgame and world hooks, reached through `GameSession.CampaignAccess`.
  `MinimalCampaignTests` run the loop on a non-I campaign.
- **T1.5 Graphics packs and text.** The Client asks for named pictures, never files: one manifest per game and platform
  (`GameData/<game>/graphics/index.json` + `amiga.json`, `atari-st.json`, `x68000.json`, I's opt-in `dos-cga.json`).
  Per-pack fonts (`IFontSource` / `BitmapFont`, `graphics/fonts/<id>.json`), pack palette (`ScreenPalette`), layout →
  pack resolution (`VirtualScreen.LayoutTransform`). All on-screen text goes through layered string tables
  (`StringTable.ForGame`: `shared` under the game's); Core looks text up by id through `GameText`.
- **T1.6 Feature modules.** Combat actions and every damage event pass a seam (`FeatureModules`, `Party.Modules`) where a
  module plugs in. Each module has a home game (on there by default) and is an option elsewhere. Golden combat-trace
  hashes guard I.

**Verified:** 181 Xvfb screenshots pixel-identical to the pre-T1 build; saves round-trip byte-identical.

**Left over** (open on `PLAN.md` as "T1 leftovers"; they wait for a need):
- Space-padded column alignment (`en.json` `{0,8}` formats, `SkillLines` / `CounterLines` `PadRight`, `EquipLine`'s
  12-char cut) needs pixel tab stops.
- Fonts are drawn in layout pixels (no per-font pixel scale), and art at layout size (no per-pack placement).
- The saved stopped-event state is still I's `DungeonScriptResume` (`PendingDungeonScript`,
  `DungeonSnapshot.ScriptResume`), kept so saves stay byte-identical; it becomes campaign-owned when a campaign needs one.
- I's world facts are campaign code constants, not data.
- Elementals, autosave and rename become profile or module flags once a game needs them. (The bank fee: covered by the
  every-game Guild treasury option, s175, `11-guild-treasury.md`.)

**Guild size per platform** (`RulesProfile.GuildByPlatform`, `guild_by_platform` in rules.json; `null` = `[?]`). The
remaster plays 22 slots / 21 max (D-002); `ForPlatform(id)` exists but nothing calls it yet. Phantasie I:
- **DOS** 22 slots / 22 max `[C]`: GUILD.DAT 22 × 282 bytes, DOSBox s9 filled 22. No DOS manual states a number (the SSI
  US manual is the Apple rulebook's 37; WizardWorks' 21 is in its C64 section).
- **Amiga** 22 slots `[~]`: `amiga/unpacked/PHANT/guild.dat` is 25 × 282 bytes, records 0–21 plus the three elementals
  at 22–24 (DOS takes those from TWNS.INT). Fillable max `[?]`.
- **Apple II** 37 / 37 (rulebook; the sample list numbers the elementals 38–40).
- **C64** max 21 (C64 supplement: "A maximum of 21 … and not 37 as in the Apple version"). Slots `[?]`.
- **`[?]`:** Atari 8-bit, Atari ST, Mac, MSX (no number in any readable manual).

## T2 — Inventory rework (08 P3, the user's top QoL item; built s74)

Core `InventoryActions` (spec `logic-specs/inventory-actions.md`; slots from `rules.json` `equipment_slots`, including
III's bow slot), rules option `p1.stash-access` (town / anywhere), Client `InventoryScreen` (paperdoll + stash;
keyboard, mouse, drag, gamepad) behind the presentation switch `GameSettings.InventoryScreen` (off = I's original).
Usability per game since s171 (`ICampaign.CanUseItem`: I / II id formulas, III `P3TownEntryPayout.CanUse`, bows on
STR + DEX). No item art: none of I / II / III's originals has item pictures (only IV's X68000, font 2, shown in IV's own
lists). Left: the follow-ups on `PLAN.md` P3 "Inventory".

## T3 — Phantasie II (Atari ST; playable since s81)

RE plan and status: `03b-phantasie2-re.md` (one file for 03b + 04b). Specs and formats in
`reverse-engineering/atari-st/phantasie2/` (`FORMAT.md`, `logic-specs/`, live Hatari checks in `parity/`).
- **Data and rules:** `GameData/phantasie2/` (built by `tools/scripts/build_p2_gamedata.py`, which since s212 emits
  the formerly hand-edited blocks too: a rerun is safe, `--check` passes), `rules.json` (training, terrain, travel time, Motorola FFP arithmetic),
  `Phantasie2Campaign`. Every DIFFERENCES-II decision is built, with the ST's behaviour as `p2.*` options.
- **Modules:** Toss Rock is module `p2.rock-throwing` (an option in the other games).
- **Art:** `GameData/phantasie2/graphics/atari-st.json` (default; ST fill patterns as `overworld.pattern.N`, D2-014),
  the ST town screen, defeat scene with the sinking door and ghost row, the ST ending. No map legend: the ST has none.
- **Left:** `PLAN.md` "T3 II left" (combat line pacing, sounds, end-of-fight boxes, ending font, all `[~]` / `[?]`).

## T4 — Phantasie III (DOS rules, Amiga art; playable since s83)

RE plan and status: `03c-phantasie3-re.md`. Formats: `reverse-engineering/dos/phantasie3/FORMAT.md` (DOS) and
`reverse-engineering/amiga/phantasie3/FORMAT.md` (Amiga screens); specs in `reverse-engineering/dos/phantasie3/logic-specs/`.
- **Campaign:** `Phantasie3Campaign` (Core `Phantasie3/`, world from `GameData/phantasie3` via `P3WorldData`): Scandor
  and the planes (`planes.md`), dungeons with all 27 event ops (`dungeon-events.md`), encounters, towns, Bank / Trade /
  Armory / training by the DIFFERENCES-III decisions (`p3.*` options), elementals by III's rule, the Astral Plane, the
  endgame and the gods' reward.
- **Combat:** III's own module `p3.combat-actions` (`P3CombatModule`, `P3RoundResolver`, `P3Fight`): actions, ranks,
  body-part injury both ways, bows and Arrow Flame, parry, Accuracy, monster specials, treasure by Find Item.
  Body-part injury, bows, Aim and party ranks as modules for the other games are placeholders (`Implemented: false`).
- **Art:** `GameData/phantasie3/graphics/amiga.json`: overworld and planes, dungeon map glyphs (`tile_maps`), monsters,
  portraits, town and title pictures, scrolls, and under the AMIGA SCREENS switch the Amiga's combat field, orders
  screen, Bank / Trade / spell / heal windows and menu bar. No DOS (CGA/EGA) pack for III.
- **Left:** `PLAN.md` "T4 III leftovers", "T4 III art left" and "T4 III RE left".

## T5 — One game, four titles

- **T5.0 Startup screen and options (built s73):** spec `09-startup-options.md`. Main menu picks the game; Options has a
  General tab and one tab per game; rules options are locked per save; four presets (Original manual / Original DOS /
  Remaster / Custom). Every rules option or QoL toggle is one `RuleOptionRegistry` descriptor; every cross-game module
  shows in the game tabs' "from other games" list.
- **Party transfer** (spec `transfer.md`; IV side `ancestry.md` sections 8.2–8.4 and 13): I → II (s128), II → I (s145),
  I / II → III (s147, `p3.transfer-*`), III → IV Hall of Glory (s148), and three IV options off in every preset (s149):
  III → IV Guild join `p4.transfer-join`, I / II into IV's Hall `p4.transfer-games`, lineage at creation in I–III
  `p4.lineage` (base races only). Left: the `[~]` / `[?]` list on `PLAN.md` "T5 party transfer".
- **Cross-game modules:** built: `p2.rock-throwing`, `p4.lineage`. Not built: III's body-part injury, bows, Aim and
  party ranks for the other games. Monster data for them (resolved s75): injury gives every monster the six areas (III:
  "every character and monster"); height from III's records for monsters III shares by name, else the closest III
  monster, else normal. Rock throwing needs only a Toss Rock skill for I characters, set as II's transfer does (GUILD
  field 42 `[~]`).
- **Graphics option:** per game, any platform pack that is present (row built s73). The CGA pack for I is opt-in and
  draws only what it maps: per-cell overworld / dungeon and the town crop are not drawn.

## T6 — Phantasie IV: The Birth of Heroes (X68000 rules and art; playable since s106)

Japan-only (Star Craft, 1990–91), designed by Winston Douglas Wood (RPG Codex interview: "I designed Phantasie IV
completely"), so it belongs to the series. Platforms (all in the repo): PC-88 and PC-98 Sep 1990, MSX2 Jan 1991,
**X68000 Mar 1991 = rules source**; PC-98 is the cross-check. Each new character has two ancestors (new, or a person from
the Hall of Glory); stats are partly inherited. Analysis: `09-trilogy-iv-parentage.md`. RE plan and status:
`03d-phantasie4-re.md`; formats in `reverse-engineering/x68000/phantasie4/FORMAT.md`, specs in its `logic-specs/`.
The 2008 DJANGO English patch for MSX2 (`msx2/…/phantasie4/patch-2008/`) is a cross-check only.

- **T6.0 Sourcing (s71, s81):** images for all four platforms, the PC-98 manual, spell list and supplement, magazines,
  item catalogue (`docs/manuals/phantasie4/`). Open: the X68000 third floppy `[?]` (probably a player save disk, not
  dumped).
- **T6.1 Translation:** the manual (`phantasie4-manual-en.md`) and the in-game text (1279 strings,
  `reverse-engineering/x68000/phantasie4/extracted/text/strings.json`, notes in `translation-notes.md`) into IV's string
  tables, keyed by id. Open: text inside pictures; an in-game read-through.
- **T6.2 RE (X68000):** all 705 functions named, specs complete, event bytecode and PKH pictures decoded;
  `DIFFERENCES-IV.md` decided.
- **T6.3 Rules and campaign:** `GameData/phantasie4/` (`build_p4_gamedata.py`, `strings/en.json` + `ja.json`), Core
  `Phantasie4/` with the `p4.*` options, `Phantasie4Campaign`: Pendragon, creation at the Guild with ancestors, every
  town building, dungeon, outdoor, 7×7 combat (`P4Battle`), F/P/C/U menus, the five endings, saves. The Hall's deeds lines
  come from each person's I / II / III results (`ancestry.md` section 8); the shared `Character.Deeds` record feeds them for
  imports (I: Black Lord, II: the orb). Other deeds have no Hall line in IV.
- **T6.4 Art (X68000 pack `GameData/phantasie4/graphics/x68000.json`):** built: title pictures and tune, town streets,
  every building and the Hall with its history pages, outdoor map, the location-map (dungeon) 9 × 9 overhead view with
  `_ianim` cycling, the combat field with `p4mon.pix` sprites, endings and scroll page, picture animations at XEiJ-measured pass
  times (`FORMAT.md` section 6.10), P4.X's own ANK fonts where the original draws text on its art (section 6.12), the
  Command panel (`_mkicon`) on every screen whose original has one (section 6.9), the ship mark; s151: the Hall's import
  figures (`_hpix`), the window-34 lists (spell list, Transportation, creation), item icons (font 2), `_ewait` / typed
  prompts / `_numin` / shipwright window / hover look, Welcome!, the title's `_disk(0)` panel; s152: `_sklup`'s level-up window, the combat spell
  list (`_getmagic(member, 0)`), the sound effects (`VDATA`, MSM6258 ADPCM, FORMAT 6.13). Left: `[~]`s only (`PLAN.md`).

---

## Decisions (user)

**s72, 2026-10-26: IV parentage and transfer** (`09-trilogy-iv-parentage.md`; all built s148–s149):
- **III → IV:** faithful by default (the character enters the Hall of Glory as a possible ancestor), plus an option, off
  by default, to join IV's party directly.
- **I and II characters into the Hall:** an option (off = III only, as in the original).
- **Lineage at creation in I–III:** a cross-game option, **base races only** (no hybrids, so I–III need no invented race
  data).
- **Deeds record** on the shared character model, so the Hall can show what a character did.

**s71, 2026-10-25:**
- **Missing art on a platform:** show what that version showed (often nothing or text). Never borrow another platform's art.
- **Scope of the graphics choice:** art, font and palette inside one shared remaster layout. No per-platform screen layouts.
- **II numbers source:** the version released last of Apple II, C64 and Atari ST → the Atari ST.
- **IV rules source:** the version released last → the X68000.
