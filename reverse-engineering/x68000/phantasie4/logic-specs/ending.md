# Phantasie IV — endings, final screen and the character "Score" (spec, 3d.6, s82)

Source: X68000 `P4.X`, static reading of `_ending` (`0xB378`), `_endset` (`0xB042`), `_common_proc` (`0xAECE`),
`_banquet` (`0xADE0`), `_arathamus` (`0xACDC`), `_endmsg` (`0xA8B2`), `_endstory` (`0xA9E6`), and the call in `_town`
(`0x321C`). Written sources: the registration card (`phantasie4-manual-en.md`, "Registration card"), manual p. 29
(Score). Tags: `[C]` code, `[~]` likely, `[?]` open. Not cross-checked on MSX2 or in an emulator.

## 1. When an ending runs `[C]`

`_town` calls `_ending()` when the party enters **map 1** (`_cnewn` = 1, Pendragon: town 1 in `_towns` and in the Transportation table `[C]`) and story flag `_par`+158 is
set. Five story flags `_par`+158 … +15C (f0 … f4 = story flags 56 … 60, `events.md` §3) choose the ending. All five
belong to the Skull Fortress (map 21, `O_021`; s110 `[C]`, no other script or code sets them):

| Flag | Set by |
|---|---|
| f0 (56) | pulling the lever at (11, 9) facing N: "Lord Geoffrey came out of the cell and thanked you …" (`O_021` `0x031D`, once) |
| f1 (57) | the first council hall (16, 12) facing S: choosing "Use Zeus Juice" with a Zeus Juice (item 0x7E) in the party — one is taken, the generals die, Arathamus escapes (`0x0662`); the hall's floor is then opened (`L012F`) |
| f2 (58) | the second council hall (16, 20) facing S: the same with another Zeus Juice (`0x0AB3`) |
| f3 (59) | beating encounter 83 (Arathamus, `FIGHT 83` in his room at (13, 23) facing W): `_monevf` marks story flag 59 when the fight ends by 101 (all killed) or 103 (melted), not when he leaves the field or surrenders (`combat.md` §8.1) |
| f4 (60) | pulling the lever at (10, 18) facing N: the humming stops, the energy barrier round the island is gone (`0x09C1`; `_ochkedge` / `_omain` read it) |

Lord Geoffrey at (14, 10) (any facing, once f0 is set) offers to teleport the party to Pendragon (`TELEPORT 0, 1`):
at once with f3 or f4 (f4 adds his congratulations); with f1 and f2 but not f3 he first urges them on to Arathamus;
otherwise he urges them on if a Zeus Juice is in the party, else hints that going on without one is foolish, and then
offers. Arriving in Pendragon runs the ending. The first branch that matches wins:

| Condition | Ending score | Messages / scenes | Afterwards |
|---|---|---|---|
| f0 set, f1 clear | **125** | "The people of Pendragon are grateful for the rescue of Lord Geoffrey." + "Within weeks Arathamus and his army will leave the Skull Islands … attack on Scandor. You feel that it has all ended in failure … But that is another adventure." | f0–f3 all set to 1 |
| f1 set, f2 clear | **150** | rescue lines + "With half of Arathamus's generals fallen, it will take him months to gather his forces again. Today you are heroes, but soon … another adventure." | f0–f3 set |
| f2 set, f3 clear | **175** | rescue lines + "With all of Arathamus's generals fallen, it will take him years to rebuild his army…", banquet scene (`END2`), "Lord Geoffrey held a banquet. He granted you the rank of 'Noble'." | f0–f3 set; **every member's social class = 3 Noble** |
| f3 set, f4 clear | **200** | rescue lines + "They are also grateful that you defeated Arathamus and his generals.", banquet, "He granted you the title of 'Lord'.", each member shown as "<origin> of <name> Lord", "Only… the energy barrier still remains…", Arathamus in his cell (`END3`), "Lord Geoffrey took the senseless body of Arathamus back to Pendragon … tomorrow is another story…" | **every member's social class = 9 Lord** |
| f4 set | **225** | banquet + Lord title as above, "We wish to thank and celebrate these great souls who have restored all of Scandor to peace." | social class = 9 Lord |

(Message line ranges: table at `0x40C00`, lines at `0x40C58`; English in `extracted/text/strings.json`.) Because the
125 / 150 / 175 branches set f0–f3, the next visit to map 1 with f4 still clear runs the 200 ending `[~]` (static
reading of the flag order; check in play).

### 1.1 After the final screen; a save made there (s110) `[C]`

`_endstory` asks "Save the game?" (Yes → `_disk(1)`, the slot menu), then `_acgamend(1)`: a "Select Game" window with
two icons only (`_mkicon` clears every other button), **End game** (`_acend(0)`: confirm → "The game ends.", back to
Human68k) or **Back to the title screen** (`_acend(1)`: confirm → `_par` cleared, `__cflag` = 1, `_main` starts over
with the title). A No to either confirm shows the two icons again; the party never walks on in Pendragon.

A save made there holds Pendragon (`_endset` calls `_tload(1)`, which sets `_cmapf` = 0, `_cmapn` = 1; `_save` writes
both into `_par`+0 / +1). Loading it: `_init` takes `_cnewf` / `_cnewn` from `_par`, `_game` sets `_cmovf` = 2 and
calls `_town`, which skips `_townent` / `_welcome` (no aging, no deposits) and, with f0 set, runs `_ending` **again**
with the flags the last ending left: after 125, 150 or 175 (f0–f3 set) the reload runs the **200** ending (Lord title,
cell scene); a 200 or 225 save runs the same ending again. Each run rewrites +09 (ending score + average level), HP /
MP, Charisma and the social class (§2), and ends at the final screen again. Built in the remaster (s110):
`P4TownLoop.Restore` re-runs Pendragon's entry when the save was made at the final screen.

## 2. What an ending does to the party (`_endset`) `[C]`

For every party member (slots 0 … party size − 1):

1. **Average level:** A = trunc(Σ level / party size + 0.5) (rounded), stored in `_par`+0B (the same byte the Mystic
   writes, `towns.md` §7).
2. **Score byte +09 = ending score + A** (byte; 225 + A wraps above 255).
3. HP = HP max, MP = MP max, **Charisma raised to at least 20**, condition 1 Okay, all wounds cleared, combat state
   recomputed.

Then the ending music and pictures (`END1` …), and the messages of §1.

**Consequences `[C]`:** record +09 is the "Score" shown on the character sheet (manual p. 29: "a number giving an overall
rating of what the character has achieved"; Figure 6 shows Score 0). No other writer was found (search for direct writes to
+09 in `_pmb` / `_gmb`; record copies move it along) `[~]`, so it is 0 until an ending; after any ending it is ≥ 125, and the training hall and the Magic Training Hall then answer
"There is nothing more I can teach" (`training.md` §1, §5: +09 ≥ 125). Charisma ≥ 20 makes training free in principle
(`training.md` §3), but the +09 gate stops training first.

## 3. Final screen (`_endstory(score)`) `[C]`

Window "Phantasie IV — THE GAME END":

- "Congratulations on completing the game. The story ends here."
- "Your score is **N**." — N = the ending score of §1 (125, 150, 175, 200 or 225; not the +09 bytes).
- "Your completion code is **ダック** (Duck)." — a **fixed word**, the same for every ending and party.
- "Fill in the postcard enclosed in the package and send it to us. Everyone will receive a certificate and a Gold
  Card." … "STARCRAFT Inc. 1990".
- "<<Save the game?>>" → Yes saves (`_disk(1)`), then back to the title (`_acgamend`).

s158 (`0xA9E6`–`0xAC30` read): lines 0–1 of the table at `0x40D1C`, the score (font 1, colour 8, `_wdouti(32, 3, n)`)
and the code, lines 2–7 (the postcard text, an empty line `0x41373`, "STARCRAFT Inc. 1990"), then **`_spc_wait`**
(`0xAB88`), line 8 "<<Save the game?>>" (`0x4139f`, `_font(1, 1, 2, 16)`), `_sdbeep` (`0xABFC`), `_confirm`; YES →
`_disk(1)`; then on either answer `_sdbeep` (`0xAC1A`) and `_acgamend(1)` (End game / Back to title, FORMAT.md 6.9).
**Built (s158):** `P4TownLoop.Ending` shows all six postcard lines (data `ending.postcard_lines`, "" = the empty line),
waits for SPACE, adds the question (`ending.save_question`) and sounds the first beep; the final screen's prompt is the
question (`P4EndingStep.Question`, panel `_confirm`), so a save made at it reloads into the ending as §1.1 says; Y / O
saves (the shell's save) and N / C does not, then `AnswerEnding` sounds the second beep and the Client opens
`_acgamend(1)` with the final screen's lines kept in the box (`parity/shots/p4-remaster-ending-save-question.png`,
`-ending-select-game.png`, taken before the box kept the final lines). `_xblack` before `_deadmsg`'s `_acgamend(1)` is not drawn `[~]`.

This matches the registration card (the "complete code shown on the final screen" and the score go on postcard ②).

## 4. Open `[?]`

- (s110 resolved: which events set f0 … f4, §1; what a save made at the final screen does, §1.1.)
- MSX2 / PC-98 completion code (only X68000 read).
