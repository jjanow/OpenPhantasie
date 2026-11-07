# Phantasie IV — skills: values, level-up training and where each skill is rolled (spec, 3d.6)

Source: X68000 `P4.X`, static reading (addresses = P4.X image addresses; float code read with the FPACK operand order of
`../FORMAT.md` §6). Written source: PC-98 manual pp. 20–21 (skills), p. 27 (training), p. 38 (traps, locks). Tags:
`[C]` code, `[~]` likely, `[?]` open. Not cross-checked on MSX2; nothing checked in an emulator, but §2 is checked
against the four recovered test save sets (`../unpacked/deleted-disk*/`). Combat use of Attack, Parry and Fire Bow:
`combat.md`. Outdoor use of Swim and Navigation: `outdoor.md`.

Record offsets: `../FORMAT.md` §6.2. Skill k (0–10, `_skill` order: Attack, Parry, Spot Item, Spot Trap, Disarm,
Listen, Fire Bow, Pick Lock, Swim, Navigation, Throwing) is the pair {value at +3C + 2k, bonus at +3D + 2k} `[C]`
(the offsets of every reader below fit this order). Party member m's record is `_pmb` + 190 m (`0x79708`).

## 1. Ability bonus per skill (`_calpow` `0xC320`) `[C]`

Recomputed from the current abilities at creation and at every level-up, before the skill values (§2):

| Skill | Bonus (byte +3D + 2k) |
|---|---|
| Attack | B(Str) |
| Parry | B(Dex) |
| Spot Item | B(Int) |
| Spot Trap | B(Int) |
| Disarm | B(Dex) |
| Listen | B(Int) |
| Fire Bow | v = Str + 4 × Dex + (15 if race = Elf); bonus = v − 50, or 0 if v < 50 |
| Pick Lock | B(Dex) |
| Swim | B(Con) |
| Navigation | 0 |
| Throwing | 6 × Dex − 84, or 0 if 6 × Dex < 84 |

B(a) = `_calbon(a)` = 0 for a < 10, else 2a − 20 (a = 18 → 16). The Elf +15 is the manual's "Elves +15 % with bows"
(p. 12) applied as a bonus to the Fire Bow skill.

## 2. Skill value: `_calsk(record, k, L)` (`0xBBB0`) `[C]`

```
L' = min(L, 14)                                   L = the level passed in (creation 0; level-up: level before it)
G  = TABL[0x1C2 + (k × 8 + class) × 15 + L']      (skill growth table, ../FORMAT.md §6.6)
v  = (old value + G) / 2 + bonus                  (float)
v  = max(v, 1.0)
new value = trunc(v + 0.5)                        (rounded; written to the byte, no working 255 cap — see note)
```

- The code also adds L − 14 for L ≥ 15, but L was already clamped to 14 a few instructions earlier, so that branch
  is dead `[C]`: above level 15 the growth target stays at the level-14 column.
- The 255 check is made on the byte after the conversion, so it cannot fire; a value above 255 would wrap `[~]`. With
  G ≤ 255 and the bonuses above, (old + G) / 2 + bonus can pass 255 only for an old value near 255 plus a large bonus.
- **Creation** (`_calpow(rec, 0)` from `_mkdetail`): every skill is computed once with old value 0, i.e.
  round(G[L0] / 2 + bonus), minimum 1. **Checked `[C]`:** this reproduces all eleven skills of all eight level-1
  characters in the recovered test saves exactly (`deleted-disk2/_uils.1`).
- **Level-up:** only the skills the player picks are recomputed (§3); the other skills keep their old values even
  though their bonuses were refreshed.
- Effect: a skill picked repeatedly moves halfway towards G + 2 × bonus each time, so its ceiling at a level is about
  G[L'] + 2 × bonus.

## 3. Training the skills at a level-up (`_sklup` `0xC0EE`, `_sklupmenu` `0xBF48`, `_sklpr` `0xBDF2`) `[C]`

`_calpow(rec, L)` with L ≥ 1 (called by guild training `_trang`, `training.md`) refreshes the bonuses (§1) and then
calls `_sklup`:

1. A "Training Menu" window lists the eleven skills (keys A–K) with "Now" (current value) and "After" (`_calsk`'s
   result if that skill is picked now).
2. Three times: "* You can train N skill(s)." (N = 3, 2, 1); the player presses a skill key; that skill is set to
   `_calsk(rec, k, L)`; its line is redrawn; "<skill> skill trained.". The **same skill may be picked again** (each pick
   recomputes from the new value) `[C]`.
3. "This training session is over." (今回の訓練は終了です).

There is no cancel and no gold cost here (the cost is paid in `_trang` before). `_bvs` maps the key to the button
index; a key outside the eleven buttons returns −1 `[~]` (not guarded; index −1 would write record +3A, current HP).

**Checked on save data `[C]`:** for the six characters that went from level 1 to 4 between test sets `_uils.1` and
`_uils.3`, every changed skill is reached by some sequence of picks at levels 1, 2, 3 with this formula, and the picks
add up to 9 = 3 × 3 level-ups for every character (the split per level is not unique).

Manual p. 27 / p. 20: "each time a character goes up a level he gets the chance to learn 3 skills", the same skill may
be chosen repeatedly, the screen shows before/after values — **matches** the code.

## 4. Where each skill is read

Party-wide rolls loop over members 0 … n−1; every member whose condition (+0A) is not 0 (Dead) rolls
`rand(100) ≤ skill` (rand returns 1..100, so the chance is `skill` %), and the **last** member in party order who
succeeds is the one named. Asleep members (condition 2) roll too `[C]`.

| Skill | Reader(s) | Rule |
|---|---|---|
| Attack (+3C) | `_set1` (`0x20634`) | copied into the combat hit value, `combat.md` §1 |
| Parry (+3E) | `_opt_get`, `_set1` | Parry action, `combat.md` §2 |
| **Spot Item (+40)** | **none** | no instruction in P4.X reads +40 (absolute `_pmb` / `_gmb` addresses or record-pointer displacements) `[C]`. Post-combat treasure (`_esub02` `0x24774`) uses no skill: §5 |
| Spot Trap (+42) | `_dtrap` (`0x1B604`), `_ctrap` (`0x1AD50`), `_cspi` (`0x1B1FC`) | party-wide roll: spots a floor trap / a chest trap; `_cspi` (a chest search, called by `_dchest(3)` from the event code) uses the same roll and prints "<name> found an item." |
| Disarm (+44) | `_dtrap`, `_ctrap` | one chosen member: rand(100) ≤ Disarm |
| Listen (+46) | `_listen` (`0x18FE4`, event opcode 0x2F), `_oencchk` (`0x1668C`) | party-wide roll "<name> sensed monsters nearby." (dungeon events); outdoors see `outdoor.md` §5 |
| Fire Bow (+48) | `_set1` (missile hit Hr), `_pmagic1` (`0x2C8FA`: Arrow Flame sets Hr = Fire Bow + 50 / + 100, cap 255) | `combat.md` §1, §3 |
| Pick Lock (+4A) | `_try_ul` (`0x1BB14`), `_dchest(1)` (`0x1B33E`) | party-wide roll; doors of wall type 3 never open |
| Swim (+4C) | `_dswim` (`0x1AC54`), `_owater` (`0x1514E`) | dungeon water §4.4; outdoors `outdoor.md` §3 |
| Navigation (+4E) | `_oship_mark` (`0x14478`), `_owater` | the best navigator steers the ship, `outdoor.md` §4 |
| **Throwing (+50)** | **none** | no reader `[C]`; thrown weapons use the Fire Bow value (`combat.md` §3.1). (`combat.md` §5 named `_pmagic1` as the one reader of +50; the `_pmagic1` access is +48, Fire Bow.) |

### 4.1 Locked doors (`_dmove` `0x1AAC2` → `_try_ul`) `[C]`

Each wall side of a dungeon cell is a nibble of `_dinmap`; 2 = locked door, 3 = door that never opens. Moving into
either calls `_try_ul(type)`: a party-wide Pick Lock roll; if someone succeeds **and** type ≠ 3: "<name> forced the
lock." and the party moves through. Otherwise "The lock won't open." and the party stays. The roll is made again on
every attempt (no memory of a failure) `[~]`. Matches manual p. 21 / p. 38 (some doors never open; find a key).

### 4.2 Floor traps (`_dtrap`, event opcode 0x30) and chest traps (`_ctrap`) `[C]`

1. Party-wide Spot Trap roll. **Nobody succeeds:** the trap goes off (step 3).
2. **Spotted:** "<name> found a trap (on the chest)." Menu "Disarm the trap / Give up". Give up → nothing happens (a
   trapped chest stays closed). Disarm → "Who disarms the trap?", the player picks one member; if that member is not
   dead and rand(100) ≤ Disarm: "<name> succeeded." (no damage); else "<name> failed." and the trap goes off.
3. **Trap goes off:** "A trap!" Then for every member: if condition = 1 (Okay) and rand(100) < record **+35**:
   "<name> dodged it."; else damage = rand(5) × (trunc(`_cmapn` / 5) + 1) (`_cmapn` = dungeon number), "<name> took
   N damage." via `_ddamage` (HP − N; HP ≤ N → dead; all dead → game over).

Record +35 is the combat defence value set by `_set1` at the start of every fight (`combat.md`) `[C]` (the value
persists in the record: 0 for characters that never fought, 22–50 in the test saves); so a character who has not yet
fought can never dodge `[~]`. Manual p. 38: "if an unskilled member tries [to disarm], everyone is hurt" — matches;
"only one person can try at a time" (p. 21) — matches.

### 4.3 Chests (`_dchest(kind)` `0x1B33E`, from the event code) `[C]`

If the cell is already marked opened (`_pinmap` low 6 bits = 6) nothing happens. Otherwise "There is a chest." and by
`kind`: 0 = opens; 1 = locked: party-wide Pick Lock roll ("forced the lock" / "The lock won't open."); 2 = trapped:
`_ctrap` (§4.2); 3 = `_cspi` (Spot Trap roll, "found an item"). On success the cell is marked opened (kind 3 does not
mark it). What the chest then gives is decided by the event code `[?]` (3d.8).

### 4.4 Dungeon water (`_dmain` `0x17C56` → `_dswim`) `[C]`

On entering a cell whose `_pinmap` type is 0x1B–0x1E (water), unless the Levitation flag (`_par`+15) is set: for every
member, damage = rand(level + 1) + 1 (2 … level + 2); if the member is not dead and rand(100) > Swim, `_ddamage(m,
damage)`. The flag is set by casting Levitation (spell 13) outside combat (`_pmagic3` `0x2D3EC`: "The party floated up
into the air.") and cleared at the end of every fight (`_encount` `0x227B0`).

## 5. Treasure after a fight (`_esub02` `0x24774`) — no skill `[C]`

Map kind first: `_cmapf` is 0 town, **1 outdoors, 2 dungeon** (`_game` `0x27E`: 1 → `_outdoor`, 2 → `_dungeon`;
`_omove` sets 1 when walking off an outdoor map, `_odungeon` sets 2) `[C]`. (`../FORMAT.md` §6.7 and §6.4 read 1 as
dungeon; that is wrong: the monster treasure ids of `DATM` +3A/3C/3E are loaded by `_enc_mons` only when `_cmapf` = 1,
i.e. **outdoors**.)

```
t = 45 (dungeon); outdoors (_cmapf = 1): t = _emlevel + 18      (_emlevel = sum of the monster groups' levels)
if rand(100) ≤ t:
    offer the monsters' item ids 1–3 (`_findit`: "take it?" if the pack has room; ids 0 / > 156 skipped)
    if an accumulator of those ids ≠ 0 (see below):
        dungeon (2): only if rand(100) < 65;  outdoors (1): always
        offer one random item: list 0x443CA[rand(2 × _emlevel + 6)], index > 30 → entry 0
```

The accumulator (local −8(a6)) is never initialised before the three ids are added (original bug) `[C]`, so whether
the extra random item is offered depends on stack garbage `[~]`. Spot Item plays no part.

## 6. Against the manual (item 1 → D4-036, item 2 → D4-037; decided: manual, amounts user s84 — see `DIFFERENCES-IV.md`)

1. **Spot Item** (manual p. 21, 「C Spot（アイテムの発見）戦闘の後、落ちているアイテムを見つけ出す能力です。…またこの技能が高いと、
   [original text]の性質まで見抜けるので、役に立つ物かどうかを判別することも可能です。」 — "the ability to find items lying
   around after combat … a high value also reveals the nature of found items"; p. 45 "if a member has a high skill at
   finding treasure, then after a fight in a dungeon …", p. 44): the code never reads Spot Item; post-combat treasure is a flat
   chance (§5) and no item identification uses it. Manual p. 44 also says treasure comes automatically when the enemy
   is wiped out outdoors (「アウトドアで敵を全滅させた時、…相手が財宝を持っていれば、自動的に手に入ります。」); the code rolls
   _emlevel + 18 % outdoors and 45 % in dungeons, and loads the monsters' own treasure ids only outdoors.
2. **Throwing** (manual p. 21: a separate skill, "practically the same as Fire Bow"): the code never reads it; thrown
   weapons use Fire Bow (`combat.md` §3.1).
3. Pick Lock, Spot Trap, Disarm, the three level-up picks: match.

## 7. Open `[?]`

- Who calls `_dtrap` / `_dchest` / `_listen` and with what arguments: event bytecode (3d.8).
- The pick key outside A–K (`_bvs` −1) in a real game.
- MSX2 cross-check of §1–§3; an emulator check of one level-up.
