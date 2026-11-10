# Pad fix list (s186 code audit)

s186 replaced the play-found "Pad leftovers" with a code audit. Four read-only passes covered every input handler in
`src/OpenPhantasie.Client` (I–III gameplay, modal dialogs, IV, front end + global keys) and found ~125 raw findings. They
are merged below by root cause into ~45 fixes. The audit
is the complete list as of s186: a fix found later in play means the audit missed it; add it here, with the cause.

Pad today (for reference): D-pad / left stick = arrows, A = Return, B = Esc, LB / RB = Save / Load (PgUp / PgDn in
menus), X inventory, Y bestiary, Back auto-map (on I–III's town row: the town command list, s192), Start + Back scale,
L3 / R3 turn, right stick sideways = F2, up / down = sidebar / prompt
scroll (in a fight the log, RT + stick the rank box), LT fast-forward, RT + A = field command list, A in the Guild = Guild command list. IV maps its own
(`P4DungeonScreen.Pad.cs`: Y / X step clickable icons, Start = Esc, B = N / Space / Backspace by context).

Paths are relative to `src/OpenPhantasie.Client/` unless stated.

## A. Bugs that hit every input: done s187

1. IV street S icon under Common Town Keys: the icon (click, pad Y / X + A) now presses F10, which opens System on the
   street there (`P4DungeonScreen.SystemKeyCommon`); help `p4.town.help_menu_common` names "F10 system".
2. The LB + RB Guild-depart chord is gone (`InputMap.PadHeld`); the Guild's A command list and the town bar cover it.
3. I Amiga field Party Options: Esc / B with a menu pulled down closes the menu (`Game1` Esc route takes
   `P1FieldMenuShown`).
4. III deposit fields say "Enter deposit" (`towndlg.type_amount_deposit`); III Amiga Bank's list step has its own line
   (`p3town.bank_window_help`, no Y / N).
5. Field help without "F2 map" when the pack has no map layout (`*_nomap` ids, `Game1.MapHelpId`); the map-only dungeon
   shows `game.map_only_help` (`Game1.FieldBandHelp`); the Title screen's Guild hint is `ui.title_guild_hint` (A / D).
6. The Guild / Title member list scrolls by [ ] / the right stick; IV's party lines by [ ] / the right stick and its box
   by , . / RT + the right stick (`SidebarScroll.Apply` in Game1). Kept: I Amiga's MESS early end by a click on a row
   (Amiga-faithful). Not seen live yet (one pad pass at the end of the list).

## B. The digit wheel (one root cause behind ~30 dialog findings)

B7 and B9 done s188, B8 s189 (none seen live):
- **B7.** The wheel is opt-in. Only a step that picks by digit with no D-pad cursor, or that types, turns it on
  (`IPadWheel.PadWheel(session)`, else `ITypedEntry`; `Game1.ModalWheel`). Its range follows the step
  (`DialogPad.Picks`: 1-2, 1-5, 1-6…). Wheel steps: Cast (who / spell page / heal whom), Use potion (3), Time lag's
  0-9, Withdraw / III DOS Deposit / III Amiga "How much" amounts, Learn spell's page, Guild Drop, Spells / Training
  "who", III training's skills, New's kind / race / class / name, Inspect "whom", Use item (item / target / option).
  Every other step keeps D-pad Left / Right, X / Y send nothing, and its own help shows. Trade and Distribute have a
  cursor (SELL / sale rows included), so they get no wheel.
- **B9.** The town picture's band shows the help whenever the pad is the input (`Game1` town HUD), so the wheel line,
  its character, `pad.dialog_yes_no` and `game.x_x_shares` show there. Side effect for group E: a help with no `.pad`
  twin now shows there in keyboard words (before: nothing).
- **B8.** Each wheel step's help has a `.wheel` twin (`<help id>.wheel`, `DialogPad.WheelSuffix` / `WheelTextFor`):
  a template whose `{0}` is the wheel's pick with what it picks (`IPadWheel.PadPickName`: the member, spell, potion,
  item, option, race, class, kind or III skill), naming what X / Y / A / B do on that step. It shows as soon as the pad
  is the input (no wait for the wheel's first use) and replaces the generic `pad.dialog_wheel_x` there (which stays for
  Continue's Guild number). Rename got its own help (`member.type_name_enter_accept_esc_back`, B back); Withdraw's
  amount names no B (DOS ignores Esc there). A new step (its help, or `IPadWheel.PadStep` where Cast / Use potion / Use
  item reuse one text) starts the wheel again at 1.

## C. No pad route (I–III, front end)

10-13 done s190: Y (Tab) on the DOS save / transfer path opens Import's browser (`FileBrowser.cs`, shared); B / Esc
cancels III Amiga's share window; the right stick scrolls a fight's log, RT + right stick its rank box (not left / right:
that is the pad's F2, `RightStickSideways`; on the two steps where RT is CANCEL / Thrust its press still sends that);
the left stick steps the map, the old inventory overlay, the bestiary and the Armory list (`PressedMove`), on a slant
with DIAGONAL MOVES on (an eight-way quantiser beside the menus' four-way one). Routing in `Game1` (share window, fight)
not unit-tested; none seen live.

14-23 done s192 (none seen live; `PadFixListC2Tests`):
- 14: the right stick left / right scrolls the modern inventory's detail box (`[` / `]`); `inventory.help_pad(_town)`
  say "RS scroll".
- 15: Back on I–III's DOS town row (on the bar, no door flash; not III Amiga's street bar, whose Options menu has it)
  opens the pad command list with the manual's town letters: Distribute, Use item, Inspect, Trade (where the rules have
  it), Save game (`PadCommandDialog.EntriesFor(Town)`, `Game1.TownPadCommandsAsked`). Back there no longer toggles the
  auto-map (it still does everywhere else). Help `game.arrows_enter_a_b_m_n.pad` says "Back more".
- 16: III Amiga's Guild command list (A) has Info when the town takes ? (`TownInfoTakesInput`); the pick opens the
  credits itself (`InjectPadCommand`), as the bars read their keys before an injected key.
- 17: I Amiga's orders list ends on FIGHT (sends F, gadget 1), as III Amiga's Fight!.
- 18: the MESS menu outlines the pad's answer (`DrawP1MapMenu` `padCursor`, `Game1.PadPromptCursor`); I Amiga's sheet
  (requester 14) outlines the page button A takes while the pad is the input (`InspectDialog.SheetCursor`); Guild Add's
  move question as requester 0 is a Y / N step for the pad (`GuildAddDialog.P1Requester` / `AsksYesNo`: A yes, X no,
  B cancel, help `guild.add_move_y_n`); III's "Press a number" page shows the pad's pick in its footer
  (`pad.event_page_pick_x_x`, moved left to stay on the parchment).
- 19: on the DOS order steps that read only a digit (Rank, PartyRank, HealTarget, SpellNumber) A with no pick takes the
  first pick (`CombatPadStep.AFirstPick`); the order menu keeps "A unpicked keeps the order". The help shows no pick
  until the D-pad moves (A's default is unsaid).
- 20: IV title `_disk(0)`: the D-pad / left stick light an icon (outlined, wrapping), A takes it (New Game while none is
  lit), not in the menu's first 0.5 s (`TitleSplash.PadStep` / `PadChoose` / `MenuSettled`); RB still loads the first
  save; a remaster help line under the window while the pad is the input (`title.disk_help.pad`; keys see the original's
  bare window).
- 21: B skips the whole title splash, as Back and Esc.
- 22: Start held + Back = F11's scale (`InputMap.PadHeld`; Back alone is the auto-map only without Start);
  `game.f11_scale_esc_menu.pad` names it. Order matters: Back first toggles the auto-map, then Start scales; where Start
  has its own action (I Amiga field menus, III bar Options) that press goes there first `[~]`.
- 23: the save-slot picker's LB / RB = first / last row; the Treasury's LT / RT = top / foot of the ledger
  (`TreasuryDialog.PadEndKeys`, help in D-pad words); II ST: a pad step or A in the field clicks as its key does
  (`Game1.FieldPadClicks`; not RT + A), and any pad button cuts the delayed cues.

## D. No pad route (IV)

24-26 done s191 (`P4DungeonScreen.Pad.cs`, `.System.cs`): with no icons for Y / X (no art pack), Back opens a
command list on the street (T / D / F / C / U / S, S = F10 under COMMON TOWN KEYS), outdoors (M / F / P / C / U / S)
and a location map (E / F / P / C / U / S): D-pad pick, A sends the key, B / Back close (`PadCommandEntries`). The
System, Select Game and save / load pages without a panel (no pack, or the load page over a fight / party menu) list
their choices under a cursor (`SystemChoices`; keyboard Up / Down / Return too), so T, the slots and SPACE are reachable.
Q auto move is the last step of Y / X's order cycle (`PadAutoMove`, bracketed in the orders line). Tests in
`P4PadTests`; none seen live.

27. IV small: Guild treasury PgUp / PgDn (`P4DungeonScreen.cs:332-340`); typed names lack punctuation in the wheel
    (accept unless needed).

## E. Help lines that are wrong or keyboard-only for the pad

28. IV: auto-combat rounds and the wiped party need "A next round" / "A continue"; the treasury wait omits
    "Up/Down scroll"; MYSTIC SKIP has no "A skip" (keyboard none either);
    waits omit "LB save"; box texts `p4.town.game_over`, `combat_not_built`, System's key list have no pad twins.
    Keyboard: `p4.dungeon.help_move` omits P.
29. I–III hidden pad help: I Amiga map-only screens blank the help, so the pad never learns A fight / RT+A / Start
    menu / Back / right stick (`Game1.cs:1624-1634`); III Amiga field help never mentions Start; I / III Amiga field,
    orders and spell windows fill the screen over the help (`Game1.cs:1671, 1699-1704`), so RT = CANCEL, Left/Right =
    MORE, A = FIGHT are never shown. Fix: a one-row pad hint on those screens while `PadWording.Active`.
30. Labels / lines in keyboard words with no `.pad` twin: game menu rows "Save game F5 / Load F9 / Import F7 /
    Export F6" (`GameMenuDialog.cs:47-50`); DOS orders' "WHICH RANK? 1-n", "HEAL WHOM?", "1 FRONT 2 MIDDLE 3 BACK",
    "[original text]" (`CombatPresenter.cs:1724-1729`); DOS orders' "B redo" (B is the game menu on the first member,
    "Two" on III's rank menu); "PRESS ENTER" (`UiOverlays.cs:1504`); "INVENTORY (V)" / "BESTIARY (B)"; the band's "G
    enters the Guild" / "Press V for the inventory" (`Game1.cs:452, 2934`); "Press any key" boxes (other buttons do their
    global action there; RB loads); the field sidebar's command list (add "RT+A commands"); Update screen body "Press
    Enter to restart" (and B restarts there too); III Amiga Bank pick step's twin lacks "D-pad pick".
31. Options descriptions can't take pad wording at all: `Detail()` draws `UiText.S(desc)` raw
    (`OptionsScreen.cs:447`); route it through `PadWording.S`, then twins for WINDOW SCALE (F11), TITLE SCREEN (Esc),
    AUTO-MAP (Tab → Back), COMBAT SPEED (Shift → LT). (DIAGONAL MOVES names the stick in its own text since s190.)
32. Wording: `import.help.path.pad`, `dossave.up_down_scroll_enter_close.pad` say "Up/Down" where others say "D-pad".

## F. Cleanup

33. `Game1.cs:1184-1185` `_p4.PadInput = false` is redundant with `PadWording.Note`; IV's `PadKeys`
    (`P4DungeonScreen.Pad.cs:281`) sets it true on a frame with both a key and a pad press (I–III: keyboard wins).
    Delete the lines and follow `Note`'s rule.

## Accepted, not fixes

- No pad remapping (keys are rebindable in the settings JSON only; no UI for either).
- Import's browser can't reach an unmapped UNC share.
- Not seen live (from the old leftovers): the right stick's prompt scroll, the II / III-only pad lines, III Amiga's
  share window (B cancel since s190). Check them during the live pass after the fixes.

## Suggested order

A (done s187) → B 7 + 9 (done s188) → B 8 (done s189) → C 10-13 (done s190) → D 24-26 (done s191) → C 14-23 (done s192)
→ the rest (D 27, E, F) → one live virtual-pad pass on Xvfb.
