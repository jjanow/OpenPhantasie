# Save File Format

Phantasie I has **no single "save game"**. Persistent state is spread across
several data files that the game writes in place [X] (walkthrough confirms:
"it simply keeps track of the state of each town … and the roster of characters
that makes up the Adventurers Guild. There is no reset button"):

| File | Size (this disk) | Holds |
|---|---|---|
| `GUILD.DAT` | 6204 B | Adventurers' Guild roster (every character ever made) |
| `DNGX.SAV` | 2500 B | Active-party + current dungeon/expedition state |
| `DNG.SAV`  | 6386 B | Dungeon persistent state (opened doors, taken loot, flags) |
| `SACK.DAT` | 120 B | The shared loot "sack" (undistributed gold/items/XP pool) |
| `TWNS.DAT` | 3492 B | Per-town state (mainly Armory stock, with randomization) |

> On the master disk in `../original-files/` all of `GUILD.DAT`, `SACK.DAT`,
> `*.SAV` are **entirely zero-filled** (a fresh, never-played disk). Two
> dynamic passes populate fixtures under `../extracted/samples/`:
>
> 1. **Pass 1** — create three characters in DOSBox (`wizardo` ELF/WIZARD,
>    `priesto` HUMAN/PRIEST, `thiefo` LIZ.MAN/THIEF) → `GUILD.3chars.dat`.
> 2. **Pass 2** — form a party, buy gear (dagger + clothing + 2 scrolls), leave
>    town, enter a dungeon → `GUILD.pass2.dat`, `DNGX.pass2.sav`,
>    `DNG.pass2.sav`, `TWNS.pass2.dat`. Parser: `tools/scripts/parse_guild.py`.
>
> The character-record core below is **[C]**. Inventory / dungeon / town-stock
> fields from pass 2 are marked **[C]** where pinned. `SACK.DAT` got its first
> non-zero fixture in s35 (`SACK.s35.dat`, §Loot sack).
>
> **When files are written (s35 DOSBox):** only by town Option→Save game, which writes `GUILD.DAT`, `SACK.DAT` and
> `TWNS.DAT` together (and first runs the share split if the carried purse is non-zero). Character creation, town
> entry, the Inn and entering a dungeon write nothing. `DNG.SAV`/`DNGX.SAV` are written on leaving a dungeon when the
> player answers YES to "[original text]" (writer OV2 `0x5929`, s36 [C]). [~] (s56 static) the
> party-wipe path (OV1 `0x3bc..0x5a5`) also writes `GUILD.DAT`.
>
> **Load [C] (s56 static, "Continue saved game", OV1 `0x1e15(2)` → `0x425c` → `0x7dd(2)`):** the loop `0x866..0x89d` walks
> roster slots 0..21 in order and copies every record with `@218 ≠ 0` into the next party slot; it never reads `@260` or
> `@92`. The party's town is `@254` of party slot 0. With no record flagged, `0x7e7` sets the town index `[0xc470]` := 0:
> the Pelnor town menu opens with an empty party (town actions that need one are refused, OV1 `0xad5d`). The manual's
> character pick differs (`DIFFERENCES.md` D-069, DECIDED s57: manual; remaster rule in §Remaster codecs → *Several
> parties*).

## Character record — confirmed field set [C]

The character-sheet renderer's format strings (`PHANTASI.COM` `0x8479`–`0x8528`)
enumerate exactly what a character stores:

```
LEVEL%3u <class> <race> <status>     (s49: pushes at COM 0x2ef4..0x2f2c = @88, class @220, race @256, status @24)
STRENGTH %u        AGE %u
INTELLIGENCE %u    HIT POINTS %u/%u      (current / max)
DEXTERITY %u       MAGIC %u/%u           (current / max)
CONSTITUTION %u    GOLD IN BANK %u
CHARISMA %u        EX. PTS %ld           (experience is 32-bit signed)
LUCK %u            SCORE %u
runes: %s %s %s %s %s %s                 (6 rune slots)
RING: %s
```

Plus the eight **skill** values (labels @ `0x8372`): `attack, parry, spot trap,
disarm trap, pick lock, find item, swim, listen` — printed as `%u`.

Plus inventory counters (labels @ `0x83f1`): `%u hl potions` (healing),
`%u mg potions` (magic), `%u scroll(s)`, `%u treasures`, and worn/carried
equipment slots (weapon / armor / shield indices into the item table).

### Record size — confirmed [C]

`GUILD.DAT` = 6204 B = **282-byte record × 22 slots** (exact). The three test
characters landed in slots 0/1/2 with a fixed 282-byte stride (names at file
offsets 270 / 552 / 834). So the roster is 22 slots, not the ~44 previously
guessed; the manual's "as many as 37" refers to how many you may *create over
time*, not simultaneous storage.

### Character record layout [C] — offsets within the 282-byte record

All multi-byte fields are **little-endian**. Offsets validated against all three
sheets (`parse_guild.py` round-trips them exactly).

| Offset | Type | Field | Notes |
|---|---|---|---|
| 0  | u16 | `strength` | |
| 2  | u16 | `intelligence` | |
| 4  | u16 | `dexterity` | |
| 6  | u16 | `constitution` | |
| 8  | u16 | `charisma` | |
| 10 | u16 | `luck` / **lifespan seed** | **[C]** (s11) rolled at creation `rand/5462 + 8` (8..13, OV1 `0x0e4b`) and multiplied into the lifespan `@252` (OV1 `0x0e84..0x0eef`, see `@252`). Also the WIZARD training stat sum `INT*2 + @10` (OV1 `0x129a`). The manual's sheet shows no LUCK. Zeroed with the other stats on death from old age (OV1 `0x1ae6`). Read by the CON-at-death code (OV2 `0x1317`). Not touched by combat prep (s19: the defense rating goes to `@12`). |
| 12 | u16 | `defense_rating` | **[C]** (s19) written by combat prep (OV2 `0x32f7`): `(DEX·20 − @244·50 + PARRY·10/4)/10`, floor 0, `+ @242/3`, cap 255 — matches all 6 fixtures. Monster melee to-hit reads it (`0xe68`). Town aging scales it ×4/5 / zeroes it with the six stats (OV1 `0x1954`). |
| 14 | u16 | `magic_ceiling` (max MP allowed at this class + level) | **[C]** (s11). Creation table `creation-tables.json` (offset `0x01c2`, `class*15+level`, key `mp_ceiling` — was `combat_ratings` until s17) is **identical, all 6×15 cells, to the manual's "Maximum Magic Points at Different Levels by Class"**; fixtures 10/10/7/6 = level-1 WIZARD·PRIEST/FIGHTER/THIEF. Creation sets `@16 = @18 = (3·@14)>>2` (OV1 `0x0e66`). The Inn raises `@16` toward it (`town-menus.md` §Inn). **Was labelled `combat_rating` [C]** from OV2 `char*0x11a+0x5bc0` — but that used party base `0x5bb2`; with `0x5bb0` that address is `+0x10` = `@16`. **s33 [C]:** the spell resist roll (`0x13b5`) reads `@16` MP max, not this field; Core `SpellCaster` now uses `MagicMax` (`combat-resolution.md` §To-hit). |
| 16 | u16 | `magic_max` | **[C]** (s11) — **was swapped with `@18`.** Sheet prints `MAGIC %u/%-3u` with `@18` before the slash and `@16` after (PHANTASI.COM `0x2f76`/`0x2f79`, format `0x300a`); the manual says the number after the `/` is the total. The Inn copies `@16 → @18` and increments `@16` on MAGIC INCREASES (OV1 `0x2a6d`/`0x2a82`). Fixtures can't tell (`@16 == @18` everywhere). |
| 18 | u16 | `magic_cur` | **[C]** (s11) — see `@16`. Restored to `@16` by both inns. |
| 20 | u16 | `hp_max` | **[C]** pass 3: crusho sheet `7/8` matches `@22/@20` (labels were swapped earlier) |
| 22 | u16 | `hp_cur` | **[C]** |
| 24 | u16 | `status` | **[C]** (PLAN H3, s10) DOS status word, runtime `+0x18`. Names from the 3-entry table PHANTASI.COM `0x6dfe`: **0 = DEAD, 1 = OK, 2 = SLEEP**. Sheet/Inspect read it at `0x2ef4`; Training rejects `== 0` (OV1 `0x119b`); Mystic counts only `== 1` (OV1 `0x2100`); revive helper OV1 `0x16b` sets it to 1 (and HP = max). DOSBox s10: byte-edited `@24=0` → **DEAD**, `@24=2` → **SLEEP** in Guild→Inspect (`working/snapshots/h3-status-s10/`). Was mislabelled `level` (every fixture holds 1 in both). No other status exists: UNDEAD is race `@256 = 15`, DESTROYED (OV1 `0x1ef`) removes the character. Core `CharacterStatus` uses these exact values (PLAN H4). |
| 26 | u16 | `word_26` | **Unused [C] (s58):** no reader or writer in COM/OV1/OV2; the only path would be script field id 19, which nothing passes. 0 in every fixture (s19); round-trip only. Was "weapon_dmg_A" — that label came from OV2 `0x5bcc` read with the wrong party base (`0x5bb2`); `0x5bcc` is `@28`. |
| 28 | u16 | `weapon_dmg_A` | **[C]** (s19) combat prep: damage effective `@248 = @28 + @240` (OV2 `0x3284..0x328c`); STRENGTH adds its bonus on top (`0x0527`). Fixtures: `@248 − @240` = `@28` for all 6. **Writer [C] (s28):** town Exit (OV1 `0x3249`, called from `0x207c` = case 3 of the town table `0x208f`) sets, for every party slot (DEAD included), `@28 = (3·@88·|7 − 2·@220| + 11·@0) / 66` (signed word math; `@220` read as a word, `@221` = 0 in all fixtures). Matches all 6 party members in every snapshot; fresh roster records never left town → 0. |
| 30 | u16 | skill: `attack` | |
| 32 | u16 | skill: `parry` | |
| 34 | u16 | skill: `find_item` | **[C]** pass 4: thiefo sheet `find item 5` ↔ `@34=5` |
| 36 | u16 | skill: `spot_trap` | **[C]** pass 4: thiefo sheet `spot trap 17` ↔ `@36=17` |
| 38 | u16 | skill: `disarm_trap` | thiefo = 45 |
| 40 | u16 | skill: `listen` | **[C]** pass 4: thiefo sheet `listen 2` ↔ `@40=2` |
| 44 | u16 | skill: `pick_lock` | thiefo = 40 |
| 48 | u16 | skill: `swim` | thiefo = 18 |
| 50..68 | u16 ×10 | `skill_bonus[i]` (record order, incl. unused `@42`/`@46` slots) | **[C]** seeded at creation as `2·linkedAttr + 30` (0 for the unused slots; all 22 fixture records match) and added into every level-up skill gain (OV1 `0x9ee`, `town-menus.md` §Level-up gains). Not rewritten when stats change later. Core `Character.SkillBonuses` (s17). |
| 70 | u16 | `score` | **[C]** pass 4: Mystic “Your score is **2**” → `@70` `0→2` on **all** party members (mirrored). Sheet screenshot still showed `SCORE 0` (taken before mystic, or UI not refreshed). |
| 92 | u16 | `roster_ordinal` / occupied marker | **[C]** set at creation to the 1-based creation order (fixtures 1..6); **nonzero = occupied**. The RANDOM-race duplicate guard tests `@92 == 0` for the empty slot (OV1 `0x0C59`). (The old note that OV2 `0x0a55`/`0x678c` reuse this offset as a transient flag used party base `0x5bb2`; with `0x5bb0` those writes hit `@94` — see below.) |
| 94 | u16 | `cast_since_rest` | **[C]** (s11) set to 1 in OV2 `0x0a55` (context [~]: same block writes `@18` MP current — likely the spell-cost path, no string anchor yet), consumed and cleared by the town Inn (OV1 `0x2a40`/`0x2a46`) and the wilderness inn (OV2 `0x678c`/`0x6793`) for the MAGIC INCREASES roll. Matches the manual's hint: "Characters who have cast spells during their adventures may find their maximum limit for magic has been raised after resting at an inn." Persisted in the record (0 in all fixtures). Town casting does **not** set it (s35 DOSBox: FIREFLASH I and HEALING I cast in town left it 0); wilderness/dungeon field casts (OV2 `0x4790`) don't write it either **[C] (s41)** — only the combat cost path does. |
| 216 | u8 | `race` (birth race → `Character.BirthRace`) | 0–14; ELF=2, HUMAN=0, LIZ.MAN=13 confirmed. The game **displays** `@256`, not this byte; the undead conversion (OV1 `0x113`) changes only `@256`, so `@216` keeps the living race. **Write-only [C] (s58):** written only at creation (OV1 `0x0dca`), no reader anywhere; round-trip only. |
| 218 | u16 | `in_party` | **[C]** (PLAN H3, s10) — **not a status.** "In the active party" flag, runtime `+0xda`: Guild→Add member (OV1 `0x2786`) sets it to 1 on the roster record before copying it into a party slot; Guild→Drop member (OV1 `0x225c`) sets it to 0 and copies the record back to the roster (`*(DS:0x63e0) + (@92−1)·0x11a`). Fixtures: 1 for party members; **0 for all 22** in `roster-full-22-s9` (party dropped). Explains s9: gobbo `@218=3` still showed OK. OV2 `0x6d38` (`[bx+0x5c8c]` tested `== 0` / `== 3`) is **not** this field — with party base `0x5bb0` that is `+0xdc` = class, i.e. THIEF / FIGHTER **[~]** (inference; that branch's purpose is untraced). |
| 220 | u8 | `class` | THIEF=0 MONK=1 RANGER=2 FIGHTER=3 PRIEST=4 WIZARD=5 |
| 256 | u16 | `race` (displayed → `Character.Race`) | **[C]** race shown on the sheet / Inspect (PHANTASI.COM `0x2f03`, 16-entry table `0x6d77`: 15 races + **15 = UNDEAD**). Equal to `@216` from creation; OV1 `0x113` sets it to 15 when a character turns undead (prints "%s [original text]", status → OK, HP → max, `@88` level := 20, `@240` := 15). DOSBox s10: `@256=15` → race **UNDEAD**, status OK. Also read by the creation duplicate-strong-race guard (OV1 `0x0C5F`). |
| 82 | u16 | `worn_shield` | **[C]** pass 3: crusho sheet `SM SHIELD` ↔ `@82=4`; fighto/thiefo/gobbo same; wizardo/priesto `0`/`0` |
| 84 | u16 | `worn_armor` | **[C]** pass 3: crusho `ROBES` ↔ `@84=22`; wizardo/priesto wear `CLOTHING` (`21`) |
| 86 | u16 | `worn_weapon` | **[C]** pass 3: crusho `CLUB` ↔ `@86=48`; gobbo `MACE` (`49`) |
| 88 | u16 | `level` | **[C]** (PLAN H3, s10) sheet `LEVEL%3u` arg (PHANTASI.COM `0x2f1d`, runtime `+0x58`); Training rejects `>= 15` (OV1 `0x11a1`); Mystic sums it. DOSBox s10: `@88=7` → sheet **LEVEL 7**. `@90` is also 1 in every fixture: **[C] (s39)** creation writes 1 (OV1 `0x0cbe`) and it is the **magical-pool product** — pool `T` (OV2 `0x656b`/`0x65cc..0x65e7`) skips a member whose `@90` is divisible by `k = 2T+1` (3/5/7), else multiplies by `k`, so each pool kind works once per character (`overworld-movement.md` §Magical pool, `endgame.md` §2.1, TEST id 55). `@90 ≥ 100` (DNG5 ev20) = all three pools used. Core creation writes 1 since s39. |
| 222..238 | u16 ×9 | carried items | **[C]** (s19) nine item ids, **worn items included** (manual: 9 items per character). Walked by the OV1 worn-item selector (`0x2fdb`, `+0xde`), the Mystic and the equipment screen (COM `0x2cf6`). Pass 2: thiefo bought DAGGER `46`, CLOTHING `21`, MAGIC 1 `111`, SCROLL 8 `128` into the free slots after club/robes/shield. Equipment-screen counts are **derived** from these ids, not stored: 101–110 HEALING potions, 111–120 MAGIC potions, 121–140 scrolls, 141–150 rings (shown as letters), 151+ treasures. **0 = empty slot; gaps are legal [C] (s58):** a drunk potion's slot is zeroed in place (OV2 `0x446b`, OV1 `0x31ac`), the Armory fills the first 0 (OV1 `0x1c18`), Distribute compacts (`0x2f22`). Core keeps the slot layout up to the last held item, and import/export round-trips the gaps. |
| 240 | u16 | `weapon_dmg_B` | **[C]** (s19) worn weapon points, written by the OV1 selector (`+0xf0`, `0x3085`). UNDEAD conversion sets it to 15 (OV1 `0x15f`). Fixtures: club/mace = 4. |
| 242 | u16 | `shield_soak` | **[C]** (s19) worn shield id (= its points), selector `+0xf2`. PARRY subtracts it from monster damage (OV2 `0xef3`); defense adds `/3`. |
| 244 | u16 | `armor_soak` | **[C]** (s19) worn armor points, selector `+0xf4` (ROBES 2, CLOTHING 1). |
| 246 | u16 | `armor_soak_eff` | **[C]** (s19) fight start copies `@244` (OV2 `0x327c`); PROTECTION sets `@244 + 1/3/6/10` (`0x05ab`); monster damage subtracts it (`0xebf`). |
| 248 | u16 | `weapon_dmg_eff` | **[C]** (s19) `@28 + @240` at fight start; STRENGTH adds 2/4/7/11 (`0x0527`). |
| 250 | u16 | `weapon_hit_eff` | **[C]** (s19) copy of **attack skill `@30`** at fight start (`0x3290`); QUICKNESS scales it `@30·(100+pct)/100` (`0x0498`); swings per round `= @250/75 + 1` (`0x45ed`). |
| 252 | u16 | `lifespan` | **[C]** (s11) set once at creation (OV1 `0x0eef`): `@10 × race factor`, integer math — HUMAN, PIXIE ×1; DWARF, HALFLING, GNOME ×5/2; ELF ×19; every other race ×2/5 (6-entry jump table OV1 `0x0ece`, code delta `0x53dc`; races ≥ 6 take the default). Reproduces all 22 fixture records exactly. Unit = `@264 >> 8`. Used by the town-entry aging check (`town-menus.md` §Aging). **Remaster:** not used on import — Core recomputes the lifespan from the D-003 band for the birth race and `@10` (user, s17; `DIFFERENCES.md` D-003). |
| 254 | u16 | `current_town` | **[C]** written at creation with the creating Guild's town (OV1 `0xdfb`), for all 7 party slots on town entry (OV1 `0x1b57`) and on Guild→Add (`0x2848`); Add asks first (OV1 `0x3b08`) when the member is in another town; every confirmed Add (same town too) then adds 4 to `@264` (s49). **Encoding [C] (s39, verified s56):** 0-based town record index (Pelnor = 0): `0x1b57` stores the town-loop argument, the endgame restart enters with `[0x64ee] = 0`, and every fixture party standing in Pelnor holds 0. Core town ids are 1-based (Pelnor = 1), so `GuildCodec` converts at the boundary: `Character.CurrentTown = @254 + 1`, written back as `max(0, id − 1)`. |
| 258 | u16 | `has_aged` | **[C]** (s11) latch: set to 1 the first time the aging check fires ("%s has aged", OV1 `0x1ad4`), so the ×4/5 stat penalty is applied once. |
| 260 | u16 | `party_order_stamp` | **[C structure]** `0` until the character joins an active party, then a **static** word: fixtures show 12/14/16/18/20/22 for party positions 1..6 = `2 × @92 + 10`. It does **not** change with game time (constant across pass2→3→4), so it is **not** a time/age clock (that is `@264`). **Write-only [C] (s58):** its one access is town Exit (OV1 `0x32eb`), which sets `@260 = 2·slot + 12` (slot 0-based); no reader anywhere (the load loop OV1 `0x866..0x89d` does not read it either, s56). The OV1 party loop `0x1760`–`0x17a2` once attributed to it uses `@262` (bank gold — the Deposit share split), not `@260`. Now persisted by the GUILD codec for round-trip fidelity. |
| 262 | u16 | `gold_in_bank` | **[C]** u16 LE; fresh=256; the sheet prints it as GOLD IN BANK (PHANTASI.COM `0x2f94`). Bank Withdraw moves it into the shared carried purse `[0x63f6]`; the town-entry / save-time share split (OV1 `0x167d`) deposits each member's share back into it (s35 DOSBox: 207 → 355 after a 148-GP share). The pass-2 `256 → 197` on all three records is that split after a 177-GP purchase: `(3×256 − 177)/3 = 197`. The revive helper OV1 `0x16b` does `and @262, 0xff` (clears the high byte) **[?]**. |
| 264 | u16 | `age_clock` (was `birth_stamp`) | **[C]** (s11) unit = **1/26 year (2 weeks)**: sheet `AGE` = `@264 / 26` unsigned (PHANTASI.COM `0x2f35`). Seeded at creation **[C]** (s15, OV1 `0x0f04..0x0f4f`): `260 + trunc(@252 · 256 · (0.1 + rand/1310720))` = 10 years + 10–12.5 % of the lifespan (inline doubles 1310720.0, 0.1, 256.0; all 6 fixture records fit). Grows: `+2` per spell learned (OV1 `0x165f`), `+4` on every Guild→Add (OV1 `0x284c`; s49: not only from another town), `+hi(time)/7` on town entry (OV1 `0x19bb`). Aging checks use `@264 >> 8` (≈ 9.85 years per step) — `town-menus.md` §Aging. |
| 266 | s32 | `experience` | **[C remaster]** sheet `%ld`; remaster stores `long`, GUILD codec r/w little-endian s32 @266..269 (low values match prior u16 samples). |
| 270 | 12 B | `name` | ASCII, space/NUL-padded |

**Inspect screens — decoded [C] (s19 derived counts; s49 static read of the renderer).** The counters are **not
stored**: they are counted from the nine carried items `@222..@238`. Capstone on the raw COM file works here (the
code `0x2a80..0x3060` and its strings sit inside one EXEPACK literal run; string ptr + `0x54b8` = file).

- **`0x2aa6` member view:** sheet (`0x2ef4`), then COM menu `0xa` (labels SPELLS / EQUIPMENT / SKILLS / POTIONS — label
  order from the string run `[~]`, dispatch `0x2af6` [C]): 0 → `0x2b18` "[original text]" (s58 [C] static: COM `0x2864`
  runs the same scrolling picker as casting, COM `0x139c` — see below), 1 → `0x2c65` equipment, 2 → `0x2b7e` skills, 3 → `0x2cf6` counters; a key (`0xfe0`) returns to sheet + menu;
  any other menu result leaves.
- **SPELLS page = the spell picker COM `0x139c` [C] (s58, static; manual silent).** Spell **names only** (no "#n"),
  one per row. The highlight bar stays fixed on **screen row 11**; the list scrolls under it, with up to **3** known
  spells shown above the bar and **2** below (rows 8..13; blank where the list runs out). Up/Down move the cursor
  through the known spells. The cursor is **one global word shared with every spell picker** (combat and town casting
  too): it starts at 0 and keeps its value between pickers. Return or Esc closes the page. Spells whose cost is above
  current MP go through `0x740`, but nothing visible changes (below); what `0x740` redraws is **[~]**.
  **[D✓] s58 DOSBox** (`capture_s58_2026-10-25.md` §4 (C), priesto with HEALING I / RESURRECTION and 1 MP): names only, no
  #n (`b6`). The full frame `b6` shows the title, **3 blank rows**, then the bar on HEALING I, RESURRECTION below and one
  blank row — the fixed-bar window. After Down (`b6_c` → `b7_c`, cut with the same crop box: the town-picture strip at
  the right edge is pixel-identical) the bar is on the **same screen row** and the names moved up one row under it. So the
  bar stays fixed and the list scrolls, as decoded; the capture's prose "the highlight bar moves" reads the crops the
  other way and is superseded by the frames. RESURRECTION (4 MP, 1 MP left) is drawn in the same white as HEALING I:
  **no colour difference** (only black/white pixels in the text area).
  Remaster (`SpellPicker`, `InspectDialog`): the same 6-row window with `>` on the bar row, one colour for every name;
  the shared cursor lives in one `SpellPicker` instance (the remaster's cast dialogs still use their own numbered lists,
  so only Inspect shares it for now); the cursor is clamped to the member's known-spell count; Up at the first spell
  and Down at the last stay put **[~]** (wrap behaviour not decoded).
- **Sheet `0x2ef4`:** `LEVEL%3u %s %s %s` = level `@88`, class, race, status (the manual's order, "LEVEL 1 PRIEST HUMAN
  OKAY"); `STRENGTH%8u AGE%15u` (`@264/26`); `INTELLIGENCE%4u HIT POINTS%4u/%-3u` (`@22/@20`); `DEXTERITY%7u MAGIC%9u/%-3u`
  (`@18/@16`); `CONSTITUTION%4u GOLD IN BANK%6u` (`@262`); `CHARISMA%8u EX. PTS%11ld` (`@266`); `LUCK%12u SCORE%13u`
  (`@10`, `@70`); then `runes:` + " AIR"/" EARTH"/" FIRE"/" WATER"/" GOD" for each set flag, printed only if `@72`, `@76` or
  `@78` is set (quirk, `endgame.md` §2.2). Status names DEAD / OK / SLEEP (the manual's example says OKAY, cosmetic).
- **Equipment `0x2c65`:** rows `@86` weapon, `@84` armor, `@82` shield, each `%-13.12sPTS:%-2u` with the points of `0x3053`
  (= `ItemRules.CombatGate`).
- **Skills `0x2b7e`:** `attack%7u parry%10u spot trap%4u disarm trap%4u pick lock%4u find item%6u swim%9u listen%9u`.
- **Counters `0x2cf6` → `0x2da5`:** over the nine items: 101–110 → hl, 111–120 → mg, 121–140 → scrolls, 151+ → treasures;
  141–150 write the letter `id − 76` ('A'…) at position `id − 141` of a 10-space buffer and set an any-ring flag. Prints
  `%-3uhl potions` (col 4) / `%-3umg potions` (col 21), `%-3uscroll(s)` / `%-3utreasures`; "[original text]" if
  position 9 (WAND 150) is set; the buffer is cut to 9 chars and `RING:%s` printed if the flag is set — so the letters
  are positional (rings A and C → `A C      `) and a wand alone prints `RING:` with blanks. Core:
  `Character.HealingPotions/MagicPotions/ScrollCount/Treasures`; Client `InspectDialog`.
- **Worn gear** — `@82` shield / `@84` armor / `@86` weapon are **[C]** (sheet match). Carried inventory is a separate list starting ~`@222`/`@228` (purchases land here without replacing worn slots). Unequip/equip diffs still useful to see whether soak/hit fields (`@240`–`@248`) recompute on equip or only at fight start.
- **OV2 party base = `0x5bb0` [C] (s19)** — histogram of `[bx+0x5bxx]` operands in OV2: status `+0x18`
  (`0x5bc8`) 42 hits, level `+0x58`, race `+0x100`, all even offsets from `0x5bb0`. Every row that earlier
  sessions derived with base `0x5bb2` was 2 too low; the combat rows above are re-derived and checked
  against the fixture bytes (`@246`, `@248`, `@250`, `@12` reproduce for all 6 pass-3/4 characters).
- **Field-id map** — `OV2` `FUN_0000_1ba5` maps spell/effect field ids → record words. Its offsets were
  noted with base `0x5bb2` — **re-derived s33 [C]** with base `0x5bb0`: full id → offset table in `combat-resolution.md` §Monster specials.
  **Unmapped ids never occur [C] (s49).** `0x1ba5` has two callers (`0x1d5a` read, `0x1d71` write), and those are
  called only from `0x1b7c`/`0x1b95` (inside the monster special `0x1a7b`, ids `0x39`/`0x3a`) and the dungeon script
  ops SET-MIN `0x288e`/`0x28a2` and TEST `0x28cb`. A scan of every script reachable from an event record in DNG1..10
  finds only ids **42–47, 55, 161** (SET-MIN 43..47; TEST 42..47, 55, 161). So the fall-through ids (14–17, 40, 41,
  51–54, 59, 121–151, ≥ 165) and the whole 60–120 block are never used by shipped data. (Scanning *every* script slot
  shows more ids, but slots start every 2 bytes and overlap, so those are mid-script decodes, not real scripts.)
- **`@94..@214` meaning [C] (s49, closes the watch-list `[?]`).** Field ids 60–120 map to `0x5c10 + 2·(id − 61)`:
  id 60 = `@94` `cast_since_rest`, ids 61..120 = `@96..@214` = `spells_known` DOS slot `id − 61` (rows above). Both
  fields were already `[C]`; only the per-id label was missing. DOS slots 54–56, 58, 59 stay unnamed (no spell-table entry names them).

**Resolved in M8:** experience width (s32 @266).

**Resolved in Phase 8 P0.1 [C]:**
- ~~`@28` dual-use~~ — **retracted (s19)**: built on the wrong OV2 base. `@28` is weapon damage A; the
  scroll count is derived from the carried items; weapon-hit effective copies the attack skill `@30`.
- **`@92` roster ordinal / occupied marker**, **`@256` race echo**,
  **`@218` status word** (0/1/3 code-confirmed), **`@260` party-order stamp** —
  see table rows above; all now read/written by `GuildCodec` with round-trip tests.

**`spells_known` [C]** — `u16[60]` at record `@0x60` (**DOS slot** × 2, `@96..@214`). Slots 0–53 are remaster
indices 0–53; **DIVINE I is slot 57 = `@210`** (remaster index 54); slots 54–56, 58, 59 (`@204..@208`, `@212`, `@214`) are
unnamed (s41, `magic-spells.md` §DIVINE I; `GuildCodec` maps 54 ↔ `@210` and leaves the empty slots to the raw record). Values: `0` unknown,
`1` known, `2` available to learn (`FUN_09EE`). New MONK/WIZARD set index 4
(FIREFLASH I); RANGER/PRIEST set index 0 (HEALING I).

**Skill storage order [C]** (≠ on-screen column order):
`attack, parry, find_item, spot_trap, disarm_trap, listen, pick_lock, swim`
at `@30,32,34,36,38,40,44,48`.

## Dungeon save slot (`DNG.SAV` + `DNGX.SAV`) [C] (s36 static, s35 DOSBox fixtures)

One save slot for one dungeon. It is written **only** by OV2 `0x5929(id)`, which is called from one place: the exit
handler `0x2d46`, when the player answers YES to "[original text]". The full flow (prompts, NO
path, re-entry) is in `dungeon-representation.md` §Leaving and re-entering. Fixtures: `DNG.s35.sav` / `DNGX.s35.sav`
(DNG1 after one fight, YES) and `DNG.pass2.sav` / `DNGX.pass2.sav` (DNG1 unchanged). The older "DNG.SAV is `0xFF`
bitfields" and "DNGX.SAV holds party/position" notes were wrong: both pass-2 files are a plain DNG1 save.

### `DNG.SAV` (6386 B) — id + map-screen image [C]
| Offset | Size | Content |
|---|---|---|
| 0 | 2 | dungeon id `[0x64ec]` (u16 LE, 1..10). The reader compares this word with the dungeon being entered |
| 2 | 6384 | the revealed-map screen image: 76 rows × (42 B from CGA bank `B800` + 42 B from bank `BA00`), rows from video offset `0x3d2` with stride `0x50` (`0x5929` reads video RAM directly; the reader `0x570b` copies it back into the back buffer `[0x5baa]`) |

The image is opaque display data. The remaster keeps the bytes for round trips but draws its own map **[I]**.

### `DNGX.SAV` (2500 B) — the level buffer [C]
The live 2500-byte level buffer `[0x6504]` (= DS `0xc6fa`), written by COM `0xc791` (create + write) from `0x5929`.
It is the `DNGn` file image (grid 1254 B + side tables + event records + scripts) with every in-play rewrite applied:
cleared fight cells (`0x2a26`), spent event records (`r0 := 0xff`), spent one-shot ops, SET-CELL writes (`endgame.md` §2.1).
s35: `DNGX.s35.sav` = `DNG1` byte-for-byte except offset 1240 (x 19, y 37) `0xCB → 0xC8` (a lock/ambush fight cell
cleared by `0x2a26`), then zeros to 2500.

The bytes after the `DNGn` file length (≈ 1922–1984 → 2499) are **stale** `[~]`: `DNGn` is read with a 2500-byte
limit (COM `0xc75a`), which stops at the file end, so the tail keeps whatever an earlier dungeon left there. It is zero
only for the first dungeon loaded since start-up. No code path that reads the tail was found.

**Not saved** `[C]`: party position and facing (re-entry always starts at the OUT entrance's `dest`), the wandering
encounter chance `[0x1a5a]` (reset to 7 by every dungeon entry, state 13), and the MESS text (re-read from `MESSn`
on every entry, `0x1e78`).

### Other writers `[~]`
OV1 `0x4ca8` / `0x4e76` read and write every game file (OUT*, SACK.DAT, DNG.SAV, DNGX.SAV, DNG1..10, GUILD/TWNS) as
one block for the Utility menu's backup / restore / **Reset the game** (`0x4a4b`; screen "CAUTION !!! / ALL DATA ON
YOUR GAME DISK WILL BE RESET / Press R for reset or X to exit"). Reset (`0x4885`, s204 static read) `[C]`:
1. **Erases every character:** for records 0..24 at `[0x63e0]` (stride `0x11a`) writes `@92 = 0` (the occupied marker)
   and `@218 = 0` (in_party). The 282-B bodies are left in place but every slot reads as empty.
2. `[0x64ee] = 0` and the town index `[0xc470] = 0` (Pelnor).
3. **Re-fogs the regions and re-arms fixed fights** (`0x4914(i)` for i = 0..17, skipping 0 and 4): for each of the
   `0x208` bytes of 1 KB block i, `b -= 120` if `b ≥ 120`; `b -= 1` if `b % 10 == 8` (a spent x8 fight back to x7);
   then `b += 120` (fogged) unless i is 0 or 4. The block → file map is `0x4ca8`'s read order: `0x400` B per file,
   OUT1..OUT16, OUT18, OUT19 (no OUT17), so blocks 0 / 4 = OUT1 / OUT5, which ship unfogged with no fight cells and are
   left as they are.
4. Zeroes `0x4800..0x6b2e` (SACK.DAT, DNG.SAV, DNGX.SAV), so every dungeon loads fresh.
This matches the manual (p. 19, BACKING UP CHARACTERS, key 5): "old characters will be erased and the dungeons reset —
just as if you had bought a new game". TWNS.DAT (town / Armory state) is not written by `0x4885`; whether `0x4a4b`'s
write-back restores it not traced `[~]` (the remaster resets towns, following the manual).

## Loot sack (`SACK.DAT`) [C] (s35 DOSBox)

120 bytes = **60 × u16 item ids** (the found-item list `0x63fa`, 60 slots), in insertion order, `0` = empty.
**No XP or gold field** — the combat XP/gold pool lives in RAM (`[0x63f4]`/`[0x63f6]`) and is split among the party
on town entry or Save game (`town-menus.md` §Town-entry share split). Items get into the sack from the Inn hand-out
screen's SACK row (and combat treasure). Fixture `extracted/samples/SACK.s35.dat` (13 items):

```
80 00 80 00 30 00 30 00 2e 00 2e 00 2e 00 15 00 15 00 04 00 04 00 04 00 04 00  then zeros
= SCROLL 8 ×2 (128), CLUB ×2 (48), DAGGER ×3 (46), CLOTHING ×2 (21), SM SHIELD ×4 (4)
```

All earlier fixtures (`original-files/`, passes 1–4) are zero — no combat or sack use. The remaster keeps its semantic
`LootSack` (XP/GP/items, 90 item slots per D-018).

## Town state (`TWNS.DAT`) [C]

3492 bytes = **12 towns × 291-byte records** (exact).

| Town-record offset | Type | Field |
|---|---|---|
| 0 .. ~180 | u8[] | Armory **stock quantity** indexed by extended item id (0..180). Fresh disk seeds many slots to `05`. Pass 2: Pelnor (town 0) slots 21/46/111/128 went `5→4` for CLOTHING / DAGGER / MAGIC 1 / SCROLL 8. |
| `0xb5` (181) | u16[45] | **Restock list [C] (s41)**: OV1 `0x8e1` sets `stock[list[i]] := 1` for 4 random `i ≤` average party level on every town entry (`town-menus.md` §Armory restock). Same list in the 11 shop towns; PHANTASIA all zeros |
| `0x10f` (271) | u16 | **town_price_index** used in buy formula `V² × index / 500 + 1` (Pelnor=150; see `items.json` / `data-tables.md`) |
| `0x111` (273) | u16 | **[C] (s39, s56: static; town Exit is its only reader)** overworld region of the town: OV1 `0x3309` copies it into `[0x63e2]` and region `[0x64ea]`; Pelnor 1, Pineville 5, Olympia 18. `0x113`/`0x115` = the town tile's x/y **[C] (s41)**: the same Exit code copies them into the party position `[0x63e4]`/`[0x63e6]` (OV1 `0x3313`/`0x331a`); 10 of the 11 map towns match their tile-40 feature cell; APPLETON's (OUT13 (17,19)) is a tile-41 inn cell and PHANTASIA's (OUT14 (5,11)) a plain tile (`overworld-movement.md` tile 41) |
| 279 .. 288 | 10 chars | Town name space-padded (e.g. `PELNOR`), mirrored from / with `TWNS.INT`; STARVILLE is NUL-padded |
| 289..290 | — | unmapped **[?]** (everything else is now mapped, s41); carried byte-for-byte by `TownsCodec` (P1.4) |

`TWNS.INT` still holds the static interior map grids; `TWNS.DAT` is the mutable
stock + pricing state.

## Remaster codecs (P1.4, s39) [I]

All five files import and re-export byte-identical (`DosSaveSetTests`, every fixture in `extracted/samples/` plus
`tests/.../Fixtures/GUILD.{h3status,roster22}.dat`):

- **GUILD.DAT** — `GuildCodec` keeps each imported 282-B record (`Character.Phantasie1.DosRecord`, saved in the session JSON) and
  writes known fields over it. Fields Core decodes lossily keep their carried bytes while unchanged: name padding
  (`wizardo\0  \0\0`), status words > 2, `@94`/`@218`/`@258` flag words ≠ 1, item-slot holes, and `@252` (the DOS
  lifespan goes back while the D-003 lifespan is unchanged). A changed item list rewrites all nine slots. Values that
  don't fit 16 bits (e.g. elf `@264`) are clamped and listed. Empty-name slots are written as zeros (all fixtures agree).
  **Field map check (P1.1, s49):** with the carry dropped, the codec alone rebuilds every byte of all nine fixtures
  except `@246..@251` (fight-start copies, not kept), `@252` (D-003) and name padding
  (`DosSaveSetTests.Guild_FieldMapAlone_RebuildsEveryMappedByte`). Runes `@72..@80` and `@90` are pinned by change
  (`Guild_RunesAndWord90_WriteOnlyTheirWords`) — no fixture holds a rune or a pool visit yet.
- **SACK.DAT** — `SackCodec`: 60 slots; `LootSack.ItemIds` keeps the slot layout itself (0 = hole, trailing empties
  trimmed), as DOS does **[D✓] s65**: a kept item fills the first empty slot, BEG FOR MERCY zeroes slots 0, 2, …, 48 in
  place (so a second BEG hits the same slots), and BEG also empties the carried purse. Slots past 60 (D-018's 90) are
  packed into free slots on export. XP/GP = 0 on import.
- **TWNS.DAT** — `TownsCodec`: stock (u8, clamped), price index, name over the carried 291-B record. Town id = index + 1.
  Towns built from `towns.json` carry the master-disk record (`dos_record`, from `extract_towns.py`), so a remaster-born game exports a full TWNS.DAT. Session towns are now kept between visits and in the JSON save (`GameSession.Towns`); the town-entry restock
  (OV1 `0x8e1`) is decoded and in Core (s41, `ArmoryRestock`); `TownsCodec` reads the `+0xb5` list into `TownState.RestockItems`.
- **DNG/DNGX.SAV** — `DungeonSaveCodec` (s36); no slot → both files zero.
- **`DosSaveConverter`** — import = `SessionSnapshot` + warnings (party = `@218 ≠ 0` in roster order, first 6 [C];
  town = `@254 + 1` of the first, Pelnor with an empty party when none is flagged [C]; wilderness position = the town's
  TWNS `+0x111`/`+0x113`/`+0x115` exit region/x/y [C]); a bank balance over the running save's `p1.gold-cap` is a rule
  break (D-048). Export blockers: not in town, sack > 60, XP/gold to split with nobody alive. The unsplit XP/gold pool is
  paid out as on town entry (D-037; remainders dropped under `p1.payout-remainder` = `dropped`) and the carried purse
  goes into the banks (DOS keeps gold only in `@262`), on copies (user s40). Unvisited towns come from `towns.json` (noted).

### Several parties and Continue (D-069, user s57: manual) [I] (s58)

Manual (ENCORE: Continuing Saved Games): "PHANTASIE allows several parties to be exploring concurrently, different
characters may be located in different towns"; [original text] asks which party member you want — "Give the Guild
number of the character you want to call up" — and takes you "straight to the town where that character was residing
at the close of the last game" with "the party he or she is a member of". DOS (static + DOSBox s56) keeps one party
(one `@218` flag per record, no question); the remaster follows the manual.

**Remaster save (`SessionSnapshot`, JSON):** the top level is the *active* party plus the *shared* world. Every other
party is a *parked* entry in `OtherParties`, holding only party-local state:

| Shared (top level only) | Party-local (top level = active party; each `OtherParties` entry = one parked party) |
|---|---|
| roster (`RosterSlots`, the one copy of every character), town Armory states (`Towns`), wilderness region bytes (`Wilderness.RegionTiles`), the one-slot dungeon save (`DungeonSave`) | member roster slots, carried gold, elemental (slot 7), sack (XP/GP/found items), mode + combat-return mode, town id, wilderness position + terrain, travel time, wilderness globals and pending menus/entry, dungeon state (live level buffer, pending trap/lock/event/exit, a script stopped at op 6 KEEP IT? (s58)), combat, pending treasure, defeat pending |

A parked party is kept exactly where it was (town, wilderness, dungeon, even mid-fight); remaster saves are
save-anywhere, so "the town where that character was residing" becomes "wherever that party was" — in a town when it
was saved in one, as the manual assumes. A party with no members is never parked (dropped instead).

**Continue (Load) [I]:** asks "GUILD NUMBER OF THE CHARACTER?" (1..22 = roster slot + 1, as on the L)IST MEMBERS screen). With the save
file's state:

1. Empty slot, or 23..25 (the elementals: "Town #0, not found in the Material Plane") → refused, nothing loaded.
2. The character is in the active party → load as saved.
3. The character is in a parked party → that party becomes active (its party-local state over the shared world); the
   file's active party, if it has members, is parked in its place.
4. The character is in no party (manual silent) → a new, empty party opens in that character's town (`@254`, the
   town's menu, as DOS `0x7e7` does for an empty party), ready for Guild A)DD; the file's active party is parked.
   This is the only way to start a second party while the first stays out.

**Membership rules [I] (manual silent):** a character belongs to at most one party. Guild A)DD refuses a member of a
parked party ("In another party."), whatever its `@218`, and so does P)URGE (a parked party may be mid-dungeon or
mid-fight, and its saved prompts and fight hold party positions; continue it and D)ROP the member first). R)ENAME refuses
a parked member as it does an active one (D-065: "cannot be renamed while they are members of a party"; parked
prompts also hold the name). Membership comes from the party lists, not from `@218` (a DOS import can leave
extra `@218` marks, D-069 Import note).

**DOS files:** import (F7) still makes one active party and no parked ones (one `@218` flag per record). Export writes
`@218` = 0 for members of parked parties (on copies) so that DOS, which loads every flagged record, gets the active
party only; a note lists them and says their purse and sack are not in the DOS files (DOS keeps one `SACK.DAT` and
gold only per character; the auto-split pays out the active party only). Without parked parties the export is unchanged.

## Remaster guidance

`OpenPhantasie.Data` should model the **character record** above as the canonical
schema (this is the single most reused structure — combat, towns, saves, the
char sheet all read it). Serialize saves as JSON, not the original packed
binary. ~~Keep a one-way importer~~ — superseded s38, see below. The GUILD
importer already exists (`tools/scripts/parse_guild.py`, validated against
`extracted/samples/GUILD.3chars.dat`) and is useful for regression-testing the
reimplementation against real save states.

**Compatibility scope agreed s38** (`08-parity-closeout.md` P1.4): **import + export** of all five files, byte-exact
where possible (unknown/stale bytes carried through), import as-is with warnings, out-of-range values clamped and
listed on export, export in town only. Known constraint: the roster stays **22 record slots** on disk even
though the remaster caps new characters at the manual's 21 (`DIFFERENCES.md` D-002), so a full DOS
`GUILD.DAT` (fixture: `working/snapshots/roster-full-22-s9/`) must still load.
