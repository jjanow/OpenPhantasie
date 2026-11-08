using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

public partial class Game1
{
    // s154: I Amiga's requesters 0 / 22 / 14 behind AMIGA SCREENS (pack map_requesters, P1MapRequesters). The questions
    // keep Core's prompts and the remaster's keys; a click on a button sends the key of its answer. STYX / OCEAN have no
    // Core prompt: the Client holds their OK requester (h59 +0x408 / +0x418 through h86 +0x32a) until a key or OK.
    private string? _p1OkSpec;

    /// <summary>s156: Info › About Phantasie's requester 27 is up (the logo; a click on it or a key closes it).</summary>
    private bool _p1About;

    /// <summary>
    /// s163: a Client-held requester 0 in the town (pack <c>map_requesters.town</c> key: "D O N E !", the Printout's
    /// "PREPARE PRINTER"); <see cref="P1TownHeld.Answer"/> gets the button's index (0 = the first) when it closes.
    /// </summary>
    private sealed record P1TownHeld(string Key, Action<int>? Answer = null);

    private P1TownHeld? _p1TownHeld;

    /// <summary>One of the Client-held requesters is up (the STYX / OCEAN OK, About Phantasie, s163 the town's).</summary>
    private bool P1HeldRequesterUp => _p1OkSpec is not null || _p1About || _p1TownHeld is not null;

    /// <summary>True when the pack draws the town requester <paramref name="key"/> now (I, AMIGA SCREENS, in a town).</summary>
    private bool P1TownRequesterReady(string key) =>
        P1Requesters is { Menu: not null, TownPalette: not null, PenRemap.Count: 16 } layout && layout.Town.ContainsKey(key)
        && _session?.Mode is GameMode.Town or GameMode.Guild;

    /// <summary>s163: opens the town requester <paramref name="key"/> (no-op without one in the pack).</summary>
    private void OpenP1TownHeld(string key, Action<int>? answer = null)
    {
        if (P1TownRequesterReady(key))
            _p1TownHeld = new P1TownHeld(key, answer);
    }

    private Data.MapRequestersLayout? P1Requesters =>
        _session is { Game: GameId.P1 } && CurrentGameSettings.OriginalScreens ? _assets?.Pack?.MapRequesters : null;

    /// <summary>
    /// Requester 0 or 22 for a question over the overworld map (the HEARS / SLEEPING MONSTERS and Black Knights menus, the
    /// town / dungeon / inn questions), or null (another prompt or game, AMIGA SCREENS off, a pack without them).
    /// </summary>
    private RequesterScene? P1MapRequesterFor(SessionPrompt prompt)
    {
        if (P1Requesters is not { } layout || _session is not { } s)
            return null;
        var asked = (prompt.Kind, s.Mode) switch
        {
            (SessionPromptKind.WildernessEncounter or SessionPromptKind.OverworldEntry or SessionPromptKind.WildernessInn, GameMode.Overworld) => true,
            // s156: the dungeon's h41 +0x15e / +0x11c questions and the find's "KEEP IT?" (h44 +0xfe4)
            (SessionPromptKind.DungeonTrap or SessionPromptKind.DungeonLock or SessionPromptKind.DungeonExitTakeIt
                or SessionPromptKind.DungeonExitSaveMap, GameMode.Dungeon) => true,
            (SessionPromptKind.Treasure, GameMode.Dungeon or GameMode.Overworld) => true,
            _ => false,
        };
        if (!asked)
            return null;
        if (prompt.MenuId == layout.BlackKnightsMenu && layout.BlackKnights is { } knights)
            return P1MapRequesters.Fixed(layout.Palette, knights, i => P1MapRequesters.ClickKey(prompt, i));
        string? id = null;
        if (layout.Menu is null || !(prompt.MenuId is int menu && layout.Menus.TryGetValue(menu, out id)
                                     || layout.Kinds.TryGetValue(prompt.Kind.ToString(), out id)))
            return null;
        string Member(int slot) => slot >= 0 && slot < s.Party.Members.Count ? s.Party.Members[slot].Name : "";
        var arg = prompt.Kind switch
        {
            SessionPromptKind.WildernessEncounter => s.WildernessPrompt is { ListenerSlot: int slot } ? Member(slot) : "",
            SessionPromptKind.DungeonTrap => s.DungeonTrapPrompt is { } trap ? Member(trap.SpotterIndex) : "",
            SessionPromptKind.DungeonLock => s.DungeonLockPrompt is { } heard ? Member(heard.ListenerIndex) : "",
            SessionPromptKind.Treasure => s.PendingTreasureName ?? "",
            _ => "",
        };
        return P1MapRequesters.Menu(layout, UiText.F(id!, arg), i => P1MapRequesters.ClickKey(prompt, i));
    }

    /// <summary>
    /// Requester 14 for Inspect's sheet, or null (the remaster's panel): in the town or the Guild with its stored pens on the
    /// town palette (s154), over the overworld or a dungeon (h50 <c>+0x7b2</c>, s156) with the pens remapped on the map palette.
    /// </summary>
    private RequesterScene? P1SheetFor(GameSession s)
    {
        if (P1Requesters is not { Sheet: { } sheet } layout)
            return null;
        // s156: the sheet's EQUIPMENT / SKILLS / POTIONS views (requesters 10 / 9 / 11); s158: SPELLS = requester 12.
        var scene = _inspectDialog.SheetMember is { } member ? P1MapRequesters.Sheet(sheet, member)
            : _inspectDialog.PageShown is (0, var caster) && P1SheetSpellList is { } spells
                ? P1MapRequesters.SheetSpells(spells, sheet.Palette, layout.PenUnmap,
                    [.. SpellPicker.Known(caster).Select(id => UiOverlays.P1SpellName(id, _data))], _inspectDialog.AmigaSpellFirst)
            : _inspectDialog.PageShown is var (page, who) ? P1MapRequesters.SheetPageFor(sheet, page, who, _data)
            : null;
        if (scene is null)
            return null;
        return s.Mode switch
        {
            GameMode.Town or GameMode.Guild => scene,
            GameMode.Overworld or GameMode.Dungeon when layout.PenRemap.Count == 16 =>
                P1MapRequesters.Remap(scene, layout.PenRemap, layout.Palette),
            _ => null,
        };
    }

    /// <summary>
    /// s158: the sheet's SPELLS view as the fight's requester 12 (pack <c>combat_field.spell_list.spells</c>), or null (the
    /// remaster's page): I, AMIGA SCREENS, a pack with the sheet, in the town, the Guild or over the map.
    /// </summary>
    private Data.CombatFieldList? P1SheetSpellList =>
        P1Requesters is { Sheet: not null } && _session?.Mode is GameMode.Town or GameMode.Guild or GameMode.Overworld or GameMode.Dungeon
            ? _assets?.Pack?.CombatField?.SpellList?.Spells : null;

    /// <summary>
    /// s156: the town's h86 questions as requester 0 with the stored pens (Guild › Add's "NOT IN THE TOWN" move, Training's
    /// "IT WILL COST"), or null (another modal or step: the remaster's panel). Clicks send the remaster's keys (Y / N, A / N).
    /// </summary>
    private RequesterScene? P1TownRequesterFor(IModalDialog modal, GameSession s)
    {
        if (P1Requesters is not { } layout || s.Mode != GameMode.Guild)
            return null;
        if (ReferenceEquals(modal, _guildAddDialog) && _guildAddDialog.Confirming)
            return P1MapRequesters.Town(layout, "guild_add_move", i => i == 0 ? Keys.Y : Keys.N);
        if (ReferenceEquals(modal, _guildWhoDialog) && _guildWhoDialog.ConfirmingTraining is { } m && s.CurrentTown is not null)
            return P1MapRequesters.Town(layout, "training_cost", i => i == 0 ? Keys.A : Keys.N, s.TrainingGoldCost(m));
        return null;
    }

    /// <summary>s163: the scroll's printout questions in the town's Use item (h29 <c>+0x1da</c> / <c>+0x1f6</c>), or null.</summary>
    private RequesterScene? P1UseItemRequesterFor(IModalDialog modal, GameSession s)
    {
        if (P1Requesters is not { } layout || s.Mode != GameMode.Town || !ReferenceEquals(modal, _useItemDialog))
            return null;
        if (_useItemDialog.AskingPrintout)
            return P1MapRequesters.Town(layout, "scroll_printout", i => i == 0 ? Keys.Y : Keys.N);
        if (_useItemDialog.AskingPrinter)
            return P1MapRequesters.Town(layout, "prepare_printer", i => i == 0 ? Keys.Enter : Keys.Escape);
        return null;
    }

    /// <summary>s156: Guild › New's rolled character as requester 14 with PURGE / KEEP (h23 <c>+0xc08</c>), or null.</summary>
    private RequesterScene? P1RolledSheetFor(GameSession s) =>
        P1Requesters is { Sheet: { RolledButtons.Count: 2 } sheet } && s.Mode == GameMode.Guild && _guildNewDialog.RolledMember is { } rolled
            ? P1MapRequesters.Sheet(sheet, rolled, rolled: true)
            : null;

    /// <summary>After a blocked wilderness step: "THE RIVER STYX" / `core.overworld.that_is_the_ocean` open their OK requester.</summary>
    private void OfferP1Ok(GameSession s, long mark, OverworldMoveResult result)
    {
        if (result.Kind == OverworldMoveKind.Blocked)
            OfferP1OkLines(s, mark);
    }

    /// <summary>
    /// The first line logged since <paramref name="mark"/> that the pack's <c>oks</c> name opens its requester (STYX / OCEAN
    /// after a blocked step; s158: the wilderness inn's `p1field.req.cant_afford`, h71 <c>+0xe76</c>, filmed).
    /// </summary>
    private void OfferP1OkLines(GameSession s, long mark)
    {
        if (P1Requesters is not { Menu: not null } layout || s.Mode != GameMode.Overworld)
            return;
        var logged = s.LogSince(mark).Select(l => l.Trim()).ToList();
        foreach (var (line, spec) in layout.Oks)
        {
            if (!logged.Contains(GameText.S(line)))
                continue;
            _p1OkSpec = UiText.S(spec);
            _walk = null;
            SetFeedback("", HelpForMode(s.Mode)); // the text is in the requester, not on the line
            return;
        }
    }

    /// <summary>While the OK requester is up it owns every key, button and click; a new one (a click on OK sends Return) closes it.</summary>
    private bool HandleP1OkKeys(KeyboardState keys, GamePadState pad)
    {
        if (!P1HeldRequesterUp)
            return false;
        if (_p1TownHeld is { } held)
        {
            // s163: one button: any key closes it; two: Y / Return / O / the first button's click answer it, N / Esc / A
            // the second (h86 +0x32a's index).
            var two = P1Requesters?.Town.GetValueOrDefault(held.Key) is { } id && P1MapRequesters.Parse(UiText.S(id)).Buttons.Count > 1;
            int? answer = null;
            foreach (var key in _input.NewKeys(keys))
            {
                answer = !two ? 0 : key is Keys.Y or Keys.Enter or Keys.O or Keys.Space ? 0
                    : key is Keys.N or Keys.Escape or Keys.A or Keys.Back ? 1 : null;
                if (answer is not null)
                    break;
            }
            if (answer is null && !two && _input.NewButtons(pad).Any())
                answer = 0;
            if (answer is int i)
            {
                _p1TownHeld = null;
                held.Answer?.Invoke(i);
            }
            return true;
        }
        if (_input.NewKeys(keys).Any() || _input.NewButtons(pad).Any())
        {
            _p1OkSpec = null;
            _p1About = false;
        }
        return true;
    }

    /// <summary>
    /// s154 developer switch: <c>--p1-map-question NAME</c> with a test party puts the party in the wilderness with a question
    /// up: black-knights, hears, sleeping, town, dungeon, or styx / ocean (their OK requester). To film the requesters.
    /// </summary>
    public const string P1QuestionSwitchName = "--p1-map-question";

    private static string? P1QuestionSwitch(IReadOnlyList<string> args)
    {
        var i = args.ToList().IndexOf(P1QuestionSwitchName);
        return i >= 0 && i + 1 < args.Count ? args[i + 1] : null;
    }

    private void OpenP1Question(GameSession s, string question)
    {
        s.DepartGuildToWilderness();
        var a = s.CampaignAccess;
        switch (question)
        {
            case "black-knights":
                a.WildernessPrompt = new WildernessPrompt(WildernessMenu.BlackKnights, 0, null, GameText.S("core.encounter.black_knights"));
                break;
            case "hears":
                a.WildernessPrompt = new WildernessPrompt(WildernessMenu.HearsMonsters, 1, 0,
                    GameText.F("core.encounter.x_hears_monsters", s.Party.Members[0].Name));
                break;
            case "sleeping":
                a.WildernessPrompt = new WildernessPrompt(WildernessMenu.SleepingMonsters, 1, null, GameText.S("core.encounter.sleeping_monsters"));
                break;
            case "town":
            case "dungeon":
                a.EntryPrompt = new OverworldEntryPrompt(question == "town" ? OverworldEntryKind.Town : OverworldEntryKind.Dungeon, 1,
                    s.Overworld.X, s.Overworld.Y);
                break;
            case "styx":
                _p1OkSpec = UiText.S("p1field.req.river_styx");
                break;
            case "ocean":
                _p1OkSpec = UiText.S("p1field.req.the_ocean");
                break;
        }
    }

    private RequesterScene? P1OkScene() =>
        _p1TownHeld is { } held && P1TownRequesterReady(held.Key)
            ? P1MapRequesters.Town(P1Requesters!, held.Key, i => i == 0 ? Keys.Y : Keys.N)
            : _p1OkSpec is { } spec && P1Requesters is { Menu: not null } layout && _session is { Mode: GameMode.Overworld }
            ? P1MapRequesters.Menu(layout, spec, _ => Keys.Enter)
            : _p1About && P1Requesters is { About: not null } about && _session is { Mode: GameMode.Overworld or GameMode.Dungeon }
                ? P1MapRequesters.About(about)
                : null;
}
