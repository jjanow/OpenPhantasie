using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Pipelines.P3Amiga;
using OpenPhantasie.Importer.Pipelines.P3Apple2;
using static OpenPhantasie.Importer.Tests.P3Apple2TablesTests;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// The Apple-only III import (s212, <c>p3-apple2</c> recipe 1): <see cref="P3Apple2Tables.Write"/> writes the ten tables
/// with DOS III's value in every number / structure cell where the Apple's differ or it has none (the shipped
/// <c>phantasie3/apple2-import.json</c>), keeping the Apple's text, so the result equals the DOS-built GameData but for
/// provenance and text, loads, and starts a game. With DOS III (or the Amiga) imported too, their tables win.
/// </summary>
[Collection(nameof(LocatorCollection))]
public class P3Apple2ImportTests
{
    private const string DosIma = "reverse-engineering/dos/original-images/phantasie3/phantasie3-1988-ssi-360k.ima";
    private const string Adf = "reverse-engineering/amiga/original-images/phantasie3/phantasie3-v1.0-1987-ssi.adf";
    private const string DosGameData = "src/OpenPhantasie.Data/GameData/phantasie3";
    private const string Cells = DosGameData + "/" + P3Apple2Tables.CellsFile;

    private static JsonNode Dos(string file) => JsonNode.Parse(File.ReadAllBytes(Repo.Path($"{DosGameData}/{file}")))!;

    private static JsonArray ShippedCells() => JsonNode.Parse(File.ReadAllBytes(Repo.Path(Cells)))!["dos_cells"]!.AsArray();

    private static (Dictionary<string, JsonObject> Raw, Dictionary<string, JsonObject> Patched, List<string> Warnings)? _built;

    private static (Dictionary<string, JsonObject> Raw, Dictionary<string, JsonObject> Patched, List<string> Warnings) Built(
        string? shippedRoot = null)
    {
        if (shippedRoot is null && _built is { } cached)
            return cached;
        var result = WithContext(ctx =>
        {
            var raw = P3Apple2Tables.Build(ctx).ToDictionary(t => t.File, t => t.Node);
            var warnings = new List<string>();
            var patched = P3Apple2Tables.BuildWithDosValues(ctx, warnings).ToDictionary(t => t.File, t => t.Node);
            return (raw, patched, warnings);
        }, shippedRoot, WozA, WozB);
        if (shippedRoot is null)
            _built = result;
        return result;
    }

    private static readonly Regex Provenance = new(
        @"^\$\.(_source|_note|_notes|_layout)(\[\d+\])?$|\._source$|\._note$|\._address$|\.source$",
        RegexOptions.CultureInvariant);

    /// <summary>The Apple's own text the import keeps (the same list as <c>diff_a2_p3_vs_dos.py</c>'s TEXT): names,
    /// dungeon messages and the record size their text area sets, scroll pages.</summary>
    private static readonly Regex Text = new(
        @"\.name$|\.names\[\d+\]$|^\$\.levels\.DNG\d+\.(messages\.|size$)|^\$\.scrolls\[\d+\]\.(text|lines)",
        RegexOptions.CultureInvariant);

    /// <summary>Every shipped cell: its <c>dos</c> value is DOS III's GameData, its <c>apple</c> value the Apple build's.</summary>
    [RequiresFilesFact(WozA, WozB, Cells, DosGameData + "/monsters.json")]
    public void ShippedCells_HoldDosIIIsValue_AndTheAppleBuildsValue()
    {
        var raw = Built().Raw;
        var bad = new List<string>();
        foreach (var cell in ShippedCells())
        {
            var file = (string)cell!["file"]!;
            var path = cell["path"]!.AsArray();
            var where = $"{file} {path.ToJsonString()}";
            if (CellPatches.Walk(Dos(file), path) is not { } dos || CellPatches.Walk(raw[file], path) is not { } apple)
            {
                bad.Add($"{where}: path not found");
                continue;
            }
            JsonNode? Value(JsonNode? node)
            {
                if (cell["byte"] is not { } b)
                    return node?.DeepClone();
                var bytes = Convert.FromHexString((string)node!);
                return (int)b < bytes.Length ? bytes[(int)b] : null;
            }
            if (!JsonNode.DeepEquals(Value(CellPatches.Get(dos.Parent, dos.Key)), cell["dos"]))
                bad.Add($"{where}: dos is not DOS III's value");
            if (!JsonNode.DeepEquals(Value(CellPatches.Get(apple.Parent, apple.Key)), cell["apple"]))
                bad.Add($"{where}: apple is not the Apple build's value");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(50)));
    }

    /// <summary>With the cells applied, every table equals DOS III's but for provenance and the Apple's own text; the
    /// dungeon records equal DOS's byte for byte up to the text pointers (0x593).</summary>
    [RequiresFilesTheory(WozA, WozB, Cells, DosGameData + "/monsters.json")]
    [MemberData(nameof(TableData))]
    public void WithDosValues_EqualsTheDosGameData_ButForProvenanceAndText(string file)
    {
        var (_, patched, warnings) = Built();
        Assert.Empty(warnings);
        var diffs = P3AmigaTablesTests.Differences(Dos(file), patched[file], "$")
            .Where(d => !Provenance.IsMatch(d) && !Text.IsMatch(d) && !Regex.IsMatch(d, @"^\$\.levels\.DNG\d+\.raw_hex$"))
            .ToList();
        Assert.True(diffs.Count == 0, $"{file}: differs from DOS III in\n" + string.Join("\n", diffs.Take(100)));
        if (file == "dungeons.json")
            foreach (var (id, level) in Dos(file)["levels"]!.AsObject())
            {
                var dos = Convert.FromHexString((string)level!["raw_hex"]!);
                var apple = Convert.FromHexString((string)patched[file]["levels"]![id]!["raw_hex"]!);
                Assert.True(dos.AsSpan(0, 0x593).SequenceEqual(apple.AsSpan(0, 0x593)), id);
            }
    }

    public static TheoryData<string> TableData() => new(Tables);

    /// <summary>The text differences kept are text: names, messages, scroll pages (no number hides among them).</summary>
    [RequiresFilesFact(WozA, WozB, Cells, DosGameData + "/monsters.json")]
    public void KeptText_IsStrings()
    {
        var (_, patched, _) = Built();
        foreach (var file in Tables)
            foreach (var d in P3AmigaTablesTests.Differences(Dos(file), patched[file], "$").Where(d => Text.IsMatch(d)))
            {
                var node = Resolve(patched[file], d);
                Assert.True(node is null || node.GetValueKind() is System.Text.Json.JsonValueKind.String
                    or System.Text.Json.JsonValueKind.Object or System.Text.Json.JsonValueKind.Array
                    || d.EndsWith(".size", StringComparison.Ordinal) || d.EndsWith(".length", StringComparison.Ordinal), $"{file} {d}");
            }

        static JsonNode? Resolve(JsonNode root, string path)
        {
            JsonNode? n = root;
            foreach (Match m in Regex.Matches(path[1..], @"\.([^.\[]+)|\[(\d+)\]"))
            {
                if (m.Groups[1].Value == "length")
                    return null;
                n = m.Groups[1].Success ? n?[m.Groups[1].Value] : n?[int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)];
            }
            return n;
        }
    }

    /// <summary>The public repo's shipped folder (III's dropped files left out, the overlays' original fields removed)
    /// gives the same tables: the import reads nothing of DOS III's but the shipped cells.</summary>
    [RequiresFilesFact(WozA, WozB, Cells)]
    public void PublicShipped_GivesTheSameTables()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3a2-public-").FullName;
        try
        {
            var shipped = Path.Combine(dir, "GameData");
            var dropped = new HashSet<string>(StringComparer.Ordinal)
            {
                "dungeons.json", "elementals.json", "items.json", "monsters.json", "names.json", "overworld.json",
                "scrolls.json", "spells.json", "graphics/fonts/amiga-topaz-8.json",
            };
            foreach (var file in Directory.EnumerateFiles(Repo.GameData, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(Repo.GameData, file).Replace('\\', '/');
                if (rel.StartsWith("phantasie3/", StringComparison.Ordinal) && dropped.Contains(rel["phantasie3/".Length..]))
                    continue;
                var dest = Path.Combine(shipped, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest);
            }
            void Strip(string file, Action<JsonObject> strip)
            {
                var path = Path.Combine(shipped, "phantasie3", file);
                var doc = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
                strip(doc);
                File.WriteAllText(path, doc.ToJsonString());
            }
            Strip("rule-tables.json", d =>
            {
                foreach (var key in new[] { "classes", "core_class", "core_race" })
                    d.Remove(key);
                foreach (var s in d["skills"]!["order"]!.AsArray())
                    s!.AsObject().Remove("name");
                d["social_classes"]!.AsObject().Remove("names");
            });
            Strip("towns.json", d => { foreach (var t in d["towns"]!.AsArray()) t!.AsObject().Remove("name"); });

            var (_, masked, warnings) = Built(shipped);
            Assert.Empty(warnings);
            foreach (var f in Tables)
                Assert.True(JsonNode.DeepEquals(Built().Patched[f], masked[f]), f);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---- the whole flow ---------------------------------------------------------------------------------------------

    private static JsonNode Manifest(string root) =>
        JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, ImportRunner.ManifestName)))!;

    private static string MonstersSource(string root) =>
        (string)JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "GameData", "phantasie3", "monsters.json")))!["_source"]!;

    private static ImportReport Import(string root, params string[] sources)
    {
        var report = new ImportRunner(root, Repo.GameData).Run(ImportRunner.Scan(sources.Select(Repo.Path).ToList()));
        Assert.True(report.Committed);
        Assert.All(report.Versions, v => Assert.True(v.Ok, v.Error));
        return report;
    }

    /// <summary>An Apple-only import is a playable III: it loads through III's loader, a new game starts in Pendragon
    /// and the party leaves town.</summary>
    [RequiresFilesFact(WozA, WozB, Cells)]
    public void AppleOnlyImport_LoadsAndStartsAGame()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3a2-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            var report = Import(root, WozA, WozB);
            Assert.DoesNotContain(report.Notes, n => n.Contains("needs its data", StringComparison.Ordinal));
            Assert.Empty(report.Versions.Single().Warnings);
            var manifest = Manifest(root);
            Assert.Equal("p3-apple2", (string?)manifest["games"]!["phantasie3"]!["tables"]);
            Assert.Equal(2, (int)manifest["games"]!["phantasie3"]!["versions"]!["p3-apple2"]!["recipe"]!);
            Assert.StartsWith(P3Apple2Tables.Label, MonstersSource(root));
            Assert.True(File.Exists(Path.Combine(root, "GameData", "phantasie3", ImportedStrings.FallbackFile("p3-apple2"))));

            GameDataLocator.UseImport(root);
            Assert.True(GameDataLocator.IsImported(GameId.P3));
            var data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));
            GameText.Source = StringTable.ForGame(GameId.P3);
            Assert.Equal(57, data.Spells.Count);
            Assert.NotNull(data.BuildPhantasie3World());
            var s = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
            s.StartNewGame(null);
            Assert.Equal(GameId.P3, s.Game);
            Assert.Equal(GameMode.Town, s.Mode);
            Assert.Equal("PENDRAGON", s.CurrentTown!.Name);
            Assert.Equal((0, 24, 37), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
            s.OpenGuild();
            var hero = s.CreateAndEnroll("GLADE", Race.Human, CharacterClass.Fighter);
            Assert.True(s.TryAddToParty(hero, out var err), err);
            s.LeaveGuild();
            Assert.True(s.LeaveTown());
            Assert.Equal(GameMode.Overworld, s.Mode);
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The v1.0 [cr] sides import too; their played files (CHAR3, TWN31, TWN317) give the played-copy note, and
    /// the tables (read from the TF3 template) are the same as from a fresh copy.</summary>
    [RequiresFilesFact(Cr1A, Cr1B, Cells)]
    public void CrackedPlayedCopy_ImportsWithAPlayedNote()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3a2cr-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            var scan = ImportRunner.Scan(new[] { Cr1A, Cr1B }.Select(Repo.Path).ToList());
            var d = scan.Detections.Single(x => x.Profile.Id == "p3-apple2");
            Assert.Equal(["CHAR3", "TWN31", "TWN317"], d.PlayedFiles.Order(StringComparer.Ordinal));
            Assert.Contains("not read", ImportRunner.PlayedWarning(d), StringComparison.Ordinal);
            Import(root, Cr1A, Cr1B);
            Assert.Equal("p3-apple2", (string?)Manifest(root)["games"]!["phantasie3"]!["tables"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>With DOS III in the same run, DOS III's tables win and the Apple writes none.</summary>
    [RequiresFilesFact(WozA, WozB, DosIma)]
    public void WithDosIII_TheDosTablesWin()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3a2d-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            var report = Import(root, WozA, WozB, DosIma);
            Assert.Equal(["p3-dos-1988", "p3-apple2"], report.Versions.Select(v => v.ProfileId));
            Assert.Contains(report.Versions[1].Warnings, w => w.Contains("comes from another copy", StringComparison.Ordinal));
            Assert.Equal("p3-dos-1988", (string?)Manifest(root)["games"]!["phantasie3"]!["tables"]);
            Assert.StartsWith("Phantasie III, DOS", MonstersSource(root));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Apple alone, then the Amiga: the Amiga's tables replace the Apple's; the Apple again leaves them.</summary>
    [RequiresFilesFact(WozA, WozB, Adf)]
    public void AppleThenAmigaThenApple_TheAmigaTablesStay()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3a2a-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            Import(root, WozA, WozB);
            Assert.StartsWith(P3Apple2Tables.Label, MonstersSource(root));
            Import(root, Adf);
            Assert.StartsWith(P3AmigaTables.Label, MonstersSource(root));
            Import(root, WozA, WozB);
            Assert.StartsWith(P3AmigaTables.Label, MonstersSource(root));
            Assert.Equal("p3-amiga-1.0", (string?)Manifest(root)["games"]!["phantasie3"]!["tables"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
