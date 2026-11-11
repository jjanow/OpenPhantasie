using System.Text.Json;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>Campaign spell ids for the unified menu's shortcuts, from shared/town-menu.json.</summary>
public static class TownMenuData
{
    private static readonly Lazy<Dictionary<string, Dictionary<string, int[]>>> Data = new(Load);

    private static Dictionary<string, Dictionary<string, int[]>> Load()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(GameDataRepository.SharedDataPath, "town-menu.json")));
        return doc.RootElement.EnumerateObject().Where(p => !p.Name.StartsWith('_')).ToDictionary(p => p.Name,
            p => p.Value.EnumerateObject().ToDictionary(c => c.Name,
                c => c.Value.EnumerateArray().Select(n => n.GetInt32()).ToArray()));
    }

    public static IReadOnlySet<int> Spells(GameId game, string command) =>
        Data.Value.TryGetValue(game.Prefix(), out var commands) && commands.TryGetValue(command, out var ids)
            ? ids.ToHashSet() : new HashSet<int>();
}
