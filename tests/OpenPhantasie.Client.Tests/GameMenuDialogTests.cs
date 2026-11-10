using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;

namespace OpenPhantasie.Client.Tests;

/// <summary>Esc's game menu (remaster): Resume / Save / Load / DOS import / export / Quit with a Y/N check.</summary>
public class GameMenuDialogTests
{
    [Fact]
    public void LineAfterClose_GivesBackTheLineTheTitleReplaced()
    {
        // s183: "GAME MENU" stayed on the message line after Esc / Resume.
        Assert.Equal("LEFT TOWN FOR THE WILDERNESS.",
            GameMenuDialog.LineAfterClose(GameMenuDialog.Title, "LEFT TOWN FOR THE WILDERNESS.", timedBand: false));
        Assert.Equal("", GameMenuDialog.LineAfterClose(GameMenuDialog.Title, "PELNOR", timedBand: true)); // no replay on the town band
        Assert.Equal("", GameMenuDialog.LineAfterClose(GameMenuDialog.Title, null, timedBand: false));
        Assert.Equal("SAVED.", GameMenuDialog.LineAfterClose("SAVED.", "PELNOR", timedBand: false)); // a pick's own line stays
    }

    [OriginalDataFact]
    public void Esc_Resumes_WithNoChoice()
    {
        var s = ClientSessions.Overworld();
        var d = new GameMenuDialog();
        d.Open();
        Assert.True(d.IsOpen);
        Assert.NotNull(d.HandleKeys([Keys.Escape], s));
        Assert.False(d.IsOpen);
        Assert.Null(d.TakeChoice());
    }

    [OriginalDataFact]
    public void Enter_PicksTheHighlightedEntry_Once()
    {
        var s = ClientSessions.Overworld();
        var d = new GameMenuDialog();
        d.Open();
        d.HandleKeys([Keys.Down, Keys.Down], s);
        Assert.Equal(">Load game        F9", d.Lines(s, ClientSessions.Data)[2 + 2]);
        d.HandleKeys([Keys.Enter], s);
        Assert.False(d.IsOpen);
        Assert.Equal(GameMenuChoice.Load, d.TakeChoice());
        Assert.Null(d.TakeChoice());
    }

    [OriginalDataFact]
    public void Quit_AsksFirst_NGoesBack_YQuits()
    {
        var s = ClientSessions.Overworld();
        var d = new GameMenuDialog();
        d.Open();
        d.HandleKeys([Keys.Up, Keys.Enter], s); // wraps to Quit game
        Assert.True(d.IsOpen);
        Assert.Contains(GameMenuDialog.QuitQuestion, d.Lines(s, ClientSessions.Data));
        d.HandleKeys([Keys.N], s);
        Assert.True(d.IsOpen);
        Assert.Equal(GameMenuDialog.EntriesFor(s.Game).Length + 2, d.Lines(s, ClientSessions.Data).Count);
        d.HandleKeys([Keys.Enter, Keys.Y], s);
        Assert.False(d.IsOpen);
        Assert.Equal(GameMenuChoice.Quit, d.TakeChoice());
    }
}
