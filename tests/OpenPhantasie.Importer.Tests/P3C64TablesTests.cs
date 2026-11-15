using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Pipelines.P3C64;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// III's tables from the C64 disk alone (<see cref="P3C64Tables"/>, s218): detection of v1.0 and v1.2 (alt), the ten files
/// DOS III's pipeline writes, v1.2's tables equal to v1.0's, the extract's C64 values where FORMAT.md section 3 says they
/// differ from DOS's, and the starter party's records (FORMAT.md 3.5). <see cref="P3C64ImportTests"/> checks the shipped
/// cells (DOS's values) and the import.
/// </summary>
public class P3C64TablesTests
{
    private const string Images = "reverse-engineering/c64/original-images/phantasie3/";
    public const string V10S1 = Images + "c64pp/phantasie_iii_s1[ssi_1987](v1_0)(!).g64";
    public const string V10S2 = Images + "c64pp/phantasie_iii_s2[ssi_1987](v1_0)(!).g64";
    public const string V12S1 = Images + "c64pp/phantasie_iii_s1[ssi_1987](v1_2)(alt).g64";
    public const string V12S2 = Images + "c64pp/phantasie_iii_s2[ssi_1987](v1_2)(alt).g64";
    public const string V11A = Images + "v1.1/Phantasie_III_v1.1_1987_SSI_Side_A.d64";
    public const string V11B = Images + "v1.1/Phantasie_III_v1.1_1987_SSI_Side_B.d64";
    public const string HeldS1 = Images + "Phantasie_III_1987_SSI_Disk_1_of_2_Side_B.d64";
    public const string HeldS2 = Images + "Phantasie_III_1987_SSI_Disk_2_of_2_Side_A.d64";

    public static readonly string[] Tables = P3AmigaTablesTests.Tables;

    /// <summary>Run <paramref name="body"/> with an import context for the <paramref name="profile"/> detection of
    /// <paramref name="sources"/>.</summary>
    public static T WithContext<T>(string profile, Func<ImportContext, T> body, string? shippedRoot = null, params string[] sources)
    {
        var scan = ImportRunner.Scan(sources.Select(Repo.Path).ToList());
        var detection = scan.Detections.Single(d => d.Profile.Id == profile);
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

    public static T V10<T>(Func<ImportContext, T> body, string? shippedRoot = null) =>
        WithContext("p3-c64", body, shippedRoot, V10S1, V10S2);

    public static T V12<T>(Func<ImportContext, T> body, string? shippedRoot = null) =>
        WithContext("p3-c64-1.2", body, shippedRoot, V12S1, V12S2);

    private static Dictionary<string, JsonObject>? _extract;

    private static Dictionary<string, JsonObject> Extract() =>
        _extract ??= V10(ctx => new P3C64Tables(ctx.Files).Extract());

    [RequiresFilesFact(V10S1, V10S2, V12S1, V12S2)]
    public void Detects_V10_And_V12_EachAsItsOwnProfile()
    {
        var v10 = ImportRunner.Scan([Repo.Path(V10S1), Repo.Path(V10S2)]);
        Assert.Equal(DetectionStatus.Ready, Assert.Single(v10.Detections, d => d.Profile.Id == "p3-c64").Status);
        Assert.Empty(Assert.Single(v10.Detections, d => d.Profile.Id == "p3-c64").PlayedFiles);
        Assert.DoesNotContain(v10.Detections, d => d.Profile.Id == "p3-c64-1.2");
        var v12 = ImportRunner.Scan([Repo.Path(V12S1), Repo.Path(V12S2)]);
        Assert.Equal(DetectionStatus.Ready, Assert.Single(v12.Detections, d => d.Profile.Id == "p3-c64-1.2").Status);
        Assert.Empty(Assert.Single(v12.Detections, d => d.Profile.Id == "p3-c64-1.2").PlayedFiles);
        Assert.DoesNotContain(v12.Detections, d => d.Profile.Id == "p3-c64"); // not "another build" of v1.0 either
        var one = ImportRunner.Scan([Repo.Path(V10S1)]);
        Assert.Equal(DetectionStatus.MissingDisk, Assert.Single(one.Detections, d => d.Profile.Id == "p3-c64").Status);
    }

    /// <summary>The held v1.1 pair carries v1.0's programs and roster (only the utility B differs): it is v1.0, fresh. The
    /// held cracked pair is v1.0 too, played (its roster, the towns' stock).</summary>
    [RequiresFilesFact(V11A, V11B, HeldS1, HeldS2)]
    public void HeldD64Pairs_AreV10()
    {
        var v11 = ImportRunner.Scan([Repo.Path(V11A), Repo.Path(V11B)]);
        Assert.Empty(Assert.Single(v11.Detections, d => d.Profile.Id == "p3-c64" && d.Status == DetectionStatus.Ready).PlayedFiles);
        var held = ImportRunner.Scan([Repo.Path(HeldS1), Repo.Path(HeldS2)]);
        var d = Assert.Single(held.Detections, d => d.Profile.Id == "p3-c64" && d.Status == DetectionStatus.Ready);
        Assert.Contains("PCHARS0", d.PlayedFiles);
        Assert.Contains("TOWN A", d.PlayedFiles);
    }

    [RequiresFilesFact(V10S1, V10S2)]
    public void Build_GivesEveryTableDosIIIWrites()
    {
        var built = V10(P3C64Tables.Build);
        Assert.Equal(Tables.Order(), built.Select(t => t.File).Order());
        Assert.All(built, t => Assert.StartsWith(P3C64Tables.Label, (string)t.Node["_source"]!));
        // Optional dump of the raw build for tools/scripts/build_c64_p3_import.py --built (the cells file is made from it).
        if (Environment.GetEnvironmentVariable("P3C64_DUMP") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            foreach (var (file, node) in built)
                File.WriteAllText(Path.Combine(dir, file), node.ToJsonString(new System.Text.Json.JsonSerializerOptions
                    { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
    }

    /// <summary>v1.2 (alt) gives the same tables as v1.0 (FORMAT.md 1: the same data files and tables, the area levels 2
    /// bytes later in its PHANT.OBJ).</summary>
    [RequiresFilesFact(V10S1, V10S2, V12S1, V12S2)]
    public void V12_GivesV10sTables()
    {
        var v10 = V10(P3C64Tables.Build).ToDictionary(t => t.File, t => t.Node);
        var v12 = V12(P3C64Tables.Build).ToDictionary(t => t.File, t => t.Node);
        foreach (var f in Tables)
        {
            var diffs = P3AmigaTablesTests.Differences(v10[f], v12[f], "$")
                .Where(d => !d.EndsWith("._address", StringComparison.Ordinal) && !d.EndsWith("._note", StringComparison.Ordinal))
                .ToList();
            Assert.True(diffs.Count == 0, $"{f}: {string.Join(", ", diffs.Take(10))}");
        }
    }

    // ---- the C64's own values (FORMAT.md section 3) -------------------------------------------------------------

    [RequiresFilesFact(V10S1, V10S2)]
    public void Extract_HoldsTheC64sOwnValues()
    {
        var x = Extract();
        var mon = x["monsters.json"]["monsters"]!.AsArray();
        Assert.Equal("Drip Slimes", (string?)mon[2]!["name"]);
        Assert.Equal(87, (int)mon[2]!["xp"]!);
        Assert.Equal(100, (int)mon[9]!["next_group_chance"]!);
        Assert.All(mon, m => Assert.True((int)m!["byte_27"]! is >= 126 and <= 202));

        var items = x["items.json"];
        Assert.Equal("HALBERD", (string?)items["items"]![80]!["name"]);
        Assert.Equal("SWORD +3", (string?)items["items"]![90]!["name"]);
        Assert.Equal(0, (int)items["items"]![42]!["requirement"]!);
        Assert.Equal(234, (int)items["items"]![27]!["value"]!);
        Assert.Equal(89, (int)items["items"]![69]!["value"]!);
        Assert.Equal(199, (int)items["town_price_index"]![6]!["index"]!);

        var spells = x["spells.json"]["spells"]!.AsArray();
        Assert.Equal("HEAL 1", (string?)spells[0]!["name"]);
        Assert.Equal("", (string?)spells[52]!["name"]);
        Assert.Equal("DIVINE", (string?)spells[56]!["name"]);

        var names = x["names.json"];
        Assert.Equal("FIGHTER", (string?)names["classes"]![0]!["name"]);
        Assert.Equal("TROLL", (string?)names["races"]![15]!["name"]);
        Assert.Equal("ELEMENTAL", (string?)names["races"]![16]!["name"]);
        Assert.Null((string?)names["races"]![5]!["name"]);
        Assert.Equal("CAVE SMELLING OF SULFUR", (string?)names["places"]![7]!["name"]);
        Assert.Equal("FIRE BOW", (string?)names["skills"]![8]!["name"]);

        var towns = x["towns.json"]["towns"]!.AsArray();
        Assert.Equal("PENDRAGON", (string?)towns[1]!["name"]);
        Assert.Equal(5, (int)towns[1]!["stock"]![61]!);
        Assert.All(towns.Skip(1), t => Assert.Equal(0, (int)t!["kinds"]!));

        var scrolls = x["scrolls.json"];
        Assert.Equal(19, scrolls["records"]!.AsArray().Count);
        Assert.Contains("I, KILMOR, AM WRITING", (string)scrolls["records"]![0]!["text"]!, StringComparison.Ordinal);

        var rt = x["rule-tables.json"];
        Assert.Equal(15, rt["hp_dice"]!["by_class"]!["Fighter"]!.AsArray().Count);
        Assert.Equal(25, rt["wilderness_area_levels"]!["values"]!.AsArray().Count);
    }

    // ---- the starter party (FORMAT.md 3.5) ------------------------------------------------------------------------

    [RequiresFilesFact(V10S1, V10S2, V12S1, V12S2)]
    public void StarterGuild_IsTheSixOfTheDisk_EachVersionItsOwnRolls()
    {
        var v10 = V10(ctx => P3C64StarterGuild.Read(ctx.Files))!;
        var v12 = V12(ctx => P3C64StarterGuild.Read(ctx.Files))!;
        string[] names = ["CHUMLEY", "TRACKER", "FINGERS", "GRASSHOPPER", "BONES", "SAURIAN"];
        string[] races = ["Troll", "Human", "Sprite", "Elf", "Gnome", "Elf"];
        string[] classes = ["Fighter", "Ranger", "Thief", "Monk", "Priest", "Wizard"];
        foreach (var node in new[] { v10, v12 })
        {
            var m = node["members"]!.AsArray();
            Assert.Equal(names, m.Select(x => (string)x!["name"]!));
            Assert.Equal(races, m.Select(x => (string)x!["race"]!));
            Assert.Equal(classes, m.Select(x => (string)x!["class"]!));
            Assert.All(m, x =>
            {
                Assert.Equal(1, (int)x!["level"]!);
                Assert.Equal("Ok", (string)x["status"]!);
                Assert.Equal(3, (int)x["origin"]!); // Scandor
                Assert.Equal(1, (int)x["town"]!); // Pendragon
                Assert.Equal(256 * (int)x["social_class"]!, (int)x["gold"]!);
                Assert.Equal(22, (int)x["worn"]!["armor"]!);
            });
        }
        // v1.0: CHUMLEY STR 22 INT 10 DEX 15 CON 16 CHA 5 LCK 8, HP 14; v1.2: INT 4, CHA 12, LCK 11, HP 12.
        Assert.Equal([22, 10, 15, 16, 5, 8], v10["members"]![0]!["attributes"]!.AsArray().Select(v => (int)v!));
        Assert.Equal([22, 4, 15, 17, 12, 11], v12["members"]![0]!["attributes"]!.AsArray().Select(v => (int)v!));
        Assert.Equal(14, (int)v10["members"]![0]!["hp_max"]!);
        Assert.Equal(12, (int)v12["members"]![0]!["hp_max"]!);
        // TRACKER (Ranger) knows Heal 1 and may learn Fireflash 1; FINGERS the Thief wears the Short Bow (44).
        Assert.Equal(1, (int)v10["members"]![1]!["spells_known"]![0]!);
        Assert.Equal(2, (int)v10["members"]![1]!["spells_known"]![4]!);
        Assert.Equal(44, (int)v10["members"]![2]!["worn"]!["bow"]!);
    }

    [RequiresFilesFact(HeldS1, HeldS2)]
    public void PlayedRoster_GivesNoStarterGuild() =>
        Assert.Null(WithContext("p3-c64", ctx => P3C64StarterGuild.Read(ctx.Files), null, HeldS1, HeldS2));
}
