using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

/// <summary>
/// The mouse in virtual-screen pixels (presentation only; it drives the same commands as the keys). Fed once per frame by
/// <see cref="Update"/> with the pointer already mapped through <see cref="VirtualScreen.WindowToVirtual"/> (null outside the
/// picture or when the window is not active).
/// </summary>
/// <remarks>
/// <para><b>For new screens (e.g. the drag-and-drop inventory):</b> draw, and register each clickable rectangle with
/// <see cref="HitMap.Add"/> (a <see cref="HitRegion"/> with a tag of your own type: a slot, a card…). In the next Update read
/// this class: <see cref="Position"/> + <see cref="HitMap.At"/> for hover, <see cref="LeftClick"/> (the press point of a
/// press-and-release that did not drag) for clicks, <see cref="RightClick"/> for back, <see cref="WheelSteps"/> for scrolling.</para>
/// <para>Drag and drop: a left press that moves more than <see cref="DragThreshold"/> pixels starts a drag —
/// <see cref="DragStarted"/> is true for that one frame and <see cref="DragOrigin"/> holds the press point (look up what is
/// under it and keep it in <see cref="DragPayload"/>); while <see cref="Dragging"/>, draw the carried thing at
/// <see cref="Position"/>; on release <see cref="DragEnded"/> gives (origin, drop point) for one frame (drop point = last
/// position inside the picture) and the payload is still readable that frame. A drag never also counts as a click.</para>
/// </remarks>
public sealed class MouseInput
{
    /// <summary>Pixels (virtual) a held left button must move before it is a drag rather than a click.</summary>
    public const int DragThreshold = 3;

    /// <summary>One wheel notch in MonoGame's <c>ScrollWheelValue</c> units.</summary>
    public const int WheelNotch = 120;

    private bool _prevLeft;
    private bool _prevRight;
    private int? _prevWheel;
    private int _wheelRemainder;
    private Point? _press;
    private Point? _lastInside;

    /// <summary>Pointer position in virtual pixels, or null when it is outside the picture.</summary>
    public Point? Position { get; private set; }

    /// <summary>The pointer moved this frame (inside the picture both frames). Hover acts only on this, never on a still pointer.</summary>
    public bool Moved { get; private set; }

    public bool LeftDown { get; private set; }
    public bool RightDown { get; private set; }

    /// <summary>A button went down this frame.</summary>
    public bool AnyPressed { get; private set; }

    /// <summary>The left button went down this frame (anywhere, a drag's start too).</summary>
    public bool LeftPressed { get; private set; }

    /// <summary>The left button came up this frame (anywhere, a drag's end too).</summary>
    public bool LeftReleased { get; private set; }

    /// <summary>Left press-and-release without a drag finished this frame: the press point. Null otherwise.</summary>
    public Point? LeftClick { get; private set; }

    /// <summary>Right button released this frame after a press inside the picture.</summary>
    public bool RightClick { get; private set; }

    /// <summary>Wheel notches this frame: positive = up / away from the user.</summary>
    public int WheelSteps { get; private set; }

    /// <summary>Where the current drag began (the left press point), while a drag is under way.</summary>
    public Point? DragOrigin { get; private set; }

    public bool Dragging => DragOrigin is not null;

    /// <summary>True on the one frame a drag starts.</summary>
    public bool DragStarted { get; private set; }

    /// <summary>The frame a drag is dropped: where it began and where it was let go.</summary>
    public (Point From, Point To)? DragEnded { get; private set; }

    /// <summary>What the consumer picked up when the drag started (free for the screen to set; cleared one frame after the drop).</summary>
    public object? DragPayload { get; set; }

    public void Update(Point? position, bool left, bool right, int wheelValue)
    {
        if (DragEnded is not null)
            DragPayload = null; // the drop frame has passed
        LeftClick = null;
        RightClick = false;
        DragStarted = false;
        DragEnded = null;

        Moved = position is { } p && Position is { } before && p != before;
        Position = position;
        if (position is not null)
            _lastInside = position;
        AnyPressed = (left && !_prevLeft) || (right && !_prevRight);
        LeftPressed = left && !_prevLeft;
        LeftReleased = !left && _prevLeft;
        LeftDown = left;
        RightDown = right;

        if (left && !_prevLeft)
            _press = position; // a press outside the picture is not a click or a drag
        if (left && _press is { } from && DragOrigin is null && _lastInside is { } now
            && Math.Max(Math.Abs(now.X - from.X), Math.Abs(now.Y - from.Y)) > DragThreshold)
        {
            DragOrigin = from;
            DragStarted = true;
        }
        if (!left && _prevLeft)
        {
            if (DragOrigin is { } origin)
                DragEnded = (origin, _lastInside ?? origin);
            else if (_press is { } at && position is not null)
                LeftClick = at;
            DragOrigin = null;
            _press = null;
        }
        if (!right && _prevRight && position is not null)
            RightClick = true;

        WheelSteps = 0;
        if (_prevWheel is { } prev)
        {
            _wheelRemainder += wheelValue - prev;
            WheelSteps = _wheelRemainder / WheelNotch;
            _wheelRemainder -= WheelSteps * WheelNotch;
        }
        _prevWheel = wheelValue;
        if (position is null)
            WheelSteps = 0;

        _prevLeft = left;
        _prevRight = right;
    }
}

/// <summary>
/// A clickable rectangle on the virtual screen, registered while drawing. <see cref="Tag"/> says what it is (screens
/// define their own tag types); <see cref="Hover"/> / <see cref="Click"/> are keys to feed into <see cref="InputMap"/> when
/// the pointer moves onto it / clicks it, so a mouse pick runs exactly the keyboard's command.
/// </summary>
public sealed record HitRegion(Rectangle Bounds, object? Tag = null)
{
    public IReadOnlyList<Keys> Hover { get; init; } = [];
    public IReadOnlyList<Keys> Click { get; init; } = [];
}

/// <summary>
/// The clickable regions of the last drawn frame, topmost last. Rebuilt every Draw (<see cref="UiOverlays.Hits"/>); a
/// region added later sits on top of earlier ones (a modal panel over the map).
/// </summary>
public sealed class HitMap
{
    private readonly List<HitRegion> _regions = [];

    public IReadOnlyList<HitRegion> Regions => _regions;

    /// <summary>Keys the wheel sends (up, down) for the topmost scrollable thing drawn; null = the wheel does nothing.</summary>
    public (Keys Up, Keys Down)? Wheel { get; set; }

    public void Clear()
    {
        _regions.Clear();
        Wheel = null;
    }

    public void Add(HitRegion region) => _regions.Add(region);

    public void Add(Rectangle bounds, object? tag = null) => _regions.Add(new HitRegion(bounds, tag));

    /// <summary>The topmost region containing <paramref name="point"/> whose tag is a <typeparamref name="T"/>, or null.</summary>
    public T? Find<T>(Point point) where T : class
    {
        for (var i = _regions.Count - 1; i >= 0; i--)
            if (_regions[i].Tag is T tag && _regions[i].Bounds.Contains(point))
                return tag;
        return null;
    }

    /// <summary>The topmost region containing <paramref name="point"/>, or null.</summary>
    public HitRegion? At(Point point)
    {
        for (var i = _regions.Count - 1; i >= 0; i--)
            if (_regions[i].Bounds.Contains(point))
                return _regions[i];
        return null;
    }
}

/// <summary>Small hit-test helpers for laid-out rows and cards.</summary>
public static class HitTest
{
    /// <summary>Index of the first rectangle containing <paramref name="point"/>, or −1.</summary>
    public static int Index(IReadOnlyList<Rectangle> rects, Point point)
    {
        for (var i = 0; i < rects.Count; i++)
            if (rects[i].Contains(point))
                return i;
        return -1;
    }

    /// <summary>Keys that move a cursor from <paramref name="from"/> to <paramref name="to"/> one step at a time.</summary>
    public static Keys[] Steps(int from, int to, Keys back, Keys forward) =>
        Enumerable.Repeat(to < from ? back : forward, Math.Abs(to - from)).ToArray();

    /// <summary>The digit key for 0..9.</summary>
    public static Keys Digit(int d) => Keys.D0 + d;
}

// Tags the shell interprets (Game1.Mouse.cs) where moving the cursor by keys would not land on the row.

/// <summary>A wilderness / dungeon map view: the cell under a click is <c>(X0 + dx / CellW, Y0 + dy / CellH)</c>.</summary>
public sealed record MapViewTag(int X0, int Y0, int CellWidth, int CellHeight);

/// <summary>Line <paramref name="Line"/> of a full-screen menu (main menu / Options), character column from its left.</summary>
public sealed record MenuLineTag(int Line, string Text = "");

/// <summary>Row <paramref name="Index"/> of the Armory stock list.</summary>
public sealed record ArmoryRowTag(int Index);

/// <summary>Item row <paramref name="Index"/> of the inventory overlay.</summary>
public sealed record InventoryRowTag(int Index);

/// <summary>Row <paramref name="Row"/> of the Continue party list (<see cref="ContinueDialog"/>).</summary>
public sealed record ContinueRowTag(int Row);

/// <summary>A row of III Amiga's spell / heal window on the combat orders screen: a press moves the cursor there (s119).</summary>
public sealed record AmigaRowTag(int Row);

/// <summary>
/// s165: a member row of III Amiga's window 6 for an open prompt (the disarm question): a press puts the prompt's cursor on
/// answer <paramref name="Cursor"/> (0-based) and picks nothing (<c>0x17a6</c>); OK / Return takes it.
/// </summary>
public sealed record AmigaPromptRowTag(int Cursor);

/// <summary>A title on III Amiga's fight menu bar: a press pulls its menu down or closes it (s119).</summary>
public sealed record AmigaMenuTitleTag(int Menu);

/// <summary>An item of III Amiga's open fight menu: the pointer over it highlights it, a click presses its key (s119).</summary>
public sealed record AmigaMenuItemTag(int Item);

/// <summary>A title on III Amiga's bar outside the towns: a click pulls its menu down or closes it (s136).</summary>
public sealed record OutsideMenuTitleTag(int Menu);

/// <summary>An item of III Amiga's open bar menu outside the towns: hover highlights it, a click takes it (s136).</summary>
public sealed record OutsideMenuItemTag(int Item);

/// <summary>A title on III Amiga's town bar (s156): a press or click pulls its menu down.</summary>
public sealed record TownMenuTitleTag(int Menu);

/// <summary>An item of III Amiga's open town menu (s156): hover lights it, a click or a release on it takes it.</summary>
public sealed record TownMenuItemTag(int Item);

/// <summary>The town screen behind an open town menu (s156): a click there closes the menu.</summary>
public sealed record TownMenuBackdropTag;

/// <summary>A modal panel's backdrop: clicks that miss every row stop here (nothing below reacts).</summary>
public sealed record BackdropTag;

/// <summary>s153: row <paramref name="Row"/> of I Amiga's timed MESS window (requester 21): a click ends the window early.</summary>
public sealed record MapWindowRowTag(int Row);

/// <summary>s153: button <paramref name="Index"/> of I Amiga's event menu (requester 25): a click answers it.</summary>
public sealed record MapMenuButtonTag(int Index);
