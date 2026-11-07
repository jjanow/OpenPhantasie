# Phase 8 — DOS Parity Closeout + Inventory QoL

> **Historical package definitions.** 1:1 parity was signed off by the user (s63). The checkboxes below are frozen at
> 2026-10-10 by design and are not ticked any more: open state lives in `PLAN.md` (section 3).

**Goal:** close the gap between the current M8 remaster and **1:1 behavioral
parity with the SSI 1987 IBM/PC (DOS) release**, then ship a small set of
**explicit QoL upgrades** that do not break that parity. The headline QoL
item is a **proper per-character inventory** — equip / unequip / transfer
gear without redistributing the loot sack or re-equipping the whole party.

**When to start:** after Phase 6 M8 (done) and Phase 7 high/medium QoL (done).
This phase replaces “descoped is fine for first playable” with “decode it or
prove it matches DOS.”

**Source of truth:** `reverse-engineering/dos/original-files/` under DOSBox,
backed by `logic-specs/` + Ghidra. Amiga art is cosmetic only — if DOS and
Amiga disagree on numbers/rules, **DOS wins**.

**Parity definition (what “1:1” means here):**
- Same rules outcomes for creation, towns, overworld, dungeons, combat, magic,
  saves, and win/lose conditions given the same inputs and RNG stream.
- Same data tables (monsters, items, spells, maps, messages) as extracted from
  the DOS binaries — no invented formulas.
- Intended remaster **FIX**es from `quirks-and-bugs.md` stay (Q1 gold width,
  Q2 distribute remainder) unless an authentic-mode toggle is added later.
- Visuals may use Amiga sprites/tiles at integer scale; layout/flow should
  still reflect the DOS game’s modes and options.

---

## How to work this phase

Work packages below are **P0 → P3**. Finish P0 (RE gaps) before claiming
combat/town parity. Each package is one Claude Code session sized unit —
load this file + the named logic-spec(s), not the whole plan set.

```
P0  Finish open RE / decode remaining [?]
P1  Wire decoded rules into Core + tests (parity)
P2  Client presentation parity (tiles, full mode coverage)
P3  QoL (inventory first) — additive; Core hooks OK if optional/default-DOS
```

---

## P0 — Remaining reverse-engineering (must close for parity)

These were intentionally descoped in Phase 3–6. They are **blocking** for
true DOS parity.

### P0.1 — Character / save record leftovers
Spec: `save-file-format.md`, `character-creation.md`

- [~] Decode **`hl_potions` / `mg_potions` / `treasures`**, 6 runes, ring
      counters and wire into `Character` + `GuildCodec` — **still open [?]**:
      offsets unpinnable (all fixtures zero; sheet renderer references the format
      strings indirectly so field reads can't be traced). Only the sibling
      `scroll(s)` counter is pinned (= `@28`). Kept as nullable stubs; not invented.
- [x] Confirm **`@28` dual-use** (scroll_count vs weapon_hit) — **[C]** combat
      prep `FUN_0000_3263` (OV2 `0x3290`) *copies* `@28` → effective weapon-hit
      `@248` and never overwrites `@28`; documented in `combat-resolution.md` +
      `save-file-format.md`. Remaster already matches (one word).
- [x] Confirm status byte encoding — ~~`@218` word: 0=empty, 1=OK, 3=UNDEAD~~ **superseded (s10, PLAN H3/H4):**
      status is `@24` (0 DEAD / 1 OK / 2 SLEEP), `@218` is the in-party flag, UNDEAD is race 15 at `@256`.
- [x] Confirm counter `@260` and other still-`[?]` GUILD fields — **[C]** `@260`
      = `2×@92+10` party-order stamp (static, **not** a time clock; `@264` is the
      age counter). Also confirmed `@92` roster ordinal + `@256` race echo. All
      wired into `Character`/`GuildCodec` with round-trip tests.
- [ ] Capture a **non-zero `SACK.DAT`** fixture; document exact binary layout —
      **blocked**: every fixture/snapshot is entirely zero (no combat captured).
      Verified + write-timing hypothesis documented; layout deliberately **not
      invented** (semantics exist as `LootSack`).
- [x] Q4 — DOS strong-race roll suppression — **[C]** confirmed in OV1 `0x0C28`
      (RANDOM path); **PRESERVE** (authentic anti-cheese). `quirks-and-bugs.md` Q4.

### P0.2 — Town economy & services
Spec: `town-menus.md`, `data-tables.md`

- [x] **Armory buy price** — **[C]** no CHA/other discount: OV1 charges full
      list price. Pass-2's 177-GP shared-purse purchase settled as
      `177 / 3 = 59` from each member's persisted `@262` carried-gold share.
- [x] Guild **training** XP threshold + gold cost — **[C]** all six formula
      variants (A–F: `DEX*2+STR`, `INT+DEX+STR`, `INT+CHA+STR`, `DEX+STR`,
      `INT+STR`, `INT*2+LUCK`) and gold cost
      `min(15000, required_xp * max(20-CHA,0)^2 * price_index / 100)`
      **(s39 correction: the gold base is `(long)pow(level, 2.2)`, not required XP; the whole float chain incl.
      `1.8^level`, the XP-0 reset and the `+1e-5` term is now [C] — `town-menus.md` §Guild)**
      implemented in `TownMenus.cs`; 148 tests green. **Formula direction
      corrected 2026-07-18** — level-1 threshold is `mult*1000 / statSum`, NOT
      `statSum * mult` as originally reconstructed from static RE (a live
      DOSBox-X capture disproved the multiply direction: low-stat characters
      need *more* XP, not less). **Class→formula for Thief/Fighter/Priest/
      Wizard (A/D/E/F) at level 1 is [C]** from that same live capture (Thief's
      exact multiplier is [~], slightly high — direction/stat-combo solid);
      **Monk/Ranger (B/C guess) still [?]** — the captured party has neither
      class. The `×1.8^(level-1)` scaling above level 1 remains **[?]**,
      carried over unverified from the original static pass — every captured
      character is level 1. The `CS:0x669a` runtime dispatch table itself was
      conclusively shown to be unrecoverable from the static files (empty/
      unrelated under every addressing hypothesis, cross-checked via a live
      Ghidra headless query) — see `HANDOFF-2026-07-18.md` and
      `working/training_capture_2026-07-18.md`.
- [ ] Temple heal/raise **cost curves**
- [x] Mystic **SCORE** — **[C]**, resolved via direct capstone disassembly of
      the OV1 0x20bb..0x225a handler body (no DOSBox needed — Ghidra drops it
      as "unreachable" only because it's runtime-jump-table-dispatched, same
      as the training table, but raw disassembly reads it fine).
      `score = floor((sum_of_member_levels + 18*distinct_ring/wand_ids_held +
      66*wand_held + 1) / 3)`; ids 141-150 are the RING A-I / WAND items
      (cross-checked against `items.json`). Readiness-tier score thresholds
      are [C]; which OV1 string attaches to which tier is [~] (guess by
      string-table order, doesn't affect the persisted score). See
      `town-menus.md` §Mystic.
- [ ] Inn **aging** thresholds / death-from-age (Q7) — expose as data tables
- [x] Spell learn fees — **[C]** `town.price_index * level * 90 /
      (CHA * INT * (9 - class_id))` (OV1 `0x154d..0x165c`); independent of
      spell id. The old `spellId % 4 + 1` placeholder is the MP cast cost.

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
- [ ] Q5 — document dungeon reset vs unique-item re-duplication; PRESERVE
      default, optional FIX behind a setting if desired

### P0.5 — Combat polish
Spec: `combat-resolution.md`

- [~] Equip selector: **[C]** points → weapon_dmg_B / shield+armor soak;
      damage_A and weapon_hit writer remain open
- [ ] Initiative / turn order (DEX / quickness) coefficients
- [ ] SLASH swing count rules (3 vs 4)
- [ ] Status effects ordering (SLOWED, etc.)
- [~] Monster runtime table (DS:0x1a6c, stride 0x39) captured live via DOSBox-X
      (`reverse-engineering/dos/extracted/monster_runtime_table.bin` +
      `monsters_runtime.json`, removed s22; the table is in `data-tables.md` Monsters). **[C]** attack1/attack2 hits_or_count,
      damage_max, extra (offsets +0x14/+0x16/+0x18 and +0x23/+0x25/+0x27;
      67/72 and 11/11 exact match vs `monsters.json`, damage_max cross-checked
      against manual for VIPER=14/SKELETON=7). **Armor / defense /
      attack_power still [?]** — the HANDOFF-2026-07-18.md Ghidra-derived
      offset guesses for these were tested against the live capture and
      disproved (no offset in the stride matches `combat_rating` above
      3/72). `ToCombatant` fill still open. (Wrong, s22: the guesses were right; the
      check had parsed packed bytes. See `RE-REFERENCE.md` do-not-redo list.)
- [ ] Optional: DOSBox per-swing sampling fixtures (Phase 3 left this optional;
      do it if unit tests alone leave drift)

### P0.6 — Magic polish
Spec: `magic-spells.md`

- [ ] **HEALING** RNG range verified vs DOS
- [ ] Remaining **special** spell effects (summon, buffs, undead detection, …)
- [ ] Refresh `spells.json` notes so no `[?]` remains for shipped spells

### P0.7 — Map tile graphics (presentation parity)
Spec: `amiga/FORMAT.md` (tiles section); DOS CGA tiles also acceptable

- [ ] Locate overworld + dungeon **tile bitmaps** (Amiga `game` binary and/or
      DOS `*.IBM` / PAT) and extract to PNG
- [ ] Extend `asset-manifest.json` so every map cell class maps to a tile asset
- [ ] Drop Client’s colored-rectangle placeholder once tiles exist

---

## P1 — Core parity implementation

Translate every newly confirmed P0 rule into `OpenPhantasie.Core` /
`OpenPhantasie.Data` with headless tests. No invented numbers.

### P1.1 — Data & codecs
- [ ] Update GUILD / SACK / DNG / DNGX codecs to match P0 field maps
- [ ] Round-trip tests against real DOS fixtures (`extracted/samples/`, new
      captures under `working/snapshots/`)
- [ ] Clear `[?]` stubs in `CharacterSchemaNotes` / town / spell JSON as each
      item lands

### P1.2 — Systems
- [ ] TownMenus: real discount / train / mystic / aging / learn fees *(no temple — none in DOS, session 9)*
- [ ] CombatGear + Combat: DOS-accurate equip fill, initiative, swing counts
- [ ] Magic / SpellCaster: HEALING + specials
- [ ] DungeonTraversal: real persistence + events
- [ ] Overworld: encounter/time coupling

### P1.3 — Parity verification
- [ ] Expand `PlaythroughParityTests` (and add focused suites) so every former
      `[?]` has at least one golden assertion
- [ ] Side-by-side checklist vs DOSBox for: create → guild → town buy/sell →
      wilderness fight → inn distribute → dungeon clear → resurrect (spell) → train →
      mystic → save/load → endgame path as far as practical
- [ ] Document any remaining intentional deviations (Q1/Q2 only, unless new
      authentic-mode toggles)

### P1.4 — Save-file compatibility *(scope agreed with the user s38, 2026-10-11)*
How remaster saves and original DOS saves relate. Format facts: `reverse-engineering/dos/logic-specs/save-file-format.md`.

**Decisions (user, s38):**
1. **Both directions.** Import a DOS save folder into the remaster; export a remaster game as DOS files
   that the original game runs.
2. **All five files:** `GUILD.DAT`, `SACK.DAT`, `TWNS.DAT`, `DNG.SAV` + `DNGX.SAV`.
3. **Import loads as-is, then warns.** Nothing in a DOS save is clamped, moved or rejected for breaking a
   manual-based rule (e.g. a 22nd character, D-002: all 22 stay; creation is blocked until under 21). The
   player gets a list of every field/state the remaster would not have produced.
4. **Byte-exact where possible.** Import → export with no game changes gives identical files. Unknown and
   stale bytes (`@94..@214`, the DNGX tail, unmapped `TWNS.DAT` bytes) are carried through unchanged; known
   fields are written over the carried bytes. Pinned by fixture tests.
5. **Sack > 60 items blocks export** with a message (remaster holds 90, D-018; DOS 60). Nothing dropped silently.
6. **Export is a town action only**, like DOS Option→Save game (manual: "S)AVE GAME is always accomplished in a
   town"): the carried XP/gold pool is split into shares first (town-entry split, D-037), so the result matches a DOS
   save. **User s40:** the manual is silent on the split, so it is **automatic** — `DosSaveConverter.AutoSplit`: fight
   XP/gold paid out as on town entry, carried gold into the living members' banks in equal shares (remainder to the
   first), on copies (the live game keeps its purse). Blocks only when no member is alive.
7. **Out-of-range values on export are clamped and listed** in the same warning list (e.g. D-003 elf age
   clock > 65,535 in u16 `@264`; bank gold > 65,535 in u16 `@262`).
8. ~~The remaster's JSON save always loads old versions.~~ **Reversed (user s40):** "do not spend time trying to
   preserve old saves of any sort. Just make a new save if things change enough to break one." No migration chain,
   no per-version fixtures; a save that no longer loads is refused (the existing v1 mid-combat refusal stands).
   Existing migration code stays but nothing new is added. DOS import/export (decisions 1–7) is unaffected.
9. **Entry point:** `OpenPhantasie.Data` API + tests first, then a Client menu entry (title / town Utility) that
   takes a folder path. Export never overwrites a folder without confirmation.

**Work items** (open checkboxes in `PLAN.md` §3; closed ones in `docs/plan/BOARD-ARCHIVE.md`):
- Raw-record carry: keep each imported 282-B GUILD record (and the other files' unmapped bytes) through the
  session and its JSON save; `GuildCodec.WriteRecord` overlays known fields instead of `r.Clear()`. Same for
  `@252` lifespan: write the imported DOS value back unless the D-003 band changed it.
- `SackCodec` (60 × u16 ids ↔ `LootSack.ItemIds`; XP/GP not in the file — 0 on import).
- `TownsCodec` (12 × 291 B ↔ `TownState` stock 0..180 + price index `0x10f` + name; rest carried raw).
- `DosSaveSet` import/export API over all five files + DNG/DNGX codecs (s36) + warnings list
  (rule breaks on import, clamps on export, blockers: not in town, sack > 60).
- Fixture tests: every `extracted/samples/` set imports and re-exports byte-identical.
- ~~JSON migration chain + per-version fixtures~~ — dropped (decision 8, user s40).
- Client menu entry + overwrite confirmation (P2 side).

---

## P2 — Client presentation & mode completeness

Parity of *rules* is P1; this package is “the PC game is playable end-to-end
with correct visuals and full menus.”

- [ ] Render overworld / dungeon with extracted tiles (P0.7)
- [ ] Town / guild / temple / bank / mystic / inn / armory UIs expose every
      DOS option the Core now supports (no silent stubs)
- [ ] Combat UI shows party/monster state consistent with DOS flow (actions,
      targets, flee, spells, potions)
- [ ] Character sheets show decoded counters (potions, runes, ring, score,
      skills) once P0.1 lands
- [ ] Keep Phase 7 QoL (save-anywhere, speed, auto-map, scale, bestiary,
      remaps) — they stay; they must not alter Core formulas

---

## P3 — QoL upgrades (post-parity defaults)

Do these **after** P1 formulas are locked, so QoL cannot paper over wrong
math. Each item is additive; default behavior remains DOS-like where it
matters.

### P3.1 — Proper per-character inventory (required)

**Problem today:** worn slots auto-update on buy (`AutoEquipIfBetter`); the
**V** overlay is read-only compare. There is no first-class
equip / unequip / give / drop. Players still think in “distribute loot then
hope auto-equip,” which is worse than the original’s already-awkward gear
flow.

**Target UX:**
- Per-character inventory panel (not party-blob only)
- Actions: **Equip**, **Unequip**, **Use** (potions/scrolls where legal),
  **Give** (to another living member), **Drop** / sell still via town
- Weapon / armor / shield slots explicit; unequip returns item to that
  character’s carried list
- Respect DOS usability gates (STR etc. — `ItemRules.IsUsable`); show why an
  equip failed
- Does **not** require Inn distribute or party-wide re-equip
- Optional: drag-drop once sprite inventory art exists (Phase 7 deferred this)

**Core work (allowed):**
- [ ] `InventoryActions` (or similar) on `Character` / `Party`: Equip,
      Unequip, Transfer — update worn ids + `CombatGear.ApplyWornItemPoints`
      using the **P0.5-accurate** fill path
- [ ] Unit tests: equip worse/better gear, unequip mid-dungeon, transfer,
      cannot equip unusable, selling worn clears slot
- [ ] `GameSession` wrappers for Client

**Client work:**
- [ ] Replace read-only **V** overlay with interactive inventory
- [ ] Keyboard + gamepad: select character → select item → action menu
- [ ] Compare pane kept (points vs currently worn)

### P3.2 — Other QoL (nice-to-have in this phase)

Carry-forward from Phase 7 “larger scope” plus inventory-adjacent polish:

- [ ] Authentic-mode toggle for Q1/Q2 (optional 16-bit gold truncate / discard
      remainder) — default = remaster FIX
- [ ] Difficulty modifiers (Core hook; off by default)
- [ ] Colorblind-friendly palette pass
- [ ] Inventory sort/filter + drag-drop (once art allows)
- [ ] Party import/export I↔II/III — **defer** until sequel data exists

Out of scope unless reopened: audio remaster, multiplayer.

---

## Stale checklist cleanup (docs only)

While closing parity, update plan checkboxes that already match reality:

- [x] `01-legal-and-sourcing.md` — originals present; DOSBox + ADF verified
- [x] `02-toolchain-setup.md` — Ghidra snap + DOSBox smoke + prior tool checks
- [x] `05-engine-architecture-csharp.md` — solution/schema done-criteria marked

---

## Done-criteria for Phase 8

- [ ] Every former Phase 3–6 `[?]` / `[~]` that affects gameplay is either
      **[C]** in logic-specs and implemented, or explicitly listed as
      non-gameplay / N/A with rationale
- [ ] GUILD / SACK / DNG / DNGX round-trip against real DOS fixtures
- [ ] Town, combat, magic, dungeon, overworld behaviors match DOS under
      side-by-side + automated golden tests
- [ ] Map tiles extracted and used in Client (no rectangle placeholders)
- [ ] **Interactive per-character inventory** ships (equip / unequip / give /
      use) without requiring loot redistribute or party-wide re-equip
- [ ] Phase 7 QoL remains; Core combat/movement formulas unchanged except
      where P0 proved DOS differed from the previous stub
- [ ] `dotnet test` green; playthrough can reach late-game content without
      hitting stubbed menus

## Suggested session order

1. P0.1 + P0.2 (saves + towns) — highest player-facing drift  
2. P0.5 + P0.6 (combat + magic)  
3. P0.4 + P0.3 (dungeon + overworld)  
4. P1 implementation + parity tests for the above  
5. P0.7 tiles → P2 Client  
6. **P3.1 inventory QoL** (after equip fill is DOS-accurate)  
7. P3.2 optional QoL + doc checkbox cleanup  
