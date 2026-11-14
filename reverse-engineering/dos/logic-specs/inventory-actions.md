# Inventory actions (P3 inventory rework = 09 T2) — remaster spec

**Status:** remaster QoL layer over the original item rules. It adds no DOS behaviour; every legality rule below comes from
the manual or from code that is already `[C]`. Built headless in Core (`InventoryActions`); the Client's paperdoll + stash
screen (a presentation option) calls it. Page numbers are the manual PDF's (`docs/manuals/phantasie1/Phantasie_I_1986_SSI_manual_text.pdf`).

**User decisions (2026-10-26, final):** a paperdoll per character plus a party **stash**. The stash **is** the found-item sack
(`LootSack`, 90 items, D-018) made visible, not a new pool. Where the stash can be used is a **rules option** (locked per
save); the screen itself is a presentation option.

---

## 1. The original rules this layer keeps

| Rule | Source |
|---|---|
| One weapon, one armor, one shield worn at a time; never lost or stolen | manual p.11 ("A character can only use one weapon, armor, and shield at a time, and they are never lost or stolen") |
| A character carries at most 9 items — **worn items count** (they are carried items the selector points at) | manual p.12 ("each party member can only carry up to 9 items"); `Character.InventoryItemIds` "worn ones included", OV1 `0x2fdb` [C], `town-menus.md` §Distribute |
| The party carries up to 90 found items | manual p.15; D-018 (DOS 60) |
| Found items are handed out in town | manual p.15 ("Items can be distributed and used when you enter a town"), p.12 D)ISTRIBUTE; DOS: Town menu D only (OV1 `0x2ab3`) [C] |
| Equipment needs enough STR (armor/shield) or STR×2+DEX (weapons); else UNUSABLE | manual p.11 and p.21–22; `ItemRules.IsUsable` / `CombatGate` (FUN_0000_3053) [C] |
| What is worn is always the usable shield/armor/weapon with the most points (first of equals, slot order) | OV1 `0x2fdb` selector (`CombatGear.SelectWornItems`) [C]; Armory buy wears a new item when it is at least as good (`TownMenus.AutoEquipIfBetter`) [C]. Manual p.11: new gear "should have a higher point value than the one already owned" |
| DEAD members' items are pooled and DEAD members can be given items | OV1 `0x2ab3` / give `0x2e03` (no status test) [C] |
| Carried slots keep holes (a drunk potion empties its slot in place); the sack keeps holes (a kept item fills the first empty slot) | `CarriedItems` [C] s58; `LootSack` [D✓] s65 |

**Per game.** The table above is I's (and II's: the ST's gate `0xc366` computes I's formulas,
`atari-st/phantasie2/FORMAT.md` section 5). Each rule is an `ICampaign` hook whose default is I's: `CanUseItem` (the gate),
`ItemPoints` (an item's points), `ReadyBestEquipment` (the best-gear pick), `GearStatsOf` (the gear line and Compare
numbers) and `AfterArmoryBuy` (what the Armory wears). III overrides all five from its own code: the table gate ovl2
`0x39b5` (bows STR + DEX), the point table `DS:0x113c`, ready-best `0x37f1` (bow slot, ties to the later item, a slot
never emptied), the table points with III's defence (root `0xd730`) and no damage A, and an Armory that wears nothing
(`phantasie3/logic-specs/town.md` section 7).

The original has **no** "choose what to wear" and **no** member-to-member or sack access outside the D)ISTRIBUTE screen.
The actions in §3 are therefore all remaster QoL; with the new screen off nothing changes.

## 2. Equipment slots come from data

Each game's `rules.json` lists its worn-item slots (`equipment_slots`): a slot id plus the item-id range it takes.

| Game | Slots | Source |
|---|---|---|
| I | `shield` 1–20, `armor` 21–40, `weapon` 41–100 | manual p.21–22 tables; `ItemDistribution.ItemLine` S/A/W letters (OV1 `0x17df`) [C] |
| III (later, T4) | shield, armor, weapon **61–100**, **`bow` 41–60** | `09-trilogy-matrix.md` §1e (III PDF 6 / 11–12) |

Core maps a slot id onto the character record with one accessor (`WornItems`): `shield`/`armor`/`weapon` →
`WornShield`/`WornArmor`/`WornWeapon`; III's `bow` → `WornBow` (T4). The action code itself never tests item-id ranges; it asks the layout.

Usability is each game's own gate (`ICampaign.CanUseItem`, asked through `GameSession.CanUseItem` / `CanWear`):
I's `ItemRules.IsUsable` (id formulas, [C]); II the same formulas, which II's `items.json` holds `[~]` (checked live only
for the transfer starter kit); III its tables (`P3TownEntryPayout.CanUse`, ovl2 `0x39b5` [C]: `min_str` against STR for
shields and armour, STR + DEX for bows 41–60, 2·STR + DEX for melee weapons; equals III PDF 11–12). The detail box
(`ItemRules.Describe`) prints the game's own table numbers too. IV has its own screens (`P4Equipment.CanUse`) and never
opens this one.

## 3. Actions

Addressing: a **carried slot** is an index into `Character.InventoryItemIds`; a **stash slot** is an index into
`LootSack.ItemIds` (holes included). Every action works on one item.

| Action | Effect | Refusals (reason key) |
|---|---|---|
| **Equip** (carried slot) | The worn pointer of the item's slot is set to the item. The item that was worn stays carried (it was never anywhere else). Worn points are recomputed (`CombatGear.ApplyWornItemPoints`). | empty slot `no_item` · not in a layout slot `not_equipment` · fails the gate `unusable` |
| **Equip** (stash slot) | The item leaves the stash slot (hole) and goes into the first free carried slot, then is worn as above. With 9 carried, the item **worn in that slot** swaps out to the stash (into the freed stash slot) and the new item takes its carried slot. | as above · 9 carried and nothing worn in that slot `carry_full` |
| **Unequip** (equipment slot id) | The worn pointer is cleared; the item stays carried. | nothing worn there `nothing_worn` |
| **Transfer** (member A carried slot → member B) | The item moves to B's first free carried slot. If A no longer carries a copy of a worn id, A's worn pointer clears. B's worn items do not change. | `no_item` · same member `same_member` · B has 9 `carry_full` |
| **ToStash** (carried slot) | The item moves into the stash's first empty slot; A's worn pointer clears as for Transfer. | `no_item` · 90 found items `stash_full` |
| **FromStash** (stash slot → member) | The item moves to the member's first free carried slot (not worn). | `no_item` · `carry_full` |

Common refusals: the member is not in the party `not_in_party`; the stash can't be used here `not_here` (§4).

**Duplicates:** a worn pointer is an item id. Moving one copy away while another copy of the same id is still carried keeps
it worn.

**Compare** (`InventoryActions.Compare`): the member's gear numbers now and with the item worn in its slot — shield soak,
armor soak, weapon damage (A + B, `Combat.PrepFor`'s `WeaponDamageEff`) and defense rating
(`CombatMath.ComputeDefenseRating` with DEX, armor, parry, shield) — worked out on a clone, so nothing is written.

### Remaster choices (no manual or DOS rule exists)

- **Dead members** may receive, hand on and wear items (DOS Distribute does it, §1). Not refused. Decided: keep (user, s75).
- **A hand-picked worse item can be overridden later**: D)ISTRIBUTE runs the DOS selector on every hand-out and the Armory
  wears a bought item that is at least as good. Kept as DOS does it with `modern-inventory` off; with it on nothing
  overrides the player's pick (section 7, user s178).
- **Receiving an item (Transfer, FromStash) does not wear it**, unlike D)ISTRIBUTE's selector. The player wears it with
  Equip. Decided: keep (user, s75). With `modern-inventory` on it is worn when its slot is empty (user s178, section 7).

## 4. Where the stash can be used — rules option `p1.stash-access`

| Value | Allowed in | Preset |
|---|---|---|
| `town` | `GameMode.Town` and the town's `GameMode.Guild` (the original: items are handed out "when you enter a town", D)ISTRIBUTE is a Town-menu command; the Guild added by the user, s75) | Original (manual), Original (DOS) |
| `anywhere` | every mode with a party except `Title` and `Combat` | Remaster |

- The option gates **every** action of §3, not only the stash: the original has no member-to-member move or wear choice
  outside town either (III's T)rade is also a town command, III PDF 6).
- In both values the actions are refused while the D)ISTRIBUTE screen is open (`GameSession.ItemDistribution`, its pool holds
  the items) and while a found treasure waits for "KEEP IT?" (`PendingTreasure`).
- **The Guild screen** is inside a town but has no D)ISTRIBUTE. Decided (user, s75: "let me adjust inventory anywhere"):
  both values allow it, so `town` = Town + Guild. The other refusals stay (open D)ISTRIBUTE, waiting treasure, no party).
- No `DIFFERENCES.md` entry: this is a QoL option, not a manual↔code question.

## 5. Reason string keys

`InventoryRefusal` → `inventory.refuse.<key>`; the Client owns the English text. Suggested text in the DOS message style:

| Key | Suggested text |
|---|---|
| `not_here` | ITEMS CAN ONLY BE MOVED IN TOWN OR THE GUILD. |
| `not_in_party` | NOT IN THE PARTY. |
| `no_item` | NO ITEM THERE. |
| `not_equipment` | THAT CAN'T BE WORN. |
| `unusable` | UNUSABLE |
| `carry_full` | {0} CAN'T CARRY MORE. |
| `stash_full` | THE SACK IS FULL. |
| `same_member` | ALREADY CARRIED BY {0}. |
| `nothing_worn` | NOTHING WORN THERE. |

`UNUSABLE`, `CAN'T CARRY MORE` and `THE SACK IS FULL` are the DOS words (Armory, D)ISTRIBUTE).

## 6. The Client screen (presentation option `GameSettings.InventoryScreen`)

Off (default) = the original screens: `V` shows the old item overlay, D)ISTRIBUTE unchanged (Xvfb screenshots
pixel-identical to main). On = `V` opens `InventoryScreen` (`src/OpenPhantasie.Client/InventoryScreen.cs`, drawn by
`UiOverlays.Inventory.cs`): member tabs, the paperdoll (Amiga race portrait + one box per `EquipmentLayout` slot), the 9
carried slots with their holes (row = raw slot), the stash (filter ALL / each slot / OTHER, sort FOUND / NAME / KIND; rows
keep their raw sack slot), and the item under the cursor (UNUSABLE, `CompareGear` before>after, `Describe`). Outside
`InventoryAccess` the screen is read-only and shows the reason.

Every move goes through the `GameSession` wrappers. Drop table (`InventoryScreen.Plan`, tested): carried/worn → own box =
Equip; → another member's box = Transfer + Equip (both checked first); → own list = nothing (a worn box dropped on the own
list = Unequip); → another member's tab or list = Transfer; → stash = ToStash; stash → box = EquipFromStash; stash → tab or
list = FromStash.

Remaster choices, no manual or DOS rule (decided by the user, s75):
- **A drop on a box of another slot is refused** ("THAT GOES IN THE SHIELD BOX."), not worn in its own slot. Keep.
- **A summoned elemental has no tab** (every move refuses it, `not_in_party`): elementals can't be equipped. Keep.
- **The Guild is editable under `town`** (§4; rejected the earlier read-only Guild). `--test-party` starts there.
- **Full gamepad support inside the screen** (rejected the earlier keyboard/mouse-only screen;
  `InventoryScreen.HandleButton`, the shell feeds `InputMap.NewButtons`). Every key has a button: D-pad / left stick =
  arrows, A = Enter (pick up / place), B = Esc (put back, then close), Y = E (wear / take off), Back = U, right trigger = S
  (sack), Start = G (then LB / RB pick who and A gives, the 1–6 step), LB / RB = previous / next member (Tab / 1–6), left /
  right stick click = F / O (filter / sort), right stick up / down = PageUp / PageDown; X (V) closes. After a button
  press the help line shows the button hints (`inventory.help*_pad`); a key press switches back.
- `not_here` reads "not now" (`inventory.not_now`) whenever the place is allowed but something else blocks (an open
  D)ISTRIBUTE, a waiting treasure), not only under `anywhere`.

## 7. Modern inventory — rules option `modern-inventory` (QoL, user s178)

**User decisions (2026-11-09, final):** "full modernized and not the insane original system where you have to re-equip
every time you went into town". One switch per save for I, II and III (`EveryGame`, `Games` I / II / III; IV has its own
equipment screens), QUALITY OF LIFE section: `off` in both Original presets, `on` in Remaster. Locked per save like every
rules option. Answers given: the town redistribution goes; a new item is worn by itself **only into an empty slot**;
selling only in town; one option, not separate toggles.

With `on`:

| What | Original (`off`) | Modern (`on`) |
|---|---|---|
| Where the moves of section 3 work | `p1.stash-access` | anywhere outside combat (section 4's `anywhere`; overrides `p1.stash-access`) |
| D)ISTRIBUTE (Town D, Inn, III Option / bar) | gold / XP split, then the hand-out screen pools **every** carried and found item and strips all worn gear | gold / XP split only (I / II); `GameSession.BeginItemDistribution` refuses (`ItemHandOut` false), the Client says "Items stay where they are. Press V …" and III skips its "redistribute?" question |
| III town entry (D3-071) | ready-best for every character, then the found-item hand-out screen, then "Shares for" | no ready-best, no hand-out (found items wait in the stash); the last wand still leaves the found list (ovl2 `0x5a9`, a game rule) and the "Shares for" prompts stay |
| Armory buy | I / II wear the bought item when it is at least as good (`AutoEquipIfBetter`); III wears nothing | worn only when nothing is worn in its slot and the member can use it |
| Give (Transfer, III Trade give), take from the stash | not worn (s75); III Trade re-readies giver and receiver | worn only into the receiver's empty slot; the giver is not re-readied (an emptied slot stays empty) |
| Selling | Distribute's SELL row; III Trade sell | inventory screen **Sell** in a town (`GameMode.Town`): carried, worn or stash item, the game's Distribute price (`ICampaign.DistributeSalePrice`, I / II `ItemRules.SellPrice`, III 75 % of the town's price), the town's stock +1 (max 99), the treasury sweep after it |
| V | the old item overlay unless the Inventory screen switch is on | always the inventory screen (I, II, III) |

"Arrival-only" wear (`InventoryActions.WearIfSlotEmpty`, `GameSession.WearArrival`): the arriving item is worn when its
layout slot holds 0, the member carries it and the game's gate passes; nothing else moves. The game's own best-gear pick
(`ICampaign.ReadyBestEquipment`) is untouched and still runs wherever the original runs it with the option off.

**Client (Sell):** L (pad LT) on the item under the cursor, a click on the detail box's "SELL FOR N GP (L)" line or a drop
on it asks "SELL X FOR N GP? (Y/N)"; Y / Enter (pad A) sells, any other key, button or click keeps it. The help line shows
L only where selling is possible. Refusal `no_shop` ("ITEMS ARE SOLD IN TOWN.") outside a town or with the option off.

**Left as it is:** III's own Trade dialog (`p3.trade`) stays in the town Option list as its value says; the inventory screen
gives (anywhere) and sells (in town) whatever `p3.trade` is, so Trade is available at all times (D3-027). A full stash refuses a found item at "KEEP IT?" as before (it no longer empties on every town
visit; the stash is usable anywhere). Seen live on Xvfb (I, s178): the sell line, L / Y, the D note.
