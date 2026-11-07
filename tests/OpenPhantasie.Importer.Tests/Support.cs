using System.Text.Json;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>Repo paths for the golden tests (found by walking up from the test output folder).</summary>
public static class Repo
{
    private static readonly Lazy<string?> _root = new(() =>
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (Directory.Exists(System.IO.Path.Combine(d.FullName, "reverse-engineering")) && File.Exists(System.IO.Path.Combine(d.FullName, "OpenPhantasie.sln")))
                return d.FullName;
        return null;
    });

    public static string? Root => _root.Value;

    public static string Path(string repoRelative) =>
        System.IO.Path.Combine(Root ?? throw new InvalidOperationException("repo root not found"),
            repoRelative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public static bool Has(params string[] repoRelative) =>
        Root is not null && repoRelative.All(p => File.Exists(Path(p)) || Directory.Exists(Path(p)));

    /// <summary>The committed GameData (the golden for table output; also the "shipped" folder in the private repo).</summary>
    /// Without the private repo's reference files (the public repo) it is the GameData shipped next to the tests.
    public static string GameData => Root is null ? OpenPhantasie.Data.GameDataLocator.ShippedRoot : Path("src/OpenPhantasie.Data/GameData");
}

/// <summary>A fact that is skipped when the reference images (or other repo files) are absent, e.g. in the public repo.</summary>
public sealed class RequiresFilesFactAttribute : FactAttribute
{
    public RequiresFilesFactAttribute(params string[] repoRelativePaths)
    {
        if (!Repo.Has(repoRelativePaths))
            Skip = "reference files absent: " + string.Join(", ", repoRelativePaths);
    }
}

public sealed class RequiresFilesTheoryAttribute : TheoryAttribute
{
    public RequiresFilesTheoryAttribute(params string[] repoRelativePaths)
    {
        if (!Repo.Has(repoRelativePaths))
            Skip = "reference files absent: " + string.Join(", ", repoRelativePaths);
    }
}

/// <summary>Runs one pipeline on repo images, in memory, and returns what it would write.</summary>
public static class PipelineHarness
{
    /// <summary>
    /// A pipeline's writes. <see cref="Outputs"/> leaves out the games' own string-table lines
    /// (<c>GameData/&lt;table&gt;/strings/en.&lt;profile&gt;.json</c>, <see cref="ImportedStrings"/>), which have no
    /// committed counterpart file and are checked by <c>ImportedStringsTests</c> through <see cref="AllOutputs"/>.
    /// </summary>
    public sealed record Result(IReadOnlyDictionary<string, byte[]> AllOutputs, IReadOnlyList<string> Warnings, Detection Detection)
    {
        public IReadOnlyDictionary<string, byte[]> Outputs => AllOutputs
            .Where(kv => !IsImportedStrings(kv.Key, Detection.Profile.Id))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        private static bool IsImportedStrings(string key, string profile) =>
            key.StartsWith("GameData/", StringComparison.Ordinal) && key.EndsWith("/" + ImportedStrings.OutputFile(profile), StringComparison.Ordinal);
    }

    private static readonly Dictionary<string, Result> Cache = new();

    /// <summary>
    /// Scan <paramref name="repoRelativeSources"/>, detect <paramref name="profileId"/> and run its pipeline. Shipped
    /// GameData = the committed GameData; there is no earlier import. Results are cached per (profile, sources).
    /// </summary>
    public static Result Run(string profileId, params string[] repoRelativeSources)
    {
        var cacheKey = profileId + "|" + string.Join("|", repoRelativeSources);
        lock (Cache)
        {
            if (Cache.TryGetValue(cacheKey, out var cached))
                return cached;
            var scan = ImportRunner.Scan(repoRelativeSources.Select(Repo.Path).ToList());
            var detection = scan.Detections.Single(d => d.Profile.Id == profileId);
            Assert.Equal(DetectionStatus.Ready, detection.Status);
            var staging = Directory.CreateTempSubdirectory("phantasie-import-test-").FullName;
            try
            {
                var session = new ImportSession
                {
                    ShippedGameDataRoot = Repo.GameData, StagingRoot = staging, Detections = scan.Detections,
                };
                var ctx = new ImportContext(session, detection);
                PipelineRegistry.For(profileId)!.Run(ctx);
                var result = new Result(new Dictionary<string, byte[]>(ctx.Pending), ctx.Warnings.ToList(), detection);
                Cache[cacheKey] = result;
                return result;
            }
            finally
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }
}

/// <summary>Golden comparisons with readable failures.</summary>
public static class Golden
{
    /// <summary>Assert <paramref name="actual"/> is JSON-equal to the committed file at <paramref name="repoRelative"/>.</summary>
    public static void JsonEqualsFile(byte[] actual, string repoRelative)
    {
        var expected = JsonNode.Parse(File.ReadAllBytes(Repo.Path(repoRelative)));
        var got = JsonNode.Parse(actual);
        var diff = FirstDifference(expected, got, "$");
        Assert.True(diff is null, $"{repoRelative}: {diff}");
    }

    /// <summary>True when the bytes equal the committed file exactly (formatting included).</summary>
    public static bool BytesEqualFile(byte[] actual, string repoRelative) =>
        File.ReadAllBytes(Repo.Path(repoRelative)).AsSpan().SequenceEqual(actual);

    public static void PngEqualsFile(byte[] actualPng, string repoRelative)
    {
        var expected = Png.Decode(File.ReadAllBytes(Repo.Path(repoRelative)));
        var got = Png.Decode(actualPng);
        Assert.True(expected.Width == got.Width && expected.Height == got.Height,
            $"{repoRelative}: size {got.Width}x{got.Height}, expected {expected.Width}x{expected.Height}");
        Assert.True(expected.PixelEquals(got), $"{repoRelative}: pixels differ");
    }

    /// <summary>A path and both values of the first difference, or null when equal.</summary>
    public static string? FirstDifference(JsonNode? a, JsonNode? b, string path)
    {
        switch (a)
        {
            case null:
                return b is null ? null : $"{path}: expected null, got {Short(b)}";
            case JsonObject oa:
                if (b is not JsonObject ob)
                    return $"{path}: expected object, got {Short(b)}";
                foreach (var (k, v) in oa)
                {
                    if (!ob.ContainsKey(k))
                        return $"{path}.{k}: missing";
                    if (FirstDifference(v, ob[k], $"{path}.{k}") is { } d)
                        return d;
                }
                foreach (var (k, _) in ob)
                    if (!oa.ContainsKey(k))
                        return $"{path}.{k}: unexpected key";
                return null;
            case JsonArray aa:
                if (b is not JsonArray ab)
                    return $"{path}: expected array, got {Short(b)}";
                for (var i = 0; i < Math.Min(aa.Count, ab.Count); i++)
                    if (FirstDifference(aa[i], ab[i], $"{path}[{i}]") is { } d)
                        return d;
                return aa.Count == ab.Count ? null : $"{path}: length {ab.Count}, expected {aa.Count}";
            default:
                if (b is not JsonValue vb)
                    return $"{path}: expected {Short(a)}, got {Short(b)}";
                var va = (JsonValue)a;
                if (va.GetValueKind() == JsonValueKind.Number && vb.GetValueKind() == JsonValueKind.Number)
                    return va.GetValue<double>() == vb.GetValue<double>() ? null : $"{path}: expected {Short(a)}, got {Short(b)}";
                return JsonNode.DeepEquals(va, vb) ? null : $"{path}: expected {Short(a)}, got {Short(b)}";
        }
    }

    private static string Short(JsonNode? n)
    {
        var s = n?.ToJsonString() ?? "null";
        return s.Length > 120 ? s[..120] + "…" : s;
    }
}
