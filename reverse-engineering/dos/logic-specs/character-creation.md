# Character Creation / Stat Generation

Owned by the **Guild** in `OV1.OVR` (anchors: "INPUT NAME:" `0x5c08`,
"[original text]" `0x5bf4`, race/class menus in `PHANTASI.COM` `0x5ff3`).
Creation body recovered at **`OV1.OVR` `0x0BAC`–`0x0FA2`** (Ghidra omitted it;
Capstone + fixture cross-check). Data enums: `data-tables.md`. Tables live in
`TWNS.INT` → `extracted` / `GameData/phantasie1/creation-tables.json`.

## Inputs
- Player choice of **race** then **class** (manual: race first, then class). [X]
- RNG returning a **0..32767** word (DOS-style); used as `floor(rand / N)`. [C]
- Per-class / per-race modifier tables in `TWNS.INT`. [C]

## Flow [X]
1. Guild → **New Member**. One character at a time.
2. Choose race (`HUMAN, DWARF, ELF, HALFLING, GNOME, RANDOM`, plus the
   monster-races via RANDOM), then class
   (`THIEF, MONK, RANGER, FIGHTER, PRIEST, WIZARD`).
3. Program rolls the six attributes (bounded per race — table below), rolls
   starting gold, and shows the sheet (D-075, user: manual — the full sheet + SPACE after the name; DOS goes straight
   back to the Guild menu). Name must be **unique** in the roster
   ("[original text]"); on accept it is written to `GUILD.DAT`.
4. New chars are stuck in the Guild until added to a party (cannot train or
   leave town first). [X]
5. Roster is **22 slots × 282 bytes** on disk, and DOS lets all 22 be filled (DOSBox; the 23rd
   New member silently does nothing). The manual says max **21** — the remaster follows the manual
   (`DIFFERENCES.md` D-002). See `save-file-format.md`.

## RANDOM race [C] (`OV1` `0x0C2D`)
```
race = floor(rand / 3277) + 5          # PIXIE..MINOTAUR
if that race already exists in roster:
    race = floor(rand / 6554)          # HUMAN..GNOME
```

## Attributes [C]

Six core attributes, each `u16`: **STR, INT, DEX, CON, CHA, LUCK**.

For each of STR/INT/DEX/CON/CHA (`OV1` `0x0CDD`–`0x0D50`):

```
Q = floor(rand16 / 1821)                 # 0..17
attr = min(18, Q + classMod - 7)
attr = attr + raceMod - 10
attr = max(3, attr)
```

`classMod` / `raceMod` from `TWNS.INT` `@0x924` / `@0x942` (5 bytes × class/race).
Race **ceiling** = `raceMod + 8` (matches the manual except **HALFLING DEX = 19**
in the binary; manual prints 20). [C binary / X manual]
**Remaster follows the manual** (user decision 2026-10-10): Halfling DEX modifier
is **12** in `creation-tables.json` (binary: 11), so the max is 20 — and every
Halfling DEX roll is 1 higher than in DOS. See `DIFFERENCES.md` D-001.

### Luck [C] (`OV1` `0x0E4B`)
```
LUCK = floor(rand16 / 5462) + 8          # 8..13
```
Not race- or class-modified.

### Maximum attribute value by race [X/C]

| Race | STR | INT | DEX | CON | CHA |
|---|---|---|---|---|---|
| HUMAN | 18 | 18 | 18 | 18 | 18 |
| DWARF | 20 | 17 | 17 | 19 | 17 |
| ELF | 17 | 19 | 19 | 17 | 18 |
| GNOME | 19 | 17 | 18 | 19 | 17 |
| HALFLING | 16 | 18 | **20**† | 18 | 17 |
| GNOLL | 20 | 13 | 17 | 21 | 11 |
| GOBLIN | 17 | 14 | 18 | 17 | 11 |
| KOBOLD | 17 | 15 | 19 | 19 | 13 |
| LIZ.MAN | 19 | 14 | 17 | 18 | 13 |
| MINOTAUR | 20 | 14 | 16 | 19 | 12 |
| OGRE | 21 | 14 | 16 | 19 | 13 |
| ORC | 19 | 16 | 17 | 18 | 14 |
| PIXIE | 16 | 18 | 21 | 16 | 17 |
| SPRITE | 16 | 18 | 22 | 16 | 17 |
| TROLL | 22 | 13 | 15 | 20 | 12 |

† Manual value, used by the remaster. DOS binary ceiling is 19 (`DIFFERENCES.md` D-001).

## Derived / starting state [C]

### MP ceiling + MAGIC
From `TWNS.INT` `@0x1c2` (class × 15 + level); level-1. This is the manual's
"Maximum Magic Points" table, stored in `@14` (`save-file-format.md` `@14`); MAGIC goes to `@16` max and
`@18` current.

| Class | MP ceiling | MAGIC = `(3×ceiling)>>2` |
|---|---|---|
| THIEF | 6 | 4 |
| MONK | 10 | 7 |
| RANGER | 8 | 6 |
| FIGHTER | 7 | 5 |
| PRIEST | 10 | 7 |
| WIZARD | 10 | 7 |

Every class starts with a magic pool (including thieves/fighters).

### HP (`OV1` `FUN_0000_09EE` / `0x0AB6`)
```
factor = hp_factors[class][level]       # TWNS @0x000; L1 = 3,4,4,5,4,3
diceCount = (factor * (CON + 5)) >> 4
HPmax = sum of diceCount rolls of (floor(rand16 / 10923) + 1)   # d3
HPcur = HPmax
```
**Remaster (D-007, user decision):** `diceCount = (factor * (CON + 5)) / 15` so averages match the
manual's HP chart; creation shares this with level-up (Core `LevelUp.HitDiceCount`).

### Skills
Seed fields then class coefficients (`TWNS` `@0x21c`, 10×u16 per class/level):

```
skill = clamp(0..255, (coeff >> 1) + 2*linkedAttr + 30 - 49)
      = clamp(0..255, (coeff >> 1) + 2*linkedAttr - 19)
```

Links: attack→STR, parry→DEX, find/spot/listen→INT, disarm/pick→DEX, swim→CON.
Slots `@42`/`@46` exist in the loop but stay unused (coeff 0).

### Starting kit
- Level 1; experience 0; score 0; gold in bank **256**.
- Inventory ids: shield **4**, weapon **48** (CLUB), armor **22** (ROBES) — then
  `FUN_0000_2FDB` picks best usable worn slots by points.
- **Spells known** as `u16[55]` at record `@0x60`: value `1` = known.
  - MONK / WIZARD → FIREFLASH I (index 4)
  - RANGER / PRIEST → HEALING I (index 0)
  - THIEF / FIGHTER → none
  - `FUN_09EE` also marks learnable spells as `2`.

## Outputs / side effects
- One character record appended to `GUILD.DAT`.
- Starting gold credited to that character's bank balance.

## Quirks to watch
- DOS **does** have the duplicate strong-race guard for RANDOM (see above). [C]
- Starting age **[C]** (`OV1` `0x0f04..0x0f4f`): `@264 = 260 + trunc(@252 · 256 · (0.1 + rand/1310720))` — 10 years + 10–12.5 % of the lifespan; AGE = `@264 / 26`. Remaster lifespans: `DIFFERENCES.md` D-003.
