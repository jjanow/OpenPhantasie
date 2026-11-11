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
