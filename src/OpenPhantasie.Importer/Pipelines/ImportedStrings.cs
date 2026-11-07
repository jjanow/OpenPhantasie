using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P1Dos;
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
    /// Resolve every id the running version can give from its files and write one file per table that gets any.
    /// <paramref name="extra"/> adds more resolved ids for a table (Phantasie IV's lines that equal the Japanese).
    /// </summary>
    public static void Write(ImportContext ctx, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? extra = null)
    {
        var files = new FileCache(ctx.Files);
        var profile = ctx.Profile.Id;
        var tables = Tables.Concat(extra?.Keys ?? []).Distinct();
        foreach (var table in tables)
        {
            var strings = new JsonObject();
            if (ctx.ReadShipped(table, SourcesFile)?["strings"] is JsonObject sources)
                foreach (var (id, alts) in sources)
                {
                    if (alts is not JsonArray list)
                        continue;
                    foreach (var alt in list)
                        if (alt?["from"]?.GetValue<string>() == profile && alt["parts"] is JsonArray parts
                            && Resolve(parts, files) is { } text)
                        {
                            strings[id] = text;
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

    /// <summary>A version's files by sources key: a raw file name, or a decoded image (<c>name:unpacked</c> …).</summary>
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
                "PHANTASI.COM:unpacked" => Raw("PHANTASI.COM") is { } com ? ComImage.Unpack(com) : null,
                "MAIN.EXE:root" => Raw("MAIN.EXE") is { } exe ? P3Exe.UnpackRoot(exe) : null,
                _ => Raw(key),
            };
            _cache[key] = data;
            return data;
        }
    }
}
