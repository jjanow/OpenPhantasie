using System.Text.Json.Nodes;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// The whole flow on a temp import folder: scan, import, <c>import.json</c>, the swap, re-import, and the game loading
/// from the import (<see cref="GameDataLocator"/>). The locator is static: these tests run one at a time and reset it.
/// </summary>
[Collection(nameof(LocatorCollection))]
public class ImportFlowTests
{
    private static string Temp() => Directory.CreateTempSubdirectory("phantasie-import-flow-").FullName;

    [RequiresFilesFact("reverse-engineering/amiga/original-images/phantasie3/phantasie3-v1.0-1987-ssi.adf")]
    public void ImportsArt_WritesManifest_AndPacksResolveIntoTheImport()
    {
        var dir = Temp();
        try
        {
            var root = Path.Combine(dir, "import");
            var scan = ImportRunner.Scan([Repo.Path("reverse-engineering/amiga/original-images/phantasie3/phantasie3-v1.0-1987-ssi.adf")]);
            var report = new ImportRunner(root, Repo.GameData).Run(scan);
            Assert.True(report.Committed);
            var v = Assert.Single(report.Versions);
            Assert.True(v.Ok, v.Error);
            Assert.False(Directory.Exists(root + ".tmp"));

            var manifest = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, ImportRunner.ManifestName)))!;
            Assert.Equal("p3-amiga-1.0", (string?)manifest["games"]!["phantasie3"]!["art"]!["amiga"]);
            Assert.Null(manifest["games"]!["phantasie3"]!["tables"]); // art only: III's tables need the DOS copy
            var outputs = manifest["games"]!["phantasie3"]!["versions"]!["p3-amiga-1.0"]!["outputs"]!.AsObject();
            Assert.Equal(v.FilesWritten, outputs.Count);

            GameDataLocator.UseImport(root);
            Assert.False(GameDataLocator.IsImported(GameId.P3));
            Assert.Equal("p3-amiga-1.0", GameDataLocator.ImportedArt(GameId.P3)["amiga"]);
            var catalog = GraphicsPackCatalog.Load(Path.Combine(Repo.GameData, "phantasie3"));
            var pack = Assert.Single(catalog.Available);
            Assert.StartsWith(Path.Combine(root, "files"), pack.RootDirectory);

            // Re-import: same outputs, the old ones replaced, still one version.
            var again = new ImportRunner(root, Repo.GameData).Run(scan);
            Assert.True(again.Committed);
            var manifest2 = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, ImportRunner.ManifestName)))!;
            Assert.Single(manifest2["games"]!["phantasie3"]!["versions"]!.AsObject());
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ImportedTables_AreTheGameFolder_AndShippedOwnFilesAreSynced()
    {
        var dir = Temp();
        try
        {
            var root = Path.Combine(dir, "import");
            var game = Path.Combine(root, "GameData", "phantasie2");
            Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, "items.json"), "{}");
            var manifest = new JsonObject
            {
                ["games"] = new JsonObject
                {
                    ["phantasie2"] = new JsonObject
                    {
                        ["tables"] = "p2-st-1.0",
                        ["versions"] = new JsonObject
                        {
                            ["p2-st-1.0"] = new JsonObject
                            {
                                ["outputs"] = new JsonObject { ["GameData/phantasie2/items.json"] = "x" },
                            },
                        },
                    },
                },
            };
            File.WriteAllText(Path.Combine(root, "import.json"), PyJson.Serialize(manifest));

            GameDataLocator.UseImport(root);
            Assert.True(GameDataLocator.IsImported(GameId.P2));
            Assert.Equal(game, GameDataRepository.DefaultPathFor(GameId.P2));
            Assert.False(GameDataLocator.IsImported(GameId.P1));
            Assert.Equal(Path.Combine(GameDataLocator.ShippedRoot, "phantasie1"), GameDataRepository.DefaultPathFor(GameId.P1));
            // Our own files came over from the shipped folder; the imported one was left alone.
            Assert.True(File.Exists(Path.Combine(game, "rules.json")));
            Assert.Equal("{}", File.ReadAllText(Path.Combine(game, "items.json")));
            Assert.Equal(GameDataLocator.BuiltInFallback, GameDataLocator.IsAvailable(GameId.P3));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ImportedStringFiles_GoOverTheShippedTables_IncludingAnExtendedOne()
    {
        var dir = Temp();
        try
        {
            var root = Path.Combine(dir, "import");
            void Write(string table, string profile, string id, string text)
            {
                var path = Path.Combine(root, "GameData", table, "strings", $"en.{profile}.json");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, PyJson.Serialize(new JsonObject
                {
                    ["strings"] = new JsonObject { [id] = text },
                }));
            }
            Write("shared", "p1-dos-1987", "inventory.refuse.unusable", "IMPORTED SHARED");
            Write("phantasie1", "p1-dos-1987", "health.dead", "IMPORTED I");
            Write("phantasie1", "p1-amiga-1.0", "health.sleep", "IMPORTED I AMIGA");
            // en.sources.json in an import folder is never a layer.
            Write("phantasie1", "sources", "health.okay", "NOT A LAYER");

            GameDataLocator.UseImport(root);
            Assert.Equal(2, GameDataLocator.ImportedStringFiles("phantasie1").Count);
            var p1 = StringTable.ForGame(GameId.P1);
            Assert.Equal("IMPORTED SHARED", p1.Get("inventory.refuse.unusable"));
            Assert.Equal("IMPORTED I", p1.Get("health.dead"));
            Assert.Equal("IMPORTED I AMIGA", p1.Get("health.sleep"));
            Assert.NotEqual("NOT A LAYER", p1.Get("health.okay"));
            // II's table extends I's: I's imported lines reach it too.
            Assert.Equal("IMPORTED I", StringTable.ForGame(GameId.P2).Get("health.dead"));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
        Assert.Empty(GameDataLocator.ImportedStringFiles("phantasie1"));
    }

    [Fact]
    public void NothingImportable_LeavesNoImportFolder()
    {
        var dir = Temp();
        try
        {
            File.WriteAllText(Path.Combine(dir, "readme.txt"), "not a game");
            var root = Path.Combine(dir, "import");
            var report = new ImportRunner(root, Repo.GameData ?? dir).Run(ImportRunner.Scan([dir]));
            Assert.False(report.Committed);
            Assert.Empty(report.Versions);
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Profiles_SupportedOnesHavePipelines()
    {
        foreach (var p in Profiles.VersionProfiles.All.Where(p => p.IsSupported && p.Game != "shared"))
            Assert.True(Pipelines.PipelineRegistry.For(p.Id) is { } pl && pl is not Pipelines.PlannedPipeline, $"{p.Id}: no pipeline");
        Assert.Equal("supported", Profiles.VersionProfiles.Get("p2-apple2").Status);
    }
}

[CollectionDefinition(nameof(LocatorCollection), DisableParallelization = true)]
public sealed class LocatorCollection;
