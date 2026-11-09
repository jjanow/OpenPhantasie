using OpenPhantasie.Data;
using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// D3-035 QoL <c>p3.transport-planes</c>: off (every preset) = DOS's 14 Scandor towns; <c>visited</c> = also each plane town
/// 15–17 a member of the party has entered (a per-character bit, <see cref="Phantasie3CharacterData.PlaneTownsVisited"/>).
/// </summary>
public class P3TransportPlanesTests
{
    private static readonly int[] Scandor = Enumerable.Range(1, 14).ToArray();

    private static GameSession Visited(int members = 2) =>
        WithParty(options: GameOptions.ForPreset(GameId.P3).With("p3.transport-planes", "visited"), members: members);

    [Fact]
    public void Option_IsQol_OffInEveryPreset()
    {
        var d = P3RuleOptions.TransportPlanes;
        Assert.Contains(d, RuleOptionRegistry.For(GameId.P3));
        Assert.True(d.Qol);
        Assert.Equal("D3-035", d.Difference);
        foreach (var preset in new[] { OptionPreset.Manual, OptionPreset.Dos, OptionPreset.Remaster, OptionPreset.Custom })
        {
            Assert.Equal("off", d.ValueFor(preset));
            Assert.False(GameOptions.ForPreset(GameId.P3, preset).P3TransportToVisitedPlanes());
        }
    }

    [Fact]
    public void VisitedBits_OnePerPlaneTown_ScandorIgnored()
    {
        var p3 = new Phantasie3CharacterData();
        p3.MarkPlaneTownVisited(3);
        Assert.Equal(0, p3.PlaneTownsVisited);
        p3.MarkPlaneTownVisited(16);
        Assert.True(p3.HasVisitedPlaneTown(16));
        Assert.False(p3.HasVisitedPlaneTown(15));
        Assert.False(p3.HasVisitedPlaneTown(17));
        Assert.True(p3.Clone().HasVisitedPlaneTown(16));
    }

    [OriginalDataFact]
    public void Off_ScandorOnly_EvenAfterAVisit()
    {
        var s = WithParty();
        s.EnterTown(16);
        Assert.True(s.Party.Members[0].P3.HasVisitedPlaneTown(16)); // recorded whatever the option says
        s.EnterTown(1);
        Assert.Equal(Scandor.Where(t => t != 1), s.TransportDestinations);
    }

    [OriginalDataFact]
    public void Visited_NoVisitYet_ScandorOnly()
    {
        var s = Visited();
        Assert.Equal(Scandor.Where(t => t != 1), s.TransportDestinations);
    }

    [OriginalDataFact]
    public void Visited_ListsTheEnteredPlaneTownsAfterScandor_NotTheCurrentOne()
    {
        var s = Visited();
        s.EnterTown(17);
        s.EnterTown(15);
        Assert.Equal([.. Scandor, 17], s.TransportDestinations); // cast in Light: Light itself left out
        s.EnterTown(1);
        Assert.Equal([.. Scandor.Where(t => t != 1), 15, 17], s.TransportDestinations);
    }

    [OriginalDataFact]
    public void Visited_OneMemberIsEnough()
    {
        var s = Visited();
        s.EnterTown(1);
        s.Party.Members[1].P3.MarkPlaneTownVisited(16);
        Assert.Contains(16, s.TransportDestinations);
        s.Party.Members[1].P3.PlaneTownsVisited = 0;
        Assert.DoesNotContain(16, s.TransportDestinations);
    }

    [OriginalDataFact]
    public void Visited_CastToAPlaneTown_RunsTheTownEntry()
    {
        var s = Visited(members: 4);
        s.EnterTown(16);
        s.EnterTown(1);
        var caster = s.Party.Members.First(m => m.Class == CharacterClass.Wizard);
        caster.SpellsKnown[SpellId.Transportation] = 1;
        caster.MagicCur = caster.MagicMax = 50;
        Assert.True(s.CastSpell(caster, SpellId.Transportation).Success);
        Assert.True(s.TransportationPending);
        Assert.True(s.ChooseTransportDestination(16));
        Assert.Equal(16, s.CurrentTownId);
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Equal((Phantasie3Campaign.DarkPlane, 4, 1), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
    }

    [OriginalDataFact]
    public void Visited_AnUnvisitedPlaneTownIsRefused()
    {
        var s = Visited(members: 4);
        var caster = s.Party.Members.First(m => m.Class == CharacterClass.Wizard);
        caster.SpellsKnown[SpellId.Transportation] = 1;
        caster.MagicCur = caster.MagicMax = 50;
        Assert.True(s.CastSpell(caster, SpellId.Transportation).Success);
        Assert.False(s.ChooseTransportDestination(15));
        Assert.True(s.TransportationPending);
    }

    [OriginalDataFact]
    public void JoiningInAPlaneTown_CountsAsAVisit()
    {
        var s = Visited(members: 1);
        s.EnterTown(17);
        s.OpenGuild();
        var newcomer = s.CreateAndEnroll("NEWCOMER", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(newcomer, out var err), err);
        Assert.True(newcomer.P3.HasVisitedPlaneTown(17));
    }

    [OriginalDataFact]
    public void VisitedBit_SurvivesASave_AndStaysWithItsParty()
    {
        var s = Visited(members: 1);
        s.EnterTown(15);
        s.EnterTown(1);
        var file = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));
        var loaded = GameSessionFactory.Create(Phantasie3CampaignTests.Data, new SeededDosRandomSource(7));
        loaded.ApplySnapshot(file);
        Assert.True(loaded.Party.Members[0].P3.HasVisitedPlaneTown(15));
        Assert.Contains(15, loaded.TransportDestinations);

        // D-069: a second party of characters who never went there doesn't get the town.
        loaded.OpenGuild();
        var other = loaded.CreateAndEnroll("OTHER", Race.Elf, CharacterClass.Wizard);
        var number = Array.IndexOf(loaded.Roster.Slots, other) + 1;
        Assert.True(loaded.Continue(SessionCodec.ReadJson(SessionCodec.WriteJson(loaded.CaptureSnapshot())), number, out var error), error);
        loaded.OpenGuild();
        Assert.True(loaded.TryAddToParty(loaded.Roster.Slots[number - 1]!, out var err), err);
        loaded.LeaveGuild();
        Assert.DoesNotContain(15, loaded.TransportDestinations);
    }
}
