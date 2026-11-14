# Phantasie III — bows, Fire Bow skill, Arrow Flame and Accuracy

Phase 09 T4 3c.6. Static unless marked live. Cross-game module: III replaces II's thrown rocks with bows
(manual App. II, PDF 14 / pr.23). Manual rules: `docs/plan/09-trilogy-manual-p3.md` section 4. Code: DOS `MAIN.EXE`
(`../FORMAT.md`, `../FUNCTIONS.md`). Tags `[C]` / `[~]` / `[?]` as in `body-areas.md`. Conflicts: `DIFFERENCES-III.md`.

## 1. Items and the bow slot

- Bows are items **41–60** (names string 150 + id; points `DS:0x113c + id`, requirement `DS:0x11a2 + id`, both equal to
  the manual's bow table) `[C]` (`FORMAT.md` section 4.1).
- **Requirement:** a bow is usable when **STR + DEX ≥ requirement** (shields / armour: STR; melee weapons:
  2·STR + DEX) `[C]` (ovl2 `0x39b5`). Matches the manual.
- **Separate bow slot:** record `+0x86` (item id, 0 = none), beside shield `+0x68`, armour `+0x6a`, weapon `+0x6c`
  `[C]`. "Ready best equipment" (ovl2 `0x37f1`) puts the usable carried bow with the highest point value there.
- New characters (and transferred ones) get **44 Short Bow if Thief, else 41 Self Bow** (`FORMAT.md` section 5.1) `[C]`.

## 2. Fire Bow skill

- Skill value record `+0x48`, training seed `+0x4a` `[C]`. Seed at creation = **2·Level + 4·DEX + STR + 10, +30 Thief,
  +15 Elf** (root `0xa1f2`); the Thief and Elf bonuses match the manual. How training raises the value: as the other
  skills, `training.md` section 5 (root `0x9ea0`, cap 250) `[C]`.

## 3. Choosing Fire Bow (combat menu) `[C]`

Root `0xfc41` (menu entry 4), re-checked at round start by `0x10be4` and in the attack itself (`0xc03b`):

| Condition | Result |
|---|---|
| Right Arm Broken or Gone | Parry (as for every action) |
| Left Arm, Right Arm, Left Leg or Right Leg ≥ Broken | Thrust instead |
| no bow worn (`+0x86` = 0) | Thrust instead |
| race Elemental (16) | Thrust instead |
| status Down (4) | Thrust instead |
| a second monster rank exists | "[original text]" (1 / 2) → Fires Bow (R1) or (R2) |
| otherwise | Fires Bow (R1) |

Manual: "may not fire a bow if any of his Arms or Legs are Broken" — the code also bars Gone limbs and Down
characters (manual silent: not a conflict, a clarification) `[C]`.

## 4. Resolving a bow shot `[C]`

Once per fight (`combat-round.md` section 1), `combat_char_stats` (root `0xd730`) sets for party member i: `bow% = Fire Bow skill (+0x48)` and
`bow_damage = 2 · points[bow] + 4` (min 1; with no bow 4). `character_attack` (root `0xc03b`), for a Fire Bow action:

```
swings = 1
target = random living member of the chosen rank's group
chance = bow% − monster_defence / 2          # monster_defence = group word +0x28 = CREATURE.DAT byte 18
if target.status != Okay: chance = 100      # sleeping / down / unconscious targets: auto-hit (D3-019)
chance = max(1, chance)
if rnd(100) <= chance:                       # hit
    damage = max(1, rnd(bow_damage))
    damage_monster(target, damage, location row 0)   # body-areas.md section 2: row 0 = head 69 %, torso 11 %
else: " misses"
```

- No rank term and no −15 for the second rank: **party position does not affect bows** and bows reach rank 2 at full
  chance `[C]` (manual: "Being at the back does not affect spells or bows").
- Melee subtracts the full monster defence, bows half: "Armor is less effective vs. bows" `[C]` (group fields live s81,
  `combat-actions-and-ranks.md` section 5).
- Row 0 of the hit-location table gives bows the best chance at Head / Torso (80 %) `[C]`, as the manual says. Against
  the "tall" monsters (ids 44, 67, 68) every hit uses row 4 (no Head) (`body-areas.md` section 1).

## 5. Spells that change bow fire `[C]`

`combat_spell_5_28` (root `0xc3cc`, jump table at seg `0xb0b`:`0x15a1`) and `combat_spell_29_56` (root `0xc687`) overwrite
the per-fight values for the rest of the fight (`0xd730` runs once per fight; live s82: Arrow Flame values kept in
round 2 without a recast) `[C]`:

| Spell | Effect in code | Manual |
|---|---|---|
| #13 Arrow Flame I (caster) / #14 P.Arrow Flame I (party) | bow% = Fire Bow + 50; **bow_damage = points[bow] + 15** | "+50 percent and Bow Point Values by +15" |
| #15 Arrow Flame II / #16 P.Arrow Flame II | bow% = Fire Bow + 100; **bow_damage = points[bow] + 25** | "+100 percent and bow point values by +25" |
| #55 Accuracy (caster) / #56 Party Accuracy | bow% = 250 | "raise … Fire Bow percentage to 250" |

- Accuracy matches the manual. The jump table at seg `0xb0b`:`0x19bf` sends #55 to `0xca34` (bow % word
  `FAR:0xdd9c + 2·caster` = 250) and #56 to `0xca46` (the same for every slot `0 … party count − 1`, whatever its
  status); neither touches the damage word, so Accuracy after an Arrow Flame keeps the flame's damage, while a later
  Arrow Flame writes both words and so ends the 250 `[C]`.
- Costs: III's spells sit in their own slots (index = slot), so #55 / #56 cost 54 % 4 + 1 = 3 and 4 MP, as the
  manual's table. Built in `P3CombatModule` / `P3Fight.BowStatsOf` on the shared `SpellSlots` layout.
- Casting one over the other can lower the % (the last spell sets it): **D3-039**, decided for the manual (no spell
  lowers the %; DOS's side is `p3.bow-spells` = `dos`).
- Arrow Flame **replaces** the damage die `2·points + 4` with `points + 15/25`.

**Verified live s82:** bow damage words `FAR:0xb58c + 2·member` 6 / 44 (Self Bow / God Bow) became 16 / 35 after
P.Arrow Flame I, bow chance `FAR:0xdd9c + 2·member` 60 → 110, and both stayed through a later round without a cast. Bows have
at most 20 points, so Arrow Flame II always raises the range, but Arrow Flame I lowers it for bows over 11 points
(#52 Compound Bow +2 and up) — **D3-017** (manual: "increase the accuracy and damage").

## 6. Open

- Monster archers / monster bow damage `[?]`.
