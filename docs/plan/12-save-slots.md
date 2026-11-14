# Phase 12: Save slots (P3 QoL)

**Asked for (user, s81 in D3-028):** "modern save slots beside autosave". Each game gets
numbered save slots next to its autosave. Save and Load pick a slot from a list that shows each slot's party, place and
date. A slot that holds a save asks before it is overwritten. The main menu can load a save. It covers all four games,
since the end goal is one interface for the series. Off = the original single save, exactly as before. IV is the
exception: its original already has three slots, so off = those three on the original's own Save page (user s177:
"two states - the original three slot system and the new qol system", section 2a).

QoL, so off by default. No manual↔code conflict, so no new `DIFFERENCES*.md` entry; D3-028's Remaster bullet points
here.

Tags: `[C]` checked in the code, `[~]` probable, `[?]` unknown.

**Built:** all four games, Client and Core (section 8 lists what is left). Code: `SaveSlots.cs` (Data: slot files, the
newest save, `Describe`), `SaveSlotPreview.cs` (Core: what a slot's row tells), `SaveSlotScreen.cs` (the picker),
`Game1.SaveSlots.cs` (the shell's side). Tests: `SaveSlotsTests` (Core), `SaveSlotScreenTests` (Client).

---

## 1. The option: a per-game presentation switch, not a rules option

`SAVE SLOTS` (`GameSettings.SaveSlots`, `options.save_slots`) is an ON / OFF row in each game tab's presentation part,
after CONTINUE FROM A LIST. It is off by default and stored in `settings.json` under `games.<pN>.saveSlots`.

It is **not** a rules descriptor in the QUALITY OF LIFE section (the treasury's pattern, `11-guild-treasury.md`
sections 1 and 6), for these reasons:

- **Load must know it before any save is read.** Rules are locked per save and live inside the save file. F9 and the
  main menu have to decide whether to show the picker before a file is chosen, so no save's rules can decide it. This is
  the same reason CONTINUE FROM A LIST (D-069 QoL) is a presentation switch.
- **It is not a game rule.** It changes where files go, not how the game plays. A save made under any preset plays the
  same either way.
- **It can change at any time.** Turning it on in an Original-preset save makes sense, and a locked rule would forbid
  that.

So presets do not set it (presets cover rules only, `09-startup-options.md` section 3). Turning it on or off changes no
save file.

## 2. Slots and files

| Game | Slots | First slot (the single save of before) | Other slots |
|---|---|---|---|
| I, II, III | AUTO + 1–9 | AUTO = `savegame-pN.json` | `savegame-pN-1.json` … `-9.json` |
| IV | 1–3 (its original's three) | 1 = `savegame-p4.json` | `savegame-p4-2.json`, `-3.json` |

- **The single save becomes the game's first slot.** It keeps its file name, so with the switch off, or after turning it
  off, the game reads and writes exactly that file, as before. Nothing is migrated (no old-save compatibility).
- **IV has no autosave**, so its first slot is a numbered one. The original has three slots (manual p. 6 and p. 33:
  "Save 1", "Save 2" or "Save 3"; `_diskmenu`'s `*1:Save`–`*3:Save`, `FORMAT.md` 6.10) `[C]`. **IV always has those
  three files, whichever way the switch is** (`SaveSlots.Slotted`); the switch picks only the screen (section 2a).

## 2a. IV with the switch off: the original's three slots (user s177)

The user's call (s177): two states, a toggle. Off = the original three-slot system, on = the QoL picker.

- **Save** (F5, System ▸ S, the final screen's YES) opens `_diskmenu(1)` as P4.X draws it (`0x1AB4`, `_disk(1)`
  `0x1C7E`) `[C]`: window 34 titled "Select File", " SPACE" (table entry 1, icon 20) at icon place 0, then all three
  "k:Save" icons (icon 1) at places 1–3 with '*' (saved: the slot's file exists) or ' ' written over the label's
  first character, and "[original text]" ("* = saved") centred on x 68 at y 308 (`_wkprcen(34, 68, 308, …)`). A slot's
  key or icon saves there at once (no question over a saved slot, as `_disk` takes one `_mgetc`); SPACE goes back.
  After either, System ▸ S goes back to the System icons (`_syscon`'s loop), F5 to the place, the final screen to
  `_acgamend(1)`.
- **Load in a running game (F9)** is the remaster's: the original loads only from the title (`_disk(0)`). It opens the
  same window with SPACE and the slots that hold a save at the next places (as `_disk(0)` places them); with none, "No
  IV save to load." It also works over a fight or a party menu, where the slots show as text lines `[~]`.
- **The title's `_disk(0)`** shows each slot that holds a save, as the original does.
- **Without the pack's panel** (or in a fight) the page lists the slots as text lines.
- Data: the panels file's `"disk"` gained `"space"` and `"caption"` (`x68k_p4_panels.py`, `P4Panels.cs`).
  Code: `P4DungeonScreen.System.cs` (`P4SystemPage.Save` / `Load`, `OriginalSlots`, `DiskPanel`, `DiskLabel`),
  `P4ScreenRequest.SaveSlot` / `LoadSlot`. Tests: `P4OriginalSaveSlotsTests`.
- Files sit in the remaster's data folder (`ClientSettings.DataDirectory`). `SaveSlots` takes the folder as a
  parameter, so tests use a temporary folder.

## 3. Save, Load and the autosave

With the switch **on**:

- **Save** opens the picker on the numbered slots (I–III 1–9, IV 1–3). AUTO is not offered: only the autosave writes it.
  The picker opens from:
  - F5, the game menu's Save and a town's Save game (I–III);
  - III's town bar "save", after III's own "Are you sure …?" question, which stays (DOS III, s160);
  - IV's F5, System ▸ S and the ending's save question. IV's refusals (in battle, mid-event, inside a building) still
    come first.
- **Load** opens the picker on every slot (AUTO first) from F9, the game menu's Load and a town's Load (III asks its
  question first).
  - I–III: picking a slot goes on as Load always did. D-069's Guild-number question (or CONTINUE FROM A LIST's party
    list) is asked, and `p1.continue = dos` loads the saved party without asking.
  - IV: the save is restored onto the running screen.
- **The autosave on leaving town** writes the AUTO slot and never asks:
  - I and II's town "Auto save" toggle;
  - II's `p2.autosave = always` (D2-016);
  - III's manual rule (D3-028).

  This is the same file and the same behaviour as before.
- **Other games' reads** use the newest file among that game's slots when that game's switch is on, else its single
  save (`ClientSettings.ReadSavePathFor`, `SaveSlots.ReadPath`). These reads are the I ↔ II and I / II → III transfers,
  the lineage Hall and IV's Hall transfer.

With the switch **off**, every path above uses the single save, exactly as before.

## 4. The picker (`SaveSlotScreen`)

A full-screen remaster menu (`UiOverlays.DrawMenuScreen`), the same in every game and look, and over the main menu.

```
            LOAD GAME
 PHANTASIE I
  AUTO (EMPTY)
 >1    SWAT +5        PELNOR       11-09 17:53
  2    (EMPTY)
  …

 SWAT ARENTINO LEFTY FRANCIS FOLNAR ZANDOR
 PELNOR  +1 OTHER PARTY
 2026-11-09 17:53
 Up/Down or 1-9 pick  Enter choose  Esc back
```

- **A row:** the slot (AUTO or its number), then the party's leader and size, where it is, and the file's date
  (`saveslots.row_date`).
  - The party comes from `SaveSlotPreview`. I–III use the active party, or the first parked one when the active party
    is empty (D-069's `SeveralParties.Parties`). A fight counts as the place it is fought in.
  - The place is the town's name when the game's names are at hand, else TOWN n; WILDERNESS; or DUNGEON n.
- **Detail under the list:** every name, the place, how many other parties the save keeps (I–III), and the full date
  (`saveslots.long_date`).
- **Rows that can't be loaded:** an empty slot shows (EMPTY), and a file that can't be read shows (CAN'T BE READ). A save
  from an older remaster version shows (OLDER VERSION). Load refuses all three with a line in the detail area. Save
  writes an empty slot at once and asks `OVERWRITE SLOT n? (Y/N)` over any other.
- **Where the cursor starts:** on the newest save. Save starts on the first empty slot, else the newest.
- **Keys:**
  - Up / Down (wrapping), Home / PgUp, End / PgDn;
  - a digit picks that slot, A picks AUTO;
  - Return chooses, Esc goes back. Esc on Save says "Not saved."
- **Mouse:** hovering selects a row, a click chooses it.
- **Pad:** the D-pad moves, A chooses (and answers yes), B / Back go back.
- **Fit or scroll:** a row or detail line wider than the screen goes on, whole, on the next line. When the rows don't fit,
  the list scrolls with the cursor and ends in "...". Nothing is cut.
- **Text:** `saveslots.*` and `options.save_slots(.desc)` in `GameData/shared/strings/en.json`. It is our own text
  (`build_string_sources.py` `OURS`), and the date formats are strings too.

## 5. The main menu

With the switch on and a save in any of the game's slots, picking the game on the main menu, after its title splash,
opens the picker in **Start** mode. NEW GAME is the first row (key N) and the slots follow.

- **I–III:** a slot builds the game (as Play does) and then loads it, as F9 would.
- **IV with its title screen on:** the original's start menu (`_disk(0)`) already shows a `*k:Save` icon for each slot
  that holds a save, either way the switch is. With the title screen off, the picker opens instead (switch off: a new
  game, and F9 loads).

With the switch off, or with no save, picking a game starts a new one, as before.

## 6. Options screen

```
 CONTINUE FROM A LIST OFF
 SAVE SLOTS          OFF        ← every game tab
 NEW INVENTORY SCREEN OFF
```

The row's description (`options.save_slots.desc`) gives the slot counts, says that OFF keeps the one save in I–III, and
that IV always has three slots (ON in the list, OFF on the original's Save screen).

## 7. Saves

- No save format changes. A slot file is an ordinary remaster save: the session codec in I–III, `Phantasie4SaveCodec`
  in IV.
- The date shown is the file's last write time.

## 8. Built: what is left

- **The picker is the remaster's own screen in every look** (switch on). IV with the switch off uses the original's
  Save page instead (section 2a).
- **IV's "Now Saving..."** (`_save`'s window) is not drawn: the note says "Saved in slot k." `[~]`.
- **Other games' reads** take the newest slot of the source game. A transfer's file step starts there; another slot
  is reached by typing its path or by the step's browser (Tab / pad Y), not by a slot picker. IV's Hall reads the
  newest slot with no choice `[~]`.
- **Main menu load in I–III** builds a new game first and then loads, as F9 after Play does. Esc in D-069's
  Guild-number question leaves the player in that new game `[~]`.
- **IV's town names** need the running campaign's tables. In the picker over the main menu (IV with its title screen
  off) a town shows as TOWN n `[~]`.
- **Unit-tested:** `Game1`'s routing decisions sit in `SaveSlotRouting` (the slots each mode lists, Save / Load
  with the switch on or off, the main menu's Start, the picker's outcome in every mode for I–III and IV, in game and over
  the main menu, Esc on Save, and the RESET GAME row); `SaveSlotRoutingTests`. `Game1` itself (the I/O each outcome
  runs) is not built in tests (it needs a graphics device).
  - Checked live on Xvfb: I's F5 / overwrite question / F9 into D-069's question, the main menu's Start screen
    (scrolled list), and IV's F5 / F9 with Pendragon named; IV with the switch off: F5's Save page, a save to slot 2
    (then "*2:Save"), F9's load page and the load. The s179 pass covered I–IV (keys, mouse, Esc; IV `_diskmenu(1)`,
    the title's `_disk(0)` with slots 2 / 3); the virtual pad in s181 (LB save, RB load, D-pad, A, B).
