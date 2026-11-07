# Message & Scroll Text (encrypted files)

## Files
- `MESS1..MESS10` — per-area event/story text, opened by `OV2.OVR` via the
  `MESS%-d` format string.
- `SCROLLS.DTX` — the collectible **scroll** texts (Filmon the Sage reports,
  history of Gelnor, dungeon rumors), opened via `scrolls.dtx`
  (`PHANTASI.COM` @ `0x641b`).

Both are **obfuscated on disk** (no plain ASCII), unlike the short event
strings embedded in `DNG*` / `OUT*` (already extracted by `extract_maps.py`).

## SCROLLS.DTX — cipher cracked [C]

`tools/scripts/decrypt_scrolls.py` → `extracted/scrolls.json` (all 20 scrolls,
printable_ratio = 1.0). Cross-checked: Scroll 20 is Filmon’s report on
J.R. Trolkin’s secret control room (matches the walkthrough).

### Layout
- File size 16000 = **20 scrolls × 800 bytes**.
- Each scroll = **20 lines × 40 columns**, NUL-padded per line.
- Scroll labels in `PHANTASI.COM` (`SCROLL 1`..`SCROLL 20` @ `0x6b2f`).

### Transform
For scroll index `n = 0..19` (SCROLL 1..20), with `i` = offset within the
800-byte record:

```
step  = 9 + 4*n
plain[i] = ((cipher[i] - (0x0D + step*i)) & 0xFF) & 0x7F
```

Authoritative routine: `PHANTASI.COM` @ `0x1de8` (in-place `sub [bx], al`).
Asm detail: the key is actually `((i & 0x7F) * step + 0x0D) & 0x7F` (the
`i & 0x7F` folds every 128 bytes); the Python form above matches the on-disk
corpus. Key **resets per scroll**.

## MESS1..MESS10 — cipher cracked [C]

`tools/scripts/decrypt_messages.py` → `extracted/messages.json` (all 10
areas). Sample checks: purple-gas cavern text (MESS1), Charista reward
(MESS3), Filmon the Sage (MESS9), Bleeb button puzzle (MESS7).

### Transform
For file number `n = 1..10`, a **running subtract** with an 8-step delta
cycle (full 8-bit keys — **not** the SCROLLS `& 0x7F` scheme):

```
key   = (0xFF - 3*n) & 0xFF
phase = (6 - 3*n) % 8
for i in 0 .. len-1:
    plain[i] = (cipher[i] - key) & 0xFF
    key = (key + 1 + 0x20 * ((phase + i) % 8)) & 0xFF
```

Delta cycle: `[0x01, 0x21, 0x41, 0x61, 0x81, 0xA1, 0xC1, 0xE1]` starting at
`phase`. Derived from long space-padded runs (same ciphertext substrings
shared across files at different offsets) plus the per-file first-byte
pattern `cipher[0] ≈ 0xFF - 3*n` when the file opens on constant plaintext.

`OV2.OVR` holds the `MESS%-d` name at file offset `0x772e`; Ghidra did not
recover xrefs into the read/decrypt loop (overlay analysis gap). The cipher
above is validated by plaintext, not by a labeled disassembly site.

### Layout
After decrypt, each file is:
1. **Slot table** — u16 LE values, either `0` or a multiple of **41**,
   indexing event/script slots onto message records.
2. **Records** — fixed **41 bytes** = 1 prefix byte + **40 columns** of
   text (space-padded). Prefixes seen: `0x28`, `0x50`, `0x01`, `0xFF`
   (likely line-count / type; kept raw in the JSON).

Slot offset `S` addresses file offset `base + S` with
`base = text_start - 41`.

## Plaintext already recovered
Short unencrypted strings in `OUT*.DAT` / `DNG*` → `overworld.json` /
`dungeons.json`. Full scroll corpus → `scrolls.json`. Full area messages →
`messages.json`.

## Menus — cursor memory per menu id [C] (static) + [D✓] s58
The COM menu routine keeps **one selection word per menu id** (DS `0x24 + 16·id`, inside the menu-table entry for id `n`
at DS `0x20 + 16·n`, `dungeon-representation.md` §Leaving): a menu reopens on the last choice made **in that menu**,
and its first-ever highlight is the word's initial value. Initial values that are not the first option (static):
`0x0` title "Do you want:" → "- continue saved game"; `0x1` town bar → Guild; `0x3` town Option → Save game; `0x4` Guild
menu → New member; `0x8` PURGE/KEEP → KEEP; `0xa` SPELLS/EQUIPMENT/SKILLS/POTIONS → SKILLS; `0x13` Speed → normal;
`0x18` PARTY OPTIONS → FIGHT; `0x20` Utility menu → Return. Every other menu starts on its first option.
- **DOSBox s58** (`capture_s58_2026-10-25.md` §4, §5, §7): "[original text]" (`0x1c`) answered NO reopens on NO (`m7`); a NO
  to "SAVE THE DUNGEON MAP?" (`0x1e`) leaves ENTER? on YES (`d9`); s56 saw KEEP IT? (`0x1b`) carry NO to the next KEEP IT?.
  This **supersedes the s43 reading "one YES/NO cursor shared by every YES/NO prompt"** (that capture's NO was given to the
  same menu). The town Option menu reopened on Save game (`po1`); Inspect's page menu opened on SKILLS (§1).
- YES/NO boxes: Left/Right move the cursor (`f6`); Down does nothing (§5). The keys `y`, `n`, `N` do nothing (`b14..b16`).
- **Dungeon event menus (MESS menus, `0x2a95`) do not remember:** DNG7 ev19 reopened on option 1 every time, not on the last
  pick (§5). Up on option 1 wraps to the last option (§7); Down on the last wraps to option 1 `[~]` (assumed symmetric).
- Ids the remaster uses: `0x1b` KEEP IT?, `0x1c` [original text], `0x1d` [original text], `0x1e` SAVE MAP?, `0x16` AN INN.
  ENTER?, `0xf`/`0x10`/`0x11` wilderness, `0x1f` GO BACK ? (DOS also uses `0x1f` for "DISARM IT ?"; the remaster's trap menu is
  the manual's D)ISARM / L)EAVE (D-052) and keeps its own memory `[~]`), `0x15` INSPECT MEMBER?, `0xa` inspect pages, `9`
  Guild move confirm, `0x3` town Option, `0x1` town bar, `0xd` Inn. "A TOWN. ENTER?" has its own label (COM `0x60d0`, between
  the Inn menu's labels and menu `0xf`'s), read as menu `0xe` `[~]` (label order; not captured), so it does not share ENTER?'s
  memory with the dungeon prompt. The Bank's Withdraw / Deposit list sits between the AGREE/NOT and the Inn labels (`0xc` by
  label order `[~]`); it starts on its first option.
- **Remaster:** `SessionPrompt.MenuId` carries the id; the Client's `PromptMenu` keys its memory by it (no shared YES/NO
  slot), event menus always open on option 1, Up/Down are ignored on a YES/NO box; `TownMenu` keeps one cursor per list
  (bar first-ever Guild, Option first-ever Save game). In memory only, not saved. D-066: the remaster keeps Y/N and the
  manual's letter keys on top of the cursor. Not built because the remaster has no cursor menu for them: `0x0` (title),
  `0x4` (Guild letters), `0x8` (the new-member KEEP / PURGE step after the roll — label order: race menu 5, classes 6/7; the remaster asks "Y keep  N roll again" by letter), `0x13` (no Speed menu), `0x20` (no Utility menu).

## Text boxes, key waits and timed lines on the map screens [C] (s154) + [D✓] s154

Addresses: OV1 / OV2 near calls go to the unpacked COM at target − `0xac24` (`RE-REFERENCE.md`); OV2 uses the same
delta as OV1 (both overlays load at one base: `0xb954` / `0xb914` / `0xb9d1` are the same routines from either), and the
live checks below fit it.

| COM routine | Called as (OV) | What it does |
|---|---|---|
| `0x2e5f(s)` | `0xda83` | saves the screen (`0xd30`), fills rows 9..11 × columns 0..39 (`0xdad`, attr `0xf00f`: a white-ruled band), prints `s` centred on row 10, **waits for a key** (`0x37a0`), restores the screen (`0xcf0`) |
| `0x2ead(s1, s2)` | `0xdad1` | the same with rows 9..12, `s1` on row 10, `s2` on row 11, each centred; waits for a key |
| `0x2f1d(s1, s2, s3)` | `0xdb41` | rows 9..13, three centred rows 10..12; waits for a key |
| `0x37a0()` | `0xe3c4` | flushes the keyboard (`0x3933`: INT 16h AH=1 / AH=0 until empty), then INT 16h AH=0: **any key** |
| `0x3bf8(n)` | `0xe81c` | a timed wait: deadline = now + `n` hundredths (`0x3420`: INT 21h AH=2Ch, h·360000 + m·6000 + s·100 + cs); `n ≤ 0` takes the Speed's 20 / 40 / 80 (`[0x3090]` 0 / 1 / other); flushes the keyboard, then polls: **a key ends the wait early** (flushed). A deadline past 8,640,000 (midnight) becomes 0: no wait (not modelled) |

**Every `0xda83` / `0xdad1` / `0xdb41` caller in OV2 (s154 call scan)**, all wait for a key:
- Dungeon (h41's set on the Amiga, the same lines): [original text] (`0x23b8`), "%s %s" = `%s PICKS THE LOCK` /
  `%s FAILS` / `%s SUCCEEDS` (`0x2f86`), [original text] %-2d TO %-2d (`0x2974`), [original text] (`0x2e94`),
  script ops 3 A SPELL IS CAST, 7 / 9 gold, 12 [original text], 14 [original text], 17 MONSTERS
  APPEAR, 1 [original text] (`0x2983`).
- Overworld: [original text] (`0x7153`), %s IS ON WATCH (`0x71f1`), %s IS ON WATCH / [original text] (two rows,
  `0x722f`), [original text] (`0x7252`), %s HEARS MONSTERS (`0x741f`), [original text] (`0x748a`),
  [original text] (`0x74d5`); THE RIVER STYX / [original text] (`0x619a` / `0x61a4`); a location's text pages
  (`0x5fd8` one row, `0x6005` two, `0x6036` three: `overworld-movement.md` Location text); [original text] (inn, two-row
  call `0x6848`).
- Combat (not the map's): [original text], [original text] / SURRENDER / GREET YOU / RUN, MONSTERS
  KILLED, [original text] (two rows), `%s SPOTS AN ITEM` (`0x419d`), [original text] %-2d (`0x48ac`).

So **the encounter lines before a wilderness fight and op 17's MONSTERS APPEAR wait for a key** before the fight's screen
(the "waits before fights"). A wandering or forced dungeon fight prints nothing (`dungeon-representation.md` Fight cells).

**Windows that wait for a key (`0xe3c4`) on the map:**
- `0x2a95` (MESS text with no menu, `0x2bef`): fills rows 4.. × columns 0..39 (`0xb9d1`, attr `0xf000`), the 40-char
  records on rows 5.., a ruled row under them, waits, restores. Callers: an event's message, script op 11 / 19, a
  description cell (every step onto it), a trap's MESS a + 4 (`dungeon-representation.md`).
- Damage reports, each its own window: the trap's "IN THE TRAP:" (`0x31cb`; window columns 6..33, rows 7..16, title at
  (13, 8), member rows from (7, 10)), ops 1–4 "REPORT:" (`0x26ca`; columns 6..34, rows 7..16, title at (16, 8)),
  "SWIMMING:" (`0x6956`; columns 1..38, rows 7..16, title at (14, 8)), the magical pool's `%-10.9sIS CHANGED` rows (`0x66eb`) and
  the wilderness inn's "IN THE INN:" (`0x6835`).
- Row 24 lines with **no** wait: message cells and wandering text (`0x23c0` → `0x2d03`), the teleport's blank (ops
  12/13). They stay on row 24 under the next box.

**[D✓] s154 DOSBox-X** (Xvfb, sound off; s58 pre-fight set with DNG9 (15,1) patched to `0xDA`): bumping (14,0) shows the
full-width band "[original text]" on rows 9..11 and it is still up 5 s later; the key closes it. Stepping onto `0xDA`
shows "[original text] 1  TO 2" in the same band, still up 5 s later. This **supersedes s56's "stairs: no key
needed"** (that run's next key closed the band unseen). Shots `reverse-engineering/dos/working/s154-shots/`.

**Remaster (s154, AMIGA SCREENS off):** `P1DosMapPages` (Client) pages one step's lines: every line of the keys above
(data: `GameData/phantasie1/dos-screens.json` `map_boxes`), every Core `MapWindow` (Text, Report, Box) is its own page in
the remaster's text panel and waits for a key (or a click, or a pad button); other lines stay on the message line.
A fight Core started waits for the last page. **s156:** a step whose lines are all paged keeps the message line from
before it, as DOS's row 24 (`P1DosMapPages.MessageLine`).

**Places (s156, static `[C]` + DOSBox-X):** on DOS's 40 × 25 grid of 8 × 8 cells:
- Band (`0x2e5f` / `0x2ead` / `0x2f1d`): rows 9..10 + n, full width, black; a 2-px white rule in row 9's pixel lines 1–2
  and in the last row's lines 5–6; the n rows centred as stored (`(40 − length) / 2`, a sign's padding counted) on rows
  10.. (live s154: rules at y 73–74 / 93–94, [original text] from x 96; s45's Red Pony sign, two rows: rules at y 73–74
  / 101–102, its padded 39-character rows from columns 2 and 9; a three-row band inferred `[~]`).
- MESS text window (`0x2a95`): the same from row 4; the 40-character records from column 0 on rows 5.. (live s156, DNG9's
  gate text "[original text]…": rules at y 33–34 and 69–70; shot
  `reverse-engineering/dos/working/s156-shots/mess-window-dng9-gate.png`).
- Report windows: OV2 `0xb9d1(column, row, last column, last row)`, black, a 2-px white frame inset in the edge cells the
  same way (live s43 swim, s45 inn): the trap (`0x3097`, title `0x30a9`, rows `0x3125`…) columns 6..33, title (13, 8), rows from (7, 10); ops 1–4
  (`0x25d1`) 6..34, (16, 8), (7, 10); swimming (`0x6956`) 1..38, (14, 8), (2, 10); the magical pool (`0x658d`) 8..29,
  "A MAGICAL POOL" at (12, 8), rows from (9, 10); the wilderness inn (`0x6749`) 5..32, "IN THE INN:" at (13, 8), rows from
  (6, 10); every one rows 7..16.

**Remaster (s156):** `dos-screens.json` `pages` holds these places; `P1DosPageLayout` puts a page on them and
`UiOverlays.DrawP1DosPage` draws it, each character in its own 8-px cell in the remaster's font (DOS's CGA font is not
drawn), white on black. A page DOS's grid can't hold whole (a row past 40 columns, a window past row 24, more member rows
than the frame, an unknown report title) goes to the remaster's scrolling panel, so nothing is cut. The pool's and the
wilderness inn's reports are pages too (the inn's after "AN INN. ENTER?" YES). Shots `parity/shots/p1-remaster-dos-band-wall.png`,
`-dos-report-window.png`, `-dos-mess-window.png`. A page logged before a Core move (op 12's band, a jammed lock's) shows
the party on the cell it stood on until its key, as DOS draws the band before the move (s58 live: WAVE WAND's "THE PARTY
IS MOVED" over the unmoved map) (`P1DosMapPages.Page.PartyAt`, from Core's `DungeonMovesSince`, as the Amiga run's).

## Remaster note
Store decrypted texts as plain JSON keyed by scroll id / area message id in
`OpenPhantasie.Data`; no reason to keep the original cipher at runtime.
