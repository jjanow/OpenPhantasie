using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Pipelines.P3Amiga;
using OpenPhantasie.Importer.Pipelines.P3Apple2;
using OpenPhantasie.Importer.Pipelines.P3C64;
using static OpenPhantasie.Importer.Tests.P3C64TablesTests;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// The C64-only III import (s218, <c>p3-c64</c> / <c>p3-c64-1.2</c>): <see cref="P3C64Tables.Write"/> writes the ten
/// tables with DOS III's value in every number / structure cell where the C64's differ or it has none (the shipped
/// <c>phantasie3/c64-import.json</c>), keeping the C64's text, so the result equals the DOS-built GameData but for
/// provenance and text; the starter party of the disk goes into the Guild's first six slots of a new game. DOS III, the
/// Amiga and the Apple win over it.
/// </summary>
[Collection(nameof(LocatorCollection))]
public class P3C64ImportTests
{
    private const string DosIma = "reverse-engineering/dos/original-images/phantasie3/phantasie3-1988-ssi-360k.ima";
    private const string Adf = "reverse-engineering/amiga/original-images/phantasie3/phantasie3-v1.0-1987-ssi.adf";
    private const string DosGameData = "src/OpenPhantasie.Data/GameData/phantasie3";
    private const string Cells = DosGameData + "/" + P3C64Tables.CellsFile;

    private static JsonNode Dos(string file) => JsonNode.Parse(File.ReadAllBytes(Repo.Path($"{DosGameData}/{file}")))!;

    private static JsonArray ShippedCells() => JsonNode.Parse(File.ReadAllBytes(Repo.Path(Cells)))!["dos_cells"]!.AsArray();

    private static (Dictionary<string, JsonObject> Raw, Dictionary<string, JsonObject> Patched, List<string> Warnings)? _built;

    private static (Dictionary<string, JsonObject> Raw, Dictionary<string, JsonObject> Patched, List<string> Warnings) Built(
        string? shippedRoot = null)
    {
        if (shippedRoot is null && _built is { } cached)
            return cached;
        var result = V10(ctx =>
        {
            var raw = P3C64Tables.Build(ctx).ToDictionary(t => t.File, t => t.Node);
            var warnings = new List<string>();
            var patched = P3C64Tables.BuildWithDosValues(ctx, warnings).ToDictionary(t => t.File, t => t.Node);
            return (raw, patched, warnings);
        }, shippedRoot);
        if (shippedRoot is null)
            _built = result;
        return result;
    }

    private static readonly Regex Provenance = new(
        @"^\$\.(_source|_note|_notes|_layout)(\[\d+\])?$|\._source$|\._note$|\._address$|\.source$",
        RegexOptions.CultureInvariant);

    /// <summary>The C64's own text the import keeps (the rule of <c>build_c64_p3_import.py</c>, as the Apple's): names,
    /// dungeon messages and the record size their text area sets, scroll pages.</summary>
    private static readonly Regex Text = new(
        @"\.name$|\.names\[\d+\]$|^\$\.levels\.DNG\d+\.(messages\.|size$)|^\$\.scrolls\[\d+\]\.(text|lines)",
        RegexOptions.CultureInvariant);

    /// <summary>Every shipped cell: its <c>dos</c> value is DOS III's GameData, its <c>c64</c> value the C64 build's.</summary>
    [RequiresFilesFact(V10S1, V10S2, Cells, DosGameData + "/monsters.json")]
    public void ShippedCells_HoldDosIIIsValue_AndTheC64BuildsValue()
    {
        var raw = Built().Raw;
        var bad = new List<string>();
        foreach (var cell in ShippedCells())
        {
            var file = (string)cell!["file"]!;
            var path = cell["path"]!.AsArray();
            var where = $"{file} {path.ToJsonString()}";
            if (CellPatches.Walk(Dos(file), path) is not { } dos || CellPatches.Walk(raw[file], path) is not { } c64)
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
            if (!JsonNode.DeepEquals(Value(CellPatches.Get(c64.Parent, c64.Key)), cell["c64"]))
                bad.Add($"{where}: c64 is not the C64 build's value");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(50)));
    }

    /// <summary>With the cells applied, every table equals DOS III's but for provenance and the C64's own text; the
    /// dungeon records equal DOS's byte for byte up to the text pointers (0x593).</summary>
    [RequiresFilesTheory(V10S1, V10S2, Cells, DosGameData + "/monsters.json")]
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
                var c64 = Convert.FromHexString((string)patched[file]["levels"]![id]!["raw_hex"]!);
                Assert.True(dos.AsSpan(0, 0x593).SequenceEqual(c64.AsSpan(0, 0x593)), id);
            }
    }

    public static TheoryData<string> TableData() => new(Tables);

    /// <summary>v1.2 (alt) takes the same cells without a warning (its tables are v1.0's).</summary>
    [RequiresFilesFact(V12S1, V12S2, Cells)]
    public void V12_TakesTheCellsWithoutAWarning()
    {
        var warnings = new List<string>();
        V12(ctx => P3C64Tables.BuildWithDosValues(ctx, warnings));
        Assert.Empty(warnings);
    }

    /// <summary>The text differences kept are text: names, messages, scroll pages (no number hides among them).</summary>
    [RequiresFilesFact(V10S1, V10S2, Cells, DosGameData + "/monsters.json")]
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
    [RequiresFilesFact(V10S1, V10S2, Cells)]
    public void PublicShipped_GivesTheSameTables()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3c64-public-").FullName;
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

    private static string StarterFile(string root) => Path.Combine(root, "GameData", "phantasie3", P3C64StarterGuild.File);

    private static ImportReport Import(string root, params string[] sources)
    {
        var report = new ImportRunner(root, Repo.GameData).Run(ImportRunner.Scan(sources.Select(Repo.Path).ToList()));
        Assert.True(report.Committed);
        Assert.All(report.Versions, v => Assert.True(v.Ok, v.Error));
        return report;
    }

    /// <summary>A new game of an import at <paramref name="root"/> (the locator pointed at it).</summary>
    private static GameSession NewGame(string root)
    {
        GameDataLocator.UseImport(root);
        Assert.True(GameDataLocator.IsImported(GameId.P3));
        var data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));
        GameText.Source = StringTable.ForGame(GameId.P3);
        Assert.Equal(57, data.Spells.Count);
        Assert.NotNull(data.BuildPhantasie3World());
        var s = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
        s.StartNewGame(null);
        return s;
    }

    /// <summary>A C64-only import is a playable III: it loads, a new game starts in Pendragon with the disk's six in the
    /// Guild's first six slots, they join the party at once (they are in Pendragon) and the party leaves town.</summary>
    [RequiresFilesFact(V10S1, V10S2, Cells)]
    public void C64OnlyImport_StartsAGame_WithTheStarterSixInTheGuild()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3c64-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            var report = Import(root, V10S1, V10S2);
            Assert.DoesNotContain(report.Notes, n => n.Contains("needs its data", StringComparison.Ordinal));
            Assert.Empty(report.Versions.Single().Warnings);
            var manifest = Manifest(root);
            Assert.Equal("p3-c64", (string?)manifest["games"]!["phantasie3"]!["tables"]);
            Assert.StartsWith(P3C64Tables.Label, MonstersSource(root));
            Assert.True(File.Exists(StarterFile(root)));

            var s = NewGame(root);
            Assert.Equal(GameMode.Town, s.Mode);
            Assert.Equal("PENDRAGON", s.CurrentTown!.Name);
            Assert.Equal((0, 24, 37), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
            string[] six = ["CHUMLEY", "TRACKER", "FINGERS", "GRASSHOPPER", "BONES", "SAURIAN"];
            Assert.Equal(six, s.Roster.Slots.Take(6).Select(c => c?.Name));
            Assert.All(s.Roster.Slots.Skip(6), c => Assert.Null(c));
            var chumley = s.Roster.Slots[0]!;
            Assert.Equal((Race.Troll, CharacterClass.Fighter, 1, 14), (chumley.Race, chumley.Class, chumley.Level, chumley.HpMax));
            Assert.Equal((4, 22, 64, 41), (chumley.WornShield, chumley.WornArmor, chumley.WornWeapon, chumley.WornBow));
            Assert.Equal(1, s.Roster.Slots[1]!.SpellsKnown[0]); // TRACKER knows Heal 1

            s.OpenGuild();
            foreach (var c in s.Roster.Slots.Take(6))
            {
                Assert.False(s.AddNeedsMoveConfirm(c!), c!.Name); // already in Pendragon: no two-month journey
                Assert.True(s.TryAddToParty(c, out var err), err);
            }
            s.LeaveGuild();
            Assert.True(s.LeaveTown());
            Assert.Equal(GameMode.Overworld, s.Mode);
            Assert.Equal(6, s.Party.Members.Count);
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// D3-078 (user s218): v1.0's stored age clocks (P16 + 256 P17, FORMAT.md 3.5: 6635 .. 8165, 255 .. 314 years on the
    /// C64's own sheet) are not kept: the file carries them as record_age_clock and no age_clock, so a new game gives the
    /// six Guild New's start age. v1.2's fit the C64's creation formula and are kept.
    /// </summary>
    [RequiresFilesFact(V10S1, V10S2, V12S1, V12S2)]
    public void V10Ages_GuildNewStartAge_V12Ages_Kept()
    {
        var v10 = V10(ctx => P3C64StarterGuild.Read(ctx.Files))!["members"]!.AsArray();
        Assert.Equal([6635, 6635, 6635, 8165, 7655, 7400], v10.Select(m => (int)m!["record_age_clock"]!));
        Assert.All(v10, m => Assert.Null(m!["age_clock"]));
        var v12 = V12(ctx => P3C64StarterGuild.Read(ctx.Files))!["members"]!.AsArray();
        Assert.All(v12, m => Assert.Equal((int)m!["record_age_clock"]!, (int)m["age_clock"]!));
        Assert.Equal(360, (int)v12[0]!["age_clock"]!); // CHUMLEY: (1 + 24) * 4 + 260
    }

    /// <summary>D3-078: under the manual's lifespans (the default) v1.0's six are young and alive after the first town
    /// entry with the party (before the decision four of them died there).</summary>
    [RequiresFilesFact(V10S1, V10S2, Cells)]
    public void V10Starters_SurviveTheFirstTownEntry()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3c64age-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            Import(root, V10S1, V10S2);
            var s = NewGame(root);
            Assert.All(s.Roster.Slots.Take(6), c =>
            {
                Assert.True(c!.Lifespan > 0, c.Name);
                Assert.True(c.AgeClock < c.Lifespan * 7 / 10, $"{c.Name} age {c.AgeClock} span {c.Lifespan}");
            });
            s.OpenGuild();
            foreach (var c in s.Roster.Slots.Take(6))
                Assert.True(s.TryAddToParty(c!, out var err), err);
            s.LeaveGuild();
            Assert.True(s.LeaveTown());
            s.EnterTown(1);
            Assert.All(s.Party.Members, m => Assert.Equal(CharacterStatus.Ok, m.Status));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A fresh v1.0 import, then the played pair into the same folder: the played run leaves no starter file
    /// behind, so a new game no longer gets the fresh copy's six.</summary>
    [RequiresFilesFact(V10S1, V10S2, HeldS1, HeldS2, Cells)]
    public void FreshThenPlayed_LeavesNoStarterFile()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3c64fp-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            Import(root, V10S1, V10S2);
            Assert.True(File.Exists(StarterFile(root)));
            Import(root, HeldS1, HeldS2);
            Assert.False(File.Exists(StarterFile(root)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>v1.2 (alt): its own six (re-rolled), the same tables.</summary>
    [RequiresFilesFact(V12S1, V12S2, Cells)]
    public void V12Import_GivesV12sRolls()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3c64v12-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            Import(root, V12S1, V12S2);
            Assert.Equal("p3-c64-1.2", (string?)Manifest(root)["games"]!["phantasie3"]!["tables"]);
            var s = NewGame(root);
            var chumley = s.Roster.Slots[0]!;
            Assert.Equal("CHUMLEY", chumley.Name);
            Assert.Equal((22, 4, 15, 17, 12, 11), (chumley.Stats.Str, chumley.Stats.Int, chumley.Stats.Dex, chumley.Stats.Con,
                chumley.Stats.Cha, chumley.Stats.Luck));
            Assert.Equal(12, chumley.HpMax);
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The held cracked pair (v1.0's programs, played: roster and town stock) imports, with a played note and
    /// without the starter party (its roster is not the disk's six as shipped).</summary>
    [RequiresFilesFact(HeldS1, HeldS2, Cells)]
    public void PlayedCopy_ImportsWithoutTheStarterParty()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3c64held-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            var report = Import(root, HeldS1, HeldS2);
            var v = report.Versions.Single();
            Assert.Contains(v.Warnings, w => w.Contains("pre-generated party is not imported", StringComparison.Ordinal));
            Assert.Contains(v.Warnings, w => w.Contains("has been played", StringComparison.Ordinal));
            Assert.False(File.Exists(StarterFile(root)));
            var s = NewGame(root);
            Assert.All(s.Roster.Slots, c => Assert.Null(c));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>With DOS III in the same run, DOS III's tables win: the C64 writes none and no starter party.</summary>
    [RequiresFilesFact(V10S1, V10S2, DosIma)]
    public void WithDosIII_TheDosTablesWin()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3c64d-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            var report = Import(root, V10S1, V10S2, DosIma);
            Assert.Equal(["p3-dos-1988", "p3-c64"], report.Versions.Select(v => v.ProfileId));
            Assert.Contains(report.Versions[1].Warnings, w => w.Contains("comes from another copy", StringComparison.Ordinal));
            Assert.Equal("p3-dos-1988", (string?)Manifest(root)["games"]!["phantasie3"]!["tables"]);
            Assert.StartsWith("Phantasie III, DOS", MonstersSource(root));
            Assert.False(File.Exists(StarterFile(root)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>C64 alone, then the Amiga: the Amiga's tables replace the C64's and the C64's starter file is no longer
    /// read (a new game has an empty Guild); the C64 again leaves the Amiga's tables.</summary>
    [RequiresFilesFact(V10S1, V10S2, Adf)]
    public void C64ThenAmiga_TheAmigaTablesStay_AndNoStarters()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3c64a-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            Import(root, V10S1, V10S2);
            Assert.StartsWith(P3C64Tables.Label, MonstersSource(root));
            Import(root, Adf);
            Assert.StartsWith(P3AmigaTables.Label, MonstersSource(root));
            Import(root, V10S1, V10S2);
            Assert.StartsWith(P3AmigaTables.Label, MonstersSource(root));
            Assert.Equal("p3-amiga-1.0", (string?)Manifest(root)["games"]!["phantasie3"]!["tables"]);
            var s = NewGame(root);
            Assert.All(s.Roster.Slots, c => Assert.Null(c));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>C64, then the Apple II: the Apple's tables take over the C64's (the Apple outranks it).</summary>
    [RequiresFilesFact(V10S1, V10S2, P3Apple2TablesTests.WozA, P3Apple2TablesTests.WozB)]
    public void C64ThenApple_TheAppleTablesWin()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3c64a2-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            Import(root, V10S1, V10S2);
            Import(root, P3Apple2TablesTests.WozA, P3Apple2TablesTests.WozB);
            Assert.StartsWith(P3Apple2Tables.Label, MonstersSource(root));
            Assert.Equal("p3-apple2", (string?)Manifest(root)["games"]!["phantasie3"]!["tables"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
