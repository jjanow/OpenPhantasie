# Phantasie III (Apple II, SSI 1987): disks, tables and a standalone import

s212. Where each of III's game tables lives on the Apple II disks, how `tools/scripts/extract_a2_p3_tables.py` (and its
C# port `P3Apple2Tables`, `src/OpenPhantasie.Importer/Pipelines/P3Apple2/`) reads it into the shapes of DOS III's
extract, and every cell where the Apple differs from DOS III. Program notes (Bank, Divide, Beg for Mercy, restock,
Parry, Drop / Purge): `README.md` next to this file. Tags: `[C]` read in the code or data, `[~]` probable or a layout
choice of ours, `[?]` not known. BASIC line numbers are cited as `PROGRAM line`.

Tools (`python3 -I`, from the repo root):

| Script | Does |
|---|---|
| `tools/scripts/a2_woz.py <in.woz> <out.dsk>` | 16-sector `.woz` → DOS-order `.dsk` (the Python twin of `WozImage` for 16-sector disks; library `load_image`) |
| `tools/scripts/a2_rdos.py <side.dsk> [--extract DIR]` | RDOS 3.3 catalog and files |
| `tools/scripts/detok_apple2_p3.py <side.dsk> [PROGRAM [first last] \| --all]` | program listings |
| `tools/scripts/extract_a2_p3_tables.py [SIDE_A SIDE_B] [--out DIR]` | the tables below → `extracted/*.json` (default: the v3.0 `.woz` pair) |
| `tools/scripts/diff_a2_p3_vs_dos.py` | the extract against DOS III's `reverse-engineering/dos/phantasie3/extracted/`; `--built DIR [--cells OUT]` the importer's raw build against the GameData, and the shipped cells |

## 1. Versions and which one is the reference

| Images (`../original-images/phantasie3/`) | Build | State |
|---|---|---|
| `woz-original-v3.0/…-disk1-side-a-boot.woz`, `…-side-b-utilities.woz` | **v3.0 5/26/87** (`INTRO3`) | uncracked, **unplayed**: every save file is the factory one |
| `Phantasie_III_1987_SSI_cr_Disk_1_of_3_Side_A.dsk`, `…_Side_B.dsk`, `…_Disk_2_of_3_Side_A_Dungeon.dsk`, `…_Disk_3_of_3_Side_A_Player.dsk` | v1.0 | cracked, **played** (`CHAR3`, `TWN31`, `TWN317`), five scroll files damaged |

The shipped game is **two sides of one disk** `[C]`: side A (boot) holds the towns, the wilderness, the monsters, the
scrolls; side B (utilities) the dungeons (`DUNGON3`, `DNG1`–`DNG10`, `DNGLD3` / `DNGOUT3`, `BACKUP`, `WIN`). The [cr]
set's "Disk 2 Dungeon" and "Disk 3 Player" are not further game disks: `DISK MAKER` (side B, menu `PHANTASIE III`
line 200) copies side A to a Player disk and side B to a Dungeon disk, deleting `PHANTASIE III` and storing `TEST`
(`DISK MAKER2` 157–159). So **v3.0 covers the whole game**; the dungeon data comes from its side B.

**Reference: v3.0 for every file.** Data files are byte-identical in both builds except where noted:

| File | v3.0 vs v1.0 [cr] |
|---|---|
| `ITEM`, `TINFO`, `INFO`, `TF3`, `MN30`–`MN39`, `OT*`, `SC1`–`SC6`, `SC8`, `SC10`, `SC12`–`SC16`, `SC18`, `SC19`, `DNG1`–`DNG10`, `NIKADEMUS` | equal |
| `TOWN3`, `OUTDOORS3`, `STARTER`, `DUNGON3`, `BACKUP`, `DNGLD3`, `DNGOUT3`, `INTRO3`, `DEMO` | programs revised (other sizes: `TOWN3` 19,318 / 19,287 B, `OUTDOORS3` 21,026 / 20,918, `DUNGON3` 23,117 / 23,055, `STARTER` same size, other bytes). The extractor parses whichever side it is given; it reads only `TOWN3` 130 / 260 / 270 / 290 / 295 and `STARTER` 120 / 430 / 450, which read the same in both, and no DATA of `OUTDOORS3` / `DUNGON3` (`P3Apple2TablesTests.CrackedV10_SameTables_ButFiveScrolls`). The string sources point into v3.0's programs only |
| `WIN`, `WIN PIC`, `ICE DRAGON` | [cr]: a 233-byte crack stub and the cracker's own screens; the real ones only on v3.0 (art; not read here) |
| `SC7`, `SC9`, `SC11`, `SC17`, `SC20` | **[cr] damaged**: each holds another scroll's page (`SC7` = `SC9` = Departed Souls, `SC11` = News about the Great War, `SC17` = SC11's v3.0 page, `SC20` = Kilmor's account); load address $1388 in the [cr] catalog |
| `CHAR3`, `TWN31`, `TWN317` | [cr] played |
| `TWN317` | **resolved** (was `[?]`): v3.0's is the Town of Death template (= `TF3` record 17, load $1E46 = $0EA6 + 16 · 250, its slot). The [cr] file is **Asmith's record** (`TF3` record 8) with four stock bytes changed (items 12, 16, 17, 18 = 5, 5, 5, 1), load $1388 = `TWN36`'s slot: a played / mis-stored file of the crack, not game data. The import never reads `TWN3n` (it reads the `TF3` template) |

`TF3` record t is file `TWN3t` (both load at $0EA6 + 250 (t − 1)); `BACKUP` 500–550 (new game) writes `TF3`'s 17
records to `TWN31`–`TWN317` and clears `CHAR3` `[C]`.

**Files play rewrites** (every `& STORE` / `& DEL` in every program, both sides) `[C]`: `CHAR3` and `TWN3<n>`
(`TOWN3` 3720–3730 S)ave; `BACKUP` 440–450), `DNGSAV`, `DNGCHK`, `PIC` (`DNGOUT3` 50–90), `BACKFILE3` (backup disk
only), `TEST` / `SYSTEMBOOT` (`DISK MAKER*`, on the copies). Profile `p3-apple2` lists the first five (with v3.0's
factory sha1s) as `mutable_files`; none is read by the import, so a played copy imports the same (the report says so).
`OT<n>` (the wilderness) is never written `[C]`.

## 2. Containers and programs

RDOS 3.3, 16 sectors (`a2_rdos.py`; `../phantasie2/FORMAT.md` section 1). The v3.0 `.woz` decode with no missing
sector (`WozImage`, `a2_woz.py`). Memory map (as II): party / Guild records at `SA` = 16385, 170 B each (`FN P(X) =
PEEK(SA + 170 · PM + X)`); global state `SA` + 1189..1269; `LA` = 2048 (towns load at `LA` + 1, sections and levels at
`LA`); `XA` = 1990, `TINFO` at `XA` + 310 = $08FC; `INFO` at $302.

**The hi-res window font** `[C]` (`IMPTEXT`, 59 glyphs of 8 bytes; glyph k draws character 37 + k): the wilderness /
dungeon windows print `=` as a blank, `:` as `-`, `@` as `:`, `<` as `,`, `?` as `)`, `>` as `?`, `;` as `"`. Window
widths `[C]`: `GOSUB 550` 8 columns, `800` 15, `650` 22 (`POKE 33,w`). Text laid out for a window (padded with `=`,
words hyphenated with `:`) is read as one line: the font decoded, each window row stripped, a row ending in `-` joined
to the next `[~]` (`window_text`). The town (`TOWN3`) and the event texts print on the 40-column text screen in the
normal font.

## 3. Tables (→ the shapes of `reverse-engineering/dos/phantasie3/extracted/`)

### Monsters: `MN30`–`MN39` `[C]`

Text files, 8 records of 22 CR-ended fields (side A = side B); monster m = record m mod 8 of `MN3<m div 8>`
(`OUTDOORS3` / `DUNGON3` 900–915: `& READ "MN3"+MF`, then `INPUT`). Field order (line 910) → DOS name:
`N$` name · `MA%` armor · `DM%` damage · `TM` level · `EX` xp · `SL%` special_chance · `SP%(0..2)` specials · `DM`
hp_base · `PW%` combat_rating · `WG`, `ML` companions · `PM` reaction_modifier · `PL` next_group_chance · `D%` defense ·
`X` gold · `Y` group_max (group size `INT(RND · Y)`, line 920) · `IA`, `IB` treasure_items · `AK%` attack_power (the
whole value; DOS stores it / 10) · `S%` the Apple's picture number. **No byte_27, no byte_33** (DOS's picture / undead
bytes). Values are whole numbers (DOS packs hp > 200, xp and gold > 64,000 into a byte / word, `encounters.md` 4).
`NIKADEMUS` (side B, 256 B) is reloaded before monsters 77 / 78 (`DUNGON3` 899) `[?]` (picture data).

### Items: `ITEM` + `TINFO` `[C]`

`ITEM`: 180 CR-ended names, ids 1–180 (`TOWN3` 230); no id 0. Value factor f = `TINFO`[89 + id]; price
`INT(f² / 5 · CF + 1)` (`TOWN3` 1117), CF = town factor / 100, so **value = INT(f² / 5 + 1)** (the price at factor 100,
DOS's `value · index / 100`). Sell `f² / 8` (8430). Points and requirement are formulas (`TOWN3` 1150–1190, 5510–5560;
the manual's tables): shields 1–20 = (id, STR ≥ id); armour 21–30 (id − 20, STR ≥ 2 id − 40), 31–40 (id − 29, 2 id −
64); bows 41–60 (id − 40, STR + DEX > 2 id − 82); weapons 61–80 (INT(id / 1.36 − 43), 2 STR + DEX ≥ 3 id − 180), 81–100
(INT(1.2 id − 95), ≥ 3 id − 240). Kinds by id range (`II$`, `STARTER` 470).

### Towns: `TF3` `[C]`

17 × 250 B, record t = town t (the new-game template; play keeps `TWN3t`). File offset k = address `LA` + 1 + k:
stock of item id at id − 1 (counts: `TOWN3` 1290 buy −1, 8450 sell +1 up to 99), restock pool 180–225 (860), price
factor 226 (825), exit section / x / y 227–229 (6020: where the party stands after leaving), name 230–249 padded with
0x15 (810). No "kinds" byte. Town names, numbers and order are DOS's.

### Rule tables: `TINFO`, `INFO` `[C]`

`TINFO` (1730 B, offsets = address − 2300), the Apple's class order THIEF, MONK, RANGER, FIGHTER, PRIEST, WIZARD
(Phantasie I's; the extractor keys by III's class names):

| Offset | Table | Read by |
|---|---|---|
| 0 + 15 class + level | HP dice, levels 1–15 (dice of 1–3, `v · (CON + 5) / 16` of them) | `TOWN3` 1520 |
| 89 + id | item value factor | 1117 |
| 275 + 5 race + attr / 350 + 5 class + attr | creation modifiers (not in DOS's extract shapes) | 2250–2260 |
| 380 + 60 class + spell − 1 | the level (1–15) the Guild offers the spell at, 0 never | 1570 |
| 740 + 15 class + level | magic ceiling | 1505 |
| 830 + 150 class + 10 level + slot | skill coefficients, slots ATTACK, PARRY, SPOT ITEM, SPOT TRAP, DISARM, LISTEN, FIRE BOW, PICK LOCK, ????, SWIM (`TOWN3` 130) | 1512 |

Spell rows (DOS's 13 rows × 15 levels) are rebuilt from the levels, spells ascending per level `[~]`: they equal DOS's
cell for cell. `INFO` + 223..237 = the wilderness terrain monster offsets (`OUTDOORS3` 6216 `PEEK(993 + TR + RND · 4)`):
DOS's entries 1–15 exactly; DOS's entry 0 and its overrun 16–25 have no Apple counterpart.

**Formulas where DOS has tables** (Apple ≠ DOS rules, not cells: Core plays DOS's / the manual's rules):
- Training XP: `XX = 1000 · (1.8^L − 0.8) / J / (1 + (social = 5) + 5 (social = 4))`, J = a class score of the
  attributes (`TOWN3` 2815–2820; DOS: the `training_xp_curve` table).
- Training gold: `CF · (20 − CHA)² · L^2.2`, at most 15,000 (2900; DOS: `training_gold_curve` · index · (20 − CHA)² / 100).
- Skill gain on training: `(old + table) / 2 + seed − 49.5`, 1..255 (1512).
- Spells: at most `L · INT^1.177 / 90 · (class + 1 − 3 (Fighter))` known (3010); a try costs
  `CF · 9000 / CHA / INT · L / (9 − class)` and succeeds below `5 · INT` % (3055–3100).
- Hit location: `rnd^(DT / 2) · 6` (`OUTDOORS3` 8100; DOS: `hit_location` rows).
- Mystic's score: level / 6 summed + `log(1 + quest bits) · 16.23` (`TOWN3` 4630; DOS: `score_weights`).
- Wilderness monster: `12 · TY + INT(RND · 2) + 2 · offset` (6216; DOS `12 L + offset`), TY = the square's level digit.

### World: `OT1`–`OT43` `[C]`

256-B sections (side A): 10 × 15 tile bytes at `LA` + 10 y + x (`OUTDOORS3` 210, moves 5220–5375: off an edge to the
section ± 1 / ± 5), then the section's places: byte 150 town number, 151 dungeon file (`DNG<n>`), 152 / 153 the
dungeon's entrance x / y, 154 the town name (8-column window), 171 the inn name (8), 180 a sign text, 226 the dungeon /
place text (15). **Tile byte = 10 · terrain + level** (5070): terrain `TR$` (`STARTER` 120: GRASS, FOREST, HILLS,
MOUNTAINS, BUILDINGS, DUNGEON, PATHWAY, BRIDGE, LAVA, DESERT, WATER, MIST; on the planes terrain 7 shows THE LIGHT
PLANE / THE DARK PLANE / NETHERWORLD LEVEL THREE, 5400), the ones digit the square's monster level (6 / 7: a sign / a
place text, 8: no encounter); 40 town, 41 inn, 50–59 a dungeon, 100 ocean / River Styx (blocked, 5270).
- **Scandor** = `OT1`–`OT25`, 5 × 5 sections, section s at (10 ((s − 1) mod 5), 15 ((s − 1) div 5)): the 50 × 75 map.
  Against DOS's `WORLD.DAT`, terrain by terrain (DOS tile → terrain by its ranges), **3,712 of 3,750 squares agree** (the 38 others: Lansing's square and its road, a few coast, desert, forest and path squares);
  DOS's tile ids (coast and edge variants of each terrain) are not derivable from the Apple's bytes (no neighbour rule
  reproduces them: 757 squares miss with the best 8-neighbour rule).
- **Planes**: Light `OT29`, Dark `OT31` (10 × 15 each), the Netherworld `OT41`–`OT43` (30 × 15): the Apple's own maps,
  not DOS's 9 × 9 / 31 × 16. A dungeon exit's (destination, x, y) is (section, x, y) where the party lands
  (`DUNGON3` 7180) `[C]`; DOS reads the destination only.
- **Places**: a section holds at most one town, inn and dungeon. Every Scandor square equals DOS's place list but
  **Lansing** (Apple (21, 11), DOS (22, 12)); the plane places sit elsewhere on the Apple's own maps. Neither inns nor
  dungeons carry a number: the extractor names them by the section III's place list puts them in (`DUNGEON_SECTION`,
  `INN_SECTION`) and lists the places in III's order (`PLACE_ORDER`) `[~]`. **Start**: Pendragon (`TOWN3` 710), then
  its record's exit square (section 13, (4, 7)) = (24, 37), DOS's.
- Encounter level: per square (the ones digit), not DOS's per-section table; the digit is mostly DOS's section level.

### Dungeons: `DNG1`–`DNG10` `[C]`

Side B, 4,132 B each, **in DOS `DNGS.DAT`'s record layout** (map 24 × 30, level, forced fights, exits, event squares,
event programs to 0x593, text pointers at 0x593, texts at pointer − 0x800; `DUNGON3` 390, 8200–8660), with a shorter
text area. `DNG<n>` = DOS record letter n (A..I); **`DNG10` = DOS's records J and K** (byte-identical in DOS), used by
dungeons 1 and 8. The dungeon of a section is its `OT` byte 151 (`DNGLD3` 330). Map cells equal DOS's in all eleven;
the tables equal but for the event cells in section 'Apple ≠ DOS'. Texts: those padded with `=` are laid out for the
15-column window (`DUNGON3` 6000 / 6310), the others for the 40-column screen (8210); both read as one line, then laid
out in 38-column rows (DOS's width) `[~]`. `DNGSAV` / `DNGCHK` are the played dungeon and its number (`DNGOUT3`).

### Scrolls: `SC1`–`SC20` `[C]`

1,000-B dumps of the text page ($400), shown as they are (`TOWN3` 8010); item i (121–140) shows `SC(i − 120)`, then the
next file while row 22 column 0 is `!` (8020). `SC6` (Scroll IV) is the map of the known world drawn in characters
(DOS: a picture). Records in DOS's numbering (`SC1`–`SC5`, `SC7`–`SC20`); text = the 38 columns inside the frame.

### Names `[C]`

Classes `TOWN3` 260, races 270 (Phantasie I's 16; no Random, no Elemental), skills 130, origins `H$` / body states
`S$` 290, social classes 295 (the Apple's 4 = SUPER HERO, 5 = DARK LORD: DOS's numbers swapped), status short / class
short `STARTER` 450 / 430, terrain 120, towns `TF3`, inns / dungeons the `OT` texts (an inn shows as "THE " + name +
" INN", `OUTDOORS3` 7200). **No spell names** (cast by number, `TOWN3` 6882), no combat-order or rank labels as a table.
I → III transfer: `BACKUP` 4000–5015 copies I / II records unchanged (the maps are the identity in the Apple's numbering).

## 4. Apple ≠ DOS, and the standalone import

**Rule (the user's for II's Apple and III's Amiga): DOS's values where the Apple differs or has none.** An Apple-only
import (`p3-apple2`, recipe 1) writes the ten III tables from the Apple, then DOS III's value in every number /
structure cell of the shipped `GameData/phantasie3/apple2-import.json` (653 cells, made by `diff_a2_p3_vs_dos.py
--built … --cells`, applied by `CellPatches`); the Apple's own text is kept (names, messages, scroll pages). With DOS
III or the Amiga imported too, their tables win. Tests: `P3Apple2TablesTests` (C# = the Python extract; the [cr] sides;
the ten files), `P3Apple2ImportTests` (cells hold DOS's and the Apple's values; patched = DOS but provenance and text;
dungeon records equal to 0x593; the public folder; load, start in Pendragon, leave town; DOS / Amiga win).

The cells, by group (each a real Apple ≠ DOS difference unless "lacks"):
1. **Monsters** (69 numbers; Apple / DOS, DOS's packed values decoded): attack power on 27 monsters, 24 of them 5
   above DOS's tens (Wild Cat 45 / 40), Kobold 45 / 50, Undead Gnome 60 / 80, Giant Ghost 95 / 100; Drip Slime xp 87 / 97; Kobold damage 7 / 8, next-group 100 / 7; Goblin gold
   350 / 100 and treasure 72, 24 / none; Wight reaction −10 / −11; Gremlin −2 / 0; **Wolf** a full record (armour 2 /
   5, damage 16 / 2, level 5 / 0, special 0 / 7, hp 14 / 0, rating 7 / 14, companion 31 / 0, next group 65 / 31: DOS's
   Wolf is a zero-level stub); Giant Ghost damage 21 / 15; Dragon next group 60 / 0; Dragon King companions 35, 59 /
   none, next group 90 / 0; Spirit defense 300 / −1; the bosses' whole numbers against DOS's packed ones: Light Fairy hp
   450 / 400, Nikademus (77) hp 1250 / 1200, xp 999,999 / 999,000, defense 200 / −56, Nikademus (78) hp 750 / 900, xp
   300,002 / 300,000, defense 160 / −96, gold 99,999 / 99,000, Lord Wood hp 950 / 900, rating 28 / 26, companions 70,
   66 / none, reaction 7 / 10, next group 100 / 0, defense 170 / −86, gold 99,999 / 99,000. Lacks: byte_27 / byte_33
   (160 cells).
2. **Items**: Hammer (69) value 89 / 59 (factor 21); Cloth +1 requirement −2 / 0 (the formula; both always pass).
   Lacks: item 0.
3. **Towns**: 44 starting-stock cells (Knife (61) 5 where DOS 0 or 1 in seven towns, Club (63) 5 / 1 in two; no Staff
   (66) in twelve towns that DOS stocks with 1; Leather (23) 1–8 / 0 in four towns and 0 / 1 in four; Robes, Mace,
   bows, Small Shield +2, items 67 / 70, potions 102–106 in single towns);
   **Tirith price index 199 / 200**.
4. **Dungeons** (55): dungeon 4 square 0xe2 message 6 / 59, program 70 / 71; dungeon 5 square 0xe4 program 160 / 65
   and twelve event-program bytes (0x4d3–0x4de: Apple ops, DOS zero); dungeon 9 34 event-program bytes (0x42f–0x430,
   0x483–0x4a2); **dungeons 1 and 8 share one file** (letter J / DOS K for 8); **dungeon 11's entrance x
   12 / 11**. The Apple has two extra text slots in dungeon 5 ("YOU SCUM.", "YOU MUST RETIRE.") and one in 9; kept.
5. **Scrolls**: item 123 shows records [2, 3] / DOS [4]; 124 [3] / [3, 4, 5]; 130 [8, 9] / [8].
6. **World**: the Scandor grid in the Apple's encoding (75 rows; terrain agrees on 3,712 squares, see World); the three
   plane maps (other sizes and layouts); Lansing's square; every place tile; the terrain table (ranges by the Apple's
   bytes; no Ocean / Styx / Black Path / Steam / Tavern names, no blocked-from, no-encounter or Netherworld overrides).
7. **Rule tables**: HP dice 15 levels (DOS 22, L16–22 = L15; L1–15 equal); **skill tables: the Apple has Phantasie I's
   cells where DOS has D3-032's five data errors** (Ranger Attack L15 250 / 25, Ranger Parry L15 85 / 60, Thief Pick
   Lock L3 55 / 35, Monk / Wizard Parry not shifted), so `skill_table_fixes` is empty on the Apple (DOS's restored by a
   cell); terrain offsets entries 0 and 16–25; dungeon 8's letter. Lacks (the Apple computes them, section 3): the
   training XP / gold curves, score weights, hit location rows and columns, area levels.
8. **Elementals**: the Apple has none (Air's 31 spells from DOS); the elementals' numbers are P3Build's DOS constants.
9. **Names** the Apple lacks (63 + 57 spells): every spell name; races 5 / 16 (Random, Elemental), status 2–4, body
   state Broken, terrain names 12, 15, 17–21.

None of these is a manual question but the inn (DIFFERENCES-III D3-076): the manual has no monster, stock, dungeon or
training tables, and its HP chart (L1–15) and item points / requirements equal the Apple's.

### Left `[?]`

- `NIKADEMUS` (side B), reloaded for monsters 77 / 78: not read.
- `TINFO` 270–274: not read (as II).
- The per-square level digits where they differ from DOS's section level (DOS's table is used).
- The plane arrival squares: per dungeon exit on the Apple (`DUNGON3` 7180); DOS's fixed (4, 1) / (7, 3) are used.
