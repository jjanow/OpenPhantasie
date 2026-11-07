# Dungeon Representation, Movement & Scripted Events

Owned by `OV2.OVR` (same overlay as combat). Dungeon levels are the `DNG1..DNG10`
files (`../extracted/dungeons.json`, regen `tools/scripts/extract_maps.py`); the
overlay opens them by name via the `DNG%-d` format string. The live 2500-byte
level buffer can be kept in a one-slot save (`DNG.SAV` = id + map-screen image,
`DNGX.SAV` = the buffer) when the party leaves — §Leaving and re-entering
(s36) and `save-file-format.md`.

## Level file format [C]
- 10 level files, ~1922–1984 bytes each. [C]
- **Walkable grid = first 1254 bytes = 33×38** (`width=0x21`, `height=0x26`).
  Confirmed by OV2:
  - `FUN_0000_20c3`: `cell = buf[y * 0x21 + x]`
  - `FUN_0000_1f05`: bounds `0 ≤ x < 0x21`, `0 ≤ y < 0x26`; requires `cell != 0`
  - Overworld dungeon-1 entrance `dest=(20,0)` lands on `0xd7` (exit/stairs)
- Earlier “≈1740 B grid” note was an upper bound that also covered **side
  tables** packed after the map (stairs/event metadata near `buf+0x4f2` /
  `+0x4f6`). Treat **1254** as the map; bytes after that are side data +
  embedded text (`"A STRANGE PURPLE GAS…"`, `"ENDING SEQUENCE"`). [C]
- Longer scripted narration lives in encrypted `MESS1..MESS10` — see
  `message-and-text.md`. [C]

## Feature-byte legend [C] — `FUN_0000_2249`

| Byte | Kind | Notes |
|---|---|---|
| `0x00` | **Wall** | Impassable (`[original text]` via `FUN_0000_23ae`) |
| `0x01`, `0x07` | Floor | Plain walkable |
| `0x02`–`0x06` | Trap | Trap kind = the byte (`0x2f8e`, §Traps) |
| `0x08`–`0x11` | Wandering encounter | Fight on `rand0_99 ≤ [0x1a5a]`, else message `b + 3` (§Fight cells) |
| `0x12`–`0x7f` | Description (Core: `ScriptLow`) | `0x298b`: MESS message `byte − 0x11` every step, no last-message check (§Description cells, s41) |
| `0x80`–`0x89` | Combat | Forced fight, monster `buf[0x468 + cell]` = `buf[0x4e8 + (cell − 0x80)]` (§Fight cells) |
| `0x8a` | Other | A cleared forced-fight cell (`0x2a26`); nothing happens |
| `0x8b`–`0xb8` | Message | `FUN_0000_23c0` / MESS (`id = byte - 0x76`) |
| `0xb9`–`0xd6` | Lock / ambush | Door kinds 1..15 in two banks (`0x2e03`, §Locks) |
| `0xd7`–`0xd9` | Exit / stairs | Wilderness exit (`FUN_0000_2d46`); DNG1 entrance is `0xd7` |
| `0xda`–`0xdc` | Stairs msg | `[original text] (b-0xd9) TO (b-0xd8)` (`FUN_0000_2951`) |
| `≥ 0xdd` | Scripted event | `FUN_0000_24b2` (gold/teleport/puzzle scripts) |

## Movement model [C]
- On entering a dungeon the wilderness map is replaced by a **scrolling dungeon
  map**; party portraits appear only on an Encounter.
- Grid movement with facing; bumping a wall prints **"[original text]"**
  (`0x7736`). Stairs move between levels:
  **"[original text] %-2d TO %-2d"** (`0x7808`).
- Remaster Core: `DungeonTraversal` (cardinal + face/forward), same axes as the
  file (`+Y` = south).

## Sight — step reveal `0x20e5` and L)OOK `0x1f9f` [C] (s67 static + DOSBox)
The level has **no seen bits**: the map window image is the only record of what was seen (back buffer across menus,
`DNG.SAV` on leaving). Full disassembly: `working/static_s67_look.md`; live: `working/capture_s67_look.md`.

```
STEP onto (x, y), cell c (0x1f05 → 0x20e5(c, x, y, 0)):
    repaint the cell just left
    r = 0 if c < 2 or 0xb9 <= c <= 0xc7 else 1         # 0x01 and lock bank 1: own cell only
    draw every cell of [x−r, x+r] × [y−r, y+r] inside the level; party glyph; then the cell event 0x2249

LOOK (Enter → Options → Look, dungeon only; 0x5d48 → 0x1f9f):
    if c < 2 or 0xb9 <= c <= 0xd6: return               # silently nothing (no text, no beep)
    for each of the 9 directions (dx, dy) in −1..1 × −1..1:
        m = largest k in 1..3 such that cells 1..k along the ray are inside the level and in 0x02..0xb8 (else 0)
        draw the step box (radius 1) around party + m·(dx, dy)
    party glyph; save the screen to the back buffer      # the reveal stays
```
- Open floor → the full **9×9** square; a blocking cell (wall, `0x01`, door, exit, stairs, event) stops the ray but is
  drawn when it touches the last open cell. Diagonals have **no corner check** (DOSBox: the DNG1 hall from (20,13) shows
  row 16 cols 16–24 through the pillar gaps). Note the asymmetry: a step onto `0xc8..0xd6` draws radius 1, Look refuses there.
- **Free:** no time (DAY OUT unchanged over ~20 Looks live), no roll, no cell event, no turn; repeatable. No text, no key
  wait — the Options menu stays open on Look until Exit.
- Manual DUNGEON EXPLORATION ("a larger area … some rooms may be even larger") agrees — not a conflict.
- **Remaster:** `DungeonSight.StepReveal` / `.Look` (Core, `DungeonSightTests`); the Client's **L** key (dungeon only,
  D-076) marks the cells in the auto-map's visited set, and every frame marks the step box the same way.

## Scripted events [C messages]
From the `OV2.OVR` string table, dungeon tiles/scripts can trigger:
- **Locked doors**: "PICKS THE LOCK" / "[original text]" (`0x783d`) —
  resolved by the party's *pick lock* skill.
- **Traps**: "SPOTS A TRAP. DISARM IT ?" / "[original text]" /
  "FAILS"/"SUCCEEDS" / "AVOIDS THE TRAP" / "TAKES %d DAMAGE" (`0x7885`–`0x790f`)
  — *spot trap* then *disarm trap* skills; failure damages a member.
- **Ambush / noise**: "[original text]" (`0x7865`),
  "[original text]" (`0x77d7`), "MONSTERS APPEAR" (`0x77f8`).
- **Environmental**: "[original text]" (`0x7827`), "[original text]"
  (teleport tiles, `0x77c2`), gold gain/loss tiles ("[original text]" /
  "[original text]" `0x778e`/`0x77a7`), "A SPELL IS CAST" trap tiles.
- **Fixed treasure / puzzle scripts** (e.g. Trolkin's castle gate control room,
  the Minotaur-guarded door, the purple-gas puzzle) are encoded in the level
  feature bytes + `MESS` text.

## Fight cells — OV2 `0x2249` [C] (s33 decode, s39 static re-check + Core)

`rand0_99` = OV2 `0x655b`. `[0x64e6]` is the 1-based first monster; Core's monster id is `[0x64e6] − 1` (0-based).
`T = [0x64f0]` = level buffer byte `0x4e7`, copied on entry (`0x1e32`): the level's monster tier (DNG1 0, DNG2 1, DNG3 2,
DNG4–5 3, DNG6–7 4, DNG8–10 5). `[0x64ec]` = dungeon id. **`b` = the cell byte itself** (not its offset in the bank).

```
cells 0x08..0x11 (wandering, 0x2259):
    if rand0_99 ≤ [0x1a5a]:                         # signed jle
        F,H := 1,1                                  # 0x227e, normal opening
        [0x64e6] := rand0_99/33 + b + T − 7         # 0-based: rand/33 + b + T − 8  (rand/33 ∈ 0..3)
        if [0x64ec] == 6 and rand0_99 > 50: [0x64e6] := 0x22      # DNG6: TROLKIN (0-based 33)
        return 11                                   # build
    [0x64f2] := b + 3
    if [0x64fa]: 0x23c0(b + 3)                      # message b + 3
    return 3
cells 0x80..0x89 (forced, 0x2383):
    [0x64e6] := buf[0x468 + b] + 1;  F,H := 1,1;  return 11        # no message, no roll
                                                    # = buf[0x4e8 + (b − 0x80)]: the 10 bytes after T (0x4e7); DNG1 [1..9, 0] (s68)
cells 0x8b..0xb8 (message, 0x22c8):
    [0x64f2] := b − 0x76;  if [0x64fa]: 0x23c0(b − 0x76);  return 3
```
- **Chance `[0x1a5a]`**: 7 on every dungeon entry (state 13, `0x82`). Every encounter build (`0x3316`) does −1, then
  sets it to 1 if it is below 5 (7 → 6 → 5 → 1 → 1 …). A BEG adds 1 (`0x3c0b`). Its only reader is `0x225c`. A raw sweep
  (s39) finds `5a 1a` only at OV2 `0x84`, `0x225e`, `0x3318`, `0x331b`, `0x3324` and `0x3c0d`, and never in OV1 or the
  unpacked COM.
- **`[0x64fa]`** is set to 1 on every step (`0x1f88`). That is its only writer, and it sits right before the only `0x2249`
  call (`0x1f97`). So in play the message branch always runs `[C]` (raw `fa 64`: OV2 `0x1f8a`, `0x226d`, `0x22d0` only).
- **`0x23c0(id)`** shows MESS message `id` (via `0x2418`) unless `id == [0x64f4]`, the last message shown. It remembers
  `id` only when the message was found. `[0x64f4] := 0` on entry (`0x1dc6`) and after a script **teleport, ops 12/13** (`0x27fa`;
  s49 fix — the old note said ops 14/15, but `0x27fa` sits in the `0x27b0` teleport body). So in a
  room of wandering cells the description shows again only after some other message has been shown.
- Data check:
  - DNG1 (T 0) wandering cells 8, 9, 12 and 15 give monsters 0..3, 1..4, 4..7 and 7..10 (from SKELETON on). The s35
    DOSBox run met SKELETON in DNG1.
  - DNG9 `0x80` gives monster 76 = BLACK KNIGHT.
  - The wandering formula adds T once, while the lock ambush (below) adds 12·T. That is a code fact, not a provable error.
- **No message is printed** for a wandering or forced fight in `0x2249`. The old Core stub printed "MONSTERS APPEAR",
  which is the text of script op 17.
- Wandering cells are never rewritten, so they keep firing, also in a restored dungeon.
- **[D✓] s45 DOSBox** (layout B, cell b = 15, 10 entries, `capture_s45_2026-10-16.md` §1): 31 fights / 488 steps; per-phase
  rate 7.2 / 8.9 / 5.1 / 2.4 % vs 8 / 7 / 6 / 2 % (all inside 1 σ); 31/31 opened with PARTY OPTIONS; monsters only COYOTE /
  ORC / KOBOLD / one RANGER (ids 7..10). The data fits; it cannot separate 8/7/6 % from each other.

**End of a dungeon fight** (`combat-resolution.md` §Party options, `[0x74b4]` / `[0x74ac]`):
- `0x2a26` rewrites the cell under the party, unless BEG / GREET / FLEE / TELEPORTATION cleared `[0x74b4]`:
  - `0x80..0x89` → `0x8a`.
  - `0xb9..0xc7` → `0xb9`.
  - `0xc8..0xd6` → `0xc8`.
  - Both bases are door kind 1, an open door (§Locks). **[C]** (was `[~]`).
- Raw `b4 74` (s39): set to 1 at build `0x335a`; cleared at `0x3c46` (BEG), `0x3d14` (GREET), `0x3d74` (FLEE) and
  `0x3f93` (TELEPORTATION); read at `0x3fa1`. CHARM does not touch it.
- After BEG / FLEE the post-combat hook `0x6255` steps back (`0x29a9`).

**Core (s39):**
- `DungeonEncounters`: chance, wandering roll, forced table, `ClearFightCell`.
- `DungeonState.WanderingChance` / `LastMessageId`, and `DungeonMoveResult.MonsterId` / `Surprise`.
- The session builds the fight with those flags and lowers the chance on each build. After the fight it clears the cell
  or steps back (`GameSession.DungeonCells.cs`).
- **CHARM (s49, [C] DOS side):** DOS CHARM (`0x767..0x7a2`) never ends a fight: on success it only adds 15 to Σreaction
  `[0x750c]` and clears `[0x74b6]`. It writes neither `[0x74b4]` nor `[0x74ac]`, so a DOS fight with CHARM in it ends by one
  of the usual routes and the cell follows that route. Raw sweeps: `b4 74` is written only at `0x335a` (1), `0x3c46` BEG,
  `0x3d14` GREET, `0x3d74` FLEE, `0x3f93` TELEPORTATION and read only at `0x3fa1`; `0x2a26` has one caller (`0x3fa6`,
  gated on `[0x64e8] = 3` and `[0x74b4] ≠ 0`). `ba 74` (treasure allowed) has the same five end writers, plus the
  treasure roll's own writes (`0x4124/0x416a/0x41c2`), which do not touch `[0x74b4]`.
  The remaster's CHARM end is D-022 (not D-021, which is FEAR), "like a successful GREETINGS". GREETINGS zeroes `[0x74b4]`
  (`0x3d12`) and leaves `[0x74ac]`, so the cell is **not** cleared and the party does **not** step back. Core does exactly
  that (`SpellEnd.Charmed` → `TreasureForfeited`). Consequence: a CHARMed forced-fight cell (`0x80..0x89`) or lock ambush
  stays armed and fights again on the next step onto it, as after GREETINGS. FEAR's remaster run (D-021) leaves the flag
  set, so the cell clears, like DOS's DISPELL / morale runs.

## Description cells — OV2 `0x298b(b, x, y)` [C] (s41)

Called from `0x236a` for `0x11 < b < 0x80`; the dispatcher then returns 3 (no state change).
```
id := b − 0x11                 # 0x2996
[0x64f6] := id                 # write-only: OV2 writes it at 0x23c9 / 0x24fd / 0x299b, no reader in OV2, OV1, unpacked COM
0x2a95(id)                     # MESS message id: text, and a menu when the record says so; the return value is dropped
```
- **No de-dup.** `0x298b` does not go through `0x23c0`, so it neither checks nor writes `[0x64f4]`, the last message shown
  (raw `f4 64` in OV2: `0x1dc8` entry clear, `0x23e3` check, `0x2408` write, `0x27fc` ops 12/13 (teleport) clear — all `0x23c0`
  or script). `0x2a95` does not touch it either (`0x2a95..0x2d02`). So the text shows on **every** step onto the cell,
  and a message cell with the same id right after it is still suppressed if it was the last one `0x23c0` showed.
- **Never rewritten.** Nothing in `0x298b` writes the cell. It does not write `[0x64f2]` either (the wandering / message
  branches do, `0x2269` / `0x22cc`; the teleport ops 12/13 clear it at `0x27f4`). `[0x64f2]` has no reader in OV2, OV1 or the unpacked COM.
- **Empty slot** (`0x2418` returns 0): `0x2a95` jumps to `0x2cfd`, returns 1, shows nothing.
- **Data (s41 scan of DNG1..10):** 62 cells (DNG8 and DNG9 have none), every one on the first record of a plain message (room descriptions,
  wall writing, DNG7 bleeb hints, DNG4 tavern talk). None points at an empty slot, a continuation record or a menu.
  So the dropped menu choice never matters `[~]`.
- **Core (s41):** `DungeonTraversal.ShowDescription` (the full message as `Log`, no `LastMessageId` change).
- Manual: no rule about these cells (it only says messages appear as the party explores). No conflict.

## Traps — OV2 `0x2f8e(a, x, y)` [C] (s39)

`a` = the trap kind: the cell byte (2..6) for a trap cell, or script op 16's argument (then `x, y` = the event cell).
There is **no difficulty table**. The strings at `0x783d..0x7908` are only messages; `a` drives the odds and the damage.
Party loops run over slots `0..[0x648e]−1` and skip DEAD (`@24 = 0`).
```
spotter := last member with rand0_99 ≤ @36 (spot trap)                       # 0x2fa0, jg skips → "≤"
if spotter:
    menu 0x1f "%s SPOTS A TRAP. DISARM IT ?" (0x31f5)   NO (1) → step back 0x29a9, return 3   # the trap stays
    m := 0x5a0a("[original text]", mode 1)         # lists only status OK (1) members; none / Esc → −1
    m == −1 → step back, return 3
    # Remaster (D-052, user s40, manual): "%s SPOTS A TRAP" + D)ISARM OR L)EAVE; L = NO. OK-only disarmers kept (manual silent).
    if rand0_99 > @38 (disarm trap): sound 0x1d, "%s FAILS" → spring
    else: "%s SUCCEEDS"; cell(x, y) := 7; return 3
spring (0x306f):
    MESS message a + 4 (0x2a95);  "IN THE TRAP:"
    for each non-DEAD member:
        if status == OK and 22·rand0_99 < 10·DEX @4·(a + 3):  "%-10.9sAVOIDS THE TRAP"; next   # SLEEP: no avoid roll
        d := 5·rand0_99·(a − 1)/100                   # a=2: 0..4 … a=6: 0..19
        0x124a(member, d, 1)                          # floor 1, CON loss on death (0x12cf)
        "DIES" | "TAKES %-3d DAMAGE" (d > 0) | "AVOIDS THE TRAP" (d = 0, yet 1 HP is lost)   [C quirk]
    cell(x, y) := 7
    return 0x43ae() == −1 ? 6 : 3                     # nobody left → party defeat
```
- The avoid odds rise with `a` (a=2: `rand < 2.27·DEX`; a=6: `rand < 4.1·DEX`), and so does the damage. Code fact.
- Every trap is one-shot: disarmed or sprung, the cell becomes floor 7. Only NO, or no disarmer, keeps it.
- Manual (PDF p.14): "DISARM TRAP is an option available when traps are spotted. If you want the trap disarmed, you will be asked
  who will do it." PDF p.13 shows "THIEF SPOTS A TRAP / D)ISARM OR L)EAVE". DOS asks YES/NO, then WHO. Only the labels
  differ, and LEAVE = NO (step back). No rule conflict.
- **Core (s39):**
  - `DungeonTraps`: `FindSpotter`, `DisarmSucceeds`, `CanDisarm`, `Spring`.
  - `DungeonTraversal` returns `SpotterIndex` and waits; `ResolveSpottedTrap` answers.
  - Session: `DungeonTrapPrompt` / `AnswerDungeonTrap(int?)`.
  - Script op 16 runs the same flow on the event cell.

## Locks and lock ambushes — OV2 `0x2e03(b, x, y)` [C] (s39)

```
di := b − 0xb8;  si := 0;  if di > 15: si := 15; di −= 15        # two banks: 0xb9..0xc7, 0xc8..0xd6
di == 14 (0xc6 / 0xd5) — locked:
    picker := last non-DEAD member with rand0_99 < @44 (pick lock)
    picker → "%s PICKS THE LOCK"; cell := 0xb9 + si (open); return 3        # the party stays on the door
    none   → "[original text]"; cell := 0xc7 + si (kind 15); step back; return 3
di > 14 (0xc7 / 0xd6) — jammed: "[original text]"; cell := 0xc7 + si; step back; return 3
di < 2  (0xb9 / 0xc8) — open: return 3
di 2..13 — ambush:
    listener := last non-DEAD member with rand0_99 < @40 (listen)
    if listener: menu 0x1f "%s [original text]"   YES (0) → step back, return 3   # no fight
    [0x64e6] := 12·T + di − 1                              # 0-based 12·T + di − 2
    H := rand0_99 ≤ 50 ? 1 : 0;  F := (rand0_99 > 50 and listener) ? 0 : 1;  return 11
```
- **[D✓] s43:** 120 live pick rolls match `rand0_99 < @44`, last success (none 38.3 % vs 38.1 %; per-member shares within
  noise; `capture_s43_2026-10-16.md` §2b). Listen on an ambush 14/38 (spec 47.7 %, small n). The ambush **opening** matches
  too (s44 re-check: 15/24 PARTY OPTIONS vs ≈ 51 % predicted; `combat-resolution.md` §Surprise).
- **A failed pick jams the door for good** in this buffer: kind 15 never rolls again. Only a fresh load of the level
  brings the lock back (one that was not saved, or whose slot another dungeon's save has overwritten). `0xd6` already exists in DNG4/6/7/8 data
  (doors no one can pick). The manual (PDF p.14: "PICK LOCK … automatically attempted … Success depends upon the party
  members' skill levels") does not say whether a failed door can be tried again. No conflict stated; noted for the user.
- A won ambush clears the cell to its open base (`0x2a26`). BEG / FLEE leave it, so the ambush is there again next time.
- s35 DOSBox fixture: DNG1 (19,37) `0xCB` (bank 15, kind 4) became `0xC8` after the fight. This matches. The fight was 7 DRIP SLIME (`capture_s35_2026-10-11.md` §8) = monster 2 = `12·0 + 4 − 2`, and it opened with no PARTY OPTIONS, i.e. H rolled 0 (party surprised, ≈ 49 % when nobody heard): consistent with the formula and the 0-based convention.
- **Core (s39):**
  - `DungeonLocks`: `Decode`, `Classify` → `DungeonDoorKind`, `FindPicker`, `FindListener`, `AmbushMonster`.
  - The traversal returns `ListenerIndex` and waits; `ResolveHeardMonsters` answers.
  - Session: `DungeonLockPrompt` / `AnswerDungeonLock(bool goBack)`.

## Q5 — dungeon reset vs getting unique items again [C] (s39, documented; behaviour unchanged)

Every "unique" dungeon item comes from a script GIVE (op 6) or a SET-MIN rune write. Both sit inside an event that is made
one-shot by its record (`r0 > 0x80` → `0xff`) or by a one-shot op. That state lives **only in the 2500-byte level
buffer**. So:
- **NO at the exit** → nothing is saved. With D-027 (remaster), a slot holding this dungeon is purged. The next entry
  loads the original `DNGn`, so every one-shot event can be collected again (rings, wand, scrolls, Zeus, Lord Wood's
  scrolls). The items already taken stay in the party's packs, so you get duplicates. DOS behaves the same, except that
  an older YES-save of this dungeon would come back instead (D-027).
- **YES at the exit** → the spent records travel with the slot, so re-entry cannot give them again. That holds only until
  a YES in **another** dungeon overwrites the single slot; after that, this dungeon is fresh again on its next entry.
- Party defeat and the Black Lord win leave the slot as it is; the Utility "Reset the game" zeroes it
  (`save-file-format.md`).
- Runes are flags (SET-MIN writes 1), so running a rune event again duplicates nothing.

So unique items **can be duplicated**: leave without saving and come back, or save another dungeon in between. The
remaster PRESERVES this (1:1). A "no re-duplication" fix would be a P3 QoL toggle, after 1:1.

## Leaving and re-entering — the one-slot dungeon save [C] (s36 static; s35 DOSBox for the YES path)

Menu ids below are the ids the code passes. The COM menu table entry for id `n` is at DS `0x20 + 16·n` (title label
word 3, first option word 4; labels DS `0x64c`) — one entry later than the "DS `0x10 + 16·id`" note in
`combat-resolution.md`, which is corrected there.

| menu | title | options (0-based return) |
|---|---|---|
| `0x1c` | [original text] | YES = 0 / NO = 1 |
| `0x1d` | [original text] | YES = 0 / NO = 1 |
| `0x1e` | [original text] | YES = 0 / NO = 1 |

**Exit handler OV2 `0x2d46(b, x, y)`** — cells `0xd7..0xd9` (dispatch `0x2249`):
```
menu 0x1d "[original text]"            # repeats while the menu returns −1
if NO:  return 3                         # stay in the dungeon, standing on the exit cell
menu 0x1e "[original text]" → save
0x21df(x, y)                             # redraw the exit cell's map glyph (screen only)
if save == YES:  0x5929([0x64ec])        # write DNG.SAV + DNGX.SAV (below)
k := b − 0xd7                            # exit record k at buf + 0x4f2 + 3k
[0x64ea] := buf[0x4f2+3k]                # overworld region (OUTn)
[0x63e6] := buf[0x4f3+3k]
[0x63e4] := buf[0x4f4+3k];  [0x751a] := 25 − [0x63e4];  [0x751c] := [0x63e6]
return 2                                 # overworld
```
Exit records `[C data]`: for all ten levels, region `buf[0x4f2]`, x = 25 − `buf[0x4f4]`, y = `buf[0x4f3]` is exactly the
OUT feature that enters that dungeon (DNG1 `1, 8, 16` → OUT1 (9,8); DNG9 `2, 7, 4` → OUT2 (21,7); DNG10 → OUT19 (20,10)).
DNG7's second exit `0xd8` at (16,26) uses record 1 = OUT8 (12,5). **[C] s58:** the exit handler (OV2 `0x2d46`) keeps the raw byte in `[0x63e4]` and puts the live x = 25 − raw in `[0x751a]`
(cell index, feature lookup and every step use `[0x751a]`); the mirrored `[0x63e4]` is never read before the next fresh write
(town exit OV1 `0x3317`, defeat `0x46a`, next step OV2 `0x5f67`) — a latent DOS bug with no effect. Core lands on (25 − raw, y).
**DNGX tail [C] s58:** DNGn is read (2500-byte limit, OV2 `0x1e66`) into a buffer that is not cleared, so bytes past the file
length keep the previous dungeon's data; no shipped script reads past each file's length (highest byte 1651–1929, DNG6 1827 of
1922). Core zero-fills the tail — only a byte-exact DNGX.SAV export can differ, never gameplay.

**Writer OV2 `0x5929(id)` [C]** — the only writer in OV2:
```
create "DNG.SAV" (COM 0xe0ec); write the id word
for r in 0..75: write 42 B of B800:(0x3d2 + 0x50·r), then 42 B of BA00:(0x3d2 + 0x50·r)   # map-screen image
close
0xc791("DNGX.SAV", [0x6504], 0x9c4)       # create + write the 2500-byte level buffer
```

**NO path [C]:** nothing is written. The slot keeps what it had: another dungeon, or **an older save of this same
dungeon**. So leaving with NO after an earlier YES makes the next entry load that older state, not the original and not
the state at leaving. A YES in any other dungeon overwrites the slot.

**Other ways out write nothing [C]:** `0x5929` has exactly one caller (`0x2da8`). Party defeat (state 6), the Black Lord
win (state 7), and any non-exit way back to the surface do not save the dungeon.

**Re-entry — OV2 `0x1d8b` (state 13) → `0x1e47(id)` [C]:**
```
menu 0x1c "[original text]";  NO → return 2
read id, x, y from the OUT feature; [0x64ec] := id
0x57ac(id):  0x570b(id): open DNG.SAV; if missing → 0
                         read word 0; if ≠ id → close, 0
                         read the 76×2×42 image into the back buffer [0x5baa]; close; 1
             if 1: read DNGX.SAV (2500 B) into [0x6504]          → returns 0 ("restored")
             else: clear the map window                         → returns 1
if 0x57ac returned 1: read DNGn (≤ 2500 B) into [0x6504]       # 0x1e60 "DNG%-d", COM 0xc75a
read MESSn (4000 B) into 0x6506 and decrypt (0x1ea6)            # always fresh
draw at (x, y); [0x64f0] := buf[0x4e7]; [0x64e8] := 3
```
So a saved dungeon is restored **only when `DNG.SAV` word 0 equals its id**. **No time value is read anywhere on this
path**, and nothing else alters the saved buffer. A restored level keeps every rewrite exactly (cleared fight cells,
spent events, opened walls). An unsaved level is the original file. Two related facts, recorded without a conclusion
(D-027): the wandering-encounter chance `[0x1a5a]` is reset to 7 on every entry, and wandering cells `0x08..0x11` are
never cleared, so random fights continue in a restored dungeon.

Manual p.14: "LEAVING THE DUNGEON is done through the same door used when the party entered. On leaving, you will be
asked if you want to save its current status. Hit Y or N." — the prompt matches DOS. "The same door used when the party
entered" does not quite: DNG7's `0xd8` exit leads to OUT8 (12,5), where no entrance exists (**D-038 DECIDED s37: DOS** — a manual error; Core reads the exit records). The "Playing Hint" that follows (monsters
"prepared for you… new locks installed and new monsters recruited… if you return soon after you left the monsters you
killed might still be dead") describes a time-based partial reset that the DOS code does not have: **D-027 DECIDED s37: DOS one-slot rule, except a NO purges a slot holding the same dungeon** (no
older save of it can come back; a slot of another dungeon is kept).

## What waits for a key on a dungeon step [C] (s154) — the DOS path's paging

From the call scan in `message-and-text.md` (Text boxes, key waits): one step can show, in Core's log order,
1. a row-24 line (message cell / wandering text, `0x23c0`): no wait, it stays under what follows;
2. centred one-line bands (`0xda83`): [original text], `%s PICKS THE LOCK` / `FAILS` / `SUCCEEDS`, NO ONE CAN PICK THE
   LOCK, the stairs line, script ops 1 / 3 / 7 / 9 / 12 / 14 / 17 — each waits for a key;
3. a MESS text window from row 4 (`0x2a95`: event message, ops 11 / 19, description cells, a trap's MESS a + 4) — waits;
4. a damage report window ("IN THE TRAP:", each op 1–4's "REPORT:") — waits.
A trap: MESS a + 4 (waits) → "IN THE TRAP:" (waits) → defeat or the map. The wand's DESTROY: the banner (waits) → REPORT
(waits) → REPORT (waits). Op 17 → MONSTERS APPEAR (waits) → the fight. Nothing on a step is timed.
Core marks 3 and 4 (`LogWindowKind.Text` / `.Report`, `DungeonMoveResult.Windows`, `GameSession.MapWindowsSince`);
2 is found by text (`dos-screens.json` `map_boxes.dungeon_keys`). Built s154: `P1DosMapPages` (Client), see
`message-and-text.md` Remaster.

## Static audit vs Core — s49 (P0.4 [S])

Capstone pass over the OV2 handlers (file offsets). Op table `0x28f2` re-read: 0 `0x2927`, 1 `0x25b4`, 2/4 `0x25b7`,
3 `0x26e1`, 5 `0x26f4`, 6 `0x2725`, 7 `0x273a`, 8 `0x2742`, 9 `0x276f`, 10 `0x2777`, 11 `0x2796`, 12 `0x27a0`, 13 `0x27b0`,
14 `0x2807`, 15 `0x2817`, 16 `0x282d`, 17 `0x283f`, 18 `0x2868`, 19 `0x28b0` (entry = T − `0x53dc`).

| Handler | Status | Core |
|---|---|---|
| Dispatcher `0x2249` (ranges, return states) | [C] | matches `DungeonLevel.Classify` / `DispatchFeature` |
| Message cells / wandering text `0x23c0` | [C] | **s49:** shows only the first 40-char record on row 24 (`0x2418(id, 0x29)` → `0x2d03`; a record equal to `" "` blanks the line). No length trim, no continuation, no menu. DNG1..10 scan: every id sent here is one record and its 40 chars equal the trimmed text, so nothing differs. Core now logs one line. |
| Description cells `0x298b` | [C] (s41) | matches |
| Event record `0x24b2..0x252c` | [C] | matches (`[0x64f6] := r0 & 0x7f`, write-only) |
| Ops 1–4 damage `0x25b7` | [C] | matches (`rand·a / (10·@10)`, floor 1, "REPORT:", `%-10.9sDIES` / `TAKES %-3d DAMAGE` / name) |
| Op 5 fight `0x26f4` | [C] | matches (`H = rand ≤ 90`, `F = 1`) |
| Op 6 give `0x2725` / `0x2599` | [C] + [D✓] s58 | **s58 fix:** DOS asks "YOU FIND %s. KEEP IT?" at the op (modal `0x421d`) and runs the later ops after the answer — DNG9 wand: -TAKE IT- → "YOU FIND WAND. KEEP IT?" → YES → "[original text]" (`capture_s58_2026-10-25.md` §2.5). The item goes to the found-item list (SACK.DAT held 150 after the next save). Core used to queue the offer and finish the script first; now `DungeonScript.Run` stops at op 6 (`DungeonScriptEnd.OfferItem` + a `DungeonScriptResume`), the session asks KEEP IT? and `AnswerTreasure` resumes the script (`DungeonScript.Resume`). The resume point is saved with the dungeon (`DungeonSnapshot.ScriptResume`), so a save taken at the prompt resumes the same ops after a load. A step away drops it, like an unanswered event menu. |
| Ops 7/8 gold | [C] | matches. `a·256` uses `xchg/cwd`, so `a ≥ 0x80` would sign-extend `[~]`; no op 7/8 in DNG1..10 has `a ≥ 0x80` (max 30) |
| Ops 9/10, 11, 17, 18, 19 | [C] | match |
| **Ops 12/13 teleport `0x27a0/0x27b0`** | [C] | **s49 fix:** after the move the body blanks the message line (`0x2d03(" ")`) and sets `[0x64f2]`, `[0x64f4] := 0` (`0x27f4/0x27fa`). Core had put the `[0x64f4]` reset on ops 14/15; it now resets on 12/13. |
| Ops 14/15 SET-CELL `0x2807/0x2817` | [C] | 14: sound `0x1a` + "[original text]"; then `0x2a02(a, b, c)`. **No** `[0x64f4]` reset (s49). |
| Op 16 trap `0x282d` | [C] | matches (returns `0x2f8e`'s state) |
| End check `0x293d` | [C] | matches |
| Traps `0x2f8e` (spot, disarm, spring `0x306f..0x31f5`) | [C] re-read | matches (report strings are `%-10.9sAVOIDS THE TRAP`, `%-10.9sTAKES %-3dDAMAGE` (no space), `%-10.9sDIES`). **s154:** Core's I text now pads too (`{1,-3}`; s156: the name `{0,-10:.9}` = cut at 9, then padded: `GameText`'s string precision, for imported names over `GuildHall.MaxNameLength`): ids `core.dungeon.x_avoids_the_trap` / `trap_x_takes_x_damage` / `trap_x_dies`, ops 1–4 `core.script.report_x_takes_x_damage` / `report_x_dies` (`%-10.9sTAKES %-3d DAMAGE`), swimming `x_swims` / `x_sinks_and_takes_x_damage` / `swim_x_dies` (OV2 `0x7f69..0x7f82`). II's and III's tables keep their unpadded text (their originals not checked) |
| Locks `0x2e03` | [C] + [D✓] s43 | not re-read |
| Stairs text `0x2951` | [C] + [D✓] s154 | prints `[original text] %-2d TO %-2d` in the centred band (`0xda83`, waits for a key; `message-and-text.md` Text boxes) and returns 3: **no level change** (cells `0xda/0xdb` in DNG5, `0xda` in DNG8). **s49:** Core text now uses the `%-2d` padding. |
| Exit `0x2d46` | [C] (s36) + [D✓] s58 | DOS prints nothing of its own: "[original text]" → YES → "[original text]" → YES or NO → straight to the overworld on the door; the old message line stays on row 24 under the prompts (`capture_s58_2026-10-25.md` §4, §7). **s58 fix:** the remaster-only lines "Leaving the dungeon." (exit step) and "Exited dungeon." (after the answer) are gone. |
| Scripted-event step | [C] | **s49 fix:** Core used to note the level's first embedded text ("[original text]" in DNG1, "ENDING SEQUENCE" elsewhere) on every live event cell. DOS prints only `0x2a95(r0 & 0x7f)`; the stray line is gone. |
| End of fight `0x2a26` (only caller `0x3fa6`) | [C] | matches `ClearFightCell` |

**Every `0x2a95` / `0x23c0` caller** (call scan): `0x2a95` ← `0x2500` (event message), `0x2799` (op 11), `0x28ea`
(TEST message), `0x299f` (description), `0x3079` (trap `a + 4`); `0x23c0` ← `0x2273` (wandering `b + 3`), `0x22d6`
(message cell). These are exactly the sources `extract_dungeon_events.py` `referenced_ids` checks, so its "no
continuation is ever asked for" guard is complete. DNG1 message 7 is record 7 = prefix 1 (an option byte) after message 6
(length 80): the second line of the purple-gas text. It could be reached only by a kind-3 trap in DNG1 (`a + 4 = 7`), and
DNG1 has only kind-2 traps and no op 16. The "THE PARTY" / "MEMBERS CHO" menu is what `0x2a95(7)` *would* draw: a correct
emulation of an unreachable slot, not an extractor bug (s49: extractor rerun, `dungeons.json` unchanged).

**[D✓] s56 DOSBox** (`working/capture_s56_2026-10-25.md`; DNGX.SAV patched so the events sit south of the DNG1 entrance):
op 6 gives are asked one by one mid-script in script order ("YOU FIND SM AXE. KEEP IT?" …), nothing in between; a revisit
shows the chest menu again but the spent gives offer nothing (one-shot, declined items included) — Core's pending-treasure
order and one-shot ops give the same result. Ops 1–4 "REPORT:" lines `%-10.9s` + `TAKES %-3d DAMAGE`, a 0 roll prints the
name alone. Trap: MESS a+4 first (waits for a key), then "IN THE TRAP:" with `TAKES %-3dDAMAGE` / `AVOIDS THE TRAP`;
NO to "DISARM IT ?" steps back silently. Stairs `0xda`: "[original text] 1  TO 2", same level. All match Core.
(s154: "no wait" was wrong: the stairs line is a `0xda83` band that waits for a key, DOSBox-X s154 and the code;
`message-and-text.md` Text boxes.)

**[D✓] s58 DOSBox** (`capture_s58_2026-10-25.md` §4, §5; patched sets `working/snapshots/s58-dng1-ev8/`, `s58-dng7-ev19/`):
- **Four-option spill, DNG7 ev19** (moved to (15,1)): the box "[original text] / THE SAME COLOR
  DISAGREE THEN THEY ARE -" has **four clean menu rows** GREEN/BLUE / RED / BLUE / RED/GREEN — no overflow, nothing garbled
  (`k4`). Option 4 → "YOU FIND RING E. KEEP IT?" (`k6`) → YES, no further text: option 4 runs `r5` = the next record's
  `r0` = script 80 → op 6 give 145, as decoded. Stepping off and back the menu shows again (repeatable record), and
  option 4 now gives **nothing** (`k22`): the give is one-shot. Option 1 → "[original text]" → (16,25), row 24 blank
  (`k25`/`k26`). The menu reopened on option 1 each time (`message-and-text.md` §Menus).
- **Teleport and the message line, DNG1 ev8** (target patched to (20,3), message cells `0x8f` at (20,1) and (20,4)):
  the bedroom line from (20,1) stays on row 24 under "[original text] / HEAVY CURRENT." (`e2`);
  after the key the party is at (20,3) and **row 24 is blank** (`e4`); stepping onto (20,4), a cell with the **same**
  message, **prints it again** (`e5`). DNG9 showed the same blank line after op 12 (WAVE WAND) and op 13 (Filmon). This
  fits the static reading (ops 12/13 blank the line and zero `[0x64f2]`/`[0x64f4]`, so the repeat check lets the same
  message through). Not separated by this run: whether a message cell **always** prints, even without a teleport in
  between `[~]` — the s49 static reading says a repeat of the last message id is skipped; a live check needs two
  same-message cells in a row with no teleport.
- **Remaster text:** the entry and exit paths have no remaster lines any more ("Entered dungeon…", "Leaving the dungeon." / "Exited dungeon." removed
  s58, Exit row above).

**Live checks still open `[D+]`** (state needed):
- One-shot 4-byte op quirk, second visit: DNG8 ev3 (10,20) and ev5 (18,18), visited twice in one load.
- Message-cell repeat without a teleport: two adjacent cells with the same message id, stepped one after the other
  (does the second print, or is it skipped as `[0x64f4]` says?).

## Inputs / outputs
- **Inputs**: current level grid (`DNG*`), party position/facing, party skills
  (pick lock, spot/disarm trap, find item, listen), RNG, `DNG.SAV` flags.
- **Outputs**: movement + reveal, event resolution (damage, gold, teleport,
  combat entry), persistent flags (opened doors, taken loot) to `DNG.SAV`.

## Remaster note
M5 Core wiring: `DungeonLevel` (33×38) + `DungeonTraversal` with feature
dispatch. s39: fight cells, traps and locks follow the DOS rules above (no
approximate tables left; `DungeonRules.cs`, `GameSession.DungeonCells.cs`). s36: the
`ClearedEventKeys` stand-in is replaced by the live 2500-byte buffer + a one-slot
`DungeonSaveSlot` (codec `DungeonSaveCodec` in `OpenPhantasie.Data`); re-entry loads
the slot on an id match; a NO purges a same-dungeon slot (D-027). Exits land on the exit record's tile (D-038). No dungeon-tile art in the
Phase 4 manifest yet — Client rendering waits on assets; Core is headless-tested.
