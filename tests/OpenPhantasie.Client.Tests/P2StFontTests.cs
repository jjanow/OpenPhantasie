using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s160, II ST's look (user, s159): the alert boxes, end-of-fight boxes and the ending's text in EmuTOS's 8×8 system font
/// (<c>tools/scripts/emutos_st_font.py</c>; TOS's own is not on the disks, the look was measured on EmuTOS), checked
/// against the EmuTOS shots in <c>atari-st/phantasie2/parity/shots</c>; the alert boxes wait for Return only, as the AES
/// form loop.
/// </summary>
public class P2StFontTests
{
    private static string FontFile(string id) =>
        Path.Combine(GameDataRepository.DefaultPathFor(GameId.P2), "graphics", "fonts", id + ".json");

    private static BitmapFont Font(string id) => new(FontDefinition.Load(FontFile(id)));

    private static GraphicsPack Pack() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2)).Find("atari-st")!;

    private static string Row(GlyphDefinition g, int row) =>
        string.Concat(Enumerable.Range(0, g.Width).Select(c => g.IsSet(c, row) ? '#' : '.'));

    [Fact]
    public void EmuTosFont_IsEightByEight_Monospaced_AsTheAlertShot()
    {
        var font = Font("emutos-st-8x8");
        Assert.Equal((8, 8), (font.Height, font.LineHeight));
        Assert.All(" AMUiW.?ß", c => Assert.Equal(8, font.Glyph(c).Advance));
        // p2-endgame-wand-unusable.png, "UNUSABLE" at (104, 93): the U and N rows as EmuTOS drew them.
        var u = font.Glyph('U');
        Assert.Equal([".##..##.", ".##..##.", ".##..##.", ".##..##.", ".##..##.", ".##..##.", "..#####.", "........"],
            Enumerable.Range(0, 8).Select(r => Row(u, r)));
        Assert.Equal(".###.##.", Row(font.Glyph('N'), 1));
        Assert.True(font.Has('a')); // lower case of its own (the ending's pages)
    }

    [Fact]
    public void Italic_RowsSlantRight_FromTheBottom_ByTheSkewMask()
    {
        // VDI F_SKEW, skew mask 0x5555 rotated once per row from the bottom: rows 0..7 sit 4, 3, 3, 2, 2, 1, 1, 0 px right.
        var plain = Font("emutos-st-8x8").Glyph('N');
        var italic = Font("emutos-st-8x8-italic").Glyph('N');
        Assert.Equal((12, 8), (italic.Width, italic.Advance));
        int[] shift = [4, 3, 3, 2, 2, 1, 1, 0];
        for (var r = 0; r < 8; r++)
            Assert.Equal(new string('.', shift[r]) + Row(plain, r) + new string('.', 4 - shift[r]), Row(italic, r));
    }

    [Fact]
    public void Justified_IsEmuTosVJustified_AsTheTitleShot()
    {
        // p2-endgame-titles.png (s80): each glyph's x on the bottom line, measured pixel for pixel. 310 − 256 = 54 extra:
        // the word gap capped at half the 8-px cell (+4 per space, no remainder), 34 left over 31 gaps = 1, the first 3 +1.
        using var _ = PixelFont.With(Font("emutos-st-8x8"));
        var xs = VictoryScreenView.JustifiedXs("KLEPTO - the bold from Ferronrah", 310);
        int?[] shot = [5, 15, 25, 35, 44, 53, null, 75, null, 97, 106, 115, null, 137, 146, 155, 164, null, 186, 195, 204,
            213, null, 235, 244, 253, 262, 271, 280, 289, 298, 307];
        for (var i = 0; i < shot.Length; i++)
            if (shot[i] is int x)
                Assert.Equal(x, xs[i]);
    }

    [Fact]
    public void Justified_ALineWiderThanTheField_ClosesUp()
    {
        // 42 characters (336 px) in 310: each of the 5 word gaps −26 / 5 = −5, capped at −4; 316 − 310 = 6 left, taken
        // 1 px each from the first 6 letter gaps. The line starts at 5 and ends at 315 as the ST's; nothing runs off.
        using var _ = PixelFont.With(Font("emutos-st-8x8"));
        const string text = "FATHERJO - the courageous from Netherworld";
        var xs = VictoryScreenView.JustifiedXs(text, 310);
        Assert.Equal(5, xs[0]);
        Assert.Equal(12, xs[1]); // 5 + 8 − 1
        Assert.Equal(315, xs[^1] + 8);
        Assert.True(xs.Zip(xs.Skip(1)).All(p => p.Second > p.First));
    }

    [Fact]
    public void AlertBox_OnlyReturnAnswers()
    {
        var box = new P2AlertBox();
        box.Offer(StAlert.Parse("[0][UNUSABLE][SORRY]"));
        Assert.True(box.HandleKeys([Keys.Space, Keys.Escape, Keys.Y, Keys.N])); // used up, the box stays
        Assert.True(box.Shown);
        Assert.True(box.HandleKeys([Keys.Enter]));
        Assert.False(box.Shown);
    }

    [OriginalDataFact]
    public void Pack_DrawsTheBoxesAndTheEnding_InEmuTosFonts()
    {
        var pack = Pack();
        Assert.Equal("emutos-st-8x8", FontSources.ForRole(pack, UiOverlays.P2AlertFontRole)!.Id);
        Assert.Equal("emutos-st-8x8", FontSources.ForRole(pack, UiOverlays.P2EndBoxFontRole)!.Id);
        Assert.Equal(PixelFont.Id, pack.Font); // the rest of II's text keeps the remaster's font
        var ending = pack.EndingScene!;
        Assert.Equal("emutos-st-8x8", VictoryScreenView.SpeechFont(ending)!.Id);
        foreach (var page in new[] { ending.IntroPage!, ending.ClosingPage! })
        {
            Assert.Equal("emutos-st-8x8-italic", VictoryScreenView.PageFont(page)!.Id);
            Assert.Equal((2, -4), (page.X, page.FontDx)); // v_gtext at x 2; the slanted cell starts 4 px left
            Assert.Equal("emutos-st-6x6", VictoryScreenView.ButtonFont(page)!.Id); // s162: the OK label, te_font 5
        }
    }

    [Fact]
    public void SmallFont_IsSixBySix_AndPutsTheOkLabel_AsTheEndingShot()
    {
        // s162: OUT.RSC tree 18's G_BOXTEXT "OK" has te_font 5, the 6×6 small font (EmuTOS fnt_st_6x6.c). On
        // p2-endgame-filmon-1.png (s80) the O's rows are 278–280 / 277, 281 at y 184–188 and the K's 284 + 287 …
        var font = Font("emutos-st-6x6");
        Assert.Equal((6, 6), (font.Height, font.LineHeight));
        Assert.All(" OK.a", c => Assert.Equal(6, font.Glyph(c).Advance));
        Assert.Equal([".###..", "#...#.", "#...#.", "#...#.", ".###..", "......"],
            Enumerable.Range(0, 6).Select(r => Row(font.Glyph('O'), r)));
        Assert.Equal([".#..#.", ".#.#..", ".##...", ".#.#..", ".#..#.", "......"],
            Enumerable.Range(0, 6).Select(r => Row(font.Glyph('K'), r)));
        using (PixelFont.With(font))
            Assert.Equal(new Microsoft.Xna.Framework.Point(277, 184), VictoryScreenView.OkLabelAt("OK"));
    }
}
