# Endgame: quest chain, win, party defeat (Astral Plane) — PLAN P0.8

Spec only (s31). It covers the rune and ring quest, the Black Lord fight that wins the game, the Zeus victory
screen, and the party-defeat sequence ("Astral Plane" / "Great Demon") with the dead-character fates.
Address conventions follow `docs/plan/RE-REFERENCE.md` (OV1 string imm = file + `0x5ff7`, OV2 string imm = file + `0x433d`,
jump tables file = T − `0x53dc`, party record `0x5bb0 + i·0x11a`, runtime `+0xNN` = GUILD `@NN`).
`rand` = COM `0x3c96` (0..32767), `rand0_99` = OV2 `0x655b` (0..99).

**Main corrections to older notes**
- "[original text]" / "[original text]" are the **party-defeat
  (lose) sequence**, not the win. The win screen is Zeus (`zeus.ibm`) with the "Well done, my small friends" speech.
- The "victory text" `scrolls.json` id 17 ("Congratulations… recovered The Wand") is **an item you pick up**
  (SCROLL 17 = item 137, on a pedestal in the Fortress). The game does not show it automatically.
- The 5 rune words `@72..@80` are now pinned: **AIR, EARTH, FIRE, WATER, GOD**.

---

## 1. Top-level state codes [C]

OV2's main loop (OV2 `0x0d`) dispatches on `[0x64fc]` through the table OV2 `0xae` (14 entries). States **1, 6
and 7** return to the caller, and the COM main loop then hands over to OV1. OV1's entry (OV1 `0x0e`) dispatches the
same word through the table OV1 `0xdd`:

| `[0x64fc]` | OV2 meaning | OV1 handler |
|---|---|---|
| 1 | back to town | `0xb4` → town loop `0x1e15(0)` |
| 2 / 3 | overworld / dungeon step (`0x4325`) | — |
| 5 / 11 / 12 | combat round / build / end (`combat-resolution.md`) | — |
| **6** | party has no non-DEAD member | **`0x7e` → `0x5ee` → party defeat `0x3bc`** (§4) |
| **7** | **Black Lord killed** | **`0x6d` → Zeus victory `0x4d5`** (§3) |
| 13 | enter a dungeon (`0x1d8b`) | — |

Producers of **6**: end of combat `0x3fb2` (`0x43ae() + 1 == 0`), the monster-turn routine (`0x0d9c`, `0x0f43`,
`0x0ff6`, `0x106e`), the dungeon script damage op (`0x26d8`), traps (`0x31e9`), the step routine (`0x4336`,
`0x43a9`) and the party-options orders (`0x6964`, `0x6a40`). They share one condition: no member is non-DEAD.
Sleepers count as alive, as in Core `PartyWiped`.

The only producer of **7** is the end of combat (OV2 `0x4202..0x4214`):
```
[0x64fe] := 1
if [0x64e6] == 0x49 and [0x74aa] != 0:  return 7        # first monster id (1-based) 73 = BLACK LORD (0-based 72)
return [0x64e8]                                          # back to overworld (2) / dungeon (3)
```
`[0x74aa]` is the "all monsters killed" flag. Build sets it to 0 (`0x3328`). End of combat sets it to 1 when the
living-monster recount is 0 (`0x3f42..0x3f53`, which also prints "MONSTERS KILLED").

**A second win route: DISSOLVE [C].** The Black Lord always fights alone (`monsters.json` index 72: group size
factor 1, next-group chance 0), with `L = 21`. Two combat spells matter (cast table OV2 `0x9d1`, entry 5 = FEAR,
entry 6 = DISSOLVE):
- **FEAR** (`0x80c`): if `rand0_99 < 5·(caster @88 − L) + 50`, then `L −= 6` (`0x834`). It stacks.
- **DISSOLVE** (`0x83c`): if `rand0_99 ≤ 110 − 10·L`, then "[original text]", `[0x74b0] := 2` (combat
  ends), the gold reward is zeroed, the group's XP is paid (`0x8c1`), and **`[0x74aa] := 1`** (`0x087b`). End of combat
  then returns **7**.

**Remaster (s32, D-021):** FEAR now makes ordinary monsters run, but the Black Lord is `never_flees`
(`monster-traits.json`), so against him FEAR only lowers `L` exactly as below — this route is unchanged
(test `BlackLord_FearThreeTimes_ThenDissolve_Wins`).

So DISSOLVE kills the Black Lord once FEAR has lowered `L` far enough:

| Successful FEARs | L | DISSOLVE chance |
|---|---|---|
| 0 or 1 | 21 / 15 | 0 % |
| 2 | 9 | 21 % |
| 3 | 3 | 81 % |
| 4 | −3 | 100 % |

A level-15 caster's FEAR odds against him are 20 % (L 21), then 50 % (L 15), then 80 % (L 9); an UNDEAD level-20
caster's are 45 % / 75 % / 100 %.

Two more rules:
- TELEPORTATION (`[0x74b0] := 1`, `0x7da`) leaves `[0x74aa] = 0`: escaping is not a win.
- The party-wipe test (`→ 6`) runs before the win test, so a mutual kill is a defeat **[C]**.

**Can 0x49 come from anywhere else? No [C]** (every `[0x64e6]` writer checked):
- Dungeon random encounters (`0x229d`): `rand0_99/33 + (b − 7) + [0x64f0]`, with `[0x64f0]` = level byte `+0x4e7`
  (≤ 5) → at most 18.
- Dungeon forced fights `0x80..0x89` (`0x2393`): table `+0x468 + cell` (= `0x4e8 + (cell − 0x80)`) +1. No DNG holds 72 there.
- Lock ambush (`0x2f25`): `12·[0x64f0] + di − 1`. The only `di` that gives 73 is 14, and `di = 14` takes the
  pick-lock branch (`0x2e1f`), which never fights.
- Overworld fixed encounter (`0x6053`, feature `arg + 1`): no `OUT*.DAT` tile-`x7` feature has `arg = 72`.
- Overworld tile class 7 (`0x60b0`): `12·(tile%10) + 10` ≠ 73.
- Random overworld encounter `0x70ed` (only caller `0x6168`, where `k = tile%10 ∈ {0..5, 9}`) returns one of:
  - `r + 12k + 1`, `r ∈ {0,1,4}`
  - `q + 12k + 3`, `q ≤ 9`
  - `c + 2·T + 12k + 1`, `c ≤ 1`, `T` from the table DS `0x1a5c` = `4 1 2 3 3 4 2 4 3 5 5 4 4 0 0 1`
  - the Black Knight toll, 77 (`0x72a7`)

  None of them can be 73.
- Script ops 5 / 17 (`0x26f8`, `0x2857`): only **DNG9 event 11** uses monster 72 (§2.5).

## 2. Dungeon event scripts (the quest is data) [C]

All quest logic lives in the `DNG1..DNG10` event scripts, run by OV2 `0x24b2` (feature bytes `≥ 0xdd`). `DNGn` pairs
with `MESSn` (message slot = `messages.json` slot index). The level buffer is the 2500-byte image of the file
(`[0x6504] = 0xc6fa`, OV2 `0x57c7`).

### 2.1 Format
- **Event record** `k = b − 0xdc` (1..35) at file `0x4f6 + 5k`: `r0 r1 r2 r3 r4`.
  - `r0 == 0xff` → event spent; nothing happens (return 3).
  - Otherwise print message `r0 & 0x7f` (`0x2a95`). It returns 1 for plain text, or the 1-based menu choice `c`.
  - Then **if `r0 > 0x80` the record becomes `0xff`: one-shot, spent on first trigger whatever the choice**
    (`0x2506..0x2511`).
  - The script id is `r[c + 1]`, so `r2` = first/only option, `r3`, `r4`… `r1` is never read; it equals the option
    count in every record checked **[~]**. A 4-option menu reads `r5`, the next record's `r0` (DNG7 event 19 →
    event 20 is a dummy `50 00 00 00 00`, so option 4 runs script 80 = RING E) **[C]**.
- **Script** `s` at file `0x5aa + 2s`: up to **6 ops** (`[bp−0x10] < 6`). Each op is `opByte, a` (2 bytes) or
  `opByte, a, b, c` (4 bytes).
  - `op = opByte % 128`. Bit 7 set = continue with the next op.
  - Bit 6 set (op > 0x3f) = one-shot op: after it runs, the byte becomes `0x80`, or `0` on the last op (`0x2571`).
- Opcode table OV2 `0x28f2` (20 entries; dispatch `cs:[bx+0x7cce]`). Op 6 outside DNG9 (`[0x64ec] != 9`) is
  special-cased before the table (`0x258c`) but does the same give. The 4-byte ops are 12–15, 18 and 19:

| op | handler | effect |
|---|---|---|
| 0 | `0x2927` | no-op |
| 1 / 2 / 3 / 4 | `0x25b4` / `0x25b7` / `0x26e1` / `0x25b7` | **damage every non-DEAD member**: `dmg = rand0_99·a / (10·@10)` via `0x124a`, prints dies / takes / unhurt; **all dead → state 6** (`0x26d8`). 1 first prints "[original text]" (`0x297c`); 3 first plays sound `0x1c` + "A SPELL IS CAST"; the damage is reported under "REPORT:" |
| 5 | `0x26f4` | fight monster `a` (0-based; skipped if `a + 1 ≤ 1`): `[0x64e6] = a + 1`, `[0x64a0] = (rand0_99 ≤ 90)`, `[0x649e] = 1` → state 11 |
| 6 | `0x2725` | give item `a` (`0x421d(a, 0)`): "YOU FIND %s. KEEP IT?" is asked **at this op**, modal; the later ops run after the answer (s56/s58 DOSBox). YES → found-item list `0x63fa` (the sack), not a member |
| 7 / 8 | `0x273a` / `0x2742` | found gold `[0x63f6] += a·256` (16-bit clamp), then `|= 0xff`; 7 prints "[original text]" |
| 9 / 10 | `0x276f` / `0x2777` | found gold `−= a·250` (floor 0); 9 prints "[original text]" |
| 11 | `0x2796` | show message `a` |
| 12 / 13 | `0x27a0` / `0x27b0` | teleport to `(a, b)`; 12 plays sound `0x1e` + "[original text]" |
| 14 / 15 | `0x2807` / `0x2817` | SET-CELL `(x = a, y = b) := c` (`0x2a02`: `buf[y·33 + x]`); 14 first prints "[original text]" |
| 16 | `0x282d` | trap `0x2f8e(a, x, y)` on the event cell; the script returns the trap state (`dungeon-representation.md` §Traps — a sprung or disarmed trap turns the event cell into floor 7) |
| 17 | `0x283f` | "MONSTERS APPEAR": fight monster `a`, `[0x64a0] = 0`, `[0x649e] = 1` → state 11 |
| **18** | `0x2868` | **SET-MIN**: every non-DEAD member with field `a` < `b` gets field `a := b` |
| **19** | `0x28b0` | **TEST**: count members (**DEAD included**) with field `a ≥ b`; if none → push the party back to its previous cell (`0x29a9`), show message `c`, stop the script |

**s36 additions from the disassembly (`0x24b2..0x294e`) [C]:**
- Ops 1–4 compute `dmg = rand0_99·a / (10·LUCK @10)` and call `0x124a(i, dmg, 1)`. Flag 1 only suppresses
  `0x124a`'s own print; `0x124a` still floors the hit at **1**, so a member reported with just a name ("unhurt", `dmg ≤ 0`)
  loses 1 HP **(quirk)**. A death runs `0x12cf` (CON loss, D-009). After the loop, no non-DEAD member → state 6 mid-script.
- Ops 5 / 17 return state 11 at once (later ops don't run). With `a = 0` (`[0x64e6] = 1`) they continue, and the end check
  (`0x293d`: `[0x64e6] > 0` → 11) then fights monster 0 — `a = 0` never occurs in DNG1..10 `[~]` (s36 scan).
- Opcodes ≥ 20 are no-ops (`0x291a`: `cmp ax, 0x14; jae` tail). They occur only in 4th-option spills.
- Teleport (12/13) sets the position **and** the previous cell (`[0x751e]/[0x7520]`) to `(a, b)`; the destination's event
  does not run. TEST's push-back (`0x29a9`) goes to the previous cell, then shows message `c` and stops.
- **One-shot 4-byte op quirk [C]:** a spent one-shot op becomes `0x80`/`0`, a **2-byte** no-op, so on the next run of a
  repeatable record the interpreter reads the old `b` byte as the next op byte (when the spent op had the continue bit).
  In DNG1..10 this matters only for DNG8 ev3 and ev5 (repeatable records, `0xcf`/`0xce` first ops): on a second visit the
  stale `b` is `0x16` (op 22, no-op, no continue), so the rest of the script is skipped. Core reproduces the bytes.
- TEST ids in the data: 42 (score ≥ 60), 43–47 (runes), **55 `@90` ≥ 100** (DNG5 ev20) and **161 race ≥ 14** (DNG8 ev25,
  i.e. ELEMENTAL/UNDEAD only). **`@90` [C] (s39)** is the pool-visit product: creation writes 1 (OV1 `0x0cbe`); the
  wilderness pool `0x656b` skips a member whose `@90` is divisible by `k = 2·(tile % 10) + 1` and otherwise multiplies it
  by `k` (16-bit `imul`). So each pool kind works once per character, and DNG5 ev20 (`@90 ≥ 100`) passes only after
  enough distinct pools (e.g. 3·5·7 = 105). No other reference in COM / OV1 / OV2 (raw `0a 5c` and `[reg+0x5a]` sweeps).
  The pool's effects themselves are P0.3.
- `r1` is never read (confirmed again).

**Message and menu format — OV2 `0x2a95` / `0x2418` [C] (s36):** MESSn is read into `0x6506` and decrypted (`0x1ea6`).
Message `id` = slot word `u16[2(id−1)]`; empty when it equals `u16[2·id]` (→ plain, returns 1). The record is at
`0xe6 + word` (hard-coded text base `0x65ec`): `prefix, 40 chars`. The first prefix is the text length; the text runs on
through records `id+1, …` (one per 40 chars). The **option count is the prefix of record `id+1`, minus 1**; a message
shorter than 40 chars forces it to 2. A menu exists only when `2 ≤ prefix ≤ 20`; each label is the next 11 chars of the
same stream (3 chars when prefix > 7). Return = 1-based choice. Emulated by `tools/scripts/extract_dungeon_events.py` →
`dungeons.json` `levels.*.messages` (golden: DNG1 103 ATTACK/TALK/LEAVE HIM, DNG10 70 PRAY/ATTACK/RUN, DNG7 74 four options,
DNG10 50 plain).
**Continuation records (s41) [C]:** the table holds every non-empty slot, and the later records of a long message
are slots too (prefix = the option byte ≤ 20, not a length). `0x2a95` asked for one shows its leftover text as a
stray menu (DNG1 7, the second line of the purple-gas message 6 → blank line + "THE PARTY" / "MEMBERS CHO"). No cell,
event, script op 11/16/19 or SET-CELL value in DNG1..10 asks for one, so DOS never shows them. The extractor tags them
`continuation_of` and fails if a referenced id is tagged. Orphan starts (DNG1 39, DNG3/8/10 111: a length prefix whose
next record is itself a start) are left as starts.

Field ids go through `0x1ba5` (the OV2 field-id map). The ones the quest uses: **42 = `@70` score**,
**43..47 = `@72..@80` runes**, 55 = `@90`, 161 = `@256` race. (152..160 = carried items `@222..@238` are mapped, but no DNG1..10 script reads them — see §6.1.)

`[0x64a0]` = monster-hold surprise flag H (s33 [C]): see `combat-resolution.md` §Surprise.

### 2.2 Runes — `@72..@80` pinned [C]

| Rune | field id | record | given by (SET-MIN) | requires (TEST) |
|---|---|---|---|---|
| AIR | 43 | `@72` | DNG1 ev16 old man → "TALK TO HIM" (MESS1 106–110, "KEEPER OF THE AIR RUNE") | — |
| EARTH | 44 | `@74` | DNG2 ev31 locked box → "BURN BOX" (MESS2 46–49) | **FIRE** (else "[original text]") |
| FIRE | 45 | `@76` | DNG3 ev11 old man in the orange robe (MESS3 70–74) | — |
| WATER | 46 | `@78` | DNG5 ev17 old man (MESS5 108–110) | — |
| GOD | 47 | `@80` | DNG10 ev7 Zeus → "PRAY TO HIM" (MESS10 73–77) | score ≥ 60, AIR, EARTH, WATER (§2.4) |

These are confirmed three ways:
1. `0x1ba5`: ids 43..47 → `+0x48..+0x50`.
2. The sheet renderer COM `0x2cf8..0x2d5c` (unpacked image) prints name table DS `0x2f96` =
   `" AIR"," EARTH"," FIRE"," WATER"," GOD"` for each non-zero word `@72 + 2k`.
3. The DNG10 test events pair each id with its "TEST OF AIR/FIRE/WATER/EARTH" message.

SET-MIN writes **1** to every non-DEAD member present, so every living member gets the rune. Rune words are
0/1 flags.

**Quirk [C]:** the sheet's `runes:` line is printed only if `@72`, `@76` or `@78` is set (COM `0x2cfb..0x2d0b`).
A member with only EARTH and/or GOD shows no rune line. The Mystic's 4-flag check reads `@72..@78` (the four
elemental runes; `town-menus.md` §Mystic).

### 2.3 Rings and the "test of rings" = Mystic score [C]

There is no op that tests ring items. The only ring gate is **score `@70` ≥ 60** (field 42). The Mystic
writes the score to every party member (`town-menus.md` §Mystic):
`score = ⌊(Σ levels of OK members + 18 per distinct id 141..150 held by OK members + 66 if WAND 150 held + 1) / 3⌋`.
Score 60 needs that sum ≥ 179. So the party must hold enough rings **and visit a Mystic after collecting them**.
The wand only appears after the GOD rune (§2.5), so it can't help.

Where the rings come from (script GIVE ops):

| Ring | Item | Source |
|---|---|---|
| A, B, C | 141–143 | DNG6 ev13 "SMALL GLOWING PEDESTAL" (MESS6 47) |
| D | 144 | DNG7 ev17 red Bleeb riddle, option 2 |
| E | 145 | DNG7 ev19 green Bleeb riddle, option 4 (record spill, §2.1) |
| F | 146 | DNG7 ev21 green Bleeb riddle, option 2 (then teleport) |
| G, H | 147–148 | DNG8 ev16 "TWO RINGS IN A CASE" (an alarm summons guards) |
| I | 149 | **PHANTASIA's Armory [C] (s49)**: `TWNS.DAT` record 7 `stock[149] = 1` (raw byte at `7·291 + 149`) |

With 8 rings (144) a party needs Σ levels ≥ 35. Ring I is not needed.

**Ring I [C] (s49).** The old "not in `TWNS.DAT`" note was wrong. PHANTASIA (TWNS index 7) stocks exactly one item:
RING I ×1.
- The Armory lister (OV1 `0x3e2d`, bound `cmp ax, 0xb5`) lists every slot 1..180, so it is for sale. Price
  `⌊124²·100/500⌋ + 1` = **3076 GP**; V = 124, PHANTASIA price index 100.
- The restock churn only wipes ids 0..119, so the ring stays until bought.
- No other source exists. It is in no DNG script GIVE. Every `0x421d` caller is either script op 6 (unshifted) or
  the combat treasure `[0x74a6]/[0x74a8]` (OV2 `0x396d`/`0x3978`: the first group's monster record `+0x22/+0x23`). No
  `treasure_items` value is within the `id − 1 … id + 3` shift range of 149 (max ≤ 140 is 135; next is 154 → 153..157).
- **PHANTASIA has no town tile.** Its TWNS position, OUT14 (5,11), is a plain tile with no feature
  (`overworld-movement.md`). So the town, and Ring I with it, can only be reached by **TRANSPORTATION** (§2.4).

Lore cross-check (matches, not a rule): scroll 7 says three rings went to J. R. Trolkin, three to the Dosnebian
Clerics, and three were lost. MESS5 85 ("…THREE OF THE NINE [original text] KNIGHTS AND
NIKADEMUS") is Lord Wood's man in DNG5. His script also hands over 4 scrolls (126, 127, 133, 135).

### 2.4 Olympus — DNG10 (the six tests and Zeus) [C]

DNG10's entrance is OUT19 feature (20,10) → start (16,0). OUT18 holds town arg 99 (OLYMPIA) and fixed god/monster encounters.

**Route to OUT18/OUT19 [C] (s49): TRANSPORTATION to the blank town.** This matches MESS5 106, "TO VISIT GODS TELEPORT
TO BLANK TOWN".
1. Town CAST SPELL TRANSPORTATION (OV1 `0x275b`). The MP is spent first. Then the picker runs at **unpacked COM
   `0x16a2`**; the OV1 call target is `0xc2c6`, and `0xc2c6 − 0xac24` gives the unpacked address.
   - The picker prints "[original text]" (DS `0xce8`) and scrolls a cursor `[0x1676]` over **12 entries,
     0..11**. The cursor is kept between casts and reset to 0 if > 11 [C]; it starts at 0 (PELNOR), so the first-ever
     highlight is the first entry [C] (s58). It holds the **absolute town record**, not a screen row. **Remaster:**
     since D-062 drops the current town, the dialog remembers the highlighted town id (updated on every move, so an
     ESC keeps it too); when that town is the current one and so not listed, the highlight goes to the entry that
     took its place (the next town in record order; the first entry after the last) [~].
   - Each entry is printed by `0x1887`. Index 11 prints DS `0xd07` = `" "` instead of the record's name, so the 12th
     entry is a **blank line**: the "blank town".
   - The current town is not excluded, and there is no visited/known check. **Remaster (D-062, user s53, manual "from one
     town to another"):** the current town is left out (`GameSession.TransportDestinations`); the blank 12th entry stays.
   - It is a cursor list, not a typed number (Amiga: the same prompt and list). Arg 99 is only OLYMPIA's OUT18 map-feature code.
   - ESC returns −1 (nothing happens, MP stays spent). Enter returns the cursor.
2. On a pick `t` (0-based), `[0x63e2] := t`, then the full town entry runs: `0x1954(t)` (aging, share split, `@254`
   for all party slots, the restock `0x8e1(t)` of record `t`). t = 11 is **OLYMPIA** (TWNS record 11; stock GEMS
   157/167/177).
3. Town Exit (OV1 `0x3305..0x331e`) copies the record's `+0x111/+0x113/+0x115` into region/x/y. For OLYMPIA that is
   **OUT18 (10,5)**, the tile-40 cell with feature arg 99.
   - OV2 `0x611a` maps arg 99 to `[0x64ee] = 11`; other args are 1-based, `arg − 1`. OV1 `0x4299` does the same.
   - So stepping back onto (10,5) re-enters OLYMPIA.
4. OUT18's N/E/W edges are wall (100). Its **south edge** (row 19, x 3..23 open) crosses to region 18 + 1 = **OUT19**,
   arriving at y = 0. OUT19 row 0 x 3..23 is open, and DNG10's tile-50 entrance is at (20,10).
5. **No other way in [C].**
   - No dungeon exit record points to region 18/19 except DNG10's own (back to OUT19 (20,10)).
   - The only writers of `[0x64ea]` are: town Exit (OV1 `0x3310`), the defeat drop (OV1 `0x485`) and the Zeus victory (OV1 `0x5c6`), both the constant 1, the dungeon
     exit (OV2 `0x2dcc`) and the edge crossing (OV2 `0x5ef8`).
   - Walking in fails:
     - OUT14 → west (+4) → 18 arrives at x = 25, and OUT18 column 25 is all wall.
     - OUT15 → west → 19 arrives at OUT19 (25,11), a wall cell walled in on every side. It is the "on a wall"
       arrival quirk, a dead end; `overworld-movement.md` lists it as one of the 13 cells.
     - OUT19 column 25 / rows 18–19 and column 0..1 are wall.
6. PHANTASIA (record 7, printed by name) is reachable only through the same picker (§2.3).

**Consequence:** without TRANSPORTATION there is no DNG10, so no GOD rune, so no DNG9 gate, so no win. Core had
TRANSPORTATION as a message-only stub until s49 (§8).

Events 1–6 are repeatable (`r0 = 0x32`). Each is "THE DEEP VOICE OF A GOD SPEAKS", then TEST + "PASSED", and
two SET-CELL ops open the next room:

| ev | test | on fail |
|---|---|---|
| 1 | AIR (43) | "FAILED THE TEST OF AIR" |
| 2 | FIRE (45) | … FIRE |
| 3 | **score ≥ 60** (42) | "FAILED THE TEST OF RINGS" |
| 4 | WATER (46) | … WATER |
| 5 | EARTH (44) | … EARTH |
| 6 | none ("PASSED THE TEST OF COMBAT") | — |

Ev7, Zeus (`r0 = 0xc6`, **one-shot**), menu PRAY TO HIM / ATTACK HIM / RUN AWAY:
- **PRAY**: TEST score ≥ 60, TEST AIR, TEST EARTH, TEST WATER (each else "YOU HAVE NOT PASSED THE SIX TESTS OF
  [original text] READY."), then "I AM VERY PROUD OF YOU… I GIVE YOU THE GOD RUNE…",
  SET-MIN GOD := 1. That is exactly 6 ops, the interpreter limit.
- **ATTACK**: fight ZEUS (monster 74).
- **RUN AWAY**: quiet teleport (16,24).

Zeus doesn't test FIRE directly, but EARTH can only be earned with FIRE (§2.2), so the four tests imply all four
elemental runes **[C]**. Nothing records the "combat test". Because ev7 is one-shot, **Zeus can be met once per
load of DNG10**. Praying too early, attacking or running away all spend him (§6, Q6).

### 2.5 Fortress of the Black Knights — DNG9 (gate, Black Lord, wand) [C]

Entrance: OUT2 feature (21,7) → (15,0) (`0xd7` at (15,0)). Cells are `(x, y)`.

1. **GOD-rune gate**, ev16 at (15,2), repeatable: TEST GOD (47). On fail: "[original text]
   EVIL AND FLEE IN TERROR" and the party is pushed back. **[D✓] s63:** a two-line box (break after "OF"), waits for a key,
   party back on (15,1), row 24 blank; repeatable; **one** member with `@80` is enough to pass. On pass: the cell becomes `08` and "…FEEL THE FORCE OF
   EVIL [original text] [original text]"
2. **Gems**, ev3 (one-shot), "TAKE THEM / DESTROY ALL":
   - TAKE: items 177, 172, 167, 178.
   - **DESTROY**: rewrites the four lightning cells (28,20) (28,15) (25,15) (25,8) from ev7 (`op4(50)`) to ev8
     (`op4(10)`), and **(29,8) from ev9 to ev10**.
   - This matches scroll 18 ("[original text] [original text] Magic Gems and The Wand").
   - Gold, ev2: TAKE = 17·256 gold. Throne, ev4: SIT = `op4(9)`, DESTROY opens (28,23).
3. **The Black Lord at (29,8)**:
   - ev9 (gems kept, one-shot): every option → "[original text] DISAPPEARS". Nothing opens.
   - ev10 (gems destroyed, one-shot), option 1: "THE BLACK LORD WAVES HIS WAND… FISSURE… TOSSES HIS WAND TO THE
     NORTH AND RUNS SOUTH". Sets (29,7) and (29,9) to `0x8b` (open).
4. **The wand**, ev12 at (29,2), repeatable, LEAVE / TAKE IT / DESTROY IT:
   - **TAKE**: (29,7) := wall; gives **WAND 150** ("[original text]" on the equipment screen, COM `0x2b2c`);
     (29,2) := ev13; "THE EARTH TREMBLES AGAIN".
   - **DESTROY**: "[original text]", `op4(255)` twice (heavy damage, can wipe the party), (29,2) := `0x8b`.
     **[D✓] s63:** the banner waits for a key, then each op4 is its **own "REPORT:" box** (one line per living member,
     "<name>  TAKES n  DAMAGE" / "<name> DIES"), each waiting for a key; damage is per member (`rand·255/(10·LUCK)`), the dead
     are left out of the second box; the fissure line stays on row 24. The party is not trapped (north and south stay open).
     HP 30 → wipe → the normal defeat sequence (§4).
   - **LEAVE**: closes the box, no text, cell unchanged (the menu returns on the next step on) [D✓] s63.
5. **Trapped with the wand**, ev13 at (29,2), WAVE WAND / BREAK WAND / THINK:
   - WAVE → teleport to (13,14), Filmon's sealed room.
   - BREAK → explodes like DESTROY and reopens (29,2) and (29,7). **[D✓] s63:** same banner + two REPORT boxes as DESTROY;
     (29,2) fires nothing afterwards, (29,7..9) walkable, and the final fight (32,2) and the win still follow. **The wand
     (150) is not taken away** — it stays in the found-item list (SACK.DAT after the save) and on any member who holds one.
   - THINK → nothing: the menu closes, no text, the fissure line stays; the menu returns on the next step on [D✓] s63.
6. **Filmon's room**:
   - ev14 pedestal: gives **item 137 = SCROLL 17** (`scrolls.json` id 17, "Congratulations… recovered The Wand of
     Nikademus…"). Reading SCROLL n shows `scrolls.json` id n **[C]** (§6.1).
   - ev15 Filmon, repeatable, "YOU HAVE THREE OPTIONS": LEAVE CAVE → (15,2); FIGHT LORD → (29,10); THINK OVER → nothing.
7. **The final fight**, ev11 at (32,2) (one-shot): "[original text] JUMPS OUT FROM A
   CREVICE AND ATTACKS YOU". Re-seals (29,9), then **ambush op 17, monster 72 = BLACK LORD** → `[0x64e6] = 0x49`.
   Killing him ends combat with state **7** (§1). The exit `0xd7` at (32,0) is two cells north.

**Reachability [C, from the grid]:** the (29..32, 1..13) block is reached only through (29,7) or (29,9), which
only ev10 opens, or by Filmon's teleport, which needs the wand. So **taking the gems instead of destroying them
leaves the Black Lord unreachable for that load of DNG9.** It is a puzzle the scrolls hint at, but it soft-locks
the visit (§6). Destroying the wand still leaves the way back south open (ev10 already opened (29,9)).

## 3. Win — Zeus victory screen, OV1 `0x4d5` [C]

```
show "zeus.ibm" (0x5bd6)
n := (partySize == 7) ? 6 : partySize                    # the summon slot is skipped
for i in 0..n-1:
    copy party record i → roster slot (@92 − 1)          # 0x7bf(src, dst, 0x11a)
    if status@24 == OK: @210 := 1                        # spells_known[57] = DIVINE I (DOS slot 57, data-tables.md)
play music 0x19
print the 7 lines at OV1 0x5913..0x5a09 (table 0x5905), rows 17..23:
    "  [original text] / [original text] /
     [original text] / [original text] /
     [original text] / [original text] /
     [original text]"
wait for a key; WRITE guild.dat (0x5bdf, 0x183c = 22 × 0x11a bytes from [0x63e0])
    # 0xc791 → COM 0x1e5d: create (AH=3Ch, 0x37b8) + write (0x38e7); the load twin 0xc75a → 0x1e26 opens (AH=3Dh)
[0x64ee] := 0; current town [0x63e2] := 1; [0x64ea] := 1; clear screen; return 1
```
Remaster (s127): Core puts the speech in `GameSession.PendingVictoryScene` (`SessionPromptKind.VictoryScene`, one key);
with the Amiga pack the Client shows the Amiga's screen instead (`final.pic` fading in, the party row, the speech line by
line; `reverse-engineering/amiga/FORMAT.md` "The victory screen").

The OV1 dispatcher then sets `[0x64e8] := 2` and runs the town loop (`0xb4` → `0x1e15(0)`). **The game goes on.
There is no game-over or credits screen**, and `0x4d5` writes no win flag **[C]**.

**Which town [C] (s58 static + DOSBox):** the town loop and the town entry `0x1954` take the **0-based** record index
`[0x64ee]` (`town-menus.md` §Armory restock), so `[0x64ee] := 0` is **PELNOR**. The `[0x63e2] := 1` (OV1 `0x47f`; the
defeat path has the same pair at `0x5b7`/`0x5c0`) fits that word's other uses, where Pelnor is 1: town Exit stores the
record's `+0x111` overworld region there and in `[0x64ea]` (OV1 `0x3309..0x3310`, Pelnor 1), and `0x4299(arg)` reads
record `arg − 1`. It does not pick the town: the entry's restock `0x8e1(t)` overwrites it with `t` = 0 at once (OV1
`0x8e9`). **[D✓] s58 DOSBox**
(`capture_s58_2026-10-25.md` §2.11–12): after Zeus' key, the town-entry share split ("You have: / 65535 gold / 252000 XP /
Shares for:" → "Each share is: / 10922 gold and / 42000 XP.") then the **PELNOR** town bar, cursor on Exit; no game-over
screen. Core: `GameSession.ResolveVictory` → `EnterTown(1)` (Core ids are 1-based, Pelnor = 1); the share split is the
remaster's Inn Distribute (D-037), not a town-entry prompt.

**Quirk [C]:** DIVINE I is set in the in-memory party record *after* that record was copied to the roster. So the
`guild.dat` written here lacks it. It reaches the roster only at a later party → roster copy (Drop, Exit, …) **[~]**.
The manual has no DIVINE I spell (its spell table stops at #54 TRANSPORTATION). The text itself says it is for
Phantasie II.

## 4. Lose — party defeat, Astral Plane, Great Demon, OV1 `0x3bc` [C]

Reached from state 6 (§1). OV1 `0x5ee` sets `[0x64e8] := 2` and calls `0x3bc`:
```
if partySize == 7: partySize := 6
if slot 7 @92 != 0: remove slot 7 (0x1bae(0x624c))      # the summoned member
0x2af:  CGA palette 0; print "[original text]" (0x5b66) on black; read + unpack "cemen.ibm" (0x5b83)
        into buffers A and B, blank the doorway in A (0x2fd..0x378, off screen);
        music 0x17; A to the screen, a ghost per member (0x5fe); print "[original text]" (0x5b8d);
        music 0x14; B (with the face) to the screen, the ghosts again; print "[original text]" (0x5bb2)
anyRevived := 0                                           # [0xc45e]
for i in 0..partySize-1:                                  # every member is DEAD here
    L := level@88;  music 0x15
    if CON@6 == 0:                                fate := DESTROYED              # 0x40d
    elif L > 2 and rand/0x2000 < L − 2:           fate := RESURRECTED            # 0x41a
    elif rand/0x2aab < L:                         fate := UNDEAD                 # 0x437
    else:                                         fate := DESTROYED
0x974: compact the party (keep slots with @92 != 0)
gold/purse [0x63f4] = [0x63f6] = [0x63f2] := 0; found items [0x63fa..+0x78] := 0; [0xc82] := 0
position: [0x63e4] := 20, [0x63e6] := 5, region [0x64ea] := 1, town [0x63e2] := 1, [0x64ee] := 0
return anyRevived ? 2 : 1
```
- **RESURRECTED** (`0x18e`): revive helper `0x16b` sets status `@24 := 1` (OK), `@262 &= 0xff`, HP `@22 := @20`
  (max), anyRevived := 1. Then prints "%s YOU HAVE BEEN RESURRECTED" (format `0x5b48`, word `0x5b3c`).
- **UNDEAD** (`0x113`): race `@256 := 15` (UNDEAD; `@216` keeps the birth race), then the revive helper, then
  "%s [original text]" (`0x5b23`), **level `@88 := 20`**, `@240 := 15` (weapon points).
- **DESTROYED** (`0x1ef`): erases the portrait, prints "%s YOU HAVE BEEN DESTROYED" (`0x5b5c`), stores the record's
  `@92` in the first negative entry of the free-slot list `[0x6472]` (8 words, all −1 at guild load, `0x7ff`), and
  calls `0x1bae` on the party record.
- **`0x1bae(rec)` [C] (s49)** writes exactly three words: `@218` (`+0xda`, in-party flag), `@24` (`+0x18`, status = 0
  DEAD) and `@92` (`+0x5c`, roster id). Nothing else. Its three callers:
  - `0x242`: the DESTROYED member's **party** record. `0x974` then compacts it away (`@92` = 0).
  - `0x3dd`: summon slot 6 (`0x624c`) at defeat, when `[0x62a8]` (its `@92`) ≠ 0.
  - `0x197e`, at the top of **town entry** `0x1954`. It walks `[0x6472]` while an entry is > 0, calls
    `0x1bae(roster base [0x63e0] + (id − 1)·0x11a)`, and sets the entry to −1.
- So DOS **does not erase the roster record**. At the next town entry it only zeroes `@218`/`@24`/`@92` of that
  GUILD.DAT slot, and the name, stats and items stay. This decodes the PLAN P0.8 "DESTROYED keeps the GUILD.DAT record"
  line. Core removes the roster slot; that line stays open for the D-002 roster-cap consequence.

**Odds [C]** (`rand` is uniform 0..32767; `rand/0x2000 ∈ {0..3}`, each 1/4; `rand/0x2aab ∈ {0,1,2}`, each 1/3 to
within 1/32768). CON is lowered at death (OV2 `0x12cf`, D-009), so CON 0 is a real case.

| Level at death | Resurrected | Undead | Destroyed |
|---|---|---|---|
| CON = 0 (any level) | — | — | 100 % |
| 1 | — | 33.3 % | 66.7 % |
| 2 | — | 66.7 % | 33.3 % |
| 3 | 25 % | 75 % | — |
| 4 | 50 % | 50 % | — |
| 5 | 75 % | 25 % | — |
| ≥ 6 (incl. UNDEAD, level 20) | 100 % | — | — |

After the demon:
- **Survivors** (return 2): the OV1 dispatcher (`0x87..0xa7`) puts the party on the overworld at region 1 (OUT1),
  (20,5). That is the OUT1 feature `arg 6` = "[original text]" **[C data, ~ that `[0x64ea]`/
  `[0x63e4..6]` are region/x/y]**. Party gold and found items are lost.
- **Nobody survives** (return 1): back into the town loop (state 1, `0xb4`) with an empty party. State 1 → `0x1e15(0)` →
  the full town entry `0x1954`: aging (no-op, empty party) and the Armory restock of town 1 (s41; Core `EnterTown(1)`).

There is no "game over" either way.

**Timing [C] (s154).** Every line of the sequence is printed by OV1 `0x24c(s)`: clear row 24, print `s` centred on row
24, `0xe81c(500)` = COM `0x3bf8(500)`, clear row 24. `0x3bf8` waits **500 hundredths = 5.00 s** on the DOS clock and **a
key ends the wait early** (the keyboard is flushed before and after; `message-and-text.md` Text boxes). Each line is its
own wait, so a key skips the current line only. Lines: [original text] (`0x2d8`), YOU HAVE ARRIVED AT THE
ASTRAL PLANE (`0x397`), [original text] (`0x3b2`), then one fate line per member (`0x1c8` → `0x24c`;
UNDEAD `0x156` → `0x24c`). Between them: the CEMEN pictures (below, "Pictures"), music
`0x17` / `0x14` and, before each fate, `0x15` (`0xde87` = COM `0x3263`, the sound dispatcher, a no-op with the sound off `[0x308a]` = 0). **s156 (static, unpacked COM) `[C]`: every tune blocks until it ends**: the dispatcher's table (`0x3313`) sends `0x17` to `0x2354` (two sweeps `0x1d09` and a tone), `0x14` to `0x200c` (20 × three tones), `0x15` to `0x523` (a sweep `0x1bd7`); each plays the PC speaker (PIT channel 2, port `0x61`) and waits in a counted busy loop (`0xa4` / `0x1c7a`: duration × `[0x5ba8]`, a CPU-speed scale) before it returns, so the next line waits for the tune. Their lengths are not timed `[?]`. After the last fate: compaction, the bottom rows cleared, no wait. So the sequence is
(3 + party size) × 5 s plus the pictures and tunes. Manual: silent on the timing (no conflict).
**Pictures [C] (s157, static: OV1 `0x2af`/`0x5fe`/`0x6a8`/`0x747`/`0x787`, COM `0x1b9e`/`0x2398`/`0x544`/`0xcf0`..`0xd50`/`0x3abd`).**
- `0x2c7` calls COM `0x3abd(0)` (INT 10h AH 0Bh, BH 1): **CGA palette 0** (black, light green, light red, yellow at high
  intensity) for the whole sequence; the s63 shots' yellow ghosts and text, red face. The screen is cleared and line 1 prints
  on black.
- `CEMEN.IBM` (10030 B) is RLE-packed: COM `0x1b9e` reads it into buffer A, `0x2398` unpacks it with `0x544` (a byte is
  copied; `0, n` = n + 1 zeros) into B and copies B back to A: a 16384-B CGA screen like `PELNOR.IBM`. **The file is the
  temple with the Great Demon's face in the doorway.** COM `0xd50` copies A to B, then OV1 `0x2fd..0x378` blanks the
  doorway **in buffer A only** (x 128..199: `0x302` pokes 0 into even-bank row 8 bytes 32..49 (screen row 16); `0x31f`
  copies 18 bytes even → odd bank at the loop's end offset `0x2b2` (a quirk: screen row 17 takes row 16's x 200..271, its
  doorway stays); `0x336` copies the blank strip into bank rows 9..53 of both banks: screen rows 18..107). **There is no
  wipe on screen**: COM `0xcf0` (A → screen) shows the empty doorway with line 2, COM `0xd10` (B → screen) the face with
  line 3, both whole-screen copies. (Earlier text here called `0x302..0x378` a wipe animation; it is this off-screen blank.)
- The row (`0x5fe(n)`): text rows 19..23 cleared; n = members with status 0 (all of them here); gap = ((320 − 32n)/(n + 1))
  rounded down to 4 px; slot i at x = gap + i·(gap + 32), y 156; `0x6a8` copies the 32 × 32 ghost from DS `0xba1a` (OV1
  file `0x5a23`, 256 B, 8 bytes a row) into each slot.
- Each fate after its tune `0x15` and before its line: RESURRECTED / UNDEAD `0x787` draws the member's figure (COM `0x30ac`,
  DS `0x3b96 + race·256` = `PRT1IBM.PAT` loaded by COM `0x19e3`; race `@256`, so UNDEAD shows figure 15) in the next slot;
  DESTROYED `0x747` clears text columns x/8 − 1 .. x/8 + 4 of rows 19..23. A slot counter `[0xb8fa]` (0 before line 2)
  steps per member either way, so a destroyed member's gap stays.
- Extracted by `tools/scripts/extract_dos_defeat_pictures.py` → `reverse-engineering/dos/extracted/ui/defeat-temple.png`,
  `defeat-demon.png`, `defeat-ghost.png`, `defeat-figures/00..15.png` (palette 0 high).

Remaster (s154, AMIGA SCREENS off, I): the defeat box shows 5.00 s by itself, then the outcome adds one line every
5.00 s and closes 5.00 s after the last; any key (or a click / pad button) ends the current line's wait
(`GameData/phantasie1/dos-screens.json` `defeat`). **s157 (user: the pictures follow the art pack, not the layout
switch):** on this clock the outcome draws one line at a time on the bottom row over pictures:
- **DOS (CGA) pack** (`dos-cga.json` `defeat_pictures` + the optional pictures `defeat.temple` / `defeat.demon` /
  `defeat.ghost` / `defeat.figure.N`, not in `requires`): DOS's own screens as above (empty doorway + ghosts with line 2,
  the face with line 3, each fate before its line, a destroyed slot cleared, OV1's slot places and y 156, yellow text)
  `[C]`. The same with AMIGA SCREENS on: the Amiga's sequence (`defeat_screen`, its door and timing) lives only in the Amiga
  pack and the CGA pack borrows nothing, so this pack always runs DOS's sequence with DOS's pictures.
- **Amiga pack** (the default) on the DOS layout: the Amiga's pictures (`decor.pic`, door, face, ghost, race portraits, its
  row places and pen 5) on DOS's beats: decor with the door shut and the ghosts with line 2, the door gone and the face with
  line 3, each fate before its line `[~]` (the Amiga's 7-s door sink has no place on a 5-s line; no Amiga sounds here).
- A pack with neither set: the timed text panel (s154). Never CGA art unless the player picked the CGA pack.
- The defeat box (line 1) stays the remaster's panel with the step's report above "[original text]" (DOS: the
  line alone on black) `[~]`. The tunes block (above) but their lengths are not timed, so the 5.00-s lines run without them
  `[?]`.

**[D✓] s63 DOSBox (four defeats: three overworld, one DNG1, two of them from the DNG9 wand explosion too):** the screens run
by themselves, **no key at any point**, about 5 s each: black screen with "[original text]" on the bottom line →
the CEMEN picture (a temple doorway) with a yellow ghost per member, "[original text]" → the demon's face
drawn into the doorway, "[original text]" → one fate line per member in party order (a resurrected or
undead ghost turns back into a figure; a destroyed one is erased and its gap stays) → destination. Fates matched the odds
table in every case (CON 0 → destroyed 10/10; L ≥ 5 resurrected; L 2–3 undead; an undead L20 killed again → resurrected).
Survivors wake on OUT1 (20,5) (the small temple), MP not restored, purse/XP/found items (SACK.DAT) zeroed, DAY OUT 0;
nobody left → PELNOR town screen with the cursor on Exit, and Pelnor restocked. GUILD.DAT: destroyed = only
`@24`/`@92`/`@218` zeroed; undead = race 15, level 20, `@240` 15, bank `& 0xff`, HP max; DNG.SAV/DNGX.SAV untouched.

## 5. Manual (1986 SSI) — everything it says about the endgame

Cited by PDF page (`pdftotext -layout`; the printed page numbers are inconsistent on spreads):
- **p.6 (story):** the quest: "Defeating the Black Knights and destroying Nikademus is just the quest… No one
  knows where Nikademus can be found, but you can start with the Black Knights." "It is rumored that new Black
  Knights are spawned in a fortress somewhere in Gelnor." "Astral Plane" appears only in the creation legend (the
  first plane), with the Olympic Plane of Zeus and the River Styx.
- **p.16:** "When they reach 0, the character is dead (although he or she might be brought back to life by a
  high-level priest using a RESURRECT spell)." Also: "one does not wrangle with Black Knights, gods, or other
  high-level beings carelessly."
- **p.22:** "20 scrolls… their contents hold the keys to restoring Gelnor to its former glory." "One hears of… a
  golden ring of incredible workmanship…" "RINGS AND WAND, according to rumors, are described in several scrolls,
  and they are significant to the plight of Gelnor." New game = "old characters will be erased and the dungeons
  reset".
- **p.14:** leaving a dungeon asks whether to save its status (Y/N). "When you enter a dungeon for the second
  time, the monsters may be prepared for you — new locks installed and new monsters recruited. There is a limit
  however, to what can be done with short notice; if you return soon after you left the monsters you killed might
  still be dead."
- **p.17/18:** "A second FEAR spell, for example, can cause the victim to be even more afraid." "FEAR (#46)
  frightens the monsters into running away in terror." "DISSOLVE (#47) causes monsters to slowly dissolve away
  into nothing." (The FEAR effect is a combat-spell question for P0.5, not the endgame. DOS FEAR lowers `L`; it
  does not make monsters run by itself.)
- The manual says **nothing** about runes, the six tests, Zeus' GOD rune, the Black Lord, the win screen, DIVINE I,
  party defeat, the Astral Plane / Great Demon judgement, or UNDEAD characters.

**Manual ↔ DOS:** I found **no direct contradiction**. Two items to log as NOT A CONFLICT, so the user can review
them (§7):
1. Manual p.16 names only the RESURRECT spell as a way back from death. DOS also revives automatically through the
   Great Demon after a party defeat (§4: resurrect / UNDEAD / destroy). The manual is silent, so DOS stands.
2. Manual p.6 frames the quest as "destroying Nikademus". In DOS the game is won by killing the **Black Lord**
   (Nikademus is never met; Zeus: "removed a Great Evil"; scroll 17: Nikademus is "still in full force in other
   places"). This is story framing, not a rule.

One more conflict, **D-027 — DECIDED s37 (DOS, but a NO purges a same-dungeon slot):** p.14 implies a time-based restore ("if you return soon… the
monsters you killed might still be dead"). DOS restores a dungeon only from the single-slot DNG.SAV/DNGX.SAV on an id
match, written only by a YES at the exit, with no time factor anywhere (s36 [C], `dungeon-representation.md`
§Leaving and re-entering).

## 6. Q6 — "a bug that prevents winning" (Apple II rumor)

**Verdict for DOS [C static]:** I found **no defect that makes the game unwinnable**. The win test (§1) is sound,
the Black Lord is reachable, and the rune chain is complete (Zeus' missing FIRE test is covered by EARTH's FIRE
requirement). There are **soft-locks**, all from one-shot events. Each one lasts only until DNG9/DNG10 is reloaded
from the original file:

| Trap | Effect |
|---|---|
| Visit Zeus (DNG10 ev7) before you qualify, or attack him, or run away | Zeus is spent → no GOD rune this load |
| Take the gems (DNG9 ev3) instead of destroying them | ev10 never appears → Black Lord area unreachable |
| Escape the final fight (Flee `rand0_99 < 80 − 5·partySize`, BEG `rand0_99 < Σreaction + 90 = 80` — the Black Lord's reaction is −10, TELEPORTATION) | ev11 is spent → no second Black Lord fight this load |

**Persistence [C read path]:** dungeon entry (`0x1e47` → `0x57ac` → `0x570b`) restores a saved level **only if
`DNG.SAV`'s first word equals this dungeon's id**:
- `DNG.SAV` = id word + the saved map-screen image, 76 rows × 2 CGA banks × 42 B (6386 B ✓).
- `DNGX.SAV` = the 2500-byte level buffer (✓ size).
- Otherwise the original `DNGn` is loaded. There is a single save slot, written only by OV2 `0x5929`, only when
  the player leaves by an exit cell and answers YES to "[original text]" **[C] (s36)**.

So a soft-lock becomes permanent only if the player saves that dungeon's state after the mistake. Even then it
clears once another dungeon's state is saved **[C]**. Leaving with NO keeps whatever the slot held before (it can be an
older save of the same dungeon, which then comes back on the next entry). The manual's time-based wording (p.14, §5)
is not matched (D-027). Remaster: a NO also purges a slot holding this same dungeon, so the older save can't come back.

The Black Lord fight can also be won with FEAR + DISSOLVE (§1), so the third trap is milder than it looks.

### 6.1 SCROLL 17 outside the Fortress, and what scrolls and the wand do [C] (s49)

**FIRE ELEMENTAL's SCROLL 17 is harmless.**
- Monster 53 has treasure `[0, 135]`. The drop `0x421d` shifts it to `135 + 3 − rand0_99/20`, so 137 = SCROLL 17
  20 % of the time.
- The Fortress guard (OV2 `0x4249..0x4281`: no drop if not (`[0x64e8] == 3` and `[0x64ec] == 9`) and id == `0x89`)
  tests the **unshifted** id, so it never fires for 135. This is D-031; Core tests the shifted id, per the user.
- Even when the drop happens, nothing in the game reads item 137:
  - The only `0x89` item compare in OV1/OV2/unpacked COM is that guard (OV2 `0x427c`).
  - Winning is state 7 only (§1: Black Lord killed or DISSOLVED).
  - So a SCROLL 17 found anywhere is lore. It cannot win the game or skip a step, and it changes nothing about the
    endgame.

**Reading a scroll** works only in town: Option → USE ITEM, OV1 `0x30f1`.
- Pick a member (`0x40e5`), then an item `@222 + 2k`.
- Ids outside 101..140 → `0x40a9`, which prints **"UNUSABLE"** (imm `0xc191`).
- Ids 101..120 are potions (their slot is cleared at `0x31ac`).
- Ids **121..140**: `0x45ba(id − 121)` loads `scrolls.dtx` entry n (`0xc6c0`), prints its 20 lines, then asks "Print
  scroll? (y/n)" (DS `0xc220`; Y → printer, `0xe41f` check, "printer not ready" path).
- The scroll is **not consumed**, and no state changes.
- The field USE POTION (OV2 `0x43e2`, list `0x5b5f`) offers only ids 101..120 (`0x5b83..0x5b8b`), so **scrolls can't be
  read outside town**. This matches manual p.12: "Scrolls can only be used (read) in a town."

**The WAND (150) beyond +84 score: nothing [C].** Every compare of an item id against 150/151 was checked:
| Where | What it does |
|---|---|
| OV1 `0x2152..0x2163` + `0x2193` (Mystic) | flag index `id − 141` (9 = wand), +18 per flag, +66 more if flag 9 (`[bp−0xa]`) → wand = 84 before `/3` |
| COM `0x2a6f..0x2a88` (equipment screen) | ids 141..150 → letters `A`..`J`; `0x2b2c` tests the 10th letter and prints "[original text]" (string `0x842b`) |
| OV2 `0x6d01` | `@250 ≥ 150`, unrelated |
| OV2 `0x4b78`, COM `0x3ed`/`0x22a9`/`0x215f` | screen/sound arithmetic (`0x97 − arg` next to `0x140` = 320 px; `0x1d54` parameter block), not item ids |
| OV2 `0x1ceb` | field ids 152..160, not item ids |

- USE ITEM on the wand prints "UNUSABLE". The restock never wipes it (id ≥ 120). DNG9's WAVE / BREAK WAND menu (ev13) is
  a cell event and doesn't check the inventory: no TEST op reads item fields 152..160 in DNG1..10.
- **Manual (p.12)** says USE ITEM lets you "find the unusual properties of an item". DOS answers "UNUSABLE" for every
  non-potion, non-scroll item, rings and wand included. **D-061 DECIDED (user s53): manual** — any other item shows a
  detailed description: its kind, point value (weapon damage / shield and armor protection), strength needed, sale value
  and the remaster's text from `item-descriptions.json` (`ItemRules.Describe`). Potions and scrolls work as in DOS.

The rumor is about the Apple II build. I did not check C64/Amiga (not needed for DOS).

## 7. Open items

- **Closed s49 [C]:** Ring I source (PHANTASIA Armory, §2.3); route to OUT18/OUT19 (TRANSPORTATION → blank 12th entry
  = OLYMPIA → OUT18 (10,5) → south → OUT19, §2.4); `0x1bae` (`@218`/`@24`/`@92` := 0, §4); WAND effects (Mystic + sheet
  label only, §6.1); SCROLL 17 outside the Fortress (lore only, §6.1).
- **[U]** Manual p.12 USE ITEM "find the unusual properties of an item" vs DOS "UNUSABLE" for every non-potion/non-scroll
  (§6.1). Manual p.20 TRANSPORTATION "from one town to another" (manual p.8: 11 initial towns) vs DOS's 12-entry
  picker with a blank-named OLYMPIA and the current town allowed (§2.4) — for the user to classify.
- **[C] s58** Equipment screen `RING:%s` (COM `0x8441`): the buffer is cut to 9 chars before printing (COM-raw `0x2e37`), so the
  wand's `J` never prints (`save-file-format.md` §inventory; Client `InspectDialog` matches).
- FEAR's manual "running away" vs DOS `L −= 6` → P0.5 / D-021.
- **Closed s63 [D✓]:** real party defeats (§4) — fates, OUT1 (20,5) wake, Pelnor restock with no survivor.
- **P0.4 (s36 [C]):** DNG.SAV is a screen image; dungeon persistence is the whole level buffer in DNGX.SAV (writer
  OV2 `0x5929`, `dungeon-representation.md` §Leaving and re-entering). Core replaced `ClearedEventKeys` with the buffer.

## 8. Core implementation (P0.8 [I]) — done s36

Implemented: `DungeonScript` (interpreter on the live 2500-byte buffer), `dungeons.json` messages, runes `@72..@80` +
`@90` in `Character`/`GuildCodec`, `Endgame` (state 7 incl. DISSOLVE, DIVINE I after the GUILD write), `PartyDefeat`
(fates, DOS draw order), session wiring `GameSession.Endgame.cs` (event menus `AnswerDungeonEvent`, `PartyDefeatPending` →
`ResolvePartyDefeat`). Tests `EndgameTests`.
**s49:** town TRANSPORTATION now moves the party. `GameSession.CastSpell` sets `TransportationPending`;
`ChooseTransportDestination(int? townId)` (1..12, null = ESC) runs `EnterTown`. The OUT18 arg 99 → Core town **12**
(OLYMPIA); it was 11 = STARVILLE. Tests `TransportationTests`. The Client has no picker UI yet (P2). Open: the Client has no prompt UI for event menus / defeat yet (P2); party
defeat does not touch Core's pooled `Party.GoldCarried` (DOS `@262 &= 0xff` on revival is applied) `[~]`. Swimming deaths
(`0x68ff`) are in Core since s39 (`Swimming`, overworld state 10; `overworld-movement.md` §Swimming). The original outline:

1. **Data** (`OpenPhantasie.Data`): extract per-level event records + scripts from DNG1..10 (`0x4f6`/`0x5aa`,
   format §2.1) into `dungeons.json` as a script table. Don't hard-code the quest in C#.
2. **Core `DungeonScript` interpreter**: the 20 opcodes in §2.1. It mutates the level copy (cell writes, `0xff`
   record, one-shot op clear) and returns an outcome (`Continue / Combat(monsterId, surprise) / PartyDefeated`).
   Field-id map 42–47 etc. → `Character` properties; add `Runes` (5 flags `@72..@80`) and wire them through
   `GuildCodec`.
3. **Win**: `CombatEncounter` end → if the first monster id is BLACK LORD (0-based 72) and either all monsters died
   or DISSOLVE succeeded (needs FEAR to have lowered `L`, §1) → `GameOutcome.Victory`. Then copy the party to the roster, set DIVINE I
   (DOS slot 57) on OK members after the copy (keep the quirk), and move the party to town 1. Golden test:
   scripted kill → Victory + DIVINE I set + roster unchanged for DIVINE I. Also a FEAR×3 + DISSOLVE → Victory test,
   and a Flee/BEG/TELEPORTATION → no-Victory test.
4. **Lose**: `PartyDefeat.Resolve(party, rng)` per §4, with RNG draws in DOS order (one `rand` for the resurrect
   test only when `CON > 0` and `L > 2`, then one for undead when reached). Fates table test with a scripted
   RNG; reset purse/found items; place survivors at OUT1 (20,5), else the town with an empty party.
5. **Persistence**: one-slot dungeon save (id + level buffer) replaces `ClearedEventKeys`, behind the P0.4 decode.
