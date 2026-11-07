using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

public partial class Game1
{
    // s139 / s140: I Amiga's requester 23 over the map (P1MapMessageRun): the boxes a wilderness step's encounter (h74) or a
    // dungeon step's events (h41) open, then the fight. s141: each box's sound first (h89 blocks), the party on the cell it
    // stood on until Core's move (op 12, a jammed lock). s144: requester 21 in the dungeon: a MESS text's timed window is a
    // step of the run like a box; a damage report waits for a key or a click (OK), between the boxes or after the last.
    private P1MapMessageRun? _mapRun;
    private double _mapRunStartMs;
    private double _mapRunSoundsTo;
    private bool _mapRunWaiting;
    private long _mapRunFightMark;
    private long _mapMoveMark;
    private IReadOnlySet<string> _mapBoxed = new HashSet<string>();

    /// <summary>True while a map box is up (or the run's last box has not closed yet).</summary>
    private bool MapBoxesUp => _mapRun is not null;

    /// <summary>
    /// The mode the screen shows: the map (overworld or dungeon) while the step's boxes play, though Core is already in the fight.
    /// </summary>
    private GameMode ViewMode(GameSession s) =>
        (MapBoxesUp || DosPagesUp || _p1FightLoading) && s.Mode == GameMode.Combat ? s.CombatReturnMode : s.Mode; // s154: the pictures' read, DOS's pages

    /// <summary>
    /// After a wilderness or dungeon step or a prompt's answer under AMIGA SCREENS with I's pack: the lines the step logged
    /// that the Amiga shows in requester 23 over the map (h74 / h41, pack <c>map_message</c>) play as boxes first; a fight's
    /// screen waits for the last box. The remaster's D-058 flashes are no box (no wait, user s140): the combat screen's
    /// line shows them. True when boxes were planned (the caller leaves the fight's start to them).
    /// </summary>
    private bool PlanMapMessages(GameSession s, long mark)
    {
        _mapBoxed = new HashSet<string>(); // an earlier step's boxed lines go back on the line (or AMIGA SCREENS went off)
        var moveMark = Math.Min(_mapMoveMark, s.DungeonMoveSequence); // a loaded session counts from 0 again
        _mapMoveMark = s.DungeonMoveSequence; // this step's moves are read now or never
        _stepMoveMark = moveMark; // s156: DOS's pages read the same moves (PlanDosPages, after this)
        if (!CurrentGameSettings.OriginalScreens || _assets?.Pack?.MapMessage is not { } layout || _combatUi.IsPresenting)
            return false;
        return StartMapMessages(P1MapMessageRun.ForStep(layout, s, mark, MapPolls(), Environment.TickCount64,
            P1SoundSeconds, moveMark), mark);
    }

    /// <summary>h3: the Speed's polls, the combat screen's (the boot value until a Speed is picked).</summary>
    private int MapPolls() => _combatUi.P1Layout is { } field ? field.LinePolls(_combatUi.P1Speed?.Invoke() ?? 0) : 0;

    private bool StartMapMessages(Data.MapMessageLayout layout, IReadOnlyList<string> lines, long fightMark) =>
        StartMapMessages(P1MapMessageRun.Plan(layout, lines, MapPolls(), Environment.TickCount64, soundSeconds: P1SoundSeconds),
            fightMark);

    private bool StartMapMessages(P1MapMessageRun? run, long fightMark)
    {
        if (run is null)
            return false;
        StartMapPart(run);
        _mapRunFightMark = fightMark;
        _mapBoxed = run.BoxedLines;
        // The boxes show their lines: the note under a prompt (a disarm's "%s FAILS", a menu answer's surprise) leaves them out.
        if (_promptNote.Length > 0 && _promptNote == string.Join(" ", _promptNoteLines))
            _promptNote = string.Join(" ", _promptNoteLines.Where(n => !run.BoxedLines.Contains(n.Trim())));
        _walk = null; // the requester takes the mouse: a map walk stops
        return true;
    }

    private void StartMapPart(P1MapMessageRun run)
    {
        _mapRun = run;
        _mapRunStartMs = _updateSeconds * 1000;
        _mapRunSoundsTo = -1;
        _mapRunWaiting = false;
        PlayMapSounds(0);
    }

    /// <summary>s141: the boxes' sounds whose start has come (I Amiga's h89 <c>+0xee</c> / <c>+0xe0</c>, Sound switch on).</summary>
    private void PlayMapSounds(double elapsedMs)
    {
        if (_mapRun is not { } run || elapsedMs < _mapRunSoundsTo)
            return;
        var from = _mapRunSoundsTo < 0 ? double.NegativeInfinity : _mapRunSoundsTo;
        _mapRunSoundsTo = elapsedMs + 0.001;
        foreach (var sound in run.SoundsBetween(from, _mapRunSoundsTo))
            if (_settings.Sound && _assets?.Pack?.DosoundEffects is { } effects)
                _effects.Play(effects, sound);
    }

    /// <summary>
    /// While the map boxes play they own every key and click (h80 <c>+0x546</c> replies and drops them). s141 / s144: a part
    /// that ends in a damage report waits for <paramref name="pressed"/> (a key or a click: requester 21's OK; the Amiga
    /// takes only the click `[~]`), then the next part plays, or the run ends. When the last box closes, a fight Core already started gets its screen. True while the boxes
    /// own the frame.
    /// </summary>
    private bool UpdateMapMessages(GameSession s, bool pressed)
    {
        if (_mapRun is not { } run)
            return false;
        var elapsed = _updateSeconds * 1000 - _mapRunStartMs;
        // s153: a click on a row of the timed MESS window ends it (a GADGETUP of a RELVERIFY row gadget ends h80 +0x546);
        // what follows is timed again from now (before the sounds, so the next box's sound still plays).
        if (_mouse.LeftClick is { } click && _hits.Find<MapWindowRowTag>(click) is not null)
            _mapRun = run = run.EndWindowAt(elapsed, Environment.TickCount64 - elapsed);
        PlayMapSounds(elapsed);
        if (elapsed < run.LengthMs)
            return true;
        if (run.Waits)
        {
            if (!_mapRunWaiting)
            {
                _mapRunWaiting = true; // the press that ends the wait must come after it opened
                return true;
            }
            if (!pressed)
                return true;
            if (run.Next(Environment.TickCount64) is { } next)
            {
                StartMapPart(next);
                return true;
            }
            // s144: a report after the last box (a trap's): OK ends the run.
        }
        _mapRun = null;
        _mapRunWaiting = false;
        if (s.Mode == GameMode.Combat && !_combatUi.IsPresenting && !_p1FightLoading)
        {
            NoteEncounterMonsters(s);
            _fightMark = _mapRunFightMark;
            StartFightScreen(s); // s154: I Amiga's monster pictures read first
        }
        return true;
    }

    /// <summary>s144: the map box or requester-21 MESS window up now, or null.</summary>
    private P1MapMessageRun.Box? MapBox() =>
        _mapRun?.BoxAt(_updateSeconds * 1000 - _mapRunStartMs);

    /// <summary>s141: the lines shown between two dungeon parts while they wait for a key or a click, or null.</summary>
    private IReadOnlyList<string>? MapWaitLines() => _mapRunWaiting ? _mapRun?.WaitLines : null;

    /// <summary>
    /// s141: the dungeon cell the map draws the party on: the cell it stood on until Core moved it, while the boxes before
    /// that move play (h41: op 12's and a jammed lock's box come before the move); null for Core's own position.
    /// </summary>
    private (int X, int Y)? MapPartyAt(DungeonState d) =>
        (_mapRun?.PartyAt(_updateSeconds * 1000 - _mapRunStartMs) ?? DosPagePartyAt()) is { } cell && cell.LevelId == d.LevelId
            ? (cell.X, cell.Y)
            : null;

    private long _stepMoveMark;

    /// <summary>Drops the boxes (a load, the main menu).</summary>
    private void CloseMapMessages()
    {
        ResetP1Holds(); // s154: a load or the main menu drops a pending step, question hold or pictures' read
        _mapRun = null;
        _mapRunWaiting = false;
        _mapBoxed = new HashSet<string>();
    }
}
