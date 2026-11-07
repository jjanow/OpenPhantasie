# Amiga graphics format notes (Phantasie I)

## Disk layout

ADF master: `original-images/phantasie1/phantasie-v1.0-1987-ssi.adf`  
Unpack:

```bash
xdftool reverse-engineering/amiga/original-images/phantasie1/phantasie-v1.0-1987-ssi.adf \
  unpack reverse-engineering/amiga/unpacked/
```

Volume name `PHANT`. **No IFF FORM/ILBM** assets — `strings` hits on `FORM` are
false positives inside English prose. Graphics are raw bitplane blobs.

## Palette (4.4)

Two consecutive 16-entry Amiga OCS tables in `game`:

| Label  | File offset | Role                          |
|--------|-------------|-------------------------------|
| combat | `0x194BC`   | Sprites / most screens        |
| alt    | `0x194DC`   | Second copper-style table     |

Each entry is a big-endian word `0x0RGB` (4 bits/channel). Kroah's Sprite Viewer (and its published reference PNGs)
expands them with the Amiga→ST nibble LUT; the game sets every table with LoadRGB4 / SetRGB4, so the screen shows
`c · 17` per gun (`hw4`). The sprites, covers, town, defeat and ending pictures and (since s127) the map art are
extracted `hw4` (see "Palette tables in use" below). Swatches: `extracted/palettes/{combat,alt}.{json,png}` (LUT).

**Palette tables in use (s125, static) `[C]`.** All palette loads go through `h77 +0x466` (copy 16 words to the
colour map `h79 +0x52c`, then `LoadRGB4`, `h236 +0x54`) or `h77 +0x288` (one pen, `SetRGB4`, `h236 +0x118`).
`h77 +0x4d2` saves the current 16 pens (`GetRGB4`) to `h79 +0x4f0`, `h77 +0x516` loads them back. Callers of `+0x466`:

| Caller | Table (h69) | Screen | Extraction |
|---|---|---|---|
| h77 `+0x340` | `+0xb4` (`0x194DC`) | title covers | `hw4` (s102) |
| h20 `+0x186`, h29 `+0x2e` | `+0x54` (`0x1947C`) | town | `hw4` (s120) |
| h26 `+0xc4c` | `+0xb4`, pen 2 first set to `0xE00` | not identified; h26 also sets pens one by one (`+0x288` at `+0xe66`, `+0xecc`) | none |
| h32 `+0x5a8` (ending: `+0x288` per pen, `+0x754`) | `+0x94` (`0x194BC`) | defeat / ending | `hw4` (s124) |
| h53 `+0x33c` | `+0x94` (`0x194BC`) | combat | sprites `hw4` (s125) |
| h56 `+0x550`, `+0xed6` | `+0x74` (`0x1949C`) | overworld / dungeon map | `hw4` (s127; FS-UAE equal s128) |
| h77 `+0x4c0` | `+0x34` (all black) | blank before a picture | — |

The map art (`extract_amiga_map_graphics.py`: patterns, regions, icons, glyphs, compass, party sprite via `SetRGB4`) is
therefore on screen `c · 17` too: h56 `+0x550` draws the map, then LoadRGB4s `+0x74` (s127 read), the same path the
four captured screens use. Extracted `hw4` since s127; seen on FS-UAE s128 (map section below): equal pixel for pixel. The colourblind table has the new map colours (`#AA4400`, `#446600`, `#228800`, `#2266AA`).

## Sprites — `*.pat` (4.3)

### Monsters — `mstr1.pat` / `mstr2.pat` (68016 bytes each)

| Field        | Location in `game`      |
|--------------|-------------------------|
| byte offset  | `0x16842 + i*4` (u32 BE)|
| width/height | `0x1EA8 + i*4` (u16×2)  |
| name         | `0xC88 + i*58` (C string)|

- 80 sprites, indices align with Phase 3 `monsters.json`
- 4 bitplanes, **planar-sequential** (plane0 full image, then plane1, …)
- `mstr1` = idle, `mstr2` = attacking pose (same offsets/dims)

### Race portraits — `party1.pat` / `party2.pat` (8192 bytes each)

- 16× (32×32×4 planar) = 512 bytes each, packed contiguously
- Names at `game` `0x69BE` (sequential C strings)
- Indices align with Phase 3 `names.json` → `races`

### Sprite colours on screen (s125) `[C]`

- Hunk 53 is the only code naming `PARTY1.PAT`, `PARTY2.PAT`, `MSTR1.PAT`, `MSTR2.PAT` (strings in its data hunk 54;
  h53 `+0xc82`, called once from h17, loads the portraits into the shape pointers h55 `+0xd0` / `+0xd4`). The shape
  table h55 is used only by h53 and by h32. Combat begins at h53 `+0x274` (saves the pens, `h77 +0x4d2`) and its
  set-up (h53 `+0x2fc`) draws the frame with pen fills (h56 `+0x170` / `+0x1ac`, no picture), then **LoadRGB4s the
  combat table h69 `+0x94`** (`0x194BC`, h53 `+0x33c` → h77 `+0x466`); h53 has no `SetRGB4` (`h77 +0x288`) call and
  `+0x2a4` loads the saved pens back (`h77 +0x516`). h32 draws the portraits (h55 `+0xd0`, 512 B a race) in the
  defeat judgement (`+0x46c`, called at `+0x5c4`) after its LoadRGB4 of the same table (`+0x5a8`). So monsters and
  portraits (idle and attacking) show at `c · 17` per gun wherever the Amiga draws them. (h32's other defeat bitmaps,
  h77 `+0xcfe` of h69 `+0x1814`, `+0x994`, `+0x32d4`, `+0xb94`, are not extracted for I.)
- Extracted with `load_palette(hw4=True)` since s125: `000 A42 E00 E60 EA0 EE0 0E0 080 884 0EE 0AE 04E EBB 888 E2A EEE`
  → `#000000 #AA4422 #EE0000 #EE6600 #EEAA00 #EEEE00 #00EE00 #008800 #888844 #00EEEE #00AAEE #0044EE #EEBBBB
  #888888 #EE22AA #EEEEEE` (the LUT gave `#B64924 #FF0000 …`, Kroah's PNG colours).
- **Measured** on FS-UAE: `tools/scripts/p1_amiga_sprite_sheets.py` packs 50 sprite frames (all 32 portrait frames, 18
  monster frames) into two 320×200 pictures written over `decor.pic` / `final.pic` of a disk copy whose title shows
  them in table `0x94` (`p1_amiga_patch_title.py ADF decor.pic final.pic 0x94`; same loader and LoadRGB4 as combat).
  `parity/shots/p1-amiga-sprites-a.png` / `-b.png` (window shots, 2×, screen origin at (160, 100)): all **115,808**
  sprite pixels equal the new PNGs, the **35,091** non-black ones included; the old LUT PNGs matched 0 of those 35,091.
  All 16 pens occur in the sample. A real combat screen was not reached in s125; s128 reached one (below).
- **A real fight on FS-UAE (s128)** (OUT1 east of Pelnor; `parity/shots/p1-amiga-encounter.png`, `p1-amiga-combat-orders.png`
  exact; `p1-amiga-combat-*-window.png` window grabs, 3× wide): the encounter screen has the menu "Party Options" (Beg
  mercy, Threaten, Greetings, Fight, Flee, Show names, Party health); Fight opens a blue orders window (rows "NAME
  HP:n MP:n PARRY" — only EMILY, HOLLY and MANGAR, members 3, 5 and 6, were listed; the others were asleep: the window lists OK members only, "The combat screen" below `[C]` — and a FIGHT button). In a round the monster
  shows its attack frame (~0.36 s), a line prints in orange centred under the party row ("BANTIR MISSES", "LIZ TAKES 1
  DAMAGE", "LIZ AWAKENS"), then a **party flash**: every party sprite redrawn with bitplanes 0 and 1 cleared (pen p
  shows p & 12: `0E0`/`080`/`EE0` → `EA0`, `0AE`/`04E`/`0EE` → `884`, `EEE`/`888`/`E2A` → `EBB`, pens 1–3 → black;
  the monster's `080` showed `0E0`) for ~40 ms, the row cleared and redrawn member by member (~90 ms); not an effect but the round's redraw caught mid-fill, "The combat screen" below `[~]`.
- **Sound in that round (s128, FS-UAE's own stream recorded with `parec --monitor-stream`):** one effect began ~0.4 s
  after the FIGHT click and ended 1.4 s later; the pointer came back (round over) at 9.88 s, the sound ended at 9.90 s,
  and the last 0.9 s (the decay after the flash) had no screen change. So the round seems to wait for the sound to end
  (h89 `+0x304` playing in line) `[~]` (audio latency ±0.1 s; static check left to the sound decoding).

## The combat screen (s132, static) `[C]`

Hunk offsets as above; h53 draws, h35 is the round (state 5, `h2 +0x232`), h38 the damage / spell lines, h44 the
encounter (state 4: set-up `+0x614`, Party Options `+0xd6`), h71 the orders requester and the per-round set-up,
h84 the Intuition requesters (struct `Requester` 0x70 B at h84 `+0x3fc6 + id·0x70`, opened by h86 `+0`).
Screen coordinates below are Amiga screen pixels (an `ashot` file row is 5 more).

**h53's state (BSS h55).** Three monster groups of 0x30 B at `+0` (per monster 8 B: x, y, w, h; w = 0 = gone),
party slots at `+0x90` (8 B each: x, y, 32, 32), shapes `+0xd0` / `+0xd4` (`party1.pat` idle / `party2.pat`
attacking, 512 B a race), `+0xd8` / `+0xdc` (`mstr1` / `mstr2` buffers, three slots of 0xc80 B, loaded per fight by
`+0xd5e` with a three-slot cache h54 `+2`), `+0xe0..e4` each group's last monster index (−1 = no group), `+0xe6..ea`
its monster id, `+0xee` the party row's last index, `+0xf0` the row's record indices.

**Layout.**
- Gap `+0(n − 1, w)` = (320 − w·n)/(n + 1) (signed word division); thing i at x = gap + i·(gap + w).
- Party row (`+0x30`, `+0x86`): every record with status `+0x18` ≠ 0 (DEAD = 0 is left out; asleep members stay), in
  record order, 32×32 at **y 150** (h54 `+0x1e6`). A death re-lays the row (below).
- Monster groups (`+0x140(g, id, last, w, h)` from h71 `+0x10f2`, w × h from the size table h3 `+0x1234`): every
  monster of group g in one row, x by the gap rule with the group's count. y (`+0x2fc`): group 0 (the first rank in
  h4 `+0x132` order) sits on y 147 − h (3 rows above the party), each next group 3 rows above the one before:
  y_g = top_(g−1) − 3 − h_g. Dead monsters leave their place empty (the row is not re-laid).
- **Set-up `+0x2fc(0)`**: RectFill pen 0 of (3, 14)–(316, 196) (h58 `+0x230`, `h77 +0x94a(0)`), LoadRGB4 the combat
  table h69 `+0x94`, the party row (`+0x478(0)`: one `h77 +0xcfe(2, …)` blit per member, each after a WaitTOF), the
  living monsters (idle shapes, `h77 +0xcfe(10, …)`, a straight copy, minterm 0xC0), then the frame `h77 +0x6dc(0, 11,
  319, 186, 1)`: a 13-point PolyDraw in pen 1 (`A42`), i.e. a 3-px border round (0, 11)–(319, 199) (rows 11–13 and
  197–199, columns 0–2 and 317–319). Rows 0–10 are the Intuition screen's title bar with the menu strip.
- The set-up runs at **every round start** (`h71 +0x1050`, called by h35 `+0xe` after FIGHT and by h44 `+0xca`): the
  field is filled and redrawn member by member. **Sound 17** plays only at a fight's first set-up (`+0x274`: flag h54
  `+0x1e8`, set again by the fight's end `+0x2a4`, which BltClears the screen and loads the saved pens back).
- **One picture a frame (s135, static `[C]`, live `[C]`).** The fill (`h56 +0x1ac` → `h77 +0x94a(0)` → `+0xa60`: SetAPen 0,
  **WaitTOF**, RectFill) and every blit (`h77 +0xcfe`: InitBitMap, **WaitTOF** unless h81 `+2` (set only on the menu
  handler's logo path), BltBitMap minterm 0xC0) wait a vertical blank, so a set-up of n members and m monsters takes
  1 + n + m frames: the field cleared, the row member by member (`+0x478(0)`), the monsters group by group, then the frame
  PolyDraw. Live: s132 r1 6 + 1 → 0.14 s, s134 5 + 1 → 0.12 s, s135 5 + 1 → 0.12 s (60 Hz, NTSC). Built s135: pack
  `combat_field.setup_frame_seconds` 1/60; `P1CombatFieldRun` starts each round with it (`SetupMs`, `State.SetupDrawn`),
  `UiOverlays.P1CombatField` draws only the pictures done (`parity/shots/p1-remaster-combat-setup.png`: five of six
  members, the monster not yet). The round's end runs it again (below, "Round end"; built s136).
- **The fight's start (s135, static corrected s136 `[C]`, live `[C]`).** The state machine (h2 `+0xbc`, h4 `+0x6e`, a
  13-entry table) runs state **11** for a new fight: h62 `+0xa`, h77 `+0x106c`, then **h44 `+0x0`** (the encounter's
  values, `+0x998`, `+0x1232`; with h4 `+0x12` or `+0x10` = 0 straight to the round, state 5), whose `+0xca` calls **h71
  `+0x1050`**: `h53 +0xfc`, `+0x140` per group, `+0xd5e` (the monster shapes from disk, cached: the requester's 3.6 s; s154 below),
  then `+0x274`: with h54 `+0x1e8` set, `h77 +0x4d2` saves the 16 pens (GetRGB4 into h79 `+0x4f0`), `h77 +0x246` BltClears
  all four planes (8000 B each: the **whole screen**, the menu bar's rows too), the flag cleared, **sound 17** (38 one-tick
  steps; the player blocks) on that black screen, then the set-up `+0x2fc(0)`; state 4 (h44 `+0xd6`, Party Options)
  follows. (s135 credited this to h44 `+0x614`: that is state **12**, the fight's end, "Round end" below.) `+0x2fc` has
  one caller (`+0x29c`) and h71 `+0x1050` three (h35 `+0xe`, `+0x16cc`, h44 `+0xca`). **The surprise flags decide which**
  (s136 `[C]` static): h4 `+0x10` / `+0x12` are DOS's `F` / `H` (`combat-resolution.md` "Surprise"; writers in h74, the
  outdoor encounter: "HEARS MONSTERS" › FIGHT with roll ≥ 30 sets `H` = 2, `F` = 0 at `+0x4ba`; "YOU SPOT SLEEPING
  MONSTERS" › ATTACK the same at `+0x35c`; the whole routine in "The map screen's requester 23" below). h44 `+0x0` first sets every member's order
  h4 `+0x146` to 4 (and slot 6), then with `H` = 0 or `F` = 0 returns state **5** at once (no set-up there): round 1 (h35
  `+0xe` → h71 `+0x1050`: the BltClear, sound 17 and the set-up), nobody of the party acts, asleep monsters may wake, the
  round's end (`+0x16ac`: `H` := `H` = 2 ? 1 : 0, the set-up again, sound 18), then state 4. Only with both set does `+0xca`
  run the one set-up and Party Options follow directly. s135's fight (HEARS MONSTERS) was the first kind: set-up 1 =
  round 1's, set-up 2 = its end, the bar 0.84 s later = sound 18's hold (s136 live: the pointer came back 0.84 s after a
  round-end set-up, r1 4.64 → 5.48). **s138 live `[C]`** (`working/capture_s138_combat.md`, shots
  `p1-amiga-combat-33`…`-37`): two surprise openings filmed (HEARS → FIGHT → requester 23 "[original text]" 0.95 s,
  h74 `+0x4ac`; BLACK KNIGHTS → FIGHT): black 1.08 s, set-up 1 (the frame last), at once the end set-up (round 1 empty),
  the bar **black from the BltClear until the Party Options wait**, back with the pointer 0.82 / 0.85 s after the end
  set-up; the normal opening (h74 roll < 30): one set-up, the bar a frame after the frame. A click during the black ends
  sound 17 there and the set-ups still run. **The bar** is the game's own: the Party
  Options wait (h71 `+0x36` → h80 `+0xbfe`) prints " Info" at 4 and " Party Options   Speed" at 59 (IntuiText h81 `+0x4a`,
  pen 13, PrintIText on h79 `+0x514`), then SetMenuStrip, the input wait, ClearMenuStrip. **Built s136:** `Intro` = black
  + one set-up (the Party Options opening); a surprise opening's round-1 run carries the black (`Plan(leadBlackMs:)`,
  `State.Black`, presenter `_p1SurpriseOpening`). Live (`working/capture_s135_combat.md`,
  window video): the HEARS MONSTERS requester stays 3.6 s after FIGHT (the monster shapes load; s153: 3.62 s of map after
  a surprise box, 3.77 s after ATTACK on sleeping monsters, 2.77 s after a box for three groups, `[~]` disk; **s154**
  static `[C]`: `+0xd5e` keeps a three-slot cache (h54 `+2`, 200 = empty at load, never reset) of monster ids; a group
  whose id is cached uses its slot, a group with an earlier group's id shares it, any other takes the first slot this
  fight does not use and is read by `+0xccc` (h122 Open, Seek to the id's offset in the table h54 `+0xa6`, Read, Close;
  MSTR1.PAT then MSTR2.PAT): no Delay or WaitTOF, pure disk time. Quirk: group 1's block runs when the group count is
  above 0, not 1, so a one-group fight may compare a stale id `[~]`. **Built s154:** pack
  `combat_field.shape_load_seconds` 3.39 (the mean of the three live loads, one figure for a fight with any read `[~]`) and
  `shape_cache_slots` 3; `P1MonsterShapeCache` / `Game1.StartFightScreen`: after a map FIGHT (a step's encounter, a
  menu's or box's answer, the fight key) the map stays that long before the combat screen when a group is not cached, no
  wait when all are; the cache lives for the Client's run, as the Amiga's), black 1.08 s (s153's hold of 17: 0.76 + 38 · 2 · 0.0037 + 0.0075 = 1.04 s, audible 1.04 live,
  plus the fill's frame), the first set-up 0.12 s with the frame drawn last, the second 0.12 s
  (the frame stays), the bar back 0.84 s later (above: round 1's end and sound 18). Built s135: `P1CombatFieldRun.Intro` /
  `CombatPresenter.P1Intro` (black for 17's hold, 0 with the Sound switch off; s136: one set-up; the bar off until it ends);
  s138: a key cuts the black and sound 17 there, the set-up still runs (`CombatPresenter.CutP1Black`); a surprise opening's
  run keeps the bar cleared and draws the first set-up's frame last (`Plan(opensFight:)`, `State.BarCleared` /
  `FrameHidden`, one extra frame for the PolyDraw).

**The line `+0xb80(text, n)`.** n ≠ 15: `h89 +0xee(n)` first (n < 0 = none; the player blocks). Then
`h80 +0x84a(text, 3, 185)`: the line cleared, the text centred on 320 in **pen 3** (`E60`, orange) at TopEdge 185.
Then, if n ≠ 15 and strlen > 3, `h62 +0x576(h3)`: h3 polls (`Delay(1)` + a key / click check, 21.87 ms each
live, s130) that a key or click ends. The line stays until the next one. h3 (h3 `+0`) is **10 at boot**; the Speed
menu (`h65 +0x27e`, from `h71 +0x36` codes 0x14–0x16) sets Fast 1, Normal 30, Slow 80, though Normal is ticked at boot.
Sounds by caller (h35 / h38):

| Line | Party | Monster |
|---|---|---|
| `%s AWAKENS` | 0 | 10 |
| `%s CASTS A SPELL` | 1 | — (the Amiga prints `" "` with sound 9, no wait); the remaster keeps DOS's text with sound 9, no wait (D-079, user s138; pack `monster_no_wait`) |
| `%s HITS` / `%s MISSES` | 3 / 4 | 5 / 8 |
| `[original text]` / `A STUNNING BLOW` | | 6 / 7 |
| `MONSTERS RUN`, `PARTY AWAKENS` | 2, 0 | |
| `%s IS SLEPT` (party spell / monster spell) | 11 | 14 |
| `%s IS RESURRECTED` | 12 | |
| `%s BREATHES` | | 13 |
| `%s TAKES %d DAMAGE`, `%s IS HEALED`, `[original text]`, `[original text]`, armour reduced, slowed | −1 | −1 |
| `%s DIES` (monster) | 16 | |
| `%s DIES` (member) | 15 = no sound, no wait (the death below plays 15) | |

**Actors.**
- A swing (`+0x85e(m, 1)`, `+0x652(g, i, 1)` → `+0x59e` / `+0x622`): the attacking shape, `Delay(18)` (0.36 s, dos
  ticks), the idle shape; then the roll and its line. One per swing line.
- A cast (`+0x85e(m, 2)`, `+0x652(g, i, 2)` → `+0x1060`): the caster's **silhouette** (`h77 +0x1332`: the four planes
  ORed into plane 0 of a cleared buffer, so pen 1 `A42` where the shape has any pen) for `Delay(18)` (`Delay(1)` when
  h3 ≤ 1, Fast), then the attacking shape `Delay(18)`, then idle. Then the party's "%s CASTS A SPELL" (sound 1); a
  monster's one-space line (sound 9) and its spell's own lines (`h38 +0x480`).
- **A party damage spell's sparkle** (s133, static `[C]`; `+0xab8(g, i, damage)`). Callers: h35 `+0x4bc` (FIREFLASH,
  the Amiga's spell codes 1–4 = DOS 4–7: its one target, the damage it then deals) and h38 `+0x1a6` / `+0x1f6` (`h38
  +0(caster, mode, param)` modes 4 / 5 = MINDBLAST / FLAMEBOLT, codes 29–32 / 33–36 = DOS 32–39: every monster of
  every group, each with its own roll `param · h71 +0x103a() / 100`; FLAMEBOLT passes that roll **before** the
  monster's `+0x14` is subtracted for `+0xcf6`). Nothing else calls it: no sparkle for heals, buffs, CONFUSION /
  WEAKNESS / BINDING (modes 1–3), SLEEP (mode 6), monster spells or melee. Always before the monster's damage
  (`+0xcf6`, whose line is "%s TAKES %d DAMAGE").
  - **The roll h71 `+0x103a`** (s134 `[C]`): `rand() / 327` (h182 `+0`, h203 `+0` = signed long divide, quotient):
    **0…100**, 101 values; 100 only for r ≥ 32700 (68 of 32768). Every percent roll of the Amiga's I goes through it
    (h35 ×21, h38 ×11, h41, h44, h50, h59, h74). DOS's `rand0_99` (OV2 `0x655b`) is `rand / 328` = 0…99: a number
    difference; DOS wins, Core keeps 0…99.
  - **FIREFLASH** (h35 `+0x2f0`…`+0x4e4`, s134 `[C]`): param by code 1–4 = **9 / 29 / 58 / 100** (Core's
    `FireflashParams`); damage = roll · param (h206 long multiply) · caster INT (record `+2`) / 2000 (h203) + 1: DOS's
    formula (OV2 `0x339`, `Magic.FireflashDamageDos`). The damage roll comes first. Target: DOS OV2 `0x361` step by step
    (`combat-resolution.md` "FIREFLASH target"): e = group count (h4 `+4`) − 1 = the **back rank**; g = h4
    `+0x132[e]`, L = h4 `+0xfc[g]` (last index); L < 0 → e − 1 with no roll; else k = roll · L / 100 (`muls.w`,
    `divs.w #100`), and while monster k's HP (h4 `+0x102 + 16g + 2k`) < 1 and tries ≤ L: tries + 1, k = k < L ? k + 1 : 0;
    tries > L → e − 1. Then `+0xab8(e, k, damage)` and `+0xcf6(e, k, damage)`; e < 0 = no target. Only difference to DOS:
    the roll's 100 makes k = L, so the Amiga can pick a group's last monster outright (DOS never does) and reach 100 ·
    param · INT / 2000 + 1 (INT 18: FIREFLASH IV 91, DOS 90). MINDBLAST / FLAMEBOLT's `param · roll / 100` likewise reach
    the full param on the Amiga. Manual: D-029 / D-059 (the Amiga sides with DOS).
  - **Indices.** `+0xab8` and `+0xcf6` take the rank position e (the screen's group g: h71 `+0x1050` lays group g from
    h4 `+0x132[g]`), and k is a monster record. A monster's death compacts the records at once (h38 `+0x1d6` / `+0x23e`:
    k − 1 after `+0xcf6` reports a death, so the loop visits the next monster at the same k) while the screen only
    erases its slot (w = 0), so record k is the k-th standing slot: what `+0x7de` looks up.
  - `+0x7de(g, i)` turns i (the i-th living monster of group g) into its screen slot (the slots 0 … h55 `+0xe0[g]` with
    w ≠ 0, counted); fewer than i + 1 standing gives slot 0, and a slot with w = 0 returns at once (no sparkle).
  - **Count** = damage / 32 + 1 (`divs.w #32`, truncating; 0–31 → 1, 32–63 → 2, …).
  - Each sparkle: the spot `+0xa54` (rect at h55 `+0xc8`): x = monster x + rnd(w − 16), y = monster y + rnd(h − 16),
    16 × 16. **rnd(n)** (`+0xa0e`) = 0 for n = 0, else r / (32767 / n) (signed long division), r = the game's `rand()`
    (h182 `+0`: seed · 3677 + 3 mod 32768, 0…32767); the top few r give n itself, so the sprite can end on the monster's
    right / bottom edge. Then h89 `+0x1cc` (the sound below; it blocks), `h77 +0xf2a(x, y)`, `Delay(18)` (Speed does not
    change it), `h77 +0x108e`, h89 `+0x1da` (bare `rts`).
  - **The sparkle is a hardware sprite**, not a blit, so it draws over everything and erasing leaves the playfield
    untouched. `h77 +0xf2a`: WaitTOF; Intuition `SetPointer(window, h69 +0xd4, 1, 1, 1, 1)` (a blank one-row pointer, so
    sprite DMA can come on without showing the mouse pointer); `DMACON = 0x8020` (sprite DMA on); `GetSprite(SimpleSprite
    h79 +0xec, 4)` (the number to h79 `+0x4e8`; −1 shows an error requester h78 `+0x1ce`); height 16, x, y into the
    SimpleSprite; SetRGB4 of registers 16 + 2·(num & 6) + 1 / + 2 / + 3 (sprite 4: 25 / 26 / 27) = `EE0` / `E00` / `EB0`;
    `ChangeSprite(vp, sprite, h69 +0x154)`; `MoveSprite(NULL, sprite, x, y)`. `h77 +0x108e`: `FreeSprite`, `DMACON =
    0x0020` (sprite DMA off: the pointer stays off for the round), `SetPointer(window, h69 +0xe0, 16, 16, 0, 0)` (the
    normal pointer back).
  - **The shape** (h69 `+0x154` = file `0x1957c`; extracted to `extracted/ui/combat-sparkle.png`): SimpleSprite data,
    two control words, 16 rows of word A / word B, two end words. Every B word is 0, so only colour 1 (`EE0`, yellow)
    is drawn; `E00` and `EB0` are set but unused. Row 15 is empty. Rows (word A): `8008 1922 16a0 0298 4a98 0930 6ca8
    4808 3274 13a6 2d90 6952 0420 2208 0840 0000`: a scatter of single yellow pixels.
  - `MoveSprite(NULL, …)` places the sprite relative to the View; the screen sits at TopEdge 0, so screen pixels
    **`[C]` live (s134,** `working/capture_s134_spells.md`, shot `p1-amiga-combat-20`): FIREFLASH IV on a Viper (144, 125,
    32 × 22), "TAKES 78 DAMAGE" after 3 sparkles (78 / 32 + 1), each the 70-pixel template, at offsets 3…16 / 0…4
    inside [0, 16] × [0, 6]; each on 0.36–0.38 s, 0.18–0.20 s apart (sound 30's hold), the first 0.40 s after the CASTS
    line, the TAKES line 0.02 s after the last. **s153 (static + live with sound):** the 0.18 s is sound 30's hold (0.10,
    audible 0.084) plus h77 `+0xf2a`'s 4 WaitTOFs before the sprite shows; `+0x108e` takes it off with 3 more (the sprite
    up for the first); built: `sparkle.on_frames` 4 / `off_frames` 3 (`P1CombatFieldRun`), with the new sound holds. **s135 live** (`working/capture_s135_combat.md`): MINDBLAST I (MANGAR) on a
    Viper: one sparkle, the template exact at (147, 128) = Viper + (3, 3) (shot `-21`), "VIPER TAKES 1 DAMAGE"; FLAMEBOLT
    IV (VILMAR, INT 18): two sparkles (video), "VIPER TAKES 53 DAMAGE". Both fit damage / 32 + 1; with the Viper's armour 3 (DOS
    table) the roll before armour (56) gives 2 as well. **s136 live `[C]`** (`working/capture_s136_combat.md`): I's monster
    table is h3 `+0x14` (75 records of 0x3a B, name first, armour word `+0x14`; Viper h3 `+0x170` = file `0xde4`, armour
    3 as DOS), copied per group to h4 `+0x16e` + g·0x3a whose `+0x14` h38 `+0x222` subtracts (damage < 1 → 1 in `+0xcf6`).
    With every armour patched to 30, FLAMEBOLT IV on a Viper: two sparkles then "VIPER TAKES 11 DAMAGE" (after armour 11
    → 1; before 41 → 2) and two then "TAKES 14" (44 → 2): the count is the roll before armour, as built.
  - The sound (h89 `+0x1cc` → `+0x132` → `+0x304`): h90 `+0x299` (file `0x29a31`), a whole script `07 fe 08 0f 01 06 82
    03 08 0a 01 07 82 01 ff`: channel A only (B off), PSG period `0x600` volume 15 for 3 ticks, then `0x700` volume 10 for
    1 tick. Register 0 is never written; `+0x304` zeroes its register copies (locals) at each call, so the low byte is 0
    `[C]`. The remaster's pack calls it sound 30 (not a table entry).
- A monster dies (`h38 +0xe3a`): its rect filled pen 0 and w := 0, then sound 16 and "%s DIES".
- A member dies (`h38 +0xbb4`): status, HP := 0, "%s DIES" (no sound, no wait), then `+0x85e(m, 0)`: its slot filled
  pen 0, the **grave** (16×19, 4 planes at h54 `+0xe` = file `0x167aa`, kind 9) at slot + (8, 13), sound 15 (68 ticks),
  `h62 +0x576(30)` (30 polls), and the row re-laid without the dead (`+0x478(1)`: the old row's span filled pen 0, the
  gap rule over the remaining members, each blitted after a WaitTOF).
- **Round end (s136, static `[C]`).** A round the monsters survive ends at h35 `+0x16ac`: `h38 +0xe72` re-ranks (h4
  `+0x132` sorted by each group's last index, largest first, `≥` so a tie goes to the later group, empty groups last:
  DOS's `0x19d7` rule, `combat-resolution.md`; its result is always 1, the first group passes `last ≥ −1`), so `h71
  +0x1050` **always** re-runs the set-up (the row as it stands, the groups re-laid by the living count; its fill clears
  the line at row 185), then `h53 +0x2da`: **sound 18**, the pointer on again (`h77 +0x111e`), state 4. The pointer is off
  from FIGHT (`+0x2c2`). The other exits play `+0x2da` only: when no monster is left (h4 `+0xfa` = 0, checked at `+0x104a`
  before each actor and after `h38 +0xcf6` at `+0xfc4`) → state **12**; when no member is left (h47 `+0x172` = 0, after
  each monster's line / spell: `+0x1194`, `+0x1402`, `+0x1502`, `+0x15d0`) → state 6. **State 12 = h44 `+0x614`, the
  fight's end:** `+0x46e` (only with a 7th member), `+0xfc` (the row's slots, the groups emptied), `+0x274` (the flag is
  clear: no sound 17, the set-up: fill, the row, the frame), then "MONSTERS KILLED" when no monster is left (`+0x66e`),
  the shares (`+0x736`), "SPOTS AN ITEM" (`+0x8d8`), the fight's end `+0x964` → `h53 +0x2a4`. Live: s135 shot `-22` (the
  field with only its first member, no line, the pointer already back) and `-23` (no line under requester 23); s132 r1
  "VIPER DIES" 5.04 → set-up 6.08 = 10 polls 0.24 + sound 18 (~0.86, s128) `[C]`; s135 (the kill by FLAMEBOLT) 9.16 →
  10.62 is 0.36 s longer, not explained; s153's FLAMEBOLT IV kill (with sound): "DIES" → set-up 1.03 s, as s132's
  melee kill, so the 0.36 s did not come back (s135's mark likely included a sparkle) `[~]`. **Built s136:** `P1CombatFieldRun` (`RoundEndSetupMs`, `State.Relaid`):
  a surviving round's set-up after its last line, then 18; a won fight 18, then the row-only set-up, then its boxes; a
  lost one 18 only; the line cleared from the set-up on; `UiOverlays.P1CombatField` draws the field as it stands after
  the round during it.

**The s128 "party flash" is the redraw `[~]`.** No h53 routine clears planes 0 and 1. The frames of s128's round
(`r3/`: "BANTIR MISSES" at 8.67 s, the "flash" at 8.90, the field empty but member 1 at 8.94, all six by 9.02, the
pointer at 9.88 after sound 18) fit `h38 +0xe72` → set-up: the RectFill (blitter, plane by plane) caught part done,
then the member-by-member blits. The 0.23 s between the line and the redraw fits h3 = 10 polls (boot) `[~]`.

**Party Options and the orders requester.**
- The menu strip (h81): "Info", "Party Options" (Beg mercy, Threaten, Greetings, Fight, Flee, Show names, Party
  health → `h71 +0x36` returns 1–7), "Speed" (Fast, *Normal, Slow). Beg / Threaten / Greetings messages go to
  requester 23 (Left 33, Top 67, 255×47, backfill pen 10, two pen-12 borders, three pen-15 lines at (8, 13 + 8k)).
- **Requester 23 (s133, static) `[C]`.** Three openers in h44: `+0x466(text)` (rows 101 / 102 / 103 = blank, text,
  blank: one line takes the **middle row**), `+0x4d4(a, b)` (a, b, blank), `+0x556(a, b, c)`. Each text line first goes
  through **h65 `+0x1f4(text, buffer, 40)`**, which centres it in 40 cells: ⌊(40 − strlen) / 2⌋ spaces in front
  (`divs.w #2`, signed; none when the text is longer than 40), then the text; h23 `+0xd28` copies the result into the
  IntuiText (LeftEdge 8, screen x 41). So a line's ink starts at x 41 + 6·⌊(40 − n)/2⌋ (s135 live, shots
  `p1-amiga-combat-23` / `-24`: "MONSTERS KILLED" x 113, "[original text]" 107, "140 XP AND 40 GP" 113, z6font
  pixel-equal; this corrects s133's "no centring, left at x 41"). h65 `+0x1f4` has no other caller. Each calls `h80 +0x546(0x17, h3 + 60·rows)`
  (`+0x1468`: h3 + 360, a list). `h80 +0x546` notes `CurrentTime`, opens the requester and loops: GetMsg on the window's
  port, a GADGETUP (class 0x40) would end it, every other message (keys, clicks) is replied and **swallowed**; else
  `+0xd2a(limit)` with limit = (n / 60)·10⁶ µs (h202 `+0` unsigned divide, h205 `+0` multiply), so the box stays
  **⌊(h3 + 60·rows) / 60⌋ whole seconds**: 1 s for a line, 2 s for two at boot (10), Fast (1) and Normal (30); 2 / 3 s at
  Slow (80). Live (s132 r1): "MONSTERS KILLED" 0.94 s, the shares 1.92 s `[C]`. **The clock quirk (s136, static `[C]`).**
  `+0x546` takes `CurrentTime` (intuition LVO −0x54 stub h237 `+0x50`) into h82 `+0` / `+4` when it opens, then loops
  without waiting: GetMsg, and only when the port is empty `+0xd2a(limit)`: `CurrentTime` again, now = (secs & 7)·10⁶ +
  µs, then = (h82 `+0` & 7)·10⁶ + h82 `+4`, `cmp.l limit, now − then` / `bcs`: the box stays while now − then < limit,
  **unsigned**. Once the clock's seconds wrap from 7 to 0 (a multiple of 8 s since the clock's zero) now − then is negative,
  i.e. huge unsigned, and the box closes at the next poll. So a box shows **min(its whole seconds, the time to the
  clock's next multiple of 8 s)**; the longest wait any caller asks is 7 s (`+0x1468`: h3 + 360 at Slow), so the wrap is the
  only way the compare goes wrong. The clock is Intuition's (the time of the last input event; input.device sends timer
  events ~10 a second, so the close lands within ~0.1 s of the wrap `[~]`); no game value sets it (no `SetClock` in the
  start-up sequence: it counts from boot `[~]`). Likely live: s135's shares box lasted 1.52 s, not 1.92 `[~]`. Built s136:
  pack `combat_field.message.clock_wrap_seconds` 8, `CombatFieldMessage.ShownSeconds`, `P1CombatFieldRun.Plan(clockMs:)`;
  the presenter takes the clock from `P1ClockMs` (the Client passes the host's `Environment.TickCount64`, a clock from
  boot like the Amiga's `[~]`).
  Callers in a fight: h44 `+0x1c8` "[original text]", `+0x24c` "[original text]", `+0x25a`
  "[original text]", `+0x2a8` "[original text]", `+0x3a6` the morale check's "MONSTERS RUN" (a spell's MONSTERS RUN
  is h35's field line with sound 2), `+0x66e` "MONSTERS KILLED", `+0x736` "[original text]" / "%ld XP AND %ld GP",
  `+0x8d8` "%s SPOTS AN ITEM". **Show names (s154, static `[C]`):** h44 `+0x138c` (Party Options' 6th item) blanks slots
  101–103 (h45 `+0xed`, ""), then for k < h4 `+4` (the group count) with g = h4 `+0x132`[k] and h4 `+0xfc`[g] ≥ 0 (the
  living count − 1) `sprintf(h45 +0xee "RANK %1d : %1d  %s(S)", k + 1, count, name h4 +0x16e + 0x3a·g)` into the next slot
  (h23 `+0xd28`, **not** through h65 `+0x1f4`: no centring, the first rank on the **top** row), and shows requester 23 for
  `h80 +0x546(0x17, h3 + 360)` at `+0x1468`: ⌊(h3 + 360) / 60⌋ s (6 s at boot, Fast and Normal, 7 at Slow), the 8-s
  clock cut, keys swallowed. The number is k + 1 with a skipped (wiped) group keeping its number; the round end's re-rank
  (h38 `+0xe72`) puts empty groups last, so at Party Options the numbers are the living ranks' 1, 2, 3, as DOS's and
  Core's `lines.Count + 1`. I has at most three groups (`EncounterBuilder.MaxGroups`), the box's three rows. **Built
  s154:** pack `combat_field.message.list_ticks` 360 (`CombatFieldMessage.ListSeconds`); on I's field the presenter
  times the list (`CombatPresenter.OpenInfoBox`, closed by itself after the seconds) and the Client draws it as
  requester 23 from the top row at x 41 (`UiOverlays.DrawP1NamesList`); a key or a click still closes it at once (the
  Amiga swallows them) `[~]`. Party health: requester 21 at Top 45 (s158 live; "Requester 21 on the map", the fight's Party health). Others (h41 dungeon events, "The dungeon's requester 23"; h74 encounters, "The map screen's requester 23"; h50 potions; h59) use it
  outside the fight. **Built s133:** pack `combat_field.message` (`keys`, `outside_round_keys`, `joins`, `extra_ticks`),
  `P1CombatFieldRun` boxes (after the round's end: sound 18, then state 12's set-up, s136), presenter
  interludes for a party option's answer and the morale check (keys swallowed).
- Fight (`h71 +0x90`): requester **21** moved to Top 23 (Left 33, 255×110, backfill pen 10 `0AE`, two pen-13 borders
  (0, 0)–(254, 109) and (1, 1)–(252, 108)). `h47 +0x172` fills h4 `+0x14` with the non-dead members in record
  order; the loop keeps only those with **status 1 (OK)**, swapped to the front, one row each: gadget 11 + k at
  (7, 25 + 8k), 241×7, IText pen 15 / 10 JAM2 at (1, 1): `"%-10.9sHP:%-4.3dMP:%-3d "` (h72 `+0x10`) plus the order
  (`+0x446`: "THRUST" / "ATTACK" / "SLASH" / "LUNGE" with " AT RANK n" when asked, the spell, "PARRY"). FIGHT = gadget 1
  (GRELRIGHT / GRELBOTTOM −245, −18, 235×9: screen (42, 114)–(276, 122) fill pen 15, two pen-2 borders out to
  (40, 112)–(278, 124), "F I G H T" pen 0 at +90, +2). Screen: box (33, 23)–(287, 132), rows from (41, 49).
- **Why only members 3, 5 and 6 in s128 `[C]`:** the window lists OK members only; members 1, 2 and 4 were asleep
  (the row still showed all six, so none was dead, and the round printed "LIZ AWAKENS"). DOS OV2 `0x6ab9` lists the
  same members (the remaster's `CombatPresenter.IsOrderable`): not a difference.
- A row click (`h62 +0x48`) → requester **24** (`h62 +0xcc`, Left 33, Top 131, 255×43, backfill 10, pen-13 borders):
  six 70×9 buttons at (10, 8), (93, 8), (176, 8), (10, 26), (93, 26), (176, 26) (fill pen 15, two pen-2 borders,
  texts "  THRUST  ", "  ATTACK  ", "   SLASH  ", "   LUNGE  ", "    CAST  ", "   PARRY  " pen 0 at +2, +2, h63);
  the pick is gadget − 11 (0 THRUST … 5 PARRY). A rank question (`h71 +0x318`: h4 `+6` > 1, the groups with a living
  monster counted by h44 `+0x1232`, and the record's word `+0xdc` ≤ 0, i.e. **class** (DOS `@220`) 0 = THIEF; s136 `[C]`:
  DOS's `pick_rank` and Core's rule, no difference) opens requester **26** (Top 131, 255×29;
  title pen 0 at (49, 13); digit buttons 11×9 at (160 + 12k, 11), `h62 +0x170`); Cast opens the spell list
  ("[original text]", requester 12, h84 `+0x4506`; read s133, "The spell list" below).
- Encounter set-up: state 11, h44 `+0x0` → `+0xca` → h71 `+0x1050` (above, "The fight's start"); h44 `+0x614` is state 12,
  the fight's end.
- **Seen on FS-UAE (s132,** `working/capture_s132_combat.md`, shots `parity/shots/p1-amiga-combat-00`…`14`): every
  place above matched (field, frame, party x 18 + 50k / 26 + 58k, groups y 147 − h stacked 3 rows apart, the line in
  pen 3 at 185, requesters 21 / 24 / 26 and the FIGHT button), monsters and portraits pixel-equal to the `hw4`
  extracts. Also: the menu strip at rest is the screen title in pen 13; held, Intuition's strip in pen 15 (Party
  Options box (55, 9)–(192, 84), items from row 13, 10 rows apart; Speed (183, 9)–(260, 44), its `*` is Normal's own
  text); the rank buttons' border is pen 1 (not 2); a picked row is not highlighted; the requesters' font is 6 px a
  character; a group is re-laid by its living count at the next round's set-up. Line waits 0.24 s at boot (10 polls),
  0.66 s after Speed › Normal (30) `[C]`; attack frame 0.36–0.38 s; a monster's "DIES" 0.28 s after its erasure; a
  member's death: portrait erased, grave, row re-laid 2.36 s later (sound off in the emulator). The "flash" is the
  field fill caught after planes 0–1 at every set-up `[C]`. Fight end: requester 23 "MONSTERS KILLED" 0.90 s, "EACH
  SURVIVOR GETS / n XP AND n GP" 1.92 s. One ATTACK pick came back THRUST: **DOS's gate** (s136 `[C]`): h71 `+0x608` reads
  the record's `+0xfa` (DOS `@250`, the fight's attack skill) and turns ATTACK below 75 into THRUST, SLASH (`+0x656`)
  below 150 into the ATTACK check, the label rewritten at once; D-011 (the remaster: the manual's no-gate, option
  `p1.attack-gate` = `dos` for this).
- **Built s132:** pack `combat_field` (`CombatFieldLayout`: places, pens, timings, the line sounds by message and side,
  menu strip, requesters 21 / 24 / 26 / 23), Client `P1CombatFieldRun` (a round line by line: attack frame, the caster's
  silhouette, the sounds holding the screen, the text, the polls; a member's grave and re-laid row; sound 18) and
  `UiOverlays.P1CombatField.cs`, behind AMIGA SCREENS (Options row now for I too); Core tags I's swing, waking and cast
  lines (`CombatLineTag`, presentation only). Normal keeps the boot 10 polls until a Speed is picked in the game.
  Preview: `./play.sh --test-level 5 --p1-combat N`.

**The spell list and "ON WHOM:" (s133, static; requester 12 checked on shot `-07`) `[C]`.** Cast (h71 `+0x73a`, order 4):
the title text (slot 0x56) := "[original text]" (`h23 +0xd28`), then `h23 +0x570(record +0x60, −(record +0x12))`: the
60 spell words and minus MP now.
- **Pens on screen ≠ pens in the structs.** `h77 +0x112e` (from h2 `+0x6c` / `+0x144`, on leaving the town menus; `+0x1230`
  undoes it with h69 `+0x1ac`) maps every requester **0–19**'s backfill, borders 0–52 (h84 `+0x186a`, 16 B each) and
  ITexts 0–171 (h84 `+0x1d2a`, 20 B each) through h69 `+0x19c` = `0 12 1 14 3 10 2 9 4 7 6 13 8 11 5 15`. Requesters
  20+ (21 / 23 / 24 / 26) are not mapped, which is why their struct pens were the screen's (s132). Pens below are the
  screen's, struct pen in brackets.
- **The list** (`+0x570`): every slot whose word is 1 (known), in ascending slot order: no combat or place filter
  (MONSTER EVAL. was listed live): DOS's picker, D-063 `dos`. None → −1 at once, no requester → PARRY.
- **Requester 12** (h84 `+0x4506`): Left 46, Top 57, 228×111, backfill 2 [6] (`E00`, red); borders 10 [5] (`0AE`)
  (0, 0)–(227, 110) and (1, 1)–(225, 108); title pen 0 JAM1 at (15, 12) = screen (61, 69); the list's frame pen 0
  (7, 28)–(191, 82) = (53, 85)–(237, 139); its fill two 106-point PolyDraws in 11 [13] (`04E`), 125 px wide at (8, 29) and
  (66, 29), 53 rows = (54, 86)–(236, 138).
- **Rows**: gadgets id 11–16 (h84 `+0x2dba` index 0x23–0x28), (12, 33 + 8k), 176×7, GADGHCOMP, RELVERIFY | GADGIMMEDIATE;
  IText pen 15 on 11 [13] JAM2 at (1, 1) = text at (59, 91 + 8k): `"%-29.29s"` (h24 `+0x113`) of the name table h12
  `+0xe18`. Six a page; a row past the list gets spaces and is made inert (`+0xd9a`: GADGHNONE, GADGIMMEDIATE off).
- **MP**: a row whose cost (slot % 4 + 1) is above the MP is made inert by `+0xd9a` too, but `+0xe6e` → `+0xde6` then
  makes the same gadget live again: it is listed, drawn like the rest and can be picked; h71 `+0x82c` turns the pick into
  PARRY (Core's chooser does the same).
- **Scroll gadgets** id 2 / 3 (GRELRIGHT | GRELBOTTOM −26, −80 / −26, −38, 18×9 = screen (247, 87) / (247, 129)): two
  borders 12 [1] (`EBB`) at −2 and −1, fill 15, IText pen 0 at (6, 2) with text slots 0x45 `^` / 0x46 `@` in the game's
  font: z6font's own `^` / `@` (s134: every pixel of both on `-07` once the ghost dots are taken out; the s133 measure
  had them wrong). Each is GADGDISABLED (Intuition's ghost:
  a pen-0 dot every 4 px, odd rows shifted 2, shot `-07`) when it cannot move (`+0xcd0`: up while the page starts at 0,
  down while no spell lies past it). Up: start −= min(start, 6); Down: start += 6; the list is rebuilt and refreshed
  (RefreshGList).
- **EXIT** id 0 (GRELRIGHT | GRELBOTTOM −98, −18, 64×9 = (175, 149)): borders 1 [2] (`A42`), fill 15, "EXIT" (slot 8)
  pen 0 at (20, 2) = (195, 151). → −1 → PARRY (h71 `+0x936`).
- **GREL gadgets sit at Left + Width − 1 + LeftEdge, Top + Height − 1 + TopEdge** (EXIT and both scroll gadgets on `-07`;
  s132's FIGHT agrees).
- **Input** (`h86 +0x132`): only a GADGETUP of a gadget with GADGIMMEDIATE counts; no keys. **The tone** (s134 `[C]`):
  h23 `+0x734` calls `h89 +0xd2` right after `h86 +0x132` returns, before it acts on the event (page rebuilt, list
  closed): the script h90 `+5` (file `0x2979d`: `00 2d 01 02 08 0f 07 fe 82 03 ff` = A period `0x22d`, volume 15, B
  off, one step of 3 ticks), not in the numbered table; the player blocks. So a live row (an over-MP one too), a
  scroll gadget that can move and EXIT each play it; an inert row (no GADGIMMEDIATE) or a ghosted gadget gives no event.
  `h62 +0x508` ("ON WHOM:") does not call `+0xd2` (h89 `+0xd2`'s callers: h23 ×6, h26 ×7, h29 ×7, h65 ×1). Built s134:
  pack sound 31, `spell_list.spells.tone`, `CombatPresenter.P1Tone` (the event's result waits for the tone's hold).
- **After the pick** (h71 `+0x7d0`, the requester closed): the spell must be known and cost ≤ MP, else PARRY. Spells 5+
  (1-based) → order 100 + s, the member's order text = the spell's name (`+0x83e`). HEALING I–IV → `h62 +0x508("ON WHOM:",
  4)`.
- **"ON WHOM:" = requester 6** (h84 `+0x4266`, opened by `h86 +0(6)`): Left 43, Top 46, 233×107, backfill 0, borders 1 [2]
  (`A42`); title (slot 0x26) pen 15 JAM1 at (7, 13) = (50, 59); rows gadgets id 11–17 at (6, 28 + 8k), 224×7 = (49,
  74 + 8k), IText pen 15 on 0 JAM2 at (1, 1). `+0x28e` mode 4 lists **every** party record in record order (DEAD and
  asleep too): `"%-10.9s%-6.5sHP%4d/%-4dMP%4d/%-3d"` (h63 `+0x48`) = name, status (h12 `+0xb78`: DEAD / OK / SLEEP), HP
  `+0x16` / `+0x14`, MP `+0x12` / `+0x10`. Row k = member k → order s·1000 + k; EXIT (the shared gadget, −71, −17 =
  (204, 135)) → PARRY. No tone here. Seen live s134 (`p1-amiga-combat-19`: a DEAD member listed "DEAD HP 0/8") `[C]`.
- **Built s133:** pack `combat_field.spell_list` (`CombatFieldSpellList` / `CombatFieldList`: both requesters, screen pens,
  the glyphs and the ghost), drawn by `UiOverlays.P1CombatField.cs`, keys in `CombatPresenter` I mode (the Amiga is mouse
  only; the remaster: 1–6 a row, Up / Down the scroll gadgets, E EXIT, Backspace back with no change; clicks on the
  gadgets), replacing requester 26's typed spell number (kept for a pack without `spell_list`). The list is Core's
  (`CombatText.CombatSpellChoices`: D-063 `manual` = combat and everywhere spells; `dos` = every known spell, the Amiga's
  list). A cast's order row shows the spell's name. Shots `parity/shots/p1-remaster-combat-spells.png`,
  `p1-remaster-combat-on-whom.png` (Xvfb, `--test-level 5 --p1-combat`). Seen live s134 (`working/capture_s134_spells.md`, shots `p1-amiga-combat-15`…`19`): three pages (an empty row's
  click does nothing), the over-MP pick → PARRY, requester 6; the remaster's draw equal but the text. A word of 2 ("can
  learn") is not listed (EMILY's QUICKNESS I). The font: z6font (s134, "Fonts").

## Screens — `*.pic`

| File         | Size  | Decoded as     | Content                          |
|--------------|-------|------------------|----------------------------------|
| `cover1.pic` | 32000 | 320×200×4 planar | Title (door / PHANTASIE); palette `0x194DC` (see Title) |
| `cover2.pic` | 32000 | 320×200×4 planar | Credits (Wood; Amiga 1.0 by Majsa / Horvath); palette `0x194DC` |
| `decor.pic`  | 32000 | 320×200×4 planar | Party-defeat hall; combat palette `0x194BC` ×17 (s124, below) |
| `pelnor.pic` | 32000 | 320×200×4 planar | Town of Pelnor; palette `0x1947C` (s120, town text below) |
| `final.pic`  | 32000 | 320×200×4 planar | Ending art (Zeus); combat palette `0x194BC` ×17 (s124, below) |
| `logo.pic`   | 7040  | 176×80×4 planar  | About Phantasie's logo (requester 27's Image h81 `+0x22`); map palette `0x1949C` ×17 (s156) |

**Town screen text (s102, static) `[C]`.** Town entry (`h29:0x0`, town record = `TWNS` + 292·index; arg 99 → 11)
prints the record's name (+280) with `h80:0x818(text, pen)`: an IntuiText at **TopEdge 185**, a blank line printed
first at LeftEdge 0 (the whole line cleared), then the text centred on 320 (`(320 − IntuiTextLength)/2`, `h80:0x892`).
The name uses **pen 2**. The same routine prints "LEAVING %s" (`h29:0x168`, pen 4), so a town message takes the
name's line.

**Measured on the Amiga (s120, FS-UAE A500 / KS 1.3, Pelnor on "START THIS TURN IN PELNOR"; `parity/shots/p1-amiga-town-name.png`
is FS-UAE's 2× capture, screen origin at (37, 18) of it: the picture's frame on columns 0 / 319 and rows 11 / 177).**
- Place `[C]`: "PELNOR" ink at **x 130–164, y 185–189**. The top row is TopEdge 185 `[C]`. The x is
  `(320 − 6·10)/2` = 130: the record's name (+280) is a **10-character field padded with spaces** (`PELNOR    `,
  `twns.dat`), and `IntuiTextLength` counts the trailing spaces, so every name's left edge sits at 130 rather than the
  trimmed name being centred (STARVILLE is stored as 9 characters + NUL: left edge 133).
- Font: not topaz 8 (the menu bar is topaz). 6-pixel advance, capitals 5 rows tall, 1-pixel strokes: the disk's own
  `fonts/z6font/6` (size 6) `[C]` (s134, "Fonts": the window's font).
- Colour `[C]`: **`0xE24`** = `#EE2244` on screen (×17 per gun). Town entry loads its own palette: `h29 +0x2e`
  `pea h69+0x54; jsr h77+0x466` (the LoadRGB4 routine of the title), also from `h20 +0x186`. That is file
  **`0x1947C`**: `000 E00 E24 800 EE0 264 0E0 A20 0EE 0AE 04E 666 444 AAA 888 EEE`, so pen 2 = `E24`. (s102's
  "`#FF0000` in both palette tables" read the combat / alt tables, which the town does not use.) `pelnor.pic` decoded
  with this table (×17) equals the capture pixel for pixel, apart from the pointer, the party figure and the name.
- Remaster, before s120 (5× shot on Xvfb): ink x 142–176, y 185–191 (remaster-5x7, 7 rows), `#FF0000`; the street in
  the combat table through the house LUT (pink tower, orange wall, blue road). s120: pack `town_screen.name_field` 10
  pads the name to the field before centring (left edge 130; STARVILLE 3 px left of the Amiga's 133 `[~]`),
  `name_colour` `#EE2244`, and `pelnor-town.png` is extracted with `0x1947C` ×17 (`extract_amiga_graphics.py`); after
  (`parity/shots/p1-remaster-town-name.png`, 5×, origin (160, 16)): ink x 130–164, y 185–191, `#EE2244`. Still
  different: the remaster's 5×7 font (7 rows, the pack's `font`) against z6font's 5 rows. **s135:** the town line is
  z6font: "PELNOR" pixel-equal at (130, 185) on an exact FS-UAE shot (`parity/shots/p1-amiga-town-pelnor-exact.png`);
  `h80:0x818` draws on the window's RastPort, whose font the boot code sets to z6font, so "LEAVING %s" and every other
  town message on that line use it too. **s153:** "LEAVING PELNOR" caught on a disk copy whose h29 `+0x1ac` (`jsr h92 +0`,
  the next step after the print) is patched to `bra.s *` (shot `parity/shots/p1-amiga-town-leaving-pelnor.png`, file row
  = screen row + 5): z6font, pixel-equal at (106, 185), `#EEEE00` (pen 4 of `0x1947C`); 106 = (320 − 6 · 18) / 2, the
  name's 10-character field counted, as PELNOR's. **s154, when (static `[C]`):** h5 `+0` (the town run) calls h29 `+0x168`
  at `+0xde`, after its town loop (`+0x5c4`) returns: `sprintf("LEAVING %s", record +0x118)`, h80 `+0x818(text, 4)`, then
  `bsr +0x58` (the save: three files) only when h30 `+0x2a` is set (Town Options › Auto save toggles it, `+0x1b4` via h5
  `+0x6e6`; 0 at boot), then `jsr h92 +0` (a bare `rts`): no wait of its own; h5 returns 2 and the overworld loads. **DOS
  OV1 `0x483e`** (called once, `0x1e68`, after the town loop `0x1e72`): clears row 24 (`0xb9d1(0, 0x18, 0x27, 0x18, 0, 0)`),
  prints "LEAVING %s" (imm `0xc284` = file `0x628d` + `0x5ff7`) of the record's `+0x117` name at column 11, row 24
  (a print routine outside OV1: the call lands before the overlay), then saves (`0x4562(0)`) when `[0x3092]` is set (toggled by `0x459f`); the same order as the
  Amiga's. **Filmed s154** (`working/capture_s154_leaving.md`): the line 0.16–0.17 s after the gate click, on the town
  picture for 2 film frames (1–3 Amiga frames `[~]`), then the whole screen black 2.1–2.35 s, then the map in one frame
  (the bar a frame later); the live frame equals the patched shot (`p1-amiga-town-leaving-pelnor-live`). **Built s154:**
  pack `town_screen.leaving_colour` `#EEEE00`, `leaving_seconds` 0.033 (the two frames), UI string `p1town.leaving_x`; under
  AMIGA SCREENS a P1 town or Guild → overworld change holds the town picture with the line (`Game1.NoteLeaving`,
  `UiOverlays.DrawTownLeaving`, x 106 in z6font, test `P1ReportWindowTests.Leaving_IsPenFourAtOneHundredSix`), keys and
  clicks waiting. **Built s156:** the black after it, pack `town_screen.leaving_black_seconds` 2.24 (the two films' 2.35 /
  2.14 s, mean; the map's load likely `[~]`), the whole screen black, keys and clicks still waiting
  (`TownScreenLayout.LeavingAt`, test `P1ReportWindowTests.Leaving_ThenTheFilmedBlack_ThenTheMap`); the Amiga's pointer
  shows for its first 1.6–1.8 s, then vanishes `[?]` (the remaster's pointer is not changed). The DOS path does not
  print it (the DOS-path package's).
  Built: pack `town_screen.font` = `amiga-z6font-6`; the Client draws the name and the line's messages (not the
  remaster's key help) in it behind AMIGA SCREENS (`UiOverlays.TownTextFont`); the remaster's PELNOR is pixel-equal to
  the Amiga's (`parity/shots/p1-remaster-town-name-z6font.png`, 5×, origin (160, 14)). Messages keep the remaster's
  colour (the Amiga's "LEAVING %s" is pen 4 `EE0`, built s154; other messages' pens not read) `[~]`.

Remaster pack: `town_screen` name (160, 185), `name_field` 10, `#EE2244`, `message_y` 184 (text row 185), `font` z6font.

**Defeat and ending pictures (s124) `[C]`.** Both are shown only by hunk 32 (`h33` holds their names):
- `decor.pic`, party defeat (h32 `+0x55c`): sprite DMA off, `h77 +0x4c0` (black table h69 `+0x34`), read and blit
  (`h77 +0x524`), the door sprite, then **LoadRGB4 h69 `+0x94`** (`0x194BC`, the combat table) via `h77 +0x466`; then
  "[original text]", sound 23, the door opening 90 steps of 4 WaitTOF. The `DECOR.PIC` at h33 `+0x114`
  (h32 `+0x150`) is only Open + Close (an existence check), not a display.
- `final.pic`, the ending (h32 `+0x6d0`): black, read and blit, then the 16 pens of the same h69 `+0x94` table set one at a
  time (`h77 +0x288`, 10 WaitTOF apart: a fade-in of 160 VBLs, 3.2 s at 50 Hz, 2.67 s at 60 Hz), `Delay(120)` (2.4 s), then
  the party row and the speech (below, "The victory screen").
- So both are on screen in the combat table at 4-bit hardware guns: extracted with `c · 17` (`hw4`), not the house
  Amiga→ST LUT. **Measured** on FS-UAE with a test disk whose title shows them (`tools/scripts/p1_amiga_patch_title.py
  ADF decor.pic final.pic 0x94`; same loader `h77 +0x524` and LoadRGB4 as the real paths; final's real fade ends on the
  same 16 words): `parity/shots/p1-amiga-decor.png` / `p1-amiga-final.png` (4×, crop = picture x 40–279, y 25–174) equal
  the new extraction in all 36,000 sampled pixels each; the old LUT extraction matched 41 % of final.
- The combat screen loads the same table the same way (h53 `+0x33c`), so the monster / portrait sprites are also ×17 on
  the Amiga; extracted that way since s125 ("Sprite colours on screen").
- Pack: `ui.decor` → `decor.png`; since s125 also `ending.final` → `final.png` (`PictureNames.EndingFinal`, the key II's
  ST pack already used). s153: the Client draws I's defeat screens under AMIGA SCREENS ("The party defeat" below).

**The victory screen, h32 `+0x6d0` (s127, static) `[C]`.** Hunk numbers are the relocation targets; h89 is the sound
module, h234 / h236 dos / graphics stubs.
1. `h89 +0xee(22)` (a sound: entry n of the table at h91 (BSS) `+0x1f4`, played by `+0x304` and waited for; "Sound effects" below),
   black (`h77 +0x4c0`), `final.pic` read and blitted (`h77 +0x524`), the row laid out (`+0x36e(1)`), `h89 +0xee(25)`.
2. Fade-in: pen k = 0…15 set to word k of h69 `+0x94` (`h77 +0x288`), 10 WaitTOF after each. `Delay(120)`.
3. The ghost (h69 `+0x994`, 32×32, kind 2) in slots 0…n−1 (`+0x43e(0)`), n = members with status 1 (OK).
   `h89 +0xee(1)`, then Zeus' raised arm (h69 `+0x3694`, 32×37, kind 8) at (160, 48) over `final.pic`.
4. Per party record i (copied to the roster, `0x1a44`, 282 B): status 1 → DIVINE I (`+210 := 1`), its portrait over its
   slot (`+0x46c(i, 1)`: race `+256`, 512 B each from h55 `+0xd0`), `h89 +0xee(21)`, `Delay(120)`.
5. The 7 lines (h33 `+0xf8`, upper case: "WELL DONE, MY SMALL FRIENDS. YOU HAVE" / "REMOVED A GREAT EVIL FROM GELNOR." /
   "YOUR QUEST IS OVER, NOW GO AND REST TO" / "PREPARE YOU FOR THE NEXT ADVENTURE." / "I GRANT YOU YOUR FIRST DIVINE
   SPELL." / "YOU WILL LEARN LATER HOW TO USE IT" / "PROPERLY IN PHANTASIE II."), each by `+0x340`: `h80 +0x84a(text,
   5, 192)` (the line cleared, the text centred on 320 at TopEdge 192, pen 5) and `h62 +0x576(180)` (up to 180
   `Delay(1)` ticks, ended by a key or click, `h80 +0x4e4`).
6. `h89 +0xee(24)`, `guild.dat` written (7050 B = 25 × 282), `h89 +0xee(22)`, fade-out: pens 15…0 set to 0, 10 WaitTOF
   after each; `h77 +0x111e`; back to the town loop.
- **Party size:** the loops at `+0x36e` and `+0x7ca` run over every party record (0 … word 0), with no 7-member check,
  so an OK summoned elemental would get a slot, a portrait and DIVINE I; DOS copies at most 6 records and the remaster
  keeps DOS (`Party.Adventurers`) `[~]`.
- **Row** (`+0x36e(flag)`): gap = `h53 +0(n − 1, 32)` = (320 − 32n)/(n + 1), boxes 32×32 at y 158; with flag 1 only OK
  members count, and a slot's box is the place of the next OK member (so a dead member's slot shares it, and the ghosts
  drawn by slot index 0…n−1 can double up while the last place stays empty).
- **Built s127:** pack `ending_scene` (`EndingSceneLayout`), Core `VictoryScene` / `SessionPromptKind.VictoryScene`,
  Client `VictoryScreen` (clock) / `VictoryScreenView` (drawn in pens, so the fades act per pen); `ui/ending-ghost.png`
  and `ui/ending-arm.png` extracted `hw4`. Watched on Xvfb (`./play.sh --test-level 10 --p1-victory`). s128: the six
  sounds play and each holds the screen for its length (+0x304 blocks): 22 (30 ticks) before the black, 25 (125) on
  black before the fade-in, 1 (7) between the ghosts and the arm, 21 (31) after each portrait, 24 (7) and 22 (30)
  between the last line and the fade-out: 199 ticks (3.98 s) + 31 per granted member. s128 watched the Client on Xvfb:
  the sounds start where `VictoryScreen.SoundStarts` puts them, apart from the first frame's hitch shortening sound 22
  (fixed s156, `working/capture_s156_victory_22.md`). s130: with the Sound switch off the remaster drops the sound waits (user s129).
- **Captured live s130** (FS-UAE, patched copy of the saved disk; `reverse-engineering/amiga/working/capture_s130_victory.md`,
  shots `parity/shots/p1-amiga-victory-*`). h32 `+0x6ea` BltClears the four planes (h77 `+0x246`) **before** sound
  22, so 22 plays on a pen-0 (black) screen, as in the remaster `[C]`. Measured (six OK members): 22 0.65 s, `final.pic`
  read 3.45 s on black, 25 3.45 s, fade-in 2.67 s, `Delay(120)` 2.40 s, ghosts → sound 1 → arm and portrait 0 in
  one frame 0.27 s, each portrait 3.20 s (21 0.80 + 2.40), each line 3.93 s (180 `Delay(1)` polls ≈ 21.8 ms), 24
  0.20 s, `guild.dat` write 5.95 s, 22 0.60 s, fade-out 2.67 s, 3.40 s black before the town; 68.9 s in all. Each
  Dosound step costs its Delay plus ~8–12 ms `[~]`, so the remaster's Σ ticks / 50 waits are short. Keys `[C]`: one
  ends sound 25 / 21 at its current step; one in the fade-in / Hold is flushed by the next sound; one queued in a
  Delay with no sound after it ends line 0 and its release line 1; in a line a press ends it and its release the
  next. The Amiga's own line text: line 3 "…AND REST TO", 4 "PREPARE YOU FOR THE NEXT ADVENTURE.", 5 "…DIVINE
  SPELL." (remaster: DOS breaks, `core.endgame.zeus_3..5`). WaitTOF at 60 Hz (NTSC, as the title tune) `[~]`.
- **Built s131 from the capture:** each sound's wait = per step its ticks / 50 + 7 ms, then 75 ms per call
  (`sound_effects.step_overhead_seconds` / `call_overhead_seconds`, fitted on 25 and 21 `[~]`; one-step sounds come out
  0.08 s over their ticks, live 0.01–0.06); the rendered audio stretches each step the same way (the tail is silent);
  a line = 180 polls of 21.87 ms (`line_poll_seconds`); disk waits `ending_scene.disk` `[~]` (patched town-exit path):
  `final.pic` 3.45 s after 22, `guild.dat` 5.95 s after 24, the town 3.06 s after the fade-out ends (the 3.40 s of black
  live counts from the last lit pen); keys through the input port (`queued_keys`: presses and releases queue, each
  sound drains them, a key in a sound ends it at a step, a line takes one queued event); the Amiga's speech lines
  (`ending_scene.speech`, `amiga.endgame.zeus_3..5`). Six members land within 0.06 s of every r1 mark
  (`VictorySoundTests.SixMembers_LandOnTheS130Capture`).
- **s153 (60-fps film with sound, `working/capture_s153_sounds.md`) `[C]`:** h77 `+0xcfe` waits a WaitTOF before each
  blit, so the ghosts land **one a frame** (six: 17.117…17.200), the arm the frame sound 1 ends (17.367), portrait 0 one
  frame later (17.383; s130's 30-fps film saw both in one frame), each later portrait a frame before its 21; first ghost
  → arm 0.25 s with six members. A click is two input events (press and release each end a line). Static: the player
  takes one message a step and returns at a cut, so an event after the cut stays in the port. **Built s153:**
  `ending_scene.blit_vbl` 1 (`VictoryScreen`: ghosts one a frame, the arm's and each portrait's blit frame), the new sound
  holds (Sound effects), the disk waits refitted (`final.pic` 3.50, `guild.dat` 6.09, town 3.47 s), a key or release in a
  cut sound's tail queued (not dropped), I's clicks sent as press and release (`Game1`); six members land within 0.05 s
  of s153's v1 marks (`VictorySoundTests`). Left: ghosts doubling up behind a dead member not filmed (static only; the
  remaster's `VictoryScreen.Drawn` follows it) `[~]`; the first frame's hitch on 22: recorded and fixed s156 (`working/capture_s156_victory_22.md`: an uncut sound plays out); the
  live path into state 7 not played (no Amiga save near the Black Lord) `[?]`.

## The party defeat, h32 `+0` / `+0x55c` (s153, static and live) `[C]`

State 6 (h2 `+0x242`) → h71 `+0x1118` (h62 `+0xa`, state := 2) → **h32 `+0`**: h3 saved and set to 4, the row over every
record (`+0x36e(0)`), then `+0x55c`:
1. `h77 +0x110e`, BltClear, the black table, `decor.pic` read and blitted, the door (h69 `+0x1814`, kind 4, 80 × 90) at
   (123, 14), LoadRGB4 of the combat table: picture, door and colours appear in one frame.
2. `+0x340("[original text]")`: `h80 +0x84a(text, 5, 192)` (centred, pen 5) then `h62 +0x576(180)` (180
   polls of 21.87 ms = 3.93 s, a key or click ends it). Every text line of the screens goes this way.
3. `+0x43e(0)`: the ghost (h69 `+0x994`) in every slot, one blit a frame; sound 23; "YOU HAVE ARRIVED AT THE ASTRAL
   PLANE".
4. `h89 +0x7ca` (a raw tone: Paula period 6000, channel 0), the door sinks: 90 steps of 4 WaitTOF + a blit of its top
   90 − j rows at y 14 + j (live 7.17 s); `h89 +0x7f4`. h33 `+0` (defeats this run) += 1; at 5 the sign (h69 `+0x32d4`,
   kind 6, 48 × 40) at (139, 39) and 180 polls. `Delay(180)`, sound 20, the face (h69 `+0xb94`, kind 3, 80 × 80) at
   (123, 19), "[original text]".
5. Back in `+0`, per record d7: sound 21, then by `+6` (0 → destroyed) and the rolls (`+0x58` > 2 and rand / 0x2000 <
   `+0x58` − 2 → resurrected; else `+0x58` > rand / 0x2aab → undead; else destroyed): `+0x18a` (slot filled, "%s YOU HAVE
   BEEN DESTROYED", removed), `+0x24a` (portrait, "… RESURRECTED"), `+0x2a4` (race 15, portrait, "… [original text]"),
   each line by `+0x340`.
6. `h5 +0x708`, state resets, `h62 +0x576(300)` (6.56 s or a key), the pointer back, h3 restored.

**No key is needed anywhere**: every wait is a poll count a key shortens. Live (FS-UAE, `defeat.adf`: state 2 → 6;
capture s153): black 3.0 s (5.4 s the first time) `[~]`, line 1 3.93, ghosts one a frame, 23, line 2, the door 0.15 s after
its 3.93 s, 7.17 s long, sound 20 Delay(180) + 0.17 s after it, the face, line 3, fates 4.73 s apart (21 + 3.93), back
3.93 + 6.56 s after the last fate. The manual says nothing of these screens' pacing (no conflict).
**DOS** (PLAN Watch list): its defeat / Astral Plane / demon / fate screens also run by themselves (~5 s each, no key).
The remaster's non-Amiga path still shows the defeat box and the outcome as prompts that wait for a key `[~]` (not
changed s153).

**Built s153:** pack `defeat_screen` (`DefeatScreenLayout`: `picture_read` 3.0, `line_polls` 180, `poll_seconds`, `end_polls`
300, `door_seconds` 7.17, `door_lead_seconds` 0.15, `door_tail_seconds` 0.17, `after_door_ticks` 180, sounds 23 / 20 / 21,
`text_y` 192, `text_pen` 5, `blit_vbl` 1), pictures `decor.door`, `decor.demon-face`, `decor.sorry-sign` (extracted `hw4`,
`tools/scripts/extract_amiga_graphics.py`) and `portrait.ghost` = I's own ghost; Client `P1DefeatScreen` (clock, keys as
the victory's input port) and `Game1.P1Defeat.cs`: under AMIGA SCREENS the defeat box is answered at once (the Amiga shows
no cause report), the screens run on the clock with their sounds over `UiOverlays.DrawDefeatScene` (II's door and row,
the line centred in pen 5), and answer the outcome themselves. Preview `./play.sh --test-level 10 --p1-defeat`; shots
`parity/shots/p1-remaster-defeat-door.png`, `-fates.png` (Amiga window frames `p1-amiga-defeat-*-window.png`). Left
`[~]`: an elemental's slot (the Amiga's loops take every record, Core drops it).

**s154 (static h32 `+0x5b6`…`+0x6c6`, h89 `+0x7ca` / `+0x954`) `[C]`:** the raw tone is the effects' 16-byte ramp (h91
`+0x278`) on channel 0 at period `0x1770` (not scaled), volume 60, cycles 0; `+0x7ca` runs right after line 2's wait (so
under the door's 0.15-s lead too) and `+0x7f4` (FINISH, `IOF_QUICK`) right after the 90th step. Then h33 `+0` (DATA, 0 at
load, written only here, never saved) += 1; **== 5** (only the 5th of the program run, not the 10th) blits the sign (h69
`+0x32d4`, kind 6, 48 × 40) at (139, 39) and polls 180 (a key ends it), then `Delay(180)`, 20 and the face, whose 80 × 80
blit at (123, 19) covers it. The fates do not change: presentation only, no Core rule (Core already counts
`GameSession.PartyDefeatsThisRun` for every game; II's campaign reads it). Each line goes through `+0x340` → h80 `+0x84a`
(pen 5, TopEdge 192), the victory speech's routine: z6font. **Built s154:** pack `defeat_screen.raw_tone` (channel,
period, volume; `AmigaDosoundScore.Tone`, `SoundEffectPlayer.PlayTone`, played from the lead's start to the door's end, the
Sound switch on), `sign` (defeat 5, (139, 39), 180 polls; `P1DefeatScreen` step `Sign`, the Client passes
`PartyDefeatsThisRun`) and `font` z6font (the lines centred in it). Live (Xvfb, `--test-level 10 --p1-defeat`): the line in
z6font, a 37-Hz tone for 7.3 s, then 20 3.6 s after it. **s156:** the Client counts
the defeat screens of its program run (`Game1._p1DefeatScreensThisProgramRun`, as h33 `+0`: a load keeps counting). Left
**Filmed s158** `[C]`: the run's counter (h33 `+0`, chip `0x19268` in that run) patched to 4 through the UAE debugger,
then a defeat: "Sorry, we've run out of Demons" at (139, 39), every pixel of `decor.sorry-sign` (shot
`p1-amiga-defeat-5th-sign-live`, test `P1DefeatScreenTests.FifthDefeatSign_MatchesTheLiveShot`), up between 6.7 and 10 s
before the face (ashots 1.7 s apart).

`logo.pic` (s156, static `[C]`; s102 read it as 320×44): h80 `+0xcb4` ("NO MEMORY FOR LOGO") AllocMems 0x1b80 bytes,
reads the file into them and points the Image h81 `+0x22` (176 × 80, depth 4, PlanePick 0xff: plane after plane, 22 bytes
a row) at them, then h86 `+0(0x1b)` shows requester 27 with it ("Info › About Phantasie", section "The map's message
line"). Decoded so on the map palette it is a clean picture: "phantasie" in red, "VERSION 1.0", the SSI and ANDROMEDA
SOFTWARE marks, "COPYRIGHT STRATEGIC SIMULATIONS INC. 1987", "BY K.MAJSA, Z.HORVATH" on the grey of pen 13
(`extracted/ui/logo.png`, pack picture `ui.logo`).

## Title: the boot sequence (s102) `[C]`

Hunk offsets as in the map section below (`hunks()` of `extract_amiga_p3_common.py` works on `game` too).

- `s/startup-sequence`: `Stack 4000`, `run f` (a small `StealMem` helper), `echo "Phantasie (C) 1987 SSI"`, `game`.
- `game` main (hunk 2 `+0x0`) checks free memory, then calls the title routine **h77 `+0x2de`**:
  1. `h77 +0x110e`: sprite DMA off (no pointer on the title).
  2. `h77 +0x524("cover1.pic")`: read 32000 B into the screen bitmap and blit it (DMA off/on around the blit).
  3. Read `cover2.pic` into the same buffer (`hunk 20`), `AllocMem(3000)`, read `tamb.dat` (2850 B, the title tune).
  4. `h77 +0x466(h69 +0xb4)`: copy 16 colour words and `LoadRGB4` them (`h236 +0x54` → graphics LVO −192). That is
     file `0x194DC` (`PALETTE_ALT_OFF`): `000 820 EB0 AAA C22 620 048 068 A20 0EE 0AE 04E 48A 666 888 EEE`. LoadRGB4
     takes 4-bit guns, so the covers are extracted with `c · 17` per gun (`load_palette(hw4=True)`); the house
     Amiga→ST LUT would zero the odd nibbles (`EB0`, the yellow of the logos). The covers were extracted with the
     combat palette `0x194BC` before s102 (magenta stone).
  5. Flush the window's messages, then **play `tamb.dat`** (h89 `+0x304`, a byte-code tune player); each note checks
     `h80 +0x4e4`, which returns true on an IDCMP `MOUSEBUTTONS` (8) or `RAWKEY` (0x400) message, and a key or mouse
     button **ends the tune**. It lasts 2678 dos ticks = **53.56 s** (s108, "Title tune" below).
  6. `h77 +0x524("cover2.pic")`: the credits picture, same palette; `FreeMem(tamb)`; `SetPointer`; `h80 +0x0` builds
     the menu strip.
- Back in main: **`Delay(250)`** (`h234 +0xe4`, dos.library LVO −198 on `DOSBase` = hunk 1 `+0x30`, set from
  `OpenLibrary("dos.library")` at hunk 0 `+0x1c2`): 250 ticks of 1/50 s = **5 s** on PAL and NTSC alike, keys ignored.
  Then sprite DMA on (`h77 +0x111e`) and the game.
- So: **cover1 while the tune plays (a key or click ends it), then cover2 for 5 s.** The remaster's title splash
  (`GameData/phantasie1/graphics/amiga.json` `title_sequence`) plays the tune and shows cover1 for its length (a key ends
  both; with the TITLE TUNE option off cover1 still lasts 53.56 s) and cover2 for 5 s (a key ends it there too, unlike
  the Amiga).

## Title tune: `tamb.dat` and the player h89 `+0x304` (s108) `[C]`

Static read of hunks 89 (code), 90 (the sound scripts of the game's effects) and 91 (BSS: four audio.device IORequests,
the waveform, the expansion buffer). Remaster: `OpenPhantasie.Data.Audio.AmigaDosoundScore` (decoder),
`OpenPhantasie.Client.Audio.AmigaDosoundRenderer` + `PaulaMixer` (playback); tests `TitleTuneDecoderTests`, `TitleTuneTests`.

- **`tamb.dat` is an Atari ST `Dosound` script** (the game came from the ST): byte pairs `reg, value` for PSG registers
  0–15, `0x80 v` / `0x81 reg step end` sweeps, `0x82 n` = wait, `0xFF` / a wait of 0 = end. The file: 338 waits, the
  end `0x82 0x00` at byte 2844, then `0xFF` and 3 stray bytes. Registers used: 0, 1, 2, 3 (A and B periods), 7 (once,
  `0xD8`), 8, 9, 10 (levels), 12, 13 (envelope); no sweeps.
- **The Amiga has no PSG**: h89 `+0x304(script)` plays the script itself on Paula through audio.device.
  1. (s128) A script whose pointer equals the long at h91 `+0x420` (the title tune's buffer: h77 `+0x354` pushes
     `[h91 +0x420]`) is played in place, without the copy below; `tamb.dat` has no sweeps, so nothing changes. Any other
     script: `+0x9bc` first copies the script to h91 `+0x424`, expanding a `0x81` sweep (start from the last `0x80`) into
     `reg, v, 0x82, 1` for v = start, start + step, … until v = end (end itself not written); an expansion past 1900
     bytes returns 0 and nothing plays. Other pairs are copied as they are; `0xFF` ends the copy.
  2. Then, per command: regs 0/1, 2/3, 4/5 set the low / high byte of the A, B, C periods; reg 7: bit 0 = A off, bit 1
     = B off, and **any** reg 7 write sets the "channel 1 off" flag for good; regs 8/9/10: level 16 (the envelope flag)
     becomes 15, then `(v & 15) << 2` = an Amiga volume 0–60; `0x81` skips its 3 bytes; `0x82 n`: wait =
     **⌊n · 6 / 5⌋** (`mulu #6`, `divu #5`); regs 6, 11–15 and `0x80` are dropped (no noise, no envelope); `0xFF` returns.
  3. At each `0x82`: a wait of 0 returns; a key or mouse button (`h80 +0x4e4`) returns. Else the periods become Paula
     periods: **⌊PSG period × 1.7897732⌋** (IEEE single `0x3FE5174A` at `+0x30e`; float helpers h214 / h220 / h207 / h218,
     h218 = truncate to unsigned). 1.7897732 = 3 579 545 / 2 000 000: the NTSC Paula clock over the ST's PSG clock, so with
     16 samples a cycle the Amiga plays the ST's pitch on an NTSC machine (about 1 % flat on PAL `[~]`).
  4. Four `CMD_WRITE` requests (`+0x954`: flags `ADIOF_PERVOL | IOF_QUICK`, data h91 `+0x278`, length 16, cycles 0 =
     loop): **channel 0 = B** (unless B off), **channel 1 = B** (unless the channel-1 flag), **channels 2 and 3 = A**
     (unless A off; both test the A flag). C is never played. Units: masks `1, 2, 4, 8` at h90 `+0x3e8`. Channels 0
     and 3 are the left output, 1 and 2 the right.
  5. `Delay(wait)` (h234 `+0xe4`, dos ticks of 1/50 s on PAL and NTSC), then `ADCMD_FINISH` (`+0x7f4` style, request
     `+0x2cc`) to each channel sent: it stops at the end of its current waveform cycle. Then the next commands.
- **The waveform** (`+0x8ee` into h91 `+0x278`): from 127 down by 8 while above 0 = `127, 119, …, 7` (16 bytes, a
  falling ramp, all positive).
- **Numbers:** 338 steps, Σ⌊n·6/5⌋ = **2678 ticks = 53.56 s**. Step 1: A = `0x153` → Paula 606 (369.2 Hz on NTSC; the
  ST: 368.7 Hz) at volume 60, B period and level still 0. The `07 D8` at the start sets the channel-1 flag, so channel 1
  never sounds: B is heard on the left only, A on both sides.
- Not modelled `[~]`: audio.device's own latency between a FINISH and the next write (the next write is taken at the
  wrap of the finishing one). PAL or NTSC pitch: the remaster uses the NTSC clock the constant was computed for (pack
  `clock_hz` 3 579 545).
- **The A500's output filters (s158) `[C]`**, modelled in `OpenPhantasie.Client.Audio.A500AudioFilter` (every Paula sound:
  I's tune and effects, III's tunes and effects; `PaulaMixer(…, a500Filter: true)` by default). Source: Commodore's
  *A500 System Schematics* (#312511-02 rev 5 and #312511-03 rev 6A/7, sheet 4 "AUDIO FILTERS"; scan
  `devili.iki.fi/pub/Commodore/docs/books/System Schematics A500 Revisions 5, 6A, 7.pdf`, PDF pages 9 and 19; the
  filter values are the same in both, only the EMI parts and the coupling capacitor 0.22 / 0.33 µF differ). Per side,
  U14 (LF347 / TL084): (1) an op-amp stage whose feedback is R331 / R321 = 360 Ω parallel to C331 / C321 = 0.1 µF, one
  pole at 1 / (2π · 360 · 0.1 µF) = **4421 Hz**, always in; (2) the "LED" filter, a unity-gain Sallen-Key low-pass,
  R332 = R333 = 10 kΩ, C332 = 6800 pF (feedback), C333 = 3900 pF (ground): f0 = 1 / (2π · 10 kΩ · √(6800 pF · 3900 pF))
  = **3091 Hz**, Q = ½ √(6800 / 3900) = **0.660**; two MPF102 JFETs (Q331 / Q321, driven from `_LED` through U38 1488 and
  Q301) bypass it when /LED is high ("Note: LED off, Filters bypassed"). The fan-page figures (4.9 kHz, 3275 Hz
  Butterworth) are not these values. **Filter state:** Kickstart 1.3 (34.5, `kick34005.A500`) writes DDRA `$BFE201` = 3
  and **`$BFE001` = 2** right after its reset (ROM `0xfe` / `0x106`: LED dim, filter bypassed), then exec's start-up
  **`bclr #1,$BFE001`** (ROM `0x50c`, on the cold-start path before the capture vectors and InitCode: LED bright,
  filter in); the bset / bclr pairs at `0x5d6` and `0x30c6` are the alert blink loops. Neither I's `game` nor III's
  `PhantasieIII` contains `$BFE001`, so both play with the LED filter in. The HRM (Audio Hardware, "Low-Pass Filter",
  ADCD 2.1 `node00EE`) names the switch for "A2000's with 2 layer motherboards and later A500 models"; an A1000 or early
  A2000 has its own filter, not modelled (machine choice: an A500). Digital form: bilinear, prewarped at each corner,
  DC gain 1, within 0.5 dB of the analogue curve up to the corners. The output coupling capacitor (high-pass ≈ 5 Hz
  into 1 kΩ + 390 Ω) is the `DcBlocker`'s job `[~]`.

## Sound effects: the table h91 `+0x1f4` and `h89 +0xee(n)` (s128, static) `[C]`

Hunk 89 = code, 90 = DATA (1060 B, no relocations, file `0x29798`), 91 = BSS. There are **no samples**: every sound
is an ST Dosound script in h90, played by the same player `+0x304` as `tamb.dat` ("Title tune" above: the 16-byte
ramp waveform on Paula, period ⌊PSG × 1.7897732⌋, volume (v & 15) × 4, wait ⌊n · 6/5⌋ ticks of 1/50 s, `0x81` sweeps
expanded by `+0x9bc`). Period, volume and length come per step from the script; the ramp loops within a step
(`ioa_Cycles` 0), a script never loops.

- **The table** (h89 `+0x0`, called once from h17 `+0x12c`): 30 longs at **h91 `+0x1f4`** (BSS; the "h89 data
  `+0x1f4`" of s127 was this). All 30 start as h90 `+0x10`, then 16 are set:

  | n | h90 | file | steps / ticks | | n | h90 | file | steps / ticks |
  |---|---|---|---|---|---|---|---|---|
  | 0 | `+0x261` | `0x299f9` | 3 / 9 | | 17 | `+0x19f` | `0x29937` | 38 / 38 |
  | 3 | `+0x1b` | `0x297b3` | 8 / 8 | | 18 | `+0x13c` | `0x298d4` | 15 / 31 |
  | 4 | `+0x26` | `0x297be` | 3 / 3 | | 19 | `+0x4b` | `0x297e3` | 2 / 20 |
  | 5 | `+0x3e` | `0x297d6` | 3 / 3 | | 20 | `+0x375` | `0x29b0d` | 15 / 31 |
  | 8 | `+0x31` | `0x297c9` | 3 / 3 | | 21 | `+0x312` | `0x29aaa` | 15 / 31 |
  | 10 | `+0x282` | `0x29a1a` | 3 / 9 | | 22 | `+0x307` | `0x29a9f` | 1 / 30 |
  | 15 | `+0x6f` | `0x29807` | 31 / 68 | | 23 | `+0x2a8` | `0x29a40` | 19 / 22 |
  | 16 | `+0x62` | `0x297fa` | 10 / 10 | | 25 | `+0x3d7` | `0x29b6f` | 125 / 125 |

  **Default** h90 `+0x10` (`0x297a8`): A period `0x764` (Paula 3386), volume 60, B off, one step of 7 ticks: a short
  beep. Numbers 1, 2, 6, 7, 9, 11–14, 24, 26, 28, 29 all play it (so 1 = 24 is the table, not a slip).
- **`h89 +0xee(n)`**: n < 0 returns; **n = 27** calls `+0x158(6, h90 +0x24d)`: the script copied 6 times into h91
  `+0x0` (each copy up to, not including, its `0xFF`, scanned byte by byte), then 30 bytes from h90 `+0x4`, whose
  first byte is `0xFF`: one script "6 × then end", 12 steps / 60 ticks. Any other n plays `t[n]` (no upper bound
  check: n ≥ 30 would read past the table; no caller does).
- **Sound 25** is A high byte 1 and a sweep of the low byte from `0xFE` by `+0x32` to `0x68`: byte arithmetic needs 125
  steps (one tick each, 2.5 s) to hit `0x68`. Sound 5 is the same sweep by `0xCE` (−50): 3 steps. 25's step is
  probably a slip in the data, but the Amiga plays it so.
- **Other entries:** `+0xd2` plays h90 `+0x5` (A `0x22d`, 3 ticks; the combat spell list's tone, s134, pack sound 31); `+0xe0` the default beep; `+0x1cc` h90 `+0x299`,
  a whole script ending just before sound 23's (`07 fe 08 0f 01 06 82 …`: 2 steps / 4 ticks; the combat sparkle, s133); `+0x144(script)` a caller's script.
  `+0x7ca` starts a raw Paula tone (period `0x1770` = 6000 itself, not scaled; volume 60; channel 0, cycles 0 = until
  stopped) and `+0x7f4` stops it (FINISH, channel 0): the defeat screen (h32 `+0x5e4` / `+0x636`). `+0x156`, `+0x1ca`,
  `+0x1da`, `+0x1dc` are a bare `rts` (the ST build's hooks): their calls (h32 `+0x71a`, `+0x78e`, `+0x8d0`, …) do
  nothing.
- **`+0x304` blocks.** It first drains the window's IDCMP port (h77 `+0x3a6`: GetMsg / ReplyMsg until empty), so a key
  pressed before a sound is lost. Then per step: the writes, `Delay(⌊n·6/5⌋)`, FINISH to each sent channel and
  `WaitIO`; it returns at `0xFF` or a wait of 0. So a sound holds the game for Σ ticks / 50 s. Before each step after
  its commands, h80 `+0x4e4` takes **one** message; a `RAWKEY` (`0x400`, key down or up) or `MOUSEBUTTONS` (8) ends the
  sound there (the current step's Delay always runs out; one-step sounds can't be cut).
- The FINISH request (h91 `+0x2cc`, set by `+0x820`) has `io_Flags` 1 = `IOF_QUICK` only, no `ADIOF_SYNCCYCLE`
  (`0x20`), so the channel stops at once, not "at the end of its current waveform cycle" as the title-tune notes say
  `[~]` (no tick changes; the renderer still finishes at the wrap).
- **Callers of `+0xee`** (the number is the `moveq #n` before the call): h26 `+0xe4a` 22; h32 `+0x4e` 21, `+0x5ce` 23
  (defeat), `+0x692` 20, victory `+0x6f4` 22, `+0x724` 25, `+0x798` 1, `+0x860` 21, `+0x8a8` 24, `+0x8ee` 22; h38
  `+0x466` 12, `+0xcd2` 15; h41 `+0x320` 19, `+0xb50` 29, `+0x1862` 28, `+0x1968` 16, `+0x1a02` 26; h44 `+0x398` 2; h53
  (combat) `+0x292` 17, `+0x2de` 18, `+0x9e2` 15, `+0xb92` a computed n; h59 `+0x342` 19; h74 `+0x204` 27. `+0xd2`: h23
  ×6, h26 ×7, h29 ×7, h65 ×1; `+0xe0`: h26 `+0x72`, `+0x1a8`, h41 `+0x19a`; `+0x1cc`: h53 `+0xb3a`. Every one of them
  blocks its screen for the sound. **Read s153 (static):** h26 (the town's shops: Inn, Armory, training; h27 strings)
  `+0xe4a` 22 before a fade (`h77 +0x288`); h38 `+0x466` 12 after the requester-23 box "%s IS RESURRECTED"; h38 `+0xcd2`
  15 a member's death (wired); h41 `+0x320` 19 before "[original text]"; h44 `+0x398` 2 before the morale
  check's "MONSTERS RUN" box; h59 `+0x342` 19 before "[original text]"; h74 `+0x204` 27 before the BLACK
  KNIGHTS menu (h65 `+0x168`). The tone `+0xd2`: **h65 `+0x158`** when the map's input routine (h65 `+0`, called by h47
  `+0x7a`) returns a direction, i.e. at every overworld / dungeon step (live s153: a 0.064-s tone after each step click);
  h23 / h26 / h29 (Guild, Temple, shops, town bar: clicks in the town's own windows, which the remaster draws as its own
  screens): s158 wired for the sheet (h29 `+0xcf4` / `+0xe86` / `+0xf52` / `+0x934`) and Guild › New's PURGE / KEEP (h23
  `+0xc56`), pack `cue_sounds.sheet_tone`; the rest (h23 `+0x22a` / `+0x88e` / `+0xa34` / `+0xbf8`, h26 ×6, h29 `+0x318` /
  `+0x75e` / `+0x7dc`: the town's own list windows) not wired `[~]`; h23 `+0x734` is the spell list's (wired s134).
  `+0xe0`: h41 `+0x19a` the dungeon wall (wired s141); h26 `+0x72` / `+0x1a8` (the alerts' beep) wired s158 by line (pack
  `cue_sounds.alerts`, `P2AlertBeeps`: Guild Add's NO ROOM and move question, the Armory's AFFORD / NINE ITEMS, Guild
  Spells' NO MORE / AFFORD / LEARNED / FAILED, training's AFFORD and NEED MORE, NO ONE IS WISE, ASSEMBLE A GROUP, aging's
  DIED / AGED, the map Cast's NOT ENOUGH MAGIC; not mapped `[~]`: "D O N E !" (h8 ×4 via h26 `+0x38`), "UNUSABLE" (h8
  `+0x13e0`), the printout questions (h29 `+0x1e0` / `+0x210`). h32 `+0x5e4` / `+0x636` the defeat's raw tone. **Built s153:** pack
  `cue_sounds` (`CueSounds`, Client `Game1.P1Cues.cs`): under AMIGA SCREENS the step tone at each one-cell overworld /
  dungeon move, 19 when "A TOWN. ENTER?" / "[original text]" opens, 27 when the BLACK KNIGHTS menu opens. s158: 22 before
  the town TRANSPORTATION's fade (h26 `+0xe4a`, a town picked; pack `cue_sounds.transport`), the fade itself not drawn
  `[~]`. Also built s153: 2 before the morale check's
  "MONSTERS RUN" box (`combat_field.message.sounds_before`, `P1CombatFieldRun`; it holds the run).
  **s154 (static) `[C]`:** h65 `+0` loops on h80 `+0xb48` until a code 0–8 comes (cursor keys 5–8; a click on a cell next
  to the party, `0x32` → h56 `+0xb2a` / `+0xc08`, any other cell loops on with no tone; the Options menu's picks 0–4 =
  Inspect, Health, Cast Spell, Use potions, Look, h81 `+0x2de`; Speed's 0x14–0x16 set h3 and loop on), then `+0x158`
  plays the tone and returns the code; h47 `+0x7a` hands 5–8 to h59 (overworld) / h41 `+0x516` (dungeon) and 0–4 to h50 /
  h41 `+0x1eac`. So **every** move gets the tone first, a refused one too: on the overworld h59 `+0` then finds tile 100
  (`+0xd2`) and opens "THE RIVER STYX" / "[original text]" (h60 `+0x18` / `+0x2b`, h86 `+0x32a`, by h13 `+0x7ba` > 16)
  with no other sound; a map edge is a region change (`+0x5ce` / `+0x62c`), not a refusal; in the dungeon h41 `+0x564` →
  `+0x19a`: the beep `+0xe0`, then the box. **Built s154:** `P1MapHolds` / `Game1.P1Cues.cs`: under AMIGA SCREENS the
  step tone plays at the move's input (key, click, mouse-walk step; a QoL diagonal's pair gets one) and the move (or its
  refusal) runs after the tone's hold; 19 / 27 play after the move and their question shows after their hold
  (`CurrentPrompt` hides it); the map takes no input during a hold, a move pressed meanwhile is made right after (the
  Amiga's port keeps the event: a one-step sound can't be cut). The holds are the sound model's (`P1SoundSeconds`, s153:
  tone 0.075, 19 0.43, 27 1.34 s), 0 with the Sound switch off (user s129). **Built s156:** the tone before the Options
  picks 0–4 (`Game1.MapOptionAfterTone`): on the overworld or in the dungeon under AMIGA SCREENS the remaster's Inspect,
  Health, Cast Spell, Use potions and Look (by the map bar's item or the remaster's key) play the tone and open after its
  hold. **s156 (static h89 `+0x33e`…`+0x58c`) `[C]`:** the player drains the port once (`+0x33e`, h77 `+0x3a6`), then
  per wait command (`0x82`, ticks ≠ 0) takes one message (h80 `+0x4e4`, `+0x584`) **before** that step's writes and
  `Delay`; the end byte `0xFF` (`+0x38e` → `+0x7c2`) takes none. So a one-step sound (the step tone) only looks at the port
  right after draining it: every event of its hold (a key's press and its release, a click, a menu pick) stays queued for
  the map's input routine, which takes one a call, each move with its own tone. A multi-step sound (19, 27) takes one
  message per later step: a key's press there cuts the sound and is used up (its release stays queued). **Built s156:**
  `P1MoveQueue` (Client): the moves pressed during a hold are made in order after it, one per tone; a question, box or
  dialog that opens drops the rest `[~]` (the Amiga's requesters take and drop them). Left `[~]`: a held key's
  auto-repeat (the remaster takes presses only); a menu pick or an Options key during a hold is not kept.
  **Correction s154 (static) `[C]`:** h38 `+0x2ea` (RESURRECTION) branches on the state h4 `+0x6e` (`+0x41e`): in a round
  (state 5) h53 `+0x46e` re-lays the row with the member, then `+0xb80(text, 12)`: **12 before the line** (then its polls),
  then `+0xb80(" ", −1)`: the line cleared, no sound, no wait. Any other state: requester 23 (h44 `+0x466`), then 12
  (`+0x466`). s153's note joined the two branches: the remaster's 12 before the field line is the Amiga's order. **Built
  s154:** `line_sounds` `then_blank` (the line cleared after its wait). Left `[~]`: the row re-laid at once (the remaster
  shows the member at the next set-up). **Built s156:** outside a fight, 12 after the remaster's camp cast result
  (pack `cue_sounds.resurrected`, Core's `FieldCues.FieldResurrected`); the requester-23 box and its hold before it not
  drawn `[~]`.
- **Hold, live (s153, `working/capture_s153_sounds.md`) `[C]` / constants `[~]`:** recorded with sound (an own null sink,
  window and sound in one ffmpeg) for the first time in fights, the spell list, the map and the victory / defeat
  screens. Each step costs its `Delay` plus **3.7 ms per channel it sends** (the player writes, FINISHes and `WaitIO`s each
  sent channel: `07 fe` scripts send 2, `07 fc` 3), each call 7.5 ms more (`sound_effects.channel_overhead_seconds` /
  `call_overhead_seconds`; `step_overhead_seconds` 0). Fitted on 25 (3.43), 21 (0.79) and 1 (0.155); checked on 3, 8, 10,
  16, 17, 18, 22, 23, 31 within a frame (table in the capture). s130's 7 ms a step + 75 ms a call put one-step sounds
  0.07 s long. The film's sound and picture clocks differ by 0.2 %, so holds are taken between picture events.
- **Remaster (s128):** pack `sound_effects` with `format` `amiga-dosound` (`AmigaDosoundEffects`): file
  `unpacked/PHANT/game`, per number the script's file offset (27: the offset 6 times = joined as `+0x158` does);
  `SoundEffectPlayer.Play(AmigaDosoundEffects, n)` renders with `AmigaDosoundRenderer` (the title tune's). Wired: the
  victory screen only (`ending_scene.sounds`).

## The map screen's requester 23 (s139, static) `[C]`

**The wilderness encounter h74 `+0`** (only caller: the wilderness step h59 `+0x3be`, after its roll h71 `+0x103a` < 6;
h59's own strings are "A TOWN.  ENTER?", "THE RIVER STYX", "[original text]", so the dungeon never runs it). Its rolls
are h71 `+0x103a` (0–100, `rand / 327`); h4 `+0xe` is DOS's T (`[0x649c]`, tile % 10: the monster is r + 12 · h4 `+0xe`),
h4 `+0xc` the terrain class, h13 `+0x7c2` the highest score, members are h13 `+4` + 0x11a · i (status `+0x18`, 0 = DEAD,
1 = OK, 2 = asleep; LISTEN `+0x28`; name `+0x10e`). The branches are DOS's (`overworld-movement.md`, Core
`NightEncounter` / `WildernessEncounter`) with the Amiga's 0–100 rolls; every message box is **requester 23** (h44
`+0x466` one row in the middle, `+0x4d4` two rows), drawn over the map:

| Call | Branch | Text (h75) | Rows |
|---|---|---|---|
| `+0x6c` | night (roll < 35), before the watch roll | `+0x0` "[original text]" | 1 |
| `+0x168` | night, the watcher not DEAD and not heard: h44 `+0x10c0` (the prep), every OK member asleep, the watcher OK | `+0x12` "%s IS ON WATCH" | 1 |
| `+0x1b6` | night, heard (status ≠ 0 and roll < LISTEN) | `+0x21` "%s IS ON WATCH" / `+0x30` "[original text]" | 2 |
| `+0x1ee` | day, roll > 91 and highest score > 83 (no fight) | `+0x47` "[original text]" | 1 |
| `+0x436` | day, a member heard (roll · 222 / 100 ≤ LISTEN) and roll < 6 · T + 20: normal fight, no menu | `+0x91` "%s HEARS MONSTERS" | 1 |
| `+0x4b2` | the HEARS menu (h86 `+0x32a`, `+0xa3` "%s\|HEARS MONSTERS[RUN\|FIGHT]") → FIGHT, roll ≥ 30 | `+0xc0` "[original text]" | 1 |
| `+0x51c` | day, nobody heard, roll ≥ 6 · T + 20, then roll ≥ 50 | `+0xd3` "[original text]" | 1 |

A DEAD watcher gets no box (status 0 skips `+0x168`). The menus ("BLACK KNIGHTS" after sound 27, "YOU SPOT|SLEEPING
MONSTERS![ATTACK|LEAVE]", the HEARS menu) are other requesters (h65 `+0x168`, h86 `+0x32a`), built s154 as Amiga
requesters. h86's answer 1 is the **second** button (HEARS: 1 = FIGHT, `bne` to RUN's roll at `+0x4d6`; SLEEPING: 1 =
LEAVE → no fight, else ATTACK → `H` = 2, `F` = 0). (s138's capture note read `+0x91` as "%s IS ON WATCH / AND HEARS THE
MONSTERS" and `h4 +0xe` as an unnamed watch chance; both are corrected here: `+0x91` is the heard-without-menu box, and the
night watch's chance is DOS's LISTEN roll.)
- **Box and timing:** requester 23 as in the fight ("The combat screen": Left 33, Top 67, 255 × 47, rows at 80 / 88 / 96,
  each line centred in 40 cells from x 41, z6font), ⌊(h3 + 60 · rows) / 60⌋ whole seconds, keys and clicks swallowed,
  closed early when the clock reaches a multiple of 8 s. The boxes of one step follow each other with only the rolls (and,
  night not heard, the prep) in between `[~]` (not filmed back to back).
- **Pens:** the map's palette (h69 `+0x74`, `palettes.map`): fill pen 10 `#446600`, borders pen 12 `#44EE22`, text
  pen 15 `#EEEEEE`, as shot `p1-amiga-combat-33` shows ("[original text]" over the map, its ink at x 107 = 41 + 6 · 11).
- **Amiga-only numbers (DOS wins, not built):** the night monster r = roll · 5 / 100 is 5 when the roll is 100 (DOS: r ∈
  {0, 1, 4}); the watch slot roll · size / 100 is the party size when the roll is 100 (68 in 32768 each).

**Built s139:** pack `map_message` (`palette` "map", `keys`: the seven texts above by their string ids; `splits`: the
heard line on its two rows, `core.encounter.and_hears_the_monsters`), `MapMessageLayout`, `P1MapMessageRun` (the boxes of
a step's log, the HEARS menu's title skipped while the menu waits), Game1 (`Game1.MapMessage.cs`): under AMIGA SCREENS a
wilderness step or menu answer plays its boxes over the map, swallows keys and clicks, and opens the fight's screen (sound
17, black, set-up) after the last one; the remaster's message line leaves the boxed texts out. The text is Core's (DOS's)
except where the Amiga's differs: s139 kept DOS's "[original text]", "[original text]", "BLACK KNIGHTS SEE
YOU AND FLEE !" (by analogy with D-079); **s157 (user): the box prints the Amiga's** "[original text]", "MONSTERS
SURPRISE YOU!", "[original text]" (h75 `+0xc0` / `+0xd3` / `+0x47`, read from the program file `game`),
pack `map_message.texts` (Core's id → the Amiga's string id `p1field.box.*`; Core's text still picks the box and stays in
the log and on the DOS path). Preview: `./play.sh --test-level 15 --p1-combat 6
--p1-map-boxes` [`--p1-surprise`]; shots `parity/shots/p1-remaster-map-box-night-encounter.png`, `-on-watch-hears.png`,
`-you-surprise-them.png` (1 px = 1 Amiga px; the remaster's map layout with its sidebar under the box). s141: statically
the step h59 writes the new position (`+0xda`) and redraws the fog and the party sprite there (`+0xc8` → `+0x66c` → h56
`+0x9e8`) before h74 runs (`+0x3be`), so the first box comes after the party moved to its new cell.
**s143 (filmed, `capture_s143_dungeon_boxes.md`) `[C]`:** text pages (tile % 10 = 6) are requester-23 boxes too (h59
`+0x182`: feature arg < 10 one row, < 20 two (`+0x4d4`), else three (`+0x556`, 3 s); rows = the region's 40-byte pages
at `+0x253`). Filmed on a step: the sprite on the new cell one frame, **gone** the frame before the box, the box (two
rows 1.87 s), one black frame, the map, the sprite back one frame later. h80 `+0x546` (the show of every requester-23
box) frees the party's hardware sprite (`+0x54e`: h78 `+0x12` ≠ −1 → h77 `+0x106c` WaitTOF, FreeSprite, h78 `+0x12` :=
−1) and gets it back after (`+0x702` → h56 `+0x8e2`), so **no party sprite under any map box** (s141's "sprite on its
cell" under "[original text]" was the red pointer; that film shows the sprite gone too, also under the HEARS menu).
Built s143: `P1MapMessageRun.PartySpriteShown`, the remaster draws no overworld party sprite while a box is up. Left:
nothing: text pages as boxes built s153 (below), the menus hide the sprite too (s153, "Overworld / dungeon map graphics", Party).
**s144 (static, for the text-page boxes, not built):** the Amiga `OUT1.DAT` pages at `+0x253` are 39 characters
padded with blanks and a NUL (`"         [original text]            "`), so h65 `+0x1f4`'s centring adds
⌊(40 − 39) / 2⌋ = 0 and the page's own leading blanks place the text: x 41 + 6 · 9 = 95 and 41 + 6 · 4 = 65 for
"[original text]", as shot `p1-amiga-map-40` shows (95 / 65). Core's pages (`overworld.json`
`text_pages`) are trimmed, so a box drawn from them would need the leading blanks back (or the raw pages) `[~]`.
**Built s153:** h59 `+0x182…+0x23c` re-read: arg < 10 → `+0x466(page)`, < 20 → `+0x4d4(page, page + 1)`, else
`+0x556(page, page + 1, page + 2)`, each through h80 `+0x546` with h3 + 60 · rows (`+0x5cc`: h3 + `0xb4`), i.e. the
same whole seconds and clock cut as every requester-23 box. DOS's and the Amiga's pages 0–9 are byte-equal in every
region, and every page a tile-6 feature names ends in a NUL at byte 39 and is not blank (s153 scan of OUT1–19). Data:
`text_pages[].raw` (the page up to its NUL, `extract_maps.py`), `OverworldRegion.RawTextPages`,
`OverworldMoveResult.LocationTextRows` (the pages as stored, one per page). Core logs the trimmed lines as before and
marks them `LogWindowKind.Box` with those rows (`GameSession.MapWindow`, renamed from `DungeonWindow`, now noted on the
overworld too; `LogWindow.Rows` / `MapWindow.Rows` carry the side channel). `P1MapMessageRun.ForStep` passes the
overworld's windows; `Plan` makes a Box window one requester-23 box of its raw rows (centred by full length, so ink at
x 95 / 65 for the s143 path page; one row in the middle row, two / three from the top) and keeps the logged lines and
the joined sign off the message line. Test `OverworldTextBoxTests` (Core), `P1MapMessageTests.ATextPageStep_…`.
Shot `parity/shots/p1-remaster-map-box-text-page.png` (compare `p1-amiga-map-40`; the remaster's map layout).

**D-058's flashes (user s140: "do whatever shows it but doesn't slow things down"):** the remaster's "A DAY ENCOUNTER"
(every wilderness fight that is not a night one: rolled, fixed and tile-class fights) and the dungeon's "AN ENCOUNTER"
have no Amiga box and are **no box** since s140 (s139 boxed the day one, adding ~1 s before the fight). They print on the
combat screen's line (y 185, z6font, line pen; `CombatPresenter.P1Banner`) from the fight's start, over sound 17's black and
the set-up too, and stay until the fight's first line prints or its first run ends: no wait is added. The night fight's
"[original text]" stays the Amiga's box (`+0x6c`) and puts nothing on the line. In a surprise opening with the Sound
switch off there is no black, so the flash is seen only during the set-up `[~]`.

## The dungeon's requester 23 (s140, static) `[C]`

Hunk 41 (CODE, 8440 B, file `0xF888`; strings in its DATA hunk 42, file `0x11FD0`) runs the dungeon's cells: the step
`+0x516` (x + dx in 0…32, y in 0…37, else or on cell 0 the wall), the cell dispatch `+0x8ee…+0x9be` (`0xdd`+ the event
record / script `+0x15b8`, `0xda…0xdc` stairs `+0xddc`, `0xd7…0xd9` `+0x414` (exit), `0xb9…0xd6` the lock `+0xe26`, 2…6 the
trap `+0xa78`; DOS's ranges, `dungeon-representation.md`). It calls requester 23's one-row opener h44 `+0x466` eleven
times and never `+0x4d4` / `+0x556`; every box is the map's (the dungeon map shares the overworld's palette, h56 `+0xed6`
LoadRGB4s h69 `+0x74`), so pens 10 / 12 / 15 as on the overworld. Box, centring, whole seconds and the 8-s clock cut are
those of "The combat screen" (h80 `+0x546`).

| Call | Where | Text (h42) | Sound before (h89) |
|---|---|---|---|
| `+0x1a6` | `+0x19a`, from the step `+0x564`: outside the level or cell 0 | `+0x5d` "THAT IS A WALL" | `+0xe0` (the default beep) |
| `+0x1ea` | `+0x1b0(member, text)`: `+0x6c` "%s %s" with the member's name (h13 `+4` + 0x11a · i `+0x10e`) | see the next three | |
| ← `+0xb66` | trap `+0xa78`: spotted (roll ≤ `+0x24`), "%s[original text] IT?[YES\|NO]" (`+0x11c` → `+0x15e` → h86 `+0x32a`) YES, "WHO WILL DISARM?" (h62 `+0x508`), roll > `+0x26` | `+0xf6` "FAILS" (then the spring `+0xba0`) | 29 (`+0xee`) |
| ← `+0xb7c` | the same, roll ≤ `+0x26` | `+0xfc` "SUCCEEDS" (cell := 7) | |
| ← `+0xf12` | lock `+0xe26`, kind 14: the last member (status ≠ 0) with roll < `+0x2c` | `+0x162` "PICKS THE LOCK" (cell := `0xb9` + bank) | |
| `+0xe1a` | stairs `+0xddc(c)` | `+0x12a` "[original text] %-2d TO %-2d" (c − `0xd9`, c − `0xd8`) | |
| `+0xe68` | lock kind ≥ 15 (jammed), or kind 14 with no picker | `+0x149` "[original text]" (cell := `0xc7` + bank, step back) | |
| `+0x15ae` | `+0x15a8`, script op 1, then the damage (op 2's) | `+0x18e` "[original text]" | |
| `+0x1870` | script op 3, then the damage | `+0x1ad` "A SPELL IS CAST" | 28 |
| `+0x18d6` | script op 7, then gold += a · 256 (op 8: the same without the box) | `+0x1bd` "[original text]" | |
| `+0x1914` | script op 9, then gold −= a · 250, floor 0 (op 10: no box) | `+0x1d6` "[original text]" | |
| `+0x1976` | script op 12, then the move (`+0x5f0` redraws the cell left, the party goes to (a, b)); op 13: no box | `+0x1f1` "[original text]" | 16 |
| `+0x1a10` | script op 14, then the cell write `+0xdb4(a, b, c)` (op 15: no box) | `+0x206` "[original text]" | 26 |
| `+0x1a6e` | script op 17, then H = 0, F = 1 and, when a + 1 > 1, the fight (return 11) | `+0x227` "MONSTERS APPEAR" | |

The script's jump table (`+0x1742`, 19 entries, op = the byte mod `0x80`, less `0x40` above `0x3f`) is DOS's op table
(OV2 `0x28f2`): the same ops print the same texts, and Core's `DungeonScript` logs them at the same ops (1, 3, 7, 9, 12, 14,
17). Not requester 23 (no box): "SPOTS A TRAP. DISARM IT?" and "HEARS MONSTERS. GO BACK?" (h86 `+0x32a` menus), "WHO
WILL DISARM?" (h62 `+0x508`), the damage report ("   REPORT:" h71 `+0x112e`, the member lines h23 `+0xd28`, shown by
h86 `+0(0x15)`), "IN THE TRAP:" / "AVOIDS THE TRAP", the message cells and op 11 (`+0x11e4`). No manual conflict: the
manual names none of these windows.

**Built s140:** pack `map_message.dungeon_keys` (the thirteen texts by Core's string ids: `core.dungeon.that_is_the_wall`,
`x_fails`, `x_succeeds`, `x_picks_the_lock`, `stairs_from_level_x_to_x`, `no_one_can_pick_the_lock`, `core.script.
smoke_fills_the_air`, `a_spell_is_cast`, `party_gets_some_gold`, `some_gold_is_taken`, `the_party_is_moved`,
`you_hear_a_noise`, `monsters_appear`); `MapMessageLayout.DungeonKeys` / `KeysFor`; `P1MapMessageRun.ForStep` picks the
dungeon's keys in the dungeon or a fight started there (the overworld's never match there, nor the dungeon's on the
overworld); Game1 keeps the dungeon map under the boxes while Core is already in the fight (`ViewMode` →
`CombatReturnMode`), then opens the fight. Tests `P1DungeonMessageTests` (each text, a real wall bump, stairs, a jammed
lock, MONSTERS APPEAR then the fight, every DNG1–10 MESS line and option checked against the wildcard keys: none matches).
Core logs DOS's "[original text]"; since s157 (user) the box prints the Amiga's "THAT IS A WALL" (h42 `+0x5d`, pack
`map_message.texts`). Stairs: Core trims
the `%-2d` pad, the Amiga centres "…TO 2 " by its strlen (26); ⌊(40 − 25) / 2⌋ = ⌊(40 − 26) / 2⌋ = 7, the same cell.

**Where the party stands under each box, and what runs between them (s141, static) `[C]`:**
- **The step** `+0x516`: off the level or cell 0 → `+0x19a` (beep, "THAT IS A WALL") before any write, so the party stays on
  its cell. Else the position is written (h4 `+0x166` / `+0x168`, the old one to `+0x16a` / `+0x16c`), `+0x6a0` redraws
  the cell left (`+0x5f0`: the cell's glyph by DOS's rule, h56 `+0xf76`) and the party glyph `0xed` on the new cell, and
  only then `+0x7f8` rolls and dispatches (`+0x8ee`). So every box of a step shows the party on the step's new cell.
- **Op 12** `+0x1964`: sound 16, the box (`+0x1976`), then `+0x5f0(old x, old y)`, the position := (a, b) (also h13
  `+0x7bc` / `+0x7be` and the previous cell), h56 `+0xf76(x, y, 0xed)`, `+0x10c6(h42 +0x204)` (the message line). The box
  is over the event cell; the party moves when it closes. Op 13 (`+0x197e`) is the same without sound and box.
- **Jammed lock** `+0xe26`: the box (`+0xe68`), then the cell write `+0xdb4` and the step back `+0xd26`: the box is over
  the lock cell. **Stairs** `+0xddc`: the box only (return 3). **Trap** "%s FAILS": sound 29 (`+0xb50`), the box, then the
  spring, on the trap cell.
- **Between the boxes:** ops 1–4's damage (`+0x1792`): "   REPORT:" (h71 `+0x112e`) and the member lines (h23 `+0xd28`) go
  into requester 21, which h86 `+0(0x15)` opens: `Request`, then `+0x132` waits for a GADGETUP (its EXIT), `EndRequest`.
  So op 3's box, the report (until the click) and a later box play in that order. A MESS text that is no menu (`+0x11e4`,
  ops 11 / 19 and the event's own message): requester 21 sized to its lines, shown by h80 `+0x546(0x15, (n + 1) · 120)`
  (timed, keys swallowed `[~]`: n read as the line count); a menu goes to `+0x1514`. In DNG1–10 a box and another
  window share a script at: DNG1 record 1 (op 11's text, then MONSTERS APPEAR), DNG2 23 (A SPELL IS CAST, report, THE
  PARTY IS MOVED), DNG4 7 (SPELL, report, SPELL, report, op 13, MONSTERS APPEAR), DNG8 7 (SMOKE, report, MONSTERS
  APPEAR), DNG5 3 and DNG6 6 (a box, then op 6's item offer), and DNG1 3 / DNG6 5 / 6 / DNG7 1 (a box, then the report).
  No script has two boxes with nothing between them.

**Built s141:** pack `map_message.sounds` (the wall's beep = pack sound 32, `0x297a8`, which h89 `+0xe0` plays directly;
29, 28, 16, 26 by their keys) and `P1MapMessageRun`: a box's sound plays first (`Game1.PlayMapSounds`, the effect
player) and the box opens after its hold (`P1SoundSeconds`; Sound switch off: no sound, no wait), the 8-s clock cut
taken after it. Core records the moves a step makes after some of its lines (`GameSession.DungeonMove`: op 12 / 13's
from `DungeonScriptResult.Moves`, a jammed lock's step back); the boxes logged before a move carry its cell
(`Box.PartyAt`) and the dungeon map draws the party there (and reveals around it) until the box closes. In the dungeon the
lines between two boxes split the run into parts (`WaitLines`, `Next`): the remaster shows them in its text panel and
waits for a key or a click (requester 21's EXIT), then the next box. Preview: `./play.sh --test-level 15 --p1-dungeon 2
6 28`, step south, BRIBE THEM (the map-only layout F2 puts the dungeon at the Amiga's (10, 16)). Left:
- ~~requester 21 itself is not drawn~~ built s144 ("Requester 21 on the map" below): the MESS window (timed) and the
  report (OK) are drawn, in the log's order, before, between and after the boxes;
- the uncut length of a MESS window: filmed s158 (5.93 s for two rows, the static 6 s); SMOKE / SPELL with a report, stairs and a jammed lock not filmed `[~]`.

**Filmed s143 (DNG1 on FS-UAE, `capture_s143_dungeon_boxes.md`) `[C]`:** "THAT IS A WALL", "%s PICKS THE LOCK" (party
drawn on the door first), "%s FAILS" (trap), "MONSTERS APPEAR" (after op 11): requester 23 over the map, the party glyph
on the step's new cell, one black frame before and after, 0.88–0.93 s for a 1-s box; twelve wall boxes 3 s apart: ten
0.917 s, two cut to 0.517 s exactly 24 s apart (the 8-s clock cut, seen live); click → box 0.395 s (the beep's hold).
**Requester 21:** a MESS text with no menu is a borderless pen-10 window across the top of the map (screen y ≈ 21 on,
no title, no button), **timed, no key needed** (op 11's two lines: 3.57 s, read as a clock-cut wait; the trap's MESS 6:
≥ 4.13 s); an event's menu MESS uses the same window with its buttons; the report ("IN THE TRAP:", yellow title, member
lines) has an **OK** button and waits for it. Order seen: trap FAILS box → MESS window → report (OK) → map; gold menu
TAKE → op 11's MESS window → MONSTERS APPEAR box → 3.05 s of map (shape load) → the fight. One-line MESS of a message
cell and the area texts go on the message line (yellow, y ≈ 190; s155 static: TopEdge 192, pen 3, "The map's message line"). The remaster matches the boxes; requester 21 is built
s144 (below).

A box's line also leaves the note under a prompt (`_promptNote`: a disarm's "%s FAILS", a HEARS answer's surprise), as it
leaves the message line (s140).

## Requester 21 on the map (s144, static) `[C]`

Requester 21 is the struct at h84 `+0x3fc6 + 21 · 0x70` = `+0x48f6` (dumped s144 with a hunk reader; ITexts h84 `+0x1d2a`
+ 20 · i, Borders `+0x186a` + 16 · i, Gadgets `+0x2dba` + 44 · i). As stored: Left 33, **Top 45**, 255 × 110, backfill pen
10, ReqBorder = Borders 55 / 56 (pen 13, (0, 0)–(254, 109) and (1, 1)–(252, 108)), ReqText = IText 184 (pen 3 on 10, at
(73, 8), font TextAttr h78 `+0x22` = **topaz 8**, its text filled at run time), ReqGadget = gadgets 83…89 (ids 11–17: rows
at (7, 25 + 8k), 241 × 7, no render, Activation `RELVERIFY | GADGIMMEDIATE`, IText 186 + k pen 15 on 10 JAM2 at (1, 1) in
z6font), then gadget 1 (the button: GRELRIGHT | GRELBOTTOM (−245, −18), 235 × 9, Borders 57 / 58 pen 2 at (−2, −2) and
(−1, −1), Border 59 pen 15 (the face, 18 points), IText 185 pen 0 on 15 at (90, 2), z6font). The fight moves it (h71 `+0x106`
Top := 23, its orders list and FIGHT; `+0x2a4` puts 45 back; `+0xb4` clears ReqText). Screen positions with the map palette
(`palettes.map`): fill `#446600`, borders `#888888`, title `#EEBB00`, rows `#EEEEEE`, button face `#EEEEEE` with `#228800`
borders and black text; the button sits at (42, 136)–(276, 144) (GRELRIGHT / GRELBOTTOM counted from the last pixel, as the
fight's FIGHT at (42, 114) on the s132 shots).

- **The damage report** (h71 `+0x112e(title)`): the title into text slot 156, ReqText := IText 184 (h84 `+0x2b8a`), the
  seven row texts cleared (h23 `+0xe32`), slot 157 := h72 `+0x179` **"   O K"**; the member lines go into the rows (h23
  `+0xd28`); h86 `+0(0x15)` then `Request`s it and `+0x132` loops GetMsg until a **GADGETUP** (class `0x40`) of a gadget
  with Activation bit 1 (`andi #2`): the OK button or any row (all RELVERIFY | GADGIMMEDIATE); keys and other messages are
  replied and dropped, so **a key does not close it** `[C]` (static); `EndRequest`, the sprite back. Title strings: h42
  `+0x105` "IN THE TRAP:", `+0x1a2` "   REPORT:" (h41's two report callers, trap `+0xa78` and ops 1–4 `+0x1792`). Seen
  s143: "IN THE TRAP:" yellow at x ≈ 106, the OK button, waiting 10 s for the click (shot `p1-amiga-dungeon-45`).
- **The MESS text window** (h41 `+0x11e4(id)`, the show of a MESS message = DOS `0x2a95`): callers the event's own message
  `+0x1624`, op 11 / 19 `+0x195a` / `+0x1b64`, the trap's message `+0xbae` (MESS a + 4) and a description cell `+0xa58`
  (cells `0x12…0x7f`, id = b − 17); a message cell (`0x8b…0xb8`, `+0x1052`) prints its first record on the message line
  instead (`+0x10c6`, the last-message check h4 `+0x66`), as DOS's `0x23c0`. The MESS file (`messN`, an index of LE words +
  230, records of a length byte and 40 characters): the first record's byte is the text's length (40, 80, …), a later
  record's byte the menu's button count + 1 (1 = no menu, 3 = two buttons). `+0x11e4` copies the text 40 characters a row
  into the row slots (101 + k), takes the next record's byte as the button count when the first row ends at 40, and with
  a count outside 2…20 shows the text alone: ReqText := 0, ReqBorder := 0, gadget 17's NextGadget := 0 (`+0x136a`: OK
  unlinked), the row list cut after row n (`+0x13ae`), **Height := 8n + 33, Top := 22**, h43 `+0` := 8n + 55, then
  **h80 `+0x546(0x15, (n + 1) · 120)`**: ⌊(n + 1) · 120 / 60⌋ = 2(n + 1) whole seconds (4 s for a row, 6 s for two, 8 s for
  three), keys swallowed, the 8-s clock cut (h80 `+0xd2a`, "The combat screen"), **no Speed polls** added. A click on a
  row is a GADGETUP and ends it early (built s153; **filmed s158** `[C]`: the gas window ended within 0.2 s of a row
  click, 2.40 s into its 6 s, the report 0.1 s later, `p1-amiga-dungeon-row-click-ends-mess-window`; uncut, the gold
  window 96 stayed 356 frames = 5.93 s at 60 fps against the static 6 s, `capture_s158_filming.md`). Afterwards ReqBorder, the OK link, Top 45
  and Height 110 are put back (ReqText stays 0 until the next report sets it). Rows are the records as stored, not
  centred (the gas text's second row starts with two blanks, shot `-44`). With a count in 2…20 (a menu) `+0x1440`
  `Request`s requester 21 as the same text window (no wait) and puts the buttons in **requester 25** under it (s153,
  below): the event menu. (s144's "12-character cells, 3 or 11 a row" read the label width as a row count: the labels
  are 11 characters, or 3 when the count byte is above 7, each in a 12-byte cell; the buttons sit 3 a row.) A first record whose byte is below 40 sets the count to 3 (`+0x13c4`, the menu branch); no
  message start the game shows has one (`mess1…10` scanned s144: every byte < 40 is a later record or a blank one) `[?]`.
- **The event menu (s153, static) `[C]`:** h41 `+0x1440`: `Request(requester 21)`; label width w = 3 when the count
  byte is above 7, else 11; for each of the count − 1 buttons the next w characters of the text stream (reading the next
  record each 40) go into a 12-byte cell; then `+0x1f6(k, cells)`: `sprintf("%s")` each cell into text slot 112 + i
  (ITexts 202 + i), gadget 92 + k's NextGadget := 0 (the list cut after button k), requester 25 (h84 `+0x4ab6`: Left 33,
  Width 255, backfill **pen 12** `#44EE22`, ReqText 0) gets ReqBorder := 0, Height := 18 · ⌈k / 3⌉ + 7 and Top := h43
  `+0` = 8n + 55 (the text window's bottom edge), and h86 `+0(0x19)` shows it: it moves the party sprite off (h236
  `+0x228`, y 200), `Request`s, waits in `+0x132` for a GADGETUP of a gadget whose Activation has bit 1 (RELVERIFY) and
  returns its GadgetID; keys and other messages are replied and dropped. The answer is id − 10. `EndRequest` (requester
  21) and the restores follow as for the text window. Buttons = gadgets 93–102 (ids 11–20), stored at (10, 8), (93, 8),
  (176, 8), (10, 26), (93, 26), (176, 26), (10, 44), (93, 44), (176, 44) and the tenth alone at (93, 62), each 70 × 9,
  render Border 68 (pen 2, (−2, −2), 74 × 13) → 69 (pen 2, (−1, −1), 72 × 11) → 70 (pen 15, the face's 18-point fill),
  IText pen 0 on 15 at (2, 2) in z6font. Gold's menu (DNG1 91): cells " TAKE THEM " and " LEAVE THEM"; DNG4's digit
  menus: " 1 " … " 9 " (three rows). The gadgets 93–98 serve requester 24 too (h62 `+0x142` cuts the list after six).
  **Filmed s158** `[C]` (`p1-amiga-dungeon-event-menu-requester25`, test `P1MapWindowTests.TheMenu_MatchesTheLiveShot`):
  the text window pen 10 y 22–70, the pen-12 band y 71–95 over x 33–287, the two buttons' pen-2 borders and faces at
  (43, 79) / (126, 79). Clicks on the text rows while requester 25 waits (rows 0 and 1, three clicks) do nothing: only
  the buttons answer, as the remaster does `[C]`. No MESS id that a trap, description cell, op 11 / 16 / 19 or
  SET-CELL can ask for is a menu (s153 scan, `extract_dungeon_events.referenced_ids`), so only the event's own message
  shows one.
- **Swimming (s153, static) `[C]`:** h71 `+0xe88` (the wilderness step's state 10, caller h2 `+0x318`) puts h72 `+0x131`
  "  SWIMMING:" through `+0x112e` (the report's title), the member lines (`+0x13d` "%-10.9sSWIMS", `+0x14a` / `+0x16d`
  the sink / death lines) in the rows and shows requester 21 through h86 `+0(0x15)`: the trap's report window, waiting for
  OK; a defeat returns 6. h71 `+0x112e`'s callers are h41 `+0xbba` (the trap), `+0x1798` (ops 1–4), h44 `+0x1486` (the
  fight's party health) and this one. Every `h86 +0(0x15)` (requester 21 shown and waited on): h41 `+0xcec` (trap),
  `+0x1840` (ops 1–4), h44 `+0x1570` (fight health), h71 `+0x100c` (swim), and three more (built s154, below): h71
  `+0xa50…+0xc88` "A MAGICAL POOL" (h72 `+0xa5`) with "%-10.9s IS CHANGED" rows (the pool), h71 `+0xca2…+0xe66`
  "  IN THE INN" (`+0xdf`, after "[original text]") with "%-10.9s[original text]" / "%-10.9sIS RESTING" rows
  (the wilderness inn), h50 `+0x54a` "HEALTH REPORT:" (h51 `+0x75`, the map's Health).
- **The map's Health (s154, static) `[C]`:** h50 `+0x420`: h51 `+0x75` "HEALTH REPORT:" into slot 156, ReqText := IText
  184, the seven row slots cleared (h23 `+0xe32`), slot 157 := h51 `+0x84` "   O K", then one row per member (h4 `+0`)
  `sprintf(h51 +0x8b " %-10.9s%-6.5sHP%4d/%-4dMP%4d/%-3d ", name, status, HP, HP max, MP, MP max)` (a blank first and
  last; status from h12 `+0xb78`: DEAD / OK / SLEEP by the record's `+0x18`) into slots 101 + k, and h86 `+0(0x15)` at
  `+0x54a`: requester 21 at Top 45, waiting for OK. DOS's row (`HealthDialog`, DS `0xbd7d`) has neither blank and MP
  `%3d`.
- **The fight's Party health (s154, static) `[C]`:** h44 `+0x1478`: h45 `+0x107` "HEALTH REPORT:" through h71 `+0x112e`,
  then per party slot (h4 `+0`, an elemental included) `sprintf(h45 +0x116 "%-10.9s%c HP%4d/%-4dA:%-3.2dMP%4d/%-3d ",
  name, "D S"[status] (h45 `+0x104`), HP, HP max, @250 / 75 + 1, MP, MP max)` into slots 101 + k, h86 `+0(0x15)` at
  `+0x1570`, in the combat palette. **s158 live (correction):** the window shows at Top 45, as the map's
  (`p1-amiga-combat-party-health-before-orders`): h71 `+0x102` sets Top 23 only while the orders requester is up and
  `+0x2a0` puts 45 back, and with the orders up the Party Options picks do nothing (filmed); s154 had read Top 23. Pack
  `fight_top` 45. DOS (DS `0xbdb1`) "%-10.9s%c HP%4d/%-4dA:%d MP%3d/%d": the Amiga has no blank before MP and pads
  A to 3 cells. `%-3.2d`'s precision is dropped (no leading zero): the program's printf (h167) uses a precision only for
  `%s` and floats (s156, static `[C]`; the s128 shot showed the same for the orders rows' `%-4.3d`).
- **Film (s143)** fits: the gold window y 21…70 (two rows: 22 + 49 − 1 = 70), 3.57 s (6 s cut by the clock), the gas
  window ≥ 4.13 s; the report Top 45 (y ≈ 45…154 on shot `-45`).
- **Remaster vs Amiga text:** Core prints DOS's texts: "REPORT:" where the Amiga has "   REPORT:" (the title three
  cells left of the Amiga's; s154: the Amiga path draws the Amiga's titles, pack `window.titles`), and the member lines pad the name to `%-10.9s` as both originals since s154 ("ARENTINO  AVOIDS THE TRAP", I's own
  string ids; a name over 9 characters, possible only by transfer, is padded but not cut `[~]`).

**Built s144:** Core marks the runs of a step's log the original shows in their own window (`LogWindow`, `LogWindowKind`
Text / Report; `DungeonMoveResult.Windows` for a trap's MESS a + 4 and "IN THE TRAP:" report and a description cell's
text, `DungeonScriptResult.Windows` for ops 1–4's "REPORT:", `ScriptHost.NoteMessage` for op 11 / 19 and an event's own
text; `GameSession.DungeonWindow` / `DungeonWindowsSince`, renamed s153 to `MapWindow` / `MapWindowsSince`): DOS's log is unchanged. Pack `map_message.window` (the numbers
above, `ok_label` = UI string `p1field.ok`), `MapMessageWindow` (`TextBox`, `TextRowY`, `TextSeconds`, `ReportBox` /
`OkFor`: a report longer than seven rows grows the window downward, no line dropped). `P1MapMessageRun.Plan(windows:)`:
a MESS window is a timed step of the run like a box (`Box.Window`, 2(n + 1) s through `CombatFieldMessage.ClockCut`), a
report ends its part and waits (`Waits`, `WaitLines`; also after the last box), the run may hold windows and no box;
lines in no window and no box stay on the message line. `UiOverlays.DrawP1MapTextWindow` / `DrawP1MapReport`. The
remaster lets **a key or a click** close the report (the Amiga: a click on OK or a row only; keys kept, user decision s154). Order now as filmed:
trap FAILS box → MESS window → report (OK) → map; gold TAKE → op 11's MESS window → MONSTERS APPEAR → the fight; BRIBE:
SPELL box → report (OK) → MOVED box. Tests: Core `DungeonWindowTests`, Client `P1MapWindowTests`. Shots (`parity/shots/`,
the 1920-px window halved): `p1-remaster-dungeon-mess-window-gold`, `-mess-window-gas`, `-in-the-trap-report-window`
(compare `p1-amiga-dungeon-46`, `-44`, `-45`). Left: ~~the event menu's MESS window with its buttons~~ built s153;
~~a click on a MESS row closing it early~~ built s153; both filmed s158 (`capture_s158_filming.md`).

**Built s153:** (1) the event menu: pack `map_message.window.menu` (requester 25 above: `left`, `width`, `fill_pen`,
`row_height`, `height_base`, `per_row`, the ten stored `buttons`, `button_size`, pens, `label`), `MapMessageMenu`
(`BoxFor`, `Button`); the MESS option cells as stored (`dungeons.json` `option_cells`, `extract_dungeon_events.py`;
`DungeonMessage.OptionCells`); under AMIGA SCREENS a P1 dungeon event's MESS menu (`Game1.P1MapMenuFor`) draws
`UiOverlays.DrawP1MapMenu` (the text window, the pen-12 band, the buttons with the cells as stored) instead of the
remaster's prompt box; a click on a button presses its digit (`MapMenuButtonTag`), other clicks stop at the backdrop.
The remaster keeps its keys there (digits, arrows + Return; the Amiga: a click only; keys kept, user decision s154), as for the report. A save
loaded while a menu waits shows the trimmed labels (the save keeps no cells) `[~]`. (2) A click on a row of the timed
MESS window ends it: `map_message.window.row_gadget` ((−1, −1, 241, 7) from a row's text), `MapMessageWindow.TextRowGadget`,
`MapWindowRowTag` hit regions, `P1MapMessageRun.EndWindowAt` (the boxes after it timed again from the click, their clock
cut with them; a requester-23 box has no gadget, so a click there does nothing). (3) Swimming: Core marks the swim lines
as a Report window (`GameSession.Swim`), so the overworld plays them as the report with OK. The title is DOS's
"SWIMMING:" where the Amiga has "  SWIMMING:" (two cells right; s154: the Amiga's on the Amiga path); the overworld party sprite is off under the
report (h86 moves it off; s153 lead, `MapPartySprite` with the run's wait). Tests: `P1MapWindowTests` (menu, row gadgets, row click, cells),
`P1MapMessageTests.ASwimStep_…`. Shots: `p1-remaster-dungeon-mess-menu-gold`, `-mess-row-click-ends-window` (a click on
row 1 at 0.5 s: MONSTERS APPEAR at once), `p1-remaster-map-swimming-report-window`.
Core notes op 11 / 19's text while the script runs and the script's own lines (boxes, reports) after it, so a text after
a box or a report in one script would show first `[~]`; no DNG1–10 script has op 11 / 19 after ops 1–4, 7, 9, 12, 14 or
17 (s144 scan of every event record's scripts), so no shipped event meets it.

**Built s154:** (1) The magical pool and the wilderness inn are Report windows in Core: `Phantasie1Campaign.OnSpecialSite`
marks the pool's lines (`GameSession.NoteMapWindow`), and `GameSession.StayAtWildernessInn` marks the inn's one joined
line (DOS's band text, unchanged) as a Report whose `Rows` are the title and member lines (`TownTransactionResult.Lines`);
`P1MapMessageRun.Plan` shows a Report's `Rows` when given. Both play on the overworld as the swim's report (OK, the
sprite off). The map's Health and the fight's Party health are the remaster's own dialogs (`HealthDialog`, the combat
presenter's info box), not Core log lines, so Core marks nothing for them: under AMIGA SCREENS the Client draws the map's
Health as requester 21 (`P1MapReports.MapHealth`, the Amiga's rows above; the sprite off) and the fight's Party health
as requester 21 moved to the pack's `fight_top` 23 (`MapMessageWindow.MovedTo`, `P1MapReports.FightHealth`) in the
combat palette. A key or a click closes either (the Amiga: a click on OK or a row; keys kept, user decision s154).
(2) Titles: pack `map_message.window.titles` maps a Core title (DOS's text) to the Amiga's (UI strings `p1map.title.*`:
"  SWIMMING:", "   REPORT:", "  IN THE INN", "A MAGICAL POOL", "IN THE TRAP:"; `health_title` "HEALTH REPORT:",
`health_row`), drawn by `DrawP1MapReport` (`P1MapReports.Report`); the DOS path keeps Core's text. The Amiga's "  IN THE
INN" has no colon where DOS has "IN THE INN:": an Amiga / DOS text difference, not a manual one. The member rows stay
Core's (DOS's) text, but the pool's Amiga row "%-10.9s IS CHANGED" (a blank more than Core's "{0,-10}IS CHANGED") is
printed on the Amiga path since s156 (`row_texts`); the inn's rows are equal. (3) The report title's topaz 8 was never loaded before s154: `FontSources.Named` needs the
font file, and the window gave the id only, so every report title (s144 on) was drawn in the remaster's 5×7; now
`MapMessageWindow.TitleFontFile`. Tests: Core `OverworldReportWindowTests`, Client `P1ReportWindowTests`. Shots (the
1920-px window halved): `p1-remaster-map-health-report-window`, `p1-remaster-combat-party-health-requester21`,
`p1-remaster-combat-show-names-requester23`, `p1-remaster-town-leaving-pelnor` (sampled to 320 × 200: the LEAVING
line pixel-equal to `p1-amiga-town-leaving-pelnor-live`). **Filmed s158** (`capture_s158_filming.md`): the
inn's "  IN THE INN" report, the map's HEALTH REPORT (box (33, 45, 255, 110), OK (42, 136, 235, 9) as built), the fight's
Party health (Top 45, not 23: corrected), requester 25's band, a row click ending a MESS window, the uncut window length.
The magical pool (OUT5 (16, 13) only) not filmed `[~]`.

## Requesters 0, 22 and 14 (s154, static) `[C]`

Dumped s154 with a hunk reader (as s144: the struct at h84 `+0x3fc6 + n · 0x70`, its Borders `+0x186a + 16i`, ITexts
`+0x1d2a + 20i`, Gadgets `+0x2dba + 44i`, the relocated pointers resolved per hunk).

- **The run-time pen remap.** h2 `+0x6c` (boot) and `+0x144` (after every town visit) call h77 `+0x112e`, which puts the
  pens of Borders 0–52 (Front / Back), ITexts 0–171 (Front / Back) and requesters 0–19 (BackFill) through the table h69
  `+0x19c` = 0→0, 1→12, 2→1, 3→14, 4→3, 5→10, 6→2, 7→9, 8→4, 9→7, 10→6, 11→13, 12→8, 13→11, 14→5, 15→15; h2 `+0x12e` calls
  h77 `+0x1230` (table h69 `+0x1ac`, the inverse) before the town (h5). So the town and the Guild show the **stored** pens
  on the town palette (h69 `+0x54`, pack `palettes.town`), the map, the dungeon and the fight the remapped ones on their
  palettes. Requesters 20 and up (21, 22, 23 …), Borders 53 and up and ITexts 172 and up are never remapped, which is why
  requester 21's stored pens were right as they stand (s144). Checked: requester 0 on shot `-00` (fill 5→10 `#446600`,
  borders 2→1 `#EE0000`), 22 on `p1-amiga-black-knights` (its own fill 0, Borders 60 / 61 pen 8 `#666666` and IText 194
  pen 12 `#44EE22` unremapped, its buttons' Borders 0 / 1 pen 2→1 `#EE0000`), 14 on `p1-amiga-guild-inspect-sheet`
  (stored: border pen 3 `#880000`, name 9 `#00AAEE`, LEVEL 6 `#00EE00`, rows 4 `#EEEE00`, buttons pen 2 `#EE2244`).
- **Requester 0** (h84 `+0x3fc6`): Left 81, Top 63, 158 × 73, backfill 5, ReqBorder Border 3 ((3, 3), 152 × 67) → 2
  ((2, 2), 154 × 69) pen 2, ReqText ITexts 0–4 (pen 15 on 5, JAM2, at (10, 11 + 7k), z6font), gadget 0 (11, 53) 64 × 9 id 0
  (render Borders 0 / 1 / 4: pen-2 outlines at (−2, −2) 68 × 13 and (−1, −1) 66 × 11, the pen-15 face's 18 points; IText 5
  pen 0 on 15 JAM1 at (2, 2)), gadget 1 (82, 53) id 1 (IText 6), both RELVERIFY | GADGIMMEDIATE.
  **h86 `+0x32a(text)`** is its only filler (39 callers; the map's: h59 `+0x350` "[original text]" (h60 `+0`),
  `+0x408` "[original text]" (h60 `+0x18`), `+0x418` "[original text]" (h60 `+0x2b`), h41 `+0x32e` "A DUNGEON.
  ENTER?[YES|NO]" (h42 `+0x75`), h71 `+0xcb0` "[original text]" (h72 `+0xc7`), h74 `+0x342` "YOU SPOT|SLEEPING
  MONSTERS![ATTACK|LEAVE]" (h75 `+0x67`), `+0x472` the sprintf of "%s[original text]" (h75 `+0xa3`, at
  `+0x456`)). It clears slots 0–7, copies the text into slot 0, 1, … ('|' ends a line), '[' starts the labels in slot 6
  ('|' to slot 7), ']' ends; with no or one '|' before '[' (one or two lines) it rotates the slot pointers 0–5 twice
  (`+0x502(1)`), with two once, with more not at all, and repoints the ITexts (`+0x55a`), so **one or two lines start on
  row 2 (y 25), three on row 1**; a label's LeftEdge := (10 − strlen) · 3 + 2 (ITexts 5 / 6), a line's := (23 − strlen) ·
  3 + 10 (ITexts 0–4); no '|' after '[' (one button): gadget 0 LeftEdge 47, NextGadget 0; one '|': LeftEdge 11, NextGadget
  gadget 1; then `h86 +0(0)` (the sprite off, Request, the GADGETUP wait; the gadget id is the answer index). Shot `-00`
  ("VILMAR" / "HEARS MONSTERS" on rows 2 / 3, ink x 142 / 118, RUN 115, FIGHT 180) is pixel-equal to this model over the
  whole requester. A one-line requester sits on row 2 (the code's rotation): filmed s158 ("A DUNGEON.  ENTER?", "[original text]"), pixel-equal (tests `OneLineQuestion_MatchesTheLiveShot`, `InnCantAfford_IsAnOkRequester_MatchesTheLiveShot`).
- **Requester 22** (h84 `+0x4966`, opened only by h65 `+0x168(0x16, 2, 4)` from h74 `+0x218`): as in "Fonts" below; its
  gadgets 90–92 (ids 2–4) at (11, 42), (83, 42), (155, 42) with the Borders 0 / 1 / 4 render; Border 60 (0, 0) 227 × 60 → 61
  (1, 1) 225 × 58 pen 8. The shot is pixel-equal to the stored struct with the remap rule above.
- **Requester 14** (h84 `+0x45e6`): Border 32 (0, 0) 241 × 139 → 33 (1, 1) 239 × 137 pen 3, backfill 0; ITexts 123 (topaz,
  pen 9, (7, 7)), 124 (pen 6, (7, 20)), 125–130 (pen 4, (7, 31 + 9k)), 131–136 (pen 4, (121, 31 + 9k)), 137–142 (pen 1, y 87,
  x 7 / 55 / 85 / 127 / 163 / 205); gadgets 41 (−231, −38) SPELLS id 1, 42 (−151, −38) EQUIPMENT id 2, 43 (−71, −38) SKILLS
  id 3, 44 (−231, −17) POTIONS id 4, 3 (−71, −17) EXIT id 0, all GRELRIGHT | GRELBOTTOM (x = 279 + LeftEdge, y = 168 +
  TopEdge: (48, 130) … (208, 151) on the screen, as the shot), IText labels pen 0 at (14 / 5 / 14 / 11 / 20, 2).
  **h29 `+0x93e(record)`** fills it: slot 124 := the name (`+0x10e`); slot 125 := sprintf("LEVEL%3d %s %s %s", `+0x58`,
  class h12 `+0xaaa`[`+0xdc`], race `+0xb2a`[`+0x100`], status `+0xb78`[`+0x18`]); the stat rows are templates in their
  slots (h84 `+0xb8e` …: "STRENGTH     XXX" …) whose value h23 `+0x3b0(slot, text, column)` writes over the X's: STR …
  LUCK (`+0` … `+0xa`) "%3u" at column 13; AGE "%7u" of `+0x108` / 26 at 12; HIT POINTS "%3u/%-3u" (`+0x16`, `+0x14`) at
  12; MAGIC the same of (`+0x12`, `+0x10`); GOLD IN BANK "%5u" (`+0x106`) at 14; EX. PTS "%10lu" (`+0x10a`) at 9; SCORE
  "%3u" (`+0x46`) at 16. The rune row: IText 137 "RUNES:" is blanked (h23 `+0xd4e`) unless rune 0, 2 or 3 (`+0x48`,
  `+0x4c`, `+0x4e`) is held (DOS's rule), and each of AIR / EARTH / FIRE / WATER / GOD (ITexts 138–142) unless its word
  `+0x48 + 2i` is set. h12's names equal Core's but the status "OK". The remaster prints the manual's words (D-064; user s155: the Amiga's are
  font text, not art, so the manual wins): "OKAY" here, in the map Health rows and requester 6's rows, and "EXPER. POINTS"
  (value in six cells, a blank before a 6-digit or longer value; a row that would reach the border widens the requester,
  `P1MapRequesters.Sheet`) for "EX. PTS". **h29 `+0xcca`**
  shows it with `h86 +0(14)` and loops: 1 → `+0xe90`, 2 → `+0xee2`, 3 → `+0xd46` (skills), 4 → `+0xfb6`, 0 (EXIT) → back
  (the four sub-views not traced `[~]`). Callers: the Guild's Inspect (h5 `+0x1a02`) and the map's (h50 `+0x7b2`, after
  "[original text] INSPECT:" through h62 `+0x508`), the latter with the remapped pens on the map palette.
  **The PURGE / KEEP variant is Guild › New's rolled character, not a Purge confirmation** (h23 `+0xc08`, only caller h5
  `+0xc32`, inside the New routine `+0x832`, which rolls the stats, sets AGE `+0x108` := 260 (10 years) and loops back to
  `+0x83a` to roll again): gadget 41's NextGadget := 43 and 43's := 0 (two buttons), IText 143 := slot 0x99 "PURGE", 145 :=
  0x9a " KEEP ", 123 := the blank slot (no name); fill, show, then SPELLS / SKILLS / the name and the links are put back;
  it returns answer = 3 (" KEEP "). PURGE (answer 0) clears the new record's `+0x5c` and rolls again; KEEP runs `+0xfd6`
  (the name's letters to upper case) and keeps the member. The Guild's real Purge (`+0xe44`, "[original text]
  PURGE?" h6 `+0x85`) picks a member and deletes it (`+0x708`) with no sheet, as DOS.

**Built s154:** pack `map_requesters` (`MapRequestersLayout`: `menu` = requester 0 and h86's rules, `menus` = the
remaster's DOS menu ids 0xe / 0x1c / 0x16 / 0x10 / 0x11 → the Amiga's h86 texts as UI strings `p1field.req.*`, `oks` =
Core's STYX / OCEAN lines → their texts, `black_knights` = requester 22, `sheet` = requester 14 with `palettes.town`), every
pen the screen pen above. `P1MapRequesters` lays them out as draw steps (`Parse` is h86's parse; `Menu`, `Fixed`, `Sheet`),
`UiOverlays.DrawP1Requester` draws them (titles topaz 8, the rest z6font) with a backdrop hit region and one per button.
Under AMIGA SCREENS, I: the HEARS / SLEEPING / Black Knights menus and "A TOWN / A DUNGEON / AN INN.  ENTER?" over the
overworld are these requesters instead of the prompt panel (their text off the message line); a click on a button sends
the key of its answer (Y / N on YES / NO, else the digit); the remaster's keys and pad work as before (the Amiga: a click
only; keys kept, user decision s154). A blocked wilderness step whose log has Core's "THE RIVER STYX" / "[original text]"
opens requester 0 with OK (`Game1.P1Requesters.cs`): it owns every key, button and click until a new key, a pad button or
the OK click (Return) closes it; the party sprite is off and the water stands still under it, the line stays empty, a
diagonal step stops there. Inspect's sheet in the town or the Guild (`InspectDialog.SheetMember`) is requester 14 over the
town picture: SPELLS / EQUIPMENT / SKILLS / POTIONS send 1–4 (the remaster's own pages follow `[~]`), EXIT sends Esc (back
to the picker). Texts on this path are the Amiga's ("[original text]", "A TOWN.  ENTER?" with two blanks, "YOU SPOT" /
"SLEEPING MONSTERS!" on two rows, OK); the DOS path keeps Core's. Dev switch `--p1-map-question
black-knights|hears|sleeping|town|dungeon|styx|ocean` (with `--test-party`). Tests `P1MapRequesterTests` (the three shots
pixel-equal over each requester, the sheet but its status word; the rune rule; the OK layout; a click answers every
prompt kind). Shots `parity/shots/p1-remaster-map-requester-hears.png`, `-black-knights`, `-sleeping`, `-town`,
`-dungeon`, `-styx` (dev switch), `-ocean` (a real blocked step north of the start), `p1-remaster-town-inspect-sheet.png`
(the remaster's layout, 1 px = 5 window px).
~~Left: the map's Inspect sheet, Guild › New's rolled sheet, the dungeon's and the town's h86 questions~~ built s156
(below). With the map-only layout off the requesters sit over the remaster's sidebar, as the s139 boxes do `[~]`.

## Requesters, s156 (static, `[C]` unless marked)

Read with a hunk loader (HUNK_HEADER / CODE / DATA / BSS / RELOC32) and capstone's m68k, relocations named `hN +off`.

- **Every `h86 +0x32a` caller (39).** Map / dungeon: h59 `+0x350` A TOWN, `+0x408` STYX, `+0x418` OCEAN, `+0x4d2` "EVENT NOT
  FOUND!|TRY TO FIX THE POSITION[OK]" (a data error); h41 `+0x32e` A DUNGEON; **h41 `+0x15e(text)` = sprintf("%s[YES|NO]",
  text)** (h42 `+0x52`) and **`+0x11c(member, text)` = sprintf("%s %s", name, text) then `+0x15e`**: "[original text]"
  (`+0x41c`, h42 `+0x90`), "[original text] MAP?" (`+0x444`, `+0xa2`), name + " [original text]
  IT?" (`+0xae6`, `+0xca`), name + " [original text] BACK?" (`+0xfbe`, `+0x171`); the answer is 1 for YES. The name's
  line keeps the blank ("VILMAR "), so it is centred by 7 cells; three '|' before '[' mean no rotation: rows 0–3 (y 11,
  18, 25, 32). **"YOU FIND %s[original text]"** (h45 `+0xcf`, the item's h12 `+0x7aa` name) is h44 `+0xfe4(item,
  random)`: from the dungeon's op 6 (h41 `+0x1716`) over the dungeon, and after a fight (h44 `+0x942` / `+0x956`, items
  h4 `+0xe4` / `+0xe6`) **before** h53 `+0x2a4` leaves the fight screen, so that one sits over the fight field in the combat
  palette (the remaster asks over the map: map palette `[~]`). The remaster prints Core's item name (the longest,
  "SAPPHIRE A"…"E", makes a 20-cell line, inside the 23 cells), not h12 `+0x7aa`'s `[~]`. h44 `+0x9c4` "WRONG MONSTER # !!![SORRY]", h50 `+0x9e0`
  "MONSTERS ARE LEVEL %-2d[SORRY]", h56 `+0x7d8` / `+0x816` "IS OPENED[SORRY]" / "IS CLOSED[SORRY]", h71 `+0xe76` "YOU CAN'T
  AFFORD IT[SORRY]" (the wilderness inn), h41 `+0x1e4e` "NOT USED BUT ACTIVATED![SORRY]". Town: h5 `+0x109c` "NAME IS NOT
  UNIQUE[SORRY]"; h26 `+0x1d2` "YOU NEED %ld MORE|[original text]| |GO AND LEARN![O.K.]", **`+0x204` "IT WILL COST|%d
  GOLD[AGREE|NOT]"** (training), `+0x7d4` "ARMORY IS EMPTY[SORRY]", `+0x10` "HE'S JUST BOUGHT|NINTH ITEM| |CHANGE
  SHOPPER![O.K.]"; h26 `+0x6e(text)` plays h89 `+0xe0` (the beep) first, for the h84 `+0x1a8…+0x1dc` table: THERE IS NO
  ROOM, YOU CAN NOT AFFORD IT, YOU MUST ASSEMBLE|A GROUP, YOU HAVE LEARNED[O.K.], NO ONE IS WISE ENOUGH|TO TEACH YOU, **NOT IN
  THE TOWN|IT TAKES 2 MONTH|TO GET HERE[YES|NO]** (h26 `+0xc2a`: Guild › Add's move question), YOU CAN NOT LEARN|ANY MORE,
  NOT ENOUGH MAGIC, D O N E !, UNUSABLE, YOU HAVE FAILED, HE HAS NINE ITEMS, DO YOU WANT PRINTOUT?, PREPARE PRINTER. The rest
  are disk / memory errors.
- **Built:** pack `map_requesters.menus` 0x1d / 0x1e / 0x1f / 0x1b, `kinds` (DungeonTrap) and `town` (guild_add_move,
  training_cost); `Game1.P1MapRequesterFor` draws requester 0 for the dungeon's TAKE IT / SAVE MAP / trap / lock questions
  (dungeon) and KEEP IT (dungeon or overworld), `P1TownRequesterFor` the Guild's move and cost questions with the stored
  pens (`pen_remap`'s inverse) on the town palette. The trap's buttons carry D-052's manual labels DISARM / LEAVE (the
  Amiga: YES / NO; DIFFERENCES D-052). Clicks send the remaster's keys (Y / N, A / N, 1 / 2). The town's OK / SORRY alerts
  stay the remaster's message line `[~]`. **s158:** the beep (h26 `+0x6e` → h89 `+0xe0`, pack 32) plays before the move
  question and with the remaster's lines for the other h26 `+0x6e` / `+0x1a8` alerts (pack `cue_sounds.alerts`).
- **Requester 14 over the map** (h50 `+0x7b2`): requesters 0–19 / Borders 0–52 / ITexts 0–171 go through h69 `+0x19c` after
  the town, so the sheet's stored pens become border 14 `#2266AA`, name 7 `#0088AA`, LEVEL 2 `#228800`, rows 3
  `#EEBB00`, runes 12, button borders 1 `#EE0000` on the map palette (pack `pen_remap`; `P1MapRequesters.Remap`). Filmed s158
  over OUT1 (EMILY) with its four views: pixel-equal but the pointer and D-064's words (tests `MapSheet_MatchesTheLiveShot`,
  `MapSheetPages_MatchTheLiveShots`). Every Inspect step over the overworld (the summary with INSPECT / EXIT, the who list,
  the sheet, the views) has the party sprite off (filmed; built s158).
- **Guild › New's roll** (h23 `+0xc08`): ITexts 143 / 145 (the buttons' labels) both at (14, 2), pen 0; gadget 41 (PURGE,
  id 1) and 43 (" KEEP ", id 3); the name IText on the blank slot; h89 `+0xd2` (the tone) after the answer. Built as
  `sheet.rolled_buttons` over the remaster's Accept step (`GuildNewDialog.RolledMember`; PURGE sends N, KEEP Y); a
  descendant's roll (T5) keeps the remaster's panel (its ancestors' line has no place). Where the Amiga asks the name
  (before or after the roll) not traced `[?]`; the remaster keeps its D-075 flow.
- **The sheet's views** (h29 `+0xcca`): SKILLS `+0xd46` = requester 9 (h84 `+0x43b6`: (72, 69) 176 × 62, backfill 0, Borders
  pen 15 at (0, 0) / (1, 1), ITexts 49–56 pen 4 at (6, 6 + 9k) / (82, 6 + 9k): "ATTACK %3u", "PARRY", "SWIM", "LISTEN" from
  record `+0x1e` / `+0x20` / `+0x30` / `+0x28`, then "SPOT TRAP", "DISARM TRAP", "FIND ITEM", "PICK LOCK" from `+0x24` /
  `+0x26` / `+0x22` / `+0x2c`; EXIT = gadget 3 at GREL (−71, −17) = (176, 113)); EQUIPMENT `+0xee2` = requester 10
  (backfill 7, ITexts 57–59 pen 0 at (29, 12 / 21 / 30): "%-13.12sPTS:%-2u" of the weapon `+0x56`, armor `+0x54`, shield
  `+0x52`, points h8 `+0x1f6`); POTIONS `+0xfb6` → `+0x842` = requester 11 (backfill 7, ITexts 60–63 pen 15 at (9, 8 / 16 /
  25 / 34)): "%-3d" over "XX HL POTIONS XX MG POTIONS" and "XX SCROLL(S)  XX TREASURES " at columns 0 / 14 (items 101–110,
  111–120, 121–140, 151+ of the nine at `+0xde`), row 3 "RINGS:   " with the letters of 141–150 written at column 7 (blank
  unless one is held), row 4 "[original text]" (blank unless 150). The letters go in with h23 `+0x3b0`, which copies
  no NUL: slot 0x3f "RINGS:   " sits at h84 `+0x641` and slot 0x40 "[original text]" at `+0x64b`, so the nine
  letters at `+0x648…+0x650` overwrite the template's NUL and the wand text's first six characters. From the first POTIONS
  view on (the write comes before `h86 +0(11)`, and the slots are never restored) row 3 reads "RINGS: " + the nine letters
  + "ND OF NIKADEMUS" and the wand row (when shown) the letters of 144–149 + "ND OF NIKADEMUS" (static, not filmed `[~]`;
  the remaster prints the clean rows: user s157, an Amiga bug, fixed, D-081). SPELLS `+0xe90` = the fight's requester 12 ("The spell list",
  title "[original text]", h23 `+0x570(record +0x60, 1)`), browse only. **Built:** pack `sheet.pages` (equipment /
  skills / potions), `P1MapRequesters.SheetPageFor`, in the town (stored pens) and over the map (remapped); EXIT sends Esc
  (back to the sheet). **s158:** SPELLS is the fight's requester 12 (`P1MapRequesters.SheetSpells`, the combat list's
  screen pens unmapped to stored ones): browse only, scroll gadgets by six, a row or EXIT back to the sheet; filmed over
  the map (EMILY) and in the Guild (OMAR, town palette), both pixel-equal but the pointer (tests
  `SheetSpells_IsRequester12_MatchesTheLiveShot`, `SheetSpells_InTheGuild_MatchesTheLiveShot`). The tone (pack
  `cue_sounds.sheet_tone`, h89 `+0xd2`) plays after each answer of requester 14 (h29 `+0xcf4`), each view's EXIT
  (`+0xe86` / `+0xf52` / `+0x934`), each requester-12 gadget (h23 `+0x734`) and PURGE / KEEP (h23 `+0xc56`); the remaster
  shows the next step at once and holds input for the tone (the Amiga acts after it) `[~]`.
- **sprintf** (h137 → h167, the program's `_doprnt`): a precision is parsed (`.` then digits into `-0x1c`) but used only
  by `%s` (the most characters) and the float conversions; `%d` / `%u` / `%x` / `%o` ignore it. So `%-3.2d` prints as
  `%-3d` and `%-4.3d` as `%-4d` (the fight's Party health, the orders rows: as built). Every integer argument is read as a
  long.
- **The dungeon's line** prints a message cell's MESS record as stored (40 characters, h41 `+0x10e6`) centred by all 40:
  built s156 (`map_message.line.record_cells`; `GameSession.LastDungeonMessageRecord`; `UiOverlays.P1MapLineRows`'s
  `record`): "[original text]" at x 40, a stored leading blank a cell right.
- **A save loaded while an event menu waits** gets the stored option cells back from the level's message when the saved
  choices are that message's (`GameSession.ApplySnapshot`).
- **The pool's rows** on the Amiga path print h72 `+0xb4` "%-10.9s IS CHANGED" (`map_message.window.row_texts`,
  `P1MapReports.Row`).
- **The boxes' texts (not changed, a question for the user):** on the Amiga path the requesters (s154) and report titles
  print the Amiga's text, the s139 / s140 requester-23 boxes Core's (DOS's). They differ in four: h75 `+0xc0` "YOU
  SURPRISE THEM!", `+0xd3` "[original text]", `+0x47` "[original text]" (DOS: a blank before "!") and
  h42 `+0x5d` "THAT IS A WALL" (DOS "[original text]"). The manual names none. `[U]`
- Shots (Client on Xvfb, 1920 × 1032 windows): `parity/shots/p1-remaster-map-overworld-frame.png`,
  `-map-about-phantasie.png`, `-map-inspect-sheet.png`, `-map-sheet-skills.png`, `-map-sheet-equipment.png`,
  `-map-sheet-potions.png`, `p1-remaster-guild-new-purge-keep.png`, `p1-remaster-dungeon-requester-exit.png`,
  `-dungeon-requester-save-map.png`. Tests `P1MapRequesterTests`, `P1MapBarTests`, `P1ReportWindowTests`, Core
  `DungeonWindowTests.ALoadWhileAMenuWaits_KeepsTheStoredCells`.

## Overworld / dungeon map graphics (s52) — the graphics the remaster shows

User directive 2026-10-24: **no DOS CGA art on screen**; Amiga graphics everywhere (later a player-selectable
graphics set). Extractor: `tools/scripts/extract_amiga_map_graphics.py` → `extracted/maps/` (+ `maps.json`).
Static RE of `game` (hunk-relative offsets; hunk 69 = graphics data at file `0x19428`, hunk 56 = map code,
57 = its data, 58 = its BSS, 77 = drawing primitives):

- **Overworld is not a tile grid.** `h56:0x948` sets 12×9 cells at origin (4,15) (26×20 = (4,15,312,180)).
  `h56:0x6bc` fills the map rect with the pattern in the first word at `OUT*.DAT + 0x3e8`, then area-fills polygon
  records `[count][pattern][count × (x,y)]` (BE words, screen pixels) until count = −1 (AreaMove/Draw/End,
  `h77:0x614`). The Amiga `OUT*.DAT` is 2500 B: grid cell (x,y) at `(25 − x)·20 + y` (`h56:0x214`), polygons at 1000.
- **Patterns:** 12 × 16×16 multicolour (`AreaPtSz = −4`), 4 sequential planes, 128 B at hunk 69 + `0x294` (file
  `0x196bc`), index = terrain class; 4/5 are black (town/dungeon icons drawn on top). Screen-aligned (x&15, y&15).
  Palette hunk 69 + `0x74` (file `0x1949c`).
- **Icons** (`h56:0x5a4` → `h77:0xaf4`): class 4 = town (tile 41 = inn), class 5 = dungeon; one per kind per
  region (last hit). 16×9 4-plane bitmaps at hunk 69 + `0x1bc` + kind·`0x48`, blitted 12×9.
- **Party:** hardware sprite (`h77:0xdfe`), SimpleSprite data hunk 69 + `0x128`, height 9, colours 0x000/0xE00/0xEB0.
  **s153 (static) `[C]`, under the map's questions:** h86 `+0` (the show of every h86 requester) and its wait `+0x132`
  check h78 `+0x12` ≠ −1 (the sprite allocated) and call MoveSprite (h236 `+0x228`, graphics −0x1aa) with y = 200
  (`move.l #$c8`), below the screen, before `Request` (h237 `+0xd8`, −0xf0) and again with the saved y after
  `EndRequest`; h86 `+0x32a` (the menus) ends in h86 `+0`. So the sprite is gone under every h86 requester over the map:
  "A TOWN.  ENTER?" (h59 `+0x350`), "THE RIVER STYX" / "[original text]" (`+0x408` / `+0x418`, OK requesters),
  "A DUNGEON.  ENTER?" (h41), "AN INN.  ENTER?", the SLEEPING and HEARS menus (h74 `+0x342` / `+0x472`), BLACK KNIGHTS
  (h65 `+0x188`). Requester 23 frees it instead ("The map screen's requester 23", s143). **Built s153:** pack
  `map_layout.party_hidden_under_prompts`, `MapPartySprite`: under AMIGA SCREENS the overworld draws no party sprite
  while any question is up (every remaster question over the map is one of those h86 menus); shots
  `parity/shots/p1-remaster-map-inn-prompt-no-sprite.png`, `-inn-sprite-back.png` (HEARS itself not filmed in the
  remaster: the same prompt path). Left: ~~STYX / OCEAN are OK requesters on the Amiga, the remaster prints them on the
  message line~~ built s154 ("Requesters 0, 22 and 14": their OK requester, the sprite off under it); the remaster's own dialogs over the map (cast, inspect, use
  potion, health, time lag) keep the sprite: their Amiga counterparts (menu-strip picks, h62 `+0x12` / `+0x508`
  choosers through h86 `+0`) not mapped one by one `[~]`.
- **Seen on FS-UAE (s128)**, master ADF, six guild members added in Pelnor, OUT1 just outside Pelnor
  (`parity/shots/p1-amiga-map-out01.png`, `p1-amiga-map-out01-town-icon.png`: FS-UAE's 640×400 crop halved; crop row =
  screen row + 5, measured on the town picture): all 56,160 map-rect pixels equal the extracted region with the inn,
  dungeon and town icons at their cells' top-left and the party sprite on its cell (only the pointer differs), once the
  polygon fill was fixed: an AreaEnd polygon covers the scan-line interior **plus every edge drawn as a blitter line**
  (from vertex i to i + 1 in record order; error 2·dmin − dmax, minor step when ≥ 0). PIL's own outline left 235 pixels
  one row off on near-horizontal edges (`extract_amiga_map_graphics.py` `area_mask`, all 18 regions re-extracted).
- **Water cycle (s128) `[C]`:** h77 `+0x178`, called from the input wait loop (h80 `+0x1ac`, while no IDCMP message
  waits), rotates the words h78 `+6/+8/+a` (`08B`, `08A`, `26A`) once 200000 µs of CurrentTime have passed (h80
  `+0xd2a`, interval h78 `+2` = `0x30D40`) and SetRGB4s them into pens h78 `+0xc` = 4, 7, 14 (pen 4's colour moves to
  7, 7's to 14, 14's to 4). Switched on by h62 `+0` (only h2 `+0x196`, the wilderness state) and off by h62 `+0xa`
  (town, dungeon and other states). Seen: a pen-4 pixel shows `08B` → `26A` → `08A`, ~0.22 s a step. The pack's
  `colour_cycle` (registers 4, 7, 14; 0.2 s) draws it. **s153 (static) `[C]`:** h77 `+0x178`'s only caller is h80
  `+0x1ac`, in the main input loop h80 `+0x152` (GetMsg; none waiting → h80 `+0xdb6`, then `+0x178`); requester 23's wait
  (h80 `+0x546`) and h86's (`+0x132`) never call it, so the water stands still while a box, a requester or a step runs.
  h80 `+0xd2a(interval)` compares (CurrentTime seconds mod 8, µs) with the stored time h82 `+0`/`+4` and, when the
  interval has passed (or the mod-8 seconds wrapped: negative, compared unsigned), stores the new time and returns 1: one
  step at once on the first idle poll after a pause, no catching up. **Built s153:** `colour_cycle.runs_while`
  "input_wait" and `clock_wrap_seconds` 8, `InputWaitCycleClock`: the Client counts steps only while the overworld
  waits (no box run, question, dialog, overlay or fight screen) on the host clock the boxes' 8-s cut reads. The ST's VBL
  cycle (II) keeps running. Left: h80 `+0x546` also writes h82 (its start time), so after a box the next step comes ≤ 0.2 s
  after the box opened (the remaster: after the last step), invisible since a box always lasts ≥ 0.5 s; the remaster's
  own overlays (game menu, inventory, bestiary) stopped it, where the Amiga's menu strip lets the loop poll on (built s154, below); a map
  redraw (LoadRGB4 of h69 `+0x74`) shows the stored pens until the next step, with the rotated words h78 `+6`…`+a` kept
  (the remaster keeps the phase) `[~]`. **s154 (static) `[C]`:** the LoadRGB4 of h69 `+0x74` on the map is h56 `+0x4d4`
  (the region's OUT%d.DAT read and drawn), called only by h59 `+0x62c`, from h2 `+0xb6` (start), h2 `+0x346` (h2
  `+0x328`: back in state 2 from the town (state 1), the dungeon (3) or the defeat (6)) and h59 `+0x8c` (a step into
  another region); after a fight h53 `+0x2a4` loads back the pens it saved at its start (the rotation as it stood). The
  menu strip: Intuition sends nothing while it is held, so h80 `+0x152` keeps polling and the water keeps turning; a pick
  that opens a chooser or requester (h62 / h86) stops it. **Built s154:** `InputWaitCycleClock.Redraw` / `StoredShown`:
  the Client shows the stored colours from a return to the overworld from anything but a fight (town, dungeon, defeat,
  load) or a region change until the next step (the next step shows the kept phase + 1); the remaster's game menu,
  inventory and bestiary keep the water turning (they stand in for the menu strip), its dialogs (cast, inspect, potion,
  health, …) and the s154 holds stop it. **s156:** a defeat's return redraws too when the party stays in its region
  (`Game1.AnswerPrompt` forgets the last map). Left `[~]`: the first idle poll after a pause steps at once, so the stored colours show only while something else runs.
- **The clock (s153, static, not built) `[~]`:** h80 `+0xd2a` and requester 23's h80 `+0x546` read Intuition's
  CurrentTime (h237 `+0x50` = intuition −0x54), the time of the last input event, which input.device's timer events move
  on about every 0.1 s; h80 `+0x546` reads its start (`+0x570`) before it draws and `Request`s the box (`+0x636`). So a
  box runs short by the start reading's staleness (0–0.1 s) plus its own drawing time, which fits the live boxes
  0.06–0.14 s under whole seconds (s143), and the cycle's ~0.22 s steps. The remaster times both on the host clock; not
  modelled.
- **Keys on the map (s153, static) `[C]`:** the input loop h80 `+0x152` takes RAWKEY (class `0x400`) only under the map's
  menu strip (h81 `+0x270` = `+0x33c`) and only codes `0x4c…0x4f` (the cursor keys → 5…8); MOUSEBUTTONS hit-test the
  screen's buttons (results 10…15, `0x32`), MENUPICK the menus. No function key (F2 = `0x51`) is read anywhere (the only
  other GetMsg loops, h77 `+0x3a6`, h80 `+0x4e4` / `+0x654`, h86 `+0x132`, flush or take any key / a GADGETUP). So the
  remaster's F2 (`ToggleMapLayout`, the map-only layout at the Amiga's (4, 15)) is the remaster's own; the Amiga's map
  screen is always that layout. s128's "F2 did nothing on Xvfb" was the harness: xdotool 3.20160805 sent F-keys with
  Alt_L held and metacity took Alt+F2 (its run dialog), so the Client got only the release; `client_xvfb.sh` now sends
  F-keys by keycode through XTEST, and F2 toggles live (s153).
- **Fog:** map rect cleared to black, rects of cells < 120 copied back (`h56:0x254/0x2ea/0x860`).
- **Dungeon map:** `h56:0xd10` 5×4 cells at (10,16); glyph ids `0xe6..0xed` as DOS (`h56:0xf76`), 16×4 4-plane
  bitmaps, 32 B each at hunk 69 + `0x894` (`h77:0xbc0`), blitted 5×4. Compass 64×63 at hunk 69 + `0x2af4`, drawn at
  (214,68) (`h56:0xe98`; not shown by the remaster yet). The Amiga saves the drawn dungeon map as a 32000-B screen.
- **vs DOS:** terrain equals the DOS grid except OUT2 (11,8) and OUT12 (11,14) (Amiga water patch, DOS mountain /
  forest; Core runs DOS). **User s53: keep the DOS terrain, patch the picture** — the extractor fills each lake polygon
  with the DOS class pattern; it also applies `map-patches.json` (APPLETON's OUT13 (17,19) → town icon, D-060). Initial fog differs in OUT9/10/11/13/14. Glyph-per-cell rule: DOS OV2 `0x21df` is used;
  the Amiga's own rule is not checked `[~]`.

## Fonts (s134) `[C]`

Hunk 78 (DATA, file 0x20860) holds two TextAttrs and the NewScreen:
- **z6font 6**, TextAttr h78 `+0x36` = { `+0x2a` "z6font.font", 6, 0, 1 }. The boot code (h77 `+0x138`) opens it with
  OpenDiskFont (stub h233: diskfont LVO −30) and SetFonts it on the window's RastPort (h77 `+0x158`, stub h236 `+0x28`:
  graphics LVO −66), so Text() on the window uses it. The h84 IntuiTexts (`+0x1d2a`, 20 B each) name it, all but ITexts
  123 / 184 / 194 (the ReqTexts of requesters 14 / 21 / 22, which name topaz; requester 21's is empty on the s132
  shots and filled by the damage report, "IN THE TRAP:" on s143's shot `p1-amiga-dungeon-45`, built s144, "Requester 21
  on the map"; 14 and 22 below, s153); h81 `+0x5e` (pen 0, TopEdge 185) names it too. The file is on the game disk: `fonts/z6font.font` (FontContents,
  `z6font/6`, YSize 6, flags 0x42) and `fonts/z6font/6`, a DiskFontHeader hunk with a TextFont at code `+0x3a`:
  YSize 6, XSize 6, Baseline 5, flags 0x42 (DISKFONT | DESIGNED, fixed width), characters 0x20–0x7F, CharData `+0x6e`
  (modulo 54), CharLoc `+0x1b2`. Capitals are 5 rows from the cell's top. **a–z have the capitals' CharLoc entries**:
  lower case reads as upper case.
- **topaz 8**, TextAttr h78 `+0x22` = { `+0x16` "topaz.font", 8, 0, 1 }: NewScreen.Font (NewScreen at h78 `+0x4e`,
  320×200×4, CUSTOMSCREEN, title " ZOLI'S SCREEN"), so the screen's title bar and the menu titles, and the menu items'
  IntuiTexts (h81 `+0x36` / `+0x4a`). It is the Kickstart's ROM font: not on the disk. Read from the user-supplied KS 1.3
  (34.5) image `original-images/kickstart/kick34005.A500`: the TextFont values (an InitStruct table) at ROM `0x910a`,
  YSize 8, XSize 8, Baseline 6, flags 0x41, characters 0x20–0xFF, CharData ROM `0x9134` (modulo 192), CharLoc `0x9734`.
- Extracted by `tools/scripts/extract_amiga_fonts.py` to `src/OpenPhantasie.Data/GameData/phantasie1/graphics/fonts/`
  `amiga-z6font-6.json` / `amiga-topaz-8.json` (`phantasie.font/v1`: the font's own glyphs, advance XSize, the default
  glyph as fallback); preview sheets `extracted/ui/fonts/*.png`.
- **Checked on the s132 FS-UAE shots, every pixel of each text's cells:** z6font: "QUICKNESS I" / "MONSTER EVAL." (`-07`
  rows at (59, 91 + 8k)), the scroll gadgets' `^` / `@`, an orders row and "F I G H T" (`-04`), the line "DRAGONSON MISSES"
  (`-09`), the six order buttons (`-05`; THRUST / ATTACK were a column left before s134) and "THRUST AT RANK" with its
  digits (`-06`); topaz 8: "Info" / "Party Options" / "Speed" at pen x 12 / 67 / 195, row 1 (`-01`, `-02`), "Beg mercy" … "Party
  health" at (69, 13 + 10k) (`-02`), "*Normal" at (189, 23) (`-03`). Topaz glyphs carry blank columns, so the pack's
  `title_x` / `item_x` are pen x, 1–3 px left of the ink.
- **Built s134:** pack `combat_field.font` (z6font) and `combat_field.menu_bar.font` (topaz 8); the Client draws I's
  combat screen (behind AMIGA SCREENS) in them (`PixelFont.With`), the rest of the remaster's text keeps the pack's
  `font`. Test `P1AmigaFontTests` holds the shot checks. Shots `parity/shots/p1-remaster-combat-font-orders.png` /
  `-font-spells.png`.
- **s135, more text checked against z6font on exact FS-UAE shots** (all pixel-equal): requester 6 "ON WHOM:" (title,
  member rows, EXIT; `-19`), requester 23 (centred, "The combat screen"; `-23` / `-24`), the town name (`p1-amiga-town-
  pelnor-exact`), the start-up requester "DO YOU WANT:" / "[original text]" (`p1-amiga-start-menu`, the remaster has
  no such screen), the overworld's "HEARS MONSTERS" requester (`-00`) and the victory screen's "WELL DONE…" line
  (`p1-amiga-victory-07`). Town name and messages drawn in z6font since s135. Not drawn in the Amiga fonts yet: the
  remaster's own screens (town bar, Guild, shops: no Amiga counterpart drawn).
  Requesters 14 / 21 / 22's topaz titles never seen filled `[?]` (answered s153, below).
- **s153, who fills the topaz ReqTexts (static, then FS-UAE).** The IText text pointers are all 0 in the file; h86
  `+0x55a` (boot) points them at the text slots (pointer table h84 `+0xd4a`, slot k at `+0xd4a + 4k`): IText i ← slot i for
  i < 5 and slot i + 1 for 5 ≤ i < 0x98, then ITexts 172–183 ← slots 112–123, 186–192 ← 101–107, 193–197 ← 158–162,
  198–200 ← 101–103, 202–211 ← 112–121, and singly IText 184 ← slot 156 "HEALTH REPORT:", 185 ← 157 "F I G H T", 193 ←
  158 "INSPECT", 201 ← 0xfd6 "THRUST AT RANK:". `h23 +0xd28(slot, text)` copies a string into a slot, `h23 +0xd4e(itext,
  slot, blank)` repoints an IText (blank: slot 155, a NULL pointer).
  - **Requester 14 = the character sheet** (Left 39, Top 30, 241 × 139, backfill 0): ReqText IText 123 (topaz, pen 9 in
    the struct, at (7, 7)) ← slot 124, which `h29 +0x93e(record)` fills with the member's name (record `+0x10e`); IText 124
    "LEVEL%3d %s %s %s" (h30 `+0x73`) at (7, 20), the stat rows (z6font) from (7, 31) and (121, 31); buttons SPELLS /
    EQUIPMENT / SKILLS / POTIONS / EXIT. Shown by `h29 +0xcca` (callers: the Guild's Inspect, h5 `+0x1a02`, and h50
    `+0x7b2`, the pick list of h62 `+0x508`). `h23 +0xc08` (h5 `+0xc32`: Guild › New's rolled character, s154; not Purge) shows it with IText 123 on the
    blank slot (no title), buttons PURGE / KEEP, and puts the name slot and SPELLS / SKILLS back afterwards. **Seen s153**
    (Guild › Inspect › VILMAR, `parity/shots/p1-amiga-guild-inspect-sheet.png`, file row = screen row + 5): "VILMAR" in
    topaz 8 pixel-equal at (46, 37), `#00AAEE` (the struct pens are remapped at run time through h69 `+0x19c` / `+0x1ac`, h77 `+0x112e` / `+0x1230`); "LEVEL  1 FIGHTER DWARF OK" z6font at
    (46, 50), `#00EE00`. The remaster's Guild / Inspect are its own screens (no Amiga sheet drawn) `[~]`. **Built s154:** the town / Guild
    Inspect's sheet as requester 14 ("Requesters 0, 22 and 14"); its sub-pages and New's PURGE / KEEP variant not.
  - **Requester 22 = the Black Knights** (Left 46, Top 70, 227 × 60, backfill 0): ReqText IText 194 ← slot 159 h84
    `+0xfc6` **"[original text]"**, a fixed string (no run-time fill), topaz 8, struct pen 12, at (49, 19); gadgets ids
    2–4 (64 × 9 at (11, 42), (83, 42), (155, 42)), ITexts 195–197 "RUN" / "FIGHT" / "SURRENDER" (z6font, pen 0 on 15, at
    (23, 2), (17, 2), (5, 2)). Only opener: h74 `+0x200` (the wilderness encounter, h59 `+0x3be`'s step): sound 27
    (`h89 +0xee(0x1b)`), then `h65 +0x168(0x16, 2, 4)` (h62 `+0x12` → h86 `+0`), on a roll > 91 (`h71 +0x103a`, h74
    `+0x1d2`) with the highest SCORE h13 `+0x7c2` ≤ 83 (> 83: "the Black Knights flee", h75 `+0x47`), as DOS. **Seen
    s153** on a disk copy with h74 `+0x1d8` (`ble.w`) NOPped and `+0x1e6` made `bra.b` (every wilderness encounter is the
    Knights): `parity/shots/p1-amiga-black-knights.png` (file row = screen row + 5), title topaz 8 pixel-equal at (95,
    89), `#44EE22`; RUN / FIGHT / SURRENDER z6font black at (80, 114), (146, 114), (206, 114), buttons `#EEEEEE` with
    two-px `#EE0000` borders. The Amiga's text has no space before "!!!" (DOS menu `0xf`: "[original text]", Core's
    `core.encounter.black_knights`): an Amiga / DOS text difference, not a manual one. The remaster shows the menu in its
    own prompt panel (as HEARS / SLEEPING MONSTERS), not as requester 22 `[~]`. **Built s154:** requester 22 with the Amiga's text ("Requesters 0, 22 and 14").
- **Built s153:** pack `ending_scene.font` = `amiga-z6font-6` (`EndingSceneLayout.Font`); the Client's victory screen
  draws I's speech in it (`VictoryScreenView.SpeechFont` / `SpeechLine`), centred on 320 at TopEdge 192 (h80 `+0x84a` on
  the window's RastPort): line 1 pixel-equal to `p1-amiga-victory-07` at (49, 192) (`P1AmigaFontTests`), also in the
  running Client on Xvfb (`--test-party --test-level 10 --p1-victory`; `parity/shots/p1-remaster-victory-line1-z6font.png`,
  the 5× window sampled to 320 × 200: rows 188–199 equal the Amiga shot). II's justified lines and pages keep the pack's
  font.
- **s153 audit: I's screens on the AMIGA SCREENS path, the Amiga's font per the code against the remaster's.**

  | Screen / text | Amiga (code) | Remaster |
  |---|---|---|
  | Combat field: line, orders, spells, ON WHOM, buttons, requester 23 | z6font (window RastPort, h84 ITexts) | z6font (s134 / s135) `[C]` |
  | Combat menu bar and menus | topaz 8 (NewScreen.Font, h81 `+0x36` / `+0x4a`) | topaz 8 (s134) `[C]` |
  | Combat Party Options › Party health | requester 21, title IText 184 "HEALTH REPORT:" (h45 `+0x107` via h71 `+0x112e`) topaz 8, rows `%-10.9s%c HP%4d/%-4dA:%-3.2dMP%4d/%-3d` z6font, OK | requester 21 at Top 45 (s158 live; s154 read 23), topaz title, z6font rows `[C]` |
  | Combat › Show names | requester 23, `RANK %1d : %1d  %s(S)` rows from the top, not centred, z6font, ⌊(h3 + 360) / 60⌋ s (h44 `+0x138c`, traced s154) | requester 23 the same way (s154) `[C]` |
  | Map Options › Health | requester 21, title "HEALTH REPORT:" topaz 8, rows `" %-10.9s%-6.5sHP%4d/%-4dMP%4d/%-3d "` z6font, OK (h50 `+0x420`) | requester 21 the same way (s154) `[C]` |
  | Town name and town messages, line at 185 | z6font (h80 `+0x818`) | z6font (s135) `[C]`; the key help keeps 5×7 |
  | "LEAVING %s" | z6font, pen 4 (shot s153, filmed s154) | z6font, pen 4, x 106, two frames (s154) `[C]` |
  | Town / Guild / shop menus, Show party, Inspect sheet | topaz 8 menu bar and items; requesters 14 / 20 / pick lists z6font, sheet title topaz 8 | the sheet: requester 14 in topaz 8 / z6font (s154) `[C]`; the rest the remaster's own screens (5×7) `[~]` |
  | Map / dungeon requester 23 boxes, MESS windows, reports | z6font; report title topaz 8 (IText 184) | z6font (s139 / s144) `[C]`; the report title in topaz 8 only from s154 (before: the remaster's 5×7, the font file was not resolved; shots `p1-remaster-dungeon-in-the-trap-report-window` and `p1-remaster-map-swimming-report-window` predate the fix) `[C]` |
  | Overworld / dungeon top line | topaz 8 menu bar ("Info / Options / Speed", shot `-00`; s154 items and places, `capture_s154_leaving.md`; s155 h80 `+0xb48` and the strip structs) | the Amiga's bar, topaz 8, its menus (s155, map-only layout) `[C]`; the remaster's message moved to the Amiga's line (next row) |
  | Map message line (dungeon; none on the overworld) | z6font, pen 3, TopEdge 192 (h41 `+0x10c6` → h80 `+0x84a`) | z6font, pen 3, TopEdge 192 on both maps (s155), extra lines upward `[C]` for the place, `[~]` for the remaster-only lines |
  | HEARS / SLEEPING MONSTERS, Black Knights, "AN INN.  ENTER?", "A TOWN. ENTER?" | requesters, z6font (22's title topaz 8) | requesters 0 / 22 (s154) `[C]` |
  | Victory speech | z6font, pen 5, TopEdge 192 | z6font (s153) `[C]` |
  | Defeat ("[original text]", decor.pic) | not checked (h32 `+0x55c`) `[?]` | the remaster's text only |
  | Start-up "DO YOU WANT:" | z6font (shot) | no such screen |

  So the Amiga elements the remaster builds use the Amiga's fonts at the Amiga's places; the 5×7 panels marked `[~]`
  (some drawn over an Amiga screen: the wilderness menus over the map) stand in for requesters it does not build.
- **s154, the overworld's menu bar (filmed, `working/capture_s154_leaving.md`):** at rest a black bar, topaz 8 titles
  `#888888` on rows 1–8, "Info" x 13, "Options" x 67, "Speed" x 148 (ink starts); held: titles `#EEEEEE`, the lit title's
  white box Info x 3–57, Options x 58–137, Speed x 138–199; items Info: "About Phantasie"; Options: Inspect, Health, Cast
  Spell, Use potions, Look (ghosted outdoors); Speed: Fast, Normal (ticked), Slow (shots `p1-amiga-map-menu-info` /
  `-options` / `-speed`). ~~Not drawn by the remaster~~ user s155: "Move the remaster's message line to wherever it sits
  on the Amiga" (built s155, below).
- **s155, the map's menu bar (static) `[C]`:** the map's input wait h80 `+0xb48` (the fight's is `+0xbfe`) prints the
  bar as the IntuiText h81 `+0x4a` (FrontPen 13, BackPen 0, JAM2, TopEdge 1, ITextFont topaz 8 = TextAttr h78 `+0x22`):
  `" Info"` at LeftEdge 4 and `" Options   Speed      "` (h81 `+0x4ce` / `+0x4d4`) at 59, so the titles' pen x are 12 /
  67 / 147 (ink 13 / 67 / 148, as filmed; the trailing blanks clear the fight's longer "Party Options   Speed"). It then
  sets Speed's LeftEdge (h81 `+0x2c4`) to 137 (the fight: 185) and SetMenuStrips h81 `+0x270` Info (LeftEdge 2, Width
  55) → `+0x33c` Options (57, 80) → `+0x2c0` Speed (137, 62), Height 10 each: the lit boxes L + 1 … L + W are the film's
  3–57 / 58–137 / 138–199. Items (IntuiTexts built at run time in h82; their strings h81 `+0x254` " About Phantasie ",
  `+0x2e8` … `+0x316` " Inspect ", " Health ", " Cast Spell ", " Use potions ", " Look ", `+0x294` … `+0x2a4` " Fast ",
  "*Normal ", " Slow "). Measured on the exact ashots (s155): item boxes pen 0 with a pen-15 border, 1 row at top and
  bottom and 2 px at the sides: Info x 0–147 rows 9–24, Options 55–172 rows 9–64, Speed 135–212 rows 9–44; items in
  topaz 8 pen 15 at y 13 + 10k, pen x 14 (Info), 69 (Options), 141 (Speed: " Fast" / "*Normal" / " Slow", the `*` fixed
  text; fitted on the rows clear of the pointer: About Phantasie, Inspect and " Fast" lie partly under it on their shots);
  the pointed item's row complemented (10 rows from y 12 + 10k) is the fight's s132 capture, no map shot shows a lit item
  `[~]`. **Look is ghosted outdoors:**
  Intuition clears its text pixels to pen 0 where x % 4 = 3 on even rows and x % 4 = 1 on odd rows (screen coordinates;
  fitted to `-options`, every pixel). The map's Speed items set h3 (the fight's Speed word, `h65 +0x27e`). **About Phantasie (s156,
  static `[C]`):** h80 `+0x152`'s MENUPICK branch takes menu 0 item 0 (code & 0x1f = 0, (code >> 5) & 0x3f = 0) to
  `+0xcb4` (`+0x2c2`; in the town `+0x2ae` runs `+0xf08` first and sets h81 `+2`): logo.pic into the Image h81 `+0x22`,
  then requester 27 (h84 `+0x4b96`: (69, 57) 182 × 86, backfill 0, Borders 73–75 pen 15 / 15 / 0 at (0, 0) / (1, 1) /
  (2, 2), gadget 103 at (3, 3) 176 × 80, GADGHNONE | GADGIMAGE, RELVERIFY, id 1, the Image as its render) through h86
  `+0(0x1b)`, which waits for a GADGETUP (a click on the picture; keys dropped); then FreeMem. **Built s156:** pack
  `map_requesters.about` (`PictureRequester`), `P1MapRequesters.About`, the bar's About item opens it over the map
  (`Game1._p1About`, held like the STYX / OCEAN OK: the sprite off, a click on the logo or any key closes it; the Amiga: the
  click only). Not filmed `[~]`.

## The map's message line (s155, static) `[C]`

h80 `+0x84a(text, pen, y)` uses y only as a switch: y = 185 gives TopEdge 185 and the 51-blank clear h81 `+0x495` at
LeftEdge 8 (the fight's line, h53 `+0xb80`); any other y gives TopEdge **192** and the 53-blank clear h81 `+0x45f` at
LeftEdge 0 (x 0–317). Both go through `+0x892` on the JAM2 IntuiText h81 `+0x5e` (BackPen 0, ITextFont = z6font TextAttr
h78 `+0x36`): the clear first, then the text centred on 320 by its IntuiTextLength (the string as given, blanks included).
Callers (every relocation to h80 `+0x818` / `+0x84a` / `+0x892` in the program): `+0x84a` from h32 `+0x352` (the victory
speech, pen 5), h53 `+0xba8` (the fight's line, pen 3, 185) and **h41 `+0x10d8`**, the dungeon's line `+0x10c6(text)` =
`+0x84a(text, 3, 192)`: pen 3 = `#EEBB00` of the map palette (h69 `+0x74`, which the dungeon shares). `+0x818` (TopEdge
185, pen given) only from the towns, guild, bank and shops (h5, h8 via h23, h26, h29, h80 `+0x9b2` / `+0xa56`).
- **The dungeon's callers of `+0x10c6`:** `+0x1052(id)` (h4 `+0x68` := id; id 0 prints `" "`, the line cleared; the id the
  line already shows (h4 `+0x66`) prints nothing; else the MESS file's first record of id (`+0x10e6`, 40 characters as
  stored, into h43 `+2`) and h4 `+0x66` := id), called by the cell dispatch for cells 8–17 when the roll beats h3 `+2`
  (`+0x83c`, id = cell + 3; a lower roll is the fight) and the message cells `0x8b…0xb8` (`+0x8da`, id = cell − `0x76`),
  each when h4 `+0x6c` is set (the step `+0x516` sets it at `+0x5c0`, after its redraw and before the dispatch); by h65 `+0x1d6` (the redraw after a requester / menu: the dungeon branch
  redraws with h56 `+0x1030`, clears h4 `+0x66` and prints h4 `+0x64` again; the overworld branch h56 `+0x860` prints
  nothing); op 12 (`+0x19e2`: `" "`, then h4 `+0x64` / `+0x66` := 0); and h38 `+0xb40` ("%s TAKES %d DAMAGE" when its
  mode is 2). No caller passes mode 2 (the fight 0, the trap h41 `+0xcbc`, ops 1–4 `+0x1814` and the swim h71 `+0xfa0`
  pass 1, which prints nothing): dead code.
- **The overworld prints no message line.** No path from the map loop h47 (input h65 `+0`, the steps h59 `+0x4e6` …
  `+0x594` → h59 `+0`, the encounter h74, the Options handlers h50 `+0` / `+0x41c` / `+0x55a` / `+0x7c4`, the wilderness
  pool / inn / swim h71) reaches `+0x818` / `+0x84a`: its texts are requester-23 boxes, requesters 0 / 21 / 22 and h86
  menus; a map Resurrection (h50 `+0x8f4` → h38 `+0x2ea`) uses the line only in the fight (state 5), else requester 23.
  So "MOVED TO…", blocked moves, refusals and outcomes are the remaster's own lines.
- The 3× dungeon shot `p1-amiga-dungeon-41` (FS-UAE's own screenshot, not 3× vertically, its OSD over the line) agrees
  with a line near y 190; no exact ashot of the dungeon line exists `[~]` (the place is the code's).
- **Built s155 (user s155: "Move the remaster's message line to wherever it sits on the Amiga"):** pack
  `map_message.menu_bar` (the fight's `CombatFieldMenuBar` schema plus `actions`, `ghost_pattern`, `ghosted_outdoors`;
  strings `p1map.*`, Info / Speed and their items shared with the fight's `p1field.*`) and `map_message.line`
  (`MapMessageLine`: top_edge 192, pen 3, clear 53 cells from x 0, centre on 320). Under AMIGA SCREENS with I's pack on
  the map-only layout (F2) with the status line off, the overworld and the dungeon show the Amiga's bar at the top
  (`UiOverlays.DrawP1MapMenuBar`, `P1MapMenuBar`, `Game1.P1MapBar.cs`): a click on a title pulls its menu down (a second
  click, a click elsewhere or any key closes it: Esc is used up, another key still runs its command, so nothing moves
  under an open strip, which on the Amiga exists only while the button is held), the pointed item lights, a click takes it: Inspect / Health / Cast Spell /
  Use potions / Look press the command's bound key (Health opens requester 21 as H does; Look only in the dungeon, ghosted
  and inert outdoors; a click on it closes the strip with no pick, the remaster's reading of a release there, not filmed
  `[~]`), Speed sets the game's COMBAT SPEED (the fight's word),
  About Phantasie shows requester 27 (s156). The remaster's message goes on the Amiga's line (`UiOverlays.DrawP1MapLine`): z6font,
  pen 3, centred, each row on its pen-0 clear; on the overworld it covers map rows 192–194 while a line shows (the map
  fills 15–194), where the Amiga never prints. Rules for what only the remaster has (`P1MapLineRows`): text is trimmed
  before centring (the Amiga centres a MESS record by its stored 40 characters, so "--A PASSAGE WITH TORCHES ON THE
  WALL--" sits 6 px right of the Amiga's x 40 `[~]`); a line longer than the 53 cells or with several lines wraps at a
  word and stacks upward one font height (6) a row, the last at 192 (nothing is dropped); a dialog's own key help (the
  remaster's, not the map's command list) goes on the row above the message, or on the line itself when there is no
  message, in the bar's pen 13 (a remaster choice). The full-screen overworld showed no key help before (DrawMapHud) and
  still shows none outside dialogs. The dungeon's map-only screen is black under it (pen 0; the remaster's backdrop was
  `#0A0A0E`). The layout with the party sidebar keeps the remaster's bands. Tests `P1MapBarTests` (titles, items, boxes and
  the ghosted Look pixel for pixel on the ashots; the line's rows). Shots: `parity/shots/p1-remaster-map-menu-bar.png`,
  `-menu-bar-options.png` (Look ghosted), `-menu-bar-dungeon-options.png`, `p1-remaster-map-message-line.png`,
  `-message-line-dungeon.png`, `-message-line-dungeon-help.png` (a menu's help on the empty line),
  `-message-line-help-above.png` (5× Client windows on Xvfb).
- **The overworld's frame (s156, static `[C]`, built):** the redraw h56 `+0x860` ends with h77 `+0x6dc(1, 12, 317, 183,
  13)` (`+0x8b8`): SetAPen 13, Move(1, 12), one PolyDraw of 13 points, i.e. three nested outlines x 1–318 / 2–317 / 3–316,
  y 12–197 / 13–196 / 14–195 in `#888888` (the ashots' rows 12–14 and x 1–3 / 316–318; the bottom rows 195–197 are below
  the ashot). Pack `map_message.overworld_frame`, `UiOverlays.DrawP1MapFrame` under the line (the remaster's line, which the
  Amiga never prints on the overworld, covers its bottom while it shows). Test `P1MapBarTests.OverworldFrame_MatchesTheAshot`
  (every frame pixel of `p1-amiga-map-out01` but the pointer's tip). The same routine frames the dungeon's screen (h56
  `+0xe7c`: (7, 13, 170, 155, pen 10), with h69 `+0x2af4` blitted at (214, 68)) and the fight (h53 `+0x45c`: (0, 11, 319,
  186, pen 1)). **Built s158:** the dungeon's (pack `map_message.dungeon_frame` (7, 13, 171, 158), 3 px, pen 10
  `#446600`; live `p1-amiga-dungeon-entry-frame`, test `P1MapBarTests.DungeonFrame_MatchesTheLiveShot`; remaster shot
  `p1-remaster-dungeon-frame`). Left `[~]`: the Amiga fills the dungeon map's unseen cells pen 13 grey `#888888` (live),
  the remaster leaves them black; the remaster's compass sits about 10 px left / up of h69 `+0x2af4`'s (214, 68) and adds
  its own "D1 SOUTH (x, y)" lines.

## Tooling

```bash
python3 tools/scripts/extract_amiga_fonts.py
python3 tools/scripts/extract_amiga_graphics.py
python3 tools/scripts/build_amiga_asset_manifest.py
```

Credit: dimension/palette offsets confirmed via [Kroah's Phantasie Sprite
Viewer](http://bringerp.free.fr/RE/Phantasie/spriteViewer.php5) source
(`MonstersManagerP1.cs`, `RacesManagerP1.cs`, `Sprite.cs`, `Palette.cs`).
