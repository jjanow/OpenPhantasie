using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>Where the presenter is inside a combat round.</summary>
public enum CombatPhase
{
    Idle,

    /// <summary>PARTY OPTIONS menu (after the morale check).</summary>
    PartyOptions,

    /// <summary>Orders for each status-OK member.</summary>
    Orders,

    /// <summary>Waiting out the round delay, then <see cref="GameSession.ResolveCombatRound"/>.</summary>
    Resolving,
}

/// <summary>III Amiga's windows over the fight's field (s120, FORMAT.md §8.5): M and H.</summary>
public enum AmigaFieldWindow
{
    None,

    /// <summary>`p3field.monster_identification` (<c>0x1a264(1)</c>, window 23 moved down).</summary>
    MonsterNames,

    /// <summary>The party's health (<c>0xadac</c>, window 24).</summary>
    PartyHealth,
}

/// <summary>
/// Presents combat round by round, the DOS way (OV2 <c>0x3b70</c>, <c>combat-resolution.md</c> §Party options):
/// each round starts with the morale check ("MONSTERS RUN"), then PARTY OPTIONS (Beg mercy / Threaten / Greetings /
/// FIGHT / Flee, arrows + Enter), then FIGHT asks the orders of every status-OK member (sleepers are left off).
/// Orders start as PARRY and repeat from round to round until changed (s35 capture; chooser mode 0): Enter keeps a
/// member's order, a menu digit changes it (I: 1 THRUST 2 ATTACK 3 SLASH 4 LUNGE 5 CAST 6 PARRY, from the feature
/// modules' <see cref="FeatureModuleSet.CombatChoices"/>), Backspace starts the list again.
/// The pause before each round is the manual's T)IME LAG. Holding fast-forward skips all of it (Phase 7 QoL: the party auto-thrusts via
/// <see cref="GameSession.FightToEnd"/>).
/// </summary>
public sealed partial class CombatPresenter
{
    private enum OrderStep { Menu, Rank, SpellNumber, HealTarget, PartyRank }

    private const int MaxMembers = 7; // six adventurers + a summoned elemental (slot 7)

    private readonly ClientSettings _settings;
    private readonly List<string> _log = new();
    // Repeat orders (DOS 0x5b96). [D✓] s65: kept across rounds, encounters, a failed FLEE and a surprise opening; DOS
    // resets them to PARRY only on leaving town / after a defeat (D-078 decided manual: they stay until changed; the
    // DOS reset is p1.orders-reset, counted by GameSession.OrdersResets).
    private readonly int[] _lastPacked = new int[MaxMembers];
    private int _ordersResets;
    private readonly Character?[] _orderOwners = new Character?[MaxMembers];
    private double _accumMs;
    private long _logMark;
    private double _clockMs;
    private readonly Dictionary<Character, double> _deathMarks = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// How long a member who died in the round just shown carries the death mark on its portrait: II's ST draws the mark
    /// at portrait + (8,13), then waits 500 ms before rebuilding the row without the dead (<c>0x188fc</c> mode 0,
    /// FORMAT.md §10.3). Only a pack with <see cref="OpenPhantasie.Data.PictureNames.PortraitDeathMark"/> draws it.
    /// </summary>
    public const double DeathMarkMs = 500;

    private int _orderMember = -1;
    private OrderStep _step;
    private CombatChoice _choice;
    private string _spellDigits = "";
    private int _spellIndex = -1;

    public CombatPhase Phase { get; private set; }

    public bool IsPresenting => Phase != CombatPhase.Idle;

    /// <summary>True while the presenter waits for a key (PARTY OPTIONS or orders).</summary>
    public bool AwaitingInput => Phase is CombatPhase.PartyOptions || AwaitingOrders;

    /// <summary>True while the presenter waits for a member's order key (on III Amiga's orders screen: for any key).</summary>
    public bool AwaitingOrders => Phase == CombatPhase.Orders && (_orderMember >= 0 || _amiga || _p1Orders);

    /// <summary>Highlighted entry of <see cref="CombatText.PartyOptionMenu"/>.</summary>
    public int OptionCursor { get; private set; } = CombatText.DefaultPartyOptionIndex;

    /// <summary>Member whose order is being asked, or −1.</summary>
    public int OrderMember => AwaitingOrders ? _orderMember : -1;

    public string LastLine { get; private set; } = "";

    /// <summary>The open Show names / Party health box (empty when none); any key closes it.</summary>
    public IReadOnlyList<string> InfoBox
    {
        get => _infoBox;
        private set
        {
            _infoBox = value;
            if (value.Count == 0)
            {
                InfoBoxIndex = -1;
                _infoBoxCloseMs = null;
            }
        }
    }

    private IReadOnlyList<string> _infoBox = [];

    /// <summary>s154: the <see cref="CombatText.PartyOptionMenu"/> entry <see cref="InfoBox"/> shows, or −1.</summary>
    public int InfoBoxIndex { get; private set; } = -1;

    /// <summary>s154: when I Amiga's Show names list closes by itself (presenter clock, ms), or null.</summary>
    private double? _infoBoxCloseMs;

    /// <summary>
    /// s154: the Show names / Party health box on I Amiga's field: Show names is requester 23 for
    /// ⌊(h3 + <see cref="CombatFieldMessage.ListTicks"/>) / 60⌋ s (h44 <c>+0x1468</c>, the 8-s clock cut included), then it
    /// closes; a key still closes it at once (the Amiga swallows keys there `[~]`). Party health (requester 21, OK) waits.
    /// </summary>
    private void OpenInfoBox(int index, GameSession session)
    {
        InfoBox = CombatText.InfoBox(index, session);
        if (InfoBox.Count == 0)
            return;
        InfoBoxIndex = index;
        if (index == CombatText.ShowNamesIndex && _p1Party && P1Layout is { Message: { ListTicks: > 0 } message } layout)
        {
            var full = message.ListSeconds(layout.LinePolls(P1Speed?.Invoke() ?? 0));
            _infoBoxCloseMs = _clockMs + message.ClockCut(full, P1ClockMs?.Invoke() / 1000) * 1000;
        }
    }

    /// <summary>s154: I Amiga's field draws the open Show names box as requester 23 (top row on, not centred).</summary>
    public bool P1NamesShown => _p1Party && InfoBoxIndex == CombatText.ShowNamesIndex && InfoBox.Count > 0;

    /// <summary>s154: I Amiga's field draws the open Party health box as requester 21 with OK at the fight's Top.</summary>
    public bool P1HealthShown => _p1Party && InfoBoxIndex == CombatText.PartyHealthIndex && InfoBox.Count > 0;
    public IReadOnlyList<string> Log => _log;

    /// <summary>The combat log's box (s99): it follows the newest line; PgUp / PgDn, the wheel or its bar scroll back.</summary>
    public ScrollBox LogScroll { get; } = new();

    /// <summary>The right column's box (options, spells, orders) when its rows run past the panel.</summary>
    public ScrollBox MenuScroll { get; } = new();

    /// <summary>The rank list's box (monster pictures 1:1): Home / End, the wheel or its bar scroll it.</summary>
    public ScrollBox RankScroll { get; } = new();

    /// <summary>
    /// True while <paramref name="member"/> carries the ST's death mark: on II's line-by-line round (<see cref="P2Run"/>) from
    /// the mark after its "DIES" until the row is re-laid; else for <see cref="DeathMarkMs"/> after the round it died in.
    /// </summary>
    public bool HasDeathMark(Character member) =>
        P2Run is { } run ? run.Marked.Contains(member)
        : _deathMarks.TryGetValue(member, out var until) && _clockMs < until;

    /// <summary>
    /// True while <paramref name="member"/> keeps its slot on II's combat row though DEAD: its death mark runs, or on the
    /// line-by-line round it dies in this round and the row has not been re-laid without it yet (ST <c>0x18a02</c>).
    /// </summary>
    public bool KeepsRowSlot(Character member) =>
        HasDeathMark(member) || P2Run is { } run && run.Dying.Contains(member) && !run.Removed.Contains(member);

    /// <summary>
    /// II ST's combat lines (s136, <see cref="P2CombatLineRun"/>): the pack's table when II's round plays line by line with
    /// its sounds; null = the round's lines at once (set by the front end).
    /// </summary>
    public Func<StCombatLines?>? P2Lines { get; set; }

    /// <summary>How long II ST's sound n runs, seconds (0 with the Sound switch off or no pack: no sound waits).</summary>
    public Func<int, double>? P2SoundSeconds { get; set; }

    /// <summary>The II round playing line by line now, or null.</summary>
    public P2CombatLineRun? P2Run { get; private set; }

    private CombatResult? _p2Round;
    private int _p2Pushed;
    private IReadOnlyList<IReadOnlyList<Monster>>? _p2RanksBefore;

    /// <summary>
    /// s156: while II's round plays line by line, the monster ranks as they stood before it (the order kept; the ST re-ranks
    /// at the round's end), each with the monsters whose picture is not erased yet (<see cref="P2CombatLineRun.Slain"/>).
    /// A rank whose monsters are all erased: with the pack's <see cref="StCombatLines.MonsterSlots"/> it stays as an empty
    /// row until the re-rank (s163, the ST: <c>0x18458</c> lays the groups out from their counts, which only
    /// <c>0x18288</c> rewrites at the round's end, and an erase only zeroes the slot's width), else it is dropped. Null
    /// outside a round's run: the encounter's own ranks show.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<Monster>>? P2RankRows =>
        P2RanksShown() is { } shown && P2Run is { } run
            ? shown.Select(r => (IReadOnlyList<Monster>)r.Where(m => !run.Slain.Contains(m)).ToList()).ToList()
            : null;

    /// <summary>
    /// s163: the kind of each row of <see cref="P2RankRows"/> (its first monster before the round), for the row's line and
    /// picture size when every monster of it is erased. Null outside a round's run.
    /// </summary>
    public IReadOnlyList<Monster>? P2RankKinds => P2RanksShown()?.Select(r => r[0]).ToList();

    // The ranks before the round that show while it plays: the ones with a monster then; with monster slots all of them
    // (an emptied rank keeps its row), else those with a monster not erased yet.
    private List<IReadOnlyList<Monster>>? P2RanksShown()
    {
        if (P2Run is not { } run || _p2Round is null || _p2RanksBefore is not { } before)
            return null;
        var keep = P2Lines?.Invoke() is { MonsterSlots: true };
        return before.Where(r => r.Count > 0 && (keep || r.Any(m => !run.Slain.Contains(m)))).ToList();
    }

    /// <summary>
    /// s162: II ST's monster slots (<see cref="StCombatLines.MonsterSlots"/>, FORMAT.md 7.3 "The poses"): per rank shown
    /// (as <see cref="P2RankRows"/>) its monsters in order, an erased one (<see cref="P2CombatLineRun.Slain"/>) as null: its
    /// slot stays empty, the others keep their places (<c>0x18856</c>) until the round's re-rank; a rank with every
    /// monster erased is a row of empty slots (s163). Outside a round's run the encounter's living monsters. Null when the
    /// pack draws one picture per rank or there is no fight.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<Monster?>>? P2RankSlots(CombatEncounter? combat)
    {
        if (P2Lines?.Invoke() is not { MonsterSlots: true })
            return null;
        if (P2RanksShown() is { } shown && P2Run is { } run)
            return shown.Select(r => (IReadOnlyList<Monster?>)r.Select(m => run.Slain.Contains(m) ? null : m).ToList()).ToList();
        return combat?.Ranks.Where(r => !r.IsWiped).Select(r => (IReadOnlyList<Monster?>)r.Living.ToList()).ToList();
    }

    /// <summary>A sparkle roll for II's run (tests pin it), 0..32767.</summary>
    public Func<int>? P2Roll { get; set; }

    /// <summary>A key or a click while II's round plays: ends the hold running now (the ST's <c>evnt_multi</c> waits).</summary>
    public void P2Key() => P2Run?.Key();

    /// <summary>The sounds II's round has started since the last call (the front end plays them).</summary>
    public IReadOnlyList<int> TakeP2Sounds()
    {
        var due = _p2PendingSounds.ToList();
        _p2PendingSounds.Clear();
        if (P2Run is { } run)
            due.AddRange(run.TakeSounds());
        return due;
    }

    // Sounds due that no run holds: the fight-start sound 17, and what a finished run had not handed over yet.
    private readonly List<int> _p2PendingSounds = [];

    public CombatPresenter(ClientSettings settings) =>
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>
    /// Turns a round's hits into its spell flashes (III Amiga, <see cref="CombatFlashRun.Plan"/>); null = no flashes (the
    /// pack has none). Set by the front end, which knows the pack.
    /// </summary>
    public Func<IReadOnlyList<CombatHit>, CombatFlashRun?>? PlanFlashes { get; set; }

    /// <summary>The flashes of the round just shown, or null. The next round waits until they are over (s110).</summary>
    public CombatFlashRun? FlashRun { get; private set; }

    /// <summary>Counts the flash runs started, so the front end starts each run's sound once.</summary>
    public int FlashRunSerial { get; private set; }

    private double _flashStartMs;
    private bool _holdForFlashes;

    /// <summary>
    /// III Amiga's combat field (s117, <see cref="CombatFieldRun"/>): true = play each round on the field one line at a
    /// time (the game's AMIGA SCREENS switch and a pack with the field's art). Set by the front end; null / false = the
    /// remaster's screen, a round's lines at once.
    /// </summary>
    public Func<bool>? AmigaField { get; set; }

    /// <summary>
    /// III Amiga's orders screen (s118, <c>0x188e4</c>; FORMAT.md §8.3): true = the orders are given on the original's
    /// screen, member by member in any order (a member's box, then an option letter), F or the Fight! button starts the
    /// round. Set by the front end (the game's AMIGA SCREENS switch and a pack with the screen's art); null / false = the
    /// remaster's list, each member asked in turn.
    /// </summary>
    public Func<bool>? AmigaOrders { get; set; }

    private bool _amiga;

    /// <summary>
    /// I Amiga's combat screen (s132, <see cref="P1CombatFieldRun"/>; Amiga FORMAT.md "The combat screen"): true = the
    /// field with its menu strip, the orders requester and each round played line by line (the game's AMIGA SCREENS switch
    /// and a pack with <see cref="P1Layout"/>). Set by the front end; null / false = the remaster's screen.
    /// </summary>
    public Func<bool>? AmigaP1Field { get; set; }

    /// <summary>I Amiga's combat screen layout (the pack's <c>combat_field</c>); set by the front end.</summary>
    public CombatFieldLayout? P1Layout { get; set; }

    /// <summary>How long I Amiga's sound n holds the screen, seconds (0 with the Sound switch off: no wait, user s129).</summary>
    public Func<int, double>? P1SoundSeconds { get; set; }

    /// <summary>I Amiga's Speed for the line waits: 1 Fast, 2 Normal, 3 Slow, 0 = not picked yet (the boot value).</summary>
    public Func<int>? P1Speed { get; set; }

    /// <summary>
    /// The machine's clock in ms for requester 23's wrap quirk (h80 <c>+0xd2a</c>, s136: a box closes when the clock's
    /// seconds reach a multiple of 8); null = not modelled (the box stays its whole seconds). The Amiga reads Intuition's
    /// <c>CurrentTime</c>, which no game value sets; the front end passes the host's clock `[~]`.
    /// </summary>
    public Func<double>? P1ClockMs { get; set; }

    private bool OnP1Field => P1Layout is not null && AmigaP1Field?.Invoke() == true;

    private bool _p1Orders;

    /// <summary>True while I Amiga's orders requester is up (requester 21, h71 <c>+0x90</c>).</summary>
    public bool P1OrdersShown => Phase == CombatPhase.Orders && _p1Orders;

    /// <summary>The I field's round now (its lines and sounds), or null.</summary>
    public P1CombatFieldRun? P1Run { get; private set; }

    /// <summary>Who stood on the I field when <see cref="P1Run"/>'s round began.</summary>
    public P1CombatFieldSnapshot? P1Snapshot { get; private set; }

    /// <summary>The I field's state now, or null when no round plays.</summary>
    public P1CombatFieldRun.State? P1Shown => P1Run?.At(_clockMs - _fieldStartMs);

    /// <summary>Milliseconds into <see cref="P1Run"/>.</summary>
    public double P1RunMs => _clockMs - _fieldStartMs;

    /// <summary>The last line the I field printed: it stays under the party until the next round's set-up clears the field.</summary>
    public string? P1LastLine { get; private set; }

    /// <summary>
    /// D-058 on I Amiga's field (user s140: "do whatever shows it but doesn't slow things down"): the remaster's own encounter
    /// flash ("A DAY ENCOUNTER", the dungeon's "AN ENCOUNTER"; the Amiga has neither) on the field's line from the fight's
    /// start, adding no wait, until the fight's first line prints or its first run ends. Null for a night fight: the
    /// Amiga's `core.encounter.a_night_encounter` box (h74 <c>+0x6c</c>) has shown it over the map.
    /// </summary>
    public string? P1Banner { get; private set; }

    /// <summary>The I field's line now: the round's, else the last printed, else <see cref="P1Banner"/>.</summary>
    public string? P1LineText => P1Shown?.Text ?? P1LastLine ?? P1Banner;

    /// <summary>The fight's remaster-only D-058 flash in <paramref name="log"/> (its last lines), or null.</summary>
    public static string? RemasterBanner(IReadOnlyList<string> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        for (var i = log.Count - 1; i >= Math.Max(0, log.Count - 8); i--)
        {
            if (log[i] == NightEncounter.Banner)
                return null;
            if (log[i] == GameSession.EncounterBanner || log[i] == GameSession.DayEncounterBanner)
                return log[i];
        }
        return null;
    }

    /// <summary>True while I Amiga's combat screen is up (a round playing, Party Options or the orders, s165 the finds after it).</summary>
    public bool P1FieldShown => P1Run is not null || (IsPresenting && _p1Party) || P1OpeningPending || P1AfterFightShown;

    /// <summary>
    /// s165: the field a won fight leaves while its finds are asked (h44 <c>+0x942</c> / <c>+0x956</c>: "YOU FIND %s.|KEEP
    /// IT?" through h86 <c>+0x32a</c>, before h53 <c>+0x2a4</c> leaves the fight screen), as state 12's set-up left it
    /// (h44 <c>+0x614</c> → h53 <c>+0xfc</c> / <c>+0x274</c>: the row as it stands, the groups emptied, the frame, no line).
    /// Set by the front end after the fight when an item waits, null when none; a new fight drops it.
    /// </summary>
    public P1CombatFieldSnapshot? P1AfterFight { get; set; }

    /// <summary>s165: the after-fight field (<see cref="P1AfterFight"/>) is what the I field shows now.</summary>
    public bool P1AfterFightShown => P1AfterFight is not null && !IsPresenting && P1Run is null && OnP1Field;

    /// <summary>
    /// A surprise opening whose round 1 has not been planned yet (the frame between <see cref="Start"/> and the first
    /// update): the field is up and black, as h53 <c>+0x274</c>'s BltClear leaves it (s138).
    /// </summary>
    public bool P1OpeningPending => _p1SurpriseOpening && P1Run is null && IsPresenting && OnP1FieldOnly;

    private bool _p1Party;

    /// <summary>Counts the fights the presenter started (the front end plays I Amiga's fight-start sound once each).</summary>
    public int P1FightSerial { get; private set; }

    private double _p1IntroStartMs;
    private bool _p1IntroOver = true;

    // The fight opened with round 1 (the surprise flags: OV2 / h44 +0x0 -> state 5): its run carries sound 17's black (s136).
    private bool _p1SurpriseOpening;

    // Where a key cut sound 17's black, ms from the fight's start (s138); null = not cut.
    private double? _p1BlackCutMs;

    // Plans the surprise opening's run again with another black (a key cut it, s138).
    private Func<double, P1CombatFieldRun>? _p1Replan;

    /// <summary>Counts the keys that cut sound 17 (h89's player ends a sound at a step on a key or click): the front end stops it.</summary>
    public int P1StartSoundCuts { get; private set; }

    private double P1IntroBlackMs(CombatFieldLayout layout)
    {
        var full = layout.FightStartSound is int start ? Math.Max(0, P1SoundSeconds?.Invoke(start) ?? 0) * 1000 : 0;
        return _p1BlackCutMs is double cut ? Math.Min(full, cut) : full;
    }

    /// <summary>A mouse click on I's field (s138): during sound 17's black it cuts the black like a key (h89's player).</summary>
    public void P1Click() => CutP1Black();

    // A key while sound 17 holds the black (the fight's start or a surprise opening's round 1) cuts it there (s138).
    private void CutP1Black()
    {
        if (P1Layout is not { } layout)
            return;
        if (P1Run is { OpensFight: true } run && _p1Replan is { } replan)
        {
            var at = _clockMs - _fieldStartMs;
            if (at < run.LeadBlackMs)
            {
                P1Run = replan(at);
                P1StartSoundCuts++;
            }
            return;
        }
        if (_p1IntroOver || P1Run is not null || !_p1Party)
            return;
        var t = _clockMs - _p1IntroStartMs;
        if (t < P1IntroBlackMs(layout))
        {
            _p1BlackCutMs = t;
            P1StartSoundCuts++;
        }
    }

    /// <summary>
    /// The fight's start on I's field (s135 / s136, <see cref="P1CombatFieldRun.Intro"/>): black while sound 17 holds (its
    /// <see cref="P1SoundSeconds"/>, 0 with the Sound switch off), then the set-up, for a row of
    /// <paramref name="members"/> and <paramref name="monsters"/> standing; null once over or when a round plays. A key or
    /// click during the black cuts sound 17 there (<see cref="P1StartSoundCuts"/>) and the set-up still runs (s138 live: a
    /// click 0.6 s into the black, the set-up at once, the bar after it).
    /// </summary>
    public P1CombatFieldRun.IntroView? P1Intro(int members, int monsters)
    {
        if (_p1IntroOver || P1Run is not null || P1Layout is not { } layout || !_p1Party)
            return null;
        var blackMs = P1IntroBlackMs(layout);
        var view = P1CombatFieldRun.Intro(layout, _clockMs - _p1IntroStartMs, blackMs, members, monsters);
        if (view is null)
            _p1IntroOver = true;
        return view;
    }

    /// <summary>Counts the I field's runs started, so the front end starts each run's sounds once.</summary>
    public int P1RunSerial { get; private set; }

    /// <summary>
    /// The members I Amiga's orders requester lists (h71 <c>+0x90</c>): status OK only, in record order (DOS OV2
    /// <c>0x6ab9</c> lists the same, <see cref="IsOrderable"/>), at most seven rows.
    /// </summary>
    public static IReadOnlyList<int> P1OrderRows(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return [.. session.Party.Members.Select((m, i) => (m, i)).Where(p => p.i < MaxMembers && IsOrderable(p.m)).Select(p => p.i)];
    }

    /// <summary>The member being given an order on the I field's requester (−1 = the list).</summary>
    public int P1PickedMember => P1OrdersShown ? _orderMember : -1;

    /// <summary>The order whose rank, spell or target is being asked.</summary>
    public CombatChoice PendingChoice => _choice;

    /// <summary>The I field asks the picked member's order (requester 24's buttons).</summary>
    public bool P1AsksOrder => P1OrdersShown && _orderMember >= 0 && _step == OrderStep.Menu;

    /// <summary>The I field's spell list is up (requester 12, s133: Cast picked).</summary>
    public bool P1AsksSpell => P1OrdersShown && _orderMember >= 0 && _step == OrderStep.SpellNumber && P1SpellLists;

    /// <summary>The I field's "ON WHOM:" is up (requester 6, s133: a HEALING spell picked).</summary>
    public bool P1AsksTarget => P1OrdersShown && _orderMember >= 0 && _step == OrderStep.HealTarget && P1SpellLists;

    /// <summary>The pack has I's Cast requesters (else the spell number is typed into requester 26, as before s133).</summary>
    private bool P1SpellLists => P1Layout?.SpellList is not null;

    /// <summary>Rows a page of requester 12 shows (6).</summary>
    private int P1SpellRows => P1Layout?.SpellList?.Spells.Rows ?? 6;

    /// <summary>The first spell of requester 12's page (h23 <c>+0x570</c>'s scroll offset).</summary>
    public int P1SpellFirst { get; private set; }

    /// <summary>
    /// The spells requester 12 lists for the picked member, in slot order (h23 <c>+0x570</c>: every slot whose word is 1,
    /// ascending). Core's / the option's list (<see cref="CombatText.CombatSpellChoices"/>: D-063 <c>dos</c> = every known
    /// spell, as the Amiga; <c>manual</c> = the combat-castable ones).
    /// </summary>
    public IReadOnlyList<int> P1SpellList(GameSession session) =>
        _orderMember >= 0 && _orderMember < session.Party.Members.Count
            ? CombatText.CombatSpellChoices(session.Party.Members[_orderMember], session.Options) : [];

    /// <summary>The spells on requester 12's current page.</summary>
    public IReadOnlyList<int> P1SpellPage(GameSession session) =>
        P1SpellList(session).Skip(P1SpellFirst).Take(P1SpellRows).ToList();

    /// <summary>Scroll gadget 2 (up) is live: the page does not start at the first spell (h23 <c>+0xcd0</c>).</summary>
    public bool P1SpellCanScrollUp => P1SpellFirst > 0;

    /// <summary>Scroll gadget 3 (down) is live: spells lie past the page.</summary>
    public bool P1SpellCanScrollDown(GameSession session) => P1SpellList(session).Count > P1SpellFirst + P1SpellRows;

    /// <summary>III Amiga's spell / heal window (s119): the spell page's first index and the cursor row on it.</summary>
    private int _amigaFirst, _amigaCursor;

    /// <summary>Spells a page of the Amiga's spell window shows (<c>0xf770</c>: rows 6–17).</summary>
    public const int AmigaSpellRows = 12;

    /// <summary>
    /// III Amiga's fight menus (s119, FORMAT.md §8.5; menu list <c>0x2397c</c>, key table <c>0x2189c</c> rows 6–8): File
    /// (its "?) Info" has no key in a fight), Options (F Fight, T Threaten, G Greetings, B Beg for mercy, R Run away,
    /// M Monster Names, H Party health) and Speed (+ Fast, = Normal, − Slow). Each item: its key and its label's string id.
    /// </summary>
    public static readonly IReadOnlyList<(string TitleId, IReadOnlyList<(Keys Key, string LabelId)> Items)> AmigaFieldMenus =
    [
        ("p3field.menu.file", [(Keys.None, "p3field.item.info")]),
        ("p3field.menu.options",
        [
            (Keys.F, "p3field.item.F"), (Keys.T, "p3field.item.T"), (Keys.G, "p3field.item.G"), (Keys.B, "p3field.item.B"),
            (Keys.R, "p3field.item.R"), (Keys.M, "p3field.item.M"), (Keys.H, "p3field.item.H"),
        ]),
        ("p3field.menu.speed", [(Keys.Add, "p3field.item.fast"), (Keys.OemPlus, "p3field.item.normal"),
            (Keys.OemMinus, "p3field.item.slow")]),
    ];

    /// <summary>
    /// I Amiga's menu strip on the field (h81, s132): Info (no items in a fight `[~]`), Party Options (Beg mercy, Threaten,
    /// Greetings, Fight, Flee, Show names, Party health: <c>h71 +0x36</c> picks 1–7) and Speed (Fast, Normal, Slow:
    /// <c>h65 +0x27e</c>). The Amiga's items have no keys; the letters are the remaster's (III's).
    /// </summary>
    public static readonly IReadOnlyList<(string TitleId, IReadOnlyList<(Keys Key, string LabelId)> Items)> P1FieldMenus =
    [
        ("p1field.menu.info", []),
        ("p1field.menu.party_options",
        [
            (Keys.B, "p1field.item.beg_mercy"), (Keys.T, "p1field.item.threaten"), (Keys.G, "p1field.item.greetings"),
            (Keys.F, "p1field.item.fight"), (Keys.R, "p1field.item.flee"), (Keys.M, "p1field.item.show_names"),
            (Keys.H, "p1field.item.party_health"),
        ]),
        ("p1field.menu.speed", [(Keys.Add, "p1field.item.fast"), (Keys.OemPlus, "p1field.item.normal"),
            (Keys.OemMinus, "p1field.item.slow")]),
    ];

    /// <summary>The menus of the field now: I's on the I field, else III's.</summary>
    public IReadOnlyList<(string TitleId, IReadOnlyList<(Keys Key, string LabelId)> Items)> FieldMenus =>
        _p1Party ? P1FieldMenus : AmigaFieldMenus;

    /// <summary>The Options menu's index in <see cref="AmigaFieldMenus"/> (and in <see cref="P1FieldMenus"/>).</summary>
    public const int AmigaOptionsMenu = 1;

    private bool _amigaParty;

    /// <summary>
    /// True while III Amiga's fight waits for an Options choice on the field (s119, <c>0x14ed2</c>): the field with the
    /// menu bar instead of the remaster's PARTY OPTIONS panel (the AMIGA SCREENS switch, <see cref="AmigaField"/>).
    /// </summary>
    public bool AmigaFieldMenuShown => Phase == CombatPhase.PartyOptions && _amigaParty && !_p1Party;

    /// <summary>True while I Amiga's field waits for a Party Options pick (its menu strip, s132).</summary>
    public bool P1FieldMenuShown => Phase == CombatPhase.PartyOptions && _p1Party;

    /// <summary>The fight menu pulled down (index in <see cref="AmigaFieldMenus"/>), or null when the bar is closed.</summary>
    public int? FieldMenuOpen { get; private set; }

    /// <summary>The highlighted item of the open fight menu.</summary>
    public int FieldMenuItem { get; private set; }

    /// <summary>
    /// True when M / H on the field open the Amiga's own windows (s120: `p3field.monster_identification` <c>0x1a264(1)</c>, the
    /// party's health <c>0xadac</c>; the pack has them); null / false = the remaster's names / health box.
    /// </summary>
    public Func<bool>? AmigaFieldWindows { get; set; }

    /// <summary>The Amiga window M or H opened over the field, waiting for any key or a click (<c>0x1e8c4(1)</c>).</summary>
    public AmigaFieldWindow FieldWindow { get; private set; }

    /// <summary>Called with the Speed menu's new setting (the front end stores it as the game's COMBAT SPEED).</summary>
    public Action<AmigaCombatSpeed>? FieldSpeedChosen { get; set; }

    /// <summary>A press on a fight menu's title: pulls that menu down, or closes it when it is already down.</summary>
    public void OpenFieldMenu(int menu)
    {
        if (!(AmigaFieldMenuShown || P1FieldMenuShown) || menu < 0 || menu >= FieldMenus.Count || FieldMenus[menu].Items.Count == 0)
            return;
        FieldMenuOpen = FieldMenuOpen == menu ? null : menu;
        FieldMenuItem = FirstFieldItem(menu);
    }

    /// <summary>The Speed menu opens on the setting in force (the one it ticks); the others on their first item.</summary>
    private int FirstFieldItem(int menu) => menu == AmigaSpeedMenu ? (int)FieldSpeed - 1
        : _p1Party && menu == AmigaOptionsMenu ? OptionCursor : 0; // I: the last pick (Fight at first), as DOS's menu

    /// <summary>The Speed menu's index in <see cref="AmigaFieldMenus"/>; its items are Fast, Normal, Slow (<see cref="AmigaCombatSpeed"/> − 1).</summary>
    public const int AmigaSpeedMenu = 2;

    /// <summary>The pointer over item <paramref name="item"/> of the open menu highlights it.</summary>
    public void HoverFieldMenuItem(int item)
    {
        if (FieldMenuOpen is { } menu && item >= 0 && item < FieldMenus[menu].Items.Count)
            FieldMenuItem = item;
    }

    /// <summary>
    /// A key while the field waits for an Options choice (<c>0x19928</c>). The menus' letters act at once with the bar
    /// open or closed: F fights (the orders), T / G / B / R are the party options, M and H show the names / the party's
    /// health and come back (the round is not used); + (numeric pad) / = / − set Speed (<c>0x870e</c>). The remaster's
    /// additions: Up / Down pull Options down and move its highlight, Left / Right step to the next menu, Return / Space
    /// take the highlighted item (or pull Options down), Esc closes the menu. Other keys do nothing.
    /// </summary>
    private void HandleFieldMenuKey(GameSession session, Keys key)
    {
        if (key == Keys.Subtract)
            key = Keys.OemMinus;
        var menus = FieldMenus;
        var direct = menus.SelectMany(m => m.Items).FirstOrDefault(i => i.Key != Keys.None && i.Key == key);
        if (direct.Key != Keys.None)
        {
            FieldMenuOpen = null;
            ActOnFieldItem(session, direct.Key);
            return;
        }
        switch (key)
        {
            case Keys.Escape:
                FieldMenuOpen = null;
                break;
            case Keys.Up or Keys.Down when FieldMenuOpen is null:
                FieldMenuOpen = AmigaOptionsMenu;
                FieldMenuItem = FirstFieldItem(AmigaOptionsMenu);
                break;
            case Keys.Up or Keys.Down when FieldMenuOpen is { } open:
                var count = menus[open].Items.Count;
                FieldMenuItem = (FieldMenuItem + (key == Keys.Up ? count - 1 : 1)) % count;
                break;
            case Keys.Left or Keys.Right when FieldMenuOpen is { } open:
                var next = open;
                do
                    next = (next + (key == Keys.Left ? menus.Count - 1 : 1)) % menus.Count;
                while (menus[next].Items.Count == 0 && next != open); // I's Info has no items in a fight
                FieldMenuOpen = next;
                FieldMenuItem = FirstFieldItem(next);
                break;
            case Keys.Enter or Keys.Space when FieldMenuOpen is null:
                FieldMenuOpen = AmigaOptionsMenu;
                FieldMenuItem = FirstFieldItem(AmigaOptionsMenu);
                break;
            case Keys.Enter or Keys.Space when FieldMenuOpen is { } open:
                var item = menus[open].Items[Math.Clamp(FieldMenuItem, 0, menus[open].Items.Count - 1)];
                if (_p1Party && open == AmigaOptionsMenu)
                    OptionCursor = FieldMenuItem;
                FieldMenuOpen = null;
                if (item.Key != Keys.None)
                    ActOnFieldItem(session, item.Key);
                break;
        }
    }

    private void ActOnFieldItem(GameSession session, Keys key)
    {
        switch (key)
        {
            case Keys.F: ChooseOption(session, PartyOption.Fight); break;
            case Keys.T: ChooseOption(session, PartyOption.Threaten); break;
            case Keys.G: ChooseOption(session, PartyOption.Greetings); break;
            case Keys.B: ChooseOption(session, PartyOption.BegMercy); break;
            case Keys.R: ChooseOption(session, PartyOption.Flee); break;
            case Keys.M when AmigaFieldWindows?.Invoke() == true: FieldWindow = AmigaFieldWindow.MonsterNames; break;
            case Keys.H when AmigaFieldWindows?.Invoke() == true: FieldWindow = AmigaFieldWindow.PartyHealth; break;
            case Keys.M: OpenInfoBox(CombatText.ShowNamesIndex, session); break;
            case Keys.H: OpenInfoBox(CombatText.PartyHealthIndex, session); break;
            case Keys.Add or Keys.OemPlus or Keys.OemMinus:
                FieldSpeed = key == Keys.Add ? AmigaCombatSpeed.Fast : key == Keys.OemPlus ? AmigaCombatSpeed.Normal : AmigaCombatSpeed.Slow;
                FieldSpeedChosen?.Invoke(FieldSpeed);
                break;
        }
    }

    /// <summary>True while the orders are given on III Amiga's orders screen (<see cref="AmigaOrders"/>).</summary>
    public bool AmigaOrdersShown => Phase == CombatPhase.Orders && _amiga;

    /// <summary>The Amiga's `p3combat.rank_question` window is open (window 20, <c>0x19806</c>).</summary>
    public bool AsksRankWindow => AmigaOrdersShown && _orderMember >= 0 && _step == OrderStep.Rank;

    /// <summary>The packed order member <paramref name="index"/> holds now (what the orders screen labels).</summary>
    public int PackedOrder(int index) => (uint)index < MaxMembers ? _lastPacked[index] : CombatAction.Cleared;

    /// <summary>The Amiga Speed menu's setting for the field's waits.</summary>
    public AmigaCombatSpeed FieldSpeed { get; set; } = AmigaCombatSpeed.Normal;

    /// <summary>The round on the field now (its lines, then its flashes), or null.</summary>
    public CombatFieldRun? FieldRun { get; private set; }

    /// <summary>Who stood on the field when <see cref="FieldRun"/>'s round began.</summary>
    public CombatFieldSnapshot? FieldSnapshot { get; private set; }

    /// <summary>Counts the field runs started, so the front end starts each run's sounds once.</summary>
    public int FieldRunSerial { get; private set; }

    private double _fieldStartMs;
    private CombatResult? _fieldRound;

    /// <summary>The field's state now (lines over: the last moment, flashes may still run), or null.</summary>
    public CombatFieldRun.State? FieldShown => FieldRun?.At(_clockMs - _fieldStartMs);

    private void EndField()
    {
        P2Run = null;
        _p2Round = null;
        _p2Then = null;
        _p2EndsFight = false;
        FieldRun = null;
        FieldSnapshot = null;
        P1Run = null;
        P1Snapshot = null;
        _fieldRound = null;
        _p1Interlude = false;
        _p1Then = null;
        _p1EndsFight = false;
    }

    // I Amiga: a party option's or the morale check's lines in requester 23 (h44 +0x466 …), outside a round.
    private bool _p1Interlude;
    private Action<GameSession>? _p1Then;
    private bool _p1EndsFight;

    /// <summary>I's field without the III field (the round runs and requester 23 belong to it).</summary>
    private bool OnP1FieldOnly => OnP1Field && AmigaField?.Invoke() != true;

    /// <summary>
    /// I Amiga: when the lines noted since <paramref name="mark"/> hold a requester-23 message (a party option's answer, the
    /// morale check's "MONSTERS RUN", the fight's end), play them on the field (<see cref="P1CombatFieldRun"/> without a
    /// round) before going on: <paramref name="endsFight"/> ends the encounter, else <paramref name="then"/> runs. Keys are
    /// swallowed meanwhile (h80 <c>+0x546</c>). False (nothing started) when there is no such line.
    /// </summary>
    private bool StartP1Interlude(GameSession session, P1CombatFieldSnapshot? snapshot, long mark, bool endsFight,
        Action<GameSession>? then)
    {
        if (snapshot is null || P1Layout is not { Message: { } message } layout)
            return false;
        var scene = session.PendingRewardScene?.Lines;
        var lines = session.LogSince(mark).Where(l => scene is null || !scene.Contains(l)).ToList();
        if (!lines.Any(l => !string.IsNullOrEmpty(l) && P1CombatFieldRun.MessageKey(message, l, outsideRound: true) is not null))
            return false;
        P1Snapshot = snapshot;
        P1Run = P1CombatFieldRun.Plan(layout, lines, [], snapshot.Row, [], _ => false, P1Speed?.Invoke() ?? (int)FieldSpeed,
            P1SoundSeconds ?? (_ => 0), roundEnd: false, clockMs: P1ClockMs?.Invoke());
        _fieldRound = null;
        _fieldStartMs = _clockMs;
        P1RunSerial++;
        _p1Interlude = true;
        _p1Then = then;
        _p1EndsFight = endsFight;
        Phase = CombatPhase.Resolving;
        return true;
    }

    /// <summary>VBLs since the flash run started.</summary>
    private double FlashVbl => FlashRun is { } run ? (_clockMs - _flashStartMs) / 1000.0 * run.Layout.VblHz + 1e-6 : 0;

    /// <summary>
    /// The flash shown now and how many of its frames are drawn, or null: on the Amiga's field the one playing among the
    /// round's lines (<see cref="CombatFieldRun"/>), else the one of the run after the round's lines.
    /// </summary>
    public (CombatFlashRun.Flash Flash, int Frames)? FlashShown =>
        FieldRun is not null ? FieldShown?.Flash : FlashRun?.At(FlashVbl);

    /// <summary>The flashes' layout for <see cref="FlashShown"/>, or null.</summary>
    public CombatFlashLayout? FlashLayout => FieldRun?.FlashRun?.Layout ?? FlashRun?.Layout;

    private void StartFlashes(IReadOnlyList<CombatHit> hits)
    {
        FlashRun = PlanFlashes?.Invoke(hits);
        _flashStartMs = _clockMs;
        if (FlashRun is not null)
            FlashRunSerial++;
    }

    private void EndFlashes()
    {
        FlashRun = null;
        _holdForFlashes = false;
    }

    public void Start(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _accumMs = 0;
        EndFlashes();
        _log.Clear();
        LogScroll.Set(0, 1);
        MenuScroll.Set(0, 1);
        RankScroll.Reset();
        _deathMarks.Clear();
        InfoBox = [];
        FieldWindow = AmigaFieldWindow.None;
        P1LastLine = null;
        P1AfterFight = null; // s165: a new fight's own field
        P1Banner = session.Mode == GameMode.Combat ? RemasterBanner(session.Log) : null; // D-058, s140: no wait
        _p1ToneThen = null;
        P1FightSerial++;
        _p1IntroStartMs = _clockMs;
        _p1IntroOver = false;
        _p1BlackCutMs = null;
        _p1Replan = null;
        _p1SurpriseOpening = session.Mode == GameMode.Combat && !session.CombatRoundHasOptions;
        var members = session.Party.Members;
        var reset = session.OrdersResets != _ordersResets; // D-078 dos: left town / defeated since the last fight
        _ordersResets = session.OrdersResets;
        for (var i = 0; i < MaxMembers; i++)
        {
            var owner = i < members.Count ? members[i] : null;
            if (reset)
                _lastPacked[i] = CombatAction.Cleared;
            if (!ReferenceEquals(_orderOwners[i], owner)) // remaster: a slot whose member changed starts on PARRY
                _lastPacked[i] = CombatAction.Cleared;
            _orderOwners[i] = owner;
        }
        _logMark = session.LogSequence;
        _p2PendingSounds.Clear();
        // II ST: the combat screen starts with sound 17 (0x1838e from the set-up 0xfd82, whatever started the fight) when
        // [0x23eb6] is set: not on the first fight after a defeat (s163; only combat_victory's end sets it again); the
        // first line's sound wait ignores it (not tracked).
        if (session.Mode == GameMode.Combat && !session.LastFightLost && P2Lines?.Invoke() is { FightStartSound: >= 0 } p2Table)
            _p2PendingSounds.Add(p2Table.FightStartSound);
        LastLine = session.LastMessage;
        // Open the log at the encounter flash (D-058: "AN ENCOUNTER", or "A DAY / NIGHT ENCOUNTER") when it is still in view.
        var log = session.Log;
        var from = log.Count - 1;
        for (var i = log.Count - 1; i >= Math.Max(0, log.Count - 8); i--)
        {
            if (log[i] == GameSession.EncounterBanner || log[i] == GameSession.DayEncounterBanner || log[i] == NightEncounter.Banner)
            {
                from = i;
                break;
            }
        }
        for (var i = Math.Max(0, from); i < log.Count; i++)
            Push(log[i]);
        Phase = session.Mode == GameMode.Combat ? CombatPhase.Resolving : CombatPhase.Idle;
        if (IsPresenting)
            BeginRound(session);
    }

    public void Cancel()
    {
        P1AfterFight = null;
        InfoBox = [];
        FieldWindow = AmigaFieldWindow.None;
        Phase = CombatPhase.Idle;
        _accumMs = 0;
        _orderMember = -1;
        _amiga = false;
        _p1Orders = _p1Party = false;
        EndFlashes();
        EndField();
    }

    /// <summary>Current order of member <paramref name="index"/> as shown in the orders list.</summary>
    public string OrderLabelFor(GameSession session, int index)
    {
        var ch = session.Party.Members[index];
        return CombatText.OrderLabel(CombatText.RepeatFor(_lastPacked[index], ch.Class, LivingRanks(session)));
    }

    /// <summary>Members listed on the orders screen: status OK only (OV2 <c>0x6ab9</c>).</summary>
    public static bool IsOrderable(Character ch) =>
        ch.Status is CharacterStatus.Ok or CharacterStatus.Down && ch.HpCur > 0; // Down: III only (a leg Broken, still fights)

    /// <summary>
    /// Advance presentation. Returns true when the encounter ends.
    /// </summary>
    public bool Update(GameSession session, double elapsedMs, bool fastForward)
    {
        _clockMs += elapsedMs;
        if (_infoBoxCloseMs is { } close && _clockMs >= close && InfoBox.Count > 0)
        {
            InfoBox = []; // s154: I Amiga's Show names list closes after its seconds (h80 +0x546)
            LastLine = Prompt(session);
        }
        if (P1Banner is not null && P1Shown?.Text is { Length: > 0 })
            P1Banner = null; // D-058: the fight's first line takes the field's line
        FinishP1Tone(session);
        if (!IsPresenting || (session.Mode != GameMode.Combat && FieldRun is null && P1Run is null && P2Run is null))
        {
            Phase = CombatPhase.Idle;
            EndField();
            return false;
        }

        if (_p1Interlude && P1Run is { } interlude)
        {
            if (!fastForward && _clockMs - _fieldStartMs < interlude.LengthMs)
                return false;
            if (interlude.LastFieldLine is { } kept)
                P1LastLine = kept;
            P1Banner = null;
            var then = _p1Then;
            var ends = _p1EndsFight;
            EndField();
            if (ends)
            {
                Phase = CombatPhase.Idle;
                return true;
            }
            then?.Invoke(session);
            return !IsPresenting;
        }

        if ((FieldRun?.LengthMs ?? P1Run?.LengthMs) is { } fieldLength && _fieldRound is { } fieldRound)
        {
            if (fastForward || _clockMs - _fieldStartMs >= fieldLength)
            {
                _fieldRound = null; // the lines and the flashes among them are over (s120: the Amiga flashes mid-round)
                if (P1Run?.LastFieldLine is { } kept)
                    P1LastLine = kept; // stays until the next round's set-up (h53 +0x2fc)
                P1Banner = null;
                if (fieldRound.EncounterOver)
                {
                    EndField();
                    Phase = CombatPhase.Idle;
                    return true;
                }
                EndField();
                BeginRound(session);
                return !IsPresenting;
            }
            return false;
        }

        if (P2Run is { } p2)
        {
            if (fastForward)
                p2.Finish();
            else
                p2.Advance(elapsedMs);
            PushP2Lines(p2);
            if (!p2.Done)
                return false;
            return _p2Round is null ? EndP2Interlude(session) : EndP2Round(session);
        }

        if (fastForward)
        {
            EndField();
            EndFlashes();
            var result = session.FightToEnd();
            DrainLog(session);
            if (result.PartyWon)
            {
                LastLine = UiText.S("combat.victory");
                Push(LastLine);
            }
            Phase = CombatPhase.Idle;
            _orderMember = -1;
            return true;
        }

        if (Phase != CombatPhase.Resolving)
        {
            LastLine = Prompt(session);
            return false;
        }

        if (_holdForFlashes)
        {
            // s110: the round's spell flashes play out before the next round's menu (the Amiga flashes inside the round).
            if (FlashRun is { } run && FlashVbl < run.LengthVbl)
                return false;
            EndFlashes();
            EndField();
            _accumMs = 0;
            BeginRound(session);
            return !IsPresenting;
        }

        var onField = AmigaField?.Invoke() == true;
        var onP1Field = !onField && OnP1Field;
        var p2Lines = onField || onP1Field ? null : P2Lines?.Invoke();
        // T)IME LAG (D-073); the Amiga's field has no such pause, its Speed paces the lines instead (II ST: its Speed menu,
        // manual: no Time lag for Atari users).
        var delay = onField || onP1Field || p2Lines is not null ? 0 : ClientSettings.RoundDelayMs(_settings.TimeLag);
        _accumMs += elapsedMs;
        if (_accumMs < delay)
            return false;

        _accumMs = 0;
        var living = session.Party.Members.Where(m => m.Status != CharacterStatus.Dead).ToList();
        var snapshot = onField && session.Combat is { } before ? CombatFieldSnapshot.Of(session.Party, before) : null;
        var p1Snapshot = onP1Field && session.Combat is { } p1Before ? P1CombatFieldSnapshot.Of(session.Party, p1Before) : null;
        _p2RanksBefore = p2Lines is not null && session.Combat is { } p2Before
            ? p2Before.Ranks.Select(r => (IReadOnlyList<Monster>)r.Members.Where(m => m.IsAlive).ToList()).ToList()
            : null;
        var mark = _logMark;
        var round = session.ResolveCombatRound();
        if (p1Snapshot is not null)
        {
            StartP1Field(session, p1Snapshot, round, mark);
            DrainLog(session);
            if (round.EncounterOver && round.PartyWon)
            {
                LastLine = UiText.S("combat.victory");
                Push(LastLine);
            }
            return false;
        }
        if (snapshot is not null)
        {
            StartField(session, snapshot, round, mark);
            DrainLog(session);
            if (round.EncounterOver && round.PartyWon)
            {
                LastLine = UiText.S("combat.victory");
                Push(LastLine);
            }
            return false;
        }
        if (p2Lines is not null)
        {
            StartP2Run(session, p2Lines, round, living);
            return false;
        }
        DrainLog(session);
        StartFlashes(round.Hits);
        foreach (var m in living.Where(m => m.Status == CharacterStatus.Dead))
            _deathMarks[m] = _clockMs + DeathMarkMs; // "%s DIES" (ST 0x9894 → 0x188fc mode 0)
        if (round.EncounterOver)
        {
            Phase = CombatPhase.Idle;
            if (round.PartyWon)
            {
                LastLine = UiText.S("combat.victory");
                Push(LastLine);
            }
            return true;
        }

        if (FlashRun is not null)
        {
            _holdForFlashes = true;
            return false;
        }
        BeginRound(session);
        return !IsPresenting;
    }

    /// <summary>
    /// Feed one key press. Returns true if it moved the presenter to another phase (the caller should drop the rest of
    /// this frame's keys so one press isn't read twice).
    /// </summary>
    public bool HandleKey(GameSession session, Keys key)
    {
        if (session.Combat is null)
            return false;
        CutP1Black(); // a key ends sound 17's black; the set-ups still run (s138)
        var before = Phase;
        if (key is Keys.PageUp or Keys.PageDown)
        {
            LogScroll.HandleKey(key); // the log scrolls back and forth; no command uses these keys
            return false;
        }
        if (key is Keys.Home or Keys.End)
        {
            RankScroll.ScrollBy(key == Keys.Home ? -Math.Max(1, RankScroll.Visible - 1) : Math.Max(1, RankScroll.Visible - 1));
            return false;
        }
        if (key is Keys.Up or Keys.Down && SpellDigits.Length == 0 && AwaitingOrders && _step == OrderStep.SpellNumber && !_amiga
            && !(_p1Orders && P1SpellLists))
        {
            MenuScroll.HandleKey(key); // the spell list has no cursor: the arrows scroll it
            return false;
        }
        if (Phase == CombatPhase.PartyOptions)
            HandleOptionKey(session, key);
        else if (AwaitingOrders)
            HandleOrderKey(session, key);
        return Phase != before;
    }

    // --- Round top: morale check + PARTY OPTIONS (OV2 0x3b70) --------------------------------------------------------

    private void BeginRound(GameSession session)
    {
        if (session.Mode != GameMode.Combat || session.Combat is null)
        {
            Phase = CombatPhase.Idle;
            return;
        }

        // Surprise opening (0x33ab → 5): no morale check, no options — the round runs at once (the repeat orders stay).
        if (!session.CombatRoundHasOptions)
        {
            Phase = CombatPhase.Resolving;
            return;
        }

        if (session.CombatOptionChosenThisRound) // e.g. a game loaded mid-orders
        {
            BeginOrders(session);
            return;
        }

        if (!session.CombatMoraleCheckedThisRound)
        {
            var p1Snapshot = OnP1FieldOnly && session.Combat is { } before ? P1CombatFieldSnapshot.Of(session.Party, before) : null;
            var mark = _logMark;
            var morale = session.StartCombatRound();
            var over = morale is MoraleOutcome.Ran or MoraleOutcome.EndedBySpell;
            if (StartP2Interlude(session, over, over ? null : BeginRound)) // II ST: the "MONSTERS RUN" box (0xa066) first
                return;
            DrainLog(session); // "MONSTERS RUN", and the XP/GP line if they got away
            if (StartP1Interlude(session, p1Snapshot, mark, over, BeginRound)) // I Amiga: requester 23 first (h44 +0x3a6)
                return;
            if (over)
            {
                Phase = CombatPhase.Idle;
                return;
            }
        }

        // DOS menus reopen on the last choice (s45, `PromptMenu`): the highlight is not reset between rounds or fights.
        Phase = CombatPhase.PartyOptions;
        _amigaParty = AmigaField?.Invoke() == true; // III Amiga: the field with its menu bar (s119)
        _p1Party = !_amigaParty && OnP1Field; // I Amiga: the field with its menu strip (s132)
        _amigaParty |= _p1Party;
        FieldMenuOpen = null;
        FieldWindow = AmigaFieldWindow.None;
        LastLine = Prompt(session);
    }

    private void HandleOptionKey(GameSession session, Keys key)
    {
        if (FieldWindow != AmigaFieldWindow.None)
        {
            FieldWindow = AmigaFieldWindow.None; // 0x1e8c4(1): any key closes it and is used up; back to the bar (0x274a)
            LastLine = Prompt(session);
            return;
        }
        if (InfoBox.Count > 0)
        {
            InfoBox = []; // any key closes the box; back to the menu (DOS 0xe3c4 waits for a key)
            LastLine = Prompt(session);
            return;
        }
        if (_amigaParty)
        {
            HandleFieldMenuKey(session, key);
            if (Phase == CombatPhase.PartyOptions)
                LastLine = Prompt(session);
            return;
        }
        var count = CombatText.PartyOptionMenu.Count;
        switch (key)
        {
            case Keys.Up or Keys.Left:
                OptionCursor = (OptionCursor + count - 1) % count;
                break;
            case Keys.Down or Keys.Right:
                OptionCursor = (OptionCursor + 1) % count;
                break;
            case Keys.Enter or Keys.Space:
                if (CombatText.PartyOptionMenu[OptionCursor].Option is { } option)
                {
                    ChooseOption(session, option);
                    return;
                }
                OpenInfoBox(OptionCursor, session); // Show names / Party health: the round is not used
                break;
        }
        LastLine = Prompt(session);
    }

    private void ChooseOption(GameSession session, PartyOption option)
    {
        var p1Snapshot = OnP1FieldOnly && session.Combat is { } before ? P1CombatFieldSnapshot.Of(session.Party, before) : null;
        var mark = _logMark;
        var result = session.ChoosePartyOption(option);
        var ends = result.Outcome == PartyOptionOutcome.CombatOver;
        if (StartP2Interlude(session, ends, ends ? null : s => AfterOption(s, result.Outcome))) // II ST: the answer's box
            return;
        DrainLog(session); // `core.combat.monsters_dont_respond` / `core.combat.monsters_surrender` / … and the XP/GP line
        // I Amiga: the answer in requester 23 (h44 +0x1c8 … +0x2a8, +0x736), then on as below.
        if (StartP1Interlude(session, p1Snapshot, mark, result.Outcome == PartyOptionOutcome.CombatOver,
                s => AfterOption(s, result.Outcome)))
            return;
        AfterOption(session, result.Outcome);
    }

    private void AfterOption(GameSession session, PartyOptionOutcome outcome)
    {
        switch (outcome)
        {
            case PartyOptionOutcome.CombatOver:
                Phase = CombatPhase.Idle;
                break;
            case PartyOptionOutcome.LoseRound:
                // Failed FLEE: the round runs without orders (0x3d84); the repeat orders stay ([D✓] s65).
                Phase = CombatPhase.Resolving;
                _accumMs = 0;
                break;
            default:
                BeginOrders(session);
                break;
        }
    }

    // --- Orders (OV2 0x6a2a) ------------------------------------------------------------------------------------------

    /// <summary>
    /// Every listed (status-OK) member gets the repeat order first (chooser mode 0, which re-rolls PARRY and recomputes
    /// SLASH); a member who isn't listed keeps action 4 and so repeats PARRY next time. Then each is asked in turn.
    /// </summary>
    private void BeginOrders(GameSession session)
    {
        Phase = CombatPhase.Orders;
        _padSerial++; // a new round's orders: the pad's pick starts again (CombatPresenter.Pad.cs)
        CastNotice = "";
        var members = session.Party.Members;
        var ranks = LivingRanks(session);
        for (var i = 0; i < members.Count && i < MaxMembers; i++)
        {
            if (!IsOrderable(members[i]))
            {
                _lastPacked[i] = CombatAction.Cleared;
                continue;
            }
            var repeat = CombatText.RepeatFor(_lastPacked[i], members[i].Class, ranks);
            _lastPacked[i] = session.ChooseAction(i, repeat.Choice, repeat.RankIndex, repeat.SpellIndex,
                repeat.HealTargetIndex);
        }
        _amiga = AmigaOrders?.Invoke() == true;
        _p1Orders = !_amiga && OnP1Field;
        if (_amiga || _p1Orders)
        {
            // The Amiga's screen shows every member with the order already set; nobody is asked until picked (0x188e4).
            _step = OrderStep.Menu;
            _orderMember = -1;
            LastLine = Prompt(session);
            return;
        }
        RestartOrderList(session);
    }

    private void RestartOrderList(GameSession session)
    {
        _rankLit = 0; // Backspace out of DOS III's rank menu: the next question lights One again
        _step = OrderStep.Menu;
        _orderMember = NextOrderable(session, -1);
        if (_orderMember < 0)
            ToResolving();
        else
            LastLine = Prompt(session);
    }

    private void HandleOrderKey(GameSession session, Keys key)
    {
        if (_amiga && CastNotice.Length > 0)
        {
            // III Amiga's message box (window 7, 0xaaac): any key or OK closes it and does nothing else (0x3ba0 with key 0).
            CastNotice = "";
            LastLine = Prompt(session);
            return;
        }
        CastNotice = ""; // a refused cast's message stays until the next key
        if (_amiga && HandleAmigaOrderKey(session, key))
        {
            LastLine = Prompt(session);
            return;
        }
        if (_p1Orders && HandleP1OrderKey(session, ref key))
        {
            LastLine = Prompt(session);
            return;
        }
        if (key == Keys.Back)
        {
            RestartOrderList(session); // walk the list again; orders given so far stay
            return;
        }

        var digit = key switch
        {
            >= Keys.D0 and <= Keys.D9 => key - Keys.D0,
            >= Keys.NumPad0 and <= Keys.NumPad9 => key - Keys.NumPad0,
            _ => -1,
        };
        var ch = session.Party.Members[_orderMember];
        if (_step == OrderStep.Rank && AsksDosRankMenu(session))
        {
            HandleDosRankKey(session, key, digit);
            return;
        }

        switch (_step)
        {
            case OrderStep.Menu when key == Keys.Enter:
                Advance(session); // keep the current order
                break;
            case OrderStep.Menu when digit >= 1 && digit <= MenuChoices(session).Count:
                _choice = MenuChoices(session)[digit - 1];
                var asksRank = session.Combat?.Modules.AsksRank(session.Combat, _orderMember, _choice);
                if (_choice == CombatChoice.ChangeRank)
                    _step = OrderStep.PartyRank;
                else if (_choice == CombatChoice.Cast && session.Game == GameId.P3
                         && P3CombatModule.CastRefusalBeforeList(ch) is { } refused)
                    RefuseBeforeList(session, refused); // DOS 0xffef / 0xffdf: no spells or no magic: message, Thrust (R1)
                else if (_choice == CombatChoice.Cast)
                {
                    _spellDigits = "";
                    _step = OrderStep.SpellNumber;
                }
                else if (asksRank ?? (ch.Class == CharacterClass.Thief && LivingRanks(session) > 1
                         && _choice is CombatChoice.Thrust or CombatChoice.Attack or CombatChoice.Slash))
                    _step = OrderStep.Rank; // III: the module says (Thieves, Fire Bow); I: a Thief with two ranks
                else if (_choice == CombatChoice.Toss && LivingRanks(session) >= 1)
                    _step = OrderStep.Rank; // II pick_rank 0x0f14c: TOSS always asks, any class ([~] with one rank too)
                else
                    Commit(session, rank: 0);
                break;
            case OrderStep.Rank when digit >= 1 && digit <= LivingRanks(session):
                Commit(session, rank: digit - 1);
                break;
            case OrderStep.PartyRank when digit is >= 1 and <= 3:
                session.ChangeCombatRank(_orderMember, (P3PartyRank)(3 - digit)); // 1 Front (2) … 3 Back (0), as 2 − choice
                _step = OrderStep.Menu; // the member still picks an order
                break;
            case OrderStep.SpellNumber when digit >= 0 && _spellDigits.Length < 2:
                _spellDigits += digit;
                break;
            case OrderStep.SpellNumber when key == Keys.Enter && _spellDigits.Length > 0:
                _spellIndex = int.Parse(_spellDigits) - 1; // the manual numbers spells from #1 (= Core index + 1)
                if (session.Game == GameId.P3 && (!Magic.KnowsSpell(ch, _spellIndex)
                        || P3CombatModule.CastRefusal(ch, _spellIndex, null, session.Options) is not null))
                {
                    // III (s128, root 0xfc41): a refused spell is DOS's message and Thrust (R1), which Core packs.
                    Refuse(P3CombatModule.CastRefusal(ch, _spellIndex, null, session.Options));
                    Commit(session, rank: 0);
                }
                else if (!CombatText.CombatSpellChoices(ch, session.Options).Contains(_spellIndex))
                {
                    // Not on the list (unknown, or a non-combat spell, D-035): DOS "invalid/unusable → beep, then the
                    // PARRY path" (0x6d75); no MP is spent.
                    _choice = CombatChoice.Parry;
                    Commit(session, rank: 0);
                }
                else if (_spellIndex is >= 0 and <= 3)
                    _step = OrderStep.HealTarget;
                else
                    Commit(session, rank: 0);
                break;
            case OrderStep.HealTarget when digit >= 1 && digit <= session.Party.Members.Count:
                if (session.Game == GameId.P3)
                    Refuse(P3CombatModule.CastRefusal(ch, _spellIndex, session.Party.Members[digit - 1], session.Options));
                Commit(session, rank: 0, healTarget: digit - 1);
                break;
        }

        LastLine = Prompt(session);
    }

    private int _rankLit; // DOS III's rank menu: 0 = One lit, 1 = Two lit

    /// <summary>
    /// s156: III's rank question off the Amiga screens is DOS's (root <c>0x10cf3</c> → <c>0x10d58</c>): `p3combat.x_target_which_rank`
    /// in the Yes / No menu (<c>0x1820</c>) with the words One / Two, One lit; Y answers One, N and Esc Two (word 2), Return
    /// the lit word (dungeon-events.md 2). With two living ranks only (one rank: no question, Core's rule).
    /// </summary>
    private bool AsksDosRankMenu(GameSession session) =>
        session.Game == GameId.P3 && !_amiga && !_p1Orders && LivingRanks(session) == 2;

    /// <summary>True while III's DOS rank menu waits (the front end sends it Esc, which answers Two).</summary>
    public bool AsksDosRank(GameSession session) => AwaitingOrders && _step == OrderStep.Rank && _orderMember >= 0
        && AsksDosRankMenu(session);

    /// <summary>
    /// A key on DOS III's rank menu: the arrows move the light between One and Two, Return / Space take it; Y or 1 is One,
    /// N, 2 or Esc is Two (the digits are the remaster's, and give the line its click targets). Backspace walks the order
    /// list again before this is reached (the remaster's way back).
    /// </summary>
    private void HandleDosRankKey(GameSession session, Keys key, int digit)
    {
        switch (key)
        {
            case Keys.Left or Keys.Right or Keys.Up or Keys.Down:
                _rankLit = 1 - _rankLit;
                LastLine = Prompt(session);
                return;
            case Keys.Enter or Keys.Space:
                Commit(session, rank: _rankLit);
                return;
            case Keys.Y:
                Commit(session, rank: 0);
                return;
            case Keys.N or Keys.Escape:
                Commit(session, rank: 1);
                return;
        }
        if (digit is 1 or 2)
            Commit(session, rank: digit - 1);
    }

    private void Commit(GameSession session, int rank, int healTarget = 0)
    {
        _rankLit = 0; // the next rank question lights One again (0x10d58's lit word 1)
        var spell = _choice == CombatChoice.Cast ? _spellIndex : -1;
        _lastPacked[_orderMember] = session.ChooseAction(_orderMember, _choice, rank, spell, healTarget);
        Advance(session);
    }

    private void Advance(GameSession session)
    {
        _step = OrderStep.Menu;
        if (_amiga || _p1Orders)
        {
            _orderMember = -1; // back to the member boxes (0x18c20 → 0x1894c); I: back to requester 21's list
            return;
        }
        _orderMember = NextOrderable(session, _orderMember);
        if (_orderMember < 0)
            ToResolving();
    }

    private void ToResolving()
    {
        _amiga = _p1Orders = false;
        _orderMember = -1;
        Phase = CombatPhase.Resolving;
        _accumMs = 0;
    }

    private static int NextOrderable(GameSession session, int after)
    {
        var members = session.Party.Members;
        for (var i = after + 1; i < members.Count && i < MaxMembers; i++)
        {
            if (IsOrderable(members[i]))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// The option keys of the Amiga's order list (hit table <c>0x21fdc</c>, switch <c>0x18b02</c>): A Attack, L Lunge,
    /// S Slash, T Thrust, P Parry, B Fire Bow, M Aim Blow, C Cast Spell; 1 / 2 / 3 Move Back / Middle / Front (rows 0 / 1 / 2)
    /// stand for <see cref="CombatChoice.ChangeRank"/>.
    /// </summary>
    public static readonly IReadOnlyList<(Keys Key, CombatChoice Choice)> AmigaOptionKeys =
    [
        (Keys.A, CombatChoice.Attack), (Keys.L, CombatChoice.Lunge), (Keys.S, CombatChoice.Slash),
        (Keys.T, CombatChoice.Thrust), (Keys.P, CombatChoice.Parry), (Keys.B, CombatChoice.FireBow),
        (Keys.M, CombatChoice.Aim), (Keys.C, CombatChoice.Cast),
    ];

    /// <summary>A member the Amiga lets the player pick: not dead, asleep or out (<c>0x189e0</c>).</summary>
    public static bool AmigaPickable(Character ch) =>
        IsOrderable(ch) && P3Fight.StatusOf(ch) is P3Status.Okay or P3Status.Down;

    /// <summary>
    /// A key on III Amiga's orders screen (s118, <c>0x188e4</c>). With no member picked: a digit 1–7 picks that member
    /// (the Amiga's boxes take only the mouse, hit table <c>0x21f9a</c>; the digits are the remaster's keyboard way), F
    /// fights. With a member picked: an option letter sets its order (<see cref="AmigaOptionKeys"/>); 1 / 2 / 3 move it
    /// to the Back / Middle / Front row at once (<c>0x193d6</c>; its order stays); F fights; any other letter or digit
    /// sets Thrust as the Amiga's default does (<c>0x18afa</c>); Return, Backspace or Esc go back with no change (the
    /// remaster's; the Amiga has no way back). The rank window: O / Return / Y = rank 1, C / Esc / N = rank 2. False =
    /// the shared code takes the key (the spell number and heal target steps).
    /// </summary>
    private bool HandleAmigaOrderKey(GameSession session, Keys key)
    {
        var members = session.Party.Members;
        var digit = key switch
        {
            >= Keys.D0 and <= Keys.D9 => key - Keys.D0,
            >= Keys.NumPad0 and <= Keys.NumPad9 => key - Keys.NumPad0,
            _ => -1,
        };
        if (_orderMember < 0)
        {
            if (key == Keys.F)
                ToResolving();
            else if (digit >= 1 && digit <= Math.Min(members.Count, MaxMembers) && AmigaPickable(members[digit - 1]))
            {
                _orderMember = digit - 1;
                _step = OrderStep.Menu;
            }
            return true;
        }
        switch (_step)
        {
            case OrderStep.Menu:
                if (key == Keys.F)
                {
                    ToResolving(); // F while asked fights too (0x18a88)
                    return true;
                }
                if (key is Keys.Enter or Keys.Back or Keys.Escape)
                {
                    _orderMember = -1;
                    return true;
                }
                if (digit is >= 1 and <= 3)
                {
                    session.ChangeCombatRank(_orderMember, (P3PartyRank)(digit - 1)); // '1' Back … '3' Front
                    _orderMember = -1;
                    return true;
                }
                var picked = AmigaOptionKeys.FirstOrDefault(o => o.Key == key);
                if (picked.Key == Keys.None)
                {
                    if (key is not (>= Keys.A and <= Keys.Z) && digit < 0)
                        return true; // no character: nothing happens
                    picked = (key, CombatChoice.Thrust); // the Amiga's default order (0x18afa)
                }
                if (!MenuChoices(session).Contains(picked.Choice))
                    return true;
                _choice = picked.Choice;
                if (_choice == CombatChoice.Cast
                    && P3CombatModule.CastRefusalBeforeList(members[_orderMember]) is { } refused)
                    RefuseBeforeList(session, refused); // 0x19430 / 0x1943a: window 7, no spell window, Thrust (R1)
                else if (_choice == CombatChoice.Cast)
                {
                    _spellDigits = "";
                    _step = OrderStep.SpellNumber;
                    _amigaFirst = _amigaCursor = 0;
                }
                else if (session.Combat?.Modules.AsksRank(session.Combat, _orderMember, _choice) == true)
                    _step = OrderStep.Rank; // the Amiga asks in window 20 (0x19806)
                else
                    Commit(session, rank: 0);
                return true;
            case OrderStep.Rank:
                if (key is Keys.O or Keys.Enter or Keys.Y)
                    Commit(session, rank: 0); // OK = rank 1
                else if (key is Keys.C or Keys.Escape or Keys.N)
                    Commit(session, rank: 1); // CANCEL = rank 2 (0x19878)
                return true;
            case OrderStep.SpellNumber or OrderStep.HealTarget:
                HandleAmigaListKey(session, key);
                return true;
            default:
                if (key is Keys.Back or Keys.Escape)
                {
                    _step = OrderStep.Menu;
                    _orderMember = -1;
                    return true;
                }
                return false;
        }
    }

    /// <summary>
    /// A key in III Amiga's spell window 11 (<c>0xf770</c> / <c>0xff82</c>) or heal window 6 (<c>0x127ce</c>): Up or '8'
    /// / Down or '2' move the cursor (wrapping on the page); M (and the MORE button) shows the next 12 spells, back to the
    /// first after the last, cursor on the top row; O / Return (OK) takes the cursor's row; C (CANCEL) sets Thrust
    /// (<c>0x19656</c>, <c>0x19572</c>); Esc / Backspace go back to the member boxes with no change (the remaster's).
    /// The spell taken then follows Core's (DOS III) rules (<see cref="P3CombatModule.CastRefusal"/>, root <c>0xfc41</c>, the
    /// Amiga's <c>0x19430</c> the same): a refused spell (not in combat, magic too low) or Healing on a Dead member →
    /// Thrust (R1) and the message (<see cref="CastNotice"/>); Healing I–IV → the heal window.
    /// </summary>
    private void HandleAmigaListKey(GameSession session, Keys key)
    {
        var spells = _step == OrderStep.SpellNumber;
        var rows = spells ? AmigaSpellPage(session).Count : session.Party.Members.Count;
        switch (key)
        {
            case Keys.Back or Keys.Escape:
                _step = OrderStep.Menu;
                _orderMember = -1;
                break;
            case Keys.Up or Keys.D8 or Keys.NumPad8 when rows > 0:
                _amigaCursor = _amigaCursor <= 0 ? rows - 1 : _amigaCursor - 1;
                break;
            case Keys.Down or Keys.D2 or Keys.NumPad2 when rows > 0:
                _amigaCursor = _amigaCursor >= rows - 1 ? 0 : _amigaCursor + 1;
                break;
            case Keys.M when spells:
                var all = AmigaSpellList(session).Count;
                _amigaFirst = Math.Min(_amigaFirst + AmigaSpellRows, all) >= all ? 0 : _amigaFirst + AmigaSpellRows;
                _amigaCursor = 0;
                break;
            case Keys.C:
                _choice = CombatChoice.Thrust; // CANCEL: the order becomes Thrust (0x19656 / 0x19572)
                Commit(session, rank: 0);
                break;
            case Keys.O or Keys.Enter when rows > 0:
                var at = Math.Clamp(_amigaCursor, 0, rows - 1);
                var caster = session.Party.Members[_orderMember];
                if (!spells)
                {
                    Refuse(P3CombatModule.CastRefusal(caster, _spellIndex, session.Party.Members[at], session.Options));
                    Commit(session, rank: 0, healTarget: at);
                    break;
                }
                _spellIndex = AmigaSpellPage(session)[at];
                if (P3CombatModule.CastRefusal(caster, _spellIndex, null, session.Options) is { } why)
                {
                    Refuse(why);
                    Commit(session, rank: 0); // Core makes the order Thrust (R1) (root 0xff55)
                }
                else if (_spellIndex is >= 0 and <= 3)
                {
                    _step = OrderStep.HealTarget;
                    _amigaCursor = 0;
                }
                else
                    Commit(session, rank: 0);
                break;
        }
    }

    /// <summary>
    /// The message of a refused III cast (root <c>0xfc41</c>): on the Amiga orders screen the message box, window 7
    /// (<c>0xaaac</c>, <see cref="AmigaMessageShown"/>), which the next key only closes; otherwise the feedback line until
    /// the next key. Empty when none.
    /// </summary>
    public string CastNotice { get; private set; } = "";

    private void Refuse(string? textId)
    {
        if (textId is not null)
            CastNotice = GameText.S(textId);
    }

    /// <summary>
    /// III's Cast refused before the spell list (<see cref="P3CombatModule.CastRefusalBeforeList"/>): the message and the
    /// order Thrust (R1) with no rank question (DOS <c>0xff55</c>, Amiga <c>0x196ac</c>: action 3 stored directly).
    /// </summary>
    private void RefuseBeforeList(GameSession session, string textId)
    {
        Refuse(textId);
        _choice = CombatChoice.Cast;
        _spellIndex = -1; // no spell: Core packs Thrust (R1) (P3CombatModule.TryChoose, root 0xff55)
        Commit(session, rank: 0);
    }

    /// <summary>
    /// III Amiga's message box (window 7, <c>0xaaac</c>) is up on the orders screen: a refused cast's message, closed by
    /// the next key or OK (s134).
    /// </summary>
    public bool AmigaMessageShown => AmigaOrdersShown && CastNotice.Length > 0;

    /// <summary>
    /// The spells III Amiga's window 11 lists for the picked member: every spell it knows (the record's list, which
    /// <c>0xd08c</c> sorts highest first), as Core indices; under <c>p3.combat-spell-list</c> = <c>manual</c> (D-063) only
    /// the combat and everywhere spells.
    /// </summary>
    public IReadOnlyList<int> AmigaSpellList(GameSession session)
    {
        if (_orderMember < 0 || _orderMember >= session.Party.Members.Count)
            return [];
        var ch = session.Party.Members[_orderMember];
        return Enumerable.Range(0, Math.Min(SpellSlots.For(session.Options).Count, ch.SpellsKnown.Length))
            .Where(id => Magic.KnowsSpell(ch, id) && CombatText.IsCombatCastable(id, session.Options)) // D-063: p3.combat-spell-list
            .OrderByDescending(id => id).ToList();
    }

    /// <summary>The spells on the window's current page (up to <see cref="AmigaSpellRows"/>).</summary>
    public IReadOnlyList<int> AmigaSpellPage(GameSession session) =>
        AmigaSpellList(session).Skip(_amigaFirst).Take(AmigaSpellRows).ToList();

    /// <summary>The spell window is up (III Amiga orders screen, Cast picked).</summary>
    public bool AsksAmigaSpell => AmigaOrdersShown && _orderMember >= 0 && _step == OrderStep.SpellNumber;

    /// <summary>The heal window is up (III Amiga orders screen, a Healing spell picked).</summary>
    public bool AsksAmigaHeal => AmigaOrdersShown && _orderMember >= 0 && _step == OrderStep.HealTarget;

    /// <summary>The cursor row in the spell window's page or the heal window.</summary>
    public int AmigaListCursor => _amigaCursor;

    /// <summary>A press on row <paramref name="row"/> of the spell / heal window moves the cursor there (<c>0x17a6</c>).</summary>
    public void AmigaPickRow(GameSession session, int row)
    {
        var rows = AsksAmigaSpell ? AmigaSpellPage(session).Count : AsksAmigaHeal ? session.Party.Members.Count : 0;
        if (row >= 0 && row < rows)
            _amigaCursor = row;
    }

    /// <summary>"WHICH RANK?" is asked: a rank number is the next key.</summary>
    public bool AsksRank => AwaitingOrders && _step == OrderStep.Rank;

    /// <summary>"HEAL WHOM?" is asked: a member number is the next key.</summary>
    public bool AsksHealTarget => AwaitingOrders && _step == OrderStep.HealTarget;

    /// <summary>Digits typed so far at "WHICH SPELL?" (empty when not asking).</summary>
    public string SpellDigits => AwaitingOrders && _step == OrderStep.SpellNumber ? _spellDigits : "";

    /// <summary>
    /// The spell list shown while "WHICH SPELL?" is asked: the member's combat-castable spells
    /// (<see cref="CombatText.CombatSpellChoices"/>) whose number starts with the digits typed so far; empty otherwise.
    /// </summary>
    public IReadOnlyList<int> SpellChoices(GameSession session)
    {
        if (!AwaitingOrders || _step != OrderStep.SpellNumber)
            return [];
        return CombatText.CombatSpellChoices(session.Party.Members[_orderMember], session.Options)
            .Where(id => (id + 1).ToString().StartsWith(_spellDigits, StringComparison.Ordinal))
            .ToList();
    }

    private static int LivingRanks(GameSession session) =>
        session.Combat?.Ranks.Count(r => !r.IsWiped) ?? 0;

    private string Prompt(GameSession session)
    {
        if (Phase == CombatPhase.PartyOptions && FieldWindow != AmigaFieldWindow.None)
            return UiText.S("combat.press_any_key");
        if (Phase == CombatPhase.PartyOptions && _amigaParty && InfoBox.Count == 0)
            return _p1Party ? UiText.S("p1field.help") : UiText.S("p3field.help");
        if (Phase == CombatPhase.PartyOptions)
            return InfoBox.Count > 0 ? UiText.S("combat.press_any_key")
                : UiText.F("combat.party_options_x_arrows_enter", CombatText.PartyOptionMenu[OptionCursor].Label);
        if (!AwaitingOrders)
            return Phase == CombatPhase.Resolving ? UiText.S("combat.fighting") : LastLine;
        if (AwaitingOrders && CastNotice.Length > 0)
            return CastNotice;
        if (_amiga && (_orderMember < 0 || _step is OrderStep.Menu or OrderStep.Rank))
            return _step == OrderStep.Rank && _orderMember >= 0 ? UiText.S("p3combat.rank_help") : UiText.S("p3combat.orders_help");
        if (_amiga && _step is OrderStep.SpellNumber or OrderStep.HealTarget)
            return _step == OrderStep.SpellNumber ? UiText.S("p3combat.spell_help") : UiText.S("p3combat.heal_help");
        if (_p1Orders && _orderMember < 0)
            return UiText.S("p1field.orders_help");
        if (_p1Orders && _step == OrderStep.Menu)
            return UiText.S("p1field.order_help");
        if (_p1Orders && P1SpellLists && _step is OrderStep.SpellNumber or OrderStep.HealTarget)
            return UiText.S(_step == OrderStep.SpellNumber ? "p1field.spell_help" : "p1field.target_help");

        var name = session.Party.Members[_orderMember].Name;
        return _step switch
        {
            OrderStep.Rank when AsksDosRankMenu(session) => UiText.F("p3combat.x_target_which_rank", name,
                _rankLit == 0 ? ">" : " ", _rankLit == 1 ? ">" : " "),
            OrderStep.Rank => UiText.F("combat.x_which_rank_1_x", name, LivingRanks(session)),
            OrderStep.SpellNumber => UiText.F("combat.x_spell_x_enter", name, _spellDigits),
            OrderStep.HealTarget => UiText.F("combat.x_heal_whom_1_x", name, session.Party.Members.Count),
            OrderStep.PartyRank => UiText.F("combat.x_rank_front_middle_back", name),
            _ => UiText.F("combat.x_x_orders_x_enter_keep", name, OrderLabelFor(session, _orderMember),
                CombatText.OrderMenu(MenuChoices(session))),
        };
    }

    /// <summary>
    /// The order menu (Phase 09 T1.6): the fight's feature modules' <see cref="FeatureModuleSet.CombatChoices"/>, numbered
    /// from 1 in that order. Phantasie I: 1 THRUST 2 ATTACK 3 SLASH 4 LUNGE 5 CAST 6 PARRY.
    /// </summary>
    public static IReadOnlyList<CombatChoice> MenuChoices(GameSession session) =>
        (session.Combat?.Modules ?? session.FeatureModules).CombatChoices;

    /// <summary>Preview party #0 thrust vs rank-0 monster to-hit % (tooltip).</summary>
    public static string FormatHitTooltip(GameSession session)
    {
        if (session.Combat is null || session.Party.Members.Count == 0)
            return "";

        var enc = session.Combat;
        if (enc.Prep.Length == 0)
            enc.Prepare(fightStart: false);

        var mon = enc.Ranks.FirstOrDefault()?.Living.FirstOrDefault();
        if (mon is null)
            return UiText.S("combat.no_living_foes");

        var prep = enc.Prep[0];
        var action = CombatAction.Thrust(0);
        var (swings, _) = CombatAction.Decode(action);
        var chance = CombatMath.PartyMeleeHitChance(prep.WeaponHitEff, swings, mon.Defense);
        var raw = CombatMath.PartyToMonsterDamage(50, prep.WeaponDamageEff, mon.Armor, action);
        var dmg = CombatMath.ApplyDamageFloor(raw);
        return UiText.F("combat.x_vs_x_hit_x_dmg", session.Party.Members[0].Name, mon.Name, chance, dmg);
    }

    /// <summary>
    /// The order letters of I Amiga's requester 24 (h63: THRUST, ATTACK, SLASH, LUNGE, CAST, PARRY), the remaster's keys for
    /// its buttons, as menu digits 1–6.
    /// </summary>
    public static readonly IReadOnlyList<(Keys Key, CombatChoice Choice)> P1OrderKeys =
    [
        (Keys.T, CombatChoice.Thrust), (Keys.A, CombatChoice.Attack), (Keys.S, CombatChoice.Slash),
        (Keys.L, CombatChoice.Lunge), (Keys.C, CombatChoice.Cast), (Keys.P, CombatChoice.Parry),
    ];

    /// <summary>
    /// A key on I Amiga's orders requester (s132, h71 <c>+0x90</c>). With the list up: a digit 1–7 picks that row (the
    /// Amiga takes a click on the row; the digits are the remaster's), F or Return is the FIGHT button. With a member
    /// picked: a digit 1–6 or a letter of <see cref="P1OrderKeys"/> is a button of requester 24; Return keeps the order
    /// (the remaster's), Esc / Backspace go back to the list. The rank, spell and target steps take DOS's digits (the
    /// shared code). <paramref name="key"/> is rewritten for the shared code (a letter becomes its digit); true = used up.
    /// </summary>
    private bool HandleP1OrderKey(GameSession session, ref Keys key)
    {
        if (_p1ToneThen is not null)
            return true; // the tone blocks (h89 +0x304); requester 12 takes no keys anyway
        var digit = key switch
        {
            >= Keys.D0 and <= Keys.D9 => key - Keys.D0,
            >= Keys.NumPad0 and <= Keys.NumPad9 => key - Keys.NumPad0,
            _ => -1,
        };
        if (_orderMember < 0)
        {
            var rows = P1OrderRows(session);
            if (key is Keys.F or Keys.Enter)
                ToResolving(); // FIGHT (gadget 1)
            else if (digit >= 1 && digit <= rows.Count)
            {
                _orderMember = rows[digit - 1];
                _step = OrderStep.Menu;
            }
            return true;
        }
        if (key is Keys.Escape or Keys.Back)
        {
            _step = OrderStep.Menu;
            _orderMember = -1;
            return true;
        }
        var pressed = key;
        if (_step == OrderStep.Menu && P1OrderKeys.FirstOrDefault(o => o.Key == pressed) is { Key: not Keys.None } picked)
        {
            var index = MenuChoices(session).ToList().IndexOf(picked.Choice);
            if (index < 0)
                return true;
            digit = 1 + index;
            key = Keys.D1 + index;
        }
        if (!P1SpellLists)
            return false; // no Cast requesters in the pack: requester 26 takes the typed number (the shared code)
        var choices = MenuChoices(session);
        if (_step == OrderStep.Menu && digit >= 1 && digit <= choices.Count && choices[digit - 1] == CombatChoice.Cast)
        {
            // h71 +0x73a: the list opens on its first page; with no spell to list it does not open (+0x570 → −1): PARRY.
            _choice = CombatChoice.Cast;
            P1SpellFirst = 0;
            if (P1SpellList(session).Count == 0)
                P1Exit(session);
            else
                _step = OrderStep.SpellNumber;
            return true;
        }
        if (_step == OrderStep.SpellNumber)
        {
            HandleP1SpellKey(session, key, digit);
            return true;
        }
        if (_step == OrderStep.HealTarget)
        {
            if (key == Keys.E)
                P1Exit(session);
            else if (digit >= 1 && digit <= Math.Min(session.Party.Members.Count, P1Layout!.SpellList!.Target.Rows))
                Commit(session, rank: 0, healTarget: digit - 1); // row k = member k (+0x28e mode 4: every member, DEAD too)
            return true;
        }
        return false;
    }

    /// <summary>
    /// A key on I Amiga's spell list (requester 12, h23 <c>+0x570</c>; the Amiga takes only clicks, gadget-up): a digit 1–6
    /// is that row of the page (the remaster's keys for the clicks), Up / Down the scroll gadgets 2 / 3 (a page of 6 back /
    /// on; nothing while ghosted), E the EXIT gadget (→ PARRY, h71 <c>+0x936</c>). A pick (h71 <c>+0x7d0</c>): a spell
    /// whose cost (slot % 4 + 1) is above the member's MP → PARRY (<c>+0x82c</c>, as Core's chooser); HEALING I–IV → "ON
    /// WHOM:" (requester 6); any other → the order.
    /// </summary>
    private void HandleP1SpellKey(GameSession session, Keys key, int digit)
    {
        var page = P1SpellPage(session);
        if (key == Keys.E)
            P1Tone(() => P1Exit(session));
        else if (key == Keys.Up && P1SpellCanScrollUp)
            P1Tone(() => P1SpellFirst -= Math.Min(P1SpellFirst, P1SpellRows));
        else if (key == Keys.Down && P1SpellCanScrollDown(session))
            P1Tone(() => P1SpellFirst += P1SpellRows);
        else if (digit >= 1 && digit <= page.Count)
        {
            var spell = page[digit - 1];
            P1Tone(() =>
            {
                _spellIndex = spell;
                var caster = session.Party.Members[_orderMember];
                if (!Magic.CanAfford(caster, _spellIndex, SpellSlots.For(session.Options)))
                    P1Exit(session);
                else if (_spellIndex is >= 0 and <= 3)
                    _step = OrderStep.HealTarget;
                else
                    Commit(session, rank: 0);
            });
        }
        // A row past the list (inert, no GADGIMMEDIATE) and a ghosted scroll gadget give no event: no tone, no change.
    }

    /// <summary>Counts requester 12's tones (s134), so the front end plays each once (<see cref="P1ToneSound"/>).</summary>
    public int P1ToneSerial { get; private set; }

    /// <summary>The sound requester 12 plays after each gadget event (the pack's <c>spell_list.spells.tone</c>), or null.</summary>
    public int? P1ToneSound => P1Layout?.SpellList?.Spells.Tone;

    /// <summary>True while requester 12's tone holds the screen: the event's result waits for it, keys are not taken.</summary>
    public bool P1ToneHolding => _p1ToneThen is not null;

    private Action? _p1ToneThen;
    private double _p1ToneUntilMs;

    /// <summary>
    /// Requester 12's gadget event (s134, h23 <c>+0x734</c>): h86 <c>+0x132</c> returns, h89 <c>+0xd2</c> plays the tone (h90
    /// <c>+5</c>, 3 ticks; the player blocks), and only then does the code act on the event (the page redrawn, the requester
    /// closed, "ON WHOM:" opened). <paramref name="then"/> runs when the tone's hold (<see cref="P1SoundSeconds"/>) is over: at
    /// once with the Sound switch off (no sound waits, user s129) or a pack without the tone.
    /// </summary>
    private void P1Tone(Action then)
    {
        if (P1ToneSound is not int tone)
        {
            then();
            return;
        }
        P1ToneSerial++;
        var holdMs = Math.Max(0, P1SoundSeconds?.Invoke(tone) ?? 0) * 1000;
        if (holdMs <= 0)
        {
            then();
            return;
        }
        _p1ToneThen = then;
        _p1ToneUntilMs = _clockMs + holdMs;
    }

    /// <summary>Run the held gadget event once its tone is over (<see cref="P1Tone"/>).</summary>
    private void FinishP1Tone(GameSession session)
    {
        if (_p1ToneThen is not { } then || _clockMs < _p1ToneUntilMs)
            return;
        _p1ToneThen = null;
        then();
        if (session.Combat is not null && Phase == CombatPhase.Orders)
            LastLine = Prompt(session);
    }

    /// <summary>The spell list's or "ON WHOM:"'s EXIT, an empty list, a spell above the MP: the order becomes PARRY (h71 <c>+0x936</c>).</summary>
    private void P1Exit(GameSession session)
    {
        _choice = CombatChoice.Parry;
        Commit(session, rank: 0);
    }

    /// <summary>
    /// Put the round just resolved on I Amiga's field (s132): its lines in the Amiga's order with their sounds and waits
    /// (<see cref="P1CombatFieldRun"/>), paced by <see cref="P1Speed"/>.
    /// </summary>
    private void StartP1Field(GameSession session, P1CombatFieldSnapshot snapshot, CombatResult round, long mark)
    {
        P1Snapshot = snapshot;
        P1LastLine = null; // the round's set-up fills the field (h53 +0x2fc)
        // What the session noted after the round's lines (the fight's end: `core.combat.each_survivor_gets` …) follows them (requester 23).
        var scene = session.PendingRewardScene?.Lines;
        var log = round.Log.Concat(session.LogSince(mark).Skip(round.Log.Count).Where(l => scene is null || !scene.Contains(l)))
            .ToList();
        // A surprise opening (round 1 before Party Options): the fight's first set-up is this round's (h35 +0xe -> h71
        // +0x1050 -> h53 +0x274: BltClear, sound 17), s136.
        var lead = _p1SurpriseOpening && !_p1IntroOver ? P1IntroBlackMs(P1Layout!) : 0;
        var opens = _p1SurpriseOpening; // the BltClear happened even when a key cut the black (s138: bar and frame)
        _p1SurpriseOpening = false;
        _p1IntroOver = true;
        var layout = P1Layout!;
        var speed = P1Speed?.Invoke() ?? (int)FieldSpeed;
        var clock = P1ClockMs?.Invoke();
        var monsters = snapshot.Groups.SelectMany(g => g).ToList();
        P1CombatFieldRun Run(double black) => P1CombatFieldRun.Plan(layout, log, round.LineTags, snapshot.Row, monsters,
            m => !m.IsAlive, speed, P1SoundSeconds ?? (_ => 0), round.Hits, clockMs: clock, leadBlackMs: black, opensFight: opens);
        P1Run = Run(lead);
        _p1Replan = opens ? Run : null;
        _fieldRound = round;
        _fieldStartMs = _clockMs;
        P1RunSerial++;
    }

    /// <summary>
    /// Put the round just resolved on the field (s117): its lines with their tags, then what the session noted after them
    /// (and the victory line on a win), paced by <see cref="FieldSpeed"/>.
    /// </summary>
    private void StartField(GameSession session, CombatFieldSnapshot snapshot, CombatResult round, long mark)
    {
        var scene = session.PendingRewardScene?.Lines;
        var noted = session.LogSince(mark);
        var after = noted.Skip(round.Log.Count).Where(l => scene is null || !scene.Contains(l)).ToList();
        if (round.EncounterOver && round.PartyWon)
            after.Add(UiText.S("combat.victory"));
        var log = round.Log.Select(l => scene is not null && scene.Contains(l) ? "" : l).ToList();
        FieldSnapshot = snapshot;
        FieldRun = CombatFieldRun.Plan(log, round.LineTags, after, snapshot.Party, snapshot.DeadBefore,
            snapshot.Groups.SelectMany(g => g).ToList(), m => !m.IsAlive, FieldSpeed, PlanFlashes?.Invoke(round.Hits));
        _fieldRound = round;
        _fieldStartMs = _clockMs;
        FieldRunSerial++;
    }

    /// <summary>
    /// II ST (s136): play the round just resolved line by line (<see cref="P2CombatLineRun"/>): the lines the session noted
    /// since the last drain, with the victory line on a win, paced by the game's COMBAT SPEED.
    /// </summary>
    private void StartP2Run(GameSession session, StCombatLines table, CombatResult round, IReadOnlyList<Character> living)
    {
        var scene = session.PendingRewardScene?.Lines;
        var noted = session.LogSince(_logMark);
        // Core's tags count from the round's first line: find where it starts among the noted lines.
        var at = Enumerable.Range(0, Math.Max(0, noted.Count - round.Log.Count + 1))
            .FirstOrDefault(k => noted.Skip(k).Take(round.Log.Count).SequenceEqual(round.Log), -1);
        var tags = at < 0 ? [] : round.LineTags.Select(t => t with { Line = t.Line + at }).ToList();
        var hits = at < 0 ? [] : round.Hits.Select(h => h with { Line = h.Line + at }).ToList();
        var lines = noted.Select(l => scene is not null && scene.Contains(l) ? "" : l).ToList();
        _logMark = session.LogSequence;
        if (round.EncounterOver && round.PartyWon)
            lines.Add(UiText.S("combat.victory"));
        var entries = P2CombatLineRun.Classify(table, lines, tags, living, hits: hits);
        // s156: sound 18 ends every round (0x18416), before combat_victory's boxes; the sparkles before their damage lines.
        P2Run = new P2CombatLineRun(table, entries, (int)FieldSpeed, P2SoundSeconds ?? (_ => 0), roundEnds: true,
            hits: hits, roll: P2Roll);
        _p2Round = round;
        _p2Pushed = 0;
        PushP2Lines(P2Run);
    }

    private Action<GameSession>? _p2Then;
    private bool _p2EndsFight;

    /// <summary>
    /// II ST (s138, FORMAT.md 7.3a): when the lines noted since the last drain hold an end-of-fight box (a party option's
    /// answer, the morale check's "MONSTERS RUN", the XP / GP of a fight ended outside a round), play them through
    /// <see cref="P2CombatLineRun"/> (outside a round) before going on: <paramref name="endsFight"/> ends the encounter,
    /// else <paramref name="then"/> runs. False (nothing started) without such a line or without II's table.
    /// </summary>
    private bool StartP2Interlude(GameSession session, bool endsFight, Action<GameSession>? then)
    {
        if (P2Lines?.Invoke() is not { EndBoxes: not null } table || AmigaField?.Invoke() == true || OnP1Field)
            return false;
        var scene = session.PendingRewardScene?.Lines;
        var lines = session.LogSince(_logMark).Select(l => scene is not null && scene.Contains(l) ? "" : l).ToList();
        var entries = P2CombatLineRun.Classify(table, lines, [], session.Party.Members, outsideRound: true);
        if (!entries.Any(e => e.BoxKey is not null))
            return false;
        _logMark = session.LogSequence;
        P2Run = new P2CombatLineRun(table, entries, (int)FieldSpeed, P2SoundSeconds ?? (_ => 0));
        _p2Round = null;
        _p2Pushed = 0;
        _p2Then = then;
        _p2EndsFight = endsFight;
        Phase = CombatPhase.Resolving;
        PushP2Lines(P2Run);
        return true;
    }

    /// <summary>II's interlude (<see cref="StartP2Interlude"/>) has played out: the fight ends, or the step after it.</summary>
    private bool EndP2Interlude(GameSession session)
    {
        _p2PendingSounds.AddRange(P2Run?.TakeSounds() ?? []);
        P2Run = null;
        var then = _p2Then;
        var ends = _p2EndsFight;
        _p2Then = null;
        _p2EndsFight = false;
        if (ends)
        {
            Phase = CombatPhase.Idle;
            return true;
        }
        then?.Invoke(session);
        return !IsPresenting;
    }

    private void PushP2Lines(P2CombatLineRun run)
    {
        for (; _p2Pushed < run.Printed; _p2Pushed++)
        {
            Push(run.Entries[_p2Pushed].Text);
            LastLine = run.Entries[_p2Pushed].Text;
        }
    }

    /// <summary>II's round has played out: the fight ends, or the next round's menu.</summary>
    private bool EndP2Round(GameSession session)
    {
        var round = _p2Round!;
        _p2PendingSounds.AddRange(P2Run?.TakeSounds() ?? []);
        P2Run = null;
        _p2Round = null;
        if (round.EncounterOver)
        {
            Phase = CombatPhase.Idle;
            return true;
        }
        BeginRound(session);
        return !IsPresenting;
    }

    /// <summary>Copy every line the session noted since the last drain into the combat log.</summary>
    private void DrainLog(GameSession session)
    {
        var scene = session.PendingRewardScene?.Lines; // the reward scene tells these itself, after the fight (s102)
        foreach (var line in session.LogSince(_logMark))
            if (scene is null || !scene.Contains(line))
                Push(line);
        _logMark = session.LogSequence;
        if (_log.Count > 0)
            LastLine = _log[^1];
    }

    private void Push(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        _log.Add(line);
        if (_log.Count > 40)
            _log.RemoveAt(0);
    }
}
