using Microsoft.Xna.Framework;

namespace OpenPhantasie.Client;

/// <summary>
/// A graphics pack's palette applied to the finished frame (T1.5): every pixel becomes the nearest palette colour
/// (squared RGB distance; the first entry wins a tie), so a 4-colour CGA pack shows only its 4 colours. A pack without a
/// palette (Amiga) gets none and its frame is presented untouched.
/// </summary>
public sealed class ScreenPalette
{
    private readonly Color[] _colours;
    private readonly Dictionary<uint, Color> _nearest = new();

    public ScreenPalette(IReadOnlyList<Color> colours)
    {
        if (colours.Count == 0)
            throw new ArgumentException("A palette needs at least one colour.", nameof(colours));
        _colours = [.. colours];
    }

    public IReadOnlyList<Color> Colours => _colours;

    /// <summary>The pack's <c>#RRGGBB</c> palette, or null when it declares none.</summary>
    public static ScreenPalette? From(IReadOnlyList<string>? hex) =>
        hex is null || hex.Count == 0 ? null : new ScreenPalette([.. hex.Select(VirtualScreen.ParseColor)]);

    /// <summary>The palette colour nearest <paramref name="c"/> (opaque; alpha is not compared).</summary>
    public Color Nearest(Color c)
    {
        var key = c.PackedValue | 0xff000000u;
        if (_nearest.TryGetValue(key, out var hit))
            return hit;
        var best = _colours[0];
        var bestD = int.MaxValue;
        foreach (var p in _colours)
        {
            int dr = c.R - p.R, dg = c.G - p.G, db = c.B - p.B;
            var d = dr * dr + dg * dg + db * db;
            if (d < bestD)
            {
                bestD = d;
                best = p;
            }
        }
        best.A = 255;
        if (_nearest.Count < 65536)
            _nearest[key] = best;
        return best;
    }

    /// <summary>Map every pixel of <paramref name="pixels"/> to its nearest palette colour.</summary>
    public void Apply(Span<Color> pixels)
    {
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = Nearest(pixels[i]);
    }
}
