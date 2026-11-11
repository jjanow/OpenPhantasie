using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines;

/// <summary>
/// Shipped number cells for an import from a second version (II's <c>apple2-import.json</c> <c>st_cells</c>, I's
/// <c>amiga-import.json</c> <c>dos_cells</c>): each cell is a path into a GameData file the import built, with the
/// copy's value and the reference version's. A cell holding the copy's value takes the reference value; one holding
/// neither is kept and warned.
/// <para>Paths: a string is an object key, a number an array index, an object picks the array element whose fields
/// match. <c>byte</c> = an offset into a hex string; <c>fog</c> = a map tile whose fog (tile + 120) is kept.</para>
/// </summary>
public sealed record CellPatches(string FromKey, string ToKey, string CopyData, string FromValue, string ToValue,
    string ToValueSet)
{
    /// <summary>Apply every cell of <paramref name="cells"/> to <paramref name="built"/>; returns the cells changed.</summary>
    public int Apply(JsonArray cells, IReadOnlyDictionary<string, JsonObject> built, List<string> warnings)
    {
        var changed = 0;
        foreach (var cell in cells)
        {
            var file = (string)cell!["file"]!;
            var path = cell["path"]!.AsArray();
            var where = $"{file} {Describe(path)}";
            if (!built.TryGetValue(file, out var root) || Walk(root, path) is not { } leaf)
            {
                warnings.Add($"{where}: not found in {CopyData}; {ToValueSet} could not be set.");
                continue;
            }
            var (parent, key) = leaf;
            var from = cell[FromKey];
            var to = cell[ToKey];
            if (cell["byte"] is { } byteNode)
            {
                var at = (int)byteNode;
                var bytes = Convert.FromHexString((string)Get(parent, key)!);
                if (bytes[at] == (int)to!)
                    continue;
                if (bytes[at] != (int)from!)
                {
                    warnings.Add($"{where} byte {at}: {bytes[at]} is neither {FromValue} {(int)from!} nor {ToValue} {(int)to!}; kept.");
                    continue;
                }
                bytes[at] = (byte)(int)to!;
                Set(parent, key, Convert.ToHexStringLower(bytes));
                changed++;
                continue;
            }
            var now = Get(parent, key);
            if (cell["fog"] is JsonValue fog && fog.GetValue<bool>())
            {
                var t = (int)now!;
                var fogged = t is >= 120 and < 255;
                var tile = fogged ? t - 120 : t;
                if (tile == (int)to!)
                    continue;
                if (tile != (int)from!)
                {
                    warnings.Add($"{where}: tile {tile} is neither {FromValue} {(int)from!} nor {ToValue} {(int)to!}; kept.");
                    continue;
                }
                Set(parent, key, (int)to! + (fogged ? 120 : 0));
                changed++;
                continue;
            }
            if (JsonNode.DeepEquals(now, to))
                continue;
            if (!JsonNode.DeepEquals(now, from))
            {
                warnings.Add($"{where}: {now?.ToJsonString()} is neither {FromValue} {from?.ToJsonString()} nor {ToValue} {to?.ToJsonString()}; kept.");
                continue;
            }
            Set(parent, key, to?.DeepClone());
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
    public static (JsonNode Parent, JsonNode Key)? Walk(JsonNode root, JsonArray path)
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

    public static JsonNode? Get(JsonNode node, JsonNode step) => (node, step.GetValueKind()) switch
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
}
