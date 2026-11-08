using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// IV X68000's own ANK fonts (s135, x68000 FORMAT.md 6.12; <c>tools/scripts/x68k_p4_fonts.py</c>): P4.X font 0 for the
/// Mystic's bubble and, in its italic style (<c>_wfopt</c> bit 1), for the fight's "Exit" label; checked against the
/// s120 XEiJ shots (<c>parity/shots/p4-x68k-exit-window-zoom.png</c>, <c>p4-x68k-mystic-bubble.png</c>).
/// </summary>
public class P4X68kFontTests
{
    private static GraphicsPack Pack() => GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Find("x68000")!;

    private static IFontSource Role(string role) => FontSources.ForRole(Pack(), role)!;

    [OriginalDataFact]
    public void Pack_NamesP4XFonts_ForTheFourPlacesOnly()
    {
        var pack = Pack();
        Assert.Equal("x68k-p4-font0-italic", pack.RoleFonts[P4DungeonScreen.ExitFontRole].Id);
        Assert.Equal("x68k-p4-font0", pack.RoleFonts[P4DungeonScreen.MysticFontRole].Id);
        Assert.Equal("x68k-p4-font0-bold", pack.RoleFonts[P4DungeonScreen.BannerFontRole].Id);
        Assert.Equal("x68k-p4-font1", pack.RoleFonts[P4DungeonScreen.PanelLabelFontRole].Id);
        Assert.Equal(4, pack.RoleFonts.Count);
        Assert.All(pack.RoleFonts.Values, f => Assert.True(File.Exists(f.File)));
        Assert.Equal(PixelFont.Id, pack.Font); // the rest of IV's text keeps the remaster's font
        Assert.Null(FontSources.ForRole(pack, "no-such-role"));
        Assert.Null(FontSources.ForRole(null, P4DungeonScreen.ExitFontRole));
    }

    [OriginalDataFact]
    public void Font0_IsProportional_SixteenRows()
    {
        var f = Role(P4DungeonScreen.MysticFontRole);
        Assert.Equal(16, f.Height);
        Assert.Equal(7, f.Glyph('E').Advance);
        Assert.Equal(8, f.Glyph('x').Advance);
        Assert.Equal(4, f.Glyph('i').Advance);
        Assert.Equal(6, f.Glyph('t').Advance);
        Assert.Equal(25, PixelFont.Measure("Exit", f));
        // Italic keeps the advances (_wfnt adds width only for bit 2).
        Assert.Equal(25, PixelFont.Measure("Exit", Role(P4DungeonScreen.ExitFontRole)));
    }

    [Fact]
    public void ExitLabel_StartsAtEight_InItsSquare()
    {
        // _wopen(..., _fsize + 16, ...), _wprcen(win, width / 2, 2, "Exit"): x = 20 − (25 >> 1) = 8; ink from 9 (s120).
        Assert.Equal(8, P4DungeonScreen.ExitLabelX(25));
        Assert.Equal(2, P4DungeonScreen.ExitLabelY);
    }

    /// <summary>The ink of "Exit" on the s120 XEiJ shot (zoom file, 4 × 4 per pixel; rows 4–12 of the square).</summary>
    private static readonly string[] ExitShot =
    [
        "....#####..........##...##",
        "....##..................##",
        "...##.....##..##..##..####",
        "...##.....##..##..##...##.",
        "..####...##..##..##...##..",
        "..##......####...##...##..",
        ".##.....##..##..##...##...",
        ".##.....##..##..##...##...",
        "#####..##..##..##....##...",
    ];

    [OriginalDataFact]
    public void ItalicExit_MatchesTheXeijShot_PixelForPixel()
    {
        var drawn = Render(Role(P4DungeonScreen.ExitFontRole), "Exit");
        // Glyph rows 2–10 hold the ink (label at window y 2 → square rows 4–12); its first ink column is the pen's 1.
        var ink = drawn.Skip(2).Take(9).Select(r => r[1..].TrimEnd('.').PadRight(ExitShot[0].Length, '.')).ToArray();
        Assert.Equal(ExitShot, ink);
        Assert.All(drawn.Take(2).Concat(drawn.Skip(11)), r => Assert.DoesNotContain('#', r));
    }

    [OriginalDataFact]
    public void MysticScoreDigit_IsFont0s_AsOnTheXeijShot()
    {
        // The red "2" (_wdouti, colour 2) on p4-x68k-mystic-bubble.png, shot rows 40–48 from x 182 (glyph rows 2–10).
        string[] shot = [".####", "#...##", "....##", "....##", "...##", "..##", ".##", "##", "######"];
        var g = Render(Role(P4DungeonScreen.MysticFontRole), "2");
        Assert.Equal(shot, g.Skip(2).Take(9).Select(r => r[1..].TrimEnd('.')).ToArray());
        Assert.All(g.Take(2).Concat(g.Skip(11)), r => Assert.DoesNotContain('#', r));
    }

    [OriginalDataFact]
    public void RoleFont_FallsBackToThePackFont_ForWhatItLacks()
    {
        var f = Assert.IsType<FallbackFont>(Role(P4DungeonScreen.MysticFontRole));
        Assert.True(f.Primary.Has('A'));
        Assert.True(f.Primary.Has(0xFF71)); // half-width ア (JIS X 0201 0xB1)
        Assert.False(f.Primary.Has('汝'));  // kanji: the CG ROM's, not P4.X's
        Assert.Same(PixelFont.Remaster5x7.Glyph('汝'), f.Glyph('汝'));
        Assert.NotSame(PixelFont.Remaster5x7.Glyph('A'), f.Glyph('A'));
    }

    /// <summary>
    /// "Congraturation!" on the banner of <c>parity/shots/p4-x68k-ending-street.png</c> (s135 XEiJ shot; picture origin at
    /// shot (216, 62)): shot rows 72–83 from x 319 = window rows 10–21 from x 103, i.e. glyph rows 2–13 at the pen.
    /// </summary>
    private static readonly string[] CongratsShot =
    [
        "..#####...............................................###.............................###...###.....................###",
        ".###..##..............................................###.............................###...........................###",
        ".###.......#####...######....######..######..#####...#####..###.###..######..#####...#####..###...#####...######....###",
        ".###......###.###..###.###..###.###..####...##..###...###...###.###..####...##..###...###...###..###.###..###.###...###",
        ".###......###.###..###.###..###.###..###.....######...###...###.###..###.....######...###...###..###.###..###.###...###",
        ".###......###.###..###.###..###.###..###....###.###...###...###.###..###....###.###...###...###..###.###..###.###...###",
        ".###......###.###..###.###..###.###..###....###.###...###...###.###..###....###.###...###...###..###.###..###.###",
        ".###..##..###.###..###.###..###.###..###....###.###...###...###.###..###....###.###...###...###..###.###..###.###...###",
        "..#####....#####...###.###...######..###.....######....###...######..###.....######....###..###...#####...###.###...###",
        "................................###",
        "............................##..###",
        ".............................#####",
    ];

    [OriginalDataFact]
    public void BoldBanner_MatchesTheXeijEndingShot_PixelForPixel()
    {
        var f = Role(P4DungeonScreen.BannerFontRole);
        var text = GameText.S("p4x.0x3fa55");
        Assert.Equal("Congraturation!", text);
        // _fsize counts the plain advances (106); the bold pass draws each glyph one wider (121).
        Assert.Equal(106, P4DungeonScreen.BoldPlainWidth(text, f));
        Assert.Equal(121, PixelFont.Measure(text, f));
        var line = P4DungeonScreen.StreetBanner(null, ending: true, t => P4DungeonScreen.BoldPlainWidth(t, f))!.Value;
        Assert.Equal((text, 103), line); // 156 − (106 >> 1)
        var drawn = Render(f, text);
        Assert.All(drawn.Take(2).Concat(drawn.Skip(14)), r => Assert.DoesNotContain('#', r));
        Assert.Equal(CongratsShot, drawn.Skip(2).Take(12).Select(r => r.TrimEnd('.')).ToArray());
    }

    [Fact]
    public void StreetBanner_CentresTheNameAlone_SuffixAfter_BelowMap13()
    {
        static int Plain(string t) => t.Length * 10;
        var pendragon = new P4TownSite(1, "Pendragon", 0, 9, 9, 10, null, null);
        var (text, x) = P4DungeonScreen.StreetBanner(pendragon, ending: false, Plain)!.Value;
        Assert.Equal("Pendragon" + GameText.S("p4x.0x3fa50"), text);
        Assert.Equal(136 - (90 >> 1), x); // _wkprcen(32, 156 − 20, 8, name), then _wkprint("の町") at the pen
        var skull = new P4TownSite(13, "Skull City", 2, 1, 1, 1, null, null);
        Assert.Equal(("Skull City", 156 - 50), P4DungeonScreen.StreetBanner(skull, ending: false, Plain));
        Assert.Null(P4DungeonScreen.StreetBanner(null, ending: false, Plain));
    }

    [Fact]
    public void KanjiBanner_SitsWhereTheSystemMenuShotHasIt()
    {
        // p4-x68k-system-menu.png: Pendragon's banner (six characters + "の町"), ink from window x 89 to 222. Six CG ROM kanji count 16 each in
        // _fsize (96): x = 136 − 48 = 88; eight bold kanji of 17 end at 88 + 136 = 224 (each kanji's last column blank).
        var line = P4DungeonScreen.StreetBanner(new P4TownSite(1, "アイウエオカ", 0, 9, 9, 10, null, null), false, t => t.Length * 16)!.Value;
        Assert.Equal(88, line.X);
    }

    /// <summary>The text as <see cref="PixelFont.DrawScaled"/> lays it out (pen by advances), one string per font row.</summary>
    private static string[] Render(IFontSource font, string text)
    {
        var width = PixelFont.Measure(text, font) + 16;
        var rows = Enumerable.Range(0, font.Height).Select(_ => new char[width]).ToArray();
        foreach (var r in rows)
            Array.Fill(r, '.');
        var pen = 0;
        foreach (var ch in text)
        {
            var g = font.Glyph(ch);
            for (var row = 0; row < g.Rows.Count; row++)
                for (var col = 0; col < g.Width; col++)
                    if (g.IsSet(col, row))
                        rows[row][pen + col] = '#';
            pen += g.Advance;
        }
        return [.. rows.Select(r => new string(r))];
    }

    /// <summary><c>_oship_mark</c>'s <c>_xbox(8, …)</c> (s135): palette index XOR 8 on the picture's colours.</summary>
    [OriginalDataFact]
    public void ShipMark_FlipsPaletteBit3()
    {
        var pkh = Pack().Palettes["pkh"].Select(VirtualScreen.ParseColor).ToArray();
        Assert.Equal(16, pkh.Length);
        var marked = P4DungeonScreen.ShipMarkColours([pkh[0], pkh[15], pkh[9], pkh[1], new Microsoft.Xna.Framework.Color(1, 2, 3)], pkh);
        Assert.Equal(pkh[8], marked[0]);  // the party square's black border, aboard
        Assert.Equal(pkh[7], marked[1]);  // its white inside, aboard
        Assert.Equal(pkh[1], marked[2]);  // light blue water
        Assert.Equal(pkh[9], marked[3]);
        Assert.Equal(new Microsoft.Xna.Framework.Color(1, 2, 3), marked[4]); // not a palette colour: unchanged
    }
}
