# Phantasie IV — combat (spec, 3d.6)

Source: X68000 `P4.X`, static reading (addresses = P4.X image addresses; float code read in the disassembly with the
FPACK operand order of `../FORMAT.md` §6: the double pushed last is the left operand). Written source: PC-98 manual
ch. 6–7 pp. 39–55 and p. 32 (`docs/manuals/phantasie4/phantasie4-manual-en.md`), plus the spell list. Tags: `[C]` code,
`[~]` likely, `[?]` open. Not cross-checked on MSX2 (except +3B); no full-system emulator run. Manual↔code conflicts found here
are listed in §12 (to be filed in `DIFFERENCES-IV.md` as PENDING; no side taken).

Record offsets: `../FORMAT.md` §6.2 (party record, `_pmb` = `0x79708`, 190 B). Monster slots: `_mns` (`0x7AB06`, 70-byte
`DATM` records, `../FORMAT.md` §6.4), one record per monster **type** in the encounter (groups of up to 6 share it, so
anything a spell writes into `_mns` hits the whole group). Per-monster state: `_emhp` (HP), `_st` (1 asleep, 2 acted,
3 ready), `_ml` (6 wound levels), `_mc` (condition, §5), `_emmove`, `_emsplmv`, `_emstr`.

Body parts (`_body`, index 0–5) `[C]`: **0 head, 1 torso, 2 right arm, 3 left arm, 4 right leg, 5 left leg**; wound
levels (`_hurt`): 0 none, 1 light, 2 serious, 3 lost. The party record packs them into +0C (head in the top two bits).

## 1. Setup and round structure `[C]` (`_encount` `0x227B0`, `_set1` `0x20634`)

1. **Equipment (`_equip` `0x9024`)**, run when the party leaves town: for each of the 9 carried items the best usable
   one per slot is equipped (highest `_idat` b1 value; requirement b2 as `../FORMAT.md` §6.5):
   - shield (ids 1–8, b2 ≤ Str) → value +B6, id +64;
   - armour (9–0x1B, b2 ≤ Str) → value +B7, id +65;
   - weapon (0x1C–0x3F, b2 ≤ 2 Str + Dex) → value +6B, id +66;
   - bow (0x40–0x4B, b2 ≤ Str + Dex) → value +6A, id +67;
   - thrown weapon (0x4C–0x54, b2 ≤ Str + Dex) → value +69, id +68.
   The thrown weapon's own value +69 is only displayed: **thrown damage uses the bow value** (§3.2) `[C]`.
2. **Per member at the start of a fight (`_set1`):**
   - melee damage **Wm** (+6E) = +6B + +3B. +3B is set when the party leaves a town (`_exittown` `0x5B3E`–`0x5C88`):
     +3B = trunc(trunc(Str / 6) + |class − 3.5| × level / 11) `[C]` — odd as it looks (Thieves and Gargoyles get the
     largest level term, Fighters / Priests the smallest), confirmed s82 by running the real P4.X + FLOAT2 code
     (`tools/scripts/x68k_p4_cpu_harness.py melee`, 0 mismatches over classes 0–7 × levels 1–30 × Str 3–25) and the MSX2
     seg `1000` code (`msx2_p4_z80_harness.py melee`, identical). At Str 18 Thieves and Gargoyles go from 3 at level 1
     to 12 at level 30, Fighters and Priests from 3 to 4;
   - melee hit **Hm** (+70) = +3C (skill 0, Attack); missile damage **Wr** (+6D) = +6A (bow value); missile hit **Hr**
     (+71) = +48 (skill 6, Fire Bow);
   - armour **A** (+6F) = +B7 (armour value); temporary strength bonus `_epstr` = 0;
   - base MV `_epmovt` = `_calmove(member, 0)` (§7);
   - **defence D** (+35) = trunc(Parry (+3E) / 8) + 10 × flier (+B8) + 3 × base MV + Dex, plus trunc(shield value / 6)
     (if the first sum is < 1 only the shield term is kept). D is what monsters subtract from their hit chance (§4).
3. **The field and the monsters** (s98, §1.1): `_e_terrain`, `_enc_mons`, `_echkdisp`.
   Then, until the fight ends: options (`_enc_opt`, §8), then **three phases** p = 1, 3, 5. In each phase every party
   member who has not acted this round may act (`_enc_patk(p)` → `_chrexe(member, p)`), then every monster
   (`_enc_matk(p)` → `_monexe`, §4). `_reset_act` clears the "acted" flags at the end of the round.
   **Surprise:** `_einf1` = 1 (the party surprised the monsters) skips the monsters' turns; `_einf2` ≠ 0 (the monsters
   surprised the party) skips the options and the party's turns (§10). `_einf1` works through `_enc_mons`: every monster
   starts with `_st` = 3 − `_einf1` (2 = already acted) and `_mc` = 1 (asleep) only when `_einf1` = 2, a value no caller
   passes (`_oencgo` gets (1, 0) for both "attack the sleeping monsters" and "you took them by surprise") `[C]`. Both
   flags are cleared after round 1's phases (`_encount` `0x227EE`) `[C]` (s98).
   The loop (`_encount` `0x227B0`): `_enc_opt` unless `_einf2`; then while `_eexitf` is 0: phases 1, 3, 5 (each
   `_enc_patk(p)` then `_enc_matk(p)`, stopping early when one returns non-zero), `_bturn` + 1, flags cleared,
   `_enc_opt`, `_reset_act`. After an end other than 1: `_enc_end`; every member with condition 2 gets `_pc` = 3 and
   `_setpc` again, which recomputes it from the condition, so **a sleeper is still asleep after the fight** `[C]`.
   **Party movement** happens in `_enc_opt` (`_b_chmove`): each member is moved on the field, then given his action
   (`_opt_set` / `_opt_get`); the actions run later in the phases (manual pp. 41–43 agree).

### 1.1 Field, monsters and placement (s98 static reading) `[C]`

- **Squares** 0–48 = 7 × row + column, row 0 at the top (`_opn_field` draws column = sq mod 7, row = sq / 7). `_pos`
  holds 4 bytes per square: obstacle kind (0xFF none), walkable, saved walkable, occupant (0xFF none).
- **Battlefield** (`_e_terrain` `0x26E0C`): on a location map only the background (`_e_finit`); outdoors by the terrain
  type of the party's square (tile / 10): 0, 1, 13 ship (`_e_on_ship`, also sets `_shipf`), 2 desert, 3 and 4 hills,
  5 forest, 6 grassland, 8 and 12 mist, 11 Netherworld; 7, 9, 10 and the places call no routine (no encounter there).
- **Obstacles** (`_e_obstacle(kind, count, passable)` `0x266C8`): count times a free square rand(35) + 6 (7–41), retried
  until free. Calls (kind, count, passable): ship (1, rand(6) + 1, yes); desert (2, rand(2), yes), (3, rand(4) + 2, no),
  (4, rand(2), yes); hills (5, rand(2), yes), (6, rand(3) − 1, yes), (7, rand(6), no); forest (8, rand(11) + 4, yes),
  (9, rand(11) + 4, no), (6, rand(5), yes); grassland (2, rand(6) − 1, yes) ×2 with kinds 2 and 6; mist (10, rand(25) − 1,
  no); Netherworld (5, rand(5) − 1, yes), (7, rand(5) − 1, yes), (10, rand(3) − 1, no). A blocking obstacle can only be
  flown over. Tables in `rule-tables.json` `combat.battle`.
- **First monster type outdoors** (`_emons`, set by the `_e_on_*` routine; this closes "which group an outdoor fight
  uses" `[?]`): desert rand(4) − 1 + 12 A + 8; hills rand(10) − 1 + 12 A + 2; forest rand(10) − 1 + 12 A; grassland
  rand(8) − 1 + 12 A + 2 (A = the map's area level `_al`); mist / Netherworld rand(12) − 1 + 60, then with rand(100) < 50
  rand(2) − 1 + 60; ship rand(100) < 65 → 76 Swashbuckler, else 75 Pirate. On a location map `_denc` names the type.
- **Groups** (`_enc_mons` `0x20D54`): up to **3** groups (the loop stops at the third). Group size rand(n), n = +14 —
  **but n = the party's average level `_par`+0B when +14 > +0B + 1** (FORMAT.md had "party size + 1"; `0x789D3` is +0B,
  the member count is +0C). Each monster: HP (D4-055), `_st`, `_mc`, wounds 0, gold `_emgold` += +2A, MV +38, Speed /
  Strength 0. After a group, another follows on rand(100) ≤ +34, of type companions[rand(2) − 1]. **`_emlevel` and
  `_emnice` are the means of the groups' +1E / +28** (summed once per group, divided by the group count at `0x21332`;
  level at least 1) — not the sum over the monsters. Outdoors the **last** group's +3A / +3C / +3E are the treasure ids.
- **Placement** (`_echkdisp` `0x24D4C`): members on the bottom row first (square 48 − c), then the rows above, monsters
  on row 0 (square c) then downward, each row by the columns c = 3, 2, 4, 1, 5, 0, 6 (table `0x44508`). A monster needs
  a walkable square; members ignore obstacles. Members with condition 0 (dead) are not placed.
- **Exits** (`_init_edge` `0x26FF4`): outdoors all 24 edge squares are exits; on a location map or a ship one random
  side exit (rand(14) − 1 of the pairs at `0x44670`: the 14 squares of columns 0 and 6) and every other edge a wall.
- **Movement helpers** `[C]` (s102): `_b_range` = squared distance; `_b_friend(0)` = the nearest occupant of the other
  side (`_tmark`+0B: 1 party, 2 monsters; first in square order on a tie; `_b_friend(1)` the same side); `_b_getnear` =
  the nearest open exit (it tests the walk byte of square *i*, not of exit *i*, original slip `[C]`); `_center(side)` =
  the mean row × 7 + mean column of that side's squares (integer division); `_faraway(sq)` = the free walkable square
  farthest from `sq` (first in square order). The walk itself is §7.1.
4. **When a member acts** (melee and most spells; bow, throw and spells 5–8 skip this test, so they resolve in phase
   1): only if rand(100) − 1 ≤ 5 p² + 3 × level. So phase 1 needs ≤ 5 + 3L, phase 3 ≤ 45 + 3L, phase 5 always
   passes: higher levels tend to strike earlier in the round.
5. A **sleeping / paralysed** member (condition 2) wakes ("Wake Up!") if rand(100) < 16 + 32 × (number of wounded body
   parts) (rand returns 1..n, so a (15 + 32 k) % chance), and in any case does nothing that round.
6. A member whose **right arm** is seriously wounded or lost (wound ≥ 2 on part 2) has his action replaced by Parry
   before melee or spells resolve (bow and throw are tested earlier, §2) `[C]` (`_chrexe` `0x28322`).

## 2. Actions and what the menu offers `[C]` (`_opt_set` `0x1D5EC`, `_opt_get` `0x1D9A2`)

Action codes (`_epatk`): 0x0B Aim Blow, 0x0C Parry / none, 0x0D Lunge, 0x0E Attack, 0x0F Thrust, 0x10 Slash, 0x11 Fire
Bow, 0x12 Throw Weapon, 0x14 Cast Spell, 0x15 / 0x16 Fly / Land.

Offered in manual mode (`_opt_set`), only while the member's combat state `_pc` is 3 (§5) and the right arm is < serious:
- **Throw / Fire Bow:** if the left arm is also < serious and a thrown weapon (+68) / bow (+67) is equipped.
- **Melee:** for reach r = 1, then 2: if an enemy is within r squares:
  - Fighter (class 3): **Lunge**; at r = 2 a Fighter gets Lunge only;
  - everyone in reach: **Thrust**, **Aim Blow**; **Attack** if Hm ≥ 75; **Slash** if Hm ≥ 150.
  - Only Thieves (class 0) and Fighters look at r = 2, so **a Thief's melee reaches 2 squares**.
- **Cast Spell** always; **Fly / Land** for a flier (+B8) whose arms (wings) are unwounded.
- **Parry** always.

Reach in `_opt_get`: 1 for melee, 2 for Lunge and Thief melee, 6 for bow, throw and spells.

- **Parry** (`_opt_get` 0x0C): the member is marked as parrying (`_epopt` = 1) if rand(100) < Parry skill (+3E) **or**
  his left leg (part 5) has a wound ≥ 2 (the code's test, not an error of reading). Effect: §4.3.
- **Slash** stores n = trunc(Hm / 75) + 1; with Slash only offered from Hm 150 and Hm ≤ 255, n is **3 or 4**.
- Spells that need a target: Healing 1–4 and Resurrection ask for a party member; Fire Flash 1–4, Summon El. and
  Shock Wave / Flameball (51–58) ask for a square (`_b_chmove` `0x1DD72`).

## 3. Party weapon attack resolution (`_chrexe` `0x28322`, `_chatkp` `0x27E18`, `_chatkd` `0x27920`) `[C]`

**One hit roll and one damage roll per action** for every weapon action, Attack and Slash included: `_chrexe` makes one
`_chatkp` / `rand(100)` / `_chatkd` pass, `_f2f` (`0x297D6`) is only the facing / shot / flash animation, and
`_enc_patk` calls `_chrexe` once per member per round (the "acted" flag). See §12 item 1.

### 3.1 Hit chance

```
melee:   c = Hm                      (Slash: c = trunc(Hm / n), n = 3 or 4)
         c −= monster +1C (defence)  if +1C ≥ 0
         Aim Blow: c = max(0, c − 15);  Lunge: c = max(0, c − 30)
bow / thrown:  c = Hr − trunc(+1C / 4)  if +1C ≥ 0, else Hr
target _mc < 3 or = 4 (asleep, a leg or head/torso seriously hurt; §5):  c = 200
attacker _pc = 2 (a leg seriously hurt): c = trunc(3c / 4);  else off-hand attack: c = trunc(c / 2)   (_chlostp)
hit  if  rand(100) ≤ c
```

`_chcond(member, kind)` (`0x2810C`): bow (kind 2): impossible unless `_pc` = 3 and both arms < serious. Melee / throw
(1, 3): impossible if `_pc` ≠ 3 or both arms ≥ serious; **off-hand** ("Left Hand Attack!", damage × 2/3, hit ÷ 2) if
the right arm is ≥ serious and the left is not. (For melee the right-arm rule of §1.6 turns the action into Parry first,
so only a throw can actually be made with the left hand.)

### 3.2 Damage

```
melee:                u = (rand(100) / 100) ^ 0.5
                      d = trunc(u × Wm − monster +1A (armour)) + F
                      F: Thrust +1, Attack −1, Slash −n, Aim Blow / Lunge 0;   d ≤ 0 → 0
bare-handed Shadow Warrior (class 6, no weapon in +66):
                      d = (rand(rand(2 (L + 9)) − 1) − 1) + rand(rand(3 L − 1)) − armour;  d ≤ 0 → 0
                      if rand(100) < 25 (24 %): "A clean bare-hand strike!!", d += L + 10 (instead of the critical)
bow and thrown:       u = (rand(100) / 100) ^ 0.2
                      d = trunc(u × Wr × 1.2 − armour / 2) + K[class]     (Wr = the bow's value, also for throws)
                      d < 0 → 0, but d = 0 → 1 (original quirk)
off-hand:             d = trunc(2 d / 3)
then:                 d = min(255, d + _epstr)
```

K (class 0–9; bow table `0x4476E`, thrown `0x44796`): Thief 4 / 1, Monk 0 / −1, Ranger 0 / −1, Fighter 0 / −1, Priest
−2 / −4, Wizard −2 / −4, Shadow Warrior 2 / 4, Gargoyle 2 / −1, Sage 0 / 0, Lord 0 / 0. Constants: 100.0, 0.2, 1.2,
2.0 (`0x447D6`…), 0.5 (`0x447FE`).

A member with no bow has Wr = 0, so a throw does d = K − armour / 2 (usually 0 → floored by `_mdamage` to 1) `[C]`.

`_epstr` is the party-wide bonus of Strength 1–4 (+2 / 4 / 7 / 11, §9); a bow or throw hit that does not end the fight
resets the thrower's `_epstr` to 0; melee keeps it `[C]`.

### 3.3 Critical hits (`_critical(item)` `0x2771A`) `[C]`

After a hit: critical if rand(100) < t, t by the item used (melee: weapon +66; bow +67; thrown +68), so (t − 1) %:

| Item id | 35, 36, 74 | 59 | 62, 75 | 63, 84 | 83 | others |
|---|---|---|---|---|---|---|
| t | 35 | 75 | 50 | 40 | 25 | 0 |

"Critical hit!" adds Wm (melee) or Wr (bow / thrown) once more. Item names: `../extracted/items.json`.

### 3.4 Applying damage to a monster (`_mdamage(square, d, type)` `0x21ADC`) `[C]`

1. d −= trunc(`_epweak` × d / 100) (`_epweak` = 20 / 40 / 60 / 80 after a monster's Weakness 1–4, §9.3); **d < 1 →
   1** (every hit does at least 1).
2. If type > 0: hit location `_mlget` and wound `_getmd` (§6); then the monster's condition `_setmc` (§5).
3. If d < HP and `_mc` > 0: HP −= d. Otherwise the monster **dies** ("%s fell!"): its experience (+20) is added to the
   fight's experience `_epgetxp`, it leaves the field; when none is left `_eexitc` = 101 and the fight ends (§11).

Types: 10 arrows, 6 thrown, melee 1 — or 5 when the monster has the +43 flag or the target flies and the attacker does
not (`_chdtyp` `0x28266`) —, 2 Fire Flash / Flameball, 0 Mind Blast / Shock Wave (no wound).

## 4. Monster turn (`_enc_matk` `0x262BE`, `_monexe` `0x251D4`) `[C]`

### 4.1 Acting

- A monster asleep (`_st` = 1) wakes with rand(100) < 15 (14 %: "Wake Up!") and does nothing else that turn (the test
  is made at every `_monexe` call, i.e. in each of the three phases, s102).
- A ready monster (`_st` = 3) acts in phase p if rand(100) ≤ 5 p² + 3 × `_emlevel` (`_emlevel` = the **mean** of the
  groups' levels, §1.1; s82 read it as the sum) and its right arm is < serious and its `_mc` is 2 or 3.
- **Spell or special:** if rand(100) < +2E (spell ability): it moves (`_b_amove`, §7.2; a monster that left the field
  stops), is marked acted, takes its aim `_obj_select(0)` (below), then picks up to 10 times one of its three ids +30 /
  +31 / +32 (rand(3)); passive spells already in force for the monsters (Speed / Slow 9–14, Protection 16–19, Weakness
  30–33, Sleep 38: flag `_msplf`) are re-picked. An id 1–65 is cast (`_mmagic`, §9.3; 66 / 67 are the breath attacks,
  cast without a name); 0 = nothing that turn. Ids **68** and up are not spells: the monster moves **again** and goes on
  to a melee attack (§4.2) carrying that special.
- Otherwise (or after a 68 / 69 pick): it moves (`_b_amove`) and attacks a party member picked by `_obj_select(1)`.
- **`_obj_select(kind)`** `[C]` (s102): for r = 1 (kind 1, melee) or r = 1 … 6 (kind 0, spells), the members within r
  squares (Chebyshev box `_b_chbtn`) in square order; at the first r holding any, a random one (rand(count) − 1). No
  member next to it → no melee that turn.
- `_mstrategy` is set to 4 only if 0x44 < +30 < 0x42, which never holds (dead code).

### 4.2 Melee attacks

```
n = max(1, trunc(+16 / 115 + 0.7))                     attacks per turn (+16 = attack value)
each attack:  hit if rand(100) < trunc(+16 / n) − D (target +35)   or the target's _pc < 3
              u = (rand(100) / 100) ^ 0.5
              d = trunc(u × +18) − A (target +6F)        (+18 = damage value; 32 767 for two Deaths)
              parry (§4.3)
              _pdamage(target, _emstr + d, type 1)        (_emstr = the monster's Strength bonus)
```

Specials after a hit: **68 paralysis** ("%s is paralysed!": condition 2, the member's action becomes Parry; same
condition as sleep, so the wake roll of §1.5 applies); **35 drain magic** (current MP = trunc(MP × 0.6 + 0.5)) —
but 35 is below 68, so a monster that picked it has already cast it as Mind Blast 2 and returned; the drain branch is
unreachable `[C]` (original dead code). 69: plain attack.

So **DATM +16 = attack value (number of attacks and hit chance), +18 = damage value** `[C]` (`../FORMAT.md` §6.4 had
+18 as `[?]`).

### 4.3 Parry

The first monster hit with d > 0 on a member marked as parrying (§2) is reduced by the shield value (+B6), and the parry
is used up (`_epopt` = −1). Parry also raises D permanently through trunc(Parry / 8) (§1.2).

### 4.4 Damage to a party member (`_pdamage(square, d, type)` `0x1E940`) `[C]`

d < 1 → 1. If type > 0: hit location `_plget`, wound `_getpd` (§6), then `_setpc` (§5). If d < HP and the member is not
dead: HP −= d; otherwise the member dies (`_setdead`: HP 0, condition 0) and leaves the field. All members dead →
game over (`_deadmsg`, `_eexitc` = 1). Outside combat (traps, swimming) the same routine prints "%s: %d damage!".

## 5. Combat condition `[C]`

Party `_pc` (`_setpc` `0x1D3C`, recomputed from +0C after every wound; labels from `_healch` `0x316`):

| `_pc` | When | Label | Effect |
|---|---|---|---|
| 4 | dead, **or head or torso lost** | "Out" | no MV, no action |
| 0 | asleep / paralysed (condition 2), or head or torso seriously wounded | "Unconcious" [sic] | no MV; monsters hit automatically |
| 2 | a leg seriously wounded or lost | "Down" | hit chance × 3/4; no bow / throw / melee (`_chcond`); monsters hit automatically |
| 3 | otherwise | name | normal |

`_setpc` sets condition 0 (dead) only when HP is 0. **Losing the head or torso makes a member "Out" without killing
him**: condition stays 1, HP stays, he is shown "Out" after the fight too, and a Healing that restores the part (§9.1)
brings him back. `_alldead` counts only condition 0 (§12 item 2).

Monster `_mc` (`_setmc` `0x21452`): 0 dead (HP < 0 or **head or torso lost** — it dies at the next damage call, §3.4);
4 head or torso seriously wounded; 1 asleep; 2 a leg seriously wounded or lost; 3 normal. Party hit chance is 200 on
`_mc` < 3 or 4.

## 6. Hit location and wounds `[C]`

**Location** (`_plget` `0x1E728` for party members, `_mlget` `0x218BA` for monsters), with the attack type t of §3.4:

```
party:    u = rand(100) / 100            (0.01 … 1.00)
monster:  u = (rand(100) − 1) / 100      (0.00 … 0.99)
part = trunc(6 × u ^ (t / 2))            (part 6, i.e. u = 1.00, → 3 left arm)
t = 5:    part = rand(3) + 2  → left arm, right leg or left leg   (never the right arm, original quirk)
```

So arrows (t = 10, u^5) land on the head about 70 % of the time; thrown weapons (u^3) favour head and torso; spells
(t = 2, u^1) are uniform; melee (t = 1, u^0.5) mostly hits the legs and arms (head about 3 %, torso about 8 %); type 5
(big monsters, fliers hit from the ground) never reaches head or torso. This matches the manual (p. 46).
Body shapes (monster +42): 2 = body only (every hit "body", part 1); 3 = snake (every hit but the head is torso); 4 =
winged (arms are "right / left wing"). Party: the summoned elemental (slot 6) is always hit in the "body" (torso);
fliers' (+B8) arms are wings.

**Wound level** (`_getpd` `0x1E4B0` party, `_getmd` `0x21694` monsters):

```
u = rand(100) / 100
w = trunc( u × (d / (H + 0.0001) + 0.2 × old) / 0.15 + 0.5 )     old = the part's current level
if w > old:  part level = min(3, w)                                 (never lowers)
H = party: max HP (+39);  monster: its current HP before this hit
```

**Healing points** (`_heal(member, k)` `0x1FFA`): k points heal wounds level-first: a part at level k is cleared and costs
k, then k − 1, … — so 1 point = a light wound, 2 = a serious wound or two light, 3 = a lost part or serious + light. This is
the manual's "lost = light × 3 or serious + 1, serious = light × 2" (p. 45) `[C]`. The inn uses k = 2 (`towns.md` §4),
so it never restores a lost part, as the manual's "Healing 2".

## 7. Movement on the 7 × 7 field `[C]`

- **MV per round** (`_calmove(member, left)` `0x23F86`): from last round's unused MV `left` and the action:
  kept = left/2 (Aim Blow, Fire Bow), 0 (Parry), 2·left/3 (Attack), left/3 (Thrust), left − left/4 / left/2 / left/4 /
  0 for a spell of 1 / 2 / 3 / 4 MP, else left. Then MV = (left − spent) + base, base = `_baserate` (stride +29 with
  the armour % b3 and +2 for Shoes of Speed, `../FORMAT.md` §6.5); MV < 0 → 1; MV ≤ stride (+29). Spell modifier
  `_esplmv`: ≥ 11 → MV = that value (Ninja 11 / 14); 0–10 → + value (Speed +2 / +5, cumulative, cap 99 — so two Speed 2
  give 10, three give an absolute 15 `[C]` quirk); −1 → MV / 2, −2 → MV / 4 (Slow 1 / 2). MV ≥ 1; 0 if not Okay or
  `_pc` 0 / 4.
- **Wounded legs** (`_movlost` `0x23C06`): for a walker the code tests the right leg twice and never reduces MV (the
  "×2/3" branch is unreachable, original bug); `_pc` 2 already blocks most actions. For a **flier** with a wounded
  wing: both legs unhurt → MV × 3/4; one leg light → ½; both light → ¼; a leg serious → 0 and `_pc` = 2.
- **Steps** (`_b_chauto` `0x2324A`, `_dii` `0x230CC`): orthogonal step 1 MV; diagonal steps cost **1, 2, 1, 2 …** (the
  cost of diagonals toggles after each diagonal step) — as the manual (p. 42). A square may be entered if it is free
  terrain (`_pos`+1 ≠ 0) or the mover flies.
- Monsters (`_mcalmove` `0x2605C`, `_mmovlost` `0x25D30`) `[C]` (s102): called by `_enc_matk` after **every**
  `_monexe` call (each phase, acted or not) as `_emmove` = `_mcalmove(_emmove)`. Kept part by the action marker of that
  call: none 0, melee 2·left/3, a spell of 1 / 2 / 3 / 4 MP left/4, left/2, 3·left/4, left (not the party's table). Then
  `mv += left − kept` on a local that is **never initialised**: it is the stack slot of `_monexe`'s spell-id local (0
  after a plain melee turn, the spell id after a cast, otherwise whatever an earlier call left there); clamped to 1 …
  +38 after `_mmovlost` (the leg/wing table, +41 = flier), then the Speed / Slow modifiers (≥ 11 absolute, 0–10 added,
  −1 ½, −2 ¼), at least 1. The remaster does not emulate the stack value (§14).

### 7.1 The automatic walk (`_b_chauto` `0x2324A`, `_bf_checker`, `_bv_checker`, `_b_sqchecker`, `_b_sqchkinit`, `_b_mmove`, `_dii`) `[C]` (s102)

Used by every automatic move (monsters, the party's flight, auto combat and M). `_b_amove` first marks the box of
Chebyshev radius MV around the mover (`_b_chbtn` → `_fmark`) and resets the diagonal toggle (`_dii(0)`). Then
`_b_chauto(square, target, MV)`:

1. Visited marks cleared; the **target** is marked visited when it is occupied, or blocked terrain for a mover that is not
   flying (`_tmark`+7).
2. MV times (the loop counts steps, not MV): mark the current square visited; among the 8 neighbours (`_base` = −8 −7
   −6 / −1 +1 / +6 +7 +8; no wrap at columns 0 / 6) inside the box, the candidates are, by `_mode`: 0 (walker) the
   squares whose walk byte is set (walkable and free: placement and `_b_mmove` zero it under an occupant), 1 (a flier
   whose two wings are unwounded) the free squares. The candidate with the smallest squared distance to the target wins
   (first in `_base` order on a tie) **even if it is farther than the current square**; none → stay.
3. If the winner is visited or occupied, the walk ends. Otherwise the mover steps there; `_mvcnt` (the MV left) drops by
   1, or on a diagonal by 1, 2, 1, 2 … (`_reg_dia` toggle), floor 0. `_feaful` (step to the farthest neighbour) is
   cleared by `_center` before any walk reads it, so it never applies.

So a walker stops next to an occupied target, may go round a blocker, cannot step back onto a square it passed, and
on an **empty** target (an exit) it usually steps off again on its next step unless the square it came from is the first
best neighbour (original behaviour). The MV left is what `_b_amove` returns (`_epmov` / `_emmove`).

### 7.2 Choosing the target (`_b_amove` `0x238AE`, `_b_chkflee` `0x2743E`) `[C]` (s102)

- **Monsters** (`_mstrategy` 0; the 4 branch is dead code, §4.1): a member within 1 square → no move at all (and no
  flight check). Else the target is the nearest member (`_b_friend(0)`).
- **Party members in auto mode** by movement strategy D (+2D low nibble, 0-based): 0 group → `_center(party)`; 1 spread
  out → `_faraway(_center(party))`; 2 cover a companion → the protected member's square (`_protwho`: the first time
  the player is asked "Whom does %s support?"; the answer is kept in +2D as 0x80 | member × 16; an invalid answer or
  himself sets +2D to 0); 3 close with the nearest enemy → none if a monster is within 1 square and the member has a
  target in `_atk_list`, else `_b_friend(0)`; 4 keep away → `_faraway(_center(monsters))`; 5 charge → `_center(monsters)`;
  6 → no move.
- **`_b_chkflee`** then: party flight (`_fleeflag` = 1, option L) or auto flight (party: current HP < max HP × (30 if
  A = 1, 55 if A = 2) / 100, A = +2A; monsters: HP < 30 % of +26, never for an event monster `_evmon` ≥ 0; sets
  `_fleeflag` = 2 for this mover only). A fleeing mover on an exit leaves the field at once (`_b_chkedge`, target none);
  otherwise its target becomes `_b_getnear`'s exit.
- Then, for a target 0–48, the walk of §7.1 with the mover's MV.

## 8. Encounter options (`_enc_opt` `0x1CE02`, `_partyact` `0x1CC36`) `[C]`

Before each round (in round 1 or when the player asks, otherwise the party strategy picks) the party chooses: Q auto
combat, R auto round, G Greet (2), B Beg for mercy (3), A Demand surrender (4), F Fight (5), L Flee (6).
`_cmapf` = 1 is **outdoors**, 2 a dungeon (`../FORMAT.md` §6.7, corrected s82). `_emnice` = the sum of the monsters' +28 ("nice", −10 undead, −1000
Deaths).

| Option | Succeeds if | Result |
|---|---|---|
| Greet | rand(100) × (1 − `_emcham`) **>** (50 outdoors / 90 dungeon) − `_emnice` (s102: the comparison was read the wrong way round before); `_emcham` = 1 once any monster has taken damage (`_mdamage`), so after that only a limit below 0 lets it through | "The monsters greeted you too."; fight ends (`_eexitc` 106): no gold; the experience of monsters already killed is still paid |
| Beg for mercy | rand(100) < (90 + nice outdoors / 60 + 3 × nice dungeon) | "They took gold in return."; party gold becomes gold mod 256; back to the previous square (outdoors only, §11); kills' experience paid. **Items are not taken** |
| Demand surrender | rand(100) ≤ c × (`_epgetxp` / XP of the first monster type) + 2 × `_par`+0B − 3 × `_emlevel`, c = 25 outdoors / 15 dungeon | "The monsters surrendered."; `_eexitc` 105: gold, kills' experience and the treasure roll (§11) |
| Flee | `_fleeflag` = 1 and `_eautomov`: each able member (`_pc` 3, Okay) runs `_eauto1` — `_try_cast` (while fleeing only Healing, §9.2), else `_b_amove` (§7.2: on an exit he leaves, else he walks toward the nearest open exit) and `_try_attack` | "The party escaped the fight." when none is left; otherwise `_fleeflag` is cleared and the members' orders follow as for Fight (manual menu or auto) |

On failure: "The monsters ignored you." and the fight goes on. `_par`+0B is the party's average level as last stored
by the Mystic (`towns.md`), not recomputed here. `_epgetxp` / XP is roughly the number of monsters already killed.

**Party strategy** (`_par`+15E attitude, +15F flight; manual p. 36): friendly → Greet while round < 4, then Demand
surrender; hostile → Fight; surrender → Beg; demand surrender → Demand. The flight setting overrides with Flee when the
party's current HP total < 75 % (slightly in danger) / 60 % (fairly) / 40 % (very) / 0 (fight to the end) of its
maximum HP total.

### 8.2 Auto mode (`_enc_opt`, `_partyact` `0x1CC36`, `_eautomov` `0x1CB30`, `_eauto1` `0x1C956`, `_try_attack` `0x1FDF2`, `_protect` `0x1FA34`) `[C]` (s102)

- **Flow:** the options menu is shown in round 1 (`_bturn` ≤ 1) and whenever the last choice `_xpopt` was not Q; after Q
  the party strategy picks the option every round without a menu (Q = auto to the end), after R the menu returns. Q / R
  call `_partyact` (above) for the option. A refused Greet / Beg / Demand, Fight, or a flight that leaves members on the
  field all continue at `0x1D0E2`: auto mode → `_eautomov` (every member's `_eauto1`) and the round; manual mode → a
  summoned elemental acts by itself (`_eauto1(6)`), then the member menu (1–6 a member's own setup `_b_chmove`, M auto
  move = `_eautomov` with `_player` = 1 so a member on an exit does not leave, R reset, T display speed, F start).
  Members set by hand (`_eflag`) are skipped by `_eauto1`.
- **`_eauto1(member)`:** only a living member with `_pc` 3 and condition 1 (others are just flagged); `_mode` 1 for a
  flier with both wings sound, else 0; `_try_cast` up to 3 times; if no spell: `_b_amove(_epmov)` (§7.2), then
  `_try_attack` unless `_protect` returns ≥ 0.
- **`_protect`:** −1 while fleeing or without the +2D bit 7; a protégé no longer on the field sets +2D = 2 and returns −1;
  otherwise a monster square (one next to both, else the nearest monster) — only its sign is used, so a covering member
  never makes a weapon choice in auto mode and keeps the action he had.
- **`_try_attack`:** for r = 1 … 6 the first ring holding monsters gives the target (`_atklist` favours the square with
  the highest `_atk_list` entry — it indexes that table by list position, original slip —, else a random one; detail
  below); r ≥ 3:
  Fire Bow (B = 0 with a bow) or Throw (B = 1 with a thrown weapon); r = 2: Lunge for a Fighter; r = 1, or 2 for a
  Thief: by C (0 bow, 1 thrown, 2 Thrust, 3 Slash, 4 Aim Blow; Attack is never chosen), each through `_opt_get(1, …)`.
  Nothing else changes the action. B, C = +2B, +2C.
- **`_atk_list`** (s110, `_chrexe` / `_try_attack` / `_atklist` `0x1FD42`) `[C]`: per member a memory of 49 longs, one per
  square: −1 unknown, 1 attacked (written when `_target` finds a monster, before the hit roll), 2 hit, −2 missed or
  nobody on the square. Written by every bow, throw and melee action (manual or auto). `_try_attack` clears it to −1
  when the member's square differs from the one at its previous call (`_tmark` record byte +4, 0 after `_enc_init`;
  the list itself is not cleared between fights, so only a member standing on square 0 at his first call keeps an old
  fight's memory — not emulated). `_atklist(list)` scans list positions i = 0 … count − 1 and keeps the first i with
  the highest `_atk_list[member][i]` above −1 — **square i, not the square listed at i** —; if that value is below 0 it
  returns −1 (random target), else **i itself becomes the target square** (`_opt_get(1, …)` keeps `_epopt` even with
  nobody there and does not test the reach). So a member who stands still after attacking squares 0 … count − 1 aims at
  those squares, wherever the monsters now are; an empty square makes `_chrexe` print "Miss!" and write −2.
  Movement D = 3 (`_b_amove`): next to a monster **and** any memory entry > 0 → no move, else toward the nearest
  monster (`_b_friend(0)`).
- **`_bturn`** (s110) `[C]`: 1 when `_encount` starts, + 1 after each round's phases (so rounds fought + 1); read by
  `_enc_opt` (menu while ≤ 1), `_partyact` (Greet while < 4) and `_try_cast` (chance − 5 × `_bturn`).
- **Strategy bytes** (+2A–+2E, profile page 2, 0-based): A auto defence, B missile, C melee, D movement (+ bit 7 / bits 4–6
  the covered member), E magic; `_mkdetail` sets 2, 0, 2, 3, 3. Party strategy: `_par`+15E attitude, +15F flight.

### 8.1 Fleeing and leaving the field (`_b_chkflee` `0x2743E`, `_b_chkedge` `0x270B6`) `[C]`

A combatant standing on one of the 24 edge squares (table `0x446E0`, minus walls `_ewall`) leaves the field. A party
member leaving: when no member is left, `_eexitc` = 4 (fled: back to the previous square, no gold). Monsters leaving:
when none is left, `_eexitc` = 102 (spoils as for a win, §11). Automatic flight during auto movement: a party member
with auto-defence A = 1 / 2 runs for the edge when HP < 30 % / 55 % of max (A = 0 never); a monster when HP < 30 % of
its HP base (+26), unless the fight is an event fight (`_evmon` ≥ 0: `_monevf(0)` in `_enc_mons` sets it when the
first `DATM` type is 72 Lord Castlebaum, 73 Kor or 83 Arathamus; then no monster of that fight flees). Winning such a
fight by end 101 or 103 (`_enc_end` calls `_monevf(1)` only there) sets story flag 12 / 9 / 59 (`events.md` §3,
`ending.md` §1) `[C]` (s110).

## 9. Spells

**MP cost** (`_getmp` `0x2B6F8`, table `0x44B7C`, by spell number 1–65) `[C]`:

| # | 1–4 | 5–8 | 9, 10 | 11, 12 | 13 | 14 | 15 | 16–19 | 20–23 | 24 | 25 | 26 | 27 | 28 | 29 | 30–33 | 34–37 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| MP | 1–4 | 1–4 | 1, 2 | 2, 3 | 3 | 2 | 1 | 1–4 | 1–4 | 1 | 2 | 3 | 4 | 3 | 4 | 1–4 | 1–4 |

| # | 38 | 39 | 40 | 41 | 42 | 43 | 44, 45 | 46 | 47 | 48 | 49 | 50 | 51–54 | 55–58 | 59–62 | 63 | 64 | 65 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| MP | 2 | 3 | 4 | 2 | 4 | 1 | 1, 1 | 3 | 2 | 3 | 4 | 2 | 1–4 | 1–4 | 1–4 | 1 | 4 | 1 |

Casting (`_chrexe`): spells 1–37 → `_pmagic1` (`0x2BCC4`), 38+ → `_pmagic2` (`0x2A5A0`); afterwards MP −= cost (floor
0), spell 64 (Annihilate Enemy) empties MP, and the inn MP-growth flag (+72) is set (`towns.md` §4). The menu lists a
known spell only if current MP ≥ its cost (`_splist` `0x2B86E`). Rank r (1–4) of each spell: table `0x44C59`.

**Resistance roll R** used by several spells: the spell works on a monster if rand(100) ≤ (caster +37 − monster +24) ×
5 + 50 (+37 = caster's max MP; +24 = monster magic resistance). Monster spells on the party use (monster +24 − member
+37) × 5 + 50.

**R compares unsigned `[C]` (s110, static):** every such roll is `cmp.l limit, rand; bhi fail` — an unsigned compare. A
limit below 0 (target 11+ points of MP / resistance above the caster) is a huge unsigned number, so the spell then
**always works**; a signed reading would make it never work. Sites: monster Slow `0x29D44`, monster Mind Blast
`0x2A282`, Resurrection `0x2A882` ((caster L − target L) × 5 + 50), Fear `0x2AA4C` ((L − `_emlevel`) × 5 + 50), party
Slow `0x2C240`, Confusion `0x2C76C`, Weakness `0x2CDAE`, party Mind Blast `0x2D05E`. The remaster follows it under the
code option of D4-051 (§14).

### 9.1 Party spells (`_pmagic1`, `_pmagic2`) `[C]` unless marked

| # | Spell | Code |
|---|---|---|
| 1–4 | Healing r | target member: HP += rand(6r² + 4) − 1 (0–9 / 27 / 57 / 99), capped at max; `_heal(r)` (§6) |
| 5–8 | Fire Flash r | target square: d = trunc(rand(N) × Int / 15), N = 10 / 30 / 60 / 100; no resistance roll; `_mdamage` type 2 |
| 9, 10 | Speed 1, 2 | every member `_esplmv` += 2 / 5 (cap 99); MV recomputed |
| 11, 12 | Slow 1, 2 | each monster passing R: `_emsplmv` = −1 / −2 → MV ½ / ¼ |
| 13 | Levitation | every member flies |
| 14 | Sink | every flying monster lands (no roll) |
| 16–19 | Protection r | every member A (+6F) = armour value (+B7) + 1 / 3 / 6 / 10 (replaces, cap 255) |
| 20–23 | Confusion r | each monster passing R: its type's spell ability +2E × 0.8 / 0.6 / 0.4 / 0.2 (once per affected monster, so a group's value shrinks once per member hit) |
| 24, 25 | Arrow Flame 1, 2 | caster: Hr = Fire Bow + 50 / + 100, Wr = bow + 15 / + 25 (cap 255) |
| 26, 27 | *Arrow Flame 1, 2 | every member: Wr = bow + 15 / + 25; **no hit bonus** |
| 28, 29 | Accuracy, *Accuracy | caster / every member: Hr = 250 |
| 30–33 | Weakness r | each monster passing R: its type's +1C (party hit penalty) × 0.8 / 0.6 / 0.4 / 0.2, and its `_emstr` = 0. Monster damage (+18) unchanged |
| 34–37 | Mind Blast r | every monster passing R: d = rand(N) − 1, N = 8 / 18 / 30 / 44 (then floored to 1, so 1 … N − 1); **monsters whose experience +20 is a multiple of 7 and below 2000 are immune**; no wound. Draw order (`0x2CFEC`): an immune monster draws nothing; any other draws rand(N) **then** R's rand(100), so a monster that resists still uses both |
| 38 | Sleep | each monster with rand(100) ≥ `_emlevel`² falls asleep (no caster term; encounters of level 10 and up are immune) |
| 39 | Teleportation | rand(100) < 3 × level + 20 → the party leaves (as a flight: `_eexitc` 2, previous square outdoors / same square on a location map (§11), no gold) |
| 40 | Resurrection | target dead with Con (+32) ≠ 0: if rand(100) ≤ (caster L − target L) × 5 + 50 → placed on a random free square (10 tries), condition 1, HP = max, all wounds healed (`_heal(10)`) |
| 41 | Fear | if rand(100) ≤ (L − `_emlevel`) × 5 + 50: `_emlevel` −= 6 (min 1). Nothing makes monsters leave directly `[~]` |
| 42 | Summon El. | `[C]` (s102, `0x2AAB4`): if `_par`+0C ≤ 6, the target square's walk byte is set (free walkable ground) and `_par`+14 (elemental 0–2, set at the guild) ≤ 2: `_par`+0C = 7, `_summflag` = 1, ELMS record `_par`+14 → slot 6 (MP and HP set to max, condition 1, wounds 0, `_pc` 3, `_set1(6)`: Wm = +6B + its stored +3B), placed on the target square; `_splfp[42]` = 1. No message either way. Its orders come from `_eauto1(6)` at the next options (automatic in manual mode too), so it acts from the next round (manual p. 53 agrees). In the cast round `_enc_patk` already counts slot 6, and `_chrexe(6)` runs its stale `_epatk`: `_enc_init` sets all seven to 0x0C (Parry) and every executed action resets it to 0x0C, so it is Parry (nothing happens) — unless an earlier elemental of the same fight left the field with an action it never got to run (its phase roll failed), which then runs at its old target (s110 `[C]`; the remaster's new slot 6 always parries, that leftover case is not emulated). `_enc_end` clears slot 6's condition and restores +0C from `_svpmcnt` |
| 43 | Dispel Undead | once per fight, only if the **first monster type** has the +44 flag: x = (L − `_emlevel`) × 5 − rand(100); x > −5 → all monsters melt (`_eexitc` 103: counted as killed); −45 < x ≤ −5 → they leave (104 = 'h': spoils of a win, no extra XP); else "It had no effect" |
| 44, 45 | Ninja 1, 2 | caster: Hm 200 / 255, Wm 20 / 25, A 8 / 10, MV 11 / 14 |
| 46 | Awaken | every member in condition 2 (asleep or paralysed) wakes |
| 48 | Dissolve | rand(100) ≤ 110 − 10 × `_emlevel` → all monsters melt at once (`_eexitc` 103) |
| 51–54 | Shock Wave r | target square; every creature (friend or foe) on the wave path `_xwave` from the caster: 7 / 15 / 24 / 35 fixed, no roll, no wound. **Path `[C]` (s102, `_f2f(caster, target, 20)` → `_shoot` `0x29296`):** the field as a 6 × 5 grid of 8-pixel cells per square; a Bresenham line from the caster's cell (column × 6, row × 5) toward the target's, one point per step of the longer axis (the end point itself not plotted); each point (8x + 24, 8y + 24) → square (`_b_cmouse`: row = y / 40, column = x / 48). `_pmagic2` (`0x2B192`) walks that list: an empty square, the caster's own square and the square hit just before are skipped; the others take the damage (`_pdamage` / `_mdamage` type 0) until the fight ends. A diagonal line stairs through side squares (48 → 0 crosses 48, 47, 40, 39, …, 7, 0); the target square is normally reached |
| 55–58 | Flameball r | every creature on the pattern around the **target square** (5 / 13 / 21 / 21 squares: + shape, radius-2 diamond, 5 × 5 without corners; `_xflam2` `0x44954`, counts `0x44950`, offsets on the 7-wide grid walked in table order 0, −7, −1, +7, +1, −14, −8, −2, +6, +14, +8, +2, −6, −15, −9, +5, +13, +15, +9, −5, −13). **Edge `[C]` (s110):** `_b_chbtn(target, r)` (`0x2B418`) first sets `_fmark` to the box of Chebyshev radius r around the target (clipped to the field); a pattern square is used only if it is 0–48 and marked (`0x2B54E`–`0x2B570`). The pattern's radius is at most 2 ≤ r and a square that wraps to another row lands 5+ columns away, so this is exactly an edge check: nothing wraps. Each square: a member (`_chr_here`, armour A) or else a monster (`_mon_here`, +1A); `_f2f` × 2, then d = rand(N) − armour, N = 18 / 25 / 35 / 43, floored to 1; type 2 |
| 59–62 | Strength r | every member `_epstr` = 2 / 4 / 7 / 11 |
| 63–65 | Divine Aid, Annihilate Enemy, Super Transport | `[C]` (s102): the combat spell pages (`_getmagic(member, 0)`, page table `0x44AC8`, lists `0x44AE2`) hold every spell but 15, 47, 49, 50, 63 and 65 (63 is on the dungeon page, 65 on the town page). **64** is listed; `_pmagic2` has no branch for it (its compares stop at 62), so it only says its name and empties the caster's MP (D4-071). 15, 47, 49, 50 outside combat: `spells-noncombat.md` |

`_eexitc` 103 (`_enc_end`): fight won, experience of the kills so far + monster count (`_emcnt`) × XP of the first
monster type.

#### MSX2 / PC-98 (s104): spell 64 (D4-071)

Static reading only. **Both platforms work the same way as X68000 `[C]`:** 64 is on combat page 4 and needs known + MP ≥ 4
to appear. Casting it shows the name, has no effect, and sets MP to 0.

- **MSX2 `[C]`** (player disk = TOSEC "Disk 1 of 2 (Scenario Disk)"; segment addresses at load base `0x0100`; tool
  `tools/scripts/msx2_p4_dis.py`, inline-operand helpers seg `4001` `i=66bf,6682`, `4002` `i=496f,4932`, `4004`
  `i=4ba9,4b6c`). Name `Annihilate E.` (seg `4001` `0x5E41`).
  - *Listed:* seg `4002` `0x2124` calls the spell list `0x26CE(member, 0)`. The page is place + key − '1': keys 1–5
    give pages 0–4 ("4:こうげきけい" is page 3), and 6 quits. Page table `0x438E` and lists `0x42F5` are byte-identical to
    X68000 `0x44AC8` / `0x44AE2`, so page 3 = 20–23, 30–33, 59–62, **64**. A spell is selectable if known (+73 + i = 1)
    and MP (+38) ≥ cost (`0x14CB`, table `0x1485`). At `0x2130` the action is 8 (cast) and the spell is index + 1. 64
    gets no target prompt (`0x21B2`: target 0).
  - *Cast:* seg `4001` `0x14B5` (action 8). It prints "%sは%sのまほうをとなえた。" (`0x14FB`). At `0x150B`, spell > 37
    calls stub `0x0116` → seg `4004`; otherwise `0x010E` → seg `4003`. Cost = `0x33AE(spell − 1)` (table `0x3368`,
    the 65 X68000 values; #64 = **4**). MP −= cost, or MP = 0 if MP ≤ cost (`0x155E`–`0x1598`). Then `0x1599`: spell
    = `0x40` → MP = 0.
  - *Effect:* seg `4004` entry `0x0131` (`_pmagic2`). It compares the spell with 38 … 43, 46, 48 (`0x0163`–`0x0882`),
    then ranges 51–54 (`0x09A8`), 55–58 (`0x0C27`) and 59–62 (`0x0F66`). Anything else, 64 included, goes to `0x1034`
    and returns 0. **No effect.**
- **PC-98 `[C]`** (`Phantasie IV (Disk 1).hdm`, `P4.EXE` via `mcopy`). The EXE is **EXEPACK-packed**: its header is at
  image `0x38BD0` (signature `RB`, real CS:IP `2214:0016`). It was unpacked with a scratchpad script (not in the repo).
  The addresses below are linear offsets in the unpacked load image; DGROUP is `0x6B97`. Name `Annihilate Enemy`
  (`0x6CA70`).
  - *Listed:* `_getmagic` `0x16C98` (`16A1:0288`) is called at `0x1BE86` with (member, 0). The page is place + key,
    keys 0–4; key 5 cancels (returns `0x64`). Page table DS:`0x69C8`, lists DS:`0x69E2` (byte-identical to X68000;
    packed file offset `0x36A5B`). The page display `0x16B28` shows a spell if known (+4D91 = 1) and MP (+4D56) ≥
    `_getmp`. Then action `0x14` (cast) and spell = index + 1 (`0x1BEAA`–`0x1BEB4`).
  - *Cast:* `_chrexe` cast branch `0x1B302`. Spell ≤ 37 → `_pmagic1` `0x16DFB`, else `_pmagic2` `0x289E8`
    (`0x1B337`). `_getmp(spell − 1)` `0x16A14`, table DS:`0x6A7C` (packed file offset `0x36B0F`, the same 65 values;
    #64 = **4**). MP −= cost, or MP = 0 if MP ≤ cost (`0x1B36E`–`0x1B3A6`). Then `0x1B3AB`: `cmp spell, 0x40` → MP = 0.
  - *Effect:* `_pmagic2` compares 38 … 46, 48, then ranges 51–54, 55–58 and 59–62 (last test `0x29497` / `0x294A0`:
    < 59 or > 62 → `0x29568`, return 0). **No effect.**

So D4-071's code side is the same on X68000, MSX2 and PC-98: no version gives 64 an effect.

### 9.2 Automatic spell choice (`_try_cast` `0x1EFE2`, `_psetcast` `0x1EDF8`) `[C]`

Used in auto combat / auto round, by profile setting E (+2E: 0 always attack, 1 attack when needed, 2 never, 3 as
needed, 4 defend when needed, 5 always defend). A spell is cast only if MP left after it ≥ max MP / 2 (healing,
counters) or / 3 (random defence).

- E ≥ 3 (defensive): Healing 4 → 1 on a member below half HP; Resurrection on a dead member; Speed 1 / 2 against a
  monster Slow; Levitation against Sink; Awaken if ≥ 2 members asleep; then with chance 30 / 50 / 80 % (E = 3 / 4 / 5)
  − 5 × round one random spell of {9, 10, 13, 16, 17, 18, 19} not already in force.
- While fleeing: only Healing on members below half HP.
- E < 4 (offensive): Slow 1 / 2 against monster Speed; Sink against monster Levitation; Strength 1–4 against monster
  Weakness; Dispel Undead (40 %, first type undead, once); Sink on flying monsters; then with chance 75 / 35 / 15 % (E =
  0 / 1 / 3) walk the list **8, 37, 7, 36, 6, 35, 5, 34, 12, 27, 29, 23, 62, 22, 26, 61, 21, 60, 20, 59** (`0x44162`),
  trying spell i with chance (785 − 30 i) / 10 000; Fire Flash goes to the farthest opponent (`_faropp`).

So the automatic set is {1–4, 5–8, 9–14, 16–19, 20–23, 26, 27, 29, 34–37, 40, 43, 46, 59–62}.

Details (s110, decomp of `0x1EFE2`) `[C]`:
- "In force" `_csf(s)` = `_splfc[s]`: the party spells in force (`_splfp`, set only by lasting spells — never by
  Healing or Resurrection) or **chosen this round** by an earlier member's automatic turn (`_psetcast` sets `_splft` /
  `_splfc`; `_eoptreset` rebuilds them each round). `_msf(s)` = monster spell s in force.
- Healing / Resurrection: the members are visited **in party order** (slot 6 included while an elemental is summoned);
  a dead member gets Resurrection (40) if not chosen this round and his square's walk byte (`_pos`+1 at `_tmark`) is
  set; a living member in condition 1 (Okay — an asleep member is skipped) with HP < HP max >> 1 gets the highest of
  Healing 4 … 1 not chosen this round that `_psetcast` allows. While fleeing only the Healing part runs.
- Counters (E ≥ 3): `_msf(11 + i)` → Speed rank i + 1 (9 + i, Slow 1 → Speed 1, Slow 2 → Speed 2); `_msf(14)` → 13;
  ≥ 2 members in condition 2 → Awaken (46) unless chosen.
- Random defence: rand(100) < (30 / 50 / 80) − 5 × `_bturn`, then **one** entry rand(7) of {9, 10, 13, 16, 17, 18,
  19}: refused when Speed 1 or 2 is in force (for 9 / 10), 13 is in force (for 13), or any of 16–19 is (for 16–19);
  no second pick.
- E < 4: `_msf(9 + i)` → 11 + i; `_msf(13)` → 14; `_msf(30 + i)` → 59 + i (i = 0 … 3, in that order); Dispel Undead:
  rand(100) is drawn when the first type is undead, cast if ≤ 39, not chosen, and `_epda` = 0; Sink (14) when any
  monster on the field flies and 14 is not in force; then rand(100) < 75 / 35 / 15 and the walk: for i = 0 … 19 the
  roll rand(10 000) < 785 − 30 i comes **before** `_psetcast`; a square spell (5–8) goes to `_faropp`'s square (none:
  next entry; nobody there: no cast).

### 9.3 Monster spells (`_mmagic` `0x29A08`) `[C]`

Fire Flash r: rand(10 / 30 / 60 / 100) to the target square `_obj_select(0)` gave (no Int scaling, no roll), type 2. Speed: every monster
`_emsplmv` = 2 / 5. Slow on the party: each member failing R gets −1 / −2. Sink: party fliers land. Protection r: each
monster type's armour +1A raised to at least 5 / 7 / 9 / 11. Weakness r on the party: `_epweak` = 20 / 40 / 60 / 80 %
off all party damage (§3.4). Mind Blast r: each member passing R takes rand(N) − 1 (same N and the same "experience a
multiple of 7 and < 2000" immunity, applied to the member's experience; as the party's, an immune member draws nothing,
any other draws rand(N) before R's rand(100), `0x2A20A`). Sleep: each living member with rand(100) ≥
level² falls asleep (level 10+ immune). Breath 66 / 67 (s102 re-read, `0x2A3D6`): for r = 1 … 6 the box of
radius r (`_b_chbtn(monster, r)` → `_fmark` `0x2A426`, set just before the scan; `_chk_range(monster, r, 1)` skips a ring
with no member, s110) is scanned in square order and each member found takes rand(rand(16) + 9) / rand(rand(31) + 14) − A,
type 2 (67 draws rand(16) first and overwrites it, `0x2A488`: three draws a hit); a
member inside a smaller ring is found again on each wider one, and the scan stops only when the hit count **exceeds**
n = 2 / 4, so n + 1 hits land (an adjacent lone member is hit three times by 66). Other ids: no effect in combat `[?]`.

### 9.4 Which casts turn the caster and the target (`_f2f` `0x297D6`) `[C]` (static reading)

`_chrexe`'s cast branch (`0x28E54`–`0x28F8C`: name, `_sdmag`, `_pmagic1` / `_pmagic2`, MP) turns nobody itself. Every turn
is an `_f2f(a, b, kind)` call inside the spell code. `_f2f` (body `0x29906`) does nothing unless a and b are both 0–48.
Otherwise it always turns both sides, whatever `kind` is: first the creature on b toward a (`_b_chkdir(b, a)` →
`_xdir`, `_b_face(b)`), then the creature on a toward b. Only after that does `kind` choose the picture: < 100 a missile
(`_shoot`, 20 the Shock Wave wave), ≥ 100 a flash on b. `_b_chkdir` sets `_xdir` = 0 when a = b, and `_b_face` does
nothing for delta 0 or an empty square, so a cast at one's own square or at an empty square turns only the other side.
`_b_face` is the facing table of `FORMAT.md` §6.9. Repeating it with the same delta changes nothing, so the 2–3 calls
per target below act as one turn. Loops go through squares 0 → 48, so a caster that reaches several creatures ends up
facing the **last one in square order**.

Party casts (caster square: `_pmagic1` `fp-12`, `_pmagic2` `fp-40`):

| Spell | Call | a ↔ b | When |
|---|---|---|---|
| Fire Flash 5–8 | `0x2BFDE` (kind 0), `0x2BFF6` (304) | caster ↔ target square | always (an empty square or a member's square too) |
| Sink 14 | `0x2C4A6` (304) | caster ↔ each monster | each flying monster, as it lands, in square order |
| Confusion 20–23 | `0x2C786` (305) | caster ↔ each monster | each monster that fails to resist (R) |
| Weakness 30–33 | `0x2CDCE` (305 × 3) | caster ↔ each monster | each monster that fails to resist (R) |
| Mind Blast 34–37 | `0x2D07C` (303 × 3) | caster ↔ each monster | not immune and fails to resist (R); before the damage |
| Dissolve 48 | `0x2B12A` (304) | caster ↔ each monster | only on success, each just before it is removed |
| Shock Wave 51–54 | `0x2B20E` (20) | caster ↔ target square | first, before the wave |
| | `0x2B2FC` (302–304) | caster ↔ each square hit | friend or foe, before the damage |
| Flameball 55–58 | `0x2B406` (1) | caster ↔ target square | first |
| | `0x2B48C` (2), `0x2B4A4` (306) | **centre (target) square** ↔ each occupied pattern square | members too, before the damage roll; the caster plays no part |

Monster casts: **every** cast turns first. In `_monexe`, after the spell-ability roll and the move, `_obj_select(0)`
picks the aim. If the aim is a square 0–48, `_f2f(monster, aim, 200)` (`0x25578`) runs before the spell id is picked.
So the monster and the aimed member face each other whatever the id turns out to be: a spell without its own call
(Speed, Slow, Protection, Weakness, Sleep), id 0 (nothing), or 68 and up (it then walks again and strikes; the melee
`_f2f(monster, target, 206)` is `0x25938`). Inside `_mmagic` (caster square `fp-16`) some spells turn again:

| Spell | Call | a ↔ b | When |
|---|---|---|---|
| Fire Flash 5–8 | `0x29B6C` (0), `0x29B84` (304) | monster ↔ aim square (`_obj_select(0)`) | always |
| Sink 14 | `0x29E82` (304) | monster ↔ each member | each flying member, as he lands |
| Mind Blast 34–37 | `0x2A2A0` (303 × 3) | monster ↔ each member | not immune and fails to resist (R); before the damage |
| Breath 66 / 67 | `0x2A512` (1), `0x2A52A` (304) | monster ↔ member | once per hit |

**No turn:** party Healing, Speed, Slow, Levitation, Protection, Arrow Flame, Accuracy, Sleep, Teleportation,
Resurrection, Fear, Summon El., Dispel Undead, Ninja, Awaken, Strength and 64 have no `_f2f`. Monster Speed, Slow,
Protection, Weakness and Sleep have none inside `_mmagic`, but still get the `_monexe` turn toward the aim. The manual
says nothing about facing.

(Side note, not a facing matter: party and monster Mind Blast draw the damage rand(N) **before** the resistance
rand(100) (`0x2CFEC`, `0x2A20A`); an immune target (experience a multiple of 7 below 2000, tested at `0x2CFE8` /
`0x2A206`) skips both. The remaster draws them in this order since s110.)

## 10. Night attacks, Listen and surprise outdoors (`_oencchk` `0x1668C`, `_oencgo` `0x165D6`) `[C]`

When an outdoor encounter is triggered:

- **37 %** (rand(100) ≤ 37), without a day/night test in this routine `[~]`: **night attack**. One random living member
  is on watch; every other Okay member is put to sleep (condition 2). "<watch> sensed monsters nearby"; if rand(100) <
  the watch's Listen (+46): "noticed them first" and the fight starts without a surprise; else the monsters get a free
  first round (`_einf2`). The sleepers stay asleep either way (they wake by §1.5 or Awaken).
- Otherwise: 36 % "You found sleeping monsters. Attack them in the night?" (yes → the party gets a free round, no → the
  encounter is skipped). Else each member with rand(188) ≤ Listen senses them. Then with chance (8 × area level + 30) %:
  if nobody sensed, 60 % (rand(10) ≥ 5) "The monsters took you by surprise!", else they just approach; otherwise, if
  someone sensed: "Fight?" — yes: 80 % (rand(10) ≥ 3) "You took the monsters by surprise!" (free round), 20 % surprised;
  no: 70 % (rand(10) < 8) avoided, 30 % they approach. Area level = `_al`[map]. (s97: percentages corrected from the
  disassembly, rand(n) = 1..n; `outdoor.md` §5 had them right.)

Dungeon `_listen` (`0x18FE4`): any Okay member with rand(100) ≤ Listen hears something (message only).

## 11. End of the fight and spoils (`_enc_end` `0x24AE2`, `_esub01` `0x248AC`, `_esub02` `0x24774`) `[C]`

| `_eexitc` | Cause | Gold | Experience | Treasure roll |
|---|---|---|---|---|
| 1 | party dead | — | — | — |
| 2, 4 | fled / Teleportation | 0 | kills | no |
| 3 | Beg for mercy | party gold → gold mod 256 | kills | no |
| 101 | all killed | `_emgold` | kills | yes |
| 102 | all monsters left the field | `_emgold` | kills | yes |
| 103 | Dissolve / Dispel Undead melt | `_emgold` | kills + monster count × XP of the first type | yes |
| 104, 105 | Dispel Undead drives them off / surrender | `_emgold` | kills | yes |
| 106 | Greet accepted | 0 | kills | no |

**Where the party goes** `[C]` (s102): for ends 2, 3 and 4 `_enc_end` copies the old square `_cox` / `_coy` into the
current one `_ccx` / `_ccy` (`0x24C28`, `0x24C4A`); Greet (106) and the wins skip it. Both pairs are the **outdoor**
square (`_cox` is written only by `_omove`, `_save_ship`, `_oload`; `_ccx` is read only by outdoor code), and `_oencgo`
reloads the outdoor map at `_ccx` (a sea fight's ship is moored there). On a location map (`_cmapx` / `_cmapy`) nothing
moves the party: `_denc` and its callers ignore the end code, so the event or pass goes on from the fight's square
(D4-070 for Teleportation). Party slot 6 (a summoned elemental) is removed first: if `_par`+0C = 7, slot 6's condition
(+0A) is cleared and +0C is restored from `_svpmcnt` (`0x24AE6`).

`_esub01`: shows "The surviving members gained N experience (and M gold)", then the party experience pool `_par`+18 +=
experience / 10 and party gold += gold (both capped at 999 999 999; `towns.md` §2).

`_esub02` (treasure): chance = `_emlevel` + 18 % outdoors / 45 % in a dungeon (rand(100) ≤ chance). Then each of the
three treasure ids of the encounter (DATM +3A / +3C / +3E of the group, filled **outdoors only**) is offered with
`_findit` (if the pack has a free slot, the player may take it). Then, if the sum of the ids plus an **uninitialised
local** is non-zero (original bug `[~]`): outdoors always, in a dungeon only with rand(100) ≤ 64 (64 %), one more item from the table at
`0x443CA` (index rand(2 × `_emlevel` + 6), > 30 → 0). **No skill is read** (Spot Item is not used).

`_findit(item)` (`0x24664`, s140 static `[C]`): ids outside 1–156 are skipped; the first free slot of the 128-byte pack
is looked up and **with none the item is passed over with no question and no message**; else "<item>" and the question
(`0x4445B`) are printed, `_mkicon(2, 0x4438A, 1, "Found Item(s)")` puts up the panel O:拾う (pick up, icon 34) /
[original text] (leave, icon 35), and `_mgetc`: **O (button 0x443A6) takes it** into that slot ("…" `0x44477`), **any other key
leaves it** (no `_confirm`). The remaster asks Y / N (the panel's O / C press Y / N) and passes over a full pack the same
way (s140).

`_spoile` (`0xEC4A`, potions): a potion goes bad ("This potion has gone bad!") with rand(100) > 90 and does rand(8)
damage (`_ddamage`).

## 12. Against the manual (decided: manual, X68000 code as option; amounts user s84 — see `DIFFERENCES-IV.md`)

Filed s82 in `DIFFERENCES-IV.md`: items 1–6 → D4-042 … D4-047, item 7 → D4-036 (with Spot Item),
items 8–12 → D4-048 … D4-052; the Throwing skill (below) → D4-037.

1. **Attack / Slash strike count** (manual p. 43: Attack 「敵を2度攻撃します」 "attacks the enemy twice"; Slash 「敵に3～4度
   素早い攻撃をしかけます」 "3–4 quick attacks"). Code: one hit roll and one damage roll per action for every weapon action
   (`_chrexe` `0x28322`, `_chatkp` `0x27E18`); Slash's n = 3 or 4 divides the hit chance and is subtracted from the
   damage instead. The damage offsets match (Thrust − Attack = +2; Slash 2–3 below Attack, manual 1–2). The menu
   thresholds (Attack from Hm 75, Slash from Hm 150) are the manual's "low proficiency" rule. Manual rule (user s84/s86):
   each strike rolls Hm − 10 (Attack) / Hm − 20 (Slash); Attack F = 0, Slash F = −(n − 2) (1–2 below Attack).
2. **Losing the head or torso** (manual p. 46: 「頭部や胴体を失ってしまえば、ヒット・ポイントには関係なしに死んでしまいます」 "if
   the head or torso is lost, the character dies regardless of HP"). Code: a party member becomes "Out" (`_pc` 4) but
   stays alive (condition 1, HP kept; `_setpc` `0x1D3C`, `_pdamage` `0x1E940`) and can be healed back; monsters do die.
3. **MP costs** (manual pp. 50–53): Slow 1 / 2 cost 1 / 2 (manual; the spell list says 2 / 3) vs code **2 / 3**;
   Arrow Flame 2 costs 3 and Party Arrow Flame 1 costs 2 (manual p. 50 and the spell list) vs code **2 and 3**
   (swapped); Dissolve costs 1 (manual p. 51; the spell list says 3) vs code **3** (table `0x44B7C`).
4. **Party Arrow Flame** (manual p. 50: 「パーティ全員の弓の命中率を高め、相手に与えるダメージの量を増やします」 "raises the
   whole party's bow hit chance and damage", 50 % / 100 %). Code: damage +15 / +25 only, no hit bonus (`_pmagic1`
   `0x2BCC4`); the single-target Arrow Flame does add 50 / 100 to the hit chance.
5. **Auto-combat spell sets** (manual p. 32: attack 5–8, 11–12, 14, 20, 38, 43, 48, 59–62; defence 1–4, 9–10, 13,
   16–19, 40). Code (`_try_cast` `0x1EFE2`, tables `0x44146`, `0x44162`): also casts Confusion 2–4 (21–23), Party Arrow
   Flame 1 / 2 (26, 27), Party Accuracy (29), Mind Blast 1–4 (34–37) and Awaken (46); never casts Sleep (38) or
   Dissolve (48).
6. **Flameball** (manual p. 52: 「これを唱える者の周りにいる者は、敵・味方を問わず火の玉の被害を受けます。強力な鎧を着ている者には
   通じません」 "everyone around the caster … does not affect those wearing strong armour"; radius 1 / 2 / 2.5 / 3). Code:
   centred on the chosen target square, armour subtracted but every creature hit takes at least 1, and ranks 3 and 4
   cover the same 21 squares (`_pmagic2` `0x2A5A0`, count table `0x4494F`).
7. **Spoils** (manual p. 44: dungeon treasure 「パーティのメンバーが財宝を見つける高い技能を身につけていると」 "if a member has
   a high skill at finding treasure"; 「アウトドアで敵を全滅させた時、ダンジョンで敵が降参した時に、相手が財宝を持っていれば、
   自動的に手に入ります」 "obtained automatically when the enemy is wiped out outdoors or surrenders in a dungeon").
   Code: one fixed-chance roll (`_emlevel` + 18 % outdoors, 45 % in dungeons) for every won fight, surrender included;
   no skill term; dungeon encounters carry no monster treasure ids (`_esub02` `0x24774`).
8. **Night watch** (manual p. 44: 「見張り役の聴覚（Listen）が良ければ、遭遇する前にパーティ全員を起こすことも出来ます」 "if the
   watch's Listen is good he can wake the whole party before the encounter"). Code: a successful Listen roll only
   removes the monsters' free first round; the other members stay asleep (`_oencchk` `0x1668C`).
9. **Beg for mercy** (manual p. 40: 「パーティの所持金や品物を相手に渡して立ち去ることになります」 "you hand over the party's gold
   and goods and leave"). Code: party gold → gold mod 256; no items are taken (`_enc_end` `0x24AE2`).
10. **Weakness** (manual p. 50: 「敵の強さを奪うので、攻撃を受けてもダメージが少なくてすみます」 "saps the enemy's strength, so its
    attacks do less damage"). Code: lowers the monsters' +1C (the penalty to the party's hit chance) to 80 / 60 / 40 /
    20 % and cancels a monster Strength bonus; the monsters' damage value (+18) is untouched (`_pmagic1`).
11. **Spell damage ranges** (manual pp. 49–50; spell list): Fire Flash 1–10 / 30 / 60 / 100 vs code rand(N) × Int / 15
    (Int 18 → up to 1.2 N; Int < 15 never reaches N); Mind Blast 1–8 / 18 / 30 / 44 vs code 1 … N − 1, and no effect
    on monsters whose experience is a multiple of 7 below 2000. The manual (p. 48) also says offensive spell effects
    depend on 「となえる者のレベルや呪文のランク」 "the caster's level and the spell's rank"; the code uses Int (Fire Flash),
    max MP (resistance roll R) and the encounter level (the groups' mean level, §1.1; Sleep, Fear, Dissolve, Dispel Undead), and the caster's
    level only for Fear, Dispel Undead, Teleportation and Resurrection.
12. Smaller wording mismatches (candidates; the user may judge them not conflicts): **Slow 2** "enemy stride to ¾"
    (p. 53, as printed) vs code ¼ (`_mcalmove`); **Dissolve** "slowly dissolves" (p. 51) vs all monsters at once on one
    roll; **Fear** "the enemy flees in terror" (p. 51) vs only lowering the encounter level by 6 `[~]`;
    **Teleportation** "to another place" (p. 51) vs back to the previous square.

Checked, no conflict: hit location by weapon (p. 46); healing points light / serious / lost (p. 45, `_heal`); a serious
arm or leg wound forbids bows, a serious right arm leaves only Parry (p. 46; a left-hand throw is still possible);
diagonal MV 1, 2, 1, 2 (p. 42); Lunge Fighters only; Thrust needs a near enemy (Thieves reach 2); Protection +1 / 3 / 6
/ 10 replacing the earlier one (pp. 52–53); Speed +2 / +5; Ninja stride 11 / 14; Shock Wave 7 / 15 / 24 / 35 to friend
and foe; Healing 1–9 / 27 / 57 / 99 + wounds (p. 55); Resurrection may fail (p. 55); Awaken also counters Sleep (p. 51);
the Throwing skill (manual p. 21: "practically the same skill as Fire Bow") — the code uses Fire Bow for thrown hits
(but never reads the Throwing value itself, so it was filed after all: D4-037);
**Elves +15 % with bows** (p. 12): `_calpow` adds 15 to the Elf's Fire Bow ability bonus (+49 = Str + 4 Dex + 15 −
50, floor 0) `[C]`; how +49 reaches the hit value +48 is `_calsk` `[~]` (`skills.md`).

## 13. Open `[?]`

- `_mdamage`/`_pdamage` paths for `_eexitc` 4 inside `_pdamage` (`_chk_range(0x18, 6, 1)`). (When `_einf1` /
  `_einf2` are cleared: after round 1, §1.3, s98.)
- (s102: the walk §7.1, the targets §7.2 and `_obj_select` §4.1 are decoded; `_b_chkedge` with `_player` set (M auto
  move in manual mode) only reports "on an exit" and does not leave.)
- Monster ids 15, 24–29, 39–65 other than those in §9.3 when cast by monsters; ids 66–69 meanings beyond §4.
- Whether condition 2 is "Sleep" or a shared sleep/paralysis state everywhere (`_healch` shows "Sleep").
- (+3B and the per-monster HP formula: checked s82 at CPU level, §1 and `../FORMAT.md` §6.4; no full-system emulator run.)
- MSX2 cross-check of this whole spec.
- (s110: `_fmark` (`0x7B4E0`, 49 longs) is decoded: only `_b_chbtn` (`0x1D282`) writes it — it clears all 49 and marks the
  Chebyshev box of radius r around a square, clipped to the field. `_chk_range` saves and restores it around its own
  `_b_chbtn` call. Its other readers (`_try_attack`, `_obj_select`, `_bf_checker`, `_b_smbtn`) serve moves and aiming;
  Flameball and breath each call `_b_chbtn` right before their loop, and no call inside those loops (`_f2f`, `_shoot`,
  `_pdamage`, `_mdamage`, `_chr_here`, `_mon_here`, `_rand`) reaches `_b_chbtn` other than through `_chk_range`. So it is
  never stale at cast time: Flameball's box is (target, rank), breath's (monster, r).)

## 14. Remaster build (s98, `P4Battle`)

`src/OpenPhantasie.Core/Phantasie4/P4Battle*.cs`, data `P4BattleData` (monsters.json, items.json, `rule-tables.json`
`combat.battle`), driven by `Phantasie4Campaign` (a loop's Fight prompt starts it; `Follow` hands the outcome back:
101–105 won, 1 lost, the rest not won). Built as §1–§11 with every D4 option the fight touches (D4-036, 037, 042, 043,
044, 045, 047, 049, 050, 051, 052, 055). Choices where the code is undecoded, tagged `[~]` in code too:

- **Steps** (s102): the decoded walk §7.1 and targets §7.2 for monsters and the party's flight; monster melee / spell
  aim by `_obj_select` (§4.1); breath as §9.3; Greet with `_emcham` (§8).
- **Monster MV** `[~]`: +38 with the Speed / Slow modifier and the wing table each turn; the code's carry-over adds to an
  uninitialised stack local (§7), which the remaster does not emulate.
- **Flee:** as §8: members on an exit leave at once, the others walk toward the nearest open exit, then give orders. A
  member in manual mode may also choose **Leave** (X) on an exit square (remaster; the code's manual mode has none).
- **Greet / Beg / Demand refused:** the members have no orders that round (Parry).
- **Event fights** (s110, `_monevf`): built as decoded (`P4Battle.EventFlag`, data `combat.battle.event_monsters`).
  Reading `[~]`: the manual Fear (D4-052) still makes an event fight's monsters run.
- **+3B and +29** (s110): the fight reads the record's melee bonus +3B (`P4Member.MeleeBonus`, written by `_exittown`)
  and stride +29 (`P4Member.MoveBase`: creation, Pixie Dust, Motion Potion) as `_set1` / `_calmove` do `[C]`.
  **Equipment** is still chosen when the fight starts (the code runs `_equip` only in town: leaving, the Armory, Trade,
  Distribute); the two differ only when a Strength change outside town crosses an item's requirement `[~]`.
- **Fire Flash by a monster** hits the nearest member; **breath** as §9.3 (rings r = 1 … 6, n + 1 hits).
- **Draw order** (s110): Mind Blast draws rand(N) before R (an immune target draws nothing), breath 67 draws the unused
  rand(16), the code's Flameball walks `_xflam2` in table order (data `combat.flameball_code_offsets`) and drops squares
  outside the `_fmark` box (no row wrap), all as §9.1 / §9.3. Under the manual options the Mind Blast order is the same
  (the manual says nothing about draws).
- **Success rolls** (s110, `P4Combat.SuccessRoll`): R, Fear and Resurrection compare unsigned like the code (§9, "R
  compares unsigned": a limit below 0 always works) under the code option `p4.spell-damage` (D4-051, which covers these
  rolls); under the manual option they stay signed (a limit below 0 never works; the manual gives no success chance).
- **Dissolve (D4-052 manual):** each monster's HP spread over this round and the next two (type 0 damage); deaths pay
  their experience as kills.
- **Wound messages** (s102, `_getpd` / `_getmd` `[C]`): "%s's %s is %s!" (`0x440D3` party, `0x44226` monsters) when a
  part's wound level **rises**, with the part name (`_body`: head, body, right / left arm, right / left leg; monsters of
  shape 2 "body", shape 3 "body" but the head, shape 4 "right / left wing"; party fliers' arms are wings, the elemental
  "body") and the level word (`_hurt`: wounded, badly wounded, lost). The code prints them only while the combat
  message setting (`contr` 2) is not 0; the remaster always does.

- **After the fight** (s102, `P4FightReturn`, §11): a flight or Beg outdoors goes back to the outdoor loop's old square
  (a sea fight moors the ship there); on a location map it stays. Teleportation follows `p4.teleport` (D4-052, D4-070
  PENDING): `previous` = the old square on both map kinds (the dungeon's STEPBACK square), `random` = a random square of
  the map — outdoors a land square with a battlefield, on a location map a square with an open side that is not water
  `[~]` (a ship stays moored where the party fought) —, `entrance` = the square where the party came onto the location
  map `[~]` (outdoors: the previous square). A fight on a location map's arrival square, before any step, has no previous square there (the code's `_oldcx` still holds the last map's): Teleportation then stays `[~]`.

- **Shock Wave** (s102): as §9.1 (`P4Battle.WavePath`, data `combat.shock_wave`).
- **Annihilate Enemy** (s117, QoL `p4.annihilate` = `divine`, D4-071; not an original rule): every monster on the field
  takes 2M + rand(2M) − 1, M = the caster's MP at the cast; no roll, immunity, armour or wound; MP 0 (`P4Battle.Annihilate`).

- **Cast facing** (§9.4, `P4Battle.FaceOff`): built as decoded. The loops that turn creatures now go in square order, so
  the resistance rolls do too (Confusion, Weakness, Mind Blast, Sink, Dissolve; monster Sink and Mind Blast). Readings
  `[~]`:
  - Monster Sink turns only the members it lands. The remaster keeps its own walkable-square check for landing.
  - Under the manual Dissolve (D4-052), the monsters stay on the field over three rounds. They turn toward the caster
    at the cast all the same.
  - Under the manual Flameball (D4-047), the centre is the caster's square. There is no first caster ↔ target turn,
    and the pattern turns toward the caster.

- **Auto mode** (s102, `P4Battle.Auto.cs`): §8.2 as decoded (s110: `_bturn` and the `_atk_list` memory with its slip
  are built as decoded, `P4BattleMember.AttackMemory`; the remaster's Melee still drops an order whose monster is out of
  reach `[~]`; s110: `_try_cast` as §9.2's details, `_chosenThisRound` for `_splft`), with these remaster readings
  `[~]`: a dead member may get Resurrection when his Constitution is not 0 (the code's walk-byte test is not
  modelled); "in force" for lasting spells is any spell auto-chosen earlier in the fight; E = 2 never casts; Slash beyond the skill falls back to Attack, then Thrust (manual p. 43); a covering member (D = 2) without a
  chosen protégé does not move (the code asks "Whom does %s support?"; `P4Battle.SetSupport` sets it); M is one step
  (auto move + start) and sits on Q in the Client (M is Aim Blow among the order keys). Strategy bytes:
  `P4Member.Strategy` (saved); the profile / party-strategy screens that edit them are not built.

- **Summon Elemental** (s102): §9.1 with `P4Party.Elemental` (`_par`+14, saved; the guild screen that sets it is not
  built) and `elementals.json` (ELMS, with the strategy bytes and +3B); slot 6 lives in the battle only.

Found items under D4-036 manual (s110): the code has no identification state (item names always show; Identify only
opens the appraisal screen `_ident_mi`), so "a high Spot Item reveals the item's nature" is built as the game's own
appraisal line (`spells-noncombat.md` "Item appraisal texts") shown under each found item when the best living Spot Item
reaches the amount (50, user s84) `[~]` (the remaster does not track which member's roll found it). A fight cannot be
saved (F5 refused in the Client).
D4-058 (Aim Blow, decided s99: manual) is built as an option: `manual` lands an Aim Blow on the head (rand(12) ≤ 3) or
torso, except on type 5; `x68000` keeps the ordinary melee roll. MSX2 and PC-98 match X68000 (only −15 to hit).

## 15. PC-98 cross-check (s201, static `[C]`)

The PC-98 release (1990-09-14, the version whose manual is the source of truth) against §1–§11. Static only: no PC-98
emulator is installed (no np2kai / MAME on the machine), and none was needed once the code read the same.

**Binary.** `P4.EXE` (244,409 B, sha1 `f7ec5095…a760`) off `Phantasie IV (Disk 1).hdm` with `mcopy`; MS C 1988, large
model, Microsoft EXEPACK. `tools/scripts/pc98_p4_unpack.py` gives the load image (0x75440 B); DGROUP is paragraph
`0x6B97`, so DS:d = image `0x6B970 + d`. Floating point goes through the MS C math helpers (`lcall 2214:xxxx`, `pow` at
`27A0:0497`) with the constants in DGROUP. `rand(n)` is `627C:008B`. No symbols: functions were found by the strings and
tables they use; the names are the X68000 ones they match.

**Data.** Byte-for-byte (as 16-bit ints where X68000 has 32-bit `int`): the bow / thrown class bonus tables K (DS
`0x7672` / `0x7686`), the 24 edge squares of §8.1 (DS `0x7AC4`), the random-defence list {9, 10, 13, 16, 17, 18, 19}
(DS `0x79EA`) and the attack-spell walk 8, 37, 7, … 59 (DS `0x79F8`) of §9.2; `TABL`, spell costs and the spell pages
were already known identical (s82, s104). Every combat constant of §3–§4 (100, 0.5, 0.2, 1.2, 2, 115, 0.7, 0.6) is in
DGROUP and is the one the matching code loads.

**Code read, all identical to X68000:**

| Rule (spec) | PC-98 (image offset) | Result |
|---|---|---|
| Hit chance §3.1 (`_chatkp`) | `0x1A8B0` | melee Hm (÷ Slash n), − defence if ≥ 0; Aim Blow −15 / Lunge −30, floored at 0 (the action word 0x0B / 0x0D); bow / thrown Hr − trunc(defence / 4); target `_mc` ≤ 2 or = 4 → 200 |
| Leg / off-hand §3.1 (`_chlostp`) | `0x1A48F`, `0x1A4DA` | leg (`_pc` 2) 3c/4; off-hand c/2; off-hand damage 2d/3 |
| Action offset F §3.2 | `0x1A4FD` | Thrust +1, Attack −1, Slash −n, else 0 |
| Damage §3.2 (`_chatkd`) | `0x1A567` | bow / thrown trunc((r/100)^0.2 × Wr × 1.2 − armour / 2) + K[class]; < 0 → 0, = 0 → 1 (unsigned `cmp 1 / sbb`); melee trunc((r/100)^0.5 × Wm − armour) + F, ≤ 0 → 0; bare hand rand(rand(2L + 18) − 1) − 1 + rand(rand(3L − 1)) − armour; then + `_epstr`, cap 255 |
| Hit roll, bare-hand strike §3.2 (`_chrexe`) | `0x1B192`–`0x1B275` | hit if rand(100) ≤ c; class 6 with no weapon: rand(100) < 25 → "clean strike", + L + 10, else the critical |
| Critical §3.3 (`_critical`) | `0x1A3E6` | items 35 / 36 / 74 → 35, 59 → 75, 62 / 75 → 50, 63 / 84 → 40, 83 → 25; critical if rand(100) < t |
| Monster turn §4.1–4.2 (`_monexe`) | `0x1F847` | asleep: rand(100) < 15 wakes; acts if rand(100) ≤ 5p² + 3 × `_emlevel`; spell pick up to 10 tries, re-picked when in force: 9–14, 16–19, 30–33, 38 ("spell already in force" for monsters); attacks n = max(1, trunc(+16 / 115 + 0.7)); hit rand(100) < trunc(+16 / n) − D (+35) or target `_pc` < 3; damage (r/100)^0.5 × +18; drain magic MP × 0.6 + 0.5 (still dead code: 35 is cast as Mind Blast first) |
| Encounter options §8 (`_enc_opt`) | `0x21D4B` | Greet rand(100) × (1 − `_emcham`) > (50 outdoors / 90) − nice; Beg rand(100) < 90 + nice outdoors / 60 + 3 × nice; Demand rand(100) ≤ c × XP / first XP + 2 × `_par`+0B − 3 × `_emlevel`, c = 25 / 15; ends 106 (greet) and 3 (beg) |
| R rolls §9 "R compares unsigned" | `_pmagic1` `0x16DFB` (4 sites: `0x1712D`, `0x17478`, `0x177DE`, `0x179B9`), `_pmagic2` `0x289E8` (2) | (caster +37 − monster +24) × 5 + 50, compared unsigned (`jb` / `jae`), as X68000 |
| Resurrection §9.1 | `0x28B5E` | target dead (+0A = 0) and Constitution (+32) ≥ 1; rand(100) ≤ (caster L − target L) × 5 + 50 unsigned; a random free square in 10 tries (rand(49) − 1, walk byte `_pos`+1 ≠ 0); condition 1, HP = max, `_heal(10)` |
| Fear §9.1 | `0x28C9C` | rand(100) ≤ (L − `_emlevel`) × 5 + 50 unsigned; `_emlevel` −= 6, or 1 when ≤ 6. Nothing else: no rout, event fights or not |
| Auto Resurrection §9.2 (`_try_cast`) | `0x1E81F`, `0x1E96A`–`0x1E9E7` | a dead member is offered Resurrection when 40 is not chosen this round and his square's walk byte (`_tmark` → `_pos`+1) is set, as X68000 |

So the PC-98 program is the same C source as the X68000 one for combat: no rule, constant or table checked differs, and
no PENDING entry is needed. What this does **not** settle: the §14 `[~]` items are the remaster's own readings where it
does not copy the code (the Constitution test for auto Resurrection instead of the walk byte, "in force" for lasting
spells approximated, equipment picked at fight start, the manual Fear still routing event fights). For auto
Resurrection and Fear PC-98 has the X68000 code's behaviour (walk byte; Fear only lowers `_emlevel`), so those two
stay `[~]` as remaster choices against a code side now confirmed on two platforms; party "in force" (`_splfc`) and
equipment were not read on PC-98. Not read on PC-98: initiative / MV (`_calmove`, phases), `_set1` (whether it calls `_equip`), the
auto-flight HP thresholds, monster spells `_mmagic`, spoils §11.
