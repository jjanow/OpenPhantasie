using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// D3-071 (manual, user s142): III shares the experience pool out "when they reach a town" (Amiga <c>0x7964</c> →
/// <c>0xbc7c(1)</c>, DOS root <c>0xa911</c> → ovl2 <c>0x0000</c>(1)): the found items' hand-out screen first, then
/// "Shares for NAME :" per member (DOS order, s143).
/// </summary>
public class P3TownEntryPayoutTests
{
    [OriginalDataFact]
    public void EnterTown_HandsItemsOutFirst_ThenAsksTheShares()
    {
        var s = WithParty();
        var prompts = new SessionPrompts(s);
        var town = s.CurrentTownId!.Value;
        Assert.True(s.LeaveTown());
        s.Sack.Experience = 400;
        s.Sack.ItemIds.Add(20);

        s.EnterTown(town);

        Assert.Equal(400, s.Sack.Experience); // DOS ovl2 0x0000(1): the items' screen comes first (s143)
        Assert.True(s.TownItemHandOutPending);
        Assert.False(s.TownXpSharesDue);
        Assert.Null(prompts.Current);
        Assert.True(s.TakeTownItemHandOut());
        Assert.False(s.TakeTownItemHandOut()); // once
        Assert.Equal(1, s.Sack.ItemCount); // the screen hands them out
        var carried = s.Party.Members.Select(m => m.InventoryItemIds.ToList()).ToList();
        Assert.Equal([20], s.BeginItemDistribution(foundOnly: true)!.Remaining); // found items only; carried ones stay
        Assert.Equal(carried, s.Party.Members.Select(m => m.InventoryItemIds.ToList()).ToList());
        Assert.False(s.TownXpSharesDue); // still on the item screen
        s.GiveDistributedItem(DistributeTarget.Member(0));
        Assert.Null(s.ItemDistribution);
        Assert.True(s.TownXpSharesDue);
        Assert.Equal(SessionPromptKind.ExperienceShares, prompts.Current!.Kind);
    }

    [OriginalDataFact]
    public void SharesPrompts_SplitThePoolByShares_RemainderDrawnPointByPoint()
    {
        var s = WithParty();
        var prompts = new SessionPrompts(s);
        var town = s.CurrentTownId!.Value;
        Assert.True(s.LeaveTown());
        var members = s.Party.Members.ToList();
        var before = members.Select(m => m.Experience).ToList();
        s.Sack.Experience = 1000;

        s.EnterTown(town);

        var shares = members.Select((_, i) => i == 0 ? 3 : 1).ToList(); // total 6: 1000 = 6 · 166 + 4
        foreach (var (n, i) in shares.Select((n, i) => (n, i)))
        {
            var p = prompts.Current!;
            Assert.Equal(SessionPromptKind.ExperienceShares, p.Kind);
            Assert.Contains(members[i].Name, p.Lines[^1]);
            Assert.Equal(["1", "2", "3"], p.Options);
            Assert.False(p.Remembers);
            Assert.Equal(1, p.InitialChoice); // creation's stored shares, 1
            prompts.Answer(p.Options.ToList().IndexOf(n.ToString(System.Globalization.CultureInfo.InvariantCulture)) + 1);
        }
        var gained = members.Select((m, i) => m.Experience - before[i]).ToList();
        Assert.Equal(1000, gained.Sum()); // DOS ovl2 0x049b: the remainder goes out one point at a time, nothing lost
        for (var i = 0; i < members.Count; i++)
            Assert.InRange(gained[i], shares[i] * 166, shares[i] * 166 + 4);
        Assert.Equal(shares, members.Select(m => m.P3.ExperienceShares)); // +0x54 keeps the answer
        Assert.Equal(0, s.Sack.Experience);
        Assert.False(s.TownXpSharesPending);
        Assert.Null(prompts.Current);
    }

    [OriginalDataFact]
    public void SharesPrompts_AskEveryCharacter_DeadFromZero_NotTheElemental()
    {
        var s = WithParty();
        var prompts = new SessionPrompts(s);
        var town = s.CurrentTownId!.Value;
        s.OpenGuild();
        Assert.True(s.TryAddElemental(0, out var err), err);
        s.LeaveGuild();
        Assert.True(s.LeaveTown());
        var members = s.Party.Members.ToList();
        members[1].Status = CharacterStatus.Dead;
        members[2].Status = CharacterStatus.Out;
        members[3].P3.ExperienceShares = 3;
        var before = members.Select(m => m.Experience).ToList();
        s.Sack.Experience = 600;

        s.EnterTown(town);

        var asked = new List<SessionPrompt>();
        while (prompts.Current is { Kind: SessionPromptKind.ExperienceShares } p)
        {
            asked.Add(p);
            prompts.Answer(p.InitialChoice!.Value); // Return on the pre-filled value
        }
        Assert.Equal(members.Count, asked.Count); // four characters; the elemental is never asked (FAR:0xb614)
        for (var i = 0; i < members.Count; i++)
            Assert.Contains(members[i].Name, asked[i].Lines[^1]);
        Assert.Equal(["0", "1", "2", "3"], asked[1].Options); // dead: 0..3, starts on 0
        Assert.Equal("0", asked[1].Options[asked[1].InitialChoice!.Value - 1]);
        Assert.Equal(["1", "2", "3"], asked[2].Options); // Out counts as living (status > 0)
        Assert.Equal("3", asked[3].Options[asked[3].InitialChoice!.Value - 1]);
        // shares 1, 0, 1, 3 = 5: 600 / 5 = 120 a share, no remainder
        Assert.Equal([120L, 0L, 120L, 360L], members.Select((m, i) => m.Experience - before[i]));
        Assert.Equal(0, members[1].P3.ExperienceShares);
    }

    [OriginalDataFact]
    public void EnterTown_DropsTheLastWand_ALoneWandOpensNoHandOut()
    {
        var s = WithParty();
        var town = s.CurrentTownId!.Value;
        Assert.True(s.LeaveTown());
        s.Sack.ItemIds.AddRange([180, 20, 180]);
        s.EnterTown(town);
        Assert.Equal([180, 20], s.Sack.ItemIds); // ovl2 0x5a9: the last one only
        Assert.True(s.TownItemHandOutPending);

        var t = WithParty();
        var town2 = t.CurrentTownId!.Value;
        Assert.True(t.LeaveTown());
        t.Sack.ItemIds.Add(180);
        t.EnterTown(town2);
        Assert.Equal(0, t.Sack.ItemCount);
        Assert.False(t.TownItemHandOutPending);
    }

    [OriginalDataFact]
    public void EnterTown_ReadiesBestEquipment_OnlyOnAPayingEntry()
    {
        var s = WithParty();
        var town = s.CurrentTownId!.Value;
        var m = s.Party.Members[0];
        m.Stats = m.Stats with { Str = 18 };
        Assert.True(s.LeaveTown());
        m.InventoryItemIds.Add(16); // Giant Shield: 16 points, STR 16
        s.EnterTown(town); // empty sack: DOS calls ovl2 0x0000 only with a pool or found items
        Assert.Equal(4, m.WornShield);

        Assert.True(s.LeaveTown());
        s.Sack.Experience = 10;
        s.EnterTown(town);
        Assert.Equal(16, m.WornShield);
    }

    [OriginalDataFact]
    public void EnterTown_FoundGoldJoinsThePurse()
    {
        var s = WithParty();
        var town = s.CurrentTownId!.Value;
        Assert.True(s.LeaveTown());
        var gold = s.Party.GoldCarried;
        s.Sack.Gold = 50;
        s.EnterTown(town);
        Assert.Equal(0, s.Sack.Gold);
        Assert.Equal(gold + 50, s.Party.GoldCarried);
        Assert.False(s.TownXpSharesPending);
    }

    [OriginalDataFact]
    public void EnterTown_EmptySack_NoHandOut()
    {
        var s = WithParty();
        var town = s.CurrentTownId!.Value;
        Assert.True(s.LeaveTown());
        s.EnterTown(town);
        Assert.False(s.TownItemHandOutPending);
    }

    [OriginalDataFact]
    public void LeaveTown_DropsAnUnopenedHandOut()
    {
        var s = WithParty();
        var town = s.CurrentTownId!.Value;
        Assert.True(s.LeaveTown());
        s.Sack.ItemIds.Add(20);
        s.EnterTown(town);
        Assert.True(s.LeaveTown());
        Assert.False(s.TakeTownItemHandOut());
    }

    [OriginalDataFact]
    public void LeaveTown_DropsUnaskedShares()
    {
        var s = WithParty();
        var town = s.CurrentTownId!.Value;
        Assert.True(s.LeaveTown());
        s.Sack.Experience = 90;
        s.EnterTown(town);
        Assert.True(s.TownXpSharesDue);
        Assert.True(s.LeaveTown());
        Assert.False(s.TownXpSharesPending);
        Assert.Equal(90, s.Sack.Experience); // kept for the next town
    }

    private sealed class Catalog : IItemCatalog
    {
        private readonly Dictionary<int, ItemCatalogEntry> _items = new()
        {
            [4] = Item(4, 4, 4, "str"), // Small Shield
            [8] = Item(8, 8, 8, "str"), // Medium Shield
            [16] = Item(16, 16, 16, "str"), // Giant Shield
            [22] = Item(22, 2, 4, "str"), // Robes
            [44] = Item(44, 4, 6, "str_plus_dex"), // Short Bow
            [47] = Item(47, 7, 12, "str_plus_dex"), // Medium Bow
            [64] = Item(64, 4, 12, "str2_plus_dex"), // Mace
            [65] = Item(65, 4, 15, "str2_plus_dex"), // Small Axe
            [70] = Item(70, 8, 30, "str2_plus_dex"), // Spear
        };

        private static ItemCatalogEntry Item(int id, int points, int need, string check) =>
            new() { Id = id, Name = $"#{id}", ValueFactor = 0, Points = points, Requirement = need, StrCheck = check };

        public ItemCatalogEntry? Get(int itemId) => _items.GetValueOrDefault(itemId);

        public int Count => _items.Count;
    }

    private static readonly EquipmentLayout P3Slots = new([
        new EquipmentSlot(WornItems.Shield, 1, 20), new EquipmentSlot(WornItems.Armor, 21, 40),
        new EquipmentSlot(WornItems.Bow, 41, 60), new EquipmentSlot(WornItems.Weapon, 61, 100),
    ]);

    [Fact]
    public void ReadyBest_TieGoesToTheLaterItem_UnusableSkipped_WornKept()
    {
        var c = new Character { Stats = new CharacterStats(Str: 6, Int: 10, Dex: 6, Con: 10, Cha: 10, Luck: 10) };
        c.InventoryItemIds.AddRange([64, 65, 70, 16, 44, 47]);
        c.WornShield = 8; // worn but no longer carried: kept, nothing usable beats it (16 needs STR 16)
        c.WornWeapon = 0;
        c.WornBow = 0;
        c.WornArmor = 22;

        P3TownEntryPayout.ReadyBestEquipment(c, new Catalog(), P3Slots);

        Assert.Equal(65, c.WornWeapon); // 2·6 + 6 = 18 ≥ 15: Mace and Small Axe tie on 4 points, the later one is worn
        Assert.Equal(8, c.WornShield);
        Assert.Equal(47, c.WornBow); // STR + DEX = 12 ≥ 12
        Assert.Equal(22, c.WornArmor); // no armour carried: the slot is never emptied
    }

    [Fact]
    public void ShareOut_RefusesShareCountsDosDoesNotOffer()
    {
        var alive = new Character { Name = "A", Status = CharacterStatus.Ok };
        var dead = new Character { Name = "B", Status = CharacterStatus.Dead };
        var sack = new LootSack { Experience = 10 };
        var rng = new SystemRandomSource();
        Assert.Throws<ArgumentException>(() => P3TownEntryPayout.ShareOut([alive, dead], [0, 0], sack, rng));
        Assert.Throws<ArgumentException>(() => P3TownEntryPayout.ShareOut([alive, dead], [1, 4], sack, rng));
        P3TownEntryPayout.ShareOut([alive, dead], [2, 0], sack, rng);
        Assert.Equal(10, alive.Experience + dead.Experience);
        Assert.Equal(0, sack.Experience);
    }
}
