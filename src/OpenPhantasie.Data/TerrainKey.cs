using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>One row of a terrain key: the string id of its name and the pack pictures drawn as its swatch.</summary>
public sealed record TerrainKeyEntry(string Label, IReadOnlyList<string> Pictures);

/// <summary>
/// A game's terrain key (<c>GameData/&lt;game&gt;/terrain-key.json</c>; PLAN P3 QoL, Phantasie II's TERRAIN KEY switch,
/// DIFFERENCES-II D2-014): the rows the rulebook's key lists beside the map, each with the map pictures (fill patterns,
/// icons) that show it. Data only: which picture means which terrain is the file's (and its note's) reading.
/// </summary>
public static class TerrainKey
{
    public const string FileName = "terrain-key.json";

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The key of <paramref name="game"/>, or empty when the game has none (no file, or a bad one).</summary>
    public static IReadOnlyList<TerrainKeyEntry> For(GameId game) =>
        Load(Path.Combine(GameDataLocator.GameFolder(game), FileName));

    /// <summary>The key in <paramref name="path"/>; empty when the file is missing or unreadable.</summary>
    public static IReadOnlyList<TerrainKeyEntry> Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return [];
            var file = JsonSerializer.Deserialize<KeyFile>(File.ReadAllText(path), Options);
            return (file?.Entries ?? [])
                .Where(e => !string.IsNullOrEmpty(e.Label))
                .Select(e => new TerrainKeyEntry(e.Label!, e.Pictures ?? []))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private sealed class KeyFile
    {
        [JsonPropertyName("entries")]
        public List<KeyRow>? Entries { get; set; }
    }

    private sealed class KeyRow
    {
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("pictures")]
        public List<string>? Pictures { get; set; }
    }
}
