using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// The mouse (presentation only, always on): hover moves the cursor the arrows move, a left click is Return on what is
/// under it, a right click is Esc, the wheel scrolls lists. Every draw routine registers what it lays out in a
/// <see cref="HitMap"/>; most targets carry the keys to send (<see cref="InputMap.Inject"/>), so a mouse pick runs exactly
/// the keyboard's code. A click on a seen map cell walks there step by step (<see cref="FieldPath"/>), each step through
/// <see cref="FieldStep"/> like a key press, stopping on anything but a plain move; a click on the party's own cell is the
/// fight key. Typed entry (a name, a Guild number) gets a clickable character grid once the mouse is used
/// (<see cref="TypingGrid"/>). Apart from that grid nothing is drawn for the mouse: the OS pointer is shown when the mouse
/// moves and hidden when a key is typed.
/// </summary>
public partial class Game1
{
    /// <summary>Pause between the steps of a mouse walk, so the party is seen moving.</summary>
    private const double WalkStepMs = 140;

    private readonly MouseInput _mouse = new();
    private readonly HitMap _hits = new();
    private bool _hitsFresh;        // the hit map shows the state as it is now (a Draw ran since the mouse last acted)
    private bool _hoverDirty;       // the pointer moved since hover last acted
    private Rectangle? _hovered;    // hover acts once per target, so a still pointer never fights the keys
    private Point? _pendingClick;
    private int _pendingWheel;
    private Point? _lastDesktopPointer;
    private bool _mouseUsed;       // the mouse moved at least once this run (the combat order line shows all choices)
    private (GameMode Mode, int Area, int X, int Y)? _walk;
    private ScrollBarTag? _scrollDrag; // a drag that began on a scroll bar moves that box's thumb
    private double _walkWait;

    private void BeginHits()
    {
        _hits.Clear();
        UiOverlays.Hits = _hits;
    }

    private void EndHits()
    {
        UiOverlays.Hits = null;
        _hitsFresh = true;
    }

    /// <summary>Read the mouse, turn this frame's hover / click / wheel into keys or cursor moves, and pace a walk.</summary>
    private void UpdateMouse(GameTime gameTime, KeyboardState keys)
    {
        var state = Mouse.GetState();
        Point? at = null;
        if (IsActive && _screen is not null)
        {
            var pp = GraphicsDevice.PresentationParameters;
            var client = Window.ClientBounds;
            var x = client.Width > 0 ? state.X * pp.BackBufferWidth / client.Width : state.X;
            var y = client.Height > 0 ? state.Y * pp.BackBufferHeight / client.Height : state.Y;
            at = _screen.WindowToVirtual(new Point(x, y), pp.BackBufferWidth, pp.BackBufferHeight);
        }
        _mouse.Update(at, state.LeftButton == ButtonState.Pressed, state.RightButton == ButtonState.Pressed,
            state.ScrollWheelValue);
        ScrollBarDrag();
        InventoryDrag(); // Game1.Inventory.cs: drag and drop on the inventory screen
        AmigaBarPress(); // s156: III Amiga's bars pull down on the press and take the row the button is released on

        if (_input.AnyRealKeyPressed(keys))
        {
            _walk = null; // any key stops a walk
            _pendingClick = null;
            IsMouseVisible = false;
        }
        // A move is the pointer moving on the desktop: a window that opens, maximizes or moves under a still pointer
        // shifts the window-relative position without the user touching the mouse.
        var desktop = new Point(Window.ClientBounds.X + state.X, Window.ClientBounds.Y + state.Y);
        var userMoved = _lastDesktopPointer is { } last && last != desktop;
        _lastDesktopPointer = desktop;
        if (_mouse.Moved && userMoved)
        {
            _mouseUsed = true;
            _hoverDirty = true;
            IsMouseVisible = true;
        }
        if (_mouse.AnyPressed)
            _walk = null; // so does any click (a click on the map then starts a new one)
        if (_mouse.LeftClick is { } click)
            _pendingClick = click;
        _pendingWheel += _mouse.WheelSteps;

        if (_mouse.RightClick)
        {
            _input.Inject([Keys.Escape]); // the Esc path: back out, close, or the game menu
            _pendingClick = null;
            _hitsFresh = false;
        }
        else if (_hitsFresh)
        {
            if (_pendingWheel != 0)
            {
                if (_mouse.Position is { } over && _hits.Find<ScrollAreaTag>(over) is { } area)
                    area.Box.ScrollBy(-_pendingWheel); // the wheel over a scrolling box scrolls that box
                else if (_hits.Wheel is var (up, down))
                    _input.Inject(Enumerable.Repeat(_pendingWheel > 0 ? up : down, Math.Abs(_pendingWheel)));
                _pendingWheel = 0;
                _hitsFresh = false;
            }
            else if (_pendingClick is { } p)
            {
                _pendingClick = null;
                _hoverDirty = false;
                _hovered = _hits.At(p)?.Bounds;
                Act(_hits.At(p), p, clicked: true);
            }
            else if (_hoverDirty && _mouse.Position is { } hover)
            {
                _hoverDirty = false;
                var region = _hits.At(hover);
                if (region?.Bounds != _hovered)
                {
                    _hovered = region?.Bounds;
                    Act(region, hover, clicked: false);
                }
            }
        }

        StepWalk(gameTime.ElapsedGameTime.TotalMilliseconds);
    }

    /// <summary>Hover or click on <paramref name="region"/>: its keys, or the shell's own handling of its tag.</summary>
    private void Act(HitRegion? region, Point at, bool clicked)
    {
        if (region is null)
            return;
        var keys = clicked ? region.Click : region.Hover;
        switch (region.Tag)
        {
            case MenuLineTag menu:
                MenuLine(menu.Line, PixelFont.ColumnAt(menu.Text, at.X - 14), clicked);
                break;
            case P4KeypadTag { Digit: char digit } when clicked:
                _p4?.Keypad(digit); // s151: _numin's keypad types the digit as the keyboard does
                break;
            case P4PanelIconTag { Line: int line } when clicked:
                _p4?.AimAt(line); // a Command panel icon whose list line has no digit: the cursor there, then Return
                break;
            case ArmoryRowTag row:
                _townCursor = row.Index; // the Armory moves its cursor once a frame: set it, Return buys
                if (clicked)
                    keys = [_settings.GetKey(GameAction.Confirm)];
                break;
            case InventoryRowTag row:
                _inventoryCursor = row.Index;
                if (clicked)
                    keys = [_settings.GetKey(GameAction.Confirm)];
                break;
            case InventorySpot spot:
                InventoryPoint(spot, clicked);
                break;
            case ContinueRowTag row:
                _continueDialog.MoveTo(row.Row); // hover selects; a click loads (Return)
                if (clicked)
                    keys = [Keys.Enter];
                break;
            case BackdropTag when clicked && _combatUi.P1FieldShown:
                _combatUi.P1Click(); // s138: a click on I Amiga's field during sound 17's black ends it, as a key does
                break;
            case AmigaPromptRowTag row when clicked && CurrentPrompt is { } rowPrompt:
                _promptMenu.MoveTo(rowPrompt, row.Cursor); // s165: window 6's row press moves the cursor (0x17a6)
                break;
            case AmigaRowTag row when clicked && _session is { } session:
                _combatUi.AmigaPickRow(session, row.Row); // 0x17a6: a press moves the cursor; OK takes it
                break;
            case AmigaMenuTitleTag or OutsideMenuTitleTag or TownMenuTitleTag when clicked && _barPressOpened:
                _barPressOpened = false; // the press already pulled this menu down: the click leaves it open (click, click)
                break;
            case AmigaMenuTitleTag title when clicked:
                _combatUi.OpenFieldMenu(title.Menu);
                break;
            case AmigaMenuItemTag item:
                _combatUi.HoverFieldMenuItem(item.Item);
                break;
            case OutsideMenuTitleTag title when clicked && OutsideBarTakesInput():
                _outsideBar.Toggle(title.Menu, CurrentGameSettings.CombatSpeed);
                break;
            case OutsideMenuItemTag item when OutsideBarTakesInput():
                _outsideBar.Hover(item.Item);
                if (clicked)
                    RunOutsideBarAct(_outsideBar.Take(item.Item));
                break;
            case TownMenuTitleTag or TownMenuItemTag or TownMenuBackdropTag:
                TownBarMouse(region.Tag, clicked); // s156: III Amiga's town bar
                break;
            case P1MapMenuTitleTag or P1MapMenuItemTag or P1MapMenuBackdropTag:
                P1MapBarMouse(region.Tag, clicked); // s155: I Amiga's map menu bar
                break;
            case ScrollBarTag bar when clicked:
                bar.Box.ScrollTo(bar.Box.OffsetAt(bar.Track, at.Y));
                break;
            case MapViewTag map when clicked:
                StartWalk(map, region.Bounds, at);
                break;
        }
        if (keys.Count > 0)
            _input.Inject(keys);
        _hitsFresh = false;
    }

    private bool _barPressOpened; // a left press on a III bar title pulled its menu down (the click that ends it keeps it)

    /// <summary>
    /// s156: III Amiga's bars (the fight's, outside the towns, the town's) follow the Amiga's mouse: a press on a title
    /// pulls its menu down at once (<c>0x184a</c> runs while the button is down above y 11), the pointer lights the rows
    /// while the button is held, and the release takes the row it is on (the key from <c>0x2189c</c>); a release anywhere
    /// else closes the menu. A press and release on the title (a click) leaves the menu down, so click, click still works.
    /// </summary>
    private void AmigaBarPress()
    {
        if (_mouse.LeftPressed && _mouse.Position is { } at && _session is { } s)
        {
            _barPressOpened = false;
            switch (_hits.At(at)?.Tag)
            {
                case AmigaMenuTitleTag t when _combatUi.AmigaFieldMenuShown && _combatUi.FieldMenuOpen != t.Menu:
                    _combatUi.OpenFieldMenu(t.Menu);
                    _barPressOpened = _combatUi.FieldMenuOpen == t.Menu;
                    break;
                case OutsideMenuTitleTag t when OutsideBarTakesInput() && _outsideBar.Open != t.Menu:
                    _outsideBar.Toggle(t.Menu, CurrentGameSettings.CombatSpeed);
                    _barPressOpened = true;
                    break;
                case TownMenuTitleTag t when TownBarPlaces(s) is { } bar && TownBarTakesInput(s) && _townBar.Open != t.Menu:
                    _townBar.PullDown(t.Menu);
                    _barPressOpened = true;
                    break;
            }
            if (_barPressOpened)
                _hitsFresh = false; // the next draw lays out the open menu's rows
        }
        if (_mouse.DragEnded is not { } drop || _session is not { } session)
            return;
        var wasPressOpened = _barPressOpened;
        _barPressOpened = false;
        var region = _hits.At(drop.To);
        var from = _hits.At(drop.From)?.Tag;
        if (from is not (AmigaMenuTitleTag or OutsideMenuTitleTag or TownMenuTitleTag) && !wasPressOpened)
            return;
        switch (region?.Tag)
        {
            case AmigaMenuItemTag item when _combatUi.AmigaFieldMenuShown && _combatUi.FieldMenuOpen is not null:
                _combatUi.HoverFieldMenuItem(item.Item);
                _input.Inject(region.Click.Count > 0 ? region.Click : [Keys.Escape]); // the item's key, or none (File's Info)
                break;
            case OutsideMenuItemTag item when OutsideBarTakesInput() && _outsideBar.Open is not null:
                RunOutsideBarAct(_outsideBar.Take(item.Item));
                break;
            case TownMenuItemTag:
                TownBarMouse(region.Tag, clicked: true);
                break;
            default: // released off the rows (the title too: key 0): the menu goes away
                if (_combatUi.AmigaFieldMenuShown && _combatUi.FieldMenuOpen is { } fightMenu)
                    _combatUi.OpenFieldMenu(fightMenu); // a second press on the open title closes it
                if (OutsideBarTakesInput())
                    _outsideBar.CloseMenu();
                _townBar.Close();
                break;
        }
        _hitsFresh = false;
    }

    /// <summary>A drag that starts on a scroll bar drags its thumb (and is never an item drag).</summary>
    private void ScrollBarDrag()
    {
        if (_mouse.DragStarted && _mouse.DragOrigin is { } from)
            _scrollDrag = _hits.At(from)?.Tag as ScrollBarTag;
        if (_scrollDrag is { } bar && _mouse.Position is { } at)
            bar.Box.ScrollTo(bar.Box.OffsetAt(bar.Track, at.Y));
        if (!_mouse.Dragging)
            _scrollDrag = null;
    }

    /// <summary>Main menu: the arrows to the row (+ Return); Options: its own pointing (rows skip headers).</summary>
    private void MenuLine(int line, int column, bool clicked)
    {
        if (_slotScreen.IsOpen)
        {
            // QoL SAVE SLOTS: hovering selects a slot's row, a click chooses it.
            if (!clicked)
                _slotScreen.Point(line);
            else if (_slotScreen.Click(line))
                RunSaveSlotPick();
        }
        else if (_update.IsOpen)
        {
            if (clicked)
                _update.Click(line);
            else if (_update.RowOfLine(line) is int updateRow && updateRow != _update.Cursor)
                _input.Inject([Keys.Down]);
        }
        else if (_import.IsOpen)
        {
            if (_import.RowOfLine(line) is int importRow)
            {
                var steps = HitTest.Steps(_import.Cursor, importRow, Keys.Up, Keys.Down);
                _input.Inject(clicked ? [.. steps, Keys.Enter] : steps);
            }
        }
        else if (_options.IsOpen)
        {
            if (clicked)
                _options.Click(line, column);
            else
                _options.Point(line);
        }
        else if (_session is null && _mainMenu.RowOfLine(line) is int row)
        {
            var move = HitTest.Steps(_mainMenu.Cursor, row, Keys.Up, Keys.Down);
            _input.Inject(clicked ? [.. move, Keys.Enter] : move);
        }
    }

    /// <summary>
    /// A click on the map: the party's own cell is the fight key (Space by default, as the help line's "Space fight"), a
    /// neighbouring diagonal the numpad move (when on), anything else a walk.
    /// </summary>
    private void StartWalk(MapViewTag map, Rectangle bounds, Point at)
    {
        if (_session is not { Mode: GameMode.Overworld or GameMode.Dungeon } s || FieldSpot(s) is not var (area, px, py))
            return;
        var x = map.X0 + (at.X - bounds.X) / map.CellWidth;
        var y = map.Y0 + (at.Y - bounds.Y) / map.CellHeight;
        var (dx, dy) = (x - px, y - py);
        if (dx == 0 && dy == 0)
        {
            _input.Inject([_settings.GetKey(GameAction.Fight)]); // the keyboard's own fight code runs
            return;
        }
        if (Math.Abs(dx) == 1 && Math.Abs(dy) == 1 && CurrentGameSettings.DiagonalMoves)
        {
            FieldMove(s, dx, dy); // the same two steps the numpad diagonal makes (D-076 QoL)
            return;
        }
        _walk = (s.Mode, area, x, y);
        _walkWait = 0;
    }

    /// <summary>One step of the walk when it is due; the walk ends at the goal, on any non-plain step, or when anything opens.</summary>
    private void StepWalk(double elapsedMs)
    {
        if (_walk is not { } walk)
            return;
        var s = _session;
        if (s is null || s.Mode != walk.Mode || FieldSpot(s) is not var (area, px, py) || area != walk.Area
            || _options.IsOpen || OpenModal is not null || _dosDialog.IsOpen || CurrentPrompt is not null
            || _combatUi.IsPresenting || _showInventory || _showBestiary)
        {
            _walk = null;
            return;
        }
        _walkWait -= elapsedMs;
        if (_walkWait > 0 || _p1Holds.Busy(_updateSeconds)) // s154: the step tone's hold before the next step
            return;

        var path = s.Mode == GameMode.Overworld
            ? FieldPath.Overworld(s.CurrentOverworldRegion, (px, py), (walk.X, walk.Y))
            : WalkableDungeonPath(s.Dungeon!, (px, py), (walk.X, walk.Y));
        if (path is not { Count: > 0 })
        {
            _walk = null;
            return;
        }
        var (dx, dy) = FieldPath.Delta(path[0]);
        void Step()
        {
            var outcome = FieldStep(s, path[0]);
            if (outcome != StepOutcome.Plain || FieldSpot(s) != (area, px + dx, py + dy) || path.Count == 1)
                _walk = null; // a fight, prompt, sign, town, blocked step… or there
            else
                _walkWait = WalkStepMs;
        }
        if (!StepAfterTone(Step)) // s154: I Amiga's step tone first, the step after its hold
            Step();
    }

    private IReadOnlyList<CardinalDirection>? WalkableDungeonPath(DungeonState d, (int X, int Y) from, (int X, int Y) to) =>
        DungeonWalkPath(d.Level ?? _dungeonView!.GetLevel(d.LevelId), _qol.VisitedCells(d.LevelId),
            _qol.StoodOnCells(d.LevelId), from, to);

    /// <summary>
    /// A mouse walk on a dungeon level over the cells the map has drawn (<paramref name="drawn"/>), by each cell's real
    /// passability (<see cref="DungeonLevel.IsWalkable"/>). The <c>0x01</c> corridor is drawn with the wall glyph
    /// (OV2 <c>0x21df</c>), so the walk takes it only once the party has stood on it (<paramref name="stoodOn"/>, kept in
    /// <c>qol.json</c>; user s77). The arrow keys walk it either way.
    /// </summary>
    public static IReadOnlyList<CardinalDirection>? DungeonWalkPath(
        DungeonLevel level, IEnumerable<(int X, int Y)> drawn, IEnumerable<(int X, int Y)> stoodOn,
        (int X, int Y) from, (int X, int Y) to)
    {
        var seen = drawn.ToHashSet();
        var stood = stoodOn.ToHashSet();
        return FieldPath.Dungeon(level, from, to, (x, y) => seen.Contains((x, y)), (x, y) => stood.Contains((x, y)));
    }

    /// <summary>The map the party is on (region / level id) and its cell.</summary>
    private static (int Area, int X, int Y)? FieldSpot(GameSession s) => s.Mode switch
    {
        GameMode.Overworld => (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y),
        GameMode.Dungeon when s.Dungeon is { } d => (d.LevelId, d.X, d.Y),
        _ => null,
    };

    /// <summary>
    /// The Continue party list (QoL <see cref="GameSettings.ContinueFromList"/>): each shown row exactly, over the panel's
    /// generic rows (the "..." line and the names line are not rows); the wheel steps the cursor without wrapping.
    /// </summary>
    private void HitContinueRows(IReadOnlyList<string> lines)
    {
        if (UiOverlays.Hits is not { } hits || !_continueDialog.ShowsList)
            return;
        for (var line = 0; line < lines.Count; line++)
            if (_continueDialog.RowAtLine(line) is int row && UiOverlays.TextPanelLineTop(lines, line) is int y)
                hits.Add(UiOverlays.TextSpan(14, y, 0, PanelMouse.RowWidth), new ContinueRowTag(row));
        hits.Wheel = (Keys.PageUp, Keys.PageDown);
    }

    /// <summary>A drawn map view: clicks on it walk (cells from <paramref name="x0"/>, <paramref name="y0"/>).</summary>
    private static void HitMapView(int left, int top, int cols, int rows, int cellW, int cellH, int x0, int y0) =>
        UiOverlays.Hits?.Add(new Rectangle(left, top, cols * cellW, rows * cellH), new MapViewTag(x0, y0, cellW, cellH));
}
