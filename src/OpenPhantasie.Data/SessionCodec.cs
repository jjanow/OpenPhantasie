using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Remaster campaign save codec (Phase 7). JSON primary format — save anywhere,
/// no town/inn restriction. Guild roster characters reuse <see cref="GuildCodec"/> DTOs.
/// <para>
/// Phase 09 T1.2: the file names its game at the root (<c>"game": "p1"</c>) and every item, monster and dungeon id in
/// it is game-prefixed (<c>"p1:42"</c>, <see cref="ContentId"/>). Arrays indexed by id (town stock, spell flags) stay
/// positional under the root game. The snapshot keeps the local numbers Core works with. Files from before T1.2
/// (version &lt; 3, bare ids) are refused, not migrated. DOS GUILD/SACK/TOWNS/DNG files never pass through here.
/// </para>
/// </summary>
public static class SessionCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new ContentIdJsonConverter() },
    };

    public static string WriteJson(SessionSnapshot snap)
    {
        ArgumentNullException.ThrowIfNull(snap);
        var dto = ToDto(snap, snap.Game);
        dto.Game = snap.Game.Prefix();
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static void WriteJsonFile(string path, SessionSnapshot snap) =>
        File.WriteAllText(path, WriteJson(snap));

    /// <summary>
    /// Read a remaster save. A file from before game-prefixed ids (version &lt; 3 or missing, no <c>"game"</c>, or a
    /// bare-number id) throws <see cref="OldSaveFormatException"/>; anything else unreadable throws
    /// <see cref="InvalidDataException"/>.
    /// </summary>
    public static SessionSnapshot ReadJson(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The save file is not valid JSON.", ex);
        }
        if (root is not JsonObject obj)
            throw new InvalidDataException("The save file is not a remaster save.");

        // Looked at on the raw tree first: an old file's bare-number ids make the typed read fail before its version
        // could be checked.
        var version = obj["version"] is JsonValue v && v.TryGetValue<int>(out var n) ? n : 0;
        var gameText = obj["game"] is JsonValue g && g.TryGetValue<string>(out var t) ? t : null;
        if (version < FirstPrefixedIdVersion || gameText is null)
            throw new OldSaveFormatException(version);
        if (!GameIds.TryParsePrefix(gameText, out var game))
            throw new InvalidDataException($"The save names an unknown game \"{gameText}\".");

        try
        {
            var dto = obj.Deserialize<SessionDto>(JsonOptions) ?? throw new InvalidDataException("The save file is empty.");
            return FromDto(dto, game);
        }
        catch (BareContentIdException ex)
        {
            throw new OldSaveFormatException(version, ex); // claims version 3 but an id is still a bare number
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The save file can't be read: {ex.Message}", ex);
        }
    }

    public static SessionSnapshot ReadJsonFile(string path) =>
        ReadJson(File.ReadAllText(path));

    private static SessionDto ToDto(SessionSnapshot snap, GameId game)
    {
        var slots = new List<GuildSlotDto?>();
        for (var i = 0; i < snap.RosterSlots.Length; i++)
        {
            var ch = snap.RosterSlots[i];
            slots.Add(ch is null
                ? null
                : new GuildSlotDto { Slot = i, CharacterJson = GuildCodec.WriteJson(new[] { ch }, game) });
        }

        return new SessionDto
        {
            Version = snap.Version,
            Mode = snap.Mode,
            CombatReturnMode = snap.CombatReturnMode,
            CurrentTownId = snap.CurrentTownId,
            PartySlotIndices = snap.PartySlotIndices.ToArray(),
            PartyGold = snap.PartyGold,
            ElementalJson = snap.Elemental is null ? null : GuildCodec.WriteJson(new[] { snap.Elemental }, game),
            ElementalSummoned = snap.ElementalSummoned,
            OverworldRegionId = snap.OverworldRegionId,
            OverworldX = snap.OverworldX,
            OverworldY = snap.OverworldY,
            OverworldTerrainClass = snap.OverworldTerrainClass,
            TravelTime = snap.TravelTime,
            Wilderness = WildernessToJson(snap.Wilderness, game),
            Roster = snap.RosterSlots.Length == 0 ? null : slots, // a parked party (D-069) holds no roster
            Dungeon = snap.Dungeon is null
                ? null
                : new DungeonDto
                {
                    LevelId = new ContentId(game, snap.Dungeon.LevelId),
                    X = snap.Dungeon.X,
                    Y = snap.Dungeon.Y,
                    Facing = snap.Dungeon.Facing,
                    LevelBuffer = snap.Dungeon.LevelBuffer,
                    ExitPending = snap.Dungeon.ExitPending,
                    PrevX = snap.Dungeon.PrevX,
                    PrevY = snap.Dungeon.PrevY,
                    WanderingChance = snap.Dungeon.WanderingChance,
                    LastMessageId = snap.Dungeon.LastMessageId,
                    TrapPrompt = snap.Dungeon.TrapPrompt,
                    LockListener = snap.Dungeon.LockListener,
                    LockMessage = snap.Dungeon.LockMessage,
                    EventFeature = snap.Dungeon.EventFeature,
                    EventMessageId = snap.Dungeon.EventMessageId,
                    EventLines = snap.Dungeon.EventLines,
                    EventOptions = snap.Dungeon.EventOptions,
                    ScriptResume = snap.Dungeon.ScriptResume,
                    PageLines = snap.Dungeon.PageLines,
                    PageWaits = snap.Dungeon.PageWaits,
                },
            OtherDungeonSaves = snap.OtherDungeonSaves.Length == 0
                ? null
                : snap.OtherDungeonSaves.Select(s => new DungeonSaveDto
                {
                    DungeonId = new ContentId(game, s.DungeonId),
                    LevelBuffer = s.LevelBuffer,
                    ScreenImage = s.ScreenImage,
                }).ToList(),
            DungeonSave = snap.DungeonSave is null
                ? null
                : new DungeonSaveDto
                {
                    DungeonId = new ContentId(game, snap.DungeonSave.DungeonId),
                    LevelBuffer = snap.DungeonSave.LevelBuffer,
                    ScreenImage = snap.DungeonSave.ScreenImage,
                },
            Sack = new SackDto
            {
                Experience = snap.Sack.Experience,
                Gold = snap.Sack.Gold,
                ItemIds = snap.Sack.ItemIds.ToIds(game),
                TownExitItemIds = snap.Sack.TownExitItemIds?.ToIds(game),
            },
            PendingTreasure = snap.PendingTreasure.ToIds(game),
            PendingTreasureFinders = snap.PendingTreasureFinders.Any(f => f is not null) ? snap.PendingTreasureFinders : null,
            PartyDefeatPending = snap.PartyDefeatPending,
            OtherParties = snap.OtherParties.Length == 0 ? null : snap.OtherParties.Select(p => ToDto(p, game)).ToList(),
            Options = snap.Options is { } o ? new OptionsDto { Preset = o.Preset, Values = o.Values, Modules = o.Modules } : null,
            Towns = snap.Towns.Length == 0
                ? null
                : snap.Towns.Select(t => new TownDto
                {
                    Id = t.Id,
                    Index = t.Index,
                    Name = t.Name,
                    PriceIndex = t.PriceIndex,
                    Stock = t.Stock.ToArray(),
                    RestockItems = t.RestockItems.ToIds(game),
                    DosRecord = t.DosRecord,
                }).ToList(),
            Combat = snap.Combat is null
                ? null
                : new CombatDto
                {
                    RewardXp = snap.Combat.RewardXp,
                    RewardGold = snap.Combat.RewardGold,
                    MonsterLevel = snap.Combat.MonsterLevel,
                    TreasureItemIds = snap.Combat.TreasureItemIds.ToIds(game),
                    FirstMonsterId = snap.Combat.FirstMonsterId is >= 0 and var first
                        ? new ContentId(game, first)
                        : null, // -1 = no monster yet
                    ReactionSum = snap.Combat.ReactionSum,
                    ThreatenFactor = snap.Combat.ThreatenFactor,
                    MonsterDamaged = snap.Combat.MonsterDamaged,
                    MonstersLoseRound = snap.Combat.MonstersLoseRound,
                    SpellEnd = (int)snap.Combat.SpellEnd,
                    Dissolved = snap.Combat.Dissolved,
                    DispellUndeadTried = snap.Combat.DispellUndeadTried,
                    Prep = snap.Combat.Prep?.Select(p => new CombatPrepDto
                    {
                        ArmorEff = p.ArmorEff,
                        WeaponDamageEff = p.WeaponDamageEff,
                        WeaponHitEff = p.WeaponHitEff,
                        PackedAction = p.PackedAction,
                    }).ToList(),
                    SurpriseFirstRound = snap.Combat.SurpriseFirstRound,
                    MonsterHold = snap.Combat.MonsterHold,
                    OpensWithoutOrders = snap.Combat.OpensWithoutOrders,
                    MoraleCheckedThisRound = snap.Combat.MoraleCheckedThisRound,
                    OptionChosenThisRound = snap.Combat.OptionChosenThisRound,
                    Phantasie3 = snap.Combat.Phantasie3,
                    Monsters = snap.Combat.Monsters.Select(m => new CombatMonsterDto
                    {
                        Id = new ContentId(game, m.Id),
                        Group = m.Group,
                        HitPoints = m.HitPoints,
                        Armor = m.Armor,
                        Defense = m.Defense,
                        AttackPower = m.AttackPower,
                        StatusFlags = (int)m.StatusFlags,
                    }).ToList(),
                },
        };
    }

    private static SessionSnapshot FromDto(SessionDto dto, GameId game)
    {
        var slots = new Character?[dto.Roster?.Count ?? 0];
        if (dto.Roster is not null)
        {
            foreach (var entry in dto.Roster)
            {
                if (entry?.CharacterJson is null) continue;
                if (entry.Slot < 0 || entry.Slot >= slots.Length) continue;
                var chars = GuildCodec.ReadJson(entry.CharacterJson, game);
                if (chars.Count > 0)
                    slots[entry.Slot] = chars[0];
            }
        }

        return new SessionSnapshot
        {
            Version = dto.Version <= 0 ? SessionSnapshot.CurrentVersion : dto.Version,
            Game = game, // parked parties too
            Mode = dto.Mode,
            CombatReturnMode = dto.CombatReturnMode,
            CurrentTownId = dto.CurrentTownId,
            RosterSlots = slots, // all empty in a parked party (D-069): its roster is the top level's
            PartySlotIndices = dto.PartySlotIndices ?? Array.Empty<int>(),
            PartyGold = dto.PartyGold,
            Elemental = dto.ElementalJson is null ? null : GuildCodec.ReadJson(dto.ElementalJson, game).FirstOrDefault(),
            ElementalSummoned = dto.ElementalSummoned,
            OverworldRegionId = dto.OverworldRegionId,
            OverworldX = dto.OverworldX,
            OverworldY = dto.OverworldY,
            OverworldTerrainClass = dto.OverworldTerrainClass,
            TravelTime = dto.TravelTime,
            Wilderness = WildernessFromJson(dto.Wilderness, game),
            Dungeon = dto.Dungeon is null
                ? null
                : new DungeonSnapshot
                {
                    LevelId = dto.Dungeon.LevelId.LocalIn(game),
                    X = dto.Dungeon.X,
                    Y = dto.Dungeon.Y,
                    Facing = dto.Dungeon.Facing,
                    LevelBuffer = dto.Dungeon.LevelBuffer, // older saves: null (ClearedEventKeys ignored)
                    ExitPending = dto.Dungeon.ExitPending,
                    PrevX = dto.Dungeon.PrevX,
                    PrevY = dto.Dungeon.PrevY,
                    WanderingChance = dto.Dungeon.WanderingChance,
                    LastMessageId = dto.Dungeon.LastMessageId,
                    TrapPrompt = dto.Dungeon.TrapPrompt,
                    LockListener = dto.Dungeon.LockListener,
                    LockMessage = dto.Dungeon.LockMessage,
                    EventFeature = dto.Dungeon.EventFeature,
                    EventMessageId = dto.Dungeon.EventMessageId,
                    EventLines = dto.Dungeon.EventLines,
                    EventOptions = dto.Dungeon.EventOptions,
                    ScriptResume = dto.Dungeon.ScriptResume,
                    PageLines = dto.Dungeon.PageLines,
                    PageWaits = dto.Dungeon.PageWaits,
                },
            OtherDungeonSaves = dto.OtherDungeonSaves?
                .Where(s => s.LevelBuffer is { Length: > 0 })
                .Select(s => new DungeonSaveSnapshot
                {
                    DungeonId = s.DungeonId.LocalIn(game),
                    LevelBuffer = s.LevelBuffer!,
                    ScreenImage = s.ScreenImage,
                }).ToArray() ?? Array.Empty<DungeonSaveSnapshot>(),
            DungeonSave = dto.DungeonSave is { LevelBuffer: { } lb }
                ? new DungeonSaveSnapshot
                {
                    DungeonId = dto.DungeonSave.DungeonId.LocalIn(game),
                    LevelBuffer = lb,
                    ScreenImage = dto.DungeonSave.ScreenImage,
                }
                : null,
            Sack = new LootSackSnapshot
            {
                Experience = dto.Sack?.Experience ?? 0,
                Gold = dto.Sack?.Gold ?? 0,
                ItemIds = dto.Sack?.ItemIds.ToLocals(game) ?? Array.Empty<int>(),
                TownExitItemIds = dto.Sack?.TownExitItemIds?.ToLocals(game),
            },
            PendingTreasure = dto.PendingTreasure.ToLocals(game),
            PendingTreasureFinders = dto.PendingTreasureFinders ?? Array.Empty<string?>(),
            PartyDefeatPending = dto.PartyDefeatPending,
            OtherParties = dto.OtherParties?.Select(p => FromDto(p, game)).ToArray() ?? Array.Empty<SessionSnapshot>(),
            Options = dto.Options is { } o
                ? new GameOptionsSnapshot
                {
                    Preset = o.Preset ?? "",
                    Values = o.Values ?? new Dictionary<string, string>(StringComparer.Ordinal),
                    Modules = o.Modules ?? Array.Empty<string>(),
                }
                : null,
            Towns = dto.Towns?
                .Where(t => t.Stock is { Length: TownState.StockSlotCount })
                .Select(t => new TownState(t.Id, t.Index, t.Name ?? "", t.PriceIndex, t.Stock!)
                {
                    DosRecord = t.DosRecord is { Length: TownsCodec.RecordSize } rec ? rec : null,
                    RestockItems = t.RestockItems?.ToLocals(game)
                                   ?? (t.DosRecord is { Length: TownsCodec.RecordSize } r2 ? TownsCodec.ReadRestockItems(r2) : []),
                })
                .ToArray() ?? Array.Empty<TownState>(),
            Combat = dto.Combat is null
                ? null
                : new CombatSnapshot
                {
                    RewardXp = dto.Combat.RewardXp,
                    RewardGold = dto.Combat.RewardGold,
                    MonsterLevel = dto.Combat.MonsterLevel,
                    TreasureItemIds = dto.Combat.TreasureItemIds.ToLocals(game),
                    FirstMonsterId = dto.Combat.FirstMonsterId?.LocalIn(game),
                    ReactionSum = dto.Combat.ReactionSum,
                    ThreatenFactor = dto.Combat.ThreatenFactor,
                    MonsterDamaged = dto.Combat.MonsterDamaged,
                    MonstersLoseRound = dto.Combat.MonstersLoseRound,
                    SpellEnd = (SpellCombatEnd)dto.Combat.SpellEnd,
                    Dissolved = dto.Combat.Dissolved,
                    DispellUndeadTried = dto.Combat.DispellUndeadTried,
                    Prep = dto.Combat.Prep?.Select(p => new CombatPrepSnapshot
                    {
                        ArmorEff = p.ArmorEff,
                        WeaponDamageEff = p.WeaponDamageEff,
                        WeaponHitEff = p.WeaponHitEff,
                        PackedAction = p.PackedAction,
                    }).ToList(),
                    SurpriseFirstRound = dto.Combat.SurpriseFirstRound,
                    MonsterHold = dto.Combat.MonsterHold,
                    OpensWithoutOrders = dto.Combat.OpensWithoutOrders,
                    MoraleCheckedThisRound = dto.Combat.MoraleCheckedThisRound,
                    OptionChosenThisRound = dto.Combat.OptionChosenThisRound,
                    Phantasie3 = dto.Combat.Phantasie3,
                    Monsters = (dto.Combat.Monsters ?? new List<CombatMonsterDto>())
                        .Select(m => new CombatMonsterSnapshot
                        {
                            Id = m.Id.LocalIn(game),
                            Group = m.Group,
                            HitPoints = m.HitPoints,
                            Armor = m.Armor,
                            Defense = m.Defense,
                            AttackPower = m.AttackPower,
                            StatusFlags = (MonsterStatusFlags)m.StatusFlags,
                        }).ToList(),
                },
        };
    }

    /// <summary>The first save format with game-prefixed ids (T1.2); older files are refused.</summary>
    private const int FirstPrefixedIdVersion = 3;

    /// <summary>
    /// <see cref="WildernessSnapshot"/> is a Core class saved as is, except its two content ids, which are written
    /// game-prefixed: the prompt monster (left out when no prompt is open: the snapshot then holds 0) and a dungeon
    /// entry's level (town entries keep the bare town id).
    /// </summary>
    private static JsonObject? WildernessToJson(WildernessSnapshot? w, GameId game)
    {
        if (w is null) return null;
        var node = JsonSerializer.SerializeToNode(w, JsonOptions)!.AsObject();
        node.Remove(PromptMonsterKey);
        if (w.PromptMenu is not null)
            node[PromptMonsterKey] = new ContentId(game, w.PromptMonsterId).ToString();
        if (w.EntryKind == OverworldEntryKind.Dungeon)
            node[EntryIdKey] = new ContentId(game, w.EntryId).ToString();
        return node;
    }

    private static WildernessSnapshot? WildernessFromJson(JsonObject? node, GameId game)
    {
        if (node is null) return null;
        var copy = node.DeepClone().AsObject();
        var prompt = copy[PromptMonsterKey]?.Deserialize<ContentId>(JsonOptions);
        copy.Remove(PromptMonsterKey);
        ContentId? dungeonEntry = null;
        if (copy[EntryIdKey] is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.String)
        {
            dungeonEntry = v.Deserialize<ContentId>(JsonOptions);
            copy.Remove(EntryIdKey);
        }

        var w = copy.Deserialize<WildernessSnapshot>(JsonOptions) ?? new WildernessSnapshot();
        w.PromptMonsterId = prompt?.LocalIn(game) ?? 0;
        if (w.EntryKind == OverworldEntryKind.Dungeon)
            w.EntryId = dungeonEntry?.LocalIn(game)
                        ?? throw new InvalidDataException("A dungeon entry prompt needs a game-prefixed level id.");
        else if (dungeonEntry is not null)
            throw new InvalidDataException("Only a dungeon entry prompt carries a game-prefixed entry id.");
        return w;
    }

    private const string PromptMonsterKey = "promptMonsterId";
    private const string EntryIdKey = "entryId";

    private sealed class SessionDto
    {
        public int Version { get; set; }

        /// <summary>T1.2: the game every id in the file belongs to (<c>p1</c>…<c>p4</c>); written on the top level only.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Game { get; set; }
        public GameMode Mode { get; set; }
        public GameMode CombatReturnMode { get; set; }
        public int? CurrentTownId { get; set; }
        public int[]? PartySlotIndices { get; set; }
        public long PartyGold { get; set; }

        /// <summary>Party slot 7 (SUMMON ELEMENTAL), GUILD JSON of one character; absent in older saves.</summary>
        public string? ElementalJson { get; set; }

        public bool ElementalSummoned { get; set; }
        public int OverworldRegionId { get; set; }
        public int OverworldX { get; set; }
        public int OverworldY { get; set; }
        public int OverworldTerrainClass { get; set; }

        /// <summary>Travel-time counter <c>[0x63f2]</c>; absent (0) in older saves.</summary>
        public int TravelTime { get; set; }

        /// <summary>s39 wilderness globals + region bytes; absent in older saves.</summary>
        public JsonObject? Wilderness { get; set; }
        public List<GuildSlotDto?>? Roster { get; set; }
        public DungeonDto? Dungeon { get; set; }
        public DungeonSaveDto? DungeonSave { get; set; }

        /// <summary>III: the other dungeons' saved maps (s95).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<DungeonSaveDto>? OtherDungeonSaves { get; set; }
        public SackDto? Sack { get; set; }
        public CombatDto? Combat { get; set; }
        public ContentId[]? PendingTreasure { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string?[]? PendingTreasureFinders { get; set; }
        public bool PartyDefeatPending { get; set; }

        /// <summary>P1.4 town Armory states kept between visits; absent in older saves.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<TownDto>? Towns { get; set; }

        /// <summary>D-069 parked parties (party-local fields only, no roster/towns/region bytes/dungeon slot).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SessionDto>? OtherParties { get; set; }

        /// <summary>T5.0: the save's locked rules, top level only; absent before s73 (Original (manual)).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OptionsDto? Options { get; set; }
    }

    private sealed class OptionsDto
    {
        public string? Preset { get; set; }
        public Dictionary<string, string>? Values { get; set; }
        public string[]? Modules { get; set; }
    }

    private sealed class TownDto
    {
        public int Id { get; set; }
        public int Index { get; set; }
        public string? Name { get; set; }
        public int PriceIndex { get; set; }
        public int[]? Stock { get; set; }

        /// <summary>The town's restock list (TWNS.DAT <c>+0xb5</c>, <see cref="TownState.RestockItems"/>).</summary>
        public ContentId[]? RestockItems { get; set; }

        /// <summary>Carried TWNS.DAT record (base64) after a DOS import.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public byte[]? DosRecord { get; set; }
    }

    private sealed class GuildSlotDto
    {
        public int Slot { get; set; }
        public string? CharacterJson { get; set; }
    }

    private sealed class DungeonDto
    {
        public ContentId LevelId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public DungeonFacing Facing { get; set; }
        /// <summary>Pre-s36 stand-in; still read so old saves load, but ignored (the level restarts from the original).</summary>
        public int[]? ClearedEventKeys { get; set; }

        public byte[]? LevelBuffer { get; set; }
        public bool ExitPending { get; set; }
        public int? PrevX { get; set; }
        public int? PrevY { get; set; }
        public int? WanderingChance { get; set; }
        public int? LastMessageId { get; set; }

        // s41: modal prompts survive a save.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DungeonTrapPromptSnapshot? TrapPrompt { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? LockListener { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LockMessage { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? EventFeature { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? EventMessageId { get; set; }
        // s95: the question's own lines and choices (prompts that are not level messages, padded menus).
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? EventLines { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? EventOptions { get; set; }
        // s58: an event script stopped at op 6's KEEP IT? resumes after a load.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DungeonScriptResume? ScriptResume { get; set; }
        // III's event-text page: its rows and whether it waits for a key.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? PageLines { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool PageWaits { get; set; }
    }

    private sealed class DungeonSaveDto
    {
        public ContentId DungeonId { get; set; }
        public byte[]? LevelBuffer { get; set; }
        public byte[]? ScreenImage { get; set; }
    }

    private sealed class SackDto
    {
        public long Experience { get; set; }
        public long Gold { get; set; }
        public ContentId[]? ItemIds { get; set; }
        public ContentId[]? TownExitItemIds { get; set; }
    }

    private sealed class CombatDto
    {
        public long RewardXp { get; set; }
        public long RewardGold { get; set; }
        public int? MonsterLevel { get; set; }
        public ContentId[]? TreasureItemIds { get; set; }
        public ContentId? FirstMonsterId { get; set; }
        public int ReactionSum { get; set; }
        public int? ThreatenFactor { get; set; }
        public bool MonsterDamaged { get; set; }
        public bool MonstersLoseRound { get; set; }
        public int SpellEnd { get; set; }
        public bool Dissolved { get; set; }
        public bool DispellUndeadTried { get; set; }
        public bool SurpriseFirstRound { get; set; }
        public int MonsterHold { get; set; }
        public bool OpensWithoutOrders { get; set; }

        /// <summary>Per-round PARTY OPTIONS state (P0.5 s39); false in older saves.</summary>
        public bool MoraleCheckedThisRound { get; set; }
        public bool OptionChosenThisRound { get; set; }
        public List<CombatMonsterDto>? Monsters { get; set; }

        /// <summary>Absent in saves from before s33 (no version bump: missing = rebuilt by prep).</summary>
        public List<CombatPrepDto>? Prep { get; set; }

        /// <summary>A Phantasie III fight's own state (s95); written only by III.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public P3FightSnapshot? Phantasie3 { get; set; }
    }

    private sealed class CombatPrepDto
    {
        public int ArmorEff { get; set; }
        public int WeaponDamageEff { get; set; }
        public int WeaponHitEff { get; set; }
        public int PackedAction { get; set; }
    }

    private sealed class CombatMonsterDto
    {
        public ContentId Id { get; set; }
        public int? Group { get; set; }
        public int HitPoints { get; set; }
        public int Armor { get; set; }
        public int Defense { get; set; }
        public int AttackPower { get; set; }
        public int StatusFlags { get; set; }
    }
}
