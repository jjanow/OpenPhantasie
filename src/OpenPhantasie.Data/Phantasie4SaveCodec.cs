using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Phantasie IV's remaster save (<see cref="P4CampaignSnapshot"/>, T6.3): JSON with <c>"game": "p4"</c> at the root like
/// <see cref="SessionCodec"/>'s files, and item, dungeon-map and monster (<c>DATM</c> encounter) ids game-prefixed
/// (<c>"p4:42"</c>, empty item slots null; T1.2). Town and outdoor map numbers stay bare, like I's town ids. IV's record
/// is not I's, so the file has its own layout and version; <see cref="IsPhantasie4Save"/> tells the two apart.
/// </summary>
public static class Phantasie4SaveCodec
{
    public const int CurrentVersion = 4; // 2 (s97): outdoor and town loops; 3 (s102): guild roster, shop, record bytes;
                                         // 4 (s106): ancestors, Hall descendant counts

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new ContentIdJsonConverter() },
    };

    public static string WriteJson(P4CampaignSnapshot snap)
    {
        ArgumentNullException.ThrowIfNull(snap);
        var dto = new SaveDto
        {
            Version = CurrentVersion,
            Game = GameId.P4.Prefix(),
            Options = snap.Options,
            Party = PartyDto.From(snap.Party),
            Place = snap.Place,
            Map = snap.Map,
            X = snap.X,
            Y = snap.Y,
            Facing = snap.Facing,
            Dungeon = DungeonToJson(snap.Dungeon),
            Started = snap.Started,
            Outdoor = snap.Outdoor,
            Town = snap.Town,
        };
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>Writes the save, making its folder first (a fresh install has none until something is saved; s135).</summary>
    public static void WriteJsonFile(string path, P4CampaignSnapshot snap)
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } dir)
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, WriteJson(snap));
    }

    /// <summary>True when <paramref name="json"/> is a remaster save of Phantasie IV (its root names <c>p4</c>).</summary>
    public static bool IsPhantasie4Save(string json)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject o && o["game"] is JsonValue g && g.TryGetValue<string>(out var t)
                && t == GameId.P4.Prefix();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <exception cref="InvalidDataException">Not an IV save, a newer version, or unreadable.</exception>
    public static P4CampaignSnapshot ReadJson(string json)
    {
        if (!IsPhantasie4Save(json))
            throw new InvalidDataException("This file isn't a Phantasie IV remaster save.");
        try
        {
            var dto = JsonSerializer.Deserialize<SaveDto>(json, JsonOptions)
                ?? throw new InvalidDataException("The save file is empty.");
            if (dto.Version != CurrentVersion)
                throw new InvalidDataException($"The save's version {dto.Version} is not {CurrentVersion}.");
            return new P4CampaignSnapshot(
                dto.Options ?? throw new InvalidDataException("The save has no rules."),
                (dto.Party ?? throw new InvalidDataException("The save has no party.")).ToSnapshot(),
                dto.Place, dto.Map, dto.X, dto.Y, dto.Facing, DungeonFromJson(dto.Dungeon), dto.Started, dto.Outdoor, dto.Town);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The save file can't be read: {ex.Message}", ex);
        }
    }

    public static P4CampaignSnapshot ReadJsonFile(string path) => ReadJson(File.ReadAllText(path));

    private static ContentId?[] ToIds(int[] items) =>
        [.. items.Select(i => i == 0 ? (ContentId?)null : new ContentId(GameId.P4, i))];

    private static int[] ToLocals(ContentId?[]? ids) =>
        ids?.Select(i => i is { } id ? id.LocalIn(GameId.P4) : 0).ToArray() ?? [];

    private sealed class SaveDto
    {
        public int Version { get; set; }
        public string Game { get; set; } = "";
        public GameOptionsSnapshot? Options { get; set; }
        public PartyDto? Party { get; set; }
        public P4Place Place { get; set; }
        public int Map { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Facing { get; set; }
        public JsonObject? Dungeon { get; set; }
        public bool Started { get; set; }
        public P4OutdoorSnapshot? Outdoor { get; set; }
        public P4TownSnapshot? Town { get; set; }
    }

    /// <summary>The dungeon loop as written: its map, encounter pool and pending fight as game ids.</summary>
    private static JsonObject? DungeonToJson(P4DungeonSnapshot? d)
    {
        if (d is null)
            return null;
        var node = JsonSerializer.SerializeToNode(d, JsonOptions)!.AsObject();
        node["map"] = Id(d.Map);
        node["pool"] = new JsonArray([.. d.Pool.Select(m => (JsonNode?)Id(m))]);
        if (d.PendingFight is { } f)
            node["pendingFight"]!["encounter"] = Id(f.Encounter);
        return node;

        static JsonNode Id(int local) => JsonValue.Create(new ContentId(GameId.P4, local).ToString());
    }

    private static P4DungeonSnapshot? DungeonFromJson(JsonObject? node)
    {
        if (node is null)
            return null;
        var copy = node.DeepClone().AsObject();
        copy["map"] = Local(copy["map"]);
        copy["pool"] = new JsonArray([.. (copy["pool"]?.AsArray() ?? []).Select(n => (JsonNode?)Local(n))]);
        if (copy["pendingFight"] is JsonObject fight)
            fight["encounter"] = Local(fight["encounter"]);
        return copy.Deserialize<P4DungeonSnapshot>(JsonOptions);

        static JsonNode Local(JsonNode? n) =>
            JsonValue.Create((n?.Deserialize<ContentId>(JsonOptions)
                ?? throw new InvalidDataException("A dungeon id is missing.")).LocalIn(GameId.P4));
    }

    private sealed class MemberDto
    {
        public string Name { get; set; } = "";
        public int Race { get; set; }
        public int Class { get; set; }
        public int Level { get; set; }
        public int Condition { get; set; }
        public int HpMax { get; set; }
        public int Hp { get; set; }
        public int MpMax { get; set; }
        public int Mp { get; set; }
        public int Defence { get; set; }
        public int[] Skills { get; set; } = [];
        public ContentId?[] Items { get; set; } = [];
        public P4MemberMore? More { get; set; }

        public static MemberDto From(P4MemberSnapshot m) => new()
        {
            Name = m.Name, Race = m.Race, Class = m.Class, Level = m.Level, Condition = m.Condition, HpMax = m.HpMax, Hp = m.Hp,
            MpMax = m.MpMax, Mp = m.Mp, Defence = m.Defence, Skills = m.Skills, Items = ToIds(m.Items), More = m.More,
        };

        public P4MemberSnapshot ToSnapshot() =>
            new(Name, Race, Class, Level, Condition, HpMax, Hp, MpMax, Mp, Defence, Skills, ToLocals(Items),
                More ?? throw new InvalidDataException($"{Name}: the saved member has no record fields."));
    }

    private sealed class PartyDto
    {
        public MemberDto[] Members { get; set; } = [];
        public long Gold { get; set; }
        public int AverageLevel { get; set; }
        public bool Levitating { get; set; }
        public int? LevitationSteps { get; set; }
        public ContentId?[] Pack { get; set; } = [];
        public int[] PackCharges { get; set; } = [];
        public byte[] StoryFlags { get; set; } = [];
        public byte[] LocationFlags { get; set; } = [];
        public P4PartyTravel? Travel { get; set; }
        public GuildDto? Guild { get; set; }
        public int? Elemental { get; set; }

        /// <summary>The Guild treasury (11-guild-treasury.md); absent while empty and in older saves.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TreasurySnapshot? Treasury { get; set; }

        public static PartyDto From(P4PartySnapshot p) => new()
        {
            Members = [.. p.Members.Select(MemberDto.From)], Gold = p.Gold, AverageLevel = p.AverageLevel,
            Levitating = p.Levitating, LevitationSteps = p.LevitationSteps, Pack = ToIds(p.Pack), PackCharges = p.PackCharges,
            StoryFlags = p.StoryFlags, LocationFlags = p.LocationFlags, Travel = p.Travel,
            Guild = p.Guild is { } g ? GuildDto.From(g) : null, Elemental = p.Elemental, Treasury = p.Treasury,
        };

        public P4PartySnapshot ToSnapshot() => new(
            [.. Members.Select(m => m.ToSnapshot())], Gold, AverageLevel, Levitating, LevitationSteps, ToLocals(Pack),
            PackCharges, StoryFlags, LocationFlags,
            Travel ?? throw new InvalidDataException("The saved party has no travel fields."), Elemental, Guild?.ToSnapshot(),
            Treasury);
    }

    /// <summary>The guild roster (members as in the party; empty slots null) and the shop's stock by item id.</summary>
    private sealed class GuildDto
    {
        public MemberDto?[] Roster { get; set; } = [];
        public int MaxScore { get; set; }
        public int[] GoodsCount { get; set; } = [];
        public int[] GoodsTown { get; set; } = [];
        public int[] ShopStock { get; set; } = [];
        public int EncounterLevel { get; set; }
        public int?[]? HallDescendants { get; set; }

        /// <summary>T5: the Hall persons taken in from III, by slot (0–2 null); absent in a save without any.</summary>
        public HallPersonDto?[]? HallImports { get; set; }

        public static GuildDto From(P4PartyGuild g) => new()
        {
            Roster = [.. g.Roster.Select(m => m is null ? null : MemberDto.From(m))], MaxScore = g.MaxScore,
            GoodsCount = g.GoodsCount, GoodsTown = g.GoodsTown, ShopStock = g.ShopStock,
            EncounterLevel = g.EncounterLevel, HallDescendants = g.HallDescendants,
            HallImports = g.HallImports is { } hi && hi.Any(h => h is not null)
                ? [.. hi.Select(h => h is null ? null : HallPersonDto.From(h))] : null,
        };

        public P4PartyGuild ToSnapshot() =>
            new([.. Roster.Select(m => m?.ToSnapshot())], MaxScore, GoodsCount, GoodsTown, ShopStock,
                EncounterLevel, HallDescendants, HallImports?.Select(h => h?.ToPerson()).ToArray());
    }

    /// <summary>One Hall of Glory record (<c>FORMAT.md</c> section 6.3) as the III transfer wrote it.</summary>
    private sealed class HallPersonDto
    {
        public int Slot { get; set; }
        public string Name { get; set; } = "";
        public int Race { get; set; }
        public int Class { get; set; }
        public int Level { get; set; }
        public int Social { get; set; }
        public int Origin { get; set; }
        public int[] Stats { get; set; } = [];
        public int[] Results { get; set; } = [];
        public int Descendants { get; set; }
        public int DeedsLine { get; set; }
        public bool Alive { get; set; }

        public static HallPersonDto From(P4HallPerson h) => new()
        {
            Slot = h.Slot, Name = h.Name, Race = h.Race, Class = h.Class, Level = h.Level, Social = h.Social,
            Origin = h.Origin, Stats = [.. h.Stats], Results = [.. h.Results], Descendants = h.Descendants,
            DeedsLine = h.DeedsLine, Alive = h.Alive,
        };

        public P4HallPerson ToPerson() =>
            new(Slot, Name, Race, Class, Level, Social, Origin, Stats, Results, Descendants, DeedsLine, Alive);
    }
}
