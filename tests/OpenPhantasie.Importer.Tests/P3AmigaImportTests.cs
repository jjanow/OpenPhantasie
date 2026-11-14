using System.Text.Json.Nodes;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Pipelines.P3Amiga;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// The Amiga-only III import (s210, <c>p3-amiga-1.0</c> recipe 2): <see cref="P3AmigaTables.Write"/> writes the ten
/// tables with DOS III's value in every cell where the Amiga's differ (user s209: DOS for all eight, the shipped
/// <c>phantasie3/amiga-import.json</c>), so the result equals the DOS-built GameData but for provenance, loads, and
/// starts a game. With DOS III imported too, DOS's tables win. <see cref="P3AmigaTablesTests"/> keeps the raw
/// Amiga ≠ DOS list the cells come from.
/// </summary>
[Collection(nameof(LocatorCollection))]
public class P3AmigaImportTests
{
    private const string Adf = "reverse-engineering/amiga/original-images/phantasie3/phantasie3-v1.0-1987-ssi.adf";
    private const string DosIma = "reverse-engineering/dos/original-images/phantasie3/phantasie3-1988-ssi-360k.ima";
    private const string DosGameData = "src/OpenPhantasie.Data/GameData/phantasie3";
    private const string Cells = DosGameData + "/" + P3AmigaTables.CellsFile;

    private static JsonNode Dos(string file) => JsonNode.Parse(File.ReadAllBytes(Repo.Path($"{DosGameData}/{file}")))!;

    private static JsonArray ShippedCells() => JsonNode.Parse(File.ReadAllBytes(Repo.Path(Cells)))!["dos_cells"]!.AsArray();

    private static (Dictionary<string, JsonObject> Raw, Dictionary<string, JsonObject> Patched, List<string> Warnings)? _built;

    private static (Dictionary<string, JsonObject> Raw, Dictionary<string, JsonObject> Patched, List<string> Warnings) Built(
        string? shippedRoot = null)
    {
        if (shippedRoot is null && _built is { } cached)
            return cached;
        var scan = ImportRunner.Scan([Repo.Path(Adf)]);
        var detection = scan.Detections.Single(d => d.Profile.Id == "p3-amiga-1.0");
        var staging = Directory.CreateTempSubdirectory("phantasie-import-test-").FullName;
        try
        {
            var session = new ImportSession
            {
                ShippedGameDataRoot = shippedRoot ?? Repo.GameData, StagingRoot = staging, Detections = scan.Detections,
            };
            var ctx = new ImportContext(session, detection);
            var raw = P3AmigaTables.Build(ctx).ToDictionary(t => t.File, t => t.Node);
            var warnings = new List<string>();
            var patched = P3AmigaTables.BuildWithDosValues(ctx, warnings).ToDictionary(t => t.File, t => t.Node);
            var result = (raw, patched, warnings);
            if (shippedRoot is null)
                _built = result;
            return result;
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
    }

    private static readonly System.Text.RegularExpressions.Regex Provenance = new(
        @"^\$\.(_source|_note|_notes|_layout)(\[\d+\])?$|\._source$|\._note$|\._address$|\.source$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Every shipped cell: its <c>dos</c> value is DOS III's GameData, its <c>amiga</c> value the Amiga build's.</summary>
    [RequiresFilesFact(Adf, Cells, DosGameData + "/monsters.json")]
    public void ShippedCells_HoldDosIIIsValue_AndTheAmigaBuildsValue()
    {
        var raw = Built().Raw;
        var bad = new List<string>();
        foreach (var cell in ShippedCells())
        {
            var file = (string)cell!["file"]!;
            var path = cell["path"]!.AsArray();
            var where = $"{file} {path.ToJsonString()}";
            if (CellPatches.Walk(Dos(file), path) is not { } dos || CellPatches.Walk(raw[file], path) is not { } amiga)
            {
                bad.Add($"{where}: path not found");
                continue;
            }
            if (!JsonNode.DeepEquals(CellPatches.Get(dos.Parent, dos.Key), cell["dos"]))
                bad.Add($"{where}: dos is not DOS III's value");
            if (!JsonNode.DeepEquals(CellPatches.Get(amiga.Parent, amiga.Key), cell["amiga"]))
                bad.Add($"{where}: amiga is not the Amiga build's value");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    /// <summary>User s209 (DOS for all eight): with the cells applied, every table equals DOS III's but for provenance.</summary>
    [RequiresFilesTheory(Adf, Cells, DosGameData + "/monsters.json")]
    [MemberData(nameof(TableData))]
    public void WithDosValues_EqualsTheDosGameData_ButForProvenance(string file)
    {
        var (_, patched, warnings) = Built();
        Assert.Empty(warnings);
        var diffs = P3AmigaTablesTests.Differences(Dos(file), patched[file], "$").Where(d => !Provenance.IsMatch(d)).ToList();
        Assert.True(diffs.Count == 0, $"{file}: differs from DOS III in\n" + string.Join("\n", diffs.Take(100)));
    }

    public static TheoryData<string> TableData() => new(P3AmigaTablesTests.Tables);

    /// <summary>The loader wants 22 HP levels; the Amiga has 21 rising past L15. DOS's 22 hold L15's value from L16 on.</summary>
    [RequiresFilesFact(Adf, Cells)]
    public void HpDice_TwentyTwoLevels_FlatFromL15_AsDos()
    {
        var (raw, patched, _) = Built();
        foreach (var (cls, row) in patched["rule-tables.json"]["hp_dice"]!["by_class"]!.AsObject())
        {
            var levels = row!.AsArray().Select(v => (int)v!).ToArray();
            var amiga = raw["rule-tables.json"]["hp_dice"]!["by_class"]![cls]!.AsArray().Select(v => (int)v!).ToArray();
            Assert.Equal(22, levels.Length);
            Assert.Equal(21, amiga.Length);
            Assert.Equal(amiga[..15], levels[..15]);
            Assert.All(levels[15..], v => Assert.Equal(levels[14], v));
        }
    }

    /// <summary>
    /// The public repo's shipped folder (III's dropped files left out, the overlays' original fields removed) gives the
    /// same tables: the import reads nothing of DOS III's text or numbers but the shipped cells.
    /// </summary>
    [RequiresFilesFact(Adf, Cells)]
    public void PublicShipped_GivesTheSameTables()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3a-public-").FullName;
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
            foreach (var f in P3AmigaTablesTests.Tables)
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

    /// <summary>An Amiga-only import is a playable III: it loads through III's loader and a new game starts in Pendragon.</summary>
    [RequiresFilesFact(Adf, Cells)]
    public void AmigaOnlyImport_LoadsAndStartsAGame()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3a-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            var report = Import(root, Adf);
            Assert.DoesNotContain(report.Notes, n => n.Contains("needs its data", StringComparison.Ordinal));
            var manifest = Manifest(root);
            Assert.Equal("p3-amiga-1.0", (string?)manifest["games"]!["phantasie3"]!["tables"]);
            Assert.Equal("p3-amiga-1.0", (string?)manifest["games"]!["phantasie3"]!["art"]!["amiga"]);
            Assert.Equal(3, (int)manifest["games"]!["phantasie3"]!["versions"]!["p3-amiga-1.0"]!["recipe"]!);
            Assert.StartsWith(P3AmigaTables.Label, MonstersSource(root));
            Assert.True(File.Exists(Path.Combine(root, "GameData", "phantasie3", ImportedStrings.FallbackFile("p3-amiga-1.0"))));

            GameDataLocator.UseImport(root);
            Assert.True(GameDataLocator.IsImported(GameId.P3));
            var data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));
            GameText.Source = StringTable.ForGame(GameId.P3);
            Assert.Equal(57, data.Spells.Count);
            var world = data.BuildPhantasie3World();
            Assert.NotNull(world);
            var s = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
            s.StartNewGame(null);
            Assert.Equal(GameId.P3, s.Game);
            Assert.Equal(GameMode.Town, s.Mode);
            Assert.Equal("Pendragon", s.CurrentTown!.Name);
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

    /// <summary>With DOS III in the same run, DOS III's tables win and the Amiga adds its art and sounds (the path as before s210).</summary>
    [RequiresFilesFact(Adf, DosIma)]
    public void WithDosIII_TheDosTablesWin()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3ad-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            var report = Import(root, Adf, DosIma);
            Assert.Equal(["p3-dos-1988", "p3-amiga-1.0"], report.Versions.Select(v => v.ProfileId));
            Assert.Contains(report.Versions[1].Warnings, w => w.Contains("comes from the DOS copy", StringComparison.Ordinal));
            var manifest = Manifest(root);
            Assert.Equal("p3-dos-1988", (string?)manifest["games"]!["phantasie3"]!["tables"]);
            Assert.Equal("p3-amiga-1.0", (string?)manifest["games"]!["phantasie3"]!["art"]!["amiga"]);
            var outputs = manifest["games"]!["phantasie3"]!["versions"]!["p3-amiga-1.0"]!["outputs"]!.AsObject();
            Assert.DoesNotContain(outputs, o => P3AmigaTablesTests.Tables.Any(t => o.Key == $"GameData/phantasie3/{t}"));
            Assert.DoesNotContain(outputs, o => o.Key.Contains("en.fallback.", StringComparison.Ordinal));
            Assert.StartsWith("Phantasie III, DOS", MonstersSource(root));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Amiga alone, then DOS III, then the Amiga again: DOS III's tables replace the Amiga's and stay.</summary>
    [RequiresFilesFact(Adf, DosIma)]
    public void AmigaThenDosThenAmiga_TheDosTablesStay()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3ada-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            Import(root, Adf);
            Assert.StartsWith(P3AmigaTables.Label, MonstersSource(root));
            Import(root, DosIma);
            Assert.StartsWith("Phantasie III, DOS", MonstersSource(root));
            Assert.Equal("p3-dos-1988", (string?)Manifest(root)["games"]!["phantasie3"]!["tables"]);
            Import(root, Adf);
            Assert.StartsWith("Phantasie III, DOS", MonstersSource(root));
            Assert.Equal("p3-dos-1988", (string?)Manifest(root)["games"]!["phantasie3"]!["tables"]);
            foreach (var t in P3AmigaTablesTests.Tables)
                Assert.True(File.Exists(Path.Combine(root, "GameData", "phantasie3", t)), t);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
