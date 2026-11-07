using System.Text.Json.Nodes;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>Phase 09 T1.2: game-prefixed ids in game data and remaster saves (no change to how Phantasie I plays).</summary>
public class ContentIdTests
{
    private static GameDataRepository Data => GameDataRepository.LoadPhantasie1();

    // --- The id itself -----------------------------------------------------------------------------

    [Theory]
    [InlineData(GameId.P1, 0, "p1:0")]
    [InlineData(GameId.P1, 180, "p1:180")]
    [InlineData(GameId.P2, 7, "p2:7")]
    [InlineData(GameId.P4, 12345, "p4:12345")]
    public void FormatsAndParses(GameId game, int local, string text)
    {
        var id = new ContentId(game, local);
        Assert.Equal(text, id.ToString());
        Assert.Equal(id, ContentId.Parse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("42")]
    [InlineData("p1:")]
    [InlineData(":4")]
    [InlineData("p5:1")]
    [InlineData("p0:1")]
    [InlineData("P1:1")]
    [InlineData("p1:-1")]
    [InlineData("p1:+1")]
    [InlineData("p1:1a")]
    [InlineData(" p1:1")]
    [InlineData("p1:1 ")]
    [InlineData("p1:99999999999")]
    public void RejectsMalformed(string? text)
    {
        Assert.False(ContentId.TryParse(text, out _));
        if (text is not null)
            Assert.Throws<FormatException>(() => ContentId.Parse(text));
    }

    [Fact]
    public void SameLocalInTwoGames_AreDifferentIds()
    {
        Assert.Equal("?:0", default(ContentId).ToString());
        Assert.NotEqual(new ContentId(GameId.P1, 5), new ContentId(GameId.P2, 5));
        Assert.Equal(5, new ContentId(GameId.P1, 5).LocalIn(GameId.P1));
        Assert.Throws<InvalidDataException>(() => new ContentId(GameId.P2, 5).LocalIn(GameId.P1));
    }

    // --- Game data -----------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Phantasie1Data_EveryRecordCarriesItsP1Id()
    {
        var data = Data;
        Assert.Equal(GameId.P1, data.Game);
        Assert.All(data.Items, i => Assert.Equal(new ContentId(GameId.P1, i.Id), i.ContentId));
        Assert.All(data.Spells, s => Assert.Equal(new ContentId(GameId.P1, s.Id), s.ContentId));
        Assert.All(data.Monsters, m => Assert.Equal(new ContentId(GameId.P1, m.Id), m.ContentId));
        Assert.All(data.Dungeons, d => Assert.Equal($"DNG{d.ContentId.Local}", d.Id));
        Assert.All(data.Dungeons, d => Assert.Equal(GameId.P1, d.ContentId.Game));
        Assert.Equal(data.Items.Count, data.Items.Select(i => i.ContentId).Distinct().Count());
    }

    [OriginalDataFact]
    public void Lookups_ById_FindOwnGameOnly()
    {
        var data = Data;
        Assert.Same(data.GetItem(46), data.GetItem(ContentId.Parse("p1:46")));
        Assert.Same(data.GetSpell(3), data.GetSpell(data.IdOf(3)));
        Assert.Same(data.GetMonster(0), data.GetMonster(ContentId.Parse("p1:0")));
        Assert.Equal("DNG3", data.GetDungeon(ContentId.Parse("p1:3")).Id);

        // A II/III/IV id never resolves to I's record with the same number.
        Assert.Throws<KeyNotFoundException>(() => data.GetItem(new ContentId(GameId.P2, 46)));
        Assert.Throws<KeyNotFoundException>(() => data.GetSpell(new ContentId(GameId.P3, 3)));
        Assert.Throws<KeyNotFoundException>(() => data.GetMonster(new ContentId(GameId.P4, 0)));
        Assert.Throws<KeyNotFoundException>(() => data.GetDungeon(new ContentId(GameId.P2, 3)));
    }

    [OriginalDataTheory]
    [InlineData("items.json", "\"id\": \"p1:5\",", "\"id\": \"p1:6\",")]     // local ≠ index
    [InlineData("spells.json", "\"id\": \"p1:5\",", "\"id\": \"5\",")]       // no prefix
    [InlineData("monsters.json", "\"id\": \"p1:5\",", "\"id\": \"p2:5\",")]  // a second game in one folder
    [InlineData("dungeons.json", "\"id\": \"p1:4\",", "\"id\": \"p1:40\",")] // level number ≠ DNG key
    public void Loader_RejectsBadIds(string file, string from, string to)
    {
        var dir = CopyData();
        try
        {
            var path = Path.Combine(dir, file);
            var text = File.ReadAllText(path);
            Assert.Contains(from, text);
            File.WriteAllText(path, ReplaceFirst(text, from, to));
            Assert.Throws<InvalidDataException>(() => GameDataRepository.Load(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // --- Remaster saves ------------------------------------------------------------------------------

    [OriginalDataFact]
    public void SaveJson_WritesPrefixedIds_AndLoadsBackTheLocalNumbers()
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(7));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Ids", Race.Human, CharacterClass.Fighter);
        hero.InventoryItemIds.Clear();
        hero.InventoryItemIds.AddRange([46, 0, 12]); // an empty slot in the middle stays in place (s58)
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.Sack.DepositRewards(11, 22, [46, 101]);
        session.BeginCombat(Data.Monsters[3].Id);

        var snap = session.CaptureSnapshot();
        Assert.Equal(GameId.P1, snap.Game);
        var json = SessionCodec.WriteJson(snap);
        var root = JsonNode.Parse(json)!.AsObject();

        Assert.Equal("p1", (string?)root["game"]);
        Assert.Equal(["p1:46", "p1:101"], root["sack"]!["itemIds"]!.AsArray().Select(n => (string?)n));
        Assert.Equal("p1:3", (string?)root["combat"]!["firstMonsterId"]);
        Assert.All(root["combat"]!["monsters"]!.AsArray(), m => Assert.StartsWith("p1:", (string?)m!["id"]));
        var character = JsonNode.Parse((string)root["roster"]![0]!["characterJson"]!)![0]!;
        Assert.Equal(["p1:46", "p1:0", "p1:12"], character["inventoryItemIds"]!.AsArray().Select(n => (string?)n));
        Assert.Equal($"p1:{hero.WornWeapon}", (string?)character["wornWeapon"]);

        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        loaded.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.Equal([46, 101], loaded.Sack.ItemIds);
        Assert.Equal([46, 0, 12], loaded.Roster.Slots[0]!.InventoryItemIds);
        Assert.Equal(hero.WornWeapon, loaded.Roster.Slots[0]!.WornWeapon);
        Assert.Equal(3, loaded.Combat!.FirstMonsterId);
        Assert.Equal(session.Combat!.AllMonsters.Select(m => m.Id), loaded.Combat.AllMonsters.Select(m => m.Id));
    }

    [Fact]
    public void SaveJson_DungeonAndTownIds_ArePrefixed()
    {
        var stock = new int[TownState.StockSlotCount];
        stock[46] = 5;
        var snap = new SessionSnapshot
        {
            Mode = GameMode.Dungeon,
            Dungeon = new DungeonSnapshot { LevelId = 4, X = 1, Y = 2 },
            DungeonSave = new DungeonSaveSnapshot { DungeonId = 4, LevelBuffer = [1, 2, 3] },
            Towns = [new TownState(1, 0, "PELNOR", 150, stock) { RestockItems = [46, 21] }],
        };
        var root = JsonNode.Parse(SessionCodec.WriteJson(snap))!.AsObject();
        Assert.Equal("p1:4", (string?)root["dungeon"]!["levelId"]);
        Assert.Equal("p1:4", (string?)root["dungeonSave"]!["dungeonId"]);
        var town = root["towns"]![0]!;
        Assert.Equal(["p1:46", "p1:21"], town["restockItems"]!.AsArray().Select(n => (string?)n));
        Assert.Equal(5, (int)town["stock"]![46]!); // stock stays positional (index = item number) under the root game

        var back = SessionCodec.ReadJson(root.ToJsonString());
        Assert.Equal(4, back.Dungeon!.LevelId);
        Assert.Equal(4, back.DungeonSave!.DungeonId);
        Assert.Equal([46, 21], back.Towns[0].RestockItems);
        Assert.Equal(5, back.Towns[0].Stock[46]);
    }

    [Fact]
    public void SaveJson_WildernessPromptMonster_IsPrefixed_OnlyWhileAPromptIsOpen()
    {
        var snap = new SessionSnapshot
        {
            Wilderness = new WildernessSnapshot { PromptMenu = WildernessMenu.BlackKnights, PromptMonsterId = 17 },
        };
        var root = JsonNode.Parse(SessionCodec.WriteJson(snap))!.AsObject();
        Assert.Equal("p1:17", (string?)root["wilderness"]!["promptMonsterId"]);
        Assert.Equal(17, SessionCodec.ReadJson(root.ToJsonString()).Wilderness!.PromptMonsterId);

        snap.Wilderness = new WildernessSnapshot { PromptMenu = null, PromptMonsterId = 0 };
        root = JsonNode.Parse(SessionCodec.WriteJson(snap))!.AsObject();
        Assert.Null(root["wilderness"]!["promptMonsterId"]);
        Assert.Equal(0, SessionCodec.ReadJson(root.ToJsonString()).Wilderness!.PromptMonsterId);
    }

    [Fact]
    public void SaveJson_DungeonEntryPrompt_IsPrefixed_TownEntryStaysBare()
    {
        var snap = new SessionSnapshot
        {
            Wilderness = new WildernessSnapshot { EntryKind = OverworldEntryKind.Dungeon, EntryId = 6 },
        };
        var root = JsonNode.Parse(SessionCodec.WriteJson(snap))!.AsObject();
        Assert.Equal("p1:6", (string?)root["wilderness"]!["entryId"]);
        Assert.Equal(6, SessionCodec.ReadJson(root.ToJsonString()).Wilderness!.EntryId);

        snap.Wilderness = new WildernessSnapshot { EntryKind = OverworldEntryKind.Town, EntryId = 3 };
        root = JsonNode.Parse(SessionCodec.WriteJson(snap))!.AsObject();
        Assert.Equal(3, (int)root["wilderness"]!["entryId"]!);
        Assert.Equal(3, SessionCodec.ReadJson(root.ToJsonString()).Wilderness!.EntryId);
    }

    [Fact]
    public void ReadJson_RefusesOldBareIdSaves_AndOtherGamesIds()
    {
        var snap = new SessionSnapshot { Sack = new LootSackSnapshot { ItemIds = [46] } };
        var good = JsonNode.Parse(SessionCodec.WriteJson(snap))!.AsObject();

        // A pre-T1.2 save: bare numbers, version 2, no game. Not migrated (memory rule: no old-save compat).
        var bare = good.DeepClone().AsObject();
        bare["sack"]!["itemIds"] = new JsonArray(46);
        Assert.Throws<OldSaveFormatException>(() => SessionCodec.ReadJson(bare.ToJsonString()));

        var oldVersion = good.DeepClone().AsObject();
        oldVersion["version"] = 2;
        Assert.Throws<OldSaveFormatException>(() => SessionCodec.ReadJson(oldVersion.ToJsonString()));

        var noGame = good.DeepClone().AsObject();
        noGame.Remove("game");
        Assert.Throws<OldSaveFormatException>(() => SessionCodec.ReadJson(noGame.ToJsonString()));

        // A Phantasie II id inside a Phantasie I save is refused, never read as I's item 46.
        var foreign = good.DeepClone().AsObject();
        foreign["sack"]!["itemIds"] = new JsonArray("p2:46");
        Assert.Throws<InvalidDataException>(() => SessionCodec.ReadJson(foreign.ToJsonString()));

        Assert.Equal([46], SessionCodec.ReadJson(good.ToJsonString()).Sack.ItemIds);
    }

    [OriginalDataFact]
    public void ApplySnapshot_RefusesAnotherGamesSave()
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        var snap = session.CaptureSnapshot();
        snap.Game = GameId.P3;
        Assert.Throws<InvalidDataException>(() => session.ApplySnapshot(snap));
    }

    [Fact]
    public void GuildJson_ItemIdsArePrefixed_SpellFlagsStayPositional()
    {
        var ch = new Character { Name = "pfx", WornShield = 2, WornArmor = 0, WornWeapon = 30 };
        ch.InventoryItemIds.AddRange([5, 0, 7]);
        ch.SpellsKnown[3] = 1;

        var json = GuildCodec.WriteJson([ch]);
        var node = JsonNode.Parse(json)![0]!;
        Assert.Equal("p1:2", (string?)node["wornShield"]);
        Assert.Equal("p1:0", (string?)node["wornArmor"]);
        Assert.Equal(["p1:5", "p1:0", "p1:7"], node["inventoryItemIds"]!.AsArray().Select(n => (string?)n));
        Assert.Equal(1, (int)node["spellsKnown"]![3]!);

        var back = Assert.Single(GuildCodec.ReadJson(json));
        Assert.Equal([5, 0, 7], back.InventoryItemIds);
        Assert.Equal((2, 0, 30), (back.WornShield, back.WornArmor, back.WornWeapon));
        Assert.Throws<InvalidDataException>(() => GuildCodec.ReadJson(json, GameId.P2));
    }

    private static string CopyData()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-ids-").FullName;
        foreach (var f in Directory.GetFiles(GameDataRepository.DefaultPhantasie1Path))
            File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
        return dir;
    }

    private static string ReplaceFirst(string text, string from, string to)
    {
        var at = text.IndexOf(from, StringComparison.Ordinal);
        return text[..at] + to + text[(at + from.Length)..];
    }
}
