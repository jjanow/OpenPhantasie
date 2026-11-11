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

27. Done s193: the Guild treasury's LT / RT page the ledger (PgUp / PgDn; LB / RB stay save / load), D-pad Up / Down
    scroll as before. Typed names lacking punctuation in the wheel: accepted, as the audit suggested.

## E. Help lines that are wrong or keyboard-only for the pad

28. Done s193 (`P4DungeonScreen.Pad.cs` `PadHelp` / `PadS`): auto combat "A next round" (keys: "Space next round", the
    options help no longer shows there), the wiped party "A continue", MYSTIC SKIP "A skip the reading" (keys: "Space/Return
    skip the reading"), the treasury "Up/Down scroll  LT/RT page"; waits name "LB save" only where a save is taken
    (`CanSave`: not in a building, a menu or a fight; `p4.pad.help_wait_nosave` there). `.pad` twins for `p4.town.game_over`,
    both `combat_not_built`, the auto combat box line and System's choice list (no key letters). `p4.dungeon.help_move`
    names P. Tests in `P4PadTests`; none seen live.
29. Done s193 / s194 (`PadFixListETests`, `AmigaOrdersPadHintTests`): I Amiga map line shows a pad hint ("A fight  Start menu  RT+A commands
    R-stick <> party") in turn with the message (message 4 s, then the hint; back when the text changes); I Amiga
    combat field hint on the free top row (on the bar during orders if monsters reach it; not over pulled-down menus
    or Party Options); III Amiga field hint on the empty round-line row (not under Party Health); III Amiga outdoors
    names Start (`game.outside_bar_help`, `game.outside_bar_dungeon_help`); pad twins for `p1field.*`, `p3field.help`,
    `p3combat.rank_help`. s194: III Amiga's orders screen
    shows a short hint per step in the cells right of Fight! (x 80–198, rows 21–22; one row at y 177 under the spell
    window): "A no pick: Fight!", "L/R more RT cancel", rank / heal / message lines (`pad.p3orders.*`,
    `UiOverlays.OrdersPadHintRows`); seen live on Xvfb (member, order and spell steps).
30. Done s193: game menu "Save game LB / Load game RB" (import / export rows drop the F-keys); DOS orders' WHICH RANK /
    HEAL WHOM / 1 FRONT 2 MIDDLE 3 BACK end in "(D-pad, A)"; DOS III "[original text]" is original text, so its help
    says "D-pad pick  A take  B Two"; "B redo" reads "B game menu" on the first member, "B Two" on III's rank menu
    (keyboard help fixed there too: "Esc menu", "Arrows Enter pick  1 One  2 Two  Bksp redo"); "PRESS A",
    "INVENTORY (X)", "BESTIARY (Y)", town welcome lines, "Press X for the inventory", Update screen; sidebar move row
    "D-pad move  RT+A commands"; III Amiga Bank "D-pad pick"; fight boxes' "Press any key" → "Press A" (pad-only
    string: the original's line is file text). Skipped: other buttons still act on "Press any key" boxes (RB loads):
    behaviour, not wording. Lines built once (welcome, distribute, order prompts, "Press A") switch wording only when
    rebuilt.
31. Done s193: Options descriptions go through pad wording; WINDOW SCALE (Start+Back), TITLE SCREEN (any other
    button, B or Back skips all), AUTO-MAP (Back), COMBAT SPEED (holding LT).
32. Done s193: both say "D-pad".

## F. Cleanup

33. Done s194: the shell's `_p4.PadInput = false` and `PadKeys`' own set are gone; IV's flag is `PadWording.Note`'s
    alone, so a key wins a frame with both, as in I–III (`P4PadTests.PadInput_IsPadWordingActive_OneFlag`).

## G. The live virtual-pad pass (s194, Xvfb)

Seen working on screen: F33 (pad / key / left-stick-only switch the help, IV street and map), A1, A6, B7 / B8 (III
Withdraw wheel), C10, C11, C12, C13, C15 (III DOS and I town rows), C16, C17, C19 (spell step), C20, C21, C22, C23,
D24-26 (no art pack), E28 (auto combat, Mystic skip), E29 (III Amiga orders, member / order / spell steps). Fixed in the
pass: III Amiga's street help said "Back more" (Back toggles the auto-map there); it has its own help now
(`game.town_bar_help`, Start menus). Not reached: C14, C18's MESS outline and Guild Add move question, C19's rank /
heal steps, D27's paging (a one-page ledger), the right stick's prompt scroll, the II / III-only pad lines, IV's wiped
party / game-over lines, a key and a pad press in one frame (unit-tested).

Found in the pass, open (a fix found later = the audit missed it; cause where known):
34. IV: the Mystic's result wait and the Inn's "slept" wait say "LB save" and LB saves there: the help follows
    `CanSave` (`Phantasie4Campaign.cs:138`), and `InBuilding` (`P4TownLoop.Screens.cs:46`, `_screen is not null`) is
    false at those waits. Whether IV's original saves there is not read `[?]`; the outdoor "took them by surprise" wait
    before a fight says it too.
35. III DOS look: a fight opens on the Amiga-style field ("A opens options"); A then pulls down an all-black menu that
    blanks the monsters (`padpass/shots/p3d_f1c.png`, `p3d_f2top.png` in the s194 scratchpad). Not traced.
36. I Amiga orders window: its yellow outline draws over the game menu, the import path box and the file browser, and
    the fight's help line stays up under them; once it listed only one member of six. Not traced.
37. III Amiga share windows: the bottom row keeps the message ("You have: n gold", "[original text]"), not B9's
    pad help.
38. IV title `_disk(0)`: the pad help (`title.disk_help.pad`) is white on the light-grey window, flush with its
    right edge.
39. III field menu bar with no menu pulled down: the help names an open menu's buttons ("A take  B close").
40. Scrolled lists cut pictures: the fight's rank box (RT + right stick) splits a rank's header from its picture; the
    bestiary shows half pictures (against "fit or scroll, never crop").
41. Not pad: a III save whose `newGameRules` has `"modules": []` crashes the first Fight (`ArgumentOutOfRangeException:
    No enabled module offers this order`, `FeatureModules.cs:370`; `SessionSnapshot.cs:98` defaults `Modules` to empty).
    Reached with a hand-written settings file, not from Options. IV's found-item window said "Found Knife" while the
    log said "Found Medium Shield"; III's victory line shows "Victory ? Return" (a glyph the font lacks); IV's log said
    "The party greeted" before an auto combat. Each needs a look of its own.

## Accepted, not fixes

- No pad remapping (keys are rebindable in the settings JSON only; no UI for either).
- Import's browser can't reach an unmapped UNC share.

## Suggested order

A (done s187) → B 7 + 9 (done s188) → B 8 (done s189) → C 10-13 (done s190) → D 24-26 (done s191) → C 14-23 (done s192)
→ D 27 + E (done s193) → E29's III Amiga orders + F (done s194) → live virtual-pad pass on Xvfb (s194, section G) → G 34-41.
