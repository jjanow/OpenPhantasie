using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Loads Phase 3/4 JSON tables from a GameData root (e.g. GameData/phantasie1).
/// M1: all extracted tables are required and validated on load.
/// </summary>
public sealed partial class GameDataRepository
{
    public const int ExpectedMonsterCount = 80;
    public const int ExpectedItemCount = 181;
    public const int ExpectedSpellCount = 55;
    public const int ExpectedScrollCount = 20;
    public const int ExpectedMessageAreaCount = 10;
    public const int ExpectedClassCount = 6;
    public const int ExpectedRaceCount = 15;
    public const int ExpectedOverworldRegionCount = 18;
    public const int OverworldGridWidth = 26;
    public const int OverworldGridHeight = 20;
    public const int ExpectedDungeonLevelCount = 10;
    public const int DungeonGridWidth = 33;
    public const int DungeonGridHeight = 38;
    public const int DungeonGridByteLength = DungeonGridWidth * DungeonGridHeight; // 1254
    public const int ExpectedTownCount = 12;
    public const int TownStockSlotCount = 181;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<MonsterDefinition> Monsters { get; }
    public IReadOnlyList<ItemDefinition> Items { get; }
    public IReadOnlyList<SpellDefinition> Spells { get; }
    public IReadOnlyList<DungeonLevelDefinition> Dungeons { get; }
    public IReadOnlyList<OverworldRegionDefinition> OverworldRegions { get; }
    public IReadOnlyList<MessageAreaDefinition> MessageAreas { get; }
    public IReadOnlyList<ScrollDefinition> Scrolls { get; }
    public NameTables Names { get; }
    public GuildRosterDefinition GuildRoster { get; }
    public CharacterCreationTables CreationTables { get; }
    public IReadOnlyList<TownDefinition> Towns { get; }
    public IReadOnlyDictionary<int, AssetSpritePaths> MonsterSprites { get; }

    /// <summary>`core.spell.summon_elemental` templates FIRE, WATER, EARTH (<c>elementals.json</c>).</summary>
    public IReadOnlyList<ElementalDefinition> Elementals { get; }

    /// <summary>Travel time per wilderness step (<c>travel-time.json</c>, OV2 <c>0x5f88</c>) [C].</summary>
    public TravelTimeRules TravelTime { get; private init; } = TravelTimeRules.Dos;

    /// <summary>Day-encounter monster pairs (<c>wilderness-encounters.json</c>, COM DS <c>0x1a5c</c>) [C] (s39).</summary>
    public WildernessEncounterRules WildernessEncounters { get; private init; } = WildernessEncounterRules.Dos;

    /// <summary>The game's rule constants (<c>rules.json</c>, Phase 09 T1.3: gold caps, Guild size, starting gold).</summary>
    public RulesProfile Rules { get; private init; } = RulesProfile.PhantasieI;

    /// <summary>Phantasie III's own tables (<c>rule-tables.json</c>); null for I / II.</summary>
    public Phantasie3RuleTables? Phantasie3 { get; private init; }

    /// <summary>Phantasie III's hard-coded wilderness places; empty for I / II.</summary>
    public IReadOnlyList<WorldPlaceDefinition> Places { get; private init; } = [];

    /// <summary>Phantasie III's tile terrain; null for I / II.</summary>
    public TileTerrainDefinition? TileTerrain { get; private init; }

    /// <summary>Phantasie III's scroll item → text records; empty for I / II.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<int>> ScrollItemRecords { get; private init; } =
        new Dictionary<int, IReadOnlyList<int>>();

    /// <summary>DOS map tile rules (<c>asset-manifest.json</c> → <c>map_tiles</c>, P0.7); null when the section is absent.</summary>
    public MapTileManifest? MapTiles { get; private init; }

    public const int ExpectedElementalCount = 3;

    private GameDataRepository(
        IReadOnlyList<MonsterDefinition> monsters,
        IReadOnlyList<ItemDefinition> items,
        IReadOnlyList<SpellDefinition> spells,
        IReadOnlyList<DungeonLevelDefinition> dungeons,
        IReadOnlyList<OverworldRegionDefinition> overworldRegions,
        IReadOnlyList<MessageAreaDefinition> messageAreas,
        IReadOnlyList<ScrollDefinition> scrolls,
        NameTables names,
        GuildRosterDefinition guildRoster,
        CharacterCreationTables creationTables,
        IReadOnlyList<TownDefinition> towns,
        IReadOnlyDictionary<int, AssetSpritePaths> monsterSprites,
        IReadOnlyList<ElementalDefinition> elementals)
    {
        Monsters = monsters;
        Items = items;
        Spells = spells;
        Dungeons = dungeons;
        OverworldRegions = overworldRegions;
        MessageAreas = messageAreas;
        Scrolls = scrolls;
        Names = names;
        GuildRoster = guildRoster;
        CreationTables = creationTables;
        Towns = towns;
        MonsterSprites = monsterSprites;
        Elementals = elementals;
    }

    /// <summary>
    /// Load and validate every extracted phantasie1 table from <paramref name="gameDataDirectory"/>.
    /// </summary>
    public static GameDataRepository Load(string gameDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDataDirectory);
        if (!Directory.Exists(gameDataDirectory))
            throw new DirectoryNotFoundException($"Game data directory not found: {gameDataDirectory}");

        // Phase 09 T3: a folder whose manifest names another game than I loads through the generic tables (counts from
        // game.json, I-only files optional). Phantasie I keeps its strict path below.
        var manifestPath = Path.Combine(gameDataDirectory, GameManifest.FileName);
        if (File.Exists(manifestPath) && GameManifest.Load(manifestPath).Game != GameId.P1)
            return FromTables(LoadTables(gameDataDirectory), gameDataDirectory);

        var sprites = LoadMonsterSprites(RequireFile(gameDataDirectory, "asset-manifest.json"));
        var monsters = LoadMonsters(
            RequireFile(gameDataDirectory, "monsters.json"), sprites,
            LoadMonsterTraits(RequireFile(gameDataDirectory, "monster-traits.json")));
        var items = LoadItems(RequireFile(gameDataDirectory, "items.json"));
        var spells = LoadSpells(RequireFile(gameDataDirectory, "spells.json"));
        var dungeons = LoadDungeons(RequireFile(gameDataDirectory, "dungeons.json"));
        var overworld = LoadOverworld(RequireFile(gameDataDirectory, "overworld.json"),
            LoadMapPatches(Path.Combine(gameDataDirectory, "map-patches.json")));
        var messages = LoadMessages(RequireFile(gameDataDirectory, "messages.json"));
        var scrolls = LoadScrolls(RequireFile(gameDataDirectory, "scrolls.json"));
        var names = LoadNames(RequireFile(gameDataDirectory, "names.json"));
        // guild.json is a test fixture (class f, 10-importer.md 2.2), not game data: optional, so an import without it loads.
        var guildPath = Path.Combine(gameDataDirectory, "guild.json");
        var guild = File.Exists(guildPath)
            ? LoadGuild(guildPath)
            : new GuildRosterDefinition { RecordSize = CharacterSchemaNotes.GuildRecordSize, SlotCount = CharacterSchemaNotes.GuildSlotCount };
        var creation = CreationTablesLoader.Load(RequireFile(gameDataDirectory, "creation-tables.json"));
        var towns = LoadTowns(RequireFile(gameDataDirectory, "towns.json"));
        var elementals = LoadElementals(RequireFile(gameDataDirectory, "elementals.json"));
        var travelTime = LoadTravelTime(RequireFile(gameDataDirectory, "travel-time.json"));
        var wildernessEncounters = LoadWildernessEncounters(RequireFile(gameDataDirectory, "wilderness-encounters.json"));

        ValidateCrossRefs(monsters, spells, names, items);

        return new GameDataRepository(
            monsters, items, spells, dungeons, overworld, messages, scrolls, names, guild, creation, towns, sprites,
            elementals) {
            TravelTime = travelTime, WildernessEncounters = wildernessEncounters,
            Rules = RulesProfileLoader.Load(RequireFile(gameDataDirectory, RulesProfileLoader.FileName)),
            MapTiles = MapTileManifest.Load(Path.Combine(gameDataDirectory, "asset-manifest.json")),
            ItemDescriptions = LoadItemDescriptions(Path.Combine(gameDataDirectory, "item-descriptions.json")),
            Game = SingleGame(monsters.Select(m => m.ContentId).Concat(items.Select(i => i.ContentId))
                .Concat(spells.Select(s => s.ContentId)).Concat(dungeons.Select(d => d.ContentId))),
        };
    }

    /// <summary>
    /// Resolves the shipped phantasie1 data folder next to the Data assembly output.
    /// </summary>
    public static string DefaultPhantasie1Path => GameDataLocator.GameFolder(GameId.P1);

    public static GameDataRepository LoadPhantasie1() => Load(DefaultPhantasie1Path);

    /// <summary>
    /// The data folder of <paramref name="game"/>, whether or not it exists yet: the player's import when the Client set
    /// one (<see cref="GameDataLocator"/>), else the shipped <c>GameData/phantasie1</c> ….
    /// </summary>
    public static string DefaultPathFor(GameId game) => GameDataLocator.GameFolder(game);

    /// <summary>
    /// Data shared by every game (T5.0): the remaster shell's string table (main menu, options, option labels) under
    /// <c>GameData/shared</c>. A game's own table goes over it.
    /// </summary>
    public static string SharedDataPath => Path.Combine(GameDataLocator.ShippedRoot, "shared");

    public MonsterDefinition GetMonster(int id) =>
        Monsters.FirstOrDefault(m => m.Id == id)
        ?? throw new KeyNotFoundException($"Monster id {id} not found.");

    public SpellDefinition GetSpell(int id) =>
        Spells.FirstOrDefault(s => s.Id == id)
        ?? throw new KeyNotFoundException($"Spell id {id} not found.");

    public ItemDefinition GetItem(int id) =>
        Items.FirstOrDefault(i => i.Id == id)
        ?? throw new KeyNotFoundException($"Item id {id} not found.");

    /// <summary>Lookup by overworld town id (Pelnor = 1).</summary>
    public TownDefinition GetTown(int townId) =>
        Towns.FirstOrDefault(t => t.Id == townId)
        ?? throw new KeyNotFoundException($"Town id {townId} not found.");

    public IItemCatalog CreateItemCatalog() => new ItemCatalog(Items, ItemDescriptions);

    /// <summary>U)SE ITEM texts by item id (<c>item-descriptions.json</c>, D-061); empty when the file is absent.</summary>
    public IReadOnlyDictionary<int, ItemDescription> ItemDescriptions { get; private init; } =
        new Dictionary<int, ItemDescription>();

    public TownState CreateTownState(int townId) => GetTown(townId).ToRuntime();

    /// <summary>
    /// Canonical MP cost from OV2.OVR: <c>(DOS slot % 4) + 1</c> (<see cref="Magic.MagicPointCost"/>; DIVINE I = slot 57 → 2).
    /// </summary>
    public static int SpellMagicPointCost(int spellIndex) => Magic.MagicPointCost(spellIndex);

    private static string RequireFile(string dir, string name)
    {
        var path = Path.Combine(dir, name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Required game data file missing: {name}", path);
        return path;
    }

    private static void ValidateCrossRefs(
        IReadOnlyList<MonsterDefinition> monsters,
        IReadOnlyList<SpellDefinition> spells,
        NameTables names,
        IReadOnlyList<ItemDefinition> items)
    {
        if (monsters.Count != ExpectedMonsterCount)
            throw new InvalidDataException($"Expected {ExpectedMonsterCount} monsters, got {monsters.Count}.");
        if (items.Count != ExpectedItemCount)
            throw new InvalidDataException($"Expected {ExpectedItemCount} items, got {items.Count}.");
        if (spells.Count != ExpectedSpellCount)
            throw new InvalidDataException($"Expected {ExpectedSpellCount} spells, got {spells.Count}.");
        if (names.Classes.Count != ExpectedClassCount)
            throw new InvalidDataException($"Expected {ExpectedClassCount} classes, got {names.Classes.Count}.");
        if (names.Races.Count != ExpectedRaceCount)
            throw new InvalidDataException($"Expected {ExpectedRaceCount} races, got {names.Races.Count}.");
        if (names.Spells.Count != ExpectedSpellCount)
            throw new InvalidDataException($"names.json spells count {names.Spells.Count} != {ExpectedSpellCount}.");
        if (names.Monsters.Count != ExpectedMonsterCount)
            throw new InvalidDataException($"names.json monsters count {names.Monsters.Count} != {ExpectedMonsterCount}.");

        for (var i = 0; i < spells.Count; i++)
        {
            var expected = SpellMagicPointCost(spells[i].Id);
            if (spells[i].MagicPointCost != expected)
            {
                throw new InvalidDataException(
                    $"Spell {spells[i].Id} ({spells[i].Name}) mpCost={spells[i].MagicPointCost}, expected {expected}.");
            }

            if (!string.Equals(spells[i].Name, names.Spells[i].Name, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Spell name mismatch at {i}: spells.json={spells[i].Name}, names.json={names.Spells[i].Name}.");
            }
        }
    }

    private static IReadOnlyList<MonsterDefinition> LoadMonsters(
        string path,
        IReadOnlyDictionary<int, AssetSpritePaths> sprites,
        MonsterTraitsDto traits)
    {
        var dto = JsonSerializer.Deserialize<MonstersFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");

        if (dto.Monsters is null || dto.Monsters.Count == 0)
            throw new InvalidDataException($"{path} contains no monsters.");
        var indices = dto.Monsters.Select(m => m.Index).ToHashSet();
        foreach (var id in traits.Undead.Concat(traits.NeverFlees))
        {
            if (!indices.Contains(id))
                throw new InvalidDataException($"monster-traits.json names unknown monster index {id}.");
        }

        return dto.Monsters.Select(m =>
        {
            sprites.TryGetValue(m.Index, out var sprite);
            return new MonsterDefinition(
                Id: m.Index,
                Name: m.Name ?? $"MONSTER_{m.Index}",
                Armor: m.Armor,
                Damage: m.Damage,
                Level: m.Level,
                Experience: m.Xp,
                SpecialChance: m.SpecialChance,
                Specials: m.Specials ?? [],
                HpBase: m.HpBase,
                CombatRating: m.CombatRating,
                Companions: m.Companions ?? [],
                ReactionModifier: m.ReactionModifier,
                NextGroupChance: m.NextGroupChance,
                Defense: m.Defense,
                Gold: m.Gold,
                GroupSizeFactor: m.GroupSizeFactor,
                TreasureItems: m.TreasureItems ?? [],
                AttackPower: m.AttackPower,
                SpriteWidth: m.SpriteWidth,
                SpriteHeight: m.SpriteHeight,
                Undead: traits.Undead.Contains(m.Index),
                NeverFlees: traits.NeverFlees.Contains(m.Index),
                SpriteIdlePath: sprite?.Idle,
                SpriteAttackingPath: sprite?.Attacking)
            {
                ContentId = ParseRecordId(m.Id, m.Index, "monster", path),
                GroupMax = m.GroupMax,
                Byte27 = m.Byte27,
                Byte33 = m.Byte33,
            };
        }).ToList();
    }

    private static IReadOnlyList<ItemDefinition> LoadItems(string path)
    {
        var dto = JsonSerializer.Deserialize<ItemsFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");
        if (dto.Items is null || dto.Items.Count == 0)
            throw new InvalidDataException($"{path} contains no items.");

        return dto.Items
            .Select(i => new ItemDefinition(
                i.Index,
                i.Name ?? $"ITEM_{i.Index}",
                i.ValueFactor,
                i.Kind ?? "unknown",
                i.Points,
                i.MinStr,
                i.StrCheck)
            {
                ContentId = ParseRecordId(i.Id, i.Index, "item", path),
                Value = i.Value,
            })
            .ToList();
    }

    private static IReadOnlyList<TownDefinition> LoadTowns(
        string path, int expectedCount = ExpectedTownCount, int stockSlots = TownStockSlotCount)
    {
        var dto = JsonSerializer.Deserialize<TownsFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");
        if (dto.Towns is null || dto.Towns.Count == 0)
            throw new InvalidDataException($"{path} contains no towns.");

        var towns = dto.Towns.Select(t =>
        {
            var stock = t.Stock ?? [];
            if (stock.Count < stockSlots)
            {
                throw new InvalidDataException(
                    $"Town {t.Name} stock has {stock.Count} slots; expected {stockSlots}.");
            }

            return new TownDefinition
            {
                Id = t.Id,
                Index = t.Index,
                Name = t.Name ?? $"TOWN_{t.Id}",
                PriceIndex = t.PriceIndex,
                Stock = stock.Take(stockSlots).ToList(),
                RestockItems = t.RestockItems ?? [],
                DosRecord = t.DosRecord is null ? null : Convert.FromHexString(t.DosRecord) is { Length: TownsCodec.RecordSize } rec
                    ? rec
                    : throw new InvalidDataException($"Town {t.Name} dos_record must be {TownsCodec.RecordSize} bytes."),
                Plane = t.Plane,
                TransportDestinations = t.TransportDestinations,
                Exit = t.DosRecord is null && t.ExitRegion is int er && t.ExitX is int ex && t.ExitY is int ey
                    ? (er, ex, ey)
                    : null,
            };
        }).ToList();

        if (towns.Count != expectedCount)
        {
            throw new InvalidDataException(
                $"Expected {expectedCount} towns, got {towns.Count}.");
        }

        return towns;
    }

    private static IReadOnlyList<SpellDefinition> LoadSpells(string path, bool costFromSlotRule = true)
    {
        var dto = JsonSerializer.Deserialize<SpellsFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");
        if (dto.Spells is null || dto.Spells.Count == 0)
            throw new InvalidDataException($"{path} contains no spells.");

        return dto.Spells
            .Select(s => new SpellDefinition(
                s.Index,
                s.Name ?? $"SPELL_{s.Index}",
                s.Category ?? "?",
                s.EffectSummary,
                s.MagicPointCost ?? (costFromSlotRule ? SpellMagicPointCost(s.Index) : 0),
                s.EffectCoefficientsNote,
                s.DosSlot ?? s.Index)
            {
                ContentId = ParseRecordId(s.Id, s.Index, "spell", path),
                MagicPointCostKnown = s.MagicPointCost is not null || costFromSlotRule,
            })
            .ToList();
    }

    private static IReadOnlyList<DungeonLevelDefinition> LoadDungeons(string path, int expectedCount = ExpectedDungeonLevelCount)
    {
        var dto = JsonSerializer.Deserialize<DungeonsFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");
        if (dto.Levels is null || dto.Levels.Count == 0)
            throw new InvalidDataException($"{path} contains no dungeon levels.");

        var levels = new List<DungeonLevelDefinition>();
        foreach (var (id, level) in dto.Levels)
        {
            byte[] raw = Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace(level.RawHex))
            {
                try
                {
                    raw = Convert.FromHexString(level.RawHex);
                }
                catch (FormatException ex)
                {
                    throw new InvalidDataException($"Invalid raw_hex for dungeon {id}", ex);
                }
            }

            // T4: a level may state its own grid (Phantasie III: 24x30 at the start of a 4224-byte DNGS record);
            // without one it is I's 33x38.
            var gw = level.GridWidth ?? DungeonGridWidth;
            var gh = level.GridHeight ?? DungeonGridHeight;
            if (gw <= 0 || gh <= 0)
                throw new InvalidDataException($"Dungeon {id}: bad grid {gw}x{gh}.");
            if (raw.Length < gw * gh)
            {
                throw new InvalidDataException(
                    $"Dungeon {id} is {raw.Length} bytes; expected at least {gw * gh} for the grid region.");
            }

            var cells = new Grid<DungeonCell>(gw, gh, new DungeonCell(0));
            for (var y = 0; y < gh; y++)
            for (var x = 0; x < gw; x++)
                cells[x, y] = new DungeonCell(raw[y * gw + x]);

            levels.Add(new DungeonLevelDefinition
            {
                Id = id,
                ContentId = ParseRecordId(level.Id, DungeonLevelNumber(id), "dungeon", path),
                ByteSize = level.Size > 0 ? level.Size : raw.Length,
                RawFeatureBytes = raw,
                GridByteLength = gw * gh,
                GridWidth = gw,
                GridHeight = gh,
                Cells = cells,
                Name = level.Name,
                Tables = level.Tables?.ToDefinition(id),
                EmbeddedTexts = (level.EmbeddedText ?? [])
                    .Select(t => new DungeonEmbeddedText(t.Offset ?? "", t.Text ?? ""))
                    .ToList(),
                Messages = (level.Messages ?? new Dictionary<string, DungeonMessageDto>())
                    .ToDictionary(
                        kv => int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture),
                        kv => new DungeonMessage(kv.Value.Lines ?? [], kv.Value.Options ?? [])
                        {
                            ContinuationOf = kv.Value.ContinuationOf,
                            OptionCells = kv.Value.OptionCells,
                        }),
            });
        }

        if (levels.Count != expectedCount)
        {
            throw new InvalidDataException(
                $"Expected {expectedCount} dungeon levels, got {levels.Count}.");
        }

        return levels;
    }

    /// <summary>A remaster map decision (<c>map-patches.json</c>): overworld cell (<paramref name="X"/>, <paramref name="Y"/>) of
    /// region <paramref name="Region"/> shows visible tile <paramref name="Tile"/> instead of the DOS one.</summary>
    public sealed record MapPatch(int Region, int X, int Y, int Tile);

    private static IReadOnlyDictionary<int, ItemDescription> LoadItemDescriptions(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<int, ItemDescription>();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("items").EnumerateObject().ToDictionary(
            p => int.Parse(p.Name),
            p => new ItemDescription(p.Value.GetProperty("full_name").GetString() ?? "",
                p.Value.GetProperty("text").GetString() ?? ""));
    }

    private static IReadOnlyList<MapPatch> LoadMapPatches(string path)
    {
        if (!File.Exists(path))
            return [];
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.TryGetProperty("overworld", out var list)
            ? list.EnumerateArray().Select(p => new MapPatch(
                p.GetProperty("region").GetInt32(), p.GetProperty("x").GetInt32(), p.GetProperty("y").GetInt32(),
                p.GetProperty("tile").GetInt32())).ToList()
            : [];
    }

    private static IReadOnlyList<OverworldRegionDefinition> LoadOverworld(
        string path, IReadOnlyList<MapPatch> patches, int expectedCount = ExpectedOverworldRegionCount)
    {
        var dto = JsonSerializer.Deserialize<OverworldFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");
        if (dto.Regions is null || dto.Regions.Count == 0)
            throw new InvalidDataException($"{path} contains no overworld regions.");

        var regions = new List<OverworldRegionDefinition>();
        foreach (var (id, region) in dto.Regions)
        {
            var width = region.GridWidth > 0 ? region.GridWidth : OverworldGridWidth;
            var height = region.GridHeight > 0 ? region.GridHeight : OverworldGridHeight;
            var tiles = (region.Tiles ?? [])
                .Select(row => (IReadOnlyList<int>)(row ?? []).ToList())
                .ToList();

            // T4: the grid size is the data's (I / II: 26x20 regions; III: one map per plane, 50x75 / 9x9 / 31x16).
            if (tiles.Count != height || tiles.Any(r => r.Count != width))
                throw new InvalidDataException($"Overworld {id}: tile grid must be {width}x{height}.");

            var regionId = region.RegionId;
            if (regionId == 0 && region.Plane is null)
            {
                var digits = new string(id.Where(char.IsDigit).ToArray());
                regionId = int.Parse(digits);
            }

            foreach (var patch in patches.Where(p => p.Region == regionId))
            {
                var row = (List<int>)tiles[patch.Y];
                row[patch.X] = patch.Tile + (row[patch.X] >= OverworldRegion.FogOffset ? OverworldRegion.FogOffset : 0);
            }

            regions.Add(new OverworldRegionDefinition
            {
                Id = id,
                RegionId = regionId,
                ByteSize = region.Size,
                GridWidth = width,
                GridHeight = height,
                Tiles = tiles,
                Plane = region.Plane,
                Arrival = region.Arrival is { } a ? (a.X, a.Y) : null,
                Features = (region.Features ?? [])
                    .Select(f => new OverworldFeatureDefinition(f.Index, f.X, f.Y, f.Arg, f.DestX, f.DestY))
                    .ToList(),
                TextPages = (region.TextPages ?? [])
                    .Select(p => new OverworldTextPage(p.Index, p.Text ?? "") { Raw = p.Raw })
                    .ToList(),
                EmbeddedTexts = (region.EmbeddedText ?? [])
                    .Select(t => new DungeonEmbeddedText(t.Offset ?? "", t.Text ?? ""))
                    .ToList(),
            });
        }

        if (regions.Count != expectedCount)
        {
            throw new InvalidDataException(
                $"Expected {expectedCount} overworld regions, got {regions.Count}.");
        }

        return regions;
    }

    private static IReadOnlyList<MessageAreaDefinition> LoadMessages(string path, int expectedCount = ExpectedMessageAreaCount)
    {
        var dto = JsonSerializer.Deserialize<MessagesFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");
        if (dto.Areas is null || dto.Areas.Count == 0)
            throw new InvalidDataException($"{path} contains no message areas.");

        var areas = dto.Areas.Select(a => new MessageAreaDefinition
        {
            Id = a.Source ?? (a.Id is int n ? $"MESS{n}" : "MESS?"),
            Source = a.Source,
            Size = a.Size,
            SlotCount = a.SlotCount,
            RecordCount = a.RecordCount,
            Records = (a.Records ?? [])
                .Select(r => r?.Text ?? "")
                .Where(t => t.Length > 0)
                .ToList(),
        }).ToList();

        if (areas.Count != expectedCount)
        {
            throw new InvalidDataException(
                $"Expected {expectedCount} message areas, got {areas.Count}.");
        }

        return areas;
    }

    private static IReadOnlyList<ScrollDefinition> LoadScrolls(string path, int expectedCount = ExpectedScrollCount)
    {
        var dto = JsonSerializer.Deserialize<ScrollsFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");
        if (dto.Scrolls is null || dto.Scrolls.Count == 0)
            throw new InvalidDataException($"{path} contains no scrolls.");

        var scrolls = dto.Scrolls.Select(s => new ScrollDefinition
        {
            Id = s.Id,
            Step = s.Step,
            Text = s.Text ?? "",
            Lines = (s.Lines ?? []).Select(l => l ?? "").ToList(),
        }).ToList();

        if (scrolls.Count != expectedCount)
        {
            throw new InvalidDataException(
                $"Expected {expectedCount} scrolls, got {scrolls.Count}.");
        }

        return scrolls;
    }

    private static NameTables LoadNames(string path)
    {
        var dto = JsonSerializer.Deserialize<NamesFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");

        return new NameTables
        {
            Classes = MapNamed(dto.Classes),
            Races = MapNamed(dto.Races),
            Spells = MapNamed(dto.Spells),
            SpellLevelNames = MapNamed(dto.SpellLevelNames),
            Items = MapNamed(dto.Items),
            Monsters = MapNamed(dto.Monsters),
            TownLabels = MapNamed(dto.TownLabels),
            Skills = MapNamed(dto.Skills),
            SocialClasses = MapNamed(dto.SocialClasses),
            Origins = MapNamed(dto.Origins),
            BodyAreas = MapNamed(dto.BodyAreas),
            BodyStates = MapNamed(dto.BodyStates),
        };
    }

    private static IReadOnlyList<NamedIndex> MapNamed(List<NamedDto>? list) =>
        (list ?? [])
            .Select(n => new NamedIndex(n.Index, n.Name ?? $"NAME_{n.Index}"))
            .ToList();

    private static GuildRosterDefinition LoadGuild(string path)
    {
        var dto = JsonSerializer.Deserialize<GuildFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");

        if (dto.RecordSize != CharacterSchemaNotes.GuildRecordSize)
        {
            throw new InvalidDataException(
                $"GUILD record_size {dto.RecordSize} != expected {CharacterSchemaNotes.GuildRecordSize}.");
        }

        if (dto.Slots != CharacterSchemaNotes.GuildSlotCount)
        {
            throw new InvalidDataException(
                $"GUILD slots {dto.Slots} != expected {CharacterSchemaNotes.GuildSlotCount}.");
        }

        var characters = (dto.Characters ?? []).Select(c => new GuildCharacterSnapshot
        {
            Name = c.Name ?? "",
            Race = c.DisplayedRace ?? c.Race,
            BirthRace = c.Race,
            Class = c.Class,
            Status = c.Status,
            Level = c.Level,
            Strength = c.Strength,
            Intelligence = c.Intelligence,
            Dexterity = c.Dexterity,
            Constitution = c.Constitution,
            Charisma = c.Charisma,
            Luck = c.Luck,
            MagicCur = c.MagicCur,
            MagicMax = c.MagicMax,
            MpCeiling = c.MpCeiling,
            Word12 = c.Word12,
            SkillBonuses = c.SkillBonus,
            CastSinceRest = c.CastSinceRest != 0,
            LifespanDos = c.LifespanDos,
            CurrentTown = c.CurrentTown,
            HasAged = c.HasAged != 0,
            AgeClock = c.AgeClock,
            HpMax = c.HpMax,
            HpCur = c.HpCur,
            GoldInBank = c.GoldInBank,
            Experience = c.Experience,
            WeaponDamageA = c.WeaponDmgA,
            ScrollOrWeaponHit = c.ScrollOrWeaponHit,
            Inventory = c.Inventory,
            WornShield = c.WornShield,
            WornArmor = c.WornArmor,
            WornWeapon = c.WornWeapon,
            Skills = c.Skills is null
                ? null
                : new GuildSkillsSnapshot
                {
                    Attack = c.Skills.Attack,
                    Parry = c.Skills.Parry,
                    FindItem = c.Skills.FindItem,
                    SpotTrap = c.Skills.SpotTrap,
                    DisarmTrap = c.Skills.DisarmTrap,
                    Listen = c.Skills.Listen,
                    PickLock = c.Skills.PickLock,
                    Swim = c.Skills.Swim,
                },
            Score = c.Score,
            Slot = c.Slot,
        }).ToList();

        if (characters.Count == 0)
            throw new InvalidDataException($"{path} contains no guild characters.");

        return new GuildRosterDefinition
        {
            RecordSize = dto.RecordSize,
            SlotCount = dto.Slots,
            Characters = characters,
        };
    }

    private static IReadOnlyDictionary<int, AssetSpritePaths> LoadMonsterSprites(string path)
    {
        var dto = JsonSerializer.Deserialize<AssetManifestDto>(File.ReadAllText(path), JsonOptions);
        var map = new Dictionary<int, AssetSpritePaths>();
        foreach (var entry in dto?.Assets ?? [])
        {
            if (!string.Equals(entry.Kind, "monster", StringComparison.OrdinalIgnoreCase))
                continue;

            var id = entry.DataId ?? entry.Id;
            var paths = entry.Sprites ?? entry.Paths;
            if (id is null || paths is null)
                continue;

            map[id.Value] = new AssetSpritePaths(paths.Idle, paths.Attacking);
        }

        return map;
    }

    // --- DTOs matching Phase 3/4 JSON shapes ---

    private sealed class MonstersFileDto
    {
        public List<MonsterDto>? Monsters { get; set; }
    }

    /// <summary>Remaster rule data keyed by monster index (D-021, D-024).</summary>
    private sealed class MonsterTraitsDto
    {
        public List<int> Undead { get; set; } = [];

        [JsonPropertyName("never_flees")]
        public List<int> NeverFlees { get; set; } = [];
    }

    private sealed class ElementalsFileDto
    {
        public List<ElementalDefinition> Elementals { get; set; } = [];
    }

    private static TravelTimeRules LoadTravelTime(string path)
    {
        var dto = JsonSerializer.Deserialize<TravelTimeFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");
        if (dto.StepCostByTerrainClass is not { Count: > 0 } costs || costs.Any(c => c <= 0) || dto.StepCostOther <= 0)
            throw new InvalidDataException($"{path}: step costs must be positive.");
        return new TravelTimeRules(costs, dto.StepCostOther, dto.WildernessInnDos);
    }

    private static WildernessEncounterRules LoadWildernessEncounters(string path)
    {
        var dto = JsonSerializer.Deserialize<WildernessEncountersFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");
        if (dto.PairTable is not { Count: 16 } table || table.Any(v => v is < 0 or > 5))
            throw new InvalidDataException($"{path}: pair_table must hold 16 values 0..5.");
        return new WildernessEncounterRules(table);
    }

    private sealed class WildernessEncountersFileDto
    {
        [JsonPropertyName("pair_table")]
        public List<int>? PairTable { get; set; }
    }

    private sealed class TravelTimeFileDto
    {
        [JsonPropertyName("step_cost_by_terrain_class")]
        public List<int>? StepCostByTerrainClass { get; set; }

        [JsonPropertyName("step_cost_other")]
        public int StepCostOther { get; set; }

        [JsonPropertyName("wilderness_inn_dos")]
        public int WildernessInnDos { get; set; }
    }

    private static IReadOnlyList<ElementalDefinition> LoadElementals(string path, int spellCount = ExpectedSpellCount)
    {
        var dto = JsonSerializer.Deserialize<ElementalsFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");
        if (dto.Elementals.Count != ExpectedElementalCount)
            throw new InvalidDataException($"Expected {ExpectedElementalCount} elementals, got {dto.Elementals.Count}.");
        foreach (var e in dto.Elementals)
        {
            if (e.KnownSpells.Any(k => (uint)k >= spellCount))
                throw new InvalidDataException($"elementals.json: {e.Name} knows an unknown spell slot.");
        }
        return dto.Elementals;
    }

    private static MonsterTraitsDto LoadMonsterTraits(string path) =>
        JsonSerializer.Deserialize<MonsterTraitsDto>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"Failed to parse {path}");

    private sealed class MonsterDto
    {
        /// <summary>Game-scoped id, <c>p1:</c> + index (T1.2).</summary>
        public string? Id { get; set; }

        public int Index { get; set; }
        public string? Name { get; set; }

        public int Armor { get; set; }
        public int Damage { get; set; }
        public int Level { get; set; }
        public long Xp { get; set; }

        [JsonPropertyName("special_chance")]
        public int SpecialChance { get; set; }

        public List<int>? Specials { get; set; }

        [JsonPropertyName("hp_base")]
        public int HpBase { get; set; }

        [JsonPropertyName("combat_rating")]
        public int CombatRating { get; set; }

        public List<int>? Companions { get; set; }

        [JsonPropertyName("reaction_modifier")]
        public int ReactionModifier { get; set; }

        [JsonPropertyName("next_group_chance")]
        public int NextGroupChance { get; set; }

        public int Defense { get; set; }
        public long Gold { get; set; }

        [JsonPropertyName("group_size_factor")]
        public int GroupSizeFactor { get; set; }

        [JsonPropertyName("treasure_items")]
        public List<int>? TreasureItems { get; set; }

        [JsonPropertyName("attack_power")]
        public int AttackPower { get; set; }

        [JsonPropertyName("sprite_width")]
        public int SpriteWidth { get; set; }

        [JsonPropertyName("sprite_height")]
        public int SpriteHeight { get; set; }

        [JsonPropertyName("group_max")]
        public int? GroupMax { get; set; }

        [JsonPropertyName("byte_27")]
        public int? Byte27 { get; set; }

        [JsonPropertyName("byte_33")]
        public int? Byte33 { get; set; }
    }

    private sealed class ItemsFileDto
    {
        public List<ItemDto>? Items { get; set; }
    }

    private sealed class ItemDto
    {
        /// <summary>Game-scoped id, <c>p1:</c> + index (T1.2).</summary>
        public string? Id { get; set; }

        public int Index { get; set; }
        public string? Name { get; set; }

        [JsonPropertyName("value_factor")]
        public int ValueFactor { get; set; }

        public string? Kind { get; set; }
        public int? Points { get; set; }

        [JsonPropertyName("min_str")]
        public int? MinStr { get; set; }

        [JsonPropertyName("str_check")]
        public string? StrCheck { get; set; }

        /// <summary>T4 (III): the DOS value (price = value · town price index / 100); absent in I / II.</summary>
        public int? Value { get; set; }
    }

    private sealed class TownsFileDto
    {
        public List<TownDto>? Towns { get; set; }
    }

    private sealed class TownDto
    {
        public int Id { get; set; }
        public int Index { get; set; }
        public string? Name { get; set; }

        [JsonPropertyName("price_index")]
        public int PriceIndex { get; set; }

        public List<int>? Stock { get; set; }

        /// <summary>TWNS.DAT <c>+0xb5</c>: 45 item ids the town-entry restock (OV1 <c>0x8e1</c>) draws from.</summary>
        [JsonPropertyName("restock_items")]
        public List<int>? RestockItems { get; set; }

        /// <summary>P1.4: the whole TWNS.DAT record as hex (carried for DOS export).</summary>
        [JsonPropertyName("dos_record")]
        public string? DosRecord { get; set; }

        /// <summary>Phantasie II: <c>"ferronrah"</c> / <c>"netherworld"</c> (ST towns.md §3); absent for I.</summary>
        public string? Plane { get; set; }

        /// <summary>Phantasie II: TRANSPORTATION's town list from this town (ST towns.md §4); absent for I.</summary>
        [JsonPropertyName("transport_destinations")]
        public List<int>? TransportDestinations { get; set; }

        /// <summary>Phantasie II: where town Exit puts the party (TWNS.DAT +274..+278, ST towns.md §2); I reads its dos_record.</summary>
        [JsonPropertyName("exit_region")]
        public int? ExitRegion { get; set; }

        [JsonPropertyName("exit_x")]
        public int? ExitX { get; set; }

        [JsonPropertyName("exit_y")]
        public int? ExitY { get; set; }
    }

    private sealed class SpellsFileDto
    {
        public List<SpellDto>? Spells { get; set; }
    }

    private sealed class SpellDto
    {
        /// <summary>Game-scoped id, <c>p1:</c> + index (T1.2).</summary>
        public string? Id { get; set; }

        public int Index { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }

        [JsonPropertyName("effect_summary")]
        public string? EffectSummary { get; set; }

        [JsonPropertyName("magic_point_cost")]
        public int? MagicPointCost { get; set; }

        [JsonPropertyName("effect_coefficients_note")]
        public string? EffectCoefficientsNote { get; set; }

        [JsonPropertyName("dos_slot")]
        public int? DosSlot { get; set; }
    }

    private sealed class DungeonsFileDto
    {
        public Dictionary<string, DungeonLevelDto>? Levels { get; set; }
    }

    private sealed class DungeonLevelDto
    {
        /// <summary>Game-scoped id, <c>p1:</c> + level number (T1.2).</summary>
        public string? Id { get; set; }

        public int Size { get; set; }

        [JsonPropertyName("raw_hex")]
        public string? RawHex { get; set; }

        [JsonPropertyName("embedded_text")]
        public List<EmbeddedTextDto>? EmbeddedText { get; set; }

        /// <summary>MESSn text + menus as OV2 0x2a95 shows them (tools/scripts/extract_dungeon_events.py, s36).</summary>
        public Dictionary<string, DungeonMessageDto>? Messages { get; set; }

        /// <summary>T4: the level's own grid size (Phantasie III 24×30); absent = I's 33×38.</summary>
        [JsonPropertyName("grid_width")]
        public int? GridWidth { get; set; }

        [JsonPropertyName("grid_height")]
        public int? GridHeight { get; set; }

        /// <summary>T4 (III): the place name of the dungeon.</summary>
        public string? Name { get; set; }

        /// <summary>T4 (III): the DNGS record's side tables.</summary>
        public DungeonRecordTablesDto? Tables { get; set; }
    }

    private sealed class DungeonMessageDto
    {
        public List<string>? Lines { get; set; }
        public List<string>? Options { get; set; }

        /// <summary>s153: the option labels as stored (blanks kept).</summary>
        [System.Text.Json.Serialization.JsonPropertyName("option_cells")]
        public List<string>? OptionCells { get; set; }

        /// <summary>s41: this slot is a later line of message N (never asked for by DNG1..10).</summary>
        [System.Text.Json.Serialization.JsonPropertyName("continuation_of")]
        public int? ContinuationOf { get; set; }
    }

    private sealed class EmbeddedTextDto
    {
        public string? Offset { get; set; }
        public string? Text { get; set; }
    }

    private sealed class OverworldFileDto
    {
        public Dictionary<string, OverworldRegionDto>? Regions { get; set; }
    }

    private sealed class OverworldRegionDto
    {
        public int Size { get; set; }

        [JsonPropertyName("region_id")]
        public int RegionId { get; set; }

        [JsonPropertyName("grid_width")]
        public int GridWidth { get; set; }

        [JsonPropertyName("grid_height")]
        public int GridHeight { get; set; }

        public List<List<int>>? Tiles { get; set; }
        public List<OverworldFeatureDto>? Features { get; set; }

        [JsonPropertyName("text_pages")]
        public List<OverworldTextPageDto>? TextPages { get; set; }

        [JsonPropertyName("embedded_text")]
        public List<EmbeddedTextDto>? EmbeddedText { get; set; }

        /// <summary>T4 (III): the plane this map is (<c>scandor</c>, <c>light</c>, <c>dark</c>, <c>netherworld</c>).</summary>
        public string? Plane { get; set; }

        /// <summary>T4 (III): where a party arriving from another plane lands.</summary>
        public SquareDto? Arrival { get; set; }
    }

    internal sealed class SquareDto
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    private sealed class OverworldFeatureDto
    {
        public int Index { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Arg { get; set; }

        [JsonPropertyName("dest_x")]
        public int DestX { get; set; }

        [JsonPropertyName("dest_y")]
        public int DestY { get; set; }
    }

    private sealed class OverworldTextPageDto
    {
        public int Index { get; set; }
        public string? Text { get; set; }
        public string? Raw { get; set; }
    }

    private sealed class MessagesFileDto
    {
        public List<MessageAreaDto>? Areas { get; set; }
    }

    private sealed class MessageAreaDto
    {
        public int? Id { get; set; }
        public string? Source { get; set; }
        public int Size { get; set; }

        [JsonPropertyName("slot_count")]
        public int SlotCount { get; set; }

        [JsonPropertyName("record_count")]
        public int RecordCount { get; set; }

        public List<MessageRecordDto>? Records { get; set; }
    }

    private sealed class MessageRecordDto
    {
        public string? Text { get; set; }
    }

    private sealed class ScrollsFileDto
    {
        public List<ScrollDto>? Scrolls { get; set; }
    }

    private sealed class ScrollDto
    {
        public int Id { get; set; }
        public int Step { get; set; }
        public string? Text { get; set; }
        public List<string?>? Lines { get; set; }
    }

    private sealed class NamesFileDto
    {
        public List<NamedDto>? Classes { get; set; }
        public List<NamedDto>? Races { get; set; }
        public List<NamedDto>? Spells { get; set; }

        [JsonPropertyName("spell_level_names")]
        public List<NamedDto>? SpellLevelNames { get; set; }

        public List<NamedDto>? Items { get; set; }
        public List<NamedDto>? Monsters { get; set; }

        [JsonPropertyName("town_labels")]
        public List<NamedDto>? TownLabels { get; set; }

        public List<NamedDto>? Skills { get; set; }

        [JsonPropertyName("social_classes")]
        public List<NamedDto>? SocialClasses { get; set; }

        public List<NamedDto>? Origins { get; set; }

        [JsonPropertyName("body_areas")]
        public List<NamedDto>? BodyAreas { get; set; }

        [JsonPropertyName("body_states")]
        public List<NamedDto>? BodyStates { get; set; }
    }

    private sealed class NamedDto
    {
        public int Index { get; set; }
        public string? Name { get; set; }
    }

    private sealed class GuildFileDto
    {
        [JsonPropertyName("record_size")]
        public int RecordSize { get; set; }

        public int Slots { get; set; }
        public List<GuildCharDto>? Characters { get; set; }
    }

    private sealed class GuildCharDto
    {
        public string? Name { get; set; }
        public string? Race { get; set; }
        [JsonPropertyName("displayed_race")]
        public string? DisplayedRace { get; set; }
        public string? Class { get; set; }
        public string? Status { get; set; }
        public int Level { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Charisma { get; set; }
        public int Luck { get; set; }

        [JsonPropertyName("magic_cur")]
        public int MagicCur { get; set; }

        [JsonPropertyName("mp_ceiling")]
        public int MpCeiling { get; set; }

        [JsonPropertyName("word_12")]
        public int Word12 { get; set; }

        [JsonPropertyName("skill_bonus")]
        public int[]? SkillBonus { get; set; }

        [JsonPropertyName("cast_since_rest")]
        public int CastSinceRest { get; set; }

        [JsonPropertyName("lifespan_dos")]
        public int LifespanDos { get; set; }

        [JsonPropertyName("current_town")]
        public int CurrentTown { get; set; }

        [JsonPropertyName("has_aged")]
        public int HasAged { get; set; }

        [JsonPropertyName("age_clock")]
        public int AgeClock { get; set; }

        [JsonPropertyName("magic_max")]
        public int MagicMax { get; set; }

        [JsonPropertyName("hp_max")]
        public int HpMax { get; set; }

        [JsonPropertyName("hp_cur")]
        public int HpCur { get; set; }

        [JsonPropertyName("gold_in_bank")]
        public int GoldInBank { get; set; }

        public int Experience { get; set; }

        [JsonPropertyName("weapon_dmg_a")]
        public int? WeaponDmgA { get; set; }

        /// <summary>Pre-s19 key for @28 — read only.</summary>
        [JsonPropertyName("scroll_or_weapon_hit")]
        public int? ScrollOrWeaponHit { get; set; }

        [JsonPropertyName("inventory")]
        public int[]? Inventory { get; set; }

        [JsonPropertyName("worn_shield")]
        public int WornShield { get; set; }

        [JsonPropertyName("worn_armor")]
        public int WornArmor { get; set; }

        [JsonPropertyName("worn_weapon")]
        public int WornWeapon { get; set; }

        public GuildSkillsDto? Skills { get; set; }
        public int Score { get; set; }
        public int Slot { get; set; }
    }

    private sealed class GuildSkillsDto
    {
        public int Attack { get; set; }
        public int Parry { get; set; }

        [JsonPropertyName("find_item")]
        public int FindItem { get; set; }

        [JsonPropertyName("spot_trap")]
        public int SpotTrap { get; set; }

        [JsonPropertyName("disarm_trap")]
        public int DisarmTrap { get; set; }

        public int Listen { get; set; }

        [JsonPropertyName("pick_lock")]
        public int PickLock { get; set; }

        public int Swim { get; set; }
    }

    private sealed class AssetManifestDto
    {
        public List<AssetEntryDto>? Assets { get; set; }
    }

    private sealed class AssetEntryDto
    {
        public string? Kind { get; set; }
        public int? Id { get; set; }

        [JsonPropertyName("data_id")]
        public int? DataId { get; set; }

        public AssetPathsDto? Paths { get; set; }
        public AssetPathsDto? Sprites { get; set; }
    }

    private sealed class AssetPathsDto
    {
        public string? Idle { get; set; }
        public string? Attacking { get; set; }
    }
}

public record AssetSpritePaths(string? Idle, string? Attacking);
