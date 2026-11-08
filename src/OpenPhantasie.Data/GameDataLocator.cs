using System.Text.Json;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Where each game's data comes from (10-importer.md 4.3): the player's import (<c>&lt;data dir&gt;/import/</c>, built by
/// <c>OpenPhantasie.Importer</c> from their own copy), else, in a dev build only, the GameData committed in this repo.
/// <para><b>The switch:</b> MSBuild property <c>OpenPhantasieBuiltInGameData</c> (default <c>true</c> here, in
/// <c>OpenPhantasie.Data.csproj</c>). Set it to <c>false</c> (the public repo) and a game without an import is unavailable,
/// and art is looked up only in the import; with <c>true</c> the committed GameData and the repo's
/// <c>reverse-engineering/</c> art roots stay usable as a fallback.</para>
/// <para>The import root is opt-in: only the Client sets it (<see cref="UseImport"/>), so tests always read the
/// committed GameData whatever is imported on the machine.</para>
/// </summary>
public static class GameDataLocator
{
#if OPENPHANTASIE_NO_BUILTIN_GAMEDATA
    public const bool BuiltInFallback = false;
#else
    public const bool BuiltInFallback = true;
#endif

    /// <summary>The GameData shipped with the app (class (a) files and overlays; the full files in the private repo).</summary>
    public static string ShippedRoot => Path.Combine(AppContext.BaseDirectory, "GameData");

    /// <summary>The import folder in use, or null (tests, and before the Client starts).</summary>
    public static string? ImportRoot { get; private set; }

    private static ImportState _state = ImportState.Empty;

    /// <summary>
    /// Read <c>import.json</c> under <paramref name="importRoot"/> (null = no import) and bring each imported game's
    /// folder up to date with the shipped files the import doesn't build (our own data, which can change with an
    /// app update). Call again after an import run.
    /// </summary>
    public static void UseImport(string? importRoot)
    {
        ImportRoot = importRoot;
        _state = importRoot is null ? ImportState.Empty : ImportState.Load(importRoot);
        foreach (var game in _state.TableGames)
            SyncShipped(game);
    }

    /// <summary>The game's tables come from the player's import.</summary>
    public static bool IsImported(GameId game) => _state.TableGames.Contains(Folder(game));

    /// <summary>The game can be started: imported, or (dev build) its committed GameData is present.</summary>
    public static bool IsAvailable(GameId game) =>
        IsImported(game) || (BuiltInFallback && File.Exists(Path.Combine(ShippedRoot, Folder(game), "game.json")));

    /// <summary>The data folder the game loads from (whether or not it exists).</summary>
    public static string GameFolder(GameId game) => FolderNamed(Folder(game));

    /// <summary>A data folder by name (<c>phantasie1</c>, as in a string table's <c>extends</c>).</summary>
    public static string FolderNamed(string folder) =>
        ImportRoot is not null && _state.TableGames.Contains(folder)
            ? Path.Combine(ImportRoot, "GameData", folder)
            : Path.Combine(ShippedRoot, folder);

    /// <summary>
    /// The importer's string files for the table in <paramref name="folder"/> (<c>shared</c>, <c>phantasie1</c> …):
    /// <c>&lt;import&gt;/GameData/&lt;folder&gt;/strings/&lt;language&gt;.&lt;profile&gt;.json</c>, the games' own lines
    /// that the public tables carry only as "..." (10-importer.md 7.6), fallback files first. Empty without an import
    /// (tests, dev builds).
    /// </summary>
    public static IReadOnlyList<string> ImportedStringFiles(string folder, string language = "en")
    {
        if (ImportRoot is null)
            return [];
        var dir = Path.Combine(ImportRoot, "GameData", folder, "strings");
        if (!Directory.Exists(dir))
            return [];
        // The remaster's own wording for lines a version lacks (en.fallback.<profile>.json) goes under every other
        // imported file, so a version that holds the line still wins. A version's lines in its own wording
        // (en.own.<profile>.json, in its game's folder) go on top, but only while that game's tables come from that
        // version (10-importer.md 7.6).
        var fallback = language + ".fallback.";
        var own = language + ".own.";
        _state.Tables.TryGetValue(folder, out var tables);
        return Directory.EnumerateFiles(dir, language + ".*.json")
            .Select(f => (Path: f, Name: Path.GetFileName(f)))
            .Where(f => !f.Name.Equals(language + ".sources.json", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Name.StartsWith(own, StringComparison.OrdinalIgnoreCase)
                        || f.Name.Equals($"{own}{tables}.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Name.StartsWith(fallback, StringComparison.OrdinalIgnoreCase) ? 0
                : f.Name.StartsWith(own, StringComparison.OrdinalIgnoreCase) ? 2 : 1)
            .ThenBy(f => f.Path, StringComparer.Ordinal)
            .Select(f => f.Path)
            .ToList();
    }

    /// <summary>
    /// Roots searched for graphics-pack folders (repo-relative pack <c>root</c> paths): the import's <c>files/</c>
    /// first; in a dev build, then the app folder and its parents (the repo's <c>reverse-engineering/</c>).
    /// </summary>
    public static IEnumerable<string> ArtSearchRoots(string? searchFrom = null)
    {
        if (ImportRoot is not null)
            yield return Path.Combine(ImportRoot, "files");
        if (!BuiltInFallback && searchFrom is null)
            yield break;
        for (var dir = new DirectoryInfo(searchFrom ?? AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            yield return dir.FullName;
    }

    /// <summary>Art packs imported for <paramref name="game"/> (pack id → version profile id).</summary>
    public static IReadOnlyDictionary<string, string> ImportedArt(GameId game) =>
        _state.Art.TryGetValue(Folder(game), out var a) ? a : new Dictionary<string, string>();

    /// <summary>Version profile ids imported for <paramref name="game"/> (tables or art).</summary>
    public static IReadOnlyList<string> ImportedVersions(GameId game) =>
        _state.Versions.TryGetValue(Folder(game), out var v) ? v : [];

    private static string Folder(GameId game) => "phantasie" + (int)game;

    /// <summary>
    /// Copy the shipped files the import didn't build into the imported game's folder (new or changed ones only).
    /// </summary>
    private static void SyncShipped(string folder)
    {
        var shipped = Path.Combine(ShippedRoot, folder);
        var target = Path.Combine(ImportRoot!, "GameData", folder);
        if (!Directory.Exists(shipped) || !Directory.Exists(target))
            return;
        foreach (var file in Directory.EnumerateFiles(shipped, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(shipped, file).Replace('\\', '/');
            if (_state.Outputs.Contains($"GameData/{folder}/{rel}"))
                continue;
            var dest = Path.Combine(target, rel);
            try
            {
                if (File.Exists(dest) && new FileInfo(dest).Length == new FileInfo(file).Length
                    && File.ReadAllBytes(dest).AsSpan().SequenceEqual(File.ReadAllBytes(file)))
                    continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class ImportState
    {
        public static readonly ImportState Empty = new();
        public HashSet<string> TableGames { get; } = new(StringComparer.Ordinal);

        /// <summary>Game folder → the version profile its tables come from.</summary>
        public Dictionary<string, string> Tables { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Outputs { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Dictionary<string, string>> Art { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<string>> Versions { get; } = new(StringComparer.Ordinal);

        public static ImportState Load(string root)
        {
            var path = Path.Combine(root, "import.json");
            var state = new ImportState();
            if (!File.Exists(path))
                return state;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
                if (!doc.RootElement.TryGetProperty("games", out var games) || games.ValueKind != JsonValueKind.Object)
                    return state;
                foreach (var g in games.EnumerateObject())
                {
                    if (g.Value.TryGetProperty("tables", out var t) && t.ValueKind == JsonValueKind.String
                        && Directory.Exists(Path.Combine(root, "GameData", g.Name)))
                    {
                        state.TableGames.Add(g.Name);
                        state.Tables[g.Name] = t.GetString() ?? "";
                    }
                    var art = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (g.Value.TryGetProperty("art", out var a) && a.ValueKind == JsonValueKind.Object)
                        foreach (var p in a.EnumerateObject())
                            art[p.Name] = p.Value.GetString() ?? "";
                    state.Art[g.Name] = art;
                    var versions = new List<string>();
                    if (g.Value.TryGetProperty("versions", out var vs) && vs.ValueKind == JsonValueKind.Object)
                        foreach (var v in vs.EnumerateObject())
                        {
                            versions.Add(v.Name);
                            if (v.Value.TryGetProperty("outputs", out var outs) && outs.ValueKind == JsonValueKind.Object)
                                foreach (var o in outs.EnumerateObject())
                                    state.Outputs.Add(o.Name);
                        }
                    state.Versions[g.Name] = versions;
                }
            }
            catch (JsonException) { }
            catch (IOException) { }
            return state;
        }
    }
}
