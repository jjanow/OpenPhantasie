using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Pipelines.P3Apple2;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// III's tables from the Apple II disks alone (<see cref="P3Apple2Tables"/>, s212): the C# extract equals the committed
/// Python extract (<c>tools/scripts/extract_a2_p3_tables.py</c>) from the v3.0 originals; the [cr] v1.0 sides give the
/// same tables but for the scroll files the crack damaged; <see cref="P3Apple2Tables.Build"/> gives the ten files DOS
/// III's pipeline writes. <see cref="P3Apple2ImportTests"/> checks the shipped cells (DOS's values) and the import.
/// </summary>
public class P3Apple2TablesTests
{
    public const string WozA = "reverse-engineering/apple2/original-images/phantasie3/woz-original-v3.0/phantasie3-v3.0-disk1-side-a-boot.woz";
    public const string WozB = "reverse-engineering/apple2/original-images/phantasie3/woz-original-v3.0/phantasie3-v3.0-disk1-side-b-utilities.woz";
    public const string Cr1A = "reverse-engineering/apple2/original-images/phantasie3/Phantasie_III_1987_SSI_cr_Disk_1_of_3_Side_A.dsk";
    public const string Cr1B = "reverse-engineering/apple2/original-images/phantasie3/Phantasie_III_1987_SSI_cr_Disk_1_of_3_Side_B.dsk";
    private const string Extracted = "reverse-engineering/apple2/phantasie3/extracted";

    public static readonly string[] Tables = P3AmigaTablesTests.Tables;

    /// <summary>Run <paramref name="body"/> with an import context for the p3-apple2 detection of <paramref name="sources"/>.</summary>
    public static T WithContext<T>(Func<ImportContext, T> body, string? shippedRoot = null, params string[] sources)
    {
        var scan = ImportRunner.Scan(sources.Select(Repo.Path).ToList());
        var detection = scan.Detections.Single(d => d.Profile.Id == "p3-apple2");
        Assert.Equal(DetectionStatus.Ready, detection.Status);
        var staging = Directory.CreateTempSubdirectory("phantasie-import-test-").FullName;
        try
        {
            var session = new ImportSession
            {
                ShippedGameDataRoot = shippedRoot ?? Repo.GameData, StagingRoot = staging, Detections = scan.Detections,
            };
            return body(new ImportContext(session, detection));
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
    }

    private static Dictionary<string, JsonObject>? _extract;

    private static Dictionary<string, JsonObject> Extract() =>
        _extract ??= WithContext(ctx => new P3Apple2Tables(ctx.Files).Extract(), null, WozA, WozB);

    [RequiresFilesTheory(WozA, WozB, Extracted)]
    [MemberData(nameof(ExtractData))]
    public void Extract_EqualsThePythonExtract(string file)
    {
        var python = JsonNode.Parse(File.ReadAllBytes(Repo.Path($"{Extracted}/{file}")))!;
        var diffs = P3AmigaTablesTests.Differences(python, Extract()[file], "$");
        Assert.True(diffs.Count == 0, $"{file}: C# differs from the Python extract in\n" + string.Join("\n", diffs.Take(40)));
    }

    public static TheoryData<string> ExtractData() => new(["monsters.json", "items.json", "spells.json", "names.json",
        "towns.json", "scrolls.json", "world.json", "dungeons.json", "rule-tables.json"]);

    /// <summary>The v1.0 [cr] sides read the same, but for five scroll files the crack replaced with other scrolls'
    /// pages (SC7, SC9, SC11, SC17, SC20: FORMAT.md 'Versions').</summary>
    [RequiresFilesFact(Cr1A, Cr1B, WozA, WozB)]
    public void CrackedV10_SameTables_ButFiveScrolls()
    {
        var cr = WithContext(ctx => new P3Apple2Tables(ctx.Files).Extract(), null, Cr1A, Cr1B);
        foreach (var (file, node) in Extract())
        {
            var diffs = P3AmigaTablesTests.Differences(node, cr[file], "$");
            if (file != "scrolls.json")
                Assert.True(diffs.Count == 0, $"{file}: {string.Join(", ", diffs.Take(10))}");
            else
                Assert.Equal(["SC11", "SC17", "SC20", "SC7", "SC9"],
                    diffs.Where(d => d.StartsWith("$.records[", StringComparison.Ordinal) && d.EndsWith("].text", StringComparison.Ordinal))
                        .Select(d => (string)node["records"]![int.Parse(d[10..d.IndexOf(']')], System.Globalization.CultureInfo.InvariantCulture)]!["file"]!)
                        .Order(StringComparer.Ordinal));
        }
    }

    [RequiresFilesFact(WozA, WozB)]
    public void Build_GivesEveryTableDosIIIWrites()
    {
        var built = WithContext(P3Apple2Tables.Build, null, WozA, WozB);
        Assert.Equal(Tables.Order(), built.Select(t => t.File).Order());
        Assert.All(built, t => Assert.StartsWith(P3Apple2Tables.Label, (string)t.Node["_source"]!));
        // Optional dump of the raw build for tools/scripts/diff_a2_p3_vs_dos.py --built (the cells file is made from it).
        if (Environment.GetEnvironmentVariable("P3A2_DUMP") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            foreach (var (file, node) in built)
                File.WriteAllText(Path.Combine(dir, file), node.ToJsonString(new System.Text.Json.JsonSerializerOptions
                    { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
    }
}
