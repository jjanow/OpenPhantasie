using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>The DOS item hand-out screen (OV1 0x2ab3 / 0x2bed / 0x2e03, town-menus.md §Distribute).</summary>
public class ItemDistributionTests
{
    private static GameSession TownSession(out Character fighter, out Character wizard)
    {
        var session = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(5));
        session.OpenGuild();
        fighter = session.CreateAndEnroll("Fighto", Race.Human, CharacterClass.Fighter);
        wizard = session.CreateAndEnroll("Wizo", Race.Human, CharacterClass.Wizard);
        Assert.True(session.TryAddToParty(fighter, out _));
        Assert.True(session.TryAddToParty(wizard, out _));
        session.EnterTown(1);
        return session;
    }

    [OriginalDataFact]
    public void Begin_PoolsCarriedWornAndFoundItems_HighestIdFirst_NothingWorn()
    {
        var s = TownSession(out var f, out var w);
        var carried = f.InventoryItemIds.Concat(w.InventoryItemIds).ToList();
        s.Sack.ItemIds.AddRange([103, 59]);
        var d = s.BeginItemDistribution()!;

        Assert.Equal(carried.Concat([103, 59]).OrderByDescending(id => id), d.Remaining);
        Assert.Empty(f.InventoryItemIds);
        Assert.Empty(s.Sack.ItemIds);
        Assert.Equal((0, 0, 0), (f.WornShield, f.WornArmor, f.WornWeapon));
    }

    [OriginalDataFact]
    public void Distribute_CompactsADrunkPotionsGap()
    {
        // [C] s58: Begin zeroes every slot, give writes slot[count] (OV1 0x2f22), so the refill is contiguous.
        var s = TownSession(out var f, out var w);
        w.InventoryItemIds.Clear();
        f.InventoryItemIds.Clear();
        f.InventoryItemIds.AddRange([121, 0, 122]);
        var d = s.BeginItemDistribution()!;
        while (!d.IsDone)
            Assert.True(s.GiveDistributedItem(DistributeTarget.Member(0)));
        Assert.Equal([122, 121], f.InventoryItemIds);
    }

    [OriginalDataFact]
    public void Give_Member_ReEquipsTheBest_Sack_Sell_AndRefusesAFullMember()
    {
        var s = TownSession(out var f, out _);
        f.Stats = f.Stats with { Str = 18, Dex = 18 };
        var d = s.BeginItemDistribution()!;
        while (!d.IsDone)
            Assert.True(s.GiveDistributedItem(DistributeTarget.Member(0)) || s.GiveDistributedItem(DistributeTarget.Sack));
        Assert.Null(s.ItemDistribution); // the screen ends with the pool
        Assert.NotEqual(0, f.WornWeapon); // OV1 0x2fdb ran on every hand-out
        Assert.Equal(f.InventoryItemIds.Where(ItemRules.IsWeapon).MaxBy(id => ItemRules.CombatGate(id).Points), f.WornWeapon);

        // SELL: V²/8 gold and the Armory gets the item back.
        f.InventoryItemIds.Clear();
        f.InventoryItemIds.Add(59); // SWORD
        var stock = s.CurrentTown!.GetStock(59);
        var gold = s.Party.GoldCarried;
        d = s.BeginItemDistribution()!;
        Assert.Equal(59, d.Current);
        Assert.True(s.GiveDistributedItem(DistributeTarget.Sell));
        Assert.Equal(gold + ItemRules.SellPrice(GameDataRepository.LoadPhantasie1().Items.First(i => i.Id == 59).ValueFactor), s.Party.GoldCarried);
        Assert.Equal(stock + 1, s.CurrentTown.GetStock(59));

        // A member with nine items refuses; the item is asked again.
        while (f.InventoryItemIds.Count < Character.InventorySlots) f.InventoryItemIds.Add(121);
        s.Sack.ItemIds.Add(46);
        d = s.BeginItemDistribution()!;
        f.InventoryItemIds.AddRange(Enumerable.Repeat(121, Character.InventorySlots));
        Assert.False(s.GiveDistributedItem(DistributeTarget.Member(0)));
        Assert.Equal(10, d.Remaining.Count);
    }

    [OriginalDataFact]
    public void ItemLine_ShowsPointsKindAndWhoCanUseIt()
    {
        var s = TownSession(out var f, out var w);
        f.Stats = f.Stats with { Str = 18, Dex = 18 };
        w.Stats = w.Stats with { Str = 3, Dex = 3 };
        Assert.Equal("SWORD     :  9 W 1", ItemDistribution.ItemLine(59, "SWORD", s.Party));
        Assert.Equal("HEALING 3 :  0", ItemDistribution.ItemLine(103, "HEALING 3", s.Party));
    }
}
