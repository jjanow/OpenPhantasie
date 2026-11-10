using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>INPUT NAME (OV1 <c>0xfd3</c>): letters, digits and space from key edges; Backspace deletes, Enter accepts.</summary>
public sealed class NameEntry
{
    public const int MaxLength = GuildHall.MaxNameLength;

    /// <summary>What <see cref="Handle"/> types, in the mouse grid's order (<see cref="TypingGrid"/>); the space too.</summary>
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ";

    public string Text { get; private set; } = "";

    public void Clear() => Text = "";

    /// <summary>True on Enter; otherwise edits the text.</summary>
    public bool Handle(Keys key)
    {
        switch (key)
        {
            case Keys.Enter:
                return true;
            case Keys.Back when Text.Length > 0:
                Text = Text[..^1];
                break;
            case >= Keys.A and <= Keys.Z when Text.Length < MaxLength:
                Text += (char)('A' + (key - Keys.A));
                break;
            case >= Keys.D0 and <= Keys.D9 when Text.Length < MaxLength:
                Text += (char)('0' + (key - Keys.D0));
                break;
            case Keys.Space when Text.Length is > 0 and < MaxLength:
                Text += ' ';
                break;
        }
        return false;
    }

    public string Line => UiText.S("member.input_name") + Text + "_";
}

/// <summary>
/// Guild N)EW MEMBER (OV1 <c>0xbac</c>, manual p.4): race by number, then class, then the rolled attributes with a Y/N.
/// N drops the roll and starts again at the race menu (DOS frees the slot, <c>0xf5e</c> → <c>0xbb2</c>); Y asks INPUT NAME
/// (unique, `core.guild.name_not_unique`), then shows the full sheet until SPACE. Esc backs out before the name is accepted.
/// <para>
/// T5 lineage (<see cref="GameSession.LineageOffered"/>, <c>ancestry.md</c> section 13): with the option on, a first
/// question picks the plain roll or a descendant. A descendant picks ancestor 1 and ancestor 2 from the shared Hall list
/// (first row: a new ancestor; arrows page the list), a refused pair says why and asks again; the race menu only when both
/// are new; then the class, the inherited roll and the usual Y/N, name and sheet. N goes back to the first question.
/// </para>
/// </summary>
public sealed class GuildNewDialog : IModalDialog, ITypedEntry, IPagedDialog, IPadYesNo, IPadArrows, IPadWheel
{
    /// <summary>Pad fix B7 (s188): the pad wheel on the kind, race and class numbers and the name (the ancestor lists have their cursor) (<see cref="IPadWheel"/>).</summary>
    public string? PadWheel(GameSession session) => _step switch
    {
        Step.Kind => DialogPad.Picks(2),
        Step.Race => DialogPad.Picks(_descendant ? BaseRaceRows : RaceMenu.Length),
        Step.Class => DialogPad.Picks(ClassMenu.Length),
        Step.Name => NameEntry.Alphabet,
        _ => null,
    };

    private enum Step { Closed, Kind, Ancestor1, Ancestor2, Race, Class, Accept, Name, Sheet }

    /// <summary>Hall-list rows shown at once; the rest scroll (with the title and the three lines under it, the box needs no scroll of its own).</summary>
    public const int AncestorRows = 8;

    /// <summary>The descendant's race menu: Human … Gnome (no RANDOM; other races come from a person ancestor).</summary>
    private const int BaseRaceRows = 5;

    // Race menu (COM 0x5ff3): the five player races, then RANDOM (a monster race, OV1 0x0c2d).
    private static string[] RaceMenu => [UiText.S("member.human"), UiText.S("member.dwarf"), UiText.S("member.elf"), UiText.S("member.halfling"), UiText.S("member.gnome"), UiText.S("member.random")];
    private static string[] ClassMenu => [UiText.S("member.thief"), UiText.S("member.monk"), UiText.S("member.ranger"), UiText.S("member.fighter"), UiText.S("member.priest"), UiText.S("member.wizard")];

    private readonly Func<GameSession, LineageSetup?>? _lineageSource;
    private Step _step;
    private int _race;
    private Character? _rolled;
    private string _error = "";
    private readonly NameEntry _name = new();

    // T5 lineage: the setup read when the descendant path opens, the picks (null = a new ancestor) and the list cursor.
    private LineageSetup? _lineage;
    private bool _descendant;
    private LineagePerson? _first;
    private LineagePerson? _second;
    private int _cursor;
    private int _scroll;

    /// <param name="lineageSource">Reads the shared Hall list for a descendant (T5); null or a null result = no lineage.</param>
    public GuildNewDialog(Func<GameSession, LineageSetup?>? lineageSource = null) => _lineageSource = lineageSource;

    public bool IsOpen => _step != Step.Closed;

    /// <summary>
    /// s156: the plain roll waiting for its Y / N (the Amiga's PURGE / KEEP sheet, h23 <c>+0xc08</c>), or null: any other step,
    /// or a descendant (its ancestors' line has no place on that sheet).
    /// </summary>
    public Character? RolledMember => _step == Step.Accept && !_descendant ? _rolled : null;

    /// <summary>
    /// s165: counts the plain roll's race and class picks: I Amiga's list window h23 <c>+0x970</c> (requester 1, the races,
    /// h23 <c>+0x8cc</c>; requester 2, the classes under "CREATING %s", <c>+0x8d8</c>) plays the tone (<c>+0xa34</c>) when
    /// O.K. is pressed with a row selected, not on its cancel. A descendant's lists (T5, the remaster's) are not counted.
    /// </summary>
    public int ListTones { get; private set; }

    public ListPage? Page { get; private set; }

    public string? TypingAlphabet => _step == Step.Name ? NameEntry.Alphabet : null;

    /// <summary>The rolled member's keep question (Y / N) is up.</summary>
    public bool AsksYesNo => _step == Step.Accept;

    /// <summary>The enrolled member's sheet (PRESS SPACE) types nothing: no pad wheel or wheel hint there (s182 live pass:
    /// the name's wheel hint stayed on it). Left / Right reach it as arrows, which it ignores.</summary>
    public bool TakesLeftRight => _step == Step.Sheet;

    public string Help => _step switch
    {
        Step.Kind => UiText.S("lineage.kind_help"),
        Step.Ancestor1 or Step.Ancestor2 => UiText.S("lineage.pick_help"),
        Step.Race when _descendant => UiText.S("lineage.race_help"),
        Step.Race => UiText.S("member.1_6_race_esc_cancel"),
        Step.Class => UiText.S("member.1_6_class_esc_back"),
        Step.Accept => UiText.S("member.y_keep_n_roll_again"),
        Step.Name => UiText.S("member.type_name_enter_accept_esc_cancel"),
        _ => UiText.S("member.space_done"),
    };

    public string? Open(GameSession session)
    {
        if (session.Mode is not GameMode.Guild)
            return UiText.S("member.only_in_the_guild");
        if (!session.GuildHasRoom)
            return UiText.S("member.guild_roster_is_full");
        _step = session.LineageOffered ? Step.Kind : Step.Race;
        _descendant = false;
        _rolled = null;
        _error = "";
        return null;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data)
    {
        Page = null;
        return _step switch
        {
            Step.Kind => [UiText.S("lineage.kind_title"), $"1 {UiText.S("lineage.plain")}", $"2 {UiText.S("lineage.descendant")}", "", _error],
            Step.Ancestor1 or Step.Ancestor2 => AncestorLines(),
            Step.Race when _descendant => [UiText.S("member.select_race"), .. RaceMenu.Take(BaseRaceRows).Select((r, i) => $"{i + 1} {r}"), "", _error],
            Step.Race => [UiText.S("member.select_race"), .. RaceMenu.Select((r, i) => $"{i + 1} {r}")],
            Step.Class => [UiText.S("member.select_class"), .. ClassMenu.Select((c, i) => $"{i + 1} {c}")],
            Step.Accept => [.. AttributeLines(_rolled!), .. AncestryLines(_rolled!), "", UiText.S("member.accept_y_n")],
            Step.Name => [.. AttributeLines(_rolled!), .. AncestryLines(_rolled!), "", _name.Line, _error],
            Step.Sheet => [.. InspectDialog.SheetLines(_rolled!, treasury: session.TreasuryOn), "", UiText.S("member.press_space")],
            _ => [],
        };
    }

    /// <summary>The Hall list: the title, the new-ancestor row, then the persons; ancestor 1 and a refusal's reason under it.</summary>
    private IReadOnlyList<string> AncestorLines()
    {
        var rows = AncestorRowCount;
        Page = ListPage.Of(1, AncestorRows, _scroll, rows);
        var title = _step == Step.Ancestor1 ? UiText.S("lineage.first_ancestor") : UiText.S("lineage.second_ancestor");
        var lines = new List<string> { title };
        for (var i = _scroll; i < Math.Min(rows, _scroll + AncestorRows); i++)
            lines.Add((i == _cursor ? "> " : "  ") + RowText(i));
        lines.Add("");
        if (_step == Step.Ancestor2)
            lines.Add(UiText.F("lineage.first_is_x", PersonName(_first)));
        lines.Add(_error);
        return lines;
    }

    private int AncestorRowCount => 1 + (_lineage?.Persons.Count ?? 0);

    private LineagePerson? PersonAt(int row) => row == 0 ? null : _lineage!.Persons[row - 1];

    private string RowText(int row) => PersonAt(row) is { } p
        ? UiText.F("lineage.person_x_x_x_x", p.Name.ToUpperInvariant(), UiText.Name(p.Race).ToUpperInvariant(), ClassText(p),
            SourceText(p.Source))
        : UiText.S("lineage.new_ancestor");

    private static string SourceText(GameId game) => game switch
    {
        GameId.P1 => UiText.S("lineage.from_p1"),
        GameId.P2 => UiText.S("lineage.from_p2"),
        GameId.P3 => UiText.S("lineage.from_p3"),
        _ => UiText.S("lineage.from_p4"),
    };

    private static string ClassText(LineagePerson p) =>
        (p.Class is >= 0 and <= (int)CharacterClass.Wizard ? UiText.Name((CharacterClass)p.Class) : p.ClassName ?? $"#{p.Class}")
        .ToUpperInvariant();

    private static string PersonName(LineagePerson? p) => p is null ? UiText.S("lineage.new") : p.Name.ToUpperInvariant();

    private static string ParentName(ParentRef p) => p.Name is { } n ? n.ToUpperInvariant() : UiText.S("lineage.new");

    /// <summary>A descendant's two ancestors under the roll (nothing for a plain roll).</summary>
    private static IReadOnlyList<string> AncestryLines(Character ch) =>
        ch.Lineage is { Parents.Count: 2 } l ? [UiText.F("lineage.ancestors_x_x", ParentName(l.Parents[0]), ParentName(l.Parents[1]))] : [];

    private static IReadOnlyList<string> AttributeLines(Character ch)
    {
        var st = ch.Stats;
        return
        [
            $"{ClassMenu[(int)ch.Class]} {UiText.Name(ch.Race).ToUpperInvariant()}",
            UiText.F("member.strength_x", st.Str),
            UiText.F("member.intelligence_x", st.Int),
            UiText.F("member.dexterity_x", st.Dex),
            UiText.F("member.constitution_x", st.Con),
            UiText.F("member.charisma_x", st.Cha),
            UiText.F("member.luck_x", st.Luck),
        ];
    }

    private static string RefusalText(LineageRefusal r) => r switch
    {
        LineageRefusal.MixedRaces => UiText.S("lineage.refused.mixed"),
        LineageRefusal.RaceNotOffered => UiText.S("lineage.refused.race"),
        LineageRefusal.SamePerson => UiText.S("lineage.refused.same"),
        _ => UiText.S("lineage.refused.shipped"),
    };

    private void StartAncestors(Step step)
    {
        _step = step;
        _cursor = 0;
        _scroll = 0;
        _error = "";
    }

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            switch (_step)
            {
                case Step.Kind:
                    if (key is Keys.Escape or Keys.Back)
                    {
                        _step = Step.Closed;
                        return "";
                    }
                    if (DialogKeys.Digit(key) == 1)
                    {
                        _descendant = false;
                        _error = "";
                        _step = Step.Race;
                        return null;
                    }
                    if (DialogKeys.Digit(key) == 2)
                    {
                        _lineage = ReadLineage(session);
                        if (_lineage is null)
                        {
                            _error = UiText.S("lineage.no_data");
                            return null;
                        }
                        _descendant = true;
                        StartAncestors(Step.Ancestor1);
                        return null;
                    }
                    break;
                case Step.Ancestor1 or Step.Ancestor2:
                    if (key is Keys.Escape or Keys.Back)
                    {
                        if (_step == Step.Ancestor1)
                        {
                            _error = "";
                            _step = Step.Kind;
                        }
                        else
                        {
                            StartAncestors(Step.Ancestor1);
                        }
                        return null;
                    }
                    if (key is Keys.Up)
                        _cursor = Math.Max(0, _cursor - 1);
                    else if (key is Keys.Down)
                        _cursor = Math.Min(AncestorRowCount - 1, _cursor + 1);
                    else if (key is Keys.PageUp)
                        _cursor = Math.Max(0, _cursor - AncestorRows);
                    else if (key is Keys.PageDown)
                        _cursor = Math.Min(AncestorRowCount - 1, _cursor + AncestorRows);
                    else if (key is Keys.Enter or Keys.Space)
                    {
                        PickAncestor(session);
                        return null;
                    }
                    _scroll = Math.Clamp(_scroll, Math.Max(0, _cursor - AncestorRows + 1), _cursor);
                    break;
                case Step.Race:
                    if (key is Keys.Escape or Keys.Back)
                    {
                        if (_descendant)
                        {
                            StartAncestors(Step.Ancestor2);
                            return null;
                        }
                        _step = Step.Closed;
                        return "";
                    }
                    if (DialogKeys.Digit(key) is var r and >= 1 and <= 6 && (!_descendant || r <= BaseRaceRows))
                    {
                        if (!_descendant)
                            ListTones++;
                        _race = r - 1;
                        _error = "";
                        _step = Step.Class;
                        return null;
                    }
                    break;
                case Step.Class:
                    if (key is Keys.Escape or Keys.Back)
                    {
                        if (_descendant && (_first is not null || _second is not null))
                            StartAncestors(Step.Ancestor2);
                        else
                            _step = Step.Race;
                        return null;
                    }
                    if (DialogKeys.Digit(key) is var c and >= 1 and <= 6)
                    {
                        if (!_descendant)
                            ListTones++;
                        var cls = (CharacterClass)(c - 1);
                        if (_descendant)
                        {
                            _rolled = session.RollDescendant(_lineage!, _first, _second, (Race)_race, cls);
                        }
                        else
                        {
                            var random = _race == RaceMenu.Length - 1;
                            _rolled = session.RollNewMember(random ? Race.Human : (Race)_race, cls, random);
                        }
                        _step = Step.Accept;
                        return null;
                    }
                    break;
                case Step.Accept:
                    if (key == Keys.Y)
                    {
                        session.KeepRolledMember(); // III: sound 6 (Amiga 0x13938)
                        _name.Clear();
                        _error = "";
                        _step = Step.Name;
                        return null;
                    }
                    if (key is Keys.N or Keys.Escape)
                    {
                        _rolled = null;
                        // A descendant's N goes back to the first question, as IV's Cansel drops both ancestors.
                        _step = _descendant ? Step.Kind : Step.Race;
                        _descendant = false;
                        return null;
                    }
                    break;
                case Step.Name:
                    if (key == Keys.Escape)
                    {
                        _rolled = null;
                        _step = Step.Closed;
                        return "";
                    }
                    if (!_name.Handle(key))
                        break;
                    if (_name.Text.Trim().Length == 0)
                        break;
                    if (!session.TryEnrollNewMember(_rolled!, _name.Text, out var error))
                    {
                        _error = error;
                        _name.Clear();
                        break;
                    }
                    _step = Step.Sheet;
                    return session.LastMessage;
                case Step.Sheet:
                    if (key is Keys.Space or Keys.Enter or Keys.Escape)
                    {
                        _step = Step.Closed;
                        return "";
                    }
                    break;
            }
        }
        return null;
    }

    /// <summary>Enter on the Hall list: ancestor 1 is kept; ancestor 2 is checked with it (a refusal says why and stays).</summary>
    private void PickAncestor(GameSession session)
    {
        var picked = PersonAt(Math.Clamp(_cursor, 0, AncestorRowCount - 1));
        if (_step == Step.Ancestor1)
        {
            if (picked is not null && !LineageRules.OffersRace(session.Game, picked.Race))
            {
                _error = RefusalText(LineageRefusal.RaceNotOffered);
                return;
            }
            _first = picked;
            StartAncestors(Step.Ancestor2);
            return;
        }
        var pair = LineageRules.Pair(session.Game, _first, picked, Race.Human);
        if (pair.Refusal != LineageRefusal.None)
        {
            _error = RefusalText(pair.Refusal);
            return;
        }
        _second = picked;
        _error = "";
        if (_first is null && _second is null)
        {
            _step = Step.Race;
            return;
        }
        _race = (int)pair.Race;
        _step = Step.Class;
    }

    private LineageSetup? ReadLineage(GameSession session)
    {
        try
        {
            return _lineageSource?.Invoke(session);
        }
        catch (Exception) // IV's data missing or unreadable: no descendants, the plain roll still works
        {
            return null;
        }
    }
}

/// <summary>
/// Guild P)URGE (OV1 <c>0x23d5</c>, `member.whom_do_you_want_to_purge`) and R)ENAME (OV1 <c>0x22f6</c>, `member.who_needs_renaming`):
/// arrows + Return pick a Guild member. Purge deletes at once (no confirm in I's DOS) and asks again; s160: in III a pick
/// that would not be refused first waits for DOS III's "Are you sure you want to purge this character permanently?" (S407,
/// <see cref="PurgeAsked"/>, answered through <see cref="AnswerPurge"/>). Rename asks INPUT NAME and lists only members
/// outside every party (D-065, manual), or everyone when the save's <c>p1.rename-party</c> allows it
/// (<see cref="GameSession.CanRename"/>).
/// </summary>
public sealed class GuildPickDialog : IModalDialog, ITypedEntry, IPagedDialog
{
    public ListPage? Page { get; private set; }

    public static string PurgeTitle => UiText.S("member.whom_do_you_want_to_purge");
    public static string RenameTitle => UiText.S("member.who_needs_renaming");
    public static string ExamineTitle => UiText.S("member.who_shall_be_examined");

    /// <summary>s160: III's question before a purge (DOS S407, Amiga <c>0x230cc</c>), the "\r" its row break.</summary>
    public static string PurgeQuestion => UiText.S("p3bar.confirm_purge");

    private readonly NameEntry _name = new();
    private bool _open;
    private bool _rename;
    private Action<Character>? _examine; // s158: Examine mode (III's Guild › Examine): a pick opens the member's sheet
    private Character? _renaming;
    private string _error = "";
    private int _scroll;

    /// <summary>
    /// s160: the III member picked for a purge while the front end asks S407 (root <c>0xb075</c>); the list is not open
    /// meanwhile (the question owns the keys) and comes back after the answer (<see cref="AnswerPurge"/>).
    /// </summary>
    public Character? PurgeAsked { get; private set; }

    /// <summary>
    /// s165: counts the Purge / Rename list's answers (a row picked, or Esc / Backspace): I Amiga's roster list h26
    /// <c>+0x346</c> → <c>+0x412</c> (`member.whom_do_you_want_to_purge` h5 <c>+0xe62</c>, `member.who_needs_renaming` <c>+0xf1a</c>)
    /// plays the tone after each gadget (<c>+0x614</c>). III's Examine list is not counted.
    /// </summary>
    public int ListTones { get; private set; }

    public bool IsOpen => _open && PurgeAsked is null;

    public string? TypingAlphabet => _open && _renaming is not null ? NameEntry.Alphabet : null;

    public int Cursor { get; private set; }

    public string Help => _renaming is not null
        ? UiText.S("member.type_name_enter_accept_esc_cancel")
        : _examine is not null ? UiText.S("member.arrows_choose_enter_examine_esc_done")
        : _rename ? UiText.S("member.arrows_choose_enter_rename_esc_done") : UiText.S("member.arrows_choose_enter_purge_esc_done");

    private IReadOnlyList<Character> Rows(GameSession session)
    {
        var rows = session.Roster.Occupied.Where(c => !_rename || session.CanRename(c)).ToList(); // D-065 (p1.rename-party)
        // s172: III's Purge list holds the elemental records too, as DOS III's roster (slots 0-2) and the Amiga's: picking
        // one is refused with S406 (guild_menu 0xafe3; Amiga 0xb524). Only once a
        // character is listed: DOS opens Purge only with more than the three elemental records.
        if (!_rename && _examine is null && ListsElementalsForPurge(session.Game) && rows.Count > 0)
            rows.AddRange(session.ElementalTemplates);
        return rows;
    }

    /// <summary>s172: III's Purge list shows the elementals (refused when picked, DOS S406); I's and II's do not (ST <c>0xd68a</c>: 22 rows).</summary>
    public static bool ListsElementalsForPurge(GameId game) => game == GameId.P3;


    public string? OpenPurge(GameSession session) => Open(session, rename: false);

    public string? OpenRename(GameSession session) => Open(session, rename: true);

    /// <summary>
    /// s158: III's Guild › Examine (Amiga <c>0xbabc</c>, DOS guild_menu item 6): every roster member is listed (the party,
    /// other towns' members too); a pick calls <paramref name="examine"/> (the member's sheet) and the list asks again until
    /// Esc. An empty roster is refused with the roster line (S408 in III).
    /// </summary>
    public string? OpenExamine(GameSession session, Action<Character> examine)
    {
        ArgumentNullException.ThrowIfNull(examine);
        if (session.Mode is not GameMode.Guild)
            return UiText.S("member.only_in_the_guild");
        if (!session.Roster.Occupied.Any())
            return UiText.S("guild.roster_empty");
        _rename = false;
        _examine = examine;
        PurgeAsked = null;
        _open = true;
        _renaming = null;
        _error = "";
        Cursor = 0;
        _scroll = 0;
        return null;
    }

    private string? Open(GameSession session, bool rename)
    {
        if (session.Mode is not GameMode.Guild)
            return UiText.S("member.only_in_the_guild");
        _examine = null;
        PurgeAsked = null;
        _rename = rename;
        if (Rows(session).Count == 0)
            return rename && session.Roster.Occupied.Any() ? GameSession.RenamePartyMemberRefusal : UiText.S("member.the_guild_has_no_members");
        _open = true;
        _renaming = null;
        _error = "";
        Cursor = 0;
        _scroll = 0;
        return null;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data)
    {
        Page = null;
        if (_renaming is not null)
            return [RenameTitle, _renaming.Name, "", _name.Line, _error];
        var rows = Rows(session);
        Page = ListPage.Of(1, 10, _scroll, rows.Count);
        return
        [
            _examine is not null ? ExamineTitle : _rename ? RenameTitle : PurgeTitle,
            .. rows.Skip(_scroll).Take(10).Select((c, i) =>
                (i + _scroll == Cursor ? "> " : "  ") + $"{c.Name}  {UiText.Name(c.Class).ToUpperInvariant()}"),
        ];
    }

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            if (_renaming is { } who)
            {
                if (key == Keys.Escape)
                {
                    _renaming = null;
                    return "";
                }
                if (!_name.Handle(key) || _name.Text.Trim().Length == 0)
                    continue;
                if (!session.TryRename(who, _name.Text, out var error))
                {
                    _error = error;
                    _name.Clear();
                    continue;
                }
                _renaming = null;
                return session.LastMessage;
            }

            var rows = Rows(session);
            switch (key)
            {
                case Keys.Escape or Keys.Back:
                    if (_examine is null)
                        ListTones++;
                    _open = false;
                    return "";
                case Keys.Up:
                    Cursor = Math.Max(0, Cursor - 1);
                    break;
                case Keys.Down:
                    Cursor = Math.Min(rows.Count - 1, Cursor + 1);
                    break;
                case Keys.Enter or Keys.Space when rows.Count > 0:
                    if (_examine is null)
                        ListTones++;
                    var member = rows[Math.Clamp(Cursor, 0, rows.Count - 1)];
                    if (_examine is { } examine)
                    {
                        examine(member);
                        return null;
                    }
                    if (_rename)
                    {
                        _renaming = member;
                        _name.Clear();
                        _error = "";
                        return null;
                    }
                    if (AsksBeforePurge(session.Game))
                    {
                        // DOS III refuses first (an elemental: S406; a party member under elemental-purge refused: S448), then asks (0xb075).
                        if (session.PurgeRefusal(member) is { } refusal)
                            return refusal;
                        PurgeAsked = member;
                        return null;
                    }
                    return Purge(session, member);
            }
            _scroll = Math.Clamp(_scroll, Math.Max(0, Cursor - 9), Cursor);
        }
        return null;
    }

    /// <summary>s160: III asks S407 before a purge (DOS guild_menu root <c>0xb075</c>, Amiga <c>0xb4c2</c>); I does not.</summary>
    public static bool AsksBeforePurge(GameId game) => game == GameId.P3;

    /// <summary>
    /// s160: III's Guild examines any roster member (DOS guild_menu item 6 "Inspect", Amiga Guild › Examine), so the
    /// Guild's I key opens <see cref="OpenExamine"/> under every III layout; I's Guild has no such item.
    /// </summary>
    public static bool ExaminesRoster(GameId game) => game == GameId.P3;

    /// <summary>
    /// s160: the answer to S407 for <see cref="PurgeAsked"/>: Yes purges (the list asks again, as after any purge), No
    /// leaves the member; either way the list is back. Returns the line for the band, or null when nothing was asked.
    /// </summary>
    public string? AnswerPurge(GameSession session, bool yes)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (PurgeAsked is not { } member)
            return null;
        PurgeAsked = null;
        return yes ? Purge(session, member) : "";
    }

    /// <summary>Leave without an answer (a load replaced the session): the asked member stays, the list closes.</summary>
    public void Close()
    {
        PurgeAsked = null;
        _open = false;
        _renaming = null;
    }

    private string Purge(GameSession session, Character member)
    {
        var ok = session.TryPurge(member, out var err);
        var rows = Rows(session);
        if (rows.Count == 0)
            _open = false;
        Cursor = Math.Clamp(Cursor, 0, Math.Max(0, rows.Count - 1));
        return ok ? session.LastMessage : err;
    }
}

/// <summary>
/// Guild L)IST MEMBERS (manual p.4): Guild number, name, class and town of residence ("Pelnor is Town #1"); SPACE goes back
/// to the Guild menu. Arrows scroll a long roster. A never-moved member holds town 0 (= Pelnor, as DOS <c>@254</c> = 0) and
/// is listed as town 1.
/// </summary>
public sealed class GuildListDialog : IModalDialog, IPagedDialog
{
    public ListPage? Page { get; private set; }

    public static string Title => UiText.S("member.guild_members");
    private const int PageRows = 12;
    private int _scroll;

    public bool IsOpen { get; private set; }

    public string Help => UiText.S("member.arrows_scroll_space_done");

    public string? Open(GameSession session)
    {
        if (session.Mode is not GameMode.Guild)
            return UiText.S("member.only_in_the_guild");
        IsOpen = true;
        _scroll = 0;
        return null;
    }

    public static IReadOnlyList<string> Rows(GameSession session) =>
        session.Roster.Slots
            .Select((c, i) => (Char: c, Number: i + 1))
            .Where(x => x.Char is not null)
            .Select(x => UiText.F("member.x_x_x_town_x", x.Number, x.Char!.Name, ShortClass(x.Char.Class), Math.Max(1, x.Char.CurrentTown)))
            .ToList();

    private static string ShortClass(CharacterClass c) => c switch
    {
        CharacterClass.Thief => UiText.S("member.th"),
        CharacterClass.Monk => UiText.S("member.mo"),
        CharacterClass.Ranger => UiText.S("member.ra"),
        CharacterClass.Fighter => UiText.S("member.fi"),
        CharacterClass.Priest => UiText.S("member.pr"),
        _ => UiText.S("member.wi"),
    };

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data)
    {
        var rows = Rows(session);
        Page = ListPage.Of(1, PageRows, _scroll, rows.Count);
        return rows.Count == 0
            ? [Title, UiText.S("member.no_members")]
            : [Title, .. rows.Skip(_scroll).Take(PageRows)];
    }

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            switch (key)
            {
                case Keys.Space or Keys.Enter or Keys.Escape or Keys.Back:
                    IsOpen = false;
                    return "";
                case Keys.Up:
                    _scroll = Math.Max(0, _scroll - 1);
                    break;
                case Keys.Down:
                    _scroll = Math.Clamp(_scroll + 1, 0, Math.Max(0, Rows(session).Count - PageRows));
                    break;
            }
        }
        return null;
    }
}

/// <summary>
/// <c>--test-party</c> (dev aid, PLAN P2): a full six-member party, one per class, already in the Pelnor Guild with its
/// bank gold withdrawn. <c>--test-level N</c> (1..15) builds the same party and trains each member up to level N through
/// the real Guild training (<see cref="GameSession.Train"/>: HP, MP ceiling, skills, learnable spells), with the XP and fee
/// supplied and the party's own gold left as it was. Every test party then starts ready to play: healed, MP at the
/// ceiling, every spell its level allows already learned, and gear fitting the level (<see cref="Outfit"/>). A normal
/// start has an empty roster.
/// </summary>
public static class TestParty
{
    public const string Switch = "--test-party";
    public const string LevelSwitch = "--test-level";
    public const string GameSwitch = "--test-game";

    /// <summary>
    /// A developer switch to watch I's victory screen (s127): with a test party, the party is put in the Black Lord's fight
    /// (DNG9) and wins it at once (<see cref="WinBlackLordFight"/>).
    /// </summary>
    public const string VictorySwitch = "--p1-victory";

    /// <summary>A developer switch to watch I's party defeat (s153): the test party's members all DEAD in the wilderness,
    /// `core.defeat.your_party_has_been_defeated` waiting (<see cref="Defeat"/>).</summary>
    public const string DefeatSwitch = "--p1-defeat";

    /// <summary>Every member DEAD at 0 HP on the wilderness and the defeat pending, as after a lost fight.</summary>
    public static void Defeat(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DepartGuildToWilderness();
        foreach (var m in session.Party.Members)
        {
            m.HpCur = 0;
            m.Status = CharacterStatus.Dead;
        }
        session.CampaignAccess.PartyDefeatPending = true;
    }

    /// <summary>
    /// The Black Lord's fight, won before its first round ends: every monster at 0 HP, one round resolved, so Core's
    /// victory (Zeus' screen, the GUILD.DAT write, DIVINE I) runs as after a real fight. For <see cref="VictorySwitch"/>.
    /// </summary>
    public static void WinBlackLordFight(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DepartGuildToWilderness();
        session.EnterDungeon(9, 15, 0);
        session.BeginDungeonCombat(Endgame.BlackLordId, SurpriseFlags.Normal);
        foreach (var m in session.Combat!.AllMonsters)
            m.HitPoints = 0;
        session.ResolveCombatRound();
    }

    /// <summary>
    /// A developer switch to watch I's combat screen (s132): <c>--p1-combat [N]</c> with a test party puts the party in the
    /// wilderness in a fight with monster N (default <see cref="DefaultCombatMonster"/>), at once. s136: also in II (its
    /// line-by-line round, <see cref="P2CombatLineRun"/>).
    /// </summary>
    public const string CombatSwitch = "--p1-combat";

    /// <summary>The monster <see cref="CombatSwitch"/> starts with when no number follows it (the Bantir of s128's capture).</summary>
    public const int DefaultCombatMonster = 1;

    /// <summary>The monster id after <see cref="CombatSwitch"/> (the default without a number), or null without the switch.</summary>
    public static int? CombatMonster(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var i = args.ToList().IndexOf(CombatSwitch);
        if (i < 0)
            return null;
        return i + 1 < args.Count && int.TryParse(args[i + 1], out var id) && id >= 0 ? id : DefaultCombatMonster;
    }

    /// <summary>
    /// With <see cref="CombatSwitch"/>, <c>--p1-surprise</c> opens the fight as "%s HEARS MONSTERS" → FIGHT → "YOU SURPRISE
    /// THEM !" does (s138, to film the surprise opening: <see cref="SurpriseFlags.MonstersSurprised"/>, round 1 before
    /// Party Options).
    /// </summary>
    public const string SurpriseSwitch = "--p1-surprise";

    /// <summary>
    /// With <see cref="CombatSwitch"/>, <c>--p1-map-boxes</c> first plays I Amiga's encounter boxes over the map (s139,
    /// <see cref="P1MapMessageRun"/>): `core.encounter.a_night_encounter`, then "%s IS ON WATCH" / `core.encounter.and_hears_the_monsters` for the first
    /// member; with <see cref="SurpriseSwitch"/> `core.encounter.you_surprise_them` (the remaster's D-058 flash is no box since s140: the
    /// fight's line shows it). A preview: Core's fight is the switch's, the lines are not rolled.
    /// </summary>
    public const string MapBoxesSwitch = "--p1-map-boxes";

    /// <summary>The lines <see cref="MapBoxesSwitch"/> boxes, or null without the switch.</summary>
    public static IReadOnlyList<string>? MapBoxLines(IReadOnlyList<string> args, GameSession session)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(session);
        if (!args.Contains(MapBoxesSwitch) || session.Party.Members.Count == 0)
            return null;
        return args.Contains(SurpriseSwitch)
            ? [WildernessEncounter.YouSurpriseThem]
            : [NightEncounter.Banner, GameText.F("core.encounter.x_on_watch_hears", session.Party.Members[0].Name)];
    }

    /// <summary>
    /// s141: a developer switch to watch I Amiga's dungeon boxes (<see cref="P1MapMessageRun"/>, FORMAT.md "The dungeon's
    /// requester 23"): <c>--p1-dungeon L X Y</c> with a test party puts the party on cell (X, Y) of dungeon level L; the
    /// player's own steps then run the level's real cells (a wall's beep and box, an event's sounds, boxes and report).
    /// E.g. <c>--p1-dungeon 2 6 28</c>: one step south is DNG2's robed men (BRIBE: A SPELL IS CAST, the report, THE PARTY
    /// IS MOVED).
    /// </summary>
    public const string DungeonSwitch = "--p1-dungeon";

    /// <summary>The (level, x, y) after <see cref="DungeonSwitch"/>, or null without the switch or its three numbers.</summary>
    public static (int Level, int X, int Y)? DungeonCell(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var i = args.ToList().IndexOf(DungeonSwitch);
        if (i < 0 || i + 3 >= args.Count)
            return null;
        return int.TryParse(args[i + 1], out var level) && int.TryParse(args[i + 2], out var x) && int.TryParse(args[i + 3], out var y)
            ? (level, x, y) : null;
    }

    /// <summary>The party leaves the Guild for the wilderness and enters dungeon <paramref name="cell"/>'s level there (<see cref="DungeonSwitch"/>).</summary>
    public static void EnterDungeon(GameSession session, (int Level, int X, int Y) cell)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DepartGuildToWilderness();
        session.EnterDungeon(cell.Level, cell.X, cell.Y);
    }

    /// <summary>
    /// <see cref="DungeonSwitch"/> in III (s163, to watch the dungeon step's windows: a trap, a door with monsters, a locked
    /// door): the test party leaves the Guild and the town and enters dungeon <paramref name="cell"/>'s level on its cell,
    /// the original map (no saved one). E.g. <c>--p1-dungeon 5 3 15</c>: one step south is the Pendragon archives' door
    /// 0xc5 (monsters behind it); <c>5 23 7</c>: south is trap 2; <c>5 15 22</c>: north is the locked door 0xc6.
    /// </summary>
    public static void EnterP3Dungeon(GameSession session, (int Level, int X, int Y) cell)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.LeaveGuild();
        session.LeaveTown();
        session.EnterDungeon(cell.Level, cell.X, cell.Y, loadSaved: false);
    }

    /// <summary>The opening <see cref="CombatSwitch"/> asks for: <see cref="SurpriseSwitch"/> or the normal one.</summary>
    public static SurpriseFlags CombatSurprise(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Contains(SurpriseSwitch) ? SurpriseFlags.MonstersSurprised : SurpriseFlags.Normal;
    }

    /// <summary>The fight for <see cref="CombatSwitch"/>: the party leaves the Guild for the wilderness and meets <paramref name="monsterId"/>.</summary>
    public static void StartFight(GameSession session, int monsterId, SurpriseFlags? surprise = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DepartGuildToWilderness();
        session.BeginCombat(monsterId, surprise);
    }

    /// <summary>
    /// <see cref="CombatSwitch"/> in III (s134, to watch the Amiga orders screen): the test party leaves the Guild and the
    /// town for the wilderness and meets <paramref name="monsterId"/> in a normal fight.
    /// </summary>
    public static void StartP3Fight(GameSession session, int monsterId)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.LeaveGuild();
        session.LeaveTown();
        session.BeginCombat(monsterId, SurpriseFlags.Normal);
    }

    /// <summary>
    /// A developer switch to watch III's town-entry shares prompt (D3-071, s144): the test party leaves town with an XP
    /// pool of <see cref="EntryPayoutPool"/>, its second member dead, and walks back in.
    /// </summary>
    public const string P3EntryPayoutSwitch = "--p3-entry-payout";

    /// <summary>The pool <see cref="P3EntryPayoutSwitch"/> brings to town (not a multiple of the six shares: a remainder).</summary>
    public const int EntryPayoutPool = 1003;

    /// <summary>
    /// s169: a developer switch to watch III's gods' reward (<see cref="RewardScene"/>): <c>--p3-reward good</c> (Zeus) or
    /// <c>--p3-reward evil</c> (Pluto; the default) runs the campaign's reward at once, as after the last fight.
    /// </summary>
    public const string P3RewardSwitch = "--p3-reward";

    /// <summary>Whether the reward after <see cref="P3RewardSwitch"/> is the good god's, or null without the switch.</summary>
    public static bool? P3RewardGood(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var i = args.ToList().IndexOf(P3RewardSwitch);
        return i < 0 ? null : i + 1 < args.Count && args[i + 1] == "good";
    }

    /// <summary>III's reward scene for <see cref="P3RewardSwitch"/>: the test party leaves the town and is rewarded.</summary>
    public static void RunP3Reward(GameSession session, bool good)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.LeaveGuild();
        session.LeaveTown();
        ((Phantasie3Campaign)session.Campaign).Reward(session, good);
    }

    /// <summary>
    /// s169: a developer switch to watch III's party defeat (the Astral Plane): the test party leaves the town, every member
    /// DEAD at 0 HP, the defeat pending as after a lost fight.
    /// </summary>
    public const string P3AstralSwitch = "--p3-astral";

    /// <summary>III's party defeat for <see cref="P3AstralSwitch"/>.</summary>
    public static void P3Defeat(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.LeaveGuild();
        session.LeaveTown();
        foreach (var m in session.Party.Members)
        {
            m.HpCur = 0;
            m.Status = CharacterStatus.Dead;
        }
        session.CampaignAccess.PartyDefeatPending = true;
    }

    /// <summary>
    /// s169: a developer switch to watch a III plane's map: <c>--p3-plane R X Y</c> puts the test party, out of the town, on
    /// cell (X, Y) of map R (1 the Light plane, 2 the Dark plane, 4 the Netherworld).
    /// </summary>
    public const string P3PlaneSwitch = "--p3-plane";

    /// <summary>The (map, x, y) after <see cref="P3PlaneSwitch"/>, or null without the switch or its three numbers.</summary>
    public static (int Region, int X, int Y)? P3PlaneCell(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var i = args.ToList().IndexOf(P3PlaneSwitch);
        if (i < 0 || i + 3 >= args.Count)
            return null;
        return int.TryParse(args[i + 1], out var r) && int.TryParse(args[i + 2], out var x) && int.TryParse(args[i + 3], out var y)
            ? (r, x, y) : null;
    }

    /// <summary>
    /// III's plane for <see cref="P3PlaneSwitch"/>: the test party leaves the town and stands on the cell (the planes are
    /// 9 × 9, e.g. <c>1 6 7</c> the Castle of Light, <c>2 2 7</c> the Castle of Dark). A cell off that map leaves the party
    /// where the town's exit put it (false).
    /// </summary>
    public static bool PutOnP3Plane(GameSession session, (int Region, int X, int Y) cell)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.LeaveGuild();
        session.LeaveTown();
        var was = (session.Overworld.RegionId, session.Overworld.X, session.Overworld.Y);
        (session.Overworld.RegionId, session.Overworld.X, session.Overworld.Y) = cell;
        if (!session.CurrentOverworldRegion.InBounds(cell.X, cell.Y))
        {
            (session.Overworld.RegionId, session.Overworld.X, session.Overworld.Y) = was;
            return false;
        }
        session.CurrentOverworldRegion.RevealAround(cell.X, cell.Y);
        return true;
    }

    /// <summary>
    /// s169: a developer switch to read a III scroll: <c>--p3-scroll N</c> puts item N (121..140; 126 = Scroll IV, the map)
    /// first in the first member's pack, so the field's Use reads it.
    /// </summary>
    public const string P3ScrollSwitch = "--p3-scroll";

    /// <summary>The item after <see cref="P3ScrollSwitch"/>, or null without the switch or its number.</summary>
    public static int? P3ScrollItem(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var i = args.ToList().IndexOf(P3ScrollSwitch);
        return i >= 0 && i + 1 < args.Count && int.TryParse(args[i + 1], out var id) && id > 0 ? id : null;
    }

    /// <summary>III's scroll for <see cref="P3ScrollSwitch"/>: item <paramref name="itemId"/> first in member 0's pack.</summary>
    public static void GiveP3Scroll(GameSession session, int itemId)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Party.Members.Count > 0)
            session.Party.Members[0].InventoryItemIds.Insert(0, itemId);
    }

    /// <summary>III's town entry with a pool for <see cref="P3EntryPayoutSwitch"/>.</summary>
    public static void EnterP3TownWithPool(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var town = session.CurrentTownId ?? 1;
        session.LeaveGuild();
        session.LeaveTown();
        if (session.Party.Members.Count > 1 && session.Party.Members[1] is { } dead)
        {
            dead.Status = CharacterStatus.Dead;
            dead.HpCur = 0;
        }
        session.Sack.Experience = EntryPayoutPool;
        session.EnterTown(town);
    }

    /// <summary>
    /// A developer switch to watch II's ending (s128): with a test party in II, the last member is made DEAD (as in the
    /// s80 Hatari run: five presented, no title for the dead one) and Filmon's ceremony runs at once
    /// (<see cref="Phantasie2Campaign.ResolveEnding"/>).
    /// </summary>
    public const string P2VictorySwitch = "--p2-victory";

    /// <summary>II's ending for <see cref="P2VictorySwitch"/>: the last member DEAD, then the ceremony.</summary>
    public static void RunP2Ending(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Party.Members.Count > 1 && session.Party.Members[^1] is { } last)
        {
            last.Status = CharacterStatus.Dead;
            last.HpCur = 0;
        }
        Phantasie2Campaign.ResolveEnding(session);
    }

    /// <summary>
    /// A developer switch to watch a field Resurrection (s163): with a test party, the party leaves the Guild for the
    /// wilderness with its last member DEAD (<see cref="FieldWithDeadMember"/>).
    /// </summary>
    public const string FieldDeadSwitch = "--field-dead";

    /// <summary>The party in the wilderness, its last member DEAD at 0 HP, for <see cref="FieldDeadSwitch"/>.</summary>
    public static void FieldWithDeadMember(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DepartGuildToWilderness();
        if (session.Party.Members.Count > 1 && session.Party.Members[^1] is { } last)
        {
            last.Status = CharacterStatus.Dead;
            last.HpCur = 0;
        }
    }

    /// <summary>
    /// The game a test party starts in: <c>--test-game N</c> (a game the main menu can play,
    /// <see cref="MainMenuScreen.IsPlayable"/>), default I. A bad or unbuilt game is an error.
    /// </summary>
    public static GameId ParseGame(IReadOnlyList<string> args, out string? error)
    {
        error = null;
        var i = args.ToList().IndexOf(GameSwitch);
        if (i < 0)
            return GameId.P1;
        if (i + 1 < args.Count && int.TryParse(args[i + 1], out var n) && n is >= 1 and <= 4 &&
            MainMenuScreen.IsPlayable((GameId)n))
            return (GameId)n;
        error = $"{GameSwitch} needs the number of a built game.";
        return GameId.P1;
    }

    /// <summary>
    /// Read the command line: null = no test party; otherwise the level to start at (1 for a bare <see cref="Switch"/>).
    /// <see cref="LevelSwitch"/> takes <c>N</c> or <c>=N</c>; a missing or out-of-range level is an error.
    /// </summary>
    public static int? ParseArgs(IReadOnlyList<string> args, out string? error)
    {
        error = null;
        int? level = args.Contains(Switch) ? 1 : null;
        for (var i = 0; i < args.Count; i++)
        {
            string? value;
            if (args[i] == LevelSwitch)
                value = i + 1 < args.Count ? args[i + 1] : null;
            else if (args[i].StartsWith(LevelSwitch + "=", StringComparison.Ordinal))
                value = args[i][(LevelSwitch.Length + 1)..];
            else
                continue;
            if (!int.TryParse(value, out var n) || n < 1 || n > LevelUp.MaxLevel)
            {
                error = $"{LevelSwitch} needs a level from 1 to {LevelUp.MaxLevel}.";
                return null;
            }
            level = n;
        }
        return level;
    }

    private static readonly (string Name, Race Race, CharacterClass Class)[] Members =
    [
        // Names from the manual's GUILD MEMBERS example (p.4); races after its first-party hint.
        ("SWAT", Race.Dwarf, CharacterClass.Fighter),
        ("ARENTINO", Race.Human, CharacterClass.Ranger),
        ("LEFTY", Race.Halfling, CharacterClass.Thief),
        ("FRANCIS", Race.Human, CharacterClass.Monk),
        ("FOLNAR", Race.Human, CharacterClass.Priest),
        ("ZANDOR", Race.Elf, CharacterClass.Wizard),
    ];

    /// <summary>Highest item value factor (<c>items.json</c>) handed out at level 15; the budget grows linearly from 0 at level 1.</summary>
    public const int TopValueFactor = 250;

    public static void Bootstrap(GameSession session, IItemCatalog items, int level = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, LevelUp.MaxLevel);
        session.OpenGuild();
        foreach (var (name, race, cls) in Members)
        {
            var member = session.CreateAndEnroll(name, race, cls);
            session.TryAddToParty(member, out _);
            session.BankWithdraw(member, member.GoldInBank);
        }
        foreach (var member in session.Party.Members)
        {
            TrainTo(session, member, level);
            LearnAllowedSpells(member);
            Outfit(member, items, level);
            Heal(member);
        }
    }

    /// <summary>Every spell the levels so far opened ("can learn", 2) is learned, without the Guild fee or its aging.</summary>
    private static void LearnAllowedSpells(Character member)
    {
        for (var i = 0; i < member.SpellsKnown.Length; i++)
            if (member.SpellsKnown[i] == 2)
                member.SpellsKnown[i] = 1;
    }

    /// <summary>
    /// Per slot (weapon, armor, shield): the item with the most points the member can use whose value factor is within
    /// <c>TopValueFactor·(level − 1)/14</c> — level 1 keeps the starting gear, level 15 reaches the +7 swords and
    /// halberds. GOD items are quest rewards and never given. The new item takes the old one's carried slot.
    /// </summary>
    public static void Outfit(Character member, IItemCatalog items, int level)
    {
        var budget = TopValueFactor * (level - 1) / (LevelUp.MaxLevel - 1);
        int Best(Func<int, bool> slot, int current) =>
            Enumerable.Range(1, 100)
                .Where(id => slot(id) && ItemRules.IsUsable(id, member.Stats))
                .Select(id => items.Get(id))
                .Where(e => e is not null && e.ValueFactor <= budget && !e.Name.StartsWith("GOD", StringComparison.Ordinal))
                .Select(e => e!.Id)
                .Append(current)
                .MaxBy(id => (ItemRules.CombatGate(id).Points, id == current));
        member.WornWeapon = Swap(member, member.WornWeapon, Best(ItemRules.IsWeapon, member.WornWeapon));
        member.WornArmor = Swap(member, member.WornArmor, Best(ItemRules.IsArmor, member.WornArmor));
        member.WornShield = Swap(member, member.WornShield, Best(ItemRules.IsShield, member.WornShield));
        CombatGear.ApplyWornItemPoints(member);
    }

    private static int Swap(Character member, int old, int next)
    {
        if (next == old)
            return old;
        var slot = old == 0 ? -1 : member.InventoryItemIds.IndexOf(old);
        if (slot >= 0)
            member.InventoryItemIds[slot] = next;
        else
            CarriedItems.Put(member, next);
        return next;
    }

    private static void Heal(Character member)
    {
        member.Status = CharacterStatus.Ok;
        member.HpCur = member.HpMax;
        member.MagicMax = Math.Max(member.MagicMax, member.MpCeiling);
        member.MagicCur = member.MagicMax;
    }

    private static void TrainTo(GameSession session, Character member, int level)
    {
        var gold = session.Party.GoldCarried;
        while (member.Level < level)
        {
            member.Experience += session.TrainingXpShortfall(member);
            while (session.TrainingXpShortfall(member) > 0)
                member.Experience++; // float rounding at the threshold
            session.Party.GoldCarried = session.TrainingGoldCost(member);
            if (session.Train(member).Kind != TownTransactionKind.Ok)
                throw new InvalidOperationException($"{member.Name} could not train past level {member.Level}.");
        }
        session.Party.GoldCarried = gold;
    }
}
