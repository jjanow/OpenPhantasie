namespace OpenPhantasie.Core;

/// <summary>What the dungeon is waiting for (<see cref="P4Dungeon.Prompt"/>).</summary>
public enum P4DungeonPrompt
{
    /// <summary>The next player action: a move button or another command (<see cref="P4Dungeon.Move"/>, <see cref="P4Dungeon.Act"/>).</summary>
    Action,

    /// <summary>"Space": the message box waits for a key (<c>_ewait</c>; <see cref="P4Dungeon.Continue"/>).</summary>
    Wait,

    /// <summary>A numbered menu (<see cref="P4Dungeon.MenuOptions"/>; <see cref="P4Dungeon.Choose"/>, 1-based).</summary>
    Menu,

    /// <summary>"Letter&gt;": up to 10 characters (<see cref="P4Dungeon.EnterText"/>).</summary>
    Letters,

    /// <summary>"Number&gt;": up to 5 digits (<see cref="P4Dungeon.EnterNumber"/>).</summary>
    Number,

    /// <summary>Pick a party member (<c>_getwho</c>; <see cref="P4Dungeon.Choose"/>, 0-based).</summary>
    Who,

    /// <summary>A fight (<see cref="P4Dungeon.PendingFight"/>) for the combat loop; then <see cref="P4Dungeon.FightOver"/>.</summary>
    Fight,

    /// <summary>The party left for a town or the outdoors (<see cref="P4Dungeon.Transfer"/>).</summary>
    Left,

    /// <summary>Nobody is alive: "The party has been wiped out." (<c>_deadmsg</c>, the game ends).</summary>
    PartyLost,
}

/// <summary>Dungeon commands other than moving (<c>_dmain</c>'s buttons).</summary>
public enum P4DungeonCommand
{
    /// <summary>Profile, party info, cast, use item, system menu: the front end runs the screen, the loop then makes a pass.</summary>
    Other,

    /// <summary>E: Examine (<c>_seainit</c>; D4-041).</summary>
    Examine,
}

/// <summary>A fight an event or the random roll starts: <c>DATM</c> encounter group and battle terrain (<c>_denc</c>).</summary>
public sealed record P4Fight(int Encounter, int Terrain, bool FromEvent);

/// <summary>A move to another map kind (<c>_tele</c>): 0 town, 1 outdoors, 2 dungeon; map number, square, facing.</summary>
public sealed record P4Transfer(int Kind, int Map, int X, int Y, int Dir)
{
    public const int Town = 0, Outdoors = 1, Dungeon = 2;
}

/// <summary>
/// Phantasie IV's location maps (<c>_dungeon</c> → <c>_dload</c> → <c>_dmain</c>; spec
/// <c>reverse-engineering/x68000/phantasie4/logic-specs/events.md</c>): the 8-way move buttons, locked doors, water, the
/// <c>O_0xx</c> event interpreter (<c>_exe_ev</c>, 59 opcodes) with its traps, chests and menus, random encounters, the
/// rotating per-location flag reset and Examine. Headless and resumable: every key the original waits for is a
/// <see cref="Prompt"/>, answered through the methods below; <see cref="Capture"/> saves the whole state, mid-event
/// included. Fights and moves to towns / the outdoors are handed to the caller (<see cref="P4DungeonPrompt.Fight"/>,
/// <see cref="P4DungeonPrompt.Left"/>).
/// </summary>
public sealed partial class P4Dungeon
{
    private readonly GameOptions _options;
    private readonly P4Tables _tables;
    private readonly IRandomSource _rng;
    private readonly Func<int, P4DungeonFile> _maps;
    private readonly Func<int, string> _itemName;

    public P4Dungeon(GameOptions options, P4Tables tables, P4Party party, IRandomSource rng, Func<int, P4DungeonFile> maps,
        Func<int, string> itemName)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        Party = party ?? throw new ArgumentNullException(nameof(party));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _maps = maps ?? throw new ArgumentNullException(nameof(maps));
        _itemName = itemName ?? throw new ArgumentNullException(nameof(itemName));
    }

    public P4Party Party { get; }

    /// <summary>The loaded map (<c>_cmapn</c>).</summary>
    public P4DungeonMap Map => _map ?? throw new InvalidOperationException("No map is loaded.");

    private P4DungeonMap? _map;

    /// <summary>True once a map was loaded (<see cref="Enter"/> or <see cref="Restore"/>).</summary>
    public bool HasMap => _map is not null;

    /// <summary>Sets <c>_dire</c> before any map is loaded (a save taken outside a dungeon keeps the party's facing).</summary>
    public void Face(int dir)
    {
        if (HasMap)
            throw new InvalidOperationException("The facing of a loaded map changes only by moving.");
        Dir = dir & 3;
    }

    /// <summary><c>_cmapx</c> / <c>_cmapy</c>.</summary>
    public int X { get; private set; }

    public int Y { get; private set; }

    /// <summary><c>__dire</c>: 0 N, 1 E, 2 S, 3 W.</summary>
    public int Dir { get; private set; }

    /// <summary><c>_oldcx</c> / <c>_oldcy</c>: where the last move came from (STEPBACK).</summary>
    public int OldX { get; private set; }

    public int OldY { get; private set; }

    /// <summary>
    /// No step taken since the map loaded: <see cref="OldX"/> / <see cref="OldY"/> still hold the last map's square (the
    /// code keeps them stale too), so Teleportation's "previous square" stays put <c>[~]</c>.
    /// </summary>
    public bool NoStepSinceLoad { get; private set; }

    /// <summary>Where the party came onto the current map (D4-052 <c>p4.teleport</c> = <c>entrance</c>, <c>[~]</c>).</summary>
    public int EntryX { get; private set; }

    public int EntryY { get; private set; }

    public P4DungeonPrompt Prompt { get; private set; } = P4DungeonPrompt.Action;

    /// <summary>An event's bytecode or one of its trap / chest / lock helpers is running (<c>_exe_ev</c>).</summary>
    public bool InEvent => _stage is Stage.Event or Stage.Helper;

    /// <summary>The message box's centred top line (TITLE, the location name), or null.</summary>
    public string? BoxTitle { get; private set; }

    /// <summary>The message box's lines.</summary>
    public IReadOnlyList<string> Box => _box;

    private readonly List<string> _box = [];

    /// <summary>The menu shown by <see cref="P4DungeonPrompt.Menu"/>.</summary>
    public IReadOnlyList<string> MenuOptions => _menu;

    private readonly List<string> _menu = [];

    public P4Fight? PendingFight { get; private set; }

    public P4Transfer? Transfer { get; private set; }

    // --- Loop state (the originals' globals) -------------------------------------------------------------------------

    /// <summary><c>__encf</c>: random encounters allowed after the current event.</summary>
    public bool EncountersOn { get; private set; } = true;

    /// <summary><c>__encper</c>: the random-encounter rate in % (ENCPCT).</summary>
    public int EncounterRate { get; private set; }

    /// <summary><c>_mons</c> (24) and <c>__monp</c>: the random-encounter pool. POOLCLR only resets the count.</summary>
    public IReadOnlyList<int> Pool => _pool.Take(_poolCount).ToArray();

    private readonly int[] _pool = new int[PoolSize];
    private int _poolCount;

    public const int PoolSize = 24;

    /// <summary><c>__inext</c>: a queued button (0–8) the next pass runs without a key; 9 = none.</summary>
    private int _inext = NoButton;

    private const int NoButton = 9;

    /// <summary><c>__seadc</c>: Examine steps left (63 at the start).</summary>
    public int ExamineSteps { get; private set; }

    /// <summary><c>_sw</c>: Examine's visit counts, 30 × 30 bytes. Never cleared (<c>_seainit</c>'s loop misses them).</summary>
    private readonly byte[] _visits = new byte[P4DungeonFile.Size * P4DungeonFile.Size];

    /// <summary>D4-041 manual: the search bonus for the rolls of the current pass (after E).</summary>
    private int _searchBonus;

    private bool _searching;

    // --- Entry --------------------------------------------------------------------------------------------------------

    /// <summary><c>_dungeon</c>: load map <paramref name="map"/> with the party on (x, y) facing <paramref name="dir"/>.</summary>
    public void Enter(int map, int x, int y, int dir)
    {
        Transfer = null;
        Dir = dir;
        Load(map, x, y);
        Run();
    }

    /// <summary>
    /// <c>_dload</c>: the rotating flag reset (<c>_dreset</c>), the map file, the movement map and the map-load event
    /// (trigger x = 0x20), which ends with a random-encounter roll like every event.
    /// </summary>
    private void Load(int map, int x, int y)
    {
        ResetLocationFlags();
        _map = new P4DungeonMap(_maps(map));
        X = EntryX = x;
        Y = EntryY = y;
        NoStepSinceLoad = true;
        _stage = Stage.PassTop;
        StartEvent(P4EventTrigger.OnLoad, 0, P4EventTrigger.AnyDirection, After.PassTop);
    }

    /// <summary>
    /// <c>_dreset</c>: story byte 0 is a counter; clear the 20 flags of map <i>counter</i>, then count down (below 1 → 21).
    /// </summary>
    private void ResetLocationFlags()
    {
        var n = Party.StoryFlags[0];
        Array.Clear(Party.LocationFlags, n * P4Party.LocationFlagsPerMap, P4Party.LocationFlagsPerMap);
        var next = n - 1;
        Party.StoryFlags[0] = (byte)(next < 1 ? 21 : next);
    }

    // --- Player input -------------------------------------------------------------------------------------------------

    /// <summary>
    /// A move button, in <c>_bdng</c> order (the numeric pad from the bottom row): 0 SW, 1 S, 2 SE, 3 W, 5 E, 6 NW, 7 N,
    /// 8 NE. A diagonal is two steps: the horizontal one now and the vertical one queued for the next pass (or the
    /// vertical one now and the horizontal one queued when the first is blocked).
    /// </summary>
    public void Move(int button)
    {
        Require(P4DungeonPrompt.Action);
        if (button is < 0 or > 8 or 4)
            throw new ArgumentOutOfRangeException(nameof(button));
        BeginButton(button);
        Run();
    }

    /// <summary>Another command (<see cref="P4DungeonCommand"/>): the loop finishes its pass and runs the square's event again.</summary>
    public void Act(P4DungeonCommand command)
    {
        Require(P4DungeonPrompt.Action);
        if (command == P4DungeonCommand.Examine)
        {
            if (P4Outdoor.ExamineWalks(_options))
                StartExamineWalk();
            else
                _searchBonus = P4Outdoor.ExamineSearchBonus(_options, _tables.Amounts);
        }
        _stage = Stage.LoopEnd;
        Run();
    }

    /// <summary>The "Space" key.</summary>
    public void Continue()
    {
        Require(P4DungeonPrompt.Wait);
        Resume(0);
    }

    /// <summary>A menu line (1-based) or, for <see cref="P4DungeonPrompt.Who"/>, a member (0-based).</summary>
    public void Choose(int n)
    {
        if (Prompt == P4DungeonPrompt.Menu)
        {
            if (n < 1 || n > _menu.Count)
                throw new ArgumentOutOfRangeException(nameof(n));
        }
        else
        {
            Require(P4DungeonPrompt.Who);
            if (n < 0 || n >= Party.Members.Count)
                throw new ArgumentOutOfRangeException(nameof(n));
        }
        Resume(n);
    }

    /// <summary>INPUTS: the text typed (up to 10 characters kept).</summary>
    public void EnterText(string text)
    {
        Require(P4DungeonPrompt.Letters);
        ArgumentNullException.ThrowIfNull(text);
        _vm.Text = text.Length > 10 ? text[..10] : text;
        Resume(0);
    }

    /// <summary>INPUTN: the number typed (up to 5 digits).</summary>
    public void EnterNumber(int number)
    {
        Require(P4DungeonPrompt.Number);
        if (number is < 0 or > 99999)
            throw new ArgumentOutOfRangeException(nameof(number));
        _vm.Number = number;
        Resume(0);
    }

    /// <summary>
    /// The fight is over. A party that survived goes on where the fight started (an event's next instruction, or the rest
    /// of the pass), with Examine stopped and any queued step dropped (<c>_denc</c>); a lost party ends the game.
    /// <paramref name="move"/> (<see cref="P4FightReturn"/>; the code never moves the party here, D4-070: <c>p4.teleport</c> = <c>stay</c>) puts it on another
    /// square of the map first, as STEPBACK does (no event runs for the new square).
    /// </summary>
    public void FightOver(bool partyLost, P4FightMove? move = null)
    {
        Require(P4DungeonPrompt.Fight);
        PendingFight = null;
        if (partyLost)
        {
            Lose();
            return;
        }
        if (move is not null)
            (X, Y) = (move.X, move.Y);
        ExamineSteps = 0;
        _inext = NoButton;
        // _encount clears Levitation (_par+15) at the end of every fight (outdoor.md §6; D4-039 reading s97).
        Party.Levitating = false;
        Party.LevitationSteps = null;
        Prompt = P4DungeonPrompt.Action;
        Run();
    }

    private void Require(P4DungeonPrompt prompt)
    {
        if (Prompt != prompt)
            throw new InvalidOperationException($"The dungeon is waiting for {Prompt}, not {prompt}.");
    }

    private void Resume(int answer)
    {
        _answer = answer;
        Prompt = P4DungeonPrompt.Action;
        Run();
    }

    private int _answer;

    // --- The loop (_dmain) --------------------------------------------------------------------------------------------

    private enum Stage
    {
        /// <summary>Top of a pass: water, then the square's event.</summary>
        PassTop,

        /// <summary>An event's bytecode runs (<see cref="_vm"/>).</summary>
        Event,

        /// <summary>A trap / chest / lock helper waits for an answer (<see cref="_helper"/>).</summary>
        Helper,

        /// <summary>After the event: the random-encounter roll (<c>_rndenc</c>).</summary>
        RandomEncounter,

        /// <summary>The event and its roll are done: carry on with <see cref="_after"/>.</summary>
        AfterEvent,

        /// <summary>A move button is running (<see cref="_button"/>).</summary>
        Button,

        /// <summary>End of a pass: Examine's next step, leaving the map.</summary>
        LoopEnd,

        /// <summary>Waiting for the player's next action.</summary>
        Idle,

        /// <summary>The party left the map or was lost.</summary>
        Stopped,
    }

    /// <summary>What follows an event (the three places <c>_event</c> is called from).</summary>
    private enum After { PassTop, PassInput, LoopEnd }

    private Stage _stage = Stage.Idle;
    private After _after;

    /// <summary>Runs until the loop waits for something.</summary>
    private void Run()
    {
        while (Prompt == P4DungeonPrompt.Action)
        {
            switch (_stage)
            {
                case Stage.PassTop:
                    PassTop();
                    break;
                case Stage.Event:
                    RunEvent();
                    break;
                case Stage.Helper:
                    RunHelper();
                    break;
                case Stage.RandomEncounter:
                    _stage = Stage.AfterEvent;
                    RandomEncounter();
                    break;
                case Stage.AfterEvent:
                    AfterEvent();
                    break;
                case Stage.Button:
                    RunButton();
                    break;
                case Stage.LoopEnd:
                    LoopEnd();
                    break;
                case Stage.Idle:
                    return;
                case Stage.Stopped:
                    return;
            }
        }
    }

    /// <summary>
    /// Top of a <c>_dmain</c> pass: on water (floor 27–30) without Levitation every member may take damage
    /// (<c>_dswim</c>), then the square's event runs with the party's facing.
    /// </summary>
    private void PassTop()
    {
        if (P4DungeonMap.IsWater(Map.Floor(X, Y)) && !Party.Levitating)
        {
            Swim();
            if (_stage == Stage.Stopped)
                return;
        }
        _searching = _searchBonus != 0;
        StartEvent(X, Y, Dir, After.PassInput);
    }

    /// <summary>
    /// <c>_dswim</c>: every member rolls d = rand(level + 1) + 1; a living member then takes d when rand(100) &gt; Swim.
    /// </summary>
    private void Swim()
    {
        for (var i = 0; i < Party.Members.Count; i++)
        {
            var m = Party.Members[i];
            var d = P4Dice.Rand(_rng, m.Level + 1) + 1;
            if (m.Alive && P4Dice.Rand(_rng, 100) > m.Skill(P4Skill.Swim) && Damage(i, d))
                return;
        }
    }

    private void AfterEvent()
    {
        _searching = false;
        _searchBonus = 0;
        switch (_after)
        {
            case After.PassTop:
                _stage = Stage.PassTop;
                break;
            case After.LoopEnd:
                _stage = Stage.LoopEnd;
                break;
            default:
                if (Transfer is not null)
                {
                    _stage = Stage.LoopEnd; // _cmovf: skip input, finish the pass
                    break;
                }
                if (_inext < NoButton)
                {
                    var b = _inext;
                    _inext = NoButton;
                    BeginButton(b);
                }
                else
                    _stage = Stage.Idle;
                break;
        }
    }

    /// <summary>End of the pass: Examine's next step (<c>_seasel</c>), then leave the map or start the next pass.</summary>
    private void LoopEnd()
    {
        if (ExamineSteps > 0)
            ExamineStep();
        if (Transfer is { } t)
        {
            if (t.Kind == P4Transfer.Dungeon)
            {
                // _game: the map kind is still 2, so _dungeon loads the new map.
                Transfer = null;
                Dir = t.Dir;
                Load(t.Map, t.X, t.Y);
                return;
            }
            _stage = Stage.Stopped;
            Prompt = P4DungeonPrompt.Left;
            return;
        }
        _stage = Stage.PassTop;
    }

    private void Lose()
    {
        Party.Sounds.Note(P4Sounds.WipedOut); // _deadmsg 0x1E484: _sdbeep before its key
        _stage = Stage.Stopped;
        PendingFight = null;
        _box.Clear();
        BoxTitle = null;
        _box.Add(GameText.S("p4x.0x44097"));
        _box.Add(GameText.S("p4x.0x440b0"));
        Prompt = P4DungeonPrompt.PartyLost;
    }

    // --- Damage, waits, fights ----------------------------------------------------------------------------------------

    /// <summary>
    /// <c>_ddamage</c>: HP −= d; d ≥ HP → HP 0 and Dead. A living member is then Okay again (<c>_healch</c> outside combat).
    /// True when nobody is left alive (the game ends, <c>_deadmsg</c>).
    /// </summary>
    private bool Damage(int index, int d)
    {
        var m = Party.Members[index];
        Party.Sounds.Note(P4Sounds.MemberDamaged); // _ddamage 0x1A348: _sddam after HP -= d
        if (d < m.Hp)
            m.Hp -= d;
        else
        {
            m.Hp = 0;
            m.Condition = P4Member.Dead;
        }
        if (m.Alive)
            m.Condition = P4Member.Okay;
        if (Party.AnyAlive)
            return false;
        Lose();
        return true;
    }

    /// <summary><c>_ewait</c>: "Space"; it also stops Examine and drops a queued step.</summary>
    private void Wait()
    {
        Party.Sounds.Note(P4Sounds.MessageWait); // _ewait 0x1887E: _sdbeep before _mgetc
        ExamineSteps = 0;
        _inext = NoButton;
        Prompt = P4DungeonPrompt.Wait;
    }

    private void ShowMenu(IEnumerable<string> options)
    {
        Party.Sounds.Note(P4Sounds.EventMenu); // _xmenu 0x18F46: _sdbeep before _mgetc
        _menu.Clear();
        _menu.AddRange(options);
        ExamineSteps = 0;
        _inext = NoButton;
        Prompt = P4DungeonPrompt.Menu;
    }

    private void ClearBox()
    {
        _box.Clear();
        BoxTitle = null;
    }

    private void Print(string text)
    {
        ClearBox();
        _box.AddRange(text.Split('$'));
    }

    /// <summary>
    /// <c>_denc</c>: no fight on water; otherwise the fight on battle terrain 0 (floor 3 / 31 / 35) or 1. True when one starts.
    /// </summary>
    private bool StartFight(int encounter, bool fromEvent)
    {
        if (Map.FightRefused(X, Y))
            return false;
        PendingFight = new P4Fight(encounter, Map.BattleTerrain(X, Y), fromEvent);
        Prompt = P4DungeonPrompt.Fight;
        return true;
    }

    /// <summary>
    /// <c>_rndenc</c>, after every event: when encounters are on and rand(100) (rand(400) during Examine) ≤ the rate, a fight
    /// with pool entry rand(pool size) − 1 (an empty pool reads entry 0 of the stale array).
    /// </summary>
    private void RandomEncounter()
    {
        if (!EncountersOn || _stage == Stage.Stopped)
            return;
        if (P4Dice.Rand(_rng, ExamineSteps != 0 ? 400 : 100) > EncounterRate)
            return;
        StartFight(_pool[P4Dice.Rand(_rng, _poolCount) - 1], fromEvent: false);
    }

    /// <summary>The last living member (party order) whose rand(100) ≤ the skill (+ the search bonus); null when none.</summary>
    private int? LastSuccess(P4Skill skill, int bonus = 0)
    {
        int? who = null;
        for (var i = 0; i < Party.Members.Count; i++)
        {
            var m = Party.Members[i];
            if (m.Alive && P4Dice.Rand(_rng, 100) <= m.Skill(skill) + bonus)
                who = i;
        }
        return who;
    }

    private int SearchBonus => _searching ? _searchBonus : 0;
}
