# Phantasie II (Atari ST): divine spells and other magic changes — logic spec (3b.7)

Phase 09 T3, task 3b.7 (`docs/plan/03b-phantasie2-re.md`). This covers **only II's changes**. Everything else in magic
follows I's spec, `reverse-engineering/dos/logic-specs/magic-spells.md`. Addresses are `PHANT.PRG` image addresses
(`../FORMAT.md` §1). Party record `i` = image `0x25162 + 318·i` (`@n` = byte offset n, as in `GUILD.DAT`). `r` =
`rnd100 0xfd40` = `rand() / 327` (0..100). Tags: `[C]` read in the code, `[~]` probable, `[?]` open.
Source: s78 static reading (objdump + Ghidra 12 headless decompiles). Spell behaviour is not checked live (see §6).

Manual (combined I/II rulebook): "DIVINE SPELLS (#58+) may only be acquired in the course of the game for special
purposes" (p.14); the I → II transfer keeps "divine spells (#55 and above)" (p.27). The numbering question is D2-004
(decided: #55+).

## 1. The spell table `[C]`

- 60 slots (`names.json`). Slots 0–53 have **I's 54 names unchanged**, NINJA II at slot 44 and NINJA I at slot 49 included.
  **Slot 56 = DIVINE II (#57, new)**, **slot 57 = DIVINE I (#58, as in DOS I)**. Slots 54, 55, 58 and 59 are empty.
  A player's spell number is slot + 1.
- The class/level table (`TWNS.INT` +0x5a, `extracted/towns/creation-tables.json`) is byte-identical to I. Slots 54–59 are
  0 for every class. Guild Learn (`0x0d0d2`) keeps I's bound (slot < 55), so the Guild never teaches a divine spell.
- **Cost**: n = slot + 1 and cost = n mod 4, with 0 → 4. That is the same formula as I. **DIVINE II costs 1 MP, DIVINE I costs 2 MP.**
  Field `0x6ecc`, combat chooser `0xf298` (`n − 4·⌊(n−1)/4⌋`) and combat MP deduction (`0x7610`) all agree.
- Both spell pickers (`0x17d9c`, the field one and the combat "SPELLS AVAILABLE") list every slot 0–59 whose flag
  `@96 + 2·slot` = 1. A spell whose cost is above the current MP is greyed out. So known divine spells show up in town, in the
  field and in combat.

## 2. DIVINE II (#57, slot 56) `[C]`

**Effect** (field cast `cast_spell_field 0x6ecc`, branch at `0x71bc`):
1. The caster picks the spell. The MP is deducted **before** any test (`0x6f4c`).
2. If location `0x24eb4` = 2 (wilderness) **and** region `0x25b92` = 26, it summons a fight:
   Draw order (Core must replay it). Each rᵢ is a separate `rnd100` call:
   1. r₁ and r₂: the forced-encounter monster id `0x2832c` (**1-based**) := ⌊r₁·r₂·0.0072⌋, i.e. 0–72: `ftoi(fmul(itof(r₁·r₂),
      0xebedfa39))`, the integer product first. In the program's FFP this equals the exact floor for every pair of draws
      (s83, checked by running its own routines, `tools/scripts/st_p2_ffp_oracle.py`).
   2. r₃: if r₃ < 25 (about 25 %), then
   3. r₄: the id is overwritten with `ftoi(fadd(fmul(itof(r₄), 0.02), 75.0))` (`0x7246..0x7282`; FFP `0x96000047` = 75,
      `0xa3d70a3b` ≈ 0.02). The FFP multiply rounds: 50·0.02 is exactly 1 and 100·0.02 exactly 2. So **r₄ 0–49 → 75,
      50–99 → 76, r₄ = 100 → 77** `[C]` (s83, the program's own routines run in a 68000 emulator).
   4. id 0 → 1. The function returns state 11 (encounter start).
   The base id from step 1 is always computed, even when step 3 overwrites it.
3. Anywhere else (another region, a dungeon, a town): nothing happens, and the MP stays spent.

- **1-based ids** `[C]`: combat victory tests `0x2832c == 80` for PLUTO (0-based 79, `0xa290`), and script ops 5/17 store
  `a + 1`.
  - So the 25 % branch summons 1-based 75 = **PLUTO'S WYVERN** (beast V, 0-based 74; r₄ 0–49, 16,350 of 32,768 rand
    values, about 12.5 % of casts), 76 = **PLUTO'S SNAKE-DRAGON** (beast VI, 0-based 75; r₄ 50–99, about 12.5 %) or, at
    r₄ = 100 (68 of 32,768), 77 = **PLUTO'S GIANT WYVERN** (beast VII, minion 7, about 0.05 % of casts).
  - That matches scroll K ("The Book of Beasts"): "Beast V, the mysterious wyvern, and beast VI … roam the astral
    plane and can be summoned by a powerful magic spell".
  - The other 75 % come from the ⌊r₁·r₂·0.0072⌋ part: 1-based 1–72 (0-based 0–71). Low ids are the most likely,
    because a product of two independent draws is weighted toward small values.
- **Region 26** is the Astral Plane `[~]`. Its text cells read "[original text] [original text]"
  (`overworld.json` region 26). Scroll N says vortices link the Netherworld levels through the astral plane.
- Beasts V and VI are Pluto's minions 5 and 6 (`endgame.md` §3).
  - No fixed overworld fight, dungeon script or dungeon fight cell uses them (all 17 regions and 8 levels checked).
  - So **DIVINE II is their only source** found `[~]`. The random-encounter builder `0xfe38` was not fully bounded:
    its third branch adds `2·table[…]` from data+`0x1c66` (`0x1fefe`), which is not decoded `[?]`.
  - Without DIVINE II a party cannot get amulets 5 and 6, so it can never reach SCORE 100 and win (`endgame.md` §2).
- **In combat**: castable, the MP is spent, nothing happens. The combat round (`0x7610`) has no case past AWAKEN
  (action code 100 + n, switch cases 141–151 = spells 41–51) `[C]`.

**How it is gained** `[C]`. One source only: **DNG3 event 21, the pool at (13,36)**. DNG3 is the Impling Hatchery; its
message 105 is "TRY TO VISIT THE ENTIRE HATCHERY".
- Message 52 "[original text]" (a one-shot record), then script 175: message 59 "[original text]",
  then op 18 field 117 := 1.
- Field 117 is the spell flag of slot 56 (`0x12078`: fields 61–120 → `@96 + 2(f − 61)`).
- Op 18 writes only to **non-DEAD** members present (`0x11f0c`).
- The record is one-shot and the op is one-shot (bit 6), so it works **once per load of DNG3** (the DNGX.SAV rules,
  as in I's `dungeon-representation.md`).
- No other script, item or level-up writes slot 56 (all eight DNG files decoded, s78).

## 3. DIVINE I (#58, slot 57) `[C]`

**Effect** (`0x72a4`):
- Only at location 3 (a dungeon). The deity is chosen as `["DEMIGOD", "ATHENA", "APOLLO", "ZEUS"][⌊r₁·r₂·0.0004⌋]`.
  r₁ and r₂ are two separate `rnd100` draws; the index is `ftoi(fmul(itof(r₁·r₂), 0xd1b71735))`, which equals the exact
  floor for every pair (s83, the program's own routines). Zeus needs r₁·r₂ ≥ 7500, which is rare (both draws roughly
  75+). **r₁ = r₂ = 100** (about 1 in 232,000 casts) gives **index 4**, past the 4-entry table (`0x1fd12`): the program
  reads the next bytes ("WHO …") as a string pointer (`0x57484f20`), an ST fault `[C]`. That longword is not in the
  relocation list, so `%s` reads `0x484f20` (24-bit bus), past 4 MB of ST memory: a bus error, the program crashes `[~]`
  (s92, static; not run live). What the remaster shows is **D2-044** (decided s92: ZEUS).
- It prints "`%s` [original text]" in a box (`0x11444`), then **dungeon message 105 of the current level**
  (`0x116aa(105)`).
- Then the spell ends (`0x25f7c` := 0). Outside a dungeon nothing happens and the MP stays spent. In combat: as DIVINE II,
  no effect, MP spent.
- The advice is the level's fixed hint (MESS*n* record 105, `dungeons.json`). The deity name is cosmetic.
- **Core (s92):** `Phantasie2Campaign.CastDivineI` (the `CastFieldSpell` hook): 2 MP first, then in a dungeon the two
  `rand / 327` draws, the deity by the ST's FFP (`DivineIDeity`, exact floor checked for every pair), the advice line and
  the current level's message 105. Combat: the shared caster (no effect, MP spent).

| DNG | Message 105 |
|---|---|
| 1 | "YOU WOULD BE WISE TO FOLLOW THE [original text] AND AS QUICKLY AS POSSIBLE." |
| 2 | "THERE IS A SCROLL IN THE CAPTAIN'S ROOM." |
| 3 | "TRY TO VISIT THE ENTIRE HATCHERY." |
| 4 | "DO NOT FLOOD THE PASSAGE TOO SOON." |
| 5 | "SEEK THE BLUE AREA OR THE RED AREA [original text] CAN ENTER THERE AREAS." |
| 6 | "AVOID THE GAS TRAPS. IF YOU TRIGGER [original text] UP THE GAS SOURCE." (+ leftover `'Y IN THE CHEST.` in the record `[~]`) |
| 7 | "TRUST THE GREAT TROLL." |
| 8 | "PRESS THE NUMBERS OF THE BEASTS AND [original text] BUT IT MAY TAKE YOU A WHILE." |

**How it is gained.** Not in II `[C]`.
- No DNG script sets field 118, the Guild cannot teach it (§1), and nothing else writes slot 57.
- The only way to have it is to win **Phantasie I**: Zeus grants DIVINE I (`dos/logic-specs/endgame.md` §3, "You will
  learn later how to use it properly in Phantasie II"). The I → II transfer then keeps slots 54–59 (`transfer.md` §3).
- So in II, DIVINE I is the "Gelnor hero" spell, and what it does is the advice above. Manual p.14 says divine spells
  "may only be acquired in the course of the game". Whether a II-only party should be able to gain DIVINE I is for the
  user: **D2-017** (NOT A CONFLICT, s79).
- In I, DIVINE I has no effect anywhere (`magic-spells.md` §DIVINE I).

## 4. Other spell changes vs I

- **Field casting** (`0x6ecc`, I `h50:0x07c4`) keeps HEALING I–IV (slots 0–3, target picker), RESURRECTION (#44,
  `0x9188`), #52 MONSTER EVAL. / #53 VISION and #54 TRANSPORTATION handling. Only the two divine cases are new.
  Re-checking the existing ones against I is the job of `magic-spells.md` parity work. Not done here.
- **RESURRECTION** (`0x9188`, field and combat case 144) revives the **first** DEAD member with CON > 0 for whom
  r < 5·(caster level − target level) + 50. That member gets status 1 and HP = max. If no member passes, nothing happens
  `[C]`. It is the same rule shape as I `[~]`: not diffed line by line here.
- Combat spell cases 141–151 (CHARM … AWAKEN) and the damage/buff blocks for 5–40 are present as in I `[~]`. The
  per-level constant tables `0x7b60…0x8224` were not diffed against I (open, `magic-spells.md` P0.5 territory).
- No other new spell, no new spell effect, no new class access.

## 5. Manual questions (`09-trilogy-manual-p2.md` §3)

- **Q14 (resurrect wording)** `[C]`: no wizard can ever have RESURRECTION (#44). The class table gives slot 43 to the PRIEST
  at level 9 and the RANGER at level 13; the WIZARD row is 0. That table is the one I uses (byte-identical) and the one the
  manual prints on p.14. So CB p.10's "a high-level **wizard** using a RESURRECT spell" disagrees with the code and with the
  manual's own spell tables. I's manual says priest. Raised as **D2-015** (decided s79: manual, wizards learn it at level 13).
  Core (s79): II's `creation-tables.json` gives WIZARD slot 43 level 13; the ST's 0 is `dos_tables`, played by the rules
  option `p2.wizard-resurrection` = `st` (Original (Atari ST) preset).
- **Q4 / D2-004** (decided) gets two facts from this spec: DIVINE II is gained only in DNG3 and summons in region 26 only;
  DIVINE I is gained only in Phantasie I and gives dungeon advice.

## 6. Live check

Not practical yet. A live check needs a II party in region 26 (DIVINE II) or in a dungeon (DIVINE I) with the spell flags
set. That means editing `GUILD.DAT` (`@96 + 2·56/57` = 1, MP ≥ 2), forming a party, and walking or teleporting to region
26 or into a dungeon. The harness can do it (`../parity/`), but the party-building UI flow is not scripted yet. Planned
check: cast DIVINE I in DNG1 (expect the box + message 105 and MP −2), and cast DIVINE II in region 26 (expect a fight; a
fight with PLUTO'S WYVERN or SNAKE-DRAGON about a quarter of the time).

## 7. Open

- Answered s83: the DIVINE I index is the exact floor (index 4, past the table, at r₁ = r₂ = 100); the DIVINE II beast
  split is 75 / 76 / 77 (§2). Core (s83): DIVINE II's two draws go through `MotorolaFfp`
  (`Phantasie2Campaign.DivineIiBaseMonster` / `DivineIiBeastMonster`). DIVINE I is not built in II's Core yet (a transferred
  character can bring it; the ST gives the advice box in a dungeon).
- ~~Whether random encounters can also produce beasts V/VI~~: no `[C]`. Random-roll tiles have terrain digit ≤ 4 on
  every grid, so random fights stay within ids 0–60 (`encounters.md` §4).
