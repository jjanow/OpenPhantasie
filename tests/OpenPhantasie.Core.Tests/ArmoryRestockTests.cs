using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>s41: town-entry Armory churn, OV1 <c>0x8e1</c> (town-menus.md §Armory restock).</summary>
public class ArmoryRestockTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    private sealed class Words(params int[] values) : IDosRandomSource
    {
        private readonly Queue<int> _q = new(values);
        public int Draws { get; private set; }
        public int Next(int minInclusive, int maxExclusive) => throw new InvalidOperationException("restock uses rand words only");
        public int NextDosWord()
        {
            Draws++;
            return _q.Dequeue();
        }
    }

    private static TownState Town(int[] restock)
    {
        var stock = new int[TownState.StockSlotCount];
        Array.Fill(stock, 5);
        return new TownState(1, 0, "T", 150, stock) { RestockItems = restock };
    }

    private static int[] Wipes(params int[] words) =>
        words.Concat(Enumerable.Repeat(0, ArmoryRestock.WipeCount - words.Length)).ToArray();

    [Fact]
    public void Wipes_EighteenWholeStacks_InIds0To119()
    {
        var town = Town(Enumerable.Range(150, 45).ToArray());
        // rand/274: 32767 → 119 (the top id), 274 → 1, 0 → 0 (the remaining 16 draws hit id 0 again).
        var rng = new Words(Wipes(32767, 274).Concat([0, 0, 0, 0]).ToArray());

        ArmoryRestock.Apply(town, averagePartyLevel: 0, rng, dosRule: true);

        Assert.Equal(0, town.GetStock(119));
        Assert.Equal(0, town.GetStock(1));
        Assert.Equal(0, town.GetStock(0));
        Assert.Equal(5, town.GetStock(118));
        Assert.Equal(5, town.GetStock(120)); // MAGIC 10 and everything above can never be wiped
        Assert.Equal(ArmoryRestock.WipeCount, rng.Draws); // average level 0: no draw for the 4 adds
    }

    [Fact]
    public void AverageLevelZero_SetsListEntry0_FourTimes()
    {
        var town = Town(Enumerable.Range(150, 45).ToArray());
        ArmoryRestock.Apply(town, 0, new Words(Wipes()), dosRule: true);
        Assert.Equal(1, town.GetStock(150)); // set to 1, not +1: the 5-stack drops to 1
        Assert.Equal(5, town.GetStock(151));
    }

    [Theory]
    [InlineData(1, 32766, 0)]  // 32767/1 = 32767 → index 0 ...
    [InlineData(1, 32767, 1)]  // ... except rand = 32767 → index 1 (DOS can pick past L-1)
    [InlineData(3, 10922, 1)]  // 32767/3 = 10922
    [InlineData(3, 32766, 3)]
    [InlineData(15, 32767, 15)] // LevelUp.MaxLevel
    [InlineData(20, 32767, 20)] // UNDEAD fate level 20: still inside the 45-entry list
    public void AddIndex_IsRandOverQuotient(int level, int word, int index)
    {
        var restock = Enumerable.Range(130, 45).ToArray();
        var town = Town(restock);
        ArmoryRestock.Apply(town, level, new Words(Wipes().Concat([word, word, word, word]).ToArray()), dosRule: true);
        Assert.Equal(1, town.GetStock(restock[index]));
        Assert.Equal(5, town.GetStock(restock[index == 0 ? 1 : 0]));
    }

    [Fact]
    public void ManualRule_D054_OnlyWeaponsAndPotionsChange_SameDraws()
    {
        // Restock list: SM SHLD+2 (6, shield), MACE (49, weapon). Wipes: id 0 (shield), 23 (LEATHER), 49 (MACE), 105 (HEALING 5).
        var town = Town([6, 49]);
        var rng = new Words(Wipes(0, 23 * 274, 49 * 274, 105 * 274).Concat([0, 32767, 32767, 32767]).ToArray());

        ArmoryRestock.Apply(town, averagePartyLevel: 1, rng);

        Assert.Equal(5, town.GetStock(0));   // shield: stays
        Assert.Equal(5, town.GetStock(23));  // armor: stays
        Assert.Equal(1, town.GetStock(49));  // weapon: wiped, then restocked to 1 (index 1)
        Assert.Equal(0, town.GetStock(105)); // potion: wiped
        Assert.Equal(5, town.GetStock(6));   // restock index 0 = shield: not added
        Assert.Equal(ArmoryRestock.WipeCount + ArmoryRestock.AddCount, rng.Draws); // DOS draw count kept
    }

    [Fact]
    public void ManualRule_D054_SoldOutArmorComesBack_ButStacksNeverDrop()
    {
        // Restock list: SM SHLD+2 (6, shield), LEATHER (23, armor). Level 1, words 0 → index 0, 32767 → index 1.
        var town = Town([6, 23]);
        town.SetStock(6, 0); // bought out
        ArmoryRestock.Apply(town, averagePartyLevel: 1, new Words(Wipes().Concat([0, 32767, 0, 32767]).ToArray()));

        Assert.Equal(1, town.GetStock(6));  // sold-out shield refilled to 1
        Assert.Equal(5, town.GetStock(23)); // armor 5-stack not cut to 1
    }

    [OriginalDataFact]
    public void TownsJson_CarriesRestockLists()
    {
        var pelnor = Data.CreateTownState(1);
        Assert.Equal(45, pelnor.RestockItems.Count);
        Assert.Equal(new[] { 49, 23, 51, 6, 105 }, pelnor.RestockItems.Take(5)); // MACE, LEATHER, SM AXE, SM SHLD+2, HEALING 5
        Assert.Equal(110, pelnor.RestockItems[44]); // HEALING10
        Assert.All(Data.CreateTownState(8).RestockItems, id => Assert.Equal(0, id)); // PHANTASIA: all zero
        Assert.Equal(pelnor.RestockItems, pelnor.Clone().RestockItems);
    }

    [OriginalDataFact]
    public void Buy_RefusesItemZero_TheArmoryListStartsAtOne()
    {
        var party = new Party();
        var hero = new Character { Name = "a", Class = CharacterClass.Fighter, Stats = new CharacterStats(15, 10, 10, 10, 10, 10) };
        party.Members.Add(hero);
        party.GoldCarried = 1000;
        var town = Data.CreateTownState(8);
        town.SetStock(0, 1); // PHANTASIA's all-zero list puts it there
        var result = TownMenus.Buy(party, hero, town, new ItemCatalog(Data.Items), 0);
        Assert.Equal(TownTransactionKind.Failed, result.Kind);
        Assert.Equal(1, town.GetStock(0));
    }

    [OriginalDataFact]
    public void Session_EnterTown_RestocksWithTheExitAverageLevel_AndSavesIt()
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(11));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Porter", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        hero.Level = 3;
        session.DepartGuildToWilderness();
        Assert.Equal(3, session.TownExitAverageLevel);

        session.EnterTown(1);
        var stock = session.CurrentTown!;
        // Only Pelnor's first four list entries (index ≤ 3) can have been added, so any 1-stack of a list item past
        // index 3 that was not in the master stock would be a bug. And nothing ≥ 120 changes.
        var master = Data.CreateTownState(1);
        for (var id = 120; id < TownState.StockSlotCount; id++)
            Assert.Equal(master.GetStock(id), stock.GetStock(id));
        foreach (var id in master.RestockItems.Skip(4).Except(master.RestockItems.Take(4)).Where(i => i < 120))
            Assert.True(stock.GetStock(id) == 0 || stock.GetStock(id) == master.GetStock(id));

        hero.Level = 7;
        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(12));
        loaded.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(session.CaptureSnapshot())));
        Assert.Equal(3, loaded.TownExitAverageLevel); // [0x63ec] is the stale exit value, not recomputed
        Assert.Equal(45, loaded.CurrentTown!.RestockItems.Count);
    }
}
