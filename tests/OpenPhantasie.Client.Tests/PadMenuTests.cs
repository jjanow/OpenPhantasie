using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Importer;
using OpenPhantasie.Client.Updates;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// "Pad coverage" (s182): the main menu, Options, the import screen and the update screen driven by pad buttons only,
/// fed as <c>Game1.Update</c> feeds them (the shared <see cref="InputMap.MenuPadKeys"/>, or a screen's own PadKeys).
/// </summary>
public sealed class PadMenuTests
{
    private static IEnumerable<Keys> Pad(params Buttons[] buttons) => InputMap.MenuPadKeys(buttons);

    [Fact]
    public void MainMenu_Pad_PicksAGame_Options_AndQuit()
    {
        var menu = new MainMenuScreen(_ => true);
        Assert.Equal((MainMenuChoice.Play, GameId.P1), menu.HandleKeys(Pad(Buttons.A)));
        Assert.Equal((MainMenuChoice.Play, GameId.P3), menu.HandleKeys(Pad(Buttons.DPadDown, Buttons.LeftThumbstickDown, Buttons.A)));
        Assert.Equal((MainMenuChoice.Options, GameId.P1), menu.HandleKeys(Pad(Buttons.DPadDown, Buttons.DPadDown, Buttons.A)));
        Assert.Equal((MainMenuChoice.Quit, GameId.P1), menu.HandleKeys(Pad(Buttons.DPadDown, Buttons.A)));
        Assert.Equal((MainMenuChoice.Play, GameId.P4), menu.HandleKeys(Pad(Buttons.DPadUp, Buttons.DPadUp, Buttons.A)));
    }

    [Fact]
    public void Options_Pad_SwitchesTabs_TogglesARow_PagesAndCloses()
    {
        var settings = new ClientSettings();
        var o = new OptionsScreen(settings, _ => [], g => g == GameId.P1);
        o.Open();
        Assert.Null(o.Tab);
        o.HandleKeys(OptionsScreen.PadKeys([Buttons.DPadRight]));
        Assert.Equal(GameId.P1, o.Tab);
        o.HandleKeys(OptionsScreen.PadKeys([Buttons.DPadLeft]));
        Assert.Null(o.Tab);

        // General: row 2 is START MAXIMIZED; A and Right both step it on, Left back.
        var was = settings.StartMaximized;
        o.HandleKeys(OptionsScreen.PadKeys([Buttons.DPadDown, Buttons.DPadDown, Buttons.A]));
        Assert.Equal(2, o.Cursor);
        Assert.NotEqual(was, settings.StartMaximized);
        o.HandleKeys(OptionsScreen.PadKeys([Buttons.DPadLeft]));
        Assert.Equal(was, settings.StartMaximized);
        Assert.True(o.Changed);

        // I's tab: RB pages down past the first screen of rows, LB back up.
        o.HandleKeys(OptionsScreen.PadKeys([Buttons.DPadUp, Buttons.DPadUp, Buttons.DPadRight]));
        Assert.Equal(GameId.P1, o.Tab);
        o.HandleKeys(OptionsScreen.PadKeys([Buttons.RightShoulder]));
        Assert.True(o.Cursor > 1);
        o.HandleKeys(OptionsScreen.PadKeys([Buttons.LeftShoulder]));
        Assert.Equal(0, o.Cursor);

        Assert.True(o.HandleKeys(OptionsScreen.PadKeys([Buttons.B])));
        Assert.False(o.IsOpen);
    }

    [Fact]
    public void Options_PadKeys_TriggersScrollTheDescription_TheRestIsTheSharedMap()
    {
        Assert.Equal([Keys.OemOpenBrackets, Keys.OemCloseBrackets],
            OptionsScreen.PadKeys([Buttons.LeftTrigger, Buttons.RightTrigger]));
        Buttons[] shared = [Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight, Buttons.A, Buttons.B,
            Buttons.LeftShoulder, Buttons.RightShoulder, Buttons.X, Buttons.Start];
        Assert.Equal(InputMap.MenuPadKeys(shared), OptionsScreen.PadKeys(shared));

        var o = new OptionsScreen(new ClientSettings(), _ => [], g => g == GameId.P1);
        o.Open();
        o.DetailBox.Set(total: 20, visible: 5);
        o.HandleKeys(OptionsScreen.PadKeys([Buttons.RightTrigger, Buttons.RightTrigger]));
        Assert.Equal(2, o.DetailBox.Offset);
        o.HandleKeys(OptionsScreen.PadKeys([Buttons.LeftTrigger]));
        Assert.Equal(1, o.DetailBox.Offset);
    }

    [Fact]
    public void Options_Pad_OpensImportAndUpdate()
    {
        var o = new OptionsScreen(new ClientSettings(), _ => [], g => g == GameId.P1);
        o.Open();
        o.HandleKeys(OptionsScreen.PadKeys([Buttons.DPadDown, Buttons.DPadDown, Buttons.DPadDown]));
        Assert.True(o.HandleKeys(OptionsScreen.PadKeys([Buttons.A]))); // IMPORT GAMES
        Assert.True(o.ImportRequested);
        o.Open();
        o.HandleKeys(OptionsScreen.PadKeys([Buttons.DPadUp])); // wraps to the last row, UPDATE NOW
        Assert.True(o.HandleKeys(OptionsScreen.PadKeys([Buttons.A])));
        Assert.True(o.UpdateRequested);
    }

    [Fact]
    public void Import_Pad_AddsALibrary_RemovesItWithX_ImportsAndLeaves()
    {
        var calls = new List<IReadOnlyList<string>>();
        var s = new ImportScreen(Path.Combine(Path.GetTempPath(), "phantasie-import-screen-test"),
            (paths, _) => { calls.Add(paths); return new ImportReport(); },
            () => [new StoreLibraries.Library("Steam", "/steam/common")]);
        s.Open(firstRun: false);

        s.HandleKeys(s.PadKeys([Buttons.DPadDown, Buttons.A])); // the Steam row: added
        Assert.Equal(["/steam/common"], s.Paths);
        Assert.Equal(1, s.Cursor); // now on the added path's row
        s.HandleKeys(s.PadKeys([Buttons.X])); // removed
        Assert.Empty(s.Paths);

        s.HandleKeys(s.PadKeys([Buttons.A, Buttons.DPadDown, Buttons.A])); // add it, then IMPORT (an added library leaves its row)
        for (var i = 0; i < 500 && !s.ShowingReport; i++)
        {
            Thread.Sleep(10);
            s.Update();
        }
        Assert.True(s.ShowingReport);
        Assert.Equal([["/steam/common"]], calls);
        s.HandleKeys(s.PadKeys([Buttons.A])); // the report goes
        Assert.False(s.ShowingReport);
        Assert.True(s.HandleKeys(s.PadKeys([Buttons.B])));
        Assert.False(s.IsOpen);
    }

    [Fact]
    public void Import_PadKeys_XIsDelete_YBrowses_TheRestIsTheSharedMap()
    {
        var s = new ImportScreen(Path.Combine(Path.GetTempPath(), "phantasie-import-screen-test"), (_, _) => new ImportReport(), () => []);
        s.Open(firstRun: false);
        Assert.False(s.PadInUse);
        Assert.Equal([Keys.Delete], s.PadKeys([Buttons.X]));
        Assert.True(s.PadInUse);
        Assert.Equal([Keys.Tab], s.PadKeys([Buttons.Y])); // s183: the folder browser
        Buttons[] shared = [Buttons.DPadUp, Buttons.DPadDown, Buttons.A, Buttons.B, Buttons.LeftShoulder,
            Buttons.RightShoulder, Buttons.Start];
        Assert.Equal(InputMap.MenuPadKeys(shared), s.PadKeys(shared));
    }

    private static readonly UpdateRelease Found =
        new(new Version(1, 2, 0), "v1.2.0", "## New\n\n* one\n* two", "", "OpenPhantasie-1.2.0-linux-x64.tar.gz", "u", 1);

    [Fact]
    public void Update_Pad_LaterWithTheDPad_OrBClose()
    {
        var s = new UpdateScreen(_ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), true);
        s.OpenWith(new UpdateCheckResult(UpdateCheckOutcome.Available, Found, Found.Version));
        s.HandleKeys(Pad(Buttons.DPadDown));
        Assert.Equal(1, s.Cursor);
        Assert.True(s.HandleKeys(Pad(Buttons.A)));
        Assert.False(s.IsOpen);

        s.OpenWith(new UpdateCheckResult(UpdateCheckOutcome.Available, Found, Found.Version));
        Assert.True(s.HandleKeys(Pad(Buttons.B)));
        Assert.False(s.RestartRequested);
    }

    [Fact]
    public void Update_Pad_ANowInstalls_ThenARestarts()
    {
        var s = new UpdateScreen(_ => throw new InvalidOperationException(),
            (_, p) => { p.Report(1.0); return Task.CompletedTask; }, isRelease: true);
        s.OpenWith(new UpdateCheckResult(UpdateCheckOutcome.Available, Found, Found.Version));
        Assert.False(s.HandleKeys(Pad(Buttons.A)));
        for (var i = 0; i < 500 && s.Current != UpdateScreen.State.Installed; i++)
        {
            Thread.Sleep(10);
            s.Update();
        }
        Assert.True(s.HandleKeys(Pad(Buttons.A)));
        Assert.True(s.RestartRequested);
    }
}
