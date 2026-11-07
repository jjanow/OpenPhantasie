# Phantasie IV — dungeon events: `O_0xx` bytecode, triggers, traps, chests, doors (spec, 3d.6 / 3d.8)

Source: X68000 `P4.X`, static reading with the program's symbol table (addresses = P4.X image addresses,
`../FUNCTIONS.md`). Interpreter: `_event` `0x1A1F2`, `_exe_ev` `0x195B0`, `_evadr` `0x18474`, `_evval` `0x184C4`,
`_evj` `0x1850E`; loader `_dload` `0x1A93A`; dungeon loop `_dmain` `0x17C56`. Data: `O_001`–`O_021` (disk 1).
Tool: `tools/scripts/x68k_p4_events.py` (disassembler + coverage check); listings of all 21 files in
`../extracted/events/O_0xx.txt`. Tags: `[C]` from the code, `[~]` likely, `[?]` open. Nothing here was checked in an
emulator; MSX2 / PC-98 not cross-checked. Written source: PC-98 manual pp. 20–21 (skills), p. 38 (dungeons).

Record offsets: `../FORMAT.md` §6.2 (party records at `_pmb` `0x79708`, so `0x79708 + off` = field `off`); `_par`
§6.7 (`0x789C8`).

## 1. Maps and files `[C]`

- The game loop `_game` runs `_town` / `_outdoor` / `_dungeon` by `_cnewf` = **0 town, 1 outdoors, 2 dungeon** `[C]`.
  (`../FORMAT.md` §6.7 had dungeon and outdoors the other way round; corrected s82: `_odungeon` sets 2 for a dungeon
  and `_tele` treats 1 as the outdoor map with a `MOUT` block.)
- Dungeon map n (1–21; names `_dungeons` / `_dnames`, e.g. 1 Pendragon Town Hall, 2 under the Town Hall, 3 Lord
  Castlebaum's mansion, 4 Orc village, 7 under Lord Keogh's castle, 21 Skull Fortress) = two files read by `_dload`:
  `IND<n>.PDT` (disk 2, 1922 B = 31 × 31 big-endian words, the map `_pinmap`) and `O_0<nn>` (disk 1, the event file,
  read whole into `_evbuf`). Name built as `"o_0"` + (`"0"` if n < 10) + n.
- `_pinmap` word per square: bits 0–5 floor type, bits 6–10 the north-side wall picture ("yoko"), bits 11–13 the
  west-side wall, bits 14–15 set by `_prex`: the doorway-edge marks of the view (`../FORMAT.md` §6.9, s108) `[C]` (row/column 30 hold the south / east walls of the last row /
  column). `_mkdin` turns it into `_dinmap` (30 × 30 words): four 4-bit movement codes per square, N = bits 0–3,
  E = 4–7, S = 8–11, W = 12–15. Code 0 = open, 1 = wall, 2 = locked door, 3 = door that will not open, 7 = map edge
  (`_dmove`). `_mkdin` itself only produces 0 / 1 / 7: a side is open when the wall picture is one of 2, 3, 16, 19, 31,
  and then the neighbour's floor type decides through the table at `0x437E6` (floor types 0–3, 5, 6, 27–32, 40, 42,
  43 passable; 4, 7–26, 33–39, 41 blocked). Doors 2 and 3 exist only where an event writes them (§4). Floor type 6 =
  a chest already opened (`_dchest`); 27–30 = water (§6.3).
- Direction numbers everywhere: 0 N, 1 E, 2 S, 3 W (`_dmove`: N = y − 1, E = x + 1).
- Entering from the outdoors (`_odungeon` `0x14E9A`, s94): "There is X here. Go inside?"; yes sets `_cmovf` = 1,
  `_cnewf` = 2 and `_cnewn` / `_cnewx` / `_cnewy` from the MOUT dungeon table (`outdoor.md` §1, offset `0x2AA`). The
  facing `_dire` is **not** set there or by `_dload`: only `_dmove` and SETDIR (`_exe_ev`) write it, so a map is
  entered facing the way the party last faced (0 = N before any move, BSS) `[C]`.

## 2. Event file layout `[C]`

```
byte 0       n = number of trigger entries
bytes 1–2    0 (unused)
bytes 3–4    base = start of the code, u16 little-endian; always (n + 1) × 5
entry i = 1…n at byte 5 i:   x, y, dir, u16 LE offset (relative to base)
code + text  from base to the end of the file
```

All 16-bit operands are **little-endian** (`_evadr` / `_evval` combine `_evbuf[pc]` low and `_evbuf[pc + 1]` high),
although the CPU is big-endian — the files were probably built on a PC `[~]`. Code and text addresses are relative to
`base`. Text is Shift-JIS, NUL-terminated, `$` = line break (`_eprint`), max 18 full-width characters per line
(`_eprint` wraps at 288 pixels).

### 2.1 When events run (`_event(x, y, dir)`) `[C]`

`_event` walks the entries in file order and runs the **first** whose x and y match and whose dir is 4 (any) or the
direction the party faces (`__dire`). Afterwards it always calls `_rndenc` (§5), whether an entry matched or not.

| x, y | Caller | When |
|---|---|---|
| (0x20, 0) | `_dload` | once when the map is loaded (`_event(0x20, 0, 4)`): sets doors, the encounter pool and rate |
| (0x21, 0) | `_dmain` | the party tried to walk off the map edge (`_dmove` returned 7); usually a TELEPORT to the outdoors |
| (x, y) | `_dmain` | at the top of **every** pass of the dungeon loop on the party's square |

The dungeon loop runs one pass per player action (a step, a bump into a wall, viewing a profile, casting, using an item,
the system menu), so a square's event **runs again after every action taken on it**, and every action rolls a random
encounter. Scripts guard one-time events with a per-location flag (§3, `TESTLF n; JT end; SETLF n`).

### 2.2 The dungeon loop and the move buttons (`_dmain`, `_dmove`) `[C]` (s93, decompile)

One pass: water damage (§6.3, if the square is water and Levitation is off), `_event` on the party's square with its
facing, then one input: a queued button (`__inext` < 9) runs at once, otherwise a key. The nine pad buttons are, in
`_bdng` order, 0 SW, 1 S, 2 SE, 3 W, (4 none), 5 E, 6 NW, 7 N, 8 NE. An orthogonal button is one `_dmove`. A **diagonal
is two steps over two passes**: it queues the vertical button (`__inext` = 1 or 7) and moves horizontally now; if that
step is blocked (any code ≠ 0) it queues the horizontal button instead (`__inext` = 3 or 5) and moves vertically now.
The intermediate square's event and encounter roll run in between. `_dmove` turns the facing only when no button is
queued (`__inext` = 9), so a diagonal faces the direction of its **second** step. Every `_ewait`, `_xmenu`, INPUTS /
INPUTN and fight sets `__inext` = 9 (drops a queued step) and `__seadc` = 0 (stops Examine). A step off the map (code 7)
runs `_event(0x21, 0, facing)`. End of pass: `_seasel` while Examine runs; `_cmovf` set (teleport away, party lost) →
leave. Profile, party info, cast, use item and the system menu take a pass too (the square's event runs again).

Every event starts with `__encf` = 1 (random encounters allowed); NOENC clears it and TELEPORT clears it. `__encf` is
not reset when no entry matches, so after a NOENC square encounters stay off on following squares until another event
runs `[C]`. Many corridors use `NOENC; TITLE …` for this reason.

## 3. Flags `[C]`

| Store | Address | Opcodes | Size |
|---|---|---|---|
| per-location flags | `_par`+160 + `_cmapn` × 20 + n | TESTLF / SETLF / CLRLF | 20 per map (n = 0–19 all used) |
| story flags | `_par`+120 + n | TESTSF / SETSF / CLRSF / TGLSF | 64 (n used up to 63) |

- **Per-location flags reset in rotation** (`_dreset`, called by every `_dload`): story byte 0 (`_par`+120) is a
  counter; each dungeon load clears the 20 flags of map *counter* and then decrements it (below 1 → 21). So one map's
  chests, fights and one-time events come back every 21 dungeon loads (any dungeon, including going up/down a level).
  A new game starts with the counter at 0, so the first load clears map 0 and the cycle is then 21, 20, …, 1 `[C]`.
- **Story flags read by P4.X itself:** Mystic score (`towns.md` §7: bytes 124–12A = flags 4–10, 147 = 39, 14E = 46);
  `_monevf` sets flag 12 when the party beats encounter 72 (Lord Castlebaum), **9** for 73 (Kor) and 59 for 83
  (Arathamus); `_town` / `_ending` use 56–60 (entering map 1 with flag 56 → the ending sequence; which `O_021` events set
56, 57, 58 and 60: `ending.md` §1); `_ochkedge` /
  `_omain` read 60; `_party` / `_party_info` / `_partyact` use 62–63 (party options). Story flags used only by the
  scripts are listed per file in the listings.
- Quest flags (O_001, the Town Hall) `[C]`: 1 / 2 / 3 = appointment booked with the sage HILL / NILL / LILL (typed at the
  receptionist's desk, CMPS against "JILL", "KILL", "HILL", "NILL", "LILL"; JILL and KILL need no appointment);
  4 / 5 / 6 = mini-quest accepted (box delivery / rescue / orc quest); **7 / 8 / 9 = mini-quest done** (7 set in O_003
  Castlebaum's mansion, 8 in O_007 under Keogh's castle, 9 by beating Kor); 10 = advanced quest accepted (item 144,
  the scroll).

### 3.1 Advanced-quest unlock (O_001 `0x7C7`) `[C]`

Asked at any of the three sages' desks (CALL before their own quest offer). The party qualifies if **any** of:

1. at least two of the mini-quest-done flags 7, 8, 9;
2. party level value `_par`+0B **≥ 8**;
3. `_par`+0B ≥ 6 **and** at least one of 7, 8, 9.

`_par`+0B is written when the party leaves a town (`_exittown` `0x5D68`: trunc(Σ member levels / party size + 0.5),
the rounded average) and by the Mystic (truncated average, `towns.md` §7), so the check uses the average level as of
the last town exit or Mystic visit. Qualifying parties get the advanced-quest offer (once: flag 10). Otherwise, if two
of 4, 5, 6 are set: "You have already taken two missions …"; else the sage's own mini-quest offer follows (refused if
that sage's quest is already taken: "no other mission"). The secondary sources disagree with each other and with this
(handbook "level 7 or two mini-quests"; kitahei PC-88 "one quest + level ≥ 5, or two quests",
`docs/manuals/phantasie4/phantasie4-secondary-sources.md` §11 item 12); the manual is silent, so not raised.

## 4. Opcodes (`_exe_ev`) `[C]`

`pc` starts at base + entry offset. Operands: b = byte, w = u16 LE value, a = code address (base-relative), t = text
address (base-relative). Length includes the opcode. Two registers: `tff` (true/false, `__tff`) and `cmp` (signed
compare result, `0x4A432`). `evint` = last number typed (INPUTN), `evstr` = last text typed (INPUTS). Unknown opcodes
(> 0x3A) are skipped as 1 byte (none occur).

| Op | Name | Operands | Len | Effect |
|---|---|---|---|---|
| 00 | END | | 1 | stop |
| 01 | JMP | a | 3 | jump |
| 02 | JT | a | 3 | jump if tff ≠ 0 |
| 03 | JF | a | 3 | jump if tff = 0 |
| 04 | JGT | a | 3 | jump if cmp > 0 |
| 05 | JEQ | a | 3 | jump if cmp = 0 |
| 06 | JLT | a | 3 | jump if cmp < 0 |
| 07 | CALL | a | 3 | return address = pc + 3, jump (one level only: a single saved address) |
| 08 | RET | | 1 | jump to the saved return address |
| 09 | PRINT | t | 3 | clear the message box and print the text |
| 0A | TITLE | t | 3 | clear the box, print the text centred on the top line (the location name) |
| 0B | WAIT | | 1 | "Space" prompt, wait for the key (`_ewait`; also stops Examine, §6.4) |
| 0C | INPUTS | | 1 | prompt "Letter>", read up to 10 characters into evstr (s135: §4.1) |
| 0D | CMPS | t | 3 | cmp = strcmpi(evstr, text) (case-insensitive); tff = 1 if equal, else 0 |
| 0E | INPUTN | | 1 | prompt "Number>", read up to 5 digits into evint (s135: §4.1) |
| 0F | CMPN | w | 3 | cmp = evint − w |
| 10 | HASITEM | b | 2 | tff = 1 if item b is in the party pack (`_par`+20, 128) or any member's carried slots (+52–5A); nothing removed |
| 11 | GIVEITEM | b | 2 | put item b in the first empty pack slot (pack full → overwrite slot rand(127), 1–127); rings 129 / 131 get 20 charges (any other item keeps the slot's old charge byte); "<item> obtained." + WAIT |
| 12 | CMPGOLD | w | 3 | w = 0 → evint. If party gold ≤ 30 000: cmp = gold − w; else cmp = 30 000 |
| 13 | GIVEGOLD | w | 3 | party gold += w; "w GP obtained." + WAIT. If gold > 990 000 afterwards, gold **+=** 990 000 (original bug: meant as a cap) |
| 14 / 15 / 16 | TESTLF / SETLF / CLRLF | b | 2 | per-location flag b: tff = flag / flag = 1 / flag = 0 |
| 17 / 18 / 19 | TESTSF / SETSF / CLRSF | b | 2 | story flag b: same |
| 1A | DAMAGE | b | 2 | every member (alive or not) takes rand(b) damage (1…b) via `_ddamage` (§6.1) |
| 1B | WALL2 | x, y, d | 4 | movement code of side d of square (x, y) = 2 (locked door, can be picked) |
| 1C | WALL0 | x, y, d | 4 | … = 0 (open) |
| 1D | OPENGATE | x, y | 3 | if square (x, y)'s wall picture is 16 (gate), animate it to 19 (open) with a sound; movement codes untouched |
| 1E | TELEPORT | f, n, x, y, d | 6 | `_tele`: go to kind f (0 town, 1 outdoors, 2 dungeon) map n at (x, y), facing d. f = 0 and n = 0 → town number evint. f = 2 and n = current map → move inside the map. Outdoors: `MOUT` block from the table at `0x43732` by n. Clears `__encf` |
| 1F | NOP | b | 2 | nothing |
| 20 | SOUND | b | 2 | `_sdnoi(b)` |
| 21 | REDRAW | | 1 | redraw the view |
| 22 | NOENC | | 1 | no random encounter after this event (§2.1) |
| 23 | FIGHT | m | 2 | fight encounter m (`DATM` group, `_denc`); not on water squares (27–30). `_denc` masks the floor to **5 bits** (`& 0x1F`): battle terrain 0 when that is 3 or 31 (floors 3, 31 and 35; its test for 32 can never match), else 1 (s93) |
| 24 | CHEST | k | 2 | `_dchest(k)` (§6.2); tff = 1 if the party gets the contents |
| 25 | CMPLVL | b | 2 | cmp = `_par`+0B − b (§3.1) |
| 26 | CHANCE | b | 2 | tff = 1 if rand(100) < b (so a (b − 1) % chance) |
| 27 | MCLEAR | | 1 | empty the menu |
| 28 | MADD | t | 3 | add the text as the next menu line (max 40 bytes per line) |
| 29 | MENU | | 1 | show the numbered menu, answer = the number chosen |
| 2A | MJMP | b, a | 4 | jump if answer = b |
| 2B | POOLADD | m | 2 | add encounter m to the random-encounter pool (`_mons`, room for 24) |
| 2C | ENCPCT | b | 2 | random-encounter rate = b (§5) |
| 2D | POOLCLR | | 1 | empty the pool |
| 2E | WALL3 | x, y, d | 4 | movement code = 3 (door that will not open) |
| 2F | LISTEN | | 1 | `_listen`: each living member rolls rand(100) ≤ Listen (+46); the **last** in party order who succeeds is named: "<name> sensed monsters nearby." + WAIT. No other effect |
| 30 | TRAP | b | 2 | `_dtrap` (§6.1); the operand is **not used**. tff = 0 if the party turned back, else 1 |
| 31 | STEPBACK | | 1 | put the party back on the square it came from |
| 32 | TAKEITEM | b | 2 | remove the first item b found (pack first, then members' slots); no message |
| 33 | TAKEGOLD | w | 3 | w = 0 → evint; party gold −= w (no check: scripts test with CMPGOLD first) |
| 34 | RACEIS | b | 2 | tff = 1 if any member (alive or not) is of race b (only use: 34 = Duck, O_003) |
| 35 | ENCOPT | b | 2 | writes `_encopt`; **nothing reads it** (dead variable) |
| 36 | TGLSF | b | 2 | story flag b ^= 1 |
| 37 / 38 | WALLPIC5 / WALLPIC4 | x, y | 3 | north wall picture of (x, y) = 5 / 4, redraw (graphics only; `_dinmap` not rebuilt) `[~]` |
| 39 | RESTORE | | 1 | every member, dead ones too: HP = max, MP = max; status refresh (`_healall` → `_healch`, which also turns a sleeping living member Okay outside combat). Wounds unchanged; the dead stay dead (s93: `_refle` sets HP / MP for all) |
| 3A | SETFLOOR | x, y, v | 4 | floor type of (x, y) = v (not passed to `_dinmap`) |

### 4.1 The typed prompts (INPUTS `0x196A2`, INPUTN `0x19768`; s135) `[C]`

Both: `_wfbox(msg, 14, 0, 80, 304, 16)` (a colour-14 band across the message window at y 80, where `_ewait`'s "Space"
box sits), the prompt "文字＞" / "数字＞" (`0x437D8` / `0x437DF`; "Letter>" / "Number>") by `_wlocpr` at (16, 80) in the
current colour, `_sdbeep`, then the reader at the text cursor; afterwards `_bgcol` = 16, `0x438C6` = 0 and `__inext` = 9
(a queued step dropped). No redraw of the view and no `_iaflag` write.
- **INPUTS** `_wscan(msg, evstr, 10)` (`0x34550`): accepts bytes 0x20–0x7E and 0xA0–0xDF (half-width katakana) while
  fewer than 10 are typed, each drawn in font 0 on a colour-14 cell; codes 8, 127 and 29 erase the last one (its cell
  boxed in 14); Return ends (an empty answer is allowed); Escape and every other key are ignored. The answer keeps its
  case (CMPS compares case-insensitively). All CMPS answers in the shipped files are ASCII ("JILL" …, "A" … "I").
- **INPUTN** `_wiscan(msg, &evint, 5)` (`0x34742`): digits only, up to 5, the value built as v · 10 + digit (erase: v / 10);
  Return ends, with no digit the value is 0.
- **Cursor:** a 1-pixel bar, `_ylen` 16 high, at the text cursor, flipping between colours 0 and 14 every 1000 key polls
  (`_wgetc`, INPUTS) or 150 (`_wiscan`'s own loop): CPU-bound, so its rate is the machine's `[~]`.
- **The view does not animate** while either reads (no `_mgetc`, so no `_ianim`; FORMAT.md §6.9).
- **Seen on XEiJ (s135):** a one-member party warped to Town Hall (map 1) (9, 15) (RE-REFERENCE "Into a dungeon with a
  party", pokes `c7d7c=1,c7d7e=9,c7d80=f`), one step north onto (9, 14): PRINT + WAIT, then "[original text]"
  + INPUTS (`O_001` `0x0674`). The message window lies over the view's top three rows; the grey band at its y 80 holds
  "文字＞" from x 16 (`parity/shots/p4-x68k-event-letters-prompt.png`). A 10 s GIF of the prompt (116 frames): the view
  (with its wall-top torches, which step under `_ewait`) never changes; the only change is a 1 × 16 cursor bar at the text
  cursor flipping every frame, about 86 ms. Typed "ji" is echoed in font 0, lower case kept, the bar after it
  (`p4-x68k-event-letters-typed-zoom.png`).
- Remaster: the prompt is a line of its text box (layout of its own; no beep), the same characters (ASCII
  only: no katakana input) and limits, Backspace erases, Enter ends; the view stops while typing (s135). **Cursor bar
  (s136):** a 1-pixel bar one text row high right after the typed text (the line's colour; it replaced the remaster's
  static "_"), on first, flipping every 86 ms (the XEiJ GIF's rate; CPU-bound in the original, so `[~]`), for the
  dungeon's INPUTS / INPUTN and the town's prompts (`P4DungeonScreen.Cursor.cs`). `_wscan` / `_wiscan` have three callers:
  `_exe_ev` (these prompts), `_mkname` and `_re_name` (the town's name prompts: the same bar `[C]`). s151: the Bank's
  amount is `_numin` (its keypad in window 34, a steady 8 × 16 black block for a cursor; FORMAT.md 6.9 "Dungeon waits,
  …"), drawn so; the colour-14 band is drawn (with the prompt and the typed text in it); `_sdbeep` is ADPCM sound effect
  2 of `VDATA` (not an OPM or a beep); s152: played (FORMAT.md 6.13).

Usage over all 21 files (instructions reached): END 668, JT 339, PRINT 324, WALL0 316, TITLE 233, TESTLF 229, POOLADD
180, NOENC 173, SETLF 172, WAIT 165, JMP 164, TELEPORT 163, JF 152, MADD 137, REDRAW 121, TESTSF 118, FIGHT 100,
GIVEITEM 90, MJMP 81, CHEST 76, WALL3 74, SETFLOOR 68, LISTEN 56, MCLEAR 56, MENU 54, SETSF 48, WALL2 48, HASITEM 31,
ENCPCT 23, JLT 21, TRAP 19, CMPN 19, OPENGATE 18, POOLCLR 18, INPUTN 16, JGT 15, TGLSF 12, the rest ≤ 11.

## 5. Random encounters in dungeons (`_rndenc`) `[C]`

After every `_event` call: if `__encf` ≠ 0 and rand(N) ≤ rate (ENCPCT), fight a random member of the pool
(`_mons[rand(pool size) − 1]`, `_denc`). N = 100, or **400 while Examine runs** (§6.4). So the chance per action is
rate % (rate / 4 % during Examine). Not on water squares (`_denc` refuses floor 27–30). The map-load event of each file
sets pool and rate (e.g. O_002: 10 entries, rate 4).

## 6. Helpers

### 6.1 Damage, floor traps (`_ddamage`, `_dtrap`) `[C]`

- `_ddamage(member, d)`: HP −= d; HP ≤ d → HP 0 and condition 0 (dead). Then status refresh; if no member is alive, the
  party is lost (`_deadmsg`). Body parts are not touched.
- `_dtrap` (TRAP): each living member rolls rand(100) ≤ **Spot Trap** (+42); the last success finds it: "<name> found a
  trap." Menu: 1 Disarm the trap / 2 Turn back. Turn back → tff = 0 (scripts then STEPBACK). Disarm: the player picks
  a member; success if that member is alive and rand(100) ≤ **Disarm** (+44): "<name> succeeded." Otherwise "<name>
  failed." and the trap goes off. Nobody spots it → it goes off at once. **Trap going off:** "A trap!", then for each
  member: if condition = 1 (Okay) and rand(100) < record **+35**, "<name> dodged it."; else damage = rand(5) ×
  (trunc(`_cmapn` / 5) + 1) (1–5 on maps 1–4, up to 5–25 on map 21). tff = 1 whenever the trap is gone (disarmed or
  sprung). Record +35 is the combat defence D (`combat.md` §1, s82), also scaled by aging (D4-034).

### 6.2 Chests (`_dchest(k)`, `_ctrap`, `_cspi`) `[C]`

If the party's square already has floor type 6 (opened chest): tff = 0, nothing shown. Else, for k = 0–2 "There is a
chest.":

| k | Rule |
|---|---|
| 0 | plain: tff = 1 |
| 1 | locked: each living member rolls rand(100) ≤ **Pick Lock** (+4A); last success "<name> forced the lock." tff = 1; none: "The lock won't open." tff = 0 |
| 2 | trapped (`_ctrap`): same as the floor trap (§6.1; texts "found a trap on the chest", menu Disarm / Give up); Give up → tff = 0, otherwise tff = 1 even when the trap went off |
| 3 | hidden item (`_cspi`), no chest text: each living member rolls rand(100) ≤ record **+42** (the Spot Trap skill, the same byte `_dtrap` uses); last success "<name> found an item." tff = 1; none: tff = 0, silently |

For k = 0–2 with tff = 1 the square becomes floor 6 (opened). Scripts follow with `JF end; GIVEITEM n` (or GIVEGOLD),
guarded by a per-location flag that is set **before** the CHEST, so a failed lock or search is not retried until the
flag cycle (§3) clears it. 76 CHEST uses: k = 3 hidden item 34, k = 0 plain 27, k = 2 trapped 9, k = 1 locked 6.

### 6.3 Doors, water `[C]`

- **Locked doors** (`_dmove` → `_try_ul(code)`): each living member rolls rand(100) ≤ Pick Lock (+4A), on code 3 too.
  Code 2 with a success: "<name> forced the lock." and the party passes. Code 3, or nobody succeeds: "The lock won't
  open." and the party stays. **In both cases** the square's side the party **faces** becomes 3 (`_setmove(x, y,
  __dire, 3)`, s93: the facing, not the move direction; they differ on a diagonal's first step), so a picked door
  cannot be passed again from that side until the map is reloaded (original quirk; the other side has its own code).
  The message ends with `_ewait`, which drops a queued diagonal step.
- **Water in dungeons** (`_dswim`, floor 27–30, only while `_par`+15 = 0, the Levitation flag: `outdoor.md` §6, s82): on each loop pass, each member
  rolls d = rand(level + 1) + 1; a living member with rand(100) > **Swim** (+4C) takes d damage. Runs before the
  square's event. Outdoor swimming: not covered here.

### 6.4 Examine (dungeon "E", `_seainit` / `_seasel`) `[C]`

The dungeon's E button (manual p. 38 "E 調べる") starts an **automatic walk** of up to 63 steps: each step goes to the
open neighbour square with the lowest visit count (`_sw`; walls, edges and closed sides count as 90; ties: N, then E, S,
W order of the compare chain), adding 1 to the current square's count. Any WAIT, menu, chest, fight end or
teleport stops it (`__seadc` = 0). s93 (decompile): a side counts as open only through the plain opening (north / south
picture 31, west / east wall 7) with movement code 0, inside the map; if all four count 90 it still queues N. The walk
runs its passes without a key until it ends or something stops it. The visit counts `_sw` are never cleared (bug below),
so they carry over between maps and Examine runs. While it runs, random encounters roll rand(400) instead of rand(100) (§5). Nothing
in the spot / trap / chest rolls depends on it. `_seainit`'s clearing loop is an original bug: it clears the single byte
`_dinmap`+0E 900 times instead of the visit counts — that byte holds the S and W movement codes of square (7, 0), which
become 0 (open) until the map is reloaded; the visit counts are never cleared by it.

## 7. Worked example: O_002 (under the Town Hall), the three cells

```
trigger on map load            -> L00a3: NOENC; WALL3 14,20,S; WALL3 10,17,N; WALL3 14,17,N; WALL3 10,12,N; …
                                         POOLCLR; POOLADD 0,1,2,3,4,5,6,5,2,7; ENCPCT 4; END
trigger at (8,17) facing N     -> L011b: TESTSF 11; JT L016f            ; buttons already used → "the three cells are empty"
                                         PRINT "three push buttons … 'Do not touch, by order of Mr. Kill.'"; WAIT
                                         PRINT … (a Half-Orc, Sprixies and Dark Sprites in the cells) …
                                         MCLEAR; MADD "Don't press" / "Open all three" / "Free only the Half-Orc" /
                                         "Free the Sprixies and Dark Sprites"; MENU
                                         MJMP 1 → end; SETSF 11
                                         MJMP 4 → L015a
                                         OPENGATE 10,17; WALL0 10,17,N; PRINT (Half-Orc: "don't trust Lord Castlebaum")
                                         MJMP 3 → L00a0 (WAIT; REDRAW; END)
                                         WAIT
                              L015a:     OPENGATE 14,17; WALL0 14,17,N; OPENGATE 14,21; WALL0 14,20,S
                                         PRINT ("the item is in the cell, but fight us first"); WAIT; FIGHT 16 (Sprixies)
```

The cell doors are sealed (code 3) at every load; pressing the buttons opens them with OPENGATE (picture) + WALL0
(movement), and story flag 11 makes the choice permanent.

## 8. Coverage (`x68k_p4_events.py`) `[C]`

Recursive traversal from every trigger reaches 4,722 instructions; **473 of the 484** O_ strings in
`../extracted/text/strings.json` are reached as PRINT / TITLE / MADD / CMPS operands, plus 70 short texts the string
extractor skipped (answers such as "A", "JILL", "はい"). s93: 47 of those skipped texts are printed (room names such as
拷問室, shop lines, two longer messages); they were translated and `build_p4_gamedata.py` now adds every PRINT / TITLE /
MADD text the bytecode reaches (`Phantasie4DungeonTests.EveryReachableTextHasAnEnglishString`). Bytes covered: 41,618 of 42,049. The rest is unreferenced, i.e.
content no trigger reaches (no indirect jumps exist, so this is final for the X68000 files):

- text never printed: O_007 `0x3AF`; O_009 `0x729`; O_018 `0x7E6`; O_017 `0x2E3` ("[original text]", POW holding area); O_021 `0x322`–`0x35A`
  (a lever on the wall: "Pull it? / Pull / Don't pull") and `0x4E8`;
- dead code: O_009 `0x400`–`0x406`; O_014 `0x99`–`0xBC` and O_017 `0xE9`–`0x10C` (an identical "CHEST 0; JF; PRINT 'this
  chest is empty'" fragment); O_015 `0x8C`, O_016 `0x87` (WAIT; REDRAW; END); O_021 `0x3EC`; single padding bytes.

## 9. Against the manual

- p. 38: a failed disarm hurts everyone — code agrees (§6.1; each member may dodge with record +35).
- p. 21 (H Pick Lock): some locked doors never open with the skill; their key must be found elsewhere — code: code-3
  doors (WALL3) open only by script (HASITEM → WALL0) `[C]`. Agrees.
- p. 38: dungeons have secret doors and passages — scripts open walls with WALL0. Agrees.
- p. 20 (F Listen): hearing monsters approach in advance — code: LISTEN only names who heard them; any advantage is in
  the combat code (`_oencchk` outdoors), not in the event. Not a conflict.
- **D4-041 (decided: manual; user s84: Examine +20 to the trap and treasure rolls):** p. 38 "E Examine: the whole party searches its surroundings, especially in rooms; this finds traps and
  treasure more efficiently" vs the code's E = automatic 63-step walk with a quarter of the encounter rate and no
  better finding rolls (§6.4).
- **Noted under D4-036** (not a separate conflict: the manual names no skill for hidden dungeon items): p. 20 "C Spot (アイテムの発見) … find items lying after a fight"; "D Trap (ワナの発見) … find traps". Hidden
  dungeon items (CHEST 3) are found with record +42, the byte the trap search uses (Spot Trap), not +40 (Spot Item).
  The manual does not say which skill finds hidden dungeon items, so this may be no conflict.

## 9a. Remaster reading `[~]` (s93)

- **D4-041 manual Examine:** E makes one pass on the square (its event runs again) with +20 on the spotting rolls of
  that pass: floor and chest traps (`_dtrap` / `_ctrap`, record +42) and hidden items (`_cspi`, +42). The disarm and
  lock rolls are not "finding" rolls and get no bonus. Encounter rate unchanged.

## 10. Open `[?]`

- (Resolved s82: record +35 = combat defence D, `combat.md` §1; `_par`+15 = Levitation flag, `outdoor.md` §6.)
- Encounter numbers: `FIGHT` / `POOLADD` operands index `DATM` groups (`_enc_mons`); per-file lists not tabulated.
- MSX2 / PC-98 event files not compared; nothing checked in an emulator.
- `_getwho` accepts Escape (button 14) and then reads member 14's record (garbage); the remaster asks for a member.
- What `__cflag` ≠ 0 after `_encount` means besides a wiped party (a fled party?) — the remaster treats only a lost
  party as the end; a dungeon step's time cost (if any) is not in `_dmain`.
