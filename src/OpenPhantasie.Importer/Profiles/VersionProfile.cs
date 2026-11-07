using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenPhantasie.Importer.Profiles;

/// <summary>A version of one game the importer knows (<c>Profiles/profiles.json</c>).</summary>
public sealed class VersionProfile
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    /// <summary>GameData folder name (<c>phantasie1</c> …) or <c>shared</c> (e.g. the Kickstart ROM).</summary>
    [JsonPropertyName("game")] public string Game { get; init; } = "";
    [JsonPropertyName("title")] public string Title { get; init; } = "";
    [JsonPropertyName("platform")] public string Platform { get; init; } = "";
    /// <summary><c>supported</c>, <c>planned</c> or <c>recognised</c>.</summary>
    [JsonPropertyName("status")] public string Status { get; init; } = "recognised";
    [JsonPropertyName("reason")] public string? Reason { get; init; }
    /// <summary>What it builds: <c>tables</c>, <c>art:&lt;pack&gt;</c>, <c>strings:ja</c>, <c>font:topaz-8</c>.</summary>
    [JsonPropertyName("feeds")] public List<string> Feeds { get; init; } = [];
    [JsonPropertyName("disks")] public List<Disk> Disks { get; init; } = [];
    [JsonPropertyName("name_markers")] public List<string> NameMarkers { get; init; } = [];
    [JsonPropertyName("mutable_files")] public List<KeyFile> MutableFiles { get; init; } = [];
    [JsonPropertyName("images")] public List<ImageFingerprint> Images { get; init; } = [];

    public bool IsSupported => Status == "supported";

    public sealed class Disk
    {
        [JsonPropertyName("label")] public string Label { get; init; } = "";
        [JsonPropertyName("key_files")] public List<KeyFile> KeyFiles { get; init; } = [];
    }

    public sealed class KeyFile
    {
        /// <summary>File name, or a path tail such as <c>D/m0</c>; <c>*</c> = any name.</summary>
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("size")] public long Size { get; init; }
        [JsonPropertyName("sha1")] public string Sha1 { get; init; } = "";
    }

    public sealed class ImageFingerprint
    {
        [JsonPropertyName("sha1")] public string Sha1 { get; init; } = "";
        [JsonPropertyName("size")] public long Size { get; init; }
        [JsonPropertyName("note")] public string? Note { get; init; }
    }

    public override string ToString() => Id;
}

public static class VersionProfiles
{
    private sealed class FileDto
    {
        [JsonPropertyName("profiles")] public List<VersionProfile> Profiles { get; init; } = [];
    }

    private static IReadOnlyList<VersionProfile>? _all;

    /// <summary>Every profile, from the embedded <c>profiles.json</c>.</summary>
    public static IReadOnlyList<VersionProfile> All => _all ??= LoadEmbedded();

    public static VersionProfile Get(string id) =>
        All.FirstOrDefault(p => p.Id == id) ?? throw new KeyNotFoundException($"no import profile '{id}'");

    private static IReadOnlyList<VersionProfile> LoadEmbedded()
    {
        using var s = typeof(VersionProfiles).Assembly.GetManifestResourceStream("OpenPhantasie.Importer.profiles.json")
            ?? throw new InvalidOperationException("profiles.json not embedded");
        return JsonSerializer.Deserialize<FileDto>(s)!.Profiles;
    }
}
