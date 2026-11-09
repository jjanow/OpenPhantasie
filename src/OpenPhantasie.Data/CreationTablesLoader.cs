using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Loads TWNS.INT-derived creation tables from creation-tables.json.
/// </summary>
public static class CreationTablesLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly string[] ClassOrder =
        ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"];

    private static readonly string[] RaceOrder =
    [
        "HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE",
        "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC", "GOBLIN", "LIZ.MAN", "MINOTAUR",
    ];

    private static readonly string[] AttrOrder = ["STR", "INT", "DEX", "CON", "CHA"];

    public static CharacterCreationTables Load(string path) => Load(path, requireLifespans: true);

    /// <summary>
    /// <paramref name="requireLifespans"/> = false lets a game without remaster lifespan bands load (Phantasie II: D-003 is a
    /// Phantasie I decision); <see cref="CharacterCreationTables.LifespanBandsByRace"/> is then empty.
    /// </summary>
    public static CharacterCreationTables Load(string path, bool requireLifespans)
    {
        var dto = JsonSerializer.Deserialize<CreationFileDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Failed to parse {path}");

        return new CharacterCreationTables
        {
            HpFactorsByClass = MapClassIntLists(dto.HpFactors?.ByClass),
            MpCeilingByClass = MapClassIntLists(dto.MpCeiling?.ByClass),
            SkillCoefficientsByClassLevel = MapClassLevelTable(dto.SkillCoefficients?.ByClassLevel),
            SpellLevelsByClass = MapClassIntLists(dto.SpellLevels?.ByClass),
            LifespanBandsByRace = !requireLifespans && dto.LifespanBands is null
                ? []
                : MapLifespanBands(dto.LifespanBands?.ByRace),
            ClassAttributeModifiers = MapClassAttrMods(dto.ClassAttributeModifiers?.ByClass),
            RaceAttributeModifiers = MapRaceAttrMods(dto.RaceAttributeModifiers?.ByRace),
            Dos = MapDosTables(dto.DosTables),
            SharedAging = SharedAgingLoader.Shipped, // QoL shared-aging (13-aging-normalised.md)
        };
    }

    /// <summary>
    /// <c>dos_tables</c> (P3.2 Authentic mode): the original code's cells the manual changed and DOS's lifespan factors
    /// (I: DOS; II: the Atari ST, D2-015). Keys starting with <c>_</c> are notes. Null when the file has no block. Phantasie I
    /// states all three parts; a game may leave out <c>race_attribute_modifiers</c> or <c>spell_levels</c> (no cells) and
    /// <c>lifespan_factors</c> (no DOS lifespan rule: <see cref="DosCreationTables.LifespanFactorsByRace"/> is empty).
    /// </summary>
    private static DosCreationTables? MapDosTables(Dictionary<string, JsonElement>? dos)
    {
        if (dos is null)
            return null;
        var raceMods = new List<(int, int, int)>();
        foreach (var (race, attrs) in OptionalEntries(dos, "race_attribute_modifiers"))
            foreach (var (attr, value) in attrs.EnumerateObject().Where(p => !p.Name.StartsWith('_')).Select(p => (p.Name, p.Value)))
                raceMods.Add((IndexOf(RaceOrder, race), IndexOf(AttrOrder, attr), value.GetInt32()));
        var spells = new List<(int, int, int)>();
        foreach (var (cls, slots) in OptionalEntries(dos, "spell_levels"))
            foreach (var p in slots.EnumerateObject().Where(p => !p.Name.StartsWith('_')))
                spells.Add((IndexOf(ClassOrder, cls), int.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture), p.Value.GetInt32()));
        if (!dos.ContainsKey("lifespan_factors"))
            return new DosCreationTables(raceMods, spells, []);
        var factors = Entries(dos, "lifespan_factors").ToDictionary(e => e.Key, e => e.Value);
        var lifespan = RaceOrder.Select(r =>
        {
            if (!factors.TryGetValue(r, out var f) || f.GetArrayLength() != 2 || f[1].GetInt32() <= 0)
                throw new InvalidDataException($"dos_tables.lifespan_factors needs [num, den] for {r}.");
            return (f[0].GetInt32(), f[1].GetInt32());
        }).ToList();
        return new DosCreationTables(raceMods, spells, lifespan);
    }

    private static IEnumerable<(string Key, JsonElement Value)> OptionalEntries(Dictionary<string, JsonElement> block, string name) =>
        block.ContainsKey(name) ? Entries(block, name) : [];

    private static IEnumerable<(string Key, JsonElement Value)> Entries(Dictionary<string, JsonElement> block, string name) =>
        block.TryGetValue(name, out var e)
            ? e.EnumerateObject().Where(p => !p.Name.StartsWith('_')).Select(p => (p.Name, p.Value))
            : throw new InvalidDataException($"dos_tables.{name} missing in creation-tables.json.");

    private static int IndexOf(string[] order, string name)
    {
        var i = Array.IndexOf(order, name);
        return i >= 0 ? i : throw new InvalidDataException($"Unknown name '{name}' in dos_tables.");
    }

    private static IReadOnlyList<IReadOnlyList<int>> MapClassIntLists(Dictionary<string, List<int>>? map)
    {
        if (map is null)
            throw new InvalidDataException("Missing class table in creation-tables.json.");

        return ClassOrder.Select(c =>
        {
            if (!map.TryGetValue(c, out var list) || list.Count == 0)
                throw new InvalidDataException($"Missing creation table row for class {c}.");
            return (IReadOnlyList<int>)list.ToList();
        }).ToList();
    }

    private static IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> MapClassLevelTable(
        Dictionary<string, List<List<int>>>? map)
    {
        if (map is null)
            throw new InvalidDataException("Missing skill coefficient table in creation-tables.json.");

        return ClassOrder.Select(c =>
        {
            if (!map.TryGetValue(c, out var rows) || rows.Count != 15 || rows.Any(r => r.Count != 10))
                throw new InvalidDataException($"Skill coefficients for {c} must be 15 levels × 10 slots.");
            return (IReadOnlyList<IReadOnlyList<int>>)rows.Select(r => (IReadOnlyList<int>)r.ToList()).ToList();
        }).ToList();
    }

    private static IReadOnlyList<(int Lo, int Hi)> MapLifespanBands(Dictionary<string, LifespanBandDto>? map)
    {
        if (map is null)
            throw new InvalidDataException("Missing lifespan_bands in creation-tables.json.");

        return RaceOrder.Select(r =>
        {
            if (!map.TryGetValue(r, out var band) || band.LoYears <= 0 || band.HiYears < band.LoYears)
                throw new InvalidDataException($"Missing or invalid lifespan band for {r}.");
            return (band.LoYears, band.HiYears);
        }).ToList();
    }

    private static IReadOnlyList<IReadOnlyList<int>> MapClassAttrMods(
        Dictionary<string, Dictionary<string, int>>? map)
    {
        if (map is null)
            throw new InvalidDataException("Missing class attribute modifiers.");

        return ClassOrder.Select(c =>
        {
            if (!map.TryGetValue(c, out var row))
                throw new InvalidDataException($"Missing class modifiers for {c}.");
            return (IReadOnlyList<int>)AttrOrder.Select(a => row[a]).ToList();
        }).ToList();
    }

    private static IReadOnlyList<IReadOnlyList<int>> MapRaceAttrMods(
        Dictionary<string, RaceModDto>? map)
    {
        if (map is null)
            throw new InvalidDataException("Missing race attribute modifiers.");

        return RaceOrder.Select(r =>
        {
            if (!map.TryGetValue(r, out var row) || row.Modifiers is null)
                throw new InvalidDataException($"Missing race modifiers for {r}.");
            return (IReadOnlyList<int>)AttrOrder.Select(a => row.Modifiers[a]).ToList();
        }).ToList();
    }

    private sealed class CreationFileDto
    {
        [JsonPropertyName("hp_factors")]
        public NamedIntTableDto? HpFactors { get; set; }

        [JsonPropertyName("mp_ceiling")]
        public NamedIntTableDto? MpCeiling { get; set; }

        [JsonPropertyName("skill_coefficients")]
        public SkillTableDto? SkillCoefficients { get; set; }

        [JsonPropertyName("spell_levels")]
        public NamedIntTableDto? SpellLevels { get; set; }

        [JsonPropertyName("lifespan_bands")]
        public LifespanTableDto? LifespanBands { get; set; }

        [JsonPropertyName("class_attribute_modifiers")]
        public NamedAttrTableDto? ClassAttributeModifiers { get; set; }

        [JsonPropertyName("race_attribute_modifiers")]
        public RaceTableDto? RaceAttributeModifiers { get; set; }

        [JsonPropertyName("dos_tables")]
        public Dictionary<string, JsonElement>? DosTables { get; set; }
    }

    private sealed class NamedIntTableDto
    {
        [JsonPropertyName("by_class")]
        public Dictionary<string, List<int>>? ByClass { get; set; }
    }

    private sealed class SkillTableDto
    {
        [JsonPropertyName("by_class_level")]
        public Dictionary<string, List<List<int>>>? ByClassLevel { get; set; }
    }

    private sealed class LifespanTableDto
    {
        [JsonPropertyName("by_race")]
        public Dictionary<string, LifespanBandDto>? ByRace { get; set; }
    }

    private sealed class LifespanBandDto
    {
        [JsonPropertyName("lo_years")]
        public int LoYears { get; set; }

        [JsonPropertyName("hi_years")]
        public int HiYears { get; set; }
    }

    private sealed class NamedAttrTableDto
    {
        [JsonPropertyName("by_class")]
        public Dictionary<string, Dictionary<string, int>>? ByClass { get; set; }
    }

    private sealed class RaceTableDto
    {
        [JsonPropertyName("by_race")]
        public Dictionary<string, RaceModDto>? ByRace { get; set; }
    }

    private sealed class RaceModDto
    {
        public Dictionary<string, int>? Modifiers { get; set; }
    }
}
