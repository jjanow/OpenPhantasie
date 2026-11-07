using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// III's Guild Drop gold share (DOS ovl2 <c>0x3c92</c>, Amiga <c>0xd448</c>, <c>town.md</c> §2): the dropped character's
/// account gets <c>party gold / characters</c>, counted before the Drop, the elemental not counted.
/// </summary>
public class P3DropGoldShareTests
{
    [OriginalDataFact]
    public void RulesProfile_IIIHasTheShare_IDoesNot()
    {
        Assert.True(Phantasie3CampaignTests.Data.Rules.DropGoldShare);
        Assert.False(RulesProfile.PhantasieI.DropGoldShare);
    }

    [OriginalDataFact]
    public void Drop_PaysPartyGoldOverCharacters_RemainderStaysWithTheParty()
    {
        var s = WithParty(members: 4);
        s.Party.GoldCarried = 1003;
        var hero = s.Party.Members[1];
        var bank = hero.GoldInBank;
        Assert.True(GuildHall.TryDropFromParty(s.Party, hero, out var err), err);
        Assert.Equal(bank + 250, hero.GoldInBank); // 1003 / 4, the count before the Drop
        Assert.Equal(753, s.Party.GoldCarried);
        Assert.DoesNotContain(hero, s.Party.Members);
    }

    [OriginalDataFact]
    public void Drop_LastCharacter_TakesAllTheGold()
    {
        var s = WithParty(members: 1);
        s.Party.GoldCarried = 777;
        var hero = s.Party.Members[0];
        var bank = hero.GoldInBank;
        Assert.True(GuildHall.TryDropFromParty(s.Party, hero, out var err), err);
        Assert.Equal(bank + 777, hero.GoldInBank);
        Assert.Equal(0, s.Party.GoldCarried);
    }

    [OriginalDataFact]
    public void Drop_DeadCharacter_StillGetsHisShare()
    {
        var s = WithParty(members: 2);
        s.Party.GoldCarried = 100;
        var hero = s.Party.Members[0];
        hero.Status = CharacterStatus.Dead; // ovl2 0x3c92 tests no status
        var bank = hero.GoldInBank;
        Assert.True(GuildHall.TryDropFromParty(s.Party, hero, out _));
        Assert.Equal(bank + 50, hero.GoldInBank);
        Assert.Equal(50, s.Party.GoldCarried);
    }

    [OriginalDataFact]
    public void Drop_LoadedElemental_IsNotCountedAndDroppingItMovesNoGold()
    {
        var s = WithParty(members: 2);
        s.OpenGuild();
        Assert.True(s.TryAddElemental(0, out var err), err);
        s.Party.GoldCarried = 90;
        var hero = s.Party.Members[0];
        var bank = hero.GoldInBank;
        Assert.True(GuildHall.TryDropFromParty(s.Party, hero, out err), err);
        Assert.Equal(bank + 45, hero.GoldInBank); // FAR:0xb614 = characters only
        Assert.Equal(45, s.Party.GoldCarried);
        Assert.NotNull(s.Party.Elemental);
    }

    [OriginalDataFact]
    public void Drop_NoGold_MovesNothing()
    {
        var s = WithParty(members: 3);
        s.Party.GoldCarried = 2;
        var hero = s.Party.Members[2];
        var bank = hero.GoldInBank;
        Assert.True(GuildHall.TryDropFromParty(s.Party, hero, out _));
        Assert.Equal(bank, hero.GoldInBank); // 2 / 3 = 0
        Assert.Equal(2, s.Party.GoldCarried);
    }

    [OriginalDataFact]
    public void Drop_UnderTheManualAccountCap_OnlyWhatFitsLeavesThePurse()
    {
        var s = WithParty(members: 1);
        var hero = s.Party.Members[0];
        hero.GoldInBank = 64_900;
        s.Party.GoldCarried = 500;
        Assert.True(GuildHall.TryDropFromParty(s.Party, hero, out _));
        Assert.Equal(65_000, hero.GoldInBank); // D3-026 manual cap
        Assert.Equal(400, s.Party.GoldCarried); // nothing lost
    }

    [Fact]
    public void Drop_PhantasieI_LeavesThePurseAlone()
    {
        var party = new Party();
        var a = new Character { Name = "A", GoldInBank = 10 };
        party.Members.Add(a);
        party.Members.Add(new Character { Name = "B" });
        party.GoldCarried = 600;
        Assert.True(GuildHall.TryDropFromParty(party, a, out _));
        Assert.Equal(10, a.GoldInBank);
        Assert.Equal(600, party.GoldCarried);
    }

    [OriginalDataFact]
    public void Drop_NotInParty_IsRefusedAndMovesNoGold()
    {
        var s = WithParty(members: 2);
        s.Party.GoldCarried = 100;
        var stranger = new Character { Name = "X" };
        Assert.False(GuildHall.TryDropFromParty(s.Party, stranger, out _));
        Assert.Equal(0, stranger.GoldInBank);
        Assert.Equal(100, s.Party.GoldCarried);
    }

    [OriginalDataFact]
    public void Drop_CountsTheFoundGoldAsPartyGold_TakesItFirst()
    {
        // DOS has one treasury (FAR:0x9de); the remaster keeps III's found gold in the sack (Phantasie3Campaign.PartyGold).
        var s = WithParty(members: 2);
        s.Party.GoldCarried = 100;
        s.Sack.Gold = 60;
        var hero = s.Party.Members[0];
        var bank = hero.GoldInBank;
        Assert.True(GuildHall.TryDropFromParty(s.Party, hero, out var err, s.Sack), err);
        Assert.Equal(bank + 80, hero.GoldInBank); // (100 + 60) / 2
        Assert.Equal(0, s.Sack.Gold);
        Assert.Equal(80, s.Party.GoldCarried);
    }
}
