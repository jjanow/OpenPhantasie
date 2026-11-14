# Phantasie III — training (level-up, HP, skills)

Phase 09 T4 3c.6 (static). Source of truth: the III manual (Guild ▸ Training PDF 5 / pr.5,
Skills PDF 4 / pr.3, Social Class PDF 5 / pr.4, Hit Point chart PDF 8 / pr.11, CHA PDF 4 / pr.3). Code: DOS `MAIN.EXE`
(addresses `../FORMAT.md` section 1; functions `../FUNCTIONS.md`). Tags: `[C]` read from code / data bytes, `[~]` probable,
`[?]` not decoded. Manual ↔ code conflicts go to `DIFFERENCES-III.md`; they are not decided here.

Record fields below are offsets in the 262-B character record (`../FORMAT.md` section 5); training works on the work copy
`FAR:0x9de8` (the party record is copied there and back with `rep movsw`).

## 1. Who may train `[C]`

Guild ▸ Train Member (root `0xad01…0xad35`, inside `guild_menu` `0xac21`): needs a non-empty party and `FAR:0xfc9a`
clear (the party-state flag: 1 party dead, 2 after the Astral Plane, 3 after the gods' reward; arriving in a town,
root `0xa911`, clears it, so in town it is always 0). The picked member is refused with "This character is unable
to do this." (string 417) when

- status (`+0x2c`) is **Dead (0) or Out (3)**, or
- race (`+0xa8`) is **17 Undead**, or
- level (`+0x74`) is **≥ 21**.

Learn Spells uses the same Dead / Out / Undead test without the level test. So an Undead character (Phantasie I
transfer, D3-007 / D3-016) can neither train nor learn spells in DOS III. The manual's tables stop at level 15 and it
names no cap; the code's cap is 21 (after training, level is clamped to 21).

## 2. Experience needed `[C]` (ovl2 `0x1031`, `training_level_up`)

```
L     = level (+0x74)                       # current level, before training
s     = class score, integer maths:         # attribute fields: STR +0x0a, INT +0x0c, DEX +0x0e, CHA +0x12, LCK +0x14
        Fighter  (2·STR + DEX)       · 100 / 33
        Thief    (2·DEX + STR)       · 100 / 20
        Priest   (2·INT + STR)       · 100 / 36
        Ranger   (STR + INT + CHA)   · 100 / 53
        Monk     (STR + INT + DEX)   · 100 / 50
        Wizard   (2·INT + LCK)       · 100 / 43
        other    1
s     = max(s, 17)
need  = (1000 · G[L] − 80000) / s           # 32-bit; G = DS:0xb88, u32 per level: G[L] = floor(100 · 1.8^L)
if score (+0x82) >= 250: need = need / 6
elif score >= 200:       need = need / 2
short = need − XP (+0x102)
```

- `G[1..21]` = 180, 324, 583, 1050, 1890, 3401, 6122, 11020, 19836, 35705, 64268, 115683, 208230, 374813, 674664,
  1214395, 2185911, 3934640, 7082353, 12748236, 22946825 `[C]` bytes. So need = (1.8^L − 0.8) · 1000 · 100 / s — the
  same curve as DOS I (`reverse-engineering/dos/logic-specs/town-menus.md`, "(F − 0.8) · 1000 / s") with the class
  score scaled by 100. Unlike I there is no "XP 0 → level-1 value" special case.
- The Wizard row reads `+0x14`, which the sheet labels LCK (`../FORMAT.md` section 5) — read as written `[C]`; a typo for a
  different stat would be a guess.
- **Score** (`+0x82`) lowers the requirement: from 200 it halves, from 250 it is a sixth. Score comes from quest bits
  and the endgame titles (`endgame.md` section 2): a Darklord's score is held at 200–249, a Superhero's at ≥ 250.
- `short > 0` → " must gain" / *short* / "more experience points" / "to go to the next level." / "Continue your
  adventuring." No training.
- The manual says the XP needed "depends on his race, attributes, and class" (PDF 6 / pr.7). The code has no race term;
  race acts only through the attributes it allows. Not raised as a conflict (the manual's wording is satisfied
  indirectly), noted here for the user.

## 3. Gold cost `[C]`

```
cost = T[L] · price_index[town] · (20 − CHA)² / 100       # 32-bit; T = DS:0xb6c (u16 per level)
cost = min(cost, 15000); cost = max(cost, 5)
```

- `T[1..15]` = 1, 5, 11, 21, 34, 52, 72, 97, 126, 158, 195, 237, 282, 332, 387 `[C]` bytes — ≈ L^2.2 (DOS I computes
  `pow(L, 2.2)`: 1, 4, 11, 21, 34, 51 …; III's table rounds up in four places). For L 16–20 the index runs into the
  `G` table (`T[16]` = 180, `T[17]` = 0, `T[18]` = 324, `T[19]` = 0, `T[20]` = 583) `[C]` — a level-16+ character pays
  very little or the 5-gold minimum (data overrun, not a rule).
- `price_index` = DS:`0x10fc` (u16 per town, indexed by the current town word `FAR:0xdd98`; Pendragon = town 1 → 150)
  `[C]` table, `[~]` town numbering of the other entries.
- `(20 − CHA)` is **not** clamped at 0: CHA 21 gives 1, CHA 22 gives 4 (DOS I clamps at 0) `[C]`.
- No race term. The manual's "random creatures … pay much larger training fees" (PDF 3 / pr.1) is explained in the
  manual itself by their low Charisma (PDF 4 / pr.3: "Another reason why yucky random creatures pay more for
  training"); in the code it comes only from CHA. Not raised.
- "has gained enough experience to increase a level. It will cost *cost*"; the party treasury (`FAR:0x9de`, u32) must
  hold `cost`, else "The party cannot afford it" (nothing taken). Then "Do you wish to train, now?" Yes / No.

## 4. Level-up `[C]`

On Yes:

```
level += 1; level = min(level, 21)
party gold −= cost
dice = HPDICE[class][level]                 # DS:0xa77 + 22·class + level (byte)
roll = sum of dice × rnd(3)                 # rnd(n) = rand() % n + 1, so 1..3 each
if roll <= HP max (+0x20): roll = HP max + 1
gain = roll − HP max
HP max = HP current (+0x22) = roll          # training also refills HP
print "Hit Points increase by" gain
then 3 skill trainings (section 5), counter shown as "[original text]"
```

HP dice per class and level (DS:`0xa78 + 22·class + (level − 1)`) `[C]`:

| Class | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15–22 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Fighter | 5 | 10 | 16 | 22 | 28 | 35 | 41 | 47 | 53 | 60 | 65 | 70 | 75 | 80 | 85 |
| Thief | 3 | 5 | 9 | 11 | 16 | 20 | 24 | 27 | 30 | 35 | 39 | 43 | 47 | 51 | 55 |
| Priest | 4 | 6 | 10 | 15 | 20 | 25 | 30 | 35 | 40 | 45 | 49 | 53 | 57 | 61 | 65 |
| Ranger | 4 | 9 | 14 | 20 | 25 | 30 | 36 | 42 | 48 | 55 | 60 | 65 | 70 | 75 | 80 |
| Monk | 4 | 5 | 9 | 11 | 16 | 22 | 27 | 31 | 35 | 40 | 44 | 48 | 52 | 56 | 60 |
| Wizard | 3 | 4 | 6 | 8 | 11 | 14 | 17 | 19 | 22 | 25 | 28 | 31 | 34 | 37 | 40 |

- Twice each value is exactly the manual's "Hit Points by Class and Level for Adventurers with 10 Constitution" chart
  (PDF 8 / pr.11; Fighter 10, 20, 32 … 170; Wizard 6, 8, 12 … 80), i.e. the chart is the average of `dice × rnd(3)`.
- **Constitution plays no part** `[C]`: neither training nor creation (ovl2 `0x227d`: `dice(level 1) × (rnd(2) + 1)`,
  2–3 per die) reads CON. The direct and record-field reads of CON found by search are: the Swimming seed (root
  `0xa1f2`), the sheet, the I → III transfer, the elemental records, Resurrection (−rnd(2)), two dungeon events (−1)
  and a cap at 22 (root `0x8612`); none feeds HP. The manual: "The number of hit points an adventurer has is based
  on his Level, Class, and Constitution" (PDF 8 / pr.11) and CON is "a modifier on the number of Hit Points" (PDF 4 / pr.3) — **D3-022**.
- HP max is re-rolled from scratch each level (as DOS I); a low roll still gives +1.
- Current HP is set to the new maximum. The manual tells the party to go "into the Inn to rest up their Hit Points to
  their new maximum" (PDF 5 / pr.5) — advice, not a rule; noted only.
- **No gold for the new level** `[C]`: training writes no bank or party gold; the only writers of the bank field
  `+0xfe` are creation (class · 256), the transfer (+ old ÷ 35), the bank and Drop Member (ovl2 `0x3c92`: the dropped
  character takes party gold ÷ party size). The manual: social class "determines how much gold he gets to start and
  how much he gets for going up a level" (PDF 5 / pr.4; App. II PDF 14 / pr.23) — **D3-023**.
- Magic is not touched: the magic ceiling `+0x1a` follows the new level at the next inn stay (`../FORMAT.md` section 5).
  New spells come from Learn Spells (ovl2 `0x1a6c`, not specced here).

## 5. Skill training `[C]` (ovl2 `0x16d0`, `training_skills`, called three times)

Each call first re-seeds the skill bonuses from the current attributes and level (root `0xa1f2`, `../FORMAT.md` section 5:
2·attr + 30, Fire Bow 2·Level + 4·DEX + STR + 10, +30 Thief, +15 Elf), then shows all nine skills with Now / After
and lets the player pick one (highlight-bar menu; an Esc / out-of-range answer leaves the skills unchanged but still
uses up the pick `[~]`). Remaster: the Guild's training dialog asks the three picks after AGREE
(`GameSession.TrainingSkillRows` shows Now / After, picks applied in order, Esc = a used-up pick). **Screen order**
`[C]` static (ovl2 `0x18b0…0x19b7`): row i = name table `DS:0x1438`[i] with its Now / After, and the menu answer i
picks row i: Attack, Parry, Swimming, Listen, Find Trap, Disarm Trap, Find Item, Pick Lock, Fire Bow. Built
(`P3Training.ScreenOrder`, used by the rows and the Client's digits).

```
L' = min(level, 15)                         # the new level
for skill k in Attack, Parry, Swim, Listen, Find Trap, Disarm, Find Item, Pick Lock:
    after[k] = (C_k[class][L'] + now[k]) / 2 + seed[k] − 49      # '/ 2' is an arithmetic shift
after[FireBow] = now[FireBow] / 2 + seed[FireBow] − 49            # no class table
after = clamp(after, 1, 250)
chosen skill := after                       # only the chosen one changes
```

- There is **no "never decrease"** test: a pick lowers the skill when `now > C + 2·(seed − 49)`. The screen shows the
  After value, so the player can see it.
- **Diminishing return** (the manual's "advantage gained is decreased each time"): repeated picks move the skill half
  way towards the fixed point `C + 2·(seed − 49)` (Fire Bow: `2·(seed − 49)`), so each repeat gains half the last.
- Creation (root `0x9ea0`, called by ovl2 `0x227d`) sets every skill to the same formula with `now = 0`:
  `C/2 + seed − 49` (min 1), Fire Bow `seed − 49` (`initial_skills` in `../FUNCTIONS.md`).
- Skill caps: 250 here; the manual's Accuracy text names 250 for Fire Bow too.

Class tables `C_k[class][L]` = byte at `DS:base + 15·class + L` (L 1–15) `[C]`. For a Wizard at L 15 the index runs
into the next table's byte 0 (shown in the last column as read). Odd entries kept as data: Ranger Attack L15 = 25,
Thief Pick Lock L3 = 35, Monk Disarm falling with level, Wizard Find Item L13 = 8.

Attack (DS:`0xc53`)

| Class | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Fighter | 100 | 110 | 120 | 135 | 145 | 155 | 165 | 180 | 190 | 200 | 210 | 220 | 235 | 245 | 255 |
| Thief | 70 | 75 | 80 | 85 | 90 | 95 | 100 | 105 | 110 | 115 | 120 | 125 | 130 | 135 | 140 |
| Priest | 80 | 90 | 100 | 110 | 120 | 125 | 135 | 145 | 155 | 165 | 175 | 180 | 190 | 200 | 210 |
| Ranger | 90 | 95 | 100 | 115 | 125 | 135 | 145 | 160 | 170 | 180 | 190 | 200 | 215 | 235 | 25 |
| Monk | 80 | 85 | 95 | 100 | 105 | 110 | 120 | 125 | 130 | 140 | 145 | 150 | 155 | 160 | 170 |
| Wizard | 70 | 70 | 75 | 75 | 80 | 80 | 85 | 85 | 90 | 90 | 95 | 95 | 100 | 100 | 125 |

Parry (DS:`0xcad`)

| Class | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Fighter | 50 | 55 | 55 | 55 | 60 | 60 | 60 | 65 | 65 | 65 | 70 | 70 | 70 | 70 | 70 |
| Thief | 50 | 50 | 55 | 55 | 60 | 60 | 65 | 65 | 65 | 65 | 65 | 65 | 65 | 65 | 65 |
| Priest | 50 | 50 | 50 | 50 | 55 | 55 | 55 | 55 | 55 | 60 | 60 | 60 | 60 | 60 | 60 |
| Ranger | 60 | 60 | 60 | 60 | 65 | 70 | 75 | 80 | 85 | 85 | 85 | 85 | 85 | 85 | 60 |
| Monk | 60 | 65 | 65 | 70 | 70 | 75 | 75 | 80 | 85 | 90 | 95 | 100 | 105 | 110 | 40 |
| Wizard | 45 | 50 | 55 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 0 |

Swim (DS:`0xd07`)

| Class | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Fighter | 42 | 50 | 58 | 67 | 75 | 84 | 84 | 84 | 84 | 84 | 84 | 84 | 84 | 84 | 84 |
| Thief | 42 | 48 | 55 | 62 | 68 | 75 | 82 | 89 | 89 | 89 | 89 | 89 | 89 | 89 | 89 |
| Priest | 42 | 46 | 50 | 54 | 58 | 62 | 67 | 71 | 75 | 79 | 79 | 79 | 79 | 79 | 79 |
| Ranger | 42 | 50 | 58 | 67 | 75 | 84 | 84 | 84 | 84 | 84 | 84 | 84 | 84 | 84 | 84 |
| Monk | 42 | 47 | 53 | 59 | 65 | 71 | 77 | 84 | 84 | 84 | 84 | 84 | 84 | 84 | 84 |
| Wizard | 42 | 45 | 48 | 52 | 55 | 58 | 62 | 65 | 68 | 72 | 75 | 78 | 78 | 78 | 78 |

Listen (DS:`0xd61`)

| Class | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Fighter | 0 | 0 | 0 | 4 | 4 | 4 | 7 | 7 | 7 | 10 | 10 | 10 | 14 | 14 | 14 |
| Thief | 31 | 38 | 45 | 51 | 58 | 65 | 71 | 78 | 85 | 91 | 98 | 105 | 111 | 118 | 138 |
| Priest | 0 | 0 | 4 | 4 | 7 | 7 | 10 | 10 | 14 | 14 | 17 | 17 | 20 | 20 | 24 |
| Ranger | 0 | 4 | 7 | 10 | 14 | 17 | 20 | 24 | 27 | 30 | 34 | 37 | 40 | 44 | 144 |
| Monk | 31 | 31 | 35 | 35 | 38 | 38 | 41 | 45 | 45 | 48 | 48 | 51 | 51 | 55 | 55 |
| Wizard | 0 | 0 | 0 | 0 | 4 | 4 | 4 | 4 | 7 | 7 | 7 | 7 | 7 | 10 | 14 |

Find Trap (DS:`0xdbb`)

| Class | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Fighter | 5 | 5 | 5 | 5 | 10 | 10 | 10 | 10 | 15 | 15 | 15 | 15 | 20 | 20 | 20 |
| Thief | 60 | 60 | 60 | 60 | 65 | 65 | 65 | 65 | 70 | 70 | 70 | 70 | 75 | 75 | 75 |
| Priest | 5 | 5 | 5 | 5 | 10 | 10 | 10 | 10 | 10 | 15 | 15 | 15 | 15 | 20 | 20 |
| Ranger | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 65 |
| Monk | 35 | 35 | 35 | 35 | 35 | 35 | 35 | 35 | 70 | 70 | 70 | 70 | 95 | 95 | 95 |
| Wizard | 5 | 5 | 5 | 5 | 5 | 10 | 10 | 10 | 10 | 10 | 15 | 15 | 15 | 15 | 15 |

Disarm Trap (DS:`0xe15`)

| Class | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Fighter | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |
| Thief | 60 | 54 | 54 | 54 | 54 | 54 | 54 | 54 | 54 | 57 | 57 | 57 | 57 | 57 | 60 |
| Priest | 8 | 8 | 8 | 8 | 8 | 8 | 8 | 8 | 8 | 8 | 8 | 8 | 8 | 8 | 8 |
| Ranger | 8 | 8 | 8 | 8 | 8 | 11 | 11 | 11 | 11 | 11 | 14 | 14 | 14 | 14 | 47 |
| Monk | 47 | 40 | 37 | 34 | 34 | 31 | 27 | 27 | 24 | 24 | 21 | 14 | 8 | 1 | 4 |
| Wizard | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |

Find Item (DS:`0xe6f`)

| Class | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Fighter | 0 | 0 | 0 | 0 | 4 | 4 | 4 | 4 | 8 | 8 | 8 | 8 | 12 | 12 | 24 |
| Thief | 37 | 37 | 41 | 41 | 45 | 45 | 49 | 49 | 53 | 53 | 57 | 61 | 65 | 69 | 77 |
| Priest | 0 | 0 | 0 | 4 | 4 | 4 | 8 | 8 | 8 | 12 | 12 | 12 | 16 | 16 | 24 |
| Ranger | 8 | 8 | 8 | 8 | 8 | 12 | 12 | 12 | 12 | 12 | 16 | 24 | 32 | 40 | 48 |
| Monk | 13 | 13 | 17 | 17 | 21 | 21 | 25 | 25 | 29 | 29 | 33 | 33 | 37 | 37 | 41 |
| Wizard | 0 | 0 | 0 | 0 | 0 | 4 | 4 | 4 | 8 | 8 | 12 | 12 | 8 | 20 | 24 |

Pick Lock (DS:`0xec9`)

| Class | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Fighter | 2 | 2 | 2 | 2 | 2 | 4 | 4 | 4 | 4 | 4 | 6 | 6 | 6 | 6 | 6 |
| Thief | 51 | 53 | 35 | 56 | 58 | 60 | 62 | 64 | 65 | 67 | 69 | 71 | 72 | 74 | 76 |
| Priest | 4 | 4 | 4 | 4 | 4 | 5 | 5 | 5 | 5 | 5 | 6 | 6 | 6 | 6 | 6 |
| Ranger | 6 | 6 | 6 | 6 | 6 | 6 | 6 | 6 | 6 | 6 | 6 | 6 | 6 | 6 | 13 |
| Monk | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 |
| Wizard | 0 | 0 | 0 | 0 | 0 | 3 | 3 | 3 | 3 | 3 | 6 | 6 | 6 | 6 | 6 |

## 6. Manual comparison

| Manual | Code | |
|---|---|---|
| Train three skills per level, the same skill more than once with less gain each time (PDF 5 / pr.5) | 3 picks, half-way-to-target rule | agrees |
| Told the XP still needed, or the gold cost; gold from the party treasury (PDF 5 / pr.5) | as stated | agrees |
| CHA affects the training price (PDF 4 / pr.3) | `(20 − CHA)²` factor | agrees |
| HP from level, class and CON (PDF 8 / pr.11, PDF 4 / pr.3) | class and level only | **D3-022** |
| Gold for each level by social class (PDF 5 / pr.4, PDF 14 / pr.23) | none | **D3-023** |
| XP needed depends on race, attributes and class (PDF 6 / pr.7) | class and attributes (+ score) | not raised (race only through attributes) |
| Random creatures pay larger fees (PDF 3 / pr.1) | through low CHA only | not raised (manual's own explanation) |
