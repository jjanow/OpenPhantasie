# Unified town menu (P3 QoL)

## Sources and command inventory

Read manuals before code: I, `Phantasie_I_1986_SSI_manual_text.pdf`, printed pp. 3–7 (Town Menu,
Guild, Bank, Armory and Other Options); II, `phantasie2-manual.pdf`, General Rules Town Menu
and Atari ST supplement; III, `phantasie3-manual.pdf`, Town Menu / Other Options and the
Apple keyboard summary (printed p. 21); IV, `phantasie4-manual-en.md`, printed pp. 7–9,
23–30. Existing manual/code decisions remain in `DIFFERENCES*.md`.

| Command / procedure | I | II (ST) | III | IV (X68000) | Unified location |
|---|---|---|---|---|---|
| Guild / create, roster and party management | Guild | Guild | Guild | Guild roster, form party, delete, rename, profile | Town: Guild |
| Buy equipment / choose shopper | Armory | Armory | Armory | Armory | Town: Armory |
| Deposit / withdraw | Bank | Bank | Bank (rules select shares or DOS all/some) | Bank | Town: Bank |
| Rest | Inn | Inn | Inn | Inn | Town: Inn |
| Score / advice | Mystic | Mystic | Mystic | Mystic | Town: Mystic |
| Level and skill training | Guild: Training | Guild: Training | Guild: Training | Guild: Training Hall | Town: Training |
| Learn spells | Guild: Spells | Guild: Spells | Guild: Spells | Guild: Magic Training Hall | Town: Learn spells |
| History, Hall characters, ancestors, transfers | — | — | — | Hall of Glory (Pendragon) | Town: Hall of Glory |
| Buy ship | — | — | — | Shipwright (coastal towns) | Town: Shipwright |
| Leave town | Road / gate | Road / gate | Leave | Town exit | Town: Leave town |
| Inspect / profile | Inspect / Show party | Inspect / Show party | Inspect / View Party | Profile, strategy, spells | Party: Inspect |
| Exchange items without pooling all gear | Modern inventory option | Modern inventory option | Trade (manual / give-only; absent in DOS) | Trade | Party: Trade items |
| Distribute and sell | D / Inn | D / Inn | D (Amiga option, DOS entry procedure) | D (found-item share-out) | Party: Distribute |
| Use item / read scroll | U | U | U | U, includes Identify | Party: Use item |
| Noncombat spells | C | C | C | C | Magic: Cast spell |
| Resurrection | Spell 44 | Spell 44 (class rules differ) | Spell 44 | Spell through C | Magic: Resurrection shortcut |
| Transportation | Spell 54 | Spell 54 (plane restrictions by rules) | Spell 54 (visited planes option) | Transportation / Super Transport through C | Magic: Transportation shortcut |
| Save, load, preferences, sound | Option / shell | Option / shell | File / Option / shell | System / shell | Game |
| Auto save | Option | Option (or always by rule) | Rule option | — | Game: Auto save (where supported) |
| Printout | Option / Inn | Option / Inn | File / shell | — | Game: Printout (where supported) |
| Town Gate arrival report, deposit, XP shares, aging | Own entry rules | Own entry rules | Own entry rules | Automatic `_townent` | Unchanged arrival procedure |
| Banquet / audience / ending | Own ending | Own ending | Own ending | Automatic story sequence in Pendragon | Unchanged story procedure |

Code cross-check: `TownMenu`, `Game1.RunTownChoice`, `Game1.TownBar`, `CommonTownKeySet`,
`CastDialog`; `P4TownLoop` / `.Screens`, `P4DungeonScreen.Services` / `.System`.
Original specs: DOS `town-menus.md`; Atari ST II `towns.md` sections 1–4; DOS III `town.md`
sections 1–8; X68000 IV `towns.md` sections 1–9 and `ending.md`.
Gate share-out and the banquet are automatic procedures, not freely repeatable town services.
The unified menu must preserve their event and reward guards.

## Design

`unified-town-menu`: every-game rules option, off/on, **off in every preset**. Like other rules
it is saved and locked per save. Off uses the existing town UI and input unchanged.

On: the same four tabs and row order in every game and town: **Town, Party, Magic, Game**.
The Town tab starts on Guild. Left/right changes tabs, up/down changes row, Return/Space
activates. D-pad / left stick and A do the same; B/Esc selects Game (without leaving town).
Mouse hover and click select and activate rows; wheel moves rows. The common letters
A/B/G/M/N/L/C/U/D/I/S/T work regardless of the original art or COMMON TOWN KEYS setting.
F5/F9 and pad LB/RB save/load. A service returns to the shared menu when its existing dialog
or building closes; service internals keep the campaign's rules and presentation.

All tabs retain their rows. Unavailable actions are dimmed and explain the reason when
selected. An empty party can still enter the Guild, save/load and preferences. Hall and
Shipwright availability follows IV's actual town data. Unsupported services do not create
ships, historical records, currencies or new campaign events in games that lack them.
Resurrection and Transportation are shortcuts into the normal caster/spell workflow, retaining
spell knowledge, cost, failure, target and destination checks. Treasury replaces Bank when
that option is active; distribution respects modern inventory.

No original formulas, spell destinations, entry payouts, age increments, training costs,
banquets or ending conditions are changed by this menu.

## Built and verified (s198)

Shared controller and hit regions: `UnifiedTownMenu`; I–III host: `Game1.UnifiedTown`;
IV adapter: `P4DungeonScreen.UnifiedTown`. No rendering dependency was added to Core.
The spell-id mapping is `GameData/shared/town-menu.json` (I–III zero-based, IV one-based),
loaded by `TownMenuData`. `CastDialog` and `P4Menus` optionally filter the existing list;
normal Cast clears the filter. IV can open the shell's Options with the actual save's
rules locked, including hiding the importer/updater actions while the campaign runs.
The shared menu includes spendable gold and party size. Auto save is unavailable when
this save's rules mandate saving on departure; the row explains that policy.
I/II Trade opens modern inventory when enabled; otherwise the row explains that items
move through Distribute. III retains the save's Trade policy, IV its own Trade flow.

Regression tests: `UnifiedTownMenuTests`. Live keyboard, mouse and gamepad evidence:
`parity/s198-unified-town-menu.md`. Original formulas and story triggers are unchanged;
there is no new manual/code conflict or D-entry.
