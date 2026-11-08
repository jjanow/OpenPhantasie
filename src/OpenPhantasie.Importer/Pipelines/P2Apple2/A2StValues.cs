using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenPhantasie.Importer.Pipelines.P2Apple2;

/// <summary>
/// The user's s159 decisions for an Apple-only II (DIFFERENCES-II D2-046..D2-050), from the shipped remaster data
/// <c>GameData/phantasie2/apple2-import.json</c> (numbers, the compass word map and the manual's spell names): the ST's
/// cells over the Apple's, the scroll compass turned to the ST orientation, spell names by slot.
/// </summary>
internal sealed class A2StValues
{
    public const string File = "apple2-import.json";

    private readonly JsonObject _doc;
    private readonly Regex _compass;
    private readonly Dictionary<string, string> _compassMap;

    private A2StValues(JsonObject doc)
    {
        _doc = doc;
        _compassMap = (doc["scroll_compass"]?.AsObject() ?? throw new InvalidDataException($"{File}: scroll_compass missing"))
            .ToDictionary(kv => kv.Key, kv => (string)kv.Value!, StringComparer.Ordinal);
        // At the start of a word (SOUTH-, NORTHERN), never inside one (BEAST).
        _compass = new Regex(@"(?<![A-Za-z])(" + string.Join("|", _compassMap.Keys.Select(Regex.Escape)) + ")",
            RegexOptions.CultureInvariant);
    }

    public static A2StValues Load(ImportContext ctx) =>
        Parse(ctx.ReadShipped(P2Apple2Pipeline.GameId, File) ?? throw new InvalidOperationException($"shipped phantasie2/{File} missing"));

    public static A2StValues Parse(JsonNode doc) => new(doc.AsObject());

    /// <summary>Spell names by the ST table slot (the manual's list; remaster wording for the divine spells).</summary>
    public IReadOnlyDictionary<int, string> SpellNames =>
        _doc["spell_names"]!.AsArray().ToDictionary(s => (int)s!["slot"]!, s => (string)s!["name"]!);

    // ---- cells ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// Put the ST value into every listed cell of the built files that holds the Apple value (a fogged map tile keeps
    /// its fog). A cell holding neither is left and named in <paramref name="warnings"/>. Returns the cells changed.
    /// </summary>
    public int ApplyCells(IReadOnlyDictionary<string, JsonObject> built, List<string> warnings)
    {
        var changed = 0;
        foreach (var cell in _doc["st_cells"]!.AsArray())
        {
            var file = (string)cell!["file"]!;
            var path = cell["path"]!.AsArray();
            var where = $"{file} {Describe(path)}";
            if (!built.TryGetValue(file, out var root) || Walk(root, path) is not { } leaf)
            {
                warnings.Add($"{where}: not found in the Apple copy's data; the Atari ST value could not be set.");
                continue;
            }
            var (parent, key) = leaf;
            var apple = cell["apple"];
            var st = cell["st"];
            if (cell["byte"] is { } byteNode)
            {
                var at = (int)byteNode;
                var bytes = Convert.FromHexString((string)Get(parent, key)!);
                if (bytes[at] == (int)st!)
                    continue;
                if (bytes[at] != (int)apple!)
                {
                    warnings.Add($"{where} byte {at}: {bytes[at]} is neither the Apple's {(int)apple!} nor the Atari ST's {(int)st!}; kept.");
                    continue;
                }
                bytes[at] = (byte)(int)st!;
                Set(parent, key, P2St.Py.Hex(bytes));
                changed++;
                continue;
            }
            var now = Get(parent, key);
            if (cell["fog"] is JsonValue fog && fog.GetValue<bool>())
            {
                var t = (int)now!;
                var fogged = t is >= 120 and < 255;
                var tile = fogged ? t - 120 : t;
                if (tile == (int)st!)
                    continue;
                if (tile != (int)apple!)
                {
                    warnings.Add($"{where}: tile {tile} is neither the Apple's {(int)apple!} nor the Atari ST's {(int)st!}; kept.");
                    continue;
                }
                Set(parent, key, (int)st! + (fogged ? 120 : 0));
                changed++;
                continue;
            }
            if (JsonNode.DeepEquals(now, st))
                continue;
            if (!JsonNode.DeepEquals(now, apple))
            {
                warnings.Add($"{where}: {now?.ToJsonString()} is neither the Apple's {apple?.ToJsonString()} nor the Atari ST's {st?.ToJsonString()}; kept.");
                continue;
            }
            Set(parent, key, st?.DeepClone());
            changed++;
        }
        return changed;
    }

    private static string Describe(JsonArray path) => string.Join("/", path.Select(p => p switch
    {
        JsonObject o => "{" + string.Join(", ", o.Select(kv => $"{kv.Key} {kv.Value}")) + "}",
        _ => p!.ToString(),
    }));

    /// <summary>The container and key of the path's last step, or null when a step is missing.</summary>
    private static (JsonNode Parent, JsonNode Key)? Walk(JsonNode root, JsonArray path)
    {
        JsonNode? node = root;
        for (var i = 0; i < path.Count; i++)
        {
            var step = path[i]!;
            if (node is null)
                return null;
            if (i == path.Count - 1)
                return step is not JsonObject && Has(node, step) ? (node, step) : null;
            node = step is JsonObject match ? Match(node, match) : Get(node, step);
        }
        return null;
    }

    private static JsonNode? Match(JsonNode node, JsonObject match) =>
        node is JsonArray a
            ? a.FirstOrDefault(e => e is JsonObject o && match.All(kv => JsonNode.DeepEquals(o[kv.Key], kv.Value)))
            : null;

    private static bool Has(JsonNode node, JsonNode step) => (node, step.GetValueKind()) switch
    {
        (JsonObject o, System.Text.Json.JsonValueKind.String) => o.ContainsKey((string)step!),
        (JsonArray a, System.Text.Json.JsonValueKind.Number) => (int)step < a.Count,
        _ => false,
    };

    private static JsonNode? Get(JsonNode node, JsonNode step) => (node, step.GetValueKind()) switch
    {
        (JsonObject o, System.Text.Json.JsonValueKind.String) => o[(string)step!],
        (JsonArray a, System.Text.Json.JsonValueKind.Number) when (int)step >= 0 && (int)step < a.Count => a[(int)step],
        _ => null,
    };

    private static void Set(JsonNode node, JsonNode step, JsonNode? value)
    {
        if (node is JsonObject o)
            o[(string)step!] = value;
        else
            ((JsonArray)node)[(int)step] = value;
    }

    // ---- scroll compass -------------------------------------------------------------------------------------------

    /// <summary>
    /// One scroll line with its compass words turned to the ST orientation, all at once. A boxed line ("! ... !") keeps
    /// its closing ! in the same column where the run of spaces before it allows.
    /// </summary>
    public string TurnCompass(string line)
    {
        var turned = _compass.Replace(line, m => _compassMap[m.Value]);
        var delta = turned.Length - line.Length;
        if (delta == 0 || !turned.EndsWith('!'))
            return turned;
        var body = turned[..^1];
        var trimmed = body.TrimEnd(' ');
        var spaces = body.Length - trimmed.Length;
        var want = spaces - delta;
        if (want < 1)
            return turned; // no room: the box edge moves (never cut text)
        return trimmed + new string(' ', want) + "!";
    }

    /// <summary>The compass words a text holds, in order (for checks).</summary>
    public IEnumerable<string> CompassWords(string text) => _compass.Matches(text).Select(m => m.Value);
}
