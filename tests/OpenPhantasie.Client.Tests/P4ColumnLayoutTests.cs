using Microsoft.Xna.Framework;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s157: IV's text column beside a picture and the Command strip (<see cref="P4DungeonScreen.LayOutColumn"/>): the key
/// help goes under the picture and the strip where it fits, the party lines under a short picture, and the old column
/// layout stays where nothing fits. <see cref="P4DungeonScreen.HelpLines"/> breaks help lines between key groups.
/// </summary>
public class P4ColumnLayoutTests
{
    // The street: a 320 × 348 pack picture at half scale from (4, 4); the strip 80 layout pixels wide, 174 high.
    private static readonly Rectangle Street = new(4, 4, 160, 174);
    private const int Left = 168, Right = 232, StripBottom = 178;

    private static int Rows(string text, int cols) => P4DungeonScreen.HelpLines(text, cols).Count;

    [Fact]
    public void Street_HelpGoesUnderPictureAndStrip_InTwoRows()
    {
        const string help = "1-9 door  T trade  D share out  F profile  C spell  U use  S system  F5 save";
        var l = P4DungeonScreen.LayOutColumn(Street, Left, Right, StripBottom, c => Rows(help, c), _ => 12);
        Assert.Equal(4, l.NoteX);
        Assert.Equal(51, l.NoteCols);
        Assert.Equal(2, Rows(help, l.NoteCols));
        Assert.True(l.NoteTop >= Street.Bottom + 2);
        Assert.True(l.NoteTop + PixelFont.LineHeight + PixelFont.GlyphHeight <= 196);
        // The party stays in the column (no room under the picture), with more rows than the old 6, above the help.
        Assert.Equal(Left, l.Party.X);
        Assert.True(l.Party.Height > 6 * PixelFont.LineHeight + 4);
        Assert.True(l.Party.Bottom <= l.NoteTop - 3);
        Assert.Equal(l.Party.Y - 4, l.Box.Bottom);
        Assert.True(l.Box.Height >= l.Party.Height - 2 * PixelFont.LineHeight);
    }

    [Fact]
    public void ShortPicture_PartyLinesGoUnderIt_OneRowEach()
    {
        var guild = new Rectangle(4, 4, 160, 90);
        var l = P4DungeonScreen.LayOutColumn(guild, Left, Right, StripBottom, _ => 1, c => c >= 31 ? 4 : 12);
        Assert.Equal(4, l.Party.X);
        Assert.Equal(Right - 4, l.Party.Width);
        Assert.Equal(4 * PixelFont.LineHeight + 4, l.Party.Height);
        Assert.True(l.Party.Y >= guild.Bottom + 4);
        Assert.Equal(new Rectangle(Left, 4, Right - Left, l.Party.Y - 8), l.Box);
    }

    [Fact]
    public void TallListWindow_HelpGoesLeftOfTheStrip()
    {
        // The spell list's 360-row window ends at 196: under the location map's view (to y 164) the help takes the width
        // left of the strip.
        var view = new Rectangle(4, 4, 152, 160);
        var l = P4DungeonScreen.LayOutColumn(view, Left, Right, 196, c => c >= 51 ? 2 : 3, _ => 4);
        Assert.Equal(4, l.NoteX);
        Assert.Equal((Right - 6) / PixelFont.Advance, l.NoteCols);
    }

    [Fact]
    public void NoStripOrNoRoom_KeepsTheColumnLayout()
    {
        var l = P4DungeonScreen.LayOutColumn(Street, Left, 312, null, _ => 3, _ => 8);
        Assert.Equal(Left + 1, l.NoteX);
        Assert.Equal(196 - 3 * PixelFont.LineHeight, l.NoteTop);
        Assert.Equal(6 * PixelFont.LineHeight + 4, l.Party.Height);
        var tall = new Rectangle(4, 4, 160, 190);
        var t = P4DungeonScreen.LayOutColumn(tall, Left, Right, StripBottom, _ => 2, _ => 8);
        Assert.Equal(Left + 1, t.NoteX);
    }

    [Fact]
    public void FightSpellList_ColumnGivesWay_BesideTheField()
    {
        // s160 (user s159): the combat spell list opens at the panel's place right of the field (336 × 300 pack at half
        // scale from (4, 4)); its 360-row window reaches row 197. The column narrows to the field's side, the help goes
        // under the field in the width left of the list, and nothing reaches the list.
        var field = new Rectangle(4, 4, 168, 150);
        foreach (var listWidth in new[] { 136, 159 })
        {
            var stripLeft = 316 - (int)Math.Ceiling((listWidth + 1) * 0.5f);
            Assert.True(stripLeft > field.Right + 4, $"{listWidth}");
            var right = stripLeft - 4;
            var l = P4DungeonScreen.LayOutColumn(field, field.Right + 4, right, 197, c => c >= 30 ? 1 : 2, c => 4 * (c >= 30 ? 1 : 3));
            Assert.Equal(4, l.NoteX);
            Assert.Equal((right - 6) / PixelFont.Advance, l.NoteCols);
            Assert.True(l.NoteTop >= field.Bottom + 2);
            Assert.True(l.NoteTop + PixelFont.GlyphHeight <= 196);
            Assert.True(l.Box.Right <= right && l.Party.Right <= right, $"{listWidth}");
            Assert.Equal(field.Right + 4, l.Box.X);
            Assert.True(l.Party.Bottom <= l.NoteTop - 3);
        }
        // Closed, the fight's full-width column comes back (no strip in a fight).
        var closed = P4DungeonScreen.LayOutColumn(field, field.Right + 4, 316, null, _ => 1, _ => 4);
        Assert.Equal((field.Right + 4, 316 - field.Right - 4), (closed.Box.X, closed.Box.Width));
        Assert.Equal(field.Right + 5, closed.NoteX);
    }

    [Fact]
    public void HelpLines_BreakBetweenKeyGroups_KeepingTheirSpacing()
    {
        var lines = P4DungeonScreen.HelpLines("Numpad/arrows move  E examine  F profile  C spell  U use  S system", 30);
        Assert.Equal(["Numpad/arrows move  E examine", "F profile  C spell  U use", "S system"], lines);
        Assert.Equal(["Space continue"], P4DungeonScreen.HelpLines("Space continue", 30));
        // A group wider than the line wraps by words; nothing is lost.
        var narrow = P4DungeonScreen.HelpLines("Numpad/arrows move  E examine", 10);
        Assert.Equal("Numpad/arrows move E examine".Replace(" ", ""), string.Concat(narrow).Replace(" ", ""));
    }

    /// <summary>
    /// s185 (Pad leftovers): the bank's entries in the 9-column text column beside the picture and the Command strip read
    /// "W:WITHDRA / W"; they now break after the key's ':' with the rest indented, and a row that fits is unchanged.
    /// </summary>
    [Fact]
    public void BoxLineRows_AMenuEntry_BreaksAfterItsKey_NeverInsideAWord()
    {
        Assert.Equal(["2 W:", " Withdraw"], P4DungeonScreen.BoxLineRows(" 2 W:Withdraw", 9));
        Assert.Equal([">1 D:", " Deposit"], P4DungeonScreen.BoxLineRows(">1 D:Deposit", 9));
        Assert.Equal(["3 L:Leave"], P4DungeonScreen.BoxLineRows(" 3 L:Leave", 9));
        Assert.Equal([">1 D:Deposit"], P4DungeonScreen.BoxLineRows(">1 D:Deposit", 20));
        Assert.Equal([">1", " D:Deposit"], P4DungeonScreen.BoxLineRows(">1 D:Deposit", 11)); // whole, indented
        Assert.Equal([""], P4DungeonScreen.BoxLineRows("", 9));
        // Prose (no cursor-mark column) wraps as before.
        Assert.Equal(UiOverlays.Wrap("PIP has grown older.", 9), P4DungeonScreen.BoxLineRows("PIP has grown older.", 9));
    }

    [Theory]
    [InlineData(" 2 W:Withdraw")]
    [InlineData(">4 Leave the Guild of Pendragon")]
    [InlineData(" 1 ABCDEFGHIJKLMNOPQRSTU")] // a run with no break: cut between characters, still nothing lost
    [InlineData(">7 Long Sword/Short Sword-Spear")]
    public void BoxLineRows_EveryRowFits_NothingIsLost(string entry)
    {
        for (var cols = 4; cols <= 16; cols++)
        {
            var rows = P4DungeonScreen.BoxLineRows(entry, cols);
            Assert.All(rows, r => Assert.True(PixelFont.Measure(r) <= cols * PixelFont.Advance, $"{cols}: \"{r}\""));
            Assert.Equal(entry.Replace(" ", ""), string.Concat(rows).Replace(" ", ""));
            // A word that fits a row is never split: each one is found whole in a row.
            foreach (var word in entry.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => PixelFont.Measure(P4DungeonScreen.EntryIndent + w) <= cols * PixelFont.Advance))
                Assert.Contains(rows, r => r.Split(' ').Contains(word));
        }
    }
}
