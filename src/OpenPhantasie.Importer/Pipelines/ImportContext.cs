using System.Text.Json;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Pipelines;

/// <summary>
/// What a pipeline sees: the detected version's files, readers for other GameData (this run's output, then the
/// existing import, then the shipped GameData), and writers. Writes are buffered and committed only when the
/// pipeline finishes without an exception, so a failed pipeline leaves nothing behind.
/// </summary>
public sealed class ImportContext
{
    private readonly ImportSession _session;
    internal readonly Dictionary<string, byte[]> Pending = new(StringComparer.Ordinal);

    internal ImportContext(ImportSession session, Detection detection)
    {
        _session = session;
        Detection = detection;
    }

    public Detection Detection { get; }
    public VersionProfile Profile => Detection.Profile;
    public FileSet Files => Detection.Files!;
    public CancellationToken Cancel => _session.Cancel;
    public List<string> Warnings { get; } = [];

    /// <summary>The files of another detected (Ready) version in this run, e.g. <c>kickstart-1.3</c>; null if absent.</summary>
    public FileSet? OptionalFiles(string profileId) =>
        _session.Detections.FirstOrDefault(d => d.Profile.Id == profileId && d.Status == DetectionStatus.Ready)?.Files;

    public void Warn(string message) => Warnings.Add(message);

    public void Progress(string message) => _session.Progress?.Report($"{Profile.Title}: {message}");

    // ---- readers ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A GameData file of <paramref name="game"/> (<c>phantasie1</c> …, path like <c>overworld.json</c>): this run's
    /// output first, then the existing import, then the shipped GameData. Null when none has it.
    /// </summary>
    public byte[]? ReadGameDataBytes(string game, string relativePath)
    {
        var key = GameDataKey(game, relativePath);
        if (Pending.TryGetValue(key, out var mine))
            return mine;
        if (_session.ReadStaged(key) is { } staged)
            return staged;
        return ReadShippedBytes(game, relativePath);
    }

    /// <summary>
    /// A GameData file this run or an earlier import wrote (never the shipped one): e.g. whether another copy of the
    /// game already gave its tables.
    /// </summary>
    public JsonNode? ReadImportedGameData(string game, string relativePath)
    {
        var key = GameDataKey(game, relativePath);
        var bytes = Pending.TryGetValue(key, out var mine) ? mine : _session.ReadStaged(key);
        return bytes is null ? null : JsonNode.Parse(bytes, documentOptions: JsonDocOptions);
    }

    public JsonNode? ReadGameData(string game, string relativePath) =>
        ReadGameDataBytes(game, relativePath) is { } b ? JsonNode.Parse(b, documentOptions: JsonDocOptions) : null;

    /// <summary>
    /// The shipped GameData file (our own data: class (a) files and the overlays of (c) files, 10-importer.md 2.7).
    /// In the private repo the shipped folder still holds the full files; pipelines must take only the fields that
    /// are ours from it.
    /// </summary>
    public byte[]? ReadShippedBytes(string game, string relativePath)
    {
        var path = Path.Combine(_session.ShippedGameDataRoot, game, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public JsonNode? ReadShipped(string game, string relativePath) =>
        ReadShippedBytes(game, relativePath) is { } b ? JsonNode.Parse(b, documentOptions: JsonDocOptions) : null;

    internal static readonly JsonDocumentOptions JsonDocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
    };

    // ---- writers ---------------------------------------------------------------------------------------------

    /// <summary>Write <c>GameData/&lt;game&gt;/&lt;relativePath&gt;</c> as Python's <c>json.dumps(indent=…)</c> would.</summary>
    public void WriteGameData(string game, string relativePath, JsonNode node, int? indent = 2, bool ensureAscii = false) =>
        Pending[GameDataKey(game, relativePath)] = PyJson.SerializeUtf8(node, indent, ensureAscii);

    public void WriteGameDataBytes(string game, string relativePath, byte[] bytes) =>
        Pending[GameDataKey(game, relativePath)] = bytes;

    /// <summary>
    /// Write an art or raw file at its repo-relative path (e.g. <c>reverse-engineering/amiga/extracted/monsters/x.png</c>,
    /// <c>reverse-engineering/amiga/unpacked/PHANT/tamb.dat</c>). The import keeps the same tree under <c>files/</c>, so
    /// the graphics packs' <c>root</c> and <c>../unpacked/…</c> paths resolve unchanged.
    /// </summary>
    public void WriteFile(string repoRelativePath, byte[] bytes) =>
        Pending["files/" + Normalize(repoRelativePath)] = bytes;

    public void WritePng(string repoRelativePath, RgbaImage image, bool forceAlpha = false) =>
        WriteFile(repoRelativePath, Png.Encode(image, forceAlpha));

    public void WriteJsonFile(string repoRelativePath, JsonNode node, int? indent = 2, bool ensureAscii = false) =>
        WriteFile(repoRelativePath, PyJson.SerializeUtf8(node, indent, ensureAscii));

    internal static string GameDataKey(string game, string relativePath) => $"GameData/{game}/{Normalize(relativePath)}";

    private static string Normalize(string p)
    {
        var n = p.Replace('\\', '/').Trim('/');
        if (n.Split('/').Any(s => s is ".." or "." or ""))
            throw new ArgumentException($"bad output path '{p}'");
        return n;
    }
}
