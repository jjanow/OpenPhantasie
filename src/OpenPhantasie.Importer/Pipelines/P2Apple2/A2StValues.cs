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

    private static readonly CellPatches StCells =
        new("apple", "st", "the Apple copy's data", "the Apple's", "the Atari ST's", "the Atari ST value");

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
    public int ApplyCells(IReadOnlyDictionary<string, JsonObject> built, List<string> warnings) =>
        StCells.Apply(_doc["st_cells"]!.AsArray(), built, warnings);

    // ---- start fog (D2-052) ---------------------------------------------------------------------------------------

    private const int Fog = 120;

    /// <summary>
    /// D2-052 (user s209): the shipped start fog over a played copy's grids (<c>start_fog</c>: OUT1 / OUT2 open, the rest
    /// fogged; the ST's <c>MAPS.INT</c>, which the Memorial Set's unplayed Apple disk equals). Every cell of an open region
    /// loses its fog, every cell of a fogged region gets it (fog = tile + 120). A region the list or the grids lack is
    /// named in <paramref name="warnings"/>. Returns the cells changed.
    /// </summary>
    public int ApplyStartFog(JsonObject overworld, List<string> warnings)
    {
        var fog = _doc["start_fog"]?.AsObject() ?? throw new InvalidDataException($"{File}: start_fog missing");
        var regions = overworld["regions"]!.AsObject();
        var listed = new HashSet<string>(StringComparer.Ordinal);
        var changed = 0;
        foreach (var (kind, fogged) in new[] { ("open", false), ("fogged", true) })
            foreach (var name in fog[kind]!.AsArray().Select(n => (string)n!))
            {
                listed.Add(name);
                if (regions[name]?["tiles"] is not JsonArray rows)
                {
                    warnings.Add($"{File}: start fog region {name} is not in the Apple copy's data; left as it is.");
                    continue;
                }
                foreach (var row in rows.Select(r => r!.AsArray()))
                    for (var x = 0; x < row.Count; x++)
                    {
                        var t = (int)row[x]!;
                        var isFogged = t is >= Fog and < 255;
                        if (isFogged == fogged || (fogged && t + Fog >= 255))
                            continue;
                        row[x] = fogged ? t + Fog : t - Fog;
                        changed++;
                    }
            }
        foreach (var (name, _) in regions)
            if (!listed.Contains(name))
                warnings.Add($"{File}: region {name} has no start fog; its fog is left as played.");
        return changed;
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
