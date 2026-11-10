using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// QoL <c>modern-inventory</c> (user s178, <c>inventory-actions.md</c> section 7): gear stays where the player puts it. No
/// D)ISTRIBUTE pooling, no III town-entry hand-out or ready-best, a new item worn only into an empty slot, the moves anywhere
/// outside combat and Sell in town. Off (every Original preset) changes nothing.
/// </summary>
public class ModernInventoryTests
{
    // I: shields 1–20, armour 21–40, weapons 41–100 (rules.json equipment_slots).
    private const int Shield = 2, Weapon = 72, OtherWeapon = 45;

    private static GameSession Game(GameId game, bool modern = true)
    {
        var s = GameSessionFactory.Create(GameDataRepository.Load(GameDataRepository.DefaultPathFor(game)), new SeededDosRandomSource(7));
        var options = GameOptions.ForPreset(game);
        s.StartNewGame(modern ? options.With(RuleOptionRegistry.ModernInventory.Id, "on") : options);
        s.OpenGuild();
        foreach (var (name, cls) in new[] { ("ONE", CharacterClass.Fighter), ("TWO", CharacterClass.Fighter) })
        {
            var c = s.CreateAndEnroll(name, Race.Human, cls);
            Assert.True(s.TryAddToParty(c, out var err), err);
        }
        s.LeaveGuild();
        Assert.Equal(GameMode.Town, s.Mode);
        return s;
    }

    private static GameSession P3(bool modern = true)
    {
        var options = GameOptions.ForPreset(GameId.P3);
        return Phantasie3CampaignTests.WithParty(
            options: modern ? options.With(RuleOptionRegistry.ModernInventory.Id, "on") : options, members: 2);
    }

    /// <summary>A member carrying exactly <paramref name="items"/>, wearing nothing, strong enough for all of them.</summary>
    private static Character Carrying(GameSession s, int index, params int[] items)
    {
        var m = s.Party.Members[index];
        m.InventoryItemIds.Clear();
        m.InventoryItemIds.AddRange(items);
        m.WornShield = m.WornArmor = m.WornWeapon = m.WornBow = 0;
        m.Stats = new CharacterStats(18, 10, 18, 10, 10, 10);
        return m;
    }

    [Fact]
    public void Option_IsQol_OnInRemaster_OffInBothOriginals_NotInIV()
    {
        var o = RuleOptionRegistry.ModernInventory;
        Assert.True(o.Qol);
        Assert.Equal(("off", "off", "on"), (o.Manual, o.Dos, o.Remaster));
        foreach (var g in new[] { GameId.P1, GameId.P2, GameId.P3 })
        {
            Assert.Single(RuleOptionRegistry.For(g), d => d.Id == o.Id);
            Assert.True(GameOptions.ForPreset(g, OptionPreset.Remaster).ModernInventory);
            Assert.False(GameOptions.ForPreset(g, OptionPreset.Dos).ModernInventory);
            Assert.False(GameOptions.ForPreset(g, OptionPreset.Manual).ModernInventory);
        }
        Assert.DoesNotContain(RuleOptionRegistry.For(GameId.P4), d => d.Id == o.Id);
    }

    [Fact]
    public void Option_OverridesTheStashRule_Anywhere()
    {
        var town = GameOptions.ForPreset(GameId.P1, OptionPreset.Manual);
        Assert.Equal(StashAccessRule.Town, town.StashAccess);
        Assert.Equal(StashAccessRule.Anywhere, town.With(RuleOptionRegistry.ModernInventory.Id, "on").StashAccess);
    }

    [OriginalDataFact]
    public void Moves_WorkOutsideTown()
    {
        var s = Game(GameId.P1);
        Assert.True(s.LeaveTown());
        Assert.NotEqual(GameMode.Town, s.Mode);
        Assert.Equal(InventoryRefusal.None, s.InventoryAccess);
    }

    [OriginalDataFact]
    public void ArmoryBuy_WearsOnlyIntoAnEmptySlot()
    {
        var s = Game(GameId.P1);
        var town = s.CurrentTown!;
        s.Party.GoldCarried = 60_000;
        town.SetStock(Weapon, 2);
        var a = Carrying(s, 0, OtherWeapon);
        a.WornWeapon = OtherWeapon;
        Assert.True(ItemRules.CombatGate(Weapon).Points > ItemRules.CombatGate(OtherWeapon).Points);
        Assert.Equal(TownTransactionKind.Ok, s.Buy(a, Weapon).Kind);
        Assert.Equal(OtherWeapon, a.WornWeapon); // the player's pick stays, even against a better item

        var b = Carrying(s, 1);
        Assert.Equal(TownTransactionKind.Ok, s.Buy(b, Weapon).Kind);
        Assert.Equal(Weapon, b.WornWeapon); // nothing worn there: worn
    }

    [OriginalDataFact]
    public void ArmoryBuy_OriginalStillReadiesTheBetterItem()
    {
        var s = Game(GameId.P1, modern: false);
        s.Party.GoldCarried = 60_000;
        s.CurrentTown!.SetStock(Weapon, 1);
        var a = Carrying(s, 0, OtherWeapon);
        a.WornWeapon = OtherWeapon;
        Assert.Equal(TownTransactionKind.Ok, s.Buy(a, Weapon).Kind);
        Assert.Equal(Weapon, a.WornWeapon);
    }

    [OriginalDataFact]
    public void TakenOff_StaysOff_WhenAnotherKindArrives()
    {
        var s = Game(GameId.P1);
        var a = Carrying(s, 0, Shield);
        Assert.Equal(InventoryRefusal.None, s.TryEquip(a, 0));
        Assert.Equal(InventoryRefusal.None, s.TryUnequip(a, "shield"));
        s.Sack.ItemIds.Add(Weapon);
        Assert.Equal(InventoryRefusal.None, s.TryTakeFromStash(0, a));
        Assert.Equal(0, a.WornShield); // the emptied slot stays empty
        Assert.Equal(Weapon, a.WornWeapon); // the arriving weapon fills its own empty slot
    }

    [OriginalDataFact]
    public void Give_WearsOnlyIntoTheReceiversEmptySlot()
    {
        var s = Game(GameId.P1);
        var a = Carrying(s, 0, Weapon, Shield);
        var b = Carrying(s, 1, OtherWeapon);
        b.WornWeapon = OtherWeapon;
        Assert.Equal(InventoryRefusal.None, s.TryTransferItem(a, 0, b));
        Assert.Equal(OtherWeapon, b.WornWeapon); // not swapped
        Assert.Equal(InventoryRefusal.None, s.TryTransferItem(a, 1, b));
        Assert.Equal(Shield, b.WornShield); // empty slot: worn
    }

    [OriginalDataFact]
    public void Give_Original_DoesNotWear()
    {
        var s = Game(GameId.P1, modern: false);
        var a = Carrying(s, 0, Shield);
        var b = Carrying(s, 1);
        Assert.Equal(InventoryRefusal.None, s.TryTransferItem(a, 0, b));
        Assert.Equal(0, b.WornShield); // s75: receiving doesn't wear
    }

    [OriginalDataFact]
    public void Distribute_NeverPoolsTheItems()
    {
        var s = Game(GameId.P1);
        var a = Carrying(s, 0, Weapon);
        a.WornWeapon = Weapon;
        s.Sack.ItemIds.Add(Shield);
        Assert.False(s.ItemHandOut);
        Assert.Null(s.BeginItemDistribution());
        Assert.Equal(Weapon, a.WornWeapon);
        Assert.Equal([Weapon], a.InventoryItemIds);
        Assert.Equal([Shield], s.Sack.ItemIds);
        Assert.NotNull(Game(GameId.P1, modern: false).BeginItemDistribution()); // original: the pool
    }

    [OriginalDataFact]
    public void II_NoPooling_ArmoryWearsOnlyIntoAnEmptySlot()
    {
        var s = Game(GameId.P2);
        var a = Carrying(s, 0, OtherWeapon);
        a.WornWeapon = OtherWeapon;
        s.Sack.ItemIds.Add(Shield);
        Assert.Null(s.BeginItemDistribution());
        Assert.Equal(OtherWeapon, a.WornWeapon);
        s.Party.GoldCarried = 60_000;
        s.CurrentTown!.SetStock(Weapon, 1);
        Assert.Equal(TownTransactionKind.Ok, s.Buy(a, Weapon).Kind);
        Assert.Equal(OtherWeapon, a.WornWeapon);
        Assert.Equal(InventoryRefusal.None, s.TrySellStashItem(0)); // II sells in town too
    }

    [OriginalDataFact]
    public void Sell_Carried_PaysTheDistributePrice_RaisesStock_StopsWearing()
    {
        var s = Game(GameId.P1);
        var town = s.CurrentTown!;
        var a = Carrying(s, 0, Weapon);
        a.WornWeapon = Weapon;
        s.Party.GoldCarried = 0;
        var stock = town.GetStock(Weapon);
        var price = s.SellPriceOf(Weapon);
        Assert.True(price > 0);
        Assert.Equal(InventoryRefusal.None, s.TrySellItem(a, 0));
        Assert.Equal(price, s.Party.GoldCarried);
        Assert.Equal(Math.Min(ItemDistribution.MaxStock, stock + 1), town.GetStock(Weapon));
        Assert.Equal(0, a.WornWeapon);
        Assert.Equal(0, CarriedItems.Count(a));
    }

    [OriginalDataFact]
    public void Sell_Stash_KeepsTheHoles()
    {
        var s = Game(GameId.P1);
        s.Sack.ItemIds.AddRange([Shield, Weapon]);
        s.Party.GoldCarried = 0;
        Assert.Equal(InventoryRefusal.None, s.TrySellStashItem(0));
        Assert.Equal([0, Weapon], s.Sack.ItemIds);
        Assert.Equal(s.SellPriceOf(Shield), s.Party.GoldCarried);
        Assert.Equal(InventoryRefusal.NoItem, s.CanSellStashItem(0));
    }

    [OriginalDataFact]
    public void Sell_OnlyInTown_AndOnlyWithTheOption()
    {
        var off = Game(GameId.P1, modern: false);
        off.Sack.ItemIds.Add(Shield);
        Assert.Equal(InventoryRefusal.NoShop, off.CanSellStashItem(0));

        var s = Game(GameId.P1);
        s.Sack.ItemIds.Add(Shield);
        Assert.True(s.LeaveTown());
        Assert.Equal(InventoryRefusal.NoShop, s.CanSellStashItem(0));
        Assert.Equal(0, s.SellPriceOf(Shield));
    }

    [OriginalDataFact]
    public void III_Sell_PaysSeventyFivePercentOfTheTownPrice()
    {
        var s = P3();
        var a = Carrying(s, 0, 64);
        s.Party.GoldCarried = 0;
        var expected = s.TradeSalePrice(64); // P3Town.SalePrice: 75 % of this town's price
        Assert.Equal(expected, s.SellPriceOf(64));
        Assert.Equal(InventoryRefusal.None, s.TrySellItem(a, 0));
        Assert.Equal(expected, s.Party.GoldCarried);
    }

    [OriginalDataFact]
    public void III_TownEntry_NoHandOut_NoReadyBest_WandAndSharesStay()
    {
        var s = P3();
        var town = s.CurrentTownId!.Value;
        var m = s.Party.Members[0];
        m.Stats = m.Stats with { Str = 18 };
        Assert.True(s.LeaveTown());
        m.InventoryItemIds.Add(16); // Giant Shield, better than the worn one
        var worn = m.WornShield;
        s.Sack.Experience = 100;
        s.Sack.ItemIds.AddRange([20, 180]);

        s.EnterTown(town);

        Assert.Equal(worn, m.WornShield); // no ready-best
        Assert.False(s.TownItemHandOutPending);
        Assert.False(s.TakeTownItemHandOut());
        Assert.Equal([20], s.Sack.ItemIds); // the found item waits in the stash; the last wand still goes (0x5a9)
        Assert.True(s.TownXpSharesDue); // the XP shares are still asked
    }

    [OriginalDataFact]
    public void III_TradeGive_NoReReady_ReceiverWearsOnlyIntoAnEmptySlot()
    {
        var s = P3();
        var a = Carrying(s, 0, 44, 41); // Short Bow worn, Self Bow carried
        a.WornBow = 44;
        var b = Carrying(s, 1);
        Assert.Equal(P3TradeResult.Done, s.TradeGive(a, 0, b));
        Assert.Equal(0, a.WornBow); // the giver's slot stays empty (original: the Self Bow is readied)
        Assert.Equal(44, b.WornBow);
    }
}
