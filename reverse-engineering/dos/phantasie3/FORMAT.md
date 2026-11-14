# Phantasie III (DOS SSI 1988, + Amiga v1.0): format and RE notes

Phase 09 T4. Plan and status: `docs/plan/03c-phantasie3-re.md`. Files: `unpacked/README.md`
(DOS) and `reverse-engineering/amiga/phantasie3/unpacked/README.md` (Amiga). Tables: `extracted/` (written by
`tools/scripts/extract_dos_p3_tables.py`). Manual ↔ code conflicts: `DIFFERENCES-III.md`.

Tags: `[C]` confirmed from code or data bytes · `[~]` probable (value match, one sample, or reasoning) · `[?]` unknown.
Static unless marked live (DOSBox-X checks from s81: section 6 transfer; combat in `logic-specs/`).

## 1. Address conventions

| Convention | Meaning |
|---|---|
| **img x** | offset in the unpacked root load image (`extract_dos_p3_common.unpack_root()`, 0x2f840 B, sha1 `916786ad…`). Segment `s` = img `s·16` |
| **DS:x** | img `0x28c00 + x` (DGROUP paragraph `0x28c0`; DS:8 = "MS Run-Time Library - Copyright (c) 1987, Microsoft Corp") |
| **FAR:x** | img `0x18200 + x`: the 64 KB far-data segment `0x1820` (zero-filled in the file). Code reaches it through segment words kept in DGROUP (`mov es,[0x2aaa]` etc., all = `0x1820`; a few = `0x27f3` or `0x28c0`) |
| **ovlN x** | offset in overlay N's load image. Every overlay loads at segment `0x13d5` (img `0x13d50`), so ovlN x = img `0x13d50 + x` while N is resident |
| **Ghidra** | `build_unpacked_mz()` output loads at segment `0x1000`: Ghidra `SSSS:oooo` = img `(SSSS−0x1000)·16 + oooo` |

## 2. Program inventory (3c.2)

`MAIN.EXE` (152,564 B) `[C]`:

| Module | File offset | Size | Header | Relocs | Notes |
|---|---|---|---|---|---|
| root | 0x00000 | 0x1b8fb | 0x200 | 0 (packed) | **EXEPACK** (header at CS `0x1a22`, variant with skip_len, signature `RB`); unpacks to 0x2f840 B with 2503 relocations; real entry `03b2:2091`, SS:SP `2f85:0a58` |
| overlay 1 | 0x1ba00 | 0x0be2 | 0x200 | 73 | start-up: allocations, loads `creature.dat`/`initarms.dat`/`world.dat`, **I → III transfer** |
| overlay 2 | 0x1c600 | 0x4b48 | 0x800 | 413 | largest; town: guild, creation `0x227d`, training, spells, bank, mystic, armory, inn, equipment (`FUNCTIONS.md`) `[C]` |
| overlay 3 | 0x21200 | 0x1912 | 0x200 | 64 | wilderness: terrain, places ("Will you enter?"), River Styx `[C]` strings |
| overlay 4 | 0x22c00 | 0x27f4 | 0x400 | 159 | dungeons (doors, traps, treasure, dungeon save), endgame gods, copy-protection quiz `[C]` strings |

- Compiler: Microsoft C 5.x, 1987 runtime (`MS Run-Time Library`, `C_FILE_INFO`, MSEM87 float emulator at img
  `0x118f0`), large/medium model with far data `[C]`. Overlay manager: MS-LINK `int 3Fh; db overlay; dw offset` stubs
  ("Overlay not found", "Overlay Manager stack overflow"). 20 overlay calls in root code, 1 in ovl1 (→ ovl2 `0x227d`),
  2 in ovl3 (→ ovl4, ovl2) `[C]`.
- Root code: segments `0x000`, `0x284`, `0x3b2` (runtime + game), `0x6f6`, `0x7b9`, `0x834`, `0xb0b`, `0xf9c`, `0x118f`
  (float emulator), `0x13d5` (overlay area), `0x1820` (far data), `0x27f3`, `0x28c0`/`0x28c4` (DGROUP) `[C]` reloc
  targets. Ghidra 12 auto-analysis finds 372 functions in root `[C]`.
- **Copy protection:** a rulebook quiz (strings DS:`0x5032…0x5284`: "How many skills are there for an adventurer? (p. 3)",
  "How many different social classes are there? (page 23)", …) `[C]` strings; the same idea as II's. No disk protection
  seen `[~]`.
- **Hidden character editor:** "Standing by for Edit mode..." + a field menu (Race, Class, Level, Score, Age, MaxAge,
  SocClass, Origin, Townin, Phant1?/Phant2?/Phant3?, Expernce, GoldBank, Status, the six stats, eight skills, HitPts,
  HPAvail, Magic, MgcAvail, MaxPow, Combat Opt, Spell Opt, Body, Spells, PartyGold/Exp/Items) at DS:`0x45b0` `[C]`
  strings; ovl1 `0x88c`/`0x8a5` load its message index (0x1b0) `[C]`; how it is entered `[?]`.
- **Tools:** `tools/scripts/extract_dos_p3_common.py` (unpack, overlay split, rebuilt MZ with relocations and the
  overlay stubs rewritten as far calls for Ghidra/rea; `python3 -I … <outdir>` writes `main_root_unpacked.exe` and
  `main_with_ovl1..4.exe`). Ghidra headless: `analyzeHeadless <proj> P3DOS -import <outdir>/main_*.exe -scriptPath
  tools/scripts -postScript GhidraExport.java <out> -postScript GhidraDecompile.java <out>` (outputs kept out of git,
  regenerate as needed). Capstone (`tools/scripts/cross-version/dis86.py`) on the img works everywhere.
- **WizardWorks 1991**: same `MAIN.EXE` and data, only batch files differ (`unpacked/README.md`) `[C]`.

**Amiga** `PhantasieIII`: hunk exe, CODE 0x21888 / DATA 0x277c (+BSS) / BSS 4, Manx Aztec C, not packed `[C]`
(`tools/scripts/extract_amiga_p3_common.py`). Same title text "Amiga Version 1.0 by …"; DOS: "IBM Version 1.0 by
Westwood" (DS:`0x3edd`) `[C]`.

## 3. The master string table (DS:0x1308)

One array of far pointers (offset, segment `0x28c0`), 554 entries until the first non-DGROUP segment `[C]`. All of
`extracted/strings.json`; groups in `extracted/names.json`:

| Index | Content |
|---|---|
| 0–18 | combat action labels (Front/Middle/Back, "Attacks (R1)", "Fires Bow (R1)", "Aims Blow (R1)", "Casts Spell", R2 forms) |
| 19–33 | short status (DEAD OKAY SLPG OUT DOWN), short classes, rank labels |
| 34–53 | wilderness places ("The Pendragon town archives", "The Castle of Light/Dark", …) and 8 inns |
| 54–75 | terrain (Meadows … Steam, incl. Netherworld, Lava, Bright Mist, Ocean, The River Styx, Black Path) |
| 76–84 | **9 skills:** Attack, Parry, Swimming, Listen, Find Trap, Disarm Trap, Find Item, Pick Lock, Fire Bow |
| 85–102 | **races:** Human 0, Dwarven 1, Elven 2, Gnome 3, Halfling 4, Random 5, Gnoll 6, Goblin 7, Kobold 8, Lizard Man 9, Minotaur 10, Ogre 11, Orc 12, Pixie 13, Sprite 14, Troll 15, Elemental 16, Undead 17 |
| 103–108 | **classes:** Fighter 0, Thief 1, Priest 2, Ranger 3, Monk 4, Wizard 5 (I: THIEF 0 MONK 1 RANGER 2 FIGHTER 3 PRIEST 4 WIZARD 5) |
| 112–125 | **14 towns:** Pendragon, Sandy Shores, Pendleton, Rocky Hills, Deltor, Tirith, Tierrin, Asmith, Sierion, Xanador, Flagler, Lansing, Scanport, Sciattica |
| 126–134 | planes Light/Dark/Death; **origins** Gelnor 0, Ferronrah 1, Netherworld 2, Scandor 3; Light, Darkness |
| 135–139 | status: Dead 0, Okay 1, Asleep 2, Out 3, Down 4 |
| 140–145 | **social classes:** Peasant 0, Labourer 1, Craftsman 2, Noble 3, **Darklord 4, Superhero 5** |
| 146–149 | body-area states: Okay 0, Injured 1, Broken 2, Gone 3 |
| 150–330 | **item names**, id 0..180 (150 + id) |
| 331–388 | **spell names**, #1..#57 (331 + n; #53 is an empty string) |
| 389– | UI messages (prompts, Guild/Bank/Mystic texts, the sheet template, editor menu, …) |

Darklord / Superhero are endgame titles: DS:`0x52a1…` "[original text] [Pluto./Zeus.] … You have done a
great service for the cause of [evil/good] … I grant thee the highest [original text] are now …"
`[C]` strings, `[~]` that it writes social class 4/5 (two Amiga save characters hold 5).

## 4. Master tables (3c.3) → `extracted/`

### 4.1 Items (`items.json`)

- Names: string 150 + id, ids 0..180 `[C]`. 1–20 shields, 21–40 armour, **41–60 bows**, **61–100 melee weapons**,
  101–110 healing potions, 111–120 magic potions, 121–140 scrolls ("Scroll I" … "Scroll XVI", with III and VII three
  times each), 141–179 treasures (gems, keys "Dark Key"/"Light Key", "Gem of Light", …), **180 Wand of Nikademus**.
- **Points** `DS:0x113c + id` and **attribute requirement** `DS:0x11a2 + id`, ids 0..100, u8 `[C]` (read as
  `[bx+0x113c]` at root `0x0f84`, `0x8cae`, `0xb85a`, `0xc4da`… and `[bx+0x11a2]` at `0x0f9b`). Unlike DOS I (points
  and STR gate computed from the id), III keeps them in tables.
- **Every point and requirement value equals the manual's tables** (III PDF 11–12 / pr.17–18) for all 100 equipment ids
  `[C]`. Only names differ (all in D3-005): #81/#82 "Dagger +1/+2" (manual "Knife +1/+2"), #52 "Compound Bow +2"
  (manual prints "+1" twice), "God Armour" / "Halberd" (manual "God Armor" / "Halbred").
- Which attribute sum each requirement is compared with `[C]` (ovl2 `0x39b5`, called by the disburse screen
  ovl2 `0x061f` and by "ready best equipment" ovl2 `0x37f1`): ids 1–40 (shields, armour) **STR**, 41–60 (bows)
  **STR + DEX**, 61–100 (melee weapons) **2·STR + DEX**; any other id 0. An item is usable when that sum ≥
  `requirement[id]`. This equals the manual.
- **Value** `DS:0xf92 + 2·id` (u16, ids 0..180) `[C]` bytes, and the **town price index** `DS:0x10fc + 2·town`, indexed
  by the town number (100 unused, 150 Pendragon, 145, 140, 130, 125, 200, 115, 200, 105, 100, 88, 95, 90, **123
  Sciattica**, 100, 100, 100 for towns 0–17) `[C]` bytes; that it has 18 entries, one per `INITARMS.DAT` row, is `[~]`.
  The armory build (root `0x0fb8…0x102e`) walks the town's stock bytes and stores `value · index[FAR:0xdd98] / 100`
  (long math) per stocked id `[~]`; town 1 = Pendragon = entry 1 (150) `[C]`. `INITARMS.DAT` (= Amiga `inititem.dat`):
  the per-town stock table, section 9.3.

### 4.2 Spells (`spells.json`)

57 names `[C]`: as I for #1–12, 17–52, 54 except: **#13–16 Arrow Flame I, P.Arrow Flame I, Arrow Flame II, P.Arrow
Flame II** (I: Strength I–IV); **#53 empty** (Vision removed, as the manual says); **#55 Accuracy, #56 Party Accuracy,
#57 Divine Aid** (the only divine spell name). "Ninja II" is #45 and "Ninja I" #50, as in I and the manual. Spell
learnability per class/level: `DS:0x1208` = **13 rows × 15 levels** of spell numbers (0 = none), cell for cell
the manual's "Spells Available at Different Levels by Class" (III PDF 10 / pr.14): Fighter 1 row, Thief 1, Priest 3,
Ranger 2, Monk 2, Wizard 4, in that order `[C]` bytes; the per-class row grouping is `[~]` (by the manual match; the
Learn Spells code ovl2 `0x1a6c` was not read). No spell appears twice in a class. MP costs: the manual's table gives
`(n − 1) % 4 + 1` for every listed spell (#54 Transportation 2); the code's cost rule and Divine Aid's cost are `[?]`.

### 4.3 Classes, races, origin, social class

See section 3. The I → III maps (used by the transfer) `[C]`: class `DS:0x48` = `[1,4,3,0,2,5]` (THIEF→Thief, MONK→Monk,
RANGER→Ranger, FIGHTER→Fighter, PRIEST→Priest, WIZARD→Wizard); race `DS:0x4e` =
`[0,1,2,4,3,13,14,11,6,15,8,12,7,9,10,17]` (every I race to the same-named III race; I's UNDEAD 15 → Undead 17).
Class modifiers / race modifiers / race maxima `[C]`: static DGROUP tables (the segment
words `[0x5a50]`, `[0x5a52]`, `[0x5a54]` are all `0x28c0`), one signed byte per attribute a = STR, INT, DEX, CON, CHA:
**class modifier** `DS:0xf24 + 5·class` (6 rows), **race modifier** `DS:0xf42 + 5·race`, **race maximum**
`DS:0xa28 + 5·race` (16 rows each, race 5 "Random" all 0). Creation (root `0xa719`, `attribute(a, class, race)`):

```
v = rnd(6) + rnd(6) + rnd(6) + (class_mod − 10) + (race_mod − 10) + 3      # = 3d6 + class_mod + race_mod − 17
v = min(v, race_max);  v = max(v, 3)
```

Every modifier cell equals the manual's tables (III PDF 4 / pr.3, which are I's manual values: Fighter +2 −2 +1 0 −1 =
12 8 11 10 9, …). Every race-maximum cell equals the manual except **Kobold CHA = 3** (manual 13) — **D3-034**.
Halfling DEX maximum is 20 here (I's DOS had 19, D-001). Every race-maximum cell but Kobold CHA equals race_mod + 8, so
the cap is the same as I's `min(18, roll + class − 7) + race − 10`; what differs from I's creation (as the remaster's
shared `CharacterCreation.RollAttributes` does it) is the **roll**: III draws **3d6** (3–18, bell-shaped), I one uniform
draw (`word / 1821`, 0–17), so III attributes run about one point higher at the bottom and cluster in the middle. Floor 3
in both. LCK: III `rnd(6) + 7`, I `word / 5462 + 8`, both 8–13. The bytes after each table are the next tables (HP dice `0xa78`, item values `0xf92`).

### 4.4 Monsters (`monsters.json`)

`CREATURE.DAT` = 80 records × 38 B + 32 pad bytes `0x60`; byte-identical to the Amiga `monsinfo.dat` `[C]`. **16-bit
fields are big-endian** (the file is shared with the 68000 version) `[~]`. Overlay 1 mallocs `0xbe5` B for it at
`FAR:0xfd1e`; code indexes it with `idx·0x26` (root `0xbbfa`, `0xbc23`, `0xefe4`, `0x11219`) `[C]`.

| Off | Field | Tag / evidence |
|---|---|---|
| 0 | name, 14 B NUL-padded | `[C]` |
| 14 | max group size | `[~]` 1 for every boss, 2 for dragons/giants |
| 15 | attack power ÷ 10 | `[~]` = DOS I attack_power/10 for 33/35 shared monsters |
| 16 | damage | `[~]` DOS I damage (often +1) |
| 17 | armour | `[~]` = DOS I for 35/35 |
| 18 | defence (s8) | `[~]` 33/35 |
| 19 | level | `[~]` 35/35 |
| 20 / 21–23 | special chance / 3 specials | `[~]` 32/35 |
| 24 | combat rating | `[~]` 35/35 |
| 25 | HP base | `[~]` 35/35 |
| 26 | reaction modifier (s8) | `[~]` 34/35 |
| 27 | **picture id** (Amiga: sheet and picture, ≥ 48 = none; > 41 = a giant picture, which also drops the second group and caps the count at 2) | `[C]` Amiga (`reverse-engineering/amiga/phantasie3/FORMAT.md` section 6); **DOS the same `[C]` (static)**: the group maker (root `0xefd8`, `0xf0ef…0xf137`) copies byte 27 to each group's `+0x32`; a rank-1 value > 41 drops rank 2 (its id := −1), sets rank 1's `+0x48` flag and caps its count (`+2`) at 2; a rank-2 value > 41 drops rank 2; a dropped rank 2 gets count 0 (built in Core) |
| 28 | next-group chance | `[~]` |
| 29–30 | companion monster ids | `[~]` |
| 31–32 | treasure item ids: the two items the lead group can leave after a fight (root `0xee93`, `logic-specs/encounters.md` section 4) | `[C]` |
| 33 | flag: 1 on undead and demons (+ Constrictor) | `[?]` purpose; candidate for Dispell Undead |
| 34–35 | gold (BE) | `[~]` 34/35 |
| 36–37 | XP (BE) | `[~]` (Skeleton 77 = I; Viper 731 vs I 732) |

**No height field and no reputation field is evident** `[?]`: the manual's Monster Reputation Table (Good/Neutral/Bad,
fighting skill) and the "very tall monsters" rule must come from code or from fields above (26 reaction, 14 group).
35 of 80 names are shared with DOS I; 45 are new (Sniverling … Boo Boo Kitty, two Nikademus records, Lord Wood)
(`diff-vs-p1.json`).

## 5. Character record (3c.4)

**262 bytes** `[C]` (Amiga `gamesave.dat` stride 0x106; DOS clears a 262-B new-character buffer at `FAR:0x9de8`). All
fields are 16-bit words except the name; DOS little-endian, Amiga big-endian `[~]` (Amiga sample values fit). Offsets
below are record-relative (= `FAR:0x9de8 + off` in DOS). Sources: the Inspect sheet (root `0xa292`, prints each field at
a fixed row/column) `[C]`, the creation skill seeding (root `0xa1f2`) `[C]`, the transfer (ovl1) `[C]`, the creation /
admission code (ovl2 `0x227d`) `[C]`, combat and equipment code (s79, `logic-specs/`) `[C]`, Amiga sample `[~]`.

**Where records live (DOS)** `[C]`: the roster = 20 records at `FAR:0xe820 + 0x106·i`; the party = 7 slots
(6 members + an elemental) at `FAR:0x236 + 0x106·i`, party size `FAR:0xb614`, elemental-in-party flag `FAR:0xb612`;
`FAR:0x9de8` is a work copy (combat copies a member there and back with `rep movsw`, 0x83 words).

**Cross-check with the Amiga `gamesave.dat`** `[C]` data: in all 26 live records the worn ids fall in the
right ranges (shield 1–20, armour 21–40, weapon 61–100, bow 41–60 or 0) and appear in the carried list; `+0x26` equals
the number of non-zero carried ids and `+0x56` the number of non-zero spells; ranks are 2 or 0; fresh characters
carry exactly the kit `[4, 22, 41, 64]` (Wizards with spell 5). The Amiga file holds 25 roster slots (elementals
Air / Mist / Mud in 20–24) and then the 7 party slots (25–31).

| Off | Field | Tag / evidence |
|---|---|---|
| 0x00 | name, 8 B (7 chars + NUL) | `[C]` sheet; transfer copies 7 chars, writes NUL at +7 |
| 0x08 | **combat rank**: 2 Front, 1 Middle, 0 Back (new characters 2) | `[C]` change-rank root `0xfb5f` stores 2 − menu choice; S30+rank prints "(Back)/(Mid)/(Front)"; creation writes 2 |
| 0x0a–0x14 | STR, INT, DEX, CON, CHA, LCK | `[C]` sheet rows 11–16 |
| 0x16 | `[?]` (0) | |
| 0x18 | **Phantasie II score** ("Phant2?"): the Mystic adds "[original text]" when ≠ 0; dungeon events read it as II's score (Filmon: ≥ 200 "who destroyed the orb of Ferronrah", 1–199 "could not"; the archive altar needs ≥ 200, `logic-specs/dungeon-events.md` section 5–6); DOS's transfer never sets it | `[C]` ovl2 `0x2ca2` (s81), DNGS ops (s82) |
| 0x1a | magic ceiling for class and level ("MaxPow"): the inn sets it to `table[class·15 + min(level, 15)]` (byte table at `0xafb` of segment `[0x5e16]` = DGROUP, i.e. `DS:0xafb`: cell for cell the manual's Maximum Magic Points table); creation: `table[class·15 + 1]` | `[C]` inn ovl2 `0x347d`, creation ovl2 `0x227d` |
| 0x1c / 0x1e | magic max / current (sheet "Magic Power: cur/max" prints +0x1e then +0x1c). Creation: both = 75 % of +0x1a. At an inn, if there was no inn stay since the last fight (`FAR:0xde70` clear; combat clears it, the inn sets it) and max < ceiling: max +1 with chance 20·(ceiling − max) % ("Magic increases!"); every inn sets current = max | `[C]` |
| 0x20 / 0x22 | HP max / current (sheet prints +0x22 / +0x20) | `[C]` |
| 0x24 | in-party flag (≠ 0: "[original text]") | `[C]` add-member ovl2 `0x3a0a`; creation writes 0 |
| 0x26 | **number of carried items** (0–9) | `[C]` creation writes 4 with the 4-item kit; inventory / disburse loops |
| 0x28 | age clock, 1/26 year (sheet Age = +0x28 / 26, as I's `@264`); +1 per inn stay | `[C]` |
| 0x2a | maximum age: `⌊LCK · race factor⌋` at creation (section 5.1); no reader found in DOS (D3-041) | `[C]` ovl2 `0x2719`; editor field "MaxAge" |
| 0x2c | status (Dead 0, Okay 1, Asleep 2, Out 3, Down 4) | `[C]` |
| 0x30 + 4k | **skill values**, k = Attack 0, Parry 1, Find Item 2, Find Trap 3, Disarm 4, Listen 5, **Fire Bow 6**, Pick Lock 7, Swim 8 | `[C]` sheet (Attack +0x30, Parry +0x34, Swim +0x50, Listen +0x44, Find Trap +0x3c, Disarm +0x40, Find Item +0x38, Pick Lock +0x4c, Fire Bow +0x48) |
| 0x32 + 4k | **skill seeds** (I's `@50..68` bonus, added on training `[~]`): 2·STR+30, 2·DEX+30, 2·INT+30, 2·INT+30, 2·DEX+30, 2·INT+30, *Fire Bow*, 2·DEX+30, 2·CON+30 | `[C]` root `0xa1f2` |
| 0x4a | Fire Bow seed = **2·Level + 4·DEX + STR + 10, +30 if Thief, +15 if Elf** | `[C]` root `0xa1f2…0xa28a` (Thief/Elf bonuses match the manual) |
| 0x54 | **experience shares**: the town entry's "Shares for <name>:" (ovl2 `0x0000`(1), every character) stores the typed value, living 1–3 (pre-filled with this word; 0 or less → 1 first), dead 0–3 (pre-filled 0). Payout: `r = pool mod Σshares` points to `rnd(characters)` one by one, then each character shares × ((pool − r) ÷ Σshares); creation writes 1 | `[C]` ovl2 `0x0000` (`0x1af`, `0x2a8`, `0x49b…0x57f`) (`capture_s143_town_entry.md` 3) |
| 0x56 | **number of known spells** (≤ 32) | `[C]` root `0xabbe` appends spell n (1–56) at +0xb0 + 2·count |
| 0x58 | origin: Gelnor 0, Ferronrah 1, Netherworld 2, Scandor 3, Light 4, Darkness 5 (sheet prints string 129 + origin) | `[C]` sheet; creation: 3, or 4 / 5 / 2 when created in place 15 / 16 / 17 (ovl2 `0x227d`) |
| 0x5c | Phantasie I score (transfer) ("Phant1?") | `[C]` transfer writes the I score here |
| 0x5e | current town (1 = Pendragon) = `FAR:0xdd98` at creation | `[C]` creation; transfer writes 1 |
| 0x68 | **worn shield** (item id 1–20, 0 = none) | `[C]` ready-best ovl2 `0x37f1`; shield points feed parry/defence (root `0xd730`) |
| 0x6a | **worn armour** (21–40) | `[C]` ovl2 `0x37f1`; root `0xd730` defence |
| 0x6c | **wielded weapon** (61–100; 0 = bare hands) | `[C]` ovl2 `0x37f1`; root `0xd730` weapon damage |
| 0x6e | **last combat action code** (string 3 + code: 0 Attack … 5 Parries … 6 Aim, 11–16 R2 forms; editor "Combat Opt"); creation 3 (Thrust) | `[C]` root `0xd4ff` copies the fight's last choices here at fight **end**; `0xd55b` loads it as the next fight's default; monster melee tests it for Parry (`0xb64e`), so only a saved Parry counts (D3-036). Amiga sample: 0, 2, 3, 5, 13, 15 |
| 0x70 | **last spell chosen** in combat (editor "Spell Opt"); creation 0 | `[C]` root `0xd4ff`; Amiga sample 4, 5, 8, 39–41, 56 |
| 0x74 | **level** | `[C]` sheet row 4; creation 1 |
| 0x7e | roster slot (the party record is copied back there, root `0x853d`) | `[C]` |
| 0x82 | score (recomputed from `+0x8a` and level at the Mystic and on leaving a dungeon; Darklord held at 200–249, Superhero ≥ 250) | `[C]` sheet, root `0x9884` |
| 0x86 | **bow slot** (41–60) | `[C]` ovl2 `0x37f1`; combat bow damage and the Fire Bow option test it (root `0xd730`, `0xfc41`) |
| 0x88 | unused: no access in the root or any overlay (work copy, party and roster bases) and no DNGS op addresses it | `[C]` |
| 0x8a | **quest bits** 0–7; score = Σ weight[bit] (DS `0x1c50`: 0, 4, 8, 12, 17, 20, 25, 30) + level / 6 (root `0x9884`); bit 2 (the cursed wand) also tested by the inventory screen. **Written only by DNGS event ops** (type 24, all party records): Kilmor found, funeral, wand, Old Bow, Gnome Bow, Lord Wood, Castle of Light, Castle of Darkness | `[C]` reader, writers (`logic-specs/dungeon-events.md` section 4) |
| 0x8c | **deed bits**, written only by DNGS ops: 0 Guaggle Beast, 1 / 2 soups, 3–5 statues broken, 6 Gem of Light, 7 Light Fairy attacked; events compare the word as a number (≥ 1 / 8 / 56 = evil). Amiga 7 / 71 fit | `[C]` (`logic-specs/dungeon-events.md` section 4) |
| 0x8e | social class (0–5) | `[C]` sheet; creation roll (section 5.1) |
| 0x94 | class | `[C]` sheet + Thief bonus |
| 0x96 + 2k | **9 carried item ids**, k < +0x26 (worn items stay in this list too) | `[C]` creation kit, inventory, ready-best loop |
| 0xa8 | race | `[C]` sheet + Elf bonus |
| 0xb0 + 2k | **known spells**, k < +0x56 (32 slots, to 0xee) | `[C]` root `0xabbe` appends; the Amiga sample lists them in descending order (52, 51, 49, …), so the list is sorted somewhere `[~]` |
| 0xf0 | **Divine Aid** (#57) slot, holds 57 once learned | `[C]` root `0xabbe` writes it apart from the list |
| **0xf2 + 2k** | **six body areas**, k = **Head 0, Left Arm 1, Right Arm 2, Torso 3, Left Leg 4, Right Leg 5**; each 0 Okay / 1 Injured / 2 Broken / 3 Gone | `[C]` sheet loop (root `0xa6ce`, labels string 456 in that order), character-injury messages (root `0xe9ee`: "'s left arm" → 1, "'s right arm" → 2, "'s torso" → 3, …), status rules (root `0x107b`) |
| 0xfe | gold (u32); creation: social class · 256 | `[C]` sheet, creation |
| 0x102 | experience (u32) | `[C]` sheet |

**Answer to the manual's 4-vs-6 question (III Q6): the record holds six areas** `[C]`. No per-area HP field exists
(Q8): an injury's severity comes from the hit's damage against the HP left (`logic-specs/body-areas.md`) `[C]`.
The Amiga art is `Graphics/anatomy1/2.csh`.

### 5.1 Creation, admission and the class kit (ovl2 `0x227d`) `[C]`

One routine creates a new character and admits a transferred one (`FAR:0xb5e6` ≠ 0 = transfer: race / class / name
prompts skipped). It needs a free roster slot (`FAR:0x97c0` < 20). It writes, in order:

- race (`+0xa8`, choice 5 "Random" = `rnd(10) + 5`, so 6–15), class (`+0x94`);
- level 1, `+0x54` = 1, status Okay, XP 0, `+0x24` = 0, rank Front (2), town = current, origin (above);
- magic ceiling / max / current (`+0x1a`, 75 %), HP (`+0x20`, a sum of `rnd(2)+1` dice, count from a class table at
  `0xa78` of segment `[0x5ca4]`, stride 0x16);
- **social class** `+0x8e`: `r = rnd(100)` for races 0–4 (Human, Dwarf, Elf, Gnome, Halfling), `rnd(75)` for all
  others; r < 30 Peasant, < 60 Labourer, < 90 Craftsman, else Noble (so other races are never Noble). `rnd(n)` is
  `rand() % n + 1` (root `0x2c31`). **Gold** `+0xfe` = social class · 256 (0 / 256 / 512 / 768);
- **attributes** (new characters only; a transfer keeps its own) ovl2 `0x3e60`: STR, INT, DEX, CON, CHA in that order,
  each `attribute(a, class, race)` (three `rnd(6)` each, section 4.3), then **LCK = rnd(6) + 7** (8–13) `[C]`;
- **maximum age and age** (ovl2 `0x2691…0x2748`, MSEM87 emulator code `int 34h…3Dh`) `[C]`: `f` = LCK as
  a single-precision float; races > 4 `f = f · 2.0 / 5.0`; races 1 Dwarven and 4 Halfling `f = f · 5.0 / 2.0`; race 2
  Elven `f = 19.0 · f`; races 0 Human and 3 Gnome keep `f` (constants DS `0x5e18` 2.0, `0x5e20` 5.0, `0x5e28` 19.0f,
  `0x5e2c` 260.0). A `race == 13` / `== 14` test sits on the races > 4 path but every race > 4 takes the 2 / 5 branch,
  so Pixie and Sprite get 2 / 5 too (the race-13 test in the 5 / 2 branch is dead code). **Maximum age** `+0x2a` =
  `_ftol(f)` (root `0x3b2:0x2d9d`, truncation); **age clock** `+0x28` = `_ftol((rnd(7) + 24) · f + 260.0)` (1/26 year).
  Human LCK 10: 19.6–21.9 years, maximum 10; Elf LCK 10: 193–237 years, maximum 190;
- **class kit**, the same for every class except the bow: 4 items carried (`+0x26` = 4, list `[4, 22, bow, 64]`) and
  worn: shield **4 Small Shield** (`+0x68`), armour **22 Robes** (`+0x6a`), bow **44 Short Bow for a Thief, else 41 Self
  Bow** (`+0x86`), weapon **64 Mace** (`+0x6c`);
- **first spell** (`+0xb0`, count `+0x56` = 1): **Priest and Ranger #1 Healing I, Monk and Wizard #5 Fireflash I**;
  Fighter and Thief none. Answers III Q4 ("normal beginning spell by class") `[C]`;
- body areas all Okay; then the name prompt (new characters) and the roster insert (root `0x834:0x1d5b`).

**Random-draw order at creation** (ovl2 `0x227d…0x2757`) `[C]`: race `rnd(10)` (only for "Random") → HP dice
(`rnd(2)` per die) → social class `rnd(100)` / `rnd(75)` → STR, INT, DEX, CON, CHA (3 × `rnd(6)` each) → LCK `rnd(6)` →
age `rnd(7)` → nothing more (skill seeds root `0xa1f2` and starting skills root `0x9ea0` draw nothing). HP is rolled
before the attributes, so it cannot depend on CON (D3-022). "Keep character?" No goes back to the race menu `[~]`.
Remaster: `Phantasie3Campaign.RollNewMember` (the `ICampaign.RollNewMember` hook) draws in this order, 3d6 by
`RollAttribute` (cap = race_mod + 8, the manual's table); under the manual's HP (D3-022, CON counts) the HP dice move
after LCK. The age clock starts at DOS's value (`P3CreationAgeRule`, `rule-tables.json` `creation_age`); the maximum
age is not kept: D3-041 (manual) gives III characters I's lifespan bands instead.

Transferred characters therefore get the same kit and first spell (FORMAT section 6), plus the gold of their social class
(Noble = 768) before the transfer adds old gold ÷ 35 `[~]` (order of the two writes not traced).

**Equipping** (ovl2 `0x37f1`, "ready best equipment") `[C]`: for each carried item the character can use (attribute
sum ≥ requirement, section 4.1) it wears the item in the matching slot when its point value is ≥ the worn item's
(shields 1–20 → `+0x68`, armour 21–40 → `+0x6a`, bows 41–60 → `+0x86`, weapons 61–100 → `+0x6c`). Ties go to the later
item. A slot is never emptied: a worn item no longer carried or usable stays until replaced; a character with no items
is skipped (`capture_s143_town_entry.md` 3). The disburse screen ("Player: Class: SH AR BW WP", ovl2
`0x061f`) shows the four slots.

## 6. The I → III transfer (DOS, overlay 1) — 3c.4

Builds on `reverse-engineering/cross-version/wand-and-transfer.md` section 2; `[C]` unless tagged:

- **Entry:** a start-up question, "Are you playing off the hard drive?" / "Would you like to transfer older characters
  saved from Phantasie I?" (DS:`0x2b12`, `0x2b36`), not a Utilities menu. Reads `guild.dat` from "a Phantasie I data
  disk" (282-B records, 22 slots). The II path (318-B records, chosen when `final2.pic` exists) only runs if the first
  open fails, and the code then reopens `Guild.dat` anyway, so it looks unreachable `[~]`.
  **Live s81:** the transfer question is asked only after "No" to the hard-drive question (ovl1 `0x994`: "Yes" sets
  `[0x2bb6]:0x1bb0` = 1 and skips it), so a hard-disk install cannot transfer. It then needs a saved game on the data
  disk ("There is not a saved game on this data disk to load. Save a game to this data disk and try again later.").
  Disks are told apart by file (root `0x7d22`): `town.cmp` present = game disk, `dtown.cmp` = data disk, and the swap
  prompts loop until the right one is in the current drive. The manual instead puts the transfer on the Other
  Utilities Menu (option 6, "the first action you should take after making your Player and Dungeon Disks", III PDF
  13 / pr.19–20) and says nothing about hard disks or a saved game: **D3-030**.
- **Adds, does not erase:** "Insert the data disk on to which these characters will be added." (DS:`0x2a2d`). Each
  accepted character goes through the normal insert routine (`0834:1d5b`); a counter `FAR:0xb5bc` starts at `20 − (roster
  count)` and stops the loop when it reaches 0 ("[original text]") (D3-003).
- **Per character** the player sees name, class, race, level, status, age and the six stats and answers "Will you keep
  this character?" (DS:`0x2970`).
- **Order and fields:** ovl1 first copies the six stats (STR, INT, DEX, CON, CHA, LCK), race (via `DS:0x4e`) and class
  (via `DS:0x48`) into the work record (img `0x140ee…0x1411f`), sets `FAR:0xb5e6` (transfer mode) and calls the shared
  creation routine ovl2 `0x227d` (section 5.1), which writes **level 1**, the class kit (items 4, 22, 41|44, 64) and first
  spell, fresh HP / magic / skills (seeded and trained once, root `0xa1f2` / `0x9ea0`), a **random social class and
  gold = that class · 256**. Back in ovl1 (img `0x14160…0x141f0`) it writes the name (7 chars), age clock (I `@264` →
  +0x28), status (I `@24`), XP = **100 × old level** (u32), **level 20 if Undead** (ovl1 `0x434`, D3-007), bank gold
  **+=** **old bank gold ÷ 35** (no +100), town 1 = Pendragon, social class **3 = Noble**, Phantasie I score (I `@70` →
  +0x5c), origin 0 = Gelnor (II path: Ferronrah if its II score ≥ 100), and inserts the record. So non-Undead
  transferred characters start at **level 1** (D3-016), and their gold is (random class) · 256 + old ÷ 35 while the
  class shown is Noble.
  **Verified live s81** (DOSBox-X, a Phantasie I `GUILD.DAT`): I level 3 → III level 1 with XP 300; level 1 → level 1
  with XP 100; UNDEAD (I race 15) level 2 → III Undead (17) level 20 with XP 200. All got social class 3 and the kit
  (shield 4, armour 22, bow 41 / 44 for the Thief, weapon 64) with stats unchanged. I race 13 (thiefo) became Lizard
  Man (9) and I race 12 Goblin (7), as `DS:0x4e` maps them. After the transfer the roster also held Air / Mist / Mud
  (Elemental, level 15) in slots 7–9; where those are created was not traced.
- **Not carried:** items (incl. the wand: D3-001), skills, HP/MP, spells (so **no divine spells**, D3-002), deeds.
  **Live s82:** DIVINE I in the I record → no Divine Aid in III; a Dead I character is offered and kept Dead; the new
  records are inserted before the elementals and the old roster stays (10 → 12). With no `GUILD.DAT` but a
  `FINAL2.PIC` the game says "Not a Phantasie I data disk!" (the II branch reopens `Guild.dat`, ovl1 `0x0`).

**Amiga III** imports from Phantasie I only (strings) `[C]`; not decoded `[?]`.

**Remaster:** `Phantasie3Campaign.AdmitTransferred` runs the creation routine's transfer-mode draws in DOS's order
(HP dice, the social-class roll for the bank gold, the age roll; no attributes) and then ovl1's writes; the values are
`rule-tables.json` `transfer`. The manual's rules are the defaults (level 0, D3-007 / D3-016; I / II source, D3-004;
divine spells as Divine Aid, D3-002; the wand, D3-001) with DOS's as `p3.transfer-*` options; DOS's add is kept (D3-003)
and the transfer is a game-menu action at any time (D3-030). XP is DOS's 100 × old level (D3-072, user ruling).

## 7. Manual questions answered (from `09-trilogy-manual-p3.md` §3)

| Q | Answer |
|---|---|
| 1 transfer platforms | DOS: from Phantasie I only (startup question); II path dead `[~]`. Amiga: from I only (strings) `[C]` |
| 2 transfer details | section 6: the six stats, class/race remapped, status, age, I score, social Noble, origin Gelnor, XP = 100·level, gold ÷ 35; no skills, items, spells; level 1 and the class kit, first spell, fresh skills from the creation routine (D3-016) |
| 3 divine-spell numbering | III's only divine spell is #57 Divine Aid; #55/#56 are Accuracy/Party Accuracy `[C]`; DOS transfer keeps no spells (D3-002) |
| 5 social classes | Six names: Peasant, Labourer, Craftsman, Noble + endgame titles Darklord / Superhero (D3-006) `[C]` strings |
| 6 body-area count | Six in the record and on the sheet `[C]` |
| 15 "Slow" | No spell of that name in III's table `[C]` (monster-only, if any `[?]`) |
| 16 bow #52 | the code says "Compound Bow +2" `[C]` (D3-005) |
| 17 towns / scrolls | 14 town names; 16 numbered scrolls in 20 item slots (III and VII ×3) `[C]` |
| — Fire Bow bonuses | Thief +30, Elf +15 `[C]`, as the manual |
| — Vision #53 | removed (empty name) `[C]` |
| 4 beginning spell | Priest and Ranger #1 Healing I, Monk and Wizard #5 Fireflash I, also for transferred characters (section 5.1) `[C]` |
| 7 broken-limb rules | "Broken" means Broken **or Gone** everywhere: any arm or leg ≥ Broken → no bow; right arm ≥ Broken → Parry only (spells and swings blocked). The left arm alone blocks only the bow. A Down character (a leg ≥ Broken) cannot fire either (`logic-specs/body-areas.md`) `[C]` |
| 8 HP per location | No: one HP pool; the injury level comes from the hit's damage against the HP left after it (integer HP-left ÷ damage: 0 Gone, 1 Broken, 2 Injured, ≥ 3 none) `[C]` |
| 9 hit-location odds | A 6 × 6 cumulative table `DS:0x12cc` (melee, bow, spell, Aim, "tall" rows) `[C]` (`logic-specs/body-areas.md`) |
| 18 inn and Breaks | Town inn: full HP and every Injured / Broken area healed (Gone stays). Wilderness inn: full HP and 2 cure points (1 Break or 2 Injuries). D3-015 `[C]` |
| 19 thief reach | Thieves pick rank 1 or 2 for Thrust and Slash (and Lunge, like Fighters), not for Attack. D3-011 / D3-012 `[C]` |

Answered in the logic specs: Q10 autosave (`town.md` section 4, D3-028), Q12 elementals (`elementals.md`), Q13 fees / XP /
training (`town.md` section 2, `training.md`). Still open: Q11 (the Amiga's gold cap), Q14 (`09-trilogy-manual-p3.md` section 3).

## 8. Save files (3c.7, DOS) `[C]` (s79, static)

All save I/O goes through two routines: `save_game` root `0x8176` / `load_game` root `0x7f8b` (Town ▸ Other options:
"Are you sure you want to save this game over your previously saved one?", string 390) and the general file routine
root `0x7bff` `file_io(char c, mode 1 read | 2 write, name n, far buf, len, retry, long offset)`, whose file name is
string 500 + n with its 4th character replaced by `c` when no offset is given. Files are opened in the current
directory (the data disk); the `\D\…` strings (500, 510) are leftovers of the Amiga paths and are only used with an
offset or not at all `[~]`. Files are written with `O_WRONLY|O_CREAT|O_TRUNC|O_BINARY`, little-endian.

**`gamesave.dat`** = 10,502 B, four blocks written in this order (load reads them back the same way):

| File off | Size | Source | Content |
|---|---|---|---|
| 0x0000 | 0x1478 (5240) | `FAR:0xe820` | **roster**: 20 × 262-B records (section 5) |
| 0x1478 | 0x7b0 (1968) | `FAR:0x236` | **party block**: 7 × 262-B party records (`0x236–0x95f`, slot 6 = the elemental), then found items 60 × u16 (`0x960`, cap 60: "The party can't carry any more items.", root `0xf421`; manual 90, D3-020), count u16 (`0x9d8`), party experience u32 (`0x9da`), party gold u32 (`0x9de`), days out u32 (`0x9e2`, counts thirds of a day; step costs by the terrain word `FAR:0xb61a`, `logic-specs/planes.md` section 3a) — the order of the Party Status screen (root `0x1b6d`) |
| 0x1c28 | 0x24 (36) | `FAR:0x97f0` | 18 globals copied there before the write (table below) |
| 0x1c4c | 0xcba (3258) | `0x27f3:0000` | the first 0xcba bytes of `INITARMS.DAT` as loaded at start-up (ovl1 `0x5e0` reads 0xcba B of `initarms.dat` there): per-town armory stock and other run-time state `[~]` |

Globals block (`FAR:0x97f0 + 2k`, source segment `0x1820` unless noted):

| k | Source | Meaning |
|---|---|---|
| 0, 1 | `0x09f8`, `0x487e` | copies of the party x / y (start 24, 37) `[~]` |
| 2, 3 | `0x97ec`, `0x9814` | x / y to return to (start 4, 4; plane arrivals rewrite them) `[~]` |
| 4, 5 | `0x4882`, `0x4884` | **party x / y** on the current wilderness map `[C]` (`logic-specs/planes.md`; `0x4880` next to them = elemental-in-combat flag `[C]`) |
| 6 | `0x27f3:0x0cc0` | `[?]` (1 at start) |
| 7 | `0x487c` | set to 1 when the party leaves a town (ovl3 `0x88`) `[~]` "in the wilderness" |
| 8 | `0xe596` | **in-town flag** (inn: town inn vs wilderness inn; set entering a town, ovl3 `0x6c8`) `[C]` |
| 9 | `0xb61e` | combat-active flag `[~]` |
| 10 | `0x97c0` | **roster count** (creation needs < 20) `[C]` |
| 11 | `0x27f3:0x0cc8` | **filled party slots** including the elemental (≤ 7) `[C]` (`logic-specs/elementals.md`) |
| 12 | `0xb614` | **party size** `[C]` |
| 13 | `0xb612` | **elemental in party** `[C]` |
| 14 | `0xdd98` | **current town / place** `[C]` |
| 15 | `0xde70` | rested-since-combat flag (cleared at combat start, set by an inn stay; magic max only grows when clear) `[~]` |
| 16 | `0xe810` | number of item kinds the current town's armory stocks (`INITARMS` row byte 0, root `0x0f22`) `[C]` |
| 17 | `0x09f2` | `[?]` (1 at start) |

The game save is **one slot per data disk** (load: "There is not a saved game on this data disk to load", string
553). It holds no map position file of its own: the world position is in the globals `[~]`.

**Dungeon saves** (ovl4 `0x62d`, "Would you like to save this map …" / "[original text]") `[C]`:
`file_io` with c = the dungeon's record letter `FAR:0xb5b0` (from `DS:0x1c84 + 2·dungeon`, 'A'…'K'):

| File | Name string | Size | Content |
|---|---|---|---|
| `DNP<c>` | 511 "DNPX" | 0x1080 (4224) | the current dungeon's map (one `DNGS.DAT` record as modified: doors opened, treasure taken) `[~]` |
| `DNG<c>` | 501 "DNGX" | 0x5a (90) | dungeon state from `FAR:0x0000` (position, facing, …) `[?]` fields |

A fresh dungeon is read from `dngs.dat` (string 503) at offset `record · 0x1080` with record = letter − 'A' (11 records = 46,464 B) `[C]`.
`plns.dat` (string 520) holds the three plane maps, read by offset (section 9.2).

**Amiga** `gamesave.dat` (11,812 B = 45 × 262 + 22 `[~]`) has a different layout (25 roster slots, …); not decoded.
Not run in DOSBox: the byte layout above is from the save/load code, not from a saved file.

## 9. Data files (3c.7, s81, static) — WORLD, PLNS, INITARMS, SCROLLS, DNGS

All five are shared with the Amiga version (`D/…`); multi-byte fields, where any, are big-endian like `CREATURE.DAT`.
Behaviour that uses them: `logic-specs/planes.md` (maps, places, scrolls), `logic-specs/town.md` (armory stock).

### 9.1 `WORLD.DAT` — the Scandor map `[C]`

3750 B = **50 × 75 tile bytes, row-major**: tile(x, y) = byte `y · 50 + x`, x 0–49 west → east, y 0–74 north → south
(ovl1 `0x5e0` reads it whole into a 0xea6-B block, `FAR:0xddae`; width `FAR:0x97c8` = 50, last row `0x27f3:0x0cce` =
74; ovl3 `0x375` indexes `map[y · width + x]`). The party starts at (24, 37), Pendragon. Tile id → terrain in
`logic-specs/planes.md` section 3. Checked against the hard-coded place list: the 14 town squares hold tiles 26–38, the 8
dungeon squares 39–46, the 8 inn squares 47–54. Row 0 is all tile 1 (Water); the outer frame is blocked ("Ocean") by
position, not by tile. 5 × 5 sections of 10 × 15 squares = the manual's map sections `[~]`. No code writes the file
back `[~]`.

### 9.2 `PLNS.DAT` — the three plane maps `[C]`

658 B, tile bytes, row-major, read by offset with `file_io` (root `0x125d`):

| Offset | Size | Plane | Map |
|---|---|---|---|
| 0x000 | 81 | 1 Light | 9 × 9 |
| 0x051 | 81 | 2 Dark | 9 × 9 |
| 0x0a2 | 496 | 4 Netherworld | 31 × 16 |

Every map is framed by tile 249 (River Styx, blocked). Light uses tiles 68–74 (Bright Mist), Dark 61–67 (Dense Fog),
the Netherworld 61–88, 154–213 and 242–249 (Black Path, Mist, Steam, Lava, Styx). Place tiles: Light (4, 1) 56 = town
15, (6, 7) 59 = Castle of Light; Dark (4, 1) 58 = town 16, (2, 7) 55 = Castle of Dark; Netherworld (7, 3) 57 = town 17
"Death", (6, 10) 60 = dungeon 9. (Amiga: the same data split into `D/pln1–4` `[~]`.)

### 9.3 `INITARMS.DAT` — armory stock per town `[C]`

3328 B. The first 0xcba = 3258 B = **18 rows × 181 B** are loaded at `0x27f3:0000` (ovl1 `0x5e0`) and saved inside
`gamesave.dat` (section 8), so they are the live stock; the last 70 B are padding (`0x3a`). Row = town number (0 unused,
1–14 the Scandor towns, 15–17 the plane towns, all empty):

| Byte | Meaning |
|---|---|
| 0 | number of item kinds stocked (copied to `FAR:0xe810`) |
| 1–180 | stock count of item id 1–180 (0 = not sold; 200 / 99 / 77 on potions and scrolls, i.e. plenty) |

The armory (root `0x0f22`) lists every id with a non-zero count and prices it `value[id] · price_index[town] / 100`
(section 4.1). Pendragon (row 1): Clothing 21 / Robes 22 ×5, Leather 23, Knife 61, Club 63, Mace 64, Healing / Magic Potion 5
(105 / 115) ×5, Scroll IV (126, the map) and Scroll VIII (132) ×200. **No restocking** `[C]`: only buying (−1) and
selling (+1, in the town of the sale) change a count; nothing regenerates it (`logic-specs/town.md` section 7) — **D3-033**.

### 9.4 `SCROLLS.DAT` — scroll texts `[C]`

19950 B = **19 records × 1050 B** of NUL-terminated text (root `0xeb` reads record `k` at `k · 0x41a`); lines are
cut by the window width (no CR). Item → record map in `logic-specs/planes.md` section 7 (Scroll IV is a picture, not text;
three-page scrolls use consecutive records). Record titles include "Departed Souls" (4), "[original text]" (5),
"The Universe" (7–9: "[original text] many planes…"), "[original text]" (13), "The Plane
of Light" (15), "[original text]" (16), "[original text]" (17), "[original text]" (18).

### 9.5 `DNGS.DAT` — dungeon records (`[C]` layout, field meanings partly `[~]` / `[?]`)

46464 B = **11 records × 4224 (0x1080) B**, one per dungeon (names = master strings 34–44). **Record ≠ dungeon − 1**:
the entry code takes a letter from `DS:0x1c84 + 2·dungeon` and loads record `letter − 'A'`: dungeons
1–11 → records 9, 7, 4, 3, 0, 2, 1, 10, 8, 5, 6 (J H E D A C B K I F G) `[C]` (ovl4 `0x6c7`, `0x77d`); the Amiga
`D/DNPA…DNPK` are played copies of the DOS record of the same letter. Records 9 and 10 (dungeons 1 straw hut and 8
sulphur cave) are byte-identical. The record is loaded into the work buffer (`FAR:0xb63c`) on entry and saved as
`DNP<letter>` (section 8). Events, quest bits and exits: `logic-specs/dungeon-events.md`.

| Offset | Size | Content |
|---|---|---|
| 0x000 | 720 | **map, 24 × 30 cells**, cell(x, y) = byte `24 · y + x` (ovl4 `0x2248` / `0x2275`; x 0–23, y 0–29; ovl4 `0xe14` rejects moves outside) `[C]` |
| 0x2d0 | 1 | dungeon level L (0–5), picks the random / lair monster band `[C]` |
| 0x2d1 | 10 | monster id for each forced-fight cell 0x80–0x89 (`record[0x251 + cell]`) `[C]` |
| 0x2db | 3 × 3 | exits, one per exit cell 0xd7–0xd9: (destination, x, y). Only the destination is read (ovl4 `0x977`): 29 / 31 / 41 = go to the Light / Dark / Netherworld plane at its fixed start square; any other value (3–23 in the data) = stay on the plane, back on the dungeon's square. x, y are never read `[C]` (`logic-specs/dungeon-events.md` section 7) |
| 0x2e4 | 5 × 35 | **event squares** 0xdd–0xff (`0x2e4 + 5 · (cell − 0xdd)`, ovl4 `0x12a4`, entered after the copy-protection quiz ovl4 `0x43e` passes): byte 0 = message number (+1) with bit 7 = once only (the entry becomes 0xff), byte 1 = number of choices offered (keys 1…n, n up to 9), program index for choice k = byte `2 + k − 1` (byte 2 when there is no choice; for n > 3 this runs on into the next entries; ovl4 `0x1351…0x1369`) `[C]` |
| 0x393 | 512 | **event programs**: ops at `0x393 + 2 · index` (2 or 4 bytes); op byte bit 7 = continue with the next op, bit 6 = clear the op after use, bits 0–5 = type 1–27 (ovl4 `0x14b0`, jump table `0x1aea`): all 27 types decoded in `logic-specs/dungeon-events.md` section 3 `[C]` |
| 0x593 | 2 each | text pointers, big-endian; text at `pointer − 0x800` within the record (e.g. 0xe71 → 0x671 "[original text]") `[C]` |
| … – 0x107f | | texts and other tables `[?]` |

Cell codes (map bytes) as the code treats them, `[C]` unless tagged:

| Cell | Meaning |
|---|---|
| 0 | **wall** `[C]`: the step code ovl4 `0xf4f` returns 2 for cell 0 while `FAR:0xdd96` is clear, and the mover (ovl4 `0xad5`) does not move on any result below 5; `FAR:0xdd96` is only ever written 0 (ovl1 `0x5f1`, ovl4 `0x63d`), so cell 0 always blocks (`logic-specs/dungeon-events.md` section 7a) |
| 1 | walkable, drawn as a wall (shape 0x2e): a secret door `[~]` |
| 7 | secret door `[~]` |
| 8–17 | random-encounter squares: a fight when `FAR:0xdd8c` > `rand() % 100`, monster `12 · L + (cell − 8) + rand() % 3` (ovl4 `0xeb5`) |
| 0x80–0x89 | forced fight with the monster in the table at 0x2d1 (ovl4 `0xe76`) |
| 0xb9 / 0xc8 | doors (shape 0x2f / 0x30); 0xba–0xc6 and 0xc9–0xd5 = doors with monsters behind ("[original text]"), monster `cell − 186` resp. `cell − 201`, plus `12 · L` (ovl4 `0x1056`) |
| 0xc7 / 0xd6 | special doors (event 0x2a) `[?]` |
| 0xd7–0xd9 | exits ("[original text]"), table at 0x2db |
| 0x8a | the square of a won event fight (type 5) `[~]` |
| ≥ 0xdd | event squares (treasure, traps, messages, fights), table at 0x2e4 |

### 9.6 Extractors and GameData (s82)

`tools/scripts/extract_dos_p3_{world,towns,scrolls,dungeons,rules}.py` write `extracted/world.json`, `towns.json`,
`scrolls.json`, `dungeons.json` and `rule-tables.json`; `tools/scripts/build_p3_gamedata.py` builds
`src/OpenPhantasie.Data/GameData/phantasie3/` from them (`--check` compares). Findings on the way, `[C]` unless tagged:

- **Places** are read from ovl3 `place_at_party` by its `cmp [0x4882], x` / `cmp [0x4884], y` / `mov [0x0cbc], type` /
  `mov [0xdd84], n` operands: 30 Scandor squares + the Netherworld's two, equal to `planes.md` section 4; every square sits on
  a tile of its kind. The four Light / Dark squares use another code shape (kept from section 4 by hand).
- **Scroll lines:** a 38-character cut is the only width from 30 to 44 that never splits a word in the 19 records `[~]`.
- **Dungeon texts:** the pointer table at 0x593 runs to the lowest text (0x66f in every record: 110 slots), zero
  entries are empty slots; 348 texts in all. An event square's message byte names slot `(E[0] & 0x7f) − 1` (`logic-specs/dungeon-events.md`).
- **INITARMS** byte 0 equals the number of non-zero stock bytes in all 18 rows.
- **Skill class tables vs Phantasie I** (`TWNS.INT @0x21c`): equal except five cells: Thief Pick Lock L3 35 (I 55),
  Ranger Attack L15 25 (I 250), Ranger Parry L15 60 (I 85), and Monk / Wizard Parry one level ahead of I's rows (the
  Parry table is one byte short after Ranger L14, so Monk L15 = 40, Wizard L15 = 0). DOS III data errors, not manual
  questions (the manual prints no skill tables); kept as read. HP dice and the magic ceiling equal I's tables.

## 10. CGA pictures (s160, static) — `WORLD.CGA`, `SHAPES.CGA`, `MONSTERS.CGA`, `TOWN.CMP`, `PICS.CMP`

Every picture file is CGA 320×200 four-colour data: 2 bits a pixel, 4 pixels a byte, high bits first, rows stored one
after another (not the screen's two banks); the blitter (root `0x3b2:0x420`, called through `0x284:0x774(buffer, x
words, y, width words, rows, 0)`) copies `2 · width` bytes a row into the interleaved screen, opaque `[C]`. Colours:
two CGA palettes, switched per screen (s169, below): **palette 0** (black, #55FF55, #FF5555, #FFFF55) and **palette 1**
(black, #55FFFF, #FF55FF, #FFFFFF), high intensity on every DOSBox-X shot (s81–s156); nothing sets the intensity or
background (`0x284:0xa70` has no caller), so it is the mode set's default `[~]` (the colour values are DOSBox's).

**Palette per screen** (s169, static `[C]`): `0x284:0xaaf(n)` (img `0x32ef`) is `int86(0x10)` with AH 0Bh, BH 1, BL n:
the CGA palette select. Its callers, with the screen each starts:

| Caller | Palette | Screen |
|---|---|---|
| ovl1 `0x619` (start-up) | 0 | title, start-up questions |
| root `0x68` (`load_town_graphics`) | 0 | the town street |
| ovl3 `0xb1` (`leave_town`); root `0x7c2` (the map redraw `0x79e`) | 0 on Scandor, 1 on a plane | overworld: `FAR:0xcc4` (the plane) = 0 → 0, else 1 |
| ovl4 `0x7dd` (`dungeon_enter_save`), `0xc5d` (`dungeon_step`), `0x147a` (after an event page) | 1 | the dungeon map (and its windows) |
| root `0x1081e` (`0x1080a`, the combat field: clear, palette, field `0x103bf`, the groups `0x11525`) | 1 | the fight's field, its pictures and lines |
| root `0xf9dd` (`combat_select_member`) | 0 | the orders screen ("Selecting combat options") |
| root `0x1eba` / `0x240c` (`party_death_astral_plane`) | 1, then 0 | the Astral Plane: its three intro lines, the picture and one line per member all in 1; after the last member's line and its wait `0x452(200)` the screen is cleared (`0x3b2:0x50c`, 16 KB of 0 at `B800:0`) and only then palette 0 (s170 `[C]`), so nothing shows in 0 before the next screen |
| ovl4 `0x3e` (`endgame_gods`) | 1 | the gods' intro, its flashes, Zeus / Pluto |
| root `0x4b7` (the text page `0x4a3`, below) | 0 | scroll pages, Scroll IV, dungeon event pages, the god's speech |

The s82 shots agree: the dungeon map, the planes' arrival and the fight's lines are cyan / magenta / white, the altar
page, the town, Scandor and the orders screen green / red / yellow. Every picture file is cut in palette 0's colours;
the remaster's DOS CGA pack shows a palette-1 screen by recolouring index for index (pack `scene_palettes`).

| File | Bytes | Layout | Loaded by |
|---|---|---|---|
| `TOWN.CMP` (`DTOWN.CMP` on a data disk) | 10,736 | one packed screen: the town street (every town) | root `0x0000` → `0x284:0` → blit (0, 0, 40 words, 200) `[C]` |
| `PICS.CMP` | 30,170 | four packed screens back to back: 0 at 0 (9,180 B) the title, 1 at `0x23dc` (5,574 B) the Astral Plane, 2 at `0x39a2` (7,640 B) Zeus, 3 at `0x577a` (7,776 B) Pluto | title ovl1 `0x7f7`; Astral Plane root `0x1edd` (party defeat); gods ovl4 `0xa8` (argument ≠ 0 → 2, else 3; which god is which by the pictures `[~]`) `[C]` offsets |
| `WORLD.CGA` | 16,000 | 250 overworld tiles of 16×16 (64 B each, 4 B a row); tile = map byte, the Amiga `phantasy.set` order | root `0x00bf` (16,000 B at FAR `0x9fa`) `[C]` |
| `SHAPES.CGA` | 16,192 | six blocks, below | ovl1 `0x884…0x929`, root `0x0124` `[C]` |
| `MONSTERS.CGA` | 48,768 | 41 normal pictures × 768 B (two 32×48 frames, 384 B each: idle, attacking), then 6 giants × 2,880 B (two 80×72 frames, 1,440 B each) at `0x7b00` | root `0x7e89` per group (FAR `0x9eee` / `0xaa32`) `[C]` |

**Packed screens** (`0x284:0`, root img `0x2840`): byte 0 picks the unpacker, 1 = row order (`0x3b2:0xee`), 2 = column
order (`0x3b2:0x161`); all five DOS III screens use 2 `[C]`. The output is 16,000 bytes (80 a row, 200 rows); order 2
fills it a byte column at a time (offset += 80 for 200 rows, then back to the top of the next column). Then control
bytes: `c` in 1..127 = `c` literal bytes follow; `c` ≥ 128 = the next byte `256 − c` times; `c` = 0 = a little-endian
word count, then the byte repeated that many times; it stops after 16,000 bytes, so the files hold no length or end
mark (the four `PICS.CMP` screens end exactly where the next starts) `[C]`.

**`SHAPES.CGA` blocks** (offset, size → FAR buffer):

| Offset | Bytes | What | Use |
|---|---|---|---|
| `0x0000` | 432 | three 24×24 frames (144 B) at FAR `0xb642`: the **flash** (the Amiga's shapes 37–39) | root `0x1537`, below `[C]` |
| `0x01b0` | 240 | fifteen 8×8 glyphs (16 B) at FAR `0xe6ca`: the **text page's frame** (the Amiga's `scroll.csh` 54–68) | root `0x0636(n, x, y)` (glyph `n` at 8-px column `x`, pixel row `y`, opaque) from the page routine `0x4a3`, below `[C]` |
| `0x02a0` | 9,600 | 25 party pictures 32×48 (384 B): 12 race pairs (idle, attacking) + the grave (24) | same cells as Amiga `heros.cmp`; race → picture by the byte table DS:`0x9ea` (img `0x295ea`) = Amiga `0x239a0`: Human 0, Dwarf 2, Elf 4, Gnome / Halfling 6, (Random 0), Gnoll 14, Goblin / Kobold / Orc 16, Lizard Man 10, Minotaur 12, Ogre 14, Pixie / Sprite 8, Troll 20, Elemental 22, Undead 18, entry 18 = 24 (the grave) `[C]` data |
| `0x2820` | 112 | dungeon map glyphs 8×5 (2 B × 5 rows, 16 B a slot) | ovl4 `0x5a8(x, y, g)`: glyph `g` 46..52 → slot 1, 2, 3, 6, 5, 4, 0 (jump table `0x5f7`) `[C]` |
| `0x2890` | 5,040 | Scroll IV's map "[original text]", 120×168 | root `0x0124` (`read_scroll`, item 126): on the text page (below), blit at (96, 8) (12 words, 8, 15 words, 168) `[C]` |
| `0x3c40` | 768 | the body-area figure: 4 states × 12 pieces × 16 B (8×8) | root `0x0dcf` / `0x0eb5`, below `[C]` |

**Dungeon glyphs** (ovl4 `0x21bb`, the cell → glyph map): map byte 0 / 1 → 46 (also the not-seen fill, ovl4 `0x7ff`),
185–199 → 47, 200–214 → 48, 215–217 → 52, ≥ 221 → 49, else 50; a glyph outside 45..52 draws 51 (ovl4 `0x1fe3`) `[C]`:
the same numbers as the Amiga's `dng.csh` glyphs (pack `tile_maps.dungeon_cells`). Cells sit 8 px apart, 5 rows.

**Body figure** (root `0x0dcf`, called by `0x0d00` with the record's six area words `+0xf2…+0xfc`): piece `p` of state
`s` is at `s · 192 + p · 16` and is drawn at text cell (x + p div 4, y + p mod 4), so the 12 pieces are a 3 × 4 grid
(24×32) stored column by column. State word 0 Okay → set 0 (green), 1 Injured → 1 (yellow), 2 Broken → 2 (red),
3 Gone → 3 (all black). Pieces drawn: 4 head (area 0); 1 the viewer's left arm = area 2 Right Arm; 5 the chest (always
set 0); 9 area 1 Left Arm; 2 and 3 area 5 Right Leg; 6 area 3 Torso; 10 and 11 area 4 Left Leg. Pieces 0, 7, 8 are not
drawn `[C]`.

**The text page** (root `0x4a3(text, first, window)`, s169 `[C]`): called by `read_scroll` (root `0x16a` Scroll IV with
(null, 1, 1); `0x32f` every text scroll page with (text, page = first, 1)), `dungeon_show_text` (ovl4 `0x1c91`, the
event page, (null, 1, 0)) and `endgame_gods` (ovl4 `0x1a5`, the speech, (its first line, 1, 0)). With `first`: palette
0 (`0xaaf(0)`); then with `window` the whole-screen window DS:`0x5c8` = cells (0, 0)–(39, 24), frame and fill colour 3
(root `0x6f6:0x322`: fill `0x284:0x92a` and frame `0x8ae`, inclusive), else the fill (8, 8)–(311, 191) in colour 3
(`0x92a` clamps x2 / y2 to 319 / 199 and keeps x2: inclusive, so 304 × 184); then the frame (`first` only): corners
glyph 0 at column 0 row 0, 1 at (39, 0), 3 at (0, 192), 2 at (39, 192); top (y 0) columns 1–38 glyph 4 + (rnd and 2) =
4 or 6; right (column 39) y 8, 16 … 184 glyph 12 + (rnd and 2) = 12 or 14; bottom (y 192) columns 1–38 glyph 7 + (rnd
and 1) = 7 or 8; left (column 0) y 8 … 184 glyph 9 + (rnd and 2) = 9 or 11 (in that order; `rnd` = `0x284:0x3e4`, so
glyphs 5, 10 and 13 are never drawn). The text, when given, at cell (1, 1) in colour 0 on 3 (`0x284:0x7a1(text, column,
row, ink, paper)`). Later scroll pages fill (8, 8)–(311, 191) in 3 and call it with `first` 0 (no frame redrawn). The
event page puts "Press a key" / "Press a number" (FAR `0x1b20` + 4·n) at cell (14, 23), colour 0 on 3, and its lines from
cell (1, 1) down, one row each (FAR `0xac` counts them). Scroll IV: the page, the map at (96, 8), "Geography of
Scandor" (DS:`0x2699`) at cell (10, 23) in colour 2 on 3 (root `0x1b4`), a key (`0x284:0x35e`). The altar page (`screens-s82/s82_altar_menu.png`)
shows it: yellow inside, the red / black frame, black text.

**The flash** (root `0x1537(x, y, giant)`, s169 `[C]`): x (in 8-px bytes) += rnd mod 3, + 2 when `giant`, at most 36; y
+= 6 + rnd mod 22; the 3 × 24 rectangle under it saved (`0x284:0x1f6`, a stack of up to 10 at FAR `0xaa2e`), then frames
0, 1, 2 each drawn over the last by `0x284:0x19c` (every word through `0x3b2:0x4b7`, screen = screen OR data: colour 0
leaves the screen as it is, other colours OR into what lies under), sound `0x284:0xb75(0x66)` (the PC speaker) before
frame 1, and after each frame the wait `0x452(FAR 0xb5e8)`; then the rectangle restored. Callers: `damage_monster`
(root `0xb14c`, when its flag argument is set: x / y / giant from the group's field slot `+0x3c + 2·i`, `+0x46`,
`+0x48`), `damage_character` (`0xbca4`, when its flag is set: (6·slot, 140, 1)), and the gods' intro (ovl4 `0x396`:
50 flashes at (rnd mod 36, rnd mod 120, 0); with argument 1 the wait word goes 1 down every 5 flashes from flash 0,
not below 1, with 0 one up, not above 8; it starts at 7 (ovl4 `0x4a`) and is 6 after the scene, as in a fight). The
same numbers as the Amiga's `0x1b814` except the member's x (DOS 6·slot, the Amiga max(0, 6·slot − 1)) and the blit
(the Amiga masks: colour 0 shows what lies under, the others cover it). In CGA's palettes every channel is 0x00, 0x55 or
0xFF, so the OR of two colours is their per-channel maximum (the pack's `blend: "or"`, drawn as a max blend; s170, seen
in the remaster on Xvfb, not in DOSBox: a cyan sparkle over the magenta monster turns white where they meet).

**The wait** (root `0x452(w)`, s169 `[C]`): `0x284:0x30b(max(1, ⌊100·w / 60⌋))`, a busy loop of that many units,
`FAR:0xdd94` loops each; the start-up (`0x284:0x2ae0…0x2b41`) counts loops over 4 BIOS ticks (`0x40:0x6c`, 219.7 ms)
and stores count · 455 / 10,000, i.e. loops per 10 ms. So `w` = w / 60 s rounded down to 10 ms (w 6 → 100 ms, 7 → 110 ms),
where the Amiga waits w + 1 VBLs.

**The town name** (root `0xa9e2`, s169 `[C]`): name = string 111 + town (FAR `0xdd98`); twelve spaces (DS:`0x5a58`) at
cell (14, 2) in colour 3 on 3, then the name at cell 14 + ((12 − length) >> 1), row 2 (y 16), colour 0 on 3: centred in
the 12 cells, an odd gap half a cell left (the Amiga's rule, `0x7e54`). "Pendragon" starts at x 120.

**Overworld view** (ovl3 `0x12f1`): 9 × 9 tiles at x = 16·column, y = 16 + 16·row; the party's cell draws **tile 89
instead of** the map byte (opaque, not over it) `[C]`. The tile draw (ovl3 `0x0000`) swaps six tiles while the plane
(FAR `0xcc4`) is 0, Scandor: 154 → 144, 166 → 165, 170 → 181, 187 → 186, 243 → 237, 249 → 1 `[C]` (25 Scandor cells
hold one of them). **The Amiga does not swap** (s169 `[C]`): its view `0x9b92` passes the map byte straight to the tile
draw `0x3834`, which only clamps ids above 250; instead its `phantasy.set` map already holds other bytes at most of these
cells (54 of the 3,750 Scandor cells differ from `WORLD.DAT`; at the 25: 187 → 186 seven times and 166 → 165, 243 →
237, 249 → 1 once each, as DOS draws them, but 154 → 143 five times and 139 once (DOS draws 144), 187 → 181 twice,
170 → 181 once and 170 kept six times (DOS draws 181)). So a few Scandor cells look different on the two machines;
each pack draws its own machine's.

**View size and edges** (s169, s170 `[C]`): the view (ovl3 `0x12f1`) reads map byte (cy + r − 4) · columns + cx + c
− 4 unchecked from the map pointer FAR `0x488a` (FAR `0x487e` / `0x9f8` the centre cell cy / cx, `0x9814` / `0x97ec` the
party's view row vr / column vc, `0x4884` / `0x4882` its map cell), with columns FAR `0x97c8` and rows FAR `0xcce` as
`load_plane_map` (root `0x125d`) sets them: Scandor **50 × 74** (also the start-up, ovl1 `0x873`; the map pointer FAR
`0xddae`, `WORLD.DAT`'s 3,750 bytes), the Netherworld **31 × 15** (496 bytes read, 31 × 16, then 0xF9 written at bytes
496 and 497 as on the Amiga), the Light and Dark planes 9 × 8 (81 bytes, a 9 × 9 map: the view never scrolls). The four
steps (each only while the centre is on the map, 0 ≤ c ≤ size − 1; the party's map cell moves with the party):

| Step | Party moves in the view (no scroll) | Else | Clamps after |
|---|---|---|---|
| north ovl3 `0x13ac` | cy ≤ 4: vr − 1; or rows − 4 ≤ cy ≤ rows − 1 and vr > 4: vr − 1 | cy − 1 | cy, vr, y ≥ 0 |
| south `0x1470` | cy ≤ 4 and vr < 4: vr + 1; or rows − 4 ≤ cy ≤ rows − 1: vr + 1 | cy + 1 | cy, y ≤ rows; vr ≤ 8 |
| west `0x154e` | cx ≤ 4: vc − 1; or **columns − 5** ≤ cx ≤ columns − 1 and vc > 4: vc − 1 | cx − 1 | cx, vc, x ≥ 0 |
| east `0x1612` | cx ≤ 4 and vc < 4: vc + 1; or **columns − 5** ≤ cx ≤ columns − 1: vc + 1 | cx + 1 | cx, x ≤ columns − 1; vc ≤ 8 |

So the centre stops at rows − 4 but at **columns − 5** (the Amiga: counter − 4 on both, amiga FORMAT.md section 4):
DOS's Scandor view ends at **columns 41–49** and rows 66–74 (the Amiga's at 42–50, one column past the map), the
Netherworld's at columns 22–30 and rows 7–15. The east step runs with columns − 1 while the plane (FAR `0xcc4`) and 3 is
not 0 (the Light and Dark planes; `columns` is put back after), so there the party stops at column 7 (column 8 is
tile 249 on both planes). Every writer of the view words places a consistent view: the start-up (ovl1 `0x780`: Pendragon
(24, 37), centre on the party, vr = vc = 4), the Astral Plane's town (root `0x1d5b`: centre = the town, vr = vc = 4;
all 14 towns lie in 5..44 × 5..69), the planes (root `0x1300`: Light / Dark centre (4, 4), party (4, 1); `0x13bc`: the
Netherworld centre (7, 4), party (7, 3)) and a loaded game (root `0x8052…`, the saved words); nothing else writes the
party's map cell FAR `0x4882` / `0x4884` (scanned in root and the four overlays). So **DOS never reads a byte
past a map** (s170 `[C]`): Scandor's last view cell is byte 74 · 50 + 49 = 3,749, the Netherworld's 15 · 31 + 30 = 495.
The remaster draws the view from the party's cell with the pack's `view_counters` and `view_centre_margin` [5, 4]
(the Amiga pack: none, counter − 4), equal to these steps.

**Monster pictures** (root `0x10b8b` → `0x7e89`): picture index = record byte 27, minus 1 when above 27 (so 27 and 28,
Troll Baby and Forest Elf, both show index 27); index < 41 → normal picture at `768 · index`; 41..46 → giant at
`0x7b00 + 2880 · (index − 41)`; any other index → **normal picture 15** (the Skeleton's): Mud Elemental (48) and Wolf
(65) show the Skeleton on DOS, where the Amiga cuts nothing (D3-067) `[C]`. Byte 27 > 41 still makes the group a giant
group (section 4.4), so these two stand at the giant places with a normal picture. The remaster's DOS CGA pack does not draw
these three choices (D3-067, user s161): Wolf takes the Wild Dog's picture, Mud Elemental the party elemental's and
Troll Baby the party Troll's (SHAPES.CGA party pictures 22 / 20), as the Amiga pack does.

Extractor: `tools/scripts/extract_dos_p3_graphics.py` → `extracted/graphics/` (the importer's `P3Dos/P3CgaArt.cs`
writes the same; pack `GameData/phantasie3/graphics/dos-cga.json`).
