using OpenPhantasie.Client;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s216 (watch list, s212 / s214 III live): a modal panel over the Guild never starts inside its header (PHANTASIE,
/// ADVENTURERS GUILD, the key hint): it goes below the header when it fits above the help band, else up to the content's
/// top, where its opaque body covers the header whole. The III confirm question shows whole on the status line.
/// </summary>
public class ModalPanelPlacementTests
{
    private const int Top = UiOverlays.ContentTop + 2;

    [Theory]
    [InlineData(31, 116, null, null, 31)] // no header to keep clear: where it was
    [InlineData(31, 116, 20, 48, 48)] // III Guild (tall cards): the 10-line sheet fits under the header (48 + 116 = 164)
    [InlineData(31, 116, 26, 69, Top)] // I Guild: it does not fit under, so it covers the header from the top
    [InlineData(46, 86, 26, 69, 69)] // I Guild: the race list goes under the header
    [InlineData(Top, 146, 20, 48, Top)] // a full-height panel starts above the header already: unchanged
    [InlineData(80, 40, 20, 48, 80)] // a small panel under the header: unchanged
    public void KeepClearOf_NeverStartsInsideTheHeader(int top, int height, int? bandTop, int? bandBottom, int expected)
    {
        (int, int)? band = bandTop is int t && bandBottom is int b ? (t, b) : null;
        var placed = UiOverlays.KeepClearOf(top, height, band);
        Assert.Equal(expected, placed);
        Assert.True(placed + height <= UiOverlays.ContentBottom || placed == top);
        if (band is (int bt, int bb))
            Assert.False(placed > bt && placed < bb);
    }

    [Fact]
    public void ConfirmQuestion_StatusLine_IsTheWholeQuestion()
    {
        var prompt = P3ConfirmQuestion.PromptFor("Save this game in place of\rthe one saved before?");
        Assert.Equal(["Save this game in place of", "the one saved before?"], prompt.Lines);
        Assert.Equal("Save this game in place of the one saved before?", P3ConfirmQuestion.StatusLine(prompt));
        Assert.Equal("Are you sure?", P3ConfirmQuestion.StatusLine(P3ConfirmQuestion.PromptFor("Are you sure?")));
    }
}
