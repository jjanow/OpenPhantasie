using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>The Guild treasury (P3 QoL, user s175, <c>docs/plan/11-guild-treasury.md</c>), I–III.</summary>
public class TreasuryTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    private static GameOptions Rules(string value) => GameOptions.ForPreset(GameId.P1).With("treasury", value);

    private static SessionSnapshot SaveAndRead(GameSession s) => SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));

    /// <summary>A new I game with two enrolled characters (A in the party), in Pelnor's town menu.</summary>
    private static GameSession NewGame(string treasury, out Character a, out Character b)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(7));
        s.StartNewGame(Rules(treasury));
        s.OpenGuild();
        a = s.CreateAndEnroll("Alpha", Race.Human, CharacterClass.Fighter);
        b = s.CreateAndEnroll("Bravo", Race.Elf, CharacterClass.Priest);
        Assert.True(s.TryAddToParty(a, out _));
        s.LeaveGuild();
        return s;
    }

    // ---- the Treasury type ------------------------------------------------------------------------------------------

    [Fact]
    public void Ledger_KeepsTheLast50_WithTripAndBalance()
    {
        var t = new Treasury();
        t.Add(100, TreasuryEntryKind.Accounts);
        t.StartTrip();
        Assert.Equal(40, t.Take(40, TreasuryEntryKind.Inn));
        Assert.Equal(60, t.Balance);
        Assert.Equal(new TreasuryEntry(1, TreasuryEntryKind.Inn, -40, 60), t.Ledger[^1]);
        Assert.Equal(0, t.Take(0, TreasuryEntryKind.Inn));
        for (var i = 0; i < 60; i++)
            t.Add(1, TreasuryEntryKind.Loot);
        Assert.Equal(Treasury.LedgerSize, t.Ledger.Count);
        Assert.Equal(120, t.Balance);
        var back = Treasury.From(t.ToSnapshot());
        Assert.Equal(t.Balance, back.Balance);
        Assert.Equal(t.Trip, back.Trip);
        Assert.Equal(t.Ledger, back.Ledger);
    }

    [Fact]
    public void TrySpend_TakesTheStoresInOrder_ThenTheTreasury_OrNothingWhenShort()
    {
        long purse = 30, sack = 20;
        var t = new Treasury();
        t.Add(100, TreasuryEntryKind.Accounts);
        (Func<long>, Action<long>) p = (() => purse, v => purse = v), k = (() => sack, v => sack = v);
        Assert.True(GoldPurse.TrySpend(60, t, TreasuryEntryKind.Inn, p, k));
        Assert.Equal((0, 0, 90), (purse, sack, t.Balance));
        Assert.False(GoldPurse.TrySpend(91, t, TreasuryEntryKind.Inn, p, k));
        Assert.Equal(90, t.Balance);
        purse = 10;
        Assert.False(GoldPurse.TrySpend(11, null, TreasuryEntryKind.Inn, p)); // off: the purse alone
        Assert.Equal(10, purse);
    }

    // ---- the option -------------------------------------------------------------------------------------------------

    [Fact]
    public void Option_IsQol_EveryGame_OffInBothOriginals_TripInRemaster()
    {
        var d = RuleOptionRegistry.Treasury;
        Assert.True(d.Qol);
        Assert.All(new[] { GameId.P1, GameId.P2, GameId.P3, GameId.P4 }, g => Assert.Contains(d, RuleOptionRegistry.For(g)));
        Assert.False(GameOptions.ForPreset(GameId.P1).TreasuryOn);
        Assert.False(GameOptions.ForPreset(GameId.P3, OptionPreset.Dos).TreasuryOn);
        Assert.True(GameOptions.ForPreset(GameId.P2, OptionPreset.Remaster).TreasuryOn);
        Assert.Equal(GoldCapRule.Lifted, Rules("trip").GoldCap);
        Assert.True(Rules("safe").TreasurySafe);
    }

    [OriginalDataFact]
    public void Off_KeepsTheBankAccounts()
    {
        var s = NewGame("off", out var a, out var b);
        Assert.True(a.GoldInBank > 0);
        Assert.True(b.GoldInBank > 0);
        Assert.Equal(0, s.Treasury.Balance);
        Assert.Null(s.Party.Treasury);
    }

    [OriginalDataFact]
    public void On_StartingGoldOfEveryRosterCharacter_GoesIntoTheTreasury_Once()
    {
        var s = NewGame("trip", out var a, out var b);
        Assert.Equal(0, a.GoldInBank);
        Assert.Equal(0, b.GoldInBank); // not in the party, still the Guild's
        var balance = s.Treasury.Balance;
        Assert.Equal(2 * Data.Rules.StartingGold, balance);
        s.SweepTreasury();
        s.OpenGuild();
        s.LeaveGuild();
        Assert.Equal(balance, s.Treasury.Balance); // idempotent
        Assert.All(s.Treasury.Ledger, e => Assert.Equal(TreasuryEntryKind.Accounts, e.Kind));
    }

    [OriginalDataFact]
    public void On_TheArmoryPaysFromTheTreasury_AndRefusesWhenEvenThatIsShort()
    {
        var s = NewGame("trip", out var a, out _);
        var items = new ItemCatalog(Data.Items);
        var town = s.CurrentTown!;
        var id = Enumerable.Range(0, TownState.StockSlotCount)
            .First(i => town.GetStock(i) > 0 && items.Get(i) is { } it && TownMenus.ListBuyPrice(it, town) <= s.Treasury.Balance);
        var price = TownMenus.ListBuyPrice(items.Get(id)!, town);
        var before = s.Treasury.Balance;
        Assert.Equal(0, s.Party.GoldCarried);
        Assert.Equal(TownTransactionKind.Ok, s.Buy(a, id).Kind);
        Assert.Equal(before - price, s.Treasury.Balance);
        Assert.Equal(new TreasuryEntry(0, TreasuryEntryKind.Armory, -price, before - price), s.Treasury.Ledger[^1]);

        var dear = Enumerable.Range(0, TownState.StockSlotCount)
            .FirstOrDefault(i => town.GetStock(i) > 0 && items.Get(i) is { } it && TownMenus.ListBuyPrice(it, town) > s.Treasury.Balance);
        if (dear > 0)
        {
            var left = s.Treasury.Balance;
            Assert.Equal(TownTransactionKind.Failed, s.Buy(a, dear).Kind);
            Assert.Equal(left, s.Treasury.Balance);
        }
    }

    [OriginalDataFact]
    public void On_TripGold_IsCarriedUntilTown_ThenBankedAsATrip()
    {
        var s = NewGame("trip", out _, out _);
        var start = s.Treasury.Balance;
        Assert.True(s.LeaveTown());
        Assert.Equal(1, s.Treasury.Trip);
        s.Party.GoldCarried = 70;
        s.Sack.Gold = 30;
        s.SweepTreasury(); // in the field: nothing moves under "trip"
        Assert.Equal(100, s.TripGold);
        Assert.Equal(start + 100, s.SpendableGold);
        Assert.Equal(start, s.Treasury.Balance);

        // A loss takes only trip gold: the original's surrender empties the purse and the found gold.
        s.Party.GoldCarried = 0;
        s.Sack.Gold = 0;
        Assert.Equal(start, s.SpendableGold);

        s.Party.GoldCarried = 25;
        s.EnterTown(1);
        Assert.Equal(0, s.TripGold);
        Assert.Equal(start + 25, s.Treasury.Balance);
        Assert.Equal(new TreasuryEntry(1, TreasuryEntryKind.Trip, 25, start + 25), s.Treasury.Ledger[^1]);
    }

    [OriginalDataFact]
    public void Safe_BanksFieldGoldAtOnce()
    {
        var s = NewGame("safe", out _, out _);
        var start = s.Treasury.Balance;
        Assert.True(s.LeaveTown());
        s.Sack.Gold = 40;
        s.SweepTreasury();
        Assert.Equal(0, s.TripGold);
        Assert.Equal(start + 40, s.Treasury.Balance);
        Assert.Equal(TreasuryEntryKind.Loot, s.Treasury.Ledger[^1].Kind);
    }

    [OriginalDataFact]
    public void Save_KeepsTheTreasury_AndAParkedPartyKeepsItsOwnTripGold()
    {
        var s = NewGame("trip", out var a, out var b);
        Assert.True(s.LeaveTown());
        s.Party.GoldCarried = 55; // A's trip gold, out in the wilderness
        var balance = s.Treasury.Balance;
        var file = SaveAndRead(s);
        Assert.Equal(balance, file.Treasury!.Balance);

        // Continue with B (in no party): A's party is parked with its purse; the treasury is shared.
        var number = Array.FindIndex(s.Roster.Slots, x => x?.Name == b.Name) + 1;
        Assert.True(s.Continue(file, number, out var error), error);
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Equal(balance, s.Treasury.Balance);
        Assert.Equal(55, Assert.Single(s.OtherParties).PartyGold);
        Assert.Null(s.OtherParties[0].Treasury);
        Assert.Equal(balance, SaveAndRead(s).Treasury!.Balance);
    }

    [OriginalDataFact]
    public void Transfer_GivesEachRosterCharacterAnEvenShare()
    {
        var s = NewGame("trip", out _, out _);
        var file = SaveAndRead(s);
        var share = TreasuryShares.PerCharacter(file);
        Assert.Equal(file.Treasury!.Balance / 2, share);
        Assert.All(TreasuryShares.Roster(file).Where(c => c is not null), c => Assert.Equal(share, c!.GoldInBank));
        Assert.All(file.RosterSlots.Where(c => c is not null), c => Assert.Equal(0, c!.GoldInBank)); // source unchanged

        var off = SaveAndRead(NewGame("off", out _, out _));
        Assert.Equal(0, TreasuryShares.PerCharacter(off));
        Assert.Same(off.RosterSlots, TreasuryShares.Roster(off));
    }

    [OriginalDataFact]
    public void ASaveWithoutATreasury_LoadedUnderOne_CollectsEveryAccountOnce_AndOnlyTheTownPartysPurse()
    {
        // A save written with the option off: two accounts, A's party out in the wilderness with a purse (then parked),
        // B's new party in town with a purse.
        var s = NewGame("off", out var a, out var b);
        var accounts = a.GoldInBank + b.GoldInBank;
        Assert.True(s.LeaveTown());
        s.Party.GoldCarried = 55;
        var number = Array.FindIndex(s.Roster.Slots, x => x?.Name == b.Name) + 1;
        Assert.True(s.Continue(SaveAndRead(s), number, out var error), error);
        s.Party.GoldCarried = 30;
        var file = SaveAndRead(s);
        Assert.Null(file.Treasury is { Balance: > 0 } ? file.Treasury : null);
        file.Options!.Values["treasury"] = "trip"; // as Restore gives an old Remaster save

        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(7));
        loaded.ApplySnapshot(file);
        Assert.Equal(accounts + 30, loaded.Treasury.Balance);
        Assert.Equal(0, loaded.Party.GoldCarried);
        Assert.Equal(55, Assert.Single(loaded.OtherParties).PartyGold); // the parked party's trip gold stays its own
        loaded.SweepTreasury();
        Assert.Equal(accounts + 30, loaded.Treasury.Balance);
    }

    [OriginalDataFact]
    public void ATransferIn_BanksTheNewcomersAccounts_AtOnce_SoTheFirstBuyWorks()
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(2));
        s.StartNewGame(Rules("trip"));
        Character From(string name) => new()
        {
            Name = name, Race = Race.Dwarf, BirthRace = Race.Dwarf, Class = CharacterClass.Fighter, Status = CharacterStatus.Ok,
            Level = 6, Stats = new CharacterStats(16, 10, 14, 12, 9, 8), HpMax = 50, HpCur = 44, GoldInBank = 3_000,
            Experience = 40_000, SpellsKnown = new int[CharacterCreation.SpellCount],
            Phantasie2 = new() { QuestScore = 7, Origin = Phantasie2Origin.Gelnor },
        };
        s.TransferFromPhantasieII([From("GLADE"), null, From("KLEPTO")]);
        var banked = s.Treasury.Balance;
        Assert.True(banked > 0);
        Assert.All(s.Roster.Occupied, c => Assert.Equal(0, c.GoldInBank));
        Assert.Equal(banked, s.SpendableGold);

        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.Roster.Slots[0]!, out _));
        s.LeaveGuild();
        var items = new ItemCatalog(Data.Items);
        var town = s.CurrentTown!;
        var id = Enumerable.Range(0, TownState.StockSlotCount)
            .First(i => town.GetStock(i) > 0 && items.Get(i) is { } it && TownMenus.ListBuyPrice(it, town) <= banked);
        Assert.Equal(TownTransactionKind.Ok, s.Buy(s.Roster.Slots[0]!, id).Kind);
    }

    [Fact]
    public void Report_ListsBalance_ThenTripsNewestFirst()
    {
        var t = new Treasury();
        t.Add(512, TreasuryEntryKind.Accounts);
        t.StartTrip();
        t.Add(90, TreasuryEntryKind.Trip);
        var lines = TreasuryReport.Lines(t, tripGold: 0);
        Assert.Equal("GUILD TREASURY", lines[0]);
        Assert.Equal("BALANCE 602 GP", lines[1]);
        Assert.Equal("TRIP 1: +90 GP", lines[3]);
        Assert.Contains("TRIP GOLD", lines[4]);
        Assert.Equal("TRIP 0: +512 GP", lines[6]);
        Assert.Equal("GOLD 602 (+90 TRIP)", TreasuryReport.GoldLine(602, 90));
    }
}
