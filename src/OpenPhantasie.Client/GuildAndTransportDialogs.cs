using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Town TRANSPORTATION destination picker (COM <c>0x16a2</c>, <c>endgame.md</c> §2.4) → <see cref="GameSession.ChooseTransportDestination"/>.
/// Opens by itself while <see cref="GameSession.TransportationPending"/>. Lists <see cref="GameSession.TransportDestinations"/>:
/// TWNS records 0..11 = Core town ids 1..12 without the current town (D-062); OLYMPIA (12) is printed blank (COM
/// <c>0x1887</c> → " "). Arrows + Return pick, Esc cancels (MP already spent). The DOS cursor <c>[0x1676]</c> is the absolute
/// town record 0..11, kept between casts (starts at 0 = PELNOR, reset when &gt; 11) [C]; so this remembers the highlighted
/// town id, not the row. The first-ever highlight is the first entry [C]. When the remembered town is the current one (not
/// listed, D-062) the highlight goes to the entry that took its place <c>[~]</c>.
/// </summary>
public sealed class TransportDialog : IModalDialog
{
    public static string Title => UiText.S("guild.transport_to_which_town");

    private bool _open;

    public bool IsOpen => _open;

    /// <summary>Highlighted row of <see cref="GameSession.TransportDestinations"/>.</summary>
    public int Cursor { get; private set; }

    /// <summary>The remembered town (Core id = DOS record + 1); 1 = PELNOR, as DOS starts at record 0.</summary>
    public int TownId { get; private set; } = 1;

    public string Help => UiText.S("guild.arrows_choose_enter_go_esc_cancel");

    /// <summary>s140: picks made (a town or the cancel): II ST's picker clicks after <c>form_do</c> either way
    /// (<c>0x25b4</c>, before the cancel check).</summary>
    public int Picks { get; private set; }

    /// <summary>s158: the last pick was a town, not the cancel (I Amiga plays 22 only for a town, h26 <c>+0xe0a</c>).</summary>
    public bool LastPickWasTown { get; private set; }

    /// <summary>Opens the picker when Core waits for a destination; true if it is open.</summary>
    public bool OpenIfPending(GameSession session)
    {
        if (!_open && session.TransportationPending && session.Mode == GameMode.Town)
        {
            _open = true;
            var towns = session.TransportDestinations;
            if (!session.Campaign.TransportTowns.Contains(TownId))
                TownId = session.Campaign.TransportTowns[0]; // DOS: cursor > 11 → 0
            // The remembered town's row; if it is the current town (not listed), the entry that took its place.
            var row = towns.Count(id => id < TownId);
            Cursor = row < towns.Count ? row : 0;
        }
        return _open;
    }

    /// <summary>The printed name of town <paramref name="townId"/>: the town name, blank where the campaign says (I: OLYMPIA, 12).</summary>
    public static string EntryName(GameDataRepository data, ICampaign campaign, int townId) =>
        campaign.IsUnnamedTransportTown(townId) ? " "
        : data.Towns.FirstOrDefault(t => t.Id == townId)?.Name ?? UiText.F("guild.town_x", townId);

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data) =>
        [Title, .. session.TransportDestinations.Select((id, i) => (i == Cursor ? "> " : "  ") + EntryName(data, session.Campaign, id))];

    /// <summary>
    /// s158: runs as a town is picked, before <see cref="GameSession.ChooseTransportDestination"/>: II ST's fade keeps the
    /// frame on screen then (the old town with this picker, <see cref="VirtualScreen.Freeze"/>).
    /// </summary>
    public Action? BeforePick { get; set; }

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        var towns = session.TransportDestinations;
        var count = towns.Count;
        foreach (var key in newKeys)
        {
            switch (key)
            {
                case Keys.Up or Keys.Left:
                    Cursor = (Cursor + count - 1) % count;
                    TownId = towns[Cursor];
                    break;
                case Keys.Down or Keys.Right:
                    Cursor = (Cursor + 1) % count;
                    TownId = towns[Cursor];
                    break;
                case Keys.Escape or Keys.Back:
                    _open = false;
                    Picks++;
                    LastPickWasTown = false;
                    session.ChooseTransportDestination(null);
                    return "";
                case Keys.Enter or Keys.Space:
                    _open = false;
                    Picks++;
                    LastPickWasTown = true;
                    TownId = towns[Cursor];
                    BeforePick?.Invoke(); // s158: the frame with the picker, before Core moves the party
                    session.ChooseTransportDestination(towns[Cursor]);
                    return session.LastMessage;
            }
        }
        return null;
    }
}

/// <summary>
/// Guild A)DD MEMBER (OV1 <c>0x2786</c>): roster members in no party (D-069: parked parties keep theirs), then — when
/// the party has six members and no elemental is loaded — the three elementals (roster rows 22–24). Arrows + Return pick. A member who lives in another
/// town (<see cref="GameSession.AddNeedsMoveConfirm"/>) first gets COM menu 9 (<see cref="GuildHall.AddMoveConfirmText"/>,
/// YES/NO, with that menu's own cursor memory in <see cref="PromptMenu"/>); only YES calls
/// <see cref="GameSession.TryAddToParty"/>.
/// </summary>
public sealed class GuildAddDialog : IModalDialog, IPagedDialog
{
    public ListPage? Page { get; private set; }

    private readonly PromptMenu _yesNo;
    private readonly GameDataRepository _data;
    private bool _open;
    private Character? _confirming;
    private int _scroll;

    public GuildAddDialog(PromptMenu yesNo, GameDataRepository data)
    {
        _yesNo = yesNo;
        _data = data;
    }

    public bool IsOpen => _open;

    public int Cursor { get; private set; }

    /// <summary>s156: the move question (menu 9) is up.</summary>
    public bool Confirming => _confirming is not null;

    /// <summary>s140: times the move question (COM menu 9; II ST alert 6, which beeps first) has been asked.</summary>
    public int MoveQuestions { get; private set; }

    /// <summary>
    /// s165: counts the roster list's answers (a row picked, or Esc / Backspace): I Amiga's list window h26 <c>+0x412</c>
    /// (Add's member question, h5 <c>+0xcae</c>) plays the tone after each gadget (<c>+0x614</c>), before the pick runs.
    /// </summary>
    public int ListTones { get; private set; }

    /// <summary>
    /// s162: III's move question shows in the Amiga's window 1 (<c>0xd24c</c> through <c>0xaae2</c>: OK / CANCEL, no
    /// highlight, Return = OK; <see cref="Game1.KeepWindowChoice"/>), set by the Client from
    /// <see cref="P3ConfirmQuestion.InWindow"/>. Off: the Yes / No panel.
    /// </summary>
    public bool AmigaWindow { get; set; }

    public string Help => _confirming is not null
        ? (AmigaWindow ? GameText.S("game.keep_window_ok_cancel") : UiText.S("guild.arrows_enter_or_y_yes_n"))
        : UiText.S("guild.arrows_choose_enter_add_esc_done");

    /// <summary>The YES/NO gate (COM menu 9) as a prompt, so it keeps that menu's own cursor memory.</summary>
    public static SessionPrompt MoveConfirmPrompt { get; } =
        new(SessionPromptKind.None, GuildHall.AddMoveConfirmText, [SessionPrompts.Yes, SessionPrompts.No]) { MenuId = 9 };

    /// <summary>
    /// The gate for <paramref name="game"/>: III asks on its two-item menu root <c>0x75a3</c> (ovl2 <c>0x3b75</c> through
    /// root <c>0x1820</c> with highlight 1), so "Yes" is lit every time and nothing is remembered; Esc is No in both.
    /// </summary>
    public static SessionPrompt MoveConfirmPromptFor(GameId game) => game == GameId.P3 ? P3MoveConfirmPrompt : MoveConfirmPrompt;

    private static SessionPrompt? _p3MoveConfirmPrompt;

    /// <summary>
    /// s162: III's question is its own line, S445 "It will take this character two\rmonths to make the journey here."
    /// (DOS ovl2 <c>0x3b75</c> and the Amiga's <c>0xd24c</c> print the same text), split at the "\r" as both break it.
    /// Rebuilt when the string table's text changes (the table is the game's, loaded after this type).
    /// </summary>
    private static SessionPrompt P3MoveConfirmPrompt
    {
        get
        {
            IReadOnlyList<string> lines = GameText.S("guild.move_journey_question").Split('\r', '\n');
            if (_p3MoveConfirmPrompt is not { } p || !p.Lines.SequenceEqual(lines))
                _p3MoveConfirmPrompt = new(SessionPromptKind.None, lines, [SessionPrompts.Yes, SessionPrompts.No])
                    { Remembers = false, InitialChoice = 1, EscapeChoice = 2 };
            return _p3MoveConfirmPrompt;
        }
    }

    /// <summary>The rows offered: roster members first (null template), then elementals (template index).</summary>
    public static IReadOnlyList<(Character? Member, int Elemental, string Label)> Rows(GameSession session, GameDataRepository data)
    {
        var rows = session.Roster.Occupied.Where(c => !session.Party.Members.Contains(c) && !session.IsInOtherParty(c)) // D-069
            .Select(c => ((Character?)c, -1, UiText.F("guild.x_x_l_x", c.Name, UiText.Name(c.Class).ToUpperInvariant(), c.Level)))
            .ToList();
        if (session.Party.Members.Count >= session.GuildElementalRules.MinMembers && session.Party.Elemental is null)
            rows.AddRange(data.Elementals.Select((e, i) => ((Character?)null, i, e.Name)));
        return rows;
    }

    public string? Open(GameSession session)
    {
        var data = _data;
        if (session.Mode is not (GameMode.Guild or GameMode.Title))
            return UiText.S("guild.only_in_the_guild");
        // s158: an empty roster has its own line (III: S408 `guild.roster_empty`, DOS guild_menu item 2 /
        // Amiga 0xb274, both tested before the list opens); other empty lists keep the remaster's line (III lists the
        // party's own members and refuses them on the pick, FORMAT.md 8.4).
        if (!session.Roster.Occupied.Any())
            return UiText.S("guild.roster_empty");
        if (Rows(session, data).Count == 0)
            return UiText.S("guild.no_one_to_add");
        _open = true;
        _confirming = null;
        Cursor = 0;
        _scroll = 0;
        return null;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data)
    {
        Page = null;
        if (_confirming is not null)
        {
            var question = MoveConfirmPromptFor(session.Game);
            var c = _yesNo.CursorFor(question);
            return [.. question.Lines, UiText.F("guild.x_yes_x_no", (c == 0 ? ">" : " "), (c == 1 ? ">" : " "))];
        }
        var rows = Rows(session, data);
        Page = ListPage.Of(1, 10, _scroll, rows.Count);
        return [UiText.S("guild.add_who"), .. rows.Skip(_scroll).Take(10).Select((r, i) => (i + _scroll == Cursor ? "> " : "  ") + r.Label)];
    }

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        var data = _data;
        foreach (var key in newKeys)
        {
            if (_confirming is { } who)
            {
                if (key == Keys.Escape)
                {
                    _confirming = null;
                    return "";
                }
                var answer = AmigaWindow ? Game1.KeepWindowChoice(key) : _yesNo.HandleKey(MoveConfirmPromptFor(session.Game), key);
                if (answer is null)
                    continue;
                _confirming = null;
                _yesNo.Sync(null);
                return answer == 1 ? Add(session, who, data) : "";
            }

            var rows = Rows(session, data);
            switch (key)
            {
                case Keys.Escape or Keys.Back:
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
                    ListTones++;
                    var row = rows[Math.Clamp(Cursor, 0, rows.Count - 1)];
                    if (row.Member is null)
                    {
                        var ok = session.TryAddElemental(row.Elemental, out var err);
                        return AfterAdd(session, data, ok ? session.LastMessage : err);
                    }
                    if (session.AddNeedsMoveConfirm(row.Member))
                    {
                        _confirming = row.Member;
                        MoveQuestions++;
                        session.AskConfirmQuestion(); // s141: III Amiga's 36 before window 1's question (0xaae2)
                        _yesNo.Sync(MoveConfirmPromptFor(session.Game)); // I: menu 9's last answer; III: "Yes"
                        return null;
                    }
                    return Add(session, row.Member, data);
            }
            _scroll = Math.Clamp(_scroll, Math.Max(0, Cursor - 9), Cursor);
        }
        return null;
    }

    private string Add(GameSession session, Character member, GameDataRepository data)
    {
        var ok = session.TryAddToParty(member, out var error);
        return AfterAdd(session, data, ok ? session.LastMessage : error);
    }

    private string AfterAdd(GameSession session, GameDataRepository data, string message)
    {
        var rows = Rows(session, data);
        if (rows.Count == 0)
            _open = false;
        Cursor = Math.Clamp(Cursor, 0, Math.Max(0, rows.Count - 1));
        return message;
    }
}

/// <summary>
/// Guild D)ROP MEMBER (OV1 <c>0x225c</c>, `guild.whom_do_you_want_to_drop`): a digit sends that member back to the roster through
/// <see cref="GameSession.TryDropFromParty"/> (<c>@218</c> := 0; I: every Drop also clears the elemental slot, OV1 <c>0x22d1</c>; III keeps it while a character is
/// left, <see cref="ElementalRules.DropKeeps"/>; II keeps it even alone; the save's <c>elemental-drop</c> can pick I's or III's in
/// any game). s172: in II and III (<see cref="ElementalRules.DropsElemental"/>) the loaded elemental is the row after the
/// characters, and its digit unloads it (ST <c>0xd57c</c>'s seven-record list; DOS III ovl2 <c>0x3c92</c>).
/// </summary>
public sealed class GuildDropDialog : IModalDialog
{
    public static string Title => UiText.S("guild.whom_do_you_want_to_drop");

    public bool IsOpen { get; private set; }

    public string Help => UiText.S("guild.1_6_drop_esc_done");

    /// <summary>s165: counts the chooser left by Esc / Backspace (I Amiga's list tone, h23 <c>+0x22a</c> from h5 <c>+0xdc2</c>).</summary>
    public int ChooserCancels { get; private set; }

    public string? Open(GameSession session)
    {
        if (session.Mode is not (GameMode.Guild or GameMode.Title))
            return UiText.S("guild.only_in_the_guild");
        if (session.Party.Members.Count == 0 && ListedElemental(session) is null)
            return UiText.S("guild.no_one_to_drop");
        IsOpen = true;
        return null;
    }

    /// <summary>The loaded elemental when this game's Drop list holds it (<see cref="ElementalRules.DropsElemental"/>), else null.</summary>
    public static Character? ListedElemental(GameSession session) =>
        session.GuildElementalRules.DropsElemental ? session.Party.Elemental : null;

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data) =>
    [
        Title, .. session.Party.Members.Select((m, i) => $"{i + 1} {m.Name}"),
        .. ListedElemental(session) is { } e ? [$"{session.Party.Members.Count + 1} {e.Name}"] : Array.Empty<string>(),
    ];

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            if (key is Keys.Escape or Keys.Back)
            {
                ChooserCancels++;
                IsOpen = false;
                return "";
            }
            var d = DialogKeys.Digit(key);
            if (d >= 1 && d <= session.Party.Members.Count)
            {
                var member = session.Party.Members[d - 1];
                var ok = session.TryDropFromParty(member, out var error);
                if (session.Party.Members.Count == 0 && ListedElemental(session) is null)
                    IsOpen = false;
                return ok ? UiText.F("guild.x_leaves_the_party", member.Name) : error;
            }
            if (d == session.Party.Members.Count + 1 && ListedElemental(session) is { } elemental)
            {
                var ok = session.TryDropElemental(out var error);
                if (session.Party.Members.Count == 0)
                    IsOpen = false;
                return ok ? UiText.F("guild.x_leaves_the_party", elemental.Name) : error;
            }
        }
        return null;
    }
}

/// <summary>
/// Guild party options S)PELLS and T)RAINING (manual p.4: "PARTY MEMBERS options can only be used by characters that have
/// been added to a party"): a digit picks the party member — DOS `guild.who_is_going_to_be_trained` (OV1 DS <c>0x5c22</c>) /
/// "WHO WILL LEARN?" — then <see cref="OnPick"/> runs (open the spell list). Training (<see cref="OpenTraining"/>) first shows
/// DOS's "It will cost / N gold" box with A)GREE / N)OT (s65 DOSBox; manual: "you will be told … how much the training will
/// cost"), and like DOS returns to the WHO list after each result until Esc.
/// </summary>
public sealed class GuildPartyPickDialog : IModalDialog
{
    public static string TrainTitle => UiText.S("guild.who_is_going_to_be_trained");
    public static string LearnTitle => UiText.S("guild.who_will_learn");

    private string _title = "";
    private Func<GameSession, Character, string>? _onPick;
    private bool _training;
    private Character? _confirming;

    /// <summary>III: the member whose three skill picks are being asked (after AGREE), and the picks so far.</summary>
    private Character? _skillMember;
    private readonly List<P3Skill?> _skillPicks = [];
    private const int SkillPicks = 3;

    public bool IsOpen => _onPick is not null || _training;

    /// <summary>s156: the member whose training cost waits for AGREE / NOT, or null.</summary>
    public Character? ConfirmingTraining => _confirming;

    /// <summary>
    /// s165: counts the WHO list left by Esc / Backspace (I Amiga's list tone, h23 <c>+0x22a</c> from h5 <c>+0x1116</c>
    /// `guild.who_will_learn` / <c>+0x13c2</c> `guild.who_is_going_to_be_trained`).
    /// </summary>
    public int ChooserCancels { get; private set; }

    public string Help => _skillMember is not null ? UiText.S("p3train.help")
        : _confirming is not null ? UiText.S("guild.a_agree_n_not") : UiText.S("guild.1_6_choose_esc_done");

    public string? Open(GameSession session, string title, Func<GameSession, Character, string> onPick)
    {
        if (session.Mode != GameMode.Guild)
            return UiText.S("guild.only_in_the_guild");
        if (session.Party.Members.Count == 0)
            return UiText.S("guild.add_members_to_a_party_first");
        _title = title;
        _onPick = onPick;
        _training = false;
        _confirming = null;
        return null;
    }

    /// <summary>T)RAINING: pick → cost box (AGREE trains, NOT takes nothing) → back to the WHO list.</summary>
    public string? OpenTraining(GameSession session)
    {
        var refused = Open(session, TrainTitle, (_, _) => "");
        if (refused is not null)
            return refused;
        _onPick = null;
        _training = true;
        return null;
    }

    public IReadOnlyList<string> Lines(GameSession session, GameDataRepository data)
    {
        if (_skillMember is { } sm)
            return
            [
                UiText.F("p3train.skills_to_train_in_x", SkillPicks - _skillPicks.Count),
                UiText.S("p3train.header"),
                .. session.TrainingSkillRows(sm, _skillPicks).Select((r, i) =>
                    $"{i + 1} {UiText.Name(r.Skill),-10}{r.Now,4}{r.After,6}"),
            ];
        if (_confirming is { } m && session.CurrentTown is not null)
            return [UiText.S("guild.it_will_cost"), UiText.F("guild.x_gold", session.TrainingGoldCost(m)), UiText.S("guild.a_agree"), UiText.S("guild.n_not")];
        return [_title, .. session.Party.Members.Select((p, i) => $"{i + 1} {p.Name}")];
    }

    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session)
    {
        foreach (var key in newKeys)
        {
            if (_skillMember is { } trainee)
            {
                // ovl2 0x16d0 three times: 1–9 picks a skill; Esc uses the pick up with no change [~].
                var k = DialogKeys.Digit(key);
                var skills = P3Training.ScreenOrder; // the rows' order (TrainingSkillRows)
                if (key is Keys.Escape or Keys.Back)
                    _skillPicks.Add(null);
                else if (k >= 1 && k <= skills.Count)
                    _skillPicks.Add(skills[k - 1]);
                else
                    continue;
                if (_skillPicks.Count < SkillPicks)
                    return "";
                _skillMember = null;
                return session.Train(trainee, [.. _skillPicks]).Message;
            }
            if (_confirming is { } member)
            {
                if (key is Keys.A or Keys.Enter)
                {
                    _confirming = null;
                    if (session.TrainingAsksSkills && session.TrainingWouldSucceed(member))
                    {
                        _skillMember = member; // III: `p3train.skills_to_train_in_x` before the level-up is applied
                        _skillPicks.Clear();
                        return "";
                    }
                    return session.Train(member).Message;
                }
                if (key is Keys.N or Keys.Escape or Keys.Back)
                {
                    _confirming = null; // DOS NOT: back to the list, nothing taken
                    return "";
                }
                continue;
            }
            if (key is Keys.Escape or Keys.Back)
            {
                ChooserCancels++;
                _onPick = null;
                _training = false;
                return "";
            }
            var d = DialogKeys.Digit(key);
            if (d < 1 || d > session.Party.Members.Count)
                continue;
            var picked = session.Party.Members[d - 1];
            if (_training)
            {
                if (!session.CanTrain(picked, out _))
                    return session.Train(picked).Message; // refusal text (+ cost, D-050); nothing taken
                _confirming = picked;
                return "";
            }
            if (_onPick is { } pick)
            {
                _onPick = null;
                return pick(session, picked);
            }
        }
        return null;
    }
}
