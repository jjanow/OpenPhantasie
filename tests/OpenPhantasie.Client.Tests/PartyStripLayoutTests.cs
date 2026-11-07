using Microsoft.Xna.Framework;
using OpenPhantasie.Client;

namespace OpenPhantasie.Client.Tests;

/// <summary>P2: the party strip draws all seven slots (six + summoned elemental) and never leaves its region.</summary>
public class PartyStripLayoutTests
{
    // The four regions the Client draws the strip in.
    public static TheoryData<int, int, int, int, int> Regions => new()
    {
        { 8, 34, 304, 56, 52 },   // combat
        { 166, 42, 150, 120, 50 }, // overworld / dungeon sidebar
        { 212, 32, 102, 66, 50 },  // town sidebar
        { 20, 74, 280, 56, 56 },   // guild
    };

    [Theory]
    [MemberData(nameof(Regions))]
    public void EveryCount_UpToSeven_FitsInsideTheRegion(int x, int y, int w, int h, int maxCard)
    {
        var area = new Rectangle(x, y, w, h);
        for (var n = 1; n <= 7; n++)
        {
            var layout = PartyStripLayout.Compute(n, area, maxCard);
            Assert.Equal(n, layout.Cards.Count);
            foreach (var card in layout.Cards)
                Assert.True(area.Contains(card), $"n={n}: {card} outside {area}");
            for (var i = 0; i < n; i++)
            for (var j = i + 1; j < n; j++)
                Assert.False(layout.Cards[i].Intersects(layout.Cards[j]), $"n={n}: cards {i} and {j} overlap");
        }
    }

    [Fact]
    public void Combat_SevenPortraitCardsInOneRow()
    {
        var layout = PartyStripLayout.Compute(7, new Rectangle(8, 34, 304, 56), 52);
        Assert.False(layout.Lines);
        Assert.All(layout.Cards, c => Assert.Equal(34, c.Y));
        Assert.All(layout.Cards, c => Assert.True(c.Width >= PartyStripLayout.PortraitSize));
    }

    [Fact]
    public void Sidebar_SevenCardsUseTwoRows()
    {
        var layout = PartyStripLayout.Compute(7, new Rectangle(166, 42, 150, 120), 50);
        Assert.False(layout.Lines);
        Assert.Equal(2, layout.Cards.Select(c => c.Y).Distinct().Count());
    }

    [Fact]
    public void Town_TooShortForPortraits_FallsBackToOneLineRows()
    {
        var layout = PartyStripLayout.Compute(7, new Rectangle(212, 32, 102, 66), 50);
        Assert.True(layout.Lines);
    }

    [Fact]
    public void MoreThanSeven_IsCappedAtSlotSeven() =>
        Assert.Equal(7, PartyStripLayout.Compute(9, new Rectangle(0, 0, 320, 200)).Cards.Count);

    [Fact]
    public void IiCombatRow_CentresEachCardOnTheStPortraitSlot_s98()
    {
        // ST 0x181ca: portraits at gap + i·(gap + 32), gap = (320 − 32·n)/(n + 1) (0x1814e); the row spreads out again
        // after a death instead of packing from the left.
        var area = new Rectangle(8, 34, 304, 56);
        for (var n = 1; n <= 6; n++)
        {
            var layout = PartyStripLayout.ComputeEvenRow(n, area, 52);
            Assert.False(layout.Lines);
            Assert.Equal(n, layout.Cards.Count);
            for (var i = 0; i < n; i++)
            {
                var card = layout.Cards[i];
                Assert.Equal(DefeatScene.SlotX(n, i) + 16, card.X + card.Width / 2);
                Assert.True(area.Contains(card), $"n={n}: {card} outside {area}");
                Assert.True(card.Width >= PartyStripLayout.MinCardWidth);
                if (i > 0)
                    Assert.False(layout.Cards[i - 1].Intersects(card), $"n={n}: cards {i - 1} and {i} overlap");
            }
        }
        Assert.Equal(new Rectangle(134, 34, 52, 54), PartyStripLayout.ComputeEvenRow(1, area, 52).Cards[0]); // centred
    }

    // s99 (user: make the cards taller, nothing cropped): III Amiga's 32×48 portraits get 70-row cards in these regions.
    public static TheoryData<int, int, int, int, int, int> TallRegions => new()
    {
        { 8, 26, 304, 70, 52, 7 },   // combat: one row
        { 20, 58, 280, 72, 56, 7 },  // guild: one row
        { 212, 32, 102, 82, 50, 2 }, // town sidebar: one row of two (more members get one-line rows, as in I)
    };

    [Theory]
    [MemberData(nameof(TallRegions))]
    public void TallPortraits_WholeCardsInsideTheRegion_s99(int x, int y, int w, int h, int maxCard, int cards)
    {
        var area = new Rectangle(x, y, w, h);
        for (var n = 1; n <= 7; n++)
        {
            var layout = PartyStripLayout.Compute(n, area, maxCard, portraitHeight: 48);
            Assert.Equal(n, layout.Cards.Count);
            Assert.Equal(n > cards, layout.Lines);
            foreach (var card in layout.Cards)
                Assert.True(area.Contains(card), $"n={n}: {card} outside {area}");
            if (layout.Lines)
                continue;
            Assert.All(layout.Cards, c => Assert.Equal(70, c.Height));
            // The whole portrait (2 rows down) sits above the name line, the HP line inside the card.
            Assert.All(layout.Cards, c => Assert.True(PartyStripLayout.NameTop(c, 48) >= c.Y + 2 + 48));
            Assert.All(layout.Cards, c => Assert.True(PartyStripLayout.NameTop(c, 48) + PixelFont.LineHeight +
                PixelFont.GlyphHeight <= c.Bottom));
        }
    }

    [Fact]
    public void TallPortraits_IiEvenRowKeepsWholeCards_s99()
    {
        var layout = PartyStripLayout.ComputeEvenRow(6, new Rectangle(8, 26, 304, 70), 52, portraitHeight: 48);
        Assert.False(layout.Lines);
        Assert.All(layout.Cards, c => Assert.Equal(70, c.Height));
    }

    [Fact]
    public void SquarePortraits_CardsAsBefore_s99()
    {
        // I and II (32×32) keep the 54-row card and the name / HP lines at +36 / +45.
        Assert.Equal(PartyStripLayout.CardHeight, PartyStripLayout.CardHeightFor(32));
        Assert.Equal(70, PartyStripLayout.CardHeightFor(48));
        var area = new Rectangle(8, 34, 304, 56);
        Assert.Equal(PartyStripLayout.Compute(6, area, 52).Cards, PartyStripLayout.Compute(6, area, 52, 32).Cards);
        Assert.Equal(new Rectangle(8, 34, 48, 54), PartyStripLayout.Compute(6, area, 52).Cards[0]);
        Assert.Equal(34 + 36, PartyStripLayout.NameTop(new Rectangle(8, 34, 48, 54), 32));
    }

    [Fact]
    public void PortraitTarget_NeverCropsOrStretches_s99()
    {
        // The picture of the box's shape fills it (I, II), as before.
        Assert.Equal(new Rectangle(10, 20, 32, 32), UiOverlays.PortraitTarget(32, 32, new Rectangle(10, 20, 32, 32)));
        // III's 32×48 in its own 32×48 box: 1:1.
        Assert.Equal(new Rectangle(10, 20, 32, 48), UiOverlays.PortraitTarget(32, 48, new Rectangle(10, 20, 32, 48)));
        // A 32×32 picture in a 32×48 box: 1:1 at the top, not stretched.
        Assert.Equal(new Rectangle(10, 20, 32, 32), UiOverlays.PortraitTarget(32, 32, new Rectangle(10, 20, 32, 48)));
        // III's 32×48 elemental on a 32×32 body-figure card: whole at half size, centred.
        Assert.Equal(new Rectangle(18, 24, 16, 24), UiOverlays.PortraitTarget(32, 48, new Rectangle(10, 20, 32, 32)));
    }
}
