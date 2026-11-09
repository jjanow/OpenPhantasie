using Microsoft.Xna.Framework;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s171 (PLAN P3, QoL): III's tile dungeons get the auto-map back as a whole-level overview
/// (<see cref="UiOverlays.TileOverviewPlacement"/>, <see cref="UiOverlays.DrawDungeonOverview"/>), inside the view left
/// of the member cards, in the corner away from the party. s172: I / II's glyph view places it the same way.
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

    // Game1.DrawDungeonGlyphs on I / II (s172): 15 x 18 of the 33 x 38 level, 5 x 4 Amiga glyphs drawn 2x (10 x 8).
    private const int P1W = 33, P1H = 38, P1Cols = 15, P1Rows = 18, P1GlyphW = 10, P1GlyphH = 8;
    private static readonly Rectangle P1View = new(ViewLeft, UiOverlays.ContentTop, P1Cols * P1GlyphW, P1Rows * P1GlyphH);

    private static Rectangle P1PartyOnScreen(int x, int y)
    {
        var x0 = Math.Clamp(x - P1Cols / 2, 0, P1W - P1Cols);
        var y0 = Math.Clamp(y - P1Rows / 2, 0, P1H - P1Rows);
        return new Rectangle(P1View.X + (x - x0) * P1GlyphW, P1View.Y + (y - y0) * P1GlyphH, P1GlyphW, P1GlyphH);
    }

    [Fact]
    public void PhantasieI_EveryPartyCell_TheBoxCoversNoCard_NorTheParty_AndStaysInTheView()
    {
        // s171's shot p1-s171-automap-box-over-cards.png: the old box at (222, 16) lay over cards 2, 3 and 5.
        for (var y = 0; y < P1H; y++)
        for (var x = 0; x < P1W; x++)
        {
            var party = P1PartyOnScreen(x, y);
            var l = UiOverlays.TileOverviewPlacement(P1View, party, P1W, P1H);
            Assert.True(l.Box.Right < UiOverlays.FieldPartyArea.X, $"({x},{y}): {l.Box}");
            Assert.False(l.Box.Intersects(UiOverlays.FieldPartyArea), $"({x},{y}): {l.Box}");
            Assert.False(l.Box.Intersects(party), $"({x},{y}): {l.Box} over the party {party}");
            Assert.True(P1View.Contains(l.Box), $"({x},{y}): {l.Box} outside the view {P1View}");
        }
    }

    [Fact]
    public void PhantasieI_TheWholeLevel_TwoByOneCells_NoneCut()
    {
        var l = UiOverlays.TileOverviewPlacement(P1View, P1PartyOnScreen(16, 19), P1W, P1H);
        Assert.Equal((2, 1), (l.CellWidth, l.CellHeight));
        Assert.Equal((P1W * 2, P1H), (l.Grid.Width, l.Grid.Height)); // all 33 x 38 cells
        Assert.Equal((70, 42), (l.Box.Width, l.Box.Height));
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
