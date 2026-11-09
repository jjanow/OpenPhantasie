namespace OpenPhantasie.Core;

/// <summary>
/// High-level play state for a single campaign. Wires M1–M7 subsystems into
/// one mode machine so M8 can drive a start-to-finish playthrough headless
/// (tests) or from <c>OpenPhantasie.Client</c>.
/// </summary>
public enum GameMode
{
    Title,
    Guild,
    Town,
    Overworld,
    Dungeon,
    Combat,
}

public sealed partial class GameSession
{
    private readonly OverworldTraversal _overworld;
    private readonly DungeonTraversal _dungeons;
    private readonly ICombatResolver _combat;
    private readonly IItemCatalog _items;

    internal IItemCatalog Items => _items;
    private readonly Func<int, TownState> _townFactory;
    private readonly Func<int, Monster> _monsterRecord;
    private readonly IReadOnlyList<int> _wildernessMonsterIds;
    private readonly CharacterCreationTables _creation;
    private readonly IDosRandomSource _rng;
    private readonly IReadOnlyList<Character> _elementalTemplates;
    private readonly WildernessEncounterRules _wildernessEncounters;
    private readonly List<string> _log = new();

    public GameMode Mode { get; private set; } = GameMode.Title;
    public GuildRoster Roster { get; }
    public Party Party { get; }
    public LootSack Sack { get; }

    /// <summary>
    /// This campaign's rule constants (Phase 09 T1.3: gold caps, Guild size, starting gold), from the game's
    /// <c>rules.json</c>. The roster, party and sack are built with it.
    /// </summary>
    public RulesProfile Rules { get; }

    private readonly List<int> _pendingTreasure = new();

    /// <summary>
    /// Who found each <see cref="PendingTreasure"/> item, in step with it: null = the game's plain question (I's "YOU FIND %s.
    /// KEEP IT?", III's dungeon "Keep %s?"), a name = III's fight find ("&lt;name&gt; finds &lt;item&gt;" / `core.p3.will_the_party_keep_it`,
    /// root <c>0xf2eb</c>).
    /// </summary>
    private readonly List<string?> _pendingFinders = new();

    /// <summary>
    /// Treasure items found at the end of a fight, each waiting for "YOU FIND %s. KEEP IT?" (OV2 <c>0x421d</c>, menu
    /// <c>0x1b</c>). Answer the first with <see cref="AnswerTreasure"/>; <see cref="TreasurePrompt"/> is its text.
    /// </summary>
    public IReadOnlyList<int> PendingTreasure => _pendingTreasure;

    /// <summary>
    /// s156: the first record of the MESS message a message cell last showed on the dungeon's line (<see cref="DungeonState.LastMessageId"/>,
    /// OV2 <c>[0x64f4]</c> / Amiga h4 <c>+0x66</c>) as the data stores it (leading blanks kept), or null. Presentation only:
    /// I Amiga's line centres the record's 40 stored cells.
    /// </summary>
    public string? LastDungeonMessageRecord =>
        Dungeon is { LastMessageId: > 0 } d && _dungeons.LevelOf(d).Messages.TryGetValue(d.LastMessageId, out var m)
            ? m.Lines.FirstOrDefault()
            : null;

    /// <summary>The first pending item's name (the one <see cref="TreasurePromptLines"/> asks about), or null.</summary>
    public string? PendingTreasureName => _pendingTreasure.Count == 0 ? null : _items.Get(_pendingTreasure[0])?.Name ?? "?";

    /// <summary>"YOU FIND {item}. KEEP IT?" for the first pending item, or null (<see cref="TreasurePromptLines"/> joined).</summary>
    public string? TreasurePrompt => TreasurePromptLines is { } lines ? string.Join(" ", lines) : null;

    /// <summary>
    /// The question for the first pending item, or null: one line ("YOU FIND {item}. KEEP IT?"; III's dungeon "Keep {item}?"),
    /// or, for a III fight find, DOS's two (root <c>0xf2eb</c>: "{name} finds {item}" at row 10, `core.p3.will_the_party_keep_it` at
    /// row 12).
    /// </summary>
    public IReadOnlyList<string>? TreasurePromptLines
    {
        get
        {
            if (_pendingTreasure.Count == 0)
                return null;
            var item = _items.Get(_pendingTreasure[0])?.Name ?? "?";
            return _pendingFinders[0] is { } finder
                ? [GameText.F("core.p3.x_finds_x", finder, item), GameText.S("core.p3.will_the_party_keep_it")]
                : [GameText.F("core.treasure.you_find_x_keep_it", item)];
        }
    }

    /// <summary>The finder of each pending item (null = none named), in step with <see cref="PendingTreasure"/>.</summary>
    public IReadOnlyList<string?> PendingTreasureFinders => _pendingFinders;

    private void ClearPendingTreasure()
    {
        _pendingTreasure.Clear();
        _pendingFinders.Clear();
    }

    private void AddPendingTreasure(int itemId, string? finder)
    {
        _pendingTreasure.Add(itemId);
        _pendingFinders.Add(finder);
    }

    /// <summary>
    /// Answer the first pending treasure prompt. YES stores it in the found items; a full sack prints "Sack is full!!!" and the
    /// item is lost, as is a NO. Returns true if the item was kept. If an event script stopped at this offer
    /// (<see cref="PendingDungeonScript"/>), its later ops run before this returns.
    /// </summary>
    public bool AnswerTreasure(bool keep)
    {
        if (_pendingTreasure.Count == 0)
            throw new InvalidOperationException("No treasure is waiting for an answer.");
        var id = _pendingTreasure[0];
        _pendingTreasure.RemoveAt(0);
        _pendingFinders.RemoveAt(0);
        var kept = keep && StoreTreasure(id);
        // An event script that stopped at this offer (op 6) runs its later ops now (s58: DOS asks mid-script).
        if (_pendingTreasure.Count == 0 && PendingDungeonScript is not null && Dungeon is not null)
            Campaign.ResumeDungeonScript(this);
        return kept;
    }

    private bool StoreTreasure(int id)
    {
        if (!Sack.TryAdd(id)) // [D✓] s65: the first empty slot
        {
            Note(GameText.S("core.treasure.sack_is_full"));
            return false;
        }
        return true;
    }
    public OverworldState Overworld { get; } = new();

    /// <summary>The live region the party is in (raw bytes incl. fog, read-only use: drawing).</summary>
    public OverworldRegion CurrentOverworldRegion => _overworld.GetRegion(Overworld.RegionId);

    /// <summary>DOS travel-time word <c>[0x63f2]</c> (<see cref="TravelClock"/>): wilderness steps add, town entry ages and resets.</summary>
    public int TravelTime { get; set; }
    public DungeonState? Dungeon { get; private set; }
    public CombatEncounter? Combat { get; private set; }

    // Per-round PARTY OPTIONS state (OV2 0x3b70 runs once per round); reset by ResolveCombatRound / BeginCombat.
    private bool _roundMoraleDone;
    private bool _roundOptionTaken;
    public TownState? CurrentTown { get; private set; }
    public int? CurrentTownId { get; private set; }

    /// <summary>
    /// True from <see cref="StartNewGame"/> until the party next enters a town (<see cref="EnterTown"/>); a loaded game is
    /// never fresh. II ST: a new turn opens Pippacott with the town global <c>[0x25b94]</c> still 0 (only the town tile,
    /// the save loader, TRANSPORTATION, the defeat and the ending write it), so the banner prints in pen 0
    /// (black, s109 live) until then. The Client picks the town name colour by it; no rule reads it.
    /// </summary>
    public bool FreshStart { get; private set; }

    private readonly Dictionary<int, TownState> _towns = new();

    /// <summary>
    /// Town Armory states built so far, by town id (P1.4). A town is built from the data files on its first visit and
    /// then kept, as DOS keeps TWNS.DAT (s35: bought stock stays gone on re-entry). Every <see cref="EnterTown"/> runs the
    /// DOS restock churn on the town entered (<see cref="ArmoryRestock"/>, s41).
    /// </summary>
    public IReadOnlyDictionary<int, TownState> Towns => _towns;

    /// <summary>
    /// The factory town of <paramref name="townId"/> when it carries static data a save does not hold (II's exit position and
    /// transport list), else null (Phantasie I: the save's record is the whole town).
    /// </summary>
    private TownState? StaticTownTemplate(int townId)
    {
        try
        {
            var t = _townFactory(townId);
            return t.Exit is not null || t.TransportDestinations is not null ? t : null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    /// <summary>The kept state of town <paramref name="townId"/>, built from the town factory on first use.</summary>
    private TownState TownStateFor(int townId)
    {
        if (!_towns.TryGetValue(townId, out var town))
        {
            town = _townFactory(townId);
            _towns[townId] = town;
        }
        return town;
    }

    /// <summary>FIRE, WATER, EARTH ELEM templates (roster 22–24, <c>elementals.json</c>) for Guild → Add.</summary>
    public IReadOnlyList<Character> ElementalTemplates => _elementalTemplates;

    /// <summary>[D✓] s65: INPUT NAME's uniqueness loop also covers roster records 22–24 ("FIRE ELEM" is taken).</summary>
    private IEnumerable<string> ElementalNames => _elementalTemplates.Select(e => e.Name);

    /// <summary>
    /// How a summoned elemental's combat fields are set (<see cref="ElementalPrepRule"/>), from this save's
    /// <see cref="Options"/>. D-032 (user, s34): the normal prep (manual and Remaster presets), fixing DOS's never-prepped
    /// slot 7; <see cref="ElementalPrepRule.Dos"/> is the Original (DOS) preset.
    /// </summary>
    public ElementalPrepRule ElementalPrep => Options.ElementalPrep;

    /// <summary>
    /// The rules this save plays by (T5.0, <c>09-startup-options.md</c> §5): set for a new game
    /// (<see cref="StartNewGame"/>) and replaced only by a loaded save. Locked: nothing else changes it. Written once at the
    /// top level of the save.
    /// </summary>
    public GameOptions Options
    {
        get => _options;
        private set
        {
            if (value.Game != Game)
                throw new ArgumentException(
                    $"These options are {value.Game.Prefix()}, but this session is {Game.Prefix()}.", nameof(value));
            _options = value;
            // D-048 (p1.gold-cap): the purse, the bank accounts and the found gold follow the save's cap.
            Party.GoldCapRule = value.GoldCap; // the Guild treasury lifts it (GameOptions.GoldCap)
            Sack.GoldCap = Party.GoldCap;
            BindTreasury(); // s175: spends draw on the Guild treasury when the save keeps one
            // P3.2: the DOS creation-table cells (D-001, D-006), the original's lifespans (D-003 / D2-021) and the original
            // platform's Guild size (D-002 / D2-020).
            _rulesCreation = _creation.ForRules(
                value.IsDos(RuleOptionRegistry.HalflingDex), value.OriginalSpellLevels, value.OriginalLifespans);
            Sack.Capacity = value.IsDos(RuleOptionRegistry.SackSize) ? LootSack.DosSlotCount : LootSack.MaxItems; // D-018
            Roster.MaxCharacters = value.OriginalGuildSize
                ? Rules.ForPlatform(Rules.OriginalPlatform).GuildMaxCharacters
                : Rules.GuildMaxCharacters;
            Roster.Unlimited = value.GuildUnlimited; // QoL guild-limit (PLAN P3, user s79): overrides the cap above
            // The campaign's own set when the save keeps the game's modules; a built set only for a custom mix.
            var defaults = Campaign.DefaultFeatureModules;
            _featureModules = value.Modules.SequenceEqual(defaults.Modules.Select(m => m.Descriptor.Id))
                ? defaults
                : value.CreateModules();
            if (Party is not null) // the constructor builds the party before it sets the options
                Party.Modules = _featureModules;
            Campaign.ApplyRules(this, value); // T4: a game's own limits (III: found items, roster)
        }
    }

    private GameOptions _options = null!;

    /// <summary>
    /// The feature modules this session's fights run with (Phase 09 T1.6), handed to every encounter it starts. Built
    /// from <see cref="Options"/> (saved with them); the setter is a test hook for explicit module instances and is not
    /// saved. A set must start from this session's <see cref="Game"/>.
    /// </summary>
    public FeatureModuleSet FeatureModules
    {
        get => _featureModules;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Game != Game)
                throw new ArgumentException(
                    $"These feature modules start from {value.Game.Prefix()}, but this session is {Game.Prefix()}.",
                    nameof(value));
            _featureModules = value;
            Party.Modules = value; // out-of-combat damage (traps, scripts, swimming) runs the same hooks
        }
    }

    private FeatureModuleSet _featureModules = null!; // set with Options in the constructor

    /// <summary>
    /// The game this session plays (Phase 09 T1.4): its campaign's game. The single source of "which game": the rules
    /// profile, the feature modules, the loaded data (<c>GameSessionFactory</c>) and every save are checked against it.
    /// </summary>
    public GameId Game => Campaign.Game;

    /// <summary>
    /// The game's own content on top of the shared loop (Phase 09 T1.4): magical pools, dungeon scripts, the win, party
    /// defeat and the death step. Phantasie I: <see cref="Phantasie1Campaign"/>.
    /// </summary>
    public ICampaign Campaign { get; }

    /// <summary>What the campaign's hooks may use beyond the public API (<see cref="Core.CampaignAccess"/>).</summary>
    public CampaignAccess CampaignAccess { get; }

    /// <summary>Mode we return to when combat ends (overworld or dungeon).</summary>
    public GameMode CombatReturnMode { get; private set; } = GameMode.Overworld;

    public IReadOnlyList<string> Log => _log;
    public string LastMessage => _log.Count == 0 ? "" : _log[^1];

    public GameSession(
        OverworldTraversal overworld,
        DungeonTraversal dungeons,
        IItemCatalog items,
        Func<int, TownState> townFactory,
        Func<int, Monster> monsterRecord,
        CharacterCreationTables creation,
        IDosRandomSource rng,
        RulesProfile rules,
        ICombatResolver? combat = null,
        IReadOnlyList<int>? wildernessMonsterIds = null,
        IReadOnlyList<Character>? elementalTemplates = null,
        WildernessEncounterRules? wildernessEncounters = null,
        ICampaign? campaign = null)
    {
        _overworld = overworld ?? throw new ArgumentNullException(nameof(overworld));
        _dungeons = dungeons ?? throw new ArgumentNullException(nameof(dungeons));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _townFactory = townFactory ?? throw new ArgumentNullException(nameof(townFactory));
        _monsterRecord = monsterRecord ?? throw new ArgumentNullException(nameof(monsterRecord));
        _creation = creation ?? throw new ArgumentNullException(nameof(creation));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        Campaign = campaign ?? Phantasie1Campaign.Instance;
        CampaignAccess = new CampaignAccess(this);
        Rules = (rules ?? throw new ArgumentNullException(nameof(rules))).Validate();
        if (Rules.Game != Game)
            throw new ArgumentException(
                $"The rules profile is {Rules.Game.Prefix()}, but the campaign is {Game.Prefix()}.", nameof(rules));
        if (Campaign.DefaultFeatureModules.Game != Game)
            throw new InvalidOperationException(
                $"The {Game.Prefix()} campaign's default feature modules start from {Campaign.DefaultFeatureModules.Game.Prefix()}.");
        Roster = new GuildRoster(Rules);
        Party = new Party(Rules) { Death = Campaign.Death };
        Sack = new LootSack { GoldCap = Rules.PartyGoldCap };
        Options = GameOptions.ForPreset(Game); // after Party and Sack: it sets their gold caps and the party's modules
        _combat = combat ?? Campaign.CombatResolver ?? new CombatResolver();
        _wildernessMonsterIds = wildernessMonsterIds is { Count: > 0 }
            ? wildernessMonsterIds
            : Campaign.FallbackMonsterPool;
        _elementalTemplates = elementalTemplates ?? Array.Empty<Character>();
        _wildernessEncounters = wildernessEncounters ?? WildernessEncounterRules.Dos;
    }

    /// <summary>
    /// The DOS surprise globals <c>[0x649e]</c>/<c>[0x64a0]</c> between fights [C] (s39, <c>combat-resolution.md</c> §Surprise
    /// stale flags). Every fight start sets them and every fight end leaves the live values here. Fixed encounters
    /// (x7 tiles, class 7) and the <c>0x70ed</c> fall-off fight write neither flag, so they open with what is left.
    /// [~] DOS BSS is presumably 0 at start (and in older saves).
    /// </summary>
    public SurpriseFlags SurpriseGlobals { get; set; }

    /// <summary>
    /// <c>[0x63ea]</c> [C] (s39): the highest <c>@70</c> SCORE of the party (all slots) when it last left town (OV1
    /// <c>0x3271</c>). The day encounter's Black Knights flee from a party above 83.
    /// </summary>
    public int HighestScore { get; set; }

    /// <summary>
    /// <c>[0x63ec]</c> [C] (OV1 <c>0x3269..0x32fc</c>): the average party level (sum / party size, truncated) when the
    /// party last left town. The next town entry's Armory restock draws from the first ~L entries of the town's restock
    /// list (<see cref="ArmoryRestock"/>, OV1 <c>0x8e1</c>). Computed with <see cref="PartyOptions.AveragePartyLevel"/>.
    /// </summary>
    public int TownExitAverageLevel { get; set; }

    /// <summary>A day-branch menu of the random wilderness encounter waits for <see cref="AnswerWildernessPrompt"/>.</summary>
    public WildernessPrompt? WildernessPrompt { get; internal set; }

    /// <summary>"A TOWN. ENTER?" / `core.overworld.a_dungeon_enter` waits for <see cref="AnswerOverworldEntry"/> (manual p.12).</summary>
    public OverworldEntryPrompt? EntryPrompt { get; internal set; }

    /// <summary>
    /// Number of lines ever passed to <see cref="Note"/> (read-only; never reset, unlike <see cref="Log"/>, which keeps
    /// only the last 200). A front end compares two values to show exactly the lines added in between.
    /// </summary>
    public long LogSequence { get; private set; }

    /// <summary>The lines noted after <paramref name="sequence"/> (an earlier <see cref="LogSequence"/>), oldest first;
    /// lines already dropped from <see cref="Log"/> are skipped.</summary>
    public IReadOnlyList<string> LogSince(long sequence)
    {
        var count = (int)Math.Clamp(LogSequence - sequence, 0, _log.Count);
        return _log.GetRange(_log.Count - count, count);
    }

    /// <summary>
    /// s165: III's last Damage Report (<see cref="P3DamageReport"/>, Amiga <c>0x1d4ea</c>); null before any. A front end
    /// shows a report once, when <see cref="DamageReportSerial"/> moves on.
    /// </summary>
    public P3DamageReport? DamageReport { get; private set; }

    /// <summary>Number of Damage Reports ever made (never reset); see <see cref="DamageReport"/>.</summary>
    public long DamageReportSerial { get; private set; }

    internal void Report(P3DamageReport report)
    {
        DamageReport = report;
        DamageReportSerial++;
    }

    /// <summary>
    /// Number of game sounds ever started through <see cref="PlaySound"/> (never reset). A front end compares two values to
    /// play exactly the sounds started in between (<see cref="SoundsSince"/>).
    /// </summary>
    public long SoundSequence { get; private set; }

    private readonly List<int> _sounds = new();

    /// <summary>The sound numbers started after <paramref name="sequence"/> (an earlier <see cref="SoundSequence"/>),
    /// oldest first; only the last 16 are kept.</summary>
    public IReadOnlyList<int> SoundsSince(long sequence)
    {
        var count = (int)Math.Clamp(SoundSequence - sequence, 0, _sounds.Count);
        return _sounds.GetRange(_sounds.Count - count, count);
    }

    /// <summary>
    /// s136: start the game's sound <paramref name="sound"/> (III: the number <c>0xb126(n)</c> plays on the Amiga, the same
    /// numbers DOS writes to its event word <c>FAR:0xde58</c>). Core only records it; the front end plays it from the pack
    /// with its Sound switch on, or not at all.
    /// </summary>
    public void PlaySound(int sound)
    {
        if (sound <= 0)
            return;
        SoundSequence++;
        _sounds.Add(sound);
        if (_sounds.Count > 16)
            _sounds.RemoveRange(0, _sounds.Count - 16);
    }

    /// <summary>
    /// s139: number of moments ever noted through <see cref="Cue"/> (never reset); a front end compares two values to sound
    /// exactly the moments noted in between (<see cref="CuesSince"/>).
    /// </summary>
    public long CueSequence { get; private set; }

    private readonly List<string> _cues = new();

    /// <summary>The moments noted after <paramref name="sequence"/> (an earlier <see cref="CueSequence"/>), oldest first;
    /// only the last 16 are kept.</summary>
    public IReadOnlyList<string> CuesSince(long sequence)
    {
        var count = (int)Math.Clamp(CueSequence - sequence, 0, _cues.Count);
        return _cues.GetRange(_cues.Count - count, count);
    }

    /// <summary>
    /// s139: note that the moment <paramref name="moment"/> (<see cref="FieldCues"/>) happened. Core only records it; the
    /// front end looks the moment up in the platform pack (II ST: <c>field_sounds</c>) and plays that sound, or nothing.
    /// </summary>
    public void Cue(string moment)
    {
        ArgumentException.ThrowIfNullOrEmpty(moment);
        CueSequence++;
        _cues.Add(moment);
        if (_cues.Count > 16)
            _cues.RemoveRange(0, _cues.Count - 16);
    }

    /// <summary>
    /// s141: a move of the party inside a dungeon level that a step or an answer made after some of its lines: the party
    /// stood on (<paramref name="FromX"/>, <paramref name="FromY"/>) of level <paramref name="LevelId"/> until
    /// <paramref name="AfterLine"/> lines had been noted (<see cref="LogSequence"/>). I Amiga shows the boxes logged before
    /// it over that cell (h41: op 12's `core.script.the_party_is_moved` and a jammed lock's `core.dungeon.no_one_can_pick_the_lock` come before the
    /// move). <paramref name="Serial"/> is <see cref="DungeonMoveSequence"/> after it.
    /// </summary>
    public sealed record DungeonMove(long Serial, long AfterLine, int LevelId, int FromX, int FromY);

    /// <summary>s141: number of <see cref="DungeonMove"/>s ever recorded (never reset).</summary>
    public long DungeonMoveSequence { get; private set; }

    private readonly List<DungeonMove> _dungeonMoves = new();

    /// <summary>The moves recorded after <paramref name="sequence"/> (an earlier <see cref="DungeonMoveSequence"/>),
    /// oldest first; only the last 16 are kept.</summary>
    public IReadOnlyList<DungeonMove> DungeonMovesSince(long sequence) => [.. _dungeonMoves.Where(m => m.Serial > sequence)];

    /// <summary>s141: the party is about to leave (<paramref name="fromX"/>, <paramref name="fromY"/>) of the current
    /// level after the lines noted so far (<see cref="DungeonMove"/>).</summary>
    internal void NoteDungeonMove(int fromX, int fromY)
    {
        if (Dungeon is not { } d)
            return;
        DungeonMoveSequence++;
        _dungeonMoves.Add(new DungeonMove(DungeonMoveSequence, LogSequence, d.LevelId, fromX, fromY));
        if (_dungeonMoves.Count > 16)
            _dungeonMoves.RemoveRange(0, _dungeonMoves.Count - 16);
    }

    /// <summary>
    /// s144: <paramref name="Count"/> lines noted after <paramref name="AfterLine"/> lines (<see cref="LogSequence"/>) that the
    /// original shows in a window of their own (<see cref="LogWindowKind"/>: a MESS text, a damage report, s153 an overworld
    /// text page's box). Core prints them as before; I Amiga's front end draws them as requester 21 (FORMAT.md "Requester 21
    /// on the map") or 23 ("The map screen's requester 23"). s153: renamed from DungeonWindow, the overworld notes them too.
    /// </summary>
    public sealed record MapWindow(long AfterLine, int Count, LogWindowKind Kind)
    {
        /// <summary>s153: the window's rows as the original stores them (<see cref="LogWindow.Rows"/>); null: the logged lines.</summary>
        public IReadOnlyList<string>? Rows { get; init; }
    }

    private readonly List<MapWindow> _mapWindows = new();

    /// <summary>s144: the windows whose lines were all noted after <paramref name="sequence"/> (an earlier <see cref="LogSequence"/>),
    /// oldest first; only the last 16 are kept.</summary>
    public IReadOnlyList<MapWindow> MapWindowsSince(long sequence) =>
        [.. _mapWindows.Where(w => w.AfterLine >= sequence && w.AfterLine + w.Count <= LogSequence)];

    /// <summary>s144: the last <paramref name="count"/> lines noted are one <paramref name="kind"/> window (s153: shown as
    /// <paramref name="rows"/> when given).</summary>
    internal void NoteMapWindow(LogWindowKind kind, int count, IReadOnlyList<string>? rows = null)
    {
        if (count > 0)
            NoteMapWindowAt(LogSequence - count, count, kind, rows);
    }

    /// <summary>s144: a result's <paramref name="log"/> was just noted in full (its last line is the last noted): mark its windows.</summary>
    internal void NoteMapWindows(IReadOnlyList<string> log, IReadOnlyList<LogWindow> windows)
    {
        var first = LogSequence - log.Count; // lines noted before log[0]
        foreach (var w in windows)
            if (w.Count > 0 && w.Start >= 0 && w.Start + w.Count <= log.Count)
                NoteMapWindowAt(first + w.Start, w.Count, w.Kind, w.Rows);
    }

    private void NoteMapWindowAt(long afterLine, int count, LogWindowKind kind, IReadOnlyList<string>? rows = null)
    {
        _mapWindows.Add(new MapWindow(afterLine, count, kind) { Rows = rows });
        if (_mapWindows.Count > 16)
            _mapWindows.RemoveRange(0, _mapWindows.Count - 16);
    }

    public void Note(string message)
    {
        LogSequence++;
        _log.Add(message);
        if (_log.Count > 200)
            _log.RemoveRange(0, _log.Count - 200);
    }

    // --- Title / Guild -------------------------------------------------------

    /// <summary>
    /// New game [C] (OV1 <c>0x7dd(1)</c>, branch <c>0x8ae</c>): every roster slot leaves the party (<c>@218</c> := 0) and
    /// the town index is 0 = Pelnor, then the town loop runs — the party starts at the Pelnor town menu with nobody in
    /// it (manual p.4: "The first place a new game player should visit is the Guild"). `[~]` the same branch calls the
    /// Armory churn <c>0x8e1(0)</c>, which may return early on the new-game flag <c>[0x63e8]</c>; Core skips it.
    /// <paramref name="options"/> are the rules the new save is locked to (T5.0); null = Original (manual).
    /// </summary>
    public void StartNewGame(GameOptions? options = null)
    {
        Treasury = new(); // s175: a new game starts with an empty Guild treasury
        Options = options ?? GameOptions.ForPreset(Game);
        foreach (var member in Roster.Occupied)
            member.InParty = false;
        Party.Members.Clear();
        Party.Elemental = null;
        _otherParties.Clear(); // D-069: a new game has no parked parties
        var startTown = Campaign.StartTownId; // I: 1 = Pelnor
        CurrentTownId = startTown;
        FreshStart = true;
        CurrentTown = TownStateFor(startTown);
        Dungeon = null;
        Combat = null;
        Mode = GameMode.Town;
        Note(GameText.F("core.session.entered_town_x", startTown));
        Campaign.OnTownEntered(this, startTown); // T4: III puts the party on Pendragon's square
    }

    public void OpenGuild()
    {
        Mode = GameMode.Guild;
        SweepTreasury();
        Note(GameText.S("core.session.adventurers_guild"));
    }

    /// <summary>Guild menu Exit: back to the town menu (the Guild is a town building, manual p.3).</summary>
    public void LeaveGuild()
    {
        if (CurrentTownId is null)
            throw new InvalidOperationException("The Guild is not in a town.");
        Mode = GameMode.Town;
        SweepTreasury();
        Note(GameText.S("core.session.left_the_guild"));
    }

    /// <summary>Guild N)EW: false when the roster is full (<see cref="RulesProfile.GuildMaxCharacters"/>; I: the manual's 21, D-002).</summary>
    public bool GuildHasRoom => Roster.HasRoom;

    /// <summary>
    /// Guild N)EW, first half (OV1 <c>0xbac</c>): race, class and the attribute roll, before the name. The character is
    /// not enrolled; <see cref="TryEnrollNewMember"/> names and enrolls it on Y, and an N simply drops it — DOS frees the
    /// slot and starts again at the race menu (<c>0xf5e</c> → <c>0xbb2</c>). The name stays empty until INPUT NAME.
    /// </summary>
    public Character RollNewMember(Race race, CharacterClass cls, bool randomRace = false) =>
        RollNewMember(race, cls, randomRace, abilities: null);

    /// <summary>
    /// <see cref="RollNewMember(Race, CharacterClass, bool)"/> with the five abilities given (T5 lineage,
    /// <see cref="RollDescendant"/>): the game's own creation with <paramref name="abilities"/> in place of the attribute
    /// roll, clamped to the game's range; null = the plain roll.
    /// </summary>
    private Character RollNewMember(Race race, CharacterClass cls, bool randomRace, IReadOnlyList<int>? abilities)
    {
        var rolled = Campaign.RollNewMember(this, race, cls, randomRace, abilities) ?? CharacterCreation.Create(
            new CharacterCreationRequest
            {
                Name = UnnamedPlaceholder,
                Race = race,
                RandomRace = randomRace,
                Class = cls,
                ExistingRaces = Roster.Occupied.Select(c => c.Race).ToList(),
                DosHitDice = Options.OriginalHitDice,
                TossRock = FeatureModules.TossRock,
                Abilities = abilities,
            },
            CreationTables,
            _rng,
            Rules);
        rolled.Name = "";
        return rolled;
    }

    private const string UnnamedPlaceholder = "?";

    /// <summary>
    /// T5: Guild New may make a descendant (<see cref="FeatureModuleRegistry.Lineage"/> switched on in this save's rules; IV
    /// has its own creation and never asks here).
    /// </summary>
    public bool LineageOffered =>
        Game != GameId.P4 && Options.Modules.Contains(FeatureModuleRegistry.Lineage.Id, StringComparer.Ordinal);

    /// <summary>
    /// T5 lineage, Guild New's descendant (<c>ancestry.md</c> section 13): the pair is checked (<see cref="LineageRules.Pair"/>;
    /// a refused pair throws), the abilities are inherited by IV's rule (<see cref="LineageRules.Inherit"/>), then the
    /// game's own creation runs with them (<see cref="RollNewMember(Race, CharacterClass, bool)"/>'s other draws, clamped
    /// to the game's range) and <see cref="Character.Lineage"/> holds both ancestors. <paramref name="race"/> is used only
    /// when both ancestors are new; otherwise the descendant takes the persons' race. Not enrolled; no name yet.
    /// </summary>
    public Character RollDescendant(LineageSetup setup, LineagePerson? first, LineagePerson? second, Race race,
        CharacterClass cls)
    {
        ArgumentNullException.ThrowIfNull(setup);
        var pair = LineageRules.Pair(Game, first, second, race);
        if (pair.Refusal != LineageRefusal.None)
            throw new InvalidOperationException($"Lineage pair refused: {pair.Refusal}.");
        var abilities = LineageRules.Inherit(Options, setup.Tables, _rng, first, second, pair.Race, cls, () =>
        {
            var s = RollNewMember(pair.Race, cls, randomRace: false, abilities: null).Stats;
            return [s.Str, s.Int, s.Dex, s.Con, s.Cha];
        });
        var rolled = RollNewMember(pair.Race, cls, randomRace: false, abilities);
        rolled.Lineage = LineageRules.LineageOf(Game, first, second, pair.Race, cls);
        return rolled;
    }

    /// <summary>
    /// Guild N)EW: the rolled character is kept (Y), before INPUT NAME. s138: III plays 6 here (Amiga <c>0x13938</c>: OK on
    /// window 3 over the roll's sheet <c>0x13b50</c>, then the name field); the others play nothing.
    /// </summary>
    public void KeepRolledMember()
    {
        if (Game == GameId.P3)
            PlaySound(P3Sounds.RollKept);
    }

    /// <summary>
    /// s141: the front end asks a confirm question the Amiga asks in window 1 (<c>0xaae2</c>: "Are you sure …?" for load /
    /// redistribute / purge / save, and the guild Add's "It will take this character two months to make the journey
    /// here."). III plays 36 first (<c>0xaaf2</c>, <c>jsr %pc@(0xb126)</c>); the others play nothing. Callers: the guild
    /// Add's question (<see cref="AddNeedsMoveConfirm"/>) and, s160, III's Load / Save / Distribute / Purge questions (DOS
    /// III root <c>0x1820</c>(text, 2), the Amiga's <c>0xaae2</c>) under every III layout.
    /// </summary>
    public void AskConfirmQuestion()
    {
        if (Game == GameId.P3)
            PlaySound(P3Sounds.ConfirmQuestion);
    }

    /// <summary>
    /// The front end could not write the save. s138: III plays 12, the Amiga's "Disk Error! A save may have been attempted on
    /// a protected disk" sound (<c>0x20258</c>, from the save writer <c>0x1faf0</c>) `[~]` equivalence; the others nothing.
    /// </summary>
    public void NoteSaveFailed()
    {
        if (Game == GameId.P3)
            PlaySound(P3Sounds.DiskError);
    }

    /// <summary>Remaster text (D-065): DOS never refuses, so it has no message of its own.</summary>
    public static string RenamePartyMemberRefusal => GameText.S("core.guild.rename_party_member");

    /// <summary>Guild N)EW, second half (OV1 <c>0xfd3</c>, INPUT NAME): the name must be unique (`core.guild.name_not_unique`).</summary>
    public bool TryEnrollNewMember(Character rolled, string name, out string error)
    {
        if (!GuildHall.TryRename(rolled, name, Roster, out error, ElementalNames) ||
            !GuildHall.TryEnroll(Roster, rolled, out _, out error))
            return false;
        rolled.CurrentTown = GuildTownId; // [C] OV1 0xdfb: @254 := the creating Guild's town
        Campaign.OnCharacterEnrolled(this, rolled);
        SweepTreasury(); // s175: the new character's starting gold
        Note(GameText.F("core.guild.enrolled_x_x_x", rolled.Name, rolled.Class, rolled.Race));
        return true;
    }

    /// <summary>
    /// Why Guild P)URGE would refuse <paramref name="member"/> (a member of another parked party, D-069; then
    /// <see cref="GuildHall.PurgeRefusal"/>), or null. s160: DOS III refuses before it asks "Are you sure …?" (root
    /// <c>0xb075</c>), so the front end asks only when this is null.
    /// </summary>
    public string? PurgeRefusal(Character member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (IsInOtherParty(member)) // D-069 (manual silent): continue that party and D)ROP the member first
            return SeveralParties.OtherPartyRefusal;
        if (_elementalTemplates.Contains(member)) // the Guild's elemental records: never purged (every game and option)
            return GameText.S("core.guild.cannot_purge_elemental");
        return GuildHall.PurgeRefusal(Roster, Party, member, PurgeRule);
    }

    /// <summary>
    /// The Guild's elemental rules this save plays by: the game's rules.json block (<see cref="RulesProfile.Elementals"/>)
    /// under the every-game options <c>elemental-load</c> / <c>elemental-drop</c> (<see cref="ElementalGuildRules.Elementals"/>).
    /// </summary>
    public ElementalRules GuildElementalRules => Options.Elementals(Party.Rules.ElementalRulesOrDefault);

    /// <summary>What Guild P)URGE does to a party member and a loaded elemental (<c>elemental-purge</c>; under <c>game</c> the game's own, D3-040 for III).</summary>
    public PurgePartyRule PurgeRule => Options.PurgeRule(GuildElementalRules);

    /// <summary>
    /// Guild P)URGE (OV1 <c>0x23d5</c>, `member.whom_do_you_want_to_purge`): no confirm in I's DOS; s160: III's front end asks DOS
    /// III's S407 first (<see cref="AskConfirmQuestion"/>).
    /// </summary>
    public bool TryPurge(Character member, out string error)
    {
        if (PurgeRefusal(member) is { } refusal)
        {
            error = refusal;
            return false;
        }
        if (!GuildHall.TryPurge(Roster, Party, member, out error, PurgeRule))
            return false;
        Note(GameText.F("core.guild.x_is_purged", member.Name));
        return true;
    }

    /// <summary>
    /// Guild R)ENAME (OV1 <c>0x22f6</c>, `member.who_needs_renaming`). DOS renames party members too (it syncs the party copy
    /// before and after INPUT NAME); D-065 (user s53): the manual — "Characters cannot be renamed while they are members
    /// of a party." The save's <c>p1.rename-party</c> option can allow it (<see cref="CanRename"/>).
    /// </summary>
    public bool TryRename(Character member, string newName, out string error)
    {
        if (!CanRename(member))
        {
            error = RenamePartyMemberRefusal;
            return false;
        }
        var old = member.Name;
        if (!GuildHall.TryRename(member, newName, Roster, out error, ElementalNames))
            return false;
        Note(GameText.F("core.guild.x_is_now_x", old, member.Name));
        return true;
    }

    /// <summary>
    /// Whether Guild R)ENAME may rename <paramref name="member"/> (D-065): anyone under the save's
    /// <see cref="GameOptions.RenamePartyMembers"/> (DOS); otherwise only a character in no party, parked parties
    /// (D-069) included (manual).
    /// </summary>
    public bool CanRename(Character member) =>
        Options.RenamePartyMembers || !(Party.Members.Contains(member) || IsInOtherParty(member));

    public Character CreateAndEnroll(string name, Race race, CharacterClass cls)
    {
        var created = RollNewMember(race, cls);
        if (!TryEnrollNewMember(created, name, out var error))
            throw new InvalidOperationException(error);
        return created;
    }

    /// <summary>
    /// The town whose Guild is open: the current town, else Pelnor (1) — the new-game Guild (manual: "you will be in
    /// Pelnor"). DOS creation writes this Guild's town into <c>@254</c> (OV1 <c>0xdfb</c>).
    /// </summary>
    public int GuildTownId => CurrentTownId ?? 1;

    /// <summary>[C] OV1 <c>0x2835</c>: Add asks first when <paramref name="member"/> is in another town (<see cref="GuildHall.AddNeedsMoveConfirm"/>).</summary>
    public bool AddNeedsMoveConfirm(Character member) => GuildHall.AddNeedsMoveConfirm(member, GuildTownId);

    /// <summary>
    /// Guild → Add (OV1 <c>0x2786</c>): the member moves to this town, ages 4 units (2 months) and is flagged in the
    /// party. Callers ask <see cref="AddNeedsMoveConfirm"/> first and call this only on a yes.
    /// </summary>
    public bool TryAddToParty(Character member, out string error)
    {
        if (IsInOtherParty(member)) // D-069: a character belongs to one party at a time
        {
            error = SeveralParties.OtherPartyRefusal;
            return false;
        }
        if (!GuildHall.TryAddToParty(Roster, Party, member, GuildTownId, Options.IsDos(RuleOptionRegistry.AddAging), out error,
                GuildElementalRules))
            return false;
        Note(GameText.F("core.guild.x_joins_the_party", member.Name));
        return true;
    }

    public bool TryAddToPartyByName(string name, out string error)
    {
        var member = Roster.Occupied.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (member is null)
        {
            error = GameText.S("core.guild.no_such_guild_member");
            return false;
        }

        return TryAddToParty(member, out error);
    }

    /// <summary>
    /// Guild → Add an elemental (OV1 <c>0x2786</c>, roster rows 22–24): a fresh copy of template
    /// <paramref name="templateIndex"/> goes to party slot 7. Needs six members (I; <see cref="GuildElementalRules"/>) and no
    /// elemental loaded.
    /// </summary>
    public bool TryAddElemental(int templateIndex, out string error)
    {
        if ((uint)templateIndex >= (uint)_elementalTemplates.Count)
        {
            error = GameText.S("core.guild.no_such_elemental");
            return false;
        }

        if (!GuildHall.TryAddElemental(Party, _elementalTemplates[templateIndex], out error, GuildElementalRules))
            return false;
        Note(GameText.F("core.guild.x_joins_the_party", Party.Elemental!.Name));
        return true;
    }

    /// <summary>
    /// Guild D)ROP (OV1 <c>0x225c</c>, III ovl2 <c>0x3c92</c>): <paramref name="member"/> goes back to the roster under this
    /// save's elemental rules (<see cref="GuildElementalRules"/>); III's Drop share comes out of the party gold and the found
    /// gold (<see cref="GuildHall.DropGoldShare"/>).
    /// </summary>
    public bool TryDropFromParty(Character member, out string error) =>
        GuildHall.TryDropFromParty(Party, member, out error, Sack, GuildElementalRules);

    /// <summary>
    /// Guild D)ROP on the loaded elemental (II: the ST's Drop list holds it, <c>0xd57c</c>; III: DOS ovl2 <c>0x3c92</c>), in
    /// the games whose rules list it (<see cref="ElementalRules.DropsElemental"/>). The Guild record stays.
    /// </summary>
    public bool TryDropElemental(out string error) => GuildHall.TryDropElemental(Party, out error, GuildElementalRules);

    /// <summary>Test shortcut: Guild straight to the wilderness (new-game spawn if unset). The game leaves through the town Exit.</summary>
    public void DepartGuildToWilderness()
    {
        if (Party.Members.Count == 0)
            throw new InvalidOperationException("Form a party before leaving the guild.");

        CurrentTown = null;
        CurrentTownId = null;
        Dungeon = null;
        Combat = null;
        LeaveTownGlobals();
        Mode = GameMode.Overworld;
        Note(GameText.F("core.session.party_enters_wilderness_x_x_x", Overworld.RegionId, Overworld.X, Overworld.Y));
    }

    // --- Town ----------------------------------------------------------------

    public void EnterTown(int townId)
    {
        EntryPrompt = null;
        CurrentTownId = townId;
        FreshStart = false;
        CurrentTown = TownStateFor(townId);
        Mode = GameMode.Town;
        SweepTreasury(TreasuryEntryKind.Trip); // s175: trip gold is banked on arrival, before any share-out
        Note(GameText.F("core.session.entered_town_x", townId));
        foreach (var line in Aging.ApplyTownEntry(Party, townId, TravelTime, Options.OriginalLifespans,
                     Campaign.TownEntryAgeUnits(TravelTime), Campaign.AgesTheDead))
            Note(line);
        TravelTime = 0; // [C] OV1 0x19d0
        // [C] (s41) 0x1954 ends with 0x8e1(town): the Armory churn (wipes ids 0..119, re-adds from the restock list).
        // D-054: the manual's restock unless the save plays DOS's (p1.armory-restock); both draw the same numbers.
        if (Campaign.ArmoryRestocks)
            ArmoryRestock.Apply(CurrentTown, TownExitAverageLevel, _rng, dosRule: Options.DosArmoryRestock);
        Campaign.OnTownArrival(this, CurrentTown);
        Campaign.OnTownEntered(this, townId);
        // D3-071 (manual, user s142): III pays out on entry (Amiga 0x7964 → 0xbc7c(1); DOS root 0xa911 → ovl2 0x0000(1),
        // s143 static and live). DOS order: the found items' hand-out screen first (the Client opens it through
        // TakeTownItemHandOut), then "Shares for <name> :" per character (SessionPrompts, ShareOutExperience). Before
        // the hand-out, with a pool or found items, DOS readies each character's best equipment (ovl2 0x37f1) and drops
        // the last wand from the found list (0x5a9): Campaign.BeforeTownEntryPayout (s144). A defeat already emptied the
        // sack. Found gold joins the purse here as the s142 build did `[~]` (DOS's payout covers the pool and the items only).
        TownItemHandOutPending = false;
        TownXpSharesPending = false;
        if (Campaign.PaysOutOnTownEntry && HasPayableCharacter)
        {
            if (Sack.Gold > 0)
            {
                Party.AddGold(Sack.Gold); // D-048: over the cap is lost
                Sack.Gold = 0;
            }
            if (Sack.Experience > 0 || Sack.ItemCount > 0) // root 0xa975…0xa9a5
                Campaign.BeforeTownEntryPayout(this);
            TownItemHandOutPending = Sack.ItemCount > 0;
            TownXpSharesPending = Sack.Experience > 0;
        }
    }

    /// <summary>
    /// D3-071: the characters (no elemental, DOS <c>FAR:0xb614</c>) a town entry's "Shares for NAME :" asks, in party order:
    /// every one, the dead too (s144 <c>[C]</c>).
    /// </summary>
    public IReadOnlyList<Character> TownEntryShareTakers => Party.Adventurers.ToList();

    /// <summary>A character not dead: a living one is offered 1–3 shares, so the split has shares to divide.</summary>
    private bool HasPayableCharacter => Party.Adventurers.Any(m => m.Status != CharacterStatus.Dead);

    /// <summary>D3-071: the town entry's XP pool waits for its "Shares for" prompts (III), asked after the item hand-out.</summary>
    public bool TownXpSharesPending { get; private set; }

    /// <summary>The "Shares for" prompts are due now: in town, the entry's item hand-out done (DOS order, s143).</summary>
    public bool TownXpSharesDue =>
        TownXpSharesPending && !TownItemHandOutPending && ItemDistribution is null && Mode == GameMode.Town
        && Sack.Experience > 0 && HasPayableCharacter;

    /// <summary>
    /// D3-071, DOS III ovl2 <c>0x0000</c>(1) (s143 / s144 <c>[C]</c>): <paramref name="shares"/> lines up with
    /// <see cref="TownEntryShareTakers"/> (a dead character 0–3, any other 1–3); the campaign splits the pool
    /// (<see cref="ICampaign.ShareOutTownEntryExperience"/>; III: the remainder drawn point by point, nothing lost) and
    /// empties it.
    /// </summary>
    public void ShareOutExperience(IReadOnlyList<int> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);
        Campaign.ShareOutTownEntryExperience(this, TownEntryShareTakers, shares);
        TownXpSharesPending = false;
    }

    /// <summary>D3-071: the town entry left found items for the hand-out screen (III), not yet opened.</summary>
    public bool TownItemHandOutPending { get; private set; }

    /// <summary>True once (and clears the flag) when the town entry's item hand-out is due and the party is still in town.</summary>
    public bool TakeTownItemHandOut()
    {
        if (!TownItemHandOutPending)
            return false;
        TownItemHandOutPending = false;
        return Mode == GameMode.Town && Sack.ItemCount > 0;
    }

    /// <summary>A fresh record copy of 0-based monster <paramref name="id"/> (campaign builders, T4).</summary>
    internal Monster MonsterRecordOf(int id) => _monsterRecord(id);

    /// <summary>Town menu Exit. Refused with an empty party [C] (OV1 <c>0x3250</c>: <c>[0x648e] == 0</c>).</summary>
    public bool LeaveTown()
    {
        // [?] s172: II's own rules can leave only the elemental in the party (ST Drop 0xd57c); what the ST does on the way
        // out then is not read, so this guard (no character = no Exit) holds for it too.
        if (Party.Members.Count == 0)
        {
            Note(TownExit.EmptyPartyMessage);
            return false;
        }
        TownExit.Apply(Party);
        TransportationPending = false; // the DOS picker is modal; an unanswered pick never outlives the town
        TownItemHandOutPending = false;
        TownXpSharesPending = false;
        LeaveTownGlobals();
        // [C] (s41) OV1 0x3309..0x331a: Exit puts the party on the town's TWNS tile (+0x111 region, +0x113/+0x115 x/y).
        if (CurrentTown?.ExitPosition is { } pos)
            (Overworld.RegionId, Overworld.X, Overworld.Y) = pos;
        CurrentOverworldRegion.RevealAround(Overworld.X, Overworld.Y); // [D✓] s65: Exit shows the 3×3 round the party
        CurrentTown = null;
        CurrentTownId = null;
        Mode = GameMode.Overworld;
        Note(GameText.S("core.session.left_town_for_wilderness"));
        return true;
    }

    /// <summary>OV1 town exit: <c>[0x63ea]</c> := max <c>@70</c> (<c>0x3271</c>) and <c>[0x6496]</c> := 0 (<c>0x9b</c>/<c>0xa9</c>/<c>0xbe</c>).</summary>
    private void LeaveTownGlobals()
    {
        StartTreasuryTrip(); // s175: bank what the party holds, a new trip starts
        if (Options.IsDos(RuleOptionRegistry.OrdersReset))
            OrdersResets++; // D-078 dos: OV1 0xd0 sets every member's repeat order to PARRY
        HighestScore = Party.Members.Count == 0 ? 0 : Math.Max(0, Party.Members.Max(m => m.Score));
        TownExitAverageLevel = PartyOptions.AveragePartyLevel(Party);
        Sack.MarkTownExit(); // III's Mercy takes only items found after this (D3-024)
        Overworld.TileClassFightDone = false;
        WildernessPrompt = null;
    }

    /// <summary>
    /// Option → Cast spell outside combat. The place follows the mode (town OV1 <c>0x254e</c>; wilderness / dungeon OV2
    /// <c>0x4790</c>, D-035), and the field context carries what MONSTER EVAL. and VISION read (s41): the party cell's
    /// terrain subcode <c>[0x649c]</c> = tile % 10, the dungeon tier (buffer <c>0x4e7</c>) and the live region for the reveal.
    /// </summary>
    public MagicResult CastSpell(Character caster, int spellIndex, Character? healTarget = null)
    {
        TransportationPending = false;
        CastLocation location;
        FieldCastContext? field = null;
        switch (Mode)
        {
            case GameMode.Town:
                location = CastLocation.Town;
                break;
            case GameMode.Overworld:
                location = CastLocation.Field;
                var region = CurrentOverworldRegion;
                var subcode = region.GetNormalized(Overworld.X, Overworld.Y) % 10;
                field = new FieldCastContext(
                    TerrainSubcode: subcode,
                    Region: region, X: Overworld.X, Y: Overworld.Y,
                    NearbyMonsterLevels: MonsterLevels(12 * subcode, 12));
                break;
            case GameMode.Dungeon:
                location = CastLocation.Dungeon;
                var tier = DungeonEncounters.Tier(_dungeons.LevelOf(RequireDungeon()));
                field = new FieldCastContext(DungeonTier: tier, NearbyMonsterLevels: MonsterLevels(Rules.EncounterRulesOrDefault.DungeonTierFactor * tier, 13));
                break;
            default:
                return MagicResult.Fail(GameText.S("core.magic.no_spells_here"));
        }
        if (Campaign.CastFieldSpell(this, caster, spellIndex) is { } own) // II: DIVINE II
        {
            if (own.Message.Length > 0)
                Note(own.Message);
            foreach (var line in own.Log.Where(l => l != own.Message))
                Note(line);
            return own;
        }
        var result = SpellCaster.CastOutOfCombat(Party, caster, spellIndex, healTarget, _rng, location, field, Options);
        Note(result.Message);
        foreach (var line in result.Log)
            Note(line);
        if (CastCue(result, spellIndex, location, (healTarget ?? caster).Status != CharacterStatus.Dead) is { } cue)
            Cue(cue);
        // [C] (s49) OV1 0x275b: the MP is spent, then the `guild.transport_to_which_town` picker opens.
        if (result.Success && location == CastLocation.Town && spellIndex == SpellId.Transportation)
            TransportationPending = true;
        return result;
    }

    /// <summary>
    /// s139: the moment a finished cast outside combat makes (FORMAT.md 7.4): a Resurrection that raised a member
    /// (field / dungeon: II ST <c>0x92d8</c>, sound 12 after the box; town: <c>0xe7f2</c>, the "D O N E !" alert), or a
    /// town Healing on a living member (<c>0xe5ec</c>, the same alert; the ST skips a dead pick at <c>0xe50e</c>, no
    /// alert). <paramref name="targetAlive"/>: the Healing's target is not dead. The remaster shows no "D O N E !" box,
    /// only its beep `[~]`.
    /// </summary>
    public static string? CastCue(MagicResult result, int spellIndex, CastLocation location, bool targetAlive)
    {
        if (!result.Success)
            return null;
        if (spellIndex == SpellId.Resurrection)
            return result.Message.Length == 0 ? null // no one raised: the MP is spent, nothing shows
                : location == CastLocation.Town ? FieldCues.TownCastDone : FieldCues.FieldResurrected;
        return location == CastLocation.Town && targetAlive && spellIndex is >= SpellId.HealingI and <= SpellId.HealingIv
            ? FieldCues.TownCastDone
            : null;
    }

    /// <summary>
    /// D-056: the levels of the monsters that can be met here. Wilderness: the 12-monster block <c>12·T</c> of the cell's
    /// subcode (every day roll lands in it, <see cref="WildernessEncounter.RollDay"/>). Dungeon: the wandering range
    /// <c>T .. T+12</c> (<see cref="DungeonEncounters.RollWandering"/>). Ids without a record are skipped.
    /// </summary>
    private IReadOnlyList<int> MonsterLevels(int firstId, int count)
    {
        var levels = new List<int>(count);
        for (var id = firstId; id < firstId + count; id++)
        {
            try
            {
                levels.Add(_monsterRecord(id).Level);
            }
            catch (ArgumentOutOfRangeException)
            {
            }
            catch (KeyNotFoundException)
            {
            }
        }
        return levels;
    }

    /// <summary>
    /// D-078 <c>dos</c> (<c>p1.orders-reset</c>): counts DOS's resets of the repeat orders (<c>0x5b96</c>) to PARRY, on
    /// leaving town (OV1 <c>0xd0</c>) and after a party defeat (<c>0x84</c>). The front end, which keeps the orders,
    /// clears them when the count changes. Stays 0 under the manual rule (orders remain until changed). Not saved.
    /// </summary>
    public int OrdersResets { get; private set; }

    /// <summary>A town TRANSPORTATION cast is waiting for <see cref="ChooseTransportDestination"/>.</summary>
    public bool TransportationPending { get; private set; }

    /// <summary>
    /// The towns TRANSPORTATION offers, in picker order: the campaign's <see cref="ICampaign.TransportTowns"/> without the
    /// current town (D-062, user s53, manual "from one town to another"). I: DOS lists all 12 TWNS records, the current
    /// town included; entry 12 (OLYMPIA) stays: it is printed blank and is the only route to Olympus.
    /// </summary>
    /// <remarks>D-062 <c>dos</c> (<c>p1.transport-list</c>): every town, the current one included; picking it runs the full
    /// town entry, as DOS's <c>0x1954(pick)</c>.</remarks>
    /// <remarks>D2-030 <c>st</c> (<c>p2.transport-list</c>): the current town's same-plane list (<see cref="TownState.TransportDestinations"/>).</remarks>
    public IReadOnlyList<int> TransportDestinations =>
        Options.SamePlaneTransport && CurrentTown?.TransportDestinations is { } samePlane
            ? samePlane.ToList()
            : Campaign.TransportTowns.Where(id => id != CurrentTownId || Options.IsDos(RuleOptionRegistry.TransportList)).ToList();

    /// <summary>
    /// TRANSPORTATION destination (<c>endgame.md</c> §2.4, s49 [C]). The DOS picker (COM <c>0x16a2</c>, unpacked image) lists
    /// all 12 TWNS records; entry 12 (OLYMPIA, Core id 12) is printed as a blank name (<c>0x1887</c>: index 11 → DS
    /// <c>0xd07</c> = " "). Only <see cref="TransportDestinations"/> are accepted (D-062). ESC (<paramref name="townId"/>
    /// null) returns with the MP already spent. A pick sets <c>[0x63e2]</c> and runs the full town entry
    /// <c>0x1954(pick)</c> (Core <see cref="EnterTown"/>).
    /// </summary>
    public bool ChooseTransportDestination(int? townId)
    {
        if (!TransportationPending || Mode != GameMode.Town)
            return false;
        if (townId is int id && !TransportDestinations.Contains(id))
            return false;
        TransportationPending = false;
        if (townId is int t)
        {
            Cue(FieldCues.Transport); // II ST 0x262c: sound 22, then the palette fade (the pack's transport_fade, s140)
            EnterTown(t);
        }
        return true;
    }

    public TownTransactionResult BankWithdraw(Character member, long amount) =>
        NoteResult(IsPhantasie3
            ? P3Town.Withdraw(Party, member, amount, Options)
            : TownMenus.Withdraw(Party, member, amount));

    /// <summary>
    /// Bank D)EPOSIT (manual p.10, DOS OV1 <c>0x167d</c>, D-049): all carried gold is split by shares (1/2/3 per member,
    /// 0..3 for the dead), one entry per <see cref="Party.Members"/>, into the members' accounts. III pays by its
    /// <c>p3.bank</c> rule (D3-025: Apple II's fee, or I's maths under <c>no-fee</c>).
    /// </summary>
    public TownTransactionResult BankDeposit(IReadOnlyList<int> shares) =>
        NoteResult(IsPhantasie3
            ? P3Town.DepositShares(Party, shares, Options)
            : TownMenus.DepositShares(Party, shares));

    private bool IsPhantasie3 => Campaign.Game == GameId.P3;

    /// <summary>III under <c>p3.bank</c> = <c>dos</c>: the Bank offers DOS's `p3town.bank_deposit_some` and `p3town.bank_withdraw_all`.</summary>
    public bool BankIsDos => IsPhantasie3 && Options.P3DosBank();

    /// <summary>DOS III's `p3town.bank_deposit_some` (<see cref="P3Town.DosDeposit"/>); false = a bad amount, nothing changed.</summary>
    public bool DosBankDeposit(long amount)
    {
        if (!BankIsDos || !P3Town.DosDeposit(Party, amount, _rng, Options))
            return false;
        Note(GameText.F("core.p3.deposited_x", amount));
        return true;
    }

    /// <summary>DOS III's `p3town.bank_withdraw_all` (<see cref="P3Town.DosWithdrawAll"/>).</summary>
    public bool DosBankWithdrawAll()
    {
        var before = Party.GoldCarried;
        if (!BankIsDos || !P3Town.DosWithdrawAll(Party, Options))
            return false;
        Note(GameText.F("core.p3.withdrew_x", Party.GoldCarried - before));
        return true;
    }

    /// <summary>III's town Trade Items (D3-027): give and sell (manual), give only (<c>give-only</c>), or none (DOS).</summary>
    public bool CanTrade => IsPhantasie3 && !Options.P3NoTrade();

    /// <summary>True when Trade Items may also sell ("trading it to adventurer 7"), i.e. III's manual rule.</summary>
    public bool CanTradeSell => CanTrade && !Options.P3TradeGiveOnly();

    /// <summary>Trade Items, give: <paramref name="giver"/>'s item at <paramref name="slot"/> to <paramref name="receiver"/>.</summary>
    public P3TradeResult TradeGive(Character giver, int slot, Character receiver)
    {
        var id = slot >= 0 && slot < giver.InventoryItemIds.Count ? giver.InventoryItemIds[slot] : 0;
        var result = IsPhantasie3 ? P3Town.Give(giver, slot, receiver, Options, ReadyBestEquipment) : P3TradeResult.NoTrade;
        NoteTrade(result, () => GameText.F("core.p3.trade_x_to_x", ItemName(id), receiver.Name));
        return result;
    }

    /// <summary>Trade Items, sell: the item goes to the town's stock for 75 % of its price (<see cref="P3Town.SalePrice"/>).</summary>
    public P3TradeResult TradeSell(Character giver, int slot)
    {
        if (!IsPhantasie3 || CurrentTown is not { } town)
            return P3TradeResult.NoTrade;
        var id = slot >= 0 && slot < giver.InventoryItemIds.Count ? giver.InventoryItemIds[slot] : 0;
        var gold = Party.GoldCarried;
        var result = P3Town.Sell(Party, giver, slot, town, _items, Options, ReadyBestEquipment);
        NoteTrade(result, () => GameText.F("core.p3.trade_x_sold_for_x", ItemName(id), Party.GoldCarried - gold));
        SweepTreasury();
        return result;
    }

    /// <summary>The price Trade Items' sell pays for <paramref name="itemId"/> here (0 outside a town).</summary>
    public long TradeSalePrice(int itemId) =>
        CurrentTown is { } town ? P3Town.SalePrice(_items.Get(itemId)?.Value ?? 0, town.PriceIndex) : 0;

    private string ItemName(int id) => _items.Get(id)?.Name ?? $"#{id}";

    /// <summary>The move's line (remaster text) or the refusal `core.p3.cant_carry_anymore` (III string 455).</summary>
    private void NoteTrade(P3TradeResult result, Func<string> done)
    {
        switch (result)
        {
            case P3TradeResult.Done:
                Note(done());
                break;
            case P3TradeResult.ReceiverFull:
                Note(GameText.S("core.p3.cant_carry_anymore"));
                break;
        }
    }

    /// <summary>
    /// Town U)SE ITEM on an item the campaign gives its own use (<see cref="ICampaign.ItemUseOptions"/>; II: amulets, the
    /// wand of Filmon): the menu it opens (empty = no menu), or null when the item has none.
    /// </summary>
    public IReadOnlyList<string>? ItemUseOptions(Character user, int itemId) =>
        Mode == GameMode.Town ? Campaign.ItemUseOptions(this, user, itemId) : null;

    /// <summary>Use a campaign item with the 0-based menu <paramref name="option"/> (<see cref="ItemUseOptions"/>).</summary>
    public TownTransactionResult UseCampaignItem(Character user, int itemId, int option = 0)
    {
        EnsureMode(GameMode.Town);
        return NoteResult(Campaign.UseItem(this, user, itemId, option));
    }

    /// <summary>Town U)SE ITEM on a non-potion, non-scroll item: <see cref="ItemRules.Describe"/> (D-061).</summary>
    public IReadOnlyList<string> DescribeItem(int itemId) =>
        _items.Get(itemId) is { } item ? ItemRules.Describe(item) : [$"#{itemId}"];

    public TownTransactionResult Buy(Character shopper, int itemId)
    {
        var result = NoteResult(TownMenus.Buy(Party, shopper, RequireTownState(), _items, itemId, CanUseItem,
            (who, id) => Campaign.AfterArmoryBuy(this, who, id)));
        // s137: III's Armory plays 36 with "He cannot carry anymore!" (0x111e0) and "The party cannot afford it!" (0x11386)
        if (Game == GameId.P3 && result.Kind == TownTransactionKind.Failed
            && (result.Message == TownMenus.NinthItemMessage || result.Message == GameText.S("core.armory.not_enough_gold")))
            PlaySound(P3Sounds.ArmoryRefusal);
        return result;
    }

    public TownTransactionResult RestAtInn()
    {
        if (Campaign.RestAtInn(this) is { } own)
            return NoteResult(own);
        var result = NoteResult(TownMenus.Rest(Party, _rng, dosNoTime: Options.TownInnNoTime, skipDead: Options.TownInnSkipsDead));
        if (result.Kind == TownTransactionKind.Ok)
            Campaign.AfterInnStay(this);
        return result;
    }

    /// <summary>The XP / gold split (D-037). With <paramref name="items"/> false the found items stay in the sack for
    /// <see cref="BeginItemDistribution"/> (the DOS hand-out screen).</summary>
    public TownTransactionResult DistributeLoot(bool items = true) =>
        NoteResult(TownMenus.Distribute(Party, Sack, items, dropRemainder: Options.PayoutRemainderDropped));

    /// <summary>The DOS item hand-out screen in progress (<see cref="OpenPhantasie.Core.ItemDistribution"/>), or null.</summary>
    public ItemDistribution? ItemDistribution { get; private set; }

    /// <summary>D)ISTRIBUTE / Inn (OV1 <c>0x2ab3</c>): pool every carried and found item for the hand-out screen.
    /// <paramref name="foundOnly"/>: the found items alone (III's town entry, D3-071; DOS ovl2 <c>0x0000</c>(1), s143).</summary>
    public ItemDistribution? BeginItemDistribution(bool foundOnly = false)
    {
        EnsureMode(GameMode.Town);
        var town = RequireTownState();
        ItemDistribution = OpenPhantasie.Core.ItemDistribution.Begin(Party, Sack, town, _items,
            Campaign.DistributeSalePrice(town, _items), foundOnly, Gear);
        return ItemDistribution;
    }

    /// <summary>Give the current pooled item; the screen closes itself when the pool is empty.</summary>
    public bool GiveDistributedItem(DistributeTarget target)
    {
        if (ItemDistribution is not { } d)
            return false;
        var ok = d.Give(target, out var message);
        if (message.Length > 0)
            Note(message);
        SweepTreasury(); // s175: a SELL's gold
        if (d.IsDone)
            ItemDistribution = null;
        return ok;
    }

    /// <summary>Town M)YSTIC: the campaign's (I: score and readiness, <see cref="TownMenus.ConsultMystic(Party)"/>).</summary>
    public TownTransactionResult ConsultMystic() =>
        NoteResult(Campaign.ConsultMystic(this));

    /// <summary>The learn roll, taken each time a member is picked at "WHO WILL LEARN?" (<see cref="TownMenus.SpellLearnRoll"/>).</summary>
    public bool RollSpellLearn(Character member) => TownMenus.SpellLearnRoll(member, _rng);

    /// <summary>
    /// Town G)UILD learn pick (<see cref="TownMenus.LearnSpell"/>, the roll <paramref name="learned"/> taken at "WHO WILL
    /// LEARN?"). s137: III plays 6 with " It is learned successfully! " (Amiga <c>0x10902</c>) and 42 with " The spell was not
    /// learned... " (<c>0x1092c</c>); a refusal plays none.
    /// </summary>
    public TownTransactionResult LearnSpell(Character member, int spellId, bool learned)
    {
        var result = TownMenus.LearnSpell(Party, member, RequireTownState(), spellId, learned);
        Note(result.Message);
        if (Game == GameId.P3 && result.Kind == TownTransactionKind.Ok)
            PlaySound(learned ? P3Sounds.MemberLine : P3Sounds.Loss);
        return result;
    }

    /// <summary>III: training asks three skill picks (<see cref="TrainingSkillRows"/>, then <see cref="Train(Character, IReadOnlyList{P3Skill?})"/>).</summary>
    public bool TrainingAsksSkills => Campaign is Phantasie3Campaign;

    /// <summary>III's pick screen for <paramref name="member"/>'s next level: Now / After per skill after the picks so far.</summary>
    public IReadOnlyList<(P3Skill Skill, int Now, int After)> TrainingSkillRows(
        Character member, IReadOnlyList<P3Skill?> picksSoFar) =>
        Campaign is Phantasie3Campaign p3 ? p3.SkillPickRows(Options, member, member.Level + 1, picksSoFar) : [];

    /// <summary>True when <paramref name="member"/> may train now and the party can pay (no refusal would follow).</summary>
    public bool TrainingWouldSucceed(Character member) =>
        CanTrain(member, out _) && TrainingXpShortfall(member) <= 0 && Party.CanSpend(TrainingGoldCost(member));

    /// <summary>III's training with the player's three skill picks (null = an Esc'd pick, used up).</summary>
    public TownTransactionResult Train(Character member, IReadOnlyList<P3Skill?> skillPicks) =>
        Campaign is Phantasie3Campaign p3 ? NoteResult(p3.Train(this, member, skillPicks)) : Train(member);

    public TownTransactionResult Train(Character member) =>
        NoteResult(Campaign.Train(this, member)
            ?? TownMenus.Train(Party, member, RequireTownState(), CreationTables, _rng,
                dosHitDice: Options.OriginalHitDice, rules: Rules.Training,
                costHiddenWhileShort: Options.TrainingCostHidden, toss: FeatureModules.TossRock));

    /// <summary>XP <paramref name="member"/> still needs before training, by this game's rules (0 = may train).</summary>
    public long TrainingXpShortfall(Character member) =>
        Campaign.TrainingXpShortfall(this, member) ?? TownMenus.TrainingXpShortfall(member, Rules.Training);

    /// <summary>Whether <paramref name="member"/> may train now, by this game's rules (the refusal reason otherwise).</summary>
    public bool CanTrain(Character member, out string reason)
    {
        if (Campaign.CanTrain(this, member) is var (may, why))
        {
            reason = why;
            return may;
        }
        return TownMenus.CanTrain(member, out reason, Rules.Training);
    }

    /// <summary>What training <paramref name="member"/> costs in the current town, by this game's rules.</summary>
    public long TrainingGoldCost(Character member) =>
        Campaign.TrainingGoldCost(this, member) ?? TownMenus.TrainingGoldCost(member, RequireTownState(), Rules.Training);

    // --- Overworld -----------------------------------------------------------

    public OverworldMoveResult MoveOverworld(CardinalDirection dir)
    {
        EnsureMode(GameMode.Overworld);
        if (WildernessPrompt is not null)
            throw new InvalidOperationException("A wilderness encounter menu is waiting for an answer.");
        if (EntryPrompt is not null)
            throw new InvalidOperationException($"\"{EntryPrompt.Text}\" is waiting for an answer.");
        if (Campaign.MoveOverworld(this, dir) is { } own) // T4: III's planes, places and tile terrain
            return own;
        Party.DosLiving.Rebuild(Party.Members); // step handler 0x4325 (0x432a): DOS's living list, D-033 dos
        var result = _overworld.TryMove(Overworld, dir, _rng);
        var cost = result.TimeCost;
        if (cost > 0 && Options.OriginalPathTime)
            cost = TravelTimeRules.Dos.StepCost(Overworld.TerrainClass); // D-055 dos: the traced DOS table (path 60)
        TravelTime = TravelClock.Add(TravelTime, cost); // [C] OV2 0x5f88: before town entry / any dispatch
        HandleOverworldResult(result);
        return result;
    }

    /// <summary>
    /// Phantasie I's blocked-step line (<see cref="Phantasie1Campaign.BlockedMessage"/>), kept for callers without a
    /// session; the loop asks its campaign.
    /// </summary>
    public static string BlockedMessage(int regionId) => Phantasie1Campaign.Instance.BlockedMessage(regionId);

    private void HandleOverworldResult(OverworldMoveResult result)
    {
        switch (result.Kind)
        {
            case OverworldMoveKind.Blocked:
                Note(Campaign.BlockedMessage(Overworld.RegionId));
                break;
            case OverworldMoveKind.EnterTown:
                if (result.TownId is int tid)
                    AskEntry(new OverworldEntryPrompt(OverworldEntryKind.Town, tid, 0, 0)); // OV2 0x6115
                break;
            case OverworldMoveKind.EnterDungeon:
                if (result.DungeonId is int did && result.DestX is int dx && result.DestY is int dy)
                    AskEntry(new OverworldEntryPrompt(OverworldEntryKind.Dungeon, did, dx, dy)); // OV2 0x1e47, menu 0x1c
                break;
            case OverworldMoveKind.LocationText:
                if (result.LocationText is null)
                    Note(GameText.S("core.overworld.a_landmark"));
                else
                {
                    var noted = 0;
                    foreach (var line in result.LocationTextLines.Where(l => l.Length > 0))
                    {
                        Note(line); // OV2 0x5fae: one message line per page
                        noted++;
                    }
                    // s153: I Amiga shows the pages as one requester-23 box (h59 +0x182), its rows the pages as stored.
                    NoteMapWindow(LogWindowKind.Box, noted,
                        result.LocationTextRows.Count > 0 ? result.LocationTextRows : result.LocationTextLines);
                }
                break;
            case OverworldMoveKind.Swim:
                Swim(); // OV2 state 10 (0x685c): swim rolls, damage, maybe party defeat
                break;
            case OverworldMoveKind.Lava:
                BurnInLava(result); // II state 8 (0xfac2(0)): every living member burnt, maybe party defeat
                break;
            case OverworldMoveKind.WildernessInn:
                Note(SessionPrompts.WildernessInnText); // menu 0x16 — label [D✓] s45 DOSBox; yes → StayAtWildernessInn
                break;
            case OverworldMoveKind.RandomEncounter
                when NightEncounter.TryRoll(Party, result.Tile % 10, _rng, Rules.EncounterRulesOrDefault) is { } night:
                CombatReturnMode = GameMode.Overworld;
                StartEncounter(night.MonsterId, SurpriseFlags.Normal, blackKnightSpot: false, NightEncounter.Banner);
                foreach (var line in NightEncounter.ApplyAfterPrep(Party, night))
                    Note(line);
                break;
            case OverworldMoveKind.ScriptedEncounter:
            case OverworldMoveKind.TileClassFight:
                // [C] OV2 0x6045 / 0x6094: fixed monster, prep 0x3263, no surprise write (stale globals).
                if (result.MonsterId is int fixedId)
                    BeginWildernessCombat(fixedId, SurpriseGlobals);
                break;
            case OverworldMoveKind.RandomEncounter:
                ApplyWildernessOutcome(WildernessEncounter.RollDay(
                    Party, HighestScore, result.Tile / 10, result.Tile % 10, SurpriseGlobals, _rng, _wildernessEncounters, Rules.EncounterRulesOrDefault));
                break;
            case OverworldMoveKind.SpecialSite:
                Campaign.OnSpecialSite(this, result); // I: "A MAGICAL POOL" (OV2 0x656b)
                break;
            default:
                Note(GameText.F("core.overworld.moved_to_x_x_tile_x", result.X, result.Y, result.Tile));
                break;
        }
    }

    private void AskEntry(OverworldEntryPrompt prompt)
    {
        EntryPrompt = prompt;
        if (!prompt.AsksLoad) // II ST: sound 19 before "A TOWN. ENTER ?" (0x63e2) / "A DUNGEON. ENTER ?" (0x106a0)
            Cue(prompt.Kind == OverworldEntryKind.Town ? FieldCues.TownPrompt : FieldCues.DungeonPrompt);
        Note(prompt.Text);
    }

    /// <summary>
    /// Answer the pending <see cref="EntryPrompt"/>. YES enters the town or dungeon; NO leaves the party on the tile as a
    /// plain move (manual p.12; user s55: town NO = dungeon NO, OV2 menu <c>0x1c</c> NO → return 2).
    /// D3-038 <c>dos</c> (<c>p3.dungeon-map-no</c>): a YES at a dungeon asks DOS III's `core.p3.load_saved_dungeon` next
    /// (<see cref="OverworldEntryPrompt.AsksLoad"/>, ovl4 <c>0x6fe</c>); that answer enters, with the saved map on YES (when
    /// there is one) and the original on NO, which leaves the saved map in place (DOS keeps <c>DNP&lt;letter&gt;</c>).
    /// </summary>
    public void AnswerOverworldEntry(bool enter)
    {
        EnsureMode(GameMode.Overworld);
        var prompt = EntryPrompt ?? throw new InvalidOperationException("No ENTER? question is waiting.");
        EntryPrompt = null;
        if (prompt.AsksLoad)
        {
            EnterDungeon(prompt.Id, prompt.DestX, prompt.DestY, loadSaved: enter);
            return;
        }
        if (!enter)
            return;
        if (prompt.Kind == OverworldEntryKind.Town)
            EnterTown(prompt.Id);
        else if (Rules.DungeonSavePerDungeon && Options.IsDos(P3RuleOptions.DungeonMapNo))
            AskEntry(prompt with { AsksLoad = true });
        else
            EnterDungeon(prompt.Id, prompt.DestX, prompt.DestY);
    }

    /// <summary>
    /// Answer the pending <see cref="WildernessPrompt"/> (OV2 menus <c>0xf</c>/<c>0x10</c>/<c>0x11</c>). Returns the fight it
    /// starts, or null when the party gets away (RUN, LEAVE, SURRENDER).
    /// </summary>
    public CombatEncounter? AnswerWildernessPrompt(WildernessAnswer answer)
    {
        EnsureMode(GameMode.Overworld);
        var prompt = WildernessPrompt ?? throw new InvalidOperationException("No wilderness encounter menu is waiting.");
        if (Campaign.AnswerWildernessPrompt(this, prompt, answer)) // T4: III's own menus
            return Mode == GameMode.Combat ? Combat : null;
        var outcome = WildernessEncounter.Answer(prompt, answer, SurpriseGlobals, _rng, Rules.EncounterRulesOrDefault);
        WildernessPrompt = null;
        return ApplyWildernessOutcome(outcome);
    }

    private CombatEncounter? ApplyWildernessOutcome(WildernessEncounterOutcome outcome)
    {
        foreach (var line in outcome.Messages)
            Note(line);
        SurpriseGlobals = outcome.Globals;
        if (outcome.SurrenderFoundGold)
        {
            // [C] 0x72ad: [0x63f6] := 0. DOS has one purse, so bank-withdrawn gold goes too (s49 DOSBox: carried 301 → 0);
            // bank balances (@262) are untouched.
            Sack.Gold = 0;
            Party.GoldCarried = 0;
        }
        if (outcome.Prompt is { } prompt)
        {
            WildernessPrompt = prompt;
            Note(prompt.Title);
            return null;
        }
        return outcome.MonsterId is int id ? BeginWildernessCombat(id, outcome.Globals) : null;
    }

    private void ApplyWildernessSnapshot(WildernessSnapshot? w)
    {
        // Older saves: [0x6496] clear, globals (0, 0) [~], score recomputed as on a town exit, region bytes as loaded.
        Overworld.TileClassFightDone = w?.TileClassFightDone ?? false;
        HighestScore = w?.HighestScore ?? (Party.Members.Count == 0 ? 0 : Math.Max(0, Party.Members.Max(m => m.Score)));
        TownExitAverageLevel = w?.TownExitAverageLevel ?? PartyOptions.AveragePartyLevel(Party);
        SurpriseGlobals = w is null ? default : new SurpriseFlags(w.SurpriseFirstRound, w.MonsterHold);
        Party.DosLiving.Restore(w?.DosLivingEntries, w?.DosLivingWord ?? 0);
        WildernessPrompt = w?.PromptMenu is { } menu
            ? new WildernessPrompt(menu, w.PromptMonsterId, w.PromptListenerSlot, w.PromptTitle ?? "")
            : null;
        EntryPrompt = w?.EntryKind is { } kind
            ? new OverworldEntryPrompt(kind, w.EntryId, w.EntryDestX, w.EntryDestY) { Name = w.EntryName, AsksLoad = w.EntryAsksLoad }
            : null;
        if (w?.RegionTiles is null)
            return;
        foreach (var (id, tiles) in w.RegionTiles)
        {
            var region = _overworld.GetRegion(id);
            if (tiles.Length != region.Tiles.Length)
                throw new InvalidDataException($"Save has a bad tile grid for region {id}.");
            tiles.CopyTo(region.Tiles, 0);
        }
    }

    /// <summary>Stay at the independent inn the party stands on (tile 41, OV2 <c>0x6700</c>; D-005, D-008).</summary>
    public TownTransactionResult StayAtWildernessInn()
    {
        EnsureMode(GameMode.Overworld);
        if (Campaign.StayAtWildernessInn(this) is { } own) // T4: III's inns (places, 250 gold)
            return NoteResult(own);
        if (_overworld.GetRegion(Overworld.RegionId).GetNormalized(Overworld.X, Overworld.Y) != 41)
            throw new InvalidOperationException("The party is not at an inn.");
        var stay = WildernessInn.Stay(Party, Sack, _rng, Options);
        if (stay.Kind == TownTransactionKind.Ok && Options.WildernessInnTravelTime)
            TravelTime = TravelClock.Add(TravelTime, _overworld.TravelTime.WildernessInnDos); // D-008 dos: OV2 0x6827
        NoteResult(stay);
        // s154: "IN THE INN:" and the member rows are one report (I Amiga h71 +0xca2…+0xe66: the title through +0x112e, the
        // rows, then requester 21 waits for OK, h86 +0(0x15)). The log keeps DOS's one joined line; the window's rows are
        // the lines.
        if (stay.Lines.Count > 0)
            NoteMapWindow(LogWindowKind.Report, 1, stay.Lines);
        return stay;
    }

    // --- Dungeon -------------------------------------------------------------

    /// <summary>
    /// The DOS one-slot dungeon save (<c>DNG.SAV</c> + <c>DNGX.SAV</c>, s36 [C]); written by
    /// <see cref="AnswerDungeonExit"/> on a YES to `core.prompt.save_dungeon_map`. Null = empty slot. In a game
    /// that keeps one saved map per dungeon (<see cref="RulesProfile.DungeonSavePerDungeon"/>, III's <c>DNP&lt;letter&gt;</c>)
    /// this is the last one written and <see cref="DungeonSaves"/> holds them all; setting it replaces them all.
    /// </summary>
    public DungeonSaveSlot? DungeonSave
    {
        get => _lastDungeonSave is int id ? _dungeonSaves.GetValueOrDefault(id) : null;
        set
        {
            _dungeonSaves.Clear();
            _lastDungeonSave = value?.DungeonId;
            if (value is not null)
                _dungeonSaves[value.DungeonId] = value;
        }
    }

    /// <summary>Every saved dungeon map (one at most unless the game keeps one per dungeon), by dungeon id.</summary>
    public IReadOnlyDictionary<int, DungeonSaveSlot> DungeonSaves => _dungeonSaves;

    private readonly Dictionary<int, DungeonSaveSlot> _dungeonSaves = new();
    private int? _lastDungeonSave;

    /// <summary>Keep <paramref name="slot"/>: the only slot (I / II), or its dungeon's own (III).</summary>
    private void StoreDungeonSave(DungeonSaveSlot slot)
    {
        if (!Rules.DungeonSavePerDungeon)
        {
            DungeonSave = slot;
            return;
        }
        _dungeonSaves[slot.DungeonId] = slot;
        _lastDungeonSave = slot.DungeonId;
    }

    /// <summary>Forget the saved map of dungeon <paramref name="dungeonId"/> (the others stay).</summary>
    private void PurgeDungeonSave(int dungeonId)
    {
        _dungeonSaves.Remove(dungeonId);
        if (_lastDungeonSave == dungeonId)
            _lastDungeonSave = null;
    }

    /// <summary>True while `core.prompt.an_exit_take_it` waits for <see cref="AnswerDungeonExit"/> (OV2 <c>0x2d46</c>, menu <c>0x1d</c>).</summary>
    public bool DungeonExitPending { get; internal set; }

    /// <param name="loadSaved">False = the original map even when a saved one exists (III's DOS entry question answered No).</param>
    public void EnterDungeon(int levelId, int destX, int destY, bool loadSaved = true)
    {
        EntryPrompt = null;
        Dungeon ??= new DungeonState();
        // OV2 0x1e47 -> 0x57ac -> 0x570b: the slot is restored only when DNG.SAV word 0 equals this id [C].
        DungeonLevel? restored = null;
        if (loadSaved && _dungeonSaves.TryGetValue(levelId, out var slot))
        {
            restored = _dungeons.GetLevel(levelId).WithBuffer(slot.LevelBuffer);
        }
        _dungeons.Enter(Dungeon, levelId, destX, destY, restored);
        DungeonExitPending = false;
        Mode = GameMode.Dungeon;
        Campaign.OnDungeonEntered(this);
    }

    /// <summary>
    /// Answer the exit prompts (OV2 <c>0x2d46</c> [C]): <paramref name="takeIt"/> = `core.prompt.an_exit_take_it` (NO = stay on the
    /// exit cell); <paramref name="saveMap"/> = `core.prompt.save_dungeon_map` — YES overwrites the one slot
    /// with this dungeon's live buffer (<c>0x5929</c>). NO: DOS writes nothing and the slot keeps what it had; the remaster
    /// also purges the slot when it holds this same dungeon, so an older save can't come back (D-027, user s37). A slot of
    /// another dungeon is kept.
    /// </summary>
    public void AnswerDungeonExit(bool takeIt, bool saveMap)
    {
        if (!DungeonExitPending)
            throw new InvalidOperationException("No dungeon exit is waiting for an answer.");
        DungeonExitPending = false;
        if (!takeIt)
            return;
        if (Dungeon is { } d)
        {
            var level = _dungeons.LevelOf(d);
            // D-038 (user s37: DOS): the exit record decides where the party lands, not the tile it entered from.
            if (Campaign.TakeDungeonExit(this, d, level))
            {
                // T4: the campaign put the party where the exit leads (III: its planes, or the dungeon's own square)
            }
            else if (level.ExitTarget(level.Get(d.X, d.Y)) is var (region, ox, oy) && _overworld.HasRegion(region))
                (Overworld.RegionId, Overworld.X, Overworld.Y) = (region, ox, oy);
            if (saveMap)
                StoreDungeonSave(new DungeonSaveSlot(d.LevelId, level.ToBuffer()));
            else if (_dungeonSaves.ContainsKey(d.LevelId) && !Options.IsDos(RuleOptionRegistry.DungeonMapNo)
                     && !Options.Is(RuleOptionRegistry.P2DungeonMapNo, "st") && !Options.IsDos(P3RuleOptions.DungeonMapNo))
                PurgeDungeonSave(d.LevelId); // D-027 / D2-041 / D3-038 remaster rule (DOS / ST keep it: p1 / p3 dos, p2 st)
        }
        ExitDungeonToWilderness();
    }

    /// <summary>Leave without the prompts (remaster key / tests). Touches no save slot (like DOS defeat / win exits).</summary>
    public void ExitDungeonToWilderness()
    {
        Dungeon = null;
        DungeonExitPending = false;
        Overworld.TileClassFightDone = false; // [C] OV2 0x9b: state 3 → 2 (dungeon → wilderness) clears [0x6496]
        DungeonTrapPrompt = null;
        DungeonLockPrompt = null;
        PendingDungeonScript = null;
        DungeonPage = null;
        Mode = GameMode.Overworld; // no text: DOS goes straight to the overworld on either SAVE MAP answer (s58)
    }

    public void TurnDungeonLeft()
    {
        EnsureMode(GameMode.Dungeon);
        _dungeons.TurnLeft(RequireDungeon());
    }

    public void TurnDungeonRight()
    {
        EnsureMode(GameMode.Dungeon);
        _dungeons.TurnRight(RequireDungeon());
    }

    public DungeonMoveResult MoveDungeonForward()
    {
        EnsureMode(GameMode.Dungeon);
        EnsureNoDungeonPrompt();
        var d = RequireDungeon();
        var (dx, dy) = d.Facing switch
        {
            DungeonFacing.North => (0, -1),
            DungeonFacing.South => (0, 1),
            DungeonFacing.East => (1, 0),
            _ => (-1, 0),
        };
        var (tx, ty) = (d.X + dx, d.Y + dy);
        Party.DosLiving.Rebuild(Party.Members); // step handler 0x4325 (0x432a)
        var result = Campaign.StepDungeon(this, d, _dungeons.LevelOf(d), tx, ty)
                     ?? _dungeons.TryMoveForward(d, Party, _rng);
        HandleDungeonResult(result, tx, ty);
        return result;
    }

    public DungeonMoveResult MoveDungeon(CardinalDirection dir)
    {
        EnsureMode(GameMode.Dungeon);
        EnsureNoDungeonPrompt();
        var d = RequireDungeon();
        var (dx, dy) = dir switch
        {
            CardinalDirection.North => (0, -1),
            CardinalDirection.South => (0, 1),
            CardinalDirection.East => (1, 0),
            _ => (-1, 0),
        };
        var (tx, ty) = (d.X + dx, d.Y + dy);
        Party.DosLiving.Rebuild(Party.Members); // step handler 0x4325 (0x432a)
        var result = Campaign.StepDungeon(this, d, _dungeons.LevelOf(d), tx, ty) // T4: III's cell codes
                     ?? _dungeons.TryMove(d, dir, Party, _rng);
        HandleDungeonResult(result, tx, ty);
        return result;
    }

    private void HandleDungeonResult(DungeonMoveResult result, int targetX, int targetY)
    {
        if (result is { Kind: DungeonMoveKind.BlockedWall, FeatureKind: DungeonFeatureKind.Wall })
            Cue(FieldCues.DungeonWall); // II ST 0x1046c: the default script, a cell 0 and a step off the map alike
        if (!string.IsNullOrEmpty(result.Message))
            Note(result.Message!);
        // s141: a step that went back after its message (a jammed lock, h41 +0xe68 then the step back) stood on the target.
        if (result is { SteppedBack: true, Message.Length: > 0 })
            NoteDungeonMove(targetX, targetY);

        switch (result.Kind)
        {
            case DungeonMoveKind.ExitDungeon:
                DungeonExitPending = true; // `core.prompt.an_exit_take_it` -> AnswerDungeonExit
                break;
            case DungeonMoveKind.ScriptedEvent:
                Campaign.BeginDungeonEvent(this, result); // OV2 0x24b2 (I: Phantasie1Campaign)
                break;
            case DungeonMoveKind.TriggerCombat:
            case DungeonMoveKind.TriggerEncounter:
            case DungeonMoveKind.TriggerTrap:
            case DungeonMoveKind.TriggerLock:
            case DungeonMoveKind.TriggerMessage:
                HandleDungeonCell(result); // fights, trap / lock prompts (GameSession.DungeonCells.cs)
                break;
        }
    }

    // --- Combat --------------------------------------------------------------

    public CombatEncounter BeginWildernessCombat(int? monsterId = null, SurpriseFlags? surprise = null)
    {
        CombatReturnMode = GameMode.Overworld;
        return StartEncounter(monsterId, surprise, blackKnightSpot: false, DayEncounterBanner);
    }

    /// <summary>
    /// Dungeon fight; where the campaign says so, every BLACK KNIGHT group uses size factor 4 (I: inside DNG9, OV2
    /// <c>0x387e</c>).
    /// </summary>
    public CombatEncounter BeginDungeonCombat(int? monsterId = null, SurpriseFlags? surprise = null)
    {
        CombatReturnMode = GameMode.Dungeon;
        return StartEncounter(monsterId, surprise,
            blackKnightSpot: Dungeon is { } d && Campaign.IsBlackKnightSpot(d.LevelId));
    }

    /// <summary>
    /// Phantasie I's Fortress of the Black Knights (DNG9, <c>[0x64ec]</c>): <see cref="Phantasie1Campaign.BlackKnightFortressLevel"/>,
    /// kept for callers without a session; the loop asks <see cref="ICampaign.IsBlackKnightSpot"/>.
    /// </summary>
    public const int BlackKnightFortressLevel = Phantasie1Campaign.BlackKnightFortressLevel;

    /// <summary>
    /// Builds the encounter from its first monster (OV2 <c>0x34aa</c>, <see cref="EncounterBuilder"/>): groups, HP,
    /// gold and <c>L</c> come from the monster records; XP accrues per kill. Every game path passes the DOS first
    /// monster (<c>[0x64e6]</c>: wilderness roll, night, fixed and tile-class fights, dungeon cells and scripts).
    /// Without <paramref name="monsterId"/> (only the Client's debug Fight key) the monster is a random pick from the
    /// factory's stub pool.
    /// <paramref name="surprise"/> defaults to <see cref="SurpriseFlags.Normal"/>, which every random encounter sets:
    /// PARTY OPTIONS first, and in round 1 the monsters sit out pass 1. Any other opening skips the options for round 1
    /// (<see cref="CombatRoundHasOptions"/>).
    /// </summary>
    public CombatEncounter BeginCombat(int? monsterId = null, SurpriseFlags? surprise = null) =>
        StartEncounter(monsterId, surprise, blackKnightSpot: false);

    /// <summary>
    /// D-058 (user s46: manual, PDF p.14): "a message will flash on the screen: AN ENCOUNTER" when the party and the
    /// monsters meet. DOS prints it nowhere; it only flashes <see cref="NightEncounter.Banner"/> before night fights,
    /// which the remaster keeps as that fight's flash (manual silent on night). User s53: "night encounter or day
    /// encounter as appropriate" — wilderness fights that aren't night ones flash <see cref="DayEncounterBanner"/>;
    /// dungeon fights (no day or night underground) keep this one.
    /// </summary>
    public static string EncounterBanner => GameText.S("core.encounter.an_encounter");

    /// <summary>Remaster text (user s53): the day counterpart of <see cref="NightEncounter.Banner"/>.</summary>
    public static string DayEncounterBanner => GameText.S("core.encounter.a_day_encounter");

    private CombatEncounter StartEncounter(int? monsterId, SurpriseFlags? surprise, bool blackKnightSpot,
        string? banner = null)
    {
        if (Party.Members.Count == 0)
            throw new InvalidOperationException("No party for combat.");

        Note(banner ?? EncounterBanner);

        var id = monsterId ?? _wildernessMonsterIds[_rng.Next(0, _wildernessMonsterIds.Count)];
        // [C] OV2 0x352f: the build sets party slot 7's status to OK every encounter (HP untouched).
        if (Party.Elemental is { } elemental)
            elemental.Status = CharacterStatus.Ok;
        var own = Campaign.BuildEncounter(this, id, surprise ?? SurpriseFlags.Normal); // T4: III's two-group builder
        var enc = own ?? EncounterBuilder.Build(Party, id, _monsterRecord, _rng, blackKnightSpot, surprise ?? SurpriseFlags.Normal,
            Rules.EncounterRulesOrDefault);
        SurpriseGlobals = enc.Surprise;
        if (own is null)
            OnEncounterBuilt(); // 0x3316: [0x1a5a] − 1, then 1 below 5 (I's builder only; a campaign keeps its own chance)
        if (Campaign.ReplaceEncounter(this, enc) is int swap) // II: a minion already killed → a plain fight, rebuilt
            return StartEncounter(swap, SurpriseFlags.Normal, blackKnightSpot, EncounterBanner);
        enc.ElementalPrep = ElementalPrep;
        enc.Rules = Options;
        enc.Modules = FeatureModules;
        enc.Prepare();
        Campaign.OnCombatStarted(this, enc);
        Combat = enc;
        _roundMoraleDone = _roundOptionTaken = false;
        Mode = GameMode.Combat;
        Note(GameText.F("core.encounter.combat_vs_x", string.Join(", ", enc.Ranks.Select(r => $"{r.Members.Count} {r.Members[0].Name}"))));
        return enc;
    }

    public void SetPartyAction(int memberIndex, int packedAction)
    {
        EnsureMode(GameMode.Combat);
        RequireCombat().SetAction(memberIndex, packedAction);
    }

    /// <summary>Give member <paramref name="memberIndex"/> a menu order (<see cref="CombatOrders.Choose"/>).</summary>
    public int ChooseAction(int memberIndex, CombatChoice choice, int rankIndex = 0, int spellIndex = -1,
        int healTargetIndex = 0)
    {
        EnsureMode(GameMode.Combat);
        var enc = RequireCombat();
        enc.EnsurePrep();
        if ((uint)memberIndex >= (uint)Party.Members.Count)
            throw new ArgumentOutOfRangeException(nameof(memberIndex));
        var packed = enc.Modules.Choose(
            new CombatOrderRequest(
                Party.Members[memberIndex], enc.Prep[memberIndex], choice, rankIndex, spellIndex, healTargetIndex, enc.Rules,
                enc),
            _rng);
        enc.SetAction(memberIndex, packed);
        return packed;
    }

    /// <summary>III's Change Rank in the order menu (<see cref="CombatChoice.ChangeRank"/>, root <c>0xfb5f</c>).</summary>
    public void ChangeCombatRank(int memberIndex, P3PartyRank rank)
    {
        EnsureMode(GameMode.Combat);
        if ((uint)memberIndex >= (uint)Party.Members.Count)
            throw new ArgumentOutOfRangeException(nameof(memberIndex));
        P3CombatModule.ChangeRank(Party.Members[memberIndex], rank);
    }

    /// <summary>
    /// Auto-thrust living members at rank 0 — used by parity playthroughs. I's packing when I's orders are on (no RNG);
    /// otherwise the fight's modules pack the Thrust (III: a Down member thrusts too).
    /// </summary>
    public void AutoMeleeRank0()
    {
        EnsureMode(GameMode.Combat);
        var enc = RequireCombat();
        var own = !enc.Modules.IsEnabled(FeatureModuleRegistry.P1CombatOrders.Id)
                  && enc.Modules.CombatChoices.Contains(CombatChoice.Thrust);
        for (var i = 0; i < Party.Members.Count; i++)
        {
            var m = Party.Members[i];
            if (m.HpCur <= 0 || !(m.Status == CharacterStatus.Ok || (own && m.Status == CharacterStatus.Down)))
                continue;
            if (own)
                ChooseAction(i, CombatChoice.Thrust);
            else
                enc.SetAction(i, CombatAction.Thrust(0));
        }
    }

    public CombatResult ResolveCombatRound()
    {
        EnsureMode(GameMode.Combat);
        var enc = RequireCombat();
        _roundMoraleDone = _roundOptionTaken = false;
        enc.TakeRoundHits(); // hits noted before the round (an option's spell) are not this round's
        enc.TakeRoundLines();
        var result = _combat.ResolveRound(enc, _rng);
        var hits = enc.TakeRoundHits();
        var tags = enc.TakeRoundLines();
        result.Hits = hits;
        result.LineTags = tags;
        foreach (var line in result.Log)
            Note(line);

        if (!result.EncounterOver)
            return result;

        result = CloseElementalSlot(result);
        result.Hits = hits;
        result.LineTags = tags;

        // Every way out except a wipe pays the kill XP (0x3f29), e.g. TELEPORTATION (no gold).
        if (!result.PartyWiped)
        {
            DepositEncounterTotals(result.RewardXp, result.RewardGold);
        }

        CombatEncounter.WakeParty(Party); // 0x3fbb → 0x3eff
        if (!result.PartyWiped)
            OfferTreasure(enc);
        SurpriseGlobals = enc.Surprise; // stale for the next fixed encounter
        Combat = null;
        Mode = CombatReturnMode == GameMode.Dungeon && Dungeon is not null
            ? GameMode.Dungeon
            : GameMode.Overworld;

        if (result.PartyWiped)
            Note(GameText.S("core.combat.party_wiped_out"));
        else if (result.PartyWon)
            Note(GameText.S("core.combat.victory_return_to_inn"));

        AfterCombatEnded(enc, result); // state 6 / state 7 (GameSession.Endgame.cs)
        return result;
    }

    /// <summary>
    /// Top of a combat round (OV2 <c>0x3b70</c>): the monster morale check. On <see cref="MoraleOutcome.Ran"/> the
    /// fight is over and the rewards are in the sack (likewise <see cref="MoraleOutcome.EndedBySpell"/>, checked first);
    /// otherwise pick a <see cref="PartyOption"/> next.
    /// </summary>
    public MoraleOutcome StartCombatRound() => RunMoraleCheck(out _);

    /// <summary>
    /// False while the fight opens without PARTY OPTIONS (OV2 <c>0x33ab</c> → round 1 at once, every order cleared). Then
    /// call <see cref="ResolveCombatRound"/> directly; the morale check and options are refused for that round.
    /// </summary>
    public bool CombatRoundHasOptions => Combat is { OpensWithoutOrders: false };

    /// <summary>Read-only: the morale check (<see cref="StartCombatRound"/>) already ran this round.</summary>
    public bool CombatMoraleCheckedThisRound => _roundMoraleDone;

    /// <summary>Read-only: a <see cref="PartyOption"/> was already chosen this round (orders are next).</summary>
    public bool CombatOptionChosenThisRound => _roundOptionTaken;

    private MoraleOutcome RunMoraleCheck(out CombatResult? end)
    {
        EnsureMode(GameMode.Combat);
        var enc = RequireCombat();
        if (enc.OpensWithoutOrders)
            throw new InvalidOperationException("Surprise: this round has no PARTY OPTIONS.");
        if (_roundMoraleDone)
            throw new InvalidOperationException("The morale check already ran this round.");
        _roundMoraleDone = true;
        end = null;
        if (enc.SpellEnd != SpellCombatEnd.None)
        {
            end = CombatSpells.EndBySpell(enc);
            EndCombatByOption(end, forfeitsSack: false);
            return MoraleOutcome.EndedBySpell;
        }

        var log = new List<string>();
        var outcome = Campaign.MoraleCheck(this, enc, log) ?? PartyOptions.MoraleCheck(enc, _rng, Overworld.TerrainClass, log);
        foreach (var line in log)
            Note(line);
        if (outcome == MoraleOutcome.Ran)
        {
            end = PartyOptions.MonstersRan(enc);
            EndCombatByOption(end, forfeitsSack: false);
        }
        return outcome;
    }

    /// <summary>
    /// PARTY OPTIONS choice (OV2 <c>0x3b70</c>). <see cref="PartyOptionOutcome.GiveOrders"/>: set orders, then
    /// <see cref="ResolveCombatRound"/>. <see cref="PartyOptionOutcome.LoseRound"/>: resolve the round without orders.
    /// </summary>
    /// <remarks>
    /// One option per round (DOS goes straight to orders after a refusal). Runs the morale check first if the caller
    /// didn't; if the monsters run, the option is not used and the result is <see cref="PartyOptionOutcome.CombatOver"/>.
    /// </remarks>
    public PartyOptionResult ChoosePartyOption(PartyOption option)
    {
        EnsureMode(GameMode.Combat);
        if (RequireCombat().OpensWithoutOrders)
            throw new InvalidOperationException("Surprise: this round has no PARTY OPTIONS.");
        if (_roundOptionTaken)
            throw new InvalidOperationException("A party option was already chosen this round.");
        if (!_roundMoraleDone && RunMoraleCheck(out var ran) is MoraleOutcome.Ran or MoraleOutcome.EndedBySpell)
            return new PartyOptionResult { Outcome = PartyOptionOutcome.CombatOver, End = ran, Log = ran!.Log };
        _roundOptionTaken = true;
        var enc = RequireCombat();
        var result = Campaign.ChoosePartyOption(this, enc, option)
                     ?? PartyOptions.Choose(enc, option, _rng, CombatReturnMode == GameMode.Dungeon, Overworld.TerrainClass,
                         TownExitAverageLevel);
        if (result.Outcome == PartyOptionOutcome.CombatOver)
        {
            OnPartyOptionEnded(option); // BEG: [0x1a5a]++; BEG/FLEE: [0x74ac] step back
            EndCombatByOption(result.End!, result.ForfeitsSack);
        }
        else
            foreach (var line in result.Log)
                Note(line);
        return result;
    }

    /// <summary>
    /// Treasure roll and drops (the campaign's <see cref="ICampaign.RollTreasure"/>, else <see cref="TreasureDrop"/>) after
    /// the party wakes; each item waits for KEEP IT?.
    /// </summary>
    private void OfferTreasure(CombatEncounter enc)
    {
        var log = new List<string>();
        var dungeonLevel = CombatReturnMode == GameMode.Dungeon ? Dungeon?.LevelId : null;
        var offers = Campaign.RollTreasure(this, enc, log) ?? TreasureDrop.Roll(
            enc, dungeonLevel, _items.Count, _rng, log, Campaign.MayDropItem, Campaign.WithholdsDungeonTreasure)
            .Select(id => new TreasureOffer(id, null)).ToList();
        foreach (var line in log)
            Note(line);
        foreach (var offer in offers)
            AddPendingTreasure(offer.ItemId, offer.Finder);
    }

    private void EndCombatByOption(CombatResult end, bool forfeitsSack)
    {
        foreach (var line in end.Log)
            Note(line);
        if (CloseElementalSlot(end).PartyWiped)
        {
            Note(GameText.S("core.combat.party_wiped_out"));
        }
        else
        {
            long? begPurse = null;
            if (forfeitsSack)
            {
                begPurse = Sack.Gold + Party.GoldCarried;
                Sack.ForfeitToBeggars();
                Party.GoldCarried = 0; // [D✓] s65: one DOS purse — bank-withdrawn gold goes too (as surrender, 0x72ad)
            }
            DepositEncounterTotals(end.RewardXp, end.RewardGold, begPurse);
        }
        CombatEncounter.WakeParty(Party); // every exit runs 0x3f29 → 0x3fbb → 0x3eff
        var enc = RequireCombat();
        if (!end.PartyWiped)
            OfferTreasure(enc);
        SurpriseGlobals = enc.Surprise; // stale for the next fixed encounter
        Combat = null;
        Mode = CombatReturnMode == GameMode.Dungeon && Dungeon is not null
            ? GameMode.Dungeon
            : GameMode.Overworld;
        AfterCombatEnded(enc, end); // state 6 / state 7 (GameSession.Endgame.cs)
    }

    /// <summary>
    /// Fight until the encounter ends. Living members keep thrusting rank 0.
    /// If neither side can finish the fight within <paramref name="maxRounds"/>,
    /// the party withdraws (no loot) instead of throwing.
    /// </summary>
    public CombatResult FightToEnd(int maxRounds = 200)
    {
        CombatResult? last = null;
        for (var r = 0; r < maxRounds; r++)
        {
            if (CombatRoundHasOptions)
            {
                if (!_roundMoraleDone && RunMoraleCheck(out var ran) is MoraleOutcome.Ran or MoraleOutcome.EndedBySpell)
                    return ran!;
                AutoMeleeRank0();
            }
            last = ResolveCombatRound();
            if (last.EncounterOver)
                return last;
        }

        // Stalemate (e.g. L1 party vs high-defense spawn) — withdraw cleanly.
        Note(GameText.S("core.combat.neither_side_prevails"));
        Party.DismissElemental();
        CombatEncounter.WakeParty(Party);
        if (Combat is { } stalled)
            SurpriseGlobals = stalled.Surprise;
        Combat = null;
        Mode = CombatReturnMode == GameMode.Dungeon && Dungeon is not null
            ? GameMode.Dungeon
            : GameMode.Overworld;

        return new CombatResult
        {
            EncounterOver = true,
            PartyWon = false,
            PartyWiped = false,
            Log = new[] { GameText.S("core.combat.stalemate_withdrew") },
        };
    }

    /// <summary>
    /// Every end of combat (OV2 <c>0x3f29</c>): party size 7 → 6 first (<c>0x3f2e</c>), so a summoned elemental goes
    /// back to slot 7, shares no XP/gold and isn't counted as a survivor. If all six adventurers are DEAD the result is
    /// a party defeat (state 6, <c>0x3fb2</c>) even when the elemental and nobody else is left standing; defeat also
    /// removes the elemental (OV1 <c>0x3bc</c>).
    /// </summary>
    /// <summary>
    /// End-of-combat payout, D-053 (user s46: manual, PDF p.15): the encounter's whole XP and gold are shown and go
    /// into the pool; <see cref="TownMenus.Distribute"/> divides them on town entry. DOS instead pays
    /// XP / (10·n) · 10 and gold / n per survivor here and drops the remainders (OV2 <c>0x3fbe</c>,
    /// <c>combat-resolution.md</c> §Outputs). Call after <see cref="CloseElementalSlot"/> and before the wake-up.
    /// </summary>
    /// <param name="begPurse">BEG FOR MERCY: the purse the monsters took (DOS's gold reward is its negative).</param>
    private void DepositEncounterTotals(long xp, long gold, long? begPurse = null)
    {
        if (Options.SurvivorShares)
        {
            DepositSurvivorShares(xp, gold, begPurse);
            BankFieldGoldIfSafe();
            return;
        }
        Sack.DepositRewards(xp, gold);
        Note(GameText.F("core.combat.x_experience_points_and_x_gold", xp, gold));
        BankFieldGoldIfSafe();
    }

    /// <summary>
    /// D-053 <c>dos</c> (<c>p1.combat-shares</c>, OV2 <c>0x3fbe</c> [C]): with <c>n</c> non-DEAD adventurers, show
    /// <c>⌊xp/(10n)⌋·10</c> XP and <c>⌊gold/n⌋</c> GP `core.combat.each_survivor_gets` and pool only <c>n</c> such shares; the remainders
    /// are lost. After BEG the gold is <c>−purse</c>, so the purse keeps <c>purse mod n</c> (DOSBox s65: 556 GP, 6 survivors →
    /// 4 left; kept here as found gold [~]) and the screen shows <c>−⌊purse/n⌋</c>.
    /// </summary>
    private void DepositSurvivorShares(long xp, long gold, long? begPurse)
    {
        var n = CombatEncounter.Survivors(Party);
        if (n == 0)
            return; // state 6: the caller reports the wipe
        var s = SurvivorShares(xp, gold, n, begPurse);
        Note(GameText.F("core.combat.x_xp_and_x_gp", s.XpEach, s.GoldEach));
        Note(GameText.S("core.combat.each_survivor_gets"));
        Sack.DepositRewards(s.PooledXp, s.PooledGold);
    }

    /// <summary>
    /// DOS's end-of-combat shares (OV2 <c>0x3fbe..0x407a</c>, D-053 <c>dos</c>) for <paramref name="survivors"/> ≥ 1: the
    /// shown share each and what goes into the pool (<c>n</c> shares; after BEG, the purse's <c>mod n</c>).
    /// </summary>
    public static (long XpEach, long GoldEach, long PooledXp, long PooledGold) SurvivorShares(
        long xp, long gold, int survivors, long? begPurse = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(survivors, 1);
        var xpEach = xp / (10L * survivors) * 10;
        if (begPurse is { } purse)
            return (xpEach, -(purse / survivors), xpEach * survivors, purse % survivors);
        var goldEach = gold / survivors;
        return (xpEach, goldEach, xpEach * survivors, goldEach * survivors);
    }

    private CombatResult CloseElementalSlot(CombatResult result)
    {
        Party.DismissElemental();
        Party.DosLiving.Recount(Party.Members); // 0x3fa9: survivor count over party size 6 (D-033 dos reads the word)
        if (!result.PartyWiped && Party.Members.Count > 0
            && Party.Members.TrueForAll(m => m.Status == CharacterStatus.Dead))
        {
            result = new CombatResult { EncounterOver = true, PartyWiped = true, Log = result.Log };
        }
        if (result.PartyWiped)
            Party.Elemental = null;
        return result;
    }

    // --- Save / load (Phase 7 — any mode, no town/inn gate) -------------------

    /// <summary>Capture full campaign state for remaster JSON save.</summary>
    public SessionSnapshot CaptureSnapshot()
    {
        var partyIndices = new List<int>();
        foreach (var member in Party.Adventurers)
        {
            var idx = Array.IndexOf(Roster.Slots, member);
            if (idx < 0)
                throw new InvalidOperationException($"Party member '{member.Name}' is not on the guild roster.");
            partyIndices.Add(idx);
        }

        var slots = new Character?[Roster.Slots.Length];
        for (var i = 0; i < Roster.Slots.Length; i++)
            slots[i] = Roster.Slots[i]?.Clone();

        CombatSnapshot? combatSnap = null;
        if (Combat is not null)
        {
            combatSnap = new CombatSnapshot
            {
                RewardXp = Combat.RewardXp,
                RewardGold = Combat.RewardGold,
                MonsterLevel = Combat.MonsterLevel,
                TreasureItemIds = Combat.TreasureItemIds.ToArray(),
                FirstMonsterId = Combat.FirstMonsterId,
                ReactionSum = Combat.ReactionSum,
                ThreatenFactor = Combat.ThreatenFactor,
                MonsterDamaged = Combat.MonsterDamaged,
                MonstersLoseRound = Combat.MonstersLoseRound,
                SpellEnd = Combat.SpellEnd,
                Dissolved = Combat.Dissolved,
                DispellUndeadTried = Combat.DispellUndeadTried,
                Prep = Combat.Prep.Select(p => new CombatPrepSnapshot
                {
                    ArmorEff = p.ArmorEff,
                    WeaponDamageEff = p.WeaponDamageEff,
                    WeaponHitEff = p.WeaponHitEff,
                    PackedAction = p.PackedAction,
                }).ToList(),
                SurpriseFirstRound = Combat.Surprise.FirstRound,
                MonsterHold = Combat.Surprise.MonsterHold,
                OpensWithoutOrders = Combat.OpensWithoutOrders,
                OrdersSkipped = Combat.OrdersSkipped,
                MoraleCheckedThisRound = _roundMoraleDone,
                OptionChosenThisRound = _roundOptionTaken,
                Monsters = Combat.Ranks.SelectMany(r => r.Members.Select(m => new CombatMonsterSnapshot
                {
                    Id = m.Id,
                    Group = r.RankIndex,
                    HitPoints = m.HitPoints ?? 0,
                    Armor = m.Armor,
                    Defense = m.Defense,
                    AttackPower = m.AttackPower,
                    StatusFlags = m.StatusFlags,
                })).ToList(),
            };
            Campaign.OnCombatCaptured(this, Combat, combatSnap); // III: its fight state (P3Fight)
        }

        return new SessionSnapshot
        {
            Version = SessionSnapshot.CurrentVersion,
            Game = Game,
            Mode = Mode,
            CombatReturnMode = CombatReturnMode,
            CurrentTownId = CurrentTownId,
            RosterSlots = slots,
            PartySlotIndices = partyIndices.ToArray(),
            PartyGold = Party.GoldCarried,
            Elemental = Party.Elemental?.Clone(),
            ElementalSummoned = Party.ElementalSummoned,
            OverworldRegionId = Overworld.RegionId,
            OverworldX = Overworld.X,
            OverworldY = Overworld.Y,
            OverworldTerrainClass = Overworld.TerrainClass,
            TravelTime = TravelTime,
            Wilderness = new WildernessSnapshot
            {
                TileClassFightDone = Overworld.TileClassFightDone,
                HighestScore = HighestScore,
                TownExitAverageLevel = TownExitAverageLevel,
                SurpriseFirstRound = SurpriseGlobals.FirstRound,
                MonsterHold = SurpriseGlobals.MonsterHold,
                DosLivingEntries = [.. Party.DosLiving.Entries],
                DosLivingWord = Party.DosLiving.Word,
                PromptMenu = WildernessPrompt?.Menu,
                PromptMonsterId = WildernessPrompt?.MonsterId ?? 0,
                PromptListenerSlot = WildernessPrompt?.ListenerSlot,
                PromptTitle = WildernessPrompt?.Title,
                EntryKind = EntryPrompt?.Kind,
                EntryId = EntryPrompt?.Id ?? 0,
                EntryDestX = EntryPrompt?.DestX ?? 0,
                EntryDestY = EntryPrompt?.DestY ?? 0,
                EntryName = EntryPrompt?.Name,
                EntryAsksLoad = EntryPrompt?.AsksLoad ?? false,
                RegionTiles = _overworld.Regions.ToDictionary(r => r.RegionId, r => r.Tiles.ToArray()),
            },
            Dungeon = Dungeon is null
                ? null
                : new DungeonSnapshot
                {
                    LevelId = Dungeon.LevelId,
                    X = Dungeon.X,
                    Y = Dungeon.Y,
                    Facing = Dungeon.Facing,
                    LevelBuffer = _dungeons.LevelOf(Dungeon).ToBuffer(),
                    ExitPending = DungeonExitPending,
                    PrevX = Dungeon.PrevX,
                    PrevY = Dungeon.PrevY,
                    WanderingChance = Dungeon.WanderingChance,
                    LastMessageId = Dungeon.LastMessageId,
                    TrapPrompt = DungeonTrapPrompt is { } t
                        ? new DungeonTrapPromptSnapshot
                        {
                            TrapKind = t.TrapKind, X = t.X, Y = t.Y, SpotterIndex = t.SpotterIndex, Message = t.Message,
                            Disarmers = t.Disarmers.ToArray(),
                        }
                        : null,
                    LockListener = DungeonLockPrompt?.ListenerIndex,
                    LockMessage = DungeonLockPrompt?.Message,
                    EventFeature = DungeonEventPrompt?.Feature,
                    EventMessageId = DungeonEventPrompt?.MessageId,
                    EventLines = DungeonEventPrompt?.Message.Lines.ToArray(),
                    EventOptions = DungeonEventPrompt?.Message.Options.ToArray(),
                    ScriptResume = PendingDungeonScript,
                    PageLines = DungeonPage?.Lines.ToArray(),
                    PageWaits = DungeonPage?.WaitsForKey ?? false,
                },
            DungeonSave = DungeonSave is null
                ? null
                : new DungeonSaveSnapshot
                {
                    DungeonId = DungeonSave.DungeonId,
                    LevelBuffer = (byte[])DungeonSave.LevelBuffer.Clone(),
                    ScreenImage = (byte[])DungeonSave.ScreenImage.Clone(),
                },
            OtherDungeonSaves = _dungeonSaves.Values.Where(s => s.DungeonId != _lastDungeonSave).OrderBy(s => s.DungeonId)
                .Select(s => new DungeonSaveSnapshot
                {
                    DungeonId = s.DungeonId,
                    LevelBuffer = (byte[])s.LevelBuffer.Clone(),
                    ScreenImage = (byte[])s.ScreenImage.Clone(),
                }).ToArray(),
            Sack = new LootSackSnapshot
            {
                Experience = Sack.Experience,
                Gold = Sack.Gold,
                ItemIds = Sack.ItemIds.ToArray(),
                TownExitItemIds = Sack.TownExitItemIds?.ToArray(),
            },
            Combat = combatSnap,
            PendingTreasure = _pendingTreasure.ToArray(),
            PendingTreasureFinders = _pendingFinders.ToArray(),
            PartyDefeatPending = PartyDefeatPending,
            Towns = _towns.Values.OrderBy(t => t.Id).Select(t => t.Clone()).ToArray(),
            OtherParties = _otherParties.ToArray(),
            Options = GameOptionsSnapshot.From(Options),
            Treasury = Treasury.ToSnapshot(), // s175: top level only
        };
    }

    /// <summary>
    /// Restore campaign state from a snapshot. Safe from any mode — replaces
    /// roster/party/world. Town stock is rebuilt from the town factory if needed.
    /// </summary>
    public void ApplySnapshot(SessionSnapshot snap)
    {
        ArgumentNullException.ThrowIfNull(snap);
        if (snap.Version is < 1 or > SessionSnapshot.CurrentVersion)
            throw new InvalidDataException($"Unsupported save version {snap.Version}.");
        if (snap.Game != Game)
            throw new InvalidDataException(
                $"This save belongs to {snap.Game.Prefix()}, not {Game.Prefix()}.");
        if (snap.Version < 2 && snap.Combat is { Monsters.Count: > 0 })
            throw new InvalidDataException(
                "This save was made mid-combat by an older version with wrong monster stats and can't be loaded.");
        ItemDistribution = null;
        // T5.0 §5: the save's own rules; a save written before s73 has none and plays Original (manual). Set only once
        // the save is accepted, so a refused load leaves the running game's rules alone.
        Options = snap.Options?.ToOptions(Game) ?? GameOptions.ForPreset(Game);

        Roster.Reset(snap.RosterSlots.Length); // a save under guild-limit = none may hold more than the game's slots
        Party.Members.Clear();
        Party.Elemental = null;
        Sack.Clear();
        ClearPendingTreasure();
        Combat = null;
        Dungeon = null;
        DungeonExitPending = false;
        DungeonEventPrompt = null;
        DungeonTrapPrompt = null;
        DungeonLockPrompt = null;
        PendingDungeonScript = null;
        DungeonPage = null;
        PartyDefeatPending = false;
        PendingRewardScene = null; // shown only right after the fight (not saved; the reward itself is in the save)
        PendingVictoryScene = null;
        CurrentTown = null;
        CurrentTownId = null;
        _roundMoraleDone = _roundOptionTaken = false; // P0.5: a load must not inherit the old session's round state
        _towns.Clear();
        _otherParties.Clear();
        _otherParties.AddRange(snap.OtherParties);
        foreach (var town in snap.Towns)
            _towns[town.Id] = StaticTownTemplate(town.Id) is { } template ? town.WithStaticDataFrom(template) : town.Clone();

        var count = Math.Min(snap.RosterSlots.Length, Roster.Slots.Length);
        for (var i = 0; i < count; i++)
            Roster.Slots[i] = snap.RosterSlots[i]?.Clone();

        Party.GoldCarried = snap.PartyGold;
        Treasury = Treasury.From(snap.Treasury); // s175: top level only, shared by every party
        BindTreasury();
        foreach (var idx in snap.PartySlotIndices)
        {
            if (idx < 0 || idx >= Roster.Slots.Length || Roster.Slots[idx] is null)
                throw new InvalidDataException($"Save references missing roster slot {idx}.");
            Party.Members.Add(Roster.Slots[idx]!);
        }
        Party.Elemental = snap.Elemental?.Clone();

        Overworld.RegionId = snap.OverworldRegionId;
        Overworld.X = snap.OverworldX;
        Overworld.Y = snap.OverworldY;
        Overworld.TerrainClass = snap.OverworldTerrainClass;
        TravelTime = snap.TravelTime;
        ApplyWildernessSnapshot(snap.Wilderness);

        if (snap.Dungeon is { } d)
        {
            Dungeon = new DungeonState
            {
                LevelId = d.LevelId,
                X = d.X,
                Y = d.Y,
                Facing = d.Facing,
                PrevX = d.PrevX ?? d.X,
                PrevY = d.PrevY ?? d.Y,
                WanderingChance = d.WanderingChance ?? DungeonEncounters.ChanceOnEntry,
                LastMessageId = d.LastMessageId ?? 0,
            };
            // Older saves have no buffer (their ClearedEventKeys are dropped): the level restarts from the original.
            if (d.LevelBuffer is { } buf && buf.Length == _dungeons.GetLevel(d.LevelId).BufferLength)
            {
                Dungeon.Level = _dungeons.GetLevel(d.LevelId).WithBuffer(buf);
            }
            else
            {
                Dungeon.Level = _dungeons.GetLevel(d.LevelId).CloneAsBuffer();
            }
            DungeonExitPending = d.ExitPending;
            if (d.TrapPrompt is { } t)
                DungeonTrapPrompt = new PendingDungeonTrap(t.TrapKind, t.X, t.Y, t.SpotterIndex, t.Message)
                    { Disarmers = t.Disarmers };
            if (d.LockListener is { } listener)
                DungeonLockPrompt = new PendingDungeonLock(listener, d.LockMessage ?? "");
            if (d.EventFeature is { } feature && d.EventMessageId is { } messageId)
            {
                // s95: the saved lines and choices first (synthetic prompts, padded menus); older saves: the level message.
                // s156: the stored option cells come back from the level when the saved choices are its message's.
                if (d.EventOptions is { Length: > 0 } options)
                    DungeonEventPrompt = new PendingDungeonEvent((byte)feature, messageId,
                        new DungeonMessage(d.EventLines ?? [], options)
                        {
                            OptionCells = Dungeon.Level.Messages.TryGetValue(messageId, out var stored)
                                          && stored.Options.SequenceEqual(options) ? stored.OptionCells : null,
                        });
                else if (Dungeon.Level.Messages.TryGetValue(messageId, out var msg))
                    DungeonEventPrompt = new PendingDungeonEvent((byte)feature, messageId, msg);
            }
            PendingDungeonScript = d.ScriptResume;
            if (d.PageLines is { } pageLines)
                DungeonPage = new DungeonTextPage(pageLines, d.PageWaits);
        }
        PartyDefeatPending = snap.PartyDefeatPending;
        DungeonSave = null;
        foreach (var other in snap.OtherDungeonSaves) // III: the other dungeons' saved maps
            _dungeonSaves[other.DungeonId] = new DungeonSaveSlot(other.DungeonId, other.LevelBuffer, other.ScreenImage);
        if (snap.DungeonSave is { } ds)
        {
            _dungeonSaves[ds.DungeonId] = new DungeonSaveSlot(ds.DungeonId, ds.LevelBuffer, ds.ScreenImage);
            _lastDungeonSave = ds.DungeonId;
        }

        Sack.Experience = snap.Sack.Experience;
        Sack.Gold = snap.Sack.Gold;
        Sack.ItemIds.AddRange(snap.Sack.ItemIds);
        Sack.TownExitItemIds = snap.Sack.TownExitItemIds?.ToArray();
        for (var i = 0; i < snap.PendingTreasure.Length; i++)
            AddPendingTreasure(snap.PendingTreasure[i], i < snap.PendingTreasureFinders.Length ? snap.PendingTreasureFinders[i] : null);

        CombatReturnMode = snap.CombatReturnMode;
        CurrentTownId = snap.CurrentTownId;
        if (snap.CurrentTownId is int tid)
            CurrentTown = TownStateFor(tid);

        if (snap.Combat is { } c && c.Monsters.Count > 0)
        {
            var enc = new CombatEncounter(Party)
            {
                RewardXp = c.RewardXp,
                RewardGold = c.RewardGold,
                TreasureItemIds = c.TreasureItemIds.ToArray(),
                FirstMonsterId = c.FirstMonsterId ?? -1,
                ReactionSum = c.ReactionSum,
                ThreatenFactor = c.ThreatenFactor ?? PartyOptions.ThreatenFactorStart,
                MonsterDamaged = c.MonsterDamaged,
                MonstersLoseRound = c.MonstersLoseRound,
                SpellEnd = c.SpellEnd,
                Dissolved = c.Dissolved,
                DispellUndeadTried = c.DispellUndeadTried,
                Surprise = new SurpriseFlags(c.SurpriseFirstRound, c.MonsterHold),
                OpensWithoutOrders = c.OpensWithoutOrders,
                OrdersSkipped = c.OrdersSkipped,
            };
            // Saves without group ids (before the builder) held one monster per rank.
            var legacy = c.Monsters.Any(m => m.Group is null);
            MonsterRank? rank = null;
            for (var i = 0; i < c.Monsters.Count; i++)
            {
                var m = c.Monsters[i];
                var mon = _monsterRecord(m.Id);
                mon.HitPoints = m.HitPoints;
                mon.Armor = m.Armor;
                mon.Defense = m.Defense;
                mon.AttackPower = m.AttackPower;
                mon.StatusFlags = m.StatusFlags;
                var group = legacy ? i : m.Group!.Value;
                if (rank is null || rank.RankIndex != group)
                {
                    rank = new MonsterRank { RankIndex = group };
                    enc.Ranks.Add(rank);
                }
                rank.Members.Add(mon);
            }
            enc.MonsterLevel = c.MonsterLevel ?? enc.Ranks.Sum(r => r.Members[0].Level);
            enc.ElementalPrep = ElementalPrep;
            enc.Rules = Options;
            enc.Modules = FeatureModules;
            if (snap.ElementalSummoned && Party.Elemental is not null)
                Party.Members.Add(Party.Elemental);

            // Defense / shield are rebuilt from the record (nothing changes them mid-fight); the effective fields are
            // restored on top, as DOS keeps them until the next prep (0x3263). Older saves have no Prep → prep values.
            enc.Prepare(fightStart: false);
            if (c.Prep is { } prep && prep.Count == enc.Prep.Length)
            {
                for (var i = 0; i < prep.Count; i++)
                {
                    enc.Prep[i].ArmorEff = prep[i].ArmorEff;
                    enc.Prep[i].WeaponDamageEff = prep[i].WeaponDamageEff;
                    enc.Prep[i].WeaponHitEff = prep[i].WeaponHitEff;
                    enc.Prep[i].PackedAction = prep[i].PackedAction;
                }
            }
            Campaign.OnCombatRestored(this, enc, c); // III: its fight state (P3Fight)
            Combat = enc;
            _roundMoraleDone = c.MoraleCheckedThisRound;
            _roundOptionTaken = c.OptionChosenThisRound;
        }

        Mode = snap.Mode;
        if (Mode == GameMode.Combat && Combat is null)
            Mode = CombatReturnMode == GameMode.Dungeon && Dungeon is not null
                ? GameMode.Dungeon
                : GameMode.Overworld;
        SweepTreasury(TreasuryEntryKind.Trip); // s175: accounts (and, in town, the purse) of a save made before or without it

        Note(GameText.S("core.session.game_loaded"));
    }

    // --- helpers -------------------------------------------------------------

    private TownState RequireTownState() =>
        CurrentTown ?? throw new InvalidOperationException("Not in a town.");

    internal DungeonState RequireDungeon() =>
        Dungeon ?? throw new InvalidOperationException("Not in a dungeon.");

    // --- campaign access (T1.4): what CampaignAccess hands a campaign hook beyond the public API ---

    /// <summary>The session's DOS random source (one stream: campaign hooks draw from it in DOS order).</summary>
    internal IDosRandomSource Rng => _rng;

    /// <summary>
    /// The race / class tables this save plays by (creation, training, race maxima for the magical pool): the loaded
    /// tables with the save's DOS table options applied (D-001 <c>p1.halfling-dex</c>, D-003 <c>p1.lifespans</c>, D-006
    /// <c>p1.monk-sleep</c>; <see cref="CharacterCreationTables.ForRules"/>).
    /// </summary>
    public CharacterCreationTables CreationTables => _rulesCreation;

    private CharacterCreationTables _rulesCreation = null!; // set with Options in the constructor

    /// <summary>The live level the party is in (throws outside a dungeon).</summary>
    internal DungeonLevel CurrentDungeonLevel => _dungeons.LevelOf(RequireDungeon());

    /// <summary>Queue a "YOU FIND %s. KEEP IT?" offer (<see cref="PendingTreasure"/>).</summary>
    internal void AddPendingTreasure(int itemId) => AddPendingTreasure(itemId, null);

    private CombatEncounter RequireCombat() =>
        Combat ?? throw new InvalidOperationException("Not in combat.");

    private void EnsureMode(GameMode expected)
    {
        if (Mode != expected)
            throw new InvalidOperationException($"Expected {expected}, but mode is {Mode}.");
    }

    private TownTransactionResult NoteResult(TownTransactionResult result)
    {
        Note(result.Message);
        SweepTreasury(); // s175: level gold, Distribute's gold, anything a town transaction added
        return result;
    }
}
