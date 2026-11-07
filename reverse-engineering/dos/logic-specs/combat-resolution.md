# Combat Resolution

Owned by `OV2.OVR`. Message anchors (`OV2.OVR.strings.txt`) trace the whole
pipeline: encounter setup (`RANK %1d : %1d %s(S)` `0x7990`), per-swing
(`%s HITS`/`%s MISSES`/`%s TAKES %d DAMAGE`/`%s DIES`), reactions
(`[original text]/GREET/RUN/STOP AND TAKE YOUR GOLD`), rewards
(`%ld XP AND %ld GP`, `[original text]`).

## Model overview [X]
Combat is **rank-based**, not a positional grid. Monsters form groups arranged
in numbered **ranks** (`RANK 1: n <MONSTER>(S)`, `RANK 2: …`). The party of up
to 6 acts each **melee round**; you assign every member an action, then the
round is resolved and repeated until one side is dead / fled / surrendered / the
party begs mercy.

## Inputs
- Party "CV" in the spell / special resist rolls: runtime `char*0x11a+0x5bc0` = **`@16` MP max** (party base `0x5bb0`). [C] (s33; was
  mislabelled "CV `@14`" from the old `0x5bb2` base — `@14` is the MP ceiling). There is no separate party CV field.
- Monster CV: `mon*0x39+0x7547` (spell/breath paths). [C]
- Melee also uses separate weapon-hit / defense fields (below) — not always CV. [C]
- RNG helpers (see §RNG). [C]
- Damage / soak fields (see §Armor & weapon fields). [C]
- Packed action word at `0x74fa[char]` (see §Action encoding). [C]

## Party actions [C encoding / X labels]
Packed into `0x74fa[char]` (and mirrored at `0x5b96` while resolving):

| Action | Packed value | Swings | Rank index | Notes |
|---|---|---|---|---|
| PARRY | **3** | 0 | — | On hit taken: extra shield soak (below) |
| THRUST | **10** or **12** | 1 | 0 or 2 | `FUN_0000_6bc5` type 0 |
| LUNGE | **11** | 1 | **1** (rank 2) | type 3; fighters |
| ATTACK | **20–22** | 2 | 0–2 | type 1 |
| SLASH | **30–49** | 3–4 | 0–9 | type 2; swings = `@250/75 + 1` (§Action chooser) |
| CAST / specials | ≥105 (`value−0x68`) etc. | — | — | spell/item paths |

Encoding: `value = swings * 10 + target_rank_index` (except PARRY=3). [C]

```
swings     = action / 10
rank_index = action % 10    # indexes DAT_0000_74f4 rank→monster-group
```

Manual “THRUST harder / +dmg, SLASH weaker per swing” falls out of the formulas
below (fewer swings → larger per-swing hit chance & smaller action penalty).

- **Thieves** may ATTACK/THRUST/SLASH monsters in **any** rank (stealth). [X]
- Monsters attack with 1–2 attack sub-blocks from their record (see
  `data-tables.md`); some `BREATHE` or `CAST A SPELL`.

## Action chooser — OV2 `0x6bc5` [C] (s20)
Called per member while orders are given. Arg mode `[bp+4]`: 0 = repeat last round (type recomputed from the
previous packed action `0x5b96[i]`: <10 → PARRY, 10/12 → THRUST, 11 → LUNGE, 20–22 → ATTACK, 30–90 → SLASH, else
spell), 1 = menu choice (type in `[bp+8]`). Switch at `0x6f84`, table OV2 file `0x6f78` (`jmp cs:[bx+T]`,
file = T − **0x53dc**, same delta as OV1): 0 `0x6c82` THRUST · 1 `0x6ccd` ATTACK · 2 `0x6cf3` SLASH ·
3 `0x6d2e` LUNGE · 4 `0x6d75` SPELL · 5 `0x6f2b` PARRY.

```
THRUST:  swings = 1                                   # prints "THRUST"
ATTACK:  if @250 < 75:  goto THRUST                   # silently downgraded
         swings = 2                                   # prints "ATTACK"
SLASH:   if @250 < 150: goto ATTACK                   # may fall through to THRUST
         swings = @250 / 75 + 1                       # 150–224 → 3, 225–255 → 4; prints "SLASH"
melee:   rank = pick_rank(mode, i, swings)            # 0x6f94, 1-based
         action = 10·swings + rank − 1
LUNGE:   if class @220 in {THIEF 0, FIGHTER 3}: action = 11   # prints "LUNGE"
         else goto THRUST
PARRY:   action = (rand0_99 <= parry @32) ? 3 : 4     # 4 = no action this round
SPELL:   "WHICH SPELL?" … invalid/unusable → beep, then PARRY path
```
`pick_rank` (`0x6f94`): only class 0 (THIEF) is asked for a rank when more than one rank `[0x6494]` exists; everyone
else gets rank 1. In repeat mode the previous rank is kept, clamped to the current rank count.
`@250` writers in OV2: prep `0x3294` (= `@30`), QUICKNESS `0x04be` (cap 255 at `0x04b3`), and fixed pairs
`@250/@248` = 255/25 (`0x07f7`) and 200/20 (`0x09ab`), which belong to some special spell/summon `[~]`. So swings ≤ 4.
HEALTH REPORT `A:` (`0x45ed`) prints the same `@250/75 + 1`, i.e. the most swings a SLASH can get.
**Manual differences (user decided s20 → manual):** D-011 no skill gate, ATTACK 2 / SLASH 3, or 4 at `@250 ≥ 225`;
D-012 LUNGE for fighters only (others → THRUST rank 1). Core: `CombatAction.SlashFor` / `LungeFor`. The PARRY roll isn't mentioned in the manual (no conflict).
Core chooser (s21): `CombatOrders.Choose` / `GameSession.ChooseAction` — PARRY rolled at order time, thieves pick a rank,
an unusable spell takes the PARRY path. Client (s36): orders start as PARRY and repeat each round via mode 0 (`CombatText.RepeatFor`); each status-OK member is asked, Enter keeps, 1–6 changes.
**[D✓] s65 (§Orders):** the repeat array `0x5b96` is reset to 4 (PARRY) only by COM `0x7c`, called on leaving town (OV1 `0xd0`)
and after a party defeat (`0x84`); it survives rounds, encounters, a failed FLEE and a surprise opening (D-078; user s66:
manual — the remaster never resets them). A melee order at
a rank that no longer exists does nothing and prints nothing. HITS / MISSES name only the attacker ("VIPER HITS" then
"priesto TAKES 8 DAMAGE"). The thief's rank order reads "THRUST AT RANK 3".

## RNG helpers [C]

### `FUN_0000_655b` @ OV2 `0x655b`
```
raw = func_0x0000e5ca()          # PHANTASI.COM FUN_0000_3c96 LCG
return raw / 0x148               # 328 → result 0..99
```
`FUN_0000_3c96` updates 32-bit seed at `0x308c/0x308e` (`seed = seed*0x7d − …`),
then returns a 0..0x7FFF-ish value; `/0x148` maps that to **0..99**. [C]

### `func_0x0000dfa9` ≡ COM `FUN_0000_3675` — integer square root
Link map: OV2 extern `0xdfa9` ↔ COM `0x3675` (same delta as RNG/LDIV). [C]

Newton isqrt of the 0..99 roll → coefficient **0..9** (implementation quirks:
e.g. `isqrt(1)=0`; not always `floor(sqrt(n))`).

```
coef = isqrt(roll_0_99)           # FUN_0000_3675 / dfa9
```

### `func_0x0000f8d1` ≡ COM `FUN_0000_4f9d` → `FUN_0000_4ff3`/`4ff7`
Signed/unsigned **32÷32** (divisor often `BX:CX` with `BX=0`, i.e. 32÷16):

```
# call sites set DX:AX = dividend, BX=0, CX = divisor
quotient = (DX:AX) / (BX:CX)     # result in AX (and DX as needed)
```

## Armor & weapon fields [C] (re-derived s19)

Character record base in DS: `char*0x11a + 0x5bb0` ↔ disk offset 0 — in COM, OV1 **and OV2**
(s19 histogram: status `+0x18` = `0x5bc8` is OV2's most-used record field). Everything below was
first derived with base `0x5bb2` and was 2 bytes too low; this version is checked against the
pass-3/4 fixture bytes (all 6 characters reproduce `@246`, `@248`, `@250`, `@12`).

| Disk off | Runtime | Role |
|---|---|---|
| 12 | `0x5bbc` | **Defense rating**, written by combat prep (below) |
| 28 | `0x5bcc` | Weapon damage component A — writer **[?]** (see `save-file-format.md`) |
| 30 | `0x5bce` | Attack skill → copied to weapon-hit effective |
| 32 | `0x5bd0` | Parry skill → defense rating |
| 240 | `0x5ca0` | Weapon damage component B (worn weapon points) |
| 242 | `0x5ca2` | **Shield soak** (worn shield id = points) |
| 244 | `0x5ca4` | **Armor soak (base)** (worn armor points) |
| 246 | `0x5ca6` | Armor soak **effective** (PROTECTION adds) |
| 248 | `0x5ca8` | Weapon damage **effective** = `@28 + @240` (STRENGTH adds) |
| 250 | `0x5caa` | Weapon hit **effective** = copy of attack `@30` (QUICKNESS scales) |

### Worn-item selector — `OV1 0x2fdb` [C]
Called with the record pointer `0x5bb0 + i·0x11a` (OV1 `0x2f26..0x2f32`). Clears
`+0xf0/+0xf2/+0xf4`, then scans the nine carried item slots at `+0xde` (`@222..@238`). For
every usable item (`0x3053`) it keeps the largest combat-point value within its kind:

| Kind (id range) | Selector write | GUILD field | Worn slot |
|---|---|---|---|
| shield (1–20) | `+0xf2 = item_id` | `@242` | `@82` |
| armor (21–40) | `+0xf4 = points` | `@244` | `@84` |
| weapon (41–100) | `+0xf0 = points` | `@240` | `@86` |

It does **not** write `@28` (damage A); that writer is still **[?]**.

`0x3263` (combat prep) [C] — OV2 `0x327c`–`0x3302`, run for each party slot:
```
@246 = @244                          # armor effective
@248 = @28 + @240                    # weapon damage effective
@250 = @30                           # weapon hit effective = attack skill
def  = (DEX@4*20 - @244*50 + (PARRY@32*10)/4) / 10
if def < 0: def = 0
def += @242 / 3                      # shield
def = min(def, 255)
@12  = def                           # stored in the record
if status@24 != DEAD: status = OK    # sleep left from an earlier fight ends
```
Callers: encounter start `0x3386` (only when `[0x64e8] == 3`), wilderness/dungeon fight starts
`0x606a`/`0x60b3`/`0x6179`, and the night encounter `0x7183`.

**Night encounter (OV2 `0x70ed`) [C/~]:** `rand < 35` → encounter. A random party slot is on
watch; if it is alive and `rand < listen@40` it hears the monsters (message, no ambush). Else
prep runs, **every OK member is set to SLEEP**, and the watch member alone is set back to OK.
This matches the manual ("whoever is on watch will hopefully hear the monsters… otherwise party
members will wake up in the middle of combat"). Re-traced s36 (`overworld-movement.md` §Night encounter: monster
`r + 12·T`, `r ∈ {0,1,4}`; DEAD watcher → no roll, all asleep). **In Core (s36):** `NightEncounter`, wired in
`GameSession` random encounters; the day branches of `0x70ed` are still not wired.

**Combat buffs (OV2 `0x0470..0x05ce`) [C]:** QUICKNESS `@250 = @30·(100+pct)/100` (cap 255);
STRENGTH `@248 = @28 + @240 + 2/4/7/11`; PROTECTION `@246 = @244 + 1/3/6/10`.

**HEALTH REPORT (OV2 `0x45a0`, loop at `0x45d8`) [C]:** format DS `0xbdb1` = `"%-10.9s%c HP%4d/%-4dA:%d MP%3d/%d"`:
name, status letter (DEAD 'D', OK ' ', asleep 'S'), HP `@22`/`@20`, A, MP `@18`/`@16` (s49 arg order from the pushes), i.e. `name HP cur/max A:n MP cur/max` with
`A = @250 / 75 + 1`: the maximum swing count the action chooser allows (§Action chooser, s20).
The manual doesn't describe this screen.

## Monster record & encounter build — OV2 `0x34aa` [C] (s22)

Source: the 80 × 0x39 table at DS:`0x1a6c` of the unpacked `PHANTASI.COM` (field table in
`data-tables.md` §Monsters; extracted by `extract_monsters.py`, equal to the DOSBox capture). The builder
copies one record per group into `0x7522 + g·0x39` (same layout), so combat reads, e.g., armor at
`+0x7536`, damage `+0x7538`, level `+0x753a`, CV `+0x7547`, defense `+0x754f`, attack power `+0x7559`.
The s3 offsets (armor `+0x14`, CV `+0x25`, defense `+0x2d`, AP `+0x37`) were right; the "disproved" verdict
compared them against a `monsters.json` parsed from EXEPACK-packed bytes.

Called from `0x3312` with the first monster id `[0x64e6]` (1-based, out of range → 0). Per group `g = 0..2`:
```
copy record m → group g;  [0x64e0+2g] := m
if m == 76 (BLACK KNIGHT) and [0x64e8] == 3 and [0x64ec] == 9:  size factor := 4      # 0x387e: in DNG9 (see note)
width_sum += sprite_width[m] + 3;  room := width_sum < 135                              # 0x399b
if room:
    n := clamp(rand0_99 · f / 102, 0, 5)       # f = +0x33; group has n + 1 monsters (max 6)  # 0x39b1
    per monster: HP := ((rand0_99 + 50) · hp_base + 50) / 100                               # 0x3a27
                 state := 3 − [0x64a0]  (H 0/1 → ready, H 2 → asleep; §Surprise)                        # 0x3a6d
                 gold_total += gold (+0x2f)
    L += level (+0x18);  reaction_sum += +0x29                                              # 0x3ac4
    m := (rand0_99 < 50) ? +0x28 : +0x27       # companion id for the next group            # 0x3adc
if room and g < 2 and rand0_99 < +0x2b: next group, else stop                               # 0x3b22
```
`L` and the totals are zeroed first (`0x3542`, `0x3340`). The `[0x7508]` writes at `0x486e`/`0x4890` belong
to a field spell's "[original text]" estimate and are overwritten here, so in combat
**`L = Σ group levels`** (minus 6 per cast of the spell at `0x834`). XP is earned per kill (`+0x1a` into
`[0x7512]` at `0x1656`); victory (`0x3fbe`) divides gold by `[0x6490]` and XP by `10·[0x6490]` before printing them — `[0x6490]` = number of
non-DEAD members (`0x43ae() + 1`, s27; summon `0x8e1` increments it), not the party-size word `[0x648e]`.
Quirk (moot): the `< 0` clamp at `0x39e1` tests group 0's count, not group g's; no record has `f ≥ 0x80`.

**Re-checked s25 (capstone):** a group that fails the width test is dropped entirely: no count or HP rolls, no
`L`/gold/companion, and the group count `[0x6492]` stays `g` (`0x3b53..0x3b67`). The width test is `sum ≥ 135` → no room.
Treasure ids are read for `g == 0` only, before the width test (`0x394c`). The next-group roll (`0x3b22`) is drawn
before the `g < 2` check, so the third group still uses up a roll. Companion byte order: `Companions[0]` = `+0x27`
(SKUM raw bytes `02 03` = `[2, 3]`). **All ids are 0-based inside the builder:** the only −1 is at init (`0x34c1`,
out of 0..0x4f → 0); the record (`0x1a6c + m·0x39`) and width table (`0x2c3e + 4m`) are indexed by `m`, and companions are
stored without adjustment; the BLACK KNIGHT compare is `m == 0x4c`. Init also sets party slot 6's status word to 1 (`0x352f`) `[~]`.
**Build state [C] (s33):** `3 − [0x64a0]`. With `H = 0` it is 3 (ready). With `H = 1` it is 2, which the build's own
compaction (`0x3378`, `[0x74ae] = 1`, states > 1 → 3) turns back into 3 before the first round, so the monster is ready.
With `H = 2` it is 1 = **asleep** (the 15 % wake check `0x1081`), which compaction keeps. The 2 that the death path
`0x163d` writes is the same "done this round" value. A dead monster is HP 0, and compaction drops it, so the two uses
do not collide. The write draws no RNG. Writers and effects of `H`: §Surprise.
**BLACK KNIGHT spot [C] (s33):** `[0x64e8] = 3` means "in a dungeon" (`0x1e35`). `[0x64ec]` is the dungeon number,
read from the overworld feature record at dungeon entry (`0x1de1`) and passed to the loader `0x1e47`
(`sprintf "DNG%-d"`). So the rule reads: **a BLACK KNIGHT group built inside DNG9 (Fortress of the Black Knights) uses
size factor 4 instead of the record's 3**, giving 1..4 knights (`rand0_99·4/102`) instead of 1..3. It applies to every
group of id 76 in the build (group 0 or a companion), not only at one square.
**XP accumulator `[0x7512]`:** zeroed at build (`0x3334`). It grows by `+0x1a` in the shared monster-damage routine
`0x1597` (callers `0x432`, `0xc19`, `0x14b5`, `0x14f1`), and also at **`0x8c1`**: the DISSOLVE / DISPELL UNDEAD dissolve path adds
the front group's XP × `[0x74be+2g]` (= living − 1), §Combat spell effects (s31). The per-round party options (`0x3b70`, §Party options) use it for the
monster morale check and THREATEN. Every exit (`0x3fbe..0x40e1`) divides it by `10·[0x6490]` (non-DEAD members) and adds share·n to the pool `[0x63f4]`, which is kept in tens and capped at `0xffff` (§Outputs, kill-XP payout [C] s41).
**Core (s25):** `EncounterBuilder.Build` implements the build above, with RNG draws in DOS order. `CombatEncounter.RecordKill`
adds XP on every kill through the damage routine; gold, `L`, treasure ids (kept, not dropped yet) and the reaction sum
(read by §Party options) are set at build. The BLACK KNIGHT spot is a flag the caller passes; since s33 `GameSession.BeginDungeonCombat` passes
`Dungeon.LevelId == 9`. The surprise hold `H = 2` builds every monster asleep (s33, §Surprise).

## To-hit [C]

### Spell / breath / `FUN_0000_1a7b` / `FUN_0000_1382` (CV formula)
```
chance = (attacker_CV - defender_CV) * 5 + 50     # 0x32
roll   = FUN_0000_655b()                            # 0..99
hit    if roll <  chance   # FUN_0000_1a7b
       if roll <= chance   # some spell paths
```
No clamp — can go &lt;0 / &gt;100 (always miss / always hit).

**Operands [C] (s33, raw bytes re-read with party base `0x5bb0`):**

| path | site | attacker | defender | compare |
|---|---|---|---|---|
| party casts CONFUSION / WEAKNESS / BINDING (`0x1382` modes 1–3, one roll per group) | `0x13ab..0x13e6` | caster `[c·0x11a + 0x5bc0]` = **`@16` MP max** | group CV `[g·0x39 + 0x7547]` = record `+0x25` | `rand ≤ chance` (`jge` hit) |
| monster party-wide special (`0x1a7b`, codes ≠ 2) | `0x1ae1..0x1b08` | group CV `+0x25` | member **`@16` MP max** (`0x5bc0`) | `rand < chance` (`jg` hit) |

`c` = `[bp−2]` of the round scheduler = the caster (the non-HEALING cast path never overwrites it; HEALING moves the
target into `[bp−2]` and keeps the caster in `[bp−4]`, `0x23f`/`0x253`). `0x5bc0` is `@16` MP max beyond doubt: the
wilderness inn rolls `rand ≤ 20·(@14 − @16)` then `@16++` and `@18 := @16` (`0x67a3..0x680a`, = town Inn), and the MAGIC
potion caps `@18` at `@16` (`0x4488..0x44ba`). The old "caster CV `@14`" was this same address read with base `0x5bb2`.
**No level term** in either roll. Other caster operands: CHARM / TELEPORTATION / FEAR / DISPELL UNDEAD use level `@88`
(§Combat spell effects); FIREFLASH damage scales with **INT `@2`** (`magic-spells.md` §Effect coefficients; target: §FIREFLASH target); MINDBLAST,
FLAMEBOLT, DISSOLVE and DOS SLEEP have no caster term.
**Core (s33):** `SpellCaster.CasterHitsGroup` reads `MagicMax` (was `MpCeiling`); `MonsterSpecials` already read `MagicMax`.
**Manual (PDF p.18, printed 14):** "Active magic's effectiveness depends upon the spellcaster's magic power and experience
level, as well as the strength of the target." DOS modes 1–3 use magic power (MP max) vs target CV but **no level** —
conflict D-028. **Decided (user, s34): manual. Remaster design, not DOS:**
`chance = 5·(caster @16 MP max − group CV) + 2·(caster level @88 − group level +0x18) + 50`, hit if `rand ≤ chance`;
equal levels = the DOS roll. Core: `SpellCaster.CasterHitsGroup` (`CasterLevelWeight = 2`), also used by remaster SLEEP.
The monster party-wide special roll (`0x1a7b`) stays DOS.
**`0x1382` group loop and wiped groups [C] (s33):** it loops `di < [0x6492]` (built group count, wiped groups
included; the non-empty count `[0x6494]` is not used) over the rank-order table `0x74f4`. Modes 1–3 draw the resist
roll (`0x13e0`) before any living test, so **a wiped group still uses up one roll**. On a hit its record is scaled,
which nobody sees. `0x1382` prints no per-group message. Core (s33) rolls for every built rank, wiped ones included.

### Party → monster melee (main swing loop) [C]
```
chance = weapon_hit_5caa / swings - monster[+0x754f]
hit    if FUN_0000_655b() < chance
```
More swings ⇒ lower per-swing chance (SLASH “harder each”).

### Monster → party melee (main swing loop) [C]
```
metric = (monster[+0x7559] / swings - party_def_5bbc) * 10
hit    if FUN_0000_655b() * 6 < metric
# equiv: roll < metric/6  ≈  (atk/swings - def) * 5/3
```

## Damage [C]

Applicator: `FUN_0000_124a(target, damage, flag)` — damage floored to **≥1**;
on kill → `FUN_0000_12cf`. Party→monster HP apply: `FUN_0000_1597`.

### Monster → party
```
roll   = FUN_0000_655b()                 # 0..99
coef   = isqrt(roll)                      # dfa9 / FUN_0000_3675 → 0..9
damage = (coef * monster[+0x7538]) / 15 - party.armor_eff_5ca6

# PARRY (action == 3) and damage > 0:
#   clear parry (store 4), then
damage = damage - party.shield_soak_5ca2

FUN_0000_124a(party_index, damage, 0)
```

### Party → monster
```
roll    = FUN_0000_655b()
coef    = isqrt(roll)                     # 0..9
product = (int32) weapon_dmg_5ca8 * coef
product = product - (int32) monster[+0x7536] * 10
product = product + 35                   # +0x23
product = product - (action_code * 100) / 67   # f8d1; action from 0x74fa
damage  = product / 10                   # f8d1

FUN_0000_1597(rank_index, slot, damage)
```

Action penalty `(action*100)/67` is why THRUST (~11) hits harder than ATTACK
(~21) and SLASH (~31+) softer per swing — matches manual ±1..2 ballpark. [C]

### Death — `FUN_0000_12cf` [C]
Only caller: the HP-damage routine `0x12b3` (via `FUN_0000_124a`). Record base `0x5bb0`.
```
status @24 (0x5bc8) := 0            # DEAD
HP     @22 (0x5bc6) := 0
[0x6490] -= 1                       # living-member count
loss = (CON > 0 ? 1 : 0) + (CON > 1 && @10 < 11 ? 1 : 0)   # CON @6 (0x5bb6), seed @10 (0x5bba, 8..13)
CON -= loss                         # no floor below 0
action[0x74fa + 2·idx] := 4
print "%s DIES"
```
Deterministic: seeds 8–10 lose 2, 11–13 lose 1. Combat prep writes its defense rating to `0x5bbc` = `@12`, so `@10`
is intact here. A CON-0 corpse can't be resurrected. **Remaster follows this rule** (`DIFFERENCES.md` D-009, user s14;
Core `CombatResolver.DeathConLoss`, s30 — every party-damage path goes through `ApplyPartyDamage`, as DOS goes through `0x124a`).
Other status-zero writes audited (s31) `[C]`: OV1 `0x9df` is the party compaction `0x974` clearing a vacated slot
(`@92 := 0`, status := 0) and OV1 `0x2475` is the Guild remove-member path clearing the slot (`@218`, `@92`, status) — neither
is a death and neither touches CON. OV2 `0x667b` is an `add @6, di` case of a stat-add switch (a stat change, not a death).
The only OV2 status := 0 is `0x12de`. **Non-combat deaths do lower CON**: dungeon event damage `0x2642`, traps `0x314f`
("IN THE TRAP:") and swimming `0x68ff` ("SWIMMING:") all call `0x124a`, so a death there runs `0x12cf` too. Core trap
damage (`DungeonTraps.Spring`, the DOS rule since s39) and swimming (`Swimming`, s39) go through `ApplyPartyDamage`. Aging death (OV1, town entry)
does not go through `0x12cf` — no CON loss there `[~]` (OV1 aging writer not re-checked).

### Spell AoE sample (`FUN_0000_1a7b` param paths) [C]
```
scaled = (FUN_0000_655b() * param) / 100   # via f8d1
# param_2 == 2 (armor-resisted):
damage = scaled / 10 - armor_eff_5ca6
# param_2 == 1:
damage = scaled / 10                       # no armor
```

## Turn order — round scheduler OV2 `0x00e4` [C] (s21)
One melee round = **three passes** `p = 1, 3, 5` (`[bp-0x18]` starts at 1, `+= 2` at `0x110e`, loop while `p ≤ 5`).
Each pass runs the whole **party block** (party order), then the whole **monster block** (rank order, slot order).
Nobody acts twice: after acting, the action is copied to `0x5b96[i]` (the "repeat" value) and `0x74fa[i] := 4`.
No DEX or QUICKNESS term — initiative is level for the party and encounter level for the monsters.

```
for p in (1, 3, 5):
  for each party slot i:                                   # 0x140
    DEAD:  skip
    SLEEP: if rand0_99 <= 16 and HP >= 1: status := OK, "%s AWAKENS"   # 0x15d; uses up the pass
    OK:    if action[i] <= 4: skip                         # PARRY (3) / no action (4) never roll → PARRY stays armed
           if rand0_99 < 3·level@88 + 5·p²: act            # 0x1b9; p=5 → always
           (spell actions > 0x5a branch after this roll, so casters are gated the same way)
  for each rank position g, each living slot k:            # 0xc9d
    state 3 (ready) and not monsters-surprised [0x64a0]==0:
       if 7·rand0_99 <= 50·p² + 30·L: act                  # 0xcf1; L = [0x7508]; p=5 → always
    state 1 (asleep): if rand0_99 < 15: state := 2, "%s AWAKENS"   # 0x1081
```
Party threshold: pass 1 = `3·lvl + 5`, pass 3 = `3·lvl + 45`, pass 5 = always.
Monster threshold (as % of the 0..99 roll): pass 1 ≈ `(50 + 30·L)/7`, pass 3 ≈ `(450 + 30·L)/7`, pass 5 always.

**Monster turn (`0xd2b`)** — state := 2 when it acts (so one action per round):
```
if rand0_99 + 1 > m[+0x1e]·(2g + 2) / (ranks + 2):  melee       # 0xd30; g = rank position 0..2
else: special = m[+0x20 + rand0_99·3/100]  → 0x16a9           # §Monster specials; result > 0x21 falls back to melee
melee: if g == 2: nothing (third rank never melees; state stays 3 → re-rolls next pass)   # 0xd68
       swings = max(1, m[+0x37] / 82)                         # attack power / 0x52
       per swing: t = ((rand0_99/10 + 10·k) · partySize / n) / 10      # 0xdd4; k = slot, n = monsters in group
                  while t >= partySize or status[t] == DEAD: t = (t + 1) mod partySize
                  → to-hit / damage below (SLEEPING members can be hit)
```
So the k-th of n monsters aims at roughly party slot `k·partySize/n` (+0..0.9 slot of jitter) — attacks spread
across the party, they are not a uniform random pick.

**Inputs:** `L = [0x7508]` "[original text]" (`0x4897`). Writers: encounter build `0x3542` (:= 0) then
`0x3ac4` adds `m[+0x18]` once per **group**; `0x486e`/`0x4890` (MONSTER EVAL., s41 [C]: wilderness `2·([0x649c]+1)`
capped 12, dungeon `2·levelbuf[0x4e7] + 2`; `magic-spells.md` §Wilderness/dungeon cast dispatch); `0x834` a spell lowers it by 6. Fields `+0x18`/`+0x1e`/`+0x20`:
§Monster record (s22); the special effects: §Monster specials below (s26). `ranks` = `[0x6492]`, the group count set at build.

## Monster specials — OV2 `0x1004` → `0x16a9` [C] (s26)
Taken when the gate passes (`rand0_99 + 1 ≤ gate·(2g + 2)/(ranks + 2)`, integer division). Unlike melee, the third rank
**may** use a special. RNG order: gate roll, then `id = m[+0x20 + rand0_99·3/100]`, state := 2 (done), then the effect.
`0x16a9(&id, group, slot)` prints `"%s BREATHES"` for ids 36/37 and `"%s CASTS A SPELL"` for every other id, then:

| id | effect | values |
|---|---|---|
| 1–4 | one target: `living[rand0_99·livingCount/100]` (non-DEAD members in party order, `0x43ae` list `[0x64a2]`); `0x124a(t, d, 0)` — no resist roll, no armor, floor 1 | `d = rand0_99·k/100`, k = 12 / 35 / 70 / 120 |
| 5–8 | the casting **group's** attack power `+0x37 := 30·n² + 35`, n = id − 5 | 35 / 65 / 155 / 305 |
| 9–12 | group damage `+0x16 := 15·n²/10 + 8`, n = id − 8 | 9 / 14 / 21 / 32 |
| 13–16 | group armor `+0x14 := 4·(id − 12) + 4` | 8 / 12 / 16 / 20 |
| 17–20 | "[original text]": `0x1a7b(10 − 2n, 0x39)`, n = id − 16 | power 8 / 6 / 4 / 2 |
| 21–24 | "[original text]": `0x1a7b(10 − 2n, 0x3a)`, n = id − 20 | power 8 / 6 / 4 / 2 |
| 25–28 | `0x1a7b(100n² + 60, 1)`, n = id − 25 — resisted, no armor | power 60 / 160 / 460 / 960 |
| 29–32 | `0x1a7b(80n² + 40, 2)`, n = id − 29 — not resisted, armor | power 40 / 120 / 360 / 760 |
| 33 | `0x1a7b(0, 0x12)` — SLEEP | — |
| 34 | id := 100 → melee (below) with **A STUNNING BLOW** rider | — |
| 35 | id := 101 → melee with **[original text]** rider | — |
| 36 / 37 | breath: `0x1a7b(200 / 360, 2)`, **then a normal melee** (id is left as is, > `0x21`) | — |

The group writes (5–16) **set** the field (not add), for every monster of the group (shared group record), so a group whose
base value is higher gets weaker. `e6b3` = COM `0x3a8f` (unpacked image) = `x²`. Ids 1–33 end the turn; ids > `0x21` (34–37) fall
into the melee code at `0xd68`: dragons breathe *and* melee. A third-rank 34–37 user prints its message (and breathes) and then
does nothing more (its state is already 2).

**Party-wide `0x1a7b(power, code, group)`** — for every party slot `i` in order (DEAD included):
```
scaled = rand0_99 · power / 100                       # always drawn
code 2:  0x124a(i, scaled/10 − armor_eff @246, 0)     # no resist roll; 0x124a skips DEAD, floors 1
else:    resist: affected if (group CV +0x25 − @16 MP max)·5 + 50 > rand0_99     # second roll
         code 1:    0x124a(i, scaled/10, 0)
         code 0x12: if status ≠ DEAD: status := SLEEP; "%s IS SLEPT" (printed even for DEAD); action[i] := 4
         else:      field(i, code) := (field · scaled + 5) / 10      # 0x1d5a / 0x1d71 via field map 0x1ba5
```
Field map `0x1ba5` for these ids: `0x37 < id < 0x3b` → `0x5c36 + 2·id`, so `0x38` = `@246` armor eff, **`0x39` = `@248` weapon
damage eff**, `0x3a` = `@250` weapon hit eff. The resist operand is `@16` (MP max, `0x5bc0`) — that is what the code reads
(**[C] s33**: `0x5bc0` is MP max, see §To-hit operands; Core `MonsterSpecials` reads `MagicMax`).

**Field-id map `0x1ba5(slot, id)` → record word — full table [C] (s33, re-read from the raw bytes; disk offset = immediate −
`0x5bb0`).** Returns `slot·0x11a + K`; read by `0x1d5a`, written by `0x1d71`. Callers: `0x1a7b` (specials 17–24, ids
`0x39`/`0x3a`) and the dungeon script ops SET-MIN / TEST (`0x288e`, `0x28a2`, `0x28cb`; `endgame.md` §2). No OV1/COM copy.

| id (dec) | test in code | K | record word |
|---|---|---|---|
| 1–13 | `id < 0x0e` | `0x5bae + 2·id` | `@(2·id − 2)`: 1 STR `@0`, 2 INT `@2`, 3 DEX `@4`, 4 CON `@6`, 5 CHA `@8`, 6 LUCK `@10`, 7 defense `@12`, 8 MP ceiling `@14`, 9 MP max `@16`, 10 MP cur `@18`, 11 HP max `@20`, 12 HP cur `@22`, 13 status `@24` |
| 0 (and negatives) | same branch | — | `@−2` = previous record / outside — invalid |
| 18, 19 | `0x11 < id < 0x14` | `0x5ba4 + 2·id` | 18 → `@24` status (alias of 13) `[~]`, 19 → `@26` (unknown word) |
| 20, 22 … 38 (even) | `0x13 < id < 0x28` | `0x5bce + (id − 20)` | skills `@30..@48`: 20 attack, 22 parry, 24 find item, 26 spot trap, 28 disarm, 30 listen, 32 `@42`, 34 pick lock, 36 `@46`, 38 swim |
| 21, 23 … 39 (odd) | same | `0x5be2 + (id − 21)` | skill bonuses `@50..@68` (id 21 + 2k → bonus k) |
| 42 | `== 0x2a` | `0x5bf6` | `@70` score |
| 43–47 | `0x2a < id < 0x30` | `0x5bf8 + 2·(id − 43)` | runes `@72..@80` (AIR, EARTH, FIRE, WATER, GOD) |
| 48–50 | `0x2f < id < 0x33` | `0x5ba2 + 2·id` | `@82` shield, `@84` armor, `@86` weapon (worn) |
| 55 | `== 0x37` (checked last) | `0x5c0a` | `@90` — magical-pool product [C] (s39; `save-file-format.md`) |
| 56–58 | `0x37 < id < 0x3b` | `0x5c36 + 2·id` | `@246` armor eff, `@248` weapon dmg eff, `@250` weapon hit eff |
| 60–120 | `0x3b < id < 0x79` | `0x5c10 + 2·(id − 61)` | `@94..@214` (60 → `@94` cast-since-rest, 61 → `@96` …; spell-known words etc., per-id meaning `[?]`) |
| 152–160 | `0x97 < id < 0xa1` | `0x5c8e + 2·(id − 152)` | carried items `@222..@238` |
| 161 | `== 0xa1` (checked last) | `0x5cb0` | `@256` displayed race |
| 162–164 | `0xa1 < id < 0xa5` | `0x5b5c + 2·id` | `@240` weapon pts, `@242` shield, `@244` armor pts |
| 14–17, 40, 41, 51–54, 59, 121–151, ≥ 165 | none | — | **falls through**: returns the id itself as a near pointer (DOS bug if ever used) — invalid `[?]` |

The ids are word indices with a 1-based start (id n ↔ `@2n − 2` in the first block). All values that earlier notes took
from this function with base `0x5bb2` were 2 too low; every entry above is computed from base `0x5bb0` and agrees with
the s31 endgame ids (42, 43–47, 55, 152–160, 161) and with the fixture-checked combat fields (56–58, 162–164).

**Melee riders** (applied after each hit's `0x124a` and the living recount; skipped if the party is wiped):
- 101: `@18 MP cur := (6·@18 + 5)/10`, "[original text]".
- 100: `status := SLEEP`, `action := 4` (and repeat `0x5b96 := 4`), "A STUNNING BLOW" — written **even if the hit killed the
  target** (DOS bug: the corpse becomes SLEEP at HP 0, counts as alive in `0x43ae`, never wakes since waking needs HP ≥ 1).

**Remaster deviations (user, s26):** D-015 — a stunning blow only stuns a member who survived the hit. D-016 — 17–20 scale armor
eff `@246` (field `0x38`) to match the message, not weapon damage `@248`. The manual describes no monster abilities (no conflict
with the values above).
Both DOS behaviours stay reachable as rules options (Original (DOS) preset): `p1.stunning-blow` = `dos`
(`MonsterSpecials.ApplyRider(stunTheDead)`) and `p1.armor-reduced` = `weapon` (`PartyWide` scales `WeaponDamageEff`).

**Monster state** `[0x64b0 + 16·group + 2·slot]`: 1 asleep, 2 done this round, 3 ready. `0x33c9` compacts dead
monsters (states move with them); with `[0x74ae]=1` (round start, per-round menu `0x3b70`) it also sets every state > 1 to 3.

**Re-rank at round end (`0x19d7`, called `0x112b`) [C]:** the three groups are re-sorted by living count, largest
first; ties go to the **higher group id** (group = encounter-build order, not current rank). Empty groups go last.
The counts are current: every monster death (`0x1694`, after "%s DIES" and the XP add) re-runs the compaction
`0x33c9`, so a group wiped this round drops to the back at round end. The same compaction shifts slot indices, so in
the monster block `k`/`n` count the monsters still alive. Encounter build sets the identity order (`0x34f0`).
Matches the manual's sample screen (RANK 1: 5, RANK 2: 2, RANK 3: 1). Party actions keep their rank *position*.

**Surprise flags:** traced in full s33, see §Surprise below.

The manual says nothing about initiative, re-ranking or rank-3 melee (no conflict).

## Surprise — `[0x649e]` / `[0x64a0]` [C] (s33)

Two DS globals, referenced only in OV2. OV1 and the unpacked COM have no reference: a linear sweep plus a raw
LE-word search turned up 17 + 24 hits, and all of them are listed below. Names used here:
**`F` = `[0x649e]`** (first-round flag) and **`H` = `[0x64a0]`** (monster hold, 0/1/2).

### Readers [C]
```
build end 0x33ab (state 11, after the build compaction 0x3378):
    if H != 0 and F != 0: draw monsters (0x7044); return 4      # PARTY OPTIONS (morale check, menu, orders)
    else:                 return 5                              # straight into round 1: no morale check, no menu,
                                                                # no orders (0x3389 set every 0x74fa[i] := 4, slot 6 too)
builder 0x3a6d:  monster state := 3 − H      # H 0 → 3 ready; H 1 → 2, reset to 3 by the build compaction 0x3378
                                             # ([0x74ae] = 1 at 0x3352, states > 1 → 3); H 2 → 1 = ASLEEP
round top 0xee:  saved := F
                 if F != 0 and H == 0: every party action 0x74fa[i] := 4     # party loses the round
                 F := 0                                                      # 0x11f, every round
monster block 0xce7: a state-3 monster rolls readiness only if H == 0; otherwise → 0x1081, the asleep check
                 (state 1: rand < 15 → state 2 "%s AWAKENS"), which runs whatever H is
after each pass 0x10f3: if saved == 1 and H != 2: H := 0; saved := 0        # i.e. after pass 1 only
round end 0x1115: H := (H == 2) ? 1 : 0                                      # then re-rank 0x19d7
```
Early exits skip the round-end update. These are a party-block kill of the last monster (`0xc26`, `0xc92` → 12) and a
party wipe (`0xd9c` …, → 6). The flags then stay as they are for the next encounter (see stale flags below).
`0x163d` (monster death) also writes **2** into the state slot. That is "done this round", not a death marker: death
is HP 0 (`0x1637`), and compaction `0x33c9` drops HP ≤ 0 slots. So build state 2 (`H = 1`) is just a ready monster.

### Writers [C]
| Site | Situation | `F` | `H` |
|---|---|---|---|
| `0x227e` | dungeon random encounter (feature `8..0x11`, `rand0_99 ≤ [0x1a5a]`) | 1 | 1 |
| `0x2396` | dungeon forced fight, feature `0x80..0x89` | 1 | 1 |
| `0x2708` / `0x2710` | DNG script op 5, fight monster `a` | 1 | `rand0_99 > 90` → **0**, else 1 |
| `0x2847` | DNG script op 17, "MONSTERS APPEAR" | 1 | **0** |
| `0x2f30..0x2f56` | dungeon lock ambush (`0x2e03`, door kinds 2..13; see below) | `rand0_99 > 50` and someone heard → **0**, else 1 | `rand0_99 ≤ 50` → 1, else **0** (drawn first) |
| `0x7143` | overworld `0x70ed`, "[original text]" (`rand0_99 < 35`) | 1 | 1 |
| `0x7284` | Black Knights menu, **any answer** (before the switch; s39). RUN that gets away and SURRENDER end here: no fight, `F` = 0 left for the next fixed encounter | **0** | — |
| `0x7284` → `0x729b` | Black Knights menu: RUN fails (`rand0_99 ≥ 44`) | 1 | 1 |
| `0x7284` → `0x72a1` | Black Knights menu: FIGHT | **0** | 1 |
| `0x7376` | "[original text]" → ATTACK | **0** | **2** |
| `0x7473` | "%s HEARS MONSTERS": `rand < 6·[0x649c] + 20` (fight, no menu); FIGHT and `rand0_99 < 30`; RUN fails (`rand0_99 ≥ 80`); nobody heard and `rand ≥ 6·[0x649c] + 20` | 1 | 1 |
| `0x748e` | "%s HEARS MONSTERS" → FIGHT, `rand0_99 ≥ 30`: "[original text]" | **0** | **2** |
| `0x74d9` | nobody heard, `rand < 6·[0x649c] + 20`, then `rand0_99 ≥ 50`: "[original text]" | 1 | **0** |
| `0x993` | DISPELL UNDEAD run, mid-round (§Combat spell effects) | — | 1 |
| `0x3d9f` | PARTY OPTIONS Flee fails (§Party options) | — | 0 |
| `0x11f`, `0x1103`, `0x111d`, `0x1125` | scheduler (above) | 0 | 0 / 1 |

Menus return a 0-based choice (COM menu table: the entry for id `n` is at DS `0x20 + 16·n` — s36 re-check, the earlier "`0x10 + 16·id`" was one entry off; labels DS `0x64c`). Menu `0xf` = [original text] /
RUN / FIGHT / SURRENDER (the code adds 1). `0x10` = [original text] / ATTACK / LEAVE. `0x11` = RUN / FIGHT,
titled "%s HEARS MONSTERS". `0x1f` = YES / NO under the lock's "%s [original text]" (0 = YES = go back). Listening rolls: overworld
`rand0_99·222/100 ≤ @40` (the last member who succeeds is the listener), lock `rand0_99 < @40`, both for non-DEAD members.
`[0x649c]` = overworld tile `% 10` (`0x5f84`); it is also the 12-monster block in the id formulas (`endgame.md` §1).

**Overworld `0x70ed` (called from the wilderness step, `rand0_99 < 6`, `0x615c`) [C]:**
```
if rand < 35:   "[original text]", F,H := 1,1 → night watch (above §Night encounter: sleeping party, watch slot)
elif rand > 91: if max score [0x63ea] > 83: "[original text]" (no fight)
                else menu 0xf, F := 0: RUN → rand < 44 no fight, else F,H := 1,1;  FIGHT → H := 1 (0,1);  SURRENDER → found gold := 0
                monster 77 (BLACK KNIGHT)
else:           pick a monster; if rand < 31: menu 0x10 (ATTACK → F,H := 0,2; LEAVE → no fight)
                (monster pick and full pseudocode: overworld-movement.md §Day encounter, s39)
                else listener roll:
                  someone heard: rand < 6·T + 20 → message, F,H := 1,1
                                 else menu 0x11: FIGHT → rand < 30 ? F,H := 1,1 : "[original text]", F,H := 0,2
                                                 RUN   → rand < 80 ? no fight : F,H := 1,1
                  nobody heard:  rand ≥ 6·T + 20 → F,H := 1,1
                                 else rand ≥ 50 → "[original text]", F,H := 1,0
                                      else (rand < 50) → falls off the end of the function   [C quirk, see below]
```
(`T = [0x649c]`.) The `[bp-6]` guard on the sleeping-monster menu is always 1.

**Lock ambush `0x2e03` [C]:** door kind `di = b − 0xb8` (`−15` more, `si = 15`, if `> 15`). Kinds `< 2` do nothing. 14 is
pick lock. `> 14` prints and pushes back. For kinds 2..13 the listener roll runs first. If someone hears, menu `0x1f` asks
"GO BACK ?", and going back pushes the party out (`0x29a9`) with no fight. Otherwise monster `12·[0x64f0] + di − 1`, then
the two rolls in the table (`H` first, `F` second; both are always drawn).
**[D✓] s44 (re-check of the s43 DOSBox frames, `capture_s43_2026-10-16.md` §2c):** 24 lock-ambush fights, all nobody-heard
(the driver answered YES to "GO BACK ?", so every heard roll was a push-back and re-roll): **15 opened with PARTY OPTIONS**, 5
with a monster message, 4 blank (between messages). The decode predicts F = 1 and options when H = 1, ≈ 51 % → ≈ 12.2 of 24;
15 is ≈ 1 σ. The s43 "24/24 monster-first" was a detector bug: the `top.sh` OCR returns empty on the yellow PARTY OPTIONS bar,
so every fight was logged MONSTERS_FIRST. Frames: `screens-s43/ambush-open/`, classifier `tools/scripts/s43_capture/openclass.py`.
Decode and Core unchanged.

### What the combinations do [C]
| `F`,`H` at build | Start | Round 1 | Later |
|---|---|---|---|
| 1,1 (**normal**: every random encounter) | PARTY OPTIONS | party acts from pass 1; **monsters sit out pass 1** and act from pass 3 | normal |
| 1,0 or 0,0 (**party surprised**) | no options, no orders | party does nothing; monsters act in all passes | round 2 normal (options) |
| 0,1 (**stalled**: Black Knights FIGHT; lock ambush when heard and `F` rolled 0) | no options, no orders | nobody acts (monsters held all round) | round 2 normal, monsters act from pass 1 |
| 0,2 (**monsters surprised**: sleeping monsters, YOU SURPRISE THEM) | no options, no orders; every monster **asleep** (state 1) | party does nothing; monsters only roll to wake (15 % per pass, woken = done) | round 2: options and orders, monsters held all round (`H` 2 → 1 → 0); round 3 on: woken monsters act; sleepers keep the 15 % roll (same asleep state as SLEEP #42, D-020) |

So the side that surprises never gets a free first round. In 0,2 the party's first real round is round 2, against held
and mostly sleeping monsters. In 0,1 the first round is empty, so Black Knights FIGHT is slightly worse than a failed RUN
(1,1: options and a free pass 1). These are code facts. They are odd, but nothing contradicts them.

**Quirks [C]:**
- **Stale flags.** The overworld fixed encounters (tile feature 7, `0x6045..0x6073`) and tile class 7 (`0x6094..0x60c2`)
  write neither flag. So does `0x70ed`'s fall-off path, which returns AX = that last `rand0_99` (0..49). Ghidra shows
  the same `return iVar1`. The caller stores it as `[0x64e6]`, so it fights a random monster id `rand − 1` (no fight on 0).
  These fights start with whatever the last fight left. After a fight that ran a full round both flags are 0 (party
  surprised). If the last monster died in a party block, `H` can still be 1 (round 1, pass 1 → 0,1, stalled). If a
  fight ended at PARTY OPTIONS before any round (BEG, GREET, THREATEN, a monster run, a flee), the flags are still 1,1.
  **[D✓] s45:** a day encounter on OUT1 tile 0 printed nothing and fought ZOMBIE (id 13, outside the day pick 0..11 and the
  night pick 0/1/4 for T = 0) monster-first, after a full-round fight — the fall-off path with stale 0,0 flags
  (`capture_s45_2026-10-16.md` §4).
- Both flags sit in the BSS the C startup zero-fills (unpacked COM `0x4f94..0x4fa6`, DS `0x3244..0xb8f0`), so they are 0 at program start [C] (s56). **No save file stores
  them [C] (s39):** the only DS writers of `0x649e`/`0x64a0` are the OV2 sites above (raw `9e 64` / `a0 64`: none in OV1
  or the unpacked COM). The town Save game writes GUILD / SACK (60 words from `0x63fa`, ending at `0x6472`) / TWNS, and
  the dungeon save `0x5929` writes the id, the map image and the level buffer. None of them covers `0x649e..0x64a1`. Stale
  flags therefore live only in RAM for the rest of the run.
- A DISPELL UNDEAD run (`H := 1`) cast during pass 1 of a 1,1 round is undone after pass 1. Flee failing in a 1,1
  round 1 sets `H := 0`, so the monsters act in pass 1 too.

**Manual:** it never mentions surprise or ambush, so there is no conflict. p.9: "LISTEN skills are automatically
employed by party members when they approach a door. If they hear monsters, a message is shown, and you have the
option of turning back." That matches the lock's "GO BACK ?". p.10 night encounters ("whoever is on watch will
hopefully hear the monsters…") matches `0x70ed`. p.9's "AN ENCOUNTER. Next, you will see this display: … PARTY
OPTIONS" describes the normal case (1,1). D-034: user (s34) confirmed — works as intended, DOS rule kept.
The flash itself is D-058 (user s46: manual): DOS prints "AN ENCOUNTER" nowhere (only "[original text]" before night
fights); Core notes `GameSession.EncounterBanner` = "AN ENCOUNTER" at the start of every fight, and the night banner in
its place for night fights (manual silent on night).

**Core (s33):** `SurpriseFlags` (`F`, `H`, presets) and `Surprise` (the `[C]` source rolls: script op 5, lock ambush,
the `0x70ed` sub-rolls). `CombatEncounter.Surprise` / `OpensWithoutOrders`, plus the builder's asleep state for `H = 2`
and the scheduler rules above. `MonstersFrozen` is now `H ≠ 0`. `GameSession.BeginCombat(…, surprise)` defaults to
1,1. Morale check and options are refused in a no-orders opening, `CombatRoundHasOptions` tells the Client, and the
snapshot keeps all three fields. **s39 (P0.3 + P0.4 merged):** overworld: the `0x70ed` day branches (`WildernessEncounter`)
and the stale globals (`GameSession.SurpriseGlobals`: set at every fight start, copied back at every fight end, used by
x7 / class-7 / fall-off fights, saved in the session) are in Core. Dungeon: wandering and forced fights open Normal (1,1)
with the cell's monster and no longer print "MONSTERS APPEAR"; the lock ambush uses `Surprise.LockAmbush` after the
listen roll / GO BACK prompt (`dungeon-representation.md` §Locks); script ops 5/17 pass their flags through
`DungeonScriptResult.Surprise` (op 17 → no PARTY OPTIONS, `DungeonCellsTests.ScriptOp17_OpensWithoutPartyOptions`).
F/H are never saved by DOS [C]. No DOSBox capture has checked this: the Normal pass-1 wait changes
every fight but is pinned only by the asm-derived unit tests, which makes it the first candidate for a per-round
fixture `[D+]`.

## Party options — OV2 `0x3b70` [C] (s27)

Combat state 4 (dispatch table OV2 `0xae`; build = 11, round = 5, end = 12, party wiped = 6) runs this at the **top of
every round**, not only before the first one. It sets `[0x74ae] = 1` and compacts (`0x33c9`, which returns the living
monster count and resets every monster state > 1 to 3). If a spell has set `[0x74b0]` (TELEPORTATION `0x7da`, dissolve `0x869`, DISPELL run `0x999` — §Combat spell effects)
or nothing is alive, it returns 12 (end).

**Morale check (`0x3b98`)**, before the menu. `XP` = kill XP so far `[0x7512]` (32-bit), `XP₀` = group 0's per-kill XP
(record copy `0x753c`, build order; re-ranking `0x19d7` only moves the order table `0x74f4`), `n` = living monsters:
```
if XP·(rand + 100) / XP₀ ≥ (rand + 200)·n:          # roughly "twice as many killed as still standing"
    print "MONSTERS RUN"
    if rand < 80 − |10·(1 − T)|:  end (12)           # they get away: full gold + kill XP, normal treasure roll
    else: [0xc432] := 45; every living monster's state := 2 (done this round)   # 0x3e13
```
`T = [0x649a]` = last overworld tile / 10 (written only by the overworld step, OV2 `0x5f7a`), so in a dungeon it is the
stale value from the entrance tile. **A failed run wakes sleepers [C] (s33):** the loop at `0x3e19..0x3e5f` writes 2 over
every living slot `0..[0x74be+2g]` of every group, whatever the old state (state 1 included); the next compaction
(`0x33c9` with `[0x74ae] = 1`, `0x3433..0x345f`) turns every state > 1 into 3 and copies states ≤ 1 unchanged. So
asleep (1) → 2 → 3 = awake. In DOS only a surprise build makes state 1, so this only ever hit surprise sleepers.
**Conflict with D-020 (reported s33, not coded):** since s32 the remaster's SLEEP spell makes a lasting asleep state that
"wakes only on the 15 % roll" (D-020, manual p.15/p.14). DOS logic applied to it would wake every slept monster on a failed
monster run; the manual says nothing about it. Core keeps D-020 sleepers asleep through a failed run (`MonstersLoseRound`
only) until the user decides.

**Menu** `0x6978` (menu `0x18`, labels at COM DS `0xd4ff`): **Beg mercy / Threaten / Greetings / FIGHT / Flee**, then
Show names (`0x3e6f`) and Party health (`0x45a0`), which return to the menu. **[C] (s49 static, capstone on raw OV2):**
Show names opens a box and, for each rank `r < [0x6492]` in rank order (group `g = [0x74f4+2r]`, skipped unless
`[0x74be+2g] > −1`), prints DS `0xbccd` = `"RANK %1d : %1d  %s(S)"` (file `0x7990`, string delta `0x433d`) with `r+1`,
`[0x74be+2g]+1` (Client: living count — same after compaction `[~]`) and the group name (`0x7522 + 57g`), then waits for a key (`0xe3c4`). Party health is the HEALTH REPORT
below (title DS `0xbda2`, one line per slot `< [0x648e]`, status letter from DS `0xbd9f` = "D S"). Neither uses the round. The DOS order is the C64 supplement's; the
Apple rulebook lists 1 FIGHT / 2 ACCEPT SURRENDER / 3 GREETINGS / 4 BEG FOR MERCY / 5 FLEE and the supplement says they
"work the same way" (labels only, no conflict). Jump table `0x3dae` (CS delta `0x53dc`):

| Option | Roll (`rand` = 0..99) | Success | Fail |
|---|---|---|---|
| Beg (`0x3bea`) | `rand < Σreaction + 90` | "[original text]"; gold reward := −`[0x63f6]` (found gold → 0), found-item slots `0x63fa + 4k`, k = 0..24 (slots 0, 2, …, 48 of 60) cleared, no treasure, `[0x1a5a]++` (encounter chance, below), `[0x74ac] = 1` | "[original text]" → orders (`0x6a2a`) → round |
| Threaten (`0x3c5b`) | `rand ≤ [0xc432]·XP/XP₀ + 3·[0x63ec] − 3·L` | "[original text]"; full gold; `[0x74b2] = (dungeon)` → treasure handed over (`0x41c4`) | same |
| Greetings (`0x3ce3`) | only if `[0x74b6] = 0`, then `rand < Σreaction + 10` | "[original text]"; gold 0, no treasure | same |
| FIGHT (`0x3d1e`) | — | orders for each member, round (5); 6 if the party is wiped | — |
| Flee (`0x3d27`) | `rand < 80 − p`, wilderness `p = 10·(1 − T)` (no abs), dungeon `p = 5·[0x648e]` | gold 0, no treasure, `[0x74ac] = 1`, end | every order `[0x74fa+2i] := 4`, `[0x64a0] := 0`, compaction (undoes a failed monster run's state 2), round (5) without orders |

- Every exit goes through the normal end of combat (`0x3f29`), which never clears `[0x7512]`: **kill XP is always paid**,
  also when the monsters run. (The old "RUN grants no XP" note came from a walkthrough; the code pays XP and full gold.)
  "MONSTERS KILLED" is printed only when no monster is left.
- `Σreaction` = `[0x750c]`, the sum of `+0x29` once per group (`0x3acc`); CHARM's success path (`0x797`) adds 15 and clears `[0x74b6]` (§Combat spell effects).
- `[0xc432]` = THREATEN factor: 15 at build (`0x332e`), 45 after a failed monster run.
- `[0x74b6]` := 1 at the top of the shared monster-damage routine `0x1597`, so after the first hit GREETINGS can't work.
- `[0x63ec]` = party average level: Σ `@88` over all `[0x648e]` slots (DEAD too) / party size, truncated (OV1 `0x3269`,
  party set-up on leaving town) — written only there, so THREATEN reads a value that is stale for the whole trip (Core s41:
  `GameSession.TownExitAverageLevel`). The same loop writes `+0x1c` (`@28`, see PLAN P0.1) and `[0x63ea]` = max `@70`.
- `[0x64e8]`: 2 = wilderness, 3 = dungeon (set on dungeon entry OV2 `0x1e35`, on leaving town OV1 `0x73`).
- `[0x6490]` (end of combat divisor) = `0x43ae() + 1` = number of non-DEAD members (`0x43ae` lists slots with `@24 ≠ 0`
  into `0x64a2` and returns the last index) **[C]**. If 0 → state 6.
- End of combat (`0x3fbe`) prints `XP/(10n)·10` and `gold/n` per member, then adds `gold/n·n` to `[0x63f6]` and
  `XP/(10n)·n` to `[0x63f4]` (both capped at `0xffff`). After BEG the printed gold is negative.
- **Flags set at build** (`0x3312`): `[0x74ba]` (treasure allowed) = 1, `[0x74b4]` (clear the cell) = 1, `[0x74ac]` (step back) = 0,
  `[0x74b2]` (dungeon surrender) = 0, `[0x74b0]` (spell end) = 0. BEG, GREET and FLEE zero `[0x74ba]` and `[0x74b4]` together,
  and so does TELEPORTATION (`[0x74b0] = 1`) at the end of combat (`0x3f77..0x3f91`, which also zeroes the gold) **[C]** — so
  Core's single `TreasureForfeited` flag is enough. Dissolve / DISPELL run (`[0x74b0] = 2`) keep both.
- **`[0x74ac]` = 1 (BEG, FLEE) → step back [C] (s33).** The post-combat hook `0x6255` (state 12 after `0x3f29`) calls `0x29a9`
  when `[0x64e8] = 3` (dungeon) and `[0x74ac] ≠ 0`, then clears it. `0x29a9` redraws the current cell (`0x21df`), copies the
  previous cell `[0x751e]/[0x7520]` (saved by every step, `0x1f58`) back to the position `[0x751a]/[0x751c]` and `[0x63e4]/[0x63e6]`,
  and redraws it (`0x20e5(cell, x, y, 0)`) **without** running the cell's event (`0x2249` is not called). So after begging or
  fleeing in a dungeon the party stands on the cell it came from and must walk into the fight cell again. Nothing happens in
  the wilderness. (`0x29a9` is also the dungeon script TEST op's push-back, `endgame.md`.)
- **`[0x74b4]` ≠ 0 → clear the fight cell [C] (s33).** End of combat `0x3f97..0x3fa6`, dungeon only, before the party-wipe
  check: `0x2a26` reads the cell under the party (`0x20c3`) and rewrites it with `0x2a02` (`buf[y·33 + x] := v`):
  forced fight `0x80..0x89` → `0x8a` (plain, "falls through"); `0xb9..0xc7` → `0xb9`; `0xc8..0xd6` → `0xc8` (the lock/ambush
  cells keep only their base value, which is door kind 1 = an open door, nothing happens — **[C] s39**, `0x2e03`).
  Wandering-encounter cells `0x08..0x11` are not touched. It runs on every exit except BEG / GREET / FLEE / TELEPORTATION:
  kills, monster run, surrender, dissolve, DISPELL run (raw `b4 74` sweep s39: CHARM never clears it). Core s39:
  `DungeonEncounters.ClearFightCell` from the session's end of combat; BEG / FLEE step back.
- **`[0x1a5a]` = dungeon wandering-encounter chance [C] (s33).** Only reader: the cell handler `0x2249` for cells
  `0x08..0x11` (8..17): `if rand0_99 ≤ [0x1a5a]` (signed `jle`, so `[0x1a5a] + 1` %) → fight: surprise flags `[0x649e]` =
  `[0x64a0]` = 1 (`0x227e`, see the surprise item), first monster `[0x64e6] = rand0_99/33 + cell + [0x64f0] − 7`, and in
  `[0x64ec] = 6` a second roll `rand0_99 > 50` replaces it with `0x22`; state 11 (build). Else `[0x64f2] = cell + 3` and, if
  `[0x64fa]`, message `cell + 3` (`0x23c0`); state 3. Writers: dungeon entry (state 13, `0x82`) := **7**; every build
  (`0x3316`, all fights incl. wilderness, forced and scripted) `−1`, then `< 5` → **1**, so 7 → 6 → 5 → 1 → 1…; BEG `+1`
  (`0x3c0b`; the next build takes it back). No COM / OV1 reference. The unpacked image's initial value is **9** (DS `0x1a5a`
  = `09 00`) — only seen if a dungeon is reached without state 13 `[~]`. DNGX.SAV does not keep it **[C]** (`0x5929` writes
  only the level buffer, and entry resets it anyway). `[0x64f0]` = the level tier, buffer byte `0x4e7`; `[0x64fa]` = 1 on
  every step, so the no-fight message always shows (`dungeon-representation.md` §Fight cells, s39 [C]). Core s39:
  `DungeonState.WanderingChance`, `DungeonEncounters`.

**Remaster (D-017, user):** BEG always works (manual; no roll). **D-018:** the found-item list keeps the manual's 90
items; BEG drops list positions 0, 2, …, 48 of Core's compact list (DOS leaves holes in its 60 slots) `[~]`.
**D-019 (decided s32: DOS):** a monster run that gets away pays the gold of every monster, as DOS does (the manual
reads as gold "retrieved from the monsters"; the user kept DOS).
**Core (s27):** `PartyOptions.MoraleCheck` / `Choose`, `GameSession.StartCombatRound` / `ChoosePartyOption`,
`LootSack.ForfeitToBeggars`, `OverworldState.TerrainClass`. Treasure flags are recorded on the encounter only (Core drops no
treasure yet). Client (s36): `CombatPresenter` runs the morale check, then the PARTY OPTIONS menu (DOS order, arrows + Enter; s49: Show names / Party health offered, decoded above; the highlight is no longer reset each round — DOS menus
remember the last choice, s45), then orders.

## Combat spell effects — OV2 `0x767..0x9bf` [C] (s31)

The combat cast routine (inside the round scheduler `0x00e4`, after "%s CASTS A SPELL") handles cast_id 1–36 with
the tiered `if` chain (`magic-spells.md` §Effect coefficients). cast_id ≥ 37 goes through a jump table:
`0x9e7`: `cast_id − 0x25 < 11` → `jmp cs:[bx + 0x5dad]`, table at file **`0x9d1`** (CS delta `0x53dc`). cast_id =
spell index − 3, so entry k is spell index 40 + k. Every handler then joins `0x9f7` (MP deduct, `@94 := 1`).

| cast_id | idx | spell (manual #) | handler | effect (DOS) |
|---|---|---|---|---|
| 37 | 40 | CHARM (#41) | `0x767` | `r₁ = rand`, `r₂ = rand`; if `r₁·lvl > r₂·L`: "[original text]", Σreaction `[0x750c]` += 15, `[0x74b6]` := 0 |
| 38 | 41 | SLEEP (#42) | `0x7a5` | `0x1382(caster, 6, 0)` — mode 6, below |
| 39 | 42 | TELEPORTATION (#43) | `0x7b8` | if `rand < 3·lvl + 50`: `[0x74b0]` := 1 (no message) |
| 40 | 43 | RESURRECTION (#44) | `0x7e3` | `0x113b(caster)` (`magic-spells.md` §OV2 RESURRECTION) |
| 41 | 44 | NINJA II (#45) | `0x7ed` | caster only: `@250` weapon hit eff := 255, `@248` weapon dmg eff := 25, `@246` armor eff := 10 |
| 42 | 45 | FEAR (#46) | `0x80c` | if `rand < 5·(lvl − L) + 50`: `L` `[0x7508]` −= 6 (no message) |
| 43 | 46 | DISSOLVE (#47) | `0x83c` | if `rand ≤ 110 − 10·L`: **dissolve** (below) |
| 44 | 47 | [original text] (#48) | `0x8cc` | if `[0x6490] == 5` and `[0x62a8] ≠ 0`: party size `[0x648e]` := 7, `[0x6490]++`, living list `[0x64a2 + 2·[0x6490]]` := 6, redraw `0xdbd3(7)`. No roll, no message (§[original text] below, s33) |
| 45 | 48 | DISPELL UNDEAD (#49) | `0x8fc` | below |
| 46 | 49 | NINJA I (#50) | `0x9a1` | caster only: `@250` := 200, `@248` := 20, `@246` := 8 |
| 47 | 50 | AWAKEN (#51) | `0x9bf` | `0x3eff`: every non-DEAD member's status := OK; "PARTY AWAKENS" |

`lvl` = caster level `@88` (`0x5c08`), `L` = encounter level `[0x7508]`, `rand` = `0x655b` (0..99), drawn in the order
written. All values are words; the products are 16-bit signed `imul` (no overflow at game ranges).

**Dissolve** (`0x85b`, shared by DISSOLVE and DISPELL UNDEAD):
```
print "[original text]"
[0x74b0] := 2                       # combat ends at the next round top
gold [0x750e] := 0                  # 32-bit
[0x74aa] := 1                       # end of combat prints "MONSTERS KILLED" (as if all died)
g := [0x74f4]                       # the group in the front rank (current order)
XP [0x7512] += XP(g) · [0x74be + 2g]   # 32-bit; XP(g) = group record +0x1a (0x753c + 0x39·g)
```
**`0x8c1` — the "second XP source" — is this line.** `[0x74be + 2g]` is the group's *last living index*
(`0x33c9` stores the compacted index, −1 when empty; the builder stores `n` for `n + 1` monsters), so the bonus is
**(living − 1) × XP per monster** of the front group only. One front monster pays 0; the other groups pay nothing.
Monsters already killed paid their XP through `0x1597` as usual. If the front group was wiped earlier in the same
round, the factor is −1 and XP **drops** by one monster's worth (no clamp; kill XP is never below 0 in practice).

**DISPELL UNDEAD** (`0x8fc`):
```
g := [0x74f4]                                     # front-rank group only
if [0x74b8] == 1:            nothing               # already worked on undead this fight
if XP(g) mod 7 != 0:         nothing               # not undead (COM 0x4cdb = 32-bit signed remainder); 0x74b8 stays 0
[0x74b8] := 1
v := 5·(lvl − level(g)) − rand                   # level(g) = record +0x18 (0x753a + 0x39·g)
if v > −5:     dissolve (above)
elif v > −45:  print "MONSTERS RUN"; [0x64a0] := 1; [0x74b0] := 2
else:          nothing
```
"Undead" = per-monster XP divisible by 7. In the DOS table that is exactly: SKELETON, BANTIR, ZOMBIE, GHOUL, LARGE
SKELETON, WIGHT, WRAITH, DEMON, VAMPIRE, GHOST, HIGH DEMON, SPIRIT. A cast at a non-undead front group can be retried; a
failed cast at an undead one cannot (`[0x74b8]` is set before the roll). `[0x64a0] := 1` stops every ready monster
from rolling readiness for the rest of the round (`0xce7`; sleepers still roll to wake).

**`[0x74b0]` — spell ends combat.** Written only here (1 = TELEPORTATION, 2 = dissolve / DISPELL run) and cleared at build
(`0x336a`). Read only at the round top `0x3b84` (before the morale check, so no morale roll) and at the end of combat
`0x3f77` (byte search of OV2 for `b0 74`: no other reader). So **the current round finishes**: members after the
caster still act, monsters still attack, the party can still be wiped (state 6 wins). If every monster dies in the same
round, combat ends at once (`0xc8b`) and the flag still applies at `0x3f77`. End of combat:
- `[0x74b0] == 1` (TELEPORTATION): gold := 0, `[0x74ba]` := 0 (no treasure roll), `[0x74b4]` := 0 (no dungeon
  post-combat `0x2a26`). **Kill XP is kept.** No "MONSTERS KILLED" unless nothing is alive.
- `[0x74b0] == 2`: nothing extra. After dissolve the gold is already 0 and "MONSTERS KILLED" prints (`[0x74aa]`); after
  DISPELL's run the full gold is paid (like a monster run, §Party options / D-019) and kill XP so far.
- `[0x74aa]` is also read at `0x420a`: monster id `[0x64e6] == 0x49` with `[0x74aa] ≠ 0` returns state 7 `[?]` (P0.8
  endgame); dissolve sets it, so DISSOLVE / DISPELL on that encounter counts as killing it.

**SLEEP — `0x1382` mode 6** (`0x1501`; no CV resist roll, unlike modes 1–3): for each group in rank order, for each slot:
`rand` (drawn and discarded, ×0 power), then `r = rand`; if `r ≥ L²` and the monster's HP ≥ 1: "%s IS SLEPT", state
`[0x64b0 + 16g + 2k]` := **2**. State 2 = "done this round": the next round-top compaction (`0x33c9` with `[0x74ae] = 1`)
sets every state > 1 to 3, so the spell only takes the monster out of the **rest of the current round**. `L ≥ 10` → never
works. Core: left to the parallel status-effect pass.

**Notes / quirks**
- CHARM, TELEPORTATION, FEAR, DISSOLVE print nothing when they fail; FEAR and TELEPORTATION print nothing on success either.
- CHARM does not stop the monsters: it adds 15 to the GREETINGS / BEG odds and re-allows GREETINGS after a monster
  was hit (`[0x74b6]`). Repeated CHARMs stack.
- FEAR stacks (−6 per success) and can push `L` below 0. `L` drives monster readiness (`50p² + 30·L`), THREATEN
  (`−3·L`), CHARM, DISSOLVE, the treasure roll (`0x40ef`/`0x41a3`) and SLEEP's `L²` (so a negative `L` makes SLEEP harder again).
  FEAR never makes monsters run: the morale check (§Party options) does not read `L`.
- NINJA **sets** the three fields (it can lower a better value). `@250 = 255` → SLASH `255/75 + 1` = 4 swings,
  `200` → 3 (§Action chooser). A later QUICKNESS/STRENGTH/PROTECTION recomputes all members from base and overwrites it.
- [original text]: traced end to end in s33, see §[original text] below.
- The old "DISPELL grants no XP" and "FEAR pays 0 XP/0 GP" notes have no source in the code or the manual (the manual
  only says XP is earned by killing monsters, PDF p.12). The dissolve path *adds* XP.

**Manual (PDF pp.18–20, printed 14–16) vs DOS** — **decided by the user s32** (D-020…D-024; D-019 = DOS). The DOS text
above stays the record of what the binary does; the remaster rules are in "Remaster (s32)" below.
- FEAR: "frightens the monsters into running away in terror"; "A second FEAR spell … can cause the victim to be even more
  afraid". DOS: `L −= 6`, the monsters never run because of it. **Conflict.**
- CHARM: "makes monsters so friendly, they forget they are in combat." DOS: the fight continues; only GREETINGS / BEG get
  easier. **Conflict (likely).**
- TELEPORTATION: "teleports the party safely away from the combat." DOS: can fail (`3·lvl + 50` %), and on success the
  rest of the round still plays out (monsters can still hit). **Possible conflict.**
- DISPELL UNDEAD: "affects skeletons, zombies, ghosts, and other creatures who have returned from the grave. They may flee
  or simply dissolve." DOS: matches flee/dissolve, but "undead" = XP mod 7, which includes DEMON, HIGH DEMON, BANTIR.
  **Possible conflict** (membership only).
- SLEEP: "they cannot attack, parry, or cast spells"; "a second SLEEP spell will not deepen a trance". DOS: only for the
  rest of the round. **Possible conflict** (duration not stated in the manual).
- DISSOLVE ("slowly dissolve away into nothing"), NINJA ("Ninja 1 costs more magic points, it is not as powerful as
  Ninja 2" — I: 2 MP 200/20/8, II: 1 MP 255/25/10), AWAKEN ("rouses all sleeping party members"): no conflict.

**Core (s31):** `CombatSpells` (Core) holds the rules; `SpellCaster.ApplySpecial` calls it. `CombatEncounter.SpellEnd`
(= `[0x74b0]`) is resolved at the next round top by `GameSession.RunMoraleCheck` (`CombatSpells.EndBySpell`);
`DispellUndeadTried` (`[0x74b8]`), `MonstersFrozen` (`[0x64a0]` from DISPELL's run, cleared at round end) and
`Dissolved` (`[0x74aa]`). SLEEP, AWAKEN (status-effect pass, parallel) and [original text] (s33, §[original text]) not changed here.

**Remaster (s32, user decisions — not DOS):**
- **CHARM (D-022):** DOS roll; success → "[original text]", `SpellEnd = Charmed`, the round stops right after the
  caster; ends like GREETINGS (kill XP, gold 0, treasure forfeited). No +15 reaction.
- **TELEPORTATION (D-023):** no roll; `SpellEnd = Teleported`, the round stops right after the caster; kill XP, gold 0,
  treasure forfeited.
- **FEAR (D-021):** DOS roll and `L −= 6`; then, unless a living monster is `never_flees` (`monster-traits.json`: Black
  Lord), "MONSTERS RUN" = DISPELL's run path (`MonstersFrozen`, `SpellEnd = Ended`, full gold per D-019). **s34 (user):**
  FEAR's runners stop for the whole round — every living monster is also set done for the round (state 2), so the
  first-round hold release after pass 1 (`0x10f3`) doesn't let them act in passes 3 and 5. DISPELL's run keeps the DOS release.
- **DISPELL UNDEAD (D-024):** "undead" = `monster-traits.json` `undead` list (XP-mod-7 set minus BANTIR, DEMON, HIGH DEMON).
- **SLEEP (D-020):** see §Status effects "Remaster (s32)".
- `Ended` (dissolve, DISPELL run, FEAR run) keeps the DOS timing: the round plays out, combat ends at the next round top.

### FIREFLASH target — OV2 `0x361..0x414` [C] (s49)

Runs right after the damage roll (`0x321..0x35d`, `magic-spells.md` §Effect coefficients), so the RNG order is
**damage first, then one slot roll per non-empty group visited**.
```
i := [0x6492] − 1                       # last entry of the rank-order table 0x74f4 = the BACK rank
loop:
    g := [0x74f4 + 2i];  L := [0x74be + 2g]          # L = last living index (−1 = wiped group)
    if L < 0: i −= 1                                # wiped group: no roll, move one rank forward
    else:
        k := rand0_99 · L / 100;  tries := 0          # 0x391..0x39f
        while HP[0x74c4 + 16g + 2k] < 1 and tries ≤ L:   # look for a living slot, wrapping k = L → 0
            tries += 1;  k := (k < L) ? k + 1 : 0
        if tries > L: i −= 1                        # no living slot: move one rank forward
    if i < 0: no target (0x414 → skip)
    if HP[g][k] ≥ 1: target = (rank i, slot k) → 0x5121 (display) and 0x1597(i, k, damage)
```
- **Back rank first.** The walk starts at rank position `[0x6492] − 1` (the built group count, wiped groups
  included), which is the back rank, and moves toward rank 1 only past a wiped group. FIREFLASH therefore always hits
  the **rearmost group that has a living monster**. Rank 1 is hit only when it is the only group left.
- **Quirk [C]:** monster slots are compact (`0x33c9` drops HP ≤ 0 slots after every death, §Re-rank), so the
  "living slot" search never runs, and `k = rand0_99 · L / 100` is at most `L − 1`. **In a group of two or more, the
  last slot (index `L`) is never the FIREFLASH target.** A group of one (`L = 0`) gives `k = 0`.
- `0x74c4 + 16g + 2k` is the monster's HP (`0x1597` subtracts the damage there and sets HP 0 / state 2 on a kill).
- **Manual (PDF p.19, printed 15):** "FIREFLASH (#5-#8) shoots a powerful blast of flame at **one monster close to the
  spellcaster**." In DOS it is a monster in the rank *farthest* from the party. **D-059 DECIDED (user s53): manual.**
- **Remaster rule (s53):** the target is one living monster of the **front rank** — the first entry of the rank order
  with a living monster (rank 1 unless it was wiped) — picked evenly among that group's living monsters
  (`rand(0, n)`). RNG order as DOS: damage roll first, then the one target roll (none for a group of one).
  Core: `SpellCaster.PickFireflashTarget`.

### [original text] — Guild slot 7 → combat member [C] (s33)

**The elemental is party slot 7.** Party records are `0x5bb0 + i·0x11a`, so slot index 6 is `0x624c` and
**`[0x62a8]` = slot 6's `@92`** (`0x624c + 0x5c`, the roster number). "An elemental is loaded" means "slot 6 is
occupied". The same trick gives `[0x618e]` = slot 5's `@92` (the 6th member exists). Nothing else names either word
(byte search of OV1, OV2 and the unpacked COM: `[0x62a8]` is read at OV1 `0x3d4`/`0x1fea`/`0x278f` and OV2 `0x8d6`;
`0x624c` appears in OV1 only).

**Templates [C].** The three elementals are the last 3 × 282 bytes of **`TWNS.INT`** (file `12141`, `12423`, `12705`).
Right before them is a 6204-byte zero block (`5937..12140`), which is exactly 22 records = `GUILD.DAT`, so the roster
buffer `[0x63e0]` is that block and the elementals are roster records 22–24 (Guild numbers `@92` = 23, 24, 25; the
manual's "Guild numbers"). Where `[0x63e0]` is set was not traced `[~]`; the layout fits exactly, and s9 saw them in
Guild→Inspect. Record contents (all other words 0):

| | FIRE ELEM | WATER ELEM | EARTH ELEM |
|---|---|---|---|
| `@92` Guild number | 23 | 24 | 25 |
| HP `@20/@22` | 100 | 150 | 200 |
| attack `@30` / parry `@32` | 200 / 60 | 150 / 80 | 100 / 100 |
| weapon pts `@240` / shield `@242` / armor `@244` | 40 / 100 / 7 | 30 / 100 / 14 | 20 / 100 / 21 |
| known spells (`@0x60`, value 1; all others 2) | 4–7 FIREFLASH I–IV | 20–31 CONFUSION, WEAKNESS, BINDING I–IV | 12–19 STRENGTH, PROTECTION I–IV |

All three: class `@220` = 3 FIGHTER, level `@88` = 20, INT `@2` = 25, every other stat 0, MP `@14/@16/@18` = 20,
status OK, displayed race `@256` = 15 (UNDEAD), birth race `@216` = 16 (outside the race table), no items, `@28` = 0,
**`@12`, `@246`, `@248`, `@250` = 0**. The manual (PDF p.9) agrees: "they are fighters, … they also have Guild
numbers, and … they live in Town #0".

**Guild prep — Add member `0x2786` [C].** The Guild menu runs Add only if the roster has a member (`0x24e1`) and
`[0x62a8] == 0` (`0x1fe8`); `0x2786` itself returns at once while `[0x62a8] ≠ 0` (`0x278d`).
```
list size := ([0x618e] ≠ 0) ? 25 : 22            # the 3 elementals are listed only when the party has 6 members
k := pick "[original text] ADD?"
if k > 21:  copy roster record k → slot 6 (0x624c)    # 0x27d8: no room check, no in-party flag @218,
            loop back → [0x62a8] ≠ 0 → return          #   no @254 / age +4, no 0x974 recount: [0x648e] stays 6
else:       normal Add (first empty slot 0..5, "There is no room" when full, @254, @264 += 4, @218 := 1, 0x974)
```
So: the party must already have **six members**; only **one** elemental can be loaded; once it is, Add is refused
until it is gone (the elemental has to be added last). The roster copy is never written back (town entry copies
`@92` 1..22 only, Drop/Purge copy only slots 0..5), so every load starts from the fresh template.

**Leaving slot 7 [C].** Slot 6 is cleared (`@218 := 0`, `@92 := 0`) by:
- Guild **Drop** (`0x225c`, at `0x22d1`) — every Drop, whoever is dropped;
- Guild **Purge** (`0x23d5`, at `0x2489`) — when the purged member is in the party;
- **town entry** (aging `0x1954`, at `0x19d3`): `[0x648e] −= 1` then clear. See the lead below;
- **party defeat** (OV1 `0x3bc`): `[0x648e]` 7 → 6, then `0x1bae(0x624c)` removes it.
Nothing else clears it, so the elemental lasts for one trip out of town and can be summoned in every fight of that
trip. The win screen (OV1 `0x4d5`) skips slot 7 (`endgame.md` §3).

**In combat before the summon [C].** `[0x648e]` = 6, so the scheduler, orders, monster targeting, prep `0x3263`, the
buffs and the end-of-combat code all skip slot 6. The build sets **slot 6's status := 1 (OK)** at every encounter
(`0x352f`; HP not touched — a dead elemental comes back OK with 0 HP) and its action := 4 (none, `0x339e`).

**The cast `0x8cc` [C].** No roll and no text (`0xdbd3(7)` only redraws the party panel). MP is spent either way
(`0x9f7`).
```
if [0x6490] == 5 and [0x62a8] ≠ 0:
    [0x648e] := 7;  [0x6490] += 1;  living list [0x64a2 + 2·[0x6490]] := 6
```
`[0x6490]` has two meanings during a round: the orders phase (`0x6a2a`) stores the last living **index**
(`0x43ae()`, no +1), but any monster turn (melee start `0xd93`, after a hit `0xf3a`, after a miss `0xfed`, after a
special `0x1065`) and RESURRECTION (`0x11d7`) store the living **count**, and a party death decrements it (`0x12f4`).
So:
- cast before any monster has acted this round (always true in pass 1): works with **6 non-DEAD members** (SLEEP counts);
- cast in pass 3 or 5 after a monster acted: works with **5 non-DEAD members**, fails with 6. The new list entry is then
  written one place too far (entry 5 stays stale) and monster targeting (`rand·[0x6490]/100`, `0x1734`) can pick that
  stale entry until the next orders phase rebuilds the list `[~]`;
- already summoned: with 7 non-DEAD the index is 6 → fails; with 7 members and one DEAD it passes again and lists slot 6
  twice (or lists a DEAD elemental) until the next rebuild `[~]`.

**Fighting [C].** It does nothing in the round it arrives (action 4 from the build; `0x1b4` skips actions ≤ 4). From
the next round it is an ordinary member: the orders phase (`0x6ab9`) lists it if OK and the player gives it orders
(default "repeat" via `0x6bc5` mode 0, or any menu choice), it rolls readiness with level 20, monsters spread their
attacks over 7 slots, and party spells (QUICKNESS/STRENGTH/PROTECTION loop to `[0x648e]`) include it. It can cast its
known spells with its 20 MP (no class check found `[~]`).
**But prep never reaches slot 6.** `0x3263` loops to `[0x648e]`, which is 6 at every encounter start, and the only
other writers of `@246/@248/@250` are the buffs and NINJA (caster); `@12` is written only by prep (byte search of OV1,
OV2, unpacked COM). So the summoned elemental fights with the record's stored values — **0** from the template:
hit chance `0/swings − defense` (never hits a monster with defense ≥ 0), weapon damage 0, armor 0, defense 0; PARRY
still subtracts the shield `@242` = 100. Only a QUICKNESS/STRENGTH/PROTECTION cast *after* the summon gives it real
values, and those then stay on slot 6 for the rest of the trip (no prep resets them). `[~]` on the gameplay result;
a DOSBox check would settle it (summon, then watch "%s MISSES").

**End of combat [C].** `0x3f29` first resets `[0x648e]` 7 → 6 (`0x3f2e`), then counts survivors (`0x3fa9`) and wakes
the party (`0x3fbb` → `0x3eff`). So the elemental **gets no XP or gold share**, is not woken, and does not count as a
survivor: if all six adventurers are DEAD, the result is state 6 (party defeated, `0x3fb2`) even if the elemental is
alive and the monsters are dead. While it is in combat it does keep the fight going (`0x43ae` counts it). Its HP,
status and MP stay on slot 6 for the next fight of the trip.

**In Core (s49, `Aging.ApplyTownEntry`) — [C]+[D✓] s49** (`capture_s49_2026-10-23.md` §3: elemental loaded, no summon needed → only the control
member "has aged"; the 6th member (past 70 %) aged on the next entry without it). The town-entry drop (`0x19dc`) lowers `[0x648e]` from 6 to 5 *before* the aging loop
`0x19eb` (which runs to `[0x648e]`) and the town loop's recount `0x974` (`0x1e7d`) restores 6 only afterwards. If
that reading holds, the 6th member skips aging (and the `@254` town write) on the visit where the elemental leaves.
Confirmed live. The `@254` write (`0x1b44`) loops all 7 slots, so it is not skipped (s49 bytes).

**Manual (PDF p.20, printed 16; p.9) vs DOS:**
- "[original text] (#48) calls up Earth, Fire, or Water (one spell summons one elemental) to fight for the party." DOS:
  one elemental, the one loaded at the Guild — agrees. **"to fight for the party" vs prep: conflict D-032 — decided (user, s34): fix the bug**; the remaster runs the
  normal prep on slot 7.
- "The elemental will only serve a party of six adventurers." DOS needs six **non-DEAD** members at the cast, and only
  five once a monster has acted earlier in the round (`[0x6490]` index/count mix). **Conflict D-033 — decided (user, s34):
  manual**, six non-DEAD members in every pass.
- "elementals must be added to your party while you are at the adventurer's Guild in one of the towns" / p.9 "add
  the elementals to the party while you're still at the Guild": agrees (Guild prep above). The manual says nothing
  about how long the elemental stays, XP, or leaving at town entry: no conflict.

**Core (s33):** `Party.Elemental` (slot 7, kept out of `Party.Members` outside combat), `GuildHall.TryAddElemental`
(six members, none loaded) and the Add refusal while loaded; Drop / Purge (party member) / town entry / party defeat
clear it. `CombatSpells.SummonElemental`: no roll, MP spent, condition **six non-DEAD members** (D-033, manual; the DOS
mid-round "five" case is not reproduced), appended with action none; `GameSession` removes it from the party on every
combat exit before rewards / wake-up, a fight won with all six adventurers DEAD counts as a wipe, and `BeginCombat`
sets a loaded elemental's status to OK (HP kept). The elemental's combat fields are a switch,
`ElementalPrepRule`: `Prepared` (default since s34, D-032 — normal prep from its record) or `Dos` (stored values, 0 for a
fresh template). Templates: `GameData/phantasie1/elementals.json` (`tools/scripts/extract_elementals.py`).

## Outputs / rewards — end of combat OV2 `0x3f29..0x421a` [C] (s33)
- On victory: `MONSTERS KILLED`. Then, on every exit, `%ld XP AND %ld GP` (`0xbcf3`, its only xref is `0x4013`) with the
  **per-survivor** shares, labelled `[original text]` (`0xbd05`, `0x4025`). DOS never prints the encounter totals.
  XP + gold stay in the RAM pool and items go to the found-item list (`SACK.DAT`), **not** distributed immediately.
- XP/gold are shared out on town entry (`town-menus.md` §Town-entry share split); items at the Inn hand-out screen.
- **Kill-XP payout [C] (s41, capstone OV2 `0x3fbe..0x40e1`).** `n` = `[0x6490]` (non-DEAD adventurers, sleepers count):
  ```
  0x3fbe  gold := gold / n                     # [0x750e:0x7510], 32-bit via f8d1
  0x3fd7  xp   := xp / (10·n)                  # [0x7512:0x7514]; 10·n built as ((n<<2)+n)<<1
  0x3ffa  print "%ld XP AND %ld GP" (xp·10, gold), "[original text]"
  0x402e  gold := gold·n;  xp := xp·n
  0x407a  gold += [0x63f6];  xp += [0x63f4];  each capped at 0xffff;  [0x63f6] := gold;  [0x63f4] := xp
  ```
  So each survivor's XP share is **⌊kill XP / (10·n)⌋ · 10**, and the pool `[0x63f4]` is kept **in tens**. The town split
  (OV1 `0x16dd..0x16f8`) multiplies it by 10 again before dividing by the shares (`town-menus.md`). Remainders are lost:
  up to 10·n − 1 XP and n − 1 gold per fight. The pool cap is 0xffff tens = 655,350 XP; the purse cap is 65,535 GP.
  **s35 check:** 4 SKELETON = 4·77 = 308 XP, n = 6 → ⌊308/60⌋·10 = **50** (old Core: 308/6 = 51). 4 BANTIR = 4·147 = 588,
  n = 5 → ⌊588/50⌋·10 = **110** (old Core: 117). Both match DOSBox exactly, with no extra factor and the monster records unchanged.
  **s41 DOSBox check:** 6 KOBOLD (50) + 5 ORC (90) = 750 XP, n = 5 → ⌊750/50⌋·10 = **150** each, as seen (divisible, so no loss).
  - BEG (gold = −`[0x63f6]`) `[~]`: if `f8d1` divides signed (truncating toward zero), the pool keeps
    `[0x63f6] − ⌊[0x63f6]/n⌋·n` = `[0x63f6] mod n` gold, and the screen shows −⌊G/n⌋. Core's BEG empties the purse and
    deposits 0 gold. Not checked live.
  - **Manual (PDF p.15):** "[original text] AND GOLD are displayed at the end of combat — the number of experience
    points earned from the encounter and any gold retrieved from the monsters. These will be divided among individual
    party members when they enter a town." DOS shows per-survivor shares rounded down to tens instead of the encounter
    total, and drops the remainders. The manual says nothing about rounding. **D-053 decided (user s42): manual. Built s46.**
  - **Core (s46, D-053 manual):** `GameSession.DepositEncounterTotals` runs at both deposit sites, after the elemental
    leaves and before the wake-up. It logs `"{xp} [original text] AND {gold} GOLD"` (the encounter totals; exact
    wording is the remaster's, the manual gives none) and puts the whole XP and gold in the sack — no per-survivor
    remainder loss. The D-037 town split (`TownMenus.Distribute`) does the dividing. `LootSack` keeps the DOS cap,
    655,350 XP (`CombatEncounter.XpPoolCap`), but holds whole XP rather than tens. The DOS share rule above stays
    documented only (s41's `SurvivorShares` was removed); `CombatEncounter.Survivors` remains.

**Full post-combat flow [C]** (every exit: kills, monster run, BEG/THREATEN/GREET/FLEE, spell end; flags as in §Party options):
```
0x3f2e  party size 7 → 6 (summoned elemental leaves);  [0x6498] := 0
0x3f42  if [0x74aa] or no monster alive:  [0x74aa] := 1; "MONSTERS KILLED"
0x3f77  if [0x74b0] == 1 (TELEPORTATION):  gold := 0; [0x74ba] := 0; [0x74b4] := 0
0x3f97  if dungeon and [0x74b4]:  0x2a26 clear the fight cell (§Party options)
0x3fa9  n := non-DEAD members (0x43ae + 1); n == 0 → state 6
0x3fbb  0x3eff wake the party; print XP/(10n)·10, gold/n "[original text]"; add gold/n·n → [0x63f6], XP/(10n)·n → [0x63f4] (cap 0xffff; pool in tens, s41)
        --- treasure roll ---
0x40e4  dungeon ([0x64e8] == 3):
            if first monster [0x64e6] == 0x49 (BLACK LORD) or not [0x74ba]:  [0x74ba] := 0
            else [0x74ba] := (rand0_99 ≤ 45·[0x74b2] + 2·L + 50)            # 0x40fe; L = [0x7508]
            if [0x74ba]:                                                      # 0x4130 find-item roll
                finder := first slot i (party order) with @24 ≠ 0 (non-DEAD) and rand0_99 ≤ @34 (find item, 0x5bd2)
                          # one roll per non-DEAD member, stops at the first success
                [0x74ba] := finder found
                if found and (id_a ≠ 0 or id_b ≠ 0):  "%s SPOTS AN ITEM" (finder's name)
        wilderness (0x41a3):
            if [0x74ba]:  [0x74ba] := (rand0_99 ≤ 2·L + 20)                   # no find-item roll
0x41c4  if ([0x74b2] or [0x74ba]) and (id_a ≠ 0 or id_b ≠ 0):
            0x421d(id_a, 1); 0x421d(id_b, 1)                                   # both, even if one id is 0
0x41fc  [0x64fe] := 1 (dead flag [C], never read — overworld-movement.md); Black Lord + [0x74aa] → state 7, else state [0x64e8] (endgame.md)
```
`id_a`/`id_b` = `[0x74a6]`/`[0x74a8]` = group 0's record `+0x35`/`+0x36` (§Monster record). `[0x74b2]` = 1 only for a
THREATEN surrender in a dungeon, and it bypasses the finder: a dungeon surrender with treasure ids **always** drops them.

**Item drop `0x421d(id, roll)` [C]** (also used by the dungeon script op 6 with `roll = 0`, `endgame.md`):
```
v := roll ? id + 3 − rand0_99/20 : id                 # 0x422b: id − 1 … id + 3
if id ≤ 0 or id ≥ 256:  return                        # tests the original id, after the rand was drawn
if id == 0x89 (137 = SCROLL 17) and not (dungeon and [0x64ec] == 9):  return     # guard tests the UNSHIFTED id
"YOU FIND %s. KEEP IT?"  (name table DS 0xec0 + 2v; menu 0x1b via 0xb207)
if the menu returns 0 (first option — YES assumed [~]):
    store v in the first empty word of the 60 found-item slots 0x63fa; none empty → "Sack is full!!!"
```
So the dropped item is not exactly the monster's id: it is shifted by `+3 − rand/20` (one of id − 1 … id + 3, each 20 %),
and `v` itself is not range-checked (`id = 1` can give `v = 0`, an empty slot) `[~]`. A refused or full drop loses the item.

**Manual check (p.10, ENCOUNTERS):** "TREASURE might be found, if the monsters had any and if a party member has good
find-item skills for locating them in dungeons. (Monsters defeated in the wilderness and who surrender in the dungeons
automatically give away their treasure.) You will be asked if you want to keep the treasure item … Answer Y or N" and
"ACCEPT SURRENDER, if the monsters do surrender, allows you to take the monsters' gold and items."
- Dungeon victory: presence roll + find-item roll — fits "might be found … find-item skills" (not a conflict).
- Dungeon surrender: always dropped — matches "automatically".
- **[D✓] s68** (`working/capture_s68_loot.md`): finder = first non-DEAD slot (DEAD 99 skipped, 7/7), "%s SPOTS AN ITEM" then one
  KEEP IT? per id; dungeon THREATEN surrender with every `@34` = 0 → KEEP IT? with no SPOTS (3/3); `[0,0]` monsters → nothing;
  a monster run still rolls. Not seen: a victory failing presence, a victory with no finder.
- **Conflict D-031 — decided (user, s34): manual, wilderness always drops (remaster skips the `2·L + 20` roll):** wilderness victory and wilderness surrender roll `rand0_99 ≤ 2·L + 20`
  (`0x41aa`; `[0x74b2]` stays 0 outside a dungeon) — the manual says wilderness monsters "automatically give away their
  treasure" and surrender gives "the monsters' gold and items".
- Not in the manual (no conflict): the `id − 1 … id + 3` shift, the scroll-137 guard, the Black Lord's no-treasure rule.
- **Guard leak [C] (s33):** the 137 guard checks `id`, not the shifted `v`. FIRE ELEMENTAL (record 53, treasure `[0, 135]`)
  rolls `v = 138 − rand/20`, so 20 % of its drops are **v = 137 = SCROLL 17** (the win scroll, `endgame.md`) anywhere it is
  met — no other record has a treasure id in 134…138. Whether that scroll then works outside the Fortress is P0.8 `[?]`.
- 90 vs 60 items: D-018 (decided, manual).

- **Guard leak fixed in the remaster (user, s34):** the SCROLL 17 guard tests the shifted `v`, so FIRE ELEMENTAL's `v = 137`
  drops nothing outside the Fortress.

**Core (s34):** `TreasureDrop.Roll` (flow above, wilderness roll removed per D-031) and `TreasureDrop.Drop` (`0x421d`; also for
script op 6 with `shift: false`), called by `GameSession` after the deposit and party wake-up on every non-wipe exit.
`CombatEncounter.FirstMonsterId` carries `[0x64e6]` for the Black Lord rule. KEEP IT?: `GameSession.PendingTreasure` /
`TreasurePrompt` / `AnswerTreasure(keep)` — YES stores it (full sack → "Sack is full!!!", item lost), NO loses it; pending
items are saved. `[~]`: a shifted `v ≤ 0` or past the item table gives nothing. Client prompt (s36): Game1 modal, Y/N.
- ~~Monsters dispelled yield no XP~~ — unsourced (not in the manual); DOS dissolve *adds* XP (§Combat spell effects, s31). Monsters that RUN on their own keep the kill XP and full gold (§Party options).

## Night encounters [X]
Wilderness night encounters: the member on **watch** rolls *listen* to wake the
party; failure means members start combat asleep (`%s IS SLEPT`/`AWAKENS`); a
priest/high-ranger AWAKEN spell speeds waking.

## Status effects — apply, tick, expire [C] (s31)
Combat status lives in four places: party status `@24` (`0x5bc8`: 0 DEAD, 1 OK, 2 SLEEP), the party's effective combat
fields `@246` armor / `@248` weapon damage / `@250` weapon hit (`0x5ca6/8/a`), the monster group records (`0x7522 + 0x39·g`),
and the monster state `[0x64b0 + 16g + 2k]` (1 asleep, 2 done this round, 3 ready). There is **no per-round tick and no
duration counter**: nothing in the scheduler `0x00e4` or at round end (`0x10f3..0x1135`: surprise flags, re-rank `0x19d7`,
redraw `0x7044`) writes any of them. Effects start the moment the action resolves (mid-pass, in scheduler order) and end
only when something overwrites them.

**Order inside one fight**
```
encounter start   prep 0x3263 — every encounter path calls it before the fight:
                    dungeon build (0x3386, only if [0x64e8] = 3), wilderness steps 0x606a / 0x60b3 / 0x6179,
                    night encounter 0x7183 (watch failed; 0x6179 is then skipped via the out flag)
                  @246 := @244, @248 := @28 + @240, @250 := @30, defense @12 rebuilt; every non-DEAD status := OK
                  night, watch failed (0x7186): every OK member := SLEEP, the watcher := OK again
build 0x3312      monster state := 3 − [0x64a0] (0x3a80) — the only way a monster starts asleep (state 1); group
                  records copied fresh, so monster-side changes from the last fight are gone; every action := 4
each round        0x3b70 compaction with [0x74ae] = 1: monster state 2 → 3 (state 1 kept); failed monster run → 2
                  orders 0x6a2a: only status-OK members are listed (0x6ab9) and get the repeat order (0x6bc5 mode 0);
                  a sleeper keeps action 4
                  passes p = 1, 3, 5 — party block, then monster block (§Turn order)
                  round end: surprise flags, re-rank, redraw — no status work
end of combat     0x3f29 → 0x3fbb: survivor count [0x6490] (sleepers count) then 0x3eff: every non-DEAD := OK
```
Party sleep therefore never outlives the fight it started in. The effective fields keep their last value after the fight,
but nothing reads them until the next prep rebuilds them.

**Writers of the effective fields and what wins.** Last write wins; there is no stacking table.
| Writer | Field | Rule | vs earlier effects |
|---|---|---|---|
| prep `0x3263` | all three | rebuilt from the record (above) | clears everything |
| QUICKNESS `0x0498` (cases 5–8 = #9–12) | `@250` | `min(255, @30·(100 + 10/25/45/70)/100)` | rebuilt from base `@30`: cancels SLOWED and an earlier QUICKNESS |
| STRENGTH `0x0527` (#13–16) | `@248` | `min(255, @28 + @240 + 2/4/7/11)` | rebuilt from base: cancels an earlier STRENGTH, and in DOS also special 17–20 (which scales `@248` there) |
| PROTECTION `0x05ab` (#17–20) | `@246` | `min(255, @244 + 1/3/6/10)` | rebuilt from base: cancels an earlier PROTECTION, and in the remaster special 17–20 (D-016 moves it to `@246`) |
| NINJA 2 `0x07ed`/`0x07f7` (#45), NINJA 1 `0x09a1`/`0x09ab` (#50) | all three | fixed 255/25/10 and 200/20/8 for the caster `[~]` (spell-effects pass) | overwrites |
| monster specials 17–24 (`0x1a7b` → `0x1d5a`) | `@248` (DOS; `@246` in the remaster, D-016) / `@250` | `(field·scaled + 5)/10` of the **current** value, per member the resist roll catches | compounds: stacks on a buff and on itself |

So QUICKNESS after "[original text]" fully undoes it, while SLOWED after QUICKNESS scales the quickened value. The SLASH
swing count reads `@250` when orders are given (`0x6bc5`), so a change shows in the next round's orders; the hit chance reads
it on every swing. The manual's "passive magic … counteracts any previously cast spells … an additional STRENGTH spell of any
kind will cancel the STRENGTH 3 spell" matches rebuild-from-base (a weaker second STRENGTH replaces a stronger one). Monster
group changes (CONFUSION/WEAKNESS/BINDING `0x1382` codes 1–3, specials 5–16) scale or set the group record and last until the
next build.

**Party sleep / stun**
- Fall asleep: monster special 33 (`0x1b43`, status := SLEEP, action := 4), the STUNNING BLOW rider (`0xf94`, action and
  repeat := 4), the night encounter (`0x71a7`, before the build sets every action to 4). Stun is plain SLEEP.
- Wake: the scheduler roll each pass (`0x15d`: `rand ≤ 16` and HP ≥ 1 → OK, "%s AWAKENS"; that is the member's pass);
  AWAKEN #51 (`0x9bf` → `0x3eff`: every non-DEAD := OK, one "PARTY AWAKENS" line); end of combat (`0x3eff`); next prep.
- A member woken mid-round still has action 4, so it does nothing until the next orders. Monsters can hit sleepers (§Turn order).

**Monster sleep.** State 1 (asleep, `rand < 15` per pass to wake, `0x1081`) is only ever set by the build when `[0x64a0]` = 2
(monsters surprised, `[C]` s33, §Surprise). The party's **SLEEP #42** (`0x7a5` → `0x1382(caster, 6, 0)`, case `0x1501`) does **not** use it:
```
for each rank position, each living slot k:       # rank order, then slot order
    rand0_99                                       # scaled power roll, power 0 (shared loop 0x148d), unused
    if rand0_99 >= L²:  "%s IS SLEPT"; state := 2  # L = [0x7508]; L ≥ 10 → never works
```
State 2 is "done this round", reset to 3 by the next round's compaction, so a slept monster only loses the rest of the
round it was slept in; it gets no wake roll and prints no "AWAKENS". Casting SLEEP again works the same way ("a second SLEEP
spell will not deepen a trance" — trivially true). There is no caster term.
**Manual check — conflict, reported s31, decided s32 (D-020: manual):**
(a) *duration* — p.15 SLEEP "causes the opponents to fall into a daze, and they cannot attack, parry, or cast spells"; p.14
combat spells "last only for the duration of combat" and "a second SLEEP spell will not deepen a trance". Read together this
suggests a trance that persists; DOS gives the rest of the casting round only (state 2 at `0x1501`, reset at `0x3b7e`).
(b) *resistance* — p.14: active magic's "effectiveness depends upon the spellcaster's magic power and experience level, as
well as the strength of the target"; DOS SLEEP depends only on `L²` (`0x1501`), no caster term, `L ≥ 10` always resists.

**Core (s31):** `CombatEncounter.WakeParty` (`0x3eff`) on every combat exit in `GameSession` and for AWAKEN;
`Monster.DoneThisRound` = state 2 (reset at round start, set by acting / waking / failed run).

**Remaster (s32, D-020 — not DOS):** SLEEP rolls once per group with the CONFUSION/WEAKNESS/BINDING caster-vs-CV roll
(`rand ≤ 5·(caster @16 MP max − group CV) + 50` — `@14` until s33, see §To-hit; shared `SpellCaster.CasterHitsGroup`; `L` not used;
since s34 plus the D-028 level term `2·(caster level − group level)`). On a hit every awake monster
of the group gets `MonsterStatusFlags.Asleep` = DOS state 1 (no turns, wake on `rand < 15` per pass, `0x1081`, across
rounds); sleepers already asleep are skipped ("The spell has no effect" if none fell asleep). Sleepers stay valid melee /
spell targets and don't wake when hit (user, s32; the old first-pass `IsNeutralized` target skip is removed). Monster
`StatusFlags` are saved in mid-combat snapshots.
Buff spells already rebuilt from base (`SpellCaster.ApplyQuickness/Strength/Protection`), specials scale current values
(`MonsterSpecials`), `Prepare(fightStart)` = prep. Night encounters and surprise (`[0x649e]`/`[0x64a0]`) are still not in Core (P0.3).
Save (s33): the combat snapshot stores `Prep` (armor / weapon damage / weapon hit effective + packed action, per member), so a
mid-combat load keeps QUICKNESS / STRENGTH / PROTECTION / NINJA / SLOWED / armor-reduced, as DOS keeps them until the next prep.
Older saves without it fall back to `Prepare(fightStart: false)`; no version bump. The old write-only `PartySlowed` /
`PartyWeakened` / `*FlatBonus` / `QuicknessPercentBonus` flags (no DOS counterpart — DOS has no "slowed" flag, only the scaled
`@250`) were removed.

## HP by class & level [C] (manual chart [X]; CON scaling OV1 `0x0ab6`, s31)
Manual p.13, "Hit Points by Class and Level for Adventurers with 10 Constitution" (averages). Every cell is exactly
**2 × `hp_factors[class][L]`** (`TWNS.INT @0x000`, `creation-tables.json` `hp_factors`) — the table below is that product,
checked cell by cell against the `pdftotext` reading (the old OCR copy had Priest L1 6, L11 96, Monk L3 16, Wizard L2 6, L6 26 wrong).

| Lvl | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| FIGHTER | 10 | 20 | 32 | 44 | 56 | 70 | 82 | 94 | 106 | 120 | 130 | 140 | 150 | 160 | 170 |
| RANGER | 8 | 18 | 28 | 40 | 50 | 60 | 72 | 84 | 96 | 110 | 120 | 130 | 140 | 150 | 160 |
| PRIEST | 8 | 12 | 20 | 30 | 40 | 50 | 60 | 70 | 80 | 90 | 98 | 106 | 114 | 122 | 130 |
| MONK | 8 | 10 | 18 | 22 | 32 | 44 | 54 | 62 | 70 | 80 | 88 | 96 | 104 | 112 | 120 |
| THIEF | 6 | 10 | 18 | 22 | 32 | 40 | 48 | 54 | 60 | 70 | 78 | 86 | 94 | 102 | 110 |
| WIZARD | 6 | 8 | 12 | 16 | 22 | 28 | 34 | 38 | 44 | 50 | 56 | 62 | 68 | 74 | 80 |

**CON scaling [C]** — the level-up routine OV1 `0x9ee` (`town-menus.md` §Level-up gains; also builds new characters with
L = 0) re-rolls HP max from scratch on every level:
```
dice = (hp_factor[class][L] · (CON + 5)) >> 4       # OV1 0x0ab6..0x0adc: CON = live @6, sar 4
roll = Σ dice × (rand/0x2aab + 1)                     # d3 each (0x0af6)
hp_max = roll > hp_max ? min(roll, 255) : hp_max + 1  # never drops; no roll once hp_max = 255
```
So HP scale **linearly with CON + 5**: relative to the CON-10 chart, a character has `(CON + 5)/15` of the chart's dice (DOS
`/16` makes even CON 10 a bit below the chart). CON lost at death (D-009, `0x12cf`) is read by every later re-roll, but HP max
can't drop because of the `+1` floor. The manual only says HP max "depends on the character's constitution, class, and
experience level" — no formula, so no conflict beyond **D-007** (user: dice = `factor·(CON + 5)/15`, so CON 10 rolls exactly
`factor` d3 and averages the chart; Core `LevelUp.HitDiceCount`, s17).

## Offset 28 dual-use — retracted (s19)
The old "`@28` = scroll count *and* weapon hit" reading came from OV2 offsets taken with the wrong
party base. `@28` is weapon damage A; weapon-hit effective copies the attack skill `@30`; the
scroll count on the equipment screen is counted from the carried items (COM `0x2cf6`). Buying
scrolls changes no combat field.

## Still unknown / next checks
- Equip selector is **[C]** for `5ca0` / `5ca4` / `5ca2`; trace the separate
  creation/equip writer for `5bcc` and `5bce` (**[~]**).
- ~~Initiative / turn order coefficients~~ — §Turn order (s21).
- ~~Monster special-ability effect (`+0x20` → `0x16a9`)~~ — §Monster specials (s26).
- ~~How SLASH chooses 3 vs 4 swings~~: `@250/75 + 1`, with the skill gate (§Action chooser, s20).
- ~~Status-effect ordering (SLOWED, QUICKNESS, sleep, etc.)~~ — §Status effects (s31).
- ~~CON → HP scaling~~ — §HP by class & level (s31).
- ~~Decode the `0x1a6c` monster table~~ — done s22 (EXEPACK image, §Monster record); `ToCombatant` uses it.
- Dynamic verify: breakpoints on `0x655b`, `0x3675`/`dfa9`, damage product before `/10`.

## Remaster note
Injectable RNG: expose both raw LCG (`3c96`) and `roll_0_99 = raw/0x148`, plus
`isqrt` matching `FUN_0000_3675`. Implement the three to-hit paths and both
damage formulas above. Keep rank model and packed action codes.
