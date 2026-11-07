# Phantasie II (Atari ST) — function map of `PHANT.PRG` and diff against Phantasie I (Amiga `game`)

Phase 09 T3, task **3b.6** (`docs/plan/03b-phantasie2-re.md`). Static only: nothing here has been checked in Hatari yet.
Tags: `[C]` read directly in both binaries, `[~]` strong evidence (string, float or call-graph match; or a read of one
side only), `[?]` weak (shape match only). Addresses: II = **image addresses** (`FORMAT.md` §1: text at 0, data at
`0x1e298`); I = **`hN:0xOFF`** = hunk N of the Amiga `game` + offset (the convention of `amiga/FORMAT.md`).

Regenerate: `python3 tools/scripts/fmap_st_p2_amiga.py` (writes `00-function-map.json` next to this file) and
`python3 tools/scripts/fmap_st_p2_amiga.py --table` (prints the §8 tables). The JSON keeps the strings, callees and the full
signature diff of every function.

## 1. Method

1. **Loading.** `PHANT.PRG`: text + data cut out with `st_prg.py`, fixups applied, disassembled with
   `m68k-linux-gnu-objdump`. Amiga `game`: 240 hunks parsed (CODE/DATA/BSS, `HUNK_RELOC32`, `HUNK_SYMBOL`); every hunk
   is placed at `hunk << 16`, relocations are applied, each code hunk is disassembled on its own. The Amiga binary is a
   per-module link (one CODE/DATA/BSS triple per C file), so a hunk number names a source module.
2. **Function starts.** Every `LINK A6` plus every `jsr`/`bsr` target (plus hunk starts and the Amiga `HUNK_SYMBOL`
   stubs). II: 602 functions. I: 573, of which 403 are game modules (hunks 0–89) and the rest libc / math / `amiga.lib`.
3. **Features per function.** Strings it references (through relocated operands), the first string of any pointer
   table it references, float constants (II: Motorola FFP pushed as immediates; I: IEEE doubles loaded into `d2:d3`),
   immediates, record displacements `aN@(d)`, callees, GEMDOS/XBIOS numbers.
4. **Matching.** (a) anchors: functions sharing rare string tokens (IDF-weighted) or float constants; (b) propagation:
   callees/callers of matched pairs and gaps between anchors inside one Amiga module, scored by a similarity of the
   features above; (c) **14 pairs fixed by reading both sides** (training, level-up, combat round, combat menu,
   lava/swim, overworld step, field spells, sheet, main loop, encounter start/victory, monster builder, spell picker,
   ending). 276 of II's 393 game functions are paired.
5. **How status is decided.** The two programs come from different compilers (DRI C with 16-bit `int` and FFP on the
   ST; an unidentified Amiga compiler with 32-bit `int` and IEEE doubles), so instruction bytes never agree.
   A pair is **same** when its *normalised signature* agrees: the same string tokens (GEM `[n][…][…]` wrappers and
   case removed), the same float values, the same set of constants and displacements (ignoring 0–4, the record
   stride 282/318, `0xffff` masks and arguments of OS / GEM / Amiga-library calls), and no matched callee that the
   partner does not call. Anything else is **changed**, with the differing parts listed (`text`, `float`, `const`,
   `calls`). **new** = no I partner; **runtime** = libc / OS / GEM glue. "same" is therefore `[~]` at best; a "changed"
   that only differs in `const` is often compiler or platform noise (UI coordinates, GEM object numbers): the
   rules-relevant changes are listed by hand in §5.

## 2. Summary counts

| | Count |
|---|---|
| II functions found | 602 = **393 game** + 209 runtime |
| II game functions paired with I | **276** (same **113**, changed **164**) |
| II game functions with no I partner (**new**) | **116** (63 of them call GEM/VDI: ST user interface; 16 reference strings) |
| I game functions (hunks 0–89) with no II partner (**removed / platform**) | **126** (mostly Intuition/graphics/audio: hunks 77, 80, 86, 89; see §8.2) |
| How the 276 pairs were found | 92 string anchors, 111 callee, 23 caller, 37 module order, 14 read |
| Changed pairs by differing part | const 148, calls 59, text 53, float 4 (several per pair) |
| Confidence of the 393 rows | `[C]` 14 pairs + 6 new functions read in full, `[~]` the rest of the string/float/sim ≥ 0.6 rows, `[?]` 44 weak shape matches |

Main-loop dispatch confirms the module correspondence: both programs run a 13-mode loop (II `0x07344`, I `h2:0x0000`)
with the same mode numbers: 1 town, 2 wilderness field loop, 3 dungeon field loop (the dungeon functions
`0x10698`…`0x1245e` return 3), 4 encounter reaction, 5 combat round,
6 party wiped out (death screen `0x12678` ↔ `h32:0x0000`), 7 ending, 8 *(I: magical pool; II: lava)*, 9 wilderness inn, 10 swimming, 11 encounter start,
12 combat victory, 13 dungeon `[C]`.

## 3. Runtime and OS bindings (II)

- **OS traps** `[C]`: `0x19076` XBIOS (trap #14), `0x19086` BIOS (trap #13), `0x19096` GEMDOS (trap #1). GEMDOS calls
  used by the game: Fopen/Fread/Fclose (RSC, `TWNS.INT`, `DNG*`, scrolls, shapes, pictures), Fcreate/Fwrite (`DNG.SAV`,
  `DNGX.SAV`, roster saves at `0x13526`), Fdelete, Fseek (`scrolls_load`), Malloc/Mfree, Cprnout (5, printout).
  XBIOS: Setpalette 6, Setcolor 7, Keytbl 16, Giaccess 28, Dosound 32 (sound effects `0x0b8b0`–`0x0b9e2`, the
  partners of the Amiga audio module h89), Kbrate 35, Vsync 37.
- **GEM**: AES call gate `0x1a6f8` (and the raw `trap #2, d0=200` at `0x0006e`), VDI gate `0x1a5f0` (`d0=115`).
  Bindings named by the opcode they store (AES `control[0]`, VDI `contrl[0]` at `0x25f3e`), all `[C]`; list below.
  `0x190a6`–`0x19d38` is the port's own object-tree / text helper layer (24-byte `OBJECT` arithmetic).
- **DRI libc** `[C]` unless marked: `sprintf 0x1bf3c` (121 of 130 call sites pass a `%` string), `_doprt 0x1bf94`,
  `lmul 0x1a606`, `ldiv 0x1a65a [~]`, `rand 0x1ad22 [~]` (0..32767; the game's `rnd100 0x0fd40` = `rand()/327`, 0..100),
  `_main 0x1ac48` / argv parser `0x1b7cc` (`C runtime`, `: unmatched quote`). Startup `0x00000` (crt0, Mshrink),
  `__BDOS 0x00096`, `blkfill 0x000c4`.
- **FFP** (Motorola fast floating point; each C wrapper loads `d7`/`d6` and calls the asm core) `[C]` from the cores'
  code and the training chain: `fpadd 0x1db48`, `fpcmp 0x1db68`, `fpdiv 0x1db86` (`a / b`), `fppow 0x1dba6`
  (`exp(b·log a)`), `fpltof 0x1dbc6`, `fpftol 0x1dc56`, `fpmul 0x1dcde`, `fpneg 0x1dcfe`, `fpsub 0x1dd1a`; asm cores
  `ffpadd 0x1dd86`, `ffpcmp 0x1de58`, `ffpdiv 0x1de88`, `ffpexp 0x1df20`, `ffplog 0x1dffa`, `ffpmul 0x1e0ca`.

Named bindings: `0x19e7c` vswr_mode (VDI 32) · `0x19eb6` v_clsvwk (VDI 101) · `0x19ee0` v_fillarea (VDI 9) · `0x19f1e` v_gtext (VDI 8) · `0x19f8a` v_gdp (v_bar…) (VDI 11) · `0x1a020` v_opnvwk (VDI 100) · `0x1a0a0` v_pline (VDI 6) · `0x1a0de` vq_extnd (VDI 102) · `0x1a152` vqt_extent (VDI 116) · `0x1a1c2` vro_cpyfm (VDI 109) · `0x1a21e` vr_recfl (VDI 114) · `0x1a25c` vrt_cpyfm (VDI 121) · `0x1a2d0` vs_clip (VDI 129) · `0x1a318` vsf_color (VDI 25) · `0x1a352` vsf_interior (VDI 23) · `0x1a38c` vsf_perimeter (VDI 104) · `0x1a3c6` vsf_udpat (VDI 112) · `0x1a408` v_show_c (VDI 122) · `0x1a43c` vsl_color (VDI 17) · `0x1a476` vsl_width (VDI 16) · `0x1a4b6` vst_alignment (VDI 39) · `0x1a506` vst_color (VDI 22) · `0x1a540` vst_effects (VDI 106) · `0x1a57a` vst_height (VDI 12) · `0x1a5f0` vdi_trap (trap #2, d0=115) · `0x1a606` lmul (32-bit multiply) · `0x1a65a` ldiv (32-bit divide) [~] · `0x1a6f8` aes_call (crys_if) · `0x1a7d6` evnt_button (AES 21) · `0x1a830` evnt_mesag (AES 23) · `0x1a84a` evnt_timer (AES 24) · `0x1a86c` evnt_multi (AES 25) · `0x1a94a` form_do (AES 50) · `0x1a96c` form_dial (AES 51) · `0x1a9c6` form_alert (AES 52) · `0x1a9e8` form_center (AES 54) · `0x1aa32` graf_handle (AES 77) · `0x1aa74` graf_mouse (AES 78) · `0x1aa96` graf_mkstate (AES 79) · `0x1aad0` menu_bar (AES 30) · `0x1aaf2` menu_icheck (AES 31) · `0x1ab1c` menu_tnormal (AES 33) · `0x1ab46` objc_draw (AES 42) · `0x1ab90` objc_offset (AES 44) · `0x1abce` rsrc_load (AES 110) · `0x1abe8` rsrc_free (AES 111) · `0x1abfa` rsrc_gaddr (AES 112) · `0x1ac2e` wind_update (AES 107)

## 4. Corrections to `FORMAT.md` (applied there as well)

1. **PRIEST multiplier is 36.3, not ≈18.15** `[C]`. FFP `0x91333346` has exponent byte `0x46` (×64), like 33.0 /
   42.9 / 49.5 / 52.8: mantissa `0x913333` / 2²⁴ = 0.567 → **36.3**. 18.15 would need exponent `0x45`. So II doubled
   both changed multipliers: FIGHTER 16.5 → 33.0, PRIEST 18.1 → 36.3 (and changed both stat sums).
2. **`TEMPLE` is not a new service** `[C]`. The Amiga I `game` has the same string (h24 `0x00ec`). In both programs it
   is the title of the spell picker that Guild **Learn** opens (II `0x00938`/`0x00974` ← `0x0d0d2`; I `h23:0x03e8` ←
   `h5:0x10f2`). There is still no Temple facility in either.
3. **Record @282 = SCORE, @284 = ORIGIN, @286–@300 and @302–@316 = two 8-word per-amulet arrays** `[C]` for @282/@284
   (sheet `0x03a50` prints `LUCK %3u SCORE %7u` with @10 and @282, and `ORIGIN: %s` from the table at data+`0x1494`
   `GELNOR / FERRONRAH / NETHERWORLD` indexed by @284); `[~]` for the arrays (creation clears both; encounter start
   reads @284 + 2k, see §5.6). Creation sets @284 = 1 (FERRONRAH), or 2 (NETHERWORLD) when the creating town index > 6
   `[~]`; transferred characters keep 0 = GELNOR.

## 5. Findings: what II changed (rules)

### 5.1 Guild training (`guild_train`, II `0x0cdde` ↔ I `h5:0x13b0`) `[C]`

Same structure in both: refuse if status @24 = 0 or level @88 ≥ 15; 6-way switch on class @220 (order THIEF, MONK,
RANGER, FIGHTER, PRIEST, WIZARD); `s = sum / mult`; `F = 1.8^level`; `X = (F − 0.8)·1000 / (s + 1e−5)`; shortfall
`XP − X`; gold `min(15000, ⌊pow(level, 2.2)⌋ · (20 − CHA)² · price / 100)`; then `level_up` (`0x0c71a` ↔ `h5:0x1748`).

| Class | I (Amiga `h5:0x143a…0x154e`) | II (`0x0ce34…0x0cf14`) |
|---|---|---|
| THIEF | (STR + 2·DEX) / 19.8 | same |
| MONK | (STR + INT + DEX) / 49.5 | same |
| RANGER | (STR + INT + CHA) / 52.8 | same |
| **FIGHTER** | **(STR + DEX) / 16.5** | **(2·STR + DEX) / 33.0** |
| **PRIEST** | **(STR + INT) / 18.1** | **(STR + 2·INT) / 36.3** |
| WIZARD | (2·INT + LUCK) / 42.9 | same |

- So **yes: PHANT.PRG's own Guild training uses the changed FIGHTER/PRIEST sums**, the same constants as UTILIT2's
  transfer threshold (`FORMAT.md` §6). Effect: with equal stats S, the required XP is ×4/3 of I's for both classes
  (FIGHTER 8.25/S → 11/S, PRIEST 9.05/S → 12.1/S, times the level factor); strength counts double for fighters,
  intelligence double for priests.
- **New in II:** `if level == 0: F = 1.0` (`0x0cf84`). The Amiga I has no such reset at all (DOS I resets on XP = 0
  instead, `town-menus.md`). At level 0 the threshold is 0.2 × the level-1 value, which is what lets transferred
  characters train at once. Gold for level 0 is `pow(0, 2.2)` through FFP log: **0 gold**, live s78 (`parity/roster-live.md` §4,
  `training.md`). Level-up also changed its spell-mark rule (every unknown spell ≤ the new level, `training.md` §2).
- Same in I-Amiga and II: `(20 − CHA)` is squared **without** the DOS clamp at 0 (`k = max(20 − CHA, 0)` in DOS), so a
  CHA above 20 still costs gold on both 68000 versions `[C]` (version difference, not a II change).

### 5.2 Toss Rock (`combat_choose_action 0x0f298`, `combat_round 0x07610`, `level_up 0x0c71a`, `create_character 0x0c90c`) `[C]` static

- **Choosing** (`0x0f74a`): option TOSS stores action = −(chosen group); the group comes from `pick_rank 0x0f14c` when
  more than one rank exists, else rank 1. **No class, rank, item or weapon test**: any member, any rank, unlimited rocks.
  The repeat path maps any negative stored action back to TOSS (switch case 6).
- **Resolving** (`0x0773a…0x079aa`, new block in I's combat round `h35:0x0000`): print `%s THROWS A ROCK`; up to 3 tries
  to pick a live target in that group: slot = ⌊(member_index + 0.02·rnd100) / (party_size + 2) · (group_count + 1)⌋;
  no live target → `%s MISSES`. Hit if `rnd100 ≤ TOSS skill @42`, else MISSES. Damage =
  `max(0, ⌊rnd100·12/100⌋ − monster armor) + ⌊rnd100·@28/100⌋` (@28 = the STR/level weapon base from DOS's town-exit
  formula), dealt through the normal monster-damage routine `0x099e0` (I `h38:0x0cf6`), which raises it to at least 1. Full rules: `toss-rock.md`.
- **Skill**: @42 TOSS grows like the other nine skills (level-up loop over @30…@48 with the class table at
  `0x25cd6`). Its bonus @62 is **recomputed at creation and at every level-up**: `@62 = 2·level + 4·DEX + STR + 10`,
  +15 for DWARF (race 1), +30 for GNOME (race 4) (`0x0cb70`, `0x0c730`); "level" is the level *before* the increment, and creation runs `level_up(0)`, so a new
  character gets 4·DEX + STR + 10 + race bonus. Live s78: a transferred TROLL FIGHTER (STR 26, DEX 3) trained 0 → 1 gets
  @62 = 48 `[C]` (`parity/roster-live.md` §4). I has no such line (I never uses @42/@62).

### 5.3 Terrain (`overworld_step 0x06022` ↔ I `h59:0x0000`; `lava_or_swim 0x0fac2` ↔ I `h71:0x0e88`) `[C]` static

Cell value v: class c = v / 10, digit d = v mod 10 (`0x25b80`, `0x25f30`). Move delay = (c + 1)·75 ticks for c ≤ 3,
else 60 (both). Class switch, I → II:

| Class | I (Amiga) | II (ST) |
|---|---|---|
| 5 | dungeon (mode 13) | same |
| 7 | fixed lair: monster 12·d + 10, once (flag) → combat | **9 % chance** (rnd100 ≤ 8) of monster 12·d + 1 + ⌊rnd100/34⌋ (1–3 + 12·d), flag set only when it fires |
| 8 | — | **lava**: d = 0 → nothing; d > 0 → mode 8 |
| 11 | **magical pool** (mode 8, `h71:0x0a50` "A MAGICAL POOL", "%s IS CHANGED") | same as class 7 (the pool is **gone**; II has no pool string) |
| 10 | swim (mode 10) | same |

- **Lava** (mode 8 = `0x0fac2(0)`): every living member takes `1 + ⌊rnd100 · d / 10⌋` (1 to d·10 + 1), no skill roll,
  then DIES if HP ran out. Lava cells return before the random-encounter roll, so **no encounters** on lava (manual
  agrees). Swimming (`0x0fac2(1)`) is I's rule: `rnd100 < SWIM @48` swims, else 1–6 damage.
- Mist = class 11 and Dark Voids = class 7 `[~]` (fill patterns, seen live s78); full rules in `terrain.md`.

### 5.4 Divine spells (`cast_spell_field 0x06ecc` ↔ I `h50:0x07c4`) `[C]` static

Two new spell cases, by spell number (slot + 1):
Both test the **location** word `0x24eb4` (3 = dungeon, 2 = wilderness `[~]`: mode 6 frees the dungeon buffers when
it is 3), not the main-loop mode.
- **#58 DIVINE I** — only at location 3 (dungeon): deity = `DEMIGOD / ATHENA / APOLLO / ZEUS`
  [⌊rnd100·rnd100·0.0004⌋]; the stored FFP constant is just below 0.0004, so even 100·100 gives 3.99… → 3 and the
  index stays 0–3 `[~]` (depends on `ffpmul` rounding). Prints `%s [original text]`, then dungeon
  message 105 (`dungeon_message 0x116aa`).
- **#57 DIVINE II** — only at location 2 (wilderness) **in region 26** (`0x25b92`): sets the forced-encounter monster
  `0x2832c` = max(1, ⌊rnd100·rnd100·0.0072⌋) (1–71 with the stored constant), or with 25 % (rnd100 < 25)
  ⌊75 + 0.02·rnd100⌋ (75–76, Pluto's dragons), and starts combat (mode 11). The id is 1-based; the 25 % branch draws a new r (75 or 76 = beasts V/VI); see `magic-divine.md` (s78), which supersedes this section.
- Learning: `guild_learn_spell 0x0d0d2` keeps I's bounds (learnable slot < 55, `#55`/`#54` tests) `[C]`, so divine
  spells are never learnt at the Guild; #57 is granted at the DNG3 pool, #58 only by Phantasie I (`magic-divine.md`).

### 5.5 Rulebook quiz (`rulebook_quiz 0x19d42`, called by `main_loop 0x07344` mode 3) `[C]` static

- Once per program run (flags at data+`0x5d0c`…`0x5d12`, all 0 in the file). On the first mode-3 entry (dungeon field
  loop) it draws N = ⌊rnd100/10⌋ until 2 ≤ N ≤ 7; the entry counter is compared *before* it is incremented and starts
  at 0, so the quiz fires on the **(N + 1)-th** dungeon-loop entry (the 3rd to the 8th).
- Shows `PLEASE ANSWER THE FOLLOWING QUESTIONS USING THE RULEBOOK`, then question q = ⌊rnd100/10⌋ with 9 → 7 and 10 → 8
  (q = 10 needs rnd100 = 100, ≈ 0.2 %), so question 7 comes up about twice as often as the others. Answer key (button, data+`0x5d26`): 1, 3, 2, 2, 3, 3, 1, 2, 1 — i.e.
  8, 18, 11, 110, 1, 9, 8, 20, 14. The checked ones match the manual (human max 18, thief HP L15 110, spell 5 costs 1,
  fighter max MP L4 8, shield max 20, armour 39 STR 14).
- Right → play on. Wrong once → `YOUR LAST CHANCE` and another random question. **Wrong twice → endless loop
  (`0x19e6c: tst; bne self`), the game hangs.** `I GIVE YOU ONE MORE CHANCE` (index 9) is never shown.

### 5.6 Amulets, orb and the endgame `[~]`

Superseded by `endgame.md` (s78, `[C]`): the minion flags are @286 + 2(k−1), the beast runes @302 + 2(k−1), and the
win is DNG8 script op 23. The lines below are the s76 first reading.

- **Pluto's minions**: `encounter_start 0x09bf4` (I `h44:0x0000`) treats a first monster group whose reaction
  modifier (+0x2a) is below −10 as minion k = −10 − value (1–8). If any party member has word @(284 + 2k) = 1
  (the first per-amulet array, @286 + 2(k − 1)), the encounter is replaced by a random monster ⌊rnd100·36/100⌋ ("AN ENCOUNTER");
  otherwise `[original text]`. On victory (`combat_victory 0x0a290`, I `h44:0x0614`) the first living member
  `TAKES AN AMULET`: item 140 + k (AMULET k, items 141–148) goes into the first free party slot;
  `[original text]`.
- **Score and orb**: `score_check 0x0d798` (I `h8:0x0d84`, which tests I's rings 141–151) now tests item 150 (ORB) and
  SCORE @282 > 150, raises @282 of every party member to a computed value, and prints `score_rank_text 0x00f64`
  (I `h23:0x07b2`) with one of
  `novice adventurers / experienced adventurers / not ready to destroy the orb / ready to destroy the orb / to find the
  orb / heroes of Ferronrah / [original text] Gelnor also` (I: "… THE GODS AND FACE THE TESTS").
- **Amulet use**: `use_item` calls `0x02ab0` with k = item − 141: GEM dialog 18; answer 2 → `YOU ARE CHANGED` and
  @302 + 2k := 1 for every member with @286 + 2k = 1; answer 3 → `LOOKS NICE !` `[~]`.
- **Item use** `use_item 0x0dfcc` (I `h8:0x1342`) tests item ids 140, 141 and 149 (FLMN WAND) where I tested 141 and 150
  `[~]`; the item-line printers `0x03946` / `0x17c92` special-case 141–150.
- **Ending**: mode 7 runs `endgame_filmon 0x12f2a` (I `h32:0x06d0`, Zeus "WELL DONE, MY SMALL FRIENDS", `final.pic`):
  rewritten (Filmon the Sage, `final2.pic`, `crowd.pic`, title `%s - the %s from %s` by origin `0x1298e`), then
  mode 2 again. Pluto's farewell (`pluto_voice 0x0ad9a`, "You hear a booming voice") is called from combat victory.
  The Astral Plane / Great Demon defeat path (`0x12bea` ↔ `h32:0x055c`) is kept from I.

### 5.7 Other changes worth a spec line

- **Monster groups**: I's builder `h44:0x0998` is split in II into `build_monster_groups 0x0a72e` and an id check
  `0x186de` ("Wrong monster") `[C]`; constants differ (group sizes) — spec with the 3b.7 "monsters / encounters" row.
- **Scrolls**: `read_scroll 0x0292a` (I `h29:0x024e`) loops over **21** pages of 40 bytes (I: 20); `SCROLLS.DTX` is
  16384 B (I 16000) `[~]`.
- **Region files** 3000 B (I 2500) in `load_out_region 0x04b88` / `save_out_region 0x04c8c` `[C]`.
- **Character sheet** `0x03a50` (I `h29:0x10ba`) adds ORIGIN, LUCK, SCORE, TOSS and an EQUIPMENT block `[C]`.
- **ST-only**: VBL colour cycling: `0x137c0` installs the routine `0x1382e` (rotates hardware palette registers
  `0xff8248…`) when the wilderness loop starts (mode 2), `0x137e6` removes it (modes 1, 6, 11); the bytes after
  `0x13862` are data, which the disassembler shows as two huge "functions" `0x137e6`/`0x151ee` `[~]`;
  RSC/GEM dialogs replace I's Intuition requesters (I modules h77, h80, h86 have no partners); Polish debug alerts
  (`0x18856`, `0x18b16`).

## 6. Changed / new = spec work list (3b.7), in priority order

| # | 3b.7 row | Functions to spec (II ↔ I) | What is known / what is open |
|---|---|---|---|
| 1 | **Training** | `0x0cdde` ↔ `h5:0x13b0`; `0x0c71a` ↔ `h5:0x1748` | done: `training.md` (live s78; level-0 gold = 0) |
| 2 | **Toss Rock** | `0x0f298` ↔ `h71:0x0446`; `0x07610` ↔ `h35:0x0000` (block `0x0773a…0x079aa`); `0x0c71a`, `0x0c90c` | done: `toss-rock.md` (live s78; `@28` = town-exit formula, damage ≥ 1) |
| 3 | **Terrain** | `0x06022` ↔ `h59:0x0000`; `0x0fac2` ↔ `h71:0x0e88` (+ removed `h71:0x0a50` pool) | §5.3; open: which classes are Mist / Dark Voids (needs `OUT*.DAT` cell values, 3b.8), encounter tables `0x0fe38` ↔ `h74:0x0000` |
| 4 | **Endgame** | `0x12f2a` ↔ `h32:0x06d0`; `0x0ad9a` new; `0x0a290` ↔ `h44:0x0614`; `0x00f64` ↔ `h23:0x07b2`; `0x0d798` ↔ `h8:0x0d84`; `0x1298e` new | §5.6 `[~]`; open: win condition, what sets @282 ranks, orb destruction trigger |
| 5 | **Amulets / orb** | `0x09bf4` ↔ `h44:0x0000`; `0x0a290`; `0x0dfcc` ↔ `h8:0x1342`; `0x12078` ↔ `h41:0x1ba4` (s95: the field-id map, DOS OV2 `0x1ba5`, not an item activation); `0x02ab0` new ("YOU ARE CHANGED", sets @302) | minion → amulet k `[~]`; open: meaning of the two 8-word arrays, FLMN WAND (149) use |
| 6 | **DIVINE II / I** | `0x06ecc` ↔ `h50:0x07c4`; combat side `0x07610` (spell numbers 55/58/60 tables) | §5.4 `[C]`; open: where #57/#58 are granted (dungeon events `0x11ab6` ↔ `h41:0x15b8`?), 0/1-based monster id |
| 7 | **Origin** | `0x0c90c` ↔ `h5:0x0832` (sets @284); `0x03a50`, `0x03192`, `0x17536` (print); `0x09bf4` | §4.3; open: town-index rule `> 6` → NETHERWORLD needs II's town list (3b.8) |
| 8 | **Rulebook quiz** | `0x19d42` (new) ← `0x07344` mode 3 | §5.5 `[C]`; decision needed (see §7) |
| 9 | **Monsters / encounters** | `0x0a72e` + `0x186de` ↔ `h44:0x0998`; `0x0fe38` ↔ `h74:0x0000`; `0x09dac` ↔ `h44:0x00d6` | done s80: `encounters.md` (static; L / reaction averaged, wandering 12·T, no Black Knights, height fit) |
| 10 | **Temple** | `0x00938`/`0x00974` ↔ `h23:0x03e8` | **no work**: same screen title in I (§4.2); close the row |
| 11 | Data formats (3b.8) | `0x04b88`/`0x04c8c` (3000-B regions), `0x0292a`/`0x13608` (scrolls), `0x10320` (DNG/MESS), `0x0ed50` (TWNS.INT), `0x13526` (roster save) | sizes known, layouts open |
| 12 | GEM UI (presentation only) | 63 new GEM-calling functions; I h77/h80/h86 | no rules; skip |

## 7. Conflicts for the user

Nothing here contradicts a rule the manual states outright, but these points need a decision (like I's D-036) or
belong next to an existing `DIFFERENCES-II.md` entry. Not decided here.

1. **Rulebook quiz hangs the game after two wrong answers** (§5.5). The manual says nothing about a quiz. Options:
   leave it out (as I's D-036 did for I's protection), keep it as an option without the hang, or keep it 1:1.
2. **Training XP for FIGHTER and PRIEST rises by a third in II** (§5.1). The manual says training is unchanged from I
   ("no change stated"), and gives no formula. Manual-silent change: does II use II's numbers (1:1) — presumably yes —
   and is it worth a `DIFFERENCES-II.md` note?
3. **DIVINE spells** (§5.4) add facts to the open **D2-004** (#55+ vs #58+): in the code DIVINE II (#57) works only in
   wilderness region 26 and starts a fight with a random (often dragon) monster; DIVINE I (#58) works only in
   dungeons and gives a deity's advice. The manual only says divine spells are "for special purposes".
4. **Magical pools are gone in II** (§5.3): terrain class 11 became an encounter terrain, and I's pool effect (one of
   the six stats changed, "%s IS CHANGED", `h71:0x0a50`) has no II counterpart. II's similar text "YOU ARE CHANGED"
   (`0x02ab0`) is the amulet-use dialog called from `use_item` (it sets @302 + 2k for members with @286 + 2k = 1),
   not a pool. The II manual's terrain text does not mention pools either way.
5. **Lava "almost always causes damage"** (manual) vs the code: a lava cell always damages every living member unless
   its digit d is 0, in which case it does nothing. Probably consistent (d = 0 cells are the "almost"), listed so the
   user can confirm once the `OUT*.DAT` values are known.

## 8. Function tables

### 8.1 II game functions (393)

| II `PHANT.PRG` | Name | Amiga I `game` | Status | Evidence |
|---|---|---|---|---|
| `0x0013c` |  | — | new [~] | calls GEM/VDI |
| `0x00352` | guild_menu | `h23:0x00ac` | changed [~] | strings "GUILD" · sim 0.68 · Δ const ([5, 6, 16, 17, 18, 19, 21, 23], [92]) |
| `0x004d2` |  | — | new [~] | calls GEM/VDI |
| `0x00580` | bank_menu | `h80:0x0ac8` | changed [~] | strings "BANK" · sim 0.43 · Δ text ([], ['OPTIONS']); const ([16, 17, 19], []) |
| `0x005da` | pick_member | `h23:0x01f8` | changed [?] | callee of 0x0cdde · calls GEM/VDI · sim 0.55 · Δ const ([9], [6, 11, 38]) |
| `0x0068e` | str: "%-10.9s%-5.4sHAS%6u GP" | `h23:0x0246` | changed [~] | strings "%-10.9s%-5.4sHAS%6u GP", "%-10.9s%-4.3s%-4.3s%-3.2sHP:" · calls GEM/VDI · sim 0.46 · Δ text (['%-10.9S%-4.3S%-4.3S%-3.2SHP', '%3D'], ['%-10.9S%-9.8S%-8.7S%-5S']); const ([18, 22], [23, 29, 38, 39, 45, 270]) |
| `0x007be` |  | — | new [~] |  |
| `0x00858` |  | — | new [~] |  |
| `0x0088c` |  | — | new [~] |  |
| `0x008d8` |  | `h23:0x03b0` | same [~] | module order 0x0068e..0x00956 · sim 0.84 |
| `0x00900` |  | — | new [~] |  |
| `0x00938` | spell_picker_title "TEMPLE" | — | new [C] | strings "TEMPLE" |
| `0x00956` | str: "CREATING NEW CHARACTER" | `h23:0x0b46` | changed [~] | strings "CREATING NEW CHARACTER" · sim 0.66 · Δ const ([6], []) |
| `0x00974` | spell_picker (TEMPLE) | `h23:0x03e8` | changed [C] | read: spell picker titled TEMPLE, called from Learn (both) · sim 0.47 · Δ text ([], ['AFFORD', 'ANY', 'CAN', 'FAILED', 'HAVE', 'LEARN', 'LEARNED', 'MORE', 'NOT', 'O.K', 'TEMPLE']); const ([7, 11], []) |
| `0x00a1a` | list_available_spells | `h23:0x0492` | changed [~] | strings "%-3d", "%7d GP. " · calls GEM/VDI · sim 0.61 · Δ const ([6], [86, 96, 149]) |
| `0x00c70` | str: "HEALING I" | — | new [~] | calls GEM/VDI |
| `0x00f04` | str: "EXIT" | — | new [~] | calls GEM/VDI · strings "EXIT", "[original text]" |
| `0x00f64` | score_rank_text (endgame lines) | `h23:0x07b2` | changed [~] | strings "%4d", "      " · calls GEM/VDI · sim 0.42 · Δ text (['ALSO', 'ARE', 'GELNOR', 'HEROES', 'NEED'], ['FACE', 'GODS', 'TESTS']); const ([6, 7, 8, 9, 11], [5, 20, 33, 34, 35, 36, 37]) |
| `0x01068` |  | — | new [~] | calls GEM/VDI |
| `0x010de` |  | `h23:0x0898` | changed [?] | module order 0x00f64..0x010fe · sim 0.58 · Δ calls ['0x011ba'] |
| `0x010fe` | str: "CREATING %s" | `h23:0x08d8` | changed [~] | strings "CREATING %s" · calls GEM/VDI · sim 0.61 · Δ const ([6, 7, 128], [13]); calls ['0x0168a'] |
| `0x011ba` |  | `h23:0x0970` | changed [?] | module order 0x010fe..0x01362 · calls GEM/VDI · sim 0.29 · Δ const ([5, 11], [6, 7, 12, 13, 44, 112, 128]); calls ['0x04054'] |
| `0x01272` |  | — | new [~] | calls GEM/VDI · strings "%ld" |
| `0x01310` |  | — | new [~] | strings "%-5u" |
| `0x01362` | str: "GOLD CARRIED %u" | `h23:0x0afe` | changed [~] | strings "GOLD CARRIED %u" · sim 0.70 · Δ const ([6], []) |
| `0x0139a` | str: "THE ITEM SINGS" | `h23:0x0b32` | changed [~] | strings "THE ITEM SINGS" · sim 0.66 · Δ const ([6], []) |
| `0x013b8` |  | `h23:0x0b5a` | changed [~] | callee of 0x0e97a · calls GEM/VDI · sim 0.61 · Δ const ([10, 16], [11]) |
| `0x01438` | str: "PURGE" | `h23:0x0c08` | changed [?] | callee of 0x0c90c · calls GEM/VDI · sim 0.34 · Δ text ([], ['KATI', 'SKILLS', 'SPELLS', 'ZOLI']); const ([11, 17, 372, 420], [14]); calls ['0x04054'] |
| `0x01586` | buy_ninth_item_alert | `h26:0x0000` | changed [~] | strings "[0][       /He's  just bough" · calls GEM/VDI · sim 0.47 · Δ text ([], ['ENOUGH', 'HAS', 'ITEMS', 'MAGIC', 'NINE', 'NOT', 'O.K']); const ([12], []); calls ['0x04054'] |
| `0x015bc` |  | `h26:0x0038` | changed [?] | module order 0x01586..0x015e0 · sim 0.40 · Δ text ([], ['O.K']); const ([8], []) |
| `0x015ce` |  | — | new [~] |  |
| `0x015e0` |  | `h26:0x0046` | changed [?] | callee of 0x0c1ea · sim 0.52 · Δ text ([], ['AFFORD', 'CAN', 'NOT']) |
| `0x015f2` |  | `h26:0x0054` | changed [?] | module order 0x015e0..0x01616 · sim 0.40 · Δ text ([], ['ENOUGH', 'ONE', 'TEACH', 'WISE']); const ([5], []) |
| `0x01604` |  | — | new [~] |  |
| `0x01616` |  | `h26:0x0062` | changed [?] | callee of 0x0dfcc · sim 0.52 · Δ text ([], ['UNUSABLE']) |
| `0x01628` |  | `h26:0x006e` | same [~] | callee of 0x00974 · calls GEM/VDI · sim 0.77 |
| `0x01668` |  | — | new [~] | calls GEM/VDI |
| `0x0168a` |  | `h41:0x1e62` | same [~] | callee of 0x11ab6 · calls GEM/VDI · sim 0.76 |
| `0x016c6` | str: "WHICH SPELL?" | `h26:0x0088` | changed [~] | strings "WHICH SPELL?" · calls GEM/VDI · sim 0.71 · Δ const ([], [86, 96]) |
| `0x0173c` |  | — | new [~] | calls GEM/VDI |
| `0x017e4` | confirm_cost "IT WILL COST" | `h26:0x018c` | changed [~] | strings "GUILD TRAINING", "[0][              /You need " · calls GEM/VDI · sim 0.69 · Δ text ([], ['AFFORD', 'CAN', 'O.K']); const ([6], []) |
| `0x018a6` |  | `h26:0x03a6` | changed [~] | caller of 0x0068e · calls GEM/VDI · sim 0.69 · Δ const ([11], []) |
| `0x019aa` |  | — | new [~] |  |
| `0x019be` |  | — | new [~] | calls GEM/VDI |
| `0x01a08` |  | `h26:0x0346` | changed [?] | callee of 0x0da82 · calls GEM/VDI · sim 0.43 · Δ const ([11], [86]); calls ['0x018a6'] |
| `0x01a88` | str: "DEAD" | `h26:0x0412` | changed [~] | strings "%-10.9s%-9.8s%-5.4s%-4s" · calls GEM/VDI · sim 0.51 · Δ text (['%-10.9S%-9.8S%-5.4S%-4S'], ['%-10.9S%-9.8S%-5.4S%-5.5S']); const ([8, 9, 10, 11, 16, 128], [12, 32, 33, 35, 70, 71, 270]); calls ['0x0168a'] |
| `0x01d54` | armory | `h26:0x0682` | changed [~] | strings "%-9s is shopping", "Cash -> %-6u" · calls GEM/VDI · sim 0.51 · Δ text (['CASH'], ['%-29.29S']); const ([9, 10, 11, 30, 128], [32, 33, 35, 65, 67, 70, 71, 270]); calls ['0x0168a'] |
| `0x02026` | str: "THE INN" | `h26:0x0988` | changed [~] | strings "THE INN" · calls GEM/VDI · sim 0.61 · Δ const ([6], [12, 20, 100]) |
| `0x02080` | inn_town | `h26:0x09d6` | changed [~] | strings "MAGIC INCREASES", "%-9s IS RESTING %s" · calls GEM/VDI · sim 0.68 · Δ const ([7, 9], [12, 20, 100, 101, 270]) |
| `0x02184` |  | `h26:0x0aea` | changed [?] | callee of 0x0e5fc · calls GEM/VDI · sim 0.53 · Δ const ([8], [15]) |
| `0x021fe` |  | — | new [~] |  |
| `0x0221e` |  | — | new [~] |  |
| `0x0223c` |  | — | new [~] | calls GEM/VDI |
| `0x0226e` |  | `h26:0x0bba` | changed [?] | callee of 0x0c5c4 · calls GEM/VDI · sim 0.42 · Δ const ([], [118]) |
| `0x022ac` |  | — | new [~] | calls GEM/VDI |
| `0x022ec` |  | `h80:0x0818` | changed [?] | callee of 0x00352 · sim 0.40 · Δ const ([], [185]); calls ['0x02308'] |
| `0x02308` |  | `h80:0x0942` | same [~] | callee of 0x00352 · calls GEM/VDI · sim 0.70 |
| `0x02356` |  | `h26:0x0c1c` | changed [?] | callee of 0x0d442 · sim 0.52 · Δ text ([], ['ROOM', 'THERE']) |
| `0x02366` |  | `h26:0x0c2a` | changed [?] | module order 0x02356..0x0238c · sim 0.41 · Δ text ([], ['GET', 'HERE', 'MONTH', 'NOT', 'TAKES', 'TOWN']); const ([6], []) |
| `0x0238c` |  | `h26:0x0c44` | same [?] | callee of 0x0bf5a · calls GEM/VDI · sim 0.48 |
| `0x023e0` |  | — | new [~] |  |
| `0x023ee` | transport_menu | — | new [~] | calls GEM/VDI · strings "[original text]", ">  %-12s<" |
| `0x0260a` |  | — | new [~] |  |
| `0x026cc` |  | `h29:0x0000` | changed [~] | callee of 0x0c0a8 · sim 0.63 · Δ const ([6, 17], []) |
| `0x02732` | str: "twns.dat" | `h29:0x0058` | same [~] | floats · sim 0.98 |
| `0x0279a` | str: "guild.dat" | `h29:0x00be` | same [~] | floats · sim 1.00 |
| `0x02856` | str: "LEAVING %s" | `h29:0x0168` | changed [~] | strings "LEAVING %s" · sim 0.75 · Δ const ([6], [280]) |
| `0x028ac` |  | `h29:0x01b4` | same [~] | module order 0x02856..0x0292a · sim 0.87 |
| `0x028cc` |  | `h29:0x01ca` | changed [?] | module order 0x02856..0x0292a · sim 0.52 · Δ text ([], ['ASSEMBLE', 'GROUP', 'MUST']) |
| `0x028de` |  | `h29:0x01da` | changed [?] | module order 0x02856..0x0292a · sim 0.41 · Δ text ([], ['PRINTOUT', 'WANT']); const ([13], []) |
| `0x02904` |  | `h29:0x01f6` | changed [?] | callee of 0x03a50 · sim 0.36 · Δ text ([], ['O.K', 'PREPARE', 'PRINTER', 'PRT']); const ([14], []) |
| `0x0292a` | read_scroll | `h29:0x024e` | changed [?] | caller of 0x13608 · calls GEM/VDI · sim 0.58 · Δ const ([21], [12, 19, 20, 152]) |
| `0x02a44` |  | — | new [~] | calls GEM/VDI |
| `0x02ab0` | "YOU ARE CHANGED" (sets @302) [~] | — | new [~] | calls GEM/VDI · strings "YOU ARE CHANGED", "LOOKS NICE !" |
| `0x02bc8` |  | — | new [~] |  |
| `0x02c0c` |  | — | new [~] | calls GEM/VDI |
| `0x02d0e` | str: "Now if You are brave and cold bloo…" | — | new [~] | calls GEM/VDI |
| `0x02db2` |  | — | new [~] | calls GEM/VDI |
| `0x02f14` | str: "%u gold" | `h29:0x076c` | changed [~] | strings "%u gold", "and %u XP" · calls GEM/VDI · sim 0.63 · Δ const ([11], [7, 47, 48]); calls ['0x04054'] |
| `0x02fbe` |  | `h29:0x07e6` | same [~] | strings "%3u" · sim 0.96 |
| `0x02fe8` |  | `h29:0x0814` | same [~] | strings "%-3d" · sim 0.96 |
| `0x03012` |  | `h29:0x0842` | changed [?] | module order 0x02fe8..0x03578 · calls GEM/VDI · sim 0.42 · Δ const ([6, 15, 128], [9, 14, 32, 61, 62, 63, 64]); calls ['0x0168a'] |
| `0x03192` | party_line (LEVEL %3d ... origin) | — | new [~] | calls GEM/VDI · strings "LEVEL%3d %s %s %s", "%7u" |
| `0x03578` |  | `h29:0x0cca` | changed [?] | callee of 0x0e69e · calls GEM/VDI · sim 0.54 · Δ const ([11, 15, 16, 17, 18, 20], [14]); calls ['0x04054'] |
| `0x0362a` |  | `h29:0x0d46` | changed [~] | caller of 0x02fbe · calls GEM/VDI · sim 0.77 · Δ const ([5, 6, 8, 10, 11, 42], [9, 50, 51, 52, 53, 54, 55, 56, 57]) |
| `0x037e6` |  | — | new [~] | calls GEM/VDI |
| `0x0383e` |  | `h29:0x0ee2` | changed [~] | caller of 0x038f2 · calls GEM/VDI · sim 0.69 · Δ const ([11], [10, 58, 59, 60]) |
| `0x038f2` | str: "%-13.12sPTS:%-2u" | `h29:0x0f5c` | changed [~] | strings "%-13.12sPTS:%-2u" · sim 0.69 · Δ text ([], ['NOTHING']); const ([364], []) |
| `0x03946` |  | `h29:0x0fb6` | changed [~] | module order 0x038f2..0x03a50 · calls GEM/VDI · sim 0.60 · Δ const ([70, 99, 149, 150], [10, 32, 65, 151]) |
| `0x03a50` | character_sheet (ORIGIN, LUCK, SCORE, TOSS) | `h29:0x10ba` | changed [C] | read: character sheet · sim 0.35 · Δ text (['%-13.12SPTS', '%-2U', '%10LU', '%3U', '%7U', 'AGE', 'ARMOR', 'ATTACK', 'CHARISMA', 'CONSTITUTION', 'DEXTERITY', 'DISARM'], ['NOTHING', 'POTIONS']); const ([6, 8, 10, 16, 18, 20, 22, 26, |
| `0x03fba` |  | — | new [~] | calls GEM/VDI |
| `0x04054` |  | `h26:0x0322` | changed [?] | callee of 0x0db5a · sim 0.48 · Δ const ([18, 181], [7]) |
| `0x04082` |  | — | new [~] | calls GEM/VDI |
| `0x042d4` |  | — | new [~] |  |
| `0x042e8` |  | — | new [~] | calls GEM/VDI |
| `0x04382` | load_phant_rsc | — | new [~] | calls GEM/VDI · strings "phant.rsc", "[1][can't load RSC file  ][abo" |
| `0x044ee` |  | `h77:0x0246` | same [~] | callee of 0x0ec36 · calls GEM/VDI · sim 0.61 |
| `0x04508` |  | — | new [~] | calls GEM/VDI |
| `0x04528` |  | — | new [~] | calls GEM/VDI |
| `0x045e8` |  | — | new [~] | calls GEM/VDI |
| `0x04706` | str: "error 1][ok]" | — | new [~] | calls GEM/VDI · strings "[0][error 1][ok]" |
| `0x04738` |  | — | new [~] | calls GEM/VDI |
| `0x0486c` |  | — | new [~] |  |
| `0x0488e` |  | `h56:0x01c8` | same [~] | callee of 0x0492e · sim 0.70 |
| `0x048ce` |  | `h56:0x01fc` | same [~] | callee of 0x06724 · sim 1.00 |
| `0x048e6` |  | `h56:0x0214` | same [~] | callee of 0x04cd0 · sim 0.70 |
| `0x0492e` |  | `h56:0x0254` | same [~] | module order 0x048e6..0x049a8 · sim 0.97 |
| `0x049a8` |  | `h56:0x02d6` | same [~] | callee of 0x055ea · sim 0.97 |
| `0x049c4` |  | `h56:0x02ea` | same [~] | callee of 0x055ea · sim 0.77 |
| `0x04af2` |  | `h56:0x0450` | same [~] | callee of 0x05e50 · sim 0.99 |
| `0x04b4c` | str: "Cant allocate 1 ][ABORT]" | `h56:0x04a2` | same [~] | strings "[3][Cant allocate 1     ][AB" · calls GEM/VDI · sim 0.91 |
| `0x04b88` | load_out_region | `h56:0x04d4` | changed [~] | strings "OUT%-d.DAT" · calls GEM/VDI · sim 0.68 · Δ const ([200, 3000], [15, 180, 312, 2500]) |
| `0x04c8c` | save_out_region | `h56:0x0562` | changed [~] | strings "OUT%-d.DAT" · sim 0.76 · Δ const ([3000], [2500]) |
| `0x04cd0` |  | `h56:0x05a4` | changed [~] | callee of 0x04b88 · sim 0.75 · Δ const ([7], []) |
| `0x05028` |  | — | new [~] | calls GEM/VDI |
| `0x05230` |  | — | new [~] | calls GEM/VDI |
| `0x05274` | str: "Is opend ][sorry]" | `h53:0x0be6` | changed [~] | strings "[3][Is opend  ][sorry]", "[3][Cant allocate map  ][ABO" · calls GEM/VDI · sim 0.27 · Δ text (['MAP', 'OPEND'], ['SHAPE']); const ([10], []); calls ['0x04b4c'] |
| `0x0537e` | str: "Is closed ][sorry]" | `h56:0x0808` | same [~] | strings "[3][Is closed  ][sorry]" · calls GEM/VDI · sim 0.82 |
| `0x053ee` |  | — | new [~] | calls GEM/VDI |
| `0x0545a` |  | — | new [~] | calls GEM/VDI |
| `0x054b6` |  | `h56:0x083e` | same [?] | module order 0x0537e..0x055ea · calls GEM/VDI · sim 0.52 |
| `0x05504` |  | — | new [~] | calls GEM/VDI |
| `0x055ea` |  | `h56:0x0860` | changed [~] | callee of 0x04b88 · calls GEM/VDI · sim 0.73 · Δ const ([15, 317], []) |
| `0x056d6` |  | `h56:0x08e2` | same [~] | callee of 0x055ea · sim 0.88 |
| `0x05712` |  | `h77:0x04c0` | same [~] | callee of 0x04b88 · sim 0.63 |
| `0x0572c` | str: "out.rsc" | — | new [~] | calls GEM/VDI · strings "out.rsc" |
| `0x0574c` |  | `h56:0x0942` | same [~] | callee of 0x07344 · calls GEM/VDI · sim 0.84 |
| `0x05760` |  | `h56:0x0948` | changed [~] | module order 0x0574c..0x05af2 · calls GEM/VDI · sim 0.67 · Δ const ([], [13, 180, 184, 312, 316]) |
| `0x05816` |  | `h56:0x09e8` | changed [~] | module order 0x0574c..0x05af2 · calls GEM/VDI · sim 0.64 · Δ const ([], [19, 25]) |
| `0x05946` |  | `h56:0x0b2a` | changed [~] | module order 0x0574c..0x05af2 · calls GEM/VDI · sim 0.75 · Δ const ([], [5]) |
| `0x059f8` |  | `h56:0x0c08` | same [~] | module order 0x0574c..0x05af2 · sim 0.94 |
| `0x05a62` |  | `h56:0x0c76` | same [~] | module order 0x0574c..0x05af2 · calls GEM/VDI · sim 0.85 |
| `0x05af2` | dungeon_map_load | `h56:0x0d10` | changed [~] | strings "DNG.SAV", "DNGX.SAV" · calls GEM/VDI · sim 0.80 · Δ const ([14, 170], [64, 68, 165, 200, 320, 32000]) |
| `0x05d06` | dungeon_save (DNG.SAV/DNGX.SAV) | `h56:0x0ef0` | changed [~] | strings "DNG.SAV", "DNG.SAV" · calls GEM/VDI · sim 0.75 · Δ const ([152], [257]) |
| `0x05e50` |  | `h56:0x0f76` | changed [~] | callee of 0x11ab6 · calls GEM/VDI · sim 0.76 · Δ const ([], [230]) |
| `0x05efc` |  | — | new [~] | calls GEM/VDI |
| `0x05f8e` |  | `h41:0x10c6` | same [?] | callee of 0x11ab6 · calls GEM/VDI · sim 0.58 |
| `0x05fd0` |  | `h56:0x104a` | changed [?] | caller of 0x05a62 · calls GEM/VDI · sim 0.56 · Δ const ([], [99, 246]) |
| `0x06022` | overworld_step (terrain switch) | `h59:0x0000` | changed [C] | read: overworld step + terrain switch · calls GEM/VDI · sim 0.57 · Δ text ([], ['ENTER', 'OCEAN', 'RIVER', 'STYX', 'THAT', 'TOWN']); const ([15, 17, 34, 37, 2000, 4000, 6000], [80, 500, 595]); calls ['0x16dce', '0x055ea', '0x16530 |
| `0x06528` | event_lookup | `h59:0x0454` | changed [~] | strings "[1][   Event not found !  ][" · calls GEM/VDI · sim 0.89 · Δ const ([], [620]) |
| `0x065ba` |  | `h59:0x04e6` | same [~] | caller of 0x06022 · sim 0.95 |
| `0x065fa` |  | `h59:0x0520` | same [~] | caller of 0x06022 · sim 0.95 |
| `0x0663a` |  | `h59:0x055a` | same [~] | caller of 0x06022 · sim 0.95 |
| `0x0667a` |  | `h59:0x0594` | same [~] | caller of 0x06022 · sim 0.95 |
| `0x066ba` |  | `h59:0x05ce` | same [~] | callee of 0x06022 · sim 0.99 |
| `0x06724` |  | `h59:0x062c` | same [~] | callee of 0x06022 · calls GEM/VDI · sim 0.96 |
| `0x0676c` |  | `h59:0x066c` | same [~] | callee of 0x06022 · sim 0.89 |
| `0x06792` |  | `h59:0x06a8` | same [~] | callee of 0x06022 · sim 0.71 |
| `0x067c6` | use_potion | `h50:0x0000` | changed [~] | strings "WHO WILL USE POTION : ", "HEALING POTION %-2d" · sim 0.75 · Δ const ([10, 24], [9, 16, 59, 77, 270]); calls ['0x17210'] |
| `0x06ad6` | heal_member | `h50:0x037a` | changed [~] | strings "%s IS HEALED" · sim 0.74 · Δ const ([14, 16, 24], [270]) |
| `0x06b94` | str: "%-10.9s%-6.5sHP%4d/%-4dMP%4d/%-3d" | `h50:0x041c` | changed [~] | strings "%-10.9s%-6.5sHP%4d/%-4dMP%4d" · calls GEM/VDI · sim 0.49 · Δ text ([], ['HEALTH', 'REPORT']); const ([10, 12, 14, 34, 75], [20, 21, 22, 24, 83, 101, 156, 157, 270]) |
| `0x06cac` | party_status | `h50:0x055a` | changed [~] | strings "DAY OUT           %u", "[original text] %lu" · calls GEM/VDI · sim 0.59 · Δ const ([13, 14, 33, 128], [12, 17, 35, 63, 79, 112, 113, 114, 115, 175]); calls ['0x0168a', '0x17210'] |
| `0x06ecc` | cast_spell_field (DIVINE I/II) | `h50:0x07c4` | changed [C] | read: cast spell outside combat · calls GEM/VDI · sim 0.48 · Δ float ([0.0, 0.007, 0.02, 75.0], []); text (['ADVICE', 'APPEARS', 'DEMIGOD', 'GIVES'], ['ENOUGH', 'MAGIC', 'NOT']); const ([11, 21, 24, 25, 26, 30, 57, 58, 70, 105], [ |
| `0x07344` | main_loop (13 modes) | `h2:0x0000` | changed [C] | read: main loop, 13-mode dispatch · sim 0.49 · Δ text ([], ['DF0', 'DISK', 'DRIVE', 'ENOUGH', 'FOR', 'GAME', 'INSERT', 'INTO', 'MEMORY', 'NOT', 'PRESS', 'RESET']); const ([12], [13]) |
| `0x075b6` |  | `h62:0x000a` | same [~] | callee of 0x07344 · sim 0.80 |
| `0x075be` |  | `h2:0x0328` | same [~] | callee of 0x07344 · sim 0.93 |
| `0x075e2` | reset_repeat_orders (D-078) | `h2:0x0350` | same [~] | callee of 0x07344 · sim 0.98 |
| `0x07610` | combat_round (incl. Toss Rock) | `h35:0x0000` | changed [C] | read: combat round · sim 0.72 · Δ float ([0.02], []); text (['ROCK', 'THROWS'], []); const ([14, 19, 23, 27, 34, 38, 42], [104, 270]); calls ['0x18c6c'] |
| `0x08f0a` | monster_spell_sleep | `h38:0x0000` | changed [~] | strings "%s IS SLEPT" · sim 0.89 · Δ const ([14], [20]) |
| `0x09188` | resurrect_member | — | new [~] | strings "%s IS RESURRECTED" |
| `0x092f8` | monster_breath_special | `h38:0x0480` | changed [~] | strings "[original text]", "[original text]" · sim 0.91 · Δ const ([32], [8, 25, 29, 33]) |
| `0x095e8` | party_wide_special (field ids) | `h38:0x08ae` | changed [~] | strings "%s IS SLEPT" · sim 0.95 · Δ const ([], [270]) |
| `0x09794` | member_takes_damage | `h38:0x0ab6` | changed [~] | strings "%s TAKES %d DAMAGE" · sim 0.79 · Δ const ([16], [270]); calls ['0x16cbe'] |
| `0x09894` | kill_member ("%s DIES", ghost mark) | `h38:0x0bb4` | changed [~] | strings "%s DIES" · sim 0.90 · Δ const ([], [270]) |
| `0x099e0` | monster_takes_damage | `h38:0x0cf6` | same [~] | strings "%s TAKES %d DAMAGE", "%s DIES" · sim 0.94 |
| `0x09b42` |  | — | new [~] |  |
| `0x09bf4` | encounter_start | `h44:0x0000` | changed [C] | read: mode-11 handler (encounter start) in both main loops · sim 0.31 · Δ text (['ENCOUNTER', 'MINION', "PLUTO'S"], []); const ([10, 11, 36, 42, 58, 100, 284], []); calls ['0x0fd40'] |
| `0x09dac` | encounter_reaction | `h44:0x00d6` | changed [~] | strings "MONSTERS STOP AND TAKE YOUR ", "[original text]" · sim 0.93 · Δ calls ['0x0a25a'] |
| `0x0a124` |  | — | new [~] | calls GEM/VDI |
| `0x0a18c` |  | — | new [~] |  |
| `0x0a1a4` |  | — | new [~] |  |
| `0x0a1c2` |  | — | new [~] | calls GEM/VDI |
| `0x0a25a` |  | `h53:0x02a4` | same [~] | callee of 0x0a290 · sim 1.00 |
| `0x0a272` |  | `h44:0x05ec` | same [~] | callee of 0x0a290 · sim 0.72 |
| `0x0a290` | combat_victory (loot, amulets) | `h44:0x0614` | changed [C] | read: mode-12 handler (combat victory) in both main loops · sim 0.76 · Δ text (['AMULET', 'MINION', "PLUTO'S", 'TAKES'], []); const ([60, 80, 140, 284], [73, 270]) |
| `0x0a72e` | build_monster_groups | `h44:0x0998` | changed [C] | read: monster-group builder (I: one function; II splits the id check into 0x186de) · sim 0.62 · Δ text ([], ['MONSTER', 'WRONG']); const ([28, 29, 34, 35, 37], [9, 41, 55, 76]) |
| `0x0ad9a` | pluto_voice (endgame text) | — | new [~] | calls GEM/VDI |
| `0x0ae1a` |  | — | new [~] | calls GEM/VDI |
| `0x0ae86` | find_items | `h44:0x0fe4` | changed [~] | strings "YOU FIND %s. KEEP IT ?" · calls GEM/VDI · sim 0.49 · Δ text ([], ['NOTHING']); const ([12, 75, 122, 123, 133, 135, 136, 138, 364], [9]); calls ['0x05f8e'] |
| `0x0afb6` | combat_prep (@246/@248/@250) | `h44:0x10c0` | changed [~] | callee of 0x06022 · sim 0.66 · Δ const ([], [28, 32]) |
| `0x0b122` |  | `h44:0x11ee` | same [~] | callee of 0x07610 · sim 0.80 |
| `0x0b16e` | compact_monsters (state > 1 → 3) | `h44:0x1232` | same [~] | callee of 0x09bf4 · sim 0.92 |
| `0x0b2a4` | show_ranks | `h44:0x138c` | changed [~] | strings "RANK %1d : %1d  %s(S)" · calls GEM/VDI · sim 0.47 · Δ const ([38, 128], [101]); calls ['0x0168a', '0x16530'] |
| `0x0b39a` | str: "%-10.9s%c HP%4d/%-4dA:%-3.2dMP%4d/…" | `h44:0x1478` | changed [~] | strings "D S", "%-10.9s%c HP%4d/%-4dA:%-3.2d" · calls GEM/VDI · sim 0.40 · Δ text ([], ['HEALTH', 'REPORT', 'SHEALTH']); const ([6, 7, 8, 10, 12, 14, 128], [18, 20, 21, 22, 101, 270]); calls ['0x0168a', '0x16530'] |
| `0x0b4e4` | field_command_loop | `h47:0x0000` | changed [~] | caller of 0x06ecc · sim 0.61 · Δ text (['ALL', 'CEMETARY', 'COMBAT', 'FOLLOWS', 'KILLED', 'MEMBERS', 'PARTY'], []); calls ['0x17210'] |
| `0x0b662` |  | `h47:0x0172` | same [~] | callee of 0x07610 · sim 0.99 |
| `0x0b6b0` |  | — | new [~] |  |
| `0x0b7e6` | sound_fx | `h89:0x00d2` | same [~] | callee of 0x01d54 · sim 0.95 |
| `0x0b7fa` | sound_fx | `h89:0x00e0` | same [~] | callee of 0x017e4 · sim 0.95 |
| `0x0b80e` |  | — | new [~] |  |
| `0x0b8b0` |  | `h89:0x0132` | same [~] | callee of 0x0b7fa · sim 0.67 |
| `0x0b8d4` |  | `h89:0x0144` | same [~] | module order 0x0b8b0..0x0b9ce · sim 0.85 |
| `0x0b8f2` |  | — | new [~] |  |
| `0x0b92a` | sound_play | — | new [~] |  |
| `0x0b9b4` |  | — | new [~] |  |
| `0x0b9ce` | sound_fx | `h89:0x01cc` | same [~] | callee of 0x18b16 · sim 0.95 |
| `0x0b9e2` |  | — | new [~] |  |
| `0x0ba02` |  | — | new [~] |  |
| `0x0ba62` |  | — | new [~] |  |
| `0x0ba9a` | town_aging | `h5:0x02e0` | changed [~] | strings "[0][%8s has died      ][SORR", "[0][%8s has aged      ][OK]" · sim 0.89 · Δ const ([14, 33, 38, 86], [270]) |
| `0x0bd78` |  | `h5:0x1a44` | same [~] | callee of 0x12f2a · sim 0.96 |
| `0x0bda6` | town_entry_payout (shares) | `h8:0x16d6` | changed [~] | callee of 0x0ba9a · sim 0.79 · Δ const ([16], []) |
| `0x0bf5a` | start_or_continue (@218 load) | `h5:0x00ea` | same [~] | caller of 0x0bd78 · sim 0.96 |
| `0x0c0a8` | armory_restock | `h5:0x021e` | same [~] | callee of 0x0ba9a · sim 0.70 |
| `0x0c14a` |  | `h5:0x0708` | changed [~] | callee of 0x0d57c · sim 0.85 · Δ const ([5], []) |
| `0x0c1ea` | shop_menu | `h8:0x0076` | changed [~] | strings "WHO WILL SHOP?" · sim 0.74 · Δ calls ['0x04054'] |
| `0x0c2f2` |  | `h8:0x01ae` | same [~] | callee of 0x0c466 · sim 0.60 |
| `0x0c366` |  | `h8:0x01f6` | same [~] | callee of 0x0c466 · sim 0.67 |
| `0x0c466` | str: "%-11.10sGP:%-5u" | `h8:0x033a` | changed [~] | strings "%-11.10sGP:%-5u          ", "PTS:%-5d" · sim 0.71 · Δ text ([], ['NOTHING']); const ([364], []) |
| `0x0c526` |  | `h8:0x03f6` | same [~] | callee of 0x0c5c4 · sim 0.74 |
| `0x0c5c4` |  | `h8:0x04a8` | changed [~] | strings ":%3d %c ", "%-9s%13s" · sim 0.70 · Δ text ([], ['NOTHING']); const ([9, 364], [7]) |
| `0x0c6d2` |  | `h8:0x05c8` | same [?] | callee of 0x0e97a · sim 0.56 |
| `0x0c71a` | level_up (TOSS bonus @62) | `h5:0x1748` | changed [C] | read: level-up · sim 0.69 · Δ const ([62, 256], [5]) |
| `0x0c90c` | create_character (origin @284) | `h5:0x0832` | changed [~] | strings "          " · sim 0.76 · Δ float ([], [1310720.0]); const ([11, 15, 62, 284, 286, 302], [270]); calls ['0x010de', '0x04054'] |
| `0x0cdde` | guild_train | `h5:0x13b0` | changed [C] | read: Guild training, same 6-way class switch · sim 0.79 · Δ float ([-0.8, 33.0, 36.3], [0.8, 16.5, 18.1]); const ([5], []) |
| `0x0d0d2` | guild_learn_spell | `h5:0x10f2` | same [~] | strings "WHO WILL LEARN?" · sim 0.88 |
| `0x0d378` |  | `h5:0x07ac` | changed [?] | caller of 0x0cdde · sim 0.58 · Δ const ([8], [9]); calls ['0x00352'] |
| `0x0d420` |  | `h5:0x05aa` | same [~] | callee of 0x0ba9a · sim 0.94 |
| `0x0d442` | guild_add | `h5:0x0c6a` | changed [~] | strings "[original text] ADD?" · sim 0.89 · Δ const ([5], [92]); calls ['0x01a08'] |
| `0x0d57c` | guild_drop | `h5:0x0db4` | changed [~] | strings "[original text]" · sim 0.85 · Δ const ([6], []); calls ['0x04054'] |
| `0x0d610` |  | `h8:0x1586` | same [~] | caller of 0x02732 · sim 0.93 |
| `0x0d68a` | guild_purge | `h5:0x0e44` | changed [~] | strings "[original text]" · sim 0.75 · Δ const ([5], []); calls ['0x04054'] |
| `0x0d798` | score_check (orb/amulets, @282) | `h8:0x0d84` | changed [~] | caller of 0x00f64 · sim 0.61 · Δ const ([5, 8, 50, 60, 90, 150, 302], [39, 59, 72, 83, 88, 140, 141, 151]) |
| `0x0d94c` |  | — | new [~] |  |
| `0x0d9a4` | name_unique_check | `h5:0x0fd6` | changed [~] | strings "[0][Name is not unique    ][" · calls GEM/VDI · sim 0.64 · Δ const ([5], [32, 92, 97, 122]) |
| `0x0da82` | guild_rename | `h5:0x0efe` | changed [~] | strings "[original text]" · sim 0.78 · Δ const ([5], [92]); calls ['0x04054'] |
| `0x0db5a` |  | `h5:0x05c4` | changed [?] | caller of 0x03a50 · sim 0.58 · Δ const ([9, 12], [5, 10, 16]); calls ['0x0c1ea'] |
| `0x0dc7c` |  | `h8:0x160a` | same [~] | callee of 0x0ba9a · sim 0.89 |
| `0x0dd6e` |  | `h8:0x0c44` | changed [~] | callee of 0x0db5a · calls GEM/VDI · sim 0.91 · Δ const ([80], []) |
| `0x0ded4` | bank_withdraw | `h8:0x0608` | changed [~] | strings "[original text]" · sim 0.92 · Δ calls ['0x04054'] |
| `0x0dfa4` |  | `h8:0x1830` | same [~] | callee of 0x11ab6 · sim 0.95 |
| `0x0dfcc` | use_item (amulets/wand/orb) | `h8:0x1342` | changed [~] | strings "WHO WILL USE?", "ON WHOM?" · sim 0.65 · Δ const ([8, 10, 12, 14, 132, 134, 137, 140, 149, 200, 250], [150]); calls ['0x04054'] |
| `0x0e3f6` | town_cast_spell | `h8:0x0f94` | changed [~] | strings "WHO WILL CAST A SPELL?" · sim 0.74 · Δ const ([], [8, 43, 48, 53]); calls ['0x04054', '0x0e766'] |
| `0x0e4d4` | town_heal | `h8:0x1190` | changed [~] | strings "[original text] HEAL?" · sim 0.75 · Δ const ([14, 24], []); calls ['0x04054'] |
| `0x0e5fc` |  | `h8:0x06be` | same [~] | caller of 0x02080 · sim 0.97 |
| `0x0e69e` | guild_inspect | `h5:0x1950` | changed [~] | strings "[original text]" · sim 0.87 · Δ const ([5], []) |
| `0x0e766` | str: "%s IS RESURRECTED" | `h38:0x02ea` | changed [~] | strings "%s IS RESURRECTED" · sim 0.58 · Δ const ([92, 327], [12, 270]); calls ['0x0bd78'] |
| `0x0e83e` |  | `h8:0x078c` | changed [~] | caller of 0x0e97a · sim 0.79 · Δ const ([24, 230], []) |
| `0x0e97a` | str: "THIEF" | `h8:0x093c` | changed [~] | strings "%c %-11.9s%-4.3s%4d%4d%4d" · sim 0.67 · Δ text (['%-11.9S%-4.3S%4D%4D%4D'], ['%-10.9S%-4.3S%4D%4D%4D']); const ([], [49, 270]) |
| `0x0eaf4` |  | `h8:0x0b3a` | same [~] | callee of 0x0e97a · sim 0.66 |
| `0x0ebf6` |  | `h5:0x1a0e` | changed [~] | callee of 0x00352 · sim 0.69 · Δ const ([5], [92]) |
| `0x0ec36` | town_main (mode 1) | `h5:0x0000` | changed [?] | caller of 0x0db5a · calls GEM/VDI · sim 0.36 · Δ text ([], ['BLACK', 'INK', 'NOTHING', 'PELNOR.PIC', 'WITH']); const ([], [5]); calls ['0x1079c', '0x18eae'] |
| `0x0ed50` | load_twns_int | `h17:0x0000` | changed [~] | strings "TWNS.INT" · sim 0.73 · Δ text ([], ["CAN'T", 'FILE', 'OPEN', 'TWNS']) |
| `0x0eeb2` |  | `h17:0x0138` | same [~] | callee of 0x0ed50 · sim 1.00 |
| `0x0eee8` |  | — | new [~] |  |
| `0x0ef02` |  | — | new [~] |  |
| `0x0ef62` |  | `h71:0x0000` | same [~] | callee of 0x06022 · sim 0.80 |
| `0x0ef9e` |  | — | new [~] |  |
| `0x0efc0` | combat_orders (FIGHT) | `h71:0x0090` | changed [?] | caller of 0x0f298 · sim 0.43 · Δ text ([], ['%-40S']); const ([], [7, 23, 45, 83, 101, 157]) |
| `0x0f0dc` | combat_member_line | `h71:0x02b2` | changed [~] | strings "%-10.9sHP:%-4.3dMP:%-3d " · sim 0.63 · Δ const ([12, 16], [18, 22, 270]) |
| `0x0f14c` | pick_rank | `h71:0x0318` | changed [~] | strings " AT RANK ", " AT RANK" · sim 0.83 · Δ const ([], [163]) |
| `0x0f298` | combat_choose_action (TOSS) | `h71:0x0446` | changed [C] | read: combat action chooser · sim 0.81 · Δ text (['TOSS'], []); const ([25, 42], [18, 86]) |
| `0x0f7e0` |  | `h71:0x09bc` | same [~] | callee of 0x092f8 · sim 1.00 |
| `0x0f80e` |  | `h71:0x09f0` | same [~] | callee of 0x07610 · sim 0.98 |
| `0x0f864` | wilderness_inn | `h71:0x0ca2` | changed [~] | strings "%-10.9s[original text]", "%-10.9sIS RESTING" · calls GEM/VDI · sim 0.58 · Δ text ([], ['AFFORD', "CAN'T", 'ENTER', 'INN']); const ([5, 7, 100, 128, 130, 3000], [21, 101, 270]); calls ['0x16530', '0x055ea', '0x0168a'] |
| `0x0fac2` | lava_or_swim (mode 8 / 10) | `h71:0x0e88` | changed [C] | read: swim handler, II adds lava (arg 0) · calls GEM/VDI · sim 0.72 · Δ text (['%-10.9STAKES'], []); const ([10, 19, 128, 130], [21, 101, 270]); calls ['0x0168a', '0x055ea'] |
| `0x0fd40` | rnd100 = rand()/327 | `h71:0x103a` | same [~] | callee of 0x0a72e · sim 0.92 |
| `0x0fd58` |  | — | new [~] |  |
| `0x0fd82` |  | `h71:0x1050` | same [~] | callee of 0x07610 · sim 0.84 |
| `0x0fe18` | party_wiped_out (mode 6) | `h71:0x1118` | same [~] | callee of 0x07344 · sim 0.88 |
| `0x0fe38` | night_watch_encounter | `h74:0x0000` | changed [~] | strings "[original text]", "%s IS ON WATCH" · calls GEM/VDI · sim 0.73 · Δ text ([], ['ATTACK', 'BLACK', 'FIGHT', 'FLEE', 'KNIGHTS', 'LEAVE', 'RUN', 'SEE', 'SLEEPING', 'SPOT']); const ([8], [22, 27, 44, 77, 83, 91, 270]) |
| `0x10290` | dungeon_alloc | `h41:0x0000` | same [~] | strings "[1][   Can't allocate messag", "[1][   Can't allocate dng_ma" · calls GEM/VDI · sim 0.91 |
| `0x102f4` |  | `h41:0x0052` | same [~] | callee of 0x07344 · sim 0.89 |
| `0x10320` | dungeon_load (DNG%d/MESS%d) | `h41:0x007c` | same [~] | strings "DNG%-d", "MESS%-d" · sim 0.96 |
| `0x103aa` |  | `h41:0x01b0` | changed [~] | callee of 0x111d6 · sim 0.84 · Δ const ([], [270]) |
| `0x103f4` |  | — | new [~] | calls GEM/VDI |
| `0x1046c` | wall_message | `h41:0x019a` | same [~] | strings "THAT IS A WALL" · sim 0.98 |
| `0x10486` |  | `h41:0x011c` | changed [~] | callee of 0x111d6 · sim 0.83 · Δ const ([], [270]) |
| `0x104cc` |  | `h41:0x01f6` | changed [?] | module order 0x10486..0x10698 · calls GEM/VDI · sim 0.50 · Δ const ([10, 32, 128], [7, 11, 18, 25, 44, 93, 112]); calls ['0x0168a'] |
| `0x1066c` |  | `h41:0x02ec` | same [~] | module order 0x10486..0x10698 · sim 0.70 |
| `0x10698` | dungeon_main | `h41:0x0318` | changed [?] | callee of 0x07344 · sim 0.58 · Δ text ([], ['DUNGEON', 'ENTER']); const ([7, 30], []); calls ['0x16530', '0x055ea'] |
| `0x1079c` | dungeon_exit (save map?) | `h41:0x0414` | changed [~] | strings "AN EXIT. TAKE IT ?", "WOULD YOU LIKE TO SAVE THE D" · calls GEM/VDI · sim 0.70 · Δ const ([], [215]) |
| `0x1087c` |  | `h41:0x0516` | same [~] | caller of 0x1046c · sim 0.96 |
| `0x10956` |  | `h41:0x05f0` | same [~] | callee of 0x11ab6 · sim 0.97 |
| `0x109f0` |  | `h41:0x06a0` | same [~] | module order 0x10956..0x10b60 · calls GEM/VDI · sim 0.98 |
| `0x10b60` |  | `h41:0x07f8` | changed [~] | caller of 0x11ab6 · sim 0.66 · Δ const ([12], [6, 34, 50]) |
| `0x10d7a` |  | `h41:0x0a58` | same [~] | callee of 0x10b60 · sim 0.60 |
| `0x10db0` | trap_disarm | `h41:0x0a78` | changed [~] | strings "SPOTS A TRAP. DISARM IT ?", "[original text]" · sim 0.83 · Δ text (['%S'], ['%-10.9S']); const ([19, 128, 130, 1000], [21, 101, 270]); calls ['0x05f8e', '0x0168a'] |
| `0x11074` |  | `h89:0x00ee` | changed [~] | callee of 0x11ab6 · calls GEM/VDI · sim 0.62 · Δ const ([19], [27]) |
| `0x110be` |  | — | new [~] |  |
| `0x110f0` |  | `h41:0x0d26` | same [~] | callee of 0x11ab6 · sim 0.91 |
| `0x11170` |  | `h41:0x0db4` | same [~] | callee of 0x11ab6 · sim 1.00 |
| `0x1119a` | stairs | `h41:0x0ddc` | changed [~] | strings "[original text] %-2d TO %-" · sim 0.66 · Δ const ([], [216, 217]) |
| `0x111d6` | door_lock | `h41:0x0e26` | changed [~] | strings "[original text]", "PICKS THE LOCK" · sim 0.93 · Δ const ([], [185, 199]) |
| `0x113c8` |  | `h41:0x1052` | same [~] | module order 0x111d6..0x11aa0 · sim 0.96 |
| `0x11444` |  | — | new [~] | calls GEM/VDI |
| `0x1159e` |  | `h41:0x10e6` | same [~] | module order 0x111d6..0x11aa0 · sim 0.70 |
| `0x116aa` | dungeon_message | `h41:0x11e4` | changed [?] | module order 0x111d6..0x11aa0 · calls GEM/VDI · sim 0.53 · Δ const ([5, 6, 8, 10, 31, 172, 180, 214, 2000], [22, 33, 44, 45, 55, 83, 101, 110]); calls ['0x05f8e'] |
| `0x11aa0` | str: "[original text]" | `h41:0x15a8` | same [~] | strings "[original text]" · sim 0.93 |
| `0x11ab6` | dungeon_event | `h41:0x15b8` | changed [~] | strings "REPORT :", "A SPELL IS CAST" · sim 0.90 · Δ const ([7, 17, 18, 22, 130, 192, 1000, 1448], [9, 101]) |
| `0x12078` | field_id_map (record word by id, DOS OV2 0x1ba5) | `h41:0x1ba4` | changed [~] | strings "[1][   Not used but activate" · calls GEM/VDI · sim 0.82 · Δ const ([8, 12, 53, 84, 134, 220], [24, 36, 82, 112, 240, 246, 324]) |
| `0x123fc` |  | `h41:0x1e88` | same [~] | callee of 0x11ab6 · sim 0.94 |
| `0x1241c` |  | — | new [~] |  |
| `0x1245e` |  | `h41:0x1eac` | changed [~] | module order 0x123fc..0x125ec · calls GEM/VDI · sim 0.92 · Δ const ([], [215]) |
| `0x125ec` |  | `h41:0x2042` | same [~] | callee of 0x0a290 · sim 0.95 |
| `0x12678` | party_death_screen | `h32:0x0000` | changed [~] | strings "DECOR.PIC" · calls GEM/VDI · sim 0.87 · Δ const ([7, 14, 20], [60]) |
| `0x127fc` | destroyed | `h32:0x018a` | same [~] | strings "DESTROYED" · calls GEM/VDI · sim 0.97 |
| `0x12872` |  | `h32:0x0216` | same [~] | callee of 0x128f0 · sim 0.97 |
| `0x128a2` | resurrected | `h32:0x024a` | same [~] | strings "RESURRECTED" · sim 0.98 |
| `0x128f0` | granted_undead | `h32:0x02a4` | same [~] | strings "[original text]" · sim 0.98 |
| `0x12950` | str: "%s YOU HAVE BEEN %s" | `h32:0x030e` | changed [~] | strings "%s YOU HAVE BEEN %s" · sim 0.54 · Δ const ([], [270]); calls ['0x12a16'] |
| `0x1298e` | hero_title "%s - the %s from %s" | — | new [~] | strings "%s - the %s from %s" |
| `0x12a16` |  | `h26:0x0b0e` | changed [~] | caller of 0x16cbe · calls GEM/VDI · sim 0.60 · Δ const ([10, 3000], [69, 111, 112]) |
| `0x12a8a` | defeat_portrait_row | `h32:0x036e` | same [~] | callee of 0x12f2a · calls GEM/VDI · sim 0.91 |
| `0x12b5e` | defeat_ghost_row | `h32:0x043e` | same [~] | callee of 0x12bea · sim 0.98 |
| `0x12b8e` | defeat_slot (ghost or race portrait) | `h32:0x046c` | changed [?] | callee of 0x128f0 · sim 0.48 · Δ const ([], [6]); calls ['0x18326'] |
| `0x12bea` | defeat / astral plane | `h32:0x055c` | changed [~] | strings "decor.pic", "decor.pic" · calls GEM/VDI · sim 0.61 · Δ const ([3000, 32000], [14, 19, 39, 48]); calls ['0x13526', '0x12a16', '0x16cbe'] |
| `0x12e78` |  | — | new [~] |  |
| `0x12efa` |  | — | new [~] | calls GEM/VDI |
| `0x12f2a` | endgame_filmon | `h32:0x06d0` | changed [C] | read: final ending (I: Zeus "WELL DONE, MY SMALL FRIENDS", final.pic; II: Filmon, final2.pic) · calls GEM/VDI · sim 0.30 · Δ text (['ADDRESSES', 'BUT', 'CAN', 'CROWD', 'CROWD.PIC', 'EACH', 'FERRONRAH', 'FILMON', 'FINAL2.PIC', 'GRE |
| `0x13526` | roster_save | `h20:0x0000` | changed [~] | callee of 0x12f2a · sim 0.60 · Δ const ([], [257]) |
| `0x13608` | scrolls_load | `h20:0x00a0` | changed [~] | strings "scrolls.dtx", "[0][Can't open SCROLLS file " · calls GEM/VDI · sim 0.62 · Δ const ([840], [800]) |
| `0x136a2` | printer_line (Cprnout) | `h20:0x011a` | same [?] | module order 0x13608..0x13758 · sim 0.45 |
| `0x13710` | str: "PELNOR.PIC" | — | new [~] | strings "PELNOR.PIC" |
| `0x13758` | disk_error_alert | `h20:0x0174` | changed [~] | strings "[3][                / There " · calls GEM/VDI · sim 0.79 · Δ text ([], ['O.K']) |
| `0x137c0` | vbl_install (colour cycling) | — | new [C] |  |
| `0x137e6` | vbl_cycle_off (+ data after 0x13862) | — | new [C] |  |
| `0x151ee` | (data, not code) | — | new [~] |  |
| `0x1640c` |  | — | new [~] | calls GEM/VDI |
| `0x16456` |  | — | new [~] | calls GEM/VDI |
| `0x164aa` |  | — | new [~] | calls GEM/VDI |
| `0x164f4` |  | — | new [~] |  |
| `0x16510` |  | — | new [~] |  |
| `0x16530` |  | `h62:0x0012` | same [~] | callee of 0x06cac · calls GEM/VDI · sim 0.67 |
| `0x1657c` |  | — | new [~] | calls GEM/VDI |
| `0x16620` |  | `h23:0x0d28` | same [~] | callee of 0x0f298 · calls GEM/VDI · sim 0.65 |
| `0x1668a` |  | — | new [~] | calls GEM/VDI |
| `0x16778` |  | — | new [~] |  |
| `0x16790` |  | — | new [~] | calls GEM/VDI |
| `0x16844` |  | — | new [~] |  |
| `0x16872` |  | — | new [~] | calls GEM/VDI |
| `0x168a0` |  | — | new [~] | calls GEM/VDI |
| `0x16920` |  | `h23:0x0e6e` | changed [?] | callee of 0x067c6 · sim 0.46 · Δ const ([9, 28], []) |
| `0x1693c` |  | — | new [~] |  |
| `0x1695c` |  | `h62:0x027a` | changed [~] | callee of 0x067c6 · sim 0.75 · Δ const ([28], [17]) |
| `0x1697c` | party_hp_mp_window | `h62:0x028e` | changed [~] | strings "%-10.9s%-6.5sHP%4d/%-4dMP%4d" · sim 0.63 · Δ const ([10, 12, 14, 128], [20, 22, 23, 38, 39, 270]); calls ['0x0168a'] |
| `0x16b4e` |  | `h62:0x049c` | same [?] | module order 0x1697c..0x16bba · sim 0.47 |
| `0x16bba` |  | `h62:0x0508` | changed [~] | callee of 0x0f298 · calls GEM/VDI · sim 0.63 · Δ const ([9, 13, 45], [6, 11, 38, 39]) |
| `0x16c4a` |  | — | new [~] | calls GEM/VDI |
| `0x16cbe` | wait_ms (evnt_multi timer; a key ends it) | `h23:0x0e32` | changed [?] | callee of 0x116aa · calls GEM/VDI · sim 0.48 · Δ const ([5, 255], []) |
| `0x16d56` |  | — | new [~] | calls GEM/VDI |
| `0x16dce` |  | `h71:0x112e` | changed [?] | callee of 0x0fac2 · calls GEM/VDI · sim 0.51 · Δ const ([], [7, 83, 101, 156, 157]) |
| `0x16eae` |  | — | new [~] |  |
| `0x16ef6` | str: "%s ][ NO YES]" | — | new [~] | calls GEM/VDI · strings "[0][%s     ][ NO / YES]" |
| `0x16f2c` |  | — | new [~] | calls GEM/VDI |
| `0x16fea` |  | `h65:0x0000` | changed [~] | callee of 0x0b4e4 · calls GEM/VDI · sim 0.63 · Δ const ([5, 10, 16, 39], [6, 7, 8, 20, 50]) |
| `0x171b4` |  | — | new [~] |  |
| `0x171dc` |  | `h65:0x0168` | same [~] | callee of 0x0fe38 · sim 0.93 |
| `0x17210` |  | `h65:0x01a6` | changed [~] | callee of 0x07344 · sim 0.84 · Δ calls ['0x05f8e'] |
| `0x17266` |  | — | new [~] |  |
| `0x172c8` |  | — | new [~] | calls GEM/VDI |
| `0x173bc` |  | — | new [~] | calls GEM/VDI |
| `0x17536` | printout | `h29:0x093e` | changed [~] | strings "LEVEL%3d %s %s %s", "%7u" · calls GEM/VDI · sim 0.75 · Δ text (['GELNOR'], []); const ([7, 11, 15, 21, 284, 302], [70, 72, 76, 78, 124, 125, 126, 127, 128, 129, 130, 131]) |
| `0x1791a` |  | — | new [~] | calls GEM/VDI |
| `0x179d6` |  | — | new [~] | calls GEM/VDI |
| `0x17b8c` |  | — | new [~] | calls GEM/VDI |
| `0x17be4` |  | — | new [~] | calls GEM/VDI |
| `0x17c92` |  | — | new [~] | calls GEM/VDI |
| `0x17d9c` | combat_spell_picker | `h23:0x0570` | changed [~] | floats · calls GEM/VDI · sim 0.27 · Δ text ([], ['%-29.29S']); const ([8, 9, 11, 20, 128], [12, 32, 33, 35, 70, 71, 77]); calls ['0x0168a'] |
| `0x18070` | str: "EXIT" | `h29:0x0e90` | changed [~] | strings "EXIT", "[original text]" · calls GEM/VDI · sim 0.60 · Δ text (['EXIT'], []); const ([204], [86, 96]) |
| `0x180d0` |  | `h77:0x04d2` | same [~] | callee of 0x0a72e · sim 0.96 |
| `0x18112` |  | — | new [~] |  |
| `0x1812c` |  | — | new [~] |  |
| `0x1814e` |  | `h53:0x0000` | same [~] | callee of 0x12a8a · sim 0.94 |
| `0x18176` |  | `h53:0x0030` | same [~] | module order 0x1814e..0x181ca · sim 0.97 |
| `0x181ca` |  | `h53:0x0086` | same [~] | callee of 0x1823c · calls GEM/VDI · sim 0.84 |
| `0x1823c` |  | `h53:0x00fc` | same [~] | callee of 0x0a290 · sim 0.99 |
| `0x18288` |  | `h53:0x0140` | same [~] | module order 0x1823c..0x1838e · calls GEM/VDI · sim 0.90 |
| `0x18326` |  | `h53:0x020a` | same [~] | module order 0x1823c..0x1838e · calls GEM/VDI · sim 0.84 |
| `0x1836c` |  | `h56:0x0170` | changed [?] | callee of 0x18458 · sim 0.47 · Δ const ([], [6]) |
| `0x1838e` |  | `h53:0x0274` | same [~] | callee of 0x0a290 · calls GEM/VDI · sim 0.96 |
| `0x183c4` |  | `h44:0x05fe` | same [~] | callee of 0x0a290 · calls GEM/VDI · sim 1.00 |
| `0x183e6` |  | `h53:0x02c0` | same [~] | callee of 0x09dac · sim 0.61 |
| `0x183ee` |  | `h38:0x0e72` | same [?] | callee of 0x07610 · calls GEM/VDI · sim 0.50 |
| `0x18416` |  | `h53:0x02da` | same [~] | callee of 0x07610 · calls GEM/VDI · sim 0.87 |
| `0x18446` |  | — | new [~] |  |
| `0x18458` |  | `h53:0x02fc` | changed [~] | module order 0x18416..0x185c4 · calls GEM/VDI · sim 0.73 · Δ const ([11, 187, 320], [14, 182, 313]) |
| `0x185c4` |  | `h53:0x046e` | same [~] | callee of 0x07610 · sim 1.00 |
| `0x185d6` |  | `h53:0x0478` | changed [~] | module order 0x185c4..0x188fc · calls GEM/VDI · sim 0.60 · Δ const ([], [6]); calls ['0x18326'] |
| `0x186a8` |  | `h53:0x0622` | same [~] | module order 0x185c4..0x188fc · calls GEM/VDI · sim 1.00 |
| `0x186de` | check_monster_id "Wrong monster" | — | new [C] | calls GEM/VDI · strings "[1][Wrong monster ][You ....]" |
| `0x18856` | debug_alert_bad_row | `h53:0x07de` | changed [~] | module order 0x185c4..0x188fc · calls GEM/VDI · sim 0.63 · Δ text (['BAD', 'COUNT', 'NIC', 'ROW'], []) |
| `0x188fc` | portrait_action (0 ghost mark, 1 attack, 2 cast) | `h53:0x085e` | changed [~] | callee of 0x07610 · calls GEM/VDI · sim 0.65 · Δ const ([500], [30]); calls ['0x186a8', '0x18326', '0x16cbe'] |
| `0x18a8a` |  | `h53:0x0a0e` | same [~] | module order 0x188fc..0x18b16 · sim 0.92 |
| `0x18ab8` |  | `h53:0x0a54` | same [~] | module order 0x188fc..0x18b16 · calls GEM/VDI · sim 0.84 |
| `0x18b16` | combat_hit_display (Polish debug alert) | `h53:0x0ab8` | changed [~] | callee of 0x07610 · calls GEM/VDI · sim 0.63 · Δ text (['OGIENIEK', 'SADYSTA', 'ZLY'], []); calls ['0x0ba9a'] |
| `0x18c6c` |  | `h8:0x12bc` | changed [?] | caller of 0x0ba9a · calls GEM/VDI · sim 0.35 · Δ text ([], ['TOWN', 'TRANSPORT', 'WHICH']); const ([10, 100], [12, 86, 99]) |
| `0x18cc2` |  | `h53:0x0b80` | changed [?] | callee of 0x07610 · calls GEM/VDI · sim 0.38 · Δ const ([12], []); calls ['0x16cbe'] |
| `0x18e2a` | alloc_shapes | `h53:0x1060` | changed [~] | strings "[3][Cant allocate shape   ][" · calls GEM/VDI · sim 0.44 · Δ const ([], [6, 3200]) |
| `0x18eae` | load_shapes (MSTR*/PARTY*.PAT) | `h53:0x0c82` | changed [~] | strings "MSTR2.PAT", "PARTY1.PAT" · sim 0.42 · Δ text (['MSTR1.PAT', 'MSTR2.PAT'], []); calls ['0x18e2a'] |
| `0x18f2a` |  | — | new [~] |  |
| `0x18f7a` |  | `h53:0x059e` | changed [?] | callee of 0x188fc · calls GEM/VDI · sim 0.46 · Δ calls ['0x186a8'] |
| `0x19d42` | rulebook_quiz | — | new [C] | calls GEM/VDI · strings "[3][/PLEASE ANSWER/THE FOLLOWI" |

### 8.2 Amiga I game functions without a II partner (126)

Mostly Amiga platform code: h77 (Intuition / graphics setup), h80 (menus, requesters, "About Phantasie"), h86 (requesters, sprites, HEALTH REPORT window), h89 (audio device), h53 (shape loading: the ST loads `MSTR*.PAT` its own way). Game-logic ones: `h71:0x0a50` (magical pool, removed in II), small helpers in h23/h26/h44/h56/h62/h65 that II inlined or replaced with GEM dialogs.

| Amiga I `game` | Size (insns) | Name / strings |
|---|---|---|
| `h0:0x0000` | 84 |  |
| `h0:0x013a` | 26 |  |
| `h0:0x01a2` | 5 |  |
| `h0:0x01b4` | 6 | str: "dos.library" |
| `h8:0x0000` | 23 |  |
| `h8:0x10ac` | 59 |  |
| `h23:0x0000` | 52 |  |
| `h23:0x08cc` | 5 |  |
| `h23:0x0a98` | 29 |  |
| `h23:0x0ca0` | 13 |  |
| `h23:0x0cd0` | 24 |  |
| `h23:0x0d4e` | 21 |  |
| `h23:0x0d9a` | 23 |  |
| `h23:0x0de6` | 23 |  |
| `h26:0x00ee` | 49 | str: "NOTHING" |
| `h26:0x0238` | 13 |  |
| `h26:0x026e` | 13 |  |
| `h26:0x02b6` | 14 |  |
| `h26:0x02ec` | 14 | str: "WHO WILL SHOP?" |
| `h26:0x0b80` | 18 |  |
| `h26:0x0bea` | 16 |  |
| `h26:0x0c72` | 126 |  |
| `h26:0x0e30` | 67 |  |
| `h29:0x0322` | 290 |  |
| `h29:0x1302` | 3 |  |
| `h32:0x0340` | 12 |  |
| `h41:0x015e` | 18 | str: "%s[YES NO]" |
| `h44:0x0466` | 32 |  |
| `h44:0x04d4` | 38 |  |
| `h44:0x0556` | 44 |  |
| `h53:0x024e` | 13 |  |
| `h53:0x02c2` | 5 |  |
| `h53:0x0652` | 124 |  |
| `h53:0x0c52` | 14 | str: "CANT ALLOCATE SHAPE[ABORT]" |
| `h53:0x0ccc` | 45 | str: "CAN'T OPEN MSTRx.PAT FILE[ABORT]" |
| `h53:0x0d5e` | 208 | str: "MSTR1.PAT" |
| `h56:0x0000` | 80 |  |
| `h56:0x00e8` | 34 |  |
| `h56:0x0140` | 8 |  |
| `h56:0x0158` | 8 |  |
| `h56:0x0192` | 10 |  |
| `h56:0x01ac` | 9 |  |
| `h56:0x06bc` | 81 |  |
| `h56:0x07ca` | 15 | str: "IS OPENED[SORRY]" |
| `h56:0x0938` | 3 |  |
| `h56:0x1000` | 16 |  |
| `h56:0x1030` | 8 |  |
| `h62:0x0000` | 2 |  |
| `h62:0x0048` | 36 |  |
| `h62:0x00cc` | 45 | str: "THRUST" |
| `h62:0x0170` | 77 |  |
| `h62:0x0576` | 14 |  |
| `h62:0x05a0` | 30 |  |
| `h65:0x01f4` | 41 |  |
| `h65:0x027e` | 17 |  |
| `h71:0x0036` | 27 |  |
| `h71:0x0a50` | 157 | str: "A MAGICAL POOL" |
| `h77:0x0000` | 91 | str: "intuition.library" |
| `h77:0x0178` | 60 |  |
| `h77:0x0288` | 24 |  |
| `h77:0x02de` | 48 | str: "cover1.pic" |
| `h77:0x03a6` | 45 |  |
| `h77:0x0466` | 20 |  |
| `h77:0x04a4` | 7 |  |
| `h77:0x0516` | 4 |  |
| `h77:0x0524` | 31 | str: "cover2.pic" |
| `h77:0x05ac` | 30 |  |
| `h77:0x0614` | 61 |  |
| `h77:0x06dc` | 98 |  |
| `h77:0x0834` | 65 | str: "No enough memory for the temp. bit…" |
| `h77:0x094a` | 85 | str: "MODE NOT 0 OR 3 or 5[O K]" |
| `h77:0x0a60` | 29 |  |
| `h77:0x0ac0` | 14 | str: "FREE MEMORY %d[O.K.]" |
| `h77:0x0af4` | 63 |  |
| `h77:0x0bc0` | 50 |  |
| `h77:0x0c5e` | 41 |  |
| `h77:0x0cfe` | 76 |  |
| `h77:0x0dfe` | 84 | str: "NO FREE SPRITE![SORRY]" |
| `h77:0x0f2a` | 86 | str: "NO FREE SPRITE![SORRY]" |
| `h77:0x106c` | 8 |  |
| `h77:0x108e` | 20 |  |
| `h77:0x10de` | 14 |  |
| `h77:0x110e` | 3 |  |
| `h77:0x111e` | 3 |  |
| `h77:0x112e` | 74 |  |
| `h77:0x1230` | 74 |  |
| `h77:0x1332` | 64 |  |
| `h80:0x0000` | 71 | str: "About Phantasie" |
| `h80:0x0152` | 237 | str: "SYSTEM ERROR WILL OCCUR !!! PLEASE…" |
| `h80:0x04e4` | 28 |  |
| `h80:0x0546` | 117 |  |
| `h80:0x070c` | 73 |  |
| `h80:0x084a` | 19 |  |
| `h80:0x0892` | 38 |  |
| `h80:0x097a` | 8 |  |
| `h80:0x0992` | 10 |  |
| `h80:0x09ae` | 34 | str: "Auto save" |
| `h80:0x0a52` | 25 | str: "Guild Options" |
| `h80:0x0b48` | 36 | str: "Info" |
| `h80:0x0bfe` | 36 | str: "Info" |
| `h80:0x0cb4` | 32 | str: "NO MEMORY FOR LOGO[SORRY]" |
| `h80:0x0d2a` | 34 |  |
| `h80:0x0db6` | 91 |  |
| `h80:0x0f08` | 20 |  |
| `h86:0x0000` | 74 |  |
| `h86:0x0132` | 79 |  |
| `h86:0x025c` | 58 |  |
| `h86:0x032a` | 129 |  |
| `h86:0x0502` | 26 |  |
| `h86:0x055a` | 123 | str: "HEALTH REPORT:" |
| `h89:0x0000` | 32 |  |
| `h89:0x0156` | 1 |  |
| `h89:0x0158` | 39 |  |
| `h89:0x01ca` | 1 |  |
| `h89:0x01da` | 2 |  |
| `h89:0x01de` | 62 |  |
| `h89:0x02f4` | 4 |  |
| `h89:0x0304` | 311 |  |
| `h89:0x07ca` | 11 |  |
| `h89:0x07f4` | 10 |  |
| `h89:0x0820` | 57 | str: "audio.device" |
| `h89:0x08ee` | 18 |  |
| `h89:0x0926` | 12 |  |
| `h89:0x0954` | 12 |  |
| `h89:0x098a` | 16 |  |
| `h89:0x09bc` | 70 |  |
