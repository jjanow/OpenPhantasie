using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// I's tables from the Amiga disk alone (<c>P1AmigaTables</c>, s201; <c>reverse-engineering/amiga/GAME-TABLES.md</c>):
/// every class (b) / (c) file DOS I's pipeline writes, in DOS I's schema. There is no Python counterpart, so the check
/// is against the DOS-built GameData: every number equal (the Amiga's differing cells take DOS's from the shipped
/// <c>amiga-import.json</c>), and the text differences exactly the listed ones (the Amiga's text is used). The import
/// loads and starts a game, reads nothing the public repo leaves out, and DOS I's tables win over it in any order.
/// </summary>
[Collection(nameof(LocatorCollection))]
public class P1AmigaTablesTests
{
    private const string Adf = "reverse-engineering/amiga/original-images/phantasie1/phantasie-v1.0-1987-ssi.adf";
    private const string P1Dos = "reverse-engineering/dos/original-images/phantasie1/phantasie-1987-ssi-360k.ima";
    private const string DosGameData = "src/OpenPhantasie.Data/GameData/phantasie1";

    /// <summary>The files <c>P1DosPipeline</c> writes under <c>GameData/phantasie1/</c> (its strings aside).</summary>
    public static readonly string[] Tables =
    [
        "monsters.json", "items.json", "names.json", "towns.json", "elementals.json", "scrolls.json", "messages.json",
        "overworld.json", "dungeons.json", "item-descriptions.json", "spells.json",
    ];

    private static PipelineHarness.Result Run() => PipelineHarness.Run("p1-amiga-1.0", Adf);

    private static JsonNode Amiga(string file) => JsonNode.Parse(Run().Outputs[$"GameData/phantasie1/{file}"])!;

    private static JsonNode Dos(string file) => JsonNode.Parse(File.ReadAllBytes(Repo.Path($"{DosGameData}/{file}")))!;

    [RequiresFilesFact(Adf)]
    public void AmigaOnly_WritesEveryTableDosIWrites()
    {
        var r = Run();
        foreach (var f in Tables)
            Assert.True(r.Outputs.ContainsKey($"GameData/phantasie1/{f}"), f);
        // Every shipped DOS cell matched the Amiga's value (no "neither" warning), and nothing else was warned.
        Assert.Empty(r.Warnings);
        Assert.Contains("GameData/phantasie1/strings/en.own.p1-amiga-1.0.json", r.AllOutputs.Keys);
        Assert.Contains("GameData/phantasie1/strings/en.fallback.p1-amiga-1.0.json", r.AllOutputs.Keys);
    }

    // ---- numbers ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Paths that may differ from the DOS build: text (the Amiga's), provenance (sources, offsets, cipher notes) and
    /// the DOS-only junk bytes 1000..1023 of each OUT*.DAT (embedded_text). Anything else differing fails.
    /// </summary>
    private static readonly Regex AllowedDifference = new(
        @"^\$\.(_source|_note|_cipher|_format|_layout\.\w+)$" +
        @"|\.(name|offset|name_ptr_ds|name_ptr_h12|text|raw_line|lines(\[\d+\])?|embedded_text|step|initial_key|phase)$" +
        @"|^\$\.areas\[\d+\]\.source$",
        RegexOptions.CultureInvariant);

    private static List<string> Differences(JsonNode? dos, JsonNode? amiga, string path)
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
                        output.Add(p);
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

    [RequiresFilesFact(Adf, DosGameData + "/monsters.json")]
    public void EveryNumber_EqualsTheDosBuild()
    {
        var failures = new List<string>();
        foreach (var f in Tables)
            failures.AddRange(Differences(Dos(f), Amiga(f), "$").Where(p => !AllowedDifference.IsMatch(p)).Select(p => $"{f} {p}"));
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(30)));
        // The fields the loaders bind straight from DOS bytes are equal too (a town's DOS export, the levels).
        Assert.True(JsonNode.DeepEquals(Dos("towns.json")["towns"], Amiga("towns.json")["towns"]));
        Assert.True(JsonNode.DeepEquals(Dos("monsters.json")["monsters"], Amiga("monsters.json")["monsters"]));
        foreach (var (level, node) in Dos("dungeons.json")["levels"]!.AsObject())
            Assert.Equal((string)node!["raw_hex"]!, (string)Amiga("dungeons.json")["levels"]![level]!["raw_hex"]!);
        foreach (var (region, node) in Dos("overworld.json")["regions"]!.AsObject())
            foreach (var key in new[] { "tiles", "features", "text_pages" })
                Assert.True(JsonNode.DeepEquals(node![key], Amiga("overworld.json")["regions"]![region]![key]), $"{region}.{key}");
    }

    /// <summary>The shipped DOS cells hold the DOS GameData's values at their paths (and the Amiga's differ).</summary>
    [RequiresFilesFact(DosGameData + "/monsters.json")]
    public void ShippedDosCells_EqualTheDosGameData()
    {
        var cells = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Repo.GameData, "phantasie1", "amiga-import.json")))!["dos_cells"]!.AsArray();
        Assert.Equal(8, cells.Count);
        foreach (var cell in cells)
        {
            var leaf = Pipelines.CellPatches.Walk(Dos((string)cell!["file"]!), cell["path"]!.AsArray());
            Assert.NotNull(leaf);
            var value = Pipelines.CellPatches.Get(leaf.Value.Parent, leaf.Value.Key);
            if (cell["byte"] is { } at)
                Assert.Equal((int)cell["dos"]!, Convert.FromHexString((string)value!)[(int)at]);
            else if (cell["fog"] is not null)
                Assert.Equal((int)cell["dos"]!, (int)value! % 120);
            else
                Assert.True(JsonNode.DeepEquals(cell["dos"], value));
            Assert.False(JsonNode.DeepEquals(cell["amiga"], cell["dos"]));
        }
    }

    // ---- text -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The Amiga's text where it differs from DOS I's (GAME-TABLES.md "Amiga vs DOS: text"): item names 101-109 and 139,
    /// the first elemental's name, two MESS lines (MESS1, MESS5), every scroll line upper-cased (so the scroll quotes of
    /// item descriptions 121-140 too); everything else the same. User s202 (D-082 / D-083): the two typos are fixed from
    /// the shipped <c>name_fixes</c>, so 139 and the elemental read as DOS's and only HEAL POT1..9 differ.
    /// </summary>
    [RequiresFilesFact(Adf, DosGameData + "/monsters.json")]
    public void TextDifferences_AreTheListedOnes()
    {
        static List<int> DifferingNames(JsonNode dos, JsonNode amiga, string list) =>
            Enumerable.Range(0, dos[list]!.AsArray().Count)
                .Where(i => (string)dos[list]![i]!["name"]! != (string)amiga[list]![i]!["name"]!).ToList();

        Assert.Equal([.. Enumerable.Range(101, 9)], DifferingNames(Dos("items.json"), Amiga("items.json"), "items"));
        Assert.Equal("HEAL POT1", (string)Amiga("items.json")["items"]![101]!["name"]!);
        Assert.Equal("HEALING10", (string)Amiga("items.json")["items"]![110]!["name"]!);
        Assert.Equal("SCROLL 19", (string)Amiga("items.json")["items"]![139]!["name"]!);
        Assert.Equal(Enumerable.Range(0, 9).ToList(), DifferingNames(Dos("names.json"), Amiga("names.json"), "spell_level_names"));
        foreach (var list in new[] { "classes", "races", "spells", "items", "monsters" })
            Assert.Empty(DifferingNames(Dos("names.json"), Amiga("names.json"), list));
        Assert.Empty(DifferingNames(Dos("names.json"), Amiga("names.json"), "town_labels"));
        Assert.Empty(DifferingNames(Dos("elementals.json"), Amiga("elementals.json"), "elementals"));
        Assert.Equal("FIRE ELEM", (string)Amiga("elementals.json")["elementals"]![0]!["name"]!);
        Assert.Empty(DifferingNames(Dos("monsters.json"), Amiga("monsters.json"), "monsters"));
        Assert.Empty(DifferingNames(Dos("towns.json"), Amiga("towns.json"), "towns"));

        // Messages: one record of MESS1 and one of MESS5 (the same in the dungeon messages built from them).
        var differing = new List<string>();
        var da = Dos("messages.json")["areas"]!.AsArray();
        var aa = Amiga("messages.json")["areas"]!.AsArray();
        for (var n = 0; n < 10; n++)
            for (var i = 0; i < da[n]!["records"]!.AsArray().Count; i++)
                if ((string)da[n]!["records"]![i]!["text"]! != (string)aa[n]!["records"]![i]!["text"]!)
                    differing.Add($"MESS{n + 1} record {i}");
        Assert.Equal(2, differing.Count);
        Assert.StartsWith("MESS1 ", differing[0]);
        Assert.StartsWith("MESS5 ", differing[1]);

        // Scrolls: the Amiga's lines are DOS's upper-cased, line for line.
        var ds = Dos("scrolls.json")["scrolls"]!.AsArray();
        var ams = Amiga("scrolls.json")["scrolls"]!.AsArray();
        for (var s = 0; s < 20; s++)
            Assert.Equal(ds[s]!["lines"]!.AsArray().Select(l => ((string)l!).ToUpperInvariant()),
                ams[s]!["lines"]!.AsArray().Select(l => (string)l!));
        var di = Dos("item-descriptions.json")["items"]!.AsObject();
        var ai = Amiga("item-descriptions.json")["items"]!.AsObject();
        foreach (var (id, node) in di)
        {
            var dosText = (string)node!["text"]!;
            var amigaText = (string)ai[id]!["text"]!;
            if (int.Parse(id) is >= 121 and <= 140)
                Assert.Equal(dosText.Split('"')[1].ToUpperInvariant(), amigaText.Split('"')[1]);
            else
                Assert.Equal(dosText, amigaText);
        }
    }

    // ---- the public repo --------------------------------------------------------------------------------------------

    /// <summary>
    /// A shipped folder as the public repo's (10-importer.md 8.1): I's (b) / dropped (c) files left out, the overlays'
    /// original fields removed. The Amiga import from it equals the one from the full private GameData, so it reads
    /// nothing of DOS I's text or numbers.
    /// </summary>
    [RequiresFilesFact(Adf)]
    public void PublicShipped_GivesTheSameTables()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p1a-public-").FullName;
        try
        {
            var shipped = Path.Combine(dir, "GameData");
            var dropped = new HashSet<string>(StringComparer.Ordinal)
            {
                "dungeons.json", "elementals.json", "item-descriptions.json", "messages.json", "monsters.json", "names.json",
                "overworld.json", "scrolls.json", "towns.json", "graphics/fonts/amiga-topaz-8.json",
                "graphics/fonts/amiga-z6font-6.json",
            };
            foreach (var file in Directory.EnumerateFiles(Repo.GameData, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(Repo.GameData, file).Replace('\\', '/');
                if (rel.StartsWith("phantasie1/", StringComparison.Ordinal) && dropped.Contains(rel["phantasie1/".Length..]))
                    continue;
                var dest = Path.Combine(shipped, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest);
            }
            void Strip(string file, Action<JsonObject> strip)
            {
                var path = Path.Combine(shipped, "phantasie1", file);
                var doc = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
                strip(doc);
                File.WriteAllText(path, doc.ToJsonString());
            }
            Strip("items.json", d => { foreach (var i in d["items"]!.AsArray()) i!.AsObject().Remove("name"); });
            Strip("spells.json", d => { foreach (var s in d["spells"]!.AsArray()) s!.AsObject().Remove("name"); });
            Strip("asset-manifest.json", d => d.Remove("assets"));

            var masked = PipelineHarness.RunWithShipped(shipped, "p1-amiga-1.0", Adf);
            Assert.Empty(masked.Warnings);
            foreach (var f in Tables)
                Assert.True(masked.Outputs[$"GameData/phantasie1/{f}"].AsSpan().SequenceEqual(Run().Outputs[$"GameData/phantasie1/{f}"]), f);
            Assert.True(masked.Outputs["GameData/phantasie1/asset-manifest.json"].AsSpan()
                .SequenceEqual(Run().Outputs["GameData/phantasie1/asset-manifest.json"]));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---- the whole flow ---------------------------------------------------------------------------------------------

    private static JsonNode Manifest(string root) =>
        JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, ImportRunner.ManifestName)))!;

    private static void Import(string root, params string[] sources)
    {
        var report = new ImportRunner(root, Repo.GameData).Run(ImportRunner.Scan(sources.Select(Repo.Path).ToList()));
        Assert.True(report.Committed);
        Assert.All(report.Versions, v => Assert.True(v.Ok, v.Error));
    }

    /// <summary>An Amiga-only import is a playable I: it loads through the game's loader and a new game starts.</summary>
    [RequiresFilesFact(Adf)]
    public void AmigaOnlyImport_LoadsAndStartsAGame()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p1a-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            Import(root, Adf);
            Assert.Equal("p1-amiga-1.0", (string?)Manifest(root)["games"]!["phantasie1"]!["tables"]);
            Assert.Equal("p1-amiga-1.0", (string?)Manifest(root)["games"]!["phantasie1"]!["art"]!["amiga"]);

            GameDataLocator.UseImport(root);
            Assert.True(GameDataLocator.IsImported(GameId.P1));
            var data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P1));
            GameText.Source = StringTable.ForGame(GameId.P1);
            Assert.Equal(80, data.Monsters.Count);
            Assert.Equal(12, data.Towns.Count);
            Assert.Equal(18, data.OverworldRegions.Count);
            Assert.Equal(10, data.Dungeons.Count);
            var s = GameSessionFactory.Create(data, new SeededDosRandomSource(1));
            s.StartNewGame(null);
            s.OpenGuild();
            var rolled = s.RollNewMember(Race.Human, CharacterClass.Fighter);
            Assert.True(s.TryEnrollNewMember(rolled, "GLADE", out var err), err);
            Assert.True(s.TryAddToParty(rolled, out err), err);
            s.LeaveGuild();
            Assert.Equal(GameMode.Town, s.Mode);
            Assert.True(s.LeaveTown());
            Assert.Equal(GameMode.Overworld, s.Mode);

            // Zeus's speech in the Amiga's own words (en.own.p1-amiga-1.0.json, on top while I's tables are the Amiga's).
            Assert.Equal("en.own.p1-amiga-1.0.json", Path.GetFileName(GameDataLocator.ImportedStringFiles("phantasie1")[^1]));
            Assert.StartsWith("WELL DONE,", StringTable.ForGame(GameId.P1).Get("core.endgame.zeus_1"));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// The public repo's tables carry "..." for each original part (10-importer.md 7.6). An Amiga-only import, laid over
    /// them as the Client does, leaves no line of I's table (nor of the shared table's I areas) on "...".
    /// </summary>
    [RequiresFilesFact(Adf)]
    public void Strings_PublicMask_AmigaOnlyImportLeavesNoEllipsis()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p1as-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            Import(root, Adf);
            GameDataLocator.UseImport(root);
            var left = new List<string>();
            var checkedIds = 0;
            foreach (var table in new[] { "shared", "phantasie1" })
            {
                var committed = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Repo.GameData, table, "strings", "en.json")))!["strings"]!.AsObject();
                var sources = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Repo.GameData, table, "strings", "en.sources.json")))!["strings"]!.AsObject();
                var shown = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (id, alts) in sources)
                    shown[id] = string.Concat(alts!.AsArray()[0]!["parts"]!.AsArray().Select(p => p is JsonArray ? "..." : (string)p!));
                foreach (var file in GameDataLocator.ImportedStringFiles(table))
                    foreach (var (id, text) in JsonNode.Parse(File.ReadAllBytes(file))!["strings"]!.AsObject())
                        shown[id] = (string)text!;
                foreach (var (id, alts) in sources)
                {
                    var fromI = alts!.AsArray().Any(a => (string)a!["from"]! is "p1-dos-1987" or "p1-amiga-1.0");
                    var otherGame = table == "shared" && Regex.IsMatch(id, @"^(p[234]\D|core\.p4\.)");
                    if (!fromI || otherGame)
                        continue;
                    checkedIds++;
                    if (shown[id].Contains("...", StringComparison.Ordinal) && !((string)committed[id]!).Contains("...", StringComparison.Ordinal))
                        left.Add($"{table} {id}: '{shown[id]}'");
                }
            }
            Assert.True(left.Count == 0, string.Join("\n", left));
            Assert.True(checkedIds > 400, $"{checkedIds} ids checked");
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>With DOS I in the same run, DOS I's tables win and the Amiga adds its art, fonts and sounds.</summary>
    [RequiresFilesFact(Adf, P1Dos)]
    public void WithDosI_TheDosTablesWin()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p1ad-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            var report = new ImportRunner(root, Repo.GameData).Run(ImportRunner.Scan([Repo.Path(Adf), Repo.Path(P1Dos)]));
            Assert.Equal(["p1-dos-1987", "p1-amiga-1.0"], report.Versions.Select(v => v.ProfileId));
            Assert.Contains(report.Versions[1].Warnings, w => w.Contains("comes from the DOS copy", StringComparison.Ordinal));
            var manifest = Manifest(root);
            Assert.Equal("p1-dos-1987", (string?)manifest["games"]!["phantasie1"]!["tables"]);
            var outputs = manifest["games"]!["phantasie1"]!["versions"]!["p1-amiga-1.0"]!["outputs"]!.AsObject();
            Assert.DoesNotContain(outputs, o => Tables.Any(t => o.Key == $"GameData/phantasie1/{t}"));
            Assert.DoesNotContain(outputs, o => o.Key.Contains("en.own.", StringComparison.Ordinal));
            Assert.StartsWith("PHANTASI.COM", (string)JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "GameData", "phantasie1", "monsters.json")))!["_source"]!);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// Amiga alone, then DOS I, then the Amiga again (a recipe re-run): DOS I's tables replace the Amiga's and stay;
    /// the Amiga's re-run neither overwrites nor removes them.
    /// </summary>
    [RequiresFilesFact(Adf, P1Dos)]
    public void AmigaThenDosThenAmiga_TheDosTablesStay()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p1ada-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            string MonstersSource() =>
                (string)JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "GameData", "phantasie1", "monsters.json")))!["_source"]!;
            Import(root, Adf);
            Assert.StartsWith("Phantasie I Amiga", MonstersSource());
            Import(root, P1Dos);
            Assert.StartsWith("PHANTASI.COM", MonstersSource());
            Assert.Equal("p1-dos-1987", (string?)Manifest(root)["games"]!["phantasie1"]!["tables"]);
            Import(root, Adf);
            Assert.StartsWith("PHANTASI.COM", MonstersSource());
            Assert.Equal("p1-dos-1987", (string?)Manifest(root)["games"]!["phantasie1"]!["tables"]);
            foreach (var t in Tables)
                Assert.True(File.Exists(Path.Combine(root, "GameData", "phantasie1", t)), t);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
