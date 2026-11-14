# Phantasie III — the combat round: turn order, monster turns, sleep, round start and end

Phase 09 T4 3c.6. Static unless marked live. Code: DOS `MAIN.EXE` root (`../FORMAT.md` section 1, `../FUNCTIONS.md`).
Tags: `[C]` read from code / data bytes, `[~]` probable, `[?]` not decoded. `rnd(n)` = `rand() % n + 1` (root `0x2c31`).
Already specced elsewhere (not repeated): action menu and melee to-hit `combat-actions-and-ranks.md`; injuries and
status-from-body `body-areas.md` section 3; bows `bows.md`; groups, Greet / Surrender / Mercy / Flee and the give-up test
`encounters.md`. Conflicts are not decided here (`DIFFERENCES-III.md`).

Names: party slot `i` = 0..6 (6 = an elemental), record at `FAR:0x236 + 0x106·i`, status `+0x2c` (Dead 0, Okay 1,
Asleep 2, Out 3, Down 4); party size `FAR:0xb614` (elemental included while it is in the fight). Monster groups R1
`FAR:0xe5b0`, R2 `FAR:0xe63a`; member `m` = 0..4, status `+0x18 + 2m` (dead 0, Okay 1, Asleep 2, Down 3, Out/"U" 4),
count `+0x02` (living members; `damage_monster` `0xb33d` decrements it). Round choices: `FAR:0x228 + 2i` (action
code, `combat-actions-and-ranks.md` section 2), spell `FAR:0xb57e + 2i`.

## 1. The fight loop — `combat_main(mode)` (root `0x104c0`) `[C]`

`mode`: 0 normal, **1 = the monsters get a free round first** (ambush, "[original text]"), **2 = the party
surprised the monsters** (sleeping monsters, "[original text]"). Callers: `encounters.md` section 7, `dungeon-events.md`.

```
fight start:
    clear: no-treasure 0xe598, failed-flee 0xb57c, Awaken 0xb5c0, elemental-formed 0xb576, dissolve 0xcca/0xccc,
           give-up 0xb630, teleport 0xb638, tall flag 0xcc2, elemental-in-fight 0x4880, hurt flag 0xb618,
           outcome 0xe59c = 0, kill counters 0xfd22 / 0xcba
    fight_on 0xb61e = 1; gold 0xb62c = 1; wake_chance 0xb628 = 15
    start strength 0x9816 = strength() (0xf460); start counts 0xb640 = R1 count, 0xb7f4 = R2 count
    0x10331: install the combat menu bar ("Options" → encounter menu, root 0x10424 → 0x1044f)
    0xd55b (decoded in full): for each member: spell slot FAR:0xb57e[i] := +0x70 (the saved Cast's spell number,
            written by the menu at 0xff03); a := +0x6e (the saved action code, combat-actions-and-ranks.md section 2), then
            Attack% (+0x30) < 76 and a = 0 → 3; Attack% < 150 and a = 2 → 3; Down and a ∈ {4, 15} → 3; right arm ≥ Broken
            (0xb550) → 5 (Parry); no bow (+0x86 = 0) and a ∈ {4, 15} → 3; +0x6e := a (written back);
            menu FAR:0x228[i] := a, and with no R2 group (FAR:0xe63c ≤ 0) the jump table at 0xd5c4 maps 1, 12, 14 → 3,
            11 → 0, 13 → 2, 15 → 4 (16, Cast R2, kept); +0x6e keeps the unmapped code
    0xd4ff (fight end): +0x6e := FAR:0x228[i], +0x70 := FAR:0xb57e[i]. Creation writes +0x6e = 3, +0x70 = 0 (ovl2 0x255e)
first = true
while fight_on:
    if first: redraw; 0xd730 per-fight stats (attack %, damage, bow %, defence); first = false   # once per FIGHT
    if mode == 1: combat_round(1); mode = 0                                                        # free monster round
    if not fight_on: break                                                                         # (0x1074e)
    party_menu()  (0xf9ca)                               # Options = encounter menu; pick members, change actions
    if failed_flee (set by Flee in the menu): mode = 1   # "the monsters get free shots"
    combat_round(mode); mode = 0; failed_flee = 0
    if elemental_formed: party size += 1 (it acts from the next round on); elemental_formed = 0
fight end:
    0xd4ff: record +0x6e = this fight's last action, +0x70 = last spell (the next fight's defaults)
    if elemental in fight: party size −= 1
    0x1d04: every member: status = 1, then status-from-body (0x107b)   # wakes all sleepers; HP ≤ 0 stays Dead
    return outcome 0xe59c
```

- **Mode 2** is not cleared before the loop, so the first `combat_round(2)` runs **with the party only** (section 2): the
  party gets a free round as well as the free Flee of `FAR:0xb5c2` (`encounters.md` section 4). `0xb5c2` is cleared at the end of
  the first round that leaves the fight on (`0xdd64`), so the free Flee holds only at the first menu `[C]`.
- **Failed Flee** (`0x10fd8` sets `0xb57c`): the same pass runs `combat_round(1)`, a monster-only round, and the party's
  chosen actions are not carried out ("free shots", manual ✓). A failed Greet / Surrender / Mercy does not set it, so a
  normal round follows.
- **Successful Greet / Surrender / Mercy / Flee** set `fight_on = 0` inside the party menu, but `combat_main` calls
  `combat_round(mode)` after the menu **without testing `fight_on`** (`0x10756…0x1077e`); the loop only checks after
  the round. So one full round (party actions and monster attacks) is still played after the monsters accept `[C]`
  control flow, effect `[~]` (live check wanted). In that round the end-of-round checks (section 5) see the fight already over.
- The Encounter menu is the "Options" item of the combat menu bar (DS `0x95e` → callback `0xf9c:0a64` = root `0x10424`).
  Options 0–4 (Fight, Surrender, Greet, Mercy, Flee) close the menu and start the round; Identify, Party Health and Sound
  keep it open `[C]`. Actions **persist between rounds**: only a member the player picks gets a new action; Asleep, Out
  and Dead members cannot be picked (`0xfad9`) `[C]`.
- `0x10be4` (Parry / Thrust forced by broken limbs) is not called by the round; it runs while the menu lists the party
  (`0x108b1`, `0xfa22`) and from the action menu `0xfc41` `[C]`. `0xd730` runs **once per fight**, not per round, so
  defence and attack % do not follow injuries taken during the fight `[C]`.

## 2. Turn order — `combat_round(mode)` (root `0xd8c7`) `[C]`

There is **no initiative roll, no passes and no DEX or level term** (unlike I's three-pass scheduler,
`../../logic-specs/combat-resolution.md` "Turn order"). Each of 17 actor slots acts at most once per round:
actor 0..6 = party slot, 7..11 = R1 member 0..4, 12..16 = R2 member 0..4.

```
order[0..16] = −1
if mode != 2: order[a] = a for a = 7..16                       # monsters (mode 2: none)
if mode != 1:                                                  # party (mode 1: none)
    k = 0; back = party_size − 1
    for i in 0..party_size−1:
        if action[i] is Fire Bow (4) or Fire Bow R2 (15): order[k] = i; k += 1   # archers first, party order
        else: order[back] = i; back −= 1                                         # the rest, reversed
else: k = 7
repeat 17 − k times:                                           # partial shuffle of slots k..16
    a = rnd(17 − k) + k − 1; b = rnd(17 − k) + k − 1; swap(order[a], order[b])
for j in 0..16: act(order[j])                                  # −1 = nobody
```

- **Bowmen always shoot first**, in party order, and are never shuffled `[C]`. Manual: "Each figure will act as his turn
  comes around" — silent on the order; no conflict.
- Everyone else (non-archers, empty party slots, all ten monster slots) is mixed by `17 − k` random swaps. That is not a
  uniform shuffle: about one slot in seven keeps its starting place (non-archers early in reverse party order, then R1,
  then R2) `[C]` code, `[~]` the odds.
- The status is read when the actor's turn comes, so a member killed or put to sleep earlier in the round loses the turn.
  There is no early exit when one side is wiped out; dead actors just skip `[C]`.

**Party actor** (`0xd9d7`, `0xdaef`):

```
if status == Asleep: if rnd(100) ≤ wake_chance: awaken(i) ("<name> awakens", status from body)   # turn used either way
elif status in (Dead, Out): nothing
else:                                           # Okay or Down
    if action in (Cast 7, Cast R2 16): cast(i, spell[i])          # 0xce66 → 0xcaac / healing / Resurrection
    elif action in 11..15:  character_attack(i, R2)               # 0xc03b
    else:                   character_attack(i, R1)               # Parry (5) returns without acting (0xc104)
```

**The cast (`0xce66`, party slots 0–6) `[C]`:**

```
if spell in 1..4 (Healing):  party_casts(i, spell); heal; magic −= cost[spell]
elif spell == 44 (Resurrection): party_casts(i, spell); raise (root 0x18b6); magic −= cost[spell]
elif FAR:0xe5b2 > 0 or FAR:0xe63c > 0:          # R1 / R2 living counts [C]
     party_casts(i, spell); magic −= cost[spell]; combat_spell_dispatch(spell, i)   # 0xcaac
else: nothing (no line, no magic spent)
party_casts (0xbf9a): line = name + " casts " + master string (331 + spell); sound 21; print the line
```

- The line names the spell (`"elrod casts Mindblast IV"`; the Amiga prints two spaces after the name, section 8.6 of
  `amiga/phantasie3/FORMAT.md`). The name is the record's NUL-terminated 7 chars, so DOS has one space. I's
  "X CASTS A SPELL" has no spell name. Remaster: `P3Fight.CastLine`.
- **The gate on the third branch** `[C]`: `0xe5b2` / `0xe63c` are the groups' `+0x02` living counts. Set at
  group build (`0xe3e4`, `0xf030…0xf138`); in a round the only writers are `damage_monster` (`0xb33d`: `dec [group+2]` on
  the death path, HP 0 / status 0; a monster put Out with HP left stays counted) and the round end's dissolve
  (`0xdc9e` / `0xdcd6`: count 0, statuses 0). The round end's victory test (`0xdbc5`, `0xdcf9`) reads the same words. So
  a non-healing cast (bow spells too: they go through `0xcaac`) after the last monster died earlier in the round prints
  nothing and spends no magic. Remaster: `P3CombatModule.CastGoesAhead` (any rank not wiped).

**The party's Fireflash I–IV (#5–8)** (`[C]` static, `0xcaac` → `0xc3cc` case 5–8 at `0xc3ef` → `0xb42e`; `[C] live`
below):

```
rank = the caster's order: 7 Cast (R1) → group R1 (FAR:0xe5b0), 16 Cast (R2) → group R2 (FAR:0xe63a)
if that group's living count (+0x02) == 0: nothing (no other rank is tried; the magic is already spent)
target = 0xb4ec(group): k = rnd(5) − 1; up to 6 looks: the first slot k, k+1, … (wrapping at 5) with HP (+0x18 + 2k) > 0
if no target: nothing
d = rnd((INT / 2 + 1) · t[spell])       # INT = record +0x0c; t = DS:0xc1a: #5 1, #6 4, #7 8, #8 13; rnd = 1…n
d = min(d, 10 / 30 / 60 / 99)           # by spell #5 / #6 / #7 / #8
damage_monster(group, d, target, rank, 1, 1)
```

- **One monster** of the rank the caster picked (the order screen asks the rank when rank 2 exists,
  `combat-actions-and-ranks.md` section 2). The pick is not uniform: a random start slot, then the first living
  monster at or after it, so a monster after a run of dead slots is hit more often.
- The roll grows with INT: the cap is reached at INT ≥ 18 (#5) and 14 (#6, #7, #8);
  below that the top damage is (INT / 2 + 1) · t. At least 1.
- **Amiga** `0x186f6(group, spell, rank, member)` (`amiga/phantasie3/FORMAT.md` 7.2 sound 39): the same roll, caps
  and rank — **DOS and Amiga agree**.
- **Manual** (III PDF 11 / pr.14, "Fireflash (#5-8): these spells shoot a blast of flame at one monster. The spell causes the
  following damage according to the level of the spell: Fireflash 1 1-10 points of damage, Fireflash 2 1-30 points of
  damage, Fireflash 3 1-60 points of damage, Fireflash 4 1-100 points of damage"): flat ranges, no INT, and IV reaches
  100. **D3-075, DECIDED** (user ruling): manual by default, this DOS rule as `p3.fireflash` = `dos`.
- `damage_monster`'s arguments (`0xb4cd`): `(group, d, target, rank, 1, 1)` = group far pointer, damage (floored
  at 1), slot, rank (1: the R1 kill counter `0xfd22`, else R2's `0xcba`), the **spell flag** `[bp+0x10]` and the location row
  `[bp+0x12]`. With the flag set, `0xb116` draws the flash (`0x1537`) and skips the injury step (`0xe6c6`) and the status
  from the body (`0xec1a`), so the row is never used: Fireflash takes HP only and kills at HP ≤ 0 `[C]`.
- Remaster: `P3CombatModule.CastFireflash` (both values; D3-075 "Remaster" has the details). With `dos` the order
  carries the rank (`CombatAction.CastRankTwoBase` = action code 16) and the order screens ask it. The numbers (t, the
  caps, the manual's maxima) are III's rule data: `rule-tables.json` `fireflash` → `P3FireflashTable`.
- **Live (s212, DOSBox-X, `parity/s212-p3-fireflash-live.md`)** `[C] live`: 74 casts, with all monster HP loss from the cast
  alone. The top damage follows INT: Fireflash IV at INT 3 never beat 26 in 12 casts, and at INT 12 never beat 91 in 28.
  Fireflash II at INT 5 reached exactly 12, so `INT / 2` truncates; Fireflash I at INT 5 reached exactly 3. At INT 20,
  Fireflash IV did exactly 99 three times in 14 casts and never 100 (cap 99). Magic cost 4 / 2 / 1 (IV / II / I).
  "[original text]" One / Two follows the spell pick when two groups exist. Code 7 hit only `FAR:0xe5b0`, and code 16
  hit only `FAR:0xe63a` (10 of 10). With three monsters alive, the target slots split 34 / 13 / 10, which fits `0xb4ec`'s
  60 / 20 / 20. The combat list prints the spell name with no rank mark. The monster list at the screen's foot puts rank 2
  on its first line. Fireflash III (t = 8) was not cast live.
- Remaster order line: the remaster's own orders list and order prompt (`CombatPresenter.OrderLabelFor`, the III hook
  `SpellName`) print a cast as the spell's name with no rank mark, as DOS's list and the Amiga's orders screen do.
- The flash on a monster (Amiga `0x1b814`, `combat_flash` in the pack) sits at the picture's top-left + (rnd mod 3) bytes
  across and 6 + (rnd mod 22) pixels down, the same rule for either rank `[C]` static. A spark low on a short picture (a
  Giant Bee, s212) is that rule, not a placement bug.

## 3. Monster turn (root `0xd069`) `[C]`

```
g = R1 for actors 7..11, R2 for 12..16; m = actor − 7 or − 12
if status[m] == Asleep: if rnd(100) ≤ wake_chance: awaken (status from body, "awakens")   # turn used either way
elif status[m] in (dead 0, Out/"U" 4): nothing
elif rnd(100) ≤ g.special_chance (+0x2c):                       # Okay or Down
    s = g.specials[rnd(3) − 1]                                   # +0x34 / +0x36 / +0x38
    cost = s % 4; if s ≥ 33: cost = 2
    if g.magic[m] (+0x0e + 2m) ≥ cost: monster_spell(s) (0xcad7); g.magic[m] −= cost
    else: melee
else: melee                                                      # monster_melee_attack(−1, g, m) (0xb64e)
```

- **No rank test: rank-2 monsters melee, cast and breathe exactly like rank 1** `[C]`. The only rank effect is the
  −15 on a character's chance to hit a rank-2 monster (`combat-actions-and-ranks.md` section 3).
- Down monsters act. The Down −40 in `0xb64e` tests member 0's status, not the acting monster's (known, section 4 there).
- Group word `+0x0e + 2m` (CREATURE.DAT byte 24, `encounters.md` section 1 "combat rating") is the member's **magic points**:
  specials spend it, and it is the monster side of the breath / Sleep resist roll below `[C]`.
- **Cost quirk** `[C]`: `s % 4` makes every 4th code free (4, 8, …, 32: Fireflash 4, Quickness 4, Strength 4, …;
  13 monster types carry one, e.g. Wizard, High Devil, Nikademus, Lord Wood), and a special slot of 0 passes the cost test, then
  `monster_spell` has no case for it, so the turn is lost silently (131 of the 240 special slots are 0) `[C]` (counts from `extracted/monsters.json`).

**Special codes** (`0xcad7`, jump table `cs:0x1d67` = img `0xce17`, 37 entries; byte table `t = DS:0x9c4`) `[C]`:

`t[1..32]` = 12 35 70 120 · 7 10 16 28 · 11 14 20 32 · 8 12 16 20 · 80 60 40 20 · 80 60 40 20 · 13 26 46 86 · 12 20 36 68.

| s | Effect (message) | Target |
|---|---|---|
| 1–4 | "&lt;name&gt; casts Fireflash": `damage_character(rnd(t[s]))` | one party member by `0x1124` (section 4) |
| 5–8 | "casts Quickness": `0xb403(10·t[s], 0x11)` = attack `+0x22` := 70 / 100 / 160 / 280 | **both** groups, every member |
| 9–12 | "casts Strength": damage `+0x24` := 11 / 14 / 20 / 32 | both groups |
| 13–16 | "[original text]": armour `+0x26` := 8 / 12 / 16 / 20 | both groups |
| 17–20 | `0xbb05(FAR:0xb59a, t[s])`: armour points × 80 / 60 / 40 / 20 %, then "[original text]" | each member not Dead that fails to resist |
| 21–24 | `0xbb05(FAR:0xfd10, t[s])`: attack % × 80 / 60 / 40 / 20 %, then "[original text]" | as above |
| 25–28 | "[original text]" (`0xba4e(s, 0)`): `rnd(13 / 26 / 46 / 86)`; resisted → nothing | each member not Dead |
| 29–32 | "[original text]" (`0xba4e(s, 1)`): `rnd(12 / 20 / 36 / 68)`; resisted → 1 | each member not Dead |
| 33 | "casts Sleep" (`0xb99b`): every member rolls; not resisted and not Dead / Out → Asleep | every member |
| 34 / 35 | melee touch `0xb64e(34 / 35)`: stun → Asleep ("is stunned!") / drain 40 % magic ("is drained!") | as melee |
| 36 / 37 | "breathes!" (`0xb582`): `rnd(20)` (36) / `rnd(36)` (37) − armour points, resisted → 1 | **every** member not Dead |

- Resist roll (all party-wide codes): `rnd(100) > 50 + 5·(the caster's magic − character's current magic +0x1e)`. The
  caster's magic is read before the cost is taken.
- Every spell hit calls `damage_character(d, i, 1, …)`: flag 1 = **no injury and no status-from-body**; `d < 0` → 0.
  So breath can deal 0. Sleep and 17–24 also call it with `d = 0` for each member they catch (the message it prints
  then is `[~]`, not reproduced).
- `FAR:0xb59a` = armour points (worn armour's points, at least 1) and `FAR:0xfd10` = attack % (Attack skill), both set
  once per fight by `0xd730`. The **party's** Quickness I–IV (#9–12, `0xc3cc`, table `DS:0xc1a`) rebuilds `0xfd10` as
  Attack + 10 / 25 / 45 / 70 and Protection I–IV (#17–20) rebuilds `0xb59a` as armour points + 1 / 3 / 6 / 10, for every
  member, so they undo the slow / armour codes (manual III pr.14: "an enemy Slow is overridden by Quickness" ✓).

## 4. Targets, sleep and Parry `[C]`

- **Monster target** (`0x1124`, used by melee, touches and Fireflash): start at slot `rnd(party_size) − 1`, walk forward
  (wrapping) to the first member whose status is **not Dead**; −1 (attack skipped) if none in 8 steps. Okay, Asleep,
  Out and Down are all eligible, the elemental too; **rank plays no part** in the choice (it only changes the to-hit,
  section 4 of `combat-actions-and-ranks.md`). Each swing picks again. A member after a Dead slot is picked more often `[C]`.
- **Monster swings**: `n = max(1, (attack + 80) / 115)`, but the swing counter `[bp−0x54]` is overwritten by a string
  length when a swing **misses** (`0xb79c`) or **stuns / drains** (`0xb91d`, `0xb726`). The value (≥ the name length)
  is at least `n` for every monster in CREATURE.DAT, so a monster's run of swings **stops at its first miss** `[C]`
  code, `[~]` effect (20 monster types have n ≥ 2; live check wanted).
- **Wake chance** `FAR:0xb628`: 15 at fight start, +15 after every round that leaves the fight on, at most 100 (15, 30,
  45 … 100 from round 7). Party and monsters share it; the roll is made on the sleeper's own turn and uses the turn.
  Being hit does **not** wake (status-from-body keeps Asleep, `body-areas.md` section 3, `0x107b` / `0xec1a`).
- **Awaken (#51)** (`0xca27`) sets `FAR:0xb5c0`; at the end of that round every Asleep party member wakes (`0xdd85`:
  each party slot with status 2 goes through `0xd301`, the same "<name> awakens" routine as a wake roll; then the flag
  is cleared).
  Monsters are not affected. After the fight `0x1d04` wakes everybody.
- **Party skips**: Dead, Out, Asleep (wake roll) skip; **Down acts** (Fire Bow becomes Thrust, −40 to hit in `0xc03b`).
- **Parry** `[C]`: the Parry action does nothing on its own turn (`0xc104`). Against monster melee the shield points are
  subtracted when record **`+0x6e`** is 5 (`0xb848`). `+0x6e` is written only at fight start (`0xd55b`, the saved
  option) and at fight end (`0xd4ff`); the round menu writes `FAR:0x228`, not `+0x6e` (all writers checked in root and
  the four overlays; the only other is creation, ovl2 `0x255e`). So **Parry chosen during a fight has no defensive effect
  in that fight**, and a member whose saved option was Parry keeps the shield bonus all fight even while attacking.
  No damage reduction exists (`0xb64e` passes `rnd(damage)` unchanged; no "parries" message is used) `[C]`, live
  check wanted. **D3-036** (user ruling, built): the remaster's default is Apple II's per-round skill roll plus the shield off the
  chance and off the first damaging hit; this DOS behaviour is the `p3.parry` = `dos` option.

## 5. End of round (root `0xdb22…0xde4e`) `[C]`

In this order:

1. **Party wiped**: no member that is not Dead and not an Elemental (race `+0xa8` ≠ 16) → `fight_on = 0`,
   `FAR:0xfc9a = 1`. Asleep / Out / Down count as alive; an elemental left alone counts as a wipe.
2. R1 monster 77 / 78 (Nikademus) or 79 (Lord Wood) with member 0 dead → outcome 2 / 3, fight over (`endgame.md`).
3. Give-up test `0xd171` → `0xb630` (`encounters.md` section 5).
4. Both counts ≤ 0 → won (outcome 1). Else give-up passed → "[original text]", won.
5. Teleportation (#43, `0xb638`) → "The party teleports away", outcome 0.
6. Dispel Undead (#49) dissolved R1 / R2 (`0xcca` / `0xccc`): that group's count and statuses := 0; if both groups are
   now empty → won with gold, and the kill counters are set to the **start counts** (`0xfd22 = 0xb640`, `0xcba = 0xb7f4`).
7. If the fight goes on: clear `0xb630` and the free-flee flag `0xb5c2`; wake_chance += 15 (max 100); Awaken (above).
8. **Ranks reorganise** ("the monsters will reorganize", manual ✓):
   - R1 count ≤ 0 and R2 count > 0 and the fight goes on → **swap R1 and R2** (`0x113e6`): R2 moves up.
   - otherwise, if `strength(R1) < strength(R2)` (`0xf48c`, level sum over odd-HP members, the `encounters.md` section 1
     quirk) and `rnd(100) ≤ 30` → swap (the stronger rear group comes forward). This test also runs after the fight ended.
   - The swap exchanges the whole 138-B records, the pictures, the start counts and the kill counters; group word
     `+0x46` is swapped twice and so stays with the rank position `[C]` (`[?]` what it holds).
9. **Compaction** (`0xd23e`, both groups): living members (status > 0 and HP > 0) move to the front slots, the rest are
   zeroed. Only HP `+0x04`, magic `+0x0e` and status `+0x18` move; the **body areas** (`+0x4c + 10k + 2m`) stay by slot,
   so a survivor that moves up takes over the injuries of the dead monster in its new slot (next status-from-body
   recompute uses them) `[C]` code, `[~]` effect.

No status is recomputed at round end; statuses change when a hit or heal happens (`body-areas.md` section 3) and when a
sleeper wakes.

## 6. Remaster (s90)

Built as `P3RoundResolver` (the III campaign's `ICampaign.CombatResolver`): section 2 turn order, section 3 monster turns with every
special code above, section 4 the shared wake chance and targets, section 5 steps 1–5 and 7–9 (the give-up test moved here from the
top of the next round); after compaction a mover keeps its status until its next hit or wake, then takes it from the
slot's body (`P3Fight.RefreshStatus`). Actions carry over between rounds; a failed Flee sets the next round to
monsters only and keeps them. Also built: Awaken's end-of-round wake (`P3Fight.AwakenAtRoundEnd`) and the fight-start orders from the saved option
(`P3CombatModule.LoadSavedOrders` / `SaveOrders`, record `+0x6e` / `+0x70` as `SavedAction` / `SavedCast`; a Cast keeps
the remaster's whole packed order). Not reproduced: the round still played after a successful Greet / Surrender / Mercy /
Flee (D3-037: a DOS bug), Dispel Undead's group zeroing (the shared dissolve), the 0-damage calls of Sleep / 17–24.

## 7. Open

- Live checks (3c.8): Parry chosen mid-fight; the stop-at-first-miss swing counter; body areas after compaction.
- `FAR:0xcbe` (the start picture / sound call `0xe0a7(3 / 4 / 5)` by mode) and group word `+0x46` `[?]`.
