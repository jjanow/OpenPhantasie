using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Pipelines.P3Dos;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// Recipe re-runs (10-importer.md 4.4) and the CLI's <c>--check</c> (4.1), on a DOS III import in a temp folder: a
/// version recorded with an older recipe is listed as stale and re-run from its recorded files (an import made before
/// the CGA pack gets it); one whose files are gone is named in the report; <c>Check</c> compares a fresh run with the
/// sha1s the import recorded and changes nothing.
/// </summary>
[Collection(nameof(LocatorCollection))]
public class ImportRefreshTests
{
    private const string P3Dos = "reverse-engineering/dos/original-images/phantasie3/phantasie3-1988-ssi-360k.ima";
    private const string Profile = "p3-dos-1988";

    private static string ManifestPath(string root) => Path.Combine(root, ImportRunner.ManifestName);

    private static JsonObject Manifest(string root) => JsonNode.Parse(File.ReadAllBytes(ManifestPath(root)))!.AsObject();

    private static JsonObject Version(JsonObject manifest) => manifest["games"]!["phantasie3"]!["versions"]![Profile]!.AsObject();

    private static string Import(string dir, string source)
    {
        var root = Path.Combine(dir, "import");
        var report = new ImportRunner(root, Repo.GameData).Run(ImportRunner.Scan([source]));
        Assert.True(report.Committed);
        return root;
    }

    /// <summary>Make the import look like one made by recipe 1 (s158): tables only, no CGA pack, no fallback lines.</summary>
    private static void MakeRecipeOne(string root)
    {
        var manifest = Manifest(root);
        var version = Version(manifest);
        version["recipe"] = 1;
        var outputs = version["outputs"]!.AsObject();
        foreach (var key in outputs.Select(kv => kv.Key).ToList())
            if (key.StartsWith("files/", StringComparison.Ordinal) || key.EndsWith("/" + ImportedStrings.FallbackFile(Profile), StringComparison.Ordinal))
            {
                outputs.Remove(key);
                File.Delete(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
            }
        manifest["games"]!["phantasie3"]!["art"] = new JsonObject();
        File.WriteAllBytes(ManifestPath(root), PyJson.SerializeUtf8(manifest, 1));
    }

    [RequiresFilesFact(P3Dos)]
    public void OlderRecipe_IsStale_AndRefreshReRunsItFromItsFiles()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-refresh-").FullName;
        try
        {
            var root = Import(dir, Repo.Path(P3Dos));
            Assert.Empty(ImportRunner.StaleVersions(root));
            MakeRecipeOne(root);
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "files"), "*.png", SearchOption.AllDirectories));

            var stale = Assert.Single(ImportRunner.StaleVersions(root));
            Assert.Equal((Profile, 1, new P3DosPipeline().RecipeVersion), (stale.ProfileId, stale.Recipe, stale.Current));
            Assert.Equal([Repo.Path(P3Dos)], stale.Sources);

            var report = new ImportRunner(root, Repo.GameData).Refresh();
            Assert.True(report.Committed);
            var v = Assert.Single(report.Versions);
            Assert.True(v.Ok, v.Error);
            Assert.Empty(ImportRunner.StaleVersions(root));
            var manifest = Manifest(root);
            Assert.Equal(new P3DosPipeline().RecipeVersion, (int)Version(manifest)["recipe"]!);
            Assert.Equal(Profile, (string?)manifest["games"]!["phantasie3"]!["art"]!["dos-cga"]);
            Assert.Equal(512, Directory.EnumerateFiles(Path.Combine(root, "files", P3CgaArt.Root.Replace('/', Path.DirectorySeparatorChar)),
                "*.png", SearchOption.AllDirectories).Count());
            Assert.True(File.Exists(Path.Combine(root, "GameData", "phantasie3", "strings", "en.fallback." + Profile + ".json")));
            // Nothing stale: nothing runs.
            var again = new ImportRunner(root, Repo.GameData).Refresh();
            Assert.False(again.Committed);
            Assert.Empty(again.Versions);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [RequiresFilesFact(P3Dos)]
    public void OlderRecipe_FilesGone_TheImportStaysAndTheReportAsks()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-refresh-gone-").FullName;
        try
        {
            var copy = Path.Combine(dir, "p3.ima");
            File.Copy(Repo.Path(P3Dos), copy);
            var root = Import(dir, copy);
            MakeRecipeOne(root);
            File.Delete(copy);
            var before = File.ReadAllBytes(ManifestPath(root));

            var report = new ImportRunner(root, Repo.GameData).Refresh();
            Assert.False(report.Committed);
            Assert.Contains(report.Notes, n => n.Contains("import them again", StringComparison.Ordinal) && n.Contains(copy, StringComparison.Ordinal));
            Assert.Equal(before, File.ReadAllBytes(ManifestPath(root)));
            Assert.Single(ImportRunner.StaleVersions(root));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [RequiresFilesFact(P3Dos)]
    public void Check_SameRunMatches_AChangedRecordIsReported_TheImportIsUntouched()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-check-").FullName;
        try
        {
            var root = Import(dir, Repo.Path(P3Dos));
            var scan = ImportRunner.Scan([Repo.Path(P3Dos)]);
            var same = Assert.Single(new ImportRunner(root, Repo.GameData).Check(scan));
            Assert.True(same.Same, string.Join(", ", same.Changed.Concat(same.Added).Concat(same.Missing)));

            // A recorded sha1 that no longer matches, a recorded file the run no longer writes, one it writes unrecorded.
            var manifest = Manifest(root);
            var outputs = Version(manifest)["outputs"]!.AsObject();
            outputs["GameData/phantasie3/monsters.json"] = "0000000000000000000000000000000000000000";
            outputs["GameData/phantasie3/old.json"] = "1111111111111111111111111111111111111111";
            outputs.Remove("GameData/phantasie3/items.json");
            File.WriteAllBytes(ManifestPath(root), PyJson.SerializeUtf8(manifest, 1));
            var before = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count();

            var diff = Assert.Single(new ImportRunner(root, Repo.GameData).Check(scan));
            Assert.False(diff.Same);
            Assert.Equal(["GameData/phantasie3/monsters.json"], diff.Changed);
            Assert.Equal(["GameData/phantasie3/items.json"], diff.Added);
            Assert.Equal(["GameData/phantasie3/old.json"], diff.Missing);
            Assert.Equal(before, Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count());
            Assert.Equal("0000000000000000000000000000000000000000",
                (string?)Version(Manifest(root))["outputs"]!["GameData/phantasie3/monsters.json"]);

            // A version not in the import yet.
            var empty = Path.Combine(dir, "none");
            var fresh = Assert.Single(new ImportRunner(empty, Repo.GameData).Check(scan));
            Assert.False(fresh.InImport);
            Assert.False(Directory.Exists(empty));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
