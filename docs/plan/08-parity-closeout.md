# Phase 8 — DOS Parity Closeout + Inventory QoL

> **Historical package definitions.** 1:1 parity was signed off by the user (s63). The checkboxes below are frozen at
> 2026-10-10 by design and are not ticked any more: open state lives in `PLAN.md` (section 3).

**Goal:** take the M8 remaster to **1:1 behavioural parity with the SSI 1987 IBM PC (DOS) release**, then add a few
**explicit QoL upgrades** that keep that parity. The headline QoL item is a **per-character inventory**: equip /
unequip / transfer gear without redistributing the loot sack or re-equipping the whole party.

**When:** after Phase 6 M8 and Phase 7 high/medium QoL (both done). Rule of this phase: "decode it or prove it matches
DOS", not "descoped is fine".

**Source of truth (this file's era; the manual now ranks first, `AGENTS.md`):** `reverse-engineering/dos/original-files/`
under DOSBox, backed by `logic-specs/` + Ghidra. Amiga art is cosmetic; on numbers / rules **DOS wins** over Amiga.

**Parity definition (what “1:1” means here):**
- Same rules outcomes (creation, towns, overworld, dungeons, combat, magic, saves, win / lose) for the same inputs and
  RNG stream.
- Same data tables (monsters, items, spells, maps, messages) as extracted from the DOS binaries; no invented formulas.
- The intended remaster **FIX**es from `quirks-and-bugs.md` stay (Q1 gold width, Q2 distribute remainder) unless an
  authentic-mode toggle is added.
- Visuals may use Amiga sprites / tiles at integer scale; layout and flow follow the DOS game's modes and options.

---

## How to work this phase

Packages run **P0 → P3**. Finish P0 (RE gaps) before claiming combat / town parity. Each package is one session:
load this file + the named logic-spec(s), not the whole plan set.

```
P0  Finish open RE / decode remaining [?]
P1  Wire decoded rules into Core + tests (parity)
P2  Client presentation parity (tiles, full mode coverage)
P3  QoL (inventory first) — additive; Core hooks OK if optional/default-DOS
```

---

## P0 — Remaining reverse-engineering (must close for parity)

Descoped in Phases 3–6; **blocking** for DOS parity.

### P0.1 — Character / save record leftovers
Spec: `save-file-format.md`, `character-creation.md`

- [~] Decode **`hl_potions` / `mg_potions` / `treasures`**, 6 runes, ring counters into `Character` + `GuildCodec`:
      **[?]**, offsets unpinnable (all fixtures zero; the sheet renderer reaches the format strings indirectly, so field
      reads can't be traced). Only the sibling `scroll(s)` counter is pinned (= `@28`). Nullable stubs, not invented.
- [x] **`@28` dual-use** (scroll_count vs weapon_hit) **[C]**: combat prep `FUN_0000_3263` (OV2 `0x3290`) *copies*
      `@28` → effective weapon-hit `@248` and never overwrites `@28` (`combat-resolution.md`, `save-file-format.md`).
      The remaster matches (one word).
- [x] Status encoding: status is `@24` (0 DEAD / 1 OK / 2 SLEEP), `@218` is the in-party flag, UNDEAD is race 15 at
      `@256` (PLAN H3/H4).
- [x] Counter `@260` and other `[?]` GUILD fields **[C]**: `@260` = `2×@92+10` party-order stamp (static, **not** a
      clock; `@264` is the age counter); `@92` roster ordinal, `@256` race echo. Wired into `Character` / `GuildCodec`
      with round-trip tests.
- [ ] **Non-zero `SACK.DAT`** fixture + exact layout: **blocked**, every fixture / snapshot is all zero (no combat
      captured). Write-timing hypothesis documented; layout **not invented** (semantics exist as `LootSack`).
- [x] Q4 DOS strong-race roll suppression **[C]** in OV1 `0x0C28` (RANDOM path); **PRESERVE** (authentic anti-cheese,
      `quirks-and-bugs.md` Q4).

### P0.2 — Town economy & services
Spec: `town-menus.md`, `data-tables.md`

- [x] **Armory buy price** **[C]**: no CHA or other discount, OV1 charges full list price. Pass-2's 177-GP shared-purse
      purchase = `177 / 3 = 59` from each member's `@262` share.
- [x] Guild **training** XP threshold + gold cost **[C]**: six stat sums A–F (`DEX*2+STR`, `INT+DEX+STR`,
      `INT+CHA+STR`, `DEX+STR`, `INT+STR`, `INT*2+LUCK`); level-1 threshold `mult*1000 / statSum` (low stats need *more*
      XP; a live DOSBox-X capture disproved the old `statSum * mult`); gold
      `min(15000, (long)pow(level, 2.2) * max(20-CHA,0)^2 * price_index / 100)`. The whole float chain (`1.8^level`, the
      XP-0 reset, the `+1e-5` term) and every class's formula are in `town-menus.md` section Guild; built in
      `TownMenus.cs`. Evidence for the old dispatch-table dead end (`CS:0x669a`): `HANDOFF-2026-07-18.md`,
      `working/training_capture_2026-07-18.md`.
- [ ] Temple heal/raise **cost curves**
- [x] Mystic **SCORE** **[C]** from raw capstone disassembly of OV1 `0x20bb..0x225a` (Ghidra drops it as unreachable
      because it is jump-table dispatched): `score = floor((sum_of_member_levels + 18*distinct_ring/wand_ids_held +
      66*wand_held + 1) / 3)`; ids 141-150 = RING A-I / WAND (`items.json`). Tier thresholds [C]; which OV1 string goes
      with which tier [~] (string-table order; the score is unaffected). `town-menus.md` section Mystic.
- [ ] Inn **aging** thresholds / death-from-age (Q7) — expose as data tables
- [x] Spell learn fees **[C]**: `town.price_index * level * 90 / (CHA * INT * (9 - class_id))` (OV1 `0x154d..0x165c`),
      independent of spell id. (`spellId % 4 + 1` is the MP cast cost.)

### P0.3 — Overworld
Spec: `overworld-movement.md`

- [ ] Encounter roll / time coupling (exact `roll < 35` linkage)
- [ ] Region link edge cases (`OUT13↔OUT12` asymmetry if real)
- [ ] Any wilderness triggers still stubbed in `Overworld` / `GameSession`

### P0.4 — Dungeon persistence & events
Spec: `dungeon-representation.md`, `save-file-format.md`

- [ ] Full **`DNG.SAV` bit → feature** map (replace `ClearedEventKeys` stand-in)
- [ ] Full **`DNGX.SAV`** field map (party position, dungeon state)
- [ ] Scripted event / message / treasure / trap / stairs behavior vs DOS
- [ ] Q5: document dungeon reset vs unique-item re-duplication; PRESERVE by default, optional FIX behind a setting

### P0.5 — Combat polish
Spec: `combat-resolution.md`

- [~] Equip selector **[C]**: points → weapon_dmg_B / shield + armor soak; damage_A and the weapon_hit writer open
- [ ] Initiative / turn order (DEX / quickness) coefficients
- [ ] SLASH swing count rules (3 vs 4)
- [ ] Status effects ordering (SLOWED, etc.)
- [~] Monster runtime table (`DS:0x1a6c`, stride `0x39`) captured live in DOSBox-X; the table is in `data-tables.md`
      Monsters (the capture files were removed). **[C]** attack1 / attack2 hits_or_count, damage_max, extra (offsets
      +0x14/+0x16/+0x18 and +0x23/+0x25/+0x27; 67/72 and 11/11 exact vs `monsters.json`; damage_max matches the manual
      for VIPER=14 / SKELETON=7). Armor / defense / attack_power: the `HANDOFF-2026-07-18.md` offset guesses are right
      (an old check that "disproved" them had parsed EXEPACK-packed bytes; `RE-REFERENCE.md` do-not-redo list).
- [ ] Optional: DOSBox per-swing sampling fixtures, if unit tests alone leave drift

### P0.6 — Magic polish
Spec: `magic-spells.md`

- [ ] **HEALING** RNG range verified vs DOS
- [ ] Remaining **special** spell effects (summon, buffs, undead detection, …)
- [ ] Refresh `spells.json` notes so no `[?]` remains for shipped spells

### P0.7 — Map tile graphics (presentation parity)
Spec: `amiga/FORMAT.md` (tiles section); DOS CGA tiles also acceptable

- [ ] Locate overworld + dungeon **tile bitmaps** (Amiga `game` and/or DOS `*.IBM` / PAT), extract to PNG
- [ ] Extend `asset-manifest.json` so every map cell class maps to a tile asset
- [ ] Drop Client’s colored-rectangle placeholder once tiles exist

---

## P1 — Core parity implementation

Every confirmed P0 rule goes into `OpenPhantasie.Core` / `OpenPhantasie.Data` with headless tests. No invented numbers.

### P1.1 — Data & codecs
- [ ] Update GUILD / SACK / DNG / DNGX codecs to match P0 field maps
- [ ] Round-trip tests against real DOS fixtures (`extracted/samples/`, new captures in `working/snapshots/`)
- [ ] Clear `[?]` stubs in `CharacterSchemaNotes` / town / spell JSON as each item lands

### P1.2 — Systems
- [ ] TownMenus: real discount / train / mystic / aging / learn fees (no temple: DOS has none)
- [ ] CombatGear + Combat: DOS-accurate equip fill, initiative, swing counts
- [ ] Magic / SpellCaster: HEALING + specials
- [ ] DungeonTraversal: real persistence + events
- [ ] Overworld: encounter/time coupling

### P1.3 — Parity verification
- [ ] Expand `PlaythroughParityTests` (and focused suites) so every former `[?]` has a golden assertion
- [ ] Side-by-side checklist vs DOSBox: create → guild → town buy/sell → wilderness fight → inn distribute → dungeon
      clear → resurrect (spell) → train → mystic → save/load → endgame path as far as practical
- [ ] Document the remaining intentional deviations (Q1/Q2 only, unless new authentic-mode toggles)

### P1.4 — Save-file compatibility *(scope agreed with the user s38, 2026-10-11)*
How remaster saves and original DOS saves relate. Format facts: `reverse-engineering/dos/logic-specs/save-file-format.md`.

**Decisions (user, s38):**
1. **Both directions.** Import a DOS save folder into the remaster; export a remaster game as DOS files
   that the original game runs.
2. **All five files:** `GUILD.DAT`, `SACK.DAT`, `TWNS.DAT`, `DNG.SAV` + `DNGX.SAV`.
3. **Import loads as-is, then warns.** Nothing in a DOS save is clamped, moved or rejected for breaking a
   manual-based rule (e.g. a 22nd character, D-002: all 22 stay; creation is blocked until under 21). The
   player gets a list of every field / state the remaster would not have produced.
4. **Byte-exact where possible.** Import → export with no game changes gives identical files. Unknown and
   stale bytes (`@94..@214`, the DNGX tail, unmapped `TWNS.DAT` bytes) are carried through unchanged; known
   fields are written over the carried bytes. Pinned by fixture tests.
5. **Sack > 60 items blocks export** with a message (remaster holds 90, D-018; DOS 60). Nothing dropped silently.
6. **Export is a town action only**, like DOS Option→Save game (manual: "S)AVE GAME is always accomplished in a
   town"): the carried XP/gold pool is split into shares first (town-entry split, D-037), so the result matches a DOS
   save. The manual is silent on the split, so it is **automatic** (user s40), `DosSaveConverter.AutoSplit`: fight
   XP/gold paid out as on town entry, carried gold into the living members' banks in equal shares (remainder to the
   first), on copies (the live game keeps its purse). Blocks only when no member is alive.
7. **Out-of-range values on export are clamped and listed** in the same warning list (e.g. D-003 elf age
   clock > 65,535 in u16 `@264`; bank gold > 65,535 in u16 `@262`).
8. **No old remaster saves are kept loading** (user s40): "do not spend time trying to preserve old saves of any sort.
   Just make a new save if things change enough to break one." No migration chain or per-version fixtures; a save
   that no longer loads is refused (the v1 mid-combat refusal stands). Existing migration code stays, nothing new is
   added. DOS import/export (decisions 1–7) is unaffected.
9. **Entry point:** `OpenPhantasie.Data` API + tests first, then a Client menu entry (title / town Utility) that
   takes a folder path. Export never overwrites a folder without confirmation.

**Work items** (open checkboxes in `PLAN.md` section 3; closed ones in `docs/plan/BOARD-ARCHIVE.md`):
- Raw-record carry: keep each imported 282-B GUILD record (and the other files' unmapped bytes) through the
  session and its JSON save; `GuildCodec.WriteRecord` overlays known fields instead of `r.Clear()`. Same for
  `@252` lifespan: write the imported DOS value back unless the D-003 band changed it.
- `SackCodec` (60 × u16 ids ↔ `LootSack.ItemIds`; XP/GP not in the file — 0 on import).
- `TownsCodec` (12 × 291 B ↔ `TownState` stock 0..180 + price index `0x10f` + name; rest carried raw).
- `DosSaveSet` import/export API over all five files + DNG/DNGX codecs + warnings list
  (rule breaks on import, clamps on export, blockers: not in town, sack > 60).
- Fixture tests: every `extracted/samples/` set imports and re-exports byte-identical.
- Client menu entry + overwrite confirmation (P2 side).

---

## P2 — Client presentation & mode completeness

Rules parity is P1; this package is "the PC game plays end to end with correct visuals and full menus".

- [ ] Render overworld / dungeon with extracted tiles (P0.7)
- [ ] Town / guild / temple / bank / mystic / inn / armory UIs expose every DOS option Core supports (no silent stubs)
- [ ] Combat UI shows party / monster state as the DOS flow does (actions, targets, flee, spells, potions)
- [ ] Character sheets show decoded counters (potions, runes, ring, score, skills) once P0.1 lands
- [ ] Phase 7 QoL stays (save-anywhere, speed, auto-map, scale, bestiary, remaps) without altering Core formulas

---

## P3 — QoL upgrades (post-parity defaults)

**After** P1 formulas are locked, so QoL cannot paper over wrong math. Each item is additive; defaults stay DOS-like.

### P3.1 — Proper per-character inventory (required)

**Problem (2026-10-10):** worn slots auto-update on buy (`AutoEquipIfBetter`); the **V** overlay is a read-only
compare; there is no equip / unequip / give / drop, so players distribute loot and hope auto-equip picks well.
(Built as T2, `09-trilogy.md`.)

**Target UX:**
- Per-character inventory panel (not party-blob only)
- Actions: **Equip**, **Unequip**, **Use** (potions / scrolls where legal), **Give** (to another living member);
  **Drop** / sell still in town
- Explicit weapon / armor / shield slots; unequip returns the item to that character's carried list
- DOS usability gates (STR etc., `ItemRules.IsUsable`); show why an equip failed
- Does **not** require Inn distribute or party-wide re-equip
- Optional: drag-drop once sprite inventory art exists (Phase 7 deferred this)

**Core work (allowed):**
- [ ] `InventoryActions` on `Character` / `Party`: Equip, Unequip, Transfer; update worn ids +
      `CombatGear.ApplyWornItemPoints` through the **P0.5-accurate** fill path
- [ ] Unit tests: equip worse / better gear, unequip mid-dungeon, transfer, can't equip unusable, selling worn clears slot
- [ ] `GameSession` wrappers for Client

**Client work:**
- [ ] Replace read-only **V** overlay with interactive inventory
- [ ] Keyboard + gamepad: select character → select item → action menu
- [ ] Compare pane kept (points vs currently worn)

### P3.2 — Other QoL (nice-to-have in this phase)

Carried from Phase 7 "larger scope", plus inventory polish:

- [ ] Authentic-mode toggle for Q1/Q2 (16-bit gold truncate / discard remainder); default = remaster FIX
- [ ] Difficulty modifiers (Core hook; off by default)
- [ ] Colorblind-friendly palette pass
- [ ] Inventory sort/filter + drag-drop (once art allows)
- [ ] Party import/export I↔II/III: **defer** until sequel data exists

Out of scope unless reopened: audio remaster, multiplayer.

---

## Stale checklist cleanup (docs only)

- [x] `01-legal-and-sourcing.md`: originals present; DOSBox + ADF verified
- [x] `02-toolchain-setup.md`: Ghidra snap + DOSBox smoke + prior tool checks
- [x] `05-engine-architecture-csharp.md`: solution / schema done-criteria marked

---

## Done-criteria for Phase 8

- [ ] Every former Phase 3–6 `[?]` / `[~]` that affects gameplay is **[C]** in logic-specs and implemented, or listed
      as non-gameplay / N/A with a reason
- [ ] GUILD / SACK / DNG / DNGX round-trip against real DOS fixtures
- [ ] Town, combat, magic, dungeon, overworld match DOS side by side + in automated golden tests
- [ ] Map tiles extracted and used in the Client (no rectangle placeholders)
- [ ] **Interactive per-character inventory** ships (equip / unequip / give / use) without loot redistribute or
      party-wide re-equip
- [ ] Phase 7 QoL remains; Core combat / movement formulas change only where P0 proved DOS differs from the old stub
- [ ] `dotnet test` green; a playthrough reaches late-game content without stubbed menus

## Suggested session order

1. P0.1 + P0.2 (saves + towns): highest player-facing drift
2. P0.5 + P0.6 (combat + magic)
3. P0.4 + P0.3 (dungeon + overworld)
4. P1 implementation + parity tests for the above
5. P0.7 tiles → P2 Client
6. **P3.1 inventory QoL** (after equip fill is DOS-accurate)
7. P3.2 optional QoL + doc checkbox cleanup  
