# Phase 13: Aging normalised across I–IV (P3 QoL)

**Asked for (user, PLAN P3 "QoL ideas from the III decisions", from D4-033's decision):** "normalise aging against all four
games". Today each game ages its characters by its own rule, and a character that moves between games can gain or lose
centuries of life on the way. This option gives all four games one aging model.

QoL, so it is a rules option. Off = each game's own rule, exactly as before. It is locked per save (T5.0). The model is the
remaster's: no original has a cross-game aging rule. It is built from the rules the user already decided for I (D-003),
II (D2-021) and III (D3-041). So there is no new manual↔code conflict and no `DIFFERENCES*.md` entry of its own; D4-033's
decision points here.

Tags: `[C]` checked in the code, `[~]` probable, `[?]` unknown.

**Built:** all four games, Core. The Options row is the generic QUALITY OF LIFE row; no other Client change. Section 7
lists what is left.
- Code: `SharedAging.cs` (`SharedAgingModel`), `Aging.IsPastDecline` / `IsPastLifespan`, `CharacterCreationTables.ForRules`
  (`sharedAging`), `P4Aging` (`SharedLifespan`, `SharedStartingAge`, `AgeCapFor`, `Check(…, lifespan)`), `P4Member.Lifespan`,
  `P4Hall.JoinFromPhantasieIII`.
- Data: `GameData/shared/aging.json`, read by `OpenPhantasie.Data.SharedAgingLoader`.
- Tests: `SharedAgingTests` (Core) and `SharedAgingClientTests` (Client).

---

## 1. Each game's own rule today (option off)

| Game | Lifespans | Age unit | Grown older | Dies | Option |
|---|---|---|---|---|---|
| I | manual bands ±10 % by seed `@10` (D-003); DOS `@10 ×` race factor in 256-unit steps | 1/26 year (`@264`) | past 70 %, abilities × 4/5 once | past 100 % | `p1.lifespans` |
| II | as I (D2-021); ST factors | as I | as I | as I | `p2.lifespans` |
| III | I's bands (D3-041); DOS III: none (it never reads its maximum age) | as I | as I | as I | `p3.lifespans` |
| IV | manual bases (Elf 2,000, Dwarf / Gnome 200, Human 80, others 45) × 0.9–1.1 by the **current** Luck (D4-033); X68000 aging factor F | 1/26 year (`+1E`, cap 65,000) | at ≥ 70 %, × 0.8 (MSX2) or 0 (X68000, D4-034) | at ≥ 100 % | `p4.lifespans`, `p4.aging` |

The age unit (1/26 year) is already the same in all four. What differs:
- the lifespans: Elf 3,000 or 2,000; IV's "others" are 40.5–49.5;
- how the luck roll spreads them;
- IV's `>=` against I's `>`;
- IV's lifespan falls when a decline lowers Luck;
- the code variants: DOS / ST factors, no aging in DOS III, X68000 zeroing;
- the transfers:
  - I ↔ II and I / II → III copy the age and the lifespan (III under `dos` drops the lifespan);
  - the III → IV Guild join rolls a new age from IV's own rule.

## 2. The option

`shared-aging` is one every-game rules option (`RuleOptionDescriptor` with `EveryGame` and `Qol`, all four games). It sits in
each game tab's QUALITY OF LIFE section, before GUILD TREASURY.

| Value | Meaning | Presets |
|---|---|---|
| `off` | each game's own rule (section 1), every `pN.lifespans` / `p4.aging` value as before | Original (manual), Original (DOS / ST / X68000) |
| `on` | the shared model (section 3) in every game; it overrides `p1.lifespans`, `p2.lifespans`, `p3.lifespans`, `p4.lifespans` and `p4.aging` | **Remaster** |

**Remaster preset on**, as the treasury: the presets' other rules already play the manual model in I–III, so for I–III the
Remaster preset changes nothing but the transfers. For IV it changes the user-confirmed D4-033 values (Elf 2,000 → about
3,000; "others" 45 ± 10 % → 40–50) and D4-034's point (`>=` → `>`). The Original (manual) preset keeps IV's own manual
lifespans, so D4-033 as decided stays one preset away.

Labels: SAME AGING IN I-IV: THE GAME'S OWN / SHARED (`option.shared-aging*` in `GameData/shared/strings/en.json`).

## 3. The shared model (remaster rule)

The model is I's D-003 manual model (user s15), applied in every game. Its numbers are in `GameData/shared/aging.json`.

- **Age unit:** 1/26 year (2 weeks): I–III's age clock `@264`, IV's `+1E`. Ages need no conversion between games.
- **Lifespan per race,** the I / II / III manuals' figures ±10 %:

  | Race | Years |
  |---|---|
  | Elf | 2,700–3,300 |
  | Dwarf, Gnome | 180–220 |
  | Human | 72–88 |
  | every other race | 40–50 |

  "Every other race" covers Halfling, Pixie, Sprite, the monster races and IV's races past 14 (Hill Giant, the mixed
  races). UNDEAD has no band; the transfers use its living race.
- **Elf: 3,000, not IV's 2,000.** Three manuals of four (I, II, III) say "about 3000"; IV's says about 2,000.
  - The code gives no reason to prefer 2,000: DOS I's Elf dies at 1,506–2,441 years, the ST's the same, DOS III's
    unread maximum reads as 1,497–2,433, and the X68000 Elf never dies.
  - The only code-side catch is IV's age cap (65,000 units, about 2,500 years), which would stop an Elf from ever dying.
    The option lifts that cap (section 4); nothing writes IV's u16 record.
  - I's DOS export already clamps an age clock over u16 and lists it (D-003).
- **Where in the band:** the game's luck roll picks one of six steps:
  - lifespan = round(26 × (lo + step × (hi − lo) / 5));
  - step = luck − the game's lowest roll, clamped to 0–5;
  - lowest roll: I / II seed `@10` 8; III LCK 8; IV Luck 9.
  
  For I–III this is exactly D-003's formula. A II seed of 14 and a declined Luck clamp to the band.
- **Fixed once:** the lifespan is set when the character is made and stored. It is never recomputed from the current
  Luck, so a decline does not shorten it (IV's own manual rule does).
- **Starting age:** I's shape, 10 years plus 10–12.5 % of the lifespan.
  - I–III: `CharacterCreation.StartingAgeClock`.
  - IV: on IV's own single draw, trunc(260 + L × (0.1 + (rand(100) − 1) / 4000)). Every later creation draw stays where
    it was.
  
  Start ages: others 14–16, Human 17–21, Dwarf / Gnome 28–37, Elf 280–422.
- **Grown older:** age > 70 % of the lifespan (I's OV1 `0x1954` compare, `Aging.IsPastDecline`). It happens once: the six
  abilities and I's `Word12` / IV's defence byte × 4/5, truncated. This is MSX2's × 0.8; X68000's zeroing is not used.
- **Dies:** age > lifespan (`Aging.IsPastLifespan`); the abilities become 0.
- **When:** on town entry, as every game already checks.
- **Time:** how fast time passes stays each game's own: I / II travel time, III's thirds, IV's day counter / 0x700, the
  inns, D-067's Guild Add, III's inn, IV's magic training. A year is the same length everywhere, but the games' clocks
  are their own (section 7). Who ages while dead also stays each game's own (I / II and IV age the dead; III skips them).

A character without a lifespan (0) never ages out, as today: an UNDEAD, or a record from an old save.
- IV gives a member made before the option one on its next town entry, by its race and current Luck.
- I–III characters made under the Remaster preset already have one.

## 4. How each game uses it

- **I, II:** `GameOptions.OriginalLifespans` is false under the option, so:
  - creation uses the shared bands (`ForRules(…, sharedAging: game)` swaps them in and drops the DOS / ST factors);
  - the town-entry check compares age units, not DOS's 256-unit steps;
  - a DOS import (F7) takes the band, not `@252`.
- **III:** `P3NoLifespans()` is false under the option, so creation gives the shared lifespan and I's start age, also
  under `p3.lifespans` = `dos`, and the town entry ages and kills as I.
- **IV:**
  - creation stores `P4Member.Lifespan` (saved in `P4MemberMore.Lifespan`, optional, no codec version bump) and takes
    the shared start age;
  - the slot-0 race bug of the X68000 lifespans is not used;
  - `P4Aging.Check` compares the stored lifespan with I's comparisons;
  - `ScaleOnAging` is × 0.8 under any `p4.aging`;
  - the age cap is lifted (`AgeCapFor`) at the town entry, magic training and the Guild join.

## 5. Transfers (age, lifespan and "has aged" carried unchanged)

| Transfer | Under the option |
|---|---|
| I → II, II → I (`TransferFromPhantasieI` / `II`) | the converters copy the character (age clock, lifespan, "has aged"); a character without a lifespan gets the shared band of its living race, luck clamped 8–13 (`ReplaceRoster`) |
| I / II → III (`AdmitTransferred`) | age, "has aged" and lifespan kept, also under `p3.lifespans` = `dos`; none → the shared band of its living race |
| III → IV Guild join (`P4Hall.JoinFromPhantasieIII`) | the age clock becomes `+1E` unchanged (same unit), "has aged" and the lifespan kept; IV's rolled age is replaced; none → the shared band by III's LCK. Abilities are III's (already declined if it aged), so no second decline |
| III / I / II → IV's Hall | Hall records hold no age: nothing to carry |
| Lineage (descendants in I–III and IV) | a new character: the shared lifespan and start age of its game's creation |

Off: every transfer as before. In particular, the III → IV join rolls a new age, and III under `dos` drops the lifespan.

The option is per save. It is the target save's value that decides how an arriving character is treated. A character
from a save with the option off keeps the lifespan it had there (e.g. a DOS-factor lifespan from I under `p1.lifespans` =
`dos`), because the rule is to carry, not to recompute.

## 6. Tests

- `SharedAgingTests` (Core, 19 cases):
  - the descriptor and presets;
  - the overrides of each game's own switches;
  - the model equals I's bands;
  - the same lifespan in all four games for a race and luck step;
  - clamping;
  - I / II / III creation under their Original presets with the option on;
  - the town entry in age units;
  - IV creation, check, × 0.8 under X68000, the lifted cap and the save round trip;
  - I ↔ II, I → III and III → IV carries, with off as before.
- `SharedAgingClientTests` (Client):
  - the row sits under QUALITY OF LIFE;
  - it shows THE GAME'S OWN for new games and SHARED in a locked Remaster save.
- `TreasuryClientTests` already checks every `Qol` row's place.
- `GameOptionsTests` and `Phantasie4RulesTests` list the new id.

## 7. What is left

- **Clocks:** each game keeps its own time scale (the age unit is shared, the speed of time is not). A trip in IV adds
  one age unit per 0x700 days of its day counter; I adds (travel time ≫ 8) / 7. So a character still ages at a different
  pace per game. Normalising the clocks would change each game's travel and inn rules; not done `[~]`.
- **Dead members:** I / II and IV age the dead, III does not (each game's own, kept) `[~]`.
- **Carried, not recomputed:** a character from a save with the option off brings that game's lifespan (a DOS / ST
  factor one); no transfer leaves IV. A save with the option on keeps it, by design `[~]`.
- **IV lazy lifespan:** an IV member with no stored lifespan gets one at its next town entry from its current Luck, so a
  member who already declined lands at the low end of its band `[~]`. IV's join from III with no III lifespan uses III's
  LCK the same way.
- **Seen live on Xvfb:** I's creation, inn and town entry (s179); the I → III transfer, also under `p3.lifespans` =
  `dos` (age, lifespan and "has aged" carried; a missing lifespan gets the band), and IV's lazy lifespan at town entry
  from the current Luck (s205, `parity/s205-qol-live.md`). II ↔ I and the III → IV join are unit-tested only `[~]`.
