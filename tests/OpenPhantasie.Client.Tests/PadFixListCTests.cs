using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s190: the pad fix list's C 10-13 (<c>docs/plan/14-pad-fix-list.md</c>): the DOS save / transfer path's browser, III
/// Amiga's share window CANCEL on B, the fight's right-stick scrolls, and the left stick on the map, lists and diagonals.
/// </summary>
public class PadFixListCTests
{
    // --- C10: the DOS save / transfer path's browser -------------------------------------------------------------------

    private static string Tree()
    {
        var root = Directory.CreateTempSubdirectory("pad-c10-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "older"));
        File.WriteAllText(Path.Combine(root, "a-old.json"), "not a save");
        File.WriteAllText(Path.Combine(root, "b-new.json"), "not a save");
        return root;
    }

    private static string? Keys_(DosSaveDialog d, params Keys[] keys) => d.HandleKeys(keys, null!, null!);

    private static string? Pad(DosSaveDialog d, params Buttons[] buttons) => d.HandleKeys(d.PadKeys(buttons), null!, null!);

    [Fact]
    public void Transfer_PadYBrowses_FromTheFileShown_XPicksAnotherFile_AndReadsIt()
    {
        var root = Tree();
        try
        {
            var shown = Path.Combine(root, "b-new.json");
            var d = new DosSaveDialog(new ClientSettings());
            d.OpenTransfer(shown, GameId.P1);
            Keys_(d, Keys.Y); // the erase question
            Assert.Equal(DosSaveDialog.Step.EnterPath, d.Current);
            Assert.Contains("Y browse", UiText.S("transfer.type_file_del_clear_enter_ok.pad"));

            Pad(d, Buttons.Y);
            Assert.Equal(DosSaveDialog.Step.Browse, d.Current);
            Assert.Equal(root, d.Browser.Folder);
            Assert.Equal(shown, d.Browser.Entries[d.Browser.Cursor].Path); // the cursor starts on the file shown
            Assert.Equal(UiText.S("import.help.browse"), d.Help);

            Pad(d, Buttons.DPadUp);
            Assert.Equal("a-old.json", d.Browser.Entries[d.Browser.Cursor].Name);
            Pad(d, Buttons.X);
            Assert.Equal(Path.Combine(root, "a-old.json"), d.Path);
            Assert.Equal(DosSaveDialog.Step.ShowWarnings, d.Current); // read (and refused: not a save)
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Transfer_PickingAFolder_OpensIt_BGoesBackToThePath()
    {
        var root = Tree();
        try
        {
            var d = new DosSaveDialog(new ClientSettings());
            d.OpenTransfer(Path.Combine(root, "b-new.json"), GameId.P1);
            Keys_(d, Keys.Y);
            Keys_(d, Keys.Tab); // the keyboard's way in
            var older = d.Browser.Entries.ToList().FindIndex(e => e.Kind == FileBrowser.EntryKind.Folder);
            while (d.Browser.Cursor != older)
                Pad(d, Buttons.DPadDown);
            Pad(d, Buttons.X);
            Assert.Equal(DosSaveDialog.Step.Browse, d.Current); // a transfer wants a file: the folder opens
            Assert.Equal(Path.Combine(root, "older"), d.Browser.Folder);
            Pad(d, Buttons.Y);
            Assert.Equal(root, d.Browser.Folder);
            Pad(d, Buttons.B);
            Assert.Equal(DosSaveDialog.Step.EnterPath, d.Current);
            Assert.Equal(Path.Combine(root, "b-new.json"), d.Path); // nothing picked: the path stays
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Browser_PagesALongFolder_TheCursorRowAlwaysShows()
    {
        var root = Directory.CreateTempSubdirectory("pad-c10-long-").FullName;
        try
        {
            for (var i = 0; i < 20; i++)
                File.WriteAllText(Path.Combine(root, $"slot{i:00}.json"), "");
            var d = new DosSaveDialog(new ClientSettings());
            d.OpenTransfer(Path.Combine(root, "slot00.json"), GameId.P1);
            Keys_(d, Keys.Y);
            Pad(d, Buttons.Y);
            for (var i = 0; i < 15; i++)
                Pad(d, Buttons.DPadDown);
            Assert.Equal("slot15.json", d.Browser.Entries[d.Browser.Cursor].Name);
            Assert.Contains(">slot15.json", d.Lines);
            Assert.Equal(2 + DosSaveDialog.BrowseRowsPerPage, d.Lines.Count);
            Assert.NotNull(d.Page); // the bar beside the rows
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DosImportPath_HelpNamesTheBrowser()
    {
        var help = UiText.For(GameId.P1);
        Assert.Contains("Tab browse", help.Get("dossave.type_folder_del_clear_enter_ok"));
        Assert.Contains("Y browse", help.Get("dossave.type_folder_del_clear_enter_ok.pad"));
    }

    // --- C11: III Amiga's share window, B cancels ----------------------------------------------------------------------

    [Fact]
    public void ShareWindowHelp_NamesBCancel()
    {
        Assert.EndsWith("B cancel", UiText.S("p3town.bank_window_shares_help.pad"));
        Assert.Contains("C/Esc cancel", UiText.S("p3town.bank_window_shares_help"));
    }

    // --- C12: the fight's right stick ----------------------------------------------------------------------------------

    [Theory]
    [InlineData(Buttons.RightThumbstickUp, false, Keys.PageUp)]
    [InlineData(Buttons.RightThumbstickDown, false, Keys.PageDown)]
    [InlineData(Buttons.RightThumbstickUp, true, Keys.Home)]
    [InlineData(Buttons.RightThumbstickDown, true, Keys.End)]
    public void Fight_RightStick_ScrollsTheLog_WithRtTheRankBox(Buttons button, bool rt, Keys key)
    {
        var pad = new CombatPad();
        Assert.Equal([key], pad.KeysFor(new CombatPadStep("any", []), [button], rt));
    }

    [Fact]
    public void Fight_RightStickSideways_SendsNothing_ItIsThePadsF2()
    {
        var pad = new CombatPad();
        Assert.Empty(pad.KeysFor(new CombatPadStep("any", []), [Buttons.RightThumbstickLeft, Buttons.RightThumbstickRight]));
    }

    // --- C13: the left stick on the map and lists ----------------------------------------------------------------------

    private static GamePadState Stick(float x, float y) =>
        new(new GamePadThumbSticks(new Vector2(x, y), Vector2.Zero), new GamePadTriggers(0, 0), new GamePadButtons(),
            new GamePadDPad());

    /// <summary>The field move the stick held at (x, y) gives over a few frames, as the shell reads it.</summary>
    private static (int, int)? FieldMove(float x, float y, bool diagonals)
    {
        var input = new InputMap(new ClientSettings());
        (int, int)? got = null;
        for (var i = 0; i < 10 && got is null; i++)
        {
            var pad = Stick(x, y);
            input.TickStick(pad, 16);
            got = input.PressedFieldMove(default, pad, diagonals);
            input.EndFrame(default, pad);
        }
        return got;
    }

    [Fact]
    public void LeftStick_StepsTheMap_FourWays_OrOnASlantWithDiagonals()
    {
        Assert.Equal((0, -1), FieldMove(0, 0.9f, diagonals: false));
        Assert.Equal((1, 0), FieldMove(0.9f, 0, diagonals: true));
        Assert.Equal((1, -1), FieldMove(0.7f, 0.7f, diagonals: true)); // up-right = north-east (y grows south)
        Assert.Equal((-1, 1), FieldMove(-0.7f, -0.7f, diagonals: true));
        var straight = FieldMove(0.7f, 0.7f, diagonals: false); // option off: one of four
        Assert.NotNull(straight);
        Assert.True(straight.Value.Item1 == 0 || straight.Value.Item2 == 0);
    }

    [Fact]
    public void LeftStick_IsAListStep_OncePerPush()
    {
        var input = new InputMap(new ClientSettings());
        var steps = 0;
        for (var i = 0; i < 20; i++)
        {
            var pad = Stick(0, -0.9f);
            input.TickStick(pad, 16);
            if (input.PressedMove(Core.CardinalDirection.South, default, pad))
                steps++;
            input.EndFrame(default, pad);
        }
        Assert.Equal(1, steps);
    }
}
