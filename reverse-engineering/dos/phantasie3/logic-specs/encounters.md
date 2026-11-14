# Phantasie III — encounters: monster groups, status letters, Greet / Surrender / Mercy / Flee, monsters fleeing

Phase 09 T4 3c.6. Static unless marked live. Source of truth: the III manual (Encounter Menu PDF 7 / pr.9–10,
end of round PDF 8 / pr.11, Monster Reputation Table PDF 9 / pr.13, Monster Status PDF 15 / pr.24, tips PDF 15 /
pr.25). Code: DOS `MAIN.EXE` (addresses `../FORMAT.md` section 1; functions `../FUNCTIONS.md`). Tags: `[C]` read from code /
data bytes, `[~]` probable, `[?]` not decoded. Conflicts go to `DIFFERENCES-III.md`; not decided here.

## 1. Monster groups `[C]`

A fight has at most **two monster groups** (= ranks): rank 1 at `FAR:0xe5b0`, rank 2 at `FAR:0xe63a` (138 B each,
id −1 = no group). `make_encounter(id, status)` (root `0xefd8`) clears both, fills rank 1 from monster `id` and, if
`rnd(100) ≤ byte 28` (next-group chance), fills rank 2 from companion byte 29 or 30 (`rnd(2) & 1`). `fill_group`
(root `0xe3e4`) copies from the 38-B `CREATURE.DAT` record (`../FORMAT.md` section 4.4):

| Group off | Value | From monster byte |
|---|---|---|
| +0x00 | monster id | — |
| +0x02 | count = `rnd(byte 14)` (1..group max) | 14 |
| +0x04 + 2m | HP of member m (m < 5) = byte 25; values > 200 mean `100·b − 20000` (201 → 100 … 255 → 5500) | 25 |
| +0x0e + 2m | per-member **magic points** (specials spend them; the breath / Sleep resist side, `combat-round.md` section 3) | 24 |
| +0x18 + 2m | status of member m (the caller's start status) | — |
| +0x22 | attack power = byte 15 · 10 | 15 |
| +0x24 / +0x26 / +0x28 | damage / armour / defence (s8) | 16 / 17 / 18 |
| +0x2a | level | 19 |
| +0x2c | special chance | 20 |
| +0x2e | undead/demon flag | 33 |
| +0x30 | **reaction** (s8); Charm adds 15 (root `0xc744`) | 26 |
| +0x32 | byte 27 (s8): > 41 → the encounter is this group alone, at most 2 monsters, flag `+0x48` = 1 (a large picture `[~]`) | 27 |
| +0x34 + 2k | 3 specials | 21–23 |
| +0x4c + 10k + 2m | six body areas (`body-areas.md` section 1) | — |

**Monster party strength** `strength()` (root `0xf460`): for both groups, the sum of `level` (+0x2a) over the five
member slots whose **HP word has bit 0 set** (`test byte [hp], 1`) `[C]` code reading. That is "odd HP", not "alive";
it looks like a bug for `HP > 0` `[~]`. Dead members (HP 0) never count, live ones count about half the time. The
value at the start of the fight is kept in `FAR:0x9816` (root `0x1069a`).

## 2. Monster status letters `[C]`

Per-member status (`monster_status_from_body`, root `0xec1a`; table in `body-areas.md` section 3): 0 dead, 1 Okay, 2 Asleep,
3 Down (a leg ≥ Broken), 4 head or torso Broken. The combat list (root `0x110f5` / `0x11297`) prints, per rank: the
count, the name with "(s)" and one letter per member:

| Status | Letter (DS) | Manual (PDF 15 / pr.24) |
|---|---|---|
| 2 Asleep | **S** (`0x6a66`) | S Sleeping |
| 3 Down | **D** (`0x6a68`) | D Down |
| 4 head / torso Broken | **O** (`0x6a6a`) | **U** Unconscious |
| anything else (1 Okay) | **K** (`0x6a6c`) | K Okay |

The unconscious state prints **"O"** (as the characters' "Out"), where the manual's example shows "S U D" —
**D3-021** (display only). "[original text]"
(root `0x1101c` mode 1) shows only "Rank :", the rank, the count and the name.

## 3. Reputation `[~]`

There is no separate reputation field. The manual's Monster Reputation Table (PDF 9 / pr.13: Good / Neutral / Bad)
matches the sign of monster byte 26, the reaction modifier: Good ones are positive (Lesser Wizard 6, Wizard 7,
High Ranger 8), Neutral ones near 0 (Barbarian 0, Wild Cat 0, Gnome Mage 3), Bad ones negative (Skeleton −10, Wight
−11, Demon −8, High Devil −10) `[C]` bytes, `[~]` that the table was made from them. The "Fighting Skill" column is
not a field either (level / combat rating `[~]`). Byte 26 is used by Greet, Mercy and Flee below.

## 4. The Encounter menu `[C]`

Menu text (DS `0x2d66…`): Fight, Threaten (= Accept Surrender), Greet, Mercy, Run away (= Flee), Identify, Party
Health, Sound. `id` below is the **rank-1 monster id**; "reaction" is its byte 26 (Greet reads the monster record,
Mercy / Flee the group copy +0x30, which Charm can raise).

| Option | Success test | Success | Failure |
|---|---|---|---|
| **Greet** (root `0x112e9`) | `id < 44` and `FAR:0xb618` = 0 (no monster hurt yet, section 6) and `rnd(100) < reaction + 40` | "Greetings...": fight ends, outcome 4; no gold, no treasure (XP see below) | "No Response..." |
| **Accept Surrender** (root `0x10dcb` → `0xd171`) | `strength() < start_strength / 2` and `rnd(100) ≤ 50` and `id < 44` | "[original text]": fight ends as a win (outcome 1: gold, XP, treasure) | "No Response..." |
| **Beg for Mercy** (root `0x10e14`) | `rnd(100) < reaction + 60` and `id < 44` | "[original text]": party gold = 0, fight ends, no gold or treasure (XP see below). Found items are **not** touched | "No Response..." |
| **Flee** (root `0x10f30`) | see below | "[original text]" | "[original text]", free-shots flag `FAR:0xb57c` = 1 |

**How an answer is shown** (static, s214) `[C]`: every answer above (success or "No Response...") goes through one
helper, DOS root `0x11376(text)` (called by Greet `0x112e9`, Surrender `0x10dcb`, Mercy `0x10e14`, Flee `0x10f30`):
print the text centred on text row 11 (column (40 − length) / 2 − 1) and wait, between a save and a restore of the
screen area (`0x6f6:0x322` / `0x6f6:0x457` on the same buffer; read from the call pattern, not decoded `[~]`). The wait
is `0:0x413(100)`: a calibrated busy loop scaled by the speed word `0x9f2` (2 doubles it), then `0x2b80`, which by its
loop drains the keyboard buffer `[~]`; no key is asked. Its length in seconds is `[~]` (the loop unit not timed). Only then does the option return:
on a success the fight ends (outcome above) and the rewards follow, on a refusal the round goes on. The Amiga does the
same for Greet (`0x1a5e4`, the line "They greet you!" `0x1a666`): its helper `0x1a676` prints the text centred on row 11
(column (40 − length) / 2 − 1) and waits `0x1bacc(40)`, 40 ticks of 1/50 s scaled by Speed (Normal 0.8 s, Slow 1.6 s,
Fast 0.2 s), no key.

Remaster: `CombatPresenter` holds each III answer line (`AnswerTicks` = 40 at the game's COMBAT SPEED) before the fight
ends or play goes on: on the Amiga field as a field line (row 24 in party blue, the place of the field's other lines,
not row 11 `[~]`), on the remaster's own screens as the combat line (the map's line once the fight is over). Both looks
use the Amiga's 40 ticks, since the unit of DOS's `0x413` wait is not timed `[~]`.

```
flee():
    ok = false
    if rank 1 exists and none of its members has status Okay: ok = true       # all asleep / down / out
    elif FAR:0xb5c2 != 0: ok = true                                           # the party surprised the monsters (section 7)
    elif no party member (elemental excluded) is Okay or Down: ok = false
    elif id >= 44: ok = false
    else: ok = rnd(100) < 70 + reaction − strength()
    if id >= 77: ok = false                                                   # Nikademus ×2, Lord Wood
```

- **Ids 44–79** (Dark Gnome … Lord Wood: 36 of the 80 types) can never be greeted, begged or made to surrender, and
  cannot be fled except when every rank-1 monster is disabled; 77–79 never. Among them the manual lists as **Good**
  High Ranger (59) and Wizard (70), and as **Neutral** All Dark Things (44, 45), Gnome Mage (46), the Lords (47, 57,
  71), All Dragons (54, 66), All Giants (67, 68), King Cobra (52), Forest Elf (55), Leopard (63), Great Troll (69),
  while saying "Trying to greet possibly friendly monsters doesn't cost you anything" and "Beg for Mercy, especially
  if you're not currently carrying much gold" (PDF 15 / pr.25) — **D3-024**.
- Mercy takes **all gold and no items**; the manual says the monsters "take all of the party's gold, some of its new
  items" (PDF 7 / pr.10). The items of the fight are lost anyway (no treasure roll after Mercy), so "new items" may mean
  those `[~]`; not raised separately — listed under D3-024's side effects for the user.
- Accept Surrender "only works after you've killed a percentage of the enemy" (PDF 15 / pr.25): the code's
  percentage is **half the start strength** (with the odd-HP sum of section 1) `[C]`.
- **Rewards after a fight** `[C]`: in the **wilderness** (driver root `0xde54`, `0xe077…0xe0a0`) `combat_rewards`
  (root `0xecde`) runs whenever the party survives, whatever the outcome. It pays, per rank, the monster's gold or XP
  value (bytes 34–35 / 36–37, big-endian; values over 64,000 mean `(v − 64,000) · 1000`) times a per-rank counter
  (`FAR:0xfd22` / `0x27f3:0x0cba`, `[~]` members killed) (root `0xf4d3`). **XP is always paid**; **gold only if
  `FAR:0xb62c` is set**, which Flee, Mercy and Greet clear; treasure (root `0xee93`) needs outcome ≠ 0 and `FAR:0xe598`
  clear (Greet sets it). So after Greet, Mercy or a successful Flee the party keeps the XP for the monsters already
  killed, without gold or items. In a **dungeon** (ovl4 `0x1e5e`) rewards are paid only for outcomes 1–3.
- **Treasure after a fight** (root `0xee93`) `[C]`: items only (gold is `combat_rewards` above), and only from the
  **lead group's monster type**: the record `FAR:0xe6c4` that `make_encounter` (root `0xefd8`) points at the first
  group's monster (`CREATURE.DAT` + 38 · id; the second group uses `FAR:0xe7ba`). Its bytes 31 and 32 are two item ids
  (`monsters.json` `treasure_items`, e.g. Skeleton 66 / 67).

  ```
  if outcome == 0: return                       # caller also skips it after Greet (FAR:0xe598) or a wipe
  a, b = monster[31], monster[32]
  fa = find(a) if a else -1                     # find() = root 0xe53a(skill 6, -1)
  fb = find(b) if b else -1
  if fa >= 0 and fb < 0: fb = fa                # item b falls back to a's finder
  if a and fa >= 0: offer(a, fa)
  if b and fb >= 0: offer(b, fb)

  find(): for m in party order 0..size-1:       # first success wins
      if status(m) != Dead and rnd(100) <= FindItem%(m): return m   # rnd = rand()%100+1; Find Item = +0x38
  return -1
  offer(i, m): "<name> finds <item>" / "[original text]" Yes / No (root 0xf2eb); Yes -> found items (root 0xf421, cap 60)
  ```

  - No presence roll and no gold or treasure-class table (I's random treasure is not used): the same monster type always
    carries the same two items, and each needs one successful Find Item roll in the party.
  - The finder's "not Out" test reads the record of member −1 (`imul [bp+8]` with the −1 argument, root `0xe5c2`), i.e.
    `FAR:0x15c`, so Out (and Asleep / Down) members can find; only Dead ones are skipped `[~]` (the byte there was not checked).
  - Dungeon fights (ovl4 `0x1e69`) run the same routine for outcomes 1–3, with the lead monster set by ovl4 `0x1da0`.
  - **The offer screen** (root `0xf2eb`, static) `[C]`: one window, the finder's name at column 4 row 10, DS `0x68ba`
    "finds" after it, the item name after that (no full stop), DS `0x68c0` "[original text]" at column 6 row
    12, then the menu "Yes" (DS `0x68db`, column 12) / "No" (DS `0x68d8`, column 25) on row 14.
  - **Default answer** (static) `[C]`: the two-item menu root `0x75a3` gets highlight 1 from `0xf2eb` on every call
    (`[bp+0x16]`), so "Yes" is lit each time, with no memory of the last answer. It returns 1 (keep) for `Y` or Return
    on "Yes", 0 for `N`, Esc, or Return on "No"; Left / Right move the bar.
  - Remaster: `Phantasie3Campaign.RollTreasure` / `FindTreasure` (the `ICampaign.RollTreasure` hook in place of I's
    `TreasureDrop`); Greet, Mercy, Flee and Teleportation forfeit it (outcome 0 / the greet flag). The offer carries its
    finder (`TreasureOffer`, saved as `PendingTreasureFinders`) and the prompt shows DOS's two lines, opens on "Yes"
    every time (`SessionPrompt.Remembers` = false; I's KEEP IT? menu `0x1b` keeps its memory) and takes Esc as "No"
    (`SessionPrompt.EscapeChoice` = 2; the Client gives Esc to the prompt before the game menu). III's dungeon
    "Keep <item>?" (ovl4 op 6, its own text) uses the same menu with the same highlight `[C]` (`dungeon-events.md` section 3,
    op 6), so both III keep questions open on "Yes" every time and take Esc as "No".

## 5. End of round: monsters flee `[C]`

After each round (combat_round root `0xd8c7`, `0xdb96…0xdc22`) the same test as Accept Surrender (`0xd171`) runs; if
it passes, "[original text]" and the fight ends **as a win** (outcome 1, the same code as "all monsters dead"),
so gold / XP / treasure for the fight are awarded as for a win (section 4, rewards) `[~]`. The manual: "If they are losing
badly, they may attempt to flee" (PDF 8 / pr.11) — agrees ("losing badly" = below half strength, 50 %).

Fight outcomes (`FAR:0xe59c`, returned by combat_main root `0x104c0`): 0 fled / begged / teleported away, 1 won
(killed, surrendered, monsters fled), 2 Nikademus (77 or 78) killed, 3 Lord Wood (79) killed, 4 greeted `[C]`;
`FAR:0xfc9a` = 1 when the whole party died. The 2 / 3 outcomes feed the endgame (`endgame.md`).

## 6. Open

- The odd-HP strength (bug or intent) — a DOSBox check could confirm: a group that is clearly winning should still
  surrender now and then.
  s110 (`../screens-s110/`) is consistent with the odd-HP reading: three Skeletons poked to HP 200 / 200 / 200 (start
  strength 3) took hits to 184 / 194 / 200 (all even, strength 0) and fled at the end of that round `[~]` (one fight).
- Decoded: `FAR:0xb618` (blocks Greet) = "a party member has hurt a monster this fight": set by `damage_monster`
  (root `0xb191`), cleared at every fight start (root `0x1055c`, ovl4 `0x1d82`) and by Charm (root `0xc758`, which also
  adds 15 to both groups' reaction) `[C]`. `FAR:0xb5c2` (free flee) = the party surprised the monsters: section 7 `[C]`.

## 7. The wilderness encounter (s83, static) `[C]`

Root `0xde54` (`wilderness_encounter`), called by the wilderness loop (ovl3 `0x239`) after a step that found no place,
on any terrain but 10 / 8 / 15 (water, lava, Styx), when the party moved, is not wiped and `FAR:0xb62e` is clear. Every
`rnd(n)` is `rand() % n + 1` (root `0x2c31`). Strings are DS literals (quoted). Tables: `rule-tables.json`
`wilderness_encounters` (`extract_dos_p3_rules.py`).

```
if rnd(100) >= 8: no encounter                                    # 7 % per step
b578 = dd8e = b5c2 = 0
L = area_level(x, y) = DS:0x9fe[x / 10 + 5 * (y / 15)]           # root 0x1198; 25 sections of 10 x 15 (s8)
if rnd(100) < 38:                                                 # the night encounter (root 0xee36)
    id = pick(1, L); make_encounter(id, 1)
    every member not Dead (0) or Out (3) → Asleep (2)             # root 0xf239
    w = a random living member (root 0x1124)
    if w.Listen > rnd(100): wake everyone (root 0x1d04); "[original text] <w> is on watch and hears them!"
    else:                                 "[original text] <w> is on watch"
    → fight, mode 0
else:
    id = pick(rnd(100) < 26 ? 2 : 3, L)
    if rnd(100) < 37:                                             # sleeping monsters
        ask a random living member: "<name> [original text] [original text]"   (root 0xe294(1))
        Yes → make_encounter(id, 2 = asleep); b5c2 = 1 → fight, mode 2;  No → b578 = 1: no fight
    else:
        make_encounter(id, 1)
        heard   = some member (party order, not Dead / Out) passes Listen ≥ rnd(100)      (root 0xe53a(3), 0xe37e)
        noticed = rnd(100) < 8 · L                                                        (root 0xe3b1)
        if noticed and not heard:  rnd(100) < 51 → "[original text]" dd8e = 1 → mode 1; else mode 0
        elif heard and not noticed:
            ask a random living member: "<name> [original text] [original text]"  (root 0xe294(2))
            Fight: rnd(100) < 31 → "[original text]" b5c2 = 1 → mode 2; else mode 0
            Run:   rnd(100) < 81 → b578 = 1: no fight; else mode 0
        else: mode 0
combat_main(mode)                      # mode = 1 when dd8e and not b5c2, 2 when b5c2 and not dd8e, else 0
pay combat_rewards unless wiped; treasure (root 0xee93) unless wiped or greeted
```

`pick(kind, L)` (root `0xf143`): kind 1 `12L + {0, 1, 4}` (`rnd(3) − 1`, 2 → 4); kind 2 `12L + rnd(10) + 1`; kind 3
`12L + DS:0xa17[t + rnd(4)]` (t = the terrain index, `planes.md` section 3; values 0–5 for Scandor's terrain, 17–20 for the
Netherworld's); then an id above 74 or below 0 becomes 11, and 61 becomes 60. Monster ids here are 0-based (the
`CREATURE.DAT` record). Mode 1 = the monsters get a free round before the party chooses (as event op 17, root
`0x1072f`); mode 2 sets the free-flee flag `FAR:0xb5c2` (Flee succeeds outright, section 4) and also gives the party a free first round
(`combat-round.md` section 1). The skill accessor root `0xe617(member, k)` maps k 2 Swim, 3 Listen, 4 Find Trap, 5 Disarm, 6 Find Item, 7 Pick Lock (record `+0x50`, `+0x44`,
`+0x3c`, `+0x40`, `+0x38`, `+0x4c`).

The manual agrees where it speaks: "the watch rolls its Listen %" for night fights (III PDF 8 / pr.11), no encounters
on water (PDF 13 / pr.21); the rates are manual-silent. Not a conflict.

Remaster: `Phantasie3Campaign.World.cs` `RollWildernessEncounter`; the fight build happens after the
questions, so DOS's draw order (the build before the Listen roll) is not kept `[~]`.
