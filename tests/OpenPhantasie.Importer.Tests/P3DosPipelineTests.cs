using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P3Dos;
using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// Golden tests for <see cref="P3DosPipeline"/> (Phantasie III DOS): every output JSON-equal to the committed
/// <c>GameData/phantasie3</c> file; the extract stage JSON-equal to <c>reverse-engineering/dos/phantasie3/extracted/</c>.
/// Nine outputs are also byte-identical to the committed files; rule-tables.json is JSON-equal only, because the blocks
/// added to it by hand (creation_age, armory_restock …) were written with inline number lists, not json.dumps(indent=1).
/// </summary>
public class P3DosPipelineTests
{
    private const string Image = "reverse-engineering/dos/original-images/phantasie3/phantasie3-1988-ssi-360k.ima";
    private const string WizardWorks =
        "reverse-engineering/dos/original-images/phantasie3/wizardworks-1991/phantasie3-1991-wizardworks-360k.ima";
    private const string Extracted = "reverse-engineering/dos/phantasie3/extracted/";

    private static readonly string[] Files =
    [
        "dungeons.json", "elementals.json", "items.json", "monsters.json", "names.json", "overworld.json",
        "rule-tables.json", "scrolls.json", "spells.json", "towns.json",
    ];

    public static readonly TheoryData<string> Outputs = new(Files);

    private static PipelineHarness.Result Run() => PipelineHarness.Run("p3-dos-1988", Image);

    [RequiresFilesFact(Image)]
    public void WritesExactlyTheTenTables_AndTheCgaPictures()
    {
        var keys = Run().Outputs.Keys.Where(k => k.StartsWith("GameData/", StringComparison.Ordinal))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(Files.Select(o => "GameData/phantasie3/" + o).OrderBy(k => k, StringComparer.Ordinal), keys);
        var art = Run().Outputs.Keys.Where(k => !k.StartsWith("GameData/", StringComparison.Ordinal)).ToList();
        Assert.All(art, k => Assert.StartsWith($"files/{P3CgaArt.Root}/", k));
        Assert.Equal(512, art.Count);
        Assert.Empty(Run().Warnings);
    }

    // ---- the CGA picture pack (s160) -------------------------------------------------------------------------

    private const string CgaRoot = "reverse-engineering/dos/phantasie3/extracted/graphics";

    /// <summary>Committed files under the CGA root the pipeline does not write, with the reason.</summary>
    private static readonly Dictionary<string, string> CgaNotProduced = new()
    {
        ["extraction-index.json"] = "the Python extractor's review index (sources and offsets per file); the pack doesn't read it",
    };

    [RequiresFilesFact(Image, CgaRoot)]
    public void CgaPictures_PixelEqualToThePythonExtract()
    {
        var r = Run();
        var committed = Directory.EnumerateFiles(Repo.Path(CgaRoot), "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Repo.Path(CgaRoot), f).Replace('\\', '/'))
            .Where(f => !CgaNotProduced.ContainsKey(f)).OrderBy(f => f, StringComparer.Ordinal).ToList();
        foreach (var rel in committed)
        {
            Assert.True(r.Outputs.TryGetValue($"files/{CgaRoot}/{rel}", out var png), $"{rel} not produced");
            Golden.PngEqualsFile(png!, $"{CgaRoot}/{rel}");
        }
        Assert.Equal(committed.Count, r.Outputs.Keys.Count(k => k.StartsWith($"files/{CgaRoot}/", StringComparison.Ordinal)));
    }

    [RequiresFilesFact(Image)]
    public void CgaPack_EveryPictureTheManifestNamesIsWritten()
    {
        // Every fixed picture and every picture-set file the Client can ask for (monsters 0..79 idle / attacking,
        // the 15 party races, tiles 0..249, dungeon glyphs 46..52, the body pieces) exists in the import.
        var outputs = Run().Outputs.Keys.Where(k => k.StartsWith($"files/{CgaRoot}/", StringComparison.Ordinal))
            .Select(k => k[$"files/{CgaRoot}/".Length..]).ToHashSet(StringComparer.Ordinal);
        var pack = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Repo.GameData, "phantasie3/graphics/dos-cga.json")))!;
        foreach (var (_, path) in pack["pictures"]!.AsObject())
            Assert.Contains((string)path!, outputs);
        foreach (var req in pack["requires"]!.AsArray())
            Assert.Contains(outputs, o => o == (string)req! || o.StartsWith((string)req! + "/", StringComparison.Ordinal));
        for (var m = 0; m < 80; m++)
            foreach (var kind in new[] { "idle", "attacking" })
                Assert.Contains(outputs, o => o.StartsWith($"monsters/{kind}/{m:00}-", StringComparison.Ordinal));
        for (var t = 0; t < 250; t++)
            Assert.Contains($"overworld/tiles/{t:000}.png", outputs);
        for (var g = 46; g <= 52; g++)
            Assert.Contains($"dungeon/glyph-{g}.png", outputs);
        for (var slot = 0; slot < 36; slot++)
            Assert.Contains($"body/{slot}.png", outputs);
        for (var race = 0; race < 16; race++)
            Assert.Contains(outputs, o => o.StartsWith($"portraits/idle/{race:00}-", StringComparison.Ordinal));
    }

    [Fact]
    public void Unpack_ColumnOrder_LiteralsRunsAndWordRuns()
    {
        // Unpacker 2: literal 3 bytes, a run of 2 (0xFE), a word-count run (0, 15995) = 16,000 bytes, column by column.
        byte[] packed = [2, 3, 0x11, 0x22, 0x33, 0xFE, 0x44, 0, 0x7B, 0x3E, 0x55];
        var (screen, end) = P3CgaArt.Unpack(packed, 0);
        Assert.Equal(packed.Length, end);
        Assert.Equal(16000, screen.Length);
        // The first column: rows 0..4 = 11 22 33 44 44, then 55 down the rest of it and every other column.
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x44, 0x55 },
            new[] { screen[0], screen[80], screen[160], screen[240], screen[320], screen[400] });
        Assert.Equal(0x55, screen[1]);
        Assert.Equal(0x55, screen[15999]);
    }

    [Fact]
    public void Cga_ClearZero_LeavesColour0Transparent()
    {
        // s169: the flash (root 0x1537) is an OR blit: colour 0 never paints; the rest is palette 0, opaque.
        byte[] row = [0b00_01_10_11];
        var plain = P3CgaArt.Cga(row, 4, 1);
        var flash = P3CgaArt.Cga(row, 4, 1, clearZero: true);
        Assert.Equal(new byte[] { 0, 0, 0, 255, 0x55, 0xFF, 0x55, 255 }, plain.Pixels[..8]);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0x55, 0xFF, 0x55, 255, 0xFF, 0x55, 0x55, 255, 0xFF, 0xFF, 0x55, 255 },
            flash.Pixels);
    }

    [Fact]
    public void Unpack_RowOrder_FillsRowByRow()
    {
        byte[] packed = [1, 2, 0x11, 0x22, 0, 0x7E, 0x3E, 0x00];
        var (screen, _) = P3CgaArt.Unpack(packed, 0);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x00 }, screen[..3]);
    }

    [Theory]
    [InlineData(0, 0, false)]          // Wild Dog
    [InlineData(27, 27 * 768, false)]  // Troll Baby: index 27 (the Forest Elf's picture)
    [InlineData(28, 27 * 768, false)]  // Forest Elf: 28 - 1
    [InlineData(41, 40 * 768, false)]  // Cobra: 41 - 1 = 40, the last normal picture
    [InlineData(42, 0x7B00, true)]     // Dragon: the first giant
    [InlineData(47, 0x7B00 + 5 * 2880, true)] // Lord Wood: the last giant
    [InlineData(48, 15 * 768, false)]  // Mud Elemental: past the giants -> picture 15 (the Skeleton's)
    [InlineData(65, 15 * 768, false)]  // Wolf: the same
    public void MonsterPicture_FollowsRoot0x10b8b(int byte27, int offset, bool giant) =>
        Assert.Equal((offset, giant), P3CgaArt.MonsterPicture(byte27));

    [RequiresFilesFact(Image)]
    public void D3_067_WolfMudElementalTrollBaby_BorrowAnAnalogousCreaturesPicture()
    {
        // User s161: as the Amiga pack (s126): Wolf = Wild Dog, Mud Elemental = the party elemental, Troll Baby = the
        // party Troll (DOS draws the Skeleton / the Forest Elf).
        var o = Run().Outputs;
        string F(string rel) => $"files/{CgaRoot}/{rel}";
        bool Same(string a, string b) => Png.Decode(o[F(a)]).PixelEquals(Png.Decode(o[F(b)]));
        foreach (var kind in new[] { "idle", "attacking" })
        {
            Assert.True(Same($"monsters/{kind}/07-wild-dog.png", $"monsters/{kind}/31-wolf.png"));
            Assert.True(Same($"portraits/{kind}/elemental.png", $"monsters/{kind}/53-mud-elemental.png"));
            Assert.True(Same($"portraits/{kind}/09-troll.png", $"monsters/{kind}/19-troll-baby.png"));
            Assert.False(Same($"monsters/{kind}/00-skeleton.png", $"monsters/{kind}/31-wolf.png"));
        }
    }

    [RequiresFilesFact(Image)]
    public void WithoutThePictureFiles_TablesImportAndAWarningSaysWhy()
    {
        // A folder holding only the table files (as a data disk lacks PICS.CMP / SHAPES.CGA).
        var dir = Directory.CreateTempSubdirectory("phantasie-p3-nopics-").FullName;
        try
        {
            var full = PipelineHarness.Run("p3-dos-1988", Image);
            foreach (var f in full.Detection.Files!.All())
                if (!f.Name.EndsWith(".CGA", StringComparison.OrdinalIgnoreCase) && !f.Name.EndsWith(".CMP", StringComparison.OrdinalIgnoreCase))
                    File.WriteAllBytes(Path.Combine(dir, f.Name), f.Data);
            var scan = ImportRunner.Scan([dir]);
            var root = Path.Combine(dir, "import");
            var report = new ImportRunner(root, Repo.GameData).Run(scan);
            var v = Assert.Single(report.Versions);
            Assert.True(v.Ok, v.Error);
            Assert.Contains(v.Warnings, w => w.Contains("TOWN.CMP not found", StringComparison.Ordinal));
            Assert.False(Directory.Exists(Path.Combine(root, "files")));
            var manifest = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, ImportRunner.ManifestName)))!;
            Assert.Equal("p3-dos-1988", (string?)manifest["games"]!["phantasie3"]!["tables"]);
            Assert.Empty(manifest["games"]!["phantasie3"]!["art"]!.AsObject()); // no pack recorded without pictures
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [RequiresFilesTheory(Image)]
    [MemberData(nameof(Outputs))]
    public void OutputEqualsCommittedGameData(string file)
    {
        var bytes = Run().Outputs["GameData/phantasie3/" + file];
        Golden.JsonEqualsFile(bytes, "src/OpenPhantasie.Data/GameData/phantasie3/" + file);
        if (file != "rule-tables.json")
            Assert.True(Golden.BytesEqualFile(bytes, "src/OpenPhantasie.Data/GameData/phantasie3/" + file),
                $"{file}: JSON-equal but not byte-identical");
    }

    [RequiresFilesFact(Image, Extracted + "items.json")]
    public void ExtractStageEqualsPythonExtracts()
    {
        var x = new P3Extract(Run().Detection.Files!);
        var stage = new Dictionary<string, JsonObject>
        {
            ["strings.json"] = x.StringsJson(), ["names.json"] = x.Names(), ["items.json"] = x.Items(),
            ["spells.json"] = x.Spells(), ["monsters.json"] = x.Monsters(), ["dungeons.json"] = x.Dungeons(),
            ["rule-tables.json"] = x.RuleTables(), ["scrolls.json"] = x.Scrolls(), ["towns.json"] = x.Towns(),
            ["world.json"] = x.World(),
        };
        foreach (var (file, node) in stage)
            Golden.JsonEqualsFile(Formats.PyJson.SerializeUtf8(node, 1), Extracted + file);
    }

    [RequiresFilesFact(WizardWorks)]
    public void WizardWorks1991IsDetectedAsTheSameBuildAndImportsTheSame()
    {
        var ww = PipelineHarness.Run("p3-dos-1988", WizardWorks);
        Assert.Empty(ww.Detection.PlayedFiles);
        if (!Repo.Has(Image))
            return;
        var reference = Run();
        Assert.Equal(reference.Outputs.Keys.Order(), ww.Outputs.Keys.Order());
        foreach (var (key, bytes) in reference.Outputs)
            Assert.True(bytes.AsSpan().SequenceEqual(ww.Outputs[key]), $"{key} differs between 1988 and WizardWorks 1991");
    }

    [RequiresFilesFact("src/OpenPhantasie.Data/GameData/phantasie1/spells.json", Extracted + "spells.json")]
    public void SameNameAsIEqualsTheBuildsNameMatch()
    {
        // build_p3_gamedata.py matched III spells to I's by name (spaces, then also dots, ignored); the port keeps
        // that result as a fixed set so the import never reads I's names.
        var p1 = JsonNode.Parse(File.ReadAllBytes(Repo.Path("src/OpenPhantasie.Data/GameData/phantasie1/spells.json")))!["spells"]!
            .AsArray().Select((s, i) => (Key: ((string)s!["name"]!).Replace(" ", "").ToUpperInvariant(), Index: i))
            .GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Last().Index);
        var p3 = JsonNode.Parse(File.ReadAllBytes(Repo.Path(Extracted + "spells.json")))!["spells"]!.AsArray();
        var matched = new HashSet<int>();
        foreach (var s in p3)
        {
            var n = (int)s!["number"]!;
            var name = ((string)s["name"]!).Replace(" ", "").ToUpperInvariant();
            if (p1.TryGetValue(name.Replace(".", ""), out var i) || p1.TryGetValue(name, out i))
            {
                Assert.Equal(n - 1, i);
                matched.Add(n);
            }
        }
        Assert.True(matched.SetEquals(P3Build.SameNameAsI), string.Join(",", matched.Order()));
    }
}
