using System.Globalization;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>What a <see cref="SaveSlotScreen"/> was opened for.</summary>
public enum SaveSlotMode
{
    /// <summary>Save (F5, the game menu, a town's Save game, IV's System ▸ Save): the numbered slots.</summary>
    Save,

    /// <summary>Load (F9, the game menu, a town's Load): every slot, AUTO first.</summary>
    Load,

    /// <summary>The game picked on the main menu: NEW GAME, then every slot.</summary>
    Start,
}

/// <summary>A pick on a <see cref="SaveSlotScreen"/>: a slot, or (Start only) a new game when <see cref="Slot"/> is null.</summary>
public sealed record SaveSlotPick(SaveSlotMode Mode, GameId Game, SaveSlot? Slot);

/// <summary>
/// QoL SAVE SLOTS (<c>docs/plan/12-save-slots.md</c>, <see cref="GameSettings.SaveSlots"/>): the remaster's slot picker, a
/// full-screen menu over any game (I–IV) or the main menu. One row per slot (its number or AUTO, the party's leader and
/// size, where it is, the file's date), the selected slot's names, place and full date below. Up/Down / Home / End move,
/// a digit picks that slot (A: AUTO), Return chooses, Esc goes back. Save asks Y/N before it overwrites a slot that holds
/// a save; Load and Start refuse an empty slot or a file that can't be read. Rows too wide for the screen go on, whole, on
/// the next line; a list longer than the screen scrolls with the cursor.
/// </summary>
public sealed class SaveSlotScreen
{
    /// <summary>Text columns of a menu-screen line (<see cref="UiOverlays.DrawMenuScreen"/>).</summary>
    public const int Columns = 49;

    /// <summary>Lines the menu screen holds under its title (y 30 … 170, ten pixels apart).</summary>
    public const int ScreenLines = 15;

    private const int WhoWidth = 14;
    private const int PlaceWidth = 12;

    private IReadOnlyList<SaveSlotInfo> _slots = [];
    private Func<SavePlace, int?, string?> _placeName = (_, _) => null;
    private int _scroll;
    private SaveSlotInfo? _overwrite;
    private string _error = "";
    private SaveSlotPick? _pick;
    private List<int?> _lineRows = [];

    public bool IsOpen { get; private set; }

    public SaveSlotMode Mode { get; private set; }

    public GameId Game { get; private set; }

    /// <summary>The selected row: in Start, 0 is NEW GAME and the slots follow.</summary>
    public int Cursor { get; private set; }

    /// <summary>The slots listed (Save: the numbered ones; Load / Start: all).</summary>
    public IReadOnlyList<SaveSlotInfo> Slots => _slots;

    /// <summary>True while the overwrite question is up.</summary>
    public bool Confirming => _overwrite is not null;

    private int RowCount => _slots.Count + (Mode == SaveSlotMode.Start ? 1 : 0);

    private SaveSlotInfo? SlotAt(int row) =>
        Mode == SaveSlotMode.Start ? (row == 0 ? null : _slots[row - 1]) : _slots[row];

    public string Help => Confirming
        ? UiText.S("saveslots.help_confirm")
        : UiText.F("saveslots.help_x", SaveSlots.NumberedCount(Game)); // 1-9, IV 1-3

    /// <summary>
    /// Opens on <paramref name="slots"/> (<see cref="SaveSlots.Describe(IEnumerable{SaveSlot})"/>). <paramref name="placeName"/>
    /// names a town or map when the caller knows the game's names (null = the place's number). The cursor starts on the
    /// newest save (Save: the first empty slot, else the newest).
    /// </summary>
    public void Open(GameId game, SaveSlotMode mode, IReadOnlyList<SaveSlotInfo> slots,
        Func<SavePlace, int?, string?>? placeName = null)
    {
        Game = game;
        Mode = mode;
        _slots = slots ?? throw new ArgumentNullException(nameof(slots));
        _placeName = placeName ?? ((_, _) => null);
        _overwrite = null;
        _error = "";
        _pick = null;
        _scroll = 0;
        var newest = NewestIndex();
        var firstEmpty = _slots.ToList().FindIndex(s => s.Empty);
        var slot = mode == SaveSlotMode.Save && firstEmpty >= 0 ? firstEmpty : newest ?? 0;
        Cursor = mode == SaveSlotMode.Start ? (newest is int n ? n + 1 : 0) : slot;
        IsOpen = true;
    }

    private int? NewestIndex()
    {
        int? best = null;
        for (var i = 0; i < _slots.Count; i++)
            if (_slots[i].Written is DateTime t && (best is not int b || t > _slots[b].Written))
                best = i;
        return best;
    }

    public void Close()
    {
        IsOpen = false;
        _overwrite = null;
    }

    /// <summary>The pick, once, after the screen closed on it (null after Esc, or when already taken).</summary>
    public SaveSlotPick? TakePick()
    {
        var pick = _pick;
        _pick = null;
        return pick;
    }

    /// <summary>Handles this frame's keys; true when the screen closed (on a pick or Esc).</summary>
    public bool HandleKeys(IEnumerable<Keys> newKeys)
    {
        if (!IsOpen)
            return false;
        foreach (var key in newKeys)
        {
            if (_overwrite is { } asked)
            {
                if (key == Keys.Y)
                    return Choose(asked);
                if (key is Keys.N or Keys.Escape or Keys.Back)
                    _overwrite = null;
                continue;
            }
            switch (key)
            {
                case Keys.Escape or Keys.Back:
                    Close();
                    return true;
                case Keys.Up:
                    Move((Cursor + RowCount - 1) % RowCount);
                    break;
                case Keys.Down:
                    Move((Cursor + 1) % RowCount);
                    break;
                case Keys.Home or Keys.PageUp:
                    Move(0);
                    break;
                case Keys.End or Keys.PageDown:
                    Move(RowCount - 1);
                    break;
                case Keys.Enter or Keys.Space:
                    if (Activate(Cursor))
                        return true;
                    break;
                default:
                    if (RowOfKey(key) is int row)
                    {
                        Move(row);
                        if (Activate(row))
                            return true;
                    }
                    break;
            }
        }
        return false;
    }

    /// <summary>A pad on the picker: the D-pad moves, A chooses (and answers Y to the question), B / Back go back.</summary>
    public static IEnumerable<Keys> PadKeys(IEnumerable<Buttons> buttons) => buttons.Select(b => b switch
    {
        Buttons.DPadUp or Buttons.LeftThumbstickUp => Keys.Up,
        Buttons.DPadDown or Buttons.LeftThumbstickDown => Keys.Down,
        Buttons.A => Keys.Y, // Y is the question's yes; elsewhere it picks nothing, so A also sends Return below
        Buttons.B or Buttons.Back => Keys.Escape,
        _ => Keys.None,
    }).SelectMany(k => k == Keys.Y ? [Keys.Y, Keys.Enter] : new[] { k }).Where(k => k != Keys.None);

    /// <summary>The row a digit (top row or keypad) or A (AUTO) or N (Start's NEW GAME) names, or null.</summary>
    private int? RowOfKey(Keys key)
    {
        int? number = key switch
        {
            >= Keys.D1 and <= Keys.D9 => key - Keys.D0,
            >= Keys.NumPad1 and <= Keys.NumPad9 => key - Keys.NumPad0,
            Keys.A => SaveSlots.AutoNumber,
            _ => null,
        };
        if (key == Keys.N && Mode == SaveSlotMode.Start)
            return 0;
        if (number is not int n)
            return null;
        var index = _slots.ToList().FindIndex(s => s.Slot.Number == n);
        if (index < 0)
            return null;
        return Mode == SaveSlotMode.Start ? index + 1 : index;
    }

    private void Move(int row)
    {
        Cursor = Math.Clamp(row, 0, RowCount - 1);
        _error = "";
    }

    /// <summary>Choose row <paramref name="row"/>: true when the screen closed on a pick.</summary>
    private bool Activate(int row)
    {
        _error = "";
        var slot = SlotAt(row);
        if (slot is null)
            return Close(new SaveSlotPick(Mode, Game, null)); // Start's NEW GAME
        if (Mode == SaveSlotMode.Save)
        {
            if (slot.Empty)
                return Choose(slot);
            _overwrite = slot;
            return false;
        }
        if (slot.Empty)
        {
            _error = UiText.S("saveslots.empty_refused");
            return false;
        }
        if (slot.Old)
        {
            _error = UiText.S("continue.old_save");
            return false;
        }
        if (slot.Unreadable)
        {
            _error = UiText.S("saveslots.unreadable_refused");
            return false;
        }
        return Choose(slot);
    }

    private bool Choose(SaveSlotInfo slot) => Close(new SaveSlotPick(Mode, Game, slot.Slot));

    private bool Close(SaveSlotPick pick)
    {
        _pick = pick;
        Close();
        return true;
    }

    /// <summary>A mouse click on line <paramref name="line"/> of <see cref="Lines"/>: the row there is selected and chosen.</summary>
    public bool Click(int line)
    {
        if (!IsOpen || RowOfLine(line) is not int row)
            return false;
        if (_overwrite is not null)
            return false;
        Move(row);
        return Activate(row);
    }

    /// <summary>The pointer over line <paramref name="line"/>: its row is selected (nothing while the question is up).</summary>
    public void Point(int line)
    {
        if (IsOpen && _overwrite is null && RowOfLine(line) is int row && row != Cursor)
            Move(row);
    }

    /// <summary>The row drawn on line <paramref name="line"/> of the last <see cref="Lines"/>, or null.</summary>
    public int? RowOfLine(int line) => line >= 0 && line < _lineRows.Count ? _lineRows[line] : null;

    /// <summary>The slot's label: AUTO or its number.</summary>
    public static string Label(SaveSlot slot) =>
        slot.IsAuto ? UiText.S("saveslots.auto") : slot.Number.ToString(CultureInfo.InvariantCulture);

    /// <summary>Where the save's party is, in words (a town's name when known, else its number).</summary>
    public string PlaceText(SaveSlotPreview p) => p.Place switch
    {
        SavePlace.Town => _placeName(p.Place, p.Number) ?? UiText.F("saveslots.place.town_x", p.Number ?? 0),
        SavePlace.Wilderness => _placeName(p.Place, p.Number) ?? UiText.S("saveslots.place.wilderness"),
        SavePlace.Dungeon => UiText.F("saveslots.place.dungeon_x", p.Number ?? 0),
        _ => UiText.S("saveslots.place.other"),
    };

    /// <summary><paramref name="t"/> in <paramref name="format"/> (a string-table date format).</summary>
    private static string Date(DateTime t, string format) => t.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>The leader and the party's size ("NAME +3"), or "(NO PARTY)".</summary>
    private static string Who(SaveSlotPreview p) => p.Names.Count switch
    {
        0 => UiText.S("saveslots.nobody"),
        1 => p.Names[0],
        _ => UiText.F("saveslots.who_x_more_x", p.Names[0], p.Names.Count - 1),
    };

    /// <summary>Row <paramref name="row"/>'s text without its cursor column.</summary>
    public string RowText(int row)
    {
        if (SlotAt(row) is not { } info)
            return UiText.S("saveslots.new_game");
        var label = Label(info.Slot).PadRight(4);
        if (info.Empty)
            return $"{label} {UiText.S("saveslots.empty")}";
        if (info.Preview is not { } p)
            return $"{label} {(info.Old ? UiText.S("saveslots.old") : UiText.S("saveslots.unreadable"))}";
        return $"{label} {Who(p).PadRight(WhoWidth)} {PlaceText(p).PadRight(PlaceWidth)} {Date(info.Written!.Value, UiText.S("saveslots.row_date"))}";
    }

    /// <summary>The selected slot's details: every name, then the place, the other parties and the full date.</summary>
    private IEnumerable<string> Detail()
    {
        if (_overwrite is { } asked)
        {
            yield return UiText.F("saveslots.overwrite_x", Label(asked.Slot));
            yield break;
        }
        if (_error.Length > 0)
        {
            yield return _error;
            yield break;
        }
        if (SlotAt(Cursor) is not { Written: DateTime written } info)
            yield break;
        if (info.Preview is { } p)
        {
            if (p.Names.Count > 0)
                yield return string.Join(" ", p.Names);
            var where = PlaceText(p);
            if (p.OtherParties > 0)
                where += "  " + (p.OtherParties == 1 ? UiText.S("saveslots.other_party") : UiText.F("saveslots.other_parties_x", p.OtherParties));
            yield return where;
        }
        yield return Date(written, UiText.S("saveslots.long_date"));
    }

    /// <summary>
    /// The screen: the title, the game's name (Start: the question), the rows from the scroll position, then the detail
    /// lines. A row or detail line wider than <see cref="Columns"/> goes on, whole, on the next line.
    /// </summary>
    public IReadOnlyList<MenuLine> Lines()
    {
        var title = Mode switch
        {
            SaveSlotMode.Save => UiText.S("saveslots.title_save"),
            SaveSlotMode.Load => UiText.S("saveslots.title_load"),
            _ => UiText.S("menu.game." + Game.Prefix()),
        };
        var head = Mode == SaveSlotMode.Start ? UiText.S("saveslots.start_question") : UiText.S("menu.game." + Game.Prefix());
        var detail = Detail().SelectMany(d => UiOverlays.Lines(d, Columns)).ToList();
        var rowLines = Enumerable.Range(0, RowCount)
            .Select(r => UiOverlays.Lines((r == Cursor ? ">" : " ") + RowText(r), Columns))
            .ToList();

        // Room for rows: the screen's lines less the head line, a gap, the detail and a "..." line when it scrolls.
        var room = Math.Max(1, ScreenLines - 1 - 1 - detail.Count);
        var total = rowLines.Sum(l => l.Count);
        if (total > room)
            room = Math.Max(1, room - 1);
        // Keep the cursor's row inside the window.
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, RowCount - 1));
        if (Cursor < _scroll)
            _scroll = Cursor;
        while (_scroll < Cursor && rowLines.Skip(_scroll).Take(Cursor - _scroll + 1).Sum(l => l.Count) > room)
            _scroll++;

        var lines = new List<MenuLine> { new(title, MenuLineStyle.Title), new(head, MenuLineStyle.Header) };
        _lineRows = [null, null];
        var used = 0;
        var r = _scroll;
        for (; r < RowCount && used + rowLines[r].Count <= room; r++)
        {
            var style = r == Cursor ? MenuLineStyle.Selected
                : SlotAt(r) is { Empty: true } || SlotAt(r) is { Unreadable: true } && Mode != SaveSlotMode.Save
                    ? MenuLineStyle.Dim : MenuLineStyle.Normal;
            foreach (var part in rowLines[r])
            {
                lines.Add(new(part, style));
                _lineRows.Add(r);
            }
            used += rowLines[r].Count;
        }
        if (_scroll > 0 || r < RowCount)
        {
            lines.Add(new(r < RowCount ? " ..." : "", MenuLineStyle.Dim));
            _lineRows.Add(null);
        }
        lines.Add(new("", MenuLineStyle.Normal));
        _lineRows.Add(null);
        foreach (var d in detail)
        {
            lines.Add(new(d, Confirming ? MenuLineStyle.Selected : MenuLineStyle.Normal));
            _lineRows.Add(null);
        }
        return lines;
    }

    /// <summary>The first row shown (rows above it scrolled off).</summary>
    public int Scroll => _scroll;
}
