# Phase 3 — DOS Disassembly & Logic Extraction

> **Closed (historical).** Phase 3 is done; I's specs live in `reverse-engineering/dos/logic-specs/` (index `README.md`).
> Open RE items and live state: `PLAN.md`. II–IV: `03b-phantasie2-re.md`, `03c-phantasie3-re.md`, `03d-phantasie4-re.md`.

**Goal:** not C# code. A set of plain-English + pseudocode documents in
`reverse-engineering/dos/logic-specs/` describing exactly how each game
subsystem behaves, precise enough that Phase 6 implementation is a translation
exercise rather than a guessing exercise.

## How to resume (new chat)

```bash
# Launch working copy (never mutates original-files/)
cd reverse-engineering/dos/working && ./run.sh
```

- Specs index: `reverse-engineering/dos/logic-specs/README.md`
- Extracted JSON: `reverse-engineering/dos/extracted/` (`items.json`,
  `monsters.json`, `scrolls.json`, `messages.json`, maps, guild)
- Fixtures: `extracted/samples/GUILD.{3chars,pass2,pass3}.dat`,
  `DNG*.pass2.sav`, `TWNS.pass2.dat`; latest live saves in
  `working/game/` and `working/snapshots/after-pass3/`
- Ghidra project: `reverse-engineering/dos/ghidra/`

**Important gameplay constraint (pass 3):** the game only writes saves in
**towns** — you cannot save in the wilderness/dungeon. Return to a city before
exiting if you need `GUILD.DAT` / `TWNS.DAT` flushed.

---

## Done-criteria for Phase 3
- [x] Logic-spec doc exists for each subsystem listed in 3.1
- [x] Save file format documented for Phase 6 (core `GUILD`/`TWNS` field-by-field; dungeon/sack leftovers descoped — see below)
- [x] Data tables (monsters/items/spells) extracted to JSON with scripts
- [x] Behavioral quirks/bugs noted with preserve/fix flags
- [x] `SCROLLS.DTX` + `MESS1..10` ciphers cracked
- [x] Combat to-hit + damage formulas documented from static analysis

### Save format — current status
| Area | Status |
|---|---|
| `GUILD.DAT` 282×22 core (attrs, level, gold, skills, name) | **[C]** |
| HP fields | **[C]** `@20=hp_max`, `@22=hp_cur` (corrected pass 3 vs sheet) |
| `experience` | **[C]** `@266` (=30 for whole party after one win) |
| Inventory / soak / weapon runtime fields | **[C]** worn slots `@82` shield / `@84` armor / `@86` weapon (sheet-matched); carried list separate |
| `TWNS.DAT` | **[C]** 12×291; `stock[item_id]`; price_index `@0x10f` |
| Armory buy prices | **[C]** `V²×town_idx/500+1` — Pelnor screenshot: DAGGER 11, MACE 37, MAGIC 1 44, SCROLL 8 121 (exact match) |
| `SACK.DAT` | **[~]** structure known (120 B loot pool → Inn distribute); zero in Phase 3 fixtures (first non-zero: `SACK.s35.dat`, s35) |
| `score` | **[C]** `@70` — mystic “score is 2” → all party members `@70=2` |
| skill order 34/36/40 | **[C]** `find_item` / `spot_trap` / `listen` (thiefo sheet) |
| `DNGX.SAV` / `DNG.SAV` field-level maps | **descoped** → Phase 5–6 (structure notes in `save-file-format.md` / `dungeon-representation.md` are enough) |
| `spells_known` bit packing, `@28` dual-use, XP>65535 width | **descoped** → Phase 5–6 (later: `spells_known` `[C]` `@0x60`; `@28` = damage A, no dual-use, s19) |

### Combat — current status ([C] in `combat-resolution.md`)
```
chance = (atk_CV - def_CV) * 5 + 50
roll   = FUN_0000_655b()            # 0..99
hit    if roll < chance             # (some paths use <=)

coef   = isqrt(roll)                 # 0..9
# monster→party: (coef * mon_dmg) / 15 - armor_soak
# party→monster: includes weapon_dmg, action penalty (action*100)/67
```
Phase 3 notes (since corrected): party CV ≈ `@14` (no CV field exists; `@14` = MP ceiling, defense = `@12`, s11 / s19);
monster CV = u16 after a `B2 00` marker (that was packed COM data; real table = unpacked DS:`0x1a6c`, s22). Current
rules: `combat-resolution.md`.

### Data extracts present
| File | Script |
|---|---|
| `names.json` | `extract_names.py` |
| `items.json` (181 ids, V/points/prices) | `extract_items.py` |
| `monsters.json` (CV + attack blocks) | decode pass |
| `scrolls.json` / `messages.json` | `decrypt_scrolls.py` / `decrypt_messages.py` |
| maps | `extract_maps.py` |

---

## Pass 3 dynamic observations (this session)
- New chars in roster: `fighto`, `crusho` (DWARF FIGHTER), `gobbo`; party gold mirrored at **207**.
- **crusho** sheet: LEVEL 1 FIGHTER DWARF; STR10 INT13 DEX3 CON12 CHA14 LUCK10; AGE40; HP **7/8**; MAGIC5/5; EX.PTS **30**; SCORE0; worn **CLUB PTS4 / ROBES PTS2 / SM SHIELD PTS4**.
- Armory (Pelnor) list prices matched formula; `UNUSABLE` shown for MACE on wizardo (STR gate).
- One fight won; `SACK.DAT` remained empty; XP@266=30 on all party members.

## Pass 4 dynamic observations
- **thiefo skills [C]**: attack38, parry40, spot trap17, disarm45, pick40, find item5, swim18, listen2
  → storage `@34=find_item`, `@36=spot_trap`, `@40=listen` (not label-column order).
- **Mystic score [C]**: “Your score is **2**. [original text].” → `GUILD` `@70` `0→2` on all six party members. Also `@266` XP `30→40`, age seeds +1; some HP restored (likely Inn).
- Fixture: `extracted/samples/GUILD.pass4.dat`; snapshots `working/snapshots/after-pass4/`.
- `SACK.DAT` still zero (optional / skip).

## Descoped / OK to defer into Phase 5–6
- Full DOSBox breakpoint verification of every swing (formulas already static-[C]).
- Monster two-form HP trailer polish (encounter HP is rolled at fight start).
- Armory price settled in Phase 8 P0.2: no CHA/other discount; the full 177-GP
  shared-purse charge settled as 59 GP from each of three persisted carried-gold shares.
- `DNGX.SAV` / `DNG.SAV` bit→feature field maps (enough structure for remaster; polish with dungeon UI).
- `spells_known` packing, `@28` scroll_count vs weapon_hit dual-use (retracted s19), XP width >65535.
- Per-spell MP costs are now **[C]** (`(index%4)+1` in OV2); remaining: non-HEALING
  effect coefficients + HEALING RNG range (potion restores are [C]).
- Non-zero `SACK.DAT` capture (pool semantics already known from Inn/combat strings).

## 3.1–3.4 checklist (original plan text)

### 3.1 — Static analysis pass
Import into Ghidra (x86 real-mode), auto-analyze, label via strings, build
subsystem function map. **Done** — see `logic-specs/00-function-map.md`.

### 3.2 — Dynamic verification
DOSBox/DOSBox-X breakpoints vs static claims. **Partial** — guild/inventory/
XP/prices verified by save diffs + screenshots; combat formulas static-[C];
live swing sampling still optional.

### 3.3 — Logic-spec documents
One markdown per subsystem with inputs / algorithm / outputs / quirks. **Done.**

### 3.4 — Data table extraction
Python scripts → JSON seed for `OpenPhantasie.Data`. **Done** for names, items,
monsters (core fields), scrolls, messages, maps.
