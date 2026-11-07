# Magic / Spell System

Spell **names/order** are confirmed from `PHANTASI.COM` @ `0x6e8a` (55 entries,
see `data-tables.md`). Categories + effects below are manual-confirmed [X];
combat effect messages are confirmed in `OV2.OVR` [C]. Per-spell **magic-point
cost** is **[C]** (computed, not tabled). Heal scaling for HEALING I–IV is
**[C]** (s18: RNG 0..99). Other spell damage/buff
coefficients remain **[~]** for specials. Potion restore formulas below are **[C]**.
Tiered combat-spell coefficients (FIREFLASH…FLAMEBOLT) are **[C]** from OV2
static decode (see tables below).

## Core mechanics [X]
- Casting consumes the caster's **magic points**; a spell costs **1–4 MP**.
- MP restored by **MAGIC potions** and **Inn** rests (Inns can also raise a
  caster's MP maximum).
- Casters: **WIZARD, PRIEST, MONK, RANGER** (need high INT). Non-casters have 0
  MP. Each character has a cap on **how many spells** they can learn (INT, class,
  level; fighters get +3, `town-menus.md` §Learn spells); learning is done at the
  **Guild** for a fee scaled by level and CHA, and can fail (≈ INT/21, fee still paid). New wizards/priests start knowing one L1 spell.
- **Active** combat magic scales with caster magic-power + level vs. target
  strength, and can stack cumulatively. **Passive** combat magic always works
  and cancels the opposing effect (e.g. STRENGTH cancels an enemy WEAKNESS;
  a new STRENGTH replaces the old one). [X]

## MP cost formula [C]

No contiguous 55-entry cost table exists in `PHANTASI.COM` / `OV*.OVR`. Cost is
computed from the 0-based spell index:

```
mp_cost = (spell_index % 4) + 1   # → 1, 2, 3, or 4
```

Pinned in `OV2.OVR` at the cast deduct site (`0x47da`–`0x47f1`):

```
ax = spell_index
cx = 4
idiv cx                 ; dx = index % 4
di = dx + 1
magic_cur -= di
```

The same expression is used for the pre-cast affordability check at `0x6e31`
(`cost = (index+1) - (index/4)*4`, algebraically identical). Applies to **all**
55 spells, including non-tiered specials (e.g. CHARM=1, SLEEP=2, …). The index is the **DOS slot**
(pointer table DS `0x174d`, 60 slots): DIVINE I is slot **57**, so it costs `57 % 4 + 1` = **2** MP, not 3 (§DIVINE I, s41).

## HEALING effect scaling [C]

After MP deduct, combat HEALING I–IV (1-based indices 1–4) runs at `OV2` `0x027d`, inside the combat cast
routine `0x00e4` (s18; older notes pointed at `0x47fb`, which is the wilderness/dungeon cast — see below).
With `tier = spell_index + 1` (so 1..4):

```
heal = ((6 * tier^2 + 4) * rng) / 100
if heal == 0: heal = 1          # OV2 floor
hp_cur = min(hp_cur + heal, hp_max)
```

`rng` comes from `OV2` `@0x655b` = `rand() / 328` (`rand` = COM `0x3c96`, 0..32767) → exactly **0..99** [C].
Packed combat action for HEALING:
`1000 * tier + target_member_index` (not the `0x68+` path).

## Combat cast encoding [C]

Non-HEALING combat casts pack as `0x68 + (spell_index - 3)` (≥105). After
subtracting `0x68`, the resulting **cast_id** drives the effect switch
(`LAB_0000_01fb`). MP deduct uses cast_id / tier as the intermediate in a
formula algebraically identical to `(index % 4) + 1`.

## Effect coefficients recovered from OV2 [C]

Static decode of the cast switch (params are decimal / hex as in decomp):

| Line | cast_id | 0-based idx | Param / effect |
|---|---|---|---|
| FIREFLASH I–IV | 1–4 | 4–7 | damage scale `9, 0x1d, 0x3a, 100`; dmg = `rng·param·INT/2000 + 1` (single target), **INT = caster `@2`** (`0x339`, s33; was written without INT) |
| QUICKNESS I–IV | 5–8 | 8–11 | `%` weapon-hit `10, 0x19, 0x2d, 0x46`; `hit_eff = min(255, @30*(100+p)/100)` (attack skill `@30`, OV2 `0x0498` reads `+0x1e` from base `0x5bb0`; s41 fix, was `@28` from the old `0x5bb2` base); cancels SLOWED |
| STRENGTH I–IV | 9–12 | 12–15 | flat dmg `2, 4, 7, 0xb`; `dmg_eff = min(255, A+B+flat)`; cancels WEAKNESS |
| PROTECTION I–IV | 13–16 | 16–19 | flat armor `1, 3, 6, 10`; `armor_eff = min(255, base+flat)` |
| CONFUSION I–IV | 17–20 | 20–23 | scale `%` `0x50, 0x3c, 0x28, 5` via `FUN_0000_1382` mode 1 → special-ability gate `+0x1e` (s22, was "CV"); D-014: remaster IV = 0 % (manual). Modes 1–3 (CONFUSION/WEAKNESS/BINDING) roll **once per group** and scale the shared group record (s25) |
| WEAKNESS I–IV | 21–24 | 24–27 | scale `%` `0x50, 0x3c, 0x28, 0x14` mode 3 (attack power) |
| BINDING I–IV | 25–28 | 28–31 | scale `%` `0x55, 0x46, 0x37, 0x2d` mode 2 (defense, signed — negative defense moves toward 0; D-013: remaster `Magic.BoundDefense` — DOS scale for positive, negative pushed down, min cut 3/6/9/11) |
| MINDBLAST I–IV | 29–32 | 32–35 | dmg scale `5, 0xb, 0x29, 0x51` mode 4 — `(rng*param)/100`, no armor |
| FLAMEBOLT I–IV | 33–36 | 36–39 | dmg scale `7, 0xf, 0x1f, 0x42` mode 5 — armor-resisted |

**FIREFLASH caster term [C] (s33)** — OV2 `0x321..0x35d`: `rand` (0..99) × param (16-bit `imul` → 32-bit), × caster
`[c·0x11a + 0x5bb2]` = **`@2` INT** (sign-extended, 32×32 multiply), ÷ 2000 via `0xf8d1`, `+ 1`. So the maximum is
`99·param·INT/2000 + 1`: INT 18 gives 1–9 / 1–26 / 1–52 / 1–90; INT 20 gives 1–9 / 1–29 / 1–58 / 1–100.
**Manual (PDF p.19, printed 15):** "Fireflash 1 — 1-10 points damage … 2 — 1-30 … 3 — 1-60 … 4 — 1-100", fixed, with no INT
term (PDF p.8: INT is "the mental prowess necessary for learning and casting spells"; race INT maxima 13–19). **Conflict
D-029 — decided (user, s34): manual ranges, scaled by INT. Remaster design, not DOS:**
`damage = 1 + rand · (max − 1) · min(INT, 18) / (99 · 18)`, `max` = 10 / 30 / 60 / 100 — INT 18 (Human ceiling) gives the
manual range exactly, INT 19 is capped there, lower INT shrinks the top (INT 13: 7 / 21 / 43 / 72). Core:
`Magic.FireflashDamage(roll, tier, INT)`; the s33 no-INT bug is gone.

**Caster operands across combat spells [C] (s33):** CONFUSION / WEAKNESS / BINDING resist = caster **`@16` MP max** vs group
CV (`combat-resolution.md` §To-hit, `0x13b5`; the old "caster CV `@14`" was the same address read with base `0x5bb2`; remaster D-028 adds `2·(caster level − group level)`, s34);
FIREFLASH damage × **INT `@2`** (remaster: D-029 formula above); CHARM, TELEPORTATION, FEAR, DISPELL UNDEAD use **level `@88`**; MINDBLAST, FLAMEBOLT,
DISSOLVE, DOS SLEEP, HEALING have no caster term. Remaster SLEEP (D-020) shares the `@16` roll.

Specials (CHARM…AWAKEN, cast_id 37–47): decoded **[C]** (s31) — jump table OV2 `0x9d1`, per-handler rules in
`combat-resolution.md` §Combat spell effects (DOS undead = XP mod 7, remaster = `monster-traits.json` list, D-024; [original text] [C] s33, §[original text] there).

## Town casting: Option → Cast spell [C] (session 13)

Static decode (capstone) of OV1 `0x254e`, the town Option → Cast spell loop. Strings via the OV1 base
`file + 0x5ff7`: "WHO WILL CAST A SPELL?" (`0x5d4e`), "[original text] HEAL?" (`0x5d65`),
"%s IS RESURRECTED" (`0x5d7f`). This covers **town** casting only. The OV2 RESURRECTION routine (combat +
wilderness/dungeon) and the wilderness/dungeon HEALING are in §OV2 RESURRECTION below (s18).

```
loop:
    if no occupied party slot (+0x5c != 0, 6 slots): return
    caster = pick member "WHO WILL CAST A SPELL?"     ; cancel → return
    if caster.status (@24) == DEAD: set caller's out-flag, return        ; [D✓] s65: town cast loop exits silently
    spell = spell picker (COM 0x1602)                  ; cancel → back to "WHO WILL CAST"
    caster.mp_cur (@18) -= (spell % 4) + 1             ; deducted BEFORE the effect, whatever happens
    switch spell:
        0..3  HEALING I–IV  → town_heal(tier = spell + 1)
        43    RESURRECTION  → town_resurrect(caster)
        53    TRANSPORTATION→ town_transport()          ; leaves the cast loop
        other → nothing                                  ; MP is still spent
    goto loop
```

**Spell picker** (COM `0x1602`): lists the caster's spell-flag words at `@96` (`+0x60`) equal to **1**
(known). It refuses a spell whose cost `(idx % 4) + 1` exceeds MP cur `@18`. **No category filter was found**
in the list routine (`0x168c`). If that holds, a known combat spell can be picked in town, its MP is spent, and
nothing happens. The manual says town Cast spell offers only non-combat and everywhere spells. **[~]**, not raised
as a conflict yet: confirm in DOSBox with a caster who knows a combat spell.

### Town HEALING (OV1 `0x261a`) [C]

```
target = pick member "[original text] HEAL?"     ; cancel → return (MP already spent)
c      = 6 * tier^2 + 4                              ; 10, 28, 58, 100
heal   = rand() / (32767 / c)                        ; integer division; rand = COM 0x3c96, 0..32767
target.hp_cur (@22) = min(target.hp_cur + heal, target.hp_max (@20))
```

- No floor: **0 is possible** (about 1 in `c`). The maximum `c` is reached only on the top few `rand` values
  (e.g. HEALING I: `rand ≥ 32760`, 8/32768). In practice the range is 0 … c−1, rarely c.
- No status check: a DEAD target's HP rises but it stays DEAD.
- Combat HEALING (OV2, above) uses `(c * rng) / 100` with floor 1, i.e. **1–9 / 1–27 / 1–57 / 1–99**, which
  matches the manual exactly. Town casting gives 0–10 / 0–28 / 0–58 / 0–100. Manual vs DOS-town → `DIFFERENCES.md` **D-010**
  (decided: manual; the remaster uses the combat formula everywhere).
- C64 cross-check (s16): C64 town HEALING is `INT(RND·c)` (0 … c−1, no floor) and combat has floor 1, the same split
  as DOS (`reverse-engineering/c64/bytecode-notes.md` §HEALING).

### Town RESURRECTION (OV1 `0x2693`) [C]

```
for i in 0 .. party_size-1 ([0x648e]), in party order:
    t = party[i]
    if t.status (@24) != DEAD: continue
    if t.con (@6) == 0:        continue     ; age-death zeroes stats (town-menus §Aging) → can't be raised
    if t.@92 == 0:             continue     ; empty slot
    roll = rand() / 327                     ; 0..100
    if roll < 50 + 5 * (caster.level (@88) − t.level (@88)):
        t.status = OK (1)
        t.hp_cur (@22) = t.hp_max (@20)
        copy party record → roster record (OV1 0x7bf(src=party, dst=roster[@92−1], 0x11a))
        print "%s IS RESURRECTED"
        return                              ; at most one member per cast
; nobody raised: no message; the 4 MP are still spent
```

- **No target choice.** Each dead member in party order gets an independent roll until one succeeds.
- **Success chance** = `clamp(50 + 5·Δlevel, 0, 101) / 101` (Δ = caster − target level). Equal levels: 50/101 ≈ 49.5 %.
  Δ ≥ +11: always. Δ ≤ −10: never.
- **No CON change, no age change, no gold.** The manual's CON loss of 1–2 happens **at death** in DOS (OV2 `0x12cf`,
  `combat-resolution.md` §Death), not here → `DIFFERENCES.md` **D-009** (decided s14: DOS rule, loss at death).
  Corpses with CON 0 are skipped, so they can't be raised.
- HP is set to **max** (Core matches since s18; it used max/2 before).
- C64 cross-check (s16): town, wilderness and dungeon resurrection on the C64 use the same rule (auto-target in party
  order, `50 + 5·Δlevel` %, HP max, no CON change); only the roll granularity differs (`/100` vs DOS `/101`).
  `reverse-engineering/c64/bytecode-notes.md` §RESURRECTION.
- Cost 4 MP (`43 % 4 + 1`), the same as the manual's level-4 listing.
- OV1 `0x7bf(a, b, n)` = copy `n` bytes **from `a` to `b`**: it builds far pointers with DS (COM `0x36ba`) and
  calls the `rep movsw` copier (COM `0x5055`: `lds si` = arg 1 → `les di` = arg 2). This settles the copy
  direction for every `0x7bf` caller (PLAN P0.1 town-entry item: read each caller's argument order).

### OV2 RESURRECTION (`0x113b`) and wilderness/dungeon HEALING [C] (session 18)

`0x113b(caster)` is called from the combat cast routine `0x00e4` (at `0x07e6`) and from the wilderness/dungeon
cast routine `0x4790` ("[original text]", at `0x4859`). Message "%s IS RESURRECTED" = OV2 `0x7677`
(imm `0xb9b4`, base `file + 0x433d`).

```
for i in 0 .. party_size-1 ([0x648e]), in party order:
    t = party[i]
    if t.status (@24) != DEAD: continue
    if t.con (@6) <= 0:        continue
    if rng0_99 (0x655b) >= 50 + 5 * (caster.level (@88) − t.level (@88)): continue
    t.status = OK
    t.hp_cur (@22) = t.hp_max (@20)
    [0x74fa + 2i] = 4 ; [0x5b96 + 2i] = 4 ; call 0x43ae ; [0x6490] += 1     ; [C] s41, see below
    print "%s IS RESURRECTED"; redraw (path depends on [0x64fc] == 5)
    return                                   ; at most one per cast
```

- **Side writes [C] (s41):** `[0x74fa+2i] = 4` is the raised member's combat order = 4 "no action" (the round loop
  skips orders ≤ 4, `0x1b1`), so it does nothing for the rest of the round; `[0x5b96+2i] = 4` is its previous-order
  mirror, so next round's default order is PARRY (< 10, `combat-resolution.md` §Action encoding). `0x43ae` rebuilds
  the non-DEAD member list `[0x64a2]` (monster targets) and returns count − 1; `[0x6490]` is the non-DEAD count used
  as the end-of-combat gold/XP divisor, so a member raised in combat shares the payout. Called from the field cast
  too, where the writes are harmless (the next encounter rebuilds them). Core: `Prep[i].PackedAction := Cleared` on a
  combat raise; target list and survivor count are derived from status (`CombatEncounter.Survivors`).
- Same rule as town (OV1 `0x2693`): auto-target in party order, one roll per eligible corpse, HP max, no CON change.
  The only difference is the roll: OV2 uses `rand/328` (0..99, chance `(50+5Δ)/100`), town `rand/327` (0..100,
  chance `/101`). The C64's `/100` matches OV2. No `@92` empty-slot check here.
- **Wilderness/dungeon HEALING** (`0x47fb`, in `0x4790`): asks "ON WHOM:" (picker at `0x5a0a`), then
  `heal = ((6·tier²+4) · rng0_99) / 100`, applied via `0x4930(member, hp + heal)`. **s41 correction [C]:** `0x4930`
  adds 1 when the new value equals the current HP (`0x4941..0x4946`), then clamps to HP max — so a 0 roll heals 1:
  the field range is **1–9 / 1–27 / 1–57 / 1–99**, the manual's, like combat. (s18 wrote "no floor"; only the town
  routine `0x261a` lacks the floor.) Manual ranges apply in the remaster everywhere (D-010).
- Combat `0x07b8` (`rng0_99 < 3·caster level + 50` → `[0x74b0] = 1`) is **TELEPORTATION** (s31, `combat-resolution.md` §Combat spell effects).
- **Core (s18):** `SpellCaster.TryResurrect` shared by combat, `CastLocation.Field` and `CastLocation.Town`
  (town roll `NextDosWord()/327`).

### Wilderness/dungeon cast dispatch (OV2 `0x4790`) [C] (s41)

"[original text]" (OV2 imm `0xbe90`) → member picker `0x5a0a`; cancel returns. Spell picker COM `0xbf36` (the
same 60-slot list as town); cancel → back to the caster prompt. Then, **before** the effect:
`caster.@18 (MP cur) -= slot % 4 + 1` (`0x47d8..0x47f1`). The switch at `0x48f6` is on `slot + 1`:

```
1..4  HEALING I–IV     → 0x47fb  (above; floor 1 via 0x4930)
44    RESURRECTION     → 0x113b(caster)            (§OV2 RESURRECTION)
52    MONSTER EVAL.    → 0x4860
53    VISION           → 0x48b3
other → nothing; the MP is still spent              (TRANSPORTATION, DIVINE I, every combat spell)
```

`0x4790` never writes `@94` (cast-since-rest, `+0x5c0e`): field casts don't feed the Inn's MAGIC INCREASES roll
(only the combat cost path `0x0a55` sets it). **Remaster:** the manual says any cast counts → D-070 decided manual, so field casts set it too. No travel time is added (`travel-time` has no caller here).

**MONSTER EVAL. (`0x4860`) [C]** — no roll, no monster is looked at:

```
if [0x64e8] == 2:                         # wilderness
    L = 2 · ([0x649c] + 1); if L > 12: L = 12     # [0x649c] = tile % 10 of the cell the party stands on (T)
else:                                     # dungeon ([0x64e8] == 3)
    L = 2 · levelbuf[0x4e7] + 2           # [0x6504] level buffer, byte 0x4e7 = the level's monster tier
[0x7508] = L                              # the combat "encounter level" word; the next encounter build resets it (0x3542)
print "[original text] %-2d" (imm 0xbeae)
```

So the answer is a fixed number per wilderness terrain block (T 0..5 → 2, 4, 6, 8, 10, 12) or per dungeon level tier
(DNG tier 0..5 → 2..12). Writing `[0x7508]` outside combat has no lasting effect (rebuilt from 0 on every encounter).
**Manual (PDF p.19, printed 15):** "MONSTER EVALUATION (#52) determines the approximate experience level of nearby
monsters. Although it occasionally yields inaccurate results, it is a good spell to cast immediately upon entering a
dungeon and at regular intervals in the wilderness." DOS gives an area estimate, never random and never from the
actual monsters met. **D-056 (decided: manual):** Core casts `SpellCaster.ManualMonsterEvalLevel`, which is the rounded
average level of the monsters that can be met at the party's spot, and is off by ±1 on 10 % of casts (never below 1;
values set by the user in s57). The DOS formula stays as `MonsterEvalLevel`.

**VISION (`0x48b3`) [C]** — wilderness only; in a dungeon the MP is spent and nothing happens:

```
if [0x64e8] == 2:
    r = (caster.@18 · 100 + 500) / 400        # @18 = MP cur AFTER the 1 MP cost; = (MP + 5) / 4, integer
    [0x750a] = r
    0x566b(x = [0x751a], y = [0x751c], &[0x750a])
    save the screen to the back buffer (0xb954 = COM 0x0d30; 0xb914 is the restore, s67)
```

`0x566b` is the same reveal every wilderness step uses (radius word `[0x750a]`, normally 1): for every cell of the
square `x−r..x+r × y−r..y+r` clipped to the 26×20 region (no crossing into neighbour regions), skipping the party's
own cell and cells already drawn (byte array DS `0xd0be`, cleared on region load `0x54c4`, set when a cell is drawn
`0x5573` — a drawn cell is never fogged), it un-fogs the cell (`0x53a5`: `raw ≥ 120 → raw − 120`) and draws it
(`0x54fa`). Then it **resets the radius to 1** (`0x5703`), so the wide reveal happens once. Radius by remaining MP:
0–2 → 1 (the normal step reveal), 3–6 → 2, 7–10 → 3, … The revealed bytes are the region bytes written back to
`OUT*.DAT`, so the reveal persists.
**Manual (PDF p.20, printed 16):** "VISION (#53) gives the spellcaster the ability to see in all directions for
hundreds of miles. It is particularly useful for mapping vast unknown areas." DOS reveals a square of radius
`(MP+5)/4` cells inside the current region only. **D-057 (decided: manual, s57):** Core reveals the whole current 26×20 region
(`VisionManualRadius`) and nothing in the neighbouring regions. The DOS radius stays as `VisionRadius`.

### DIVINE I (DOS slot 57) [C] (s41)

- The spell lists use **slot numbers**: the name table DS `0x174d` has 60 slots (0–53 + 57 named), the picker COM
  `0x1491..0x15f6` lists slot `k` when its flag word `@96 + 2k` is 1, counting over all 60 slots (`0x166d`,
  `cmp 0x3c`), and returns the slot (`0x1635`). So DIVINE I is known when **`@210`** = 1 (endgame OV1 grant,
  `endgame.md`), and `@204/@206/@208/@212/@214` are flag words of empty slots 54–56, 58, 59.
- Cost `57 % 4 + 1` = **2** (picker affordability COM `0x160e`; field `0x47da`; combat `0xa04`
  `cast_id − 4·((cast_id−1)·100/395)` with `cast_id = 54` → 2).
- **No effect anywhere.** Town loop (OV1 `0x254e`): only 0..3 / 43 / 53 have cases → nothing, 2 MP spent. Field
  (`0x4790`): no case for 58 → nothing, 2 MP spent. Combat: `cast_id = slot − 3 = 54` passes none of the tier ranges
  (1..36) and fails the special-jump bound `cast_id − 37 < 11` (`0x9e7`) → straight to the MP deduct `0x9f7`. Whether
  the combat action chooser (`0x6f78`) offers it — **[D✓] s65**: it does, and it is a 2-MP no-op.
- The manual has no DIVINE I (its spell list stops at #54 TRANSPORTATION) → no manual conflict; DOS decides: 2 MP,
  no effect. Remaster: `spells.json` `dos_slot: 57`, cost from the slot, `GuildCodec` maps remaster index 54 ↔ `@210`.

### Town TRANSPORTATION (OV1 `0x275b`) [C structure]

Picks a town (COM `0x1992`). Cancel → return, with MP already spent. Otherwise sets the current town `[0x63e2]`
and runs **town entry** `0x1954`, which also applies aging (`town-menus.md` §Aging), then redraws (COM `0x1c97`).

## Categories
`E`=Everywhere · `C/A`=Combat-Active · `C/P`=Combat-Passive ·
`N/W`=Non-combat Wilderness · `N/T`=Non-combat Town · `N/WD`=Non-combat
Wilderness/Dungeon (manual spelling; older notes had `N/WO`). **Manual:** `CAST SPELL` outside combat only
offers non-combat spells for that place + everywhere spells. **DOS (s35 DOSBox):** the town picker lists every
known spell, and a combat spell picked there spends its MP and does nothing. **Remaster (D-035, user: manual):**
`SpellCaster.IsCombatOnly` + `IsCastableAt` refuse wrong-place spells before any MP is spent.

## Spell table (binary index order) [X category]

| Idx | Spell | Cat | Effect (confirmed message / manual) |
|---|---|---|---|
| 0–3 | HEALING I–IV | E | Restore HP (scales with level). |
| 4–7 | FIREFLASH I–IV | C/A | Fire damage to a monster group. |
| 8–11 | QUICKNESS I–IV | C/P | Speeds party (counters SLOWED). |
| 12–15 | STRENGTH I–IV | C/P | Raises party damage; cancels WEAKNESS. |
| 16–19 | PROTECTION I–IV | C/P | Raises party defense/AC. |
| 20–23 | CONFUSION I–IV | C/A | Confuses a monster group. |
| 24–27 | WEAKNESS I–IV | C/A | Lowers a group's damage ("ARMOR IS REDUCED"). |
| 28–31 | BINDING I–IV | C/A | Impedes movement → targets easier to hit; great vs. flyers. Progressive. |
| 32–35 | MINDBLAST I–IV | C/A | Psychic damage. |
| 36–39 | FLAMEBOLT I–IV | C/A | Fire damage (stronger line). |
| 40 | CHARM | C/A | "[original text]" — DOS: Σreaction +15, GREETINGS allowed again; fight goes on (s31). Remaster (D-022): ends the fight at once like GREETINGS. |
| 41 | SLEEP | C/A | "%s IS SLEPT" — per monster if `rand ≥ L²`; DOS: out for the rest of the round only (s31). Remaster (D-020): per-group CV roll, lasting asleep. |
| 42 | TELEPORTATION | C/A | `rand < 3·lvl + 50` → combat ends at the next round top; no gold/treasure, kill XP kept (s31). Remaster (D-023): always works, ends at once. |
| 43 | RESURRECTION | E | "%s IS RESURRECTED" — revive a dead member. |
| 44 | NINJA II | C/P | Caster: weapon hit eff 255, dmg eff 25, armor eff 10 (s31). |
| 45 | FEAR | C/A | Manual: monsters run. DOS: encounter level `L` −6, no message (s31). Remaster (D-021): `L` −6 then MONSTERS RUN (not the Black Lord). |
| 46 | DISSOLVE | C/A | `rand ≤ 110 − 10·L` → "[original text]", combat over, gold 0, XP + front group (living − 1)·XP (s31). |
| 47 | [original text] | C/P | No roll, no message, 4 MP always. Works if an elemental (FIRE/WATER/EARTH, `TWNS.INT` templates, Guild #23–25) was loaded into party slot 7 at the Guild (Add, only with 6 members, one at a time) and the party has 6 non-DEAD members (DOS: 5 if a monster already acted this round). Joins as member 7, no action that round, then player-controlled; no XP share; back to slot 7 at combat end; leaves at town entry / Drop / Purge / defeat. DOS never preps its combat fields (0 → can't hit) — possible manual conflict (s33, `combat-resolution.md` §[original text]). |
| 48 | DISPELL UNDEAD | C/A | Front group, if undead (DOS: XP mod 7 = 0; remaster: data list, D-024): dissolves (adds XP) or runs (s31). |
| 49 | NINJA I | C/P | Caster: weapon hit eff 200, dmg eff 20, armor eff 8 (s31). |
| 50 | AWAKEN | C/A | Every non-DEAD member → OK; "PARTY AWAKENS" (s31). |
| 51 | MONSTER EVAL. | N/WD | "[original text] n": wilderness `2·(T+1)` (max 12), dungeon `2·tier+2`; no roll (s41). |
| 52 | VISION | N/W | Un-fogs a square of radius `(MP left + 5)/4` around the party, current region only; dungeon: nothing (s41). |
| 53 | TRANSPORTATION | N/T | Fast-travel between towns. |
| 54 | DIVINE I | (endgame) | DOS slot 57 (`@210`), 2 MP, no effect anywhere (s41). Granted after the final boss ("learn to use it properly in Phantasie II"). |

> Note: the game's internal index (above) differs from the manual's printed
> numbering (1–53). The binary order is authoritative for `OpenPhantasie.Data`.

## Potion use (not spells, but same MP/HP pools) [C]

`OV2` `FUN_0000_43e2` (item-on-character applicator):

```
item = inventory_slot_id
if 101 <= item <= 110:          # HEALING 1..10
    heal = (item - 100) ** 2    # 1, 4, 9, …, 100
    hp_cur = min(hp_cur + heal, hp_max)
elif 111 <= item <= 120:        # MAGIC 1..10
    restore = 3 * (item - 110)  # 3, 6, 9, …, 30
    magic_cur = min(magic_cur + restore, magic_max)
else:
    # other item ids: transfer/clear slot paths
```

So **MAGIC n** restores **`3n` MP** (n = 1..10). **HEALING n** restores **`n²` HP**.

**The used slot is emptied in place [C] (s58 static).** The field path (OV2 `0x446b`) and the town USE ITEM path
(OV1 `0x31ac`) both do `mov word [bx+0x5c8e],0`: the potion's carried slot `@222 + 2k` becomes 0 and the later slots
do **not** move up. The gap stays until an Armory buy fills it (first empty slot, `town-menus.md` §Armory) or Distribute
compacts every slot. There is no potion order in combat, so these are the only two paths. Manual: silent.
**Core (s58):** `Magic.UsePotion` → `CarriedItems.ClearSlot` (slot := 0; trailing empty slots are dropped from the list,
which writes the same nine words).

## Inputs / outputs (for the cast routine)
- **Inputs**: caster (class, level, magic-power/INT, current MP), chosen spell
  index + level, target group, combat state.
- **Outputs**: MP deducted (`(index%4)+1`); effect applied (damage roll, status
  flag on party/monster group, HP restore, summon, map reveal, travel); combat
  message emitted.
- **Still open**: passive-cancel edge cases when monsters cast WEAKNESS/SLOW on the party; (DIVINE I in the combat chooser [D✓] s65.) (HEALING range [C] s18; specials [C] s31/s33; MONSTER EVAL./VISION/DIVINE I [C] s41.)

## Remaster note
Model spells data-driven: each spell = `{index, name, category, mpCost,
effect, tiers[]}` in `OpenPhantasie.Data` (`mpCost` is derived, not authored).
Passive/active + cancel semantics belong in `OpenPhantasie.Core` combat state,
testable headless.

## Cast text (s65 DOSBox, `capture_s65_magic.md`)

- Combat: every cast prints **"%s CASTS A SPELL"**; the only effect lines are HEALING "%s IS HEALED" (no amount), CHARM
  "[original text]", SLEEP "%s IS SLEPT", AWAKEN "PARTY AWAKENS", DISSOLVE "[original text]", RESURRECTION
  "%s IS RESURRECTED" and the damage lines "%s TAKES %d DAMAGE" / "%s DIES". QUICKNESS, STRENGTH, PROTECTION, CONFUSION,
  WEAKNESS, BINDING, FEAR, NINJA and an all-resisted SLEEP print nothing more (OV2 has no such strings).
- Town / field: HEALING and TRANSPORTATION print nothing. The field caster list hides DEAD members; the town list shows them.
