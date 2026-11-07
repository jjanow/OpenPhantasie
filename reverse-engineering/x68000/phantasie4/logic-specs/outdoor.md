# Phantasie IV — outdoor travel, water, ships, encounters (spec, 3d.6)

Source: X68000 `P4.X`, static reading (addresses = P4.X image addresses). Written source: PC-98 manual pp. 35–37
(outdoors), p. 28 (shipwright), p. 21 (Swim, Navigation), pp. 49 / 55 (Levitation). Tags: `[C]` code, `[~]` likely,
`[?]` open. Not cross-checked on MSX2; nothing checked in an emulator. Skills: `skills.md`. Towns, outdoor inns,
Charon, pools: `towns.md` §10 (corrected here in §7).

`_cmapf` (map kind) = 0 town, **1 outdoors**, 2 dungeon (`_game` `0x27E`) `[C]`. `_par` offsets: `../FORMAT.md` §6.7.

## 1. Outdoor maps: `MOUT` (disk 2, 18 × 700 B) `[C]`

`_omap_load` (`0x157F6`) reads record `R[_cmapn]` of `mout` with `_xrndint(1, "mout", 700)` into `_out`
(`0x78CE0`). `R` (table `0x42DD2`, by outdoor map number 1–24): 1→0, 2→1, 3→2, 4→3, 5→4, 6→5, 7→5, 8→6, 9→7, 10→8,
11→5, 12→5, 13→9, 14→10, 15→11, 16→12, 17→13, 18→12, 19→12, 20→14, 21→15, 22→16, 23→17, 24→12. Records 5 (maps 6,
7, 11, 12: all rapids) and 12 (maps 16, 18, 19, 24: all "deep River of Hell") are impassable filler. Map 23 (record
17) is reached only through the pool on map 1.

| Offset | Size | Content | Reader |
|---|---|---|---|
| 0x000 | 26 × 26 | tile bytes, row-major (`_out[x + 26 y]`); terrain type = byte / 10, sub-index = byte % 10 | `_omain`, `_omove` |
| 0x2A4 | 3 × 2 | towns by sub-index: {town id (`_towns`), byte 2 (1 on the Scandor maps, 2 on the Netherworld maps): **never read by P4.X** `[C]` (s110: the only access to the table, `0x78F84` = `_out` + 0x2A4, is `_otown`'s byte read at index × 2; nothing reads `0x78F85` / `87` / `89`, and the four uses of `_out` are `_xrndread` and 26 × 26 grid reads)} | `_otown` |
| 0x2AA | 3 × 3 | dungeons by sub-index: {location (`_dungeons`, = `O_0xx`), entry x, entry y} | `_odungeon` |
| 0x2B3 | 1 | unused (0 in all records) `[~]` | |
| 0x2B4 | 4 | outdoor inns by sub-index (`_inns` index) | `_oinn` |
| 0x2B8 | 4 | destination {map kind, map, x, y} of this map's pool or Charon | `_opool`, `_oboatman` |

Map grid `[C]` (`_omove` `0x14864`): stepping off a 26 × 26 map enters the neighbour; maps 1–15 sit in a grid 5
wide, maps 16–24 (Netherworld) in a grid 3 wide (`map + dx − 5 dy`, resp. `− 3 dy`; diagonal exits handled). `_cblock`
/ `_oblkofs` pick the 3 × 3 block of maps drawn on the overview (`M` toggles overview / close-up, `_getxy`); decoded s108
(`../FORMAT.md` §6.9 "Outdoor map", built as `P4OutdoorLoop.OverviewBlock` / `OverviewMaps`).

## 2. A step outdoors (`_omain` `0x15A92`) `[C]`

Per move (8 directions, `_omove`):

1. **Can the party enter the target tile?** (`_ochkedge(type)` `0x14096`): type 1 "Those are rapids." — blocked;
   type 9 "That is the deep River of Death." — passable only if the party holds both **Charon's Raft** (item 132) and
   **Charon's Staff** (133), in the pack or carried by members; type 10 — always blocked (same message); type 13
   "shining sea" — see §4.3; type 19 "Beyond lies the open sea." (そこからはそと海だ) — blocked. Everything else is
   enterable.
2. **Time:** `_dayout(H)` with H hours by terrain (table `0x42D60`, index type + 1 for types 0–10, else 0):

   | Type | Terrain (`0x42D30`) | Hours per step | Encounter ‰ (`0x42FB2`) |
   |---|---|---|---|
   | 0 | water 水 | 36 | 60 |
   | 1 | rapids 激流 | 36 | 60 |
   | 2 | desert 砂漠 | 8 | 25 |
   | 3 | hills 丘 | 20 | 30 |
   | 4 | mountains 山脈 | 20 | 30 |
   | 5 | forest 森林 | 14 | 50 |
   | 6 | grassland 草原 | 6 | 40 |
   | 7 | lava 溶岩 | 20 | — (no check) |
   | 8 | mist 霧 | 12 | 60 |
   | 9 | River of Hell 地獄の河 | 48 | 0 |
   | 10 | deep River of Hell [original text] | 48 | 0 |
   | 11, 12 | (no name; Netherworld ground `[~]`) | 8 | 60 |
   | 13 | shining sea (barrier) | 8 | 0 |
   | 14–18 | town, inn, dungeon, pool, Charon | 8 | — (no check) |

   `_dayout(n)`: hours = n + `__daytime`; `__daytime` = hours mod 24; `_par`+16 += hours / 24 (cap 65 535). So
   `_par`+16 counts **days** `[C]`; the town gate ages characters by `_par`+16 / 0x700 (`towns.md` §2.2). The terrain
   name is printed when it changes.
3. **What the tile does:** water / rapids → `_owater` (§3, §4); lava → `_olava` (§3.2); town → `_otown` ("You have
   come to the town of X. Enter?"); inn → `_oinn`; dungeon → `_odungeon` ("There is X here. Go inside?"); pool →
   `_opool`; Charon → `_oboatman`. Any tile that is not water ends a voyage (`_save_ship`, §4.2).
4. **Encounter:** on every tile except lava and the special places: `rand(1000) < E[type]` (table above, per mille;
   rand returns 1..1000, so (E − 1) ‰) → `_oencchk` (§5).

## 3. Swimming and lava

### 3.1 Swimming (`_owater` `0x1514E`, party not aboard a ship) `[C]`

Every step onto water / rapids, for every member whose condition is not Dead: if `_pc` + condition < 4, or
rand(100) > Swim, the member takes rand(5) + 1 damage (2–6) through `_pdamage` (outside combat: HP − d, d ≥ HP →
dead; no wound location). `_pc` is the combat condition from `_setpc` (`0x1D3C`, read s97 `[C]`: 4 = dead or head /
torso lost; 0 = condition 2 or head / torso seriously wounded; 2 = a **leg** seriously wounded or lost; else 3), so a
fit Okay member rolls Swim, while an asleep or badly wounded member always takes the damage (an Okay member with a bad
leg: 1 + 2 = 3 < 4, always). Levitation does **not** protect outdoors (the flag `_par`+15 is not read here) `[C]`.
`_pdamage` here is type 0: no wound, no `_setpc`, so conditions stay (a night-attack sleeper stays asleep).

Manual p. 37: "members who swim badly lose hit points, as when hit by an enemy" — matches.

### 3.2 Lava (`_olava` `0x152E8`) `[C]`

"It's hot!" Then for every member not Dead: if rand(18) ≤ **Luck** (+34), the member takes rand(5) (1–5) damage. As
written, a *luckier* character is hurt more often (Luck 18: always) `[C]` (looks inverted; original, not fixed).

## 4. Ships

### 4.1 Buying (`_shipyard` `0x5E4E`, seaside towns) `[C]`

"A ship costs 1500 GP." Offered only if party gold ≥ 1500 (no town factor). If a ship exists: "If you buy a ship, I
will take back the one you bought before." Yes → gold − 1500; ship state = moored at the town's exit map, at the
town's ship square (`_shipexit`). Matches manual p. 28 (one ship at a time; the old one is scrapped).

Ship state = `_par`+06–09 `[C]`: +06 low nibble 0 none / 1 moored / 2 aboard, high nibble = navigator's party slot;
+07 map, +08 x, +09 y. A drift counter (`0x43004`, not saved) counts forced random moves.

### 4.2 Sailing (`_oship_mark` `0x14478`, `_owater`, `_save_ship` `0x14BF4`) `[C]`

- **Boarding:** stepping onto the moored ship's square: "The party boarded the ship. <name> took the helm." The
  navigator is the member with the highest **Navigation** (first in party order on a tie; dead members included, since
  condition is not checked) `[C]`; the drift counter is cleared.
- **Each step on water while aboard** (no Swim damage): if the drift counter is 0 and rand(100) > navigator's
  Navigation: with rand(100) < 15 (14 %) "Caught in a storm!" and 10 forced moves, else 1 forced move. While the
  counter is > 0 the player's key is ignored: each step goes in a random direction (rand(8)) and the counter drops by 1.
- **Reaching a non-water tile while aboard:** if no forced moves are pending: "The party left the ship." and the ship
  is moored on the last water square; if a forced move carried the ship onto land: "The ship ran aground!" — the ship
  is lost (state cleared).
- **Encounters at sea:** every outdoor fight on a water / rapids / shining-sea tile is fought on a ship battlefield
  (`_e_terrain` → `_e_on_ship` `0x26862`), and the enemy is replaced by **Swashbuckler (DATM 76) 64 % / Pirate (75)
  36 %** `[C]`. After the fight `_oencgo` marks a ship as moored on the party's square (`_shipf`) — also when the party
  was **swimming** (no ship before), so a party that is attacked in the water ends up with a ship `[C]` (looks like
  "the pirates' ship is taken"; not described in the manual `[~]`).
- **The ship mark (s135) `[C]`:** `_omark` (`0x1476A`) draws the party square (`_getxy`: 8 pixels a square in the
  close-up, 4 on the overview; `_fbox` colour 0 over the square, colour 15 one pixel inside), then `_oship_mark(1)`
  (`0x14478`), which returns at once unless the party's map is the ship's (`0x7A892` = `0x789CF`) and the ship state
  (`0x789CE` low nibble) is 1 moored or 2 aboard. It puts the ship's square into `_getxy` and calls `_xbox(8, x + 56,
  y + 46, s, s)`: `_xbox2` **XORs every pixel of that square with 8**, so the mark is the map's own picture with each
  pixel's palette index bit 3 flipped (light-blue water 9 → grey 1, blue 8 → black 0, …), toggled by `0x7E802` (`_omark(0)`
  takes it away the same way before restoring the party square). Aboard, the ship's square is the party's: the party
  square shows colours 8 (blue) and 7 (yellow) instead of 0 and 15. No mark on another map, even on the overview.
  Remaster (s135): the same, in `P4DungeonScreen.OutdoorMarks` / `ShipMarkColours`; seen in the Client with an edited
  save (`parity/shots/p4-remaster-ship-mark-moored.png`, `-moored-zoom.png`, `-aboard-zoom.png`). The original's own
  ship mark has not been seen on XEiJ `[~]` (a static reading).
- `_use_box` (`0xFCB2`, an item's use): disembarks and teleports the party to outdoor map 21 (6, 14) `[C]`.

### 4.3 The shining sea (`_obarrier` `0x13FDE`, type 13) `[C]`

Before story flag `_par`+15C is set: aboard a ship → "The sea ahead is shining. You hear a faint humming… Go
closer?" Yes → "A strange force destroyed the ship!!" (ship lost, party stays); not aboard → "A strange force pushed
you back…". After the flag is set the tiles count as water.

### 4.4 Ship loss vs the manual

Manual p. 28: 「ただし、この船は戦闘で壊れたり、使い古されたりします。」 — "But this ship can be wrecked in combat or wear
out." Code: no fight damages the ship (a sea fight leaves a moored ship on the square), and there is no use counter;
a ship is lost only by running aground during a forced drift, by the shining-sea force, or by buying another `[C]`.
D4-038 (decided: manual, user s82; amounts user s84: 5 % after a lost or fled sea fight, ship life 4000
sea steps as option `p4.ship-life`).

## 5. Outdoor encounters (`_oencchk` `0x1668C`) `[C]`

A = `_al`[map − 1] (`0x433C0`, maps 1–24: 4, 3, 2, 1, 1, 0, 0, 2, 0, 1, 0, 0, 2, 2, 2, 5, 5, 5, 5, 6, 6, 8, 7, 5).

1. **rand(100) ≤ 37 — night attack** (no time-of-day test: `__daytime` is never read here) `[C]`: one random living
   member keeps watch (15 tries of rand(party size)); every other Okay member falls asleep (condition 2). "Monsters
   attacked in the night!" If a watch was found: "<watch> sensed monsters nearby."; rand(100) < Listen → "<watch>
   noticed them first." (fight, not surprised), else the party is surprised.
2. **Else, rand(100) < 36 (35 %):** "You found sleeping monsters. Attack them in the night?" Yes → fight with the party
   surprising them; No → no fight.
3. **Else:** every member not Dead rolls rand(188) ≤ Listen; the last who succeeds "sensed monsters nearby".
   - rand(100) < 8 A + 30: the monsters come on. Nobody sensed them and rand(10) > 4 (60 %) → "The monsters took you
     by surprise!"; otherwise "Monsters approach…" (normal fight).
   - Otherwise, if someone sensed them: "Fight?" Yes → rand(10) > 2 (80 %) "You took the monsters by surprise!", else
     they surprise you. No → rand(10) < 8 (70 %) the party avoids them; else "Monsters approach…" (fight).
   - Otherwise (nobody sensed): "Monsters approach…".

Manual p. 39 / p. 44 (members with good eyes or ears may spot monsters first; at night one member keeps watch and good
Listen wakes the party) — matches in substance. How surprise (`__einf1` / `__einf2`) changes the fight: `combat.md`
§1.3 (s98).

## 6. Levitation outside combat

Casting Levitation (spell 13) outside combat sets `_par`+15 ("The party floated up into the air.", `_pmagic3`
`0x2D3EC`); `_encount` clears it at the end of every fight `[C]`. Its only effect found is in dungeons: no water
damage while it is set (`skills.md` §4.4; `_idraw`). There is no distance or step count `[C]`, and outdoors the flag is
never read (§3.1). Manual p. 55 (Levitation, wilds and dungeons): 「パーティ全員が空中を飛べるようになります。足の速さは
変化しませんが、パーティ全体の歩幅の5倍の距離だけ飛んでいられます。注：パーティ全体の歩幅とは一番遅いメンバーの歩幅です。」 —
"The whole party can fly. Speed does not change, but they stay aloft for 5 × the party's stride in distance (the stride
of the slowest member)." Candidate conflict (duration; and no outdoor effect). The outdoor cast menu offers Levitation
(`_getmagic(m, 5)`, `spells-noncombat.md`, s102) `[C]`.

## 7. Charon and the pools (corrects `towns.md` §10) `[C]`

- **Charon** (`_oboatman` `0x14CAC`, tile type 18): "The boatman Charon says he will take you to <place>. Go?" Yes →
  moved there, free. On **outdoor map 17** (MOUT record 13, the Styx Port map) the destination is hard-coded: outdoor
  map 21 (Netherworld Level 6) at (3, 2), block 2 (`_boatmsg[1]`). Anywhere else it is the record's destination bytes
  (§1): the only other Charon tile is on map 21 (record 15) → town 14, Astral City (`_boatmsg[0]`). Map 17 is not the
  Town of Mist (town 17 lies on map 20).
- **Pool** (`_opool` `0x14DC2`, type 17): "There is a pool. Step in?" → "The party was teleported by magic." to the
  record's destination: map 1 → outdoor map 23 (20, 8); map 17 → dungeon 9 (the inn) at (16, 29); map 23 → map 1
  (19, 21). (Destination map 1 / 23 / 9 also sets the overview block 0 / 2 / 1.)

## 8. Against the manual (ships D4-038, Levitation D4-039, Navigation D4-040; decided: manual, amounts user s84 — see `DIFFERENCES-IV.md`)

1. Ship wrecked in combat / worn out (p. 28) — no such code (§4.4).
2. Levitation outside combat lasts 5 × the slowest stride (p. 55) — code: until the end of the next fight, any distance,
   no effect outdoors (§6).
3. Navigation: manual p. 21 「但しキャラクタの技能が50%以上になるまでは、使わないで下さい。」 ("do not use it until the
   character's skill is 50 % or more"). Code: Navigation cannot be "used"; the best navigator is chosen automatically
   on boarding, and any value works (a low value just drifts more). Advice rather than a rule; listed for completeness.
4. Swimming (p. 37), "beyond lies the open sea" (p. 37), shipwright (p. 28), outdoor inn (p. 37: `towns.md`) — match.

## 9. The loop as read for the build (s97, `_outdoor` `0x160FE`, `_oload` `0x15FBE`, `_omain` `0x15A92`) `[C]`

- **Every entry into `_omain` runs a full step on the square the party stands on**, before any key: `_omark(1)` (boarding,
  §4.2) → terrain name → `_dayout(hours)` → the tile handler → `_save_ship` if the square is not water → the encounter roll.
  `_omain` is entered on every `_outdoor` call, i.e. after a town or location-map exit, a map-edge crossing, a pool, Charon,
  an outdoor inn stay (`_oinn` reloads on the same square) and **every fight** (`_oencgo` sets `_cmovf` with the same map
  and square): after a fight the square's hours pass again, a swimmer swims again and another encounter can be rolled.
  `_oload`: the old square `_cox`/`_coy` = the arrival square; a ship the party is aboard moves with it (`_par`+07–09 =
  the new map and square). `_omain`'s entry clears the drift counter and the "last terrain" memory.
- **Terrain name:** printed ("\n\n%s", table `0x42D30`, entry type + 1 for types 0–10, else the empty entry 0) when the
  type differs from the last printed one and the drift counter is ≤ 1.
- **Shining sea after the flag** (`_par`+15C): the type is changed to 0 *after* the hours are charged (type 13: 8 hours),
  so it is water for the handler and the encounter rate (60 ‰).
- **Encounter roll:** only after the water handler (types 0, 1), the default handler (types 2–6, 8–13, 19+); not after
  lava, town, inn, dungeon, pool or Charon. Rate entry = type + 1 for types 0–12, else entry 0 (= 0 ‰).
- **Moves** (`_omove`, directions 0 SW, 1 S, 2 SE, 3 W, 4 E, 5 NW, 6 N, 7 NE; tables `0x42EF2` dy, `0x42F12` dx, dy north
  positive): inside the map the target's type goes through `_ochkedge`; **a blocked move passes no time, rolls nothing and
  runs no step**. Off the map: the neighbour map (`map + dx − W·dy`, W = 5, or 3 from map 16; a diagonal changes map only
  along the axes that left the map and keeps the other coordinate), entering on the opposite edge (y = 25 going north);
  **the arrival square is not checked** (`_ochkedge` is not called). The data has no passable border square that leads off
  the grid (test `NoPassableBorderSquare_LeadsOffTheGrid`).
- **Forced moves** (drift counter ≥ 1): no key is read; direction rand(8) − 1; the counter drops by 1 after each forced
  move, **also a blocked one**; a forced move that ends on land with moves still pending runs aground (§4.2); the last
  forced move onto land is an ordinary "The party left the ship.".
- **Boarding** happens in `_omark(1)` at the top of the step (before the water handler), also through `_omark(0)` at the
  start of a move, which is unreachable in practice (the step on that square has already boarded).
- **Keys** (`_omain` `0x15D2A`…): F profile (`_inspect`), P party info, C spells (`_ocast(5)`), U use item, S system: none
  of them takes a step (U can move the party: `_cmovf`). **M toggles the overview / close-up view and runs the square's
  step again** (`0x15E2A` sets the "moved" flag): time passes and a new encounter roll is made. The manual makes it a
  view switch only (D4-057, user s98: the manual); the code's step is option `p4.map-step` = `x68000`.
- **Surprise pairs** (`_oencgo(einf1, einf2)`): night watch heard (0, 0), not heard or no watch (0, 1); sleeping monsters
  attacked (1, 0); "took you by surprise" (0, 1); "You took the monsters by surprise" (1, 0); approach (0, 0). `_einf3` is
  set at the top of `_oencchk` and cleared only by attacking sleeping monsters: every other fight waits for a key first.
- **`_oinn`**: party gold ≥ 256 (`bcs`), "Rest at the inn?", −256, two lines, a key, `_inn`, `_dayout(1000)`, reload on the
  same square (whose step then asks again). **`_opool`** prints "teleported" only on yes.

### 9.1 Remaster build (s97, `P4OutdoorLoop`)

Built as above, with the decided manual rules: D4-039 (a levitating party does not swim outdoors; Levitation's steps run
down per move; every fight still ends it, as the code does — the manual gives a distance, not an end on fighting `[~]`),
D4-040 (`P4Outdoor.Drifts`), D4-048 (a watch who hears wakes the members the night attack put to sleep), D4-038 (5 % wreck
of the party's own ship after a sea fight not won: the ship is lost and no ship is moored; sea steps are counted).

## 10. Open `[?]`

- (s110 resolved: MOUT town byte 2 is never read by P4.X, §1.)
- (s98: the surprise flags, the `_e_on_*` obstacles and the outdoor monster group are decoded, `combat.md` §1.1.)
- D4-038: what happens when a ship reaches `p4.ship-life` sea steps (the steps are counted; the effect is not decided).
- MSX2 cross-check; an emulator check of one voyage (drift, storm) and one outdoor encounter.
