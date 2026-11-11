using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// The Guild treasury in Phantasie IV (P3 QoL, <c>docs/plan/11-guild-treasury.md</c> sections 2–5): the sweep on arrival
/// and leaving, the spends through <see cref="P4Party.TrySpend"/>, the losses on trip gold only, <c>safe</c>'s banking,
/// the event gold opcodes, the bank's treasury view and the save. With the option off nothing changes.
/// </summary>
public class Phantasie4TreasuryTests
{
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
    private static readonly GameOptions Trip = Manual.With(RuleOptionRegistry.Treasury.Id, "trip");
    private static readonly GameOptions CodeTrip = Code.With(RuleOptionRegistry.Treasury.Id, "trip");
    private static readonly GameOptions Safe = Manual.With(RuleOptionRegistry.Treasury.Id, "safe");
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;
    private static readonly P4Atlas Atlas = Phantasie4World.LoadAtlas();
    private static readonly P4ServiceData Data = Phantasie4World.LoadServiceData();
    private static readonly P4BattleData B = Phantasie4World.LoadBattleData();

    /// <summary>Every draw returns the lowest value.</summary>
    private sealed class Low : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
    }

    private static P4Member Member(string name, int level = 1)
    {
        var m = new P4Member
        {
            Name = name, Class = P4Ids.Fighter, Race = P4Ids.Human, Level = level, HpMax = 60, Hp = 60, MpMax = 4, Mp = 4,
            Abilities = [15, 15, 15, 15, 12, 10], AgingFactor = 200, Social = 3, MoveBase = 7,
        };
        Array.Fill(m.Skills, 60);
        return m;
    }

    /// <summary>A party of <paramref name="n"/> whose members are also in the guild roster (slots 0…).</summary>
    private static P4Party Party(int n = 2, long gold = 0)
    {
        var p = new P4Party { Gold = gold, AverageLevel = 1 };
        for (var i = 0; i < n; i++)
        {
            var m = Member($"M{i}");
            m.GuildSlot = i;
            p.Roster[i] = m.Clone();
            p.Members.Add(m);
        }
        return p;
    }

    private static P4TownLoop Loop(P4Party party, GameOptions o) => new(o, T, Atlas, party, new Low(), Data);

    private static P4TownLoop Town(P4Party party, GameOptions o, int town = 2)
    {
        var w = Loop(party, o);
        w.Arrive(town);
        while (w.Prompt is P4TownPrompt.Shares or P4TownPrompt.Wait)
        {
            if (w.Prompt == P4TownPrompt.Shares)
                w.EnterShares([.. party.Members.Select(_ => 1)]);
            else
                w.Continue();
        }
        return w;
    }

    // --- Off: the original ----------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Off_ArrivalAsksTheGoldShare_AndNothingReachesTheTreasury()
    {
        var party = Party(gold: 3000);
        party.Members[0].Account = 100;
        var w = Loop(party, Manual);
        w.Arrive(2);
        Assert.Equal((P4TownPrompt.Shares, P4ShareKind.Gold), (w.Prompt, w.SharesFor)); // D4-030 manual
        w.EnterShares([1, 1]);
        Assert.Equal(0, party.Gold);
        Assert.Equal(0, party.Treasury.Balance);
        Assert.Empty(party.Treasury.Ledger);
        Assert.True(party.Members[0].Account > 100);
        Assert.False(party.TrySpend(Manual, 1, TreasuryEntryKind.Armory)); // the purse alone pays
    }

    // --- The sweep ------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Arrival_SweepsPurseAndAccounts_EachCharacterOnce_AndSkipsTheGoldShare()
    {
        var party = Party(gold: 3000);
        party.Members[0].Account = 100;      // the party copy holds the latest account …
        party.Roster[0]!.Account = 70;       // … its roster record a stale one: not counted again
        party.Roster[5] = Member("Spare");
        party.Roster[5]!.Account = 50;       // a roster record no member links to
        var w = Loop(party, Trip);
        w.Arrive(2);
        Assert.Equal(P4TownPrompt.Menu, w.Prompt); // no D4-030 gold share under the treasury
        Assert.Equal(3150, party.Treasury.Balance);
        Assert.Equal(0, party.Gold);
        Assert.All(party.Members, m => Assert.Equal(0, m.Account));
        Assert.All(party.Roster.Where(m => m is not null), m => Assert.Equal(0, m!.Account));
        Assert.Equal([(TreasuryEntryKind.Accounts, 150L), (TreasuryEntryKind.Trip, 3000L)],
            party.Treasury.Ledger.Select(e => (e.Kind, e.Amount)));
        // Idempotent: the guild's roster sync and every later sweep find nothing more.
        w.Enter(P4Building.Guild);
        w.Choose(3);
        party.SweepTreasury(Trip, inTown: true);
        Assert.Equal(3150, party.Treasury.Balance);
        Assert.Equal(2, party.Treasury.Ledger.Count);
    }

    [OriginalDataFact]
    public void Arrival_KeepsTheExperienceShareOut()
    {
        var party = Party(gold: 500);
        party.ExperiencePool = 100;
        var w = Loop(party, Trip);
        w.Arrive(2);
        Assert.Equal((P4TownPrompt.Shares, P4ShareKind.Experience), (w.Prompt, w.SharesFor));
        Assert.Equal(500, party.Treasury.Balance);
    }

    [OriginalDataFact]
    public void Leaving_SweepsAndStartsANewTrip()
    {
        var party = Party();
        var w = Town(party, CodeTrip);
        party.Members[1].Account = 40;
        w.Enter(P4Building.Leave);
        Assert.Equal(P4TownPrompt.Left, w.Prompt);
        Assert.Equal(40, party.Treasury.Balance);
        Assert.Equal(1, party.Treasury.Trip);
        Assert.Equal(0, party.Members[1].Account);
    }

    [OriginalDataFact]
    public void Away_PurseStaysTripGold_AccountsAreSwept()
    {
        var party = Party(gold: 900);
        party.Members[0].Account = 10;
        party.SweepTreasury(Trip, inTown: false);
        Assert.Equal((900L, 10L), (party.Gold, party.Treasury.Balance));
        Assert.Equal(910, party.SpendableGold(Trip));
        Assert.Equal(900, party.SpendableGold(Manual));
    }

    // --- Spending -------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void TrySpend_TakesThePurseFirst_ThenTheTreasury_AndRefusesWhenShort()
    {
        var party = new P4Party { Gold = 30 };
        party.Treasury.Add(100, TreasuryEntryKind.Trip);
        Assert.True(party.TrySpend(Trip, 50, TreasuryEntryKind.Armory));
        Assert.Equal((0L, 80L), (party.Gold, party.Treasury.Balance));
        // The purse's 30 is logged as trip gold brought in, then the whole price as the spend (balances 130, 80).
        Assert.Equal([(TreasuryEntryKind.Trip, 30L, 130L), (TreasuryEntryKind.Armory, -50L, 80L)],
            party.Treasury.Ledger.Skip(1).Select(e => (e.Kind, e.Amount, e.Balance)));
        Assert.False(party.TrySpend(Trip, 81, TreasuryEntryKind.Armory));
        Assert.Equal((0L, 80L), (party.Gold, party.Treasury.Balance));
        Assert.False(party.CanAfford(Manual, 1)); // off: the treasury does not count
    }

    [OriginalDataFact]
    public void Armory_PaysFromTheTreasury_AndRefusesWhenShort()
    {
        var party = Party(1);
        var w = Town(party, CodeTrip, town: 13);
        Array.Clear(party.ShopStock);
        party.ShopStock[40] = 2; // Long Sword
        w.Enter(P4Building.Armory);
        var price = w.ShopPrice(40);
        Assert.True(price > 10);
        party.Gold = 10;
        party.Treasury.Add(price - 5, TreasuryEntryKind.Trip);
        w.Choose(0);
        Assert.Contains(40, party.Members[0].Items);
        Assert.Equal((0L, 5L), (party.Gold, party.Treasury.Balance));
        Assert.Equal((TreasuryEntryKind.Trip, 10L), (party.Treasury.Ledger[^2].Kind, party.Treasury.Ledger[^2].Amount));
        Assert.Equal((TreasuryEntryKind.Armory, (long)-price), (party.Treasury.Ledger[^1].Kind, party.Treasury.Ledger[^1].Amount));
        w.Choose(0); // 5 GP left: "can't pay"
        Assert.Equal(1, party.Members[0].Items.Count(i => i == 40));
        Assert.Equal(5, party.Treasury.Balance);
    }

    [OriginalDataFact]
    public void Training_PaysFromTheTreasury_AndBanksTheLevelUpGold()
    {
        var party = Party(1);
        var m = party.Members[0];
        m.Experience = 1_000_000;
        var w = Town(party, CodeTrip);
        var cost = P4Training.TrainingCost(T.Training, 1, 12, P4Town.PriceIndex(T.Towns, 2));
        party.Treasury.Add(cost, TreasuryEntryKind.Trip);
        w.Enter(P4Building.Guild);
        w.Choose(1);
        w.Answer(true);
        Assert.Equal(2, party.Treasury.Ledger.Count(e => e.Kind != TreasuryEntryKind.Accounts));
        Assert.Equal(-cost, party.Treasury.Ledger.Single(e => e.Kind == TreasuryEntryKind.Training).Amount);
        // The level-up gold is banked before the first skill pick shows (the sweep follows every town step).
        Assert.Equal(P4TownPrompt.Pick, w.Prompt);
        Assert.Equal((0L, 27L * 1 * 30), (m.Account, party.Treasury.Balance));
        for (var i = 0; i < 3; i++)
            w.Choose((int)P4Skill.Attack);
        Assert.Equal(2, m.Level);
        Assert.Equal(0, m.Account); // the level-up gold went into the treasury
        Assert.Equal(27 * 1 * 30, party.Treasury.Balance); // Noble: 3^3 × L × 30
        Assert.Equal(TreasuryEntryKind.Accounts, party.Treasury.Ledger[^1].Kind);
    }

    [OriginalDataFact]
    public void Shipwright_SellsAShip_FromSpendableGold()
    {
        var site = Atlas.Towns.Values.First(s => s.Type == 1 && s.ShipX is not null);
        var party = Party(1);
        var w = Town(party, CodeTrip, site.Id);
        party.Treasury.Add(w.ShipPrice, TreasuryEntryKind.Trip);
        w.Enter(P4Building.Shipwright);
        Assert.Equal(P4TownPrompt.Confirm, w.Prompt);
        w.Answer(true);
        Assert.Equal(P4ShipState.Moored, party.Ship);
        Assert.Equal(0, party.Treasury.Balance);
        Assert.Equal(TreasuryEntryKind.Ship, party.Treasury.Ledger[^1].Kind);
    }

    [OriginalDataFact]
    public void Sale_OnArrival_GoesStraightIntoTheTreasury()
    {
        var party = Party(2);
        party.Pack[0] = 1;
        var w = Loop(party, Trip);
        w.Arrive(2);
        Assert.Equal(1, w.HeldItem);
        w.Choose(3); // S: sell
        Assert.Equal(0, party.Gold);
        Assert.Equal(Data.Items.Value(1) / 4 + 1, party.Treasury.Balance);
        Assert.Equal(TreasuryEntryKind.Sale, party.Treasury.Ledger[^1].Kind);
    }

    // --- Losses and safe ------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Beg_TakesOnlyTheTripGold()
    {
        var p = Party(1, gold: 5000);
        p.Members[0].Items[0] = 0x1C + 4;
        p.Treasury.Add(10000, TreasuryEntryKind.Trip);
        var b = P4Battle.Dungeon(CodeTrip, T, B, p, new Low(), 0);
        b.ChooseOption(P4EncounterOption.Beg);
        Assert.Equal(P4BattleEnd.Begged, b.End);
        Assert.Equal(5000 % 256, p.Gold);
        Assert.Equal(10000, p.Treasury.Balance);
    }

    [OriginalDataFact]
    public void Safe_BanksBattleGold_AtOnce()
    {
        var p = Party(1, gold: 0);
        var m = p.Members[0];
        m.Items[0] = 0x1C + 4;
        m.MoveBase = P4Creation.Stride(Safe, T.Creation, m.Race, m.Class);
        m.MeleeBonus = P4TownRules.MeleeBonus(m);
        var b = P4Battle.Dungeon(Safe, T, B, p, new Low(), 0);
        foreach (var x in b.Monsters.Skip(1))
            x.Gone = true;
        b.Monsters[0].Square = 38;
        b.Monsters[0].Hp = 1;
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.True(b.Order(new P4Order(P4ActionKind.Thrust, Target: 38)));
        Assert.Equal(P4FightOutcome.Won, b.Outcome);
        Assert.True(b.Gold > 0);
        Assert.Equal(0, p.Gold);
        Assert.Equal(b.Gold, p.Treasury.Balance);
        Assert.Equal(b.Gold, p.GoldSinceTown); // D4-062's count keeps its meaning
        Assert.Equal(TreasuryEntryKind.Loot, p.Treasury.Ledger[^1].Kind);
    }

    // --- Event gold -----------------------------------------------------------------------------------------------------

    private static P4DungeonFile OpenMap(byte[] code)
    {
        var words = new ushort[31 * 31];
        for (var i = 0; i < words.Length; i++)
            words[i] = (31 << 6) | (7 << 11);
        return new P4DungeonFile(4, "test", words, [new(0x20, 0, 4, 0), new(5, 5, 4, 2)], code);
    }

    private static P4Dungeon Dungeon(P4Party party, GameOptions o, P4DungeonFile f) =>
        new(o, T, party, new Low(), _ => f, i => $"item{i}");

    [OriginalDataFact]
    public void Safe_BanksGiveGold_AtOnce()
    {
        byte[] code = [0x22, 0x00, 0x13, 20, 0, 0x00]; // (5,5): GIVEGOLD 20; END
        var party = Party();
        Dungeon(party, Safe, OpenMap(code)).Enter(4, 5, 5, 0);
        Assert.Equal((0L, 20L, 20L), (party.Gold, party.Treasury.Balance, party.GoldSinceTown));
        var trip = Party();
        Dungeon(trip, Trip, OpenMap(code)).Enter(4, 5, 5, 0);
        Assert.Equal((20L, 0L), (trip.Gold, trip.Treasury.Balance)); // trip: found gold stays in the purse
    }

    [OriginalDataTheory]
    [InlineData(false, 30L, 0L)]  // off: CMPGOLD sees 30 < 100, the script jumps past TAKEGOLD
    [InlineData(true, 0L, 130L)]  // on: CMPGOLD sees 230, TAKEGOLD takes the purse, then 70 from the treasury
    public void CmpGold_And_TakeGold_ReadTheSameSpendableGold(bool on, long purse, long treasury)
    {
        // (5,5): CMPGOLD 100; JLT 11; TAKEGOLD 100; END
        byte[] code = [0x22, 0x00, 0x12, 100, 0, 0x06, 11, 0, 0x33, 100, 0, 0x00];
        var party = Party();
        party.Gold = 30;
        party.Treasury.Add(200, TreasuryEntryKind.Trip);
        Dungeon(party, on ? Trip : Manual, OpenMap(code)).Enter(4, 5, 5, 0);
        Assert.Equal(purse, party.Gold);
        Assert.Equal(on ? treasury : 200, party.Treasury.Balance);
        if (on)
            Assert.Equal(TreasuryEntryKind.Event, party.Treasury.Ledger[^1].Kind);
    }

    /// <summary>
    /// O_008's fortune teller (INPUTN; CMPGOLD 0; JLT; TAKEGOLD 0): above 30,000 gold CMPGOLD answers 30,000 whatever the
    /// amount (<c>bls</c> at <c>0x1989a</c>), so a typed amount over the gold reaches TAKEGOLD's unchecked <c>sub.l</c>
    /// (<c>0x19f1e</c>). On, the treasury takes only what there is (purse and treasury stay ≥ 0, the trip part logged);
    /// off keeps the original's unchecked subtraction (Core's long goes below zero; the X68000's u32 wraps, events.md).
    /// </summary>
    [OriginalDataTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void TakeGold_PastCmpGoldsCap_NeverGoesBelowZeroUnderTheTreasury(bool on)
    {
        // (5,5): INPUTN; CMPGOLD 0; JLT 12; TAKEGOLD 0; END
        byte[] code = [0x22, 0x00, 0x0E, 0x12, 0, 0, 0x06, 12, 0, 0x33, 0, 0, 0x00];
        var party = Party();
        party.Gold = on ? 1000 : 31000;
        if (on)
            party.Treasury.Add(31000, TreasuryEntryKind.Accounts);
        var d = Dungeon(party, on ? Trip : Manual, OpenMap(code));
        d.Enter(4, 5, 5, 0);
        Assert.Equal(P4DungeonPrompt.Number, d.Prompt);
        d.EnterNumber(40000);
        if (on)
        {
            Assert.Equal((0L, 0L), (party.Gold, party.Treasury.Balance));
            Assert.Equal(
                [(TreasuryEntryKind.Trip, 1000L, 32000L), (TreasuryEntryKind.Event, -32000L, 0L)],
                party.Treasury.Ledger.Skip(1).Select(e => (e.Kind, e.Amount, e.Balance)));
        }
        else
            Assert.Equal(-9000L, party.Gold); // unchanged off path (the original wraps its u32 instead) [~]
    }

    // --- The bank's treasury view ---------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Bank_OpensTheTreasuryView_SpaceLeaves()
    {
        var party = Party(gold: 700);
        var w = Town(party, CodeTrip);
        w.Enter(P4Building.Bank);
        Assert.Equal(P4TownPrompt.Wait, w.Prompt);
        Assert.Equal(P4BankView.Treasury, w.BankWindow!.View);
        Assert.Equal(TreasuryReport.Lines(party.Treasury), w.TreasuryLines);
        Assert.Contains(GameText.F("core.treasury.balance_x", 700), w.TreasuryLines!);
        w.Continue();
        Assert.Equal(P4TownPrompt.Menu, w.Prompt);
        Assert.Null(w.TreasuryLines);
        // Off: the original's accounts.
        var off = Town(Party(), Code);
        off.Enter(P4Building.Bank);
        Assert.Equal((P4TownPrompt.Pick, P4BankView.Accounts), (off.Prompt, off.BankWindow!.View));
        Assert.Null(off.TreasuryLines);
    }

    // --- Saves ----------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Save_KeepsBalanceAndLedger_AndAnEmptyTreasuryWritesNothing()
    {
        var party = Party(gold: 0);
        party.Treasury.Add(1234, TreasuryEntryKind.Trip);
        party.Treasury.StartTrip();
        party.Treasury.Take(34, TreasuryEntryKind.Inn);
        var game = Phantasie4World.NewCampaign(Trip, party, new Low(), new P4Location(P4Place.Town, 2, 0, 0));
        var json = Phantasie4SaveCodec.WriteJson(game.Capture());
        var back = Phantasie4World.RestoreCampaign(Phantasie4SaveCodec.ReadJson(json), new Low());
        Assert.Equal(1200, back.Party.Treasury.Balance);
        Assert.Equal(1, back.Party.Treasury.Trip);
        Assert.Equal(party.Treasury.Ledger, back.Party.Treasury.Ledger);
        Assert.Equal(json, Phantasie4SaveCodec.WriteJson(back.Capture()));

        var off = Phantasie4World.NewCampaign(Manual, Party(), new Low(), new P4Location(P4Place.Town, 2, 0, 0));
        Assert.DoesNotContain("\"ledger\"", Phantasie4SaveCodec.WriteJson(off.Capture()), StringComparison.Ordinal);
    }

    [OriginalDataFact]
    public void Load_InTown_SweepsTheAccounts()
    {
        var party = Party();
        var game = Phantasie4World.NewCampaign(Trip, party, new Low(), new P4Location(P4Place.Town, 2, 0, 0));
        game.Start();
        while (game.Town.Prompt is P4TownPrompt.Wait)
            game.Town.Continue();
        var snap = game.Capture();
        // An account the save holds (e.g. a save made before the option was on) is collected on loading.
        var members = snap.Party.Members.Select((m, i) => i == 0 ? m with { More = m.More with { Account = 75 } } : m).ToArray();
        var back = Phantasie4World.RestoreCampaign(snap with { Party = snap.Party with { Members = members } }, new Low());
        Assert.Equal(75, back.Party.Treasury.Balance);
        Assert.Equal(0, back.Party.Members[0].Account);
    }
}
