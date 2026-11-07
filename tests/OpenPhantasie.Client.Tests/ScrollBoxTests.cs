using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;

namespace OpenPhantasie.Client.Tests;

/// <summary>s99 (user: never lose content to fit a layout): the reusable scroll box behind every scrolling list and text.</summary>
public class ScrollBoxTests
{
    [Fact]
    public void EverythingFits_NoBarAndNoScrolling()
    {
        var box = new ScrollBox();
        box.Set(5, 8);
        Assert.False(box.CanScroll);
        box.ScrollBy(3);
        Assert.Equal(0, box.Offset);
        Assert.Equal(new Rectangle(10, 0, 3, 40), box.Thumb(new Rectangle(10, 0, 3, 40)));
    }

    [Fact]
    public void Keys_RowPageHomeEnd_StayInRange()
    {
        var box = new ScrollBox();
        box.Set(20, 8);
        Assert.True(box.HandleKey(Keys.Down));
        Assert.Equal(1, box.Offset);
        box.HandleKey(Keys.PageDown);
        Assert.Equal(8, box.Offset); // a page is the box less one row, so a line stays for context
        box.HandleKey(Keys.End);
        Assert.Equal(12, box.Offset);
        box.HandleKey(Keys.Down);
        Assert.Equal(12, box.Offset);
        box.HandleKey(Keys.Home);
        Assert.Equal(0, box.Offset);
        box.HandleKey(Keys.Up);
        Assert.Equal(0, box.Offset);
        Assert.False(box.HandleKey(Keys.Enter));
    }

    [Fact]
    public void ShrinkingContent_ClampsTheOffset()
    {
        var box = new ScrollBox();
        box.Set(20, 8);
        box.End();
        box.Set(10, 8);
        Assert.Equal(2, box.Offset);
    }

    [Fact]
    public void Reveal_ScrollsTheLeastThatShowsTheRow()
    {
        var box = new ScrollBox();
        box.Set(20, 5);
        box.Reveal(7);
        Assert.Equal(3, box.Offset);
        box.Reveal(5);
        Assert.Equal(3, box.Offset);
        box.Reveal(1);
        Assert.Equal(1, box.Offset);
    }

    [Fact]
    public void Thumb_ShowsShareAndPlace_AndClicksMapBack()
    {
        var box = new ScrollBox();
        box.Set(20, 5);
        var track = new Rectangle(100, 10, 3, 40);
        Assert.Equal(new Rectangle(100, 10, 3, 10), box.Thumb(track)); // a quarter shown: a quarter of the track
        box.End();
        Assert.Equal(new Rectangle(100, 40, 3, 10), box.Thumb(track));
        Assert.Equal(0, box.OffsetAt(track, 10));
        Assert.Equal(box.MaxOffset, box.OffsetAt(track, 49));
        Assert.Equal(8, box.OffsetAt(track, 30)); // the middle of the track: about the middle offset
    }

    [Fact]
    public void Thumb_NeverShorterThanTheMinimum()
    {
        var box = new ScrollBox();
        box.Set(1000, 5);
        Assert.Equal(ScrollBox.MinThumb, box.Thumb(new Rectangle(0, 0, 3, 40)).Height);
    }

    [Fact]
    public void Track_IsTheBoxRightEdge() =>
        Assert.Equal(new Rectangle(57, 20, ScrollBox.BarWidth, 30), ScrollBox.Track(new Rectangle(10, 20, 50, 30)));

    [Fact]
    public void Follow_RevealsOnlyWhenTheCursorMoves()
    {
        var box = new ScrollBox();
        box.Set(20, 5);
        box.Follow(0);
        box.ScrollTo(10); // the wheel looks further down
        box.Follow(0);
        Assert.Equal(10, box.Offset); // the same cursor: the view stays where the wheel put it
        box.Follow(1);
        Assert.Equal(1, box.Offset);
    }
}
