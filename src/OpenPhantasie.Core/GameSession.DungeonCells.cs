namespace OpenPhantasie.Core;

/// <summary>
/// P0.4 (s39): dungeon fight cells, traps, locks and swimming wired into the session (dungeon-representation.md
/// §Fight cells / §Traps / §Locks, overworld-movement.md §Swimming). DOS menus are modal; here each question is a pending
/// prompt with its own Answer method, and a step drops an unanswered one (<see cref="EnsureNoDungeonPrompt"/>).
/// </summary>
public sealed partial class GameSession
{
    /// <summary>"%s SPOTS A TRAP / D)ISARM OR L)EAVE" is waiting: answer with <see cref="AnswerDungeonTrap"/>.</summary>
    public PendingDungeonTrap? DungeonTrapPrompt { get; private set; }

    /// <summary>"%s HEARS MONSTERS. GO BACK ?" is waiting: answer with <see cref="AnswerDungeonLock"/>.</summary>
    public PendingDungeonLock? DungeonLockPrompt { get; internal set; }

    /// <summary>A spotted trap of kind <paramref name="TrapKind"/> on cell (X, Y); <paramref name="SpotterIndex"/> = party index.</summary>
    public sealed record PendingDungeonTrap(int TrapKind, int X, int Y, int SpotterIndex, string Message)
    {
        /// <summary>Party indices "WHO WILL DISARM ?" offers (<c>0x5a0a</c> mode 1: status OK only).</summary>
        public IReadOnlyList<int> Disarmers { get; init; } = Array.Empty<int>();
    }

    /// <summary>A lock ambush heard by <paramref name="ListenerIndex"/> on the cell the party stands on.</summary>
    public sealed record PendingDungeonLock(int ListenerIndex, string Message);

    /// <summary>BEG or FLEE ended the current dungeon fight: <c>[0x74ac]</c> → step back after it (<c>0x6255</c>).</summary>
    private bool _stepBackAfterCombat;

    /// <summary>
    /// Answer "D)ISARM OR L)EAVE" (D-052): <paramref name="disarmerIndex"/> null = LEAVE (or no one chosen at "WHO WILL DISARM ?"): the party
    /// steps back and the trap stays. Otherwise the member (status OK) tries; a failure springs the trap.
    /// </summary>
    public DungeonMoveResult AnswerDungeonTrap(int? disarmerIndex)
    {
        var prompt = DungeonTrapPrompt ?? throw new InvalidOperationException("No trap is waiting for an answer.");
        var dungeon = RequireDungeon();
        if (disarmerIndex is int i && !prompt.Disarmers.Contains(i))
            throw new ArgumentOutOfRangeException(nameof(disarmerIndex), "Only an OK member can disarm.");
        DungeonTrapPrompt = null;
        var result = DungeonTraversal.ResolveSpottedTrap(dungeon, _dungeons.LevelOf(dungeon), prompt.TrapKind,
            prompt.X, prompt.Y, disarmerIndex, Party, _rng, Rules.EncounterRulesOrDefault.Roll);
        if (result.DisarmFailed)
            Cue(FieldCues.DisarmFails); // II ST 0x10e9c: sound 29 before "%s FAILS"
        foreach (var line in result.Log)
            Note(line);
        NoteMapWindows(result.Log, result.Windows);
        CheckDungeonWipe();
        return result;
    }

    /// <summary>Answer "GO BACK ?": YES steps back with no fight; NO starts the lock ambush.</summary>
    public DungeonMoveResult AnswerDungeonLock(bool goBack)
    {
        _ = DungeonLockPrompt ?? throw new InvalidOperationException("No lock ambush is waiting for an answer.");
        var dungeon = RequireDungeon();
        DungeonLockPrompt = null;
        var result = DungeonTraversal.ResolveHeardMonsters(dungeon, _dungeons.LevelOf(dungeon), goBack, _rng,
            Rules.EncounterRulesOrDefault.Roll);
        if (result.MonsterId is int id)
            BeginDungeonCombat(id, result.Surprise);
        return result;
    }

    /// <summary>Session side of a dungeon step: log lines, fights, trap/lock prompts, wipes.</summary>
    private void HandleDungeonCell(DungeonMoveResult result)
    {
        foreach (var line in result.Log.Skip(string.IsNullOrEmpty(result.Message) ? 0 : 1))
            Note(line);
        NoteMapWindows(result.Log, result.Windows); // the Message (Log[0]) was noted by the step
        if (result.MonsterId is int monster)
        {
            BeginDungeonCombat(monster, result.Surprise);
            return;
        }
        if (result.SpotterIndex is int spotter && Dungeon is { } d)
            OfferTrap(result.FeatureByte, d.X, d.Y, spotter, result.Message);
        else if (result.ListenerIndex is int listener)
            DungeonLockPrompt = new PendingDungeonLock(listener, result.Message ?? "");
        CheckDungeonWipe();
    }

    /// <summary>An event ran a trap (I: script op 16): a spotted one's prompt targets the event cell.</summary>
    internal void OfferEventTrap(int trapKind, int x, int y, DungeonMoveResult trap)
    {
        if (trap is { SpotterIndex: int spotter })
            OfferTrap(trapKind, x, y, spotter, trap.Message);
        else
        {
            foreach (var line in trap.Log.Skip(string.IsNullOrEmpty(trap.Message) ? 0 : 1)) // Message already noted
                Note(line);
            NoteMapWindows(trap.Log, trap.Windows);
        }
    }

    private void OfferTrap(int trapKind, int x, int y, int spotter, string? message)
    {
        var disarmers = Enumerable.Range(0, Party.Members.Count)
            .Where(i => DungeonTraps.CanDisarm(Party.Members[i]))
            .ToArray();
        DungeonTrapPrompt = new PendingDungeonTrap(trapKind, x, y, spotter, message ?? "") { Disarmers = disarmers };
    }

    /// <summary>
    /// Every encounter build (<c>0x3316</c>): the wandering chance drops (7 → 6 → 5 → 1 …). DOS also does it for wilderness
    /// fights, but dungeon entry resets the chance, so only dungeon builds matter.
    /// </summary>
    private void OnEncounterBuilt()
    {
        if (Dungeon is { } d)
            d.WanderingChance = DungeonEncounters.AfterBuild(d.WanderingChance);
    }

    /// <summary>
    /// End of a dungeon fight: <c>0x2a26</c> clears the fight cell unless BEG / GREET / FLEE / TELEPORTATION zeroed
    /// <c>[0x74b4]</c> (Core <see cref="CombatEncounter.TreasureForfeited"/>); then BEG / FLEE step back (<c>0x29a9</c>,
    /// <c>[0x74ac]</c>). A wipe does neither. CHARM's end is a remaster rule (D-022; DOS CHARM never ends a fight and never
    /// touches <c>[0x74b4]</c>/<c>[0x74ac]</c>). It is modelled on GREETINGS, which zeroes <c>[0x74b4]</c> (<c>0x3d12</c>)
    /// and not <c>[0x74ac]</c>: no cell clear, no step back (s49).
    /// </summary>
    private void AfterDungeonCombat(CombatEncounter encounter, CombatResult result)
    {
        var stepBack = _stepBackAfterCombat;
        _stepBackAfterCombat = false;
        if (CombatReturnMode != GameMode.Dungeon || Dungeon is not { } d || result.PartyWiped)
            return;
        if (!encounter.TreasureForfeited)
            DungeonEncounters.ClearFightCell(_dungeons.LevelOf(d), d.X, d.Y);
        if (stepBack)
            DungeonTraversal.StepBack(d);
    }

    /// <summary>PARTY OPTIONS ended the fight: BEG raises the wandering chance (<c>0x3c0b</c>); BEG and FLEE step back.</summary>
    private void OnPartyOptionEnded(PartyOption option)
    {
        if (option is not (PartyOption.BegMercy or PartyOption.Flee))
            return;
        _stepBackAfterCombat = true;
        if (option == PartyOption.BegMercy && Dungeon is { } d)
            d.WanderingChance++;
    }

    /// <summary>Wilderness water step (OV2 state 10, <c>0x685c</c>): the swim rolls; nobody left → party defeat (state 6).</summary>
    /// <summary>Phantasie II lava (<see cref="Lava"/>, <c>terrain.md</c> §4); a wiped party goes to the defeat screen.</summary>
    private void BurnInLava(OverworldMoveResult result)
    {
        var log = new List<string>();
        var defeated = Lava.Resolve(Party, result.Tile % 10, _overworld.Terrain.For(result.Tile / 10), _rng, log);
        foreach (var line in log)
            Note(line);
        if (defeated)
            PartyDefeatPending = true;
    }

    internal void Swim()
    {
        var log = new List<string>();
        var defeated = Swimming.Resolve(Party, _rng, log, _overworld.Terrain.Roll);
        foreach (var line in log)
            Note(line);
        // s153: "SWIMMING:" and the member lines are one damage report (I Amiga h71 +0xe88: the title through +0x112e,
        // the rows, then requester 21 waits for OK, h86 +0(0x15); DOS 0x68ff: the box waits for a key).
        NoteMapWindow(LogWindowKind.Report, log.Count);
        if (defeated)
            PartyDefeatPending = true;
    }
}
