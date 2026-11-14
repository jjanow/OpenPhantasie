# Phantasie III — body areas (injury, status, healing)

Phase 09 T4 3c.6. Static unless marked live. Cross-game module: III adds body areas to I's HP model.
Source of truth: the III manual (PDF 7 / pr.8, PDF 9 / pr.12–13, App. II PDF 14–15 / pr.23–24; summary in
`docs/plan/09-trilogy-manual-p3.md` section 4). Code: DOS `MAIN.EXE` (addresses: `../FORMAT.md` section 1, functions:
`../FUNCTIONS.md`). Tags: `[C]` read from code / data bytes, `[~]` probable, `[?]` not decoded. Manual ↔ code
conflicts are in `DIFFERENCES-III.md` (D3-0nn); they are not decided here.

## 1. Data

- **Characters:** six words at record `+0xf2 + 2k` `[C]`. **k: 0 Head, 1 Left Arm, 2 Right Arm, 3 Torso, 4 Left Leg,
  5 Right Leg** (sheet labels, string 456, and the injury messages agree). Value = state: **0 Okay, 1 Injured, 2 Broken,
  3 Gone** (strings 146–149) `[C]`.
- **Monsters:** each monster group in combat (far record; word 0 = monster id, HP of member m at `+4 + 2m`, status at
  `+0x18 + 2m`, up to 5 members) holds six areas per member at `+0x4c + 10k + 2m` `[C]`. **Monster k order differs:
  0 Head, 1 Torso, 2 Right Leg, 3 Right Arm, 4 Left Leg, 5 Left Arm** (root `0xe6c6` messages) `[C]`.
- There is **no HP per area** (manual Q8): one HP pool, and the area state is set from the size of the hit (section 2) `[C]`.
- Hit-location odds: `DS:0x12cc`, 5 rows × 6 **cumulative** words, indexed by the monster k order above `[C]`:

| Row | Used for | Head | Torso | R Leg | R Arm | L Leg | L Arm | (cumulative) |
|---|---|---|---|---|---|---|---|---|
| 0 | character's **bow** hit on a monster | 69 % | 11 % | 4 % | 7 % | 0 % | 9 % | 69 80 84 91 91 100 |
| 1 | `[~]` spells (not traced to a caller) | 17 % | 16 % | 16 % | 17 % | 29 % | 5 % | 17 33 49 66 95 100 |
| 2 | character's **melee** hit; **every monster hit on a character** | 3 % | 8 % | 24 % | 14 % | 31 % | 20 % | 3 11 35 49 80 100 |
| 3 | character's **Aim** blow | 40 % | 17 % | 9 % | 14 % | 9 % | 11 % | 40 57 66 80 89 100 |
| 4 | any hit on a monster while the "tall monster" flag is set | 0 % | 5 % | 17 % | 30 % | 18 % | 30 % | 0 5 22 52 70 100 |

  For a character the row is mapped through the same k order (head, torso, right leg, right arm, left leg, left arm)
  onto record areas 0, 3, 5, 2, 4, 1 `[C]` (root `0xe9ee`).
- **"Tall monster" flag** `0x27f3:0x0cc2` `[C]`: set at the start of a fight (root `0x104c0`) when the **first monster
  group's** id is 44, 67 or 68 (Dark Gnome, Storm Giant, Cloud Giant in `extracted/monsters.json`); while set, every
  hit on a monster uses row 4 regardless of the weapon. 44 is probably meant to be 43 Valley Giant `[~]` (a code typo is
  not a manual conflict; it is noted for the remaster's data). Row 3 has an extra `rnd(30)` roll when the flag is set,
  but the flag also forces row 4 first, so that branch is dead code `[C]`.

## 2. Taking an injury `[C]`

Called after HP damage, by `damage_character` (root `0xbbb6`, only when damage > 0 and the hit is not flagged
"no injury") and `damage_monster` (root `0xb116`, same condition):

```
hp_left  = max(0, HP − damage)               # HP after the hit
q        = hp_left / max(1, damage)          # integer division
severity = {0: Gone(3), 1: Broken(2), 2: Injured(1)}.get(q, none)
if severity == none: return                  # small hits never injure
roll = rnd(100)                              # 1..100 (root 0x2c31 = rand() % n + 1)
k    = first k with odds[row][k] >= roll     # 0..5; 5 if none
if area[k] < severity:                       # areas only get worse, never by steps
    area[k] = severity
    print name + "'s <area> " + {3: "is removed!", 2: "breaks!", 1: "is injured!"}
```

- A killing blow (hp_left 0) always removes an area.
- Severity does not accumulate: two "Injured" hits on the same area leave it Injured.
- Monsters whose id is 2, 6, 17, 27, 29, 39, 40, 41, 51, 52, 62 or 65 (snakes, slimes, blobs, moulds, Viper Moth) have no
  limbs (`0xe98c`): the message says "'s body … is injured!" for every severity, but the area word is still set as above.

## 3. What an area state does

**Status from the body** (characters: root `0x107b`; monsters: root `0xec1a`; recomputed after every hit and heal) `[C]`:

| Test, in order | Character status | Monster status |
|---|---|---|
| HP ≤ 0 or status already Dead | Dead (0) | dead (0) |
| Head or Torso **Gone** | **Dead** (0) | dead (0) |
| Head or Torso **Broken** | **Out** (3) | 4 (Unconscious; the combat list prints **"O"**, manual "U": D3-021, `encounters.md` section 2) `[C]` |
| already Asleep | Asleep (2) | 2 (Sleeping) |
| Left or Right Leg ≥ Broken | **Down** (4) | 3 (Down) |
| otherwise | Okay (1) | 1 (Okay) |

The manual's death rule "Head or Torso removed" `[C]` matches; "enough hit points to an arm to kill him" has no
separate rule: an arm is only removed by a hit larger than the HP left, which also kills (HP 0).

**Death step** (`damage_character` root `0xbbb6`) `[C]`:

```
hp_left = max(0, HP − damage)
if damage > 0 and the hit may injure: take_injury (section 2); s = status_from_body (table above)
    if s is Dead or Out, or the member is not Asleep: status = s      # a non-lethal hit leaves a sleeper Asleep
HP = hp_left
if HP < 1 or status == Dead:
    HP = 0; status = Dead; "<name> dies!"; redraw the portrait (root 0x1714)
```

- There are **no HP thresholds** for Out or Down: HP 0 is death; Out / Down come only from the body areas (table above).
- **No Constitution loss at death** (unlike Phantasie I's OV2 `0x12cf`, DIFFERENCES.md D-009): no code on the death
  path writes CON. The only CON writers in the program are Resurrection, the Astral Plane (`planes.md` section 6: −1 on
  being raised or turned Undead, root `0x20e0` / `0x213b`), a cap at 22 (root `0x8622`) and creation.
- **Resurrection** (root `0x18b6`; callers spell #44 out of combat root `0x8f62` and root `0xcf10` in combat code `[~]`;
  target must be Dead): HP = 1, magic current = max, status Okay, **CON −rnd(2)** (1–2), all six areas Okay, then CON raised to 3 if below. This matches the manual
  ("Resurrection … CON permanently −1 or −2", III PDF 11 / pr.16), so III follows the manual where I's DOS did not.
- So Phantasie I's death step (status DEAD + CON loss at death) must **not** be used for III.

**Combat restrictions** `[C]` (root `0x10be4`, `0xb550`, `0xfc41`, `0xc03b`; full action rules in
`combat-actions-and-ranks.md`):

- Right Arm ≥ Broken (Broken **or Gone**) → the action is forced to **Parry** (no swing, no spell, no bow).
- Any arm or leg ≥ Broken (areas 1, 2, 4, 5) → **Fire Bow** becomes **Thrust** (checked when chosen and again at the
  start of the round). Status Down (a leg ≥ Broken) also turns Fire Bow into Thrust.
- The Left Arm alone blocks only the bow; Head / Torso act through status (Out / Dead).
- Armour points (record `+0x6a`) also soak breath: damage = `rnd(36) − armour points` (min 1 armour value) (root
  `0xb582`, `0xd730`); breath never injures an area `[C]`.
- Down attackers get −40 to hit in melee; hits on any target that is not Okay (Asleep, Out, Down, Unconscious) **always
  land** (chance set to 100) — D3-019.

## 4. Healing `[C]`

`heal_body_areas(P)` (root `0xaafc`) spends P cure points on a copy of the record:

```
remaining = P; level = P
while level > 0:
    found = false
    for k in 0..5:                       # Head, L Arm, R Arm, Torso, L Leg, R Leg
        if area[k] == level:
            area[k] = Okay               # cured outright, never one step down
            remaining -= level
            level = remaining
            found = true
    if not found: level -= 1
status = body status (table above, without the HP / Asleep tests)
```

So an area costs its state in points (Gone 3, Broken 2, Injured 1) and the worst affordable area goes first. After a
cure the level drops to the points left **within the same pass** (`0xab3b…0xab44`), so the scan goes on from the next k:
with 4 points, Head Injured, Left Arm Gone, Left Leg Injured, DOS cures the arm and the leg, not the Head. Otherwise
equal states go by lower k (Head, Left Arm, Right Arm, Torso, …). D3-031 (user ruling: the manual, most vital first):
the remaster starts over after each cure, Head, Torso, Arms, Legs `[~]`; this DOS pass is option `p3.heal-order`.
Points left over smaller than every remaining area are lost. At level 0 the loop also "cures" Okay areas (no effect).

| Source | P | HP | Tag |
|---|---|---|---|
| Healing I / II / III / IV (spells #1–4, root `0x194d`) | 1 / 2 / 3 / 4 | + rnd(9 / 27 / 57 / 99), capped at max (table `DS:0xc1a`) | `[C]` |
| Healing Potion 1–3 / 4–6 / 7–9 / 10 (items 101–110, root `0x94a5`) | 1 / 2 / 3 / 4 | + n² for potion n, capped | `[C]` |
| Wilderness inn (ovl2 `0x347d`, in-town flag clear) | 2 | full | `[C]` (live s109, `screens-s109/`) |
| **Town inn** (in-town flag `FAR:0xe596` set) | every Injured and Broken area → Okay; Gone stays | full | `[C]` |
| Resurrection (#44, root `0x18b6`) | all six areas → Okay | 1 | `[C]` (also CON − rnd(2), min 3) |
| `clear_body_areas` (root `0x1dbb`, dungeon events / endgame) | all → Okay | ≥ 1 | `[C]` |

Manual comparison: tiers I–III equal the manual's table (1 Injury; 1 Break or 2 Injuries; 1 Gone or Break + Injury or
3 Injuries). **Tier IV gives 4 points**, i.e. at most one Gone (+ one Injury), where the manual says "more than 1
Gone" — **D3-014**. **Town inns** heal every Injured and Broken area, where the manual says an inn "will cure 1 Break or
2 Injuries"; the wilderness inn matches it — **D3-015** (manual Q18: rest fixes Breaks too).

Every inn stay also: age clock +1, magic current = max, and (if no inn stay since the last fight) a chance to raise
magic max (`FORMAT.md` section 5) `[C]`.

**The inn routine** (ovl2 `0x347d`, static `[C]`), town and wilderness alike: the title (" Inn welcomes you." with
the town / place name in a town, "The party's health improves!" outside), then for each party slot in order: the name and
"is resting."; a **Dead** member (status +0x2c = 0) gets " in peace." and nothing else (no age, magic or HP); a living one:
age clock +0x28 +1; the magic ceiling +0x1a := `DS:0xafb[15·class + min(level, 15)]` (level +0x74, class +0x94: the
manual's Maximum Magic Points table for the member's level, refreshed at every stay); when the rested flag `FAR:0xde70`
is clear and max +0x1c < ceiling, `20 · (ceiling − max) > rnd(100)` (root `0x2c31`, 1…100) → "Magic increases!" and
max +1; then current +0x1e := max. In a town: HP +0x22 := max +0x20 and every area not Gone → Okay; outside: 2 cure points
(root `0x27bc(2)` on a copy of the record, with full HP). Then status := Okay and `0x107b` sets it from the body (for every
slot, the Dead too: what `0x107b` gives a Dead member with HP 0 is not read here `[~]`; the remaster keeps the Dead
dead). After the loop `FAR:0xde70` := 1. **`FAR:0xde70` is cleared at the start of every fight** (`combat_main` root `0x104c0`, the
clear at `0x1057c`; its other uses are the save / load copies `0x8142` / `0x8285`): DOS raises magic only on the first
inn stay after a fight, for every living member, whether or not they cast. The manual: "Characters who have cast spells
while in the wilderness may have their maximum number of Magic Points increased" — **D3-069**, decided for the manual.
The remaster: III's inns run this routine (`P3Inn`) with the gate "the member cast a spell outside town since the last
stay" (`Character.CastSinceRest`: a fight, the wilderness, a dungeon count; a town cast does not). In-town flag `FAR:0xe596` `[C]`: entering a place of type 1 (town) sets it
(ovl3 `0x6c8`); leaving town clears it (ovl3 `0x88`); a place of type 3 (independent inn, strings 45–52) charges
250 gold and calls the inn with the flag clear — the manual's 250-gold independent inn.

## 5. Open

- Row 1 of the odds table: which spells use it (`[~]` Fireflash / Flamebolt per the manual) `[?]`.
- Spell damage paths and the "no injury" flag users other than breath (`0xb582` passes it) `[?]`.
