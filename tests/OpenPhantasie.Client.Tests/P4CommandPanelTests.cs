using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// IV X68000's Command panel (s136; FORMAT.md 6.9 "Command panel", <c>tools/scripts/x68k_p4_panels.py</c>): the pack's
/// panel file holds P4.X's <c>_mkicon</c> tables, the screen picks the panel the original shows at the street, outdoor and
/// location-map prompts, and each icon presses its hot key.
/// </summary>
public class P4CommandPanelTests
{
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);

    private static GraphicsPack Pack() => GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Find("x68000")!;

    [OriginalDataFact]
    public void Pack_LoadsTheMkiconTables()
    {
        var set = Pack().CommandPanels!;
        Assert.Equal((496, 48, 136, 324), set.Window); // _wopen(34, 496, 48, 136, 324, ...)
        Assert.Equal(42, set.Panels.Count); // s158: _acgamend(0) and (1) computed
        Assert.Equal(32, set.IconSize);
        // _mkicon: x = 38, then 38 ^ 24 = 62, alternating; y = 8 + 52 · i.
        Assert.Equal((38, 8), set.IconAt(0));
        Assert.Equal((62, 60), set.IconAt(1));
        Assert.Equal((62, 268), set.IconAt(5)); // odd: the right column

        var town = set.Panels["_townmenu"];
        Assert.Equal("p4x.0x401d2", town.Title); // "" → "Command"
        Assert.Equal([2, 3, 4, 5, 6, 7], town.Icons.Select(i => i.Icon));
        Assert.Equal(["F", "C", "T", "D", "U", "S"], town.Icons.Select(i => i.Key));
        Assert.Equal("p4x.0x3f724", town.Icons[0].Label);
        Assert.Equal([7], set.Panels["_townmenu.no_party"].Icons.Select(i => i.Icon));
        Assert.Equal(["F", "P", "C", "U", "M", "S"], set.Panels["_outdoormenu"].Icons.Select(i => i.Key));
        Assert.Equal(["F", "P", "C", "U", "E", "S"], set.Panels["_dsetup"].Icons.Select(i => i.Key));
        Assert.Equal([20, 24, 25, 1, 26, 36], set.Panels["_syscon"].Icons.Select(i => i.Icon)); // the s110 system-menu shot
        Assert.Equal("Space", set.Panels["_syscon"].Icons[0].Key);
    }

    [OriginalDataFact]
    public void Pack_HasEveryIconPicture_AndTheLabelFont()
    {
        var pack = Pack();
        foreach (var icon in pack.CommandPanels!.Panels.Values.SelectMany(p => p.Icons))
            Assert.NotNull(pack.Resolve($"p4.icon.{icon.Icon}"));
        Assert.Equal("x68k-p4-font1", pack.RoleFonts[P4DungeonScreen.PanelLabelFontRole].Id);
    }

    [OriginalDataFact]
    public void Pack_HasEveryItemIcon_AndAnIconRowTakesTwoTextRows()
    {
        // s151: P4.X font 2's 50 glyphs (_idat b0 0–49) as p4.item.N, 32 × 32; at the pack's half scale (16 layout
        // pixels) a row takes two text rows (9 + the last row's 7), so a block in view never crosses the box's edge.
        var pack = Pack();
        for (var n = 0; n < 50; n++)
            Assert.NotNull(pack.Resolve($"p4.item.{n}"));
        Assert.Null(pack.Resolve("p4.item.50"));
        Assert.Equal(2, P4DungeonScreen.IconRows(16));
        Assert.Equal(1, P4DungeonScreen.IconRows(7));
        Assert.Equal(3, P4DungeonScreen.IconRows(17));
    }

    [Fact]
    public void IconKeys_AreLettersOrSpace()
    {
        Assert.Equal(Keys.F, P4DungeonScreen.PanelKey(new CommandPanelIcon(2, "F", "", "")));
        Assert.Equal(Keys.S, P4DungeonScreen.PanelKey(new CommandPanelIcon(7, "S", "", "")));
        Assert.Equal(Keys.Space, P4DungeonScreen.PanelKey(new CommandPanelIcon(20, "Space", "", "")));
        Assert.Equal(Keys.None, P4DungeonScreen.PanelKey(new CommandPanelIcon(20, "", "", "")));
    }

    [Fact]
    public void Label_CentredUnderTheIcon_KeyLetterInColour2()
    {
        // _seticon: _wloc(win, x + 16 − (_fsize >> 1), y + 32).
        Assert.Equal((38 + 16 - 20, 8 + 32), P4DungeonScreen.PanelLabelAt(38, 8, 32, 41));
        Assert.Equal([("F", 2), (":Profile", 0)], P4DungeonScreen.PanelLabelRuns("F:Profile"));
        Assert.Equal([(" SPACE", 2)], P4DungeonScreen.PanelLabelRuns(" SPACE")); // a leading space: all in colour 2
        Assert.Empty(P4DungeonScreen.PanelLabelRuns(""));
    }

    [OriginalDataFact]
    public void PanelName_FollowsThePrompt()
    {
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        Assert.Equal(P4TownPrompt.Menu, game.Town.Prompt);
        Assert.Equal("_townmenu", s.PanelName);
        Assert.True(s.PanelPlace);
        s.HandleKeys([Keys.F]); // s140: a party menu shows its own panel (F's member pick: _inspect)
        Assert.True(s.MenuOpen);
        Assert.Equal("_inspect", s.PanelName);
        Assert.True(s.PanelPlace);

        s.HandleKeys([Keys.Space]); // close the member pick
        Assert.False(s.MenuOpen);
        s.HandleKeys([Keys.D7]); // Leave
        Assert.Equal(P4Place.Outdoors, game.Place);
        Assert.Equal(P4OutdoorPrompt.Action, game.Outdoor.Prompt);
        Assert.Equal("_outdoormenu", s.PanelName);

        Assert.True(s.PanelPlace); // the strip stays outdoors, panel or not
        var map = new P4DungeonScreen(P4Preview.Start(4, Code));
        Assert.True(map.PanelPlace);
        Assert.Equal(P4Place.Dungeon, map.Game.Place);
        Assert.Equal(P4DungeonPrompt.Action, map.Game.Dungeon.Prompt);
        Assert.Equal("_dsetup", map.PanelName);
    }

    [OriginalDataFact]
    public void Bank_ShowsItsPanel_IconsPressTheListDigits()
    {
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        game.Town.Enter(P4Building.Bank);
        Assert.Equal(P4TownPrompt.Pick, game.Town.Prompt);
        Assert.Equal("_bank", s.PanelName);
        Assert.True(s.PanelPlace);

        var icons = Pack().CommandPanels!.Panels["_bank"].Icons;
        Assert.Equal(["L", "D", "W"], icons.Select(i => i.Key));
        // The list is Deposit, Withdraw, Leave: D = 1, W = 2, L = 3.
        Assert.Equal([Keys.D3, Keys.D1, Keys.D2], icons.Select(i => P4DungeonScreen.PanelClickKey("_bank", i)));
        Assert.Equal(Keys.F, P4DungeonScreen.PanelClickKey("_townmenu", new CommandPanelIcon(2, "F", "", "")));

        s.HandleKeys([Keys.D3]); // what the Leave icon presses
        Assert.False(game.Town.InBuilding);
        Assert.Equal("_townmenu", s.PanelName);
    }

    [OriginalDataTheory]
    [InlineData(P4Building.Guild, "_guildmenu", "LG", "41")]
    [InlineData(P4Building.HallOfGlory, "_hall", "LHTP", "4132")]
    public void GuildAndHall_ShowTheirPanels_IconsPressTheListDigits(P4Building b, string name, string keys, string digits)
    {
        var game = P4Preview.StartInTown(b == P4Building.HallOfGlory ? 1 : 2, Code); // the Hall: a type 0 town
        var s = new P4DungeonScreen(game);
        game.Town.Enter(b);
        Assert.Equal(P4TownPrompt.Pick, game.Town.Prompt);
        Assert.Equal(name, s.PanelName);
        Assert.True(s.PanelPlace);
        var icons = Pack().CommandPanels!.Panels[name].Icons;
        Assert.Equal(keys.Select(c => c.ToString()), icons.Select(i => i.Key));
        Assert.Equal(digits.Select(c => Keys.D0 + (c - '0')), icons.Select(i => P4DungeonScreen.PanelClickKey(name, i)));
        s.HandleKeys([Keys.D4]); // Leave
        Assert.False(game.Town.InBuilding);
        Assert.Equal("_townmenu", s.PanelName);
    }

    [OriginalDataFact]
    public void Armory_ShowsItsPanel_LeaveIconPressesTheLeaveLine()
    {
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        game.Town.Enter(P4Building.Armory);
        Assert.Equal(P4TownPrompt.Pick, game.Town.Prompt);
        Assert.Equal("_armory", s.PanelName);
        Assert.True(s.PanelPlace);
        Assert.Equal(["L"], Pack().CommandPanels!.Panels["_armory"].Icons.Select(i => i.Key));

        // Page lines, Next, Leave, party: Leave is line ShopPage.Count + 2 (1-based).
        var leave = Keys.D1 + game.Town.ShopPage.Count + 1;
        s.HandleKeys([leave]);
        Assert.False(game.Town.InBuilding);
        Assert.Equal("_townmenu", s.PanelName);
    }

    private static CommandPanelIcon Icon(string panel, string key) =>
        Pack().CommandPanels!.Panels[panel].Icons.Single(i => i.Key == key);

    [OriginalDataFact]
    public void EveryLabelAndTitle_FitsTheFittedWindow()
    {
        // s138: English labels run past P4.X's 136-pixel window ("H:A person from the Hall" is 150): the window widens
        // (one width for every panel) and a label that would cross the frame moves in; nothing is cut.
        var pack = Pack();
        var set = pack.CommandPanels!;
        var labelFont = FontSources.ForRole(pack, P4DungeonScreen.PanelLabelFontRole)!;
        var titleFont = FontSources.ForRole(pack, P4DungeonScreen.MysticFontRole)!;
        static string Text(string id, string original)
        {
            try
            {
                return GameText.S(id);
            }
            catch (KeyNotFoundException)
            {
                return original;
            }
        }
        var w = P4DungeonScreen.FittedPanelWidth(set, i => PixelFont.Measure(Text(i.Label, i.Text), labelFont),
            p => PixelFont.Measure(Text(p.Title, p.TitleText), titleFont));
        Assert.True(w > set.Window.Width);
        foreach (var panel in set.Panels.Values)
        {
            Assert.InRange(PixelFont.Measure(Text(panel.Title, panel.TitleText), titleFont), 0, w - 2 * P4DungeonScreen.PanelMargin);
            for (var i = 0; i < panel.Icons.Count; i++)
            {
                var (x, y) = set.IconAt(i);
                var width = PixelFont.Measure(Text(panel.Icons[i].Label, panel.Icons[i].Text), labelFont);
                var (lx, _) = P4DungeonScreen.PanelLabelAt(x, y, set.IconSize, width, w);
                Assert.True(lx >= P4DungeonScreen.PanelMargin && lx + width <= w - P4DungeonScreen.PanelMargin,
                    $"{panel.Name} icon {i}: {lx}..{lx + width} in {w}");
            }
        }
        // A label that fits stays centred under its icon (the original's place).
        Assert.Equal(P4DungeonScreen.PanelLabelAt(38, 8, 32, 41), P4DungeonScreen.PanelLabelAt(38, 8, 32, 41, w));
    }

    [OriginalDataFact]
    public void WaitsAndQuestions_ShowSpcWaitAndConfirm_OkayCanselPressYN()
    {
        var game = P4Preview.StartInTown(1, Code);
        var s = new P4DungeonScreen(game);
        while (game.Town.Prompt == P4TownPrompt.Wait)
        {
            Assert.Equal("_spc_wait", s.PanelName); // _townent: "has grown older"
            Assert.Equal(Keys.Space, P4DungeonScreen.PanelClickKey("_spc_wait", Icon("_spc_wait", "Space")));
            s.HandleKeys([Keys.Space]);
        }
        Assert.Equal(Keys.Y, P4DungeonScreen.PanelClickKey("_confirm", Icon("_confirm", "O")));
        Assert.Equal(Keys.N, P4DungeonScreen.PanelClickKey("_confirm", Icon("_confirm", "C")));
        s.HandleKeys([Keys.D7]); // Leave: outdoors, beside Pendragon
        Assert.Equal("_outdoormenu", s.PanelName);
        s.HandleKeys([Keys.Up]); // onto the town square: "Enter?"
        Assert.Equal(P4OutdoorPrompt.Confirm, game.Outdoor.Prompt);
        Assert.Equal("_confirm", s.PanelName); // _otown
        s.HandleKeys([Keys.N]);
        Assert.Equal("_outdoormenu", s.PanelName);

        // The shipwright: under the price a SPACE wait (_shipyard's _spc_wait), else _noyes's YES / NO box (s140).
        var ship = P4Preview.StartInTown(2, Code);
        var w = new P4DungeonScreen(ship);
        ship.Town.Enter(P4Building.Shipwright);
        Assert.Equal((P4TownPrompt.Wait, "_spc_wait"), (ship.Town.Prompt, w.PanelName));
        w.HandleKeys([Keys.Space]);
        ship.Party.Gold = 5000;
        ship.Town.Enter(P4Building.Shipwright);
        Assert.Equal(P4TownPrompt.Confirm, ship.Town.Prompt);
        Assert.Equal(P4DungeonScreen.NoYesName, w.PanelName);
        w.HandleKeys([Keys.N]);
        Assert.Equal("_spc_wait", w.PanelName); // s151: "Come again." and _spc_wait
        w.HandleKeys([Keys.Space]);
        Assert.Equal("_townmenu", w.PanelName);
    }

    [OriginalDataFact]
    public void GuildScreens_ShowTheirPanels_IconsPressTheirKeys()
    {
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        game.Town.Enter(P4Building.Guild);
        s.HandleKeys([Keys.D2]); // T: Training hall
        Assert.Equal("_trang", s.PanelName);
        Assert.Equal(Keys.Space, s.PanelPress("_trang", Icon("_trang", "E")).Key); // End training = SPACE
        s.HandleKeys([Keys.Space, Keys.D3]); // M: Magic training
        Assert.Equal(P4TownPrompt.Who, game.Town.Prompt);
        Assert.Equal("_mage", s.PanelName);
        s.HandleKeys([Keys.Space, Keys.D1]); // G: the roster
        Assert.Equal("_mlistmenu", s.PanelName);
        Assert.Equal((Keys.D2, (int?)null), s.PanelPress("_mlistmenu", Icon("_mlistmenu", "N")));
        Assert.Equal(Keys.Space, s.PanelPress("_mlistmenu", Icon("_mlistmenu", "Space")).Key);

        s.HandleKeys([Keys.D2]); // N: creation
        Assert.Equal("_makemenu", s.PanelName);
        Assert.Equal([Keys.D1, Keys.D2, Keys.D3], "EYN".Select(k => s.PanelPress("_makemenu", Icon("_makemenu", k.ToString())).Key));
        s.HandleKeys([Keys.D2]); // Yes: a descendant
        Assert.Equal("_make1menu", s.PanelName);
        Assert.Equal([Keys.Space, Keys.D1, Keys.D2],
            new[] { "Space", "N", "H" }.Select(k => s.PanelPress("_make1menu", Icon("_make1menu", k)).Key));
        s.HandleKeys([Keys.D1]); // N: Decide anew → the race list (no panel of the 40)
        Assert.Equal(P4TownPrompt.Pick, game.Town.Prompt);
        Assert.Null(s.PanelName);
        s.HandleKeys([Keys.D1, Keys.D1]); // a race, a class → Okay / Cansel
        Assert.Equal("_confirm", s.PanelName);
        s.HandleKeys([Keys.Y]);
        Assert.Equal("_make5menu", s.PanelName);
        Assert.Equal([Keys.Space, Keys.D1, Keys.D2, Keys.D3],
            new[] { "Space", "S", "D", "H" }.Select(k => s.PanelPress("_make5menu", Icon("_make5menu", k)).Key));
        s.HandleKeys([Keys.Space, Keys.Space, Keys.D1]); // back out: E finishes creating
        Assert.Equal("_mlistmenu", s.PanelName);

        s.HandleKeys([Keys.D1]); // P: Form party → pick the members
        Assert.Equal("_mlistsubset", s.PanelName);
        s.HandleKeys([Keys.D1, Keys.Space, Keys.Y]);
        Assert.Equal("_party", s.PanelName); // "Elemental"
        Assert.Equal(Keys.Enter, s.PanelPress("_party", Icon("_party", "Space")).Key); // SPACE takes the cursor line
        s.HandleKeys([Keys.Enter]);
        Assert.Equal("_mlistmenu", s.PanelName);
        Assert.Single(game.Party.Members);
    }

    [OriginalDataFact]
    public void HallScreens_HistoryLLeaves_ListLIsSpace()
    {
        var game = P4Preview.StartInTown(1, Code);
        var s = new P4DungeonScreen(game);
        while (game.Town.Prompt == P4TownPrompt.Wait)
            s.HandleKeys([Keys.Space]);
        game.Town.Enter(P4Building.HallOfGlory);
        s.HandleKeys([Keys.D1]); // H: history
        Assert.Equal("_hhist", s.PanelName);
        Assert.Equal(Keys.Space, s.PanelPress("_hhist", Icon("_hhist", "N")).Key); // N: Next picture
        Assert.Equal(Keys.L, s.PanelPress("_hhist", Icon("_hhist", "L")).Key);
        s.HandleKeys([Keys.Space]);
        Assert.Equal(1, game.Town.HallHistoryPage);
        s.HandleKeys([Keys.L]); // L: Done, at once
        Assert.Equal("_hall", s.PanelName);
        s.HandleKeys([Keys.D2]); // P: Character info
        Assert.Equal("_hprofmenu", s.PanelName);
        Assert.Equal(Keys.Space, s.PanelPress("_hprofmenu", Icon("_hprofmenu", "L")).Key);
        s.HandleKeys([Keys.D1]);
        Assert.Equal("_spc_wait", s.PanelName); // _dsp_hinfo
        s.HandleKeys([Keys.Space, Keys.Space]);
        Assert.Equal("_hall", s.PanelName);
    }

    [OriginalDataFact]
    public void HallTransfer_Options_GameAndPlaceQuestionsAreTextLists_ThenTheList()
    {
        // T5 options (s149): "which game?" and "Hall or Guild?" have no panel of the original's (text lines, digit keys);
        // the list after them keeps _show_gm's O / X. A III character picked for the Guild joins the roster.
        var o = Code.With(P4RuleOptions.TransferGames.Id, "all").With(P4RuleOptions.TransferJoin.Id, "guild");
        var game = P4Preview.StartInTown(1, o);
        var s = new P4DungeonScreen(game);
        game.Town.HallTransferSource = g => g == GameId.P3 ? [new Character { Name = "ALPHA", Class = CharacterClass.Priest }]
            : g == GameId.P1 ? [new Character { Name = "SQUIRE" }] : null;
        while (game.Town.Prompt == P4TownPrompt.Wait)
            s.HandleKeys([Keys.Space]);
        game.Town.Enter(P4Building.HallOfGlory);
        s.HandleKeys([Keys.D3, Keys.Y]); // T, O: Okay
        Assert.Null(s.PanelName);
        Assert.Contains(s.BoxLines(), l => l.Text.Contains(GameText.S("core.p4.hall_game_iii"), StringComparison.Ordinal));
        s.HandleKeys([Keys.D3]); // Phantasie III
        Assert.Null(s.PanelName);
        s.HandleKeys([Keys.D2]); // Guild
        Assert.Equal("_show_gm", s.PanelName);
        Assert.Equal(Keys.D2, s.PanelPress("_show_gm", Icon("_show_gm", "O")).Key);
        s.HandleKeys([Keys.D1, Keys.D2, Keys.Y]); // mark ALPHA, O, Okay
        var joined = game.Party.Roster.Where(m => m?.Name == "ALPHA").ToList();
        Assert.Single(joined);
        Assert.Equal(P4Ids.Priest, joined[0]!.Class);
        Assert.Equal("_hall", s.PanelName);
    }

    [OriginalDataFact]
    public void HallTransfer_OkayCanselThenTheListsOAndX()
    {
        // T5 III → IV: T asks with _htran's O / C; the III list's O and X are the two lines after the characters.
        var game = P4Preview.StartInTown(1, Code);
        var s = new P4DungeonScreen(game);
        game.Town.HallTransferSource = _ =>
            [new Character { Name = "ALPHA", Class = CharacterClass.Wizard }, new Character { Name = "BETA" }];
        while (game.Town.Prompt == P4TownPrompt.Wait)
            s.HandleKeys([Keys.Space]);
        game.Town.Enter(P4Building.HallOfGlory);
        s.HandleKeys([Keys.D3]); // T: Character transfer
        Assert.Equal("_htran", s.PanelName);
        Assert.Equal(Keys.Y, s.PanelPress("_htran", Icon("_htran", "O")).Key);
        s.HandleKeys([Keys.Y]);
        Assert.Equal("_show_gm", s.PanelName);
        Assert.Contains("ALPHA", game.Town.Choices[0].Text);
        Assert.EndsWith(GameText.S("p4x.0x42802"), game.Town.Choices[0].Text); // Wiz
        Assert.Equal(Keys.D3, s.PanelPress("_show_gm", Icon("_show_gm", "O")).Key);
        Assert.Equal(Keys.D4, s.PanelPress("_show_gm", Icon("_show_gm", "X")).Key);
        s.HandleKeys([Keys.D1, Keys.D3]); // mark ALPHA, O: Okay
        Assert.Equal(P4TownPrompt.Confirm, game.Town.Prompt);
        s.HandleKeys([Keys.Y]);
        Assert.Equal("ALPHA", game.Party.HallImports[3]!.Name);
        Assert.Equal("_hall", s.PanelName);

        // P: the import's sheet: III's origin list, the three results (blank = not played), Lord Wood alive (no Priest).
        game.Party.HallImports[4] = P4Hall.FromPhantasieIII(new Character
        {
            Name = "GAMMA", Class = CharacterClass.Ranger, Score = 120,
            Phantasie3 = new Phantasie3CharacterData { Origin = Phantasie3Origin.Netherworld, Phantasie1Score = 5 },
        }, 4);
        s.HandleKeys([Keys.D2, Keys.D5]); // P, the fifth person: GAMMA
        var lines = s.BoxLines().Select(l => l.Text).ToList();
        Assert.Contains(lines, l => l.EndsWith(GameText.S("p4x.0x41b76"), StringComparison.Ordinal)); // Netherworld
        Assert.Contains(UiText.F("p4.hall.result_x_y", "I", GameText.S("p4x.0x41bb5")), lines);
        Assert.Contains(UiText.F("p4.hall.result_x_y", "II", ""), lines);
        Assert.Contains(UiText.F("p4.hall.result_x_y", "III", GameText.S("p4x.0x41ba5")), lines);
        Assert.Contains(GameText.S("p4x.0x41ba0"), lines); // Alive
        Assert.Equal("GAMMA" + GameText.S("p4x.0x40a83"),
            s.AncestorText(new P4AncestorRef(P4AncestorKind.Hall, 4))[..(5 + GameText.S("p4x.0x40a83").Length)]);
    }

    [OriginalDataFact]
    public void TradeAndShareOut_IconsPressTheirLines_PastNineByTheCursor()
    {
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        var members = game.Party.Members.Count;
        s.HandleKeys([Keys.T]);
        Assert.Equal("_trade", s.PanelName);
        // Carried slots, members, then P (the pack), S (sell), L (Done): all past the digit keys.
        var done = P4Member.CarriedSlots + members + 2;
        Assert.Equal((Keys.Enter, (int?)done), s.PanelPress("_trade", Icon("_trade", "L")));
        Assert.Equal((Keys.Enter, (int?)(done - 2)), s.PanelPress("_trade", Icon("_trade", "P")));
        s.AimAt(done); // what the click does: the cursor on the line, then Return
        s.HandleKeys([Keys.Enter]);
        Assert.False(game.Town.InBuilding);

        s.HandleKeys([Keys.D]);
        Assert.Equal("_distribute", s.PanelName);
        Assert.Equal((Keys.D1 + members, (int?)null), s.PanelPress("_distribute", Icon("_distribute", "P")));
        Assert.Equal((Keys.D1 + members + 1, (int?)null), s.PanelPress("_distribute", Icon("_distribute", "S")));
    }

    [OriginalDataFact]
    public void System_SpaceBack_SSaves_EEndsThroughAcend_AShowsAbout()
    {
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        Assert.Null(s.HandleKeys([Keys.S]));
        Assert.Equal((P4SystemPage.Menu, "_syscon"), (s.SystemPage, s.PanelName));
        s.HandleKeys([Keys.A]);
        Assert.Equal("_about", s.PanelName);
        s.HandleKeys([Keys.Space]);
        Assert.Equal("_syscon", s.PanelName);
        s.HandleKeys([Keys.E]); // s158: _acgamend(0)'s Select Game
        Assert.Equal("_acgamend", s.PanelName);
        Assert.Equal(["Space", "E", "T"], Pack().CommandPanels!.Panels["_acgamend"].Icons.Select(i => i.Key));
        Assert.Equal(Keys.T, s.PanelPress("_acgamend", Icon("_acgamend", "T")).Key);
        s.HandleKeys([Keys.E]);
        Assert.Equal("_acend", s.PanelName);
        Assert.Equal(Keys.Y, s.PanelPress("_acend", Icon("_acend", "O")).Key);
        s.HandleKeys([Keys.N]); // _acend's C: back to the Select Game icons
        Assert.Equal("_acgamend", s.PanelName);
        s.HandleKeys([Keys.T]);
        Assert.Equal("_acend.2", s.PanelName);
        s.HandleKeys([Keys.C]);
        Assert.Equal("_acgamend", s.PanelName);
        s.HandleKeys([Keys.Space]);
        Assert.Equal("_syscon", s.PanelName);
        Assert.Equal(P4ScreenRequest.Save, s.HandleKeys([Keys.S]));
        Assert.Null(s.SystemPage);
        Assert.Equal("_townmenu", s.PanelName);

        s.HandleKeys([Keys.S, Keys.Space]);
        Assert.Null(s.SystemPage);
        s.HandleKeys([Keys.S, Keys.E, Keys.E]);
        Assert.Equal(P4ScreenRequest.MainMenu, s.HandleKeys([Keys.Y]));
        s.HandleKeys([Keys.S, Keys.E, Keys.T]);
        Assert.Equal(P4ScreenRequest.Title, s.HandleKeys([Keys.O])); // _acend(1): back to IV's title

        var map = new P4DungeonScreen(P4Preview.Start(4, Code)); // the location map's S
        while (map.Game.Dungeon.Prompt == P4DungeonPrompt.Wait)
            map.HandleKeys([Keys.Space]);
        map.HandleKeys([Keys.S]);
        Assert.Equal("_syscon", map.PanelName);
    }

    [OriginalDataFact]
    public void StreetWithoutParty_ShowsTheSystemIconAlone()
    {
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        game.Party.Members.Clear(); // _par+0C = 0: _townmenu's other branch
        Assert.Equal("_townmenu.no_party", s.PanelName);
    }

    [OriginalDataFact]
    public void Pack_LoadsTheIconRows_AndTheNoYesBox()
    {
        // s140: _seticon(36, 32 + 52 · i, 2, …) in the 150 × 48 window 36 (the profile pages, Party info at x 64) and
        // _noyes(win, 104, 300) in the shipwright's window (160, 32).
        var pack = Pack();
        var set = pack.CommandPanels!;
        Assert.Equal(["_detailmenu", "_p_info1menu", "_p_info3menu", "_page3"], set.Rows.Keys.Order());
        var page1 = set.Find("_detailmenu")!;
        // s141: window 36 at (314, 270) of the sheet's 472 × 338 area: 8 in from its right edge, 20 up from its bottom.
        Assert.Equal(new CommandPanelRow(150, 48, [32, 84], 2, 314, 270, 472, 338) with { IconX = page1.Row!.IconX }, page1.Row);
        Assert.All(set.Rows.Values, r => Assert.Equal((8, 20), (r.Row!.RightInset, r.Row.BottomInset)));
        Assert.Equal([(32, 2), (84, 2)], [set.IconAt(page1, 0), set.IconAt(page1, 1)]);
        Assert.Equal(["Space", "N"], page1.Icons.Select(i => i.Key));
        Assert.Equal([20, 23], page1.Icons.Select(i => i.Icon));
        Assert.Equal("p4x.0x42a3e", set.Find("_page3")!.Icons[1].Label); // "N:前頁へ"
        Assert.Equal((64, 2), set.IconAt(set.Find("_p_info1menu")!, 0));
        Assert.Same(set.Panels["_inspect"], set.Find("_inspect"));
        foreach (var icon in set.Rows.Values.SelectMany(p => p.Icons))
            Assert.NotNull(pack.Resolve($"p4.icon.{icon.Icon}"));

        var box = set.NoYes!;
        Assert.Equal((160, 32, 104, 300), (box.WindowX, box.WindowY, box.X, box.Y));
        Assert.Equal((-8, -8, 120, 32), box.Frame);
        Assert.Equal((-10, -10, 124, 36), box.Clear); // s141: _wfbox(win, 14, x − 10, y − 10, 124, 36)
        Assert.Equal((1, 15, 4, 14), (box.Dark, box.Light, box.ButtonColour, box.BackColour));
        Assert.Equal([("Y", 0, "YES"), ("N", 56, "NO")], box.Buttons.Select(b => (b.Key, b.X, GameText.S(b.Label))));
        Assert.All(box.Buttons, b => Assert.Equal((48, 16), (b.Width, b.Height)));
        Assert.Equal([Keys.Y, Keys.N], box.Buttons.Select(P4DungeonScreen.NoYesKey));
    }

    [Fact]
    public void IconRow_SitsAtTheSheetsLowerRight_InsideTheStrip()
    {
        // s141: the row keeps window 36's insets (8 right, 20 below) from the strip's right edge and the box's bottom.
        var row = new CommandPanelRow(150, 48, [32, 84], 2, 314, 270, 472, 338);
        Assert.Equal(new Vector2(316 - 79, 150 - 34), P4DungeonScreen.RowAt(row, 150, 0.5f, 316, 150, 236, 4));
        // A wider row never starts left of the strip, a short box never pushes it above the top.
        Assert.Equal(new Vector2(236, 4), P4DungeonScreen.RowAt(row, 170, 0.5f, 316, 20, 236, 4));
    }

    [Fact]
    public void FoundItemWindow_IsMsgsWindowOverTheField_AndGrowsForMoreLines()
    {
        // s141: _msg's window at (32, 112) of the squares, 272 × 48 for its two 16-dot lines; more lines make it taller
        // and move it up so it stays on the 280-row field.
        Assert.Equal((112, 48), P4DungeonScreen.MessageWindow(2, 16));
        Assert.Equal((112, 48), P4DungeonScreen.MessageWindow(1, 16));
        Assert.Equal((112, 80), P4DungeonScreen.MessageWindow(4, 16));
        var (y, h) = P4DungeonScreen.MessageWindow(14, 16);
        Assert.Equal(240, h);
        Assert.Equal(P4DungeonScreen.FieldSquaresHeight - 2 - h, y);
    }

    [OriginalDataFact]
    public void IconRows_LabelsNeverTouch_AndFitTheStrip()
    {
        // English "N:Previous page" centred under the second icon would cross "SPACE": it moves right, the row widens.
        var pack = Pack();
        var set = pack.CommandPanels!;
        var labelFont = FontSources.ForRole(pack, P4DungeonScreen.PanelLabelFontRole)!;
        int Width(CommandPanelIcon i)
        {
            try
            {
                return PixelFont.Measure(GameText.S(i.Label), labelFont);
            }
            catch (KeyNotFoundException)
            {
                return PixelFont.Measure(i.Text, labelFont);
            }
        }
        var strip = P4DungeonScreen.FittedPanelWidth(set, Width, p => 0);
        // In English every row still fits its 150-pixel window (the first label moves left), so the strip keeps its width.
        Assert.All(set.Rows.Values, r => Assert.Equal(150, P4DungeonScreen.RowLayout(r, set.IconSize, Width).Width));
        Assert.Equal(P4DungeonScreen.FittedPanelWidth(new CommandPanelSet
        {
            Window = set.Window, IconX = set.IconX, IconTop = set.IconTop, IconStep = set.IconStep, IconSize = set.IconSize,
            Panels = set.Panels,
        }, Width, p => 0), strip);
        foreach (var row in set.Rows.Values)
        {
            var (w, xs) = P4DungeonScreen.RowLayout(row, set.IconSize, Width);
            Assert.InRange(w, row.Row!.Width, strip);
            for (var i = 0; i < xs.Count; i++)
            {
                Assert.True(xs[i] >= P4DungeonScreen.PanelMargin && xs[i] + Width(row.Icons[i]) <= w - P4DungeonScreen.PanelMargin,
                    $"{row.Name} {i}");
                if (i > 0)
                    Assert.True(xs[i] >= xs[i - 1] + Width(row.Icons[i - 1]) + P4DungeonScreen.RowLabelGap, $"{row.Name} {i}");
            }
        }
        Assert.InRange(P4DungeonScreen.NoYesWidth(set.NoYes!), 0, strip);
        // Labels that fit stay where _seticon puts them.
        var narrow = P4DungeonScreen.RowLayout(set.Find("_detailmenu")!, 32, _ => 10);
        Assert.Equal(150, narrow.Width);
        Assert.Equal([32 + 16 - 5, 84 + 16 - 5], narrow.LabelX);
    }

    [OriginalDataFact]
    public void PartyMenus_ShowTheirPanels_IconsPressTheirKeys()
    {
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        var members = game.Party.Members.Count;

        // F: _inspect over the member pick; the sheet's pages show window 36's rows; N presses the page's N line.
        s.HandleKeys([Keys.F]);
        Assert.Equal("_inspect", s.PanelName);
        Assert.Equal(Keys.Space, s.PanelPress("_inspect", Pack().CommandPanels!.Find("_inspect")!.Icons[0]).Key);
        s.HandleKeys([Keys.D1]);
        Assert.Equal("_detailmenu", s.PanelName);
        var rows = Pack().CommandPanels!;
        CommandPanelIcon Row(string name, string key) => rows.Find(name)!.Icons.Single(i => i.Key == key);
        Assert.Equal((Keys.D1, (int?)null), s.PanelPress("_detailmenu", Row("_detailmenu", "N")));
        Assert.Equal((Keys.Space, (int?)null), s.PanelPress("_detailmenu", Row("_detailmenu", "Space")));
        s.HandleKeys([Keys.D1]); // N: page 2
        Assert.Equal((2, "_p_info3menu"), (game.Menus!.Profile!.Value.Page, s.PanelName));
        Assert.Equal((Keys.D6, (int?)null), s.PanelPress("_p_info3menu", Row("_p_info3menu", "N")));
        s.HandleKeys([Keys.D6]); // N: page 3
        Assert.Equal((3, "_page3"), (game.Menus.Profile!.Value.Page, s.PanelName));
        Assert.Equal((Keys.D1, (int?)null), s.PanelPress("_page3", Row("_page3", "N")));
        s.HandleKeys([Keys.Space]); // SPACE leaves the sheet
        Assert.Equal("_inspect", s.PanelName);
        s.HandleKeys([Keys.Space]);
        Assert.False(s.MenuOpen);

        // U: _setuse; its "L:Done" is the list's last line (past 9: the cursor, then Return).
        s.HandleKeys([Keys.U]);
        Assert.Equal("_setuse", s.PanelName);
        var done = P4Member.CarriedSlots + members;
        Assert.Equal((Keys.Enter, (int?)done), s.PanelPress("_setuse", Icon("_setuse", "L")));
        s.AimAt(done);
        s.HandleKeys([Keys.Enter]);
        Assert.False(s.MenuOpen);
        Assert.Equal("_townmenu", s.PanelName);

        // Outdoors: P shows Party info's SPACE row; the menu keeps the strip (the plain screen would drop it).
        s.HandleKeys([Keys.D7]);
        Assert.Equal(P4Place.Outdoors, game.Place);
        s.HandleKeys([Keys.P]);
        Assert.Equal(("_p_info1menu", true), (s.PanelName, s.PanelPlace));
        s.HandleKeys([Keys.Space]);
        Assert.Equal("_outdoormenu", s.PanelName);
    }

    [OriginalDataFact]
    public void Pack_LoadsTheWindow34Lists()
    {
        // s151 (FORMAT.md 6.9 "Lists in window 34"): _getmagic's own window and rows, _cast_47's rows under its panel,
        // creation's numbered lists and the Hall list.
        var lists = Pack().CommandPanels!.Lists;
        var spells = lists["_getmagic"];
        Assert.Equal((496, 32, 136, 360), spells.Window);
        Assert.Equal("p4x.0x44c4f", spells.Title); // "Spell List"
        Assert.Equal((0, 96, 136, 3, 1), spells.Rule);
        Assert.Equal((104, 16, 4, 18, false), (spells.Rows.Top, spells.Rows.Step, spells.Rows.KeyX, spells.Rows.TextX,
            spells.Rows.Digits));
        Assert.Equal([(8, 8, "Space", "p4x.0x44c42")], spells.Head.Select(h => (h.X, h.Y, h.Key, h.Label)));
        var pages = lists["_getmagic.8"].Head;
        Assert.Equal(["1", "2", "3", "4", "5", "6"], pages.Select(h => h.Key));
        Assert.Equal([0, 16, 32, 48, 64, 80], pages.Select(h => h.Y));
        Assert.Equal("p4x.0x44c38", pages[5].Label); // "6 :やめる"
        // s152: the combat list's category lines (_spelltop(0), 0x44A98[0-5]) over the same window and rows.
        var combat = lists["_getmagic.0"];
        Assert.Equal((496, 32, 136, 360), combat.Window);
        Assert.Equal(["1", "2", "3", "4", "5", "6"], combat.Head.Select(h => h.Key));
        Assert.Equal([0, 16, 32, 48, 64, 80], combat.Head.Select(h => h.Y));
        Assert.Equal(["p4x.0x44bc2", "p4x.0x44bfc"], new[] { combat.Head[0].Label, combat.Head[5].Label }); // 非攻撃系, やめる
        Assert.Equal((104, 18), (combat.Rows.Top, combat.Rows.TextX));
        var towns = lists["_cast_47"];
        Assert.Equal(("_cast_47", (int?)null), (towns.Panel, (int?)towns.Window?.X));
        Assert.Equal((60, 16, 136), (towns.Rows.Top, towns.Rows.TextX, towns.Rows.ButtonWidth));
        Assert.Equal((16, true, 135), (lists["_racemenu"].Rows.Top, lists["_racemenu"].Rows.Digits,
            lists["_racemenu"].Rows.ButtonWidth));
        Assert.True(lists["_rracemenu"].Rows.Digits && lists["_classmenu"].Rows.Digits);
        var hall = lists["_set_fame_lst"];
        Assert.Equal((24, 18, 96, 3), (hall.Rows.Top, hall.Rows.TextX, hall.Rows.ExtraX, hall.Rows.ExtraChars));
        Assert.Equal([(68, 8, "p4x.0x401da")], hall.Centred.Select(c => (c.X, c.Y, c.Label)));
        Assert.Equal('C', hall.Rows.KeyOf(2));
        Assert.Equal('3', lists["_classmenu"].Rows.KeyOf(2));
        Assert.Equal(48, lists["_classmenu"].Rows.YOf(2)); // 16 + 16 · 2
    }

    [OriginalDataFact]
    public void ListRows_PressTheirLine_AndTheHallRaceClearsLongNames()
    {
        Assert.Equal((Keys.Space, (int?)null), P4DungeonScreen.ListPress(-1)); // SPACE:Quit
        Assert.Equal((Keys.D1, (int?)null), P4DungeonScreen.ListPress(0));
        Assert.Equal((Keys.D9, (int?)null), P4DungeonScreen.ListPress(8));
        Assert.Equal((Keys.Enter, (int?)9), P4DungeonScreen.ListPress(9)); // past 9: the cursor there, then Return
        var hall = Pack().CommandPanels!.Lists["_set_fame_lst"].Rows;
        Assert.Equal(96, P4DungeonScreen.ListExtraX(hall, 64)); // 8 characters of 8 pixels: the original's x 96
        Assert.Equal(18 + 80 + P4DungeonScreen.ListColumnGap, P4DungeonScreen.ListExtraX(hall, 80)); // 10: moved right
    }

    [OriginalDataFact]
    public void ListsWidth_FitsTheLongestRow()
    {
        var set = Pack().CommandPanels!;
        static int Eight(string s) => 8 * s.Length;
        Assert.Equal(0, P4DungeonScreen.ListsWidth(new CommandPanelSet
        {
            Window = (0, 0, 136, 324), IconX = [38, 62], IconTop = 8, IconStep = 52, IconSize = 32,
            Panels = new Dictionary<string, CommandPanel>(),
        }, _ => [], Eight, (_, t) => t));
        // "Monster Evaluation" (18 characters) at x 18 in the spell list: 18 + 144 + 2.
        var w = P4DungeonScreen.ListsWidth(set, n => n.StartsWith("_getmagic", StringComparison.Ordinal)
            ? ["Monster Evaluation"] : [], Eight, (_, t) => t);
        Assert.Equal(18 + 144 + P4DungeonScreen.PanelMargin, w);
    }

    [OriginalDataFact]
    public void SpellList_ShowsInTheStrip_AndARowClickPicksTheSpell()
    {
        // s151: C, the caster, then _getmagic's window in the strip (no _mkicon panel); a row's click presses its digit.
        var game = P4Preview.StartInTown(2, Code);
        var caster = game.Party.Members[2];
        caster.Spells[0] = 1;
        caster.Mp = 10;
        var s = new P4DungeonScreen(game);
        s.HandleKeys([Keys.C]);
        Assert.Equal("_ocast", s.PanelName);
        s.HandleKeys([Keys.D3]);
        Assert.Equal(((string?)null, "_getmagic"), (s.PanelName, game.Menus!.ListWindow?.Name));
        var row = game.Menus.ListWindow!.Rows[0];
        Assert.Equal((Keys.D1, (int?)null), P4DungeonScreen.ListPress(row.Choice!.Value));
        s.HandleKeys([Keys.D1]); // Healing 1: whom?
        Assert.Equal(P4MenuPrompt.Who, game.Menus.Prompt);
    }
}
