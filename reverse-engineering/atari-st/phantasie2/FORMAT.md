# Phantasie II (Atari ST v1.0): format notes

Phase 09 T3. Plan and phase map: `docs/plan/03b-phantasie2-re.md`. Files and how they were extracted:
`unpacked/README.md`. Extracted tables: `extracted/` (`tools/scripts/extract_st_phantasie2.py`).

Tags as in `docs/plan/RE-REFERENCE.md`: `[C]` confirmed (code or data read directly), `[~]` probable (strong evidence,
not proven), `[?]` open. "Static" means read from the binary; §6 (the transfer, s76) and §10 (graphics, s78) are checked in an emulator (Hatari).

## 1. Addresses (don't mix these up)

| Convention | Formula | Used for |
|---|---|---|
| **Image address** (default in these notes) | text starts at 0; data at `text_size` = `0x1e298`; bss at `0x2468c` | what relocated pointers hold before TOS adds the load base; objdump on the text |
| **data+X** | image = `0x1e298 + X` | data-segment tables below |
| **File offset** | image + `0x1c` (PRG header) | hex dumps of `PHANT.PRG` |

`tools/scripts/st_prg.py` reads the header and the fixup table (`load_prg()`; `Prg.relocs` = image offsets of every
relocated longword). **Pointer tables must be resolved through the fixups**, not by assuming string order: a run of
consecutive fixups in the data segment is a pointer array (that is how every table in §3 was found).

`UTILIT2.PRG`: text `0x2eac`, data `0x584`, bss `0x2828`, same conventions (strings at image `0x3008+`).

## 2. Programs `[C]`

- **`PHANT.PRG`** — the game. GEMDOS PRG, **not packed** (strings in the clear). Header: text `0x1e298`, data `0x63f4`,
  bss `0x3cbc`, no symbol table, 6485 fixups. Built with DRI's C compiler: startup `2a4f 2a6d 0004` plus the
  "CP/M-68K(tm), Version 1.2 … Digital Research" runtime string; every function opens with `LINK A6` / ends with `UNLK`.
  OS bindings: XBIOS at image `0x19076`, BIOS `0x19086`, GEMDOS `0x19096` (`jsr` targets; `move.w #n,-(sp)` before the
  call gives the function). XBIOS functions used: 6, 7, 16, 28, 32, 35, 37 (no Floprd/Flopver). GEM AES alert strings
  (`[n][text][buttons]`) carry most UI messages; three `.RSC` files hold menus and dialogs.
- **`START.PRG`** — loader: Getrez (refuses non-low resolution, "Sorry! You have to switch to LOW resolution"), sets a
  palette, loads `cover1.pic` / `cover2.pic` into screen memory, Pexec `phant.prg`. s102, the whole boot `[C]`: Mshrink;
  Getrez; line-A init; Physbase; Setpalette(`0x176`, all black); **Dosound(`0x1b3`)** (XBIOS 32: the title tune, §7.1,
  played by the remaster s108); `COVER1` to the screen; Setpalette(`0x156`, `start`); **251 Vsyncs** (`move.w #250,d3` +
  `dbf`); `COVER2` to the screen + 10400 (line 65); Pexec `PHANT.PRG`. No key is read: the composite stays up while
  `PHANT.PRG` loads and then until the title tune ends (s156 `[C]`: `PHANT.PRG`'s start-up `0xec36` waits at `0xb8f2`
  for the running Dosound script, §7.1). The remaster's title splash: `COVER1` for 251 VBLs at the pack's
  `vbl_hz` (60, decided s109: see the door row in §10), then the composite until the tune's end (64.62 s after its start).
- **`UTILIT2.PRG`** — "PHANTASIE BACKUP UTILITY" (`BACKUP.RSC`): back to game, characters to / from a backup disk, new game
  from scratch, and **TRANSFER CHARACTERS FROM PHANTASIE 1 TO PHANTASIE 2 / FROM PHANTASIE 2 TO PHANTASIE 1**. Contains the
  Motorola mc68343 FFP float library (FFP constants below). §6 decodes the conversion.
- Debug alerts in `PHANT.PRG` are partly Polish ("[4][ Zly ogieniek ][Sadysta ]", data+`0x5c6d`): the ST port's
  programmer. Same code family as the Amiga I `game` `[~]`: identical 58-byte monster records (§4) and the same
  LINK/UNLK C style, but the Amiga build has no DRI runtime string (compiler not identified).

## 3. Master tables in `PHANT.PRG` `[C]`

All found as fixup runs in the data segment and dumped by `extract_st_phantasie2.py` to `extracted/names.json` /
`extracted/monsters.json`.

| Table | Where | Entries | vs Phantasie I |
|---|---|---|---|
| Item value factors | data+`0x180`, u16 BE × 181 | 181 | **identical** to DOS DS `0x0d54` (all 181) |
| Item names | pointer run data+`0x2ec` (181 × long) | 181 | ids 0–100 (shields, armour, weapons) and potions **identical names**; 101–110 renamed `HEAL POT1…9`, `HEALING10`; 121–140 = 20 scroll slots, 14 lettered `SCROLL A…N` + 6 blank `SCROLL `; 141–148 `AMULET 1…8`; 149 `FLMN WAND`; 150 `---ORB---`; 151–180 new treasure names (`QUARTZ` … `GOD RING`) |
| Classes / races / status / spells | pointer run data+`0x5c4` (85 × long) | 6 + 16 + 3 + 60 | classes, 16 races (15 = UNDEAD), DEAD/OK/SLEEP identical. Spell slots 0–53 identical names; **slot 56 = DIVINE II (new), slot 57 = DIVINE I** (as DOS slot 57); 54, 55, 58, 59 empty |
| Monsters | data+`0x1c76`, 80 × 58 B | 80 | 50 names shared with I (24 identical to DOS I, 25 identical to Amiga I), 30 new (§4) |

Spell numbers as players see them are slot + 1 (manual: Wizards keep "#5" = FIREFLASH I = slot 4). So in II
**DIVINE I = #58 and DIVINE II = #57** `[C]` (table position; whether the game prints the number is `[?]`).

Other strings worth knowing (data+X): end-of-quest lines 0x10c8 ("not ready to destroy the orb", "heroes of Ferronrah",
"[original text] Gelnor also"); `TEMPLE` 0x11bc (the title of the spell picker that Guild Learn opens; the Amiga I `game` has the same string and
screen, so it is **not** a Temple service: `logic-specs/00-function-map.md` §4) `[C]`; deities `DEMIGOD ATHENA APOLLO
ZEUS` 0x1bde (DIVINE I's advice-giver, `00-function-map.md` §5.4); combat strings `%s THROWS A ROCK` 0x3041, `TOSS` 0x4a53; lava `L A V A :` 0x4a92; Pluto's farewell
0x33ad; Filmon ending 0x5034; origin names `Gelnor / Ferronrah / Netherworld` 0x56de and on the character sheet
(`ORIGIN: %s` 0x1644); the **rulebook quiz** 0x5d62–0x6001 (nine `[2][question (PAGE n)][a|b|c]` alerts, then
"I GIVE YOU ONE MORE CHANCE" / "YOUR LAST CHANCE" / "PLEASE ANSWER THE FOLLOWING QUESTIONS USING THE RULEBOOK").
The quiz runs once per program run, on the (N + 1)-th entry (N random 2–7) into main-loop mode 3 (the dungeon field
loop); two wrong answers hang the
game (`rulebook_quiz` `0x19d42`, `00-function-map.md` §5.5) `[C]` static.

## 4. Monster record (58 bytes, big-endian) `[C]`

Byte-identical layout to the Amiga I `game` table (file `0xC88`, 58-byte stride; `amiga/FORMAT.md`), which is the DOS
57-byte record (`dos/logic-specs/data-tables.md` §Monsters) with **one pad byte at +0x23** so the following i16 fields
stay even. Field meanings are carried over from the DOS spec `[~]` (same values for the shared monsters prove the
layout, not that II's combat code reads each field the same way).

| Off | Size | Field (DOS name) | DOS off |
|---|---|---|---|
| 0x00 | 20 | name | 0x00 |
| 0x14 | i16 | armor | 0x14 |
| 0x16 | i16 | damage | 0x16 |
| 0x18 | i16 | level | 0x18 |
| 0x1a | u32 | XP per kill | 0x1a |
| 0x1e | i16 | special-ability gate | 0x1e |
| 0x20 | 3 × u8 | special ids | 0x20 |
| 0x23 | u8 | **pad** (0 in all 80) | — |
| 0x24 | i16 | HP base | 0x23 |
| 0x26 | i16 | CV | 0x25 |
| 0x28 | 2 × u8 | companion monster ids (0-based) | 0x27 |
| 0x2a | i16 | reaction modifier | 0x29 |
| 0x2c | i16 | next-group chance | 0x2b |
| 0x2e | i16 | defense | 0x2d |
| 0x30 | u32 | gold per monster | 0x2f |
| 0x34 | u8 | group-size factor | 0x33 |
| 0x35 | u8 | (DOS `byte_34`, overwritten with sprite width in combat) | 0x34 |
| 0x36 | 2 × u8 | treasure item ids | 0x35 |
| 0x38 | i16 | attack power | 0x37 |

**II vs I** (`extracted/diff-vs-p1.json`): 24 of the 50 shared monsters are identical to DOS I (25 to Amiga I: ORANGE
DRAGON matches Amiga I; DOS I's entry 79 reads past its table). The other 25 shared monsters differ only in encounter
make-up (group-size factor, companions, next-group chance) and a few stats (DEATH FLY defense 15 → 35; FIRE ELEMENTAL
defense 0 → 40, attack power 0 → 135; APPRENTICE DEVIL treasure). New in II (30): GREEN MUCK, WILD DOG, PANTHER,
TRIBESMAN, LESSER WIZARD, BEEPING BLOBS, COBRA, WOLF, GREMLIN, IMPLING, GIANT WASP, VIPER MOTH, DARK DWARF, NECROMANCER,
PURPLE SKUM, KING COBRA, FOREST ELF, IMP, VORPAL BEETLE, CLOUD GIANT and the PLUTO set (PLUTO'S GIANT CONST., WINGED
DEVIL, UNDEAD SNAKE, DEMON-FLY, WYVERN, SNAKE-DRAGON, GIANT WYVERN, DRAGON-DEMON, ICE DRAGON, and PLUTO = #79). Dropped
from I (29) include ZEUS, BLACK LORD, BLACK KNIGHT, MINOR DEITY, TITAN, J. R. TROLKIN, OLD MAN.

Sprite sizes and offsets: `PHANT.PRG` image `0x2112e` (w, h) and `0x23d74` (offset) `[C]`, see §10.3. The encounter
builder copies the **height** into the group record's +0x35 (the `byte_34` slot) and fits groups on Σ(height + 3) < 135
(`logic-specs/encounters.md` §3); `GameData/phantasie2/monsters.json` carries both as `sprite_width` / `sprite_height`.

## 5. `GUILD.DAT` — 25 × 318-byte big-endian records `[C]`

7950 = 25 × 318. Names sit at record + 270 in every occupied slot (`GLADE`, `YOSHI` … at 0x10e + n·318); slots 22–24
are `FIRE_ELEM`, `WATER_ELEM`, `EARTH_ELEM` (level 20, race 15 = UNDEAD shown / 16 stored at @216). So 22 character
slots + 3 elementals, the Amiga I arrangement (`amiga/unpacked/PHANT/guild.dat` = 25 × 282).

- **Bytes 0–281 = Phantasie I's 282-byte record, big-endian `[C]`.** Every field checked so far lines up with
  `dos/logic-specs/save-file-format.md` (word fields BE, so DOS "u8 @216/@220" = the low byte at 217/221): stats @0–@10,
  MP @14–@18, HP @20/@22, status @24, skills @30–@48, skill bonuses @50–@68, worn items @82–@86, level @88, roster
  ordinal @92, spell-known flags @96 + 2·slot, race @216/@256, in-party @218, class @220, items @222–@238, current town
  @254, bank gold @262, XP s32 @266, name 12 B @270. UTILIT2's I↔II conversion copies bytes 0–281 one for one apart from
  the edits in §6, which settles the shared layout.
- **@42 = TOSS skill `[~]`.** DOS I never uses @42 (an empty skill slot between LISTEN and PICK LOCK). In II it is
  non-zero for all 7 played characters (24–139) and its skill-bonus slot @62 is filled too; the sheet adds a `TOSS %3u`
  line (data+`0x17cc`). **Code proof (s76) `[C]`:** Toss Rock hits on `rnd100 ≤ @42`; @62 = 2·level + 4·DEX + STR + 10
  (+15 DWARF, +30 GNOME) at creation and every level-up (`00-function-map.md` §5.2).
- **Bytes 282–317 = 36 new bytes.** In the played roster only @284 is set (1 for all 7 characters, 0 for the elementals).
  **@284 = origin** index into `GELNOR / FERRONRAH / NETHERWORLD` (0/1/2), printed as `ORIGIN: %s` `[C]` (sheet
  `0x03a50`); creation sets 1, or 2 when the creating town index > 6 `[~]`; transferred characters get all 36 bytes
  zeroed (§6), i.e. origin Gelnor. **@282 = SCORE** (sheet `LUCK %3u SCORE %7u`) `[C]`. @286–@300 and @302–@316 = two
  8-word arrays: @286 + 2(k−1) = minion k killed, @302 + 2(k−1) = beast rune k `[C]` (s78, `logic-specs/endgame.md` §1).

## 6. I → II character transfer (`UTILIT2.PRG`) `[C]` static

Rules spec (what the transfer does to a character, options D2-001…D2-004): `logic-specs/transfer.md`; Core:
`TransferIToII`.

Menu (`BACKUP.RSC`): TRANSFER CHARACTERS → FROM PHANTASIE 1 TO PHANTASIE 2 / FROM PHANTASIE 2 TO PHANTASIE 1.
I → II is the routine at image `0x0e38`: it waits for the Phantasie I data disk (`final.pic`), reads its `guild.dat`
(25 × 282, BE; the Amiga I size 7050), writes `twns.dat` (3504 B) and `sack.dat` (120 B) from II templates, calls the
converter `0x0edc` with mode 2, and writes `guild.dat` (7950 B). II → I (`0x0d38`) does the reverse with mode 1 (it also
copies I's `maps.int`/`twns.int` and writes a 7050-B guild).

**Checked live in Hatari (s76, `parity/transfer-live.md`): the converter's output matches the rules below byte for byte
on three rosters.** The live run corrected three points of the first static reading (XP band, PRIEST constant, the
XP-raise formula); they are folded in below.

**Converter `0x0edc` (mode 2 = I → II):** for each of the **22 character slots**, empty ones included (an empty slot,
`@92 = 0`, gets the same edits and stays empty), copy bytes 0–281 one at a time with these exceptions (byte offsets; a
word's low byte is the odd offset; **the high byte of every edited word is kept**, e.g. HP 310/300 → 256/256, MP
291/290 → 261/261). I's 3 elementals are not converted: II's guild takes its own from II's `TWNS.INT` (`5950 + 22·318`).

| Bytes | Effect | Meaning |
|---|---|---|
| 17, 19 | := 5 | MP max / MP current → 5 (high byte kept) |
| 21, 23 | := 0 | **HP max / HP current low byte cleared** → HP = 256·high byte, i.e. **0** for any HP < 256 |
| 83, 223, 241, 243 | := 4 | worn shield = SM SHIELD (4), item slot 1 = 4, weapon points 4, shield points 4 |
| 85, 227 | := 23 | worn armour = LEATHER (23), item slot 3 = 23 |
| 87, 225 | := 49 | worn weapon = MACE (49), item slot 2 = 49 |
| 245 | := 3 | armour points 3 |
| 229–239 | := 0 | **item slots 4–9 emptied**: every carried item from I is gone |
| 97–203 | := 0 | spell-known flags for slots 0–53 cleared … |
| 97 | := 1 if class is RANGER (2) or PRIEST (4) | … except HEALING I (#1) |
| 105 | := 1 if class is MONK (1) or WIZARD (5) | … and FIREFLASH I (#5) |
| 204–215 | copied | slots 54–59 kept: **DIVINE I/II (#55 and above) survive** |
| 89 | := 20 if it was 20, else 0 | **level 0** (an undead level-20 character stays 20) |
| 247 | := 1 if it was 0 | armour value in effect (low byte) |
| 255 | := 1 | current town 1 = PIPPACOTT (II's `@254` is 1-based, §9 `[C]`) |
| 262 | := old high byte / 35 | **bank gold** = ⌊hi/35⌋·256 + 100 … |
| 263 | := 100 | … so ≤ 8,959 GP becomes 100 GP; the most anyone keeps is 1,892 GP |
| 266, 267, 269 | := 0 | XP (s32 BE @266) rebuilt as 256 · g … |
| 268 | := g | b = **byte 267 only** = `(XP >> 16) & 0xFF` (byte 266 is never read, so XP wraps at 2^24: 16,847,216 → b 1 → 512, live). b = 0: g = 1 if byte 268 ≥ 20 (XP mod 65,536 ≥ 5,120), else 0. b = 1 → 2, 2–5 → 3, 6–12 → 4, 13–24 → 5, 25–40 → 6, 41–70 → 7, 71–100 → 8, 101–150 → 9, 151–220 → 10, > 220 → 11 |
| 282–317 | := 0 | the 36 II-only bytes (TOSS is @42 and is copied: 0 from I) |

Then `0x128a` runs on each occupied II record (@92 > 0) and **raises XP to I's level-0 training minimum if XP is
lower** (live) — the manual's "level 0 but enough experience to improve": `XP_min = trunc(200 / (sum/mult + 1e-5))`
(I's training formula at F = 1, `town-menus.md` §Training; `0x2baa` = fpdiv, `0x2b6c` = fpadd, `0x2c5a` = fpftol, which
truncates — names from `UTILIT2.PRG`'s DRI symbol table). Sums / divisors: THIEF (STR + 2·DEX)/19.8, MONK
(STR + INT + DEX)/49.5, RANGER (STR + INT + CHA)/52.8, WIZARD (2·INT + LUCK)/42.9 (as I), but **FIGHTER (2·STR + DEX)/33.0**
and **PRIEST (STR + 2·INT)/36.3** (I: (STR + DEX)/16.5, (STR + INT)/18.1); other class bytes: sum 200 → 0. Live: FIGHTER
STR 5 DEX 3 → 507, PRIEST STR 3 INT 4 → 659, THIEF sum 15 → 263.9965 → 263. II's own Guild training uses the II sums too `[C]`: `PHANT.PRG`
`guild_train` `0x0cdde` has the same six cases and constants, plus a new `level == 0 → factor 1.0` rule
(`logic-specs/00-function-map.md` §5.1).

FFP constants seen: `0x9e666645` = 19.8, `0xc6000046` = 49.5, `0xd3333346` = 52.8, `0x84000046` = 33.0, `0x91333346` =
36.3 (not ≈18.15: exponent byte `0x46` = ×64, as for 33.0), `0xab999946` = 42.9, `0xc8000048` = 200.0, `0xa7c5ac30` ≈ 1e-5 (FFP: 24-bit mantissa, sign bit 7 and excess-64
exponent in the low byte).

Mode 1 (II → I) runs the same per-byte table (the 36 extra source bytes are skipped); it is not documented in the
manual at all.

## 7. Other files (first look)

- **`.PIC`** (decoded in §10.2) — raw ST low-res screen data (160 B per line, 4 interleaved planes), no header, palette not in the file
  `[~]`. Full screens: `COVER1`, `PELNOR` (32000 B). Partial: `COVER2` 135 lines, `DECOR` 155, `FINAL2` 160; `CROWD` 11160 B
  is not a multiple of 160 `[?]`. Palettes are set by code (START.PRG passes a table at image `0x156`/`0x176` to Setpalette).
- **`SCROLLS.DTX`** — plain-text scroll pages (40-column lines, NUL-padded), 16384 B.
- **`MESS1–8`** — dungeon messages: a table of little-endian u16 offsets at the start (`0x0029, 0x0029, …, 0x0052`) then
  40-column text blocks, each prefixed with a length byte (`(` = 40, `P` = 80, `x` = 120) `[~]`. The LE offsets on a
  big-endian machine suggest the files were generated on a PC or kept from another version's format `[?]`.
- **`DNG1–8`** — 2048 B each (Amiga I `dng1–10` ≈ 1983 B). Bytes `0x8b/0x8d/0x0f/0x8f…` look like the DOS/Amiga cell codes `[?]`.
- **`OUT*.DAT`** — 17 overworld regions numbered 1–31 (1, 2, 3, 5, 6, 7, 8, 11, 12, 17, 18, 22, 26, 27, 28, 30, 31), 3000 B each
  (Amiga I 2500 B: a 1000-B cell grid + polygon records). First bytes `0x64…` = terrain class codes `[~]`. All are played
  state (fog of war).
- **`TWNS.DAT`** 3504 B, **`TWNS.INT`** 13900 B, **`MAPS.INT`** 8840 B, **`SACK.DAT`** 120 B, **`DNGX.SAV`** 2500 B: same names as I;
  layouts to be compared with `save-file-format.md`.

### 7.1 The title tune: `START.PRG` Dosound(`0x1b3`) (s108)

- **Data `[C]`:** the XBIOS 32 argument is image `0x1b3` (file `0x1cf`, after the 28-byte PRG header), in `START.PRG`'s
  data segment: a **2064-byte Dosound script** (to `0x9df`). It first writes 0 to registers 0–13, then mixer `0xD8`
  (tone A, B, C on; noise on C only). Channels A and B carry the tune: B always on the envelope (level `0x10`), A on
  the envelope or at fixed levels 10–15; C (tone period `0x54d` plus noise, noise period 0 / 1) is only switched
  between level `0x10` and 0, a drum-like hit `[~]`. Envelope shape 0 (`\___`) is
  re-triggered by a `13, 0` write before each wait, with envelope period high bytes `0x08`–`0x60` (low byte 0). It ends
  by writing levels 8–10 to 0 and `0x82 0x00`. 285 waits, Σn = 2946; no `0x80` / `0x81` sweeps.
- **Player = TOS, not the game `[~]`:** Dosound only stores the pointer; the sound routine runs from the Timer C
  interrupt (200 Hz) on every 4th call = **50 Hz on every ST**, whatever the video rate (so the pack's `vbl_hz` 60
  question does not touch the tune). Per tick: a wait counting down just decrements; else commands are read until a
  wait (`0x00–0x7F` + byte = register write, `0x80` = sweep start, `0x81 reg step end` = one sweep step per tick, any
  byte above `0x81` + n = wait n, n = 0 ends). A wait of n holds **n + 1 ticks**. Source: EmuTOS `bios/sound.c`
  (`sndirq`) and `bios/vectors.S` (`_int_timerc`, the 4th-call sieve) — a reimplementation of TOS, hence `[~]`.
- **Length:** 3231 ticks = **64.62 s**. `START.PRG` does not wait for it: after 251 Vsyncs it shows `COVER2` and
  Pexecs `PHANT.PRG`, and the interrupt plays on. **`PHANT.PRG` waits for it (s156, static + Hatari trace `[C]`):**
  its start-up `0xec36` (callers `0x735a`, `0x73be`) calls `0xb8f2` (`0xec8e`) after reading `TWNS.INT`; with Speed's
  data default 10 (≥ 9) that loops on Dosound(−1) until the title script has ended, and only then does the first own
  screen (the "insert your Data Disk" page) appear. Traced on the floppy (`phantasie2-v1.0-disk1.stx`, Hatari's FDC,
  EmuTOS US): tune start 3.50 s, Pexec 13.13 s, `appl_init` 21.93 s, the wait 30.21–68.12 s (the tune's last tick),
  the Data Disk page 68.34 s; no other Dosound call in between (`parity/capture_s156_title.md`). So the tune never
  plays into `PHANT.PRG`'s screens and the composite lasts until it ends; `PHANT.PRG`'s load time (≈ 17 s here) only
  matters if it were longer than the tune. The tune starts 1.96 s before `COVER1` shows (`COVER1` is read on the
  black palette); s158: the remaster's splash opens with a blank 1.96-s step (pack `"blank": true`) under the tune,
  the time `[~]` (Hatari's FDC with EmuTOS, not a real drive). The remaster plays the tune from the splash's start
  until its end or until the splash ends (a key may end the pictures early).
- **Chip:** YM2149 at 2 MHz (`OpenPhantasie.Client.Audio.Ym2149`: tone f = clock / 16·TP, noise LFSR 17 bits shifted at
  clock / 16·NP, 32-step envelope at clock / 8·EP, a write of 13 restarts it). **Output (s158) `[~]` model of a
  measurement:** on the ST the three chip outputs are tied together into a 1 kΩ resistor to ground, so they do not add
  linearly (one voice at full level gives about 0.53 of all three, two about 0.85). The measured data is Paulo Simoes'
  16 × 16 × 16 table from a real STF (Hatari `src/includes/ym2149_fixed_vol.h`, Hatari's default mixing); the remaster
  uses David Savinkoff's circuit model of it (Hatari `sound.c` `YM2149_BuildModelVolumeTable`: each 5-bit step a
  pull-up conductance, steps of 2^¼ through the load, out = WARP / (1 + 1 / (gA + gB + gC)), FOURTH2 1.19, WARP 5/3),
  which stays within about 8 % of the table from level 4 up (levels 1–3 read 10–23 % high, all below 0.5 % of full
  scale; one voice at 15: 0.56 for the table's 0.53) and covers the envelope's 32 steps (the table has 16; Hatari interpolates
  them); a fixed level v uses step 2v + 1 (1 stays 1, Hatari `YmVolume4to5`). Hatari's single-voice table
  `ymout1c5bit` steps by exactly 2^¼ (1.5 dB), i.e. the data sheet curve, not a further measurement. **Levels 1–3
  (s162) `[C]`:** no II script writes a fixed level 1–3: the title script's fixed levels are 10–15 (A) and 0 / 16
  (envelope), the 28 `PHANT.PRG` scripts' (table 7.2, `0x21ab0`, the second player's bodies `0x21bc0` / `0x21b54` /
  `0x21b66` / `0x21b7a` and `0x21c48`) 7, 9, 11 and 13–15 besides 0 and the envelope (16 / 31), with no `0x81` sweep
  on registers 8–10 (a scan of the bytes). So the model's 10–23 % excess only reaches the envelope's lowest steps (2–7), all below 0.5 % of full
  scale; the measured table has no 5-bit envelope steps, so no measurement can replace the model there `[~]`.
  **The STF's RC low-pass (s163) `[C]` values, modelled:** Atari's own sheet **C070787 rev. C** ("SCHEMATIC DIAGRAM
  1040ST", Atari (Japan), drawn Dec 1986, used on 1040STF / 1040STFM; archive.org
  `Atari_1040ST_Schematic_Rev_C_C070787_Dec_26_1986`, sheet 1 of 5, bottom left, U16 YM2149) draws the output stage:
  ANALOG A / B / C (pins 4, 3, 38) tied, **C10 0.1 µF ∥ R8 1 kΩ** to ground (a CR14 1N914 clamp to +5 V), AUDIO-IN
  joining through C9 47 µF, then C11 100 µF into Q1 2N3904's base (R9 10 kΩ to ground, R10 5.1 kΩ to +12 V decoupled by
  FB / C8 0.1 µF, collector R11 150 Ω, emitter R12 470 Ω), C12 47 µF to AUDIO OUT (sheet 3: the RF modulator / monitor
  connector). These are the values Hatari's `sound.c` comment reads (`LowPassFilter`, "dec 1986 schematics"), now seen
  on the sheet. While the chip pulls down, C10 discharges into R8 ∥ R9 ∥ R10 ∥ hFE · R12 = 759.1 Ω (Hatari's "795.1" is
  a typo): **2096.6 Hz** at hFE 100 (2063 Hz at hFE ∞, 2131 Hz at hFE 50, so the transistor's gain hardly matters);
  while it pulls up, the YM's own source resistance is in parallel, Hatari's 1 kΩ · (65119 − 46602) / 65119 = 284 Ω
  (from Simoes' measured tables, an estimate `[~]`): 206.9 Ω = **7694 Hz**. The remaster models it in
  `OpenPhantasie.Client.Audio.Ym2149` (with `Ym2149Mixing.StCircuit` only): a one-pole stepped at the chip's own
  clock / 8 (250 kHz, where the output is constant between steps, so `y += (1 − e^(−2π·fc·8/clock))·(x − y)` is exact),
  the corner picked per step by x ≥ y (pull-up) or x < y (pull-down), before the per-sample averaging and the DC
  blocker. It is on for the title tune and the sound effects alike (both `DosoundRenderer`). Not modelled: AUDIO-IN
  (C9, nothing drives it), the CR14 clamp (never reached), the emitter follower's level and the monitor's own input.
  Note for comparisons with Hatari captures: Hatari runs `LowPassFilter` on its 250 kHz `YM_Buffer_250` with taps
  fixed for 44.1 kHz (`(3·(x0 + x1) + 2·y0) / 8` up, `((x0 + x1) + 6·y0) / 8` down), so its corners there are about
  43 kHz and 11.3 kHz, not the 7.7 / 2.1 kHz its comment names; Hatari's audio is not a reference for this filter.
  Hatari enables it for ST machines at output rates ≥ 40 kHz (`Sound_SetOutputAudioFreq`; its PWM anti-alias filter
  otherwise).
  Atari's earlier **520ST sheet C070112 rev. B (Jan–Feb 1985**, archive.org `atari-520-st-schematics-jan-feb-1985`, sheet
  1) has **no such capacitor**: ANALOG A / B / C tied to R111 1 kΩ to ground, then C31 1 µF into Q1 2N3904 (bias R38
  8.2 kΩ / R39 3 kΩ, R40 220 Ω collector, R41 220 Ω emitter) and C33 1 µF to AUDIO-OUT; the SAMS Computerfacts
  CSCS12 (520ST) names the same Q1 / C31 / C33. So the low-pass depends on the board; the remaster's II stands for an
  STF (as the measured table), the machine choice `[~]`. The s108 model (three 1.5-dB levels
  averaged) stays as `Ym2149Mixing.Linear`. Decoder `OpenPhantasie.Data.Audio.DosoundScore`.

### 7.2 Sound effects and the Speed menu (`PHANT.PRG`, s135) `[C]` static

- **Table:** 33 Dosound pointers at BSS `0x24a8e` (sound n at `0x24a8e + 4n`), filled once by `0xb6b0`: every entry :=
  `0x21ada` (a default script), then 26 entries set one by one (`0xb6d8..0xb7da`). Scripts in the data segment (file
  offset = image + `0x1c`): 0 `0x21ba6`, 2 `0x21bd2`, 3 `0x21a50`, 4 `0x21a70`, 5 `0x21a8c`, 8 `0x21a7e`, 10 `0x21bf2`,
  11 `0x21c0c`, 14 `0x21c2a`, 15 `0x21b0c`, 16 `0x21afc`, 17 `0x21b44`, 18 `0x21b2c`, 19 `0x21ae8`, 20 `0x21ca6`,
  21 `0x21c8e`, 22 `0x21c70`, 23 `0x21c5e`, 24 `0x21cca`, 25 `0x21cdc`, 26 `0x21d32`, 28 `0x21d56`, 29 `0x21d70`,
  30 `0x21d8a`, 31 `0x21d9c`, 32 `0x21db8`; the rest (1, 6, 7, 9, 12, 13, 27) keep `0x21ada`.
- **Play** `0xb80e(n)`: n < 0 = nothing (no wait either); n = 1, 9 and 27 go to a second player `0xb92a` (below); any
  other n → `0xb8b0(table[n])`: `0xb8f2` (the wait below), `0xba02`
  (YM silenced: registers 0–13 := 0, the mixer keeps its port bits 6–7), then Dosound (XBIOS 32). Dosound returns at once:
  the TOS interrupt plays the script (7.1's player), the game goes on. `[0x21a30]` := 1 after sound 18, else 0.
  `0xb7e6` = `0xb8b0(0x21ab0)`, a fixed script outside the table (registers 0–13 := 0, envelope period 14, mixer `0xfe`,
  A on the envelope, shape 14 (triangle), 7 ticks), a short click; 24 callers, among them the OK form `0xae1a` (both
  ending pages; the other 23 not traced one by one).
- **Second player** `0xb92a(count, body)` (s136 `[C]` static): `0xb8f2` (the wait), `0xba02` (silence), then it builds a
  script in BSS `0x24962`: the body's bytes up to (not including) its `0xff` byte, `count` times over, then the 30 bytes
  at `0x21a32` (registers 0–13 := 0, `0x82 00` end), and passes that to Dosound. Sound 1 = body `0x21bc0` × 10, 9 =
  `0x21b54` × 10, 27 = `0x21b66` × 6 (`0xb822` / `0xb838` / `0xb84e`); `0xb9b4` (its own caller `0x42d8`) = `0x21b7a` × 5.
  The bodies are short register writes with a `0x81` sweep (1, 9: envelope period swept up, the A/B levels on the
  envelope), so each sound is a quick repeated chirp. `0xb9ce` = `0xb8b0(0x21c48)`, a fixed script outside the table (the
  spell sparkle `0x18b16`, below).
- **The wait** `0xb8f2`: when `[0x1fefa]` ≥ 9 and `[0x21a30]` = 0, loop on Dosound(−1) until the previous script has
  ended; so at Speed normal / slow a sound waits for the one before it, at fast it cuts it off.
- **Sound 32** (`0x21db8`, file `0x21dd4`), the ending's crowd: mixer `0x07` (tones off, noise on A, B and C), noise
  period 31, the three levels on the envelope, envelope period `0x1000` shape 13 (rise and hold), wait 77 (78 ticks),
  period `0x9000` shape 0 (fall), wait 128 (129 ticks), levels 0, end: a noise swell and fade, 207 ticks = 4.14 s.
  Remaster (s135): the ST pack's `sound_effects` (format `atari-st-dosound`, `OpenPhantasie.Data.Audio.StDosoundEffects`)
  lists the table, 33 = `0xb7e6`'s click and (s136) 1, 9, 27 as `repeats` (body, count, tail); played: the ending's sound
  32 and its pages' click, and (s136) the combat lines (7.3) with the Speed option; (s139) 34 = `0xb7fa`'s default
  script, and the overworld / dungeon / town sounds of 7.4.
- **`[0x1fefa]` = the Speed menu** (PHANT.RSC "Speed": fast / normal / slow; objects 26–28 ticked by `menu_icheck`,
  index in `[0x23b82]`, data default 1): `0x17376` sets fast 5, normal 10, slow 1000; data default 10 (normal). Readers:
  `0x18cc2`'s message wait (ms, below), `0xb8f2` (≥ 9 waits for the previous sound), `0x19038` (> 9: the attack pose
  `0x18f7a`, the caster's pose, holds 300 ms, else 10 ms), and direct `0x16cbe([0x1fefa])` waits at `0x64b8`, `0x64d8`, `0x9822`, `0xa16a`,
  `0xa238`, `0xfcf2`, `0x171b8` (terrain.md: a timed box). The defeat (`0x12694` / `0x1269a` / `0x127ae`) and the
  ending (`0x12f2a`, endgame.md 7.1) each save it, set 20 and restore it (all reads and writes checked with objdump);
  for the ending of all readers only `0xb8f2` is reachable from it (call graph of `0x12f2a..0x13520`, direct calls),
  so 20 means "sounds wait for each other, whatever Speed the player chose".
- **Message line** `0x18cc2(text, n)`: unless n = 15, sound n first (`0xb80e`); the text on the bottom line; then,
  unless n = 15 or `strlen(text)` ≤ 3 (`0x1d1f0`), `0x16cbe([0x1fefa])`: AES `evnt_multi` (keyboard, button, timer)
  for that many ms, a key or a click ending it. So at normal Speed a combat line holds 10 ms plus its sound (which
  waits for the previous sound), and "%s CASTS A SPELL" (`0x212f4`, sound 1) first shows the caster's other pose
  300 ms (`0x79fc`: `0x188fc` mode 2 → `0x18f7a`).

### 7.3 Combat lines: which sound, how long (s136) `[C]` static

Every combat round line goes through `0x18cc2(text, n)` (7.2): 27 call sites, all read (the name's source tells whose
line it is: character record `0x25162` + 318·i, name at `+270` = a party member; `0x25d9a` + 58·i = a monster).

| Line (string) | Whose | n | Call site |
|---|---|---|---|
| `%s AWAKENS` | member | 0 | `0x770e` |
| `PARTY AWAKENS` | – | 0 | `0x8550` |
| `%s THROWS A ROCK` | member | 3 | `0x7784` |
| `%s MISSES` (the rock) | member | 4 | `0x78e4` |
| `%s HITS` | member | 3 | `0x8776` |
| `%s MISSES` | member | 4 | `0x8884` |
| `%s CASTS A SPELL` | member | 1 (second player), the caster's pose first | `0x7a38` |
| `%s IS HEALED` | member | −1 | `0x7abc` |
| `%s IS RESURRECTED` | member | 12 (then a blank line, −1) | `0x92aa` / `0x92bc` |
| `%s IS SLEPT` | member | 14 | `0x9738` |
| `%s TAKES %d DAMAGE` | member | −1 | `0x980a` |
| `%s DIES` | member | 15 = no sound, no hold, then the mark (below) | `0x998a` |
| `%s HITS` | monster | 5 | `0x8bd8` |
| `%s MISSES` | monster | 8 | `0x8cfc` |
| `%s AWAKENS` | monster | 10 | `0x8e66` |
| `%s IS SLEPT` | monster | 11 | `0x911a` |
| `%s BREATHES` | monster | 13 | `0x959c` |
| `%s TAKES %d DAMAGE` | monster | −1 | `0x9a68` |
| `%s DIES` | monster | 16 (after `0x186de(…, 0)`, its picture) | `0x9b18` |
| `[original text]` | – | 6 (the default script `0x21ada`) | `0x8c58` |
| `A STUNNING BLOW` | – | 7 (the default script) | `0x8cc2` |
| `" "` (one space): a monster's special | – | 9 (second player), after the monster's pose (`0x186de(…, 2)`) | `0x8d9e` |
| `[original text]` / `[original text]` | – | −1 | `0x826e` / `0x8382` |
| `MONSTERS RUN` (the spell) | – | 2 | `0x84e0` |
| `[original text]` / `[original text]` | – | −1 | `0x9478` / `0x94b2` |

- **A monster's special** (`0x8d36..0x8dbe`): one of its three special bytes (`+0x20`, `rand·3/100`), its pose 2, the line
  `" "` with sound 9, then `0x92f8` applies the special (its own lines: `%s BREATHES` 13, `IS SLEPT`, the damage …). The
  ST has **no "%s CASTS A SPELL" for a monster** (the only caller names a member); the remaster's monster line "X CASTS A
  SPELL" / "X BREATHES" (I DOS, `MonsterSpecials`) stands for this blank line: II plays it as sound 9 without a hold, and
  a breath line as sound 9 then 13 (the ST prints a blank line, then "%s BREATHES"). User s137: keep the
  remaster's text with the ST's sound 9 (as built). s162 traced live (`parity/capture_s162_combat.md`) `[C] live`: the
  gate as I's (`0x8980..0x89b8`: `r + 1 ≤ chance·(2g + 2)/(groups + 2)`); the silhouette on the monster's own slot 300 ms,
  the attack pose 300 ms, the blank line with 9 and no hold, then `0x92f8`'s own lines (none for 5–16, the damage line
  for 1–4, the party lines for 17–24); a breath: 13 waits for 9 (≈ 1 s at normal), then the hold, the damage lines and
  the monster's melee with its own pose.
- **Pacing per line** (`0x18cc2`, `0xb80e`, `0xb8f2`, `0x16cbe`), with S = `[0x1fefa]` (Speed: fast 5, normal 10, slow 1000):
  1. n ≥ 0 (and ≠ 15): when S ≥ 9 and the last sound was not 18, wait until the running sound has ended (Dosound(−1)
     polled; keys do not end it); then the new sound starts (at fast it cuts the old one off). n < 0: no sound, no wait.
  2. The text on the bottom line.
  3. Unless n = 15 or the text has 3 characters or fewer: hold S ms (AES `evnt_multi`: a key or a click ends it early).
  A sound never holds its own line: the line after it waits for it (at normal and slow).
- **A cast** (`0x79fc` → `0x188fc` mode 2 → `0x18f7a`): before `%s CASTS A SPELL` the caster's silhouette, then its other
  pose (s156 read, below: "The poses"): the silhouette 300 ms when S > 9, else 10 ms, then PARTY2 a fixed 300 ms (no key).
- **A member's death** (`0x9894` → `0x188fc(m, 0)` → `0x18a02`): "%s DIES" prints (n = 15: no sound, no hold), the mark
  at portrait + (8,13), sound 15 (`0xb80e`: at S ≥ 9 it waits for the running sound first), `0x16cbe(500)` (500 ms, a key
  ends it), `0xb8f2` (at S ≥ 9 wait until sound 15 has ended: its script holds 209 ticks + its writes, about 4.2 s), then
  `0x185d6(1)` re-lays the row without the member (10.3). So at normal Speed every member's death holds the round about
  4.2 s.
- **Sound 18** (`0x18416`: `0xb80e(18)`, then the mouse shown again; `0x183ee` hid it at the round's start `0x7614`) ends
  **every** round (s156 objdump, `[C]` static): `combat_round 0x7610` has seven exits, each through `0x18416`: after a
  member's action when no monster is left (`0x8840` melee, `0x88ee` the others; returns 12 = `combat_victory`), in the
  monsters' turns when no member is alive (`0xb662` = −1: `0x89f6`, `0x8c04`, `0x8d16`, `0x8dd0`; returns 6, the defeat),
  and the round's normal end (`0x8efa`, after the re-rank `0x9b42` / `0xfd82`; returns 4, the party options). So 18 plays
  after the round's last line and before `combat_victory`'s boxes (MONSTERS KILLED, the XP box). It sets `[0x21a30]` := 1:
  the next sound does not wait for it. The party options `0x9dac` play no 18.
- **Sound 17** (`0x1838e`): when `[0x23eb6]` is set, the screen switched to the combat view (`0x180d0`, `0x19af6`), the flag
  cleared and 17 played; then the combat screen is drawn (`0x18458(0)`). Callers: `0xfd78` (in `0xfd58`: no caller, no
  branch and no fixup names it, dead code, s162 `[C]` static), `0xfe08` (in the combat-screen set-up `0xfd82`, called at the round's
  start `0x761a`, its end `0x8ef4` and by the party options `0x9d96`) and `combat_victory 0xa290` (`0xa2ba`). So 17 plays
  at the first combat-screen set-up after the flag was set, whatever started the fight (a wilderness roll, a dungeon
  cell, a fixed tile, a surprise or night start: each opens with the party options or a round). `[0x23eb6]` is data 1 at load and set again only by `0x183c4`
  (`0xa714`, the end of `combat_victory`: back to the party view). Every fight ending goes through `combat_victory`
  (mode 12, 7.3a), so every encounter starts with 17, except the first one after a defeat (mode 6 never reaches
  `0x183c4`), and `combat_victory`'s own call only redraws (the flag was cleared at the fight's start) `[C]` static.
  Remaster (s163): Core's `GameSession.LastFightLost` (the last fight ended in a wipe; not saved, as the ST's flag lives
  in the program) and `CombatPresenter.Start` queues 17 only when it is false; before s163 every fight played 17
  (`P2CombatLineRunTests.Presenter_NoSound17OnTheFirstFightAfterADefeat_AgainAfterAWin`). A non-combat defeat (a dungeon
  event) leaves the flag as the last fight left it, as on the ST.
- **The poses `0x186de(group, slot, mode)`** (s156 objdump, `[C]` static): the monster's sprite index is `[0x24c1c + 2·group]`,
  its picture rectangle the 8-byte entry (x, y, w, h) at `0x25c3c` + 48·group + 8·slot (slot = the n-th living monster of the
  group, `0x18856`); MSTR2 (attacking) is read from the buffer `[0x24f34]`, MSTR1 (idle) from `[0x28332]`.
  - mode 0 (`0x9b06`, before a monster's `%s DIES`, sound 16): `0x19b28` clears the rectangle and the entry's width := 0:
    the picture is erased before the line.
  - mode 1 (`0x8ac4`, before a monster's `%s HITS` / `%s MISSES`): `0x186a8`: MSTR2 drawn, `evnt_timer(300)` (no key, at
    every Speed), MSTR1 drawn.
  - mode 2 (`0x8d8c`, before the special's blank line, sound 9): `0x18f7a`: the silhouette, then `0x186a8`.
  - `0x18f7a(attack, idle, rect)`: `0x1993a` ORs the attack picture's four planes into a one-plane mask, `vrt_cpyfm`
    (`0x198be` → `0x1a25c`, mode 1 = replace, colour indices {1, 0}) draws it over the whole rectangle: the shape in VDI 1
    (register 15, white in the combat palette), the rest VDI 0 (register 0, black); `evnt_timer(300)` when S > 9, else
    10 ms; then `0x186a8` (the attack picture 300 ms, the idle picture).
  - Members, `0x188fc(member, mode)` with PARTY2 / PARTY1 at `[0x24f48]` / `[0x2833c]` + race·512 on the row's portrait
    rectangle (`0x25b48` + 8·slot): mode 1 = `0x186a8` (`0x7748` before `%s THROWS A ROCK`, `0x86fe` before a member's
    `%s HITS` / `%s MISSES`), mode 2 = `0x18f7a` (`0x79fc`, the cast), mode 0 = the death mark (`0x18a02`, below).
  So each swing, rock and monster attack holds a fixed 300 ms before its line, at every Speed.
- **The spell sparkle `0x18b16(group, slot, amount)`** (s140 read, s156 corrected `[C]` static): the slot is resolved by
  `0x18856`; an empty slot (width 0) shows the Polish debug alert "Zly ogieniek" (`0x23f05`) and returns. amount ≥ 0 picks
  the sprite `0x23c48`, a negative amount is negated and picks `0x23c92`. The loop runs amount / 32 + 1 times:
  1. `0x18ab8`: a random 16×16 spot inside the slot's rectangle, x + `0x18a8a(w − 16)`, y + `0x18a8a(h − 16)`
     (`0x18a8a(n)` = rand() / (32767 / n), 0..n; 0 when n = 0), stored at `0x24c00` (`0x19cce`; nothing is saved there).
  2. `0xb9ce` = `0xb8b0(0x21c48)`: the wait (`0xb8f2`), the chip silenced, Dosound(`0x21c48`), a noise burst (mixer
     `0xc7`, noise period 17, levels on the envelope, then a 256-step sweep of register 6: longer than the sprite shows).
  3. the mouse hidden; `0xba62`: Vsync (XBIOS 37), then line-A `A00D` draws the sprite with its hot spot at the spot.
     Sprite blocks (74 bytes: hot spot x / y, format, background colour, foreground colour, then 16 rows of (background
     mask, foreground mask) words, as EmuTOS's `Mcdb`): both format 1 (VDI: a foreground bit takes the foreground colour,
     else a background bit the background colour, else the screen stays), hot spot (0, 0). `0x23c48`: background 5,
     foreground 2 (yellow / red in the combat palette), a scatter of dots; `0x23c92`: background 13, foreground 15 (grey /
     white), a small rock. The colours are pixel values written plane by plane (EmuTOS `cur_display`), so registers of the
     combat palette, not VDI pens `[~]` (Atari's own TOS not checked).
  4. `evnt_timer(300)` (`0x1a84a`, no key); `0xba9a` undraws (`A00C`, the sprite's own save area); `0xb9e2`:
     Dosound(`0x21a32`) silences the chip, then `0xb8f2`.
  Callers: `0x7964` the **thrown rock** (amount = −(the rock's damage before the 1-point floor), `toss-rock.md`: so the rock
  sprite; a rock of 0 damage is amount 0 and shows the sparkle sprite), `0x7d34` a member's single-target damage spell,
  `0x9054` / `0x9080` the group / all-monster damage spells (the amount rolled before the monster's armour, which
  `0x9080` subtracts only for the damage call `0x99e0`); each right before `0x99e0` (the damage and its
  `%s TAKES %d DAMAGE`). Monsters only: no call puts it on a member.
- **The rock's miss** `0x18c6c` (`0x78ec`, right after a rock's `%s MISSES` and its hold): the rock sprite `0x23c92` at the fixed
  screen spot (100, 10), 300 ms (`evnt_timer`, no key), undrawn. No sound. The spot's place on the combat screen (s163
  objdump `0x18458`): the field is cleared from (3, 13) (315 × 186, `0x18468`), the groups stacked bottom up from
  `[0x23eb4]` − 3 = 147 (data word 150, the party row's top; each group's band = its picture height + 3), so (100, 10)
  sits a third of the way across, its 16 × 16 rock straddling the menu bar's lower edge at the top of the monster area.
- **An emptied group's row** (s163 objdump `[C]` static): `0x18458`'s layout loop (`0x184c0..0x18518`) places each group
  from its count `[0x24c16 + 2·group]` and its band height (entry `+6`); only `0x18288` writes that count (after
  `0x1823c` has reset all three to −1), at the combat-screen set-up `0xfd82` (the round's start and its normal end,
  after the re-rank `0x9b42`); `combat_victory` resets them too (`0xa2b4` → `0x1823c`). An erase (`0x186de`
  mode 0, `0x19b28`) only clears the slot and zeroes its width, and the redraw skips width-0 slots (`0x18544`). So a
  group whose monsters all die in a round keeps its (empty) band until the round's end re-lays the screen, and the
  others do not move up.
#### 7.3a The end-of-fight boxes `0xa124` / `0xa1c2` (s137, s138) `[C]` static, details `[~]` marked

Two message boxes from **OUT.RSC** (`rsrc_gaddr(R_TREE, n)`, `0x1abfa(type, index, &addr)`), tree units = 8×8 px chars:

| | One line (`0xa124`, tree 40) | Two lines (`0xa1c2`, tree 41) |
|---|---|---|
| Box (root, type 20, border) | 38 × 5 chars (304 × 40 px) | 37 × 6 chars (296 × 48 px) |
| Text 1 | object 1 at (2, 2), 34 chars | object 1 at (1, 1), 34 chars |
| Text 2 | – | object 2 at (1, 3), 34 chars |
| Hold | S + 1000 ms (`0xa16a`) | S + 1500 ms (`0xa238`) |

- Each text is **centred in 35 columns** by `0x17266(str, buf, 35)`: ⌊(35 − strlen) / 2⌋ spaces, then the text (nothing
  prepended when longer); `0x7be(tree, obj, buf)` puts it in the object. Wrappers: `0xa18c(text)` / `0xa1a4(a, b)` end with
  `0x17210` (the party-view redraw), `0xa25a(text)` / `0xa272(a, b)` end with `0x18446` → `0x18458(1)` (the combat screen redrawn).
  So the fight-end boxes are the `0xa25a` / `0xa272` ones.
- Shown by `0x16dce(tree, 12, 75, ms)`: tree object 0 placed at pixel (12, 75) (the 320×200 screen, the box fills x 12..316),
  the background saved (`0x19cce`), `objc_draw` (`0x1ab46`), then `0x16cbe(ms)` (`evnt_multi`: a key, a click or the timer
  ends it); x < 0 would centre it (`form_center`), not used here. The saved background is restored by the redraw after.
  S = `[0x1fefa]` (fast 5, normal 10, slow 1000), so normal Speed holds 1010 ms / 1510 ms.
- **One-line texts through `0xa25a`:** `[original text]` (`0x9e72`), `[original text]` (`0x9f00`),
  `[original text]` (`0x9f14`), `[original text]` (`0x9f5a`), `MONSTERS RUN` (`0xa066`, the morale check: **sound 2
  first**, `0xa056` `0xb80e(2)` (s138 objdump); then the run roll rand < 80 − |10·(1 − [0x25b80])| → 12 (ran), else
  `[0x24960]` := 45 and every monster state 2, as Core's `MoraleCheck`), `MONSTERS KILLED` (`0xa2ee`, in
  `combat_victory 0xa290`). No other `0xb80e` call in `0x9d00..0xa600`: the other boxes have no sound.
- **Two-line through `0xa272(a, b)`** → `0xa1c2(a, b)`: `a` goes into object 1 (the **upper** row), `b` into object 2
  (s138 objdump `0xa272..0xa28c`). `0xa3da`: a = `[original text]` (`0x215f3`), b = the buffer `0x24ec2` formatted
  with `"%ld XP AND %ld GP"` (`0x215e1`); `0xa4fa` (in `combat_victory 0xa290`): a = `[original text]`
  (`0x21619`), b = `"%s TAKES AN AMULET"` (`0x21606`) with the first non-DEAD member's name. Both orders as the s82 live
  shots (`parity/shots/p2-minion-xp.png`, `p2-minion-amulet.png`) `[C]`.
- **Look** (OUT.RSC, s138 `rsc` read): root G_BOX `ob_spec` `0x00020076` = border 2 px inward in VDI 0 (register 0,
  black), solid fill VDI 6 (register 3 of the combat palette, `#FF6D00`); the texts are G_TEXT, font 3 (system 8×8),
  `te_color` `0x2070` / `0x2000` = VDI 0 text, transparent. On the s82 shots the orange spans x 14..306, y 77..121 (tree
  41 at (12, 75) less the border), rows at y 83 / 99, text x = 12 + 8·(column + ⌊(35 − len) / 2⌋) (`te_just` 2 adds
  nothing visible).
- `combat_victory 0xa290` also calls `0x1838e`: there it only redraws (sound 17 above).
- **Which endings reach the XP box** (s156 objdump of `0x9dac` and `combat_victory 0xa290`, `[C]` static): every fight end
  but the defeat returns mode 12 → `combat_victory`: no monster left (`0x9dce` / `0x9dd8`, or the round's `0x8840` /
  `0x88ee`), [original text] (`0x9e9e`), [original text] (`0x9f34`), [original text] (`0x9f72`: XP and
  gold := 0 first), the party's escape (`0x9fd4`: XP and gold := 0, no box of its own), MONSTERS RUN when the run roll
  succeeds (`0xa0a8`). `combat_victory` shows MONSTERS KILLED only when `[0x25d16]` is set or no monster is alive
  (`0xa2c6..0xa2ee`), then **always** the XP box (`0xa3da`, "[original text]" over "%ld XP AND %ld GP", the amounts per
  survivor: 0 XP AND 0 GP after a greeting or an escape; no survivor alive: both 0), then the minion box when
  `[0x25cda]` ≠ 0 and `[0x25cd0]` = −1 (`0xa48a..0xa4fa`), so right after the XP box. [original text] and a failed
  escape go back to the round (mode 5). The XP / GP formulas are Core's D-053 shares.

- **Remaster (s136):** the II ST pack's `combat_lines` holds this table, the Speed values and the death sequence;
  `P2CombatLineRun` plays a round line by line on the remaster's combat screen (log box, cards) with the sounds, and the
  game tab's COMBAT SPEED (fast / normal / slow) stands for the Speed menu. Lines the table lacks (the remaster's other
  spell lines, the fight's end) play no sound and hold S ms unless 3 characters or fewer `[?]`. A key ends the hold
  running (not a sound wait, not the pose); the I DOS T)IME LAG pause between rounds is skipped while the run paces II
  (the ST has no Time lag: manual "except for Commodore and Atari users"). The fight's last round (victory, wipe) plays
  out on the combat screen after Core has left the fight; its rank list is empty then (the encounter is cleared) `[~]`.
  Seen live in the remaster (Xvfb, `parity/shots/s136/`); not compared with Hatari yet `[~]`.
- **Remaster (s138), the end boxes:** the pack's `combat_lines.end_boxes` holds 7.3a (place, cells, centring, pens of the
  combat palette, holds 1000 / 1500, the one-line `keys`, `outside_round_keys` = MONSTERS RUN, the `pairs` [upper, lower],
  `sounds` = MONSTERS RUN → 2); `StEndBoxes` (Data) loads it, `StEndBoxes.Layout` places a box and its rows.
  `P2CombatLineRun` turns a box line into its step: the sound first (if any, with the `0xb80e` wait), the line(s) into the
  log, then the box held Speed + 1000 / 1500 ms (a key ends it) as `ActiveBox`; a pair's two lines in a row, in either
  order (Core notes the XP line first), make one two-line box with the pack's row order. Core logs the pair only under
  `p2.combat-shares` = `st` (D2-043, the ST's shares); under the default `manual` every fight end but the defeat logs
  "%d XP AND %d GP" alone (the totals; then the pool line "%d [original text] AND %d GOLD", a plain line). s158: a pair's
  line alone shows in the two-line box (tree 41, Speed + 1500 ms) in its own row, the partner's row empty, since
  `combat_victory` always shows the XP box and "[original text]" over the manual's totals would not be true `[~]`. The
  minion box follows the XP box (s156: `Classify` moves it up past the treasure lines Core notes between them; the ST
  order read above `[C]`).
  `UiOverlays.DrawP2EndBox` draws it over the combat screen at (12, 75) of the 320×200 screen; s160 (user, s159): the
  text in EmuTOS's 8×8 system font (the pack's `fonts` role `p2.end_box`, `GameData/phantasie2/graphics/fonts/
  emutos-st-8x8.json`, GPL-2.0+, `tools/scripts/emutos_st_font.py`; TOS's own is not on the disks), one glyph per 8-px
  cell from its top-left as `objc_draw` puts G_TEXT; seen live on Xvfb ([original text]). The party options' answers, the
  morale check's MONSTERS RUN and what follows an ending outside a round (XP box) play through the same run
  (`CombatPresenter.StartP2Interlude`), then the orders, the menu or the fight's end; MONSTERS RUN in a round stays the
  spell's line (sound 2, no box). The box's place on the remaster's combat screen (its own layout, not the ST's) and the
  key that picks an option not ending the box it opens are the remaster's; seen live on Xvfb
  (`parity/shots/s138/remaster-p2-end-box-dont-respond.png`), not compared with Hatari `[~]`.
- **Remaster (s156), poses, sparkle, 18:** the two line-A sprites are extracted (`extract_st_p2_graphics.py`:
  `combat/sparkle.png`, `combat/rock.png`, RGBA, the VDI-format rule above), the pack's `combat_lines` gains
  `attack_pose_ms` 300, `silhouette_pens` [15, 0], per-line `attack` / `pose` / `erase`, `round_end_sound` 18 (was
  `fight_end_sound`) and `sparkle` (sound 35 = `0xb9ce`'s script `0x21c48` in `sound_effects`, the two pictures, per 32,
  size 16, hold 300, rock_miss (100, 10)). `P2CombatLineRun`: the attack pose before a swing / a rock throw / a monster's
  swing (not before a rock's own MISSES), the silhouette then the attack pose before a member's cast and a monster's
  special (CASTS A SPELL / BREATHES), the monster's picture erased before its DIES (`Slain`), the sparkles before the
  damage line a spell or rock hit leads to (Core's `CombatHit.SpellDamage`: the spell's pre-armour roll, s156 also the
  rock's damage before the floor), each followed by the silence (`P2CombatLineRun.Silence`: the Client stops the effect),
  the rock's miss after its MISSES' hold, and 18 after every round's last line, before the end boxes. The combat screen
  draws the poses on the member's card / the monster's own picture (s162: the pack's `monster_slots`: each rank a row of
  its own monsters' pictures, as `0x18288`, wrapped to the rank box; an erased monster's slot stays empty, the others in
  place, until the round's re-rank, as `0x18856`; `CombatPresenter.P2RankSlots`, `UiOverlays.SlotRects`; seen on Xvfb,
  `parity/shots/s162/remaster-p2-monster-slots-strip.png`: the attack pose on the first and the third skeleton, then a
  gap left by the middle one's DIES), the sparkle at its random spot inside that picture (`StCombatSparkle.Offset`, the ST's
  `0x18a8a`), the rock's miss at (100, 10) of the ST's monster area moved onto the remaster's (s163: the pack's
  `sparkle.rock_miss_area` [0, 13, 320, 134], `StCombatSparkle.RockMissOn`: the same fraction across the rank box, the
  sprite kept inside it, so at its top a third of the way across; the remaster's placement `[~]`), and while a round
  plays the ranks as they stood before it, less the erased monsters (`CombatPresenter.P2RankRows`; closes the last
  round's empty rank list); s163: with `monster_slots` a rank whose monsters are all erased keeps its row (empty slots
  sized by its kind, its line "RANK n : 0 …", the remaster's: the ST draws no line) until the round's re-rank, as above
  (`P2RankKinds`; one picture per rank still drops it). The Vsync before each sprite (≤ 1 VBL) is not modelled `[~]`. Traced live s156
  (`parity/capture_s156_combat.md`): the attack pose's 300 ms (+ 10–20 ms drawing) before rock / monster lines, the rock's
  silent 300 ms miss after MISSES, 18 at a survived round's end (`0x8f00`) `[C] live`. s158 traced live
  (`parity/capture_s158_combat.md`) `[C] live`: a cast: the silhouette 300 ms at normal (10 ms + drawing at fast), the
  pose 300 ms at both, "CASTS A SPELL" with sound 1, then `0x18b16` from `0x7d3a`: the first burst waits for sound 1
  (≈ 735 ms at normal, none at fast), each sprite 300 ms, later loops 340 ms each with no wait, loops = amount / 32 + 1;
  a monster's death: `0x186de(group, slot, 0)` erases that monster's own rectangle before "%s DIES", whose text shows only
  after sound 16's wait; a member's death: no sound / hold on the line, the portrait cleared and the mark drawn, sound 15
  (after the running sound), 500 ms, the wait (≈ 3.7 s more), `0x185d6(1)` (75 ms): ≈ 4.5 s per death. The remaster's
  steps follow the same order; s162 a monster's special and a breath (above, "A monster's special"). Remaster seen on Xvfb
  (s156, a wilderness round: TOSS miss, eight skeleton swings): the monsters' attack frame on the rank pictures
  (`parity/shots/s156-remaster-p2-monster-attack-pose.png`, `…-rank2-attack-pose.png`); s158 (a 30 fps recording,
  `parity/shots/s158/`): the member's attack pose, the caster's silhouette, the rock's sparkle on a hit and a spell's
  sparkle on the monster picture; s163 (`parity/capture_s163_client.md`): a member's death (the portrait now cleared before the mark, as `0x18a02`), the rock's miss, an emptied rank's row and the XP box under the manual rule seen live. A monster's DIES erases the monster just hit: Core notes each monster hit right before its TAKES line
  (`CombatResolver.ApplyMonsterDamage`), so the last hit of that name up to the DIES is that very monster (s162 test
  with two ORC ranks) `[C]`. The elemental keeps its own picture (no pose).

### 7.4 Sounds outside combat: overworld, dungeon, town (s139) `[C]` static, traced live s140 / s141

Every caller of the players (`0xb80e`, `0xb8b0`, `0xb92a`, `0xb7e6`, `0xb7fa`, `0xb9b4`, `0xb9ce`) was listed from the
objdump of the text (all `jsr` absolute, each with a constant pushed just before), and each caller read. Outside the
combat round (7.3), the fight's start / end (17 / 18) and the ending (32, the click):

| Sound | Call site (routine) | When | Remaster moment (`FieldCues`) |
|---|---|---|---|
| 19 | `0x63e2` (`overworld_step 0x6022`, cell value 40) | before "A TOWN. ENTER ?" (OUT.RSC tree 37) | `overworld.town_prompt` |
| 19 | `0x106a0` (`dungeon_main 0x10698`, main-loop mode 13, set only by the class-5 tile at `0x6392`) | before "A DUNGEON. ENTER ?" (tree 30) | `overworld.dungeon_prompt` |
| 34 (default script `0x21ada`, `0xb7fa`) | `0x10470` (`0x1046c`, called only from the step `0x1087c` at `0x108d8`) | "THAT IS A WALL": a cell 0 **or a step off the map** (x < 0, y < 0, x ≥ 33, y ≥ 38) | `dungeon.wall` |
| 29 | `0x10e9c` (`trap_disarm 0x10db0`) | the disarmer's roll fails, before "%s FAILS" and the spring (success, "SUCCEEDS": none) | `dungeon.disarm_fails` |
| 28 | `0x11d02` (`dungeon_event 0x11ab6`, op 3, jump table `0x22e10`) | before the box "A SPELL IS CAST", then the damage REPORT | `dungeon.event.spell_cast` |
| 16 | `0x11dfc` (op 12) | before "[original text]" (op 13 moves silently) | `dungeon.event.party_moved` |
| 26 | `0x11e88` (op 14) | before "[original text]" (op 15 silent) | `dungeon.event.noise` |
| 22 | `0x262c` (`0x260a`, from `transport_menu 0x23ee` at `0x25da`) | TRANSPORTATION: a town picked (object 8, the cancel, returns −1 first); then the fade (below) and `0xb9e2` ends the sound | `town.transport` |
| 12 (the default script's bytes) | `0x92d8` (`resurrect_member 0x9188`, mode ≠ 5, from the field cast `0x6ecc` at `0x6fdc`, spell 44) | **after** the box "%s IS RESURRECTED" (`0xa18c`) | `field.resurrected` |
| 34 (`0xb7fa` via the alert `0x1628(9)` "D O N E !") | `0xe5ec` (`town_heal 0xe4d4`), `0xe7f2` (the town's resurrection `0xe766`) | a town cast of a Healing on a living member, or a Resurrection that raises one (after the line); both from the town's cast `0xe3f6` | `town.cast_done` |

- **The default script** `0x21ada` (table entries 6, 7, 12, 13 keep it, 7.2) is also played straight by `0xb7fa` =
  `0xb8b0(0x21ada)` (the wait, the silence, Dosound; `[0x21a30]` untouched): the pack's id **34** stands for that call.
- **Alerts beep `[C]`:** `0x1628(n)` (alert `n` of the loaded RSC's free strings: `rsrc_gaddr(5, n)`, then `0xb7fa`,
  then `form_alert`) and `0x1668(text)` (`0xb7fa`, `form_alert`) play the default script first. PHANT.RSC (loaded by the
  town `0xec36` → `0x4706`): 0 "There is no room" (`guild_add 0xd442`), 1 "You can not afford it" (`shop_menu 0xc1ea`,
  the guild's spell learning `0x974`, training `0x17e4`), 2 "You are improved" / 9 "D O N E !" / 10 "UNUSABLE"
  (`use_item 0xdfcc`; 9 also the town casts above), 3 "You must assemble a group" (`0xdb5a`), 5 "No one is wise enough
  to teach You" (`guild_train 0xcdde`), 6 "Not in the town / [original text] to get here" (YES / NO, `guild_add`), 7
  "You can't learn any more" / 11 "You've failed" (`0x974`), 12 "He has nine items" (`0x1586`, `shop_menu`), 13 / 14 the
  printout questions (`read_scroll 0x292a`, the sheet `0x3a50`); `0x1668`: "%8s has died" / "%8s has aged"
  (`town_aging 0xba9a`); training's "You need %ld more experience points" calls `0xb7fa` itself (`0x1814`), its "It will
  cost %d gold" (AGREE / NOT) does not. OUT.RSC (loaded for the field, `0x572c`) has one: 0 "Not enough Magic" (the field
  cast `0x6fb8`, magic +18 below the cost). OUT.RSC is the field's.
- **Every alert caller (s140, objdump of the text) `[C]`:** `0x1628` is called from 11 one-line wrappers and 6 other sites,
  `0x1668` from 2, `0xb7fa` straight from 2 (`0x1814`, the wall `0x10470`):

  | Alert | Wrapper / site ← caller | When | Remaster line (pack key) |
  |---|---|---|---|
  | PHANT 0 "There is no room" | `0x2356` ← `0xd500` | guild_add: six members | `core.guild.party_is_full` |
  | PHANT 1 "You can not afford it" | `0x15e0` ← `0xc276`; `0x9d6`; `0x188a` | shop; spell learning; training's cost agreed | `core.armory.not_enough_gold`, `core.guild.learn_cant_afford`, `core.guild.train_cant_afford` |
  | PHANT 2 "You are improved" | `0x1616` ← `0xe2b4` | use_item | `core.p2.you_are_improved` |
  | PHANT 3 "You must assemble a group" | `0x28cc` ← `0xdc60` | leaving with no party | `core.town.you_must_assemble_a_group` |
  | PHANT 4 "You've learned" / 11 "You've failed" | `0x9f6` (`0x974`) | the learning roll | `core.guild.learn_learned` / `core.guild.learn_failed` |
  | PHANT 5 "No one is wise enough to teach You" | `0x15f2` ← `0xce22` | guild_train | `core.guild.no_one_wise_enough` |
  | PHANT 6 "Not in the town / [original text] / to get here" (YES / NO) | `0x2366` ← `0xd51a` | guild_add, a member of another town | `core.guild.move_not_in_town` (the Guild's move question) |
  | PHANT 7 "You can't learn any more" | `0x98a` | learning, no slot | `core.guild.learn_no_more` |
  | PHANT 8 "Not enough Magic" | `0x15bc` | no caller | – |
  | PHANT 9 "D O N E !" | `0x15ce` ← `0xe164` / `0xe1de`; `0xe5ec` / `0xe7f2` | use_item: a healing / magic potion drunk; the town casts | `core.magic.x_drinks_healing_x_x` / `x_drinks_magic_x_x` (town only); the casts are the cue `town.cast_done` |
  | PHANT 10 "UNUSABLE" | `0x1604` ← `0xe07e` | use_item | `core.p2.unusable` |
  | PHANT 12 "He has nine items" | `0x15b2` (`0x1586(0)` ← `0xc24c`) | shop: a shopper with nine items | `core.armory.ninth_item` (the remaster's refusal of a full shopper, in the words of the ST's silent after-the-ninth-buy box: II's table has no "He has nine items") `[~]` |
  | PHANT 13 / 14 printout questions | `0x28de` ← `0x2962`; `0x2904` ← `0x296e` / `0x3a58` | read_scroll, the sheet | no remaster moment `[?]` |
  | `0x1668` "%8s has died" / "%8s has aged" | `0xbc14` / `0xbc4e` | town_aging | `core.aging.x_has_died` / `x_has_aged` |
  | `0xb7fa` + "You need %ld more experience points" | `0x1814` | confirm_cost `0x17e4` | `core.guild.you_need_x_more_xp` |
  | OUT 0 "Not enough Magic" | `0x6fb8` | the field cast | `core.magic.not_enough_mp` (overworld / dungeon only) |

  Silent: `0x1586(1)` (after the ninth buy, "He's just bought ninth item", `form_alert` with no `0xb7fa`), training's
  "It will cost %d gold" (AGREE / NOT). The town cast's refusals have no alert (only "D O N E !").
- **The click** `0xb7e6` (`0x21ab0`, pack 33) follows a GEM pick: the menu bar (`0x13c` at `0x294` / `0x2ee`), a dialog's
  button after `form_do` (`0x4d2`, `pick_member 0x5da`, the transport picker `0x25b4`, the OK form `0xae1a`), the field's
  command loop `0x16fea` (s140 live: one click at `0x1719a` for **every accepted field command**, each overworld and
  dungeon step key, a map click, a menu pick),
  and the town lists drawn with `0x1964a` (`0xf64`, `0x173c`, `0x2f14`, `0x3012`, `0x362a`, `0x383e`, `0xc70`, `0x11ba`,
  `0x13b8`, `0x1438` PURGE, `0x19be`, `0x1a88`, `armory 0x1d54`, `0x2184`, `0x2a44`, `0x2db2`, `0x3578`): 24 sites (s140
  objdump: `0x294`, `0x2ee`, `0x56a`, `0x674`, `0xe6c`, `0x105e`, `0x1258`, `0x140c`, `0x1502`, `0x17ba`, `0x19f8`,
  `0x1cce`, `0x1f8a`, `0x21ec`, `0x25b4`, `0x2aa6`, `0x2ef8`, `0x2fae`, `0x3182`, `0x35c6`, `0x37dc`, `0x38e8`, `0xae7c`,
  `0x1719a`). Read one by one (s140, s141) and traced (s141, `parity/capture_s141_sounds.md`): `0x294` clicks on every
  town menu-bar pick (`evnt_multi` MU_MESAG with MN_SELECTED), `0x2ee` on **any** mouse button on the town picture
  (MU_BUTTON; a building hit or not). **`0x4d2` is a menu loop** (`evnt_mesag` until a pick of its menu title, then
  `0x56a`), not a form_do dialog: the Guild options (`0x4b8`) and a second title (`0x596`, from `0xded8`). Every `0x1964a`
  list window and the town's `form_do` dialogs click once as they return, an item or EXIT alike (`0xe6c`, `0x105e`,
  `0x1258`, `0x140c`, `0x1502`, `0x17ba`, `0x19f8`, `0x1cce`, `0x1f8a`, `0x21ec`, `0x2aa6`, `0x2fae`, `0x3182`, `0x35c6`,
  `0x37dc`, `0x38e8`; `0x2ef8` once per member in a party loop); `pick_member 0x5da` clicks only on its cancel (button 9,
  `0x674`), not on a member; the transport picker clicks before its cancel check (`0x25b4`, a town and the cancel alike).
  The field loop `0x16fea` clicks at `0x1719a` on every key it takes (a key it maps to no command returns the key code,
  ≥ 0, and clicks too), a map click and an Options menu pick; its Speed and Info menus do not. The field's own pickers,
  the event boxes' buttons and the alerts' buttons do not click. The click goes through `0xb8b0`, so at normal / slow
  Speed the next sound (22 after a pick, an alert's beep after a list pick) waits for it.
- **No caller `[C]`:** `0x42d4` (`0xb9b4` = body `0x21b7a` × 5, then the wait): no `jsr`, no relocated pointer, no raw
  `000042d4` in the image. Like III's sound 30, it is never heard.
- **The defeat scene** (`endgame.md` 8; wired s140, below): `0x12bea` "[original text]", the ghost row, **23**
  (`0x12d5e`), "[original text]", **22** (`0x12d86`) with the door's 90 steps of 4 VBL, (5th defeat:
  the sign), 3000 ms, **20** (`0x12e36`), the demon, "[original text]"; then `0x12678` plays **21**
  (`0x126d2`) before each member's fate. Speed is 20 there (7.2), so each waits for the one before.
- **Silent `[C]`:** the overworld's other steps (blocked, swimming `0xfac2(1)`, lava `0xfac2(0)`, the wilderness inn,
  location text, the 6 % encounter: its fight starts with 17), the dungeon's other event ops (1 "[original text]"
  `0x11aa0`, 2 / 4 and the REPORT `0x11c0e`, 5, 6 the item, 7 / 8 the gold "[original text]", 9 / 10 "SOME OF
  YOUR GOLD IS TAKEN", 11, 13, 15, 16's trap, 17 "MONSTERS APPEAR", 18–23), the trap's spring and "%s AVOIDS THE TRAP",
  the disarm's success, the exits, the town's services apart from the alerts above. (III sounds its gold ops 7 / 9; II
  does not.)
- **Pacing:** every one goes through `0xb8f2` first: at Speed ≥ 9 (normal / slow) it waits until the running sound has
  ended, at fast (5) it cuts it off (7.2).
- **TRANSPORTATION's fade `0x260a` `[C]` static (s140):** `0x4082(" ", 6, 15)` blanks the message line, sound 22
  (`0xb80e`), then registers 0..15 := 0 in turn (XBIOS 7 Setcolor), each followed by 10 Vsyncs (XBIOS 37); 100 Vsyncs;
  registers 0..15 := the town palette `0x23a62` in turn (= the pack's `palettes.town`), each followed by 7 Vsyncs; then
  `0xb9e2` = Dosound(`0x21a32`) (the silence script) and `0xb8f2`. 372 Vsyncs, 6.2 s at 60 Hz; sound 22 runs 385 ticks
  (7.7 s), so the silence cuts its tail. Traced s141 (label stops in the loop): 160 + 100 + 112 VBL, 6180 ms by the
  200 Hz timer; the harness's VBL is 60 Hz (EmuTOS US, `_vbclock` against `_hz_200`). No key is read. The town is entered after `0x260a` returns (`0x26cc`): the ST
  fades the old town's screen out and back in, then draws the new one.
- **The picker stays on screen through the fade** (s158 objdump `0x2598..0x25da` `[C]` static): `objc_draw` (`0x1ab46`),
  `form_do` (`0x1a94a`), the click (`0x25b4`), then `0x19158(tree, button)` = `0x190f0(…, 1)`: the button's SELECTED bit
  cleared **in memory** (no `objc_change` redraw, no `form_dial` finish). So the old town fades with the picker box still
  drawn over it and its button still shown selected; the screen is redrawn only by the new town's entry after `0x260a`.
- **Traced live (s140, `parity/capture_s140_sounds.md`)** `[C]`: every row of the table, with breakpoints on the players
  and the text routines (`tools/scripts/hatari_bp_trace.py`): the numbers, callers and order hold (sound first, its box
  0–11 VBL later, the questions' form 30 VBL later; field Resurrection's box first, 12 87 VBL later, after the box's
  hold; TRANSPORTATION's `0xb9e2` 375 VBL after 22; the town casts' beep and alert in one VBL). Op 3 is in no shipped
  script; it was heard on a test-patched chest. The trap's "SPOTS A TRAP. DISARM IT ?" is silent. s140 read the VBLs at
  50 Hz; the harness runs at **60 Hz** (s141), so 87 VBL = 1.45 s and 375 VBL = 6.25 s.
- **Traced live (s141, `parity/capture_s141_sounds.md`)** `[C]`: field Resurrection: the box drawn ~95 ms after
  `0xa18c`, held Speed + 1000 ms (1005 ms normal, 2005 ms slow), the party-view redraw `0x17210` 355 ms, then 12;
  TRANSPORTATION's fade 160 / 100 / 112 VBL as read (the 3 extra VBL are the click's wait and Dosound before the loop);
  alerts 0 and 1: the list's click, the beep (waiting for the click), `form_alert`; the defeat scene (`endgame.md` 8): 23
  3060 ms after line 1 (its 3000 ms hold), 22 3090 ms after the astral line, the door 6.87 s (413 VBL: 360 plus drawing),
  20 3010 ms after it, each fate line within 15 ms of its 21's start, both after the running sound (20 ~5.3 s, 21 ~3.9 s).
- **Remaster (s139):** Core notes each moment with `GameSession.Cue` (`FieldCues`, game-neutral names; `CuesSince` for
  the front end): the entry questions (`AskEntry`, not III's "load the saved map?" follow-up), a `BlockedWall` step on a
  wall cell or off the map (`HandleDungeonResult`), a failed disarm (`DungeonMoveResult.DisarmFailed`), ops 3 / 12 / 14
  (`Phantasie2DungeonScript` through `IDungeonScriptHost.Cue`), a TRANSPORTATION pick (`ChooseTransportDestination`,
  not a cancel), and `GameSession.CastCue` after a cast outside combat. The numbers are the ST pack's `field_sounds`
  (`OpenPhantasie.Data.FieldSounds`, written by `build_st_p2_asset_manifest.py`); the Client (`Game1.UpdateFieldCues`) plays
  them with the Sound switch on, in turn at COMBAT SPEED normal / slow and cutting off at fast (`wait_speed_ms` 9 against
  the Speed word). The remaster shows the line and starts the sound in the same frame, within the ST's 0–11 VBL for
  "sound, then the box"; field Resurrection's "box, then sound" waits for the box's hold since s141 (below); the
  TRANSPORTATION fade is drawn s140 (below); the town's "D O N E !" box is drawn s158 (below); the pacing does not
  model the no-wait after sound 18
  (`[0x21a30]`) and waits for any running effect, a combat run's too `[~]`.
- **Remaster (s140):** the pack's `field_sounds` gains `defeat_scene` (speed 20, ghosts 23, door 22, demon 20, fate 21),
  `alerts` (sound 34 and the 19 keys of the table above, each with the game modes it beeps in and its ST source) and
  `click` (33); `transport_fade` holds the fade (palette `town`, `vbl_hz` 60 as the colour cycle `[~]`, out 10, hold
  100, in 7, the silence). Data `FieldSounds.Defeat` / `Alerts` / `Click`, `StTransportFade`. Client:
  - `P2DefeatSounds` from the scene's state (`DefeatScene`): 22 when the door starts, 20 when the face first shows, 21
    with each fate line; each waits for the running sound (Speed 20). s141: 23 3000 ms after the defeat page shows.
  - `P2AlertBeeps`: a line that starts as a listed text, in a listed mode, beeps 34 through the `0xb8f2` pacing. The lines
    come from the session's log (Core's noted results) and from what a dialog returns (refusals Core does not note: the
    Guild's "party is full", a potion drunk); a result in both counts once. The Guild's move question beeps when it is
    asked. The remaster's lines are not the ST's boxes (s158 draws the boxes, below): a line of another moment with the
    same text beeps too `[~]` (s141: "UNUSABLE" counts only from the Use item dialog).
    Order: the ST checks for six members (`0xd4f8`) before it asks the move question (`0xd50e`); the remaster asks first,
    so a full party and a member of another town beep twice (question, then "party is full") where the ST beeps once `[~]`.
  - The click: on a town menu pick (`RunTownChoice`, the menu bar's `0x294`) and on the transport picker's pick or cancel
    (`0x25b4`); s141: the rest (below).
  - TRANSPORTATION: `TransportFadeFilter` on the finished frame (`VirtualScreen.Fade`): a pixel whose nearest town
    register is black now shows black; colours of the remaster's own (texts, panels) follow their nearest register `[~]`.
    Keys are dropped while it runs; at its end the running sound stops. s158: the fade runs on the frame from before the
    pick (`TransportDialog.BeforePick` → `VirtualScreen.Freeze`, before Core moves the party and before that frame is
    drawn: the old town with the picker, its picked row lit, as the ST's box), so the old town fades out and back in and
    the new one shows when it ends; Core has moved the party at the pick (its cues, log and autosave unchanged). Seen
    live on Xvfb (s158, `parity/shots/s158/remaster-p2-transport-fade-strip.png`: Pippacott with the picker fades out,
    black, back in, then Saxlingham). The ST's blanked message line (`0x4082(" ", 6, 15)`) has no remaster counterpart
    on that frame `[~]`.
- **Remaster (s141):** the pack's `field_sounds.cue_delays` holds field Resurrection's wait (`hold_ms` 1000 plus the
  Speed word, `after_ms` 355): Data `FieldSounds.Delays` / `FieldCueDelay`; the Client queues 12 that long after its
  line, and a key or click ends the hold (the redraw's 355 ms remain). `defeat_scene.ghosts_after_ms` 3000: 23 plays
  3000 ms after the defeat page shows, or as the page is left (a key ends the ST's hold), and 22 waits for it; the ghost
  row still shows with line 1 `[~]`, a fate line can lead its 21 by the running sound's rest `[~]`, the door keeps the
  static 6.0 s (the ST's 6.87 s include drawing) `[~]`. The alert `core.p2.unusable` is `dialog_only`. Clicks, gated on
  the pack's `click`: every key the overworld / dungeon takes (`Game1.FieldCommandKeys`: not Esc, Tab, modifiers or
  F-keys) as `0x1719a`; a Guild command or Exit Guild as `0x56a`; an Armory pick as `0x1f8a`; a town or Guild dialog
  that closes (`Game1.ClicksOnClose`; not the transport picker nor the remaster's menus; the Guild's "who will learn /
  be trained" pick only when backed out of, as pick_member) as the lists' sites and `0x674`; the Town's Cast spell key
  as `0x294`. No remaster moment: `0x2ee` (the remaster's town has no picture to click; its menu picks click as `0x294`).
  The remaster's dialogs are not the ST's windows one for one: a multi-step dialog clicks once, as it closes (the town
  cast: the ST at the spell pick) `[~]`. Not seen live in the Client `[~]`.
- **Remaster (s158), the alert boxes:** after the beep the Client draws the ST's own box (`P2AlertBox`,
  `UiOverlays.DrawStAlert`): the pack's alert lines carry the disks' `form_alert` string (`box`: PHANT.RSC / OUT.RSC free
  string n, or PHANT.PRG's `0x1668` / `0x1814` strings at `0x229ca`, `0x229ea`, `0x1f572`, read by
  `build_st_p2_asset_manifest.py`), its `printf` holes filled from the remaster line's own values in turn ("%8s has died":
  the name, padded to 8); `cue_boxes` gives the town casts' cue `town.cast_done` PHANT.RSC 9 "D O N E !". So the box
  shows II's text where the remaster's line is I's ("You can not afford it" over "Not enough gold."). Every alert string
  has icon 0. The box is modal and has no timer: s160 (user, s159: "wait for a key as on the ST"), only Return / Enter
  (the default button: EmuTOS `aes/gemfmlib.c` `fm_keybd` takes RETURN and ENTER, nothing else ends the form) or a
  click on its button closes it (s158 also took Space and Esc); every other key is used up and does nothing; boxes
  queue in order. A click outside the box (the AES rings the bell) is not modelled. The Guild's move question (PHANT.RSC 6, YES / NO) gets no box: the remaster asks it
  in its own dialog, which a box would not answer `[~]`. **Look** (`alerts.look`) `[~]`: TOS's AES and its 8×8 font are
  not on the disks, so the geometry is measured on EmuTOS 1.4's `form_alert` in Hatari (`parity/shots/p2-endgame-wand-improved.png`,
  `p2-endgame-wand-unusable.png`, `p2-train-*.png`; EmuTOS's `form_alert` comes from DRI's GEM AES, as TOS's does):
  root box (widest line + 2) × (lines + 4) cells of 8 px with its 1-px border, OUTLINED 3 px outside, centred in
  (0, 9, 320, 191) (rounding up); lines from cell (1, 1) left-aligned; one blank row, then the buttons, each (widest label
  + 1) cells with a 3-px border outside (the default button; a two-button alert's 2 px is not modelled), 2 cells apart,
  the row centred on whole cells. Colours: VDI 0 (fill) and VDI 1 (lines, text) = registers 0 / 15 of the palette the
  mode shows (`town` for town / Guild: black and white as the shots; `map` for the field: black and red, not filmed).
  s160: the text in EmuTOS's 8×8 system font (pack role `p2.alert`, the file above), one glyph per 8-px cell from its
  top-left: "UNUSABLE" lands on `p2-endgame-wand-unusable.png` pixel for pixel (top-left (104, 93)); the s160 Xvfb shot
  of "You need 767 more experience points" matches the `p2-train-*` shots' glyphs. No mouse pointer, no icon.
- **Remaster (s156), the defeat:** the ghost row shows only once line 1's hold is over (`P2DefeatSounds.GhostsShown`: s156
  objdump, `0x12d4c` line 1 and its 3000 ms, `0x12d54` `0x12b5e` the ghosts, `0x12d5e` 23; `0x12a8a` only lays out the
  slots, nothing is drawn before), the row empty until then. The door takes the traced 413 VBL (6.87 s at 60 Hz) spread
  evenly over its 90 steps (`DefeatScene.DoorVbl`; which steps the blits slow is not measured `[~]`). A fate line can still
  lead its 21 by the running sound's rest `[~]`.
- **Remaster (s163), seen live in the Client** (Xvfb, `parity/capture_s163_client.md`, `OPENPHANTASIE_SOUND_TRACE`): a
  lost fight now opens the scene on "[original text]" (before s163 the fight's report lines came first, one
  key each, putting the ghosts and 23 behind them: `Game1.AfterFightEnded` adds no report after II's line-by-line round);
  the ghost row and 23 about 3 s after the page shows, 22 as the astral page opens (23 long over), 20 9.9 s later (door
  6.87 s + 3 s), then the 21s. Field Resurrection: "ZANDOR IS RESURRECTED" on the message line, 12 between 1.1 and 1.5 s
  after it (frame steps 0.4 s; the rule's 1365 ms at normal). The remaster's defeat page waits for a key where the ST
  goes on after its 3000 ms hold `[~]`.

## 8. Tools available

- `tools/scripts/stx_to_st.py` (Pasti → .st), `mtools` (FAT12), `tools/scripts/st_prg.py` (PRG + fixups),
  `tools/scripts/extract_st_phantasie2.py` (tables + diff vs I).
- `m68k-linux-gnu-objdump` (apt `binutils-m68k-linux-gnu`): `objdump -D -b binary -m m68k` on the text segment (cut it
  out with `st_prg.py`; addresses are then image addresses).
- Ghidra (snap) and the `rea` MCP server for decompiling. Checked s75: `rea open_binary format=raw-binary
  architecture=m68k entry_point=0x0` on the PRG image (text + data cut out with `st_prg.py`) works with Ghidra 12.1.2 and
  finds 602 procedures (fixups not applied yet); the Amiga I `game` is the natural comparison binary.
- **Hatari 2.6.1** (apt `hatari`, also `hmsa`, `atari-hd-image`) is installed; it runs `.stx` directly (built-in Pasti
  support) and needs a TOS ROM image (EmuTOS is free) for parity checks. Not run yet.

## 9. Data and save files (task 3b.8, s76) `[C]` static unless tagged

Supersedes the §7 first-look bullets for `SCROLLS.DTX`, `MESS1–8`, `DNG1–8`, `OUT*.DAT`, `TWNS.*`, `MAPS.INT`, `SACK.DAT`
and `DNGX.SAV`. Extractors: `tools/scripts/extract_st_p2_{maps,towns,dungeons,scrolls,saves}.py` (shared
`extract_st_p2_common.py`), run from the repo root with `python3 -I`. Output: `extracted/{maps,towns,dungeons,scrolls,saves}/`.
JSON follows the shape of `GameData/phantasie1/*.json` where one exists (overworld, dungeons, messages, scrolls, towns,
creation-tables, elementals). `r` below is PHANT.PRG `0xfd40` = `rand() / 327` (0..100; DOS `rand0_99` [~]).

### 9.1 What is factory data and what is played state

| File | Kind | Source of the initial state |
|---|---|---|
| `TWNS.INT`, `MAPS.INT`, `DNG1–8`, `MESS1–8`, `SCROLLS.DTX`, `OUT*.DAT` bytes 520–2999 | static | shipped |
| `OUT*.DAT` bytes 0–519 (grid + fog) | played | `MAPS.INT` |
| `TWNS.DAT`, `GUILD.DAT` | played | `TWNS.INT` @2446 / @5950 |
| `DNG.SAV`, `DNGX.SAV`, `SACK.DAT` | played | none (deleted on a new game) |

**New game** (`UTILIT2.PRG` `0xb2c`, mode 2): for each of the 17 regions it reads `OUTn.DAT` (3000 B), copies the next
520-B block of `MAPS.INT` over bytes 0–519, and writes the file back. It then calls GEMDOS 65 (Fdelete; `0x1452` is a
`trap #1` stub) on `TWNS.DAT`, `GUILD.DAT`, `DNG.SAV`, `DNGX.SAV` and `SACK.DAT`. Mode 1 does the same for I's 18 regions.
`PHANT.PRG` `0xed50` always reads `TWNS.INT`. A missing `TWNS.DAT`/`GUILD.DAT` leaves the `TWNS.INT` templates in
use `[C]` live (s78: after START NEW GAME FROM SCRATCH the Guild lists only the three template elementals,
`parity/roster-live.md` §5); the readers at `0x273a`/`0x275a` (3504/7950 B) were not traced.
**So the factory-fresh state is fully on disk 1** (`TWNS.INT` + `MAPS.INT`) plus the static files.

Played copies compared (Pasti 1995 vs TOSEC 1987; manual observation, not in an extractor: `MTOOLS_SKIP_CHECK=1 mcopy -n -i
reverse-engineering/atari-st/original-images/phantasie2/tosec-st/phantasie2-v1.0-tosec-disk2.st "::*" <scratch dir>/`): all `OUT*.DAT` match byte for byte except the TOSEC `OUT6`/`OUT7`
grids, which have 452 / 408 cells unfogged (fog only, every diff below offset 520). The Pasti `OUT*` grids equal `MAPS.INT`
exactly. The `DNGX.SAV`, `TWNS.DAT` and `GUILD.DAT` contents differ between the two sets. `SACK.DAT` is all zero in both.

### 9.2 `OUTn.DAT` (3000 B) and `MAPS.INT` (8840 B)

`PHANT.PRG` `0x4b88` reads 3000 B into the region buffer `[0x248b6]` (= `[0x25b82]`); `0x4c8c` writes it back.

| Bytes | Content | Evidence |
|---|---|---|
| 0–519 | 26 × 20 grid, **cell (x, y) at (25 − x)·20 + y**, x 0–25 west→east, y 0–19 north→south; fog = tile + 120 | `0x48e6`, `0x6792` (`500 − 20x + y`); the Amiga I order |
| 520–594 | 15 feature records × 5 B: **(y, 25 − x, arg, dest_x, dest_y)** | `0x6528` matches `rec[0]` with y `[0x25d1c]` and `rec[1]` with `25 − [0x25d1a]` |
| 595–994 | 10 text pages × 40 B; 995–999 tail | `0x615e`: page base `+0x253` |
| 1000–1499 | 500 B with no code reference found; holds disk-buffer / editor leftovers (`DNG4`…`OUT18 DAX` directory-like entries in OUT1, `ENDING SEQUENCE`, text scraps) | `[~]` unused |
| 1500–2999 | the map picture: u16 BE background pattern, then polygons `[count][pattern][count × (x, y)]` until count = −1 | `0x5308`: `[0x1fbd0]` = buffer + 1500; drawn by `0x5028` |

- **vs Amiga I (2500 B):** bytes 0–999 have the same layout, and the polygon block moved from 1000 to 1500. The 500 B in between is the only addition.
- **vs DOS I (1024 B):** the DOS grid is row-major (y·26 + x), and its features are (x, y, …).
- **Feature scan limit:** `0x6528` scans records up to buffer + 620 (21 slots), so it runs into the text pages. If nothing matches, it shows an alert. The 15 real slots end at 594.
- **Dungeon destinations:** all 15 class-5 entrance features have `dest` landing on an exit cell (`0xd7–0xd9`) of `DNG(arg)`. The extractor asserts this.
- **Rings:** DNG7 has eight entrances, a ring in OUT28 around the castle cell (18,13).
- **Text-page rule:** the same as DOS (`arg` < 10 → 1 page; 10–19 → 2 pages from `arg − 10`; ≥ 20 → 3 pages from `arg − 20`). Pages that no text feature uses are flagged as leftovers, for example `[original text]` in 12 regions.

**`MAPS.INT`** = 17 × 520 B: the initial grid of each region in the order 1, 2, 3, 5, 6, 7, 8, 11, 12, 17, 18, 22, 26, 27,
28, 30, 31. The order and the 520-B copy come from `UTILIT2` `0xb2c`. Only regions 1 and 2 start unfogged.
Amiga I `maps.int` (9360 B = 18 × 520) is the same format. That also explains the Amiga I `OUT9/10/11/13/14` fog
differences noted in `amiga/FORMAT.md`: those Amiga files are played state.

### 9.3 Overworld step rules and the new terrain (manual Q10)

`PHANT.PRG` `0x60c6..0x651e` is DOS OV2 `0x5f3b..0x6188` with changes. Let class = tile / 10 and T = tile % 10.

1. **Blocked:** tile 100 is blocked.
2. **Step cost:** class ≤ 3 costs (class + 1)·75; anything else costs 60 (`0x612c`, as DOS).
3. **By T:**
   - T = 6: text.
   - T = 7: fixed fight, monster = feature arg + 1.
   - T = 8: plain move.
4. **By class:**
   - Class 5: dungeon.
   - **Classes 7 and 11 (new rule):** `0x6338`. When the per-trip flag `[0x25b8a]` is clear, a step fights with r ≤ 8 (≈ 9 %). The monster (1-based) = r / 34 + 12·T + 1, so 0-based ids 12T … 12T+2. The flag is then set; it is cleared in the state-1/3/6 handlers `0x73d0`/`0x7454`/`0x7520` `[C]` (leaving a town, dungeon → wilderness, party wipe; `logic-specs/terrain.md` §3). These tiles never take the 6 % random roll.
   - **Class 8 with T ≠ 0 → state 8** = `0xfac2(0)`, `" L A V A :"`: **every non-dead member** (`@24 ≠ 0`) takes **1 + r·T/10** damage. There is no skill roll and no encounter. Then comes the party-dead check. Tile 80 is a plain move with no encounter roll. In DOS, state 8 was the magical pool (class 11).
5. **Towns and inns:** tile 40 is a town (arg − 1 = town index); tile 41 is an inn.
6. **Water:** class 10 is swimming (`0xfac2(1)`: SWIMS if r < swim skill `@48`, else SINKS and takes (5r + 100)/100).
7. **Otherwise:** r < 6 gives a random encounter (`0xfe38`).

Where the new terrain appears (initial grids):

- **Lava, class 8:** tiles 81/82/83/85, max damage 11/21/31/51. Regions 17, 22, 27, 28, 30 and 31.
- **Class-7 fight terrain:** tiles 72/73/74. Regions 17, 18, 22, 26, 27, 28 and 31.
- **Class-11 fight terrain:** tiles 111/112/113. Regions 17, 18, 22, 26, 27 and 28.

Regions 1–12 have none of these. The monster triples are:

| Tiles | 0-based ids | Monsters |
|---|---|---|
| 111 | 12–14 | APPRENTICE DEVIL, ZOMBIE, GHOUL |
| 72 / 112 | 24–26 | LARGE SKELETON, WIGHT, DEATH FLY |
| 73 / 113 | 36–38 | DEVIL, WRAITH, GIANT WASP |
| 74 | 48–50 | DEMON, VAMPIRE, GHOST |

These are undead plus one insect, which matches the manual's Mist / Dark Voids ("undead and insect encounters abound").

**Lava = class 8 `[C]`** (the `L A V A` string sits on that path). **Mist = class 11 (dotted fill) and Dark Voids = class 7 (solid black fill)** `[~]`, seen live (s78). Both share one code path, and no game text names either. Full rules: `logic-specs/terrain.md`.

### 9.4 `DNG1–8` (2048 B) and `MESS1–8`

- **DNG:** the Phantasie I level format, unchanged.
  - `0x10320` reads `DNGn` into the 2500-B buffer `[0x25f62]`. It reads `DNGX.SAV` instead when `0x5af2(n)` finds a matching `DNG.SAV`.
  - Cell = `buf[y·33 + x]` (`0x1066c`), 33 × 38.
  - Monster bytes are at `+0x468 + cell` (`0x10d44`), event records at `+0x4f6` (`0x11ad2`) and scripts at `+0x5aa` (`0x11b3e`). These are the DOS offsets.
  - After the level data comes 0x0d / 0x00 fill and Phantasie I's `[original text]` / `ENDING SEQUENCE` leftovers (the same tail DOS / Amiga I `DNG1` carry; Amiga I DNG = DOS DNG byte for byte).
  - Every overworld `dest` lands on an exit cell (`0xd7`–`0xd9`): row 0 for DNG1–6 and 8, and all 8 exit cells of DNG7 (rows 0, 18 and 37). DNG2, 4 and 6 have further exit cells that no entrance uses.
- **MESS:** plain text; DOS I's are enciphered, Amiga I's are not.
  - `0x1159e` is DOS OV2 `0x2418`: the slot word is a u16 **LE** at 2·(id − 1), empty when it equals the next word. The record is a prefix byte + 40 chars at `0xe6 + word`.
  - The whole file goes into a 4000-B buffer `[0x24eb8]`.
  - Seven of the eight files have exactly the size of an Amiga I `mess` file (II 1←1, 2←6, 3←9, 5←7, 6←5, 7←4, 8←2), and their tails keep I's `ENDING SEQUENCE` bytes. II's texts were typed over I's files `[~]`. `MESS4` (3584 B) is new and NUL-padded.
- **Cross-check with the DOS rules** (`extract_dungeon_events.message/referenced_ids`):
  - Every level parses.
  - Three event/script references land on continuation records (DNG1 id 72, DNG6 id 60, DNG8 id 85), which never happens in I.
  - A few TEST-op references point past id 114.
  - So II's event/script interpreter may differ from I's `[?]`. Compare `PHANT.PRG` `0x11ad2`/`0x11b3e` with OV2 before building on `messages`.
- **Menu option count (s82, play-through test):** byte 1 of an event record is the menu's option count; it agrees with
  the I-rule decode on 121 records. I's option-label rule (`extract_dungeon_events.py`) cut DNG8 message 71 ("PUSH BUTTON"
  1 … 9, the door to the Ice Dragon) to two options and left message 76 (six-button teleporter) blank, so the dragon was
  unreachable. `build_p2_gamedata.py` now takes the count from byte 1 and splits the labels from the message words; how
  the ST lays the labels out is untraced `[~]`. Still blank / garbled but unreachable: DNG4 32/68, DNG5 59, DNG6 55,
  DNG8 110, DNG6 0xdf. (s83) Nothing to fix: no event on the grid or reached by GOTO EVENT / JUMP / RANDJUMP shows them
  (the play-through test `Data_EveryLiveEventMenu_HasItsRecordsOptionCount` pins every live menu), and no message cell
  (b − 0x76), description cell (b − 0x11) or wandering fallback (b + 3) of their level points at them. DNG4 32/68, DNG5 59
  and DNG8 110 are continuation slots of messages 30, 65, 57 and 108 (I's layout, never a message start); DNG6 55 is an
  empty slot; DNG6 event `0xdf` is on no cell and no live script reaches it.

### 9.5 `TWNS.INT` (13900 B) and `TWNS.DAT` (3504 B)

`0xed50` reads `TWNS.INT` in eight blocks:

| Bytes | Content | DOS I `TWNS.INT` offset |
|---|---|---|
| 90 | hp factors | `0x000` |
| 360 | spell levels | `0x05a` |
| 90 | MP ceiling | `0x1c2` |
| 1800 | skill coefficients, u16 **BE** | `0x21c` |
| 30 | class modifiers | `0x924` |
| 76 | race modifiers (15 × 5 + 1 pad; DOS 75) | `0x942` |
| 3504 | initial `TWNS.DAT` | — |
| 7950 | initial `GUILD.DAT` | — |

- **Creation tables (bytes 0–2445):** byte-identical to Amiga I `twns.int` and value-identical to DOS I. That includes the cells Phantasie I's GameData overrides (HALFLING DEX modifier, D-001; MONK spell 41 at level 7, D-006). `extracted/towns/creation-tables.json` keeps the raw values.
- **Town record (292 B)** = the DOS 291-B record plus a pad byte at 181, so every later field moves up by one:

  | Offset | Field |
  |---|---|
  | 0–180 | stock |
  | 182 | u16[45] restock list |
  | 272 | price index |
  | 274 | exit region |
  | 276 | exit **y** |
  | 278 | exit **25 − x** |
  | 280 | name (10 chars) |
  | 290 | u16 = 0 |

- **Towns (Q11):**
  - **Reachable:** 10 towns are reachable on the map. In each case a tile-40 cell carries a feature with arg − 1 = index:
    - PIPPACOTT (OUT2 16,3)
    - HASLEMERE (OUT1 10,16)
    - SAXLINGHAM (OUT6 6,8)
    - LLITHFAEN (OUT3 9,15)
    - IPSWICH (OUT7 9,14)
    - FERRON (OUT12 21,11)
    - BLACK MOOR (OUT18 6,2)
    - DEATHPORT (OUT17 11,5)
    - DEMONIAC (OUT27 13,7)
    - DARK ABYSS (OUT28 4,2)
  - **Leftovers:** records 10–11 (STARVILLE, OLYMPIA) are **byte-identical to Phantasie I's records**, and no map cell leads to them. No other route does either: TRANSPORTATION accepts only towns 1–10 and nothing passes I's
    town number 99 `[C]` (`logic-specs/towns.md` §3).
  - **Exit positions:** 8 of the 10 exit positions are the town's own cell. BLACK MOOR exits to OUT6 (16,8) (tile 61) and DEMONIAC to OUT18 (18,14) (tile 116, a text cell with feature arg 10). Leaving town copies these fields with no other test, so the two exits are
    one-way links between regions `[C]` (`logic-specs/towns.md` §2).
  - **Town numbers are 1-based `[C]`:** `0x26cc(n)` selects record n − 1, and n = 99 selects record 11 (OLYMPIA), as in DOS. The town-tile path stores the feature arg itself in `[0x25b94]` (`0x6418`), and `0xc080` copies a party member's `@254` there.
  - **Default town = PIPPACOTT:** `@254` = 1 = record 0 `[C]`; a new game starts there `[C]` live (s78, `parity/roster-live.md` §5). `0xbf5a` defaults the town to 1 when no member is found; the played roster has `@254` = 1; the I→II transfer sets byte 255 := 1, which answers §6's `[~]`. There is no PELNOR or Pelnar in II's town table (the backdrop file is still `PELNOR.PIC`).
- **Guild template (bytes 5950–13899):** 22 empty slots, then FIRE_ELEM / WATER_ELEM / EARTH_ELEM. Every field in `GameData/phantasie1/elementals.json` matches (checked); only the names differ (`_` for the space), and bytes 282–317 are zero. That makes the Guild 22 character slots + 3 elementals, as in DOS/Amiga I (manual Q11: not the C64's 21).
- **`TWNS.DAT` (played):** differs from the template only in PIPPACOTT's Armory stock (`twns-dat-played.json`).
- **Pricing and restock:** price indexes differ from I (BLACK MOOR 220, DARK ABYSS 225, DEATHPORT 180). The restock list matches I's except PIPPACOTT slot 43 (5 instead of 36).

### 9.6 `SCROLLS.DTX` (16384 B, plain) — scroll count (Q12)

- **Records:** 840-B records of 21 lines × 40 chars, NUL-padded. `0x13608(buf, n)` seeks n·840 and reads 840 B; `0x292a(first, extra)` shows records first … first + extra.
- **Contents:** 19 full records + 424 B of I's `ENDING SEQUENCE` leftover. I's DOS file is 20 × 800 and enciphered; the Amiga I file is 20 × 800, plain.
- **Item → record** (`0xe1e8`): `i > 137 ? i − 1 : i`, minus 121. Extra pages: 2 for item 121, 1 for 132, 2 for 134.
- **Count: 14 scrolls** (SCROLL A–N), using all 19 records:
  - A = [original text] (3 pages)
  - B = The Kobold Village
  - C = Senog's Laboratory
  - D = The Impling Hatchery
  - E = Filmon the Sage
  - F = The Imp Caves
  - G = The Summer Palace
  - H = Pluto's Smallest Castle
  - I = Pluto's Menagerie
  - J = Imps and Implings (2 pages)
  - K = The Book of Beasts (3 pages)
  - L = Senog's Travels
  - M = Departed Souls
  - N = The Forgotten Temple
- **Unnamed slots:** the six blank-named `SCROLL ` slots point at continuation pages. Item 137 shows the same page as L.

### 9.7 `DNGX.SAV`, `DNG.SAV`, `SACK.DAT` (played state)

- **`DNGX.SAV`** (2500 B) is the live level buffer (`0x5bdc` read, `0x5e2a` write), as in DOS I.
  - The Pasti copy is DNG1 with 10 changes: forced-fight cells `0x80–0x83 → 0x8a` (cleared, the DOS rule), one lock cell `0xc6 → 0xc7`, and four script bytes at 1490–1496. After 2048 come zeros.
  - The TOSEC copy is DNG1 with 53 changes.
- **`DNG.SAV`** (written by `0x5d06`): u16 BE dungeon id, then a GEM raster copy of the 165 × 152 dungeon-map rectangle, w·h/2 bytes.
  - The TOSEC copy is 13378 B with id 1; the Amiga I file is 13378 B too.
  - The reader `0x5af2` compares the id and then reads up to 32000 B.
  - **The Pasti copy (1024 B) equals `DNGX.SAV`'s first 1024 B** (its directory entry is cross-linked), so it is damage, not data.
- **`SACK.DAT`** (120 B): 60 × u16, the DOS I layout `[~]`, big-endian on the ST `[~]`. All zero in both sets. `UTILIT2` `0xcd8` zero-fills a 120-B sack buffer.

### 9.8 Still open

- ~~Which of classes 7 / 11 is Mist / Dark Voids~~ and ~~when `[0x25b8a]` resets~~: answered in `logic-specs/terrain.md` (s78).
- II's dungeon event/script interpreter vs DOS (continuation hits, ids > 114).
- ~~Why BLACK MOOR's and DEMONIAC's exit cells are not their town cells~~: they are one-way links (`logic-specs/towns.md` §2).
- What feeds `OUT*` bytes 1000–1499, if anything.

## 10. Graphics (task 4b, s78) `[C]` static + live-checked in Hatari where tagged

Extractor `tools/scripts/extract_st_p2_graphics.py` → `extracted/graphics/` (PNGs, `extraction-index.json`,
`maps/maps.json`); `tools/scripts/build_st_p2_asset_manifest.py` → `asset-manifest.json` (monster / race ids → images)
and `pack.json` (a T1.5 `phantasie.graphics-pack/v1` draft for II, not wired into `OpenPhantasie.Data` yet);
`tools/scripts/verify_st_p2_graphics.py` re-runs the live checks against `parity/shots/gfx-*.png`. 262 PNGs, 1.3 MB.

### 10.1 Raster format and palettes

- **Everything is ST low-res device format** `[C]`: 4 word-interleaved planes, 8 bytes per 16 pixels, row stride
  ⌈w/16⌉·8, pixel value = plane bits 0–3 = hardware colour register. Sprites are GEM MFDBs (`0x19a72`: fd_stand 0,
  4 planes) blitted by `0x19990` → vro_cpyfm mode 3 (**replace: colour 0 is opaque**, no mask). The one exception is
  the overworld fill patterns (vsf_udpat, 4 planes **sequential**, 16 words each, §10.6). Raster copies and
  multi-plane user patterns bypass the VDI colour-index mapping, so the PNGs use the hardware registers directly.
- **Palettes** are XBIOS Setpalette (6) tables of `0x0RGB` words, 3 bits per gun. PNG colours are v·255/7; Hatari
  shows v·0x22 (the checks compare gun values).

| Name | Where | Set by / used for | Live |
|---|---|---|---|
| `start` | `START.PRG` image `0x156` | title (`COVER1`/`COVER2`); `0x176` (all black) is set first | `[C]` title |
| `town` | `PHANT.PRG` `0x23a62` | `0x238c`, `0x260a` (fade-in by Setcolor), `0x2d0e`; `PELNOR.PIC`, town walker | `[C]` town |
| `map` | `0x22956` | `0x5712` overworld, `0x5cea` dungeon map; patterns, icons, glyphs, compass | `[C]` overworld |
| `combat` | `0x23a82` | combat screen (monsters, portraits), `0x12d34` DECOR, `0x130ec` ending fade-in | `[C]` combat |

`combat` equals `start` except registers 3, 6, 7, 8, 12, 14. **Colour cycle** `[C]`: the VBL routine `0x1382e` (in
the text segment) rotates hardware registers 4 → 7 → 14 every N VBLs (counter `0x13862`/`0x13863`). In the `map`
palette those are the water pattern's three colours (pattern 10 uses only 4, 7, 14) and part of the lava pattern 8, so
water and lava shimmer on the overworld. The PNGs show the uncycled table.
- (s82) Each tick: `d0 = reg 14; reg 14 = reg 7; reg 7 = reg 4; reg 4 = d0` (a colour moves 4 → 7 → 14 → 4). N = byte
  `0x13863` = **14** (nothing writes it), so one step every 14 VBLs: 0.23 s at 60 Hz, 0.28 s at 50 Hz (which rate a
  US-sold ST ran is `[~]`; the remaster pack assumes 60).
- (s82) **Only on the overworld** `[C]`: `0x137c0` (Setexc-style VBL slot install, `0x7410`) runs only in main-loop
  mode 2 (wilderness start); `0x137e6` removes it in mode 1 (town, `0x73aa`), mode 11 (encounter, `0x7474`), mode 6
  (defeat, `0x752a`, and `0xfe1c`), and on dungeon load (`0x10324`). So the dungeon map, combat and town never cycle.

### 10.2 Screens (`.PIC`, headerless 160-B lines)

| File | Lines | Shown | Palette |
|---|---|---|---|
| `COVER1` | 200 | `START.PRG`: read to the screen | `start` |
| `COVER2` | 135 | `START.PRG`: read to screen + 10400 = **line 65**, over `COVER1`, after 251 VBLs (`dbf` from 250, s102) (`screens/title-credits.png` = the composite) | `start` |
| `PELNOR` | 200 | town backdrop (`0x43ec`), GEM menu bar and empty name scroll baked in; the town name, menu text and walker are drawn on top | `town` |
| `DECOR` | 155 | `0x12bea`: the door-and-demon sequence (§10.4) | `combat` |
| `FINAL2` | 160 | ending (`0x12f2a`), read to the screen top | `combat` |
| `CROWD` | — | **not a screen**: 3 frames of 235×31 (3720 B each = 15 words × 8 × 31), read into the `MSTR2` buffer and blitted to (40,127) over `FINAL2`: frames 0/1 alternate 10× (330 ms waits, AES `evnt_timer`), then frame 2 (`0x131a0`..`0x1327a`; `logic-specs/endgame.md` §7.1). This answers §7's "11160 B is not a multiple of 160". | `combat` |

### 10.3 Monsters and portraits

- **`MSTR1.PAT` / `MSTR2.PAT`** (76,832 B): 80 sprites; offset table `0x23d74` (u32 × 80, `0x1836c`), size table
  `0x2112e` ((u16 w, u16 h) × 80, read at `0xfdca` by the encounter builder → `0x18288`) `[C]`. Offsets are contiguous
  (offset[i+1] = offset[i] + ⌈w/16⌉·8·h; the last ends at 76,832). Widths 32/48/64/80, heights 21–80 (the Pluto set and
  the big dragons/giants are 80×80). `MSTR1` = idle, `MSTR2` = attacking (combat `0x18776`: `0x186a8` shows MSTR2,
  waits 300, shows MSTR1) `[C]`; live: WILD DOG idle and attacking match pixel for pixel.
- **`PARTY1.PAT` / `PARTY2.PAT`**: 16 × 512 B, 32×32, index = character @256 (race) `[C]` (`0x18676`, `0x12e78`).
  `PARTY1` = idle (live: HUMAN, ELF, GNOME, ORC, TROLL match exactly); `PARTY2` = the alternate pose (`0x18f7a` swaps
  the two in combat, the ending alternates them) `[~]` not caught on a live frame. The ORC and TROLL portraits are
  identical to the ORC (#8) and TROLL (#56) monster sprites.
- `ui/empty-portrait.png` (`0x22756`, 32×32) is the **ghost portrait** (a white ghost with red eyes; the file name is
  older than the reading) `[C]` static s95. `0x12b8e(slot, flag)` draws it instead of the race portrait when the flag
  is 0. Only the defeat scene does that: `0x12a8a(0)` lays out one slot per party member (DEAD included, the summoned
  elemental dropped by `0x12680`) in a row at **y = 158**, 32×32 each, gap `(320 − 32·n)/(n + 1)` (`0x1814e`, also the
  combat row's spacing); after "[original text]" (`0x12d46`) `0x12b5e(0)` turns **every** slot into the
  ghost (`0x12d54`). Each fate then redraws its slot (`0x126bc` loop): DESTROYED clears it (`0x12824`), RESURRECTED draws
  the race portrait again (`0x128c2`), [original text] sets race 15 first and draws the UNDEAD portrait (`0x1290a`).
- `ui/ghost-mark.png` (`0x23cdc`, 16×19, a small white figure on a grey stand) marks a **member dying in combat**
  `[C]` static s95: `0x9894` (kill: status 0, HP 0, CON loss, "%s DIES") calls `0x188fc(member, 0)` → `0x18a02`: the mark
  at portrait + (8,13) on the combat row, sound 15, a 500 ms wait (`0x16cbe` = AES evnt_multi timer; a key or click
  ends it early), then `0x185d6(1)` rebuilds the row from the non-DEAD members only (`0x18176`), so the dead member's
  portrait leaves the row. The row's x positions (`0x181ca` [C] s98): `0x18176` lists the non-DEAD members, `0x1814e`
  gives the gap `(320 − 32·n)/(n + 1)` and the portraits go at `gap + i·(gap + 32)`, so a rebuilt row spreads out over
  the screen again. Built s98: the remaster's II combat cards are centred on these slots
  (`PartyStripLayout.ComputeEvenRow`).
  **s134 `[C]` static, when the dead leave the row:** (1) at the death itself, mid-round: `0x9894(m, 0)` (combat; arg 2 =
  field: sound and message, no mark) prints "%s DIES" (`0x214ae`, `0x18cc2`), then `0x188fc(m, 0)` → mark, 500 ms,
  `0x185d6(1)`. That is the only combat writer of status 0: the other `clr.w $18(a0)` are guild Drop (`0xc1d4` in
  `0xc14a`) and a non-party table (`0xe8e8`, base `0x25ea0`); the other `0x188fc` calls push mode 1 (`0x7748`, `0x86fe`)
  or 2 (`0x79fc`). (2) The rebuilt list `0x24c26` keeps party order without the gaps (`0x18176`), so later members move
  left and the row re-spreads. (3) Only DEAD leaves: the test is `@24 ≠ 0` and the status table (`0x1f067`) is DEAD / OK /
  SLEEP (UNDEAD is a race), so sleepers stay; II has no stoned / unconscious status. (4) Every row build filters the
  same way: encounter start (`0xfd82` → `0x1823c` → `0x181ca`; `0xfd58` is dead code, s162), victory (`0xa2b4`), the elemental joining
  (`0x8402..0x8422`, count 6 → 7, `0x185c4`) and leaving (`0xa298..0xa2ac`), so a dead member stays off every later row
  until resurrected. (5) A resurrection in combat brings the member back at once: "%s IS RESURRECTED" (`0x9286`) and,
  in main-loop mode 5 (`0x25d1e` = 5 = `combat_round 0x7610`, jump table `0x2126e`), `0x185c4` → `0x185d6(1)`. The
  remaster (`UiOverlays.CombatRowShows`, s97) matches all five; it shows a whole round at once, so every member who died
  in a round carries the mark together for 500 ms after the round, where the ST marks and re-packs at each "DIES" line
  `[~]` (no per-line round playback in II).
  **s135 `[C]` static, the ST's per-line timing (why it is not built):** "%s DIES" goes through `0x18cc2` with n = 15
  (no sound, no wait), then `0x18a02`: the mark, sound 15, `0x16cbe(500)` (a key ends it), `0xb8f2` (at Speed ≥ normal
  wait for sound 15 to end), `0x185d6(1)` re-pack. Every other round line is `0x18cc2(text, n)` (7.2): its sound n,
  `[0x1fefa]` ms (fast 5 / normal 10 / slow 1000, or a key), and a cast line the caster's 300 ms pose first. At the
  default Speed the round's pace is set by the sound lengths (each waits for the one before). **s136: built** (7.3): the
  remaster plays II's round line by line with these sounds and the COMBAT SPEED option, marking and re-laying the row at
  each member's "DIES". The row filter itself (s97 / s134) is right.

### 10.4 Small bitmaps in the text segment

`PHANT.PRG` image `0x13862`..`0x1640c` is data, not code (right after the colour-cycle routine) `[C]`:

| Image | Addr | Size | Drawn |
|---|---|---|---|
| `decor/demon-face` | `0x13864` | 80×80 | red demon face: `0x12e50` at (123,19) behind the DECOR door; `0xdd92` at (146,114) |
| `decor/door` | `0x144e4` | 80×90 | wooden door on DECOR (123,14). s95 `[C]` static: after "ARRIVED AT THE ASTRAL PLANE" and sound 22, `0x12d8c..0x12de2` steps h = 90 → 1 with y = 104 − h: 4 Vsyncs (XBIOS 37), then the door's **top** h rows at (123, y) (`0x19990` builds the MFDB from the image start with height h, source (0,0)). The door sinks into the floor (its bottom stays at y = 104); the black doorway on DECOR and the door's black top rows leave no trail. 90 steps × 4 VBL = 6 s at 60 Hz, 7.2 s at 50 Hz: s97 static, neither `START.PRG` nor `PHANT.PRG` writes the sync mode (`$FF820A`) or reads `palmode` / `_vbclock` / `_frclock` / `_hz_200`, so the rate is the machine's (US TOS 60, PAL TOS 50) and the game fixes none; the remaster runs the pack's `vbl_hz` 60 (decided below). s98: both ST sets in the repo are SSI's own v1.0 (`original-images/SOURCES.txt`: the atarimania Pasti dump and TOSEC "(1987)(SSI)"), no European publisher or PAL edition is known, so a US machine at 60 Hz is the likeliest reference; nothing on the disks can settle it further (the timing is not in the program). **s109, decided 60 Hz `[C]` for the US release:** TOS picks the rate at boot from its header's `os_conf` word (offset `0x1C`, bit 0 = PAL): a US TOS has bit 0 clear (EmuTOS `etos192us.img`: `os_conf` = `0x0000`, country 0, NTSC; `etos192uk` = `0x0007` and `etos192de` = `0x0003`, PAL), and the game never changes it, so the US ST runs 60 Hz VBLs: COVER1 251 VBLs = 4.18 s, the door 360 VBLs = 6 s. A European ST would run 50 Hz (5.02 s, 7.2 s); no PAL edition is known. The ms waits are AES timer waits, the same on both. Then the sign on the 5th run (and 3000 ms), 3000 ms, sound 20, the demon face, "[original text]" |
| `town/walker-0..6` | `0x152f4` | 16×22 × 7 | the walker in the town street (`0x4738`, y 156, x 110..183; frame tables `0x1fafc`: 2 3 2 1 / 5 6 5 4; frame 0 blank). Live: frame 5 matches |
| `maps/dungeon/compass` | `0x157c4` | 64×63 | dungeon map, at (214,68) (`0x5c7a`; as Amiga I) |
| `decor/sorry-sign` | `0x15fa4` | 48×40 | "Sorry, we've run out of Demons": (139,39) on the fifth run of the DECOR sequence (counter `0x231fa` = 5). s83: wired as `decor.door` / `decor.demon-face` / `decor.sorry-sign` in the pack; the Client draws them on `ui.decor` behind II's defeat prompts |

### 10.5 Overworld map picture

`0x5760` sets the cell geometry **12×9, origin (4,15)**, map rect 312×180 (as Amiga I) `[C]`. `0x5028` draws
`OUTn.DAT` + 1500: fill the rect with the background pattern (first word; 10 = water in all 17 regions), then each
polygon `[count][pattern][count × (x, y)]` until count = −1 with v_fillarea on a workstation opened with fill interior
4 (user pattern; `0x5274` work_in) and perimeter off (`0x52fa`) `[C]`. **Polygon pattern = terrain class** of the cells
it covers (checked per region: the polygon pattern set equals the grid's class set apart from class 4/5) `[C]`.

- Rendering rule used by the extractor (even-odd scanlines, edge counted for min_y ≤ y < max_y, crossing rounded half
  up, inclusive spans, and **the polygon's top row not filled**: GEM's fill loop runs while y > fill_miny): **live OUT2
  matches pixel for pixel** outside the three icons and the mouse (244 px, all theirs) `[C]` (s135; filling the top row
  too, as before, left 15 px off on top-row horizontal edges; the TOS integer rounding `((2dx·dy1/dy) + 1) >> 1` with C
  truncation instead of half up gives 33 off, so the half-up crossing stays). This is not Amiga I's AreaEnd edge error
  (Amiga FORMAT.md, s128): the extractor never used PIL's polygon fill, and the VDI fill has perimeter off.
- **Icons** `0x223d6` + kind·128, 16×16 MFDBs blitted through a **12×9** cell rect (`0x4af2(rect, x, y, 1, 1)`):
  0 town (tile 40, one slot), 1 inn (tile 41, one slot), 2 dungeon (class 5, 8 slots), 3 party (`0x56d6`, at the
  party cell `0x1fbd4`), 4 "castle" (other class-4 tiles, 5 slots) `[C]`. Scan `0x4cd0`: x outer 0–25, y inner 0–19,
  fogged cells count (tile − 120); single slots keep the last hit, multi slots fill in scan order and extra hits are
  dropped. Live: party, inn and dungeon icons match pixel for pixel. II draws **every** dungeon entrance (DNG7's ring
  of 8 in OUT28) where Amiga I drew one icon per kind; `maps.json` keeps the amiga-maps/v1 one-cell `icons` and lists
  all cells in `icon_cells`.
- Dungeon map: 5×4 cells, origin (10,16), glyphs `0x22656` + (id − 0xe6)·32, 16×4 MFDBs blitted 5×4 (`0x5e50`), ids
  `0xe6..0xed` as DOS / Amiga I `[C]` static (not live-checked; same MFDB path as the icons).
- Font: **no font in the files** `[C]`. `PHANT.PRG`'s VDI calls never use vst_font (21) or vst_load_fonts (119),
  only vst_height (12), so all text is the TOS ROM system font (the baked-in menu bar of `PELNOR.PIC` shows Atari TOS's;
  EmuTOS draws its own over it). `pack.json` keeps the remaster's font; s160 (user, s159) the alert boxes, the
  end-of-fight boxes and the ending draw in EmuTOS's 8×8 system font (`fonts/emutos-st-8x8.json` and its italic, the
  pack's `fonts` roles and `ending_scene` fonts; 7.3a, 7.4, `endgame.md` 7.1), the font's source and GPL-2.0+ licence in
  `fonts/emutos-st-8x8.LICENSE.txt`.
- RSC art: **none** `[C]`. `BACKUP.RSC`, `PHANT.RSC`, `OUT.RSC` have 0 ICONBLKs, 0 BITBLKs, 0 images (menus, dialogs,
  strings only).

### 10.6 Fill patterns per terrain class (evidence for Mist / Dark Voids, D2-008)

`0x21dd6` + class·128, 12 patterns `[C]`; pixel counts by colour register (`map` palette):

| Class | Pattern | Registers |
|---|---|---|
| 0 | green speckle on black (grass / plain) | 0, 12 |
| 1 | trees | 0, 2, 9, 10 |
| 2 | green-brown bushes / hills | 0, 9, 10, 13 |
| 3 | brown mountains | 0, 9, 13 |
| 4, 5 | solid black (icons go on top) | 0 |
| 6 | grey speckle on black: 35 of 256 px colour 8 (#333 grey) | 0, 8 |
| **7** | **solid black, all 256 px colour 0** | 0 |
| 8 | lava: dense multicolour (7 registers incl. cycled 7 and 14) | 5–8, 12, 14, 15 |
| 9 | orange dashes on black (36 px colour 11) | 0, 11 |
| 10 | water (all three cycled registers) | 4, 7, 14 |
| **11** | **grey speckle on black: 35 px colour 8**, a different dot layout from class 6 | 0, 8 |

Where they appear (polygon patterns = cell classes, §9.3 regions): class 7 covers large black areas (e.g. 222 cells of
OUT18, 221 of OUT27) and class 11 grey-speckled areas in regions 17, 18, 22, 26, 27, 28. So **class 7 is drawn as a
featureless black void and class 11 as grey haze on black**, which reads as Dark Voids = 7 and Mist = 11 `[~]` (picture
evidence only; no game text names either, and the step code treats both alike). Class 6, which looks like class 11,
appears in regions 1 and 2 (OUT1: 29 cells; I's class 6).
