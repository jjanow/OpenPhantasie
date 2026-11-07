using OpenPhantasie.Core;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// III's town screens on the session: the bank by <c>p3.bank</c> (D3-025), Trade Items by <c>p3.trade</c> (D3-027) and
/// training's three skill picks (ovl2 <c>0x16d0</c>).
/// </summary>
public class P3TownScreensTests
{
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P3);

    private static GameSession Town(GameOptions? options = null)
    {
        var s = Phantasie3CampaignTests.WithParty(options: options);
        Assert.Equal(GameMode.Town, s.Mode);
        foreach (var m in s.Party.Members)
            m.GoldInBank = 0;
        return s;
    }

    [OriginalDataFact]
    public void Bank_ManualDeposit_PaysAppleIisFee_NoFeeIsIsMaths()
    {
        var s = Town();
        Assert.False(s.BankIsDos);
        s.Party.GoldCarried = 600;
        s.BankDeposit([1, 1, 1, 1]);
        Assert.All(s.Party.Members, m => Assert.Equal(149, m.GoldInBank)); // ⌊600 / 4.00001⌋

        var t = Town(Manual.With("p3.bank", "no-fee"));
        t.Party.GoldCarried = 600;
        t.BankDeposit([1, 1, 1, 1]);
        Assert.All(t.Party.Members, m => Assert.Equal(150, m.GoldInBank));
    }

    [OriginalDataFact]
    public void Bank_Dos_DepositSome_WithdrawAll()
    {
        var s = Town(Manual.With("p3.bank", "dos"));
        Assert.True(s.BankIsDos);
        s.Party.GoldCarried = 1000;
        Assert.False(s.DosBankDeposit(1001));
        Assert.True(s.DosBankDeposit(402));
        Assert.Equal(598, s.Party.GoldCarried);
        Assert.Equal(402, s.Party.Members.Sum(m => m.GoldInBank));
        Assert.All(s.Party.Members, m => Assert.InRange(m.GoldInBank, 100, 101));
        Assert.Equal(TownTransactionKind.Failed, s.BankWithdraw(s.Party.Members[0], 1).Kind); // no per-member withdraw
        Assert.True(s.DosBankWithdrawAll());
        Assert.Equal(1000, s.Party.GoldCarried);
    }

    [OriginalDataFact]
    public void Trade_GiveReReadiesBoth_SellPays75Percent_OptionsGateIt()
    {
        var s = Town();
        Assert.True(s.CanTrade);
        Assert.True(s.CanTradeSell);
        var a = s.Party.Members[0];
        var b = s.Party.Members[1];
        var weapon = a.WornWeapon;
        var slot = a.InventoryItemIds.IndexOf(weapon);
        b.InventoryItemIds.Remove(b.WornWeapon); // room, and nothing better for B to wield
        Assert.Equal(P3TradeResult.Done, s.TradeGive(a, slot, b));
        Assert.DoesNotContain(weapon, a.InventoryItemIds);
        Assert.NotEqual(weapon, a.WornWeapon); // re-readied
        Assert.Contains(weapon, b.InventoryItemIds);

        var gold = s.Party.GoldCarried;
        var shield = a.InventoryItemIds[0];
        var price = s.TradeSalePrice(shield);
        Assert.True(price >= 1);
        Assert.Equal(P3TradeResult.Done, s.TradeSell(a, 0));
        Assert.Equal(gold + price, s.Party.GoldCarried);

        var giveOnly = Town(Manual.With("p3.trade", "give-only"));
        Assert.True(giveOnly.CanTrade);
        Assert.False(giveOnly.CanTradeSell);
        Assert.Equal(P3TradeResult.NoTrade, giveOnly.TradeSell(giveOnly.Party.Members[0], 0));
        Assert.False(Town(Manual.With("p3.trade", "dos")).CanTrade);
    }

    [OriginalDataFact]
    public void Training_ThreePicksInOrder_ARepeatedPickMovesHalfAsFar_EscUsesAPickUp()
    {
        var s = Town();
        Assert.True(s.TrainingAsksSkills);
        var fighter = s.Party.Members[0];
        fighter.Experience = 1_000_000;
        s.Party.GoldCarried = 60_000;
        Assert.True(s.TrainingWouldSucceed(fighter));

        var first = s.TrainingSkillRows(fighter, []).Single(r => r.Skill == P3Skill.Parry);
        var second = s.TrainingSkillRows(fighter, [P3Skill.Parry]).Single(r => r.Skill == P3Skill.Parry);
        Assert.Equal(first.After, second.Now);
        Assert.Equal((first.After - first.Now) / 2, second.After - second.Now, tolerance: 1);
        Assert.Equal(fighter.Skills.Parry, first.Now); // the preview changed nothing

        var attack = fighter.Skills.Attack;
        Assert.Equal(TownTransactionKind.Ok, s.Train(fighter, [P3Skill.Parry, P3Skill.Parry, null]).Kind);
        Assert.Equal(second.After, fighter.Skills.Parry);
        Assert.Equal(attack, fighter.Skills.Attack); // not picked
    }
}
