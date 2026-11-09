using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Beyond the gate (<see cref="InventoryGameGateTests"/>): III's point values, best-gear pick, Compare / gear numbers,
/// Armory buy, hand-out and Trade follow III's own rules through the <see cref="ICampaign"/> hooks
/// (<see cref="ICampaign.ItemPoints"/>, <see cref="ICampaign.ReadyBestEquipment"/>, <see cref="ICampaign.GearStatsOf"/>,
/// <see cref="ICampaign.AfterArmoryBuy"/>); I and II keep I's id formulas and selector.
/// </summary>
public class InventoryGameGearTests
{
    // III table vs I's id formulas: 64 Mace 4 (I 12), 65 Small Axe 4 (I 12), 90 Sword +4 13 (I 17, needs 2·STR + DEX 30;
    // I 40), 41 Self Bow 1 (I 0), 44 Short Bow 4 (I 2), 60 God Bow 20 (I 10; STR + DEX 38, I 2·STR + DEX 40),
    // 40 God Armour 11 (needs STR 16; I 18).
    private const int Mace = 64, SmallAxe = 65, SwordPlus4 = 90, SelfBow = 41, ShortBow = 44, GodBow = 60, GodArmour = 40;

    private static GameSession P3(int members = 2)
    {
        var s = Phantasie3CampaignTests.WithParty(members: members);
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

    /// <summary>A member carrying exactly <paramref name="items"/>, wearing nothing.</summary>
    private static Character Carrying(GameSession s, int index, params int[] items)
    {
        var m = s.Party.Members[index];
        m.InventoryItemIds.Clear();
        m.InventoryItemIds.AddRange(items);
        m.WornShield = m.WornArmor = m.WornWeapon = m.WornBow = 0;
        m.Stats = new CharacterStats(18, 10, 18, 10, 10, 10);
        return m;
    }

    [OriginalDataFact]
    public void III_ItemPoints_AreItsTable()
    {
        var s = P3();
        Assert.Equal(4, s.ItemPoints(Mace));
        Assert.Equal(13, s.ItemPoints(SwordPlus4));
        Assert.Equal(1, s.ItemPoints(SelfBow));
        Assert.Equal(20, s.ItemPoints(GodBow));
        Assert.Equal(0, s.ItemPoints(0));
        Assert.Equal(12, ItemRules.CombatGate(Mace).Points); // I's formula, what III used to show
    }

    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    public void IAndII_ItemPoints_AreIsFormulas(GameId game)
    {
        var s = Game(game);
        for (var id = 1; id <= 100; id++)
            Assert.Equal(ItemRules.CombatGate(id).Points, s.ItemPoints(id));
    }

    [OriginalDataFact]
    public void III_Compare_ShowsTablePoints_NotIsDamageAPlusB()
    {
        // The s171 shot: a wizard with a Mace showed "DMG 12>17" beside the Sword +4's "DAMAGE 13 POINTS".
        var s = P3();
        var m = Carrying(s, 0, Mace, SwordPlus4, SelfBow);
        m.WornWeapon = Mace;
        m.WornBow = SelfBow;
        m.WeaponDamageA = 5; // I's town-exit damage A: III's combat never reads it
        var cmp = s.CompareGear(m, SwordPlus4)!;
        Assert.Equal(4, cmp.Before.WeaponDamage);
        Assert.Equal(13, cmp.After.WeaponDamage);
        Assert.Equal(1, cmp.Before.Bow);
        Assert.Equal(1, cmp.After.Bow);
        Assert.Equal(Mace, m.WornWeapon); // read-only

        var bow = s.CompareGear(m, GodBow)!;
        Assert.Equal("bow", bow.Slot.Id);
        Assert.Equal((1, 20), (bow.Before.Bow, bow.After.Bow));
        Assert.Equal(4, bow.After.WeaponDamage);
    }

    [OriginalDataFact]
    public void III_GearStats_AreTablePointsAndIIIsDefence()
    {
        var s = P3();
        var m = Carrying(s, 0, 8, GodArmour, SwordPlus4, ShortBow); // 8 Medium Shield (8)
        m.WornShield = 8;
        m.WornArmor = GodArmour;
        m.WornWeapon = SwordPlus4;
        m.WornBow = ShortBow;
        var g = s.GearStatsOf(m);
        Assert.Equal((8, 11, 13, 4), (g.ShieldSoak, g.ArmorSoak, g.WeaponDamage, g.Bow));
        Assert.Equal(P3Combat.Defence(8, 11, m.Skills.Parry, m.Stats.Dex, s.Options), g.DefenseRating);
    }

    [OriginalDataFact]
    public void I_Compare_AndGear_Unchanged()
    {
        var s = Game(GameId.P1);
        var m = s.Party.Members[0];
        CarriedItems.Put(m, SwordPlus4);
        var expected = InventoryActions.Compare(m, SwordPlus4, s.Equipment)!;
        var cmp = s.CompareGear(m, SwordPlus4)!;
        Assert.Equal(expected.Before, cmp.Before);
        Assert.Equal(expected.After, cmp.After);
        Assert.Null(cmp.After.Bow);
        Assert.Equal(InventoryActions.Stats(m), s.GearStatsOf(m));
    }

    [OriginalDataFact]
    public void III_ReadyBest_TieGoesToTheLaterItem_AndFillsTheBowSlot()
    {
        var s = P3();
        var m = Carrying(s, 0, Mace, SmallAxe, SelfBow, ShortBow);
        s.ReadyBestEquipment(m);
        Assert.Equal(SmallAxe, m.WornWeapon); // 4 = 4: the later one (I's selector keeps the first)
        Assert.Equal(ShortBow, m.WornBow);
        Assert.Equal(4, m.WeaponDamageB);     // the worn fields hold the table points

        var i = Carrying(Game(GameId.P1), 0, Mace, SmallAxe);
        CombatGear.SelectWornItems(i);
        Assert.Equal(Mace, i.WornWeapon);
    }

    [OriginalDataFact]
    public void III_ReadyBest_NeverEmptiesASlot()
    {
        var s = P3();
        var m = Carrying(s, 0, Mace);
        m.WornWeapon = SwordPlus4; // no longer carried
        m.Stats = new CharacterStats(1, 10, 1, 10, 10, 10); // Mace needs 2·STR + DEX 12: unusable
        s.ReadyBestEquipment(m);
        Assert.Equal(SwordPlus4, m.WornWeapon);
    }

    [OriginalDataFact]
    public void III_ArmoryBuy_SaysUnusableByIIIsGate_AndWearsNothing()
    {
        var s = P3();
        var m = Carrying(s, 0, Mace);
        m.WornWeapon = Mace;
        m.Stats = new CharacterStats(18, 10, 5, 10, 10, 10); // God Bow: III 23 < 38, I 41 ≥ 40
        s.Party.GoldCarried = 1_000_000;
        var town = s.CurrentTown!;
        town.SetStock(GodBow, 1);
        town.SetStock(SwordPlus4, 1);

        var bow = s.Buy(m, GodBow);
        Assert.Equal(TownTransactionKind.Ok, bow.Kind);
        Assert.True(bow.Unusable);
        Assert.Contains(GodBow, m.InventoryItemIds);
        Assert.Equal(0, m.WornBow);

        var sword = s.Buy(m, SwordPlus4); // usable (46 ≥ 30) and better, but DOS III's Armory wears nothing
        Assert.False(sword.Unusable);
        Assert.Equal(Mace, m.WornWeapon);
    }

    [OriginalDataFact]
    public void I_ArmoryBuy_StillAutoWears()
    {
        var s = Game(GameId.P1);
        var m = s.Party.Members[0];
        m.Stats = new CharacterStats(18, 10, 18, 10, 10, 10);
        s.Party.GoldCarried = 60_000;
        var town = s.CurrentTown!;
        town.SetStock(72, 1); // I: 72 = 4 points, needs 2·STR + DEX 4
        var before = m.WornWeapon;
        Assert.True(ItemRules.CombatGate(72).Points >= ItemRules.CombatGate(before).Points);
        Assert.Equal(TownTransactionKind.Ok, s.Buy(m, 72).Kind);
        Assert.Equal(72, m.WornWeapon);
    }

    [OriginalDataFact]
    public void III_HandOut_ReadiesByIIIsRule_AndItsLineUsesIIIsTable()
    {
        var s = P3();
        var a = Carrying(s, 0, SelfBow);
        a.WornBow = SelfBow;
        a.Stats = new CharacterStats(20, 10, 18, 10, 10, 10); // God Bow: III 38 ≥ 38, I 58 ≥ 40
        var b = s.Party.Members[1];
        b.Stats = new CharacterStats(18, 10, 5, 10, 10, 10);  // God Bow: III 23 < 38, I 41 ≥ 40
        s.Sack.ItemIds.Add(ShortBow);
        var d = s.BeginItemDistribution(foundOnly: true)!;
        Assert.Equal(ShortBow, d.Current);

        // The hand-out line: III's points (I's 10) and III's "who can use" column (ovl2 0x39b5).
        var line = ItemDistribution.ItemLine(GodBow, "God Bow", s.Party, s.Gear);
        Assert.StartsWith("God Bow   : 20", line);
        Assert.EndsWith(" 1", line);
        var iLine = ItemDistribution.ItemLine(GodBow, "God Bow", s.Party); // I's default
        Assert.StartsWith("God Bow   : 10", iLine);
        Assert.EndsWith(" 12", iLine);

        Assert.True(s.GiveDistributedItem(DistributeTarget.Member(0)));
        Assert.Equal(ShortBow, a.WornBow); // 4 ≥ 1: worn (I's selector has no bow slot)
    }

    [OriginalDataFact]
    public void III_TradeGive_GiverStopsWearingIt_ReceiverReadiesByIIIsRule()
    {
        var s = P3();
        Assert.True(s.CanTrade);
        var a = Carrying(s, 0, ShortBow);
        a.WornBow = ShortBow;
        var b = Carrying(s, 1, SelfBow);
        b.WornBow = SelfBow;
        Assert.Equal(P3TradeResult.Done, s.TradeGive(a, 0, b));
        Assert.Equal(0, a.WornBow);
        Assert.Equal(ShortBow, b.WornBow);
    }

    [OriginalDataFact]
    public void III_NewCharacter_WornFieldsHoldTablePoints()
    {
        var s = P3();
        foreach (var m in s.Party.Members)
        {
            Assert.Equal(s.ItemPoints(m.WornWeapon), m.WeaponDamageB);
            Assert.Equal(s.ItemPoints(m.WornArmor), m.ArmorSoak);
            Assert.Equal(s.ItemPoints(m.WornShield), m.ShieldSoak);
        }
    }

    [OriginalDataFact]
    public void III_GodBowLine_PrintsTablePointsAndStrPlusDex()
    {
        var lines = P3().DescribeItem(GodBow);
        Assert.Equal("WEAPON: DAMAGE 20 POINTS", lines[1]); // I's formula: 10
        Assert.Equal("NEEDS STR + DEX OF 38", lines[2]);     // I's: 2·STR + DEX of 40
    }
}
