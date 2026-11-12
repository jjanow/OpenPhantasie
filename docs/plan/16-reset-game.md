# Phase 16: Reset the game (P3 QoL command)

**Asked for (user s203, decided s204):** a per-game command (I–IV) that returns the world to a new game's state, with a
choice whether the characters stay. Purge = the original's own command; Keep = the remaster's addition.

Tags: `[C]` checked in the code, `[~]` probable, `[?]` unknown.

## 1. What the originals do

- **DOS I** (Utility → Reset the game, OV1 `0x4885`, `save-file-format.md` "Other writers") `[C]`: "CAUTION !!! / ALL DATA
  ON YOUR GAME DISK WILL BE RESET / Press R for reset or X to exit". It erases every character (`@92` / `@218` = 0 on
  roster records 0..24), sets the town to Pelnor, re-fogs OUT2–4, 6–16, 18, 19 and turns spent x8 fight cells back to x7,
  and zeroes SACK.DAT / DNG.SAV / DNGX.SAV. OUT1 / OUT5 (shipped unfogged, no fights) are not touched. TWNS.DAT is not
  rewritten by `0x4885` `[~]`.
- **The manual** (p. 19, BACKING UP CHARACTERS, key 5 "Starts a new game, from scratch"): "STARTING NEW GAME WILL ERASE
  OLD CHARACTERS OKAY? (Y/N)": Y erases the characters and resets the dungeons, "just as if you had bought a new game".
  The manual and DOS agree on characters and dungeons. Towns: `0x4885` doesn't rewrite TWNS.DAT (whether `0x4a4b`'s
  write-back does is not traced `[~]`); the remaster follows the manual and resets them. No `DIFFERENCES.md` entry.
- **II** (ST BACKUP UTILITY "new game", `UTILIT2.PRG`) not read `[?]`; **III / IV**: no such command found `[?]`.

## 2. Decisions (user s204)

| Question | Decision |
|---|---|
| Where | A RESET GAME row in each game's Options tab (I–IV), from the main menu only (as IMPORT GAMES: a running game may use the save) |
| What it acts on | One save: SAVE SLOTS off = the game's single save; on (and IV always) = a slot the player picks. Other slots and games untouched |
| Keep / Purge | Asked every time, in every game and preset (a command, not a rule) |
| Keep keeps | Everything on the characters: level, XP, HP / MP, spells, items, gold and bank. Parties are dissolved; everyone stands in the starting town's Guild |
| IV's Hall | Left alone in both modes (IV's descendant counts and III imports stay) |
| Played DOS I import | DOS's own rule re-fogs the regions and re-arms spent fights |
| Confirmation | The original's warning lines, then Keep / Purge, then YES / NO |

## 3. What a reset does

The save is read, a new game is built from GameData under **the save's own locked rules** (as F9 after a new game), and
the result is written back over the same file. A save that can't be read is refused; an empty slot offers nothing.

- **Purge:** the new game as is: an empty Guild, the start town, nothing else.
- **Keep (I–III):** the save is restored, its trip gold settled, then its roster comes over slot for slot (Guild
  numbers kept) by the transfers' path (`GameSession.ReplaceRoster`): everyone out of the party, parked parties (D-069)
  and the elemental gone, `CurrentTown` = the game's start town (I Pelnor, II / III `StartTownId`).
  - Trip gold (purse + sack gold): with the treasury on it is swept into the treasury first; with it off it goes into
    the party members' bank gold by the Bank's own deposit with one share each (`TownMenus.DepositShares`, DOS's
    remainder dropped).
  - The treasury itself is handed out as the transfers do (`TreasuryShares.Roster`: balance ÷ characters into each bank,
    remainder dropped); the new game starts with an empty treasury and its Guild sweep collects the shares again. So the
    balance comes back less the remainder, and the old ledger is gone.
  - The pending XP pool (`Sack.Experience`, paid on town entry) goes to the same takers, one share each, the remainder
    dropped. With nobody in the party, trip gold and XP go to every roster character instead.
  - The loot sack / stash (I–III's `Sack`) resets, as DOS zeroes SACK.DAT; carried and worn items stay.
  - Character-bound state stays with the character (I's magical-pool product `@90`, III's plane-town visits, aging) `[~]`.
- **Keep (IV):** the new game's roster is the save's roster with each party member's record written back to its Guild slot
  (members are copies); every record's town = Pendragon (`NewGameTown`). The purse goes into the members' accounts by
  IV's own deposit (`P4Town.Deposit`, one share each; no party: every roster record), the experience pool by
  `P4Town.ShareExperience` (one share each); the treasury is handed out as in I–III. The pack (`_par`+20) comes over:
  it is party storage (Distribute / Trade put items back with **P**, `towns.md`). Story flags reset, so a story item
  kept in the pack or on a member outlives its flags `[~]`.
- **Both:** the Hall (IV's `HallDescendants` / `HallImports`) is copied over unchanged.
- **I with DOS-sourced regions** (any data whose `overworld.json` is not the Amiga's own grids): after the build, DOS's
  rule is run on OUT2–4, 6–16, 18, 19: `b -= 120` if `b ≥ 120`; `b -= 1` if `b % 10 == 8`; `b += 120`. On pristine data
  this changes nothing (every such cell is fogged, no x8 ships). Amiga-only grids are from `maps.int` (never written) and
  hold the Amiga's own x8 cells (OUT9's 68s, OUT16 (4, 15) 88), so the rule is not run on them.

## 4. The screens

- **Options:** each game tab, after the presentation rows, has RESET GAME `...` (`options.reset_game`), from the main menu
  only. Picking it closes Options.
- **Which save:** with SAVE SLOTS on (IV: always) the slot picker opens in its Reset mode ("RESET GAME / WHICH I
  SAVE?"), every slot as Load lists them, an empty or unreadable slot refused. With it off the game's one save is taken;
  no save gives "THIS GAME HAS NO SAVE TO RESET."
- **Confirmation** (`ResetGameScreen`): "CAUTION !!!" and "ALL DATA IN SAVE n WILL BE RESET" (DOS I's lines), then KEEP
  THE CHARACTERS / ERASE THE CHARACTERS (K / E), then "RESET THE WORLD AND … THE CHARACTERS?" with NO first, YES (Y / N).
  Esc steps back: YES / NO → Keep / Erase → the slot picker when it came first (s205), else the main menu. The outcome
  line stays until a key; then the main menu.
- Keys, mouse (hover / click a row) and pad (D-pad, A choose, B back).

## 5. Built (s204)

Code: `GameSession.Reset.cs` (Core: `AdoptResetRoster`, `KeptRoster`, `ApplyDosResetFog`, `DosResetCells`),
`GameReset.cs` (Data: one save in, the reset game out to the same file; IV's side; `DosFogFor`, `AmigaOnlyP1Tables`),
`GameDataLocator.TablesVersion`, `ResetGameScreen.cs`, `Game1.Reset.cs`, `SaveSlotMode.Reset`, the Options row. Strings
`reset.*`, `options.reset_game*` (four lines that match DOS text have reworded fallbacks in I's `fallback.en.json`).
Tests: `GameResetTests` (Core: the cell rule, pristine data unchanged and OUT1 left alone, Purge, Keep with the treasury
off / on, the XP pool, no party, II / III Keep round trip and load, the file rewrite, IV Keep / Purge / file reload), `ResetGameScreenTests` (Client: the steps, keys, pad, clicks, fit, the
Options row from the main menu only, the picker's Reset mode), `SaveSlotRoutingTests` (Client, s205: the RESET GAME
row's route: the picker, "no save", the old / unreadable refusals, the one save's confirmation).

Live (s205, `parity/s205-reset-live.md`): II, III and IV Keep / Erase on Xvfb, the picker's Reset mode (IV), the no-save
line (III); Esc on Keep / Erase now steps back to the picker. Left: see `PLAN.md` P3 "Reset the game".
