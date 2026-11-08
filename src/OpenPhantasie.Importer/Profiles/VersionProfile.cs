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
    /// <summary>
    /// Other files this version's import wants and can't take from its own disks (s160: the Amiga games' screen font
    /// comes from the Kickstart ROM). When the import ends without a companion's output, the report offers to add it.
    /// </summary>
    [JsonPropertyName("companions")] public List<Companion> Companions { get; init; } = [];

    public bool IsSupported => Status == "supported";

    public sealed class Companion
    {
        /// <summary>The profile that gives it (e.g. <c>kickstart-1.3</c>).</summary>
        [JsonPropertyName("profile")] public string Profile { get; init; } = "";
        /// <summary>The import path that is there once it is given (e.g. <c>GameData/phantasie1/graphics/fonts/amiga-topaz-8.json</c>).</summary>
        [JsonPropertyName("output")] public string Output { get; init; } = "";
        /// <summary>A string id (<c>shared/strings</c>) naming what the companion gives, for the player.</summary>
        [JsonPropertyName("gives")] public string Gives { get; init; } = "";
    }

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
