# Phantasie II (Atari ST): towns — services, per-town data, TRANSPORTATION (3b.7, manual Q11)

Phase 09 T3, task 3b.7 (`docs/plan/03b-phantasie2-re.md`). This file covers **only what II changes**. I's town spec,
`reverse-engineering/dos/logic-specs/town-menus.md`, is the reference for everything marked "as I". Record layout and
the town list: `../FORMAT.md` §9.5. Data: `src/OpenPhantasie.Data/GameData/phantasie2/towns.json`
(`tools/scripts/build_p2_gamedata.py`).

Conventions:
- Addresses are `PHANT.PRG` image addresses (`../FORMAT.md` §1).
- Party record `i` = `0x25162 + 318·i`, and `@n` = byte offset n of the 318-byte record.
- Town numbers are **1-based** (1 = PIPPACOTT). `[0x25e4e]` = the current town's 292-byte record.
- Tags: `[C]` read in the code or data, `[~]` probable, `[?]` open.

Method: s80 static reading (objdump with fixups, Ghidra 12 headless decompiles of every function named below; xrefs of the
town globals `0x25b94`, `0x25ea0`, `0x24c94`, `0x25e4e`).

## 0. Summary

- **Every town offers the same services**: Guild, Armory, Bank, Inn, Mystic, the gate, plus the Town options menu
  (§1) `[C]`. No service is missing or added in any town, as in I.
- **Towns differ only in data** (§2): name, Armory stock, restock list, price index (prices, training and spell fees),
  the exit position, and a cosmetic banner nudge.
- **One rule is new: TRANSPORTATION** only reaches towns on the same plane. From a Ferronrah town (1–6) it lists the
  other five; from a Netherworld town (7–10) it offers only that town's pair, 7 ↔ 8 or 9 ↔ 10 (§4) `[C]`. → **D2-030**.
- **Town Inn** no longer refills DEAD members (I refills everyone) (§5) `[C]`. → **D2-031**.
- STARVILLE and OLYMPIA (records 10–11, I's leftovers) **cannot be reached** in II (§3) `[C]`.

## 1. The town screen and its services `[C]`

`town_main 0xec36` → `town_loop 0xdb5a`. One loop serves every town; there is no switch on the town number in it.

- **Picture.** Every town shows `PELNOR.PIC` with the town's name in a banner (`0x4082`). The banner's x position gets a
  per-town nudge: 0 for towns 1, 2, 4, 8; −11 for 6; −7 for 5; −4 for 9; 5 for the rest. Cosmetic only.
  - (s102) `0x4082(str, height, mode)` `[C]`: mode > 16 is the banner — vst_color = `[0x25b94]` (town 3 → 11, 9 → 13),
    vst_effects 0, vst_height(`height`), vst_alignment(1 = centre, 5 = top), vswr_mode 2, clip (90, 24, 310, 11), then
    v_justified at x = 320/2 − nudge, y 24, with the vqt_extent width of the **10-char space-padded** `TWNS.DAT` name
    (+280: `"PIPPACOTT "`, `"FERRON    "` …). The nudge cancels the padding: the ink centre is 120 − nudge + 4·len =
    155–156 for every town (len 9 → 0, 10 → 5, 8 → −4, 7 → −7, 6 → −11). **So the name sits at one centre in every
    town**; the remaster centres the trimmed name on one `name_x` (pack `town_screen`), which is the same thing — no
    per-town value to apply. s109 live: the ink spans 121..190 for a 9-letter name and the ink centre is 155–156 in all ten
    towns, as the code computes `[C]`; the pack's `name_x` is 156. On a new game the nudge reads town 0 ("the rest",
    5), so PIPPACOTT's ink spans 116..185, centre 151 — the s78 reading was right for that case (pack `fresh_name_x`
    151, below).
  - (s102) mode ≤ 16 is the **message line** `[C]`: the same call centred at x 159 (nudge 1), y **192**, clip from x 0, then it
    redraws the banner (mode 18, height 6). So the ST prints a town message on PELNOR's black bottom rows (191–199) and
    never over the picture; the remaster's band uses those rows (`town_screen.message_y` 192).
  - (s109) **The name colour `[C]` live** (Hatari, EmuTOS 1.4 US; `../parity/shots/p2-town-NN-*.png`, the played
    party placed in each town by `GUILD.DAT` @254 and Continue, plus one walk-in through the town tile): vst_color =
    `[0x25b94]` with towns 3 → pen 11 and 9 → pen 13, and TOS's VDI pen → register map (0→0, 1→15, 2→1, 3→2, 4→4, 5→6,
    6→3, 7→5, 8→7, 9→8, 10→9, 11→10, 12→12, 13→14, 14→11, 15→13) on the `town` palette:

    | Town | Pen | Register | Ink (ST) | Pack colour |
    |---|---|---|---|---|
    | 1 PIPPACOTT | 1 | 15 | 777 white | `#FFFFFF` |
    | 2 HASLEMERE | 2 | 1 | 040 green | `#009100` |
    | 3 SAXLINGHAM | 11 | 10 | 035 blue | `#006DB6` |
    | 4 LLITHFAEN | 4 | 4 | 030 dark green | `#006D00` |
    | 5 IPSWICH | 5 | 6 | 310 brown | `#6D2400` |
    | 6 FERRON | 6 | 3 | 331 olive | `#6D6D24` |
    | 7 BLACK MOOR | 7 | 5 | 400 red | `#910000` |
    | 8 DEATHPORT | 8 | 7 | 222 dark grey | `#484848` |
    | 9 DEMONIAC | 13 | 14 | 210 dark brown | `#482400` |
    | 10 DARK ABYSS | 10 | 9 | 333 grey | `#6D6D6D` |

    (3 → 11 likely because pen 3 = register 2 is yellow `#DAB600` on the grey scroll; 9 → 13 likewise `[~]` reason.)
    **A new game is black:** "start this turn in Pippacott" opens the town with `[0x25b94]` still 0 (every writer, §3,
    is a town tile, the save loader, TRANSPORTATION, the defeat or the ending), so the banner is pen 0 = black and 5 px
    left (nudge 5, ink centre 151) (`p2-town-01-pippacott-new-game.png`; the s78 `p2-pippacott.png` was this case) until
    the party next enters a town. Built: pack `town_screen.name_colours` (town → ink, key 0 = the fresh new game; towns
    11–12, unreachable, by the same rule) and `fresh_name_x` 151, Core `GameSession.FreshStart`, Client
    `TownScreenLayout.NameColourFor` / `NameXFor`.
- **Doors.** `0x013c` hit-tests six rectangles (x, y, w, h `[~]`, words at data `0x1f312`):

  | Pick | Rect | Building on `PELNOR.PIC` | Handler |
  |---|---|---|---|
  | 0 | (297,144,16,40) | far-right door | Guild `0xd378` (Guild options menu bar) |
  | 1 | (210,111,32,36) | crossed-axes sign | Armory: "WHO WILL SHOP?" loop `0xc1ea` |
  | 2 | (16,143,16,35) | left building | Bank `0xded4` (Bank options menu bar) |
  | 3 | (268,125,16,37) | bed sign | Inn `0xe5fc` |
  | 4 | (146,114,16,21) | the gate (guard) | leave town `0xdd6e` |
  | 5 | (72,128,16,26) | house with the gold sign | Mystic `0xd798` (`endgame.md` §2) |

  The rectangles match the doors seen live in Pippacott (`../parity/toss-training-live.md` setup) `[C]`.
- **Town options menu** (`PHANT.RSC` tree 0): Cast spell (6) `0xe3f6`, Use item (7) `0xdfcc`, Show party (8) `0x3a50`,
  Printout (9) `0x19be`, Save game (10) `0xd610`, Auto save (12) `0x28ac` (`endgame.md` §11).
- **Empty party.** Picks 1–8 need a party; with none, "You must assemble a group" (`0x28cc`). Guild, Printout, Save
  game and Auto save work without one. I only guards the exit (`town-menus.md` §Armory "Town Exit with no party"); on the
  ST the Armory, Bank, Inn, Mystic and the menu items are guarded too. Presentation of the same rule, not a game change.
- The manual's "Each Gelnorian town operates in exactly the same way" (CB p.4, shared text) agrees.

## 2. What differs per town: data `[C]`

All of it is in the 292-byte `TWNS.DAT` record (`../FORMAT.md` §9.5) and in `towns.json`:

| Field | Used by | Rule |
|---|---|---|
| `stock[0..180]` | Armory buy list (ids 1–180 with stock ≠ 0, `0x1d54`) | as I |
| `restock_items` (45 ids, +182) | town-entry restock (`0xc0a8`) | as I: 18 × `stock[rand/274] := 0`, then 4 × `stock[list[rand/(32767/avgL)]] := 1` (index 0 when avgL = 0) |
| `price_index` (+272) | buy price `0xc2f2`, training fee `0xcff0..0xd086`, learn fee `0xd272` | as I (formulas in `towns.json` `_formulas`) |
| exit region / y / 25 − x (+274..+278) | leaving town `0xdd6e` | as I: the party appears there |
| name (+280) | banner, "LEAVING %s" | — |

- **Price index** per town: PIPPACOTT 150, HASLEMERE 142, SAXLINGHAM 135, LLITHFAEN 121, IPSWICH 128, FERRON 114,
  BLACK MOOR 220, DEATHPORT 180, DEMONIAC 150, DARK ABYSS 225. So Ferron is the cheapest town and the Netherworld towns
  the dearest (Armory, training and spell learning all scale with it).
- **Stock** at the start: the `TWNS.INT` template (`towns.json` `stock`).
- **Training fee, one small delta:** II computes `(20 − CHA)²` with no clamp at 0. I clamps `max(20 − CHA, 0)`
  (`town-menus.md` §Guild). The two agree for CHA ≤ 20. CHA could only go past 20 through the wand of Filmon (`endgame.md`
  §7), whose bump stops adding at 20. So no reachable difference `[C]` static.
- **Exits that are not the town's own cell** (FORMAT §9.5 `[?]`, now read as data `[C]`): leaving BLACK MOOR (OUT18 6,2)
  puts the party at **OUT6 (16,8)** on Ferronrah, and leaving DEMONIAC (OUT27 13,7) puts it at **OUT18 (18,14)**, the
  bloodstained-temple cell where defeated survivors also wake (`endgame.md` §8). `0xdd6e` copies the record's exit
  fields with no other test, so these exits are one-way links between regions. Why the designers did it is not in the code.

### Unchanged from I `[C]` (constants compared with `town-menus.md`)

- Armory buy `⌊V²·price_index/500⌋ + 1` (`0xc2f2`), no CHA discount; 9-item limit; the Armory has no sell.
- Inn Distribute (`0xe83e`): every carried item (worn ones too) plus the sack is pooled, sorted by id descending and handed
  out; the SELL row gives `V²/8` to the party purse and the town's stock + 1, capped at 99 (`0xeaf4`). As I's
  `town-menus.md` §Distribute (same pool and the same 99 cap); seen live s80 (`../parity/endgame-live.md`).
- Guild: the same menu (Spells, Training, Add, New, Drop, Rename, Inspect, Purge, Exit). Add from another town shows
  "Not in the town / [original text] to get here" (YES/NO); both paths set `@254` := town and age `@264` += 4
  (`0xd442`), as I (D-067 is I's decision about that). Training: `training.md`. Learn fee
  `price_index·level·90/(CHA·INT·(9 − class))` with the `rand/1638 < INT` roll (`0xd272..0xd302`).
- Town entry (`0xbb00`): roster write-back, gold split, aging `@264 += time/1792`, decline at 70 % of the lifespan, death
  past it, `@254` := town for all 7 slots, then the restock.
- Leaving town (`0xdd6e`): `@28` (the TOSS/weapon base), the average level `@88` for the next restock, the highest `@70`,
  the exit position, then "LEAVING %s" and the autosave if it is on (`0x2856`).
- Bank (Deposit / Withdraw / Exit) as I `[~]` (the menu is the same; its code was not compared with I's). The wilderness inn (tile 41: party gold > 255, −256, +1000 time (`0xfa38` → `0xef62`; I's D-008 again → **D2-042**:
  user s84, manual two weeks, ST as option `p2.wild-inn-time`), DEAD skipped,
  the `≤ gap·20` MP roll) as I.
- The wilderness inn loops `d5 < [0x2831c]`; in the wilderness `[0x2831c]` is the party **count** (the main loop adds 1
  on leaving town, `0x7366`), so no member is skipped.

## 3. Reachable towns `[C]`

- **Map.** Ten tile-40 cells, one per town 1–10 (`../FORMAT.md` §9.5).
- **TRANSPORTATION** accepts only results 1–10 (`0xd94c`: `0 < t < 11`) (§4).
- **Nothing else sets the town number.** Every writer of `0x25b94` was checked: the map's town tile (feature arg), the
  save loader (`@254`), TRANSPORTATION, the defeat and the ending (both 1). The `n = 99 → record 11` branch in `0x26cc` is
  I's leftover, and no caller passes 99.
- So **STARVILLE and OLYMPIA can't be reached** in II. This closes FORMAT §9.5's `[?]`. (A transferred character always
  arrives with `@254` = 1, `transfer.md`.)
- **Planes** (`towns.json` `plane`): towns 1–6 are on Ferronrah and 7–10 [original text] That is the same split
  creation uses for ORIGIN (`endgame.md` §9) and TRANSPORTATION uses for its list (§4).

## 4. TRANSPORTATION (#54, town only) `[C]`

Town options → Cast spell → a member → spell slot 53 (`0xe3f6`: MP cost `slot mod 4 + 1` = 2, spent first, as I) →
`0xd94c` → picker `transport_menu 0x23ee` ("[original text]", `PHANT.RSC` tree 10):

```
if town < 7:                                   # Ferronrah
    list = towns 1..6 without the current one  # 5 rows
else:                                          # Netherworld
    partner = {7: 8, 8: 7, 9: 10, 10: 9}[town] # 0x24e6: record = town if town in (7, 9) else town - 2
    list = [partner]                           # 1 row
pick a row → t;  EXIT → no move (the MP stays spent)
if 0 < t < 11:  the party is in town t: counter [0x2831c] := party, town := t, town entry 0xbb00 (aging, restock …)
```

- **I** lists every town but the current one (12 entries with I's blank 12th; `dos/logic-specs/endgame.md` §2.4, D-062).
- **II**: you can't transport between Ferronrah and the Netherworld, and in the Netherworld you can only hop between
  BLACK MOOR ↔ DEATHPORT and DEMONIAC ↔ DARK ABYSS.
- **Manual** (MAGIC section, spell descriptions): "TRANSPORTATION (#54) inter-dimensionally transports the party from one town to
  another." It names no limit. → **D2-030 PENDING**.
- `towns.json`: `transport_destinations` per town (empty for 11–12). Phantasie I's `towns.json` has no such field, so I
  keeps its own rule.

## 5. Town Inn: DEAD members `[C]`

`0xe5fc`, per party member:
1. If status ≠ DEAD and the cast-since-rest flag `@94` is set: clear it, then `rand/327 < (@14 − @16)·20` → MP max + 1
   (as I).
2. Print "IS RESTING" (+ "MAGIC INCREASES"), or "IS RESTING IN PEACE" when status ≠ OK.
3. **Refill HP and MP only if status ≠ DEAD** (`0xe664: tst @24; beq`).

- **I** refills every member, DEAD too: "the fields are filled, the status stays DEAD" (`town-menus.md` §Town Inn).
- **II** leaves a DEAD member's HP and MP as they were. SLEEP members are refilled (and stay asleep), as in I.
- **Manual**: "INN is where characters can have their hit points and magic restored" (Town Menu, INN); no exception for the
  dead. This is I's D-047 (the wilderness inn) now in the town Inn too. → **D2-031 PENDING**.
- No time passes and nobody ages at the town Inn, as in I. So I's D-004 applies to II's Inn too, and I's D-046 (the
  wilderness inn's `≤`) to II's wilderness inn. → **D2-032 PENDING** (repeats of I's decisions, as D2-009 / D2-013).

## 6. Manual

Sources: the combined rulebook's Town Menu pages (Guild, Armory, Bank, Mystic, Inn), the MAGIC section, p.27.
- The Towns text is I's ("11 towns of Gelnor", "Pelnar is Town #1"): **D2-005**, NOT A CONFLICT (user s77).
- Services, Armory restock ("Rings, scrolls … stay there … combat weapons and potions may appear and disappear"), bank
  branches in every town, free town inns, inns outside town "250 GPs (plus tax)": as I, with I's decisions
  (D-004, D-005, D-046, D-047, D-054) as the reference.
- New conflicts: **D2-030** (TRANSPORTATION's limits), **D2-031** (town Inn skips the dead), **D2-032** (I's inn
  decisions repeated).

## 7. Live

The towns spec itself is static (s80). Seen live:
- in earlier sessions: the Pippacott screen and doors, "You must assemble a group" at the gate, the Guild Add / Training
  / Bank flows (`../parity/roster-live.md`, `toss-training-live.md`), and the Auto save toggle (`endgame.md` §10);
- in the s80 endgame run (`../parity/endgame-live.md`): the Inn's "IS RESTING IN PEACE" for a DEAD member, the Inn's
  Distribute pool, and the Mystic.
- The HP side of §5 (no refill for the dead) was not measurable in that run: the dead member's HP was already full.

## 8. Open `[?]`

- A live TRANSPORTATION cast from a Netherworld town (one row) and from a Ferronrah town (five rows).
- The banner nudge values are read from code, but not compared with screenshots of each town (s102: they only cancel
  the name's space padding, so the ink centre is the same everywhere; the per-town name colour is settled live s109, §1).

## 9. Core (s81)

- Towns load from `towns.json`. II's exit position (`exit_region/x/y`) and `transport_destinations` ride on `TownState`
  (`Exit`, `TransportDestinations`; restored from the data when a save loads). Start town 1, PIPPACOTT (`Phantasie2Campaign`).
- User decisions s81 (`DIFFERENCES-II.md`), manual by default, the ST as an option: D2-030 TRANSPORTATION offers every
  other town 1–10 (`p2.transport-list` = `st`: §4's lists); D2-031 the town Inn refills the dead (`p2.inn-dead` = `st`: §5's
  skip); D2-032 a town Inn stay takes two weeks (`p2.town-inn-time`) and the wilderness inn stops at the table
  (`p2.inn-magic-cap`).
- The Mystic is `endgame.md` §2 (`Phantasie2Campaign.ConsultMystic`). ORIGIN is set at Guild N)EW by the town's plane.

## 10. Guild with an elemental (Add / Drop / Purge) `[C]` static (s172, not seen live)

Record layout: party records `0x25162 + 0x13e·i` (i = 0..5 characters, i = 6 = the elemental, `0x258d6`); `+0x5c` (@92)
= roster number + 1 (0 = empty), `+0xda` = in-party word, `+0x18` = status. `0x25eb6` = last character index (−1 = none),
`0x25932` = record 6's @92 (non-zero = an elemental loaded), `0x25f5c` = the Guild roster (25 × 318: 22 characters, then
FIRE / WATER / EARTH).

- **Guild menu** `0x352` (items `0x10`–`0x18` → jump table `0x229a6`: Learn `0xd0d2`, Training `0xcdde`, Add `0xd442`,
  New `0xc90c`, Drop `0xd57c`, Rename `0xda82`, Inspect `0xe69e`, Purge `0xd68a`, Exit). `0x2308(2, item, 1)` disables an
  item. Drop (`0x22ec`) is disabled only when no character **and** no elemental is in the party. Add is disabled only
  when the party holds min(characters in the roster, 6) characters and an elemental. Learn / Training need party record
  0; Purge / Rename need a character in the roster.
- **Add** `0xd442`: loops until the sixth record and the elemental are both filled. List `0x1a08`: 25 rows (the
  elementals too) while `0x25932` = 0, else 22; rows with @92 = 0 or already in the party are left out. An elemental pick
  (row > 21) copies the record into record 6, **no member count** (an empty party too) and no town / age change. A
  character goes to the first empty record 0-5 (I's town check, alert 6 "two months", `@264 += 4`, `@218 := 1`); the
  elemental never blocks it.
- **Drop** `0xd57c`: list `0x5da(7)` = the seven party records, empty ones greyed. The pick's in-party word is cleared;
  a character (index < 6) is copied back to its Guild record; @92 and the status are cleared; `0xc14a` compacts records
  0-5 only and recounts `0x25eb6`. So a character's Drop never touches the elemental, even the last character's, and the
  elemental itself is dropped from the same list (no copy-back).
- **Purge** `0xd68a`: list `0x1a08` over the **22** character records (party members included), so an elemental is never
  offered. The Guild record's in-party word, word `0x100` and @92 are cleared, then every party record 0-5 holding it is
  cleared. Record 6 is never touched: the elemental stays.
- **SUMMON** `0x83e2`: still needs a party of six with the elemental in record 6 (`encounters.md` section 4).
- **Manual:** "The elemental will only serve a party of six adventurers" (Magic, PDF 9): the ST enforces it at SUMMON, not
  at Add: `DIFFERENCES-II.md` D2-051 (DECIDED s173: the ST's Add stays; SUMMON's six is the limit).
- `[?]` What the ST does when the party is left with only the elemental and leaves the Guild or the town (not read). The
  remaster keeps its own guard (town Exit refuses a party with no character).
- `[?]` Town entry: the remaster's II unloads the elemental on entering a town with I's rule (`Aging.ApplyTownEntry`, DOS
  OV1 `0x19d3`); no ST write to record 6 outside the Guild and the continue path was found in this read, so the ST may
  keep it. Unverified.
- **Core (s172):** `phantasie2/rules.json` `elementals` (`ElementalRules`: `min_members` 0, `blocks_add` false,
  `drop_keeps` + `keeps_alone`, `drops_elemental`, `purge` = `keeps`, `summon_min_members` 6) is II's `game` value of the
  every-game options `elemental-load` / `-drop` / `-purge`; `GameSession.TryDropElemental` + the Client's Drop list row.
