using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P1Dos;
using OpenPhantasie.Importer.Pipelines.P2Apple2;
using OpenPhantasie.Importer.Pipelines.P3Dos;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Pipelines;

/// <summary>
/// The games' own lines in the string tables (10-importer.md 7.6). The shipped <c>strings/en.sources.json</c> of each
/// table (<c>tools/scripts/build_string_sources.py</c>) says, per id, which parts of its text are original and where
/// they sit in each version's files; the public <c>strings/en.json</c> carries those ids with "..." in their place.
/// A pipeline calls <see cref="Write"/> with its version's files, and every id that version can give is written to
/// <c>GameData/&lt;table&gt;/strings/en.&lt;profile&gt;.json</c>, which <c>StringTable.ForGame</c> lays over the
/// shipped table (<c>GameDataLocator.ImportedStringFiles</c>).
/// </summary>
public static class ImportedStrings
{
    /// <summary>The tables with a sources file (Phantasie IV's English is the id-keyed patch instead, 4.6).</summary>
    public static readonly string[] Tables = ["shared", "phantasie1", "phantasie2", "phantasie3"];

    public const string SourcesFile = "strings/en.sources.json";

    /// <summary>The file a version's resolved lines go to, relative to the table's GameData folder.</summary>
    public static string OutputFile(string profileId) => $"strings/en.{profileId}.json";

    /// <summary>
    /// The remaster's own wording for the lines a version lacks, relative to the table's GameData folder:
    /// <c>GameDataLocator.ImportedStringFiles</c> lays it under every other imported file.
    /// </summary>
    public static string FallbackFile(string profileId) => $"strings/en.fallback.{profileId}.json";

    /// <summary>
    /// A version's lines in its own wording (alternatives marked <c>"wording": "own"</c>), relative to its game's
    /// GameData folder: they hold only for that game, and only while its tables come from that version
    /// (<c>GameDataLocator.ImportedStringFiles</c> checks <c>import.json</c>), so they never reach another game through a
    /// shared or extended table and never cover another version's own lines.
    /// </summary>
    public static string OwnWordingFile(string profileId) => $"strings/en.own.{profileId}.json";

    /// <summary>The shipped fallback file of a game's folder (<c>{"tables": {table: {id: text}}}</c>).</summary>
    public const string ShippedFallbackFile = "strings/fallback.en.json";

    /// <summary>
    /// A version's own shipped fallback file in a game's folder (any tables; s212: <c>phantasie3/strings/fallback.p3-apple2.en.json</c>,
    /// the lines an Apple-only III lacks that DOS III gives): <see cref="WriteFallback"/> merges it, when shipped, after the
    /// game's <see cref="ShippedFallbackFile"/>.
    /// </summary>
    public static string ShippedProfileFallbackFile(string profileId) => $"strings/fallback.{profileId}.en.json";

    /// <summary>
    /// The shipped fallback for the shared and Phantasie I tables (<c>phantasie1/strings/fallback.en.json</c>): every
    /// import without I's tables writes it (s163), with its own game's file.
    /// </summary>
    public const string CommonFallbackGame = "phantasie1";

    /// <summary>
    /// Write the remaster's own wording for the lines the running version lacks, from the shipped fallback files of
    /// <paramref name="games"/> (<see cref="ShippedFallbackFile"/>), merged per table (an id in two files must read the
    /// same; a test keeps every id in one file), one fallback file per table (DIFFERENCES-II D2-050; s163 III and the
    /// common shared / I file).
    /// </summary>
    public static void WriteFallback(ImportContext ctx, params string[] games)
    {
        var merged = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        var read = new List<string>();
        void Merge(JsonObject tables)
        {
            foreach (var (table, ids) in tables)
            {
                if (ids is not JsonObject list)
                    continue;
                if (!merged.TryGetValue(table, out var into))
                    merged[table] = into = new JsonObject();
                foreach (var (id, text) in list)
                    into[id] = text?.DeepClone();
            }
        }
        foreach (var game in games)
        {
            if (ctx.ReadShipped(game, ShippedFallbackFile)?["tables"] is not JsonObject tables)
                throw new InvalidOperationException($"shipped {game}/{ShippedFallbackFile} missing");
            Merge(tables);
            read.Add($"{game}/{ShippedFallbackFile}");
            var own = ShippedProfileFallbackFile(ctx.Profile.Id);
            if (ctx.ReadShipped(game, own)?["tables"] is JsonObject profileTables)
            {
                Merge(profileTables);
                read.Add($"{game}/{own}");
            }
        }
        var from = string.Join(", ", read);
        foreach (var (table, list) in merged)
        {
            if (list.Count == 0)
                continue;
            ctx.WriteGameData(table, FallbackFile(ctx.Profile.Id), new JsonObject
            {
                ["_schema"] = "phantasie.strings/v1",
                ["_note"] = $"The remaster's own wording for lines {ctx.Profile.Title} does not hold (shipped {from}); " +
                            "under every other imported file.",
                ["game"] = table,
                ["language"] = "en",
                ["strings"] = list,
            }, indent: 1);
        }
    }

    /// <summary>
    /// Resolve every id the running version can give from its files and write one file per table that gets any.
    /// <paramref name="extra"/> adds more resolved ids for a table (Phantasie IV's lines that equal the Japanese).
    /// Lines in the version's own wording go, from every table, to <paramref name="ownWordingGame"/>'s
    /// <see cref="OwnWordingFile"/>; null leaves them out (another version's tables won).
    /// <paramref name="sourcesProfile"/> resolves another profile's alternatives from this version's files (a
    /// repacked build whose offsets are the same); the output still goes to this profile's file.
    /// </summary>
    public static void Write(ImportContext ctx, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? extra = null,
        string? ownWordingGame = null, string? sourcesProfile = null)
    {
        var files = new FileCache(ctx.Files);
        var profile = ctx.Profile.Id;
        var from = sourcesProfile ?? profile;
        var tables = Tables.Concat(extra?.Keys ?? []).Distinct();
        var own = new JsonObject();
        foreach (var table in tables)
        {
            var strings = new JsonObject();
            if (ctx.ReadShipped(table, SourcesFile)?["strings"] is JsonObject sources)
                foreach (var (id, alts) in sources)
                {
                    if (alts is not JsonArray list)
                        continue;
                    foreach (var alt in list)
                        if (alt?["from"]?.GetValue<string>() == from && alt["parts"] is JsonArray parts
                            && Resolve(parts, files) is { } text)
                        {
                            if (alt["wording"] is null)
                                strings[id] = text;
                            else if (ownWordingGame is not null)
                                own[id] = text;
                            break;
                        }
                }
            if (extra is not null && extra.TryGetValue(table, out var more))
                foreach (var (id, text) in more)
                    strings[id] = text;
            if (strings.Count == 0)
                continue;
            ctx.WriteGameData(table, OutputFile(profile), new JsonObject
            {
                ["_schema"] = "phantasie.strings/v1",
                ["_note"] = $"Built by the importer from the player's copy ({ctx.Profile.Title}): the game's own lines of this table.",
                ["game"] = table,
                ["language"] = "en",
                ["strings"] = strings,
            }, indent: 1);
        }
        if (ownWordingGame is not null && own.Count > 0)
            ctx.WriteGameData(ownWordingGame, OwnWordingFile(profile), new JsonObject
            {
                ["_schema"] = "phantasie.strings/v1",
                ["_note"] = $"Built by the importer from the player's copy ({ctx.Profile.Title}): the lines it prints in its own " +
                            "wording, from every table; they hold for this game only, while its tables come from this version.",
                ["game"] = ownWordingGame,
                ["language"] = "en",
                ["strings"] = own,
            }, indent: 1);
    }

    /// <summary>The text of one alternative, or null when a referenced file is missing or too short.</summary>
    public static string? Resolve(JsonArray parts, Func<string, byte[]?> file)
    {
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            if (p is JsonValue v && v.TryGetValue(out string? literal))
            {
                sb.Append(literal);
                continue;
            }
            if (p is not JsonArray r || r.Count != 3)
                return null;
            var data = file(r[0]!.GetValue<string>());
            var at = r[1]!.GetValue<int>();
            var len = r[2]!.GetValue<int>();
            if (data is null || at < 0 || len < 0 || at + len > data.Length)
                return null;
            sb.Append(Encoding.Latin1.GetString(data, at, len));
        }
        return sb.ToString();
    }

    private static string? Resolve(JsonArray parts, FileCache files) => Resolve(parts, files.Get);

    /// <summary>
    /// A version's files by sources key: a raw file name, or a decoded image (<c>name:unpacked</c> …;
    /// <c>name:page</c> = an Apple II text-page dump as its 24 rows joined by newlines).
    /// </summary>
    private sealed class FileCache(FileSet set)
    {
        private readonly Dictionary<string, byte[]?> _cache = new(StringComparer.Ordinal);

        public byte[]? Get(string key)
        {
            if (_cache.TryGetValue(key, out var hit))
                return hit;
            byte[]? Raw(string name) => set.Find(name)?.Data;
            var data = key switch
            {
                // The Bonus Edition's PHANTASI.EXE unpacks to the same image (ComImage).
                "PHANTASI.COM:unpacked" => (Raw("PHANTASI.COM") ?? Raw("PHANTASI.EXE")) is { } com ? ComImage.UnpackAny(com) : null,
                "MAIN.EXE:root" => Raw("MAIN.EXE") is { } exe ? P3Exe.UnpackRoot(exe) : null,
                _ when key.EndsWith(":page", StringComparison.Ordinal) =>
                    Raw(key[..^5]) is { } page ? Encoding.Latin1.GetBytes(string.Join("\n", A2Text.TextPage(page))) : null,
                _ => Raw(key),
            };
            _cache[key] = data;
            return data;
        }
    }
}
