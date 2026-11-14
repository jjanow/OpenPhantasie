# Phase 6 — Implementation Roadmap (M0–M8)

> **Closed (historical).** M0–M8 are done. Live state: `PLAN.md`; the series plan that replaced the II/III section
> below: `09-trilogy.md`.

**Goal:** a parity-focused build order; each milestone was one session, loading only this file and its spec(s).

| # | Milestone | Depends on | Core work |
|---|-----------|-----------|-----------|
| M0 | Repo scaffold | Phase 5 | Solution builds, empty window renders, CI (optional) runs tests |
| M1 | Data model + loading | `monsters.json` etc. from Phase 3/4 | `OpenPhantasie.Data` fully loads and validates all extracted tables |
| M2 | Character creation | `character-creation.md` spec | Stat rolling, class/race rules, save format read/write |
| M3 | Overworld traversal | `overworld-movement.md` spec | Map representation, movement, encounter triggers |
| M4 | Town menus | `town-menus.md` spec | Shop/temple/bank/inn interactions |
| M5 | Dungeon crawling | `dungeon-representation.md` spec | Tile-based dungeon movement, scripted events, rendering with Phase 4 assets |
| M6 | Combat | `combat-resolution.md` spec | Turn order, to-hit/damage formulas, unit-tested against documented formulas |
| M7 | Magic/spells | `magic-spells.md` spec | Spell casting, effects, resource costs |
| M8 | Full playthrough parity pass | M1–M7 | `GameSession` mode machine + Client keyboard loop; automated playthrough test; remaining `[?]` either resolved or explicitly descoped |

## Phase 3 carry-forward by milestone

Items left open at the end of Phase 3, as they stood when M8 closed (current state: `logic-specs/README.md`):

| Milestone | Carry-forward |
|---|---|
| **M1** | Spell `mpCost` is **[C]** (`(index%4)+1`). HEALING heal formula **[~]**; combat spell coefficients **[C]** (M7). Potion restores **[C]**. Dungeon grid 33×38 **[C]**. |
| **M2** | Skills + `@70` score + worn `@82/@84/@86` + **spells_known @0x60** + creation formulas **[C]**. XP s32 @266 **[C remaster]**. `@28` dual-use preserved as one word **[C remaster]** (dual-use retracted s19: `@28` = damage A). potions/runes/ring still **[?]**. |
| **M4** | `SACK.DAT` semantics via `LootSack` + Inn distribute (Q2 FIX: remainder to first survivor). Binary layout still **[~]**. |
| **M5** | `DNG.SAV` bit→feature and `DNGX.SAV` field map still **[?]** (ClearedEventKeys stand-in). Geometry + feature legend **[C]**. |
| **M6** | Formulas static-**[C]**. `@28` → weapon_hit_eff; equip seeds when 0. |
| **M7** | MP costs **[C]**; tiered combat coefficients **[C]**; HEALING RNG **[~]**; specials **[~]**. |
| **M8** | Playthrough wired (`GameSession` + `PlaythroughParityTests`). Remaining `[?]` are intentional descopes (DNG*.SAV maps, train/temple curves, potion counters) — see logic-specs README. Armory buying is now confirmed full list price with no CHA discount. |

See also `03-dos-disassembly-and-logic-extraction.md` (“Descoped”) and `reverse-engineering/dos/logic-specs/README.md`.

## Notes on sequencing
- M1 first: everything reads from the data layer.
- M2 / M3 are independent; M4–M7 need both (party + map state).
- M6 (combat) carries the most drift risk: most unit tests, with observed DOSBox rolls as fixtures where available
  (per-swing DOSBox sampling was descoped in Phase 3; formulas are static-[C]).
- QoL (Phase 7) only after M8.

## Architecting for Phantasie II/III (per your scope choice)
> **Superseded as the working plan by `09-trilogy.md` (2026-10-25)**; the sourcing notes below still apply.

Nothing in M0–M8 is II/III-specific (the Core/Data split). **Source platforms differ per title:** II never got a DOS
or Amiga port (the 2023 digital re-release of II emulates the Apple II version), so Phases 3–4 differ per sequel:

**Phantasie III** — same pipeline as I, no changes needed:
1. Repeat Phases 3–4 against the Phantasie III DOS+Amiga images directly.

**Phantasie II** — different source platforms, decided per your Atari
ST-for-II call:
1. Phase 3 equivalent against the **Apple II or C64 executable** (6502): Ghidra's 6502 module (oberoisecurity fixes)
   or dcc6502 / py65 instead of the x86 workflow. (As built, II's rules came from the Atari ST 68000 code:
   `03b-phantasie2-re.md`.)
2. Phase 4 equivalent against the **Atari ST disk image**: bitplane-based like the Amiga but not IFF ILBM 1:1, so
   check the format before using `xdftool` / `ilbmtoppm`; Ghidra's 68000 module applies.
3. II gets its own doc (became `03b-phantasie2-re.md`); `03` / `04` stay the DOS / Amiga pipeline docs for I and III.

**Both sequels, once their logic-specs/assets exist:**
1. `phantasie2/` and `phantasie3/` beside `phantasie1/` under `OpenPhantasie.Data/GameData/`.
2. Diff each sequel's specs against I's; deltas become targeted rule overrides in Core, not duplicated systems.
3. The Client needs a game-select / character-import flow pointing at another data set.

## Done-criteria for Phase 6 (i.e. for the whole project's first playable release)
- [x] M0–M8 all complete
- [x] Side-by-side playthrough against the original DOS version shows no
      unintended behavioral drift (intended deviations from Phase 3.3's
      preserve/fix decisions are fine — undocumented drift is a bug)

M8 verification: `PlaythroughParityTests` drives guild → town → wilderness
combat → loot distribute → dungeon entry headless; Client keyboard loop
covers the same modes visually. Intended fixes from `quirks-and-bugs.md`:
Q1 (64-bit gold purse), Q2 (distribute remainder). Remaining `[?]` items are
listed as descoped in `logic-specs/README.md` and do not block first playable.
