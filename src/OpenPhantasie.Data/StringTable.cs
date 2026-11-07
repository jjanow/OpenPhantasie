using System.Globalization;
using System.Text.Json;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// On-screen text of one game in one language (T1.5), keyed by id: <c>GameData/&lt;game&gt;/strings/&lt;language&gt;.json</c>.
/// Templates use .NET composite formatting (<c>{0}</c>, <c>{0,3}</c>, <c>{0:D5}</c>), so a translation can reorder the
/// arguments. Missing ids throw: every id the Client and Core use must be in the table (tests check this). The table
/// also serves Core's <see cref="GameText"/> (ids <c>core.*</c>): one layered table per running game holds both.
/// </summary>
public sealed class StringTable : ITextSource
{
    private readonly Dictionary<string, string> _strings;

    private StringTable(string game, string language, Dictionary<string, string> strings)
    {
        Game = game;
        Language = language;
        _strings = strings;
    }

    public string Game { get; }
    public string Language { get; }
    public IReadOnlyDictionary<string, string> Strings => _strings;

    /// <summary>The text for <paramref name="id"/>.</summary>
    public string Get(string id) =>
        _strings.TryGetValue(id, out var text)
            ? text
            : throw new KeyNotFoundException($"String '{id}' is not in the {Game} {Language} string table.");

    /// <summary>The template for <paramref name="id"/> filled with <paramref name="args"/> (current culture, as <c>$"…"</c>).</summary>
    public string Format(string id, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(id), args);

    public bool Contains(string id) => _strings.ContainsKey(id);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Load <c>strings/&lt;language&gt;.json</c> under <paramref name="gameDataDirectory"/>. A later layer (e.g. a
    /// game's table over a shared one) can be merged with <see cref="Over"/>.
    /// </summary>
    public static StringTable Load(string gameDataDirectory, string language = "en") =>
        LoadFile(Path.Combine(gameDataDirectory, "strings", language + ".json"));

    /// <summary>
    /// The running game's table: the shared shell table (<c>GameData/shared</c>) with <paramref name="game"/>'s own table
    /// over it (the shared one alone if the game has none yet).
    /// </summary>
    public static StringTable ForGame(GameId game, string language = "en")
    {
        var shared = WithImported(Load(GameDataRepository.SharedDataPath, language), "shared", language);
        var path = GameDataRepository.DefaultPathFor(game);
        if (!File.Exists(Path.Combine(path, "strings", language + ".json")))
            return shared;
        var folder = "phantasie" + (int)game;
        var own = Load(path, language);
        // A game whose text is mostly another's (II reuses I's ids) names that folder in "extends"; its own ids go on top.
        var chain = new List<StringTable> { WithImported(own, folder, language) };
        for (var t = own; t.Extends is { Length: > 0 } baseFolder && chain.Count < 4;)
        {
            t = Load(GameDataLocator.FolderNamed(baseFolder), language);
            chain.Insert(0, WithImported(t, baseFolder, language));
        }
        var table = shared;
        foreach (var layer in chain)
            table = table.Over(layer);
        return table;
    }

    /// <summary>
    /// <paramref name="table"/> with the importer's files for <paramref name="folder"/> over it: the games' own lines,
    /// built from the player's copy, in place of the "..." the public table ships (10-importer.md 7.6).
    /// </summary>
    private static StringTable WithImported(StringTable table, string folder, string language)
    {
        foreach (var file in GameDataLocator.ImportedStringFiles(folder, language))
        {
            try
            {
                var layer = LoadFile(file);
                table = new StringTable(table.Game, table.Language, table.Over(layer)._strings) { Extends = table.Extends };
            }
            catch (Exception e) when (e is IOException or JsonException or InvalidDataException) { }
        }
        return table;
    }

    /// <summary>The data folder (e.g. <c>phantasie1</c>) whose table this one goes over (<c>"extends"</c>), or null.</summary>
    public string? Extends { get; private init; }

    public static StringTable LoadFile(string path)
    {
        var dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"{path}: empty.");
        if (dto.Strings is null)
            throw new InvalidDataException($"{path}: 'strings' missing.");
        return new StringTable(dto.Game ?? "", dto.Language ?? "", new Dictionary<string, string>(dto.Strings, StringComparer.Ordinal))
        {
            Extends = dto.Extends,
        };
    }

    /// <summary>This table with <paramref name="overlay"/>'s ids added or replacing these.</summary>
    public StringTable Over(StringTable overlay)
    {
        var merged = new Dictionary<string, string>(_strings, StringComparer.Ordinal);
        foreach (var (id, text) in overlay._strings)
            merged[id] = text;
        return new StringTable(overlay.Game, overlay.Language, merged);
    }

    private sealed class FileDto
    {
        public string? Game { get; set; }
        public string? Language { get; set; }
        public Dictionary<string, string>? Strings { get; set; }
        public string? Extends { get; set; }
    }
}
