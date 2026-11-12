using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// P3 "Reset the game" (<c>docs/plan/16-reset-game.md</c>): the confirmation for one save, opened from a game tab's RESET
/// GAME row. The original's warning (DOS I OV1 "CAUTION !!! / ALL DATA ON YOUR GAME DISK WILL BE RESET"), then Keep / Erase
/// the characters, then YES / NO (NO first). The shell runs the reset on <see cref="TakeRequest"/> and shows the outcome
/// with <see cref="ShowResult"/>; any key then closes. Up/Down move, Return chooses, K / E and Y / N pick at once, Esc goes
/// back a step.
/// </summary>
public sealed class ResetGameScreen
{
    private enum Step { Choose, Sure, Done }

    private Step _step;
    private ResetMode _mode;
    private (SaveSlot Slot, ResetMode Mode)? _request;
    private string _result = "";
    private List<int?> _lineRows = [];

    public bool IsOpen { get; private set; }

    public GameId Game { get; private set; }

    /// <summary>The save being reset.</summary>
    public SaveSlot? Slot { get; private set; }

    /// <summary>The slot's label for the warning (AUTO / a number), or null for the game's one save.</summary>
    public string? SlotLabel { get; private set; }

    /// <summary>The selected row: Choose 0 Keep / 1 Erase; Sure 0 NO / 1 YES.</summary>
    public int Cursor { get; private set; }

    public string Help => _step == Step.Done
        ? UiText.S("reset.help_done")
        : PadWording.Active ? UiText.S("reset.help_pad")
        : _step == Step.Choose ? UiText.S("reset.help_choose") : UiText.S("reset.help_sure");

    /// <summary>Opens on <paramref name="slot"/>; <paramref name="label"/> names it in the warning (null: the game's one save).</summary>
    public void Open(GameId game, SaveSlot slot, string? label)
    {
        Game = game;
        Slot = slot ?? throw new ArgumentNullException(nameof(slot));
        SlotLabel = label;
        _step = Step.Choose;
        _request = null;
        _result = "";
        Cursor = 0;
        IsOpen = true;
    }

    /// <summary>Opens straight on a line and no question (the game has no save to reset).</summary>
    public void OpenNotice(GameId game, string line)
    {
        Game = game;
        Slot = null;
        _request = null;
        ShowResult(line);
        IsOpen = true;
    }

    /// <summary>The confirmed reset, once (null when none is waiting).</summary>
    public (SaveSlot Slot, ResetMode Mode)? TakeRequest()
    {
        var r = _request;
        _request = null;
        return r;
    }

    /// <summary>The reset ran (or failed): <paramref name="line"/> is shown until a key closes the screen.</summary>
    public void ShowResult(string line)
    {
        _result = line;
        _step = Step.Done;
        Cursor = 0;
    }

    public void Close() => IsOpen = false;

    /// <summary>A pad here: the D-pad moves, A chooses, B goes back.</summary>
    public static IEnumerable<Keys> PadKeys(IEnumerable<Buttons> buttons) => buttons.Select(b => b switch
    {
        Buttons.DPadUp or Buttons.LeftThumbstickUp => Keys.Up,
        Buttons.DPadDown or Buttons.LeftThumbstickDown => Keys.Down,
        Buttons.A or Buttons.Start => Keys.Enter,
        Buttons.B or Buttons.Back => Keys.Escape,
        _ => Keys.None,
    }).Where(k => k != Keys.None);

    /// <summary>Handles this frame's keys; true when the screen closed.</summary>
    public bool HandleKeys(IEnumerable<Keys> newKeys)
    {
        if (!IsOpen)
            return false;
        foreach (var key in newKeys)
        {
            if (_step == Step.Done)
            {
                Close();
                return true;
            }
            switch (key)
            {
                case Keys.Escape or Keys.Back:
                    if (_step == Step.Sure)
                    {
                        _step = Step.Choose;
                        Cursor = _mode == ResetMode.Keep ? 0 : 1;
                        break;
                    }
                    Close();
                    return true;
                case Keys.Up or Keys.Down:
                    Cursor = 1 - Cursor;
                    break;
                case Keys.Enter or Keys.Space:
                    Activate(Cursor);
                    break;
                case Keys.K when _step == Step.Choose:
                    Activate(0);
                    break;
                case Keys.E when _step == Step.Choose:
                    Activate(1);
                    break;
                case Keys.N when _step == Step.Sure:
                    Activate(0);
                    break;
                case Keys.Y when _step == Step.Sure:
                    Activate(1);
                    break;
            }
        }
        return false;
    }

    private void Activate(int row)
    {
        Cursor = row;
        if (_step == Step.Choose)
        {
            _mode = row == 0 ? ResetMode.Keep : ResetMode.Purge;
            _step = Step.Sure;
            Cursor = 0; // NO first
            return;
        }
        if (_step != Step.Sure)
            return;
        if (row == 0)
        {
            _step = Step.Choose;
            Cursor = _mode == ResetMode.Keep ? 0 : 1;
            return;
        }
        _request = (Slot!, _mode);
    }

    /// <summary>A click on line <paramref name="line"/>: its row is chosen (on the outcome page any click closes).</summary>
    public bool Click(int line)
    {
        if (!IsOpen)
            return false;
        if (_step == Step.Done)
        {
            Close();
            return true;
        }
        if (RowOfLine(line) is int row)
            Activate(row);
        return false;
    }

    /// <summary>The pointer over line <paramref name="line"/>: its row is selected.</summary>
    public void Point(int line)
    {
        if (IsOpen && _step != Step.Done && RowOfLine(line) is int row)
            Cursor = row;
    }

    public int? RowOfLine(int line) => line >= 0 && line < _lineRows.Count ? _lineRows[line] : null;

    public IReadOnlyList<MenuLine> Lines()
    {
        var lines = new List<MenuLine>
        {
            new(UiText.S("reset.title"), MenuLineStyle.Title),
            new(UiText.S("menu.game." + Game.Prefix()), MenuLineStyle.Header),
        };
        _lineRows = [null, null];
        void Add(string text, MenuLineStyle style, int? row = null)
        {
            foreach (var part in UiOverlays.Lines(text, SaveSlotScreen.Columns))
            {
                lines.Add(new(part, style));
                _lineRows.Add(row);
            }
        }
        if (_step == Step.Done)
        {
            Add("", MenuLineStyle.Normal);
            Add(_result, MenuLineStyle.Normal);
            return lines;
        }
        Add("", MenuLineStyle.Normal);
        Add(UiText.S("reset.caution"), MenuLineStyle.Selected);
        Add(SlotLabel is { } label ? UiText.F("reset.warning_slot_x", label) : UiText.S("reset.warning"), MenuLineStyle.Normal);
        Add("", MenuLineStyle.Normal);
        string[] rows = _step == Step.Choose
            ? [UiText.S("reset.keep"), UiText.S("reset.erase")]
            : [UiText.S("reset.no"), UiText.S("reset.yes")];
        if (_step == Step.Sure)
            Add(_mode == ResetMode.Keep ? UiText.S("reset.sure_keep") : UiText.S("reset.sure_erase"), MenuLineStyle.Header);
        for (var i = 0; i < rows.Length; i++)
            Add((i == Cursor ? ">" : " ") + rows[i], i == Cursor ? MenuLineStyle.Selected : MenuLineStyle.Normal, i);
        Add("", MenuLineStyle.Normal);
        Add(_step == Step.Choose ? UiText.S("reset.explain_choose") : UiText.S("reset.explain_sure"), MenuLineStyle.Dim);
        return lines;
    }
}
