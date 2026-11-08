using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s162: III Amiga's window 1 (<c>0x1d77e</c> / <c>0xaae2</c>: the text as the window's title, OK / CANCEL). <c>0x854</c>
/// prints the title in the screen font, topaz 8, one 8-px row per "\r" (the s125 / s128 shots match the ROM glyphs), and
/// Guild › Add's move question (S445, <c>0xd24c</c>) is asked there too.
/// </summary>
public class P3WindowOneTests
{
    private static readonly GameDataRepository P3Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));

    private static KeepWindowsLayout.Window WindowOne =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")!.KeepWindows!.Dungeon!;

    [OriginalDataFact]
    public void WindowOne_TopazRows8PxApart_FromTheTitleCell()
    {
        var w = WindowOne;
        Assert.Equal("amiga-topaz-8", w.Font);
        Assert.Equal(8, UiOverlays.KeepWindowRowPitch(w));
        var rows = UiOverlays.KeepWindowRows(w, ["Are you sure you want to load an old", "game over this current one?"]);
        Assert.Equal([(8, 80, "Are you sure you want to load an old"), (8, 88, "game over this current one?")], rows);
    }

    /// <summary>A line wider than the box (38 topaz cells from x 8) goes on at a word on the next row, never cut.</summary>
    [OriginalDataFact]
    public void WindowOne_LongLineWrapsAtAWord_NothingCut()
    {
        const string line = "Bartholomewsson hears monsters. Continue?"; // 41 characters
        var rows = UiOverlays.KeepWindowRows(WindowOne, [line]);
        Assert.Equal(2, rows.Count);
        Assert.Equal((8, 88), (rows[1].X, rows[1].Y));
        Assert.Equal(line, string.Join(" ", rows.Select(r => r.Text)));
        Assert.All(rows, r => Assert.True(r.Text.Length <= 38, r.Text));
    }

    [Fact]
    public void AWindowWithoutAFont_KeepsTheRemastersTenPxRows()
    {
        var w = new KeepWindowsLayout.Window([0, 72, 319, 140], [(8, 80)], (72, 115), (192, 115));
        Assert.Equal(10, UiOverlays.KeepWindowRowPitch(w));
        Assert.Equal([(8, 80, "a"), (8, 90, "b")], UiOverlays.KeepWindowRows(w, ["a", "b"]));
    }

    private static (GameSession, Character) P3GuildWithAwayMember()
    {
        var s = GameSessionFactory.Create(P3Data, new SeededDosRandomSource(4));
        s.StartNewGame();
        s.OpenGuild();
        var away = s.CreateAndEnroll("AWAYO", Race.Human, CharacterClass.Fighter);
        away.CurrentTown = away.CurrentTown == 3 ? 4 : 3; // lives in another town
        return (s, away);
    }

    /// <summary>III asks S445 in its own words (DOS ovl2 <c>0x3b75</c> and the Amiga's <c>0xd24c</c> print the same text).</summary>
    [OriginalDataFact]
    public void GuildAdd_III_AsksS445_RowsSplitAtTheBreak()
    {
        var (s, away) = P3GuildWithAwayMember();
        Assert.True(s.AddNeedsMoveConfirm(away));
        Assert.Equal(["It will take this character two", "months to make the journey here."],
            GuildAddDialog.MoveConfirmPromptFor(GameId.P3).Lines);
        var d = new GuildAddDialog(new PromptMenu(), P3Data);
        Assert.Null(d.Open(s));
        d.HandleKeys([Keys.Enter], s);
        Assert.True(d.Confirming);
        var lines = d.Lines(s, P3Data);
        Assert.Equal(["It will take this character two", "months to make the journey here."], lines.Take(2));
        Assert.Equal(GuildHall.AddMoveConfirmText, GuildAddDialog.MoveConfirmPromptFor(GameId.P1).Lines); // I keeps menu 9
    }

    /// <summary>
    /// Under AMIGA SCREENS the question is window 1: no highlight, Return / O / Y = OK (added), C / N / Esc = CANCEL; the
    /// arrows do nothing; the help line names the window's keys.
    /// </summary>
    [OriginalDataFact]
    public void GuildAdd_III_InWindowOne_ReturnIsOk_ArrowsDoNothing()
    {
        var (s, away) = P3GuildWithAwayMember();
        var d = new GuildAddDialog(new PromptMenu(), P3Data) { AmigaWindow = true };
        d.Open(s);
        d.HandleKeys([Keys.Enter], s);
        Assert.True(d.Confirming);
        Assert.Equal(UiText.For(GameId.P3).Get("game.keep_window_ok_cancel"), d.Help);
        d.HandleKeys([Keys.Right, Keys.Left, Keys.Down], s);
        Assert.True(d.Confirming);
        d.HandleKeys([Keys.C], s); // CANCEL
        Assert.False(d.Confirming);
        Assert.DoesNotContain(away, s.Party.Members);
        d.HandleKeys([Keys.Enter], s);
        d.HandleKeys([Keys.Escape], s); // Esc = CANCEL
        Assert.DoesNotContain(away, s.Party.Members);
        d.HandleKeys([Keys.Enter], s);
        d.HandleKeys([Keys.O], s); // a click on OK sends O
        Assert.Contains(away, s.Party.Members);
    }

    /// <summary>Off (the DOS look): the Yes / No panel with "Yes" lit (root 0x1820 with highlight 1); Right then Return = No.</summary>
    [OriginalDataFact]
    public void GuildAdd_III_DosLook_YesLit_RightReturnIsNo()
    {
        var (s, away) = P3GuildWithAwayMember();
        var d = new GuildAddDialog(new PromptMenu(), P3Data);
        d.Open(s);
        d.HandleKeys([Keys.Enter], s);
        Assert.Equal(UiText.S("guild.arrows_enter_or_y_yes_n"), d.Help);
        d.HandleKeys([Keys.Right, Keys.Enter], s);
        Assert.DoesNotContain(away, s.Party.Members);
        d.HandleKeys([Keys.Enter], s); // the list's pick opens the question (the rest of a frame's keys wait)
        d.HandleKeys([Keys.Enter], s); // "Yes" lit again: no memory
        Assert.Contains(away, s.Party.Members);
    }
}
