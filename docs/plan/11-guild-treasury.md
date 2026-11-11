# Phase 11: Guild treasury (P3 QoL, s175)

**Decided (user, s175, 2026-11-09):** "banking across the games" (PLAN P3, from D3-025) becomes one gold pool per save,
usable at any time by any active party member, in the town and in the field. The user asked for the redesign and
approved it as a whole ("I like your entire plan. Build it as you've described"), with these choices:

1. The pool is **Guild-wide** (one per save), not one per party.
2. Only **trip gold** (gold won since the party last left town) can be lost; savings are safe.

QoL, so a rules option (off = the original), locked per save (T5.0). Remaster UI, no manual↔code conflict, so no
`DIFFERENCES*.md` entry. No original has a Guild-wide treasury or a ledger. All four have a shared party purse in the
field; trip gold continues that purse.

Tags: `[C]` checked in the code, `[~]` probable, `[?]` unknown.

**Built (s175):** all four games, Core and Client (section 8 lists what is left). Code: `Treasury.cs` (the type, `GoldPurse`,
`TreasuryReport`), `GameSession.Treasury.cs` (I–III sweeps, `TreasuryShares` for transfers out), `Party.Spend`,
`P4Party` (IV's treasury, sweeps and spends), `TreasuryDialog.cs` (I–III window), `P4DungeonScreen.Buildings.cs` (IV
window). Tests: `TreasuryTests`, `Phantasie4TreasuryTests`, `TreasuryClientTests`, `P4TreasuryScreenTests`.

---

## 1. The option

`treasury` is one every-game rules option (`RuleOptionDescriptor` with `EveryGame`, all four games). It has three values:

| Value | Meaning | Presets |
|---|---|---|
| `off` | the game's own purse, sack and bank accounts (the original) | Original (manual), Original (DOS / ST / X68000) |
| `trip` | Guild treasury; gold won on the current trip can still be lost | **Remaster** |
| `safe` | Guild treasury; every gain goes straight into the treasury, so losses find nothing to take | — (Custom) |

It sits in the game tab's new **QUALITY OF LIFE** section (section 6).

## 2. Model

- **Treasury** (Core `Treasury`): a `long` balance with no cap, the current trip number, and a ledger. The ledger keeps
  the last 50 entries; each entry has the trip number, a kind, the amount (+/−) and the balance after it.
- **Where it lives:**
  - I–III: top level of the session (`GameSession.Treasury`, saved at the snapshot's top level). It is shared by every
    party (D-069). It is never copied into or out of a parked party (`PartyLocal`, `Park`, `Select`).
  - IV: `P4Party.Treasury` (IV has one party per save).
- **Trip gold:** the existing field stores.
  - I–III: the party purse `Party.GoldCarried` plus found gold `Sack.Gold`.
  - IV: the party purse `P4Party.Gold`.

  Every original loss already takes only from these fields, so losses need no change.
- **Spendable gold** = trip gold + the treasury balance.
- **Lifted cap:** with the treasury on, the gold cap is lifted for the trip stores too (`GameOptions.GoldCap` →
  `Lifted`).

## 3. Sweeps (gold into the treasury)

A sweep moves gold into the treasury and logs it in the ledger. It is idempotent, so it is safe to call often. It moves:

- every roster member's bank account (I–III `GoldInBank`, IV `Account`), each character counted **once**.
  - IV: party members are copies of roster entries, so the sweep first syncs the roster, then empties the roster
    accounts and zeroes the copies' accounts.
- **in town only:** the active party's purse and found gold.

When it runs:
- on town entry (before any share-out), and on leaving town;
- after every town or Guild action that can add gold: creating a character, training (III and IV level gold), selling,
  Trade, a transfer in, IV's arrival hand-out;
- on loading a save or selecting a parked party that is in a town.

With `safe`, gains made in the field are also swept at once: fight payouts, dungeon / event gold, and IV's battle and
GIVEGOLD gold.

A parked party that is away from town keeps its own purse and found gold. That is its trip gold, and it is not swept.

Ledger kinds: `trip` (trip gold brought into town, or the trip-gold part of a spend: section 4), `accounts` (account
gold collected: starting gold, level gold, transfers in), `sale`, and the spends (section 4).

## 4. Spending

Every cost goes through one helper. `CanAfford(n)` tests spendable gold. `Spend(n, kind)` takes trip gold first, then
the treasury, and logs the spend whole: the part paid from trip gold first as a `trip` entry (trip gold brought in),
then the full price as the spend's entry. So each entry's balance and each trip's total stay the treasury's real
change, and a field spend paid from the purse alone still shows in the ledger (two lines that cancel). With the option
`off`, both behave exactly as the purse code did and nothing is logged.

| Game | Spends routed through the helper |
|---|---|
| I, II | Armory buy, Guild training, spell learning, wilderness inn (256, purse then found gold today) |
| III | Armory buy, training, spell learning, wilderness inn (250) |
| IV | training (`_trang`), Magic Hall (`_mage`), Armory (`_buyitem`), shipwright (1500), outdoor inn (256), Magic Bag (spell 50), dungeon TAKEGOLD (a payment), and CMPGOLD tests spendable gold, so it stays consistent with TAKEGOLD. TAKEGOLD past CMPGOLD's 30 000 cap (below) takes at most the spendable gold |

**Losses keep their own code and only ever touch trip gold:**
- Beg for mercy and wilderness surrender (I–IV)
- I/II dungeon ops 9/10
- III ops 9/10 and the FieldGold take (III's `TakeGold` is used only for losses; the wilderness inn's spend goes through
  the helper)
- I/II defeat (found gold lost)
- IV's begging (gold mod 256)

## 5. What the option switches off

- **Bank accounts:** the town Bank (I–III; III window 9) and IV's bank open the **Treasury window**: the balance, then
  the ledger, newest first, grouped by trip with a trip total. There is no deposit, withdraw, share or fee.
- **Share-outs:**
  - I/II Distribute's gold share (found gold is swept instead; the XP share and `p1.payout-remainder` stay for XP)
  - III's Drop gold share (`GuildHall.DropGoldShare`)
  - IV's entry gold shares (D4-030 `Deposit` stage)
- **Per-character gold on the sheets:**
  - I–III inspect sheet "gold in bank"
  - I Amiga map sheet
  - IV profile `Account`
- **Gold line:** shows "GOLD n (+m)", where n is the spendable total and m is the trip gold (shown only away from
  town). It is shown in every mode:
  - I–III: the status line when STATUS LINES is on; otherwise the empty top band of the field, Guild, combat and the
    town's list screens; the free right end of I Amiga's map-screen bar and III Amiga's outside and town bars; the
    message row's left on I and II's town pictures; the top row's right on the full-screen overworld map
  - IV: the town lines and the outdoor / dungeon party panel

  The Armory header, Distribute's SELL row and the inspect summary show spendable gold.
- **Transfer out** (I → II, II → I, I / II → III, III → IV's Hall and Guild join): each character leaves with an even
  share of the source save's treasury (balance ÷ number of roster characters, rounded down) as its account gold, so
  the target's own rules then apply. If the target save also has the treasury on, the sweep adds it there. The source
  is not changed (the transfers copy characters, as the originals do).
- **DOS export (F6):** the treasury is added to the purse that the export's split already puts into the living
  members' banks. Gold over the DOS cap is left behind, and the export's warning list says how much.
- **DOS import (F7):** the bank, purse and found gold of the imported characters go into the treasury at the first sweep.

## 6. Options screen: QUALITY OF LIFE section

A rules descriptor gains `Qol` (default false). Each game tab's rules part is split:

```
 RULES FOR NEW GAMES          (or THIS SAVE'S RULES (LOCKED))
 PRESET              ORIGINAL (MANUAL)
 …the game's original rules…
 QUALITY OF LIFE
 GUILD TREASURY      OFF
 ITEM STASH          IN TOWN
 GUILD ROSTER LIMIT  THE GAME'S
 …
 FROM OTHER GAMES
```

`Qol` is set only on options that exist purely for convenience and have no manual↔code conflict: `treasury`,
`p1.stash-access`, `guild-limit`, `elemental-load`, `elemental-drop` and `elemental-purge`. Options that carry a D-entry
and also have a QoL value (e.g. `p1.gold-cap` `lifted`, `p1.rename-party`) stay in the rules section.

## 7. Saves

- Saves without the field start with an empty treasury. `Restore` gives a missing option its preset's value, so an older
  Remaster-preset save loads with the treasury on. Its first sweep collects the accounts (no migration, per the user's
  no-old-save-compat rule).
- IV: the new field is optional with a default. `Phantasie4SaveCodec.CurrentVersion` is not bumped (its exact-match
  check would reject older saves).

## 8. Built (s175): what is left

- **Treasury window looks:** III under AMIGA SCREENS draws it in window 9 (with a page bar); IV in the bank's grey area
  with its sunken frame. I (Amiga and DOS look), II (every look) and III without AMIGA SCREENS use the remaster's text
  panel `[~]`. The window 9 page bar and IV's window are not seen live (short ledgers, no XEiJ-style run) `[~]`.
- **Gold line:** the status line drops the mode word, then the trip part, when the full line is wider than its band;
  a menu bar's free end too narrow for the trip part shows the spendable total alone `[~]`. III's DOS-look town picture
  has no free rows (it runs to the bottom under a full-width bar), so its gold line sits on a small veil of its own at
  the bottom-right corner, the town messages keeping left of it `[~]`. The I Amiga sheet's bank cell is left blank.
  Checked on Xvfb (s175): I Amiga and DOS (wilderness, dungeon, map-only, town, Guild), II ST (incl. the terrain key,
  map-only, town), III Amiga and DOS (wilderness, dungeon, town, Guild) and the remaster's combat screen.
- **Trips:** Transportation from town to town (I–III and IV) arrives without leaving, so the trip number does not go up
  (the sweep still runs on arrival).
- **Spends in the field:** a spend's trip-gold part is logged as a `trip` entry before the spend's full price (section
  4; s201, before that only the treasury's part was logged).
- **IV TAKEGOLD:** every TAKEGOLD in the shipped scripts has a CMPGOLD for the same amount before it on its path
  (O_006, O_008, O_009, O_010, O_013, O_018). The one way past it is CMPGOLD's cap: above 30 000 gold it answers
  30 000 whatever the amount (unsigned `bls` at `0x1989a`), so O_008's typed amount (`CMPGOLD 0` / `TAKEGOLD 0`) over
  the gold reaches TAKEGOLD's unchecked `sub.l` (`0x19f1e`, u32 purse at `0x789e4`), which wraps the purse in the
  original (`logic-specs/events.md` section 4). With the treasury on, TAKEGOLD takes at most the spendable gold (purse,
  then treasury; neither goes below zero; s201). Off, Core keeps the unchecked subtraction, but its signed purse goes
  below zero where the X68000's wraps to about 4.29 billion `[~]` (not changed).
- **Not unit-tested:** the I–III Bank routing in `Game1` (checked live on Xvfb for I, II and III) and III's window 22.
