using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

/// <summary>
/// A box that shows <see cref="Visible"/> of <see cref="Total"/> rows from <see cref="Offset"/> (s99, user: never lose
/// content to make a layout fit; longer content scrolls). Pure state and geometry: the owner keeps one per box across
/// frames, calls <see cref="Set"/> as it lays the box out, and draws the rows from <see cref="Offset"/>; the bar
/// (<see cref="UiOverlays.DrawScrollBar"/>) shows only when there is more than fits. The keys (<see cref="HandleKey"/>),
/// the wheel over the box and a click or drag on the bar all move <see cref="Offset"/>.
/// </summary>
public sealed class ScrollBox
{
    /// <summary>Width of the bar on the box's right edge.</summary>
    public const int BarWidth = 3;

    /// <summary>The thumb never gets shorter than this.</summary>
    public const int MinThumb = 4;

    public int Offset { get; private set; }
    public int Total { get; private set; }
    public int Visible { get; private set; } = 1;

    /// <summary>More rows than fit: the bar shows and the box scrolls.</summary>
    public bool CanScroll => Total > Visible;

    /// <summary>The largest <see cref="Offset"/>: the last row at the bottom.</summary>
    public int MaxOffset => Math.Max(0, Total - Visible);

    /// <summary>The content (<paramref name="total"/> rows) and the room (<paramref name="visible"/> rows); the offset is kept in range.</summary>
    public void Set(int total, int visible)
    {
        Total = Math.Max(0, total);
        Visible = Math.Max(1, visible);
        Offset = Math.Clamp(Offset, 0, MaxOffset);
    }

    public void ScrollTo(int offset) => Offset = Math.Clamp(offset, 0, MaxOffset);

    public void ScrollBy(int rows) => ScrollTo(Offset + rows);

    public void Home() => Offset = 0;

    /// <summary>The last rows (a log that shows its newest line at the bottom).</summary>
    public void End() => Offset = MaxOffset;

    /// <summary>Scroll the least that brings <paramref name="row"/> into view (a list's cursor).</summary>
    public void Reveal(int row)
    {
        if (row < Offset)
            ScrollTo(row);
        else if (row >= Offset + Visible)
            ScrollTo(row - Visible + 1);
    }

    private int? _followed;

    /// <summary>
    /// <see cref="Reveal"/> <paramref name="row"/> when it changed since the last call (a list's cursor, followed every
    /// frame), so the wheel or the bar can still show other rows until the cursor moves.
    /// </summary>
    public void Follow(int row)
    {
        if (_followed == row)
            return;
        _followed = row;
        Reveal(row);
    }

    /// <summary>
    /// <see cref="Follow(int)"/> for a block of rows <paramref name="row"/> … <paramref name="last"/> (an icon row, s151):
    /// when <paramref name="row"/> changed, scroll the least that shows the whole block (its first row when it is taller
    /// than the room).
    /// </summary>
    public void Follow(int row, int last)
    {
        if (_followed == row)
            return;
        _followed = row;
        Reveal(last);
        Reveal(row);
    }

    /// <summary>
    /// <see cref="Follow"/> for an entry over several rows (<paramref name="first"/>..<paramref name="last"/>): when it
    /// changed, scroll the least that shows it whole, its first row winning when it is taller than the box.
    /// </summary>
    public void FollowRange(int first, int last)
    {
        if (_followed == first)
            return;
        _followed = first;
        Reveal(last);
        Reveal(first);
    }

    /// <summary>Forget the row <see cref="Follow"/> / <see cref="FollowRange"/> saw last, so the next call reveals it.</summary>
    public void ForgetFollow() => _followed = null;

    private int? _shownFor;

    /// <summary>
    /// Back to the top when <paramref name="content"/> (what the box shows: an item, a page) changed since the last call;
    /// true when it did.
    /// </summary>
    public bool TopFor(int content)
    {
        if (_shownFor == content)
            return false;
        _shownFor = content;
        Home();
        return true;
    }

    /// <summary>Forget the content and cursor seen so far and go to the top (a screen opened afresh).</summary>
    public void Reset()
    {
        _shownFor = null;
        _followed = null;
        Home();
    }

    /// <summary>Up / Down a row, PgUp / PgDn a page, Home / End; true when the key is one of them (the box took it).</summary>
    public bool HandleKey(Keys key)
    {
        switch (key)
        {
            case Keys.Up: ScrollBy(-1); return true;
            case Keys.Down: ScrollBy(1); return true;
            case Keys.PageUp: ScrollBy(-Math.Max(1, Visible - 1)); return true;
            case Keys.PageDown: ScrollBy(Math.Max(1, Visible - 1)); return true;
            case Keys.Home: Home(); return true;
            case Keys.End: End(); return true;
            default: return false;
        }
    }

    /// <summary>The bar's track along the right edge of <paramref name="box"/>.</summary>
    public static Rectangle Track(Rectangle box) => new(box.Right - BarWidth, box.Y, BarWidth, box.Height);

    /// <summary>The thumb inside <paramref name="track"/>: its length is the shown share, its place the offset.</summary>
    public Rectangle Thumb(Rectangle track)
    {
        if (!CanScroll)
            return track;
        var length = Math.Clamp(track.Height * Visible / Total, Math.Min(MinThumb, track.Height), track.Height);
        var top = track.Y + (int)Math.Round((track.Height - length) * (double)Offset / MaxOffset);
        return new Rectangle(track.X, top, track.Width, length);
    }

    /// <summary>The offset that puts the thumb's middle at <paramref name="y"/> on <paramref name="track"/> (a click or a drag).</summary>
    public int OffsetAt(Rectangle track, int y)
    {
        if (!CanScroll)
            return 0;
        var length = Thumb(track).Height;
        var room = Math.Max(1, track.Height - length);
        return Math.Clamp((int)Math.Round((y - track.Y - length / 2.0) * MaxOffset / room), 0, MaxOffset);
    }
}

/// <summary>The bar of <see cref="Box"/> (drawn on <see cref="Track"/>): a click jumps there, a drag follows the pointer.</summary>
public sealed record ScrollBarTag(ScrollBox Box, Rectangle Track);

/// <summary>A scrolling box's area: the wheel over it scrolls <see cref="Box"/> (rows on top of it still take clicks).</summary>
public sealed record ScrollAreaTag(ScrollBox Box);
