# Phantasie II (Atari ST): encounters and the monster-group builder — logic spec (3b.7)

Phase 09 T3, task 3b.7, row "Monsters / encounters" (`docs/plan/03b-phantasie2-re.md`). Only II's deltas are written out.
Everything else points at Phantasie I's specs:

- `reverse-engineering/dos/logic-specs/combat-resolution.md` §Monster record & encounter build and §Surprise;
- `overworld-movement.md` §Encounters, §Night encounter and §Day encounter;
- `dungeon-representation.md` §Fight cells and §Locks.

Tags: `[C]` read in `PHANT.PRG` (static, image addresses, `FORMAT.md` §1), `[~]` probable, `[?]` open, **(live s82)** seen in
Hatari (`../parity/encounters-endgame-live.md`: DNG2 wandering cells, averaged L / reaction, FEAR, the night watch at
r = 100, the minion swap). The Guild fill (§8) was seen live in s80. Each section names the Amiga I `game` function it was
compared with (`hN:0xOFF`, `00-function-map.md`).

Conventions:

- `r` = `rnd100 0x0fd40` = `rand() / 327`, so **r is 0..100 (101 values)**, not DOS's 0..99 `[C]`.
  - `rand 0x1ad22` returns 0..32767, so r = 100 comes up for rand ≥ 32700, about 0.2 %.
  - Every range that DOS gives for `rand0_99` gains one rare top value on the ST. These are listed where they matter.
  - Core needs a 0..100 source for II.
- Monster ids are **0-based** unless marked 1-based. The game passes the first monster 1-based in `0x2832c` (DOS `[0x64e6]`).
- `T` = the terrain digit (tile % 10, `0x25f30`) on the overworld, and the level's tier byte in a dungeon (§5).
- `F` = `0x25cdc`, `H` = `0x25e9a` (DOS `[0x649e]` / `[0x64a0]`, `combat-resolution.md` §Surprise).
- Monster tables, 16 B per group (`16g + 2i`): HP `0x25bfa` (DOS `0x74c4`) and **state `0x25e6a`** (DOS `0x64b0`: 1
  asleep, 2 done this round, 3 ready) `[C]` s95 static. Build `0xac38` := 3 − H; acting (`0x8a34`, `0x8d7c`), a kill
  (`0x9ac2`), SLEEP (`0x9138`), waking (`0x8e34`, `r < 15` on state 1) and a failed monster run (`0xa0ec`) write 2;
  the round-top compaction `compact_monsters 0xb16e` with `0x25f3a` = 1 makes every state > 1 a 3 and keeps 1. As DOS,
  so SLEEP lasts the round only and a failed run wakes surprise sleepers (`DIFFERENCES-II.md` D2-045: D-020, D-030).

## 1. Summary of II's changes `[C]`

| # | Rule | Phantasie I (DOS) | Phantasie II (ST) |
|---|---|---|---|
| 1 | Encounter level `L` and the reaction sum | Σ over the built groups | **Σ ÷ group count**, truncated (Amiga I does the same, so this is a 68000-version trait, not new in II) |
| 2 | Dungeon wandering monster | `r/33 + b + T − 8` (T once) | **`r/33 + 12·T + b − 8`** |
| 3 | DNG6 TROLKIN override (wandering, r > 50) | yes | gone |
| 4 | Black Knights in the day encounter (r > 91) | yes | **gone**: every day encounter goes on to the monster pick |
| 5 | BLACK KNIGHT size factor 4 in DNG9 | yes | gone |
| 6 | Group fit test (Σ size + 3 < 135) | sprite table word 0 (`sprite_width`) | the sprite **height** (the size table's word 1) |
| 7 | Random range | `rand0_99` | 0..100 (see Conventions) |
| — | Pluto's minions | — | new: `encounter_start` swaps a minion already killed for a plain monster (`endgame.md` §3.1) |

Everything else in the builder and the encounter rolls is I's rule, unchanged: the counts, the HP roll, the companion
pick, the next-group roll, treasure, gold and the wake state. So are the night watch, the sleeping-monster and listener
menus, the surprise flags, the lock ambush, the forced-fight cells and the 6 % roll. §2–§6 give the addresses.

## 2. Encounter start (`encounter_start 0x09bf4` ↔ I `h44:0x0000`) `[C]`

Main-loop mode 11. In order:

1. **Wandering chance.** `0x1fefc` −1; if the result is below 5 it becomes 1 (`0x9c1e..0x9c38`). It is set to 7 on
   every dungeon entry (`0x106d2`) and raised by 1 by a successful BEG (`0x9e4a`). This is I's `[0x1a5a]`, and the
   sequence is 7 → 6 → 5 → 1 → 1 … as in I (live s82: 7 on DNG2 entry, −1 per encounter start, also on the overworld;
   the minion swap's restart takes it 7 → 5).
2. Clears `0x25b1e` (XP pool) and `0x24ebc` (gold pool) and sets the build flags (`0x25b1a`, `0x25f3a`, `0x25f9a` = 1).
3. `build_monster_groups(0x2832c)` (§3).
4. **Minion check (new).** If group 0's reaction modifier (+0x2a) is below −10, k = −10 − modifier. If any party member
   has `@286 + 2(k − 1)` = 1, the game prints "AN ENCOUNTER" and sets the id to 1-based ⌊r·36/100⌋. It sets F, H := 1, 1
   and returns 11, so the fight is rebuilt. Otherwise it prints "[original text]". Full rules: `endgame.md` §3.1.
   - When r < 3 the id is 1-based 0. The builder turns it into 0-based 0, SKELETON (§3 step 1) `[C]` (live s82: three
     SKELETON groups after a killed UNDEAD SNAKE).
5. `0xb16e` → `0x25b1c`; party-member prep `0xafb6` when in a dungeon (location 3); all 7 party action words := 4.
6. Returns 5 when F = 0 or H = 0 (the opening round of §Surprise), else calls `0xfd82` (draws the groups) and returns 4.

## 3. The builder (`build_monster_groups 0x0a72e` ↔ I `h44:0x0998`, DOS OV2 `0x34aa`) `[C]`

Same steps and the same RNG draw order as `combat-resolution.md` §Monster record & encounter build. The record is the
58-byte one (`FORMAT.md` §4); the group copies are at `0x25d9a + 58·g`.

```
m := first id (1-based) − 1;  if m < 0 or m > 79: m := 0                     # 0xa742..0xa756
clear L 0x25b86, reaction sum 0x25c2a, per-group count / state arrays;  party slot 6 @24 := 1   # 0xa7ba..0xa7e0
fit := 0
for g = 0, 1, 2:
    copy record m → group g, field by field                                   # 0xa7e6..0xaade
        (+0x35 := size table 0x2112e + 4m + 2 = sprite HEIGHT, not +0x35 of the record)   # 0xaa50..0xaa70
    if g == 0: treasure := record +0x36, +0x37  (0x25f32 / 0x25f38)          # 0xaade, before the fit test
    fit += height + 3;  room := fit < 135                                     # 0xab26..0xab54
    if room:
        n := r·f / 102 (f = +0x34);  if count[0] < 0: count[0] := 0;  n := min(n, 5)   # 0xab5c..0xabc4 (group-0 quirk kept)
        0x25b1c += n + 1
        per monster (n + 1): HP := ((r + 50)·hp_base + 50) / 100;  state := 3 − H;  gold pool += +0x30   # 0xabe2..0xac74
        L += level (+0x18);  reaction += +0x2a;  group id[g] := m                # 0xac88..0xacc8
        m := r < 50 ? +0x29 : +0x28                                            # 0xacca..0xad08 (companions, 0-based)
    if room and r < next-group chance (+0x2c) and g < 2: next g, else stop    # 0xad0a..0xad36 (roll drawn before the g test)
groups := room and g < 3 ? g + 1 : g                                          # 0xad42..0xad62  → 0x24c92
L := L / groups;  reaction := reaction / groups   (signed, truncated)         # 0xad68..0xad8a  ← new vs DOS
```

- **The BLACK KNIGHT rule is gone.** The `+0x34` copy at `0xaa24` is unconditional. I's `m == 76` and DNG9 test
  (Amiga `h44:0x0c44`) has no counterpart.
- **The group fit uses the sprite height** `[C]`.
  - The size table at `0x2112e` holds (u16 width, u16 height) per monster. The `MSTR*.PAT` offsets prove the order:
    offset[i+1] = offset[i] + ⌈w/16⌉·8·h, SKELETON 32 × 34 → 544 B (`FORMAT.md` §10.3).
  - The builder copies word 1 (the height) into the group's +0x35. The fit test reads +0x35 (`0xab36`).
  - Group sprites are stacked vertically on the ST combat screen. Combat draws each group with (w, h) through `0xfd82` → `0x18288`.
  - Amiga I does the same (`h44:0x0d4c`, +0x35 + 3 < 135).
  - **Note `[~]`:** DOS I's table (`GameData/phantasie1/monsters.json` `sprite_width` 34 / `sprite_height` 32 for
    SKELETON) holds the same number in its first word, so I's labels may be swapped. Phantasie I's data is left as it is.
- **Fit in practice.** Most sprites are 21–39 high: three groups always fit, since 3 × (39 + 3) = 126.
  - An 80-high sprite takes 83, so after it only a group of height ≤ 48 still fits (83 + 51 = 134).
  - Two 80-high groups never fit: the second is dropped, with no count, gold, L or companion.
  - 80-high sprites: ids 42, 68, 72 and 74–79. DRAGON KING (66) is 79 high, STORM GIANT (67) 77 and ORANGE DRAGON (54) 73.
- **`L` and the reaction sum are averaged** `[C]` (live s82: six builds read from RAM, e.g. GOBLIN 4 / −5 + KOBOLD 1 / 0 ×2
  → L 2, reaction −1; the division truncates toward zero, −7/3 = −2).
  - Every combat reader of `0x25b86` / `0x25c2a` sees the averaged values. Checked: FEAR's roll and its `L −= 6`
    (`0x8326..0x8354`), DISSOLVE's `r ≤ 110 − 10·L` (`0x835e`), and BEG's `r < reaction + 90` (`0x9e34`). The other
    readers are `0x823c`, `0x8962`, `0x90be`, `0x9ede`, `0x9f4a`, `0xa58a` and `0xa684`; they hold I's turn order, morale,
    THREATEN and GREET (`combat-resolution.md` §Turn order, §Party options) and were not traced one by one `[~]`.
  - The MONSTER EVAL field estimate (`0x7066`) writes `0x25b86` outside combat. As in DOS, the builder overwrites it.
  - One group: same as DOS. Two or three groups: L is the mean group level, not the sum. A three-group fight is
    therefore much "lower level" on the ST than in DOS.
  - The Amiga I builder ends with the same two divisions (`h44:0x0fbc..0x0fd6`), so this is not an II change. Phantasie I
    in the remaster follows DOS (Σ).
- With r up to 100: HP can reach (150·hp_base + 50)/100, and n = r·f/102 reaches ⌊100f/102⌋ (f = 6 → 5, unchanged cap).

**Id check `0x186de` ("Wrong monster") `[~]`.** It looks up a sprite slot in combat drawing (`0x18856`, table `0x25c3c`)
and alerts when the slot is empty. It is not part of the rules. Amiga I's builder carries the same alert text inline.

## 4. Overworld (`night_watch_encounter 0xfe38` ↔ I `h74:0x0000`, DOS OV2 `0x70ed`) `[C]`

**Step roll** (`0x6464`): a step on a random-encounter tile fights on **r < 6**, i.e. 6/101 ≈ 5.9 % (DOS 6 %). Lava, water,
towns, dungeons, text cells and the class-7 / 11 fight terrain return before the roll (`FORMAT.md` §9.3, `terrain.md`).

**Night** (r < 35, 35/101). As DOS §Night encounter:
- offset r′ := ⌊r·5/100⌋, redrawn while r′ is 2 or 3. So r′ ∈ {0, 1, 4} and **5 when r = 100**. Monster = 12·T + r′.
- Watch slot w = ⌊r·party/100⌋. A non-DEAD watcher hears the monsters on r < LISTEN `@40`. Otherwise the party sleeps
  (OK → SLEEP) and the watcher wakes up.
- **r = 100** gives w = party size, the record past the last member (live s82, roll forced with a breakpoint). With six
  members that is record 6, which `build_monster_groups` sets to status 1 at every build (`0xa7ba`); it has no name and
  LISTEN 0. So after the run's first fight: nobody hears, all six sleep, and the box shows **" IS ON WATCH"** with a blank
  name. Before any fight of the run (record 6 status 0) no line `[~]`. **Fewer members** (live s83, three, before the
  run's first fight): the record past them has status 0 (DEAD), so no listen roll, no "IS ON WATCH" line, and every OK
  member sleeps. That record always reads DEAD: the records past a party are zero when it loads from the Guild, and the
  defeat compaction `0xc14a` clears the status (and `@92`) of every record it vacates `[C]`.
- **With a Guild elemental** (s92, static `[C]`, not seen live): outside a fight the party size stays 6 and the elemental
  is record 6; only the fight start makes it 7, when record 6 holds one (`0x83e2`: size 6, `0x258ee` (record 6's name
  word) and `0x25932` (record 6 + 68) both non-zero → size 7; `0xa298` sets it back to 6). So the night watch never sees
  a size of 7, and r = 100 gives w = 6: **the elemental itself is on watch**, with its status, its LISTEN (the listen roll
  is drawn when it is not DEAD) and its name ("AIR IS ON WATCH [[original text]]"); the six members sleep when it
  does not hear. (Record 7, `0x25a14`, is the VDI `ptsout` array, whose word 12, the "status", no VDI call of the game
  writes, so it would read DEAD; it is never reached.) Core: `NightEncounter` takes `Party.Elemental` as the slot-6
  watcher.
- F, H := 1, 1. Strings "[original text]", "%s IS ON WATCH", "[original text]".

**Day.** The **Black Knights branch is gone**. The DOS `r > 91` test has no counterpart: `0xfffa` goes straight to the
monster pick. The menu (`OUT.RSC` tree 7, "[original text] / RUN / FIGHT / SURRENDER") is still in the resource file,
but no code opens it.
```
if r < 25:  m := ⌊r′/10⌋ + 12·T + 2                    # new draw r′; offsets 2..11, and 12 (= next block) when r′ = 100
else:       coin := ⌊2r/100⌋;  b := ⌊r/25⌋              # coin 0..1 (2 at 100), b 0..3 (4 at 100), two draws
            m := coin + 2·PAIR[C + b] + 12·T            # PAIR = data 0x1fefe, 16 B = I's table (4,1,2,3,3,4,2,4,3,5,5,4,4,0,0,1)
if r < 31:  "YOU SPOT SLEEPING MONSTERS !"  ATTACK → F,H := 0,2, fight m;  LEAVE → no fight
listener := last member with ⌊222r/100⌋ ≤ LISTEN @40 and not DEAD
listener:   "%s HEARS MONSTERS";  r < 6T + 20 → F,H := 1,1, fight
            else menu RUN / FIGHT:  FIGHT → r < 30 ? F,H := 1,1 : ("[original text]", F,H := 0,2);  RUN → r < 80 ? none : F,H := 1,1
none:       r ≥ 6T + 20 → F,H := 1,1;  else q := r:  q ≥ 50 → "[original text]", F,H := 1,0;
            q < 50 → falls off the end, returns q: fight 1-based q (0-based q − 1, none for q = 0), flags unchanged
```
- This is I's code apart from the Black Knights (`overworld-movement.md` §Day encounter), and the fall-off quirk is kept.
- **Menu answers checked** `[C]`.
  - `0x171dc(tree, base, cancel)` → `0x16530` returns −1 for the cancel object, else object − base.
  - Tree 8 (ATTACK = object 2, LEAVE = 3): called with (8, 2, 2), so LEAVE → 1 → no fight.
  - Tree 10 (RUN = 1, FIGHT = 2): called with (10, 2, 1), so FIGHT → 0 and RUN → −1.
- **Which monsters.** The overworld random roll only runs on tiles of classes 0–3, 6 and 9 (class 4 has no random roll).
  - On the 17 `MAPS.INT` grids those tiles have T = 0–4 only (tile census of the initial grids), so random fights draw from ids 0–59.
  - With the r = 100 edges the maximum is 60 (12·4 + 12, or 2 + 2·5 + 48).
  - **So beasts V / VI (74, 75) never come from random encounters**: they need DIVINE II (`magic-divine.md` §2).
  - Minion 1 (17, PLUTO'S GIANT CONST.) comes from T = 1 tiles: offset 5, i.e. the r < 25 branch with r′ 30–39, or
    PAIR = 2 with coin 1.
  - Minion 2 (43, PLUTO'S WINGED DEVIL) comes from T = 3 tiles (offset 7) and from DNG4 wandering cells (§5).
- **Fixed fights.**
  - Feature T = 7 fights the feature arg (0-based; `0x62de`, as DOS).
  - The class-7 / 11 fight terrain gives 12·T + ⌊r/34⌋ (0..2) on r ≤ 8, once per trip (`terrain.md` §3).
  - DIVINE II: `magic-divine.md` §2.

## 5. Dungeon (`0x10b60` ↔ I `h41:0x07f8`, DOS OV2 `0x2249`) `[C]`

`T` = `0x2833a` = level byte `0x4e7`, read on level load (`0x1076a`), the DOS tier byte.

```
cells 0x08..0x11 (b), wandering:                                     # 0x10b64
    if r ≤ chance 0x1fefc:  F,H := 1,1;  m := ⌊r/33⌋ + 12·T + b − 8;  return 11      # 0x10bb4..0x10be8
    else: message b + 3 (as DOS)
cells 0x80..0x89, forced:  m := buf[0x468 + b] (0-based);  F,H := 1,1;  return 11    # 0x10d26..0x10d66, as DOS
cells 0xb9..0xd6, locks / ambush:  as DOS, ambush m := 12·T + di − 2                # door_lock 0x111d6, 0x11340
```
- **12·T instead of T.** DOS and Amiga I (`h41:0x0866..0x0886`) add the tier once. In I, every level's wandering cells
  therefore drew from the first block (ids 0–21). II multiplies by 12, like the lock ambush.
  - II's data fits this: each level's wandering range sits next to its forced-fight table (table below). DNG8's cells
    give HIGH DEMON … KILLER CRUD, where T once would give DRIP SLIME.
  - No DNG6 TROLKIN override (I: `[0x64ec] == 6 and r > 50`); II has TROLKIN at id 33.
- ⌊r/33⌋ is 0–2, and 3 for r = 99 or 100 (2/101). Chance: fight on r ≤ chance, (chance + 1)/101 (7 → 7.9 %).
- The forced and lock-ambush paths are I's rules (lock: `r ≤ 50` sets H, `r > 50 and listener` sets F = 0; menu "HEARS
  MONSTERS. GO BACK ?").
- **Script fights** (`dungeon_event 0x11ab6`):
  - The op at `0x11d1c` fights arg + 1 with H := (r ≤ 90), F := 1. This is DOS op 5.
  - The op at `0x11ecc` prints "MONSTERS APPEAR" and fights arg + 1 with F, H := 1, 0. This is DOS op 17.
  - Arg 0 (1-based ≤ 1) gives no fight on both. The rest of the interpreter: `endgame.md` §5.

Wandering cells per level (initial `DNG1–8`, 0-based ids; "+n" is the r ≥ 99 id):

| Level | T | Cells b → ids |
|---|---|---|
| DNG1 | 0 | 10 → 2–4 (+5) DRIP SLIME…GIANT ANT; 15 → 7–9 (+10) WILD DOG…KOBOLD; 16 → 8–10 (+11) ORC…RANGER |
| DNG2 | 1 | 14 → 18–20 (+21) PANTHER…GOBLIN; 15 → 19–21 (+22); 16 → 20–22 (+23) GOBLIN…ILLUSIONIST |
| DNG3 | 2 | 16 → 32–34 (+35) GREMLIN…IMPLING; 17 → 33–35 (+36) TROLKIN…PRIEST |
| DNG4 | 3 | 13 → 41–43 (+44) ASP…PLUTO'S WINGED DEVIL; 15 → 43–45 (+46); 17 → 45–47 (+48) DARK DWARF…LORD |
| DNG5 | 4 | 8 → 48–50 DEMON…GHOST; 11 → 51–53; 15 → 55–57; 16 → 56–58; 17 → 57–59 (+60) |
| DNG6 | 4 | 10 → 50–52 (+53) GHOST…KING COBRA; 17 → 57–59 (+60) IMP…HIGH RANGER |
| DNG7 | 5 | 9 → 61–63 (+64) HIGH DEVIL…VORPAL BEETLE; 11 → 63–65 (+66); 16 → 68–70 (+71) CLOUD GIANT…WIZARD |
| DNG8 | 5 | 8 → 60–62 (+63) HIGH DEMON…KILLER CRUD |

**Live s82 (DNG2):** 26 sampled fights (chance poked to 100): b = 14 → 18 ×6, 19 ×2, 20; b = 15 → 19, 20 ×5, 21 ×3;
b = 16 → 20 ×4, 21, 22 ×2 and once 23 (the r ≥ 99 id). Every id fits `⌊r/33⌋ + 12·T + b − 8`.

PLUTO'S WINGED DEVIL (43, minion 2) is a regular wandering monster on DNG4 cells 13 and 15. Its group's reaction
modifier makes it a minion fight (`endgame.md` §3.1).

## 6. Monster traits in combat `[C]`

- **DISPELL UNDEAD** (slot 48, combat case `0x842c`; spell switch `0x8560`, table `0x212a2` indexed by spell − 37) tests
  group 0 only: "undead" ⇔ ⌊XP / 7⌋·7 = XP (`0x843c..0x847a`). This is DOS I's rule (D-024).
  - In II it hits SKELETON, BANTIR, ZOMBIE, GHOUL, LARGE SKELETON, WIGHT, WRAITH, DEMON, VAMPIRE, GHOST, HIGH DEMON and
    SPIRIT (ids 0, 1, 13, 14, 24, 25, 37, 48, 49, 50, 60, 64). That is I's list.
  - It misses PLUTO'S UNDEAD SNAKE (72, XP not a multiple of 7).
  - Manual vs code: `DIFFERENCES-II.md` D2-022.
- **FEAR** (slot 45, case `0x8326`): on r < 5·(caster level − L) + 50, L −= 6. No monster runs. This is DOS's rule
  (D-021); L is the averaged one (§3). `DIFFERENCES-II.md` D2-023. Live s82: on PANTHER ×2 L 3 → −3, on GOBLIN + KOBOLD ×2
  L 2 → −4, no monster ran (caster level 15, so no success rate could be measured).
- The case mapping is checked through the neighbouring cases: `0x835e` = DISSOLVE (46), `0x83e2` = [original text]
  (47, party size 6 → 7), `0x84fc` = NINJA I (49: quickness 200, damage 20, protection 8).
- **Which code reads which record field** (group copies at `0x25d9a + 58·g`; a sweep of every `0x25d9a`-based
  operand) `[C]` for the readers, `[~]` for the formulas:

  | Field | Readers besides the builder |
  |---|---|
  | +0x14 armor | `combat_round 0x07610`, `monster_spell_sleep 0x08f0a`, `monster_breath_special 0x092f8` |
  | +0x16 damage | `0x07610`, `0x092f8` |
  | +0x18 level | `0x07610` |
  | +0x1a XP | `0x07610`, `monster_takes_damage 0x099e0` (XP per kill) |
  | +0x1e special gate, +0x20 specials | `0x07610`, `0x08f0a` |
  | +0x26 CV | `0x08f0a`, `0x095e8` |
  | +0x2a reaction | `encounter_start 0x09bf4` (minion test) |
  | +0x2e defense | `0x07610`, `0x08f0a` |
  | +0x38 attack power | `0x07610`, `0x08f0a`, `0x092f8` |

  - These are the same readers DOS has (`combat-resolution.md` §To-hit, §Damage, §Monster specials).
  - `monster_takes_damage` is unchanged from Amiga I (`00-function-map.md`).
  - The formulas inside `0x07610`, `0x08f0a` and `0x092f8` (all "changed" in the function map) were **not** diffed
    against I here. Except for the two spells above, the combat use of the record stays `[~]` (carried over from DOS).
- No never-flees or undead table exists in `PHANT.PRG`. The remaster's `monster-traits.json` for I is decision data
  (D-021 / D-024), so II gets one only after D2-022 / D2-023 are decided.

## 7. Data produced

`GameData/phantasie2/monsters.json` (`build_p2_gamedata.py`) now carries `sprite_width` / `sprite_height` from
`extracted/graphics/extraction-index.json` (= table `0x2112e`, live-checked sprites, `FORMAT.md` §10.3). OpenPhantasie.Data
loads `sprite_height` as `MonsterDefinition.SpriteHeight`. A Core II builder must fit groups on `SpriteHeight`, not
`SpriteWidth` (§3).

## 8. Rules-profile values read from the code `[C]` static

These fill the `_open` list of `GameData/phantasie2/rules.json` and the `_lifespan` note of `creation-tables.json`. Only
the Guild fill was run live (below).

- **Starting gold**: `create_character 0x0cc2a` writes 256 to the bank `@262`. The manual agrees (256 GP).
- **Gold cap**: 65,535 on the ST (manual: 65,000, D2-019).
  - Clamp function `0x0dfa4` = min(x, 65535).
  - Party gold `0x25eb4` gains only through: the combat victory (`0x0a43e..0x0a468`, gold and XP pools clamped);
    a bank withdrawal (`0x0df74`); an item sale, value²/8 (`0x0eb44`); and the dungeon gold op (`0x11d98`). The gold op
    is gold + arg·256, clamped, then OR 0xff, which is I's op 7/8 (`DungeonScript` case 7/8).
  - The bank `@262` gains only on town entry, when found gold is split (`0x0bef8`, clamped). There is no deposit: every
    other writer subtracts or clears (`0x0df56` withdraw, `0x12884` keeps the low byte on party defeat).
  - The gold entry box (`0x01272`) refuses typed amounts over 65,535.
- **Guild fill**: 22 (manual: 21 C64 / 37 Apple, D2-020).
  - `guild_menu 0x00352` disables "New member" (`PHANT.RSC` tree 2, object 19) unless `0x0ebf6` finds a slot of 22
    with `@92` = 0. The menu helper is `0x02308(tree, item, disable)` → `0x190a6` with state 8 (DISABLED).
  - `create_character 0x0c93a` takes the first such slot. So N)EW fills all 22.
  - **Live (Hatari + EmuTOS, s80):** a roster with 22 occupied slots (`parity/make_p2_full_guild.py 22`) shows "New
    member" greyed in the Guild options menu (`parity/shots/p2-guild-menu-22-full.png`). With 21 occupied it is
    enabled (`p2-guild-menu-21.png`). Method: `boot_p2.sh <dir> continue`, Guild door (305,160), hover the "Guild
    options" title.
- **Lifespans** (DOS I's rule; manual: D2-021):
  - `create_character 0x0cc84`: `@10` = rand/5461 + 8, which is 8..13, and 14 when rand ≥ 32766.
  - Then `@252` = `@10` × factor through the jump table `0x22976`, by race index: 0 HUMAN ×1, 1 DWARF ×5/2, 2 ELF ×19,
    3 HALFLING ×5/2, 4 GNOME ×5/2, 5 PIXIE ×1, any other ×2/5 (UNDEAD included).
  - `town_aging 0x0bba6`: for a non-DEAD member whose `@264 >> 8` is above 7/10 of `@252`:
    - if it is above `@252`: "has died", status DEAD and stats ×0;
    - else, once (`@258`): "has aged", stats ×4/5.
  - As DOS (`save-file-format.md` @252/@258).
- **Divine spell costs**: `magic-divine.md` §1, (slot % 4) + 1: DIVINE II 1 MP, DIVINE I 2 MP.

## 9. Open

- Answered live (s82, `../parity/encounters-endgame-live.md`): the DNG2 wandering ids (§5), averaged L and reaction (§3),
  FEAR on a multi-group fight (§6) and the night watch at r = 100 (§4).
- Answered s83 (live + static): the r = 100 watcher for parties of fewer than six (no line, everyone sleeps).
- `[~]` The r = 100 watcher before the run's first fight with six members (§4). With the elemental: decoded s92 (§4),
  not seen live.
- `[~]` The other readers of the averaged L / reaction (turn order, morale, THREATEN, GREET) were not traced one by one (§3).

## 10. Core (s81)

- `GameData/phantasie2/rules.json` `encounters` (`EncounterRules`, `RulesProfile.Encounters`; null = I's DOS rules):
  `rand_327` rolls, `average` L / reaction, `sprite_height` fit, `dungeon_tier_factor` 12, no day Black Knights roll (the
  draw is gone, not just the branch), no wandering override, first id clamped to 0..79. Read by `EncounterBuilder.Build`,
  `NightEncounter.TryRoll`, `WildernessEncounter.RollDay` / `Answer` and `DungeonEncounters.RollWandering` (through
  `DungeonTraversal.Encounters`). `wilderness-encounters.json` holds the PAIR table `0x1fefe`.
- Night watch at r = 100 (§4, live s82): nobody hears and everyone sleeps; with six members `NightEncounter.ApplyAfterPrep`
  prints the blank-name " IS ON WATCH" line, other sizes print nothing (s82; fewer than six live s83).
- Minion swap (§2 step 4) and the amulet: `Phantasie2Campaign.ReplaceEncounter` / `AfterCombatSurvived` (group 0 found by
  `RankIndex`, since the round-end re-rank reorders the groups). The swap rebuilds through a fresh encounter start, so the
  wandering chance drops twice, as the ST's return to state 11 does.
- Swimming rolls II's `rand / 327` since s83 (`Swimming.Resolve(…, TerrainRoll.Rand327)`, `terrain.md` §10). Since s92 the
  lock, pick, listen and trap rolls do too `[C]`: the ST's `trap_disarm 0x10db0` (spot `r ≤ @36`, last success; disarm
  `r ≤ @38`; avoid `22·r < 10·DEX·(a + 3)`; damage `5·r·(a − 1)/100`) and `door_lock 0x111d6` (pick `r < @44`, listen
  `r < @40`, both last success over non-Dead members; the two ambush flags `r ≤ 50` at `0x11354…0x113b2`) are I's tests
  with every `r` = `rnd100 0x0fd40`. Core: the `TerrainRoll` of `EncounterRules` (`DungeonTraversal.Encounters`, the
  session's `Rules.EncounterRulesOrDefault`, script op 16) is passed to `DungeonTraps` / `DungeonLocks` /
  `Surprise.LockAmbush`; I keeps `Percent`.
