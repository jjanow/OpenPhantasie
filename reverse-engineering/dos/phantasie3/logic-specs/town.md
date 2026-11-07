# Phantasie III — town: bank, Trade / Distribute, saving (autosave), Mystic, Drop

Phase 09 T4 3c.6 (s81, static; nothing run in DOSBox). Source of truth: the III manual (Bank PDF 5 / pr.5, Town Menu:
Other Options PDF 6 / pr.6–7, Save Game PDF 7 / pr.8, Starting and Ending PDF 13 / pr.21, Mystic PDF 6 / pr.7). Code:
DOS `MAIN.EXE` (addresses `../FORMAT.md` §1; functions `../FUNCTIONS.md`). Tags: `[C]` code / data bytes, `[~]`
probable, `[?]` not decoded. Conflicts go to `DIFFERENCES-III.md`; not decided here.

## 1. Menus `[C]`

DOS III uses highlight-bar menus (DS `0x2c1e…0x2dfe`):

- **Town:** Guild, Bank, Armor(y), Mystic, Inn, Options, Exit.
- **Guild:** Learn Spells, Train Member, Add Member, New Member, Drop Member, Rename, Inspect, Purge.
- **Options** (root `town_other_options` `0x8348`): Cast Spell, Use Item, Show Party, Distribute, Load Game, Save Game,
  Quit Game, Sound, About.

There is **no Trade entry** and no Autosave entry anywhere (see §3, §4).

## 2. Bank `[C]` (ovl2 `0x297a`)

The screen shows the party treasury (`FAR:0x9de`, u32) and the sum of the members' accounts (`+0xfe`, u32). Two
options (strings 109 / 110):

```
[original text]:
    party_gold += sum of every member's account; every account = 0
[original text]:
    "How much:" — digits only; re-asked until 0 < amount <= party_gold
    party_gold −= amount
    r = amount mod party_size;  amount −= r
    repeat r times: a random living member (root 0x1124) gets 1 gold
    every member gets amount / party_size
```

- **No fee**: the deposit puts the whole amount into the accounts. The manual: "A small banking fee will be charged
  at every deposit" (PDF 5 / pr.5) — **D3-025**.
- **Equal shares**: the manual's "Each adventurer will get either 1, 2, or 3 shares of the party's gold and the value of
  each share will be displayed" (PDF 5 / pr.5) has no counterpart; the split is even (remainder 1 gold at a time to
  random members) and no share value is shown — also **D3-025**.
- **Withdraw is all-or-nothing**: there is no per-character or partial withdrawal (manual: "allows each adventurer to
  withdraw gold from his account") — part of D3-025's side effects, not a separate entry.
- **No 65,000 cap**: gold fields are 32-bit; the bank and the found-gold, armory, training, inn, Drop and transfer
  paths read add or subtract without a ceiling, and the constant 65000 does not occur in the program `[C]`. The manual: "A party can carry up to 65,000 Gold Pieces … An adventurer's bank account can hold a
  maximum of 65,000" (PDF 5 / pr.5) — **D3-026**. (The ST card's 1,000,000,000 shared purse is a different design.)
- The account display on entering the bank is the party's total, not per member.

**Drop Member** (ovl2 `0x3c92`) `[C]`: the dropped character takes `party_gold / party_size` into his own account
(party gold reduced by the same); dropping the elemental just clears the elemental flag. The record goes back to its
roster slot (`+0x7e`). The divisor is the character count (`FAR:0xb614`, the elemental not counted). A loaded
elemental stays while a character is left (`elementals.md` §2, built s97).

```
drop(i):                                         # ovl2 0x3d09…0x3d8e (s98 re-read)
    record[i].in_party (+0x24) = 0
    share = party_gold (FAR:0x9de, s32) / characters (FAR:0xb614)    # MS C long divide root 0x3b2:0x2300
    if share < 0: share = 0
    record[i].account (+0xfe) += share
    party_gold -= share;  if party_gold < 0: party_gold = 0
    copy record i back to its roster slot; close the party gap; filled_slots −1; characters −1   # 0x3e06: after the share
```

- The count still includes the dropped character (it goes down at `0x3e06`), so dropping the last one moves all the
  gold; the remainder `party_gold mod characters` stays with the party. No status test: a Dead character gets a share.
- **Amiga** `PhantasieIII` flat `0xd448` is the same routine (race 16 branch, `0x25d42 / 0x240e0` through `jsr 0x211d4`,
  clamp at 0, account `+0xfe`, party gold − share) `[C]` s98. **Apple II** Drop (town lines 3500–3520) copies the record
  back and clears the slot; it moves no gold `[C]`. The manual's Drop From Party (PDF 5) is silent on gold: not a
  conflict.
- **Built s98:** `drop_gold_share` in III's `rules.json` (`RulesProfile.DropGoldShare`), `GuildHall.DropGoldShare` inside
  `TryDropFromParty`; the remaster keeps III's found gold in the sack beside the carried gold, so the share is taken on
  both (found gold first), as DOS's single treasury. Under the manual's 65,000 account cap (D3-026) only what fits leaves the purse (no gold is lost; DOS
  has no cap, so the case never arises there).

## 3. Trade Items and Distribute `[C]`

- **No Trade in DOS.** The strings "[original text]", "[original text]", "This character can't carry
  anymore." (master strings 453–455) are in the table but nothing references them (no table access, no message-helper
  call with their index, checked over the root and all four overlays), and no menu offers Trade. The Amiga program has
  it ("G) Trade Item", "[original text]") `[C]` strings. The manual describes Trade Items, selling by trading to
  "adventurer 7" (PDF 6 / pr.7) and the tip "while in town adventurers may trade items without having to redistribute
  them" (PDF 14 / pr.23) — **D3-027**.
- **Distribute** (Options ▸ Distribute, "Are you sure you want to redistribute?", then ovl2 `0x0000` "[original text]"
  and the disburse / sell screen ovl2 `0x061f` "Disburse or Sell for" / "Player: Class: SH AR BW WP") is the only way
  to move an item from one character to another or to sell it in DOS. It needs an assembled party and the party not
  dead. Item usability uses the attribute sums of `../FORMAT.md` §4.1.
- **Sale price** (ovl2 `0x9a1…0xa1b`, s88) `[C]`: `price = ⌊⌊value[id] · price_index[town] / 100⌋ · 75 / 100⌋`, at least 1
  (MS C long multiply / divide, root `0x3b2:0x23a6` / `0x2300`; value `DS:0xf92`, index `DS:0x10fc`). The first factor is
  the town's buying price (root `0xf22`), so a sale pays **75 % of what the item costs in this town**, rounded down twice.
  The picker sells when the choice is the row after the members **or Esc** (`== party size || == −1`, ovl2 `0xa7f…0xa89`)
  `[C]` branch: Esc in the "Disburse or Sell" picker sells the item, unless the menu (`DS:0x542`) blocks Esc `[~]` (not run). The sold item goes into this town's stock (§7).
- **Trade sale price** (D3-027; the remaster's `p3.trade` sell path): DOS has no Trade, so Distribute's price above is
  the DOS figure. Amiga: its Distribute sell (`PhantasieIII` flat `0xca1a…0xca74`) is the same formula (value ·
  index / 100 · 75 / 100, minimum 1) `[~]`; its Trade routine (`0xf3dc…0xf684`, "[original text]") moves the item to
  the chosen member and has no price maths; a sale path through it was not found `[?]`.

## 4. Saving `[C]`

- **Save Game** (Options) writes `gamesave.dat` after "Are you sure you want to save this game over your previously
  saved one?" (`save_game` root `0x8176`, format `../FORMAT.md` §8). **Load Game** reads it back (root `0x7f8b`).
- `save_game` has exactly two callers: Options ▸ Save Game (root `0x844c`) and the end of the I → III transfer (ovl1
  `0x5b5`). **Leaving town does not save**: `leave_town` (ovl3 `0x88`) only clears the in-town flag and sets
  `FAR:0x487c`; no other code opens `gamesave.dat` (its name occurs only in `load_game` / `save_game`). The only other
  disk writes are the dungeon saves (`DNP?` / `DNG?`, ovl4 `0x62d`, on leaving a dungeon by choice).
- The rulebook: "Every time the party leaves a town the game is saved automatically" (PDF 7 / pr.8; PDF 13 / pr.21
  "Every time you leave town the game is automatically saved. If you stop the game while outside a town you can
  continue it again from the last town the party was in."). The IBM card (III-AU PDF 18) says only "load a saved game
  via Options menu → Load Game" and "All other rules are as stated in the rulebook"; the ST and Amiga cards say "To
  save the game, click on the Save Option … This is the only time a game will be saved." — **D3-028**.
- One save slot per data disk (load: "There is not a saved game on this data disk to load").

## 5. Mystic `[C]` (ovl2 `0x2ca2`)

Recomputes every member's score (root `0x9884`, `endgame.md` §2) and takes the party's highest score `S`:

| S | Text (string) |
|---|---|
| < 30 | "[original text]" (425) |
| 30–59 | "[original text]" (426) |
| 60–89 | "[original text]" (427) |
| ≥ 90 | "[original text]" (428) |

plus, when any member has the flag: "[original text]" (Phantasie I score `+0x5c` ≠ 0), "[original text]"
(`+0x18` ≠ 0, the Phantasie II flag; DOS's transfer never sets it), and for the highest social class above Noble: 4 →
"[original text]", 5 → "[original text]" The manual's "overall score … how far they have
proceeded on the quest and a rating of how experienced" (PDF 6 / pr.7) agrees.

## 6. Elementals in the Guild

See `elementals.md`.

## 7. Armory stock `[C]` (s88)

- The stock is one byte per town and item id: `INITARMS.DAT` rows (18 × 181 B, `../FORMAT.md` §9.3), read once at
  start-up (ovl1 `0x5e0`; ovl1 `0x96c` is the only reference to "initarms.dat") into `0x27f3:0000` and saved / loaded
  with `gamesave.dat`.
- **Buy** (armory ovl2 `0x2f2a`): price `⌊value · index / 100⌋` (root `0xf22`); stock −1 (ovl2 `0x313e`); at 0 the kind
  leaves the list and row byte 0 (kinds) −1 (ovl2 `0x31fc`). "He cannot carry anymore!" at 9 items.
- **Sell** (Distribute, §3): stock of the current town +1 (ovl2 `0xab3`); a kind at 0 first raises byte 0 (ovl2 `0xa8f`).
  No cap: a potion row at 200+ would wrap past 255 `[~]`.
- **Entering a town** (root `0xa911`): ages the party (ovl3 `0x65a`, `planes.md` §3a), then rebuilds the price list from
  the live stock (root `0xf22`), then (s143, `[C]` static and live; s144 `[C]` static) **pays out**: with the XP
  pool or the found count above 0 it calls ovl2 `0x0000`(1), over the characters only (`FAR:0xb614`, never the
  elemental): each one's best equipment readied (ovl2 `0x37f1`: usable carried item with points ≥ the worn one's, ties to
  the later item, a slot never emptied), the last wand (item 180) dropped from the found list for good (`0x5a9`, one per
  entry, no quest bit), the found items' disburse / sell screen (ovl2 `0x061f`), then "Shares for <name> :" per
  character, the dead too (living 1–3, pre-filled with `+0x54`; dead 0–3, pre-filled 0); `r = pool mod total shares`
  points go one by one to `rnd(characters)` (any status), then each gets shares × ((pool − r) ÷ total), so nothing is
  lost. A wiped party's arrival empties the pool and found list instead;
  the endgame's arrival (`0xfc9a` = 3) keeps them. D3-071; `../capture_s143_town_entry.md`. Nothing regenerates or randomises stock: the `· 0xb5` row writes in the whole program
  are the four above. So DOS stock changes only by the party's own buying and selling.
- The manual: "the stocks of scrolls and other treasures in the armory remain for future visits. However, the stocks of
  weapons and potions can change each time the party enters" (PDF 6 / pr.6) — **D3-033** (user s89, built s91: Apple
  II's churn minus the Light Key and the Burnt Gem by default, this DOS behaviour as `p3.armory-restock` = `dos`).

## 8. Transportation (#54) `[C]` (s88)

`cast_spell_noncombat` (root `0x8e50`) for spell 54 (`0x36`):

```
if not in_town (FAR:0xe596 == 0): "You cannot cast this here." (pointer DS:0x19d0); no cost
t = menu("Transport to which town?", DS:0x560: 14 rows) + 1     # string 435
if t == current town: "The party is already in this town." (string 436); no cost
if t < 1 or t > 14: nothing, no cost (Esc)
else: FAR:0xcc4 = 0, FAR:0x9e6 = −1, current town = t, redraw, "The party is transported."; MP −= cost
```

So the destinations are the **14 Scandor towns** (1 Pendragon … 14 Sciattica). It can be cast in any town, the plane
towns 15–17 included, but cannot take the party to them. The manual: "transport the party interdimensionally from one
town to another" (PDF 11 / pr.17) — **D3-035** (NOT A CONFLICT, user s89; the remaster offers 1–14 as DOS, s91).
