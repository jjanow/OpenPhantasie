# Phase 7 — QOL Backlog (post-parity)

> **Closed (historical).** Its three large open items moved to Phase 8 P3.2. Open QoL items and live state: `PLAN.md`.

**Goal:** after M8 parity. Items touch `OpenPhantasie.Client` (sometimes optional Core hooks), never verified game logic.

## High value, low risk (do first)
- [x] **Save anywhere** — original likely restricts saving to town/inn; decouple
  from that restriction in Client, serialize full state at any point.
  - `GameSession.CaptureSnapshot` / `ApplySnapshot` + `SessionCodec` JSON;
    Client **F5** / **F9** (no mode gate). Mid-combat included.
- [x] **Combat speed-up / skip animations** — toggle or hold-to-fast-forward.
  - **F** cycles Instant / Normal / Fast; hold **Shift** (or LT) to skip delays.
- [x] **Auto-map** — track visited dungeon tiles, render an overlay; doesn't
  touch dungeon logic at all, purely additive in Client.
  - Fog-of-war main view + mini-map (**Tab** toggles); persisted in `qol.json`. III's tile dungeons (s171): the
    mini-map is a whole-level overview inside the map view, in the corner away from the party, covering no card.
- [x] **Modern resolution/UI scaling** — render the extracted Amiga assets at
  integer scale factors with a resizable window, rather than being locked to
  original resolution.
  - 320×200 virtual canvas, integer scale (**F11**), resizable letterboxed window.

## Medium
- [x] **Bestiary/compendium** — log every monster encountered with stats, backed
  by the same `MonsterDefinition` data already loaded in Phase 5.
  - **B** overlay; unlocks on encounter; stats from loaded tables.
- [x] **Keybinding remap + optional controller support.**
  - `ClientSettings.KeyBindings` JSON; D-pad / face buttons / shoulders mapped.
- [x] **Inventory UI improvements** — sort/filter, drag-drop, compare-on-hover —
  cosmetic layer over existing `Party`/`Character` state, no Core changes.
  - **V** overlay: name-sorted list + Enter compare (points vs worn). Drag-drop
    deferred until sprite inventory art exists.
  - 09 T2 / P3 (s74–s79): the paperdoll + stash screen (`GameSettings.InventoryScreen`) has keyboard, click and drag
    moves; the stash filters by slot and sorts found / name / kind / value (Armory price); dragging over a member tab
    shows that member, so an item can be dropped on their paperdoll boxes.
- [x] **Tooltips** — surface the formulas from Phase 3's logic-specs directly in
  UI (e.g. showing computed to-hit% before committing to an attack).
  - Combat footer uses `CombatMath.PartyMeleeHitChance` / damage preview.

## Larger scope (evaluate later, may warrant their own Core hooks)
- **Difficulty modifiers: dropped (s80).** No Phantasie manual (I/II/III) and no DOS code has a difficulty setting
  (no table, no menu), and the user doesn't want features the originals lack added on spec.
- [x] **Colorblind-friendly palette option** — alternate palette mapping for the
  extracted Amiga assets.
  - Built in P3 (s79) without an asset pass: `GameSettings.ColorblindPalette` (per game, off = original) remaps the
    finished frame by exact colour (`GameData/shared/colorblind.json`, red-green, Okabe-Ito targets) at present time.
- [ ] **Party import/export between I and II/III** — moved to `09-trilogy.md` T5 (built; leftovers in `PLAN.md`).

## Explicitly out of scope unless you decide otherwise later
- Full audio remaster / original chiptune reuse (out of scope here; the series later got the originals' title tunes
  and sound effects, see `PLAN.md`). Amiga samples are `8SVX` IFF, so Phase 4's pipeline would apply.
- Multiplayer/networked play — original is single-player; would be a
  substantial Core-level addition, not a QOL item.

## Done-criteria
- [x] High-value items shipped in Client (save anywhere, combat speed, auto-map,
      integer scaling)
- [x] Medium items shipped (bestiary, remappable input + gamepad, inventory
      overlay, combat formula tooltips)
- [x] Core combat/movement formulas unchanged; session snapshot is additive
- [x] `SessionCodecTests` + existing M8 parity suite green
