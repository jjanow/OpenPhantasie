# Phantasie II (Atari ST): overworld terrain — logic spec (lava, mist, dark voids)

Phase 09 T3, task 3b.7. This spec covers only what II changes in the wilderness step. Everything else follows Phantasie
I's rules in `reverse-engineering/dos/logic-specs/overworld-movement.md` (DOS OV2 `0x5f3b..0x6188`) and
`map-tile-graphics.md`. Code: `PHANT.PRG` image addresses (`../FORMAT.md` section 1).
Tags as in `docs/plan/RE-REFERENCE.md`:
- `[C]` read from the ST code
- `[L]` also seen live in Hatari (`../parity/terrain-live.md`)
- `[~]` probable
- `[?]` open

Manual: the combined I/II rulebook p.27, "New terrain features" (quoted in section 6). Differences with the manual:
`DIFFERENCES-II.md` D2-007 (lava, NOT A CONFLICT), D2-008 (mist and dark voids, NOT A CONFLICT) and D2-014 (how the terrain
looks, NOT A CONFLICT). Manual question Q10 (`docs/plan/09-trilogy-manual-p2.md`): section 7.

## 1. Terms

- **Cell value** `v` = the region grid byte (`OUTn.DAT` bytes 0–519; `../FORMAT.md` section 9.2). A fogged cell stores `v + 120`.
- **Class** `c = v / 10`, **digit** `T = v mod 10`. The step code stores them in `[0x25b80]` and `[0x25f30]`.
- **r** = `rnd100` (`0xfd40` = `rand() / 327`, 0..100). Each use is a new draw. The value 100 has a weight of only
  68/32768.
- **Terrain-fight flag** `F` = `[0x25b8a]`. I's equivalent is DOS `[0x6496]`.

## 2. The step (`overworld_step 0x6022`), in order `[C]`

1. **Target cell.** It is the party position plus the direction. Leaving the 26 × 20 grid swaps the region first
   (`0x66ba`, then `0x6724`, as in I).
2. **Reveal before the read.** `0x676c` → `0x5816` un-fogs the square of radius `[0x25b8c]` around the target, redraws
   the window and resets the radius to 1. The radius is 1 except right after VISION (section 5). Only then does `0x6792` read
   the target byte. So every rule below sees the decoded value, as in DOS.
3. **Wall.** Tile 100 means no move and no time. The window is redrawn round the old cell. On an in-grid bump, the game
   shows one of two timed messages (RSC 15 if region > 16, else 17; I: "THE RIVER STYX" / "[original text]"). Off
   the grid, nothing is shown.
   - **Quirk `[C]`:** the reveal in step 2 already ran on the wall cell. A bump therefore un-fogs the radius-1 square
     round the wall.
4. **Move and time.** The party moves to the cell and the travel-time word `[0x25eb0]` gains the step cost:
   - class ≤ 3: `(c + 1) · 75`
   - any other class: **60** (`0x612c` → `0xef62`)
   - So **lava, mist and dark voids all cost 60**, the cheapest rate `[C][L]`.
   - `0xef62` adds in 16 bits and then masks with `0xffff`. The "clamp at 65535" can never fire, so the word wraps.
     This is the same dead clamp as DOS `0x6528` `[C]`.
5. **Dispatch on the digit T** (before the class):
   - T = 6: location text (feature arg → pages, as in I) and nothing else. So tiles **76, 86 and 116** are plain text
     cells: no fight and no lava damage, whatever their class.
   - T = 7: fixed fight. The monster is feature arg + 1 (1-based), `[0x25f60] := 1`, state 11.
   - T = 8: plain move.
6. **Dispatch on the class:**
   - 5: dungeon (state 13).
   - **7 or 11: terrain fight (section 3).**
   - **8: lava (section 4).**
7. **Dispatch on the cell value:**
   - 40: town ("ENTER?").
   - 41: wilderness inn (state 9).
8. **Water.** Class 10 is swimming (state 10 = `0xfac2(1)`), as in I.
9. **Random encounter.** Otherwise `r < 6` (≈ 6 %) goes to the encounter builder `0xfe38`. That builder is I's day/night
   builder, and it is not part of this spec.

Classes 7, 8 and 11 always return from step 6. They **never** reach the 6 % roll `[C]`.

## 3. Mist and dark voids: the terrain fight (classes 7 and 11) `[C][L]`

```
if F == 0 and r <= 8:                     # 9 / 101 per step, ≈ 8.98 %
    monster = r' / 34 + 12·T + 1          # 1-based; r' a new draw → 0-based id 12T + {0,1,2} (≈ 34 / 34 / 32 %)
    build the monster groups (0xafb6, the normal builder: the lead monster + its companion groups)
    F := 1;  [0x25f60] := 2;  → state 11 (encounter start)
else:
    plain move                            # no damage, no 6 % roll, no other effect
```

- **Both classes run the same code** (`0x63b6` sends c = 7 and c = 11 to `0x6338`). The only difference between
  them is the picture (section 5) and which monsters their digits select.
- **Once per trip.** F is set only when the fight fires. It is cleared in three places:
  - the state-1 handler `0x73d0`: after the town loop, that is on leaving a town
  - the state-3 handler `0x7454`: when the dungeon loop returns 2, that is dungeon → wilderness
  - the state-6 handler `0x7520`: party wiped out
  
  These are the same three events that clear I's `[0x6496]` `[C]`. Fleeing or winning the fight does not clear F, so
  after the first terrain fight **every class-7/11 cell is fight-free until the next town or dungeon** `[C][L]`.
- **`[0x25f60]`** (fight kind: 1 = T = 7 fixed fight, 2 = terrain fight, 3–5 = other builders) is written and cleared
  (`0xa2c0`), but no code reads it `[C]`. It has no effect (I's `[0x6498]` is equally stale).
- **Monsters per digit** (`extracted/monsters.json`):

  | Tiles (class 7 / class 11) | 0-based lead ids | Monsters |
  |---|---|---|
  | — / 111 | 12–14 | APPRENTICE DEVIL, ZOMBIE, GHOUL |
  | 72 / 112 | 24–26 | LARGE SKELETON, WIGHT, DEATH FLY |
  | 73 / 113 | 36–38 | DEVIL, WRAITH, GIANT WASP |
  | 74 / — | 48–50 | DEMON, VAMPIRE, GHOST |

  Undead, devils and one insect per digit. Companions come from each monster's own `companions` field (the live fight:
  WIGHT lead with ZOMBIE and GHOUL ranks, section 8).
- **vs I:** I's class 7 is a **certain** fight (100 %), once per trip, against a fixed monster `12·T + 10` (tile 74 →
  WARRIOR at the OUT8 bridge, 75 → GREAT TROLL in the Black Lord's realm). I's class 11 is the **magical pool**
  (state 8, "A MAGICAL POOL" / "%s IS CHANGED"). II turned class 7 into a 9 % roll over three monsters, and gave
  class 11 the same rule. II has no pool code and no pool string. Same tile number, different rule: the remaster's
  terrain rules must be per game (data), not shared.

## 4. Molten lava (class 8) `[C][L]`

```
T == 0 (tile 80):  plain move                        # no damage, no encounter roll (64 cells, OUT30/OUT31)
T == 6 (tile 86):  location text only (section 2 step 5)    # 1 cell, OUT30
T == 8:            plain move (none on the maps)
otherwise:         state 8 = 0xfac2(0):
    title " L A V A :"
    for each party slot k < party size [0x2831c]:
        if status @24 == 0 (dead): skip                # dead members take nothing and get no line
        dmg = r·T / 10 + 1                            # 1 … 10T+1: tile 81 → 1–11, 82 → 1–21, 83 → 1–31, 85 → 1–51
        print "<name> TAKES <dmg> DAMAGE"
        0x9794(k, dmg, 1): HP @22 −= dmg; if HP ≤ 0 → 0x9894 (death) and "<name> DIES"
    timed box (game-speed delay [0x1fefa] + 1500), then redraw
    if nobody is alive (0xb662 < 0): state 6 (party wiped out)   else state 2 (go on)
```

- There is **no skill or save roll**. Every living member is hurt on every lava step, and the damage is rolled per
  member. Lava returns before the 6 % roll, so **no encounters** on lava (the manual agrees).
- The same routine with mode 1 is swimming: "SWIMMING :", `r < SWIM @48` → "SWIMS", else "SINKS AND TAKES" `(5r +
  100)/100` = 1–6 (6 only at r = 100; I: 1–5). Otherwise I's rule.
- In I, state 8 was the magical pool. II reuses the number for lava. I's class-8 cells (sparse magenta dots, a desert
  reading `[~]`) are ordinary land.
- **Where:** 81 ×84, 82 ×55, 83 ×45, 85 ×404 (harmless 80 ×64). Regions 17, 22, 27, 28, 30 and 31 (table in section 5).

## 5. Which cells are which, and how they are drawn

**Picture.** The ST map is not drawn cell by cell, unlike DOS I.
- `0x5028` paints the region's polygon picture (`OUTn.DAT` bytes 1500–2999): a background pattern, then
  `[count][pattern][points]` polygons.
- Each fill is `vsf_udpat` with the 4-plane, 16 × 16 user pattern at data `0x21dd6 + 128·pattern` (`0x545a`), filled
  through `v_fillarea`.
- The map palette is data `0x22956` (`0x5712`, XBIOS Setpalette).
- **Pattern number = cell class** for nearly every cell `[C]`. Rasterising all 17 pictures and sampling each cell
  centre gives the same pattern as the class for: class 7 on 819 of 1036 cells, class 8 on 545 of 653, class 11 on
  247 of 379. The misses are cells on a polygon edge.
- The pictures are artwork that follows the grid; the rules only ever read the grid.

| Pattern = class | Look (EmuTOS 1.4, Hatari; `../parity/shots/p2-fill-patterns.png`, `p2-out17-terrain.png`) | Reading |
|---|---|---|
| 7 | **solid black** (planes all 0) | **Dark Voids** `[~]`: "Dark areas" |
| 8 | dense blue / red / grey / green swirl | **Molten Lava** `[C]` (the `L A V A` path) |
| 11 | **sparse grey dots** on black (pixel value 8 = `0x333`) | **Mist** `[~]`: "an area of … dots" |
| 6 | sparse grey dots, the same look as 11 | path (I's reading) |
| 9 | orange dashes on black | class 9 (ordinary land, 6 % roll) |
| 10 | blue waves | water |

- **Mist = class 11 and Dark Voids = class 7** is the reading of the manual's own descriptions ("dots" vs "dark
  areas") against the ST pictures `[~]` (seen live).
  - No game text names either class, so the code alone cannot confirm it.
  - The manual's colours do not match the ST: its mist is blue and its lava an orange wave, while the ST shows grey dots
    and a blue/red swirl. The ST's only orange pattern is class 9, which the code treats as ordinary land.
  - So the manual's pictures likely describe another platform (the rulebook covers Apple, C64, Atari 8-bit/ST and Mac)
    `[~]`. Recorded as D2-014.
  - The colours assume that a multi-plane `vsf_udpat` writes pixel values directly, which is what EmuTOS does (water
    comes out blue, as expected). That real TOS 1.0 does the same is `[~]`.
- **Party marker** `[C][L]`: `0x56d6` draws a yellow cross icon on the party's cell over the picture; towns and dungeon
  entrances get their own icons. Hazard cells never block. Only tile 100 blocks, and no terrain changes the sight
  radius.

**Cells per region** (initial grids = `MAPS.INT`; fog removed):

| Region | Dark Voids (class 7) | Mist (class 11) | Lava (class 8) | Text cells on these classes |
|---|---|---|---|---|
| OUT17 (DEATHPORT) | 72 ×116 | 112 ×30 | 81 ×16, 82 ×29 | 76 ×1 |
| OUT18 (BLACK MOOR side, DEMONIAC exit) | 72 ×222 | 111 ×50 | — | 116 ×1 |
| OUT22 | 72 ×96 | 112 ×77 | 81 ×25 | 116 ×2 |
| OUT26 | 73 ×107 | 112 ×37, 113 ×46 | — | — |
| OUT27 (DEMONIAC) | 74 ×221 | 113 ×41 | 81 ×26 | — |
| OUT28 (DARK ABYSS, DNG7 ring) | 74 ×158 | 113 ×91 | 81 ×16 | 76 ×1, 116 ×4 |
| OUT30 | — | — | 80 ×31, 81 ×1, 82 ×3, 83 ×45, 85 ×230 | 86 ×1 |
| OUT31 | 74 ×115 | — | 80 ×33, 82 ×23, 85 ×174 | — |

Regions 1–12 have none of the three. Totals: dark voids 1035 fight cells (+2 text), mist 372 (+7 text), lava 588
damaging cells (+64 harmless digit-0, +1 text).

## 6. Manual (p.27, "New terrain features")

> • Molten Lava: Appears as an orange wave pattern. Extremely dangerous to travel through the intense heat: almost always
> causes damage to your characters. No encounters.
> • Mist: Appears as an area of blue dots. A fine mist shrouds these areas, making travelling difficult. Undead and
> insect encounters abound.
> • Dark Voids: Dark areas considered dangerous to travel through. Again, undead and insects flourish in these areas.

| Manual | ST | Where |
|---|---|---|
| Lava "almost always causes damage", "no encounters" | damage on every step for every living member, except digit-0 cells; no encounters | D2-007 (decided: NOT A CONFLICT) |
| Mist "makes travelling difficult" | costs 60, the cheapest step (grassland 75, mountains 300); no other effect | D2-008 |
| Mist/voids: "encounters abound / flourish", voids "dangerous" | one ≈ 9 % fight per trip; after it, the cells are fight-free, safer than ordinary land (6 % every step); no damage | D2-008 |
| Undead and insects | yes: each digit's lead monsters are undead/devils plus one insect | agrees |
| Orange waves; blue dots | blue/red swirl; grey dots (ST, EmuTOS) | D2-014 |

## 7. Q10 answered

**"Lava damage amount and frequency; Mist's movement cost; what makes Dark Voids dangerous."**
- **Lava** hurts every living member on every step onto a lava cell with digit 1–5. The damage is `1 + ⌊r·T/10⌋`,
  rolled for each member: up to 11 / 21 / 31 / 51 on tiles 81 / 82 / 83 / 85.
  - Cells with digit 0 (tile 80) and the text cell 86 do nothing.
  - There is no roll to avoid the damage, and no encounters.
  - A member at HP ≤ 0 dies. A wiped party goes to the death screen.
- **Mist** costs 60 time units per step, as dark voids, lava, water and every class ≥ 4 do. Grassland (75) and mountains
  (300) cost more. Nothing else makes mist slower.
- **Dark Voids** are dangerous only through the terrain fight: about 9 % per step, once per trip, against undead and
  insects that depend on the cell's digit. Mist has exactly the same rule. Neither one does damage.

## 8. Live check (Hatari + EmuTOS, s78) `[L]`

`../parity/terrain-live.md`. Summary:
- **Setup:** PIPPACOTT's exit was moved to OUT17 (11,5) with a test-only `TWNS.DAT` edit, the party HP was set to 200,
  and OUT17 was un-fogged.
- **Pictures:** class 11 shows grey dots, class 7 solid black, class 8 the swirl, exactly over the cells of section 5.
- **Mist (112):** the 15th step from town started a fight. RAM showed monster `[0x2832c]` = 26 (WIGHT), F = 1,
  `[0x25f60]` = 2, and the travel time 900 = 15 × 60. The monsters attacked first; Show names gave ZOMBIE ×4, WIGHT
  ×3, GHOUL ×2.
- **After Flee:** F stayed 1. The next 42 mist steps and 41 dark-void steps had no fight, and each step added exactly
  60.
- **Lava (82):** 9 steps gave 45 damage rolls, all 1–20 (bound 21), mean 9.3 (expected ≈ 10.6). Every member was hit on every step, the
  RAM HP drops equal the printed numbers, there was no fight, and each step cost 60.

## 9. Open

- `[?]` Whether real TOS draws the multi-plane fill patterns with the same colours as EmuTOS (D2-014). It matters only
  for the picture, not the rules.
- `[~]` Mist = class 11 and Dark Voids = class 7 rests on the pictures against the manual's words. No ST text names
  them.
- **First strike in a terrain fight** `[C]` static. The terrain path (`0x6338..0x638e`) sets only the trip flag
  `[0x25b8a]` and `[0x25f60]` = 2, then calls the group builder `0xafb6`. Every writer of the surprise words `[0x25cdc]` /
  `[0x25e9a]` was listed (`0x7664` clear F at round 1; `0x84e8`, `0x8eb2`, `0x8ed6`, `0x8ee2`, `0xa004` H in the round;
  `0x9cfa`/`0x9d02` minion swap; `0xfe8a`/`0xfe92` night; `0x100b4`…`0x10270` day menus; `0x10bb4`, `0x10d56` dungeon
  fights; `0x1137c`, `0x113b2`, `0x11d48`…`0x11ee0` dungeon locks / scripts): none is on the terrain path or in the
  builder. So a terrain fight starts with the words the last encounter left (F is always 0 by then, cleared in round 1),
  I's stale-surprise class-7 quirk; Core does the same (`TileClassFight` with the stale globals). The one live sample
  (monsters struck first) fits a stale H.
- **Wall texts** `[C]`: the step shows `OUT.RSC` (the wilderness resource) tree 15 **" THE RIVER STYX "** past region
  16, else tree 17 **" [original text] "**, I's pair (read from the resource file; `PHANT.RSC`'s trees 15 / 17 are other
  dialogs).
- Not traced, because II keeps I's code: the region-swap geometry (`0x66ba`: new region = region − 4·dx − dy) and the
  encounter builder `0xfe38`.
- Related, not terrain: spell #52 MONSTER EVAL in the wilderness reports level `min(2·(T + 1), 12)` from the current
  cell's digit (`0x705c`). Spell #53 VISION sets the reveal radius `(@18·100 + 500) / 400` for one step (`0x7000`).
  Both belong to the magic spec.

## 10. Core (s79)

- **Data:** `GameData/phantasie2/rules.json` `terrain` (`TerrainRules`, loaded into `RulesProfile.Terrain`): roll
  `rand_327`, random encounter `r < 6`, classes 7 and 11 `terrain_fight` (`fight_roll_max` 8, `monster_per_digit` 12,
  `monster_spread` 34), class 8 `lava` (`damage_divisor` 10), class 10 `swim`. I's rules are data too
  (`phantasie1/rules.json` `terrain`: class 7 fixed fight `12·T + 9`, 10 swim, 11 pool; Core's 0..99 roll), pinned to
  `TerrainRules.PhantasieI`.
- **Step:** `OverworldTraversal` takes the game's `TerrainRules` (`GameTables.CreateTraversal`, `GameDataRepository.CreateTraversal`).
  A terrain fight is a `TileClassFight` with the 0-based lead monster `12·T + r′/34` and uses I's trip flag
  (`OverworldState.TileClassFightDone`, the same three clears). It starts with the stale surprise globals, like I's class-7
  fight (section 9 first strike). Lava is `OverworldMoveKind.Lava` → `Lava.Resolve`: " L A V A :", then each living
  member's `r·T/10 + 1` (`DamageSource.Lava`), deaths, and a wiped party goes to the defeat screen. Digit 0 is a plain move.
- **Step costs:** `GameData/phantasie2/travel-time.json` (section 2 step 4: classes 0–3 `(c + 1)·75`, the rest 60; read by
  `GameTables.TravelTime`). D2-040 (user: manual, as I's D-055): the file charges the path 30; the ST's 60 is `p2.path-time` = `st`.
- **Order check:** Core tests class 5 before the digits 6/7/8 (the ST does the reverse). A test shows II's maps have no tile
  56/57/58, so the two orders agree.
- **Graphics:** II's ST pack is wired (`GameData/phantasie2/graphics/atari-st.json`); the fill patterns of section 5
  resolve as `overworld.pattern.N` (D2-014). The Client draws the ST region pictures (the ST's own polygon fills
  with these patterns), every dungeon / castle icon of a region and the water / lava colour cycle. The ST shows **no map
  legend**, so none is drawn (originals first); the pattern pictures stay available for a later QoL legend (PLAN P3).
- **Swimming** rolls `rand / 327` in II (`Swimming.Resolve(…,
  TerrainRoll.Rand327)`, from the terrain rules' `roll`): the swim test `rnd < SWIM @48` and the damage
  `(5·rnd′ + 100) / 100`, which reaches **6** at rnd′ = 100 (I: 1–5). **Lava** draws each living member's swim roll
  first and ignores it (the routine `0xfac2` is shared: `0xfb5c` rolls before the mode test), then the damage roll; a member
  killed by lava shows "DIES" in place of its "TAKES" line (`0xfc92` prints over the same row).
