# Pad fix list (s186 code audit)

s186 replaced the play-found "Pad leftovers" with a code audit. Four read-only passes covered every input handler in
`src/OpenPhantasie.Client` (I–III gameplay, modal dialogs, IV, front end + global keys) and found ~125 raw findings. They
are merged below by root cause into ~45 fixes. The audit
is the complete list as of s186: a fix found later in play means the audit missed it; add it here, with the cause.

Pad today (for reference): D-pad / left stick = arrows, A = Return, B = Esc, LB / RB = Save / Load (PgUp / PgDn in
menus), X inventory, Y bestiary, Back auto-map, L3 / R3 turn, right stick sideways = F2, up / down = sidebar / prompt
scroll, LT fast-forward, RT + A = field command list, A in the Guild = Guild command list. IV maps its own
(`P4DungeonScreen.Pad.cs`: Y / X step clickable icons, Start = Esc, B = N / Space / Backspace by context).

Paths are relative to `src/OpenPhantasie.Client/` unless stated.

## A. Bugs that hit every input (fix first)

1. **IV street S icon saves instead of opening System** under Common Town Keys: the icon sends a plain `S`, which the
   common-keys check turns into F5 (`P4DungeonScreen.cs:161-167`, `Panel.cs:163-185`). System can't be opened on the
   street at all then (no key either). The test checks only keyboard S. Fix: the icon sends System itself; give System
   a key / pad button under common keys.
2. **LB + RB (Guild depart chord) also fires Save and Load** the same frame (`InputMap.cs:205-213`,
   `Game1.cs:2178, 2180, 2535`): I / II save, reload, then toggle the Guild; III opens only the save question. Fix:
   drop the chord (the town bar's Guild row and B cover it) or suppress Save / Load while both shoulders are held.
3. **I Amiga field Party Options: Esc / B with a menu pulled down opens the game menu**; the Escape case in
   `HandleFieldMenuKey` is unreachable (`CombatPresenter.cs:620-622, 2088-2092`). Add `P1FieldMenuShown` to the Esc case.
4. Keyboard wording: `towndlg.type_amount_back_clear_digit_enter` ("…Enter withdraw") is reused on both deposit fields
   (`P3TownDialogs.cs:19, 296`); `game.keep_window_ok_cancel` promises Y / N that `AmigaBankDialog.HandleKeys` ignores.
5. Keyboard help names things not on screen: the map-only dungeon's `game.3_n_e_s_w_arrows_2` names "Tab auto-map
   … [ ] party scroll" with no sidebar / overview (`Game1.cs:3523-3536`, add a `game.map_only_dungeon_help` + `.pad` as
   the overworld has); "F2 map" is named for packs without `map_layout` (III, II Apple, I CGA), where F2 does nothing
   (`Game1.cs:3467, 3518`). Guild / Title hint "A add, N new, G leave" is wrong in Title (`UiOverlays.cs:607`).
6. Mouse-only, no key either: the Guild / Title member list scroll (`UiOverlays.cs:641-650`), IV's message / party box
   scroll (`P4DungeonScreen.cs:1223-1257`; IV's update returns before the shared sidebar scroll), I Amiga's MESS window
   early end by clicking a row (`Game1.MapMessage.cs:122`; Amiga-faithful, may stay).

## B. The digit wheel (one root cause behind ~30 dialog findings)

7. **Make the wheel opt-in.** Every modal step that is not `IPadArrows` gets the digit wheel (`DialogPad.cs:107-117`),
   even list and any-key steps: Left / Right send nothing, X types an ignored digit, Y = Backspace (closes most dialogs),
   and the help flips to the generic `pad.dialog_wheel_x` "X type  Y del  A ok  B back". Fix: only steps that read
   digits / letters turn the wheel on (an interface flag), and its range follows the step (1-2, 1-5, 1-6, not 0-9).
   Fixes the help on: game menu, pad command list, Guild Add / Purge / Rename / Examine / List, New's ancestor lists,
   Inspect summary, Health, Continue list and notice, Treasury, Withdraw member picker.
8. **Per-step wheel help templates** in place of the generic line (each `.pad` / `.wheel` id, `{0}` = current pick),
   naming what X / A / Y / B really do there: Cast (3 steps; also lost "Up/Down more"), Use potion, Time lag, Drop,
   Spells / Training "who", III training's skill step (show the skill name), Learn spell, New's lineage question and race /
   class, Name / Rename ("B cancel" in New), Inspect "whom", Distribute (7 sell), Withdraw amount (no "B back" outside III
   Amiga), III DOS Deposit and Amiga "How much" (add "B cancel"), III Trade item step ("B back", not "B done"), Trade
   to whom. Also `DialogPad.StepChanged` resets only on a help-text change: Cast / Potion / Use item reuse one text.
9. **Town picture hides the pad help.** It shows help only when `PadWording.Shows` matches (`Game1.cs:1643-1647`,
   `PadWording.cs:65`); the wheel line, `pad.dialog_yes_no` and `game.x_x_shares` (no twin; `Game1.cs:2524`) never match,
   and `PadWording.F` / `S` don't set `_formatted`. Every modal in town loses its help after the first Left / Right, and
   the wheel's current character is shown nowhere (the typing grid draws for the mouse only, `Game1.cs` ~1911). Fix:
   show the band help whenever `PadWording.Active`, or make `F` / `S` / the wheel line record themselves for `Shows`.

## C. No pad route (I–III, front end)

10. **DOS-save folder and transfer file typing** (`DosSaveDialog.cs:101-167, 210-215`): pad A takes only the path
    shown (last folder / newest slot). Older slots, outside files and other folders are unreachable; the mouse can't
    either. Fix: Import's folder browser on Y (a letter wheel can't make `/ . - _ ~ :`); for transfers, or a pick list of
    the source game's slots. Then reword `dossave.type_folder_…pad` / `transfer.type_file_…pad`.
11. **III Amiga Bank deposit share window: CANCEL** (`Game1.cs:2336-2345`, `P3TownDialogs.cs:430-435`): BankShares has
    no `EscapeChoice`, so B opens the game menu. Give it one (B = C) and add "B cancel" to the twin.
12. **Combat log and monster-rank box scroll** (`CombatPresenter.cs:1052-1061`): PgUp / PgDn / Home / End / wheel only;
    the right stick is unused in fights. Right stick up / down = log, RT + right stick = rank box.
13. **Left stick on the map, old inventory overlay and bestiary**, and **diagonal steps** with DIAGONAL MOVES on:
    `PressedMove` reads `pad.DPad` only (`InputMap.cs:145-175`). Read `_stickPush` there, eight-way in the field when the
    option is on; update `options.diagonal_moves.desc` and the SidebarScroll doc.
14. **Modern inventory's detail box scroll** (`[` / `]`, `InventoryScreen.cs:400`): no button. Right stick left / right;
    name both right-stick scrolls in `inventory.help_pad`.
15. **Distribute without resting** (`TownMenu.cs:233`): the DOS-row town bar's D has no pad row; the pad reaches
    Distribute only via an Inn rest (ages the party). Add it to a town pad command list or a pad-only Option row.
16. **III Amiga Guild: Info (`?` / `/`)** has no pad route in the Guild (the street's bar has File › Info). Add Info to
    the Guild's pad command list when `TownInfoTakesInput`.
17. **I Amiga orders list: no FIGHT pick** on the D-pad (`CombatPresenter.Pad.cs:173-226`); after touching the D-pad, A
    must pick a member. Append a FIGHT entry as III's `AmigaPadStep` does.
18. **Blind pad picks** (cursor moves but nothing is drawn): I Amiga requester 14's page bar (`Game1.cs:1958`), I Amiga
    Guild Add's "not in the town" requester 0 (A can take a remembered NO; make it `IPadYesNo`), I Amiga dungeon MESS
    menu (`PadRequesterMark` not applied to `P1MapMenuFor`, `Game1.cs:1851-1867`), III's "Press a number" event page
    (the pick shows only on the help line, which `DrawScrollFrame` covers; draw it on the parchment).
19. **DOS-style orders: A with no pick does nothing** on Rank / PartyRank / HealTarget / SpellNumber
    (`CombatPad.cs:63-69`); default the pick to 1 or say "D-pad pick, A choose".
20. **IV title `_disk(0)`**: RB loads only the first saved slot, slots 2-3 unreachable; no help line at all; mashing A
    through the pictures picks New Game at once (`Game1.cs:1143-1159`). Y / X or D-pad over the icons + A; a help line;
    ignore A for ~0.5 s on arrival.
21. **Title splash: B advances one picture**, only Back skips all, unsaid (`TitleSplash.cs:60-76`). Make B skip all too.
22. **F11 scale** has no button (the Options row WINDOW SCALE is the pad's route). Low; if wanted, a chord such as
    Start + Back. Then fix `game.f11_scale_esc_menu.pad`.
23. Small: save-slot picker LB / RB = first / last row (`SaveSlotScreen.cs:150-181`); Treasury Home / End
    (`TreasuryDialog.cs:59`); II ST: pad steps and A make no ST click and don't cut delayed cues
    (`Game1.cs:1129, 3205-3211`).

## D. No pad route (IV)

24. **IV without the art pack: a command pick list** (one shared fix): the street's T / D / F / C / U / S
    (`Services.cs:128-141`), outdoors' M / F / P / C / U / S (`P4DungeonScreen.cs:269-280`), the location map's E /
    F / P / C / U / S (`:396-408`). Y / X have no icons to step there. E.g. Back opens a pick list.
25. **IV System / Select Game / Save / Load pages without icons** (no art pack, or the Load page over a fight / party
    menu even with it): give the text lines a cursor (Up / Down + A sends the line's key) on System (S / E / A),
    Select Game (E / T; T has no route at all on the final page), and the 1-3 slot lines (`System.cs:80, 165-265`).
    With SAVE SLOTS off (IV's default) LB / RB and the ending's save question open these dead ends today.
26. **IV fight: Q auto move** has no pad route (`Battle.cs:151-157`, `Pad.cs:537`): add it to Y / X's order cycle.
27. IV small: Guild treasury PgUp / PgDn (`P4DungeonScreen.cs:332-340`); typed names lack punctuation in the wheel
    (accept unless needed).

## E. Help lines that are wrong or keyboard-only for the pad

28. IV: `help_system` / `help_buttons` say "Y/X pick a button" with no buttons (System, Select Game, slot pages
    without icons); `help_move.plain` / `help_street.plain` / `help_left` name LB / RB, which open those dead-end pages
    (until 25); auto-combat rounds and the wiped party need "A next round" / "A continue"; the treasury wait omits
    "Up/Down scroll"; MYSTIC SKIP has no "A skip" (keyboard none either); `help_orders_x` lacks auto move (after 26);
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
    AUTO-MAP (Tab → Back), COMBAT SPEED (Shift → LT), DIAGONAL MOVES.
32. Wording: `import.help.path.pad`, `dossave.up_down_scroll_enter_close.pad` say "Up/Down" where others say "D-pad".

## F. Cleanup

33. `Game1.cs:1184-1185` `_p4.PadInput = false` is redundant with `PadWording.Note`; IV's `PadKeys`
    (`P4DungeonScreen.Pad.cs:281`) sets it true on a frame with both a key and a pad press (I–III: keyboard wins).
    Delete the lines and follow `Note`'s rule.

## Accepted, not fixes

- No pad remapping (keys are rebindable in the settings JSON only; no UI for either).
- Import's browser can't reach an unmapped UNC share.
- Not seen live (from the old leftovers): the right stick's prompt scroll, the II / III-only pad lines, III Amiga's
  share window. Check them during the live pass after the fixes.

## Suggested order

A (all inputs) → B 7 + 9 (one change each, clears most dialog rows) → B 8 → C 10-13, D 24-26 → the rest → one live
virtual-pad pass on Xvfb.
