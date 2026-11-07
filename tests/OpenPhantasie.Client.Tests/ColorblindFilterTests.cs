using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// PLAN P3 QoL colorblind palette (<see cref="GameSettings.ColorblindPalette"/>, <see cref="ColorblindFilter"/>): off
/// leaves every pixel as drawn; on, the status/HP and map colours stay apart under a deuteranopia and a protanopia
/// simulation (Brettel/Viénot LMS, as used by the common daltonize tools).
/// </summary>
public class ColorblindFilterTests
{
    // The remaster's own status and map colours (UiOverlays.HpColor / Accent, Game1.TileColor, the auto-map) and the
    // Amiga combat palette.
    private static readonly Color[] Hp = [new(120, 200, 140), new(220, 160, 60), new(200, 70, 70)];

    private static readonly Color[] Tiles =
    [
        new(180, 140, 60), new(100, 40, 40), new(30, 60, 140), new(30, 30, 35),
        .. new[] { 32, 40, 44, 52, 56, 64, 68, 76 }.Select(r => new Color(r, 90, 45)),
    ];

    private static readonly Color[] AutoMap = [new(70, 90, 70), new(40, 40, 50), Color.Cyan];

    private static readonly Color[] Marker = [Color.Red, Color.Gray, Color.Black];

    private static readonly Color[] Amiga =
    [
        .. new[]
        {
            // The combat table 0x194BC as the Amiga shows it (LoadRGB4, c x 17; the sprite PNGs since s125).
            "#000000", "#AA4422", "#EE0000", "#EE6600", "#EEAA00", "#EEEE00", "#00EE00", "#008800", "#888844", "#00EEEE",
            "#00AAEE", "#0044EE", "#EEBBBB", "#888888", "#EE22AA", "#EEEEEE",
        }.Select(VirtualScreen.ParseColor),
    ];

    /// <summary>Every colour of the Amiga map art (overworld regions and icons, dungeon glyphs; extracted PNGs, x17 since s127).</summary>
    private static readonly Color[] AmigaMap =
    [
        .. new[]
        {
            "#000000", "#0088AA", "#0088BB", "#2222EE", "#2266AA", "#228800", "#446600", "#44EE22", "#666666", "#888888",
            "#AA4400", "#EE0000", "#EE8800", "#EEBB00", "#EEEEEE",
        }.Select(VirtualScreen.ParseColor),
    ];

    private static Color[] RandomFrame(int seed)
    {
        var rng = new Random(seed);
        var frame = new Color[320 * 200];
        for (var i = 0; i < frame.Length; i++)
            frame[i] = new Color(rng.Next(256), rng.Next(256), rng.Next(256));
        // Include every colour the table remaps, so "off" is tested on the colours "on" would change.
        var k = 0;
        foreach (var c in ColorblindFilter.Load().Entries.Keys)
            frame[k++] = c;
        return frame;
    }

    [Fact]
    public void Off_IsNoFilter_AndTheFrameIsPixelIdentical()
    {
        Assert.False(new GameSettings().ColorblindPalette); // off by default
        Assert.Null(ColorblindFilter.For(false));
        Assert.False(VirtualScreen.FiltersFrame(null, null)); // the render target itself is presented: no copy at all

        var frame = RandomFrame(7);
        var before = frame.ToArray();
        VirtualScreen.FilterFrame(frame, null, ColorblindFilter.For(false));
        Assert.Equal(before, frame);
    }

    [Fact]
    public void Off_WithAPackPalette_IsExactlyThePalettePass()
    {
        var palette = new ScreenPalette([Color.Black, Color.White, Color.Cyan, Color.Magenta]);
        var a = RandomFrame(3);
        var b = a.ToArray();
        VirtualScreen.FilterFrame(a, palette, null);
        palette.Apply(b);
        Assert.Equal(b, a);
    }

    [Fact]
    public void On_RemapsOnlyTheListedColours()
    {
        var filter = ColorblindFilter.For(true)!;
        Assert.True(VirtualScreen.FiltersFrame(null, filter));
        Assert.NotEmpty(filter.Entries);
        Assert.Equal(new Color(86, 180, 233), filter.Map(new Color(120, 200, 140))); // healthy HP → sky blue
        Assert.Equal(new Color(240, 228, 66), filter.Map(new Color(220, 160, 60)));  // wounded → yellow
        Assert.Equal(new Color(213, 94, 0), filter.Map(new Color(200, 70, 70)));     // dead → vermilion

        var frame = RandomFrame(11);
        var before = frame.ToArray();
        filter.Apply(frame);
        for (var i = 0; i < frame.Length; i++)
            Assert.Equal(filter.Entries.TryGetValue(new Color(before[i].R, before[i].G, before[i].B), out var to) ? to : before[i], frame[i]);
    }

    public static TheoryData<string> Groups => new() { "hp", "tiles", "automap", "marker", "amiga", "amiga-map" };

    [Theory]
    [MemberData(nameof(Groups))]
    public void On_KeepsTheColoursApart_UnderRedGreenBlindness(string group)
    {
        var colours = group switch
        {
            "hp" => Hp,
            "tiles" => Tiles,
            "automap" => AutoMap,
            "marker" => Marker,
            "amiga-map" => AmigaMap,
            _ => Amiga,
        };
        var filter = ColorblindFilter.For(true)!;
        const double apart = 25; // RGB distance; the original pairs tested differ by at least this much to normal vision
        foreach (var protan in new[] { false, true })
            for (var i = 0; i < colours.Length; i++)
                for (var j = i + 1; j < colours.Length; j++)
                {
                    var (a, b) = (colours[i], colours[j]);
                    if (Distance(a, b) < apart)
                        continue;
                    var seen = Distance(Simulate(filter.Map(a), protan), Simulate(filter.Map(b), protan));
                    Assert.True(seen >= apart,
                        $"{(protan ? "protanopia" : "deuteranopia")}: {a} vs {b} look {seen:0.0} apart with the filter");
                }
    }

    [Fact]
    public void On_HelpsWhereTheOriginalColoursRunTogether()
    {
        // The map fallback's dungeon (dark red) and grass (dark green) are the classic red-green pair.
        var filter = ColorblindFilter.For(true)!;
        var dungeon = new Color(100, 40, 40);
        var grass = new Color(40, 90, 45);
        Assert.True(Distance(Simulate(dungeon, false), Simulate(grass, false)) < 30);
        Assert.True(Distance(Simulate(filter.Map(dungeon), false), Simulate(filter.Map(grass), false)) > 50);
    }

    [Fact]
    public void OptionsRow_TogglesTheGameSetting()
    {
        var settings = new ClientSettings();
        var o = new OptionsScreen(settings, _ => [("amiga", "AMIGA")], _ => true);
        o.Open();
        o.HandleKeys([Keys.Right]); // tab I
        for (var k = 0; k < 20 && !o.Lines().Any(l => l.Text.StartsWith(">COLORBLIND PALETTE")); k++)
            o.HandleKeys([Keys.Down]);
        Assert.EndsWith("OFF", o.Lines().Single(l => l.Text.StartsWith(">COLORBLIND PALETTE")).Text);
        o.HandleKeys([Keys.Enter]);
        Assert.True(settings.Game(GameId.P1).ColorblindPalette);
        Assert.False(settings.Game(GameId.P2).ColorblindPalette); // per game
    }

    private static double Distance(Color a, Color b) => Distance((a.R, a.G, a.B), (b.R, b.G, b.B));

    private static double Distance((double R, double G, double B) a, (double R, double G, double B) b) =>
        Math.Sqrt((a.R - b.R) * (a.R - b.R) + (a.G - b.G) * (a.G - b.G) + (a.B - b.B) * (a.B - b.B));

    /// <summary>How <paramref name="c"/> looks to a dichromat (LMS projection; protanopia or deuteranopia), clamped to 0–255.</summary>
    private static (double R, double G, double B) Simulate(Color c, bool protan)
    {
        double r = c.R, g = c.G, b = c.B;
        var l = 17.8824 * r + 43.5161 * g + 4.11935 * b;
        var m = 3.45565 * r + 27.1554 * g + 3.86714 * b;
        var s = 0.0299566 * r + 0.184309 * g + 1.46709 * b;
        if (protan)
            l = 2.02344 * m - 2.52581 * s;
        else
            m = 0.494207 * l + 1.24827 * s;
        static double C(double x) => Math.Clamp(x, 0, 255);
        return (C(0.0809444479 * l - 0.130504409 * m + 0.116721066 * s),
            C(-0.0102485335 * l + 0.0540193266 * m - 0.113614708 * s),
            C(-0.000365296938 * l - 0.00412161469 * m + 0.693511405 * s));
    }
}
