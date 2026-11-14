# Phantasie III — combat actions, Aim, party ranks, melee to-hit

Phase 09 T4 3c.6. Static unless marked live (section 3 running sum, section 4 defence / D3-019 / group fields: live s81, DOSBox-X).
Cross-game module: III's party ranks (Front / Middle / Back) and the Aim and Fire options are new against I. Manual: III PDF 8–9 / pr.10–12, PDF 15 / pr.24. Code: DOS `MAIN.EXE`
(`../FORMAT.md`, `../FUNCTIONS.md`). Tags as in `body-areas.md`. Bows: `bows.md`. Injuries: `body-areas.md`.
Conflicts (not decided here): `DIFFERENCES-III.md` D3-010 … D3-013, D3-018, D3-019.

## 1. Party ranks `[C]`

- Each character has a rank word at record **`+0x08`: 2 Front, 1 Middle, 0 Back**; new and transferred characters
  start Front (2) (creation ovl2 `0x227d`). The combat "Change Rank" option (root `0xfb5f`) offers Front / Middle / Back
  (strings 0–2) and stores `2 − choice`. The party line shows "(Back) / (Mid) / (Front)" (strings 30 + rank).
- **Rank term** (root `0xb934` + callers): `rank_term = 30 · (own rank − average party rank)`, where the average is
  the integer division `Σ rank / party size` over all party members. It is **added** to a character's melee chance to
  hit (root `0xc03b`) **and** to a monster's melee chance to hit that character (root `0xb64e`). So a character ahead of
  the party average hits and is hit more often, one behind it less; a party all in the same rank gets 0.
- Not used by bows (`bows.md`), by spells, or by breath attacks (`0xb582`) `[C]`. Manual: "Front … greatest chance to
  hit and be hit by melee weapons", "Back … least likely … Being at the back does not affect spells or bows" — matches.

## 2. The action menu `[C]`

Root `0xfc41` takes the menu index (strings 490–497: Attack 0, Lunge 1, Slash 2, Thrust 3, Fire Bow 4, Parry 5,
Aim Blow 6, Cast Spell 7; Change Rank is handled apart) and stores an **action code** = string 3 + code ("Attacks (R1)" 0,
"Lunges (R1)" 1, "Slashes (R1)" 2, "Thrusts (R1)" 3, "Fires Bow (R1)" 4, "Parries" 5, "Aims Blow (R1)" 6,
"Casts Spell" 7, R2 forms 11–16 = Attack, Lunge, Slash, Thrust, Fire Bow, Cast at rank 2). "Attack %" below is the
Attack skill (record `+0x30`). "Rank 2 exists" = the second monster group is present (`FAR:0xe63c` > 0).

First, for every choice: **Right Arm Broken or Gone → Parry** (`0xb550`).

| Menu | Rule in code | Manual |
|---|---|---|
| Attack | Attack % ≥ 76 → Attack (R1), 2 swings; else **Thrust**. Never rank 2, also not for Thieves | "two normal swings"; too low → Thrust ✓; Thieves "can … attack … in any rank" ✗ **D3-012** |
| Lunge | rank 2 exists **and class Fighter or Thief** → Lunge (R2); else Thrust | "only for fighters"; non-fighter → Thrust ✗ **D3-011** |
| Slash | Attack % ≥ 150 → Slash (R1), or a Thief picks rank 1/2 when rank 2 exists; Attack % < 150 → **Thrust** | "If … not high enough … to Slash he will Attack instead" ✗ **D3-010** |
| Thrust | Thief with rank 2 present picks rank 1/2; others Thrust (R1) | ✓ |
| Fire Bow | `bows.md` section 3 | |
| Parry | Parries | ✓ |
| Aim Blow | always Aims Blow (R1); **no Attack % minimum** | "If … not high enough attack percentage to Aim … he will Thrust" ✗ **D3-013** |
| Cast Spell | needs a known spell (`+0x56` > 0) and magic ≥ the spell's cost (byte table seg `[0x6962]`:`0xbe0`); #52, #54 and #57 "cannot be cast in combat" (codes 0x34, 0x36, > 0x38); Fireflash #5–8 pick a rank when rank 2 exists; Healing #1–4 and Resurrection #44 pick a party member. **Every refusal is a message and Thrust (R1)** (below) | `[C]` checks; "everywhere or combat spells" ✓ (the refused ids are the manual's N spells; #53 Vision is not in III's table); what follows a refusal is manual-silent |

**Cast refusals (static, root `0xfc41` `0xfe4c`…`0xfffd`)** `[C]`: each shows its message (`0x17dc`, a message
box) and stores action 3 = **Thrust (R1)** (`0xff55`; no rank question, also for a Thief). In order:
"[original text]" (S443, spell count `+0x56` ≤ 0, `0xffef`); "[original text]" (S484,
magic `+0x1e` ≤ 0, `0xffdf`, before the list opens); the list (`0x834:0xd99` = root `0x90d9` "Select <name>'s spell:"
over the record's 32-word list `+0xb0`, sorted first by `0x834:0x16c`: every known spell is listed) returns < 0 (no
pick) → Thrust with no message; the picked id 52, 54 or > 56 → "[original text]" (S423, `0xfec0`);
cost (`0xbe0` byte) > magic → S484 (`0xfee9`); Healing #1–4: the target picker (string 33 "Who shall be healed in
combat?") gives no pick → Thrust; a target whose status (`+0x2c`) is 0 (Dead) → "[original text]" (S421,
`0xff7c`; only Dead is tested despite the text); Resurrection #44: a target whose status is > 0 (not Dead) → "This
character is not dead." (S437, `0xffc6`). The Amiga does the same (`0x19430`…, Amiga `FORMAT.md` section 8.4). Core:
`P3CombatModule.CastRefusal` / `TryChoose` (the id, cost and Healing-target refusals; the list never offers an unknown
spell; no fallback to I's Parry roll, OV2 `0x6f2b`); the two refusals before the list:
`P3CombatModule.CastRefusalBeforeList` (both III order screens).

From the party menu (not at round start, `combat-round.md` section 1) `0x10be4` re-checks: Right Arm ≥ Broken → Parry; a
Fire Bow action with any limb ≥ Broken → Thrust. In the attack itself (`0xc03b`) a Down character's Fire Bow becomes Thrust and Right Arm ≥ Broken → Parry.

**Live s82** (DOSBox-X, `../screens-s82/`): the round's choices are kept in `FAR:0x228 + 2·member` (copied to record
`+0x6e` by root `0xd4ff` later) and the combat list prints the stored code. Fighter, Attack % 100: Slash → "Thrusts (R1)"
(D3-010); Aim at Attack % 100 and 60 → "Aims Blow (R1)" (D3-013); Attack at 60 → Thrust; Monk / Ranger Lunge →
Thrust, Thief Lunge → "Lunges (R2)" without a rank question (D3-011); Thief Attack → "Attacks (R1)" without a rank
question, Thief Thrust → "[original text]" (D3-012). Array after the choices: 3, 6, 6, 3, 3, 14.

## 3. Melee resolution (character → monster) `[C]`

`character_attack` (root `0xc03b`), skipped for Asleep, Out, Dead and Parry:

| Action | Swings n | Damage modifier | Location row (`body-areas.md` section 1) |
|---|---|---|---|
| Attack (R1/R2) | 2 | 0 | 2 |
| Slash | Attack % / 75 + 1 (3 at 150–224, 4 at 225–299, …) | −1 | 2 |
| Lunge, Thrust | 1 | +1 | 2 |
| **Aim** | 1 | 0 | **3** (head 40 %, torso 17 %) |
| Fire Bow | 1 | 0 | 0 (`bows.md`) |

```
mod = modifier above
repeat n times:
    target = random living member (0x b4ec: random start among 5 slots, first living)
    chance = Attack% / n − monster_defence (group word +0x28 = CREATURE.DAT byte 18, section 5)
    if attacker status == Down: chance −= 40
    chance += 30 · (rank − average rank)
    if target rank == 2: chance −= 15
    mod += rnd(weapon_damage)        # weapon_damage = points[weapon +0x6c]; bare hands Attack% / 25; min 1
    if target.status != Okay: chance = 100      # D3-019
    chance = max(1, chance)
    if rnd(100) <= chance: damage_monster(target, max(1, mod), row)   # " hits!"
    else: " misses"
```

- **Quirk** `[C]`: `mod` is not reset between swings, so the second and later swings deal the **running sum** of all
  damage dice rolled so far in that action (hit or miss). Attack and Slash therefore hit harder on later swings.
  Not a manual conflict (the manual gives no formula). **Verified live s81** (DOSBox-X, Attack % 120 so Attack
  gives two swings, Mace = 4 points, modifier 0, against Wild Cats): second swings dealt **5** (after 1) and **6**
  (after 2), more than one Mace roll can give; the other pairs were 1 → 2 and 2 → 3, and after a missed first swing a
  hit dealt 3. In every pair where both swings hit, the second dealt more than the first
  (`../screens-s81/s81_attack_running_sum_round2.png`, `…round3.png`). Thrust hits never went above 5 (4 + 1).
  No other damage term exists (static, s81): the weapon die is `points[weapon]` with no STR or level term (`0xd8a6`;
  bare hands Attack % / 25), the sum starts at 0 once per action (`0xc18d`), gets ±1 for the action and
  `+= rnd(die)` per swing (`0xc282`), and `damage_monster` (`0xb116`) subtracts it from HP unchanged. (In those
  captures fighto is hit at defence 36 because he was Asleep, status 2, which also gives 100.)
- Manual comparison: Thrust "greatest chance … 1-2 points more damage" → code +1 and full Attack % ✓ (+1 only);
  Slash "less likely to hit and 1-2 points less damage" → each swing gets Attack % / n and −1 ✓.
  **Aim** "has less chance of hitting than a normal attack": in code an aimed blow uses the full Attack % for its one
  swing (Attack uses Attack % / 2 per swing) and only changes the location row — part of **D3-013**.

## 4. Monster melee (monster → character) `[C]`

`monster_melee_attack` (root `0xb64e`), per monster: `n = max(1, (attack + 80) / 115)` attacks (attack = group word
`+0x22`), each on a random party member:

```
chance = attack / n − defence[char]
if monster member 0 is Down: chance −= 40
chance += 30 · (char rank − average rank)
if char action == Parry: chance −= points[shield]   # action = record +0x6e: the saved option, written at fight start (0xd55b, re-checked) and end (0xd4ff) only — D3-036
if char.status != Okay: chance = 100            # D3-019
hit if rnd(100) <= chance: damage_character(rnd(group word +0x24), location row 2)
```

`defence[char]` (root `0xd730`) = `points[shield] / 2 − 5 · points[armour] + Parry / 4 + 2 · DEX` `[C]`, **verified
live s81**: the armour term has the **wrong sign** for protection (more armour → easier to hit) — **D3-018**. `0xd730` (called
from root `0x10725` behind a flag; the values are there from the first round) writes it to the word table
`FAR:0x488e + 2·slot`; `0xd81d` (write) and `0xb809` (read) are the only instructions with that displacement. Live
values: armour 0 / 22 / 40 / 30 / 22 / 35 with a 4-point shield gave 46 / 1 / −7 / −18 / 19 / 16, and with no armour and a 20-point shield 54 / 19 / 56 / 40 / 37 / 54,
each equal to the formula. The monster's chance has **no lower clamp** (unlike the character side's `max(1, …)`):
against Wild Cats (attack 40), defence 46 gives −6, so that character is never hit.
**D3-019 (Down target → 100) verified live s81** for monster attacks: 14 of 14 attacks on an all-Down party hit.
The character → monster direction (section 3) **verified live s110**: Thrusts at 31 % hit Okay Skeletons 4 times in 18,
Down Skeletons 18 of 18 and unconscious ones 12 of 12, with plain `1 + rnd(4)` damage (`DIFFERENCES-III.md` D3-019).
**Out targets (s82):** with defence 56 against attack-40 cats, three Out members took 10 hits in five rounds while the
three Okay members took none. Armour
does protect elsewhere: breath damage is `rnd(36) − armour points` (root `0xb582`), which makes the melee sign look
like a bug rather than a design.
Special touches: type 0x22 puts the target to sleep ("is stunned!"), 0x23 drains 40 % of current magic
("is drained!"); neither injures `[C]`.

## 5. Open

- Group-record fields, **confirmed live s81** `[C]` (not open; full map `encounters.md` section 1): group records at
  `FAR:0xe5b0` (R1) and `FAR:0xe63a` (R2), `+0` monster id, `+0x22` attack = `CREATURE.DAT` byte 15 × 10, `+0x24`
  damage = byte 16, `+0x28` defence = byte 18 (Wild Cat 40 / 7 / −22, Orc 40 / 8 / −27, Kobold 50 / 8 / −40).
- Group building and rank swaps: `encounters.md` section 1, `combat-round.md` section 5; monster specials and breath:
  `combat-round.md` section 3; fleeing, surrender, reputation: `encounters.md` section 3–5.
- The skill-use call `0x14f0` made on every swing (training credit?) `[?]`.
