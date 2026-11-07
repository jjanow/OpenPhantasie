using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

public class TownMenusTests
{
    private static GameDataRepository Data => GameDataRepository.LoadPhantasie1();

    private static (Party party, Character hero, TownState pelnor, IItemCatalog catalog) FreshParty()
    {
        var data = Data;
        var hero = CharacterCreation.Create(
            new CharacterCreationRequest
            {
                Name = "Tester",
                Race = Race.Human,
                Class = CharacterClass.Fighter,
            },
            data.CreationTables,
            new SeededDosRandomSource(42));

        var party = new Party();
        party.Members.Add(hero);
        var pelnor = data.CreateTownState(1);
        return (party, hero, pelnor, data.CreateItemCatalog());
    }

    [OriginalDataFact]
    public void Towns_LoadTwelveWithPelnorPrice150()
    {
        var data = Data;
        Assert.Equal(GameDataRepository.ExpectedTownCount, data.Towns.Count);
        var pelnor = data.GetTown(1);
        Assert.Equal("PELNOR", pelnor.Name);
        Assert.Equal(150, pelnor.PriceIndex);
        Assert.Equal(5, pelnor.Stock[46]); // DAGGER
    }

    [Theory]
    [InlineData(0, 150, 1)]   // V=0 → 1
    [InlineData(6, 150, 11)]  // DAGGER
    [InlineData(12, 150, 44)] // MAGIC 1
    [InlineData(20, 150, 121)] // SCROLL 8
    public void BuyPrice_MatchesDocumentedFormula(int v, int townIndex, int expected) =>
        Assert.Equal(expected, ItemRules.BuyPrice(v, townIndex));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(6, 4)]
    [InlineData(12, 18)]
    [InlineData(20, 50)]
    public void SellPrice_MatchesDocumentedFormula(int v, int expected) =>
        Assert.Equal(expected, ItemRules.SellPrice(v));

    [OriginalDataFact]
    public void Pass2PurchaseList_ChargesFull177SharedPurseGold()
    {
        var catalog = Data.CreateItemCatalog();
        var pelnor = Data.CreateTownState(1);
        var ids = new[] { 21, 46, 111, 128 };
        var total = ids.Sum(id => TownMenus.ListBuyPrice(catalog.Get(id)!, pelnor));
        Assert.Equal(177, total);
        // DOS's three 256→197 saved carried-gold shares settle this full cost:
        // (3 * 256 - 177) / 3 == 197. It is not a CHA discount.
    }

    [OriginalDataFact]
    public void Bank_WithdrawThenDeposit()
    {
        var (party, hero, _, _) = FreshParty();
        Assert.Equal(RulesProfile.PhantasieI.StartingGold, hero.GoldInBank);
        Assert.Equal(0, party.GoldCarried);

        var w = TownMenus.Withdraw(party, hero, 100);
        Assert.Equal(TownTransactionKind.Ok, w.Kind);
        Assert.Equal(100, party.GoldCarried);
        Assert.Equal(RulesProfile.PhantasieI.StartingGold - 100, hero.GoldInBank);

        // D-049 (manual): Deposit puts ALL carried gold back by shares.
        var d = TownMenus.DepositShares(party, [1]);
        Assert.Equal(TownTransactionKind.Ok, d.Kind);
        Assert.Equal("Each share is 100 gold. Deposited: Tester 100.", d.Message);
        Assert.Equal(0, party.GoldCarried);
        Assert.Equal(RulesProfile.PhantasieI.StartingGold, hero.GoldInBank);
        Assert.Equal(TownTransactionKind.Failed, TownMenus.DepositShares(party, [1]).Kind); // nothing carried
    }

    [OriginalDataFact]
    public void Armory_BuyDecrementsStockAndSpendsGold()
    {
        var (party, hero, pelnor, catalog) = FreshParty();
        TownMenus.Withdraw(party, hero, hero.GoldInBank);

        var before = pelnor.GetStock(46);
        var price = TownMenus.ListBuyPrice(catalog.Get(46)!, pelnor);
        var result = TownMenus.Buy(party, hero, pelnor, catalog, 46);

        Assert.Equal(TownTransactionKind.Ok, result.Kind);
        Assert.Equal(before - 1, pelnor.GetStock(46));
        Assert.Equal(RulesProfile.PhantasieI.StartingGold - price, party.GoldCarried);
        Assert.Contains(46, hero.InventoryItemIds);
    }

    [OriginalDataFact]
    public void Armory_BuyingAScrollLeavesWord28Alone()
    {
        // s19: @28 is weapon damage A, not a scroll counter; the equipment screen counts scrolls
        // from the carried items (COM 0x2cf6).
        var (party, hero, pelnor, catalog) = FreshParty();
        TownMenus.Withdraw(party, hero, hero.GoldInBank);
        hero.WeaponDamageA = 2;

        var result = TownMenus.Buy(party, hero, pelnor, catalog, 128); // SCROLL 8

        Assert.Equal(TownTransactionKind.Ok, result.Kind);
        Assert.Equal(2, hero.WeaponDamageA);
        Assert.Equal(1, hero.ScrollCount);
    }

    [OriginalDataFact]
    public void Armory_BuyFillsTheFirstEmptySlot()
    {
        // [C] s58: OV1 0x1c18..0x1c2e finds the first 0 slot, the buy is written there (0x1cef).
        var (party, hero, pelnor, catalog) = FreshParty();
        TownMenus.Withdraw(party, hero, hero.GoldInBank);
        hero.InventoryItemIds.Clear();
        hero.InventoryItemIds.AddRange(new[] { 1, 0, 21, 0, 121 });

        var result = TownMenus.Buy(party, hero, pelnor, catalog, 46);

        Assert.Equal(TownTransactionKind.Ok, result.Kind);
        Assert.Equal(new[] { 1, 46, 21, 0, 121 }, hero.InventoryItemIds);
    }

    [OriginalDataFact]
    public void Armory_EightItemsAndAGap_IsNotFull()
    {
        // The 9-item test counts held items: a drunk potion's empty slot is free space.
        var (party, hero, pelnor, catalog) = FreshParty();
        TownMenus.Withdraw(party, hero, hero.GoldInBank);
        hero.InventoryItemIds.Clear();
        hero.InventoryItemIds.AddRange(new[] { 121, 121, 121, 0, 121, 121, 121, 121, 121 });

        var result = TownMenus.Buy(party, hero, pelnor, catalog, 46);

        Assert.Equal(TownTransactionKind.Ok, result.Kind);
        Assert.Equal(46, hero.InventoryItemIds[3]);
        Assert.Equal(Character.InventorySlots, hero.InventoryItemIds.Count);
        Assert.True(CarriedItems.IsFull(hero));
    }

    [OriginalDataFact]
    public void Armory_RefusesShopperWithNineItems()
    {
        // Manual: 9 items per character; DOS Armory OV1 0x1c16 turns the shopper away.
        var (party, hero, pelnor, catalog) = FreshParty();
        TownMenus.Withdraw(party, hero, hero.GoldInBank);
        while (hero.InventoryItemIds.Count < Character.InventorySlots)
            hero.InventoryItemIds.Add(121);
        var gold = party.GoldCarried;
        var stock = pelnor.GetStock(46);

        var result = TownMenus.Buy(party, hero, pelnor, catalog, 46);

        Assert.Equal(TownTransactionKind.Failed, result.Kind);
        Assert.Equal(TownMenus.NinthItemMessage, result.Message);
        Assert.Equal(Character.InventorySlots, hero.InventoryItemIds.Count);
        Assert.Equal(gold, party.GoldCarried);
        Assert.Equal(stock, pelnor.GetStock(46));
    }

    [OriginalDataFact]
    public void Armory_EmptyStockFails()
    {
        var (party, hero, pelnor, catalog) = FreshParty();
        TownMenus.Withdraw(party, hero, hero.GoldInBank);
        pelnor.SetStock(46, 0);
        var result = TownMenus.Buy(party, hero, pelnor, catalog, 46);
        Assert.Equal(TownTransactionKind.Failed, result.Kind);
        Assert.Contains("empty", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [OriginalDataFact]
    public void Inn_RestoresHpAndMp()
    {
        var (party, hero, _, _) = FreshParty();
        hero.HpCur = 1;
        hero.MagicCur = 0;
        var age = hero.AgeClock;
        var result = TownMenus.Rest(party, new SeededDosRandomSource(1));
        Assert.Equal(TownTransactionKind.Ok, result.Kind);
        Assert.Equal(hero.HpMax, hero.HpCur);
        Assert.Equal(hero.MagicMax, hero.MagicCur);
        Assert.Equal(age + 1, hero.AgeClock); // D-004: two weeks
    }

    [OriginalDataFact]
    public void Inn_RefillsDeadMembersWithoutReviving_AndNeverRaisesTheirMp()
    {
        var (party, hero, _, _) = FreshParty();
        hero.Status = CharacterStatus.Dead;
        hero.HpCur = 0;
        hero.CastSinceRest = true;
        hero.MpCeiling = hero.MagicMax + 5;
        var mpMax = hero.MagicMax;
        var result = TownMenus.Rest(party, new SeededDosRandomSource(1));
        Assert.Equal(CharacterStatus.Dead, hero.Status);
        Assert.Equal(hero.HpMax, hero.HpCur);
        Assert.Equal(mpMax, hero.MagicMax);
        Assert.True(hero.CastSinceRest); // only consumed for non-dead members
        Assert.Contains("IN PEACE", result.Message);
    }

    [OriginalDataFact]
    public void Inn_MagicIncreaseRoll_UsesCeilingGap()
    {
        var (party, hero, _, _) = FreshParty();
        // gap 5 → threshold 100: fails only for rand/327 == 100 (rand ≥ 32700).
        hero.MpCeiling = hero.MagicMax + 5;
        hero.CastSinceRest = true;
        var mpMax = hero.MagicMax;
        var result = TownMenus.Rest(party, new FixedDosRandom(0));
        Assert.Equal(mpMax + 1, hero.MagicMax);
        Assert.Equal(hero.MagicMax, hero.MagicCur);
        Assert.False(hero.CastSinceRest);
        Assert.Contains("MAGIC INCREASES", result.Message);

        // gap 1 → threshold 20: rand/327 = 20 fails (strict <).
        hero.MpCeiling = hero.MagicMax + 1;
        hero.CastSinceRest = true;
        TownMenus.Rest(party, new FixedDosRandom(20 * 327));
        Assert.Equal(mpMax + 1, hero.MagicMax);

        // No cast since rest → no roll, even with a gap.
        TownMenus.Rest(party, new FixedDosRandom(0));
        Assert.Equal(mpMax + 1, hero.MagicMax);

        // At the ceiling the gap is 0 → never raises.
        hero.MpCeiling = hero.MagicMax;
        hero.CastSinceRest = true;
        TownMenus.Rest(party, new FixedDosRandom(0));
        Assert.Equal(mpMax + 1, hero.MagicMax);
    }

    private sealed class FixedDosRandom(int word) : IDosRandomSource
    {
        public int NextDosWord() => word;
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
    }

    [OriginalDataFact]
    public void Distribute_SplitsLootEvenly()
    {
        var (party, a, _, _) = FreshParty();
        var b = CharacterCreation.Create(
            new CharacterCreationRequest { Name = "Second", Race = Race.Elf, Class = CharacterClass.Wizard },
            Data.CreationTables,
            new SeededDosRandomSource(7));
        party.Members.Add(b);

        var sack = new LootSack();
        sack.DepositRewards(xp: 100, gold: 50, items: [46, 21]);

        var result = TownMenus.Distribute(party, sack);
        Assert.Equal(TownTransactionKind.Ok, result.Kind);
        Assert.True(sack.IsEmpty);
        Assert.Equal(50, a.Experience);
        Assert.Equal(50, b.Experience);
        Assert.Equal(50, party.GoldCarried);
        Assert.Contains(46, a.InventoryItemIds);
        Assert.Contains(21, b.InventoryItemIds);
    }

    [OriginalDataFact]
    public void Distribute_SkipsFullMembersAndKeepsTheRestInTheSack()
    {
        var (party, a, _, _) = FreshParty();
        var b = CharacterCreation.Create(
            new CharacterCreationRequest { Name = "Second", Race = Race.Elf, Class = CharacterClass.Wizard },
            Data.CreationTables,
            new SeededDosRandomSource(7));
        party.Members.Add(b);
        while (a.InventoryItemIds.Count < Character.InventorySlots) a.InventoryItemIds.Add(121);
        while (b.InventoryItemIds.Count < Character.InventorySlots - 1) b.InventoryItemIds.Add(121);

        var sack = new LootSack();
        sack.DepositRewards(xp: 10, gold: 10, items: [46, 21]);
        var result = TownMenus.Distribute(party, sack);

        Assert.Equal(TownTransactionKind.Ok, result.Kind);
        Assert.Equal(Character.InventorySlots, a.InventoryItemIds.Count);
        Assert.Equal(46, b.InventoryItemIds[^1]);
        Assert.Equal(new[] { 21 }, sack.ItemIds);
        Assert.Equal(0, sack.Gold);
        Assert.Equal(0, sack.Experience);
    }

    [OriginalDataFact]
    public void Distribute_GoldRemainderGoesToFirstSurvivorPurse()
    {
        var (party, a, _, _) = FreshParty();
        var b = CharacterCreation.Create(
            new CharacterCreationRequest { Name = "Second", Race = Race.Elf, Class = CharacterClass.Wizard },
            Data.CreationTables,
            new SeededDosRandomSource(7));
        party.Members.Add(b);

        var sack = new LootSack();
        sack.DepositRewards(7, 5); // xp 3+4, gold 2+3
        TownMenus.Distribute(party, sack);

        Assert.Equal(4, a.Experience); // 3 + rem 1
        Assert.Equal(3, b.Experience);
        Assert.Equal(5, party.GoldCarried); // all gold to party purse (2*2 + 1 rem)
    }

    [OriginalDataFact]
    public void Mystic_WritesScoreToAllMembers()
    {
        var (party, a, _, _) = FreshParty();
        var b = CharacterCreation.Create(
            new CharacterCreationRequest { Name = "Second", Race = Race.Elf, Class = CharacterClass.Wizard },
            Data.CreationTables,
            new SeededDosRandomSource(7));
        party.Members.Add(b);
        a.Level = 1;
        b.Level = 1;

        var result = TownMenus.ConsultMystic(party);
        Assert.Equal(TownTransactionKind.Ok, result.Kind);
        // base = 1 + 1 = 2; score = (2+1)/3 = 1.
        Assert.Equal(1, a.Score);
        Assert.Equal(1, b.Score);
        Assert.Contains("novice", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [OriginalDataFact]
    public void Mystic_SixFreshLevelOneMembers_MatchesLiveCapture()
    {
        // Reproduces the live DOSBox-X capture (session 3, "pass 4"): six
        // fresh level-1 characters, no rings/wand, consulted Mystic and got
        // "Your score is 2." — save-file-format.md @70 and
        // docs/plan/HANDOFF-2026-07-18.md.
        var (party, _, _, _) = FreshParty();
        for (var i = 0; i < 5; i++)
        {
            var extra = CharacterCreation.Create(
                new CharacterCreationRequest { Name = $"Extra{i}", Race = Race.Human, Class = CharacterClass.Fighter },
                Data.CreationTables,
                new SeededDosRandomSource(100 + i));
            party.Members.Add(extra);
        }

        var result = TownMenus.ConsultMystic(party);
        Assert.Equal(TownTransactionKind.Ok, result.Kind);
        Assert.All(party.Members, m => Assert.Equal(2, m.Score));
        Assert.Contains("novice", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [OriginalDataTheory]
    [InlineData(26, false, "You are novice adventurers.")]                                // score 9
    [InlineData(110, false, "You are not yet ready to visit The Gods.")]
    [InlineData(200, false, "You are experienced enough to visit The Gods.")]
    [InlineData(200, true, "You are experienced enough to visit The Gods and face the tests.")]
    [InlineData(260, true, "You are Heroes of Gelnor.")]
    public void Mystic_PrintsTheDosTierLines_s49(int level, bool runes, string expected)
    {
        // OV1 0x3327 + table 0x5ecd, seen live in s49 (capture_s49_2026-10-23.md §2).
        var (party, _, _, _) = FreshParty();
        party.Members[0].Level = level;
        if (runes)
            for (var r = 0; r < 4; r++)
                party.Members[0].Phantasie1.Runes[r] = 1;

        var result = TownMenus.ConsultMystic(party);
        Assert.EndsWith(expected, result.Message);
        Assert.DoesNotContain("greater challenges", result.Message);
    }

    [OriginalDataFact]
    public void Mystic_SkipsDeadAndSleepingMembersAndTheirItems()
    {
        // OV1 0x2100: only members with status word (+0x18) == 1 contribute their
        // level and carried items (PLAN H3, s10). UNDEAD has status OK in DOS → counts.
        var (party, hero, _, _) = FreshParty();
        hero.Level = 5;
        Character Extra(string name, CharacterStatus status, int level, int? item = null)
        {
            var c = CharacterCreation.Create(
                new CharacterCreationRequest { Name = name, Race = Race.Human, Class = CharacterClass.Fighter },
                Data.CreationTables, new SeededDosRandomSource(name.Length));
            c.Status = status;
            c.Level = level;
            if (item is { } id) c.InventoryItemIds.Add(id);
            party.Members.Add(c);
            return c;
        }
        Extra("Corpse", CharacterStatus.Dead, 9, 150);  // wand on a dead member: ignored
        Extra("Dozer", CharacterStatus.Sleep, 9, 141);  // ring on a sleeper: ignored
        Extra("Ghoul", CharacterStatus.Ok, 3).Race = Race.Undead;

        TownMenus.ConsultMystic(party);

        // base = 5 + 3 = 8; score = (8+1)/3 = 3.
        Assert.Equal(3, hero.Score);
    }

    [OriginalDataFact]
    public void Mystic_WandGrantsDoubleCapabilityBonus()
    {
        var (party, hero, _, _) = FreshParty();
        hero.InventoryItemIds.Add(150); // WAND (items.json index 150)

        TownMenus.ConsultMystic(party);

        // base = level(1) + 18 (one of the ten distinct-capability flags) + 66
        // (WAND-specific bonus) = 85; score = (85+1)/3 = 28.
        Assert.Equal(28, hero.Score);
    }

    [OriginalDataFact]
    public void Mystic_RingGrantsSingleCapabilityBonus()
    {
        var (party, hero, _, _) = FreshParty();
        hero.InventoryItemIds.Add(141); // RING A (items.json index 141)

        TownMenus.ConsultMystic(party);

        // base = level(1) + 18 = 19; score = (19+1)/3 = 6.
        Assert.Equal(6, hero.Score);
    }

    [OriginalDataFact]
    public void Mystic_DuplicateRingsCountOnceAsDistinctCapability()
    {
        var (party, hero, _, _) = FreshParty();
        hero.InventoryItemIds.Add(141); // RING A
        hero.InventoryItemIds.Add(141); // same ring again — not a second flag

        TownMenus.ConsultMystic(party);

        Assert.Equal(6, hero.Score); // same as a single RING A
    }

    [OriginalDataFact]
    public void Mystic_ScoreNeverDecreases()
    {
        var (party, hero, _, _) = FreshParty();
        hero.Score = 50;

        TownMenus.ConsultMystic(party); // computed score (1 level-1 member, no items) is far lower

        Assert.Equal(50, hero.Score);
    }

    [OriginalDataFact]
    public void Guild_AddDropPurgeRename()
    {
        var data = Data;
        var roster = new GuildRoster();
        var party = new Party();
        var hero = CharacterCreation.Create(
            new CharacterCreationRequest { Name = "Hero", Race = Race.Human, Class = CharacterClass.Thief },
            data.CreationTables,
            new SeededDosRandomSource(1));

        Assert.True(GuildHall.TryEnroll(roster, hero, out var slot, out _));
        Assert.Equal(0, slot);
        Assert.True(GuildHall.TryAddToParty(roster, party, hero, 1, out _));
        Assert.Single(party.Members);

        Assert.True(GuildHall.TryRename(hero, "Renamed", roster, out _));
        Assert.Equal("Renamed", hero.Name);

        Assert.True(GuildHall.TryDropFromParty(party, hero, out _));
        Assert.Empty(party.Members);
        Assert.True(GuildHall.TryPurge(roster, party, hero, out _));
        Assert.Empty(roster.Occupied);
    }

    // Manual: "A maximum of 21 characters can be generated" — DIFFERENCES.md D-002.
    [OriginalDataFact]
    public void Guild_EnrollStopsAtManualLimitOf21()
    {
        var data = Data;
        var roster = new GuildRoster();
        Character Make(int i) => CharacterCreation.Create(
            new CharacterCreationRequest { Name = $"c{i}", Race = Race.Human, Class = CharacterClass.Thief },
            data.CreationTables,
            new SeededDosRandomSource(i));

        for (var i = 0; i < 21; i++)
            Assert.True(GuildHall.TryEnroll(roster, Make(i), out _, out _));

        Assert.False(GuildHall.TryEnroll(roster, Make(21), out var slot, out var error));
        Assert.Equal(-1, slot);
        Assert.Equal("Guild roster is full.", error);
        Assert.Equal(21, roster.Occupied.Count());
        Assert.Equal(22, roster.Slots.Length); // DOS record count kept for save compatibility
    }

    [OriginalDataFact]
    public void Train_RequiresPartyMembershipAndXp()
    {
        var (party, hero, pelnor, _) = FreshParty();
        // Pin stats so threshold is deterministic regardless of rolled values.
        hero.Stats = new CharacterStats(Str: 10, Int: 10, Dex: 10, Con: 10, Cha: 20, Luck: 10);
        Assert.False(TownMenus.CanTrain(hero, out _));

        // CHA=20 → chaFactor=0 → gold cost=0, so StartingGold covers it.
        hero.Experience = 1000;
        TownMenus.Withdraw(party, hero, hero.GoldInBank);
        var result = TownMenus.Train(party, hero, pelnor, Data.CreationTables, new SeededDosRandomSource(1));
        Assert.Equal(TownTransactionKind.Ok, result.Kind);
        Assert.Equal(2, hero.Level);
    }

    [OriginalDataFact]
    public void LevelUp_Fighter1To2_AppliesLevel1Rows()
    {
        var tables = Data.CreationTables;
        var (_, hero, _, _) = FreshParty();
        var before = hero.Skills.ToRecordSlots();
        var hpBefore = hero.HpMax;
        var hpCurBefore = hero.HpCur;
        var mpMaxBefore = hero.MagicMax;

        LevelUp.Apply(hero, 1, tables, new SeededDosRandomSource(7));

        var f = (int)CharacterClass.Fighter;
        Assert.Equal(2, hero.Level);
        Assert.Equal(tables.MpCeilingByClass[f][1], hero.MpCeiling);
        Assert.Equal(mpMaxBefore, hero.MagicMax);  // only the Inn raises MP max
        Assert.Equal(hpCurBefore, hero.HpCur);     // HP current unchanged
        Assert.True(hero.HpMax > hpBefore);        // re-roll or +1
        var coeffs = tables.SkillCoefficientsByClassLevel[f][1];
        var after = hero.Skills.ToRecordSlots();
        for (var i = 0; i < 10; i++)
        {
            var t = ((coeffs[i] + before[i]) >> 1) + hero.SkillBonuses[i] - 49;
            Assert.Equal(Math.Max(before[i], Math.Min(t, 255)), after[i]);
        }
    }

    [OriginalDataFact]
    public void LevelUp_LowRollStillGivesPlusOne_AndFlagsNewSpells()
    {
        var tables = Data.CreationTables;
        var (_, hero, _, _) = FreshParty();
        hero.HpMax = 254;
        LevelUp.Apply(hero, 1, tables, new SeededDosRandomSource(3));
        Assert.Equal(255, hero.HpMax);   // a level-1 roll can't reach 254 → +1
        LevelUp.Apply(hero, 2, tables, new SeededDosRandomSource(3));
        Assert.Equal(255, hero.HpMax);   // capped: no roll at 255

        // Fighter row: spell 51 (manual #52) becomes learnable on reaching level 3.
        Assert.Equal(3, tables.SpellLevelsByClass[(int)CharacterClass.Fighter][51]);
        Assert.Equal(2, hero.SpellsKnown[51]);
    }

    [Fact]
    public void LevelUp_HitDice_UseManualDivisor_D007()
    {
        // At CON 10 the remaster rolls exactly hp_factor d3 (DOS >>4: 15 and 79 dice here).
        Assert.Equal(16, LevelUp.HitDiceCount(16, 10));
        Assert.Equal(85, LevelUp.HitDiceCount(85, 10));
    }

    [OriginalDataFact]
    public void LevelUp_AverageHpTracksManualChart_D007()
    {
        // Manual chart (CON 10) = 2 × hp_factor[class][L]. Check the realised L15 average after
        // the full re-roll/+1 sequence lands within 2 % of 2 × factor[14] for every class.
        var tables = Data.CreationTables;
        var rng = new SeededDosRandomSource(2026);
        foreach (CharacterClass cls in Enum.GetValues<CharacterClass>())
        {
            const int runs = 2000;
            long total = 0;
            for (var run = 0; run < runs; run++)
            {
                var ch = new Character
                {
                    Class = cls,
                    Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
                    SpellsKnown = new int[CharacterCreation.SpellCount],
                };
                for (var l = 0; l < LevelUp.MaxLevel; l++)
                    LevelUp.Apply(ch, l, tables, rng);
                total += ch.HpMax;
            }
            var chart = 2.0 * tables.HpFactorsByClass[(int)cls][14];
            Assert.InRange(total / (double)runs, chart * 0.98, chart * 1.03);
        }
    }

    [Theory]
    // s5 DOSBox capture (level 1, XP 40) — working/training_capture_2026-07-18.md
    [InlineData(CharacterClass.Wizard,  3, 17,  5, 14,  6,  9,  40,  957)]
    [InlineData(CharacterClass.Priest,  3, 13, 11, 17, 10, 12,  40, 1091)]
    [InlineData(CharacterClass.Thief,  11,  3, 17,  8,  5, 13,  40,  399)]
    [InlineData(CharacterClass.Fighter,15, 14, 17, 11, 17, 10,  40,  475)]
    [InlineData(CharacterClass.Fighter,10, 13,  3, 12, 14, 10,  40, 1229)]
    [InlineData(CharacterClass.Fighter,10,  5, 18,  8,  3, 10,  40,  549)]
    // s35 DOSBox capture (level 1, XP 0 → 0.2 × target; XP 141/181) — capture_s35_2026-10-11.md §1
    [InlineData(CharacterClass.Monk,   17,  7, 11,  8,  3, 10,   0,  282)]
    [InlineData(CharacterClass.Ranger, 18, 10, 10, 15,  5,  8,   0,  319)]
    [InlineData(CharacterClass.Monk,   17,  7, 11,  8,  3, 10, 141, 1273)]
    [InlineData(CharacterClass.Ranger, 18, 10, 10, 15,  5,  8, 141, 1458)]
    [InlineData(CharacterClass.Priest,  3, 13, 11, 17, 10, 12, 181,  950)]
    [InlineData(CharacterClass.Fighter,15, 14, 17, 11, 17, 10, 181,  334)]
    [InlineData(CharacterClass.Fighter,10, 13,  3, 12, 14, 10, 181, 1088)]
    public void TrainingXpShortfall_MatchesAllLiveCaptures(
        CharacterClass cls, int str, int intel, int dex, int con, int cha, int luck, long xp, long expectedNeed)
    {
        // [C] OV1 0x11ea..0x139e: X = (F − 0.8)·1000 / ((float)(sum/mult) + 1e−5), F = 1.8^level (1.0 when XP = 0);
        // "You need N more" = −trunc((float)(XP − X)).
        var stats = new CharacterStats(str, intel, dex, con, cha, luck);
        Assert.Equal(expectedNeed, TownMenus.TrainingXpShortfall((int)cls, stats, level: 1, xp));
    }

    [Fact]
    public void TrainingXpRequired_ThiefAndRanger_UseStoredChain()
    {
        // The two captures a plain mult·1000/sum model misses by one (440 / 1600).
        Assert.Equal(439, TownMenus.TrainingXpRequired_ForClass(
            (int)CharacterClass.Thief, new CharacterStats(Str: 11, Int: 3, Dex: 17, Con: 8, Cha: 5, Luck: 13), level: 1));
        Assert.Equal(1599, TownMenus.TrainingXpRequired_ForClass(
            (int)CharacterClass.Ranger, new CharacterStats(Str: 18, Int: 10, Dex: 10, Con: 15, Cha: 5, Luck: 8), level: 1));
        // XP 0 asks 0.2 × that (F reset to 1.0).
        Assert.Equal(87, TownMenus.TrainingXpRequired_ForClass(
            (int)CharacterClass.Thief, new CharacterStats(Str: 11, Int: 3, Dex: 17, Con: 8, Cha: 5, Luck: 13), level: 1, experience: 0));
    }

    [Fact]
    public void TrainingMultipliers_AreTheStoredDosDoubles()
    {
        ulong[] bits =
        [
            0x4033CCCCCCCCCCC0, 0x4048C00000000000, 0x404A666666666660,
            0x4030800000000000, 0x4032199999999980, 0x4045733333333320,
        ];
        for (var i = 0; i < bits.Length; i++)
            Assert.Equal(bits[i], BitConverter.DoubleToUInt64Bits(TownMenus.TrainingMultipliers[i]));
    }

    [Fact]
    public void TrainingXpRequired_HigherLevels_StaticDecodeRegression()
    {
        // [C static, s39] F = 1.8^level → level L target = (1.8^L − 0.8)/1.0 × the level-1 target. No live capture above
        // level 1 yet ([D+]); these pin the decoded chain (Thief sum 45: 439 → 1073 → 2214 → 4266).
        var thief = new CharacterStats(Str: 11, Int: 3, Dex: 17, Con: 8, Cha: 5, Luck: 13);
        Assert.Equal(1073, TownMenus.TrainingXpRequired_ForClass(0, thief, level: 2));
        Assert.Equal(2214, TownMenus.TrainingXpRequired_ForClass(0, thief, level: 3));
        Assert.Equal(4266, TownMenus.TrainingXpRequired_ForClass(0, thief, level: 4));
        // XP 0 ignores the level: still 0.2 × the level-1 target.
        Assert.Equal(87, TownMenus.TrainingXpRequired_ForClass(0, thief, level: 5, experience: 0));
    }

    [Theory]
    // s41 DOSBox capture (Pelnor, levels 2–4, XP byte-edited) — capture_s41_2026-10-15.md §1: "You need N more"
    [InlineData(CharacterClass.Priest,  3, 13, 11, 17, 10, 12, 3, 3000, 2692)]
    [InlineData(CharacterClass.Fighter,15, 14, 17, 11, 17, 10, 4, 3000, 2000)]
    [InlineData(CharacterClass.Fighter,10, 13,  3, 12, 14, 10, 2, 3000,   96)]
    [InlineData(CharacterClass.Ranger, 18, 10, 10, 15,  5,  8, 2, 3000,  903)]
    // …and the ones that could train (shortfall 0) at the level they trained from
    [InlineData(CharacterClass.Priest,  3, 13, 11, 17, 10, 12, 2, 3000,    0)]
    [InlineData(CharacterClass.Fighter,15, 14, 17, 11, 17, 10, 3, 3000,    0)]
    [InlineData(CharacterClass.Monk,   17,  7, 11,  8,  3, 10, 2, 4000,    0)]
    public void TrainingXpShortfall_HigherLevels_MatchLiveCaptures(
        CharacterClass cls, int str, int intel, int dex, int con, int cha, int luck, int level, long xp, long expectedNeed)
    {
        var stats = new CharacterStats(str, intel, dex, con, cha, luck);
        Assert.Equal(expectedNeed, TownMenus.TrainingXpShortfall((int)cls, stats, level, xp));
    }

    [OriginalDataTheory]
    // s41 DOSBox capture, Pelnor (price 150): "It will cost N gold" — capture_s41_2026-10-15.md §1
    [InlineData(17, 1,   13)] // fighto
    [InlineData(17, 2,   54)]
    [InlineData(17, 3,  148)]
    [InlineData(10, 1,  150)] // priesto
    [InlineData(10, 2,  600)]
    [InlineData(14, 1,   54)] // crusho
    [InlineData( 5, 1,  337)] // rango
    [InlineData( 3, 1,  433)] // monko
    [InlineData( 3, 2, 1734)]
    public void TrainingGoldCost_MatchesLiveCaptures(int cha, int level, long expected)
    {
        var (_, hero, pelnor, _) = FreshParty();
        Assert.Equal(150, pelnor.PriceIndex);
        hero.Stats = new CharacterStats(Str: 10, Int: 10, Dex: 10, Con: 10, Cha: cha, Luck: 10);
        hero.Level = level;
        hero.Experience = 3000;
        Assert.Equal(expected, TownMenus.TrainingGoldCost(hero, pelnor));
    }

    [Fact]
    public void TrainingGoldBase_IsLevelToThePower2Point2()
    {
        long[] expected = [1, 4, 11, 21, 34, 51, 72, 97, 125, 158, 195, 236, 282, 332];
        for (var level = 1; level <= 14; level++)
            Assert.Equal(expected[level - 1], TownMenus.TrainingGoldBase(level));
    }

    [OriginalDataFact]
    public void TrainingGoldCost_IgnoresXp_AndCapsAt15000()
    {
        // [C] OV1 0x13a1..0x1451: min(15000, (long)pow(level, 2.2) · (20−CHA)² · price / 100). Pelnor price 150.
        var (_, hero, pelnor, _) = FreshParty();
        hero.Stats = new CharacterStats(Str: 10, Int: 0, Dex: 12, Con: 0, Cha: 5, Luck: 0);
        hero.Level = 1;
        hero.Experience = 5000;
        Assert.Equal(225 * 150 / 100, TownMenus.TrainingGoldCost(hero, pelnor)); // 337
        hero.Level = 6;
        // 51 · 225 · 150 / 100 = 17212 → capped
        Assert.Equal(15000, TownMenus.TrainingGoldCost(hero, pelnor));
    }

    [OriginalDataFact]
    public void Train_RefusesOnlyDead_SleepingMemberTrains_Ov1_119b()
    {
        // [C] OV1 0x119b: `cmp [bx+0x18], 0` — only status 0 (DEAD) is refused; SLEEP (2) trains. Manual silent.
        var (party, hero, pelnor, _) = FreshParty();
        hero.Stats = new CharacterStats(Str: 10, Int: 10, Dex: 10, Con: 10, Cha: 20, Luck: 10); // CHA 20 → cost 0
        hero.Experience = 1000;
        hero.Status = CharacterStatus.Sleep;
        Assert.True(TownMenus.CanTrain(hero, out _));
        var r = TownMenus.Train(party, hero, pelnor, Data.CreationTables, new SeededDosRandomSource(1));
        Assert.Equal(TownTransactionKind.Ok, r.Kind);
        Assert.Equal(2, hero.Level);

        hero.Status = CharacterStatus.Dead;
        Assert.False(TownMenus.CanTrain(hero, out var reason));
        Assert.Equal("No one is wise enough to teach You!", reason);
    }

    [OriginalDataFact]
    public void Train_XpShort_StillTellsTheCost_D050()
    {
        // Manual (D-050): "you will be told how many experience points are required and how much the training will cost".
        var (party, hero, pelnor, _) = FreshParty();
        hero.Stats = new CharacterStats(Str: 10, Int: 0, Dex: 12, Con: 0, Cha: 5, Luck: 0);
        hero.Experience = 1;
        var need = TownMenus.TrainingXpShortfall(hero);
        Assert.True(need > 0);
        var r = TownMenus.Train(party, hero, pelnor, Data.CreationTables, new SeededDosRandomSource(1));
        Assert.Equal(TownTransactionKind.Failed, r.Kind);
        Assert.Equal($"You need {need} more experience points. It will cost 337 gold.", r.Message);
    }

    [OriginalDataFact]
    public void GoldCap_65000_ExcessLost_D048()
    {
        var (party, hero, pelnor, catalog) = FreshParty();
        // Withdraw: the full amount leaves the account, the purse stops at 65,000 (DOS order, manual cap).
        hero.GoldInBank = 10_000;
        party.GoldCarried = 60_000;
        var w = TownMenus.Withdraw(party, hero, 10_000);
        Assert.Equal(TownTransactionKind.Ok, w.Kind);
        Assert.Equal(0, hero.GoldInBank);
        Assert.Equal(RulesProfile.PhantasieI.PartyGoldCap, party.GoldCarried);
        Assert.Equal(5_000, w.GoldDelta);

        // Share split: each account stops at 65,000.
        hero.GoldInBank = 64_000;
        party.GoldCarried = 3_000;
        TownMenus.DepositShares(party, [1]);
        Assert.Equal(RulesProfile.PhantasieI.BankGoldCap, hero.GoldInBank);
        Assert.Equal(0, party.GoldCarried);

        // Town-entry payout and found gold stop at the cap too.
        party.GoldCarried = 64_990;
        var sack = new LootSack();
        sack.DepositRewards(0, 100);
        TownMenus.Distribute(party, sack);
        Assert.Equal(RulesProfile.PhantasieI.PartyGoldCap, party.GoldCarried);
        sack.DepositRewards(0, 70_000);
        Assert.Equal(RulesProfile.PhantasieI.PartyGoldCap, sack.Gold);

        // A value already over the cap (DOS import) is not cut by an add.
        party.GoldCarried = 65_535;
        Assert.Equal(10, party.AddGold(10));
        Assert.Equal(65_535, party.GoldCarried);
    }

    [OriginalDataFact]
    public void DepositShares_SplitsCarriedGoldIntoBanks_DropsRemainder()
    {
        // [C] OV1 0x2919 Deposit → 0x167d: share = floor(gold / Σshares), bank += shares·share, purse zeroed.
        var (party, hero, _, _) = FreshParty();
        var second = new Character { Name = "Two", Status = CharacterStatus.Ok, GoldInBank = 10 };
        var dead = new Character { Name = "Dead", Status = CharacterStatus.Dead, GoldInBank = 0 };
        party.Members.Add(second);
        party.Members.Add(dead);
        var heroBank = hero.GoldInBank;
        party.GoldCarried = 100;

        Assert.Equal(TownTransactionKind.Failed, TownMenus.DepositShares(party, [0, 1, 0]).Kind); // living need ≥ 1
        Assert.Equal(TownTransactionKind.Failed, TownMenus.DepositShares(party, [1, 4, 0]).Kind);

        var r = TownMenus.DepositShares(party, [1, 2, 0]); // Σ = 3 → share 33, remainder 1 lost
        Assert.Equal(TownTransactionKind.Ok, r.Kind);
        Assert.Equal(heroBank + 33, hero.GoldInBank);
        Assert.Equal(10 + 66, second.GoldInBank);
        Assert.Equal(0, dead.GoldInBank);
        Assert.Equal(0, party.GoldCarried);
    }

    [OriginalDataFact]
    public void TrainingGoldCost_ZeroWithMaxCha()
    {
        // CHA >= 20 → chaFactor = 0 → cost = 0
        var (_, hero, pelnor, _) = FreshParty();
        hero.Stats = new CharacterStats(Str: 10, Int: 0, Dex: 12, Con: 0, Cha: 20, Luck: 0);
        Assert.Equal(0, TownMenus.TrainingGoldCost(hero, pelnor));
    }

    [OriginalDataFact]
    public void LearnSpell_UsesTownLevelAttributesAndClassNotMpCost()
    {
        var (party, hero, pelnor, _) = FreshParty();
        hero.Class = CharacterClass.Wizard;
        hero.Level = 2;
        hero.Stats = new CharacterStats(Str: 10, Int: 15, Dex: 10, Con: 10, Cha: 20, Luck: 10);
        hero.SpellsKnown[0] = 2;
        hero.SpellsKnown[3] = 2;
        party.GoldCarried = 100;

        // OV1 0x154d..0x165c: 150 * 2 * 90 / (20 * 15 * (9 - 5)) = 22.
        Assert.Equal(22, TownMenus.SpellLearningGoldCost(hero, pelnor));
        var age = hero.AgeClock;
        Assert.Equal(TownTransactionKind.Ok, TownMenus.LearnSpell(party, hero, pelnor, 0).Kind);
        Assert.Equal(TownTransactionKind.Ok, TownMenus.LearnSpell(party, hero, pelnor, 3).Kind);
        Assert.Equal(56, party.GoldCarried);
        Assert.Equal(age + 4, hero.AgeClock); // +2 (four weeks) per spell, OV1 0x165f
    }

    [OriginalDataTheory]
    // s63 DOSBox (Pelnor): wizard L2 INT 18 with 1 known → 3; priest L1 INT 13 with 1 known → 1; fighter L1 INT 14 → 5
    // (the +3 fighter term); thief INT 3 → 2. cap = trunc(level·INT·1.3/90·class + 3·fighter + 1), count = cap − known + 1.
    [InlineData(CharacterClass.Wizard, 2, 18, 1, 3)]
    [InlineData(CharacterClass.Priest, 1, 13, 1, 1)]
    [InlineData(CharacterClass.Fighter, 1, 14, 0, 5)]
    [InlineData(CharacterClass.Thief, 1, 3, 0, 2)]
    [InlineData(CharacterClass.Wizard, 2, 18, 4, 0)]
    public void SpellLearnCount_MatchesTheDosCap(CharacterClass cls, int level, int intel, int known, int expected)
    {
        var (_, hero, _, _) = FreshParty();
        hero.Class = cls;
        hero.Level = level;
        hero.Stats = hero.Stats with { Int = intel };
        Array.Fill(hero.SpellsKnown, 0);
        for (var i = 0; i < known; i++)
            hero.SpellsKnown[i] = 1;
        for (var i = 10; i < 20; i++)
            hero.SpellsKnown[i] = 2; // more marked than the cap allows: the count is the cap's
        Assert.Equal(expected, TownMenus.SpellLearnCount(hero));
        hero.Status = CharacterStatus.Dead;
        Assert.Equal(-1, TownMenus.SpellLearnCount(hero)); // OV1 0x14c1
    }

    [OriginalDataFact]
    public void SpellLearnCount_IsCappedByTheMarkedSpells()
    {
        var (_, hero, _, _) = FreshParty();
        Array.Fill(hero.SpellsKnown, 0);
        hero.SpellsKnown[5] = 2;
        Assert.Equal(1, TownMenus.SpellLearnCount(hero)); // fighter L1 cap allows 5, only one is marked
    }

    [OriginalDataFact]
    public void LearnSpell_FailedRoll_TakesFeeAndAge_ButNotTheSpell()
    {
        var (party, hero, pelnor, _) = FreshParty();
        hero.Stats = hero.Stats with { Int = 13 };
        Array.Fill(hero.SpellsKnown, 0);
        hero.SpellsKnown[2] = 2;
        party.GoldCarried = 1000;
        Assert.False(TownMenus.SpellLearnRoll(hero, new FixedDosRandom(13 * 1638))); // 13 < 13 fails
        Assert.True(TownMenus.SpellLearnRoll(hero, new FixedDosRandom(13 * 1638 - 1)));
        var fee = TownMenus.SpellLearningGoldCost(hero, pelnor);
        var age = hero.AgeClock;
        var r = TownMenus.LearnSpell(party, hero, pelnor, 2, learned: false);
        Assert.Equal("You've failed", r.Message);
        Assert.Equal(2, hero.SpellsKnown[2]);
        Assert.Equal(1000 - fee, party.GoldCarried);
        Assert.Equal(age + 2, hero.AgeClock);
        Assert.Equal("You've learned", TownMenus.LearnSpell(party, hero, pelnor, 2).Message);
        Assert.Equal(1, hero.SpellsKnown[2]);
        party.GoldCarried = 0;
        hero.SpellsKnown[3] = 2;
        Assert.Equal("You can't afford it!", TownMenus.LearnSpell(party, hero, pelnor, 3).Message);
        Assert.Equal(age + 4, hero.AgeClock); // nothing taken
    }

    [Fact]
    public void Club_HasCombatPoints4()
    {
        // Manual: Club → points 4 (id 48)
        Assert.Equal(4, ItemRules.CombatGate(48).Points);
    }

    [OriginalDataFact]
    public void UseItem_DescribesStatsAndText_D061()
    {
        var catalog = Data.CreateItemCatalog();
        var shield = ItemRules.Describe(catalog.Get(5)!);
        Assert.Equal("SM SHLD+1 - Small shield +1", shield[0]);
        Assert.Equal("SHIELD: PROTECTION 5 POINTS", shield[1]);
        Assert.Equal("NEEDS STRENGTH 5", shield[2]);
        Assert.Contains("enchanted (+1)", shield[^1]);

        var sword = ItemRules.Describe(catalog.Get(100)!);
        Assert.Equal("WEAPON: DAMAGE 25 POINTS", sword[1]);
        Assert.Equal("NEEDS STR x2 + DEX OF 60", sword[2]); // manual p.18: God sword, 25 points, 60

        Assert.Equal("HEALING POTION: RESTORES 9 HIT POINTS", ItemRules.Describe(catalog.Get(103)!)[1]);
        var ring = ItemRules.Describe(catalog.Get(141)!);
        Assert.Contains("Nine Magical Rings", ring[^1]);
        Assert.Equal($"THE ARMORY PAYS {ItemRules.SellPrice(188)} GP", ring[2]);
        Assert.All(Enumerable.Range(1, 180), id => Assert.NotNull(catalog.Get(id)!.Description));
    }
}
