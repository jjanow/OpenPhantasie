using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>Read-only accessors the Client uses to follow a combat round (s36 Client catch-up).</summary>
public class SessionRoundAccessorTests
{
    private static GameSession WildernessFight()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(11));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Watcher", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.BeginWildernessCombat(data.Monsters.First(m => m.HpBase <= 8).Id);
        return session;
    }

    [OriginalDataFact]
    public void RoundFlags_FollowMoraleAndOption_AndResetAfterTheRound()
    {
        var session = WildernessFight();
        Assert.False(session.CombatMoraleCheckedThisRound);
        Assert.False(session.CombatOptionChosenThisRound);

        Assert.Equal(MoraleOutcome.Stand, session.StartCombatRound()); // no kills yet
        Assert.True(session.CombatMoraleCheckedThisRound);
        Assert.False(session.CombatOptionChosenThisRound);

        session.ChoosePartyOption(PartyOption.Fight);
        Assert.True(session.CombatOptionChosenThisRound);

        session.AutoMeleeRank0();
        session.ResolveCombatRound();
        Assert.False(session.CombatMoraleCheckedThisRound);
        Assert.False(session.CombatOptionChosenThisRound);
    }

    [OriginalDataFact]
    public void LogSince_ReturnsOnlyNewLines_EvenPastTheCap()
    {
        var session = WildernessFight();
        var mark = session.LogSequence;
        session.Note("A");
        session.Note("B");
        Assert.Equal(mark + 2, session.LogSequence);
        Assert.Equal(new[] { "A", "B" }, session.LogSince(mark));
        Assert.Empty(session.LogSince(session.LogSequence));

        mark = session.LogSequence;
        for (var i = 0; i < 250; i++)
            session.Note($"L{i}");
        var since = session.LogSince(mark);
        Assert.Equal(200, since.Count); // the oldest 50 fell out of the 200-line log
        Assert.Equal("L249", since[^1]);
        Assert.Equal(new[] { "L248", "L249" }, session.LogSince(session.LogSequence - 2));
    }
}
