using OpenPhantasie.Client;

namespace OpenPhantasie.Client.Tests;

/// <summary>s206: the Guild screen's member list keeps each entry on one line when the columns can be made to fit.</summary>
public class GuildListColumnsTests
{
    private static UiOverlays.GuildListLayout For(string left, string right) =>
        UiOverlays.GuildListLayout.For(PixelFont.Measure(left), PixelFont.Measure(right));

    [Fact]
    public void ShortEntries_KeepTheUsualPanelAndColumn()
    {
        var l = For("SWAT DWARF FIGHTER L1", "ZANDOR ELF WIZARD L1");
        Assert.Equal((8, 304, 164, false), (l.PanelX, l.PanelWidth, l.SecondX, l.Wraps));
    }

    [Fact]
    public void AWideRangerRow_FitsUnwrapped_WithTheSecondColumnRightOfIt()
    {
        // s205: "ARENTINO HUMAN RANGER L10" wrapped its "L10" onto a line of its own.
        var left = PixelFont.Measure("ARENTINO HUMAN RANGER L10");
        var right = PixelFont.Measure("FOLNAR HUMAN PRIEST L10");
        var l = UiOverlays.GuildListLayout.For(left, right);
        Assert.False(l.Wraps);
        Assert.True(l.SecondX >= l.ListLeft + left + 9);
        Assert.True(l.SecondX + right <= l.ListRight);
        Assert.True(l.PanelX >= 0 && l.PanelX + l.PanelWidth <= 320);
    }

    [Fact]
    public void EntriesTooWideForAnyPanel_StillWrap()
    {
        Assert.True(UiOverlays.GuildListLayout.For(200, 150).Wraps);
    }
}
