# Town Menus (Armory, Bank, Mystic, Inn, Guild, Temple)

Owned by `OV1.OVR`. Towns are "menu towns" — no walking, just menus. There are
**12 towns** (`data-tables.md`: PELNOR #1 … OLYMPIA) plus town **#0** where the
elementals live (not on the Material Plane). Every town offers the same
facilities. Static interiors + names: `TWNS.INT`; mutable per-town state
(mainly Armory stock): `TWNS.DAT`.

Top-level menu strings (`PHANTASI.COM`): `Guild, Armory, Bank, Mystic, Inn`
(+ `Exit`). Facility sub-menus come from `OV1.OVR`.

**Town screen [C] (DOSBox s49).** A highlight bar on screen rows 0..8 — label table `PHANTASI.COM 0x5e6e` in the
order **Option Bank Mystic Exit Armory Inn Guild** (inverse-video highlight, arrows + RETURN) — over the town picture,
with the town name centred on rows 191..199. The picture is **`PELNOR.IBM`**: a raw CGA 320×200 2 bpp screen
(16384 B; even rows at `0x0000`, odd rows at `0x2000`, 80 B/row; palette 1 high intensity), art on rows 9..190, the bar and
name rows blank. OV1 names only this file (`pelnor.ibm`), so every town shows the same picture `[~]` (other towns not
seen live). Extract: `tools/scripts/extract_dos_town_picture.py` → `dos/extracted/ui/town.png`. Option → Cast spell / Use
item / Show party / Save game / Auto save / Sound / Printout / Exit (§Temple). Highlight on the first visit `[~]`
(the Client starts on Option). **D-066 (user): both** — the bar plus the manual's letters (A Armory, B Bank, M Mystic,
N Inn, G Guild, L leave town; C cast, U use item, I inspect, D distribute, S save). **Guild keys (user):** the manual's
Guild menu (p.4) — L list, P purge, N new, R rename, A add; party options S spells, T train, D drop; E exit. Train and Learn
are Guild options (as in DOS), not on the town bar.

## Guild [X] — see also `character-creation.md`
Anchors: "guild.dat" `0x5bdf`, "INPUT NAME:" `0x5c08`, "[original text]"
`0x5bf4`, drop/purge/rename/add strings `0x5cfa`–`0x5d33`.
- **New member** (create), **Add** (to party, max 6), **Drop** (from party back
  to roster — keeps guild id), **Purge** (delete from roster), **Rename**,
  **List members** (guild #, name, class, residence).
- **New game [C] (s50, OV1 `0x7dd(1)` branch `0x8ae`):** every roster slot's `@218` := 0 (nobody in the party), town
  index `[0x64ee]` := 0 (Pelnor), `0x8e1(0)` Armory churn, then the town loop — the game opens on the Pelnor town menu
  with an empty party; the Guild is a town-menu building. `0x7dd(2)` (continue) re-copies the `@218` members into the
  party (remaster: D-069 manual Continue by Guild number, several parties — `save-file-format.md` §Several parties and
  Continue); `0x7dd(0)` calls `0x498f`. `[~]` whether `0x8e1(0)` churns here or returns on `[0x63e8]` (Core skips it).
  Core: `GameSession.StartNewGame` / `OpenGuild` / `LeaveGuild` (Guild Exit → town menu).
- **New member flow [C] (s50, OV1 `0xbac..0xfa2`):** race menu (COM menu 5; cancel returns to the Guild) → class →
  attribute roll → Y/N (`0xfa3`, menu 8). **N** frees the slot (`@92` := 0, `0xf6c`) and restarts at the race menu
  (`0xbb2`); **Y** → INPUT NAME `0xfd3` (repeats until unique) → full sheet. Core: `RollNewMember` + `TryEnrollNewMember`
  (RNG order unchanged — the name draws no rand).
- **Purge [C] (s50, `0x23d5`):** "[original text]" by Guild number (1..22, `0x3766`), no confirm; clears only
  `@218` (`0x2416`), `@256` (`0x242c`) and `@92` (`0x2440`) of the roster record — name and stats stay, but an `@92 = 0`
  slot is empty to DOS (`0x0C59`) — and the matching party slot, then asks again until cancelled.
- **Rename [C] (s50, `0x22f6`):** "[original text]" by Guild number; if the member is in the party, the party copy is
  synced to the roster before INPUT NAME and back after — **party members can be renamed** in DOS. **Remaster (D-065,
  user): the manual** (p.4: "Characters cannot be renamed while they are members of a party") — `GameSession.TryRename`
  refuses a party member; allowing it is a later QoL option.
- **Training** ("[original text]" `0x5c22`): XP threshold + gold (only gold is spent; XP is cumulative) to gain
  a level. `OV1 0x1160..0x148d` rejects if status==0 or level≥15. [C] Only DEAD is refused — a SLEEPing member trains
  (`0x119b` `cmp [bx+0x18], 0`); manual silent on status, Core matches.
  **XP threshold and gold cost — whole chain [C] (s39 static decode, reproduces all 13 live captures).**
  Six stat/multiplier combinations, selected by class **[C] (s11, static)**: the
  `jmp cs:[bx+0x669a]` table at OV1 `0x12ca` sits at file **`0x12be`** (this code's
  CS delta is `0x53dc`, not `0x3c26`):
  `0x11ea, 0x120e, 0x1233, 0x1258, 0x1279, 0x129a`, index = class `@220` (read at `0x11e3`).
  | Class | Handler | Stat sum | Mult (inline double, OV1 file offset, bit pattern) |
  |---|---|---|---|
  | THIEF | `0x11ea` A | `DEX*2 + STR` | 19.8 `0x11fa` `0x4033CCCCCCCCCCC0` (−13 ulp) |
  | MONK | `0x120e` B | `INT + DEX + STR` | 49.5 `0x121f` exact |
  | RANGER | `0x1233` C | `INT + CHA + STR` | 52.8 `0x1244` `0x404A666666666660` (−6 ulp) |
  | FIGHTER | `0x1258` D | `DEX + STR` | 16.5 `0x1266` exact |
  | PRIEST | `0x1279` E | `INT + STR` | 18.1 `0x1287` `0x4032199999999980` (−26 ulp) |
  | WIZARD | `0x129a` F | `INT*2 + @10` | 42.9 `0x12ab` `0x4045733333333320` (−19 ulp) |
  Chain constants: 1.8 `0x12df`/`0x1304` `0x3FFCCCCCCCCCCCC0`, −0.8 `0x1345` `0xBFE9999999999980`, 1000 `0x1353`,
  1e−5 `0x136d` `0x3EE4F8B588E368C0`, 2.2 `0x13a4` `0x4001999999999980`.

  **FPU-emulator helpers [C] (s39).** Software float library in `PHANTASI.COM` with two 64-bit-mantissa registers,
  "acc" (`[0x3156]`) and "second" (`[0x3158]`). OV1 near target T → unpacked COM image offset **T − `0xac24`**
  (`re_common.unpack_phantasi_com()`; T − `0xa934` is the packed-file window delta, not the image offset):
  | T | op | T | op |
  |---|---|---|---|
  | `f6ce` | acc = (double) int AX | `f734` | acc = (double) long DX:AX |
  | `f11e` | acc = inline double (8 bytes after the call) | `f0ef` | second = inline double |
  | `f020` | acc = float [BX] | `f019` | second = float [BX] |
  | `f142` | acc = double [BX] | `f08c` | float [BX] = acc |
  | `f19f` | double [BX] = acc | `f1fc` | swap acc ↔ second |
  | `f267` | acc += second | `f260` | acc −= second |
  | `f506` | acc *= second | `f3e1` | acc /= second |
  | `f1dd` / `f1d4` | push acc / second on the CPU stack | `f1e6` / `f1f1` | pop into acc / second |
  | `f765` | DX:AX = (long) acc, truncating | `f220` | compare acc with second (flags) |
  OV1 `0x51f3` = **`pow(x, y)`** (x ≤ 0 → 0 / error 0x16 = EDOM; `exp(y·log x)`, `> 709.778` → error 0x15 = ERANGE).

  ```
  train(member):                                         # OV1 0x1160
      if status == DEAD or level >= 15: "[original text]"
      s  = (float)(statSum / mult)                       # 0x11f4..0x1208, [bp-4] is a float
      F  = (float)1.8                                    # 0x12d7
      for i = 1; i < level; i++: F = (float)(F · 1.8)   # 0x12ed..0x1318 → F = 1.8^level
      if XP(@266) == 0: F = 1.0                          # 0x131a..0x1339 (any level)
      X  = (F − 0.8) · 1000 / (s + 1e−5)                 # 0x133c..0x1381
      v  = (float)(XP − X)                               # 0x1387..0x139e → [bp-4]
      g  = (long)pow(level, 2.2)                         # 0x13a1..0x13c7, level = current @88
      k  = max(20 − CHA, 0)
      gold = min(15000, g · k² · town.price_index / 100) # 0x13ca..0x1451, 32-bit; negative kept (cannot happen)
      n  = (long)v                                       # truncate toward zero
      check(purse, n, gold)                              # 0x3b35:
          n < 0  → "You need" / "%ld more" (−n) / "[original text]" / "GO AND LEARN"; no training
                   # Remaster (D-050, user s40, manual): also "It will cost %d gold" here
          else   → "It will cost %d gold", yes/no; purse (u16) < gold → "[original text]"
                   else "[original text]"
      purse −= gold; levelUp(@88, rec)                   # 0x1471..0x1487 (OV1 0x9ee, §Level-up gains); XP is not spent
  ```
  - Effective rule: the character may train when `XP ≥ floor(X)`; "You need N more" = `floor(X) − XP`.
    Level 1: X ≈ mult·1000/statSum (the old level-1 formula); XP 0: X is **0.2 ×** the level-1 value whatever the level.
    Level L: X = (1.8^L − 0.8) × the level-1 value (L2 ×2.44, L3 ×5.03, L4 ×9.70 …). XP is cumulative.
  - **Live data check (s5 + s35, 13 points, all level 1):** XP 40 → WIZARD 957, PRIEST 1091, THIEF 399, FIGHTER 475/1229/549;
    XP 0 → MONK 282, RANGER 319; XP 141 → MONK 1273, RANGER 1458; XP 181 → PRIEST 950, FIGHTER 334/1088 — the chain
    reproduces every one (Core `TownMenusTests.TrainingXpShortfall_MatchesAllLiveCaptures`).
  - Thief 439 / Ranger 1599 come from the **`+1e−5`** term (and the float store of `s`),
    not from the stored-double ulps. A scratch sweep over stat sums 3..66 × 6 classes × levels 1–14 × (XP 0 / XP > 0)
    gives the same `floor(X)` with decimal and with stored constants (10752 cases, 0 differences). Core uses the stored
    bit patterns anyway.
  - **Gold base:** the long fed into the gold tail is
    `(long)pow(level, 2.2)` = 1, 4, 11, 21, 34, 51, 72, 97, 125, 158, 195, 236, 282, 332 for levels 1–14. XP never enters
    the price. Manual agrees in outline ("A character's charisma affects the price of training"). **[D✓] s41:** 9 live
    "It will cost N gold" values (13/54/148, 150/600, 54, 337, 433/1734 — levels 1–3, CHA 3–17, Pelnor) and 4 level-2..4
    shortfalls (2692/2000/96/903) all match (`working/capture_s41_2026-10-15.md` §1). Box labels **AGREE / NOT**; purse too
    small → "[original text]" (nothing taken); success → "[original text]"
  - `[~]` precision: the emulator keeps a 64-bit mantissa between ops and its float store `f08c` rounds its own way;
    Core uses IEEE double + C# `(float)` (round-half-even). Can differ only when `v` lands within ~1 ulp of an integer.

### Level-up gains [C] (s11, OV1 `0x9ee`)
The **same routine** builds a new character (`0xdde`, `L = 0`) and trains one
(`0x1484`, `L` = current level `@88`). Tables are `TWNS.INT` rows `class*15 + L`:
`hp_factors @0x000`, spell levels `@0x05a` (60 bytes/class), MP ceiling `@0x1c2`,
skill coefficients `@0x21c` (10 × u16 per class/level).
```
levelUp(L, m):
    if L >= 15: return
    m.mp_ceiling(@14) = mp_ceiling[class][L]                  # MP max (@16) is NOT raised here — only the Inn raises it
    for i in 0..9:                                            # skills @30..@48
        t = ((skill_coeff[class][L][i] + m.skill[i]) >> 1) + m.skill_bonus[i](@50+2i) - 49
        m.skill[i] = max(m.skill[i], min(t, 255))             # never decreases
    if m.hp_max(@20) < 255:
        dice  = (hp_factors[class][L] * (CON + 5)) >> 4
        roll  = sum of `dice` × (rand/10923 + 1)              # d3 each
        m.hp_max = roll > m.hp_max ? min(roll, 255) : m.hp_max + 1   # HP current (@22) unchanged
    m.level(@88) = L + 1
    for s in 0..59:
        if spell_level[class][s] == L + 1 and spell_level[class][s] > 0 and m.spells[s](@96+2s) == 0:
            m.spells[s] = 2                                   # "can learn" (1 = known)
```
- HP max is **re-rolled from scratch** each level; a low roll still gives +1.
- Skill bonus `@50+2i` was seeded at creation (= `2·linkedAttr + 30`, `character-creation.md` §Skills).
- Spell-level table check: MONK/WIZARD FIREFLASH I (index 4) and RANGER/PRIEST HEALING I
  (index 0) are level 1, matching the creation spells.
- **Learn spells** — Guild S)PELLS, OV1 `0x1490..0x1677` + picker `0x3455`/`0x3553` **[C] + [D✓] s63** (DOSBox, every text,
  fee and count below seen live):
  ```
  loop:
    m = pick "WHO WILL LEARN?" (`0x5c62`; every party member, dead ones too)   # Esc → Guild menu
    if m.status(@18) == DEAD: n = -1
    else:
      cap = trunc(level(@88)·INT·1.3/90·class + 3·(class == FIGHTER) + 1)   # x87, in this order; class THIEF 0 … WIZARD 5
      known = #spells(@96+2s, s < 60) == 1;  marked = # == 2
      n = min(marked, cap - known + 1)                                    # so a member can end up knowing cap + 1
    fee  = town.price_index · level · 90 / (CHA · INT · (9 - class))      # long, truncated; not the MP cast cost
    ok   = rand/1638 < INT                                                # rolled here, before the list, every pick
    if n <= 0: box "[original text]" (key); continue
    box "You can learn %d more spells." / "The cost is %d GP." / "Available spells:" + the shared spell picker
        listing every spell marked 2 (names only; all of them, even past n)          # Esc → loop, nothing charged
    if fee > party gold carried (unsigned): "[original text]" (key); continue      # nothing taken
    gold carried -= fee;  m.age(@264) += 2                                # also on a fail
    if ok: m.spells[s] = 1; "You've learned" (no name)  else: "You've failed" (spell stays 2)
  ```
  "[original text]" (`0x5c3e`) is **Training's** text, not Learn's. The fail chance is ≈ INT/21. The
  manual instead numbers the spells after "POSSIBLE SPELLS:" (empty = nothing to learn) — `DIFFERENCES.md` D-074, decided: manual (built).
  Core: `TownMenus.SpellLearnCount` / `SpellLearnRoll` / `LearnSpell`; Client `LearnSpellDialog`.
- New characters cannot train/learn or leave the Guild until added to a party. [X]
- OV1+`0xbac` (New Member, above) zero-inits the whole 282-byte record, rolls stats and assigns the next guild id.
  OV1+`0x1490` is Learn spells (above); an older static pass read it as an Inspect/list-member display (Level, a `+2`
  field, Class, no state change).

## Armory [X]
Anchors: "WHO WILL SHOP?" `0x5ca2`, "[original text]" `0x5cb1`,
"UNUSABLE" `0x5ce5`, list header "NAME CLASS WPN ARM SHL #" `0x5dfb`.
**Handler entry: OV1 file offset `0x1bcc`** (a direct-call target of the facility dispatch, like Bank/Mystic/Inn). It
reads inventory slots `[+0xde]`, calls the price routine `FUN_0000_1d5b` and checks/deducts the party purse (`0x63f6`).
- **Buy only — the Armory has no sell [C] (s58 DOSBox, `capture_s58_2026-10-25.md` §3 (D), shot `ar4`).** Picking a
  shopper at "WHO WILL SHOP?" goes straight to the buy list ("<name> is shopping / Cash -> n", items with GP/PTS); Esc
  returns to "WHO WILL SHOP?". The manual's Armory menu (PDF p.11) is L)IST, C)ONTINUE, B)UY, A)NOTHER SHOPPER,
  E)XIT ARMORY — no sell either. Selling is only the Inn's D)ISTRIBUTE AND SELL ITEMS (§Distribute, SELL row, D-068),
  which takes the item from the pooled hand-out list, not from a member's slot. The remaster has no Armory sell. Stock is per-town and persists in `TWNS.DAT`; every
  town entry churns it (§Armory restock below). Items a class/STR can't use show as **UNUSABLE**.
- **The list starts at id 1 [C] (s41):** the lister (OV1 `0x3e2d`) and the pick → id mapper (`0x3ee8`) both begin at
  stock slot 1, so slot 0 (NOTHING) is never shown or sold even when non-zero (PHANTASIA's restock list is all zeros,
  so its restock writes `stock[0] = 1`). Core: `TownState.FirstListedItemId`, `TownMenus.Buy` refuses id 0.
- **9-item limit [C] (s30).** Before choosing an item, the Armory (OV1 `0x1c16`) counts the shopper's non-zero slots
  `@222..@238`; at 9 it prints "[original text]" (`0x3d14(1)`; `0x3d14(0)` = "You can not afford it.")
  and leaves. The same check runs after each purchase. The generic give-item routine OV1 `0x2e03(member, id)` also
  refuses a member with 9 items (count helper `0x2dc4`); member −1 = the found-item list `0x63fa` (60 slots,
  D-018). Its only caller, the item hand-out screen `0x2d22`, lets the player pick the recipient per item and asks
  again when the pick is refused. Core's Inn distribute round-robins instead, skipping full members; items that fit
  no one stay in the sack `[~]`.
- **A bought item goes into the first empty slot [C] (s58 static).** OV1 `0x1c18..0x1c2e` scans `@222..@238` for the
  first 0 word and the purchase is written there (`0x1cef`). The "9 items" test counts non-zero slots, so an empty slot
  left by a drunk potion (`magic-spells.md` §Potion use) is free space and is refilled before any later slot.
  Core: `CarriedItems.IsFull` / `CarriedItems.Put` (Buy, and the town-entry round-robin hand-out).
- **Town Exit with no party [C] (s30):** OV1 `0x3250` — if party size `[0x648e]` = 0, print "You must assemble a
  group." and stay in town. An all-dead party may leave.
- **Buy price is the full list price** from `data-tables.md`; there is no CHA
  discount. [C] The buy path calls `FUN_0000_1d5b` then subtracts its full
  result from the shared purse at `DS:0x63f6`.
- The `GUILD @262` word is each member's **bank balance** (s35). The pass-2 `256→197` on all three records is the
  save-time share split depositing the leftover purse: `(3×256−177)/3`, after four purchases worth 177 GP. [C]

### Armory restock on town entry [C] (s41, OV1 `0x8e1`)
`0x8e1(t)` is called at the end of the town-entry pass `0x1954` (`0x1ba4`, after aging and the roster write-back) with
the 0-based town index `t = [0x64ee]`; its other caller is the new-game / load path `0x8da`.
```
[0x63e2] := t;  0x4299(t)                         # (0x4299 sets [0x6368] to record t−1, 99→11; overwritten next)
                                                  # [0x63e2] gets the 0-based t here; victory/defeat set it to 1 (1-based Pelnor) — endgame.md §3
rec := [0x6366] + 291·t;  [0x6368] := rec         # the town's TWNS.DAT record
if [0x63e8] != 0: return                          # set only by 0x1e15(arg ≠ 0) = new game / load; 0x1954 clears it (0x19c4)
repeat 18:  rec.stock[rand / 274] := 0            # rand 0..32767 → ids 0..119; whole stack, repeats allowed
L := [0x63ec]                                     # average party level at the last town exit (below)
repeat 4:
    i := L != 0 ? rand / (0x7fff / L) : 0         # signed divides; no rand drawn when L = 0
    rec.stock[ rec.word[0xb5 + 2·i] ] := 1        # set, not add: a 5-stack hit here becomes 1
```
- **`[0x63ec]` [C]** is written only by the town Exit pass (OV1 `0x3269..0x32fc`): `Σ level @88 / [0x648e]` over the
  party slots, truncated — the same loop that sets `@28`, `@260` and `[0x63ea]` (max `@70`). It is stale until the
  next exit (party defeat does not touch it). OV2 `0x3c84` (combat surrender odds) is its only other reader.
- **Restock list [C]:** TWNS.DAT record `+0xb5`, **45 u16 item ids** (bytes 181..270, previously "unmapped"). The 11
  shop towns carry the same list, ordered roughly by level: MACE, LEATHER, SM AXE, SM SHLD+2, HEALING 5, MAGIC 5,
  FLAIL, H LEATHER, MD SHIELD, DAGGER+2, HEALING 6, MAGIC 6, … SWORD +5/+6, GNT SH +2/+3, RING ML+2, HEALING10; also
  GOLD KEY (172) and EMERALD E (155) at indexes 36/37. PHANTASIA (index 7) has 45 zeros. `i ≤ L ≤ 20` (training
  stops at 15; the party-defeat UNDEAD fate sets level 20, `endgame.md` §4) so the word read always stays inside the
  list; note `i` can equal `L` (`rand` near 32767).
- **What can disappear:** ids 0..119 = all weapons, armor and shields (0..100), HEALING 1–10 (101..110) and MAGIC 1–9
  (111..119). MAGIC 10 (120), scrolls (121..140), rings, wands, gems, keys (141..180) are never wiped. This explains the
  s35 DOSBox observation (SM SHIELD 5 → 0 with no purchase).
- **What can appear:** only list entries `0..L` (with `L` from the last exit), each at quantity 1.
- **Manual (PDF p.10, Armory, Playing Hint):** "Rings, scrolls, and other treasures found in the Armory stay there
  for future visits. Combat weapons and potions, however, may appear and disappear between visits." The manual's own
  taxonomy (PDF p.21, TREASURES): "Combat equipment includes shields, armor, and weapons." DOS agrees on scrolls,
  rings and treasures staying. DOS also wipes **armor and shields** (ids 0..100 are all
  combat equipment; s35 DOSBox saw SM SHIELD 5 → 0), while the manual names only "combat weapons and potions".
  **D-054 (decided: manual):** wipes skip armor and shields. A restock draw on a sold-out armor or shield sets it to 1;
  a draw on one still in stock leaves it alone (s57, so bought-out armor comes back). `dosRule: true` = DOS rule (P3 toggle).
  Observations that fit the manual's "may": MAGIC 10 never disappears; GOLD KEY and EMERALD E can *appear*.
- Core (s41): `ArmoryRestock.Apply` run by `GameSession.EnterTown` (after `Aging.ApplyTownEntry`),
  `GameSession.TownExitAverageLevel` (`[0x63ec]`, set in `LeaveTownGlobals` with `PartyOptions.AveragePartyLevel`,
  saved in `WildernessSnapshot`), list in `towns.json` `restock_items` (`extract_towns.py`) → `TownState.RestockItems`.
  `[~]` Core's average counts `Party.Adventurers` (an elemental present at Exit would count in DOS, `[0x648e]` = 7).
  Party defeat with no survivor returns 1 → state 1 → OV1 `0xb4` → `0x1e15(0)` → `0x1954` with `[0x64ee] = 0`, so DOS ages
  the (empty) party and **restocks Pelnor** — **[D✓] s63** (18 ids wiped, list[0] set to 1); Core `ResolvePartyDefeat` →
  `EnterTown(1)`. `endgame.md` §1/§4.

## Bank [C — handler located and disassembled, session 7]
Anchors: "GOLD CARRIED %u" `0x5dd8`, "[original text]" `0x5de8`,
menu "Withdraw / Deposit".

**Handler entry: OV1 file offset `0x2919`**, a direct `call` target of the facility dispatch (guarded like Mystic/Inn:
`cmp [0x648e],0; je ...; call 0x2919`). Found by disassembling the body (capstone `CS_MODE_16`) and matching the
money flow; the runtime-populated jump table can't be recovered statically:

```
push word ptr [bx+0x5cb6]     ; member's record +0x106 (disk @262)
call 0x35df                    ; amount-entry prompt ("GOLD CARRIED %u")
...
sub word ptr [bx+0x5cb6], ax    ; member field -= amount
; 32-bit-safe add into the party purse:
mov ax, word ptr [0x63f6]
... (cx:bx widen, add, adc) ...
mov word ptr [0x63f6], ax       ; party purse += amount (low word only — see
                                 ; Q1/Q2 32-bit-gold note below in Remaster note)
```

Record offset `+0x106` (disk `@262`) is the bank-balance field the deposit/withdraw UI works on, the same field as the
Armory notes' per-member "share of carried gold" (one field, two facilities). [C]

- **Whole handler decoded [C] (s39, static).** Menu choice from COM `0x08d3` (`0x2932` → `0x29fc`):
  ```
  bank():                                                  # OV1 0x2919
      choice = menu(Withdraw / Deposit / Exit)
      0 Withdraw:
          if count(6 records with +0x5c != 0) == 0:          # 0x2519(6); +0x5c meaning [?]
              beep; goto Deposit                             # 0x29ee → 0x29f1 [~] (path seen only statically)
          loop:                                              # 0x2944
              show "GOLD CARRIED %u" (purse [0x63f6])
              m = pick "[original text]"; ESC → return    # no split on ESC
              a = editAmount(m.@262)                         # 0x35df: "[original text] %5u", 5 digits,
                                                             # refuses a > @262 (unsigned) and text > "65535" (0x58c5)
              m.@262 −= a
              purse = clamp65535(purse + a)                  # COM 0x2e5d: 32-bit sum, > 0xffff → 0xffff
      1 Deposit:   shareSplit()                              # 0x29f1 → OV1 0x167d, then return
      2 Exit:      return
  shareSplit():                                            # OV1 0x167d — also run on town entry and Save game
      if party empty or (purse == 0 and xpPool [0x63f4] == 0): beep; return
      shares[i] = prompt "Shares for:" (0x42cb; 1/2/3, DEAD 0/1/2/3 — s35 DOSBox; the "You have" XP shows xpPool·10, 0x4315 [C] s41)
      S = Σ shares (0 → 1)
      xpShare = (xpPool · 10) / S                            # 32-bit — xpPool is kept in tens (combat-resolution.md §Outputs, kill-XP payout [C] s41)
      goldShare = purse / S                                  # 16-bit div; remainder dropped
      show "Each share is …" (0x44fc)
      for each member i: @262 = clamp65535(@262 + shares[i]·goldShare); @266 += shares[i]·xpShare  (32-bit)
      purse = 0; xpPool = 0
  ```
  So **Deposit is not an amount entry**: it is the share split, exactly as the manual describes ("split the party's gold
  among the individual members … how many shares each party member gets (1, 2, or 3)"). Core has no amount deposit
  (D-049, user: manual); `TownMenus.DepositShares(party, shares)` implements the DOS/manual split for gold
  (the XP pool lives in Core's `LootSack` and is paid on town entry, D-037). Withdraw = Core's `Withdraw` (amount ≤ balance).
- **Withdraw amount entry [C] (s58, static; DOSBox check pending).** `0x35df` prints "[original text] %5u" and calls
  the field editor `0x3629` with the member's balance `@262` as the starting value, so the 5-digit field opens **filled
  with the whole balance**. Keys (manual silent → DOS):
  - **Return** accepts the value shown — an immediate Return withdraws everything.
  - **Digit** shifts in from the right (whether the leftmost digit drops out, `(v·10 + d) mod 100000`, is **[~]**); if the result is above
    `@262` or above 65535 (text compare with "65535", `0x58c5`), **beep and undo that digit**.
  - **Backspace / Del** shifts right and puts `0` in front (`v / 10`).
  - **No Esc case**: the entry cannot be cancelled. The way out is to clear the field to 0 and press Return — a 0
    withdrawal changes nothing and the loop goes back to "[original text]", where Esc leaves.
  - On-screen padding of the field while editing (spaces as `%5u`, or the editor's zeros) **[~]**.
  - **[D✓] s58 DOSBox** (`capture_s58_2026-10-25.md` §3 (A), `bk2..bk15`): the field opens on the whole balance (11293);
    a digit that would exceed it is dropped; Backspace shifts right; Esc does nothing; Return withdraws the value shown
    and the screen goes back to "[original text]" (the next member, priesto, was picked there and withdrew all with an
    immediate Return). Every withdrawal — 0 included — returns to the picker; only Esc at the picker leaves.
  Remaster (`WithdrawDialog`): "[original text]" (with GOLD CARRIED) → amount, same keys; Esc is ignored in the
  amount field; Return (0 or not) goes back to the picker, Esc there closes; the beep is not played (no sound yet).
- **One member-list cursor for WITHDRAW / SHOP / USE [C] (s58 DOSBox §3):** the Armory's "WHO WILL SHOP?" opened on
  priesto, the row last picked at "[original text]" (`ar2`); "WHO WILL USE?" then opened on crusho, the last Armory row
  (`po6`). The potion target list "ON WHOM?" started on row 1 (`po10`), so it is not part of it. Remaster: one
  `MemberCursor` row shared by the Bank's [original text], the current shopper (the Armory's 1-6 keys) and Option →
  Use item's WHO WILL USE?; "ON WHOM?" stays digit-picked.
- **Money-bug siblings [C] (s39)** — Phantasie II's bug (quirks Q1/Q2) is gold silently lost at the 16-bit cap and in
  share rounding. Phantasie I has both, in three places:
  1. **Withdraw** subtracts the full amount from `@262` *before* clamping the purse at 65535 → anything above the cap is
     destroyed (e.g. purse 60000, withdraw 10000 → purse 65535, 4465 gone).
  2. **Share split** clamps each `@262` at 65535 → the excess of a member's share is destroyed.
  3. **Share split** drops `purse mod S` gold (and XP remainder) — Q2.
  Manual says the cap is **65,000** (carry and per account) — DOS clamps at **65,535**. **D-048 decided (user s40):
  manual.** Core: `Party.AddGold` / `Character.AddBankGold` clamp at 65,000 at every add site (Withdraw, sale, payout,
  Deposit, share split, sack gold) and lose the excess as DOS does (manual silent on the excess). Lifting the cap is a
  later QoL toggle (PLAN P3).

## Mystic [C]
Gives the party an overall **SCORE** (a strength rating) and readiness hints
("novice adventurers", "[original text] …" `0x5edb`–`0x5f35`) that
gauge whether the party is ready for a given area. Pass 4 (live DOSBox-X
capture): **score 2** / "novice adventurers", and **`GUILD` record `@70 = 2`** written on every party member.

**Formula: capstone (`CS_MODE_16`) disassembly of the handler body, `OV1.OVR` file `0x20bb..0x225a`**, byte-verified.
The dispatcher `FUN_0000_1e72` reaches it through the runtime table at `OV1+0x746b`, so Ghidra drops it as an
"unreachable block" (as with the `CS:0x669a` training table and the OV2 monster table):

```
base = 0
for each party member with status == 1 (OK)         // record +0x18 = GUILD @24
                                                     //  (0x2100 cmp [bx+0x18],1; DEAD/SLEEP skipped;
                                                     //  UNDEAD is a race w/ status OK, so it counts)
    base += member.Level                              // record +0x58 = GUILD @88
    (and only these members' items feed heldIds / the rune check below)

heldIds = union of the counted members' carried-inventory ids (record +0xde,
          up to 9 slots/member — same field the INN "Distribute" handler
          mirrors from SACK.DAT)
runeFlags: for k in 0..3, if member word +0x48+2k != 0 → flag k (GUILD @72..@78);
          all four set → extra argument to the tier-message selection at 0x21cd (below)
for id in 141..150 (inclusive, checked as 140 < id < 151):
    if id in heldIds: base += 18                       // one of ten flags, index = id-141
if 150 (WAND) in heldIds: base += 66                    // extra, on top of its own +18

score = floor((base + 1) / 3)
```

**IDs 141..150 are the RING A..RING I / WAND quest items** (`extracted/items.json`: `RING A`…`RING I` at 141-149,
`WAND` at 150). The wand counts twice (+18 as one of the ten, +66 extra). [C] (static)

Also disassembled but **not part of the numeric SCORE**: the four rune words at record `+0x48..+0x4e` (`@72..@78`, OR'd
across the counted members) give one boolean, "all four set", passed to the message call `0x3327` with the score and the
tier. It only changes tier 4's last line (below). It does not affect `base`, `score` or the stored `@70`. Core:
`Phantasie1Campaign.HasTheTestRunes`.

Readiness tiers **[C] + [D✓] (s49)**: `tier = (score>9)+(score>20)+(score>39)+(score>59)+(score>83)+(score>99)`
(OV1 `0x21d7..0x2221`); `0x3327(score, tier, allFourRunes)` prints "[original text]" / "into his crystal ball and
speaks:" / "Your score is %d. You are" / `table[tier]`, the table being 7 words at OV1 file `0x5ecd` (`[bx − 0x413c]`):
0 "[original text]" · 1 "[original text]" · 2 "[original text]" · 3 "[original text]" ·
4 "[original text]" · 5 "[original text]" · 6 "Supreme Beings.". Tiers 2–4 add a line **"The Gods."**, or
at tier 4 with all four rune words `+0x48..+0x4e` (`@72..@78`) set on counted members **"[original text]"**.
All 7 tiers and the runes line seen live (`capture_s49_2026-10-23.md` §2). The Mystic charges nothing.

The routine writes the score to every active party record's `+0x46` (disk `@70`) **only when it exceeds the current
value** (never decreases). [C] Implemented in `TownMenus.ConsultMystic`.

## Inn
Manual [X]: restores every member's **HP and MP to max**; town inns are **free**
(~2-week stay); independent wilderness inns cost 250 GP + tax. Characters who
cast spells may get their **MP maximum** raised.

### Town Inn rest [C] (s11, OV1 `0x2a0d`)
Field names per `save-file-format.md` (s11 relabel): `@14` MP ceiling for the
class+level (= manual's "Maximum Magic Points" table), `@16` MP max, `@18` MP
current, `@20` HP max, `@22` HP current, `@24` status, `@94` cast-since-rest flag.

```
draw "THE INN" header                                   # OV1 0x3c72
for each active party member m (0 .. [0x648e]-1):
    raised = false
    if m.status != DEAD and m.cast_since_rest != 0:     # 0x2a3a / 0x2a40
        m.cast_since_rest = 0
        roll = rand() / 327                             # rand 0..32767 (COM 0x3c96 LCG) -> 0..100
        if roll < (m.mp_ceiling - m.mp_max) * 20:       # signed; gap <= 0 never raises
            m.mp_max += 1;  raised = true               # "MAGIC INCREASES"
    print "%-10s IS RESTING", then:
        status != OK  -> "IN PEACE"                     # 0x3cd9: DEAD *and* SLEEP members
        else raised   -> "MAGIC INCREASES"
    m.mp_cur = m.mp_max;  m.hp_cur = m.hp_max           # 0x2a7c..0x2a8b — every member, DEAD too
then menu 0x0d; choice 1 -> Distribute (OV1 0x2ab3)
```
- **Free; no time passes and nobody ages** — the handler and its helpers don't touch
  the time counter `[0x63f2]` or `@264` (write-site sweep of all three binaries, s11).
  Manual says a stay lasts about two weeks → `DIFFERENCES.md` D-004.
- Dead members keep status DEAD (their HP/MP fields are still filled). Sleepers are
  **not** woken (shown "IN PEACE").
- At most +1 MP max per member per rest; the chance is `min(gap·20, 101)/101`
  (gap 1 ≈ 19.8 %, gap 5 ≈ 99 %).

### Wilderness (independent) inn [C] (s11, OV2 `0x6700`; re-traced s36)
Strings `"IN THE INN:"` / `"%-10.9s[original text]"` / `"%-10.9sIS RESTING"` /
`"[original text]"` + `"SORRY"` (OV2 string imm = file + `0x433d`).
**Trigger [C] (s36):** stepping onto overworld **tile 41** (step returns 9 → OV2 state table `0xae` entry 9 → call at
`0x4d`, the only caller). 12 tiles 41 on the map. Tile class 11 is the pool, not the inn.
```
menu 0x16 ("AN INN. ENTER?" YES/NO, [D✓] s45); choice != 0 → leave
if party gold [0x63f6] <= 255: "[original text]" / "SORRY"; leave       # unsigned compare
gold -= 256;  print "IN THE INN:"
for each active member m (0 .. [0x648e]-1):
    if m.status == DEAD: skip entirely (no line, no refill)
    if m.cast_since_rest@94 != 0:       # DOS: set by combat casts only; remaster: any cast (D-070, manual)
        m.cast_since_rest = 0
        if rand0_99 <= (m.mp_ceiling@14 - m.mp_max@16) * 20:      # signed; 0x67b7 jg = fail
            m.mp_max += 1; "%-10.9s[original text]"; goto refill
    "%-10.9sIS RESTING"
  refill: m.mp_cur = m.mp_max; m.hp_cur = m.hp_max                 # SLEEP members refilled, not woken
time += 1000 (0x6528);  [0x64fe] = 1 (dead flag [C]: never read — overworld-movement.md)
```
- **[D✓] s45 DOSBox** (OUT1 (7,13), `capture_s45_2026-10-16.md` §3): "AN INN. ENTER?" → "IN THE INN:" / "`name`  IS RESTING";
  both DEAD members had no line and stayed at 0 HP; the others were refilled.
- **[D✓] s49 gold** (`capture_s49_2026-10-23.md` §1b): purse 295 → 39 after a stay (−256); at 39 "[original text]" / "SORRY".
- Needs party gold `> 255`; charges **256** (`sub [0x63f6],0x100`). Manual: "250 GPs
  (plus tax)" → `DIFFERENCES.md` D-005 (not a conflict).
- Members with status DEAD are skipped entirely (no HP/MP refill, unlike the town Inn) → `DIFFERENCES.md` **D-047
  (DECIDED: manual)**: the remaster refills DEAD members too (no line, no MP roll, status unchanged).
- Same MAGIC roll as the town Inn, but `roll = rand()/328` (0..99, OV2 helper `0x655b`) and it succeeds when
  `roll <= gap·20` (town Inn: `<`). **At gap 0 (MP max already at the class/level maximum) roll 0 still succeeds:
  1 % per stay to go one above the manual's "Maximum Magic Points" table** → `DIFFERENCES.md` **D-046 (DECIDED: manual)**: the remaster uses `<`, never past the table.
  Once over, the gap is negative and it can't rise again.
- Advances the time counter by **1000** (`call 0x6528(1000)`). That is 3.9 displayed days (`DAY OUT` = time >> 8) and
  on its own ages nobody (`1000 >> 8 = 3`, `3 / 7 = 0`). **Remaster (D-008, user):** the stay lasts two weeks:
  `@264 += 1` for every party member at the inn, and **no** time is added.
- Gold `[~]` (Core model): DOS has one purse `[0x63f6]`. Core splits it into `Party.GoldCarried` (bank withdrawals)
  and `LootSack.Gold` (found gold). Core tests "> 255" on the sum and takes the 256 from the carried purse first,
  then from found gold.

### Distribute [C] (s53 static, OV1 `0x2ab3` → screen `0x2bed`, give `0x2e03`; s35 DOSBox)
The Inn's Distribute is the item hand-out screen (s35 DOSBox: MAGIC 1 bought for 44 sold for 18). XP and gold are **not**
shared here — see §Town-entry share split. **Printout** prints the party.
```
if party size [0x648e] == 0: return
pool := []
for each party slot i < [0x648e]:                       # DEAD members too
    pool += the non-zero carried items @222..@238 (worn ones included); the slots := 0   # gaps vanish here
    @240 = @242 = @244 := 0;  @82 = @84 = @86 := 0      # nothing worn any more
pool += the non-zero found items [0x63fa] (60 slots); the list := 0
if pool empty: return
sort pool by item id, descending (bubble sort 0x2b79)   # highest id first: god/magic gear, then plain (manual: best to worst)
screen 0x2bed:
    "[original text]"
    row 6 "SELL" + purse [0x63f6] ("%-23s%10u");  row 7 "SACK" + found-item count ("%-32s0")
    rows 8.. one per member: "%-2d%-11.9s%-4.3s%4d%4d%4d%4d" = number, name, class, WPN @240, ARM @244, SHL @242, items
    "---------------------------------", then the first 7 pool items (0x1875), "<====" marks the current one
for each item in pool order:
    repeat: pick := menu 0xbe24 over SELL, SACK, member 1..n (cursor [0x1674] kept, starts on member 1);
            no cancel (a negative return asks again)
    until give(pick − 2, item) succeeds:
        SACK (−1):  first empty found slot := item; full (60) → beep, refused
        SELL (−2):  purse [0x63f6] := clamp(purse + V²/8) (0xd791); town stock[item] += 1, max 99
        member k:   9 items → beep, refused; else slot[count] := item (0x2f22; contiguous, as every slot was zeroed
                    above), then the worn-item selector 0x2fdb
                    (best usable shield / armor / weapon by points) and the row's WPN/ARM/SHL/# are redrawn
    the list scrolls up one line
"DONE", wait for a key
```
Item line (`0x1875`, `"%-10s:%3d %c%s"`): name, points and kind letter from `0x17df` (S shield 1–20, A armor 21–40, W weapon
41–100, else blank and 0), then `" 1234567"` with each member who can't use it (`0xd987`) blanked — the manual's "along
with the characters who can use each one"; nothing for non-equipment.
**Manual (p.12):** items listed best to worst, first magical; "type the number of the character who will get that item or
type 7 to sell it". DOS = a highlight menu with SELL and SACK rows (C-64 addendum: highlight menus); SACK is extra.
**Remaster (user: "distribute everything just like the actual game"):** `ItemDistribution` (Core) + `DistributeDialog`
(Client): the DOS pool, order, targets and selector; keys = arrows + Enter on the DOS menu, digits 1–6 pick a member and
7 sells (manual). The sack holds 90 (D-018). Sold gold goes to the carried purse (D-048 cap).

### Town-entry share split (OV1 `0x167d`) [C behaviour, s35 DOSBox]
Combat XP/gold are pooled (`[0x63f4]`/`[0x63f6]`; the DOS end-of-combat "[original text]" shows pool ÷ survivors — Core
shows the encounter totals and pools them whole, D-053). On town
entry — and on Save game when the purse is non-zero — "You have: G gold / X XP — Shares for:" asks each member for 1/2/3
shares (a DEAD member 0/1/2/3). Share = floor(total / Σ shares); each member's gold share goes into `@262` (bank), XP share
into `@266`, dead members included; remainders are dropped and the purse is zeroed. Manual agrees in outline (PDF p.15).
**Remaster (D-037, user):** keeps Core's `TownMenus.Distribute` — equal split among living members, gold to the carried
purse, remainder to the first survivor — run on town entry (`GameSession.EnterTown`, before aging as DOS `0x1992`; I and
II). Found items wait for the town's Distribute.

## Aging — on town entry, not at the Inn [C] (s11, OV1 `0x1954`)
Called when the party arrives in a town (OV1 `0x1e56`) and from `0x277a`, with the
town id. Time accumulates in the word `[0x63f2]` while travelling: every accepted wilderness step adds 60 or
`(tile/10 + 1)·75`, the wilderness inn adds 1000, nothing else adds (OV2 `0x6528`, which wraps mod 65536 — its
saturation test is dead code; `overworld-movement.md` §Travel time). 256 units = one `DAY OUT` day. Strings `"%s has aged"`,
`"%s has died"`.

```
OV1 0x1992: call 0x167d — share split of gold [0x63f6] / XP [0x63f4] (§Town-entry share split)
weeks2 = (time >> 8) / 7                    # unsigned; low byte and remainder are discarded
for slot in 0..6: slot.age_clock += weeks2  # @264, all 7 slots incl. empty/summon slot
time = 0; gold words [0x63f4],[0x63f6] = 0; [0x63e8] = 0
if slot-6 record (DS:0x624c) +0x5c != 0: party size -= 1, clear its in-party flag   # = summoned elemental leaves (s33 [C], combat-resolution.md §[original text])
for each active member m:
    years = m.age_clock >> 8                # ≈ 9.85 displayed years per step (AGE = @264/26)
    if years <= (m.lifespan * 7) / 10: continue          # @252; unsigned
    if m.status == DEAD:              continue
    if years > m.lifespan:   "%s has died";  m.status = DEAD;  k = 0
    elif m.has_aged == 0:    "%s has aged";  m.has_aged = 1; k = 4      # @258
    else:                                                 k = 5
    for stat in @0,@2,@4,@6,@8,@10,@12:  stat = stat * k / 5   # signed, truncating
    recompute derived fields (OV1 0x2fdb)
for slot in 0..6: slot.current_town = town          # @254
for each member with @92 in 1..22: copy party record → roster record @92−1 (0x11a bytes, OV1 0x1b98 → 0x7bf)
0x8e1(town)                                          # Armory restock (§Armory restock), OV1 0x1ba4
```
`0x7bf(src, dst, n)` [~] (s41) — a far copy of `n` bytes from the first argument to the second: all three callers
read that way (load `0x896` roster → party slot, party compaction `0x9bf` higher slot → lower, town entry `0x1b98`
party → roster `[0x63e0] + 0x11a·(@92 − 1)`). So town entry **writes every adventurer back into its roster record**
(`@92` = roster slot + 1; 0 or > 22 = not a roster member, e.g. a summoned elemental). The COM helper `0xf989`'s
argument order is not decoded, hence `[~]`.
- Decline: **one-time ×4/5** on STR, INT, DEX, CON, CHA, `@10`, `@12` when
  `age_clock>>8` first exceeds 70 % of the lifespan. Death from age **zeroes all seven**.
  Checked only on town entry; roster members not in the party never age.
- Travel shorter than `7·256 = 1792` time units between towns ages nobody (the
  counter is reset). One town visit can add at most `255/7 = 36` steps (≈1.4 years).
- Other `@264` writers: `+2` per spell learned (OV1 `0x165f`), `+4` on **every** Guild→Add (`0x284c`) **[C] (s49
  re-read)**: `0x2835` compares `@254` with the Guild's town; equal → `je 0x2845`, different → the `0x3b08` gate first
  (COM menu 9: "Not in the town / [original text] / to get here", YES = 0 / NO = 1 — labels 46..50 of the DS `0x64c`
  table; NO → back to the list, nothing written). Both paths then run `0x2845..0x2851`: `@254` := town, `@264` += 4,
  `@218` := 1, copy roster → party slot. The prompt's "2 months" (= 4 units) suggests the
  cost was meant for travel only, so the same-town `+4` reads as a DOS quirk (`quirks-and-bugs.md` Q8); manual silent.
  **Remaster (D-067, user):** only an Add from another town ages (+4); a same-town Add ages nobody. DOS fixtures agree with the DOS rule:
  `roster-full-22-s9` → `s41-start` every re-added member gains +5 (+4 Add, +1 town entry) standing in Pelnor, the two
  not re-added (thiefo, gobbo) gain 0. `[~]` The slot scan (`0x27ef..0x2814`) stops at an empty slot **or** one whose
  `@92` equals the pick, so re-adding a member already in the party seems to re-copy the roster record over the party
  slot and age +4 again; whether the `0x3766` list offers in-party members is not decoded. Core refuses it.
- **Units:** `(time >> 8)` is displayed days (`DAY OUT`), `/ 7` makes weeks, but one `@264` unit is two weeks
  (AGE = `@264 / 26`). So in DOS each 7 travel days age the party **14 days**. Internal to DOS; the manual gives no
  travel-time rate, so not a conflict.
- **Remaster status:** Core `Aging.ApplyTownEntry` implements the decline/death pass with the D-003
  lifespans (age-clock units, no `>>8`) **and the travel-time step** (`GameSession.TravelTime`, accrued per
  wilderness step by `TravelClock`, added to every party member — DEAD too — then reset). Summon-slot drop **[C]+[D✓]
  s49**: with an elemental in slot 7, DOS lowers `[0x648e]` before the aging loop (OV1 `0x19dc`, loop bound `0x19eb`),
  so the last adventurer skips aging on that visit; the `@254` write (`0x1b44`) covers all 7 slots. Core matches.
  Guild→Add `+4` / `@254` / `@218`: `GuildHall.TryAddToParty`, `AddNeedsMoveConfirm` (the Guild's town = current town,
  else Pelnor). Gold split: Core keeps its equal split (D-037). DOS-save characters get the D-003 band on import (user).
- Lifespan `@252 = @10 × race factor` (`save-file-format.md` `@252`). In displayed
  years (`@10` = 8..13): decline from ≈59–98 / dies at ≈88–137 for HUMAN, PIXIE;
  ≈147–226 / 206–324 for DWARF, HALFLING, GNOME; ≈1053–1703 / 1506–2441 for ELF;
  ≈29–39 / 39–59 for the other races. Manual: elves ~3000, dwarves and gnomes ~200,
  humans ~80, others 40–50 → `DIFFERENCES.md` D-003.

## Temple — no such town service in DOS [C] (session 9)
**There is no Temple facility in a DOS Phantasie I town.** The town menu-bar label table is shared by every town
(`PHANTASI.COM` `0x5e6e`: Option, Bank, Mystic, Exit, Armory, + Inn, Guild — confirmed on screen in Pelnor), the
Option submenu is Cast spell / Use item / Show party / Save game / Auto save / Sound / Printout / Exit, and no game
binary contains the string "Temple". A Temple cost rule ("scales with level/CHA", stub `level*50/CHA`) was never
sourced.

Where the word does appear: **overworld map locations** — "[original text]" (`OUT1/2/3.DAT`), "THERE IS A
LARGE FORTIFIED TEMPLE HERE" (`OUT7.DAT`); their behaviour is in `overworld-movement.md` (small temple = new-game /
party-defeat wake cell; large temple = dungeon entrance).

**In-town resurrection is a spell**, cast via Option → Cast spell: OV1 loads "WHO WILL CAST A SPELL?" @`0x257b`,
"[original text] HEAL?" @`0x2625`, "%s IS RESURRECTED" @`0x2732` (string pointers = file offset + `0x5ff7`).
Decoded `[C]` in `magic-spells.md` §Town casting (OV1 `0x254e`; RESURRECTION `0x2693`: 50 + 5·Δlevel %, HP max, no CON
loss). The "%s [original text]" / "RESURRECTED" / "DESTROYED" strings (`0x5b23`–`0x5b5c`) sit beside the Astral
Plane / Great Demon text: three sibling routines, `0x113` turns undead (race @256 := 15, status OK, HP max, level :=
20), `0x18e` resurrects (revive helper `0x16b`: status OK, HP max) and `0x1ef` destroys (logs the roster link at
`0x6472`, then calls `0x1bae` — removal, not a stored status). Callers and full effects: `endgame.md` §4.

**`0x2786` is Guild → Add member**, not Temple: it loads "[original text] ADD?" @`0x27b1` and "There is no room"
@`0x2822` (both via the `+0x5ff7` string-pointer base). `+0xda` (`@218`) is the **in-party flag**, not a status — Add
sets it to 1 (`mov word ptr [si+0xda], 1` at OV1 file `0x2853`, the only word-store of 1 to `[reg+0xDA]` in OV1),
Drop (`0x225c`) to 0. The status word is `+0x18` (`@24`: 0 DEAD / 1 OK / 2 SLEEP), level is `+0x58` (`@88`). Add also
sets `+0xfe` (`@254`) := the current town and `+0x108` (`@264`, age clock) += 4 (§Aging). See `save-file-format.md`.

Older static notes on `0x2786` (when it was thought to be Temple), still valid as facts: it is a direct-call target of
the facility switch guarded by `cmp [0x62a8],0`, beside a structurally identical call to OV1+`0xbac` (New Member). Its
member pick uses `0x3766`/`0x37c0` over 25 or 22 slots depending on a flag at `0x618e`; `0x37c0` with flag 0 (what
`0x2786` passes) accepts any occupied slot with non-zero status, living or dead. Its optional Y/N gate `0x3b08` does no arithmetic. It reads no CHA (`+0x8`), no level
(`+0x58`) and never touches the purse (`0x63f6`). Its string literals (`0xbd88`, `0xbda1`, `0xc0a1`) are runtime
CS-relative pointers outside the file's mapped range under the `OV1_BASE=0x3c26` convention.

## Inputs / outputs
- **Inputs**: party, per-character records (`GUILD.DAT`), town id + `TWNS.DAT`
  stock, `SACK.DAT` pool.
- **Outputs**: mutated character records (gold, XP, level, spells, HP/MP,
  equipment, status, age), updated Armory stock, emptied loot pool on
  distribution.

## Remaster note
Each facility is a pure transaction over `OpenPhantasie.Core` records; keep prices/
stock/training curves data-driven in `OpenPhantasie.Data`. Fix-vs-preserve for any
money bug found is deferred to `quirks-and-bugs.md`.

M4 Core wiring (`TownMenus` / `GuildHall` / `LootSack` / `TownState`):
- Bank, Armory (buy only; list buy formula [C]), Inn rest + distribute, Temple (non-DOS — see §Temple),
  Mystic, Guild add/drop/purge/rename — implemented and unit-tested.
- Q1: gold capped at the manual's 65,000, excess lost (D-048). Q2: distribute remainder to first survivor (FIX). Bank
  Deposit = the share split only (`DepositShares`, gold, 65,000 clamp; D-049 manual).
- Training XP threshold + gold cost [C] (full float chain incl. XP-0 reset, stored doubles, gold = pow(level, 2.2) base).
- Mystic SCORE formula and tier lines [C]+[D✓] — Core prints the DOS lines.
- Aging on town entry (§Aging), spell-learning fee (confirmed OV1 formula), Armory full list price.
- `SACK.DAT`: `SackCodec` (Data) reads/writes the 60 slots; play uses the semantic `LootSack` pool (D-037).

## Leaving town — "LEAVING %s" [C] (s154)
OV1 `0x1e15` (the town loop's wrapper, state 1) returns 2 (overworld) after `0x1e72` and **`0x483e`**: clear row 24
(`0xb9d1(0, 24, 39, 24)`), print `"LEAVING %s"` (OV1 string `0xc284`) at column 11, row 24 with the town record's name
(`[0x6368] + 0x117`, TWNS.DAT), then, when `[0x3092]` (Auto save) is set, save (`0x4562`). No wait: the line stays on row
24 as the overworld opens. Remaster (I, AMIGA SCREENS off): the Client notes "LEAVING <TOWN>" after Exit
(`game.dos_leaving_x`), so the message line opens on it; the auto-save runs before it (DOS: after) and the column-11
place is the message line's `[~]`.
