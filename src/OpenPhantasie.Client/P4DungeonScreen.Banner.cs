using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// The town name on the street picture's red banner (s136; <c>logic-specs/towns.md</c> section 1, "The street's banner"):
/// <c>_tload</c> writes it in P4.X font 0 bold (<c>_font(0, 1, 15, 16)</c>), colour 15, at y 8 of the street window
/// (= the picture's origin). Maps below 13: <c>_wkprcen(32, 136, 8, name)</c> then <c>_wkprint("の町")</c> straight after;
/// maps 13–21: the name centred on x 156; after the ending (<c>_tload(1)</c>): "Congraturation!" centred on x 156.
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>The pack's font role for the banner (P4.X font 0, style 1 = bold).</summary>
    public const string BannerFontRole = "p4.street.banner";

    /// <summary>The banner's line: y in the street window, the centres <c>_tload</c> passes, the colour.</summary>
    public const int BannerY = 8, BannerCentreX = 156, BannerSuffixShift = 20, BannerColour = 15;

    /// <summary>
    /// What <c>_tload</c> writes on the banner and where it starts (pack pixels from the picture's origin):
    /// <paramref name="plainWidth"/> is <c>_fsize</c>'s width (the font's plain advances: the bold pass widens what is drawn,
    /// not what is centred; <c>_wprcen</c> / <c>_wkprcen</c> take x − width / 2 with an arithmetic shift). The suffix of maps
    /// below 13 is not centred with the name: it is printed at the pen after it, so the name alone is centred on 136.
    /// Null when there is nothing to write (no town).
    /// </summary>
    public static (string Text, int X)? StreetBanner(P4TownSite? site, bool ending, Func<string, int> plainWidth)
    {
        if (ending)
        {
            var congrats = GameText.S("p4x.0x3fa55");
            return (congrats, BannerCentreX - (plainWidth(congrats) >> 1));
        }
        if (site is null)
            return null;
        var small = site.Id < 13;
        var x = BannerCentreX - (small ? BannerSuffixShift : 0) - (plainWidth(site.Name) >> 1);
        return (small ? site.Name + GameText.S("p4x.0x3fa50") : site.Name, x);
    }

    /// <summary>
    /// <c>_fsize</c>'s width of <paramref name="text"/> in a bold role font: each glyph the original's font has is drawn one
    /// pixel wider than it counts (the bold pass, advance + 1); glyphs the stand-in font gives count as drawn.
    /// </summary>
    public static int BoldPlainWidth(string text, IFontSource font)
    {
        var w = 0;
        foreach (var r in text.EnumerateRunes())
        {
            w += font.Glyph(r.Value).Advance;
            if (OwnGlyph(font, r.Value))
                w--;
        }
        return w;
    }

    private static bool OwnGlyph(IFontSource font, int codePoint) => font switch
    {
        FallbackFont f => f.Primary.Has(codePoint),
        BitmapFont b => b.Has(codePoint),
        _ => false,
    };

    /// <summary>True when the scene now up is a street picture <c>_tload</c> writes the banner on.</summary>
    private bool BannerScene => SceneName is { } s && (s.StartsWith("p4.street.", StringComparison.Ordinal) || s == EndingStreetScene);

    public const string EndingStreetScene = "p4.ending.street";

    /// <summary>
    /// The banner over the street picture at <paramref name="origin"/> (<paramref name="k"/> layout pixels per pack pixel):
    /// in the pack's <see cref="BannerFontRole"/> font, in pack pixels like the picture; without that font, the remaster's
    /// font centred on the same x (layout pixels).
    /// </summary>
    private void DrawStreetBanner(SpriteBatch batch, Texture2D pixel, Vector2 origin, float k)
    {
        if (!BannerScene || Assets is null)
            return;
        var colour = Assets.PaletteColour("pkh", BannerColour) ?? Color.White;
        var ending = SceneName == EndingStreetScene;
        if (FontSources.ForRole(Assets.Pack, BannerFontRole) is { } font)
        {
            if (StreetBanner(W.Site, ending, t => BoldPlainWidth(t, font)) is { } line)
                PixelFont.DrawScaled(batch, pixel, origin + new Vector2(line.X * k, BannerY * k), line.Text, colour, k, font);
            return;
        }
        if (StreetBanner(W.Site, ending, t => (int)Math.Round(PixelFont.Measure(t) / k)) is { } plain)
            PixelFont.Draw(batch, pixel, (int)Math.Round(origin.X + plain.X * k), (int)Math.Round(origin.Y + BannerY * k),
                plain.Text, colour);
    }
}
