using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P1Dos;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// Golden tests of the I DOS (SSI 1987) pipeline: on the reference image, every table equals the committed GameData
/// (JSON-equal, and byte-equal: the importer writes them as the Python scripts did) and every CGA picture equals the
/// committed extract pixel for pixel.
/// </summary>
public class P1DosPipelineTests
{
    [RequiresFilesFact("tools/scripts/extract_maps.py", "src/OpenPhantasie.Data/GameData/phantasie1/dungeons.json")] // s163: the shipped file is not in the public repo
    public void DungeonsNote_IsTheExtractorsWording_InTheShippedFileAndThePort()
    {
        // User s159: I's dungeons.json _note takes what extract_maps.py writes (it kept an older script note before s160).
        var script = File.ReadAllText(Repo.Path("tools/scripts/extract_maps.py"));
        var dn = script[script.IndexOf("dn = {", StringComparison.Ordinal)..];
        var note = System.Text.RegularExpressions.Regex.Match(dn, "\"_note\": \"([^\"]*)\"").Groups[1].Value;
        Assert.StartsWith("raw feature bytes; [C] grid", note);
        Assert.Equal(note, MapTables.DungeonNote);
        var shipped = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Repo.GameData, "phantasie1", "dungeons.json")))!;
        Assert.Equal(note, (string?)shipped["_note"]);
    }

    private const string Image = "reverse-engineering/dos/original-images/phantasie1/phantasie-1987-ssi-360k.ima";
    private const string Profile = "p1-dos-1987";
    private const string GameData = "src/OpenPhantasie.Data/GameData/phantasie1/";
    private const string CgaRoot = "reverse-engineering/dos/extracted/";

    private static readonly string[] TableFiles =
    [
        "dungeons.json", "elementals.json", "item-descriptions.json", "items.json", "messages.json", "monsters.json",
        "names.json", "overworld.json", "scrolls.json", "spells.json", "towns.json",
    ];

    public static readonly TheoryData<string> Tables = new(TableFiles);

    private static readonly string[] Pictures =
    [
        .. new[]
        {
            "class0", "class1", "class2", "class3", "town", "dungeon", "class6-path", "class7-bridge", "class8",
            "class9", "water", "pool", "party", "inn", "realm",
        }.Select((n, i) => $"tiles/overworld/pat-{i:00}-{n}.png"),
        "tiles/overworld/fog.png",
        .. new[] { "wall", "floor", "event", "lock-v", "lock-h", "exit", "stairs", "party" }
            .Select((n, i) => $"tiles/dungeon/glyph-{0xe6 + i:x2}-{n}.png"),
        "ui/town.png", "ui/defeat-temple.png", "ui/defeat-demon.png", "ui/defeat-ghost.png",
        .. Enumerable.Range(0, 16).Select(r => $"ui/defeat-figures/{r:00}.png"),
    ];

    public static readonly TheoryData<string> PictureData = new(Pictures);

    private static IReadOnlyDictionary<string, byte[]> Outputs => PipelineHarness.Run(Profile, Image).Outputs;

    [RequiresFilesFact(Image)]
    public void WritesExactlyTheExpectedFiles()
    {
        var expected = TableFiles.Select(t => "GameData/phantasie1/" + t)
            .Concat(Pictures.Select(p => "files/" + CgaRoot + p))
            .Append("files/" + CgaRoot + "tiles/tiles.json")
            .OrderBy(k => k, StringComparer.Ordinal);
        Assert.Equal(expected, Outputs.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Empty(PipelineHarness.Run(Profile, Image).Warnings);
    }

    [RequiresFilesTheory(Image)]
    [MemberData(nameof(Tables))]
    public void Table_EqualsCommittedGameData(string file)
    {
        var bytes = Outputs["GameData/phantasie1/" + file];
        Golden.JsonEqualsFile(bytes, GameData + file);
        // Formatting too (key order, indent, ensure_ascii per script): every table is byte-identical.
        Assert.True(Golden.BytesEqualFile(bytes, GameData + file), $"{file}: JSON-equal but not byte-identical");
    }

    [RequiresFilesFact(Image)]
    public void TilesJson_EqualsCommittedExtract()
    {
        var bytes = Outputs["files/" + CgaRoot + "tiles/tiles.json"];
        Golden.JsonEqualsFile(bytes, CgaRoot + "tiles/tiles.json");
        Assert.True(Golden.BytesEqualFile(bytes, CgaRoot + "tiles/tiles.json"));
    }

    [RequiresFilesTheory(Image)]
    [MemberData(nameof(PictureData))]
    public void Picture_EqualsCommittedExtract(string path)
    {
        var png = Outputs["files/" + CgaRoot + path];
        Golden.PngEqualsFile(png, CgaRoot + path);
        // Pillow saved these as RGBA; so does the importer.
        Assert.Equal(6, png[25]);
    }

    [RequiresFilesFact(Image)]
    public void Spells_NamesComeFromTheCopy_NotTheOverlay()
    {
        // The public overlay has the original names removed: the names must come from the copy's names.json spells.
        var overlay = (JsonObject)JsonNode.Parse(File.ReadAllBytes(Repo.Path(GameData + "spells.json")))!;
        foreach (var s in (JsonArray)overlay["spells"]!)
            ((JsonObject)s!).Remove("name");
        var names = (JsonObject)JsonNode.Parse(Outputs["GameData/phantasie1/names.json"])!;
        var spells = Derived.Spells(overlay, ComTables.SpellNames(names))!;
        var bytes = PyJson.SerializeUtf8(spells, 2, ensureAscii: true);
        Assert.True(Golden.BytesEqualFile(bytes, GameData + "spells.json"));
    }

    [RequiresFilesFact(Image)]
    public void Pictures_AreNotByteIdentical_OnlyPixelEqual()
    {
        // Documents the state: Pillow's zlib stream differs from .NET's, so no PNG is byte-identical; pixels are.
        var same = Pictures.Count(p => Golden.BytesEqualFile(Outputs["files/" + CgaRoot + p], CgaRoot + p));
        Assert.Equal(0, same);
    }

    [RequiresFilesFact(Image)]
    public void Towns_FromTwnsInt_EqualTheDiskTwnsDat()
    {
        // 10-importer.md 3.3: TWNS.INT 2445..5936 is the pristine TWNS.DAT; on the unplayed reference image both agree.
        var files = PipelineHarness.Run(Profile, Image).Detection.Files!;
        var twnsInt = files.Read("TWNS.INT");
        Assert.True(twnsInt.AsSpan(2445, 3492).SequenceEqual(files.Read("TWNS.DAT")));
    }
}
