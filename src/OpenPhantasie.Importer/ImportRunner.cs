using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer;

/// <summary>What a scan found: the containers, per-version detections, and notes (unreadable files and such).</summary>
public sealed class ScanResult
{
    public required IReadOnlyList<string> Paths { get; init; }
    public required IReadOnlyList<Container> Containers { get; init; }
    public required IReadOnlyList<Detection> Detections { get; init; }
    public required IReadOnlyList<string> Notes { get; init; }

    /// <summary>Detections that will be imported (Ready, with a pipeline that isn't a planned stub).</summary>
    public IEnumerable<Detection> Importable => Detections.Where(d =>
        d.Status == DetectionStatus.Ready && PipelineRegistry.For(d.Profile.Id) is { } p && p is not PlannedPipeline);
}

/// <summary>The result of one import run, per version.</summary>
public sealed class ImportReport
{
    public List<VersionResult> Versions { get; } = [];
    /// <summary>Versions found but not imported (recognised only, unknown builds, missing disks, planned).</summary>
    public List<string> Skipped { get; } = [];
    public List<string> Notes { get; } = [];
    public bool Committed { get; set; }

    public sealed record VersionResult(string ProfileId, string Title, bool Ok, int FilesWritten,
        IReadOnlyList<string> Warnings, string? Error);
}

internal sealed class ImportSession
{
    public required string ShippedGameDataRoot { get; init; }
    public required string StagingRoot { get; init; }
    public required IReadOnlyList<Detection> Detections { get; init; }
    public IProgress<string>? Progress { get; init; }
    public CancellationToken Cancel { get; init; }

    public byte[]? ReadStaged(string key)
    {
        var path = Path.Combine(StagingRoot, key.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}

/// <summary>
/// The import flow (10-importer.md 4): scan the player's paths, detect versions by content, run each version's
/// pipeline into <c>import.tmp/</c> (a copy of the current import, so earlier versions' outputs stay), write
/// <c>import.json</c>, then swap <c>import.tmp/</c> in for <c>import/</c>. A failed pipeline leaves no output; a failed
/// run leaves the old import working.
/// <para>Layout of the import folder: <c>import.json</c>; <c>GameData/&lt;game&gt;/…</c> (tables in today's
/// schema); <c>files/&lt;repo-relative path&gt;</c> (art roots and the raw files the audio reads, at the same relative
/// paths the graphics packs name).</para>
/// </summary>
public sealed class ImportRunner
{
    public const int ImporterVersion = 1;
    public const string ManifestName = "import.json";

    public ImportRunner(string importRoot, string shippedGameDataRoot)
    {
        ImportRoot = Path.GetFullPath(importRoot);
        ShippedGameDataRoot = shippedGameDataRoot;
    }

    public string ImportRoot { get; }
    public string ShippedGameDataRoot { get; }

    public static ScanResult Scan(IReadOnlyList<string> paths, CancellationToken cancel = default)
    {
        var notes = new List<string>();
        var containers = SourceScanner.Scan(paths, notes, cancel);
        var detections = Detector.Detect(containers);
        return new ScanResult { Paths = paths, Containers = containers, Detections = detections, Notes = notes };
    }

    public ImportReport Run(ScanResult scan, IProgress<string>? progress = null, CancellationToken cancel = default)
    {
        var report = new ImportReport();
        report.Notes.AddRange(scan.Notes);
        foreach (var d in scan.Detections)
        {
            if (d.Status == DetectionStatus.Ready && PipelineRegistry.For(d.Profile.Id) is { } p && p is not PlannedPipeline)
                continue;
            report.Skipped.Add(d.Message);
        }
        var work = Order(scan.Importable.ToList());
        if (work.Count == 0)
            return report;

        var staging = ImportRoot + ".tmp";
        if (Directory.Exists(staging))
            Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);
        if (Directory.Exists(ImportRoot))
            CopyTree(ImportRoot, staging);

        var manifest = LoadManifest(Path.Combine(staging, ManifestName));
        var session = new ImportSession
        {
            ShippedGameDataRoot = ShippedGameDataRoot, StagingRoot = staging, Detections = scan.Detections,
            Progress = progress, Cancel = cancel,
        };
        foreach (var d in work)
        {
            cancel.ThrowIfCancellationRequested();
            var pipeline = PipelineRegistry.For(d.Profile.Id)!;
            var ctx = new ImportContext(session, d);
            progress?.Report($"{d.Profile.Title}: importing");
            try
            {
                pipeline.Run(ctx);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                report.Versions.Add(new(d.Profile.Id, d.Profile.Title, false, 0, ctx.Warnings, e.Message));
                continue;
            }
            if (d.PlayedFiles.Count > 0)
                ctx.Warnings.Insert(0, PlayedWarning(d));
            // A re-import of the same version replaces its earlier outputs.
            if (manifest["games"]?[d.Profile.Game]?["versions"]?[d.Profile.Id]?["outputs"] is JsonObject previous)
                foreach (var (key, _) in previous)
                {
                    var stale = Path.Combine(staging, key.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(stale) && !ctx.Pending.ContainsKey(key))
                        File.Delete(stale);
                }
            foreach (var (key, bytes) in ctx.Pending)
            {
                var path = Path.Combine(staging, key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, bytes);
            }
            RecordVersion(manifest, d, pipeline, ctx);
            report.Versions.Add(new(d.Profile.Id, d.Profile.Title, true, ctx.Pending.Count, ctx.Warnings, null));
        }

        if (report.Versions.All(v => !v.Ok))
        {
            Directory.Delete(staging, recursive: true);
            return report;
        }
        manifest["importer_version"] = ImporterVersion;
        // A game with pictures but no tables can't be played yet: say which copy would add them.
        if (manifest["games"] is JsonObject games)
            foreach (var (game, node) in games)
            {
                if (node is not JsonObject g || g["tables"] is not null || game == "shared")
                    continue;
                var sources = VersionProfiles.All
                    .Where(p => p.Game == game && p.IsSupported && p.Feeds.Contains("tables"))
                    .Select(p => p.Title).ToList();
                if (sources.Count > 0)
                    report.Notes.Add($"{GameName(game)}: its pictures are imported, but the game itself needs its data from " +
                                     $"{string.Join(" or ", sources)}.");
            }
        File.WriteAllBytes(Path.Combine(staging, ManifestName), PyJson.SerializeUtf8(manifest, 1));
        Swap(staging);
        report.Committed = true;
        return report;
    }

    private static string GameName(string folder) => folder switch
    {
        "phantasie1" => "Phantasie I", "phantasie2" => "Phantasie II", "phantasie3" => "Phantasie III",
        "phantasie4" => "Phantasie IV", _ => folder,
    };

    /// <summary>The player-facing warning for a played copy (user decision s158: import as is and say what was lost).</summary>
    public static string PlayedWarning(Detection d)
    {
        var outs = d.PlayedFiles.Where(f => f.StartsWith("OUT", StringComparison.OrdinalIgnoreCase)).ToList();
        var files = string.Join(", ", d.PlayedFiles);
        return outs.Count > 0
            ? $"This copy of {d.Profile.Title} has been played ({files} changed). It was imported as it is: on the " +
              $"saved map regions ({string.Join(", ", outs)}) the fog is already lifted and fixed fights already won " +
              "stay gone. Import a fresh copy (an unplayed disk image, Steam \"verify files\", or the GOG installer) " +
              "to get the original maps."
            : $"This copy of {d.Profile.Title} has been played ({files} changed); those files are not read, so the " +
              "import is the same as from a fresh copy.";
    }

    private static List<Detection> Order(List<Detection> work)
    {
        var ordered = new List<Detection>();
        var left = new List<Detection>(work);
        while (left.Count > 0)
        {
            var next = left.FirstOrDefault(d =>
                PipelineRegistry.For(d.Profile.Id)!.RunsAfter.All(a => left.All(o => o.Profile.Id != a))) ?? left[0];
            ordered.Add(next);
            left.Remove(next);
        }
        return ordered;
    }

    private static JsonObject LoadManifest(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                if (JsonNode.Parse(File.ReadAllBytes(path)) is JsonObject o)
                    return o;
            }
            catch (System.Text.Json.JsonException) { }
        }
        return new JsonObject { ["_schema"] = "phantasie.import/v1", ["importer_version"] = ImporterVersion, ["games"] = new JsonObject() };
    }

    private static void RecordVersion(JsonObject manifest, Detection d, IImportPipeline pipeline, ImportContext ctx)
    {
        var games = manifest["games"] as JsonObject ?? new JsonObject();
        manifest["games"] = games;
        var game = games[d.Profile.Game] as JsonObject ?? new JsonObject();
        games[d.Profile.Game] = game;
        // A profile that can feed tables but wrote none (the Apple II II after the ST's) leaves the owner as it is.
        var tablePrefix = $"GameData/{d.Profile.Game}/";
        if (d.Profile.Feeds.Contains("tables") && ctx.Pending.Keys.Any(k => k.StartsWith(tablePrefix, StringComparison.Ordinal)
                                                                            && !k.StartsWith(tablePrefix + "graphics/", StringComparison.Ordinal)
                                                                            && !k.StartsWith(tablePrefix + "strings/", StringComparison.Ordinal)))
            game["tables"] = d.Profile.Id;
        var art = game["art"] as JsonObject ?? new JsonObject();
        game["art"] = art;
        foreach (var feed in d.Profile.Feeds.Where(f => f.StartsWith("art:", StringComparison.Ordinal)))
            art[feed[4..]] = d.Profile.Id;
        var versions = game["versions"] as JsonObject ?? new JsonObject();
        game["versions"] = versions;
        var outputs = new JsonObject();
        foreach (var (key, bytes) in ctx.Pending.OrderBy(k => k.Key, StringComparer.Ordinal))
            outputs[key] = Hashing.Sha1(bytes);
        var keyFiles = new JsonObject();
        foreach (var k in d.Profile.Disks.SelectMany(x => x.KeyFiles))
            keyFiles[k.Name] = k.Sha1;
        versions[d.Profile.Id] = new JsonObject
        {
            ["title"] = d.Profile.Title,
            ["recipe"] = pipeline.RecipeVersion,
            ["date"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["sources"] = new JsonArray(d.Containers.Select(c => (JsonNode)JsonValue.Create(c.SourcePath ?? c.Description)!).ToArray()),
            ["key_files"] = keyFiles,
            ["played_files"] = new JsonArray(d.PlayedFiles.Select(f => (JsonNode)JsonValue.Create(f)!).ToArray()),
            ["warnings"] = new JsonArray(ctx.Warnings.Select(w => (JsonNode)JsonValue.Create(w)!).ToArray()),
            ["outputs"] = outputs,
        };
    }

    private void Swap(string staging)
    {
        var old = ImportRoot + ".old";
        if (Directory.Exists(old))
            Directory.Delete(old, recursive: true);
        if (Directory.Exists(ImportRoot))
            Directory.Move(ImportRoot, old);
        Directory.Move(staging, ImportRoot);
        if (Directory.Exists(old))
            Directory.Delete(old, recursive: true);
    }

    private static void CopyTree(string from, string to)
    {
        foreach (var dir in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)), overwrite: true);
    }
}
