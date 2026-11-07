# 00 — Static-Analysis Function/Subsystem Map

Result of the Phase 3 §3.1 static pass. The Ghidra project lives in
`../ghidra/PhantasieDOS.rep`; raw exports are `../ghidra/*.functions.txt`,
`*.strings.txt`, `*.decompiled.c`.

Imported as **x86:LE:16:Real Mode**. Auto-analysis found:

| Program | Functions | Defined strings | Role |
|---|---|---|---|
| `PHANTASI.COM` | 214 | 88 | Kernel + master data tables + char sheet |
| `OV1.OVR` | 88 | 117 | Town / guild / character management |
| `OV2.OVR` | 88 | ~90 | Combat + dungeon |

> Caveat: this is a flat real-mode binary with a home-grown overlay loader and
> segment arithmetic; Ghidra's segment model is imperfect (many
> "flow into non-existing memory" warnings where overlay calls jump between
> modules). Function boundaries are a solid guide, not gospel.

## String anchors → subsystem (byte offsets into each file)

Strings are the fastest way to jump to the code that owns each subsystem
(data xrefs from a string land in the routine that prints it).

### `PHANTASI.COM` — data tables (offsets are file offsets)

> **Raw-file offsets are only reliable inside literal runs.** The COM file is EXEPACK-packed (s22), so the
> canonical addresses are DS offsets in the unpacked image (`data-tables.md` address table, H5 s24).

| Offset | Contents |
|---|---|
| `0x5ff3` | Character-creation menu: RACE / CLASS lists |
| `0x6427`–`0x6592` | **Item value factors** `V[]` (u16 × 182; Armory buy/sell) |
| `0x6593`–`0x66fe` | **Item name pointers** (u16 × 181 → strings below) |
| `0x66ff`–`0x6a78` | **Item names** (101 equipment) then potions/scrolls/treasure strings |
| `0x6a7a`–`0x6adc` | Potion item names HEALING 1…10 (item ids 101–110; not spell labels, H5 s24) |
| `0x6d4f` | **Class** list (THIEF, MONK, RANGER, FIGHTER, PRIEST, WIZARD) |
| `0x6d97` | **Race** list (15: HUMAN…MINOTAUR) |
| `0x6e8a`–`0x711a` | **Spell** list (55: HEALING I…DIVINE I) |
| `0x7131`–`0x815f` | Packed bytes of the monster table, **not** usable raw; real table = unpacked DS:`0x1a6c` (s22) |
| `0x8372`–`0x83e0` | Skill labels: attack, parry, spot trap, disarm trap, pick lock, find item, swim, listen |
| `0x8479`–`0x8528` | **Character-sheet format strings** (reveal the char struct field order) |
| `0x842b` | "[original text]" (quest artifact) |
| `0x8457`–`0x8475` | Element names: AIR, EARTH, FIRE, WATER, GOD |
| `0x8553` | "Error %d loading overlay: %s$" (overlay loader) |

### `OV1.OVR` — town / guild (segment:offset as Ghidra reports)

| Addr | Anchor | Subsystem |
|---|---|---|
| `0x5bdf` | "guild.dat" | Guild roster load/save |
| `0x5bf4`,`0x5c08` | "[original text]", "INPUT NAME:" | New member creation |
| `0x5c22`,`0x5c62` | "[original text]", "WHO WILL LEARN?" | Training / spell learning |
| `0x5ca2`,`0x5cb1` | "WHO WILL SHOP?", "Armory is empty" | Armory |
| `0x5dd8`,`0x5de8` | "GOLD CARRIED %u", "[original text]" | Bank |
| `0x5cfa`,`0x5d33` | "[original text]/PURGE?" | Guild roster edit |
| `0x5b23`–`0x5bb2` | UNDEAD / DESTROYED / ASTRAL PLANE / GREAT DEMON | Death + endgame |

### `OV2.OVR` — combat / dungeon

| Addr | Anchor | Subsystem |
|---|---|---|
| `0x7626`–`0x76c5` | "%s HITS/MISSES", "%s TAKES %d DAMAGE", "%s DIES" | Melee resolution |
| `0x75dc`–`0x771b` | CHARMED / DISSOLVING / SLEPT / ARMOR REDUCED / SLOWED | Combat spell effects |
| `0x7925`–`0x7983` | [original text] / GREET / RUN / TAKE YOUR GOLD | Monster reaction table |
| `0x7990`,`0x79b6` | "RANK %1d : %1d %s(S)", "%ld XP AND %ld GP" | Encounter setup + rewards |
| `0x7736`–`0x7827` | [original text] / [original text] / SMOKE FILLS | Dungeon movement + events |
| `0x783d`–`0x7908` | PICKS THE LOCK / SPOTS A TRAP / AVOIDS THE TRAP | Locks + traps — strings only, no difficulty table (s39): code `0x2e03` (locks), `0x2f8e` (traps), `dungeon-representation.md` |
| `0x7f5f`–`0x7f82` | SWIMMING: / SWIMS / SINKS AND TAKES %-3d DAMAGE | Swimming, state 10 `0x685c` (`overworld-movement.md`) |
| `0x20e5`, `0x1f9f` | (no strings) | Dungeon step reveal + L)OOK, from Options `0x5d14` (s67, `dungeon-representation.md` §Sight) |

## Overlay / memory-manager architecture [X]

The game predates the authors realising a program could manage >64 KiB itself,
so it ships a bespoke loader:

1. `R32768.COM` allocates 32 KiB, stores its segment in IVT slot 0x61 (int 61h),
   and leaves signatures in slots 0x60/0x62/0x63.
2. `M1.COM`, `M2.COM` each allocate more and record their segments similarly.
3. `PHANTASI.COM` reads those IVT slots to find its scratch/overlay RAM, then
   pulls `OV1.OVR` / `OV2.OVR` and the `*.PAT` graphics in as overlays via its
   own loader (`Error %d loading overlay: %s$`).

For the remaster this whole mechanism is irrelevant — it is a DOS memory hack.
Core just needs the *logic* inside the overlays, not the loader.

## Subsystem → spec doc

Character creation → `character-creation.md`; guild/save → `save-file-format.md`;
overworld → `overworld-movement.md`; towns → `town-menus.md`; dungeon →
`dungeon-representation.md`; combat → `combat-resolution.md`; magic →
`magic-spells.md`; data tables → `data-tables.md`.
