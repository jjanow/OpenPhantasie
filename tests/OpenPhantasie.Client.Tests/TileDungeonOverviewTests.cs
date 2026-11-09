using Microsoft.Xna.Framework;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s171 (PLAN P3, QoL): III's tile dungeons get the auto-map back as a whole-level overview
/// (<see cref="UiOverlays.TileOverviewPlacement"/>, <see cref="UiOverlays.DrawTileDungeonOverview"/>), inside the view left
/// of the member cards, in the corner away from the party.
/// </summary>
public class TileDungeonOverviewTests
{
    // Game1.DrawTileDungeon on III (DOS CGA and Amiga packs alike): 8 x 5 glyphs, a 160 x 150 view at (4, ContentTop).
    private const int GridW = 24, GridH = 30, GlyphW = 8, GlyphH = 5, ViewLeft = 4;
    private static readonly int Cols = Math.Min(GridW, 160 / GlyphW);
    private static readonly int Rows = Math.Min(GridH, (UiOverlays.ContentBottom - UiOverlays.ContentTop) / GlyphH);
    private static readonly Rectangle View = new(ViewLeft, UiOverlays.ContentTop, Cols * GlyphW, Rows * GlyphH);

    private static Rectangle PartyOnScreen(int x, int y)
    {
        var x0 = Math.Clamp(x - Cols / 2, 0, GridW - Cols);
        var y0 = Math.Clamp(y - Rows / 2, 0, GridH - Rows);
        return new Rectangle(View.X + (x - x0) * GlyphW, View.Y + (y - y0) * GlyphH, GlyphW, GlyphH);
    }

    [Fact]
    public void EveryPartyCell_TheBoxCoversNoCard_NoCommandLine_NorTheParty_AndStaysInTheView()
    {
        for (var y = 0; y < GridH; y++)
        for (var x = 0; x < GridW; x++)
        {
            var party = PartyOnScreen(x, y);
            var l = UiOverlays.TileOverviewPlacement(View, party, GridW, GridH);
            // The cards (six members or seven with an elemental: FieldPartyScroll) and the command lines under them all
            // sit at FieldPartyArea.X and right of it.
            Assert.True(l.Box.Right < UiOverlays.FieldPartyArea.X, $"({x},{y}): {l.Box}");
            Assert.False(l.Box.Intersects(party), $"({x},{y}): {l.Box} over the party {party}");
            Assert.True(l.Box.X >= View.X && l.Box.Top >= UiOverlays.ContentTop && l.Box.Bottom <= UiOverlays.ContentBottom,
                $"({x},{y}): {l.Box}");
        }
    }

    [Fact]
    public void ThreeByTwoCells_TheWholeLevel_InAFrame()
    {
        var l = UiOverlays.TileOverviewPlacement(View, PartyOnScreen(3, 15), GridW, GridH);
        Assert.Equal((3, 2), (l.CellWidth, l.CellHeight));
        Assert.Equal(new Rectangle(l.Box.X + 2, l.Box.Y + 2, 72, 60), l.Grid); // all 24 x 30 cells, none cut
        Assert.Equal((76, 64), (l.Box.Width, l.Box.Height));
    }

    [Theory]
    [InlineData(3, 15, true, false)]  // party left, a little below the middle: top right
    [InlineData(23, 7, false, true)]  // party right, top: bottom left
    [InlineData(2, 2, true, true)]
    [InlineData(22, 28, false, false)]
    public void TheBox_TakesTheCornerFarthestFromTheParty(int x, int y, bool right, bool bottom)
    {
        var l = UiOverlays.TileOverviewPlacement(View, PartyOnScreen(x, y), GridW, GridH);
        Assert.Equal(right, l.Box.Right == UiOverlays.FieldPartyArea.X - 1);
        Assert.Equal(!right, l.Box.X == View.X);
        Assert.Equal(bottom, l.Box.Bottom == View.Bottom);
        Assert.Equal(!bottom, l.Box.Y == View.Y);
    }

    [Fact]
    public void DominantColour_TheCommonestOpaquePixel()
    {
        var clear = new Color(0, 0, 0, 0);
        Assert.Equal(Color.Red, UiOverlays.DominantColour([clear, clear, clear, Color.Red, Color.Red, Color.Blue], Color.Gray));
        Assert.Equal(Color.Gray, UiOverlays.DominantColour([clear, clear], Color.Gray));
        Assert.Equal(Color.Gray, UiOverlays.DominantColour([], Color.Gray));
    }

    [Fact]
    public void DosCgaPalette1_TheOverviewsColoursStayApart()
    {
        // The dungeon map shows in CGA palette 1 (dos-cga.json scene_palettes), the frame index-mapped from the cut
        // palette 0 (s169). s170 dropped the I / II box partly because its greys all fell to black there.
        var palette = ScreenPalette.Mapped(["#000000", "#55FF55", "#FF5555", "#FFFF55"], ["#000000", "#55FFFF", "#FF55FF", "#FFFFFF"]);
        var stone = new Color(255, 255, 85); // glyph 46's commonest pixel (SHAPES.CGA, index 3)
        var floor = new Color(255, 85, 85);  // glyph 50's (index 2)
        var shown = new[] { UiOverlays.OverviewFill, stone, floor, UiOverlays.OverviewParty }.Select(palette.Nearest).ToList();
        Assert.Equal(4, shown.Distinct().Count());
        Assert.NotEqual(palette.Nearest(UiOverlays.OverviewFill), palette.Nearest(UiOverlays.OverviewEdge));
    }
}
