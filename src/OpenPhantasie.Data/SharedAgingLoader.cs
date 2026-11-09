using System.Text.Json;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Reads <c>GameData/shared/aging.json</c>: the shared aging model of QoL <c>shared-aging</c>
/// (<see cref="SharedAgingModel"/>, <c>docs/plan/13-aging-normalised.md</c>). Every game's creation tables and IV's tables
/// carry it.
/// </summary>
public static class SharedAgingLoader
{
    public const string FileName = "aging.json";

    private static readonly string[] RaceOrder =
    [
        "HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE",
        "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC", "GOBLIN", "LIZ.MAN", "MINOTAUR",
    ];

    private static readonly Lock Gate = new();
    private static SharedAgingModel? _shipped;

    /// <summary>The shipped model (read once), or null when the file is missing.</summary>
    public static SharedAgingModel? Shipped
    {
        get
        {
            lock (Gate)
            {
                if (_shipped is null)
                {
                    var path = Path.Combine(GameDataRepository.SharedDataPath, FileName);
                    if (File.Exists(path))
                        _shipped = Load(path);
                }
                return _shipped;
            }
        }
    }

    public static SharedAgingModel Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;
        var bands = root.GetProperty("bands_years");
        var byRace = RaceOrder.Select(r => Band(bands, r)).ToList();
        var luckMin = new Dictionary<GameId, int>();
        foreach (var p in root.GetProperty("luck_min").EnumerateObject().Where(p => !p.Name.StartsWith('_')))
        {
            var game = Enum.GetValues<GameId>().FirstOrDefault(g => g.Prefix() == p.Name);
            if (game == default)
                throw new InvalidDataException($"{FileName}: luck_min names no game '{p.Name}'.");
            luckMin[game] = p.Value.GetInt32();
        }
        var steps = root.GetProperty("luck_steps").GetInt32();
        if (steps <= 0)
            throw new InvalidDataException($"{FileName}: luck_steps must be above 0.");
        return new SharedAgingModel(byRace, Band(bands, "other"), luckMin, steps);
    }

    private static (int Lo, int Hi) Band(JsonElement bands, string race)
    {
        if (!bands.TryGetProperty(race, out var b) || b.GetArrayLength() != 2)
            throw new InvalidDataException($"{FileName}: bands_years needs [lo, hi] for {race}.");
        var (lo, hi) = (b[0].GetInt32(), b[1].GetInt32());
        if (lo <= 0 || hi < lo)
            throw new InvalidDataException($"{FileName}: bad band for {race}.");
        return (lo, hi);
    }
}
