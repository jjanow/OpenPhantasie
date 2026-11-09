using System.Text.Json.Nodes;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Pipelines.P2Apple2;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// The Apple II Phantasie II pipeline (<c>p2-apple2</c>). Layer A (golden): the RDOS reader, the raw tables equal
/// <c>extract_a2_p2_tables.py</c>'s files, the art equals <c>extract_a2_p2_graphics.py</c>'s PNGs and index, the pack
/// equals <c>apple2.json</c>. Layer B (no Python counterpart): the GameData built from the Apple alone loads and starts
/// a game, and equals the ST-built GameData on every number (D2-046..049, user s159: the ST's values whatever the
/// source); its text is the Apple's, the scroll compass turned to the ST orientation, spell names the manual's, and the
/// string tables lose every "..." of the public tables (D2-050).
/// </summary>
[Collection(nameof(LocatorCollection))]
public class P2Apple2PipelineTests
{
    private const string Profile = "p2-apple2";
    private const string SideA = "reverse-engineering/apple2/original-images/phantasie2/4am-crack/phantasie2-4am-side-a.dsk";
    private const string SideB = "reverse-engineering/apple2/original-images/phantasie2/4am-crack/phantasie2-4am-side-b.dsk";
    private const string MemA = "reverse-engineering/apple2/original-images/phantasie2/memorial-set-gog/PHANT2A.DSK";
    private const string MemB = "reverse-engineering/apple2/original-images/phantasie2/memorial-set-gog/PHANT2B.DSK";
    private const string Woz = "reverse-engineering/apple2/original-images/phantasie2/woz-original/phantasie2-disk1-side-a.woz";
    private const string Extracted = "reverse-engineering/apple2/phantasie2/extracted";
    private const string ArtRoot = "reverse-engineering/apple2/phantasie2/extracted/graphics";
    private const string StGameData = "src/OpenPhantasie.Data/GameData/phantasie2";

    private static PipelineHarness.Result Run() => PipelineHarness.Run(Profile, SideA, SideB);

    private static A2Source Source() => new(Run().Detection.Files!);

    private static JsonNode Out(string gameDataFile) => JsonNode.Parse(Run().Outputs[$"GameData/phantasie2/{gameDataFile}"])!;

    private static JsonNode St(string file) => JsonNode.Parse(File.ReadAllBytes(Repo.Path($"{StGameData}/{file}")))!;

    // ---- layer A --------------------------------------------------------------------------------------------------

    [RequiresFilesFact(SideA)]
    public void Rdos_ReadsTheCatalog()
    {
        var vol = RdosVolume.TryOpen(File.ReadAllBytes(Repo.Path(SideA)))!;
        Assert.NotNull(vol);
        Assert.Equal(72, vol.Entries.Count); // as a2_rdos.py lists side A
        Assert.Single(vol.Entries, e => e.Deleted);
        var town = vol.Files.Single(e => e.Name == "TOWN2");
        Assert.Equal(('A', 17744, 34), (town.Type, town.Length, town.Start));
        Assert.Equal(17744, vol.Read(town).Length);
        Assert.Null(RdosVolume.TryOpen(new byte[RdosVolume.ImageSize]));
    }

    [RequiresFilesFact(MemA, MemB)]
    public void Rdos_ReadsTheMemorialSetsDos33Order()
    {
        // s166: the Memorial Set's disks are the same RDOS disks in DOS 3.3 sector order. Catalog sector 0 is the same in
        // both orders, so the as-is read finds 8 entries; the reader keeps the order with the longer catalog.
        foreach (var (mem, fam, count) in new[] { (MemA, SideA, 72), (MemB, SideB, 30) })
        {
            var raw = File.ReadAllBytes(Repo.Path(mem));
            var vol = RdosVolume.TryOpen(raw)!;
            Assert.Equal(count, vol.Entries.Count);
            var reference = RdosVolume.TryOpen(File.ReadAllBytes(Repo.Path(fam)))!;
            Assert.Equal(reference.Entries.Count, vol.Entries.Count);
            var key = vol.Files.First(e => e.Name is "TOWN2" or "DUNGON2");
            Assert.Equal(reference.Read(reference.Files.Single(e => e.Name == key.Name)), vol.Read(key));
        }
    }

    [RequiresFilesFact(MemA, MemB)]
    public void Detection_MemorialSetIsReady_AndUnplayed()
    {
        var r = PipelineHarness.Run(Profile, MemA, MemB);
        Assert.Equal(DetectionStatus.Ready, r.Detection.Status);
        Assert.Empty(r.Detection.PlayedFiles);
        Assert.DoesNotContain(r.Warnings, w => w.Contains("played", StringComparison.Ordinal));
    }

    [RequiresFilesFact(MemA, MemB, StGameData + "/overworld.json")]
    public void MemorialSet_StartFogIsTheSts()
    {
        // The unplayed Apple disk's fog (OUT1 / OUT2 open, the rest fogged) equals the ST's start fog cell for cell.
        var apple = JsonNode.Parse(PipelineHarness.Run(Profile, MemA, MemB).Outputs["GameData/phantasie2/overworld.json"])!["regions"]!.AsObject();
        var st = St("overworld.json")["regions"]!.AsObject();
        Assert.Equal(st.Count, apple.Count);
        foreach (var (key, region) in st)
        {
            var a = apple[key]!["tiles"]!.AsArray();
            var t = region!["tiles"]!.AsArray();
            for (var y = 0; y < t.Count; y++)
                for (var x = 0; x < t[y]!.AsArray().Count; x++)
                    Assert.True((int)t[y]![x]! >= 120 == (int)a[y]![x]! >= 120, $"{key} ({x}, {y}): fog differs");
        }
    }

    [RequiresFilesFact(SideA, SideB, Woz)]
    public void Detection_DskPairIsReady_OneWozSideMissesTheOther()
    {
        var scan = ImportRunner.Scan([Repo.Path(SideA), Repo.Path(SideB)]);
        var d = Assert.Single(scan.Detections);
        Assert.Equal((Profile, DetectionStatus.Ready), (d.Profile.Id, d.Status));
        // The .woz is read since s169 (WozImage); side A alone is the version with side B missing.
        var woz = ImportRunner.Scan([Repo.Path(Woz)]);
        var w = Assert.Single(woz.Detections);
        Assert.Equal((Profile, DetectionStatus.MissingDisk), (w.Profile.Id, w.Status));
    }

    [RequiresFilesFact(SideA, SideB, Extracted)]
    public void RawTables_EqualThePythonExtract()
    {
        foreach (var (file, node) in A2Tables.Build(Source()))
        {
            var rel = $"{Extracted}/{file}";
            var bytes = PyJson.SerializeUtf8(node, 1);
            Golden.JsonEqualsFile(bytes, rel);
            Assert.True(Golden.BytesEqualFile(bytes, rel), $"{rel}: JSON-equal but not byte-equal");
        }
    }

    [RequiresFilesFact(SideA, SideB, ArtRoot)]
    public void Art_EqualsCommitted()
    {
        var outputs = Run().Outputs;
        var dir = Repo.Path(ArtRoot);
        int png = 0, json = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var rel = $"{ArtRoot}/{Path.GetRelativePath(dir, file).Replace('\\', '/')}";
            Assert.True(outputs.TryGetValue("files/" + rel, out var bytes), $"{rel}: committed but not produced");
            if (rel.EndsWith(".png", StringComparison.Ordinal))
            {
                Golden.PngEqualsFile(bytes!, rel);
                png++;
            }
            else
            {
                Assert.True(Golden.BytesEqualFile(bytes!, rel), $"{rel}: not byte-equal");
                json++;
            }
        }
        Assert.Equal(545, png);
        Assert.Equal(1, json);
        Assert.Equal(546, outputs.Keys.Count(k => k.StartsWith("files/", StringComparison.Ordinal)));
    }

    [RequiresFilesFact(SideA, SideB, StGameData)]
    public void Pack_IsByteEqualToApple2Json()
    {
        Assert.True(Golden.BytesEqualFile(Run().Outputs["GameData/phantasie2/graphics/apple2.json"], $"{StGameData}/graphics/apple2.json"));
    }

    // ---- layer B --------------------------------------------------------------------------------------------------

    [RequiresFilesFact(SideA, SideB)]
    public void AppleOnly_WritesTheTablesWithTheDecidedValues()
    {
        var r = Run();
        foreach (var f in new[] { "game.json", "monsters.json", "monster-traits.json", "items.json", "spells.json", "names.json",
                     "overworld.json", "dungeons.json", "scrolls.json", "towns.json", "creation-tables.json", "elementals.json",
                     "wilderness-encounters.json" })
            Assert.True(r.Outputs.ContainsKey($"GameData/phantasie2/{f}"), f);
        Assert.False(r.Outputs.ContainsKey("GameData/phantasie2/messages.json"));
        // D2-046..050 are decided (user s159): no open-question warnings, and no cell left at a value neither version has.
        foreach (var d in new[] { "D2-046", "D2-047", "D2-048", "D2-049", "D2-050" })
            Assert.DoesNotContain(r.Warnings, w => w.Contains(d, StringComparison.Ordinal));
        Assert.DoesNotContain(r.Warnings, w => w.Contains("neither the Apple", StringComparison.Ordinal));
        // The 4am crack is a played copy: the runner warns from the profile's pristine sha1s (the Memorial Set's,
        // s166), not the pipeline. OUTT2 ships stored back (756 B), so its size alone says nothing.
        Assert.DoesNotContain(r.Warnings, w => w.Contains("has been played", StringComparison.Ordinal));
        Assert.Contains("OUTT1", r.Detection.PlayedFiles);
        Assert.Contains("CHAR2", r.Detection.PlayedFiles);
        Assert.DoesNotContain("OUTT2", r.Detection.PlayedFiles);
        Assert.DoesNotContain("OUTT26", r.Detection.PlayedFiles);
        var warning = ImportRunner.PlayedWarning(r.Detection);
        Assert.Contains("fog is already lifted", warning);
        Assert.DoesNotContain("fights", warning);
        // D2-050: spell names from the manual's list (CB p.12); the two divine spells take the ST's names (user s170).
        var spells = Out("spells.json")["spells"]!.AsArray();
        Assert.Equal(56, spells.Count);
        Assert.Equal("HEALING 1", (string)spells[0]!["name"]!);
        Assert.Equal("SUMMONS ELEMENTAL", (string)spells[47]!["name"]!);
        Assert.Equal("MONSTER EVALUATION", (string)spells[51]!["name"]!);
        Assert.Equal("TRANSPORTATION", (string)spells[53]!["name"]!);
        Assert.Equal(("DIVINE I", 57), ((string)spells[54]!["name"]!, (int)spells[54]!["dos_slot"]!));
        Assert.Equal(("DIVINE II", 56), ((string)spells[55]!["name"]!, (int)spells[55]!["dos_slot"]!));
        Assert.DoesNotContain(spells, s => ((string)s!["name"]!).StartsWith("SPELL ", StringComparison.Ordinal));
        Assert.Equal("HEALING 1", (string)Out("names.json")["spells"]![0]!["name"]!);
    }

    private static A2StValues Shipped() =>
        A2StValues.Parse(JsonNode.Parse(File.ReadAllBytes(Path.Combine(Repo.GameData, "phantasie2", A2StValues.File)))!);

    /// <summary>The shipped ST cells (apple2-import.json) hold the committed ST GameData's values at their paths.</summary>
    [RequiresFilesFact(StGameData + "/monsters.json")] // s163: the ST GameData is not in the public repo
    public void ShippedStCells_EqualTheStGameData()
    {
        var doc = JsonNode.Parse(File.ReadAllBytes(Repo.Path($"{StGameData}/{A2StValues.File}")))!;
        var cells = doc["st_cells"]!.AsArray();
        Assert.Equal(37, cells.Count); // s169: + OUT1's unused feature record 14
        foreach (var cell in cells)
        {
            JsonNode? node = St((string)cell!["file"]!);
            foreach (var step in cell["path"]!.AsArray())
                node = step switch
                {
                    JsonObject m => node!.AsArray().First(e => m.All(kv => JsonNode.DeepEquals(e![kv.Key], kv.Value))),
                    JsonValue v when v.GetValueKind() == System.Text.Json.JsonValueKind.String => node![(string)v!],
                    _ => node![(int)step!],
                };
            var where = cell.ToJsonString();
            if (cell["byte"] is { } at)
                Assert.True(Convert.FromHexString((string)node!)[(int)at] == (int)cell["st"]!, where);
            else if (cell["fog"] is not null)
                Assert.True((int)node! == (int)cell["st"]! || (int)node! == (int)cell["st"]! + 120, where);
            else
                Assert.True(JsonNode.DeepEquals(node, cell["st"]), where);
        }
    }

    /// <summary>
    /// D2-046: the scrolls' compass words, turned to the ST orientation, are the ST's words in the same order (the
    /// texts agree; only the compass differs).
    /// </summary>
    [RequiresFilesFact(SideA, SideB, StGameData)]
    public void ScrollCompass_IsTheSts()
    {
        var turn = Shipped();
        var apple = Out("scrolls.json")["scrolls"]!.AsArray().Where(s => (int)s!["id"]! > 0)
            .SelectMany(s => s!["lines"]!.AsArray().SelectMany(l => turn.CompassWords((string)l!))).ToList();
        var st = St("scrolls.json")["scrolls"]!.AsArray()
            .SelectMany(s => s!["lines"]!.AsArray().SelectMany(l => turn.CompassWords(((string)l!).ToUpperInvariant()))).ToList();
        Assert.Equal(21, st.Count);
        Assert.Equal(st, apple);
        // Every turned line keeps its length (a boxed line's closing ! stays in its column).
        var pages = JsonNode.Parse(File.ReadAllBytes(Repo.Path($"{Extracted}/scrolls.json")))!["a2_pages"]!.AsObject();
        var turned = 0;
        foreach (var rec in Out("scrolls.json")["scrolls"]!.AsArray().Where(s => (int)s!["id"]! > 0))
        {
            var before = pages[$"SCROLLL{(int)rec!["id"]!}"]?.AsArray().Select(l => (string)l!).ToList() ?? [];
            var after = rec["lines"]!.AsArray().Select(l => (string)l!).ToList();
            Assert.Equal(before.Count, after.Count);
            for (var i = 0; i < before.Count; i++)
                if (before[i] != after[i])
                {
                    Assert.Equal(before[i].Length, after[i].Length);
                    turned++;
                }
        }
        Assert.Equal(14, turned);
    }

    [Fact]
    public void TurnCompass_SwapsAllAtOnceAtWordStarts()
    {
        var turn = Shipped();
        Assert.Equal("! WEST-ERN EASTERN BEASTS NORTH  !", turn.TurnCompass("! SOUTH-ERN NORTHERN BEASTS WEST !"));
        Assert.Equal("NORTH SOUTH EAST WEST", turn.TurnCompass("WEST EAST NORTH SOUTH"));
        Assert.Equal("! GO SOUTH   !", turn.TurnCompass("! GO EAST    !"));
        Assert.Equal("!GO NORTH!", turn.TurnCompass("!GO WEST!")); // no room: the box edge moves, nothing is cut
    }

    /// <summary>Every number equals the ST-built GameData (D2-046..049 decided s159: the ST's values).</summary>
    [RequiresFilesFact(SideA, SideB, StGameData)]
    public void Numbers_EqualTheSt()
    {
        var diffs = new List<string>();
        void Cmp(string where, JsonNode? a, JsonNode? s)
        {
            if (!JsonNode.DeepEquals(a, s))
                diffs.Add(where);
        }
        var am = Out("monsters.json")["monsters"]!.AsArray();
        var sm = St("monsters.json")["monsters"]!.AsArray();
        foreach (var f in new[] { "armor", "damage", "level", "xp", "special_chance", "specials", "hp_base", "combat_rating", "companions",
                     "reaction_modifier", "next_group_chance", "defense", "gold", "group_size_factor", "treasure_items", "attack_power" })
            for (var i = 0; i < 80; i++)
                Cmp($"monster {i}.{f}", am[i]![f], sm[i]![f]);
        // D2-047: VIPER, FIRE ELEMENTAL, PLUTO'S WYVERN / GIANT WYVERN / DRAGON-DEMON take the ST's numbers.
        Assert.Empty(diffs);

        var ai = Out("items.json")["items"]!.AsArray();
        var si = St("items.json")["items"]!.AsArray();
        for (var i = 0; i < 181; i++)
            foreach (var f in new[] { "value_factor", "kind", "points", "min_str", "str_check" })
                Cmp($"item {i}.{f}", ai[i]![f], si[i]![f]);
        Assert.Empty(diffs);

        var at = Out("towns.json")["towns"]!.AsArray();
        var st = St("towns.json")["towns"]!.AsArray();
        Assert.Equal(10, at.Count);
        for (var i = 0; i < 10; i++)
            foreach (var f in new[] { "price_index", "stock", "restock_items", "exit_region", "exit_x", "exit_y", "map_town_cells", "plane",
                         "transport_destinations" })
                Cmp($"town {i}.{f}", at[i]![f], st[i]![f]);
        Assert.True(diffs.Count == 0, string.Join(", ", diffs)); // D2-048

        var ac = Out("creation-tables.json");
        var sc = St("creation-tables.json");
        foreach (var t in new[] { "hp_factors", "mp_ceiling", "spell_levels" })
            Cmp(t, ac[t]!["by_class"], sc[t]!["by_class"]);
        Cmp("class mods", ac["class_attribute_modifiers"]!["by_class"], sc["class_attribute_modifiers"]!["by_class"]);
        Cmp("race mods", ac["race_attribute_modifiers"]!["by_race"], sc["race_attribute_modifiers"]!["by_race"]);
        Cmp("lifespan", ac["lifespan_bands"], sc["lifespan_bands"]);
        var ask = ac["skill_coefficients"]!["by_class_level"]!;
        var ssk = sc["skill_coefficients"]!["by_class_level"]!;
        foreach (var c in new[] { "THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD" })
            for (var lv = 0; lv < 15; lv++)
                for (var k = 0; k < 10; k++)
                    Cmp($"{c} L{lv + 1} skill {k}", ask[c]![lv]![k], ssk[c]![lv]![k]);
        Assert.Empty(diffs); // D2-049

        var ao = Out("overworld.json")["regions"]!.AsObject();
        var so = St("overworld.json")["regions"]!.AsObject();
        static int Unfog(int t) => t is >= 120 and < 255 ? t - 120 : t;
        foreach (var (key, reg) in ao)
            for (var y = 0; y < 20; y++)
                for (var x = 0; x < 26; x++)
                    if (Unfog((int)reg!["tiles"]![y]![x]!) != Unfog((int)so[key]!["tiles"]![y]![x]!))
                        diffs.Add($"{key}({x},{y})");
        Assert.Empty(diffs); // D2-049: OUT1 x2, OUT3 x4, OUT17, OUT31 x2

        // Text-cell args re-encoded in the ST's page form agree with the ST's everywhere, OUT28's entrances included.
        foreach (var (key, reg) in ao)
        {
            var sf = so[key]!["features"]!.AsArray().Select(f => ((int)f!["x"]!, (int)f["y"]!, (int)f["arg"]!, (int)f["dest_x"]!, (int)f["dest_y"]!))
                .Where(f => f != (25, 0, 0, 0, 0)).ToHashSet();
            var af = reg!["features"]!.AsArray().Select(f => ((int)f!["x"]!, (int)f["y"]!, (int)f["arg"]!, (int)f["dest_x"]!, (int)f["dest_y"]!))
                .Where(f => f != (25, 0, 0, 0, 0)).ToHashSet();
            if (!sf.SetEquals(af))
                diffs.Add(key);
        }
        // s169: OUT1's unused feature record 14 at (25, 0) (Apple dest_y 0) takes the ST's 32 like the other cells.
        Assert.Empty(diffs);
        diffs.Clear();

        var ad = Out("dungeons.json")["levels"]!.AsObject();
        var sd = St("dungeons.json")["levels"]!.AsObject();
        foreach (var (key, lv) in ad)
        {
            var a = Convert.FromHexString((string)lv!["raw_hex"]!);
            var s = Convert.FromHexString((string)sd[key]!["raw_hex"]!);
            Assert.Equal(2048, a.Length);
            for (var i = 0; i < A2Tables.LevelBytes; i++)
                if (a[i] != s[i])
                    diffs.Add($"{key}[{i}]");
        }
        Assert.Empty(diffs); // D2-049
    }

    // ---- text (D2-050) --------------------------------------------------------------------------------------------

    private static readonly string[] Tables = ["shared", "phantasie1", "phantasie2"];

    private static JsonObject Sources(string table) =>
        JsonNode.Parse(File.ReadAllBytes(Repo.Path($"src/OpenPhantasie.Data/GameData/{table}/{ImportedStrings.SourcesFile}")))!["strings"]!.AsObject();

    private static Dictionary<string, string> Committed(string table) =>
        JsonNode.Parse(File.ReadAllBytes(Repo.Path($"src/OpenPhantasie.Data/GameData/{table}/strings/en.json")))!["strings"]!.AsObject()
            .Where(kv => kv.Value is JsonValue).ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<string>());

    private static Dictionary<string, string> Written(PipelineHarness.Result r, string table, string file) =>
        r.AllOutputs.TryGetValue($"GameData/{table}/{file}", out var bytes)
            ? JsonNode.Parse(bytes)!["strings"]!.AsObject().ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<string>())
            : [];

    private static bool FromSt(JsonNode alts) => alts.AsArray().Any(a => (string)a!["from"]! == "p2-st-1.0");

    private static JsonObject? AppleAlt(JsonNode alts) => alts.AsArray().FirstOrDefault(a => (string)a!["from"]! == Profile)?.AsObject();

    /// <summary>
    /// The Apple's lines: an id it holds verbatim resolves to the committed text (its table's file); one it prints in its
    /// own wording resolves to that text, written to II's own-wording file only; every other line the ST gives gets the
    /// remaster's wording, and no id gets both.
    /// </summary>
    [RequiresFilesFact(SideA, SideB, StGameData)]
    public void Strings_TheApplesOwnLinesAndTheRemastersWording()
    {
        var r = Run();
        int verbatim = 0, own = 0, fallback = 0;
        var bad = new List<string>();
        var ownFile = Written(r, "phantasie2", ImportedStrings.OwnWordingFile(Profile));
        var ownIds = new HashSet<string>();
        foreach (var table in Tables)
        {
            var committed = Committed(table);
            var sources = Sources(table);
            var got = Written(r, table, ImportedStrings.OutputFile(Profile));
            var fb = Written(r, table, ImportedStrings.FallbackFile(Profile));
            foreach (var (id, alts) in sources)
            {
                if (AppleAlt(alts!) is { } a)
                {
                    if (a["wording"] is null)
                    {
                        verbatim++;
                        if (!got.TryGetValue(id, out var text) || text != committed[id])
                            bad.Add($"{table} {id}: verbatim line not resolved to the committed text");
                    }
                    else
                    {
                        own++;
                        ownIds.Add(id);
                        if (got.ContainsKey(id))
                            bad.Add($"{table} {id}: own wording written to the table's file (would reach other games)");
                        if (!ownFile.TryGetValue(id, out var text) || text.Trim().Length == 0)
                            bad.Add($"{table} {id}: own wording not resolved");
                    }
                    // s163: the common shared / I fallback lies under the Apple's lines (every import without I writes
                    // it); only II's own table keeps one source per id.
                    if (table == "phantasie2" && fb.ContainsKey(id))
                        bad.Add($"{table} {id}: both the Apple's line and a fallback");
                }
                else if (FromSt(alts!))
                {
                    if (fb.ContainsKey(id))
                        fallback++;
                    else
                        bad.Add($"{table} {id}: neither the Apple's line nor a fallback");
                }
            }
            foreach (var id in got.Keys.Where(k => !sources.ContainsKey(k)).Concat(fb.Keys.Where(k => !sources.ContainsKey(k))))
                bad.Add($"{table} {id}: written for an id without original text");
        }
        foreach (var id in ownFile.Keys.Where(k => !ownIds.Contains(k)))
            bad.Add($"own wording {id}: not an own-wording id");
        Assert.True(bad.Count == 0, string.Join("\n", bad));
        Assert.Equal((219, 95, 19), (verbatim, own, fallback)); // s165: p1field.scroll.ok ("O.K.", the ST has it too); s173: p2.terrain_key.party / .dungeon
        // One id is in both I's and II's table (two templates): II's template wins in the one file, as in II's chain.
        Assert.Equal(94, ownFile.Count);
    }

    /// <summary>II's chain as <c>StringTable.ForGame</c> lays it, over the public tables ("..." for each original part).</summary>
    private static Dictionary<string, string> PublicChainForP2()
    {
        var shown = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var table in Tables)
        {
            var layer = Committed(table);
            foreach (var (id, alts) in Sources(table))
                layer[id] = string.Concat(alts!.AsArray()[0]!["parts"]!.AsArray().Select(p => p is JsonArray ? "..." : (string)p!));
            foreach (var file in GameDataLocator.ImportedStringFiles(table))
                foreach (var (id, text) in JsonNode.Parse(File.ReadAllBytes(file))!["strings"]!.AsObject())
                    layer[id] = (string)text!;
            foreach (var (id, text) in layer)
                shown[id] = text;
        }
        return shown;
    }

    private static string ImportInto(string dir, params string[] sources)
    {
        var root = Path.Combine(dir, "import");
        var report = new ImportRunner(root, Repo.GameData).Run(ImportRunner.Scan(sources.Select(Repo.Path).ToList()));
        Assert.All(report.Versions, v => Assert.True(v.Ok, v.Error));
        return root;
    }

    /// <summary>
    /// The public repo's tables carry "..." for each original part (10-importer.md 7.6). An Apple-only import, laid over
    /// them as the Client does (fallback files first, the own wording last), leaves no id the ST gives on "...".
    /// </summary>
    [RequiresFilesFact(SideA, SideB, StGameData)]
    public void Strings_PublicMask_AppleOnlyImportLeavesNoEllipsis()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-a2s-").FullName;
        try
        {
            GameDataLocator.UseImport(ImportInto(dir, SideA, SideB));
            var shown = PublicChainForP2();
            var left = Tables.SelectMany(t => Sources(t).Where(kv => FromSt(kv.Value!)).Select(kv => kv.Key)).Distinct()
                .Where(id => shown[id].Contains("...", StringComparison.Ordinal) && !shown[id].EndsWith("SPEAKS...", StringComparison.Ordinal))
                .Select(id => $"{id}: '{shown[id]}'").ToList(); // (the Apple's own "...APPEARS AND SPEAKS..." ends so)
            Assert.True(left.Count == 0, string.Join("\n", left));
            Assert.Equal(["en.fallback.p2-apple2.json", "en.p2-apple2.json"],
                GameDataLocator.ImportedStringFiles("phantasie1").Select(Path.GetFileName));
            Assert.Equal(["en.fallback.p2-apple2.json", "en.p2-apple2.json", "en.own.p2-apple2.json"],
                GameDataLocator.ImportedStringFiles("phantasie2").Select(Path.GetFileName));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    private static readonly string[] OwnIdsOfSharedAndI =
        [.. new[] { "shared", "phantasie1" }.SelectMany(t => Sources(t).Where(kv => AppleAlt(kv.Value!)?["wording"] is not null).Select(kv => kv.Key))];

    private const string P1Dos = "reverse-engineering/dos/original-images/phantasie1/phantasie-1987-ssi-360k.ima";
    private const string Stx1 = "reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk1.stx";
    private const string Stx2 = "reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk2.stx";

    /// <summary>
    /// DOS I + the Apple in one run: Phantasie I keeps DOS I's text for every id the Apple prints in its own wording
    /// (the Apple's lines never reach I through the shared or I table); Phantasie II shows the Apple's.
    /// </summary>
    [RequiresFilesFact(SideA, SideB, P1Dos, StGameData)]
    public void Strings_WithDosI_TheApplesWordingStaysInII()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-a2p1-").FullName;
        try
        {
            GameDataLocator.UseImport(ImportInto(dir, SideA, SideB, P1Dos));
            Assert.Equal(30, OwnIdsOfSharedAndI.Length);
            var p1 = StringTable.ForGame(GameId.P1);
            var p2 = StringTable.ForGame(GameId.P2);
            var own = JsonNode.Parse(File.ReadAllBytes(Path.Combine(dir, "import", "GameData", "phantasie2",
                ImportedStrings.OwnWordingFile(Profile).Replace('/', Path.DirectorySeparatorChar))))!["strings"]!.AsObject();
            var shared = Committed("shared");
            var i = Committed("phantasie1");
            var differ = 0;
            foreach (var id in OwnIdsOfSharedAndI)
            {
                Assert.Equal(i.TryGetValue(id, out var t) ? t : shared[id], p1.Get(id));
                Assert.Equal((string)own[id]!, p2.Get(id));
                if (p1.Get(id) != p2.Get(id))
                    differ++;
            }
            Assert.True(differ >= 25, $"{differ}"); // a few Apple lines read like I's (WHO WILL USE?)
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// With the ST copy, II keeps the ST's text (D2-050): in the same run the Apple writes no own-wording file; when the
    /// ST comes in a later run, the Apple's earlier file no longer applies (II's tables are the ST's).
    /// </summary>
    [RequiresFilesFact(SideA, SideB, Stx1, Stx2, StGameData)]
    public void Strings_WithTheSt_TheStsTextWins()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-a2st-s-").FullName;
        try
        {
            var root = ImportInto(dir, SideA, SideB, Stx1, Stx2);
            Assert.False(File.Exists(Path.Combine(root, "GameData", "phantasie2", "strings", "en.own.p2-apple2.json")));
            GameDataLocator.UseImport(root);
            AssertStText();
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);

            Directory.CreateDirectory(dir);
            ImportInto(dir, SideA, SideB);
            root = ImportInto(dir, Stx1, Stx2);
            GameDataLocator.UseImport(root);
            AssertStText();
        }
        finally
        {
            GameDataLocator.UseImport(null);
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }

        static void AssertStText()
        {
            var p2 = StringTable.ForGame(GameId.P2);
            Assert.DoesNotContain("en.own.p2-apple2.json", GameDataLocator.ImportedStringFiles("phantasie2").Select(Path.GetFileName));
            var committed = new Dictionary<string, string>(Committed("shared"));
            foreach (var t in new[] { "phantasie1", "phantasie2" })
                foreach (var (id, text) in Committed(t))
                    committed[id] = text;
            foreach (var table in Tables)
                foreach (var (id, alts) in Sources(table))
                    if (AppleAlt(alts!)?["wording"] is not null && FromSt(alts!))
                        Assert.Equal(committed[id], p2.Get(id));
        }
    }

    /// <summary>Menus the ST shows as options are options here too, with the same count.</summary>
    [RequiresFilesFact(SideA, SideB, StGameData)]
    public void DungeonMenus_HaveTheStsOptionCounts()
    {
        var ad = Out("dungeons.json")["levels"]!.AsObject();
        var sd = St("dungeons.json")["levels"]!.AsObject();
        var checkedMenus = 0;
        var bad = new List<string>();
        foreach (var (key, lv) in ad)
            foreach (var (mid, m) in lv!["messages"]!.AsObject())
            {
                var s = sd[key]!["messages"]![mid];
                // Skip the ST's continuation records and stale slots (no text before the options).
                if (s is null || s["continuation_of"] is not null || s["options"]!.AsArray().Count < 2
                    || s["lines"]!.AsArray().All(l => ((string)l!).Trim().Length == 0))
                    continue;
                if (s["options"]!.AsArray().Count != m!["options"]!.AsArray().Count)
                    bad.Add($"{key} {mid}: {m.ToJsonString()} / ST {s.ToJsonString()}");
                checkedMenus++;
            }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
        Assert.True(checkedMenus > 30, $"{checkedMenus} menus");
        // DNG1 message 50: one text line, then its two marked choices as options (no game text quoted here).
        var gate = ad["DNG1"]!["messages"]!["50"]!;
        Assert.Equal(2, gate["options"]!.AsArray().Count);
        Assert.Single(gate["lines"]!.AsArray());
    }

    [Fact]
    public void SplitMenu_SplitsMarkedChoices_ElseFallsBackToDigits()
    {
        var (head, marked) = A2GameData.SplitMenu(["A DOOR.", "1-GO ON  2-TURN", "BACK"], 2);
        Assert.Equal(["A DOOR."], head);
        Assert.Equal(["GO ON", "TURN BACK"], marked);
        var (lines, options) = A2GameData.SplitMenu(["A PANEL OF KEYS, THE", "KEYS ARE MARKED 1-9.", "PRESS WHICH KEY?"], 9);
        Assert.Equal(3, lines.Count);
        Assert.Equal(Enumerable.Range(1, 9).Select(i => i.ToString()), options);
    }

    /// <summary>An Apple-only import is a playable II: it loads through the game's loader and a new game starts.</summary>
    [RequiresFilesFact(SideA, SideB)]
    public void AppleOnlyImport_LoadsAndStartsAGame()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-a2-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            var report = new ImportRunner(root, Repo.GameData).Run(ImportRunner.Scan([Repo.Path(SideA), Repo.Path(SideB)]));
            var v = Assert.Single(report.Versions);
            Assert.True(v.Ok, v.Error);
            var manifest = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, ImportRunner.ManifestName)))!;
            Assert.Equal(Profile, (string?)manifest["games"]!["phantasie2"]!["tables"]);
            Assert.Equal(Profile, (string?)manifest["games"]!["phantasie2"]!["art"]!["apple2"]);

            GameDataLocator.UseImport(root);
            var data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2));
            GameText.Source = StringTable.ForGame(GameId.P2);
            Assert.Equal(GameId.P2, data.Game);
            Assert.Equal(10, data.Towns.Count);
            Assert.Equal(80, data.Monsters.Count);
            var s = GameSessionFactory.Create(data, new SeededDosRandomSource(1));
            s.StartNewGame(null);
            s.OpenGuild();
            var rolled = s.RollNewMember(Race.Human, CharacterClass.Fighter);
            Assert.True(s.TryEnrollNewMember(rolled, "GLADE", out var err), err);
            Assert.True(s.TryAddToParty(rolled, out err), err);
            s.LeaveGuild();
            Assert.Equal(GameMode.Town, s.Mode);

            // D2-050: the Filmon ending's line 14 in the Apple's words with its party size (WIN line 3116, PO + 1); a
            // summoned elemental (none on the Apple) is not counted (s170).
            var elemental = new Character { Name = "FIRE ELEMENTAL", Status = CharacterStatus.Ok };
            s.Party.Elemental = elemental;
            s.Party.Members.Add(elemental);
            Phantasie2Campaign.ResolveEnding(s);
            var scene = Assert.IsType<VictoryScene>(s.PendingVictoryScene);
            Assert.Equal("WE ARE ALL DOOMED. THESE 1 BRAVE ADVEN-", scene.ClosingPage[13]);
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>With the ST copy in the same run, the ST's tables win and the Apple adds only its art pack.</summary>
    [RequiresFilesFact(SideA, SideB, "reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk1.stx")]
    public void WithTheSt_TheStTablesWin()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-a2st-").FullName;
        try
        {
            var root = Path.Combine(dir, "import");
            var scan = ImportRunner.Scan([Repo.Path(SideA), Repo.Path(SideB),
                Repo.Path("reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk1.stx"),
                Repo.Path("reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk2.stx")]);
            var report = new ImportRunner(root, Repo.GameData).Run(scan);
            Assert.Equal(2, report.Versions.Count);
            Assert.All(report.Versions, v => Assert.True(v.Ok, v.Error));
            Assert.Equal("p2-st-1.0", report.Versions[0].ProfileId);
            var manifest = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, ImportRunner.ManifestName)))!;
            Assert.Equal("p2-st-1.0", (string?)manifest["games"]!["phantasie2"]!["tables"]);
            var outputs = manifest["games"]!["phantasie2"]!["versions"]![Profile]!["outputs"]!.AsObject();
            // Its string-table files (the Apple's lines, the fallback) go under the ST's (10-importer.md 7.6).
            Assert.DoesNotContain(outputs, o => o.Key.StartsWith("GameData/phantasie2/", StringComparison.Ordinal)
                                                 && o.Key != "GameData/phantasie2/graphics/apple2.json"
                                                 && !o.Key.StartsWith("GameData/phantasie2/strings/", StringComparison.Ordinal));
            Assert.Equal("atari-st", (string?)JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "GameData", "phantasie2", "game.json")))!["platform"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
