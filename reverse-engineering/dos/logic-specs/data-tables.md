# Data Tables — Monsters / Items / Spells / Races / Classes

All master tables live inside `PHANTASI.COM` as contiguous blocks. Names are
plain ASCII and were extracted verbatim; see `../extracted/names.json`
(regenerate with `tools/scripts/extract_names.py`). Numeric stat fields are
documented here as far as they are decoded.

**Addresses:** `PHANTASI.COM` is EXEPACK-packed (section Monsters, `docs/plan/RE-REFERENCE.md`). Every
script reads the **unpacked** image through the game's DS pointer tables; the DS addresses
below are canonical. The raw-file offsets kept in headings (`0x6d4f`, `0x66ff`, …) and the old
`file = ptr + 0x56c5 / 0x56d3` deltas only work because the string tables sit inside long literal
runs. Proof that this fails outside them: the raw read of item 180 returned `DIAMOND En\x0b²`.

| Table | DS pointer table | Entries |
|---|---|---|
| Classes | `0x167e` → strings `0x168a` | 6 |
| Races | `0x16b2` → strings `0x16d2` | 16 (15 = UNDEAD, race only) |
| Status | `0x1739` → strings `0x173f` | 3 (DEAD/OK/SLEEP) |
| Spells | `0x174d` → strings `0x17c5` | 60 slots, 0–53 + 57 used |
| Items | `0x0ec0` → strings `0x102c` | 181 |
| Monsters | records `0x1a6c`, stride `0x39`, name = first `0x14` bytes | 80 |

`names.json` `spell_level_names` (`HEALING 1`…`HEALING10`) are **the potion item names, ids
101–110**. They are referenced only from the item pointer table, not from any spell code. The key
name is historical.

## Classes [C] — `PHANTASI.COM` @ `0x6d4f`, 6 entries

`0=THIEF, 1=MONK, 2=RANGER, 3=FIGHTER, 4=PRIEST, 5=WIZARD`

Class roles [X] (manual): Fighter = pure melee; Ranger = melee + some healing;
Priest = healing/support caster; Monk = melee + combat spells + some thieving;
Thief = thieving skills (pick lock, disarm, find item); Wizard = offensive
caster, weak melee. Spellcasters = wizard, monk, priest, ranger.

## Races [C] — `PHANTASI.COM` @ `0x6d97`, 15 entries

`0=HUMAN 1=DWARF 2=ELF 3=HALFLING 4=GNOME 5=PIXIE 6=SPRITE 7=OGRE 8=GNOLL`
`9=TROLL 10=KOBOLD 11=ORC 12=GOBLIN 13=LIZ.MAN 14=MINOTAUR`

This is the full race enum used for the character `race` byte. The creation
menu (`0x5ff3`) offers a subset plus **RANDOM** (rolls any race, historically
the way to get the strong monster-races). Per-race attribute tendencies are in
the manual [X] (e.g. Dwarf +STR/+CON, Sprite +DEX/−STR, Minotaur/Ogre +STR,
Kobold −STR/+DEX/+CON) — exact modifier values are **[C]**: `TWNS.INT` `@0x942`
(5 bytes × race), applied as `attr + raceMod − 10` — see `character-creation.md`.

## Spells [C] — `PHANTASI.COM` @ `0x6e8a`, 55 entries

Name **pointer table** at DS `0x174d` (60 × `u16`; raw file `0x6e12`, `ptr + 0x56C5`
only inside this literal run). Slots 0–53 + 57 resolve the 55 spell names; slots
54–56 and 58–59 point to empty strings. **DIVINE I is DOS slot 57**, but remaster index 54
(`names.json` and `spells.json` record `dos_slot: 57`): it costs 2 MP and its known flag is `@210` (`magic-spells.md` section DIVINE I). Strings themselves begin at
`0x6e8a`. There is **no** interleaved cost byte in this table.

Index order (0-based) as stored:

```
0-3   HEALING I..IV        4-7   FIREFLASH I..IV
8-11  QUICKNESS I..IV     12-15  STRENGTH I..IV
16-19 PROTECTION I..IV    20-23  CONFUSION I..IV
24-27 WEAKNESS I..IV      28-31  BINDING I..IV
32-35 MINDBLAST I..IV     36-39  FLAMEBOLT I..IV
40 CHARM        41 SLEEP          42 TELEPORTATION   43 RESURRECTION
44 NINJA II     45 FEAR           46 DISSOLVE        47 [original text]
48 DISPELL UNDEAD  49 NINJA I     50 AWAKEN          51 MONSTER EVAL.
52 VISION       53 TRANSPORTATION 54 DIVINE I
```

MP cost = `(index % 4) + 1` — see `magic-spells.md` [C]. Category / effect
per spell also live there.

## Items [C] — names @ `0x66ff`, value factors @ `0x6427`

There is **no** fixed-size multi-field item-stat record (no packed AC/damage/price
struct per item). Combat “point value”, UNUSABLE gates, and Armory prices are
split across one u16 table + index formulas. Extracted JSON:
`../extracted/items.json` (`tools/scripts/extract_items.py`).

### Index space [C]

| Range | Kind | Examples |
|---|---|---|
| `0` | sentinel | `NOTHING` |
| `1..20` | shields | `GLOVE` … `GOD SHLD` |
| `21..40` | body armor | `CLOTHING` … `GOD ARMOR` |
| `41..100` | weapons | `STICK` … `GOD SWORD` |
| `101..110` | healing potions | `HEALING 1` … `HEALING10` |
| `111..120` | magic potions | `MAGIC 1` … `MAGIC 10` |
| `121..140` | scrolls | `SCROLL 1` … `SCROLL 20` |
| `141..180` | rings / treasure | `RING A` … `DIAMOND E` |

Equipment names alone span file `0x66ff`–`0x6a7a` (**101** strings). The **name
pointer** table continues through potions/scrolls/treasure (**181** ids
`0..180`). Pass-2 purchases used ids `21`, `46`, `111`, `128`
(`CLOTHING` / `DAGGER` / `MAGIC 1` / `SCROLL 8`) — matching `TWNS.DAT` stock
slots and extended inventory fields.

### On-disk layout [C]

| DS offset | File offset | Contents |
|---|---|---|
| `0xd54` | `0x6427` | `u16 V[182]` value/cost factors (last slot unused) |
| `0xec0` | `0x6593` | `u16` name pointers → DS strings from `0x102c` |
| — | `0x66ff`… | NUL-terminated name strings |

File offsets = DS + `0x56d3`, valid only inside this literal run. `extract_items.py`
reads the unpacked image. The block after
`scrolls.dtx` that looked like “XP thresholds” **is** this `V[]` table (cheap
items have `V=0`; clothing/stick are free-tier, not table breaks).

### Combat point value + UNUSABLE [C]

`PHANTASI.COM` `FUN_0000_3053` (@ `0x3053`, runtime `0xd987` when OV1 is mapped)
computes points and the STR gate. Club → points **4** (matches the manual). No
per-item class bitmask was found — UNUSABLE is STR/(STR×2+DEX) only.

```
if id < 21:   # shield
    points = id
    usable if STR >= id
elif id < 31: # armor (plain)
    points = id - 20
    usable if STR >= id*2 - 40
elif id < 41: # armor (magic/god)
    points = id - 29
    usable if STR >= id*2 - 62
elif id < 71: # weapon (plain)
    points = (id >> 1) - 20
    usable if STR*2 + DEX >= id*2 - 80
elif id < 101: # weapon (magic/god)
    points = (id*3 >> 2) - 50
    usable if STR*2 + DEX >= id*2 - 140
else:
    points = 0   # potion/scroll/treasure
```

`OV1` `FUN_0000_2fdb` picks the best usable shield/armor/weapon by **points**
into char `+0xf2` / `+0xf4` / `+0xf0` (item ids at `+0x52` / `+0x54` / `+0x56`).
Those point totals are the party armor-soak / weapon effectiveness used in
combat — there is no separate AC or damage-dice column. [C wiring / I exact
damage plug-in; see `combat-resolution.md`]

### Prices [C]

`V = value_factor[id]`:

| Op | Formula | Code |
|---|---|---|
| Sell to Armory (Inn Distribute SELL row only; the Armory itself has no sell) | `V² / 8` | `OV1` sell path @ `0x2e72` |
| Buy price | `floor(V² * town_price_index / 500) + 1` | `OV1` `FUN_0000_1d5b` |

`town_price_index` = `u16` at `TWNS.DAT` town record `+0x10f` (stride **291**;
Pelnor=`150`, falls toward ~80 at Olympia).

There is **no CHA (or other) armory-buy discount**. Capstone disassembly of
`FUN_0000_1d5b` loads `V[item_id]`, squares it, multiplies the current
`TWNS.DAT + 0x10f` price index, divides the signed 32-bit product by `500`,
then adds one. The buyer path at `OV1 0x1c97..0x1cdc` compares and subtracts
that full result from the shared carried-gold accumulator at `DS:0x63f6`.

Pass 2's four purchases are therefore exactly:

```
CLOTHING (V=0)  1
DAGGER   (V=6) 11
MAGIC 1 (V=12) 44
SCROLL 8 (V=20) 121
-------------------
total             177 GP
```

The three `GUILD @262` (bank) values each changed `256 → 197`, which is not a
per-purchase deduction: the save-time share split put the leftover purse back
into the banks: `(3 × 256 − 177) / 3 = 197`. Thus the observed
per-record delta is `177 / 3 = 59`. Stock changes and carried-item writes
prove these are the same purchases. `@262` must not be used as the party transaction ledger.

### Potions [X] (Schultz)

- Healing potion tier `N` (ids `100+N`) restores **`N²`** HP.
- Magic potion tier `N` restores **`3N`** magic. (`V` is only for price.)

## Monsters [C] — 80 × 0x39 records at DS:`0x1a6c` of the **unpacked** `PHANTASI.COM` (s22)

`PHANTASI.COM` is a COM loader around an **EXEPACK**-packed EXE (MZ header at file `0xf0`, image at `0x2f0`,
EXEPACK header `RB` at `0x86a0`; DS = SS = `0xc9f`). The bytes at file `0x7131` are EXEPACK tokens
(`b2` = copy run, `b0` = fill run, decoded backwards), not monster records: the old extracts' `unknown_A` was a copy
count, `combat_rating` a fill count and the "attack blocks" record fragments. `tools/scripts/re_common.unpack_phantasi_com()` rebuilds the image;
`extract_monsters.py` reads the table, and it equals the live DOSBox capture `monster_runtime_table.bin`
byte for byte. Output: `extracted/monsters.json` (shipped copy in `GameData/phantasie1/`).

The OV2 encounter builder `0x34aa` copies a record into the combat struct `0x7522 + slot·0x39` with the
same layout, so each role is pinned by an OV2 use-site (combat-resolution.md §Monster record):

| Off | Size | Field | Use-site (OV2) |
|---|---|---|---|
| `+0x00` | 20 | name, NUL-padded | |
| `+0x14` | i16 | armor | party damage `− armor·10` (`0xbce`) |
| `+0x16` | i16 | damage | monster → party damage factor (`0xea9`) |
| `+0x18` | i16 | level | `L += level` once per group (`0x3ac4`) |
| `+0x1a` | u32 | XP per kill | added to `[0x7512]` on death (`0x1656`) |
| `+0x1e` | i16 | special-ability gate | `0xd49` |
| `+0x20` | 3 × u8 | special ids | picked by `rand·3/100` (`0x1028`); effects `combat-resolution.md` §Monster specials `[C]` |
| `+0x23` | i16 | HP base | HP roll at build (`0x3a42`) |
| `+0x25` | i16 | CV | spell / breath to-hit (`0x13ce`, `0x1ae1`) |
| `+0x27`, `+0x28` | u8 | companion monster ids (0-based) | next group's monster (`0x3af7`/`0x3b14`) |
| `+0x29` | i16 | reaction modifier | Σ per group into `[0x750c]`; BEG `rand < Σ + 90`, GREET `rand < Σ + 10` (`0x3bed`, `0x3ced`) |
| `+0x2b` | i16 | next-group chance | another group if `rand < this` (`0x3b39`) |
| `+0x2d` | i16 | defense (often negative) | party melee to-hit `− defense` (`0xb5a`) |
| `+0x2f` | u32 | gold per monster | Σ into `[0x750e]` at build (`0x3a97`) |
| `+0x33` | u8 | group-size factor | count roll (`0x39b1`) |
| `+0x34` | u8 | `[~]` — the combat copy overwrites it with the sprite width | `0x38cb` |
| `+0x35`, `+0x36` | u8 | treasure item ids (first group only) | `[0x74a6]`/`[0x74a8]` → `0x421d` (`0x41e4`) |
| `+0x37` | i16 | attack power | monster melee to-hit; swings `max(1, ap/82)` (`0xd86`) |

Parallel table DS:`0x2c3e` (u16 width, u16 height per monster) → sprite size; the builder stops adding
groups once `Σ(width + 3) ≥ 135` (`0x399b`). Entry 79 (ORANGE DRAGON) reads past the table (height 22356) `[~]`.

Samples: SKELETON armor 2 / CV 1 / defense −75 / AP 50 / HP base 3 / XP 77; VIPER 3 / 12 / −10 / 80 / 20 / 732;
ZEUS 25 / 24 / 999 / 740 / 666 / 1 191 676. There are no two-form monsters: the old "second attack
block" was the companion bytes plus neighbouring fields.

## Towns [C] — `TWNS.INT`

12 towns + 3 elemental "interiors": `PELNOR, PINEVILLE, GREENVILLE, HALFLINGOR,
APPLETON, SPLITWATER, TROLLPORT, PHANTASIA, DRAGONOR, NORTHFORD, STARVILLE,
OLYMPIA`, then `FIRE ELEM, WATER ELEM, EARTH ELEM`. Each town name is a 10-char
space-padded header preceding that town's interior map grid. See
`town-menus.md` and `save-file-format.md` (per-town state lives in `TWNS.DAT`).
