# Phase 6 — Implementation Roadmap (M0–M8)

> **Closed (historical).** M0–M8 are done. Live state: `PLAN.md`; the series plan that replaced the II/III section
> below: `09-trilogy.md`.

**Goal:** working, parity-focused build order. Each milestone is a good unit
for one Claude Code session — reference only this file plus the specific
Phase 3 logic-spec doc(s) relevant to that milestone, not the whole plan.

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

Items deliberately left open at end of Phase 3. Resolve during the matching
milestone (or a short DOSBox/Ghidra side-pass) — do not assume they are done:

| Milestone | Carry-forward |
|---|---|
| **M1** | Spell `mpCost` is **[C]** (`(index%4)+1`). HEALING heal formula **[~]**; combat spell coefficients **[C]** (M7). Potion restores **[C]**. Dungeon grid 33×38 **[C]**. |
| **M2** | Skills + `@70` score + worn `@82/@84/@86` + **spells_known @0x60** + creation formulas **[C]**. XP s32 @266 **[C remaster]**. `@28` dual-use preserved as one word **[C remaster]**. potions/runes/ring still **[?]**. |
| **M4** | `SACK.DAT` semantics via `LootSack` + Inn distribute (Q2 FIX: remainder to first survivor). Binary layout still **[~]**. |
| **M5** | `DNG.SAV` bit→feature and `DNGX.SAV` field map still **[?]** (ClearedEventKeys stand-in). Geometry + feature legend **[C]**. |
| **M6** | Formulas static-**[C]**. `@28` → weapon_hit_eff; equip seeds when 0. |
| **M7** | MP costs **[C]**; tiered combat coefficients **[C]**; HEALING RNG **[~]**; specials **[~]**. |
| **M8** | Playthrough wired (`GameSession` + `PlaythroughParityTests`). Remaining `[?]` are intentional descopes (DNG*.SAV maps, train/temple curves, potion counters) — see logic-specs README. Armory buying is now confirmed full list price with no CHA discount. |

Canonical list also lives in `03-dos-disassembly-and-logic-extraction.md`
(“Descoped”) and `reverse-engineering/dos/logic-specs/README.md`.

## Notes on sequencing
- M1 first because everything downstream reads from it — get the data layer
  solid before writing any gameplay code against it.
- M2/M3 can happen in parallel if you want (they don't depend on each other),
  but M4–M7 all depend on M2/M3 existing (party + map state).
- M6 (combat) is usually the highest-risk milestone for behavioral drift —
  budget the most unit-test time there, and lean on Phase 3.2's dynamic
  verification data (actual observed rolls from DOSBox debugging) as test
  fixtures, not just the formula as documented. Full per-swing DOSBox
  sampling was descoped in Phase 3 — formulas are static-[C]; add fixtures
  if parity drift shows up.
- Don't start QOL work (Phase 7) until M8 is done. Building QOL on top of an
  unverified core means rework when parity bugs surface later.

## Architecting for Phantasie II/III (per your scope choice)
> **Superseded as the working plan by `09-trilogy.md` (2026-10-25)**; the sourcing notes below still apply.

Nothing in M0–M8 is II/III-specific — that's the point of the Core/Data split
from Phase 5. But **the source platforms differ per title**, confirmed via
release research: Phantasie II never got a DOS or Amiga port (the 2023
digital re-release of II emulates the Apple II version for exactly this
reason). So Phases 3–4 don't repeat identically for both sequels:

**Phantasie III** — same pipeline as I, no changes needed:
1. Repeat Phases 3–4 against the Phantasie III DOS+Amiga images directly.

**Phantasie II** — different source platforms, decided per your Atari
ST-for-II call:
1. Phase 3 equivalent runs against the **Apple II or C64 executable** (6502,
   not x86) instead of a DOS binary — this is the harder RE path we
   discussed and set aside for Phantasie I; it applies here instead. Ghidra's
   6502 module (with the oberoisecurity fixes) or a dedicated 6502
   disassembler (dcc6502/py65) replaces the x86 Ghidra workflow from
   `02-toolchain-setup.md`/`03-dos-disassembly-and-logic-extraction.md`.
2. Phase 4 equivalent runs against the **Atari ST disk image** instead of an
   ADF — different container/filesystem, different graphics conventions
   (still bitplane-based since ST video hardware is architecturally similar
   to Amiga's, but not the same IFF ILBM tooling 1:1 — verify format before
   assuming `xdftool`/`ilbmtoppm` apply as-is). 68000 disassembly (Ghidra's
   68000 module) still applies here as it would for Amiga, since the ST is
   also 68000-based.
3. When you get to II, this warrants its own numbered doc pair (something
   like `03b-`/`04b-` or a `phantasie2/` subfolder under `docs/plan/`) rather
   than overloading `03`/`04` as written — those two remain accurate
   reference docs for the DOS/Amiga pipeline used by I and III.

**Both sequels, once their logic-specs/assets exist:**
1. Add `phantasie2/` and `phantasie3/` alongside `phantasie1/` under
   `OpenPhantasie.Data/GameData/`.
2. Diff each sequel's logic-specs against I's — most subsystems (combat,
   movement) should be near-identical since these were the same engine year
   over year; document deltas as targeted rule overrides in Core rather than
   duplicating whole systems.
3. Client just needs a game-select/import-character flow to point at a
   different data set — this is the "affordance" the architecture buys you,
   not something to build now.

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
