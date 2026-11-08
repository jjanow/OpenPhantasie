using OpenPhantasie.Data;
using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s97 T4 leftovers: the fight-find question (root <c>0xf2eb</c>), DOS's dungeon entry question (ovl4 <c>0x6fe</c>, D3-038
/// <c>dos</c>), and Guild Drop with an elemental loaded (ovl2 <c>0x3c92</c>).
/// </summary>
public partial class P3LeftoverTests
{
    private static GameSession Reload(GameSession s, Action<SessionSnapshot> edit)
    {
        var snap = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));
        edit(snap);
        var t = GameSessionFactory.Create(Phantasie3CampaignTests.Data, new SeededDosRandomSource(3));
        t.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(snap)));
        return t;
    }

    [OriginalDataFact]
    public void Drop_KeepsTheLoadedElementalWhileACharacterIsLeft()
    {
        Assert.True(Phantasie3CampaignTests.Data.Rules.ElementalRulesOrDefault.DropKeeps);
        var s = WithParty(members: 2);
        s.OpenGuild();
        Assert.True(s.TryAddElemental(0, out var err), err);
        var first = s.Party.Members[0];
        Assert.True(GuildHall.TryDropFromParty(s.Party, first, out err), err);
        Assert.NotNull(s.Party.Elemental); // ovl2 0x3c92: the elemental moves up and stays last
        Assert.True(GuildHall.TryDropFromParty(s.Party, s.Party.Members[0], out err), err);
        Assert.Null(s.Party.Elemental); // 0x3e0b: no character left, everything cleared
    }

    [OriginalDataFact]
    public void TrainingPickScreen_ListsTheSkillsInDosOrder()
    {
        var s = WithParty(members: 1);
        var rows = s.TrainingSkillRows(s.Party.Members[0], []);
        Assert.Equal(
            [P3Skill.Attack, P3Skill.Parry, P3Skill.Swim, P3Skill.Listen, P3Skill.FindTrap, P3Skill.Disarm, P3Skill.FindItem,
                P3Skill.PickLock, P3Skill.FireBow], // ovl2 0x18b0: name table DS:0x1438
            rows.Select(r => r.Skill));
    }

    [Fact]
    public void Drop_PhantasieI_StillClearsTheElemental()
    {
        Assert.False(ElementalRules.PhantasieI.DropKeeps);
        var party = new Party();
        var a = new Character { Name = "A" };
        party.Members.Add(a);
        party.Members.Add(new Character { Name = "B" });
        party.Elemental = new Character { Name = "AIR" };
        Assert.True(GuildHall.TryDropFromParty(party, a, out _));
        Assert.Null(party.Elemental);
    }
}
