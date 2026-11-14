# Phantasie IV — character creation, ancestry and inheritance (spec, 3d.6)

Source: X68000 `P4.X` (Ver.1.02), static RE with the program's own symbol table. Function names and addresses:
`../FORMAT.md` section 6 and `../FUNCTIONS.md`. Record layouts: `../FORMAT.md` section 6.2 (character), section 6.3 (Hall). Tables:
`../extracted/creation-tables.json` (`tools/scripts/extract_x68k_p4_creation.py`). MSX2 cross-check of sections 3, 4 and 6:
section 12. Tags: `[C]` read directly from the code (or code + data agree), `[~]` likely, `[?]` open. Rules are written as plain rules, not as a transcript of the code.

Conventions: `rand(n)` is the game's generator (`_rand`, a 31-bit shift register) and returns **1..n**; `rand(0)` = 1.
"Abilities" = the five rolled ones (Str, Int, Dex, Con, Cha); Luck is separate (section 7). `trunc` = toward zero.

## 1. The creation menu `[C]` (`_make`, `_makemenu`)

1. Creation needs a free guild slot (10 slots); otherwise an error message and back.
2. The game asks "Create the new character as the descendant of a mixed race? (Yes / No / E: Finish)".
   - **No** → plain character (section 2). The record's two ancestor references stay "none" (shown "Unknown").
   - **Yes** → descendant (section 3).
   - **E** → leave creation.

## 2. Plain character `[C]` (`_make2` → `_mkcha`)

1. **Race menu:** 1–5 = Human, Dwarf, Elf, Halfling, Gnome; 6 = "Random Creature"; 7 = Back.
2. **Random Creature** (`_rrace`) shows a list and the player picks from it:
   - normally (rand(100) ≥ 26, i.e. 75%): Pixie, Sprite, Gnoll, Kobold, Orc, Goblin, Lizard Man, Minotaur;
   - with rand(100) < 26 (25%): Ogre, Troll, Hill Giant, Gargoyle, Spriggan — but if any guild member is already a Hill
     Giant or Gargoyle, the list is Ogre, Troll, Spriggan.
   - Hybrids, `  ?` and Duck are never offered.
3. **Class menu:** only the classes the race allows (bit mask per race, `creation-tables.json` `class_mask_by_race`):
   - Human, Dwarf, Elf, Halfling, Gnome: Thief, Monk, Ranger, Fighter, Priest, Wizard.
   - Pixie … Minotaur (races 5–14): Thief, Fighter. Hill Giant: Fighter. Gargoyle: Gargoyle. Spriggan: Thief.
   - Hybrids: Half Elf = the six + Shadow Warrior (the only race with Shadow Warrior); Half Dwarf, Half Gnome, Dwarfling,
     Dwarf Gnome, Gnomeling = the six; Half Ogre, Half Orc = Thief, Fighter, Priest, Wizard; Kobold-Dwarf = Thief,
     Ranger, Fighter, Priest; Gnome-Goblin = Thief, Fighter, Priest; Sprixie, Trollgre, Gnoll-Orc, Koblin, Lizataur =
     Thief, Fighter.
4. **Abilities:** rolled with the fixed exponent 0.6 (section 5.3).
5. Confirm, then name (8 characters), then the record is built (section 7).

## 3. Descendant: the two ancestors `[C]` (`_make1`, `_make5`)

### 3.1 Ancestor 1 (`_make1`, screen "First ancestor")

The player chooses "N: Decide anew" or "H: A person from the Hall".

- **N:** exactly like a plain character's race/class/ability choice (section 2), except Random Creature offers the 8-race list
  or, at 25%, only **Ogre, Troll** (`_rrace` lists 3/4). Abilities are rolled with exponent 0.6.
- **H:** pick a Hall person; ancestor 1 = that person's race, class and five abilities as stored in the Hall; their move
  base is recomputed from race + class. The Hall slot is remembered ("the chosen Hall person").

### 3.2 Ancestor 2 and the descendant's race (`_make5`, screen "Create Descendant")

The player chooses how ancestor 2 is found: **S: Same race**, **D: Different race**, **H: Race of a Hall person**.

**Nikademus rule:** if ancestor 1 is Hall slot 2 (Nikademus), every choice ends in "Not possible" and creation starts
again from ancestor 1. `[C]`

**S and D** (`_mopta(threshold, wanted)`; S = (50, same), D = (25, different)):

1. Let `A` = ancestor 1's race. Roll `r = rand(100)`.
2. If `r > threshold` (S: 50%, D: 75%): collect every race `x` in 0..14 (the 15 races of I) for which `combine(A, x)`
   (section 4) exists; pick one of them uniformly. Ancestor 2's race = `x`, the descendant's race = `combine(A, x)`.
   If no `x` exists, fall through to step 3.
3. Otherwise ancestor 2 is the same race as `A`, and the descendant's race = `A` (no table lookup; so same-race works for
   every race, also those that `combine` would reject).
4. Message: the game shows ancestor 2's race. It prefixes "Sorry, the ancestor you found was …" when the result is not
   what the player asked for: for S when ancestor 2 is a different race; for D when it is the same race.
5. Ancestor 2 is then a **new person** of that race: the player chooses its class (class menu of that race) and its
   abilities are rolled with exponent 0.6 (section 5.3). It is stored as a "new" ancestor (section 7.4).

**H** (`_moptb`): the player picks a Hall person `P`. The descendant's race = `combine(A, P.race)`. "Not possible" if any of:
- `combine(A, P.race)` does not exist (this includes same-race pairs above race 4, e.g. Orc + Orc, and any pair with
  a `  ?`-race person such as Nikademus);
- `P` is the same Hall person as ancestor 1;
- ancestor 1 is one of the shipped three (Hall slots 0–2) **and** `P` is one of the shipped three.
Otherwise ancestor 2 = `P`'s stored race, class and abilities (a Hall ancestor).

**The descendant** (`_set2work(…, descendant race, 1)`): the player picks the class (class menu of the descendant's race),
the abilities are inherited (section 5), confirm, then social class (section 6), name, and the record (section 7). If the player declines at
the confirmation, nothing is created and the creation menu returns.

## 4. Mixed races: `combine(a, b)` `[C]` (`_combi`)

Order does not matter. Races 0–4: table at `0x41516`:

| | Human | Dwarf | Elf | Halfling | Gnome |
|---|---|---|---|---|---|
| **Human** | Human | Half Dwarf | Half Elf | — | Half Gnome |
| **Dwarf** | | Dwarf | — | Dwarfling | Dwarf Gnome |
| **Elf** | | | Elf | — | — |
| **Halfling** | | | | Halfling | Gnomeling |
| **Gnome** | | | | | Gnome |

Any other pair: key = `((max(a,b) << 4) & 0xF0) | (min(a,b) & 0x0F)`, looked up in nine fixed pairs:

| Pair | Result |
|---|---|
| Pixie + Sprite | Sprixie |
| Human + Ogre | Half Ogre |
| Ogre + Troll | Trollgre |
| Dwarf + Kobold | Kobold-Dwarf |
| Human + Orc | Half Orc (matches magazine B's screenshot) |
| Gnoll + Orc | Gnoll-Orc |
| Gnome + Goblin | Gnome-Goblin |
| Kobold + Goblin | Koblin |
| Lizard Man + Minotaur | Lizataur |

No other pair has a result. **Original quirk `[C]`:** the key keeps only the low 4 bits of the larger race number, so a
race ≥ 16 would alias onto a listed pair (Dwarf Gnome 22 + Pixie → Sprixie; Gnomeling 23 + Human → Half Ogre; Trollgre
26 + Dwarf → Kobold-Dwarf). It cannot happen in the shipped game: a new ancestor 1 is one of races 0–14 (the ancestor
lists of section 3.1), the S/D roll only tries races 0–14, and Hall persons come from III (races 0–14) or are the shipped three
(Human, Human, `  ?`). A port can use the plain table `[~]`.

## 5. Abilities

### 5.1 The dice `[C]` (`_mdice(race, class, ability, k)`, `_frand`, `_mexp`)

```
die()        = 6 × u^k        where u = rand(100) / 100   (0.01 .. 1.00, uniform)
roll         = trunc(die() + die() + die()) + 3                       (3 .. 21)
roll        += class_mod[class][ability];  if roll > 18: roll = 18     (TABL 0x122)
roll        += race_mod[race][ability];    if roll < 3:  roll = 3     (TABL 0x078)
```

The cap of 18 applies before the race modifier, so race modifiers can lift a value above 18. `k` < 1 skews the dice
upward, `k` > 1 downward.

### 5.2 Inheritance (`_cmbattr`) `[C]`

For the descendant, for each of the five abilities:

1. Take both ancestors' stored values and **remove their own bonuses**:
   `raw = value − class_mod[ancestor class][ability] − race_mod[ancestor race][ability]` (`_sub_bonus`).
2. `avg = (raw1 + raw2) / 2` (a real number, may end in .5).
3. Exponent `k` from `avg` (`_get_modifer`, table `inheritance_exponent`): start at 18.0 and step down by 0.5;
   the first step `s_i = 18 − 0.5 i` with `avg ≥ s_i` gives `k = K[i]`:

   | avg ≥ | 18 | 17.5 | 17 | 16.5 | 16 | 15.5 | 15 | 14.5 | 14 | 13.5 | 13 | 12.5 | 12 | 11.5 |
   |---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
   | k | 0.05 | 0.078 | 0.141 | 0.184 | 0.227 | 0.273 | 0.322 | 0.374 | 0.43 | 0.491 | 0.557 | 0.629 | 0.707 | 0.795 |

   | avg ≥ | 11 | 10.5 | 10 | 9.5 | 9 | 8.5 | 8 | 7.5 | 7 | 6.5 | 6 | 5.5 | 5 |
   |---|---|---|---|---|---|---|---|---|---|---|---|---|---|
   | k | 0.892 | 1.0 | 1.126 | 1.268 | 1.433 | 1.627 | 1.859 | 2.141 | 2.492 | 2.943 | 3.543 | 4.38 | 5.635 |

4. Roll the ability with section 5.1 for the descendant's race and class and this `k`.

**avg below 5.0 `[C]` (CPU check):** the lookup loop runs out (27 steps, 18.0 down to 5.0) without returning a
table value, and the function returns what is left in d0:d1: the loop counter 27 and the low half of the last stepped
value, i.e. the double `0x0000001B_00000000` ≈ 5.7 × 10⁻³¹³ (a denormal). Run with
the real P4.X and FLOAT2 code (`tools/scripts/x68k_p4_cpu_harness.py ancestry`): `_get_modifer` returns exactly that
for avg 4.5, 4.0, 3.0, 0.0 and −3.0, and 400 real `_mdice` calls (Human Fighter Strength, real `_rand`/`_frand`/`_mexp`)
give 18 every time, against 5–18 at avg 5.0 and 9–18 at avg 13.5. So a pair of very weak ancestors gives maximum
abilities (each die 6, roll 21 before the 18 cap). MSX2 not checked. D4-056 (user ruling): a bug; the remaster uses the
table's last exponent (k = 5.635, weak dice) below 5.0, the run-off as option `p4.weak-ancestors` = `x68000`.

**Hall persons of class Sage/Lord `[C]`:** `TABL` has no class row for classes 8–9, so step 1 subtracts bytes of the
next table: Sage (Filmon) −6, −6, −7, −7, −8; Lord −8, −9, −10, −11, −12. Filmon's raw values are therefore 6, 15, 7,
6, 7, and he passes on far weaker abilities than his stored 12/21/14/13/15 suggest. (Nikademus can never be used.)

**Check against the one published sample** (MSX Magazine 1991-03; parentage doc section 1): Lord Wood (Human Ranger
18/17/16/16/17) + Orc Fighter 19/12/14/12/6 → Half Orc Fighter. Raw: Wood 18/16/16/16/16; the Orc 16/16/14/12/11.
avg 17/16/15/14/13.5 → k 0.141/0.227/0.322/0.43/0.491. Expected roll ≈ 18/17/16/15/14 before modifiers, and the
published 19/17/18/14/13 lies inside each ability's possible range (Str: 18 cap, +1 Half Orc = 19). Consistent `[C]`.

### 5.3 New persons (plain characters and "new" ancestors) `[C]` (`_newattr`)

section 5.1 with `k = 0.6` for every ability (expected die 3.75, expected roll ≈ 14 before modifiers).

### 5.4 The creation screen's sixth value `[C]`

The creation screens print six values under the ability names; the sixth (labelled "Luck") is the combat move base
`move_race[race] + move_class[class]` (`_moverate`), not Luck. Real Luck is rolled later (section 7).

## 6. Social class `[C]` (`_get_social`, `_mkdetail`)

1. Base: `r = rand(100)` for races 0–4, else `r = rand(75)`; social = Peasant if r ≤ 29, Laborer if ≤ 59, Craftsman
   if ≤ 89, Noble otherwise. So Noble is possible only for Human, Dwarf, Elf, Halfling, Gnome (11%).
2. For each ancestor that is a **Hall person** (in order ancestor 1, then 2): if that person's social class is Super
   Hero → the descendant becomes **Royalty**; if Dark Lord → **Outcast**. Any other Hall social class (Lord Wood's
   Royalty, Filmon's Noble, Nikademus's Black Lord) has no effect. The last matching ancestor wins.
3. "New" ancestors get their own social class from step 1 by their race (stored in `_anc`, shown on the character
   sheet); it does not affect the descendant.
4. Social class also sets the gold a character earns at each level-up (social^social × level × 30; Noble 810 per
   level, Royalty the 65 535 cap): `training.md` section 4. The training cost itself has no social-class term.

## 7. The new record (`_mkdetail`) `[C]` unless tagged

1. Name, race, class, five abilities and move base from the creation buffer; condition Okay; level 1.
2. Ancestor references: none / Hall (slot) / new (index = guild slot × 2 + n, with social, race, class stored).
3. Social class (section 6). **Gold** = social × 256.
4. **Hall ancestors:** each one's descendant count +1 (stays ≤ 99). **Divine Aid:** the descendant knows spell 62
   (Divine Aid) iff the (last) Hall ancestor has a Phantasie I, II or III result ≥ 100 ("completed") (`_chkwon`).
5. **Origin** from the map where the character is made (`_cmapn`): `2 + [m=2] + 2·[m=9] + 4·[m>13] + 3·[m=13] − 2·[m=8]`
   (index into `_origin`: map 1 → Scandor) `[C]` expression, map meanings `[~]`.
6. **Flier flag** (+B8) for Pixie, Sprite, Gargoyle, Sprixie `[~]`.
7. **Starting spells:** Monk Fire Flash 1 + Accuracy; Ranger Healing 1 + Fire Flash 1; Priest Healing 1 + Awaken;
   Wizard Fire Flash 1 + *Speed 1; Gargoyle *Speed 1. Everyone except Shadow Warriors carries Darts.
8. Level-up routine at level 0 (`_calpow`): skills, magic level, hit points (TABL, `../FORMAT.md` section 6.6), level = 1.
9. **Luck** = rand(6) + 8 = 9..14. **MP** max = current = trunc(0.75 × magic level).
10. **Aging factor** (+20) = Luck, then ÷ 2.5 for races ≥ 7, × 2.5 for Dwarf/Halfling/Gnome, × 19 for Elf, × 1
    otherwise; trunc, cap 255. **Original bug `[C]`:** the race tested is the race of **guild slot 0**, not of the new
    character (all four test saves fit this: their slot 0 is a Minotaur, so every character got Luck ÷ 2.5).
11. **Starting age** (+1E, in 1/26 years) = trunc(260 + 256 × F × (0.1 + (rand(100) − 1) / 4000)), F = aging factor;
    i.e. 10 years + about F years. Fits all test saves.
12. +2A..+2E = 2, 0, 2, 3, 3 `[?]`.

## 8. Hall of Glory: biography lines and status `[C]` (`_dsp_hinfo`)

Each Hall person shows two lines of "Deeds" (table `0x418a6`, 2 lines per index):

| Index | Lines |
|---|---|
| 0 | Leader of the armies of Scandor / who fought Nikademus (Lord Wood) |
| 1 | A sage, adviser to Scandor / for more than 75 years (Filmon) |
| 2 | Attempted to conquer Scandor (Nikademus) |
| 3 / 4 | Defeated the Black Lord and defended Gelnor / Failed to defend Gelnor |
| 5 / 6 | Freed Ferronrah from the curse of Nikademus's orb / failed to free Ferronrah |
| 7 | Defeated Lord Wood and aided Nikademus, for a time bringing terror to Scandor |
| 8 / 9 | Defeated Nikademus and saved Scandor / Failed to save Scandor |
| 10 | Nothing of note |

Slots 0–2 use the stored index. For imported persons (slots 3–10), with results `p1, p2, p3` (0 = not played,
1–99 = played, ≥ 100 = completed):

```
if p3 != 0:            index = 9 (failed)            if p3 < 100
                       index = 8 (saved Scandor)     if p3 >= 100 and social != Dark Lord
                       index = 7 (aided Nikademus)   if p3 >= 100 and social == Dark Lord
elif p2 != 0:          index = 5 if p2 >= 100 else 6
elif p1 != 0:          index = 3 if p1 >= 100 else 4
else:                  index = 10
```

Only the latest game played counts. The per-game rows "Phantasie I / II / III" show "Completed" / "Not completed" /
blank by the same thresholds (imports only).

**Status line:** Dead / Alive from byte +1A. For Lord Wood (slot 0) it is recomputed on display: Alive, unless some
imported person (slots 3–10) has class byte 4 → Dead (the byte is copied from III's record unchanged; section 8.2 item 5).

### 8.1 Feeding the lines from the shared model (`Character.Deeds`, for T6.3)

The Hall stores no deed list, only the three per-game results and the social class. A remaster character can produce
them from `Character.Deeds` like this (p1 / p2 are built for the I / II option, section 8.4, with `Deed.Slew` and
`Deed.Destroyed`; p3 and the social row stay proposals, as III's transfer copies III's own scores, section 8.2):

| IV field | From the shared model |
|---|---|
| `p1` | 0 if the character never played I; 100 if it has a deed "completed I" (killed the Black Lord: the existing `Deed.Slew` with I's Black Lord as subject); else 1 |
| `p2` | same for II (completed = freed Ferronrah / destroyed the orb) |
| `p3` | same for III (completed = Nikademus defeated, or the Lord Wood ending) |
| social | Dark Lord for the III ending that sides with Nikademus (that ending sets the social class in III) `[~]` |
| alive (+1A) | the character's alive/dead state at transfer |

"Played" needs a per-game "took part" fact (e.g. a deed `played:<game>` written when the character first enters a
game); without one, a character imported from I/II (the user's T5 option) could fall back to "played" = it has any
record in that game. Characters with no record give index 10, "Nothing of note".

### 8.2 The III transfer: the Hall's T (s148) `[C]` unless tagged

Read from the decompile of `_hall` `0xdacc`, `_htran` `0xccfe`, `_trans` `0xcb1a`, `_p3disk` `0xcab4`, `_mtrans`
`0x11680`, `_show_gm` `0x10ef8`, `_mark2`, `_cntdown`, `_mcopy`, `_hpix` `0xca5c`, `_dsp_hinfo`.

1. **`_htran`:** "Phantasie III's / character data? / Transfer" (`0x41a06`, `0x41a13`, `0x41a1f`; English reordered as
   "Transfer Phantasie III's / character data?") with O: Okay / C: Cansel. C → back to the Hall.
2. **`_trans`:** a loop: the disk prompt (one drive: "Insert III's player disk in drive 1", `0x41822`; two: "Swap the disk
   in drive 1 for III's player disk", `0x417f6`; then "When ready, press the space bar") with Q: Abort / SPACE: Start.
   Q → back. SPACE → `_p3disk` (sector 0x120 holds "Pendragon" at byte 230); a wrong disk → the prompt again. After
   `_mtrans`, with two drives, "Put the original disk back in drive 1" and a key. Then the Hall is redrawn with the
   imports' figures (`_hpix(1, slot)` loads `p4mon.pix` record race + 84).
3. **`_mtrans`:** reads III's 21 records (15 sectors from 0x20) and lists them (`_show_gm`, window "Guild roster", "==
   Phantasie III ==", two columns of 12 lettered A–U; a record is listed when its byte 0x3A > 0: name 0x82, class 0x8C as
   Thi / Mon / Ran / Fig / Pri / Wiz, `0x427d6`), with O: Okay / X: Abort. Free = the unused Hall slots (of 11; 0–2 are
   always used, so at most 8).
   - Free 0: "No more can be transferred." (`0x4285a`) and SPACE; nothing else.
   - Else a loop: "N more can be transferred." (`0x42873` + N + `0x42879`); a pick toggles that line's mark (`_mark2`,
     an XOR box); a key that is no button clears every mark (`_bvs` → −1). When the marks reach Free: "No more can be
     transferred." and on. O goes on at once; X returns with nothing copied.
   - `_confirm` (Okay / Cansel); Okay copies each marked record, in list order, into the first unused slot (`_mcopy`).
     Nothing stops the same III character being transferred twice.
4. **`_mcopy`** (III offset → Hall): used 1; name 0x82 (10 bytes); race 0x96; class 0x8C; level 0x35; social 0x7F;
   origin 0x26; abilities 0x01–0x05; I / II / III results 0x28 / 0x08 / 0x79; descendants 0; status 1 when 0x12 ≠ 0
   (else the slot's cleared 0, Dead). The III record behind these offsets (160 bytes) is not DOS III's 262-byte one; which
   III release writes it is `[?]` (no Japanese III disk in the repo).
5. **The Hall sheet** (`_dsp_hinfo`) for slots 3–10: race > 14 shows as "?" (race 33); origin from the import list
   `0x4187a` (Gelnor, Ferronrah, Netherworld, Scandor, Plane of Light, Plane of Darkness = III's origin order, not IV's
   `_origin`); the three result rows "Completed" (≥ 100) / "Not completed" (1–99) / blank (0; `0x41bc7` is empty);
   the deeds line of section 8. **Lord Wood's status** (slot 0) is recomputed here: Alive unless an import's **class byte**
   (+0C) is 4 — III's and IV's Priest. Read as written; it looks like a slip for the social byte (DOS III's Darklord is
   4, "Lord Wood killed") `[?]`.

**Remaster (`P4Hall`, `P4TownLoop.HallTransfer`):** the source is the remaster's III save (`savegame-p3.json`,
its top-level roster, every character listed, letters A–Z); no disk prompts: with no III save the Hall says so
(`core.p4.hall_no_iii_save`) and returns. Marks show as "*"; the list's O / X are two lines after the characters (no
mark-clearing stray key). The imports live in the IV save (`P4Party.HallImports`, slots 3–10); every Hall reader
(the profile list, creation's Hall lists, the ancestor line) reads the merged list. Field mapping by meaning:

| Hall field | From the remaster's III character |
|---|---|
| name | `Name`, cut to 10 |
| race | `Race` (I's numbering 0–14 = IV's); an Undead's `BirthRace`, "?" (33) if none `[~]` (raw 15 would be IV's Hill Giant) |
| class | `Class` (III's order = IV's 0–5) |
| level | `Level` (cap 255) |
| social | III's 0–3 as they are; Darklord 4 → IV's Dark Lord 5, Superhero 5 → IV's Super Hero 4 (`names.json`; IV's own tests read 4 = Super Hero → Royalty, 5 = Dark Lord → Outcast and "aided Nikademus") |
| origin | `P3.Origin` (III's order = the import list) |
| results | `P3.Phantasie1Score`, `P3.Phantasie2Score`, `Score` (III's score: 200 Darklord / 250 Superhero), each capped at 255 `[~]` |
| alive | `Status` ≠ Dead |

The imports' figures on the Hall stage (`_hpix`): built (FORMAT.md section 6.9 "Hall stage": record race + 84,
frame 0, slots 3–5 at (151 + 48 p, 209), slots 6–10 at (23 + 48 (p − 3), 259) from HALL_A's top-left).

### 8.3 Remaster option: a III character joins IV's Guild (s149) `[~]` unless tagged

The user's s72 option `p4.transfer-join` (`hall` = the original, every preset; `guild` = this section). No original does
it: the manual (p. 8) says transferred characters "serve only to make descendants", and the code writes only Hall slots
(`DIFFERENCES-IV.md` D4-017). So everything here is a remaster rule; where IV has a routine, the routine is used as decoded.

**Flow.** The Hall's T as in section 8.2 up to `_htran`'s Okay and the III roster read; then (only with the option on and
III as the source) "Where shall they go?" with 1 Hall of Glory (section 8.2 unchanged) / 2 Guild; SPACE leaves. Guild: the
same `_show_gm` list and marking, with Free = the empty Guild slots (of 10; `_make`'s limit, section 1 item 1) instead of
the Hall's. Characters with no IV race (an Undead with no living race, section 8.2's "?") are not listed: IV has no tables
for race 33. After Okay each marked character takes the first empty Guild slot, out of the party, in town, ancestors
"Unknown" (IV's Hall persons have none either, section 3).

**The record.** Built by `_mkdetail` (section 7) as for a plain character made in this town, with the III character's
race (section 8.2's race rule), class and five abilities in place of the creation choices `[C]` routine. So these come from
IV's code: Luck (rand(6) + 8), the level-0 `_calpow` (skills, magic level, HP, the level-1 spell row), MP, the starting
spells and Darts by class, flier flag, aging factor (with the guild-slot-0 race under the code's lifespans, as creation
does) and starting age, combat strategy, level 1. Then the remaster sets:

| Field | Rule |
|---|---|
| name | III's name cut to 8 (`_mkname`'s limit) |
| class | III's class even where IV's class mask (section 2 item 3) would not offer it to that race (e.g. a III Orc Priest): IV's per-class and per-race tables cover classes 0–5 and races 0–14, and the character already is that class |
| social class | section 8.2's mapping (III 0–3 kept, Darklord → Dark Lord 5, Superhero → Super Hero 4). Note: a Super Hero / Dark Lord member earns 7 680 L / the 65 535 cap in level-up gold (`training.md` section 4) |
| account (gold) | social × 256 (section 7 item 3) for that social class; III's money stays in III |
| experience | 100 × III's level: the series' own transfer rule (DOS III's `ovl1`, D3-072), so the character can train up again by IV's tables and costs; level stays 1 |
| spells | IV's starting kit only (IV's spell list is its own); plus Divine Aid (spell 62) when III's record has Divine Aid, as `_chkwon` gives it to a completed hero's descendant |
| items | Darts by `_mkdetail` only; III's items stay in III |
| origin | IV's `_origin` entry of the same name for III's Gelnor, Ferronrah, Scandor, Plane of Darkness; else (Netherworld, Plane of Light) `_mkdetail`'s own (where the character joins) |
| condition | Okay; Dead (HP 0) when III's character is dead |
| Score (+09) | 0 (IV's training gate reads it, `training.md` section 1) |

### 8.4 Remaster option: Phantasie I and II characters into the Hall (s149) `[~]` unless tagged

The user's s72 option `p4.transfer-games` (`iii` = the original, every preset; `all` = this section). With it on, the
Hall's T asks after `_htran`'s Okay "Transfer from which game?" (Phantasie I / II / III; SPACE leaves); III goes on as
section 8.2 (and 8.3); I and II read the remaster's own save of that game (`savegame-p1.json` / `savegame-p2.json`, its
top-level roster, as the I / II → III transfer does) and copy into the Hall only (never the Guild: no rule exists for an
I / II record in IV's party). The list's heading names the game; without that game's save the Hall says so. Marking,
Free, Okay / Cansel and the copy order are section 8.2's.

| Hall field | From the I / II character |
|---|---|
| name, race, class, level, abilities, alive | as section 8.2 (I's and II's races and classes are I's numbering = IV's) |
| social | Noble (3): what DOS III writes for every I / II character it takes in (`rule-tables.json` `transfer`) |
| origin | I: Gelnor (0). II: II's own origin (Gelnor / Ferronrah / Netherworld = the import list's 0–2) |
| p1 | 100 if the character has the deed "slew I's Black Lord" (`Deed.Slew` on `p1:` Black Lord, section 8.1); else 1 if it played I (from I's save, or a II character whose II origin is Gelnor = came from I); else 0 |
| p2 | 100 if the deed "destroyed II's orb" (`Deed.Destroyed` on the orb `p2:150`, written by II's ending, Filmon's ceremony, for every party member); else 1 if it played II (from II's save, or it has II data, i.e. came from II to I); else 0 |
| p3 | 0 |

"Played" is section 8.1's fallback (it comes from that game's save); no older save is changed. The biography line, the
result rows and Divine Aid for descendants (section 7 item 4) follow from p1 / p2 by the original's rules; an imported
Priest also makes Lord Wood "Dead" on his sheet by the kept code test (section 8.2 item 5).

## 9. Conflicts with the written sources (decided: `DIFFERENCES-IV.md` D4-001 / D4-002, user s81)

1. **Nikademus as an ancestor:** magazine B says a descendant of Nikademus can be made and is very strong; fan page D says
   choosing him fails ("just a joke"). The code agrees with **D**: every path with Nikademus ends in "Not possible"
   (he is ancestor-1-blocked explicitly and his `  ?` race combines with nothing). The "Sorry, the ancestor you found
   was …" message is **not** a Nikademus joke; it is the S/D race-roll message (section 3.2 step 4). **MSX2 (the magazine's
   platform) does the same `[C]`** (section 12): the same two explicit slot-2 checks and the same `combine` table. So magazine B
   contradicts both codes.
2. **"Lord Wood or Filmon as ancestor → noble birth, levelling costs no money"** (magazine C, 1991-07):
   - *Noble birth:* no such rule, on X68000 or MSX2 `[C]` (section 12). Hall ancestors change the social class only for Super
     Hero / Dark Lord persons (imports from III). Lord Wood is Royalty and Filmon Noble in the Hall, but neither is passed
     on; the descendant's social class is the random roll of section 6.
   - *Levelling costs no money:* the training cost has no social-class or ancestry term (`training.md` section 3, both
     platforms `[C]`). But each level-up pays the character social^social × level × 30 gold (`training.md` section 4), which
     for a Noble roughly covers the next training and for Royalty always exceeds it (65 535 cap vs a 25 000 maximum
     cost). So "levelling costs no money" is true **for a noble-born character** in both codes `[~]` (interpretation);
     what neither code does is make a Lord Wood / Filmon descendant noble-born.
3. **Inheritance "to some extent" + random part** (C, D): agrees (section 5.2).

## 10. The creation screens (s106) `[C]`

Read from the decompile of `_make` `0x1198c`, `_make1` `0x11c28`, `_make2` `0x136c6`, `_make3` `0x11a6e`, `_make5`
`0x13db0`, `_mkcha` `0x12a0c`, `_mopta` `0x13a7a`, `_moptb` `0x13978`, `_set_anc_social` `0x13744`, `_confirm` `0x6a8c`.
Strings are `p4x.<address>` in `GameData/phantasie4/strings/`.

**Guild roster N (`_make`)**, a loop:
1. No free guild slot (`_mkpos`: the first slot whose +01 is 0) → "The Guild roster / is full…" (`0x42935`, `0x42946`),
   wait for SPACE, back to the roster.
2. Else the question "Create the new character / as the descendant / of a mixed race?" (`0x428fe`, `0x42911`, `0x42926`)
   with the icons **E: Finish creating** (`0x4289e`), **Yes**, **No**. E → back to the roster; Yes → `_make1`; No → `_make2`.
   After either the loop starts again at step 1 (so the player can make one character after another until E).

**Plain character (`_make2` → `_mkcha`)** in the "Create Character" window (`0x42bd5`):
1. Race menu: "Race (RACE) / choose" (`0x42b7c`…); 1–5 the base races, 6 Random Creature (`0x42bc0`), 7 Back (`0x42bd0`).
   Back → the question again (nothing rolled).
2. Random Creature: `_rrace` (section 2 item 2) rolls rand(100) and shows the list numbered from 1; a pick is the race.
3. Class menu: "Class (CLASS) / choose" (`0x42b9d`…), only the race's classes (section 2 item 3), numbered from 1; no Back.
4. The five abilities are rolled (section 5.3) and the sheet is shown: Name (`?` while empty), Race, Class, the five abilities
   and the sixth value under "Luck" = the move base (section 5.4).
5. Okay / Cansel (`0x40192`, `0x401b2`; "Cansel" is the original's spelling). Cansel → back to the question; nothing is
   kept, so choosing again is the re-roll (manual p. 9: "choose again as often as you like").
6. Okay → "Choose a name" window (`0x42c1c`), prompt `:`, up to 8 characters; then `_mkdetail` (section 7), the sheet with the
   skills (`_dsp_skill`), wait for SPACE; back to the question.

**Descendant (`_make1`)**, windows "First ancestor" (`0x42a0b`) and "Second ancestor" (`0x42a14`):
1. "How will you / set this / ancestor?" (`0x429c8`, `0x429d3`, `0x429e2`) with **SPACE**, **N: Decide anew** (`0x42988`),
   **H: A person from the Hall** (`0x429a8`). SPACE → back to the question.
2. N → `_mkcha` in the First ancestor window (Random Creature lists 3 / 4, section 3.1). Back or Cansel → step 1.
3. H → "Which person / in the Hall?" (`0x429f1`, `0x429fc`): the used Hall slots listed A, B, C… (name and the first three
   letters of the race; the button's value is the Hall slot); a pick shows that person's sheet; Okay / Cansel. `_make3`
   returns `_confirm`'s result (d0 at its `rts`), so Cansel → step 1.
4. Ancestor 1 is kept (`_push_work(0)`). `_make5`: the "Create Descendant" window (`0x42d26`), "Choose this ancestor's /
   race" (`0x42cd4`, `0x42ce5`) with **SPACE**, **S: Same race**, **D: Different race**, **H: Race of a Hall person**
   (`0x42c5a`…). SPACE → step 1 (ancestor 1 is chosen again).
5. S / D (`_mopta`): rand(100), and rand(n) when the race has partners (section 3.2), are rolled **before** the Nikademus test;
   Nikademus (code) → "Not possible" (`0x42cf2`), wait, step 1. Otherwise the result is shown in the Second ancestor
   window: either "Race: <race>" or, when not what was asked, "Sorry, the ancestor / you found was / <race> / ." (`0x42cfd`,
   `0x42d10`, `0x42d1f`); wait for SPACE; then the class menu of that race (no Back) and the new ancestor's abilities.
6. H (`_moptb`): the Hall list as in step 3 ("Which person / in the Hall?", `0x42cba`, `0x42cc5`); a refused pair (section 3.2) →
   "Not possible", step 1.
7. The descendant: the class menu of the descendant's race, the inherited abilities (section 5.2), the sheet, Okay / Cansel.
   Cansel → back to the question (both ancestors are dropped).
8. Okay → `_set_anc_social`: for ancestor 1, then 2, a "new" ancestor gets kind 2, index guild slot × 2 + n, and a social
   class rolled by its race (`_get_social`, one draw each, before the name); a Hall ancestor keeps kind 1 and its slot.
   Then name, `_mkdetail`, the sheet with skills, SPACE, back to the question.

**Random draws in order:** Random Creature list roll; ability dice (15); for a descendant: ancestor 1 (list roll, 15
dice), the S / D race roll(s), ancestor 2's 15 dice, the descendant's 15 dice, the new ancestors' social rolls; then
`_mkdetail`'s social, HP, Luck and age rolls (section 7).

**Aging factor (section 7 item 10):** the race tested is guild slot 0's. `_mkpos` takes the first free slot, so slot 0 is always
filled by the time `_mkdetail` reads it (the new character itself when it is slot 0).

## 11. Open `[?]`

- The meaning of record bytes +09 (Score), +28, +2A–2E, +36's exact use, and the III class numbering behind Lord Wood's
  status (section 8).
- avg < 5.0 behaviour (section 5.2): checked at CPU level, max abilities confirmed; MSX2 not checked.
- Monster HP per-monster formula (`../FORMAT.md` section 6.4 note): checked at CPU level; X68000 and MSX2 differ.
- Emulator check of section 9 on either platform (all static so far).
- No recovered save has ancestors, so sections 3–5 are checked against the code and the magazine sample only.

## 12. MSX2 cross-check (s80) `[C]`

Read in the MSX2 player disk's code (segment `1001`, load base `0x0100`; disk layout and addresses:
`../../../msx2/phantasie4/FORMAT.md`). Static reading; no emulator.

- **Hall data:** the shipped Hall (segment `8200`) is byte-identical to X68000 `FAME` (27 of its 28 bytes per slot);
  Nikademus is slot 2, race 33 (`  ?`), class Lord, social Black Lord.
- **Ancestor 1 from the Hall** (`1001:0x10FC` → `0x120B(slot, 0)`): stores the Hall slot in `(0x8002)` (reset to −1 at
  `0x0355` when the first ancestor is "decided anew").
- **S / D** (`0x1335` → `0x1507(50, same)` / `0x1507(25, different)`, the X68000 `_mopta` thresholds): after the race
  roll, `0x1621`: if `(0x8002)` = 2 → "ふかのうです" (Not possible, `0x17CC`) and back. The "Sorry, the ancestor you found
  was …" text is at `0x1641`, on the same S/D path as X68000.
- **H** (`0x1821`): `0x1822`: if `(0x8002)` = 2 → Not possible first; then Not possible if `combine` < 0, if the same Hall
  person is picked twice, or if both are among slots 0–2; otherwise `0x120B(slot, 1)`.
- **`combine`** (`0x171E`): the same 5×5 table (`0x16EC`) and the same nine packed keys (`0x65 0x70 0x97 0xB8 0xA1 0xCA
  0xB0 0xC4 0xED` → races 24–32) with the same 4-bit key quirk. Race 33 forms no key, so Nikademus as ancestor 2 is
  also Not possible.
- **Social class** (`0x1A5A` = section 6 step 1, same thresholds; `_mkdetail` `0x1C11`–`0x1CF3`): per Hall ancestor, descendants
  +1 (while < 100), Hall social 4 (Super Hero) → descendant 7 (Royalty), 5 (Dark Lord) → 8 (Outcast); nothing for
  Royalty (Lord Wood) or Noble (Filmon). The social-class name table (segment `1006`) has the X68000 order.
- **Training and level-up gold** (segment `100C`): same as X68000, `training.md`.

No second MSX2 code build is known: the TOSEC `[a]` player disk differs only in sectors 284–311, which hold the save
set (directory ids `8000`–`8010`).

Result: on both section 9 items the MSX2 version behaves exactly like X68000. The magazine claims do not describe a
platform difference.

## 13. Lineage in I–III (remaster option, s149)

User decision s72 (final; `09-trilogy.md` Open user decisions): IV's ancestry at creation is offered in Phantasie I, II and
III as an option, **base races only**. None of I–III has it, so everything here is a remaster rule. Where it follows IV
it says which section above; where IV has no rule it is tagged `[~]`. Code: `src/OpenPhantasie.Core/LineageCreation.cs`
(`LineageRules`), `GameSession.RollDescendant`, `src/OpenPhantasie.Data/LineageHall.cs`, the Client's `GuildNewDialog`.

### 13.1 The option and the Hall list

- **Option:** the cross-game feature module `p4.lineage` (`FeatureModuleRegistry.Lineage`), listed under "from other
  games" in the I, II and III Options tabs. Off in every preset (a module that is not a game's own is never in a preset;
  switching it on makes the save's rules Custom). It is not a switch in IV, whose creation always has ancestors
  (`BuiltIntoHome`: IV's module list and presets are unchanged). Off = the game's own Guild New, unchanged.
- **The Hall list** `[~]` (IV's Hall exists only in IV): built each time Guild New asks for a descendant, read-only, no
  file of its own. In order:
  1. IV's shipped Hall persons with a base race (`rule-tables.json` `hall`): Lord Wood (Human Ranger) and Filmon (Human
     Sage). Nikademus (race "?") never.
  2. The characters on the remaster's saves of the **other** games (`savegame-p1/p2/p3.json`, read as the transfers read
     them), I then II then III, by Guild slot. The game's own roster is not on its list.
  3. The IV save's Hall imports (slots 3–10, section 8.2), when there is an IV save.
  A person whose race is not a base race (Human … Minotaur) is left out: an Undead, IV's "?". A missing, old or unreadable
  save adds nothing. A III character imported into IV's Hall can be listed twice (from the III save and from the IV
  Hall); the two are separate records.
- Nothing is written back: no descendant count is kept for a Hall person used in I–III (IV's +1 is IV's own record,
  section 7 item 4) `[~]`.

### 13.2 The pair

Each ancestor is either **new** or a **person** from the list (IV's N / H for ancestor 1, section 3.1).

1. Both persons: they must be the **same race**; that is the descendant's race. A mixed pair is refused ("the races
   differ"). No `combine` (section 4): no hybrids, so I–III need no new race data.
2. One person and one new: the new ancestor is of the person's race (IV's S always keeping the race, section 3.2,
   without the roll: base races only means S's "same race" can never fail) `[~]`.
3. Both new: the player picks the race from the game's race menu, Human … Gnome; no RANDOM there (other races come from
   a person ancestor) `[~]`.
4. The race must be one the game's Guild New can give. I and II: the five picks plus RANDOM's Pixie … Minotaur; III: the
   five plus its RANDOM list of ten. So all fifteen base races in each `[C]` (creation code).
5. IV's own refusals are kept `[C]` (section 3.2, `_moptb`): the same person as both ancestors; two of IV's shipped
   persons (slots 0–2, so Lord Wood + Filmon).
6. A refused pair says why and asks for ancestor 2 again.

### 13.3 Abilities

1. A **new** ancestor is the **target game's own plain roll** (its Guild New's five attribute draws, with the save's
   creation tables) for the descendant's race and **class** (IV lets the player pick the new ancestor's class; the
   remaster gives it the descendant's) `[~]`. Ancestor 1 is rolled before ancestor 2. Not IV's `_newattr`: IV's roll
   runs above I's (point 5), so "new + new" in I came out about +3 per ability over a plain roll and the plain roll would
   never be worth picking.
2. The five abilities are IV's `_cmbattr` exactly (section 5.2, `P4Creation.Inherit`, the same code IV runs): each
   ancestor's stored value minus IV's class and race modifiers for that ancestor, the average, the exponent table, three
   dice `6·u^k` + 3, IV's class modifier capped at 18, IV's race modifier floored at 3. A Hall person of class Sage
   (Filmon) subtracts the bytes section 5.2 names. Below avg 5.0: the table's last exponent (D4-056's fix; I–III saves
   have no `p4.weak-ancestors` option).
3. **Why IV's `_cmbattr` dice and not the game's own roll for the descendant** `[~]`: the target games' random parts have other shapes (I / II: one
   uniform `word / 1821`, 0–17; III: three d6), and IV's random part is three continuous dice skewed by `u^k`. But the
   modifier structure is the same in all four games: IV's class and race modifiers are I's / III's minus 10 (every
   cell except Gnoll DEX, I −1 vs IV −2), I's roll is `q + 3 + (class − 10)` capped 18, `+ (race − 10)`, at least 3, and
   III's `3d6 + 3 + (class − 10) + (race − 10)`, at most race + 8 (= IV's 18-cap then race), at least 3. So IV's roll lands
   on the target game's scale directly, and its own math is kept.
4. Then each value is **clamped to the target game's own range** for that race and class: I / II the lowest and highest
   value of its own formula (q = 0 and q = 17; `CharacterCreation.AttributeRange`, with the save's creation tables, e.g.
   D-001's Halfling DEX); III `3 + class + race − 17 … 18 + class + race − 17`, at most race + 8, at least 3
   (`Phantasie3Campaign.ClampAttribute`).
5. Scale `[~]` (a side effect for the user to know): IV's plain roll (k = 0.6, raw mean ≈ 14) runs above I's (raw mean
   11.5) and a little above III's (13.5); that is why a new ancestor uses the game's own roll (point 1). Measured with IV's
   `_newattr` for new ancestors (300 Human Fighters, sum of the five): I plain 57.0, descendant of two plain I characters 60.5, two IV-new
   ancestors 73.3; III plain 66.6, two plain III characters 71.1, two IV-new ancestors 72.7. With the game's own roll a
   "new + new" descendant is a descendant of two plain characters of that game: measured with the game's own roll
   (`NewPlusNew_StaysNearTheGamesPlainRoll`, 300 Human Fighters) the five-sum is +4.2 over a plain roll in I and +4.7 in
   III (about +0.9 per ability, from `_cmbattr`'s dice).

### 13.4 The rest of creation, and the record

- Everything else is the target game's own Guild New in its own draw order: I / II Luck, HP, skills, spells, lifespan,
  age, kit; III social class and gold, Luck, HP, age, kit, skills, body areas. Only the five attribute draws are not
  made. Class rules are the game's own (any class for any race in I–III). The descendant's social class is the game's
  own roll (IV's Super Hero / Dark Lord rule, section 6 step 2, has no Hall social class to read in I–III) `[~]`. No
  Divine Aid from a Hall ancestor (section 7 item 4 is IV's spell list) `[~]`.
- Draw order `[~]`: new ancestor 1's plain roll (the game's whole Guild New roll, of which only the five abilities are
  used), new ancestor 2's, the descendant's 15 dice, then the game's own creation draws.
- **`Character.Lineage`** holds both ancestors, ancestor 1 first, as `ParentRef(RaceId, ClassId, Name)`: a person by its
  own game's ids (`p4:0` / `p4:2` "Lord Wood"; `p3:11` / `p3:3` for a III Orc Fighter) and its name; a new ancestor by the
  target game's ids for the descendant's race and class and no name (IV keeps none, section 7 item 2). The remaster saves
  of I, II and III keep it (`GuildCodec` JSON, tested per game); the original platforms' save formats have no place for
  it, so a DOS / ST export drops it (as before).

### 13.5 The screens (Client `GuildNewDialog`, all three games)

With the option on, Guild New first asks "1 PLAIN ROLL / 2 DESCENDANT OF TWO ANCESTORS" (IV's question, section 10).
1 = the game's own race / class / roll, unchanged. 2 = FIRST ANCESTOR, then SECOND ANCESTOR: the list's first row is
"NEW ANCESTOR", then the Hall list (name, race, class, the game it comes from), arrows / PgUp / PgDn, Return picks, Esc
goes back a step; eight rows show and the rest scroll (no row is dropped; the box itself fits without its own scroll bar). A refused pair shows the reason under the list.
Then the race menu (both new only), the class menu, the roll with an "ANCESTORS: …" line, Y / N, name, sheet. N goes back
to the first question with both ancestors dropped (IV's Cansel, section 10 step 7). With IV's data missing the
descendant path says so and the plain roll still works.
