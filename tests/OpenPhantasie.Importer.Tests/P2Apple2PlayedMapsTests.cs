using System.Text.Json.Nodes;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Importer.Pipelines.P2Apple2;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// D2-052 (user s209): a played Apple II Phantasie II (the 4am dump: 15 <c>OUTTn</c> stored back with fog lifted) is
/// imported as it is or, when the player says so, with the shipped start fog (<c>apple2-import.json</c>
/// <c>start_fog</c>), so a new game and RESET GAME both start fogged as the manual says.
/// </summary>
[Collection(nameof(LocatorCollection))]
public class P2Apple2PlayedMapsTests
{
    private const string Profile = "p2-apple2";
    private const string SideA = "reverse-engineering/apple2/original-images/phantasie2/4am-crack/phantasie2-4am-side-a.dsk";
    private const string SideB = "reverse-engineering/apple2/original-images/phantasie2/4am-crack/phantasie2-4am-side-b.dsk";
    private const string MemA = "reverse-engineering/apple2/original-images/phantasie2/memorial-set-gog/PHANT2A.DSK";
    private const string MemB = "reverse-engineering/apple2/original-images/phantasie2/memorial-set-gog/PHANT2B.DSK";
    private const string StOverworld = "src/OpenPhantasie.Data/GameData/phantasie2/overworld.json";

    private static A2StValues Values() =>
        A2StValues.Parse(JsonNode.Parse(File.ReadAllBytes(Path.Combine(Repo.GameData, "phantasie2", A2StValues.File)))!);

    private static JsonNode Overworld(string root) =>
        JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "GameData", "phantasie2", "overworld.json")))!;

    private static JsonNode WithoutSize(JsonNode overworld)
    {
        foreach (var (_, region) in overworld["regions"]!.AsObject())
            region!.AsObject().Remove("size");
        return overworld;
    }

    private static SessionSnapshot SaveAndRead(SessionSnapshot s) => SessionCodec.ReadJson(SessionCodec.WriteJson(s));

    [Fact]
    public void ApplyStartFog_FogsAndUnfogsByRegion_KeepsUnfoggableCells()
    {
        var ow = new JsonObject
        {
            ["regions"] = new JsonObject
            {
                ["OUT1.DAT"] = new JsonObject { ["tiles"] = new JsonArray(new JsonArray(7, 127, 220, 255)) },
                ["OUT3.DAT"] = new JsonObject { ["tiles"] = new JsonArray(new JsonArray(7, 127, 100, 255)) },
                ["OUT99.DAT"] = new JsonObject { ["tiles"] = new JsonArray(new JsonArray(7)) },
            },
        };
        var warnings = new List<string>();
        var changed = Values().ApplyStartFog(ow, warnings);

        var regions = ow["regions"]!;
        Assert.Equal([7, 7, 100, 255], regions["OUT1.DAT"]!["tiles"]![0]!.AsArray().Select(t => (int)t!));
        Assert.Equal([127, 127, 220, 255], regions["OUT3.DAT"]!["tiles"]![0]!.AsArray().Select(t => (int)t!));
        Assert.Equal(7, (int)regions["OUT99.DAT"]!["tiles"]![0]![0]!); // not listed: as played
        Assert.Equal(4, changed);
        Assert.Contains(warnings, w => w.Contains("OUT99.DAT", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.Contains("OUT2.DAT", StringComparison.Ordinal)); // listed, not in this data
    }

    /// <summary>The list is the ST's start fog (MAPS.INT, in the shipped ST GameData): every region all open or all fogged.</summary>
    [RequiresFilesFact(StOverworld)]
    public void StartFog_IsMapsIntsPatternPerRegion()
    {
        var doc = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Repo.GameData, "phantasie2", A2StValues.File)))!["start_fog"]!;
        var open = doc["open"]!.AsArray().Select(n => (string)n!).ToList();
        var fogged = doc["fogged"]!.AsArray().Select(n => (string)n!).ToList();
        Assert.Equal(["OUT1.DAT", "OUT2.DAT"], open);
        var st = JsonNode.Parse(File.ReadAllBytes(Repo.Path(StOverworld)))!["regions"]!.AsObject();
        Assert.Equal(st.Select(kv => kv.Key).Order(StringComparer.Ordinal), open.Concat(fogged).Order(StringComparer.Ordinal));
        foreach (var (key, region) in st)
        {
            var want = fogged.Contains(key);
            Assert.All(region!["tiles"]!.AsArray().SelectMany(r => r!.AsArray()), t => Assert.Equal(want, (int)t! >= 120));
        }
    }

    [RequiresFilesFact(SideA, SideB, MemA, MemB)]
    public void APlayedCopy_IsAsked_AndRestoredItImportsAsTheUnplayedDisk_AndResetsFogged()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p2a2-played-maps-").FullName;
        try
        {
            var played = ImportRunner.Scan([Repo.Path(SideA), Repo.Path(SideB)]);
            var fresh = ImportRunner.Scan([Repo.Path(MemA), Repo.Path(MemB)]);
            var d = played.Detections.Single(x => x.Profile.Id == Profile);
            Assert.True(ImportRunner.CanRestoreMaps(d));
            Assert.False(ImportRunner.MapsHoldFights(d));
            Assert.Equal([Profile], ImportRunner.RestorableMaps(played).Select(x => x.Profile.Id));
            Assert.Empty(ImportRunner.RestorableMaps(fresh));

            var freshRoot = Path.Combine(dir, "fresh");
            Assert.True(new ImportRunner(freshRoot, Repo.GameData).Run(fresh).Committed);

            var keptRoot = Path.Combine(dir, "kept");
            var kept = new ImportRunner(keptRoot, Repo.GameData).Run(played);
            var keptWarning = kept.Versions.Single().Warnings[0];
            Assert.Contains("fog is already lifted", keptWarning);
            Assert.DoesNotContain("fixed fights", keptWarning);
            Assert.False(JsonNode.DeepEquals(Overworld(freshRoot), Overworld(keptRoot)));
            Assert.Empty(ImportRunner.RestoredMaps(keptRoot));

            var restoredRoot = Path.Combine(dir, "restored");
            var restored = new ImportRunner(restoredRoot, Repo.GameData).Run(played, restoreMaps: new HashSet<string> { Profile });
            var restoredWarning = restored.Versions.Single().Warnings[0];
            Assert.Contains("were restored as on a fresh copy: the fog is back.", restoredWarning);
            Assert.DoesNotContain("fixed fights", restoredWarning);
            // Play rewrites only the fog (FORMAT.md 3.6): restored, the played copy's maps are the unplayed disk's. Only
            // each region's source file length ("size": 756 B once stored back, 768 B as shipped) still tells them apart.
            Assert.True(JsonNode.DeepEquals(WithoutSize(Overworld(freshRoot)), WithoutSize(Overworld(restoredRoot))));
            Assert.False(JsonNode.DeepEquals(WithoutSize(Overworld(freshRoot)), WithoutSize(Overworld(keptRoot))));
            Assert.Equal([Profile], ImportRunner.RestoredMaps(restoredRoot));

            // A new game and RESET GAME on the restored import start with the start fog; on the kept one they do not.
            foreach (var (root, fogged) in new[] { (restoredRoot, true), (keptRoot, false) })
            {
                GameDataLocator.UseImport(root);
                var data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2));
                GameText.Source = StringTable.ForGame(GameId.P2);
                var s = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
                s.StartNewGame(GameOptions.ForPreset(GameId.P2));
                var start = SaveAndRead(s.CaptureSnapshot());
                var save = SaveAndRead(s.CaptureSnapshot());
                foreach (var tiles in save.Wilderness!.RegionTiles!.Values)
                    for (var i = 0; i < tiles.Length; i++)
                        tiles[i] = (byte)OverworldRegion.Normalize(tiles[i]); // the whole world explored
                foreach (var mode in new[] { ResetMode.Purge, ResetMode.Keep })
                {
                    var world = SaveAndRead(GameReset.ResetSession(save, data, mode, dosFog: false)).Wilderness!.RegionTiles!;
                    Assert.Equal(17, world.Count);
                    foreach (var (id, tiles) in start.Wilderness!.RegionTiles!)
                        Assert.Equal(tiles, world[id]);
                    // MAPS.INT's pattern: regions 1 and 2 have no fog, the others are fogged in every cell.
                    Assert.Equal(fogged, world.All(r => r.Value.All(b => b >= OverworldRegion.FogOffset == (r.Key is not (1 or 2)))));
                }
            }
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }
}
