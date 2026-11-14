# Phase 5 — Engine Architecture (C# / .NET)

> **Closed (historical).** Phase 5 is done; the code in `src/` is the current architecture. Live state: `PLAN.md`.

**Goal:** repo skeleton with the project split, solution file, and data
schemas in place, ready for Phase 6 to fill in.

## Project split
```
src/
├── OpenPhantasie.Core/       # Pure game logic. Zero rendering/audio/input deps.
├── OpenPhantasie.Data/       # Data schemas + JSON loaders. Depends only on Core's models.
└── OpenPhantasie.Client/     # MonoGame front end. Depends on Core + Data.
```

A renderer-agnostic Core can be unit-tested against the logic-specs without a window, and makes II/III/IV a
`OpenPhantasie.Data` + delta-rules problem. The engine is custom: MonoGame is only the windowing / input / blit / audio
layer under `OpenPhantasie.Client` (no off-the-shelf scene graph or ECS).

## `OpenPhantasie.Core` — key types (starting point, will grow with Phase 3 findings)
```csharp
namespace OpenPhantasie.Core;

public record CharacterStats(int Str, int Int, int Wis, int Dex, int Con, int Cha);
public class Character { /* fields per Phase 3's save-format spec */ }
public class Party { public List<Character> Members; }

public interface ICombatResolver
{
    CombatResult ResolveRound(Party party, List<Monster> monsters, IRandomSource rng);
}

// IRandomSource wraps RNG so combat is deterministic/testable — swap a seeded
// implementation in unit tests, a real one at runtime.
public interface IRandomSource { int Next(int minInclusive, int maxExclusive); }
```

Each logic-spec maps to one or more classes here, with unit tests asserting its documented formulas.

## `OpenPhantasie.Data` — schema-first, data-driven
```csharp
namespace OpenPhantasie.Data;

public record MonsterDefinition(int Id, string Name, CharacterStats Stats,
    int HitDice, string SpritePath, /* ... per Phase 3 extraction ... */);

public class GameDataRepository
{
    public IReadOnlyList<MonsterDefinition> Monsters { get; }
    public IReadOnlyList<ItemDefinition> Items { get; }
    public IReadOnlyList<SpellDefinition> Spells { get; }
    // Loaded from the JSON produced in Phase 3 + asset-manifest.json from Phase 4
}
```

JSON lives under `src/OpenPhantasie.Data/GameData/phantasie1/`; each later game is a sibling folder (`phantasie2/` …).

## `OpenPhantasie.Client` — MonoGame shell
Thin: input, sprite batching, screen/state management (title → overworld → town → dungeon → combat); all game
logic is in `OpenPhantasie.Core`. Phase 7 QoL (auto-map, save anywhere, speed toggles) mostly lives here.

## Solution setup
```bash
dotnet new sln -n Phantasie
dotnet new classlib -n OpenPhantasie.Core -o src/OpenPhantasie.Core
dotnet new classlib -n OpenPhantasie.Data -o src/OpenPhantasie.Data
dotnet new mgdesktopgl -n OpenPhantasie.Client -o src/OpenPhantasie.Client
dotnet new xunit -n OpenPhantasie.Core.Tests -o tests/OpenPhantasie.Core.Tests

dotnet sln add src/OpenPhantasie.Core src/OpenPhantasie.Data src/OpenPhantasie.Client tests/OpenPhantasie.Core.Tests
dotnet add src/OpenPhantasie.Data reference src/OpenPhantasie.Core
dotnet add src/OpenPhantasie.Client reference src/OpenPhantasie.Core src/OpenPhantasie.Data
dotnet add tests/OpenPhantasie.Core.Tests reference src/OpenPhantasie.Core
```

## Phase 3 open fields to model (carry-forward)

Open / descoped at the end of Phase 3 (schemas left `[?]` / TODO comments, no invented values). Later state per row:
`06-implementation-roadmap.md`'s carry-forward table and the specs named.

| Item | Where it bites | Spec |
|---|---|---|
| `DNGX.SAV` / `DNG.SAV` field-level maps | Party position, dungeon persistence flags | `save-file-format.md`, `dungeon-representation.md` |
| Non-zero `SACK.DAT` layout (fixture still all-zero) | Post-combat loot pool → Inn distribute | `save-file-format.md`, `combat-resolution.md` |
| `spells_known` bit packing | Character record mid-block | `save-file-format.md`, `magic-spells.md` |
| `@28` scroll_count vs weapon_hit dual-use (retracted s19: `@28` = damage A) | Sheet vs combat prep | `save-file-format.md`, `combat-resolution.md` |
| Experience width >65535 (s16 vs s32) | Long campaigns / importer | `save-file-format.md` |
| Per-spell MP costs | `SpellDefinition.MagicPointCost` | **[C]** `(index%4)+1` — see `magic-spells.md` |
| Non-HEALING effect coefficients / HEALING RNG | `SpellDefinition` notes | **[~]/[?]** — `magic-spells.md` |
| Dungeon wall/feature byte legend + W×H | `Grid<Cell>` feature ids | grid ≈1740 B **[I]**; legend **[?]** (later **[C]**: 33×38 = first 1254 B, legend in `dungeon-representation.md`) |

Confirmed Phase 3 baselines to lean on: `GUILD` 282×22 (incl. skills + `@70`
score + worn `@82/@84/@86`), `TWNS` stock/prices, combat to-hit/damage, potion
HP/MP restore formulas.

## Done-criteria for Phase 5
- [x] Solution builds with all four projects wired up per the reference graph
      above (`OpenPhantasie.sln` + Core/Data/Client/Tests)
- [x] `OpenPhantasie.Core` has no package/project reference to MonoGame
- [x] At least one JSON schema (start with monsters) round-trips: Phase 3/4
      output → `OpenPhantasie.Data` load → usable in a `OpenPhantasie.Core.Tests` test
      (`MonsterDataRoundTripTests`, `GameDataLoadTests`, etc.)
- [x] Empty MonoGame window boots from `OpenPhantasie.Client` referencing the
      other two projects successfully (M0+ playable Client shell)
- [x] Character / spell / dungeon schemas note the Phase 3 carry-forward gaps
      above — see `CharacterSchemaNotes.cs` (stubs/TODOs; no invented layouts)
