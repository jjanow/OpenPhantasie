using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s49 [C] (endgame.md §2.4): the only way to Olympus is TRANSPORTATION to the blank 12th picker entry (OLYMPIA), whose
/// TWNS exit tile is OUT18 (10,5); OUT18's tile-40 feature arg 99 is OLYMPIA too (OV2 0x611a).
/// </summary>
public class TransportationTests
{
    private static GameSession NewSessionInTown(out Character caster)
    {
        var session = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(3));
        session.OpenGuild();
        caster = session.CreateAndEnroll("Porter", Race.Human, CharacterClass.Wizard);
        Assert.True(session.TryAddToParty(caster, out _));
        caster.SpellsKnown[SpellId.Transportation] = 1;
        caster.MagicCur = caster.MagicMax = caster.MpCeiling = 20;
        session.EnterTown(1);
        return session;
    }

    [OriginalDataFact]
    public void Transportation_ToBlankTwelfthEntry_ExitsOnOut18()
    {
        var session = NewSessionInTown(out var caster);
        var mp = caster.MagicCur;

        Assert.True(session.CastSpell(caster, SpellId.Transportation).Success);
        Assert.True(session.TransportationPending);
        Assert.True(mp > caster.MagicCur); // spent before the pick

        Assert.True(session.ChooseTransportDestination(12));
        Assert.False(session.TransportationPending);
        Assert.Equal(12, session.CurrentTownId);
        Assert.True(session.LeaveTown());
        Assert.Equal((18, 10, 5), (session.Overworld.RegionId, session.Overworld.X, session.Overworld.Y));
    }

    [OriginalDataFact]
    public void Transportation_ToPhantasia_SellsRingI()
    {
        var session = NewSessionInTown(out var caster);
        session.CastSpell(caster, SpellId.Transportation);
        Assert.True(session.ChooseTransportDestination(8)); // PHANTASIA: no town tile on OUT14
        Assert.Equal(1, session.CurrentTown!.GetStock(149)); // RING I, TWNS.DAT stock[149] = 1, never wiped (≥ 120)
    }

    [OriginalDataFact]
    public void Transportation_Cancel_KeepsTownAndSpendsMp()
    {
        var session = NewSessionInTown(out var caster);
        var mp = caster.MagicCur;
        session.CastSpell(caster, SpellId.Transportation);
        Assert.True(session.ChooseTransportDestination(null));
        Assert.Equal(1, session.CurrentTownId);
        Assert.True(mp > caster.MagicCur);
        Assert.False(session.ChooseTransportDestination(2)); // nothing pending any more
    }

    [OriginalDataFact]
    public void UnansweredPick_DoesNotSurviveLeavingTown()
    {
        var session = NewSessionInTown(out var caster);
        session.CastSpell(caster, SpellId.Transportation);
        Assert.True(session.LeaveTown());
        session.EnterTown(1);
        Assert.False(session.TransportationPending);
        Assert.False(session.ChooseTransportDestination(12));
        Assert.Equal(1, session.CurrentTownId);
    }

    [OriginalDataFact]
    public void Transportation_LeavesOutTheCurrentTown_D062()
    {
        var session = NewSessionInTown(out var caster);
        Assert.Equal(Enumerable.Range(2, 11), session.TransportDestinations);
        session.CastSpell(caster, SpellId.Transportation);
        Assert.False(session.ChooseTransportDestination(1)); // "from one town to another"
        Assert.True(session.TransportationPending);
        Assert.True(session.ChooseTransportDestination(12));
    }

    [OriginalDataFact]
    public void ChooseTransportDestination_WithoutCast_IsRefused()
    {
        var session = NewSessionInTown(out _);
        Assert.False(session.ChooseTransportDestination(12));
        Assert.Equal(1, session.CurrentTownId);
    }

    [OriginalDataFact]
    public void Out18TownTile_Arg99_IsOlympia()
    {
        var traversal = GameDataRepository.LoadPhantasie1().CreateTraversal();
        var state = new OverworldState { RegionId = 18, X = 10, Y = 6 };
        var result = traversal.TryMove(state, CardinalDirection.North);
        Assert.Equal(OverworldMoveKind.EnterTown, result.Kind);
        Assert.Equal(99, result.FeatureArg);
        Assert.Equal(12, result.TownId);
    }
}
