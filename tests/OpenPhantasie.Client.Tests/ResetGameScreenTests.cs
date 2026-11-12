using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>P3 "Reset the game" (<c>docs/plan/16-reset-game.md</c>): the confirmation screen and the Options row.</summary>
public class ResetGameScreenTests
{
    private static readonly SaveSlot Slot2 = new(GameId.P1, 2, "/saves/savegame-p1-2.json");

    private static ResetGameScreen Open()
    {
        var r = new ResetGameScreen();
        r.Open(GameId.P1, Slot2, "2");
        return r;
    }

    [Fact]
    public void Warning_ThenKeep_ThenYes_RequestsAKeepReset()
    {
        var r = Open();
        Assert.Contains(r.Lines(), l => l.Text == UiText.F("reset.warning_slot_x", "2")); // DOS text: "..." in the public tree
        r.HandleKeys([Keys.Enter]); // KEEP THE CHARACTERS (first row)
        Assert.Contains(r.Lines(), l => l.Text.Contains("KEEP THE CHARACTERS?"));
        Assert.Equal(0, r.Cursor); // NO first
        Assert.Null(r.TakeRequest());
        r.HandleKeys([Keys.Down, Keys.Enter]); // YES
        Assert.Equal((Slot2, ResetMode.Keep), r.TakeRequest());
        Assert.Null(r.TakeRequest()); // once
    }

    [Fact]
    public void EraseKey_ThenNo_GoesBack_EscCloses()
    {
        var r = Open();
        r.HandleKeys([Keys.E]);
        Assert.Contains(r.Lines(), l => l.Text.Contains("ERASE THE CHARACTERS?"));
        r.HandleKeys([Keys.N]); // back to the choice, on ERASE
        Assert.Equal(1, r.Cursor);
        Assert.Contains(r.Lines(), l => l.Text.Contains("ERASE THE CHARACTERS") && !l.Text.Contains('?'));
        Assert.Null(r.TakeRequest());
        Assert.True(r.HandleKeys([Keys.Escape]));
        Assert.False(r.IsOpen);
    }

    [Fact]
    public void EscOnTheFirstQuestion_StepsBackToThePicker_OnlyWhenThePickerCameFirst()
    {
        // s205 (live on IV): Esc on Keep / Erase went to the main menu although the slot picker came first.
        var picked = Open(); // the picker's pick: a slot label
        picked.HandleKeys([Keys.E, Keys.Escape]); // YES / NO back to Keep / Erase: still open, no step back
        Assert.True(picked.IsOpen);
        Assert.False(picked.TakeBackToPicker());
        Assert.True(picked.HandleKeys([Keys.Escape]));
        Assert.True(picked.TakeBackToPicker());
        Assert.False(picked.TakeBackToPicker()); // once

        var single = new ResetGameScreen(); // SAVE SLOTS off: the game's one save, no label
        single.Open(GameId.P2, new SaveSlot(GameId.P2, 0, "/saves/savegame-p2.json"), null);
        Assert.True(single.HandleKeys([Keys.Escape]));
        Assert.False(single.TakeBackToPicker());

        var done = Open(); // the outcome page closes on any key, Esc too, without a step back
        done.ShowResult("DONE.");
        Assert.True(done.HandleKeys([Keys.Escape]));
        Assert.False(done.TakeBackToPicker());
    }

    [Fact]
    public void ErasePlusY_RequestsAPurge_AndTheOutcomeClosesOnAnyKey()
    {
        var r = Open();
        r.HandleKeys([Keys.E, Keys.Y]);
        Assert.Equal((Slot2, ResetMode.Purge), r.TakeRequest());
        r.ShowResult("DONE.");
        Assert.Contains(r.Lines(), l => l.Text == "DONE.");
        Assert.True(r.HandleKeys([Keys.Space]));
        Assert.False(r.IsOpen);
    }

    [Fact]
    public void Pad_AChooses_BGoesBack()
    {
        var r = Open();
        r.HandleKeys(ResetGameScreen.PadKeys([Buttons.A])); // KEEP
        r.HandleKeys(ResetGameScreen.PadKeys([Buttons.A])); // NO: back
        Assert.Null(r.TakeRequest());
        Assert.True(r.HandleKeys(ResetGameScreen.PadKeys([Buttons.B])));
    }

    [Fact]
    public void Click_ChoosesTheRowOnThatLine()
    {
        var r = Open();
        var lines = r.Lines();
        var erase = lines.ToList().FindIndex(l => l.Text.Contains("ERASE THE CHARACTERS"));
        r.Click(erase);
        lines = r.Lines();
        var yes = lines.ToList().FindIndex(l => l.Text.TrimStart('>', ' ') == "YES");
        r.Click(yes);
        Assert.Equal((Slot2, ResetMode.Purge), r.TakeRequest());
    }

    [Fact]
    public void Help_FitsTheMenuScreen_InEveryStep()
    {
        var r = Open();
        foreach (var step in new[] { Array.Empty<Keys>(), [Keys.Enter], [Keys.Down, Keys.Enter] })
        {
            r.HandleKeys(step);
            Assert.True(r.Help.Length <= SaveSlotScreen.Columns, r.Help);
        }
        Assert.True(UiText.S("reset.help_pad").Length <= SaveSlotScreen.Columns);
        Assert.True(UiText.S("reset.help_done").Length <= SaveSlotScreen.Columns);
    }

    [Fact]
    public void Lines_FitTheMenuScreen()
    {
        var r = Open();
        Assert.All(r.Lines(), l => Assert.True(l.Text.Length <= SaveSlotScreen.Columns, l.Text));
        Assert.True(r.Lines().Count <= SaveSlotScreen.ScreenLines + 2);
        r.HandleKeys([Keys.Enter]);
        Assert.All(r.Lines(), l => Assert.True(l.Text.Length <= SaveSlotScreen.Columns, l.Text));
        Assert.True(r.Lines().Count <= SaveSlotScreen.ScreenLines + 2);
    }

    [Fact]
    public void Options_ResetRow_FromTheMainMenuOnly()
    {
        var o = new OptionsScreen(new ClientSettings(), _ => [], g => g == GameId.P1);
        o.Open();
        o.HandleKeys([Keys.Right]);
        Assert.Equal(GameId.P1, o.Tab);
        Assert.Contains(o.RowTexts(), t => t.StartsWith("RESET GAME", StringComparison.Ordinal));
        for (var i = 0; i < 60 && !o.Lines().Any(l => l.Style == MenuLineStyle.Selected && l.Text.Contains("RESET GAME")); i++)
            o.HandleKeys([Keys.Down]);
        o.HandleKeys([Keys.Enter]);
        Assert.False(o.IsOpen);
        Assert.Equal(GameId.P1, o.ResetRequested);

        o.OpenFor(GameOptions.ForPreset(GameId.P1)); // in game: no reset (the running game may use the save)
        Assert.Null(o.ResetRequested);
        Assert.DoesNotContain(o.RowTexts(), t => t.StartsWith("RESET GAME", StringComparison.Ordinal));
    }

    [Fact]
    public void SlotPicker_ResetMode_RefusesAnEmptySlot_AndPicksASave()
    {
        var dir = "/saves";
        var slots = SaveSlots.All(dir, GameId.P1).Select(s => s.Number == 2
            ? new SaveSlotInfo(s, new DateTime(2026, 11, 9), new SaveSlotPreview(["DELTA"], SavePlace.Town, 1, 0))
            : new SaveSlotInfo(s, null, null)).ToArray();
        var screen = new SaveSlotScreen();
        screen.Open(GameId.P1, SaveSlotMode.Reset, slots);
        Assert.Equal("RESET GAME", screen.Lines()[0].Text);
        screen.HandleKeys([Keys.D1]);
        Assert.True(screen.IsOpen); // empty: refused
        Assert.True(screen.HandleKeys([Keys.D2]));
        Assert.Equal(2, screen.TakePick()!.Slot!.Number);
    }
}
