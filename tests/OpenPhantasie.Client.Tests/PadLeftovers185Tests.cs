using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Importer;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s185 "Pad leftovers": the import screen's rows fill the screen with a scroll bar for more, its help follows
/// <see cref="PadWording"/>, I–III's menus read the left stick one direction per push, the title Guild's help names only
/// what works there, and the full-screen overworld has a help line.
/// </summary>
public sealed class PadLeftovers185Tests : IDisposable
{
    public void Dispose() => PadWording.Active = false;

    // --- Import: rows down to the help line, a scroll bar for more ----------------------------------------------------

    [Fact]
    public void MenuScreen_HoldsFifteenLinesUnderTheTitle_ClearOfTheHelpLine()
    {
        const int helpY = 186;
        var last = ImportScreen.FirstLineY + (ImportScreen.ScreenLines - 1) * ImportScreen.LinePitch;
        Assert.True(last + PixelFont.GlyphHeight < helpY);
        Assert.True(last + ImportScreen.LinePitch + PixelFont.GlyphHeight >= helpY); // one more would touch it
        var area = UiOverlays.ImportRowsArea(1, ImportScreen.ScreenLines);
        Assert.Equal(ImportScreen.FirstLineY - 1, area.Y);
        Assert.True(area.Bottom <= helpY && area.Right < 313); // inside the panel, above the help
    }

    /// <summary>A scratch folder with <paramref name="files"/> files (f00 …).</summary>
    private static string Folder(int files)
    {
        var root = Path.Combine(Path.GetTempPath(), "phantasie-import-rows-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        for (var i = 0; i < files; i++)
            File.WriteAllBytes(Path.Combine(root, $"f{i:00}.adf"), [0]);
        return root;
    }

    [Fact]
    public void Browser_FillsTheScreen_BarWhenLonger_EveryRowReachable_MouseRowsLineUp()
    {
        var root = Folder(30);
        try
        {
            var s = new ImportScreen(Path.Combine(Path.GetTempPath(), "phantasie-import-screen-test"), (_, _) => new ImportReport(),
                () => [], browseHome: root);
            s.Open(firstRun: false);
            s.HandleKeys([Keys.Tab]);
            Assert.True(s.Browsing);
            var lines = s.Lines();
            Assert.Equal(1 + ImportScreen.ScreenLines, lines.Count); // the title, then the screen's 15 lines, all used
            var rows = s.VisibleLines;
            Assert.True(rows > 8, $"{rows} rows");
            Assert.Equal(lines.Count - s.BodyLine, rows);
            Assert.DoesNotContain(lines, l => l.Text == " ...");
            Assert.True(s.Scroll.CanScroll);
            Assert.Equal(s.BrowseEntries.Count, s.Scroll.Total);
            Assert.Equal(0, s.RowOfLine(s.BodyLine));
            Assert.Equal(rows - 1, s.RowOfLine(s.BodyLine + rows - 1));
            Assert.Null(s.RowOfLine(s.BodyLine + rows));

            // Down to the last entry: it shows, and the mouse rows follow the scroll.
            var last = s.BrowseEntries.Count - 1;
            while (s.Cursor != last)
                s.HandleKeys([Keys.Down]);
            lines = s.Lines();
            Assert.Equal(">f29.adf", lines[^1].Text);
            Assert.Equal(s.Scroll.MaxOffset, s.Scroll.Offset);
            Assert.Equal(last, s.RowOfLine(lines.Count - 1));

            // The bar (or the wheel) moves the view without moving the cursor; the cursor's next step brings it back.
            s.Scroll.ScrollTo(0);
            Assert.Equal("f00.adf", s.Lines()[s.BodyLine + 2].Text.TrimStart(' ', '>'));
            s.HandleKeys([Keys.Up]);
            Assert.Equal(">f28.adf", s.Lines()[^1].Text);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Browser_ShortFolder_NoBar()
    {
        var root = Folder(2);
        try
        {
            var s = new ImportScreen(Path.Combine(Path.GetTempPath(), "phantasie-import-screen-test"), (_, _) => new ImportReport(),
                () => [], browseHome: root);
            s.Open(firstRun: false);
            s.HandleKeys([Keys.Tab]);
            Assert.Equal(s.BodyLine + s.BrowseEntries.Count, s.Lines().Count);
            Assert.False(s.Scroll.CanScroll);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Report_FillsTheRoomUnderTheGames_ScrollsToItsEnd()
    {
        var report = new ImportReport();
        for (var i = 0; i < 25; i++)
            report.Notes.Add($"note {i}");
        var s = new ImportScreen(Path.Combine(Path.GetTempPath(), "phantasie-import-screen-test"), (_, _) => report, () => []);
        s.Open(firstRun: false);
        s.AddPaths([Path.GetTempPath()]);
        s.HandleKeys([Keys.Down, Keys.Down, Keys.Enter]); // IMPORT
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!s.ShowingReport && DateTime.UtcNow < until)
        {
            Thread.Sleep(10);
            s.Update();
        }
        Assert.True(s.ShowingReport);
        var lines = s.Lines();
        Assert.Equal(1 + ImportScreen.ScreenLines, lines.Count);
        Assert.True(s.Scroll.CanScroll);
        for (var i = 0; i < 30; i++)
            s.HandleKeys([Keys.Down]);
        Assert.Equal(" note 24", s.Lines()[^1].Text);
        s.HandleKeys([Keys.PageUp, Keys.PageUp, Keys.PageUp]);
        Assert.Equal(" note 0", s.Lines()[s.BodyLine].Text);
    }

    // --- Import's help follows PadWording ----------------------------------------------------------------------------

    [Fact]
    public void ImportHelp_EveryKeyHelpHasItsPadTwin()
    {
        string[] ids = ["import.help", "import.help.typing", "import.help.path", "import.help.report", "import.help.report.offer",
            "import.help.offer", "import.help.browse"];
        foreach (var id in ids)
            Assert.Equal(UiText.Table.Get(id + PadWording.Suffix), PadWording.PadTextFor(UiText.Table, UiText.Table.Get(id)));
    }

    [Fact]
    public void ImportHelp_IsTheKeysText_DrawnInThePadsWordsWhileThePadIsTheInput()
    {
        var s = new ImportScreen(Path.Combine(Path.GetTempPath(), "phantasie-import-screen-test"), (_, _) => new ImportReport(), () => []);
        s.Open(firstRun: false);
        Assert.Equal(UiText.S("import.help.typing"), s.Help);
        s.HandleKeys(s.PadKeys([Buttons.DPadDown, Buttons.DPadDown])); // IMPORT, then DONE: the plain rows' help
        Assert.Equal(UiText.S("import.help"), s.Help); // the screen itself keeps the keys' text
        PadWording.Active = true;
        Assert.Equal(UiText.S("import.help.pad"), PadWording.Help(s.Help));
        PadWording.Active = false;
        Assert.Equal(UiText.S("import.help"), PadWording.Help(s.Help));
    }

    // --- I–III: the left stick, one direction per push ------------------------------------------------------------------

    private static GamePadState Stick(float x, float y) =>
        new(new GamePadThumbSticks(new Vector2(x, y), Vector2.Zero), new GamePadTriggers(0, 0), new GamePadButtons(),
            new GamePadDPad());

    /// <summary>Feeds <paramref name="frames"/> (16 ms each) to <paramref name="input"/> as the shell does; the stick
    /// buttons <see cref="InputMap.NewButtons"/> gave over them.</summary>
    private static List<Buttons> Run(InputMap input, params GamePadState[] frames)
    {
        var got = new List<Buttons>();
        foreach (var pad in frames)
        {
            input.TickStick(pad, 16);
            got.AddRange(input.NewButtons(pad).Where(StickQuantizer.IsStickButton));
            input.EndFrame(default, pad);
        }
        return got;
    }

    [Fact]
    public void MenuStick_ADiagonalCreepingPush_SendsOneDirection_NotUpThenRight()
    {
        var input = new InputMap(new ClientSettings());
        // Up first, the stick then swings to up-right: the raw buttons came as Up, then Right.
        var got = Run(input, Stick(0, 0.6f), Stick(0.3f, 0.8f), Stick(0.45f, 0.85f), Stick(0.45f, 0.85f), Stick(0.45f, 0.85f),
            Stick(0.45f, 0.85f), Stick(0.45f, 0.85f));
        Assert.Equal([Buttons.LeftThumbstickUp], got);
    }

    [Fact]
    public void MenuStick_HeldSendsOnce_LetGoAndPushAgainSendsAgain_FourWay()
    {
        var input = new InputMap(new ClientSettings());
        var held = Enumerable.Repeat(Stick(0.9f, 0), 20).ToArray();
        Assert.Equal([Buttons.LeftThumbstickRight], Run(input, held));
        Assert.Empty(Run(input, Stick(0, 0)));
        // A true diagonal still comes as one of four: never two buttons at once on a menu.
        Assert.Single(Run(input, Enumerable.Repeat(Stick(0.7f, -0.72f), 10).ToArray()));
        Assert.Empty(Run(input, Stick(0, 0)));
        Assert.Equal([Buttons.LeftThumbstickDown], Run(input, Enumerable.Repeat(Stick(0, -0.9f), 10).ToArray()));
    }

    [Fact]
    public void MenuStick_TheOtherButtonsStillComeAsPressed()
    {
        var input = new InputMap(new ClientSettings());
        var a = new GamePadState(new GamePadThumbSticks(), new GamePadTriggers(), new GamePadButtons(Buttons.A), new GamePadDPad());
        input.TickStick(a, 16);
        Assert.Equal([Buttons.A], input.NewButtons(a));
    }

    // --- The title Guild's help -----------------------------------------------------------------------------------------

    [Fact]
    public void TitleGuildHelp_NamesOnlyWhatWorksThere_AndHasItsPadWords()
    {
        foreach (var game in new[] { GameId.P1, GameId.P2, GameId.P3 })
        {
            var table = UiText.For(game);
            var help = table.Get("game.title_guild_help");
            Assert.DoesNotContain("G leave", help, StringComparison.Ordinal); // G has done nothing on this screen since s50
            Assert.Contains("A add", help, StringComparison.Ordinal);
            Assert.Contains("D drop", help, StringComparison.Ordinal);
            Assert.Equal(table.Get("game.title_guild_help.pad"), PadWording.PadTextFor(table, help));
        }
        // Its pad list is the same two commands (A opens it; B is the game menu, BackOut).
        var settings = new ClientSettings();
        Assert.Equal([settings.GetKey(GameAction.GuildAdd), settings.GetKey(GameAction.GuildDrop)],
            PadCommandDialog.EntriesFor(GameMode.Title, settings.GetKey, false, false).Select(e => e.Key));
    }

    // --- The full-screen overworld's help line -------------------------------------------------------------------------

    [Fact]
    public void MapOnlyHelp_FitsOneBandRow_InKeysAndPadWords()
    {
        foreach (var game in new[] { GameId.P1, GameId.P2, GameId.P3 })
        {
            var table = UiText.For(game);
            var keys = table.Get("game.map_only_help");
            var pad = table.Get("game.map_only_help.pad");
            Assert.Equal(pad, PadWording.PadTextFor(table, keys));
            Assert.Single(UiOverlays.Lines(keys, 52));
            Assert.Single(UiOverlays.Lines(pad, 52));
        }
    }

    [Fact]
    public void MapOnlyBand_DialogHelpAlways_MapHelpOnlyWhenAsked_AndKeepsTheBandToOneRow()
    {
        const string mode = "MODE HELP", map = "MAP HELP", dialog = "DIALOG HELP";
        // A dialog's help: always, under the message.
        Assert.Equal(("YOU SEE A TOWN", dialog), Game1.MapOnlyBand("YOU SEE A TOWN", true, dialog, mode, map, false));
        // The map's own: not for the keyboard with STATUS LINES off (D-077).
        Assert.Equal(("YOU SEE A TOWN", ""), Game1.MapOnlyBand("YOU SEE A TOWN", false, mode, mode, map, false));
        Assert.Equal(("", ""), Game1.MapOnlyBand("", false, mode, mode, map, false));
        // Asked for (pad / STATUS LINES): the map-only text, on the band's one row: after a new message's seconds.
        Assert.Equal(("YOU SEE A TOWN", ""), Game1.MapOnlyBand("YOU SEE A TOWN", true, mode, mode, map, true));
        Assert.Equal(("", map), Game1.MapOnlyBand("YOU SEE A TOWN", false, mode, mode, map, true));
        Assert.Equal(("", map), Game1.MapOnlyBand("", true, mode, mode, map, true));
    }

    [Fact]
    public void MapHudLines_HelpUnderTheMessage_OrOnItsRowWhenThereIsNone_NothingDropped()
    {
        Assert.Equal([("YOU SEE A TOWN", false)], UiOverlays.MapHudLines("YOU SEE A TOWN", null, null));
        Assert.Equal([("", false)], UiOverlays.MapHudLines("", null, "")); // as before: the band's one row
        Assert.Equal([("YOU SEE A TOWN", false), ("F2 party", true)], UiOverlays.MapHudLines("YOU SEE A TOWN", null, "F2 party"));
        Assert.Equal([("F2 party", true)], UiOverlays.MapHudLines("", null, "F2 party"));
        var longHelp = string.Join("  ", Enumerable.Range(0, 20).Select(i => $"K{i} do"));
        var rows = UiOverlays.MapHudLines("A\nB", "GOLD 123", longHelp);
        Assert.Equal(["A", "B"], rows.Where(r => !r.Help).Select(r => r.Text));
        Assert.True(rows.Count(r => r.Help) >= 2);
        Assert.Equal(longHelp.Replace(" ", ""), string.Concat(rows.Where(r => r.Help).Select(r => r.Text)).Replace(" ", ""));
    }
}
