using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Pipelines.P3Amiga;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// III's tables from the Amiga disk alone (<see cref="P3AmigaTables.Build"/>, s208; the Amiga's own values): the ten
/// files DOS III's pipeline writes, built by the same <c>P3Build</c> from the Amiga's tables, against the committed
/// DOS-built GameData. Every difference must be one of the listed ones: provenance by pattern, every text, number and
/// content cell by its exact path. That list is the set of Amiga ≠ DOS cells (user s209: DOS for all eight; the import
/// writes DOS's values from <c>phantasie3/amiga-import.json</c>, <see cref="P3AmigaImportTests"/>;
/// <c>reverse-engineering/amiga/phantasie3/FORMAT.md</c> section "Game tables for a standalone import").
/// </summary>
public class P3AmigaTablesTests
{
    private const string Adf = "reverse-engineering/amiga/original-images/phantasie3/phantasie3-v1.0-1987-ssi.adf";
    private const string DosGameData = "src/OpenPhantasie.Data/GameData/phantasie3";

    public static readonly string[] Tables =
    [
        "monsters.json", "items.json", "spells.json", "names.json", "overworld.json", "dungeons.json", "scrolls.json",
        "towns.json", "elementals.json", "rule-tables.json",
    ];

    private static Dictionary<string, JsonObject>? _built;

    private static Dictionary<string, JsonObject> Built()
    {
        if (_built is not null)
            return _built;
        var scan = ImportRunner.Scan([Repo.Path(Adf)]);
        var detection = scan.Detections.Single(d => d.Profile.Id == "p3-amiga-1.0");
        var staging = Directory.CreateTempSubdirectory("phantasie-import-test-").FullName;
        try
        {
            var session = new ImportSession { ShippedGameDataRoot = Repo.GameData, StagingRoot = staging, Detections = scan.Detections };
            var ctx = new ImportContext(session, detection);
            return _built = P3AmigaTables.Build(ctx).ToDictionary(t => t.File, t => t.Node);
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
    }

    private static JsonNode Dos(string file) => JsonNode.Parse(File.ReadAllBytes(Repo.Path($"{DosGameData}/{file}")))!;

    /// <summary>Provenance only (the files and addresses read, our notes): every other difference is listed below.</summary>
    private static readonly Regex Provenance = new(
        @"^\$\.(_source|_note|_notes|_layout)(\[\d+\])?$|\._source$|\._note$|\._address$|\.source$",
        RegexOptions.CultureInvariant);

    private static readonly string[] P3Classes = ["Fighter", "Thief", "Priest", "Ranger", "Monk", "Wizard"];

    private static readonly int[] ArticleItems =
        [142, 145, 148, 149, ..Enumerable.Range(155, 11), ..Enumerable.Range(167, 12)];

    /// <summary>Spell indexes (number − 1): #13-16 Flame Arrow, #53.</summary>
    private static readonly int[] SpellTexts = [12, 13, 14, 15, 52];

    /// <summary>Scandor cells (x, y) where <c>phantasy.set</c>'s map differs from DOS <c>WORLD.DAT</c>.</summary>
    private static readonly (int X, int Y)[] ScandorCells =
    [
        (44, 3), (47, 3), (0, 5), (0, 6), (43, 9), (43, 12), (23, 13), (43, 13), (44, 13), (24, 14), (24, 15), (27, 16),
        (44, 16), (44, 17), (43, 19), (3, 20), (3, 21), (4, 21), (5, 21), (32, 22), (12, 29), (12, 30), (11, 33), (47, 35),
        (48, 35), (36, 36), (37, 36), (24, 37), (44, 37), (46, 40), (22, 42), (37, 42), (47, 43), (47, 44), (22, 45),
        (2, 46), (21, 46), (25, 51), (38, 52), (39, 52), (40, 52), (7, 53), (8, 53), (9, 53), (10, 53), (25, 53), (38, 53),
        (37, 54), (28, 68), (1, 71), (1, 72), (1, 73), (45, 73), (49, 74),
    ];

    /// <summary>
    /// Every number / content cell where the Amiga differs from DOS (FORMAT.md "Game tables", Amiga ≠ DOS): an
    /// Amiga-only import uses DOS's (user s209).
    /// </summary>
    private static readonly Dictionary<string, string[]> Content = new()
    {
        // Text: 27 treasure names with an article, Flame Arrow (#13-16), #53's placeholder; dungeon 2; three inns.
        ["items.json"] = [..ArticleItems.Select(i => $"$.items[{i}].name")],
        ["spells.json"] = [..SpellTexts.Select(i => $"$.spells[{i}].name")],
        ["names.json"] = [..ArticleItems.Select(i => $"$.items[{i}].name"), ..SpellTexts.Select(i => $"$.spells[{i}].name")],
        ["dungeons.json"] = ["$.levels.DNG2.name"],
        ["overworld.json"] =
        [
            // Scandor: the 54 cells of FORMAT.md section 4, Pendragon's (24, 37) among them.
            ..ScandorCells.Select(c => $"$.regions.PLANE0.tiles[{c.Y}][{c.X}]"),
            // Pendragon's square is tile 25 there (DOS 29, a town tile).
            "$.places[18].tile",
            // Names: dungeon 2 (place 6); the Crown, Jolly and Royal Inns (places 15, 16, 25).
            "$.places[6].name", "$.places[15].name", "$.places[16].name", "$.places[25].name",
            // Terrain: 19 names (4 / 5 / 6 and three capitals differ); ranges checked by Terrain_*; other = null.
            "$.terrain.names.length", "$.terrain.names[4]", "$.terrain.names[5]", "$.terrain.names[6]",
            "$.terrain.names[11]", "$.terrain.names[13]", "$.terrain.names[17]", "$.terrain.other",
        ],
        ["scrolls.json"] =
        [
            // Items 123 / 124: Amiga records [1, 2, 3] both; DOS [4] and [3, 4, 5].
            "$.item_records.123.length", "$.item_records.123[0]",
            "$.item_records.124[0]", "$.item_records.124[1]", "$.item_records.124[2]",
        ],
        ["rule-tables.json"] =
        [
            // HP: 21 levels on the Amiga (DOS 22); L16..21 rise.
            ..P3Classes.SelectMany(c => Enumerable.Range(15, 6).Select(i => $"$.hp_dice.by_class.{c}[{i}]")
                .Prepend($"$.hp_dice.by_class.{c}.length")),
            "$.training_xp_curve.values[0]", "$.training_gold_curve.values[0]",
            "$.training_gold_curve.values[16]", "$.training_gold_curve.values[17]", "$.training_gold_curve.values[18]",
            "$.training_gold_curve.values[19]", "$.training_gold_curve.values[20]",
            "$.skill_tables.by_skill.parry.Wizard[14]",
            // Entry 0 is never read; 16-25 are past the 15-entry table, where each version has other data.
            "$.wilderness_encounters.terrain_offsets[0]",
            ..Enumerable.Range(16, 10).Select(i => $"$.wilderness_encounters.terrain_offsets[{i}]"),
        ],
    };

    internal static List<string> Differences(JsonNode? dos, JsonNode? amiga, string path)
    {
        var output = new List<string>();
        void Walk(JsonNode? a, JsonNode? b, string p)
        {
            switch (a)
            {
                case JsonObject oa when b is JsonObject ob:
                    foreach (var k in oa.Select(kv => kv.Key).Union(ob.Select(kv => kv.Key)))
                    {
                        if (!oa.ContainsKey(k) || !ob.ContainsKey(k))
                            output.Add($"{p}.{k}");
                        else
                            Walk(oa[k], ob[k], $"{p}.{k}");
                    }
                    return;
                case JsonArray aa when b is JsonArray ab:
                    if (aa.Count != ab.Count)
                        output.Add($"{p}.length");
                    for (var i = 0; i < Math.Min(aa.Count, ab.Count); i++)
                        Walk(aa[i], ab[i], $"{p}[{i}]");
                    return;
                default:
                    if (!JsonNode.DeepEquals(a, b))
                        output.Add(p);
                    return;
            }
        }
        Walk(dos, amiga, path);
        return output;
    }

    [RequiresFilesFact(Adf, DosGameData)]
    public void AmigaOnly_BuildsEveryTableDosIIIWrites()
    {
        var built = Built();
        Assert.Equal(Tables.OrderBy(t => t), built.Keys.OrderBy(t => t));
    }

    [RequiresFilesTheory(Adf, DosGameData)]
    [MemberData(nameof(TableData))]
    public void AmigaOnly_DiffersFromDosOnlyInTheListedCells(string file)
    {
        var diffs = Differences(Dos(file), Built()[file], "$")
            .Where(d => !Provenance.IsMatch(d) && !d.StartsWith("$.terrain.ranges", StringComparison.Ordinal)).ToHashSet();
        var listed = Content.GetValueOrDefault(file, []).ToHashSet();
        var unexpected = diffs.Except(listed).Order(StringComparer.Ordinal).ToList();
        Assert.True(unexpected.Count == 0, $"{file}: unlisted differences:\n" + string.Join("\n", unexpected.Take(300)));
        // Each listed cell really differs (the list holds no stale entries).
        var stale = listed.Except(diffs).Order(StringComparer.Ordinal).ToList();
        Assert.True(stale.Count == 0, $"{file}: listed but equal: " + string.Join(", ", stale));
    }

    public static TheoryData<string> TableData() => new(Tables);

    /// <summary>The terrain of a tile (0xa4a6): DOS's ranges without the place tiles' three, nothing else changed.</summary>
    [RequiresFilesFact(Adf, DosGameData)]
    public void Terrain_IsDosWithoutThePlaceRanges()
    {
        static List<string> Ranges(JsonNode? n) =>
            n!.AsArray().Select(r => $"{(int)r!["first"]!}-{(int)r["last"]!}:{(int)r["terrain"]!}").ToList();
        var dos = Ranges(Dos("overworld.json")["terrain"]!["ranges"]);
        var amiga = Ranges(Built()["overworld.json"]["terrain"]!["ranges"]);
        Assert.Equal(dos.Except(["26-38:19", "39-46:20", "47-54:21"]), amiga);
        Assert.True(JsonNode.DeepEquals(Dos("overworld.json")["terrain"]!["netherworld_overrides"],
            Built()["overworld.json"]["terrain"]!["netherworld_overrides"]));
    }

    [RequiresFilesFact(Adf, DosGameData)]
    public void Places_AndEveryRecordMatchDos()
    {
        var b = Built();
        // The 36 places, in order, square for square.
        var dosPlaces = Dos("overworld.json")["places"]!.AsArray();
        var amigaPlaces = b["overworld.json"]["places"]!.AsArray();
        Assert.Equal(dosPlaces.Count, amigaPlaces.Count);
        for (var i = 0; i < dosPlaces.Count; i++)
            foreach (var k in new[] { "plane", "x", "y", "type", "number", "tile" })
                if (!(i == 18 && k == "tile")) // Pendragon: tile 25 on the Amiga, DOS 29
                    Assert.True(JsonNode.DeepEquals(dosPlaces[i]![k], amigaPlaces[i]![k]), $"place {i} {k}");
        Assert.Equal(1, (int)dosPlaces[18]!["number"]!);
        Assert.Equal(25, (int)amigaPlaces[18]!["tile"]!);
        // Dungeon records, monsters, stock: byte for byte the same files.
        Assert.DoesNotContain(Differences(Dos("monsters.json")["monsters"], b["monsters.json"]["monsters"], "$"), d => !d.EndsWith(".name"));
        Assert.DoesNotContain(Differences(Dos("dungeons.json")["levels"], b["dungeons.json"]["levels"], "$"), d => !d.EndsWith(".name", StringComparison.Ordinal));
    }
}
