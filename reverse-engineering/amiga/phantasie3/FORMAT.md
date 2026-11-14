# Phantasie III (Amiga v1.0): graphics formats (4c)

Phase 09 T4, 4c (s97). Static RE of `unpacked/PhantasieIII/PhantasieIII` (hunk exe, Aztec C; README). Addresses are
offsets in the flat image of `tools/scripts/extract_amiga_p3_common.py` (`load_flat`: CODE at 0, DATA at `0x21890`);
`m68k-linux-gnu-objdump -D -b binary -m m68k:68000` over that image reads them. Rules and numbers stay DOS
(`reverse-engineering/dos/phantasie3/FORMAT.md`); this file covers only the art. Extractor:
`python3 -I tools/scripts/extract_amiga_p3_graphics.py` → `extracted/graphics/` (PNGs, `extraction-index.json`,
`pack.json` = the pack manifest `src/OpenPhantasie.Data/GameData/phantasie3/graphics/amiga.json`).

Tags as elsewhere: `[C]` read in code (and checked visually), `[~]` inferred, `[?]` not decoded.

## 1. Packed screens: `.cmp`, `.csh`, `D/s6` `[C]`

No IFF anywhere. Loader `0x123c(name, buffer)`: 32-B palette (16 words) → global `0x2437e`, then a u16 packed length,
then that many packed bytes (files are padded to 128-B blocks). Unpacker `0x1208(packed, screen)` picks the order by
the first packed byte:

| First byte | Routine | Order |
|---|---|---|
| 1 | `0x2b72` | the 32000-B screen in memory order (only `dlgbttns.cmp`) |
| else (2) | `0x2bc6` | plane by plane (4 × 8000 B); inside a plane, column by column: 200 bytes down one 8-pixel column, then the next |

Run codes (both routines): `c = 0` → u16 count, then one byte repeated; `c > 0` → `c` literal bytes; `c < 0` → `−c`
copies of the next byte. The result is always a 320×200 screen of 4 sequential planes (40 B per row). Every file's
packed length is used to the byte by the decoder (checked by the extractor).

**Palettes** `[C]` `0x11ae`: each word is masked `& 0x777` and shifted left once before it goes to the colour registers,
so the art is ST-style (3 bits per gun, even Amiga nibbles only). The PNGs use the same Amiga→ST table as Phantasie I's
Amiga map art (`amiga_gfx_common.AMIGA_TO_ST`: 3-bit `c` → `round(c · 255 / 7)`); I's sprites and pictures are `c · 17`
since s124 / s125 (`amiga/FORMAT.md` "Palette tables in use"); the hardware
value is nibble `2c` (8-bit `34c`, 238 at the top).

## 2. Shape sheets: `.csh` `[C]`

`0x293e(name, table, first, wide)` unpacks a `.csh` screen and cuts **15 shapes** (`k = 0..14`) into slots
`first + k` of the shape table `0x24ca8`. Shape `k` sits in a 48×48 grid cell, 5 per row, at
(`48·(k mod 5)`, `48·(k div 5) + 1`). Its size is stored in screen row 0: plane `k mod 4`, byte `2·(k div 4)` =
rows − 1, the next byte = width in bytes (empty shapes have 0). Shape memory: `[?, rows − 1, width bytes, ?]` then rows
of 4 interleaved plane words (blit `0x2c4e`, plain copy, colour 0 opaque).

| File | First slot (`0x7498…0x750e`) | Shapes | Use |
|---|---|---|---|
| `anatomy1.csh` | 16 | 15 body-area pieces | body-area figure (§2.1) `[C]` |
| `anatomy2.csh` | 31 | the last 6 figure pieces (31–36) + 3 sparkles (37–39) | §2.1; the sparkles are the flash `0x1b814` (§2.4) `[C]` |
| `dng.csh` | 45 | 9 dungeon map glyphs | §5 |
| `scroll.csh` | 54 | 15 parchment edge pieces | the frame of text scrolls (§2.2) `[C]` |

Later loads overwrite earlier slots past their 15 (so `anatomy1` keeps 16–30 only after `anatomy2` at 31).

### 2.1 Body-area figure `[C]` (s98)

`0x9694(member, rastport)` draws one member's stick figure; its only caller is the **party roster** `0x93e0` (12
callers: the main screen in town, wilderness and dungeon, and combat), which draws it for every member whose status
(record `+0x2c`) is not 0 (Dead) — the manual's "stick figure diagram under the character's status". The roster places
members in 2 columns × 3 rows: figure origin (table `0x220c2`, x in bytes) (27, 40) / (27, 104) / (27, 168) for members
0–2 and x 35 for 3–5, i.e. pixels x 216 / 280; with `0x240d8` = 1 (the combat orders screen, s118 re-read of `0x96d2`: the `− 8` is on the `= 1` branch) y − 8, and in mode 2 members 1 / 4 another
− 8 and 2 / 5 − 16. Text beside it: name at text column 25, HP / MP at 29 / 27, the status name.

`0x95f8` first swaps the record's arm words (`+0xf4` ↔ `+0xf6`) and leg words (`+0xfa` ↔ `+0xfc`), so the viewer's
left is the character's right (swapped back after). Then pieces `k` = 0..6 at offsets `0x220da` (x bytes · 8, y):

| k | Area (record) | Offset | Gone clears (`0x648`, inclusive) |
|---|---|---|---|
| 0 | head (`+0xf2`) | (8, 0) | x 8–16, y 0–7 |
| 1 | right arm (`+0xf6`) | (0, 8) | x 0–8, y 8–15 |
| 2 | left arm (`+0xf4`) | (16, 8) | x 16–24, y 8–15 |
| 3 | torso (`+0xf8`) | (0, 16) | x 0–8, y 16–23 |
| 4 | right leg (`+0xfc`) | (0, 24) | x 0–24, y 24–31 (the only `+16` case) |
| 5 | left leg (`+0xfa`) | (16, 24) | x 16–24, y 24–31 |
| 6 | chest (no area) | (8, 8) | — always shape 22 (status ≠ 0 here) |

Shape = slot `16 + 7·state + k` for state 0–2 (Okay white, Injured orange, Broken red); state 3 (Gone) gives ≥ 21 and
the routine fills the "clears" rectangle in colour 0 instead. The blit `0x2ce4` is **masked** (mask = OR of the four
planes), so colour 0 is transparent. Every piece's art is the left 8 pixels of its 16-wide shape (the torso 12 of 32).
Remaster: pack section `body_figure` (`amiga.json`), pieces `body/<slot>.png` in the main-screen (overworld) palette
with colour 0 transparent; drawn in the III wilderness / dungeon party cards (`UiOverlays.DrawBodyFigure`).

### 2.2 Text-scroll frame `[C]` (s98)

`0x1e2c2(rastport, text, frame)` — text scrolls (`0x1f7e2`; first page only, `frame` = 1, later pages refill the
inside), and two other text pages (s102, both decoded `[C]`):

- **`0x1f018`, the gods' reward speech** (§3.1, `0x1ee82`): drawn with the frame and no text on the back buffer while
  the god's picture shows, filled with strings 40–43 (`0x23a62`) at text column 1, rows 1 / 6 / 11 / 16 (colour 2 on 4)
  plus the words that differ by outcome at fixed cells: the god (strings 36 / 37 "Pluto." / "Zeus.") at (31, 1), the
  cause (38 / 39 "evil" / "good") at (10, 7), the title (44 / 45 "Darklords." / "Superheroes.") at (9, 10); then the
  buffers flip (`0x724`), the **dungeon palette** is set (`0x11ae(0x244de)`) and a key is awaited (`0x1e8c4`). Built
  (s102): `GodsRewardScene` page 3, the remaster's DOS text wrapped on the parchment (not placed at the Amiga's cells).
- **`0x1d9ba`, dungeon event texts** (`0x1d982(text, menu)`, event op 11 and the menus): the first text of an event
  program draws the frame (no text) and "Press a key" / "Press a number" (string 18 / 19, the latter when a menu
  follows) at column 14 row 23, colour 2 on 4; every text then prints at column 1 from row `0x24074` (1, 2, ...), which
  each text and each line break advances, scrolling the inside from row 22 (`0x854` with `0x24072` set); a text with no
  menu waits for a key. The rows pile up on one page until the event program ends: the step routine clears `0x24074`
  (`0x1bf62`) and flips back to the dungeon screen after a program that showed a page (`0x1c1f0`).
  s108 details `[C]`: `0x1d982` returns at once for a text of 2 bytes or less (`0x2127c`, the same < 3 test as DOS); the
  square's call passes `menu` = `E[1] & 14` (`0x1c8e4`), so a square with 0 / 1 choices waits and one with more shows
  "Press a number" and loops on `0x1e8c4(0)` until a digit 1…n (`0x1c902`); op 11 passes 0 (`0x1cbfe`). The key wait
  `0x1e8c4(1)` takes any key or a mouse button (`0x24066`). An event fight clears `0x24074` too (`0x1da86`), so the next
  text starts a new page. The other event messages (op 7 gold, 9, 12, 14, ...) set `0x240fa` and go through `0xb126` and
  a window (`0x3904`) at the op tail `0x1d1a6`, not this page (their look is not modelled `[?]`; the remaster logs them).
  Built (s108): Core `DungeonTextPage` (`GameSession.DungeonPage`, saved; the program stops at the text and runs on after
  `AcknowledgeDungeonPage`, sub-square callers chained in `DungeonScriptResume.Caller`); Client: the page on the
  parchment (`DrawScrollFrame` with the foot line at cell (14, 23)), edges picked once per page, the newest rows in view; any key (Esc too) or a
  click anywhere answers "Press a key", only the numbers (or a click on a choice) answer "Press a number". The remaster's rows are its 38-character data
  lines at 10 px (the Amiga's 8-px text cells and its own wrap `0x854` are not copied).

The parchment pieces use colours 0, 1, 3, 4 only, which are the same in the map, dungeon and `scroll.csh` palettes, so
they look the same on all three pages. Fill (8, 8)–(312, 192) in colour 4; the text from text cell
(1, 1) in colour 2 on 4 (`#B6926D` / `#924924` in the map palette). With `frame`, for column `i` = 0..39 (`0x1e838`
draws the left 8 px of a shape, plain copy): top row (y 0) corners 54 (left) / 55 (right), else `58 + rnd mod 3`; bottom
row (y 192) corners 57 / 56, else `61 + (rnd and 1)`; while `i` < 23 the side pieces of row `i + 1` (y `8·(i + 1)`):
left `63 + rnd mod 3`, right `66 + rnd mod 3`. Remaster: pack section `scroll_frame`, pieces `scroll/<slot>.png`; the
Client picks the random pieces once per reading.

### 2.3 Dialog buttons `[C]` (s102: YES / NO never drawn)

`0x8012` cuts `dlgbttns.cmp` into slots 1–10: button `i` (OK, CANCEL, MORE, YES, NO) at (64 i, 0) = slot 2i + 1 and
(64 i, 19) = slot 2i + 2, 64×18 (yellow / cyan lettering). The second is shown while the button is held `[C]` (s102:
the mouse test `0x15ea` lights slot + 1, then `0x1684` blits it). Windows are 52-B structs (25 of them from DATA
`0x23228`: +0/+2 colours, +6..+12 the box, +14..+22 title position / colours / text, +26 button count, +28.. button
slots, +34.. button positions (x in bytes, y), +46.. one key per button: `O` 79, `C` 67, `M` 77, −1 / 0 none), drawn by
`0x3904` / `0x3b0c` (buttons) and answered by `0x3ba0` (Return / `o` / `O` = the first button, 1; `c` / `C` = CANCEL, 0;
a click on a button = that button).

**YES / NO (slots 7–10) are cut but never drawn** `[C]` static: no window in the table has a button slot other than
1, 3, 5; the only immediate 7 / 9 writes are an input IORequest field (`0xf88`) and window 23's colour (`0x236d6`); every
other read of the shape table (`0x24ca8`) is the body figure, monster sprites, parchment or dungeon glyphs. The Amiga's
yes / no questions use **OK / CANCEL**: the dungeon "Keep <item>?" (event op 6, `0x1cb26`) is window 1 (box
(0, 72)–(319, 140), OK at x 72, CANCEL at x 192, both y 115, the question as the title at text (1, 10) colour 9; seen
s125, §8.7, in the dungeon palette); the
fight find (`0x1a044`) is window 23 (box (16, 64)–(300, 128), button count forced to 2 at `0x1a048`: OK at x 72, CANCEL
at x 176, y 104; the finder's name at text (4, 9), "finds" and the item after it, "[original text]" at (6, 11),
colour 9). Extracted to `buttons/<name>-<0|1>.png`, pack pictures `button.<name>[.chosen]`.

Window drawing `[C]` (s104, `0x3904`): box filled (`0x648`, inclusive) in struct +4 (0 for both); frame `0x31e6` in +0 =
4 (outer: top row y0 and left column x0, to x1 − 1 / y1 − 1) and +2 = 3 (inner, one pixel in and down, to x1 / y1); the
title (+14 x cell, +16 y cell, +18 / +20 colours, +22 text) through `0x854`; the buttons (`0x3b0c`) last. The palette is
`0x24010` (the map palette, set once if not yet in force). Remaster (user s103): pack section `keep_windows`; III's keep
questions draw these windows instead of DOS's Yes / No bar, keys Return / O = OK, C = CANCEL, plus the manual's Y / N and
DOS's Esc = No.

### 2.4 The flash: sparkles 37–39 `[C]` (s108, static)

`0x1b814(x, y, right)` is a three-frame sparkle: `x` (in bytes, 8 px) += `rnd mod 3` (+2 when `right`), capped at 36;
`y` += `rnd mod 22` + 6; the 32×24 rectangle there (x rounded down to a 16-px word) is saved to the back buffer
(`0x2e60`), then shapes **37, 38, 39** are blitted masked (`0x2ce4`, colour 0 transparent) one after another at
(x·8, y), each followed by a wait of `0x241e8` vertical blanks (`0x2af6`: the VBL server `0xdf8` counts `0x241a8`,
`AddIntServer(5)`), with sound 15 (`0xb126`) on the second frame; the rectangle is restored. Callers: the gods' reward
scene `0x1f30a` (50 flashes at x `rnd mod 36`, y `rnd mod 120`; the wait word `0x241e8` = 7, −1 every 5 flashes down
to 1 while the noise grows, +1 every 5 up to 8 while it fades, §3.1); combat, on a party member's card
(`0x17f1e`: x = max(0, 6·slot − 1), y 140, `right`) and on a monster (`0x18372`: x = the group's word `+60 + 2i` of
monster i, y = `+70`, `right` = the giant flag `+72`) — the spell effect (§2.4.1).

#### 2.4.1 The flash in combat `[C]` (s110, static)

- **When.** `damage_monster` `0x1833e(group, damage, i, …, flash)` calls the sparkle on monster i when its argument at
  `+18` is set: the party's area spells with a resist roll (`0x181b8`; spells 33–36 roll damage from table `0x2271a`,
  the others set the monster's status word `+24 + 2i` to 2 with 0 damage), `0x182c6` and `0x186f6` pass 1; the party's melee (`0x16230`) passes 0
  and prints "<name> hits!" with sound 15 (`0x1662c`) instead. `damage_character` `0x17e16(damage, slot, flash, …)` calls
  it on the member's portrait when `+12` is set: the monsters' Fireflash (`0x1673e`), breath (`0x176c2`), Mindblast /
  Fireball (`0x17c80`), Sleep (`0x17bc2`, 0 damage, then status 2), lowered armour / attack (`0x17d56`, 0 damage, then
  the value × % / 100) and the stunning / draining touches (`0x177c8` types 34 / 35); a plain monster swing (`0x17aac`)
  passes 0 and prints "<monster> hits!" with sound 15 (`0x17eda`). Each flash comes inside the damage routine, before its
  "takes n damage!" line. **Seen s120** (FS-UAE, §8.6) for a party spell: the cast line, then per monster hit its flash, its
  "takes n damage!" line and its "dies!" line, then the next monster's flash.
- **Speed.** The wait word `0x241e8` is 6 outside the reward scene: set at boot (`0x7656`) and set back at the scene's
  end (`0x1f13a`). So a combat flash is 3 frames of 7 VBLs, 21 VBLs, sound 15 at VBL 7. Measured s120 on the PAL A500:
  each frame 7 frames of a 50 fps capture (0.14 s), 0.42 s a flash `[C]` (PAL); 0.35 s on a 60 Hz machine `[~]`.
- **Where.** A member: x = max(0, 6·slot − 1) + rnd mod 3 + 2 bytes (at most 36), y = 140 + rnd mod 22 + 6; its portrait
  is at (48·slot, 143 / 138 / 133) for record `+8` = Back 0 / Middle 1 / Front 2 (`0x1b42e`; 123 for 3, the reward
  scene's). A monster: its picture's x (`0x1b150`: normal groups from byte 3 + 7·((5 − n) / 2) (+3 for an even n), 7
  bytes apart; giants 6 or 15, 18 apart) + rnd mod 3 (+2 for a giant), y = the group's y + rnd mod 22 + 6.
- **Palette.** The combat screen loads heros.cmp's palette (`0x19906`, §6): registers 12–15 are red `#DB0000`, orange
  `#DB9200`, yellow `#FFDB49` and white.

Remaster (s110): `flash/combat/37.png` … `39.png` (combat palette), pack `combat_flash` (`CombatFlashLayout`); Core notes
every hit of a round in order (`CombatResult.Hits`: `DamageSource`, the target, a touch flag; recorded in the damage
steps, III's Sleep and lowered-armour / attack too, no random number drawn); the Client (`CombatFlashRun`) flashes a
monster hit by a party spell (`PartySpell`) and a member hit by a monster special or touch, one after another after the
round's lines, and the next round waits for them; sound 15 with each (the Sound switch, D3-066). `[~]`: the remaster's
combat screen is its own (cards and a rank list, not the Amiga's screen), so a member's flash sits at the Amiga's offset
from its portrait and a monster's at the offset from its rank's one picture (the cap at byte 36 is not applied there); a
hit monster whose rank is wiped by the round is no longer listed, and the fight's last round leaves the screen at once,
so those flashes are not seen (their sound plays). **s125:** the party's Sleep (Amiga spell 42) goes through `0x181b8`'s
0-damage branch: status 2, then `damage_monster` with 0 damage and the flash flag; the "takes 0 damage!" line is built
but printed only for damage > 0 (`0x18482`), so each sleeper gets a flash and no line. Core now notes a `PartySpell` hit
for each monster its Sleep puts to sleep (`SpellCaster`, both rule paths), before that monster's "is slept" line (the
remaster keeps that DOS line). **s121:** with AMIGA SCREENS on, the combat field (§8.1) plays each
flash mid-round, just before its hit's first line (Core notes the line, `CombatHit.Line`; `CombatFieldRun.Flashes`), the
sound with it, so a killed monster's flash shows before its "dies!" line removes it; the remaster's own screen (all
lines at once) keeps the run after the lines. **s128:** the giant's +2 bytes follow the screen: on the Amiga field
the record's flag (`UiOverlays.FieldGiant`; Wolf / Mud Elemental shift though their borrowed pictures are 32 px wide,
as the Amiga places them), on the remaster's rank list the picture drawn there (`UiOverlays.FlashGiant`: only a
giant-size picture, wider than 32 px, shifts), so a Wolf's flash lands on its 32 × 48 picture, not beside it. A rank
with no picture at all (the 12 × 12 box; no III monster since D3-067) is still smaller than the 32 × 24 sparkle. Watched on Xvfb (`parity/shots/p3-remaster-field-flash-midround.png`:
Mindblast II, cast line → bee flash → "takes 4" → "dies" → next bee). s114: watched in the running Client (Xvfb): a Fireflash I on a
monster rank that outlives the round draws the three frames over the rank's picture, sound with it. (s125: the
`0x181b8` 0-damage branch, the party's Sleep, is noted as hits now, see above.) s117: sound 15 on the melee "hits!" lines and 18 on "misses" are wired on the combat field (§8.1).
**s163, a monster's Sleep on the party, seen live** (FS-UAE, `../working/capture_s163_p3_dungeon.md`): `0x17bc2` rolls each
member (chance 5 · (the caster's word `+0x0e + 2i` − the member's `+0x1e`) + 50; dead / out members skipped); a member
the roll takes gets `damage_character(0, slot, flash 1)` and status 2. Filmed: the red "Skeleton casts Sleep" with sound
21, the line cleared, then one flash per member taken, slots 0 to 5 in turn (three frames 7 VBLs apart, sound 15 on the
second), and **no line after any flash**. A monster casts only while its word `+0x0e + 2i` is at least the spell's tier
(id mod 4, 2 for ids from 33 up), which byte 24 = 255 left at −1: why s162's sleep disk never cast. The remaster, watched
on Xvfb (`:157`, III test party, `--p1-combat 72`, the Guagle Beast, every member parrying): "GUAGLE BEAST CASTS SLEEP",
then a flash on each member slept, one after another, and no line after them
(`parity/shots/p3-remaster-s163-monster-sleep-cast-line.png`, `-flash.png`), as the Amiga.

**Palette in the reward intro `[C]` (s109):** `0x1ee82` sets no palette before the flashes, so the one in force is what the
event fight `0x1da5c` left: after the fight it sets black (`0x21a88`), clears, draws the dungeon map and sets `0x244de`
(`0x1db8e`), the dungeon palette (= `dng.csh`'s, copied at `0x74fa`). The keep questions after it (`0x19cac`, `0x19f72`)
reach the window routine `0x3904`, which sets the map palette `0x24010` only while the flag `0x24070` is 0; every fight
round sets it to 1 (`0x198a2` at `0x19914`, called before `0x154ec`), nothing in the fight or in `0x1da5c` clears it, so
the dungeon palette stays. The sparkles use registers 12–15 only (grey, dark grey, mid grey, white there; in the map
palette 12–13 would be blue and green). Wait: `0x2af6(w)` loops until the VBL counter passes start + w, i.e. **w + 1
VBLs** a frame; the rising run is 3 · (5 · (7 + 6 + 5 + 4 + 3) + 25 · 2) = 525 VBLs, the fading one
3 · (5 · (3 + 4 + 5 + 6 + 7 + 8) + 20 · 9) = 1035 (8.75 s and 17.25 s at 60 Hz `[~]`). Sound 15 (`0xb126`) is not a
sample: it starts three voice streams from table `0x23e1a` (entries 15–17) on the music driver `0x1f52` (§7.2), before
frame 1 is drawn (after frame 0's wait). It lasts 45 VBLs, so at the rising run's speeds every flash cuts the last
one's noise short (a start resets all three voices).

Remaster (s109): extract `flash/37.png` … `39.png` (dungeon palette, colour 0 transparent), pack `reward_scene.flash`
(`RewardFlashLayout`: the positions, the speed word and the timing as above); the Client draws them on the reward
scene's intro pages (`RewardSceneClock`), each page moving on when its run ends (Return skips). s110: sound 15
(`sound` / `sound_frame` 1, `RewardFlashLayout.SoundVbls`): a run's 50 sounds as one driver run (`P3MusicScore.Run`),
started with the page and stopped when the page changes; silent with the Sound switch off (D3-066).

## 3. Full screens `[C]` unless marked

| File | Picture | Where |
|---|---|---|
| `Graphics/title.cmp` | title "The Wrath of Nikademus" | `0x73fe` |
| `D/w4.cmp` | town street (every town) | loaded at start `0x7776`. Town name (s102, `0x7e54`): 12 spaces at text cell (14, 2), then the name (table `0x22f14`, mixed case "Pendragon") at cell 14 + (12 − len)/2, row 2 (`0x854`: x = col·8, baseline row·8 + 6, so the text top is y 16); BackPen 4 (the scroll's tan); FrontPen **2** (`#924924`) in towns 1–15, **0** in 16–17 (`0x24152` = 0 / 1 / 2 / 4 for towns < 15 / 15 / 16 / 17+, `0xb17e`). Towns 16–17 fade (`0x4390` is the palette fade, as in §3.1) to `0x23050`, a dim palette (words 000 222 333 444 333 432 321 210 531 430 222 123 102 100 222 444), instead of `0x24010` (= `w4`'s own colours) when the street is drawn (`0x78ee`, and after File › Load `0xb228`, s162) `[C]` s108. Cell centring puts an odd-gap name 4 px left of the exact centre `[C]`. Built (s108): pack `town_screen` `name_cells` / `name_cell` (`TownScreenLayout.NameShift`), `dim_towns` [16, 17] with `palettes.town_dim` and the name in black (`AssetCatalog.TownBackdropFor` recolours the street and the doors). No message line on this screen (the picture runs to row 199). |
| `D/w3.cmp` | five 48×48 **open-door pieces** of the town street (s102 `[C]`): Bank at (16, 141), Mystic (64, 141), Armory (160, 143), Inn (208, 139), Guild (272, 141); the rest of the screen is colour 0 | loaded with `w4` on entering a town (`0x7776` / `0x7788`) and unpacked to the back buffer; on going into a building `0xacc8(door)` saves the 48×48 rect of the street (table `0x23070`: x in 16-px words, y), copies the open door from `w3` over it, waits 15 ticks and restores the street. Door numbers from the callers: 0 bank (`0x11812`), 1 Mystic (`0x12072`), 2 armory (`0x10fa2`), 3 inn (`0x11bc2`), 4 the Guild and its screens (`0x12bfc`, `0x12422`, `0x127ce`). Remaster (s102): pack `town_screen.doors` + `town.doors`; the Client shows the door for 0.3 s (15 ticks at 1/50 s `[~]`) before going in |
| `D/w2.cmp` | a temple with a red demon: the gates of the Astral Plane | party defeat `0x1dc16`: "[original text]…" / "…arrived at the gates…", then the picture (X of `D/WX.cmp`, string 35, set to `'2'` at `0x1dc70`); drawn behind III's defeat outcome; screen layout in §6.1 |
| `D/w1.cmp` | Zeus on clouds (art rows 0–120) | reward scene `0x1ee82(outcome)`: outcome 2 (Nikademus killed, good) → `'1'` (§3.1); DOS ovl4 `0x0000`, `endgame.md` §3 |
| `D/w0.cmp` | demon (Pluto) on a throne in lava (art rows 0–120) | the same, outcome 3 (Lord Wood killed, evil) → `'0'`; called from the event fight `0x1da5c` when the fight returns 2 / 3 |
| `D/s6` | **Scroll IV**: the map "[original text]" | `0x1f7ee` loads and unpacks `D/s6` when the scroll has no text; the other `D/s<n>` are 1152-B texts |
| `Graphics/dlgbttns.cmp` | OK / CANCEL / MORE / YES / NO buttons (two states) | cut by `0x8012`: 5 × 2 buttons of 64×18 |

### 3.1 The gods' reward scene (root `0x1ee82`) `[C]` static s102

The outcome becomes 1 (good, from 2) or 0 (evil); `X` of `D/WX.cmp` = `'0'` + it. On the cleared screen: "You hear
noises and see flashing lights" at text (0, 11) colour 15, then `0x1f30a(1)`: 50 random flashes (`0x1b814` at x
`rnd mod 36`, y `rnd mod 120`) while the speed word `0x241e8` (set to 7) counts down every 5; "When the noises and lights
have reached / such intensity ... / begin to fade." at (0, 9), `0x1f30a(0)` (the speed counts up to 8). The picture is
loaded, the palette faded to black (`0x4390(0x21a88)`) and the picture unpacked; the party's race bytes go to `0x24270`
(slot 6 = −1: no elemental) and every record's combat-row word (`+8`) := 3, so `0x1b6be(−1)` draws the portraits at
x = 48·slot, y = 123 (`0x1b42e`); "Press a key" (string 18) at (14, 23) colour 15; **`D/M1` starts** (`0x1fa5c`, §7);
the palette fades in to the picture's own (`0x4390(0x2437e)`) and a key is awaited. Then the speech page (§2.2) in the
dungeon palette, a key; back to the picture and its palette; per member (not the elemental): `0x1f4fa(m, 1)` (heal),
score 250 / 200 (a clamp to `base ... base + 49` computed and stored to `+0x82`), social class 5 / 4 (`+0x8e`), row word
2; then `0xfb8c`, the music stopped (`0x1f52(0, 0, 0)`), town 1 (`0x2411c` := 3), fade to black. The DOS text is the
same (`endgame.md` §3; the Amiga's row is 2, DOS's rank Front: rules stay DOS). Remaster (s102): Core `RewardScene`
(`GameSession.PendingRewardScene`, prompt `RewardScene` after the fight's own keep questions); Client
`GodsRewardScene`: intro lines on black, the whole picture with the party's portraits and "Press a key", then the speech
on the parchment; Return pages. s109: the flashes (§2.4) drawn on the intro pages (no key read there on the Amiga: the pages move on when
the runs end, Return skips), and `D/M1` played from the picture page to the scene's end (pack `reward_scene.tune`;
`0x1fa5c` / `0x1f52` test no sound switch, so it plays whatever the settings; silent with no sound device). It does not
loop: 3073 VBLs, 51.2 s at 60 Hz.

### Title: the boot sequence (s102) `[C]`

`S/startup-sequence` runs `PhantasieIII`. Its start routine `0x736c`: allocate the buffers; `0x123c` + `0x1208` load and
unpack `Graphics/Title.cmp` into the shown screen (`0x24188`); `0x7efe` keeps four of its colours; `0x1fa5c("D/M0", 830)`
loads the title tune and starts it (`0x1f52`, interrupt-driven, it does not wait; §7.1, played by the remaster s108);
`0x11ae` sets the title palette. Then it loads the rest while the title shows: Anatomy1/2.csh, Dng.csh, Scroll.csh,
Dlgbttns.cmp, Phantasy.set, the seven sprite sheets (`0x7f3c`: heros, purple1/2, yellow1/2, giant1/2 — sprite sheets,
not intro pictures), monsinfo.dat and inititem.dat (`0x1f99a`, `0x1f9ee`), and at `0x7660` sets palette `0x21a88` and
clears the screen (`0x3130`). No key is read: the title stays only as long as the loading: **about 20 s** on FS-UAE (A500, Kickstart 1.3,
PAL and NTSC alike; title shown → Initial Menu, s156 `[C] live`, emulated floppy timing `[~]` for real drives;
`reverse-engineering/amiga/working/capture_s156_p3_title.md`). The manual says
"Hit any key to get to the Initial Menu" (D3-065, decided s103); the remaster's title splash waits for a key, and (s157)
the tune plays on after it (§7.1). The "Amiga
Version 1.0 by Westwood" text (`0x571a`) is the About box, not the title.

## 4. Overworld: `Graphics/phantasy.set` `[C]`

Read raw (not packed) by `0x27d8`: 3 bytes (`0xfa`, rows = 75, columns = 50; kept in `0x24096` / `0x24098`), a 32-B
palette, the 50×75 Scandor map (3750 B), then **250 tiles of 16×16** (32000 B; 128 B each = 16 rows × 4 interleaved
plane words). The 9×9 overworld view (`0x9b92`) draws map byte `n` as tile `n` (`0x3834`, ids above 250 clamped) at
16-px steps from y = 12; the party's cell gets tile **89** OR-ed over the cell's tile (`0x378a`: mask = OR of the four
planes, so colour 0 is transparent).

**View at the edges `[C]` (s102, static).** `0x240ac` / `0x240ae` = the map cell drawn in the view's centre (x, y),
`0x240b2` / `0x240b4` = the party's cell in the view (0..8), `0x240c4` / `0x240c6` = the party's map cell; map size
`0x24098` columns / `0x24096` rows. The view draws cell `(cy + r − 4)·cols + cx + c − 4` with no bounds check
(`0x9b9e`). A step north (`0x985e`): if `cy` ≤ 4, or `cy` ≥ rows − 4 with the party below the centre, only the party
moves in the view; else the view scrolls (`cy` − 1); south (`0x9912`) mirrors it (the party moves while `cy` ≤ 4 and it
is above the centre, or `cy` ≥ rows − 4); the columns (`0x99e2...0x9b38`) are the same with `cols`. So the centre stays
in 4 ... size − 4: at the top / left the view starts at row / column 0, at the bottom / right it ends at row `rows` /
column `cols`. In play Scandor's counters are 50 × **74** (the boot's plane change, below), so its view ends at columns
42–50 (**one column past the map**) and rows 66–74 (the map's last row), seen s125 (§8.7). The step test `0x9c6a`
treats row 0, column 0, row `rows` and column `cols` as the edge (type 4, number 12 Scandor / 15 a plane = blocked):
on Scandor row 74 (the map's last, as DOS) and **column 50** (one past the map, so column 49 can be entered; DOS
blocks x = width − 1 = 49, `planes.md` §4). A click on an edge cell does nothing (s125: no step, no message).

**Past the map `[C]` (s108, static).** The view reads byte `y · cols + x` of the map buffer unchecked, so a cell one
column past the east edge is **the next row's first cell**. Scandor's buffer `0x241f4` is a 3750-B block of the bump
allocator `0x5cc` (sizes rounded up to 4, one `AllocMem(123000, MEMF_CHIP)` pool `0x11c`, not cleared), followed by
the tile buffer `0x2419c`: row 75 is 2 pad bytes (undefined) then tile 0's pixel bytes (`FF FF FF FF 00 00 FF FF`…),
and the tile draw `0x3834` clamps ids above 250 to **250**, a tile past the 250 read (undefined memory) — so row 75
(and (50, 74)) is undefined art. With the rows counter at 74 the view never reaches row 75; (50, 74), the view's
bottom-right corner at the south-east edge, is drawn black by the remaster.

**Planes and Netherworld `[C]` (s108).** On a plane change (`0x1eb32`): Light / Dark (`0x24152 & 3`) set the counters
to columns 9, rows **8**, centre (4, 4), party (4, 1) and load `D/pln<n>` (81 B); every column step subtracts 1 from
the column count first and adds it back after (`0x9a96` / `0x9b6c`), so the centre stays at (4, 4): the 9×9 view is
always the whole plane. The Netherworld (bit 2) sets columns **31**, rows **15**, centre (7, 4), party (7, 3), loads
496 B (31 × 16) into `0x241ec` and writes **0xF9 to bytes 496 and 497** (`0x1ec62`): its centre column reaches 27, so the
view runs one column past the east edge (that column is the next row's first cell; the last row's is byte 496, tile
249), while its rows stay on the map (centre row ≤ 11 → rows 7–15). Back on Scandor the code sets columns 50 and rows
**74** (`0x1ec86`), not the 75 the file load gave (`0x27d8`). **The boot runs this too** `[C]` (s125, static and seen):
after loading `phantasy.set` (`0x7550`, rows 75) it sets the previous plane `0x24154` = 1 (`0x76ce`), the plane
`0x24152` = 0 (`0x76fa`) and calls `0x1eb18` (`0x7758`) with no branch around it, and no other writer of `0x24154`
runs between, so rows = 74 before the first step of every game, new or continued, not only after a plane visit
(s108 read it as plane-only). Seen on FS-UAE (§8.7): at row 73 with the view on rows 66–74 a step north moves the
party up in the view without scrolling (rows 75 would scroll) and a step south to row 74 is refused. The saved game
does not hold the plane (`gamesave.dat` = `0x26a04` 6550 B, `0x2559a` 1968 B, the 36-B block `0x24564` copied from
`0x240ac…0x240f6` at `0x1fb2a`, `0x25d4a` 3258 B; block words: centre x / y, view x / y, party x / y, `0x240ca`,
`0x240cc`, `0x240d6` in town, …).
Light / Dark: the column count is cut to 8 only inside a column step (`0x9a96`), so the party may reach column 8
and the step test, with 9 back, never fires there: the east column is open on the planes too, as on Scandor (50) and
the Netherworld (31: column 30 open). Rows: 8 / 15 / 74 = each map's last row, as DOS. So **the Amiga's east edge is
one column further out than DOS's** (DOS blocks x = width − 1 on every map, `planes.md` §4: ovl3 `0xa5f…0xa6c`
compares x with the column count **− 1**, the Amiga's `0x9c92` with the count itself; the rows test is the same in
both; DOS sets the same counters: Scandor 50 × 74, the Netherworld 31 × 15, Light / Dark 9 × 8, `MAIN.EXE` file
offsets `0x152b…0x1672`, and 74 rows already at start-up, `0x1c472`) `[C]` static (Scandor seen s125); a DOS-vs-Amiga numbers difference, recorded, not built (the remaster plays DOS's edge, Core
`Phantasie3Campaign.MoveOverworld`).
Remaster: `Game1.OverworldViewMaxOrigin` (the view's last row / column = the pack's `tile_maps.view_counters`:
Scandor 50 × 74, the Netherworld 31 × 15, s125) and `Game1.PastMapTile` (the next row's first cell; the pack's
`tile_maps.past_map` for the Netherworld's 0xF9; else black).

- **Planes** (`D/pln1..4` = DOS `PLNS.DAT`: Light, Dark 9×9, Netherworld 31×16) use the same tiles `[C]` (previews
  render cleanly; `pln1` is byte-identical to the start of `PLNS.DAT`).
- **vs DOS:** the Amiga map differs from DOS `WORLD.DAT` in 54 cells (list in `extraction-index.json`). By the terrain
  table (`GameData/phantasie3/overworld.json`) 41 keep their terrain (shore / edge variants) and **13 change it**
  (x, y: DOS → Amiga): (3,21) Meadows → Desert, (32,22) Mountains → Meadows, (24,37) tile 29 (no name) → Water,
  (46,40) Meadows → Forest, (2,46) Hills → Water, (25,51) and (25,53) Meadows → Forest, (7..10,53) Forest → Meadows,
  (45,73) River Styx (blocked) → Forest, (49,74) River Styx → Water. The remaster plays the DOS grid and draws it with
  Amiga tiles (as I, s53), so these are rules differences between the versions only, not drawn ones `[C]` data.
- The palette equals `anatomy1.csh`'s, which is the one the overworld loads (`0x24010`, saved at `0x74a6`).

## 5. Dungeon map `[C]`

`0x1e47a` draws all 24×30 cells; each is an 8×5 glyph at (8·x, 5·y + 17) (`0x1e518`, blit `0x1e838` copies only the
first byte of each plane word, so a glyph is the left 8 pixels of its `dng.csh` shape). A cell not yet seen (bit
`x` of row `y` in the 90-B bitmap `0x245d8` = `D/DNG<letter>`, read at `0x1bc54`) is glyph 46; a seen cell's glyph
comes from its map byte (`0x1e750`); out-of-range glyphs become 51; the party is glyph 51.

| Map byte | Glyph (slot) |
|---|---|
| 0, 1 | 46 (stone) |
| 185–199 | 47 (door, horizontal) |
| 200–214 | 48 (door, vertical) |
| 215–217 | 52 |
| ≥ 221 | 49 (green cross) |
| any other | 50 (floor) |
| party | 51 (red cross) |

Glyph 45 and 53 are loaded but not drawn by this routine. Dungeon palette: `dng.csh`'s (`0x244de`, saved at `0x74ee`).
`D/dng<a..k>.dat` (4352 B) are the factory dungeon records: their first 4224 B equal DOS `DNGS.DAT` records 0–10, the
rest is zero. `D/DNP<A..K>` are the played copies, `D/DNG<A..K>` (90 B) the seen bitmaps.

## 6. Encounter and party pictures `[C]`

`0x7f3c` loads seven packed sheets (pointer table DATA `0x220a6`): **0 heros, 1 purple1, 2 yellow1, 3 purple2,
4 yellow2, 5 giant1, 6 giant2**, and keeps the palettes of heros / purple1 / yellow1. The combat screen (`0x198a2`)
draws both groups and then loads the **heros palette** (`0x19906`), so every encounter picture shows in it (the sheets'
own palettes differ in a few registers and are not shown).

**Monsters** (`0x1b4c8`): the picture id is **monster record byte 27** (`0x1b01a` copies it to the group's `+50`).

| Byte 27 | Sheet, picture `p` |
|---|---|
| < 15 | yellow1, `p` = byte |
| 15–27 | purple1, `p` = byte − 15; but 27 (`p` = 12) → heros picture 10 |
| 28–35 | purple2, byte − 28 |
| 36–41 | yellow2, byte − 36 |
| 42–43 | giant1, byte − 42 |
| 44–47 | giant2, byte − 44 |
| ≥ 48 | no picture is cut (the group's copy is set to 14 and the routine returns): **Mud Elemental (48) and Wolf (65)** |

`0x1b4c8` runs once per fight, from the fight set-up `0x14c62` (`0x14e1e` / `0x14e34`), on group structs rebuilt from
the records each fight (`0x1ae9a`), so the 14 is never used to cut a picture. The group's sprite slots (18–19 / 20–21
of `0x24696`) then keep whatever an earlier fight cut there, and the draw (`0x1b7c4`) shows that stale picture. **Seen
s125** (FS-UAE, §8.8): after a Sniverling fight, "2 Wolf(s)" stood as two Sniverlings (the earlier fight's rank-1
frames) at the giant places (48, 50) and (192, 50); byte 27 > 41 still sets the giant flag. On the **first fight after
boot** the slots are still 0 (`0x24696` lies in the DATA hunk's zeroed part; only the cut `0x2ffe` writes them), so
`0x2c4e` reads the shape header from address 0: rows 1, width 0 → `dbf` from −1 copies 65536 words a row over chip memory
and the field stays black; the game hangs (seen three times, §8.8). The stale picture and the hang are bugs of the original
and are not built. The remaster gives these two **borrowed pictures** (user s126, D3-067, no option): Wolf the Wild Dog's
(byte 0), Mud Elemental the party elemental's (heros picture 22); both are normal-size, and it still places them as giants
(`UiOverlays.FieldGiant`: byte 27 > 41, s125; before, the giant flag came from the picture's height, so they stood at the
normal places and their flash lacked the giant's +2 bytes).

Each picture has two frames, idle and attacking, side by side. Normal pictures are 32×48, 10 per row: frame `f` of
`p` at x = 32·((2p mod 10) + f), y = 48·(2p div 10). Giants (byte 27 > 41; the group's `+72` flag, set at `0x1a928`,
which also drops the second group and caps the count at 2; DOS does the same `[C]` s102, DOS `FORMAT.md` monster byte 27) are 80×72, 4 per row: x = 80·((2p mod 4) + f),
y = 72·(2p div 4).

**Party portraits** (`0x1b2b2`): race byte (record `+0xa8`, party records at `0x2559a`, stride 262) → picture through
the byte table `0x239a0`: Human 0, Dwarf 2, Elf 4, Gnome 6, Halfling 6, (Random 0), Gnoll 14, Goblin 16, Kobold 16,
Lizard Man 10, Minotaur 12, Ogre 14, Orc 16, Pixie 8, Sprite 8, Troll 20, Elemental 22, Undead 18 (entry 18 = 24, the
grave). Same 32×48 cells and frames on heros.cmp. `0x1b2b2` reads seven race bytes (`0x24270`, one per combat slot):
the fight set-up fills them from the records (`0x14d46`) and sets slot 7's to **16** (`0x14fb6`) when an elemental
fights, so the elemental shows **picture 22** `[C]`. The portraits are drawn by the combat screen and two text scenes
(`0x1ddcc`, `0x1efb0`); the roster outside combat shows the body figure (§2.1), not the portrait.
The extract files them by the **remaster's race id** (Core `Race`, I's order, through `rule-tables.json` `core_race`;
s97 used III's order, which put e.g. the Human picture on Pixies): `portraits/<kind>/<NN>-<race>.png`, the elemental as
`portraits/<kind>/elemental.png` (pack `portrait.elemental.idle`). The remaster's party cards are 32×32: a 32×48 portrait
is drawn 1:1 and cut to 32 rows from its first row of art (head to hips), never squeezed (`UiOverlays.DrawPortrait`).

### 6.1 The party-defeat screen (root `0x1dc16`) `[C]` static s98

Text goes through `0x854(text, column, row, colour, 0)` on 8-pixel text rows. On the cleared screen: "  The souls of your
defeated warriors\r [original text]" at row 12 (two lines), 120 ticks; `D/W2.cmp` is loaded, "
[original text]\r[original text]" at row 12, 120 ticks; the picture is
unpacked to the screen (art on rows 0–140). The party's race bytes go into `0x24270` (slot 7 = −1), `0x1b2b2` cuts the
sprites, sound 45, "[original text]" at row 24 (y 192), 100 ticks. Per member: sound (48 destroyed /
9 otherwise), the name at column 0 row 24 (colour 15; 14 for undead), the rest of the row cleared (`0x648`), the fate
text (strings 21 destroyed / 22 resurrected / 23 undead at `0x23a62`) after the name, then `0x1b6be(−1)` redraws the
portraits: 32×48 sprites at x = 48·slot, y = `0x24346[slot]`, which `0x1b42e` fills from each record's combat-row word
(`+8`): 0 → 143 (the defeat loop clears the word first), 1 → 138, 2 → 133, 3 → 123. A destroyed member's byte becomes 22
→ sprite 16 = the grave (heros.cmp x 128, y 96; extract `portraits/grave.png`, pack `portrait.grave`); an undead one
shows race 17 (the Undead portrait). 140 ticks between members. The line's colour (`[bp−4]`) is 15, or **14 for an
undead fate** (`0x1df0a`), for the name and the fate text alike, in `D/W2.cmp`'s palette (14 = `0x762`, yellow; pack
`palettes.astral`); built s102 (`AstralScene.LineColour`). The remaster draws this as `AstralScene` (Client), with
Return paging the lines instead of the waits.

## 7. Not decoded / open

- `D/m0`, `D/m1` **are music** `[C]` (s102): `0x1fa5c(name, length)` reads `length` bytes into `0x241ec`, splits them
  at the 0xFF bytes into **three voice streams** and starts them with `0x1f52(v1, v2, v3)` (`0x1f52(0, 0, 0)` stops).
  `D/M0` (830 B used: 298 + 324 + 205) is played at start (`0x7426`), `D/M1` (659 B: 252 + 201 + 203) in the gods'
  reward scene (§3.1). Stream format and driver decoded s108, §7.1; the title splash plays `D/M0`, the reward scene `D/M1` (s109).
- The flash's palette in the reward scene's intro: traced s109, the dungeon palette `0x244de` `[C]` (§2.4); the planes' view: decoded s108 (§4).
- The Amiga font (`phantasy.set` holds no font): text uses the **system font**, topaz 8 from the Kickstart ROM (seen s116,
  §8: the serif face of the town name and every window). Not on the game disk, so the remaster keeps its own font.

### 7.1 The music driver and `D/m0` (s108) `[C]` static

Decoder `OpenPhantasie.Data.Audio.P3MusicScore` (the driver run frame by frame, giving Paula register writes), playback
`OpenPhantasie.Client.Audio.P3MusicRenderer` + `PaulaMixer`; tests `TitleTuneDecoderTests`, `TitleTuneTests`.

- **Set-up `0x1e6c`** (called once at boot, `0xc0`): three 64-byte waveform buffers, each an 8-byte table repeated 8
  times: `0x24336` ← `0x21f8e` `0 53 127 73 0 −53 −127 −73` (sine-like), `0x2433a` ← `0x21f7e` `127 ×4, −127 ×4`
  (square), `0x2433e` ← `0x21f86` `0 42 85 127 −127 −85 −42 0` (triangle); `0x24342` = 4000 bytes of noise: 1000 longs
  of the LCG `0x1c24`, seed = (seed · 25173 + 13849) mod 65536, seed `0x241ac` in BSS = 0 at boot (bytes `00 00 hi lo`).
- **Start `0x1f52(v1, v2, v3)`** (all 0 = stop: `DMACON` = 15 off, pointers cleared): per voice i (Paula channel i, 0–2)
  whose pointer is not −1: waveform 8 (triangle), tempo factor 1, loop counters, note counter, envelope phase, vibrato
  and rates 0, length 32 words; `DMACON` clears the channel's bit; level ceiling `0x240a6` = `0x3F00`; then row
  `0x240a4` = 15 and "playing" = 1.
- **Clock:** `0xd4a` adds an interrupt server "VertB-Timer" (`AddIntServer(5)`, `INTB_VERTB`) whose code `0xdf8` calls
  `0x2192` **once a VBL**: 60 Hz NTSC or 50 Hz PAL; the US release suggests NTSC `[~]` (pack `vbl_hz` 60).
- **Per frame `0x2192`**, voices 0, 1, 2 (`0x21e4`), then the frame counter `0x240aa` = (n + 1) mod 4:
  - Pointer 0 (ended): clear the channel's DMA bit.
  - Note counter ≠ 0: count down; reaching 0 starts the release (phase 3) unless the note is tied. Step the envelope.
    With vibrato on, write `AUDxPER` = p − p/128 on counter value 1, p + p/128 on 3, else p (p = the note's period,
    `asr #7`).
  - Counter 0: read a byte. **Below `0x80` = a note** (0 = rest), the next byte its length L (bit 7 = tie); with tempo
    factor t ≠ 1, L = (L + 1)·t − 1; counter = L, so the note holds **(L + 1)·t frames**. Phase 0 (attack). A rest
    goes to release (phase 3) and steps the envelope. A note: period = table `0x21ed0`[note − 9] (87 words,
    `16256 … 124`; `856` = the Amiga's C), level 0, `AUDxVOL` 0, `AUDxLC` / `AUDxLEN` = the voice's waveform, `AUDxPER`,
    one envelope step, then `DMACON` on only if the channel's DMA is off (`DMACONR` test) — a running channel takes
    the new waveform when its buffer wraps.
  - **Commands** (`0x2436`): `C2 w` waveform (`10` sine, `20` triangle, `40` square, else noise, length 32 words;
    noise 2000); `C4 ad sr` envelope: attack = `0x21ab0`[row][a], decay = `0x21cb0`[row][d], release =
    `0x21cb0`[row][r], sustain = `0x21eb0`[s]; `C7 r` row r and ceiling `0x21eb0`[r]; `CD n` / `CE hi lo` loop 1:
    count, then back hi·256 + lo bytes while the count (decremented) is not 0; `D1` / `D2` loop 2 the same; `E2 t`
    tempo factor of **all three** voices, `E8 k` / `E9 k` minus / plus k; `DB x x` and `D3`, `D4` vibrato on, `DC`
    off; `E1` sine; `E5 x` noise; `C3 x x`, `E6 x` skipped; `CA`, `CC`, `E3`, `E4` nothing; `E7` restart every voice from
    its start (ends the frame: the tune loops); `FF` end of the voice. Any other byte falls through to `0x2714`: a
    4-byte click (`0x21f98`) on channel 3, period 2000, volume 64.
  - **Envelope `0x2360`** (level in 1/256 of a Paula volume; `AUDxVOL` = level >> 8): attack adds its rate until the
    ceiling (rate 0 = the ceiling at once and a decay step the same frame); decay subtracts down to the sustain level
    (rate 0 = sustain at once); sustain holds; release subtracts to 0 (rate 0 = 0 at once). Tables: `0x21ab0` and
    `0x21cb0` are 16 rows × 16 rates (row 0 all zero, row r ≈ r/15 of row 15), `0x21eb0` = 1075.2·n (16128 = 63 × 256).
- **`D/m0` (title, 830 B):** voices at 0, 299, 624. Voice 1 starts `E2 07` (tempo 7 for all), `E1`, `C2 20`
  (triangle), `C4 00 B4`, `DB 0C 06`, then `3C 01` = C, 14 frames. 141 + 155 + 98 notes; no `E7` and no unknown byte;
  all three voices read `FF` on frame 2896 and go off on **frame 2897 = 48.28 s at 60 Hz** (57.94 s at 50 Hz). Nothing
  in the boot path stops it: it plays on while the game loads and into the Initial Menu until it ends or another
  sound replaces it (`0xb126` starts the effects through the same driver start `0x1f52`, §7.2). s156 `[C] live`
  (FS-UAE NTSC, recorded): music 29.0–77.3 s after power-on (48.3 s), the title 29.2–49.2 s, so about 28 s of it
  play over the Initial Menu when no key is pressed (PAL: 57.94 s, about 38 s). The remaster plays it from the title
  splash on (D3-065 made the title wait for a key) and, since s157 (user), **lets it play on** after the splash into the
  game until its end or the game's first sound effect (pack `title_sequence.tune.plays_on`, Client
  `Audio.TitleTuneCarry`): what cuts it on the Amiga is the effect's driver start (`0xb126` → `0x1f52`, every voice reset,
  §7.2) `[C]` static; nothing else on the way from the Initial Menu into a new or loaded game stops the driver (the
  load's disk check plays 12 only on a failure). The remaster cuts it at the first driver start, whatever the Sound
  switch says (the Amiga has no switch, D3-066): a sound Core raises (`GameSession.PlaySound`), the reward tune, and
  (s158) a fight round's, a combat flash run's or a reward flash run's first sound **at that sound's own time** (its VBL
  after the run starts, `TitleTuneCarry.CutAtMs`; a round with no sound does not cut it), the astral defeat's 45 / 48 /
  9, and the stop 33 (s158 wired: "[original text]" is tagged with it, "[original text]" raises it; §7.2).
  Entering a fight does not stop the driver on the Amiga (33's two callers are flight lines, not the set-up).
- **`D/m1` (reward, 659 B):** voices at 0, 253, 455; decodes the same way, no `E7` (no loop), 3073 VBLs (51.2 s at
  60 Hz). Played by the reward scene (s109, §3.1).


### 7.2 Sound effects (s110) `[C]` static

The game has no sampled sounds. `0xb126(n)` (n > 0; it ends by clearing the event-sound word `0x240fa`) calls the
driver start `0x1f52(t[n], t[n + 1], t[n + 2])` with the long pointers of table `t` = `0x23e1a` (DATA, relocated), so a
sound is three voice streams in the §7.1 format, and it **replaces whatever the driver plays**, music included (a start
resets every voice). A 0 pointer is not −1, so that voice is set up as ended: its channel stays off. No sound switch is
tested (D3-066). Sounds are the multiples of 3 from 3 to 48; the streams sit at `0x23b54 … 0x23e19` (file offset = flat
address + `0x6038`: DATA starts at flat `0x21890` and file `0x278c8`; no relocation falls inside the streams).

| n | Voices | Callers (static) |
|---|---|---|
| 3 | 3 | `0x19da0`; event op 7 "[original text]" (s135: op 7 sets the message and falls into op 8, which writes 3 to the event word at `0x1cbbc`; the op tail `0x1d1a6` plays the word only when a message is set, so op 8 entered on its own leaves it to the dungeon step's own play at `0x1c246` / `0x1c31c` `[~]`) |
| 6 | 3 | `0x10902` (s136: the guild's spell learning, " It is learned successfully! "), `0x13938` (s138 `[C]` static: **Guild New character**. `0x12bfc` is the Guild's new-character routine (door 4, "Select a Race:" / "Select a Class:", "The character roster is now full."), entered from the Guild at `0xb4a8` with `0x24104` = 0 and from the Phantasie I transfer at `0x20838` with `0x24104` = 1 (that routine's strings follow it: "Insert a Phantasie I Disk now.", "This [original text] disk!"). With `0x24104` = 0 it draws window 3 (`0x232c4`: box (8, 16)-(312, 199), OK and CANCEL at y 174), the rolled character's sheet (`0x13b50`: "Name: … Score:", "Dexterity … Disarm Trap:", "Head:"), waits (`0x3ba0`, keys O / C); OK plays 6, then window 2 (`0x23290`) shows "Unknown" and the name is typed into `0x24970`; CANCEL rolls again. A transfer skips the window and the sound. Built: `GameSession.KeepRolledMember` from the Guild New dialog's Y, `P3Sounds.RollKept`), `0x1f5c8` (s135: `0x1f572(text, slot)` builds "<name><text>" in `0x24632` and plays 6; its four callers are the dungeon step's "[original text]" `0x1c554`, "picks the lock." `0x1c668`, "[original text]" `0x1c6bc`, "[original text]" `0x1c74e`, 8.4) |
| 9 | 3 | `0x1dfe6` (the party-defeat scene: a member not destroyed, 6.1); event op 12 "[original text]" (s135: `0x1cc18`) |
| 12 | 1 | `0x1f93c` ("Insert your Phantasie III disk."; s138 `[C]` static: in the disk check `0x1f87e(mask, mode)`, which opens `D/PLN3` (mode 3 writes the mask word into it, other modes read the word) and in mode 1 loops with 12 and the message while word & mask is 0; every caller passes mask 7: the disk file loader `0x1f5d8` (dungeon `D/DNP` / `D/DNG` loads `0x1bc44` …, scrolls `0x1ebc6`), the save writer `0x1faf0` and the loader `0x1fc7e` with mode 1, `0x20090` / `0x200e2` with mode 0 (no prompt); what the word stands for beyond "this is a Phantasie III disk" `[~]`. **No remaster moment**: the remaster has no disks, so it is not wired), `0x20308` ("Disk Error! A save may have been attempted on a protected disk. Make sure a Phantasie III Disk is in the disk drive. Press a key."; s138 `[C]` static: in `0x20258(file)`, which closes the file, draws the box over screen rows 0-48, plays 12 and waits for a key; callers `0x12b4`, `0x28b8`, `0x1f6b4`, `0x1f7bc`, `0x1f90e` (the disk check's open failing in mode 1), `0x1fa3a`, `0x1fc52` (the save writer `0x1faf0`, when gamesave cannot be opened and its mode is not 0), `0x1fe26`. Built: the remaster plays 12 when its own save fails (`GameSession.NoteSaveFailed`, `P3Sounds.DiskError`), as the box's own text names a save `[~]` equivalence; the other callers (reads, the disk check) have no remaster moment); s136: the dungeon step's **wall bump** writes 12 to the event word (`0x1c4be`: cell 0 with `0x24164` clear, result 2; `0x24164` is set only by the dead Q switch's `#` key, `0x1c038`, 8.5), played with message 2 "That is a wall" by `0x1c28c` (`0x1c31c`) |
| **15** | 3, the same noise burst | the flash's second frame (`0x1b89e`, §2.4), every melee "hits!" (`0x1662c` a member's, `0x17eda` a monster's) |
| 18 | 1 | a melee **miss**: `0x16682` (a member's "misses"), `0x17b28` (a monster's) (s117) |
| 21 | 3 | the party's cast line `0x1620a`; the monster spell lines in `0x1673e` (`0x17184` … `0x17436`): "casts Fireflash / Quickness / Strength / Protection", "[original text] / a Fireball", "casts Sleep" and the lines "[original text]" / "[original text]"; breath and the touches play none (s125: heard with "Giant Bee casts Fireflash", §8.8; built s127: `P3Fight.CastSound`, `CombatLineTag.Sound`) |
| 24 | 2 | `0x14a28` (s136: the overworld's **random encounter** `0x149d4`, called from the main loop `0x7c5a`: with the party on the map and members, `rnd(100)` < 8 → the monster pick `0x1aa40`, then 24, then the night (< 38) / kind / Listen rolls; built: Core `P3Sounds.Encounter`) |
| 27 | 2 | event op 14 "[original text]" (s135: `0x1cc3e` writes 27 to the event word `0x240fa`, played at the op tail `0x1d1b6`) |
| 30 | 1 | **no caller** (s135 `[C]`: every reference to `0xb126` is a `jsr` (none by `bsr`; s140: one `jsr %pc@`, 36 at `0xaaee`), each with a constant, the event word `0x240fa` (written only with 12, 42, 3, 9, 27) or the defeat's `%a5@(-24)`, which is 48 / 9 / 9 at the call: each loop pass sets `%a5@(-8)` to 21 (`0x1de34`), maybe then 22 / 23, and the local's other write, the name length at `0x1e00c`, comes after the call) |
| 33 | none: all three pointers 0 = the driver's stop | `0x15990`, `0x19c66` (s157 `[C]` static: both in the fight's end of round: `0x15990` before "[original text]" (`0x1a676`), `0x19c66` before "[original text]" (the party's flight succeeding; "[original text]" plays nothing). s158 wired (`P3Sounds.Stop`): Core tags "[original text]" with 33 (the field run plays it as the line prints) and raises it with "[original text]"; it silences the running sound and a carried title tune, 7.1) |
| 36 | 2 | `0x111e0` (s136: the Armory's "[original text]"), `0x11386` ("[original text]"), both in `0x10fa2`; s140 `[C]` live: `0xaaee` in `0xaae2(text)`, window 1's confirm question (OK / CANCEL), called by `jsr %pc@(0xb126)` (the one PC-relative call, missed by the s135 sweep): "Are you sure you want to load an old game over this current one?" (`0xb1f6`), "… redistribute?" (`0xb36c`), "… purge this character permanently?" (`0xb55a`), "… save this game over your previously saved one?" (`0xbaa0`, heard live), "It will take this character two months to make the journey here." (`0xd24c`, asked only for a member whose town word `+0x88` differs from the guild's `0x240e6`; OK adds 4 to the age clock `+0x92` and moves him: DOS's `AddNeedsMoveConfirm` / `AddAgeCost`). s141 wired: `GameSession.AskConfirmQuestion` (`P3Sounds.ConfirmQuestion`) plays 36 when the Guild Add dialog asks its question; s158 File › Load / Save under the town bar; s160: before every one of III's load / save / redistribute / purge questions under every III layout (`P3ConfirmQuestion`, 8.9) |
| 39 | 2 | `0x18610` (s136: in `0x1833e`, a combat line ending " dies!"; callers `0x1665c`, `0x18270`, `0x182ac`, `0x18328`, `0x187ce`. s138 `[C]` static: `0x187ce` is in `0x186f6(group, spell, rank, member)`, the party's **Fireflash I-IV**: the cast routine's case for spells #5-8 (`0x168f2`, jump table `0x1705a`) calls it for order 7 (Cast at rank 1, group `0x24794`) or 16 (Cast at rank 2, group `0x2481e`) when that group has a living monster; it picks the monster with `0x187da`, rolls `rnd((INT / 2 + 1) · t[spell])` (the caster's INT, record +0x0c at `0x255a6`; magic +0x1e is `0x255b8`, table `0x2271a`), caps it at 10 / 30 / 60 / 99 by tier, at least 1, and calls `damage_monster` with the flash set. Built s137 `CombatEncounter.TagDies`: every monster kill in Core (melee, the party's Fireflash and the other spells through `CombatResolver.ApplyMonsterDamage`) tags its " dies!" line with 39; a member's death has none) |
| 42 | 2 | `0x1092c` (s136: " The spell was not learned... "), `0x1d516` (s136: the **Damage Report** window `0x1d4ea`, shown by every party-damage call `0x1d24a`: a sprung trap `0x1c7b6`, ops 1–4 `0x1cb00`, op 16 `0x1cc78`, and `0xa676` / `0xa6b8`); the dungeon step's special doors 0xc7 / 0xd6 (s136: `0x1c4e0` writes 42 with result 4, message 4 "No one can pick the lock"; a locked door 0xc6 / 0xd5 nobody picks gives result 4 with no sound); event op 9 "[original text]" (s135: op 9 sets the message and falls into op 10, which writes 42 at `0x1cbf2`); ops 1–4 (`0x1cb06`) and op 10 on its own write 42 with no message, so the op tail does not play it and the word is left for the dungeon step's play at `0x1c246` (s136 `[C]`, below) |
| 45 | 3 | `0x1dde6` (the defeat's demon appears) |
| 48 | 2 | `0x18132`; the defeat scene: a member destroyed (§6.1) |

**The event word `0x240fa`** (s136 `[C]` static; DOS's `FAR:0xde58`, written with the same numbers, `dungeon-events.md`
7a): `0xb126` clears it after any play. The dungeon loop `0x1bf4e` clears it before each key; the step `0x1c36c` writes
12 (wall) or 42 (special door); the loop shows the step's message through `0x1c28c` (`0x1c23a`), which plays the word
with any message but 1 (`0x1c314`), and plays what is left at `0x1c23e` once the step (and any event program it ran) is
over; an exit's "Will you take it?" clears it (`0x1c1b4`). The event-op routine `0x1ca6e` runs **one op a call** (called
at `0x1c9b0`) and clears the word first (`0x1cadc`); its tail `0x1d1a6` plays the word when the op set a message
(`%a5@(-2)`: ops 6 = −1, 7, 9, 12, 14). So per op: 7 → 3 with "[original text]", 9 → 42 with "Some gold was
taken.", 12 → 9 with "[original text]", 14 → 27 with "[original text]"; 8 and 10 on their
own write 3 / 42 and ops 1–4 write 42 (after the Damage Report's own 42) with no message, which sound at the step's end
only when that op is the program's **last**: the loop `0x1c952` calls `0x1ca6e` for every op byte, an ending one (type 0
or above 27) too, before its type test (`0x1c9c0`), so an op with the continue bit loses its word to the next byte's
clear; 13 and 15 write nothing (silent). Built s136: Core `P3Sounds` / `GameSession.PlaySound` (the numbers are DOS's
too: ovl4 writes `FAR:0xde58` = 12 / 42 at `0xf6c` / `0xf92`, 0 per op at `0x1514`, 42 for ops 1–4 / 9 / 10 at
`0x1550`, 3 at `0x1609`, 9 at `0x1672`, 27 at `0x1693`); the Client plays them from `sound_effects` on III's dungeon
and overworld with the Sound switch on.

**Sound 15** (`e2 02 c7 0e e5 0e c4 00 f8 32 05 00 0f ff` on voice 1, the same without `e2 02` on voices 2 and 3): tempo
2, row 14, noise, attack and decay 0, sustain nibble 15, release nibble 8; note `0x32` (period `0x21ed0`[41] = 1524) for
(5 + 1)·2 = 12 VBLs at volume 63 (the sustain level 16128 lies above the row-14 ceiling 15053, and attack 0 / decay 0
end on the sustain), then a rest of 32 VBLs releasing 836 a VBL; `0xFF` read on VBL 44, the channels off on 45: **45
VBLs**, 0.75 s at 60 Hz `[~]`.

Remaster (s110): pack `sound_effects` (`OpenPhantasie.Data.Audio.P3SoundEffects`: per sound the three stream offsets in the
program file, null for a 0 pointer); a run of sounds at known VBLs is one driver run (`P3MusicScore.Run` with a
`P3DriverStart` per sound, each start cutting the one before), played by `OpenPhantasie.Client.Audio.SoundEffectPlayer`.
Wired: 15 on the reward flashes (§2.4) and the combat flashes (§2.4.1), with the game's Sound switch (D3-066). s117:
15 / 18 on the combat field's melee lines (§8.1); 45 when the defeat scene's demon line shows and 48 / 9 with each
member's fate line (the remaster turns those pages with Return, so each sound plays as its page shows rather than after
the Amiga's waits). The others are packed and decode (`P3SoundEffectTests`); their callers are named in the table above (s136), and the
dungeon / overworld ones are wired (the event word paragraph above). s137: 36 (Armory refusals), 6 / 42 (guild learning), 39 (a monster's " dies!" line). s141: 36 before the guild Add's "two months" question. s138: 6 on Guild New's Y (the roll kept), 12 on a failed save (the Disk Error box, `[~]` equivalence); "Insert your Phantasie III disk." has no remaster moment; **traced live s138** (FS-UAE on Xvfb, sound off, console debugger: a breakpoint on `0xb126` logged each number and its caller; the master ADF's saved party, CODE loaded at `0xC07988`): Guild New → OK on the sheet → 6 from `0x13938`, then the "Rename Character" box ("Current name is: [Unknown]"); the Armory: 36 from `0x111e0` when buying a 1-gold Small Shield with a full pack, 36 from `0x11386` with the party's gold patched to 0 (long at `0xC2D6CA`, flat `0x25d42`); leaving Pendragon and walking: 24 from `0x14a28` with the encounter; the fight: 15 from `0x1662c` per melee hit and 39 from `0x18610` with each Sniverling's death. **Traced live s140** (`../working/capture_s140_p3_sounds.md`, the same set-up, label stops added to the watcher): 6 / 42 of the guild's learning (`0x10902` / `0x1092c`, the roll forced each way), 42 for the Damage Report (`0x1d516`, event op 1) and then op 1's own event-word 42 at the step's end (`0x1c246`), the special door 0xc7 (`0x1c4e0`, played by `0x1c28c` with "No one can pick the lock"), op 9 (`0x1cbf2`, played at the op tail `0x1d1b6` with "[original text]"), 39 from `0x18610` right after the party's Fireflash `0x187ce` ("Kobold dies!", the round's only party action), 12 for the wall (`0x1c4be`, played with "That is a wall") and for the Disk Error box (`0x20308`, after the box is drawn; the save's Open forced to fail). Not traced: "Insert your Phantasie III disk." (12, no remaster moment). Also found: 36 before window 1's confirm questions (`0xaae2`, table above). Method: `RE-REFERENCE.md`, FS-UAE section.

## 8. Seen in a running game (s116, FS-UAE)

A500, Kickstart 1.3, PAL (`tools/scripts/fsuae_run.sh`, input `fsuae_input.sh`); a new game, four level-1 Human
Fighters. Shots in `parity/shots/p3-amiga-*.png`. Timings come from host-timed screenshots (~24 a second), so ±0.05 s.

- **Town screen** (`p3-amiga-town-pendragon.png`): the game's own menu bar (list `0x238a4` drawn by `0x12d6`, §8.5; not Intuition's, s120) "File  Guild  Options" (Options: A) Armoury,
  B) Bank, I) Inn, M) Mystic, X) Exit, C) Cast Spell, D) Distribute, G) Trade Item, U) Use Item, V) View Party;
  `p3-amiga-town-options-menu.png`; File: ? Info, O) Load, S) Save; Guild: + Add, − Drop, E) Examine, N) New, P) Purge,
  R) Rename, L) Learn Spells, T) Training). The menu letters work as keys. The name scroll matches `0x7e54` as the pack
  places it (centre x 160, cell row y 16, pen 2): the name is printed as stored ("Pendragon", mixed case) in topaz 8.
  Pen 2 shows as hardware `0x842` (#884422): the art's `& 0x777`, `<< 1` (§1); the pack's #924924 is the house 3-bit
  ramp of the same value, kept on purpose (I and III alike). The remaster prints the name in its own upper-case font.
- **Bank** (`p3-amiga-bank.png`, `p3-amiga-bank-deposit.png`): clicking the door opens one window: "The party is in the
  bank." / "[original text] n" / "[original text] n" / two picks "[original text]", "[original text]"
  (yellow = picked) / OK, CANCEL. Withdraw: the window closes, no message. Deposit: the same window with "How much:"
  and an **empty** field (DOS's reading opens it holding the party's gold, `DosDepositDialog`; left as built, an
  Amiga-vs-DOS detail); Return closes it, no message. A new party had carried 0 and 1024 in the bank: starting gold is
  banked, as Core does (`CharacterCreation`, `GoldInBank`).
- **Trade Item** (`p3-amiga-trade-who.png`, `-trade-item.png`, `-trade-to.png`): "[original text]" (the
  party list: name, race, level, class, status) → "[original text]" (the giver's items) → "[original text]" (the
  whole party, the giver too; **no sell line**, so `give-only` is confirmed) → OK hands it over with no message and goes
  back to "[original text]" (the remaster's Trade now does the same, s116). CANCEL leaves.
- **Combat** (`p3-amiga-combat-orders.png`, `-combat-field.png`, `-combat-field-attack.png`, `-combat-field-dead.png`):
  the orders screen has a box per member (name + order, "is asleep" for sleepers), the order list (Attack, Lunge,
  Slash, Thrust, Parry / Fire Bow, Aim Blow, Cast Spell, Move Back / Middle / Front), a "Fight!" button, the monster
  groups ("5 Sniverling(s)  KKKKK") and the body-figure party panel; Options: F) Fight, T) Threaten, G) Greetings,
  [original text], R) Run away, [original text], H) Party health; Speed menu: Fast / Normal (ticked) / Slow. The round
  plays on a black field: monster groups in rows at the top (5 then 3 here), the party's portraits in a row below,
  **one line at a time** under them, centred, **party lines blue, monster lines red**. An attacker shows its attack frame
  ~0.4 s before its "hits!" / "misses" line; each line stays **0.6 s** (Normal: 30 ticks of 1/50 s), with one blank
  frame between lines. A member killed in the round becomes the **grave** (§6.1's sprite 16) on the field. Running
  away shows "Each survivor gets the following / Gold: 0  Experience: 0" with OK (`p3-amiga-flee-reward.png`).
  No flash was seen: the only hits were melee (no spell, no monster special, §2.4.1).

### 8.1 The combat field in code (s117) `[C]` static

Read to build the field (Client `CombatFieldRun`, `UiOverlays.DrawCombatField`; the AMIGA SCREENS switch, on by default):

- **A line** `0x166c6(text, side)`: colour 11 when side = 1 (the party; combat palette `#6D92FF`, blue), else 12
  (`#DB0000`, red); column (40 − length) / 2 − 1 on text row 24 (`0x854`); then `0x1bacc(30)`; then the row is cleared
  (`0x648`: (0, 191)–(319, 199), colour 0). Members print with side 1 (`0x16638`), monsters with 0 (`0x17b34`).
- **Waits** `0x1bacc(w)`: the Speed word `0x240f6` (menu: 1 Fast, 2 Normal, 3 Slow): Slow doubles w, Fast takes
  w >> 2, Normal keeps it; then `0x21404` = dos.library `Delay` (−198), 1/50 s ticks. So a line is 30 ticks (0.6 s),
  60 Slow, 7 Fast.
- **A melee swing**: a member's "hits!" plays sound 15 then prints (`0x1662c`), "misses" sound 18 (`0x16682`); a
  monster's the same (`0x17eda` / `0x17b28`). Before it the attacker animates: `0x1b6be(slot)` for a member and
  `0x1b150(group, m)` → `0x1b7c4` for a monster draw the attack frame, wait 21 ticks (`pea 0x15`), then draw the idle
  frame (the captured ~0.4 s).
- **Places**: rank 1's group at y 78, rank 2's at y 28 (fight set-up `0x14e10` / `0x14e26` → `0x1b4c8`, group `+70`); a
  giant group (`+72`) at y 50 (`0x1b4e6`). Monster x as §2.4.1 (`0x1b150`). Portraits at x 48·slot (`0x1b77c`), y by
  combat row (`0x1b42e`, §6.1).
- **Remaster `[~]`**: lines that are not a swing take the side of the actor whose turn printed them (the damage and status
  routines' own side argument is not read); a monster killed in the round vanishes from its row when its "dies" line
  prints (seen s120: the row is redrawn without it as the "dies!" line prints, §8.6); since s121 the round's spell
  flashes play mid-round on the field, each just before its hit's first line (`CombatHit.Line`; flash → "takes" →
  "dies", §8.6), a flash with no line after it after the round's last line. **A bow shot (s156, `[C]` static)** is a swing: the
  member's attack turn `0x16230` (called from the round at `0x15816` / `0x15838`) serves every order but Parry, Fire Bow
  R1 / R2 (4 / 15) included; per attack it picks the target (`0x187da`, none → nothing), draws the attack frame
  `0x1b6be(slot)` (`0x164d2`; slot 6 for a summoned elemental's), then the bow's to-hit (tables `0x242fc` / `0x2430a`,
  `0x164da`) or the melee's, then sound 15 + " hits!" or sound 18 + " misses" as a swing. The remaster tags a shot's
  line as a swing (`P3CombatModule.FireBow`), so it gets the frame and the sound. The orders screen between rounds is built since s118 (§8.3).
  Watched s117 on Xvfb: rows, portraits, attack frames, red / blue lines and a skeleton leaving its row on "dies".

### 8.2 The Bank and Trade windows (s117)

Window table at DATA `0x23228` (52-byte structs, §2.3): the **Bank is window 9** (box (24, 48)–(296, 146), title cell
(5, 7) colour 4, OK / CANCEL at bytes (9, 117) / (24, 117)); `0x7464` stores the bank text pointer (`0x230fc` →
"  [original text]\r\r[original text]\r[original text]") in its `+22` `[C]`. The Trade windows
match **window 6** ("[original text]" / "[original text]": box (8, 44)–(312, 160), buttons (7, 135) /
(24, 135)) and **window 10** ("[original text]": box (56, 36)–(264, 164), buttons (11, 139) / (22, 139)) by box and
button places on the captures `[~]` (the code that picks them is not read). Text places in the pack
(`keep_windows.bank` / `trade_who` / `trade_item`) are measured on the captures `[~]`. Remaster (Client
`AmigaBankDialog`, `TradeDialog.View`, `UiOverlays.DrawAmigaWindow`): Up / Down pick (yellow), Return / O = OK, C =
CANCEL; under `p3.bank` = `dos` the picks are the Amiga's ([original text]; [original text] with an empty "How much:"
field), under the other bank rules they open the remaster's withdraw dialog / deposit prompt. Watched s117 on Xvfb.
**Read in the code (s156, `[C]` static), closing the `[~]`s above:** Trade's "[original text]" is
`0x127ce(64)` from the town dispatch (`0xb9b4`) and "[original text]" `0x127ce(65)` from `0xf58e`; `0x127ce(n)` always
draws window 6 (struct `0x23360`) with the title `0x23084[n]` at the struct's cell (5, 7) = (40, 56) and the member rows
by `0x12ac8` from column 2, row 9 = (16, 72). "[original text]" is Trade's own window 10 (`0xf2c0` → `0x3904(0x23430)`;
(`0x23430` − `0x23228`) / 52 = 10), the title by `0x854` at column 10, row 6 = (80, 48) colour 8, the items at column 11,
rows 8 + i = (88, 64 + 8 i) colour 4. The Bank (`0x1185c`, window 9): the title row from cell (5, 7) with its two leading
blanks (ink from (56, 56)), the labels at (40, 72) / (40, 80), the values by `0xab54` at column 28, rows 9 / 10 =
(224, 72) / (224, 80) colour 8, the picks `0x22a24` / `0x22a28` at column 11, rows 12 / 13 = (88, 96) / (88, 104)
colour 4 ("How much:" `0x23214` at the same (88, 96)). Every place the pack holds is the code's.
s157 (user): under `p3.bank` = `manual` / `no-fee` with AMIGA SCREENS on, those two follow-ups draw in window 9's style
(same box, frame, list colours, title cell and OK / CANCEL; the box grows down when the rows need it): "Who will
withdraw?" with the party's gold and one row per member (bank gold in the value colour; Up / Down or the digit, Return / O
= OK, C / Esc = CANCEL leaves), then "Withdraw from NAME's account" with the "How much:" field (C / Esc back to the
picker); the share deposit shows "Shares for: NAME" in the title cell, the gold carried, and one pick row per share count
(C = CANCEL, nothing deposited). **The rows and their places inside the window are the remaster's own design**: the Amiga
has no per-member withdraw and no share deposit (its bank is withdraw all / deposit an amount split evenly, D3-025).
Client `WithdrawDialog.View`, `AmigaShareWindow`; seen s157 on Xvfb.

### 8.3 The combat orders screen (s118) `[C]` static

`0x188e4` (called from the fight loop `0x14f6c` after the Options menu's F) Fight, when `0x2413c` = 0) draws the screen
with `0x18c48` and loops until F. Text through `0x854` on 8 × 8 cells; boxes through `0xac48` (four `0xa40` lines).
`0x18c48` first sets the all-black palette `0x21a88` (never written: it hides the drawing), clears the screen, draws, then
restores `0x2437e`. The colours seen are the **map palette**'s (frames tan 4, orders yellow 8, Fight! box red 10,
monster lines orange 9, roster names teal 5, white 15), matched on `p3-amiga-combat-orders.png`; s156 `[C]` static: in
every fight `0x2437e` holds the map palette (8.4 "Button palette").

- **Member boxes** (one per member up to the party count `0x240e0`, the elemental included): box (6, 6 + 16 i)–(194,
  17 + 16 i) colour 4; name at column 1, row 2 i + 1, colour 15; at column 9 colour 8 the order label `0x22a38[order]`
  (Attacks / Lunges / Slashes / Thrusts / Fires Bow (R1), Parries, Aims Blow (R1), Casts Spell, Moves to Back / Middle /
  Front, then the five R2 labels at 11–15), a cast (7, 16) shows the spell's name `0x22d4c[spell]`; status (`+0x2c`) 0
  "is dead", 2 "is asleep", 3 "is out" instead. Before printing, the stored order is rechecked (cast with a bad spell or
  too few MP → Thrust; Lunge R2 with no rank 2 → Thrust; Fire Bow R2 with no rank 2 → Fire Bow R1; `0x188b6` true → Parry):
  these are the Amiga's readings of DOS rules; the remaster keeps Core's (DOS) rechecks.
- **Order list** `0x231d4` at column 1, row 15, colour 15 ("Attack     Fire Bow" … "           Move Front", " Fight!"
  on row 21); the **Fight! box** (6, 166)–(73, 177) colour 10.
- **Monster groups** `0x1a264(2)`: rank 2's group (`0x2481e`, set at y 28 on the field) on row 23, rank 1's (`0x24794`,
  y 78) on row 24, colour 9: the count at column 0, the name with "(s)" at 2, one letter per monster from column 19
  (`0x1a57a`: status 2 S, 3 D, 4 O, else K). This also answers §8.1's "which group struct is rank 1".
- **Roster** `0x9198` + `0x93e0` (`0x240d8` = 1): every member but a summoned elemental (`0x24124`), slots 0–2 at column
  25, rows 1 / 9 / 17, slots 3–5 at column 33. Name (colour 5) at the cell; HP and MP as current right-aligned to column
  + 2 (`0xabec`: 10–99 one cell left, 100–999 two, 1000+ not moved), ":" at + 3, the most at + 4 (colour 4); "H" / "M"
  at + 7 for slots 0–2 only (`0x23168`, colour 9); the class (`0x22eac`: FGTR, THIF, PRST, RNGR, MONK, WZRD) one letter a
  row down column + 0 from row + 3, the status (`0x22f00`: DEAD, OKAY, SLPG, " OUT", DOWN) down column + 6; the body
  figure (§2.1) at x 216 / 280, y 32 / 96 / 160 unless dead; the row (`0x22a2c`: "(Back) ", " (Mid) ", "(Front)") at
  row + 7.
- **Input** `0x188e4`: hit tables at `0x22092` (4-word entries: column, row, width in cells, key; −1 = the entry's
  index): kind 1 `0x21f9a` = the member rows (column 1, row 2 i + 1, width 23; their "keys" are 0–6, so a member is picked
  **by the mouse only**; a missing member's column is set to 30, out of reach) and Fight! (1, 21, 8, 'F'); kind 2
  `0x21fdc` = the list: A Attack, L Lunge, S Slash, T Thrust, P Parry / B Fire Bow, M Aim Blow, C Cast Spell, 1 Move Back,
  2 Move Middle, 3 Move Front, and F. A hit flashes its cells (`0x3d86` XOR, 4 VBLs). Dead, asleep and out members can't
  be picked. The picked member's name cells (7) stay inverted and "Select option:" replaces its order; the key goes
  through `0x18b02`: A 0, L 1, S 2, T 3, B 4, P 5, M 6, C 7, 1 / 2 / 3 → 8 / 9 / 10, F fights, **any other key → 3
  (Thrust)**. `0x19728` then `0x1907e` apply it: a move (8–10) writes the row word (`+8`) at once and redraws the roster
  and portraits (`0x1b42e`) and **keeps the order**; Cast opens the spell window 11 (§8.4); for the orders that ask (Remaster below, s156: `0x24a04` is the member's class, not a rank flag) the rank question `0x19806` opens window 20 "Target rank 1? (or
  2)" (OK = rank 1, CANCEL = rank 2: the order + 11, Cast → 16). The Amiga card (III-AU PDF 18): "you may change a
  character's defaulted action by clicking the character's name and then selecting the Option … you enter combat by
  clicking the Fight! box". No manual conflict.
- **Remaster** (Client `UiOverlays.DrawCombatOrders`, `CombatPresenter.AmigaOrders`; AMIGA SCREENS on, pack with the body
  figure and `keep_windows.rank`): all of the above 1:1 in the remaster's font; Core decides when the rank question is
  asked (DOS: Fire Bow, a Thief's Thrust / Slash / Attack) and every order's rules. **Which orders ask on the Amiga
  (s156, `[C]` static, `0x1907e` on the member's record copy `0x24970`, class word `0x24a04` (1 = Thief), rank 2's count
  `0x24820`):** Attack: Attack skill (`0x242d2[slot]`) ≥ 76, else Thrust; a Thief with rank 2 alive is asked, anyone else
  gets Attack (R1). Thrust: a Thief with rank 2 alive is asked, else Thrust (R1). Lunge: never asked; a Fighter or Thief
  with rank 2 alive gets Lunge (R2) (order 12), else Thrust. Slash: Attack skill > 149, then as Attack, else Thrust. Fire
  Bow: Thrust when `0x24a64` ≥ 2 or no bow (`0x249f6` ≤ 0) or the blocking body states (`0x24a18` = 16, `0x2499c` = 4);
  else asked when rank 2 is alive (any class), else Fire Bow (R1). Parry and Aim never ask. So the Amiga asks for exactly
  the orders Core asks for under `p3.thief-attack` = manual (D3-012: the Thief's Attack asks); under `dos` Core's Thief
  Attack does not ask (DOS's own rule), where the Amiga would. Additions: digits 1–7 pick a member from the keyboard; Return / Backspace / Esc leave a pick with no change
  (Esc reaches the screen instead of the game menu while a member is picked); in the rank window Esc / N also answer
  CANCEL, Y / Return OK. Gamepad: as the remaster's own orders list, keys only (no pad path before either). Cast and the
  heal target are the Amiga's windows since s119 (§8.4). The PARTY OPTIONS step is the Amiga's field with its menu bar since s119 (§8.5). B is Fire Bow here
  (the bestiary key steps aside). Watched s118 on Xvfb: boxes, "is asleep" / "is dead", rank window, Fire Bow (R2),
  a move to (Back) with the order kept, Healing I with its target, the Fight! button clicked.

### 8.4 The spell and heal windows (s119) `[C]` static

- **Spell window 11** (`0xf770`, struct `0x23464`): box (56, 24)–(252, 176), no title; buttons OK / CANCEL / MORE at bytes
  (8, 151) / (16, 151) / (24, 151) (x 64 / 128 / 192), keys −1 / `C` / `M`. The button count (+26) is set to 3 when the
  member knows more than 12 spells (`0x249c6` > 12), else 2, so MORE shows only then. "Select" at cell (8, 4), the
  member's name at column 15, "'s spell:" after it, all colour 8 (yellow).
- **The list** `0xff82(first, last, page)`: before the window the caller sorts the record's spell list (`+0xb0`, 32
  words, ids 1-based, 0 = empty) with `0xd08c`, a selection sort **highest id first**; the window shows 12 a page from
  row 6 (`0x10192`: the name `0x22d4c[id]` at column 8, the MP cost `0x226e1[id]` at column 27 through `0xab1c`), colour
  4, the cursor's row redrawn in 8; the rows (64, 48)–(224, 144) are cleared before each page. Every known spell is
  listed, castable in combat or not.
- **Keys** (`0x3ba0` then `0xff82`): cursor up (raw 160) or `8` up, cursor down (162) or `2` down, wrapping on the page;
  `M` or MORE the next 12 (after the last page the first again), the cursor back on the top row; Return / `o` / `O` / OK
  takes the cursor's row; `c` / `C` / CANCEL = −1. A press on a row (hit band `0x1776(6, 6 + n − 1, 8, 27)`, polled by
  `0x17a6`) **moves the cursor there**; it does not take it (OK does).
- **After the pick** (`0x19430`…): CANCEL → Thrust (`0x19656`). Refusals, each a message box (`0xaaac`) and **Thrust**:
  no spell known "[original text]" (`0x19690`, the window never opens), MP 0 or the cost above the MP
  "[original text]" (`0x1966c` / `0x19632`), id 52, 54 or ≥ 57 (Monster Evaluate, Transportation,
  Divine Aid and the plane spells; 53 is the unused Vision) "[original text]" (`0x1949c`).
  Ids 5–8 (Fireflash) with rank 2 alive ask "Target rank" (window 20, §8.3). Ids 1–4 (Healing) and 44 ask the target.
- **Heal target** = window 6 (`0x127ce(33)`, struct `0x23360`, the Trade windows' box (8, 44)–(312, 160), OK / CANCEL at
  x 56 / 192, y 135): "[original text]" (`0x23084[33]`) at cell (5, 7) colour 8; one member a row from row 9
  (`0x12ac8`: name at column 2, race 10, level 21, class 24, status 32 — Dead / Okay / Asleep / Out / Down), colour 4,
  the cursor's row 8; the same keys and row press (band columns 2–37); CANCEL → Thrust; a dead target → `0x23104` and
  Thrust (`0x1958e`).
- **Remaster** (pack `keep_windows.spell` / `.heal`; `CombatPresenter.HandleAmigaListKey`,
  `UiOverlays.DrawSpellWindow` / `DrawHealWindow`): the windows, rows, colours, keys, MORE paging, row press and
  CANCEL → Thrust 1:1; Esc / Backspace go back to the member boxes with no change (the remaster's). The spell taken then
  follows Core's (DOS III) rules, which are the Amiga's: **DOS III's root `0xfc41` refuses exactly as the Amiga does**
  (`combat-actions-and-ranks.md` §2 "Cast refusals", s128 static): ids 52, 54, > 56 "This spell cannot be cast in
  combat.", cost above the magic "[original text]", a Dead heal target "This character is dead or
  out.", each a message and Thrust (R1); DOS's list also shows every known spell. So no DOS ↔ Amiga rule difference here.
  Remaster (s128): `P3CombatModule.CastRefusal` (Core packs Thrust (R1); before s128 it rolled I's Parry), the message on
  the feedback line until the next key (`CombatPresenter.CastNotice`) on the remaster's own order list.
  **Before the list (s134, `[C]` static):** `0x19430` tests the spell count `0x249c6` (record `+0x56`, every known spell)
  ≤ 0 → "[original text]" (`0x19690`), then `0x1943a` the current magic `0x2498e` (`+0x1e`) ≤ 0 →
  "[original text]" (`0x1966c`); each stores action 3 (Thrust) with no rank question and the spell
  window never opens. DOS `0xffef` / `0xffdf` the same. Built: Core `P3CombatModule.CastRefusalBeforeList`, used by both
  III order screens. Under `p3.combat-spell-list` = `manual` a member who knows only non-combat spells passes both tests
  and gets an empty window (OK does nothing, CANCEL = Thrust) `[~]`.
  **The message box is window 7 (s134, `[C]` static, seen):** `0xaaac(text)` puts the text in window 7's title (`+22`,
  `0x233aa`) and draws struct `0x23394` through `0x3904` with save flag 1 (what lies under is put back after): frame
  colours 4 / 3, fill 0, box (0, 0)–(319, 52), the text at cell (1, 1) = (8, 8) in colour 8 on 0, one button (`+26` = 1)
  OK at bytes (9, 27) = (72, 27), its key `+46` = 0. With key 0, `0x3ba0` ends the wait on **any** key (`0x3c2a` clears
  its loop flag for every non-zero key) or a click on OK. No sound (`0xaae2`, which puts its text in window 1, plays sound
  36 first). 78 callers: the combat refusals, the town / guild refusals ("[original text]" …), the spells cast
  outside a fight, the disk messages. Seen on FS-UAE (s134, the TOSEC save in Pendragon, Options → Cast Spell →
  sprite → Monster Evaluate): "[original text]" with the box (0, 0)–(319, 52) (outer frame row 0 / column 0,
  inner row 52 / column 319), the text's ink from (8, 8) in (238, 204, 68) = map index 8, OK at (72, 27)–(127, 44); an
  `x` closed it (`parity/shots/p3-amiga-window7-cannot-cast-here.png`). Built: pack `keep_windows.message`,
  `UiOverlays.DrawMessageWindow` on the orders screen (`CombatPresenter.AmigaMessageShown`: the next key, Esc included,
  or OK only closes it), watched on Xvfb (`parity/shots/p3-remaster-window7-knows-no-spells.png`). The other 77
  callers: the caller table below (s135). **s162:** the text is topaz 8 (the shot's "[original text]" equals the
  ROM glyphs, 1664 of 1664 pixels); pack `keep_windows.message.font` = `amiga-topaz-8`. Not built: Resurrection on a living target ("This character is not
  dead.", Core's Resurrection order has no target). Watched
  s119 on Xvfb (`:79`): a level-12 Priest's two pages, MORE, Healing III → the heal window, cursor, a row press, CANCEL →
  Thrusts (R1).
- **Window 7's callers (s135, `[C]` static).** All 78 `jsr 0xaaac` in the flat image, the text resolved from the push
  before each call (a `movel 0x230xx` names a pointer table slot, a `pea` the literal). Grouped by routine:

  | Routine | Callers (text) | Remaster |
  |---|---|---|
  | `0x8074` inn | `0x8264` "The party cannot afford the stay here." | `core.p3.cannot_afford_stay`: window 7 (the remaster's test party rests with I's inn, so not seen live) |
  | `0x83e0` the keys outside the towns, Cast / Use (s140: callers the overworld loop `0x7d20` and the dungeon `0x1c024`, 8.9) | `0x84ba` "…unable to do this.", `0x84e0` "…knows no spells.", `0x8576` "…not carrying anything." | s158: "…knows no spells." = `cast.x_knows_no_spells` (the remaster's Cast when the caster knows no spell at all; III's table: S443): window 7, here and in town (`0xb8aa`); the other two have no fixed remaster line (the remaster's dead caster / its potion list) `[~]` |
  | `0xb1cc` town / guild menu dispatch (s158: read key by key, below) | "[original text]" (`0xb274`, `0xb57e`, `0xb7f6`, `0xbb46`), "The party is fully assembled." (`0xb2e2`) / "You can only add an elemental." (`0xb2f2`), the two picked by the word `0x240e2` (its meaning not read `[~]`), "[original text]" (13 places), "…unable to do this." (`0xb42a`, `0xb67e`, `0xb8ba`, `0xb976`), "The local armoury is out of stock." (`0xb44c`), "[original text]" (`0xb4b6`, New), "[original text]" (`0xb524`), "[original text]" (`0xb54a`), "You cannot rename an Elemental." (`0xb742`), "…knows no spells." (`0xb8aa`), "…not carrying anything." (`0xb9fa`, `0xba7c`), "You cannot examine an Elemental." (`0xbb34`) | window 7 (s158): `core.guild.cannot_purge_party_member`, `core.guild.roster_full`, `guild.roster_empty` (Add), the empty-party lines of Drop / Learn / Training (III's table: "[original text]"), `core.p3.cannot_train` (S417); the rest: the remaster's lists leave the refused rows out, no fixed equivalent `[~]` |
  | `0xd100` guild Add | `0xd12a` "[original text]" (an elemental with no member), `0xd15a` "[original text]", `0xd180` "[original text]", `0xd210` "[original text]" | `core.guild.party_not_assembled`, `.elemental_already_joined`, `.already_in_party`, `.party_is_full`: window 7 (the remaster's Add list hides these rows, so rarely reached) |
  | `0xd684` View Party (s158: the town's V, `0xb6d4`; Examine is `0xd796`) | `0xd6c2` "You cannot examine an elemental." | none |
  | `0xde0c` Training | `0xe36c` "This character is at maximum level." | none: the dispatch's T tests only the status and the class (`0xb92a`…); DOS guild_menu item 1 refuses level ≥ 21 with S417 "[original text]" (root `0xad25`), Core's rule, so `core.p3.cannot_train` reads S417 (s158) and this Amiga line has no remaster moment `[~]` |
  | `0xec76` Use Item | `0xefe6` "…unable to do this.", `0xf13a` "This character's health improves.", `0xf1dc` "This character's magic restores." | none: the remaster's item lines are I's `[~]`; s158: the dispatch's "…not carrying anything." (`0xba7c`, U by a member with no item; DOS root `0x8cc6` S416) = `useitem.x_carries_nothing` in III's table: window 7 (the remaster tests carried and worn items; whether `+0x26` counts worn ones not read `[~]`) |
  | `0xf2c0` Trade | `0xf5be` "[original text]" | `core.p3.cant_carry_anymore`: window 7 |
  | `0xf770` Cast outside a fight (s162 `[C]` static, the routine at `0xf8f6`: MP < 1 → too low; the list `0xf770`; cost > MP → too low; ids 1–4 → `0xfcbe`; 44 → a target (status > 0 → not dead, else raised and "restored"); 54 outside a town or 52 in a town → "cannot cast this here"; 54 to the same town → "already in this town"; every other id → "cannot cast this here") | `0xf950` / `0xf9a0` "…magic is too low!", `0xfa08` "This character is not dead.", `0xfa22` "This character's life is restored.", `0xfa4c` / `0xfb02` / `0xfb1e` "[original text]", `0xfa7a` "The party is already in this town." | s162: III's table says Core's out-of-fight refusals in these words, all window 7: `core.magic.not_enough_mp` = S484 "[original text]", `core.magic.cant_cast_here` and `.only_in_combat` = S434 "[original text]" (the remaster's list leaves most refused spells out, so rarely reached); "not dead" / "restored" / "already in this town": no remaster equivalent (Resurrection's target, Transportation's town pick) `[~]`. s165: the order is built: III's Cast refuses "…magic is too low!" with current magic < 1 right after the caster pick (before the list) and with the picked spell's cost above the magic before the heal target (`SpellCaster.OutOfCombatMagicRefusal`, `CastDialog`; the routine's code starts at `0xf8f8`, its callers `0x84c4` / `0xb8aa` refuse "knows no spells" first). DOS III's `cast_spell_noncombat` root `0x8e50` has the same order (`0x8e9c` magic < 1, the list `0x90d9`, `0x8eeb` the cost, then `0x194d` the target), so no DOS ↔ Amiga difference; I / II keep theirs |
  | `0xfcbe` heal outside a fight | `0xfd42` "[original text]" | s163: Core says it after every out-of-fight III heal (`core.p3.character_health_improves`; DOS root `0x194d` prints the same text at `0x19be` once the target is picked, where I's DOS prints nothing), window 7; watched on Xvfb (`p3-remaster-s163-window7-health-improves.png`) |
  | `0xfed6` Monster Evaluate | `0xff2e` "The approximate level of the monsters\r      within this region is" + the level (`0x1aa40` + 1, × 2, at most 12; s162: the text is copied to a buffer and the level's digits (`0x1d8a`, itoa) written at byte 66, after "is "; DOS root `0x1b02` does the same at `bp − 0xe`) | s162: III's `core.magic.monsters_at_level_x` = that text with the row joined ("… monsters within this region is {0}", the band's one line), window 7 (the level itself is Core's rule, D-056); s163: the text keeps the programs' "\r" and row 2's six blanks (one byte span in both, `en.sources.json`), so window 7 prints row 2 one 8-px row down from the title column with its six blank cells (`UiOverlays.MessageRows`); the remaster's one-line band turns the break into a space (`Game1.OneLine`). Not seen in the Client: the test party knows no Monster Evaluate `[~]` |
  | `0x103f2` Learn Spells | `0x1058a` "[original text]", `0x1068a` "[original text]\r[original text]" | `core.guild.learn_no_more` (III's wording since s156) and `core.guild.learn_cant_afford` (s158: DOS ovl2 `0x1d12` prints the same DS text when the party's gold is below the cost; Core tests the same after the pick, DOS before the list): window 7 |
  | `0x12bfc` New character | `0x139fc` "The character roster is now full." | none |
  | `0x188e4` orders screen | the six cast refusals (s134, above) | built s134 |
  | `0x1a192` take an item | `0x1a1a4` "The party can't carry any more items." | none `[~]` |
  | `0x1c36c` dungeon step | `0x1c674` "<name> picks the lock.", `0x1c75a` "<name> [original text]" (both built in `0x24632` by `0x1f572`, below) | s162: window 7 (`core.p3.x_picks_the_lock` / `x_disarms_the_trap`; `text_ids` take format items, `AmigaMessageBox.Matches`), the OK and text in the dungeon palette (`DrawMessageWindow(inDungeon)`, text colour 8 → `#FFFF00`). s163: all four of the step's member lines seen live on the Amiga (`../working/capture_s163_p3_dungeon.md`: sound 6 once from `0x1f5cc` before each window) and watched in the Client (8.7) |
  | `0x1f6dc`, `0x1faf0`, `0x20404` disk and I → III transfer | "Insert your Phantasie III disk.", "This [original text]I Disk.", the two drive-light warnings, "To use these characters, select the 'Start this turn in Pendragon' option.", "Insert a Phantasie I Disk now.", "Insert the Phantasie III Disk on to which these characters will be added.", "[original text]" (`0x20978`), "This [original text] disk!" | no remaster path (the importer has no disks) |

  **The town dispatch key by key (s158, `[C]` static).** `0xb1cc` upper-cases the key and jumps through the word table
  `0xbb52` (base `0xbbd2`, keys `+` … `_`): `+` / `=` Add `0xb264`, `-` / `_` Drop `0xb2fe`, `/` / `?` Info `0xb25a`, A
  `0xb396`, B `0xb458`, C `0xb802`, D `0xb358`, E `0xbabc`, G (Trade) `0xb996`, I `0xb5b4`, L `0xb5e2`, M `0xb69e`, N
  `0xb48c`, O (Load) `0xb1f0`, P (Purge) `0xb4c2`, R `0xb6ec`, S `0xba9a`, T `0xb8da`, U `0xba18`, V `0xb6cc`, X `0xb58a`.
  Window 7 per key: **"[original text]"** when the party is empty (the word `0x240e0` = 0: D, A, I, L, M, V,
  C, T, X; Drop tests `0x240dc` ≤ 0) and, for D, B, G and U, also while the defeat flag `0x2411c` is set; **"The character
  roster is empty."** when the roster word `0x240da` < 4 (Add) or ≤ 3 (Purge, Rename, Examine) (the roster's first
  three records are the elementals `[~]`); "The party is fully assembled." / "You can only add an elemental." when
  `0x240dc` = 7 (Add); "[original text]" when `0x240da` ≥ 25 (New); "The local armoury is out of stock."
  when `0x240ec` ≤ 0 (A); "…unable to do this." for a picked member whose status (`+0x2c`) is 0 or 3 (A, L, C, T), whose
  class / race word `+0xa8` is 17 (L, T) or whose `+0xf6` > 1 (C); "…knows no spells." (C, no spell, `+0x56` = 0); "…not
  carrying anything." (G, U, `+0x26` = 0); the Elemental refusals of P, R, E (the roster record's word at `0x26aac` + 262 · n = 16); a party member for P (`0x26a28` + 262 · n ≠ 0). O,
  S, D and P's pick ask through `0xaae2` (window 1 with sound 36): "Are you sure you want to load an old\rgame over this
  current one?" (`0x2316c`), "… save this\rgame over your previously saved one?" (`0x23088`), "… redistribute?"
  (`0xbbd8`), "… purge\rthis character permanently?" (`0x230cc`). **E examines any roster member** (`0x12422(6)`, the
  roster list, then `0xd796`); V (View Party) is `0xd684`. The meanings of `0x240dc`, `0x240e0` and `0x240e2` beyond
  "party empty" are not read `[~]`. **DOS** `guild_menu` root `0xac21` (items 0 Learn, 1 Train, 2 Add, 3 New, 4 Drop, 5
  Rename, 6 Examine, 7 Purge; table at CS `0x2d5f`, CS base img `0x8340`) has the same tests and strings: S405 for an
  empty party (Learn / Train: `FAR:0xb614` = 0 or `0xfc9a` set; Drop: `0xcc8` ≤ 0), S408 for a roster word `0x97c0` < 4 /
  ≤ 3 (Examine also needs `0x97c2` = 0), S404 / S418 at a party word of 7 (picked by `0xb612`), S409 at 20 (New),
  S417 for Dead / Out (status 0 / 3), race 17 and, for Train only, level ≥ 21; Examine (`0x999f(6)`, message 395 "Who shall
  be examined?") lists the roster too and opens `0x86f0`. So DOS and the Amiga agree; the roster sizes differ (20 / 25).
  DOS asks O / S / D / P's questions through root `0x1820`(text, 2) with "No" lit (`dungeon-events.md` 2.2, town.md
  "The questions"); the Amiga's window 1 has no highlight and Return = OK. **Built s160 (user s159), every III layout:**
  one Client question (`P3ConfirmQuestion`, `Game1.P3Confirm.cs`) with the shared lines `p3bar.confirm_load` / `_save` /
  `_distribute` / `_purge` (DOS's and the Amiga's text, byte-equal in both programs), sound 36 first, shown through the
  prompt path: window 1 when AMIGA SCREENS is on and the pack has window 1 and the OK / CANCEL art, else DOS's Yes / No
  prompt ("No" lit, Esc = No). Asked before: Load (the bar's File › Load; the game menu / F9 in a town or the Guild),
  Save (the bar's File › Save, the street's S, the DOS row's Option › Save game, the game menu / F5 in a town or the
  Guild), Distribute (D, the bar's Options › Distribute, the DOS row's Inn list; after the empty-party refusal; not the
  town-entry hand-out, which DOS's `0x0000(1)` runs without one) and a Purge pick (after Core's refusals,
  `GameSession.PurgeRefusal`; the list steps aside while it asks, `GuildPickDialog.PurgeAsked`). E (Examine any roster
  member) is the Guild's I key outside the bar (`GameAction.GuildExamine`, DOS's label "Inspect"; E stays Exit Guild).
  Watched s160 on Xvfb (`:187`, III test party): AMIGA SCREENS on: Guild I → the roster list → a sheet; P → SWAT →
  window 1 with S407, CANCEL → the list; the street's S → window 1 (the band empty, as s158), OK → "Saved."; D → window 1,
  OK → the hand-out screen. AMIGA SCREENS off (the DOS row): P → SWAT → "Yes >No", Return → the list, SWAT kept; S, F9
  and D → the prompt with "No" lit; Esc on Load's closed it; Y on Distribute's opened the hand-out
  (`parity/shots/p3-remaster-confirm-{purge,distribute}-window1.png`, `…-confirm-{purge,save,load,distribute}-dos.png`,
  `p3-remaster-guild-examine-key.png`).
  **`0x1f572(text, member)`** copies the member's name (record `0x2559a` + 262 · member) into `0x24632`, appends the
  text and plays **sound 6** (`0xb126(6)` at `0x1f5c8`). Its four callers in `0x1c36c`: " [original text]"
  (`0x1c554`) and " [original text]" (`0x1c6bc`), which then ask in window 1, and " picks the lock." (`0x1c668`)
  / " [original text]" (`0x1c74e`), which go to window 7. So sound 6 goes with every one of these four lines.
  **Built (s135):** pack `keep_windows.message.text_ids` lists the remaster lines above that have an equivalent;
  `OpenPhantasie.Client.AmigaMessageBox` + `Game1.OfferAmigaMessage` show such a line (a dialog's outcome or a town choice's
  refusal / the inn's line) in window 7 over the screen under AMIGA SCREENS, the band / line left empty while it shows;
  any key or a click on OK closes it and that key goes no further (`0x3ba0` with key 0); only OK takes a click. Off: the
  remaster's line. Seen on Xvfb (`:121`, III test party, the new-game rule `p3.purge` = `dos`): Guild › Purge › a party
  member → "YOU CANNOT PURGE A PARTY MEMBER." in window 7 over the purge list
  (`parity/shots/p3-remaster-window7-guild-purge-refusal.png`), Return closed it with the list unchanged
  (`…-closed-key-taken.png`); AMIGA SCREENS off: the remaster's line (`…-off-remaster-line.png`). The text is the
  remaster's line in III's table. **III's own wording (s156):** where Core refuses on DOS III's own condition, III's
  string table now says it in DOS III's words (MAIN.EXE strings, which the Amiga's callers above quote too):
  `core.guild.party_is_full` (Add with six characters, ovl2 `0x3a0a`) S411 "[original text]";
  `core.guild.already_in_party` (Add of a member already in the party, `0x3a0a`) S412 "[original text]";
  `core.guild.roster_full` (New with the roster full, guild menu `0xac21`, Amiga `0xb4b6`) S409 "The character roster is
  full."; `core.guild.learn_no_more` (Learn Spells with nothing more to learn now, ovl2 `0x1a6c`, Amiga `0x1058a`: the
  known count ≥ the allowed count or no learnable spell) S429 "[original text]", now also a window-7
  line (`keep_windows.message.text_ids`). s158 (the dispatch above): `guild.no_one_to_drop` and `guild.add_members_to_a_party_first` (Drop, Learn, Training
  on an empty party) read S405 "[original text]", Add on an empty roster `guild.roster_empty` S408 "The
  character roster is empty.", `core.p3.cannot_train` S417, `core.guild.learn_cant_afford` the DOS / Amiga text; all go
  to window 7. Left with the remaster's wording because the condition does not match one-for-one `[~]`: "No one to add."
  for a roster whose members are all in the party (III lists them and refuses each pick; seen s163 on the band with six
  members and an elemental, where the Amiga's Add says "The party is fully assembled." or "You can only add an
  elemental." by the word `0x240e2`, not read), Use Item's lines. s163: the out-of-fight caster's "…magic is too low!"
  watched in window 7. s165: `0xf8f6` tests the magic before the list and the cost before the target, and so does the
  remaster now (watched: Folnar with 2 MP picks Healing IV → window 7 at once, no target asked;
  `parity/shots/p3-remaster-s165-cast-refused-before-target.png`).
- **Button palette `[C]` static (s128).** The spell / heal / rank windows sit on the orders screen. `0x18c48` sets
  black (`0x11ae`), draws, then sets `0x2437e` and `0x24070` = 1 (`0x19058`), so `0x3904` keeps that palette.
  `0x2437e` is written only when a `.cmp` / `.set` / sheet is loaded (`0x123c` / `0x27d8` copy the file's 16 colours):
  at boot by the sheet loop `0x7f3c` (the last sheet's), on a town entry by `w4` and then `w3` (`0x777a` / `0x778c`), by
  Scroll IV's `D/s6` (`0x1f7f8`), the defeat's `w2` (`0x1dc88`) and the reward scene's `w0` / `w1` (`0x1ef1e`). `w3`,
  `w4` and `s6` hold the map palette word for word (000 310 421 432 543 154 252 131 762 741 510 134 136 120 555 777).
  `w0`–`w2` are heros-like palettes (index 8 green `042`). The reward scene ends in town 1 (a town entry follows). So in
  play the windows show in the map palette, which is the palette the button art is cut in, and the remaster draws them
  unchanged. **Neither exception happens (s156, `[C]` static):** the main loop (`0x775e`) starts each pass with the
  town entry when the word `0x240ca` ≠ 0, and `0x240ca` is written only at boot (`0x75f0`, 1) and by the load
  (`0x1fd5e`, from the save block `0x24570`, which the save `0x1fb66` fills from `0x240ca`; the save `0x1faf0` is called
  only by the town's S (`0xbab0`) and the I → III transfer at boot (`0x2098c`)), so a new or continued game opens with a
  town entry (w4, w3) before any fight. The party defeat runs from the overworld loop (`0x7b2e`: `0x2411c` ≠ 0 →
  `0x7d3c` `jsr 0x1dc16` → `bra 0x775e`), and the walk loop skips the random encounter (`0x7c4a`) while the flag is
  set, so the scene's `w2` is replaced by the next town's w4 / w3 before any fight. The orders screen and its windows
  therefore always show in the map palette. The field (`0x198a2`: `0x2445e`, flag 1) has no button window of its own
  `[~]`.

### 8.5 The fight's menu bar: PARTY OPTIONS on the field (s119, s120) `[C]` static

- **The wait** (fight loop `0x14ed2`–`0x14f58`): between rounds the field stays up as the last round left it (monster
  groups, portraits; no line) under the game's **own** menu bar (not Intuition's): while the mouse button is down and the
  pointer is above y 11 (`0x24078` < 11) `0x184a(rastport, 0x2397c)` pulls a menu down and returns the item's key from
  the table `0x2189c` (15 bytes a menu; the fight's menus are rows 6–8 by the base `0x24094`; byte 0 of a row is the
  title's, so a release on the title gives 0); otherwise a key (`0xcc4` / `0xcdc`). `0x19928` takes either (upper-cased,
  `0x1e4a`). The palette is the combat one: `0x198a2` (the field's set-up) draws the bar (`0x3894`), loads `0x2445e` and
  sets `0x24070` = 1, and nothing in the wait changes it.
- **Menus** (list `0x2397c` → `0x23958` → `0x23934`, link +32; struct: +0 title column, +2 title length + 1, +4 = 1 (may
  invert), +6 / +8 text colours 0 / 15, +10 the title (long), +14 box column, +16 box width in cells, +18 box height in
  pixels, +28 the item table): File "?) Info " (key 0 in the fight: nothing); Options "F) Fight", "T) Threaten ",
  "G) Greetings ", "[original text] ", "R) Run away ", "[original text]", "H) Party health"; Speed "+)  Fast",
  "=)  Normal ", "-)  Slow" (the trailing blanks are in the strings and count in the widths).
- **Places** `0x12d6(list)` (s120; run once a list at start-up, `0x7700`–`0x772e`, for the lists `0x238a4`, `0x23910` and
  `0x2397c`, guarded by `0x24080`): the first title at column 0, each next one at the last one's column + its length + 2
  (the fight: File 0, Options 6, Speed 15; the town list `0x238a4`: File 0, Guild 6, Options 13 — its Options has 11 items with a "------------- " line, so the town bar on the s116 capture is this code too (§8 corrected s120); `0x23910` = File, Options (Cast Spell, Examine Member, Party Status, Use Item), Speed: the bar outside the towns (s135 `[C]` static): drawn (`0x3894`) and pulled (`0x184a`) in the main loop `0x736c` (`0x7be2` / `0x7cc8`: the overworld's walk loop, which also runs the random encounter `0x149d4` and the step test `0x9c6a`; s136 `[C]`: `0x24092` is a bar-drawn flag, drawn once and set at `0x7bf0`, cleared at `0x76e8` / `0x77e0` / `0x7ab8` and by the dungeon `0x1bcac` / an event fight `0x1db0e`; the town list `0x238a4` uses it too, `0x78c2` / `0x78de`) and in the dungeon routine `0x1bb58` (`0x1bce0` / `0x1bf92`), redrawn after an event fight (`0x1db20`) and by `0x82f8`. Items: File "?) Info " (window 0 `0x23228`: the credits text `0x2308c`, set at `0x7450`, one OK button), Options "C) Cast Spell", "[original text] ", "P) Party Status" (window 22 `0x236a0`), "U) Use Item", Speed as the fight's; their keys go to `0x83e0` (`?` and `/` Info `0x86e6`, C `0x8442`, E `0x85a6`, P `0x8832`, U `0x8522`, + = − `0x870e`, the number pad and arrows move). Built s136: `AmigaOutsideBar` / `UiOverlays.DrawOutsideMenuBar` under AMIGA SCREENS (status line off) over rows 0–10, colours 15 / 0 of the map or dungeon palette; items open the remaster's cast / inspect / health / use-potion dialogs, Speed sets COMBAT SPEED, + = − too; s140: Info's credits window built, ? / P bound, E left east (D3-070, decided s141: the manual), the dungeon's bar seen live (8.9). The town bar is the Amiga's since s156 (8.9 "The town bar, built"); every III bar takes the Amiga's press-drag-release and
  click, click (8.9 "Press-drag-release"). Watched s136 on Xvfb `:131` (a held press, `mousedown` / 0.15 s / `mouseup`; an instant `xdotool click` did not register): Options pulled down at (48, 12)–(207, 51), the hovered row inverted, Cast Spell → the cast dialog, Speed opened on the ticked Normal, Slow chosen and saved and ticked, a second title click closing it); the box column = the title's rounded down
  to even; the box width = each item's length + 1 rounded up to even, the widest; the height = (items + 1) · 8.
  - **The bar** `0x3894`: `0x648(0, 0, 319, 10)` in colour +8 = 15, then each title by `0x1c5c` (JAM2, pen 0 on 15; the open
    one 15 on 0) at Move((column + 1) · 8, 8), i.e. x 8 / 56 / 128 (the town 8 / 56 / 112), the text's top at y 2.
  - **Pulled down** `0x184a`: the area is saved (`0x2e60`: column / 2 words, y 1, height + 11 rows), the title inverted
    (`0x2b1c`(column, 1, length + 2, 10): XOR of all four planes over cells column … column + length + 1, rows 1–10:
    File x 0–47, Options 48–119, Speed 120–175), the box `0x28de`(box column, 12, width, height): **solid colour 15, no
    frame**, from y 12; the items by `0x171a` through `0x854` at text column box + 1, rows 2, 3, … (y 16 + 8 i), pen 0 on
    15. The pointer over item row r (2 ≤ r < height / 8 + 1, inside the box's columns) inverts (`0x2b1c`) the box's full
    width × 8 rows at y 8 r. The fight's boxes: File (0, 12)–(79, 27), Options (48, 12)–(191, 75), Speed (112, 12)–(207,
    43); the town's Options (96, 12)–(223, 107) (s116 measured y 11: one row off; the rest agreed).
  - **Speed's tick** `0x870e(key)`: '*' over character 3 of the setting in force ("+) *Fast") and ' ' over the others',
    in both Speed item tables (`0x237d8`… and `0x23810`…), and the Speed word `0x240f6` = 1 / 2 / 3; start-up calls
    `0x870e('=')` (`0x76c0`), so Normal is ticked first.
  - Colours: combat palette 15 (pack #FFFFFF; hardware `0xEEE`, the s116 capture's #EEEEEE) and 0 (#000000; the capture's
    #242424 was the emulator's scaling).
- **Keys** (`0x19928`): F → the orders screen (`0x188e4`, §8.3); T `0x19a38`, G `0x1a5e4`, B `0x19a88`, R `0x19baa` (the
  party options; Core's rules); M `0x1a264(1)`, H `0xadac` (below); + = − `0x870e`; `Q` (not on a menu) tests a flag
  `0x240fc`. **The flag is never set** (s135 `[C]` static): its only write is the clear at start-up (`0x7370`); its other
  five uses only test it (Q in the fight `0x1994c`, key `1` in the outside-town keys `0x840c` (s140, 8.9), the guild's Examine `0xbaee`, two dungeon
  step keys `0x1c030` / `0x1c042`), no other code or data holds its address. So Q and those branches do nothing in the
  shipped game (a dead test switch; the remaster has no Q). M, H and the speed keys do not use the round.
- **M: "[original text]"** `0x1a264(1)` (s120): window 23 (`0x236d4`) with its +8 / +12 set to 128 / 191, its frame
  colours +0 / +2 (4 / 3) to 2 / 7 and its button count +26 to 0, all put back after; drawn by `0x3904` (fill +4 = 0; the
  key +46 = −1, so no button wait), the area under it saved. "[original text]" at cell (7, 18) through `0x854`, pen
  1 on 0. Then `0x1a44c(rank, count, 1)` for rank 2 (group struct `0x2481e`) and then rank 1 (`0x24794`), each only when
  its count (+2) > 0 and its id (+0) ≥ 0: "Rank  :" at column 7 on row 22 − rank (rank 2 row 20, rank 1 row 21), the
  rank's digit over column 12 (`0xab1c`, left-aligned) so it reads "Rank 2:", the count at column 15, the name at 17
  (`0x1a4ea`: the monster name `0x241dc` + 38 · id, trailing blanks cut, "(s)" put right after). All colour 1. Then
  `0x1e8c4(1)` waits for any key or mouse button, and `0x274a` puts back what was under the window.
- **H: the party's health** `0xadac` (s120): window 24 (`0x23708`): box (24, 16)–(286, 199), frame 2 / 7, fill 0, no
  buttons; its title (+22, set at start-up `0x7482` to `0x23178` "Name:\rStatus:\rHits:\rMagic:\rHead:\rLt Arm:\rRt
  Arm:\rTorso:\rLt Leg:\rRt Leg:"; `0x854` starts a new row at each `\r`, same column) at cell (4, 3), pen 1 on 0. One
  column a member, slots 0 … `0x240e0` − 1 less one when an elemental is summoned (`0x24124`), by `0xae2c(slot)`: slots
  0–2 at columns 12 / 20 / 28 row 3, slots 3–5 the same columns row 14; slot 3 first prints the labels again at (4, 14)
  colour 1. A member (record `0x2559a` + 262 · slot): the name (+0) colour 4; row + 1 the status `0x22eec[+0x2c]` (Dead,
  Okay, Asleep, Out, Down) colour 15; rows + 2 / + 3 HP / MP: the current (+0x22 / +0x1e) right-aligned to column + 2
  (`0xabec`: 10–99 one cell left, 100–999 two), ":\r:" (`0x2317c`) at column + 3 on row + 2, the most (+0x20 / +0x1c) at
  + 4 (`0xab1c`), colour 15; rows + 4 … + 9 the six areas +0xf2 … +0xfc (Head, Lt Arm, Rt Arm, Torso, Lt Leg, Rt Leg; not
  swapped as the body figure's are, §2.1) as `0x22edc` … `0x22ee8` "Okay" / "Injured" / "Broken" / "Gone" in colour
  15 / 14 / 13 / 12 (a value above 3 keeps the previous area's text and colour: not reached). Then `0x1e8c4(1)` and
  `0x274a` as M. III's manuals (US, Australian) say nothing of either window: no conflict.
- **Remaster** (AMIGA SCREENS; `CombatPresenter.HandleFieldMenuKey`, `UiOverlays.DrawFieldMenuScreen`): the field and
  the bar as above, places from the pack (`keep_windows.menu_bars.fight`, s156: `0x12d6`'s places written out, see "The
  bars as pack data" below; the boxes start at y 12 with no outline, the items at y 16 + 8 i with the full-width
  highlight, the title inversion over its cells, the tick a '*' in the label; bar colours from the combat palette, topaz
  8); the letters act with the bar open or closed; the mouse as the Amiga (press on a title, drag, release on an item,
  s156) or click, click (a click on a title pulls its menu down, a second click closes it, a click on an item takes it);
  the remaster's arrows pull Options down and move the highlight (Left / Right: the next menu), Return / Space take it, Esc
  closes it. Speed is the game's COMBAT SPEED option (Options tab), saved when changed here; + is the numeric pad's (Shift
  is the remaster's auto-fight key), = and − the main row's. **M and H** (s120) open the Amiga's windows (pack
  `keep_windows.monster_names` / `.party_health`: boxes, text places, combat-palette colour indices; texts in the string
  table) over the field: any key or a click closes them and is used up (F there does not fight). The count and name M shows
  are the orders screen's (living monsters, §8.3), which is the Amiga's (s136 `[C]` static): the group's +2 is set at
  the group's build (`0x1aeb6`: `rnd` of the monster's number byte +14), **decremented at each death** by the " dies!"
  routine `0x1833e` (`0x185ac`: `subq #1` after clearing the member's HP word and redrawing the row `0x1b150`), and
  cleared with a whole rank (`0x15a0e` / `0x15a4a` on the flags `0x2412a` / `0x2412c`, both set by "The monsters begin
  to dissolve", `0x16e94`); its only other absolute uses are tests. The remaster's names / health box stays for packs without these windows.
  Watched s119 on Xvfb (`:79`): the bar, Options and Speed pulled down by keys and by the mouse, Slow chosen by a click
  and saved, = back to Normal, F → the orders screen → Fight! → the round on the field → the bar again. Watched s120 on
  Xvfb (`:85`): M (Rank 2: 2 Wild Cat(s), Rank 1: 3 Giant Bee(s)), H (six members, both label sets), a key and a click
  closing them, Options and Speed (Normal starred) at the code's places.
- **The bars as pack data (s156, `[C]` static).** `0x12d6` runs once a list at start-up and only writes the places into the
  structs (the shipped image holds zeros there), so the places are a fixed function of the strings; the pack now holds
  them written out (`keep_windows.menu_bars.fight` / `.outside` / `.town`: title x, the inverted span, the box, item x / y /
  height, pens, `frame: false`, topaz 8) and the Client draws every III bar from it (`UiOverlays.DrawAmigaMenuBar`);
  `UiOverlays.RuleMenuBar` keeps the rule as the fallback for a pack without them and as the test oracle
  (`P3MenuBarsTests`). The lists, read from the image (s156): town `0x238a4` File "?) Info ", "O) Load", "S) Save"; Guild
  "+) Add Member", "-) Drop Member", "[original text]", "N) New Member", "P) Purge Member", "[original text]",
  "----------------- " (17 dashes and a blank), "L) Learn Spells", "T) Training"; Options as §8 with the rule
  "------------- " (13 dashes and a blank). The key table `0x2189c`, 15 bytes a menu (byte 0 = the title's): town rows
  0–2 `?OS` / `+-ENPR·LT` / `ABIMX·CDGUV`, outside 3–5 `?` / `CEPU` / `+=-`, fight 6–8 (none) / `FTGBRMH` / `+=-`; a rule
  row's key is 0, so a release there does nothing. The bars are drawn in topaz 8 since s156 (the remaster's font before).

### 8.6 Flashes, monster rows and the fight-find window in a running game (s120, FS-UAE)

Party: the TOSEC dump's own played save (`gamesave.dat`: strider, sprite, elrod, jonpaul, ninja 3, frodo, all level 21,
in Pendragon). "Continue a previously saved game" loads it. Speed Slow (lines 60 ticks). The fights were near
Pendragon. The round was recorded with ffmpeg x11grab at 50 fps; times below come from that capture. Shots:
`parity/shots/p3-amiga-flash-1-cast-line.png` … `-5-next-monster.png`, `p3-amiga-keep-fight-find.png`,
`p3-amiga-fight-reward-before-keep.png`.

- **Party spell flash, order `[C]` seen** (built s121, §8.1). The wizard cast Mindblast IV on 1 Giant Bee + 2 Wild Cats, with the rest of the
  party parrying. The caster's attack frame shows for 0.84 s (21 ticks doubled at Slow). Then
  "elrod  casts Mindblast IV" (blue, 1.22 s). Then, for each monster in turn: the **flash on that monster** (3 frames ×
  0.14 s, starting the frame after the cast line is cleared) → "Giant Bee takes 56 damage!" → "Giant Bee dies!" (the
  monster is already gone from its row on the frame where "dies!" prints) → the next monster's flash → … So the flash is
  **mid-round and per target**, interleaved with that target's lines, not after the round. The "takes" and "dies" lines
  are **blue**, the party's colour (the caster's side), which matches the remaster's `[~]` for lines that are not a swing.
- **Resisted targets.** Against 3 Gremlins, Mindblast IV hit one (flash, "takes 13 damage!", "dies!"). The other two got
  **no flash and no line at all**: the round went straight on to the next actor.
- **Monster lines.** "Gremlin hits!" and then "strider takes 12 damage!" are both **red** (the monster's side).
- **Monster special on the party:** not reached in s120; seen s125 (§8.8).
- **Fight-find keep window `[C]` seen** (`p3-amiga-keep-fight-find.png`): after the reward box ("Each survivor gets the
  following / Gold: 112  Experience: 253", OK), window 23 shows "strider finds Axe" / "[original text]". The text
  is orange (colour 9), with OK and CANCEL. Box, text cells and buttons fall exactly where §2.3 places them (box
  (16, 64)–(300, 128), name at (4, 9), question at (6, 11), OK x 72, CANCEL x 176, y 104). The Gremlins' second item
  followed in its own window, under another finder ("elrod  finds Leather"); `c` = CANCEL closed it with no message.
  So each found item gets its own window, after the reward box. The dungeon "Keep …?" (window 1): seen s125, §8.7.
- **Rank 2 moving up.** In a 5 + 2 Killer Bee fight, a lone bee was later drawn at the rank-1 place (y 78) once the
  top row was empty. **s156 `[C]` static:** at the round's end, while the fight goes on (`0x240d8`), `0x15b22` swaps the
  rank structs (`0x1b062`: the 69 words of `0x24794` ↔ `0x2481e`, their counters `0x247da` / `0x24864`, `0x24130` /
  `0x24132`, `0x2411e` / `0x24120`, then clears (24, 14)–(300, 130) and redraws both ranks at their places) when rank 1's
  count ≤ 0 and rank 2's > 0, else when `0x1a218(rank 1)` < `0x1a218(rank 2)` (the group's `+42` summed over its slots
  whose word `+4 + 2i` is odd) and `rnd(100)` ≤ 30: DOS's rule (`combat-round.md` step 8, `0x113e6`), which Core runs
  (`P3RoundResolver`, `RankSwapChance`); the Amiga skips it once the fight is over (DOS also tests the strength swap then).
  The remaster's field draws each group at its rank index's place (`FieldGroupY`), so the moved group shows at y 78 from
  the next frame, as the Amiga's redraw.
- **Other notes.** The spell window's `m` key did not page (a click on MORE did); the keypress may simply have been lost
  `[~]`. A Thief's Attack asks "[original text]" and a Ranger's does not (DOS's rule).

**Wolf / Mud Elemental pictures:** not reached in s120; seen s125 (§8.8).

**Scandor rows after a plane:** s125 found that the boot sets them (§4, §8.7); no plane visit is needed.

### 8.7 The dungeon "Keep …?" window and Scandor's edges (s125, FS-UAE)

Same save as §8.6 (master ADF, "Continue a previously saved game"), FS-UAE on Xvfb with `zoom = 640x400` (the whole
320 × 200 screen at 3× in the 960 × 600 window; the Amiga pointer then moves 1 px per 2 host px). In a dungeon the
number pad moves the party (8 / 2 / 4 / 6; the diagonals did nothing); on the overworld a click on the next cell.

- **Dungeon "Keep …?" `[C]` seen** (`parity/shots/p3-amiga-keep-dungeon.png`, `-ok-held.png`). The Pendragon archives
  (dungeon 5, Scandor (25, 39)): "[original text]" CANCEL (the original map), the start on the exit
  square asks "[original text]" (`p3-amiga-dungeon-exit-question.png`), then (11, 0) → (11, 4) → room →
  door (8, 6) → (6, 6) → (6, 10) → door (5, 10) → (2, 10), event cell 0xdd (op 6, item 122): **"Keep Scroll II?"**.
  All three questions are window 1. Box (0, 72)–(319, 140), outer frame colour 4 from (0, 72) to x 318 / y 139, inner
  colour 3 one pixel in to x 319 / y 140, fill 0; the text's ink box (8, 80)–(126, 87) = text cell (1, 10), colour 9;
  OK (72, 115)–(127, 132), CANCEL (192, 115)–(247, 132): the remaster's `keep_windows.dungeon` exactly. The button art
  matches `buttons/ok-0.png` pixel for pixel **except the label: (238, 238, 0) = the dungeon palette's index 8**
  (#FFFF00 in the pack's scale), not the map's #FFDB49; held, the label is (0, 204, 238) = dungeon index 5 (#00DBFF),
  not #24B692. So the window shows in the dungeon palette: `0x3904` loads the map palette only while `0x24070` = 0,
  and the dungeon (faded to `0x244de` at `0x1bd72`) leaves it 1. Indices 0, 3, 4, 9 are equal in the two palettes.
  OK closed the window with no message. No copy-protection question came up on the event square.
  Remaster (s125): `keep_windows.button_palette` / `dungeon_palette`; `UiOverlays.DrawKeepWindow(inDungeon)` shows the
  buttons through `AssetCatalog.GetInPalette` while `GameSession.Dungeon` is set (both keep windows; a dungeon fight's
  find by the code, not seen).
- **The dungeon entry / exit questions are window 1 `[C]` static (s128).** `0x1d77e(text)` puts the text in window 1's
  title (`0x23272`) and draws window 1 (`0x2325c`, `0x3904`). Its callers: the dungeon routine `0x1bb58` asks
  "[original text]" (`0x1bc18`), "[original text]" (`0x1bda4`) and "Would you like to save this
  map?" (`0x1bdc4`); the dungeon step asks "[original text]" again (`0x1c1a6`), "<name> hears monsters.
  Continue?" (`0x1c560`) and "<name> [original text]" (`0x1c6c8`). Palette: the load question comes before
  `0x1bb58` fades to black (`0x1bcbe`) and to the dungeon palette (`0x1bd72`), so it shows in the overworld's palette
  `0x24010` (the map palette: labels `#FFDB49`); the exit and save-map questions come before the fade out at `0x1be42`,
  in the dungeon palette (labels `#FFFF00`, as seen s125). Remaster (s128): `Game1.P3WindowFor` routes the load, exit and
  save-map prompts to `keep_windows.dungeon` (OK = Yes, CANCEL = No; Return = OK as in every Amiga window, where DOS
  lights No), the palette by `GameSession.Dungeon` (null at the load question). Watched on Xvfb
  (`parity/shots/p3-remaster-window1-{exit-question,save-map,load-dungeon}.png`). Since s134 the dungeon step's "hears
  monsters. Continue?" and "[original text]" (Core's III Yes / No event questions) are window 1 too, in the
  dungeon palette; I's DISARM / LEAVE prompt keeps the bar. **Rows (s162 `[C]` seen):** the title is topaz 8 (the s125 /
  s128 shots: "Keep Scroll II?" 960 / 960, "[original text]" 1656 / 1664 pixels equal the ROM glyphs) and a
  "\r" goes one 8-px row down: the town's save question at (8, 80) / (8, 88), live (`../working/capture_s162_p3_windows.md`,
  `parity/shots/p3-amiga-window1-save-question.png`). Remaster: pack `keep_windows.dungeon.font` = `amiga-topaz-8`,
  `UiOverlays.KeepWindowRows` (rows the font's line height apart, wrapped at a word in the font's widths; a window without
  a font keeps the remaster's 10 px). **Guild Add's move question (s162 `[C]` seen)** is window 1 too ("It will take this
  character two\rmonths to make the journey here.", `0xd24c` → `0xaae2`), over the street with the roster list closed;
  CANCEL brings the list back (`p3-amiga-window1-guild-add-two-months.png`). Remaster: III's text (S445, string
  `guild.move_journey_question`; before s162 III showed I's three-row text) in both looks; under AMIGA SCREENS window 1
  over the Guild with the list stepped aside (`GuildAddDialog.AmigaWindow`: Return / O / Y = OK, C / N / Esc = CANCEL),
  else the Yes / No panel with "Yes" lit. OK = Yes `[C]` (s134): after `0x1c560` OK sets the fight
  (`0x1c56a` → `0x1da5c`), CANCEL skips it; after `0x1c6c8` OK opens "who disarms" (window 6, `0x127ce(92)`). No answer
  is lit, Return = OK, where DOS lights No on "hears monsters" (D3-068, PENDING).
  **s163, seen live on the Amiga** (`../working/capture_s163_p3_dungeon.md`, cells poked in the archives): "strider spots a
  trap. Disarm it?" and "strider [original text]" in window 1 on one row (ink index 9 from (8, 80), labels the
  dungeon's yellow), sound 6 before each; OK on the trap → **window 6 "[original text]"**: the heal window's box
  (8, 44)–(312, 160), the title at cell (5, 7) in the dungeon's colour 8, one member a row from y 72 (name, race, level,
  class, status; the cursor's row in 8, the others the dungeon's 4), OK / CANCEL at y 135; the `2` key moved the cursor
  (the host's Down did nothing `[~]`). **Static (`0x1c6d6`…`0x1c71a`, DOS ovl4 `0x1189`…`0x11bc` the same):** the picker
  asks again while the pick's status is 0 (Dead) or 3 (Out); its CANCEL (−1) leaves the trap as CANCEL on the question
  does (nothing sprung, the cell kept); a pick rolls Disarm (`0x1b91e(5, member)`): success sets the cell to 7 and shows
  "<name> [original text]" (window 7) with the party back where it came from (step result −1 at `0x1c7c2`, as No; seen:
  the next step went onto the disarmed cell), failure springs it (message 0x60, the Damage Report). Built s163: Core
  re-asks on a Dead / Out pick, takes the picker's cancel (the prompt's escape choice, one past the members) as No and
  steps the party back after a disarm (before s163 a Dead / Out pick sprang the trap, there was no cancel and a disarm
  left the party on the cell); the Client draws the question as window 6 under AMIGA SCREENS
  (`AmigaDisarmWindow`, pack `keep_windows.heal`, the dungeon palette through `DrawAmigaWindow(inDungeon)`; Up / Down,
  Return / O, C / Esc = CANCEL). **The picker's keys (s165 `[C]` static, `0x127ce` loop `0x12922`…`0x12996`):**
  `0x3ba0` returns 1 for Return / O / OK (the cursor's member), 0 for C / CANCEL (−1), `'2'` moves the cursor down and
  `'8'` up (wrapping, `0x128ba`), anything else (the other digits too) does nothing. A press on a member row (`0x17a6`,
  the band `0x1776` set at `0x12850`: rows 9…, columns 2–37) on another row stores that row in `0x2432e` and posts `'8'`,
  which the loop reads as "move there" (`0x12952`); a press on the lit row posts nothing. Built s165: 8 / 2 (and the
  number pad's) move the cursor, the other digits do nothing, a click on a row only moves the cursor
  (`AmigaPromptRowTag`, `PromptMenu.MoveTo`), OK / Return takes it; watched (`p3-remaster-s165-window6-row-press-moves-cursor.png`:
  3 did nothing, 2 2 moved to LEFTY, a click on SWAT's row moved the cursor there, a second click nothing, O took SWAT).
  The combat heal window (`0x127ce(33)`, `CombatPresenter.AmigaPickRow`) already moved the cursor on a press. **Watched in the Client s163** (Xvfb `:157`, III test
  party, `--p1-dungeon 5 23 7` / `5 3 15` / `5 15 22`: the archives' trap 2, door 0xc5 and locked door 0xc6): window 1
  "LEFTY [original text]" → OK → window 6 → O on LEFTY → window 7 "LEFTY [original text]" (a click on OK
  closed it); CANCEL clicked in window 6 left the trap unsprung; "LEFTY [original text]" in window 1, CANCEL
  clicked (the party back), the step again, OK clicked → the fight; "LEFTY picks the lock." in window 7
  (`parity/shots/p3-remaster-s163-window1-spots-trap.png`, `-window6-who-disarms.png`, `-window7-disarms-the-trap.png`,
  `-window1-hears-monsters.png`, `-window7-picks-the-lock.png`).
  **The Damage Report (s165 `[C]` static, `0x1d4ea`):** every party-damage call `0x1d24a(die, add, kind)` (a sprung trap
  `0x1c7b6` (die, 0, 0), op 16 `0x1cc78` (a, 1, 0), ops 1–4 `0x1cb00` (a, 1, 2), lava `0xa676` (13, 1, 3), the swim
  `0xa6b8` (0, 1, 1)) rolls each member, then shows window 6 (struct `0x23360`, box (8, 44)–(312, 160)) with its button
  count `+26` set to 1 (OK only at (56, 135); 2 again after), sound 42 (`0x1d516`), "Damage Report" (`0x1d74c`) by
  `0x854` at cell (13, 7) colour 9, and a row a member (`0x240e0`, party order) from row 9: the name at column 2 colour
  15; at column 10 a damage of 0 prints the kind's zero line from the dungeon message table `0x23a62` (kind 0 → 12
  "[original text]", 1 → 14 "swims.", 2 / 3 → 16 "[original text]"), else the next message ("takes " / "sinks for "),
  the number (`0x1d8a`) and " damage." colour 15 or, past the member's HP (the routine adds 500), " [original text]"
  colour 9; a member already at HP ≤ 0 "is dead." colour 10. Then `0x3ba0` with key 0 (any key or OK), the window's
  ground put back (`0x274a`), and only then the wiped-party test (`0x1d4b4` sets `0x2411c`, `0x1dc16`). Built: Core
  records the report (`GameSession.DamageReport`, `P3DamageReport`: every member a row, the dead too) from
  `Phantasie3Campaign.DamageParty` (trap / op 16 "[original text]", ops 1–4 / lava "[original text]"); the Client
  (`P3DamageReportBox`) shows it over everything and it owns the keys: under AMIGA SCREENS window 6 as above (pack
  `keep_windows.damage_report`, colours as indices of the palette in force, the dungeon's below), else the remaster's
  text panel; the member lines leave the band ("A trap!!!" stays), the prompt's note and the defeat box. Watched
  (`p3-remaster-s165-damage-report-window6.png`, `-after-damage-report.png`): SWAT's failed disarm → six "takes n
  damage." rows, none cut, "A TRAP!!!" on the band; OK clicked → the map. `[~]`: the rows are in the pack's font (as
  the s163 window 6), not topaz 8; Core's swim reports through its own lines, not this window (kind 1 not used); the
  Amiga's damage is `rnd % die + add` (0…die − 1 for the trap, 0 = "[original text]"), Core rolls 1…die (rules, not
  changed here).
- **Scandor's south edge `[C]` seen** (`p3-amiga-scandor-south-edge-1-at-24-73.png`, `-2-north-no-scroll.png`). The
  save patched on a disk copy (block `0x24564` at `gamesave.dat` + 8518: centre (24, 70), view (4, 7), party (24, 73),
  still in Pendragon; leaving the town puts the party on its saved cell). The view shows rows 66–74. A step north
  (into water, a swim) moved the party up one cell in the view and the view stayed on rows 66–74; with 75 rows it
  would have scrolled (centre 70 < 71). A click on row 74 did nothing. So the rows counter is 74 in a continued game
  that never left Scandor.
- **Scandor's east edge `[C]` seen** (`p3-amiga-scandor-east-edge-1-at-47-66.png`, `-2-on-column-49.png`). Party
  patched to (47, 66), centre (46, 66): the view shows columns 42–50, column 50 being the next row's first cell (all
  water here). Two steps east (swims) put the party **on column 49**; a click on column 50 did nothing. DOS blocks
  column 49 (`planes.md` §4): a DOS-vs-Amiga difference (§4), reported; the remaster keeps DOS's edge.

### 8.8 Wolf pictures and a monster's Fireflash on the party (s125, FS-UAE)

Same machine and save as §8.6 (Speed as saved: lines stay 7 frames). `monsinfo.dat` is read once at boot (`0x73f2`), so
each test needs its own disk: `p3_amiga_patch_monsters.py`-style patches (38-byte records copied over others) written
with `adf_patch_file.py`. Recorded with ffmpeg x11grab at 50 fps plus the emulator's audio from a null sink. Shots:
`parity/shots/p3-amiga-wolf-*.png`, `p3-amiga-party-flash-1-cast-line.png` … `-5-takes-line.png`.

- **First fight after boot with byte 27 ≥ 48: the game hangs `[C]`.** Every record = Wolf: "strider spots sleeping
  monsters", OK, and the screen goes black for good (the pointer still moves; `p3-amiga-wolf-first-fight-hang.png`). Two
  mixed disks (Wild Cat → Wolf or Mud Elemental) hung the same way on their first fight. The unpatched disk's first fight
  (Skeletons) drew the field as usual (control). Cause: §6 (null sprite slot, the 65536-word copy).
- **Later fight: the stale picture `[C]`.** Disk with Skeleton / Gremlin → Wolf, Undead Gnome → Mud Elemental, Drip
  Slime / Wild Cat / Giant Bee → the Fireflash bee. Fight 1: 2 + 2 Sniverlings. Fight 2: "2 Wolf(s)" on the orders
  screen, and the field showed **two Sniverlings** at Amiga (48, 50) and (192, 50) (art 5 px right and 10 px down of the
  cell, as the same picture at its normal places) = `FieldMonsterX(m, 2, giant)`, `FieldGroupY(_, giant)`
  (`p3-amiga-wolf-stale-picture.png`). A Mud Elemental group was not met (the same `0x1b4c8` path, byte 48).
- **A monster's Fireflash on the party `[C]`.** 2 + 2 Giant Bees (special 1, chance 100), the party parrying. Per cast:
  the red line "Giant Bee casts Fireflash" (no tier; sound **21** with it: an audio burst ~0.25 s starts with the line)
  → the line is cleared → the **flash on the target's portrait** (three frames of 7 VBLs = 0.14 s each, each drawn over
  the last, the colours `#CC0000`, `#CC8800`, `#EECC44`, white) with sound 15 from the second frame (a ~0.6 s burst) →
  the rectangle restored → "sprite takes 4 damage!" (red). Then the next caster. Measured places (first frame's sparkle
  inside its 32 × 24 rectangle): slot 1 x ≈ 56, y ≈ 154; slot 3 x ≈ 168; slot 5 x ≈ 256: inside §2.4.1's
  x = max(0, 6·slot − 1) + rnd 3 + 2 bytes, y = 146 … 167. One frame after each "takes" line the same line shows in
  green (`#008843`) before the next line `[~]`. s156 static, ruled out: the line is printed once, by `0x17e16` →
  `0x166c6(text, 0)` (colour 12, Delay 30, then rows 191–199 cleared to 0); no reprint in colour 8 follows, and the
  injury lines after it (`0x14534`, `0x147cc`) print in red through the same `0x166c6`. So the green frame is not a
  second print. **s158 `[C]` static, no palette change:** nothing writes a colour during the fight (the only `0x11ae` /
  `0x4390` palette loads in the fight are the round set-up's `0x198ac` / `0x1990c` and the orders screen's; the one
  direct colour-register write, the interrupt at `0xec0` setting COLOR11–14, runs only while `0x24090` is set, which the
  start-up sets at `0x7440` and clears at `0x7682` before play). heros.cmp's palette holds **index 8 = `0x084`** (`#008844`,
  the measured `#008843`), and the line's colour 12 is `1100b`: `0x166c6`'s clear is `0x648` → SetAPen(0) and
  graphics.library RectFill (`-0x132`), which fills the rows one bitplane after the other (planes 0 … 3), so a frame shown
  after plane 2 is cleared and before plane 3 is shows the line's pixels as `1000b` = colour 8, green. The green frame is
  the clear caught half done (one frame, the capture's timing), not a drawn state `[~]` (not filmed again); the party's
  colour 11 (`1011b`) would pass through 10 / 8 the same way. The remaster clears the line in one step and does not draw
  that frame (a hardware timing artefact, like the boxes' CurrentTime staleness). The remaster's field matches: cast line, then the flash
  at `CombatFlashLayout.PartyOffset` mid-round before the "takes" line (`CombatHit.Line`), sound 15 with frame 2; sound 21
  on cast lines is played since s127.
- **The party's Sleep on monsters** was not cast in the run; §2.4.1 holds the static reading.

### 8.9 The bar outside the towns: Info's credits, the keys, the dungeon (s140) `[C]` static and seen (FS-UAE)

Static reading of `0x83e0` / `0x86e6` / `0x3904` / `0x3ba0` / `0x1bf40`, then FS-UAE on Xvfb (`:147`, `zoom = 640x400`,
sound off, the master ADF's saved party, "Continue a previously saved game"). Shots: `parity/shots/p3-amiga-window0-credits-{town,overworld,dungeon}.png`,
`p3-amiga-{overworld,dungeon}-e-examine.png`, `p3-amiga-dungeon-bar.png`, `-dungeon-bar-options.png`,
`p3-amiga-window22-party-status.png`; the remaster's `p3-remaster-window0-credits-{overworld,dungeon}.png`.

- **Window 0, the credits `[C]` static and seen.** File › Info (and the keys `?` / `/`) calls `0x86e6`, which draws
  window 0 (struct `0x23228`, the first of the 52-byte window table) through `0x3904` with save flag 1. The town's
  File › Info calls the same routine (`0xb25a` in the town / guild dispatch `0xb1cc`); in the fight File's item gives key
  0 (nothing, 8.5). Struct: frame +0 / +2 = 4 / 3, fill +4 = 0, box (36, 44)–(284, 168), title cell (+14, +16) = (5, 7) in
  colour +18 = 9 on +20 = 0, one button (+26 = 1, art slot +28 = 1 = OK) at bytes (+34, +36) = (10, 143) = (80, 143),
  keys +46 = `'O'`, +48 = 0. Its title (+22) is set at start-up (`0x7450`) from the pointer `0x2308c` to the text
  "        Phantasie III\r\r    Amiga Version 1.0 by\r          Westwood\r\r     Original Version by\r       Winston D.
  Wood\r\r       Copyright 1987\r  Strategic Simulations Inc." `0x854` prints it with each `\r` one row down at the title
  column, so the rows sit at y 56 + 8 i and each row's leading blanks are 8-px topaz cells. **Seen** (town, overworld and
  dungeon, the window pixel-equal in all three): frame outer colour 4 at x 36 / y 44, inner at 37 / 45, bottom rows 167 /
  168; ink (238, 136, 34) = index 9 (`0x741` doubled; pack #FF9224); "Phantasie III" from x 104 (cell 13) at y 56, "Amiga
  Version 1.0 by" x 72 / y 72, "Westwood" x 120 / y 80, "Original Version by" x 80 / y 96, "Copyright 1987" y 120,
  "Strategic Simulations Inc." x 56 (ink from 57) / y 128; OK at (80, 143)–(135, 160). In the dungeon the OK label is
  (238, 238, 0) = the dungeon palette's index 8 (as the keep windows, 8.7); text and frame are the same there.
- **The wait `[C]` static and seen.** `0x3ba0(+46 = 'O', +48 = 0)`: O, o, Return or a click on OK (`0x13ee` returns the
  button's key +46) end it with 1; c (99) or the second key (0) with 0; **every other key is read and dropped** (unlike
  window 7, whose key 0 takes any key). Seen: `e` and Space left the window up (screen unchanged), `c`, `o` and Return
  closed it, and what lay under came back pixel for pixel.
- **The keys outside the towns `[C]` static, seen in part.** `0x83e0` upper-cases the key (`0x1e4a`) and dispatches:
  `+` / `-` / `=` the speed (`0x870e`); `/` and `?` Info; `1` the dead-flag test (`0x240fc`, never set, 8.5: this is the
  `0x840c` use of 8.5, formerly called a town member pick); `8` / `2` / `4` / `6` and the cursor keys (raw 160 / 162 / 163 / 161) move north /
  south / west / east (`0x985e` / `0x9912` / `0x99de` / `0x9a92`); `C` Cast (window 6 `0x127ce(10)`), `E` **Examine
  Member** (`0x85a6`: window 6 `0x127ce(6)` "[original text]", then `0xd796`), `P` Party Status (`0x8832`), `U`
  Use Item (`0x127ce(9)` then `0xec76`). The overworld's walk loop (`0x7cf0`) sends every key there; the dungeon's
  (`0x1c052`) moves on its own `8` / `2` / `4` / `6` and 160–163, tests the dead flag on `#` / `@`, and sends `+ - / = ?
  C E P U` and `_` (95, which `0x83e0` ignores) to `0x83e0`. **No letter moves the party on the Amiga**, so E is never
  east there; the Amiga card in the Australian manual says "Movement is entered through either the mouse or use of the
  cursor (arrow) keys." Seen: `E` on the overworld and in the dungeon opened "[original text]" (the party did not
  move); the number pad's 2 / 6 walked; `P` in the dungeon opened window 22. The III manual's Adventure Menu has E =
  move east (and 3 / N, W, S, I, U, T, L): **D3-070, DECIDED (s141): the manual, E moves east**.
- **Window 22, Party Status `[C]` static and seen (s141).** `0x8832` first counts the found items by kind, then draws
  window 22 (struct `0x236a0` through `0x3904`, save flag 1: frame 4 / 3, fill 0, box (72, 28)–(248, 180), no title,
  one OK at bytes (12, 155) = (96, 155)), prints the labels (pointer `0x23160`: "  - Party Status -\r\r Days out:\r
  Experience:\r Gold:\r\r Items found:\r   Shields:\r   Armour:\r   Bows:\r   Weapons:\r   Potions:\r   Scrolls:\r
  Treasure:") with `0x854` at cell (10, 5) in colour 5 on 0, the values left-aligned at column 23 (x 184) in colour 5:
  rows 7 / 8 / 9 (y 56 / 64 / 72) days, experience, gold (`0xab54`, a long), rows 12–18 (y 96 … 144) the seven counts
  (`0xab1c`, a word); row 11 "Items found:" has no value. Then `0x3ba0('O', 0)`, the credits' wait (O, Return, c or a
  click on OK close it; every other key is dropped), and `0x274a` puts back what lay under. Colour 5 is the palette in
  force: map index 5 (`0x154`, pack #24B692) on the overworld, the dungeon's index 5 (pack #00DBFF) below.
  - **Days out** = `0x25d46` ÷ 3 (`0x211d4`, long division; d1 = 3). The counter counts thirds of a day: `0x8a56` adds 2
    for terrain word `0x240f0` = 1, 3 for 2 or 3, else 1, and caps it at 96000; it runs after each of the four moves of
    `0x83e0` (`0x85f6` … `0x8650`, north / south / west / east, so every overworld step) and in the dungeon loop after a
    step that moves (`0x1c1c4`: step result ≥ 5, then the result > 3 test), except a step onto the exit cells 215–217,
    which asks "[original text]" and skips the add. Cleared on leaving a town (`0x7ac6`, the town loop's exit
    to the map) and by the new-game set-up `0x90de` (with the party, experience, gold and item count). Town entry ages
    each member by (counter ÷ 3) ÷ 14 units (`0x7df2`, called at `0x77e6`). Core (DOS III, `P3TravelTime`): the same
    step costs and ÷ 3; Core clears the counter on town **entry** (DOS's place) rather than exit, which shows nowhere
    (P is not a town key; in a town the Amiga's P is Purge). Not in Core, kept DOS (s158 `[C]` static, DOS wins on numbers):
    the 96000 cap, and a dungeon exit cell's step adding nothing. DOS's add (root `0x14a9`: +2 / +3 / +1 by the terrain
    word `FAR:0xb61a`) is a 32-bit `add` / `adc` on `FAR:0x9e2` / `0x9e4` with no cap, and the dungeon step calls it
    (ovl4 `0xc02`, step result > 3) before it tests the cell for an exit (`0xc0b`: `FAR:0x9f6` ≥ 221), so an exit-cell
    step adds ⅓ there.
  - **Experience** = the long `0x25d3e`, the party's experience pool, whole points: a won fight adds its experience
    (`0x19cf6`, the reward routine; gold goes to the purse at `0x19cec`). Emptied on town entry (`0x7922`…): with the
    defeat flag `0x2411c` set (1; 3 only clears the flag) the pool and the found-item count are cleared (`0x793c` /
    `0x7942`), else a non-empty pool or item list is handed out by `0xbc7c(1)` (experience split over the party, the
    rest dropped, `0xc256` / `0xc2d4`; the items given out, the count cleared at `0xc04c` / `0xcd20`); and by `0x90de`.
    Core: `LootSack.Experience` (whole XP, D-053), emptied by the defeat (`0xa911`) and paid out only by the town's
    Distribute (`GameSession.DistributeLoot`) **and on town entry** (D3-071, user s142: the manual's "divided among the
    adventurers when they reach a town", as the Amiga's `0x7964` → `0xbc7c(1)`): `GameSession.EnterTown` opens the hand-out
    screen for the found items, then asks "Shares for NAME :" per living member (DOS III's order: root `0xa911` → ovl2
    `0x0000`(1), s143, `reverse-engineering/dos/phantasie3/capture_s143_town_entry.md`).
  - **Gold** = the long `0x25d42`, the party's one purse (the Armoury, the Bank, the inn and the event ops take from and
    add to it), so it is the gold the party carries, not gold found on this trip; the manual's Inspect says "the number
    of Gold Pieces the party is carrying". Core: `Phantasie3Campaign.PartyGold` (carried + found gold, one purse).
  - **Items found** = the word list `0x25cc4` (item ids, packed, 60 at most), its count the word `0x25d3c`: `0x1a192`
    appends a kept item (with 60 already: "The party can't carry any more items." in window 7). The counts: for each of
    the first count ids, 1–20 Shields, 21–40 Armour, 41–60 Bows, 61–100 Weapons, 101–120 Potions, 121–140 Scrolls, **141
    and up** Treasure (no upper bound); 0 or less counts nowhere. Cleared on town entry as the pool (above) and by
    `0x90de`. Core: the sack's found items (`LootSack.Items`, the holes skipped; emptied by Distribute and the defeat,
    and on entry by the hand-out screen, as the pool); Core's sack holds 90 (D3-020) where the
    Amiga holds 60.
  - **Seen (s141, FS-UAE, `../working/capture_s141_p3_window22.md`):** after leaving Pendragon, Days out 0 (the exit's
    clear), Experience 0, Gold 2763347, every count 0, the ink (34, 170, 136) = map index 5
    (`parity/shots/p3-amiga-window22-overworld.png`). With the debugger's `W` (test values only): 28 ids (20; 21 40; 41 60
    50; 61 100 80 90; 101 120 110 115 105; 121 140 130 135 125 122; 141 180 200 150 160 170 175), experience 12345, the
    counter 10: "Days out: 3", "Experience: 12345", counts 1 / 2 / 3 / 4 / 5 / 6 / 7
    (`p3-amiga-window22-overworld-poked.png`), so every boundary, the open Treasure end (200) and the ÷ 3 hold. `e`,
    Space and Esc left it up, `c` closed it and the screen came back pixel for pixel.
  - The manual (Wilderness Menu, Inspect): "the time the party has been out of town, the Experience Points earned since
    the last time in town, the number of Gold Pieces the party is carrying, and the items the party has acquired since
    the last time in town" — the same four, so no conflict.
- **The bar in the dungeon `[C]` seen.** Entering the Pendragon archives (two cells south and one east of Pendragon,
  "Will you enter?" OK, "[original text]" CANCEL, "[original text]" CANCEL) leaves the bar
  "File  Options  Speed" over rows 0–10 in (238, 238, 238) = the dungeon palette's 15 (pack #FFFFFF) with black titles;
  Options pulled down at (48, 12)–(207, 51) with the title inverted over x 48–119, File (0, 12)–(79, 27), Speed (112,
  12)–(207, 43) (the save's Fast ticked): the places of 8.5 and the remaster's `AmigaOutsideBarTests`.
- **Remaster (s140, s141):** pack `keep_windows.credits` (box, title origin, colour, OK, `text_ids` = `p3bar.credits`, the
  Amiga's text with its blanks, `font` = `amiga-topaz-8`); `UiOverlays.DrawCreditsWindow` / `CreditsRows` (each row at
  its 8-px cell; s141: drawn in topaz 8, the pack's copy of the ROM font, so every character fills its cell: the text
  pixels equal the town shot's, 2143 of 2143); the OK button in the dungeon palette in a dungeon;
  `AmigaOutsideBar.ShowCredits` / `HandleWindowKeys` (O, Return, C close; Esc too, the remaster's back key; every other
  key dropped; a click on OK sends O). Keys under the bar: `?` / `/` (and the numeric pad's /) Info, `P` Party Status;
  C and U were already the remaster's Cast / Use; E moves east (D3-070 decided: the manual). Watched on Xvfb (`:148`,
  III test party): `?` on the overworld and in the archives showed the window (the OK label yellow in the dungeon), `e`
  and Space were dropped, `o` and Return closed it with the screen as before.
  - **The town's File (s141 `[C]` static):** the town list `0x238a4` is File ("?) Info ", "O) Load", "S) Save"), Guild
    ("+) Add Member", "-) Drop Member", "[original text]", "N) New Member", "P) Purge Member", "[original text]", a
    rule, "L) Learn Spells", "T) Training") and Options ("A) Armoury", "B) Bank", "I) Inn", "M) Mystic", "X) Exit", a
    rule, "C) Cast Spell", "D) Distribute", "G) Trade Item", "U) Use Item", "V) View Party"); the dispatch `0xb1cc`
    (keys `+` … `_`, jump table `0xbb52`) sends `?` / `/` to `0x86e6` (`0xb25a`), O to the load question (`0xb1f0`), S
    to the save. Built (s141): in a III town or guild under AMIGA SCREENS `?` / `/` open the same window 0
    (`Game1.TownInfoTakesInput`: on the street, no dialog, list or building up); File › Info on the town bar (s156,
    below) opens it too. Seen on Xvfb (`:164`): `?` in the Guild and
    on Pendragon's bar showed it (`parity/shots/p3-remaster-window0-credits-town.png`).
  - **The town bar, built (s156).** Under AMIGA SCREENS with the pack's `keep_windows.menu_bars.town`, the street shows
    the Amiga's bar (`0x238a4`, places as 8.5 "The bars as pack data") over the picture's rows 0–10 in colours 15 / 0 of
    the map palette (w4's, word for word, 8.4) or `town_dim`'s in towns 16–17, and the remaster's DOS row is not drawn
    (`AmigaTownBar`, `Game1.TownBar.cs`, `UiOverlays.DrawTownMenuBar`). Each item runs the remaster's command for it:
    File › Info the credits (window 0), Load the remaster's load, Save its save (s158: each first asks the Amiga's
    window-1 question with sound 36, `0xb1f0` / `0xba9a`: "Are you sure you want to load an old\rgame over this current
    one?" / "… save this\rgame over your previously saved one?", OK runs it, CANCEL / Esc drops it, other keys are
    dropped; drawn by `DrawKeepWindow` in window 1's box with the map palette; s160: `P3ConfirmQuestion` through the
    prompt path, which also asks before Options › Distribute and Guild › Purge's pick, and DOS's Yes / No prompt in the
    layouts without the Amiga look, 8.4 "The town dispatch"); Options › Armoury / Bank as the town keys A / B (the Armoury screen, the Amiga Bank
    window 9), Inn and Mystic through their doors, Exit leaves, Cast Spell, Distribute, Trade Item (only where `p3.trade`
    allows trading), Use Item, View Party (the remaster's inspect); Guild › an item takes the party into the remaster's
    Guild (its door) and opens that Guild key's dialog there; s158: Examine lists every roster member ("Who shall be
    examined?", `GuildPickDialog.OpenExamine`, as the Amiga's `0x12422(6)` and DOS's message 395) and a pick opens that
    member's sheet (`InspectDialog.OpenSheet`), the list asking again until Esc; once the item is over (its dialog closed,
    any window-7 refusal answered) the party is back on the street (`Game1.ReturnFromBarGuild`), where the Amiga never
    left. Watched s158 on Xvfb (`:193`, III test party): File › Load's question, CANCEL; File › Save's question, OK →
    "Saved."; Guild › Examine → the roster list → a sheet → Esc → the list → Esc → the street
    (`parity/shots/p3-remaster-town-bar-{load-question,save-question,examine-roster,guild-item-street}.png`). In the
    Pendragon archives "[original text]" showed in window 1 with its key help, and after CANCEL the help line
    went back to the dungeon's (empty under D-077) (`p3-remaster-dungeon-help-after-window1.png`); "hears monsters.
    Continue?" / "[original text]" not caught: the scripted walk's screen check missed window 1 and its N key
    would have answered one (CANCEL) `[~]`. The Street's S (D-066) asks File › Save's question too under the bar (one
    caller, `0xba9a`; s160: under every III layout); a refusal of a Guild key on the remaster's Guild screen goes to window 7 as the bar's do. The
    rule rows do nothing. Keys: the bar's letters are not bound (the town keeps the manual's letters, D-066: A, B, M, N, G,
    L, U, I, D, S, and ? / / Info); the remaster's arrows pull Options down and step (Left / Right the next menu), Return /
    Space take, Esc closes. The DOS row's Option list is not on this screen: Sound is in the Options tab, Printout on the
    Inn list after a rest, but the Auto save toggle (I's DOS Option; neither DOS III nor the Amiga has one) has no route
    on III's Amiga street `[~]` (the setting still applies). The dim towns' bar palette (`town_dim`) follows the street's
    fade; **seen s162** (`p3-amiga-town-dim-bar.png`, town word poked to 16 at File › Load's `0xb17e`): the bar in the dim
    palette's 15 (`444`) with black titles, i.e. `town_dim` 15 / 0 as the remaster draws it `[C]`. (`0xb17e` runs at the
    town entry `0x78bc` and after File › Load `0xb21c`; `0xb228` is the load's redraw, not a building's.) Watched on Xvfb (`:141`, III test party): the bar, Options by keys → Bank (window 9),
    Guild by the mouse held → Drop Member → the Guild's drop list (`parity/shots/p3-remaster-town-bar*.png`).
  - **Press-drag-release (s156).** `0x184a` runs while the button is down above y 11 and returns the key of the row the
    button is released on (0 off the rows or on the title). Every III bar (fight, outside, town) now does that in the
    remaster: the press on a title pulls its menu down, the held pointer lights the rows, the release takes the row (a
    release elsewhere closes it); a press and release on the title (a click) leaves the menu down, so click, click works
    too (`Game1.AmigaBarPress`). Watched on Xvfb (`:141`): the town's Guild › Drop Member and the outside bar's Options ›
    Party Status by a held drag (`p3-remaster-outside-bar-held.png`, `-released-status.png`; xdotool's absolute moves do
    not reach the Client while a button is held on Xvfb, `mousemove_relative` does). The fight's bar uses the same path;
    s162 seen on the Amiga: held on Options, dragged, released over "[original text]" → window 23
    (`p3-amiga-fight-bar-held.png`, `-released.png`); s163 the remaster's fight bar watched on Xvfb: held on Options, dragged
    to "[original text]" (the row inverted), released → window 23 "[original text]"
    (`parity/shots/p3-remaster-s163-fight-bar-held.png`, `-released-window23.png`). The town bar's Guild › Purge Member by
    a held drag → the roster list → a party member → window 1's purge question on two rows, CANCEL clicked → the list,
    Esc → the street (`p3-remaster-s163-town-bar-guild-purge-held.png`, `-bar-purge-window1.png`); the DOS row's Inn ›
    Distribute → "[original text]" with "No" lit (`p3-remaster-s163-dos-inn-distribute-question.png`).
  - **A click on OK `[C]` seen (s141, FS-UAE, `../working/capture_s141_p3_window22.md`):** in Pendragon, `?`, then the
    left button held on OK: the label turns map index 5 (34, 170, 136), the chosen OK art (`ok-1`), and the window stays;
    the release closes it and the town comes back (`parity/shots/p3-amiga-window0-credits-town-ok-held.png`). The
    remaster: the held press draws `button.ok.chosen` (`p3-remaster-window0-credits-ok-held.png`), the release (a
    click) closes it, as the Amiga.
  - **Window 22 (s141):** pack `keep_windows.party_status` (box, label origin, colour index 5, value column 184 and
    rows, OK, `text_ids` = `p3bar.party_status`, `font` = `amiga-topaz-8`, `kinds` = `0x885e`'s ranges); Core
    `P3PartyStatus.Of` (days `Campaign.DaysOut(TravelTime)`, `Sack.Experience`, `Phantasie3Campaign.PartyGold`, the
    sack's items counted by the kinds); `UiOverlays.DrawPartyStatusWindow` / `StatusValues`; P and Options › Party
    Status open it under AMIGA SCREENS (the health dialog stays for a pack without it), the credits' wait. Seen on Xvfb
    (`:164`, the III test party just out of Pendragon): the text pixels equal the Amiga's but the gold value (another
    party) (`p3-remaster-window22-overworld.png`); `e` dropped, `o` closed it.


## Game tables for a standalone import (s201 survey, s208 decoded) `[C]`

Goal: an Amiga-only III owner imports the game tables too (today they come from DOS III, `P3DosPipeline`). s208 read
every table DOS III's import takes from its Amiga counterpart, static (flat image of `extract_amiga_p3_common`;
code sites are the `lea` / `cmpi` that index each table). Builder: `P3AmigaTables`
(`src/OpenPhantasie.Importer/Pipelines/P3Amiga/`), same shapes as `P3Extract`, then the unchanged `P3Build`;
**not called by any pipeline** (the user decides the differences below first). Test: `P3AmigaTablesTests` (the
Amiga-built GameData against the committed DOS-built one; every differing cell listed).

**The s201 survey's open list, resolved:**
- `D/w0` … `w4.cmp` are **pictures**, not the world (section 3: Pluto, Zeus, the defeat, the door pieces, the street).
  The Scandor grid is `Graphics/phantasy.set` (section 4).
- `D/DNGA` … `DNGK` (90 B) are the seen-cell bitmaps (section 5) and `D/DNPA` … `DNPK` (4,224 B, 0-66 bytes off
  the factory records; DNPJ equal) the played maps (DOS `DNP<c>`, 10-importer.md 3.3): play state, never read.
- `D/dng<a..k>.dat`: the first 4,224 B equal DOS `DNGS.DAT`'s record for that letter byte for byte; the last 128 B
  are zero in all eleven (padding) `[C]`.
- `D/pln<n>`: the plane change `0x1eb32` loads `D/PLN` + ('0' + plane) through the file table `0x23a62` entry 20:
  `pln1` (Light, 81 B) = `PLNS.DAT` +0, `pln2` (Dark, 81 B) = +0x51, `pln4` (Netherworld, 496 B) = +0xa2, byte for byte;
  the rest of each file is padding. Arrival (4, 1) / (7, 3) set there `[C]`. `pln3` is not a map: `0x1f87e` opens
  `D/PLN3` only to check that the data disk is in (`Insert the Phantasie III Data Disk`).
- `D/s1` … `s20`: scrolls, below.

### Files

| Amiga | DOS III | Layout | vs DOS |
|---|---|---|---|
| `monsinfo.dat` | `CREATURE.DAT` | 80 × 38 B + pad | byte for byte |
| `inititem.dat` | `INITARMS.DAT` | 18 × 181 B + pad | byte for byte |
| `D/dng<a..k>.dat` | `DNGS.DAT` records A..K | 4,224 B + 128 B of zeros | records byte for byte |
| `Graphics/phantasy.set` +0x23 | `WORLD.DAT` | 50 × 75 | **54 cells differ** (section 4; Pendragon's square among them) |
| `D/pln1`, `pln2`, `pln4` | `PLNS.DAT` +0, +0x51, +0xa2 | 9 × 9, 9 × 9, 31 × 16 | byte for byte |
| `D/s1`..`s5`, `s7`..`s20` | `SCROLLS.DAT` records 0..18 | 1,050 B each (plain text, padded to 1,152); `D/s6` is Scroll IV's picture | text equal, record for record |

### Scrolls `[C]` `0x1f6dc`

Items 121..140 only: n = item − 120; n 2..4 become n = 2 with three pages; n 9 has three pages; n 6 draws the picture
`D/s6` (`0x123c` / `0x1208`); page k reads 1,050 B of `D/S` + (n + k) (file table `0x23a62` entry 10, number written
by `0x1d8a`) into `0x241f0` and shows it (`0x1e2c2`). As DOS records (file order): 121 → [0], 122 / 123 / 124 →
[1, 2, 3], 125 → [4], 127 → [5], 128 → [6], 129 → [7, 8, 9], 130 → [8], 131 → [9], 132..140 → [10..18].
**vs DOS** (planes.md section 7): DOS item 123 → [4], 124 → [3, 4, 5]; every other item the same.

### Names: string-pointer tables in DATA `[C]`

Each group is its own table of 4-B pointers (strings in CODE), indexed by the number the code holds:

| Group | Table | Code | vs DOS |
|---|---|---|---|
| items 0..180 | `0x22a78` + 4 id | `0x8c74` area | 27 treasure names carry an article (`a Pearl`, `an Opal`, …: items 142, 145, 148, 149, 155..165, 167..178) |
| spells 0..57 | `0x22d4c` + 4 n | | #13..16 `Flame Arrow I` / `P.Flame Arrow I` / `Flame Arrow II` / `P.Flame Arrow II` (DOS `Arrow Flame …`); #53 a placeholder name (DOS empty) |
| towns 0..17 | `0x22f14` + 4 n | `0xa382` | equal |
| dungeons 1..11 | `0x22f8c` + 4 n (one line, `0x80cc`); `0x22f5c` + 4 n (with line breaks, the place box `0xa39a`) | | dungeon 2 `Many men guard tent here` (DOS `… guard a tent here`; the boxed set has the `a`) |
| inns 1..8 | `0x22fbc` + 4 n | `0x80f6`, `0xa3b2` | Crown, Jolly, Royal end in a space |
| terrain 0..18 | `0x22fe0` + 4 t | `0xa728` | 19 names (DOS 22): 4 / 5 `BUILDING` / `DUNGEON` (DOS empty), 6 `Pathway` (DOS `Cobblestone Path`), `Bright mist`, `A bridge`, `Black path` (DOS capitals); no 19..21 (DOS empty, empty, `A Tavern`) |
| races 0..17, classes 0..5 | `0x22e4c`, `0x22e94` | `0x206d6` | no padding spaces (the build strips DOS's) |
| class short, social classes, body states, status, status short, skills, ranks | `0x22eac`, `0x22ec4`, `0x22edc`, `0x22eec`, `0x22f00`, `0x2302c`, `0x22a2c` | | equal |
| origins 0..5 | `0x22e34` + 4 n | `0x13c62` | equal (DOS's group also holds towns 15..17 in front; the builder takes them from the town table) |
| combat lines | `0x22a38`, 16 | `0x18bea` | `Moves to Back / Middle / Front` where DOS has three empty slots and no `Front / Middle / Back` in front (not used by the build) |

Transfer maps I → III: `0x23f12` (class, 6 B, `0x206b6`) and `0x23f18` (race, 16 B, `0x206f4`) = DOS `DS:0x48` / `0x4e`.

### Numbers `[C]`

| Table | Amiga (index) | DOS | vs DOS |
|---|---|---|---|
| item points / requirement | `0x227a0` + id / `0x22805` + id (`0x8c74`, `0x8c9a`) | `DS:0x113c` / `0x11a2` | equal |
| item value | `0x2286a` + 2 id, u16 (`0x8bc0`) | `DS:0xf92` | equal |
| town price index | `0x229d4` + 2 town (`0x8bd6`) | `DS:0x10fc` | equal |
| HP dice | `0x221dc` + 21 class + level − 1, level clamped at 21 (`0xe39c`, `0xe3d4`) | `DS:0xa78` + 22 class + level − 1 | **L1..15 equal; L16..21 keep rising** (Fighter 90 95 100 105 110 115, Thief +4 a level to 79, Priest to 89, Ranger +5 to 110, Monk +4 to 84, Wizard +3 to 58) where DOS repeats L15; no L22 (DOS 22 levels, the loader wants 22) |
| magic ceiling | `0x2225a` + 15 class + level − 1, level ≤ 15 (`0x11d60`) | `DS:0xafb` + 15 class + level | equal |
| skill tables ×8 | `0x2234e`, `0x223a8`, `0x22401`, `0x2245b`, `0x224b5`, `0x2250f`, `0x22569`, `0x225c3` + 15 class + level − 1 (`0xe6d0` … `0xe866`) | `DS:0xc53` … `0xec9` + 15 class + level | equal but one cell: the Parry table is one byte short on both (D3-032), so Wizard L15 reads the next table's first byte: DOS 0, Amiga 42 (Swim's Fighter L1) |
| spell rows | `0x2261e`, 13 × 15 (`0x10b8e`) | `DS:0x1208` | equal |
| training XP | `0x222fa` + 4 (level − 1), u32, levels 1..21 (`0xdfde`) | `DS:0xb88` + 4 level | levels 1..21 equal; no level-0 entry (DOS's is neighbouring data, unused) |
| training gold | `0x222dc` + 2 (level − 1), u16 (`0xde92`) | `DS:0xb6c` + 2 level | levels 1..15 equal; **16..20 run into the XP curve on both**, so they differ by byte order: Amiga 0, 180, 0, 324, 0; DOS 180, 0, 324, 0, 583 |
| hit location | `0x239b4`, 5 × 6 u16 (`0x1421c`) | `DS:0x12cc` | equal |
| score weights | `0x23a50`, 8 u16 (`0x1e9e4`) | `DS:0x1c50` | equal |
| Air elemental's spells | `0x23f48`, 31 u16 (copy loop `0x8f4c`) | `DS:0x1bb2`, 0-terminated | equal |
| wilderness area levels | `0x220f6` s8 × 25 (`0x1aa78`) | `DS:0x9fe` | equal |
| terrain monster offsets | `0x2210f` + t + rnd(4) − 1 (`0x1a9d6`; rnd 1..n, `0xaa7e`) = DOS-style index t + rnd(4) from `0x2210e` | `DS:0xa17` + t + rnd(4) | indices 1..15 equal (the real table); 0 never read; **16..25 (terrain 12+) read the next object**, a different one in each version (Amiga `0x2211e`'s bytes 12 8 11 10 …; DOS 0 18 18 …) |
| dungeon letters | `0x23b3c` + 2 n, low byte (`0x1bbe6`) | `DS:0x1c84` | equal (J H E D A C B K I F G) |
| dungeon entrance x | `0x23a38` + 2 (letter − 'A') (`0x1bd44`); dungeon 1 → x 5 (`0x1bd5a`) | `DS:0x1bb6` + 2 letter | equal |
| elementals' stats | code constants `0x8ed8` … `0x90c6` | ovl constants (`P3Build.Elementals`) | Air 30 / 150 / 150 / 32, Mist 20 / 200 / 210 / 42, Mud 12 / 250 / 255 / 38: equal |

### World `[C]`

- **Places** (`0x9c6a`, DOS ovl3 `0xa22`): each fixed square is `cmpi.w #x,0x240c4; bne; cmpi.w #y,0x240c6; bne;
  move.w #type,0x240be; move.w #number,0x240c0`. The scan finds the same 32 squares as DOS, in the same order, the
  Netherworld's two first (`0x9cda`, under the plane test). Light / Dark (`0x9d5a`): (4, 1) is town 14 + plane;
  (6, 7) on Light and (2, 7) on Dark dungeon 9 + plane, as DOS. Edges `0x9c6a` (section 4: the east column is one
  further out than DOS's). **Start** (24, 37): `0x7602`, `0x2010e`.
- **Pendragon's square** (24, 37) is **tile 25** on the Amiga map (DOS 29, a town tile); by the terrain test below it
  is Water (10). One of the 54 differing cells.
- **Terrain of a tile** `0xa4a6`: a run of range tests, each later one overriding: 0..25 → 10, 68..74 → 11, 61..67 → 14,
  90..114 → 3, 115..154 → 2, 155..187 → 9, 188..205 → 0, 206..209 → 6, 214..224 → 6, 225..241 → 1, 222 → 13,
  ≥ 242 → 15 (blocked); on the Netherworld then 61..77 → 17, 78..82 → 16, 83..87 → 18, 88..241 → 8 (`0xa5fa`). Same
  as DOS but **no place ranges** (DOS 26..38 → 19, 39..46 → 20, 47..54 → 21) and **no default**: tiles 26..60 and
  75..89 leave the local unset (never initialised) `[?]` (DOS: 4). No encounters on 10, 8, 15 (`0x7c24`).

### Amiga ≠ DOS, decided (an Amiga-only import)

**Decision (user s209): DOS for all eight.** An Amiga-only import writes DOS's values wherever the list below differs
(numbers, the Scandor grid, place-tile terrain, Scroll III pages, text), carried as data in the importer; III then plays
the same whichever version was imported. Not built yet.

Standing rule: between versions DOS wins on numbers; maps and content are the user's call. None of these is a
manual ↔ code conflict: the III manual's HP chart (III PDF 8 / pr.11) stops at L15, and it gives no training
costs and no terrain table.
1. **HP dice L16..21** rise on the Amiga, flat on DOS; the Amiga has no L22.
2. **Training gold L16..20** (both overruns): Amiga 0, 180, 0, 324, 0; DOS 180, 0, 324, 0, 583.
3. **Parry, Wizard L15** (overrun, only for `p3.skill-tables = dos`, D3-032): Amiga 42, DOS 0.
4. **Terrain monster offsets past the table** (terrain 12+): each version reads its own neighbour.
5. **Scandor**: 54 cells (section 4), Pendragon's square tile 25.
6. **Terrain of the place tiles**: no 19 / 20 / 21 and no default on the Amiga; its names `BUILDING` / `DUNGEON` /
   `Pathway`, 19 names.
7. **Scroll III pages**: items 123 and 124 show records 1..3 on the Amiga (DOS [4] and [3, 4, 5]).
8. **Text**: the item articles, `Flame Arrow`, spell #53's placeholder, dungeon 2's name, the inns' trailing spaces,
   terrain capitals. (D3-005 chose DOS's item names for the DOS-built tables.)

### Left `[?]`

- A recipe for `p3-amiga-1.0` that writes the tables (DOS's still win when both are
  imported, as I's `P1AmigaTables`). As built the files do not load (`Phantasie3Data`): `hp_dice` has 21 levels (the
  loader wants 22), the curves' index 0 and `terrain.other` are null.
- III's own UI lines (`strings/en.json` `core.p3.*`, 124 ids quoted from `MAIN.EXE`) have no Amiga alternatives in
  `en.sources.json`: an Amiga-only import shows the fallback wording.
- `P3Build`'s other hand-written constants (class kits, encounter numbers, the rule blocks) are DOS readings not all
  re-read on the Amiga (the elementals' are, above).
