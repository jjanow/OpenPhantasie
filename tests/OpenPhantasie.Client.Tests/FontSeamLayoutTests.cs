using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// T1.5 rest: the layouts that counted characters (inventory wrap and stash header, the Inspect EQUIPMENT row, the DOS
/// save path tail) now measure through the font. With the default 5×7 font (6 px every glyph) the result is the old one.
/// </summary>
public class FontSeamLayoutTests
{
    private static IFontSource Default => PixelFont.Remaster5x7;

    /// <summary>Variable widths: i advances 2, W advances 7, space 3, anything else falls back to ? (advance 4).</summary>
    private static IFontSource Narrow => new BitmapFont(FontDefinition.Parse("""
        {
          "id": "test-narrow", "height": 3, "line_height": 5, "space_advance": 3, "fallback": "?",
          "glyphs": {
            "?": { "rows": ["###", "..#", ".#."] },
            "i": { "rows": ["#", "#", "#"] },
            "W": { "rows": ["#...#", "#.#.#", ".#.#."], "advance": 7 }
          }
        }
        """));

    // The character rules before the move, kept here to compare against; since s99 a word longer than the line goes on,
    // in pieces of the line's width, instead of being cut.
    private static IEnumerable<string> OldWrap(string text, int width)
    {
        var line = "";
        foreach (var word in (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = word;
            while (w.Length > width)
            {
                if (line.Length > 0)
                    yield return line;
                line = "";
                yield return w[..width];
                w = w[width..];
            }
            if (w.Length == 0)
                continue;
            if (line.Length == 0)
                line = w;
            else if (line.Length + 1 + w.Length <= width)
                line += " " + w;
            else
            {
                yield return line;
                line = w;
            }
        }
        if (line.Length > 0)
            yield return line;
    }

    private static string OldTail(string s, int max) => s.Length <= max ? s : "..." + s[^(max - 3)..];

    [Theory]
    [InlineData("THE ARMORY PAYS 12 GP", 10)]
    [InlineData("ABCDEFGH", 5)]
    [InlineData("", 25)]
    [InlineData("   ", 25)]
    [InlineData("A RING OF REGENERATION HEALS ITS WEARER A LITTLE EVERY ROUND, EVEN IN THE DUNGEON", 25)]
    [InlineData("SUPERCALIFRAGILISTICEXPIALIDOCIOUSLY LONG WORD", 25)]
    public void Wrap_DefaultFont_IsTheOldCharacterRule(string text, int width) =>
        Assert.Equal(OldWrap(text, width), UiOverlays.Wrap(text, width, Default));

    [Fact]
    public void Wrap_VariableFont_MeasuresWidths()
    {
        // 4 columns = 24 px. "iiii iiii" = 8 + 3 + 8 = 19 px: one line (the old rule broke it at 4 characters).
        Assert.Equal(["iiii iiii"], UiOverlays.Wrap("iiii iiii", 4, Narrow));
        // WWWW = 28 px > 24: whole glyphs to a line (3 × 7 = 21), the rest goes on (s99: nothing cut).
        Assert.Equal(["WWW", "W i"], UiOverlays.Wrap("WWWW i", 4, Narrow));
    }

    [Theory]
    [InlineData("/home/user/.local/share/OpenPhantasie/dos", 48)]
    [InlineData("/home/administrator/.local/share/OpenPhantasie/dos/a/very/long/folder_", 48)]
    [InlineData("C:\\GAMES", 48)]
    [InlineData("ABCDEFGHIJ", 10)]
    [InlineData("ABCDEFGHIJK", 10)]
    public void Tail_DefaultFont_IsTheOldCharacterRule(string text, int max) =>
        Assert.Equal(OldTail(text, max), PixelFont.Tail(text, max * PixelFont.Advance, Default));

    [Fact]
    public void Tail_VariableFont_KeepsWhatFits()
    {
        // 24 px: "..." is 3 × 4 = 12, so 12 px of the end stay: six i (2 px each).
        Assert.Equal("...iiiiii", PixelFont.Tail("WWWWiiiiii", 24, Narrow));
        Assert.Equal("iiii", PixelFont.Tail("iiii", 24, Narrow));
        Assert.Equal("", PixelFont.Tail("", 24, Narrow));
    }

    [Fact]
    public void CutLength_CountsWholeGlyphsThatFit()
    {
        Assert.Equal(4, PixelFont.CutLength("ABCDEFG", 24, Default));
        Assert.Equal(4, PixelFont.CutLength("ABCDEFG", 29, Default));
        Assert.Equal(3, PixelFont.CutLength("WWWW", 24, Narrow));
        Assert.Equal(0, PixelFont.CutLength("", 24, Narrow));
    }

    [OriginalDataFact]
    public void InspectSheet_EquipmentRow_PacksAsBefore()
    {
        // The sample's three gear cells; the old rule packs cells while row + 2 + cell ≤ 35 characters.
        var lines = InspectDialog.SheetLines(ClientSessions.Fight().Party.Members[0], ClientSessions.Data);
        var at = lines.ToList().IndexOf("EQUIPMENT");
        Assert.True(at > 0);
        var rows = lines.Skip(at + 1).TakeWhile(l => !l.StartsWith("runes")).ToList();
        Assert.All(rows, r => Assert.True(r.Length <= 35, r));
        Assert.All(rows.Zip(rows.Skip(1)), p =>
            Assert.True(p.First.Length + 2 + p.Second.Split("  ")[0].Length > 35, $"{p.First} | {p.Second}"));
    }
}
