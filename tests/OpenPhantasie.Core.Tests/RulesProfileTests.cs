using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T1.3: the per-game rule constants (gold caps, Guild size, starting gold) come from
/// <c>GameData/phantasie1/rules.json</c> and every consumer reads them from the session's profile.
/// </summary>
public class RulesProfileTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    /// <summary>
    /// A profile unlike Phantasie I in every number, so a leftover hardcoded value shows. Its game stays p1: a session
    /// refuses rules of another game than its campaign's (T1.4).
    /// </summary>
    private static readonly RulesProfile Odd = new()
    {
        Game = GameId.P1,
        PartyGoldCap = 1_000_000_000,
        BankGoldCap = 100_000,
        GuildSlotCount = 5,
        GuildMaxCharacters = 3,
        StartingGold = 1_000,
        Equipment = EquipmentLayout.PhantasieI,
    };

    private const string ValidJson = """
        { "game": "p1", "party_gold_cap": 65000, "bank_gold_cap": 65000, "guild_slot_count": 22,
          "guild_max_characters": 21, "starting_gold": 256,
          "equipment_slots": [ { "id": "shield", "first_item": 1, "last_item": 20 },
            { "id": "armor", "first_item": 21, "last_item": 40 }, { "id": "weapon", "first_item": 41, "last_item": 100 } ] }
        """;

    [OriginalDataFact]
    public void ShippedRulesJson_IsExactlyPhantasieI()
    {
        var loaded = RulesProfileLoader.LoadPhantasie1();
        Assert.Equal(RulesProfile.PhantasieI, loaded);
        Assert.Equal(GameId.P1, loaded.Game);
        Assert.Equal(65_000, loaded.PartyGoldCap);
        Assert.Equal(65_000, loaded.BankGoldCap);
        Assert.Equal(22, loaded.GuildSlotCount);
        Assert.Equal(21, loaded.GuildMaxCharacters);
        Assert.Equal(256, loaded.StartingGold);
        Assert.Equal(RulesProfile.PhantasieI, Data.Rules);
    }

    [OriginalDataFact]
    public void FactorySession_UsesTheRepositoryRules()
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        Assert.Same(Data.Rules, s.Rules);
        Assert.Same(s.Rules, s.Party.Rules);
        Assert.Equal(22, s.Roster.Slots.Length);
        Assert.Equal(21, s.Roster.MaxCharacters);
        Assert.Equal(65_000, s.Sack.GoldCap);
    }

    [Fact]
    public void GuildRecordCount_StaysAFormatFact()
    {
        Assert.Equal(22, GuildCodec.SlotCount);
        Assert.Equal(282 * 22, GuildCodec.FileSize);
    }

    [Theory]
    [InlineData("party_gold_cap")]
    [InlineData("bank_gold_cap")]
    [InlineData("guild_slot_count")]
    [InlineData("guild_max_characters")]
    [InlineData("starting_gold")]
    public void Loader_MissingField_Throws(string field)
    {
        var json = System.Text.RegularExpressions.Regex.Replace(ValidJson, $"\"{field}\": \\d+,?", "");
        var ex = Assert.Throws<InvalidDataException>(() => RulesProfileLoader.Parse(json));
        Assert.Contains(field, ex.Message);
    }

    [Theory]
    [InlineData("\"game\": \"p1\"", "\"game\": \"p9\"")]
    [InlineData("\"party_gold_cap\": 65000", "\"party_gold_cap\": 0")]
    [InlineData("\"bank_gold_cap\": 65000", "\"bank_gold_cap\": -1")]
    [InlineData("\"guild_max_characters\": 21", "\"guild_max_characters\": 23")]
    [InlineData("\"starting_gold\": 256", "\"starting_gold\": -5")]
    [InlineData("\"last_item\": 20", "\"last_item\": 30")] // shield overlaps armor
    [InlineData("\"id\": \"armor\"", "\"id\": \"shield\"")] // duplicate slot id
    [InlineData("\"id\": \"weapon\"", "\"id\": \"tail\"")] // no character field for it
    [InlineData("\"first_item\": 41", "\"first_item\": 0")]
    public void Loader_BadValue_Throws(string from, string to) =>
        Assert.Throws<InvalidDataException>(() => RulesProfileLoader.Parse(ValidJson.Replace(from, to)));

    [Fact]
    public void Loader_MissingEquipmentSlots_Throws()
    {
        var cut = ValidJson.IndexOf("\"equipment_slots\"", StringComparison.Ordinal);
        var json = ValidJson[..ValidJson.LastIndexOf(',', cut)] + " }";
        var ex = Assert.Throws<InvalidDataException>(() => RulesProfileLoader.Parse(json));
        Assert.Contains("equipment_slots", ex.Message);
    }

    [Fact]
    public void ShippedEquipmentSlots_AreShieldArmorWeapon()
    {
        var layout = RulesProfileLoader.LoadPhantasie1().Equipment;
        Assert.Equal(["shield", "armor", "weapon"], layout.Slots.Select(s => s.Id));
        Assert.Equal("shield", layout.SlotFor(20)!.Id);
        Assert.Equal("armor", layout.SlotFor(21)!.Id);
        Assert.Equal("weapon", layout.SlotFor(100)!.Id);
        Assert.Null(layout.SlotFor(101));
        Assert.Null(layout.SlotFor(0));
    }

    [Fact]
    public void Loader_ParsesOtherGames()
    {
        var p = RulesProfileLoader.Parse(ValidJson.Replace("\"p1\"", "\"p3\"").Replace("\"party_gold_cap\": 65000", "\"party_gold_cap\": 1000000000"));
        Assert.Equal(GameId.P3, p.Game);
        Assert.Equal(1_000_000_000, p.PartyGoldCap);
    }

    [OriginalDataFact]
    public void Session_HonoursANonDefaultProfile_Everywhere()
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(7), rules: Odd);
        Assert.Same(Odd, s.Rules);
        Assert.Equal(5, s.Roster.Slots.Length);
        s.StartNewGame();
        s.OpenGuild();

        // Starting gold and the Guild-full limit.
        var a = s.CreateAndEnroll("Alpha", Race.Human, CharacterClass.Fighter);
        Assert.Equal(1_000, a.GoldInBank);
        s.CreateAndEnroll("Bravo", Race.Elf, CharacterClass.Priest);
        Assert.True(s.GuildHasRoom);
        s.CreateAndEnroll("Charlie", Race.Dwarf, CharacterClass.Thief);
        Assert.False(s.GuildHasRoom);
        var extra = s.RollNewMember(Race.Human, CharacterClass.Monk);
        Assert.False(s.TryEnrollNewMember(extra, "Delta", out var full));
        Assert.Equal("Guild roster is full.", full);

        // Party purse: no 65,000 cap.
        Assert.True(s.TryAddToParty(a, out _));
        Assert.Equal(0, s.Party.AddGold(500_000_000));
        Assert.Equal(500_000_000, s.Party.GoldCarried);
        Assert.Equal(500_000_000, s.Party.AddGold(1_000_000_000)); // the excess over 1,000,000,000 is lost
        Assert.Equal(1_000_000_000, s.Party.GoldCarried);

        // Bank deposit clamps at this profile's bank cap.
        s.Party.GoldCarried = 200_000;
        Assert.Equal(TownTransactionKind.Ok, s.BankDeposit([1]).Kind);
        Assert.Equal(100_000, a.GoldInBank);

        // Found gold in the sack follows the party cap.
        s.Sack.DepositRewards(0, 70_000);
        Assert.Equal(70_000, s.Sack.Gold);

        // The save keeps the profile's roster size through JSON.
        var snap = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));
        Assert.Equal(5, snap.RosterSlots.Length);
    }

    [OriginalDataTheory]
    [InlineData(5, "No such Guild member.")]
    [InlineData(6, "The elementals live in Town #0.")]
    [InlineData(8, "The elementals live in Town #0.")]
    [InlineData(9, "No such Guild member.")]
    public void Continue_ElementalNumbers_FollowTheRosterSize(int number, string expected)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(7), rules: Odd);
        s.StartNewGame();
        s.OpenGuild();
        s.CreateAndEnroll("Alpha", Race.Human, CharacterClass.Fighter);
        var file = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));
        Assert.False(s.Continue(file, number, out var error));
        Assert.Equal(expected, error);
    }

    [Fact]
    public void ShippedRulesJson_StatesTheGuildSizePerPlatform()
    {
        var g = RulesProfileLoader.LoadPhantasie1().GuildByPlatform;
        Assert.Equal(["amiga", "apple2", "c64", "dos"], g.Platforms.Order());
        Assert.Equal(new PlatformGuildSize(22, 22), g["dos"]);
        Assert.Equal(new PlatformGuildSize(22, null), g["amiga"]);
        Assert.Equal(new PlatformGuildSize(37, 37), g["apple2"]);
        Assert.Equal(new PlatformGuildSize(null, 21), g["c64"]);
    }

    [Fact]
    public void ForPlatform_AppliesThatPlatformsGuild_AndKeepsTheRest()
    {
        var p1 = RulesProfile.PhantasieI;
        var dos = p1.ForPlatform("dos");
        Assert.Equal((22, 22), (dos.GuildSlotCount, dos.GuildMaxCharacters));
        Assert.Equal(p1.StartingGold, dos.StartingGold);
        var apple = p1.ForPlatform("apple2");
        Assert.Equal((37, 37), (apple.GuildSlotCount, apple.GuildMaxCharacters));
        Assert.Equal(37, new GuildRoster(apple).Slots.Length);
        var c64 = p1.ForPlatform("c64");
        Assert.Equal((22, 21), (c64.GuildSlotCount, c64.GuildMaxCharacters));
        var amiga = p1.ForPlatform("amiga");
        Assert.Equal((22, 21), (amiga.GuildSlotCount, amiga.GuildMaxCharacters));
        Assert.Same(p1, p1.ForPlatform("atari-st")); // unknown [?]: the remaster's numbers
    }

    [Fact]
    public void Equality_ComparesThePlatformTableByContent()
    {
        var a = RulesProfile.PhantasieI with { GuildByPlatform = new(RulesProfile.PhantasieI.GuildByPlatform.Entries.Reverse()) };
        Assert.Equal(RulesProfile.PhantasieI, a);
        var b = RulesProfile.PhantasieI with { GuildByPlatform = new([KeyValuePair.Create("dos", new PlatformGuildSize(22, 22))]) };
        Assert.NotEqual(RulesProfile.PhantasieI, b);
    }

    [Fact]
    public void Loader_GuildByPlatform_IsOptional() =>
        Assert.Equal(PlatformGuildTable.Empty, RulesProfileLoader.Parse(ValidJson).GuildByPlatform);

    [Theory]
    [InlineData("{ \"x\": {} }")]
    [InlineData("{ \"x\": null }")]
    [InlineData("{ \"x\": { \"slots\": 0 } }")]
    [InlineData("{ \"x\": { \"max_characters\": 0 } }")]
    [InlineData("{ \"x\": { \"max_characters\": 23 } }")] // above the top-level 22 slots
    [InlineData("{ \"x\": { \"slots\": 10, \"max_characters\": 11 } }")]
    public void Loader_BadGuildByPlatform_Throws(string block) =>
        Assert.Throws<InvalidDataException>(() =>
            RulesProfileLoader.Parse(ValidJson.Replace("\"starting_gold\": 256", $"\"starting_gold\": 256, \"guild_by_platform\": {block}")));

    [Fact]
    public void ParameterlessConstructors_UsePhantasieI()
    {
        Assert.Same(RulesProfile.PhantasieI, new Party().Rules);
        Assert.Equal(22, new GuildRoster().Slots.Length);
        Assert.Equal(21, new GuildRoster().MaxCharacters);
        Assert.Equal(65_000, new LootSack().GoldCap);
    }
}
