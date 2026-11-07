using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s154: the overworld's magical pool and wilderness inn reports are marked as one <see cref="LogWindowKind.Report"/> window
/// (I Amiga: requester 21 with the title and OK, h71 <c>+0xa50</c> / <c>+0xca2</c> → h86 <c>+0(0x15)</c>); DOS's log is
/// unchanged (the pool's lines one by one, the inn's one joined line).
/// </summary>
public class OverworldReportWindowTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    private static GameSession Party(out Character hero)
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        session.OpenGuild();
        hero = session.CreateAndEnroll("BATHER", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        return session;
    }

    [OriginalDataFact]
    public void APool_LogsItsLines_AndMarksThemOneReport()
    {
        var s = Party(out _);
        var mark = s.LogSequence;
        s.Campaign.OnSpecialSite(s, new OverworldMoveResult { Kind = OverworldMoveKind.SpecialSite, Tile = 111 });
        var lines = s.LogSince(mark);
        Assert.Equal([MagicalPool.Banner, "BATHER    IS CHANGED"], lines);
        var w = Assert.Single(s.MapWindowsSince(mark));
        Assert.Equal((mark, 2, LogWindowKind.Report), (w.AfterLine, w.Count, w.Kind));
        Assert.Null(w.Rows);
    }

    [OriginalDataFact]
    public void TheInn_LogsOneJoinedLine_AndMarksAReportWithItsRows()
    {
        var s = Party(out var hero);
        s.Party.GoldCarried = 300;
        (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (1, 6, 13);
        s.MoveOverworld(CardinalDirection.East); // onto the inn (tile 41), as TravelTimeTests
        var mark = s.LogSequence;
        var r = s.StayAtWildernessInn();
        Assert.Equal(TownTransactionKind.Ok, r.Kind);
        Assert.Equal([r.Message], s.LogSince(mark)); // DOS's band text, unchanged
        Assert.Equal(["IN THE INN:", "BATHER    IS RESTING"], r.Lines);
        var w = Assert.Single(s.MapWindowsSince(mark));
        Assert.Equal((mark, 1, LogWindowKind.Report), (w.AfterLine, w.Count, w.Kind));
        Assert.Equal(r.Lines, w.Rows);
        Assert.Equal(hero.HpMax, hero.HpCur);
    }

    [OriginalDataFact]
    public void AnInnThatRefuses_MarksNoWindow()
    {
        var s = Party(out _);
        s.Party.GoldCarried = 0;
        (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (1, 6, 13);
        s.MoveOverworld(CardinalDirection.East);
        var mark = s.LogSequence;
        Assert.Equal(TownTransactionKind.Failed, s.StayAtWildernessInn().Kind);
        Assert.Empty(s.MapWindowsSince(mark));
    }
}
