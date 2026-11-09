using System.Net.Http.Headers;
using System.Text.Json;

namespace OpenPhantasie.Client.Updates;

/// <summary>A release newer than the running build, with the archive for this machine.</summary>
public sealed record UpdateRelease(Version Version, string Tag, string Notes, string PageUrl, string AssetName,
    string AssetUrl, long AssetSize);

/// <summary>What a check found.</summary>
public enum UpdateCheckOutcome
{
    /// <summary>The running build is the latest release (or newer).</summary>
    UpToDate,
    /// <summary>A newer release with an archive for this machine (<see cref="UpdateCheckResult.Release"/>).</summary>
    Available,
    /// <summary>A newer release exists but offers no archive for this machine.</summary>
    NoBuildForPlatform,
}

public sealed record UpdateCheckResult(UpdateCheckOutcome Outcome, UpdateRelease? Release, Version? Latest);

/// <summary>
/// Asks GitHub for the repository's latest release (<c>GET /repos/{owner}/{name}/releases/latest</c>, no sign-in) and
/// downloads its archive for this machine (s174). Release archives are named
/// <c>OpenPhantasie-{version}-{platform}.zip</c> (Windows) or <c>.tar.gz</c> (Linux, macOS); see <see cref="AppVersion.PlatformId"/>.
/// </summary>
public sealed class UpdateService(HttpClient http, string repository, Version current, string? platformId)
{
    /// <summary>The service for this build, on a shared client.</summary>
    public static UpdateService ForThisBuild() =>
        new(SharedClient.Value, AppVersion.Repository, AppVersion.Current, AppVersion.PlatformId)
        {
            // For testing the updater against a local server (s174): the API root to ask instead of GitHub's.
            ApiRoot = Environment.GetEnvironmentVariable("OPENPHANTASIE_UPDATE_API") is { Length: > 0 } api
                ? api.TrimEnd('/') : "https://api.github.com",
        };

    private static readonly Lazy<HttpClient> SharedClient = new(() =>
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OpenPhantasie", AppVersion.Text));
        return c;
    });

    /// <summary>The API root (tests point it at a local server).</summary>
    public string ApiRoot { get; init; } = "https://api.github.com";

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancel = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiRoot}/repos/{repository}/releases/latest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await http.SendAsync(request, cancel).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new(UpdateCheckOutcome.UpToDate, null, null); // no release published yet
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(body, cancellationToken: cancel).ConfigureAwait(false);
        return Evaluate(doc.RootElement, current, platformId);
    }

    /// <summary>Read a release JSON (GitHub's release object) against the running version and platform.</summary>
    public static UpdateCheckResult Evaluate(JsonElement release, Version current, string? platformId)
    {
        var tag = release.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        var latest = AppVersion.TryParse(tag);
        if (latest is null || latest <= current)
            return new(UpdateCheckOutcome.UpToDate, null, latest);
        var notes = release.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() ?? "" : "";
        var page = release.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
        if (platformId is not null && release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (!IsAssetFor(name, platformId))
                    continue;
                return new(UpdateCheckOutcome.Available,
                    new UpdateRelease(latest, tag, notes, page, name, a.GetProperty("browser_download_url").GetString() ?? "",
                        a.TryGetProperty("size", out var s) ? s.GetInt64() : 0),
                    latest);
            }
        return new(UpdateCheckOutcome.NoBuildForPlatform, null, latest);
    }

    /// <summary>Whether a release file is the archive for <paramref name="platformId"/> (<c>…-linux-x64.tar.gz</c>).</summary>
    public static bool IsAssetFor(string name, string platformId) =>
        name.StartsWith("OpenPhantasie-", StringComparison.OrdinalIgnoreCase)
        && (name.EndsWith($"-{platformId}.zip", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith($"-{platformId}.tar.gz", StringComparison.OrdinalIgnoreCase));

    /// <summary>Download the release's archive to <paramref name="path"/>, reporting the fraction done (0..1).</summary>
    public async Task DownloadAsync(UpdateRelease release, string path, IProgress<double>? progress, CancellationToken cancel = default)
    {
        using var response = await http.GetAsync(release.AssetUrl, HttpCompletionOption.ResponseHeadersRead, cancel)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? release.AssetSize;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var source = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
        await using (var target = File.Create(path))
        {
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancel).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancel).ConfigureAwait(false);
                done += read;
                if (total > 0)
                    progress?.Report(Math.Min(1.0, (double)done / total));
            }
        }
        if (release.AssetSize > 0 && new FileInfo(path).Length != release.AssetSize)
            throw new IOException($"the download is {new FileInfo(path).Length} bytes, the release says {release.AssetSize}");
    }
}
