namespace OpenPhantasie.Core;

/// <summary>Which modal question the front end has to show (DOS menus are modal: nothing else happens until answered).</summary>
public enum SessionPromptKind
{
    None,

    /// <summary><c>PartyDefeatPending</c>: `core.defeat.your_party_has_been_defeated` → <see cref="GameSession.ResolvePartyDefeat"/>.</summary>
    PartyDefeat,

    /// <summary>The Great Demon's judgement (the defeat log incl. each fate), shown until acknowledged.</summary>
    DefeatOutcome,

    /// <summary>
    /// Phantasie II's 5th defeat of the run only (<see cref="PartyDefeatResult.DemonsRanOut"/>): "YOU HAVE ARRIVED AT THE
    /// ASTRAL PLANE" while the open door shows the "Sorry, we've run out of Demons" sign, before <see cref="DefeatOutcome"/>.
    /// </summary>
    DefeatDemonSign,

    /// <summary>
    /// A reward scene after a final fight (<see cref="GameSession.PendingRewardScene"/>: III's gods' reward), shown until
    /// acknowledged (<see cref="GameSession.AcknowledgeRewardScene"/>).
    /// </summary>
    RewardScene,

    /// <summary>
    /// Phantasie I's victory screen (<see cref="GameSession.PendingVictoryScene"/>): Zeus' speech, then a key (DOS OV1
    /// <c>0x4d5</c>) → <see cref="GameSession.AcknowledgeVictoryScene"/>.
    /// </summary>
    VictoryScene,

    /// <summary>A MESS menu on a scripted-event cell (OV2 <c>0x2a95</c>) → <see cref="GameSession.AnswerDungeonEvent"/>.</summary>
    DungeonEvent,

    /// <summary>
    /// III's event-text page waiting for a key ("Press a key", <see cref="GameSession.DungeonPage"/>) →
    /// <see cref="GameSession.AcknowledgeDungeonPage"/>. Any key answers it (Amiga <c>0x1e8c4(1)</c>), Esc included.
    /// </summary>
    DungeonPage,

    /// <summary>
    /// "%s SPOTS A TRAP / D)ISARM OR L)EAVE" (manual p.10, D-052; DOS OV2 <c>0x31f5</c> asks "DISARM IT ?" YES/NO, menu
    /// <c>0x1f</c>) → 1 = DISARM, 2 = LEAVE = <see cref="GameSession.AnswerDungeonTrap"/>(null).
    /// </summary>
    DungeonTrap,

    /// <summary>`core.prompt.who_will_disarm` (<c>0x5a0a</c> mode 1: OK members only — the manual names no limit), asked after DISARM.</summary>
    DungeonTrapWho,

    /// <summary>"%s `core.dungeon.x_hears_monsters_go_back`" → <see cref="GameSession.AnswerDungeonLock"/>.</summary>
    DungeonLock,

    /// <summary>`core.prompt.an_exit_take_it` (OV2 <c>0x2d46</c>, menu <c>0x1d</c>).</summary>
    DungeonExitTakeIt,

    /// <summary>`core.prompt.save_dungeon_map` (asked only after a YES to TAKE IT?).</summary>
    DungeonExitSaveMap,

    /// <summary>"YOU FIND %s. KEEP IT?" (<see cref="GameSession.AnswerTreasure"/>).</summary>
    Treasure,

    /// <summary>A day-branch menu of the random wilderness encounter (menus <c>0xf</c>/<c>0x10</c>/<c>0x11</c>) →
    /// <see cref="GameSession.AnswerWildernessPrompt"/>.</summary>
    WildernessEncounter,

    /// <summary>"A TOWN. ENTER?" / `core.overworld.a_dungeon_enter` (manual p.12) → <see cref="GameSession.AnswerOverworldEntry"/>.</summary>
    OverworldEntry,

    /// <summary>The wilderness inn on tile 41 (menu <c>0x16</c>) → <see cref="GameSession.StayAtWildernessInn"/>.</summary>
    WildernessInn,

    /// <summary>
    /// Bank D)EPOSIT (manual p.10, D-049): "how many shares each party member gets (1, 2, or 3)", one member at a time
    /// (DOS "Shares for:", a DEAD member 0..3) → <see cref="GameSession.BankDeposit"/> after the last one.
    /// </summary>
    BankShares,

    /// <summary>
    /// III's town entry (D3-071, DOS ovl2 <c>0x0000</c>(1), s143): "`core.prompt.p3_distributing_xp` `core.prompt.p3_distributing_xp`" / "Shares for
    /// NAME :", one character at a time (a dead one 0..3, any other 1..3; s144) → <see cref="GameSession.ShareOutExperience"/>
    /// after the last one.
    /// </summary>
    ExperienceShares,
}

/// <summary>One modal question: its text lines and its answers (1-based in <see cref="SessionPrompts.Answer"/>).</summary>
public sealed record SessionPrompt(SessionPromptKind Kind, IReadOnlyList<string> Lines, IReadOnlyList<string> Options)
{
    /// <summary>
    /// The DOS COM menu id (null: none known). DOS keeps one cursor per menu id (message-and-text.md §Menus, s58), so a
    /// front end keys its cursor memory by it.
    /// </summary>
    public int? MenuId { get; init; }

    /// <summary>
    /// False = the menu opens on its first option every time, with no cursor memory: III's fight find (root <c>0xf2eb</c>
    /// passes highlight 1 = "Yes" to its two-item menu <c>0x75a3</c> on every call, s98 [C]).
    /// </summary>
    public bool Remembers { get; init; } = true;

    /// <summary>
    /// The answer Esc gives (1-based), or null: Esc is not an answer (the front end's game menu key). III's keep questions
    /// use the two-item menu root <c>0x75a3</c>, which returns 0 = "No" for Esc (fight find root <c>0xf2eb</c>, dungeon
    /// op 6 through root <c>0x1820</c>; s98 / s102 static [C]).
    /// </summary>
    public int? EscapeChoice { get; init; }

    /// <summary>
    /// The answer lit when the prompt opens (1-based), for a prompt with no memory (<see cref="Remembers"/> = false); null
    /// = the first. III's two-item menu root <c>0x75a3</c> takes it from each caller (<c>[bp+0x16]</c>): "No" (2) for the
    /// exit pair, `core.p3.load_saved_dungeon` and the door's "Continue?", "Yes" (1) for the rest (s108 static [C]).
    /// </summary>
    public int? InitialChoice { get; init; }

    /// <summary>A YES/NO question (answer 1 = YES, 2 = NO).</summary>
    /// <summary>A share-count question (the Bank's or III's XP): each option is the share count itself.</summary>
    public bool IsShares => Kind is SessionPromptKind.BankShares or SessionPromptKind.ExperienceShares;

    public bool IsYesNo => Options.Count == 2 && Options[0] == SessionPrompts.Yes && Options[1] == SessionPrompts.No;

    /// <summary>
    /// A letter menu ("D)ISARM OR L)EAVE"): every option is "X)…", answered with that first letter (manual p.10: "hit …
    /// the first letter before the )"). Null when the options are not all of that form or two share a letter.
    /// </summary>
    public string? Hotkeys
    {
        get
        {
            if (Options.Count < 2 || Options.Any(o => o.Length < 3 || o[1] != ')' || !char.IsLetter(o[0])))
                return null;
            var keys = new string(Options.Select(o => char.ToUpperInvariant(o[0])).ToArray());
            return keys.Distinct().Count() == keys.Length ? keys : null;
        }
    }
}

/// <summary>
/// Front-end glue (P2, s39): turns the session's pending questions into one modal prompt at a time and sends the answer
/// back through the existing <see cref="GameSession"/> API. Holds only what Core does not: the YES to `core.prompt.an_exit_take_it`
/// while "SAVE THE DUNGEON MAP?" is asked (Core takes both answers in one call), the wilderness inn offer (Core only
/// notes it), and the defeat outcome until it is acknowledged. No rules live here.
/// </summary>
/// <remarks>
/// Priority: defeat first (<see cref="GameSession.ResolvePartyDefeat"/> clears the treasure queue, and a script can offer
/// an item and then wipe the party), then the event menu, the wilderness encounter menu, the trap / lock questions, the exit pair, KEEP IT?, ENTER?, the inn.
/// </remarks>
public sealed class SessionPrompts
{
    public static string Yes => GameText.S("core.prompt.yes");
    public static string No => GameText.S("core.prompt.no");
    public static string Continue => GameText.S("core.prompt.continue");
    public static string ExitTakeItText => GameText.S("core.prompt.an_exit_take_it");
    public static string ExitSaveMapText => GameText.S("core.prompt.save_dungeon_map");
    public static string WhoWillDisarmText => GameText.S("core.prompt.who_will_disarm");
    public static string Disarm => GameText.S("core.prompt.disarm"); // manual p.10 (D-052)
    public static string Leave => GameText.S("core.prompt.leave_trap");
    public static string DefeatText => GameText.S("core.defeat.your_party_has_been_defeated"); // OV1 0x5b66

    /// <summary>Menu <c>0x16</c>: COM menu labels <c>0x59</c>/<c>0x5a</c>/<c>0x5b</c> = "AN INN. ENTER?" / YES / NO (s41 static decode of the
    /// menu table, checked against the live 0x1c/0x1d/0x1e boxes); same text as <c>GameSession</c> notes.</summary>
    public static string WildernessInnText => GameText.S("core.prompt.an_inn_enter");

    public static string SharesForText => GameText.S("core.prompt.shares_for"); // DOS 0x42cb (s35 capture)

    /// <summary>"A TOWN. ENTER?": its own COM label (<c>0x60d0</c>), read as menu <c>0xe</c> from the label order <c>[~]</c>.</summary>
    public const int TownEntryMenuId = 0xe;

    private readonly GameSession _session;
    private bool _exitTaken;
    private bool _trapDisarming;
    private (int Region, int X, int Y)? _innOfferedAt;
    private PartyDefeatResult? _defeatToShow;
    private bool _demonSignShown;
    private List<int>? _shares;
    private List<int>? _xpShares;

    public SessionPrompts(GameSession session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

    /// <summary>
    /// The resolved defeat whose sign / outcome prompts are showing (null otherwise): the front end draws each member's
    /// fate from it (II's defeat scene, FORMAT.md §10.3).
    /// </summary>
    public PartyDefeatResult? ShownDefeat => _defeatToShow;

    /// <summary>True while any prompt waits; the front end must not move, turn or fight until it is answered.</summary>
    public bool IsOpen => Current is not null;

    /// <summary>The prompt to show now, or null.</summary>
    public SessionPrompt? Current
    {
        get
        {
            var s = _session;
            if (!s.DungeonExitPending)
                _exitTaken = false; // the exit pair was closed elsewhere (a load, a defeat)
            if (s.DungeonTrapPrompt is null)
                _trapDisarming = false;
            if (s.PartyDefeatPending)
                return new(SessionPromptKind.PartyDefeat, [DefeatText], [Continue]);
            if (_defeatToShow is { DemonsRanOut: true } sign && !_demonSignShown)
                return new(SessionPromptKind.DefeatDemonSign,
                    sign.Log.Where(l => l == GameText.S("core.defeat.arrived_at_astral_plane")).ToList(), [Continue]);
            if (_defeatToShow is { } d)
                return new(SessionPromptKind.DefeatOutcome, d.Log.Where(l => l != DefeatText).ToList(), [Continue]);
            if (s.Mode == GameMode.Combat)
                return null; // the combat presenter owns the screen
            if (_shares is not null && (s.Mode != GameMode.Town || _shares.Count >= s.Party.Members.Count))
                _shares = null; // left town / party changed
            if (_shares is not null)
            {
                var m = s.Party.Members[_shares.Count];
                return new(SessionPromptKind.BankShares,
                    [GameText.F("core.prompt.you_have_x_gold", s.Party.GoldCarried), $"{SharesForText} {m.Name}"],
                    m.Status == CharacterStatus.Dead ? ["0", "1", "2", "3"] : ["1", "2", "3"]);
            }
            if (s.DungeonEventPrompt is { } ev)
                return s.Game == GameId.P3 && Phantasie3Campaign.YesNoHighlight(ev) is { } lit // the trap / door questions
                    ? P3YesNo(SessionPromptKind.DungeonEvent, ev.Message.Lines, lit)
                    : new(SessionPromptKind.DungeonEvent, ev.Message.Lines, ev.Message.Options)
                    {
                        EscapeChoice = s.Game == GameId.P3 && Phantasie3Campaign.AsksDisarmer(ev) ? ev.Message.Options.Count + 1 : null,
                    };
            if (s.DungeonPage is { WaitsForKey: true } page)
                return new(SessionPromptKind.DungeonPage, page.Lines, [Continue]) { EscapeChoice = 1 };
            if (s.WildernessPrompt is { } p3Wild && s.Game == GameId.P3)
                return P3YesNo(SessionPromptKind.WildernessEncounter, [p3Wild.Title], 1); // root 0xe294: `core.p3.x_spots_sleeping_monsters` / "Fight?"
            if (s.WildernessPrompt is { } wp)
                return new(SessionPromptKind.WildernessEncounter, [wp.Title], WildernessOptions(wp.Menu).Select(o => o.Label).ToList())
                    { MenuId = WildernessMenuId(wp.Menu) };
            if (s.DungeonTrapPrompt is { } trap)
                return _trapDisarming
                    ? new(SessionPromptKind.DungeonTrapWho, [WhoWillDisarmText],
                        trap.Disarmers.Select(i => s.Party.Members[i].Name).ToList())
                    : new(SessionPromptKind.DungeonTrap, [trap.Message], [Disarm, Leave]);
            if (s.DungeonLockPrompt is { } heard)
                return new(SessionPromptKind.DungeonLock, [heard.Message], [Yes, No]) { MenuId = 0x1f };
            if (s.DungeonExitPending && s.Game == GameId.P3) // ovl4 0x892 / 0x8cf through 0x1b81: "No" lit
                return _exitTaken
                    ? P3YesNo(SessionPromptKind.DungeonExitSaveMap, [ExitSaveMapText], 2)
                    : P3YesNo(SessionPromptKind.DungeonExitTakeIt, [ExitTakeItText], 2);
            if (s.DungeonExitPending)
                return _exitTaken
                    ? new(SessionPromptKind.DungeonExitSaveMap, [ExitSaveMapText], [Yes, No]) { MenuId = 0x1e }
                    : new(SessionPromptKind.DungeonExitTakeIt, [ExitTakeItText], [Yes, No]) { MenuId = 0x1d };
            if (s.TreasurePromptLines is { } t)
                return s.Game == GameId.P3 // fight find (0xf2eb) and dungeon "Keep …?" (ovl4 op 6 → 0x1820): always on Yes, Esc = No
                    ? new(SessionPromptKind.Treasure, t, [Yes, No]) { Remembers = false, EscapeChoice = 2 }
                    : new(SessionPromptKind.Treasure, t, [Yes, No]) { MenuId = 0x1b };
            // DOS's dungeon fight pays the fight's rewards (the keep questions) before the reward scene (ovl4 0x1d30).
            if (s.PendingRewardScene is { } reward)
                return new(SessionPromptKind.RewardScene, reward.Lines, [Continue]);
            if (s.PendingVictoryScene is { } victory)
                return new(SessionPromptKind.VictoryScene, victory.Lines, [Continue]);
            if (!s.TownXpSharesDue)
                _xpShares = null;
            else
            {
                _xpShares ??= new List<int>();
                var takers = s.TownEntryShareTakers;
                if (_xpShares.Count >= takers.Count)
                    _xpShares.Clear(); // the party changed under the prompts: ask again
                var m = takers[_xpShares.Count];
                var options = P3TownEntryPayout.ShareOptions(m); // s144: every character, a dead one 0..3
                return new(SessionPromptKind.ExperienceShares,
                    [GameText.S("core.prompt.p3_distributing_xp"), GameText.F("core.prompt.p3_shares_for_x", m.Name)],
                    options)
                {
                    Remembers = false, // DOS pre-fills the member's stored shares (+0x54), a dead one's 0
                    InitialChoice = options.ToList().IndexOf(P3TownEntryPayout.DefaultShares(m).ToString(
                        System.Globalization.CultureInfo.InvariantCulture)) + 1,
                };
            }
            if (s.EntryPrompt is { } p3Entry && s.Mode == GameMode.Overworld && s.Game == GameId.P3)
                return P3YesNo(SessionPromptKind.OverworldEntry, [p3Entry.Text], p3Entry.AsksLoad ? 2 : 1); // ovl3 0x830 / ovl4 0x704
            if (s.EntryPrompt is { } entry && s.Mode == GameMode.Overworld)
                return new(SessionPromptKind.OverworldEntry, [entry.Text], [Yes, No])
                    { MenuId = entry.Kind == OverworldEntryKind.Town ? TownEntryMenuId : 0x1c };
            if (InnOfferStillValid() && s.Game == GameId.P3) // an inn is a place: "<name> Will you enter?" (ovl3 0x830)
                return P3YesNo(SessionPromptKind.WildernessInn, [P3InnText(s)], 1);
            if (InnOfferStillValid())
                return new(SessionPromptKind.WildernessInn, [WildernessInnText], [Yes, No]) { MenuId = 0x16 };
            return null;
        }
    }

    /// <summary>Feed every overworld step here: a step onto the inn tile offers the inn; any other step withdraws it.</summary>
    public void NoteOverworldMove(OverworldMoveResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _innOfferedAt = result.Kind == OverworldMoveKind.WildernessInn
            ? (_session.Overworld.RegionId, _session.Overworld.X, _session.Overworld.Y)
            : null;
    }

    /// <summary>
    /// Bank D)EPOSIT: start asking the shares (manual p.10, D-049). False — nothing asked — outside a town, with no
    /// party, or with no gold carried (DOS beeps).
    /// </summary>
    public bool BeginBankDeposit()
    {
        var s = _session;
        if (s.Mode != GameMode.Town || s.Party.Members.Count == 0 || s.Party.GoldCarried <= 0)
            return false;
        _shares = new List<int>();
        return true;
    }

    /// <summary>
    /// Leave Bank D)EPOSIT before the last member's shares (III Amiga window's CANCEL, s157): nothing is deposited (the
    /// gold moves only after the last answer). False when no deposit was being asked.
    /// </summary>
    public bool CancelBankDeposit()
    {
        if (_shares is null)
            return false;
        _shares = null;
        return true;
    }

    /// <summary>Forget front-end-only state (after a load): the exit pair restarts at TAKE IT?, no inn offer.</summary>
    public void Reset()
    {
        _exitTaken = false;
        _trapDisarming = false;
        _innOfferedAt = null;
        _defeatToShow = null;
        _demonSignShown = false;
        _shares = null;
        _xpShares = null;
    }

    /// <summary>Answer a YES/NO prompt, or acknowledge a CONTINUE one (either value).</summary>
    public SessionPromptKind Answer(bool yes) => Answer(yes ? 1 : 2);

    /// <summary>
    /// Answer the current prompt with a 1-based <paramref name="choice"/> (YES = 1, NO = 2; CONTINUE takes any value).
    /// Returns the kind that was answered. Combat may have started (a script ending in a fight): check <c>Mode</c>.
    /// </summary>
    public SessionPromptKind Answer(int choice)
    {
        var prompt = Current ?? throw new InvalidOperationException("No prompt is waiting for an answer.");
        var s = _session;
        switch (prompt.Kind)
        {
            case SessionPromptKind.PartyDefeat:
                _defeatToShow = s.ResolvePartyDefeat();
                _demonSignShown = false;
                break;
            case SessionPromptKind.DefeatDemonSign:
                _demonSignShown = true;
                break;
            case SessionPromptKind.DefeatOutcome:
                _defeatToShow = null;
                break;
            case SessionPromptKind.RewardScene:
                s.AcknowledgeRewardScene();
                break;
            case SessionPromptKind.VictoryScene:
                s.AcknowledgeVictoryScene();
                break;
            case SessionPromptKind.DungeonEvent:
                s.AnswerDungeonEvent(choice);
                break;
            case SessionPromptKind.DungeonPage:
                s.AcknowledgeDungeonPage();
                break;
            case SessionPromptKind.DungeonTrap:
                RequireYesNo(choice); // 1 = DISARM, 2 = LEAVE
                if (choice == 1 && s.DungeonTrapPrompt!.Disarmers.Count > 0)
                    _trapDisarming = true;
                else
                    s.AnswerDungeonTrap(null); // LEAVE, or no one can be chosen: step back, trap stays
                break;
            case SessionPromptKind.DungeonTrapWho:
            {
                var disarmers = s.DungeonTrapPrompt!.Disarmers;
                if (choice < 1 || choice > disarmers.Count)
                    throw new ArgumentOutOfRangeException(nameof(choice), $"1..{disarmers.Count}.");
                _trapDisarming = false;
                s.AnswerDungeonTrap(disarmers[choice - 1]);
                break;
            }
            case SessionPromptKind.DungeonLock:
                RequireYesNo(choice);
                s.AnswerDungeonLock(goBack: choice == 1);
                break;
            case SessionPromptKind.DungeonExitTakeIt:
                RequireYesNo(choice);
                if (choice == 1)
                    _exitTaken = true;
                else
                    s.AnswerDungeonExit(takeIt: false, saveMap: false);
                break;
            case SessionPromptKind.DungeonExitSaveMap:
                RequireYesNo(choice);
                _exitTaken = false;
                s.AnswerDungeonExit(takeIt: true, saveMap: choice == 1);
                break;
            case SessionPromptKind.Treasure:
                RequireYesNo(choice);
                s.AnswerTreasure(choice == 1);
                break;
            case SessionPromptKind.WildernessEncounter:
            {
                var options = s.Game == GameId.P3 ? P3WildernessOptions(s.WildernessPrompt!.Menu) : WildernessOptions(s.WildernessPrompt!.Menu);
                if (choice < 1 || choice > options.Count)
                    throw new ArgumentOutOfRangeException(nameof(choice), $"1..{options.Count}.");
                s.AnswerWildernessPrompt(options[choice - 1].Answer);
                break;
            }
            case SessionPromptKind.BankShares:
            {
                if (choice < 1 || choice > prompt.Options.Count)
                    throw new ArgumentOutOfRangeException(nameof(choice), $"1..{prompt.Options.Count}.");
                _shares!.Add(int.Parse(prompt.Options[choice - 1]));
                if (_shares.Count == s.Party.Members.Count)
                {
                    var shares = _shares;
                    _shares = null;
                    s.BankDeposit(shares);
                }
                break;
            }
            case SessionPromptKind.ExperienceShares:
            {
                if (choice < 1 || choice > prompt.Options.Count)
                    throw new ArgumentOutOfRangeException(nameof(choice), $"1..{prompt.Options.Count}.");
                _xpShares!.Add(int.Parse(prompt.Options[choice - 1], System.Globalization.CultureInfo.InvariantCulture));
                if (_xpShares.Count == s.TownEntryShareTakers.Count)
                {
                    var shares = _xpShares;
                    _xpShares = null;
                    s.ShareOutExperience(shares);
                }
                break;
            }
            case SessionPromptKind.OverworldEntry:
                RequireYesNo(choice);
                s.AnswerOverworldEntry(enter: choice == 1);
                break;
            case SessionPromptKind.WildernessInn:
                RequireYesNo(choice);
                _innOfferedAt = null;
                if (choice == 1)
                    s.StayAtWildernessInn();
                break;
        }
        return prompt.Kind;
    }

    /// <summary>Menu entries in DOS order (<c>WildernessMenu</c> docs). Labels = COM menu labels <c>0x43..0x45</c> (menu <c>0xf</c>),
    /// <c>0x47</c>/<c>0x48</c> (<c>0x10</c>), <c>0x70</c>/<c>0x71</c> (<c>0x11</c>) — s41 static decode of the menu table.</summary>
    private static IReadOnlyList<(string Label, WildernessAnswer Answer)> WildernessOptions(WildernessMenu menu) => menu switch
    {
        WildernessMenu.BlackKnights =>
            [(GameText.S("core.prompt.run"), WildernessAnswer.Run), (GameText.S("core.prompt.fight"), WildernessAnswer.Fight), (GameText.S("core.prompt.surrender"), WildernessAnswer.Surrender)],
        WildernessMenu.SleepingMonsters => [(GameText.S("core.prompt.attack"), WildernessAnswer.Attack), (GameText.S("core.prompt.leave"), WildernessAnswer.Leave)],
        WildernessMenu.HearsMonsters => [(GameText.S("core.prompt.run"), WildernessAnswer.Run), (GameText.S("core.prompt.fight"), WildernessAnswer.Fight)],
        _ => throw new ArgumentOutOfRangeException(nameof(menu)),
    };

    /// <summary>
    /// III's wilderness questions are Yes / No (root <c>0xe294</c>): "… `core.p3.x_spots_sleeping_monsters` `core.p3.x_spots_sleeping_monsters`" (Yes =
    /// attack) and "… `core.p3.x_hears_monsters_fight` `core.p3.x_hears_monsters_fight`" (Yes = fight, No = run).
    /// </summary>
    private static IReadOnlyList<(string Label, WildernessAnswer Answer)> P3WildernessOptions(WildernessMenu menu) => menu switch
    {
        WildernessMenu.SleepingMonsters => [(Yes, WildernessAnswer.Attack), (No, WildernessAnswer.Leave)],
        WildernessMenu.HearsMonsters => [(Yes, WildernessAnswer.Fight), (No, WildernessAnswer.Run)],
        _ => throw new ArgumentOutOfRangeException(nameof(menu)),
    };

    /// <summary>
    /// A III Yes / No question on the two-item menu root <c>0x75a3</c> (s108 static [C]): <paramref name="lit"/> is lit on
    /// every call (no memory), Y / Return on "Yes" = Yes, N / Esc / Return on "No" = No.
    /// </summary>
    private static SessionPrompt P3YesNo(SessionPromptKind kind, IReadOnlyList<string> lines, int lit) =>
        new(kind, lines, [Yes, No]) { Remembers = false, EscapeChoice = 2, InitialChoice = lit };

    private static string P3InnText(GameSession s)
    {
        var o = s.Overworld;
        return s.Campaign is Phantasie3Campaign p3 && p3.World.PlaceAt(o.RegionId, o.X, o.Y) is { } inn
            ? GameText.F("core.p3.will_you_enter", inn.Name)
            : WildernessInnText;
    }

    private static int WildernessMenuId(WildernessMenu menu) => menu switch
    {
        WildernessMenu.BlackKnights => 0xf,
        WildernessMenu.SleepingMonsters => 0x10,
        _ => 0x11,
    };

    private bool InnOfferStillValid()
    {
        if (_innOfferedAt is not { } at)
            return false;
        var o = _session.Overworld;
        if (_session.Mode != GameMode.Overworld || (o.RegionId, o.X, o.Y) != at)
        {
            _innOfferedAt = null;
            return false;
        }
        return true;
    }

    private static void RequireYesNo(int choice)
    {
        if (choice is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(choice), "YES = 1, NO = 2.");
    }
}
