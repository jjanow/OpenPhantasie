using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// The inventory moves ask each game's own strength gate (<see cref="ICampaign.CanUseItem"/>, <see cref="GameSession.CanWear"/>),
/// not I's id formulas for every game: III's tables (<c>min_str</c> / <c>str_check</c>, ovl2 <c>0x39b5</c>; bows 41–60 on
/// STR + DEX, melee 2·STR + DEX), I and II the formulas of <see cref="ItemRules.IsUsable"/>.
/// </summary>
public class InventoryGameGateTests
{
    // III: 32 Robes +1 (needs STR 0; I's formula 2), 42 Self Bow +1 (STR + DEX 2; I's 2·STR + DEX 4),
    // 60 God Bow (STR + DEX 38; I's 2·STR + DEX 40).
    private const int RobesPlus1 = 32, SelfBowPlus1 = 42, GodBow = 60;

    private static GameSession P3()
    {
        var s = Phantasie3CampaignTests.WithParty(members: 2);
        Assert.Equal(GameMode.Town, s.Mode);
        return s;
    }

    private static GameSession Game(GameId game)
    {
        var s = GameSessionFactory.Create(GameDataRepository.Load(GameDataRepository.DefaultPathFor(game)), new SeededDosRandomSource(7));
        s.StartNewGame();
        s.OpenGuild();
        foreach (var (name, cls) in new[] { ("ONE", CharacterClass.Fighter), ("TWO", CharacterClass.Wizard) })
        {
            var c = s.CreateAndEnroll(name, Race.Human, cls);
            Assert.True(s.TryAddToParty(c, out var err), err);
        }
        s.LeaveGuild();
        return s;
    }

    [OriginalDataFact]
    public void III_BowIsGatedOnStrPlusDex_NotIsTwiceStrPlusDex()
    {
        var s = P3();
        var m = s.Party.Members[0];
        m.Stats = new CharacterStats(1, 10, 1, 10, 10, 10);
        Assert.False(ItemRules.IsUsable(SelfBowPlus1, m.Stats)); // I's rule: 2·1 + 1 = 3 < 4
        Assert.True(s.CanUseItem(m, SelfBowPlus1));             // III's: 1 + 1 = 2 ≥ 2
        Assert.Equal(InventoryRefusal.None, s.CanWear(m, SelfBowPlus1));

        m.Stats = new CharacterStats(18, 10, 5, 10, 10, 10);
        Assert.True(ItemRules.IsUsable(GodBow, m.Stats));        // I's rule: 41 ≥ 40
        Assert.False(s.CanUseItem(m, GodBow));                   // III's: 23 < 38
        Assert.Equal(InventoryRefusal.Unusable, s.CanWear(m, GodBow));
    }

    [OriginalDataFact]
    public void III_ArmourUsesItsOwnTable()
    {
        var s = P3();
        var m = s.Party.Members[0];
        m.Stats = new CharacterStats(1, 10, 10, 10, 10, 10);
        Assert.False(ItemRules.IsUsable(RobesPlus1, m.Stats)); // I: 2·32 − 62 = 2
        Assert.True(s.CanUseItem(m, RobesPlus1));              // III table: 0
    }

    [OriginalDataFact]
    public void III_EquipFromCarriedAndStash_FollowTheGame()
    {
        var s = P3();
        var m = s.Party.Members[0];
        m.Stats = new CharacterStats(18, 10, 5, 10, 10, 10);
        CarriedItems.Put(m, GodBow);
        Assert.Equal(InventoryRefusal.Unusable, s.TryEquip(m, m.InventoryItemIds.IndexOf(GodBow)));

        m.Stats = new CharacterStats(1, 10, 1, 10, 10, 10);
        s.Sack.ItemIds.Add(SelfBowPlus1);
        var slot = s.Sack.ItemIds.IndexOf(SelfBowPlus1);
        if (CarriedItems.IsFull(m))
            CarriedItems.ClearSlot(m, m.InventoryItemIds.IndexOf(GodBow));
        Assert.Equal(InventoryRefusal.None, s.TryEquipFromStash(slot, m));
        Assert.Equal(SelfBowPlus1, WornItems.Get(m, "bow"));
    }

    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    public void IAndII_KeepIsIdFormulas(GameId game)
    {
        var s = Game(game);
        var m = s.Party.Members[0];
        foreach (var stats in new[] { new CharacterStats(1, 10, 1, 10, 10, 10), new CharacterStats(18, 10, 5, 10, 10, 10),
                     new CharacterStats(9, 10, 9, 10, 10, 10) })
        {
            m.Stats = stats;
            for (var id = 1; id <= 180; id++)
                Assert.Equal(ItemRules.IsUsable(id, stats), s.CanUseItem(m, id));
        }
    }

    [OriginalDataFact]
    public void Describe_III_PrintsItsOwnRequirement_IUnchanged()
    {
        var p3 = P3();
        var bow = p3.DescribeItem(SelfBowPlus1);
        Assert.Equal("WEAPON: DAMAGE 2 POINTS", bow[1]);
        Assert.Equal("NEEDS STR + DEX OF 2", bow[2]);
        Assert.Equal("ANYONE CAN WEAR IT", p3.DescribeItem(RobesPlus1)[2]);

        var p1 = Game(GameId.P1);
        Assert.Equal("NEEDS STR x2 + DEX OF 4", p1.DescribeItem(SelfBowPlus1)[2]); // I: id 42 is a weapon, 2·42 − 80
        Assert.Equal("NEEDS STRENGTH 2", p1.DescribeItem(RobesPlus1)[2]);
    }
}
