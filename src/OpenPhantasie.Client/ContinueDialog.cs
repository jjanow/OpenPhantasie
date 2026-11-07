using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// CONTINUE SAVED GAME (manual, ENCORE: "Give the Guild number of the character you want to call up"; D-069): the Load key
/// reads the save and asks for a Guild number (1..22, as L)IST MEMBERS shows them), then <see cref="GameSession.Continue"/>
/// calls up that character's party where it was. Digits type the number (Up/Down step it), Return loads, Esc cancels.
/// A number that names nobody is refused and asked again.
/// <para>
/// QoL <see cref="GameSettings.ContinueFromList"/> (off = the manual's question): the save's parties instead, one row
/// each (<see cref="SeveralParties.Parties"/>), picked with Up/Down + Return; the last row, BY GUILD NUMBER, asks the
/// manual's question (Esc there goes back to the list). Rows sit one per line right under the two header lines.
/// </para>
/// <para>Also shows a notice panel (an old save that can't be loaded); any key closes it.</para>
/// </summary>
public sealed class ContinueDialog : IModalDialog, ITypedEntry, IPagedDialog
{
    public static string Title => UiText.S("continue.continue_saved_game");
    public static string Question => UiText.S("continue.guild_number_of_the_character");
    public static string ListQuestion => UiText.S("continue.which_party");
    private const int MaxDigits = 2;

    /// <summary>Digits a Guild number may have: 2, or more for a roster grown past 99 (QoL <c>guild-limit</c> = <c>none</c>).</summary>
    private static int DigitsFor(SessionSnapshot file) => Math.Max(MaxDigits, file.RosterSlots.Length.ToString().Length);

    /// <summary>List rows shown at once (the panel holds 13 lines: two header lines, the rows, "...", the names line).</summary>
    public const int VisibleRows = 8;

    /// <summary>The list's first row line (after the title and the question).</summary>
    public const int FirstRowLine = 2;

    /// <summary>Party column width: the names, padded to line the places up when short.</summary>
    private const int PartyWidth = 16;

    private SessionSnapshot? _file;
    private IReadOnlyList<SavedParty> _parties = [];
    private bool _list; // showing the party list (else the Guild-number question)
    private int _scroll;
    private string _number = "";
    private string _error = "";
    private bool _loaded;
    private IReadOnlyList<string>? _notice;

    public bool IsOpen => _file is not null || _notice is not null;

    /// <summary>True while the party list (not the Guild-number question) is shown.</summary>
    public bool ShowsList => _file is not null && _list;

    /// <summary>The Guild-number question takes digits (the mouse grid, <see cref="TypingGrid"/>).</summary>
    public string? TypingAlphabet => _file is not null && !_list && _notice is null ? "0123456789" : null;

    /// <summary>The selected list row: 0.. the parties, then BY GUILD NUMBER.</summary>
    public int Cursor { get; private set; }

    /// <summary>The first list row shown: panel line <see cref="FirstRowLine"/> + i is row <c>Scroll + i</c>.</summary>
    public int Scroll => _scroll;

    /// <summary>The parties the list offers (empty in the manual's question).</summary>
    public IReadOnlyList<SavedParty> Parties => _parties;

    public string Help =>
        _notice is not null ? UiText.S("continue.any_key")
        : _list ? UiText.S("continue.list_help")
        : UiText.S("continue.type_the_guild_number_enter_load");

    /// <param name="fromList">QoL <see cref="GameSettings.ContinueFromList"/>: the party list; a save with no party
    /// asks the manual's question.</param>
    public void Open(SessionSnapshot file, bool fromList = false)
    {
        _file = file ?? throw new ArgumentNullException(nameof(file));
        _parties = fromList ? SeveralParties.Parties(file) : [];
        _list = _parties.Count > 0;
        Cursor = 0;
        _scroll = 0;
        _number = "";
        _error = "";
        _loaded = false;
        _notice = null;
    }

    /// <summary>A notice panel under the title (an old save, …); any key closes it.</summary>
    public void OpenNotice(IReadOnlyList<string> lines)
    {
        _file = null;
        _notice = lines;
        _loaded = false;
    }

    /// <summary>True once after a successful load (the shell then resets its per-game state).</summary>
    public bool TakeLoaded()
    {
        var loaded = _loaded;
        _loaded = false;
        return loaded;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data)
    {
        if (_notice is { } notice)
            return [Title, "", .. notice];
        if (!_list)
            return _error.Length == 0 ? [Title, Question, $"> {_number}_"] : [Title, Question, $"> {_number}_", _error];

        var lines = new List<string> { Title, ListQuestion };
        var rows = _parties.Count + 1;
        for (var i = _scroll; i < Math.Min(rows, _scroll + VisibleRows); i++)
            lines.Add((i == Cursor ? ">" : " ") + RowText(i, data));
        if (rows > _scroll + VisibleRows)
            lines.Add(" ...");
        if (Cursor < _parties.Count)
            lines.Add(string.Join(" ", _parties[Cursor].Names));
        if (_error.Length > 0)
            lines.Add(_error);
        return lines;
    }

    /// <summary>The list's page when it has more rows than <see cref="VisibleRows"/> (the panel's bar).</summary>
    public ListPage? Page => _file is not null && _list
        ? ListPage.Of(FirstRowLine, VisibleRows, _scroll, _parties.Count + 1) : null;

    /// <summary>
    /// List row <paramref name="index"/> without its cursor column: every member's name (s99: not "leader +5"), then
    /// where; a row longer than the panel goes on, whole, on the panel's next line.
    /// </summary>
    public string RowText(int index, GameDataRepository data)
    {
        if (index >= _parties.Count)
            return UiText.S("continue.by_guild_number");
        var p = _parties[index];
        var who = string.Join(" ", p.Names);
        return $"{who.PadRight(PartyWidth - 1)} {Where(p, data)}";
    }

    private static string Where(SavedParty p, GameDataRepository data) => p.Mode switch
    {
        GameMode.Overworld => UiText.S("continue.where_wilderness"),
        GameMode.Dungeon => UiText.F("continue.where_dungeon_x", p.DungeonLevelId ?? 0),
        _ when p.TownId is int town && data.Towns.Any(t => t.Id == town) => data.GetTown(town).Name,
        _ => UiText.Name(p.Mode).ToUpperInvariant(),
    };

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        if (_notice is not null)
        {
            if (!newKeys.Any())
                return null;
            _notice = null;
            return "";
        }
        if (_file is not { } file)
            return null;
        foreach (var key in newKeys)
        {
            if (_list)
            {
                if (ListKey(key, file, session) is { } line)
                    return line;
                continue;
            }
            if (Digit(key) is int d)
            {
                if (_number.Length < DigitsFor(file))
                    _number += d;
                _error = "";
                continue;
            }
            switch (key)
            {
                case Keys.Escape when _parties.Count > 0:
                    _list = true; // BY GUILD NUMBER: Esc goes back to the party list
                    _number = "";
                    _error = "";
                    return ListQuestion;
                case Keys.Escape:
                    _file = null;
                    return UiText.S("continue.load_cancelled");
                case Keys.Back:
                    if (_number.Length > 0)
                        _number = _number[..^1];
                    break;
                case Keys.Up or Keys.Down:
                    var n = int.TryParse(_number, out var v) ? v : 0;
                    // The game's slots, or the file's longer roster (guild-limit = none).
                    n = Math.Clamp(n + (key == Keys.Up ? 1 : -1), 1, Math.Max(session.Rules.GuildSlotCount, file.RosterSlots.Length));
                    _number = n.ToString();
                    _error = "";
                    break;
                case Keys.Enter or Keys.Space when _number.Length > 0:
                    var result = Load(file, int.Parse(_number), session);
                    if (IsOpen)
                        _number = "";
                    return result;
            }
        }
        return null;
    }

    private string? ListKey(Keys key, SessionSnapshot file, GameSession session)
    {
        var rows = _parties.Count + 1;
        switch (key)
        {
            case Keys.Escape or Keys.Back:
                _file = null;
                return UiText.S("continue.load_cancelled");
            case Keys.Up:
                MoveTo((Cursor + rows - 1) % rows);
                return null;
            case Keys.Down:
                MoveTo((Cursor + 1) % rows);
                return null;
            case Keys.PageUp: // the mouse wheel: a step that stops at the ends
                MoveTo(Cursor - 1);
                return null;
            case Keys.PageDown:
                MoveTo(Cursor + 1);
                return null;
            case Keys.Enter or Keys.Space:
                if (Cursor < _parties.Count)
                    return Load(file, _parties[Cursor].GuildNumber, session);
                _list = false;
                _error = "";
                return Question;
        }
        return null;
    }

    /// <summary>The list row drawn on panel line <paramref name="line"/>, or null (header, "...", names, not the list).</summary>
    public int? RowAtLine(int line)
    {
        if (!ShowsList)
            return null;
        var i = line - FirstRowLine;
        var row = _scroll + i;
        return i >= 0 && i < VisibleRows && row <= _parties.Count ? row : null;
    }

    /// <summary>Select list row <paramref name="row"/> (scrolls it into view).</summary>
    public void MoveTo(int row)
    {
        Cursor = Math.Clamp(row, 0, _parties.Count);
        if (Cursor < _scroll) _scroll = Cursor;
        if (Cursor >= _scroll + VisibleRows) _scroll = Cursor - VisibleRows + 1;
        _error = "";
    }

    /// <summary>Continue with <paramref name="guildNumber"/>: closes on success; a refusal stays open with the error.</summary>
    private string Load(SessionSnapshot file, int guildNumber, GameSession session)
    {
        bool ok;
        string error;
        try
        {
            ok = session.Continue(file, guildNumber, out error);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException)
        {
            _file = null;
            return UiText.F("continue.load_failed_x", ex.Message);
        }
        if (!ok)
        {
            _error = error;
            return error;
        }
        _file = null;
        _loaded = true;
        return session.LastMessage;
    }

    private static int? Digit(Keys key) =>
        key is >= Keys.D0 and <= Keys.D9 ? key - Keys.D0
        : key is >= Keys.NumPad0 and <= Keys.NumPad9 ? key - Keys.NumPad0
        : null;
}
