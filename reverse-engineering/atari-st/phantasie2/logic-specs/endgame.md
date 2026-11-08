# Phantasie II (Atari ST): endgame — Pluto's minions, amulets, the orb, the Filmon ending, defeat (3b.7)

Phase 09 T3, task 3b.7 (`docs/plan/03b-phantasie2-re.md`). This covers **only II's changes**. I's endgame spec,
`reverse-engineering/dos/logic-specs/endgame.md`, is the reference for everything marked "as I".

Conventions:
- Addresses are `PHANT.PRG` image addresses (`../FORMAT.md` §1).
- Party record `i` = `0x25162 + 318·i`, and `@n` = byte offset n of the 318-byte record (`GUILD.DAT` layout, `FORMAT.md` §5).
- The found-item list ("sack", 60 words, `SACK.DAT`) is at `0x25eb8`.
- `r` = `rnd100 0xfd40` (0..100).
- Monster ids are **0-based** unless written "1-based" (the forced-encounter word `0x2832c` is 1-based).
- Tags: `[C]` read in the code or data, `[~]` probable, `[?]` open.

Method: s78 static reading (objdump with fixups, Ghidra 12 headless decompiles of the functions named below, and a decoder
for the II dungeon interpreter over `extracted/dungeons/dungeons.json`: `../parity/decode_p2_events.py`, output
`../parity/p2-dungeon-events.txt`). Live status: §10. s80 ran Mystic → DNG8 feeding → Filmon ending → wand use in Hatari
(`../parity/endgame-live.md`); sections marked "(live s80)" were seen there. s82 ran a minion fight with its amulet, the
dragon's ATTACK and two party defeats (`../parity/encounters-endgame-live.md`, marked "(live s82)").

## 0. Summary

- **Win** = feed the orb to Pluto's Ice Dragon in DNG8. The script op that does it is the only producer of main-loop
  mode 7 (§6).
- The dragon only lets a party approach once a member's **SCORE `@282` ≥ 100**. Only the Mystic writes that score: 20 for
  carrying the orb plus 10 per "beast rune" (§2).
- A beast rune is earned by killing one of **Pluto's 8 minions** (each drops its amulet) and then **EXAMINE**-ing that
  amulet in town (§3).
- **Ending** = Filmon the Sage's ceremony. Each surviving member gets a one-use **wand of Filmon** (item 149), then the
  game goes on in town 1 (§7). There is no game-over screen, as in I.
- **Lose** = I's party defeat (Astral Plane, Great Demon, the same fate odds). Survivors now wake at a different place
  (§8).
- **Origin `@284`** only affects text (§9).

## 1. Record fields II adds `[C]`

| Field | Meaning | Written by | Read by |
|---|---|---|---|
| `@282` SCORE | quest progress: 0 → ≤ 100 (Mystic) → 200 (orb destroyed) → 250 (wand of Filmon used) | creation := 0 (`0xcbda`); Mystic raise (`0xd934`); DNG8 script SET 200; wand use 250 (`0xe2ae`) | Mystic rank (`> 150`); DNG1 oracle and DNG8 dragon TESTs (script field 8); wand-use gate (`== 200`); sheet `SCORE %7u` |
| `@284` ORIGIN | 0 GELNOR, 1 FERRONRAH, 2 NETHERWORLD | creation (§9); transfer := 0 | text only (§9) |
| `@286 + 2(k−1)`, k = 1..8 | **minion k killed** while this member was in the party | combat victory (§3.1) | encounter start (§3.1); amulet EXAMINE (§3.2) |
| `@302 + 2(k−1)` | **beast rune k** (sheet "BEAST RUNES: 1 2 … 8") | amulet EXAMINE (§3.2) | Mystic (§2); sheet / party line / printout |

- Creation clears both arrays (`0xcc00..0xcc22`). With them, all 36 new bytes (282–317) have a known use.
- The dungeon field-id map `0x12078` gives scripts access to `@282` (field 8) and to I's fields.
- It has **no** id for `@284`–`@316`, so dungeon scripts cannot read or set origin, minion flags or beast runes `[C]`.
- New in the map vs DOS: 8 → `@282`. The rest is as DOS `0x1ba5`: 42 = `@70` (the Phantasie I score), 43–47 runes,
  55 = `@90`, 61–120 spell flags, 152–160 items, 161 race.

## 2. The Mystic: score and rank (`score_check 0xd798`, text `0x0f64`) `[C]` (live s80)

This replaces I's ring score (`town-menus.md` §Mystic). It runs over the party members with status OK (`@24` = 1):
```
orb      := any OK member carries item 150 (---ORB---) in @222..@238     # carried, not in the sack
hero5    := any OK member has SCORE @282 > 150
gelnor   := any OK member has @70 > 99                                   # the Phantasie I score: "wand of Nik" flag
rune[k]  := any OK member has @302+2(k-1) = 1, k = 1..8
s        := 20·orb + 10·Σ rune[k]                                        # 0..100
rank     := 5·hero5 + (s > 10) + (s > 60) + (s > 90)
if s > 50 and not orb:  rank := 4
rank     := min(rank, 5)
show "Your score is %4d" (s) + rank text; + "[original text]" if gelnor
for every party member (any status): SCORE @282 := max(@282, s)
```
Rank texts (`0x1f342`): 0 "[original text].", 1 "… [original text]", 2 "… not ready to destroy the
orb.", 3 "… [original text]", 4 "**You need** [original text]", 5 "… heroes of Ferronrah.".

- s = 100 needs the orb carried **and** all 8 runes. That is the only way to reach the dragon's SCORE ≥ 100 gate (§6),
  because no script raises `@282` below 200.
- The Mystic only **raises** SCORE, and it writes s to every party member, dead ones included. So one visit with s = 100
  qualifies the whole party.
- Rank 3 ("ready to destroy the orb") needs s > 90, which means s = 100.

## 3. Pluto's minions and the amulets (items 141–148)

### 3.1 Minion fights (`encounter_start 0x9bf4`, `combat_victory 0xa290`) `[C]` (live s82: minion 3)

- **Which monsters.** A first monster group whose reaction modifier (+0x2a) is below −10 is **minion k = −10 − value**:

| k | Monster (0-based) | Beast (scroll K) | Where it is met |
|---|---|---|---|
| 1 | 17 PLUTO'S GIANT CONST. | I | random encounters on terrain digit 1 (`0xfe38`: 0-based 12T + ⌊r/10⌋ + 2 or a PAIR pick, `encounters.md` §4) `[C]` |
| 2 | 43 PLUTO'S WINGED DEVIL | II | random encounters on terrain digit 3, and DNG4 wandering cells 13 / 15 (`encounters.md` §4–5) `[C]` |
| 3 | 72 PLUTO'S UNDEAD SNAKE | III | fixed fight OUT2 (20,10) (tile 97, arg 72) |
| 4 | 73 PLUTO'S DEMON-FLY | IV | fixed fight OUT5 (3,10) (tile 97, arg 73) |
| 5 | 74 PLUTO'S WYVERN | V | DIVINE II in region 26 (`magic-divine.md` §2) |
| 6 | 75 PLUTO'S SNAKE-DRAGON | VI | DIVINE II in region 26 |
| 7 | 76 PLUTO'S GIANT WYVERN | VII | DNG7 event 17 at (3,12), one-shot ambush; also DIVINE II when its beast draw r₄ = 100 (FFP, s83) |
| 8 | 77 PLUTO'S DRAGON-DEMON | VIII | DNG8 forced-fight cells `0x85` at (6,33), (10,36) |

  - PLUTO'S ICE DRAGON (78, beast IX) and PLUTO (79) have modifier −10, so they are not minions and drop no amulet.
  - Scroll K's placement matches the table: III/IV "small desert islands", V/VI "the astral plane … summoned by a powerful
    magic spell", VII/VIII "pets somewhere in the Netherworld".
- **Encounter start.**
  - If **any** party member has `@286+2(k−1)` = 1, the game prints "AN ENCOUNTER". The fight becomes 1-based monster
    ⌊r·36/100⌋ (an ordinary 0–36 id), surprise flags 1/1, and the encounter restarts (state 11). So **each minion is fought
    only until it has been killed once** with a given member present.
  - Otherwise it prints "[original text]" and the fight runs normally.
  - r < 3 gives 1-based id 0, which the builder turns into SKELETON (0-based 0; `0xa742..0xa756`, `encounters.md` §3) `[C]`
    (live s82: three SKELETON groups; another swap gave 1-based 7 = VIPER; F, H = 1, 1).
- **Victory.** The minion counts as killed only if group 0's live count word `0x25cd0` = −1, i.e. the whole group died.
  - **A DISSOLVE win does not count** (live s82): "[original text]" (`0x835e`) ends the fight with `0x2832e` = 2
    and leaves the count words alone, so no flag and no amulet. DISPELL UNDEAD's dissolve / run uses the same exit
    (`0x8378`, `0x84f0`). A FEAR win does not exist on the ST: FEAR makes no monster run (`encounters.md` §6).
  Then, after "[original text] … XP AND … GP":
  1. One box: "[original text]" above "`<first non-DEAD member>` TAKES AN AMULET" (`0xa272(0x21619, buf)`;
     live s82: "GLADE TAKES AN AMULET").
  2. Item 140 + k (**AMULET k**) goes into the **first free sack slot**, with no "KEEP IT?" question. If the sack is full
     (60 slots), the amulet is lost `[C]`.
  3. `@286+2(k−1)` := 1 for **every** party member, dead ones included.
- **Killing PLUTO** (1-based 80, DNG7 event 24, the statue at (30,26)) with all monsters dead shows "You hear a booming
  voice: [original text] [original text] form …'" (20 lines, `pluto_voice 0xad9a`,
  table `0x214f2`). It sets nothing and is not the win `[C]`.

### 3.2 Using an amulet: beast runes (`use_item 0xdfcc` → `0x2ab0`) `[C]` (EXAMINE live s80)

- Town menu → Use item → member → item 141–148 opens dialog 18: **EXAMINE / WEAR / LEAVE** (`PHANT.RSC` tree 18).
  - **EXAMINE**: "YOU ARE CHANGED". For every party member with `@286+2j` = 1 (j = item − 141), set `@302+2j` := 1 = beast
    rune j+1. A member who was not in the party at that minion's death gets nothing.
  - **WEAR**: "LOOKS NICE !". No effect, and the "WHO WILL USE" loop runs again.
  - **LEAVE**: no effect.
- The amulet is **not used up**. One examine per amulet is enough for the whole party.
- The manual's "amulets … are the important artifacts" (p.27) fits. The princess's "studied the amulets of Pluto's other
  beasts and thus gained the eight beast runes" (DNG6 message 68) names this step.

## 4. The orb (item 150) `[C]`

- **Where.** DNG5 event 9, a pedestal at (16,33), one-shot: "[original text] [original text]" →
  GIVE 150 ("YOU FIND ---ORB---. KEEP IT?" → sack).
  - DNG5 is the imp caves `[~]`: OUT17 (20,5), east of Deathport (scroll F).
  - Hints from the same level: freeing the halfling (event 1) gives "THE ORB IS HIDDEN [original text]
    THE SECRET WORDS ARE 'NAIMON SHAIMON'"; Pelzer (event 2) gives SCROLL N; message 105 "SEEK THE BLUE AREA OR THE RED
    AREA …".
  - Event 4/8 pools teleport by class/`@240` tests.
- **What it does.** Nothing when used ("UNUSABLE": `use_item` rejects ids > 149). It only counts in the Mystic's +20 while an
  OK member **carries** it. Found items sit in the sack until handed out, as in I.
- **Sheet / summary.** Party-items box tree 23 shows "- THE ORB -", "- FILMON WAND -" and "- WAND OF NIK -" (the I score flag).

## 5. The II dungeon interpreter (`dungeon_event 0x11ab6`) — what changed vs I `[C]`

I's format and most ops are kept (`dos/logic-specs/endgame.md` §2.1). The II changes the quest depends on:

| Change | I (DOS OV2 `0x24b2`) | II |
|---|---|---|
| ops per run | 6 | **17** (`0x12034`) |
| op 18 | SET-MIN (raise to b) | **SET** field a := b, non-DEAD members (`0x1241c`; field 53 writes 0xff into XP's low byte `@269`) |
| op 19 TEST fail | push back, show message c, stop | push back (`0x110f0`), then **jump to script c**. It goes on only if the TEST op had the continue bit |
| op 20 | no-op | **JUMP** to script a |
| op 21 | no-op | **RANDJUMP**: if r < a, jump to script b (4-byte op) |
| op 22 | no-op | **GOTO EVENT** a: restart with event record a (its message and menu) |
| op 23 | no-op | **ENDING**: return state 7 (§6) |
| spent 4-byte one-shot op | becomes a 2-byte no-op (I's stale-byte quirk) | becomes `0xc0`/`0`; `0xc0` skips 4 bytes (`0x12028`), so **I's quirk is gone** |
| field ids | — | adds 8 = SCORE `@282` (§1) |

Ops 1–17 are as I: damage, fight, give, gold, message, move, set cell, trap, ambush. The other II levels' content (DNG1–8)
is level data, not endgame, and is not specced here.

## 6. Win: DNG8, Pluto's Ice Dragon `[C]` (live s80; ATTACK s82; the kill s83)

DNG8 is entered from OUT30 (15,5) at (19,0). Its message 105: "PRESS THE NUMBERS OF THE BEASTS AND YOU WILL COME TO THE ICE
DRAGON CAVE". The dragon is event 1 at (32,33) (repeatable): "[original text] …" LEAVE / APPROACH.
- **LEAVE** → moved back to (32,32).
- **APPROACH** →
  1. TEST SCORE ≥ 100 (any member, DEAD included). On fail: "[original text]", pushed back to
     (32,32).
  2. TEST SCORE ≥ 101. On fail (nobody past 100, so the orb is not destroyed yet) → event 2. On pass: "THE ORB HAS BEEN
     DESTROYED ALREADY …".
- **Event 2**: ATTACK / TALK / FEED IT.
  - **ATTACK**: the cell becomes `0x8e` (a plain message cell), then a fight with ICE DRAGON (78). Killing it wins nothing
    (live s83: an ordinary victory, "[original text] 50000 XP AND 0 GP" for 6 members, its two treasure items, no
    ending, SCORE unchanged, (32,33) "ICE CAVERN" only; its defense 160 makes melee miss, a TOSS hits).
    The event is gone for this load of DNG8, so **attacking soft-locks the win until DNG8 is reloaded** from the file
    (live s82): the cell is 0x8e before the fight starts; the fight is ICE DRAGON (reaction −10, no minion). A map save
    on exit writes `DNGX.SAV` with the 0x8e cell and the next entry has no dragon; NO on a later exit writes nothing, so the
    same slot loads again and the dragon stays gone; only when the slot holds another dungeon (or nothing) does DNG8 load
    from its file with the dragon. I's slot rule (`DIFFERENCES.md` D-027; II: D2-041).
  - **TALK**: random lines (r < 20, < 25, < 33, < 50 chains), then back to event 2.
  - **FEED IT** → event 3.
- **Event 3**: GOLD / THE ORB / MEMBER. GOLD and MEMBER give one line each, then back to event 2.
  - **THE ORB**:
    1. "THE DRAGON APPROACHES THE ORB … GRINDS IT UP" and "… BY DESTROY[original text] THAT HAS
       POSESSED ME." HE FLIES AWAY.
    2. **SET SCORE := 200** for every non-DEAD member present.
    3. Cell (32,33) := `0x8e`.
    4. **ENDING** → state 7.

**Quirk: the orb is never checked or taken** `[C]` (live s80: a party with SCORE 100 and no orb gets the ending). No TEST reads item fields 152–160 here, and nothing removes item 150.
- The real gate is SCORE ≥ 100. Once a Mystic visit has written 100, the party can win **without carrying the orb**
  (sold or dropped after the visit).
- The orb stays in the inventory after the win.
- A member who was DEAD at the feeding keeps SCORE 100 (no wand-use gate, §7).

**Producers of state 7** `[C]`: only op 23. Of the eight levels, only DNG8 event 3 option 2 contains it. This replaces I's
"Black Lord killed" test (I's `0x49` check is not in II's `combat_victory`).

## 7. The ending: Filmon (`endgame_filmon 0x12f2a`, main-loop mode 7) `[C]` (live s80)

Mode 7 removes the VBL colour cycling (`0x137e6`), frees the dungeon buffers (`0x102f4`) and runs `0x12f2a`:
1. Ten lines (`0x231fc`): "[original text] [original text] greets You and tells You
   that You [original text] Filmon tells You that since You have destroyed the orb the curse of Nikademus will
   slowly fade from the land. He leads You into a small room [original text]" Then a click.
2. Loads `final2.pic` and `crowd.pic`, fades in. "F i l m o n s p e a k s :" / "[original text]".
3. It shows a title `%s - the %s from %s` for **party records 0 … n − 1, where n = the number of members with status OK**
   (`0x12a8a(1)` counts them into `[0x24bec]`; the loop `0x131a0..0x13288` prints record `d7`). So a DEAD member is left
   out only when it stands behind every OK member; a DEAD member in the middle would get a title and the last OK member
   none `[C]` static (live s80: dead FATHER JO in the last slot, 5 titles for 5 OK members). Each title has:
   - the name;
   - a random word from `magnificent, mighty, courageous, powerful, bold, daring, gallant, avesome [sic], valiant, superb`
     (`rand() / 0xce4`, 0–9);
   - the ORIGIN name (§9).
   Each title is followed by the crowd cheering (crowd frames 0/1 swapped 10 times, 330 ms apart) and sound 32 (§7.1).
4. "[original text] [original text] can only be used once."
5. **Rewards and save.**
   1. Find the first free sack slot (capped at 54).
   2. For each member: copy the party record to its roster slot (`@92 − 1`). If the member's status is OK, put **item 149
      FLMN WAND** in the next sack slot.
   3. Write `guild.dat` (7950 B) and `sack.dat` (120 B).
   - `[~]` With a nearly full sack the wands overwrite slots 54–59.
   - Unlike I's Zeus grant, nothing is set *after* the roster copy, so I's "DIVINE I missing from guild.dat" quirk has
     no II counterpart.
6. Reloads `mstr2.pat`. Sets current town `0x25ea0` := 1, town `0x25b94` := 1, region `0x25b92` := 1.
7. Twenty-one closing lines (`0x23244`): "Filmon addresses the crowd … Nikademus and the full force of his army are at this
   minute marching on the great continent to the north … [original text] and final installment … Filmon
   teleports the party. The end."
8. Fades out and returns 1 → the main loop enters **the town loop of town 1 (PIPPACOTT)** `[C]` (live s80).
   - **The game continues**: no game-over, no credits screen, no win flag beyond SCORE 200. The same as I.
   - The text sets up Phantasie III (T5 transfer: SCORE and the wand may matter).

**After the ending** `[C]`:
- **Wand of Filmon** (Town → Use item, item 149). It works only for a member whose SCORE is exactly 200; anyone else gets
  "UNUSABLE".
  - The wand is removed and SCORE := 250. Alert "You are improved".
  - Each of STR, INT, DEX, CON, CHA becomes `bump(stat, LUCK @10)` (`0x2bc8`): +1 if < 9, +1 more if LUCK > 11 and the
    stat < 18, then +1 if < 20. So each stat gains +1 to +3, capped near 20.
- The Mystic's rank 5 "heroes of Ferronrah" follows from SCORE > 150.
- The DNG1 **kobold oracle** (event 11, "DROP YOUR GOLD AND WALK INTO THE SMOKE") takes all the party's gold, does smoke
  damage, then advises by SCORE stage:
  - < 100: "go to the Netherworld, find the Book of Beasts and bring it back to Filmon";
  - < 200: "take the orb … feed it to Pluto's Ice Dragon";
  - < 250: "those given the wand of Filmon should use it on yourselves";
  - else: "LEAVE FERRONRAH AND SEEK OUT NEW ADVENTURES …".

**Other quest-gate facts from the scripts** `[C]`:
- Filmon in DNG4 (event 17) and Pluto's wall text in DNG8 (event 18) branch on the **Phantasie I score `@70`**: ≥ 100
  "congratulations on defeating the Black Lord"; ≥ 1 "you could not defeat the Black Lord"; 0 for II-native characters.
  - Filmon's "fight with sticks" test sets MP `@18` := 0, `@240` := 1 and worn armor/weapon to none / STICK (41), gives
    SCROLL H, and fights a KING COBRA.
- **Pluto's castle** (DNG7, OUT28): event 3 lets the party in only if some member has race `@256` ≥ 15 (UNDEAD) ("YOU ARE
  ALLOWED TO ENTER UNDEAD SERVANTS OF PLUTO"; scroll H).
- In II the UNDEAD state is reached through the Great Demon (§8) or by transfer.

### 7.1 The ceremony screen, step by step (s128, static `[C]`; checked against the s80 shots `[C]` live)

`0x12f2a` in order (all waits named; "line" = `0x12a16`, see below):
1. `[0x1fefa]` (the Speed menu: fast 5 / normal 10 / slow 1000, FORMAT.md 7.2) saved and set to 20, restored at the
   end `[C]` (s135): of its readers only the sound wait `0xb8f2` is reachable from here, so every sound waits for the one
   before it to end whatever Speed was chosen (the ceremony's own waits are fixed: 3000, 2000, 330 ms, 10 Vsyncs).
2. **Intro page.** Logbase kept; the screen cleared (`0x19af6`); text style `0x2c0c` (VDI pen 3, italic, height 6 =
   the 8×8 font, left / top alignment; it also fills the screen black). The ten lines `0x231fc` by `v_gtext` at
   (2, 50 + 10i), i = 0…9; the mouse on; `0xae1a`: GEM form 18 (the OK button; s158 read from OUT.RSC tree 18 `[C]`:
   a G_BOX root 9 × 3 cells = 72 × 24 px, OUTLINED, and a G_BOXTEXT "OK" at cell (2, 1), 5 × 1 cells, SELECTABLE /
   DEFAULT / EXIT, centred, border 1 px outside, `te_font` 5 = the small font; so (247, 175)…(318, 198) and the button
   262…303 × 182…191 with its border, as the remaster draws it from the s80 shots) at the bottom right (319 − w, 199 − h),
   `form_do` until OK. The palette is the one the dungeon left: VDI 3 = register 2 of `map` (`#249100`; live shot
   `p2-endgame-filmon-1.png` (34,136,0) = `0x140`) and the button in register 15 (red) `[C]` live. s162: the whole page
   is `map` (DNG8's map view sets it, `0x5cea`; the colour cycle runs on the overworld only, FORMAT.md 10.1, and mode 7
   removes it), and the page shows only registers 0 (the fill), 2 and 15 `[C]`.
3. The mouse off; `final2.pic` opened for 2 bytes (existence check); the screen cleared; `0x19abc`: when `[0x23f76]`
   (data value 1) is set, all 16 registers := 0 (black). `final2.pic` read to the screen (25600 B: rows 0…159);
   `crowd.pic` (11160 B) into the `MSTR2` buffer `[0x24f34]` (3 frames of 235×31, 3720 B each).
4. Text style for the ceremony: VDI pen 5 (= register 6, green `#00FF00` in `combat`), effects 0 (plain), height 6,
   alignment centre / top, writing mode replace. `0x12a8a(1)`: the row (n = OK members, gap (320 − 32n)/(n + 1),
   y 158; a slot's box is the place of the next OK member, as I's `+0x36e(1)`). Slot rect 6 := (150, 75, 32, 32): the
   **door spot** (the figure stands in Filmon's doorway).
5. **Fade-in:** register k = 0…15 := `combat[k]` (XBIOS 7 Setcolor), then 10 Vsync (XBIOS 37) after each.
6. Line "F i l m o n    s p e a k s :", line "[original text]", then `evnt_timer(2000)` (2 s, no key).
7. **Per record d7 = 0 … n − 1** (n = OK members, so the same quirk as the titles, §7 step 3):
   1. `0x12e78(d7, 6)`: d7's idle portrait (`PARTY1`, race `@256`) on the door spot.
   2. Line: the title (`0x1298e`, "%s - the %s from %s").
   3. `0x12e78(d7 + 10, 6)`: the alternate pose (`PARTY2`) on the door spot.
   4. Sound 32 (`0xb80e(32)` → Dosound of table `0x24a8e[32]` = `0x21db8`, FORMAT.md 7.2: a noise swell and fade,
      207 ticks = 4.14 s). Dosound does not block: the crowd frames run over it; the next member's sound 32 waits for
      this one to end (`0xb8f2`, step 1), which can only bite when keys cut the 3000 ms lines short.
   5. Ten times: crowd frame f (f = 0, 1, 0, 1, …) blitted (`vro_cpyfm` replace) at (40, 127), clip (40, 127, 235, 31);
      `evnt_timer(330)` (330 ms, no key).
   6. `0x12e78(d7, d7)`: the idle portrait in row slot d7; crowd frame 2 at (40, 127).
8. The door spot cleared to pen 0 (`0x19b28`: `vro_cpyfm` mode 0 on (150, 75, 32, 32)). Lines "F i l m o n    s p e a k
   s :", `0x23238` "[original text]", `0x2323c` "[original text]", `0x23240` "can only
   be used once ." (the copies at `0x23228`…`0x23230`, one with "greaat", are never used `[C]`).
9. Rewards and save (§7 step 5), then line " " (the bottom line blanked, 3 s or a key).
10. `guild.dat`, `sack.dat` written; `mstr2.pat` reloaded; town / region := 1.
11. **Closing page:** `0x2c0c` (screen black, italic, left / top), VDI pen 4 (register 4, `#FFB600` in `combat`; live
    (238,170,0) = `0x750`), the 21 lines `0x23244` by `v_gtext` at (2, 4 + 8i) (8 = the character height
    `[0x24e9a]`); the mouse on; the OK form (button in register 15, white) until OK.
12. **Fade-out:** register k = 15 … 0 := 0, 10 Vsync after each. Return 1 → the town loop.

- **Line** (`0x12a16(text)`): rect (0, 190, 320, 10) cleared, then `v_justified(160, 192, text, 310, word on, char on)`:
  the text centred on x 160 and stretched to 310 px (x 5 … 315); then `0x16cbe(3000)`: `evnt_multi` timer 3000 ms,
  ended early by a key or a click. The line stays until the next one. **Spacing** (s160, read from EmuTOS 1.4
  `vdi/vdi_text.c` `vdi_v_justified` / `output_text`, integer arithmetic `[C]` against the shot): natural width w (8 px a
  character), s spaces, n characters, max 310; each space gets (310 − w) / s more, capped at ± max_cell_width / 2 = 4
  (the cap drops the remainder, else the first (310 − w) mod s spaces get 1 more); then w' = w + that; each character
  gap gets (310 − w') / (n − 1) and the first (310 − w') mod (n − 1) gaps 1 more (negative for a line wider than 310:
  it closes up); the text starts at 160 − 310 / 2 = 5. The s80 title shot ("KLEPTO - the bold from Ferronrah": 54 px
  extra, +4 per space, 34 over 31 gaps = 1 and 3 more) puts every glyph where this says: x 5, 15, 25, 35, 44, 53, 75 …
  (`P2StFontTests`).
- **Fonts** (s160, user s159): TOS's 8×8 is not on the disks; the remaster draws EmuTOS's 8×8 system font
  (`GameData/phantasie2/graphics/fonts/emutos-st-8x8.json`, GPL-2.0+, `tools/scripts/emutos_st_font.py`), the pages in
  its VDI italic (F_SKEW: skew mask 0x5555 rotated once per row from the bottom, rows 0…7 4, 3, 3, 2, 2, 1, 1, 0 px
  right; `emutos-st-8x8-italic.json`). Top-aligned skewed `v_gtext` starts the cell left_offset + right_offset = 4 px left
  of its x (`vdi_text.c`, vertical alignment 5): pack `font_dx` −4. Both pages match the s80 shots pixel for pixel
  (`p2-endgame-filmon-1.png`, `p2-endgame-closing.png`; the closing page also on a s160 Xvfb shot of the Client).
- **Sounds:** sound 32 once per presented member, and (s135) the OK form's click after each page: `0xae1a` ends with
  `0xb7e6` = Dosound(`0x21ab0`) (channel A on the triangle envelope, 7 ticks; `0xb7e6` has 24 callers, `0xae1a` is one). The I Amiga ending's sounds are not in II.
- **Remaster (s128):** Core `Phantasie2Campaign.ResolveEnding` puts the screen in `GameSession.PendingVictoryScene`
  (`VictoryScene` with `IntroPage`, `HeaderLines`, `Titles`, `SpeechLines` = the wand lines and the blank line,
  `ClosingPage`); with the ST pack's `ending_scene` the Client runs I's `VictoryScreen` with II's steps (door spot,
  crowd, pages); without one the prompt box shows every line and waits for a key. s135: sound 32 plays with each cheer
  (pack `sound_effects` format `atari-st-dosound` = PHANT.PRG's table, `ending_scene.crowd.sound` 32; Sound switch on)
  and the click when OK closes a page (pack number 33 = `0x21ab0`, the pages' `sound`), neither waited for. s163: the
  next sound 32 waits for the last (step 7.4's `0xb8f2`), the other pose on the door spot meanwhile
  (`VictoryScreen` CheerWait, the sound's length from the pack's script); seen live on Xvfb (s163, keys every ~0.75 s:
  before the change one 32 started 3.85 s after the last, inside its 4.14 s; `P2VictoryScreenTests.CheerSound_TheNext32WaitsForTheLast_WhenKeysCutTheTitles`). s136 live (Xvfb, the Client's own null sink, `--test-party --test-game 2 --p2-victory`): the click
  after Return on the intro page (0.5 s burst) and sound 32 at each cheer (14.5 s, 21 s, 27.5 s, each a ~4 s swell
  and fade in the recording) are heard; the s135 "not heard" does not come back (the code path is unchanged).
  s160: the pages in EmuTOS's 8×8 italic, the bottom lines in its plain 8×8 with `v_justified`'s own spacing (above);
  s162: the OK button's label in EmuTOS's 6×6 small font (`te_font` 5: `GameData/phantasie2/graphics/fonts/
  emutos-st-6x6.json`, GPL-2.0+, `emutos_st_font.py`; the pages' `button_font`), centred in the G_BOXTEXT inside the
  1-px border with its cell's top centred in the 8-px row: the O at (277, 184) and the K at (283, 184) as the s80
  shot, pixel for pixel (`P2StFontTests`).

## 8. Lose: party defeat (mode 6 → `0xfe18` → `0x12678`; screens `0x12bea`) `[C]` (live s82)

As I (`dos/logic-specs/endgame.md` §4):
- "[original text]" → "[original text]" → "[original text]" (live
  s82: typed on the `DECOR.PIC` temple, one ghost per member, the door opens, the demon's face appears, then one
  "`%s` YOU HAVE BEEN `%s`" line per member).
- The same fate rule per member: CON `@6` = 0 → DESTROYED; L > 2 and `rand/0x2000 < L − 2` → RESURRECTED; `rand/0x2aab <
  L` → UNDEAD (race 15, level 20, `@240` := 15); else DESTROYED.
- The same free-slot list for destroyed members.

Changes vs I:
- The UNDEAD line reads "`%s` YOU HAVE BEEN **[original text]**" (I: "[original text]"). All three use
  `%s YOU HAVE BEEN %s` (`0x23910`).
- **Survivors wake at region 18, x = 25 − 7 = 18, y = 14** (`0x25ea2` holds 25 − x, as the main loop's `0x19 − [0x25ea2]`
  shows). That cell, OUT18 (18,14), is the "[original text] [original text]" text cell (tile 116, which
  DEMONIAC's town exit also uses). I wakes survivors at OUT1 (20,5). Live s82: region 18, (18,14), party compacted to the
  survivors, HP max.
- `0x12678` clears the XP pool `0x25eb2`, the found gold `0x25eb4` and `0x25eb0`, and sets town := 1. **The found items
  `0x25eb8` are kept** (live s82: AMULET 3 stayed in the sack). I clears them. The manual is silent, so the ST rule stands.
- The fate loop runs `d7 = 0 … [0x2831c]` inclusive, and main-loop mode 6 has just turned the party count into the
  **last index** (`subqw #1, 0x2831c` at `0x74d2`, undone at `0x74e4`). So it judges exactly the members; its "6 → 5" cut
  drops a 7th slot (the elemental), as I removes slot 7 `[C]` (live s83, three members: three fate lines, one free-slot
  entry). s82's reading "one record past the party" was wrong.
- The lines (live s83): "GLADE YOU HAVE BEEN RESURRECTED", "YOSHI YOU HAVE BEEN [original text]", "FOREST YOU HAVE BEEN
  DESTROYED", in party order; a revived member's ghost becomes its portrait.
- With no survivor: return 1 → the town loop of town 1, **PIPPACOTT** (live s82).
- A defeat counter `0x231fa` (per program run) draws an extra picture block (`0x15fa4`, 48×40 at (139,39)) on the **5th**
  defeat only (`0x12de4`: increment, then `== 5`; live s82, counter poked to 4): as the door opens a sign **"Sorry, we've
  run out of Demons"** shows behind it for a moment, then the Great Demon appears anyway (its face covers the sign) and
  judges as usual. Cosmetic. Built s83: `PartyDefeatResult.DemonsRanOut`, the prompt page `DefeatDemonSign`, and the
  Client's DECOR scene for II (§13); the remaster counts defeats per session, not per program run `[~]` (on the ST a load
  in the same run keeps counting).
- **Sounds (s140, FORMAT.md 7.4) `[C]` static:** `0x12694` saves the Speed word and sets 20 (restored `0x127ae`), so each
  sound waits for the one before. `0x12bea`: line 1 (`0x12a16`: the bottom line, then 3000 ms or a key), the ghost row,
  **23** (`0x12d5e`), the astral line (3000 ms), **22** (`0x12d86`) then the door's 90 steps of 4 VBL, (5th defeat: the
  sign, 3000 ms), 3000 ms, **20** (`0x12e36`), the face, the demon's line; the fate loop plays **21** (`0x126d2`) before
  every member's fate, DESTROYED included. Wired s140 (pack `field_sounds.defeat_scene`, Client `P2DefeatSounds`): 22 as
  the door starts, 20 with the face, 21 with each fate line. Traced live s141 (`parity/capture_s141_sounds.md`): 23 3060
  ms after line 1, 22 3090 ms after the astral line, the door 6.87 s (413 VBL at 60 Hz, drawing included), 20 3010 ms
  after it, each fate line within 15 ms of its 21's start, both waiting for the running sound; the remaster plays 23
  3000 ms after the defeat page shows (s141).
- **The page goes on by itself (s165, FORMAT.md 7.4) `[C]`:** line 1's hold is `0x16cbe(3000)`: the timer, any key or a
  click ends it (keys pressed before are drained, `0x16d56`); nothing waits for a key. The remaster leaves the DEFEATED
  page as the hold ends (or on any key or click), with the ghost row and 23; seen live on Xvfb s165. The astral line's
  own 3000 ms before the door is not modelled `[~]`.
- Scroll M ("Departed Souls") describes this judgement in-world. The manual says nothing about it (as for I).

## 9. Origin `@284` `[C]`

- **Creation** (`create_character 0x0c90c`, called from the Guild with the current town number, 1-based; `0xed10` →
  `0xdb5a` → `0xd378`): **@284 = 2 NETHERWORLD if the town number > 6, else 1 FERRONRAH**.
  - Towns 1–6: PIPPACOTT, HASLEMERE, SAXLINGHAM, LLITHFAEN, IPSWICH, FERRON → Ferronrah.
  - Towns 7–10: BLACK MOOR, DEATHPORT, DEMONIAC, DARK ABYSS → Netherworld.
  - This fits the scrolls' geography (the impling hatchery "west of Black Moor", the imp caves "east of Deathport" are
    on "the first level of the Netherworld").
- **Transfer from I**: 0 = GELNOR (`transfer.md` §2).
- **Readers**: the character sheet `ORIGIN: %s` (`0x3a9c`), the party line (`0x31ce`), the printout (`0x17572`) and the
  ending title (§7). **No rule reads it** `[C]`: every `@284` reference in the program was checked. The encounter/victory
  code reads `@284 + 2k` only with k ≥ 1, i.e. the minion flags.
- The Netherworld is not a coded "plane" flag. It is the set of map regions past the vortices; origin is a label.

## 10. Live checks

- **Done (s78, Hatari + EmuTOS, `../parity/shots/p2-autosave-off.png`, `p2-autosave-on.png`)**: the Town options menu has
  **Auto save**, unchecked on a fresh start. Choosing it adds the check mark (the `0x1f6ea` toggle `0x28ac` → `menu_icheck`
  object 22). That answers Q15 (§11).
- **Done (s80, `../parity/endgame-live.md`)**: the chain from an edited roster (all minion flags, runes 1–6, AMULET 7/8
  and the orb carried, one DEAD member) through EXAMINE (runes 7–8 to every party member, the dead one too), the Mystic
  ("Your score is 100. You are [original text]" + "heroes of Gelnor also"; SCORE 100 on every member), DNG8
  APPROACH → TALK → FEED IT → THE ORB, the Filmon ending (titles with each ORIGIN, 5 wands for 5 OK members, SCORE 200
  for the living and 100 for the dead, the orb kept), back in PIPPACOTT, the Mystic's rank 5, and two wands used
  ("You are improved", SCORE 250, stat bumps exactly as `0x2bc8` predicts). Second and third runs: "TOO TERRIFIED" at
  SCORE 0, a wand at SCORE 0 is "UNUSABLE", and a SCORE-100 party with no orb still gets the ending.
- **Done (s82, `../parity/encounters-endgame-live.md`)**: minion 3 (UNDEAD SNAKE at OUT2 (20,10)): a DISSOLVE win gives
  nothing; a kill gives the box "[original text]" / "GLADE TAKES AN AMULET", AMULET 3 in the sack and the flag
  on every member; later visits are swapped (VIPER, SKELETON ×3 from 1-based 0). ATTACK on the Ice Dragon: cell 0x8e,
  ICE DRAGON fight, the soft-lock survives a saved map and a later NO, and ends when DNG8 loads from its file. Two
  defeats: survivors (resurrected L15 / L3, an UNDEAD L1) at OUT18 (18,14) with the sack kept; no survivors → PIPPACOTT; the
  5th defeat's "Sorry, we've run out of Demons" sign.
- **Done (s83, `../parity/encounters-endgame-live.md` §8–§9)**: killing the Ice Dragon (wins nothing); the fate lines of
  a defeat with survivors (three members: one line each, nothing for the record past the party).
- **Not done live**: GOLD / MEMBER at the feeding, the DNG1 oracle.

## 11. Manual

Sources: CB p.2 story, p.10, p.14, p.27; the readme; the ST section pp.24–25.
- **p.27** "amulets and an enchanted orb are the important artifacts": agrees (§3, §4).
- **Story** "I shall free Ferronrah from Nikademus's evil power": the code's win is destroying the orb that carries
  Nikademus's curse (§6, §7: "the curse of Nikademus will slowly fade"). Nikademus is never met. This looks like story
  framing, as I's note in `dos/logic-specs/endgame.md` §5. **D2-018**: NOT A CONFLICT (s79).
- The manual says nothing about minions, beast runes, SCORE, the dragon, Filmon's ending or the defeat sequence. Manual
  silent → the code stands.
- **Q15 (autosave)** `[C]` static + live:
  - The ST has an **opt-in "Auto save"** item in the Town options menu, off at start (data word `0x1f6ea` = 0).
  - When it is on, leaving a town ("LEAVING %s", `0x2856`) writes `twns.dat` (3504 B), `guild.dat` (7950 B) and `sack.dat`
    (120 B) through `save_game 0x2732`, the same as Town options → Save game.
  - This is exactly the ST section of the rulebook ("You may also click on Auto Save to save the game each time you leave
    town", CB p.25 left column).
  - CB p.19's "automatically saved" is the **Apple** section. The suggestion is no conflict for the ST, plus a T5
    option for the other platforms' always-on behaviour. **D2-016**: NOT A CONFLICT (s79).
- **Q14** (resurrect wording): `magic-divine.md` §5 → D2-015.
- **DIVINE I** (manual p.14, "acquired in the course of the game") can't be gained in II: `magic-divine.md` §3 → D2-017.

## 12. Open `[?]`

- Answered live s82: the 1-based id 0 swap (SKELETON), DISSOLVE / FEAR wins (no amulet; FEAR wins impossible), the
  no-survivor town (PIPPACOTT), the 5th-defeat picture, the ATTACK soft-lock through `DNGX.SAV`.
- Exact sources of minions 1 and 2 (random-encounter tables, data `0x1fefe`); see `magic-divine.md` §7.
- Answered s83: the fate loop judges exactly the members (mode 6 passes the last index).

## 13. Core (s81)

`Phantasie2Campaign` (Core) plays this file:
- the Mystic (§2) and the minions and amulets (§3; EXAMINE / WEAR / LEAVE go through `GameSession.ItemUseOptions` /
  `UseCampaignItem`);
- the interpreter (§5, `Phantasie2DungeonScript`), re-read from the disassembly: the one-shot rewrite at
  `0x11ba4..0x11c00`, the field map at `0x12078`, and JUMP / RANDJUMP keep the continue rule. A GOTO EVENT onto a menu
  event waits for the player's answer;
- the ending (§7; text from the `PHANT.PRG` tables, in `phantasie2/strings/en.json`), the wand bump, DIVINE II
  (`magic-divine.md` §2), and the defeat (§8: wake at OUT18 (18,14), "[original text]").

The character's II bytes are `Character.Phantasie2` (`QuestScore`, `Origin`, `MinionKilled`, `BeastRunes`), saved in the
remaster JSON. One thing is assumed `[~]`: II's death step is I's (D-009; `0x09894` not traced). The wall texts are I's
pair, read from `OUT.RSC` trees 15 / 17 (s83 [C]). Tests: `Phantasie2CampaignTests` (the DNG8 chain on the real data, and the s80 live values for
the wand).

s82 (from the live checks): the minion box prints KILLED before TAKES; a DISSOLVE win drops no amulet (pinned); the defeat
keeps the found items (`PartyDefeat.Resolve(keepFoundItems: true)`, II only); the ATTACK rewrite and its saved-map
persistence are pinned through `DungeonSaveSlot`. The remaster's NO-purges-the-slot rule is I's D-027 decision applied to II
(D2-041, decided s82).

s83: the defeat counter (`GameSession.PartyDefeatsThisRun`, not saved) and `PartyDefeatResult.DemonsRanOut` (II, exactly
the 5th); `SessionPrompts` shows the `DefeatDemonSign` page ("[original text]") between the banner
and the judgement on that defeat only. The Client draws II's DECOR scene behind the three defeat pages (closed door, the
sign, the demon's face; `UiOverlays.DrawDefeatScene`, pictures `decor.door` / `decor.sorry-sign` / `decor.demon-face` in
the ST pack). s98 (user: never crop scenes): the scene is the whole ST screen 1:1 over the remaster's status, help and
message lines — all 155 DECOR rows (stairs and the guards' platform included), the portrait row at y 158 and the page's
text one line at a time on the bottom line (`DefeatScenePager`: Return shows the next line, on the last one it answers;
while the door sinks Return waits for the judgement; a dim `>` at the line's end marks a page that takes Return; each
slot of the row turns from ghost to its fate as that member's line shows, as the ST's fate loop redraws it); no box covers the picture (`../parity/shots/remaster-p2-defeat-banner.png`,
`remaster-p2-defeat-scene.png`, `remaster-p2-defeat-fates.png`). s95 (static reads,
`../FORMAT.md` §10.3/§10.4): the door sinks into the floor while the first page after the banner shows (90 steps of 4
VBLs, 6 s at 60 Hz (the US release on a US ST, decided s109; PAL 50 Hz: 7.2 s); the sign right after it, the face 3 s later; until the face shows, the outcome
page holds only the Astral Plane line, as the ST prints the speech and the fates after it), and the portrait row (ghost portraits `portrait.ghost`,
then each fate: cleared / race portrait / UNDEAD portrait) at the ST's y 158 (`DefeatScene`, `DefeatSceneClock`, `SessionPrompts.ShownDefeat`). In combat a member who dies carries the
ST's mark `portrait.death-mark` on its portrait for 500 ms (`CombatPresenter.HasDeathMark`); the ST then drops the dead
from its row, the remaster's strip keeps them. Tests: `DefeatSceneTests`, `PartyDefeat_II_FifthDefeatOfTheRun_ShowsTheOutOfDemonsSign`,
`PartyDefeat_II_FateLines_OnePerMember`, `Dragon_KilledAfterAttack_WinsNothing`.
