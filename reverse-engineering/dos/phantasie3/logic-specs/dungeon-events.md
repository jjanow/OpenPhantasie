# Phantasie III — dungeon event squares, event programs, quest bits, Divine Aid, exits

Phase 09 T4 3c.6 / 3c.7 (s82, static; the lines marked **live** were run in DOSBox-X, `../screens-s82/`). Source of truth: the III manual. It does not describe
dungeon events (only "Divine Spells (#57 +): these spells may only be acquired during the course of the game for special
purposes", III PDF 11 / pr.16), so nothing here is a manual conflict. Code: DOS `MAIN.EXE` overlay 4 (addresses
`../FORMAT.md` §1); data: `DNGS.DAT` (`../FORMAT.md` §9.5). Tags: `[C]` code / data bytes, `[~]` probable, `[?]` not
decoded. Decoder used for the tables below: a throw-away script that follows every choice, goto and created square
(it is not in the repo; the rules below are enough to rewrite it).

## 1. Which record is which dungeon `[C]`

The dungeon entry (ovl4 `0x62d`) does **not** load record `dungeon − 1`. It takes a letter from the word table
`DS:0x1c84 + 2·dungeon` into `FAR:0xb5b0` and reads `DNGS.DAT` record `letter − 'A'` (ovl4 `0x77d…0x794`); the same
letter names the save files `DNP<letter>` / `DNG<letter>` (Amiga `D/DNPA…DNPK`, each within 0–66 bytes of the matching
DOS record, i.e. played copies):

| Dungeon (place, master string 33 + n) | Letter | Record | Where | Content (§5) |
|---|---|---|---|---|
| 1 "A very small straw hut" | J | 9 | Scandor (46, 9) | soup man, four dragon hoards |
| 2 "Many men guard a tent here" | H | 7 | Scandor (25, 8) | Lord Wood, weapons |
| 3 "A large crystal building" | E | 4 | Scandor (48, 36) | Chambers of Chronos, button machine, exits to Light / Dark |
| 4 "A large cavern starts here" | D | 3 | Scandor (36, 38) | Gnome catacombs (dwarves / gnomes only), Divine Aid wizard |
| 5 "The Pendragon town archives" | A | 0 | Scandor (25, 39) | Filmon (quest guide), altar, wand pedestal |
| 6 "The Dwarven burial grounds" | C | 2 | Scandor (2, 35) | Kilmor's funeral (Nikademus appears) |
| 7 "A giant wooden building" | B | 1 | Scandor (28, 67) | Hall of Giants: Kilmor's body, Guaggle Beast, six buttons |
| 8 "A cave smelling of sulphur" | K | 10 | Scandor (7, 66) | byte-identical to record 9 |
| 9 "A dark fortress" | I | 8 | Netherworld (6, 10) | Nikademus and Lord Wood, the final fights |
| 10 "The Castle of Light" | F | 5 | Light (6, 7) | crystal gate (Light Key), statues, Gem of Light, Light Fairy |
| 11 "The Castle of Dark" | G | 6 | Dark (2, 7) | black gate (Dark Key or evil deeds), Nikademus statue / wand, exit to the Netherworld |

Records 9 and 10 are byte-identical, so dungeons 1 and 8 have the same map and events (their saves are separate).
**Live s82:** leaving the Pendragon archives (dungeon 5) with "save this map" wrote `DNPA` / `DNGA`, and `DNPA` equals
record 0 except the two event ops cleared by bit 6 (`../screens-s82/s82_archives_exit_save_map.png`).

**Entry (s95 static, ovl4 `0x14444`):** before anything else the entry asks "[original text]" (DS
`0x62ec`); Yes reads `DNP<letter>` (`0x1080` B) into the work record, No or a missing file reads `DNGS.DAT` record
`letter − 'A'`. The days-out counter `FAR:0x9e2` is read only by root `0x1c62` (status screen) and ovl3 `0x676` (town
aging), never in overlay 4, so a saved map comes back as saved `[C]`. The entry's `rand() & 7` → `DS:0x1c9c` (ovl4
`0x649`) is the **copy-protection quiz's question index** `[C]` s97: its only other users are the quiz at ovl4 `0x43e`
(`0x473…0x484`: take the index, add 1, wrap after 9; the quiz runs when `rand() & 7 > 5`), so a dungeon starts the quiz
at a random question 0–7. The remaster leaves the quiz out, so nothing to build. Leaving by an exit asks "Would you
like to save this map?" (DS `0x6326`). The question (s97 static, ovl4 `0x6f5…0x7c2`) is asked on **every** entry, with
no file test first: Yes tries `DNP<letter>` (and its `DNG<letter>` companion, `0x5a` B to `FAR:0`), and a failed read
falls through to `DNGS.DAT`; No reads `DNGS.DAT`. Manual vs code: **D3-038** (DECIDED s96). **Built (s95):** one saved
map per dungeon (`rules.json` `dungeon_save_per_dungeon`; saves keep them all), loaded without the question, I's D-027
rule on a NO. **s97:** under `p3.dungeon-map-no` = `dos` the entry asks the question after "Will you enter?"
(`OverworldEntryPrompt.AsksLoad`, saved): Yes loads the saved map when there is one, No the original; neither touches
the saved map.

## 2. Event squares `[C]` (ovl4 `0x1299`)

Map cells `0xdd…0xff` are event squares; entry `e = cell − 0xdd` is 5 bytes at record `0x2e4 + 5·e`:

```
on entering cell c >= 0xdd (after the copy-protection quiz, ovl4 0x43e, passes):
    E = record[0x2e4 + 5*(c - 0xdd) ...]
    if E[0] == 0xff: nothing
    if E[0] & 0x80: E[0] = 0xff                  # once only (written into the work record, so a saved map keeps it)
    show text (E[0] & 0x7f) - 1                  # texts: §2.1; a text shorter than 3 bytes is not shown
    n = E[1]
    if n <= 1: k = 1
    else: wait for a key '1'..chr('0'+n); k = key - '0'
    run program E[2 + k - 1]                     # n may exceed 3: keys 4..9 read into the next entry's bytes
```

The party **does** step onto the event square (dungeon_step commits the move with `0x1b9e(new x, y)` at ovl4 `0xb06`
before the cell dispatch at `0xc0b`; the square it came from is kept in `FAR:0xb61c` / `0xb624`, ovl4 `0xa6b`). **Only
op 19's jump puts it back**: the jump branch (ovl4 `0x1856…0x1883`) calls `0x1b9e(FAR:0xb61c, 0xb624)`, so every
"if nobody has … goto" sends the party back to the square it came from (the archive door, the gates, Lord Wood's
room, Filmon's speech) `[C]`. **Live s82:** the first prayer at the altar took op 19's jump (no torso hurt) and left the
party on the entrance (11, 0); the second prayer took no op-19 jump and left it on the altar (11, 1), from where Up
reached the exit. The copy-protection quiz comes up on
about 2 event steps in 8 (`rand() & 7 > 5`, ovl4 `0x455`): three choices, answer table `DS:0x1c62`
(`../screens-s82/s82_quiz_example.png`).

`n` up to 9 occurs (the archive's "Push which?" 1–9, the fortress's "Cast which spell?" 1–9, Hall of Giants' 6 buttons
+ leave = 7): choice k reads byte `2 + k − 1`, which runs on into the following 5-byte entries. Entries that no map
cell holds are reached only by program ops (§3: "run square", "set cell").

### 2.1 Texts `[C]`

Text n: big-endian pointer at record `0x593 + 2·n`, text at `pointer − 0x800` within the record, NUL-terminated
(ovl4 `0x1bf9`). Line breaks are byte `0xf0` in some texts (menus "ð1) …"). Shown by ovl4 `0x1c4d` in the message
window; with choices the text stays up for the key, otherwise "Press a key".

**Built (s108):** the wait stops the program (a square's text before its program, op 11 before the next op) until the
key; the texts of one step pile up on one page, drawn on the Amiga parchment (Amiga `0x1d982`, FORMAT §2.2; Core
`DungeonTextPage`, prompt `SessionPromptKind.DungeonPage`). A menu's text and choices go on the page and wait for a
number. A step or an event fight starts a new page.

### 2.2 Yes / No questions: the two-item menu root `0x75a3` `[C]` (s108 static)

`0x75a3(text1, col1, row1, text2, col2, row2, lit, …)` draws two words and lights word `lit` (`[bp+0x16]`, 1 or 2) on
**every call** (no memory). Left / Right move the bar; it returns 1 for `Y`, or Return on word 1; 0 for `N`, **Esc**
(`0x1b`), or Return on word 2. Root `0x1820(text, lit)` prints a text and, with `lit` ≠ 0, asks "Yes" (col 12) / "No"
(col 25) on row 14 through it; ovl4 `0x1b81` is a near wrapper of `0x1820`. Every caller (far calls to `0x6f6:0x643`,
`0:0x1820`, near calls to ovl4 `0x1b81`):

| Caller | Question | Lit | Remaster |
|---|---|---|---|
| ovl4 `0x704` | "[original text]" | 2 No | `OverworldEntry` (`AsksLoad`) |
| ovl4 `0x892`, `0xb73` | "[original text]" | 2 No | `DungeonExitTakeIt` |
| ovl4 `0x8cf` | "[original text]" | 2 No | `DungeonExitSaveMap` |
| ovl4 `0x101a` | "<name> [original text]" (door) | 2 No | `DungeonEvent` (III Yes / No) |
| ovl4 `0x1179` | "<name> [original text]" | 1 Yes | `DungeonEvent` (III Yes / No) |
| ovl4 `0x1b59` (op 6, op 12 `[~]`) | "Keep <item>?" | 1 Yes | `Treasure` (built s102) |
| root `0xf3f2` | "<name> finds <item> / [original text]" | 1 Yes | `Treasure` (built s98 / s102) |
| root `0xe359` (`0xe294`) | "<name> [original text] [original text]" / "<name> [original text] [original text]" | 1 Yes | `WildernessEncounter`: III Yes / No (Yes = attack / fight, No = leave / run) |
| ovl3 `0x830` (`0x6c8`) | "<place> Will you enter?" (town, dungeon, **inn**: place kinds 1 / 2 / 3) | 1 Yes | `OverworldEntry`; the inn: `WildernessInn` with the inn's name |
| ovl2 `0x3b75` (`0x3a0a`) | "It will take this character two months to make the journey here." | 1 Yes | Guild Add move gate (`GuildAddDialog.MoveConfirmPromptFor`) |
| ovl2 `0x14c4` | "Do you wish to train, now?" | 1 Yes | — (no such question in the remaster's Guild) |
| ovl2 `0x423d` (`0x4192`) | "Really leave <name>?" | 1 Yes | — |
| ovl2 `0x2883` (creation) | "Keep character?" | 2 No | — (the remaster's creation flow is I's) |
| root `0x83c1…0x8465` (options) | "[original text]" (`DS:0x58e2`, after the empty-party / defeat refusals) / "…load an old game…" (S447) / "…save this game…" (S390) / "…quit?" (S389) | 2 No | s160: the first three (`P3ConfirmQuestion`, `Game1.P3Confirm.cs`) before the town's Distribute (every route but the town-entry hand-out), Save (S, Option › Save game, the bar's File › Save, the game menu / F5 in a town or the Guild) and Load (the bar's File › Load, the game menu / F9 in a town or the Guild); Quit has no remaster moment (the game menu's Main menu) |
| root `0xb075` | "Are you sure you want to purge this character permanently?" (S407, after the elemental / party-member refusals) | 2 No | s160: after a Purge pick that Core would not refuse (`GameSession.PurgeRefusal`, `GuildPickDialog.PurgeAsked`) |
| root `0x87ba`, `0x8aa0` | "See more info?" (inspect) | 1 Yes | — |
| root `0x10d58` | "[original text]" One / Two (not Yes / No: Esc gives "Two") | 1 One | AMIGA SCREENS off (s156): the combat order step "NAME: [original text]  > 1 One    2 Two" (`CombatPresenter.HandleDosRankKey`: One lit, arrows move the light, Return takes it, Y / 1 = One, N / 2 / Esc = Two; Backspace walks the list again, the remaster's); on: the Amiga's window 20 |
| ovl1 `0x380`, `0x98c`, `0x9b8` | transfer questions, "Are you playing off the hard drive?" | 2 No | — (T5 / not modelled) |

**Built (s108):** every row with a remaster prompt is a `SessionPrompt` with `Remembers` = false, `InitialChoice` = the
lit word and `EscapeChoice` = 2 (No) in III; I / II keep their own menus. The III card (AU PDF 18) says "Esc exits most
menus", which agrees. **s160 (user s159):** the town's four "Are you sure …?" rows (root `0x83c1` / `0x83f1` / `0x8440`
and `0xb075`, each `push 2` before `lcall 0:0x1820`, so "No" lit) are asked under every III layout through one Client
question (`P3ConfirmQuestion`): with AMIGA SCREENS on and a pack with window 1 and the OK / CANCEL art, the Amiga's window 1
(`0xaae2`, Return = OK, D3-068's split); otherwise this table's prompt, "No" lit, Return = No, Y / N, Esc = No. Sound 36
before each (`GameSession.AskConfirmQuestion`).

## 3. Event programs `[C]` (ovl4 `0x14b0`, jump table ovl4 `0x1aea`)

A program index `p` addresses 2-byte units at record `0x393 + 2·p`. Each op is an op byte plus arguments a1 (and a2, a3
for 4-byte ops). Op byte: bit 7 = **continue** with the next op, bit 6 = **clear** (the op byte is set to 0 after it
runs, so the step happens once per saved map), bits 0–5 = type. Type 0 or > 27 ends the program; types 16 and 17 end it
even with bit 7 set. Goto ops set "continue" themselves.

Fields: ops 18, 19, 24, 25, 27 address a word of each **party** record at `+8 + 2·f`, with `f = a` (the field byte), or
`a − 61` when `a > 120` (so 0x7a → `+0x82`, 0x7e → `+0x8a`, 0x7f → `+0x8c`). Special values: `117` = Divine Aid,
`14` / `15` (set) = take party gold, `52` (set) = take 12,000 XP. "Living" = status ≠ Dead (`+0x2c ≠ 0`).

| Type | Len | Effect |
|---|---|---|
| 1, 2, 3, 4 | 2 | **trap damage**: every member with HP > 0 takes `rnd(a1)` (1…a1); HP ≤ damage → HP 0, status Dead; "Damage Report" box (root `0x2426(a1, 1, 2)` / `0x2623`); a wiped party goes to the Astral Plane (`planes.md` §6). The four types are identical |
| 5 | 2 | **fight** monster a1 (ovl4 `0x1d30(a1, 1, 0)`), a normal group (`encounters.md`); if the party wins, the party's square becomes cell `0x8a` |
| 6 | 2 | **item** a1: "Keep <item name>?" Yes / No; Yes → found-item list (root `0xf421`, cap 60, D3-020). Live s82: "Keep Scroll II?", and with 60 found items "The party can't carry any more items." **Menu (s102, static)** `[C]`: the handler (ovl4 `0x156e`) builds the text, sets the Yes / No flag (`[bp−0x14]` = 1) and jumps to the shared tail `0x1b20`, which calls root `0x1820(text, [bp−2])`; `[bp−2]` is 1 from the interpreter's entry (`0x14bc`) and is zeroed only for the plain-text flag 2, so root `0x1820` passes highlight 1 to the two-item menu root `0x75a3` ("Yes" col 12 / "No" col 25, row 14): "Yes" lit on every call, no memory; `Y` / Return on "Yes" keep, `N` / Esc / Return on "No" leave it (`encounters.md` §4). A non-zero answer calls root `0xf421` (add to the found items) |
| 7 | 2 | **gold** + a1 · 256, "[original text]" (press a key). The amount is `a1 << 8` **sign-extended** to 32 bits, so a1 ≥ 128 would be negative (the data's gold ops all use a1 < 128, except type 9 below) |
| 8 | 2 | gold + a1 · 256, no message |
| 9 | 2 | **gold − a1 · 256** (to 0 if less), "[original text]" With a1 ≥ 128 the sign-extended amount is negative: the wizard's a1 = 255 is −256, so it **adds** 256 gold. **Live s82:** 50,000 gold → 0 (op 18) → **256** after this op |
| 10 | 2 | gold − a1 · 256, no message |
| 11 | 2 | **text** a1 − 1 |
| 12 | 4 | **teleport** within the dungeon to (a1, a2), "[original text]" The message call passes the Yes / No flag, so a Yes / No menu follows the text `[~]` (answer ignored) |
| 13 | 4 | teleport to (a1, a2), no message |
| 14 | 4 | **set map cell** (a1, a2) := a3, "[original text]" (ovl4 `0x2248`); a3 ≥ 0xdd creates an event square, 0xd7–0xd9 an exit, 0x8x a fight / plain floor |
| 15 | 4 | set map cell, no message |
| 16 | 2 | trap damage `rnd(a1)` (report variant `0xc`), then **stop** |
| 17 | 2 | **ambush fight** monster a1: the monsters get a free round before the party chooses (combat_main argument 1, root `0x1072f`); no cell change; then stop |
| 18 | 4 | **set field**: for each living member: f = 117 → learn Divine Aid (root `0xabbe(member, 57)` writes 57 to `+0xf0`); f = 14 or 15 → party gold −= min(gold, 65,280) (once per living member); f = 52 → if XP > 12,000: XP −= 12,000; else word `+8 + 2f` := a2 |
| 19 | 4 | **test field**: if **no** party member has word `+8 + 2f` ≥ a2 (signed), goto a3 **and put the party back on the square it came from** (§2); else go on. a1 = 117: "if no member has Divine Aid (`+0xf0 ≠ 0`), goto a3" |
| 20 | 2 | **goto** a1 |
| 21 | 4 | **chance**: if `rand() % 100 < a1`, goto a2 |
| 22 | 2 | **run event square** entry a1 − 1 (cell `0xdc + a1`): its text, choices and program, as in §2 |
| 23 | 4 | **has item**: if a1 is in the found-item list or among any member's 9 carried items, goto a2 |
| 24 | 4 | **set bit** a1 − 1 of field a2 in **every** party record (dead too): a3 ≠ 0 → OR; a3 = 0 → AND with the bit itself (keeps only that bit; no data uses it) |
| 25 | 4 | **test bit**: if **any** member has bit a1 − 1 of field a2, goto a3 (a2 = 117: always goto). Note the opposite polarity to type 19 |
| 26 | 2 | if any member is a **Dwarf or Gnome** (race 1 / 3), goto a1 |
| 27 | 2 | **raise field**: each living member's word `+8 + 2·a1` +1 if below 30 (the soups: STR, INT, DEX, CON, CHA) |

After the program the party's square is redrawn. A message index other than the five above is never used ("Smoke
fills the air.", index 2, has no op).

## 4. Quest bits `+0x8a` and deed bits `+0x8c` (all writers) `[C]`

No code writes either word: **every writer is a DNGS type-24 op** (OR, all party members, dead or alive). The bits are
read by the score (`+0x8a`, `endgame.md` §2), the inventory (bit 2, the cursed wand) and the event tests.

| `+0x8a` bit (score weight) | Set by | Dungeon |
|---|---|---|
| 0 (0) | finding Kilmor's body (Scroll I, "written in blood") | 7 wooden building (Hall of Giants) |
| 1 (4) | attending Kilmor's funeral (needs bit 0 to open the door) | 6 burial grounds |
| 2 (8) | taking the Wand of Nikademus (cursed; "you cannot get rid of it") — from the archive pedestal, or from the black statue in the Castle of Dark | 5 archives / 11 Castle of Dark |
| 3 (12) | the gnome chief's reward for the viper's egg (Old Bow, item 58) | 4 cavern |
| 4 (17) | taking the Gnome Bow (item 53) | 4 cavern |
| 5 (20) | meeting Lord Wood (Scrolls V and XIV) | 2 tent |
| 6 (25) | opening the Castle of Light gate with the Light Key | 10 Castle of Light |
| 7 (30) | entering the Castle of Darkness (Dark Key, or "Enter evil ones") | 11 Castle of Dark |

`+0x8c` holds once-only flags and **evil deeds**; the tests compare the whole word as a number (≥ 1, ≥ 8, ≥ 56):

| `+0x8c` bit | Set by |
|---|---|
| 0 | wrestling the Guaggle Beast (also: magic current := 0, CON +1 unless already set) — wooden building |
| 1, 2 | first / second soup drunk — dungeons 1 / 8 |
| 3, 4, 5 | breaking the crystalline statue of a man / an elf / a dwarf — Castle of Light |
| 6 | taking the Gem of Light — Castle of Light |
| 7 | attacking the Light Fairy — Castle of Light |

Uses: ≥ 56 (e.g. all three statues, or the Gem, or the Fairy) = "evil": the archive pedestal then gives 25,600 gold
instead of the wand; the Castle of Dark opens to a knock ("Enter evil ones"); ≥ 1 / ≥ 8 pick Nikademus's speech and
whether he fights at once (§5, dungeon 9). The Amiga save's 7 and 71 (`../FORMAT.md` §5) fit: soups + Guaggle, + Gem.

## 5. The story chain (what the programs do), by dungeon

- **5 archives:** **Filmon** reads the party: "who defeated the Black Lord of Gelnor" if any member's Phantasie I score
  `+0x5c` ≥ 100 ("who could not …" if 1–99), "who destroyed the orb of Ferronrah" if any `+0x18` (Phantasie II score) ≥
  200 ("could not destroy the orb" if 1–199); then the next step from the quest bits (rescue Kilmor in the Hall of
  Giants → the burial grounds → Chronos to the east → the Castle of Light / Darkness → "meet Lord Wood in the Fortress of
  Nikademus, in the Netherworld"). **Pedestal**: non-evil parties get the wand (bit 2), evil ones 25,600 gold. **Altar**:
  §6. Items: Scroll II, Scroll III, a weapons case.
- **7 wooden building (Hall of Giants):** six buttons open cells; Kilmor's body (bit 0); the giant's Guaggle Beast
  wrestle (`+0x8c` bit 0, CON +1, magic emptied, fight 72); chests (burn = Burnt Gems, open = 60 % safe); Dark Key.
- **6 burial grounds:** the door opens with bit 0; the funeral (two trap hits rnd(13) / rnd(8) from Nikademus), then bit 1.
- **4 cavern (Gnome catacombs):** only parties with a Dwarf or Gnome enter. Gnome Bow (bit 4); the viper's egg with the Foul
  Bottle → Old Bow (bit 3); **the wizard sells Divine Aid** (§6).
- **3 crystal building (Chronos):** Light Key; the button machine (letter, digit, sign) sets the lever's square to one of
  four lever events: "A7+" / "B7−" (and A or B with 7 and either sign) → exit cell 0xd7 (back to Scandor); "C2+" → 0xd8
  (Light plane); "C2−" → 0xd9 (Dark plane); anything else → a lever that hurts (rnd(8)). The wall notes give the codes.
- **10 Castle of Light:** Light Key gate (bit 6), statues / Gem / Fairy (deeds), the Fairy's hint ("the Castle of
  Darkness … through the Plane of Darkness … do just the opposite").
- **11 Castle of Dark:** Dark Key or evil deeds (bit 7); the black statue's wand (bit 2); in the centre room, with the
  wand: "break the wand" teleports the party to (3, 1), beside the exit cell 0xd8 at (3, 0), whose destination 41 is the
  **Netherworld**; "turn around 3 times & wave the wand" = an explosion rnd(100) and a teleport.
- **2 tent:** Lord Wood is there only if **no** member has score `+0x82` ≥ 200 (a Darklord / Superhero finds "no one");
  he gives Scrolls V and XIV, sets bit 5 and says [original text] Gnome Catacombs".
- **9 dark fortress:** with no member at score ≥ 200 Nikademus appears (else "a charred body"). Fight: with deeds < 8 he
  fights at once (monster 77); otherwise he summons Lord Wood and the party chooses Fight Lord Wood (79) / Fight
  Nikademus. "Cast which spell?": only #57 works, and only if a member has Divine Aid; then Lord Wood and Nikademus duel
  and the party chooses Lord Wood (79) or Nikademus (78). Outcomes: `endgame.md`.
- **1 straw hut / 8 sulphur cave:** the old man's soups (two bowls, each +1 to one of STR / INT / DEX / CON / CHA, max 30,
  `+0x8c` bits 1 / 2; after both "I am Nikademus…"), four dragon hoards (1,024 – 20,480 gold + items).

## 6. Divine Aid (#57) `[C]`

- **Record:** `+0xf0` holds 57 once learned (root `0xabbe` writes it apart from the 32-slot list). Its only readers are
  the Inspect spell list (root `0x8b93`, prints "Divine Aid" after the list) and event op 19 with a1 = 117. It is **not
  castable**: the cast menus list only `+0xb0…`; no code reads `+0xf0` there (all five modules and the three record
  bases checked).
- **How it is learned in DOS:** only from the **wizard in the Gnome catacombs** (dungeon 4): "Talk" → if no member knows
  it: "For all your gold and some [original text] powerful spell" (Fight / Okay / Leave). Okay: every
  living member learns #57; the party gold is taken (min(gold, 65,280) for each living member and both fields 14 / 15, i.e. all of it below
  65,280; the closing op 9 with a1 = 255 is sign-extended to −256 and so **gives 256 gold back**, §3); each
  living member with more than 12,000 XP loses 12,000. If someone knows it: "I see you have the powerful ancient spell
  of summoning". Fight: monster 70 (Wizard), the square becomes 0x90.
- **What it does:** in the dark fortress, "Cast which spell?" choice 8 (#57) with a member who knows it brings Lord Wood
  through a black gate to fight Nikademus psychically (§5). That is its only effect.
- **Live s82** (`../screens-s82/s82_wizard_*.png`): party of six, 50,000 gold, fighto XP 20,000, crusho 5,000: after
  Okay every member had `+0xf0` = 57, fighto XP 8,000, crusho 5,000 (unchanged), party gold **256** (op 9's sign, §3).
- **The archive altar (dungeon 5), "Pray on it":** if any member's `+0x18` ≥ 200 (Phantasie II score) and **no**
  member's word `+0xf8` ≥ 1, it shows "[original text] the spell of Divine Aid…" and runs set field
  f = 120 := 1, which writes `+0xf8` = **the Torso area := Injured** for every living member; it never calls the
  spell-learning routine. Otherwise (no II hero, or any torso already hurt) it gives 1,024 gold. The field byte looks
  like an off-by-four for `+0xf0` (f = 116): a **DOS data fault**, `[C]` static; DOS's own transfer never writes `+0x18`
  (`../FORMAT.md` §6), so the branch needs an edited record. **Verified live s82** (fighto's `+0x18` set to 200, all
  areas Okay): the text appeared, every member's **Torso became Injured** (`+0xf8` = 1) and `+0xf0` stayed 0 (no spell);
  praying again (torsos now hurt) gave "[original text]" (+1,024), which also confirms op 19's polarity
  (`../screens-s82/s82_altar_*.png`).
- **Transfer:** D3-002 (decided: I's DIVINE I → Divine Aid). Lord Wood's "If you don't have spell 57" and the fortress
  menu are the only places the game asks for it.

## 7. Exits `[C]`

Exit cells 0xd7 / 0xd8 / 0xd9 use exit entries 0 / 1 / 2 at record `0x2db + 3·(cell − 0xd7)`. "An exit. Will you take
it?" → (ovl4 `0x95b`) scores are recomputed, then only **byte 0** of the entry is read (ovl4 `0x977`):

```
dest = record[0x2db + 3*(cell - 0xd7)]
if dest == 29: plane = 1 (Light)
if dest == 31: plane = 2 (Dark)
if dest == 41: plane = 4 (Netherworld)    # (if the loaded-plane marker is 12, Castle of Dark, it is reset first)
load_plane_map()                           # root 0x125d
```

- Bytes 1 and 2 of each entry (the x, y that look like a target square) are **never read** (no access to record
  `0x2dc` / `0x2dd` in the root or any overlay). Arrival on another plane uses the hard-coded start square: Light /
  Dark (4, 1), Netherworld (7, 3) (`planes.md` §2). Entering a dungeon from a plane sets the loaded-plane marker
  `FAR:0x9e6` to plane + 10, so an exit to the **same** plane reloads the map but keeps the position (the dungeon's own
  square).
- **Every other destination value** (3, 5, 11, 13, 14, 15, 21, 23 in the data) leaves the plane unchanged: the party
  is back on the dungeon's square. So **all Scandor dungeons exit to their own entrance square**; the destination
  numbers only matter as 29 / 31 / 41.
- **Entrance square:** (`DS:0x1bb6 + 2·letter`, 0), i.e. x 11, 11, 12, 5, 11, 11, 11, 12, 11, 13, 13 for letters A–K,
  except dungeon 1, which starts at (5, 0) (`FAR:0xe59a`, ovl4 `0x84e`). So dungeons 1 and 8 share one map but enter at
  opposite exits: dungeon 1 on the 0xd7 cell (destination 5), dungeon 8 on the 0xd8 cell (destination 21) `[C]` static.
  The party starts on the exit cell, so the first thing asked is "[original text]" (live s82).
- Exit data by dungeon: 1 / 8 (5 →, 21 →) own square; 2 (3) own; 3 crystal building 0xd7 (15) own, 0xd8 (29) Light,
  0xd9 (31) Dark — 0xd8 / 0xd9 appear only when the lever sets them; 4 (14) own; 5 (13) own; 6 (11) own; 7 (23) own;
  9 fortress (41) stays in the Netherworld; 10 Castle of Light (29) stays on Light; 11 Castle of Dark 0xd7 (31) stays on
  Dark, 0xd8 (41) **Netherworld**.
- **Live s82:** with the crystal building's entrance cell patched to 0xd8 (entry 1, (29, 4, 1)) the party arrived on
  the Light plane at (4, 1); patched to 0xd9 (entry 2, (31, **5**, 1)) it arrived on the Dark plane at **(4, 1)**, not
  (5, 1): the x, y bytes are ignored (`../screens-s82/s82_exit_*.png`). Leaving the archives through its normal exit
  (destination 13) put the party back on (25, 39), the archives' own square.
- Amiga: whether its code reads the x, y bytes `[?]` (not checked).

## 7a. The dungeon step and its cell codes (s83, static) `[C]`

Ovl4 `0xe14` (`dungeon_step(x, y)`) after the move (x 0–23, y 0–29): `rand() % 100` is drawn on **every** step. Then by
the cell entered:

| Cell | What happens |
|---|---|
| 0x80–0x89 | forced fight `0x1d30(record[0x2d1 + cell − 0x80], 1, 0)` |
| 8–17 | when the dungeon chance `FAR:0xdd8c` > that roll: fight `12L + rand() % 3 + cell − 8`, clear flag 0; afterwards the chance −1 while above 1. The chance is 10 on entry (ovl4 `0x679`) |
| 2–6 | trap: the first member (not Dead / Out) passing Find Trap spots it: "<name> [original text]" — Yes: "[original text]" (the member picker `0x18bd(92)`, ovl4 `0x1189`; s163 static: it asks again while the pick is Dead / Out, and its cancel (−1) does as No) — the chosen member rolls Disarm: success "<name> [original text]" and the cell becomes 7, the party back where it came from (step result −1 at `0x1265`, as No; s163); failure springs it; No: nothing changes (step result 0). Not spotted, or a failed disarm: "A trap!!!", every member with HP > 0 takes `rnd(5t − 25)` (12 when that is below 0 or above 25; t = 5 gives a die of 0, `[~]` what root `0x2426` does with it), the cell becomes 7 |
| 0xba–0xc5, 0xc9–0xd4 | a door with monsters behind: the first member passing Listen: "<name> [original text]" — Yes: fight mode 2 (the party surprises); No: back (result 1). Nobody hears: fight mode 1 (the monsters surprise). Monster `(cell ≥ 0xc8 ? cell − 15 : cell) − 0xba + 12L`, clear flag = the cell |
| 0xc6, 0xd5 | locked door: the first member passing Pick Lock: "<name> picks the lock." and the cell becomes `cell − 13` (a plain door 0xb9 / 0xc8); nobody: result 4 (no entry) |
| 0xc7, 0xd6 | special door: result 4, `FAR:0xde58` = 0x2a `[~]` (the manual's locks that "cannot be picked") |
| 0xb9, 0xc8, 7 | doors: result 7 (drawn as a door) |
| 0xd7–0xd9 | exit (`FAR:0xb626` = the cell; "[original text]", §7) |
| ≥ 0xdd | event square (§2) |

The fight routine ovl4 `0x1d30(id, clear, mode)`: `id & 0xff` above 79 → 11, 61 → 60; `make_encounter(id, 1)`;
`combat_main(mode)`; on any outcome but 0 with `clear` ≠ 0 the party's square becomes 0x8a, or 0xb9 / 0xc8 for a door
cell (`clear` > 100); a wiped party goes to the Astral Plane; outcomes 1–3 pay the rewards and 2 / 3 run the reward
scene. **Walls** `[C]` (s88): cell 0 is a wall. The step code tests it (ovl4 `0xf4f`): cell 0 with `FAR:0xdd96` clear →
result 2 and `FAR:0xde58` = 0x0c (the bump feedback, shown by ovl4 `0xd36`); the mover (ovl4 `0xad5`) moves only on a
result ≥ 5, so the party stays put and no time passes. `FAR:0xdd96` is only ever written 0 (ovl1 `0x5f1`, ovl4
`0x63d`): a walk-through-walls switch with no way to turn it on. The remaster's "cell 0 blocks" is therefore DOS. Cell 1 is walkable and drawn as a wall: the manual's secret doors, "found by walking into a wall"
(III PDF 14 / pr.22) `[~]`.

**The event sound word `FAR:0xde58`** (s136 `[C]` static, ovl4): 0 at the dungeon loop's top (`0xa10`), after an exit's
question (`0xb85`) and as each event op starts (`0x1514`); the step writes 0x0c on a wall bump (`0xf6c`) and 0x2a on a special door (`0xf92`); the event ops write
0x2a for ops 1–4 and 9 / 10 (`0x1550`, ops 9 / 10 jump there), 3 for ops 7 / 8 (`0x1609`), 9 for op 12 (`0x1672`, not 13)
and 0x1b for op 14 (`0x1693`, not 15). The numbers are the Amiga's sound numbers (`reverse-engineering/amiga/phantasie3/
FORMAT.md` 7.2, which reads when each plays); the remaster records them as game sounds (`P3Sounds`).

## 8. Remaster notes

- Built (T4, s83): `Phantasie3DungeonScript` runs these programs over the live record (cleared ops and rewritten cells
  persist with the dungeon save); `Phantasie3Campaign.Dungeon.cs` the step (§7a), exits (§7) and squares (§2). An item
  op (6) with the continue bit stops at "Keep …?" and resumes after the answer; a sub-square with choices (op 22) keeps
  one caller continuation. The copy-protection quiz is left out. Fields the data never uses read 0 `[?]`.
- Faithful quirks kept: type 18's gold-taking runs per living member; type 24 with a3 = 0; op 19's jump returning the
  party to its previous square. Not kept: type 12's Yes / No after "[original text]" (its answer is ignored).
- The two DOS bugs (manual silent) are fixed by default with DOS as an option: the archive altar grants Divine Aid as its
  text says (`p3.altar` = `fixed`: field 120 reads as 117 in the test and the write; `dos` keeps the Torso fault, §6),
  and event gold is unsigned (`p3.event-gold` = `fixed`; `dos` sign-extends, so the wizard's op 9 gives 256 back).
