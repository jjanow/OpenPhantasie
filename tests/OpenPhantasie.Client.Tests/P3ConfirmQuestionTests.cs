using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s160 (user s159): III's "Are you sure …?" questions before Load / Save / Distribute / a Purge pick under every III layout.
/// DOS III asks each through root <c>0x1820</c>(text, 2): "No" lit, Return takes it, Y / N, Esc = No. The Amiga asks the
/// same lines in window 1 (<c>0xaae2</c>): OK / CANCEL, Return = OK. One <see cref="P3ConfirmQuestion"/>, two looks.
/// </summary>
public class P3ConfirmQuestionTests
{
    private static readonly GameDataRepository P3Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));

    private static string P3(string id) => UiText.For(GameId.P3).Get(id);

    /// <summary>DOS III's own lines (S447, S390, the redistribute literal at DS:0x58e2, S407); the Amiga's are the same.</summary>
    [OriginalDataFact]
    public void Texts_AreDosIIIsLines_RowsSplitAtTheBreak()
    {
        Assert.Equal("Are you sure you want to load an old\rgame over this current one?", Game1.ConfirmText("load"));
        Assert.Equal("Are you sure you want to save this\rgame over your previously saved one?", Game1.ConfirmText("save"));
        Assert.Equal("Are you sure you want to redistribute?", Game1.ConfirmText("distribute"));
        Assert.Equal("Are you sure you want to purge\rthis character permanently?", Game1.ConfirmText("purge"));
        Assert.Equal(P3("p3bar.confirm_purge"), GuildPickDialog.PurgeQuestion); // shared lines: III's table has them
        var q = new P3ConfirmQuestion();
        q.Open(P3("p3bar.confirm_purge"), "purge");
        Assert.Equal(["Are you sure you want to purge", "this character permanently?"], q.Lines);
    }

    [Fact]
    public void Prompt_IsYesNo_NoLitEveryTime_EscIsNo()
    {
        var q = new P3ConfirmQuestion();
        Assert.False(q.Shown);
        Assert.Null(q.Prompt);
        q.Open("x\ry", "save");
        var p = q.Prompt!;
        Assert.True(p.IsYesNo);
        Assert.False(p.Remembers);
        Assert.Equal(2, p.InitialChoice);
        Assert.Equal(2, p.EscapeChoice);
        Assert.True(q.Owns(p));
        Assert.False(q.Owns(P3ConfirmQuestion.PromptFor("x\ry"))); // another question, even with the same text
        Assert.Equal(("save", true), q.Answer(1));
        Assert.False(q.Shown);
        q.Open("x", "load");
        Assert.Equal(("load", false), q.Answer(2));
    }

    /// <summary>Which look per layout: window 1 only for III under AMIGA SCREENS with the pack's window 1 and its buttons.</summary>
    [Theory]
    [InlineData(GameId.P3, true, true, true)]   // the Amiga bar (or any III Amiga screen)
    [InlineData(GameId.P3, false, true, false)] // AMIGA SCREENS off: the remaster's DOS row, DOS's prompt
    [InlineData(GameId.P3, true, false, false)] // a pack without window 1 (a CGA / other art pack): DOS's prompt
    [InlineData(GameId.P3, false, false, false)]
    [InlineData(GameId.P1, true, true, false)]
    public void InWindow_PerLayout(GameId game, bool originalScreens, bool packHasWindow1, bool expected) =>
        Assert.Equal(expected, P3ConfirmQuestion.InWindow(game, originalScreens, packHasWindow1));

    /// <summary>Under the Amiga look (window 1): Return / O / Y / Space = OK, C / N = CANCEL, Esc = CANCEL; others dropped.</summary>
    [Fact]
    public void WindowLook_ReturnIsOk()
    {
        var q = new P3ConfirmQuestion();
        var menu = new PromptMenu();
        q.Open("x", "save");
        Assert.Equal(1, q.Choice(Keys.Enter, inWindow: true, menu));
        Assert.Equal(1, q.Choice(Keys.O, inWindow: true, menu));
        Assert.Equal(2, q.Choice(Keys.C, inWindow: true, menu));
        Assert.Equal(2, q.Choice(Keys.Escape, inWindow: true, menu));
        Assert.Null(q.Choice(Keys.E, inWindow: true, menu));
        Assert.Null(q.Choice(Keys.Left, inWindow: true, menu)); // no highlight to move
    }

    /// <summary>
    /// Under every other III layout, DOS's two-item menu: "No" lit, so Return cancels; Left lights "Yes"; Y / N answer;
    /// Esc = No; the next question opens on "No" again (no memory, root 0x75a3).
    /// </summary>
    [Fact]
    public void DosLook_NoIsLit_ReturnCancels()
    {
        var q = new P3ConfirmQuestion();
        var menu = new PromptMenu();
        q.Open("x", "load");
        menu.Sync(q.Prompt);
        Assert.Equal(1, menu.CursorFor(q.Prompt!)); // "No"
        Assert.Equal(2, q.Choice(Keys.Enter, inWindow: false, menu));
        Assert.Null(q.Choice(Keys.Left, inWindow: false, menu));
        Assert.Equal(1, q.Choice(Keys.Enter, inWindow: false, menu));
        Assert.Equal(1, q.Choice(Keys.Y, inWindow: false, menu));
        Assert.Equal(2, q.Choice(Keys.N, inWindow: false, menu));
        Assert.Equal(2, q.Choice(Keys.Escape, inWindow: false, menu));
        Assert.Null(q.Choice(Keys.E, inWindow: false, menu));
        q.Answer(1);
        q.Open("x", "load"); // asked again: "No" lit again
        menu.Sync(q.Prompt);
        Assert.Equal(2, q.Choice(Keys.Enter, inWindow: false, menu));
    }

    /// <summary>
    /// Which commands ask: III's Distribute and Purge always; Load / Save in a town or the Guild (DOS III offers them only
    /// in the town's Options), so the game menu's and the F5 / F9 ones ask there too; I never asks.
    /// </summary>
    [Theory]
    [InlineData(GameId.P3, GameMode.Town, "load", true)]
    [InlineData(GameId.P3, GameMode.Town, "save", true)]
    [InlineData(GameId.P3, GameMode.Guild, "save", true)]
    [InlineData(GameId.P3, GameMode.Guild, "load", true)]
    [InlineData(GameId.P3, GameMode.Overworld, "save", false)]
    [InlineData(GameId.P3, GameMode.Dungeon, "load", false)]
    [InlineData(GameId.P3, GameMode.Town, "distribute", true)]
    [InlineData(GameId.P3, GameMode.Guild, "purge", true)]
    [InlineData(GameId.P1, GameMode.Town, "save", false)]
    [InlineData(GameId.P1, GameMode.Town, "distribute", false)]
    [InlineData(GameId.P1, GameMode.Guild, "purge", false)]
    [InlineData(GameId.P2, GameMode.Town, "load", false)]
    public void AsksFirst_IIIOnly(GameId game, GameMode mode, string action, bool expected) =>
        Assert.Equal(expected, Game1.AsksFirst(game, mode, action));

    private static GameSession P3InGuild()
    {
        var s = GameSessionFactory.Create(P3Data, new SeededDosRandomSource(4));
        s.StartNewGame();
        s.OpenGuild();
        return s;
    }

    /// <summary>
    /// III's Purge pick waits for S407 (the list steps aside while it asks): No keeps the member and the list is back, Yes
    /// purges; the question's text is DOS's. Same in every layout (the dialog holds no look).
    /// </summary>
    [OriginalDataFact]
    public void Purge_III_AsksFirst_NoKeeps_YesPurges()
    {
        var s = P3InGuild();
        var one = s.CreateAndEnroll("ONE", Race.Human, CharacterClass.Fighter);
        var count = s.Roster.Occupied.Count();
        var d = new GuildPickDialog();
        Assert.True(GuildPickDialog.AsksBeforePurge(GameId.P3));
        Assert.Null(d.OpenPurge(s));
        var at = s.Roster.Occupied.ToList().IndexOf(one);
        d.HandleKeys([.. Enumerable.Repeat(Keys.Down, at), Keys.Enter], s);
        Assert.Same(one, d.PurgeAsked);
        Assert.False(d.IsOpen); // the question owns the keys
        Assert.Contains(one, s.Roster.Occupied);

        Assert.Equal("", d.AnswerPurge(s, yes: false));
        Assert.Null(d.PurgeAsked);
        Assert.True(d.IsOpen); // the list again
        Assert.Contains(one, s.Roster.Occupied);

        d.HandleKeys([Keys.Enter], s);
        Assert.Same(one, d.PurgeAsked);
        var line = d.AnswerPurge(s, yes: true);
        Assert.Equal(s.LastMessage, line);
        Assert.DoesNotContain(one, s.Roster.Occupied);
        Assert.Equal(count - 1, s.Roster.Occupied.Count());
        Assert.Null(d.AnswerPurge(s, yes: true)); // nothing asked
    }

    /// <summary>I's Purge still deletes at once (OV1 0x23d5 has no question).</summary>
    [OriginalDataFact]
    public void Purge_I_NoQuestion()
    {
        Assert.False(GuildPickDialog.AsksBeforePurge(GameId.P1));
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(4));
        s.StartNewGame();
        s.OpenGuild();
        s.CreateAndEnroll("ONE", Race.Human, CharacterClass.Fighter);
        var d = new GuildPickDialog();
        Assert.Null(d.OpenPurge(s));
        d.HandleKeys([Keys.Enter], s);
        Assert.Null(d.PurgeAsked);
        Assert.Empty(s.Roster.Occupied);
    }

    /// <summary>III's Guild examines any roster member with its own key (DOS "Inspect", default I) under every layout.</summary>
    [Fact]
    public void Examine_IIIGuildKey()
    {
        Assert.True(GuildPickDialog.ExaminesRoster(GameId.P3));
        Assert.False(GuildPickDialog.ExaminesRoster(GameId.P1));
        Assert.Equal(Keys.I, new ClientSettings().GetKey(GameAction.GuildExamine));
    }
}
