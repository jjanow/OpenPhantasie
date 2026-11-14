using System.Text.Json.Nodes;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Importer.Pipelines;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// Imports without I's tables and the public string tables (10-importer.md 7.6, s163): a game's chain (shared → I → its
/// own table) carries "..." for every original part, a version gives only the lines its files hold, and the shipped
/// fallbacks give the remaster's wording for the rest: <c>phantasie1/strings/fallback.en.json</c> (shared and I tables,
/// written by every II, III and IV import) and each game's own file (its table). A DOS-only III, an ST-only II and an
/// Apple-only II leave no id their game shows on "..."; s212: so does an Apple-only III (v3.0 or [cr]), with its own
/// <c>phantasie3/strings/fallback.p3-apple2.en.json</c>. The scopes are built from <c>en.sources.json</c> at test time,
/// so ids a later <c>build_string_sources.py</c> run adds fail here until they get a wording.
/// </summary>
[Collection(nameof(LocatorCollection))]
public class P3StringFallbackTests
{
    private const string P3Dos = "reverse-engineering/dos/original-images/phantasie3/phantasie3-1988-ssi-360k.ima";
    private const string A2SideA = "reverse-engineering/apple2/original-images/phantasie2/4am-crack/phantasie2-4am-side-a.dsk";
    private const string A2SideB = "reverse-engineering/apple2/original-images/phantasie2/4am-crack/phantasie2-4am-side-b.dsk";
    private const string Stx1 = "reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk1.stx";
    private const string Stx2 = "reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk2.stx";
    private const string GameData = "src/OpenPhantasie.Data/GameData";
    private const string Common = GameData + "/phantasie1/" + ImportedStrings.ShippedFallbackFile;

    /// <summary>Ids no game but I (or IV) shows: I's Amiga field / map / town screens, I's ending, IV's (the files' notes).</summary>
    private static readonly string[] OnlyIOrIv = ["p1field.", "p1map.", "p1town.", "amiga.", "core.endgame.", "p4.", "core.p4."];

    /// <summary>III's own ids in the shared table (II never shows them).</summary>
    private static readonly string[] OnlyIii = ["p3", "enum.P3", "module.p3", "transfer.title_into_iii", "transfer.erase_question_iii"];

    private static string[] Chain(string game) => ["shared", "phantasie1", game];

    private static JsonObject Sources(string table) =>
        JsonNode.Parse(File.ReadAllBytes(Repo.Path($"{GameData}/{table}/{ImportedStrings.SourcesFile}")))!["strings"]!.AsObject();

    private static Dictionary<string, string> Committed(string table) =>
        JsonNode.Parse(File.ReadAllBytes(Repo.Path($"{GameData}/{table}/strings/en.json")))!["strings"]!.AsObject()
            .Where(kv => kv.Value is JsonValue).ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<string>());

    private static string Masked(JsonNode alts) =>
        string.Concat(alts.AsArray()[0]!["parts"]!.AsArray().Select(p => p is JsonArray ? "..." : (string)p!));

    private static JsonObject Shipped(string game, string file = ImportedStrings.ShippedFallbackFile) =>
        JsonNode.Parse(File.ReadAllBytes(Repo.Path($"{GameData}/{game}/{file}")))!["tables"]!.AsObject();

    /// <summary>s212: the Apple-only III's own shipped fallback (the lines DOS III or the Amiga gives and the Apple does not).</summary>
    private static readonly string AppleFallback = ImportedStrings.ShippedProfileFallbackFile("p3-apple2");

    /// <summary>A game's chain as <c>StringTable.ForGame</c> lays it over the public tables, and the masked text per id.</summary>
    private static (Dictionary<string, string> Shown, Dictionary<string, string> Masked) PublicChain(string game)
    {
        var shown = new Dictionary<string, string>(StringComparer.Ordinal);
        var masked = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var table in Chain(game))
        {
            var layer = Committed(table);
            foreach (var (id, alts) in Sources(table))
                masked[id] = layer[id] = Masked(alts!);
            foreach (var file in GameDataLocator.ImportedStringFiles(table))
                foreach (var (id, text) in JsonNode.Parse(File.ReadAllBytes(file))!["strings"]!.AsObject())
                    layer[id] = (string)text!;
            foreach (var (id, text) in layer)
                shown[id] = text;
        }
        return (shown, masked);
    }

    /// <summary>The ids of <paramref name="game"/>'s chain that hold original text and that the game can show.</summary>
    private static IEnumerable<string> Scope(string game) =>
        Chain(game).SelectMany(t => Sources(t).Select(kv => kv.Key)).Distinct()
            .Where(id => !OnlyIOrIv.Any(p => id.StartsWith(p, StringComparison.Ordinal)))
            .Where(id => game != "phantasie2" || !OnlyIii.Any(p => id.StartsWith(p, StringComparison.Ordinal)));

    private static string ImportInto(string dir, params string[] sources)
    {
        var root = Path.Combine(dir, "import");
        var report = new ImportRunner(root, Repo.GameData).Run(ImportRunner.Scan(sources.Select(Repo.Path).ToList()));
        Assert.All(report.Versions, v => Assert.True(v.Ok, v.Error));
        return root;
    }

    private static void AssertNoEllipsis(string game, params string[] sources)
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-fallback-").FullName;
        try
        {
            GameDataLocator.UseImport(ImportInto(dir, sources));
            var (shown, masked) = PublicChain(game);
            var left = Scope(game).Where(id => shown[id] == masked[id] && masked[id].Contains("...", StringComparison.Ordinal))
                .Select(id => $"{id}: '{shown[id]}'").ToList();
            Assert.True(left.Count == 0, string.Join("\n", left));
            var table = StringTable.ForGame(game == "phantasie2" ? GameId.P2 : GameId.P3);
            Assert.All(Scope(game), id => Assert.True(table.Contains(id), id));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// Each fallback file holds only its own tables (the common one shared and I, a game's its own), no id is in two
    /// files (so the order of two fallback files in one table never matters), every id is one with original text, and
    /// every format item of the table's text is kept. s212: III's Apple file may hold the shared, I and III tables.
    /// </summary>
    [RequiresFilesFact(Common)]
    public void ShippedFallbacks_OwnTablesOnly_NoIdTwice_FormatItemsKept()
    {
        var files = new Dictionary<string, JsonObject>
        {
            ["phantasie1"] = Shipped("phantasie1"), ["phantasie2"] = Shipped("phantasie2"), ["phantasie3"] = Shipped("phantasie3"),
            ["phantasie3 p3-apple2"] = Shipped("phantasie3", AppleFallback),
        };
        Assert.Equal(["phantasie1", "shared"], files["phantasie1"].Select(kv => kv.Key).Order(StringComparer.Ordinal));
        Assert.Equal(["phantasie2"], files["phantasie2"].Select(kv => kv.Key));
        Assert.Equal(["phantasie3"], files["phantasie3"].Select(kv => kv.Key));
        Assert.Equal(["phantasie1", "phantasie3", "shared"],
            files["phantasie3 p3-apple2"].Select(kv => kv.Key).Order(StringComparer.Ordinal));
        var bad = new List<string>();
        var seen = new HashSet<(string, string)>();
        foreach (var (game, tables) in files)
            foreach (var (table, ids) in tables)
            {
                var sources = Sources(table);
                var committed = Committed(table);
                foreach (var (id, text) in ids!.AsObject())
                {
                    if (!seen.Add((table, id)))
                        bad.Add($"{table} {id}: in two fallback files");
                    if (!sources.ContainsKey(id))
                        bad.Add($"{table} {id}: no original text to stand in for");
                    else if (FormatItems((string)text!) != FormatItems(committed[id]))
                        bad.Add($"{table} {id}: format items differ ('{text}' vs '{committed[id]}')");
                }
            }
        Assert.True(bad.Count == 0, string.Join("\n", bad));

        static string FormatItems(string s) =>
            string.Join(",", System.Text.RegularExpressions.Regex.Matches(s, @"\{\d+[^}]*\}").Select(m => m.Value).Order());
    }

    /// <summary>A DOS-only III import laid over the public tables as the Client does leaves no id III shows on "...".</summary>
    [RequiresFilesFact(P3Dos, Common)]
    public void PublicMask_DosIiiOnly_NoEllipsis()
    {
        AssertNoEllipsis("phantasie3", P3Dos);
        var dir = Directory.CreateTempSubdirectory("phantasie-p3s-").FullName;
        try
        {
            GameDataLocator.UseImport(ImportInto(dir, P3Dos));
            Assert.Equal(["en.fallback.p3-dos-1988.json", "en.p3-dos-1988.json"],
                GameDataLocator.ImportedStringFiles("phantasie1").Select(Path.GetFileName));
            // An id DOS III lacks reads the remaster's wording.
            Assert.Equal("NOBODY OPENS THE LOCK", StringTable.ForGame(GameId.P3).Get("core.dungeon.no_one_can_pick_the_lock"));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    private const string P3Adf = "reverse-engineering/amiga/original-images/phantasie3/phantasie3-v1.0-1987-ssi.adf";

    /// <summary>
    /// An Amiga-only III (s210): the Amiga gives most of III's lines (its own alternatives in <c>en.sources.json</c>);
    /// the few only DOS III holds, and those neither holds, get the remaster's wording.
    /// </summary>
    [RequiresFilesFact(P3Adf, Common)]
    public void PublicMask_AmigaIiiOnly_NoEllipsis() => AssertNoEllipsis("phantasie3", P3Adf);

    /// <summary>
    /// s212 (user): an Amiga-only III shows the Amiga's own wording for the Mystic's Scandor line and the greeting answer
    /// (<c>en.own.p3-amiga-1.0.json</c>, over III's table), and neither id is in its fallback any more.
    /// </summary>
    [RequiresFilesFact(P3Adf, Common)]
    public void AmigaIiiOnly_OwnWording_ForScandorAndGreeting()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p3aown-").FullName;
        try
        {
            var root = ImportInto(dir, P3Adf);
            GameDataLocator.UseImport(root);
            var table = StringTable.ForGame(GameId.P3);
            Assert.Equal("and heroes of Scandor.", table.Get("core.p3.mystic.heroes_of_scandor"));
            Assert.Equal("They greet you!", table.Get("core.p3.greetings"));
            var fallback = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "GameData", "phantasie3", "strings",
                "en.fallback.p3-amiga-1.0.json")))!["strings"]!.AsObject();
            Assert.False(fallback.ContainsKey("core.p3.greetings"));
            Assert.False(fallback.ContainsKey("core.p3.mystic.heroes_of_scandor"));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    private const string WozA = P3Apple2TablesTests.WozA;
    private const string WozB = P3Apple2TablesTests.WozB;
    private const string Cr1A = P3Apple2TablesTests.Cr1A;
    private const string Cr1B = P3Apple2TablesTests.Cr1B;

    /// <summary>
    /// s212: an Apple-only III (v3.0 originals): the Apple gives 17 status / class words; the rest of what DOS III or the
    /// Amiga gives takes the remaster's wording from <c>phantasie3/strings/fallback.p3-apple2.en.json</c>.
    /// </summary>
    [RequiresFilesFact(WozA, WozB, Common)]
    public void PublicMask_AppleIiiOnly_NoEllipsis()
    {
        AssertNoEllipsis("phantasie3", WozA, WozB);
        var dir = Directory.CreateTempSubdirectory("phantasie-p3a2s-").FullName;
        try
        {
            GameDataLocator.UseImport(ImportInto(dir, WozA, WozB));
            var table = StringTable.ForGame(GameId.P3);
            // The Apple's own word wins over the fallback under it; a line it lacks reads the remaster's wording.
            Assert.Equal("MONK", table.Get("p3combat.class.Monk"));
            Assert.Equal("Welcome...", table.Get("core.p3.greetings"));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>s212: the [cr] (v1.0) sides give no Apple line at all; the fallbacks cover every id III shows.</summary>
    [RequiresFilesFact(Cr1A, Cr1B, Common)]
    public void PublicMask_AppleIiiCrOnly_NoEllipsis() => AssertNoEllipsis("phantasie3", Cr1A, Cr1B);

    /// <summary>An ST-only II: the ST gives II's lines; the I-table ids it lacks (e.g. the Mystic's score line) get the remaster's wording.</summary>
    [RequiresFilesFact(Stx1, Stx2, Common)]
    public void PublicMask_StOnly_NoEllipsis() => AssertNoEllipsis("phantasie2", Stx1, Stx2);

    /// <summary>An Apple-only II (s160's check covered only the ids the ST gives; the Mystic's score line was left on "...").</summary>
    [RequiresFilesFact(A2SideA, A2SideB, Common)]
    public void PublicMask_AppleOnly_NoEllipsis() => AssertNoEllipsis("phantasie2", A2SideA, A2SideB);

    private const string P4Disk1 = "reverse-engineering/x68000/original-images/phantasie4/Phantasie IV (1991)(Starcraft)(Disk 1 of 2).dim";
    private const string P4Disk2 = "reverse-engineering/x68000/original-images/phantasie4/Phantasie IV (1991)(Starcraft)(Disk 2 of 2).dim";

    /// <summary>
    /// An X68000-only IV: the shared table's ids IV can show (its Hall's <c>core.p4.</c> lines and the shell's own:
    /// everything but the I / II / III screens' ids) leave nothing on "..." (IV's own text is the id-keyed patch, 4.6).
    /// </summary>
    [RequiresFilesFact(P4Disk1, P4Disk2, Common)]
    public void PublicMask_IvOnly_SharedTableNoEllipsis()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p4s-").FullName;
        try
        {
            GameDataLocator.UseImport(ImportInto(dir, P4Disk1, P4Disk2));
            var layer = Committed("shared");
            var masked = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (id, alts) in Sources("shared"))
                masked[id] = layer[id] = Masked(alts!);
            foreach (var file in GameDataLocator.ImportedStringFiles("shared"))
                foreach (var (id, text) in JsonNode.Parse(File.ReadAllBytes(file))!["strings"]!.AsObject())
                    layer[id] = (string)text!;
            string[] otherGames = ["p1", "p2", "p3", "amiga.", "core.endgame.", "enum.P3", "module.p3"];
            var left = masked.Keys.Where(id => !otherGames.Any(p => id.StartsWith(p, StringComparison.Ordinal)))
                .Where(id => layer[id] == masked[id] && masked[id].Contains("...", StringComparison.Ordinal))
                .Select(id => $"{id}: '{layer[id]}'").ToList();
            Assert.True(left.Count == 0, string.Join("\n", left));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The Apple-only II and DOS III in one import folder: both games still leave nothing on "...".</summary>
    [RequiresFilesFact(P3Dos, A2SideA, A2SideB, Common)]
    public void PublicMask_AppleIiAndDosIii_BothHold()
    {
        AssertNoEllipsis("phantasie3", P3Dos, A2SideA, A2SideB);
        AssertNoEllipsis("phantasie2", P3Dos, A2SideA, A2SideB);
    }
}
