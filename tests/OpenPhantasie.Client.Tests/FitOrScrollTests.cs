using Microsoft.Xna.Framework;
using OpenPhantasie.Client;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s99 (user: never lose content to make a layout fit): names, lines and lists are shown whole — a second line or a
/// scrolling box where they are longer than their room, never a cut or a dropped row.
/// </summary>
public class FitOrScrollTests
{
    [Fact]
    public void CardName_OneLineWhenItFits_ElseEvenPieces()
    {
        Assert.Equal(["SWAT"], PartyStripLayout.NameLines("SWAT", 48));
        Assert.Equal(["FRANCIS"], PartyStripLayout.NameLines("FRANCIS", 48)); // 7 × 6 − 1 = 41 pixels in 44
        Assert.Equal(["AREN", "TINO"], PartyStripLayout.NameLines("ARENTINO", 48));
        Assert.Equal(["ARENTINO"], PartyStripLayout.NameLines("ARENTINO", 51)); // the sidebar's 51-pixel card
        Assert.Equal(["ABCDE", "FGHI"], PartyStripLayout.NameLines("ABCDEFGHI", 48));
        Assert.Equal(PartyStripLayout.CardHeight + PixelFont.LineHeight, PartyStripLayout.CardHeightFor(32, 2));
    }

    [Fact]
    public void CardNames_WrapOnlyWhenACardIsTooNarrow()
    {
        var area = new Rectangle(8, 34, 304, 200);
        var layout = PartyStripLayout.Compute(6, area, 52);
        Assert.Equal(1, PartyStripLayout.NameLinesNeeded(layout, ["SWAT", "LEFTY", "FRANCIS", "A", "B", "C"]));
        Assert.Equal(2, PartyStripLayout.NameLinesNeeded(layout, ["SWAT", "ARENTINO", "LEFTY", "A", "B", "C"]));
        var taller = PartyStripLayout.Compute(6, area, 52, nameLines: 2);
        Assert.All(taller.Cards, c => Assert.Equal(PartyStripLayout.CardHeight + PixelFont.LineHeight, c.Height));
    }

    [Fact]
    public void FieldSidebar_ThreeCardsOfFiftyOne_HoldAnEightLetterName()
    {
        var layout = PartyStripLayout.Compute(6, UiOverlays.FieldPartyArea, 52, gap: 1);
        Assert.False(layout.Lines);
        Assert.All(layout.Cards, c => Assert.Equal(51, c.Width));
        Assert.All(layout.Cards, c => Assert.True(UiOverlays.FieldPartyArea.Contains(c)));
        Assert.Equal(1, PartyStripLayout.NameLinesNeeded(layout, ["SWAT", "ARENTINO", "LEFTY", "FRANCIS", "FOLNAR", "ZANDOR"]));
    }

    [Fact]
    public void HudBand_OneLineEach_AsBefore_LongerOnesGoOnBelow()
    {
        Assert.Equal((UiOverlays.HelpY, UiOverlays.MessageY, 9), UiOverlays.HudRows(1, 1));
        Assert.Equal((UiOverlays.HelpY, UiOverlays.MessageY, 9), UiOverlays.HudRows(1, 2)); // message's 2nd line at 191
        Assert.Equal((UiOverlays.HelpY, UiOverlays.HelpY + 18, 9), UiOverlays.HudRows(2, 1));
        var (help, message, pitch) = UiOverlays.HudRows(2, 2);
        Assert.Equal(8, pitch);
        Assert.True(message + pitch + PixelFont.GlyphHeight <= 200); // the fourth line still on the screen
        Assert.Equal(help + 2 * pitch, message);
    }

    [Fact]
    public void Tooltip_ClearsTheHelpLine_NotTheMessage()
    {
        // s114: the combat hit tooltip was drawn over the round's key help without clearing it.
        var one = UiOverlays.TooltipBand(1);
        Assert.True(one.Top <= UiOverlays.HelpY - 1 && one.Bottom >= UiOverlays.HelpY + PixelFont.GlyphHeight);
        Assert.True(one.Bottom <= UiOverlays.MessageY);
        Assert.Equal(UiOverlays.MessageY, UiOverlays.TooltipBand(2).Bottom);
    }

    [Fact]
    public void Lines_KeepsSpacingWhenItFits_ElseWrapsWhole()
    {
        Assert.Equal(["  BEG MERCY"], UiOverlays.Lines("  BEG MERCY", 26));
        Assert.Equal(["ARENTINO TAKES 4", "DAMAGE!"], UiOverlays.Lines("ARENTINO TAKES 4 DAMAGE!", 16));
        Assert.Equal(string.Concat(UiOverlays.Lines("A RATHER LONG LINE OF TEXT", 7)).Replace(" ", ""),
            "A RATHER LONG LINE OF TEXT".Replace(" ", ""));
    }

    [Fact]
    public void SideBySideAnswers_OneRowWhileTheyFit_ElseGoOnWhole()
    {
        var (rows, spots) = UiOverlays.SideBySide(["YES", "NO"], 0, 48);
        Assert.Equal([">YES   NO"], rows);
        Assert.Equal([(0, 0, 4), (0, 6, 9)], spots);
        string[] many = ["FIRST ANSWER", "SECOND ANSWER", "THIRD ANSWER", "FOURTH ANSWER"];
        (rows, spots) = UiOverlays.SideBySide(many, 3, 30);
        Assert.All(rows, r => Assert.True(r.Length <= 30));
        Assert.Equal(many.Length, spots.Count);
        Assert.Equal(">FOURTH ANSWER", rows[spots[3].Row][spots[3].Start..spots[3].End]);
    }

    [Fact]
    public void PromptLines_AreAllKept()
    {
        var lines = UiOverlays.PromptLines(Enumerable.Range(1, 20).Select(i => $"LINE {i}"), 48);
        Assert.Equal(20, lines.Count);
    }

    [Fact]
    public void ListPage_OnlyWhenTheListIsLonger()
    {
        Assert.Null(ListPage.Of(1, 9, 0, 9));
        Assert.Equal(new ListPage(1, 9, 2, 12), ListPage.Of(1, 9, 2, 12));
    }

    [Fact]
    public void WornBox_ALongNameGetsLinesOfItsOwn_AndTheLayoutMakesRoomWhileItCan()
    {
        Assert.Equal(["SWORD +4"], UiOverlays.InventoryLayout.SlotNameLines("SWORD +4"));
        Assert.Equal(["", "LARGE SHIELD +1"], UiOverlays.InventoryLayout.SlotNameLines("LARGE SHIELD +1"));
        // I: three boxes, one of two lines: the gear line moves right, all nine carried rows still fit at 8 pixels.
        var a = UiOverlays.InventoryLayout.Arrange([2, 1, 1]);
        Assert.True(a.CarriedFit);
        Assert.Equal(1, a.Moved);
        Assert.Equal(19, a.Slot(0).Height);
        Assert.Equal(a.Slot(0).Bottom + 1, a.Slot(1).Y);
        // III: four boxes beside the 48-row portrait, two names on lines of their own: name, gear line and carried header
        // go to the right column, the nine rows still fit at 8 pixels; the sack keeps 3 rows and scrolls.
        var iii = UiOverlays.InventoryLayout.Arrange([2, 2, 1, 1], 48);
        Assert.True(iii.CarriedFit);
        Assert.Equal(3, iii.Moved);
        Assert.True(iii.Slot(3).Bottom < iii.CarriedRow(0).Y);
        Assert.Equal(3, iii.StashRowsShown);
    }

    /// <summary>s102: more long worn names than the left column holds: the column scrolls, no name is cut.</summary>
    [Fact]
    public void WornBoxes_TooManyLongNames_ScrollInsteadOfCutting()
    {
        // III: four boxes, every name on two lines (8 lines) beside the 48-row portrait: the carried rows don't fit.
        var lines = new[] { 2, 2, 2, 2 };
        Assert.False(UiOverlays.InventoryLayout.Arrange(lines, 48).CarriedFit);
        var shown = UiOverlays.InventoryLayout.SlotLinesThatFit(lines.Sum(), 48);
        Assert.InRange(shown, 2, lines.Sum() - 1); // at least one whole two-line box, fewer than all
        var view = UiOverlays.InventoryLayout.Arrange([shown], 48);
        Assert.True(view.CarriedFit);
        Assert.False(UiOverlays.InventoryLayout.Arrange([shown + 1], 48).CarriedFit); // the most that fits

        // The cursor's box is brought into view whole, its first line winning in a box taller than the view.
        var box = new ScrollBox();
        box.Set(lines.Sum(), shown);
        box.FollowRange(6, 7); // the fourth box
        Assert.InRange(6, box.Offset, box.Offset + shown - 1);
        Assert.InRange(7, box.Offset, box.Offset + shown - 1);
        box.FollowRange(0, 1);
        Assert.Equal(0, box.Offset);
        // Another member (TopFor) with the cursor on the same lines: forgetting the follow brings it back into view.
        box.FollowRange(6, 7);
        Assert.True(box.TopFor(2));
        box.ForgetFollow();
        box.FollowRange(6, 7);
        Assert.InRange(7, box.Offset, box.Offset + shown - 1);
        var small = new ScrollBox();
        small.Set(8, 1);
        small.FollowRange(4, 5);
        Assert.Equal(4, small.Offset);
    }

    [Fact]
    public void CardHp_BreaksAfterTheSlashOnlyWhenTooWide()
    {
        Assert.Equal(["11/11"], PartyStripLayout.HpLines("11/11", 41));
        Assert.Equal(["192/", "192"], PartyStripLayout.HpLines("192/192", 41)); // seven cards in combat
        Assert.Equal(["192/192"], PartyStripLayout.HpLines("192/192", 48));
        var seven = PartyStripLayout.Compute(7, new Rectangle(8, 26, 304, 200), 52);
        Assert.Equal(2, PartyStripLayout.NameLinesNeeded(seven, ["SWAT", "LEFTY", "A", "B", "C", "D", "AIR"],
            ["192/192", "1/1", "1/1", "1/1", "1/1", "1/1", "1/1"]));
    }
}
