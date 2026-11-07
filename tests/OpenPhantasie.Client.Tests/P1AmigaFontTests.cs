using System.Buffers.Binary;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// I Amiga's two fonts (s134, Amiga FORMAT.md "Fonts"; <c>tools/scripts/extract_amiga_fonts.py</c>): the game's own
/// <c>z6font</c> 6 for the window's text (the line, the requesters) and the Kickstart's topaz 8 for the screen's bar and
/// menus, as pack data drawn on the combat screen behind AMIGA SCREENS; each checked against the s132 FS-UAE shots.
/// </summary>
public class P1AmigaFontTests
{
    private static GraphicsPack Pack() => GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!;

    private static IFontSource Z6() => FontSources.Named(Pack().CombatField!.Font, Pack().CombatField!.FontFile)!;
    private static IFontSource Topaz() => FontSources.Named(Pack().CombatField!.MenuBar!.Font, Pack().CombatField!.MenuBar!.FontFile)!;

    [OriginalDataFact]
    public void Pack_NamesTheTwoFonts_ForTheCombatScreenOnly()
    {
        var pack = Pack();
        var l = pack.CombatField!;
        Assert.Equal("amiga-z6font-6", l.Font);       // TextAttr h78 +0x36
        Assert.Equal("amiga-topaz-8", l.MenuBar!.Font); // TextAttr h78 +0x22 (NewScreen.Font)
        Assert.True(File.Exists(l.FontFile));
        Assert.True(File.Exists(l.MenuBar.FontFile));
        Assert.Equal(PixelFont.Id, pack.Font);        // the rest of the remaster's text keeps its own font
    }

    [OriginalDataFact]
    public void Z6Font_IsSixBySix_AndHasNoLowerCase()
    {
        var z6 = Z6();
        Assert.Equal(6, z6.Height);
        Assert.All(new[] { 'A', 'M', '0', ':', 'a' }, c => Assert.Equal(6, z6.Glyph(c).Advance));
        // The font's CharLoc gives a–z the capitals' bits: lower case reads as upper case on the Amiga.
        for (var c = 'a'; c <= 'z'; c++)
            Assert.Equal(z6.Glyph(char.ToUpperInvariant(c)).Rows, z6.Glyph(c).Rows);
        Assert.Equal(6, PixelFont.Measure("ABCDEFGHIJ", z6) / 10);
    }

    [OriginalDataFact]
    public void Topaz_IsEightByEight_WithItsOwnLowerCase()
    {
        var topaz = Topaz();
        Assert.Equal(8, topaz.Height);
        Assert.Equal(8, topaz.Glyph('a').Advance);
        Assert.NotEqual(topaz.Glyph('A').Rows, topaz.Glyph('a').Rows);
        Assert.Equal(13 * 8, PixelFont.Measure("Party Options", topaz));
    }

    [OriginalDataFact]
    public void Named_FallsBack_WhenThereIsNoFont()
    {
        Assert.Null(FontSources.Named(null, null));
        Assert.Null(FontSources.Named("no-such-font", null));
        Assert.Same(PixelFont.Remaster5x7, FontSources.Named(PixelFont.Id, null));
        var before = PixelFont.Font;
        using (PixelFont.With(null))
            Assert.Same(before, PixelFont.Font);
        using (PixelFont.With(Z6()))
            Assert.Same(Z6(), PixelFont.Font);
        Assert.Same(before, PixelFont.Font);
    }

    /// <summary>The scroll gadgets' "^" / "@" (h84 ITexts 68 / 69 name z6font) are the pack's up / down glyphs.</summary>
    [OriginalDataFact]
    public void ScrollGlyphs_AreTheFontsOwn()
    {
        var list = Pack().CombatField!.SpellList!.Spells;
        Assert.Equal(Ink(Z6().Glyph('^')).Take(list.UpGlyph.Count), list.UpGlyph.Select(r => r.PadRight(6, '.')));
        Assert.Equal(Ink(Z6().Glyph('@')).Take(list.DownGlyph.Count), list.DownGlyph.Select(r => r.PadRight(6, '.')));
    }

    /// <summary>
    /// The s132 FS-UAE shots (<c>parity/shots/p1-amiga-combat-*</c>, file row = screen row + 5) against the fonts drawn at
    /// the pack's places: every pixel of each text's cells.
    /// </summary>
    [OriginalDataTheory]
    [InlineData("p1-amiga-combat-07-spells-available.png", false, 59, 91, "QUICKNESS I", 0xEEEEEE)]
    [InlineData("p1-amiga-combat-07-spells-available.png", false, 59, 99, "MONSTER EVAL.", 0xEEEEEE)]
    [InlineData("p1-amiga-combat-04-orders.png", false, 41, 49, "VILMAR    HP:", 0xEEEEEE)]
    [InlineData("p1-amiga-combat-04-orders.png", false, 132, 116, "F I G H T", 0x000000)]
    [InlineData("p1-amiga-combat-09-line.png", false, 112, 185, "DRAGONSON MISSES", 0xEE6600)]
    [InlineData("p1-amiga-combat-01-field.png", true, 12, 1, "Info", 0x888888)]
    [InlineData("p1-amiga-combat-01-field.png", true, 67, 1, "Party Options", 0x888888)]
    [InlineData("p1-amiga-combat-01-field.png", true, 195, 1, "Speed", 0x888888)]
    [InlineData("p1-amiga-combat-02-party-options-menu.png", true, 69, 13, "Beg mercy", 0xEEEEEE)]
    [InlineData("p1-amiga-combat-02-party-options-menu.png", true, 69, 73, "Party health", 0xEEEEEE)]
    [InlineData("p1-amiga-combat-03-speed-menu.png", true, 189, 23, "*Normal", 0xEEEEEE)]
    public void Fonts_MatchTheAmigaShots(string shot, bool bar, int x, int y, string text, int ink) =>
        Assert.Equal(0, Mismatch(shot, bar, x, y, text, ink));

    /// <summary>
    /// Requester 24's six labels (shot <c>-05</c>) from the string table at the overlay's place (the button + (2, 2)), and
    /// requester 26's digits (shot <c>-06</c>, the button + (3, 2)): the Amiga's strings are 11 characters
    /// (<c>"   THRUST  "</c>, <c>"    CAST   "</c>).
    /// </summary>
    [OriginalDataFact]
    public void OrderButtons_AndRankDigits_MatchTheAmigaShots()
    {
        var o = Pack().CombatField!.Orders!;
        string[] orders = ["thrust", "attack", "slash", "lunge", "cast", "parry"];
        for (var b = 0; b < orders.Length; b++)
        {
            var r = o.ChoiceButtons[b];
            Assert.Equal(0, Mismatch("p1-amiga-combat-05-order-buttons.png", false, r.X + 2, r.Y + 2,
                UiText.S($"p1field.order.{orders[b]}").TrimEnd(), 0));
        }
        for (var b = 0; b < 3; b++)
        {
            var r = o.RankButtons[b];
            Assert.Equal(0, Mismatch("p1-amiga-combat-06-rank.png", false, r.X + 3, r.Y + 2, (b + 1).ToString(), 0));
        }
        Assert.Equal(0, Mismatch("p1-amiga-combat-06-rank.png", false, o.PromptTextX, o.PromptTextY, "THRUST AT RANK", 0));
    }

    /// <summary>
    /// Requester 6 "ON WHOM:" on the s134 shot <c>-19</c>: its title, five member rows built by
    /// <see cref="UiOverlays.P1TargetRow"/> at the pack's row + (1, 1) (LIZ's row is under the pointer) and EXIT.
    /// </summary>
    [OriginalDataFact]
    public void OnWhom_MatchesTheAmigaShot()
    {
        const string shot = "p1-amiga-combat-19-on-whom.png";
        var t = Pack().CombatField!.SpellList!.Target;
        Assert.Equal(0, Mismatch(shot, false, t.TitleX, t.TitleY, UiText.S("p1field.on_whom"), 0xEEEEEE));
        (int Row, string Name, CharacterStatus Status, int Hp, int HpMax, int Mp, int MpMax)[] rows =
        [
            (0, "VILMAR", CharacterStatus.Ok, 255, 255, 255, 255), (1, "DRAGONSON", CharacterStatus.Ok, 10, 10, 7, 7),
            (2, "EMILY", CharacterStatus.Ok, 30, 30, 2, 8), (4, "HOLLY", CharacterStatus.Dead, 0, 8, 4, 4),
            (5, "MANGAR", CharacterStatus.Ok, 91, 91, 7, 7),
        ];
        foreach (var r in rows)
        {
            var text = UiOverlays.P1TargetRow(new Character
            {
                Name = r.Name, Status = r.Status, HpCur = r.Hp, HpMax = r.HpMax, MagicCur = r.Mp, MagicMax = r.MpMax,
            });
            // D-064 (s155): the remaster prints the manual's "OKAY" where the Amiga's h12 table has "OK"; the shot's pixels
            // are checked with the Amiga's word in the same %-6.5s cell.
            if (r.Status == CharacterStatus.Ok)
            {
                Assert.Equal("OKAY  ", text.Substring(10, 6));
                text = text[..10] + "OK    " + text[16..];
            }
            var row = t.Row(r.Row);
            Assert.Equal(0, Mismatch(shot, false, row.X + 1, row.Y + 1, text, 0xEEEEEE));
        }
        Assert.Equal(0, Mismatch(shot, false, t.Exit.X + t.ExitTextDx, t.Exit.Y + t.ExitTextDy, UiText.S(t.ExitLabel), 0));
    }

    /// <summary>
    /// Requester 23 on the s135 shots <c>-23</c> / <c>-24</c> (Speed at boot, the end of a fight): each line centred in 40
    /// cells (h65 <c>+0x1f4</c>) from x 41, so "MONSTERS KILLED" starts at 113, "EACH SURVIVOR GETS" at 107 and "140 XP AND 40
    /// GP" at 113; one line takes the middle row, two the top two.
    /// </summary>
    [OriginalDataFact]
    public void Message_IsCentred_AsOnTheAmigaShots()
    {
        var m = Pack().CombatField!.Message!;
        Assert.Equal(40, m.CenterColumns);
        var killed = UiText.S("core.combat.monsters_killed");
        var share = UiText.S("core.combat.each_survivor_gets");
        var gold = UiText.F("core.combat.x_xp_and_x_gp", 140, 40);
        Assert.Equal("140 XP AND 40 GP", gold);
        Assert.Equal(113, m.TextLeft(killed.Length, 6));
        Assert.Equal(107, m.TextLeft(share.Length, 6));
        Assert.Equal(113, m.TextLeft(gold.Length, 6));
        Assert.Equal(m.TextX, m.TextLeft(45, 6)); // longer than the field: no pad (the count goes negative)
        Assert.Equal(0, Mismatch("p1-amiga-combat-23-message-monsters-killed.png", false, m.TextLeft(killed.Length, 6),
            m.RowsY[m.RowIndices(1)[0]], killed, 0xEEEEEE));
        Assert.Equal(0, Mismatch("p1-amiga-combat-24-message-survivors.png", false, m.TextLeft(share.Length, 6), m.RowsY[0], share,
            0xEEEEEE));
        Assert.Equal(0, Mismatch("p1-amiga-combat-24-message-survivors.png", false, m.TextLeft(gold.Length, 6), m.RowsY[1], gold,
            0xEEEEEE));
    }

    /// <summary>
    /// The town line (s135): the pack names z6font for the name and messages (h80 <c>:0x818</c> prints on the window's
    /// RastPort); PELNOR at the remaster's place in it equals the exact FS-UAE shot, and a message's text row is TopEdge 185.
    /// </summary>
    [OriginalDataFact]
    public void TownName_IsZ6Font_AsOnTheAmigaShot()
    {
        var town = Pack().TownScreen;
        Assert.Equal("amiga-z6font-6", town.Font);
        Assert.Same(Z6(), UiOverlays.TownTextFont(town));
        int left;
        using (PixelFont.With(UiOverlays.TownTextFont(town)))
            left = UiOverlays.TownNameLeft(town, "PELNOR", freshStart: false);
        Assert.Equal(130, left);
        Assert.Equal(0, Mismatch("p1-amiga-town-pelnor-exact.png", false, left, town.NameY, "PELNOR", 0xEE2244));
        Assert.Equal(185, UiOverlays.TownBandTop(town) + 1); // DrawTownHud's text row on the pack's own line
        Assert.Null(UiOverlays.TownTextFont(TownScreenLayout.Default));
    }

    /// <summary>The start-up requester ("DO YOU WANT:", shot <c>p1-amiga-start-menu</c>) is z6font too (s135, not drawn by the remaster).</summary>
    [OriginalDataTheory]
    [InlineData(77, 79, "DO YOU WANT:")]
    [InlineData(89, 115, "CONTINUE SAVED GAME")]
    public void StartMenu_IsZ6Font(int x, int y, string text) =>
        Assert.Equal(0, Mismatch("p1-amiga-start-menu.png", false, x, y, text, 0xEEEEEE));

    /// <summary>
    /// s153: the victory screen's speech (h80 <c>+0x84a</c> on the window's RastPort) is z6font, centred on 320 at TopEdge
    /// 192 in pen 5; the remaster's line 1 at its own place in the pack's font equals the s130 shot <c>-07</c> (its rows
    /// are screen rows).
    /// </summary>
    [OriginalDataFact]
    public void VictoryLine_IsZ6Font_AsOnTheAmigaShot()
    {
        var scene = Pack().EndingScene!;
        Assert.Equal("amiga-z6font-6", scene.Font);
        Assert.Same(Z6(), VictoryScreenView.SpeechFont(scene));
        int x;
        string text;
        using (PixelFont.With(VictoryScreenView.SpeechFont(scene)))
            (x, text) = VictoryScreenView.SpeechLine(UiText.S(scene.SpeechKeys![0]));
        Assert.Equal(49, x); // (320 − 6 · 37) / 2
        Assert.Equal(192, scene.TextY);
        Assert.Equal(0, Mismatch("p1-amiga-victory-07-line1.png", false, x, scene.TextY, text, 0xEEEE00, rowOffset: 0));
    }

    /// <summary>
    /// s153 FS-UAE shots of text the remaster does not draw as the Amiga does (FORMAT.md "Fonts"): requester 14 (Guild ›
    /// Inspect, the character sheet; its ReqText, IText 123, is the member's name in topaz 8 at (7, 7), the rows z6font),
    /// requester 22 (the Black Knights; IText 194 "BLACK KNIGHTS!!!" in topaz 8 at (49, 19), the buttons' z6font labels)
    /// and "LEAVING %s" on the town line (h29 <c>+0x168</c>, z6font, pen 4, the 10-character name field centred with it).
    /// </summary>
    [OriginalDataTheory]
    [InlineData("p1-amiga-guild-inspect-sheet.png", true, 46, 37, "VILMAR", 0x00AAEE)]
    [InlineData("p1-amiga-guild-inspect-sheet.png", false, 46, 50, "LEVEL  1 FIGHTER DWARF OK", 0x00EE00)]
    [InlineData("p1-amiga-black-knights.png", true, 95, 89, "BLACK KNIGHTS!!!", 0x44EE22)]
    [InlineData("p1-amiga-black-knights.png", false, 80, 114, "RUN", 0x000000)]
    [InlineData("p1-amiga-black-knights.png", false, 146, 114, "FIGHT", 0x000000)]
    [InlineData("p1-amiga-black-knights.png", false, 206, 114, "SURRENDER", 0x000000)]
    [InlineData("p1-amiga-town-leaving-pelnor.png", false, 106, 185, "LEAVING PELNOR", 0xEEEE00)]
    [InlineData("p1-amiga-town-leaving-pelnor-live.png", false, 106, 185, "LEAVING PELNOR", 0xEEEE00)] // s154 film, frame 123
    public void UndrawnRequesters_MatchTheirFonts(string shot, bool topaz, int x, int y, string text, int ink) =>
        Assert.Equal(0, Mismatch(shot, topaz, x, y, text, ink));

    private static int Mismatch(string shot, bool bar, int x, int y, string text, int ink, int rowOffset = 5)
    {
        var font = bar ? Topaz() : Z6();
        var path = Path.Combine(Pack().RootDirectory, "..", "..", "..", "parity", "shots", shot);
        var (w, h, px) = Pixels(path);
        var bad = 0;
        var cx = x;
        foreach (var ch in text)
        {
            var g = font.Glyph(ch);
            for (var row = 0; row < font.Height; row++)
            {
                for (var col = 0; col < g.Advance; col++)
                {
                    var sy = y + row + rowOffset;
                    if (cx + col >= w || sy >= h)
                        continue;
                    if ((px[sy * w + cx + col] == ink) != g.IsSet(col, row))
                        bad++;
                }
            }
            cx += g.Advance;
        }
        // The menu titles' places are the pack's (pen x, topaz glyphs carry blank columns).
        if (bar && y == 1 && rowOffset == 5)
            Assert.Contains(Pack().CombatField!.MenuBar!.Menus, m => m.TitleX == x);
        return bad;
    }

    private static IEnumerable<string> Ink(GlyphDefinition g) =>
        Enumerable.Range(0, g.Rows.Count).Select(r => new string([.. Enumerable.Range(0, 6).Select(c => g.IsSet(c, r) ? '#' : '.')]));

    /// <summary>An 8-bit RGB PNG's pixels as 0xRRGGBB.</summary>
    internal static (int W, int H, int[] Px) Pixels(string path)
    {
        var data = File.ReadAllBytes(path);
        int p = 8, width = 0, height = 0, colourType = 0;
        var idat = new MemoryStream();
        while (p < data.Length)
        {
            var len = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(p));
            var type = System.Text.Encoding.ASCII.GetString(data, p + 4, 4);
            var body = data.AsSpan(p + 8, len);
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(body);
                height = BinaryPrimitives.ReadInt32BigEndian(body[4..]);
                Assert.Equal(8, body[8]);
                colourType = body[9];
            }
            else if (type == "IDAT")
                idat.Write(body);
            p += 12 + len;
        }
        var bpp = colourType switch { 2 => 3, 6 => 4, _ => throw new NotSupportedException($"PNG colour type {colourType}") };
        idat.Position = 0;
        using var z = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress);
        var raw = new MemoryStream();
        z.CopyTo(raw);
        var bytes = raw.ToArray();
        var stride = width * bpp;
        var prev = new byte[stride];
        var cur = new byte[stride];
        var px = new int[width * height];
        for (var y = 0; y < height; y++)
        {
            var filter = bytes[y * (stride + 1)];
            Array.Copy(bytes, y * (stride + 1) + 1, cur, 0, stride);
            for (var i = 0; i < stride; i++)
            {
                int a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                cur[i] = (byte)(cur[i] + filter switch
                {
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => 0,
                });
            }
            for (var x = 0; x < width; x++)
                px[y * width + x] = (cur[x * bpp] << 16) | (cur[x * bpp + 1] << 8) | cur[x * bpp + 2];
            (prev, cur) = (cur, prev);
        }
        return (width, height, px);

        static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }
}
