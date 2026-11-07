using Microsoft.Xna.Framework;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// T1.5 seams: the font source a pack picks (glyphs and widths by code point), the pack palette applied to the frame,
/// and the pack resolution / pixel aspect over the one shared 320×200 layout. The Amiga pack must draw as before.
/// </summary>
public class GraphicsPackSeamTests
{
    private static readonly GraphicsPackCatalog Catalog = GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path);
    private static GraphicsPack Amiga => Catalog.Find("amiga")!;
    private static GraphicsPack Cga => Catalog.Find("dos-cga")!;
    private static IFontSource Default => PixelFont.Remaster5x7;

    /// <summary>A variable-width font with a Japanese glyph: あ is 7 wide (advance 8), i is 1 wide (advance 2).</summary>
    private const string TestFontJson = """
        {
          "id": "test-var", "height": 3, "line_height": 5, "space_advance": 3, "fallback": "?",
          "glyphs": {
            "?": { "rows": ["###", "..#", ".#."] },
            "i": { "rows": ["#", "#", "#"] },
            "U+3042": { "rows": ["#######", "#.....#", "#######"] },
            "W": { "rows": ["#...#", "#.#.#", ".#.#."], "advance": 7 }
          }
        }
        """;

    private static IFontSource TestFont => new BitmapFont(FontDefinition.Parse(TestFontJson));

    // --- font seam ---

    [Fact]
    public void DefaultFont_MeasuresSixPixelsPerCharacter_AsBefore()
    {
        var all = new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray()) + "é~ıÿ";
        foreach (var ch in all)
            Assert.Equal(PixelFont.Advance, PixelFont.Measure(ch.ToString(), Default));
        Assert.Equal(all.Length * 6, PixelFont.Measure(all, Default));
        Assert.Equal(0, PixelFont.Measure("", Default));
    }

    [Fact]
    public void DefaultFont_DrawsLowerCaseAsCapitals_AndUnknownAsQuestionMark()
    {
        Assert.Same(Default.Glyph('A'), Default.Glyph('a'));
        Assert.Same(Default.Glyph('?'), Default.Glyph('~'));
        Assert.Same(Default.Glyph('?'), Default.Glyph(0x3042));
        Assert.Empty(Default.Glyph(' ').Rows); // a space draws nothing and advances one cell
        Assert.Equal(6, Default.Glyph(' ').Advance);
        var a = Default.Glyph('A');
        Assert.Equal((5, 7), (a.Width, a.Rows.Count));
        Assert.True(a.IsSet(1, 0) && !a.IsSet(0, 0)); // ".###."
    }

    [Theory]
    [InlineData("SWAT", 8)]
    [InlineData("ARENTINO", 6)]
    [InlineData("ARENTINO", 8)]
    [InlineData("ARENTINO", 1)]
    [InlineData("ARENTINO", 0)]
    [InlineData("A", 0)]
    [InlineData("", 3)]
    [InlineData("LEVEL 1 TEST PARTY READY IN THE GUILD.", 20)]
    public void DefaultFont_FitAndColumns_MatchThePreviousCharacterRules(string text, int maxChars)
    {
        // UiOverlays.Fit before T1.5: s.Length <= max ? s : s[..Max(1, max - 1)] + "."
        var oldFit = text.Length <= maxChars ? text : text[..Math.Max(1, maxChars - 1)] + ".";
        Assert.Equal(text.Length == 0 ? "" : oldFit, PixelFont.Fit(text, maxChars * 6, 1, Default));
        // DrawWrapped / Shown before T1.5: the cut keeps maxChars − 1
        if (maxChars >= 1)
            Assert.Equal(text.Length <= maxChars ? text.Length : maxChars - 1,
                PixelFont.FitLength(text, maxChars * 6, 0, Default).Length);
        for (var col = 0; col < text.Length + 4; col++)
            Assert.Equal(col * 6, PixelFont.ColumnX(text, col, Default));
        for (var x = -1; x < (text.Length + 4) * 6; x++)
            Assert.Equal(x / 6, PixelFont.ColumnAt(text, x, Default)); // the old (at.X − 14) / Advance
    }

    [Fact]
    public void VariableWidthFont_MeasuresFitsAndHitsByItsOwnWidths()
    {
        var f = TestFont;
        Assert.Equal((7, 8), (f.Glyph(0x3042).Width, f.Glyph(0x3042).Advance));
        Assert.Equal(2, f.Glyph('i').Advance);
        Assert.Equal(7, f.Glyph('W').Advance);
        Assert.Equal(3, f.Glyph(' ').Advance);
        Assert.Equal(f.Glyph('?'), f.Glyph('I')); // no case folding unless the font asks for it

        const string text = "あiあ i"; // 8 + 2 + 8 + 3 + 2
        Assert.Equal(23, PixelFont.Measure(text, f));
        Assert.Equal(text, PixelFont.Fit(text, 23, 0, f));
        // "." is the fallback (4 wide): budget 18 − 4 = 14 → "あi" (10), the next あ would make 18
        Assert.Equal("あi.", PixelFont.Fit(text, 18, 0, f));
        Assert.Equal((2, true), PixelFont.FitLength(text, 18, 0, f));

        Assert.Equal(0, PixelFont.ColumnAt(text, 7, f));
        Assert.Equal(1, PixelFont.ColumnAt(text, 8, f));
        Assert.Equal(2, PixelFont.ColumnAt(text, 10, f));
        Assert.Equal(10, PixelFont.ColumnX(text, 2, f));
        Assert.Equal(23 + 6, PixelFont.ColumnX(text, text.Length + 1, f)); // past the end: the layout's 6-pixel columns
    }

    [Fact]
    public void FontNonBmpCodePoint_IsOneGlyph()
    {
        var f = new BitmapFont(FontDefinition.Parse("""
            { "id": "x", "height": 1, "glyphs": { "?": { "rows": ["#"] }, "U+20BB7": { "rows": ["###"] } } }
            """));
        Assert.Equal(4, PixelFont.Measure("\U00020BB7", f)); // one code point, two UTF-16 chars
        Assert.Equal(2, PixelFont.ColumnAt("\U00020BB7i", 4, f));
    }

    [Theory]
    [InlineData("""{ "id": "x", "height": 2, "glyphs": { "?": { "rows": ["#"] } } }""")] // row count ≠ height
    [InlineData("""{ "id": "x", "height": 1, "glyphs": { "A": { "rows": ["#"] } } }""")] // no fallback glyph
    [InlineData("""{ "id": "x", "height": 1, "glyphs": { "?": { "rows": ["#"] }, "AB": { "rows": ["#"] } } }""")] // bad key
    [InlineData("""{ "height": 1, "glyphs": { "?": { "rows": ["#"] } } }""")] // no id
    public void BadFontFiles_AreRejected(string json) =>
        Assert.Throws<InvalidDataException>(() => FontDefinition.Parse(json));

    [OriginalDataFact]
    public void FontSource_IsPickedPerPack_DefaultForPhantasieI()
    {
        Assert.Same(PixelFont.Remaster5x7, FontSources.For(null));
        Assert.Same(PixelFont.Remaster5x7, FontSources.For(Amiga));
        Assert.Same(PixelFont.Remaster5x7, FontSources.For(Cga));
        Assert.Same(PixelFont.Remaster5x7, PixelFont.Font); // nothing in the tests switched the font
        Assert.Null(Amiga.FontFile);
    }

    private static string TempGame(string packJson, string? fontId = null, string? fontJson = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "phantasie-seams-" + Guid.NewGuid().ToString("N"), "testgame");
        Directory.CreateDirectory(Path.Combine(dir, "graphics", "fonts"));
        Directory.CreateDirectory(Path.Combine(dir, "art"));
        File.WriteAllText(Path.Combine(dir, "graphics", "index.json"),
            """{ "game": "testgame", "default_order": ["p"], "packs": ["p.json"] }""");
        File.WriteAllText(Path.Combine(dir, "graphics", "p.json"), packJson);
        if (fontId is not null)
            File.WriteAllText(Path.Combine(dir, "graphics", "fonts", fontId + ".json"), fontJson);
        return dir;
    }

    [Fact]
    public void PackWithItsOwnFontFile_GetsThatFont_UnknownFontIsNotDrawable()
    {
        var dir = TempGame("""{ "id": "p", "game": "testgame", "platform": "pc98", "root": "art", "font": "test-var" }""",
            "test-var", TestFontJson);
        var missing = TempGame("""{ "id": "p", "game": "testgame", "platform": "pc98", "root": "art", "font": "nope" }""");
        try
        {
            var pack = GraphicsPackCatalog.Load(dir, searchFrom: dir).Find("p")!;
            Assert.Equal(Path.Combine(dir, "graphics", "fonts", "test-var.json"), pack.FontFile);
            var font = FontSources.For(pack);
            Assert.NotNull(font);
            Assert.Equal("test-var", font.Id);
            Assert.Equal(8, font.Glyph(0x3042).Advance);
            Assert.True(FontSources.CanDraw(pack));

            var noFont = GraphicsPackCatalog.Load(missing, searchFrom: missing).Find("p")!;
            Assert.Null(noFont.FontFile);
            Assert.Null(FontSources.For(noFont));
            Assert.False(FontSources.CanDraw(noFont));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
            Directory.Delete(Path.GetDirectoryName(missing)!, recursive: true);
        }
    }

    // --- palette seam ---

    [OriginalDataFact]
    public void AmigaPack_HasNoPalette_SoTheFrameIsPresentedUntouched()
    {
        Assert.Empty(Amiga.Palette);
        Assert.Null(ScreenPalette.From(Amiga.Palette));
        Assert.Null(ScreenPalette.From(null));
    }

    [OriginalDataFact]
    public void CgaPalette_MapsEveryColourToTheNearestOfItsFour()
    {
        var palette = ScreenPalette.From(Cga.Palette)!;
        Assert.Equal(4, palette.Colours.Count);
        Assert.Equal(new Color(0, 0, 0), palette.Nearest(VirtualScreen.ParseColor("#10121C"))); // remaster background
        Assert.Equal(new Color(255, 255, 255), palette.Nearest(Color.White));
        Assert.Equal(new Color(255, 255, 255), palette.Nearest(new Color(200, 220, 255)));
        Assert.Equal(new Color(0x55, 0xff, 0xff), palette.Nearest(new Color(40, 200, 210)));
        Assert.Equal(new Color(0xff, 0x55, 0xff), palette.Nearest(new Color(220, 60, 200)));

        Color[] frame = [Color.Black, Color.Yellow, new Color(16, 18, 28), Color.Magenta];
        palette.Apply(frame);
        Assert.All(frame, c => Assert.Contains(c, palette.Colours));
        Assert.All(frame, c => Assert.Equal(255, c.A));
    }

    [Fact]
    public void BadPaletteColour_IsRejectedWhenThePackLoads()
    {
        var dir = TempGame("""{ "id": "p", "game": "testgame", "platform": "x", "root": "art", "palette": ["#000000", "red"] }""");
        try
        {
            Assert.Throws<InvalidDataException>(() => GraphicsPackCatalog.Load(dir, searchFrom: dir));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    // --- resolution / pixel-aspect seam ---

    [OriginalDataFact]
    public void AmigaPack_NeedsNoLayoutTransform()
    {
        Assert.Null(VirtualScreen.LayoutTransformFor(Amiga.Screen));
        Assert.Null(VirtualScreen.LayoutTransformFor(ScreenSpec.Default));
        Assert.Equal((1f, 1f), VirtualScreen.LayoutScale(Amiga.Screen));
        Assert.Equal((320, 200), (VirtualScreen.LayoutWidth, VirtualScreen.LayoutHeight));
    }

    [Fact]
    public void LargerPack_ScalesTheSharedLayoutOntoItsResolution()
    {
        var pc98 = new ScreenSpec(640, 400, 1, 1, "#000000");
        Assert.Equal((2f, 2f), VirtualScreen.LayoutScale(pc98));
        var m = VirtualScreen.LayoutTransformFor(pc98)!.Value;
        Assert.Equal(new Vector2(640, 400), Vector2.Transform(new Vector2(320, 200), m));

        var hires = new ScreenSpec(640, 200, 1, 2, "#000000"); // PC-88 / Amiga hires: tall pixels
        Assert.Equal((2f, 1f), VirtualScreen.LayoutScale(hires));
        Assert.Equal(new Vector2(20, 10), Vector2.Transform(new Vector2(10, 10), VirtualScreen.LayoutTransformFor(hires)!.Value));
    }

    [Fact]
    public void Mouse_MapsTheWindowBackToLayoutPixels_WhateverThePackResolution()
    {
        // 640×400 pack at 2× in a 1280×800 window: the window centre is still layout (160, 100).
        var present = VirtualScreen.PresentRect(640, 400, 1280, 800);
        Assert.Equal(new Rectangle(0, 0, 1280, 800), present);
        Assert.Equal(new Point(160, 100), VirtualScreen.WindowToVirtual(present, VirtualScreen.LayoutWidth,
            VirtualScreen.LayoutHeight, new Point(640, 400)));
        Assert.Equal(new Point(319, 199), VirtualScreen.WindowToVirtual(present, VirtualScreen.LayoutWidth,
            VirtualScreen.LayoutHeight, new Point(1279, 799)));

        // 640×200 with 1:2 pixels shows as 640×400.
        var tall = VirtualScreen.PresentRect(640, 400, 640, 400);
        Assert.Equal(new Point(160, 100), VirtualScreen.WindowToVirtual(tall, VirtualScreen.LayoutWidth,
            VirtualScreen.LayoutHeight, new Point(320, 200)));
    }
}
