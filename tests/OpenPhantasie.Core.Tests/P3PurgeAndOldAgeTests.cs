using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s99 user decisions: D3-040 (Purge on a party member, manual, as III's Drop; <c>p3.purge</c>) and D3-041 (III old age,
/// manual, I's lifespans; <c>p3.lifespans</c>).
/// </summary>
public class P3PurgeAndOldAgeTests
{
    private static GameSession With(string option, string value, int members = 2) =>
        WithParty(options: GameOptions.ForPreset(GameId.P3).With(option, value), members: members);

    [OriginalDataFact]
    public void Purge_Manual_PurgesThePartyMember_ElementalStaysWhileACharacterIsLeft()
    {
        var s = WithParty(members: 2);
        Assert.Equal(PurgePartyRule.KeepsElemental, s.Options.P3PurgeRule());
        s.OpenGuild();
        Assert.True(s.TryAddElemental(0, out var err), err);
        var first = s.Party.Members[0];
        Assert.True(s.TryPurge(first, out err), err);
        Assert.DoesNotContain(first, s.Party.Members);
        Assert.DoesNotContain(first, s.Roster.Occupied);
        Assert.NotNull(s.Party.Elemental);

        Assert.True(s.TryPurge(s.Party.Members[0], out err), err);
        Assert.Null(s.Party.Elemental); // no character left: unloaded, as III's Drop
    }

    [OriginalDataFact]
    public void Purge_PhantasieIOption_UnloadsTheElemental()
    {
        var s = With("p3.purge", "phantasie-i");
        s.OpenGuild();
        Assert.True(s.TryAddElemental(0, out var err), err);
        Assert.True(s.TryPurge(s.Party.Members[0], out err), err);
        Assert.Null(s.Party.Elemental);
    }

    [OriginalDataFact]
    public void Purge_DosOption_RefusesAPartyMember()
    {
        var s = With("p3.purge", "dos");
        s.OpenGuild();
        var member = s.Party.Members[0];
        Assert.False(s.TryPurge(member, out var err));
        Assert.Equal("You cannot purge a party member.", err);
        Assert.Contains(member, s.Party.Members);
        Assert.Contains(member, s.Roster.Occupied);
    }

    [OriginalDataTheory]
    [InlineData("manual")]
    [InlineData("phantasie-i")]
    public void Purge_NeverRemovesAnElementalFromTheGame(string rule)
    {
        var s = With("p3.purge", rule, members: 1);
        s.OpenGuild();
        Assert.True(s.TryAddElemental(0, out var err), err);
        var name = s.Party.Elemental!.Name;
        Assert.True(s.TryPurge(s.Party.Members[0], out err), err);
        Assert.Null(s.Party.Elemental);

        var c = s.CreateAndEnroll("NEWHERO", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(c, out err), err);
        Assert.True(s.TryAddElemental(0, out err), err); // the record is still in the Guild
        Assert.Equal(name, s.Party.Elemental!.Name);
    }

    [OriginalDataFact]
    public void Creation_GivesTheManualsLifespan_DosOptionNone()
    {
        var s = WithParty(members: 3);
        foreach (var m in s.Party.Members)
        {
            var (lo, hi) = s.CreationTables.LifespanBandsByRace[(int)m.Race];
            Assert.InRange(m.Lifespan, 26 * lo, 26 * hi);
            Assert.InRange(m.AgeClock, 260 + m.Lifespan / 10, 260 + m.Lifespan / 8); // I's start age (D-003)
        }
        Assert.InRange(s.Party.Members[0].Lifespan, 26 * 72, 26 * 88); // Human (manual "about 80")

        var d = With("p3.lifespans", "dos", members: 4);
        Assert.All(d.Party.Members, m => Assert.Equal(0, m.Lifespan));
        Assert.True(d.Party.Members[3].AgeClock > 26 * 29); // DOS's Halfling (factor 2.5) starts at 29 years or more
    }

    [OriginalDataFact]
    public void TownEntry_PastTheLifespan_Dies_PastSeventyPercent_Declines()
    {
        var s = WithParty(members: 2);
        var old = s.Party.Members[0];
        var aging = s.Party.Members[1];
        old.AgeClock = old.Lifespan + 1;
        aging.AgeClock = aging.Lifespan * 7 / 10 + 1;
        var str = aging.Stats.Str;
        Assert.True(s.LeaveTown());
        s.EnterTown(s.GuildTownId);
        Assert.Equal(CharacterStatus.Dead, old.Status);
        Assert.True(aging.HasAged);
        Assert.Equal(str * 4 / 5, aging.Stats.Str);
    }
}
