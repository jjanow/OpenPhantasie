using OpenPhantasie.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// The side tables of one Phantasie III <c>DNGS.DAT</c> record (<c>reverse-engineering/dos/phantasie3/FORMAT.md</c> §9.5),
/// as <c>dungeons.json</c> <c>tables</c> carries them. Meanings the specs leave <c>[?]</c> stay raw.
/// </summary>
/// <param name="Level">Dungeon level L (0–5): random and lair monsters come from band <c>12·L</c> [C].</param>
/// <param name="ForcedFights">Monster id for each forced-fight cell 0x80–0x89 [C].</param>
/// <param name="Exits">The exits of cells 0xd7–0xd9 [C]; destinations 29 / 31 / 41 = Light / Dark / Netherworld, others [?].</param>
/// <param name="EventSquares">The 35 event squares of cells 0xdd–0xff [C].</param>
/// <param name="EventProgram">The 512 bytes of 2-byte event ops (op types beyond traps and fights [?]).</param>
/// <param name="TextSlots">Size of the record's text pointer table [~].</param>
public sealed record DungeonRecordTables(
    int Level,
    IReadOnlyList<int> ForcedFights,
    IReadOnlyList<DungeonExit> Exits,
    IReadOnlyList<DungeonEventSquare> EventSquares,
    byte[] EventProgram,
    int TextSlots);

/// <summary>One exit cell: where the exit leads (<see cref="Destination"/>) and the square there.</summary>
public sealed record DungeonExit(int Cell, int Destination, int X, int Y);

/// <summary>
/// One event square: message number (byte 0 & 0x7f), once only (bit 7), number of choices (keys 1…n) and the event-program
/// index per choice (ovl4 <c>0x12a4</c>) [C].
/// </summary>
public sealed record DungeonEventSquare(int Cell, int Message, bool Once, int Choices, IReadOnlyList<int> Programs);

internal sealed class DungeonRecordTablesDto
{
    public int Level { get; set; }

    [JsonPropertyName("forced_fights")] public List<int>? ForcedFights { get; set; }
    public List<ExitDto>? Exits { get; set; }
    [JsonPropertyName("event_squares")] public List<SquareDto>? EventSquares { get; set; }
    [JsonPropertyName("event_program_hex")] public string? EventProgramHex { get; set; }
    [JsonPropertyName("text_slots")] public int TextSlots { get; set; }

    public DungeonRecordTables ToDefinition(string id)
    {
        if (ForcedFights is not { Count: 10 } || Exits is not { Count: 3 } || EventSquares is not { Count: 35 })
            throw new InvalidDataException($"Dungeon {id}: tables need 10 forced fights, 3 exits and 35 event squares.");
        return new DungeonRecordTables(Level, ForcedFights,
            Exits.Select(e => new DungeonExit(e.Cell, e.Destination, e.X, e.Y)).ToList(),
            EventSquares.Select(e => new DungeonEventSquare(e.Cell, e.Message, e.Once, e.Choices, e.Programs ?? [])).ToList(),
            Convert.FromHexString(EventProgramHex ?? ""), TextSlots);
    }

    internal sealed class ExitDto
    {
        public int Cell { get; set; }
        public int Destination { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    internal sealed class SquareDto
    {
        public int Cell { get; set; }
        public int Message { get; set; }
        public bool Once { get; set; }
        public int Choices { get; set; }
        public List<int>? Programs { get; set; }
    }
}

/// <summary>
/// A hard-coded wilderness place (Phantasie III, ovl3 <c>place_at_party</c>; <c>logic-specs/planes.md</c> §4): a town,
/// dungeon or independent inn on a plane's square. <see cref="Number"/> is the town / dungeon / inn number.
/// </summary>
public sealed record WorldPlaceDefinition(string Plane, int X, int Y, string Kind, int Number, string Name);

/// <summary>A tile-id range and the terrain index it gives.</summary>
public sealed record TileTerrainRange(int First, int Last, int Terrain);

/// <summary>
/// Phantasie III's terrain by tile id (ovl3 <c>0x375</c>, <c>planes.md</c> §3): the first matching range wins, else
/// <see cref="Other"/>; the Netherworld's overrides are tried first there.
/// </summary>
public sealed record TileTerrainDefinition(
    IReadOnlyList<string> Names,
    IReadOnlyList<TileTerrainRange> Ranges,
    int Other,
    IReadOnlyList<TileTerrainRange> NetherworldOverrides,
    int BlockedFrom,
    IReadOnlyList<int> NoEncounter)
{
    /// <summary>The terrain index of <paramref name="tile"/> on <paramref name="plane"/>.</summary>
    public int TerrainOf(int tile, string? plane)
    {
        if (plane == "netherworld" && NetherworldOverrides.FirstOrDefault(r => tile >= r.First && tile <= r.Last) is { } o)
            return o.Terrain;
        return Ranges.FirstOrDefault(r => tile >= r.First && tile <= r.Last)?.Terrain ?? Other;
    }

    public bool IsBlocked(int tile) => tile >= BlockedFrom;
}

/// <summary>Reads the optional III blocks of <c>overworld.json</c> and <c>scrolls.json</c> (absent in I / II files).</summary>
internal static class WorldExtrasLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static IReadOnlyList<WorldPlaceDefinition> LoadPlaces(string overworldPath)
    {
        var dto = JsonSerializer.Deserialize<ExtrasDto>(File.ReadAllText(overworldPath), Options);
        return (dto?.Places ?? []).Select(p => new WorldPlaceDefinition(
            p.Plane ?? throw new InvalidDataException($"{overworldPath}: a place has no plane."), p.X, p.Y,
            p.Type ?? throw new InvalidDataException($"{overworldPath}: a place has no type."), p.Number,
            p.Name ?? "")).ToList();
    }

    public static TileTerrainDefinition? LoadTerrain(string overworldPath)
    {
        var t = JsonSerializer.Deserialize<ExtrasDto>(File.ReadAllText(overworldPath), Options)?.Terrain;
        if (t is null)
            return null;
        static List<TileTerrainRange> R(List<RangeDto>? l) =>
            (l ?? []).Select(r => r.First <= r.Last ? new TileTerrainRange(r.First, r.Last, r.Terrain)
                : throw new InvalidDataException($"terrain range {r.First}..{r.Last} is empty.")).ToList();
        return new TileTerrainDefinition(t.Names ?? [], R(t.Ranges), t.Other, R(t.NetherworldOverrides),
            t.BlockedFrom ?? throw new InvalidDataException($"{overworldPath}: terrain.blocked_from is missing."),
            t.NoEncounter ?? []);
    }

    public static IReadOnlyDictionary<int, IReadOnlyList<int>> LoadScrollItemRecords(string scrollsPath)
    {
        var map = JsonSerializer.Deserialize<ScrollExtrasDto>(File.ReadAllText(scrollsPath), Options)?.ItemRecords;
        return (map ?? []).ToDictionary(
            kv => int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture),
            kv => (IReadOnlyList<int>)kv.Value);
    }

    /// <summary>
    /// <c>scrolls.json</c> <c>page_layout</c> = <c>rows</c> (II ST): a scroll's lines are the screen rows the original
    /// prints (centred titles, blank rows kept), so an <c>item_records</c> page is shown as stored, not trimmed.
    /// </summary>
    public static bool LoadScrollPagesAsRows(string scrollsPath) =>
        JsonSerializer.Deserialize<ScrollExtrasDto>(File.ReadAllText(scrollsPath), Options)?.PageLayout == "rows";

    private sealed class ExtrasDto
    {
        public List<PlaceDto>? Places { get; set; }
        public TerrainDto? Terrain { get; set; }
    }

    private sealed class PlaceDto
    {
        public string? Plane { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string? Type { get; set; }
        public int Number { get; set; }
        public string? Name { get; set; }
    }

    private sealed class TerrainDto
    {
        public List<string>? Names { get; set; }
        public List<RangeDto>? Ranges { get; set; }
        public int Other { get; set; }
        [JsonPropertyName("netherworld_overrides")] public List<RangeDto>? NetherworldOverrides { get; set; }
        [JsonPropertyName("blocked_from")] public int? BlockedFrom { get; set; }
        [JsonPropertyName("no_encounter")] public List<int>? NoEncounter { get; set; }
    }

    private sealed class RangeDto
    {
        public int First { get; set; }
        public int Last { get; set; }
        public int Terrain { get; set; }
    }

    private sealed class ScrollExtrasDto
    {
        [JsonPropertyName("item_records")] public Dictionary<string, List<int>>? ItemRecords { get; set; }
        [JsonPropertyName("page_layout")] public string? PageLayout { get; set; }
    }
}

/// <summary>III's wilderness encounter numbers (<c>rule-tables.json</c> <c>wilderness_encounters</c>).</summary>
public sealed record Phantasie3WildernessTables(
    int Chance, int NightBelow, int Kind2Below, int SleepingBelow, int NoticePerLevel, int SurpriseBelow,
    int PartySurpriseBelow, int RunAwayBelow, IReadOnlyList<int> AreaLevels, IReadOnlyList<int> TerrainOffsets);

/// <summary>One of Phantasie III's nine skills in record order (<c>FORMAT.md</c> §5).</summary>
/// <param name="SeedStat">The stat of the <c>2·stat + 30</c> seed; null for Fire Bow (its own formula).</param>
/// <param name="Table">The class table in <see cref="Phantasie3RuleTables.SkillTables"/>; null for Fire Bow (none).</param>
public sealed record Phantasie3Skill(string Id, string Name, string? SeedStat, string? Table);

/// <summary>A cumulative hit-location row (<c>DS:0x12cc</c>, <c>logic-specs/body-areas.md</c>).</summary>
public sealed record HitLocationRow(string Use, IReadOnlyList<int> Cumulative);

/// <summary>
/// Phantasie III's own tables (<c>GameData/phantasie3/rule-tables.json</c>, built by <c>tools/scripts/build_p3_gamedata.py</c>
/// from <c>MAIN.EXE</c>'s DGROUP and the specs): skills, class skill tables, HP dice, training curves, body areas,
/// social classes, the class kit, score weights and the decided limits. Data for Core's III rules, not formulas. Class
/// arrays use III's class order (<see cref="Classes"/>: Fighter 0 … Wizard 5); <see cref="CoreClass"/> / <see cref="CoreRace"/>
/// map III numbers onto Core's (Phantasie I) enum names.
/// </summary>
public sealed class Phantasie3RuleTables
{
    public const string FileName = "rule-tables.json";

    public required IReadOnlyList<string> Classes { get; init; }
    public required IReadOnlyDictionary<int, string> CoreClass { get; init; }
    public required IReadOnlyDictionary<int, string> CoreRace { get; init; }
    public required IReadOnlyList<Phantasie3Skill> Skills { get; init; }

    /// <summary>Fire Bow seed: <c>base + per_level·L + per_dex·DEX + per_str·STR</c> + class / race bonus.</summary>
    public required (int Base, int PerLevel, int PerDex, int PerStr) FireBowSeed { get; init; }
    public required IReadOnlyDictionary<string, int> FireBowClassBonus { get; init; }
    public required IReadOnlyDictionary<int, int> FireBowRaceBonus { get; init; }
    public required int SkillCap { get; init; }
    public required int TrainingsPerLevel { get; init; }

    /// <summary>Skill id → class name → values for levels 1..15.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<int>>> SkillTables { get; init; }

    /// <summary>Class name → HP dice for levels 1..22.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<int>> HpDice { get; init; }

    /// <summary>G[level], level 0..21.</summary>
    public required IReadOnlyList<long> TrainingXpCurve { get; init; }

    /// <summary>T[level], level 0..20 (16..20 a DOS data overrun).</summary>
    public required IReadOnlyList<int> TrainingGoldCurve { get; init; }
    public required int MaxLevel { get; init; }
    public required IReadOnlyList<string> BodyAreas { get; init; }
    public required IReadOnlyList<string> BodyStates { get; init; }
    public required IReadOnlyList<string> HitLocationColumns { get; init; }
    public required IReadOnlyList<HitLocationRow> HitLocationRows { get; init; }
    public required IReadOnlyList<int> CharacterAreaForColumn { get; init; }
    public required IReadOnlyList<int> TallMonsterIds { get; init; }
    public required IReadOnlyList<int> LimblessMonsterIds { get; init; }

    /// <summary>The party's Fireflash I–IV (D3-075): the manual's top damage, DOS's factor t and DOS's caps.</summary>
    public required IReadOnlyList<int> FireflashManualMax { get; init; }
    public required IReadOnlyList<int> FireflashDosFactor { get; init; }
    public required IReadOnlyList<int> FireflashDosCap { get; init; }
    public required IReadOnlyList<string> SocialClasses { get; init; }
    public required IReadOnlyList<int> SocialClassThresholds { get; init; }
    public required int SocialClassGoldPerClass { get; init; }
    public required IReadOnlyList<int> SocialClassBaseRaces { get; init; }
    public required (int BaseRaces, int Others) SocialClassRollDie { get; init; }

    /// <summary>The kit every new character wears: shield, armour, weapon, and the bow by class (<c>default</c> otherwise).</summary>
    public required (int Shield, int Armor, int Weapon) Kit { get; init; }
    public required IReadOnlyDictionary<string, int> KitBow { get; init; }

    /// <summary>Class name → first spell number (0 = none).</summary>
    public required IReadOnlyDictionary<string, int> FirstSpell { get; init; }
    public required IReadOnlyList<int> ScoreWeights { get; init; }
    public required IReadOnlyList<int> FinalBossIds { get; init; }
    public required (int Manual, int Dos) FoundItemLimit { get; init; }
    public required (int Records, int Characters) DosRoster { get; init; }

    /// <summary>
    /// D3-032: skill table rows as Phantasie I has them where III's differ (skill id → class → 15 levels); empty when the
    /// file has none.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<int>>> SkillTableFixes { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<int>>>();

    /// <summary>The wilderness encounter block (s83, root <c>0xde54</c>); null when the file has none.</summary>
    public Phantasie3WildernessTables? Wilderness { get; init; }

    /// <summary>Entrance x of dungeon n at index n − 1 (y = 0); empty when the file has none.</summary>
    public IReadOnlyList<int> DungeonEntranceX { get; init; } = [];

    /// <summary>D3-033: Apple II's Armory restock (<c>armory_restock</c>); null when the file has none.</summary>
    public P3ArmoryRestockRule? ArmoryRestock { get; init; }

    /// <summary>D3-071: the found item a paying town entry drops (<c>town_entry_payout.dropped_found_item</c>); null when absent.</summary>
    public int? TownEntryDroppedItem { get; init; }

    /// <summary>The I / II → III transfer's values (<c>transfer</c>); null when the file has none.</summary>
    public P3TransferRule? Transfer { get; init; }

    /// <summary>Creation age (<c>creation_age</c>): DOS race index → (Mul, Div), the others, roll base, offset; null when absent.</summary>
    public (IReadOnlyDictionary<int, (int Mul, int Div)> Factors, (int Mul, int Div) Other, int RollBase, int Offset)? CreationAge
    {
        get; init;
    }

    public int KitBowFor(string className) => KitBow.TryGetValue(className, out var b) ? b : KitBow["default"];

    public static Phantasie3RuleTables Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        try
        {
            return Parse(doc.RootElement).Validate();
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{path}: {ex.Message}", ex);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"{path}: {ex.Message}", ex);
        }
    }

    private static Phantasie3RuleTables Parse(JsonElement r)
    {
        static List<int> Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToList();
        static List<string> Strs(JsonElement e) => e.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        static Dictionary<string, IReadOnlyList<int>> ByClass(JsonElement e) =>
            e.EnumerateObject().Where(p => !p.Name.StartsWith('_')).ToDictionary(p => p.Name, p => (IReadOnlyList<int>)Ints(p.Value));
        static Dictionary<int, string> IntKeys(JsonElement e) =>
            e.EnumerateObject().Where(p => !p.Name.StartsWith('_'))
                .ToDictionary(p => int.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture), p => p.Value.GetString() ?? "");
        static Dictionary<string, int> NamedInts(JsonElement e) =>
            e.EnumerateObject().Where(p => !p.Name.StartsWith('_')).ToDictionary(p => p.Name, p => p.Value.GetInt32());

        var sk = r.GetProperty("skills");
        var seed = sk.GetProperty("fire_bow_seed");
        var body = r.GetProperty("body");
        var soc = r.GetProperty("social_classes");
        var kit = r.GetProperty("class_kit");
        var die = soc.GetProperty("roll_die_by_race_group");
        return new Phantasie3RuleTables
        {
            Classes = Strs(r.GetProperty("classes")),
            CoreClass = IntKeys(r.GetProperty("core_class")),
            CoreRace = IntKeys(r.GetProperty("core_race")),
            Skills = sk.GetProperty("order").EnumerateArray().Select(s => new Phantasie3Skill(
                s.GetProperty("id").GetString() ?? throw new InvalidDataException("a skill has no id."),
                s.GetProperty("name").GetString() ?? "",
                s.GetProperty("seed_stat").GetString(), s.GetProperty("table").GetString())).ToList(),
            FireBowSeed = (seed.GetProperty("base").GetInt32(), seed.GetProperty("per_level").GetInt32(),
                seed.GetProperty("per_dex").GetInt32(), seed.GetProperty("per_str").GetInt32()),
            FireBowClassBonus = NamedInts(seed.GetProperty("class_bonus")),
            FireBowRaceBonus = NamedInts(seed.GetProperty("race_bonus"))
                .ToDictionary(kv => int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture), kv => kv.Value),
            SkillCap = sk.GetProperty("cap").GetInt32(),
            TrainingsPerLevel = sk.GetProperty("trainings_per_level").GetInt32(),
            SkillTables = r.GetProperty("skill_tables").GetProperty("by_skill").EnumerateObject()
                .ToDictionary(p => p.Name, p => (IReadOnlyDictionary<string, IReadOnlyList<int>>)ByClass(p.Value)),
            HpDice = ByClass(r.GetProperty("hp_dice").GetProperty("by_class")),
            TrainingXpCurve = r.GetProperty("training_xp_curve").GetProperty("values").EnumerateArray()
                .Select(x => x.GetInt64()).ToList(),
            TrainingGoldCurve = Ints(r.GetProperty("training_gold_curve").GetProperty("values")),
            MaxLevel = r.GetProperty("max_level").GetProperty("value").GetInt32(),
            BodyAreas = Strs(body.GetProperty("areas")),
            BodyStates = Strs(body.GetProperty("states")),
            HitLocationColumns = Strs(body.GetProperty("hit_location_columns")),
            HitLocationRows = body.GetProperty("hit_location_rows").EnumerateArray()
                .Select(h => new HitLocationRow(h.GetProperty("use").GetString() ?? "", Ints(h.GetProperty("cumulative"))))
                .ToList(),
            CharacterAreaForColumn = Ints(body.GetProperty("character_area_for_column")),
            TallMonsterIds = Ints(body.GetProperty("tall_monster_ids")),
            LimblessMonsterIds = Ints(body.GetProperty("limbless_monster_ids")),
            FireflashManualMax = Ints(r.GetProperty("fireflash").GetProperty("manual_max")),
            FireflashDosFactor = Ints(r.GetProperty("fireflash").GetProperty("dos_factor")),
            FireflashDosCap = Ints(r.GetProperty("fireflash").GetProperty("dos_cap")),
            SocialClasses = Strs(soc.GetProperty("names")),
            SocialClassThresholds = Ints(soc.GetProperty("thresholds")),
            SocialClassGoldPerClass = soc.GetProperty("gold_per_class").GetInt32(),
            SocialClassBaseRaces = Ints(soc.GetProperty("base_races")),
            SocialClassRollDie = (die.GetProperty("base_races").GetInt32(), die.GetProperty("others").GetInt32()),
            Kit = (kit.GetProperty("shield").GetInt32(), kit.GetProperty("armor").GetInt32(), kit.GetProperty("weapon").GetInt32()),
            KitBow = NamedInts(kit.GetProperty("bow")),
            FirstSpell = NamedInts(kit.GetProperty("first_spell")),
            ScoreWeights = Ints(r.GetProperty("score_weights").GetProperty("values")),
            FinalBossIds = Ints(r.GetProperty("final_bosses").GetProperty("ids")),
            FoundItemLimit = (r.GetProperty("found_items").GetProperty("manual").GetInt32(),
                r.GetProperty("found_items").GetProperty("dos").GetInt32()),
            DosRoster = (r.GetProperty("roster").GetProperty("dos_records").GetInt32(),
                r.GetProperty("roster").GetProperty("dos_characters").GetInt32()),
            SkillTableFixes = r.TryGetProperty("skill_table_fixes", out var fixes)
                ? fixes.GetProperty("by_skill").EnumerateObject()
                    .ToDictionary(p => p.Name, p => (IReadOnlyDictionary<string, IReadOnlyList<int>>)ByClass(p.Value))
                : new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<int>>>(),
            Wilderness = r.TryGetProperty("wilderness_encounters", out var we)
                ? new Phantasie3WildernessTables(
                    we.GetProperty("chance").GetInt32(), we.GetProperty("night_below").GetInt32(),
                    we.GetProperty("kind2_below").GetInt32(), we.GetProperty("sleeping_below").GetInt32(),
                    we.GetProperty("notice_per_level").GetInt32(), we.GetProperty("surprise_below").GetInt32(),
                    we.GetProperty("party_surprise_below").GetInt32(), we.GetProperty("run_away_below").GetInt32(),
                    Ints(we.GetProperty("area_levels")), Ints(we.GetProperty("terrain_offsets")))
                : null,
            DungeonEntranceX = r.TryGetProperty("dungeon_entries", out var de) ? Ints(de.GetProperty("x")) : [],
            ArmoryRestock = r.TryGetProperty("armory_restock", out var ar)
                ? new P3ArmoryRestockRule(ar.GetProperty("wipes").GetInt32(), ar.GetProperty("wipe_ids").GetInt32(),
                    ar.GetProperty("picks").GetInt32(), ar.GetProperty("level_cap").GetInt32(), Ints(ar.GetProperty("pool")),
                    Ints(ar.GetProperty("removed")).ToHashSet())
                : null,
            TownEntryDroppedItem = r.TryGetProperty("town_entry_payout", out var tp)
                && tp.TryGetProperty("dropped_found_item", out var drop) ? drop.GetInt32() : null,
            Transfer = r.TryGetProperty("transfer", out var tr)
                ? new P3TransferRule(tr.GetProperty("social_class").GetInt32(), tr.GetProperty("xp_per_level").GetInt32(),
                    tr.GetProperty("bank_gold_divisor").GetInt32(), tr.GetProperty("dos_undead_level").GetInt32(),
                    tr.GetProperty("wand_item").GetInt32(), tr.GetProperty("wand_quest_bit").GetInt32(),
                    tr.GetProperty("wand_score_above").GetInt32())
                : null,
            CreationAge = r.TryGetProperty("creation_age", out var ca)
                ? (ca.GetProperty("factor_by_race").EnumerateObject().ToDictionary(
                        p => int.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture),
                        p => (p.Value[0].GetInt32(), p.Value[1].GetInt32())),
                    (ca.GetProperty("other_factor")[0].GetInt32(), ca.GetProperty("other_factor")[1].GetInt32()),
                    ca.GetProperty("roll_base").GetInt32(), ca.GetProperty("offset").GetInt32())
                : null,
        };
    }

    private Phantasie3RuleTables Validate()
    {
        if (Classes.Count != 6)
            throw new InvalidDataException("classes must list six classes.");
        if (Skills.Count != 9)
            throw new InvalidDataException("skills.order must list nine skills.");
        foreach (var s in Skills.Where(s => s.Table is not null))
        {
            if (!SkillTables.TryGetValue(s.Table!, out var t) || Classes.Any(c => !t.TryGetValue(c, out var row) || row.Count != 15))
                throw new InvalidDataException($"skill table '{s.Table}' needs 15 levels for every class.");
        }
        if (Classes.Any(c => !HpDice.TryGetValue(c, out var d) || d.Count != 22))
            throw new InvalidDataException("hp_dice needs 22 levels for every class.");
        if (BodyAreas.Count != 6 || BodyStates.Count != 4)
            throw new InvalidDataException("body needs six areas and four states.");
        if (HitLocationRows.Any(h => h.Cumulative.Count != HitLocationColumns.Count || h.Cumulative[^1] != 100))
            throw new InvalidDataException("every hit_location row must end at 100 over all columns.");
        if (CharacterAreaForColumn.Count != 6 || CharacterAreaForColumn.Distinct().Count() != 6)
            throw new InvalidDataException("character_area_for_column must map six columns onto six areas.");
        if (ScoreWeights.Count != 8)
            throw new InvalidDataException("score_weights needs eight values.");
        if (!KitBow.ContainsKey("default"))
            throw new InvalidDataException("class_kit.bow needs a default.");
        if (ArmoryRestock is { } ar && (ar.Pool.Count < ar.LevelCap * 3 || ar.WipeIds > 181))
            throw new InvalidDataException("armory_restock.pool must cover level_cap * 3 positions and wipe_ids at most 181.");
        return this;
    }
}
