# Phantasie II (Atari ST): I → II and II → I character transfer — logic spec

Phase 09 T3. Rules read from the converter in `UTILIT2.PRG` (I → II routine image `0x0e38`, converter `0x0edc` mode 2,
XP pass `0x128a`); the byte table is `../FORMAT.md` §6. This file states **what** the transfer does to a character, in
the terms of the remaster's shared `Character` model; offsets are cited so each rule can be checked against the byte
table. Tags as in `docs/plan/RE-REFERENCE.md`: `[C]` read from the ST code, `[~]` probable, `[?]` open. **Checked live in
Hatari (s76, `../parity/transfer-live.md`)**: UTILIT2's output equals these rules byte for byte on three rosters; Core's
converter is pinned to two of them (`tests/…/Fixtures/p2-transfer/`).

Manual: the combined I/II rulebook p.27 ("Updated Rules", quoted in `docs/plan/09-trilogy-manual-p2.md` §2).
Differences with the manual: `DIFFERENCES-II.md` D2-001 … D2-004. Core: `TransferIToII` (`src/OpenPhantasie.Core`).

## 1. What is converted

- The **22 character slots** of I's Guild, empty ones included (`0x0edc` loops over 22; an empty slot, `@92 = 0`, gets
  the edits and stays empty). The three elemental records (slots 22–24) are **not** converted; II's guild takes its own
  from II's `TWNS.INT` (`5950 + 22·318`, live).
- The XP raise (§4 step 2) runs only on **occupied** records (`@92 > 0`).
- The transfer replaces the whole II roster (the manual: "Any characters you have created beforehand … will be erased").
  `twns.dat` and `sack.dat` are written from II templates, so the party's found gold and items are gone too `[~]`.

## 2. The rule: copy everything, then change these fields `[C]`

The record is copied whole (bytes 0–281, I's layout; FORMAT §5) and then the fields below are changed — only their low
bytes: **the high byte of every edited word is kept** (live: HP 310/300 → 256/256, MP 291/290 → 261/261; the item, worn
and points words are below 256 in any real record). Every field not
listed keeps its I value: stats, skills and skill bonuses, status (a DEAD character stays DEAD), race, class, age clock
and lifespan, roster ordinal, in-party flag, cast flag, runes and the other I words, name.

| Field (Core) | Offset | II value | Note |
|---|---|---|---|
| MP max, MP current | low bytes 17, 19 | `(v & ~0xFF) \| 5` → **5** for any MP < 256 | MP ceiling `@14` is kept |
| HP max, HP current | low bytes 21, 23 | `v & ~0xFF` → **0** for any HP < 256 | manual silent; the status is not changed `[~]` (see §6) |
| Level | low byte 89 | **20** if it was 20 (an undead), else **0** | manual: "they will be at level 0" |
| Worn shield, armor, weapon | 83, 85, 87 | SM SHIELD **4**, LEATHER **23**, MACE **49** | D2-001 |
| Carried items | 223, 225, 227; 229–239 | slot 1 = 4, slot 2 = 49, slot 3 = 23; slots 4–9 empty | every I item is gone; the wand survives as the score flag (§6, D2-001) |
| Weapon points, shield soak, armor soak | 241, 243, 245 | 4, 4, 3 | the MACE / SM SHIELD / LEATHER values |
| Armor in effect | 247 | 1 if it was 0 | a fight-start copy (`@246`); Core has no field for it, combat prep rewrites it |
| Current town | 255 | **1** = PIPPACOTT (II's `@254` is 1-based, FORMAT §9 `[C]`; Core town **1**) | Core first read it as I's 0-based index (town 2); fixed with the T5 wiring |
| Spell flags | 97–203, 204–215 | §3 | D2-002, D2-004 |
| Bank gold | 262, 263 | §5 | D2-003 |
| Experience | 266–269 | §4 | |
| II-only bytes | 282–317 | all 0 | @284 (origin `[~]`) = 0 = Gelnor; the rest `[?]`. Not in the Core model yet |

Bytes the ST copies whose meaning in II is open: `@42` (TOSS in II `[~]`, 0 in every I record, so a transferred character
starts with TOSS 0), runes `@72–@80`, `@90`, `@260`.

## 3. Spells `[C]`

Spell flags are words at `@96 + 2·slot`, slots 0–59; a player's spell number is slot + 1.

1. Clear the flags of slots 0–53 (spells #1–#54).
2. Keep slots 54–59 (#55–#60) as they were. In II's spell table DIVINE II is slot 56 (#57) and DIVINE I slot 57 (#58);
   in I only slot 57 (DIVINE I) is named.
3. Set HEALING I (slot 0, #1) to 1 for a **RANGER or PRIEST**, and FIREFLASH I (slot 4, #5) to 1 for a **MONK or WIZARD**
   (class bytes 2/4 and 1/5). The flag is set whether or not the character knew the spell in I (1 = known).

Manual: "all spells except for any divine spells (#55 and above). Wizards will keep spell #5 and Priests spell #1."
Options (user, s76): step 3 is `p2.transfer-spells` — `st` (as above; Original (Atari ST) preset) or `manual` (Wizards
#5 and Priests #1 only, the same "set to 1" mechanics; Original (manual) and Remaster presets: read strictly, the manual
takes Rangers' and Monks' spells). Step 2 is `p2.transfer-divine` — `p27` (#55 and above, as the ST) or `p14` (#58 and above,
p.14's "DIVINE SPELLS (#58+)": slots 54–56 are cleared too).

The remaster's spell array holds I's 54 spells plus DIVINE I (index 54 = slot 57). It has no place for slots 54–56, 58,
59, so for any character coming from I the two `p2.transfer-divine` values give the same result (I never sets those
slots). They differ once II's own spell list (DIVINE II, slot 56) is modelled.

## 4. Experience `[C]` (live)

Step 1 (`0x0edc`): the s32 XP becomes **256 · g**. The ST reads only **byte 267**, b = `(XP >> 16) & 0xFF`; byte 266 is
never read, so XP wraps at 2^24 (live: 16,847,216 = `0x01011170` → b 1 → 512). For b = 0, g = 1 if byte 268 ≥ 20 (XP
mod 65,536 ≥ 5,120), else 0.

| b = (XP >> 16) & 0xFF | g | New XP |
|---|---|---|
| 0, XP mod 65,536 < 5,120 | 0 | 0 |
| 0, XP mod 65,536 ≥ 5,120 | 1 | 256 |
| 1 | 2 | 512 |
| 2 … 5 | 3 | 768 |
| 6 … 12 | 4 | 1,024 |
| 13 … 24 | 5 | 1,280 |
| 25 … 40 | 6 | 1,536 |
| 41 … 70 | 7 | 1,792 |
| 71 … 100 | 8 | 2,048 |
| 101 … 150 | 9 | 2,304 |
| 151 … 220 | 10 | 2,560 |
| > 220 | 11 | 2,816 |

Remaster gap: a remaster XP outside I's s32 word is read as the word would hold it (clamped to 0 … 2^31 − 1).

Step 2 (`0x128a`, occupied records only): **XP is raised to I's level-0 training minimum when lower** — the manual's
"level 0 but enough experience to improve":

`XP_min = trunc(200 / (sum / divisor + 1e-5))` (I's training formula at F = 1, `town-menus.md` §Training)

| Class | sum / divisor | I's |
|---|---|---|
| THIEF | (STR + 2·DEX) / 19.8 | same |
| MONK | (STR + INT + DEX) / 49.5 | same |
| RANGER | (STR + INT + CHA) / 52.8 | same |
| FIGHTER | **(2·STR + DEX) / 33.0** | (STR + DEX) / 16.5 |
| PRIEST | **(STR + 2·INT) / 36.3** | (STR + INT) / 18.1 |
| WIZARD | (2·INT + LUCK) / 42.9 | same |
| other class byte | 200 → 0 | — |

Live: FIGHTER STR 5 DEX 3 → 507, PRIEST STR 3 INT 4 → 659, THIEF sum 15 → 263.9965 → **263** (truncated). The ST
computes in FFP singles; Core uses doubles, which gave the same integers on every live input (a threshold within float
rounding of an integer could differ `[~]`).

## 5. Bank gold `[C]`

`hi` = the high byte of I's u16 bank word. II's word = **⌊hi / 35⌋ · 256 + 100**: up to 8,959 GP becomes 100 GP, the most
anyone keeps is 7 · 256 + 100 = 1,892 GP. A character with under 100 GP gains gold.

- Remaster gap (no ST equivalent): a remaster bank balance above 65,535 (`p1.gold-cap` = `lifted`) is read as 65,535
  first, as the DOS export writes it.
- Option `p2.transfer-gold` (D2-003): `st` (the formula; user s76: accepted) or `no-gain` = min(old, formula), so nobody
  gains gold (a candidate, pending the user).

## 6. Open points

- HP 0 with status kept: is a transferred character with HP 0 and status OK able to fight before resting or training?
  Check by loading a converted roster in `PHANT.PRG` `[?]`. (Training at level 0 → 1 should give HP; the Inn refills HP.)
- Whether II's own Guild training uses the II FIGHTER / PRIEST sums of §4 `[?]`.
- D2-001 settled (s76 research): no II version has a wand item. II's "wand of Nikademus" is a **flag**: the character's
  I score `@70` > 99, which the transfer copies unchanged (`PHANT.PRG` `0x3a16` prints "- WAND OF NIK -"; the Mystic
  `0xd7e4` adds "heroes of Gelnor"). So the ST keeps the wand as the manual says; no option. Core:
  `TransferIToII.HasWandOfNikademus`.
- II → I (mode 1): section 8.

## 7. Remaster flow (Phase 09 T5)

What the original does around the converter, and how the remaster offers it.

**Where (the original).** Not in II's Guild or creation. The ST has it in the backup utility `UTILIT2.PRG` (TRANSFER
CHARACTERS → FROM PHANTASIE 1 TO PHANTASIE 2); the manual's p.27 calls it "the Utilities function", the Apple/C64
text "Option 6" on the dungeon side. It "should be the first thing you do".

**Steps (ST, live s76, `../parity/transfer-live.md` §1) `[C]`:**
1. Alert "THIS WILL ERASE ANY CHARACTERS ON THE PHANTASIE 2 DISK. OKAY?" (YES / NO).
2. "INSERT PHANTASIE 1 DATA DISK": I's own roster file (`guild.dat`) is read. Only the roster: nothing else of I's.
3. "INSERT PHANTASIE 2 DATA DISK": II's `guild.dat` is written (§1–§5, II's own elementals), `twns.dat` from II's
   template, `sack.dat` zeroed, `DNG.SAV` / `DNGX.SAV` deleted. `OUT*.DAT` get their grids back from II's `MAPS.INT` (the new-game routine
   `0xb2c(2)`, called first; s208 `[C]` static, FORMAT §9.1), so the fog is reset too: regions 1 and 2 open, the rest
   fogged. (`MAPS.INT` is the buffer loaded at start-up; a II → I transfer earlier in the same run reloads it with I's
   `maps.int`, so an I → II after it would copy I's grids `[~]`, not checked live.)
4. The next `START.PRG` run starts in Pippacott with nobody in the party; the converted characters are free Guild
   members (`@218` is kept but ignored and cleared by the next save, `../parity/roster-live.md` §4.4).

So a transfer is **a new II game whose roster is I's converted roster**.

**Remaster:**
- **Where:** the game menu's utility row (the slot of I's "Import DOS save", key F7) reads "Transfer from Phantasie I"
  in II. It asks the ST's question first (Y/N).
- **Source:** I's roster file = the remaster's own Phantasie I save (`savegame-p1.json`, its top-level roster; parked
  parties hold none). A save of another game, or an old-format save, is refused. Not offered: a DOS `GUILD.DAT` read
  straight into II (no original did that; F7 in I, then save, then the transfer gives the same), and the ST's own
  big-endian I `GUILD.DAT`.
- **Rules:** the running II save's locked options (`p2.transfer-spells` / `-divine` / `-gold`), and **II's**
  `guild-limit`: `game` converts the first 22 slots (the ST's loop; anyone past slot 22 of a remaster I roster that
  grew under I's `guild-limit` = `none` is left behind and named in the report); `none` converts every slot and keeps
  the slot numbers (Guild number = slot + 1, no compacting).
- **Occupied:** a remaster slot holding a character is an occupied record. A character made in the remaster has no
  DOS `@92` (`RosterOrdinal` 0); it is given its slot number, as the DOS export writes `@92`, so the §4 step 2 XP raise
  runs for it too (the manual: "enough experience to improve").
- **Reset (a new game):** the party, the elemental and the parked parties are empty, the purse 0, the sack and pending
  treasure empty, no dungeon save, every town from its template, the start in Pippacott (II's new-game start), every
  character out of the party (`InParty` false), II-only fields zero (`Phantasie2` new: origin Gelnor, quest score 0).
- Creation's origin rule (`OnCharacterEnrolled`) is not run: a transferred character's origin stays Gelnor.

## 8. II → I (mode 1, Phase 09 T5, built s145)

**The original `[C]` (static `0x0d38` + live run C, `../parity/transfer-live.md`):** UTILIT2's TRANSFER CHARACTERS →
FROM PHANTASIE 2 TO PHANTASIE 1. Alerts: "THIS WILL ERASE ANY CHARACTERS ON THE PHANTASIE 1 DISK. OKAY ?", then the P2
data disk (II's `guild.dat` read), the P1 game disk (I's `maps.int` / `twns.int`), the P1 data disk (I's `guild.dat`,
7050 B, written; `twns.dat` from I's template, `sack.dat` zeroed, dungeon saves deleted, `OUT*.DAT` grids from
`maps.int`). So a II → I transfer is **a new I game whose roster is II's converted roster**.

**Rules:** the converter `0x0edc` in mode 1 runs **the same per-byte table as sections 2–5** on II's 22 slots, with:
- **no XP raise** (`0x128a` does not run): XP is only the section 4 tier, 256 · g. Live: II XP 1360 → 0.
- the **36 II-only bytes dropped** (I's record is 282 B): origin, quest SCORE, minion flags, beast runes are gone.
  I's `@70` score is copied, so a wand holder stays one.
- **TOSS `@42` copied** into I's unused word (live: 24–139 kept). The remaster keeps `TossRock`; I uses it only with
  the rock-throwing module on.
- **town byte 255 := 1**; I's `@254` is 0-based (DOS; the ST I's record follows the DOS layout `[~]`), so **town 2 =
  PINEVILLE**. The new I game starts in Pelnor, so Guild → Add there asks the move question and ages the member +4
  (I's own `0x2835` rule for a member of another town).
- II's elementals are not converted; I's come from I's `twns.int`.
- In mode 1 the converter compares source byte `i + 283` with 200 into a flag never read (dead code).

**No options.** The manual has no II → I, so the ST's values run (`TransferIToIIRules.St`: Rangers and Monks get their
spell too, divine #55+ kept, the gold formula), whatever the II save's `p2.transfer-*` say. Not a manual difference.

**HP 0 `[C]` (live s146, `../parity/transfer-live.md` section 8):** every arrival has HP 0 / max 0 and status OK, like an
I → II arrival (section 6), and XP 0 unless II's XP was at least 5,120 (no raise), so most cannot train until they earn
XP. In the ST I (v2.0) such a member dies to the first hit that lands on it; run C's party of six was wiped by four
coyotes and the Great Demon destroyed every one. Unlike I → II (XP raised to the level-0 training minimum, 100 GP or
more banked), nothing lets a II → I arrival get HP before it fights. The remaster does the same (Core: a hit at HP ≤ 0
is the death step; `TransferIIToITests.AnHpZeroArrival_DiesToTheFirstHit`; Inn: nothing to refill).

**Town byte `[~]`:** in the same live run the ST I's Guild in Pelnor added run C's members (town byte 1) with no move
question; the remaster reads byte 1 as I's 0-based Pineville and asks I's move question (+4 years). Either the ST I's
town byte is 1-based (as II's) or the ST I has no move question; not read.

**Remaster (Core `TransferIIToI`, `GameSession.TransferFromPhantasieII`):**
- **Where:** Phantasie I's game menu, row "Transfer from II" under the DOS rows (I's F7 stays the DOS import). It asks
  the ST's erase question first (Y/N), then the file.
- **Source:** the remaster's own Phantasie II save (`savegame-p2.json`, its top-level roster). Another game's save or
  an old-format save is refused (`TransferSource`).
- **Rules:** I's `guild-limit`: `game` converts the first 22 slots and names the rest; `none` converts every slot at
  its Guild number. A remaster-made character with no `@92` gets slot + 1, as in section 7.
- **Reset:** a new I game under the running save's locked options: Pelnor, nobody in the party, purse 0, no
  elemental, no parked parties, everyone out of the party.
- Pinned to run C byte for byte (`TransferIIToITests`, fixture `setC-p2-guild-in.dat` = the played II roster).
