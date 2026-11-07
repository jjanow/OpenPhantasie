using System.Text.Json;
using Microsoft.Xna.Framework;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// The colorblind palette (PLAN P3 QoL, presentation option <see cref="GameSettings.ColorblindPalette"/>; off = the original
/// colours): an exact-colour remap of the finished frame, read from <c>GameData/shared/colorblind.json</c>
/// (<c>red-green</c>: deuteranopia and protanopia). The status/HP colours, the map colours and the confusable Amiga palette
/// entries move to an Okabe-Ito set; every colour the table does not list is left as drawn. Applied after the pack's
/// <see cref="ScreenPalette"/> in <see cref="VirtualScreen"/>. With the option off there is no filter at all, so the
/// frame is presented exactly as before.
/// </summary>
public sealed class ColorblindFilter
{
    /// <summary>The table's key in <c>colorblind.json</c>.</summary>
    public const string RedGreen = "red-green";

    private readonly Dictionary<uint, Color> _map;

    public ColorblindFilter(IReadOnlyDictionary<Color, Color> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map.ToDictionary(kv => Key(kv.Key), kv => Opaque(kv.Value));
    }

    /// <summary>Source colour → replacement (opaque), for tests and tools.</summary>
    public IReadOnlyDictionary<Color, Color> Entries =>
        _map.ToDictionary(kv => new Color(kv.Key | 0xff000000u), kv => kv.Value);

    /// <summary>The filter for <paramref name="on"/>: null when off (nothing is remapped), else the shipped table.</summary>
    public static ColorblindFilter? For(bool on, string? path = null) => on ? Load(path) : null;

    /// <summary>Read <c>colorblind.json</c> (default: <c>GameData/shared</c>); a missing or bad file gives an empty table.</summary>
    public static ColorblindFilter Load(string? path = null)
    {
        path ??= Path.Combine(GameDataRepository.SharedDataPath, "colorblind.json");
        var map = new Dictionary<Color, Color>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty(RedGreen, out var table))
                foreach (var entry in table.EnumerateObject())
                    map[VirtualScreen.ParseColor(entry.Name)] = VirtualScreen.ParseColor(entry.Value.GetString() ?? "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            map.Clear();
        }
        return new ColorblindFilter(map);
    }

    /// <summary>The colour <paramref name="c"/> is shown as (alpha ignored; an unlisted colour comes back unchanged).</summary>
    public Color Map(Color c) => _map.TryGetValue(Key(c), out var to) ? to : c;

    /// <summary>Remap every pixel of <paramref name="pixels"/> in place.</summary>
    public void Apply(Span<Color> pixels)
    {
        if (_map.Count == 0)
            return;
        for (var i = 0; i < pixels.Length; i++)
            if (_map.TryGetValue(Key(pixels[i]), out var to))
                pixels[i] = to;
    }

    private static uint Key(Color c) => c.PackedValue | 0xff000000u;

    private static Color Opaque(Color c) => new(c.R, c.G, c.B, (byte)255);
}
