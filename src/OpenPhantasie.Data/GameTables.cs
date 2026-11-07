using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// A game data folder's manifest (<c>GameData/&lt;game&gt;/game.json</c>, schema <c>phantasie.game/v1</c>): which game it
/// holds, the original platform its numbers come from, and how many records each shared table has. Phase 09 T3: the
/// counts differ per game (I: 18 regions, 10 levels, 15 races; II: 17, 8, 16), so the generic loader reads them here
/// instead of from Phantasie I constants.
/// </summary>
public sealed record GameManifest(
    GameId Game,
    string Platform,
    IReadOnlyDictionary<string, int> Counts,
    string? SpellCostRule)
{
    public const string FileName = "game.json";

    /// <summary>The DOS rule "MP cost = (DOS slot % 4) + 1" (Phantasie I).</summary>
    public const string DosSlotCostRule = "dos-slot-mod-4";

    public static readonly IReadOnlyList<string> RequiredCounts =
    [
        "monsters", "items", "spells", "classes", "races", "overworld_regions", "dungeon_levels", "scrolls", "towns",
        "elementals",
    ];

    /// <summary>
    /// Counts a folder may set to 0 or leave out, with the table's file then absent (T4: Phantasie III has no MESS
    /// message files; its dungeon texts are in <c>dungeons.json</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> OptionalCounts = ["message_areas"];

    public int Count(string table) => Counts.TryGetValue(table, out var n) ? n : 0;

    public static GameManifest Load(string path)
    {
        Dto dto;
        try
        {
            dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(path), Options)
                  ?? throw new InvalidDataException($"{path}: empty manifest.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{path}: {ex.Message}", ex);
        }

        var game = Enum.GetValues<GameId>().Where(g => g.Prefix() == dto.Game).Cast<GameId?>().FirstOrDefault()
                   ?? throw new InvalidDataException($"{path}: unknown game \"{dto.Game}\" (expected p1..p4).");
        var counts = dto.Counts ?? throw new InvalidDataException($"{path}: \"counts\" is missing.");
        foreach (var name in RequiredCounts)
        {
            if (!counts.TryGetValue(name, out var n) || n <= 0)
                throw new InvalidDataException($"{path}: counts.{name} must be a positive number.");
        }
        foreach (var name in OptionalCounts)
        {
            if (counts.TryGetValue(name, out var n) && n < 0)
                throw new InvalidDataException($"{path}: counts.{name} must not be negative.");
        }
        if (dto.SpellCostRule is not (null or DosSlotCostRule))
            throw new InvalidDataException($"{path}: unknown spell_cost_rule \"{dto.SpellCostRule}\".");
        return new GameManifest(game, dto.Platform ?? throw new InvalidDataException($"{path}: \"platform\" is missing."),
            counts, dto.SpellCostRule);
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private sealed class Dto
    {
        [JsonPropertyName("game")] public string? Game { get; set; }
        [JsonPropertyName("platform")] public string? Platform { get; set; }
        [JsonPropertyName("counts")] public Dictionary<string, int>? Counts { get; set; }
        [JsonPropertyName("spell_cost_rule")] public string? SpellCostRule { get; set; }
    }
}

/// <summary>
/// The tables every Phantasie game shares a schema for (monsters, items, spells, names, towns, overworld, dungeons,
/// messages, scrolls, creation tables, elementals, rules), loaded from any game's data folder by its
/// <see cref="GameManifest"/>. Phase 09 T3: this is how Phantasie II's data loads before II has a campaign; it holds
/// no play state and builds no session. Phantasie I's full game data (sprites, traits, travel time …) stays
/// <see cref="GameDataRepository"/>.
/// </summary>
public sealed class GameTables
{
    public required GameManifest Manifest { get; init; }
    public GameId Game => Manifest.Game;
    public required IReadOnlyList<MonsterDefinition> Monsters { get; init; }
    public required IReadOnlyList<ItemDefinition> Items { get; init; }
    public required IReadOnlyList<SpellDefinition> Spells { get; init; }
    public required NameTables Names { get; init; }
    public required IReadOnlyList<TownDefinition> Towns { get; init; }
    public required IReadOnlyList<OverworldRegionDefinition> OverworldRegions { get; init; }
    public required IReadOnlyList<DungeonLevelDefinition> Dungeons { get; init; }
    public required IReadOnlyList<MessageAreaDefinition> MessageAreas { get; init; }
    public required IReadOnlyList<ScrollDefinition> Scrolls { get; init; }
    public required CharacterCreationTables CreationTables { get; init; }
    public required IReadOnlyList<ElementalDefinition> Elementals { get; init; }
    public required RulesProfile Rules { get; init; }

    /// <summary>The wilderness step costs (<c>travel-time.json</c>); null when the folder has none.</summary>
    public TravelTimeRules? TravelTime { get; init; }

    /// <summary>
    /// Hard-coded wilderness places (<c>overworld.json</c> <c>places</c>; Phantasie III's towns, dungeons and inns by
    /// plane and square). Empty when the folder's places are region features (Phantasie I, II).
    /// </summary>
    public IReadOnlyList<WorldPlaceDefinition> Places { get; init; } = [];

    /// <summary>Tile-range terrain (<c>overworld.json</c> <c>terrain</c>, Phantasie III); null when the folder has none.</summary>
    public TileTerrainDefinition? TileTerrain { get; init; }

    /// <summary>Scroll item id → the scroll records it shows (<c>scrolls.json</c> <c>item_records</c>, III); empty when absent.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<int>> ScrollItemRecords { get; init; } =
        new Dictionary<int, IReadOnlyList<int>>();

    /// <summary>Phantasie III's own tables (<c>rule-tables.json</c>); null for folders without one.</summary>
    public Phantasie3RuleTables? Phantasie3 { get; init; }

    /// <summary>Phantasie IV's own tables (<c>rule-tables.json</c>, T6); null for folders of other games.</summary>
    public Phantasie4RuleTables? Phantasie4 { get; init; }

    /// <summary>The wilderness traversal of these tables: their regions, step costs and terrain rules (no town aliases).</summary>
    public OverworldTraversal CreateTraversal(IReadOnlyDictionary<int, int>? townAliases = null) =>
        new(OverworldRegions.Select(r => r.ToRuntime()), TravelTime, townAliases, Rules.Terrain);

    /// <summary>Load the shared tables of the data folder <paramref name="gameDataDirectory"/> (it must hold a game.json).</summary>
    public static GameTables Load(string gameDataDirectory) => GameDataRepository.LoadTables(gameDataDirectory);

    /// <summary>Load the shipped folder of <paramref name="game"/> (<see cref="GameDataRepository.DefaultPathFor"/>).</summary>
    public static GameTables Load(GameId game) => Load(GameDataRepository.DefaultPathFor(game));
}

public sealed partial class GameDataRepository
{
    /// <inheritdoc cref="GameTables.Load(string)"/>
    internal static GameTables LoadTables(string dir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dir);
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"Game data directory not found: {dir}");

        var m = GameManifest.Load(RequireFile(dir, GameManifest.FileName));
        var traitsPath = Path.Combine(dir, "monster-traits.json");
        var monsters = LoadMonsters(RequireFile(dir, "monsters.json"), new Dictionary<int, AssetSpritePaths>(),
            File.Exists(traitsPath) ? LoadMonsterTraits(traitsPath) : new MonsterTraitsDto());
        var items = LoadItems(RequireFile(dir, "items.json"));
        var spells = LoadSpells(RequireFile(dir, "spells.json"), costFromSlotRule: m.SpellCostRule is not null);
        var names = LoadNames(RequireFile(dir, "names.json"));
        var rules = RulesProfileLoader.Load(RequireFile(dir, RulesProfileLoader.FileName));
        var overworldPath = RequireFile(dir, "overworld.json");
        var scrollsPath = RequireFile(dir, "scrolls.json");
        var p3Path = Path.Combine(dir, Phantasie3RuleTables.FileName);

        var tables = new GameTables
        {
            Manifest = m,
            Monsters = monsters,
            Items = items,
            Spells = spells,
            Names = names,
            // T6: a town's stock has one slot per item of the folder (IV: 157); I-III keep the 181 slots they ship.
            Towns = LoadTowns(RequireFile(dir, "towns.json"), m.Count("towns"),
                m.Game == GameId.P4 ? m.Count("items") : TownStockSlotCount),
            OverworldRegions = LoadOverworld(overworldPath,
                LoadMapPatches(Path.Combine(dir, "map-patches.json")), m.Count("overworld_regions")),
            Places = WorldExtrasLoader.LoadPlaces(overworldPath),
            TileTerrain = WorldExtrasLoader.LoadTerrain(overworldPath),
            Dungeons = LoadDungeons(RequireFile(dir, "dungeons.json"), m.Count("dungeon_levels")),
            MessageAreas = m.Count("message_areas") > 0
                ? LoadMessages(RequireFile(dir, "messages.json"), m.Count("message_areas"))
                : [],
            Scrolls = LoadScrolls(scrollsPath, m.Count("scrolls")),
            ScrollItemRecords = WorldExtrasLoader.LoadScrollItemRecords(scrollsPath),
            Phantasie3 = m.Game == GameId.P3 && File.Exists(p3Path) ? Phantasie3RuleTables.Load(p3Path) : null,
            Phantasie4 = m.Game == GameId.P4 && File.Exists(p3Path) ? Phantasie4RuleTables.Load(p3Path) : null,
            CreationTables = CreationTablesLoader.Load(RequireFile(dir, "creation-tables.json"), requireLifespans: false),
            Elementals = LoadElementals(RequireFile(dir, "elementals.json"), m.Count("spells")),
            Rules = rules,
            TravelTime = File.Exists(Path.Combine(dir, "travel-time.json"))
                ? LoadTravelTime(Path.Combine(dir, "travel-time.json"))
                : null,
        };
        ValidateTables(tables, dir);
        return tables;
    }

    /// <summary>
    /// The full repository of a non-I data folder (Phase 09 T3), built from its shared tables. Files only Phantasie I
    /// ships are optional here: no <c>asset-manifest.json</c> (sprites come from the game's graphics pack), no
    /// <c>guild.json</c> fixture (empty roster); <c>wilderness-encounters.json</c> and <c>item-descriptions.json</c> are
    /// read when present.
    /// </summary>
    internal static GameDataRepository FromTables(GameTables t, string dir)
    {
        var wild = Path.Combine(dir, "wilderness-encounters.json");
        return new GameDataRepository(
            t.Monsters, t.Items, t.Spells, t.Dungeons, t.OverworldRegions, t.MessageAreas, t.Scrolls, t.Names,
            new GuildRosterDefinition { RecordSize = 0, SlotCount = t.Rules.GuildSlotCount }, t.CreationTables, t.Towns,
            new Dictionary<int, AssetSpritePaths>(), t.Elementals)
        {
            TravelTime = t.TravelTime ?? TravelTimeRules.Dos,
            WildernessEncounters = File.Exists(wild) ? LoadWildernessEncounters(wild) : WildernessEncounterRules.Dos,
            Rules = t.Rules,
            MapTiles = null,
            ItemDescriptions = LoadItemDescriptions(Path.Combine(dir, "item-descriptions.json")),
            Game = t.Game,
            Phantasie3 = t.Phantasie3,
            Places = t.Places,
            TileTerrain = t.TileTerrain,
            ScrollItemRecords = t.ScrollItemRecords,
        };
    }

    private static void ValidateTables(GameTables t, string dir)
    {
        var m = t.Manifest;
        void Expect(string table, int actual)
        {
            if (actual != m.Count(table))
                throw new InvalidDataException($"{dir}: expected {m.Count(table)} {table}, got {actual}.");
        }

        Expect("monsters", t.Monsters.Count);
        Expect("items", t.Items.Count);
        Expect("spells", t.Spells.Count);
        Expect("classes", t.Names.Classes.Count);
        Expect("races", t.Names.Races.Count);
        Expect("elementals", t.Elementals.Count);
        if (t.Names.Spells.Count != t.Spells.Count || t.Names.Monsters.Count != t.Monsters.Count)
            throw new InvalidDataException($"{dir}: names.json spell/monster lists do not match spells.json / monsters.json.");

        // One folder, one game: every id and the rules profile name the manifest's game.
        var ids = t.Monsters.Select(x => x.ContentId).Concat(t.Items.Select(x => x.ContentId))
            .Concat(t.Spells.Select(x => x.ContentId)).Concat(t.Dungeons.Select(x => x.ContentId));
        var game = SingleGame(ids);
        if (game != m.Game || t.Rules.Game != m.Game)
            throw new InvalidDataException(
                $"{dir}: game.json says {m.Game.Prefix()}, ids say {game.Prefix()}, rules.json says {t.Rules.Game.Prefix()}.");

        for (var i = 0; i < t.Spells.Count; i++)
        {
            var s = t.Spells[i];
            if (!string.Equals(s.Name, t.Names.Spells[i].Name, StringComparison.Ordinal))
                throw new InvalidDataException($"{dir}: spell {i} is {s.Name} in spells.json, {t.Names.Spells[i].Name} in names.json.");
            if (m.SpellCostRule == GameManifest.DosSlotCostRule && s.MagicPointCost != s.DosSlot % 4 + 1)
                throw new InvalidDataException($"{dir}: spell {s.Id} ({s.Name}) costs {s.MagicPointCost}, the slot rule says otherwise.");
            if (s.MagicPointCostKnown && s.MagicPointCost is < 1 or > 4)
                throw new InvalidDataException($"{dir}: spell {s.Id} ({s.Name}) costs {s.MagicPointCost} MP (1-4 expected).");
        }

        // Cross-references stay inside the folder: monster companions/treasure and town stock point at its own tables.
        foreach (var mon in t.Monsters)
        {
            if (mon.Companions.Any(c => (uint)c >= t.Monsters.Count) || mon.TreasureItems.Any(i => (uint)i >= t.Items.Count))
                throw new InvalidDataException($"{dir}: monster {mon.Id} ({mon.Name}) points outside the folder's tables.");
        }
        foreach (var town in t.Towns)
        {
            if (town.RestockItems.Any(i => (uint)i >= t.Items.Count))
                throw new InvalidDataException($"{dir}: town {town.Name} restocks an unknown item.");
        }

        // T4: hard-coded places name a town / dungeon of the folder and sit on one of its plane maps.
        var planes = t.OverworldRegions.Where(r => r.Plane is not null).ToDictionary(r => r.Plane!);
        foreach (var p in t.Places)
        {
            var known = p.Kind switch
            {
                "town" => t.Towns.Any(x => x.Id == p.Number),
                "dungeon" => p.Number >= 1 && p.Number <= t.Dungeons.Count,
                "inn" => p.Number >= 1,
                _ => false,
            };
            if (!known || !planes.TryGetValue(p.Plane, out var map) || (uint)p.X >= map.GridWidth || (uint)p.Y >= map.GridHeight)
                throw new InvalidDataException($"{dir}: place {p.Kind} {p.Number} ({p.Name}) at {p.Plane} ({p.X}, {p.Y}) is not in the folder.");
        }
        foreach (var (item, records) in t.ScrollItemRecords)
        {
            if ((uint)item >= t.Items.Count || records.Any(r => (uint)r >= t.Scrolls.Count))
                throw new InvalidDataException($"{dir}: scroll item {item} points outside the folder's tables.");
        }
    }
}
