# Overworld / Wilderness Movement & Map Representation

The Isle of **Gelnor** overworld is stored as the `OUT*.DAT` region files
(`../extracted/overworld.json`, regen `tools/scripts/extract_maps.py`).
Movement lives in **`OV2.OVR`** (`FUN_5EA2` @ `0x5EA2`, region swap
`FUN_61CE` @ `0x61CE`, tile read `FUN_538B` @ `0x538B`).

## Map file format [C]

18 region files: `OUT1..OUT16`, `OUT18`, `OUT19` (no `OUT17`). Each is
**1024 bytes**:

| Offset | Size | Contents |
|---|---|---|
| `0x000` | 520 | **26×20** tile grid (`tile = data[y*26 + x]`) |
| `0x208` | 75 | **15 feature records** × 5 bytes: `x, y, arg, dest_x, dest_y` |
| `0x253` | … | **40-byte text pages** (space-padded, often NUL-terminated) |

Fog-of-war is **in-band**: unexplored cells store `visibleTile + 120`; the
draw/move path subtracts 120 when `raw > 119`. Exploration mutates the region
bytes (saved back to `OUT*.DAT`).

Normalized tile ids (after fog decode) are graphic/feature codes. Observed
classes:

| Tile / pattern | Meaning |
|---|---|
| `100` | Wall / blocked |
| `100..109` | Water class 10 (skips random encounters). **[C]** the step returns state **10** (`0x6151`) → OV2 `0x685c`, the swimming handler (`0x68ff` "SWIMMING:" deaths) — decoded (§Swimming), in Core (`OverworldMoveKind.Swim` → `GameSession.Swim`). 1959 cells of tile `101` |
| `40` | Town (`"A TOWN. ENTER?"`); feature `arg` = town id (`99→11`) |
| `41` | **Wilderness (independent) inn** [C]: step returns 9 → OV2 state 9 → `0x6700` (`town-menus.md` §Wilderness inn). 12 such tiles; none needs a feature record. **OUT13 (17,19) [C]:** tile 41 with feature `arg 5` — and APPLETON's own TWNS.DAT record (`+0x111..0x115` = region 13, (17,19), the town-tile position the Exit code copies into the party position) points at exactly this cell. Stepping there takes the inn path (state 9) and never "A TOWN. ENTER?", so **APPLETON cannot be entered from the wilderness**; the other 10 map towns' records match their tile-40 cells, and PHANTASIA's (OUT14 (5,11)) is a plain tile 35 with no feature. The manual: Pelnor is "one of the 11 initial towns of Gelnor" (PDF p.8). **What the record does [C]: nothing — dead data.** It is APPLETON's town-entrance record: the tile-40 path reads `arg − 1` as the town index (`0x611f`; arg 5 → index 4 = APPLETON, and the 10 working town records use args 1, 2, 3, 4, 6, 7, 9, 10, 11, 99 — 5 is the only missing one besides PHANTASIA's 8). But the step dispatch sends tile `0x29` straight to state 9 (`0x6149` → `0x613e`), and the inn handler `0x6700..0x685b` never calls the feature lookup `0x6214` (its only callers are `0x1dcc` dungeon entrance, `0x5fae` text, `0x6045` x7 fight, `0x6115` town). The raw cell byte is `161` (= 41 fogged), not `160`, so this looks like a map-data slip (inn tile where a town tile belongs). APPLETON is still reachable by town TRANSPORTATION (`magic-spells.md`; whether its picker lists APPLETON `[~]`). **Remaster (D-060, user: manual):** `map-patches.json` turns the cell into town tile 40, so APPLETON is entered on foot |
| `50` / class `5` | Dungeon entrance; `arg` = dungeon id; `dest_*` = dungeon start |
| `tile % 10 == 6` | Location text [C] (§Location text): `arg` < 10 → 1 page, 10..19 → 2 pages from `arg − 10`, ≥ 20 → 3 pages from `arg − 20`; no encounter roll. Temples, cathedrals, signposts and inn names |
| `tile % 10 == 7` | Fixed encounter [C]: `[0x64e6] = (arg + 1) & 0xff` (1-based → Core monster id = `arg`), `inc byte [cell]` (one-shot), prep `0x3263`, `[0x6498] := 1`, return 11 (fight). Writes no surprise flag (stale, `combat-resolution.md` §Surprise). `[~]` no feature record → `0x6214` returns region `+0x253`, so the id comes from byte `+0x255`; no shipped x7 tile lacks a record (Core: no fight) |
| class `11` (`111..113`) | "A MAGICAL POOL": step returns 8 → OV2 state 8 → `0x656b` [C] (§Magical pool). Not the inn |
| `tile % 10 == 8` | Plain move, **no encounter roll** (`0x608a` returns 2) [C]. This is a *spent* fixed encounter: the x7 path (`0x6045..0x6068`) does `inc byte [cell]` before the fight, so a fought x7 tile becomes x8 and the region bytes are saved back — fixed encounters are one-shot. No shipped map has an x8 tile. Core: increments the raw byte and fights `arg`; region bytes go into the session save |
| class `7` (`74`, `75`) | Tile-class fight `0x6094` [C]: if `[0x6496] == 0`: `[0x64e6] = 12·(tile%10) + 10` (1-based; Core `12·T + 9`: tile 75 → **GREAT TROLL**, 74 → **WARRIOR**), prep `0x3263`, `[0x6496] := 1`, `[0x6498] := 2`, return 11; else return 2 (plain move). **Never the 6 % roll.** `[0x6496]` is cleared only by the OV1 dispatcher exits (`0x9b`, `0xa9`, `0xbe`: leaving a town, and party defeat = OV1 state 6 `0x7e` → `0x5ee` → `0x3bc`) and on dungeon → wilderness (OV2 `0x9b`: state 3 handler `0x4325` returned 2), so it is **one fight per trip**. 698 × `75` (OUT18 360, OUT19 338) + 1 × `74`. No surprise write (stale). **[D✓] s41** (tile 75 placed on OUT1): fight at once — GREAT TROLL + KOBOLD/ORC companion groups; step off/on → plain move; leave a town → fights again; both fights opened monster-first (stale flags). Core: `OverworldMoveKind.TileClassFight`, `OverworldState.TileClassFightDone` |

## Movement model [C]

Cardinal handlers push `(dx, dy)` into `FUN_5EA2`:

| Dir | `(dx,dy)` | New position |
|---|---|---|
| East | `(1,0)` | `x+1` |
| West | `(-1,0)` | `x-1` |
| South | `(0,-1)` | `y+1` (`ny = y - dy`) |
| North | `(0,1)` | `y-1` |

Y=0 is the **north** edge; Y=19 is south. Only normalized tile **100** hard-
blocks an in-region step (party stays put).

### Region edges [C]
When the step leaves `0..25 × 0..19`:

```
new_region = old_region - 4*dx - dy
```

| Crossing | Region Δ | Arrival |
|---|---|---|
| North | −1 | `y = 19` |
| South | +1 | `y = 0` |
| East | −4 | `x = 0` |
| West | +4 | `x = 25` |

Valid ids: `{1..16, 18, 19}`. **[C] `0x5ea2`/`0x61ce`:** on leaving the grid DOS saves the current region
(`0x535d`, `OUT%-d.DAT`), computes the new id with **no range check**, writes the arrival edge coordinate straight into
the party position (`[0x751a]`/`[0x751c]`, crossing axis only), loads the new file (`0x5dce` → `0x530d`, COM read
`0xc75a`), reveals, and only then reads the arrival tile:
- arrival tile `100` → `0x6188` sees `[bp-6]` (crossed) set: **no message, no step back, return 2** — the party stands
  on the wall cell of the new region (no time; `[0x649a]`/`[0x649c]` untouched). `[C quirk]`, in Core. 13 edge
  cells do this: OUT1 S (24,19), OUT1 W (0,0), OUT8 W (0,16..18), **OUT13 N (9,0) → OUT12 (9,19)**, OUT14 S (1..4,19),
  OUT15 W (0,11), OUT16 E (25,17..18).
- the only edge onto a missing file is **OUT1's north edge** (24 water cells) → id 0, `OUT0.DAT`. Core: blocked.
  **COM reader on a missing file [C] (unpacked COM image):** the loader `0x530d` stores the id in `[0xc5f6]` and
  calls the read helper (`0xc75a` = COM `0x1b36`): `open(name, &h)` (`0x33df`, int 21h AH=3Dh) **sets `h := 0` when
  CF is set**, and `0x1b36` ignores that — it reads `0x400` bytes from handle **0** (AH=3Fh, `0x351e`) into the region
  buffer `0xc6fa` and then **closes handle 0** (AH=3Eh, `0x363a`). No message, no error path. Handle 0 is standard
  input, so DOS waits for a typed line from the console (echoed) and copies it over the start of the buffer `[~]`
  (DOS CON semantics, not game code; `[D+]` to see it). The rest of the buffer keeps OUT1's bytes, and the next region
  save (`0x535d`, id `[0xc5f6]` = 0) creates `OUT0.DAT` from it. stdin stays closed for the rest of the run.
  **Static:** OUT1 row 1 is all wall and row 0 has no other way in (OUT5 row 0 is wall, no region east of OUT1), so
  the shipped game **cannot reach** this edge; the `[?]` only matters after a map edit/teleport (`capture_s41_2026-10-15.md` §5).

Rough mosaic (north up, east right):

```
13  9  5  1
14 10  6  2
15 11  7  3
16 12  8  4
19       18
```

`OUT15 ↔ OUT19` is the narrow `+4` link. **OUT13→OUT12 [C]:** OUT13's top row is wall except (9,0) (tile 3);
north from there is `13 − 1 = 12` (the formula wraps the column, there is no region 12-above-13), arriving on OUT12
(9,19), a wall — so the party is put on that wall cell (quirk above). Not a real link, but it works both ways: south from OUT12 (9,19) goes back to OUT13 (9,0) ([D✓] s65).

### Step dispatch order [C] (s36, OV2 `0x5f3b..0x6188`)
```
tile = map byte at the new cell (0x538b, raw)            # see fog note below
if tile == 100: blocked (no time, no move)               # 0x5f44
move; [0x649a] = tile / 10; [0x649c] = tile % 10         # unsigned
travel time += step cost (§Travel time)                  # 0x5f88..0x5fa7, before any dispatch
switch tile % 10:  6 → text page;  7 → fixed encounter (feature arg + 1);  8 → plain move
switch tile / 10:  5 → dungeon;  7 → tile-class fight 0x6094 (once per trip, else plain move);  11 → pool (state 8)
switch tile:       40 → "A TOWN. ENTER?";  41 → wilderness inn (state 9)
if tile / 10 == 10: state 10 = swimming (0x6156 → 0x685c, §Swimming); no encounter roll
                   ("THE RIVER STYX"/"[original text]" are the *blocked* messages, 0x618e: [0x63e2] > 16 → STYX)
random encounter roll (below): rand0_99 < 6 → 0x70ed; AX = 1-based monster, 0 = no fight; caller preps if not prepped
```

**ENTER? gates (Core):** tile 40 asks "A TOWN. ENTER?" and class 5 asks "[original text]" (menu `0x1c`) before
anything is entered (manual p.12). The step's time is already added. NO = a plain move for both: dungeon NO is DOS `[C]`
(`0x1e47` returns 2); town NO follows the manual (user), the DOS NO branch at `0x6115` is not traced. Core:
`GameSession.EntryPrompt` / `AnswerOverworldEntry`, kept in saves.

Fog note [C]: the target is un-fogged before it is read — in-grid step `0x5f1f` → `0x564c` → `0x53c5(nx,ny)` →
`0x53a5` subtracts 120, then the reveal `0x566b` (skips the centre), then the read `0x538b` at `0x5f3b`; an edge crossing
(`0x5dce` → `0x530d` → `0x547b` → `0x53c5`) does the same for the arrival cell. Wall check, cost and dispatch all see the
decoded tile (a fogged wall 220 still blocks). Core matches.

### Travel time [C] (s36)
The travel-time word `[0x63f2]` has exactly three adders, all calls to OV2 `0x6528` (byte sweep of OV1, OV2 and
the unpacked COM for `f2 63`/`f3 63`):

| Caller | When | Adds |
|---|---|---|
| `0x5f94` | accepted wilderness step, `tile / 10 > 3` | **60** |
| `0x5fa4` | accepted wilderness step, `tile / 10 ≤ 3` | **`(tile/10 + 1) · 75`** = 75 / 150 / 225 / 300 |
| `0x682b` | wilderness inn stay | **1000** (remaster: D-008, see `town-menus.md`) |

- Every accepted step counts, including the step onto a town, dungeon, inn, text or encounter tile, and the step that
  crosses a region edge. A blocked step (tile 100) adds nothing.
- **Dungeon steps, combat, spells, potions and the town Inn add nothing** (no other caller, no other writer).
- `0x6528`: `ax = n + [0x63f2]` in 16 bits, then a long compare against `0xffff` that can never be true (`dx` was
  zeroed). So the "saturate at `0xffff`" is dead code: the counter **wraps mod 65536** [C quirk]. Reached only after
  ≥ 219 mountain steps without a town.
- Readers: OV2 `0x46c0` prints **`DAY OUT %u` = `[0x63f2] >> 8`** on the party summary screen (with
  `[original text]` and `GOLD PIECES`), so **256 units = one displayed day**. OV1 `0x1995` converts it to age on
  town entry (`town-menus.md` §Aging: `@264 += (time >> 8) / 7`, i.e. one age unit = 2 weeks per 7 displayed days).
- Zeroed by town entry (OV1 `0x19d0`) and by the party-defeat reset (OV1 `0x467`, `endgame.md`). No new-game writer
  exists: the C startup (unpacked COM entry `0x4ee5`, `0x4f94..0x4fa6`) zero-fills DS `0x3244..0xb8f0`, so it starts at 0 [C]; it is not in `SACK.DAT`/`GUILD.DAT`. Dungeon save (`DNG.SAV`) keeping it: `[?]`.
- **Terrain per class (`map-tile-graphics.md`).** Step cost code [C] (`0x5f88..0x5fa7`): `class = tile / 10`;
  `class ≤ 3` → `(class + 1)·75`, else **60**. The picture is chosen by `tile / 10` only [C]. The code knows only the
  class number (no terrain name table in COM/OV1/OV2). Names, with cost: class 0 dotted open land/grassland (75),
  1 forest (150), 2 hills (225), 3 mountains (300), 6 **path** (60), tile 74 bridge (60), 8/9 magenta sand/dunes =
  desert? (60, weak), water 10x (60). So path costs the same as desert (8/9?), water and every class ≥ 4, and less only
  than classes 0..3. Core costs are by class; `MapTileTests.StepCost_ByTileClass` pins them.
  **Names from the game's own text [C data] (location-text records, §Location text):** OUT1 (13,15) is tile **66**
  (class 6) and its text reads "[original text] / [original text]" → **class 6 = path** (black,
  one-cell-wide roads joining towns, inns and signs: OUT1, OUT8; OUT4 inn + sign). "[original text]
  HILLSIDE" sits on the class-2 ring (tiles 20/26) round DNG1 at OUT1 (9,8) → **class 2 = hills**. "A MAGNIFICENT
  GATEWAY IS SET [original text]" sits on the class-3 ring (35/36) round DNG9 at OUT2 (21,7) → **class 3 =
  mountains**. Class 0 (grassland/open land) and 1 (forest) stay `[~]` (no text names them; the manual pairs them as
  "Grassland/Forest"). Tile 74 = bridge and 8/9 = desert stay `[~]`; tile 74 is class 7 (the one-per-trip WARRIOR
  fight) and not water, so the party crosses OUT8's river there without swimming. Class 0 is not the path (an older
  guess from the encounter mix).
- **Manual ↔ DOS: D-055** (manual p.25: path "takes less time relative to the other types of terrain"; DOS path = 60,
  equal to water, 8/9 and tile 74). Decided: manual (`travel-time.json` path 30; option `p1.path-time`). Desert ("Easy
  travelling") at 60 agrees if 8/9 are desert.

### Encounters [C]
After an ordinary successful step (not town/dungeon/text/scripted/ocean/special, not `tile % 10 == 8`):

```
roll = floor(rng / 0x148)   # 0..99  (FUN_655B)
if roll < 6: enter wilderness encounter builder (FUN_70ED)
```

→ **6%** per ordinary step. **No time coupling [C]:** `0x70ed` and the step never read `[0x63f2]`; the
night branch is simply `rand0_99 < 35` inside `0x70ed`, i.e. 35 % of encounters (2.1 % of ordinary steps) are night
encounters whatever the time.

### Swimming — OV2 state 10, `0x685c` [C]
Every accepted step onto a water tile (`tile / 10 == 10`, `0x614e`) returns state 10 before the random-encounter roll.
```
"SWIMMING:"
for each non-DEAD member (party order):
    if rand0_99 < @48 (swim):  "%-10.9sSWIMS"
    else: d := (5·rand0_99 + 100)/100            # 1..5
          0x124a(member, d, 1)                   # CON loss on death (0x12cf)
          "%-10.9sDIES" | "%-10.9sSINKS AND TAKES %-3d DAMAGE"
if no non-DEAD member: return 6                  # party defeat (DOSBox s36: a swim death of the last member = defeat)
[0x64fe] := 1;  return 2                         # [0x64fe]: write-only, see below
```
**`[0x64fe]` has no reader [C].** Its four writers all store 1: swim `0x696d`, pool `0x66f1`, wilderness inn stay
`0x682f`, and the end-of-combat loot routine `0x41fc` (after the treasure gives `0x421d`). Checked: (1) every 16-bit
operand `0x64fc..0x6500` in COM (unpacked), OV1 and OV2 — the only hits are these four `mov word [0x64fe], 1` and the
state word `[0x64fc]`; (2) indexed operands `[reg + 0x64c0..0x64fd]` — only the group array `[bx + 0x64e0]` (index =
group number, ≤ 3 groups, so it never reaches `0x64fe`); (3) no immediate `0x64fx` pointer; (4) block I/O — every caller of the
read/write helpers (COM `0x1b36`/`0x1b6d`; OV1/OV2 `0xc75a`/`0xc791`): SACK block OV1 `0x4591` (`0x78` bytes from
`0x63fa` → ends at `0x6471`), region save OV2 `0x535d` (`0x400` from `0xc6fa`), DNG.SAV OV2 `0x59a8` (`0x9c4` from
`[0x6504]` = `0xc6fa`, set at `0x57c7`), GUILD/TWNS from heap blocks at `0x75cd`+ (COM `0x1e08`); COM's own reads
(`0x19f2`, `0x1a04`, `0x27ac`) fill `0x3244..0x5b96`. None covers `0x64fe`. So the flag is set and never read or saved — dead in DOS;
Core needs nothing.
Manual (PDF p.13): "CROSSING WATER … the party members must swim or cross a bridge. Characters with poor swimming abilities
may sustain damage", and the terrain key (PDF p.25): "River/Lake. You must swim … You will encounter no one, and no monsters will
attack you." Both match (no encounter roll on water). The manual does not say swimming can kill. DOS can kill (1..5 per
step through `0x124a`), which the manual's "sustain damage" does not rule out — no conflict.
Core: `Swimming.Resolve`, `OverworldMoveKind.Swim`, `GameSession` → `PartyDefeatPending` when nobody is left.
**[D✓] s43:** 107 live steps: per-member swim rates and the uniform 1..5 damage match, and the Health report equals the shown
damage (no hidden loss). rango (last of five rolls) swam 63.6 % vs skill 52 (z ≈ 2.4, `capture_s43` §3) is chance **[C] s58**: OV2 `0x685c` rolls `rand()/328`
(0..99) `< @48` per living member with no modifier; the DOS RNG (COM-unp `0x39a6`: `seed·125 mod 2796203`, output
`((3·seed)>>8) & 0x7fff`, seed DS `0x308c` = 100001) simulated over 300 k steps gives every member its own skill.

### Night encounter [C] (OV2 `0x70ed..0x71fe`; DOSBox s35 §2)
```
*prepped = 0
if rand0_99 >= 35: → day branches (combat-resolution.md §Surprise, 0x723b)
do r = rand0_99 * 5 / 100 while r in {2, 3}           # r ∈ {0, 1, 4}
monster [0x64e6] = r + 12·(tile % 10) + 1             # Core 0-based id = r + 12·T
[0x6498] = 3 (write-only, no reader in any binary); F = [0x649e] = 1; H = [0x64a0] = 1
print "[original text]"
w = rand0_99 * partySize / 100                        # watch slot, signed
if w.status != DEAD and rand0_99 < w.listen@40:       # listen roll drawn only for a non-DEAD watcher
    print "%s IS ON WATCH" + "[original text]"; return  (caller runs prep 0x3263; nobody sleeps)
prep 0x3263 (every non-DEAD member → OK)
for every party slot: if status == OK: status = SLEEP
*prepped = 1
if w.status != DEAD: w.status = OK; print "%s IS ON WATCH"
return
```
- A DEAD watcher means no listen roll and no message: the whole party sleeps.
- The fight then opens normally (1,1: PARTY OPTIONS); sleepers wake through the usual combat rules (DOSBox s35:
  "wizardo AWAKENS").
- Manual (p.12): "whoever is on watch will hopefully hear the monsters and awaken the other party members.
  Otherwise, party members will wake up in the middle of combat." Agrees — not a conflict.

### Day encounter [C] (OV2 `0x723b..0x74f0`)
`T = [0x649c]` (tile % 10), `C = [0x649a]` (tile / 10). Monster ids below are **0-based** (DOS returns `id + 1`).
```
r = rand0_99
if r > 91:                                                   # 8 %: Black Knights
    if [0x63ea] > 83: print "[original text]"; no fight         # [0x63ea] = max @70 SCORE at town exit
    picture 0x1b; menu 0xf ([original text] / RUN / FIGHT / SURRENDER); F := 0  # 0x7284, before the switch
    RUN:       rand0_99 < 44 → no fight (F stays 0);  else F,H := 1,1, fight 76 (BLACK KNIGHT)
    FIGHT:     H := 1 → (0,1) stalled, fight 76
    SURRENDER: found gold [0x63f6] := 0, no fight (F stays 0) — one purse: bank-withdrawn gold goes too, bank @262 kept ([D✓] s49; Core zeroes Sack.Gold + Party.GoldCarried)
if rand0_99 < 25:  m = rand0_99 / 10 + 12·T + 2              # offsets 2..11, [0x6498] := 4
else:              coin = rand0_99·2/100; b = rand0_99/25     # in this order
                   m = coin + 2·PAIR[C + b] + 12·T            # [0x6498] := 5; PAIR = COM DS 0x1a5c
if rand0_99 < 31:  menu 0x10 ([original text] / ATTACK / LEAVE): ATTACK → F,H := 0,2, fight m; LEAVE → none
listener = last member i with rand0_99·222/100 ≤ @40 and status ≠ DEAD     # one roll per slot, DEAD ones too
if listener:  "%s HEARS MONSTERS"; rand0_99 < 6T+20 → print, F,H := 1,1, fight m
              else menu 0x11 (RUN / FIGHT, titled with the message):
                   FIGHT: rand0_99 < 30 → F,H := 1,1;  else "[original text]", F,H := 0,2;  fight m
                   RUN:   rand0_99 < 80 → none;        else F,H := 1,1, fight m
else:         rand0_99 ≥ 6T+20 → F,H := 1,1, fight m
              q = rand0_99: q ≥ 50 → "[original text]", F,H := 1,0, fight m
                            q < 50 → falls off the end: AX = q → fight monster q − 1 (none if q = 0), no flag write
```
**[D✓] s45 DOSBox** (`capture_s45_2026-10-16.md` §2, §4): every branch's text seen — "[original text]" / RUN FIGHT SURRENDER
(SURRENDER → map, no text); "[original text]" / ATTACK LEAVE (LEAVE ×5 → no fight); "`name` HEARS MONSTERS" / RUN
FIGHT → FIGHT → "[original text]" (0,2 round 1); "[original text]" (monster-first); the fall-off path (no text,
ZOMBIE id 13 on a T = 0 tile, monster-first); "[original text]" → "`name` IS ON WATCH" (or no line for a DEAD watcher).
Every menu remembers its last choice (the sleeping-monsters menu reopened on LEAVE) — per menu id, `message-and-text.md` §Menus.

`PAIR` (16 bytes, `wilderness-encounters.json`) = `4,1,2,3,3,4,2,4,3,5,5,4,4,0,0,1`. Each 12-monster block holds six
pairs: 0 undead/devils, 1 slimes, 2 insects, 3 animals, 4 humanoids, 5 magic users. So by class: 0 → humanoid / slime /
insect / animal (a "variety"), 1 → slime / insect / animal ×2, 2 → insect / animal ×2 / humanoid, 3 → animal ×2 /
humanoid / insect, 4 → animal / humanoid / insect / humanoid, 6 → insect / humanoid / animal / magic, 8 → animal / magic ×2 /
humanoid, 9 → magic ×2 / humanoid ×2 (pair table DS `0x1a5c`, indexes 6..9). Undead appear only at night (night offsets 0, 1, 4) — manual: "Nighttime brings
more insects and undead". The manual's terrain key (path: variety; desert: humanoids and magic users; grassland/forest: insects and slimes;
mountains: wild animals). The tile layout makes class 6 the path (`map-tile-graphics.md`), whose mix insect / humanoid / animal / magic is the manual's "variety";
1 (forest) slime/insect/animal fits; 2/3 (hills/mountains) animals fit; 9 (desert) magic/humanoid fits. No mismatch.
Names (sweep of every game file): only PATH (class 6), HILLSIDE (2) and MOUNTAIN (3) appear in game text; class 0 =
grassland and 1 = forest follow the manual key "Grassland/Forest" + the Amiga/DOS art `[~]`; 8/9 desert `[~]` (art, cost,
mix); no code branches on 8/9 [C] (readers of `[0x649a]`: `0x5f88`, `0x60d4`, `0x614e`, `0x7316`, `0x3d32`/`0x3de7`). Tile
74 = bridge `[~]` strong: its only cell OUT8 (14,11) has water N/S and road E/W, the Amiga picture draws the road across the
river; no code special-cases 74 (generic class 7).

Core: `WildernessEncounter.RollDay` / `Answer`, `GameSession.WildernessPrompt` + `AnswerWildernessPrompt`,
`GameSession.SurpriseGlobals` (stale flags), `GameSession.HighestScore` (`[0x63ea]`, set by `LeaveTown`).
The manual says nothing about the Black Knight menu or surrender in its rules (the story only says the Knights make
people "surrender all their money or die"); DOS zeroes only the found gold, not `@262`.
**[D✓] s49** (`capture_s49_2026-10-23.md` §1c): SURRENDER with 301 GP withdrawn from the bank → town entry shows no split box,
Bank "GOLD CARRIED 0". `[0x63f6]` is the one carried purse, so bank-withdrawn gold is lost too (Core keeps `Party.GoldCarried`).

### Location text — temples, cathedrals, signs [C] (OV2 `0x5fae..0x603c`)
Reached from the subcode switch (`0x607d`: `tile % 10 == 6` → `0x5fae`) before the class switch, so the tile's class
never matters (tiles 6, 16, 26, 36, 66, 86, 106 all occur).
```
f = feature record at the party cell (0x6214); a = f.arg                     # byte +2
page(p) = region buffer [0x6502] + 0x253 + 40·p                             # 40-byte, NUL-padded pages
a < 10:   print page(a)                                  (0xda83, one message line)
a < 20:   print page(a−10), page(a−9)                    (0xdad1, two lines)
else:     print page(a−20), page(a−19), page(a−18)       (0xdb41, three lines; no fourth decade)
return 2                                                  # plain move — no 6 % roll, no other effect
```
The printers are COM routines (`0xda83` is also the one-line "THE RIVER STYX"/"[original text]" blocked message);
the manual: "If no response is required for a message, hit RETURN to erase it". With no feature record `0x6214`
falls off its loop and returns region `+0x253`, so the arg would be the byte at `+0x255` `[~]`; a scan of all 18
region files (working and master disk) finds **no x6 or x7 tile without a record**, so it never happens.

**Temples are text-only sites [C].** Every temple/cathedral cell is an x6 tile; DOS prints the line and nothing else
happens — no service, no fight, no encounter roll, no flag:

| Site | Cell | Tile | Arg → page | Text | Next to |
|---|---|---|---|---|---|
| Small temple | OUT1 (20,5) | 16 | 6 | [original text] | — (it is the new-game / party-defeat wake cell, §New game) |
| Large fortified temple | OUT7 (5,10) | 6 (fogged 126) | 0 | [original text] | dungeon entrance OUT7 (4,10), arg 8 → `DNG8` (the Minotaur God / Dosnebian temple text lives there) |
| Small cathedral | OUT5 (9,16), (8,15), (7,16) | 6 | 5 | [original text] | dungeon entrance OUT5 (8,16), arg 2 → `DNG2` ("SMALL ELEGANT TEMPLE OF MARBLE", "TEMPLE OF THE GELNOR PRIESTS") |

OUT2 and OUT3 also carry the page "[original text]" (page 6), and OUT6/OUT7/OUT8..OUT16 the cathedral
page 5, but **no feature record points at them** — the page blocks were evidently copied between region files.
They are unreachable data. The manual never mentions temples outside the scroll/lore text — **not a conflict**
(the town Temple question is closed separately, `town-menus.md` §Temple).

Core: `OverworldTraversal.LocationTextPages(arg)`, `OverworldMoveResult.LocationTextLines` (1–3 lines,
`LocationText` = them joined); `GameSession` notes each line; the Client shows the joined text. Two- and three-line signs: args 10, 12, 14, 20 on OUT1/2/4/5/10/14/18/19.

### Magical pool [C] (OV2 `0x656b..0x66ff`) — Core `MagicalPool`, capped at the manual race maximum (D-051)
```
window; print "A MAGICAL POOL"
row = 10
for i in 0 .. [0x648e]−1:
    if status@24 == DEAD: continue
    k = 2·T + 1                                   # T = tile % 10: pools 111/112/113 → 3/5/7
    if @90 % k == 0: continue                     # this character already used this pool
    @90 *= k
    s = (5·rand0_99 + 100) / 100                  # 1..5
    gain = isqrt(@10) · @10 / 11                  # @10 = luck (8..13 → 2..3; isqrt = the Newton routine, isqrt(8) = 3, [D✓] s65); isqrt = COM 0x3675
    stat[s] += gain                               # jump table 0x66a9: 1 STR @0, 2 INT @2, 3 DEX @4, 4 CON @6, 5 CHA @8
                                                  # (entry 6 = @10 is unreachable); no cap, no clamp
    print_at(9, row++, "%-10.9sIS CHANGED", name)
wait key; [0x64fe] := 1; return 2
```
**Remaster (D-051, user: manual):** `stat[s] = max(stat, min(stat + gain, raceMod + 8))` (undead: birth race);
`@90` and "IS CHANGED" as DOS even when the gain is cut to 0.
So **`@90` is the pool-visit product** (1 at creation, ×3 / ×5 / ×7 per pool). DNG5 ev20's test `@90 ≥ 100`
(`endgame.md`) means "has bathed in all three pools" (105). There is exactly one tile each of 111, 112, 113.

### New game / town exit [C]
OV1 writes the position `[0x63e4]`/`[0x63e6]` in only two places: the party-defeat reset `0x46a`/`0x470` (inside `0x3bc`,
reached only from `0x5ee`: region 1, x=20, y=5 — x is `[0x751a]`, bound 0..25 at OV2 `0x5eaa`, so the wake cell is OUT1
(20,5), the small temple) and Town Exit `0x3305..0x331e` (TWNS `+0x111/+0x113/+0x115` → region, x, y). A new game
(`0x7dd(1)`, `0x8cb`: town index 0) opens in Pelnor; its first Exit puts the party on OUT1 (14,15), Pelnor's tile. Before
that the globals are 0 (BSS zero-fill). Entering a town preserves coordinates;
leaving restores them (reappear on the town tile).

## Wilderness commands [X]
Move, **CAST SPELL** (non-combat/wilderness + everywhere), **HEALTH**, enter
town/dungeon on the appropriate tile, rest at independent inns (250 GP + tax).

**Remaster keys and screen (D-076 / D-077, user: manual).** Manual keys C cast, H health, I inspect, U use potion,
T time lag, 3 or N north, E east, S south, W west — in the wilderness and the dungeon (same command line + L)OOK,
dungeon only). DOS reads none of these letters (arrows + Return menu only, s65). Arrows / D-pad / numpad also move; QoL numpad
diagonals = two ordinary steps (`Game1.FieldMove`). Screen: map left, party pictures + command list right
(`UiOverlays.DrawFieldSidebar`); QoL status / help lines; F2 = the Amiga map-only screen.

### Options menu `0x14` — dispatch `0x5d14` [C] (static, capstone on OV2; strings imm − `0x433d`)
DOS opens it with **Enter** (menu bar `0x12` Options / Speed / Exit, `0x5c55`; arrows move). Items (labels COM DS
`0x64c`): dungeon **7** = Inspect / Health / Cast spell / Use potion / Look / **±Sound** / Exit; wilderness **6** — COM
`0x05bb` unlinks Look, so index 4 there is ±Sound and the Look row stays blank (COM `0x3243` toggles `[0x308a]`). Look `0x5d48` → `0x1f9f` when
`[bp+4] == 3` (`dungeon-representation.md` §Sight). Every choice returns to the menu; Exit leaves.
- **Inspect → `0x464a`:** box; `DAY OUT%21u` (time `>> 8`), `[original text]%11lu` (`[0x63f4]·10`), `GOLD PIECES%17u`
  (`[0x63f6]`), then per found-item class with a count `%-4.3d%s(S)`: class `(id − 1)/20` over the 60 found-item slots
  `0x63fa` (ids > 180 skipped), names DS `0xbdd3` SHIELD / ARMOR / WEAPON / GOOD WEAPON / MG WEAPON / POTION / SCROLL /
  TREASURE / GOOD TREASURE. Then menu `0x15` "INSPECT MEMBER?" NO / YES (YES = 1); YES → loop "[original text]
  INSPECT?" (picker `0xbd53` mode 7) → COM member view `0xd3da` (`save-file-format.md` §Inspect screens) until cancelled.
- **Health → `0x450c`:** "HEALTH REPORT:" then per slot `< [0x648e]` `%-10.9s%-6.5sHP%4d/%-4dMP%3d/%-3d` = name, status
  name, HP `@22/@20`, MP `@18/@16` (matches s43). The combat PARTY OPTIONS "Party health" is `0x45a0` (adds `A:`).
- **Cast spell → `0x4790`** (`magic-spells.md`).
- **Use potion → `0x43e2`:** "[original text]" (picker `0x5a0a` mode 1), potion pick `0x5b5f` (cancel → out),
  "ON WHOM:" (mode 4); the slot word is **zeroed** (a hole, not removed — Core's compact list removes it `[~]`); HEALING n
  → HP `+n²` capped at max, MAGIC n → MP `+3n` capped at `@16`. `0x43e2` has no other caller: **no potion in combat**
  (orders bar THRUST…PARRY; manual p.10 lists none either).

## Remaster note
`OpenPhantasie.Data` loads 26×20 grids + features + text pages.
`OpenPhantasie.Core.OverworldTraversal` owns movement, fog reveal, feature
dispatch, and the 6% encounter roll (headless-testable). Each result carries
`TimeCost` (`TravelTimeRules`, data `travel-time.json`); `GameSession.TravelTime`
accrues it (persisted in the session snapshot) and town entry converts it
(`Aging.ApplyTownEntry`). Tile 41 → `WildernessInn` (`GameSession.StayAtWildernessInn`,
D-008: +1 age unit, no time). Night encounters → `NightEncounter`; day branches → `WildernessEncounter`; class-7
fight and x7 one-shot fights (stale surprise globals); cross-region wall quirk; the pool (`MagicalPool`) and swimming
(`Swimming`) as above. The session save keeps `[0x6496]`, `[0x63ea]`, the surprise globals, a pending day menu and every
region's raw bytes (so fog persists, as DOS writes `OUT*.DAT` back). Tile art: `map-tile-graphics.md` — keep normalized
tile ids stable.
