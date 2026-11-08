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
    private readonly Color[] _shown;
    private readonly Dictionary<uint, Color> _nearest = new();

    public ScreenPalette(IReadOnlyList<Color> colours) : this(colours, colours)
    {
    }

    /// <summary>
    /// s169: a palette whose nearest colour i shows as <paramref name="shown"/>[i] (III DOS: the frame drawn in CGA palette
    /// 0's colours, shown in palette 1 on the screens DOS selects it for).
    /// </summary>
    private ScreenPalette(IReadOnlyList<Color> colours, IReadOnlyList<Color> shown)
    {
        if (colours.Count == 0)
            throw new ArgumentException("A palette needs at least one colour.", nameof(colours));
        if (shown.Count != colours.Count)
            throw new ArgumentException("The shown palette needs as many colours.", nameof(shown));
        _colours = [.. colours];
        _shown = [.. shown];
    }

    /// <summary>
    /// s169: pixels snap to the nearest colour of <paramref name="cut"/> and show that index's colour of
    /// <paramref name="shown"/> (CGA's palette select: the same pixel values, another set of colours).
    /// </summary>
    public static ScreenPalette Mapped(IReadOnlyList<string> cut, IReadOnlyList<string> shown) =>
        new([.. cut.Select(VirtualScreen.ParseColor)], [.. shown.Select(VirtualScreen.ParseColor)]);

    public IReadOnlyList<Color> Colours => _shown;

    /// <summary>The pack's <c>#RRGGBB</c> palette, or null when it declares none.</summary>
    public static ScreenPalette? From(IReadOnlyList<string>? hex) =>
        hex is null || hex.Count == 0 ? null : new ScreenPalette([.. hex.Select(VirtualScreen.ParseColor)]);

    /// <summary>The palette colour nearest <paramref name="c"/> (opaque; alpha is not compared), as shown.</summary>
    public Color Nearest(Color c)
    {
        var key = c.PackedValue | 0xff000000u;
        if (_nearest.TryGetValue(key, out var hit))
            return hit;
        var best = _shown[0];
        var bestD = int.MaxValue;
        for (var i = 0; i < _colours.Length; i++)
        {
            var p = _colours[i];
            int dr = c.R - p.R, dg = c.G - p.G, db = c.B - p.B;
            var d = dr * dr + dg * dg + db * db;
            if (d < bestD)
            {
                bestD = d;
                best = _shown[i];
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
