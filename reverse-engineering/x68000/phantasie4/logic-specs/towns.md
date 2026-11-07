# Phantasie IV — towns: gate, buildings, bank, inn, shop, prices (spec, 3d.6)

Source: X68000 `P4.X`, static reading with the program's symbol table (addresses = P4.X image addresses, `../FUNCTIONS.md`).
Cross-checked against MSX2 segment `1000` (the town segment; load base `0x0100`, party records at `0x77DD`, `_par` at
`0x74C1`; `../../../msx2/phantasie4/FORMAT.md`). Written source: the **PC-9801 manual** (Japanese, 33 pp., found s81),
chapter 4 "Town" pp. 23–29, 32 and p. 30 (age). Tags: `[C]` from the code, `[~]` likely, `[?]` open. Nothing here was
checked in an emulator. Manual/code conflicts: `DIFFERENCES-IV.md` D4-030 … D4-034 (PENDING). Where the spec says
"manual agrees" the manual page was checked against the code value. The manual is the PC-98 version's; PC-98 code was
not read, but its program (`pc98/original-images/phantasie4/`, Disk 1) holds the same town strings ([original text] at
offset 352 122, 預金入金 352 069, は歳をとった 356 736, 宿代として 342 851, 船は 1 隻 1500 348 325).

Record offsets: `../FORMAT.md` §6.2 (character, 190 B) and §6.7 (`_par`). Party records start at `_pmb` `0x79708`, so
`0x79708 + off` = record field `off`.

## 1. Towns and town types `[C]`

- 21 towns by map number `_cmapn` 1–21 (`_towns` `0x3DA64`): 1 Pendragon, 2 Pendleton, 3 Delta, 4 Tiris, 5 Flagler,
  6 Scanport, 7 Pelnor, 8 Pippacott, 9 Almanar, 10 Elisar, 11 Akihabara, 12 Lingstroff, 13 Skull City, 14 Astral City,
  15 Nether Village, 16 Styx Harbour, 17 Town of Mist, 18 Titan Village, 19 Lava City, 20 Town of Plutars, 21 Astral Town.
  Maps below 13 get the suffix "の町" (" (town)") on the title (`_tload`).
- **The street's banner (s135, static; seen on XEiJ: "[original text]の町") `[C]`:** `_tload` (`0x298C`–`0x29F8`) writes the
  name **on the street picture's red banner**: `_font(0, 1, 15, 16)` (font 0, bold, colour 15), `_wkprcen(32, 156 − 20 ·
  (map < 13), 8, _towns[map])` then, for maps below 13, `_wkprint("の町")` (so name + suffix is centred on x 156), y 8 in
  the street window; after the ending (`_tload`'s argument ≠ 0) `_wprcen(32, 156, 8, "Congraturation!")` instead. While
  the street picture unpacks it shows "Welcome!" (`_font(0, 2, 15, 16)`: italic, colour 15) centred at (156, 81), which the
  picture then covers. **Centring (s136) `[C]`:** `_wprcen` / `_wkprcen` take x − (`_fsize` >> 1), and `_fsize` sums the
  **plain** advances (the bold pass adds 1 to each drawn advance only), so "Congraturation!" (plain 106, bold 121) starts
  at x 103: the s135 XEiJ ending shot's banner ink is the bold glyphs at (103, 8) pixel for pixel (532 of 532). The suffix
  is printed at the pen after the name, so the name alone is centred on 136; the system-menu shot's "[original text]の町"
  (six CG ROM kanji of 16 → x 88, bold kanji 17 wide) agrees. **Built s136:** the remaster writes the banner on
  `p4.street.N` and `p4.ending.street` in the pack's `p4.street.banner` font (P4.X font 0 bold, `x68k-p4-font0-bold`),
  colour `pkh` 15, in pack pixels (`P4DungeonScreen.Banner.cs`; `parity/shots/p4-remaster-street-banner.png`); English
  "Pendragon (town)" from `p4x.0x3fa50`. Kanji (Japanese text) come from the pack's 5 × 7 font at the picture's scale,
  so they are small `[~]`; the CG ROM's bold kanji are not extracted. s151: "Welcome!" is drawn while the street
  "loads" (FORMAT.md section 6.10 "Welcome!": 1137 ms measured on XEiJ `[~]`); `_tload` also shows "Please Wait..."
  first while the street's animation sheet loads (not drawn by the remaster). The text box keeps its own title line.
- **Town type** `_twtyps[map]` (`0x3F70E`): 0 = Pendragon only; 1 = maps 2, 3, 5–13; 2 = maps 4, 14–21.
- **Buildings** (`_town` `0x321C`, `_bvs` picks the door): 0 Guild, 1 Mystic, 2 Armory (shop), 3 Bank, 4 Inn,
  5 = **Hall of Glory** in type 0, **Shipwright** in type 1; type 2 has no fifth building (door 5 = leave). 6 = leave
  (`_exittown`). So the Hall exists only in Pendragon (manual p. 23 agrees) and every type-1 town has a shipwright
  (manual: "towns by the sea").
- **There is no temple, healer or curse-remover in any town** `[C]`: no such building, menu or string; the manual's
  building list (pp. 7, 23) has none either. Wounds are healed by the inn (§4) and spells; "Temple of the Demon / Duck"
  are dungeon locations.
- **Town menu** (right-hand icons): F Profile (`_tprof`), C Spells (`_tcast` → `_ocast(7)`), T Item exchange (`_trade`),
  D Item distribution (`_distribute(0)`), U Use item (`_use`), S System (`_syscon`). Manual p. 32: only Healing,
  Resurrection and Transportation can be cast in town; `_ocast(7)` offers Healing 1–4, Levitation, Resurrection, Identify,
  Transportation, Magic Bag and Super Transport (`spells-noncombat.md`, D4-061) `[C]`.
- **Town price factor** `T[map]` (`_ctab` `0x3FB3C`): maps 0–9 → 80, 10–12 → 100, 13–21 → 120 (%). Used by training,
  magic training (`training.md`) and shop prices (§6). MSX2: same table (seg `100C` `0x2A1E`).

## 2. Entering a town (`_town` → `_townent` `0x2CF2`, `_welcome` `0x2C8E`, `_tload` `0x2988`)

Done on every arrival except a reload inside town (`_cmovf` = 2). Order:

1. **Share out experience** (`_gatebank` `0x4EF0`, window "[original text]" Share out experience) `[C]` — only if the party
   experience pool `_par`+18 ≠ 0 and the party is not empty:
   - The pool holds combat experience **÷ 10**: at the end of each fight `_esub01` adds trunc(XP / 10) (cap 999 999 999),
     so up to 9 XP per fight are lost `[C]`. The window shows pool × 10.
   - For each member in order the player picks a share 0–3. The "0" button is enabled only for a member whose
     condition is not Okay; an Okay member must take 1–3 `[~]` (button-enable field read from `_bank`'s use of it).
   - per share = trunc(pool × 10 / (0.0001 + Σ shares)); each member's experience (+04) += per share × shares, cap
     9 999 999. The pool is set to 0 (the remainder is lost).
   - **Party gold is not touched** here or anywhere else on entry (see D4-030).
2. **Aging** (rest of `_townent`) `[C]`, for each member:
   - age (+1E, in 1/26 year) += trunc(`_par`+16 / 0x700), cap 65 000; then `_par`+16 = 0 (the remainder is lost).
     `_par`+16 is the counter `_dayout(n)` advances by trunc((n + `__daytime`) / 24); its real-time unit is `[?]`
     (MSX2 `1000:0x0356`: same divisor 0x700, reset at `0x0396`). **Only two places pass time** `[C]` (the only
     `_dayout` callers): each outdoor move (`_omain`, 8 or 36 by the square type, table `0x42D60`) and an outdoor inn
     (1000). Dungeons, town inns and combat pass none. Other aging: +2/26 yr per magic-training attempt (`_mage`),
     +4/26 yr when joining from another town (`_party`).
   - Let F = aging factor (+20, `ancestry.md` §7 item 10) and A = age / 256 as a **signed** 16-bit shift (X68000
     `move.w`/`ext.l`/`asr.l #8` at `0x2E06`; MSX2 signed divide `0x42E9`), so from age 32 768 (≈ 1 260 years) A is
     negative and the character is skipped for good.
   - If A < 0.7 × F: nothing (and no `_healch` refresh).
   - Else if A < F: if the "has aged" flag (+21) is 0: "<name> has grown older." (`0x3FA6F`), flag = 1, and the seven
     bytes +2F … +35 (Str, Int, Dex, Con, Cha, Luck and +35) are scaled by 80 %.
   - Else (A ≥ F): condition (+0A) = 0 Dead, "<name> has died of old age." (`0x3FA89`), and the seven bytes are set to 0.
   - So a character dies at age ≥ F × 256 / 26 ≈ 9.85 F years and declines once at 70 % of that — as long as that
     stays below age 32 768: with F > 127 a character can never die of old age, and with F > 181 it never declines
     (only Elves reach such F: Luck × 19 = 171–255).
   - **Platform difference `[C]`:** MSX2 (`1000:0x0472`/`0x04EA`/`0x0534`–`0x06C2`) computes byte × 80 / 100 in
     integers. X68000 multiplies each byte by the **integer conversion of the float 0.8** (`__CFTOL`, then `mulu.w`,
     `0x301A`–`0x31BC`). FLOAT2.X on disk 1 (`SYS/FLOAT2.X`, line-F table at `0x17E`; `$FEE9` → `0x6AA` → `0x2A7C`,
     `0x2B6E`) truncates and returns 0 for |x| < 1, so on X68000 "has grown older" **sets all seven bytes to 0**
     (abilities and Luck 0). The MSX2 rule (× 0.8) is the evident intent; D4-034.
   - **Original quirk `[~]`:** a member whose flag is already 1 and who is still alive takes the branch that scales the
     seven bytes again with the factor variable **left over** from an earlier member (or uninitialised); same flow on
     MSX2 (`0x0443` → `0x0534`).
   - Lifespans this gives: D4-033.
3. **Item distribution** (`_welcome`): if the party pack (`_par`+20, 128 slots) holds anything, `_distribute(1)` runs
   before the town opens (manual p. 32 note 3 agrees) `[C]`.
4. **Town load** (`_tload`): `_cmapn` = new map, town type set; every member's guild record and party record +1B = this
   town (so +1B is the **current town**, not only the creation town) `[C]`; then the shop is restocked (§6.3).
5. Pendragon with story flag `0x78B20` set runs the ending (`_ending`) instead of the town `[C]`.

**Joining from another town** (`_party` `0x7A14`): a guild member whose +1B is not the current town ages +4/26 year
(about 2 months) when put in the party `[C]`; the manual (p. 26 note 3) says the same.

**Leaving** (`_exittown` `0x5AE8`, float code read s102) `[C]`: `_par`+0A = the highest Score (+09) in the party; each
member's +3B = trunc(trunc(Str / 6) + |class − 3.5| × level / 11) (3.5 is the double at `0x400C4`; Str / 6 is an
unsigned integer divide; the melee damage bonus, `combat.md` §1); `_par`+0B = trunc(Σ level / party size + 0.5) (0.5 at
`0x400CC`; the sum is a float, so no byte wrap here, unlike the Mystic's); `_equip` re-equips everyone, and the party is
placed at the town's exit square (`_townexit`) on the outdoor map; `__daytime` = 0.

**Exit and ship squares** (extracted s97, `towns.json` `exit` / `ship`) `[C]`: `_townexit` (`0x3FF20`) holds int32
{outdoor map, x, y} per town (index map − 1), read at `_exittown` `0x5DA4`–`0x5E1C` into `_cnewn` / `_cnewx` / `_cnewy`
(with `_cnewf` = 1); `_shipexit` (`0x4001C`) holds int32 {x, y} (index map − 1, read by `_shipyard` `0x60DE`), the
mooring square on the exit map; −1 = none (Pendragon, Tiris, Nether Village). Every exit lies next to its town's 2 × 2
block and every ship square on water (type 0) or the River of Hell (type 9), with these oddities as shipped: Astral City's
(20: 20, 2) and Astral Town's (22: 14, 5) exits are a square of their own town block, so the step on arrival asks "Enter?"
at once; Lingstroff's (1: 20, 11) and Skull City's (1: 9, 11) exits are two squares from their blocks; Skull City's exit and
ship squares are shining sea (type 13).

| Town | Exit (map: x, y) | Ship | Town | Exit | Ship | Town | Exit | Ship |
|---|---|---|---|---|---|---|---|---|
| 1 Pendragon | 9: 9, 10 | — | 8 Pippacott | 14: 23, 16 | 24, 15 | 15 Nether Village | 21: 8, 18 | — |
| 2 Pendleton | 8: 18, 6 | 17, 5 | 9 Almanar | 10: 18, 20 | 17, 20 | 16 Styx Harbour | 17: 16, 17 | 13, 17 |
| 3 Delta | 10: 7, 18 | 8, 18 | 10 Elisar | 2: 20, 9 | 19, 10 | 17 Town of Mist | 20: 4, 8 | 1, 8 |
| 4 Tiris | 9: 9, 24 | — | 11 Akihabara | 2: 8, 18 | 7, 18 | 18 Titan Village | 20: 6, 19 | 6, 22 |
| 5 Flagler | 5: 8, 10 | 7, 9 | 12 Lingstroff | 1: 20, 11 | 19, 9 | 19 Lava City | 23: 14, 21 | 14, 24 |
| 6 Scanport | 3: 17, 12 | 17, 13 | 13 Skull City | 1: 9, 11 | 10, 11 | 20 Town of Plutars | 22: 21, 23 | 22, 24 |
| 7 Pelnor | 13: 4, 12 | 3, 11 | 14 Astral City | 20: 20, 2 | 19, 2 | 21 Astral Town | 22: 14, 5 | 14, 6 |

**Remaster build (s97, `P4TownLoop`):** entry (D4-030 gold deposit by shares under the manual, the experience share-out,
aging with D4-033 / D4-034, `_tload`'s current town and the 8 random shop items), Inn (with D4-032's two weeks), Mystic,
Shipwright and Leave. Aging messages wait for a key `[~]` (the original's wait was not read).

**Remaster build (s102, `P4TownLoop.Screens.cs`, `P4Menus`, `P4Services.cs`):** Guild (roster P / F / D / R with the
elemental pick, training with the three skill picks and the whole `_calpow`, D4-060; Magic Hall with its five pages),
Armory (pages, N-U, sold-back stock at D4-020's price), Bank, Hall of Glory (history pages, the three shipped persons'
sheets), item distribution on arrival and T / D in town, `_exittown`'s +0A / +3B / +0B, the inn's MP growth, the ending
(messages, Noble / Lord, final screen with score and "Duck"); party menus F / P / C / U in every place (D4-061, D4-062).
Remaster choices: the live shop stock is saved with the party (the original loses it on a restart); one Identify cast rolls
D4-053 once and covers every item appraised in that cast; nothing can be saved inside a building or a party menu (the
original's System icon sits on each place's main menu; S opens the remaster's System screen (s138, FORMAT.md 6.9): its S is the shell's F5, E the main menu; F9 loads as before); remaster strings are in
`shared/strings/en.json` (the per-game file is generated). Guild N (the creation screens) built s106 (`ancestry.md` §10). Not built: Hall transfer (T5);
the "re-equip" is computed where it is read (`P4Equipment.Best`), the battle runs its own copy.

## 3. Guild (`_guild` `0x47F8`) `[C]` (s102)

On entry every party record is copied back into its guild record (`_tranch(party m → guild slot link − 1)`). Menu: G
Guild roster (`_mlist`), and only while the party is not empty T Training hall (`_trang`) and M Magic training hall
(`_mage`); L Leave. Training and magic training: `training.md` §4.1, §5; skill picks: `skills.md` §3.

**Roster** (`_mlist`): the ten guild slots (`_gmb`, 190-byte records; a slot is used when +01 ≠ 0) listed as "#,
registered name, race, current town" (`_set_mem_lst`). Buttons: P Form party (only while some slot is used), N Make a
character (`_make`, `ancestry.md`; both N buttons), F Profile (`_inspect(0)`: pick roster members, `_detail`), D Delete
(`_dislodge`), R Rename (`_re_name`), L Leave. Every one of P, D, R first **clears the party** (`_clrparty`: party size
0, `_par`+0B = 0, the seven party links +01 and conditions +0A = 0).

- **D Delete:** pick a slot, confirm → the slot's +01 = 0 (the record is forgotten). Repeats until SPACE.
- **R Rename:** pick a slot, type up to 8 characters; an empty name changes nothing; confirm → the name is replaced.
- **P Form party** (`_party` `0x7A14`): toggle up to 6 slots in the roster (each toggle appends / removes it from the
  pick order; the order picked is the party order); SPACE ends picking; with at least one picked, confirm; then
  **choose the elemental** to take (E: Earth spirit, Mist spirit, Air spirit, Do not summon — starts on "Do not summon";
  each press moves one down, wrapping; SPACE takes it) → `_par`+14. Then: party size = picks, `_par`+0E… = the slots,
  each guild record is copied into the party (`_tranch`), the **party strategy is reset** to A Hostile / B Flee when
  fairly in danger (`_par`+15E / +15F = 1, which `events.md` lists as story flags 0x3E / 0x3F), and every member whose
  guild record's current town (+1B) ≠ this town ages 4/26 year (manual p. 26: 2 months); every member's +1B = this town.
- The guild never re-sorts the roster; deleting leaves a gap that the next character made fills `[~]`.

**Party strategy and profile page 2 `[C]`:** Party info (outdoor "P", `_party_info`): gold, "Experience gained" = the
pool `_par`+18 **as stored** (the ÷ 10 value; the gate shows × 10), "Items found": counts of the pack by kind (shields
1–8, armour 9–27, weapons 28–63, missiles 64–84, potions 85–128, treasures 129+), and the party strategy A combat
attitude (Friendly, Hostile, Surrender, Accept surrender) and B behaviour (Flee when slightly / fairly / greatly in danger,
Fight to the end), each key press moving one down with wrap (`_par`+15E / +15F). Profile page 2 (`_page2`): a member's
strategy bytes +2A … +2E = A auto defence (3), B missile (2), C melee weapon (5), D movement (7), E use of magic (6),
0-based, each press moving one down with wrap; creation sets 2, 0, 2, 3, 3 (`FORMAT.md` §6.2: these are the "unknown"
bytes +2A–2E).

**Profile pages (s140, static `[C]`):** `_inspect(place)` puts up the "Character info" panel (SPACE) over the member
pick, then `_detail(member)`: page 1 (`_page1`) waits on window 36's SPACE / N:次頁へ (`_detailmenu`): N → page 2, SPACE
leaves. Page 2 (`_page2`, its row from `_p_info3menu`: SPACE / N:次頁へ): A–E turn the strategy, N → page 3, SPACE leaves.
Page 3 (`_page3`: SPACE / N:前頁へ, "previous page"): N → back to page 2, SPACE leaves. Leaving returns to the member pick
(`_inspect` loops). The manual (p. 29, Figure 6) shows the same "□SPACE □N 次頁へ" on page 1. The remaster had N on
pages 1 / 3 only, page 3's N wrapping to page 1 and SPACE on page 2 going to page 3; it follows the code since s140.

## 4. Inn (`_inn` `0x2188`) `[C]`

Free: no gold is taken and **no time passes** (no `_dayout`, no age change; MSX2 `1000:0x1FA3`–`0x21F8` has no
day-counter write either). For each member:

1. If condition ≠ Dead: "<name> slept."; then, if the MP-growth flag (+72) is set: clear it and, if rand(100) ≤
   (magic level +36 − max MP +37) × 20, max MP +1 ("'s MP went up!"). What sets +72 is not read `[?]`.
2. Current MP = max MP, current HP = max HP.
3. Wounds: `_heal(member, 2)` removes up to 2 points of body-part injury. The six body parts are 2-bit levels packed in
   +0C (`_pa`). It first clears parts at level 2 (each costs 2), then level 1; **level-3 parts are never healed** by the
   inn. `_setpc` then recomputes the combat condition (`_pc`: 4 = out if Dead or part 0/1 at level 3, …) `[~]` for the
   part names.
4. Dead members get only step 5.
5. Rings recharged: every carried Ring of Magic Power (129) or Ring of Healing (131) gets 20 charges (+5B…+63, one per
   carried-item slot +52…+5A), and the same in the party pack (`_par`+A0 charges).

MSX2: same steps and constants (`0x2094` rand(100), `0x72`/`0x36`/`0x37`, refill `0x38`/`0x3A`, `0x2147` heal, ring
loop `0x216E`–`0x21A6`).

**Outdoor inns** (`_oinn` `0x14F98`, "There is the %s Inn here"): need party gold > 255; cost **256** gold ("256ｺﾞｰﾙﾄﾞを
支払った"; manual p. 27: "250 gold plus tax"), then `_inn()` and `_dayout(1000)`, and the party reloads on the same
square `[C]`.

## 5. Bank (`_bank` `0x57A4`, `_deposit` `0x4992`, `_withdraw` `0x5414`) `[C]`

Each character's **bank account** is record +26 (u16) — the field the character sheet shows as "Gold" (manual p. 29:
"銀行預金", the account, excluding the party's gold). New characters start with social class × 256 (Noble 768, Peasant
0: manual p. 25 agrees). Level-ups pay into it (`training.md` §4, cap 65 535). Training, magic training, the shop, the
shipwright and outdoor inns all spend **party gold** (`_par`+1C), so account money must be withdrawn first (manual p. 25
says the same). The bank is the same in every town (one account per character).

Screen: the accounts and balances of the party; D Deposit (disabled when party gold = 0), W Withdraw (disabled when party
gold = 65 535), L Leave. Deposit asks one share per member in party order (buttons 0–3; 0 enabled only for a member who
is not Okay), shows "Per share N GP", then waits for SPACE. Withdraw goes through the members in order showing balance and
limit; a member with limit 0 is skipped, the others type an amount (cut to the limit); then SPACE.

- **Deposit:** each member picks a share 0–3 (0 only for members not Okay, as in §2.1 `[~]`); per share =
  trunc(party gold / (0.0001 + Σ shares)); each account += per share × shares, **capped at 65 000** (a sum above 65 000
  is set to 65 000, the excess lost); party gold = 0 (the division remainder is lost).
- **Withdraw:** for each member in turn: limit = min(balance, 65 000 − party gold); if > 0 the player types an amount
  (5 digits, cut to the limit); account −= amount, party gold += amount (cap 65 000).
  **Quirk `[C]`:** the limit is unsigned (`bls` at `0x5456`, `0x554E`). If party gold is already above 65 000 (possible
  through combat loot or selling, below), 65 000 − gold wraps to a huge number, so any typed amount up to 99 999 is
  accepted, the u16 account wraps below 0 (`sub.w` at `0x559A`, e.g. 100 − 500 → 65 136), and party gold is set back to
  65 000.
- MSX2 seg `1000`: same screens and caps (`0xFDE8` = 65 000 at `0x1BAB`, `0x1BF1`, `0x1DE8`, `0x1E07`, `0x1F21`).

**Gold caps differ by path** (D4-031): withdraw/deposit 65 000; sell 65 535; combat loot 999 999 999; level-up gold
into the account 65 535.

## 6. Armory = shop (`_armory` `0xE552`, `_itemlist` `0xDE82`, `_buyitem` `0xE2DA`, `_sell` `0xA236`) `[C]`

### 6.1 Buying

- The shop lists every item 1–156 with stock (`_itemp` > 0), six per page ("MORE"), with icon, name, value (DEF/ATT for
  items 1–84) or "N-U" when the selected member cannot use it (`_cal_user` rules, `../FORMAT.md` §6.5), and the price.
- **Buy price = trunc(price × (single-precision T[map] / 100))**, `price` = `_idat` u16 @4. So Pendragon and the other
  maps 1–9 sell at 80 %, maps 10–12 at 100 %, maps 13–21 at 120 %. The listed price is the same expression
  (`_itemlist` reads `_ctab[_cmapn]` and the same float calls before printing) `[~]` (the float sequence was read
  only in `_buyitem`).
- The buyer is the highlighted member (click a member to switch). Needs party gold ≥ price ("can't pay") and a free
  carried slot (+52…+5A, first empty of 9; "can't carry more than 9"). Rings 129 / 131 come with 20 charges. Party gold
  −= price; shop stock −1 (and the saved stock count −1 if > 0).
- Leaving the shop runs `_equip` for every member: a better item is put on automatically (manual p. 24 note).
- Manual p. 24 figure (Small Shield 10, Clothing 5, …, Magic Shield 2000) shows the **base** prices (T = 100); in
  Pendragon (T = 80) the code would list 8, 4, …, 1600 `[~]` (the figure is illustrative; not raised).

- **Screen (s102):** six lines per page starting where the last page stopped ("MORE" pages on, wrapping after 156),
  letter A–F buys at once (no confirm): "can't pay.", "can't carry more than 9." or "<item> bought."; points shown for
  ids 1–84, "N-U" when the highlighted member can't use it: ids 1–27 need b2 ≤ Str, 28–63 b2 ≤ 2 Str + Dex, 64–84 b2 ≤
  Str + Dex, 85+ always usable.

### 6.2 Selling

- **Sell price = trunc(price / 4) + 1**, added to party gold, cap 65 535 (`_sell`).
- The sold item's saved stock (`goods`: `_item[i]` count +1) moves to the current town (`_item[i].town` = `_cmapn`), so
  an item's saved stock lives in one town only: the last one where it was sold `[C]`. It shows in the shop on the next
  town load (the live list `_itemp` is not updated by `_sell`) `[~]`.
- Selling happens in Item distribution (`_distribute`, "S", for party-pack items) and also in Item exchange (`_trade`,
  for a member's own item) `[C]`. Manual p. 32 note 1: distribution is the only way to sell the **party's** belongings;
  that holds.

### 6.3 Restock (`_igen8` `0xDDD4`, `_igen` `0xDCFA`) on every town load

1. For every item whose saved town = this town: live stock = saved count. **The live list `_itemp` is never cleared**
   (s102: no writer besides `_igen8` and `_buyitem`; it is not in the save set either), so counts from earlier towns and
   every earlier random item stay on sale until bought `[C]`; after the program restarts it starts empty.
2. Add 8 random items to the live list (not to the saved stock, so they vanish on the next load): each is
   `_gen_itm[r + 1]` (`0x3D65E`) with r = rand(2 × min(m, 12) + 5), m = the **sum** of the party's levels (`_igen`
   `0xDCFA` adds the level bytes; s102 correction — the remaster had used the average).
   r = 1 gives entry 2 (Magic 2); the most is entry 30 (Splint Mail) at m ≥ 12. Entries: 2 Magic 2, 3 Clothing, 4 Dagger,
   5 Small Shield, 6 Magic 3, 7 Robes, 8 Short Sword, 9 Leather, 10 Healing 3, 11 Medium Shield, 12 Magic 4, 13 Flail,
   14 Throwing Knives, 15 Medium Bow, 16 Hard Leather, 17 Hammer, 18 Duck Potion, 19 Magic 5, 20 Magic Mace, 21 Nigel's
   Nostrum, 22 Sword, 23 Magic Darts, 24 Magic Elixir, 25 Monk's Robe, 26 Pike, 27 Magic Shield, 28 Thief's Sword,
   29 Bardiche, 30 Splint Mail. Entries 0, 1 and 31 are never rolled.

### 6.4 Item distribution and exchange (s102) `[C]`

- **Distribute** (`_distribute(kind)`): kind 0 (town "D") gathers **every member's carried items and the pack** (members'
  equipment is cleared), kind 1 (on arrival) only the pack, and is skipped when the pack is empty or nobody is alive.
  The items are sorted by id, highest first (treasure, magic potions, healing potions, missiles, weapons, armour, shields:
  the manual's order, p. 32), then for each item in turn the player presses a member number (a member with 9 items is
  disabled) → it goes to that member (re-equipped if usable), **P** → back into the pack (while the pack has < 128), or
  **S** → sold (§6.2). Every item must be placed; SPACE ends after the last.
- **Trade** (`_trade`, town "T"): pick a member (whose 9 slots are listed), pick one of the items (it is taken into the
  hand; the owner re-equips), then a member with a free slot (gets it, re-equips), **P** (into the pack, if it has
  room) or **S** (sold). L Done only with nothing in hand.

Manual p. 23: the armory stocks "whatever it has at the time" and resells bought items "at a slightly higher price":
consistent (resale at T × price vs. selling at price / 4 + 1).

## 7. Mystic (`_mystic` `0x6BF0`) `[C]`

Shows the party's score and a verdict; no other service (manual p. 28: "you can learn the whole party's score; asking
registers it"). Score =

```
3 × Σ story flags _par+124 … _par+12A   (7 bytes)
+ 5 × _par+14E + 5 × _par+147
+ 15 × number of Zeus Juice (item 126) in the party pack and in members' carried slots
+ 2 × trunc(Σ member levels / party size)        (the average is stored in _par+0B: "registers")
```

Verdict: ≤ 30 "A mere fledgling!", 31–60 "Not bad!", 61–90 "Most impressive!", > 90 "O brave hero!". The level sum
is a byte (wraps above 255) `[C]`. The stored `_par`+0B average also feeds Demand surrender (`combat.md` §8) and the advanced-quest unlock
(`events.md`), and the ending rewrites it (`ending.md` §2) (s82).

## 8. Shipwright (`_shipyard` `0x5E4E`, type-1 towns) `[C]`

"A ship costs 1500 GP." Needs party gold ≥ 1500. If the party already owns a ship: "If you buy a ship, I will take back
the one you bought before." Yes → party gold −1500, ship owned (`_par`+06 = 1) and placed at this town's ship square
(`_townexit` map, `_shipexit` x/y). One ship at a time; the old one is simply replaced, no refund. Manual p. 28 agrees.
MSX2 "1500" in seg `1004` `0x026B` (not read further).

## 9. Hall of Glory (`_hall` `0xDACC`, Pendragon)

History, Hall person info and III transfer: `ancestry.md` §8–§10.

## 10. Outdoor places reached from the map (for completeness) `[C]`

- **Charon the boatman** (`_oboatman` `0x14CAC`): offers to take the party to a fixed place (`_boatmsg`); yes → moves
  there. Free. On **outdoor** map 17 (the Styx Port map; not the Town of Mist) the target is outdoor map 21
  (Netherworld Level 6) at (3, 2), block 2; on map 21 it is the `MOUT` destination, town 14 (Astral City)
  (`outdoor.md` §7, which corrects the earlier "map 17, Town of Mist" reading).
- **Pool** (`_opool` `0x14DC2`): "There is a pool. Step in?" → magic teleport to the stored destination
  (`outdoor.md` §7).

## 11. Open `[?]`

- Unit of `_par`+16 and what the 0x700 divisor means in game time.
- (s102 resolved: the inn MP flag +72 is set by every combat cast (`_psetcast`; the remaster battle does not set it yet), `training.md` §4.2; `_exittown` decoded; `_ocast(7)`
  spells in `spells-noncombat.md`.)
- (s102 resolved: the live stock is never cleared, §6.3.)
- An emulator check of one deposit, one purchase and one town entry with experience.
