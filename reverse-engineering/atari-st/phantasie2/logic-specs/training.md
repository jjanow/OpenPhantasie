# Phantasie II (Atari ST): Guild training and level-up — logic spec

Phase 09 T3, task 3b.7. What II's Guild Training and level-up do differently from Phantasie I. Everything not listed
is I's rule: `reverse-engineering/dos/logic-specs/town-menus.md` §Training and §Level-up gains. Tags as in
`docs/plan/RE-REFERENCE.md` (`[C]` read in `PHANT.PRG`, `[C] live` also seen in Hatari). Live check:
`../parity/toss-training-live.md`. Function pairs: `00-function-map.md` section 5.1.

Code: `guild_train 0x0cdde` (I-Amiga `h5:0x13b0`), cost box `confirm_cost 0x017e4`, `level_up 0x0c71a`
(`h5:0x1748`), class switch table data+`0x46f6` (order THIEF, MONK, RANGER, FIGHTER, PRIEST, WIZARD).

## 1. Training threshold `[C] live`

```
train(member):                                    # Guild → Training → "[original text]" (party members)
    if status == DEAD or level >= 15: refuse      # RSC alert; level-20 elementals/undead are refused too
    s = statSum / divisor                         # FFP single precision; table below
    F = 1.0               if level == 0           # NEW in II
        1.8^level         otherwise               # no DOS-style "XP == 0 → F = 1"
    X = (F − 0.8) · 1000 / (s + 1e−5)
    v = XP − X;  n = trunc(v)
    n < 0:  alert "You need −n more experience points" [GO AND LEARN]; no cost shown
    else:   gold = min(15000, trunc(level^2.2) · (20 − CHA)² · town price index / 100)
            alert "It will cost <gold> gold" [AGREE | NOT]
            AGREE and carried gold < cost → "You can not afford it" [SORRY]
            AGREE and enough → carried gold −= cost; level_up(level)          # XP is not spent
    loop back to the member picker until EXIT
```

| Class | statSum / divisor | I (DOS and Amiga) |
|---|---|---|
| THIEF | (STR + 2·DEX) / 19.8 | same |
| MONK | (STR + INT + DEX) / 49.5 | same |
| RANGER | (STR + INT + CHA) / 52.8 | same |
| **FIGHTER** | **(2·STR + DEX) / 33.0** | (STR + DEX) / 16.5 |
| **PRIEST** | **(STR + 2·INT) / 36.3** | (STR + INT) / 18.1 |
| WIZARD | (2·INT + LUCK) / 42.9 | same |

Every class's threshold was read live from a "You need N" box (THIEF 370, MONK 2575, RANGER 2399, FIGHTER 549, PRIEST
6508, WIZARD 2265; `../parity/toss-training-live.md`). What changed from I, all `[C]`, items 1–4 seen live:

1. **FIGHTER and PRIEST sums and divisors.** Strength counts double for fighters, intelligence for priests. At equal
   stats S both need 4/3 of I's XP. Live: FIGHTER STR 22 DEX 15, level 1, XP 10 → "You need **549**" (I's formula: 435).
2. **Level 0 → F = 1.0** (X = 0.2 × the level-1 value). Only transferred characters have level 0, so this is the
   rule that lets them train at once (manual p.27). Live: THIEF level 0 XP 5000 → "It will cost **0** gold".
3. **No XP = 0 rule.** DOS I sets F = 1.0 whenever XP is 0 (any level); II does not (nor does Amiga I). Live: RANGER
   level 1, XP 0 → "You need **2399**" (DOS would say 479).
4. **Level-0 price is 0.** `trunc(0^2.2)` through the FFP library: `ffplog(0)` returns the largest negative number,
   `exp` of it underflows to 0 `[C]`, so g = 0 and the cost is 0 whatever the town or CHA (live, above).
5. **CHA above 20 is not clamped** (`(20 − CHA)²` grows again), as in Amiga I; DOS clamps `max(20 − CHA, 0)`. No race
   can reach CHA 21 at creation in II (best ceiling 18, `extracted/towns/creation-tables.json`) and II has no magical
   pools, so it only matters for stats raised some other way `[~]`. Static only (not seen live).

Unchanged from I and seen live: the "You need N more" box shows **no cost** (I's D-050 behaviour), the cost box asks
AGREE / NOT, a short purse gives "You can not afford it", the price uses the carried gold ("GOLD CARRIED"), not the
bank. Other live values: MONK level 2 XP 50 → 2575; PRIEST level 4 XP 10 → 6508; WIZARD level 3, CHA 18, Pippacott
(price 150) → 66 gold (= 11 · 2² · 150 / 100).

**FFP arithmetic (`[C]`, checked with `tools/scripts/st_p2_ffp_oracle.py`, the program's own routines in a 68000
emulator).** `guild_train 0x0cf58…0x0d012` computes in FFP singles, step for step:
`s = itof(sum) / D` (D as stored: THIEF `0x9E666645`, MONK `0xC6000046`, RANGER `0xD3333346`, FIGHTER `0x84000046`,
PRIEST `0x91333346`, WIZARD `0xAB999946`); `s += 1e−5` (`0xA7C5AC30`); **F = 1.8 (`0xE6666641`), multiplied by 1.8 once per
level above 1** (no `pow`; 1.0 = `0x80000041` at level 0); `X = (F + −0.8) · 1000 / s` (`0xCCCCCCC0`, `0xFA00004A`);
`n = ftoi(itof(XP) − X)` (`fsub 0x1dd1a`, truncation toward zero). The price uses the library `pow` (`0x1dba6`):
`g = ftoi(pow(itof(L), 2.2))` (2.2 = `0x8CCCCC42`) equals `trunc(L^2.2)` in doubles for every level 1–15.
The threshold does not: the single-precision F drifts at high levels, so over every sum 3–129 × the six divisors ×
levels 0–14 × the five XP values around each threshold, 7,925 of 57,150 `n` differ from a double model by 1–3 (e.g.
THIEF sum 3 level 12: X = 7,629,300.0 in FFP vs 7,629,303.6). Core: `TrainingRules.Arithmetic` = `motorola_ffp`
(II's `rules.json`, with each class's `divisor_ffp`) runs this path (`MotorolaFfp.Sub` / signed `Add`);
test `P2TrainingFfpTests` (40 oracle cases, 24 of them where FFP and double differ). The live values above agree with
both.

## 2. Level-up `[C] live`

Same as I's routine (`town-menus.md` §Level-up gains: MP ceiling, nine skills, HP re-roll, level + 1) plus:

1. **TOSS bonus and skill:** before the skill loop, `@62 = 2·L + 4·DEX + STR + 10` (+15 DWARF, +30 GNOME); TOSS then
   grows like any skill (`toss-rock.md` section 3).
2. **Spell marks: every unknown spell up to the new level**, not only the new level's:
   `for each spell s: if 0 < spell_level[class][s] ≤ L + 1 and flag[s] == 0: flag[s] = 2` ("can learn").
   DOS marks `== L + 1` only (with the same "still 0" test); Amiga I marks `== L + 1` and has no "still 0" test at all.
   Effect: a spell flag that is 0 below the current level (cleared by the I → II transfer, or never marked) comes back
   at the next training. Live: a WIZARD with all flags 0 trained 3 → 4 and got every level 1–4 spell marked (slots 4, 5, 6,
   8, 9, 12, 13, 40), where DOS's rule would mark only the level-4 ones.
3. HP: a roll ≥ the old maximum becomes the new maximum (capped 255), a lower roll gives +1 — II and Amiga I agree;
   DOS's spec text reads "roll > old" for the first case, so an equal roll gives +1 there `[~]` (not re-checked in DOS).
   HP current is not raised (live: a transferred-style character with HP 0/0 trained to HP 0/4, status OK).

The training check (`../parity/make_p2_train_guild.py check`) reproduces every saved word of the trained records
except the dice-rolled HP maximum, the MP ceiling (`@14`, a table lookup the model leaves out) and the bank gold
(the save's share payout).

## 3. Manual

The manual's training text is I's (CB pp.4–5, shared) and gives no formula, so the new numbers are not a conflict
(D2-012). The manual says you are told the XP needed **and** the cost; II, like DOS I, shows the cost only once the XP
is enough → `DIFFERENCES-II.md` D2-013 (I's D-050 again). The level-up HP rule is I's again (I's D-007, manual chart vs
re-rolled dice) → also D2-013.

## 4. Core (s79)

- **Threshold and price:** `RulesProfile.Training` (`TrainingRules`, `GameData/phantasie2/rules.json` `training`):
  section 1's sums and divisors, `F = 1.0` at level 0, `Shortfall` = `−trunc(XP − X)`, `GoldCost` with no CHA clamp
  (`charisma_clamp: false`) and level 0 free. `TownMenus.Train` / `CanTrain` / `TrainingXpShortfall` /
  `TrainingGoldCost` take the profile's rules; I's values still run through the bit-exact DOS chain. Session:
  `GameSession.Train`, `TrainingXpShortfall`, `TrainingGoldCost`.
- **Level-up deltas (data):** `spell_marks: up_to_new_level` (section 2 item 2) and `hp_tie: new_maximum` (section 2 item 3), read by
  `LevelUp.Apply`. TOSS (section 2 item 1) grows while the Toss Rock module is on (`toss-rock.md` section 7).
- **D2-013 (user: manual, as I):** rules options `p2.training-cost` (`manual`: the cost is shown with "You need N";
  `st`: hidden, the ST's `confirm_cost`) and `p2.hit-dice` (`manual`: `factor·(CON + 5)/15`, the chart; `st`: `>> 4`).
  Both `manual` in the Original (manual) and Remaster presets, `st` in Original (Atari ST).
- **D2-015 (user: manual):** `creation-tables.json` gives WIZARD slot 43 (#44 RESURRECTION) level 13; its
  `dos_tables.spell_levels` holds the ST's 0, applied by `p2.wizard-resurrection` = `st` (Original (Atari ST)).
- Pinned by `Phantasie2RulesTests`: the six live "You need" boxes, the 0 and 66 gold boxes, the live WIZARD spell marks,
  the KLEPTO / GLADE TOSS values.
- **Left for the campaign:** the Client's Guild screens still call I's static `TownMenus` overloads; II's creation needs
  its lifespans `[?]`; the MP ceiling `@14` is the table lookup I uses.
