using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Pipelines.P1Apple2;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// I's tables from the Apple II disks alone (<c>P1Apple2Tables</c>, s222; <c>reverse-engineering/apple2/phantasie1/FORMAT.md</c>):
/// every class (b) / (c) file DOS I's pipeline writes, in DOS I's schema. As for the C64, the check is against the
/// DOS-built GameData: every number equal (the Apple's differing cells take DOS's from the shipped
/// <c>apple2-import.json</c>), the text the Apple's. The import loads and starts a game, reads nothing the public repo
/// leaves out, and DOS I's, the Amiga's and the C64's tables win over it in any order.
/// </summary>
[Collection(nameof(LocatorCollection))]
public class P1Apple2TablesTests
{
    private const string Images = "reverse-engineering/apple2/original-images/";
    private const string DskA = Images + "phantasie1/4am-crack/phantasie1-4am-side-a-boot.dsk";
    private const string DskB = Images + "phantasie1/4am-crack/phantasie1-4am-side-b-dungeon.dsk";
    private const string WozA = Images + "phantasie1/woz-original/phantasie1-boot.woz";
    private const string WozB = Images + "phantasie1/woz-original/phantasie1-dungeon.woz";
    private const string Adf = "reverse-engineering/amiga/original-images/phantasie1/phantasie-v1.0-1987-ssi.adf";
    private const string P1Dos = "reverse-engineering/dos/original-images/phantasie1/phantasie-1987-ssi-360k.ima";
    private const string C64Side1 = "reverse-engineering/c64/original-images/phantasie1/c64pp/phantasie_s1[ssi_1985](v1_4).g64";
    private const string C64Side2 = "reverse-engineering/c64/original-images/phantasie1/c64pp/phantasie_s2[ssi_1985](v1_4).g64";
    private const string DosGameData = "src/OpenPhantasie.Data/GameData/phantasie1";
    private const string Cells = DosGameData + "/" + P1Apple2Tables.CellsFile;

    private static readonly string[] Tables = P1AmigaTablesTests.Tables;

    private static PipelineHarness.Result Run() => PipelineHarness.Run("p1-apple2", DskA, DskB);

    private static JsonNode Apple(string file) => JsonNode.Parse(Run().Outputs[$"GameData/phantasie1/{file}"])!;

    private static JsonNode Dos(string file) => JsonNode.Parse(File.ReadAllBytes(Repo.Path($"{DosGameData}/{file}")))!;

    private static T WithContext<T>(Func<ImportContext, T> body, params string[] sources)
    {
        var scan = ImportRunner.Scan(sources.Select(Repo.Path).ToList());
        var detection = scan.Detections.Single(d => d.Profile.Id == "p1-apple2");
        var staging = Directory.CreateTempSubdirectory("phantasie-import-test-").FullName;
        try
        {
            var session = new ImportSession { ShippedGameDataRoot = Repo.GameData, StagingRoot = staging, Detections = scan.Detections };
            return body(new ImportContext(session, detection));
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
    }

    private static Dictionary<string, JsonObject>? _raw;

    /// <summary>The Apple's build without DOS's cells (what <c>diff_a2_p1_vs_dos.py --built</c> compares).</summary>
    private static Dictionary<string, JsonObject> Raw() => _raw ??= WithContext(ctx =>
        P1Apple2Tables.Build(ctx, [], withDosValues: false).ToDictionary(kv => kv.Key, kv => kv.Value.Node), DskA, DskB);

    /// <summary>
    /// Writes the raw build for <c>tools/scripts/diff_a2_p1_vs_dos.py --built DIR</c> when
    /// <c>OPENPHANTASIE_DUMP_P1_APPLE2=DIR</c> is set (the shipped cells are made from it); otherwise only builds it.
    /// </summary>
    [RequiresFilesFact(DskA, DskB)]
    public void RawBuild_DumpsForTheCellScript()
    {
        var raw = Raw();
        Assert.Equal(Tables.Order(StringComparer.Ordinal), raw.Keys.Order(StringComparer.Ordinal));
        if (Environment.GetEnvironmentVariable("OPENPHANTASIE_DUMP_P1_APPLE2") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            foreach (var (file, node) in raw)
                File.WriteAllText(Path.Combine(dir, file), node.ToJsonString());
        }
    }

    [RequiresFilesFact(DskA, DskB)]
    public void Detects_TheDskPair_AsPlayedTowns()
    {
        var scan = ImportRunner.Scan([Repo.Path(DskA), Repo.Path(DskB)]);
        var d = Assert.Single(scan.Detections, x => x.Profile.Id == "p1-apple2");
        Assert.Equal(DetectionStatus.Ready, d.Status);
        // The towns were saved in play (TF's slices are a fresh game's TWNn); the import reads TF, so nothing is lost.
        Assert.Equal(P1Apple2Tables.TownFiles, d.PlayedFiles);
        Assert.False(ImportRunner.CanRestoreMaps(d));
        Assert.Contains("the import is the same as from a fresh copy", ImportRunner.PlayedWarning(d));
        Assert.DoesNotContain(scan.Detections, x => x.Status == DetectionStatus.UnknownBuild);

        var one = ImportRunner.Scan([Repo.Path(DskA)]);
        Assert.Equal(DetectionStatus.MissingDisk, Assert.Single(one.Detections, x => x.Profile.Id == "p1-apple2").Status);
    }

    /// <summary>
    /// The Apple I disks share file names with II's and III's Apple disks (MN*, ITEM, TINFO, DNGn, HELLO) and with the C64
    /// I (OUTDOORS, TINFO): every held Apple and C64 image names only its own game, and no other version reads as "another
    /// build" of p1-apple2.
    /// </summary>
    [RequiresFilesFact(Images + "phantasie2", Images + "phantasie3", "reverse-engineering/c64/original-images")]
    public void EachAppleAndC64Image_NamesOnlyItsOwnGame()
    {
        foreach (var root in new[] { Images, "reverse-engineering/c64/original-images/" })
            foreach (var game in new[] { "phantasie1", "phantasie2", "phantasie3" })
            {
                var dir = Repo.Path(root + game);
                if (!Directory.Exists(dir))
                    continue;
                var scan = ImportRunner.Scan([dir]);
                Assert.All(scan.Detections, d => Assert.Equal(game, d.Profile.Game));
                if (game != "phantasie1" || !root.StartsWith(Images, StringComparison.Ordinal))
                    Assert.DoesNotContain(scan.Detections, d => d.Profile.Id == "p1-apple2");
            }
    }

    [RequiresFilesFact(DskA, DskB)]
    public void AppleOnly_WritesEveryTableDosIWrites()
    {
        var r = Run();
        foreach (var f in Tables)
            Assert.True(r.Outputs.ContainsKey($"GameData/phantasie1/{f}"), f);
        // Every shipped DOS cell matched the Apple's value (no "neither" warning), and nothing else was warned.
        Assert.DoesNotContain(r.Warnings, w => !w.StartsWith("This copy of", StringComparison.Ordinal));
        Assert.Contains("GameData/phantasie1/strings/en.fallback.p1-apple2.json", r.AllOutputs.Keys);
    }

    /// <summary>The <c>.woz</c> originals are the same played copy: their import equals the <c>.dsk</c> pair's.</summary>
    [RequiresFilesFact(DskA, DskB, WozA, WozB)]
    public void WozPair_ImportsAsTheDskPair()
    {
        var woz = PipelineHarness.Run("p1-apple2", WozA, WozB);
        var dsk = Run();
        Assert.Equal(dsk.AllOutputs.Keys.Order(StringComparer.Ordinal), woz.AllOutputs.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, bytes) in dsk.AllOutputs)
            Assert.True(bytes.AsSpan().SequenceEqual(woz.AllOutputs[key]), $"{key} differs");
    }

    // ---- numbers ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Paths that may differ from the DOS build: text (the Apple's), provenance, the Apple's own text files (messages.json:
    /// its dungeon slots; scrolls), the region text pages, the dungeon messages and level sizes (compared below).
    /// </summary>
    private static readonly Regex AllowedDifference = new(
        @"^\$\.(_source|_note|_cipher|_format|_layout\.\w+|_messages_note)$" +
        @"|\.(name|full_name|offset|name_ptr_ds|name_ptr_a2|text|raw_line|raw|lines(\[\d+\])?|embedded_text|step|printable_ratio)$" +
        @"|\.(options|option_cells)(\[\d+\])?$|\.text_pages(\[\d+\](\.index)?)?$|^\$\.towns\[\d+\]\.dos_record$" +
        @"|^\$\.levels\.DNG\d+\.(size|raw_hex|messages(\..*)?)$|^\$\.monsters\[\d+\]\.raw_hex$",
        RegexOptions.CultureInvariant);

    [RequiresFilesFact(DskA, DskB, DosGameData + "/monsters.json")]
    public void EveryNumber_EqualsTheDosBuild()
    {
        var failures = new List<string>();
        foreach (var f in Tables.Where(f => f is not ("messages.json" or "scrolls.json")))
            failures.AddRange(Differences(Dos(f), Apple(f), "$").Where(p => !AllowedDifference.IsMatch(p)).Select(p => $"{f} {p}"));
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(40)));
        Assert.True(JsonNode.DeepEquals(new JsonArray([.. Dos("monsters.json")["monsters"]!.AsArray().Select(Numbers)]),
            new JsonArray([.. Apple("monsters.json")["monsters"]!.AsArray().Select(Numbers)])));
        // The towns' records equal DOS's before the name field.
        var dt = Dos("towns.json")["towns"]!.AsArray();
        var at = Apple("towns.json")["towns"]!.AsArray();
        for (var t = 0; t < 12; t++)
            Assert.Equal(((string)dt[t]!["dos_record"]!)[..(279 * 2)], ((string)at[t]!["dos_record"]!)[..(279 * 2)]);
        // Each level's bytes equal DOS's as far as both go (DOS's files carry a 20-byte tail; DNG6's is shorter).
        foreach (var (level, node) in Dos("dungeons.json")["levels"]!.AsObject())
        {
            var dos = (string)node!["raw_hex"]!;
            var apple = (string)Apple("dungeons.json")["levels"]![level]!["raw_hex"]!;
            var n = Math.Min(dos.Length, apple.Length);
            Assert.True(n >= 2 * 1900, level);
            Assert.Equal(dos[..n], apple[..n]);
        }
        foreach (var (region, node) in Dos("overworld.json")["regions"]!.AsObject())
            foreach (var key in new[] { "tiles", "features" })
                Assert.True(JsonNode.DeepEquals(node![key], Apple("overworld.json")["regions"]![region]![key]), $"{region}.{key}");
    }

    private static JsonNode Numbers(JsonNode? m)
    {
        var o = m!.AsObject().DeepClone().AsObject();
        o.Remove("name");
        var raw = Convert.FromHexString((string)o["raw_hex"]!);
        o["raw_hex"] = Convert.ToHexStringLower(raw[0x14..]);
        return o;
    }

    internal static List<string> Differences(JsonNode? dos, JsonNode? apple, string path)
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
        Walk(dos, apple, path);
        return output;
    }

    /// <summary>Every shipped cell: its <c>dos</c> value is DOS I's GameData, its <c>apple</c> value the raw Apple build's.</summary>
    [RequiresFilesFact(DskA, DskB, Cells, DosGameData + "/monsters.json")]
    public void ShippedCells_HoldDosIsValue_AndTheAppleBuildsValue()
    {
        var cells = JsonNode.Parse(File.ReadAllBytes(Repo.Path(Cells)))!["dos_cells"]!.AsArray();
        Assert.True(cells.Count > 100);
        var raw = Raw();
        var bad = new List<string>();
        foreach (var cell in cells)
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
                if (cell["byte"] is { } b)
                    return Convert.FromHexString((string)node!)[(int)b];
                if (cell["fog"] is not null)
                    return (int)node! is >= 120 and < 255 ? (int)node! - 120 : (int)node!;
                return node?.DeepClone();
            }
            if (!JsonNode.DeepEquals(Value(CellPatches.Get(dos.Parent, dos.Key)), cell["dos"]))
                bad.Add($"{where}: dos is not DOS I's value");
            if (!JsonNode.DeepEquals(Value(CellPatches.Get(apple.Parent, apple.Key)), cell["apple"]))
                bad.Add($"{where}: apple is not the Apple build's value");
            if (JsonNode.DeepEquals(cell["apple"], cell["dos"]))
                bad.Add($"{where}: the same value twice");
            // No DOS text rides along in the public file.
            if (cell["dos"] is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.String)
                bad.Add($"{where}: a text cell");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(50)));
    }

    // ---- text and menus ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Every event record a square of the level holds (DUNGON 8200: square value 220 + record) and that asks for n ≥ 2
    /// choices (8220, record byte 1) finds a message with n options, so a chest or a lever can be answered; the Apple's
    /// "SELECT 1 OR 2" is not part of a choice.
    /// </summary>
    [RequiresFilesFact(DskA, DskB)]
    public void EveryEventMenu_HasItsChoices()
    {
        var bad = new List<string>();
        foreach (var (level, node) in Apple("dungeons.json")["levels"]!.AsObject())
        {
            var raw = Convert.FromHexString((string)node!["raw_hex"]!);
            var messages = node["messages"]!.AsObject();
            for (var f = 0xdc; f <= 0xff; f++)
            {
                var rec = 0x4f6 + 5 * (f - 0xdc);
                int mid = raw[rec] & 0x7f, count = raw[rec + 1];
                if (count < 2 || raw[rec] == 0xff || Array.IndexOf(raw, (byte)f, 0, 1254) < 0)
                    continue;
                if (messages[mid.ToString(System.Globalization.CultureInfo.InvariantCulture)]?["options"] is not JsonArray o
                    || o.Count != count)
                    bad.Add($"{level} event 0x{f:x2}: message {mid} lacks its {count} choices");
                else if (o.Any(x => ((string)x!).Contains("SELECT", StringComparison.Ordinal) || ((string)x!).Length == 0))
                    bad.Add($"{level} message {mid}: {o.ToJsonString()}");
            }
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
        var chest = Apple("dungeons.json")["levels"]!["DNG1"]!["messages"]!["60"]!;
        Assert.Equal(["OPEN THE CHEST", "LEAVE IT"], chest["options"]!.AsArray().Select(x => (string)x!));
    }

    /// <summary>The Apple's text: its item and town names, its own spellings, the manual's spell names (D-087's rule);
    /// region texts and scrolls with their compass words turned to DOS's map (OUT1's inn lies WEST, as DOS's).</summary>
    [RequiresFilesFact(DskA, DskB)]
    public void Text_IsTheApples()
    {
        Assert.Equal("HEAL POT1", (string)Apple("items.json")["items"]![101]!["name"]!);
        Assert.Equal("RING I", (string)Apple("items.json")["items"]![141]!["name"]!);
        Assert.Equal("HOBBITON", (string)Apple("towns.json")["towns"]![3]!["name"]!);
        Assert.Equal("LIZARD MAN", (string)Apple("names.json")["races"]![13]!["name"]!);
        Assert.Equal("HEALING 1", (string)Apple("names.json")["spells"]![0]!["name"]!);
        Assert.Equal("DIVINE I", (string)Apple("spells.json")["spells"]![54]!["name"]!);
        Assert.Equal("FIRE ELEM", (string)Apple("elementals.json")["elementals"]![0]!["name"]!);
        Assert.Equal("FIRE ELEM", (string)Apple("names.json")["town_labels"]![12]!["name"]!);
        // D-089 (user s223): the Apple's two monster typos take DOS's spelling.
        Assert.Equal("APPRENTICE DEVIL", (string)Apple("monsters.json")["monsters"]![12]!["name"]!);
        Assert.Equal("APPRENTICE DEVIL", (string)Apple("names.json")["monsters"]![12]!["name"]!);
        Assert.Equal("MASTER ILLUSIONIST", (string)Apple("monsters.json")["monsters"]![58]!["name"]!);
        Assert.Equal("MASTER ILLUSIONIST", (string)Apple("names.json")["monsters"]![58]!["name"]!);
        var pages = Apple("overworld.json")["regions"]!["OUT1.DAT"]!["text_pages"]!.AsArray().Select(p => (string)p!["text"]!).ToList();
        Assert.Contains(pages, p => p.StartsWith("TO THE WEST IS A BUILDING", StringComparison.Ordinal));
        Assert.Contains(pages, p => p.Contains("TO THE EAST", StringComparison.Ordinal));
        Assert.Equal(10, Apple("messages.json")["areas"]!.AsArray().Count);
        Assert.Contains("GELNOR", (string)Apple("scrolls.json")["scrolls"]![0]!["text"]!);
        // The Apple's NORTHWEST / EAST (its map turned a quarter) read as DOS's NORTHEAST / SOUTH; WEST-ERN as NORTH-ERN.
        var pools = (string)Apple("scrolls.json")["scrolls"]![4]!["text"]!;
        Assert.Contains("THE NORTHEAST PART OF GELNOR", pools);
        Assert.Contains("THE THIRD POOL LIES SOUTH OF THE", pools);
        Assert.Contains("GREAT NORTH-", (string)Apple("scrolls.json")["scrolls"]![9]!["text"]!);
    }

    // ---- the public repo --------------------------------------------------------------------------------------------

    /// <summary>A shipped folder as the public repo's: the Apple import from it equals the one from the full GameData.</summary>
    [RequiresFilesFact(DskA, DskB)]
    public void PublicShipped_GivesTheSameTables()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p1a2-public-").FullName;
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

            var masked = PipelineHarness.RunWithShipped(shipped, "p1-apple2", DskA, DskB);
            Assert.DoesNotContain(masked.Warnings, w => !w.StartsWith("This copy of", StringComparison.Ordinal));
            foreach (var f in Tables)
                Assert.True(masked.Outputs[$"GameData/phantasie1/{f}"].AsSpan().SequenceEqual(Run().Outputs[$"GameData/phantasie1/{f}"]), f);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---- strings ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// The public repo's tables carry "..." for each original part. An Apple-only import, laid over them as the Client
    /// does, leaves none of I's lines (and of the shared table's I areas) on "...": the Apple's own lines, the common
    /// fallback and <c>fallback.p1-apple2.en.json</c> (the 32 ids the Apple's programs lack verbatim; 10-importer.md 7.14).
    /// </summary>
    [RequiresFilesFact(DskA, DskB)]
    public void Strings_PublicMask_AppleOnlyImportLeavesOnlyTheKnownGap()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p1a2s-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            Import(root, DskA, DskB);
            GameDataLocator.UseImport(root);
            var left = new List<string>();
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
                    var from = alts!.AsArray().Select(a => (string)a!["from"]!).ToList();
                    // Lines only DOS I and the Amiga both give (an Amiga-only line is an Amiga screen's).
                    var fromI = from.Contains("p1-dos-1987") && from.Contains("p1-amiga-1.0");
                    var otherGame = table == "shared" && Regex.IsMatch(id, @"^(p[234]\D|core\.p4\.)");
                    if (!fromI || otherGame)
                        continue;
                    if (shown[id].Contains("...", StringComparison.Ordinal) && !((string)committed[id]!).Contains("...", StringComparison.Ordinal))
                        left.Add($"{table} {id}");
                }
            }
            Assert.True(left.Count == StringGap, $"{left.Count}:\n" + string.Join("\n", left));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The I lines an Apple-only import leaves on "..." in the public repo (s222: none).</summary>
    private const int StringGap = 0;

    // ---- the whole flow ---------------------------------------------------------------------------------------------

    private static JsonNode Manifest(string root) =>
        JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, ImportRunner.ManifestName)))!;

    private static ImportReport Import(string root, params string[] sources)
    {
        var report = new ImportRunner(root, Repo.GameData).Run(ImportRunner.Scan(sources.Select(Repo.Path).ToList()));
        Assert.True(report.Committed);
        Assert.All(report.Versions, v => Assert.True(v.Ok, v.Error));
        return report;
    }

    private static string MonstersSource(string root) =>
        (string)JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "GameData", "phantasie1", "monsters.json")))!["_source"]!;

    /// <summary>An Apple-only import is a playable I: it loads through the game's loader, a new game starts, the party
    /// leaves Pelnor into a region with DOS's start fog.</summary>
    [RequiresFilesFact(DskA, DskB)]
    public void AppleOnlyImport_LoadsAndStartsAGame()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p1a2-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            Import(root, DskA, DskB);
            Assert.Equal("p1-apple2", (string?)Manifest(root)["games"]!["phantasie1"]!["tables"]);

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
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>With DOS I in the same run, DOS I's tables win; the Apple first and the C64 or the Amiga after: theirs win,
    /// and a later Apple import leaves them.</summary>
    [RequiresFilesFact(DskA, DskB, Adf, P1Dos, C64Side1, C64Side2)]
    public void OtherCopies_WinOverTheApple()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p1a2d-").FullName;
        try
        {
            var root = Path.Combine(dir, "both");
            var report = Import(root, DskA, DskB, P1Dos);
            Assert.Equal(["p1-dos-1987", "p1-apple2"], report.Versions.Select(v => v.ProfileId));
            Assert.Contains(report.Versions[1].Warnings, w => w.Contains("comes from another copy", StringComparison.Ordinal));
            Assert.Equal("p1-dos-1987", (string?)Manifest(root)["games"]!["phantasie1"]!["tables"]);
            Assert.StartsWith("PHANTASI.COM", MonstersSource(root));

            var c64 = Path.Combine(dir, "c64");
            report = Import(c64, DskA, DskB, C64Side1, C64Side2);
            Assert.Equal(["p1-c64", "p1-apple2"], report.Versions.Select(v => v.ProfileId));
            Assert.StartsWith("Phantasie I C64", MonstersSource(c64));

            var order = Path.Combine(dir, "order");
            Import(order, DskA, DskB);
            Assert.StartsWith(P1Apple2Tables.SourcePrefix, MonstersSource(order));
            Import(order, C64Side1, C64Side2);
            Assert.StartsWith("Phantasie I C64", MonstersSource(order));
            Assert.Equal("p1-c64", (string?)Manifest(order)["games"]!["phantasie1"]!["tables"]);
            Import(order, DskA, DskB);
            Assert.StartsWith("Phantasie I C64", MonstersSource(order));

            var amiga = Path.Combine(dir, "amiga");
            Import(amiga, DskA, DskB);
            Import(amiga, Adf);
            Assert.StartsWith("Phantasie I Amiga", MonstersSource(amiga));
            Assert.Equal("p1-amiga-1.0", (string?)Manifest(amiga)["games"]!["phantasie1"]!["tables"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
